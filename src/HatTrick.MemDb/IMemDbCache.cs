using System;

namespace HatTrick.InMemDb
{
    public interface IMemDbCache<T> : IMemDbAcceessor<T>, IDisposable where T : class
    {
        public MemDbStatistics ResolveStatistics(Stats statistics);
        internal void Flush();
        internal DateTime Snapshot();
        internal (int stale, int deleted) Purge();
    }
}
