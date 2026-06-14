using System.IO;
using BenchmarkDotNet.Attributes;
using HatTrick.Data;

public class CustomCloneAndSerializeAppliedIndexThroughput : ThroughputBench
{
    [IterationSetup(Targets = [nameof(InsertEncrypted), nameof(InsertUnencrypted)])]
    public void InsertIterationSetup()
    {
        MemDb.ConfigureFor<BenchmarkRecord>(base.DatasetName, base.DbPath)
            .SetFlushInterval(2)
            .EncryptWithPassword(() => "This_is_a_super_complex_password_used_for_encryption.")
            .CloneWith(() => new BenchmarkCloner())
            .SerializeWith(() => new BenchmarkSerializer())
            .ApplyIndex<long>(nameof(BenchmarkRecord.Id), (r) => r.Id)
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
            .ApplyIndex<long>(nameof(BenchmarkRecord.Id), (r) => r.Id)
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
            .ApplyIndex<long>(nameof(BenchmarkRecord.Id), (r) => r.Id)
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
            .ApplyIndex<long>(nameof(BenchmarkRecord.Id), (r) => r.Id)
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

    [Benchmark(OperationsPerInvoke = ThroughputBench.FindByAppliedIndexPerInvoke)]
    public override void FindById()
    {
        MemDb<BenchmarkRecord> db = base.Db;
        BenchmarkRecord[] records = base.Records;
        int count = ThroughputBench.FindByAppliedIndexPerInvoke;

        //start at 50,000 to get a descent scan prior to finding the target.
        for (int i = 50_000; i < 50_000 + count; i++)
        {
            var recs = db.QueryViaIndex<long>(nameof(BenchmarkRecord.Id)).IsEqualTo(i).ToArray();
        }
    }

    [Benchmark(OperationsPerInvoke = ThroughputBench.UpdatesPerInvoke)]
    public override void UpdateById()
    {
        MemDb<BenchmarkRecord> db = base.Db;
        BenchmarkRecord[] records = base.Records;
        int count = ThroughputBench.UpdatesPerInvoke;

        //start at 50,000 to get a descent scan prior to finding the target.
        for (int i = 50_000; i < 50_000 + count; i++)
        {
            db.QueryViaIndex<long>(nameof(BenchmarkRecord.Id)).IsEqualTo(i).Update((r) => r.Count += 1);
        }
        db.Flush();
    }

    //[Benchmark(OperationsPerInvoke = ThroughputBench.DeletesPerInvoke)]
    public override void DeleteById()
    {
        MemDb<BenchmarkRecord> db = base.Db;
        BenchmarkRecord[] records = base.Records;
        int count = ThroughputBench.DeletesPerInvoke;

        //start at 10,000 to get a descent scan prior to finding the target.
        for (int i = 10_000; i < 10_000 + count; i++)
        {
            db.QueryViaIndex<long>(nameof(BenchmarkRecord.Id)).IsEqualTo(i).Delete();
        }
        db.Flush();
    }

    //[Benchmark(OperationsPerInvoke = ThroughputBench.FindByAppliedIndexPerInvoke)]
    public void FindLargeVolume()
    {
        MemDb<BenchmarkRecord> db = base.Db;
        BenchmarkRecord[] records = base.Records;
        int count = ThroughputBench.FindByAppliedIndexPerInvoke;

        //start at 50,000 to get a descent scan prior to finding the target.
        for (int i = 250_000; i < 250_000 + count; i++)
        {
            var recs = db.QueryViaIndex<long>(nameof(BenchmarkRecord.Id)).IsEqualTo(i).ToArray();
        }
    }
}
