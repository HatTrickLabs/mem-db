using System;
using System.Linq;
using System.Collections.Generic;

namespace HatTrick.Data
{
    #region mem db index collection of T [class]
    internal sealed class MemDbIndexCollection<T> where T : class
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
            for (int i = 0; i < _indexes.Length; i++)
            {
                _indexes[i].Refresh(stale, fresh);
            }
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
        internal MemDbIndex<T, YIndex> Of<YIndex>() where YIndex : IComparable
        {
            if (this is MemDbIndex<T, YIndex> index)
            {
                return index;
            }

            throw new InvalidCastException($"Invalid index cast...Index '{_name}' cannot be cast to index of {typeof(YIndex).Name}");
        }
        #endregion
    }
    #endregion

    #region mem db index of T, YIndex [class]
    internal class MemDbIndex<T, YIndex> : MemDbIndex<T> where T : class where YIndex : IComparable
    {
        #region internals
        private Func<T, YIndex> _keyResolver;
        private Dictionary<YIndex, HashSet<int>> _index;
        private List<YIndex> _lookup;
        private IMemDbComparer<YIndex> _comparer;
        #endregion

        #region interface
        protected IMemDbComparer<YIndex> Comparer => _comparer;
        #endregion

        #region ctors
        internal MemDbIndex(string name, Func<T, YIndex> keyResolver, IMemDbComparer<YIndex> comparer) : base(name)
        {
            //HMMM....in order for MemDbIndexedSet<T,YIndex> to use this as it's base class, null must be accepted here...feels janky
            //However, it's all 'internal'
            _keyResolver = keyResolver;// ?? throw new ArgumentNullException(nameof(keyResolver));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        }
        #endregion

        #region initialize
        internal override void Initialize(int capacity)
        {
            _index = new Dictionary<YIndex, HashSet<int>>(capacity, _comparer);
            _lookup = new List<YIndex>(capacity);
        }
        #endregion

        #region get full pointer set
        protected HashSet<int> GetFullPointerSet()
        {
            int cnt = _lookup.Count;
            var pointers = cnt == 0 
                ? new HashSet<int>() 
                : new HashSet<int>(_index[_lookup[0]].Count * cnt);

            for (int i = 0; i < cnt; i++)
            {
                pointers.UnionWith(_index[_lookup[i]]);
            }
            return pointers;
        }
        #endregion

        #region apply
        internal override void Apply(T record, int pointer)
        {
            YIndex key = _keyResolver(record);
            this.Apply(key, pointer);
        }

        protected void Apply(YIndex key, int pointer)
        {
            HashSet<int> pointers = null;
            if (_index.TryGetValue(key, out pointers))
            {
                pointers.Add(pointer);
            }
            else
            {
                pointers = new HashSet<int>() { pointer };
                _index.Add(key, pointers);

                //should always be less than 0 here...NO DUPLICATES
                int lookupIndex = _lookup.BinarySearch(key, _comparer);
                lookupIndex = ~lookupIndex;
                _lookup.Insert(lookupIndex, key);
            }
        }
        #endregion

        #region remove
        internal override void Remove(T record, int pointer)
        {
            YIndex key = _keyResolver(record);
            this.Remove(key, pointer);
        }

        protected void Remove(YIndex key, int pointer)
        {
            HashSet<int> pointers = _index[key];
            bool retain = pointers.Count > 1;

            if (retain)
            {
                pointers.Remove(pointer);
            }
            else
            {
                _index.Remove(key);
                int lookupIndex = _lookup.BinarySearch(key, _comparer);
                _lookup.RemoveAt(lookupIndex);
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

        protected void Refresh((YIndex key, int pointer) stale, (YIndex key, int pointer) fresh)
        {
            if (_comparer.Equals(stale.key, fresh.key))
            {
                //this is the fast path...if the update did not change the property
                //from which this index was built, we only need to swap around the pointers
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
        internal int[] EqualTo(YIndex key)
        {
            if (_index.TryGetValue(key, out HashSet<int> pointers))
            {
                var output = new int[pointers.Count];
                pointers.CopyTo(output);
                return output;
            }

            return Array.Empty<int>();
        }
        #endregion

        #region in
        internal int[] In(YIndex[] keys)
        {
            if (_lookup.Count == 0)
                return Array.Empty<int>();

            //estimate of capacity assuming a somewhat equal distribution of pointers per index key
            int capacity = _index[_lookup[0]].Count * keys.Length;
            var set = new List<int>(capacity);
            for (int i = 0; i < keys.Length; i++)
            {
                if (_index.TryGetValue(keys[i], out HashSet<int> pointers))
                    set.AddRange(pointers);
            }
            return set.ToArray();
        }
        #endregion

        #region not equal to
        internal int[] NotEqualTo(YIndex key)
        {
            int[] less = this.LessThan(key);
            int[] greater = this.GreaterThan(key);

            int count = less.Length + greater.Length;

            if (count == 0)
                return Array.Empty<int>();

            int[] notEq = new int[count];
            Buffer.BlockCopy(less, 0, notEq, 0, (less.Length * sizeof(int)));
            Buffer.BlockCopy(greater, 0, notEq, (less.Length * sizeof(int)), (greater.Length * sizeof(int)));

            return notEq;
        }
        #endregion

        #region greater than
        internal int[] GreaterThan(YIndex key)
        {
            //REMINDER: _lookup should always be a direct copy of _index.Keys, there are NO DUPLICATE ENTRIES...
            int index = _lookup.BinarySearch(key, _comparer);

            //if the result is < 0, the key doesn't exist and anything at or above the bitwise complement is a hit
            //the 'at' is included because if the key were inserted into the set, 'at' would shift to (~index + 1);
            if (index < 0)
                index = ~index;

            //if the result is >= 0, we got a direct key match and need to shift index + 1 to get only
            //items GREATER than and not equal to.
            else
                index += 1;


            if (index >= _lookup.Count)
                return Array.Empty<int>();

            //estimate of capacity assuming a somewhat equal distribution of pointers per index key
            int capacity = (_lookup.Count - index) * _index[_lookup[index]].Count;
            List<int> set = new List<int>(capacity);
            for (int i = index; i < _lookup.Count; i++)
            {
                set.AddRange(_index[_lookup[i]]);
            }
            return set.ToArray();
        }
        #endregion

        #region greater than equal to
        internal int[] GreaterThanEqualTo(YIndex key)
        {
            //REMINDER: _lookup should always be a direct copy of _index.Keys, there are NO DUPLICATE ENTRIES...
            int index = _lookup.BinarySearch(key, _comparer);

            //if the result is < 0, the key doesn't exist and anything at or above the bitwise complement is a hit
            if (index < 0)
                index = ~index;

            //if the result is >= 0, we got a direct match and want to get everything at or above

            if (index >= _lookup.Count)
                return Array.Empty<int>();

            //estimate of capacity assuming a somewhat equal distribution of pointers per index key
            int capacity = (_lookup.Count - index) * _index[_lookup[index]].Count;
            List<int> set = new List<int>(capacity);
            for (int i = index; i < _lookup.Count; i++)
            {
                set.AddRange(_index[_lookup[i]]);
            }
            return set.ToArray();
        }
        #endregion

        #region less than
        internal int[] LessThan(YIndex key)
        {
            //REMINDER: _lookup should always be a direct copy of _index.Keys, there are NO DUPLICATE ENTRIES...
            int index = _lookup.BinarySearch(key, _comparer);

            if (index < 0)
                index = ~index;

            index -= 1;//shift back one to only get pointers where value < key (NOT EQUAL)...

            if (index < 0)
                return Array.Empty<int>();

            //estimate of capacity assuming a somewhat equal distribution of pointers per index key
            int capacity = (index + 1) * _index[_lookup[index]].Count;
            List<int> set = new List<int>(capacity);
            for (int i = 0; i <= index; i++)
            {
                set.AddRange(_index[_lookup[i]]);
            }
            return set.ToArray();
        }
        #endregion

        #region less than equal to
        internal int[] LessThanEqualTo(YIndex key)
        {
            //REMINDER: _lookup should always be a direct copy of _index.Keys, there are NO DUPLICATE ENTRIES...
            int index = _lookup.BinarySearch(key, _comparer);

            if (index < 0)
                index = (~index) - 1;//take compliment then shift back one...index without shift would be at first value GREATER than key...

            if (index < 0)
                return Array.Empty<int>();
            
            //if the result is >= 0, we got a direct match and want everything at or below

            //estimate of capacity assuming a somewhat equal distribution of pointers per index key
            int capacity = (index + 1) * _index[_lookup[index]].Count;
            List<int> set = new List<int>(capacity);
            for (int i = 0; i <= index; i++)
            {
                set.AddRange(_index[_lookup[i]]);
            }
            return set.ToArray();
        }
        #endregion

        #region between
        internal int[] Between(YIndex lower, YIndex upper)
        {
            int from = _lookup.BinarySearch(lower, _comparer);
            //if the result is < 0, the key doesn't exist and anything at or above the bitwise complement is a hit
            if (from < 0)
                from = (~from);

            int to = _lookup.BinarySearch(upper, _comparer);
            if (to < 0)
                to = (~to) - 1;

            int hits = (to - from + 1);
            if (hits <= 0)
                return Array.Empty<int>();

            //estimate of capacity assuming a somewhat equal distribution of pointers per index key
            int capacity = (hits) * _index[_lookup[from]].Count;
            List<int> set = new List<int>(capacity);
            for (int i = from; i <= to; i++)
            {
                set.AddRange(_index[_lookup[i]]);
            }

            return set.ToArray();
        }
        #endregion

        #region not between
        internal int[] NotBetween(YIndex lower, YIndex upper)
        {
            int[] below = this.LessThan(lower);
            int[] above = this.GreaterThan(upper);

            int count = below.Length + above.Length;

            if (count == 0)
                return Array.Empty<int>();

            int[] notBetween = new int[count];
            Buffer.BlockCopy(below, 0, notBetween, 0, (below.Length * sizeof(int)));
            Buffer.BlockCopy(above, 0, notBetween, (below.Length * sizeof(int)), (above.Length * sizeof(int)));

            return notBetween;
        }

        internal int[] NotBetweenxxx(YIndex lower, YIndex upper)
        {//this does not seem to be any more efficient than the 'NotBetween' above using LessThan, GreaterThan and BlockCopy...

            int from = _lookup.BinarySearch(lower, _comparer);
            //if the result is < 0, the key doesn't exist and anything at or above the bitwise complement is a hit
            if (from < 0)
                from = ~from;

            from -= 1;//get below the potential hit...

            int to = _lookup.BinarySearch(upper, _comparer);
            if (to < 0)
                to = ~to;

            int hits = _lookup.Count - (to - from);

            if (hits == 0)
                return Array.Empty<int>();

            int capacity = hits * _index[_lookup[from < 0 ? to : from]].Count;
            List<int> set = new List<int>(capacity);
            for (int i = 0; i < _lookup.Count; i++)
            {
                if (i > from && i <= to)
                    continue;

                set.AddRange(_index[_lookup[i]]);
            }

            return set.ToArray();
        }
        #endregion
    }
    #endregion

    #region mem db indexed set of T, IEnumerable<YIndex> [class]
    internal class MemDbIndexedSet<T, YIndex> : MemDbIndex<T, YIndex> where T : class where YIndex : IComparable
    {
        #region internals
        private Func<T, ICollection<YIndex>> _keyResolver;
        #endregion

        #region ctors
        public MemDbIndexedSet(string name, Func<T, ICollection<YIndex>> keyResolver, IMemDbComparer<YIndex> comparer) : base(name, null, comparer)
        {
            _keyResolver = keyResolver;
        }
        #endregion

        #region apply
        internal override void Apply(T record, int pointer)
        {
            var keySet = _keyResolver(record);
            HashSet<YIndex> distinct = new HashSet<YIndex>(keySet.Count, base.Comparer);
            foreach (var key in keySet)
            {
                if (distinct.Add(key))
                    base.Apply(key, pointer);
            }
        }
        #endregion

        #region remove
        internal override void Remove(T record, int pointer)
        {
            var keySet = _keyResolver(record);
            HashSet<YIndex> distinct = new HashSet<YIndex>(keySet.Count, base.Comparer);
            foreach (var key in keySet)
            {
                if (distinct.Add(key))
                    base.Remove(key, pointer);
            }
        }
        #endregion

        #region refresh
        internal override void Refresh((T record, int pointer) stale, (T record, int pointer) fresh)
        {
            var freshSet = _keyResolver(fresh.record).ToArray();
            var staleSet = _keyResolver(stale.record).ToArray();

            Array.Sort(freshSet);
            Array.Sort(staleSet);

            if (freshSet.Length == staleSet.Length)
            {
                IMemDbComparer<YIndex> comparer = base.Comparer;
                for (int i = 0; i < freshSet.Length; i++)
                {
                    var f = freshSet[i];
                    var s = staleSet[i];
                    if (comparer.Equals(f, s))
                    {
                        base.Refresh((s, stale.pointer), (f, fresh.pointer));
                    }
                    else
                    {
                        base.Remove(s, stale.pointer);
                        base.Apply(f, fresh.pointer);
                    }
                }
            }
            else
            {
                this.Remove(stale.record, stale.pointer);
                this.Apply(fresh.record, fresh.pointer);
            }
        }
        #endregion

        #region any is equal (exists)
        internal int[] AnyIsEqual(YIndex key)
        {
            //any item in sub array set is equal to key
            return base.EqualTo(key);
        }
        #endregion

        #region any in
        internal int[] AnyIn(params ReadOnlySpan<YIndex> keys)
        {
            //any item in sub array set is equal to any keys
            if (keys.Length == 0)
                return Array.Empty<int>();

            HashSet<int> pointers = new HashSet<int>(base.EqualTo(keys[0]));

            for (int i = 1; i < keys.Length; i++)
            {
                pointers.UnionWith(base.EqualTo(keys[i]));
            }

            var output = new int[pointers.Count];
            pointers.CopyTo(output);
            return output;
        }
        #endregion

        #region any not equal
        internal int[] AnyNotEqual(YIndex key)
        {
            //any item in sub array set is not equal key
            var pointers = new HashSet<int>(base.NotEqualTo(key));
            var output = new int[pointers.Count];
            pointers.CopyTo(output);
            return output;
        }
        #endregion

        #region any not in
        internal int[] AnyNotIn(YIndex[] keys)
        {
            //any item in sub array set is not equal to any keys
            if (keys.Length == 0)
                return Array.Empty<int>();

            HashSet<int> pointers = new HashSet<int>(base.NotEqualTo(keys[0]));

            for (int i = 1; i < keys.Length; i++)
            {
                pointers.UnionWith(base.EqualTo(keys[i]));
            }

            var output = new int[pointers.Count];
            pointers.CopyTo(output);
            return output;
        }
        #endregion

        #region all not equal
        internal int[] AllNotEqual(YIndex key)
        {
            var inverseSet = new HashSet<int>(base.EqualTo(key));
            HashSet<int> allPointers = base.GetFullPointerSet();

            if (inverseSet.Count == 0)
                return allPointers.ToArray();

            int[] pointers = new int[allPointers.Count - inverseSet.Count];
            int at = 0;
            foreach (int p in pointers)
            {
                if (inverseSet.Add(p))
                    pointers[at++] = p;
            }

            return pointers;
        }
        #endregion

        #region all not in
        internal int[] AllNotIn(YIndex[] keys)
        {
            var inverseSet = new HashSet<int>(base.EqualTo(keys[0]));

            for (int i = 1; i < keys.Length; i++)
            {
                inverseSet.UnionWith(base.EqualTo(keys[i]));
            }

            HashSet<int> allPointers = base.GetFullPointerSet();

            if (inverseSet.Count == 0)
                return allPointers.ToArray();

            int[] pointers = new int[allPointers.Count - inverseSet.Count];
            int at = 0;
            foreach (int p in pointers)
            {
                if (inverseSet.Add(p))
                    pointers[at++] = p;
            }

            return pointers;
        }
        #endregion

        #region any is greater than
        internal int[] AnyIsGreaterThan(YIndex key)
        {
            //any sub array item is greater than key
            int[] pointers = base.GreaterThan(key);

            if (pointers.Length == 0 || pointers.Length == 1)
                return pointers;

            var set = new HashSet<int>(pointers);
            var output = new int[set.Count];
            set.CopyTo(output);
            return output;
        }
        #endregion

        #region any is greater than equal to
        internal int[] AnyIsGreaterThanEqualTo(YIndex key)
        {
            //any sub array item is greater or equal to key
            int[] pointers = base.GreaterThanEqualTo(key);

            if (pointers.Length == 0 || pointers.Length == 1)
                return pointers;

            var set = new HashSet<int>(pointers);
            var output = new int[set.Count];
            set.CopyTo(output);
            return output;
        }
        #endregion

        #region any is less than
        internal int[] AnyIsLessThan(YIndex key)
        {
            //any sub array item is less than key
            int[] pointers = base.LessThan(key);

            if (pointers.Length == 0 || pointers.Length == 1)
                return pointers;

            var set = new HashSet<int>(pointers);
            var output = new int[set.Count];
            set.CopyTo(output);
            return output;
        }
        #endregion

        #region any is less than equal to
        internal int[] AnyIsLessThanEqualTo(YIndex key)
        {
            //any sub array item is less than equal to key
            int[] pointers = base.LessThanEqualTo(key);

            if (pointers.Length == 0 || pointers.Length == 1)
                return pointers;

            var set = new HashSet<int>(pointers);
            var output = new int[set.Count];
            set.CopyTo(output);
            return output;
        }
        #endregion

        #region all is greater than
        internal int[] AllIsGreaterThan(YIndex key)
        {
            var inverseSet = new HashSet<int>(this.AnyIsLessThanEqualTo(key));
            HashSet<int> allPointers = base.GetFullPointerSet();

            if (inverseSet.Count == 0)
                return allPointers.ToArray();

            int[] pointers = new int[allPointers.Count - inverseSet.Count];
            int at = 0;
            foreach (int p in allPointers)
            {
                if (inverseSet.Add(p))
                    pointers[at++] = p;
            }

            return pointers;
        }
        #endregion

        #region all is greater than equal to
        internal int[] AllIsGreaterThanEqualTo(YIndex key)
        {
            var inverseSet = new HashSet<int>(this.AnyIsLessThan(key));
            HashSet<int> allPointers = base.GetFullPointerSet();

            if (inverseSet.Count == 0)
                return allPointers.ToArray();

            int[] pointers = new int[allPointers.Count - inverseSet.Count];
            int at = 0;
            foreach (int p in allPointers)
            {
                if (inverseSet.Add(p))
                    pointers[at++] = p;
            }

            return pointers;
        }
        #endregion

        #region all is less than
        internal int[] AllIsLessThan(YIndex key)
        {
            var inverseSet = new HashSet<int>(this.AnyIsGreaterThanEqualTo(key));
            HashSet<int> allPointers = base.GetFullPointerSet();

            if (inverseSet.Count == 0)
                return allPointers.ToArray();

            int[] pointers = new int[allPointers.Count - inverseSet.Count];
            int at = 0;
            foreach (int p in allPointers)
            {
                if (inverseSet.Add(p))
                    pointers[at++] = p;
            }

            return pointers;
        }
        #endregion

        #region all is less than equal to
        internal int[] AllIsLessThanEqualTo(YIndex key)
        {
            var inverseSet = new HashSet<int>(this.AnyIsGreaterThan(key));
            HashSet<int> allPointers = base.GetFullPointerSet();

            if (inverseSet.Count == 0)
                return allPointers.ToArray();

            int[] pointers = new int[allPointers.Count - inverseSet.Count];
            int at = 0;
            foreach (int p in allPointers)
            {
                if (inverseSet.Add(p))
                    pointers[at++] = p;
            }

            return pointers;
        }
        #endregion
    }
    #endregion
}
