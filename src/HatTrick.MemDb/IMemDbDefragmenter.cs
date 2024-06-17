using System;

namespace HatTrick.InMemDb
{
    internal interface IMemDbDefragmenter<T> where T : class, new()
    {
        public void Defrag();
    }
}
