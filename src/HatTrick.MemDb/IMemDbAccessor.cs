using System;

namespace HatTrick.InMemDb
{
    public interface IMemDbAcceessor<T> where T : class
    {
        public int Count();

        public int Count(Func<T, bool> selector);

        public T Find(Func<T, bool> where);

        public T[] FindAll(Func<T, bool> where);

        public void Insert(T rec, bool encrypt = false);

        public void Insert(T rec, Action<uint> idCallback, bool encrypt = false);

        public int Update(Action<T> apply, Func<T, bool> where);

        public int Delete(Func<T, bool> where);

        public MemDbExpression<T> Query();

        public void Flush();
    }
}