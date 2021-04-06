using Kraken.WebSockets.Messages;

namespace Kraken.WebSockets.Events
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="Events.KrakenDataEventArgs{TradesMessage}" />
    public sealed class TradeEventArgs : KrakenDataEventArgs<TradeMessage>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TradeEventArgs"/> class.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="pair">The pair.</param>
        /// <param name="dataMessage">The data message.</param>
        public TradeEventArgs(int channelId, string pair, TradeMessage dataMessage) 
            : base(channelId, pair, dataMessage)
        {
        }
    }
}
