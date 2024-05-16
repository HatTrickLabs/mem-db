using System;
using System.Collections.Generic;

namespace HatTrick.MemDb
{
    public interface IMemDbCloner<T> where T : class, new()
    {
        public T DeepCopy(T value);

        public T[] DeepCopy(IList<T> values);
    }
}
