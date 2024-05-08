using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HatTrick.MemDb
{
    internal struct MemDbPointer
    {
        #region interface
        internal static readonly int Size = 14;

        internal int Id;
        internal bool IsStale;
        internal bool IsEncrypted;
        internal int Position;
        internal int Length;
        #endregion

        #region constructor
        internal MemDbPointer(int id, bool isStale, bool isEncrypted, int startPosition, int length)
        {
            this.Id = id;
            this.IsStale = isStale;
            this.IsEncrypted = isEncrypted;
            this.Position = startPosition;
            this.Length = length;
        }
        #endregion

        #region serialization
        internal void SerializeTo(BinaryWriter writer)
        {

            writer.Write(this.Id);
            writer.Write(this.IsStale);
            writer.Write(this.IsEncrypted);
            writer.Write(this.Position);
            writer.Write(this.Length);
        }

        internal static MemDbPointer DeserializeFrom(BinaryReader reader)
        {
            int id = reader.ReadInt32();
            bool isStale = reader.ReadBoolean();
            bool isEncrypted = reader.ReadBoolean();
            int position = reader.ReadInt32();
            int length = reader.ReadInt32();
            return new MemDbPointer(id, isStale, isEncrypted, position, length);
        }
        #endregion
    }
}
