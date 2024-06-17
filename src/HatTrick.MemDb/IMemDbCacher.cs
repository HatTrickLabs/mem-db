using System;

namespace HatTrick.InMemDb
{
    internal interface IMemDbCacher<T> : IMemDbAcceessor<T>, IDisposable where T : class, new()
    {
    }
}
