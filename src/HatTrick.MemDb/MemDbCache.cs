using System;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    internal sealed class MemDbCache<T> : IMemDbCache<T>, IDisposable where T : class
    {
        #region internals
        private const int _initialCacheCapacity = 128;

        private string _datasetName;
        private List<MemDbRecord<T>> _records;
        private bool _isIndexed;
        private Dictionary<long, int> _index;
        private MemDbIndexCollection<T> _appliedIndexes;
        private Lock _lock;

        private IMemDbCloner<T> _cloner;
        private IMemDbPersister<T> _persister;

        private AccessMode _mode;
        private long _memOnlyLastId;
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
            _isIndexed = config.IsIndexedOnIdentity;
            _appliedIndexes = config.GetAppliedIndexes();
            _memOnlyLastId = 0;

            this.Initialize();
        }
        #endregion

        #region initialize
        private void Initialize()
        {
            if (_mode == AccessMode.AppendOnly)
                return;

            if (_persister is null)
                this.InitializeUnPersisted();

            else
                this.InitializePersisted();
        }

        private void InitializePersisted()
        {
            _persister.ReadMappedRecords(out List<MemDbRecord<T>> records);
            _records = records as List<MemDbRecord<T>>;
            if (_isIndexed || _appliedIndexes is not null)
            {
                if (_isIndexed)
                    _index = new Dictionary<long, int>(_records.Capacity);

                if (_appliedIndexes is not null)
                    _appliedIndexes.Initialize(_records.Capacity);

                for (int i = 0; i < records.Count; i++)
                {
                    _index?.Add(records[i].Id, i);
                    _appliedIndexes?.Apply(records[i].Value, i);
                }
            }
        }

        private void InitializeUnPersisted()
        {
            _records = new List<MemDbRecord<T>>(_initialCacheCapacity);

            if (_isIndexed)
                _index = new Dictionary<long, int>(_initialCacheCapacity);

            if (_appliedIndexes is not null)
                _appliedIndexes.Initialize(_initialCacheCapacity);
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
                var newIndex = _isIndexed ? new Dictionary<long, int>(capacity) : null;
                for (int i = 0; i < _records.Count; i++)
                {
                    var record = _records[i];
                    //if the rec inserted or state changed after stats collected, just shift it over so return count is accurate.
                    if (i > upperBound || record.State == RecordState.Fresh)
                    {
                        newIndex?.Add(record.Id, newSet.Count);
                        newSet.Add(record);
                    }
                    _records[i] = null;//not necessary...
                } 
                _records = newSet;
                _index = newIndex;
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
                //immediately after exiting lock, new records be added to _records...record
                //the upper bound so purge doesn't look past upper bound when these stats are recorded
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

        public bool Exists(long id)
        {
            this.EnsureReadMode(nameof(Exists));

            bool exists;

            lock (_lock)
            {
                exists = _isIndexed 
                    ? _index.ContainsKey(id)
                    : _records.Exists(r => r.Id == id && r.State == RecordState.Fresh);
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

        public T Find(long id)
        {
            this.EnsureReadMode(nameof(Find));

            MemDbRecord<T> rec = null;
            lock (_lock)
            {
                int idx = -1;
                if (_isIndexed)
                {
                    if (_index.TryGetValue(id, out int i))
                        idx = i;
                }
                else
                {
                    idx = _records.FindIndex((r) => r.Id == id && r.State == RecordState.Fresh);
                }

                if (idx > -1)
                    rec = _records[idx];
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

        public T[] FindAll(params long[] ids)
        {
            this.EnsureReadMode(nameof(FindAll));
            List<T> matches = new List<T>(ids.Length);
           
            lock (_lock)
            {
                if (_isIndexed)
                {
                    foreach (long id in ids)
                    {
                        if (_index.TryGetValue(id, out int index))
                            matches.Add(_records[index].Value);
                    }
                }
                else
                {
                    var march = _records.FindAll((r) => r.State == RecordState.Fresh && Array.Exists(ids, (id) => r.Id == id));
                }
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
        private T[] ExecuteQueryExpression(MemDbExpression<T> expression, bool deepCopy)
        {
            MemDbRecord<T>[] records = this.ExecuteQueryExpression(expression);

            return deepCopy
                ? Array.ConvertAll(records, r => _cloner.DeepCopy(r.Value))
                : Array.ConvertAll(records, r => r.Value);
        }

        private MemDbRecord<T>[] ExecuteQueryExpression(MemDbExpression<T> expression)
        {
            Func<MemDbRecord<T>, bool> filter = expression.HasFilter
                ? (r) => r.State == RecordState.Fresh && expression.Filter(r.Value)
                : (r) => r.State == RecordState.Fresh;

            MemDbRecord<T>[] set = Array.Empty<MemDbRecord<T>>();
            lock (_lock)
            {
                var matches = new List<MemDbRecord<T>>();
                for (int i = 0; i < _records.Count; i++)
                {
                    MemDbRecord<T> r = _records[i];
                    if (filter(r))
                        matches.Add(r);
                }

                int skip = expression.HasSkip ? expression.SkipCount : 0;
                int limit = expression.HasLimit ? expression.LimitCount : int.MaxValue;

                if (matches.Count == 0 || skip >= matches.Count)
                    goto EMPTY;

                if (expression.HasOrderBy && matches.Count > 1)
                    matches.Sort((a, b) => expression.OrderByComparison(a.Value, b.Value));

                if (expression.HasSkip || expression.HasLimit)
                {
                    if (limit > matches.Count - skip)
                        limit = matches.Count - skip;

                    matches = matches.Slice(skip, limit);
                }

                set = matches.ToArray();
            }
        EMPTY:
            return set;
        }
        #endregion

        #region execute update expression
        private int ExecuteUpdateExpression(MemDbExpression<T> expression, Action<T> apply)
        {
            lock (_lock)
            {
                MemDbRecord<T>[] set = this.ExecuteQueryExpression(expression);
                if (set.Length > 0)
                {
                    long utcTimestamp = DateTime.UtcNow.ToBinary();
                    for (int i = 0; i < set.Length; i++)
                    {
                        this.ApplyUpdate(apply, set[i], utcTimestamp);
                    }
                }
                return set.Length;
            }
        }
        #endregion

        #region execute delete expression
        private int ExecuteDeleteExpression(MemDbExpression<T> expression)
        {
            lock (_lock)
            {
                MemDbRecord<T>[] set = this.ExecuteQueryExpression(expression);
                if (set.Length > 0)
                {
                    long utcTimestamp = DateTime.UtcNow.ToBinary();
                    for (int i = 0; i < set.Length; i++)
                    {
                        this.Delete(set[i], utcTimestamp);
                    }
                }
                return set.Length;
            }
        }
        #endregion

        #region query via index
        public IMemDbIndexExpression<T, Y> QueryViaIndex<Y>(string indexName) where Y : IConvertible
        {
            this.EnsureReadMode(nameof(QueryViaIndex));

            if (_appliedIndexes is null)
                throw new InvalidOperationException($"No custom indexes applied to MemDb Instance '{_datasetName}'");

            var index = _appliedIndexes.Get(indexName);
            if (index is null)
                throw new ArgumentException($"No custom index exists on MemDb instance '{_datasetName}' with provided name '{indexName}'", nameof(indexName));

            //ensure generic type requested is a match to the index type...
            MemDbIndex<T, Y> typeIndex = index.Of<Y>();

            return new MemDbIndexExpression<T, Y>(index.Name, this.ExecuteIndexQueryExpression, this.ExecuteIndexedUpdateExpression, this.ExecuteIndexedDeleteExpression);
        }
        #endregion

        #region execute indexed query expression
        private T[] ExecuteIndexQueryExpression<Y>(MemDbIndexExpression<T, Y> expression, bool deepCopy) where Y : IConvertible
        {
            MemDbRecord<T>[] records = this.ExecuteIndexQueryExpression(expression);

            return deepCopy
                ? Array.ConvertAll(records, r => _cloner.DeepCopy(r.Value))
                : Array.ConvertAll(records, r => r.Value);
        }

        private MemDbRecord<T>[] ExecuteIndexQueryExpression<Y>(MemDbIndexExpression<T, Y> expression) where Y : IConvertible
        {
            string idxName = expression.IndexName;
            MemDbIndex<T, Y> index = _appliedIndexes.Get(idxName).Of<Y>();
            Y arg = expression.IndexKey;
            lock (_lock)
            {
                int[] pointers = null;
                switch (expression.RelationalOperator)
                {
                    case RelationalOperator.EqualTo:
                        pointers = index.EqualTo(arg);
                        break;
                    case RelationalOperator.NotEqualTo://TODO: deprecate
                        pointers = index.NotEqualTo(arg);
                        break;
                    case RelationalOperator.GreaterThan:
                        pointers = index.GreaterThan(arg);
                        break;
                    case RelationalOperator.LessThan:
                        pointers = index.LessThan(arg);
                        break;
                    case RelationalOperator.GreaterThanEqualTo:
                        pointers = index.GreaterThanEqualTo(arg);
                        break;
                    case RelationalOperator.LessThanEqualTo:
                        pointers = index.LessThanEqualTo(arg);
                        break;
                    default:
                        break;
                }

                var set = new MemDbRecord<T>[pointers.Length];
                for (int i = 0; i < pointers.Length; i++)
                {
                    set[i] = _records[pointers[i]];
                }

                return set;
            }
        }
        #endregion

        #region execute indexed update expression
        private int ExecuteIndexedUpdateExpression<Y>(MemDbIndexExpression<T, Y> expression, Action<T> apply) where Y : IConvertible
        {
            lock (_lock)
            {
                MemDbRecord<T>[] set = this.ExecuteIndexQueryExpression(expression);
                if (set.Length > 0)
                {
                    long utcTimestamp = DateTime.UtcNow.ToBinary();
                    for (int i = 0; i < set.Length; i++)
                    {
                        this.ApplyUpdate(apply, set[i], utcTimestamp);
                    }
                }
                return set.Length;
            }
        }
        #endregion

        #region execute indexed delete expression
        private int ExecuteIndexedDeleteExpression<Y>(MemDbIndexExpression<T, Y> expression) where Y : IConvertible
        {
            lock (_lock)
            {
                MemDbRecord<T>[] set = this.ExecuteIndexQueryExpression(expression);
                if (set.Length > 0)
                {
                    long utcTimestamp = DateTime.UtcNow.ToBinary();
                    for (int i = 0; i < set.Length; i++)
                    {
                        this.Delete(set[i], utcTimestamp);
                    }
                }
                return set.Length;
            }
        }
        #endregion

        #region get next id
        private long GetNextId()
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

        public void Insert(T record, Action<long> idCallback, bool encrypt = false)
        {
            this.EnsureMode(AccessMode.ReadWrite | AccessMode.AppendOnly, nameof(Insert));

            //hint: this must happen before deep copy...
            //we don't even know if they want the Id, but if they do...
            //it MUST be applied WHEREVER they want it BEFORE DeepCopy
            long id = this.GetNextId();
            idCallback?.Invoke(id);

            MemDbRecord<T> rec = new MemDbRecord<T>(id, _cloner.DeepCopy(record), DateTime.UtcNow.ToBinary(), encrypt);

            if (_mode != AccessMode.AppendOnly)
            {
                lock (_lock)
                {
                    rec.CacheIndex = _records.Count;
                    _records.Add(rec);

                    if (_isIndexed)
                        _index.Add(id, rec.CacheIndex);

                    _appliedIndexes?.Apply(rec.Value, rec.CacheIndex);
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
                matches = _records.FindAll(r => r.State == RecordState.Fresh && where(r.Value));
                if (matches.Count > 0)
                {
                    long utcTimestamp = DateTime.UtcNow.ToBinary();
                    for (int i = 0; i < matches.Count; i++)
                    {
                        this.ApplyUpdate(apply, matches[i], utcTimestamp);
                    }
                }
            }
            return matches.Count;
        }

        public bool Update(Action<T> apply, long id)
        {
            this.EnsureMode(AccessMode.ReadWrite, nameof(Update));

            if (apply == null)
                throw new ArgumentNullException(nameof(apply));

            int idx = -1;
            lock (_lock)
            {
                if (_isIndexed && _index.TryGetValue(id, out int i))
                    idx = i;

                else
                    idx = _records.FindIndex((r) => r.Id == id && r.State == RecordState.Fresh);

                if (idx > -1)
                {
                    long utcTimestamp = DateTime.UtcNow.ToBinary();
                    MemDbRecord<T> match = _records[idx];
                    this.ApplyUpdate(apply, match, utcTimestamp);
                }
            }

            return idx > -1;
        }

        private void ApplyUpdate(Action<T> apply, MemDbRecord<T> to, long utcTimestamp)
        {
            //We must deep copy here...if not, the old cache value(s) (that have not yet been flushed to disk)
            //will get the update.  We need a traceable / archiveable state for each update.  If we don't deep copy
            //and multi updates are applied to the same record before a disk flush, then all the records updated
            //between flushes receive all updates and look identical if archived.
            var newRec = new MemDbRecord<T>(to.Id, _cloner.DeepCopy(to.Value), utcTimestamp, to.IsEncrypted);

            //we know the MemDb instance is encryption ready if anything encrypted was ever read into or inserted
            //into the cache (the cache will not contain encrypted data if not encryption ready).

            to.MarkStale(utcTimestamp);
            apply(newRec.Value);

            newRec.CacheIndex = _records.Count;
            _records.Add(newRec);

            if (_isIndexed)
                _index[newRec.Id] = newRec.CacheIndex;

            _appliedIndexes?.Refresh(stale: (to.Value, to.CacheIndex), fresh: (newRec.Value, newRec.CacheIndex) );

            if (_persister is not null)
            {
                _persister.Insert(newRec);
                _persister.MarkStale(to);
            }
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
                for (int i = 0; i < _records.Count; i++)
                {
                    MemDbRecord<T> record = _records[i];
                    if (record.State == RecordState.Fresh && where(record.Value))
                    {
                        this.Delete(record, utcTimestamp);
                        cnt += 1;
                    }
                }
            }

            return cnt;
        }

        public bool Delete(long id)
        {
            this.EnsureMode(AccessMode.ReadWrite, nameof(Delete));

            int idx = -1;
            lock (_lock)
            {
                if (_isIndexed && _index.TryGetValue(id, out int i))
                    idx = i;

                else
                    idx = _records.FindIndex((r) => r.Id == id && r.State == RecordState.Fresh);

                if (idx > -1)
                {
                    long utcTimestamp = DateTime.UtcNow.ToBinary();
                    MemDbRecord<T> match = _records[idx];
                    this.Delete(match, utcTimestamp);
                }
            }

            return idx > -1;
        }

        private void Delete(MemDbRecord<T> record, long utcTimestamp)
        {
            record.MarkDeleted(utcTimestamp);

            if (_isIndexed)
                _index.Remove(record.Id);

            _appliedIndexes?.Remove(record.Value, record.CacheIndex);

            _persister?.MarkDeleted(record);
        }
        #endregion

        #region resolve statistics
        MemDbStatistics IMemDbCache<T>.ResolveStatistics(Stats statistics)
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
            _index = null;
            _persister?.Dispose();
        }
        #endregion
    }
}
