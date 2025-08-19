using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb
{
    public interface IIndexedQueryAccessor<T> where T : class
    {
        public IMemDbIndexExpressionRoot<T, YIndex> QueryViaIndex<YIndex>(string indexName) where YIndex : IConvertible;

        public IMemDbIndexedSetExpressionRoot<T, YIndex> QueryViaIndexedSet<YIndex>(string indexName) where YIndex : IConvertible;
    }
}
