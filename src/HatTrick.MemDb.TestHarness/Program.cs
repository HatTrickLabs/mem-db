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
        static string DbRoot = @"d:\tmp\mem-db\assets";

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

            //MemDb.Defrag(datasetName);
            //return;

            using (_db = MemDb.Open<DigitalAsset>(datasetName))
            {
                _sw.Stop();
                Console.WriteLine("initialized " + _db.Count() + " records @ " + _sw.ElapsedMilliseconds + " milliseconds.");
                _sw.Start();

                _db.Update(a => a.XXHash = 1, a => a.XXHash != 1 && a.FullPath.StartsWith(@"D:\tmp", StringComparison.OrdinalIgnoreCase));

                int total = _db.Count();
                int tmp = _db.Count(a => a.FullPath.StartsWith(@"D:\tmp", StringComparison.OrdinalIgnoreCase));
                int pics = _db.Count(a => a.FullPath.StartsWith(@"C:\Users\jerrod.eiman\Pictures", StringComparison.OrdinalIgnoreCase));
                int vids = _db.Count(a => a.FullPath.StartsWith(@"C:\Users\jerrod.eiman\Videos", StringComparison.OrdinalIgnoreCase));
                int assembla = _db.Count(a => a.FullPath.StartsWith(@"D:\assembla", StringComparison.OrdinalIgnoreCase));
                int sumTotal = tmp + pics + vids + assembla;

                Console.WriteLine($"Total files:    {total}");
                Console.WriteLine($"tmp files:      {tmp}");
                Console.WriteLine($"Picture files:  {pics}");
                Console.WriteLine($"Video files:    {vids}");
                Console.WriteLine($"assembla files: {assembla}");
                Console.WriteLine($"Total files:    {sumTotal}");

                Console.WriteLine($"Files containing xx hash of 1: {_db.Count(a => a.XXHash == 1)}");

                //List<DigitalAsset> assets = new List<DigitalAsset>(18000);
                //assets.AddRange(ResolveAssets(@"D:\tmp"));
                //assets.AddRange(ResolveAssets(@"C:\Users\jerrod.eiman\Pictures"));
                //assets.AddRange(ResolveAssets(@"C:\Users\jerrod.eiman\Videos"));
                //assets.AddRange(ResolveAssets(@"D:\assembla"));

                //_sw.Stop();
                //Console.WriteLine($"Resolved {assets.Count} assets @ {_sw.ElapsedMilliseconds}.");
                //Console.WriteLine("Press [Enter] to continue...");
                //Console.ReadLine();
                //_sw.Start();

                //ImportAssets(assets);

                //Thread t1 = new Thread(() => { SimpleUpdate(_db.FindAll(a => a.FullPath.StartsWith(@"D:\tmp"))); });
                //Thread t2 = new Thread(() => { SimpleUpdate(_db.FindAll(a => a.FullPath.StartsWith(@"C:\Users\jerrod.eiman\Pictures"))); });
                //Thread t3 = new Thread(() => { SimpleUpdate(_db.FindAll(a => a.FullPath.StartsWith(@"C:\Users\jerrod.eiman\Videos"))); });

                //t1.Start();
                //t2.Start();
                //t3.Start();

                //t1.Join();
                //t2.Join();
                //t3.Join();

                //UpdateAssetsWithXXHash(@"D:\tmp");
                //ImportAssets(@"C:\Users\jerrod.eiman\Pictures");
                //UpdateAssetsWithXXHash(@"C:\Users\jerrod.eiman\Pictures");
                //ImportAssets(@"C:\Users\jerrod.eiman\Videos");
                //UpdateAssetsWithXXHash(@"C:\Users\jerrod.eiman\Videos");

                //_sw.Stop();
                //Console.WriteLine($"Completed insert of {_db.Count()} assets @ {_sw.ElapsedMilliseconds} milliseconds.");
                //Console.WriteLine("Press [Enter] to continue.");
                //Console.ReadLine();
                //_sw.Start();
            }

            _sw.Stop();
            Console.WriteLine("Process completed @ " + _sw.ElapsedMilliseconds + " milliseconds.");
            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();
        }

        static void SimpleUpdate(DigitalAsset[] assets)
        {
            for (int i = 0; i < assets.Length; i++)
            {
                _db.Update(a => a.XXHash = 1, a => a.Id == assets[i].Id);
            }
        }

        static void ConcurrentQueryTest(int count)
        {
            Parallel.For(0, count, (i) =>
            {
                var sets = _db.Query().GroupBy(a => a.Extension.ToLower()).Having(g => g.Count() > 100).Select(g => (g.Key, g.Count())).ToArray();
                Array.Sort<(string key, int cnt)>(sets, (a, b) => b.cnt.CompareTo(a.cnt));
                Console.WriteLine(sets[0]);
            });
        }

        static void UpdateAssetsWithXXHash(DigitalAsset[] assets)
        {
            Parallel.ForEach(assets, (asset) =>
            {
                ulong hash = 0;
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

            });
        }

        static DigitalAsset[] ResolveAssets(string root)
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
                DigitalAsset asset = DigitalAsset.CreateNew(DigitalAssetType.Doc);
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
            Parallel.ForEach(assets, (asset =>
            //foreach(var file in files)
            {
                _db.Insert(asset, (id) => asset.Id = id, true);

            }));
        }
    }
}
