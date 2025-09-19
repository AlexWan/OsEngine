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
        public string topic { get; set; }
        public string @event { get; set; }
        public T data { get; set; }
    }

    public class WsTrade
    {
        public string s { get; set; }                       //"btc_usdt", symbol        
        public string i { get; set; }                      //"6316559590087222000",  trade id
        public string t { get; set; }                    //"1655992403617", trade time
        public string p { get; set; }                   //"43000", trade price
        public string q { get; set; }                //"0.21",  qty，trade quantity
        public string b { get; set; }                 //"true" whether is buyerMaker or not
    }

    public class ResponseWebSocketDepthIncremental
    {
        public string s { get; set; }                       //symbol
        public string fi { get; set; }                //firstUpdateId = previous lastUpdateId + 1
        public string i { get; set; }                 //lastUpdateId
        public List<List<string>> a { get; set; }             //List of asks (sell orders), [0]price, [1]quantity
        public List<List<string>> b { get; set; }             //List of bids (buy orders), [0]price, [1]quantity
        public string t { get; set; }
    }

    public class ResponseWebSocketDepth
    {
        public string s { get; set; }                       //symbol
        public string i { get; set; }                     //lastUpdateId
        public string t { get; set; }                         //"1655992403617", time  
        public List<List<string>> a { get; set; }             //List of asks (sell orders), [0]price, [1]quantity
        public List<List<string>> b { get; set; }             //List of bids (buy orders), [0]price, [1]quantity
    }

    public class ResponseWebSocketPortfolio
    {
        public string a { get; set; }                    //"123" accountId                     
        public string t { get; set; }                         //"1656043204763", happened time
        public string c { get; set; }                     //"btc", currency
        public string b { get; set; }                      //"123", all spot balance
        public string f { get; set; }                       //"11", frozen amount
        public string z { get; set; }                      //"SPOT", bizType [SPOT,LEVER]
        public string s { get; set; }                       //"btc_usdt", symbol
    }

    public class ResponseWebSocketOrder
    {
        public string s { get; set; }                       //"btc_usdt",  symbol
        public string bc { get; set; }                 //"btc", base currency
        public string qc { get; set; }            //"usdt",  quotation currency 
        public string t { get; set; }                 //1656043204763, happened time in ms
        public string ct { get; set; }                   //1656043204663, create time in ms
        public string i { get; set; }                      //"6216559590087220004", order id,
        public string ci { get; set; }                //"test123", client order id
        public string st { get; set; }                        //"PARTIALLY_FILLED", state NEW/PARTIALLY_FILLED/FILLED/CANCELED/REJECTED/EXPIRED
        public string sd { get; set; }                         //"BUY", order side BUY/SELL
        public string tp { get; set; }                         //"LIMIT", order type LIMIT/MARKET
        public string oq { get; set; }             //"4", original quantity
        public string oqq { get; set; }    //48000, original quotation quantity
        public string eq { get; set; }             //"2", executed quantity
        public string lq { get; set; }            //"2", remaining quantity
        public string p { get; set; }                        //"4000", price 
        public string ap { get; set; }                 //"30000", avg price
        public string f { get; set; }                          //"0.002", fee 
    }

    public class ResponseWebSocketMyTrade
    {
        public string s { get; set; }           // symbol
        public string i { get; set; }            // tradeId
        public string t { get; set; }             // time (timestamp)
        public string oi { get; set; }           // orderId
        public string p { get; set; }           // price
        public string q { get; set; }           // quantity
        public string v { get; set; }           // quoteQty
        public string b { get; set; }             // isBuyerMaker
        public string tm { get; set; }             // 1-taker, 2-maker
    }
}
