using System;

namespace HatTrick.Data
{
    public interface IMemDbCache<T> : IMemDbQueryAccessor<T>, IDisposable where T : class
    {
        internal MemDbStatistics ResolveStatistics(Stats statistics);
        internal void Flush();
        internal (int stale, int deleted) Purge();
    }
}
