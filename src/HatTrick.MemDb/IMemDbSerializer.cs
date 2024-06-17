using System;
using System.IO;

namespace HatTrick.InMemDb
{
    public interface IMemDbSerializer<T> where T : class, new()
    {
        void Serialize(T record, BinaryWriter to);

        byte[] Serialize(T record);

        T Deserialize(BinaryReader from, int length);

        T Deserialize(ReadOnlySpan<byte> from);
    }
}
