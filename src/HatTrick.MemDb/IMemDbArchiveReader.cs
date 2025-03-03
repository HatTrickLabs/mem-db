using System;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    internal interface IMemDbArchiveReader
    {
        internal IEnumerable<MemDbRecord<T>> ReadArchive<T>(string datasetName) where T : class;
    }
}
