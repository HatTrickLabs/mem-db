using System;

namespace HatTrick.MemDb
{
    internal interface IMemDbCacher<T> : IMemDbAcceessor<T>, IDisposable where T : class, new()
    {
    }
}
