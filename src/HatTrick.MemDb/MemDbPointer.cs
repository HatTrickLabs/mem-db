using System;
using System.IO;

namespace HatTrick.InMemDb
{
    internal class MemDbPointer
    {
        #region internals
        private uint _id;
        private RecordState _state;
        private long _stateSetAt;
        private bool _isEncrypted;
        private uint _position;
        private int _length;

        //not serialized
        private bool _flushed;
        #endregion

        #region interface
        internal static readonly int Size = sizeof(int) //Id
                                          + sizeof(RecordState)//State
                                          + sizeof(long)//StateSetAt
                                          + sizeof(bool)//IsEncrypted
                                          + sizeof(uint)//Position
                                          + sizeof(int);//Length

        internal uint Id => _id;
        internal RecordState State => _state;
        internal long StateSetAt => _stateSetAt;
        internal bool IsEncrypted => _isEncrypted;
        internal uint Position => _position;
        internal int Length => _length;

        internal bool Flushed => _flushed;
        #endregion

        #region constructor
        internal MemDbPointer(uint id, RecordState state, long stateSetAt, bool isEncrypted, uint startPosition, int length, bool flushed = false)
        {
            _id = id;
            _state = state;
            _stateSetAt = stateSetAt;
            _isEncrypted = isEncrypted;
            _position = startPosition;
            _length = length;
            _flushed = flushed;
        }
        #endregion

        #region mark stale
        internal MemDbPointer MarkStale(long binaryUTCTimestamp)
        {
            _state = RecordState.Stale;
            _stateSetAt = binaryUTCTimestamp;
            return this;
        }
        #endregion

        #region mark deleted
        internal MemDbPointer MarkDeleted(long binaryUTCTimestamp)
        {
            _state = RecordState.Deleted;
            _stateSetAt = binaryUTCTimestamp;
            return this;
        }
        #endregion

        #region serialize to
        internal void SerializeTo(BinaryWriter writer)
        {
            writer.Write(_id);
            writer.Write((byte)_state);
            writer.Write(_stateSetAt);
            writer.Write(_isEncrypted);
            writer.Write(_position);
            writer.Write(_length);

            _flushed = true;
        }
        #endregion

        #region deserializer from
        internal static MemDbPointer DeserializeFrom(BinaryReader reader)
        {
            uint id = reader.ReadUInt32();
            RecordState state = (RecordState)reader.ReadByte();
            long stateSetAt = reader.ReadInt64();
            bool isEncrypted = reader.ReadBoolean();
            uint position = reader.ReadUInt32();
            int length = reader.ReadInt32();
            return new MemDbPointer(id, state, stateSetAt, isEncrypted, position, length, true);
        }
        #endregion
    }
}
