using System;
using System.Linq;

namespace HatTrick.Data
{
    public interface IMemDbGroupedExpression<TKey, T> where T : class
    {
        IMemDbGroupedExpression<TKey, T> Having(Func<IGrouping<TKey, T>, bool> predicate);

        TResult[] Select<TResult>(Func<IGrouping<TKey, T>, TResult> selector);
    }
}
