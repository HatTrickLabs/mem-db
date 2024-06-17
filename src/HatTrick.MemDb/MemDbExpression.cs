using System;
using System.Collections.Generic;
using System.Linq;

namespace HatTrick.InMemDb
{
    public class MemDbExpression<T> where T : class, new()
    {
        #region internals
        private ExecuteQuery _query;
        private ExecuteUpdate _update;
        private ExecuteDelete _delete;

        private Func<T, bool> _filter;
        private Comparison<T> _orderBy;
        private int? _skip;
        private int? _limit;
        #endregion

        #region interface
        internal MemDbExpression<T>.ExecuteQuery Query  => _query;

        internal bool HasFilter => _filter is not null;
        internal Func<T, bool> Filter => _filter ?? ((x) => true);

        internal bool HasOrderBy => _orderBy is not null;
        internal Comparison<T> OrderByComparison => _orderBy;

        internal bool HasSkip => _skip.HasValue;
        internal int SkipCount => _skip.HasValue ? _skip.Value : 0;

        internal bool HasLimit => _limit.HasValue;
        internal int LimitCount => _limit.HasValue ? _limit.Value : 0;
        #endregion

        #region delegates
        internal delegate T[] ExecuteQuery(MemDbExpression<T> expression, bool deepCopy);

        internal delegate int ExecuteUpdate(MemDbExpression<T> expression, Action<T> apply);

        internal delegate int ExecuteDelete(MemDbExpression<T> expression);
        #endregion

        #region constructors
        internal MemDbExpression(ExecuteQuery query, ExecuteUpdate update, ExecuteDelete delete)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _update = update ?? throw new ArgumentNullException(nameof(update)); ;
            _delete = delete ?? throw new ArgumentNullException(nameof(delete)); ;
        }
        #endregion

        #region where
        public MemDbExpression<T> Where(Func<T, bool> predicate)
        {
            _filter = predicate;
            return this;
        }
        #endregion

        #region order by
        public MemDbExpression<T> OrderBy(Comparison<T> comparison)
        {
            _orderBy = comparison;
            return this;
        }
        #endregion

        #region group by
        public IMemDbGroupedExpression<TKey, T> GroupBy<TKey>(Func<T, TKey> keySelector)
        {
            if (keySelector is null)
                throw new ArgumentNullException(nameof(keySelector));

            return new MemDbGroupedExpression<TKey, T>(keySelector, this);
        }
        #endregion

        #region skip
        public MemDbExpression<T> Skip(int count)
        {
            _skip = count;
            return this;
        }
        #endregion

        #region limit
        public MemDbExpression<T> Limit(int count)
        {
            _limit = count;
            return this;
        }
        #endregion

        #region sum
        public int Sum(Func<T, int> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            int val = set.Sum(selector);
            return val;
        }

        public long Sum(Func<T, long> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            long val = set.Sum(selector);
            return val;
        }

        public float Sum(Func<T, float> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            float val = set.Sum(selector);
            return val;
        }

        public double Sum(Func<T, double> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            double val = set.Sum(selector);
            return val;
        }

        public decimal Sum(Func<T, decimal> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            decimal val = set.Sum(selector);
            return val;
        }
        #endregion

        #region max
        public Y Max<Y>(Func<T, Y> func)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return default(Y);

            Y max = default(Y);
            max = set.Max<T, Y>(func);
            return max;
        }
        #endregion

        #region min
        public Y Min<Y>(Func<T, Y> func)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return default(Y);

            Y min = default(Y);
            min = set.Min<T, Y>(func);
            return min;
        }
        #endregion

        #region avg
        public double Avg(Func<T, int> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            double val = set.Average(selector);
            return val;
        }

        public double Avg(Func<T, long> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            double val = set.Average(selector);
            return val;
        }

        public float Avg(Func<T, float> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            float val = set.Average(selector);
            return val;
        }

        public double Avg(Func<T, double> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            double val = set.Average(selector);
            return val;
        }

        public decimal Avg(Func<T, decimal> selector)
        {
            T[] set = _query(this, false);
            if (set.Length == 0)
                return 0;

            decimal val = set.Average(selector);
            return val;
        }
        #endregion

        #region select
        public Y[] Select<Y>(Func<T, Y> selector)
        {
            Type t = typeof(Y);
            bool allowShallowCopy = t == typeof(string) || t.IsValueType;
            
            //we only want to incur the cost of deep copy if necessary...
            //returning non ref types does not expose any risk.
            T[] set = _query(this, !allowShallowCopy);
            if (set.Length == 0)
                return Array.Empty<Y>();

            Y[] result = set.Select(selector).ToArray();

            return result;
        }
        #endregion

        #region select distinct
        public Y[] SelectDistinct<Y>(Func<T, Y> selector)// where Y : IConvertible
        {
            Type t = typeof(Y);
            bool allowShallowCopy = t == typeof(string) || t.IsValueType;

            //we only want to incur the cost of deep copy if necessary...
            //returning non ref types does not expose any risk.
            T[] set = _query(this, !allowShallowCopy);
            if (set.Length == 0)
                return Array.Empty<Y>();

            Y[] result = set.Select(selector).Distinct().ToArray();

            return result;
        }
        #endregion

        #region to array
        public T[] ToArray()
        {
            return _query(this, true);
        }
        #endregion

        #region update
        public int Update(Action<T> apply)
        {
            if (apply is null)
                throw new ArgumentNullException(nameof(apply));

            return _update(this, apply);
        }
        #endregion

        #region delete
        public int Delete()
        {
            return _delete(this);
        }
        #endregion
    }
}
