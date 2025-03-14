using System;
using System.IO;

namespace HatTrick.InMemDb.TestHarness
{
    public class AESEncryptionTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = @"..\..\..\..\_db\aes_key_encrypted";
        #endregion

        #region interface
        protected string DbPath => _dbPath;
        protected string Dataset => _dataset;
        #endregion

        #region ctors
        public AESEncryptionTests(AssetResolver assetResolver) : base(_dataset, _dbPath, assetResolver)
        {

        }
        #endregion

        #region insert encrypted records
        public void Test_InsertEncryptedRecords()
        {
            using var db = MemDb.Open<DigitalAsset>(_dataset);
            DigitalAsset[] assets = base.ResolveAssetSet();
            for (int i = 0; i < assets.Length; i++)
            {
                db.Insert(assets[i], true);
            }
            db.Flush();
        }
        #endregion

        #region insert encrypted records
        public void Test_InsertCloseReopenEncryptedRecords()
        {
            int jsonCnt = 0;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                DigitalAsset[] assets = base.ResolveAssetSet();
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i], true);
                    if (assets[i].AssetType == DigitalAssetType.Json)
                        jsonCnt += 1;
                }
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var asset500 = db.Find(a => a.Name.StartsWith("0500"));
                Assert.IsNotNull(asset500);
                var jsonAsset = db.FindAll(a => a.AssetType == DigitalAssetType.Json);
                Assert.IsEqual<int>(jsonCnt, jsonAsset.Length);
            }
        }
        #endregion

        #region intermingle encrypted and unencrypted records
        public void Test_Intermingle_EncryptedAndUnencryptedRecords()
        {
            int jsonCnt = 0;
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                DigitalAsset[] assets = base.ResolveAssetSet();
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i], (i % 2) == 0 ? true : false);
                    if (assets[i].AssetType == DigitalAssetType.Json)
                        jsonCnt += 1;
                }
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var asset500 = db.Find(a => a.Name.StartsWith("0500"));
                Assert.IsNotNull(asset500);
                var asset501 = db.Find(a => a.Name.StartsWith("0501"));
                Assert.IsNotNull(asset501);
                var asset502 = db.Find(a => a.Name.StartsWith("0502"));
                Assert.IsNotNull(asset502);
                var jsonAsset = db.FindAll(a => a.AssetType == DigitalAssetType.Json);
                Assert.IsEqual<int>(jsonCnt, jsonAsset.Length);
            }
        }
        #endregion
    }
}
