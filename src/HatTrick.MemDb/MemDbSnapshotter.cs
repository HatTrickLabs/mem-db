using System;
using System.IO;
using System.Text;

namespace HatTrick.InMemDb
{
    internal sealed class MemDbSnapshotter : IMemDbSnapshotter
    {
        #region internals
        private string _mapPath;
        private string _dbPath;
        private IMemDbEncryptionInfo _encryptionInfo;
        private Func<DateTime,  string> _snapshotMapPath;
        private Func<DateTime, string> _snapshotDbPath;
        #endregion

        #region ctors
        public MemDbSnapshotter(MemDbConfiguration config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            _mapPath = config.GetMapFilePath();
            _dbPath = config.GetDbFilePath();
            _encryptionInfo = config.GetEncryptionInfo();
            _snapshotMapPath =(timestamp) => config.GetSnapshotMapFilePath(timestamp);
            _snapshotDbPath = (timestamp) => config.GetSnapshotDbFilePath(timestamp);
        }
        #endregion

        #region write snapshot
        DateTime IMemDbSnapshotter.Snapshot()
        {
            DateTime timestamp = DateTime.UtcNow;
            this.EnsureSnapshotDirectoryPath(timestamp);

            using var fsDb = new FileStream(_dbPath, FileMode.Open, FileAccess.Read, FileShare.None);
            using var fsSnapshotDb = new FileStream(_snapshotDbPath(timestamp), FileMode.CreateNew, FileAccess.Write, FileShare.None);

            var map = new MemDbMap(_mapPath, true, _encryptionInfo);
            var snapshotMap = new MemDbMap(_snapshotMapPath(timestamp), true, _encryptionInfo);

            int maxRecLength = map.MaxFreshRecordSize;

            byte[] buffer = new byte[maxRecLength];

            for (int i = 0; i < map.Count; i++)
            {
                MemDbPointer oPtr = map[i];

                if (oPtr.State != RecordState.Fresh)
                    continue;

                fsDb.Position = oPtr.Position;
                int actualLen = oPtr.IsEncrypted ? _encryptionInfo.GetEncryptedLength(oPtr.Length) : oPtr.Length;

                fsDb.ReadExactly(buffer, 0, actualLen);

                //the pointer should always store the un-encrypted length...
                var nPtr = new MemDbPointer(oPtr.Id, RecordState.Fresh, oPtr.StateSetAt, oPtr.CreatedAt, oPtr.IsEncrypted, fsSnapshotDb.Position, oPtr.Length);
                snapshotMap.Add(nPtr);

                fsSnapshotDb.Write(buffer, 0, actualLen);
            }

            snapshotMap.OverrideLastId(map.LastId);
            snapshotMap.Flush();

            return timestamp;
        }
        #endregion

        #region ensure directory path
        private void EnsureSnapshotDirectoryPath(DateTime timestamp)
        {
            string path = Path.GetDirectoryName(_snapshotMapPath(timestamp));

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
        #endregion
    }
}
