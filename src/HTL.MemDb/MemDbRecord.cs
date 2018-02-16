using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HatTrick.MemDb
{
    public abstract class MemDbRecord// : IMemDbSerializable
    {
        #region read only
        public static readonly int BaseRecordLength = 14;
        #endregion

        #region interface
        public int Id { get; internal set; }
        internal bool IsStale { get; set; }
        public bool IsEncrypted { get; internal set; }
        internal int Index { get; set; }
        internal int MapIndex { get; set; }

        //not serialized...
        internal Guid SyncRef { get; set; }
        #endregion

        #region serialization
        public virtual void SerializeTo(Stream buffer)
        {
            buffer.Write(BitConverter.GetBytes(this.Id), 0, 4);
            buffer.Write(BitConverter.GetBytes(this.IsStale), 0, 1);
            buffer.Write(BitConverter.GetBytes(this.IsEncrypted), 0, 1);
            buffer.Write(BitConverter.GetBytes(this.Index), 0, 4);
            buffer.Write(BitConverter.GetBytes(this.MapIndex), 0, 4);
        }

        public virtual void DeserializeFrom(Stream buffer, int length)
        {
            byte[] buff = new byte[MemDbRecord.BaseRecordLength];
            buffer.Read(buff, 0, MemDbRecord.BaseRecordLength);

            this.Id = BitConverter.ToInt32(buff, 0);
            this.IsStale = BitConverter.ToBoolean(buff, 4);
            this.IsEncrypted = BitConverter.ToBoolean(buff, 5);
            this.Index = BitConverter.ToInt32(buff, 6);
            this.MapIndex = BitConverter.ToInt32(buff, 10);
        }
        #endregion

        #region deep copy
        public static T DeepCopy<T>(T target) where T : IMemDbSerializable, new()
        {
            T rec = new T();
            using (MemoryStream ms = new MemoryStream())
            {
                target.SerializeTo(ms);
                int length = (int)ms.Position;
                ms.Position = 0;
                rec.DeserializeFrom(ms, length);
            }
            
            return rec;
        }

        public static T[] DeepCopy<T>(T[] target) where T : IMemDbSerializable, new()
        {
            T[] results = new T[target.Length];
            for (int i = 0; i < target.Length; i++)
            {
                results[i] = MemDbRecord.DeepCopy<T>(target[i]);
            }
            return results;
        }
        #endregion
    }
}
