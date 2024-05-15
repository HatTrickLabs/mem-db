using System;

namespace HatTrick.MemDb
{
    internal interface IMemDbPersister<T> where T : class, new()
    {
        internal void Insert(MemDbRecord<T> record);
        internal void MarkStale(MemDbRecord<T> record);
        internal void Flush(object state);
    }
}
