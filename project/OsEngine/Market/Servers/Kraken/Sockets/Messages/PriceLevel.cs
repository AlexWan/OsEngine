using Newtonsoft.Json.Linq;
using System;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Price level information
    /// </summary>
    public sealed class PriceLevel
    {
        /// <summary>
        /// Gets the Price level.
        /// </summary>
        /// <value>
        /// The Price level.
        /// </value>
        public decimal Price { get; private set; }

        /// <summary>
        /// Gets the Price level volume..
        /// </summary>
        /// <value>
        /// The Price level volume..
        /// </value>
        public decimal Volume { get; private set; }

        /// <summary>
        /// Gets the Price level last updated, seconds since epoch.
        /// </summary>
        /// <value>
        /// The Price level last updated, seconds since epoch.
        /// </value>
        public decimal Timestamp { get; private set; }

        /// <summary>
        /// Prevents a default instance of the <see cref="PriceLevel"/> class from being created.
        /// </summary>
        private PriceLevel()
        {
        }

        /// <summary>
        /// Creates from j array.
        /// </summary>
        /// <param name="priceLevelTokens">The price level tokens.</param>
        /// <returns></returns>
        public static PriceLevel CreateFromJArray(JArray priceLevelTokens)
        {
            return new PriceLevel
            {
                Price = Convert.ToDecimal(priceLevelTokens[0]),
                Volume = Convert.ToDecimal(priceLevelTokens[1]),
                Timestamp = Convert.ToDecimal(priceLevelTokens[2]),
            };
        }
    }
}