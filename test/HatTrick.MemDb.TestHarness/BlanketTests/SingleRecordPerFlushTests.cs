// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.IO;
using System.Diagnostics;

namespace HatTrick.Data.TestHarness
{
    public class SingleRecordPerFlushTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "single-rec-flush");
        #endregion

        #region ctors
        public SingleRecordPerFlushTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .SetFlushInterval(0)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .EncryptWithPassword(() => "Helphelphelphelphelphelp")
                .Register();
        }
        #endregion

        #region single flush
        public void Test_SingleFlush()
        {
            var sw = new Stopwatch();
            var assets = base.ResolveAssetSet();
            sw.Start();
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i], true);
                }
                db.Flush();
            }
            sw.Stop();
            Console.WriteLine($"Inserted {assets.Length} records (1 flush post inserts) in {sw.ElapsedMilliseconds} milliseconds.");
            base.Cleanup();
        }
        #endregion

        #region single record per flush
        public void Test_SingleRecordPerFlush()
        {
            var sw = new Stopwatch();
            var assets = base.ResolveAssetSet();
            sw.Start();
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i], true);
                    db.Flush();
                }
            }
            sw.Stop();
            Console.WriteLine($"Inserted {assets.Length} records (1 rec per flush) in {sw.ElapsedMilliseconds} milliseconds.");
        }
        #endregion
    }
}
