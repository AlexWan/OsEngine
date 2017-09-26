using System.Collections.Generic;

namespace OsEngine.Market.Servers.Kraken.KrakenEntity
{
    public class KrakenOrder
    {
        #region Properties used to create an order
        //Asset pair
        public string Pair { get; set; }
        //Type of order (buy or sell)
        public string Type { get; set; }
        //Execution type
        public string OrderType { get; set; }
        //Price. Optional. Dependent upon order type
        public decimal? Price { get; set; }
        //Secondary price. Optional. Dependent upon order type
        public decimal? Price2 { get; set; }
        //Order volume in lots
        public decimal Volume { get; set; }
        //Amount of leverage required. Optional. default none
        public string Leverage { get; set; }
        //Position tx id to close (optional.  used to close positions)
        public string Position { get; set; }
        //list of order flags (optional):
        public string OFlags { get; set; }
        //Scheduled start time. Optional
        public string Starttm { get; set; }
        //Expiration time. Optional
        public string Expiretm { get; set; }
        //User ref id. Optional
        public string Userref { get; set; }
        //Validate inputs only. do not submit order. Optional
        public bool Validate { get; set; }
        //Closing order details
        public Dictionary<string, string> Close { get; set; }

        #endregion

        #region Properties set by Kraken during execution (all nullable)

        //Comma delimited list of transaction ids for order
        public string TxId { get; set; }
        //KrakenOrderStatus
        public string Status { get; set; }
        //Reason
        public string Reason { get; set; }
        //unix timestamp of when order was placed
        public string OpenTime { get; set; }
        //unix timestamp of when order was closed
        public string CloseTime { get; set; }
        //Volume executed
        public double? VolumeExecuted { get; set; }
        //Total cost
        public decimal? Cost { get; set; }
        //Total fee
        public decimal? Fee { get; set; }
        //AveragePrice executed
        public decimal? AveragePrice { get; set; }
        //stop price (for trailing stops)
        public decimal? StopPrice { get; set; }
        //Triggered limit price (when limit based ordertype triggered)
        public decimal? LimitPrice { get; set; }
        //Comma delimited list of miscellaneous info
        public string Info { get; set; }
        //Comma delimited list of trade ids related to order 
        public string Trades { get; set; }

        #endregion
    }

    public enum OrderTypeKraken
    {
        buy = 1,
        sell = 2
    }

    public enum KrakenOrderType
    {
        market = 1,
        limit = 2,// (price = limit price)
        stop_loss = 3, // (price = stop loss price)
        take_profit = 4, // (price = take profit price)
        stop_loss_profit = 5, // (price = stop loss price, price2 = take profit price)
        stop_loss_profit_limit = 6, // (price = stop loss price, price2 = take profit price)
        stop_loss_limit = 7,// (price = stop loss trigger price, price2 = triggered limit price)
        take_profit_limit = 8, // (price = take profit trigger price, price2 = triggered limit price)
        trailing_stop = 9, //(price = trailing stop offset)
        trailing_stop_limit = 10,// (price = trailing stop offset, price2 = triggered limit offset)
        stop_loss_and_limit = 11,// (price = stop loss price, price2 = limit price)
    }

    public enum KrakenOrderStatus
    {
        pending = 1, // order pending book entry
        open = 2, // open order
        closed = 3, //cosed order
        canceled = 4, // order canceled
        expired = 5 // order expired
    }

    public enum OFlag
    {
        viqc = 1, //volume in quote currency
        plbc = 2, //prefer profit/los in base currency
        nompp = 3 //no market price protection
    }
}
