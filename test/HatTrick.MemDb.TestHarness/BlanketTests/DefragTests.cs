using System;
using System.IO;

namespace HatTrick.InMemDb.TestHarness
{
    public class DefragTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "defrag");
        #endregion

        #region ctors
        public DefragTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
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

            //we know the constant size of each type of binary serialized asset record.
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

            Assert.IsEqual(stats.FreshCount, (txtCnt + jsonCnt));//we deleted the unknown assets
            Assert.IsEqual(stats.StaleCount, txtCnt);//we updated all the txt assets leaving all the original txt assets marked stale
            Assert.IsEqual(stats.DeletedCount, unknownCnt);//we deleted all the unknown assets marking all as deleted

            Assert.IsEqual(stats.FreshSize, ((txtCnt * txtSize) + (jsonCnt * jsonSize)));
            Assert.IsEqual(stats.StaleSize, (txtCnt * txtSize));
            Assert.IsEqual(stats.DeletedSize, (unknownCnt * unknownSize));

            //defrag (removes all deleted and stale data from the map and db files)
            MemDb.Defrag(_dataset);

            //when we re-open the db, all stale and deleted map pointers and db records should be gone.
            MemDbStatistics stats2 = null;            
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                stats2 = db.ResolveStatistics(resolve);
            }

            //all fresh data should be maintained
            Assert.IsEqual(stats2.FreshCount, (txtCnt + jsonCnt));
            Assert.IsEqual(stats.FreshSize, ((txtCnt * txtSize) + (jsonCnt * jsonSize)));

            //no stale data should exist
            Assert.IsEqual(stats2.StaleCount, 0);
            Assert.IsEqual(stats2.StaleSize, 0);

            //no deleted data should exist
            Assert.IsEqual(stats2.DeletedCount, 0);
            Assert.IsEqual(stats2.DeletedSize, 0);
        }
        #endregion
    }
}
