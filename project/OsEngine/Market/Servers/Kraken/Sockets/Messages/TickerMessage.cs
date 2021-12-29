using Newtonsoft.Json.Linq;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Ticker information includes best ask and best bid prices, 24hr volume, last trade price, volume weighted average price, 
    /// etc for a given currency pair. A ticker message is published every time a trade or a group of trade happens.
    /// </summary>
    public sealed class TickerMessage
    {
        /// <summary>
        /// Gets the channel identifier.
        /// </summary>
        /// <value>
        /// The channel identifier.
        /// </value>
        public int ChannelId { get; private set; }

        /// <summary>
        /// Gets the pair.
        /// </summary>
        /// <value>
        /// The pair.
        /// </value>
        public string Pair { get; private set; }

        /// <summary>
        /// Gets the ask.
        /// </summary>
        /// <value>
        /// The ask.
        /// </value>
        public BestPriceVolume Ask { get; private set; }

        /// <summary>
        /// Gets the bid.
        /// </summary>
        /// <value>
        /// The bid.
        /// </value>
        public BestPriceVolume Bid { get; private set; }

        /// <summary>
        /// Gets the close.
        /// </summary>
        /// <value>
        /// The close.
        /// </value>
        public ClosePriceVolume Close { get; private set; }

        /// <summary>
        /// Gets the volume.
        /// </summary>
        /// <value>
        /// The volume.
        /// </value>
        public TodayAnd24HourValue<decimal> Volume { get; private set; }

        /// <summary>
        /// Gets the volume weighted average price.
        /// </summary>
        /// <value>
        /// The volume weighted average price.
        /// </value>
        public TodayAnd24HourValue<decimal> VolumeWeightedAveragePrice { get; private set; }

        /// <summary>
        /// Gets the number of trades
        /// </summary>
        public TodayAnd24HourValue<int> Trades { get; private set; }
        
        /// <summary>
        /// Gets the low price.
        /// </summary>
        /// <value>
        /// The low price.
        /// </value>
        public TodayAnd24HourValue<decimal> LowPrice { get; private set; }
        
        /// <summary>
        /// Gets the high price.
        /// </summary>
        /// <value>
        /// The high price.
        /// </value>
        public TodayAnd24HourValue<decimal> HighPrice { get; private set; }
        
        /// <summary>
        /// Gets the open price.
        /// </summary>
        /// <value>
        /// The open price.
        /// </value>
        public TodayAnd24HourValue<decimal> OpenPrice { get; private set; }

        /// <summary>
        /// Creates a new <see cref="TickerMessage"/> insdtance from string.
        /// </summary>
        /// <param name="tickerRawMessage">The ticker raw message.</param>
        /// <param name="subscription">The subscription.</param>
        /// <returns></returns>
        public static TickerMessage CreateFromString(string tickerRawMessage, SubscriptionStatus subscription)
        {
            var tokenArray = KrakenDataMessageHelper.EnsureRawMessage(tickerRawMessage);
            var channelId = (int)tokenArray.First;
            var message = tokenArray[1];

            var tickerMessage = new TickerMessage()
            {
                ChannelId = channelId,
                Pair = subscription.Pair,
                Ask = BestPriceVolume.CreateFromJArray(message["a"] as JArray),
                Bid = BestPriceVolume.CreateFromJArray(message["b"] as JArray),
                Close = ClosePriceVolume.CreateFromJArray(message["c"] as JArray),
                Volume = TodayAnd24HourValue<decimal>.CreateFromJArray(message["v"] as JArray),
                VolumeWeightedAveragePrice = TodayAnd24HourValue<decimal>.CreateFromJArray(message["p"] as JArray),
                Trades = TodayAnd24HourValue<int>.CreateFromJArray(message["t"] as JArray),
                LowPrice = TodayAnd24HourValue<decimal>.CreateFromJArray(message["l"] as JArray),
                HighPrice = TodayAnd24HourValue<decimal>.CreateFromJArray(message["h"]as JArray),
                OpenPrice = TodayAnd24HourValue<decimal>.CreateFromJArray(message["o"] as JArray)
            };

            return tickerMessage;
        }
    }
}
