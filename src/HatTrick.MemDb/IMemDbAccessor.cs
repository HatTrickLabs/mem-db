using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HatTrick.MemDb
{
    public interface IMemDbAcceessor<T> : IDisposable where T : MemDbRecord, new()
    {
        void PreAllocId(ref T rec);

        int Count();

        int Count(Func<T, bool> func);

        Y Max<Y>(Func<T, Y> func);

        Y Min<Y>(Func<T, Y> func);

        int Sum(Func<T, int> func);

        double Sum(Func<T, double> func);

        decimal Sum(Func<T, decimal> func);

        Y[] FindDistinct<Y>(Converter<T, Y> converter) where Y : IConvertible;

        T Find(Func<T, bool> func);

        T[] FindAll(Func<T, bool> p);

        void Insert(T rec);

        void InsertEncrypted(T rec);

        bool Update(T rec);

        bool Delete(T rec);

        bool Delete(int Id);

        MemDbExpression<T> Query();

        void Flush();

        Dictionary<string, string> Stats();

        void Defrag();

        void RegisterCryptoProvider(IMemDbCryptoProvider provider);
    }
}
