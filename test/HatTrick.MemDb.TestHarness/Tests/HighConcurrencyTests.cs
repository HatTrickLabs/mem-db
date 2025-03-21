using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HatTrick.InMemDb.TestHarness
{
    public class HighConcurrencyTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = @"..\..\..\..\_db\high_concurrency";
        #endregion

        #region ctors
        public HighConcurrencyTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath).Register();
        }
        #endregion

        #region high concurrency inserts
        public void Test_HighConcurrencyInsertsNoFlush()
        {
            this.HighConcurrencyInsertsTarget(false);
        }

        public void Test_HighConcurrencyInsertsWithFlush()
        {
            this.HighConcurrencyInsertsTarget(true);
        }

        public void HighConcurrencyInsertsTarget(bool flush)
        {
            //pull in the assets 5 times in order to have a large enough parallel set.
            DigitalAsset[] assets1 = base.ResolveAssetSet();
            DigitalAsset[] assets2 = base.ResolveAssetSet();
            DigitalAsset[] assets3 = base.ResolveAssetSet();
            DigitalAsset[] assets4 = base.ResolveAssetSet();
            DigitalAsset[] assets5 = base.ResolveAssetSet();

            using var db = MemDb.Open<DigitalAsset>(_dataset);

            Thread t1 = new Thread(() => Array.ForEach(assets1, (asset) => db.Insert(asset, (id) => asset.Id = id)));
            Thread t2 = new Thread(() => Array.ForEach(assets2, (asset) => db.Insert(asset, (id) => asset.Id = id)));
            Thread t3 = new Thread(() => Array.ForEach(assets3, (asset) => db.Insert(asset, (id) => asset.Id = id)));
            Thread t4 = new Thread(() => Array.ForEach(assets4, (asset) => db.Insert(asset, (id) => asset.Id = id)));
            Thread t5 = new Thread(() => Array.ForEach(assets5, (asset) => db.Insert(asset, (id) => asset.Id = id)));

            t1.Start();
            t2.Start();
            t3.Start();
            t4.Start();
            t5.Start();

            if (flush)
                db.Flush();

            t1.Join();
            t2.Join();
            t3.Join();
            t4.Join();
            t5.Join();

            if (flush)
                db.Flush();

            //we should have 5,000 records, each with a unique auto incremented Id
            int length = assets1.Length + assets2.Length + assets3.Length + assets4.Length + assets5.Length;
            Assert.IsEqual<int>(db.Count(), length);
            for (int i = 0; i < length; i++)
            {
                int id = i + 1;
                Assert.IsNotNull(db.Find(a => a.Id == id));
            }

            //query distinct ids
            uint[] ids = db.Query().SelectDistinct<uint>(a => a.Id);
            Assert.IsEqual<int>(ids.Length, length);

            //we should have 5 sets of the same assets.
            var sets = db.Query().GroupBy(a => a.Name).Select(g => (g.Key, g.Count())).ToArray();

            //every set should have a count of 5
            Array.ForEach(sets, 
                ((string name, int count) itm) => Assert.IsEqual<int>(itm.count, 5)
            );
        }
        #endregion

        #region high concurrency updates
        public void Test_HiHighConcurrencyUpdatesNoFlush()
        {
            this.HighConcurrencyUpdatesTarget(false);
        }

        public void Test_HiHighConcurrencyUpdatesWithFlush()
        {
            this.HighConcurrencyUpdatesTarget(true);
        }

        public void HighConcurrencyUpdatesTarget(bool flush)
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);

            //build up 6000 asset set
            DigitalAsset[] assets1 = base.ResolveAssetSet();
            DigitalAsset[] assets2 = base.ResolveAssetSet();
            DigitalAsset[] assets3 = base.ResolveAssetSet();
            DigitalAsset[] assets4 = base.ResolveAssetSet();
            DigitalAsset[] assets5 = base.ResolveAssetSet();
            DigitalAsset[] assets6 = base.ResolveAssetSet();


            var assets = assets1.Concat(assets2).Concat(assets3).Concat(assets4).Concat(assets5).Concat(assets6).ToArray();
            Parallel.For(0, assets.Length, (int i) =>
            {
                var asset = assets[i];
                db.Insert(asset, (id) => asset.Id = id, false);
            });

            if (flush)
                db.Flush();

            Thread t1 = new Thread(() => { Array.ForEach(assets6, (a6) => db.Update(a => a.XXHash = 6, a => a.Id == a6.Id)); });
            Thread t2 = new Thread(() => { Array.ForEach(assets5, (a5) => db.Update(a => a.XXHash = 5, a => a.Id == a5.Id)); });
            Thread t3 = new Thread(() => { Array.ForEach(assets4, (a4) => db.Update(a => a.XXHash = 4, a => a.Id == a4.Id)); });
            Thread t4 = new Thread(() => { Array.ForEach(assets3, (a3) => db.Update(a => a.XXHash = 3, a => a.Id == a3.Id)); });
            Thread t5 = new Thread(() => { Array.ForEach(assets2, (a2) => db.Update(a => a.XXHash = 2, a => a.Id == a2.Id)); });
            Thread t6 = new Thread(() => { Array.ForEach(assets1, (a1) => db.Update(a => a.XXHash = 1, a => a.Id == a1.Id)); });

            t1.Start();
            t2.Start();
            t3.Start();
            t4.Start();
            t5.Start();
            t6.Start();

            t1.Join();
            t2.Join();
            t3.Join();
            t4.Join();
            t5.Join();
            t6.Join();

            if (flush)
                db.Flush();

            Assert.IsEqual(db.Count(), assets.Length);

            Assert.IsEqual<int>(db.Count(a => a.XXHash == 1), assets1.Length);
            Assert.IsEqual<int>(db.Count(a => a.XXHash == 2), assets2.Length);
            Assert.IsEqual<int>(db.Count(a => a.XXHash == 3), assets3.Length);
            Assert.IsEqual<int>(db.Count(a => a.XXHash == 4), assets4.Length);
            Assert.IsEqual<int>(db.Count(a => a.XXHash == 5), assets5.Length);
            Assert.IsEqual<int>(db.Count(a => a.XXHash == 6), assets6.Length);
        }
        #endregion

        #region high concurrency deletes
        public void HighConcurrencyDeletesTarget(bool flush)
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);

            //build up 6000 asset set
            DigitalAsset[] assets1 = base.ResolveAssetSet();
            DigitalAsset[] assets2 = base.ResolveAssetSet();
            DigitalAsset[] assets3 = base.ResolveAssetSet();
            DigitalAsset[] assets4 = base.ResolveAssetSet();
            DigitalAsset[] assets5 = base.ResolveAssetSet();
            DigitalAsset[] assets6 = base.ResolveAssetSet();


            var assets = assets1.Concat(assets2).Concat(assets3).Concat(assets4).Concat(assets5).Concat(assets6).ToArray();
            Parallel.For(0, assets.Length, (int i) =>
            {
                var asset = assets[i];
                db.Insert(asset, (id) => asset.Id = id, false);
            });

            if (flush)
                db.Flush();

            int deleted65 = 0;
            int deleted43 = 0;
            int deleted21 = 0;
            Thread t1 = new Thread(() => { Array.ForEach(assets6.Concat(assets5).ToArray(), (a65) => deleted65 = db.Delete(a => a.Id == a65.Id)); });
            Thread t2 = new Thread(() => { Array.ForEach(assets4.Concat(assets3).ToArray(), (a43) => deleted43 = db.Delete(a => a.Id == a43.Id)); });
            Thread t3 = new Thread(() => { Array.ForEach(assets2.Concat(assets1).ToArray(), (a21) => deleted21 = db.Delete(a => a.Id == a21.Id)); });

            t1.Start();
            t2.Start();
            t3.Start();

            t1.Join();
            t2.Join();
            t3.Join();

            if (flush)
                db.Flush();

            Assert.IsEqual<int>(db.Count(), 0);

            Assert.IsEqual<int>(deleted65, assets6.Length + assets5.Length);
            Assert.IsEqual<int>(deleted43, assets4.Length + assets3.Length);
            Assert.IsEqual<int>(deleted21, assets2.Length + assets1.Length);
        }
        #endregion

        #region high concurrency chaos
        public void Test_HighConcurrencyChaosNoFlush()
        {
            this.HighConcurrencyChaosTarget(false);
        }

        public void Test_HighConcurrencyChaosWithFlush()
        {
            this.HighConcurrencyChaosTarget(true);
        }

        public void HighConcurrencyChaosTarget(bool flush)
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);

            //build up 10,000 asset set
            DigitalAsset[] assets1 = base.ResolveAssetSet();
            DigitalAsset[] assets2 = base.ResolveAssetSet();
            DigitalAsset[] assets3 = base.ResolveAssetSet();
            DigitalAsset[] assets4 = base.ResolveAssetSet();
            DigitalAsset[] assets5 = base.ResolveAssetSet();
            DigitalAsset[] assets6 = base.ResolveAssetSet();
            DigitalAsset[] assets7 = base.ResolveAssetSet();
            DigitalAsset[] assets8 = base.ResolveAssetSet();
            DigitalAsset[] assets9 = base.ResolveAssetSet();
            DigitalAsset[] assets10 = base.ResolveAssetSet();


            var assets = assets1.Concat(assets2).Concat(assets3).Concat(assets4)
                                .Concat(assets5).Concat(assets6).Concat(assets7)
                                .Concat(assets8).Concat(assets9).Concat(assets10)
                                .ToArray();

            //insert all 10000 records in a parallel loop
            Parallel.For(0, assets.Length, (int i) =>
            {
                var asset = assets[i];
                db.Insert(asset, (id) => asset.Id = id, false);
            });

            if (flush)
                db.Flush();

            //ensure correct number of records
            Assert.IsEqual<int>(db.Count(), assets.Length);
            //ensure each record has unique / distinct Id
            Assert.IsEqual<int>(db.Query().SelectDistinct(a => a.Id).Count(), assets.Length);
            //ensure min id is 1
            Assert.IsEqual<uint>(db.Query().Min(a => a.Id), 1);
            //ensure max id is total record length
            Assert.IsEqual<uint>(db.Query().Max(a => a.Id), (uint)assets.Length);

            //t1 will update even number id records xxhash value (increment by 1)
            Thread t1 = new Thread(() => { db.Update(a => a.XXHash += 1, a => a.Id % 2 == 0 && a.Id <= assets.Length); });
            //t2 will update odd number id records xxhash value (increment by 1)
            Thread t2 = new Thread(() => { db.Update(a => a.XXHash += 1, a => a.Id % 2 == 1 && a.Id <= assets.Length); });
            //t3 will update every original record xxhash value (increment by 1) original being the original set of 10,000
            Thread t3 = new Thread(() => { 
                Parallel.For(1, (assets.Length) + 1, (int i) => { db.Update(a => a.XXHash += 1, a => a.Id == i && i <= assets.Length); }); 
            });

            //t4 will insert 1000 new records
            DigitalAsset[] assets11 = base.ResolveAssetSet();
            Thread t4 = new Thread(() => {
                Parallel.For(0, assets11.Length, (int i) =>
                {
                    var asset = assets[i];
                    db.Insert(asset, (id) => asset.Id = id, false);
                });
            });

            //t5 will attempt to delete the 1000 newly inserted records in a loop until all 1000 show up, are found and deleted.
            int deleted = 0;
            Thread t5 = new Thread(() => {
                do
                {
                    Parallel.For(assets.Length + 1, (assets.Length + assets11.Length) + 1, (int i) => 
                    {  
                        int cnt = db.Delete(a => a.Id == i);
                        if (cnt > 0)
                            Interlocked.Increment(ref deleted);
                    });

                } while (deleted < assets11.Length);
            });

            t1.Start();
            t2.Start();
            t3.Start();
            t4.Start();
            t5.Start();

            if (flush)
                db.Flush();

            t1.Join();
            t2.Join();
            t3.Join();
            t4.Join();
            t5.Join();

            if (flush)
                db.Flush();

            //ensure correct number of records (original set)
            Assert.IsEqual<int>(db.Count(), assets.Length);
            //ensure each record has unique / distinct Id
            Assert.IsEqual<int>(db.Query().SelectDistinct(a => a.Id).Count(), assets.Length);
            //ensure min id is 1
            Assert.IsEqual<uint>(db.Query().Min(a => a.Id), 1);
            //ensure max id is total record length
            Assert.IsEqual<uint>(db.Query().Max(a => a.Id), (uint)assets.Length);
            //ensure all updates actually happened
            Assert.IsEqual<int>(db.Count(a => a.XXHash == 2), assets.Length);
            //ensure the final 1000 inserted after initial set were deleted.
            Assert.IsEqual<int>(deleted, assets11.Length);
        }
        #endregion
    }
}
