using System;
using System.IO;

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
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
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

                db.Insert(asset);
            }
        }
        #endregion

        #region defrag
        public void Test_Defrag()
        {
            Stats resolve = Stats.FreshCount | Stats.StaleCount | Stats.DeletedCount | Stats.FreshSize | Stats.StaleSize | Stats.DeletedSize;
            MemDbStatistics stats = null;

            int txtSize = 104;
            int jsonSize = 105;
            int unknownSize = 100;

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

            MemDb.Defrag(_dataset);

            MemDbStatistics stats2 = null;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                stats2 = db.ResolveStatistics(resolve);
            }
        }
        #endregion

        #region cleanup
        public override void Cleanup()
        {
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
        }
        #endregion
    }
}
