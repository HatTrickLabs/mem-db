using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Diagnostics;
using HatTrick.MemDb;

namespace TestHarness
{
    class Program
    {
        static Stopwatch _sw;
        static MemDb<BookTextRecord> _db;
        static string datasetName = "books";
        static string DbRoot = @"d:\tmp\mem-db\books";

        static void Main(string[] args)
        {
            //var defrag = new MemDbDefragmenter<BookTextRecord>(datasetName, DbRoot);
            //defrag.Defrag();
            //return;

            MemDb.ConfigureFor<BookTextRecord>(datasetName, DbRoot)
                .SerializeWith(BookTextRecordSerializer.GetInstance)
                //.EncryptWith(null)
                .CloneWith(() => new BookTextCloner())
                .ReadWrite()
                .Register();

            _sw = new Stopwatch();
            _sw.Start();

            using (_db = MemDb<BookTextRecord>.Open("books"))
            {
                _sw.Stop();
                Console.WriteLine("initialized " + _db.Count() + " records @ " + _sw.ElapsedMilliseconds + " milliseconds.");
                _sw.Start();

                //Thread t = new Thread(new ThreadStart(() =>
                //{
                //    int deleted = _db.Delete((r) => r.BookName == "The Adventures Of Tom Sawyer");
                //    Console.WriteLine($"Deleted {deleted} records for Tom Sawyer.");
                //}));
                //t.Start();

                //Book text is included in the project but NOT copied to the output dir...
                //ImportBooks(@"D:\git\HatTrickLabs\mem-db\src\HatTrick.MemDb.TestHarness\BookText");

                //RunQueries();
                //ExecuteUpdates("Lord Of The Flies");

                RunQueries();
                //SearchText();
                //DefragDB();
                //MultiThreadImport();
                //MultiThreadedUpdate();
                //ConfirmUpdates();
                //MultiThreadRunQueries();
                //MultiThreadChaos();
                //ConfirmUpdates();
                //t.Join();
            }

            _sw.Stop();
            Console.WriteLine("Process completed in " + _sw.ElapsedMilliseconds + " milliseconds.");
            Console.WriteLine("Press [Enter] to exit.");
            Console.ReadLine();
        }

