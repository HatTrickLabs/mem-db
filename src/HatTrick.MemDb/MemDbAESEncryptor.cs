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

        #region calculate crypto byte length
        /// <summary>
        /// The 'MemDbAESEncryptor.Decrypt' method requires that we know the unencrypted length of the raw data.
        /// During the 'Decrypt' process we want to read directly from the mem-db data file stream by providing 
        /// that data stream into the constructor of the crypto stream.   We then read from the crypto stream until
        /// we get the exact number of bytes we know is the unencrypted length of the serailized object.  See the notes
        /// at the end of the 'MemDbAESEncrytor.Decrypt' method for more info.
        /// </summary>
        /// <param name="byteLength">The length of an unencrypted span, array or stream of bytes.</param>
        /// <returns>The byte length of the encrypted output that will be returned from the 'Encrypt' method of this class.</returns>
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

            //Obviously this AES implementation does not expect a stream full of intermingled
            //clear and encrypted data...The crypto stream over reads the provided input stream while buffering...
            //This is just a hack, I know exactly how much encrypted data I want decrypted, so I'm going to
            //shift the stream position back to where it should have stopped consuming.
            //Its either this hack, or alloc ANOTHER stream instance for every encrypted record and copy
            //the exact amount of encrypted data from the primary memdb.db file stream into the new stream 
            //before calling Decrypt...This seems a bit more efficient.
            input.Position = initPos + MemDbAESEncryptor.CalculateCryptoByteLength(length);

            return raw;
        }
        #endregion
    }
}
