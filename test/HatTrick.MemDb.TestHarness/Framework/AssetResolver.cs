using System;
using System.IO;
using System.Threading;

namespace HatTrick.InMemDb.TestHarness
{
    public class AssetResolver
    {
        #region internals
        private readonly string _path = @"..\..\..\..\assets";
        private Lock _lock;
        private DigitalAsset[] _assets;
        #endregion

        #region ctors
        public AssetResolver()
        {
            //NOTE: should be 500 txt...400 json...100 unknown
            _lock = new();
        }
        #endregion

        #region resolve test assets
        public DigitalAsset[] ResolveAssets()
        {
            lock (_lock)
            {
                if (_assets is null)
                    _assets = this.ReadAssets();
            }

            DateTime now = DateTime.Now;

            var output = new DigitalAsset[_assets.Length];
            for (int i = 0; i < _assets.Length; i++)
            {
                var original = _assets[i];
                var clone = DigitalAsset.CreateNew(original.AssetType);
                clone.Name = original.Name;
                clone.Directory = original.Directory;
                clone.Created = original.Created;
                clone.LastAccess = original.LastAccess;
                clone.LastWrite = original.LastWrite;
                clone.Length = original.Length;
                clone.Imported = now;

                output[i] = clone;
            }

            return output;
        }
        #endregion

        #region read assets
        private DigitalAsset[] ReadAssets()
        {
            var ops = new EnumerationOptions();
            ops.AttributesToSkip = FileAttributes.System | FileAttributes.Temporary;
            ops.IgnoreInaccessible = true;
            ops.ReturnSpecialDirectories = false;
            ops.RecurseSubdirectories = true;
            ops.MatchType = MatchType.Simple;

            string absolute = Path.GetFullPath(_path);
            string[] files = Directory.GetFiles(absolute, "*", ops);

            var assets = new DigitalAsset[files.Length];

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];

                FileInfo fi = new FileInfo(file);
                DigitalAsset asset = DigitalAsset.CreateNew(fi.Extension);
                asset.Name = fi.Name;
                asset.Directory = Path.GetDirectoryName(file);
                asset.Created = fi.CreationTime;
                asset.LastAccess = fi.LastAccessTime;
                asset.LastWrite = fi.LastWriteTime;
                asset.Length = fi.Length;
                //asset.Imported = ???;

                assets[i] = asset;
            }

            return assets;
        }
        #endregion
    }
}
