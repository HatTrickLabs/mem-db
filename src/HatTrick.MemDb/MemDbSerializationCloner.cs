using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HatTrick.InMemDb
{
    internal class MemDbSerializationCloner<T> : IMemDbCloner<T> where T : class
    {
        #region internals
        private IMemDbSerializer<T> _serializer;
        #endregion

        #region constructors
        internal MemDbSerializationCloner(IMemDbSerializer<T> serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }
        #endregion

        #region deep copy
        //public T DeepCopy(T value)
        //{
        //    T rec = default(T);
        //    using MemoryStream ms = new MemoryStream();
        //    using (BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, true))
        //    {
        //        _serializer.Serialize(value, writer);
        //    }
        //    int length = (int)ms.Position;
        //    ms.Position = 0;
        //    using (BinaryReader reader = new BinaryReader(ms, Encoding.UTF8, true))
        //    {
        //        rec = _serializer.Deserialize(reader, length);
        //    }
        //    return rec;
        //}

        public T DeepCopy(T value)
        {
            byte[] raw = _serializer.Serialize(value);
            T rec = _serializer.Deserialize(raw);
            return rec;
        }

        public T[] DeepCopy(IList<T> values)
        {
            int cnt = values?.Count ?? 0;

            if (cnt == 0)
                return Array.Empty<T>();

            T[] clones = new T[cnt];
            for (int i = 0; i < cnt; i++)
            {
                clones[i] = this.DeepCopy(values[i]);
            }

            return clones;
        }

        //public T[] DeepCopy(IList<T> values)
        //{
        //    T[] newValues = new T[values.Count];
        //    using MemoryStream ms = new MemoryStream();
        //    using BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, true);
        //    using BinaryReader reader = new BinaryReader(ms, Encoding.UTF8, true);
        //    for (int i = 0; i < values.Count; i++)
        //    {
        //        _serializer.Serialize(values[i], writer);
        //        int length = (int)ms.Position;
        //        ms.Position = 0;
        //        newValues[i] = _serializer.Deserialize(reader, length);
        //        ms.Position = 0;
        //    }
        //    return newValues;
        //}
        #endregion
    }
}
