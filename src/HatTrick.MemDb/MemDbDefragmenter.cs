using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HatTrick.MemDb
{
    public class MemDbDefragmenter
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
        #endregion

        #region constructors
        public MemDbDefragmenter(string path, string datasetName)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("arg must have a value.", nameof(path));

            if (string.IsNullOrEmpty(datasetName))
                throw new ArgumentException("arg must have a value.", nameof(datasetName));

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

            if (_map.Pointers.Any(p => p.IsStale) == false)
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
            _map = new MemDbMap();
            using (var fsMap = new FileStream(_fullMapPath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new BinaryReader(fsMap, Encoding.UTF8, true))
                {
                    _map.DeserializeFrom(reader);
                }
            }
            _staleCount = _map.Pointers.Count(p => p.IsStale);
        }
        #endregion

        #region ensure available drive space
        private void EnsureAvailableDriveSpace()
        {
            //get file length of the non-stale map pointers (needed for the defragged map)
            //(non-stalePointerCount * PointerByteSize) + sizeof(int)
            //the 4 is to account for the 32 bit int at the very beginning of the file (total pointer count)
            long mapLength = ((_map.Pointers.Count - _staleCount) * MemDbPointer.Size) + 4;

            //get file length of the non-stale db records
            long dbLengh = _map.Pointers.Where(p => p.IsStale == false).Sum(p => p.Length);

            //get available drive space 
            var directory = new DirectoryInfo(_path);
            var drive = new DriveInfo(directory.Root.Name);
            long spaceAvailable = drive.AvailableFreeSpace;

            //conservative when assuming standard block size of 4096 bytes.
            long spaceNeeded = dbLengh + mapLength + (4096 * 2);

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
            List<MemDbPointer> pointers = new List<MemDbPointer>(_map.Pointers.Count - _staleCount);

            int maxRecLength = originalMap.Pointers.Where(p => p.IsStale == false).Max(p => p.Length);

            byte[] buffer = new byte[maxRecLength];

            //rebuild the db and map bypassing all stale records
            using (var oldDb = new FileStream(_fullDbPath, FileMode.Open, FileAccess.Read))
            {
                using (var newDb = new FileStream(_fullTempDbPath, FileMode.Open, FileAccess.Write))
                {
                    for (int i = 0; i < originalMap.Pointers.Count; i++)
                    {
                        MemDbPointer oPtr = originalMap.Pointers[i];

                        if (oPtr.IsStale)
                            continue;

                        oldDb.Position = oPtr.Position;
                        oldDb.ReadExactly(buffer, 0, oPtr.Length);

                        var nPtr = new MemDbPointer(oPtr.Id, false, oPtr.IsEncrypted, (int)newDb.Position, oPtr.Length);
                        pointers.Add(nPtr);

                        newDb.Write(buffer, 0, oPtr.Length);
                    }
                }
            }

            //write the defragged map file
            var defraggedMap = MemDbMap.Create(pointers);
            using (var fs = new FileStream(_fullTempMapPath, FileMode.Open, FileAccess.Write))
            {
                using (var writer = new BinaryWriter(fs, Encoding.UTF8, true))
                {
                    defraggedMap.SerializeTo(writer);
                }
            }
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
