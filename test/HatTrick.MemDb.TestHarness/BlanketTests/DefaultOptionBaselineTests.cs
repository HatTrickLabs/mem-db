using System;

namespace HatTrick.InMemDb.TestHarness
{
    public class DefaultOptionBaselineTests : BaselineTests
    {
        #region ctors
        public DefaultOptionBaselineTests(AssetResolver assetResolver) : base(assetResolver)
        {
            MemDb.ConfigureFor<DigitalAsset>(base.Dataset, base.DbPath).Register();
        }
        #endregion
    }
}
