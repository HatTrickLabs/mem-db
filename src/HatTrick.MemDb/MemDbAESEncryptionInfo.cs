using System;
using System.Security.Cryptography;

namespace HatTrick.InMemDb
{
    public class MemDbAESEncryptionInfo : IMemDbEncryptionInfo
    {
        #region internals
        private const int _keySize = 256;
        private const int _blockSize = 128;
        private const int _ivSize = 128;
        private static readonly CipherMode _mode = CipherMode.CBC;
        #endregion

        #region interface
        public int KeySize => _keySize;
        public int BlockSize => _blockSize;
        public int IVSize => _ivSize;
        protected CipherMode Mode => _mode;
        #endregion

        #region ctors
        public MemDbAESEncryptionInfo()
        { }
        #endregion

        #region get encrypted length
        public int GetEncryptedLength(int byteLength)
        {
            //do everything in byte len vs bit to avoid cast to unsigned int
            //cryptolen = (inputlen + (blocklen - (inputlen % blocklen))) + ivlen;
            const int blockLength = _blockSize / 8;
            const int ivLength = _ivSize / 8;

            int len = (byteLength + (blockLength - (byteLength % blockLength))) + ivLength;

            return len;
        }
        #endregion
    }
}
