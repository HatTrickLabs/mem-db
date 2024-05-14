using System;
using System.Linq;
using System.Collections.Generic;

namespace HatTrick.MemDb
{
    internal class MemDbCacheProvider<T> : IMemDbAcceessor<T> where T : class, new()
    {
        #region internals
        private List<MemDbRecord<T>> _records;
        private object _recLock;
        #endregion

        #region constructors
        public MemDbCacheProvider()
        {
        }
        #endregion


        #region count
        #region count
        public int Count()
        {
            lock (_recLock)
            {
                return _records.Count(r => r.IsStale == false);
            }
        }

        public int Count(Func<T, bool> selector)
        {
            lock (_recLock)
            {
                return _records.Count(r => r.IsStale == false && selector(r.Value));
            }
        }
        #endregion

        #region max
        public Y Max<Y>(Func<T, Y> selector)
        {
            Y max = default(Y);
            lock (_recLock)
            {
                if (_records.Count > 0)
                {
                    max = _records.Where(r => r.IsStale == false).Max<MemDbRecord<T>, Y>((r) => selector(r.Value));
                }
            }
            return max;
        }
        #endregion

        #region min
        public Y Min<Y>(Func<T, Y> selector)
        {
            Y min = default(Y);
            lock (_recLock)
            {
                if (_records.Count > 0)
                {
                    min = _records.Where(r => r.IsStale == false).Min<MemDbRecord<T>, Y>((r) => selector(r.Value));
                }
            }
            return min;
        }
        #endregion

        #region sum
        public int Sum(Func<T, int> selector)
        {
            lock (_recLock)
            {
                return _records.Where(r => r.IsStale == false).Sum((r) => selector(r.Value));
            }
        }

        public double Sum(Func<T, double> selector)
        {
            lock (_recLock)
            {
                return _records.Where(r => r.IsStale == false).Sum((r) => selector(r.Value));
            }
        }

        public decimal Sum(Func<T, decimal> selector)
        {
            lock (_recLock)
            {
                return _records.Where(r => r.IsStale == false).Sum((r) => selector(r.Value));
            }
        }
        #endregion

        #region find distinct
        public Y[] FindDistinct<Y>(Converter<T, Y> converter) where Y : IConvertible
        {
            lock (_recLock)
            {
                return _records.Where(r => r.IsStale == false).Select((r) => converter(r.Value)).Distinct().ToArray();
            }
        }
        #endregion

        #region find
        public T Find(Func<T, bool> where)
        {
            MemDbRecord<T> rec = null;
            lock (_recLock)
            {
                for (int i = 0; i < _records.Count; i++)
                {
                    if (_records[i].IsStale == false && where(_records[i].Value))
                    {
                        rec = _records[i];
                        break;
                    }
                }
            }
            return rec is null ? null : MemDbRecord<T>.DeepCopyOf(rec.Value);
        }
        #endregion

        #region find all
        public T[] FindAll(Func<T, bool> where)
        {
            T[] matches;
            lock (_recLock)
            {
                matches = _records.Where(r => r.IsStale == false && where(r.Value)).Select(r => r.Value).ToArray();
            }

            T[] set = MemDbRecord<T>.DeepCopyOf(matches);

            return set;
        }
        #endregion

        #region query
        public MemDbExpression<T> Query()
        {
            throw new NotImplementedException();
        }
        #endregion

        #region insert
        public void Insert(T record)
        {
            this.Insert(record, false);
        }

        public void InsertEncrypted(T record)
        {
            this.Insert(record, true);
        }

        private void Insert(T record, bool encrypt)
        {
            if (encrypt && !this.IsCryptoReady)
                throw new NotCryptoReadyException();

            MemDbRecord<T> rec = new MemDbRecord<T>(MemDbRecord<T>.DeepCopyOf(record));
            rec.Id = this.GetNextId();

            lock (_recLock)
            {
                _records.Add(rec);
                rec.Index = (_records.Count - 1);
            }

            lock (_queueLock)
            {
                _insertActionQueue.Enqueue(new InsertAction(rec.Index));
            }
        }
        #endregion

        #region update
        public int Update(Action<T> apply, Func<T, bool> where)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region delete
        public int Delete(Func<T, bool> where)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
