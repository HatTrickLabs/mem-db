using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.InMemDb.TestHarness
{
    public class NotEqualException<T> : Exception
    {
        #region internals
        private T _left;
        private T _right;
        #endregion

        #region ctors
        public NotEqualException(T left, T right) 
            : base($"{typeof(T).Name} {left} != {right}")
        {
            _left = left;
            _right = right;
        }
        #endregion
    }
}
