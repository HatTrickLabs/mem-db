
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace HatTrick.InMemDb.TestHarness
{
    //TODO...
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
                .ApplyIndex<string>(nameof(DigitalAsset.Extension), (a) => a.Extension)
                .ApplyIndex<string>(nameof(DigitalAsset.Name), (a) => a.Name)
                .ApplyIndex<long>(nameof(DigitalAsset.Length), (a) => a.Length)
                .ApplyIndex<DateTime>(nameof(DigitalAsset.Imported), (a) => a.Imported)
                .ApplyIndex<ulong>(nameof(DigitalAsset.XXHash), (a) => a.XXHash)
                .ApplyIndex<DigitalAssetType>(nameof(DigitalAsset.AssetType), a => a.AssetType)
                .ApplyIndex<long>(nameof(DigitalAsset.Id), a => a.Id)
                .ApplyIndex<string>(nameof(DigitalAsset.Tags), a => a.Tags)
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

        #region x
        public void Test_X()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                db.Flush();

                var set = db.QueryViaIndexedSet<string>(nameof(DigitalAsset.Tags)).AllNotEqual("xxx").ToArray();
            }
        }
        #endregion

        #region basic query
        public void Test_BasicQuery()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);

                int json = db.Count(a => a.Extension == ".json");
                int txt = db.Count(a => a.Extension == ".txt");
                int all = db.Count();

                var set = db.QueryViaIndex<string>(nameof(DigitalAsset.Extension)).IsEqualTo(".json").ToArray();
                Assert.IsEqual(json, set.Length);

                var set2 = db.QueryViaIndex<string>(nameof(DigitalAsset.Extension)).IsNotEqualTo(".json").ToArray();
                Assert.IsEqual(all - json, set2.Length);

                //all assets should have the same imported timestamp
                DateTime[] imported = db.Query().SelectDistinct(a => a.Imported);
                Assert.IsEqual(imported.Length, 1);

                DateTime[] imported2 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported))
                    .IsGreaterThan(DateTime.MinValue)
                    .SelectDistinct(a => a.Imported);
                Assert.IsEqual(imported2.Length, imported.Length);

                var set3 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported)).IsEqualTo(imported[0]).ToArray();
                Assert.IsEqual(set3.Length, all);

                var set4 = db.QueryViaIndex<string>(nameof(DigitalAsset.Extension)).In(".json", ".txt").ToArray();
                Assert.IsEqual(set4.Length, (txt + json));

                var set5 = db.QueryViaIndex<string>(nameof(DigitalAsset.Extension)).In(".json").ToArray();
                Assert.IsEqual(set5.Length, json);

                var set6 = db.QueryViaIndex<string>(nameof(DigitalAsset.Extension)).In(".json", ".txt", ".docx", ".xlxs", ".jpg", ".jpeg", ".mp4").ToArray();
                Assert.IsEqual(set6.Length, (json + txt));

                var set7 = db.QueryViaIndex<string>(nameof(DigitalAsset.Extension)).In(".docx", ".xlxs", ".jpg", ".jpeg", ".mp4").ToArray();
                Assert.IsEqual(set7.Length, 0);
            }
        }
        #endregion

        #region count
        public void Test_Count()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);

                int zero500 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsEqualTo("0500.json").Count();
                Assert.IsEqual(zero500, 1);

                int notZero500 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsNotEqualTo("0500.json").Count();
                Assert.IsEqual(notZero500, db.Count() - 1);

                int lessThanZero500 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsLessThan("0500.json").Count();
                Assert.IsEqual(lessThanZero500, 500);

                int greaterThanZero500 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsGreaterThan("0500.json").Count();
                Assert.IsEqual(greaterThanZero500, 499);

                int lessThanEqualToZero500 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsLessThanEqualTo("0500.json").Count();
                Assert.IsEqual(lessThanEqualToZero500, 501);

                int greaterThanEqualZero500 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsGreaterThanEqualTo("0500.json").Count();
                Assert.IsEqual(greaterThanEqualZero500, 500);

                int inZero500 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).In("0500.json").Count();
                Assert.IsEqual(inZero500, 1);
            }
        }
        #endregion

        #region order by
        public void Test_OrderBy()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);

                //should get 500 the assets in order of id desc
                var set = db.QueryViaIndex<string>(nameof(DigitalAsset.Name))
                    .IsLessThan("0500.json")
                    .OrderBy((a, b) => b.Id.CompareTo(a.Id))
                    .ToArray();

                long max = db.Query().Where(a => string.Compare(a.Name, "0500.json", false) == -1).Max(a => a.Id);
                for (int i = 0; i < set.Length; i++)
                {
                    Assert.IsEqual(set[i].Id, max--);
                }
            }
        }
        #endregion

        #region group by
        public void Test_GroupBy()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);

                int jsonCnt = db.Count(a => a.Extension == ".json");
                int txtCnt = db.Count(a => a.Extension == ".txt");
                
                var set = db.QueryViaIndex<string>(nameof(DigitalAsset.Extension))
                    .IsGreaterThan(string.Empty)
                    .GroupBy(a => a.Extension)
                    .Having(g => g.Count() > 400)
                    .Select(g => (g.Key, g.Count())).ToArray();

                Assert.IsEqual<int>(Array.Find(set, s => s.Key == ".txt").Item2, txtCnt);
                Assert.IsEqual<int>(Array.Find(set, s => s.Key == ".json").Item2, default);
            }
        }
        #endregion

        #region skip / limit
        public void Test_SkipAndLimit()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);

                var set = db.QueryViaIndex<string>(nameof(DigitalAsset.Extension))
                    .IsEqualTo(".json")//400
                    .Skip(100)
                    .ToArray();
                Assert.IsEqual(set.Length, 300);

                var set2 = db.QueryViaIndex<string>(nameof(DigitalAsset.Extension))
                    .IsEqualTo(".txt")//500
                    .Limit(300)
                    .ToArray();
                Assert.IsEqual(set2.Length, 300);

                var set3 = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported))
                    .IsGreaterThan(DateTime.MinValue)//should get ALL assets
                    .Skip(500).Limit(500)
                    .ToArray();
                Assert.IsEqual(set3.Length, 500);

                var set4 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name))
                    .IsGreaterThanEqualTo("0500.json")//filters set down to 500 recs
                    .OrderBy((a, b) => b.Id.CompareTo(a.Id))
                    .Skip(100).Limit(100)
                    .ToArray();

                Assert.IsEqual(set4.Length, 100);
                Assert.IsEqual(set4[0].Id, 900);
                Assert.IsEqual(set4[^1].Id, 801);
            }
        }
        #endregion

        #region skip / limit (exhaustion)
        public void Test_SkipAndLimitExhaustion()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);

                //simple skip
                var remainder = db.QueryViaIndex<string>(nameof(DigitalAsset.Name))
                    .IsNotEqualTo("xxx")//1,000
                    .Skip(db.Count())
                    .ToArray();
                Assert.IsEqual(remainder.Length, 0);

                //skip limit
                var remainder2 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name))
                    .IsLessThan("xxx")//1,000
                    .Skip(db.Count())
                    .Limit(100)
                    .ToArray();
                Assert.IsEqual(remainder2.Length, 0);
            }
        }
        #endregion

        #region select
        public void Test_Select()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);
            int txtCnt, jsonCnt, unknownCnt;
            txtCnt = db.Count(a => a.AssetType == DigitalAssetType.Text);
            jsonCnt = db.Count(a => a.AssetType == DigitalAssetType.Json);
            unknownCnt = db.Count(a => a.AssetType == DigitalAssetType.Unknown);

            string[] names = db.QueryViaIndex<string>(nameof(DigitalAsset.Name))
                .IsLessThan("zzz")//1_000
                .Select(a => a.Name);

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

            (string name, long len)[] nameLenPairs = db.QueryViaIndex<DateTime>(nameof(DigitalAsset.Imported))
                .IsGreaterThan(DateTime.MinValue)//1_000
                .Select(a => (a.Name, a.Length));

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
        public void Test_SelectDistinct()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);

            long[] lengths = db.QueryViaIndex<string>(nameof(DigitalAsset.Extension))
                .IsNotEqualTo(".xxx")//1_000
                .SelectDistinct(a => a.Length);

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

        #region sum
        public void Test_Sum()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);

            long totalLen = 0;
            //pull out all the assets loaded
            var assets = db.FindAll(a => true);
            for (int i = 0; i < assets.Length; i++)
            {
                totalLen += assets[i].Length;
            }

            //sum expression should calc the same result
            Assert.IsEqual<long>(db.QueryViaIndex<long>(nameof(DigitalAsset.Length))
                .IsGreaterThan(0)//all of them
                .Sum(a => a.Length), totalLen);
        }

        public void Test_FilteredSum()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);

            int jsonCnt = db.Count(a => a.AssetType == DigitalAssetType.Json);
            long jsonLength = db.QueryViaIndex<DigitalAssetType>(nameof(DigitalAsset.AssetType))
                .IsEqualTo(DigitalAssetType.Json)
                .Sum(a => a.Length);

            Assert.IsEqual(jsonLength, (jsonCnt * 64));
        }

        public void Test_RestrictedSum()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);

            long length = db.QueryViaIndex<long>(nameof(DigitalAsset.Length))
                .IsLessThan(1024)//all of them
                .Skip(100).Limit(100)
                .Sum(a => a.Length);

            Assert.IsEqual(length, (100 * 64));
        }
        #endregion

        #region max
        public void Test_Max()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);

            string highest = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsGreaterThan("0").Max(a => a.Name);
            Assert.IsEqual("0999", highest);

            int extLen = db.Query().Max(a => a.Extension.Length);
            Assert.IsEqual<int>(extLen, ".json".Length);
        }

        public void Test_FilteredMax()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);

            string highest = db.QueryViaIndex<string>(nameof(DigitalAsset.Extension)).IsEqualTo(".txt").Max(a => a.Name);
            Assert.IsEqual("0499.txt", highest);
        }

        public void Test_RestrictedMax()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);
            int txtCnt = db.Count(a => a.AssetType == DigitalAssetType.Text);
            int jsonCnt = db.Count(a => a.AssetType == DigitalAssetType.Json);

            string highest = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsGreaterThan("0")
                .Skip(txtCnt)
                .Limit(jsonCnt)
                .Max(a => a.Name);
            Assert.IsEqual("0899.json", highest);
        }
        #endregion

        #region min
        public void Test_Min()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);

            string lowest = db.QueryViaIndex<string>(nameof(DigitalAsset.Name))
                .IsLessThan("zzz")
                .Min(a => a.Name);

            Assert.IsEqual("0000.txt", lowest);

            int extLen = db.QueryViaIndex<string>(nameof(DigitalAsset.Name))
                .IsNotEqualTo("xxx")
                .Min(a => a.Extension.Length);
            Assert.IsEqual(extLen, string.Empty.Length);
        }
        #endregion

        #region avg
        public void Test_Avg()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);

            double avgLen = db.QueryViaIndex<long>(nameof(DigitalAsset.Length))
                .IsEqualTo(64)
                .Avg(a => a.Length);

            Assert.IsEqual(avgLen, 64.0);//all files are exactly 64 byte, therefore avg should be 64 bytes
        }
        #endregion

        #region update
        public void Test_Update()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);
            int txtCnt = db.Count(a => a.AssetType == DigitalAssetType.Text);
            int jsonCnt = db.Count(a => a.AssetType == DigitalAssetType.Json);
            int unknownCnt = db.Count(a => a.AssetType == DigitalAssetType.Unknown);

            //update all xxhash values to 8
            int cnt = db.QueryViaIndex<long>(nameof(DigitalAsset.Id))
                .IsGreaterThan(-1)
                .Update(a => a.XXHash = 8);
            Assert.IsEqual(cnt, (txtCnt + jsonCnt + unknownCnt));
            Assert.IsEqual(db.Count(a => a.XXHash == 8), (txtCnt + jsonCnt + unknownCnt));

            //update all json files xxhash value to 9
            int cnt2 = db.QueryViaIndex<DigitalAssetType>(nameof(DigitalAsset.AssetType))
                .IsEqualTo(DigitalAssetType.Json)
                .Update(a => a.XXHash = 9);
            Assert.IsEqual(cnt2, jsonCnt);
            Assert.IsEqual(db.Count(a => a.XXHash == 9), jsonCnt);

            var jsonSet = db.FindAll(a => a.Extension == ".json");
            Assert.IsEqual(jsonSet.Length, jsonCnt);
            Assert.IsEqual(Array.TrueForAll(jsonSet, a => a.XXHash == 9), true);
        }

        public void Test_OrderedAndRestrictedUpdate()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);
            int txtCnt = db.Count(a => a.AssetType == DigitalAssetType.Text);
            int jsonCnt = db.Count(a => a.AssetType == DigitalAssetType.Json);
            int unknownCnt = db.Count(a => a.AssetType == DigitalAssetType.Unknown);

            //the natural order of the loaded data is txt, json, unknown...lets run a update expression off of a re-ordered set.

            //order by name desc and delete the unknown count (which should now be the first 100)
            int updated = db.QueryViaIndex<long>(nameof(DigitalAsset.Id))
                .IsGreaterThan(-1)
                .OrderBy((a, b) => b.Name.CompareTo(a.Name))
                .Skip(0).Limit(unknownCnt)
                .Update(a => a.XXHash = 101);

            Assert.IsEqual(updated, unknownCnt);
            Assert.IsEqual(db.Count(), txtCnt + jsonCnt + unknownCnt);
            Assert.IsEqual(db.Count(a => a.XXHash == 101), unknownCnt);
            Assert.IsEqual(db.FindAll(a => a.XXHash == 101).All(a => a.AssetType == DigitalAssetType.Unknown), true);
        }
        #endregion

        #region delete
        public void Test_Delete()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);
            int txtCnt = db.Count(a => a.AssetType == DigitalAssetType.Text);
            int jsonCnt = db.Count(a => a.AssetType == DigitalAssetType.Json);
            int unknownCnt = db.Count(a => a.AssetType == DigitalAssetType.Unknown);

            //delete all known unknown file type assets based on known locality
            int cnt = db.QueryViaIndex<long>(nameof(DigitalAsset.Id))
                .IsNotEqualTo(-1)
                .Skip(txtCnt + jsonCnt)
                .Limit(unknownCnt)
                .Delete();
            Assert.IsEqual(cnt, unknownCnt);
            Assert.IsEqual(db.Count(a => a.AssetType == DigitalAssetType.Unknown), 0);
            Assert.IsEqual(db.Count(a => a.AssetType != DigitalAssetType.Unknown), (txtCnt + jsonCnt));

            //delete a specific assert by name
            int cnt2 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name))
                .IsEqualTo("0100.txt")
                .Delete();
            Assert.IsEqual(cnt2, 1);
            Assert.IsNull(db.Find(a => a.Name == "0100.txt"));//should be gone...

            //delete the rest of the .txt assets
            int cnt3 = db.QueryViaIndex<string>(nameof(DigitalAsset.Extension))
                .IsEqualTo(".txt")
                .Delete();
            Assert.IsEqual(cnt3, txtCnt - 1);
            Assert.IsEqual(db.Count(a => a.Extension == ".txt"), 0);
            Assert.IsEqual(db.Count(a => a.Extension == ".json"), jsonCnt);//all json assets should remain
        }

        public void Test_OrderedAndRestrictedDelete()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db);
            int txtCnt = db.Count(a => a.AssetType == DigitalAssetType.Text);
            int jsonCnt = db.Count(a => a.AssetType == DigitalAssetType.Json);
            int unknownCnt = db.Count(a => a.AssetType == DigitalAssetType.Unknown);

            //the natural order of the loaded data is txt, json, unknown...lets run a delete expression off of a re-ordered set.

            //order by id desc, skip 0 limit unkown count (which should now be the first 100) and delete the results.
            int deleted = db.QueryViaIndex<long>(nameof(DigitalAsset.Id))
                .IsGreaterThan(-1)
                .OrderBy((a, b) => b.Id.CompareTo(a.Id))
                .Skip(0)
                .Limit(unknownCnt)
                .Delete();

            Assert.IsEqual(deleted, unknownCnt);
            Assert.IsEqual(db.Count(), txtCnt + jsonCnt);
            Assert.IsEqual(db.FindAll(a => true).All(a => a.AssetType != DigitalAssetType.Unknown), true);
        }
        #endregion

        #region test
        public void Test_EmptyDb()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var set1 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsEqualTo("x").ToArray();
                var set2 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).In("x").ToArray();
                var set3 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsNotEqualTo("x").ToArray();
                var set4 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsLessThan("x").ToArray();
                var set5 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsLessThanEqualTo("x").ToArray();
                var set6 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsGreaterThan("x").ToArray();
                var set7 = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsGreaterThanEqualTo("x").ToArray();
            }
        }
        #endregion
    }
}
