// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using System.Diagnostics;
using System.Threading;
using HatTrick.Data;

public class ColdStartWarmup
{
    public const int RecordSeedCount = 250_000;

    private BenchmarkRecord[] _records;
    private MemDb<BenchmarkRecord> _db;
    private string _dbPath = "db";
    private string _datasetName = "benchmark";
    private Stopwatch _sw = new Stopwatch();

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

    public void IterationSetup(int seedCount)
    {
        MemDb.ConfigureFor<BenchmarkRecord>(_datasetName, _dbPath)
            .EncryptWithPassword(() => "This_is_a_super_complex_password_used_for_encryption.")
            .CloneWith(() => new BenchmarkCloner())
            .SerializeWith(() => new BenchmarkSerializer())
            .Register();

        using (_db = MemDb.Open<BenchmarkRecord>(_datasetName))
        {
            for (int i = 0; i < seedCount; i++)
            {
                _db.Insert(_records[i % _records.Length], (id) => _records[i % _records.Length].Id = id, encrypt: false);
            }
        }
    }

    public void ColdStart()
    {
        _sw.Start();
        _db = MemDb.Open<BenchmarkRecord>(_datasetName);
        _sw.Stop();
        Console.WriteLine($"Cold start for {_db.Count()} records @ {_sw.ElapsedMilliseconds} milliseconds");
        _db.Dispose();
        _sw.Reset();
        GC.Collect();
    }

    public void IterationCleanup()
    {
        _db.Dispose();
        Thread.Sleep(1);//windows FS somehow holds a file handle longer than it should
        string[] files = Directory.GetFiles(_dbPath);
        foreach (var file in files)
        {
            File.Delete(file);
        }
        MemDb.RemoveConfigurationFor(_datasetName);
        Array.ForEach(_records, (r) => r.Id = 0);
        _sw.Reset();
    }
}
