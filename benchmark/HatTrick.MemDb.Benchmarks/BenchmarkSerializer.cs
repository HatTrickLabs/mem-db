using System;
using System.IO;
using System.Text;
using HatTrick.Data;

public class BenchmarkSerializer : IMemDbSerializer<BenchmarkRecord>
{
    public byte[] Serialize(BenchmarkRecord record)
    {
        int capacity = sizeof(int) + sizeof(long) + 255 + (sizeof(long) * 5) + sizeof(ulong);

        byte[] raw = null;
        using (var ms = new MemoryStream(capacity))
        {
            using (var writer = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                this.Serialize(record, writer);
            }
            raw = ms.ToArray();
        }
        return raw;
    }

    public void Serialize(BenchmarkRecord record, BinaryWriter to)
    {
        to.Write(record.Id);
        to.Write(record.Name);
        to.Write(record.Category);
        to.Write(record.CreatedAt.ToBinary());
        to.Write(record.UpdatedAt.ToBinary());
        to.Write(record.Amount);
        to.Write(record.Count);
        to.Write(record.IsActive);
    }

    public BenchmarkRecord Deserialize(ReadOnlySpan<byte> from)
    {
        BenchmarkRecord record = null;
        using (var ms = new MemoryStream(from.ToArray()))
        {
            using (var reader = new BinaryReader(ms))
            {
                record = this.Deserialize(reader, from.Length);
            }
        }
        return record;
    }

    public BenchmarkRecord Deserialize(BinaryReader from, int length)
    {
        var record = new BenchmarkRecord();
        record.Id = from.ReadInt64();
        record.Name = from.ReadString();
        record.Category = from.ReadString();
        record.CreatedAt = DateTime.FromBinary(from.ReadInt64());
        record.UpdatedAt = DateTime.FromBinary(from.ReadInt64());
        record.Amount = from.ReadDecimal();
        record.Count = from.ReadInt32();
        record.IsActive = from.ReadBoolean();
        return record;
    }
}
