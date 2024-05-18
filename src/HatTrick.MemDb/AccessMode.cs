using System;

namespace HatTrick.MemDb
{
    #region [enum] access mode
    [Flags]
    public enum AccessMode : int
    {
        ReadWrite = 1,
        ReadOnly = 2,
        AppendOnly = 4
    }
    #endregion
}