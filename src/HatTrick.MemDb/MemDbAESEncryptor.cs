using System;
using System.IO;
using System.Security.Cryptography;

namespace HatTrick.InMemDb
{
    public class MemDbAESEncryptor : IMemDbEncryptor
    {
        #region internals
        private const int _keySize = 256;
        private const int _blockSize = 128;
        private const int _ivSize = 128;
        private static readonly CipherMode _mode = CipherMode.CBC;

        private byte[] _key;
        private Aes _aes;
        #endregion

        #region ctors
        public MemDbAESEncryptor(byte[] key)
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            if (key.Length != (_keySize / 8))
                throw new InvalidOperationException($"Encryption key is not valid...MemDb AES encryption requires a {_keySize} bit key.");

            _key = key;
            _aes = Aes.Create();
            _aes.KeySize = _keySize;
            _aes.BlockSize = _blockSize;
            _aes.Mode = _mode;
        }
        #endregion

        #region calculate crypto byte length
        public static int CalculateCryptoByteLength(int byteLength)
        {
            //do everything in byte len vs bit to avoid cast to unsigned int
            //cryptolen = (inputlen + (blocklen - (inputlen % blocklen))) + ivlen;
            const int blockLength = _blockSize / 8;
            const int ivLength = _ivSize / 8;

            int len = (byteLength + (blockLength - (byteLength % blockLength))) + ivLength;

            return len;
        }
        #endregion

        #region encrypt
        public void Encrypt(byte[] input, Stream output)
        {
            _aes.GenerateIV();
            byte[] iv = _aes.IV;

            output.Write(iv);

            using (var encryptor = _aes.CreateEncryptor(_key, iv))
            {
                byte[] encrypted = encryptor.TransformFinalBlock(input, 0, input.Length);
                output.Write(encrypted);
            }
        }
        #endregion

        #region decrypt
        public byte[] Decrypt(Stream input, int length)
        {
            byte[] iv = new byte[_ivSize / 8];
            input.ReadExactly(iv);

            int cryptoLength = CalculateCryptoByteLength(length - iv.Length);

            byte[] encrypted = new byte[cryptoLength];
            input.ReadExactly(encrypted);

            byte[] raw = null;
            using (var decryptor = _aes.CreateDecryptor(_key, iv))
            {
                raw = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
            }
            return raw;
        }
        #endregion
    }
}
