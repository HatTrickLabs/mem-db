using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb
{
    internal class MemDbIndexAccessor<T> where T : class
    {
        public static int[] ResolvePointers<YIndex>(MemDbIndex<T> index, MemDbIndexExpression<T, YIndex> expression) where YIndex : IConvertible
        {
            MemDbIndex<T, YIndex> idx = index.Of<YIndex>();
            if (idx is MemDbIndexedSet<T, YIndex> indexedSet)
                return MemDbIndexAccessor<T>.ResolvePointers<YIndex>(indexedSet, expression);

            int[] pointers = null;
            IndexRelationalOperator op = (IndexRelationalOperator)expression.RelationalOperator;
            switch (op)
            {
                case IndexRelationalOperator.Equal:
                    pointers = idx.EqualTo(expression.IndexKey);
                    break;
                case IndexRelationalOperator.In:
                    pointers = idx.In(expression.IndexKeySet);
                    break;
                case IndexRelationalOperator.NotEqual:
                    pointers = idx.NotEqualTo(expression.IndexKey);
                    break;
                case IndexRelationalOperator.GreaterThan:
                    pointers = idx.GreaterThan(expression.IndexKey);
                    break;
                case IndexRelationalOperator.LessThan:
                    pointers = idx.LessThan(expression.IndexKey);
                    break;
                case IndexRelationalOperator.GreaterThanEqual:
                    pointers = idx.GreaterThanEqualTo(expression.IndexKey);
                    break;
                case IndexRelationalOperator.LessThanEqual:
                    pointers = idx.LessThanEqualTo(expression.IndexKey);
                    break;
                default:
                    throw new NotImplementedException($"Index expression for {op} not implemented.");
            }

            return pointers;
        }

        public static int[] ResolvePointers<YIndex>(MemDbIndexedSet<T, YIndex> index, MemDbIndexExpression<T, YIndex> expression) where YIndex : IConvertible
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
                case IndexedSetRelationalOperator.AnyLessThan:
                    pointers = index.AnyIsLessThan(expression.IndexKey);
                    break;
                case IndexedSetRelationalOperator.AnyGreaterThanEqual:
                    pointers = index.AnyIsGreaterThanEqualTo(expression.IndexKey);
                    break;
                case IndexedSetRelationalOperator.AnyLessThanEqual:
                    pointers = index.AnyIsLessThanEqualTo(expression.IndexKey);
                    break;
                default:
                    throw new NotImplementedException($"Index expression for {op} not implemented.");
            }

            return pointers;
        }
    }
}
