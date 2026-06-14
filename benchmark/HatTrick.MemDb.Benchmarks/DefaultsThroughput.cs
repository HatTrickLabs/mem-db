using System;
using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using HatTrick.Data;

public class DefaultsThroughput : ThroughputBench
{
    [IterationSetup(Targets = [nameof(InsertEncrypted), nameof(InsertUnencrypted)])]
    public void InsertIterationSetup()
    {
        MemDb.ConfigureFor<BenchmarkRecord>(base.DatasetName, base.DbPath)
            .SetFlushInterval(2)
            .EncryptWithPassword(() => "This_is_a_super_complex_password_used_for_encryption.")
            .Register();

        base.Db = MemDb.Open<BenchmarkRecord>(base.DatasetName);
    }

    [IterationSetup(Targets = [nameof(UpdateById), nameof(DeleteById)])]
    public void UpdateAndDeleteIterationSetup()
    {
        MemDb.ConfigureFor<BenchmarkRecord>(base.DatasetName, base.DbPath)
            .SetFlushInterval(2)
            .EncryptWithPassword(() => "This_is_a_super_complex_password_used_for_encryption.")
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
}
