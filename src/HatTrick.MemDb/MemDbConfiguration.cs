using System;

namespace HatTrick.MemDb
{
    public class MemDbConfiguration
    {
        #region internals
        private string _datasetName;
        #endregion

        #region interface
        public string DatasetName => _datasetName;
        #endregion

        #region constructors
        public MemDbConfiguration(string datasetName)
        {
            _datasetName = datasetName ?? throw new ArgumentNullException(nameof(datasetName));
        }
        #endregion
    }

    public class MemDbConfiguration<T> : MemDbConfiguration where T : class, new()
    {
        #region internals
        private Action<MemDbConfiguration<T>> _registerCallback;

        private IMemDbSerializer<T> _serializer;
        private IMemDbCloner<T> _cloner;
        private IMemDbEncrypter<T> _encrypter;
        private IMemDbCacher<T> _cache;
        private IMemDbPersister<T> _persister;
        private IMemDbDefragmenter<T> _defragmenter;
        #endregion

        #region interface
        public IMemDbSerializer<T> Serializer => _serializer;
        public IMemDbCloner<T> Cloner => _cloner;
        public IMemDbEncrypter<T> Encrypter => _encrypter;
        public IMemDbCacher<T> Cacher => _cache;
        public IMemDbPersister<T> Persister => _persister;
        public IMemDbDefragmenter<T> Defragmenter => _defragmenter;
        #endregion

        #region constructors
        internal MemDbConfiguration(string datasetName, Action<MemDbConfiguration<T>> registerCallback) : base(datasetName)
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
        public MemDbConfiguration<T> CloneWith(IMemDbCloner<T> cloner)
        {
            _cloner = cloner;
            return this;
        }
        #endregion

        #region encrypt with
        public MemDbConfiguration<T> EncryptWith(IMemDbEncrypter<T> encrypter)
        {
            _encrypter = encrypter;
            return this;
        }
        #endregion

        #region cache with
        public MemDbConfiguration<T> CacheWith(IMemDbCacher<T> cacher)
        {
            _cache = cacher ?? throw new ArgumentNullException(nameof(cacher));
            return this;
        }
        #endregion

        #region persist with
        public MemDbConfiguration<T> PersistWith(IMemDbPersister<T> persister)
        {
            _persister = persister ?? throw new ArgumentNullException(nameof(persister));
            return this;
        }
        #endregion

        #region defragment with
        public MemDbConfiguration<T> DefragmentWith(MemDbDefragmenter<T> defragmenter)
        {
            _defragmenter = defragmenter;
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
    }
}
