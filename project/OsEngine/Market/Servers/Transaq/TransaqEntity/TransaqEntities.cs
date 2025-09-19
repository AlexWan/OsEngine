using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace OsEngine.Market.Servers.Transaq.TransaqEntity
{
    [XmlRoot(ElementName = "client")]
    public class Client
    {
        [XmlElement(ElementName = "market")]
        public string Market { get; set; }
        [XmlElement(ElementName = "currency")]
        public string Currency { get; set; }
        [XmlElement(ElementName = "type")]
        public string Type { get; set; }
        [XmlElement(ElementName = "union")]
        public string Union { get; set; }
        [XmlElement(ElementName = "forts_acc")]
        public string Forts_acc { get; set; }
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "remove")]
        public string Remove { get; set; }
    }
    
    [XmlRoot(ElementName = "server_status")]
    public class ServerStatus
    {
        [XmlAttribute(AttributeName = "server_tz")]
        public string Server_tz { get; set; }
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "connected")]
        public string Connected { get; set; }
        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "news_header")]
    public class TransaqNews
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "timestamp")]
        public string Timestamp { get; set; }

        [XmlAttribute(AttributeName = "source")]
        public string Source { get; set; }

        [XmlAttribute(AttributeName = "title")]
        public string Title { get; set; }

        public string NewsBody { get; set; }
    }

    [XmlRoot(ElementName = "news_body")]
    public class TransaqNewsBody
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "text")]
        public string Text { get; set; }

    }

    [XmlRoot(ElementName = "opmask")]
    public class Opmask
    {
        [XmlAttribute(AttributeName = "usecredit")]
        public string Usecredit { get; set; }
        [XmlAttribute(AttributeName = "bymarket")]
        public string Bymarket { get; set; }
        [XmlAttribute(AttributeName = "nosplit")]
        public string Nosplit { get; set; }
        [XmlAttribute(AttributeName = "fok")]
        public string Fok { get; set; }
        [XmlAttribute(AttributeName = "ioc")]
        public string Ioc { get; set; }
        [XmlAttribute(AttributeName = "immorcancel")]
        public string Immorcancel { get; set; }
        [XmlAttribute(AttributeName = "cancelbalance")]
        public string Cancelbalance { get; set; }
    }

    [XmlRoot(ElementName = "security")]
    public class Security
    {
        [XmlElement(ElementName = "sec_tz")]
        public string Sec_tz { get; set; }
        [XmlElement(ElementName = "seccode")]
        public string Seccode { get; set; }
        [XmlElement(ElementName = "instrclass")]
        public string Instrclass { get; set; }
        [XmlElement(ElementName = "board")]
        public string Board { get; set; }
        [XmlElement(ElementName = "shortname")]
        public string Shortname { get; set; }
        [XmlElement(ElementName = "decimals")]
        public string Decimals { get; set; }
        [XmlElement(ElementName = "market")]
        public string Market { get; set; }
        [XmlElement(ElementName = "sectype")]
        public string Sectype { get; set; }
        [XmlElement(ElementName = "opmask")]
        public Opmask Opmask { get; set; }
        [XmlElement(ElementName = "minstep")]
        public string Minstep { get; set; }
        [XmlElement(ElementName = "lotsize")]
        public string Lotsize { get; set; }
        [XmlElement(ElementName = "point_cost")]
        public string Point_cost { get; set; }
        [XmlElement(ElementName = "quotestype")]
        public string Quotestype { get; set; }
        [XmlAttribute(AttributeName = "secid")]
        public string Secid { get; set; }
        [XmlAttribute(AttributeName = "active")]
        public string Active { get; set; }
    }

    [XmlRoot(ElementName = "sec_info_upd")]
    public class SecurityInfo
    {
        [XmlElement(ElementName = "secid")]
        public string Secid { get; set; }
        [XmlElement(ElementName = "seccode")]
        public string Seccode { get; set; }
        [XmlElement(ElementName = "market")]
        public string Market { get; set; }
        [XmlElement(ElementName = "bgo_c")]
        public string Bgo_c { get; set; }
        [XmlElement(ElementName = "bgo_nc")]
        public string Bgo_nc { get; set; }
        [XmlElement(ElementName = "bgo_buy")]
        public string Bgo_buy { get; set; }
        [XmlElement(ElementName = "buy_deposit")]
        public string Buy_deposit { get; set; }
        [XmlElement(ElementName = "sell_deposit")]
        public string Sell_deposit { get; set; }
        [XmlElement(ElementName = "minprice")]
        public string Minprice { get; set; }
        [XmlElement(ElementName = "maxprice")]
        public string Maxprice { get; set; }
    }

    [XmlRoot(ElementName = "quote")]
    public sealed class Quote
    {
        [XmlElement(ElementName = "board")]
        public string Board { get; set; }
        [XmlElement(ElementName = "seccode")]
        public string Seccode { get; set; }
        [XmlElement(ElementName = "price")]
        public decimal Price { get; set; }
        [XmlElement(ElementName = "yield")]
        public string Yield { get; set; }
        [XmlElement(ElementName = "buy")]
        public decimal Buy { get; set; }
        [XmlAttribute(AttributeName = "secid")]
        public string Secid { get; set; }
        [XmlElement(ElementName = "sell")]
        public decimal Sell { get; set; }

    }

    [XmlRoot(ElementName = "quotations")]
    public class QuotationsList
    {
        [XmlElement(ElementName = "quotation")]
        public List<BidAsk> Quotations { get; set; } = new List<BidAsk>();
    }

    [XmlRoot(ElementName = "quotation")]
    public class BidAsk
    {
        [XmlAttribute(AttributeName = "secid")]
        public string SecId { get; set; }

        [XmlElement(ElementName = "board")]
        public string Board { get; set; }

        [XmlElement(ElementName = "seccode")]
        public string Seccode { get; set; }

        [XmlElement(ElementName = "accruedintvalue")]
        public string AccruedIntValue { get; set; }

        [XmlElement(ElementName = "open")]
        public string Open { get; set; }

        [XmlElement(ElementName = "waprice")]
        public string Waprice { get; set; }

        [XmlElement(ElementName = "biddepth")]
        public string Biddepth { get; set; }

        [XmlElement(ElementName = "biddeptht")]
        public string BiddepthT { get; set; }

        [XmlElement(ElementName = "numbids")]
        public string NumBids { get; set; }

        [XmlElement(ElementName = "offerdepth")]
        public string Offerdepth { get; set; }

        [XmlElement(ElementName = "offerdeptht")]
        public string OfferdepthT { get; set; }

        [XmlElement(ElementName = "bid")]
        public string Bid { get; set; }

        [XmlElement(ElementName = "offer")]
        public string Offer { get; set; }

        [XmlElement(ElementName = "numoffers")]
        public string NumOffers { get; set; }

        [XmlElement(ElementName = "numtrades")]
        public string NumTrades { get; set; }

        [XmlElement(ElementName = "voltoday")]
        public string VolToday { get; set; }

        [XmlElement(ElementName = "openpositions")]
        public string OpenPositions { get; set; }

        [XmlElement(ElementName = "deltapositions")]
        public string DeltaPositions { get; set; }

        [XmlElement(ElementName = "last")]
        public string Last { get; set; }

        [XmlElement(ElementName = "quantity")]
        public string Quantity { get; set; }

        [XmlElement(ElementName = "time")]
        public string Time { get; set; }

        [XmlElement(ElementName = "change")]
        public string Change { get; set; }

        [XmlElement(ElementName = "priceminusprevwaprice")]
        public string PriceMinusPrevWaprice { get; set; }

        [XmlElement(ElementName = "valtoday")]
        public string ValToday { get; set; }

        [XmlElement(ElementName = "yield")]
        public string Yield { get; set; }

        [XmlElement(ElementName = "yieldatwaprice")]
        public string YieldAtWaprice { get; set; }

        [XmlElement(ElementName = "marketpricetoday")]
        public string MarketPriceToday { get; set; }

        [XmlElement(ElementName = "highbid")]
        public string HighBid { get; set; }

        [XmlElement(ElementName = "lowoffer")]
        public string LowOffer { get; set; }

        [XmlElement(ElementName = "high")]
        public string High { get; set; }

        [XmlElement(ElementName = "low")]
        public string Low { get; set; }

        [XmlElement(ElementName = "closeprice")]
        public string ClosePrice { get; set; }

        [XmlElement(ElementName = "closeyield")]
        public string CloseYield { get; set; }

        [XmlElement(ElementName = "status")]
        public string Status { get; set; }

        [XmlElement(ElementName = "tradingstatus")]
        public string TradingStatus { get; set; }

        [XmlElement(ElementName = "buydeposit")]
        public string BuyDeposit { get; set; }

        [XmlElement(ElementName = "selldeposit")]
        public string SellDeposit { get; set; }

        [XmlElement(ElementName = "volatility")]
        public string Volatility { get; set; }

        [XmlElement(ElementName = "theoreticalprice")]
        public string TheoreticalPrice { get; set; }

        [XmlElement(ElementName = "bgo_buy")]
        public string BgoBuy { get; set; }

        [XmlElement(ElementName = "point_cost")]
        public string PointCost { get; set; }

        [XmlElement(ElementName = "lcurrentprice")]
        public string LCurrentPrice { get; set; }
    }

    [XmlRoot(ElementName = "order")]
    public class Order
    {
        [XmlElement(ElementName = "orderno")]
        public string Orderno { get; set; }
        [XmlElement(ElementName = "secid")]
        public string Secid { get; set; }
        [XmlElement(ElementName = "union")]
        public string Union { get; set; }
        [XmlElement(ElementName = "board")]
        public string Board { get; set; }
        [XmlElement(ElementName = "seccode")]
        public string Seccode { get; set; }
        [XmlElement(ElementName = "client")]
        public string Client { get; set; }
        [XmlElement(ElementName = "status")]
        public string Status { get; set; }
        [XmlElement(ElementName = "buysell")]
        public string Buysell { get; set; }
        [XmlElement(ElementName = "time")]
        public string Time { get; set; }
        [XmlElement(ElementName = "brokerref")]
        public string Brokerref { get; set; }
        [XmlElement(ElementName = "value")]
        public string Value { get; set; }
        [XmlElement(ElementName = "accruedint")]
        public string Accruedint { get; set; }
        [XmlElement(ElementName = "settlecode")]
        public string Settlecode { get; set; }
        [XmlElement(ElementName = "balance")]
        public string Balance { get; set; }
        [XmlElement(ElementName = "price")]
        public string Price { get; set; }
        [XmlElement(ElementName = "quantity")]
        public string Quantity { get; set; }
        [XmlElement(ElementName = "hidden")]
        public string Hidden { get; set; }
        [XmlElement(ElementName = "yield")]
        public string Yield { get; set; }
        [XmlElement(ElementName = "withdrawtime")]
        public string Withdrawtime { get; set; }
        [XmlElement(ElementName = "condition")]
        public string Condition { get; set; }
        [XmlElement(ElementName = "maxcomission")]
        public string Maxcomission { get; set; }
        [XmlElement(ElementName = "result")]
        public string Result { get; set; }
        [XmlAttribute(AttributeName = "transactionid")]
        public string Transactionid { get; set; }
    }

    [XmlRoot(ElementName = "trade")]
    public class Trade
    {
        [XmlElement(ElementName = "secid")]
        public string Secid { get; set; }
        [XmlElement(ElementName = "tradeno")]
        public string Tradeno { get; set; }
        [XmlElement(ElementName = "orderno")]
        public string Orderno { get; set; }
        [XmlElement(ElementName = "board")]
        public string Board { get; set; }
        [XmlElement(ElementName = "seccode")]
        public string Seccode { get; set; }
        [XmlElement(ElementName = "client")]
        public string Client { get; set; }
        [XmlElement(ElementName = "buysell")]
        public string Buysell { get; set; }
        [XmlElement(ElementName = "union")]
        public string Union { get; set; }
        [XmlElement(ElementName = "time")]
        public string Time { get; set; }
        [XmlElement(ElementName = "brokerref")]
        public string Brokerref { get; set; }
        [XmlElement(ElementName = "value")]
        public string Value { get; set; }
        [XmlElement(ElementName = "comission")]
        public string Comission { get; set; }
        [XmlElement(ElementName = "price")]
        public string Price { get; set; }
        [XmlElement(ElementName = "quantity")]
        public string Quantity { get; set; }
        [XmlElement(ElementName = "items")]
        public string Items { get; set; }
        [XmlElement(ElementName = "yield")]
        public string Yield { get; set; }
        [XmlElement(ElementName = "currentpos")]
        public string Currentpos { get; set; }
        [XmlElement(ElementName = "accruedint")]
        public string Accruedint { get; set; }
        [XmlElement(ElementName = "tradetype")]
        public string Tradetype { get; set; }
        [XmlElement(ElementName = "settlecode")]
        public string Settlecode { get; set; }
        [XmlElement(ElementName = "openinterest")]
        public string Openinterest { get; set; }
        [XmlElement(ElementName = "period")]
        public string Period { get; set; }

    }

    [XmlRoot(ElementName = "candle")]
    public class Candle
    {
        [XmlAttribute(AttributeName = "date")]
        public string Date { get; set; }
        [XmlAttribute(AttributeName = "open")]
        public string Open { get; set; }
        [XmlAttribute(AttributeName = "close")]
        public string Close { get; set; }
        [XmlAttribute(AttributeName = "high")]
        public string High { get; set; }
        [XmlAttribute(AttributeName = "oi")]
        public string Oi { get; set; }
        [XmlAttribute(AttributeName = "low")]
        public string Low { get; set; }
        [XmlAttribute(AttributeName = "volume")]
        public string Volume { get; set; }
    }

    [XmlRoot(ElementName = "candles")]
    public class Candles
    {
        [XmlElement(ElementName = "candle")]
        public List<Candle> Candle { get; set; }
        [XmlAttribute(AttributeName = "secid")]
        public string Secid { get; set; }
        [XmlAttribute(AttributeName = "board")]
        public string Board { get; set; }
        [XmlAttribute(AttributeName = "seccode")]
        public string Seccode { get; set; }
        [XmlAttribute(AttributeName = "period")]
        public string Period { get; set; }
        [XmlAttribute(AttributeName = "status")]
        public string Status { get; set; }
    }

    [XmlRoot(ElementName = "message")]
    public class Message
    {
        [XmlElement(ElementName = "date")]
        public string Date { get; set; }
        [XmlElement(ElementName = "urgent")]
        public string Urgent { get; set; }
        [XmlElement(ElementName = "from")]
        public string From { get; set; }
        [XmlElement(ElementName = "text")]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "result")]
    public class Result
    {
        [XmlAttribute(AttributeName = "success")]
        public bool Success { get; set; }
        [XmlAttribute(AttributeName = "transactionid")]
        public int TransactionId { get; set; }
        [XmlElement(ElementName = "message")]
        public string Message { get; set; }
    }

    [XmlRoot(ElementName = "ticks")]
    public class Ticks
    {
        [XmlElement(ElementName = "tick")]
        public List<Tick> Tick { get; set; }
    }

    [XmlRoot(ElementName = "tick")]
    public class Tick
    {
        [XmlAttribute(AttributeName = "secid")]
        public string Secid { get; set; }
        [XmlAttribute(AttributeName = "tradeno")]
        public string Tradeno { get; set; }
        [XmlAttribute(AttributeName = "tradetime")]
        public string Tradetime { get; set; }
        [XmlAttribute(AttributeName = "price")]
        public string Price { get; set; }
        [XmlAttribute(AttributeName = "quantity")]
        public string Quantity { get; set; }
        [XmlAttribute(AttributeName = "period")]
        public string Period { get; set; }
        [XmlAttribute(AttributeName = "buysell")]
        public string Buysell { get; set; }
        [XmlAttribute(AttributeName = "openinterest")]
        public string Openinterest { get; set; }
        [XmlAttribute(AttributeName = "board")]
        public string Board { get; set; }
        [XmlAttribute(AttributeName = "seccode")]
        public string Seccode { get; set; }
    }

}


