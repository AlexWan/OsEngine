using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.BitMax
{
    public class AccGroup
    {
        /// <summary>
        /// 
        /// </summary>
        public int AccountGroup { get; set; }
    }

    public class Product
    {
        public string symbol { get; set; }
        public string domain { get; set; }
        public string baseAsset { get; set; }
        public string quoteAsset { get; set; }
        public int priceScale { get; set; }
        public int qtyScale { get; set; }
        public int notionalScale { get; set; }
        public string minQty { get; set; }
        public string maxQty { get; set; }
        public string minNotional { get; set; }
        public string maxNotional { get; set; }
        public string status { get; set; }
        public string miningStatus { get; set; }
        public bool marginTradable { get; set; }
    }

    public class Wallet
    {
        public string assetCode { get; set; }
        public string assetName { get; set; }
        public string totalAmount { get; set; }
        public string availableAmount { get; set; }
        public string inOrderAmount { get; set; }
        public string btcValue { get; set; }
    }

    public class Accaunt
    {
        public int code { get; set; }
        public List<Wallet> data { get; set; }
        public string status { get; set; }
        public string email { get; set; }
    }

    public class Depth
    {
        public string m { get; set; }
        public string s { get; set; }
        public List<List<string>> asks { get; set; }
        public List<List<string>> bids { get; set; }
    }

    public class BitMaxTrade
    {
        public string p { get; set; }
        public string q { get; set; }
        public object t { get; set; }
        public bool bm { get; set; }
    }

    public class Trades
    {
        public string m { get; set; }
        public string s { get; set; }
        public List<BitMaxTrade> trades { get; set; }
    }

    public class BitMaxCandle
    {
        public string m { get; set; }
        public string s { get; set; }
        public string ba { get; set; }
        public string qa { get; set; }
        public string i { get; set; }
        public object t { get; set; }
        public string o { get; set; }
        public string c { get; set; }
        public string h { get; set; }
        public string l { get; set; }
        public string v { get; set; }
    }

    public class BitMaxOrder
    {
        public string m { get; set; }
        public string coid { get; set; }
        public string s { get; set; }
        public string ba { get; set; }
        public string qa { get; set; }
        public long t { get; set; }
        public string p { get; set; }
        public string sp { get; set; }
        public string q { get; set; }
        public string f { get; set; }
        public string ap { get; set; }
        public string bb { get; set; }
        public string bpb { get; set; }
        public string qb { get; set; }
        public string qpb { get; set; }
        public string fee { get; set; }
        public string fa { get; set; }
        public string side { get; set; }
        public string status { get; set; }
    }

    public class Data
    {
        public string coid { get; set; }
        public string action { get; set; }
        public bool success { get; set; }
    }

    public class OrderSendResult
    {
        public int code { get; set; }
        public string email { get; set; }
        public string status { get; set; }
        public Data data { get; set; }
        public string message { get; set; }
    }

    public class OrderStateData
    {
        public long time { get; set; }
        public string coid { get; set; }
        public string symbol { get; set; }
        public string baseAsset { get; set; }
        public string quoteAsset { get; set; }
        public string side { get; set; }
        public string orderPrice { get; set; }
        public string stopPrice { get; set; }
        public string orderQty { get; set; }
        public string filled { get; set; }
        public string fee { get; set; }
        public string feeAsset { get; set; }
        public string status { get; set; }
    }

    public class OrderState
    {
        public int code { get; set; }
        public string status { get; set; }
        public string email { get; set; }
        public OrderStateData data { get; set; }
    }
}
