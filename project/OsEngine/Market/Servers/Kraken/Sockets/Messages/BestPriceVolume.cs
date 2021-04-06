using Newtonsoft.Json.Linq;
using System;

namespace Kraken.WebSockets.Messages
{
    public class BestPriceVolume
    {
        /// <summary>
        /// Gets the best price.
        /// </summary>
        /// <value>
        /// The best price.
        /// </value>
        public decimal BestPrice { get; private set; }

        /// <summary>
        /// Gets the whole lot volume.
        /// </summary>
        /// <value>
        /// The whole lot volume.
        /// </value>
        public int WholeLotVolume { get; private set; }

        /// <summary>
        /// Gets or sets the lot volume.
        /// </summary>
        /// <value>
        /// The lot volume.
        /// </value>
        public decimal LotVolume { get; private set; }
        
        /// <summary>
        /// Creates from j array.
        /// </summary>
        /// <param name="tokenArray">The token array.</param>
        /// <returns></returns>
        internal static BestPriceVolume CreateFromJArray(JArray tokenArray)
        {
            return new BestPriceVolume
            {
                BestPrice = Convert.ToDecimal(tokenArray[0]),
                WholeLotVolume = Convert.ToInt32(tokenArray[1]),
                LotVolume = Convert.ToDecimal(tokenArray[2])
            };
        }
    }
}