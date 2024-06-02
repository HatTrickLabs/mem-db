using System;
using System.Security.Cryptography;
using System.Text;

namespace HatTrick.MemDb
{
    #region [class] mem db configuration
    public class MemDbConfiguration
    {
        #region internals
        private string _datasetName;
        private string _path;
        #endregion

        #region interface
        public string DatasetName => _datasetName;
        public string Path => _path;
        #endregion

        #region constructors
        public MemDbConfiguration(string datasetName, string path)
        {
            _datasetName = datasetName ?? throw new ArgumentNullException(nameof(datasetName));
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }
        #endregion

        #region ensure generic type
        internal MemDbConfiguration<T> EnsureGenericType<T>(MemDbConfiguration configuration) where T : class, new()
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
    }
    #endregion

    #region [class] mem db configuration of T
    public class MemDbConfiguration<T> : MemDbConfiguration where T : class, new()
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
        private IMemDbCacher<T> _cache;
        private IMemDbPersister<T> _persister;
        private AccessMode _mode;

        private bool _isInitialized;
        #endregion

        #region constructors
        internal MemDbConfiguration(string datasetName, string path, Action<MemDbConfiguration<T>> registerCallback)
            : base(datasetName, path)
        {
            _registerCallback = registerCallback ?? throw new ArgumentNullException(nameof(registerCallback));

            //default mode should be read/write
            _mode = AccessMode.ReadWrite;

            //set the default providers
            _serializerProvider = () => MemDbJsonSerializer<T>.CreateInstance();
            _clonerProvider = () => new MemDbSerializationCloner<T>(_serializer);
        }
        #endregion

        #region read only
        public MemDbConfiguration<T> ReadOnly()
        {
            _mode = AccessMode.ReadOnly;
            return this;
        }
        #endregion

        #region append only
        public MemDbConfiguration<T> AppendOnly()
        {
            _mode = AccessMode.AppendOnly;
            return this;
        }
        #endregion

        #region read write
        public MemDbConfiguration<T> ReadWrite()
        {
            _mode = AccessMode.ReadWrite;
            return this;
        }
        #endregion

        #region serialize with
        public MemDbConfiguration<T> SerializeWith(Func<IMemDbSerializer<T>> serializerProvider)
        {
            _serializerProvider = serializerProvider ?? throw new ArgumentNullException(nameof(serializerProvider));
            return this;
        }
        #endregion

        #region clone with
        public MemDbConfiguration<T> CloneWith(Func<IMemDbCloner<T>> clonerProvider)
        {
            _clonerProvider = clonerProvider ?? throw new ArgumentNullException(nameof(clonerProvider));
            return this;
        }
        #endregion

        #region encrypt with key
        public MemDbConfiguration<T> EncryptWithKey(Func<byte[]> encryptionKeyProvider)
        {
            if (_encryptionKeyProvider is not null)
                throw new InvalidOperationException("Encryption key already provided.");

            _encryptionKeyProvider = encryptionKeyProvider ?? throw new ArgumentNullException(nameof(encryptionKeyProvider));
            return this;
        }
        #endregion

        #region encrypt with password
        public MemDbConfiguration<T> EncryptWithPassword(Func<string> encryptionPasswordProvider)
        {
            if (_encryptionKeyProvider is not null)
                throw new InvalidOperationException("Encryption key already provided...Use one of password or key for encryption.");

            if (encryptionPasswordProvider is null)
                throw new ArgumentNullException(nameof(encryptionPasswordProvider));

            string pw = encryptionPasswordProvider();
            if (pw is null)
                throw new InvalidOperationException($"Password provided via {nameof(encryptionPasswordProvider)} is null.");

            if (pw == string.Empty)
                throw new InvalidOperationException($"Password provided via {nameof(encryptionPasswordProvider)} is empty.");

            if (pw.Length < 10)
                throw new InvalidOperationException($"Password provided via {nameof(encryptionPasswordProvider)} must be at least 10 chars long.");

            byte[] hash = null;
            using (var sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(pw));
            }

            {
                _encryptionKeyProvider = () => hash;
            }

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
        internal void Initialize()
        {
            if (_isInitialized)
                return;

            _encryptionKey = _encryptionKeyProvider?.Invoke() ?? null;
            _encryptor = _encryptionKey is null ? null : new MemDbAESEncryptor(_encryptionKey);
            _serializer = _serializerProvider();//MUST BE BEFORE CLONER...clone provider passes this as arg on internal ctor
            _cloner = _clonerProvider();
            _persister = new MemDbMappedFile<T>(base.DatasetName, base.Path, _mode, _serializer, _encryptor);
            _cache = new MemDbCache<T>(base.DatasetName, _cloner, _persister);

            _isInitialized = true;
        }
        #endregion

        #region ensure intialized
        private void EnsureInitalized()
        {
            if (_isInitialized)
                return;

            this.Initialize();
        }
        #endregion

        #region get cache
        internal IMemDbCacher<T> GetCache()
        {
            this.EnsureInitalized();
            return _cache;
        }
        #endregion
    }
    #endregion
}
