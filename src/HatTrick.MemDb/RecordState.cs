using System;

namespace HatTrick.Data
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