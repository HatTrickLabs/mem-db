using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb
{
    #region relational operator [enum]
    public enum RelationalOperator
    {
        EqualTo,
        NotEqualTo,
        GreaterThan,
        LessThan,
        GreaterThanEqualTo,
        LessThanEqualTo
    }
    #endregion

    #region i mem db index expression root of T, Y [interface]
    public interface IMemDbIndexExpressionRoot<T, Y> where T : class
    {
        public MemDbIndexExpression<T> IsEqualTo(Y key);
        public MemDbIndexExpression<T> IsNotEqualTo(Y key);
        public MemDbIndexExpression<T> IsGreaterThan(Y key);
        public MemDbIndexExpression<T> IsLessThan(Y key);
        public MemDbIndexExpression<T> IsGreaterThanEqualTo(Y key);
        public MemDbIndexExpression<T> IsLessThanEqualTo(Y key);
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
    public class MemDbIndexExpression<T, Y> : MemDbIndexExpression<T>, IMemDbIndexExpressionRoot<T, Y> where T : class
    {
        #region internals
        private RelationalOperator _relationOp;
        private Y _key;

        private ExecuteQuery _query;
        private ExecuteUpdate _update;
        private ExecuteDelete _delete;
        #endregion

        #region interface
        public RelationalOperator RelationalOperator => _relationOp;

        public Y IndexKey => _key;
        #endregion

        #region delegates
        internal delegate T[] ExecuteQuery(MemDbIndexExpression<T, Y> expression, bool deepCopy);

        internal delegate int ExecuteUpdate(MemDbIndexExpression<T, Y> expression, Action<T> apply);

        internal delegate int ExecuteDelete(MemDbIndexExpression<T, Y> expression);
        #endregion

        #region ctors
        internal MemDbIndexExpression(string indexName, ExecuteQuery query, ExecuteUpdate update, ExecuteDelete delete) : base(indexName)
        {
            _query = query;
            _update = update;
            _delete = delete;
        }
        #endregion

        #region is equal to
        public MemDbIndexExpression<T> IsEqualTo(Y key)
        {
            _relationOp = RelationalOperator.EqualTo;
            _key = key;
            return this;
        }
        #endregion

        #region is not equal to
        public MemDbIndexExpression<T> IsNotEqualTo(Y key)
        {
            _relationOp = RelationalOperator.NotEqualTo;
            _key = key;
            return this;
        }
        #endregion

        #region is greater than
        public MemDbIndexExpression<T> IsGreaterThan(Y key)
        {
            _relationOp = RelationalOperator.GreaterThan;
            _key = key;
            return this;
        }
        #endregion

        #region is less than
        public MemDbIndexExpression<T> IsLessThan(Y key)
        {
            _relationOp = RelationalOperator.LessThan;
            _key = key;
            return this;
        }
        #endregion

        #region is greater than equal to
        public MemDbIndexExpression<T> IsGreaterThanEqualTo(Y key)
        {
            _relationOp = RelationalOperator.GreaterThanEqualTo;
            _key = key;
            return this;
        }
        #endregion

        #region is less than equal to
        public MemDbIndexExpression<T> IsLessThanEqualTo(Y key)
        {
            _relationOp = RelationalOperator.LessThanEqualTo;
            _key = key;
            return this;
        }
        #endregion

        #region count
        public override int Count()
        {
            T[] set = _query(this, false);
            return set.Length;
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
            Type t = typeof(Y);
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
}
