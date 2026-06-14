// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;

namespace HatTrick.Data.TestHarness
{
    public class NotEqualException<T> : Exception
    {
        #region internals
        private T _left;
        private T _right;
        #endregion

        #region ctors
        public NotEqualException(T left, T right) 
            : base($"{EnsureType(typeof(T)).Name} {left} != {right}")
        {
            _left = left;
            _right = right;
        }
        #endregion

        #region ensure type
        private static Type EnsureType(Type t)
        {
            var underlying = Nullable.GetUnderlyingType(t);
            return underlying ?? t;
        }
        #endregion
    }
}
