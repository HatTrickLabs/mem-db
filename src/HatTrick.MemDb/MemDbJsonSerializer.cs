using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HatTrick.MemDb
{
    public class MemDbJsonSerializer<T> : IMemDbSerializer<T> where T : class, new()
    {
        #region internals
        private MemDbJsonSerializer<T> _instance;
        private JsonSerializerOptions _options;
        #endregion

        #region ctors
        private MemDbJsonSerializer(JsonSerializerOptions options)
        {
            _options = options;
        }
        #endregion

        #region create instance
        public static MemDbJsonSerializer<T> CreateInstance(params JsonConverter[] converters)
        {
            var ops = new JsonSerializerOptions();
            ops.PropertyNameCaseInsensitive = false;
            ops.MaxDepth = 32;
            ops.IncludeFields = false;
            ops.IgnoreReadOnlyProperties = true;
            ops.IgnoreReadOnlyFields = true;
            ops.AllowTrailingCommas = true;
            ops.WriteIndented = false;
            ops.NumberHandling = JsonNumberHandling.Strict;

            if (converters is not null)
            {
                foreach (var c in converters)
                {
                    ops.Converters.Add(c);
                }
            }

            return new MemDbJsonSerializer<T>(ops);
        }
        #endregion

        #region serialize
        public void Serialize(T record, BinaryWriter to)
        {
            JsonSerializer.Serialize(to.BaseStream, record, _options);
        }

        public byte[] Serialize(T record)
        {
            byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes<T>(record, _options);
            return utf8;
        }
        #endregion

        #region deserialize
        public T Deserialize(BinaryReader from, int length)
        {
            Span<byte> raw = length <= 2048 ? stackalloc byte[length] : new byte[length];
            from.BaseStream.ReadExactly(raw);
            T val = this.Deserialize(raw);
            return val;
        }

        public T Deserialize(ReadOnlySpan<byte> from)
        {
            T val = JsonSerializer.Deserialize<T>(from, _options);
            return val;
        }
        #endregion        
    }
}
