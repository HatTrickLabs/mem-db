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
        public const string ArchiveTimestampFormat = "yyyyMMdd_HHmm_ss_ffff";
        //private const string MapNameFormat = "htl.{0}.map";
        //private const string DbNameFormat  = "htl.{0}.db";
        //private const string MapArchiveNameFormat = "{now}.htl.{datasetName}.map.arch";
        //private const string DbArchiveNameFormat = "{now}.htl.{datasetName}.db.arch";
        #endregion

        #region internals
        private bool _isInitialized;
        private string _datasetName;
        private string _path;
        private string _archivePath;
        private AccessMode _mode;
        #endregion

        #region interface
        protected bool IsInitialized => _isInitialized;
        public string DatasetName => _datasetName;
        public string Path => _path;
        internal bool ShouldArchive => _archivePath is not null;
        internal string ArchivePath => _archivePath;
        internal AccessMode Mode => _mode;
        #endregion

        #region constructors
        protected MemDbConfiguration(string datasetName, string path, AccessMode mode)
        {
            _datasetName = datasetName ?? throw new ArgumentNullException(nameof(datasetName));
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _mode = mode;
        }
        #endregion

        #region initialize
        internal virtual void Initialize()
        {
            _isInitialized = true;
        }
        #endregion

        #region set mode
        protected void SetMode(AccessMode mode)
        {
            _mode = mode;
        }
        #endregion

        #region set archive path
        protected void SetArchivePath(string archivePath)
        {
            if (_archivePath is not null)
                throw new InvalidOperationException("Archive path already provided.");

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
            return System.IO.Path.Combine(_archivePath, $"{timestamp}htl.{_datasetName}.map.arch");
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

        #region clear
        internal virtual void Clear()
        {
            _isInitialized = false;
        }
        #endregion
    }
    #endregion

    #region [interface] i mem db configuration builder
    public interface IMemDBConfigurationBuilder<T> where T : class
    {
        IMemDBConfigurationBuilder<T> SetMode(AccessMode mode);
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
        private Func<byte[]> _encryptionKeyProvider;

        private IMemDbSerializer<T> _serializer;
        private IMemDbCloner<T> _cloner;
        private byte[] _encryptionKey;
        private IMemDbEncryptor _encryptor;
        private IMemDbCache<T> _cache;
        private IMemDbPersister<T> _persister;
        #endregion

        #region constructors
        internal MemDbConfiguration(string datasetName, string path, Action<MemDbConfiguration<T>> registerCallback)
            : base(datasetName, path, AccessMode.ReadWrite)
        {
            _registerCallback = registerCallback ?? throw new ArgumentNullException(nameof(registerCallback));

            //set the default providers
            _serializerProvider = () => MemDbJsonSerializer<T>.GetInstance();
            _clonerProvider = () => new MemDbSerializationCloner<T>(_serializer);
        }
        #endregion

        #region set mode
        public new IMemDBConfigurationBuilder<T> SetMode(AccessMode mode)
        {
            base.SetMode(mode);
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
            if (_encryptionKeyProvider is not null)
                throw new InvalidOperationException("Encryption key already provided.");

            _encryptionKeyProvider = encryptionKeyProvider ?? throw new ArgumentNullException(nameof(encryptionKeyProvider));
            return this;
        }
        #endregion

        #region encrypt with password
        public IMemDBConfigurationBuilder<T> EncryptWithPassword(Func<string> encryptionPasswordProvider)
        {
            if (_encryptionKeyProvider is not null)
                throw new InvalidOperationException("Encryption key already provided...Use one of password or key for encryption.");

            if (encryptionPasswordProvider is null)
                throw new ArgumentNullException(nameof(encryptionPasswordProvider));

            _encryptionKeyProvider = () =>
            {
                string pw = encryptionPasswordProvider();
                if (pw is null)
                    throw new InvalidOperationException($"Password provided via {nameof(encryptionPasswordProvider)} from {nameof(MemDbConfiguration<T>)}.{nameof(EncryptWithPassword)} is null.");

                if (pw == string.Empty)
                    throw new InvalidOperationException($"Password provided via {nameof(encryptionPasswordProvider)} from {nameof(MemDbConfiguration<T>)}.{nameof(EncryptWithPassword)} is empty.");

                if (pw.Length < MemDbConfiguration.MinPasswordLength)
                    throw new InvalidOperationException($"Password provided via {nameof(encryptionPasswordProvider)} from {nameof(MemDbConfiguration<T>)}.{nameof(EncryptWithPassword)} must be at least {MemDbConfiguration.MinPasswordLength} chars long.");

                byte[] hash = null;
                using (var sha256 = SHA256.Create())
                {
                    hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(pw));
                }

                return hash;
            };

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

        #region initialize
        internal override void Initialize()
        {
            if (base.IsInitialized)
                return;

            base.Initialize();
            _encryptionKey = _encryptionKeyProvider?.Invoke() ?? null;
            _encryptor = _encryptionKey is null ? null : new MemDbAESEncryptor(_encryptionKey);
            _serializer = _serializerProvider();//MUST BE BEFORE CLONER...clone provider passes this as arg on internal ctor
            _cloner = _clonerProvider();
            _persister = new MemDbMappedFile<T>(this);//MUST BE BEFORE CACHE...Cache passes this as arg on internal ctor
            _cache = new MemDbCache<T>(this);
        }
        #endregion

        #region clear
        internal override void Clear()
        {
            if (!base.IsInitialized)
                return;

            base.Clear();

            _encryptionKey = null;
            _encryptor = null;
            _serializer = null;
            _cloner = null;
            _persister = null;
            _cache = null;
        }
        #endregion

        #region get cloner
        public IMemDbCloner<T> GetCloner()
        {
            this.Initialize();
            return _cloner;
        }
        #endregion

        #region get encryptor
        public IMemDbEncryptor GetEncryptor()
        {
            this.Initialize();
            return _encryptor;
        }
        #endregion

        #region get serializer
        internal IMemDbSerializer<T> GetSerializer()
        {
            this.Initialize();
            return _serializer;
        }
        #endregion

        #region get persister
        internal IMemDbPersister<T> GetPersister()
        {
            this.Initialize();
            return _persister;
        }
        #endregion

        #region get cache
        internal IMemDbCache<T> GetCache()
        {
            this.Initialize();
            return _cache;
        }
        #endregion
    }
    #endregion
}
