using Newtonsoft.Json;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Subscription status.
    /// </summary>
    public class SubscriptionStatus : KrakenMessage
    {
        public const string EventName = "subscriptionStatus";

        /// <summary>
        /// Gets the channel identifier.
        /// </summary>
        /// <value>The channel identifier.</value>
        [JsonProperty("channelID")]
        public int? ChannelId { get; internal set; }

        /// <summary>
        /// Gets the status.
        /// </summary>
        /// <value>The status.</value>
        [JsonProperty("status")]
        public string Status { get; internal set; }

        /// <summary>
        /// Gets the pair.
        /// </summary>
        /// <value>The pair.</value>
        [JsonProperty("pair")]
        public string Pair { get; internal set; }

        /// <summary>
        /// Gets the request identifier.
        /// </summary>
        /// <value>The request identifier.</value>
        [JsonProperty("reqid")]
        public int? RequestId { get; internal set; }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        /// <value>The error message.</value>
        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; internal set; }

        /// <summary>
        /// Gets the subscription.
        /// </summary>
        /// <value>
        /// The subscription.
        /// </value>
        [JsonProperty("subscription")]
        public SubscribeOptions Subscription { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Kraken.WebSockets.Subscription"/> class.
        /// </summary>
        public SubscriptionStatus()
            : base(EventName)
        {
        }
    }
}