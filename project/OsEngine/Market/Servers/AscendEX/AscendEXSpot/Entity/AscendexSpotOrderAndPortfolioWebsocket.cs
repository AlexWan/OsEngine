using Newtonsoft.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.AscendexSpot.Json
{
    public class WebSocketMessage<T>
    {
        public string m { get; set; }                  //type message  "order", "balance" 

        public string accountId { get; set; }          // ID account 

        public string ac { get; set; }                //type account "CASH / MARGIN" 

        public T data { get; set; }                  // different type data 
    }

    public class AscendexSpotOrderDataWebsocket
    {
        [JsonProperty("s")]
        public string symbol { get; set; }            // symbol "BTC/USDT"

        public string sn { get; set; }                // sequence number "8159711" — уникальный номер события

        public string ap { get; set; }                // average fill price "0" — средняя цена исполнения

        public string bab { get; set; }               // base asset available balance  "2006.5974027"— доступный базовый актив

        public string btb { get; set; }               // base asset total balance  "2006.5974027" — общий базовый актив

        public string cf { get; set; }               // cumulated commission  "0" — суммарная комиссия

        [JsonProperty("cfq")]
        public string quantity { get; set; }          // cumulated filled qty  "0" — суммарное исполненное количество

        public string err { get; set; }              // error code ""— код ошибки (может быть пустым)

        public string fa { get; set; }              // fee asset "USDT"— актив, в котором взята комиссия

        public string orderId { get; set; }         // order id "s16ef210b1a50866943712bfaf1584b" — уникальный идентификатор ордера

        [JsonProperty("ot")]
        public string orderType { get; set; }      // order type "Market, Limit"— тип ордера (например: Market, Limit)

        [JsonProperty("p")]
        public string price { get; set; }         // order price  "7967.62"— цена ордера

        [JsonProperty("q")]
        public string volume { get; set; }       // order quantity "0.0083"— запрошенное количество

        public string qab { get; set; }         // quote asset available balance "793.23"— доступный котируемый актив

        public string qtb { get; set; }        // quote asset total balance "860.23"— общий котируемый актив

        public string sd { get; set; }        // order side "Buy / Sell" — сторона сделки (Buy / Sell)

        public string sp { get; set; }       // stop price "" — цена стопа (может быть пустой)

        [JsonProperty("st")]
        public string status { get; set; }  // order status "New, Filled, Canceled etc" — статус ордера (New, Filled, Canceled и т.д.)

        public string t { get; set; }     // latest execution timestamp "1576019215402"— время последнего исполнения

        public string ei { get; set; }    // execution instruction "NULL_VAL" — инструкция исполнения
    }

    public class AscendexSpotBalanceResponseWebsocket
    {
        public string code { get; set; }

        public List<AscendexSpotBalanceWebsocket> data { get; set; }
    }

    public class AscendexSpotBalanceWebsocket
    {
        public string asset { get; set; }

        public string totalBalance { get; set; }

        public string availableBalance { get; set; }
    }
}