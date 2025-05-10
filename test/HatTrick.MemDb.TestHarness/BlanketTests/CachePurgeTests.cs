using System;
using System.IO;
using System.Threading;

namespace HatTrick.InMemDb.TestHarness
{
    public class CachePurgeTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "cache_purge");
        #endregion

        #region ctors
        public CachePurgeTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .Register();
        }
        #endregion

        #region load db
        private void LoadDb(MemDb<DigitalAsset> db, out int txtCnt, out int jsonCnt, out int unknownCnt)
        {
            txtCnt = 0;
            jsonCnt = 0;
            unknownCnt = 0;

            var assets = base.ResolveAssetSet();
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                db.Insert(asset, (id) => asset.Id = id);
                if (asset.AssetType == DigitalAssetType.Text)
                    txtCnt += 1;

                else if (asset.AssetType == DigitalAssetType.Json)
                    jsonCnt += 1;

                else if (asset.AssetType == DigitalAssetType.Unknown)
                    unknownCnt += 1;
            }
        }
        #endregion

        #region simple purge
        public void Test_SimplePurge()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

                //should generate unknownCnt deleted records...
                db.Delete(a => a.AssetType == DigitalAssetType.Unknown);
                //should generate txtCnt stale records...
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Text);

                var purged = db.PurgeCache();

                Assert.IsEqual(purged.deleted, unknownCnt);
                Assert.IsEqual(purged.stale, txtCnt);
            }
        }
        #endregion

        #region purge all
        public void Test_PurgeAll()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Text);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Json);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Unknown);

                //delete everything.
                db.Delete(a => a.AssetType == DigitalAssetType.Text);
                db.Delete(a => a.AssetType == DigitalAssetType.Json);
                db.Delete(a => a.AssetType == DigitalAssetType.Unknown);

                (int stale, int deleted) purged = db.PurgeCache();

                int total = (txtCnt + jsonCnt + unknownCnt);
                Assert.IsEqual(purged.stale, total);
                Assert.IsEqual(purged.deleted, total);

                Assert.IsEqual(db.Count(), 0);
            }
        }
        #endregion

        #region purge all close reopen
        public void Test_PurgeAllCloseReopen()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Text);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Json);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Unknown);

                //delete everything.
                db.Delete(a => a.AssetType == DigitalAssetType.Text);
                db.Delete(a => a.AssetType == DigitalAssetType.Json);
                db.Delete(a => a.AssetType == DigitalAssetType.Unknown);

                (int stale, int deleted) purged = db.PurgeCache();

                int total = (txtCnt + jsonCnt + unknownCnt);
                Assert.IsEqual(purged.stale, total);
                Assert.IsEqual(purged.deleted, total);

                Assert.IsEqual(db.Count(), 0);
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Assert.IsEqual(db.Count(), 0);
                this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

                Assert.IsEqual(db.Count(), (txtCnt + jsonCnt + unknownCnt));

                (int stale, int deleted) purged = db.PurgeCache();

                Assert.IsEqual(purged.stale, 0);
                Assert.IsEqual(purged.deleted, 0);

                //delete 1;=
                int deleted = db.Delete(a => a.Id == db.Query().Max(a => a.Id));
                Assert.IsEqual(deleted, 1);

                purged = db.PurgeCache();

                Assert.IsEqual(purged.stale, 0);
                Assert.IsEqual(purged.deleted, deleted);
            }
        }
        #endregion

        #region purge all close defrag reopen
        public void Test_PurgeAllCloseDefragReopen()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Text);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Json);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Unknown);

                //delete everything.
                db.Delete(a => a.AssetType == DigitalAssetType.Text);
                db.Delete(a => a.AssetType == DigitalAssetType.Json);
                db.Delete(a => a.AssetType == DigitalAssetType.Unknown);

                (int stale, int deleted) purged = db.PurgeCache();

                int total = (txtCnt + jsonCnt + unknownCnt);
                Assert.IsEqual(purged.stale, total);
                Assert.IsEqual(purged.deleted, total);

                Assert.IsEqual(db.Count(), 0);
            }

            MemDb.Defrag(_dataset);

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Assert.IsEqual(db.Count(), 0);
                this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

                Assert.IsEqual(db.Count(), (txtCnt + jsonCnt + unknownCnt));

                (int stale, int deleted) purged = db.PurgeCache();

                Assert.IsEqual(purged.stale, 0);
                Assert.IsEqual(purged.deleted, 0);

                //delete 1;=
                int deleted = db.Delete(a => a.Id == db.Query().Max(a => a.Id));
                Assert.IsEqual(deleted, 1);

                purged = db.PurgeCache();

                Assert.IsEqual(purged.stale, 0);
                Assert.IsEqual(purged.deleted, deleted);
            }
        }
        #endregion

        #region concurrent pressure purge
        public void Test_ConcurrentInsertPressurePurge()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);
                this.LoadDb(db, out _, out _, out _);
                this.LoadDb(db, out _, out _, out _);
                this.LoadDb(db, out _, out _, out _);
                this.LoadDb(db, out _, out _, out _);

                db.Delete(a => a.AssetType == DigitalAssetType.Unknown);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Json);

                Thread t1 = new Thread(() => this.LoadDb(db, out _, out _, out _));
                Thread t2 = new Thread(() => this.LoadDb(db, out _, out _, out _));
                Thread t3 = new Thread(() => this.LoadDb(db, out _, out _, out _));

                t1.Start();
                t2.Start();
                (int stale, int deleted) purged = db.PurgeCache();
                t3.Start();

                t1.Join();
                t2.Join();
                t3.Join();

                Assert.IsEqual(purged.stale, jsonCnt * 5);
                Assert.IsEqual(purged.deleted, unknownCnt * 5);

                //should be 8 sets of txt, 8 sets of json, 3 sets of unknown remaining within the db...
                Assert.IsEqual(db.Count(), (txtCnt * 8) + (jsonCnt * 8) + (unknownCnt * 3));
            }
        }
        #endregion
    }
}
