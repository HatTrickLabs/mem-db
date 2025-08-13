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

        private bool _persisterHalted;
        #endregion

        #region ctors
        internal MemDbCache(MemDbConfiguration<T> config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            _datasetName = config.DatasetName;
            _cloner = config.GetCloner();
            _lock = new();

            if (config.DbPath is not null)
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
            _records = records;
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

            _persister.OnHalted(this.PersisterHalted);
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

            int capacity = stats.fresh == 0 ? 128 : (int)(stats.fresh * 1.025);
            var newSet = new List<MemDbRecord<T>>(capacity);
            var newIndex = _isIndexed ? new Dictionary<long, int>(capacity) : null;

            lock (_lock)
            {
                for (int i = 0; i < _records.Count; i++)
                {
                    var record = _records[i];
                    int idx = newSet.Count;
                    if (record.State == RecordState.Fresh)
                    {
                        record.CacheIndex = idx;
                        newSet.Add(record);
                        newIndex?.Add(record.Id, idx);
                        _appliedIndexes?.Refresh(stale: (record.Value, i), fresh: (record.Value, idx));
                    }
                    else if (i >= upperBound)
                    {
                        //if the rec has been marked stale or deleted AFTER ResolveCacheStats (above),
                        //we still need to shift it over so the cache stats returned are accurate...
                        record.CacheIndex = idx;
                        newSet.Add(record);
                    }
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
                //immediately after exiting lock, new records may be added to _records...return the upper
                //bound so purge doesn't look past the upper bound index of when these stats are were resolved
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
            if (_persisterHalted)//if we are in readonly mode, the persister never existed and therefore cannot be halted.
            {
                throw new MemDbPersisterDisposedException(
                    message: "Cache persister has been halted due to a flush exception.",
                    flushException: _persister?.GetHaltException()//can be null..
                );
            }

            if ((_mode & isMode) == _mode)
                return;

            throw new InvalidOperationException(
                message: $"MemDb instance for dataset '{_datasetName}' is running in '{_mode}' mode...'{targetSite}' accessor is disabled."
            );
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
            List<T> matches = null;
           
            lock (_lock)
            {
                if (_isIndexed)
                {
                    matches = new List<T>(ids.Length);
                    foreach (long id in ids)
                    {
                        if (_index.TryGetValue(id, out int index))
                            matches.Add(_records[index].Value);
                    }
                }
                else
                {
                    matches = _records
                            .FindAll((r) => r.State == RecordState.Fresh && Array.Exists(ids, (id) => r.Id == id))
                            .ConvertAll(r => r.Value);
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

            lock (_lock)
            {
                var matches = new List<MemDbRecord<T>>();
                for (int i = 0; i < _records.Count; i++)
                {
                    MemDbRecord<T> r = _records[i];
                    if (filter(r))
                        matches.Add(r);
                }

                int count = matches.Count;
                int skip = expression.SkipCount;
                int limit = expression.LimitCount;

                if (count == 0 || skip >= count)
                    return Array.Empty<MemDbRecord<T>>();

                if (expression.HasOrderBy && count > 1)
                    matches.Sort((a, b) => expression.OrderByComparison(a.Value, b.Value));

                if (expression.HasSkip || expression.HasLimit)
                {
                    if (limit > count - skip)
                        limit = count - skip;

                    matches = matches.Slice(skip, limit);
                }

                return matches.ToArray();
            }
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
        public IMemDbIndexExpressionRoot<T, YIndex> QueryViaIndex<YIndex>(string indexName) where YIndex : IConvertible
        {
            this.EnsureReadMode(nameof(QueryViaIndex));

            if (_appliedIndexes is null)
                throw new InvalidOperationException($"No custom indexes applied to MemDb Instance '{_datasetName}'");

            var index = _appliedIndexes.Get(indexName);
            if (index is null)
                throw new ArgumentException($"No custom index exists on MemDb instance '{_datasetName}' with provided name '{indexName}'", nameof(indexName));

            //ensure generic type requested is a match to the index type...
            MemDbIndex<T, YIndex> typeIndex = index.Of<YIndex>();

            return new MemDbIndexExpression<T, YIndex>(index.Name, this.ExecuteIndexQueryExpression, this.ExecuteIndexedUpdateExpression, this.ExecuteIndexedDeleteExpression);
        }
        #endregion

        #region execute indexed query expression
        private T[] ExecuteIndexQueryExpression<YIndex>(MemDbIndexExpression<T, YIndex> expression, bool deepCopy) where YIndex : IConvertible
        {
            MemDbRecord<T>[] records = this.ExecuteIndexQueryExpression(expression);

            return deepCopy
                ? Array.ConvertAll(records, r => _cloner.DeepCopy(r.Value))
                : Array.ConvertAll(records, r => r.Value);
        }

        private MemDbRecord<T>[] ExecuteIndexQueryExpression<YIndex>(MemDbIndexExpression<T, YIndex> expression) where YIndex : IConvertible
        {
            string idxName = expression.IndexName;
            MemDbIndex<T, YIndex> index = _appliedIndexes.Get(idxName).Of<YIndex>();

            lock (_lock)
            {
                int[] pointers = null;
                switch (expression.RelationalOperator)
                {
                    case RelationalOperator.EqualTo:
                        pointers = index.EqualTo(expression.IndexKey);
                        break;
                    case RelationalOperator.In:
                        pointers = index.In(expression.IndexKeySet);
                        break;
                    case RelationalOperator.NotEqualTo:
                        pointers = index.NotEqualTo(expression.IndexKey);
                        break;
                    case RelationalOperator.GreaterThan:
                        pointers = index.GreaterThan(expression.IndexKey);
                        break;
                    case RelationalOperator.LessThan:
                        pointers = index.LessThan(expression.IndexKey);
                        break;
                    case RelationalOperator.GreaterThanEqualTo:
                        pointers = index.GreaterThanEqualTo(expression.IndexKey);
                        break;
                    case RelationalOperator.LessThanEqualTo:
                        pointers = index.LessThanEqualTo(expression.IndexKey);
                        break;
                    default:
                        throw new NotImplementedException($"Index expression for {expression.RelationalOperator} not implemented.");
                }

                int length = pointers.Length;
                var set = new MemDbRecord<T>[length];

                int skip = expression.SkipCount;
                int limit = expression.LimitCount;

                if (length == 0 || skip >= length)
                    return Array.Empty<MemDbRecord<T>>();

                for (int i = 0; i < pointers.Length; i++)
                {
                    set[i] = _records[pointers[i]];
                }

                if (expression.HasOrderBy && length > 1)
                    Array.Sort(set, (a,b) => expression.OrderByComparison(a.Value, b.Value));

                if (expression.HasSkip || expression.HasLimit)
                {
                    if (limit > length - skip)
                        limit = length - skip;

                    set = set[skip..(skip + limit)];
                }
                return set;
            }
        }
        #endregion

        #region execute indexed update expression
        private int ExecuteIndexedUpdateExpression<YIndex>(MemDbIndexExpression<T, YIndex> expression, Action<T> apply) where YIndex : IConvertible
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
        private int ExecuteIndexedDeleteExpression<YIndex>(MemDbIndexExpression<T, YIndex> expression) where YIndex : IConvertible
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

            if (record is null)
                throw new ArgumentNullException(nameof(record));

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

            _appliedIndexes?.Refresh
            (
                stale: (to.Value, to.CacheIndex), 
                fresh: (newRec.Value, newRec.CacheIndex)
            );

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

        #region persister halted
        private void PersisterHalted()
        {
            _persisterHalted = true;
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
