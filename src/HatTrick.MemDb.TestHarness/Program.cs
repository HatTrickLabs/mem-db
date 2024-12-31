using System;
using System.Diagnostics;
using HatTrick.InMemDb;
using System.IO.Hashing;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace TestHarness
{
    class Program
    {
        static Stopwatch _sw;
        static MemDb<DigitalAsset> _db;
        static string datasetName = "assets";
        static string DbRoot = @"D:\_db\dev\mem-db-dev-assets";

        static void Main(string[] args)
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

                Console.WriteLine("Image: " + _db.Count(a => a.AssetType == DigitalAssetType.Image));
                Console.WriteLine("Video: " + _db.Count(a => a.AssetType == DigitalAssetType.Video));
                Console.WriteLine("Doc:   " + _db.Count(a => a.AssetType == DigitalAssetType.Doc));
                Console.WriteLine("Repo:  " + _db.Count(a => a.AssetType == DigitalAssetType.Repo));

                var vids = _db.FindAll(a => a.AssetType == DigitalAssetType.Video && a.Length >= 5_242_880);

                Console.WriteLine("Vids >= 5MB: " + vids.Length);

                Console.WriteLine("1: " + _db.Find(a => a.Id == 1).XXHash);
                Console.WriteLine("2: " + _db.Find(a => a.Id == 2).XXHash);
                Console.WriteLine("3: " + _db.Find(a => a.Id == 3).XXHash);
                Console.WriteLine("4: " + _db.Find(a => a.Id == 4).XXHash);
                Console.WriteLine("5: " + _db.Find(a => a.Id == 5).XXHash);
                Console.WriteLine("6: " + _db.Find(a => a.Id == 6).XXHash);
                Console.WriteLine("7: " + _db.Find(a => a.Id == 7).XXHash);
                Console.WriteLine("8: " + _db.Find(a => a.Id == 8).XXHash);
                Console.WriteLine("100: " + _db.Find(a => a.Id == 100).XXHash);

                //_sw.Stop();
                //Console.WriteLine($"Resolved {assets.Count} assets @ {_sw.ElapsedMilliseconds}.");
                //_sw.Start();

                //ImportAssets(assets);

                //UpdateAssetsWithXXHash();
                _sw.Stop();
                Console.WriteLine("Updates completed at " + _sw.ElapsedMilliseconds + " milliseconds.");
                _sw.Start();

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

        static void ConcurrentQueryTest(int count)
        {
            Parallel.For(0, count, (i) =>
            {
                var sets = _db.Query()
                .GroupBy(a => a.Extension.ToLower())
                .Having(g => g.Count() > 250)
                .Select(g => (g.Key, g.Count()))
                .ToArray();
                Array.Sort<(string key, int cnt)>(sets, (a, b) => b.cnt.CompareTo(a.cnt));
                Console.WriteLine(sets[^10]);
            });
        }

        static void UpdateAssetsWithXXHash(/*DigitalAsset[] assets*/)
        {
            ulong hash = 200;
            int cnt = _db.Update(
                    apply: (a) => a.XXHash = hash = (hash + 1),
                    where: (a) => true
            );

            Console.WriteLine($"Updated {cnt} records..");

            ////Parallel.For(0, assets.Length, (i) =>
            //for (int i = 0; i < assets.Length; i++)
            //{
            //    var asset = assets[i];
            //    ulong hash = 2;
            //    //using (var fs = new FileStream(asset.FullPath, FileMode.Open, FileAccess.Read))
            //    //{
            //    //    XxHash64 xx64 = new XxHash64(fs.Length);
            //    //    xx64.Append(fs);
            //    //    hash = xx64.GetCurrentHashAsUInt64();
            //    //}

            //    int cnt = _db.Update(
            //        apply: (a) => a.XXHash = hash, 
            //        where: (a) => a.Id == asset.Id
            //    );

            //    if (i % 1_000 == 0)
            //        Console.Write('*');

            //}//);

            Console.WriteLine(string.Empty);
        }

        static DigitalAsset[] ResolveAssets(string root, DigitalAssetType assetType)
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
                DigitalAsset asset = DigitalAsset.CreateNew(assetType);
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

        static void ImportAssets(List<DigitalAsset> assets)
        {
            Parallel.For(0, assets.Count, (i) =>
            {
                var a = assets[i];
                _db.Insert(a, (id) => a.Id = id, false);
                if (i % 1_000 == 0)
                    Console.Write('.');

                if (i % 10_000 == 0)
                {
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
