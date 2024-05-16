using System;
using System.IO;

namespace HatTrick.MemDb
{
    public interface IMemDbSerializer<T> where T : class, new()
    {
        void Serialize(T record, BinaryWriter to);

        T Deserialize(BinaryReader from);
    }
}
