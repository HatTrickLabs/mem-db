using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HatTrick.InMemDb.TestHarness
{
    //TDDO: what do we really want to test here ???
    public class LargeVolumeTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "large_volume");
        #endregion

        #region ctor
        public LargeVolumeTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                //.SetMode(AccessMode.AppendOnly)
                .SetFlushInterval(0)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .IndexOnIdentity(true)
                .ApplyIndex<string>(nameof(DigitalAsset.Name), (a) => a.Name)
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
            DigitalAsset[] loadAssets = base.ResolveAssetSet();
            int total = iterations * loadAssets.Length;
            Stopwatch sw = new Stopwatch();
            Console.WriteLine($"Starting load of {total:n0} records into new database.");
            sw.Start();
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                for (int i = 0; i < iterations; i++)
                {
                    this.LoadDb(db, loadAssets);
                }

                sw.Stop();
                Console.WriteLine($"{sw.ElapsedMilliseconds}\tCompleted db load (in mem only) of {total:n0} records.");
                sw.Reset();
                Console.WriteLine("Starting concurrent queries for 250,000 records by id (index assisted)");
                DigitalAsset[] assets = new DigitalAsset[250_000];
                sw.Start();
                Parallel.For(0, 250_000, (i) =>
                {

                    assets[i] = db.Find((long)i + 300_001);
                });
                sw.Stop();
                Console.WriteLine($"{sw.ElapsedMilliseconds}\tCompleted concurrent queries for 250,000 records");
                sw.Reset();

                sw.Start();
                var sets = db.Query()
                    .GroupBy(a => a.Extension).Select(g => (g.Key, g.Count()))
                    .ToArray();
                sw.Stop();
                Console.WriteLine($"{sw.ElapsedMilliseconds}\tCalcuated total sum of file lengths for {total:n0} records .");

                for (int i = 0; i < assets.Length; i++)
                {
                    Assert.IsNotNull(assets[i]);
                    Assert.IsEqual(assets[i].Id, (long)i + 300_001);
                }

                foreach (var s in sets)
                {
                    Console.WriteLine($"{s.Key}\t{s.Item2}");
                }

                sw.Reset();
                Console.WriteLine("Kicking off 50 concurrent queries for name >= '0950' WITHOUT index...");
                sw.Start();
                DigitalAsset[] noIndex = null;
                Parallel.For(0, 50, (i) =>
                {
                    noIndex = db.Query().Where(a => string.Compare("0950", a.Name, false) == 0).ToArray();
                });
                sw.Stop();
                Console.WriteLine($"{sw.ElapsedMilliseconds}\tqueried for all files name >= 0950 WITHOUT index {noIndex.Length:n0}");

                sw.Reset();
                Console.WriteLine("Kicking off 50 concurrent queries for name >= '0950' WITH index...");
                sw.Start();
                DigitalAsset[] withIndex = null;
                Parallel.For(0, 100, (i) =>
                {
                    withIndex = db.QueryViaIndex<string>(nameof(DigitalAsset.Name)).IsEqualTo("0950").ToArray();
                });
                sw.Stop();
                Console.WriteLine($"{sw.ElapsedMilliseconds}\tqueried for all files name >= 0950 WITH index {withIndex.Length:n0}");

                sw.Reset();
                Console.WriteLine("Kicking off flush");
                sw.Start();
                db.Flush();
                sw.Stop();
                Console.WriteLine($"{sw.ElapsedMilliseconds}\tFlushed {total:n0} records to disk.");
            }

            sw.Reset();
            Console.WriteLine("Kicking off re-open");
            sw.Start();
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                sw.Stop();
                Console.WriteLine($"{sw.ElapsedMilliseconds}\tReopened {total:n0} encrypted records.");
                var asset = db.Find(100);
            }
            
            Console.WriteLine("Done...Press [Enter] to exit.");
            Console.ReadLine();
        }
        #endregion
    }
}
