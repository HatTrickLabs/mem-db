using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace HatTrick.InMemDb
{
    internal class MemDbArchiveReader<T> where T : class, new()
    {
        #region internals
        private readonly string _archivePath;
        private readonly string _datasetName;

        private string _fullArchivePath;

        private IMemDbSerializer<T> _serializer;
        private IMemDbEncryptor _encryptor;
        #endregion

        #region interface
        public bool IsEncryptionReady => _encryptor is not null;
        #endregion

        #region constructors
        internal MemDbArchiveReader(string datasetName, string archivePath, IMemDbSerializer<T> serializer)
            : this(datasetName, archivePath, serializer, null)
        { }

        public MemDbArchiveReader(string datasetName, string archivePath, IMemDbSerializer<T> serializer, IMemDbEncryptor encryptor)
        {
            if (string.IsNullOrEmpty(datasetName))
                throw new ArgumentException("arg must have a value.", nameof(datasetName));

            if (string.IsNullOrWhiteSpace(archivePath))
                throw new ArgumentException("arg must have a value.", nameof(archivePath));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            //the encryptor can be null.

            _archivePath = archivePath;
            _datasetName = datasetName;

            _serializer = serializer;
            _encryptor = encryptor;

            _fullArchivePath = Path.Combine(_archivePath, $"htl.{datasetName}.zip");

            if (!File.Exists(_fullArchivePath))
                throw new ArgumentException($"Expected archive file does not exist: {_fullArchivePath}", nameof(archivePath));
        }
        #endregion

        #region read archive records
        public IEnumerable<MemDbRecord<T>> ReadArchiveRecords()
        {
            using (ZipArchive zip = ZipFile.Open(_fullArchivePath, ZipArchiveMode.Read))
            {
                (string key, ZipArchiveEntry[] entries)[] sets = zip.Entries
                    .GroupBy(e => e.Name.Substring(0, 16))
                    .Select(g => (g.Key, g.ToArray()))
                    .ToArray();

                Array.Sort(sets, (a, b) => a.key.CompareTo(b.key));

                //should result in
                //{ x.htl.x.db.arch, x.htl.x.map.arch }
                //{ y.htl.y.db.arch, y.htl.y.map.arch }
                //{ z.htl.z.db.arch, z.htl.z.map.arch }

                foreach (var set in sets)
                {
                    var mapEntry = set.entries.First(e => e.Comment == "map");
                    MemDbMap map = new MemDbMap("xxx");

                    using (var mapStream = mapEntry.Open())
                    {
                        using (var mapReader = new BinaryReader(mapStream))
                        {
                            map.DeserializeFrom(mapReader);
                        }
                    }

                    var dbEntry = set.entries.First(e => e.Comment == "db");
                    string tmpArchFilePath = Path.Combine(_archivePath, dbEntry.Name);
                    dbEntry.ExtractToFile(tmpArchFilePath, true);
                    using (var dbStream = new FileStream(tmpArchFilePath, FileMode.Open, FileAccess.Read))
                    {
                        using (var dbReader = new BinaryReader(dbStream))
                        {
                            MemDbPointer pointer;
                            long cnt = 0;
                            Stream fsDb = dbReader.BaseStream;
                            for (int i = 0; i < map.Count; i++)
                            {
                                pointer = map[i];

                                if (pointer.IsEncrypted && !this.IsEncryptionReady)
                                {
                                    fsDb.Position += MemDbAESEncryptor.CalculateCryptoByteLength(pointer.Length);
                                    continue;
                                }

                                T value = null;
                                if (pointer.IsEncrypted)
                                {
                                    Span<byte> raw = _encryptor.Decrypt(fsDb, pointer.Length);
                                    value = _serializer.Deserialize(raw);
                                }
                                else
                                {
                                    value = _serializer.Deserialize(dbReader, pointer.Length);//T value
                                }

                                var record = new MemDbRecord<T>(pointer.Id, value, pointer.State, pointer.IsEncrypted, -1, i);
                                cnt += 1;
                                yield return record;
                            }
                        }
                    }
                    File.Delete(tmpArchFilePath);
                }
            }
        }
        #endregion
    }
}
