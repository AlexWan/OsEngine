using Kraken.WebSockets.Messages;

namespace Kraken.WebSockets.Events
{
    public sealed class OhlcEventArgs : KrakenDataEventArgs<OhlcMessage>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OhlcEventArgs"/> class.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="pair">The pair.</param>
        /// <param name="dataMessage">The data message.</param>
        public OhlcEventArgs(int channelId, string pair, OhlcMessage dataMessage) 
            : base(channelId, pair, dataMessage)
        {
        }
    }
}