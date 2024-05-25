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
            ops.MaxDepth = 16;
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
        #endregion

        #region deserialize
        public T Deserialize(BinaryReader from, int length)
        {
            Span<byte> raw = stackalloc byte[length];
            _ = from.Read(raw);
            T val = JsonSerializer.Deserialize<T>(raw, _options);
            return val;
        }
        #endregion        
    }
}
