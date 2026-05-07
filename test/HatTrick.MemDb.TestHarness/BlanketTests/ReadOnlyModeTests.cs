using System;
using System.IO;

namespace HatTrick.Data.TestHarness
{
    public class ReadOnlyModeTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "readonly_mode");
        #endregion

        #region ctors
        public ReadOnlyModeTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {
        }
        #endregion

        #region load db
        protected void LoadDb(MemDb<DigitalAsset> db)
        {
            DigitalAsset[] assets = base.ResolveAssetSet();
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                db.Insert(asset);
            }
        }
        #endregion

        #region read only allows read and throws on write
        public void Test_ReadOnlyAllowsReadAndThrowsOnWrite()
        {
            //configure for read/write
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath).Register();

            //open and load
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                this.LoadDb(db);
                //flush and close
            }
            
            //remove the read/write configuration
            MemDb.RemoveConfigurationFor(_dataset);

            //configure for readonly
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath).SetMode(AccessMode.ReadOnly).Register();

            //re-open the db in readonly mode
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var asset = db.Find(a => a.Name.StartsWith("0005"));
                Assert.IsNotNull(asset);

                var newAsset = DigitalAsset.CreateNew(DigitalAssetType.Text);
                newAsset.Name = "abc.txt";
                newAsset.Directory = @"c:\";
                newAsset.Imported = DateTime.Now;
                newAsset.Created = DateTime.Now;
                newAsset.LastWrite = DateTime.Now;
                newAsset.Length = 1;

                Assert.Throws<InvalidOperationException>(
                    when: () => db.Insert(newAsset), 
                    messageContains: $"'{nameof(AccessMode.ReadOnly)}' mode"
                );

                Assert.Throws<InvalidOperationException>(
                    when: () => db.Update(a => a.XXHash = 1, (a) => true), 
                    messageContains: $"'{nameof(AccessMode.ReadOnly)}' mode"
                );

                Assert.Throws<InvalidOperationException>(
                    when: () => db.Delete(a => true),
                    messageContains: $"'{nameof(AccessMode.ReadOnly)}' mode"
                );

            }
        }
        #endregion
    }
}
