using System;
using System.Linq;
using System.Collections.Generic;

namespace HatTrick.MemDb
{
    internal sealed class MemDbCache<T> : IMemDbCacher<T>, IDisposable where T : class, new()
    {
        #region internals
        private string _datasetName;
        private List<MemDbRecord<T>> _records;
        private object _recSyncLock;

        private IMemDbCloner<T> _cloner;
        private IMemDbPersister<T> _persister;

        private AccessMode _mode;
        #endregion

        #region constructors
        internal MemDbCache(string datasetName, IMemDbCloner<T> cloner, IMemDbPersister<T> persister)
        {
            if (datasetName is null)
                throw new ArgumentNullException(nameof(datasetName));

            if (persister is null)
                throw new ArgumentNullException(nameof(persister));

            if (cloner is null)
                throw new ArgumentNullException(nameof(cloner));

            _datasetName = datasetName;
            _cloner = cloner;
            _persister = persister; 
            _recSyncLock = new();

            _mode = _persister.Mode;

            this.Initialize();
        }
        #endregion

        #region initialize
        private void Initialize()
        {
            if (_mode == AccessMode.AppendOnly)
                return;
            
            int recCount = _persister.RecordCount;
            int capacity = _mode == AccessMode.ReadOnly ? recCount : (int)(recCount * 1.1);
            _records = new List<MemDbRecord<T>>(capacity);
            _records.AddRange(_persister.ReadAll());
        }
        #endregion

        #region ensure mode
        private void EnsureReadMode(string targetSite)
        {
            this.EnsureMode(AccessMode.ReadWrite | AccessMode.ReadOnly, targetSite);
        }

        private void EnsureMode(AccessMode isMode, string targetSite)
        {
            if ((_mode & isMode) == _mode)
                return;

            string msg = $"MemDb instance for dataset '{_datasetName}' is running in '{_mode}' mode...'{targetSite}' accessor is disabled.";
            throw new InvalidOperationException(msg);
        }
        #endregion

        #region count
        public int Count()
        {
            this.EnsureReadMode(nameof(Count));

            lock (_recSyncLock)
            {
                return _records.Count(r => r.IsStale == false);
            }
        }

        public int Count(Func<T, bool> selector)
        {
            this.EnsureReadMode(nameof(Count));

            lock (_recSyncLock)
            {
                return _records.Count(r => r.IsStale == false && selector(r.Value));
            }
        }
        #endregion

        #region max
        public Y Max<Y>(Func<T, Y> selector)
        {
            this.EnsureReadMode(nameof(Max));

            Y max = default(Y);
            lock (_recSyncLock)
            {
                if (_records.Count > 0)
                {
                    max = _records.Where(r => r.IsStale == false).Max<MemDbRecord<T>, Y>((r) => selector(r.Value));
                }
            }
            return max;
        }
        #endregion

        #region min
        public Y Min<Y>(Func<T, Y> selector)
        {
            this.EnsureReadMode(nameof(Min));

            Y min = default(Y);
            lock (_recSyncLock)
            {
                if (_records.Count > 0)
                {
                    min = _records.Where(r => r.IsStale == false).Min<MemDbRecord<T>, Y>((r) => selector(r.Value));
                }
            }
            return min;
        }
        #endregion

        #region sum
        public int Sum(Func<T, int> selector)
        {
            this.EnsureReadMode(nameof(Sum));

            lock (_recSyncLock)
            {
                return _records.Where(r => r.IsStale == false).Sum((r) => selector(r.Value));
            }
        }

        public double Sum(Func<T, double> selector)
        {
            this.EnsureReadMode(nameof(Sum));

            lock (_recSyncLock)
            {
                return _records.Where(r => r.IsStale == false).Sum((r) => selector(r.Value));
            }
        }

        public decimal Sum(Func<T, decimal> selector)
        {
            this.EnsureReadMode(nameof(Sum));

            lock (_recSyncLock)
            {
                return _records.Where(r => r.IsStale == false).Sum((r) => selector(r.Value));
            }
        }
        #endregion

        #region find distinct
        public Y[] FindDistinct<Y>(Converter<T, Y> converter) where Y : IConvertible
        {
            this.EnsureReadMode(nameof(FindDistinct));

            lock (_recSyncLock)
            {
                return _records.Where(r => r.IsStale == false).Select((r) => converter(r.Value)).Distinct().ToArray();
            }
        }
        #endregion

