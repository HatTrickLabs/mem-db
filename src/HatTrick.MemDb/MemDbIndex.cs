using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace HatTrick.InMemDb
{
    #region mem db index collection [class]
    internal class MemDbIndexCollection<T> where T : class
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
        internal MemDbIndex<T> Get(string name)
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

        #region refresh
        public void Refresh((T record, int pointer) stale, (T record, int pointer) fresh)
        {

        }
        #endregion
    }
    #endregion

    #region mem db index of T [class]
    internal abstract class MemDbIndex<T> where T : class
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
        internal abstract void Apply(T record, int pointer);
        #endregion

        #region remove
        internal abstract void Remove(T record, int pointer);
        #endregion

        #region refresh
        internal abstract void Refresh((T record, int pointer) stale, (T record, int pointer) fresh);
        #endregion

        #region of T
        internal MemDbIndex<T, Y> Of<Y>() where Y : IConvertible
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
    internal class MemDbIndex<T, Y> : MemDbIndex<T> where T : class where Y : IConvertible
    {
        #region internals
        private Func<T, Y> _keyResolver;
        private Dictionary<Y, List<int>> _index;
        private List<Y> _lookup;
        private HybridComparer<Y> _comparer;
        #endregion

        #region ctors
        public MemDbIndex(string name, Func<T, Y> keyResolver) : this(name, keyResolver, null)
        { }

        public MemDbIndex(string name, Func<T, Y> keyResolver, HybridComparer<Y> comparer) : base(name)
        {
            _keyResolver = keyResolver ?? throw new ArgumentNullException(nameof(keyResolver));
            _comparer = comparer ?? new HybridComparer<Y>();
        }
        #endregion

        #region initialize
        internal override void Initialize(int capacity)
        {
            _index = new Dictionary<Y, List<int>>(capacity, _comparer);
            _lookup = new List<Y>(capacity);
        }
        #endregion

        #region apply
        internal override void Apply(T record, int pointer)
        {
            Y key = _keyResolver(record);
            this.Apply(key, pointer);
        }

        private void Apply(Y key, int pointer)
        {
            List<int> pointers = null;
            if (_index.TryGetValue(key, out pointers))
            {
                pointers.Add(pointer);
            }
            else
            {
                pointers = new List<int>() { pointer };
                _index.Add(key, pointers);

                //should always be less than 0 here...
                int lookupIndex = _lookup.BinarySearch(key, _comparer);
                lookupIndex = ~lookupIndex;
                _lookup.Insert(lookupIndex, key);
            }
        }
        #endregion

        #region remove
        internal override void Remove(T record, int pointer)
        {
            Y key = _keyResolver(record);
            this.Remove(key, pointer);
        }

        internal void Remove(Y key, int pointer)
        {
            List<int> pointers = _index[key];
            bool retain = pointers.Count > 1;

            if (retain)
            {
                pointers.Remove(pointer);
            }
            else
            {
                _index.Remove(key);
                int lookupIndex = _lookup.BinarySearch(key, _comparer);
                _lookup.RemoveAt(~lookupIndex);
            }
        }
        #endregion

        #region refresh
        internal override void Refresh((T record, int pointer) stale, (T record, int pointer) fresh)
        {
            this.Refresh(
                stale: (_keyResolver(stale.record), stale.pointer), 
                fresh: (_keyResolver(fresh.record), fresh.pointer)
            );
        }

        internal void Refresh((Y key, int pointer) stale, (Y key, int pointer) fresh)
        {
            if (_comparer.Equals(stale.key, fresh.key))
            {
                //this is the fast path...if the update applied did not change the 
                //property from which this index was built, we only need to swap around the pointers
                var pointers = _index[stale.key];
                pointers.Remove(stale.pointer);
                pointers.Add(fresh.pointer);
            }
            else
            {
                this.Remove(stale.key, stale.pointer);
                this.Apply(fresh.key, fresh.pointer);
            }
        }
        #endregion

        #region equal to
        internal int[] EqualTo(Y key)
        {
            if (_index.TryGetValue(key, out List<int> set))
                return set.ToArray();

            return Array.Empty<int>();
        }
        #endregion

        #region not equal to
        internal int[] NotEqualTo(Y key)
        {
            //TODO: this is terrible...think about just completely removing 'NotEqualTo'
            //This index works best on dense and widely distributed data keys...NotEqualTo requires
            //a linear roll through all the keys and completely defeats the purpose.
            var keys = _index.Keys;
            List<int> pointers = new List<int>();
            foreach (var k in _index.Keys)
            {
                if (!_comparer.Equals(key, k))
                {
                    pointers.AddRange(_index[k]);
                }
            }

            return pointers.ToArray();
        }
        #endregion

        #region greater than
        internal int[] GreaterThan(Y key)
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

        #region greater than equal to
        internal int[] GreaterThanEqualTo(Y key)
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

        #region less than
        internal int[] LessThan(Y key)
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

        #region less than equal to
        internal int[] LessThanEqualTo(Y key)
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

    #region hybrid comparer
    public class HybridComparer<T> : IComparer<T>, IEqualityComparer<T>
    {
        #region internals
        private bool _isDefault;
        private IEqualityComparer<T> _equality;
        private IComparer<T> _relational;
        #endregion

        #region interface
        public bool IsDefault => _isDefault;
        #endregion

        #region ctors
        public HybridComparer()
        {
            _equality = EqualityComparer<T>.Default;
            _relational = Comparer<T>.Default;
            _isDefault = true;
        }

        public HybridComparer(IEqualityComparer<T> equality, IComparer<T> relational)
        {
            _equality = equality ?? throw new ArgumentNullException(nameof(equality));
            _relational = relational ?? throw new ArgumentNullException(nameof(relational));
            _isDefault = false;
        }
        #endregion

        #region compare
        public int Compare(T x, T y)
        {
            return _relational.Compare(x, y);
        }
        #endregion

        #region equals
        public bool Equals(T x, T y)
        {
            return _equality.Equals(x, y);
        }
        #endregion

        #region get hash code
        public int GetHashCode(T obj)
        {
            return _equality.GetHashCode(obj);
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
