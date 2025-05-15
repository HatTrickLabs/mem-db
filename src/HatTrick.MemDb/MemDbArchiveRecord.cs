using System;

namespace HatTrick.InMemDb
{
    public class MemDbArchivedRecord<T>
    {
        #region internals
        private long _id;
        private RecordState _state;
        private long _stateSetAt;
        private long _createdAt;
        private bool _isEncrypted;
        private T _value;
        #endregion

        #region interface
        public long Id => _id;
        public RecordState State => _state;
        public long StateSetAt => _stateSetAt;
        public long CreatedAt => _createdAt;
        internal bool IsEncrypted => _isEncrypted;
        public T Value => _value;
        #endregion

        #region ctors
        internal MemDbArchivedRecord(long id, RecordState state, long stateSetAt, long createdAt, bool isEncrypted, T value)
        {
            _id = id;
            _state = state;
            _stateSetAt = stateSetAt;
            _createdAt = createdAt;
            _isEncrypted = isEncrypted;
            _value = value;
        }
        #endregion
    }
}
