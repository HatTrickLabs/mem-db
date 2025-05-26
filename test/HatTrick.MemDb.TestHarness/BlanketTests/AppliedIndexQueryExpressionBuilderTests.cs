using System;
using System.IO;
using System.Linq;
using HatTrick.InMemDb;

namespace HatTrick.InMemDb.TestHarness
{
    public class AppliedIndexQueryExpressionBuilderTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "indexed_expression_builder");
        #endregion

        #region ctors
        public AppliedIndexQueryExpressionBuilderTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .IndexOnIdentity(true)
                .ApplyIndex<string>(nameof(DigitalAsset.Name), (a) => a.Name)
                .ApplyIndex<DateTime>(nameof(DigitalAsset.Imported), (a) => a.Imported)
                .ApplyIndex<ulong>(nameof(DigitalAsset.XXHash), (a) => a.XXHash)
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

        #region index build
        public void Test_IndexBuild()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                db.Flush();
            }
        }
        #endregion

        #region index updates
        public void Test_IndexUpdates()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                db.Flush();

                var ids = db.Query().Select(a => a.Id).ToArray();

                DateTime dt = db.Find(a => a.Id == 1).Imported;

                for (int i = 0; i < ids.Length; i++)
                {
                    db.Update(a => a.XXHash = (ulong)i, ids[i]);
                    db.Update(a => a.Imported = a.Imported.AddMilliseconds(i + 1), ids[i]);
                }

                var set = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsGreaterThanEqualTo(500).ToArray();
                Console.WriteLine(set.Length);

                var set2 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsGreaterThan(dt.AddMilliseconds(100)).ToArray();
                Console.WriteLine(set2.Length);
            }
        }
        #endregion
    }
}
