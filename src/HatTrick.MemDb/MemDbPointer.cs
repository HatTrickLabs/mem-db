using System;
using System.IO;

namespace HatTrick.MemDb
{
    internal class MemDbPointer
    {
        #region internals
        private int _id;
        private bool _isStale;
        private bool _isEncrypted;
        private long _position;
        private int _length;
        #endregion

        #region interface
        internal static readonly int Size = sizeof(int) + sizeof(bool) + sizeof(bool) + sizeof(long) + sizeof(int);

        internal int Id => _id;
        internal bool IsStale => _isStale;
        internal bool IsEncrypted => _isEncrypted;
        internal long Position => _position;
        internal int Length => _length;
        #endregion

        #region constructor
        internal MemDbPointer(int id, bool isStale, bool isEncrypted, long startPosition, int length)
        {
            _id = id;
            _isStale = isStale;
            _isEncrypted = isEncrypted;
            _position = startPosition;
            _length = length;
        }
        #endregion

        #region mark stale
        internal MemDbPointer MarkStale()
        {
            _isStale = true;
            return this;
        }
        #endregion

        #region serialization
        internal void SerializeTo(BinaryWriter writer)
        {
            writer.Write(_id);
            writer.Write(_isStale);
            writer.Write(_isEncrypted);
            writer.Write(_position);
            writer.Write(_length);
        }

        internal static MemDbPointer DeserializeFrom(BinaryReader reader)
        {
            int id = reader.ReadInt32();
            bool isStale = reader.ReadBoolean();
            bool isEncrypted = reader.ReadBoolean();
            long position = reader.ReadInt64();
            int length = reader.ReadInt32();
            return new MemDbPointer(id, isStale, isEncrypted, position, length);
        }
        #endregion
    }
}
