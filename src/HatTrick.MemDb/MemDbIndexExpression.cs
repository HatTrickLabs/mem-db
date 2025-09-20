using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.Data
{
    #region index relational operator [enum]
    public enum IndexRelationalOperator
    {
        None,
        Equal,
        In,
        NotEqual,
        GreaterThan,
        LessThan,
        GreaterThanEqual,
        LessThanEqual,
        Between,
        NotBetween
    }
    #endregion

    #region i mem db index expression root of T, Y [interface]
    public interface IMemDbIndexExpressionRoot<T, YIndex> where T : class
    {
        public MemDbIndexExpression<T> IsEqualTo(YIndex key);
        public MemDbIndexExpression<T> In(params YIndex[] keySet);
        public MemDbIndexExpression<T> IsNotEqualTo(YIndex key);
        public MemDbIndexExpression<T> IsGreaterThan(YIndex key);
        public MemDbIndexExpression<T> IsLessThan(YIndex key);
        public MemDbIndexExpression<T> IsGreaterThanEqualTo(YIndex key);
        public MemDbIndexExpression<T> IsLessThanEqualTo(YIndex key);
        public MemDbIndexExpression<T> IsBetween(YIndex lower, YIndex upper);
        public MemDbIndexExpression<T> IsNotBetween(YIndex lower, YIndex upper);
    }
    #endregion

    #region mem db index expression of T [class]
    public abstract class MemDbIndexExpression<T> where T : class
    {
        #region internals
        private string _indexName;
        #endregion

        #region interface
        public string IndexName => _indexName;
        #endregion

        #region ctors
        protected MemDbIndexExpression(string indexName)
        {
            _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        }
        #endregion

        #region count
        public abstract int Count();
        #endregion

        #region order by
        public abstract MemDbIndexExpression<T> OrderBy(Comparison<T> comparison);
        #endregion

        #region group by
        public abstract IMemDbGroupedExpression<TKey, T> GroupBy<TKey>(Func<T, TKey> keySelector);
        #endregion

        #region skip
        public abstract MemDbIndexExpression<T> Skip(int count);
        #endregion

        #region limit
        public abstract MemDbIndexExpression<T> Limit(int count);
        #endregion

        #region select
        public abstract X[] Select<X>(Func<T, X> selector);
        #endregion

        #region select distinct
        public abstract X[] SelectDistinct<X>(Func<T, X> selector);
        #endregion

        #region sum
        public abstract int Sum(Func<T, int> selector);

        public abstract long Sum(Func<T, long> selector);

        public abstract float Sum(Func<T, float> selector);

        public abstract double Sum(Func<T, double> selector);

        public abstract decimal Sum(Func<T, decimal> selector);
        #endregion

        #region max
        public abstract Y Max<Y>(Func<T, Y> func);
        #endregion

        #region min
        public abstract Y Min<Y>(Func<T, Y> func);
        #endregion

        #region avg
        public abstract double Avg(Func<T, int> selector);

        public abstract double Avg(Func<T, long> selector);

        public abstract float Avg(Func<T, float> selector);

        public abstract double Avg(Func<T, double> selector);

        public abstract decimal Avg(Func<T, decimal> selector);
        #endregion

        #region to array
        public abstract T[] ToArray();
        #endregion

        #region update
        public abstract int Update(Action<T> apply);
        #endregion

        #region delete
        public abstract int Delete();
        #endregion
    }
    #endregion

    #region mem db index expression of T, Y [class]
    public class MemDbIndexExpression<T, YIndex> : MemDbIndexExpression<T> where T : class
    {
        #region internals
        private int _relationOp;
        private YIndex _key;
        private YIndex[] _keySet;

        private Comparison<T> _orderBy;
        private int? _skip;
        private int? _limit;

        internal ExecuteQuery _query;
        internal ExecuteUpdate _update;
        internal ExecuteDelete _delete;
        #endregion

        #region interface
        public int RelationalOperator
        {
            get => _relationOp;
            protected set => _relationOp = value;
        }

        public YIndex IndexKey
        { 
            get => _key;
            protected set => _key = value;
        }

        public YIndex[] IndexKeySet
        {
            get => _keySet;
            protected set => _keySet = value;
        }

        internal ExecuteQuery Query => _query;

        internal bool HasOrderBy => _orderBy is not null;

        public Comparison<T> OrderByComparison => _orderBy;

        internal bool HasSkip => _skip.HasValue;

        internal int SkipCount => _skip ?? 0;

        internal bool HasLimit => _limit.HasValue;

        internal int LimitCount => _limit ?? int.MaxValue;
        #endregion

        #region delegates
        internal delegate T[] ExecuteQuery(MemDbIndexExpression<T, YIndex> expression, bool deepCopy);

        internal delegate int ExecuteUpdate(MemDbIndexExpression<T, YIndex> expression, Action<T> apply);

        internal delegate int ExecuteDelete(MemDbIndexExpression<T, YIndex> expression);
        #endregion

        #region ctors
        internal MemDbIndexExpression(string indexName, ExecuteQuery query, ExecuteUpdate update, ExecuteDelete delete) : base(indexName)
        {
            _query = query;
            _update = update;
            _delete = delete;
        }
        #endregion

        #region count
        public override int Count()
        {
            T[] set = _query(this, false);
            return set.Length;
        }
        #endregion

        #region order by
        public override MemDbIndexExpression<T> OrderBy(Comparison<T> comparison)
        {
            if (_orderBy is not null)
                throw new InvalidOperationException($"{nameof(MemDbException)} already contains a {nameof(OrderBy)} comparison.");

            _orderBy = comparison;
            return this;
        }
        #endregion

        #region group by
        public override IMemDbGroupedExpression<TKey, T> GroupBy<TKey>(Func<T, TKey> keySelector)
        {
            if (keySelector is null)
                throw new ArgumentNullException(nameof(keySelector));

            return new MemDbGroupedIndexExpression<TKey, T, YIndex>(keySelector, this);
        }
        #endregion

        #region skip
        public override MemDbIndexExpression<T> Skip(int count)
        {
            if (_skip is not null)
                throw new InvalidOperationException($"{nameof(MemDbException)} already contains a {nameof(Skip)} count.");

            _skip = count;
            return this;
        }
        #endregion

        #region limit
        public override MemDbIndexExpression<T> Limit(int count)
        {
            if (_limit is not null)
                throw new InvalidOperationException($"{nameof(MemDbException)} already contains a {nameof(Limit)} count.");

            _limit = count;
            return this;
        }
        #endregion

        #region select
        public override X[] Select<X>(Func<T, X> selector)
        {
            Type t = typeof(X);
            bool allowShallowCopy = t == typeof(string) || t.IsValueType;

            //we only want to incur the cost of deep copy if necessary...
            //returning non ref types does not expose any risk.
            T[] set = _query(this, !allowShallowCopy);
            if (set.Length == 0)
                return Array.Empty<X>();

            X[] result = set.Select(selector).ToArray();

            return result;
        }
        #endregion

        #region select distinct
        public override X[] SelectDistinct<X>(Func<T, X> selector)
        {
            Type t = typeof(YIndex);
            bool allowShallowCopy = t == typeof(string) || t.IsValueType;

            //we only want to incur the cost of deep copy if necessary...
            //returning non ref types does not expose any risk.
            T[] set = _query(this, !allowShallowCopy);
            if (set.Length == 0)
                return Array.Empty<X>();

            X[] result = set.Select(selector).Distinct().ToArray();

            return result;
        }
        #endregion

        #region sum
        public override int Sum(Func<T, int> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            int val = set.Sum(selector);
            return val;
        }

        public override long Sum(Func<T, long> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            long val = set.Sum(selector);
            return val;
        }

        public override float Sum(Func<T, float> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            float val = set.Sum(selector);
            return val;
        }

        public override double Sum(Func<T, double> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            double val = set.Sum(selector);
            return val;
        }

        public override decimal Sum(Func<T, decimal> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            decimal val = set.Sum(selector);
            return val;
        }
        #endregion

        #region max
        public override X Max<X>(Func<T, X> func)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return default(X);

            X max = default(X);
            max = set.Max<T, X>(func);
            return max;
        }
        #endregion

        #region min
        public override X Min<X>(Func<T, X> func)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return default(X);

            X min = default(X);
            min = set.Min<T, X>(func);
            return min;
        }
        #endregion

        #region avg
        public override double Avg(Func<T, int> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            double val = set.Average(selector);
            return val;
        }

        public override double Avg(Func<T, long> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            double val = set.Average(selector);
            return val;
        }

        public override float Avg(Func<T, float> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            float val = set.Average(selector);
            return val;
        }

        public override double Avg(Func<T, double> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            double val = set.Average(selector);
            return val;
        }

        public override decimal Avg(Func<T, decimal> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            decimal val = set.Average(selector);
            return val;
        }
        #endregion

        #region to array
        public override T[] ToArray()
        {
            return _query(this, true);
        }
        #endregion

        #region update
        public override int Update(Action<T> apply)
        {
            if (apply is null)
                throw new ArgumentNullException(nameof(apply));

            return _update(this, apply);
        }
        #endregion

        #region delete
        public override int Delete()
        {
            return _delete(this);
        }
        #endregion
    }
    #endregion

    #region mem db indexed expression of T, Y [class]
    public class MemDbIndexedExpression<T, YIndex> 
    : MemDbIndexExpression<T, YIndex>, IMemDbIndexExpressionRoot<T, YIndex> where T 
    : class
    {
        #region ctors
        internal MemDbIndexedExpression(string indexName, ExecuteQuery query, ExecuteUpdate update, ExecuteDelete delete) 
        : base(indexName, query, update, delete)
        { }
        #endregion

        #region is equal to
        public MemDbIndexExpression<T> IsEqualTo(YIndex key)
        {
            base.RelationalOperator = (int)IndexRelationalOperator.Equal;
            base.IndexKey = key;
            return this;
        }
        #endregion

        #region in
        public MemDbIndexExpression<T> In(params YIndex[] keys)
        {
            if (keys is null)
                throw new ArgumentNullException(nameof(keys));

            base.RelationalOperator = (int)IndexRelationalOperator.In;
            base.IndexKeySet = keys;
            return this;
        }
        #endregion

        #region is not equal to
        public MemDbIndexExpression<T> IsNotEqualTo(YIndex key)
        {
            base.RelationalOperator = (int)IndexRelationalOperator.NotEqual;
            base.IndexKey = key;
            return this;
        }
        #endregion

        #region is greater than
        public MemDbIndexExpression<T> IsGreaterThan(YIndex key)
        {
            base.RelationalOperator = (int)IndexRelationalOperator.GreaterThan;
            base.IndexKey = key;
            return this;
        }
        #endregion

        #region is less than
        public MemDbIndexExpression<T> IsLessThan(YIndex key)
        {
            base.RelationalOperator = (int)IndexRelationalOperator.LessThan;
            base.IndexKey = key;
            return this;
        }
        #endregion

        #region is greater than equal to
        public MemDbIndexExpression<T> IsGreaterThanEqualTo(YIndex key)
        {
            base.RelationalOperator = (int)IndexRelationalOperator.GreaterThanEqual;
            base.IndexKey = key;
            return this;
        }
        #endregion

        #region is less than equal to
        public MemDbIndexExpression<T> IsLessThanEqualTo(YIndex key)
        {
            base.RelationalOperator = (int)IndexRelationalOperator.LessThanEqual;
            base.IndexKey = key;
            return this;
        }
        #endregion

        #region is between
        public MemDbIndexExpression<T> IsBetween(YIndex lower, YIndex upper)
        {
            base.RelationalOperator = (int)IndexRelationalOperator.Between;
            base.IndexKeySet = [lower, upper];
            return this;
        }
        #endregion

        #region is not between
        public MemDbIndexExpression<T> IsNotBetween(YIndex lower, YIndex upper)
        {
            base.RelationalOperator = (int)IndexRelationalOperator.NotBetween;
            base.IndexKeySet = [lower, upper];
            return this;
        }
        #endregion
    }
    #endregion
}
