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
                .SerializeWith(() => new DigitalAssetSerializer())
                .CloneWith(() => new DigitalAssetCloner())
                //.EncryptWithKey(() => new byte[] { 198, 1, 6, 8, 12, 1, 1, 1, 1, 88, 1, 1, 1, 1, 1, 9, 9, 9, 1, 1, 99, 1, 1, 1, 1, 1, 1, 1, 33, 1, 1, 77 })
                //.EncryptWithPassword(() => "Jerrod's super simple password...!!!!!!!!")
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

                Console.WriteLine($"Image Count: {_db.Count(a => a.Directory.Contains("Pictures"))}");
                Console.WriteLine($"Video Count: {_db.Count(a => a.Directory.Contains("Videos"))}");
                Console.WriteLine($"Tmp Count:   {_db.Count(a => a.Directory.Contains("tmp"))}");

                var sets = _db.Query().GroupBy(a => a.Extension.ToLower()).Having(g => g.Count() > 1000).Select(g => (g.Key, g.Count())).ToArray();
                Array.Sort<(string key, int cnt)>(sets, (a, b) => b.cnt.CompareTo(a.cnt));
                Console.WriteLine(sets[0]);

                //Thread t1 = new Thread(new ParameterizedThreadStart((path) => ImportAssets(path.ToString())));
                //Thread t2 = new Thread(new ParameterizedThreadStart((count) => ConcurrentQueryTest((int)count)));
                //Thread t3 = new Thread(new ParameterizedThreadStart((path) => ImportAssets(path.ToString())));
                //Thread t4 = new Thread(new ParameterizedThreadStart((path) => ImportAssets(path.ToString())));
                //t3.Start(@"D:\tmp");
                //t4.Start(@"C:\Users\jerrod.eiman\Pictures");
                //t2.Start(100);
                //t1.Start(@"C:\Users\jerrod.eiman\Videos");

                //t2.Join();
                //t3.Join();
                //t1.Join();
                //t4.Join();

                //Thread t5 = new Thread(new ThreadStart(() => UpdateAssetsWithXXHash(@"D:\tmp")));
                //Thread t6 = new Thread(new ThreadStart(() => UpdateAssetsWithXXHash(@"C:\Users\jerrod.eiman\Pictures")));
                //Thread t7 = new Thread(new ThreadStart(() => UpdateAssetsWithXXHash(@"C:\Users\jerrod.eiman\Videos")));

                //t5.Start();
                //t6.Start();
                //t7.Start();

                //t5.Join();
                //t6.Join();
                //t7.Join();

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

            string[] files = Directory.GetFiles(root, "*", ops);
            Console.WriteLine("Starting import of " + files.Length + " digital assets.");
            
            int cnt = 0;
            Parallel.ForEach(files, (file =>
            //foreach(var file in files)
            {
                Interlocked.Increment(ref cnt);
                //XxHash64 xx64 = new XxHash64();
                //using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                //{
                //    xx64.Append(fs);
                //}

                //ulong hash = xx64.GetCurrentHashAsUInt64();

                FileInfo fi = new FileInfo(file);
                DigitalAsset asset = new DigitalAsset();
                asset.Name = fi.Name;
                asset.Directory = Path.GetDirectoryName(file);
                asset.Created = fi.CreationTime;
                asset.LastAccess = fi.LastAccessTime;
                asset.LastWrite = fi.LastWriteTime;
                asset.Length = fi.Length;
                asset.Imported = now;
                //asset.XXHash = hash;

                _db.Insert(asset, (id) => asset.Id = id);

                if ((cnt % 100) == 0)
                    Console.Write(".");
            }));
        }
    }
}
