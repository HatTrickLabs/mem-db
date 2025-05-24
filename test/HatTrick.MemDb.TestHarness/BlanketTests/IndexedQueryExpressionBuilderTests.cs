using System;
using System.IO;
using System.Linq;
using HatTrick.InMemDb;

namespace HatTrick.InMemDb.TestHarness
{
    public class IndexedQueryExpressionBuilderTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "indexed_expression_builder");
        #endregion

        #region ctors
        public IndexedQueryExpressionBuilderTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .CloneWith(() => new DigitalAssetCloner())//already tested, lets use the cloner and serializer to speed things up
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .ApplyIndex<string>(nameof(DigitalAsset.Name), (a) => a.Name)
                .Register();
        }
        #endregion

        #region load db
        protected void LoadDb(MemDb<DigitalAsset> db)
        {
            DigitalAsset[] assets = base.ResolveAssetSet();
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                db.Insert(asset, (id) => asset.Id = id, false);
            }
        }
        #endregion

        #region equal to
        public void Test_EqualTo()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                db.Flush();

                var assets = db.QueryViaIndex<string>(nameof(DigitalAsset.Name))
                    .IsLessThan("0977")
                    .ToArray();
            }
        }
        #endregion
    }
}
