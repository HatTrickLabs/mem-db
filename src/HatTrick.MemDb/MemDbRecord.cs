using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HatTrick.MemDb
{
    internal abstract class MemDbRecord
    {
        #region read only
        internal static readonly int Size = 14;
        #endregion

        #region interface
        internal int Id { get; set; }
        internal bool IsStale { get; set; }
        internal bool IsEncrypted { get; set; }
        internal int Index { get; set; }
        internal int MapIndex { get; set; }
        #endregion

        #region serialize to
        internal virtual void SerializeTo(BinaryWriter buffer)
        {
            buffer.Write(BitConverter.GetBytes(this.Id), 0, 4);
            buffer.Write(BitConverter.GetBytes(this.IsStale), 0, 1);
            buffer.Write(BitConverter.GetBytes(this.IsEncrypted), 0, 1);
            buffer.Write(BitConverter.GetBytes(this.Index), 0, 4);
            buffer.Write(BitConverter.GetBytes(this.MapIndex), 0, 4);
        }
        #endregion

        #region deserialize from
        internal virtual void DeserializeFrom(BinaryReader buffer)
        {
            byte[] buff = new byte[MemDbRecord.Size];
            buffer.Read(buff, 0, MemDbRecord.Size);

            this.Id = BitConverter.ToInt32(buff, 0);
            this.IsStale = BitConverter.ToBoolean(buff, 4);
            this.IsEncrypted = BitConverter.ToBoolean(buff, 5);
            this.Index = BitConverter.ToInt32(buff, 6);
            this.MapIndex = BitConverter.ToInt32(buff, 10);
        }
        #endregion
    }

    internal class MemDbRecord<T> : MemDbRecord where T: class, new()
    {
        #region internals
        private static ISerializationProvider<T> _serializer;

        private T _value;
        #endregion

        #region interface
        public T Value => _value;
        #endregion

        #region constructors
        internal MemDbRecord(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _value = value;
        }

        internal MemDbRecord()
        {
        }
        #endregion

        #region register serializer
        internal static void RegisterSerializer(ISerializationProvider<T> serializer)
        {
            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            _serializer = serializer;
        }
        #endregion

        #region serialize to
        internal override void SerializeTo(BinaryWriter buffer)
        {
            base.SerializeTo(buffer);
            _serializer.SerializeTo(_value, buffer);
        }
        #endregion

        #region deserialize from
        internal override void DeserializeFrom(BinaryReader buffer)
        {
            base.DeserializeFrom(buffer);
            _value = _serializer.DeserializeFrom(buffer);
        }
        #endregion

        #region deep copy value
        //TODO: we need a DeepCopyOfSet(T[] values) in order to avoid numerous alloc of stream object on LARGE data sets.
        internal static T DeepCopyOf(T value)
        {
            T rec = default(T);
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, true))
                {
                    _serializer.SerializeTo(value, writer);
                }
                int length = (int)ms.Position;
                ms.Position = 0;
                using (BinaryReader reader = new BinaryReader(ms, Encoding.UTF8, true))
                {
                    rec = _serializer.DeserializeFrom(reader);
                }
            }
            return rec;
        }

        internal static T[] DeepCopyOf(T[] values)
        {
            T[] newValues = new T[values.Length];
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, true))
                {
                    using (BinaryReader reader = new BinaryReader(ms, Encoding.UTF8, true))
                    {
                        for (int i = 0; i < values.Length; i++)
                        {
                            _serializer.SerializeTo(values[i], writer);
                            int length = (int)ms.Position;
                            ms.Position = 0;
                            newValues[i] = _serializer.DeserializeFrom(reader);
                            ms.Position = 0;
                        }
                    }
                }
            }
            return newValues;
        }
        #endregion
    }
}
