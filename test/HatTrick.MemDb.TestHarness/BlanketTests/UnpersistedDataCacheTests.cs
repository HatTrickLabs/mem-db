using System;

namespace HatTrick.InMemDb.TestHarness
{
    public class UnPersistedDataCacheTests : TestBase
    {
        #region internals
        private static readonly string _dataset = $"assets";
        private static readonly string _dbPath = null;
        #endregion

        #region ctors
        public UnPersistedDataCacheTests(AssetResolver assetResolver) 
            : base(_dataset, _dbPath, assetResolver)
        {
        }
        #endregion

        #region throws on flush interval configuration attempt
        public void Test_ThrowsOnFlushIntervalConfigurationAttempt()
        {
            Assert.Throws<InvalidOperationException>(
                when: () => {
                    MemDb.ConfigureFor<DigitalAsset>(_dataset)
                        .SetFlushInterval(10)
                        .Register();
                },
                messageContains: "Flush interval is not applicable"
            );

            Assert.Throws<InvalidOperationException>(
                when: () => {
                    MemDb.ConfigureFor<DigitalAsset>(_dataset)
                        .SetFlushInterval(0)
                        .Register();
                },
                messageContains: "Flush interval is not applicable"
            );
        }
        #endregion

        #region throws on read only mode configuration attempt
        public void Test_ThrowsOnReadOnlyModeConfiguationAttempt()
        {
            Assert.Throws<InvalidOperationException>(
                when: () => {
                    MemDb.ConfigureFor<DigitalAsset>(_dataset)
                        .SetMode(AccessMode.ReadOnly)
                        .Register();
                },
                messageContains: $"{AccessMode.ReadOnly} is inconsistent with a unpersisted database"
            );

            Assert.Throws<InvalidOperationException>(
                when: () => {
                    MemDb.ConfigureFor<DigitalAsset>(_dataset, "")
                        .SetFlushInterval(10)
                        .SetMode(AccessMode.ReadOnly)
                        .Register();
                },
                messageContains: $"{AccessMode.ReadOnly} is inconsistent with with a flush interval greater than 0"
            );
        }
        #endregion

        #region throws on append only mode configuration attempt
        public void Test_ThrowsOnAppendOnlyModeConfiguationAttempt()
        {
            Assert.Throws<InvalidOperationException>(
                when: () =>
                {
                    MemDb.ConfigureFor<DigitalAsset>(_dataset)
                        .SetMode(AccessMode.AppendOnly)
                        .Register();
                },
                messageContains: $"{AccessMode.AppendOnly} is inconsistent with a unpersisted database"
            );
        }
        #endregion

