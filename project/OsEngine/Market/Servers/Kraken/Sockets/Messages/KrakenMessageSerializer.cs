using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Kraken.WebSockets.Messages
{
    public class KrakenMessageSerializer : IKrakenMessageSerializer
    {
        private readonly JsonSerializerSettings serializerSettings;

        public KrakenMessageSerializer()
        {
            serializerSettings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        public TKrakenMessage Deserialize<TKrakenMessage>(string json) 
            where TKrakenMessage : class, IKrakenMessage
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentNullException(nameof(json));
            }

            return JsonConvert.DeserializeObject<TKrakenMessage>(json, serializerSettings);
        }

        public string Serialize<TKrakenMessage>(TKrakenMessage message) 
            where TKrakenMessage : class, IKrakenMessage
        {
#pragma warning disable RECS0017 // Possible compare of value type with 'null'
            if (message == null)
#pragma warning restore RECS0017 // Possible compare of value type with 'null'
            {
                throw new ArgumentNullException(nameof(message));
            }

            return JsonConvert.SerializeObject(message, serializerSettings);
        }
    }
}
