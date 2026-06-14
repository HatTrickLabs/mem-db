// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;

namespace HatTrick.Data
{
    #region mem db record
    internal abstract class MemDbRecord
    {
        #region internals
        private MemDbPointer _pointer;
        private int _mapIndex;
        private int _cacheIndex;
        #endregion

        #region interface
        internal long Id => _pointer.Id;
        internal RecordState State => _pointer.State;
        internal long StateSetAt => _pointer.StateSetAt;
        internal long CreatedAt => _pointer.CreatedAt;
        internal bool IsEncrypted => _pointer.IsEncrypted;

        //local (not part of Pointer)
        internal int CacheIndex => _cacheIndex;
        internal int MapIndex => _mapIndex;
        #endregion

        #region constructors
        internal MemDbRecord(long id, long createdAt, bool isEncrypted) 
        {
            _pointer = new MemDbPointer(id, RecordState.Fresh, createdAt, createdAt, isEncrypted);
            _mapIndex = -1;
        }

        internal MemDbRecord(MemDbPointer pointer, int mapIndex)
        {
            _pointer = pointer;
            _mapIndex = mapIndex;
        }
        #endregion

        #region get pointer
        public MemDbPointer GetPointer()
        {
            return _pointer;
        }
        #endregion

        #region mark stale
        internal void MarkStale(long utcTimestamp)
        {
            _pointer.MarkStale(utcTimestamp);
        }
        #endregion

        #region mark deleted
        internal void MarkDeleted(long utcTimestamp)
        {
            _pointer.MarkDeleted(utcTimestamp);
        }
        #endregion

        #region set position
        internal void SetPosition(long position)
        {
            _pointer.SetPosition(position);
        }
        #endregion

        #region set length
        public void SetLength(int length)
        {
            _pointer.SetLength(length);
        }
        #endregion

        #region set cache index
        internal void SetCacheIndex(int cacheIndex)
        {
            _cacheIndex = cacheIndex;
        }
        #endregion

        #region set map index
        internal void SetMapIndex(int mapIndex)
        {
            _mapIndex = mapIndex;
        }
        #endregion
    }
    #endregion

    #region mem db record of T
    internal class MemDbRecord<T> : MemDbRecord where T: class
    {
        #region internals
        private T _value;
        #endregion

        #region interface
        public T Value
        {
            get { return _value; }
            internal set { _value = value; }
        }
        #endregion

        #region constructors
        internal MemDbRecord(long id, T value, long createdAt, bool isEncrypted) 
            : base(id, createdAt, isEncrypted)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _value = value;
        }

        internal MemDbRecord(MemDbPointer pointer, T value, int mapIndex) : base(pointer, mapIndex)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            _value = value;
        }
        #endregion

        #region to string
        public override string ToString()
        {
            return string.Concat(base.Id, "|", base.State, "|", _value.ToString());
        }
        #endregion
    }
    #endregion
}
