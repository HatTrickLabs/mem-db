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

        public LargeVolumeTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset/*, _dbPath*/)
                //.SetMode(AccessMode.AppendOnly)
                //.SetFlushInterval(0)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .IndexOnIdentity(true)
                //.EncryptWithPassword(() => "This is a super fancy and complex password!!!!!")
                .Register();
        }

        #region load db
        protected void LoadDb(MemDb<DigitalAsset> db)
        {
            DigitalAsset[] assets = base.ResolveAssetSet();
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                //db.Insert(asset, (id) => asset.Id = id, (i % 100) == 0);
                db.Insert(asset, (id) => asset.Id = id);
            }
        }
        #endregion

        #region large volume
        public void Test_LargeVolume()
        {
            int iterations = 10_000;
            int setCount = base.ResolveAssetSet().Length;
            Stopwatch sw = new Stopwatch();
            Console.WriteLine($"Starting load of  {iterations * setCount} into new database.");
            sw.Start();
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                for (int i = 0; i < iterations; i++)
                {
                    this.LoadDb(db);
                }

                sw.Stop();
                Console.WriteLine($"{sw.ElapsedMilliseconds}\tCompleted db load (in mem only) of {iterations * setCount} records.");
                sw.Reset();
                Console.WriteLine("Starting concurrent queries for 100,000 records by id (index assisted)");
                DigitalAsset[] assets = new DigitalAsset[100_000];
                sw.Start();
                Parallel.For(0, 100_000, (i) => {
                    assets[i] = db.Find((uint)i + 1);
                });
                sw.Stop();
                Console.WriteLine($"{sw.ElapsedMilliseconds}\tCompleted concurrent queries for 100,000 records");
                sw.Reset();

                for (int i = 0; i < assets.Length; i++)
                {
                    Assert.IsNotNull(assets[i]);
                    Assert.IsEqual(assets[i].Id, (uint)i + 1);
                }
            }            

            Console.WriteLine("Done...Press [Enter] to exit.");
            Console.ReadLine();
        }
        #endregion
    }
}
