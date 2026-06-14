// SPDX-License-Identifier: Apache-2.0
// Copyright (c) HatTrick Labs, LLC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.Data
{
    public interface IIndexedQueryAccessor<T> where T : class
    {
        public IMemDbIndexExpressionRoot<T, YIndex> QueryViaIndex<YIndex>(string indexName) where YIndex : IComparable;

        //public IMemDbIndexedSetExpressionRoot<T, YIndex> QueryViaIndexedSet<YIndex>(string indexName) where YIndex : IComparable;
    }
}
