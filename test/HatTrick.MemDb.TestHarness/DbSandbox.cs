using System;
using System.Linq;
using System.IO;
using System.IO.Hashing;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using HatTrick.InMemDb;

namespace HatTrick.InMemDb.TestHarness
{
    public class DbSandbox
    {
        private Stopwatch _sw;
        private MemDb<DigitalAsset> _db;
        private string datasetName = "assets";
        private string DbRoot = @"D:\_db\dev\mem-db-dev-assets";

        #region go
        public void Go()
        {
            MemDb.ConfigureFor<DigitalAsset>(datasetName, DbRoot)
                .SerializeWith(() => new DigitalAssetBinarySerializer())
                .CloneWith(() => new DigitalAssetCloner())
                //.SerializeWith(() =>
                //{
                //    var serializer = MemDbJsonSerializer<IDigitalAsset>.GetInstance();
                //    serializer.ApplyConverterFor<IDigitalAsset>(new IDigitalAssetConverter<DigitalAsset>());
                //    return serializer;
                //})
                //.EncryptWithKey(() => new byte[] { 198, 1, 6, 8, 12, 1, 1, 1, 1, 88, 1, 1, 1, 1, 1, 9, 9, 9, 1, 1, 99, 1, 1, 1, 1, 1, 1, 1, 33, 1, 1, 77 })
                .EncryptWithPassword(() => "Jerrod's super simple password...!!!!!!!!")
                .SetMode(AccessMode.ReadWrite)
                .ArchiveOnDefrag(Path.Combine(DbRoot, "archive"))
                .Register();

            _sw = new Stopwatch();
            _sw.Start();

            using (_db = MemDb.Open<DigitalAsset>(datasetName))
            {
                _sw.Stop();
                Console.WriteLine("initialized " + _db.Count() + " records @ " + _sw.ElapsedMilliseconds + " milliseconds.");
                _sw.Start();

                //List<DigitalAsset> assets = new List<DigitalAsset>(24_000);
                //assets.AddRange(ResolveAssets(@"D:\tmp", DigitalAssetType.Doc));
                //assets.AddRange(ResolveAssets(@"C:\Users\jerrod.eiman\Pictures", DigitalAssetType.Image));
                //assets.AddRange(ResolveAssets(@"C:\Users\jerrod.eiman\Videos", DigitalAssetType.Video));
                //assets.AddRange(ResolveAssets(@"D:\svn", DigitalAssetType.Repo));

                //Console.WriteLine("Image: " + _db.Count(a => a.AssetType == DigitalAssetType.Image));
                //Console.WriteLine("Video: " + _db.Count(a => a.AssetType == DigitalAssetType.Video));
                //Console.WriteLine("Doc:   " + _db.Count(a => a.AssetType == DigitalAssetType.Doc));
                //Console.WriteLine("Repo:  " + _db.Count(a => a.AssetType == DigitalAssetType.Repo));

                //Console.WriteLine("Max Image Len: " + _db.Query().Where(a => a.AssetType == DigitalAssetType.Image).Max(a => a.Length));
                //Console.WriteLine("Max Video Len: " + _db.Query().Where(a => a.AssetType == DigitalAssetType.Video).Max(a => a.Length));
                //Console.WriteLine("Max Doc Len:   " + _db.Query().Where(a => a.AssetType == DigitalAssetType.Doc).Max(a => a.Length));
                //Console.WriteLine("Max Repo Len:  " + _db.Query().Where(a => a.AssetType == DigitalAssetType.Repo).Max(a => a.Length));

                //ConcurrentQueryTest(100);

                //_sw.Stop();
                //Console.WriteLine($"Resolved {assets.Count} assets @ {_sw.ElapsedMilliseconds}.");
                //_sw.Start();

                //ImportAssets(assets);

                //var set = _db.Query()
                //    .GroupBy(a => a.XXHash)
                //    .Having(g => g.Count() > 1)
                //    .Select(g => (g.Key, g.Count())).ToArray();

                //Console.WriteLine(set.Length);

                //foreach (var itm in set)
                //{
                //    Console.WriteLine(itm.Key + "\t\t" + itm.Item2);
                //}

                //UpdateAssetsWithXXHash(_db.FindAll(a => a is DocAsset));
                //UpdateAssetsWithXXHash(_db.FindAll(a => a is ImageAsset));
                //UpdateAssetsWithXXHash(_db.FindAll(a => a is VideoAsset));
                //_sw.Stop();
                //Console.WriteLine("Updates completed at " + _sw.ElapsedMilliseconds + " milliseconds.");
                //_sw.Start();

                //UpdateAssetsWithXXHash(@"D:\tmp");
                //ImportAssets(@"C:\Users\jerrod.eiman\Pictures");
                //UpdateAssetsWithXXHash(@"C:\Users\jerrod.eiman\Pictures");
                //ImportAssets(@"C:\Users\jerrod.eiman\Videos");
                //UpdateAssetsWithXXHash(@"C:\Users\jerrod.eiman\Videos");

                _sw.Stop();
                Console.WriteLine($"Completed @ {_sw.ElapsedMilliseconds} milliseconds.");
                _sw.Start();
            }

            _sw.Stop();
            Console.WriteLine("Process completed @ " + _sw.ElapsedMilliseconds + " milliseconds.");
            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();
        }
        #endregion

