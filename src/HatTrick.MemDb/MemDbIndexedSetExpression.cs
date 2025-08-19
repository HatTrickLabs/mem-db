using System;

namespace HatTrick.InMemDb
{
    #region index relational operator [enum]
    public enum IndexedSetRelationalOperator
    {
        None,
        AnyIsEqual,
        AnyIn,
        AnyNotEqual,
        AllNotEqual,
        AllNotIn,
        AnyGreaterThan,
        AnyLessThan,
        AnyGreaterThanEqual,
        AnyLessThanEqual
    }
    #endregion

    #region i mem db indexed set expression root of T, Y [interface]
    public interface IMemDbIndexedSetExpressionRoot<T, YIndex> where T : class
    {
        public MemDbIndexExpression<T> AnyIsEqual(YIndex key);
        public MemDbIndexExpression<T> AnyIn(params YIndex[] keys);
        public MemDbIndexExpression<T> AnyNotEqual(YIndex key);
        public MemDbIndexExpression<T> AllNotEqual(YIndex key);
        public MemDbIndexExpression<T> AllNotIn(params YIndex[] keys);
        public MemDbIndexExpression<T> AnyIsGreaterThan(YIndex key);
        public MemDbIndexExpression<T> AnyIsLessThan(YIndex key);
        public MemDbIndexExpression<T> AnyIsGreaterThanEqual(YIndex key);
        public MemDbIndexExpression<T> AnyIsLessThanEqual(YIndex key);
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
            base.RelationalOperator = (int)IndexedSetRelationalOperator.AnyIsEqual;
            base.IndexKey = key;
            return this;
        }
        #endregion

        #region any in
        public MemDbIndexExpression<T> AnyIn(params YIndex[] keys)
        {
            if (keys is null)
                throw new ArgumentNullException(nameof(keys));

            base.RelationalOperator = (int)IndexedSetRelationalOperator.AnyIn;
            base.IndexKeySet = keys;
            return this;
        }
        #endregion

        #region any not equal
        public MemDbIndexExpression<T> AnyNotEqual(YIndex key)
        {
            base.RelationalOperator = (int)IndexedSetRelationalOperator.AnyNotEqual;
            base.IndexKey = key;
            return this;
        }
        #endregion

        #region all not equal
        public MemDbIndexExpression<T> AllNotEqual(YIndex key)
        {
            base.RelationalOperator = (int)IndexedSetRelationalOperator.AllNotEqual;
            base.IndexKey = key;
            return this;
        }
        #endregion

        #region all not in
        public MemDbIndexExpression<T> AllNotIn(params YIndex[] keys)
        {
            base.RelationalOperator = (int)IndexedSetRelationalOperator.AllNotIn;
            base.IndexKeySet = keys;
            return this;
        }
        #endregion

        #region any is greater than
        public MemDbIndexExpression<T> AnyIsGreaterThan(YIndex key)
        {
            base.RelationalOperator = (int)IndexedSetRelationalOperator.AnyGreaterThan;
            base.IndexKey = key;
            return this;
        }
        #endregion

        #region any is less than
        public MemDbIndexExpression<T> AnyIsLessThan(YIndex key)
        {
            base.RelationalOperator = (int)IndexedSetRelationalOperator.AnyLessThan;
            base.IndexKey = key;
            return this;
        }
        #endregion

        #region any is greater than equal to
        public MemDbIndexExpression<T> AnyIsGreaterThanEqual(YIndex key)
        {
            base.RelationalOperator = (int)IndexedSetRelationalOperator.AnyGreaterThanEqual;
            base.IndexKey = key;
            return this;
        }
        #endregion

        #region any is less than equal
        public MemDbIndexExpression<T> AnyIsLessThanEqual(YIndex key)
        {
            base.RelationalOperator = (int)IndexedSetRelationalOperator.AnyLessThanEqual;
            base.IndexKey = key;
            return this;
        }
        #endregion
    }
    #endregion
}
