using System;
using System.IO;
using System.Linq;

namespace HatTrick.Data.TestHarness
{
    public abstract class AESEncryptionTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = Path.Combine(TestBase.DbBasePath, "aes_encryption");
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
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                DigitalAsset[] assets = base.ResolveAssetSet();
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i], true);
                }
                db.Flush();
            }
        }
        #endregion

        #region insert close reopen encrypted records
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

        #region update on intermingled encrypted and unencrypted records
        public void Test_UpdateOnIntermingledEncrypedAndUnencryptedRecords()
        {
            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                DigitalAsset[] assets = base.ResolveAssetSet();
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i], (i % 2) == 0 ? true : false);
                }
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                //update all records set xxhash = 100 for even ids and 200 for odd
                db.Update(
                    apply: a => a.XXHash = a.Id % 2 == 0 ? (ulong)100 : (ulong)200, 
                    where: a => true
                );
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var even = db.FindAll(a => a.Id % 2 == 0);
                Assert.IsEqual(even.All(a => a.XXHash == 100), true);
                //Assert.TrueForAll<DigitalAsset>(even, a => a.XXHash == 100);

                var odd = db.FindAll(a => a.Id % 2 == 1);
                Assert.IsEqual(odd.All(a => a.XXHash == 200), true);
                //Assert.TrueForAll<DigitalAsset>(odd, a => a.XXHash == 200);
            }
        }
        #endregion
    }
}
