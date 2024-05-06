using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.MemDb
{
    public class MemDbExpression<T> where T :/* MemDbRecord, */new()
    {
        #region internals
        private ExecuteQuery _executionFunc;
        private Func<T, bool> _filter;
        private Comparison<T> _orderBy;
        private int? _skip;
        private int? _limit;
        #endregion

        #region interface
        public bool HasFilter => _filter is not null;
        public Func<T, bool> Filter => _filter ?? ((x) => true);

        public bool HasOrderBy => _orderBy is not null;
        public Comparison<T> OrderByComparison => _orderBy;

        public bool HasSkip => _skip.HasValue;
        public int SkipCount => _skip.HasValue ? _skip.Value : 0;

        public bool HasLimit => _limit.HasValue;
        public int LimitCount => _limit.HasValue ? _limit.Value : 0;
        #endregion

        #region delegates
        internal delegate T[] ExecuteQuery(MemDbExpression<T> expression, bool deepCopy);
        #endregion

        #region constructors
        internal MemDbExpression(ExecuteQuery executionFunc)
        {
            _executionFunc = executionFunc;
        }
        #endregion

        #region where
        public MemDbExpression<T> Where(Func<T, bool> func)
        {
            _filter = func;
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
        #endregion

        #region skip
        public MemDbExpression<T> Skip(int? count)
        {
            _skip = count;
            return this;
        }
        #endregion

        #region limit
        public MemDbExpression<T> Limit(int? count)
        {
            _limit = count;
            return this;
        }
        #endregion

        #region sum
        public int Sum(Func<T, int> selector)
        {
            int val = _executionFunc(this, false).Sum(selector);
            return val;
        }

        public double Sum(Func<T, double> selector)
        {
            double val = _executionFunc(this, false).Sum(selector);
            return val;
        }

        public decimal Sum(Func<T, decimal> selector)
        {
            decimal val = _executionFunc(this, false).Sum(selector);
            return val;
        }
        #endregion

        #region max
        public Y Max<Y>(Func<T, Y> func)
        {
            Y max = default(Y);
            max = _executionFunc(this, false).Max<T, Y>(func);
            return max;
        }
        #endregion

        #region min
        public Y Min<Y>(Func<T, Y> func)
        {
            Y min = default(Y);
            min = _executionFunc(this, false).Min<T, Y>(func);
            return min;
        }
        #endregion

        #region avg
        public double Avg(Func<T, int> selector)
        {
            double val = _executionFunc(this, false).Average(selector);
            return val;
        }

        public double Avg(Func<T, long> selector)
        {
            double val = _executionFunc(this, false).Average(selector);
            return val;
        }

        public double Avg(Func<T, double> selector)
        {
            double val = _executionFunc(this, false).Average(selector);
            return val;
        }

        public decimal Avg(Func<T, decimal> selector)
        {
            decimal val = _executionFunc(this, false).Average(selector);
            return val;
        }
        #endregion

        #region find all
        public T[] FindAll()
        {
            return _executionFunc(this, true);
        }
        #endregion
    }
}
