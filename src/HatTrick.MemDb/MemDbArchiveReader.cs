using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace HatTrick.InMemDb
{
    internal class MemDbArchiveReader<T> where T : class
    {
        #region internals
        private readonly string _archivePath;
        private readonly string _datasetName;

        private string _fullArchivePath;

        private IMemDbSerializer<T> _serializer;
        private IBinaryReadMemDbSerializer<T> _binReadSerializer;//optional impl
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
            if (serializer is IBinaryReadMemDbSerializer<T> binReadSer)
                _binReadSerializer = binReadSer;

            _encryptor = encryptor;

            _fullArchivePath = Path.Combine(_archivePath, $"htl.{datasetName}.zip");

            if (!File.Exists(_fullArchivePath))
                throw new ArgumentException($"Expected archive file does not exist: {_fullArchivePath}", nameof(archivePath));
        }
        #endregion

        #region read archive records
        public IEnumerable<MemDbRecord<T>> ReadArchiveRecords()
        {
            //TODO: refactor this down into safer chunks...Need to ensure the tmp files get cleaned
            //up in a finally block.
            using (ZipArchive zip = ZipFile.Open(_fullArchivePath, ZipArchiveMode.Read))
            {
                (string key, ZipArchiveEntry[] entries)[] sets = zip.Entries
                    .GroupBy(e => e.Name.Substring(0, 16))
                    .Select(g => (g.Key, g.ToArray()))
                    .ToArray();

                Array.Sort(sets, (a, b) => a.key.CompareTo(b.key));

                //should result in
                //{x, [ x.htl.datasetName.db.arch, x.htl.datasetName.map.arch ] }
                //{y, [ y.htl.datasetName.db.arch, y.htl.datasetName.map.arch ] }
                //{z, [ z.htl.datasetName.db.arch, z.htl.datasetName.map.arch ] }

                foreach (var set in sets)
                {
                    var mapEntry = set.entries.First(e => e.Comment == "map");
                    MemDbMap map = new MemDbMap("xxx", false);//this is just tmp in mem only, don't init to disk...

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
                            MemDbPointer ptr;
                            long cnt = 0;
                            Stream fsDb = dbReader.BaseStream;
                            for (int i = 0; i < map.Count; i++)
                            {
                                ptr = map[i];

                                if (ptr.IsEncrypted && !this.IsEncryptionReady)
                                {
                                    fsDb.Position += MemDbAESEncryptor.CalculateCryptoByteLength(ptr.Length);
                                    continue;
                                }

                                T value = null;
                                if (ptr.IsEncrypted)
                                {
                                    Span<byte> raw = _encryptor.Decrypt(fsDb, ptr.Length);
                                    value = _serializer.Deserialize(raw);
                                }
                                else
                                {
                                    //move deserialize into local func to take advantage of stackalloc within loop (although the yeild return may actually be enough to flush scope).
                                    value = this.DeserializeRecord(dbReader, ptr.Length);
                                    //value = _serializer.Deserialize(dbReader, ptr.Length);//T value
                                }

                                var record = new MemDbRecord<T>(ptr.Id, value, ptr.State, ptr.StateSetAt, ptr.IsEncrypted, -1, i);
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

        #region deserialize record
        private T DeserializeRecord(BinaryReader from, int length)
        {
            if (_binReadSerializer is not null)
                return _binReadSerializer.Deserialize(from);

            Span<byte> raw = length > 2048 ? new byte[length] : stackalloc byte[length];
            from.BaseStream.ReadExactly(raw);
            return _serializer.Deserialize(raw);
        }
        #endregion
    }
}
