using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HatTrick.MemDb
{
    public class MemDbDefragmenter<T> : IMemDbDefragmenter<T> where T : class, new()
    {
        #region internals
        private string _path;
        private string _name;

        private string _fullMapPath;
        private string _fullDbPath;

        private string _fullTempMapPath;
        private string _fullTempDbPath;

        private MemDbMap _map;
        private int _staleCount;
        private int _deletedCount;
        #endregion

        #region constructors
        public MemDbDefragmenter(string datasetName, string path)
        {
            if (string.IsNullOrEmpty(datasetName))
                throw new ArgumentException("arg must have a value.", nameof(datasetName));

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("arg must have a value.", nameof(path));

            _path = path;
            _name = datasetName;
            _fullDbPath = Path.Combine(path, $"htl.{datasetName}.db");
            _fullMapPath = Path.Combine(path, $"htl.{datasetName}.map");

            _fullTempMapPath = _fullMapPath + ".temp";
            _fullTempDbPath = _fullDbPath + ".temp";

            if (!Directory.Exists(path))
                throw new ArgumentException("No directory exists for provided path.", nameof(path));
        }
        #endregion

        #region defrag
        public void Defrag()
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
            _map = new MemDbMap(_fullMapPath);
            _map.InitializeExisting();
            _staleCount = _map.StaleCount;
            _deletedCount = _map.DeletedCount;
        }
        #endregion

        #region ensure available drive space
        private void EnsureAvailableDriveSpace()
        {
            //get file size of the non-stale map pointers (needed for the defragged map)
            //sizeof(int) + sizeof(uint) + (non-stalePointerCount * PointerByteSize)
            //the sizeof(int) is to account for the 32 bit int at the very beginning of the file (total pointer count)
            //the sizeof(uint) is to account for the 32 bit unsigned int at the beginning of the file (Last Identity)
            //TODO: these (outside of MemDbMap) size calcs are going to bite you in the ASS.
            long mapSize = sizeof(int) + sizeof(uint) + ((_map.Count - (_staleCount + _deletedCount)) * MemDbPointer.Size);

            //get file size of the non-stale...non-deleted db records
            long dbSize = _map.TotalFreshSize;

            //get available drive space 
            var directory = new DirectoryInfo(_path);
            var drive = new DriveInfo(directory.Root.Name);
            long spaceAvailable = drive.AvailableFreeSpace;

            //conservative when assuming standard block size of 4096 bytes.
            long spaceNeeded = dbSize + mapSize + (4096 * 2);

            if (spaceAvailable < spaceNeeded)
            {
                string ex = $"Insufficient disk space available on {drive.Name} ...{Environment.NewLine}"
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

            File.Create(_fullTempMapPath).Dispose();
            File.Create(_fullTempDbPath).Dispose();

        }
        #endregion

        #region write defragmented temp files
        private void WriteDefragmentedTempFiles()
        {
            MemDbMap originalMap = _map;
            List<MemDbPointer> pointers = new List<MemDbPointer>(_map.Count - (_staleCount + _deletedCount));

            int maxRecLength = originalMap.MaxFreshRecordSize;

            byte[] buffer = new byte[maxRecLength];

            //rebuild the db and map bypassing all stale and deleted records
            using var oldDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read);
            using var newDb = new FileStream(_fullTempDbPath, FileMode.Open, FileAccess.Write);

            for (int i = 0; i < originalMap.Count; i++)
            {
                MemDbPointer oPtr = originalMap[i];

                if (oPtr.State != RecordState.Fresh)
                    continue;

                oldDb.Position = oPtr.Position;
                oldDb.ReadExactly(buffer, 0, (oPtr.IsEncrypted) ? MemDbAESEncryptor.CalculateCryptoByteLength(oPtr.Length) : oPtr.Length);

                //the pointer should always store the un-encrypted length...
                var nPtr = new MemDbPointer(oPtr.Id, RecordState.Fresh, oPtr.IsEncrypted, (uint)newDb.Position, oPtr.Length);
                pointers.Add(nPtr);

                newDb.Write(buffer, 0, oPtr.Length);
            }

            //write the defragged map file
            var defraggedMap = MemDbMap.Create(_fullMapPath, _map.LastId, pointers);
            using var fs = new FileStream(_fullTempMapPath, FileMode.Open, FileAccess.Write);
            using var writer = new BinaryWriter(fs, Encoding.UTF8, true);
            defraggedMap.SerializeTo(writer);
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
