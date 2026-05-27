using System;
using System.IO;

namespace HatTrick.Data
{
    public interface IMemDbEncryptor : IMemDbEncryptionInfo
    {
        public void EncryptTo(byte[] input, Stream output);

        public byte[] DecryptFrom(Stream input, int length);
    }
}
