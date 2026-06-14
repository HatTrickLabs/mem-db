// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.Collections.Generic;

namespace HatTrick.Data
{
    internal interface IMemDbArchiveReader
    {
        internal IEnumerable<MemDbRecord<T>> ReadArchive<T>(string datasetName) where T : class;
    }
}
