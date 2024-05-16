using System;
using System.Collections.Generic;

namespace HatTrick.MemDb
{
    internal interface IMemDbPersister<T> : IDisposable where T : class, new()
    {
        //internal IEnumerable<MemDbRecord<T>> ReadAll();
        internal MemDbRecord<T>[] ReadAll();
        internal void Insert(MemDbRecord<T> record);
        internal void MarkStale(MemDbRecord<T> record);
        internal void Flush(object state);
    }
}
