using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading;

namespace HatTrick.InMemDb
{
    internal sealed class MemDbMappedFile<T> : IMemDbPersister<T>, IDisposable where T : class
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
        private IBinaryReadMemDbSerializer<T> _binReadSerializer;//optional impl
        private IMemDbEncryptor _encryptor;
        private IMemDbSnapshotter _snapshotter;

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

            _path = config.Path;
            _datasetName = config.DatasetName;
            _mode = config.Mode;
            _flushInterval = config.FlushInterval;

            _serializer = config.GetSerializer();
            if (_serializer is IBinaryReadMemDbSerializer<T> binReadSerializer)
                _binReadSerializer = binReadSerializer;

            _encryptor = config.GetEncryptor();
            _snapshotter = config.GetSnapshotter();

            _fullDbPath = config.GetFullDbFilePath();
            _fullMapPath = config.GetFullMapFilePath();

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

            _map = new MemDbMap(_fullMapPath, true);

            if (!dbExists)
            {
                lock (_flushLock)
                {
                    using var fs = new FileStream(_fullDbPath, FileMode.CreateNew);
                }
            }
        }
        #endregion

        #region ensure mode
        private void EnsureMode(AccessMode isMode, string targetSite)
        {
            if ((_mode & isMode) == _mode)
                return;

            string msg = $"MemDb instance for dataset '{_datasetName}' is running in '{_mode}' mode...{targetSite} disabled.";
            throw new InvalidOperationException(msg);
        }
        #endregion

        #region read mapped records
        void IMemDbPersister<T>.ReadMappedRecords(out IList<MemDbRecord<T>> records)
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
                        fsDb.Position += (ptr.IsEncrypted) 
                            ? MemDbAESEncryptor.CalculateCryptoByteLength(ptr.Length) 
                            : ptr.Length;

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
                    record = new(ptr.Id, value, fresh, ptr.StateSetAt, ptr.CreatedAt, ptr.IsEncrypted, records.Count, i);
                    records.Add(record);
                }
            }
        }
        #endregion

        #region initialize record list
        private void InitializeRecordList(out IList<MemDbRecord<T>> records, AccessMode mode, bool cryptoReady)
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
            if (_binReadSerializer is not null)
                return _binReadSerializer.Deserialize(from);

            Span<byte> raw = length > 2048 ? new byte[length] : stackalloc byte[length];
            from.BaseStream.ReadExactly(raw);
            return _serializer.Deserialize(raw);
        }
        #endregion

        #region get next id
        uint IMemDbPersister<T>.GetNextId()
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
                //we must ensure we don't process any updates that need to be applied to records still
                //sitting in the insert queue...we can simply check that the record to update has a map index.
                found = _stateModQ.CanProcessHead((rec) => rec.MapIndex > -1);
                if (found)
                    record = _stateModQ.Dequeue();
            }
            return found;
        }
        #endregion

        #region flush
        void IMemDbPersister<T>.Flush(object state)
        {
            //state will be:
            //null if called from timer
            //false if flush manually called through cache / snapshot
            //true if called from close/dispose

            //this is to avoid the timer 'flush' from getting in after
            //the 'close' flush has already entered...
            if (state == null && _isClosed)
                return;

            try
            {
                this.FlushInsertQueue();
                this.FlushStateChangeQueue();
            }
            catch (Exception ex)
            {
                //TODO: need some way to bubble an exception up to the main process thread
                //when the exception is thrown from the timer fired thread
                //i.e. wire up an OnException delegate that the main thread can bind to 
            }

            if (!_isClosed && _flushInterval > 0)
                _fileSyncTimer.Change(_flushInterval, Timeout.Infinite);
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
                    this.AppendInsertedItems();
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
                    this.UpdateItemStates();
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

        #region append inserted items
        private void AppendInsertedItems()
        {
            MemDbRecord<T> record = null;
            if (!this.TryPopInsertRecord(out record))
                return;

            using (var fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                using (var dbWriter = new BinaryWriter(fsDb, Encoding.UTF8, true))
                {
                    fsDb.Position = fsDb.Length;
                    do
                    {
                        uint startPos = (uint)fsDb.Position;
                        try
                        {
                            int length;
                            if (record.IsEncrypted)
                            {
                                Span<byte> raw = this.SerializeRecord(record.Value);
                                _encryptor.Encrypt(raw, fsDb);
                                //we must record the RAW length of the record NOT crypto...we can calc crypto len on read
                                length = raw.Length;
                            }
                            else
                            {
                                this.SerializeRecord(record.Value, dbWriter);
                                length = (int)(fsDb.Position - startPos);
                            }
                            var pointer = new MemDbPointer(record.Id, RecordState.Fresh, record.StateSetAt, record.CreatedAt, record.IsEncrypted, startPos, length);
                            record.SetMapIndex(_map.Add(pointer));
                        }
                        catch
                        {
                            var operation = () =>
                            {
                                //reset the len of file back to position + length of the last pointer.
                                MemDbPointer lPtr = _map[^1];
                                int recLength = lPtr.IsEncrypted ? MemDbAESEncryptor.CalculateCryptoByteLength(lPtr.Length) : lPtr.Length;
                                int end = (int)lPtr.Position + lPtr.Length;
                                if (fsDb.Length > end)
                                    fsDb.SetLength(lPtr.Position + lPtr.Length);

                                //ensure pointers successfully added get flushed before tossing the ex
                                _map.Flush();
                            };

                            _ = this.TryWrapperOperation(operation);

                            throw;
                        }
                    } while (this.TryPopInsertRecord(out record));
                }
            }
            _map.Flush();
        }
        #endregion

        #region try action
        private bool TryWrapperOperation(Action operation)
        {
            try
            {
                operation?.Invoke();
                return true;
            }
            catch//catch all...
            { 
                return false; 
            }
        }
        #endregion

        #region update item states
        private void UpdateItemStates()
        {
            _map.UpdatePointerState(this.TryPopStateChangeRecord);
        }
        #endregion

        #region snapshot
        DateTime IMemDbPersister<T>.Snapshot()
        {
            (this as IMemDbPersister<T>).Flush(false);

            lock (_flushLock)
            {
                var now = DateTime.Now;
                _snapshotter.WriteSnapshot(now);
                return now;
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

        #region close
        private void Close(bool isFinalizer = false)
        {
            _isClosed = true;
            _fileSyncTimer?.Dispose();
            (this as IMemDbPersister<T>).Flush(true);
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