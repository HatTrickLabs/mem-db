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
        private const int _flushInterval = (1000 * 5);//5 seconds
        private const int _initialQueueCapacity = 128;

        private readonly string _path;
        private readonly string _datasetName;
        private readonly AccessMode _mode;

        private string _fullMapPath;
        private string _fullDbPath;
        private Timer _fileSyncTimer;

        private Queue<MemDbRecord<T>> _insertQueue;
        private object _insertSyncLock;

        private Queue<MemDbRecord> _stateChangeQueue;
        private object _stateChangeSyncLock;

        private MemDbMap _map;

        private object _flushLock;

        private IMemDbSerializer<T> _serializer;
        private IBinaryReadMemDbSerializer<T> _binReadSerializer;//optional impl
        private IMemDbEncryptor _encryptor;
        
        private int _cryptoRecordCount;

        private bool _isClosed;
        #endregion

        #region interface
        public AccessMode Mode => _mode;

        public int RecordCount => _map.FreshCount;

        public bool IsEncryptionReady => _encryptor is not null;
        #endregion

        #region constructors
        internal MemDbMappedFile(string datasetName, string path, AccessMode mode,  IMemDbSerializer<T> serializer)
            : this(datasetName, path, mode, serializer, null)
        { }

        internal MemDbMappedFile(string datasetName, string path, AccessMode mode, IMemDbSerializer<T> serializer, IMemDbEncryptor encryptor)
        {
            if (string.IsNullOrEmpty(datasetName))
                throw new ArgumentException("arg must have a value.", nameof(datasetName));

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("arg must have a value.", nameof(path));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            //the encryptor can be null.

            _path = path;
            _datasetName = datasetName;
            _mode = mode;

            _serializer = serializer;
            if (serializer is IBinaryReadMemDbSerializer<T> binReadSer)
                _binReadSerializer = binReadSer;

            _encryptor = encryptor;

            _fullDbPath = Path.Combine(path, $"htl.{datasetName}.db");
            _fullMapPath = Path.Combine(path, $"htl.{datasetName}.map");

            _flushLock = new();

            _insertQueue = new Queue<MemDbRecord<T>>(_initialQueueCapacity);
            _insertSyncLock = new();
            _stateChangeQueue = new Queue<MemDbRecord>(_initialQueueCapacity);
            _stateChangeSyncLock = new();

            this.Initialize();
        }
        #endregion

        #region initialize
        private void Initialize()
        {
            this.EnsureFiles();

            if (_mode != AccessMode.ReadOnly)
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

        #region initialize mapped records
        void IMemDbPersister<T>.InitializeMappedRecords(out IList<MemDbRecord<T>> records)
        {
            this.EnsureMode(AccessMode.ReadOnly | AccessMode.ReadWrite, nameof(IMemDbPersister<T>.InitializeMappedRecords));

            int encrypted = 0;
            int capacity = _mode == AccessMode.ReadOnly ? _map.FreshCount : (int)(_map.FreshCount * 1.1);
            records = new List<MemDbRecord<T>>(capacity);
            lock (_flushLock)
            {
                using var fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fsDb, Encoding.UTF8, true);

                RecordState fresh = RecordState.Fresh;
                MemDbPointer ptr;
                MemDbRecord<T> record;
                for (int i = 0; i < _map.Count; i++)
                {
                    ptr = _map[i];

                    if (ptr.State != fresh)
                    {
                        if (ptr.IsEncrypted)
                            fsDb.Position += MemDbAESEncryptor.CalculateCryptoByteLength(ptr.Length);

                        else
                            fsDb.Position += ptr.Length;

                        continue;
                    }

                    if (ptr.IsEncrypted && !this.IsEncryptionReady)
                    {
                        fsDb.Position += MemDbAESEncryptor.CalculateCryptoByteLength(ptr.Length);
                        encrypted += 1;
                        continue;
                    }

                    T value = null;
                    if (ptr.IsEncrypted)
                    {
                        Span<byte> raw = _encryptor.Decrypt(fsDb, ptr.Length);
                        value = _serializer.Deserialize(raw);
                        encrypted += 1;
                    }
                    else
                    {
                        //move the deserilize into a func with limited scope in order to take advantage of stackalloc
                        value = this.DeserializeRecord(reader, ptr.Length);
                    }

                    record = new(ptr.Id, value, fresh, ptr.StateSetAt, ptr.IsEncrypted, records.Count, i);
                    records.Add(record);
                }
            }
            _cryptoRecordCount = encrypted;
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

            lock (_insertSyncLock)
            {
                _insertQueue.Enqueue(record);
            }
        }
        #endregion

        #region mark stale
        void IMemDbPersister<T>.MarkStale(MemDbRecord<T> record)
        {
            this.EnsureMode(AccessMode.ReadWrite, nameof(IMemDbPersister<T>.MarkStale));

            lock (_stateChangeSyncLock)
            {
                _stateChangeQueue.Enqueue(record);//deletes and updates both go to the same queue
            }
        }
        #endregion

        #region mark deleted
        void IMemDbPersister<T>.MarkDeleted(MemDbRecord<T> record)
        {
            this.EnsureMode(AccessMode.ReadWrite, nameof(IMemDbPersister<T>.MarkDeleted));

            lock (_stateChangeSyncLock)
            {
                _stateChangeQueue.Enqueue(record);//deletes and updates both go to the same queue.
            }
        }
        #endregion

        #region try pop insert record
        private bool TryPopInsertRecord(out MemDbRecord<T> record)
        {
            bool found = false;
            lock (_insertSyncLock)
            {
                found = _insertQueue.TryDequeue(out record);
            }
            return found;
        }
        #endregion

        #region try pop state change record
        private bool TryPopStateChangeRecord(out MemDbRecord record)
        {
            bool found = false;
            lock (_stateChangeSyncLock)
            {
                found = _stateChangeQueue.TryDequeue(out record);
            }
            return found;
        }
        #endregion

        #region flush
        void IMemDbPersister<T>.Flush(object state)
        {
            //this is to avoid the timer 'flush' from getting in after
            //the 'close' flush has already entered...
            if (state == null && _isClosed)
                return;

            this.FlushInsertQueue();
            this.FlushStateChangeQueue();

            if (!_isClosed)
            {
                _fileSyncTimer.Change(_flushInterval, Timeout.Infinite);
            }
        }
        #endregion

        #region flush insert queue
        private void FlushInsertQueue()
        {
            //nothing can be inserted if running in readonly mode.
            if (_mode == AccessMode.ReadOnly)
                return;

            int qSizePreFlush = _insertQueue.Count;
            if (qSizePreFlush > 0)
            {
                lock (_flushLock)
                {
                    this.AppendInsertedItems();
                }

                if (!_isClosed && qSizePreFlush > (_initialQueueCapacity * 1.5))
                {
                    lock (_insertSyncLock)
                    {
                        _insertQueue.TrimExcess();
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

            int qSizePreFlush = _stateChangeQueue.Count;
            if (qSizePreFlush > 0)
            {
                lock (_flushLock)
                {
                    this.UpdateItemStates();
                }

                if (!_isClosed && qSizePreFlush > (_initialQueueCapacity * 1.5))
                {
                    lock (_stateChangeSyncLock)
                    {
                        _stateChangeQueue.TrimExcess();
                    }
                }
            }
        }
        #endregion

        #region append inserted items
        private void AppendInsertedItems()
        {
            MemDbRecord<T> record = null;
            var fresh = RecordState.Fresh;
            if (this.TryPopInsertRecord(out record))
            {
                using var fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.ReadWrite);
                using var dbWriter = new BinaryWriter(fsDb, Encoding.UTF8, true);

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

                        var pointer = new MemDbPointer(record.Id, fresh, record.StateSetAt, record.IsEncrypted, startPos, length);
                        record.SetMapIndex(_map.Add(pointer));
                    }
                    catch
                    {
                        //reset the len of file back to position before this pass started.
                        fsDb.SetLength(startPos);
                        //ensure pointers successfully added get flushed before tossing the ex
                        _map.Flush();
                        throw;
                    }

                } while (this.TryPopInsertRecord(out record));
                _map.Flush();
            }
        }
        #endregion

        #region update item state
        private void UpdateItemStates()
        {
            _map.UpdatePointerState(this.TryPopStateChangeRecord);
        }
        #endregion

        #region close
        private void Close(bool isFinalizer = false)
        {
            _isClosed = true;

            _fileSyncTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _fileSyncTimer.Dispose();

            (this as IMemDbPersister<T>).Flush(new());

            if (!isFinalizer)
            {
                GC.SuppressFinalize(this);
            }
        }
        #endregion

        #region dispose
        public void Dispose()
        {
            if (!_isClosed)
            {
                this.Close();
            }
        }
        #endregion

        #region finalizer
        ~MemDbMappedFile()
        {
            if (!_isClosed)
            {
                this.Close(true); //emergency catch all to save un-flushed records if not properly disposed...
            }
        }
        #endregion
    }
}
