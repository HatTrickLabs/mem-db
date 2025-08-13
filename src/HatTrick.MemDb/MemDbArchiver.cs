using System;
using System.IO;
using System.IO.Compression;

namespace HatTrick.InMemDb
{
    internal class MemDbArchiver : IMemDbArchiver
    {
        #region internals
        private string _datasetName;
        private string _archivePath;

        private string _mapPath;
        private string _dbPath;

        private IMemDbEncryptionInfo _encryptionInfo;

        private Func<DateTime, string> _getMapBackupPath;
        private Func<DateTime, string> _getDbBackupPath;

        string _zipArchivePath;

        private MemDbMap _map;
        private int _staleCount;
        private int _deletedCount;

        private MemDbMap _backupMap;

        private DateTime _archivedAt;
        #endregion

        #region ctors
        internal MemDbArchiver(MemDbConfiguration config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            if (!config.ShouldArchive)
                throw new InvalidOperationException($"Configuration for provided dataset '{config.DatasetName}' is not configured to archive on defrag.");

            if (!Directory.Exists(config.DbPath))
                throw new ArgumentException($"No directory exists at {nameof(config)}.{nameof(config.DbPath)}");

            _datasetName = config.DatasetName;
            _archivePath = config.ArchivePath;

            _encryptionInfo = config.GetEncryptionInfo();

            _dbPath = config.GetDbFilePath();
            _mapPath = config.GetMapFilePath();

            _getMapBackupPath = (timestamp) => config.GetMapBackupFilePath(timestamp);
            _getDbBackupPath = (timestamp) => config.GetDbBackupFilePath(timestamp);
            _zipArchivePath = config.GetZipArchiveFilePath();
        }
        #endregion

        #region archive
        void IMemDbArchiver.Archive()
        {
            _archivedAt = DateTime.UtcNow;

            //ensure the record map actually exists
            if (!File.Exists(_mapPath))
                throw new InvalidOperationException("No file exists at MemDb map file path: " + _mapPath);

            //ensure the data store actually exists
            if (!File.Exists(_dbPath))
                throw new InvalidOperationException("No file exists at MemDb data file path: " + _dbPath);

            //read into memory the fragmented map
            this.ReadFragmentedMap();

            if ((_map.StaleCount + _map.DeletedCount) == 0)
                return;//0 fragmentation, nothing can be archived.

            //ensure enough drive space exists to perform the defrag operation
            this.EnsureAvailableDriveSpace();

            this.EnsureArchivePath();

            //create backup map and db files
            this.CreateTempBackupFiles();

            //write the stale and deleted records into the backup files
            this.WriteBackupFiles();

            //pack the backup files into a zip archive.
            this.ZipAndPackBackupFiles();

            //delete the temp backup files
            this.DeleteTempBackupFiles();
        }
        #endregion

        #region ensure archive path
        private void EnsureArchivePath()
        {
            if (!Directory.Exists(_archivePath))
                Directory.CreateDirectory(_archivePath);
        }
        #endregion

        #region read fragmented map
        private void ReadFragmentedMap()
        {
            _map = new MemDbMap(_mapPath, true, _encryptionInfo);
            _staleCount = _map.StaleCount;
            _deletedCount = _map.DeletedCount;
        }
        #endregion

        #region ensure available drive space
        private void EnsureAvailableDriveSpace()
        {
            long mapSize = MemDbMap.BinaryLengthOf(_staleCount + _deletedCount);

            //get file size of the stale and deleted db records
            long dbSize = _map.TotalStaleSize + _map.TotalDeletedSize;

            //get available drive space 
            var directory = new DirectoryInfo(_archivePath);
            var drive = new DriveInfo(directory.Root.Name);
            long spaceAvailable = drive.AvailableFreeSpace;

            //conservative when assuming standard block size of 4096 bytes.
            long spaceNeeded = dbSize + mapSize + (4096 * 4);

            //must assume that 2* the space is needed (file to zip will reside an disk and be pushed into the zip)...
            //if map file will not compress much, and if the db file is binary serialized it wont compress much either...2* to be safe.
            spaceNeeded = spaceNeeded * 2;

            if (spaceAvailable < spaceNeeded)
            {
                string ex = "Archive cannot be completed ..." + Environment.NewLine
                          + $"Insufficient disk space available on {drive.Name} ...{Environment.NewLine}"
                          + $"space needed: {spaceNeeded} space available: {spaceAvailable}";
                throw new InvalidOperationException(ex);
            }
        }
        #endregion

