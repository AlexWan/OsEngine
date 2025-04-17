using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.AE.Json
{
    public class WebSocketAccountsMessage : WebSocketMessageBase
    {
        [JsonProperty("accounts")] public List<Account> Accounts { get; set; }

        public WebSocketAccountsMessage()
        {
            Type = "Accounts"; // Automatically set message type
        }
    }

    public class Account
    {
        [JsonProperty("account")] public string AccountNumber { get; set; }

        [JsonProperty("money")] public decimal Money { get; set; }

        [JsonProperty("gm")] public decimal GuaranteeMargin { get; set; }

        [JsonProperty("money_free")] public decimal FreeMoney { get; set; }

        [JsonProperty("fee")] public decimal Fee { get; set; }

        [JsonProperty("positions")] public List<Position> Positions { get; set; }

        [JsonProperty("orders")] public List<Order> Orders { get; set; }
    }

    public class Position
    {
        [JsonProperty("ticker")] public string Ticker { get; set; }

        [JsonProperty("open_date")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime OpenDate { get; set; }

        [JsonProperty("shares")] public decimal Shares { get; set; }

        [JsonProperty("open_price")] public decimal OpenPrice { get; set; }
    }

    public class Order
    {
        [JsonProperty("order_id")] public long OrderId { get; set; }

        [JsonProperty("ticker")] public string Ticker { get; set; }

        [JsonProperty("placed")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime Placed { get; set; }

        [JsonProperty("shares")] public decimal Shares { get; set; }

        [JsonProperty("shares_rest")] public decimal SharesRemaining { get; set; }

        [JsonProperty("price")] public decimal Price { get; set; }

        [JsonProperty("comment", NullValueHandling = NullValueHandling.Ignore)]
        public string Comment { get; set; }

        [JsonProperty("ext_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ExternalId { get; set; }
    }
}
