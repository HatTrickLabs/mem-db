// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

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