        #region loads
        public void Test_Loads()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset).Register();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var assets = base.ResolveAssetSet();
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i]);
                }
                Assert.IsEqual(db.Count(), assets.Length);
            }

            MemDb.RemoveConfiguationFor(_dataset);
        }
        #endregion

        #region loads
        public void Test_GeneratesDistinctIncrementalCacheLevelIds()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset).Register();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var assets = base.ResolveAssetSet();
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i], id => assets[i].Id = id);
                }
                Assert.IsEqual(db.Count(), assets.Length);
                var ids = db.Query().SelectDistinct<uint>(a => a.Id);
                Assert.IsEqual(ids.Length, assets.Length);
                for (int i = 0; i < ids.Length; i++)
                {
                    Assert.IsEqual(ids[i], (uint)i + 1);
                }
            }

            MemDb.RemoveConfiguationFor(_dataset);
        }
        #endregion

        #region nothing persisted
        public void Test_NothingPersisted()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset).Register();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var assets = base.ResolveAssetSet();
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i]);
                }
                Assert.IsEqual(db.Count(), assets.Length);
            }

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                Assert.IsEqual(db.Count(), 0);
            }

            MemDb.RemoveConfiguationFor(_dataset);
        }
        #endregion

        #region can query
        public void Test_CanQuery()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset).Register();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var assets = base.ResolveAssetSet();
                int txtCnt = 0;
                int jsonCnt = 0;
                int unknownCnt = 0;
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i]);
                    if (assets[i].AssetType == DigitalAssetType.Text)
                        txtCnt += 1;

                    if (assets[i].AssetType == DigitalAssetType.Json)
                        jsonCnt += 1;

                    if (assets[i].AssetType == DigitalAssetType.Unknown)
                        unknownCnt += 1;
                }

                Assert.IsEqual(db.Count(a => a.Extension == string.Empty), unknownCnt);

                var json = db.FindAll(a => a.Extension == ".json");
                Assert.IsNotNull(json);
                Assert.IsEqual(json.Length, jsonCnt);

                var txt = db.Query().Where(a => a.Extension == ".txt").ToArray();
                Assert.IsNotNull(txt);
                Assert.IsEqual(txt.Length, txtCnt);
            }

            MemDb.RemoveConfiguationFor(_dataset);
        }
        #endregion

        #region can update
        public void Test_CanUpdate()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset).Register();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var assets = base.ResolveAssetSet();
                int txtCnt = 0;
                int jsonCnt = 0;
                int unknownCnt = 0;
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i]);
                    if (assets[i].AssetType == DigitalAssetType.Text)
                        txtCnt += 1;

                    if (assets[i].AssetType == DigitalAssetType.Json)
                        jsonCnt += 1;

                    if (assets[i].AssetType == DigitalAssetType.Unknown)
                        unknownCnt += 1;
                }

                db.Update(a => a.XXHash += 5, a => a.AssetType == DigitalAssetType.Json);

                Assert.IsEqual(db.Count(a => a.XXHash == 5), jsonCnt);
                Assert.IsEqual(db.Count(), txtCnt + jsonCnt + unknownCnt);
            }

            MemDb.RemoveConfiguationFor(_dataset);
        }
        #endregion

        #region can delete
        public void Test_CanDelete()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset).Register();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var assets = base.ResolveAssetSet();
                int txtCnt = 0;
                int jsonCnt = 0;
                int unknownCnt = 0;
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i]);
                    if (assets[i].AssetType == DigitalAssetType.Text)
                        txtCnt += 1;

                    if (assets[i].AssetType == DigitalAssetType.Json)
                        jsonCnt += 1;

                    if (assets[i].AssetType == DigitalAssetType.Unknown)
                        unknownCnt += 1;
                }

                db.Update(a => a.XXHash += 5, a => a.AssetType == DigitalAssetType.Json);
                db.Delete(a => a.XXHash == 5);

                Assert.IsEqual(db.Count(a => a.XXHash == 5), 0);
                Assert.IsEqual(db.Count(), txtCnt + unknownCnt);
            }

            MemDb.RemoveConfiguationFor(_dataset);
        }
        #endregion

        #region can override cloner
        public void Test_CanOverrideCloner()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset)
                .CloneWith(() => new DigitalAssetCloner())
                .Register();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var assets = base.ResolveAssetSet();
                int txtCnt = 0;
                int jsonCnt = 0;
                int unknownCnt = 0;
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i]);
                    if (assets[i].AssetType == DigitalAssetType.Text)
                        txtCnt += 1;

                    if (assets[i].AssetType == DigitalAssetType.Json)
                        jsonCnt += 1;

                    if (assets[i].AssetType == DigitalAssetType.Unknown)
                        unknownCnt += 1;
                }

                db.Update(a => a.XXHash += 5, a => a.AssetType == DigitalAssetType.Json);

                Assert.IsEqual(db.Count(a => a.XXHash == 5), jsonCnt);
                Assert.IsEqual(db.Count(), txtCnt + jsonCnt + unknownCnt);
            }

            MemDb.RemoveConfiguationFor(_dataset);
        }
        #endregion

        #region can override serializer
        public void Test_CanOverrideSerializer()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset)
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .Register();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var assets = base.ResolveAssetSet();
                int txtCnt = 0;
                int jsonCnt = 0;
                int unknownCnt = 0;
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i]);
                    if (assets[i].AssetType == DigitalAssetType.Text)
                        txtCnt += 1;

                    if (assets[i].AssetType == DigitalAssetType.Json)
                        jsonCnt += 1;

                    if (assets[i].AssetType == DigitalAssetType.Unknown)
                        unknownCnt += 1;
                }

                db.Update(a => a.XXHash += 5, a => a.AssetType == DigitalAssetType.Json);

                Assert.IsEqual(db.Count(a => a.XXHash == 5), jsonCnt);
                Assert.IsEqual(db.Count(), txtCnt + jsonCnt + unknownCnt);
            }

            MemDb.RemoveConfiguationFor(_dataset);
        }
        #endregion

        #region can override cloner and serializer
        public void Test_CanOverrideClonerAndSerializer()
        {
            MemDb.ConfigureFor<DigitalAsset>(_dataset)
                .CloneWith(() => new DigitalAssetCloner())
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .Register();

            using (var db = MemDb.Open<DigitalAsset>(_dataset))
            {
                var assets = base.ResolveAssetSet();
                int txtCnt = 0;
                int jsonCnt = 0;
                int unknownCnt = 0;
                for (int i = 0; i < assets.Length; i++)
                {
                    db.Insert(assets[i]);
                    if (assets[i].AssetType == DigitalAssetType.Text)
                        txtCnt += 1;

                    if (assets[i].AssetType == DigitalAssetType.Json)
                        jsonCnt += 1;

                    if (assets[i].AssetType == DigitalAssetType.Unknown)
                        unknownCnt += 1;
                }

                db.Update(a => a.XXHash += 5, a => a.AssetType == DigitalAssetType.Json);

                Assert.IsEqual(db.Count(a => a.XXHash == 5), jsonCnt);
                Assert.IsEqual(db.Count(), txtCnt + jsonCnt + unknownCnt);
            }

            MemDb.RemoveConfiguationFor(_dataset);
        }
        #endregion
    }
}
