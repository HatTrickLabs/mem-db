using System;
using System.Collections.Generic;
using System.Linq;

namespace HatTrick.InMemDb
{
    #region [class] memdb
    public abstract class MemDb
    {
        #region static internals
        private static List<MemDbConfiguration> _configurations;
        #endregion

        #region static interface
        protected static List<MemDbConfiguration> Configurations => _configurations;
        #endregion

        #region static constructor
        static MemDb()
        {
            _configurations = new List<MemDbConfiguration>();
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
        public static void ReadArchive<T>(string datasetName) where T : class, new()
        {
            MemDbConfiguration config = MemDb.Configurations.Find(r => r.DatasetName == datasetName);

            if (config is null)
                throw new ArgumentException($"No configuration registered fr provided datasetName: {datasetName}");

            if (!config.ShouldArchive)
                throw new InvalidOperationException($"Configuration for provided dataset '{datasetName}' is not configured to archive on defrag.");

            var configOfT = config.EnsureGenericType<T>(config);
            var archReader = new MemDbArchiveReader<T>(config.DatasetName, config.ArchivePath, configOfT.GetSerializer(), configOfT.GetEncryptor());
            var archives = archReader.ReadArchiveRecords().ToArray();
        }
        #endregion

        #region configure for
        public static MemDbConfiguration<T> ConfigureFor<T>(string datasetName, string path) where T : class, new()
        {
            return new MemDbConfiguration<T>(datasetName, path, MemDb.RegisterConfiguration);
        }
        #endregion

        #region register
        private static void RegisterConfiguration<T>(MemDbConfiguration<T> configuration) where T : class, new()
        {
            if (_configurations.Exists(c => string.Compare(c.DatasetName, configuration.DatasetName, true) == 0))
                throw new MemDbConfigurationException("Cannot register configuration with duplicate dataset name of existing configuration: " + configuration.DatasetName);

            _configurations.Add(configuration);
        }
        #endregion

        #region open
        public static MemDb<T> Open<T>(string datasetName) where T : class, new()
        {
            if (datasetName is null)
                throw new ArgumentException(nameof(datasetName));

            return MemDb<T>.Open(datasetName);
        }
        #endregion
    }
    #endregion

    #region [class] memdb<T>
    public class MemDb<T> : MemDb, IDisposable, IMemDbAcceessor<T> where T : class, new()
    {
        #region internals
        private IMemDbCacher<T> _cache;
        private bool _isClosed;
        #endregion

        #region interface
        #endregion

        #region constructors
        private MemDb(IMemDbCacher<T> cacher)
        {
            if (cacher is null)
                throw new ArgumentNullException(nameof(cacher));

            _cache = cacher;
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

            return new MemDb<T>(configOfT.GetCache());
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