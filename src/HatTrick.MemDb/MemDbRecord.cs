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

        //#region serialize to
        //internal virtual void SerializeTo(BinaryWriter buffer)
        //{
        //    buffer.Write(this.Id);
        //    buffer.Write(this.IsStale);
        //    buffer.Write(this.IsEncrypted);
        //    buffer.Write(this.Index);
        //    buffer.Write(this.MapIndex);
        //}
        //#endregion

        //#region deserialize from
        //internal virtual void DeserializeFrom(BinaryReader buffer)
        //{
        //    this.Id = buffer.ReadInt32();
        //    this.IsStale = buffer.ReadBoolean();
        //    this.IsEncrypted = buffer.ReadBoolean();
        //    this.Index = buffer.ReadInt32();
        //    this.MapIndex = buffer.ReadInt32();
        //}
        //#endregion
    }

    internal class MemDbRecord<T> : MemDbRecord where T: class, new()
    {
        #region internals
        private static IMemDbSerializer<T> _serializer;

        private T _value;
        #endregion

        #region interface
        public T Value
        {
            get { return _value; }
            internal set { _value = value; }
        }
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
    }
}
