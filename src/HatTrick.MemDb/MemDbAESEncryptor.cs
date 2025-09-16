using System;
using System.IO;
using System.Security.Cryptography;

namespace HatTrick.Data
{
    //NOTE: this class is NOT thread safe due to the re-use of Aes internal...
    public class MemDbAESEncryptor : MemDbAESEncryptionInfo, IMemDbEncryptor
    {
        #region internals
        private Aes _aes;
        private byte[] _key;
        #endregion

        #region ctors
        public MemDbAESEncryptor(byte[] key)
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            if (key.Length != (base.KeySize / 8))
                throw new InvalidOperationException($"Encryption key is not valid...MemDb AES encryption requires a {base.KeySize} bit key.");

            _key = key;
            _aes = Aes.Create();
            _aes.KeySize = base.KeySize;
            _aes.BlockSize = base.BlockSize;
            _aes.Mode = base.Mode;
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
            byte[] iv = new byte[base.IVSize / 8];
            input.ReadExactly(iv);

            int cryptoLength = base.GetEncryptedLength(length - iv.Length);

            byte[] encrypted = new byte[cryptoLength];
            input.ReadExactly(encrypted);

            byte[] raw = null;
            using (var decryptor = _aes.CreateDecryptor(_key, iv))
            {
                raw = decryptor.TransformFinalBlock(encrypted, 0, cryptoLength);
            }
            return raw;
        }
        #endregion
    }
}
