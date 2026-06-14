// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.IO;

namespace HatTrick.Data.TestHarness
{
    public class FlushTimerTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "flush_timer");
        #endregion

        #region ctors
        public FlushTimerTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
        }
        #endregion

        #region load db
        protected void LoadDb(MemDb<DigitalAsset> db)
        {
            DigitalAsset[] assets = base.ResolveAssetSet();
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                db.Insert(asset, (id) => asset.Id = id);
            }
        }
        #endregion

        #region less than zero throws
        public void Test_LessThanZeroThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                when: () => {
                    MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                        .SetFlushInterval(-1)
                        .Register();
                },
                messageContains: "Argument cannot be less than 0."
            );
        }
        #endregion

        #region greater than max allowed throws
        public void Test_GreaterThanMaxAllowedThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                when: () => {
                    MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                        .SetFlushInterval(MemDbConfiguration.MaxFlushIntervalSeconds + 1)
                        .Register();
                },
                messageContains: "Max allowed flush interval is"
            );
        }
        #endregion

        #region can increase flush timer
        public void Test_CanIncreaseFlushTimer()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .SetFlushInterval(10)
                .Register();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
            }

            MemDb.RemoveConfigurationFor(_dataset);
        }
        #endregion

        #region can decrease fush timer
        public void Test_CanDecreaseFlushTimer()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .SetFlushInterval(3)
                .Register();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
            }

            MemDb.RemoveConfigurationFor(_dataset);
        }
        #endregion

        #region can disable flush timer
        public void Test_CanDisableFlushTimer()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .SetFlushInterval(0)
                .Register();

            int initialCount = 0;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                initialCount = db.Count();
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Assert.IsEqual(db.Count(), initialCount); 
            }

            MemDb.RemoveConfigurationFor(_dataset);
        }
        #endregion

        #region can manually flush when flush timer disabled
        public void Test_CanManuallyFlushWhenFlushTimerDisabled()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .SetFlushInterval(0)
                .Register();

            int count = 0;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                db.Flush();
                db.Flush();
                db.Flush();
                count = db.Count();
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Assert.IsEqual(db.Count(), count);
            }

            MemDb.RemoveConfigurationFor(_dataset);
        }
        #endregion
    }
}
