using System;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    public class MemDbIndex<T,Y> where T : class
    {
        #region internals
        private Func<T, Y> _keyResolver;
        private Dictionary<Y, List<int>> _index;
        #endregion

        #region ctors
        public MemDbIndex(Func<T, Y> keyResolver)
        {
            _keyResolver = keyResolver ?? throw new ArgumentNullException(nameof(keyResolver));
        }
        #endregion

        #region initialize
        internal void Initialize(int capacity)
        {
            _index = new Dictionary<Y, List<int>>(capacity);
        }
        #endregion

        #region apply
        public void Apply(T record)
        {

        }
        #endregion

        #region remove
        public void Remove(T record)
        {

        }
        #endregion
    }
}
