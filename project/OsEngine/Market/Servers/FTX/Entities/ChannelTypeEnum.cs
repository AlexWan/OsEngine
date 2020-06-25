using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace OsEngine.Market.Servers.FTX.Entities
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ChannelTypeEnum
    {
        [EnumMember(Value = "orderbook")]
        OrderBook,
        [EnumMember(Value = "trades")]
        Trades,
        [EnumMember(Value = "ticker")]
        Ticker,
        [EnumMember(Value = "fills")]
        Fills,
        [EnumMember(Value = "orders")]
        Orders
    }
}
