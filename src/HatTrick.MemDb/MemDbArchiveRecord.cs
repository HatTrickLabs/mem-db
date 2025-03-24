using System;

namespace HatTrick.InMemDb
{
    public class MemDbArchivedRecord<T>
    {
        #region internals
        private uint _id;
        private RecordState _state;
        private long _stateSetAt;
        private bool _isEncrypted;
        private T _value;
        #endregion

        #region interface
        public uint Id => _id;
        public RecordState State => _state;
        public long StateSetAt => _stateSetAt;
        internal bool IsEncrypted => _isEncrypted;
        public T Value => _value;
        #endregion

        #region ctors
        internal MemDbArchivedRecord(uint id, RecordState state, long stateSetAt, bool isEncrypted, T value)
        {
            _id = id;
            _state = state;
            _stateSetAt = stateSetAt;
            _isEncrypted = isEncrypted;
            _value = value;
        }
        #endregion
    }
}
