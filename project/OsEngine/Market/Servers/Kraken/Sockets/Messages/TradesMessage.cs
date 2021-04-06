using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Trade feed for a currency pair.
    /// </summary>
    public sealed class TradeMessage
    {
        /// <summary>
        /// Gets the ChannelID of pair-trade subscription.
        /// </summary>
        /// <value>
        /// The ChannelID of pair-trade subscription.
        /// </value>
        public long ChannelId { get; private set; }

        /// <summary>
        /// Gets the Array of trades.
        /// </summary>
        /// <value>
        /// The Array of trades.
        /// </value>
        public TradeValues[] Trades { get; private set; }

        /// <summary>
        /// Prevents a default instance of the <see cref="TradeMessage"/> class from being created.
        /// </summary>
        private TradeMessage()
        {
        }

        /// <summary>
        /// Creates from string.
        /// </summary>
        /// <param name="rawMessage">The raw message.</param>
        /// <returns></returns>
        public static TradeMessage CreateFromString(string rawMessage)
        {
            var message = KrakenDataMessageHelper.EnsureRawMessage(rawMessage);
            return new TradeMessage()
            {
                ChannelId = Convert.ToInt64(message.First),
                Trades = ((JArray)message[1]).OfType<JArray>().Select(tradeJArray => TradeValues.CreateFromJArray(tradeJArray)).ToArray()
            };
        }
    }
}
