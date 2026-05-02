using System;
using System.IO;

namespace HatTrick.Data
{
    public interface IMemDbSerializer<T> where T : class
    {
        void Serialize(T record, BinaryWriter to);

        byte[] Serialize(T record);

        T Deserialize(ReadOnlySpan<byte> from);

        T Deserialize(BinaryReader from, int length);
    }

    //public interface IBinaryReadMemDbSerializer<T> : IMemDbSerializer<T> where T : class
    //{
    //    T Deserialize(BinaryReader from);
    //}
}
