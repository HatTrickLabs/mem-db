using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HatTrick.InMemDb
{
    public class IDigitalAssetConverter<T> : JsonConverter<IDigitalAsset> where T : IDigitalAsset
    {
        public override IDigitalAsset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            T account = JsonSerializer.Deserialize<T>(ref reader, options);
            return account;
        }

        public override void Write(Utf8JsonWriter writer, IDigitalAsset value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, (T)value, options);
        }
    }
}
