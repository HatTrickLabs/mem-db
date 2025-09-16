using System;
using System.Threading;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace HatTrick.Data
{
    internal sealed class MemDbMap
    {
        #region delegates
        internal delegate bool StaleRecordQueue(out MemDbRecord record);
        #endregion

        #region internals
        private const int _defaultInitialCapacity = 128;

        private string _path;
        private List<MemDbPointer> _pointers;
        private int _nextFlushIdx;
        private Lock _syncLock;

        private long _lastId;
        private Lock _idSyncLock;

        private IMemDbEncryptionInfo _encryptionInfo;
        #endregion

        #region interface
        internal MemDbPointer this[Index index] => _pointers[index];

        internal int Count => _pointers.Count;

        internal long LastId => _lastId;
        
        internal int FreshCount => this.GetCount(RecordState.Fresh);
        internal int StaleCount => this.GetCount(RecordState.Stale);
        internal int DeletedCount => this.GetCount(RecordState.Deleted);

        internal long TotalFreshSize => this.GetTotalSize(RecordState.Fresh);
        internal long TotalStaleSize => this.GetTotalSize(RecordState.Stale);
        internal long TotalDeletedSize => this.GetTotalSize(RecordState.Deleted);

        internal int MaxFreshRecordSize => this.GetMaxRecordSize(RecordState.Fresh);
        internal int MaxStaleRecordSize => this.GetMaxRecordSize(RecordState.Stale);
        internal int MaxDeletedRecordSize => this.GetMaxRecordSize(RecordState.Deleted);

        internal int MinFreshRecordSize => this.GetMinRecordSize(RecordState.Fresh);
        internal int MinStaleRecordSize => this.GetMinRecordSize(RecordState.Stale);
        internal int MinDeletedRecordSize => this.GetMinRecordSize(RecordState.Deleted);

        internal double AvgFreshRecordSize => this.GetAvgRecordSize(RecordState.Fresh);
        internal double AvgStaleRecordSize => this.GetAvgRecordSize(RecordState.Stale);
        internal double AvgDeletedRecordSize => this.GetAvgRecordSize(RecordState.Deleted);

        internal int EncryptedFreshCount => this.GetEncryptedCount(RecordState.Fresh);
        internal int EncryptedStaleCount => this.GetEncryptedCount(RecordState.Stale);
        internal int EncryptedDeletedCount => this.GetEncryptedCount(RecordState.Deleted);
        #endregion

        #region constructors
        internal MemDbMap(string path, bool initialize, IMemDbEncryptionInfo encryptionInfo)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _encryptionInfo = encryptionInfo;
            _lastId = 0;
            _nextFlushIdx = 0;
            _syncLock = new();
            _idSyncLock = new();

            if (initialize)
                this.Initialize();
            else
                _pointers = new List<MemDbPointer>(_defaultInitialCapacity);
        }
        #endregion

        #region binary length of
        public static long BinaryLengthOf(int pointerCount)
        {
            //sizeof(int) + sizeof(long) + (pointerCount * PointerByteSize)
            //the sizeof(int) is to account for the 32 bit int at the very beginning of the file (total pointer count)
            //the sizeof(long) is to account for the 64 bit int at the beginning of the file (Last Identity)
            long length =  sizeof(int) + sizeof(long) + (pointerCount * MemDbPointer.Size);
            return length;
        }
        #endregion

        #region initialize
        private void Initialize()
        {
            bool exists = File.Exists(_path);

            lock (_syncLock)
            {
                if (exists)
                    this.InitializeExisting();

                else
                    this.InitializeNew();
            }
        }

        private void InitializeNew()
        {
            _pointers = new List<MemDbPointer>(_defaultInitialCapacity);
            using var fs = new FileStream(_path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            this.SerializeTo(fs);
        }

        private void InitializeExisting()
        {
            using var fsMap = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.None);
            using var reader = new BinaryReader(fsMap, Encoding.UTF8, true);
            this.DeserializeFrom(reader);
        }
        #endregion

        #region override last id
        internal void OverrideLastId(long id)
        {
            bool outOfRange = false;
            lock (_idSyncLock)
            {
                if (id < _lastId)
                    outOfRange = true;
                else
                    _lastId = id;
            }

            if (outOfRange)
                throw new ArgumentException("Provided id is less than the last allocated id.");
        }
        #endregion

        #region get next id
        internal long GetNextId()
        {
            lock (_idSyncLock)
            {
                return ++_lastId;
            }
        }
        #endregion

        #region get count
        private int GetCount(RecordState state)
        {
            lock (_syncLock)
            {
                return _pointers.Count(p => p.State == state);
            }
        }

        private int GetEncryptedCount(RecordState state)
        {
            lock (_syncLock)
            {
                return _pointers.Count(p => p.State == state && p.IsEncrypted);
            }
        }
        #endregion

        #region get total size
        private long GetTotalSize(RecordState state)
        {
            Func<MemDbPointer, long> selector = (p) => p.IsEncrypted 
                ? (long)_encryptionInfo.GetEncryptedLength(p.Length) 
                : (long)p.Length;

            lock (_syncLock)
            {
                return _pointers.Where(p => p.State == state).Select(selector).DefaultIfEmpty().Sum();
            }
        }
        #endregion

        #region get max record size
        private int GetMaxRecordSize(RecordState state)
        {
            Func<MemDbPointer, int> selector = (p) => p.IsEncrypted
                ? _encryptionInfo.GetEncryptedLength(p.Length)
                : p.Length;

            lock (_syncLock)
            {
                return _pointers.Where(p => p.State == state).Select(selector).DefaultIfEmpty().Max();
            }
        }
        #endregion

        #region get min record size
        private int GetMinRecordSize(RecordState state)
        {
            Func<MemDbPointer, int> selector = (p) => p.IsEncrypted
                ? _encryptionInfo.GetEncryptedLength(p.Length)
                : p.Length;

            lock (_syncLock)
            {
                return _pointers.Where(p => p.State == state).Select(selector).DefaultIfEmpty().Min();
            }
        }
        #endregion

        #region get avg record size
        private double GetAvgRecordSize(RecordState state)
        {
            Func<MemDbPointer, int> selector = (p) => p.IsEncrypted
                ? _encryptionInfo.GetEncryptedLength(p.Length)
                : p.Length;

            lock (_syncLock)
            {
                return _pointers.Where(p => p.State == state).Select(selector).DefaultIfEmpty().Average();
            }
        }
        #endregion

        #region add
        internal int Add(MemDbPointer pointer)
        {
            lock (_syncLock)
            {
                int idx = _pointers.Count;
                _pointers.Add(pointer);
                return idx;
            }
        }
        #endregion

        #region update pointer states
        public void UpdatePointerStates(StaleRecordQueue tryGetStaleRecord)
        {
            lock (_syncLock)
            {
                MemDbRecord record = null;
                if (!tryGetStaleRecord(out record))
                    return;

                using (var fsMap = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    do
                    {
                        //sizeof(this.pointercount) + sizeof(this.lastid) + (idx * ptr.Pintersize) + sizeof(ptr.version) + sizeof(ptr.id)
                        fsMap.Position = sizeof(int) + sizeof(long) + (record.MapIndex * MemDbPointer.Size) + sizeof(byte) + sizeof(long);
                        fsMap.WriteByte((byte)record.State);
                        fsMap.Write(BitConverter.GetBytes(record.StateSetAt));

                        if (record.State == RecordState.Stale)
                            _pointers[record.MapIndex].MarkStale(record.StateSetAt);

                        else
                            _pointers[record.MapIndex].MarkDeleted(record.StateSetAt);

                    } while (tryGetStaleRecord(out record));
                }
            }
        }
        #endregion

        #region flush
        internal void Flush()
        {
            lock (_syncLock)
            {
                int count = _pointers.Count;
                if (_nextFlushIdx < count)
                {
                    using var fsMap = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    using var mapWriter = new BinaryWriter(fsMap, Encoding.UTF8, true);

                    //overwrite count and last id at the beginning
                    mapWriter.Write(count);
                    mapWriter.Write(_lastId);

                    //shift postion to the end...
                    fsMap.Position = fsMap.Length;

                    //start this for loop at the next index after prev flush
                    //...we are only flushing newly added records
                    for (int i = _nextFlushIdx; i < count; i++)
                    {
                        var p = _pointers[i];
                        p.SerializeTo(mapWriter);
                    }

                    _nextFlushIdx = count;
                }
            }
        }
        #endregion

        #region serialize to
        private void SerializeTo(Stream stream)
        {
            lock (_syncLock)
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    this.SerializeTo(writer);
                }
            }
        }

        private void SerializeTo(BinaryWriter writer)
        {
            lock (_syncLock)
            {
                writer.Write(_pointers.Count);
                writer.Write(_lastId);

                foreach (MemDbPointer p in _pointers)
                {
                    p.SerializeTo(writer);
                }
            }
        }
        #endregion

        #region deserializer from
        internal void DeserializeFrom(BinaryReader reader)
        {
            lock (_syncLock)
            {
                int count = reader.ReadInt32();
                _lastId = reader.ReadInt64();

                //we can only do the corruption check IF the underlying stream is seekable (archive compression streams are NOT seekable).
                if (reader.BaseStream.CanSeek)
                {
                    this.EnsureNoWriteCorruption(reader.BaseStream, count);
                }
                _pointers = new List<MemDbPointer>((int)(count * 1.1));

                for (int i = 0; i < count; i++)
                {
                    _pointers.Add(MemDbPointer.DeserializeFrom(reader));
                }

                _nextFlushIdx = count;
            }
        }
        #endregion

        #region ensure no write corruption
        private void EnsureNoWriteCorruption(Stream stream, int expected)
        {
            //check for corruption...
            long pointerLength = stream.Length - (sizeof(int) + sizeof(long));
            long persisted = pointerLength / MemDbPointer.Size;

            if (persisted < expected)//TODO: after the integrity checker / corrupt file fixer util is built, reference the util in this exception.
                throw new MemDbCorruptException($"Mismatch between persisted pointer count '{persisted}' vs expected pointer count '{expected}'.");
        }
        #endregion
    }
}