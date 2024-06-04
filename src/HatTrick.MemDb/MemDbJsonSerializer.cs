using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HatTrick.MemDb
{
    #region [class] mem db json serializer
    internal class MemDbJsonSerializer
    {
        #region static internals
        private static List<MemDbJsonSerializer> _instances;
        private static JsonSerializerOptions _defaultOptions;
        #endregion

        #region interface
        protected static JsonSerializerOptions DefaultOpions => _defaultOptions;
        #endregion

        #region static ctor
        static MemDbJsonSerializer()
        {
            _instances = new List<MemDbJsonSerializer>();

            var ops = new JsonSerializerOptions();
            ops.PropertyNameCaseInsensitive = false;
            ops.MaxDepth = 32;
            ops.IncludeFields = false;
            ops.IgnoreReadOnlyProperties = true;
            ops.IgnoreReadOnlyFields = true;
            ops.AllowTrailingCommas = true;
            ops.WriteIndented = false;
            ops.NumberHandling = JsonNumberHandling.Strict;
            _defaultOptions = ops;
        }
        #endregion

        #region register
        protected static void Register(MemDbJsonSerializer serializer)
        {
            Type ofType = serializer.GetType().GetGenericArguments()[0];
            int idx = _instances.FindIndex(i => i.GetType().GetGenericArguments()[0] == ofType);

            if (idx == -1)
                _instances.Add(serializer);
            else
                _instances[idx] = serializer;
        }
        #endregion

        #region serializer registered
        protected static bool SerializerRegistered(Type ofType)
        {
            return _instances.Exists(i => i.GetType().GetGenericArguments()[0] == ofType);
        }
        #endregion

        #region get serializer
        protected static MemDbJsonSerializer GetSerializer(Type ofType)
        {
            int idx = _instances.FindIndex(i => i.GetType().GetGenericArguments()[0] == ofType);
            if (idx == -1)
                throw new ArgumentException("No serializer registerd for provided type: " + ofType.Name);

            return _instances[idx];
        }
        #endregion
    }
    #endregion

    #region [class] mem db json serializer of T
    internal class MemDbJsonSerializer<T> : MemDbJsonSerializer, IMemDbSerializer<T> where T : class, new()
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
            //clone the defaults then add converters
            var ops = new JsonSerializerOptions(MemDbJsonSerializer.DefaultOpions);

            if (converters is not null && converters.Length > 0)
            {
                foreach (var c in converters)
                    ops.Converters.Add(c);
            }

            var serializer = new MemDbJsonSerializer<T>(ops);
            MemDbJsonSerializer.Register(serializer);

            return serializer;
        }
        #endregion

        #region get instance
        public static MemDbJsonSerializer<T> GetInstance()
        {
            Type ofType = typeof(T);
            if (!MemDbJsonSerializer.SerializerRegistered(typeof(T)))
                return MemDbJsonSerializer<T>.CreateInstance();

            var serializer = MemDbJsonSerializer.GetSerializer(typeof(T));
            return (MemDbJsonSerializer<T>)serializer;
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
    #endregion
}
