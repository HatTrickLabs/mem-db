using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading;

namespace HatTrick.Data
{
    internal sealed class MemDbMappedFile<T> : IMemDbPersister<T>, IMemDbSnapshotter, IDisposable where T : class
    {
        #region internals
        private const int _initialQueueCapacity = 128;

        private readonly string _path;
        private readonly string _datasetName;
        private readonly AccessMode _mode;
        private int _flushInterval;

        private string _fullMapPath;
        private string _fullDbPath;
        private Timer _fileSyncTimer;

        private ObservableQueue<MemDbRecord<T>> _insertQ;
        private Lock _insertQLock;

        private ObservableQueue<MemDbRecord> _stateModQ;
        private Lock _stateModQLock;

        private MemDbMap _map;

        private Lock _flushLock;

        private IMemDbSerializer<T> _serializer;
        private IMemDbEncryptor _encryptor;
        private IMemDbSnapshotter _snapshotter;

        private Action _onHaltCallback;
        private MemDbFlushException _flushEx;

        private bool _isClosed;
        #endregion

        #region interface
        public AccessMode Mode => _mode;

        public int RecordCount => _map.FreshCount;

        public bool IsEncryptionReady => _encryptor is not null;
        #endregion

        #region ctors
        internal MemDbMappedFile(MemDbConfiguration<T> config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            _path = config.DbPath;
            _datasetName = config.DatasetName;
            _mode = config.Mode;
            _flushInterval = config.FlushInterval;

            _serializer = config.GetSerializer();

            _encryptor = config.GetEncryptor();
            _snapshotter = config.GetSnapshotter();

            _fullDbPath = config.GetDbFilePath();
            _fullMapPath = config.GetMapFilePath();

            _flushLock = new();

            _insertQ = new ObservableQueue<MemDbRecord<T>>(_initialQueueCapacity);
            _insertQLock = new();
            _stateModQ = new ObservableQueue<MemDbRecord>(_initialQueueCapacity);
            _stateModQLock = new();

            this.Initialize();
        }
        #endregion

        #region initialize
        private void Initialize()
        {
            this.EnsureFiles();

            if (_mode != AccessMode.ReadOnly && _flushInterval > 0)
            {
                var callback = new TimerCallback((this as IMemDbPersister<T>).Flush);
                _fileSyncTimer = new Timer(callback, null, _flushInterval, Timeout.Infinite);
            }
        }
        #endregion

        #region on halted
        void IMemDbPersister<T>.OnHalted(Action onHaltCallback)
        {
            _onHaltCallback = onHaltCallback;
        }
        #endregion

        #region ensure files
        private void EnsureFiles()
        {
            if (!Directory.Exists(_path))
                Directory.CreateDirectory(_path);

            bool dbExists = File.Exists(_fullDbPath);
            bool mapExists = File.Exists(_fullMapPath);

            //if the db already existed, but the map file does NOT, we got a BAD problem
            if (dbExists && !mapExists)
                throw new InvalidOperationException($"No map file exists for database file: {Path.GetFileName(_fullDbPath)}");

            //if the map already existed, but the db file does NOT, we got a BAD problem
            if (mapExists && !dbExists)
                throw new InvalidOperationException($"No Db file exists for map file: {Path.GetFileName(_fullMapPath)}");

            lock (_flushLock)
            {
                _map = new MemDbMap(_fullMapPath, true, _encryptor);
                if (!dbExists)
                {
                    using var fs = new FileStream(_fullDbPath, FileMode.CreateNew);
                }
            }

        }
        #endregion

        #region ensure mode
        private void EnsureMode(AccessMode isMode, string targetSite)
        {
            if (_isClosed)
            {
                throw new MemDbPersisterDisposedException(
                    message: "Persister has either been closed by consumer OR halted due to flush exception.",
                    flushException: _flushEx//can be null..
                );
            }

            if ((_mode & isMode) == _mode)
                return;

            throw new InvalidOperationException(
                message: $"MemDb instance for dataset '{_datasetName}' is running in '{_mode}' mode...{targetSite} disabled."
            );
        }
        #endregion

