// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.IO;

namespace HatTrick.Data
{
    internal class MemDbPointer
    {
        #region internals
        private byte _version = 0;//binary version...binary serialization...
        private long _id;
        private RecordState _state;
        private long _stateSetAt;
        private long _createdAt;
        private bool _isEncrypted;
        private long _position;
        private int _length;
        #endregion

        #region interface
        internal static readonly int Size 
        = sizeof(byte)//1:version
        + sizeof(long)//8:Id
        + sizeof(byte)//1:State enum : byte
        + sizeof(long)//8:StateSetAt
        + sizeof(long)//8:createdAt
        + sizeof(bool)//1:IsEncrypted
        + sizeof(long)//8:Position
        + sizeof(int);//4:Length
        //--------------------------------------
        //              39

        internal byte Version => _version;
        internal long Id => _id;
        internal RecordState State => _state;
        internal long StateSetAt => _stateSetAt;
        internal long CreatedAt => _createdAt;
        internal bool IsEncrypted => _isEncrypted;
        internal long Position => _position;
        internal int Length => _length;
        #endregion

        #region constructor
        internal MemDbPointer(long id, RecordState state, long stateSetAt, long createdAt, bool isEncrypted, long startPosition, int length)
        {
            _id = id;
            _state = state;
            _stateSetAt = stateSetAt;
            _createdAt = createdAt;
            _isEncrypted = isEncrypted;
            _position = startPosition;
            _length = length;
        }

        internal MemDbPointer(long id, RecordState state, long stateSetAt, long createdAt, bool isEncrypted)
        {
            _id = id;
            _state = state;
            _stateSetAt = stateSetAt;
            _createdAt = createdAt;
            _isEncrypted = isEncrypted;
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

        #region set position
        internal void SetPosition(long position)
        {
            _position = position;
        }
        #endregion

        #region set length
        public void SetLength(int length)
        {
            _length = length;
        }
        #endregion

        #region serialize to
        internal void SerializeTo(BinaryWriter writer)
        {
            writer.Write(_version);
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
            //if ever this class must be modified, we will have a version
            //flag to determine WHAT version the following binary data matches
            //public virtual class MemDbPointer is version 0
            //public virtual class MemDbPointerV1 : MemDbPointer is version 1
            //public virtual class MemDbPointerV2 : MemDbPointer is version 2
            //etc...
            byte version = reader.ReadByte();

            long id = reader.ReadInt64();
            RecordState state = (RecordState)reader.ReadByte();
            long stateSetAt = reader.ReadInt64();
            long createdAt = reader.ReadInt64();
            bool isEncrypted = reader.ReadBoolean();
            long position = reader.ReadInt64();
            int length = reader.ReadInt32();
            return new MemDbPointer(id, state, stateSetAt, createdAt, isEncrypted, position, length);
        }
        #endregion
    }
}
