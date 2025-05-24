using System;

namespace HatTrick.InMemDb
{
    public interface IMemDbAcceessor<T> where T : class
    {
        public bool Exists(Func<T, bool> where);

        public bool Exists(long id);

        public int Count();

        public int Count(Func<T, bool> selector);

        public T Find(Func<T, bool> where);

        public T Find(long id);

        public T[] FindAll(Func<T, bool> where);

        public T[] FindAll(params long[] ids);

        public void Insert(T rec, bool encrypt = false);

        public void Insert(T rec, Action<long> idCallback, bool encrypt = false);

        public int Update(Action<T> apply, Func<T, bool> where);

        public bool Update(Action<T> apply, long id);

        public int Delete(Func<T, bool> where);

        public MemDbExpression<T> Query();

        public IMemDbIndexExpression<T, Y> QueryViaIndex<Y>(string indexName);
    }
}