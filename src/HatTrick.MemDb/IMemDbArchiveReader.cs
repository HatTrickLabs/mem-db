using System;
using System.Collections.Generic;

namespace HatTrick.Data
{
    internal interface IMemDbArchiveReader
    {
        internal IEnumerable<MemDbRecord<T>> ReadArchive<T>(string datasetName) where T : class;
    }
}
