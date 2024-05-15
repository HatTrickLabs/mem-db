using System;
using System.IO;

namespace HatTrick.MemDb
{
    public interface IMemDbEncrypter<T> where T : class, new()
    {
        void Encrypt(Stream clearInput, Stream encryptedOutput, string seed);

        void Decrypt(Stream encryptedInput, Stream clearOutput, string seed);
    }
}
