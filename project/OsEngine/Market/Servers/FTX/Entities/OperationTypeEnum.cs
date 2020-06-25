using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace OsEngine.Market.Servers.FTX.Entities
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum OperationTypeEnum
    {
        [EnumMember(Value = "ping")]
        Ping,
        [EnumMember(Value = "login")]
        Login,
        [EnumMember(Value = "subscribe")]
        Subscribe,
        [EnumMember(Value = "unsubscribe")]
        Unsubscribe
    }
}
