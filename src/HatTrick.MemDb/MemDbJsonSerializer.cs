using System;
using System.IO;
using System.Text.Json;

namespace HatTrick.MemDb
{
    internal class MemDbJsonSerializer<T> : IMemDbSerializer<T> where T : class, new()
    {
        public T Deserialize(BinaryReader from)
        {
            throw new NotImplementedException();
        }

        public void Serialize(T record, BinaryWriter to)
        {
            throw new NotImplementedException();
        }
    }
}
