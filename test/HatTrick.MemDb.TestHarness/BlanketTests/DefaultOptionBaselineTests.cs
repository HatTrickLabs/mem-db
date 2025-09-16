using System;
using System.IO;

namespace HatTrick.Data.TestHarness
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
