using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb
{
    public interface IMemDbEncryptionInfo
    {
        int KeySize { get; }
        int BlockSize { get; }
        int IVSize { get; }

        int GetEncryptedLength(int byteLength);

        //int GetEncryptedLength(int byteLength)
        //{
        //    int blockLength = this.BlockSize / 8;
        //    int ivLength = this.IVSize / 8;
        //    int len = (byteLength + (blockLength - (byteLength % blockLength))) + ivLength;
        //    return len;
        //}
    }
}
