using Newtonsoft.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.Huobi.Futures.Entities
{
    public class Datum
    {

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("contract_code")]
        public string ContractCode { get; set; }

        [JsonProperty("contract_type")]
        public string ContractType { get; set; }

        [JsonProperty("contract_size")]
        public decimal ContractSize { get; set; }

        [JsonProperty("price_tick")]
        public decimal PriceTick { get; set; }

        [JsonProperty("delivery_date")]
        public string DeliveryDate { get; set; }

        [JsonProperty("create_date")]
        public string CreateDate { get; set; }

        [JsonProperty("contract_status")]
        public int ContractStatus { get; set; }
    }

    public class FuturesSymbolResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("data")]
        public IList<Datum> Data { get; set; }

        [JsonProperty("ts")]
        public long Ts { get; set; }
    }
}
