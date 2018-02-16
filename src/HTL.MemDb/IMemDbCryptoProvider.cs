using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HatTrick.MemDb
{
    public interface IMemDbCryptoProvider
    {
        void Encrypt(Stream clearInput, Stream encryptedOutput, string seed);

        void Decrypt(Stream encryptedInput, Stream clearOutput, string seed);
    }
}
