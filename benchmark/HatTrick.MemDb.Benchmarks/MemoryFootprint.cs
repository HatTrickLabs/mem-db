// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using HatTrick.Data;

public class MemoryFootprint
{
    public const int RecordSeedCount = 250_000;

    private BenchmarkRecord[] _records;
    private MemDb<BenchmarkRecord> _db;
    private string _dbPath = "db";
    private string _datasetName = "benchmark";

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

    public void Measure(int seedCount)
    {
        MemDb.ConfigureFor<BenchmarkRecord>(_datasetName, _dbPath)
            .EncryptWithPassword(() => "This_is_a_super_complex_password_used_for_encryption.")
            .CloneWith(() => new BenchmarkCloner())
            .SerializeWith(() => new BenchmarkSerializer())
            .Register();

        _db = MemDb.Open<BenchmarkRecord>(_datasetName);

        //baseline: db is open but empty. force GC so the delta only reflects records.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long baselineHeap = GC.GetTotalMemory(forceFullCollection: true);

        for (int i = 0; i < seedCount; i++)
        {
            _db.Insert(_records[i % _records.Length], (id) => _records[i % _records.Length].Id = id);
        }
        _db.Flush();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long afterHeap = GC.GetTotalMemory(forceFullCollection: true);
        long heapDelta = afterHeap - baselineHeap;

        Console.WriteLine($"Records: {seedCount:N0}  heap delta: {heapDelta:N0} bytes  ({(double)heapDelta / seedCount:N2} bytes/record)");
    }

    public void ReportTotalRss()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        double mb = proc.WorkingSet64 / 1024.0 / 1024.0;
        Console.WriteLine($"Total process working set: {proc.WorkingSet64:N0} bytes ({mb:N2} MB)");
    }

    public void IterationCleanup()
    {
        _db.Dispose();
        Thread.Sleep(1); //windows FS somehow holds a file handle longer than it should
        string[] files = Directory.GetFiles(_dbPath);
        foreach (var file in files)
        {
            File.Delete(file);
        }
        MemDb.RemoveConfigurationFor(_datasetName);
        Array.ForEach(_records, (r) => r.Id = 0);
    }
}
