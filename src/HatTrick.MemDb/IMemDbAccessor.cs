using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HatTrick.MemDb
{
    public interface IMemDbAcceessor<T> : IDisposable where T :/* MemDbRecord, */new()
    {
        int Count();

        int Count(Func<T, bool> selector);

        Y Max<Y>(Func<T, Y> selector);

        Y Min<Y>(Func<T, Y> selector);

        int Sum(Func<T, int> selector);

        double Sum(Func<T, double> selector);

        decimal Sum(Func<T, decimal> selector);

        Y[] FindDistinct<Y>(Converter<T, Y> converter) where Y : IConvertible;

        T Find(Func<T, bool> where);

        T[] FindAll(Func<T, bool> where);

        void Insert(T rec);

        void InsertEncrypted(T rec);

        int Update(Action<T> apply, Func<T, bool> where);

        int Delete(Func<T, bool> where);

        MemDbExpression<T> Query();

        void Flush();

        Dictionary<string, decimal> Stats();

        void Defrag();

        void RegisterCryptoProvider(IMemDbCryptoProvider provider);
    }
}
