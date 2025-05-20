using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace HatTrick.InMemDb
{
    public abstract class MemDbIndexCollection
    {
        #region internals
        private MemDbIndex[] _indexes;
        #endregion

        #region ctors
        public MemDbIndexCollection(MemDbIndex[] indexes)
        {
            _indexes = indexes ?? throw new ArgumentNullException(nameof(indexes));
        }
        #endregion

        #region get
        public MemDbIndex Get(string name)
        {
            return Array.Find(_indexes, (i) => i.Name == name);
        }

        public MemDbIndex<Y> Get<Y>(string name)
        {
            return Array.Find(_indexes, (i) => i.Name == name) as MemDbIndex<Y>;
        }
        #endregion
    }

    public abstract class MemDbIndex
    {
        #region internals
        private string _name;
        #endregion

        #region interface
        public string Name { get; set; }
        #endregion

        #region ctors
        protected MemDbIndex(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }
        #endregion
    }

    public abstract class MemDbIndex<Y> : MemDbIndex
    {
        #region internals
        private Dictionary<Y, List<int>> _index;
        private List<Y> _lookup;
        private IComparer<Y> _comparer;
        #endregion

        #region ctors
        protected MemDbIndex(string name) : this(name, null)
        { }

        protected MemDbIndex(string name, IComparer<Y> comparer) : base(name)
        {
            _comparer = comparer ?? Comparer<Y>.Default;
        }
        #endregion

        #region initialize
        internal void Initialize(int capacity)
        {
            _index = new Dictionary<Y, List<int>>(capacity);
            _lookup = new List<Y>(capacity);
        }
        #endregion

        #region apply
        public void Apply(Y key, int pointer)
        {
            List<int> set = null;
            if (_index.TryGetValue(key, out set))
            {
                set.Add(pointer);
            }
            else
            {
                set = new List<int>() { pointer };
                _index.Add(key, set);

                //should always be less than 0 here...
                int lookupIndex = _lookup.BinarySearch(key);
                lookupIndex = ~lookupIndex;
                _lookup.Insert(lookupIndex, key);
            }
        }
        #endregion

        #region remove
        public void Remove(Y key, int pointer)
        {
            List<int> set = _index[key];
            set.Remove(pointer);

            if (set.Count == 0)
            {
                int lookupIndex = _lookup.BinarySearch(key);
                _lookup.RemoveAt(~lookupIndex);
            }
        }
        #endregion

        #region get pointers
        public int[] GetPointers(Y key)
        {
            if (_index.TryGetValue(key, out List<int> set))
                return set.ToArray();

            return Array.Empty<int>();
        }
        #endregion

        #region get pointers greater than equal to
        public int[] GetPointersGreaterThanEqualTo(Y key)
        {
            int index = _lookup.BinarySearch(key);
            if (index > -1)
            {
                List<int> set = new List<int>(_lookup.Count - index);
                for (int i = index; i < _lookup.Count; i++)
                {
                    set.AddRange(_index[_lookup[i]]);
                }
                return set.ToArray();
            }
            return Array.Empty<int>();
        }
        #endregion

        #region get pointers less than equal to
        public int[] GetPointersLessThanEqualTo(Y key)
        {
            int index = _lookup.BinarySearch(key);
            if (index > -1)
            {
                List<int> set = new List<int>(index + 1);
                for (int i = index; i > -1; i--)
                {
                    set.AddRange(_index[_lookup[i]]);
                }
                return set.ToArray();
            }
            return Array.Empty<int>();
        }
        #endregion
    }

    public class MemDbIndexOf<T, Y> : MemDbIndex<Y> where T : class
    {
        #region internals
        private Func<T, Y> _keyResolver;
        #endregion

        #region ctors
        public MemDbIndexOf(string name, Func<T, Y> keyResolver) : base(name)
        {
            _keyResolver = keyResolver;
        }
        #endregion

        #region apply
        public void Apply(T record, int pointer)
        {
            Y key = _keyResolver(record);
            base.Apply(key, pointer);
        }
        #endregion

        #region remove
        public void Remove(T record, int pointer)
        {
            Y key = _keyResolver(record);
            base.Remove(key, pointer);
        }
        #endregion
    }

    public class MemDbIndexOfSet<T, Y> : MemDbIndex<Y> where T : class
    {
        #region internals
        private Func<T, IEnumerable<Y>> _keyResolver;
        #endregion

        #region ctors
        public MemDbIndexOfSet(string name, Func<T, IEnumerable<Y>> keyResolver) : this(name, keyResolver, null)
        {
        }

        public MemDbIndexOfSet(string name, Func<T, IEnumerable<Y>> keyResolver, IComparer<Y> comparer) : base(name, comparer)
        {
            _keyResolver = keyResolver;
        }
        #endregion

        #region apply
        public void Apply(T record, int pointer)
        {
            var keySet = _keyResolver(record);
            HashSet<Y> distinct = new HashSet<Y>();
            foreach (var key in keySet)
            {
                if (distinct.Add(key))
                {
                    base.Apply(key, pointer);
                }
            }
        }
        #endregion

        #region remove
        public void Remove(T record, int pointer)
        {
            var keySet = _keyResolver(record);
            foreach (var key in keySet)
            {
                base.Remove(key, pointer);
            }
        }
        #endregion
    }
}
