using System;
using System.IO;
using System.IO.Compression;

namespace HatTrick.InMemDb
{
    internal class MemDbArchiver : IMemDbArchiver
    {
        #region internals
        private string _path;
        private string _datasetName;
        private string _archivePath;

        private string _fullMapPath;
        private string _fullDbPath;

        string _fullMapArchivePath;
        string _fullDbArchivePath;

        string _fullZipArchivePath;

        private MemDbMap _map;
        private int _staleCount;
        private int _deletedCount;

        private MemDbMap _archiveMap;

        private DateTime _now;
        #endregion

        #region ctors
        internal MemDbArchiver(MemDbConfiguration config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            if (!config.ShouldArchive)
                throw new InvalidOperationException($"Configuration for provided dataset '{config.DatasetName}' is not configured to archive on defrag.");

            if (!Directory.Exists(config.Path))
                throw new ArgumentException($"No directory exists at {nameof(config)}.{nameof(config.Path)}");

            _path = config.Path;
            _datasetName = config.DatasetName;
            _archivePath = config.ArchivePath;



            _fullDbPath = config.GetFullDbFilePath();
            _fullMapPath = config.GetFullMapFilePath();

            _now = DateTime.Now;
            _fullMapArchivePath = config.GetFullMapArchiveFilePath(_now);
            _fullDbArchivePath = config.GetFullDbArchiveFilePath(_now);
            _fullZipArchivePath = config.GetZipArchiveFullFilePath();
        }
        #endregion

        #region archive
        void IMemDbArchiver.Archive()
        {
            //ensure the record map actually exists
            if (!File.Exists(_fullMapPath))
                throw new InvalidOperationException("No file exists at MemDb map file path: " + _fullMapPath);

            //ensure the data store actually exists
            if (!File.Exists(_fullDbPath))
                throw new InvalidOperationException("No file exists at MemDb data file path: " + _fullDbPath);

            //read into memory the fragmented map
            this.ReadFragmentedMap();

            if ((_map.StaleCount + _map.DeletedCount) == 0)
                return;//0 fragmentation, nothing can be archived.

            //ensure enough drive space exists to perform the defrag operation
            this.EnsureAvailableDriveSpace();

            this.EnsureArchivePath();

            //archive out the stale and deleted records into new map and db files
            this.CreateTempArchiveFiles();

            //write the stale and deleted records into the archive files
            this.WriteArchiveFiles();

            //pack the archived files into a zip archive.
            this.ZipAndPackArchiveFiles();

            //delete the zip packed temp files.
            this.DeleteTempArchiveFiles();
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
            _map = new MemDbMap(_fullMapPath, true);
            _staleCount = _map.StaleCount;
            _deletedCount = _map.DeletedCount;
        }
        #endregion

        #region ensure available drive space
        private void EnsureAvailableDriveSpace()
        {
            //get file size of the stale and deleted map pointers (needed for archive map)
            //sizeof(int) + sizeof(uint) + ((stalePointerCount + deletedPointerCount) * PointerByteSize)
            //the sizeof(int) is to account for the 32 bit int at the very beginning of the file (total pointer count)
            //the sizeof(uint) is to account for the 32 bit unsigned int at the beginning of the file (Last Identity)
            //TODO: these (outside of MemDbMap) size calcs are going to bite you in the ASS.
            long mapSize = sizeof(int) + sizeof(uint) + ((_staleCount + _deletedCount) * MemDbPointer.Size);

            //get file size of the stale and deleted db records
            long dbSize = _map.TotalStaleSize + _map.TotalDeletedSize;

            //get available drive space 
            var directory = new DirectoryInfo(_archivePath);
            var drive = new DriveInfo(directory.Root.Name);
            long spaceAvailable = drive.AvailableFreeSpace;

            //conservative when assuming standard block size of 4096 bytes.
            long spaceNeeded = dbSize + mapSize + (4096 * 2);

            if (spaceAvailable < spaceNeeded)
            {
                string ex = "Archive cannot be completed ..." + Environment.NewLine
                          + $"Insufficient disk space available on {drive.Name} ...{Environment.NewLine}"
                          + $"space needed: {spaceNeeded} space available: {spaceAvailable}";
                throw new InvalidOperationException(ex);
            }
        }
        #endregion

        #region create temp archive files
        private void CreateTempArchiveFiles()
        {
            if (File.Exists(_fullMapArchivePath))
                File.Delete(_fullMapArchivePath);

            if (File.Exists(_fullDbArchivePath))
                File.Delete(_fullDbArchivePath);


            _archiveMap = new MemDbMap(_fullMapArchivePath, true);
            File.Create(_fullDbArchivePath).Dispose();
        }
        #endregion

        #region write archive files
        private void WriteArchiveFiles()
        {
            MemDbMap origMap = _map;
            MemDbMap archMap = _archiveMap;

            int maxRecLength = Math.Max(origMap.MaxStaleRecordSize, origMap.MaxDeletedRecordSize);

            byte[] buffer = new byte[maxRecLength];

            //build archive of all stale and deleted records
            using var origDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read);
            using var archDb = new FileStream(_fullDbArchivePath, FileMode.Open, FileAccess.Write);

            for (int i = 0; i < origMap.Count; i++)
            {
                MemDbPointer oPtr = origMap[i];

                if (oPtr.State == RecordState.Fresh)
                    continue;

                origDb.Position = oPtr.Position;
                int actualLen = oPtr.IsEncrypted ? MemDbAESEncryptor.CalculateCryptoByteLength(oPtr.Length) : oPtr.Length;

                //we do not need to decrypt anything when moving to archive, simply copy the encrypted data
                //directly from the original file stream over to the archive file stream
                origDb.ReadExactly(buffer, 0, actualLen);

                //the pointer should always store the un-encrypted length...
                var nPtr = new MemDbPointer(oPtr.Id, oPtr.State, oPtr.StateSetAt, oPtr.IsEncrypted, (uint)archDb.Position, oPtr.Length);
                archMap.Add(nPtr);

                archDb.Write(buffer, 0, actualLen);
            }

            //write the archive map file
            archMap.Flush();
        }
        #endregion

        #region zip and pack archive files
        private void ZipAndPackArchiveFiles()
        {
            ZipArchiveMode mode = File.Exists(_fullZipArchivePath) ? ZipArchiveMode.Update : ZipArchiveMode.Create;
            using (ZipArchive zip = ZipFile.Open(_fullZipArchivePath, mode))
            {
                string mapName = Path.GetFileName(_fullMapArchivePath);
                ZipArchiveEntry map = zip.CreateEntryFromFile(_fullMapArchivePath, mapName, CompressionLevel.Optimal);
                map.Comment = "map";

                string dbName = Path.GetFileName(_fullDbArchivePath);
                ZipArchiveEntry db = zip.CreateEntryFromFile(_fullDbArchivePath, dbName, CompressionLevel.Optimal);
                db.Comment = "db";
            }
        }
        #endregion

        #region delete temp archive files
        private void DeleteTempArchiveFiles()
        {
            if (File.Exists(_fullMapArchivePath))
                File.Delete(_fullMapArchivePath);

            if (File.Exists(_fullDbArchivePath))
                File.Delete(_fullDbArchivePath);
        }
        #endregion
    }
}
