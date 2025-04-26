using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HatTrick.InMemDb.TestHarness
{
    //TDDO: what do we really want to test here ???
    internal class LargeVolumeTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "large_volume");
        #endregion

        public LargeVolumeTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .SetMode(AccessMode.AppendOnly)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .EncryptWithPassword(() => "This is a super fancy and complex password!!!!!")
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
            }
            sw.Stop();
            Console.WriteLine($"{sw.ElapsedMilliseconds}\tCompleted insert, flush to disk and close db after insert of {iterations * setCount} records.");


            MemDb.RemoveConfiguationFor(_dataset);
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .SetMode(AccessMode.ReadOnly)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .EncryptWithPassword(() => "This is a super fancy and complex password!!!!!")
                .Register();

            sw.Reset();
            Console.WriteLine($"Starting re-open and hydrate of {iterations * setCount} record db into RAM.");
            sw.Start();
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                sw.Stop();
                Console.WriteLine($"{sw.ElapsedMilliseconds}\tCompleted re-open and hydrate {iterations * setCount} records");

                sw.Reset();
                Console.WriteLine("Starting query for all records with .json extension");
                sw.Start();
                var json = db.FindAll(a => string.Compare(a.Extension, ".json", true) == 0);
                sw.Stop();
                Console.WriteLine($"{sw.ElapsedMilliseconds}\tResolved and cloned {json.Length} json assets.");
                sw.Reset();
                Console.WriteLine("Starting query for grouping of records by extension.");
                sw.Start();
                var groups = db.Query().GroupBy(a => a.Extension).Select(g => (g.Key, g.Count())).ToArray();
                sw.Stop();
                Console.WriteLine($"{sw.ElapsedMilliseconds}\tResolved {groups.Length} groups: {string.Join(',', groups.ToList().ConvertAll<string>(g => $"{g.Key}:{g.Item2}"))} ");


            }

            Console.WriteLine("Done...Press [Enter] to exit.");
            Console.ReadLine();
        }
        #endregion
    }
}
