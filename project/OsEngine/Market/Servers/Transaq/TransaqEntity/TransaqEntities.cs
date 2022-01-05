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
}


