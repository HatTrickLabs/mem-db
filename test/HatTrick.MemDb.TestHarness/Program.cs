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
    class Program
    {

        static void Main(string[] args)
        {
            AssetResolver resolver = new AssetResolver();
            DefaultOptionTests dot = new DefaultOptionTests(resolver);

            dot.Go();

            if (dot.HasFailures)
            {
                var failures = dot.Failures;
                foreach (var f in failures)
                {
                    Console.WriteLine($"Failed: {f.Target}...{f.Exception.Message}");
                }
            }

            Console.WriteLine(string.Empty);
            Console.WriteLine("Completed...Press [Enter] to exit.");
            Console.ReadLine();
        }
    }
}

