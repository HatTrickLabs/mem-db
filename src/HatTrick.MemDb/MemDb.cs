using System;
using System.Threading;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    #region [class] memdb
    public abstract class MemDb
    {
        #region static internals
        private static List<MemDbConfiguration> _configurations;
        private static List<string> _openDatasets;
        private static Lock _lock;
        #endregion

        #region static interface
        protected static List<MemDbConfiguration> Configurations => _configurations;
        #endregion

        #region static ctor
        static MemDb()
        {
            _configurations = new List<MemDbConfiguration>();
            _openDatasets = new List<string>();
            _lock = new();
        }
        #endregion

        #region defrag
        public static void Defrag(string datasetName)
        {
            MemDbConfiguration config = MemDb.Configurations.Find(r => r.DatasetName == datasetName);

            if (config is null)
                throw new ArgumentException($"No configuration registered fr provided datasetName: {datasetName}");

            if (config.ShouldArchive)
            {
                IMemDbArchiver archiver = new MemDbArchiver(config.DatasetName, config.Path, config.ArchivePath);
                archiver.Archive();
            }

            IMemDbDefragmenter defragmenter = new MemDbDefragmenter(config.DatasetName, config.Path);

            defragmenter.Defrag();
        }
        #endregion

        #region read archive
        public static IEnumerable<MemDbArchivedRecord<T>> ReadArchive<T>(string datasetName) where T : class
        {
            MemDbConfiguration config = MemDb.Configurations.Find(r => r.DatasetName == datasetName);

            if (config is null)
                throw new ArgumentException($"No configuration registered fr provided datasetName: {datasetName}");

            if (!config.ShouldArchive)
                throw new InvalidOperationException($"Configuration for provided dataset '{datasetName}' is not configured to archive on defrag.");

            var configOfT = config.EnsureGenericType<T>(config);
            var archReader = new MemDbArchiveReader<T>(config.DatasetName, config.ArchivePath, configOfT.GetSerializer(), configOfT.GetEncryptor());

            //we want this entire stack to be yeild return.  Archive record counts could be huge, so we
            //let the consumer analyze each record to find the set they are looking for (and toss the rest).
            foreach (MemDbRecord<T> r in archReader.ReadArchiveRecords())
            {
                yield return new MemDbArchivedRecord<T>(r.Id, r.State, r.StateSetAt, r.Value);
            }
        }
        #endregion

        #region configure for
        public static IMemDBConfigurationBuilder<T> ConfigureFor<T>(string datasetName, string path) where T : class
        {
            return new MemDbConfiguration<T>(datasetName, path, MemDb.RegisterConfiguration);
        }
        #endregion

        #region register
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
                if (_openDatasets.Contains(datasetName))
                    throw new InvalidOperationException($"MemDb instance for provided {nameof(datasetName)} '{datasetName}' is already open.");

                var memDb = MemDb<T>.Open(datasetName);
                _openDatasets.Add(datasetName);
                return memDb;
            }
        }
        #endregion

        #region close
        protected static void Close(string datasetName)
        {
            lock (_lock)
            {
                if (_openDatasets.Remove(datasetName))
                {
                    var config = _configurations.Find(c => string.Compare(c.DatasetName, datasetName, true) == 0);
                    config?.Clear();
                }
            }
        }
        #endregion
    }
    #endregion

    #region [class] memdb<T>
    public class MemDb<T> : MemDb, IDisposable, IMemDbAcceessor<T> where T : class
    {
        #region internals
        private string _datasetName;
        private IMemDbCacher<T> _cache;
        private bool _isEncryptionReady;
        private bool _isClosed;
        #endregion

        #region interface
        #endregion

        #region constructors
        private MemDb(string datasetName, IMemDbCacher<T> cacher)
        {
            _datasetName = datasetName ?? throw new ArgumentNullException(nameof(datasetName));
            _cache = cacher ?? throw new ArgumentNullException(nameof(cacher));
        }
        #endregion

        #region open
        internal static MemDb<T> Open(string datasetName)
        {
            MemDbConfiguration config = MemDb.Configurations.Find(r => r.DatasetName == datasetName);

            if (config is null)
                throw new ArgumentException($"No configuration registered for provided datasetName: {datasetName}");

            var configOfT = config.EnsureGenericType<T>(config);

            configOfT.Initialize();

            var memDb = new MemDb<T>(config.DatasetName, configOfT.GetCache());

            memDb._isEncryptionReady = configOfT.GetEncryptor() is not null;

            return memDb;
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

        #region exists
        public bool Exists(Func<T, bool> where)
        {
            return _cache.Exists(where);
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
        #endregion

        #region find all
        public T[] FindAll(Func<T, bool> where)
        {
            return _cache.FindAll(where);
        }
        #endregion

        #region insert
        public void Insert(T record, bool encrypt = false)
        {
            this.Insert(record, null, encrypt);
        }

        public void Insert(T record, Action<uint> idCallback, bool encrypt = false)
        {
            if (encrypt && !_isEncryptionReady)
                throw new NotEncryptionReadyException($"MemDb config for '{_datasetName}' does not contain an encryption key or password.");

            _cache.Insert(record, idCallback, encrypt);
        }
        #endregion

        #region update
        public int Update(Action<T> apply, Func<T, bool> where)
        {
            return _cache.Update(apply, where);
        }
        #endregion

        #region delete
        public int Delete(Func<T, bool> where)
        {
            return _cache.Delete(where);
        }
        #endregion

        #region query
        public MemDbExpression<T> Query()
        {
            return _cache.Query();
        }
        #endregion

        #region close
        private void Close(bool isFinalizer = false)
        {
            _cache.Dispose();
            MemDb.Close(_datasetName);
            _isClosed = true;
        }
        #endregion

        #region dispose
        public void Dispose()
        {
            if (!_isClosed)
            {
                this.Close();
            }
        }
        #endregion

        #region finalizer
        ~MemDb()
        {
            if (!_isClosed)
            {
                this.Close(true); //emergency catch all to save un-synced records if process dies...
            }
        }
        #endregion
    }
    #endregion
}