using System;
using System.IO;

namespace HatTrick.MemDb
{
    public class DigitalAssetSerializer : IMemDbSerializer<DigitalAsset>
    {
        public DigitalAssetSerializer()
        {
        }

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
    }
}
