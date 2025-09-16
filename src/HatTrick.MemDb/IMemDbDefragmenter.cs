using System;

namespace HatTrick.Data
{
    internal interface IMemDbDefragmenter
    {
        internal (int stale, int deleted) Defrag();
    }
}
