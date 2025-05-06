using System;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    internal sealed class MemDbCache<T> : IMemDbCache<T>, IDisposable where T : class
    {
        #region internals
        private string _datasetName;
        private List<MemDbRecord<T>> _records;
        private Lock _lock;

        private IMemDbCloner<T> _cloner;
        private IMemDbPersister<T> _persister;

        private AccessMode _mode;
        private uint _memOnlyLastId;
        private Lock _idLock;
        #endregion

        #region ctors
        internal MemDbCache(MemDbConfiguration<T> config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            _datasetName = config.DatasetName;
            _cloner = config.GetCloner();
            _lock = new();

            if (config.Path is not null)
                _persister = config.GetPersister();
            else
                _idLock = new();

            _mode = config.Mode;
            _memOnlyLastId = 0;

            this.Initialize();
        }
        #endregion

        #region initialize
        private void Initialize()
        {
            if (_mode == AccessMode.AppendOnly)
                return;

            if (_persister is not null)
            {
                //TODO: wire up some way for the persister to notify if a background thread
                //throws an exception...i.e. the timer initiated flush thread throws file access or permissions ex.
                _persister.ReadMappedRecords(out IList<MemDbRecord<T>> records);
                _records = records as List<MemDbRecord<T>>;
            }
            else
                _records = new List<MemDbRecord<T>>(128);
        }
        #endregion

        #region snapshot
        DateTime IMemDbCache<T>.Snapshot()
        {
            if (_persister is null)
                throw new InvalidOperationException($"{nameof(IMemDbCache<T>.Snapshot)} is not available with a unpersisted database (no path provided).");

            lock (_lock)
            {
                return (_persister as IMemDbPersister<T>).Snapshot();
            }
        }
        #endregion

        #region purge
        (int stale, int deleted) IMemDbCache<T>.Purge()
        {
            //if the mode is append only, the cache is never initialized.
            //if the mode is read only, the cache can never be dirty.
            //ONLY read write mode can have purgable records.
            if (_mode != AccessMode.ReadWrite)
                return (stale: 0, deleted: 0);

            (int fresh, int stale, int deleted) stats = this.ResolveCacheStats(out int upperBound);

            if (stats.stale == 0 && stats.deleted == 0)
                return (stale: 0, deleted: 0);

            lock (_lock)
            {
                int capacity = stats.fresh == 0 ? 128 : (int)(stats.fresh * 1.025);
                var newSet = new List<MemDbRecord<T>>(capacity);

                for (int i = 0; i < _records.Count; i++)
                {
                    var record = _records[i];
                    //if the rec inserted or state changed after stats collected, just shift it over so return count is accurate.
                    if (i > upperBound)
                        newSet.Add(record);

                    else if (record.State == RecordState.Fresh)
                        newSet.Add(record);

                    _records[i] = null;//not necessary...
                } 
                _records = newSet;
            }

            return (stats.stale, stats.deleted);
        }
        #endregion

        #region resolve cache stats
        private (int fresh, int stale, int deleted) ResolveCacheStats(out int upperBound)
        {
            upperBound = 0;
            int fresh = 0;
            int stale = 0;
            int deleted = 0;

            lock (_lock)
            {
                int i;
                for (i = 0; i < _records.Count; i++)
                {
                    RecordState state = _records[i].State;
                    if (state == RecordState.Fresh)
                        fresh += 1;

                    else if (state == RecordState.Stale)
                        stale += 1;

                    else if (state == RecordState.Deleted)
                        deleted += 1;
                }
                upperBound = i;
            }

            return (fresh, stale, deleted);
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

        #region exists
        public bool Exists(Func<T, bool> where)
        {
            this.EnsureReadMode(nameof(Exists));

            bool exists;
            lock (_lock)
            {
                exists = _records.Exists(r => r.State == RecordState.Fresh && where(r.Value));
            }
            return exists;
        }
        #endregion

        #region count
        public int Count()
        {
            //this count should be available in ANY AccessMode
            bool canReadCache = _mode != AccessMode.AppendOnly;
            lock (_lock)
            {
                if (canReadCache)
                    return _records.Count(r => r.State == RecordState.Fresh);

                //need to defer down into the persister for simple fresh record count...
                //this allows for a count to be assessed even when running in AppendOnly mode...
                //the count is pulled out of the always initialized MemDbMap....however, this would
                //cause a confusing read when Count() is called immediatelly after inserts as the newly
                //inserted records may still be waiting in the append buffer queue within the persister...
                //calling Flush prior to reading count ensures all inserted records have been flushed from the 
                //queue and MemDbMap has accounted for them.
                _persister.Flush(null);
                return _persister.RecordCount;
            }
        }

        public int Count(Func<T, bool> selector)
        {
            this.EnsureReadMode(nameof(Count));

            lock (_lock)
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
            lock (_lock)
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
            lock (_lock)
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
            lock (_lock)
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
            lock (_lock)
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
            lock (_lock)
            {
                T[] set = this.ExecuteQueryExpression(expression, false);
                cnt = (set.Length > 0) 
                    ? this.Delete((r) => Array.IndexOf(set, r) > -1)
                    : 0;
            }
            return cnt;
        }
        #endregion

        #region get next id
        private uint GetNextId()
        {
            if (_persister is not null)
                return _persister.GetNextId();

            lock (_idLock)
            {
                return ++_memOnlyLastId;
            }
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
            uint id = this.GetNextId();
            idCallback?.Invoke(id);

            MemDbRecord<T> rec = new MemDbRecord<T>(id, _cloner.DeepCopy(record), DateTime.UtcNow.ToBinary(), encrypt);

            if (_mode != AccessMode.AppendOnly)
            {
                lock (_lock)
                {
                    _records.Add(rec);
                }
            }

            _persister?.Insert(rec);
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
            lock (_lock)
            {
                long utcTimestamp = DateTime.UtcNow.ToBinary();
                matches = _records.FindAll(r => r.State == RecordState.Fresh && where(r.Value));

                if (matches.Count > 0)
                {
                    for (int i = 0; i < matches.Count; i++)
                    {
                        var oldRec = matches[i];
                        //We must deep copy here...if not, the old cache value(s) (that have not yet been flushed to disk)
                        //will get the update.  We need a traceable / archiveable state for each update.  If we don't deep copy
                        //and multi updates are applied to the same record before a disk flush, then all the records updated
                        //between flushes receive all updates and look identical if archived.
                        var newRec = new MemDbRecord<T>(oldRec.Id, _cloner.DeepCopy(oldRec.Value), utcTimestamp, oldRec.IsEncrypted);

                        //we know the MemDb instance is encryption ready if anything encrypted was ever read into or inserted
                        //into the cache (the cache will not contain encrypted data if not encryption ready).

                        oldRec.MarkStale(utcTimestamp);
                        apply(newRec.Value);

                        _records.Add(newRec);

                        if (_persister is not null)
                        {
                            _persister.Insert(newRec);
                            _persister.MarkStale(oldRec);
                        }
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
            lock (_lock)
            {
                long utcTimestamp = DateTime.UtcNow.ToBinary();
                var set = _records.Where(r => r.State == RecordState.Fresh && where(r.Value));
                foreach (var r in set)
                {
                    cnt += 1;
                    r.MarkDeleted(utcTimestamp);
                    _persister?.MarkDeleted(r);
                }
            }

            return cnt;
        }
        #endregion

        #region resolve statistics
        public MemDbStatistics ResolveStatistics(Stats statistics)
        {
            if (_persister is null)
                throw new InvalidOperationException("Cannot resolve statistics for unpersisted database.");

            return _persister.ResolveStatistics(statistics);
        }
        #endregion

        #region flush
        void IMemDbCache<T>.Flush()
        {
            if (_persister is null)
            {
                string msg = "Cannot flush an unpersisted database...A path must be provided during MemDb configuration for file-based persistence.";
                throw new InvalidOperationException(msg);
            }

            _persister.Flush(false);
        }
        #endregion

        #region dispose
        public void Dispose()
        {
            _records = null;
            _persister?.Dispose();
        }
        #endregion
    }
}
