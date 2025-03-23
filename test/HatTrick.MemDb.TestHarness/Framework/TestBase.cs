using System;
using System.IO;
using System.Collections.Generic;

namespace HatTrick.InMemDb.TestHarness
{
    public abstract class TestBase
    {
        #region internals
        private static readonly string _dbBasePath = Path.Combine("..", "..", "..", "..", "_db");
        private string _dataset;
        private string _dbPath;
        private AssetResolver _assetResolver;
        private Failure[] _failures;
        #endregion

        #region interface
        public static string DbBasePath => _dbBasePath;

        public bool HasFailures => _failures is not null;

        public Failure[] Failures => _failures is null ? Array.Empty<Failure>() : _failures;
        #endregion

        #region ctors
        public TestBase(string datasetName, string dbPath, AssetResolver assetResolver)
        {
            _dataset = datasetName ?? throw new ArgumentNullException(nameof(datasetName));
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
            _assetResolver = assetResolver ?? throw new ArgumentNullException(nameof(assetResolver));
        }
        #endregion

        #region go
        public void Go(ref List<Failure> failures, out int count)
        {
            Executor exe = new Executor();
            exe.Execute(this, out count);
            if (exe.HasFailures)
            {
                _failures = exe.GetFailures();
                failures.AddRange(_failures);
            }

            MemDb.RemoveConfiguationFor(_dataset);
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
            //try
            //{
                string map = Path.Combine(_dbPath, "htl." + _dataset + ".map");
                string db = Path.Combine(_dbPath, "htl." + _dataset + ".db");

                if (File.Exists(map))
                    File.Delete(map);

                if (File.Exists(db))
                    File.Delete(db);
            //}
            //catch (IOException)
            //{}
        }
        #endregion
    }
}
