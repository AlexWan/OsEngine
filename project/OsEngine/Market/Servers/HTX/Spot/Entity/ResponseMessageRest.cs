using Newtonsoft.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.HTX.Spot.Entity
{
    public class ResponseMessageSecurities
    {
        public List<Data> data { get; set; }

        public class Data
        {
            public string symbol { get; set; }
            public string state { get; set; }
            public string bc { get; set; }
            public string qc { get; set; }
            public string pp { get; set; }
            public string ap { get; set; }
            public string sp { get; set; }
            public string vp { get; set; }
            public string minoa { get; set; }
            public string maxoa { get; set; }
            public string minov { get; set; }
            public string lominoa { get; set; }
            public string lomaxoa { get; set; }
            public string lomaxba { get; set; }
            public string lomaxsa { get; set; }
            public string smminoa { get; set; }
            public string blmlt { get; set; }
            public string slmgt { get; set; }
            public string smmaxoa { get; set; }
            public string bmmaxov { get; set; }
            public string msormlt { get; set; }
            public string mbormlt { get; set; }
            public string at { get; set; }
            public string tags { get; set; }
            public string lr { get; set; }
            public string smlr { get; set; }
            public string maxov { get; set; }
        }
    }

    public class ResponseMessagePortfolios
    {
        public List<Data> data { get; set; }

        public class Data
        {
            public string type { get; set; }
            public string id { get; set; }
        }
    }

    public class ResponseMessagePositions
    {
        public Data data { get; set; }

        public class Data
        {
            public List<Lists> list { get; set; }
        }
        public class Lists
        {
            public string currency { get; set; }
            public string balance { get; set; }
            public string type { get; set; }            
        }
    }

    public class ResponseMessageCandles
    {
        public List<Data> data { get; set; }
        public string rep {  get; set; }

        public class Data
        {           
            public string open { get; set; }
            public string close { get; set; }
            public string high { get; set; }
            public string low { get; set; }
            public string vol { get; set; }
            public string id { get; set; } //timestamp
        }
    }
    public class ResponsePing
    {
        public string ping { get; set; }
    }
    public class PlaceOrderResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        public string status { get; set; }

        /// <summary>
        /// Order id
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string data;

        /// <summary>
        /// Error code
        /// </summary>
        [JsonProperty("err-code", NullValueHandling = NullValueHandling.Ignore)]
        public string errorCode;

        /// <summary>
        /// Error message
        /// </summary>
        [JsonProperty("err-msg", NullValueHandling = NullValueHandling.Ignore)]
        public string errorMessage;
    }

    public class ResponseMessageAllOrders
    {

        public List<Data> data { get; set; }

        public class Data
        {
            public string symbol { get; set; }
            public string id { get; set; }
            public string price { get; set; }
            public string amount { get; set; }
            public string type { get; set; }
            public string source { get; set; }
            public string state { get; set; }
            [JsonProperty("account-id", NullValueHandling = NullValueHandling.Ignore)]
            public string account_id { get; set; }

            [JsonProperty("created-at", NullValueHandling = NullValueHandling.Ignore)]           
            public string created_at { get; set; }

            [JsonProperty("client-order-id", NullValueHandling = NullValueHandling.Ignore)]
            public string client_order_id { get; set; }           
        }
    }

    public class ResponseMessageGetOrder
    {

        public Data data { get; set; }

        public class Data
        {
            public string symbol { get; set; }
            public string id { get; set; }
            public string price { get; set; }
            public string amount { get; set; }
            public string type { get; set; }
            public string source { get; set; }
            public string state { get; set; }
            [JsonProperty("account-id", NullValueHandling = NullValueHandling.Ignore)]
            public string account_id { get; set; }

            [JsonProperty("created-at", NullValueHandling = NullValueHandling.Ignore)]
            public string created_at { get; set; }

            [JsonProperty("client-order-id", NullValueHandling = NullValueHandling.Ignore)]
            public string client_order_id { get; set; }
        }
    }

    public class ResponseMessageGetMyTradesBySecurity
    {
        public List<Data> data { get; set; }

        public class Data
        {
            public string symbol { get; set; }
            public string id { get; set; }
            public string price { get; set; }

            [JsonProperty("filled-amount", NullValueHandling = NullValueHandling.Ignore)]
            public string filled_amount { get; set; }
            public string type { get; set; }
            public string source { get; set; }
            
            [JsonProperty("order-id", NullValueHandling = NullValueHandling.Ignore)]
            public string order_id { get; set; }

            [JsonProperty("created-at", NullValueHandling = NullValueHandling.Ignore)]
            public string created_at { get; set; }

            [JsonProperty("trade-id", NullValueHandling = NullValueHandling.Ignore)]
            public string trade_id { get; set; }
        }
    }
}
