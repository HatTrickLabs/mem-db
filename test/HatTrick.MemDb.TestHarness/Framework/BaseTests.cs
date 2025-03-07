using System;
using System.IO;

namespace HatTrick.InMemDb.TestHarness
{
    public abstract class BaseTests
    {
        #region internals
        private string _dbPath;
        private string _dataset;
        private AssetResolver _assetResolver;
        private Failure[] _failures;
        #endregion

        #region interface
        public bool HasFailures => _failures is not null;

        public Failure[] Failures => _failures is null ? Array.Empty<Failure>() : _failures;
        #endregion

        #region ctors
        public BaseTests(string dbPath, string datasetName, AssetResolver assetResolver)
        {
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
            _dataset = datasetName ?? throw new ArgumentNullException(nameof(datasetName));
            _assetResolver = assetResolver ?? throw new ArgumentNullException(nameof(assetResolver));
        }
        #endregion

        #region go
        public void Go()
        {
            Executor exe = new Executor();
            exe.Execute(this);
            if (exe.HasFailures)
                _failures = exe.GetFailures();
        }
        #endregion

        #region resolve asset set
        protected DigitalAsset[] ResolveAssetSet()
        {
            return _assetResolver.ResolveAssets();
        }
        #endregion

        #region cleanup
        public void Cleanup()
        {
            this.DeleteDbFiles();
        }
        #endregion

        #region delete db files
        private void DeleteDbFiles()
        {
            string map = Path.Combine(_dbPath, "htl." + _dataset + ".map");
            string db =  Path.Combine(_dbPath, "htl." + _dataset + ".db");

            if (File.Exists(map))
                File.Delete(map);

            if (File.Exists(db))
                File.Delete(db);
        }
        #endregion
    }
}
