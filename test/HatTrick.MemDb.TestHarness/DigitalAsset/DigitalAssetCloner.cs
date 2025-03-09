using System;
using System.Threading;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    public class DigitalAssetCloner : IMemDbCloner<DigitalAsset>
    {
        #region internals
        private int _deepCopyCount;
        #endregion

        #region interface
        public int DeepCopyCount => _deepCopyCount;
        #endregion

        #region deep copy
        public DigitalAsset DeepCopy(DigitalAsset value)
        {
            var asset = DigitalAsset.CreateNew(value.AssetType);

            asset.Id = value.Id;
            asset.Name = value.Name;
            asset.Directory = value.Directory;
            asset.Created = value.Created;
            asset.LastAccess = value.LastAccess;
            asset.LastWrite = value.LastWrite;
            asset.Length = value.Length;
            asset.XXHash = value.XXHash;
            asset.Imported = value.Imported;

            Interlocked.Increment(ref _deepCopyCount);

            return asset;
        }

        public DigitalAsset[] DeepCopy(IList<DigitalAsset> values)
        {
            DigitalAsset[] assets = new DigitalAsset[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                DigitalAsset value = values[i];
                assets[i] = this.DeepCopy(value);
            }
            return assets;
        }
        #endregion
    }
}
