using System;
using System.IO;
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

        #region static ctor
        static MemDb()
        {
            _configurations = new List<MemDbConfiguration>();
            _openDatasets = new List<string>();
            _lock = new();
        }
        #endregion

        #region get configuration
        public static MemDbConfiguration GetConfiguration(string datasetName)
        {
            var config = _configurations.Find(c => c.DatasetName == datasetName);
            config?.Initialize();
            return config;
        }
        #endregion

        #region defrag
        public static void Defrag(string datasetName)
        {
            lock (_lock)
            {
                if (_openDatasets.Contains(datasetName))
                    throw new InvalidOperationException($"MemDb instance for provided {nameof(datasetName)} '{datasetName}' is currently open.");
            }

            MemDbConfiguration config = MemDb.GetConfiguration(datasetName);
            try
            {
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
            finally
            {
                config?.Clear();
            }
        }
        #endregion

        #region read archive
        public static IEnumerable<MemDbArchivedRecord<T>> ReadArchive<T>(string datasetName) where T : class
        {
            MemDbConfiguration config = MemDb.GetConfiguration(datasetName);
            try
            {
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
            finally
            {
                config.Clear();
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
                if (_openDatasets.Contains(datasetName))
                    throw new InvalidOperationException($"MemDb instance for provided {nameof(datasetName)} '{datasetName}' is currently open.");
            }

            MemDbConfiguration config = MemDb.GetConfiguration(datasetName);
            try
            {
                if (config is null)
                    throw new ArgumentException($"No configuration registered for provided datasetName: {datasetName}");

                if (!config.ShouldArchive)
                    throw new InvalidOperationException($"{nameof(Restore)} cannot be run on a MemDb instance that was not configured for Archive.");

                var restorer = new MemDbRestorer(config, outputDirectory, utcTimestamp, true);

                restorer.Restore();
            }
            finally
            {
                config?.Clear();
            }
        }
        #endregion

        #region configure for
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

                if (_openDatasets.Contains(datasetName))
                    throw new InvalidOperationException($"Cannot remove configuration for dataset '{datasetName}', the dataset is currently open.");

                _configurations.RemoveAt(at);
            }
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
                    var config = MemDb.GetConfiguration(datasetName);
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
        private IMemDbCache<T> _cache;
        private bool _isEncryptionReady;
        private bool _isClosed;
        #endregion

        #region interface
        #endregion

        #region constructors
        private MemDb(MemDbConfiguration<T> config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            _datasetName = config.DatasetName;
            _cache = config.GetCache();
            _isEncryptionReady = config.GetEncryptor() is not null;
        }
        #endregion

        #region open
        internal static MemDb<T> Open(string datasetName)
        {
            MemDbConfiguration config = MemDb.GetConfiguration(datasetName);

            if (config is null)
                throw new ArgumentException($"No configuration registered for provided datasetName: {datasetName}");

            var configOfT = config.EnsureGenericType<T>(config);

            var memDb = new MemDb<T>(configOfT);

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

        #region resolve statistics
        public MemDbStatistics ResolveStatistics(Stats statistics)
        {
            return _cache.ResolveStatistics(statistics);
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