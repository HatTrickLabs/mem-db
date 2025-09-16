using System;
using System.IO;
using System.Linq;

namespace HatTrick.Data.TestHarness
{
    public class QueryExpressionBuilderTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "expression_builder");
        #endregion

        #region ctors
        public QueryExpressionBuilderTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .CloneWith(() => new DigitalAssetCloner())//already tested, lets use the cloner and serializer to speed things up
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

                db.Insert(asset, (id) => asset.Id = id, false);
            }
        }
        #endregion

        #region where
        public void Test_WhereExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            var txtAssets = db.Query().Where(a => a.Extension == ".txt").ToArray();
            Assert.IsEqual<int>(txtAssets.Length, txtCnt);

            var jsonAssets = db.Query().Where(a => a.Extension == ".json").ToArray();
            Assert.IsEqual<int>(jsonAssets.Length, jsonCnt);

            var unknownAssets = db.Query().Where(a => a.Extension == string.Empty).ToArray();
            Assert.IsEqual<int>(unknownAssets.Length, unknownCnt);
        }
        #endregion

        #region order by
        public void Test_OrderByExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //reverse by id...
            var reversed = db.Query().OrderBy((a, b) => b.Id.CompareTo(a.Id)).ToArray();
            //assert that record at index 0 is the highest auto generated id
            Assert.IsEqual<long>(reversed[0].Id, (long)(txtCnt + jsonCnt + unknownCnt));
            //assert that record at LAST index is the lowest auto generated id
            Assert.IsEqual<long>(reversed[^1].Id, 1);


            var nameDesc = db.Query().OrderBy((a, b) => b.Name.CompareTo(a.Name)).ToArray();
            //txt was 1st 500 inserted (0 thru 499)
            //json was next 400 inserted (500 thru 899)
            //unknown was last 100 inserted (900 thru 999)
            //we've reversed them so:
            //the first 100 should be extension-less
            //the next 400 should be .json
            //the last 500 should be .txt
            for (int i = 0; i < nameDesc.Length; i++)
            {
                if (i < unknownCnt)
                    Assert.IsEqual(nameDesc[i].Extension, string.Empty);
                
                if (i >= unknownCnt && i < (unknownCnt + jsonCnt))
                    Assert.IsEqual(nameDesc[i].Extension, ".json");

                if (i >= (unknownCnt + jsonCnt))
                    Assert.IsEqual(nameDesc[i].Extension, ".txt");
            }

        }
        #endregion

        #region group by
        public void Test_GroupByExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //basic group by
            var sets = db.Query().GroupBy(a => a.Extension).Select(g => (g.Key, g.Count())).ToArray();

            Assert.IsNotEqual(Array.Find(sets, s => s.Key == ".txt"), default);
            Assert.IsNotEqual(Array.Find(sets, s => s.Key == ".json"), default);
            Assert.IsNotEqual(Array.Find(sets, s => s.Key == string.Empty), default);

            Assert.IsEqual<int>(Array.Find(sets, s => s.Key == ".txt").Item2, txtCnt);
            Assert.IsEqual<int>(Array.Find(sets, s => s.Key == ".json").Item2, jsonCnt);
            Assert.IsEqual<int>(Array.Find(sets, s => s.Key == string.Empty).Item2, unknownCnt);
        }
        #endregion

        #region group by having
        public void Test_GroupByExpressionHaving()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //group by + having
            var sets2 = db.Query().GroupBy(a => a.Extension).Having(g => g.Count() > 100).Select(g => (g.Key, g.Count())).ToArray();
            Assert.IsEqual(sets2.Length, 2);
            Assert.IsNotEqual(Array.Find(sets2, s => s.Key == ".txt"), default);
            Assert.IsNotEqual(Array.Find(sets2, s => s.Key == ".json"), default);
            Assert.IsEqual(Array.Exists(sets2, s => s.Key == string.Empty), false);//should have eliminated uknowns with having expression
            Assert.IsEqual(Array.Find(sets2, s => s.Key == ".txt").Item2, txtCnt);
            Assert.IsEqual(Array.Find(sets2, s => s.Key == ".json").Item2, jsonCnt);
        }
        #endregion

        #region skip / limit
        public void Test_SkipAndSkipLimitExpressions()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //simple skip
            var remainder = db.Query().Skip(txtCnt).ToArray();
            Assert.IsEqual<int>(remainder.Length, (jsonCnt + unknownCnt));

            var remainder2 = db.Query().Where(a => a.AssetType == DigitalAssetType.Json).Limit(200).ToArray();
            Assert.IsEqual(remainder2.Length, 200);

            //simple skip + limit
            var jsonChunk = db.Query().Skip(txtCnt).Limit(jsonCnt).ToArray();//skip the txt assets, limit to the json assets
            Assert.IsEqual<int>(jsonChunk.Length, jsonCnt);
            Assert.IsEqual(jsonChunk.All(a => a.Extension == ".json"), true);
        }
        #endregion

        #region skip / limit (exhaustion)
        public void Test_SkipAndSkipLimitExhaustionExpressions()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //simple skip
            var remainder = db.Query().Skip(txtCnt + jsonCnt + unknownCnt).ToArray();
            Assert.IsEqual(remainder.Length, 0);

            var remainder2 = db.Query().Skip(txtCnt + jsonCnt + unknownCnt).Limit(100).ToArray();
            Assert.IsEqual(remainder2.Length, 0);
        }
        #endregion

        #region sum
        public void Test_SumExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            long totalLen = 0;
            //pull out all the assets loaded
            var assets = db.FindAll(a => true);
            for (int i = 0; i < assets.Length; i++)
            {
                totalLen += assets[i].Length;
            }

            //sum expression should calc the same result
            Assert.IsEqual<long>(db.Query().Sum(a => a.Length), totalLen);
        }

        public void Test_FilteredSumExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            long jsonLength = db.Query().Where(a => a.AssetType == DigitalAssetType.Json).Sum(a => a.Length);

            Assert.IsEqual(jsonLength, (jsonCnt * 64));
        }

        public void Test_RestrictedSumExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            long length = db.Query().Skip(100).Limit(100).Sum(a => a.Length);

            Assert.IsEqual(length, (100 * 64));
        }
        #endregion

        #region max
        public void Test_MaxExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            string highest = db.Query().Max(a => a.Name);
            Assert.IsEqual("0999", highest);

            int extLen = db.Query().Max(a => a.Extension.Length);
            Assert.IsEqual<int>(extLen, ".json".Length);
        }

        public void Test_FilteredMaxExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            string highest = db.Query().Where(a => a.AssetType == DigitalAssetType.Text).Max(a => a.Name);
            Assert.IsEqual("0499.txt", highest);
        }

        public void Test_RestrictedMaxExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            string highest = db.Query().Skip(txtCnt).Limit(jsonCnt).Max(a => a.Name);
            Assert.IsEqual("0899.json", highest);
        }
        #endregion

        #region min
        public void Test_MinExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            string lowest = db.Query().Min(a => a.Name);
            Assert.IsEqual("0000.txt", lowest);

            int extLen = db.Query().Min(a => a.Extension.Length);
            Assert.IsEqual(extLen, string.Empty.Length);
        }
        #endregion

        #region avg
        public void Test_AvgExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            double avgLen = db.Query().Avg(a => a.Length);
            Assert.IsEqual(avgLen, 64.0);//all files are exactly 64 byte, therefore avg should be 64 bytes
        }
        #endregion

        #region select 
        public void Test_SelectExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            string[] names = db.Query().Select(a => a.Name);
            Array.Sort(names, (a, b) => a.CompareTo(b));
            for (int i = 0; i < names.Length; i++)
            {
                if (i < txtCnt)
                    Assert.IsEqual(names[i], $"{i.ToString("0000")}.txt");

                if (i >= txtCnt && i < (txtCnt + jsonCnt))
                    Assert.IsEqual(names[i], $"{i.ToString("0000")}.json");

                if (i >= txtCnt + jsonCnt)
                    Assert.IsEqual(names[i], i.ToString("0000"));
            }

            (string name, long len)[] nameLenPairs = db.Query().Select(a => (a.Name, a.Length));
            for (int i = 0; i < nameLenPairs.Length; i++)
            {
                var pair = nameLenPairs[i];
                if (i < txtCnt)
                {
                    Assert.IsEqual(pair.name, $"{i.ToString("0000")}.txt");
                    Assert.IsEqual(pair.len, 64);
                }
                if (i >= txtCnt && i < (txtCnt + jsonCnt))
                {
                    Assert.IsEqual(pair.name, $"{i.ToString("0000")}.json");
                    Assert.IsEqual(pair.len, 64);
                }
                if (i >= txtCnt + jsonCnt)
                {
                    Assert.IsEqual(pair.name, i.ToString("0000"));
                    Assert.IsEqual(pair.len, 64);
                }
            }
        }
        #endregion

        #region select distinct
        public void Test_SelectDistinctExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            long[] lengths = db.Query().SelectDistinct(a => a.Length);
            Assert.IsEqual(lengths.Length, 1);//all files are the same 64 byte length
            Assert.IsEqual(Array.TrueForAll(lengths, l => l == 64), true);
            Assert.IsEqual(lengths[0], 64);

            string[] extensions = db.Query().SelectDistinct(a => a.Extension);
            Assert.IsEqual(extensions.Length, 3);//.txt, .json, string.Empty
            Assert.IsEqual(Array.Exists(extensions, a => a == ".txt"), true);
            Assert.IsEqual(Array.Exists(extensions, a => a == ".json"), true);
            Assert.IsEqual(Array.Exists(extensions, a => a == string.Empty), true);
        }
        #endregion

        #region update
        public void Test_UpdateExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //update all xxhash values to 8
            int cnt = db.Query().Update(a => a.XXHash = 8);
            Assert.IsEqual(cnt, (txtCnt + jsonCnt + unknownCnt));
            Assert.IsEqual(db.Count(a => a.XXHash == 8), (txtCnt + jsonCnt + unknownCnt));

            //update all json files xxhash value to 9
            int cnt2 = db.Query().Where(a => a.AssetType == DigitalAssetType.Json).Update(a => a.XXHash = 9);
            Assert.IsEqual(cnt2, jsonCnt);
            Assert.IsEqual(db.Count(a => a.XXHash == 9), jsonCnt);

            var jsonSet = db.FindAll(a => a.Extension == ".json");
            Assert.IsEqual(jsonSet.Length, jsonCnt);
            Assert.IsEqual(Array.TrueForAll(jsonSet, a => a.XXHash == 9), true);
        }

        public void Test_OrderedAndRestrictedUpdateExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //the natural order of the loaded data is txt, json, unknown...lets run a update expression off of a re-ordered set.

            //order by name desc and delete the unknown count (which should now be the first 100)
            int updated = db.Query().OrderBy((a, b) => b.Name.CompareTo(a.Name)).Skip(0).Limit(unknownCnt).Update(a => a.XXHash = 101);

            Assert.IsEqual(updated, unknownCnt);
            Assert.IsEqual(db.Count(), txtCnt + jsonCnt + unknownCnt);
            Assert.IsEqual(db.Count(a => a.XXHash == 101), unknownCnt);
            Assert.IsEqual(db.FindAll(a => a.XXHash == 101).All(a => a.AssetType == DigitalAssetType.Unknown), true);
        }
        #endregion

        #region delete
        public void Test_DeleteExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //delete all known unknown file type assets based on known locality
            int cnt = db.Query().Skip(txtCnt + jsonCnt).Limit(unknownCnt).Delete();
            Assert.IsEqual(cnt, unknownCnt);
            Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Unknown), 0);
            Assert.IsEqual(db.Count(a => a.AssetType != DigitalAssetType.Unknown), (txtCnt + jsonCnt));

            //delete a specific assert by name
            int cnt2 = db.Query().Where(a => a.Name == "0100.txt").Delete();
            Assert.IsEqual(cnt2, 1);
            Assert.IsNull(db.Find(a => a.Name == "0100.txt"));//should be gone...

            //delete the rest of the .txt assets
            int cnt3 = db.Query().Where(a => a.Extension == ".txt").Delete();
            Assert.IsEqual(cnt3, txtCnt - 1);
            Assert.IsEqual(db.Count(a => a.Extension == ".txt"), 0);
            Assert.IsEqual(db.Count(a => a.Extension == ".json"), jsonCnt);//all json assets should remain
        }

        public void Test_OrderedAndRestrictedDeleteExpression()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //the natural order of the loaded data is txt, json, unknown...lets run a delete expression off of a re-ordered set.

            //order by id desc, skip 0 limit unkown count (which should now be the first 100) and delete the results.
            int deleted = db.Query().OrderBy((a, b) => b.Id.CompareTo(a.Id)).Skip(0).Limit(unknownCnt).Delete();

            Assert.IsEqual(deleted, unknownCnt);
            Assert.IsEqual(db.Count(), txtCnt + jsonCnt);
            Assert.IsEqual(db.FindAll(a => true).All(a => a.AssetType != DigitalAssetType.Unknown), true);
        }
        #endregion
    }
}