        #region find
        public T Find(Func<T, bool> where)
        {
            this.EnsureReadMode(nameof(Find));

            MemDbRecord<T> rec = null;
            lock (_recSyncLock)
            {
                for (int i = 0; i < _records.Count; i++)
                {
                    if (_records[i].IsStale == false && where(_records[i].Value))
                    {
                        rec = _records[i];
                        break;
                    }
                }
            }
            return rec is null ? null : _cloner.DeepCopy(rec.Value);
        }
        #endregion

        #region find all
        public T[] FindAll(Func<T, bool> where)
        {
            this.EnsureReadMode(nameof(FindAll));

            T[] matches;
            lock (_recSyncLock)
            {
                matches = _records.Where(r => r.IsStale == false && where(r.Value)).Select(r => r.Value).ToArray();
            }

            T[] set = _cloner.DeepCopy(matches);

            return set;
        }
        #endregion

        #region query
        public MemDbExpression<T> Query()
        {
            this.EnsureReadMode(nameof(Query));

            return new MemDbExpression<T>(this.ExecuteQuery);
        }
        #endregion

        #region execute query
        private T[] ExecuteQuery(MemDbExpression<T> expression, bool deepCopy = true)
        {
            Func<MemDbRecord<T>, bool> filter = expression.HasFilter
                ? (r) => r.IsStale == false && expression.Filter(r.Value)
                : (r) => r.IsStale == false;

            lock (_recSyncLock)
            {
                List<T> matches = _records.Where(filter).Select(r => r.Value).ToList();

                if (matches.Count > 0)
                {
                    if (expression.HasOrderBy)
                        matches.Sort(expression.OrderByComparison);

                    if (expression.HasSkip && expression.HasLimit)
                        matches = matches.Skip(expression.SkipCount).Take(expression.LimitCount).ToList();

                    else if (expression.HasSkip)
                        matches = matches.Skip(expression.SkipCount).ToList();

                    else if (expression.HasLimit)
                        matches = matches.Take(expression.LimitCount).ToList();


                    if (deepCopy)
                        return _cloner.DeepCopy(matches);

                    else
                        return matches.ToArray();
                }
            }

            return Array.Empty<T>();
        }
        #endregion

        #region insert
        public void Insert(T record, bool encrypt = false)
        {
            this.EnsureMode(AccessMode.ReadWrite | AccessMode.AppendOnly, nameof(Insert));

            MemDbRecord<T> rec = new MemDbRecord<T>(_cloner.DeepCopy(record), encrypt);

            lock (_recSyncLock)
            {
                rec.SetMapIndex(_records.Count);
                _records.Add(rec);
            }

            _persister.Insert(rec);
        }
        #endregion

        #region update
        public int Update(Action<T> apply, Func<T, bool> where)
        {
            this.EnsureMode(AccessMode.ReadWrite, nameof(Update));

            if (apply == null)
                throw new ArgumentNullException(nameof(apply));

            if (where == null)
                throw new ArgumentNullException(nameof(where));

            List<MemDbRecord<T>> matches = null;
            lock (_recSyncLock)
            {
                matches = _records.FindAll(r => r.IsStale == false && where(r.Value));
            }

            if (matches.Count == 0)
                return 0;

            for (int i = 0; i < matches.Count; i++)
            {
                var oldRec = matches[i];
                var newRec = new MemDbRecord<T>(_cloner.DeepCopy(oldRec.Value), oldRec.IsEncrypted);
                matches[i].MarkStale();
                apply(newRec.Value);

                lock (_recSyncLock)
                {
                    newRec.SetCacheIndex(_records.Count);
                    _records.Add(newRec);
                    
                }

                _persister.Insert(newRec);
                _persister.MarkStale(oldRec);
            }

            return matches.Count;
        }
        #endregion

        #region delete
        public int Delete(Func<T, bool> where)
        {
            this.EnsureMode(AccessMode.ReadWrite, nameof(Delete));

            if (where == null)
                throw new ArgumentNullException(nameof(where));

            List<MemDbRecord<T>> matches = new List<MemDbRecord<T>>(8);
            lock (_recSyncLock)
            {
                var set = _records.Where(r => r.IsStale == false && where(r.Value));
                foreach (var r in set)
                {
                    r.MarkStale();
                    matches.Add(r);
                }
            }

            if (matches.Count == 0)
                return 0;

            for (int i = 0; i < matches.Count; i++)
            {
                var oldRec = matches[i];
                _persister.MarkStale(oldRec);
            }

            return matches.Count;
        }
        #endregion

        #region flush
        public void Flush()
        {
            _persister.Flush(true);
        }
        #endregion

        #region dispose
        public void Dispose()
        {
            _persister.Dispose();
        }
        #endregion
    }
}
