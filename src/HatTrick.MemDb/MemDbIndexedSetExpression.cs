using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb
{
    #region i mem db indexed set expression root of T, Y [interface]
    public interface IMemDbIndexedSetExpressionRoot<T, YIndex> where T : class
    {
        public MemDbIndexExpression<T> AnyIsEqual(YIndex key);
        public MemDbIndexExpression<T> AnyIn(params YIndex[] keys);
        public MemDbIndexExpression<T> AnyNotEqual(YIndex key);
        public MemDbIndexExpression<T> AnyIsGreaterThan(YIndex key);
        public MemDbIndexExpression<T> AnyIsLessThan(YIndex key);
        public MemDbIndexExpression<T> AnyIsGreaterThanEqualTo(YIndex key);
        public MemDbIndexExpression<T> AnyIsLessThanEqualTo(YIndex key);
    }
    #endregion

    #region memd bd indexed set expression of T, Y [class]
    //internal class MemDbIndexedSetExpression<T, YIndex> : MemDbIndexExpression<T, YIndex>
    //{
    //}
    #endregion
}
