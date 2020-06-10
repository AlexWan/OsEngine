namespace OsEngine.Market.Servers.Huobi.Response
{
    public class GetCandlestickResponse
    {
        public string security;

        /// <summary>
        /// Response status
        /// </summary>
        public string status;

        /// <summary>
        /// The data stream
        /// </summary>
        public string rep;

        /// <summary>
        /// The timestamp (millisecond) when API respond
        /// </summary>
        public long ts;

        /// <summary>
        /// Response body
        /// </summary>
        public Candlestick[] data;

        public string GetTimeFrame()
        {
            return rep.Split('.')[3];
        }

        public string GetSecurity()
        {
            return rep.Split('.')[1];
        }

        /// <summary>
        /// Candlestick detail
        /// </summary>
        public class Candlestick
        {
            /// <summary>
            /// Unix timestamp in seconds
            /// </summary>
            public int id;

            /// <summary>
            /// The aggregated trading volume in USDT
            /// </summary>
            public decimal amount;

            /// <summary>
            /// The number of completed trades
            /// </summary>
            public int count;

            /// <summary>
            /// The opening price
            /// </summary>
            public decimal open;

            /// <summary>
            /// The closing price
            /// </summary>
            public decimal close;

            /// <summary>
            /// The low price
            /// </summary>
            public decimal low;

            /// <summary>
            /// The high price
            /// </summary>
            public decimal high;

            /// <summary>
            /// The trading volume in base currency
            /// </summary>
            public decimal vol;
        }
    }
}
