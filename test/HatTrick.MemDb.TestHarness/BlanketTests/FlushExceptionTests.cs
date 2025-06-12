using System;
using System.IO;

namespace HatTrick.InMemDb.TestHarness
{
    internal class FlushExceptionTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "flush-exception");
        #endregion

        #region ctors
        public FlushExceptionTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath)
                .SetFlushInterval(5)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .EncryptWithPassword(() => "TestPasswordHere!!!...")
                .Register();
        }
        #endregion

        #region exception on flush inserts
        public void Test_ExceptionOnFlushInserts()
        {
            var assets = base.ResolveAssetSet();

            try
            {
                using (var db = MemDb.Open<DigitalAsset>(_dataset))
                {
                    try
                    {
                        for (int i = 0; i < assets.Length; i++)
                        {
                            db.Insert(assets[i], (id) => assets[i].Id = id, true);
                        }

                        Console.WriteLine("Pausing...Press [Enter] to continue.");
                        Console.ReadLine();
                        //db.Insert(assets[550], (id) => assets[550].Id = id);
                        var a = db.Find(300);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("...." + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                int cnt = db.Count();
                var set = db.FindAll(a => a.Id > 400);
            }
        }
        #endregion
    }
}
