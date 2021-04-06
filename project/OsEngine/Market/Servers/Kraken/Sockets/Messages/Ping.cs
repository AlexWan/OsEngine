using Newtonsoft.Json;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Ping message.
    /// </summary>
    public class Ping : KrakenMessage
    {
        private const string EventName = "ping";

        /// <summary>
        /// Gets the request identifier.
        /// </summary>
        /// <value>The request identifier.</value>
        [JsonProperty("reqid", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
        public int? RequestId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Kraken.WebSockets.Messages.Ping"/> class.
        /// </summary>
        public Ping() : base(EventName)
        { }
    }
}