        #region create temp backup files
        private void CreateTempBackupFiles()
        {
            string mapBackupPath = _getMapBackupPath(_archivedAt);
            string dbBackupPath = _getDbBackupPath(_archivedAt);

            if (File.Exists(mapBackupPath))
                File.Delete(mapBackupPath);

            if (File.Exists(dbBackupPath))
                File.Delete(dbBackupPath);


            _backupMap = new MemDbMap(mapBackupPath, true, _encryptionInfo);
            File.Create(dbBackupPath).Dispose();
        }
        #endregion

        #region write backup files
        private void WriteBackupFiles()
        {
            MemDbMap origMap = _map;
            MemDbMap backupMap = _backupMap;

            int maxRecLength = Math.Max(origMap.MaxStaleRecordSize, origMap.MaxDeletedRecordSize);

            byte[] buffer = new byte[maxRecLength];

            //build backup of all stale and deleted records
            using var origDb = new FileStream(_dbPath, FileMode.Open, FileAccess.Read, FileShare.None);
            using var backupDb = new FileStream(_getDbBackupPath(_archivedAt), FileMode.Open, FileAccess.Write, FileShare.None);

            for (int i = 0; i < origMap.Count; i++)
            {
                MemDbPointer oPtr = origMap[i];

                if (oPtr.State == RecordState.Fresh)
                    continue;

                origDb.Position = oPtr.Position;
                int actualLen = oPtr.IsEncrypted ? _encryptionInfo.GetEncryptedLength(oPtr.Length) : oPtr.Length;

                //we do not need to decrypt anything when moving to backup, simply copy the encrypted data
                //directly from the original file stream over to the backup file stream
                origDb.ReadExactly(buffer, 0, actualLen);

                //the pointer should always store the un-encrypted length...
                var nPtr = new MemDbPointer(oPtr.Id, oPtr.State, oPtr.StateSetAt, oPtr.CreatedAt, oPtr.IsEncrypted, backupDb.Position, oPtr.Length);
                backupMap.Add(nPtr);

                backupDb.Write(buffer, 0, actualLen);
            }

            //write the backup map file
            backupMap.Flush();
        }
        #endregion

        #region zip and pack backup files
        private void ZipAndPackBackupFiles()
        {
            string mapBackupPath = _getMapBackupPath(_archivedAt);
            string dbBackupPath = _getDbBackupPath(_archivedAt);

            ZipArchiveMode mode = File.Exists(_zipArchivePath) ? ZipArchiveMode.Update : ZipArchiveMode.Create;
            using (ZipArchive zip = ZipFile.Open(_zipArchivePath, mode))
            {
                string mapName = Path.GetFileName(mapBackupPath);
                ZipArchiveEntry map = zip.CreateEntryFromFile(mapBackupPath, mapName, CompressionLevel.Optimal);
                map.Comment = "map";

                string dbName = Path.GetFileName(dbBackupPath);
                ZipArchiveEntry db = zip.CreateEntryFromFile(dbBackupPath, dbName, CompressionLevel.Optimal);
                db.Comment = "db";
            }
        }
        #endregion

        #region delete temp archive files
        private void DeleteTempBackupFiles()
        {
            string mapBackupPath = _getMapBackupPath(_archivedAt);
            string dbBackupPath = _getDbBackupPath(_archivedAt);

            if (File.Exists(mapBackupPath))
                File.Delete(mapBackupPath);

            if (File.Exists(dbBackupPath))
                File.Delete(dbBackupPath);
        }
        #endregion
    }
}
