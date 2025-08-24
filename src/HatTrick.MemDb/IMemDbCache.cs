using System;

namespace HatTrick.InMemDb
{
    public interface IMemDbCache<T> : IMemDbAcceessor<T>, IDisposable where T : class
    {
        internal MemDbStatistics ResolveStatistics(Stats statistics);
        internal void Flush();
        internal (int stale, int deleted) Purge();
    }
}
