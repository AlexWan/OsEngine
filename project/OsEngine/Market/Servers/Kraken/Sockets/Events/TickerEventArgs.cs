using Kraken.WebSockets.Messages;

namespace Kraken.WebSockets.Events
{
    /// <summary>
    /// The information received for a ticker subscription.
    /// </summary>
    public class TickerEventArgs : KrakenDataEventArgs<TickerMessage>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TickerEventArgs" /> class.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="pair">The pair.</param>
        /// <param name="ticker">The ticker.</param>
        public TickerEventArgs(int channelId, string pair, TickerMessage ticker)
            :base(channelId, pair, ticker)
        {
        }
    }
}