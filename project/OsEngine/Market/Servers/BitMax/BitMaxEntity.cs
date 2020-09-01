using Newtonsoft.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMax
{
    public class Account
    {
        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("accountGroup")]
        public int AccountGroup { get; set; }

        [JsonProperty("viewPermission")]
        public bool ViewPermission { get; set; }

        [JsonProperty("tradePermission")]
        public bool TradePermission { get; set; }

        [JsonProperty("transferPermission")]
        public bool TransferPermission { get; set; }

        [JsonProperty("cashAccount")]
        public IList<string> CashAccount { get; set; }

        [JsonProperty("marginAccount")]
        public IList<string> MarginAccount { get; set; }

        [JsonProperty("futuresAccount")]
        public IList<string> FuturesAccount { get; set; }

        [JsonProperty("userUID")]
        public string UserUID { get; set; }
    }

    public class AccountGroup
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("data")]
        public Account Account { get; set; }
    }

    public class Product
    {
        [JsonProperty("symbol")]
        public string Symbol;

        [JsonProperty("baseAsset")]
        public string BaseAsset;

        [JsonProperty("quoteAsset")]
        public string QuoteAsset;

        [JsonProperty("status")]
        public string Status;

        [JsonProperty("minNotional")]
        public string MinNotional;

        [JsonProperty("maxNotional")]
        public string MaxNotional;

        [JsonProperty("marginTradable")]
        public bool MarginTradable;

        [JsonProperty("commissionType")]
        public string CommissionType;

        [JsonProperty("commissionReserveRate")]
        public string CommissionReserveRate;

        [JsonProperty("tickSize")]
        public string TickSize;

        [JsonProperty("lotSize")]
        public string LotSize;
    }

    public class RootProducts
    {
        [JsonProperty("code")]
        public int Code;

        [JsonProperty("data")]
        public List<Product> Data;

    }

    public class MarginAccaunt
    {
        public int code { get; set; }
        public List<MarginWallet> data { get; set; }
        public string status { get; set; }
        public string email { get; set; }
    }

    public class MarginWallet
    {
        public string assetCode { get; set; }
        public string totalAmount { get; set; }
        public string availableAmount { get; set; }
        public string borrowedAmount { get; set; }
        public string interest { get; set; }
        public string maxSellable { get; set; }
        public string interestRate { get; set; }
        public string maxTransferable { get; set; }
    }

    public class Wallet
    {
        [JsonProperty("asset")]
        public string Asset { get; set; }

        [JsonProperty("totalBalance")]
        public string TotalBalance { get; set; }

        [JsonProperty("availableBalance")]
        public string AvailableBalance { get; set; }

        [JsonProperty("borrowed")]
        public string Borrowed { get; set; }

        [JsonProperty("interest")]
        public string Interest { get; set; }
    }

    public class Wallets
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("data")]
        public IList<Wallet> Data { get; set; }
    }

    public class ReferencePrice
    {
        public string Asset { get; set; }
        public decimal Price { get; set; }
    }

    public class DepthData
    {
        [JsonProperty("ts")]
        public long Ts { get; set; }

        [JsonProperty("seqnum")]
        public long Seqnum { get; set; }

        [JsonProperty("asks")]
        public IList<IList<string>> Asks { get; set; }

        [JsonProperty("bids")]
        public IList<IList<string>> Bids { get; set; }
    }

    public class Depth
    {
        [JsonProperty("m")]
        public string M { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("data")]
        public DepthData Data { get; set; }
    }

    public class TradeInfoData
    {
        [JsonProperty("p")]
        public string P { get; set; }

        [JsonProperty("q")]
        public string Q { get; set; }

        [JsonProperty("ts")]
        public long Ts { get; set; }

        [JsonProperty("bm")]
        public bool Bm { get; set; }

        [JsonProperty("seqnum")]
        public long Seqnum { get; set; }
    }

    public class TradeInfo
    {
        [JsonProperty("m")]
        public string M { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("data")]
        public IList<TradeInfoData> Data { get; set; }
    }

    public class BitMaxCandle
    {
        [JsonProperty("c")]
        public string C { get; set; }

        [JsonProperty("h")]
        public string H { get; set; }

        [JsonProperty("i")]
        public string I { get; set; }

        [JsonProperty("l")]
        public string L { get; set; }

        [JsonProperty("o")]
        public string O { get; set; }

        [JsonProperty("ts")]
        public object Ts { get; set; }

        [JsonProperty("v")]
        public string V { get; set; }
    }

    public class DatumCandles
    {
        [JsonProperty("data")]
        public BitMaxCandle Candle { get; set; }

        [JsonProperty("m")]
        public string M { get; set; }

        [JsonProperty("s")]
        public string S { get; set; }
    }

    public class CandlesInfo
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("data")]
        public IList<DatumCandles> Candles { get; set; }
    }

    public class OrderPlaceDataInfo
    {
        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("orderType")]
        public string OrderType { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("orderId")]
        public string OrderId { get; set; }
    }

    public class OrderPlaceData
    {
        [JsonProperty("accountId")]
        public string AccountId { get; set; }

        [JsonProperty("ac")]
        public string Ac { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("info")]
        public OrderPlaceDataInfo Info { get; set; }
    }

    public class OrderPlaceResponse
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("data")]
        public OrderPlaceData Data { get; set; }
    }

    public class OrderStateData
    {
        [JsonProperty("sn")]
        public long Sn { get; set; }

        [JsonProperty("orderId")]
        public string OrderId { get; set; }

        [JsonProperty("s")]
        public string S { get; set; }

        [JsonProperty("ot")]
        public string Ot { get; set; }

        [JsonProperty("t")]
        public long T { get; set; }

        [JsonProperty("p")]
        public string P { get; set; }

        [JsonProperty("q")]
        public string Q { get; set; }

        [JsonProperty("sd")]
        public string Sd { get; set; }

        [JsonProperty("st")]
        public string St { get; set; }

        [JsonProperty("ap")]
        public string Ap { get; set; }

        [JsonProperty("cfq")]
        public string Cfq { get; set; }

        [JsonProperty("sp")]
        public string Sp { get; set; }

        [JsonProperty("err")]
        public string Err { get; set; }

        [JsonProperty("btb")]
        public string Btb { get; set; }

        [JsonProperty("bab")]
        public string Bab { get; set; }

        [JsonProperty("qtb")]
        public string Qtb { get; set; }

        [JsonProperty("qab")]
        public string Qab { get; set; }

        [JsonProperty("cf")]
        public string Cf { get; set; }

        [JsonProperty("fa")]
        public string Fa { get; set; }

        [JsonProperty("ei")]
        public string Ei { get; set; }
    }

    public class OrderState
    {
        [JsonProperty("m")]
        public string M { get; set; }

        [JsonProperty("accountId")]
        public string AccountId { get; set; }

        [JsonProperty("ac")]
        public string Ac { get; set; }

        [JsonProperty("data")]
        public OrderStateData Data { get; set; }
    }

    public class Datum
    {
        [JsonProperty("seqNum")]
        public long seqNum { get; set; }

        [JsonProperty("orderId")]
        public string orderId { get; set; }

        [JsonProperty("symbol")]
        public string symbol { get; set; }

        [JsonProperty("orderType")]
        public string orderType { get; set; }

        [JsonProperty("lastExecTime")]
        public long lastExecTime { get; set; }

        [JsonProperty("price")]
        public string price { get; set; }

        [JsonProperty("orderQty")]
        public string orderQty { get; set; }

        [JsonProperty("side")]
        public string side { get; set; }

        [JsonProperty("status")]
        public string status { get; set; }

        [JsonProperty("avgPx")]
        public string avgPx { get; set; }

        [JsonProperty("cumFilledQty")]
        public string cumFilledQty { get; set; }

        [JsonProperty("stopPrice")]
        public string stopPrice { get; set; }

        [JsonProperty("errorCode")]
        public string errorCode { get; set; }

        [JsonProperty("cumFee")]
        public string cumFee { get; set; }

        [JsonProperty("feeAsset")]
        public string feeAsset { get; set; }

        [JsonProperty("execInst")]
        public string execInst { get; set; }
    }

    public class OrdersHistoryResult
    {

        [JsonProperty("code")]
        public int code { get; set; }

        [JsonProperty("accountId")]
        public string accountId { get; set; }

        [JsonProperty("ac")]
        public string ac { get; set; }

        [JsonProperty("data")]
        public IList<Datum> data { get; set; }
    }
}
