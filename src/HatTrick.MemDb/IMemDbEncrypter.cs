using System;
using System.IO;

namespace HatTrick.MemDb
{
    public interface IMemDbEncrypter<T> where T : class, new()
    {
        public void Encrypt(Stream clearInput, Stream encryptedOutput, string seed);

        public void Decrypt(Stream encryptedInput, Stream clearOutput, string seed);
    }
}
