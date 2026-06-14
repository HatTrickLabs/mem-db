// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.Collections.Generic;
using System.IO;
using HatTrick.Data;

public class BenchmarkCloner : IMemDbCloner<BenchmarkRecord>
{
    public BenchmarkRecord DeepCopy(BenchmarkRecord value)
    {
        var record = new BenchmarkRecord();
        record.Id = value.Id;
        record.Name = value.Name;
        record.Category = value.Category;
        record.CreatedAt = value.CreatedAt;
        record.UpdatedAt = value.UpdatedAt;
        record.Amount = value.Amount;
        record.Count = value.Count;
        record.IsActive = value.IsActive;
        return record;
    }

    public BenchmarkRecord[] DeepCopy(IList<BenchmarkRecord> values)
    {
        int cnt = values?.Count ?? 0;

        if (cnt == 0)
            return Array.Empty<BenchmarkRecord>();

        BenchmarkRecord[] records = new BenchmarkRecord[cnt];
        for (int i = 0; i < cnt; i++)
        {
            BenchmarkRecord value = values[i];
            records[i] = this.DeepCopy(value);
        }
        return records;
    }
}
