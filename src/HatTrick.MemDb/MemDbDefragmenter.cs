using System;
using System.IO;

namespace HatTrick.MemDb
{
    public class MemDbDefragmenter<T> where T : class, new()
    {
        #region internals
        private string _path;
        private string _name;

        private string _fullMapPath;
        private string _fullDbPath;
        #endregion

        #region constructors
        private MemDbDefragmenter(string path, string datasetName, ISerializationProvider<T> serializer)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("arg must have a value.", nameof(path));

            if (string.IsNullOrEmpty(datasetName))
                throw new ArgumentException("arg must have a value.", nameof(datasetName));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            _path = path;
            _name = datasetName;
            _fullDbPath = Path.Combine(path, $"htl.{datasetName}.db");
            _fullMapPath = Path.Combine(path, $"htl.{datasetName}.map");

            if (!Directory.Exists(path))
                throw new ArgumentException("No directory exists for provided path.", nameof(path));

            MemDbRecord<T>.RegisterSerializer(serializer);
        }
        #endregion

        #region open
        public static MemDbDefragmenter<T> Open(string path, string name)
        {
            return new MemDbDefragmenter<T>(path, name, null);//TODO: need some type of default serializer (JSON) after update to .net 8.0
        }

        public static MemDbDefragmenter<T> Open(string path, string name, ISerializationProvider<T> serializer)
        {
            return new MemDbDefragmenter<T>(path, name, serializer);
        }
        #endregion

        #region defrag
        public void Defrag()
        {
            if (!File.Exists(_fullDbPath))
                throw new InvalidOperationException("No file exists at MemDb data file path: " + _fullDbPath);

            if (!File.Exists(_fullMapPath))
                throw new InvalidOperationException("No file exists at MemDb map file path: " + _fullMapPath);
        }
        #endregion
    }
}
