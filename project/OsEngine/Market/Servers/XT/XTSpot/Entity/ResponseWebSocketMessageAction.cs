using Newtonsoft.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.XT.XTSpot.Entity
{
    public class ResponseWebSocketMessage
    {
         public string id { get; set; }                          //call back ID
         public string code { get; set; }                        //result 0=success;1=fail;2=listenKey invalid
         public string msg { get; set; }                         //Message string, "token expire"
    }
    
    public class ResponseWebSocketMessageAction<T>
    {
        [JsonProperty("topic")]
        public string Topic { get; set; }                        //"trade", 
        [JsonProperty("event")]
        public string Event { get; set; }                        //"trade@btc_usdt"
        [JsonProperty("data")]
        public T Data { get; set; }                              // "s": "btc_usdt", 
                                                                 // "i": 6316559590087222000, 
                                                                 // "t": 1655992403617, 
                                                                 // "p": "43000", 
                                                                 // "q": "0.21", 
                                                                 
    }
    
    public class WsTrade
    {
        [JsonProperty("s")]
        public string Symbol { get; set; }                       //"btc_usdt", symbol
        [JsonProperty("i")]        
        public string TradeId { get; set; }                      //"6316559590087222000",  trade id
        [JsonProperty("t")]
        public string TradeTime { get; set; }                    //"1655992403617", trade time
        [JsonProperty("p")]
        public string TradePrice { get; set; }                   //"43000", trade price
        [JsonProperty("q")]
        public string TradeQuantity { get; set; }                //"0.21",  qty，trade quantity
        [JsonProperty("b")]
        public string IsBuyerMaker { get; set; }                 //"true" whether is buyerMaker or not
    }

    public class ResponseWebSocketDepthIncremental
    {
        [JsonProperty("s")]
        public string Symbol { get; set; }                       //symbol
        [JsonProperty("fi")] 
        public string FirstUpdateId { get; set; }                //firstUpdateId = previous lastUpdateId + 1
        [JsonProperty("i")]
        public string LastUpdateId { get; set; }                 //lastUpdateId
        [JsonProperty("a")]
        public List<List<string>> asks { get; set; }             //List of asks (sell orders), [0]price, [1]quantity
        [JsonProperty("b")]
        public List<List<string>> bids { get; set; }             //List of bids (buy orders), [0]price, [1]quantity
    }

    public class ResponseWebSocketDepth
    {
        [JsonProperty("s")]
        public string Symbol { get; set; }                       //symbol
        [JsonProperty("i")]
        public string UpdateId { get; set; }                     //lastUpdateId
        [JsonProperty("t")]
        public string Time { get; set; }                         //"1655992403617", time  
        [JsonProperty("a")]
        public List<List<string>> asks { get; set; }             //List of asks (sell orders), [0]price, [1]quantity
        [JsonProperty("b")]
        public List<List<string>> bids { get; set; }             //List of bids (buy orders), [0]price, [1]quantity
    }

    public class ResponseWebSocketPortfolio
    {
        // https://doc.xt.com/#websocket_privatebalanceChange
        [JsonProperty("a")]
        public string AccountId { get; set; }                    //"123" accountId                     
        [JsonProperty("t")]
        public string Time { get; set; }                         //"1656043204763", happened time
        [JsonProperty("c")]
        public string Currency { get; set; }                     //"btc", currency
        [JsonProperty("b")]
        public string Balance { get; set; }                      //"123", all spot balance
        [JsonProperty("f")]
        public string Frozen { get; set; }                       //"11", frozen amount
        [JsonProperty("z")]
        public string BizType { get; set; }                      //"SPOT", bizType [SPOT,LEVER]
        [JsonProperty("s")]
        public string Symbol { get; set; }                       //"btc_usdt", symbol
    }

    public class ResponseWebSocketOrder
    {
        // https://doc.xt.com/#websocket_privateorderChange
        [JsonProperty("s")]
        public string Symbol { get; set; }                       //"btc_usdt",  symbol
        [JsonProperty("bc")]
        public string BaseCurrency { get; set; }                 //"btc", base currency
        [JsonProperty("qc")]
        public string QuotationCurrency { get; set; }            //"usdt",  quotation currency 
        [JsonProperty("t")]
        public string HappenedTime { get; set; }                 //1656043204763, happened time in ms
        [JsonProperty("ct")]
        public string CreateTime { get; set; }                   //1656043204663, create time in ms
        [JsonProperty("i")]
        public string OrderId { get; set; }                      //"6216559590087220004", order id,
        [JsonProperty("ci")]
        public string ClientOrderId { get; set; }                //"test123", client order id
        [JsonProperty("st")]
        public string State { get; set; }                        //"PARTIALLY_FILLED", state NEW/PARTIALLY_FILLED/FILLED/CANCELED/REJECTED/EXPIRED
        [JsonProperty("sd")]
        public string Side { get; set; }                         //"BUY", order side BUY/SELL
        [JsonProperty("tp")]
        public string Type { get; set; }                         //"LIMIT", order type LIMIT/MARKET
        [JsonProperty("oq")]
        public string OriginalQuantity { get; set; }             //"4", original quantity
        [JsonProperty("oqq")]
        public string OriginalQuotationQuantity { get; set; }    //48000, original quotation quantity
        [JsonProperty("eq")]
        public string ExecutedQuantity { get; set; }             //"2", executed quantity
        [JsonProperty("lq")]
        public string RemainingQuantity { get; set; }            //"2", remaining quantity
        [JsonProperty("p")]
        public string Price { get; set; }                        //"4000", price 
        [JsonProperty("ap")]
        public string AveragePrice { get; set; }                 //"30000", avg price
        [JsonProperty("f")]
        public string Fee { get; set; }                          //"0.002", fee 
    }
}
