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

    #region mem db index expression of T, Y [class]
    public partial class MemDbIndexedSetExpression<T, YIndex>
    : MemDbIndexExpression<T, YIndex>, IMemDbIndexedSetExpressionRoot<T, YIndex> where T
    : class
    {
        #region ctors
        internal MemDbIndexedSetExpression(string indexName, ExecuteQuery query, ExecuteUpdate update, ExecuteDelete delete)
        : base(indexName, query, update, delete)
        { }
        #endregion

        #region any is equal
        public MemDbIndexExpression<T> AnyIsEqual(YIndex key)
        {
            _relationOp = IndexRelationalOperator.EqualTo;
            _key = key;
            return this;
        }
        #endregion

        #region any in
        public MemDbIndexExpression<T> AnyIn(params YIndex[] keys)
        {
            if (keys is null)
                throw new ArgumentNullException(nameof(keys));

            _relationOp = IndexRelationalOperator.In;
            _keySet = keys;
            return this;
        }
        #endregion

        #region any not equal
        public MemDbIndexExpression<T> AnyNotEqual(YIndex key)
        {
            _relationOp = IndexRelationalOperator.NotEqualTo;
            _key = key;
            return this;
        }
        #endregion

        #region any is greater than
        public MemDbIndexExpression<T> AnyIsGreaterThan(YIndex key)
        {
            _relationOp = IndexRelationalOperator.GreaterThan;
            _key = key;
            return this;
        }
        #endregion

        #region any is less than
        public MemDbIndexExpression<T> AnyIsLessThan(YIndex key)
        {
            _relationOp = IndexRelationalOperator.LessThan;
            _key = key;
            return this;
        }
        #endregion

        #region any is greater than equal to
        public MemDbIndexExpression<T> AnyIsGreaterThanEqualTo(YIndex key)
        {
            _relationOp = IndexRelationalOperator.GreaterThanEqualTo;
            _key = key;
            return this;
        }
        #endregion

        #region any is less than equal
        public MemDbIndexExpression<T> AnyIsLessThanEqualTo(YIndex key)
        {
            _relationOp = IndexRelationalOperator.LessThanEqualTo;
            _key = key;
            return this;
        }
        #endregion
    }
    #endregion
}
