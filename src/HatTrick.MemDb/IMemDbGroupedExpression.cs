using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb
{
    public interface IMemDbGroupedExpression<TKey, T> where T : class
    {
        IMemDbGroupedExpression<TKey, T> Having(Func<IGrouping<TKey, T>, bool> predicate);

        TResult[] Select<TResult>(Func<IGrouping<TKey, T>, TResult> selector);
    }
}
