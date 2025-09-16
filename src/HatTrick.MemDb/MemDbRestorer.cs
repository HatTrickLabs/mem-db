using System;
using System.IO;
using System.Threading;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;

namespace HatTrick.Data
{
    internal sealed class MemDbRestorer
    {
        #region internals
        private string _outputPath;
        private string _fullDbPath;
        private string _fullMapPath;
        private string _archivePath;
        private string _fullZipArchiveFilePath;
        private string _fullRestoreDbPath;
        private string _fullRestoreMapPath;
        private long _utcTimestamp;
        private bool _overwrite;
        private IMemDbEncryptionInfo _encryptionInfo;

        private Dictionary<long, RestoreRecord> _records;
        #endregion

        #region ctors
        internal MemDbRestorer(MemDbConfiguration config, string outputPath, long utcTimestamp, bool overwrite = false)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            if (utcTimestamp >= DateTime.UtcNow.ToBinary())
                throw new ArgumentException("The 'restore to' utc timestamp must be in the past.", nameof(utcTimestamp));

            _overwrite = overwrite;
            _fullDbPath = config.GetDbFilePath();
            _fullMapPath = config.GetMapFilePath();
            _outputPath = outputPath;
            _utcTimestamp = utcTimestamp;
            _encryptionInfo = config.GetEncryptionInfo();

            _archivePath = config.ArchivePath;
            _fullZipArchiveFilePath = config.GetZipArchiveFilePath();
            _fullRestoreMapPath = Path.Combine(outputPath, $"htl.{config.DatasetName}.map");
            _fullRestoreDbPath = Path.Combine(outputPath, $"htl.{config.DatasetName}.db");
            this.EnsureRestoreDirectory(outputPath);
            this.EnsureRestoreMapFile(_fullRestoreMapPath);
            this.EnsureRestoreDbFile(_fullRestoreDbPath);

            if (!File.Exists(_fullMapPath))
                throw new InvalidOperationException($"No file exists at MemDb map file path '{_fullMapPath}'.");

            if (!File.Exists(_fullDbPath))
                throw new InvalidOperationException($"No file exists at MemDb data file path '{_fullDbPath}'.");

            _records = new Dictionary<long, RestoreRecord>(128);
        }
        #endregion

        #region ensure restore directory
        private void EnsureRestoreDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
        #endregion

        #region ensure restore map file
        private void EnsureRestoreMapFile(string path)
        {
            bool exists = File.Exists(path);
            if (exists && !_overwrite)
                throw new InvalidOperationException($"MemDb file already exists at restore path '{path}'{Environment.NewLine}Use 'overwrite' option on ctor to overwrite existing map files.");

            if (exists)
                File.Delete(path);

            _ = new MemDbMap(path, true, _encryptionInfo);
        }
        #endregion

        #region ensure restore db file
        private void EnsureRestoreDbFile(string path)
        {
            bool exists = File.Exists(path);
            if (exists && !_overwrite)
                throw new InvalidOperationException($"MemDb file already exists at restore path '{path}'{Environment.NewLine}Use 'overwrite' option on ctor to overwrite existing db files.");

            if (exists)
                using (var fs = new FileStream(path, FileMode.Truncate, FileAccess.Write)) { }

            else
                File.Create(path).Dispose();
        }
        #endregion

        #region restore
        internal void Restore()
        {
            this.RestoreFromZipArchives();

            MemDbMap map = this.ExtractDbMap();
            this.RollPointers(map, _fullDbPath);

            this.BuildRestoredFiles();
        }
        #endregion

        #region restore from zip archives
        private void RestoreFromZipArchives()
        {
            if (!File.Exists(_fullZipArchiveFilePath))
                return;//no archives exist, no defrag has ever been run.

            using ZipArchive zip = ZipFile.Open(_fullZipArchiveFilePath, ZipArchiveMode.Read);
            ZipArchiveEntry[] zipEntries = this.ResolveArchiveFileSets(zip);

            //TODO: Ensure available space...
            long maxBytes = zipEntries.Max(e => e.Length);
            this.EnsureDiskSpace(maxBytes);

            for (int i = 0; i < zipEntries.Length; i++)
            {
                var entry = zipEntries[i];

                //db will always be odd, map will always be even (db,map,db,map,db,map...etc)
                if (entry.Comment == "db")
                {
                    entry.ExtractToFile(Path.Combine(_archivePath, entry.Name));
                }
                else if (entry.Comment == "map")
                {
                    MemDbMap map = this.ExtractArchiveMap(entry);
                    string dbPath = Path.Combine(_archivePath, zipEntries[i - 1].Name);
                    this.RollPointers(map, dbPath);
                    File.Delete(dbPath);
                }
            }
        }
        #endregion

