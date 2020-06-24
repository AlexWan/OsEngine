namespace OsEngine.Market.Servers.Huobi.Response
{
    public class SubscribeTradeResponse : WebSocketResponseBase
    {
        /// <summary>
        /// Response body from req
        /// </summary>
        public Trade[] data;

        /// <summary>
        /// Response body from sub
        /// </summary>
        public Tick tick;

        public class Tick
        {
            public long id;

            public string ts;

            public Trade[] data;
        }

        public class Trade
        {
            /// <summary>
            /// Unique trade id (NEW)
            /// </summary>
            public long tradeid;

            /// <summary>
            /// Last trade volume
            /// </summary>
            public decimal amount;

            /// <summary>
            /// Last trade price
            /// </summary>
            public decimal price;

            /// <summary>
            /// Last trade timestamp in millisecond)
            /// </summary>
            public long ts;

            /// <summary>
            /// Aggressive order side (taker's order side) of the trade
            /// Possible values: [buy, sell]
            /// </summary>
            public string direction;
        }
    }
}
