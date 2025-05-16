using System;
using System.IO;

namespace HatTrick.InMemDb
{
    public interface IMemDbEncryptor
    {
        public void Encrypt(byte[] input, Stream output);

        public byte[] Decrypt(Stream input, int length);
    }
}
