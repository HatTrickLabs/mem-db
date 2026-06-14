// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;

namespace HatTrick.Data
{
    internal interface IMemDbDefragmenter
    {
        internal (int stale, int deleted) Defrag();
    }
}
