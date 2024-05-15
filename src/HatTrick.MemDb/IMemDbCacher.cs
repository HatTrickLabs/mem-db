using System;

namespace HatTrick.MemDb
{
    internal interface IMemDbCacher<T> : IMemDbAcceessor<T> where T : class, new()
    {
    }
}
