using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

namespace HatTrick.MemDb
{
    internal sealed class MemDbMap
    {
        #region internals
        private List<MemDbPointer> _pointers;
        private object _syncLock;
        #endregion

        #region interface
        internal MemDbPointer this[Index index] => _pointers[index];

        internal int Count => _pointers.Count;
        
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
        internal MemDbMap()
        {
            _pointers = new List<MemDbPointer>();
            _syncLock = new();
        }

        internal MemDbMap(int capacity)
        {
            _pointers = new List<MemDbPointer>(capacity);
            _syncLock = new();
        }

        private MemDbMap(List<MemDbPointer> pointers)
        {
            _pointers = pointers;
            _syncLock = new();
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
                return _pointers.Where(p => p.IsStale == false).Sum(p => p.Length);
            }
        }
        #endregion

        #region get stale size
        private int GetStaleSize()
        {
            lock (_syncLock)
            {
                return _pointers.Where(p => p.IsStale == true).Sum(p => p.Length);
            }
        }
        #endregion

        #region get max fresh records size
        private int GetMaxFreshRecordsSize()
        {
            lock (_syncLock)
            {
                return _pointers.Where(p => p.IsStale == false).Max(p => p.Length);
            }
        }
        #endregion

        #region get max fresh records size
        private int GetMinFreshRecordSize()
        {
            lock (_syncLock)
            {
                return _pointers.Where(p => p.IsStale == false).Min(p => p.Length);
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

        #region get max stale records size
        private int GetMinStaleRecordSize()
        {
            lock (_syncLock)
            {
                return _pointers.Where(p => p.IsStale == true).Select(p => p.Length).DefaultIfEmpty().Min();
            }
        }
        #endregion

        #region create
        internal static MemDbMap Create(List<MemDbPointer> pointers)
        {
            MemDbMap map = new MemDbMap(pointers);
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

        #region serialization
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

                foreach (MemDbPointer p in _pointers)
                {
                    p.SerializeTo(writer);
                }
            }
        }

        internal void DeserializeFrom(BinaryReader reader)
        {
            lock (_syncLock)
            {
                int count = reader.ReadInt32();

                _pointers = new List<MemDbPointer>(count);

                for (int i = 0; i < count; i++)
                {
                    _pointers.Add(MemDbPointer.DeserializeFrom(reader));
                }
            }
        }
        #endregion
    }
}
