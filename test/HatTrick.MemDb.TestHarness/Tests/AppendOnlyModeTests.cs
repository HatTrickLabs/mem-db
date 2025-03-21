using System;

namespace HatTrick.InMemDb.TestHarness
{
    public class AppendOnlyModeTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = @"..\..\..\..\_db\appendonly_mode";
        #endregion

        #region ctors
        public AppendOnlyModeTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
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

        #region append only allows insert and throws on read update and delete
        public void Test_AppendOnlyAllowsInsertAndThrowsOnReadUpdateAndDelete()
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
            MemDb.RemoveConfiguationFor(_dataset);

            //configure for appendonly
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath).SetMode(AccessMode.AppendOnly).Register();

            //re-open the db in append mode
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                //should allow a non predicated count
                int recCount = db.Count();

                var newAsset = DigitalAsset.CreateNew(DigitalAssetType.Text);
                newAsset.Name = "xxx.txt";
                newAsset.Directory = @"c:\";
                newAsset.Imported = DateTime.Now;
                newAsset.Created = DateTime.Now;
                newAsset.LastWrite = DateTime.Now;
                newAsset.Length = 1;

                //should allow an insert
                db.Insert(newAsset);

                db.Flush();

                Assert.Throws<InvalidOperationException>(
                    when: () => { var a = db.Find(a => a.Name.StartsWith("0001")); },
                    messageContains: $"'{nameof(AccessMode.AppendOnly)}' mode"
                );

                Assert.Throws<InvalidOperationException>(
                    when: () => { var a = db.FindAll(a => true); },
                    messageContains: $"'{nameof(AccessMode.AppendOnly)}' mode"
                );

                Assert.Throws<InvalidOperationException>(
                    when: () => db.Update(a => a.XXHash = 1, (a) => a.Name.StartsWith("0001")),
                    messageContains: $"'{nameof(AccessMode.AppendOnly)}' mode"
                );

                Assert.Throws<InvalidOperationException>(
                    when: () => db.Delete((a) => a.Name.StartsWith("0001")),
                    messageContains: $"'{nameof(AccessMode.AppendOnly)}' mode"
                );
            }

            //remove the append only configuration
            MemDb.RemoveConfiguationFor(_dataset);

            //configure for readonly
            MemDb.ConfigureFor<DigitalAsset>(_dataset, _dbPath).SetMode(AccessMode.ReadOnly).Register();

            //re-open the db in read only mode and ensure the record inserted above was persisted.
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var a = db.Find(a => a.Name == "xxx.txt");
                Assert.IsNotNull(a);
            }
        }
        #endregion
    }
}
