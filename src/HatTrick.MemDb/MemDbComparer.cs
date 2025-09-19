using System;
using System.Collections.Generic;

namespace HatTrick.Data
{
    public sealed class MemDbComparer<YIndex> : IMemDbComparer<YIndex>
    {
        #region internals
        private bool _isDefault;
        private IEqualityComparer<YIndex> _equality;
        private IComparer<YIndex> _relational;
        #endregion

        #region interface
        public bool IsDefault => _isDefault;
        #endregion

        #region ctors
        public MemDbComparer()
        {
            _equality = EqualityComparer<YIndex>.Default;
            _relational = Comparer<YIndex>.Default;
            _isDefault = true;
        }

        public MemDbComparer(IEqualityComparer<YIndex> equality, IComparer<YIndex> relational)
        {
            _equality = equality ?? throw new ArgumentNullException(nameof(equality));
            _relational = relational ?? throw new ArgumentNullException(nameof(relational));
            _isDefault = false;
        }
        #endregion

        #region compare
        public int Compare(YIndex x, YIndex y)
        {
            return _relational.Compare(x, y);
        }
        #endregion

        #region equals
        public bool Equals(YIndex x, YIndex y)
        {
            return _equality.Equals(x, y);
        }
        #endregion

        #region get hash code
        public int GetHashCode(YIndex obj)
        {
            return _equality.GetHashCode(obj);
        }
        #endregion
    }
}
