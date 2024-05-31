using System;
using System.Diagnostics;
using HatTrick.MemDb;
using System.IO.Hashing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;

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
            //var defrag = new MemDbDefragmenter<DigitalAsset>(datasetName, DbRoot);
            //defrag.Defrag();
            //return;

            MemDb.ConfigureFor<DigitalAsset>(datasetName, DbRoot)
                .SerializeWith(() => new DigitalAssetSerializer())
                .CloneWith(() => new DigitalAssetCloner())
                //.EncryptWith(null)
                .ReadWrite()
                .Register();

            _sw = new Stopwatch();
            _sw.Start();

            using (_db = MemDb<DigitalAsset>.Open(datasetName))
            {
                _sw.Stop();
                Console.WriteLine("initialized " + _db.Count() + " records @ " + _sw.ElapsedMilliseconds + " milliseconds.");
                _sw.Start();


                DigitalAsset[] assets = _db.Query()
                    .Where(a => a.Extension == ".jpg")
                    .OrderBy((a, b) => b.Created.CompareTo(a.Created))
                    .Skip(4).Limit(60)
                    .ToArray();

                //_db.Delete(a => true);
                //Console.WriteLine(_db.Count(a => string.Compare(a.Extension, ".jpg", true)  == 0));
                //Console.WriteLine(_db.Count(a => string.Compare(a.Extension, ".jpeg", true) == 0));
                //Console.WriteLine(_db.Count(a => string.Compare(a.Extension, ".png", true)  == 0));
                //Console.WriteLine(_db.Count(a => string.Compare(a.Extension, ".csv", true)  == 0));

                //ImportAssets("d:\\tmp");
            }

            _sw.Stop();
            Console.WriteLine("Process completed in " + _sw.ElapsedMilliseconds + " milliseconds.");
            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();
        }

        static void ImportAssets(string root)
        {
            DateTime now = DateTime.Now;

            var ops = new EnumerationOptions();
            ops.AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.Temporary;
            ops.IgnoreInaccessible = true;
            ops.ReturnSpecialDirectories = false;
            ops.RecurseSubdirectories = true;
            ops.MatchType = MatchType.Simple;

            string[] files = Directory.GetFiles(root, "*", ops);
            Console.WriteLine("Starting import of " + files.Length + " digital assets.");
            XxHash64 xx64 = new XxHash64();
            int cnt = 0;
            Parallel.ForEach(files, (file =>
            {
                Interlocked.Increment(ref cnt);
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    xx64.Append(fs);
                }

                ulong hash = xx64.GetCurrentHashAsUInt64();
                xx64.Reset();

                FileInfo fi = new FileInfo(file);
                DigitalAsset asset = new DigitalAsset();
                asset.Name = fi.Name;
                asset.Directory = Path.GetDirectoryName(file);
                asset.Created = fi.CreationTime;
                asset.LastAccess = fi.LastAccessTime;
                asset.LastWrite = fi.LastWriteTime;
                asset.Length = fi.Length;
                asset.Imported = now;
                asset.XXHash = hash;

                _db.Insert(asset, (id) => asset.Id = id);

                if ((cnt % 100) == 0)
                    Console.Write(".");
            }));
        }
    }
}
