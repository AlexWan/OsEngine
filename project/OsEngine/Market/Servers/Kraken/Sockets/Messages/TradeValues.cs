using System;
using Newtonsoft.Json.Linq;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Trade values
    /// </summary>
    public sealed class TradeValues
    {
        /// <summary>
        /// Gets the price.
        /// </summary>
        /// <value>
        /// The price.
        /// </value>
        public decimal Price { get; private set; }

        /// <summary>
        /// Gets the volume.
        /// </summary>
        /// <value>
        /// The volume.
        /// </value>
        public decimal Volume { get; private set; }

        /// <summary>
        /// Gets the Time, seconds since epoch.
        /// </summary>
        /// <value>
        /// The Time, seconds since epoch.
        /// </value>
        public decimal Time { get; private set; }

        /// <summary>
        /// Gets the Triggering order side (buy/sell), values: b|s.
        /// </summary>
        /// <value>
        /// The Triggering order side (buy/sell), values: b|s.
        /// </value>
        public string Side { get; private set; }

        /// <summary>
        /// Gets the Triggering order type (market/limit), values: m|l.
        /// </summary>
        /// <value>
        /// The Triggering order type (market/limit), values: m|l.
        /// </value>
        public string OrderType { get; private set; }

        /// <summary>
        /// Gets the Miscellaneous.
        /// </summary>
        /// <value>
        /// The Miscellaneous.
        /// </value>
        public string Misc { get; private set; }

        /// <summary>
        /// Prevents a default instance of the <see cref="TradeValues"/> class from being created.
        /// </summary>
        private TradeValues()
        {
        }

        public static TradeValues CreateFromJArray(JArray tradeValueTokens)
        {
            return new TradeValues
            {
                Price = Convert.ToDecimal(tradeValueTokens[0]),
                Volume = Convert.ToDecimal(tradeValueTokens[1]),
                Time = Convert.ToDecimal(tradeValueTokens[2]),
                Side = Convert.ToString(tradeValueTokens[3]),
                OrderType = Convert.ToString(tradeValueTokens[4]),
                Misc = Convert.ToString(tradeValueTokens[5]),
            };
        }
    }
}