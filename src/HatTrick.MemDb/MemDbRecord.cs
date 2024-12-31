using System;

namespace HatTrick.InMemDb
{
    internal abstract class MemDbRecord
    {
        #region read only
        internal static readonly int Size = sizeof(uint) + sizeof(RecordState) + sizeof(long) + sizeof(bool) + sizeof(int) + sizeof(int);
        #endregion

        #region internals
        private uint _id;
        private RecordState _state;
        private long _stateSetAt;
        private bool _isEncrypted;
        private int _cacheIndex;
        private int _mapIndex;
        #endregion

        #region interface
        internal uint Id => _id;
        internal RecordState State => _state;
        internal long StateSetAt => _stateSetAt;
        internal bool IsEncrypted => _isEncrypted;
        internal int CacheIndex => _cacheIndex;
        internal int MapIndex => _mapIndex;
        #endregion

        #region constructors
        internal MemDbRecord(uint id, bool isEncrypted)
        {
            _id = id;
            _state = RecordState.Fresh;
            _stateSetAt = DateTime.UtcNow.ToBinary();
            _isEncrypted = isEncrypted;
            _mapIndex = -1;//TODO: Lets catch that elusive (You've got an update being applied before the INSERT finalized theory).
        }

        internal MemDbRecord(uint id, RecordState state, long stateSetAt, bool isEncrypted, int cacheIndex, int mapIndex)
        {
            _id = id;
            _state = state;
            _stateSetAt = stateSetAt;
            _isEncrypted = isEncrypted;
            _cacheIndex = cacheIndex;
            _mapIndex = mapIndex;
        }
        #endregion

        #region mark stale
        internal void MarkStale(long binaryUTCTimestamp)
        {
            _stateSetAt = binaryUTCTimestamp;
            _state = RecordState.Stale;
        }
        #endregion

        #region mark deleted
        internal void MarkDeleted(long binaryUTCTimestamp)
        {
            _stateSetAt = binaryUTCTimestamp;
            _state = RecordState.Deleted;
        }
        #endregion

        #region set cache index
        internal void SetCacheIndex(int cacheIndex)
        {
            _cacheIndex = cacheIndex;
        }
        #endregion

        #region set map index
        internal void SetMapIndex(int mapIndex)
        {
            _mapIndex = mapIndex;
        }
        #endregion
    }

    internal class MemDbRecord<T> : MemDbRecord where T: class
    {
        #region internals
        private T _value;
        #endregion

        #region interface
        public T Value
        {
            get { return _value; }
            internal set { _value = value; }
        }
        #endregion

        #region constructors
        internal MemDbRecord(uint id, T value, bool isEncrypted) : base(id, isEncrypted)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _value = value;
        }

        internal MemDbRecord(uint id,  T value, RecordState state, long stateSetAt, bool isEncrypted, int cacheIndex, int mapIndex) 
            : base(id, state, stateSetAt, isEncrypted, cacheIndex, mapIndex)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            _value = value;
        }
        #endregion

        #region to string
        public override string ToString()
        {
            return string.Concat(base.Id, "|", base.State, "|", _value.ToString());
        }
        #endregion
    }
}
