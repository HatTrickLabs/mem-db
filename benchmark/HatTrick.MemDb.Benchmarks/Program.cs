using System;
using System.IO;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using HatTrick.Data;

class Program
{
    static void Main(string[] args)
    {
        /*****************************************************/

        var footprint = new MemoryFootprint();
        footprint.GlobalSetup();
        footprint.Measure(1_000);
        footprint.IterationCleanup();
        footprint.Measure(10_000);
        footprint.IterationCleanup();
        footprint.Measure(100_000);
        footprint.IterationCleanup();
        footprint.Measure(250_000);
        footprint.IterationCleanup();
        footprint.Measure(500_000);
        footprint.IterationCleanup();
        footprint.Measure(1_000_000);
        footprint.ReportTotalRss();
        footprint.IterationCleanup();


        /*****************************************************/

        var cold = new ColdStartWarmup();
        cold.GlobalSetup();
        int[] seeds = [25_000, 100_000, 500_000, 1_000_000, 10_000_000];
        foreach (var seed in seeds)
        {
            cold.IterationSetup(seed);
            cold.ColdStart();
            cold.ColdStart();
            cold.ColdStart();
            cold.IterationCleanup();
        }

        /*****************************************************/

        var mem = new MemoryFootprint();
        mem.GlobalSetup();
        int[] memSeeds = [100_000, 1_000_000, 10_000_000];
        foreach (var seed in memSeeds)
        {
            mem.Measure(seed);
            mem.IterationCleanup();
        }

        /*****************************************************/

        var bench = new CustomCloneAndSerializeThroughput();
        bench.GlobalSetup();
        bench.FindIterationSetup();
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        //bench.FindById();
        var stats = bench.Db.ResolveStatistics(Stats.AvgFreshSize | Stats.FreshCount | Stats.FreshSize);
        sw.Stop();
        bench.IterationCleanup();
        Console.WriteLine(sw.ElapsedMilliseconds);

        /******************************************************/

        var config = ManualConfig.Create(DefaultConfig.Instance)
                                 .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Microsecond))
                                 .AddColumn(StatisticColumn.OperationsPerSecond);

        BenchmarkRunner.Run<DefaultsThroughput>(config: config, args: args);
        BenchmarkRunner.Run<CustomCloneAndSerializeThroughput>(config: config, args: args);
        BenchmarkRunner.Run<CustomCloneAndSerializeIdentityIndexThroughput>(config: config, args: args);
        BenchmarkRunner.Run<CustomCloneAndSerializeAppliedIndexThroughput>(config: config, args: args);
    }
}
