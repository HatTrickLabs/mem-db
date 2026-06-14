// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.IO;

namespace HatTrick.Data.TestHarness
{
    public class AESKeyFromPasswordEncryptedTests : AESEncryptionTests
    {
        #region ctors
        public AESKeyFromPasswordEncryptedTests(AssetResolver assetResolver) : base(assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(base.Dataset, base.DbPath)
                .EncryptWithPassword(() => "This is my super secure password used for AES encryption...!!!")
                .Register();
        }
        #endregion
    }

}
