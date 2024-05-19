using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;

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

        private Queue<MemDbRecord<T>> _staleStateQueue;
        private object _staleStateSyncLock;

        private MemDbMap _map;
        private object _mapSyncLock;

        private IMemDbSerializer<T> _serializer;
        private IMemDbEncrypter<T> _encrypter;
        
        private int _cryptoRecordCount;

        private bool _isClosed;
        #endregion

        #region interface
        public AccessMode Mode => _mode;

        public int RecordCount => _map.FreshCount;

        public bool IsEncryptionReady => _encrypter is not null;
        #endregion

        #region constructors
        internal MemDbMappedFile(string datasetName, string path, AccessMode mode,  IMemDbSerializer<T> serializer)
            : this(datasetName, path, mode, serializer, null)
        { }

        public MemDbMappedFile(string datasetName, string path, AccessMode mode, IMemDbSerializer<T> serializer, IMemDbEncrypter<T> encrypter)
        {
            if (string.IsNullOrEmpty(datasetName))
                throw new ArgumentException("arg must have a value.", nameof(datasetName));

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("arg must have a value.", nameof(path));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            _path = path;
            _datasetName = datasetName;
            _mode = mode;
            _dbSyncLock = new();

            _serializer = serializer;
            _encrypter = encrypter;//can be null...

            _fullDbPath = Path.Combine(path, $"htl.{datasetName}.db");
            _fullMapPath = Path.Combine(path, $"htl.{datasetName}.map");
            _mapSyncLock = new();
            _map = new MemDbMap();

            _insertQueue = new Queue<MemDbRecord<T>>(256);
            _insertSyncLock = new();
            _staleStateQueue = new Queue<MemDbRecord<T>>(256);
            _staleStateSyncLock = new();

            this.Initialize();
        }
        #endregion

        #region initialize
        private void Initialize()
        {
            this.EnsureFiles(out bool dbCreated);

            if (!dbCreated)//if the db is NOT brand new, read int the record map file
            {
                lock (_mapSyncLock)
                {
                    using var fsMap = new FileStream(_fullMapPath, FileMode.Open, FileAccess.Read);
                    using var reader = new BinaryReader(fsMap, Encoding.UTF8, true);
                    _map.DeserializeFrom(reader);
                }
            }

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

                lock (_mapSyncLock)
                {
                    using var fs = new FileStream(_fullMapPath, FileMode.CreateNew);
                    _map.SerializeTo(fs);
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

        #region read all
        public IList<MemDbRecord<T>> ReadAll()
        {
            this.EnsureMode(AccessMode.ReadOnly | AccessMode.ReadWrite, nameof(ReadAll));

            int encrypted = 0;
            List<MemDbRecord<T>> records = null;
            lock (_mapSyncLock)
            {
                lock (_dbSyncLock)
                {
                    this.Flush(null);
                    records = new List<MemDbRecord<T>>(_map.Count);
                    using var fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read);
                    using var reader = new BinaryReader(fsDb, Encoding.UTF8, true);

                    MemDbPointer pointer;
                    for (int i = 0; i < _map.Count; i++)
                    {
                        pointer = _map[i];

                        if (pointer.IsStale)
                        {
                            fsDb.Position += pointer.Length;
                        }
                        else if (pointer.IsEncrypted)
                        {
                            encrypted += 1;
                            if (!this.IsEncryptionReady)
                            {
                                fsDb.Position += pointer.Length;
                            }
                            else
                            {
                                //TODO:
                            }
                        }
                        else
                        {
                            T value = _serializer.Deserialize(reader);//T value
                            var record = new MemDbRecord<T>(pointer.Id, value, false, pointer.IsEncrypted, i, i);
                            record.SetMapIndex(i);
                            record.SetCacheIndex(i);
                            records.Add(record);
                        }
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

            //TODO: We should probably ENSURE that the cache layer actualy set the requird Id
            //this may be happening in the MemDbRecord<T> constructor...
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

            lock (_staleStateSyncLock)
            {
                _staleStateQueue.Enqueue(record);
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
        private bool TryPopStaleRecord(out MemDbRecord<T> record)
        {
            bool found = false;
            lock (_staleStateSyncLock)
            {
                found = _staleStateQueue.TryDequeue(out record);
            }
            return found;
        }
        #endregion

        #region flush
        public void Flush(object state)
        {
            if (state == null && _isClosed)
                return;

            this.AppendInsertedItems();
            this.MarkStaleItems();

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
                        using var fsMap = new FileStream(_fullMapPath, FileMode.Open, FileAccess.ReadWrite);
                        using var mapWriter = new BinaryWriter(fsMap, Encoding.UTF8, true);
                        using var fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.ReadWrite);
                        using var dbWriter = new BinaryWriter(fsDb, Encoding.UTF8, true);

                        fsMap.Position = fsMap.Length;
                        fsDb.Position = fsDb.Length;
                        do
                        {
                            uint startPos = (uint)fsDb.Position;
                            int length;

                            if (rec.IsEncrypted)
                            {
                                //TODO:
                                //using var msClear = new MemoryStream();
                                //using var localWriter = new BinaryWriter(msClear, Encoding.UTF8, true);
                                //using var msEncrypted = new MemoryStream();

                                //MemDbRecordSerializer.Serialize(rec, localWriter);//MemDbRecord interface
                                //_serializer.Serialize(rec.Value, localWriter);//T value
                                //msClear.Position = 0;
                                //_encrypter.Encrypt(msClear, msEncrypted, rec.Id.ToString("0000000000"));
                                //msEncrypted.CopyTo(fsDb);
                            }
                            else
                            {
                                //MemDbRecordSerializer.Serialize(rec, dbWriter);//MemDbRecord interfce
                                _serializer.Serialize(rec.Value, dbWriter);//T value
                            }

                            length = (int)(fsDb.Position - startPos);
                            MemDbPointer pointer = new MemDbPointer(rec.Id, false, rec.IsEncrypted, startPos, length);
                            rec.SetMapIndex(_map.Add(pointer));
                            pointer.SerializeTo(mapWriter);

                        } while (this.TryPopInsertRecord(out rec));

                        //overwrite the map pointer count header.
                        fsMap.Position = 0;
                        mapWriter.Write(_map.Count);
                    }
                }
            }
        }
        #endregion

        #region mark stale items
        private void MarkStaleItems()
        {
            lock (_mapSyncLock)
            {
                MemDbRecord<T> rec = null;
                if (!this.TryPopStaleRecord(out rec))
                    return;

                using var fsMap = new FileStream(_fullMapPath, FileMode.Open, FileAccess.ReadWrite);
                do
                {
                    MemDbPointer p = _map[rec.MapIndex].MarkStale();
                    //header count + (idx * size) + id
                    fsMap.Position = sizeof(int) + (rec.MapIndex * MemDbPointer.Size) + sizeof(int);
                    fsMap.WriteByte(1);//1 == true (IsStale = true)

                } while (this.TryPopStaleRecord(out rec));
            }
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
