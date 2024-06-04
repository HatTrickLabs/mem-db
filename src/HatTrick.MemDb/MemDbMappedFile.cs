using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading;

namespace HatTrick.MemDb
{
    internal sealed class MemDbMappedFile<T> : IMemDbPersister<T>, IDisposable where T : class, new()
    {
        #region internals
        private readonly string _path;
        private readonly string _datasetName;
        private readonly AccessMode _mode;

        private string _fullMapPath;
        private string _fullDbPath;
        private object _dbSyncLock;
        private Timer _fileSyncTimer;

        private Queue<MemDbRecord<T>> _insertQueue;
        private object _insertSyncLock;

        private Queue<MemDbRecord> _updateStateQueue;
        private object _updateStateSyncLock;

        private MemDbMap _map;
        private object _mapSyncLock;

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

        public MemDbMappedFile(string datasetName, string path, AccessMode mode, IMemDbSerializer<T> serializer, IMemDbEncryptor encryptor)
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
            _dbSyncLock = new();

            _serializer = serializer;
            _encryptor = encryptor;

            _fullDbPath = Path.Combine(path, $"htl.{datasetName}.db");
            _fullMapPath = Path.Combine(path, $"htl.{datasetName}.map");
            _mapSyncLock = new();
            _map = new MemDbMap(_fullMapPath);

            _insertQueue = new Queue<MemDbRecord<T>>(256);
            _insertSyncLock = new();
            _updateStateQueue = new Queue<MemDbRecord>(256);
            _updateStateSyncLock = new();

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
                _fileSyncTimer = new Timer(new TimerCallback(this.Flush), null, (1000 * 5), Timeout.Infinite); //5 seconds...
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
                lock (_dbSyncLock)
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

        #region read all
        public IList<MemDbRecord<T>> ReadAll()
        {
            this.EnsureMode(AccessMode.ReadOnly | AccessMode.ReadWrite, nameof(ReadAll));

            //TODO: this should at most be called ONE time on read or readwrite initialization of cache...ensure that...
            int encrypted = 0;
            List<MemDbRecord<T>> records = null;
            lock (_mapSyncLock)
            {
                lock (_dbSyncLock)
                {
                    records = new List<MemDbRecord<T>>((int)(_map.FreshCount * 1.1));
                    using var fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read);
                    using var reader = new BinaryReader(fsDb, Encoding.UTF8, true);

                    MemDbPointer pointer;
                    for (int i = 0; i < _map.Count; i++)
                    {
                        pointer = _map[i];

                        if (pointer.State != RecordState.Fresh)
                        {
                            fsDb.Position += pointer.Length;
                            continue;
                        }

                        if (pointer.IsEncrypted && !this.IsEncryptionReady)
                        {
                            //remember pointer.Length is the un-encrypted record length, must shift
                            //forward the actual length of the encrypted data
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
                            value = _serializer.Deserialize(reader, pointer.Length);//T value
                        }

                        var record = new MemDbRecord<T>(pointer.Id, value, RecordState.Fresh, pointer.IsEncrypted, records.Count, i);
                        records.Add(record);
                    }
                }
            }
            _cryptoRecordCount = encrypted;
            return records;
        }
        #endregion

        #region get next id
        public uint GetNextId()
        {
            return _map.GetNextId();
        }
        #endregion

        #region insert
        public void Insert(MemDbRecord<T> record)
        {
            this.EnsureMode(AccessMode.AppendOnly | AccessMode.ReadWrite, nameof(Insert));

            lock (_insertSyncLock)
            {
                _insertQueue.Enqueue(record);
            }
        }
        #endregion

        #region mark stale
        public void MarkStale(MemDbRecord<T> record)
        {
            this.EnsureMode(AccessMode.ReadWrite, nameof(MarkStale));

            lock (_updateStateSyncLock)
            {
                _updateStateQueue.Enqueue(record);
            }
        }
        #endregion

        #region mark deleted
        public void MarkDeleted(MemDbRecord<T> record)
        {
            this.EnsureMode(AccessMode.ReadWrite, nameof(MarkDeleted));

            lock (_updateStateSyncLock)
            {
                _updateStateQueue.Enqueue(record);
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

        #region try pop stale record
        private bool TryPopStaleRecord(out MemDbRecord record)
        {
            bool found = false;
            lock (_updateStateSyncLock)
            {
                found = _updateStateQueue.TryDequeue(out record);
            }
            return found;
        }
        #endregion

        #region flush
        public void Flush(object state)
        {
            if (state == null && _isClosed)
                return;

            if (_mode == AccessMode.ReadWrite || _mode == AccessMode.AppendOnly)
                this.AppendInsertedItems();

            if (_mode == AccessMode.ReadWrite)
                this.UpdateItemStates();

            if (!_isClosed)
            {
                _fileSyncTimer.Change((1000 * 5), Timeout.Infinite);//5 seconds...
            }
        }
        #endregion

        #region append inserted items
        private void AppendInsertedItems()
        {
            lock (_mapSyncLock)
            {
                lock (_dbSyncLock)
                {
                    MemDbRecord<T> rec = null;
                    if (this.TryPopInsertRecord(out rec))
                    {
                        using var fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.ReadWrite);
                        using var dbWriter = new BinaryWriter(fsDb, Encoding.UTF8, true);

                        fsDb.Position = fsDb.Length;
                        do
                        {
                            uint startPos = (uint)fsDb.Position;
                            int length;

                            MemDbPointer pointer = null;
                            if (rec.IsEncrypted)
                            {
                                Span<byte> raw = _serializer.Serialize(rec.Value);
                                _encryptor.Encrypt(raw, fsDb);
                                //we must record the RAW length of the record NOT crypto...we can calc crypto len on read
                                pointer = new MemDbPointer(rec.Id, RecordState.Fresh, true, startPos, raw.Length);
                            }
                            else
                            {
                                _serializer.Serialize(rec.Value, dbWriter);
                                length = (int)(fsDb.Position - startPos);
                                pointer = new MemDbPointer(rec.Id, RecordState.Fresh, false, startPos, length);
                            }

                            rec.SetMapIndex(_map.Add(pointer));

                        } while (this.TryPopInsertRecord(out rec));
                        _map.Flush();
                    }
                }
            }
        }
        #endregion

        #region update item state
        private void UpdateItemStates()
        {
            _map.UpdatePointerState(this.TryPopStaleRecord);
        }
        #endregion

        #region close
        private void Close(bool isFinalizer = false)
        {
            _isClosed = true;

            _fileSyncTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _fileSyncTimer.Dispose();

            this.Flush(new());

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
                this.Close(true); //emergency catch all to save flushed records if process dies...
            }
        }
        #endregion
    }
}
