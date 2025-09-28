using System;
using System.IO;
using System.Text;
using System.Threading;

namespace HatTrick.Data
{
    public class DigitalAssetBinarySerializer : IBinaryReadMemDbSerializer<DigitalAsset>
    {
        #region internals
        private int _serializerCount;
        private int _deserializeCount;
        #endregion

        #region interface
        public int SerializeCount => _serializerCount;
        public int DeserializeCount => _deserializeCount;
        #endregion

        #region ctors
        public DigitalAssetBinarySerializer()
        {
            _serializerCount = 0;
            _deserializeCount = 0;
        }
        #endregion

        #region serialize
        public void Serialize(DigitalAsset record, BinaryWriter to)
        {
            to.Write((int)record.AssetType);
            to.Write(record.Id);
            to.Write(record.Name);
            to.Write(record.Directory);
            to.Write(record.Created.ToBinary());
            to.Write(record.LastAccess.ToBinary());
            to.Write(record.LastWrite.ToBinary());
            to.Write(record.Length);
            to.Write(record.Imported.ToBinary());
            to.Write(record.XXHash);

            Interlocked.Increment(ref _serializerCount);
        }

        public byte[] Serialize(DigitalAsset record)
        {
            int capacity = sizeof(int) + sizeof(long) + 255 + (sizeof(long) * 5) + sizeof(ulong);

            byte[] raw = null;
            using (var ms = new MemoryStream(capacity))
            {
                using (var writer = new BinaryWriter(ms, Encoding.UTF8, true))
                {
                    this.Serialize(record, writer);
                }
                raw = ms.ToArray();
            }
            return raw;
        }
        #endregion

        #region deserialize
        public DigitalAsset Deserialize(BinaryReader from)
        {
            var type = (DigitalAssetType)from.ReadInt32();
            var record = DigitalAsset.CreateNew(type);

            record.Id = from.ReadInt64();
            record.Name = from.ReadString();
            record.Directory = from.ReadString();
            record.Created = DateTime.FromBinary(from.ReadInt64());
            record.LastAccess = DateTime.FromBinary(from.ReadInt64());
            record.LastWrite = DateTime.FromBinary(from.ReadInt64());
            record.Length = from.ReadInt64();
            record.Imported = DateTime.FromBinary(from.ReadInt64());
            record.XXHash = from.ReadUInt64();

            Interlocked.Increment(ref _deserializeCount);

            return record;
        }

        public DigitalAsset Deserialize(ReadOnlySpan<byte> from)
        {
            DigitalAsset record = null;
            using (var ms = new MemoryStream(from.ToArray()))
            {
                using (var reader = new BinaryReader(ms))
                {
                    record = this.Deserialize(reader);
                }
            }
            return record;
        }
        #endregion
    }
}
