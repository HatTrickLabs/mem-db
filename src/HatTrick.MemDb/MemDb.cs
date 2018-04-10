using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace HatTrick.MemDb
{
    public class MemDb<T> : IDisposable, IMemDbAcceessor<T> where T : MemDbRecord, IMemDbSerializable, new()
    {
        #region internals
        private string _path;
        private string _name;

        private string _fullMapPath;
        private string _fullDbPath;
        private object _fileLock;

        private MemDbMap _map;
        private List<T> _records;
        private object _recLock;

        private int _unHydratedCryptoRecCount;

        private int _lastId;
        private object _lastIdLock;

        private Queue<InsertAction> _insertActionQueue;
        private Queue<MarkStaleAction> _markStaleActionQueue;

        private object _queueLock;
        private Timer _fileSyncTimer;

        private Guid _syncRef;

        private IMemDbCryptoProvider _cryptoProvider;

        private bool _isClosed;
        #endregion

        #region interface
        public bool IsCryptoReady
        { get { return _cryptoProvider != null; } }
        #endregion

        #region constructors
        public MemDb(string path, string datasetName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("arg must have a value.", nameof(path));
            }

            _recLock = new object();
            _lastIdLock = new object();
            _fileLock = new object();
            _queueLock = new object();

            _insertActionQueue = new Queue<InsertAction>(1024);
            _markStaleActionQueue = new Queue<MarkStaleAction>(1024);

            _path = path;
            _name = datasetName;
            _fullDbPath = Path.Combine(path, $"htl.{datasetName}.db");
            _fullMapPath = Path.Combine(path, $"htl.{datasetName}.map");

            _map = new MemDbMap();
            _records = new List<T>(2048);

            this.Init();
            _fileSyncTimer = new Timer(new TimerCallback(this.FileSync), null, (1000 * 5), Timeout.Infinite); //5 seconds...
        }
        #endregion

        #region open
        public static MemDb<T> Open(string path, string name)
        {
            return new MemDb<T>(path, name);
        }
        #endregion

        #region register crypto provider
        public void RegisterCryptoProvider(IMemDbCryptoProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            lock (_fileLock)
            {
                _cryptoProvider = provider;
                if (_unHydratedCryptoRecCount > 0)
                {
                    this.HydrateCryptoRecords();
                    _unHydratedCryptoRecCount = 0;
                }
            }
        }
        #endregion

        #region init
        private void Init()
        {
            this.EnsureFiles(out bool filesCreated);

            _syncRef = Guid.NewGuid(); //changes on defrag...

            if (!filesCreated)
            {
                lock (_fileLock)
                {
                    lock (_recLock)
                    {
                        using (FileStream fsMap = new FileStream(_fullMapPath, FileMode.Open, FileAccess.Read))
                        {
                            _map.DeserializeFrom(fsMap);
                            if (_map.PointerCount > 2048)
                            {
                                _records = new List<T>((int)(_map.PointerCount * 1.5));
                            }
                        }

                        using (FileStream fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read))
                        {
                            T rec;
                            MemDbPointer pointer;
                            int staleCount = 0;
                            int index = -1;
                            for (int i = 0; i < _map.PointerCount; i++)
                            {
                                pointer = _map.Pointers[i];
                                rec = new T();

                                if (pointer.IsStale)
                                {
                                    fsDb.Position += pointer.RecordLength;
                                    staleCount += 1;
                                }
                                else if (pointer.IsEncrypted && !this.IsCryptoReady)
                                {
                                    fsDb.Position += pointer.RecordLength;
                                    _unHydratedCryptoRecCount += 1;
                                }
                                else
                                {
                                   if (pointer.IsEncrypted)
                                    {
                                        using (MemoryStream msEncrypted = new MemoryStream())
                                        {
                                            byte[] buffer = new byte[pointer.RecordLength];
                                            fsDb.Read(buffer, 0, pointer.RecordLength);
                                            msEncrypted.Write(buffer, 0, pointer.RecordLength);

                                            using (MemoryStream msClear = new MemoryStream())
                                            {
                                                _cryptoProvider.Decrypt(msEncrypted, msClear, pointer.Id.ToString("0000000000"));
                                                msClear.Position = 0;
                                                rec.DeserializeFrom(msClear, (int)msClear.Length);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        rec.DeserializeFrom(fsDb, pointer.RecordLength);
                                    }

                                    rec.MapIndex = i;
                                    rec.Index = ++index;
                                    _records.Add(rec);
                                }
                            }
                        }
                        this.InitLastId();
                    }
                }
            }
        }
        #endregion

        #region hydrate crypto records
        private void HydrateCryptoRecords()
        {
            lock (_fileLock) //re-enterable lock via same thread...
            {
                lock (_recLock)
                {
                    using (FileStream fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read))
                    {
                        T rec;
                        MemDbPointer pointer;
                        int index = -1;
                        for (int i = 0; i < _map.PointerCount; i++)
                        {
                            pointer = _map.Pointers[i];
                           
                            if (pointer.IsEncrypted && !pointer.IsStale)
                            {
                                rec = new T();
                                using (MemoryStream msEncrypted = new MemoryStream())
                                {
                                    byte[] buffer = new byte[pointer.RecordLength];
                                    fsDb.Read(buffer, 0, pointer.RecordLength);
                                    msEncrypted.Write(buffer, 0, pointer.RecordLength);

                                    using (MemoryStream msClear = new MemoryStream())
                                    {
                                        msEncrypted.Position = 0;
                                        _cryptoProvider.Decrypt(msEncrypted, msClear, pointer.Id.ToString("0000000000"));
                                        msClear.Position = 0;
                                        rec.DeserializeFrom(msClear, (int)msClear.Length);
                                    }
                                }
                                rec.MapIndex = i;
                                rec.Index = ++index;
                                _records.Add(rec);
                            }
                            else
                            {
                                fsDb.Position += pointer.RecordLength;
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region ensure files
        private void EnsureFiles(out bool filesCreated)
        {
            filesCreated = false;
            lock (_fileLock)
            {
                if (!File.Exists(_fullDbPath))
                {
                    string dir = _path;
                    if (!Directory.Exists(_path))
                    {
                        Directory.CreateDirectory(_path);
                    }

                    using (FileStream fs = File.Create(_fullMapPath))
                    {
                        _map.SerializeTo(fs);
                    }

                    using (FileStream fs = File.Create(_fullDbPath)) { }

                    filesCreated = true;
                }

            }
        }
        #endregion

        #region flush
        public void Flush()
        {
            if (!_isClosed)
            {
                _fileSyncTimer.Change(Timeout.Infinite, Timeout.Infinite);
                this.FileSync(null);
            }
        }
        #endregion

        #region file sync
        private void FileSync(object state)
        {
            if (state == null && _isClosed) { return; }

            List<InsertAction> insertActions = null;
            List<MarkStaleAction> markStaleActions = null;

            //TODO: JRod, move queue pop/pull from here to 'InjectRecords', 'MarkRecordsStale'...pop 1 at at time 
            lock (_queueLock)
            {
                if (_insertActionQueue.Count > 0)
                {
                    insertActions = _insertActionQueue.ToList();
                    _insertActionQueue.Clear();
                }

                if (_markStaleActionQueue.Count > 0)
                {
                    markStaleActions = _markStaleActionQueue.ToList();
                    _markStaleActionQueue.Clear();
                }
            }

            if (insertActions != null)
            {
                this.InjectRecords(insertActions);
            }

            if (markStaleActions != null)
            {
                this.MarkRecordsStale(markStaleActions);
            }

            if (!_isClosed)
            {
                _fileSyncTimer.Change((1000 * 5), Timeout.Infinite);//5 seconds...
            }
        }
        #endregion

        #region inject records
        private void InjectRecords(List<InsertAction> inserts)
        {
            lock (_fileLock)
            {
                using (FileStream fsMap = new FileStream(_fullMapPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    fsMap.Position = fsMap.Length;

                    using (FileStream fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.ReadWrite))
                    {
                        fsDb.Position = fsDb.Length;
                        int lastPosition = (int)fsDb.Position;
                        int length;
                        foreach (InsertAction insert in inserts)
                        {
                            T rec = default(T);
                            lock (_recLock)
                            {
                                rec = _records[insert.RecordIndex];
                            }

                            if (rec.IsEncrypted)
                            {
                                using (MemoryStream msClear = new MemoryStream())
                                {
                                    rec.SerializeTo(msClear);
                                    using (MemoryStream msEncrypted = new MemoryStream())
                                    {
                                        msClear.Position = 0;
                                        _cryptoProvider.Encrypt(msClear, msEncrypted, rec.Id.ToString("0000000000"));

                                        fsDb.Write(msEncrypted.ToArray(), 0, (int)msEncrypted.Length);
                                    }
                                }
                            }
                            else
                            {
                                rec.SerializeTo(fsDb);
                            }

                            length = (int)fsDb.Position - lastPosition;
                            MemDbPointer pointer = new MemDbPointer(rec.Id, false, rec.IsEncrypted, lastPosition, length);
                            _map.AddPointer(pointer);
                            pointer.SerializeTo(fsMap);
                            rec.MapIndex = (_map.PointerCount - 1);
                            lastPosition += length;
                        }
                    }

                    fsMap.Position = 0;
                    fsMap.Write(BitConverter.GetBytes(_map.PointerCount), 0, 4); //overwrite the map global count ...
                }
            }
        }
        #endregion

        #region mark record stale
        private void MarkRecordsStale(List<MarkStaleAction> actions)
        {
            lock (_fileLock)
            {
                using (FileStream fsMap = new FileStream(_fullMapPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    T rec = null;
                    foreach (MarkStaleAction action in actions)
                    {
                        MemDbPointer oldPointer;
                        lock (_recLock)
                        {
                            rec = _records[action.RecordIndex];
                            oldPointer = _map.Pointers[rec.MapIndex];
                            _map.Pointers[rec.MapIndex] = new MemDbPointer(oldPointer.Id, true, oldPointer.IsEncrypted, oldPointer.RecordStartPosition, oldPointer.RecordLength);
                        }

                        fsMap.Position = 4 + (rec.MapIndex * MemDbPointer.Length) + 4; //count + (mapindex * maplen) + id
                        fsMap.WriteByte((byte)1);//1 == true (is stale is true)
                    }
                }
            }
        }
        #endregion

        #region init last id
        private void InitLastId()
        {
            lock (_lastIdLock)
            {
                lock (_recLock)
                {
                    int lastIssued = _lastId;

                    //if not crypto ready, encrypted rec my have hightest Id because rec couldnt be hydrated yet...
                    int lastMapId = (_map.PointerCount> 0) ? _map.Pointers.Max(p => p.Id) : 0;

                    //records don't get added to the map.pointer list until flushed to disc.
                    int lastRecId = (_records.Count > 0) ? _records.Max(r => r.Id) : 0;        

                    //max of the 3 is guaranteed to be the last issued...
                    //_lastId = lastIssued < lastMapId 
                    //    ? (lastMapId < lastRecId ? lastRecId : lastMapId) 
                    //    : (lastIssued < lastRecId ? lastRecId : lastIssued);
                    _lastId = Math.Max(lastIssued, Math.Max(lastMapId, lastRecId));
                }
            }
        }
        #endregion

        #region get next id
        private int GetNextId()
        {
            lock (_lastIdLock)
            {
                return (++_lastId);
            }
        }
        #endregion

        #region pre alloc id
        public void PreAllocId(ref T rec)
        {
            rec.Id = this.GetNextId();
        }
        #endregion

        #region count
        public int Count()
        {
            lock (_recLock)
            {
                return _records.Count(r => r.IsStale == false);
            }
        }

        public int Count(Func<T, bool> func)
        {
            Func<T, bool> isStale = (r) => r.IsStale == false;

            lock (_recLock)
            {
                return _records.Count(r => isStale(r) && func(r));
            }
        }
        #endregion

        #region max
        public Y Max<Y>(Func<T, Y> func)
        {
            Y max = default(Y);
            lock (_recLock)
            {
                if (_records.Count > 0)
                {
                    max = _records.FindAll(r => r.IsStale == false).Max<T, Y>(func);
                }
            }
            return max;
        }
        #endregion

        #region min
        public Y Min<Y>(Func<T, Y> func)
        {
            Y min = default(Y);
            lock (_recLock)
            {
                if (_records.Count > 0)
                {
                    min = _records.FindAll(r => r.IsStale == false).Min<T, Y>(func);
                }
            }
            return min;
        }
        #endregion

        #region sum
        public int Sum(Func<T, int> func)
        {
            lock (_recLock)
            {
                return _records.FindAll(r => r.IsStale == false).Sum(func);
            }
        }

        public double Sum(Func<T, double> func)
        {
            lock (_recLock)
            {
                return _records.FindAll(r => r.IsStale == false).Sum(func);
            }
        }

        public decimal Sum(Func<T, decimal> func)
        {
            lock (_recLock)
            {
                return _records.FindAll(r => r.IsStale == false).Sum(func);
            }
        }
        #endregion

        #region find distinct
        public Y[] FindDistinct<Y>(Converter<T, Y> converter) where Y : IConvertible
        {
            lock (_recLock)
            {
                return _records.FindAll(r => r.IsStale == false).ConvertAll<Y>(converter).Distinct().ToArray();
            }
        }
        #endregion

        #region find
        public T Find(Func<T, bool> func)
        {
            T rec = null;
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].IsStale == false && func(_records[i]))
                {
                    rec = _records[i];
                    break;
                }
            }
            T outRec = null;
            if (rec != null)
            {
                outRec = MemDbRecord.DeepCopy<T>(rec); //overwrite with deep copy...
                outRec.SyncRef = _syncRef;
            }
            return outRec;
        }
        #endregion

        #region find all
        public T[] FindAll(Func<T, bool> func)
        {
            List<T> matches = _records.FindAll(r => r.IsStale == false && func(r));
            if (matches.Count > 0)
            {
                T[] copies = new T[matches.Count];
                for (int i = 0; i < matches.Count; i++)
                {
                    copies[i] = MemDbRecord.DeepCopy<T>(matches[i]);
                    copies[i].SyncRef = _syncRef;
                }
                return copies;
            }
            else
            {
                return new T[0];
            }
        }
        #endregion

        #region insert
        public void Insert(T rec)
        {
            if (rec.IsEncrypted && !this.IsCryptoReady)
                throw new NotCryptoReadyException();

            if (rec.Id == default(int)) //may have been pre alloc
            {
                rec.Id = this.GetNextId();
            }

            T recClone = MemDbRecord.DeepCopy<T>(rec); ;
            lock (_recLock)
            {
                _records.Add(recClone);
                recClone.Index = (_records.Count - 1);
                rec.Index = recClone.Index;
                rec.SyncRef = _syncRef;
            }

            lock (_queueLock)
            {
                _insertActionQueue.Enqueue(new InsertAction(recClone.Index));
            }
        }
        #endregion

        #region insert encrypted
        public void InsertEncrypted(T rec)
        {
            rec.IsEncrypted = true;
            this.Insert(rec);
        }
        #endregion

        #region update
        public bool Update(T rec)
        {
            bool updated = false;
            if (rec != null)
            {
                if (rec.IsEncrypted && !this.IsCryptoReady)
                    throw new NotCryptoReadyException();

                if (rec.SyncRef != _syncRef || rec.IsStale)
                    throw new StaleRecordException();

                T newRec = MemDbRecord.DeepCopy<T>(rec);

                rec.IsStale = true; //this is the external deep copy of the rec...

                lock (_recLock)
                {
                    if (!_records[rec.Index].IsStale)
                    {
                        _records[rec.Index].IsStale = true;
                        _records.Add(newRec);
                        newRec.Index = (_records.Count - 1);
                        updated = true;
                    }
                }

                if (updated)
                {
                    lock (_queueLock)
                    {
                        _insertActionQueue.Enqueue(new InsertAction(newRec.Index));
                        _markStaleActionQueue.Enqueue(new MarkStaleAction(rec.Index));
                    }
                }
            }

            return updated;
        }
        #endregion

        #region delete
        public bool Delete(T rec)
        {
            bool deleted = false;
            if (rec != null)
            {
                if (rec.SyncRef != _syncRef || rec.IsStale)
                    throw new StaleRecordException();

                rec.IsStale = true;

                lock (_recLock)
                {
                    if (!_records[rec.Index].IsStale)
                    {
                        _records[rec.Index].IsStale = true;
                        deleted = true;
                    }
                }

                if (deleted)
                {
                    lock (_queueLock)
                    {
                        _markStaleActionQueue.Enqueue(new MarkStaleAction(rec.Index));
                    }
                }
            }
            return (deleted);
        }

        public bool Delete(int Id)
        {
            T rec = null;
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].Id == Id && _records[i].IsStale == false)
                {
                    rec = _records[i];
                    rec.SyncRef = _syncRef;
                    break;
                }
            }
            return this.Delete(rec);
        }
        #endregion

        #region query
        public MemDbExpression<T> Query()
        {
            return new MemDbExpression<T>(this.ExecuteQuery);
        }
        #endregion

        #region execute query
        private T[] ExecuteQuery(MemDbExpression<T> expression, bool deepCopy = true)
        {
            lock (_recLock)
            {
                List<T> matches = (expression.HasFilter)
                    ? _records.FindAll(r => r.IsStale == false && expression.Filter(r))
                    : _records.FindAll(r => r.IsStale == false);

                if (matches.Count > 0)
                {
                    if (expression.HasOrderBy)
                    {
                        matches.Sort(expression.OrderByComparison);
                    }
                    if (expression.HasSkip && expression.HasLimit)
                    {
                        matches = matches.Skip(expression.SkipCount).Take(expression.LimitCount).ToList();
                    }
                    else if (expression.HasSkip)
                    {
                        matches = matches.Skip(expression.SkipCount).ToList();
                    }
                    else if (expression.HasLimit)
                    {
                        matches = matches.Take(expression.LimitCount).ToList();
                    }
                }

                if (matches.Count > 0 && deepCopy)
                {
                    T[] copies = new T[matches.Count];
                    for (int i = 0; i < matches.Count; i++)
                    {
                        copies[i] = MemDbRecord.DeepCopy<T>(matches[i]);
                        copies[i].SyncRef = _syncRef;
                    }
                    return copies;
                }
                else if (matches.Count > 0)
                {
                    matches.ForEach(m => m.SyncRef = _syncRef);
                    return matches.ToArray();
                }
                else
                {
                    return new T[0];
                }
            }
        }
        #endregion

        #region stats
        public Dictionary<string, string> Stats()
        {
            Dictionary<string, string> stats = new Dictionary<string, string>();
            decimal staleMapLength;
            decimal freshMapLength;
            decimal staleRecCount;
            decimal freshRecCount;
            decimal totalDBSize;
            decimal totalMapSize;
            decimal staleRecLength;
            decimal freshRecLength;
            decimal avgRecLength;
            decimal fragmentation;

            this.Flush();

            lock (_fileLock)
            {
                FileInfo dbInfo = new FileInfo(_fullDbPath);
                totalDBSize = dbInfo.Length;

                FileInfo mapInfo = new FileInfo(_fullMapPath);
                totalMapSize = mapInfo.Length;
            }

            lock (_recLock)
            {
                staleRecCount = _map.Pointers.Count(p => p.IsStale);
                freshRecCount = _map.Pointers.Count(p => p.IsStale == false);
                staleRecLength = _map.Pointers.FindAll(p => p.IsStale).Sum(p => p.RecordLength);
                freshRecLength = _map.Pointers.FindAll(p => p.IsStale == false).Sum(p => p.RecordLength);
            }

            staleMapLength = (staleRecCount * MemDbPointer.Length);
            freshMapLength = (freshRecCount * MemDbPointer.Length);


            decimal totalRecCount = (staleRecCount + freshRecCount);
            decimal totalRecLength = (staleRecLength + freshRecLength);

            avgRecLength = (totalRecCount > 0)
                ? (totalRecLength) / (totalRecCount)
                : 0;

            fragmentation = 0;
            if (totalRecCount > 0)
            {
                if (freshRecCount == 0)
                {
                    fragmentation = 100;
                }
                else
                {
                    fragmentation = (staleRecCount / freshRecCount);
                }
            }

            stats.Add("Fresh Recs", freshRecCount.ToString());
            stats.Add("Stale Recs", staleRecCount.ToString());
            stats.Add("Fresh Length", freshRecLength.ToString());
            stats.Add("Stale Length", staleRecLength.ToString());
            stats.Add("Avg Length", avgRecLength.ToString());
            stats.Add("DB File Size", totalDBSize.ToString());
            stats.Add("Fresh Map Length", freshMapLength.ToString());
            stats.Add("Stale Map Length", staleMapLength.ToString());
            stats.Add("Map File Size", totalMapSize.ToString());

            stats.Add("Fragmentation Index", fragmentation.ToString());

            return stats;
        }
        #endregion

        #region defrag
        public void Defrag()
        {
            if (File.Exists(_fullDbPath) && File.Exists(_fullMapPath))
            {
                lock (_recLock)
                {
                    lock (_queueLock)
                    {
                        List<InsertAction> inserts = new List<InsertAction>(_records.Count(r => r.IsStale == false));

                        _records.Sort((a, b) => a.Id.CompareTo(b.Id)); //re-order the records by Id asc...

                        lock (_fileLock)
                        {

                            if (File.Exists(_fullDbPath + "bak"))
                            {
                                File.Delete(_fullDbPath + "bak");
                            }
                            File.Copy(_fullDbPath, _fullDbPath + "bak");
                            File.Delete(_fullDbPath);

                            if (File.Exists(_fullMapPath + "bak"))
                            {
                                File.Delete(_fullMapPath + "bak");
                            }
                            File.Copy(_fullMapPath, _fullMapPath + "bak");
                            File.Delete(_fullMapPath);

                            for (int i = 0; i < _records.Count; i++)
                            {
                                if (_records[i].IsStale == false)
                                {
                                    inserts.Add(new InsertAction(i));
                                }
                            }

                            MemDbMap tmpMap = _map;

                            try
                            {
                                _map = new MemDbMap();
                                this.EnsureFiles(out bool fileCreated);
                                this.InjectRecords(inserts);
                                File.Delete(_fullDbPath + "bak");
                                File.Delete(_fullMapPath + "bak");
                                _insertActionQueue.Clear();
                                _markStaleActionQueue.Clear();
                                _records.RemoveAll(r => r.IsStale);
                                _syncRef = Guid.NewGuid();
                            }
                            catch
                            {
                                _map = tmpMap;

                                if (File.Exists(_fullDbPath))
                                {
                                    File.Delete(_fullDbPath);
                                }

                                if (File.Exists(_fullMapPath))
                                {
                                    File.Delete(_fullMapPath);
                                }
                                File.Copy(_fullDbPath + "bak", _fullDbPath);
                                File.Copy(_fullMapPath + "bak", _fullMapPath);
                            }
                        }

                    }
                }
            }
        }
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
            _isClosed = true;

            _fileSyncTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _fileSyncTimer.Dispose();

            this.FileSync(new object());
            _records.Clear();

            _cryptoProvider = null;

            if (!isFinalizer)
            {
                GC.SuppressFinalize(this);
            }
        }
        #endregion

        #region Queue action item
        public struct InsertAction
        {
            #region interface
            public int RecordIndex { get; private set; }
            #endregion

            #region constructors
            public InsertAction(int index)
            {
                this.RecordIndex = index;
            }
            #endregion
        }

        public struct MarkStaleAction
        {
            #region interface
            public int RecordIndex { get; private set; }
            #endregion

            #region constructors
            public MarkStaleAction(int index)
            {
                this.RecordIndex = index;
            }
            #endregion
        }
        #endregion
    }
}