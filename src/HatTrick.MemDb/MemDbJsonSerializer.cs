using System;
using System.IO;
using System.Text.Json;

namespace HatTrick.MemDb
{
    internal class MemDbJsonSerializer<T> : IMemDbSerializer<T> where T : class, new()
    {
        public T DeserializeFrom(BinaryReader from)
        {
            throw new NotImplementedException();
        }

        public void SerializeTo(T record, BinaryWriter to)
        {
            throw new NotImplementedException();
        }
    }
}