        #region read mapped records
        void IMemDbPersister<T>.ReadMappedRecords(out List<MemDbRecord<T>> records)
        {
            this.EnsureMode(AccessMode.ReadOnly | AccessMode.ReadWrite, nameof(IMemDbPersister<T>.ReadMappedRecords));

            bool isCryptoReady = this.IsEncryptionReady;
            this.InitializeRecordList(out records, _mode, isCryptoReady);

            if (_map.Count == 0)
                return;

            lock (_flushLock)
            {
                using var fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read, FileShare.None);
                using var reader = new BinaryReader(fsDb, Encoding.UTF8, true);

                RecordState fresh = RecordState.Fresh;
                MemDbPointer ptr;
                MemDbRecord<T> record;
                for (int i = 0; i < _map.Count; i++)
                {
                    ptr = _map[i];
                    if (ptr.State != fresh || (ptr.IsEncrypted && !isCryptoReady))
                    {
                        fsDb.Position += (ptr.IsEncrypted) ? _encryptor.GetEncryptedLength(ptr.Length) : ptr.Length;
                        continue;
                    }

                    T value = null;
                    if (ptr.IsEncrypted)
                    {
                        Span<byte> raw = _encryptor.Decrypt(fsDb, ptr.Length);
                        value = _serializer.Deserialize(raw);
                    }
                    else
                    {
                        value = this.DeserializeRecord(reader, ptr.Length);
                    }
                    
                    record = new(ptr, value, i);
                    record.SetCacheIndex(records.Count);
                    records.Add(record);
                }
            }
        }
        #endregion

        #region initialize record list
        private void InitializeRecordList(out List<MemDbRecord<T>> records, AccessMode mode, bool cryptoReady)
        {
            MemDbMap map = _map;
            int totalFresh = map.FreshCount;
            int encryptedFresh = map.EncryptedFreshCount;

            int capacity = mode == AccessMode.ReadOnly
                ? cryptoReady
                    ? totalFresh
                    : (totalFresh - encryptedFresh)
                : cryptoReady
                    ? (int)(totalFresh * 1.1)
                    : (int)((totalFresh - encryptedFresh) * 1.1);

            //if the map is empty (brand new db) set the initial capacity equal the same initial capacity as the queues
            records = new List<MemDbRecord<T>>(capacity == 0 ? _initialQueueCapacity : capacity);
        }
        #endregion

        #region serialize record
        private void SerializeRecord(T record, BinaryWriter to)
        {
            _serializer.Serialize(record, to);
        }

        private byte[] SerializeRecord(T record)
        {
            return _serializer.Serialize(record);
        }
        #endregion

        #region deserialize record
        private T DeserializeRecord(BinaryReader from, int length)
        {
            return _serializer.Deserialize(from, length);
        }
        #endregion

        #region get next id
        long IMemDbPersister<T>.GetNextId()
        {
            return _map.GetNextId();
        }
        #endregion

        #region insert
        void IMemDbPersister<T>.Insert(MemDbRecord<T> record)
        {
            this.EnsureMode(AccessMode.AppendOnly | AccessMode.ReadWrite, nameof(IMemDbPersister<T>.Insert));

            lock (_insertQLock)
            {
                _insertQ.Enqueue(record);
            }
        }
        #endregion

        #region mark stale
        void IMemDbPersister<T>.MarkStale(MemDbRecord<T> record)
        {
            this.EnsureMode(AccessMode.ReadWrite, nameof(IMemDbPersister<T>.MarkStale));

            lock (_stateModQLock)
            {
                _stateModQ.Enqueue(record);//deletes and updates both go to the same queue
            }
        }
        #endregion

        #region mark deleted
        void IMemDbPersister<T>.MarkDeleted(MemDbRecord<T> record)
        {
            this.EnsureMode(AccessMode.ReadWrite, nameof(IMemDbPersister<T>.MarkDeleted));

            lock (_stateModQLock)
            {
                _stateModQ.Enqueue(record);//deletes and updates both go to the same queue.
            }
        }
        #endregion

        #region try pop insert record
        private bool TryPopInsertRecord(out MemDbRecord<T> record)
        {
            bool found = false;
            lock (_insertQLock)
            {
                found = _insertQ.TryDequeue(out record);
            }
            return found;
        }
        #endregion

