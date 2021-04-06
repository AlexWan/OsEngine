using Newtonsoft.Json;
using System;

namespace Kraken.WebSockets.Converters
{
    /// <summary>
    /// <see cref="JsonConverter"/> implementation for <see cref="Status"/> enum
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    internal sealed class StatusConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType == typeof(Status) || objectType == typeof(Status?);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return (((string)reader.Value).ToLower()) switch
            {
                "ok" => Status.Ok,
                "error" => Status.Error,
                _ => throw new InvalidOperationException($"Value '{reader.Value}' cannot be converted to type '{nameof(Status)}'"),
            };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            => throw new NotImplementedException();
    }
}
