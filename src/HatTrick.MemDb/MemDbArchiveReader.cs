using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace HatTrick.InMemDb
{
    internal class MemDbArchiveReader<T> where T : class
    {
        #region internals
        private readonly string _datasetName;
        private readonly string _archivePath;

        private string _fullZipArchivePath;

        private IMemDbSerializer<T> _serializer;
        private IBinaryReadMemDbSerializer<T> _binReadSerializer;//optional impl
        private IMemDbEncryptor _encryptor;
        #endregion

        #region interface
        public bool IsEncryptionReady => _encryptor is not null;
        #endregion

        #region ctors
        internal MemDbArchiveReader(MemDbConfiguration<T> config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            if (!config.ShouldArchive)
                throw new InvalidOperationException($"Configuration for provided dataset '{config.DatasetName}' is not configured to archive on defrag.");

            //the encryptor can be null.

            _datasetName = config.DatasetName;
            _archivePath = config.ArchivePath;

            _serializer = config.GetSerializer();

            if (_serializer is IBinaryReadMemDbSerializer<T> binReadSerializer)
                _binReadSerializer = binReadSerializer;

            _encryptor = config.GetEncryptor();

            _fullZipArchivePath = config.GetZipArchiveFullFilePath();

            if (!File.Exists(_fullZipArchivePath))
                throw new InvalidOperationException($"Expected archive file does not exist '{_fullZipArchivePath}'");
        }
        #endregion

        #region read archive
        public IEnumerable<MemDbRecord<T>> ReadArchive()
        {
            using ZipArchive zip = ZipFile.Open(_fullZipArchivePath, ZipArchiveMode.Read);

            (string key, ZipArchiveEntry[] entries)[] sets = this.ResolveZipArchiveFileSets(zip);

            foreach (var set in sets)
            {
                var mapEntry = set.entries.First(e => e.Comment == "map");

                //this is just tmp in-mem only, don't init to disk...
                //simply give the ctor a bunk path, do not initialize and never flush
                MemDbMap map = new MemDbMap("xxx", false, _encryptor);
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

                var enumerator = this.EmitArchiveRecords(tmpArchFilePath, map);
                foreach (var rec in enumerator)
                {
                    yield return rec;
                }

                File.Delete(tmpArchFilePath);
            }
        }
        #endregion

        #region resolve zip archive file sets
        private (string key, ZipArchiveEntry[] enries)[] ResolveZipArchiveFileSets(ZipArchive zip)
        {
            (string key, ZipArchiveEntry[] entries)[] sets = zip.Entries
                //first 21 is timestamp formated as: yyyyMMdd_HHmm_ss_ffff
                .GroupBy(e => e.Name.Substring(0, MemDbConfiguration.ArchiveTimestampFormat.Length))
                .Select(g => (g.Key, g.ToArray()))
                .ToArray();

            Array.Sort(sets, (a, b) => a.key.CompareTo(b.key));

            //should result in
            //{yyyyMMdd_HHmm_ss_ffff, [ yyyyMMdd_HHmm_ss_ffff.htl.datasetName.db.arch, yyyyMMdd_HHmm_ss_ffff.htl.datasetName.map.arch ] }
            //{yyyyMMdd_HHmm_ss_ffff, [ yyyyMMdd_HHmm_ss_ffff.htl.datasetName.db.arch, yyyyMMdd_HHmm_ss_ffff.htl.datasetName.map.arch ] }
            //{yyyyMMdd_HHmm_ss_ffff, [ yyyyMMdd_HHmm_ss_ffff.htl.datasetName.db.arch, yyyyMMdd_HHmm_ss_ffff.htl.datasetName.map.arch ] }

            return sets;
        }
        #endregion

        #region emit archive records
        private IEnumerable<MemDbRecord<T>> EmitArchiveRecords(string tmpArchFilePath, MemDbMap map)
        {
            using var dbStream = new FileStream(tmpArchFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
            using var dbReader = new BinaryReader(dbStream);

            MemDbPointer ptr;
            Stream fsDb = dbReader.BaseStream;

            for (int i = 0; i < map.Count; i++)
            {
                ptr = map[i];

                if (ptr.IsEncrypted && !this.IsEncryptionReady)
                {
                    fsDb.Position += _encryptor.GetEncryptedLength(ptr.Length);
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
                }

                var record = new MemDbRecord<T>(ptr.Id, value, ptr.State, ptr.StateSetAt, ptr.CreatedAt, ptr.IsEncrypted, -1, i);
                yield return record;
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
