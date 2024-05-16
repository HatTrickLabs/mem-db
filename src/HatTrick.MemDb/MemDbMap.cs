using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace HatTrick.MemDb
{
    internal sealed class MemDbMap
    {
        #region internals
        private List<MemDbPointer> _pointers;
        #endregion

        #region interface
        //internal int ByteLength => _pointers.Count * MemDbPointer.Size;

        internal List<MemDbPointer> Pointers => _pointers;
        #endregion

        #region constructors
        internal MemDbMap()
        {
            _pointers = new List<MemDbPointer>();
        }

        internal MemDbMap(int capacity)
        {
            _pointers = new List<MemDbPointer>(capacity);
        }

        private MemDbMap(List<MemDbPointer> pointers)
        {
            _pointers = pointers;
        }
        #endregion

        #region from
        internal static MemDbMap Create(List<MemDbPointer> pointers)
        {
            MemDbMap map = new MemDbMap(pointers);
            return map;
        }
        #endregion

        #region add pointer
        internal void AddPointer(MemDbPointer pointer)
        {
            this.Pointers.Add(pointer);
        }
        #endregion

        #region serialization
        internal void SerializeTo(Stream stream)
        {
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                this.SerializeTo(writer);
            }
        }

        internal void SerializeTo(BinaryWriter writer)
        {
            writer.Write(_pointers.Count);

            foreach (MemDbPointer p in this.Pointers)
            {
                p.SerializeTo(writer);
            }
        }

        internal void DeserializeFrom(BinaryReader reader)
        {
            int count = reader.ReadInt32();

            _pointers = new List<MemDbPointer>(count);

            for (int i = 0; i < count; i++)
            {
                _pointers.Add(MemDbPointer.DeserializeFrom(reader));
            }
        }
        #endregion
    }
}
