namespace Kraken.WebSockets.Events
{
    public class KrakenDataEventArgs<TData>
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
        /// Gets the data message.
        /// </summary>
        /// <value>
        /// The data message.
        /// </value>
        public TData DataMessage { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KrakenDataEventArgs{TData}"/> class.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="pair">The pair.</param>
        /// <param name="dataMessage">The data message.</param>
        public KrakenDataEventArgs(int channelId, string pair, TData dataMessage)
        {
            ChannelId = channelId;
            Pair = pair;
            DataMessage = dataMessage;
        }
    }
}