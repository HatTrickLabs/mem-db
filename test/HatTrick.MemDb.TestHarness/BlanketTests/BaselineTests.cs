// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.IO;

namespace HatTrick.Data.TestHarness
{
    public abstract class BaselineTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "baseline");
        #endregion

        #region interface
        protected string Dataset => _dataset;
        protected string DbPath => _dbPath;
        #endregion

        #region ctors
        public BaselineTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        { } 
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

        #region ensure deep copy
        public void Test_EnsureDeepCopy()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);

            DigitalAsset[] assets = base.ResolveAssetSet();
            var asset = assets[0];
            string origName = asset.Name;
            db.Insert(asset);

            asset.Name = "aaa";
            var assetOut1 = db.Find(a => true);
            Assert.IsEqual(assetOut1.Name, origName);

            db.Update(a => a.Name = "bbb", a => true);
            Assert.IsEqual(assetOut1.Name, origName);

            var assetOut2 = db.Find(a => true);
            Assert.IsEqual(assetOut2.Name, "bbb");
        }
        #endregion

        #region inserted record counts
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
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            if (flush)
            {
                //flush to disk
                db.Flush();
            }

            //assert the cached and persisted counts....
            Assert.IsEqual<int>(db.Count(), txtCnt + jsonCnt + unknownCnt);
            Assert.IsEqual<int>(db.Count(a => a.AssetType == DigitalAssetType.Text), txtCnt);
            Assert.IsEqual<int>(db.Count(a => a.AssetType == DigitalAssetType.Json), jsonCnt);
            Assert.IsEqual<int>(db.Count(a => a.AssetType == DigitalAssetType.Unknown), unknownCnt);

            //assert the find all based on types returns correct counts
            var txt = db.FindAll(a => a.AssetType == DigitalAssetType.Text);
            Assert.IsEqual<int>(txt.Length, txtCnt);

            var json = db.FindAll(a => a.AssetType == DigitalAssetType.Json);
            Assert.IsEqual<int>(json.Length, jsonCnt);

            var unknown = db.FindAll(a => a.AssetType == DigitalAssetType.Unknown);
            Assert.IsEqual<int>(unknown.Length, unknownCnt);
        }
        #endregion

        #region updated record counts
        public void Test_UpdatedRecourdCountNoFlush()
        {
            this.UpdatedRecordCountTarget(false);
        }

        public void Test_UpdateReocrdCountWithFlush()
        {
            this.UpdatedRecordCountTarget(true);
        }

        public void UpdatedRecordCountTarget(bool flush)
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            if (flush)
            {
                //flush to disk
                db.Flush();
            }

            //run updates on all records
            Assert.IsEqual<int>(db.Update(a => a.XXHash = 1, a => a.AssetType == DigitalAssetType.Text), txtCnt);
            Assert.IsEqual<int>(db.Update(a => a.XXHash = 2, a => a.AssetType == DigitalAssetType.Json), jsonCnt);
            Assert.IsEqual<int>(db.Update(a => a.XXHash = 3, a => a.AssetType == DigitalAssetType.Unknown), unknownCnt);

            if (flush)
            {
                //flush to disk
                db.Flush();
            }

            //assert the update counts 
            Assert.IsEqual<int>(db.Count(a => a.XXHash == 1), txtCnt);
            Assert.IsEqual<int>(db.Count(a => a.XXHash == 2), jsonCnt);
            Assert.IsEqual<int>(db.Count(a => a.XXHash == 3), unknownCnt);

            //asset the find all returns correct updated record counts
            var txt = db.FindAll(a => a.XXHash == 1);
            Assert.IsEqual<int>(txt.Length, txtCnt);

            var json = db.FindAll(a => a.XXHash == 2);
            Assert.IsEqual<int>(json.Length, jsonCnt);

            var unknown = db.FindAll(a => a.XXHash == 3);
            Assert.IsEqual<int>(unknown.Length, unknownCnt);
        }
        #endregion

        #region deleted record counts
        public void Test_DeletedReordCountsNoFlush()
        {
            this.DeletedRecordCountsTarget(false);
        }

        public void Test_DeletedReordCountsWithFlush()
        {
            this.DeletedRecordCountsTarget(true);
        }

        public void DeletedRecordCountsTarget(bool flush)
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            if (flush)
            {
                //flush to disk
                db.Flush();
            }

            Assert.IsEqual<int>(db.Delete(a => a.AssetType == DigitalAssetType.Text), txtCnt);
            if (flush) db.Flush();
            Assert.IsEqual<int>(db.Count(), jsonCnt + unknownCnt);
            Assert.IsEqual<int>(db.Delete(a => a.AssetType == DigitalAssetType.Json), jsonCnt);
            if (flush) db.Flush();
            Assert.IsEqual<int>(db.Count(), unknownCnt);
            Assert.IsEqual<int>(db.Delete(a => a.AssetType == DigitalAssetType.Unknown), unknownCnt);
            if (flush) db.Flush();
            //the db should now be empty.
            Assert.IsEqual<int>(db.Count(), 0);
        }
        #endregion

        #region accurate records
        public void Test_AccurateRecords()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            this.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            string namePrefix = null;
            DigitalAsset a = null;
            DigitalAsset[] set = null;
            int cnt = 0;
            bool exists = false;
            for (int i = 0; i < db.Count(); i++)
            {
                namePrefix = i.ToString("0000");
                a = db.Find(a => a.Name.StartsWith(namePrefix));
                Assert.IsNotNull(a);
                set = db.FindAll(a => a.Name.StartsWith(namePrefix));
                Assert.IsEqual<int>(set.Length, 1);
                cnt = db.Count(a => a.Name.StartsWith(namePrefix));
                Assert.IsEqual<int>(cnt, 1);
                exists = db.Exists(a => a.Name.StartsWith(namePrefix));
                Assert.IsEqual<bool>(exists, true);
            }

            for (int i = 0; i < db.Count(); i++)
            {
                namePrefix = i.ToString("0000");
                db.Update(a => a.XXHash = 1, a => a.Name.StartsWith(namePrefix));
                Assert.IsEqual<int>(db.Count(a => a.XXHash != 0), i + 1);
            }

            //the following should NOT exist as it is 1 above the last inserted
            namePrefix = (db.Count()).ToString("0000");
            a = db.Find(a => a.Name.StartsWith(namePrefix));
            Assert.IsNull(a);

            set = db.FindAll(a => a.Name.StartsWith(namePrefix));
            Assert.IsEqual<int>(set.Length, 0);
            cnt = db.Count(a => a.Name.StartsWith(namePrefix));
            Assert.IsEqual<int>(cnt, 0);
            exists = db.Exists(a => a.Name.StartsWith(namePrefix));
            Assert.IsEqual<bool>(exists, false);
        }
        #endregion

        #region update close reopon accurate records
        public void Test_LoadUpdateCloseReopenAccurateRecordsNoFlush()
        {
            LoadUpdateCloseReopenAccurateRecordsTarget(false);
        }

        public void Test_LoadUpdateCloseReopenAccurateRecordsWithFlush()
        {
            LoadUpdateCloseReopenAccurateRecordsTarget(true);
        }

        public void LoadUpdateCloseReopenAccurateRecordsTarget(bool flush)
        {
            int txtCnt;
            int jsonCnt;
            int unknownCnt;

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db, out txtCnt, out jsonCnt, out unknownCnt);

                db.Update(a => a.XXHash = 101, a => string.Compare(a.Extension, ".json", true) == 0);
                db.Delete(a => a.Name == "0900");//first unknown
                db.Flush();
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Assert.IsEqual(db.Count(a => a.XXHash == 101 && a.AssetType == DigitalAssetType.Json), jsonCnt);
                Assert.IsEqual(db.Count(a => a.Extension == ".txt"), txtCnt);
                Assert.IsEqual(db.Count(a => a.Extension == string.Empty), unknownCnt - 1);
                Assert.IsNull(db.Find(a => a.Name == "0900"));
                Assert.IsEqual(db.Count(), (txtCnt + jsonCnt + unknownCnt - 1));
            }
        }
        #endregion
    }
}
