﻿using Newtonsoft.Json;
using System;

namespace Kraken.WebSockets.Converters
{
    /// <summary>
    /// Specialized converter for <see cref="OrderType"/>
    /// </summary>
    /// <seealso cref="JsonConverter" />
    internal sealed class OrderTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType == typeof(OrderType) || objectType == typeof(OrderType?);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            => throw new NotImplementedException();

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            => writer.WriteValue(Enum.GetName(typeof(OrderType), value).ToLower());
    }
}
