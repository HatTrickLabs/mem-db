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
        private static List<Failure> _failures;

        static void Main(string[] args)
        {

            _failures = new List<Failure>();

            AssetResolver resolver = new AssetResolver();

            //var dot = new DefaultOptionBaselineTests(resolver);
            //dot.Go(ref _failures);

            //var rct = new RegisteredCloneBaselineTests(resolver);
            //rct.Go(ref _failures);

            //var rst = new RegisterdSerializeBaselineTests(resolver);
            //rst.Go(ref _failures);

            //var rcast = new RegisteredCloneAndSerializeBaselineTests(resolver);
            //rcast.Go(ref _failures);

            //var aket = new AESKeyEncryptedTests(resolver);
            //aket.Go(ref _failures);

            //var akfpt = new AESKeyFromPasswordEncryptedTests(resolver);
            //akfpt.Go(ref _failures);

            //var romt = new ReadOnlyModeTests(resolver);
            //romt.Go(ref _failures);

            //var aomt = new AppendOnlyModeTests(resolver);
            //aomt.Go(ref _failures);

            //var aiit = new AutoIncrementIdentityTests(resolver);
            //aiit.Go(ref _failures);

            //var qebt = new QueryExpressionBuilderTests(resolver);
            //qebt.Go(ref _failures);

            var hct = new HighConcurrencyTests(resolver);
            hct.Go(ref _failures);

            if (_failures.Count > 0)
            {
                foreach (var f in _failures)
                {
                    Console.WriteLine($"Failed: {f.Target}...{f.Exception.Message}");
                }
            }
            else
            {
                Console.WriteLine("Tests complete with 0 failures");
            }

            Console.WriteLine(string.Empty);
            Console.WriteLine("Completed...Press [Enter] to exit.");
            Console.ReadLine();
        }
    }
}

