using Newtonsoft.Json;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// This class represents the "umsubscribe" message to be send to the 
    /// websocket API
    /// </summary>
    /// <seealso cref="KrakenMessage" />
    public sealed class Unsubscribe : KrakenMessage
    {
        internal const string EventName = "unsubscribe";

        /// <summary>
        /// Gets the channel identifier.
        /// </summary>
        /// <value>
        /// The channel identifier.
        /// </value>
        [JsonProperty("channelID")]
        public int ChannelId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Unsubscribe"/> class.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        public Unsubscribe(int channelId) 
            : base(EventName)
        {
            ChannelId = channelId;
        }
    }
}
