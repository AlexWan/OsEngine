using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Hitbtc
{
    // Update balance
    

    public class UpdateBalance
    {
        public string jsonrpc { get; set; }
        public List<Balances> result { get; set; }
        public int id { get; set; }
    }

    public class Result
    {
        public string id { get; set; }
        public string clientOrderId { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public string status { get; set; }
        public string type { get; set; }
        public string timeInForce { get; set; }
        public string quantity { get; set; }
        public string price { get; set; }
        public string cumQuantity { get; set; }
        public bool postOnly { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
        public string reportType { get; set; }
    }

    public class RootOrder
    {
        public string jsonrpc { get; set; }
        public Result result { get; set; }
        public int id { get; set; }
    }



    // Security
    public class Symbols
    {
        public string id { get; set; }
        public string baseCurrency { get; set; }
        public string quoteCurrency { get; set; }
        public string quantityIncrement { get; set; }
        public string tickSize { get; set; }
        public string takeLiquidityRate { get; set; }
        public string provideLiquidityRate { get; set; }
        public string feeCurrency { get; set; }
    }

    // Balance
    public class Balances
    {
        public string currency { get; set; }
        public string available { get; set; }
        public string reserved { get; set; }
    }

    // balanceInfo

    public class BalanceInfo
    {
        public string Name { get; set; }
        public List<Balances> Balances { get; set; }
    }

    // Trades

    public class Datum
    {
        public int id { get; set; }
        public string price { get; set; }
        public string quantity { get; set; }
        public string side { get; set; }
        public DateTime timestamp { get; set; }
    }

    public class Params
    {
        public List<Datum> data { get; set; }
        public string symbol { get; set; }
    }

    public class RootTrade
    {
        public string jsonrpc { get; set; }
        public string method { get; set; }
        public Params @params { get; set; }
    }

    // market Depth
    public class Ask
    {
        public string price { get; set; }
        public string size { get; set; }
    }

    public class Bid
    {
        public string price { get; set; }
        public string size { get; set; }
    }

    public class Depth
    {
        public List<Ask> ask { get; set; }
        public List<Bid> bid { get; set; }
        public string symbol { get; set; }
        public int sequence { get; set; }
        public DateTime timestamp { get; set; }
    }

    public class RootDepth
    {
        public string jsonrpc { get; set; }
        public string method { get; set; }
        public Depth @params { get; set; }
    }

    public class UpdateDepth
    {
        public string jsonrpc { get; set; }
        public string method { get; set; }
        public Depth @params { get; set; }
    }

    // Order result
    public class OrderSendResult
    {
        public int id { get; set; }
        public string clientOrderId { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public string status { get; set; }
        public string type { get; set; }
        public string timeInForce { get; set; }
        public string quantity { get; set; }
        public string price { get; set; }
        public string cumQuantity { get; set; }
        public bool postOnly { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }

    // errors
    public class Error
    {
        public int code { get; set; }
        public string message { get; set; }
        public string description { get; set; }
    }

    public class RootError
    {
        public string jsonrpc { get; set; }
        public Error error { get; set; }
        public int id { get; set; }
    }

    public class HitCandle
    {
        public DateTime timestamp { get; set; }
        public string open { get; set; }
        public string close { get; set; }
        public string min { get; set; }
        public string max { get; set; }
        public string volume { get; set; }
        public string volumeQuote { get; set; }
    }



    public class Tick
    {
        public string ask { get; set; }
        public string bid { get; set; }
        public string last { get; set; }
        public string open { get; set; }
        public string low { get; set; }
        public string high { get; set; }
        public string volume { get; set; }
        public string volumeQuote { get; set; }
        public DateTime timestamp { get; set; }
        public string symbol { get; set; }
    }

    public class RootTick
    {
        public string jsonrpc { get; set; }
        public string method { get; set; }
        public Tick @params { get; set; }
    }




    public class ReportParams
    {
        public string id { get; set; }
        public string clientOrderId { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public string status { get; set; }
        public string type { get; set; }
        public string timeInForce { get; set; }
        public string quantity { get; set; }
        public string price { get; set; }
        public string cumQuantity { get; set; }
        public bool postOnly { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
        public string reportType { get; set; }
        public string tradeQuantity { get; set; }
        public string tradePrice { get; set; }
        public int tradeId { get; set; }
        public string tradeFee { get; set; }
    }

    public class RootReport
    {
        public string jsonrpc { get; set; }
        public string method { get; set; }
        public ReportParams @params { get; set; }
    }






    class HitbtcEntity
    {
    }
}
