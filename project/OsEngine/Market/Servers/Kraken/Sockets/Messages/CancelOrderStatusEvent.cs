using Kraken.WebSockets.Converters;
using Newtonsoft.Json;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Response event to a <see cref="CancelOrderCommand"/>
    /// </summary>
    /// <seealso cref="KrakenMessage" />
    public sealed class CancelOrderStatusEvent : KrakenMessage
    {
        public const string EventName = "cancelOrderStatus";

        /// <summary>
        /// Prevents a default instance of the <see cref="CancelOrderStatusEvent"/> class from being created.
        /// </summary>
        public CancelOrderStatusEvent()
            : base(EventName)
        { }

        /// <summary>
        /// Gets or sets the request identifier.
        /// </summary>
        /// <remarks>
        /// Optional - client originated requestID sent as acknowledgment in the message response
        /// </remarks>
        /// <value>
        /// The request identifier.
        /// </value>
        [JsonProperty("reqid")]
        public int? RequestId { get; set; }

        /// <summary>
        /// Gets the status.
        /// </summary>
        /// <remarks>
        ///  "ok" or "error"
        /// </remarks>
        /// <value>
        /// The status.
        /// </value>
        [JsonProperty("status")]
        [JsonConverter(typeof(StatusConverter))]
        public Status Status { get; private set; }

        /// <summary>
        /// Gets the error message (if unsuccessful)
        /// </summary>
        /// <value>
        /// The error message (if unsuccessful)
        /// </value>
        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; private set; }
    }
}
