using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HatTrick.InMemDb
{
    #region [class] mem db json serializer
    public class MemDbJsonSerializer
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
        protected static bool SerializerRegistered(Type ofType, out MemDbJsonSerializer serializer)
        {
            serializer = _instances.Find(i => i.GetType().GetGenericArguments()[0] == ofType);
            return serializer is not null;
        }
        #endregion

        #region get serializer
        protected static MemDbJsonSerializer GetSerializer(Type ofType)
        {
            MemDbJsonSerializer serializer = _instances.Find(i => i.GetType().GetGenericArguments()[0] == ofType);

            if (serializer is null)
                throw new ArgumentException("No serializer registerd for provided type: " + ofType.Name);

            return serializer;
        }
        #endregion
    }
    #endregion

    #region [class] mem db json serializer of T
    public class MemDbJsonSerializer<T> : MemDbJsonSerializer, IMemDbSerializer<T> where T : class
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
        private static MemDbJsonSerializer<T> CreateInstance(params JsonConverter[] converters)
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
            MemDbJsonSerializer serializer = null;
            if (!MemDbJsonSerializer.SerializerRegistered(typeof(T), out serializer))
                serializer = MemDbJsonSerializer<T>.CreateInstance(/*** Default converters??? ***/);

            return (MemDbJsonSerializer<T>)serializer;
        }
        #endregion

        #region apply converter for [This is NOT thread safe]
        public void ApplyConverterFor<U>(JsonConverter<U> converter) where U : class
        {
            //build a copy of existing options
            var options = new JsonSerializerOptions(_options);

            //remove any existing converters for the same typeof(T)
            JsonConverter c = null;
            for (int i = 0; i < options.Converters.Count; i++)
            {
                if (options.Converters[i].CanConvert(typeof(T)))
                {
                    c = options.Converters[i];
                    break;
                }
            }

            if (c is not null)
                options.Converters.Remove(c);

            //replace with .. or add the applied converter
            options.Converters.Add(converter);

            //hot swap new options 
            _options = options;
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
