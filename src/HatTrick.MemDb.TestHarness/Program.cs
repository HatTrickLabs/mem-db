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

            /**********************************************************************************************************/

            //int cnt = 0;
            //foreach (var itm in MemDb.ReadArchive<DigitalAsset>(datasetName))
            //{
            //    cnt += 1;
            //    if (itm.Id == 100)
            //    {
            //        Console.WriteLine(itm.Value.Name + "\t\t" + itm.Value.XXHash);
            //    }
            //}

            //Console.WriteLine("---------------------");
            //Console.WriteLine(cnt);
            //Console.ReadLine();
            //return;

            /**********************************************************************************************************/


            _sw = new Stopwatch();
            _sw.Start();

            using (_db = MemDb.Open<DigitalAsset>(datasetName))
            {
                _sw.Stop();
                Console.WriteLine("initialized " + _db.Count() + " records @ " + _sw.ElapsedMilliseconds + " milliseconds.");
                _sw.Start();

                //List<DigitalAsset> assets = new List<DigitalAsset>(3000);
                //assets.AddRange(ResolveAssets(@"D:\tmp"));
                //assets.AddRange(ResolveAssets(@"C:\Users\jerrod.eiman\Pictures"));
                //assets.AddRange(ResolveAssets(@"C:\Users\jerrod.eiman\Videos"));

                //Console.WriteLine(_db.Count());

                //_sw.Stop();
                //Console.WriteLine($"Resolved {assets.Count} assets @ {_sw.ElapsedMilliseconds}.");
                //_sw.Start();
                //ImportAssets(assets);

                //_db.Update(a => a.Name = a.Name + ".", a => a.Id == 3);


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
            //Parallel.ForEach(assets, (asset =>
            foreach(var asset in assets)
            {
                _db.Insert(asset, (id) => asset.Id = id, true);

            };
        }
    }
}
