using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.Data
{
    internal class MemDbIndexAccessor<T> where T : class
    {
        #region resolve pointers
        public static int[] ResolvePointers<YIndex>(MemDbIndex<T, YIndex> index, MemDbIndexExpression<T, YIndex> expression) where YIndex : IComparable
        {
            int[] pointers = null;
            if (index is MemDbIndexedSet<T, YIndex> idxSet)
                pointers = MemDbIndexAccessor<T>.ResolveIndexedSetPointers(idxSet, expression);

            else
                pointers = MemDbIndexAccessor<T>.ResolveIndexPointers(index, expression);

            return pointers;
        }
        #endregion

        #region resolve index pointers
        private static int[] ResolveIndexPointers<YIndex>(MemDbIndex<T, YIndex> index, MemDbIndexExpression<T, YIndex> expression) where YIndex : IComparable
        {
            int[] pointers = null;
            IndexRelationalOperator op = (IndexRelationalOperator)expression.RelationalOperator;
            switch (op)
            {
                case IndexRelationalOperator.Equal:
                    pointers = index.EqualTo(expression.IndexKey);
                    break;
                case IndexRelationalOperator.In:
                    pointers = index.In(expression.IndexKeySet);
                    break;
                case IndexRelationalOperator.NotEqual:
                    pointers = index.NotEqualTo(expression.IndexKey);
                    break;
                case IndexRelationalOperator.GreaterThan:
                    pointers = index.GreaterThan(expression.IndexKey);
                    break;
                case IndexRelationalOperator.LessThan:
                    pointers = index.LessThan(expression.IndexKey);
                    break;
                case IndexRelationalOperator.GreaterThanEqual:
                    pointers = index.GreaterThanEqualTo(expression.IndexKey);
                    break;
                case IndexRelationalOperator.LessThanEqual:
                    pointers = index.LessThanEqualTo(expression.IndexKey);
                    break;
                case IndexRelationalOperator.Between:
                    pointers = index.Between(expression.IndexKeySet[0], expression.IndexKeySet[1]);
                    break;
                case IndexRelationalOperator.NotBetween:
                    pointers = index.NotBetween(expression.IndexKeySet[0], expression.IndexKeySet[1]);
                    break;
                default:
                    throw new NotImplementedException($"Index expression for {op} not implemented.");
            }

            return pointers;
        }
        #endregion

        #region resolve indexed set pointers
        private static int[] ResolveIndexedSetPointers<YIndex>(MemDbIndexedSet<T, YIndex> index, MemDbIndexExpression<T, YIndex> expression) where YIndex : IComparable
        {
            int[] pointers = null;
            IndexedSetRelationalOperator op = (IndexedSetRelationalOperator)expression.RelationalOperator;
            switch (op)
            {
                case IndexedSetRelationalOperator.AnyIsEqual:
                    pointers = index.AnyIsEqual(expression.IndexKey);
                    break;
                case IndexedSetRelationalOperator.AnyIn:
                    pointers = index.AnyIn(expression.IndexKeySet);
                    break;
                case IndexedSetRelationalOperator.AnyNotEqual:
                    pointers = index.AnyNotEqual(expression.IndexKey);
                    break;
                case IndexedSetRelationalOperator.AllNotEqual:
                    pointers = index.AllNotEqual(expression.IndexKey);
                    break;
                case IndexedSetRelationalOperator.AllNotIn:
                    pointers = index.AllNotIn(expression.IndexKeySet);
                    break;
                case IndexedSetRelationalOperator.AnyGreaterThan:
                    pointers = index.AnyIsGreaterThan(expression.IndexKey);
                    break;
                case IndexedSetRelationalOperator.AllGreaterThan:
                    pointers = index.AllIsGreaterThan(expression.IndexKey);
                    break;
                case IndexedSetRelationalOperator.AnyLessThan:
                    pointers = index.AnyIsLessThan(expression.IndexKey);
                    break;
                case IndexedSetRelationalOperator.AllLessThan:
                    pointers = index.AllIsLessThan(expression.IndexKey);
                    break;
                case IndexedSetRelationalOperator.AnyGreaterThanEqual:
                    pointers = index.AnyIsGreaterThanEqualTo(expression.IndexKey);
                    break;
                case IndexedSetRelationalOperator.AllGreaterThanEqual:
                    pointers = index.AllIsGreaterThanEqualTo(expression.IndexKey);
                    break;
                case IndexedSetRelationalOperator.AnyLessThanEqual:
                    pointers = index.AnyIsLessThanEqualTo(expression.IndexKey);
                    break;
                case IndexedSetRelationalOperator.AllLessThanEqual:
                    pointers = index.AllIsLessThanEqualTo(expression.IndexKey);
                    break;
                default:
                    throw new NotImplementedException($"Index expression for {op} not implemented.");
            }

            return pointers;
        }
        #endregion
    }
}
