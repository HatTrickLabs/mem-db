using System;
using System.IO;
using System.Text;

namespace HatTrick.InMemDb
{
    public class DigitalAssetSerializer : IMemDbSerializer<DigitalAsset>
    {
        #region ctors
        public DigitalAssetSerializer()
        {
        }
        #endregion

        #region serialize
        public void Serialize(DigitalAsset record, BinaryWriter to)
        {
            to.Write(record.Id);
            to.Write(record.Name);
            to.Write(record.Directory);
            to.Write(record.Created.ToBinary());
            to.Write(record.LastAccess.ToBinary());
            to.Write(record.LastWrite.ToBinary());
            to.Write(record.Length);
            to.Write(record.Imported.ToBinary());
            to.Write(record.XXHash);
        }

        public byte[] Serialize(DigitalAsset record)
        {
            int capacity = sizeof(uint) + (sizeof(long) * 6) + 255;
            byte[] utf8 = null;
            using (var ms = new MemoryStream(capacity))
            {
                using (var writer = new BinaryWriter(ms, Encoding.UTF8, true))
                {
                    this.Serialize(record, writer);
                }
                utf8 = new byte[ms.Length];
                ms.Position = 0;
                ms.Read(utf8, 0, utf8.Length);
            }
            return utf8;
        }
        #endregion

        #region deserialize
        public DigitalAsset Deserialize(BinaryReader from, int length)
        {
            var record = new DigitalAsset();
            record.Id = from.ReadUInt32();
            record.Name = from.ReadString();
            record.Directory = from.ReadString();
            record.Created = DateTime.FromBinary(from.ReadInt64());
            record.LastAccess = DateTime.FromBinary(from.ReadInt64());
            record.LastWrite = DateTime.FromBinary(from.ReadInt64());
            record.Length = from.ReadInt64();
            record.Imported = DateTime.FromBinary(from.ReadInt64());
            record.XXHash = from.ReadUInt64();

            return record;
        }

        public DigitalAsset Deserialize(ReadOnlySpan<byte> from)
        {
            //TODO: this is extremely inefficient...refactor to just smash each prop right out of the readonly span.
            DigitalAsset record = null;
            using (var ms = new MemoryStream(from.ToArray()))
            {
                using (var reader = new BinaryReader(ms))
                {
                    record = this.Deserialize(reader, from.Length);
                }
            }
            return record;
        }
        #endregion
    }
}
