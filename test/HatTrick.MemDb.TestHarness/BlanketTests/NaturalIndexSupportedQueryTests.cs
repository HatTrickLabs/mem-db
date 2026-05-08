using System;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace HatTrick.Data.TestHarness
{
    public class IdentityIndexSupportedQueryTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "identity_index");
        #endregion

        #region ctors
        public IdentityIndexSupportedQueryTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .EncryptWithPassword(() => "XXXYYYZZZAAABBBCCCDDDEEEFFFGGGHHHIIIJJJKKKLLLMMMNNN")
                .SetFlushInterval(1)
                .IndexOnIdentity(true)
                .Register();
        }
        #endregion

        #region load db
        private void LoadDb(MemDb<DigitalAsset> db, out int count)
        {
            var assets = base.ResolveAssetSet();
            for (int i = 0; i < assets.Length; i++)
            {
                db.Insert(assets[i], (id) => assets[i].Id = id, i % 2 == 0);
            }
            count = assets.Length;
        }
        #endregion

        #region exists
        public void Test_Exists()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);
                for (int i = 0; i < count; i++)
                {
                    Assert.IsEqual(db.Exists((long)i + 1), true);
                }

                Assert.IsEqual(db.Exists((long)++count), false);
            }
        }
        #endregion

        #region find
        public void Test_Find()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);
                for (int i = 0; i < count; i++)
                {
                    Assert.IsNotNull(db.Find((long)i + 1));
                }

                Assert.IsNull(db.Find((long)++count));
            }
        }
        #endregion

        #region find all
        public void Test_FindAll()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);
                var set = db.FindAll(db.Query().Select(a => a.Id).ToArray());
                Assert.IsEqual(set.Length, count);

                var set2 = db.FindAll(100, 200, 300, 400, 500);
                Assert.IsEqual(set2.Length, 5);
                Assert.IsEqual(set2[0].Id, (long)100);
                Assert.IsEqual(set2[1].Id, (long)200);
                Assert.IsEqual(set2[2].Id, (long)300);
                Assert.IsEqual(set2[3].Id, (long)400);
                Assert.IsEqual(set2[4].Id, (long)500);
            }
        }
        #endregion

        #region single update index still hits
        public void Test_SingleUpdateIndexStillHits()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);

                db.Update(a => a.XXHash += 1, a => a.Id == 100);

                var asset = db.Find(100);

                Assert.IsNotNull(asset);
                Assert.IsEqual(asset.XXHash, (ulong)1);
            }
        }
        #endregion

        #region batch updates index still hits
        public void Test_BatchUpdateIndexStillHits()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);

                db.Update(a => a.XXHash += 1, a => a.Id >= 100);

                var asset = db.Find(100);

                Assert.IsNotNull(asset);
                Assert.IsEqual(asset.XXHash, (ulong)1);
            }
        }
        #endregion

        #region single delete index still hits
        public void Test_SingleDeleteIndexStillHits()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);

                db.Delete(a => a.Id == 100);
                var asset100 = db.Find(100);
                var asset101 = db.Find(a => a.Id == 101);

                Assert.IsNull(asset100);
                Assert.IsNotNull(asset101);
            }
        }
        #endregion

        #region batch delete index still hits
        public void Test_BatchDeleteIndexStillHits()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);

                db.Delete(a => a.Id >= 100);
                var asset100 = db.Find(100);
                var asset101 = db.Find(a => a.Id == 101);
                var asset800 = db.Find(a => a.Id == 800);
                var asset50 = db.Find(50);

                Assert.IsNull(asset100);
                Assert.IsNull(asset101);
                Assert.IsNull(asset800);
                Assert.IsNotNull(asset50);
            }
        }
        #endregion

        #region multi batch update index still hits
        public void Test_MultiBatchUpdateIndexStillHits()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);

                //update ALL assets xet xxhash = 1
                db.Update(a => a.XXHash += 1, a => true);

                //update ALL assets again, set xxhash = xxhash + i
                var all = db.FindAll(a => a.Id > 0);
                for (int i = 0; i < all.Length; i++)
                {
                    db.Update(a => a.XXHash += (ulong)i, i + 1);
                }

                for (int i = 0; i < all.Length; i++)
                {
                    var asset = db.Find(i + 1);//find by id index...
                    Assert.IsEqual(asset.XXHash, (ulong)(i + 1));
                }
            }
        }
        #endregion

        #region multi batch update plus purge index still hits
        public void Test_MultiBatchUpdatePlusPurgeIndexStillHits()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);

                //update ALL assets xet xxhash = 1
                db.Update(a => a.XXHash += 1, a => true);

                //update ALL assets again, set xxhash = xxhash + i
                var all = db.FindAll(a => a.Id > 0);
                for (int i = 0; i < all.Length; i++)
                {
                    db.Update(a => a.XXHash += (ulong)i, i + 1);
                }

                db.PurgeCache();

                for (int i = 0; i < all.Length; i++)
                {
                    var asset = db.Find(i + 1);//find by id index...
                    Assert.IsEqual(asset.XXHash, (ulong)(i + 1));
                }
            }
        }
        #endregion

        #region mutli batch update and multi batch delete index still hits
        public void Test_MultiBatchUpdateAndMultiBatchDeleteIndexStillHits()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);

                int initialCount = db.Count();

                //update ALL assets xet xxhash = 1
                db.Update(a => a.XXHash += 1, a => true);

                //update ALL assets again, set xxhash = xxhash + i
                var all = db.FindAll(a => a.Id > 0);
                for (int i = 0; i < all.Length; i++)
                {
                    db.Update(a => a.XXHash += (ulong)i, i + 1);
                }

                //delete ids 100 to 199
                db.Delete(a => a.Id >= 100 && a.Id < 200);


                //delete ids 300 to 399
                for (int i = 300; i < 400; i++)
                {
                    db.Delete(i);
                }

                //refresh all...
                all = db.FindAll(a => true);
                Assert.IsEqual(all.Length, (initialCount - 200));
                for (int i = 0; i < all.Length; i++)
                {
                    int id = i + 1;
                    var asset = db.Find(i + 1);//find by id index...
                    if (asset is null)
                        Assert.IsEqual(true, (id >= 100 && id < 200) || (id >= 300 && id < 400));
                    else
                        Assert.IsEqual(asset.XXHash, (ulong)(i + 1));
                }
            }
        }
        #endregion

        #region mutli batch update and multi batch delete plus purge index still hits
        public void Test_MultiBatchUpdateAndMultiBatchDeletePlusPurgeIndexStillHits()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);

                //update ALL assets xet xxhash = 1
                db.Update(a => a.XXHash += 1, a => true);

                //update ALL assets again, set xxhash = xxhash + i
                var all = db.FindAll(a => a.Id > 0);
                for (int i = 0; i < all.Length; i++)
                {
                    db.Update(a => a.XXHash += (ulong)i, i + 1);
                }

                //delete ids 100 to 199
                db.Delete(a => a.Id >= 100 && a.Id < 200);

                db.PurgeCache();

                //delete ids 300 to 399
                for (int i = 300; i < 400; i++)
                {
                    db.Delete(i);
                }

                db.PurgeCache();

                //refresh all...
                all = db.FindAll(a => true);
                Assert.IsEqual(all.Length, (count - 200));
                for (int i = 0; i < all.Length; i++)
                {
                    int id = i + 1;
                    var asset = db.Find(i + 1);//find by id index...
                    if (asset is null)
                        Assert.IsEqual(true, (id >= 100 && id < 200) || (id >= 300 && id < 400));
                    else
                        Assert.IsEqual(asset.XXHash, (ulong)(i + 1));
                }
            }
        }
        #endregion

        #region updates against identity index hit
        public void Test_UpdatesAgainstIdentityIndexHit()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);

                for (int i = 100; i < 500; i++)
                {
                    db.Update(a => a.XXHash += 1, i);
                }
                db.Flush();
                for (int i = 1; i <= count; i++)
                {
                    if (i >= 100 && i < 500)
                        Assert.IsEqual(db.Find(i).XXHash, (ulong)1);
                    else
                        Assert.IsEqual(db.Find(i).XXHash, (ulong)0);
                }
            }
        }
        #endregion

        #region deletes against identity index hit
        public void Test_DeletessAgainstIdentityIndexHit()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int count);

                for (int i = 100; i < 500; i++)
                {
                    db.Delete(i);
                }
                db.Flush();
                for (int i = 1; i <= count; i++)
                {
                    if (i >= 100 && i < 500)
                        Assert.IsNull(db.Find(i));
                    else
                        Assert.IsEqual(db.Find(i).XXHash, (ulong)0);
                }
            }
        }
        #endregion
    }
}
