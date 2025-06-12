using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    #region [class] mem db
    public abstract class MemDb
    {
        #region static internals
        private static List<MemDbConfiguration> _configurations;
        private static Dictionary<string, FileStream> _openDatasets;
        private static Lock _lock;
        #endregion

        #region static ctor
        static MemDb()
        {
            _configurations = new List<MemDbConfiguration>();
            _openDatasets = new();
            _lock = new();
        }
        #endregion

        #region get configuration
        public static MemDbConfiguration GetConfiguration(string datasetName)
        {
            var config = _configurations.Find(c => c.DatasetName == datasetName);
            return config;
        }
        #endregion

        #region defrag
        public static void Defrag(string datasetName)
        {
            lock (_lock)
            {
                if (_openDatasets.ContainsKey(datasetName))
                    throw new InvalidOperationException($"MemDb instance for provided {nameof(datasetName)} '{datasetName}' is currently open.");
            }

            MemDbConfiguration config = MemDb.GetConfiguration(datasetName);
            if (config is null)
                throw new ArgumentException($"No configuration registered for provided datasetName: {datasetName}");
            
            if (config.ShouldArchive)
            {
                IMemDbArchiver archiver = new MemDbArchiver(config);
                archiver.Archive();
            }
            IMemDbDefragmenter defragmenter = new MemDbDefragmenter(config);
            defragmenter.Defrag();
        }
        #endregion

        #region read archive
        public static IEnumerable<MemDbArchivedRecord<T>> ReadArchive<T>(string datasetName) where T : class
        {
            MemDbConfiguration config = MemDb.GetConfiguration(datasetName);
            if (config is null)
                throw new ArgumentException($"No configuration registered fr provided datasetName: {datasetName}");

            if (!config.ShouldArchive)
                throw new InvalidOperationException($"Configuration for provided dataset '{datasetName}' is not configured to archive on defrag.");

            var configOfT = config.EnsureGenericType<T>(config);
            var archReader = new MemDbArchiveReader<T>(configOfT);

            //we want this entire stack to be yeild return.  Archive record counts could be large, so we
            //let the consumer analyze each record to find the set they are looking for (and toss the rest).
            var enumerator = archReader.ReadArchive();
            foreach (MemDbRecord<T> r in enumerator)
            {
                yield return new MemDbArchivedRecord<T>(r.Id, r.State, r.StateSetAt, r.CreatedAt, r.IsEncrypted, r.Value);
            }
        }
        #endregion

        #region restore
        public static void Restore(string datasetName, long utcTimestamp, string outputDirectory)
        {
            if (datasetName is null)
                throw new ArgumentNullException(nameof(datasetName));

            if (utcTimestamp >= DateTime.UtcNow.ToBinary())
                throw new ArgumentException($"Provided {nameof(utcTimestamp)} must represent a timestamp in the past.");

            lock (_lock)
            {
                if (_openDatasets.ContainsKey(datasetName))
                    throw new InvalidOperationException($"MemDb instance for provided {nameof(datasetName)} '{datasetName}' is currently open.");
            }

            MemDbConfiguration config = MemDb.GetConfiguration(datasetName);
            if (config is null)
                throw new ArgumentException($"No configuration registered for provided datasetName: {datasetName}");

            if (!config.ShouldArchive)
                throw new InvalidOperationException($"{nameof(Restore)} cannot be run on a MemDb instance that was not configured for Archive.");

            var restorer = new MemDbRestorer(config, outputDirectory, utcTimestamp, true);

            restorer.Restore();
        }
        #endregion

        #region configure for
        public static IMemDBConfigurationBuilder<T> ConfigureFor<T>(string datasetName) where T : class
        {
            return new MemDbConfiguration<T>(datasetName, MemDb.RegisterConfiguration);
        }

        public static IMemDBConfigurationBuilder<T> ConfigureFor<T>(string datasetName, string path) where T : class
        {
            return new MemDbConfiguration<T>(datasetName, path, MemDb.RegisterConfiguration);
        }
        #endregion

        #region remove configuration for
        public static void RemoveConfiguationFor(string datasetName)
        {
            lock (_lock)
            {
                int at = _configurations.FindIndex(c => string.Compare(c.DatasetName, datasetName, true) == 0);
                if (at < 0)
                    throw new ArgumentException($"No configuration currently registered for provided dataset name '{datasetName}'", nameof(datasetName));

                if (_openDatasets.ContainsKey(datasetName))
                    throw new InvalidOperationException($"Cannot remove configuration for dataset '{datasetName}', the dataset is currently open.");

                _configurations.RemoveAt(at);
            }
        }
        #endregion

        #region contains configuratoin for
        public static bool ContainsConfigurationFor(string datasetName)
        {
            lock (_lock)
            {
                int at = _configurations.FindIndex(c => string.Compare(c.DatasetName, datasetName, true) == 0);
                return at > -1;
            }
        }
        #endregion

        #region register configuration
        private static void RegisterConfiguration<T>(MemDbConfiguration<T> configuration) where T : class
        {
            if (_configurations.Exists(c => string.Compare(c.DatasetName, configuration.DatasetName, true) == 0))
                throw new MemDbConfigurationException("Cannot register configuration with duplicate dataset name of existing configuration: " + configuration.DatasetName);

            _configurations.Add(configuration);
        }
        #endregion

        #region open
        public static MemDb<T> Open<T>(string datasetName) where T : class
        {
            if (datasetName is null)
                throw new ArgumentException(nameof(datasetName));

            lock (_lock)
            {
                //ensure the requested db is not already open within this currently executing process...
                if (_openDatasets.ContainsKey(datasetName))
                    throw new InvalidOperationException($"MemDb instance for provided {nameof(datasetName)} '{datasetName}' is already open.");

                MemDbConfiguration config = MemDb.GetConfiguration(datasetName);

                if (config is null)
                    throw new ArgumentException($"No configuration registered for provided {nameof(datasetName)}: {datasetName}");

                var configOfT = config.EnsureGenericType<T>(config);

                FileStream lockFile = null;
                MemDb<T> memDb = null;
                try
                {
                    //ensure no other process has the db locked...
                     lockFile = config.IsPersisted ? MemDb.InitializeLockFile(config) : null;
                    memDb = new MemDb<T>(configOfT);
                    _openDatasets.Add(datasetName, lockFile);
                }
                catch//if ex is thrown during open attempt, ensure lock file is cleaned up
                {
                    lockFile?.Dispose();
                    throw;
                }

                return memDb;
            }
        }
        #endregion

        #region initialize lock file
        private static FileStream InitializeLockFile(MemDbConfiguration config)
        {
            if (!Directory.Exists(config.Path))
                Directory.CreateDirectory(config.Path);

            //if any other process has this MemDb file set open, init of this filestream
            //will throw an exception prior to this process opening the file set.
            var lockFile = new FileStream(config.GetFullLockFilePath(), new FileStreamOptions()
            {
                Access = FileAccess.Write,
                Mode = FileMode.CreateNew,//should create new, NOT overwrite
                Share = FileShare.None,//do not share
                PreallocationSize = 0,//no preallocation
                BufferSize = 0,//disable buffer
                Options = FileOptions.DeleteOnClose//delete on dispose or when file handle is orphaned (0 refs)
            });

            return lockFile;
        }
        #endregion

        #region close
        protected static void Close(string datasetName)
        {
            lock (_lock)
            {
                _openDatasets.Remove(datasetName, out FileStream lockFile);
                lockFile?.Dispose();
            }
        }
        #endregion
    }
    #endregion

    #region [class] mem db<T>
    public class MemDb<T> : MemDb, IDisposable, IMemDbAcceessor<T> where T : class
    {
        #region internals
        private string _datasetName;
        private IMemDbCache<T> _cache;
        private bool _isEncryptionReady;
        private bool _isSnapshotReady;
        private bool _isClosed;
        #endregion

        #region ctors
        internal MemDb(MemDbConfiguration<T> config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            _datasetName = config.DatasetName;
            _isEncryptionReady = config.IsEncryptionReady;
            _isSnapshotReady = config.IsSnapshotReady;
            _cache = config.GetCache();
        }
        #endregion

        #region flush
        public void Flush()
        {
            if (!_isClosed)
            {
                _cache.Flush();
            }
        }
        #endregion

        #region purge cache
        public (int stale, int deleted) PurgeCache()
        {
            return _cache.Purge();
        }
        #endregion

        #region exists
        public bool Exists(Func<T, bool> where)
        {
            return _cache.Exists(where);
        }

        public bool Exists(long id)
        {
            return _cache.Exists(id);
        }
        #endregion

        #region count
        public int Count()
        {
            return _cache.Count();
        }

        public int Count(Func<T, bool> selector)
        {
            return _cache.Count(selector);
        }
        #endregion

        #region find
        public T Find(Func<T, bool> where)
        {
            return _cache.Find(where);
        }

        public T Find(long id)
        {
            return (_cache as MemDbCache<T>).Find(id);
        }
        #endregion

        #region find all
        public T[] FindAll(Func<T, bool> where)
        {
            return _cache.FindAll(where);
        }

        public T[] FindAll(params long[] ids)
        {
            return _cache.FindAll(ids);
        }
        #endregion

        #region insert
        public void Insert(T record, bool encrypt = false)
        {
            this.Insert(record, null, encrypt);
        }

        public void Insert(T record, Action<long> idCallback, bool encrypt = false)
        {
            if (encrypt && !_isEncryptionReady)
                throw new NotEncryptionReadyException($"{nameof(MemDbConfiguration)} for '{_datasetName}' does not contain an encryption key or password.");

            _cache.Insert(record, idCallback, encrypt);
        }
        #endregion

        #region update
        public int Update(Action<T> apply, Func<T, bool> where)
        {
            return _cache.Update(apply, where);
        }

        public bool Update(Action<T> apply, long id)
        {
            return _cache.Update(apply, id);
        }
        #endregion

        #region delete
        public int Delete(Func<T, bool> where)
        {
            return _cache.Delete(where);
        }

        public bool Delete(long id)
        {
            return _cache.Delete(id);
        }
        #endregion

        #region query
        public MemDbExpression<T> Query()
        {
            return _cache.Query();
        }
        #endregion

        #region query via index
        public IMemDbIndexExpressionRoot<T, Y> QueryViaIndex<Y>(string indexName) where Y : IConvertible
        {
            return _cache.QueryViaIndex<Y>(indexName);
        }
        #endregion

        #region resolve statistics
        public MemDbStatistics ResolveStatistics(Stats statistics)
        {
            return _cache.ResolveStatistics(statistics);
        }
        #endregion

        #region snapshot
        public DateTime Snapshot()
        {
            if (_isClosed)
                throw new InvalidOperationException($"{nameof(Snapshot)} is not available on a closed database.");

            if (!_isSnapshotReady)
                throw new InvalidOperationException($"{nameof(MemDbConfiguration)} for '{_datasetName}' does not contain a {nameof(MemDbConfiguration<T>.SnapshotTo)} directory path.");

            return _cache.Snapshot();
        }
        #endregion

        #region close
        private void Close()
        {
            _isClosed = true;
            _cache.Dispose();
            MemDb.Close(_datasetName);
        }
        #endregion

        #region dispose
        public void Dispose()
        {
            if (!_isClosed)
            {
                this.Close();
                GC.SuppressFinalize(this);
            }
        }
        #endregion

        #region finalizer
        ~MemDb()
        {
            if (!_isClosed)
            {
                this.Close(); //emergency catch all to save un-synced records if process dies...
            }
        }
        #endregion
    }
    #endregion
}