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
        private Func<DateTime,  string> _snapshotMapPath;
        private Func<DateTime, string> _snapshotDbPath;
        #endregion

        #region ctors
        public MemDbSnapshotter(MemDbConfiguration config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            _mapPath = config.GetFullMapFilePath();
            _dbPath = config.GetFullDbFilePath();
            _snapshotMapPath =(timestamp) => config.GetFullSnapshotMapFilePath(timestamp);
            _snapshotDbPath = (timestamp) => config.GetFullSnapshotDbFilePath(timestamp);
        }
        #endregion

        #region write snapshot
        void IMemDbSnapshotter.WriteSnapshot(DateTime timestamp)
        {
            using var fsDb = new FileStream(_dbPath, FileMode.Open, FileAccess.Read, FileShare.None);
            using var fsSnapshotDb = new FileStream(_snapshotDbPath(timestamp), FileMode.Open, FileAccess.Write, FileShare.None);

            var map = new MemDbMap(_mapPath, true);
            var snapshotMap = new MemDbMap(_snapshotMapPath(timestamp), true);

            int maxRecLength = map.MaxFreshRecordSize;

            byte[] buffer = new byte[maxRecLength];

            for (int i = 0; i < map.Count; i++)
            {
                MemDbPointer oPtr = map[i];

                if (oPtr.State != RecordState.Fresh)
                    continue;

                fsDb.Position = oPtr.Position;
                int actualLen = oPtr.IsEncrypted ? MemDbAESEncryptor.CalculateCryptoByteLength(oPtr.Length) : oPtr.Length;

                fsDb.ReadExactly(buffer, 0, actualLen);

                //the pointer should always store the un-encrypted length...
                var nPtr = new MemDbPointer(oPtr.Id, RecordState.Fresh, oPtr.StateSetAt, oPtr.CreatedAt, oPtr.IsEncrypted, (uint)fsSnapshotDb.Position, oPtr.Length);
                snapshotMap.Add(nPtr);

                fsSnapshotDb.Write(buffer, 0, actualLen);
            }

            snapshotMap.OverrideLastId(map.LastId);
            snapshotMap.Flush();
        }
        #endregion
    }
}
