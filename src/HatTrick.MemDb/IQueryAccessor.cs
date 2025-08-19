using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb
{
    internal interface IQueryAccessor<T> where T : class
    {
        public MemDbExpression<T> Query();
    }
}
