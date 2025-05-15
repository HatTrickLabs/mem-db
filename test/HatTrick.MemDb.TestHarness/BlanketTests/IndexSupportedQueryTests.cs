using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HatTrick.InMemDb.TestHarness
{
    public class IndexSupportedQueryTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "indexed");
        #endregion

        #region ctors
        public IndexSupportedQueryTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .EncryptWithPassword(() => "XXXYYYZZZAAABBBCCCDDDEEEFFFGGGHHHIIIJJJKKKLLLMMMNNN")
                .SetFlushInterval(2)
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

        #region single delete index still hits
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
    }
}
