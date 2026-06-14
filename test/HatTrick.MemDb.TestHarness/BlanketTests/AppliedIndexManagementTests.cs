// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.IO;
using System.Linq;
using HatTrick.Data;

namespace HatTrick.Data.TestHarness
{
    internal class AppliedIndexManagementTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "index_management");
        #endregion

        #region ctors
        public AppliedIndexManagementTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .IndexOnIdentity(true)
                .ApplyIndex<string>(nameof(DigitalAsset.Name), (a) => a.Name)
                .ApplyIndex<DateTime>(nameof(DigitalAsset.Imported), (a) => a.Imported)
                .ApplyIndex<ulong>(nameof(DigitalAsset.XXHash), (a) => a.XXHash)
                .Register();
        }
        #endregion

        #region load db
        protected void LoadDb(MemDb<DigitalAsset> db)
        {
            DigitalAsset[] assets = base.ResolveAssetSet();
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                db.Insert(asset, (id) => asset.Id = id, false);
            }
        }
        #endregion

        #region index build
        public void Test_IndexBuildAndMatch()
        {
            DateTime now = DateTime.Now;
            System.Threading.Thread.Sleep(5);
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                int cnt = db.Count();
                db.Flush();

                var all = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsGreaterThan(now).ToArray();
                var all2 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsGreaterThanEqualTo(now).ToArray();
                Assert.IsEqual(all.Length, cnt);
                Assert.IsEqual(all2.Length, cnt);

                System.Threading.Thread.Sleep(10);

                DateTime now2 = DateTime.Now;
                var all3 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsLessThan(now2).ToArray();
                var all4 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsLessThanEqualTo(now2).ToArray();
                Assert.IsEqual(all3.Length, cnt);
                Assert.IsEqual(all4.Length, cnt);


                DateTime importedAt = db.Find(1).Imported;//all record will have the same imported timestamp
                var all5 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsEqualTo(importedAt).ToArray();
                Assert.IsEqual(all5.Length, cnt);

                var none = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsNotEqualTo(importedAt).ToArray();
                Assert.IsEqual(none.Length, 0);
            }
        }
        #endregion

        #region index refresh
        public void Test_IndexRefresh()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                db.Flush();

                //at this point asset.XXHash values are all 0, index with keyOf(0) is int[1000] pointers

                var ids = db.Query().Select(a => a.Id).ToArray();

                DateTime dt = db.Find(a => a.Id == 1).Imported;

                for (int i = 0; i < ids.Length; i++)
                {
                    //at this point, all asset.XXHash values are 0...
                    //set all xxhash values to i (0 thru 999)
                    db.Update(a => a.XXHash = (ulong)i, ids[i]);
                    //the update will gradually flatten the index of id from keyOf(0) is int[1000] pointers
                    //to 999 keyOf(xxhash) is int[1] pointers

                    //at this point, all asset.Imported values are equal
                    //add i milliseconds to every record.
                    db.Update(a => a.Imported = a.Imported.AddMilliseconds(i), ids[i]);
                }

                //assert index query results
                var set = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsGreaterThanEqualTo(500).ToArray();
                Assert.IsEqual(set.Length, 500);

                var set2 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsGreaterThan(500).ToArray();
                Assert.IsEqual(set2.Length, 499);

                var set3 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsLessThanEqualTo(500).ToArray();
                Assert.IsEqual(set3.Length, 501);

                var set4 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsLessThan(500).ToArray();
                Assert.IsEqual(set4.Length, 500);

                var set5 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsEqualTo(500).ToArray();
                Assert.IsEqual(set5.Length, 1);

                var set6 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsNotEqualTo(500).ToArray();
                Assert.IsEqual(set6.Length, 999);

                var set6plus = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).In(500).ToArray();
                Assert.IsEqual(set6plus.Length, 1);



                var set7 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsGreaterThanEqualTo(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set7.Length, 900);

                var set8 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsGreaterThan(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set8.Length, 899);

                var set9 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsLessThanEqualTo(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set9.Length, 101);

                var set10 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsLessThan(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set10.Length, 100);

                var set11 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsEqualTo(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set11.Length, 1);

                var set12 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsNotEqualTo(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set12.Length, 999);

                var set13 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).In(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set13.Length, 1);
            }
        }
        #endregion

        #region index remove
        public void Test_IndexRemove()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                db.Flush();

                var assets = db.Query().OrderBy((a, b) => a.Id.CompareTo(b.Id)).ToArray();
                foreach (var asset in assets)
                {
                    //delete all even id assets...
                    if (asset.Id % 2 == 0)
                        db.Delete(asset.Id);
                }

                var set1 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsLessThanEqualTo("0500.json").ToArray();
                Assert.IsEqual(set1.Length, 251);
                var set2 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsLessThan("0500.json").ToArray();
                Assert.IsEqual(set2.Length, 250);
                var set3 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsGreaterThanEqualTo("0500.json").ToArray();
                Assert.IsEqual(set3.Length, 250);
                var set4 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsGreaterThan("0500.json").ToArray();
                Assert.IsEqual(set4.Length, 249);
                var set5 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsEqualTo("0500.json").ToArray();
                Assert.IsEqual(set5.Length, 1);
                var set6 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsNotEqualTo("0500.json").ToArray();
                Assert.IsEqual(set6.Length, 499);
                var set7 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsEqualTo(0).ToArray();
                Assert.IsEqual(set7.Length, 500);
                var set8 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsNotEqualTo(0).ToArray();
                Assert.IsEqual(set8.Length, 0);
                var set9 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsLessThan(0).ToArray();
                Assert.IsEqual(set9.Length, 0);
                var set10 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsGreaterThan(0).ToArray();
                Assert.IsEqual(set10.Length, 0);
                var set11 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsGreaterThanEqualTo(0).ToArray();
                Assert.IsEqual(set11.Length, 500);
                var set12 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsLessThanEqualTo(0).ToArray();
                Assert.IsEqual(set12.Length, 500);
                var set13 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).In(0).ToArray();
                Assert.IsEqual(set13.Length, 500);
            }
        }
        #endregion

        #region index refresh plus purge
        public void Test_IndexRefreshPlushPurge()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                db.Update(a => a.XXHash = (ulong)a.Id, a => true);
                var purged = db.PurgeCache();

                int seconds = 0;
                DateTime now = DateTime.Now;
                db.Update(a => a.Imported = now.AddSeconds(seconds++), a => true);
                purged = db.PurgeCache();

                DateTime[] distinctImported = db.Query()
                    .OrderBy((a, b) => a.Imported.CompareTo(b.Imported))
                    .SelectDistinct(a => a.Imported)
                    .ToArray();

                Assert.IsEqual(distinctImported.Length, db.Count());

                seconds = 0;
                for (int i = 0; i < distinctImported.Length; i++)
                {
                    Assert.IsEqual(distinctImported[i], now.AddSeconds(seconds++));
                }

                distinctImported = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported))
                    .IsGreaterThanEqualTo(now)
                    .OrderBy((a, b) => a.Imported.CompareTo(b.Imported))
                    .SelectDistinct(a => a.Imported)
                    .ToArray();

                Assert.IsEqual(distinctImported.Length, db.Count());

                distinctImported = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported))
                    .IsLessThanEqualTo(now.AddSeconds(1_000))
                    .OrderBy((a, b) => a.Imported.CompareTo(b.Imported))
                    .SelectDistinct(a => a.Imported)
                    .ToArray();

                Assert.IsEqual(distinctImported.Length, db.Count());

                distinctImported = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported))
                    .IsLessThanEqualTo(now.AddSeconds(499))
                    .OrderBy((a, b) => a.Imported.CompareTo(b.Imported))
                    .SelectDistinct(a => a.Imported)
                    .ToArray();

                Assert.IsEqual(distinctImported.Length, 500);

                distinctImported = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported))
                    .IsGreaterThanEqualTo(now.AddSeconds(500))
                    .OrderBy((a, b) => a.Imported.CompareTo(b.Imported))
                    .SelectDistinct(a => a.Imported)
                    .ToArray();

                Assert.IsEqual(distinctImported.Length, 500);

                distinctImported = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported))
                    .IsLessThan(now.AddSeconds(500))
                    .OrderBy((a, b) => a.Imported.CompareTo(b.Imported))
                    .SelectDistinct(a => a.Imported)
                    .ToArray();

                Assert.IsEqual(distinctImported.Length, 500);
            }
        }
        #endregion

        #region index based updates
        public void Test_IndexBasedUpdates()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);

                DateTime now = DateTime.Now;

                string[] names = db.QueryViaIndex<string>(nameof(DigitalAsset.Name))
                    .IsGreaterThan("!")
                    .OrderBy((a, b) => a.Name.CompareTo(b.Name))
                    .SelectDistinct<string>(a => a.Name);

                Assert.IsEqual(names.Length, db.Count());

                int nextId = 0;
                foreach (string name in names)
                {
                    DigitalAsset[] result = db.QueryViaIndex<string>(nameof(DigitalAsset.Name))
                        .IsEqualTo(name)
                        .ToArray();

                    Assert.IsEqual(result.Length, 1);
                    //we ordered them by name asc...the ids should also be in asc order.
                    Assert.IsEqual(result[0].Id, ++nextId);
                }

                //delete 0499.txt
                int count = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsEqualTo("0499.txt").Delete();
                Assert.IsEqual(count, 1);

                var asset499 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsEqualTo("0499.txt").ToArray();
                Assert.IsEqual(asset499.Length, 0);
                Assert.IsEqual(db.Count(), 999);
            }
        }
        #endregion
    }
}
