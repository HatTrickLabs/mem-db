using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HatTrick.MemDb
{
    internal abstract class MemDbRecord
    {
        #region read only
        internal static readonly int Size = sizeof(int) + sizeof(bool) + sizeof(bool) + sizeof(int) + sizeof(int);
        #endregion

        #region internals
        private int _id;
        private bool _isStale;
        private bool _isEncrypted;
        private int _cacheIndex;
        private int _mapIndex;
        #endregion

        #region interface
        internal int Id => _id;
        internal bool IsStale => _isStale;
        internal bool IsEncrypted => _isEncrypted;
        internal int Index => _cacheIndex;
        internal int MapIndex => _mapIndex;
        #endregion

        #region constructors
        internal MemDbRecord(bool isEncrypted)
        {
            _isEncrypted = isEncrypted;
        }

        internal MemDbRecord(int id, bool isStale, bool isEncrypted, int cacheIndex, int mapIndex)
        {
            _id = id;
            _isStale = isStale;
            _isEncrypted = isEncrypted;
            _cacheIndex = cacheIndex;
            _mapIndex = mapIndex;
        }
        #endregion

        #region set id
        internal void SetId(int id)
        {
            _id = id;
        }
        #endregion

        #region mark stale
        internal void MarkStale()
        {
            _isStale = true;
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

    internal class MemDbRecord<T> : MemDbRecord where T: class, new()
    {
        #region internals
        private static IMemDbSerializer<T> _serializer;

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
        internal MemDbRecord(T value, bool isEncrypted) : base(isEncrypted)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _value = value;
        }

        internal MemDbRecord(T value, int id, bool isStale, bool isEncrypted, int cacheIndex, int mapIndex) 
            : base(id, isStale, isEncrypted, cacheIndex, mapIndex)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            _value = value;
        }
        #endregion
    }
}