        #region import books
        private static void ImportBooks(object path)
        {
            int cnt = 0;
            string[] files = Directory.GetFiles((string)path);
            foreach (string file in files)
            {
                FileInfo fi = new FileInfo(file);
                string bookName = fi.Name.Replace("_", " ").Replace(".txt", string.Empty);
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        string line = null;
                        do
                        {
                            line = sr.ReadLine();
                            BookTextRecord rec = new BookTextRecord();
                            cnt += 1;
                            rec.Text = line;
                            rec.WordCount = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                            rec.BookName = bookName;
                            _db.Insert(rec, (id) => rec.Id = id);
                        } while (!sr.EndOfStream);
                    }
                }
            }
            Console.WriteLine($"imported {cnt} lines of text");
        }
        #endregion

        #region search text
        private static void SearchText()
        {
            Again:
            Console.WriteLine("Enter a word to search or type [exit] to exit.");
            string word = Console.ReadLine();
            if (word.ToLower() != "exit")
            {
                BookTextRecord[] recs = _db.FindAll(r => r.Text.ToLower().Contains(word.ToLower()));
                foreach (BookTextRecord rec in recs)
                {
                    Console.WriteLine(rec.Text);
                }
                Console.WriteLine(string.Empty);
                Console.WriteLine(string.Empty);
                Console.WriteLine(string.Format("found {0} lines containing '{1}'", recs.Length, word));
                goto Again;
            }
        }
        #endregion

        #region run queries
        static void RunQueries()
        {
            Stopwatch sw = new Stopwatch();

            //query 1,2,3
            sw.Restart();
            int huckFinnLineCount = _db.Count(r => r.BookName == "Adventures Of Huckleberry Finn");
            int tomSawerLineCount = _db.Count(r => r.BookName == "The Adventures Of Tom Sawyer");
            int lordOfFliesLineCount = _db.Count(r => r.BookName == "Lord Of The Flies");
            sw.Stop();
            Console.WriteLine("executed query 1,2,3 in " + sw.ElapsedMilliseconds + " milliseconds.");

            //query 4,5,6
            sw.Restart();
            int huckFinnWordCnt2 = _db.Query().Where(r => r.BookName == "Adventures Of Huckleberry Finn").Sum(r => r.WordCount);
            int tomSawyerWordCount = _db.Query().Where(r => r.BookName == "The Adventures Of Tom Sawyer").Sum(r => r.WordCount);
            int lordOfFliesWordCount = _db.Query().Where(r => r.BookName == "Lord Of The Flies").Sum(r => r.WordCount);
            sw.Stop();
            Console.WriteLine("executed query 4,5,6 in " + sw.ElapsedMilliseconds + " milliseconds.");

            //query 7 => all huck finn text retrieved
            sw.Restart();
            BookTextRecord[] allHuckFinnText = _db.FindAll(r => r.BookName == "Adventures Of Huckleberry Finn");
            sw.Stop();
            Console.WriteLine("executed query 7 in " + sw.ElapsedMilliseconds + " milliseconds; retrieved " + allHuckFinnText.Length + " records.");

            //Query 8
            sw.Restart();
            BookTextRecord[] huckFinn1000LineChunk = _db.Query()
                .Where(r => r.BookName == "Adventures Of Huckleberry Finn")
                .OrderBy((a, b) => a.Id.CompareTo(b.Id)) //sort by id to ensure correct sequence...
                .Skip(1000)
                .Limit(1000)
                .FindAll();
            sw.Stop();
            Console.WriteLine("executed query 8 in " + sw.ElapsedMilliseconds + " milliseconds;  order by id, skip 1000, take 1000");

            //Query 9,10,11
            sw.Restart();
            int huckFinnMaxWordCountInALine = _db.Query().Where(r => r.BookName == "Adventures Of Huckleberry Finn").Max<int>(r => r.WordCount);
            int TomSawyerMaxWordCountInALine = _db.Query().Where(r => r.BookName == "The Adventures Of Tom Sawyer").Max<int>(r => r.WordCount);
            int lordOfFliesMaxWordCountInALine = _db.Query().Where(r => r.BookName == "Lord Of The Flies").Max<int>(r => r.WordCount);
            sw.Stop();
            Console.WriteLine("executed query 9,10,11 in " + sw.ElapsedMilliseconds + " milliseconds.");

            //Query 12,13,14
            sw.Restart();
            double avgHuckFinnLineWordCount = _db.Query().Where(r => r.BookName == "Adventures Of Huckleberry Finn").Avg(r => r.WordCount);
            double avgTomSawyerLineWordCount = _db.Query().Where(r => r.BookName == "The Adventures Of Tom Sawyer").Avg(r => r.WordCount);
            double avgLordOfFliesWordCount = _db.Query().Where(r => r.BookName == "Lord Of The Flies").Avg(r => r.WordCount);
            sw.Stop();
            Console.WriteLine("executed query 12,13,14 in " + sw.ElapsedMilliseconds + " milliseconds.");

            sw.Restart();
            //Query 15
            string[] distinctBookNames = _db.FindDistinct<string>(r => r.BookName);
            sw.Stop();
            Console.WriteLine("executed query 15 in " + sw.ElapsedMilliseconds + " milliseconds.");

            sw.Restart();
            //Query 16
            BookTextRecord[] xxxxx = _db.Query()
                .Where(r => r.Text.EndsWith("@@@"))
                .OrderBy((a, b) => a.WordCount.CompareTo(b.WordCount))
                .Skip(10).Limit(10)
                .FindAll();
            sw.Stop();
            Console.WriteLine("executed query 16 in " + sw.ElapsedMilliseconds + " milliseconds.");
        }
        #endregion

        #region execute updates
        static void ExecuteUpdates(object book)
        {
            Stopwatch sw = new Stopwatch();
            string bookName = (string)book;

            int cnt = 0;
            Action<BookTextRecord> update = (rec) =>
            {
                rec.Text = rec.Text + " @@@";
                rec.WordCount += 1;
            };
            Func<BookTextRecord, bool> updateWhere = (r) => r.BookName == bookName && !r.Text.EndsWith("@@@");

            Action<BookTextRecord> reverse = (rec) =>
            {
                rec.Text = rec.Text.Replace(" @@@", string.Empty);
                rec.WordCount -= 1;
            };
            Func<BookTextRecord, bool> reverseWhere = (r) => r.BookName == bookName && r.Text.EndsWith("@@@");

            sw.Start();
            cnt = _db.Update(update, updateWhere);
            sw.Stop();
            Console.WriteLine("updated " + cnt + " records in " + sw.ElapsedMilliseconds + " milliseconds");

            sw.Restart();
            _db.Flush(); //force fsync
            sw.Stop();
            Console.WriteLine("flushed all updates to disk in " + sw.ElapsedMilliseconds + " milliseconds.");

            sw.Reset();
        }
        #endregion

        #region confirm upates
        static void ConfirmUpdates()
        {
            int cnt = _db.FindAll(r => r.Text.EndsWith(" @@@")).Count();
            Console.WriteLine(cnt + " records end with @@@.");
        }
        #endregion

        #region multi thread import
        static void MultiThreadImport()
        {
            Thread t0 = new Thread(new ParameterizedThreadStart(ImportBooks)); //23,241 records
            Thread t1 = new Thread(new ParameterizedThreadStart(ImportBooks)); //23,241 records
            Thread t2 = new Thread(new ParameterizedThreadStart(ImportBooks)); //23,241 records
            Thread t3 = new Thread(new ParameterizedThreadStart(ImportBooks)); //23,241 records
            Thread t4 = new Thread(new ParameterizedThreadStart(ImportBooks)); //23,241 records
            Thread t5 = new Thread(new ParameterizedThreadStart(ImportBooks)); //23,241 records
            Thread t6 = new Thread(new ParameterizedThreadStart(ImportBooks)); //23,241 records
            Thread t7 = new Thread(new ParameterizedThreadStart(ImportBooks)); //23,241 records
            Thread t8 = new Thread(new ParameterizedThreadStart(ImportBooks)); //23,241 records
            Thread t9 = new Thread(new ParameterizedThreadStart(ImportBooks)); //23,241 records

            string path = @"D:\git\HatTrickLabs\mem-db\src\HatTrick.MemDb.TestHarness\BookText";
            t0.Start(path);
            t1.Start(path);
            t2.Start(path);
            t3.Start(path);
            t4.Start(path);
            t5.Start(path);
            t6.Start(path);
            t7.Start(path);
            t8.Start(path);
            t9.Start(path);

            t0.Join();
            t1.Join();
            t2.Join();
            t3.Join();
            t4.Join();
            t5.Join();
            t6.Join();
            t7.Join();
            t8.Join();
            t9.Join();
            //should have 232,410 records...
        }
        #endregion

        #region multi threaded updates
        static void MultiThreadedUpdate()
        {
            Thread t1 = new Thread(new ParameterizedThreadStart(ExecuteUpdates));
            Thread t2 = new Thread(new ParameterizedThreadStart(ExecuteUpdates));
            Thread t3 = new Thread(new ParameterizedThreadStart(ExecuteUpdates));

            t1.Start("Adventures Of Huckleberry Finn");
            t2.Start("Lord Of The Flies");
            t3.Start("The Adventures Of Tom Sawyer");

            t1.Join();
            t2.Join();
            t3.Join();
        }
        #endregion

        #region multi thread run queries
        static void MultiThreadRunQueries()
        {
            Thread t1 = new Thread(new ThreadStart(RunQueries));
            Thread t2 = new Thread(new ThreadStart(RunQueries));
            Thread t3 = new Thread(new ThreadStart(RunQueries));

            t1.Start();
            t2.Start();
            t3.Start();

            t1.Join();
            t2.Join();
            t3.Join();
        }
        #endregion

        #region multi thread chaos
        static void MultiThreadChaos()
        {
            Thread t1 = new Thread(new ThreadStart(RunQueries));
            Thread t2 = new Thread(new ParameterizedThreadStart(ExecuteUpdates));
            Thread t3 = new Thread(new ParameterizedThreadStart(ImportBooks));
            Thread t4 = new Thread(new ThreadStart(RunQueries));
            Thread t5 = new Thread(new ParameterizedThreadStart(ExecuteUpdates));

            t1.Start();
            t2.Start("Adventures Of Huckleberry Finn");
            t3.Start(@"D:\git\HatTrickLabs\mem-db\src\HatTrick.MemDb.TestHarness\BookText");
            t4.Start();
            t5.Start("Lord Of The Flies");

            t1.Join();
            t2.Join();
            t3.Join();
            t4.Join();
            t5.Join();
        }
        #endregion
    }
}
