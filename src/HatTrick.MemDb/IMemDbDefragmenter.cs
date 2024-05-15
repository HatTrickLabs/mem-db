using System;

namespace HatTrick.MemDb
{
    internal interface IMemDbDefragmenter<T> where T : class, new()
    {
        public void Defrag();
    }
}
