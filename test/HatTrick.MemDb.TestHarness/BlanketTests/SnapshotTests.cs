using System;
using System.IO;
using System.Threading;

namespace HatTrick.InMemDb.TestHarness
{
    public class SnapshotTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "snapshot");
        #endregion

        #region ctors
        public SnapshotTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .EncryptWithPassword(() => "This is a super complicated and secret password here!")
                .SnapshotTo(Path.Combine(TestBase.DbBasePath, "snapshot", "target"))
                .Register();
        }
        #endregion

        #region load db
        private void LoadDb(MemDb<DigitalAsset> db, out int txtCnt, out int jsonCnt, out int unknownCnt, bool intermingleEncrypted)
        {
            txtCnt = 0;
            jsonCnt = 0;
            unknownCnt = 0;

            var assets = base.ResolveAssetSet();
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                bool encrypt = intermingleEncrypted && (i % 2) == 0;
                db.Insert(asset, (id) => asset.Id = id, encrypt);
                if (asset.AssetType == DigitalAssetType.Text)
                    txtCnt += 1;

                else if (asset.AssetType == DigitalAssetType.Json)
                    jsonCnt += 1;

                else if (asset.AssetType == DigitalAssetType.Unknown)
                    unknownCnt += 1;
            }
        }
        #endregion

        #region throws on non persisted database configuration
        public void Test_ThrowsOnNonPersistedDatabaseConfiguration()
        {
            Assert.Throws<InvalidOperationException>(
                when: () => {
                    MemDb.ConfigureFor<DigitalAsset>(_dataset)
                        .SnapshotTo(Path.Combine(TestBase.DbBasePath, "snapshot", "target"))
                        .Register();
                },
                messageContains: "Snapshot path is not applicable with a unpersisted database"
            );
        }
        #endregion

        #region throws on readonly access mode
        public void Test_ThrowsOnReadonlyAccessMode()
        {
            Assert.Throws<InvalidOperationException>(
                when: () => {
                    MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                        .SetMode(AccessMode.ReadOnly)
                        .SnapshotTo(Path.Combine(TestBase.DbBasePath, "snapshot", "target"))
                        .Register();
                },
                messageContains: "Snapshot path is not applicable when"
            );

            Assert.Throws<InvalidOperationException>(
                when: () =>
                {
                    MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                        .SnapshotTo(Path.Combine(TestBase.DbBasePath, "snapshot", "target"))
                        .SetMode(AccessMode.ReadOnly)
                        .Register();
                },
                messageContains: $"Cannot run in {nameof(AccessMode)}.{AccessMode.ReadOnly} mode when a snapshot path has been configured."
            );
        }
        #endregion

        #region simple snapshot
        public void Test_SimpleSnapshot()
        {
            DateTime snapshotTimestamp = default;
            int txtCnt, jsonCnt, unknownCnt;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt, false);
                snapshotTimestamp = db.Snapshot();
            }

            //configure and open the snapshot...
            string datasetName = MemDb.GetConfiguration("assets").GetSnapshotDatasetName(snapshotTimestamp);
            MemDb.ConfigureFor<DigitalAsset>(datasetName, Path.Combine(_dbPath, "target"))
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .Register();

            using (var db = MemDb.Open<DigitalAsset>(datasetName))
            {
                Assert.IsEqual(db.Count(), txtCnt + jsonCnt + unknownCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Text), txtCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Json), jsonCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Unknown), unknownCnt);
            }
        }
        #endregion

        #region encrypted records snapshot
        public void Test_EncryptedRecordSnapshot()
        {
            DateTime snapshotTimestamp = default;
            int txtCnt, jsonCnt, unknownCnt;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt, true);
                snapshotTimestamp = db.Snapshot();
            }

            //configure and open the snapshot...
            string datasetName = MemDb.GetConfiguration("assets").GetSnapshotDatasetName(snapshotTimestamp);
            MemDb.ConfigureFor<DigitalAsset>(datasetName, Path.Combine(TestBase.DbBasePath, "snapshot", "target"))
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .EncryptWithPassword(() => "This is a super complicated and secret password here!")
                .Register();

            using (var db = MemDb.Open<DigitalAsset>(datasetName))
            {
                Assert.IsEqual(db.Count(), txtCnt + jsonCnt + unknownCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Text), txtCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Json), jsonCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Unknown), unknownCnt);
            }
        }
        #endregion

        #region stale data snapshot
        public void Test_StaleDataSnapshot()
        {
            DateTime snapshotTimestamp = default;
            int txtCnt, jsonCnt, unknownCnt;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt, true);
                //update all json assets to generate stale data.
                db.Update(a => a.XXHash += 1, a => a.AssetType == DigitalAssetType.Json);
                //remove all the unknown assets to generate deleted/stale data.
                db.Delete(a => a.AssetType == DigitalAssetType.Unknown);
                snapshotTimestamp = db.Snapshot();
            }

            //configure and open the snapshot...
            string datasetName = MemDb.GetConfiguration("assets").GetSnapshotDatasetName(snapshotTimestamp);
            MemDb.ConfigureFor<DigitalAsset>(datasetName, Path.Combine(TestBase.DbBasePath, "snapshot", "target"))
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .EncryptWithPassword(() => "This is a super complicated and secret password here!")
                .Register();

            using (var db = MemDb.Open<DigitalAsset>(datasetName))
            {
                //unknowns were deleted prior to snapshot
                Assert.IsEqual(db.Count(), txtCnt + jsonCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Text), txtCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Json), jsonCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Json && a.XXHash == 1), jsonCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Unknown), 0);
            }
        }
        #endregion

        #region concurrent pressure snapshot
        public void Test_ConcurrentPressureSnapshot()
        {
            DateTime snapshotTimestamp = default;
            int txtCnt, jsonCnt, unknownCnt;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt, true);
            }

            int t1Count = 0;
            int t2Count = 0;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                int count = db.Count();
                Thread t1 = new Thread(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        t1Count += db.Update(a => a.XXHash += 1, a => a.Id == i + 1);
                    }
                });

                Thread t2 = new Thread(() =>
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        t2Count += db.Update(a => a.XXHash += 1, a => a.Id == i + 1);
                    }
                });

                t1.Start();
                t2.Start();
                snapshotTimestamp = db.Snapshot();
                t1.Join();
                t2.Join();
            }

            Assert.IsEqual(t1Count, txtCnt + jsonCnt + unknownCnt);
            Assert.IsEqual(t2Count, txtCnt + jsonCnt + unknownCnt);

            //configure and open the snapshot...
            string datasetName = MemDb.GetConfiguration("assets").GetSnapshotDatasetName(snapshotTimestamp);
            MemDb.ConfigureFor<DigitalAsset>(datasetName, Path.Combine(TestBase.DbBasePath, "snapshot", "target"))
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .EncryptWithPassword(() => "This is a super complicated and secret password here!")
                .Register();

            using (var db = MemDb.Open<DigitalAsset>(datasetName))
            {
                //unknowns were deleted prior to snapshot
                Assert.IsEqual(db.Count(), txtCnt + jsonCnt + unknownCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Text), txtCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Json), jsonCnt);
                Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Unknown), unknownCnt);
            }
        }
        #endregion

        #region cleanup
        public override void Cleanup()
        {
            base.Cleanup();
            string target = Path.Combine(TestBase.DbBasePath, "snapshot", "target");
            if (Directory.Exists(target))
            {
                string[] files = Directory.GetFiles(target);
                for (int i = 0; i < files.Length; i++)
                {
                    File.Delete(files[i]);
                }
            }
        }
        #endregion
    }
}
