using Newtonsoft.Json.Linq;
using System;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Spread feed to show best bid and ask price for a currency pair
    /// </summary>
    public sealed class SpreadMessage
    {
        /// <summary>
        /// Gets the ChannelID of pair-spreads subscription.
        /// </summary>
        /// <value>
        /// The ChannelID of pair-spreads subscription.
        /// </value>
        public long ChannelId { get; private set; }

        /// <summary>
        /// Gets the Bid price.
        /// </summary>
        /// <value>
        /// The Bid price.
        /// </value>
        public decimal Bid { get; private set; }

        /// <summary>
        /// Gets the Ask price.
        /// </summary>
        /// <value>
        /// The Ask price.
        /// </value>
        public decimal Ask { get; private set; }

        /// <summary>
        /// Gets the Time, seconds since epoch.
        /// </summary>
        /// <value>
        /// The Time, seconds since epoch.
        /// </value>
        public decimal Time { get; private set; }

        /// <summary>
        /// Prevents a default instance of the <see cref="SpreadMessage"/> class from being created.
        /// </summary>
        private SpreadMessage()
        {
        }

        /// <summary>
        /// Creates from string.
        /// </summary>
        /// <param name="rawSpreadMessage">The raw spread message.</param>
        /// <returns></returns>
        public static SpreadMessage CreateFromString(string rawSpreadMessage)
        {
            var spreadMessage = KrakenDataMessageHelper.EnsureRawMessage(rawSpreadMessage);
            var spreadTokens = spreadMessage[1] as JArray;
            return new SpreadMessage
            {
                ChannelId = Convert.ToInt64(spreadMessage.First),
                Bid = Convert.ToDecimal(spreadTokens[0]),
                Ask = Convert.ToDecimal(spreadTokens[1]),
                Time = Convert.ToDecimal(spreadTokens[2]),
            };
        }
    }
}
