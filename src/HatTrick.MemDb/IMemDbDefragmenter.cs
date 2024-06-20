using System;

namespace HatTrick.InMemDb
{
    internal interface IMemDbDefragmenter<T> where T : class, new()
    {
        internal void Defrag();
    }
}
