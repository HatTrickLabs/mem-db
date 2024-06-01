using System;
using System.IO;
using System.Security.Cryptography;


namespace HatTrick.MemDb
{
    public class MemDbAESEncryptor : IMemDbEncryptor
    {
        #region internals
        private static readonly int _keySize = 256;
        private static readonly int _blockSize = 128;
        private static readonly int _ivSize = 128;
        private static readonly CipherMode _mode = CipherMode.CBC;

        private byte[] _key;
        #endregion

        #region ctors
        public MemDbAESEncryptor(byte[] key)
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            if (key.Length != (_keySize / 8))
                throw new InvalidOperationException($"Encryption key is not valid...MemDb AES encryption requires a {_keySize} bit key.");

            _key = key;
        }
        #endregion

        #region calculate crypto length
        public static int CalculateCryptoByteLength(int byteLength)
        {
            //lets do everything in byte len vs bit to avoid cast to unsigned int

            //cryptolen = (inputlen + (blocklen - (inputlen % blocklen))) + ivlen;

            int blockLength = _blockSize / 8;
            int ivLength = _ivSize / 8;

            int len = (byteLength + (blockLength - (byteLength % blockLength))) + ivLength;

            return len;
        }
        #endregion

        #region encrypt
        public void Encrypt(ReadOnlySpan<byte> input, Stream output)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = _keySize;
                aes.BlockSize = _blockSize;
                aes.Mode = _mode;
                byte[] iv = aes.IV;

                output.Write(iv);

                var encryptor = aes.CreateEncryptor(_key, iv);
                using (var cryptoStream = new CryptoStream(output, encryptor, CryptoStreamMode.Write, true))
                {
                    cryptoStream.Write(input);
                    cryptoStream.FlushFinalBlock();
                }
            }
        }
        #endregion

        #region decrypt
        public byte[] Decrypt(Stream input, int length)
        {
            long initPos = input.Position;

            byte[] raw = new byte[length];//hint: raw length not crypto length...
            int read = 0;

            using (var aes = Aes.Create())
            {
                aes.KeySize = _keySize;
                aes.BlockSize = _blockSize;
                aes.Mode = _mode;

                byte[] iv = new byte[_ivSize / 8];
                input.ReadExactly(iv);

                var decryptor = aes.CreateDecryptor(_key, iv);
                using (var cryptoStream = new CryptoStream(input, decryptor, CryptoStreamMode.Read, true))
                {
                    //the read count is UNENCRYPTED length
                    do
                    {
                        read += cryptoStream.Read(raw, read, (length - read));
                    } 
                    while (read < length);
                }
            }

            //obviously the this AES implementation does not expect a stream full of intermingled...
            //clear and encrypted data...It over F*cking reads the provided stream while buffering...
            //this is just a sh*tty temp hack, I know exactly how much encrypted data I want decrypted...
            //so I'm going to shift the stream position back to where it should have stopped consuming.
            input.Position = initPos + MemDbAESEncryptor.CalculateCryptoByteLength(length);

            return raw;
        }
        #endregion
    }
}
