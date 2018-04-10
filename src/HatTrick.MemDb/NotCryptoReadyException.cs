using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.MemDb
{
    public class NotCryptoReadyException : Exception
    {
        private static readonly string _msg = @"'Insert\Update' of 'IsEncrypted' record attempted but no IMemDbCryptoProvider has been registered.  See MemDb.RegisterCryptoProvider";
        public NotCryptoReadyException() : base(_msg)
        {
        }
    }
}
