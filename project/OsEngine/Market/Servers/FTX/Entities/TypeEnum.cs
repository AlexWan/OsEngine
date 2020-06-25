using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.FTX.Entities
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TypeEnum
    {
        [EnumMember(Value = "pong")]
        Pong,
        [EnumMember(Value = "error")]
        Error,
        [EnumMember(Value = "subscribed")]
        Subscribed,
        [EnumMember(Value = "unsubscribed")]
        Unsubscribed,
        [EnumMember(Value = "info")]
        Info,
        [EnumMember(Value = "partial")]
        Partial,
        [EnumMember(Value = "update")]
        Update
    }
}
