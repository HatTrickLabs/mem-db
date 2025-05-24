using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb
{
    public enum RelationalOperator
    {
        EqualTo,
        NotEqualTo,
        GreaterThan,
        LessThan,
        GreaterThanEqualTo,
        LessThanEqualTo
    }

    public interface IMemDbIndexExpression<T, Y> where T : class
    {
        public MemDbIndexExpression<T> IsEqualTo(Y key);
        public MemDbIndexExpression<T> IsGreaterThan(Y key);
        public MemDbIndexExpression<T> IsLessThan(Y key);
        public MemDbIndexExpression<T> IsGreaterThanEqualTo(Y key);
        public MemDbIndexExpression<T> IsLessThanEqualTo(Y key);
    }

    public abstract class MemDbIndexExpression<T> where T : class
    {
        #region to array
        public abstract T[] ToArray();
        #endregion
    }

    public class MemDbIndexExpression<T, Y> : MemDbIndexExpression<T>, IMemDbIndexExpression<T, Y>  where T : class
    {
        #region internals
        private string _name;
        private RelationalOperator _relationOp;
        private Y _key;

        private ExecuteQuery _query;
        private ExecuteUpdate _update;
        private ExecuteDelete _delete;
        #endregion

        #region interface
        public string IndexName => _name;

        public RelationalOperator RelationalOperator => _relationOp;

        public Y IndexKey => _key;
        #endregion

        #region delegates
        internal delegate T[] ExecuteQuery(MemDbIndexExpression<T, Y> expression, bool deepCopy);

        internal delegate int ExecuteUpdate(MemDbIndexExpression<T, Y> expression, Action<T> apply);

        internal delegate int ExecuteDelete(MemDbIndexExpression<T, Y> expression);
        #endregion

        #region ctors
        internal MemDbIndexExpression(string name, ExecuteQuery query, ExecuteUpdate update, ExecuteDelete delete) 
        {
            _name = name;
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

        #region to array
        public override  T[] ToArray()
        {
            return _query(this, true);
        }
        #endregion
    }
}
