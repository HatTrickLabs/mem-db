// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.IO;

namespace HatTrick.Data.TestHarness
{
    public class AutoIncrementIdentityTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "identity");
        #endregion

        #region ctors
        public AutoIncrementIdentityTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath).Register();
        }
        #endregion

        #region ensure auto increment identity accuracy
        public void Test_EnsureAutoIncrementIdentityAccuracy()
        {
            DigitalAsset[] assets = base.ResolveAssetSet();

            int length = assets.Length;

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                for (int i = 0; i < length; i++)
                {
                    var a = assets[i];
                    //utilize the identity delegate callback to set the id on the asset (happens before deep clone)
                    db.Insert(a, (id) => a.Id = id, false);
                }
                //ensure all assets have an identity
                Assert.IsEqual(db.Count(a => a.Id > 0), assets.Length);

                //close and flush
            }

            //re-open
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                //make sure we have the standard 1000 rec count.
                Assert.IsEqual<int>(db.Count(), length);

                //assert each 
                for (int i = 0; i < length; i++)
                {
                    int id = i + 1;//identity should start at 1
                    //ensure we find a record for each unique auto incremented id
                    Assert.IsNotNull(db.Find(a => a.Id == id));
                }
            }
        }
        #endregion
    }
}
