using System;
using System.Text.Json.Serialization;

namespace HatTrick.Data
{
    #region mem db record
    internal abstract class MemDbRecord
    {
        #region read only
        internal static readonly int Size = sizeof(long)//8:Id
                                          + sizeof(byte)//1:State enum : byte
                                          + sizeof(long)//8:StateSetAt
                                          + sizeof(long)//8:CreatedAt
                                          + sizeof(bool)//1:IsEncrypted
                                          + sizeof(int);//4:MapIndex
                                                        //--------------
                                                        //30
        #endregion

        #region internals
        private long _id;
        private RecordState _state;
        private long _stateSetAt;
        private long _createdAt;
        private bool _isEncrypted;
        private int _mapIndex;
        #endregion

        #region interface
        internal long Id => _id;
        internal RecordState State => _state;
        internal long StateSetAt => _stateSetAt;
        internal long CreatedAt => _createdAt;
        internal bool IsEncrypted => _isEncrypted;
        internal int MapIndex => _mapIndex;

        [JsonIgnore]//Never serialized or cloned and NOT included in Size
        internal int CacheIndex { get; set; }
        #endregion

        #region constructors
        internal MemDbRecord(long id, long createdAt, bool isEncrypted)
        {
            _id = id;
            _state = RecordState.Fresh;
            _stateSetAt = createdAt;
            _createdAt = createdAt;
            _isEncrypted = isEncrypted;
            _mapIndex = -1;
        }

        internal MemDbRecord(long id, RecordState state, long stateSetAt, long createdAt, bool isEncrypted, int mapIndex)
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
        internal MemDbRecord(long id, T value, long createdAt, bool isEncrypted) 
            : base(id, createdAt, isEncrypted)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _value = value;
        }

        internal MemDbRecord(long id, T value, RecordState state, long stateSetAt, long createdAt, bool isEncrypted, int mapIndex) 
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
