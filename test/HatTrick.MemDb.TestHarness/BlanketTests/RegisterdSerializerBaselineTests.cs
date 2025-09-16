using System;
using System.IO;

namespace HatTrick.Data.TestHarness
{
    public class RegisterdSerializerBaselineTests : BaselineTests
    {
        #region internals
        private DigitalAssetBinarySerializer _serializer;
        #endregion

        #region ctors
        public RegisterdSerializerBaselineTests(AssetResolver assetResolver) : base(assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(base.Dataset, base.DbPath)
                .SerializeWith(() => 
                    {
                        return _serializer ?? (_serializer = new DigitalAssetBinarySerializer());
                    })
                .Register();
        }
        #endregion

        #region ensure registered serializer utilized
        public void Test_EnsureRegisteredSerializerUtilized()
        {
            using var db = MemDb.Open<DigitalAsset>(base.Dataset);
            base.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            db.Flush();

            //each inserted record will be deep copied 1 time with the serializer before added to the in memory cache
            //each record will be serailized a second time when flushed to disk (hence the above db.flush())
            Assert.IsEqual(_serializer.SerializeCount, (txtCnt + jsonCnt + unknownCnt) * 2);

            //each inserted record will be deserialized 1 time during the deep copy process
            Assert.IsEqual(_serializer.DeserializeCount, (txtCnt + jsonCnt + unknownCnt));
        }
        #endregion
    }
}
