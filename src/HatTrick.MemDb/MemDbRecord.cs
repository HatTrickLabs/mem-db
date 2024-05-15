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
            buffer.Write(this.Id);
            buffer.Write(this.IsStale);
            buffer.Write(this.IsEncrypted);
            buffer.Write(this.Index);
            buffer.Write(this.MapIndex);
        }
        #endregion

        #region deserialize from
        internal virtual void DeserializeFrom(BinaryReader buffer)
        {
            this.Id = buffer.ReadInt32();
            this.IsStale = buffer.ReadBoolean();
            this.IsEncrypted = buffer.ReadBoolean();
            this.Index = buffer.ReadInt32();
            this.MapIndex = buffer.ReadInt32();
        }
        #endregion
    }

    internal class MemDbRecord<T> : MemDbRecord where T: class, new()
    {
        #region internals
        private static IMemDbSerializer<T> _serializer;

        private T _value;
        #endregion

        #region interface
        public T Value => _value;
        #endregion

        #region constructors
        internal MemDbRecord(T value, bool isEncrypted)
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
        internal static void RegisterSerializer(IMemDbSerializer<T> serializer)
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

        internal static T[] DeepCopyOf(IList<T> values)
        {
            T[] newValues = new T[values.Count];
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, true))
                {
                    using (BinaryReader reader = new BinaryReader(ms, Encoding.UTF8, true))
                    {
                        for (int i = 0; i < values.Count; i++)
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
