using System;
using System.IO;
using System.Threading;

namespace HatTrick.InMemDb.TestHarness
{
    public class PointInTimeRestoreTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "archive");
        #endregion

        #region ctors
        public PointInTimeRestoreTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            this.RegisterMemDb(_dbPath);
        }
        #endregion

        #region register memdb
        private void RegisterMemDb(string dbPath)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, dbPath)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .ArchiveOnDefrag(Path.Combine(_dbPath, "_bak"))
                .EncryptWithPassword(() => "This is my test encryption password.")
                .Register();
        }
        #endregion

        #region load db
        protected void LoadDb(MemDb<DigitalAsset> db, out int txtCnt, out int jsonCnt, out int unknownCnt)
        {
            txtCnt = 0;
            jsonCnt = 0;
            unknownCnt = 0;
            DigitalAsset[] assets = base.ResolveAssetSet();
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                if (asset.AssetType == DigitalAssetType.Text)
                    txtCnt += 1;

                if (asset.AssetType == DigitalAssetType.Json)
                    jsonCnt += 1;

                if (asset.AssetType == DigitalAssetType.Unknown)
                    unknownCnt += 1;

                db.Insert(asset, (id) => asset.Id = id, true);
            }
        }
        #endregion

        #region restore to timestamp no archive
        public void Test_RestoreToTimestampNoArchive()
        {
            long timestamp;
            int txtCnt;
            int jsonCnt;
            int unknownCnt;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt);
                timestamp = DateTime.UtcNow.ToBinary();

                Thread.Sleep(5);

                db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Text);
                db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Json);
                db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Unknown);

                db.Delete(a => a.AssetType == DigitalAssetType.Unknown);
                db.Delete(a => a.Id < 10);
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out _, out _, out _);

                db.Update(a => a.XXHash = 3, a => a.AssetType == DigitalAssetType.Unknown);
                db.Delete(a => a.AssetType == DigitalAssetType.Json);
            }

            MemDb.Restore(_dataset, timestamp, Path.Combine(_dbPath, "_restore"));
            MemDb.RemoveConfiguationFor(_dataset);
            this.RegisterMemDb(Path.Combine(_dbPath, "_restore"));

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Assert.IsEqual(db.Count(), (txtCnt + jsonCnt + unknownCnt));
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Text), txtCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Json), jsonCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Unknown), unknownCnt);

                
                long[] ids = db.Query().SelectDistinct(a => a.Id);
                Assert.IsEqual(ids.Length, (txtCnt + jsonCnt + unknownCnt));
                Array.Sort(ids);

                for (int i = 0; i < ids.Length; i++)
                {
                    Assert.IsEqual(ids[i], i + 1);
                }
            }
        }
        #endregion

        #region defrag archive restore to timestamp
        public void Test_DefragArchiveRestoreToTimestamp()
        {
            int txtCnt;
            int jsonCnt;
            int unknownCnt;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt);
            }

            long timestamp = 0;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                long[] ids = db.Query().SelectDistinct(a => a.Id);

                for (int i = 0; i < ids.Length; i++)
                {
                    db.Update(a => a.XXHash += 1, a => a.Id == ids[i]);
                    if (i == 249)
                    {
                        Thread.Sleep(1);
                        timestamp = DateTime.UtcNow.ToBinary();
                    }
                }

                for (int i = 0; i < ids.Length; i++)
                {
                    db.Delete(a => a.Id == ids[i]);
                }
            }
            MemDb.Defrag(_dataset);

            MemDb.Restore(_dataset, timestamp, Path.Combine(_dbPath, "_restore"));
            MemDb.RemoveConfiguationFor(_dataset);
            this.RegisterMemDb(Path.Combine(_dbPath, "_restore"));

            int total = (txtCnt + jsonCnt + unknownCnt);
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Assert.IsEqual(db.Count(), total);
                Assert.IsEqual(db.Count(a => a.XXHash == 1), 250);
                Assert.IsEqual(db.Count(a => a.XXHash != 1), (total - 250));
            }
        }
        #endregion

        #region multi defrag archive restore to timestamp 1
        public void Test_MultiDefragArchiveRestoreToTimestamp1()
        {
            int txtCnt;
            int jsonCnt;
            int unknownCnt;

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt);
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Text);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Unknown);
            }
            MemDb.Defrag(_dataset);

            Thread.Sleep(1);
            long timestamp = DateTime.UtcNow.ToBinary();
            Thread.Sleep(1);

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Json);
                db.Delete(a => a.AssetType == DigitalAssetType.Unknown);
            }
            MemDb.Defrag(_dataset);

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Text);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Json);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Unknown);
            }

            MemDb.Restore(_dataset, timestamp, Path.Combine(_dbPath, "_restore"));

            //remove the orig config in order to open the restored db
            MemDb.RemoveConfiguationFor(_dataset);
            this.RegisterMemDb(Path.Combine(_dbPath, "_restore"));

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Assert.IsEqual(db.Count(), (txtCnt + jsonCnt + unknownCnt));
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Text), txtCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Json), jsonCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Unknown), unknownCnt);
                Assert.IsEqual(db.Count(a => a.XXHash == 1), (txtCnt + unknownCnt));
                Assert.IsEqual(db.Count(a => a.XXHash == 0), jsonCnt);
            }
        }
        #endregion

        #region multi defrag archive restore to timestamp 2
        public void Test_MultiDefragArchiveRestoreToTimestamp2()
        {
            int txtCnt;
            int jsonCnt;
            int unknownCnt;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Json);
                db.Delete(a => a.AssetType == DigitalAssetType.Unknown);
            }
            MemDb.Defrag(_dataset);

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt);
                int uj = db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Json);
                int uu = db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Unknown);
                int ut = db.Update(a => a.XXHash += 10, a => a.AssetType == DigitalAssetType.Text);
            }
            MemDb.Defrag(_dataset);

            long timestamp = DateTime.UtcNow.ToBinary();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Assert.IsEqual(db.Count(), ((txtCnt + jsonCnt + unknownCnt) * 2) - unknownCnt);
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Json);
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Unknown);
                db.Update(a => a.XXHash -= 1, a => a.AssetType == DigitalAssetType.Text);
            }
            MemDb.Defrag(_dataset);

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt);
                db.Update(a => a.XXHash += 5, a => true);
                db.Delete(a => true);
            }

            MemDb.Restore(_dataset, timestamp, Path.Combine(_dbPath, "_restore"));

            MemDb.RemoveConfiguationFor(_dataset);
            this.RegisterMemDb(Path.Combine(_dbPath, "_restore"));

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Assert.IsEqual(db.Count(), ((txtCnt + jsonCnt + unknownCnt) * 2) - unknownCnt);
                Assert.IsEqual(db.Count(a => a.XXHash == 2), jsonCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Unknown), unknownCnt);
                Assert.IsEqual(db.Count(a => a.XXHash == 10), (txtCnt * 2));
            }
        }
        #endregion

        #region cleanup [override]
        public override void Cleanup()
        {
            MemDb.RemoveConfiguationFor(_dataset);
            base.Cleanup();
            string bakPath = Path.Combine(_dbPath, "_bak");
            if (Directory.Exists(bakPath))
            {
                string[] files = Directory.GetFiles(bakPath);
                for (int i = 0; i < files.Length; i++)
                {
                    File.Delete(files[i]);
                }
            }
            string restorePath = Path.Combine(_dbPath, "_restore");
            if (Directory.Exists(restorePath))
            {
                string[] files = Directory.GetFiles(restorePath);
                for (int i = 0; i < files.Length; i++)
                {
                    File.Delete(files[i]);
                }
            }
            this.RegisterMemDb(_dbPath);
        }
        #endregion
    }
}