using System;

namespace HatTrick.MemDb
{
    public interface IMemDbCacher<T> : IMemDbAcceessor<T> where T : class, new()
    {
    }
}
