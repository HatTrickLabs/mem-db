using System;

namespace HatTrick.InMemDb
{
    #region mem db record
    internal abstract class MemDbRecord
    {
        #region read only
        internal static readonly int Size = sizeof(uint)//id
                                          + sizeof(RecordState)//state
                                          + sizeof(long)//state set at
                                          + sizeof(long)//created at
                                          + sizeof(bool)//is encrypted
                                          + sizeof(int);//map index
        #endregion

        #region internals
        private uint _id;
        private RecordState _state;
        private long _stateSetAt;
        private long _createdAt;
        private bool _isEncrypted;
        private int _mapIndex;
        #endregion

        #region interface
        internal uint Id => _id;
        internal RecordState State => _state;
        internal long StateSetAt => _stateSetAt;
        internal long CreatedAt => _createdAt;
        internal bool IsEncrypted => _isEncrypted;
        internal int MapIndex => _mapIndex;
        #endregion

        #region constructors
        internal MemDbRecord(uint id, long createdAt, bool isEncrypted)
        {
            _id = id;
            _state = RecordState.Fresh;
            _stateSetAt = createdAt;
            _createdAt = createdAt;
            _isEncrypted = isEncrypted;
            _mapIndex = -1;//TODO: Lets catch that elusive (You've got an update being applied before the INSERT finalized theory).
        }

        internal MemDbRecord(uint id, RecordState state, long stateSetAt, long createdAt, bool isEncrypted, int mapIndex)
        {
            _id = id;
            _state = state;
            _stateSetAt = stateSetAt;
            _createdAt = createdAt;
            _isEncrypted = isEncrypted;
            _mapIndex = mapIndex;
        }
        #endregion

        #region mark stale
        internal void MarkStale(long utcTimestamp)
        {
            _state = RecordState.Stale;
            _stateSetAt = utcTimestamp;
        }
        #endregion

        #region mark deleted
        internal void MarkDeleted(long utcTimestamp)
        {
            _state = RecordState.Deleted;
            _stateSetAt = utcTimestamp;
        }
        #endregion

        #region set map index
        internal void SetMapIndex(int mapIndex)
        {
            _mapIndex = mapIndex;
        }
        #endregion
    }
    #endregion

    #region mem db record of T
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
        internal MemDbRecord(uint id, T value, long createdAt, bool isEncrypted) : base(id, createdAt, isEncrypted)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _value = value;
        }

        internal MemDbRecord(uint id, T value, RecordState state, long stateSetAt, long createdAt, bool isEncrypted, int cacheIndex, int mapIndex) 
            : base(id, state, stateSetAt, createdAt, isEncrypted, mapIndex)
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
    #endregion
}
