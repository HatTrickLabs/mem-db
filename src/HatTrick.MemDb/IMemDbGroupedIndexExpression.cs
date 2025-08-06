using System;
using System.Linq;

namespace HatTrick.InMemDb
{
    public interface IMemDbGroupedIndexExpression<TKey, T, YIndex> where T : class
    {
        IMemDbGroupedIndexExpression<TKey, T, YIndex> Having(Func<IGrouping<TKey, T>, bool> predicate);

        TResult[] Select<TResult>(Func<IGrouping<TKey, T>, TResult> selector);
    }
}
