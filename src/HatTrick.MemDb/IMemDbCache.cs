using System;

namespace HatTrick.InMemDb
{
    internal interface IMemDbCache<T> : IMemDbAcceessor<T>, IDisposable where T : class
    {
    }
}
