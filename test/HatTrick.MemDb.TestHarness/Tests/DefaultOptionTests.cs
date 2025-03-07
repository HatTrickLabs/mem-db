using System;
using System.IO;
using HatTrick.InMemDb;

namespace HatTrick.InMemDb.TestHarness
{
    public class DefaultOptionTests : BaseTests
    {
        #region internals
        private static readonly string _dbPath = @"..\..\..\..\_db";
        private static readonly string _dataset = $"config_assets";
        #endregion

        #region interface

        #endregion

        #region ctors
        public DefaultOptionTests(AssetResolver assetResolver) : base(_dbPath, _dataset, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath).Register();
        }
        #endregion

        #region default options
        public void Test_RecordCountNoFlush()
        {
            this.RecordCountTarget(false);
        }

        public void Test_RecordCountWithFlush()
        {
            this.RecordCountTarget(true);
        }

        public void RecordCountTarget(bool flush)
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);

            int txtCnt = 0;
            int jsonCnt = 0;
            int unknownCnt = 0;
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

            if (flush)
            {
                //flush to disk
                db.Flush();
            }

            //assert the cached and persisted counts....
            Assert.IsEqual<int>(db.Count(), assets.Length);
            Assert.IsEqual<int>(db.Count(a => a.AssetType == DigitalAssetType.Text), txtCnt);
            Assert.IsEqual<int>(db.Count(a => a.AssetType == DigitalAssetType.Json), jsonCnt);
            Assert.IsEqual<int>(db.Count(a => a.AssetType == DigitalAssetType.Unknown), unknownCnt);
        }

        public void Test_FindAllCountNoFlush()
        {
            this.FindAllCountTarget(false);
        }

        public void Test_FindAllCountWithFlush()
        {
            this.FindAllCountTarget(true);
        }

        public void FindAllCountTarget(bool flush)
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);

            DigitalAsset[] assets = base.ResolveAssetSet();
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                db.Insert(asset);
            }

            if (flush)
            {
                //flush to disk
                db.Flush();
            }

            int txtCnt = db.Count(a => a.AssetType == DigitalAssetType.Text);
            int jsonCnt = db.Count(a => a.AssetType == DigitalAssetType.Json);
            int unknownCnt = db.Count(a => a.AssetType == DigitalAssetType.Unknown);

            var txt = db.FindAll(a => a.AssetType == DigitalAssetType.Text);
            Assert.IsEqual<int>(txt.Length, txtCnt);

            var json = db.FindAll(a => a.AssetType == DigitalAssetType.Json);
            Assert.IsEqual<int>(json.Length, jsonCnt);

            var unknown = db.FindAll(a => a.AssetType == DigitalAssetType.Unknown);
            Assert.IsEqual<int>(unknown.Length, unknownCnt);
        }
        #endregion
    }
}
