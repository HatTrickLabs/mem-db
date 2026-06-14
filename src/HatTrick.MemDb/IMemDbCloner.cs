// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.Collections.Generic;

namespace HatTrick.Data
{
    public interface IMemDbCloner<T> where T : class
    {
        public T DeepCopy(T value);

        public T[] DeepCopy(IList<T> values);
    }
}
