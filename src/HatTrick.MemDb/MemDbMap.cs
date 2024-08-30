using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

namespace HatTrick.InMemDb
{
    internal sealed class MemDbMap
    {
        #region delegates
        internal delegate bool StaleRecordQueue(out MemDbRecord record);
        #endregion

        #region internals
        private string _path;
        private List<MemDbPointer> _pointers;
        private object _syncLock;

        private uint _lastId;
        private object _idSyncLock;
        #endregion

        #region interface
        internal MemDbPointer this[Index index] => _pointers[index];

        internal int Count => _pointers.Count;

        internal uint LastId => _lastId;
        
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
        #endregion

        #region constructors
        internal MemDbMap(string path) : this(path, 0, null)
        { }

        internal MemDbMap(string path, uint lastId, List<MemDbPointer> pointers)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _lastId = lastId;
            _pointers = pointers ?? new List<MemDbPointer>();//TODO: this alloc necessary ???
            _syncLock = new();
            _idSyncLock = new();
        }
        #endregion

        #region initialize
        internal void InitializeNew()
        {
            lock (_syncLock)
            {
                using var fs = new FileStream(_path, FileMode.CreateNew);
                this.SerializeTo(fs);
            }
        }

        internal void InitializeExisting()
        {
            lock (_syncLock)
            {
                using var fsMap = new FileStream(_path, FileMode.Open, FileAccess.Read); ;
                using var reader = new BinaryReader(fsMap, Encoding.UTF8, true);
                this.DeserializeFrom(reader);
            }
        }
        #endregion

        #region get next id
        internal uint GetNextId()
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
        #endregion

        #region get total size
        private long GetTotalSize(RecordState state)
        {
            Func<MemDbPointer, long> selector = (p) => p.IsEncrypted 
                ? (long)MemDbAESEncryptor.CalculateCryptoByteLength(p.Length) 
                : (long)p.Length;

            lock (_syncLock)
            {
                return _pointers.Where(p => p.State == state).Select(selector).DefaultIfEmpty().Sum();
            }
        }
        #endregion

        #region get max records size
        private int GetMaxRecordSize(RecordState state)
        {
            Func<MemDbPointer, int> selector = (p) => p.IsEncrypted
                ? MemDbAESEncryptor.CalculateCryptoByteLength(p.Length)
                : p.Length;

            lock (_syncLock)
            {
                return _pointers.Where(p => p.State == state).Select(selector).DefaultIfEmpty().Max();
            }
        }
        #endregion

        #region get min records size
        private int GetMinRecordSize(RecordState state)
        {
            Func<MemDbPointer, int> selector = (p) => p.IsEncrypted
                ? MemDbAESEncryptor.CalculateCryptoByteLength(p.Length)
                : p.Length;

            lock (_syncLock)
            {
                return _pointers.Where(p => p.State == state).Select(selector).DefaultIfEmpty().Min();
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

        #region update pointer state
        public void UpdatePointerState(StaleRecordQueue tryGetStaleRecord)
        {
            long binaryUtcTimestamp = DateTime.UtcNow.ToBinary();
            lock (_syncLock)
            {
                MemDbRecord record = null;
                if (!tryGetStaleRecord(out record))
                    return;

                using var fsMap = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite);
                do
                {
                    _ = record.State == RecordState.Stale 
                        ? _pointers[record.MapIndex].MarkStale(binaryUtcTimestamp)
                        : _pointers[record.MapIndex].MarkDeleted(binaryUtcTimestamp);
                    //sizeof(pointercount) + sizeof(lastId) + (idx * size) + sizeof(id)
                    //TODO: shift this calc inside MemDbMap somehow.
                    fsMap.Position = sizeof(int) + sizeof(uint) + (record.MapIndex * MemDbPointer.Size) + sizeof(int);
                    fsMap.WriteByte((byte)record.State);

                } while (tryGetStaleRecord(out record));
            }
        }
        #endregion

        #region flush
        internal void Flush()
        {
            lock (_syncLock)
            {
                using var fsMap = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite);
                using var mapWriter = new BinaryWriter(fsMap, Encoding.UTF8, true);

                //overwrite count and last id at the beginning
                mapWriter.Write(_pointers.Count);
                mapWriter.Write(_lastId);

                fsMap.Position = fsMap.Length;
                //TODO: refactor to start this for loop at the next index after
                //prev flush as we are only flushing additions
                for (int i = 0; i < _pointers.Count; i++)
                {
                    var p = _pointers[i];
                    if (!p.Flushed)
                        p.SerializeTo(mapWriter);
                }
            }
        }
        #endregion

        #region serialize to
        internal void SerializeTo(Stream stream)
        {
            lock (_syncLock)
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    this.SerializeTo(writer);
                }
            }
        }

        internal void SerializeTo(BinaryWriter writer)
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
                _lastId = reader.ReadUInt32();

                _pointers = new List<MemDbPointer>((int)(count * 1.1));

                for (int i = 0; i < count; i++)
                {
                    _pointers.Add(MemDbPointer.DeserializeFrom(reader));
                }
            }
        }
        #endregion
    }
}
