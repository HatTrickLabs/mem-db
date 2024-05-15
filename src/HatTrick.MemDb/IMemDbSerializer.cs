using System;
using System.IO;

namespace HatTrick.MemDb
{
    public interface IMemDbSerializer<T> where T : class, new()
    {
        void SerializeTo(T record, BinaryWriter to);

        T DeserializeFrom(BinaryReader from);
    }
}
