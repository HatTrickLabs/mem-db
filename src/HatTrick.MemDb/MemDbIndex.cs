using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace HatTrick.InMemDb
{
    #region mem db index collection [class]
    public class MemDbIndexCollection<T> where T : class
    {
        #region internals
        private MemDbIndex<T>[] _indexes;
        #endregion

        #region ctors
        public MemDbIndexCollection(MemDbIndex<T>[] indexes)
        {
            _indexes = indexes ?? throw new ArgumentNullException(nameof(indexes));
        }
        #endregion

        #region initialize
        public void Initialize(int capacity)
        {
            for (int i = 0; i < _indexes.Length; i++)
            {
                _indexes[i].Initialize(capacity);
            }
        }
        #endregion

        #region get
        public MemDbIndex<T> Get(string name)
        {
            return Array.Find(_indexes, (a) => a.Name == name);
        }
        #endregion

        #region apply
        public void Apply(T record, int pointer)
        {
            for (int i = 0; i < _indexes.Length; i++)
            {
                _indexes[i].Apply(record, pointer);
            }
        }
        #endregion

        #region remove
        public void Remove(T record, int pointer)
        {
            for (int i = 0; i < _indexes.Length; i++)
            {
                _indexes[i].Remove(record, pointer);
            }
        }
        #endregion
    }
    #endregion

    #region mem db index of T [class]
    public abstract class MemDbIndex<T> where T : class
    {
        #region internals
        private string _name;
        #endregion

        #region interface
        public string Name => _name;
        #endregion

        #region ctors
        protected MemDbIndex(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }
        #endregion

        #region initialize
        internal abstract void Initialize(int capacity);
        #endregion

        #region apply
        public abstract void Apply(T record, int pointer);
        #endregion

        #region remove
        public abstract void Remove(T record, int pointer);
        #endregion

        #region of T
        internal MemDbIndex<T, Y> Of<Y>()
        {
            if (this is MemDbIndex<T, Y> index)
            {
                return index;
            }

            throw new InvalidCastException($"Invalid index cast...Index '{_name}' cannot be cast to index of {typeof(Y).Name}");
        }
        #endregion
    }
    #endregion

    #region mem db index of T,Y [class]
    public class MemDbIndex<T, Y> : MemDbIndex<T> where T : class
    {
        #region internals
        private Func<T, Y> _keyResolver;
        private Dictionary<Y, List<int>> _index;
        private List<Y> _lookup;
        private IComparer<Y> _comparer;
        #endregion

        #region ctors
        public MemDbIndex(string name, Func<T, Y> keyResolver) : this(name, keyResolver, null)
        { }

        public MemDbIndex(string name, Func<T, Y> keyResolver, IComparer<Y> comparer) : base(name)
        {
            _keyResolver = keyResolver ?? throw new ArgumentNullException(nameof(keyResolver));
            _comparer = comparer ?? Comparer<Y>.Default;
        }
        #endregion

        #region initialize
        internal override void Initialize(int capacity)
        {
            _index = new Dictionary<Y, List<int>>(capacity);
            _lookup = new List<Y>(capacity);
        }
        #endregion

        #region apply
        public override void Apply(T record, int pointer)
        {
            Y key = _keyResolver(record);
            this.Apply(key, pointer);
        }

        private void Apply(Y key, int pointer)
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
        public override void Remove(T record, int pointer)
        {
            Y key = _keyResolver(record);
            this.Remove(key, pointer);
        }

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
        public int[] GetPointersGreaterThan(Y key)
        {
            int index = _lookup.BinarySearch(key, _comparer);
            if (index > -1)
            {
                if (index < (_lookup.Count - 1))
                    index += 1;//shift forward one to only get pointers where value > key (NOT EQUAL)...

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

        #region get pointers greater than equal to
        public int[] GetPointersGreaterThanEqualTo(Y key)
        {
            int index = _lookup.BinarySearch(key, _comparer);
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

        #region get pointers less than
        public int[] GetPointersLessThan(Y key)
        {
            int index = _lookup.BinarySearch(key, _comparer);
            if (index > -1)
            {
                if (index > 0)
                    index -= 1;//shift back one to only get pointers where value < key (NOT EQUAL)...

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

        #region get pointers less than equal to
        public int[] GetPointersLessThanEqualTo(Y key)
        {
            int index = _lookup.BinarySearch(key, _comparer);
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
    #endregion

    //public class MemDbIndexOfSet<T, Y> : MemDbIndex<T, Y> where T : class
    //{
    //    #region internals
    //    private Func<T, IEnumerable<Y>> _keyResolver;
    //    #endregion

    //    #region ctors
    //    public MemDbIndexOfSet(string name, Func<T, IEnumerable<Y>> keyResolver) : this(name, keyResolver, null)
    //    {
    //    }

    //    public MemDbIndexOfSet(string name, Func<T, IEnumerable<Y>> keyResolver, IComparer<Y> comparer) : base(name, comparer)
    //    {
    //        _keyResolver = keyResolver;
    //    }
    //    #endregion

    //    #region apply
    //    public void Apply(T record, int pointer)
    //    {
    //        var keySet = _keyResolver(record);
    //        HashSet<Y> distinct = new HashSet<Y>();
    //        foreach (var key in keySet)
    //        {
    //            if (distinct.Add(key))
    //            {
    //                base.Apply(record, pointer);
    //            }
    //        }
    //    }
    //    #endregion

    //    #region remove
    //    public void Remove(T record, int pointer)
    //    {
    //        var keySet = _keyResolver(record);
    //        foreach (var key in keySet)
    //        {
    //            base.Remove(key, pointer);
    //        }
    //    }
    //    #endregion
    //}
}
