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
        private string _path;
        private string _name;

        private string _fullMapPath;
        private string _fullDbPath;
        private object _fileLock;

        private MemDbMap _map;
        private List<MemDbRecord<T>> _records;
        private object _recLock;

        private int _unHydratedCryptoRecCount;

        private int _lastId;
        private object _lastIdLock;

        private Queue<InsertAction> _insertActionQueue;
        private Queue<MarkStaleAction> _markStaleActionQueue;

        private object _queueLock;
        private Timer _fileSyncTimer;

        private IMemDbCryptoProvider _cryptoProvider;

        private bool _isClosed;
        #endregion

        #region interface
        public bool IsCryptoReady
        { get { return _cryptoProvider != null; } }
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

            MemDbRecord<T>.RegisterSerializer(serializer);

            _map = new MemDbMap();
            _records = new List<MemDbRecord<T>>(2048);

            this.Init();
            _fileSyncTimer = new Timer(new TimerCallback(this.FileSync), null, (1000 * 5), Timeout.Infinite); //5 seconds...
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
                                _records = new List<MemDbRecord<T>>((int)(_map.PointerCount * 1.25));
                            }
                        }

                        using (FileStream fsDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read))
                        {
                            using (BinaryReader reader = new BinaryReader(fsDb, Encoding.UTF8, true))
                            {
                                MemDbRecord<T> rec;
                                MemDbPointer pointer;
                                int staleCount = 0;
                                int index = -1;
                                for (int i = 0; i < _map.PointerCount; i++)
                                {
                                    rec = new MemDbRecord<T>();
                                    pointer = _map.Pointers[i];

                                    if (pointer.IsStale)
                                    {
                                        fsDb.Position += pointer.Length;
                                        staleCount += 1;
                                    }
                                    else if (pointer.IsEncrypted && !this.IsCryptoReady)
                                    {
                                        fsDb.Position += pointer.Length;
                                        _unHydratedCryptoRecCount += 1;
                                    }
                                    else
                                    {
                                        if (pointer.IsEncrypted)
                                        {
                                            using (MemoryStream msEncrypted = new MemoryStream())
                                            {
                                                byte[] buffer = new byte[pointer.Length];
                                                fsDb.Read(buffer, 0, pointer.Length);
                                                msEncrypted.Write(buffer, 0, pointer.Length);

                                                using (MemoryStream msClear = new MemoryStream())
                                                {
                                                    _cryptoProvider.Decrypt(msEncrypted, msClear, pointer.Id.ToString("0000000000"));
                                                    msClear.Position = 0;
                                                    using (BinaryReader localReader = new BinaryReader(msClear, Encoding.UTF8, true))
                                                    {
                                                        rec.DeserializeFrom(localReader);
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            rec.DeserializeFrom(reader);
                                        }

                                        rec.MapIndex = i;
                                        rec.Index = ++index;
                                        _records.Add(rec);
                                    }
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
                        MemDbRecord<T> rec;
                        MemDbPointer pointer;
                        int index = -1;
                        for (int i = 0; i < _map.PointerCount; i++)
                        {
                            rec = new MemDbRecord<T>(null);
                            pointer = _map.Pointers[i];

                            if (pointer.IsEncrypted && !pointer.IsStale)
                            {
                                using (MemoryStream msEncrypted = new MemoryStream())
                                {
                                    byte[] buffer = new byte[pointer.Length];
                                    fsDb.Read(buffer, 0, pointer.Length);
                                    msEncrypted.Write(buffer, 0, pointer.Length);

                                    using (MemoryStream msClear = new MemoryStream())
                                    {
                                        using (BinaryReader reader = new BinaryReader(msClear, Encoding.UTF8, true))
                                        {
                                            msEncrypted.Position = 0;
                                            _cryptoProvider.Decrypt(msEncrypted, msClear, pointer.Id.ToString("0000000000"));
                                            msClear.Position = 0;
                                            rec.DeserializeFrom(reader);
                                        }
                                    }
                                }
                                rec.MapIndex = i;
                                rec.Index = ++index;
                                _records.Add(rec);
                            }
                            else
                            {
                                fsDb.Position += pointer.Length;
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
                        using (BinaryWriter writer = new BinaryWriter(fsDb, Encoding.UTF8, true))
                        {
                            fsDb.Position = fsDb.Length;
                            int lastPosition = (int)fsDb.Position;
                            int length;
                            foreach (InsertAction insert in inserts)
                            {
                                var rec = default(MemDbRecord<T>);
                                lock (_recLock)
                                {
                                    rec = _records[insert.RecordIndex];
                                }

                                if (rec.IsEncrypted)
                                {
                                    using (MemoryStream msClear = new MemoryStream())
                                    {
                                        using (BinaryWriter localWriter = new BinaryWriter(msClear, Encoding.UTF8, true))
                                        {
                                            rec.SerializeTo(localWriter);
                                            using (MemoryStream msEncrypted = new MemoryStream())
                                            {
                                                msClear.Position = 0;
                                                _cryptoProvider.Encrypt(msClear, msEncrypted, rec.Id.ToString("0000000000"));

                                                fsDb.Write(msEncrypted.ToArray(), 0, (int)msEncrypted.Length);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    rec.SerializeTo(writer);
                                }

                                length = (int)fsDb.Position - lastPosition;
                                MemDbPointer pointer = new MemDbPointer(rec.Id, false, rec.IsEncrypted, lastPosition, length);
                                _map.AddPointer(pointer);
                                pointer.SerializeTo(fsMap);
                                rec.MapIndex = (_map.PointerCount - 1);
                                lastPosition += length;
                            }
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
                    var rec = default(MemDbRecord<T>);
                    foreach (MarkStaleAction action in actions)
                    {
                        MemDbPointer oldPointer;
                        lock (_recLock)
                        {
                            rec = _records[action.RecordIndex];
                            oldPointer = _map.Pointers[rec.MapIndex];
                            _map.Pointers[rec.MapIndex] = new MemDbPointer(oldPointer.Id, true, oldPointer.IsEncrypted, oldPointer.Position, oldPointer.Length);
                        }

                        fsMap.Position = 4 + (rec.MapIndex * MemDbPointer.Size) + 4; //count + (mapindex * mapsize) + id
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

                    //if not crypto ready, encrypted rec may have hightest Id because rec couldnt be hydrated yet...
                    int lastMapId = (_map.PointerCount > 0) ? _map.Pointers.Max(p => p.Id) : 0;

                    //records don't get added to the map.pointer list until flushed to disc.
                    int lastRecId = (_records.Count > 0) ? _records.Max(r => r.Id) : 0;

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
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].IsStale == false && where(_records[i].Value))
                {
                    rec = _records[i];
                    break;
                }
            }
            T outVal = null;
            if (rec != null)
            {
                outVal = MemDbRecord<T>.DeepCopyOf(rec.Value);
            }
            return outVal;
        }
        #endregion

        #region find all
        public T[] FindAll(Func<T, bool> where)
        {
            List<MemDbRecord<T>> matches = null;
            //TODO: this looks like a race conidtion issue...prob need to enumerate set with
            //Where(predicate) and deep copy with a Select(r => MemDbRecord<T>.DeepCopyOf(r.Value)
            //within the record lock OR just accept that a match.Value could get marked stale prior
            //to being deep copied
            lock (_recLock)
            {
                matches = _records.FindAll(r => r.IsStale == false && where(r.Value));
            }
            T[] set = new T[matches.Count];
            for (int i = 0; i < set.Length; i++)
            {
                set[i] = MemDbRecord<T>.DeepCopyOf(matches[i].Value);
            }
            return set;
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
            if (apply == null)
                throw new ArgumentNullException(nameof(apply));

            if (where == null)
                throw new ArgumentNullException(nameof(where));

            List<MemDbRecord<T>> matches = null;
            lock (_recLock)
            {
                matches = _records.FindAll(r => r.IsStale == false && where(r.Value));
            }

            if (matches.Count == 0)
                return 0;

            for (int i = 0; i < matches.Count; i++)
            {
                var oldRec = matches[i];
                var newRec = new MemDbRecord<T>(MemDbRecord<T>.DeepCopyOf(oldRec.Value));
                matches[i].IsStale = true;//must mark stale AFTER deep copy
                apply(newRec.Value);

                lock (_recLock)
                {
                    _records.Add(newRec);
                    newRec.Index = _records.Count - 1;
                }

                lock (_queueLock)
                {
                    _insertActionQueue.Enqueue(new InsertAction(newRec.Index));
                    _markStaleActionQueue.Enqueue(new MarkStaleAction(oldRec.Index));
                }
            }

            return matches.Count;
        }
        #endregion

        #region delete
        public int Delete(Func<T, bool> where)
        {
            IEnumerable<MemDbRecord<T>> matches = _records.Where(r => r.IsStale == false && where(r.Value));

            if (!matches.Any())
                return 0;

            int cnt = 0;
            foreach (var oldRec in matches)
            {
                cnt += 1;
                lock (_recLock)
                {
                    oldRec.IsStale = true;
                }
                lock (_queueLock)
                {
                    _markStaleActionQueue.Enqueue(new MarkStaleAction(oldRec.Index));
                }
            }

            return cnt;
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
            Func<MemDbRecord<T>, bool> filter = expression.HasFilter
                ? (r) => r.IsStale == false && expression.Filter(r.Value)
                : (r) => r.IsStale == false;

            T[] copies = Array.Empty<T>();

            lock (_recLock)
            {
                List<T> matches = _records.Where(filter).Select(r => r.Value).ToList();

                if (matches.Count > 0)
                {
                    if (expression.HasOrderBy)
                        matches.Sort(expression.OrderByComparison);

                    if (expression.HasSkip && expression.HasLimit)
                        matches = matches.Skip(expression.SkipCount).Take(expression.LimitCount).ToList();

                    else if (expression.HasSkip)
                        matches = matches.Skip(expression.SkipCount).ToList();

                    else if (expression.HasLimit)
                        matches = matches.Take(expression.LimitCount).ToList();

                    if (deepCopy)
                    {
                        copies = new T[matches.Count];
                        for (int i = 0; i < matches.Count; i++)
                        {
                            copies[i] = MemDbRecord<T>.DeepCopyOf(matches[i]);
                        }
                    }
                    else
                    {
                        copies = matches.ToArray();
                    }
                }
            }

            return copies;
        }
        #endregion

        #region stats
        public Dictionary<string, decimal> Stats()
        {
            Dictionary<string, decimal> stats = new Dictionary<string, decimal>();
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
                staleRecLength = _map.Pointers.FindAll(p => p.IsStale).Sum(p => p.Length);
                freshRecLength = _map.Pointers.FindAll(p => p.IsStale == false).Sum(p => p.Length);
            }

            staleMapLength = (staleRecCount * MemDbPointer.Size);
            freshMapLength = (freshRecCount * MemDbPointer.Size);


            decimal totalRecCount = (staleRecCount + freshRecCount);
            decimal totalRecLength = (staleRecLength + freshRecLength);

            avgRecLength = (totalRecCount > 0)
                ? (totalRecLength) / (totalRecCount)
                : 0;

            fragmentation = 0;
            if (totalRecCount > 0)
            {
                if (freshRecCount == 0)
                    fragmentation = 100;

                else
                    fragmentation = (staleRecCount / freshRecCount);
            }

            stats.Add("Fresh Recs", freshRecCount);
            stats.Add("Stale Recs", staleRecCount);
            stats.Add("Fresh Length", freshRecLength);
            stats.Add("Stale Length", staleRecLength);
            stats.Add("Avg Length", avgRecLength);
            stats.Add("DB File Size", totalDBSize);
            stats.Add("Fresh Map Length", freshMapLength);
            stats.Add("Stale Map Length", staleMapLength);
            stats.Add("Map File Size", totalMapSize);
            stats.Add("Fragmentation Index", fragmentation);

            return stats;
        }
        #endregion

        #region defrag
        public void Defrag()
        {
            lock (_fileLock)
            {
                if (File.Exists(_fullDbPath) && File.Exists(_fullMapPath))
                {
                    lock (_recLock)
                    {
                        lock (_queueLock)
                        {
                            this.LockedDefrag();
                        }
                    }
                }
            }
        }

        private void LockedDefrag()
        {
            this.Flush();

            _records.Sort((a, b) => a.Id.CompareTo(b.Id)); //re-order the records by Id asc...

            if (File.Exists(_fullDbPath + "bak"))
                File.Delete(_fullDbPath + "bak");

            if (File.Exists(_fullMapPath + "bak"))
                File.Delete(_fullMapPath + "bak");

            File.Copy(_fullDbPath, _fullDbPath + "bak");
            File.Copy(_fullMapPath, _fullMapPath + "bak");

            File.Delete(_fullDbPath);
            File.Delete(_fullMapPath);

            List<InsertAction> inserts = _records.Where(r => r.IsStale == false)
                .Select((r) => new InsertAction(r.Index))
                .ToList();

            MemDbMap tmpMap = _map;

            try
            {
                _map = new MemDbMap();
                this.EnsureFiles(out bool fileCreated);
                this.InjectRecords(inserts);
                File.Delete(_fullDbPath + "bak");
                File.Delete(_fullMapPath + "bak");
                _insertActionQueue.Clear();
                _insertActionQueue.TrimExcess();
                _markStaleActionQueue.Clear();
                _markStaleActionQueue.TrimExcess();
                _records.RemoveAll(r => r.IsStale);
            }
            catch
            {
                _map = tmpMap;

                if (File.Exists(_fullDbPath))
                    File.Delete(_fullDbPath);

                if (File.Exists(_fullMapPath))
                    File.Delete(_fullMapPath);

                File.Copy(_fullDbPath + "bak", _fullDbPath);
                File.Copy(_fullMapPath + "bak", _fullMapPath);
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