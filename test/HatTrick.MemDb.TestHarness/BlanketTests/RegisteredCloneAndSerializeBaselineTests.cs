using System;

namespace HatTrick.InMemDb.TestHarness
{
    public class RegisteredCloneAndSerializeBaselineTests : BaselineTests
    {
        #region internals
        private DigitalAssetCloner _cloner;
        private DigitalAssetBinarySerializer _serializer;
        #endregion

        #region ctors
        public RegisteredCloneAndSerializeBaselineTests(AssetResolver assetResolver) : base(assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(base.Dataset, base.DbPath)
                .CloneWith(() => { _cloner = new DigitalAssetCloner(); return _cloner; })
                .SerializeWith(() => { _serializer = new DigitalAssetBinarySerializer(); return _serializer; })
                .Register();
        }
        #endregion

        #region ensure registered cloner and serializer utilized
        public void Test_EnsureRegisteredClonerAndSerializerUtilized()
        {
            using var db = MemDb.Open<DigitalAsset>(base.Dataset);
            base.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            db.Flush();

            //each inserted record is deep copied 1 time before the rec is inserted into the in memory cache...
            Assert.IsEqual(_cloner.DeepCopyCount, (txtCnt + jsonCnt + unknownCnt));

            //each record will be serailized when flushed to disk (hence the above db.flush())
            Assert.IsEqual(_serializer.SerializeCount, (txtCnt + jsonCnt + unknownCnt));
        }
        #endregion
    }
}
