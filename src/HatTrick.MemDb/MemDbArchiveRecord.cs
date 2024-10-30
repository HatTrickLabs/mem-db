using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb
{
    public class MemDbArchivedRecord<T>
    {
        #region internals
        private uint _id;
        private RecordState _state;
        private long _stateSetAt;
        private T _value;
        #endregion

        #region interface
        public uint Id => _id;
        public RecordState State => _state;
        public long StateSetAt => _stateSetAt;
        public T Value => _value;
        #endregion

        #region ctors
        internal MemDbArchivedRecord(uint id, RecordState state, long stateSetAt, T value)
        {
            _id = id;
            _state = state;
            _stateSetAt = stateSetAt;
            _value = value;
        }
        #endregion
    }
}
