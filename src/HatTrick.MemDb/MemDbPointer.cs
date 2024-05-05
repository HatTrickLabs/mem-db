using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HatTrick.MemDb
{
    public struct MemDbPointer
    {
        #region interface
        public static readonly int Size = 14;

        public int Id;
        public bool IsStale;
        public bool IsEncrypted;
        public int Position;
        public int Length;
        #endregion

        #region constructor
        public MemDbPointer(int id, bool isStale, bool isEncrypted, int startPosition, int length)
        {
            this.Id = id;
            this.IsStale = isStale;
            this.IsEncrypted = isEncrypted;
            this.Position = startPosition;
            this.Length = length;
        }
        #endregion

        #region serialization
        public int SerializeTo(Stream buffer)
        {

            byte[] buff = new byte[MemDbPointer.Size];
            this.SerializeTo(buff, 0);
            buffer.Write(buff, 0, MemDbPointer.Size);

            return MemDbPointer.Size;
        }

        public int SerializeTo(byte[] buffer, int index)
        {
            int position = index;

            BitConverter.GetBytes(this.Id).CopyTo(buffer, position);
            position += 4;

            BitConverter.GetBytes(this.IsStale).CopyTo(buffer, position);
            position += 1;

            BitConverter.GetBytes(this.IsEncrypted).CopyTo(buffer, position);
            position += 1;

            BitConverter.GetBytes(this.Position).CopyTo(buffer, position);
            position += 4;

            BitConverter.GetBytes(this.Length).CopyTo(buffer, position);
            position += 4;

            return (position - index);
        }

        public static int DeserializeFrom(Stream buffer, out MemDbPointer pointer)
        {
            byte[] buf = new byte[MemDbPointer.Size];
            buffer.Read(buf, 0, MemDbPointer.Size);
            return MemDbPointer.DeserializeFrom(buf, 0, out pointer);
        }

        public static int DeserializeFrom(byte[] buffer, int index, out MemDbPointer pointer)
        {
            int at = index;

            int id = BitConverter.ToInt32(buffer, at);
            at += 4;

            bool isStale = BitConverter.ToBoolean(buffer, at);
            at += 1;

            bool isEncrypted = BitConverter.ToBoolean(buffer, at);
            at += 1;

            int startPosition = BitConverter.ToInt32(buffer, at);
            at += 4;

            int length = BitConverter.ToInt32(buffer, at);
            at += 4;

            pointer = new MemDbPointer(id, isStale, isEncrypted, startPosition, length);

            return MemDbPointer.Size; //byte length deserialized... (4 + 1 + 4 + 4)
        }
        #endregion
    }
}
