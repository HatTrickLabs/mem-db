using System;

namespace HatTrick.MemDb
{
    #region [enum] record state
    public enum RecordState : byte
    {
        Fresh = 0,
        Stale = 1,
        Deleted = 2
    }
    #endregion
}