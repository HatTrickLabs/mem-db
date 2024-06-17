using System;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    internal interface IMemDbPersister<T> : IDisposable where T : class, new()
    {
        internal AccessMode Mode { get; }
        internal int RecordCount { get; }
        internal bool IsEncryptionReady { get; }
        internal IList<MemDbRecord<T>> InitializeMappedRecords();
        internal uint GetNextId();
        internal void Insert(MemDbRecord<T> record);
        internal void MarkStale(MemDbRecord<T> record);
        internal void Flush(object state);
    }
}