        private void ConcurrentQueryTest(int count)
        {
            Parallel.For(0, count, (i) =>
            {
                var sets = _db.Query()
                .GroupBy(a => a.Extension.ToLower())
                .Having(g => g.Count() > 5_000)
                .Select(g => (g.Key, g.Count()))
                .ToArray();
                Array.Sort<(string key, int cnt)>(sets, (a, b) => b.cnt.CompareTo(a.cnt));
                Console.WriteLine(sets[^1]);
            });
        }

        private void UpdateAssetsWithXXHash(DigitalAsset[] assets)
        {
            Console.WriteLine($"Hashing {assets.Length} assets.");
            ulong hash;
            //Parallel.For(0, assets.Length, (i) =>
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                using (var fs = new FileStream(asset.FullPath, FileMode.Open, FileAccess.Read))
                {
                    XxHash64 xx64 = new XxHash64(fs.Length);
                    xx64.Append(fs);
                    hash = xx64.GetCurrentHashAsUInt64();
                }

                int cnt = _db.Update(
                    apply: (a) => a.XXHash = hash,
                    where: (a) => a.Id == asset.Id
                );

                if (i % 1_000 == 0)
                    Console.Write('*');

            }//);

            Console.WriteLine(string.Empty);
        }

        private DigitalAsset[] ResolveAssets(string root, DigitalAssetType assetType)
        {
            DateTime now = DateTime.Now;

            var ops = new EnumerationOptions();
            ops.AttributesToSkip = FileAttributes.System | FileAttributes.Temporary;
            ops.IgnoreInaccessible = true;
            ops.ReturnSpecialDirectories = false;
            ops.RecurseSubdirectories = true;
            ops.MatchType = MatchType.Simple;

            string[] files = Directory.GetFiles(root, "*", ops);

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
                asset.Imported = now;

                assets[i] = asset;
            }

            return assets;
        }

        private void ImportAssets(List<DigitalAsset> assets)
        {
            Parallel.For(0, assets.Count, (i) =>
            {
                var a = assets[i];
                if (i % 2 == 0)
                    _db.Insert(a, (id) => a.Id = id, true);
                else
                    _db.Insert(a, (id) => a.Id = id, false);

                if (i % 1_000 == 0)
                {
                    Console.Write('.');
                    _db.Flush();
                }
            });

            Console.WriteLine(string.Empty);
            //Parallel.ForEach(assets, (asset =>
            ////foreach(var asset in assets)
            //{
            //    _db.Insert(asset, (id) => asset.Id = id, true);
            //
            //}));
        }
    }
}
