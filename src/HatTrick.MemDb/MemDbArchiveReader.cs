using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace HatTrick.Data
{
    internal class MemDbArchiveReader<T> where T : class
    {
        #region internals
        private readonly string _datasetName;
        private readonly string _archivePath;

        private string _fullZipArchivePath;

        private IMemDbSerializer<T> _serializer;
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

            _encryptor = config.GetEncryptor();

            _fullZipArchivePath = config.GetZipArchiveFilePath();

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
                string tmpBackupFilePath = Path.Combine(_archivePath, dbEntry.Name);
                dbEntry.ExtractToFile(tmpBackupFilePath, true);

                var enumerator = this.EmitArchiveRecords(tmpBackupFilePath, map);
                foreach (var rec in enumerator)
                {
                    yield return rec;
                }

                File.Delete(tmpBackupFilePath);
            }
        }
        #endregion

        #region resolve zip archive file sets
        private (string key, ZipArchiveEntry[] enries)[] ResolveZipArchiveFileSets(ZipArchive zip)
        {
            int prefixLength = "htl.".Length + MemDbConfiguration.ArchiveTimestampFormat.Length;

            (string key, ZipArchiveEntry[] entries)[] sets = zip.Entries
                .GroupBy(e => e.Name.Substring(0, prefixLength))
                .Select(g => (g.Key, g.ToArray()))
                .ToArray();

            Array.Sort(sets, (a, b) => a.key.CompareTo(b.key));

            //should result in
            //{yyyyMMdd_HHmm_ss_ffff, [ htl.yyyyMMdd.HHmm.ss.fff.datasetName.db.bak, htl.yyyyMMdd.HHmm.ss.fff.datasetName.map.bak ] }
            //{yyyyMMdd_HHmm_ss_ffff, [ htl.yyyyMMdd.HHmm.ss.fff.datasetName.db.bak, htl.yyyyMMdd.HHmm.ss.fff.datasetName.map.bak ] }
            //{yyyyMMdd_HHmm_ss_ffff, [ htl.yyyyMMdd.HHmm.ss.fff.datasetName.db.bak, htl.yyyyMMdd.HHmm.ss.fff.datasetName.map.bak ] }

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
                    Span<byte> raw = _encryptor.DecryptFrom(fsDb, ptr.Length);
                    value = _serializer.Deserialize(raw);
                }
                else
                {
                    //move deserialize into local func to take advantage of stackalloc within loop (although the yeild return may actually be enough to flush scope).
                    value = this.DeserializeRecord(dbReader, ptr.Length);
                }

                var record = new MemDbRecord<T>(ptr, value, i);
                yield return record;
            }
        }
        #endregion

        #region deserialize record
        private T DeserializeRecord(BinaryReader from, int length)
        {
            return _serializer.Deserialize(from, length);
        }
        #endregion
    }
}
