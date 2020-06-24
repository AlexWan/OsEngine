using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Huobi.Response
{
    public class SubscribeDepthResponse : WebSocketResponseBase
    {
        /// <summary>
        /// Response body from req
        /// </summary>
        public Tick data;

        /// <summary>
        /// Response body from sub
        /// </summary>
        public Tick tick;

        public class Tick
        {
            /// <summary>
            /// Timestamp in millionsecond
            /// </summary>
            public long ts;

            /// <summary>
            /// Internal data
            /// </summary>
            public long version;

            /// <summary>
            /// The current all bids in format [price, quote volume]
            /// </summary>
            public decimal[][] bids;

            /// <summary>
            /// The current all asks in format [price, quote volume]
            /// </summary>
            public decimal[][] asks;
        }
    }
}
