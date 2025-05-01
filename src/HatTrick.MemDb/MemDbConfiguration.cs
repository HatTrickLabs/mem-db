using System;
using System.Text;
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
        #endregion

        #region internals
        private string _datasetName;
        private string _path;
        private string _archivePath;
        private AccessMode _mode;
        private int _flushInterval;
        private Func<byte[]> _encryptionKeyProvider;
        #endregion

        #region interface
        public string DatasetName => _datasetName;
        public string Path => _path;
        public bool IsPersisted => _path is not null;
        internal bool ShouldArchive => _archivePath is not null;
        internal string ArchivePath => _archivePath;
        internal AccessMode Mode => _mode;
        internal int FlushInterval => _path is null ? 0 : _flushInterval;
        protected Func<byte[]> EncryptionKeyProvider => _encryptionKeyProvider;
        #endregion

        #region constructors
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
                throw new InvalidOperationException($"{nameof(AccessMode)}.{AccessMode.AppendOnly} is inconsistent with a unpersisted database (no path provided).");

            if (mode == AccessMode.ReadOnly && _path is null)
                throw new InvalidOperationException($"{nameof(AccessMode)}.{AccessMode.ReadOnly} is inconsistent with a unpersisted database (no path provided).");

            //assuming _flushInterval was never overridden if it is still equal to the default.
            if (mode == AccessMode.ReadOnly && _flushInterval != MemDbConfiguration.DefaultFlushIntervalSeconds * 1000)
                throw new InvalidOperationException($"{nameof(AccessMode)}.{AccessMode.ReadOnly} is inconsistent with with a flush interval greater than 0.");

            _mode = mode;
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
        internal string GetFullMapArchiveFilePath(DateTime at)
        {
            string timestamp = at.ToString(ArchiveTimestampFormat);
            return System.IO.Path.Combine(_archivePath, $"{timestamp}.htl.{_datasetName}.map.arch");
        }
        #endregion

        #region get full db archive file path
        internal string GetFullDbArchiveFilePath(DateTime at)
        {
            string timestamp = at.ToString(ArchiveTimestampFormat);
            return System.IO.Path.Combine(_archivePath, $"{timestamp}.htl.{_datasetName}.db.arch");
        }
        #endregion

        #region get zip archive full file path
        internal string GetZipArchiveFullFilePath()
        {
            return System.IO.Path.Combine(_archivePath, $"htl.{_datasetName}.zip");
        }
        #endregion
    }
    #endregion

    #region [interface] i mem db configuration builder
    public interface IMemDBConfigurationBuilder<T> where T : class
    {
        IMemDBConfigurationBuilder<T> SetMode(AccessMode mode);
        IMemDBConfigurationBuilder<T> SetFlushInterval(int interval);
        IMemDBConfigurationBuilder<T> SerializeWith(Func<IMemDbSerializer<T>> serializerProvider);
        IMemDBConfigurationBuilder<T> CloneWith(Func<IMemDbCloner<T>> clonerProvider);
        IMemDBConfigurationBuilder<T> EncryptWithKey(Func<byte[]> encryptionKeyProvider);
        IMemDBConfigurationBuilder<T> EncryptWithPassword(Func<string> encryptionPasswordProvider);
        IMemDBConfigurationBuilder<T> ArchiveOnDefrag(string archivePath);
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

        #region get encryptor
        public IMemDbEncryptor GetEncryptor()
        {
            return base.EncryptionKeyProvider is not null 
                ? new MemDbAESEncryptor(base.EncryptionKeyProvider())
                : null;
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
    }
    #endregion
}
