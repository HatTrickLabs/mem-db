using System;
using System.IO;

namespace HatTrick.MemDb
{
    public interface IMemDbEncryptor
    {
        public void Encrypt(ReadOnlySpan<byte> input, Stream output);

        public byte[] Decrypt(Stream input, int length);
    }
}
