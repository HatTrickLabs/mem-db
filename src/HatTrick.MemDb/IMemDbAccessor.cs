using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HatTrick.MemDb
{
    public interface IMemDbAcceessor<T> where T : class, new()
    {
        public int Count();

        public int Count(Func<T, bool> selector);

        public Y Max<Y>(Func<T, Y> selector);

        public Y Min<Y>(Func<T, Y> selector);

        public int Sum(Func<T, int> selector);

        public long Sum(Func<T, long> selector);

        public float Sum(Func<T, float> selector);

        public double Sum(Func<T, double> selector);

        public decimal Sum(Func<T, decimal> selector);

        public double Avg(Func<T, int> selector);

        public double Avg(Func<T, long> selector);

        public float Avg(Func<T, float> selector);

        public double Avg(Func<T, double> selector);

        public decimal Avg(Func<T, decimal> selector);

        public Y[] FindDistinct<Y>(Converter<T, Y> converter) where Y : IConvertible;

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