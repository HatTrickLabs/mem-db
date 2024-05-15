using System;

namespace HatTrick.MemDb
{
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
                string req = typeof(T).Name;
                string reg = configuration.GetType().GetGenericTypeDefinition().Name;
                string msg = $"Registered configuration for dataset name '{dsn}' is registered for type '{reg}'...attempted open on type '{req}'";
                throw new MemDbConfigurationException(msg);
            }

            return configOfT;
        }
        #endregion
    }

    public class MemDbConfiguration<T> : MemDbConfiguration where T : class, new()
    {
        #region internals
        private Action<MemDbConfiguration<T>> _registerCallback;

        private Func<IMemDbSerializer<T>> _serializerProvider;
        private Func<IMemDbCloner<T>> _clonerProvider;
        private Func<IMemDbEncrypter<T>> _encrypterProvider;
        private Func<IMemDbPersister<T>, IMemDbCacher<T>> _cacheProvider;
        private Func<string, string, IMemDbSerializer<T>, IMemDbPersister<T>> _persisterProvider;
        private Func<string, string, IMemDbDefragmenter<T>> _defragmenterProvider;

        private IMemDbSerializer<T> _serializer;
        private IMemDbCloner<T> _cloner;
        private IMemDbEncrypter<T> _encrypter;
        private IMemDbCacher<T> _cache;
        private IMemDbPersister<T> _persister;
        private IMemDbDefragmenter<T> _defragmenter;

        private bool _isInitialized;
        #endregion

        #region constructors
        internal MemDbConfiguration(string datasetName, string path, Action<MemDbConfiguration<T>> registerCallback)
            : base(datasetName, path)
        {
            _registerCallback = registerCallback ?? throw new ArgumentNullException(nameof(registerCallback));
        }
        #endregion

        #region serialize with
        public MemDbConfiguration<T> SerializeWith(IMemDbSerializer<T> serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
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

        #region encrypt with
        public MemDbConfiguration<T> EncryptWith(IMemDbEncrypter<T> encrypter)
        {
            _encrypter = encrypter ?? throw new ArgumentNullException(nameof(encrypter));
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

            _serializer = _serializerProvider();
            _cloner = _clonerProvider();
            _encrypter = _encrypterProvider();
            _persister = _persisterProvider(base.Path, base.DatasetName, _serializer);
            _cache = _cacheProvider(_persister);
            _defragmenter = _defragmenterProvider(base.Path, base.DatasetName);

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

        #region get serializer
        internal IMemDbSerializer<T> GetSerializer()
        {
            this.EnsureInitalized();
            return _serializer;
        }
        #endregion

        #region get cloner
        internal IMemDbCloner<T> GetCloner()
        {
            this.EnsureInitalized();
            return _cloner;
        }
        #endregion

        #region get encrypter
        internal IMemDbEncrypter<T> GetEncrypter()
        {
            this.EnsureInitalized();
            return _encrypter;
        }
        #endregion

        #region get cache
        internal IMemDbCacher<T> GetCache()
        {
            this.EnsureInitalized();
            return _cache;
        }
        #endregion

        #region get persister
        internal IMemDbPersister<T> GetPersister()
        {
            this.EnsureInitalized();
            return _persister;
        }
        #endregion

        #region get defragmenter
        internal IMemDbDefragmenter<T> GetDefragmenter()
        {
            this.EnsureInitalized();
            return _defragmenter;
        }
        #endregion
    }
}
