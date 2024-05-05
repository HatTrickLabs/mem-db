using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HatTrick.MemDb
{
    public class MemDbMap
    {
        #region internals
        private int _pointerCount;
        private List<MemDbPointer> _pointers;
        #endregion

        #region interface
        public int PointerCount { get { return _pointerCount; } }

        public int ByteLength { get { return PointerCount * MemDbPointer.Size; } }

        public List<MemDbPointer> Pointers { get { return _pointers; } }
        #endregion

        #region constructors
        public MemDbMap()
        {
            _pointerCount = 0;
            _pointers = new List<MemDbPointer>(2048);
        }
        #endregion

        #region add pointer
        public void AddPointer(MemDbPointer pointer)
        {
            this.Pointers.Add(pointer);
            _pointerCount += 1;
        }
        #endregion

        #region serialization
        public int SerializeTo(Stream buffer)
        {
            int length = 0;
            buffer.Write(BitConverter.GetBytes(this.PointerCount), 0, 4);
            length += 4;

            foreach (MemDbPointer p in this.Pointers)
            {
                length += p.SerializeTo(buffer);
            }

            return length;
        }

        public int DeserializeFrom(Stream buffer)
        {
            int length = 0;
            byte[] buff = new byte[4];

            buffer.Read(buff, 0, 4);
            int count = BitConverter.ToInt32(buff, 0);
            _pointerCount = count;
            length += 4;

            if (_pointerCount > 2048)
            {
                _pointers = new List<MemDbPointer>((int)(_pointerCount * 1.5));
            }

            MemDbPointer p;
            for (int i = 0; i < count; i++)
            {
                length += MemDbPointer.DeserializeFrom(buffer, out p);
                this.Pointers.Add(p);
            }

            return length;
        }
        #endregion
    }
}
