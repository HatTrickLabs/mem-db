using System;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    public class MemDbIndex<T,Y> where T : class
    {
        #region internals
        private string _name;
        private Func<T, Y> _keyResolver;
        private Dictionary<Y, List<int>> _index;
        #endregion

        #region ctors
        public MemDbIndex(string Name, Func<T, Y> keyResolver)
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
        public void Apply(T record, int pointer)
        {
            Y key = _keyResolver(record);
            List<int> set = null;
            if (_index.TryGetValue(key, out set))
            {
                set.Add(pointer);
            }
            else
            {
                set = new List<int>() { pointer };
                _index.Add(key, set);
            }
        }
        #endregion

        #region remove
        public void Remove(T record)
        {
            _index.Remove(_keyResolver(record));
        }
        #endregion

        public int[] ResolvePointers(Y key)
        {
            if (_index.TryGetValue(key, out List<int> set))
                return set.ToArray();

            return Array.Empty<int>();
        }
    }
}
