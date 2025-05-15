using System;
using System.IO;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

namespace HatTrick.InMemDb.TestHarness
{
    public class ArchiveTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "archive");
        #endregion

        #region ctors
        public ArchiveTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            this.RegisterMemDb(_dbPath);
        }
        #endregion

        #region register memdb
        private void RegisterMemDb(string path)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, path)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .ArchiveOnDefrag(Path.Combine(_dbPath, "_bak"))
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

                db.Insert(asset, (id) => asset.Id = id);
            }
        }
        #endregion

        #region defrag with archive
        public void Test_DefragWithArchive()
        {
            Stats resolve = Stats.FreshCount | Stats.StaleCount | Stats.DeletedCount | Stats.FreshSize | Stats.StaleSize | Stats.DeletedSize;
            MemDbStatistics stats = null;

            //int txtSize = 104;
            //int jsonSize = 105;
            //int unknownSize = 100;

            int txtCnt;
            int jsonCnt;
            int unknownCnt;

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt);
                db.Flush();

                //update all the txt assets
                db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Text);
                //delete all the extensionless assets
                db.Delete(a => a.AssetType == DigitalAssetType.Unknown);
                db.Flush();

                stats = db.ResolveStatistics(resolve);
            }

            Assert.IsEqual(stats.FreshCount, (txtCnt + jsonCnt));//we deleted the unknown assets
            Assert.IsEqual(stats.StaleCount, txtCnt);//we updated all the txt assets leaving all the original txt assets marked stale
            Assert.IsEqual(stats.DeletedCount, unknownCnt);//we deleted all the unknown assets marking all as deleted

            MemDb.Defrag(_dataset);

            MemDbStatistics stats2 = null;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                stats2 = db.ResolveStatistics(resolve);
            }

            Assert.IsEqual(stats2.FreshCount, (txtCnt + jsonCnt));
            Assert.IsEqual(stats2.StaleCount, 0);
            Assert.IsEqual(stats2.DeletedCount, 0);

            List<MemDbArchivedRecord<DigitalAsset>> archives = new List<MemDbArchivedRecord<DigitalAsset>>();
            foreach (var rec in MemDb.ReadArchive<DigitalAsset>(_dataset))
            {
                archives.Add(rec);
            }

            //we updated txt recs and deleted unknowns
            Assert.IsEqual(archives.Count, (txtCnt + unknownCnt));
            Assert.IsEqual(archives.Count(a => a.State == RecordState.Stale), txtCnt);
            Assert.IsEqual(archives.Count(a => a.State == RecordState.Deleted), unknownCnt);
        }
        #endregion

        #region multi defrag with archive
        public void Test_MultiDefragWithArchive()
        {
            int txtCnt;
            int jsonCnt;
            int unknownCnt;

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt);
            }

            long timestamp = DateTime.UtcNow.ToBinary();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Text);
                db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Unknown);
            }

            MemDb.Defrag(_dataset);

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Json);
                db.Delete(a => a.AssetType == DigitalAssetType.Unknown);
            }

            MemDb.Defrag(_dataset);

            var archives = new List<MemDbArchivedRecord<DigitalAsset>>(txtCnt + unknownCnt + jsonCnt + unknownCnt);
            foreach (var rec in MemDb.ReadArchive<DigitalAsset>(_dataset))
            {
                archives.Add(rec);
            }

            //we should have txtCnt + unknownCnt + jsonCnt + unknownCnt total archive records
            Assert.IsEqual(archives.Count, txtCnt + unknownCnt + jsonCnt + unknownCnt);

            //we should have txtCnt + unknownCnt + jsonCnt stale records
            Assert.IsEqual(archives.Count(a => a.State == RecordState.Stale), txtCnt + unknownCnt + jsonCnt);

            //we should have unknownCnt deleted records
            Assert.IsEqual(archives.Count(a => a.State == RecordState.Deleted), unknownCnt);
        }
        #endregion

        #region multi defrag archive restore to timestamp
        public void Test_MultiDefragArchiveRestoreToTimestamp()
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

        #region multi defrag archive restore to timestamp 2
        public void Test_MultiDefragArchiveRestoreToTimestamp2()
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

        #region multi defrag archive restore to timestamp 3
        public void Test_MultiDefragArchiveRestoreToTimestamp3()
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

        #region cleanup
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
