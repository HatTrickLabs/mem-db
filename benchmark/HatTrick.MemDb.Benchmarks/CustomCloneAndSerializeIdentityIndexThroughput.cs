// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System.IO;
using BenchmarkDotNet.Attributes;
using HatTrick.Data;

public class CustomCloneAndSerializeIdentityIndexThroughput : ThroughputBench
{
    [IterationSetup(Targets = [nameof(InsertEncrypted), nameof(InsertUnencrypted)])]
    public void InsertIterationSetup()
    {
        MemDb.ConfigureFor<BenchmarkRecord>(base.DatasetName, base.DbPath)
            .SetFlushInterval(2)
            .EncryptWithPassword(() => "This_is_a_super_complex_password_used_for_encryption.")
            .CloneWith(() => new BenchmarkCloner())
            .SerializeWith(() => new BenchmarkSerializer())
            .IndexOnIdentity(shouldIndex: true)
            .Register();

        base.Db = MemDb.Open<BenchmarkRecord>(base.DatasetName);
    }

    [IterationSetup(Targets = [nameof(UpdateById), nameof(DeleteById)])]
    public void UpdateAndDeleteIterationSetup()
    {
        MemDb.ConfigureFor<BenchmarkRecord>(base.DatasetName, base.DbPath)
            .SetFlushInterval(2)
            .EncryptWithPassword(() => "This_is_a_super_complex_password_used_for_encryption.")
            .CloneWith(() => new BenchmarkCloner())
            .SerializeWith(() => new BenchmarkSerializer())
            .IndexOnIdentity(shouldIndex: true)
            .Register();

        base.Db = MemDb.Open<BenchmarkRecord>(base.DatasetName);

        BenchmarkRecord[] records = base.Records;
        int count = ThroughputBench.UpdateDeleteSeedCount;

        for (int i = 0; i < count; i++)
        {
            base.Db.Insert(records[i], (id) => records[i].Id = id);
        }
        base.Db.Flush();
    }

    [IterationSetup(Targets = [nameof(FindById), nameof(FindByPredicate)])]
    public void FindIterationSetup()
    {
        MemDb.ConfigureFor<BenchmarkRecord>(base.DatasetName, base.DbPath)
            .SetFlushInterval(2)
            .EncryptWithPassword(() => "This_is_a_super_complex_password_used_for_encryption.")
            .CloneWith(() => new BenchmarkCloner())
            .SerializeWith(() => new BenchmarkSerializer())
            .IndexOnIdentity(shouldIndex: true)
            .Register();

        base.Db = MemDb.Open<BenchmarkRecord>(base.DatasetName);

        BenchmarkRecord[] records = base.Records;
        int count = ThroughputBench.FindSeedCount;

        for (int i = 0; i < count; i++)
        {
            base.Db.Insert(records[i], (id) => records[i].Id = id);
        }
        base.Db.Flush();
    }

    [IterationSetup(Target = nameof(FindLargeVolume))]
    public void LargevolumeFindIterationSetup()
    {
        MemDb.ConfigureFor<BenchmarkRecord>(base.DatasetName, base.DbPath)
            .SetFlushInterval(2)
            .EncryptWithPassword(() => "This_is_a_super_complex_password_used_for_encryption.")
            .CloneWith(() => new BenchmarkCloner())
            .SerializeWith(() => new BenchmarkSerializer())
            .IndexOnIdentity(shouldIndex: true)
            .Register();

        base.Db = MemDb.Open<BenchmarkRecord>(base.DatasetName);

        BenchmarkRecord[] records = base.Records;
        int count = ThroughputBench.LargeVolumeFindSeedCount;

        int iterations = count / ThroughputBench.RecordSeedCount;

        var largeVolRecs = new BenchmarkRecord[count];
        for (int i = 0; i < iterations; i++)
        {
            for (int j = 0; j < ThroughputBench.RecordSeedCount; j++)
            {
                base.Db.Insert(records[j]);
            }
            base.Db.Flush();
        }
        base.Db.Flush();
    }

    [Benchmark(OperationsPerInvoke = ThroughputBench.FindByIdPerInvoke)]
    public void FindLargeVolume()
    {
        MemDb<BenchmarkRecord> db = base.Db;
        BenchmarkRecord[] records = base.Records;
        int count = ThroughputBench.FindByIdPerInvoke;

        //start at 50,000 to get a descent scan prior to finding the target.
        for (int i = 250_000; i < 250_000 + count; i++)
        {
            var recs = db.Find(i);
        }
    }
}
