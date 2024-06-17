using System;
using System.Linq;

namespace HatTrick.InMemDb
{
    public class MemDbGroupedExpression<TKey, T> : IMemDbGroupedExpression<TKey, T> where T : class, new()
    {
        #region internals
        private Func<T, TKey> _keySelector;
        private MemDbExpression<T> _expression;
        private Func<IGrouping<TKey, T>, bool> _having;
        #endregion

        #region interface
        public bool HasHaving => _having is not null;
        public Func<IGrouping<TKey, T>, bool> HavingFilter => _having;
        #endregion

        #region ctors
        internal MemDbGroupedExpression(Func<T, TKey> keySelector, MemDbExpression<T> expression)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }
        #endregion

        #region having
        public IMemDbGroupedExpression<TKey, T> Having(Func<IGrouping<TKey, T>, bool> predicate)
        {
            _having = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }
        #endregion

        #region select
        public TResult[] Select<TResult>(Func<IGrouping<TKey, T>, TResult> selector)
        {
            Type t = typeof(TResult);
            bool allowShallowCopy = t == typeof(string) || t.IsValueType;

            //we only want to incur the cost of deep copy if necessary...
            //returning non ref types does not expose any risk.
            T[] set = _expression.Query(_expression, !allowShallowCopy);
            if (set.Length == 0)
                return Array.Empty<TResult>();

            var grouping = set.GroupBy(_keySelector);

            if (this.HasHaving)
                return grouping.Where(_having).Select(selector).ToArray();
            else
                return grouping.Select(selector).ToArray();
        }
        #endregion
    }
}
