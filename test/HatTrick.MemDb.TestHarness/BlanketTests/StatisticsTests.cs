using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb.TestHarness
{
    public class StatisticsTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = @"..\..\..\..\_db\statistics";
        #endregion

        #region ctors
        public StatisticsTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .CloneWith(() => new DigitalAssetCloner())
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

        #region count
        public void Test_CountStatistic()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            db.Flush();

            var stats1 = db.ResolveStatistics(Stats.FreshCount | Stats.StaleCount | Stats.DeletedCount);
            Assert.IsEqual(stats1.FreshCount, db.Count());
            Assert.IsEqual(stats1.StaleCount, 0);
            Assert.IsEqual(stats1.DeletedCount, 0);

            db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Json);
            db.Flush();
            var stats2 = db.ResolveStatistics(Stats.FreshCount | Stats.StaleCount | Stats.DeletedCount);
            Assert.IsEqual(stats2.FreshCount, db.Count());
            Assert.IsEqual(stats2.StaleCount, jsonCnt);
            Assert.IsEqual(stats2.DeletedCount, 0);

            db.Delete(a => a.AssetType == DigitalAssetType.Unknown);
            db.Flush();
            var stats3 = db.ResolveStatistics(Stats.FreshCount | Stats.StaleCount | Stats.DeletedCount);
            Assert.IsEqual(stats3.FreshCount, txtCnt + jsonCnt);
            Assert.IsEqual(stats3.StaleCount, jsonCnt);
            Assert.IsEqual(stats3.DeletedCount, unknownCnt);
        }
        #endregion

        #region size
        public void Test_SizeStatistic()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //we know the exact serialized size of each of the records...only diff is the file extensions ('.txt', '.json', '')
            int txtSize = 104;
            int jsonSize = 105;
            int unknownSize = 100;

            db.Flush();
            var stats1 = db.ResolveStatistics(Stats.FreshSize | Stats.StaleSize | Stats.DeletedSize);
            Assert.IsEqual(stats1.FreshSize, (txtSize * txtCnt) + (jsonSize * jsonCnt) + (unknownSize * unknownCnt));
            Assert.IsEqual(stats1.StaleSize, 0);
            Assert.IsEqual(stats1.DeletedSize, 0);

            db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Json);
            db.Flush();
            var stats2 = db.ResolveStatistics(Stats.FreshSize | Stats.StaleSize | Stats.DeletedSize);
            Assert.IsEqual(stats2.FreshSize, (txtSize * txtCnt) + (jsonSize * jsonCnt) + (unknownSize * unknownCnt));
            Assert.IsEqual(stats2.StaleSize, jsonSize * jsonCnt);
            Assert.IsEqual(stats2.DeletedSize, 0);

            db.Delete(a => a.AssetType == DigitalAssetType.Unknown);
            db.Flush();
            var stats3 = db.ResolveStatistics(Stats.FreshSize | Stats.StaleSize | Stats.DeletedSize);
            Assert.IsEqual(stats3.FreshSize, (txtSize * txtCnt) + (jsonSize * jsonCnt));
            Assert.IsEqual(stats3.StaleSize, (jsonSize * jsonCnt));
            Assert.IsEqual(stats3.DeletedSize, (unknownSize * unknownCnt));
        }
        #endregion

        #region max
        public void Test_MaxStatistic()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //we know the exact serialized size of each of the records...only diff is the file extensions ('.txt', '.json', '')
            int txtSize = 104;
            int jsonSize = 105;
            int unknownSize = 100;

            db.Flush();
            var stats1 = db.ResolveStatistics(Stats.MaxFreshSize | Stats.MaxStaleSize| Stats.MaxDeletedSize);
            Assert.IsEqual(stats1.MaxFreshSize, jsonSize);
            Assert.IsEqual(stats1.MaxStaleSize, 0);
            Assert.IsEqual(stats1.MaxDeletedSize, 0);

            db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Json);
            db.Flush();
            var stats2 = db.ResolveStatistics(Stats.MaxFreshSize | Stats.MaxStaleSize | Stats.MaxDeletedSize);
            Assert.IsEqual(stats2.MaxFreshSize, jsonSize);
            Assert.IsEqual(stats2.MaxStaleSize, jsonSize);
            Assert.IsEqual(stats2.MaxDeletedSize, 0);

            db.Delete(a => a.AssetType == DigitalAssetType.Json);
            db.Flush();
            var stats3 = db.ResolveStatistics(Stats.MaxFreshSize | Stats.MaxStaleSize | Stats.MaxDeletedSize);
            Assert.IsEqual(stats3.MaxFreshSize, txtSize);
            Assert.IsEqual(stats3.MaxStaleSize, jsonSize);
            Assert.IsEqual(stats3.MaxDeletedSize, jsonSize);
        }
        #endregion

        #region min
        public void Test_MinStatistic()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //we know the exact serialized size of each of the records...only diff is the file extensions ('.txt', '.json', '')
            int txtSize = 104;
            int jsonSize = 105;
            int unknownSize = 100;

            db.Flush();
            var stats1 = db.ResolveStatistics(Stats.MinFreshSize | Stats.MinStaleSize | Stats.MinDeletedSize);
            Assert.IsEqual(stats1.MinFreshSize, unknownSize);
            Assert.IsEqual(stats1.MinStaleSize, 0);
            Assert.IsEqual(stats1.MinDeletedSize, 0);

            db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Unknown);
            db.Flush();
            var stats2 = db.ResolveStatistics(Stats.MinFreshSize | Stats.MinStaleSize | Stats.MinDeletedSize);
            Assert.IsEqual(stats2.MinFreshSize, unknownSize);
            Assert.IsEqual(stats2.MinStaleSize, unknownSize);
            Assert.IsEqual(stats2.MinDeletedSize, 0);

            db.Delete(a => a.AssetType == DigitalAssetType.Unknown);
            db.Flush();
            var stats3 = db.ResolveStatistics(Stats.MinFreshSize | Stats.MinStaleSize | Stats.MinDeletedSize);
            Assert.IsEqual(stats3.MinFreshSize, txtSize);
            Assert.IsEqual(stats3.MinStaleSize, unknownSize);
            Assert.IsEqual(stats3.MinDeletedSize, unknownSize);
        }
        #endregion

        #region avg
        public void Test_AvgStatistic()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //we know the exact serialized size of each of the records...only diff is the file extensions ('.txt', '.json', '')
            double txtSize = 104;
            double jsonSize = 105;
            double unknownSize = 100;
            double txtWeight = txtSize * .5; //there are 500 txt files (50%)
            double jsonWeight = jsonSize * .4; //there are 400 json files (40 %)
            double unknownWeight = unknownSize * .1; //there are 100 extensionless files (10 %)


            db.Flush();
            var stats1 = db.ResolveStatistics(Stats.AvgFreshSize | Stats.AvgStaleSize | Stats.AvgDeletedSize);
            Assert.IsEqual(stats1.AvgFreshSize, txtWeight + jsonWeight + unknownWeight);
            Assert.IsEqual(stats1.AvgStaleSize, 0);
            Assert.IsEqual(stats1.AvgDeletedSize, 0);

            db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Unknown);
            db.Flush();
            var stats2 = db.ResolveStatistics(Stats.AvgFreshSize | Stats.AvgStaleSize | Stats.AvgDeletedSize);
            Assert.IsEqual(stats2.AvgFreshSize, txtWeight + jsonWeight + unknownWeight);
            Assert.IsEqual(stats2.AvgStaleSize, unknownSize);
            Assert.IsEqual(stats2.AvgDeletedSize, 0);

            db.Delete(a => a.AssetType == DigitalAssetType.Text);
            db.Flush();
            //we deleted the 500 txt assets leaving 400 json and 100 unknowns...calc new wieghts for the remaining fresh
            double txtWeight2 = 0;
            double jsonWeight2 = jsonSize * ((double)jsonCnt / ((double)jsonCnt + (double)unknownCnt));
            double unknownWeight2 = unknownSize * ((double)unknownSize / ((double)jsonCnt + (double)unknownCnt));
            var stats3 = db.ResolveStatistics(Stats.AvgFreshSize | Stats.AvgStaleSize | Stats.AvgDeletedSize);
            Assert.IsEqual(stats3.AvgFreshSize, txtWeight2 + jsonWeight2 + unknownWeight2);
            Assert.IsEqual(stats3.AvgStaleSize, unknownSize);
            Assert.IsEqual(stats3.AvgDeletedSize, txtSize);
        }
        #endregion

        #region last id
        public void Test_LastIdStatistic()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            uint total = (uint)(txtCnt + jsonCnt + unknownCnt);

            db.Flush();
            var stats1 = db.ResolveStatistics(Stats.LastId);
            Assert.IsEqual<uint>((uint)stats1.LastId, total);

            var assets = base.ResolveAssetSet();

            db.Insert(assets[0]);
            db.Insert(assets[1]);
            db.Insert(assets[2]);
            db.Insert(assets[3]);
            db.Insert(assets[4], (id) => assets[4].Id = id);

            var stats2 = db.ResolveStatistics(Stats.LastId);
            Assert.IsEqual<uint>((uint)stats2.LastId, assets[4].Id);
        }
        #endregion
    }
}
