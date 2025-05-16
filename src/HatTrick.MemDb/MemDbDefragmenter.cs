using System;
using System.IO;

namespace HatTrick.InMemDb
{
    internal class MemDbDefragmenter : IMemDbDefragmenter
    {
        #region internals
        private string _path;
        private string _datasetName;

        private IMemDbEncryptionInfo _encryptionInfo;

        private string _fullMapPath;
        private string _fullDbPath;

        private string _fullTempMapPath;
        private string _fullTempDbPath;

        private MemDbMap _originalMap;
        private int _staleCount;
        private int _deletedCount;

        private MemDbMap _freshMap;
        #endregion

        #region constructors
        internal MemDbDefragmenter(MemDbConfiguration config)
        {
            if (!Directory.Exists(config.Path))
                throw new ArgumentException($"No directory exists at {nameof(config)}.{nameof(config.Path)}");

            _path = config.Path;
            _datasetName = config.DatasetName;
            _encryptionInfo = config.GetEncryptionInfo();
            _fullDbPath = config.GetFullDbFilePath();
            _fullMapPath = config.GetFullMapFilePath();

            _fullTempMapPath = _fullMapPath + ".temp";
            _fullTempDbPath = _fullDbPath + ".temp";
        }
        #endregion

        #region defrag
        void IMemDbDefragmenter.Defrag()
        {
            //ensure the record map actually exists
            if (!File.Exists(_fullMapPath))
                throw new InvalidOperationException("No file exists at MemDb map file path: " + _fullMapPath);

            //ensure the data store actually exists
            if (!File.Exists(_fullDbPath))
                throw new InvalidOperationException("No file exists at MemDb data file path: " + _fullDbPath);

            //read into memory the fragmented map
            this.ReadFragmentedMap();

            if ((_originalMap.StaleCount + _originalMap.DeletedCount) == 0)
                return;//0 fragmentation, nothing can be cleaned up

            //ensure enough drive space exists to perform the defrag operation
            this.EnsureAvailableDriveSpace();

            //create the empty defragged files
            this.CreateTempDefragmentedFiles();

            //create temp defragged db
            this.WriteDefragmentedTempFiles();

            //delete the old fragmented files
            this.DeleteFragmetedFiles();

            //rename the temp defragmented files to remove the 'temp' designation
            this.RenameTempDefragmentedFiles();
        }
        #endregion

        #region read fragmented map
        private void ReadFragmentedMap()
        {
            _originalMap = new MemDbMap(_fullMapPath, true, _encryptionInfo);
            _staleCount = _originalMap.StaleCount;
            _deletedCount = _originalMap.DeletedCount;
        }
        #endregion

        #region ensure available drive space
        private void EnsureAvailableDriveSpace()
        {
            long mapSize = MemDbMap.BinaryLengthOf(_originalMap.Count - (_staleCount + _deletedCount));

            //get file size of the non-stale...non-deleted db records
            long dbSize = _originalMap.TotalFreshSize;

            //get available drive space 
            var directory = new DirectoryInfo(_path);
            var drive = new DriveInfo(directory.Root.Name);
            long spaceAvailable = drive.AvailableFreeSpace;

            //conservative when assuming standard block size of 4096 bytes.
            long spaceNeeded = dbSize + mapSize + (4096 * 2);

            if (spaceAvailable < spaceNeeded)
            {
                string ex = "Defragment cannot be completed..." + Environment.NewLine
                          + $"Insufficient disk space available on {drive.Name} ...{Environment.NewLine}"
                          + $"space needed: {spaceNeeded} space available: {spaceAvailable}";
                throw new InvalidOperationException(ex);
            }
        }
        #endregion

        #region create tmp defragmented files
        private void CreateTempDefragmentedFiles()
        {
            if (File.Exists(_fullTempMapPath))
                File.Delete(_fullTempMapPath);

            if (File.Exists(_fullTempDbPath))
                File.Delete(_fullTempDbPath);

            _freshMap = new MemDbMap(_fullTempMapPath, true, _encryptionInfo);
            File.Create(_fullTempDbPath).Dispose();
        }
        #endregion

        #region write defragmented temp files
        private void WriteDefragmentedTempFiles()
        {
            MemDbMap origMap = _originalMap;
            MemDbMap freshMap = _freshMap;

            _freshMap.OverrideLastId(_originalMap.LastId);

            int maxRecLength = origMap.MaxFreshRecordSize;

            byte[] buffer = new byte[maxRecLength];

            //rebuild the db and map bypassing all stale and deleted records
            using var oldDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read, FileShare.None);
            using var newDb = new FileStream(_fullTempDbPath, FileMode.Open, FileAccess.Write, FileShare.None);

            for (int i = 0; i < origMap.Count; i++)
            {
                MemDbPointer oPtr = origMap[i];

                if (oPtr.State != RecordState.Fresh)
                    continue;

                oldDb.Position = oPtr.Position;
                int actualLen = oPtr.IsEncrypted ? _encryptionInfo.GetEncryptedLength(oPtr.Length) : oPtr.Length;

                oldDb.ReadExactly(buffer, 0, actualLen);

                //the pointer should always store the un-encrypted length...
                var nPtr = new MemDbPointer(oPtr.Id, RecordState.Fresh, oPtr.StateSetAt, oPtr.CreatedAt, oPtr.IsEncrypted, newDb.Position, oPtr.Length);
                freshMap.Add(nPtr);

                newDb.Write(buffer, 0, actualLen);
            }

            _freshMap.Flush();
        }
        #endregion

        #region delete fragmented files
        private void DeleteFragmetedFiles()
        {
            File.Delete(_fullMapPath);
            File.Delete(_fullDbPath);
        }
        #endregion

        #region rename temp defragmented files
        private void RenameTempDefragmentedFiles()
        {
            File.Move(_fullTempMapPath, _fullMapPath);
            File.Move(_fullTempDbPath, _fullDbPath);
        }
        #endregion
    }
}
