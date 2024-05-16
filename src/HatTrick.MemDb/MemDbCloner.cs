using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HatTrick.MemDb
{
    #region memd db  record cloner of T
    internal class MemDbCloner<T> where T : class, new()
    {
        #region internals
        private IMemDbSerializer<T> _serializer;
        #endregion

        #region constructors
        internal MemDbCloner(IMemDbSerializer<T> serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }
        #endregion

        #region deep copy value
        internal T DeepCopyOf(T value)
        {
            T rec = default(T);
            using MemoryStream ms = new MemoryStream();
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
            return rec;
        }

        internal T[] DeepCopyOf(IList<T> values)
        {
            T[] newValues = new T[values.Count];
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, true);
            using BinaryReader reader = new BinaryReader(ms, Encoding.UTF8, true);
            for (int i = 0; i < values.Count; i++)
            {
                _serializer.SerializeTo(values[i], writer);
                int length = (int)ms.Position;
                ms.Position = 0;
                newValues[i] = _serializer.DeserializeFrom(reader);
                ms.Position = 0;
            }
            return newValues;
        }
        #endregion
    }
    #endregion
}
