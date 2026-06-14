using System;
using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using HatTrick.Data;

[SimpleJob]
[MemoryDiagnoser]
public abstract class ThroughputBench
{
    public const int RecordSeedCount             = 250_000;
    public const int UpdateDeleteSeedCount       = 100_000;
    public const int FindSeedCount               = 250_000;
    public const int LargeVolumeFindSeedCount    = 3_000_000;
    public const int InsertsPerInvoke            = 250_000;
    public const int UpdatesPerInvoke            = 2_500;
    public const int DeletesPerInvoke            = 2_500;
    public const int FindByIdPerInvoke           = 2_500;
    public const int FindByPredicatePerInvoke    = 2_500;
    public const int FindByAppliedIndexPerInvoke = 2_500;

    private BenchmarkRecord[] _records;
    private MemDb<BenchmarkRecord> _db;
    private string _dbPath = "db";
    private string _datasetName = "benchmark";

    public BenchmarkRecord[] Records => _records;
    public string DbPath => _dbPath;
    public string DatasetName => _datasetName;
    public MemDb<BenchmarkRecord> Db
    {
        get => _db;
        set => _db = value;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var json = File.ReadAllText("data/benchmark-data.json");
        var source = JsonSerializer.Deserialize<BenchmarkRecord[]>(json);

        //we only have 10,000 records so loop them until we get N total...
        _records = new BenchmarkRecord[RecordSeedCount];
        for (int i = 0; i < RecordSeedCount; i++)
        {
            _records[i] = source[i % source.Length];
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _db.Dispose();
        System.Threading.Thread.Sleep(1);//windows FS somehow holds a file handle longer than it should
        string[] files = Directory.GetFiles(_dbPath);
        foreach (var file in files)
        {
            File.Delete(file);
        }
        MemDb.RemoveConfigurationFor(_datasetName);
        Array.ForEach(_records, (r) => r.Id = 0);
    }

    [Benchmark(OperationsPerInvoke = ThroughputBench.InsertsPerInvoke)]
    public void InsertEncrypted()
    {
        MemDb<BenchmarkRecord> db = _db;
        BenchmarkRecord[] records = _records;
        int count = ThroughputBench.InsertsPerInvoke;

        for (int i = 0; i < count; i++)
        {
            db.Insert(records[i], encrypt: true);
        }
        db.Flush();
    }

    [Benchmark(OperationsPerInvoke = ThroughputBench.InsertsPerInvoke)]
    public void InsertUnencrypted()
    {
        MemDb<BenchmarkRecord> db = _db;
        BenchmarkRecord[] records = _records;
        int count = ThroughputBench.InsertsPerInvoke;

        for (int i = 0; i < count; i++)
        {
            _db.Insert(records[i]);
        }
        _db.Flush();
    }

    [Benchmark(OperationsPerInvoke = ThroughputBench.UpdatesPerInvoke)]
    public virtual void UpdateById()
    {
        MemDb<BenchmarkRecord> db = _db;
        BenchmarkRecord[] records = _records;
        int count = ThroughputBench.UpdatesPerInvoke;

        //start at 50,000 to get a descent scan prior to finding the target.
        for (int i = 50_000; i < 50_000 + count; i++)
        {
            db.Update((r) => r.Count += 1, id: i);
        }
        db.Flush();
    }

    [Benchmark(OperationsPerInvoke = ThroughputBench.DeletesPerInvoke)]
    public virtual void DeleteById()
    {
        MemDb<BenchmarkRecord> db = _db;
        BenchmarkRecord[] records = _records;
        int count = ThroughputBench.DeletesPerInvoke;

        //start at 10,000 to get a descent scan prior to finding the target.
        for (int i = 10_000; i < 10_000 + count; i++)
        {
            db.Delete(id: i);
        }
        db.Flush();
    }

    [Benchmark(OperationsPerInvoke = ThroughputBench.FindByIdPerInvoke)]
    public virtual void FindById()
    {
        MemDb<BenchmarkRecord> db = _db;
        BenchmarkRecord[] records = _records;
        int count = ThroughputBench.FindByIdPerInvoke;

        //start at 50,000 to get a descent scan prior to finding the target.
        for (int i = 50_000; i < 50_000 + count; i++)
        {
            var rec = db.Find(id: i);
        }
    }

    [Benchmark(OperationsPerInvoke = ThroughputBench.FindByPredicatePerInvoke)]
    public void FindByPredicate()
    {
        MemDb<BenchmarkRecord> db = _db;
        BenchmarkRecord[] records = _records;
        int count = ThroughputBench.FindByPredicatePerInvoke;

        //start at 50,000 to get a descent scan prior to finding the target.
        for (int i = 50_000; i < 50_000 + count; i++)
        {
            var rec = db.Find((r) => r.Id == i);
        }
    }
}