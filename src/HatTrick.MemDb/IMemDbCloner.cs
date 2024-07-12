using System;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    public interface IMemDbCloner<T> where T : class
    {
        public T DeepCopy(T value);

        public T[] DeepCopy(IList<T> values);
    }
}
