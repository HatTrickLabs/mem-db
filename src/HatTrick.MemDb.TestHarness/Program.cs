using System;
using System.Diagnostics;
using HatTrick.MemDb;
using System.IO.Hashing;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;
using System.Text;

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
                .EncryptWithKey(() => new byte[] { 198, 1, 6, 8, 12, 1, 1, 1, 1, 88, 1, 1, 1, 1, 1, 9, 9, 9, 1, 1, 99, 1, 1, 1, 1, 1, 1, 1, 33, 1, 1, 77})
                .ReadWrite()
                .Register();

            _sw = new Stopwatch();
            _sw.Start();

            using (_db = MemDb<DigitalAsset>.Open(datasetName))
            {
                _sw.Stop();
                Console.WriteLine("initialized " + _db.Count() + " records @ " + _sw.ElapsedMilliseconds + " milliseconds.");
                _sw.Start();

                Console.WriteLine(_db.Count(a => string.Compare(a.Extension, ".jpg", true)  == 0));
                Console.WriteLine(_db.Count(a => string.Compare(a.Extension, ".jpeg", true) == 0));
                Console.WriteLine(_db.Count(a => string.Compare(a.Extension, ".png", true)  == 0));
                Console.WriteLine(_db.Count(a => string.Compare(a.Extension, ".csv", true)  == 0));

                //ImportAssets(@"d:\abc");
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
            
            int cnt = 0;
            Parallel.ForEach(files, (file =>
            //foreach(var file in files)
            {
                Interlocked.Increment(ref cnt);
                XxHash64 xx64 = new XxHash64();
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    xx64.Append(fs);
                }

                ulong hash = xx64.GetCurrentHashAsUInt64();

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

                _db.Insert(asset, (id) => asset.Id = id, true);

                if ((cnt % 100) == 0)
                    Console.Write(".");
            }));
        }

        static void TestCrypto()
        {
            //cryptosize = inputsize + (blocksize - (inputsize % blocksize)) + ivsize;
            int cryptoSize = 74 + (16 - (74 % 16)) + 16;

            byte[] key = null;
            using (SHA256 sha256 = SHA256.Create())
            {
                key = sha256.ComputeHash(Encoding.UTF8.GetBytes("Jerrod's super secure password."));
            }

            MemDbAESEncryptor aes = new MemDbAESEncryptor(key);

            int cryptoSize2 = MemDbAESEncryptor.CalculateCryptoByteLength(74);

            string text = "This is just a sample statement used for encrypt/decrypt testing purposes.";
            byte[] encrypted = null;
            ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes(text);
            using (var ms = new MemoryStream())
            {
                aes.Encrypt(input, ms);
                encrypted = new byte[ms.Length];
                ms.Position = 0;
                ms.Read(encrypted, 0, encrypted.Length);
            }


            byte[] raw = null;
            using (var ms = new MemoryStream(encrypted))
            {
                raw = aes.Decrypt(ms, input.Length);
            }
            string roundTripText = Encoding.UTF8.GetString(raw);
        }
    }
}
