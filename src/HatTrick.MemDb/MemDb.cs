using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace HatTrick.MemDb
{
    public class MemDb<T> : IDisposable, IMemDbAcceessor<T> where T : class, new()
    {
        #region internals
        private MemDbCacheProvider<T> _cache;
        private MemDbStorageProvider<T> _storage;
        private bool _isClosed;
        #endregion

        #region interface
        #endregion

        #region constructors
        private MemDb(string path, string datasetName, ISerializationProvider<T> serializer)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("arg must have a value.", nameof(path));

            if (string.IsNullOrEmpty(datasetName))
                throw new ArgumentException("arg must have a value.", nameof(datasetName));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            _cache = new MemDbCacheProvider<T>();
        }
        #endregion

        #region open
        public static MemDb<T> Open(string path, string name)
        {
            return new MemDb<T>(path, name, null);//TODO: need some type of default serializer (JSON) after update to .net 8.0
        }

        public static MemDb<T> Open(string path, string name, ISerializationProvider<T> serializer)
        {
            return new MemDb<T>(path, name, serializer);
        }
        #endregion

        #region flush
        public void Flush()
        {
            if (!_isClosed)
            {
                _cache.Flush();
            }
        }
        #endregion

        #region count
        public int Count()
        {
            return _cache.Count();
        }

        public int Count(Func<T, bool> selector)
        {
            return _cache.Count(selector);
        }
        #endregion

        #region max
        public Y Max<Y>(Func<T, Y> selector)
        {
            return _cache.Max(selector);
        }
        #endregion

        #region min
        public Y Min<Y>(Func<T, Y> selector)
        {
            return _cache.Min(selector);
        }
        #endregion

        #region sum
        public int Sum(Func<T, int> selector)
        {
            return _cache.Sum(selector);
        }

        public double Sum(Func<T, double> selector)
        {
            return _cache.Sum(selector);
        }

        public decimal Sum(Func<T, decimal> selector)
        {
            return _cache.Sum(selector);
        }
        #endregion

        #region find distinct
        public Y[] FindDistinct<Y>(Converter<T, Y> converter) where Y : IConvertible
        {
            return _cache.FindDistinct<Y>(converter);
        }
        #endregion

        #region find
        public T Find(Func<T, bool> where)
        {
            return _cache.Find(where);
        }
        #endregion

        #region find all
        public T[] FindAll(Func<T, bool> where)
        {
            return _cache.FindAll(where);
        }
        #endregion

        #region insert
        public void Insert(T record, bool encrypt)
        {
            _cache.Insert(record, encrypt);
        }
        #endregion

        #region update
        public int Update(Action<T> apply, Func<T, bool> where)
        {
            return _cache.Update(apply, where);
        }
        #endregion

        #region delete
        public int Delete(Func<T, bool> where)
        {
            return _cache.Delete(where);
        }
        #endregion

        #region query
        public MemDbExpression<T> Query()
        {
            return _cache.Query();
        }
        #endregion

        #region execute query
        //private T[] ExecuteQuery(MemDbExpression<T> expression, bool deepCopy = true)
        //{
        //    return _cache.ExecuteQuery(expression, deepCopy);
        //}
        #endregion

        #region dispose
        public void Dispose()
        {
            if (!_isClosed)
            {
                this.Close();
            }
        }
        #endregion

        #region finalizer
        ~MemDb()
        {
            if (!_isClosed)
            {
                this.Close(true); //emergency catch all to save un-synced records if process dies...
            }
        }
        #endregion

        #region close
        private void Close(bool isFinalizer = false)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}