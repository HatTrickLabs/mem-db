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
        private long _createdAt;
        private bool _isEncrypted;
        private uint _position;
        private int _length;
        #endregion

        #region interface
        internal static readonly int Size = sizeof(uint)//Id:4
                                          + sizeof(byte)//State:1 (enum : byte)
                                          + sizeof(long)//StateSetAt:8
                                          + sizeof(long)//createdAt:8
                                          + sizeof(bool)//IsEncrypted:1
                                          + sizeof(uint)//Position:4
                                          + sizeof(int);//Length:4
                                                        //----------------
                                                        //30

        internal uint Id => _id;
        internal RecordState State => _state;
        internal long StateSetAt => _stateSetAt;
        internal long CreatedAt => _createdAt;
        internal bool IsEncrypted => _isEncrypted;
        internal uint Position => _position;
        internal int Length => _length;
        #endregion

        #region constructor
        internal MemDbPointer(uint id, RecordState state, long stateSetAt, long createdAt, bool isEncrypted, uint startPosition, int length)
        {
            _id = id;
            _state = state;
            _stateSetAt = stateSetAt;
            _createdAt = createdAt;
            _isEncrypted = isEncrypted;
            _position = startPosition;
            _length = length;
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

        #region serialize to
        internal void SerializeTo(BinaryWriter writer)
        {
            writer.Write(_id);
            writer.Write((byte)_state);
            writer.Write(_stateSetAt);
            writer.Write(_createdAt);
            writer.Write(_isEncrypted);
            writer.Write(_position);
            writer.Write(_length);
        }
        #endregion

        #region deserializer from
        internal static MemDbPointer DeserializeFrom(BinaryReader reader)
        {
            uint id = reader.ReadUInt32();
            RecordState state = (RecordState)reader.ReadByte();
            long stateSetAt = reader.ReadInt64();
            long createdAt = reader.ReadInt64();
            bool isEncrypted = reader.ReadBoolean();
            uint position = reader.ReadUInt32();
            int length = reader.ReadInt32();
            return new MemDbPointer(id, state, stateSetAt, createdAt, isEncrypted, position, length);
        }
        #endregion
    }
}