        #region resolve archive file sets
        private ZipArchiveEntry[] ResolveArchiveFileSets(ZipArchive zip)
        {
            int prefixLength = "htl.".Length + MemDbConfiguration.ArchiveTimestampFormat.Length;

            (string key, ZipArchiveEntry[] entries)[] sets = zip.Entries
                .GroupBy(e => e.Name.Substring(0, prefixLength))
                .Select(g => (g.Key, g.ToArray()))
                .OrderBy(g => g.Key)
                .ToArray();

            //each set has a db and a map (order not guaranteed).
            var entries = new ZipArchiveEntry[sets.Length * 2];
            int at = 0;
            for (int i = 0; i < sets.Length; i++)
            {
                var set = sets[i];
                entries[at++] = set.entries.First(e => e.Comment == "db");
                entries[at++] = set.entries.First(e => e.Comment == "map");
            }

            return entries;
        }
        #endregion

        #region extract archive map
        private MemDbMap ExtractArchiveMap(ZipArchiveEntry mapArchive)
        {
            //this is just tmp in-mem only, don't init to disk...
            //simply give the ctor a bunk path, do not initialize and never flush
            MemDbMap map = new MemDbMap("xxx", false, _encryptionInfo);
            using (var mapStream = mapArchive.Open())
            {
                using (var mapReader = new BinaryReader(mapStream))
                {
                    map.DeserializeFrom(mapReader);
                }
            }
            return map;
        }
        #endregion

        #region extract db map
        private MemDbMap ExtractDbMap()
        {
            //this is just tmp in-mem only, don't init to disk...
            //simply give the ctor a bunk path, do not initialize and never flush
            MemDbMap map = new MemDbMap("xxx", false, _encryptionInfo);
            using (var fs = new FileStream(_fullMapPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                using (var mapReader = new BinaryReader(fs))
                {
                    map.DeserializeFrom(mapReader);
                }
            }
            return map;
        }
        #endregion

        #region ensure disk space
        private void EnsureDiskSpace(long maxBytesNeeded)
        {
            //get available drive space 
            var directory = new DirectoryInfo(_archivePath);
            var drive = new DriveInfo(directory.Root.Name);
            long spaceAvailable = drive.AvailableFreeSpace;

            long spaceNeeded = (maxBytesNeeded + 4096 * 2);
            if (spaceAvailable < spaceNeeded)
            {
                string ex = "Restore cannot be completed..." + Environment.NewLine
                          + $"Insufficient disk space available on {drive.Name} ...{Environment.NewLine}"
                          + $"space needed: {spaceNeeded} space available: {spaceAvailable}";
                throw new InvalidOperationException(ex);
            }
        }
        #endregion

        #region roll pointers
        public void RollPointers(MemDbMap map, string dbFilePath)
        {
            using var db = new FileStream(dbFilePath, FileMode.Open, FileAccess.Read, FileShare.None);

            long restorePoint = _utcTimestamp;
            for (int i = 0; i < map.Count; i++)
            {
                MemDbPointer ptr = map[i];
                bool shouldCopy = false;

                if (ptr.CreatedAt > restorePoint)
                    continue;

                if (ptr.State == RecordState.Fresh)
                {
                    if (ptr.CreatedAt <= restorePoint)
                        shouldCopy = true;
                }
                else if (ptr.State == RecordState.Stale)
                {
                    if (ptr.CreatedAt <= restorePoint)
                        shouldCopy = true;
                }
                else if (ptr.State == RecordState.Deleted)
                {
                    if (ptr.StateSetAt <= restorePoint)
                        _ = _records.Remove(ptr.Id);

                    else if (ptr.CreatedAt <= restorePoint)
                            shouldCopy = true;
                }

                if (shouldCopy)
                {
                    db.Position = ptr.Position;
                    int length = ptr.IsEncrypted ? _encryptionInfo.GetEncryptedLength(ptr.Length) : ptr.Length;
                    var raw = new byte[length];
                    db.ReadExactly(raw, 0, raw.Length);
                    _records[ptr.Id] = new RestoreRecord(ptr, raw);
                }
            }
        }
        #endregion

        #region write restored db
        private void BuildRestoredFiles ()
        {
            long[] ids = _records.Keys.ToArray();

            long mapSize = MemDbMap.BinaryLengthOf(ids.Length);
            long dbSize = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                dbSize += _records[ids[i]].RawData.Length;
            }

            this.EnsureDiskSpace(mapSize + dbSize);

            MemDbMap map = new MemDbMap(_fullRestoreMapPath, true, _encryptionInfo);
            using (var db = new FileStream(_fullRestoreDbPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    RestoreRecord record = _records[ids[i]];
                    MemDbPointer oPtr = record.Pointer;
                    var nPtr = new MemDbPointer(oPtr.Id, RecordState.Fresh, oPtr.StateSetAt, oPtr.CreatedAt, oPtr.IsEncrypted, db.Position, oPtr.Length);
                    map.Add(nPtr);
                    db.Write(record.RawData);
                }
            }
            map.Flush();
        }
        #endregion

        #region restore recored [class]
        internal class RestoreRecord
        {
            internal MemDbPointer Pointer { get; private set; }
            internal byte[] RawData { get; private set; }

            internal RestoreRecord(MemDbPointer pointer, byte[] rawData)
            {
                this.Pointer = pointer;
                this.RawData = rawData;
            }
        }
        #endregion
    }
}
