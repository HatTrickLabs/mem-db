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


            using (_db = MemDb.Open<DigitalAsset>(datasetName))
            {
                _sw.Stop();
                Console.WriteLine("initialized " + _db.Count() + " records @ " + _sw.ElapsedMilliseconds + " milliseconds.");
                _sw.Start();

                Thread t1 = new Thread(() => { ImportAssets(@"D:\tmp"); });
                Thread t2 = new Thread(() => { ImportAssets(@"C:\Users\jerrod.eiman\Pictures"); });
                Thread t3 = new Thread(() => { ImportAssets(@"C:\Users\jerrod.eiman\Videos"); });
                Thread t4 = new Thread(() => { UpdateAssetsWithXXHash(@"C:\Users\jerrod.eiman\Videos"); });

                t3.Start();
                t1.Start();
                t2.Start();

                t3.Join();
                t4.Start();

                int hasXXX = _db.Count(a => a.XXHash > 0);
                Console.WriteLine($"{hasXXX} files have xx hash.");

                long totalFileLen = _db.Query().Sum(a => a.Length);
                Console.WriteLine($"Total file len: {totalFileLen}");

                long totalTmpLen = _db.Query().Where(a => a.FullPath.Contains("tmp")).Sum(a => a.Length);
                Console.WriteLine($"Total file len: {totalTmpLen}");

                long totalPicsLen = _db.Query().Where(a => a.FullPath.Contains("Pictures")).Sum(a => a.Length);
                Console.WriteLine($"Total file len: {totalPicsLen}");

                long totalVidsLen = _db.Query().Where(a => a.FullPath.Contains("Videos")).Sum(a => a.Length);
                Console.WriteLine($"Total file len: {totalVidsLen}");

                t1.Join();
                Console.WriteLine("joined t1");
                t2.Join();
                Console.WriteLine("joined t2");
                t4.Join();
                Console.WriteLine("joined t4");

                //ImportAssets(@"D:\tmp");
                //UpdateAssetsWithXXHash(@"D:\tmp");
                //ImportAssets(@"C:\Users\jerrod.eiman\Pictures");
                //UpdateAssetsWithXXHash(@"C:\Users\jerrod.eiman\Pictures");
                //ImportAssets(@"C:\Users\jerrod.eiman\Videos");
                //UpdateAssetsWithXXHash(@"C:\Users\jerrod.eiman\Videos");
            }

            _sw.Stop();
            Console.WriteLine("Process completed in " + _sw.ElapsedMilliseconds + " milliseconds.");
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

        static void UpdateAssetsWithXXHash(string root)
        {
            DateTime now = DateTime.Now;

            var ops = new EnumerationOptions();
            ops.AttributesToSkip = FileAttributes.System | FileAttributes.Temporary;
            ops.IgnoreInaccessible = true;
            ops.ReturnSpecialDirectories = false;
            ops.RecurseSubdirectories = true;
            ops.MatchType = MatchType.Simple;

            string[] files = Directory.GetFiles(root, "*", ops);
            Console.WriteLine("Starting update of " + files.Length + " digital assets.");

            long at = 0;
            Parallel.ForEach(files, (file) =>
            {
                Interlocked.Increment(ref at);

                ulong hash = 0;
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    XxHash64 xx64 = new XxHash64(fs.Length);
                    xx64.Append(fs);
                    hash = xx64.GetCurrentHashAsUInt64();
                }

                int cnt = _db.Update(
                    apply: (a) => a.XXHash = hash, 
                    where: (a) => a.Name == Path.GetFileName(file) && a.Directory == Path.GetDirectoryName(file)
                );

                if (cnt == 0)
                    Console.WriteLine("No asset found for file: " + file);

                long x = Interlocked.Read(ref at);
                if ((x % 100) == 0)
                    Console.Write('.');
            });
        }

        static void ImportAssets(string root)
        {
            DateTime now = DateTime.Now;

            var ops = new EnumerationOptions();
            ops.AttributesToSkip = FileAttributes.System | FileAttributes.Temporary;
            ops.IgnoreInaccessible = true;
            ops.ReturnSpecialDirectories = false;
            ops.RecurseSubdirectories = true;
            ops.MatchType = MatchType.Simple;

            //DigitalAssetType type = root.Contains("Pictures", StringComparison.OrdinalIgnoreCase)
            //    ? DigitalAssetType.Image
            //    : root.Contains("Videos", StringComparison.OrdinalIgnoreCase)
            //        ? DigitalAssetType.Video
            //        : DigitalAssetType.Doc;

            string[] files = Directory.GetFiles(root, "*", ops);
            Console.WriteLine("Starting import of " + files.Length + " digital assets.");
            
            int cnt = 0;
            //Parallel.ForEach(files, (file =>
            foreach(var file in files)
            {
                Interlocked.Increment(ref cnt);
                //XxHash64 xx64 = new XxHash64();
                //using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                //{
                //    xx64.Append(fs);
                //}

                //ulong hash = xx64.GetCurrentHashAsUInt64();

                FileInfo fi = new FileInfo(file);
                DigitalAsset asset = DigitalAsset.CreateNew(DigitalAssetType.Doc);
                asset.Name = fi.Name;
                asset.Directory = Path.GetDirectoryName(file);
                asset.Created = fi.CreationTime;
                asset.LastAccess = fi.LastAccessTime;
                asset.LastWrite = fi.LastWriteTime;
                asset.Length = fi.Length;
                asset.Imported = now;
                //asset.XXHash = hash;

                _db.Insert(asset, (id) => asset.Id = id, true);

                if ((cnt % 100) == 0)
                    Console.Write(".");
            }//));
        }
    }
}
