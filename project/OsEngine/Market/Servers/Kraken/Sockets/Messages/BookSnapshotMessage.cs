using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Order book levels. On subscription, a snapshot will be published at the specified depth.
    /// </summary>
    public sealed class BookSnapshotMessage
    {
        /// <summary>
        /// Gets the ChannelID of pair-order book levels subscription.
        /// </summary>
        /// <value>
        /// The ChannelID of pair-order book levels subscription.
        /// </value>
        public long ChannelId { get; private set; }

        /// <summary>
        /// Gets the Array of price levels, ascending from best ask.
        /// </summary>
        /// <value>
        /// The Array of price levels, ascending from best ask.
        /// </value>
        public PriceLevel[] Asks { get; private set; }

        /// <summary>
        /// Gets the Array of price levels, descending from best bid.
        /// </summary>
        /// <value>
        /// The Array of price levels, descending from best bid.
        /// </value>
        public PriceLevel[] Bids { get; private set; }

        /// <summary>
        /// Prevents a default instance of the <see cref="BookSnapshotMessage"/> class from being created.
        /// </summary>
        private BookSnapshotMessage()
        {
        }

        /// <summary>
        /// Creates from string.
        /// </summary>
        /// <param name="rawBookSnapshotMessage">The raw book snapshot message.</param>
        /// <returns></returns>
        public static BookSnapshotMessage CreateFromString(string rawBookSnapshotMessage)
        {
            var bookSnapshotTokens = KrakenDataMessageHelper.EnsureRawMessage(rawBookSnapshotMessage);
            var detailTokens = (JObject)bookSnapshotTokens[1];
            return new BookSnapshotMessage
            {
                ChannelId = Convert.ToInt64(bookSnapshotTokens.First),
                Asks = ((JArray)detailTokens["as"]).OfType<JArray>().Select(level => PriceLevel.CreateFromJArray(level)).ToArray(),
                Bids = ((JArray)detailTokens["bs"]).OfType<JArray>().Select(level => PriceLevel.CreateFromJArray(level)).ToArray()
            };
        }
    }
}
