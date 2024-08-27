using System;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    public class DigitalAssetCloner : IMemDbCloner<DigitalAsset>
    {
        public DigitalAsset DeepCopy(DigitalAsset value)
        {
            var asset = new DigitalAsset();
            asset.Id = value.Id;
            asset.Name = value.Name;
            asset.Directory = value.Directory;
            asset.Created = value.Created;
            asset.LastAccess = value.LastAccess;
            asset.LastWrite = value.LastWrite;
            asset.Length = value.Length;
            asset.XXHash = value.XXHash;
            asset.Imported = value.Imported;

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
    }
}
