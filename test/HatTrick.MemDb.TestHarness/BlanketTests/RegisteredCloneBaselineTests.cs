using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb.TestHarness
{
    public class RegisteredCloneBaselineTests : BaselineTests
    {
        #region internals
        private DigitalAssetCloner _cloner;
        #endregion

        #region ctors
        public RegisteredCloneBaselineTests(AssetResolver assetResolver) : base(assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(base.Dataset, base.DbPath)
                .CloneWith(() => { _cloner = new DigitalAssetCloner(); return _cloner; })
                .Register();
        }
        #endregion

        #region ensure registered cloner utilized
        public void Test_EnsureRegisteredClonerUtilized()
        {
            using var db = MemDb.Open<DigitalAsset>(base.Dataset);
            base.LoadDb(db, out int txtCnt, out int jsonCnt, out int unknownCnt);

            //each inserted record is deep copied 1 time before the rec is inserted into the in memory cache...
            Assert.IsEqual(_cloner.DeepCopyCount, (txtCnt + jsonCnt + unknownCnt));
        }
        #endregion
    }
}
