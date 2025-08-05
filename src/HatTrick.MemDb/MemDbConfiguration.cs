using System;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace HatTrick.InMemDb
{
    #region [class] mem db configuration
    public abstract class MemDbConfiguration
    {
        #region const
        public const int MinPasswordLength = 10;
        public const int DefaultFlushIntervalSeconds = 5;
        public const int MaxFlushIntervalSeconds = 60;
        public const string ArchiveTimestampFormat = "yyyyMMdd_HHmm_ss_ffff";
        public const string SnapshotTimestampFormat = "yyyyMMddHHmmssfff";
        #endregion

        #region internals
        private string _datasetName;
        private string _path;
        private AccessMode _mode;
        private bool _indexed;
        private int _flushInterval;
        private string _archivePath;
        private string _snapshotPath;
        private Func<byte[]> _encryptionKeyProvider;
        #endregion

        #region interface
        public string DatasetName => _datasetName;
        public string Path => _path;
        public bool IsIndexedOnIdentity => _indexed;
        public bool IsPersisted => _path is not null;
        internal bool ShouldArchive => _archivePath is not null;
        internal string ArchivePath => _archivePath;
        public bool IsSnapshotReady => _snapshotPath is not null;
        internal AccessMode Mode => _mode;
        internal int FlushInterval => _path is null ? 0 : _flushInterval;
        public bool IsEncryptionReady => _encryptionKeyProvider is not null;
        #endregion

        #region ctors
        protected MemDbConfiguration(string datasetName, string path = null)
        {
            _datasetName = datasetName ?? throw new ArgumentNullException(nameof(datasetName));
            _path = path;
            _mode = AccessMode.ReadWrite;
            //if path is null, we will never flush to disk (non persistant).
            _flushInterval = path is null ? 0 : MemDbConfiguration.DefaultFlushIntervalSeconds * 1000;
        }
        #endregion

        #region set mode
        protected void SetMode(AccessMode mode)
        {
            if (mode == AccessMode.AppendOnly && _path is null)
                throw new InvalidOperationException($"{nameof(AccessMode)}.{AccessMode.AppendOnly} is not applicable with a unpersisted database (no path provided).");

            if (mode == AccessMode.ReadOnly && _snapshotPath is not null)
                throw new InvalidOperationException($"{nameof(AccessMode)}.{AccessMode.ReadOnly} is not applicable with database snapshot functionality...Cannot run in {nameof(AccessMode)}.{AccessMode.ReadOnly} mode when a snapshot path has been configured.");

            if (mode == AccessMode.ReadOnly && _path is null)
                throw new InvalidOperationException($"{nameof(AccessMode)}.{AccessMode.ReadOnly} is not applicable with a unpersisted database (no path provided).");

            //assuming _flushInterval was never overridden if it is still equal to the default.
            if (mode == AccessMode.ReadOnly && _flushInterval != MemDbConfiguration.DefaultFlushIntervalSeconds * 1000)
                throw new InvalidOperationException($"{nameof(AccessMode)}.{AccessMode.ReadOnly} is not applicable with with a flush interval greater than 0.");

            _mode = mode;
        }
        #endregion

        #region set indexed on identity
        protected void SetIndexedOnIdentity(bool shouldIndex)
        {
            _indexed = shouldIndex;
        }
        #endregion

        #region set flush interval
        protected void SetFlushInterval(int seconds)
        {
            if (_path is null)
                throw new InvalidOperationException($"Flush interval is not applicable when database is not persisted (no path provided).");

            if (_mode == AccessMode.ReadOnly)
                throw new InvalidOperationException($"Flush interval is not applicable when {nameof(AccessMode)} is {AccessMode.ReadOnly}.");

            if (seconds == 0 || seconds == -1)
                _flushInterval = seconds;
            else
                _flushInterval = seconds * 1000;//convert to milliseconds
        }
        #endregion

        #region set encryption key provider
        protected void SetEncryptionKeyProvider(Func<byte[]> encryptionKeyProvider)
        {
            if (_encryptionKeyProvider is not null)
                throw new InvalidOperationException("Encryption key provider already configured.");

            if (_path is null)
                throw new NotImplementedException("Encryption key is not applicable when database is not persisted (no path provided).");

            _encryptionKeyProvider = encryptionKeyProvider;
        }
        #endregion

        #region set archive path
        protected void SetArchivePath(string archivePath)
        {
            if (_archivePath is not null)
                throw new InvalidOperationException("Archive path already provided.");

            if (_path is null)
                throw new NotImplementedException("Archive path is not applicable when database is not persisted (no path provided).");

            _archivePath = archivePath;
        }
        #endregion

        #region set snapshot path
        protected void SetSnapshotPath(string snapshotPath)
        {
            if (_snapshotPath is not null)
                throw new InvalidOperationException("Snapshot path already provided.");

            if (_path is null)
                throw new InvalidOperationException($"Snapshot path is not applicable with a unpersisted database (no path provided).");

            if (_mode == AccessMode.ReadOnly)
                throw new InvalidOperationException($"Snapshot path is not applicable when {nameof(AccessMode)} is {AccessMode.ReadOnly}.");

            _snapshotPath = snapshotPath;
        }
        #endregion

        #region ensure generic type
        internal MemDbConfiguration<T> EnsureGenericType<T>(MemDbConfiguration configuration) where T : class
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            if (!(configuration is MemDbConfiguration<T> configOfT))
            {
                string dsn = configuration.DatasetName;
                string req = typeof(T).Name;//requested
                string reg = configuration.GetType().GetGenericArguments()[0].Name;//registered
                string msg = $"Registered configuration for dataset name '{dsn}' is registered for type '{reg}'...attempted open on type '{req}'";
                throw new MemDbConfigurationException(msg);
            }

            return configOfT;
        }
        #endregion

        #region get full lock file path
        internal string GetFullLockFilePath()
        {
            if (!this.IsPersisted)
                throw new InvalidOperationException("Registered configuration for dataset name {_datasetName} is not persisted...No lock file initialized.");

            return System.IO.Path.Combine(_path, $"~$htl.{_datasetName}.lock");
        }
        #endregion

        #region get full map file path
        internal string GetFullMapFilePath()
        {
            return System.IO.Path.Combine(_path, $"htl.{_datasetName}.map");
        }
        #endregion

        #region get full db file path
        internal string GetFullDbFilePath()
        {
            return System.IO.Path.Combine(_path, $"htl.{_datasetName}.db");
        }
        #endregion

        #region get full map archive file path
        internal string GetFullMapArchiveFilePath(DateTime timestamp)
        {
            string at = timestamp.ToString(MemDbConfiguration.ArchiveTimestampFormat);
            return System.IO.Path.Combine(_archivePath, $"{at}.htl.{_datasetName}.map.arch");
        }
        #endregion

        #region get full db archive file path
        internal string GetFullDbArchiveFilePath(DateTime timestamp)
        {
            string at = timestamp.ToString(MemDbConfiguration.ArchiveTimestampFormat);
            return System.IO.Path.Combine(_archivePath, $"{at}.htl.{_datasetName}.db.arch");
        }
        #endregion

        #region get zip archive full file path
        internal string GetZipArchiveFullFilePath()
        {
            return System.IO.Path.Combine(_archivePath, $"htl.{_datasetName}.zip");
        }
        #endregion

        #region get full snapshot map file path
        public string GetFullSnapshotMapFilePath(DateTime timestamp)
        {
            string at = timestamp.ToString(MemDbConfiguration.SnapshotTimestampFormat);
            return System.IO.Path.Combine(_snapshotPath, $"htl.{_datasetName}.{at}.map");
        }
        #endregion

        #region get full snapshot db file path
        public string GetFullSnapshotDbFilePath(DateTime timestamp)
        {
            string at = timestamp.ToString(MemDbConfiguration.SnapshotTimestampFormat);
            return System.IO.Path.Combine(_snapshotPath, $"htl.{_datasetName}.{at}.db");
        }
        #endregion

        #region get encryptor
        public IMemDbEncryptor GetEncryptor()
        {
            return _encryptionKeyProvider is not null
                ? new MemDbAESEncryptor(_encryptionKeyProvider())
                : null;
        }
        #endregion

        #region get encryption info
        public IMemDbEncryptionInfo GetEncryptionInfo()
        {
            return new MemDbAESEncryptionInfo();
        }
        #endregion

        #region get snapshotter
        internal IMemDbSnapshotter GetSnapshotter()
        {
            return _snapshotPath is not null
                ? new MemDbSnapshotter(this)
                : null;
        }
        #endregion
    }
    #endregion

    #region [interface] i mem db configuration builder
    public interface IMemDBConfigurationBuilder<T> where T : class
    {
        IMemDBConfigurationBuilder<T> SetMode(AccessMode mode);
        IMemDBConfigurationBuilder<T> IndexOnIdentity(bool shouldIndex);
        IMemDBConfigurationBuilder<T> ApplyIndex<Y>(string name, Func<T, Y> keyResolver) where Y : IConvertible;
        IMemDBConfigurationBuilder<T> ApplyIndex<Y>(string name, Func<T, Y> keyResolver, HybridComparer<Y> comparer) where Y : IConvertible;
        //IMemDBConfigurationBuilder<T> ApplyIndex<Y>(string name, Func<T, ICollection<Y>> keySetResolver) where Y : IConvertible;
        //IMemDBConfigurationBuilder<T> ApplyIndex<Y>(string name, Func<T, ICollection<Y>> keySetResolver, HybridComparer<Y> comparer) where Y : IConvertible;
        IMemDBConfigurationBuilder<T> SetFlushInterval(int interval);
        IMemDBConfigurationBuilder<T> SerializeWith(Func<IMemDbSerializer<T>> serializerProvider);
        IMemDBConfigurationBuilder<T> CloneWith(Func<IMemDbCloner<T>> clonerProvider);
        IMemDBConfigurationBuilder<T> EncryptWithKey(Func<byte[]> encryptionKeyProvider);
        IMemDBConfigurationBuilder<T> EncryptWithPassword(Func<string> encryptionPasswordProvider);
        IMemDBConfigurationBuilder<T> ArchiveOnDefrag(string archivePath);
        IMemDBConfigurationBuilder<T> SnapshotTo(string snapshotPath);
        void Register();
    }
    #endregion

    #region [class] mem db configuration of T
    public class MemDbConfiguration<T> : MemDbConfiguration, IMemDBConfigurationBuilder<T> where T : class
    {
        #region internals
        private Action<MemDbConfiguration<T>> _registerCallback;

        private Func<IMemDbSerializer<T>> _serializerProvider;
        private Func<IMemDbCloner<T>> _clonerProvider;

        private List<MemDbIndex<T>> _appliedIndexes;
        #endregion

        #region constructors
        internal MemDbConfiguration(string datasetName, Action<MemDbConfiguration<T>> registerCallback) 
            : this(datasetName, null, registerCallback)
        { }

        internal MemDbConfiguration(string datasetName, string path, Action<MemDbConfiguration<T>> registerCallback) 
            : base(datasetName, path)
        {
            _registerCallback = registerCallback ?? throw new ArgumentNullException(nameof(registerCallback));

            //set the default providers
            _serializerProvider = () => MemDbJsonSerializer<T>.GetInstance();
            _clonerProvider = () => new MemDbSerializationCloner<T>(this.GetSerializer());
        }
        #endregion

        #region set mode
        public new IMemDBConfigurationBuilder<T> SetMode(AccessMode mode)
        {
            base.SetMode(mode);
            return this;
        }
        #endregion

        #region index on identity
        public IMemDBConfigurationBuilder<T> IndexOnIdentity(bool shouldIndex)
        {
            base.SetIndexedOnIdentity(shouldIndex);
            return this;
        }
        #endregion

        #region apply index
        public IMemDBConfigurationBuilder<T> ApplyIndex<Y>(string name, Func<T, Y> keyResolver)
        where Y : IConvertible
        {
            return this.ApplyIndex(name, keyResolver, new HybridComparer<Y>());
        }

        public IMemDBConfigurationBuilder<T> ApplyIndex<Y>(string name, Func<T, Y> keyResolver, HybridComparer<Y> comparer) 
        where Y : IConvertible
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Argument must contain a value.", nameof(name));

            if (keyResolver is null)
                throw new ArgumentNullException(nameof(keyResolver));

            if (_appliedIndexes is null)
                _appliedIndexes = new List<MemDbIndex<T>>();

            var index = new MemDbIndex<T, Y>(name, keyResolver, comparer);
            _appliedIndexes.Add(index);

            return this;
        }

        //exploratory this ... not yet exposed on IMemDbConfigurationBuilder<T>
        public IMemDBConfigurationBuilder<T> ApplyIndex<Y>(string name, Func<T, ICollection<Y>> keySetResolver)
        where Y : IConvertible
        {
            return this.ApplyIndex<Y>(name, keySetResolver, new HybridComparer<Y>());
        }

        //exploratory this ... not yet exposed on IMemDbConfigurationBuilder<T>
        public IMemDBConfigurationBuilder<T> ApplyIndex<Y>(string name, Func<T, ICollection<Y>> keySetResolver, HybridComparer<Y> comparer) 
        where Y : IConvertible
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Argument must contain a value.", nameof(name));

            if (keySetResolver is null)
                throw new ArgumentNullException(nameof(keySetResolver));

            if (_appliedIndexes is null)
                _appliedIndexes = new List<MemDbIndex<T>>();

            var index = new MemDbIndexedSet<T, Y>(name, keySetResolver, comparer);
            _appliedIndexes.Add(index);

            return this;
        }
        #endregion

        #region set flush interval
        public new IMemDBConfigurationBuilder<T> SetFlushInterval(int seconds)
        {
            if (seconds == 0)//manual flush only
                base.SetFlushInterval(seconds);

            if (seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Argument cannot be less than 0.");

            int maxAllowed = MemDbConfiguration.MaxFlushIntervalSeconds;

            if (seconds > maxAllowed)
                throw new ArgumentOutOfRangeException($"Max allowed flush interval is {maxAllowed} seconds.", nameof(seconds));

            base.SetFlushInterval(seconds);

            return this;
        }
        #endregion

        #region serialize with
        public IMemDBConfigurationBuilder<T> SerializeWith(Func<IMemDbSerializer<T>> serializerProvider)
        {
            _serializerProvider = serializerProvider ?? throw new ArgumentNullException(nameof(serializerProvider));
            return this;
        }
        #endregion

        #region clone with
        public IMemDBConfigurationBuilder<T> CloneWith(Func<IMemDbCloner<T>> clonerProvider)
        {
            _clonerProvider = clonerProvider ?? throw new ArgumentNullException(nameof(clonerProvider));
            return this;
        }
        #endregion

        #region encrypt with key
        public IMemDBConfigurationBuilder<T> EncryptWithKey(Func<byte[]> encryptionKeyProvider)
        {
            if (encryptionKeyProvider is null)
                throw new ArgumentNullException(nameof(encryptionKeyProvider));

            base.SetEncryptionKeyProvider(encryptionKeyProvider);

            return this;
        }
        #endregion

        #region encrypt with password
        public IMemDBConfigurationBuilder<T> EncryptWithPassword(Func<string> encryptionPasswordProvider)
        {
            if (encryptionPasswordProvider is null)
                throw new ArgumentNullException(nameof(encryptionPasswordProvider));

            Func<byte[]> encryptionKeyProvider = () =>
            {
                string thisName = string.Concat(nameof(MemDbConfiguration<T>), ".", nameof(EncryptWithPassword));
                string argName = nameof(encryptionKeyProvider);

                string pw = encryptionPasswordProvider();

                if (pw is null)
                    throw new InvalidOperationException($"Password provided via {argName} from {thisName} is null.");

                if (pw == string.Empty)
                    throw new InvalidOperationException($"Password provided via {argName} from {thisName} is empty.");

                if (pw.Length < MemDbConfiguration.MinPasswordLength)
                    throw new InvalidOperationException($"Password provided via {argName} from {thisName} must be at least {MemDbConfiguration.MinPasswordLength} chars long.");

                byte[] hash = null;
                using (var sha256 = SHA256.Create())
                {
                    hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(pw));
                }

                return hash;
            };

            base.SetEncryptionKeyProvider(encryptionKeyProvider);

            return this;
        }
        #endregion

        #region archive on defrag
        public IMemDBConfigurationBuilder<T> ArchiveOnDefrag(string archivePath)
        {
            if (archivePath is null)
                throw new ArgumentNullException(nameof(archivePath));

            if (archivePath == string.Empty)
                throw new ArgumentException("Argument must contain a value.", nameof(archivePath));

            base.SetArchivePath(archivePath);

            return this;
        }
        #endregion

        #region snapshot to
        public IMemDBConfigurationBuilder<T> SnapshotTo(string snapshotPath)
        {
            if (snapshotPath is null)
                throw new ArgumentNullException(nameof(ArchivePath));

            if (ArchivePath == string.Empty)
                throw new ArgumentException("Argument must contain a value.", nameof(snapshotPath));

            base.SetSnapshotPath(snapshotPath);

            return this;
        }
        #endregion

        #region register
        public void Register()
        {
            _registerCallback(this);
            _registerCallback = null;
        }
        #endregion

        #region get cloner
        public IMemDbCloner<T> GetCloner()
        {
            return _clonerProvider();
        }
        #endregion

        #region get serializer
        internal IMemDbSerializer<T> GetSerializer()
        {
            return _serializerProvider();
        }
        #endregion

        #region get persister
        internal IMemDbPersister<T> GetPersister()
        {
            return new MemDbMappedFile<T>(this);
        }
        #endregion

        #region get cache
        internal IMemDbCache<T> GetCache()
        {
            return new MemDbCache<T>(this);
        }
        #endregion

        #region get applied indexes
        internal MemDbIndexCollection<T> GetAppliedIndexes()
        {
            if (_appliedIndexes is null)
                return null;

            return new MemDbIndexCollection<T>(_appliedIndexes.ToArray());
        }
        #endregion
    }
    #endregion
}
