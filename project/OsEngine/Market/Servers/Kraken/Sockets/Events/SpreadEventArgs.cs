using Kraken.WebSockets.Messages;

namespace Kraken.WebSockets.Events
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="Events.KrakenDataEventArgs{SpreadMessage}" />
    public sealed class SpreadEventArgs : KrakenDataEventArgs<SpreadMessage>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpreadEventArgs"/> class.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="pair">The pair.</param>
        /// <param name="dataMessage">The data message.</param>
        public SpreadEventArgs(int channelId, string pair, SpreadMessage dataMessage) 
            : base(channelId, pair, dataMessage)
        {
        }
    }
}
