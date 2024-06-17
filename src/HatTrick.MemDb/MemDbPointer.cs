using System;
using System.IO;

namespace HatTrick.InMemDb
{
    internal class MemDbPointer
    {
        #region internals
        private uint _id;
        private RecordState _state;
        private bool _isEncrypted;
        private uint _position;
        private int _length;

        //not serialized
        private bool _flushed;
        #endregion

        #region interface
        internal static readonly int Size = sizeof(int) //Id
                                          + sizeof(RecordState)//State
                                          + sizeof(bool)//IsEncrypted
                                          + sizeof(uint)//Position
                                          + sizeof(int);//Length

        internal uint Id => _id;
        internal RecordState State => _state;
        internal bool IsEncrypted => _isEncrypted;
        internal uint Position => _position;
        internal int Length => _length;

        internal bool Flushed => _flushed;
        #endregion

        #region constructor
        internal MemDbPointer(uint id, RecordState state, bool isEncrypted, uint startPosition, int length, bool flushed = false)
        {
            _id = id;
            _state = state;
            _isEncrypted = isEncrypted;
            _position = startPosition;
            _length = length;
            _flushed = flushed;
        }
        #endregion

        #region mark stale
        internal MemDbPointer MarkStale()
        {
            _state = RecordState.Stale;
            return this;
        }
        #endregion

        #region serialize to
        internal void SerializeTo(BinaryWriter writer)
        {
            writer.Write(_id);
            writer.Write((byte)_state);
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
            bool isEncrypted = reader.ReadBoolean();
            uint position = reader.ReadUInt32();
            int length = reader.ReadInt32();
            return new MemDbPointer(id, state, isEncrypted, position, length, true);
        }
        #endregion
    }
}
