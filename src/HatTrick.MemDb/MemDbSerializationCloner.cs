using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.MemDb
{
    internal class MemDbSerializationCloner<T> : IMemDbCloner<T> where T : class, new()
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
        public T DeepCopy(T value)
        {
            T rec = default(T);
            using MemoryStream ms = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                _serializer.Serialize(value, writer);
            }
            int length = (int)ms.Position;
            ms.Position = 0;
            using (BinaryReader reader = new BinaryReader(ms, Encoding.UTF8, true))
            {
                rec = _serializer.Deserialize(reader);
            }
            return rec;
        }

        public T[] DeepCopy(IList<T> values)
        {
            T[] newValues = new T[values.Count];
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, true);
            using BinaryReader reader = new BinaryReader(ms, Encoding.UTF8, true);
            for (int i = 0; i < values.Count; i++)
            {
                _serializer.Serialize(values[i], writer);
                int length = (int)ms.Position;
                ms.Position = 0;
                newValues[i] = _serializer.Deserialize(reader);
                ms.Position = 0;
            }
            return newValues;
        }
        #endregion
    }
}
