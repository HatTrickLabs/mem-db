using System;

namespace HatTrick.MemDb
{
    public interface IMemDbPersister<T> where T : class, new()
    {
        //public void Insert(MemDbRecord<T> record);
        //public void MarkStale(MemDbRecord<T> record);
        //public void Flush(object state);
    }
}
