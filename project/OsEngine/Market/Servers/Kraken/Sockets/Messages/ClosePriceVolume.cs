using Newtonsoft.Json.Linq;
using System;

namespace Kraken.WebSockets.Messages
{
    public class ClosePriceVolume
    {
        /// <summary>
        /// Gets the price.
        /// </summary>
        /// <value>
        /// The price.
        /// </value>
        public decimal Price { get; private set; }

        /// <summary>
        /// Gets the lot volume.
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
        public static ClosePriceVolume CreateFromJArray(JArray tokenArray)
        {
            return new ClosePriceVolume
            {
                Price = Convert.ToDecimal(tokenArray[0]),
                LotVolume = Convert.ToDecimal(tokenArray[1])
            };
        }
    }
}
