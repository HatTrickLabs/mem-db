using System;
using System.IO;
using System.Linq;
using HatTrick.InMemDb;

namespace HatTrick.InMemDb.TestHarness
{
    public class AppliedIndexManagementTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "index_management");
        #endregion

        #region ctors
        public AppliedIndexManagementTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
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

        #region index refresh
        public void Test_IndexRefresh()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                db.Flush();

                //at this point asset.XXHash values are 0, index will keyOf(0) is int[1000] pointers

                var ids = db.Query().Select(a => a.Id).ToArray();

                DateTime dt = db.Find(a => a.Id == 1).Imported;

                for (int i = 0; i < ids.Length; i++)
                {
                    //at this point, all asset.XXHash values are 0...
                    //set all xxhash values to i (0 thru 999)
                    db.Update(a => a.XXHash = (ulong)i, ids[i]);
                    //the update will gradually flatten the index of id from keyOf(0) is int[1000] pointers
                    //to 999 keyOf(xxhash) is int[1] pointers

                    //at this point, all asset.Imported values are equal
                    //add i milliseconds to every record.
                    db.Update(a => a.Imported = a.Imported.AddMilliseconds(i), ids[i]);
                }

                //assert index query results
                var set = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsGreaterThanEqualTo(500).ToArray();
                Assert.IsEqual(set.Length, 500);

                var set2 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsGreaterThan(500).ToArray();
                Assert.IsEqual(set2.Length, 499);

                var set3 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsLessThanEqualTo(500).ToArray();
                Assert.IsEqual(set3.Length, 501);

                var set4 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsLessThan(500).ToArray();
                Assert.IsEqual(set4.Length, 500);

                var set5 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsEqualTo(500).ToArray();
                Assert.IsEqual(set5.Length, 1);

                var set6 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsNotEqualTo(500).ToArray();
                Assert.IsEqual(set6.Length, 999);



                var set7 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsGreaterThanEqualTo(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set7.Length, 900);

                var set8 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsGreaterThan(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set8.Length, 899);

                var set9 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsLessThanEqualTo(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set9.Length, 101);

                var set10 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsLessThan(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set10.Length, 100);

                var set11 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsEqualTo(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set11.Length, 1);

                var set12 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsNotEqualTo(dt.AddMilliseconds(100)).ToArray();
                Assert.IsEqual(set12.Length, 999);
            }
        }
        #endregion

        #region index remove
        public void Test_IndexRemove()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                db.Flush();

                var assets = db.Query().OrderBy((a, b) => a.Id.CompareTo(b.Id)).ToArray();
                foreach (var asset in assets)
                {
                    //delete all even id assets...
                    if (asset.Id % 2 == 0)
                        db.Delete(asset.Id);
                }

                var set1 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsLessThanEqualTo("0500.json").ToArray();
                Assert.IsEqual(set1.Length, 251);
                var set2 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsLessThan("0500.json").ToArray();
                Assert.IsEqual(set2.Length, 250);
                var set3 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsGreaterThanEqualTo("0500.json").ToArray();
                Assert.IsEqual(set3.Length, 250);
                var set4 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsGreaterThan("0500.json").ToArray();
                Assert.IsEqual(set4.Length, 249);
                var set5 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsEqualTo("0500.json").ToArray();
                Assert.IsEqual(set5.Length, 1);
                var set6 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsNotEqualTo("0500.json").ToArray();
                Assert.IsEqual(set6.Length, 499);
                var set7 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsEqualTo(0).ToArray();
                Assert.IsEqual(set7.Length, 500);
                var set8 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsNotEqualTo(0).ToArray();
                Assert.IsEqual(set8.Length, 0);
                var set9 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsLessThan(0).ToArray();
                Assert.IsEqual(set9.Length, 0);
                var set10 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsGreaterThan(0).ToArray();
                Assert.IsEqual(set10.Length, 0);
                var set11 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsGreaterThanEqualTo(0).ToArray();
                Assert.IsEqual(set11.Length, 500);
                var set12 = db.QueryViaIndex<ulong>(nameof(DigitalAsset.XXHash)).IsLessThanEqualTo(0).ToArray();
                Assert.IsEqual(set12.Length, 500);
            }
        }
        #endregion
    }
}
