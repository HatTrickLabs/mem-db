using System;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    internal interface IMemDbPersister<T> : IDisposable where T : class
    {
        internal AccessMode Mode { get; }
        internal int RecordCount { get; }
        internal bool IsEncryptionReady { get; }
        internal void ReadMappedRecords(out List<MemDbRecord<T>> records);
        internal uint GetNextId();
        internal void Insert(MemDbRecord<T> record);
        internal void MarkStale(MemDbRecord<T> record);
        internal void MarkDeleted(MemDbRecord<T> record);
        internal void Flush(object state);
        internal DateTime Snapshot();
        internal MemDbStatistics ResolveStatistics(Stats statistics);
    }
}
