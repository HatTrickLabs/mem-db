using System;
using System.IO;

namespace HatTrick.Data.TestHarness
{
    public class AESKeyEncryptedTests : AESEncryptionTests
    {
        #region ctors
        public AESKeyEncryptedTests(AssetResolver assetResolver) : base(assetResolver)
        {
            var key = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 };
            MemDb.ConfigureFor<DigitalAsset>(base.Dataset, base.DbPath)
                .EncryptWithKey(() => key)
                .Register();
        }
        #endregion
    }
}
