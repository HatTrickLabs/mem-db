// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

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
