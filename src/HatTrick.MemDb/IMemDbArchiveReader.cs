using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb
{
    internal interface IMemDbArchiveReader
    {
        internal IEnumerable<MemDbRecord<T>> ReadArchive<T>(string datasetName) where T : class, new ();
    }
}
