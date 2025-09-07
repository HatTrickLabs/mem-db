using System;
using System.IO;
using System.Threading;

namespace HatTrick.InMemDb.TestHarness
{
    public class ConcurrencyPriorityTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "concurrency_priority");
        #endregion

        #region ctors
        public ConcurrencyPriorityTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
              .CloneWith(() => new DigitalAssetCloner())
              .SerializeWith(() => new DigitalAssetBinarySerializer())
              .Register();
        }
        #endregion

        #region update on tight insert loop
        public void Test_UpdateOnTightInsertLoop()
        {
            var assets = new DigitalAsset[100_000];

            for (int i = 0; i < 100; i++)
            {
                Array.Copy(base.ResolveAssetSet(), 0, assets, (i * 1_000), 1_000);
            }

            int updateCnt = 0;
            int totalCnt = 0;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Thread tInsert = new Thread(() => 
                {
                    for (int i = 0; i < assets.Length; i++)
                    {
                        db.Insert(assets[i], (id) => assets[i].Id = id);
                    }
                });
                Thread tUpdate = new Thread(() => 
                {
                    int cnt = 0;
                    do
                    {
                        cnt = db.Count();
                    }
                    while (cnt < 1_000);
                    updateCnt = db.Update(a => a.XXHash = 3, a => a.Id % 2 == 0);
                });

                tInsert.Start();
                tUpdate.Start();

                tUpdate.Join();
                tInsert.Join();

                totalCnt = db.Count();
                //ensure the tight insert loop does not block the update from executing on another thread
                Assert.IsNotEqual(updateCnt, 0);
                Assert.IsNotEqual(totalCnt, updateCnt);
                Console.WriteLine(updateCnt);
            }
        }
        #endregion
    }
}
