using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

namespace HatTrick.MemDb
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
        
        internal int FreshCount => this.GetFreshCount();
        internal int StaleCount => this.GetStaleCount();

        internal long FreshSize => this.GetFreshSize();
        internal long StaleSize => this.GetStaleSize();

        internal int MaxFreshRecordSize => this.GetMaxFreshRecordsSize();
        internal int MinFreshRecordSize => this.GetMinFreshRecordSize();

        internal int MaxStaleRecordSize => this.GetMaxStaleRecordSize();
        internal int MinStaleRecordSize => this.GetMinStaleRecordSize();
        #endregion

        #region constructors
        internal MemDbMap(string path) : this(path, 0, null)
        { }

        private MemDbMap(string path, uint lastId, List<MemDbPointer> pointers)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _lastId = lastId;
            _pointers = pointers ?? new List<MemDbPointer>();//TODO: this alloc necessary ???
            _syncLock = new();
            _idSyncLock = new();
        }
        #endregion

        #region initialize existing
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

        #region get fresh count
        private int GetFreshCount()
        {
            lock (_syncLock)
            {
                return _pointers.Count(p => p.IsStale == false);
            }
        }
        #endregion

        #region get stale count
        private int GetStaleCount()
        {
            lock (_syncLock)
            {
                return _pointers.Count(p => p.IsStale);
            }
        }
        #endregion

        #region get fresh size
        private int GetFreshSize()
        {
            lock (_syncLock)
            {
                return _pointers.Where(p => p.IsStale == false).Select(p => p.Length).DefaultIfEmpty().Sum();
            }
        }
        #endregion

        #region get stale size
        private int GetStaleSize()
        {
            lock (_syncLock)
            {
                return _pointers.Where(p => p.IsStale == true).Select(p => p.Length).DefaultIfEmpty().Sum();
            }
        }
        #endregion

        #region get max fresh records size
        private int GetMaxFreshRecordsSize()
        {
            lock (_syncLock)
            {
                return _pointers.Where(p => p.IsStale == false).Select(p => p.Length).DefaultIfEmpty().Max();
            }
        }
        #endregion

        #region get max fresh records size
        private int GetMinFreshRecordSize()
        {
            lock (_syncLock)
            {
                return _pointers.Where(p => p.IsStale == false).Select(p => p.Length).DefaultIfEmpty().Min();
            }
        }
        #endregion

        #region get max stale records size
        private int GetMaxStaleRecordSize()
        {
            lock (_syncLock)
            {
                return _pointers.Where(p => p.IsStale == true).Select(p => p.Length).DefaultIfEmpty().Max();
            }
        }
        #endregion

        #region get min stale records size
        private int GetMinStaleRecordSize()
        {
            lock (_syncLock)
            {
                return _pointers.Where(p => p.IsStale == true).Select(p => p.Length).DefaultIfEmpty().Min();
            }
        }
        #endregion

        #region create
        internal static MemDbMap Create(string path, uint lastId, List<MemDbPointer> pointers)
        {
            MemDbMap map = new MemDbMap(path, lastId, pointers);
            return map;
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

        #region mark stale pointers
        public void MarkStalePointers(StaleRecordQueue tryGetStaleRecord)
        {
            lock (_syncLock)
            {
                MemDbRecord record = null;
                if (!tryGetStaleRecord(out record))
                    return;

                using var fsMap = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite);
                do
                {
                    _ = _pointers[record.MapIndex].MarkStale();
                    //sizeof(pointercount) + sizeof(lastId) + (idx * size) + sizeof(id)
                    fsMap.Position = sizeof(int) + sizeof(uint) + (record.MapIndex * MemDbPointer.Size) + sizeof(int);
                    fsMap.WriteByte(1);//1 == true (IsStale = true)

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
                fsMap.Position = fsMap.Length;
                for (int i = 0; i < _pointers.Count; i++)//TODO: refact to start this for loop at the next index after prev flush
                {
                    var p = _pointers[i];
                    if (!p.Flushed)
                        p.SerializeTo(mapWriter);
                }
                fsMap.Position = 0;
                mapWriter.Write(_pointers.Count);
                //pull this id from the known serialized pointer...NOT _lastId
                mapWriter.Write(_pointers[^1].Id);
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
