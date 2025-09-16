using System;
using System.IO;

namespace HatTrick.Data
{
    public interface IMemDbSerializer<T> where T : class
    {
        void Serialize(T record, BinaryWriter to);

        byte[] Serialize(T record);

        T Deserialize(ReadOnlySpan<byte> from);
    }

    public interface IBinaryReadMemDbSerializer<T> : IMemDbSerializer<T> where T : class
    {
        T Deserialize(BinaryReader from);
    }
}
