using System;
using System.IO;

namespace HatTrick.MemDb
{
    #region [class] mem db binary serializer
    internal abstract class MemDbBinarySerializer
    {
        #region serialize to
        protected void Serialize(MemDbRecord record, BinaryWriter to)
        {
            to.Write(record.Id);
            to.Write(record.IsStale);
            to.Write(record.IsEncrypted);
            to.Write(record.Index);
            to.Write(record.MapIndex);
        }
        #endregion

        #region deserialize from
        protected void Deserialize(MemDbRecord record, BinaryReader from)
        {
            record.Id = from.ReadInt32();
            record.IsStale = from.ReadBoolean();
            record.IsEncrypted = from.ReadBoolean();
            record.Index = from.ReadInt32();
            record.MapIndex = from.ReadInt32();
        }
        #endregion
    }
    #endregion

    #region [class] mem db binary serializer of T
    internal class MemDbBinarySerializer<T> : MemDbBinarySerializer where T : class, new()
    {
        #region internals
        private IMemDbSerializer<T> _serializerOfT;
        #endregion

        #region constructors
        internal MemDbBinarySerializer(IMemDbSerializer<T> serializerOfT)
        {
            _serializerOfT = serializerOfT ?? throw new ArgumentNullException(nameof(serializerOfT));
        }
        #endregion

        #region serialize to
        internal virtual void Serialize(MemDbRecord<T> record, BinaryWriter to)
        {
            base.Serialize(record, to);
            _serializerOfT.SerializeTo(record.Value, to);
        }
        #endregion

        #region deserialize from
        internal virtual MemDbRecord<T> Deserialize(BinaryReader from)
        {
            var record = new MemDbRecord<T>();
            base.Deserialize(record, from);
            T value = _serializerOfT.DeserializeFrom(from);
            record.Value = value;
            return record;
        }
        #endregion
    }
    #endregion
}