        #region try pop state change record
        private bool TryPopStateChangeRecord(out MemDbRecord record)
        {
            record = null;
            bool found = false;
            lock (_stateModQLock)
            {
                //we must ensure a state change is not attempted on a record that is still sitting in the
                //insert queue...we can simply check that the record needing state change has a map index.
                found = _stateModQ.CanProcessHead((rec) => rec.MapIndex > -1);
                if (found)
                    record = _stateModQ.Dequeue();
            }
            return found;
        }
        #endregion

        #region flush
        void IMemDbPersister<T>.Flush(object context)
        {
            //context will be:
            //null if called from timer
            //false if flush manually called through cache / snapshot
            //true if called from close/dispose

            //this is to avoid the timer 'flush' from getting in after
            //the 'close' flush has already entered...
            if (context == null && _isClosed)
                return;

            try
            {
                this.FlushInserts();
                this.FlushStateChanges();
            }
            catch
            {
                //only throw if called from primary thread and NOT being disposed.
                if (context is false)
                    throw;//this is a manual flush call.
            }

            if (!_isClosed && context is null && _flushInterval > 0)
                _fileSyncTimer.Change(_flushInterval, Timeout.Infinite);
        }
        #endregion

        #region flush inserts
        private void FlushInserts()
        {
            try
            {
                this.FlushInsertQueue();
            }
            catch(Exception ex)
            {
                var flushEx = new MemDbFlushException(ex);
                this.Halt(flushEx);
                throw flushEx;
            }
        }
        #endregion

        #region flush state changes
        private void FlushStateChanges()
        {
            try
            {
                this.FlushStateChangeQueue();
            }
            catch(Exception ex)
            {
                var flushEx = new MemDbFlushException(ex);
                this.Halt(flushEx);
                throw flushEx;
            }
        }
        #endregion

        #region flush insert queue
        private void FlushInsertQueue()
        {
            //nothing can be inserted if running in readonly mode.
            if (_mode == AccessMode.ReadOnly)
                return;

            if (_insertQ.Count > 0)
            {
                lock (_flushLock)
                {
                    this.WriteInsertsToDisk();
                    _map.Flush();
                }

                if (!_isClosed && _insertQ.Capacity > (_initialQueueCapacity * 4))
                {
                    lock (_insertQLock)
                    {
                        if (_insertQ.Count < _initialQueueCapacity)//Q may may be currently growing...
                            _insertQ.TrimExcess(_initialQueueCapacity);
                    }
                }
            }
        }
        #endregion

        #region flush state change queue
        private void FlushStateChangeQueue()
        {
            //only in read/write mode will any state changes be allowed to existing records.
            if (_mode != AccessMode.ReadWrite)
                return;

            if (_stateModQ.Count > 0)
            {
                lock (_flushLock)
                {
                    _map.UpdatePointerStates(this.TryPopStateChangeRecord);
                }

                if (!_isClosed && _stateModQ.Capacity > (_initialQueueCapacity * 4))
                {
                    lock (_stateModQLock)
                    {
                        if (_stateModQ.Count < _initialQueueCapacity)//Q may be currently growing...
                            _stateModQ.TrimExcess(_initialQueueCapacity);
                    }
                }
            }
        }
        #endregion

        #region write inserts to disk
        private void WriteInsertsToDisk()
        {
            using var fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var dbWriter = new BinaryWriter(fsDb, Encoding.UTF8, true);
            fsDb.Position = fsDb.Length;

            while (this.TryPopInsertRecord(out MemDbRecord<T> record))
            {
                long startPos = fsDb.Position;
                try
                {
                    int length;
                    if (record.IsEncrypted)
                    {
                        byte[] raw = this.SerializeRecord(record.Value);
                        _encryptor.Encrypt(raw, fsDb);
                        //we must record the RAW length of the record NOT crypto...we can calc crypto len on read
                        length = raw.Length;
                    }
                    else
                    {
                        this.SerializeRecord(record.Value, dbWriter);
                        length = (int)(fsDb.Position - startPos);
                    }
                    record.SetPosition(startPos);
                    record.SetLength(length);
                    int mapIdx = _map.Add(record.GetPointer());
                    record.SetMapIndex(mapIdx);
                }
                catch
                {
                    try
                    {
                        //reset the len of file back to position + length of the last pointer.
                        MemDbPointer lPtr = _map[^1];
                        int recLength = lPtr.IsEncrypted ? _encryptor.GetEncryptedLength(lPtr.Length) : lPtr.Length;
                        int end = (int)lPtr.Position + recLength;
                        if (fsDb.Length > end)
                            fsDb.SetLength(end);

                        //ensure pointers successfully added get flushed before tossing the ex
                        _map.Flush();
                    }
                    catch { /* must supress this one... */ }

                    throw;
                }
            }
        }
        #endregion

