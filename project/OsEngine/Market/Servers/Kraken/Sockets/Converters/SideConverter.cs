using Newtonsoft.Json;
using System;

namespace Kraken.WebSockets.Converters
{
    internal sealed class SideConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) 
            => objectType == typeof(Side) || objectType == typeof(Side?);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            => throw new NotImplementedException();

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            => writer.WriteValue(Enum.GetName(typeof(Side), value).ToLower());
    }
}
