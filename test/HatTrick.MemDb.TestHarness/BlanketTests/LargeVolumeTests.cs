using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HatTrick.InMemDb.TestHarness
{
    //TDDO: what do we really want to test here ???
    internal class LargeVolumeTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "large_volume");
        #endregion

        public LargeVolumeTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .EncryptWithPassword(() => "This is a super fancy and complex password.")
                .Register();
        }

        #region load db
        protected void LoadDb(MemDb<DigitalAsset> db)
        {
            DigitalAsset[] assets = base.ResolveAssetSet();
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                db.Insert(asset, (id) => asset.Id = id, true);
            }
        }
        #endregion

        #region large volume
        public void Test_LargeVolume()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Parallel.For(0, 1_000, (i) =>
                {
                    this.LoadDb(db);
                    if ((i % 10) == 0)
                        db.Flush();
                });
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
            }
        }
        #endregion
    }
}