        #region snapshot
        DateTime IMemDbSnapshotter.Snapshot()
        {
            this.EnsureMode(AccessMode.ReadWrite, nameof(IMemDbSnapshotter.Snapshot));

            (this as IMemDbPersister<T>).Flush(false);

            lock (_flushLock)
            {
                DateTime utcNow = _snapshotter.Snapshot();
                return utcNow;
            }
        }
        #endregion

        #region resolve statistics
        public MemDbStatistics ResolveStatistics(Stats statistics) 
        {
            var stats = new MemDbStatistics();
            if ((statistics & Stats.FreshCount) == Stats.FreshCount)
                stats.FreshCount = _map.FreshCount;

            if ((statistics & Stats.StaleCount) == Stats.StaleCount)
                stats.StaleCount = _map.StaleCount;

            if ((statistics & Stats.DeletedCount) == Stats.DeletedCount)
                stats.DeletedCount = _map.DeletedCount;

            if ((statistics & Stats.FreshSize) == Stats.FreshSize)
                stats.FreshSize = _map.TotalFreshSize;

            if ((statistics & Stats.StaleSize) == Stats.StaleSize)
                stats.StaleSize = _map.TotalStaleSize;

            if ((statistics & Stats.DeletedSize) == Stats.DeletedSize)
                stats.DeletedSize = _map.TotalDeletedSize;

            if ((statistics & Stats.MaxFreshSize) == Stats.MaxFreshSize)
                stats.MaxFreshSize = _map.MaxFreshRecordSize;

            if ((statistics & Stats.MaxStaleSize) == Stats.MaxStaleSize)
                stats.MaxStaleSize = _map.MaxStaleRecordSize;

            if ((statistics & Stats.MaxDeletedSize) == Stats.MaxDeletedSize)
                stats.MaxDeletedSize = _map.MaxDeletedRecordSize;

            if ((statistics & Stats.MinFreshSize) == Stats.MinFreshSize)
                stats.MinFreshSize = _map.MinFreshRecordSize;

            if ((statistics & Stats.MinStaleSize) == Stats.MinStaleSize)
                stats.MinStaleSize = _map.MinStaleRecordSize;

            if ((statistics & Stats.MinDeletedSize) == Stats.MinDeletedSize)
                stats.MinDeletedSize = _map.MinDeletedRecordSize;

            if ((statistics & Stats.AvgFreshSize) == Stats.AvgFreshSize)
                stats.AvgFreshSize = _map.AvgFreshRecordSize;

            if ((statistics & Stats.AvgStaleSize) == Stats.AvgStaleSize)
                stats.AvgStaleSize = _map.AvgStaleRecordSize;

            if ((statistics & Stats.AvgDeletedSize) == Stats.AvgDeletedSize)
                stats.AvgDeletedSize = _map.AvgDeletedRecordSize;

            if ((statistics & Stats.LastId) == Stats.LastId)
                stats.LastId = _map.LastId;

            return stats;
        }
        #endregion

        #region halt
        private void Halt(MemDbFlushException flushEx)
        {
            _isClosed = true;
            _fileSyncTimer?.Dispose();
            GC.SuppressFinalize(this);
            _flushEx = flushEx;
            _onHaltCallback?.Invoke();
        }
        #endregion

        #region get halt exception
        MemDbException IMemDbPersister<T>.GetHaltException()
        {
            return _flushEx;
        }
        #endregion

        #region close
        private void Close(bool isFinalizer = false)
        {
            _isClosed = true;
            _fileSyncTimer?.Dispose();
            (this as IMemDbPersister<T>)?.Flush(true);
            if (!isFinalizer)
                GC.SuppressFinalize(this);
        }
        #endregion

        #region dispose
        public void Dispose()
        {
            if (!_isClosed)
                this.Close();
        }
        #endregion

        #region finalizer
        ~MemDbMappedFile()
        {
            if (!_isClosed)
                this.Close(true); //emergency catch all to save un-flushed records if not properly disposed...
        }
        #endregion
    }
}