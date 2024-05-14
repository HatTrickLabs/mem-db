using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;

namespace HatTrick.MemDb
{
    internal sealed class MappedFileStorageProvider<T> : IMemDbStorageProvider<T>, IDisposable where T : class, new()
    {
        #region internals
        private string _path;
        private string _name;

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

        private IMemDbCryptoProvider _cryptoProvider;
        private int _encryptedCount;

        private bool _isClosed;
        #endregion

        #region interface
        internal bool IsEncryptionReady => _cryptoProvider is not null;
        #endregion

        #region constructors
        //string path, string datasetName, ISerializationProvier<T> serializer, ICloneProvider<T>, 
        internal MappedFileStorageProvider(string path, string datasetName, ISerializationProvider<T> serializer)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("arg must have a value.", nameof(path));

            if (string.IsNullOrEmpty(datasetName))
                throw new ArgumentException("arg must have a value.", nameof(datasetName));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            _path = path;
            _name = datasetName;
            _dbSyncLock = new();

            _fullDbPath = Path.Combine(path, $"htl.{datasetName}.db");
            _fullMapPath = Path.Combine(path, $"htl.{datasetName}.map");
            _mapSyncLock = new();
            _map = new MemDbMap();

            _insertQueue = new Queue<MemDbRecord<T>>(256);
            _insertSyncLock = new();
            _staleStateQueue = new Queue<MemDbRecord<T>>(256);
            _staleStateSyncLock = new();

            _fileSyncTimer = new Timer(new TimerCallback(this.Flush), null, (1000 * 5), Timeout.Infinite); //5 seconds...
        }
        #endregion

        #region init
        private void Initialize()
        {
            this.EnsureFiles(out bool dbCreated);

            if (dbCreated)
                return; //brand new db, no need to read map

            lock (_mapSyncLock)
            {
                using var fsMap = new FileStream(_fullMapPath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fsMap, Encoding.UTF8, true);
                _map.DeserializeFrom(reader);
            }
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
            if (!dbExists && !mapExists)
                throw new InvalidOperationException($"No map file exists for database file: {Path.GetFileName(_fullDbPath)}");

            //if the map already existed, but the db file does NOT, we got a BAD problem
            if (mapExists && !dbExists)
                throw new InvalidOperationException($"No Db file exists for map file: {Path.GetFileName(_fullMapPath)}");

            if (!dbExists)
            {
                dbCreated = true;
                lock (_dbSyncLock)
                {
                    using var fs = new FileStream(_fullDbPath, FileMode.CreateNew);
                    dbCreated = true;
                }

                lock (_mapSyncLock)
                {
                    using var fs = new FileStream(_fullMapPath, FileMode.CreateNew);
                    _map.SerializeTo(fs);
                }
            }
        }
        #endregion

        #region read all
        public IEnumerable<MemDbRecord<T>> ReadAll()
        {
            int encrypted = 0;
            lock (_mapSyncLock)
            {
                lock (_dbSyncLock)
                {
                    using var fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read);
                    using var reader = new BinaryReader(fsDb, Encoding.UTF8, true);
                    MemDbPointer pointer;
                    for (int i = 0; i < _map.Pointers.Count; i++)
                    {
                        pointer = _map.Pointers[i];

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
                                //TODO:implement
                            }
                        }
                        else
                        {
                            MemDbRecord<T> rec = new MemDbRecord<T>();
                            rec.DeserializeFrom(reader);
                            rec.MapIndex = i;
                            yield return rec;
                        }
                    }
                }
            }
            _encryptedCount = encrypted;
        }
        #endregion

        #region insert
        public void Insert(MemDbRecord<T> record)
        {
            lock (_insertSyncLock)
            {
                _insertQueue.Enqueue(record);
            }
        }
        #endregion

        #region mark stale
        public void MarkStale(MemDbRecord<T> record)
        {
            lock (_staleStateSyncLock)
            {
                _staleStateQueue.Enqueue(record);
            }
        }
        #endregion

        #region try get insert record
        private bool TryGetInsertRecord(out MemDbRecord<T> record)
        {
            bool found = false;
            lock (_insertSyncLock)
            {
                found = _insertQueue.TryDequeue(out record);
            }
            return found;
        }
        #endregion

        #region try get stale record
        private bool TryGetStaleRecord(out MemDbRecord<T> record)
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

            if (!_isClosed)
            {
                _fileSyncTimer.Change((1000 * 5), Timeout.Infinite);//5 seconds...
            }
        }
        #endregion

        #region insert records
        private void InsertRecords()
        {
            lock (_mapSyncLock)
            {
                lock (_dbSyncLock)
                {
                    MemDbRecord<T> rec = null;
                    if (this.TryGetInsertRecord(out rec))
                    {
                        using var fsMap = new FileStream(_fullMapPath, FileMode.Open, FileAccess.ReadWrite);
                        using var mapWriter = new BinaryWriter(fsMap, Encoding.UTF8, true);
                        using var fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.ReadWrite);
                        using var dbWriter = new BinaryWriter(fsDb, Encoding.UTF8, true);

                        fsMap.Position = fsMap.Length;
                        fsDb.Position = fsDb.Length;
                        do
                        {
                            this.InsertRecord(rec, mapWriter, dbWriter);

                        } while (this.TryGetInsertRecord(out rec));
                    }
                }
            }
        }

        private void InsertRecord(MemDbRecord<T> record, BinaryWriter map, BinaryWriter db)
        {
            long initialPosition = db.BaseStream.Position;
            int length;

            if (record.IsEncrypted)
            {
                using var msClear = new MemoryStream();
                using var localWriter = new BinaryWriter(msClear, Encoding.UTF8, true);
                using var msEncrypted = new MemoryStream();

                record.SerializeTo(localWriter);
                msClear.Position = 0;
                _cryptoProvider.Encrypt(msClear, msEncrypted, record.Id.ToString("0000000000"));
                msEncrypted.CopyTo(db.BaseStream);
            }
            else
            {
                record.SerializeTo(db);
            }

            length = (int)(db.BaseStream.Position - initialPosition);
            MemDbPointer pointer = new MemDbPointer(record.Id, false, record.IsEncrypted, initialPosition, length);
            _map.AddPointer(pointer);
            pointer.SerializeTo(map);
            record.MapIndex = (_map.Pointers.Count - 1);
        }
        #endregion

        #region flush stale state
        private void MarkRecordsStale()
        {
            lock (_mapSyncLock)
            {
                MemDbRecord<T> rec = null;
                if (!this.TryGetStaleRecord(out rec))
                    return;

                using var fsMap = new FileStream(_fullMapPath, FileMode.Open, FileAccess.ReadWrite);
                do
                {
                    MemDbPointer p = _map.Pointers[rec.MapIndex].MarkStale();
                    fsMap.Position = p.Position + sizeof(int);//position + size of Id gets us to the IsStale boolean
                    fsMap.WriteByte(1);//1 == true (IsStale = true)

                } while (this.TryGetStaleRecord(out rec));
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
        ~MappedFileStorageProvider()
        {
            if (!_isClosed)
            {
                this.Close(true); //emergency catch all to save flushed records if process dies...
            }
        }
        #endregion
    }
}
