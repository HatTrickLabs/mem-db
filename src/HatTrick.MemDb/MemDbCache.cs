using System;
using System.Linq;
using System.Collections.Generic;

namespace HatTrick.InMemDb
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

        #region ctors
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
            _records.AddRange(_persister.InitializeMappedRecords());
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
            //this count should be available in ANY AccessMode
            lock (_recSyncLock)
            {
                //need to defer down into the persister for simple fresh record count...
                //this allows for a count to be accessed even when running in AppendOnly mode...
                //the count is pulled out of the always initialized MemDbMap
                return _persister.RecordCount;
            }
        }

        public int Count(Func<T, bool> selector)
        {
            this.EnsureReadMode(nameof(Count));

            lock (_recSyncLock)
            {
                return _records.Count(r => r.State == RecordState.Fresh && selector(r.Value));
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
                    if (_records[i].State == RecordState.Fresh && where(_records[i].Value))
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
                matches = _records.Where(r => r.State == RecordState.Fresh && where(r.Value)).Select(r => r.Value).ToArray();
            }

            T[] set = _cloner.DeepCopy(matches);

            return set;
        }
        #endregion

        #region query
        public MemDbExpression<T> Query()
        {
            this.EnsureReadMode(nameof(Query));

            return new MemDbExpression<T>(this.ExecuteQueryExpression, this.ExecuteUpdateExpression, this.ExecuteDeleteExpression);
        }
        #endregion

        #region execute query expression
        private T[] ExecuteQueryExpression(MemDbExpression<T> expression, bool deepCopy = true)
        {
            Func<MemDbRecord<T>, bool> filter = expression.HasFilter
                ? (r) => r.State == RecordState.Fresh && expression.Filter(r.Value)
                : (r) => r.State == RecordState.Fresh;

            T[] set = Array.Empty<T>();
            lock (_recSyncLock)
            {
                T[] matches = _records.Where(filter).Select(r => r.Value).ToArray();

                if (matches.Length == 0 || (expression.HasSkip && expression.SkipCount >= matches.Length))
                    goto EMPTY;

                if (expression.HasOrderBy && matches.Length > 1)
                    Array.Sort<T>(matches, expression.OrderByComparison);

                if (expression.HasSkip)
                {
                    int len = expression.HasLimit 
                        ? Math.Min(matches.Length - expression.SkipCount, expression.LimitCount)
                        : matches.Length - expression.SkipCount;

                    set = new T[len];
                    Array.ConstrainedCopy(matches, expression.SkipCount, set, 0, len);
                }
                else if (expression.HasLimit && expression.LimitCount < matches.Length)
                {
                    set = new T[expression.LimitCount];
                    Array.ConstrainedCopy(matches, 0, set, 0, expression.LimitCount);
                }
                else
                {
                    set = matches;
                }

                if (deepCopy)
                    set = _cloner.DeepCopy(set);
            }
        EMPTY:
            return set;
        }
        #endregion

        #region execute update expression
        private int ExecuteUpdateExpression(MemDbExpression<T> expression, Action<T> apply)
        {
            int cnt = 0;
            lock (_recSyncLock)
            {
                T[] set = this.ExecuteQueryExpression(expression, false);
                cnt = (set.Length > 0)
                    ? this.Update(apply, (r) => Array.IndexOf(set, r) > -1)
                    : 0;
            }
            return cnt;
        }
        #endregion

        #region execute delete expression
        private int ExecuteDeleteExpression(MemDbExpression<T> expression)
        {
            int cnt = 0;
            lock (_recSyncLock)
            {
                T[] set = this.ExecuteQueryExpression(expression, false);
                cnt = (set.Length > 0) 
                    ? this.Delete((r) => Array.IndexOf(set, r) > -1)
                    : 0;
            }
            return cnt;
        }
        #endregion

        #region insert
        public void Insert(T record, bool encrypt = false)
        {
            this.Insert(record, null, encrypt);
        }

        public void Insert(T record, Action<uint> idCallback, bool encrypt = false)
        {
            this.EnsureMode(AccessMode.ReadWrite | AccessMode.AppendOnly, nameof(Insert));

            //hint: this must happen before deep copy...
            //we don't even know if they want the Id, but if they do...
            //it MUST be applied WHEREVER they want it BEFORE DeepCopy
            uint id = _persister.GetNextId();
            idCallback?.Invoke(id);

            MemDbRecord<T> rec = new MemDbRecord<T>(id, _cloner.DeepCopy(record), encrypt);

            if (_mode != AccessMode.AppendOnly)
            {
                lock (_recSyncLock)
                {
                    rec.SetCacheIndex(_records.Count);
                    _records.Add(rec);
                }
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
                matches = _records.FindAll(r => r.State == RecordState.Fresh && where(r.Value));

                if (matches.Count > 0)
                {
                    for (int i = 0; i < matches.Count; i++)
                    {
                        var oldRec = matches[i];
                        //no reason to deep copy here...the old cache value will get the update, but it will also be
                        //marked stale...the update on old will never get persisted...the deep copy is pointless.
                        var newRec = new MemDbRecord<T>(oldRec.Id, oldRec.Value, oldRec.IsEncrypted);

                        oldRec.MarkStale();
                        apply(newRec.Value);

                        newRec.SetCacheIndex(_records.Count);
                        _records.Add(newRec);

                        _persister.Insert(newRec);
                        _persister.MarkStale(oldRec);
                    }
                }
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

            int cnt = 0;
            lock (_recSyncLock)
            {
                var set = _records.Where(r => r.State == RecordState.Fresh && where(r.Value));
                foreach (var r in set)
                {
                    cnt += 1;
                    r.MarkDeleted();
                    _persister.MarkStale(r);
                }
            }

            return cnt;
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
