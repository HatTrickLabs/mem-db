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
        private const int _flushInterval = (1000 * 2);//5 seconds

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
            _encryptor = encryptor;

            _fullDbPath = Path.Combine(path, $"htl.{datasetName}.db");
            _fullMapPath = Path.Combine(path, $"htl.{datasetName}.map");
            _map = new MemDbMap(_fullMapPath);

            _flushLock = new();

            _insertQueue = new Queue<MemDbRecord<T>>(256);
            _insertSyncLock = new();
            _stateChangeQueue = new Queue<MemDbRecord>(256);
            _stateChangeSyncLock = new();

            this.Initialize();
        }
        #endregion

        #region initialize
        private void Initialize()
        {
            this.EnsureFiles(out bool dbCreated);

            if (!dbCreated)//if the db is NOT brand new, read in the record map file
            {
                _map.InitializeExisting();
            }

            if (_mode != AccessMode.ReadOnly)
                _fileSyncTimer = new Timer(new TimerCallback((this as IMemDbPersister<T>).Flush), null, _flushInterval, Timeout.Infinite); //5 seconds...
        }
        #endregion

        #region ensure files
        private void EnsureFiles(out bool dbCreated)
        {
            dbCreated = false;

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

            if (!dbExists)
            {
                dbCreated = true;
                lock (_flushLock)
                {
                    dbCreated = true;
                    using var fs = new FileStream(_fullDbPath, FileMode.CreateNew);
                }

                _map.InitializeNew();
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
        IList<MemDbRecord<T>> IMemDbPersister<T>.InitializeMappedRecords()
        {
            this.EnsureMode(AccessMode.ReadOnly | AccessMode.ReadWrite, nameof(IMemDbPersister<T>.InitializeMappedRecords));

            //TODO: this should at most be called ONE time on read or readwrite initialization of cache...ensure that...
            int encrypted = 0;
            List<MemDbRecord<T>> records = null;
            lock (_flushLock)
            {
                records = new List<MemDbRecord<T>>((int)(_map.FreshCount * 1.1));
                using var fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fsDb, Encoding.UTF8, true);

                RecordState fresh = RecordState.Fresh;
                MemDbPointer pointer;
                MemDbRecord<T> record;
                for (int i = 0; i < _map.Count; i++)
                {
                    pointer = _map[i];

                    if (pointer.State != fresh)
                    {
                        if (pointer.IsEncrypted)
                            fsDb.Position += MemDbAESEncryptor.CalculateCryptoByteLength(pointer.Length);

                        else
                            fsDb.Position += pointer.Length;

                        continue;
                    }

                    if (pointer.IsEncrypted && !this.IsEncryptionReady)
                    {
                        fsDb.Position += MemDbAESEncryptor.CalculateCryptoByteLength(pointer.Length);
                        encrypted += 1;
                        continue;
                    }

                    T value = null;
                    if (pointer.IsEncrypted)
                    {
                        Span<byte> raw = _encryptor.Decrypt(fsDb, pointer.Length);
                        value = _serializer.Deserialize(raw);
                        encrypted += 1;
                    }
                    else
                    {
                        value = _serializer.Deserialize(reader, pointer.Length);
                    }

                    record = new(pointer.Id, value, fresh, pointer.StateSetAt, pointer.IsEncrypted, records.Count, i);
                    records.Add(record);
                }
            }
            _cryptoRecordCount = encrypted;
            return records;
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

            if (_insertQueue.Count > 0 && (_mode == AccessMode.ReadWrite || _mode == AccessMode.AppendOnly))
                this.AppendInsertedItems();

            if (_stateChangeQueue.Count > 0 && _mode == AccessMode.ReadWrite)
                this.UpdateItemStates();

            if (!_isClosed)
            {
                _fileSyncTimer.Change(_flushInterval, Timeout.Infinite);
            }
        }
        #endregion

        #region append inserted items
        private void AppendInsertedItems()
        {
            lock (_flushLock)
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
                                Span<byte> raw = _serializer.Serialize(record.Value);
                                _encryptor.Encrypt(raw, fsDb);
                                //we must record the RAW length of the record NOT crypto...we can calc crypto len on read
                                length = raw.Length;
                            }
                            else
                            {
                                _serializer.Serialize(record.Value, dbWriter);
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
