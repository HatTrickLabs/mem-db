using System;
using System.Linq;
using System.IO;
using System.IO.Hashing;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using HatTrick.InMemDb;
using System.Reflection;

namespace HatTrick.InMemDb.TestHarness
{
    class Program
    {
        private static List<Failure> _failures;

        static void Main(string[] args)
        {
            _failures = new List<Failure>();

            AssetResolver resolver = new AssetResolver();

            Assembly assembly = Assembly.GetExecutingAssembly();
            var tests = assembly.GetTypes().Where(t => t.Name.EndsWith("Tests") && !t.IsAbstract && t.IsPublic).ToArray();

            int total = 0;
            for (int i = 0; i < tests.Length; i++)
            {
                var test = (TestBase)Activator.CreateInstance(tests[i], resolver);
                test.Go(ref _failures, out int count);
                total += count;
            }

            if (_failures.Count > 0)
            {
                Console.WriteLine($"Executed {total} tests with {_failures.Count} failures");
                foreach (var f in _failures)
                {
                    Console.WriteLine($"Failed: {f.Target}...{f.Exception.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Executed {total} tests with 0 failures");
            }

            Console.WriteLine(string.Empty);
            Console.WriteLine("Completed...Press [Enter] to exit.");
            Console.ReadLine();
        }
    }
}

