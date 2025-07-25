using System;

namespace HatTrick.InMemDb
{
    internal interface IMemDbDefragmenter
    {
        internal (int stale, int deleted) Defrag();
    }
}
