using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace HatTrick.InMemDb
{
    internal sealed class MemDbRestorer<T> where T : class
    {
        #region internals
        private string _outputPath;
        private string _fullDbPath;
        private string _fullMapPath;
        private string _fullRestoreDbPath;
        private string _fullRestoreMapPath;
        private long _utcTimestamp;
        private bool _overwrite;
        #endregion

        #region ctors
        internal MemDbRestorer(MemDbConfiguration config, string outputPath, long utcTimestamp, bool overwrite = false)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            if (!Directory.Exists(outputPath))
                throw new ArgumentException("No directory exists for provided path.", nameof(outputPath));

            if (utcTimestamp <= DateTime.UtcNow.ToBinary())
                throw new ArgumentException("", nameof(utcTimestamp));

            _overwrite = overwrite;
            _fullDbPath = config.GetFullDbFilePath();
            _fullMapPath = config.GetFullMapFilePath();
            _utcTimestamp = utcTimestamp;

            _fullRestoreMapPath = Path.Combine(outputPath, $"htl.{config.DatasetName}.db");
            _fullRestoreDbPath = Path.Combine(outputPath, $"htl.{config.DatasetName}.map");
            this.EnsureRestoreFile(_fullRestoreMapPath);
            this.EnsureRestoreFile(_fullRestoreDbPath);
        }
        #endregion

        #region initialize file
        private void EnsureRestoreFile(string path)
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
            //TODO: Ensure available space...

            //ensure the record map actually exists
            if (!File.Exists(_fullMapPath))
                throw new InvalidOperationException("No file exists at MemDb map file path: " + _fullMapPath);

            //ensure the data store actually exists
            if (!File.Exists(_fullDbPath))
                throw new InvalidOperationException("No file exists at MemDb data file path: " + _fullDbPath);

            var set = new Dictionary<uint, MemDbPointer>(256);
            this.ResolvePortRecords(_fullMapPath, ref set);
        }
        #endregion

        #region resove port records
        private Dictionary<uint, MemDbPointer> ResolvePortRecords(string mapPath, ref Dictionary<uint, MemDbPointer> pointers)
        {
            var map = new MemDbMap(mapPath, true);

            var set = new Dictionary<uint, MemDbPointer>();

            for (int i = 0; i < map.Count; i++)
            {
                MemDbPointer ptr = map[i];

                if (ptr.StateSetAt > _utcTimestamp)
                    break;//maps are always in chronological order...break as soon as we hit the restore point timestamp.

                if (ptr.State == RecordState.Fresh)
                    set[ptr.Id] = ptr;
            }

            return set;
        }
        #endregion

        #region port fresh records
        private void PortFreshRecords()
        {
            var origMap = new MemDbMap(_fullMapPath, true);
            var restoreMap = new MemDbMap(_fullRestoreMapPath, true);

            //TODO:
            //restoreMap.SetLastId(origMap.LastId);

            int maxRecLength = origMap.MaxFreshRecordSize;

            byte[] buffer = new byte[maxRecLength];

            using var origDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read);
            using var restoreDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.ReadWrite);

            for (int i = 0; i < origMap.Count; i++)
            {
                var oPtr = origMap[i];

                if (oPtr.StateSetAt > _utcTimestamp)
                    continue;//if it happened after the restore point, just toss it.

                if (oPtr.State == RecordState.Fresh)
                {
                }

                else if (oPtr.State == RecordState.Stale)
                {
                }

                else if (oPtr.State == RecordState.Deleted)
                {
                }

                if (oPtr.StateSetAt > _utcTimestamp)
                    continue;

                //var nPtr = new MemDbPointer(oPtr.Id, oPtr.State, oPtr.StateSetAt, oPtr.IsEncrypted, oPtr.Position, oPtr.Length);
                restoreMap.Add(oPtr.Clone());

                origDb.Position = oPtr.Position;
                int actualLen = oPtr.IsEncrypted ? MemDbAESEncryptor.CalculateCryptoByteLength(oPtr.Length) : oPtr.Length;

                origDb.ReadExactly(buffer, 0, actualLen);
                restoreDb.Write(buffer, 0, actualLen);
            }

            restoreMap.Flush();
        }
        #endregion

        #region restore deleted records
        private void RestoreDeletedRecords()
        {

        }
        #endregion
    }
}
