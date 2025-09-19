using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HatTrick.Data.TestHarness
{
    //TDDO: what do we really want to test here ???
    public class LargeVolumeTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "large_volume");

        private Stopwatch _sw = new Stopwatch();
        #endregion

        #region ctor
        public LargeVolumeTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset/*, _dbPath*/)
                //.SetMode(AccessMode.AppendOnly)
                //.SetFlushInterval(0)
                .CloneWith(() => new DigitalAssetCloner())
                //.SerializeWith(() => new DigitalAssetBinarySerializer())
                .IndexOnIdentity(true)
                .ApplyIndex<string>(nameof(DigitalAsset.Name), (a) => a.Name)
                .ApplyIndex<long>(nameof(DigitalAsset.Id), (a) => a.Id)
                .ApplyIndex<string>(
                    name: nameof(DigitalAsset.Directory), 
                    keyResolver: (a) => a.Directory, 
                    comparer: new MemDbComparer<string>(StringComparer.CurrentCultureIgnoreCase, StringComparer.CurrentCultureIgnoreCase)
                )
                //.EncryptWithPassword(() => "This is a super fancy and complex password!!!!!")
                .Register();
        }
        #endregion

        #region load db
        protected void LoadDb(MemDb<DigitalAsset> db, DigitalAsset[] assets)
        {
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                db.Insert(asset, (id) => asset.Id = id, false);
            }
        }
        #endregion

        #region large volume
        public void Test_LargeVolume()
        {
            int iterations = 10_000;
            DigitalAsset[] assets = base.ResolveAssetSet();
            int total = iterations * assets.Length;
            Console.WriteLine($"Starting load of {total:n0} records into new database.");
            _sw.Start();
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                for (int i = 0; i < iterations; i++)
                {
                    this.LoadDb(db, assets);
                }
                _sw.Stop();
                Console.WriteLine($"{_sw.ElapsedMilliseconds}\tCompleted db load of {total:n0} records.");

                _sw.Reset();
                _sw.Start();
                DigitalAsset[] result = null;
                Parallel.For(0, 100, (a) =>
                {
                    result = db.QueryViaIndex<long>(nameof(DigitalAsset.Id)).IsBetween(180_001, 200_000).ToArray();
                });
                _sw.Stop();
                Console.WriteLine($"{result.Length} records in {_sw.ElapsedMilliseconds} milliseconds...");

                _sw.Reset();
                _sw.Start();
                DigitalAsset[] result2 = null;
                Parallel.For(0, 100, (a) =>
                {
                    //result2 = db.Query().Where(a => a.Id >= 180_001 && a.Id <= 200_000).ToArray();
                    result2 = db.FindAll(a => a.Id >= 180_001 && a.Id <= 200_000).ToArray();
                });
                _sw.Stop();
                Console.WriteLine($"{result2.Length} records in {_sw.ElapsedMilliseconds} milliseconds...");

                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true);

                //this.ConcurrentQueriesOnAppliedIdIndexAssisted(db, 10_000);
                //this.QueryByIdNaturalIndexAssisted(db, 10_000);
                //var withIndex = this.ConcurrentQueriesOnAppliedNameIndexAssisted(db, 100);
                //var noIndex = this.ConcurrentQueriesOnNameWithoutIndex(db, 10);
                //this.ConcurrentQueriesOnAppliedDirectoryIndexAssisted(db, 10);

                Console.WriteLine("Done...Press [Enter] to exit.");
                Console.ReadLine();
            }
        }
        #endregion

        #region query by id natural index assisted
        private void QueryByIdNaturalIndexAssisted(MemDb<DigitalAsset> db, int iterations)
        {
            _sw.Reset();

            Console.WriteLine($"Starting concurrent queries for {iterations:n0} records by id (natural index assisted)");
            DigitalAsset[] assets = new DigitalAsset[iterations];
            _sw.Start();
            Parallel.For(0, iterations, (i) =>
            {
                assets[i] = db.Find((long)i + 125_000);
            });
            _sw.Stop();
            Console.WriteLine($"{_sw.ElapsedMilliseconds}\tCompleted concurrent queries for {iterations:n0} records");

            //ensure we got the expected output...
            for (int i = 0; i < iterations; i++)
            {
                Assert.IsNotNull(assets[i]);
                Assert.IsEqual(assets[i].Id, i + 125_000);
            }
        }
        #endregion

        #region concurrent queries on applied id index assisted
        private void ConcurrentQueriesOnAppliedIdIndexAssisted(MemDb<DigitalAsset> db, int iterations)
        {
            _sw.Reset();

            Console.WriteLine($"Starting concurrent queries for {iterations:n0} records by id (applied index assisted)");
            DigitalAsset[] assets = new DigitalAsset[iterations];
            _sw.Start();
            Parallel.For(0, iterations, (i) =>
            {
                assets[i] = db.QueryViaIndex<long>(nameof(DigitalAsset.Id)).IsEqualTo(i + 125_000).ToArray()[0];
            });
            _sw.Stop();
            Console.WriteLine($"{_sw.ElapsedMilliseconds}\tCompleted concurrent queries for {iterations:n0} records");

            //ensure we got the expected output...
            for (int i = 0; i < iterations; i++)
            {
                Assert.IsNotNull(assets[i]);
                Assert.IsEqual(assets[i].Id, i + 125_000);
            }
        }
        #endregion

        #region calculate total sum of all asset lengths
        private void CalculateTotalSumOfAllAssetLengthsGroupedByExtension(MemDb<DigitalAsset> db, int totalAssets)
        {
            _sw.Reset();

            Console.WriteLine($"Kicking off calculate total sum of all asset lengths");
            _sw.Start();
            var sets = db.Query()
                .GroupBy(a => a.Extension).Select(g => (g.Key, g.Count()))
                .ToArray();
            _sw.Stop();
            Console.WriteLine($"{_sw.ElapsedMilliseconds}\tCalcuated total sum of file lengths for {totalAssets:n0} records .");

            Console.WriteLine("Asset Lengths Grouped By Extension");
            Console.WriteLine("----------------------------------");
            foreach (var s in sets)
            {
                Console.WriteLine($"{s.Key}\t{s.Item2}");
            }
        }
        #endregion

        #region concurrent queries on name without index
        private DigitalAsset[] ConcurrentQueriesOnNameWithoutIndex(MemDb<DigitalAsset> db, int iterations)
        {
            _sw.Reset();

            Console.WriteLine($"Kicking off {iterations} concurrent queries for name >= '0950' Skip(500).Limit(250) WITHOUT index...");
            DigitalAsset[] noIndex = null;
            _sw.Start();
            Parallel.For(0, iterations, (i) =>
            {
                noIndex = db.Query()
                .Where(a => string.Compare(a.Name, "0950", false) >= 0)
                .OrderBy((a, b) => b.Id.CompareTo(a.Id))
                .Skip(500)
                .Limit(250)
                .ToArray();
            });
            _sw.Stop();
            Console.WriteLine($"{_sw.ElapsedMilliseconds}\tqueried for all files name >= '0950' Skip(500).Limit(250) WITHOUT index {noIndex.Length:n0}");
            return noIndex;
        }
        #endregion

        #region concurrent queries on applied name index assisted
        private DigitalAsset[] ConcurrentQueriesOnAppliedNameIndexAssisted(MemDb<DigitalAsset> db, int iterations)
        {
            _sw.Reset();

            Console.WriteLine($"Kicking off {iterations:n0} concurrent queries for name >= '0950' Skip(500).Limit(250) (applied index assisted)...");
            _sw.Start();
            DigitalAsset[] withIndex = null;
            Parallel.For(0, iterations, (i) =>
            {
                withIndex = db.QueryViaIndex<string>(nameof(DigitalAsset.Name))
                .IsGreaterThanEqualTo("0950")
                .OrderBy((a,b) => b.Id.CompareTo(a.Id))
                .Skip(500)
                .Limit(250)
                .ToArray();
            });
            _sw.Stop();
            Console.WriteLine($"{_sw.ElapsedMilliseconds}\tqueried for all files name >= '0950' Skip(500).Limit(250) WITH index {withIndex.Length:n0}");
            return withIndex;
        }
        #endregion

        #region concurrent queries on applied directory index assisted
        private DigitalAsset[] ConcurrentQueriesOnAppliedDirectoryIndexAssisted(MemDb<DigitalAsset> db, int iterations)
        {
            _sw.Reset();

            Console.WriteLine($"Kicking off {iterations:n0} concurrent queries for Directory less than equal (applied index assisted)...");
            _sw.Start();
            (string, int)[] withIndex = default;
            Parallel.For(0, iterations, (i) =>
            {
                withIndex = db.QueryViaIndex<string>(nameof(DigitalAsset.Directory))
                .IsLessThanEqualTo(@"d:\GIT\HatTrickLabs\mem-db\test\assets\c")
                .GroupBy(a => a.Directory)
                .Select(g => (g.Key, g.Count()));

            });
            _sw.Stop();
            Console.WriteLine($"{ _sw.ElapsedMilliseconds}\tqueried for Directory less than equal WITH index key {withIndex[0].Item1} {withIndex[0].Item2:n0}");
            return null;
        }
        #endregion

        #region flush to disk
        private void FlushToDisk(MemDb<DigitalAsset> db, int totalAssets)
        {
            _sw.Reset();

            Console.WriteLine($"Kicking off flush to disk for {totalAssets:n0} assets.");
            _sw.Start();
            db.Flush();
            _sw.Stop();
            Console.WriteLine($"{_sw.ElapsedMilliseconds}\tFlushed {totalAssets:n0} records to disk.");
        }
        #endregion

        #region re open memdb instance from disk
        private void ReOpenMemDbInstanceFromDisk(int totalAssets)
        {
            _sw.Reset();

            Console.WriteLine($"Kicking off re-open of MemDb instance containing {totalAssets:n0}");
            _sw.Start();
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                _sw.Stop();
                Console.WriteLine($"{_sw.ElapsedMilliseconds}\tReopened {totalAssets:n0} records.");
            }
        }
        #endregion
    }
}
