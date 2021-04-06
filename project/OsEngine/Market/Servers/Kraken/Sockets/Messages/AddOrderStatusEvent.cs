using Kraken.WebSockets.Converters;
using Newtonsoft.Json;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Response payload of a <see cref="AddOrderCommand"/>
    /// </summary>
    /// <seealso cref="KrakenMessage" />
    public sealed class AddOrderStatusEvent : KrakenMessage
    {
        /// <summary>
        /// The unique event name.
        /// </summary>
        public const string EventName = "addOrderStatus";

        /// <summary>
        /// Initializes a new instance of the <see cref="AddOrderStatusEvent"/> class.
        /// </summary>
        /// <param name="eventType">Event type.</param>
        public AddOrderStatusEvent() : base(EventName)
        { }

        /// <summary>
        /// Gets or sets the request identifier.
        /// </summary>
        /// <remarks>
        /// client originated requestID sent as acknowledgment in the message response
        /// </remarks>
        /// <value>
        /// The request identifier.
        /// </value>
        [JsonProperty("reqid")]
        public int? RequestId { get; set; }

        /// <summary>
        /// Gets the status.
        /// </summary>
        /// <value>
        /// The status.
        /// </value>
        [JsonProperty("status")]
        [JsonConverter(typeof(StatusConverter))]
        public Status Status { get; private set; }

        /// <summary>
        /// Gets the order ID (if successful).
        /// </summary>
        /// <value>
        /// The order ID (if successful).
        /// </value>
        [JsonProperty("txid")]
        public string OrderId { get; private set; }

        /// <summary>
        /// Gets the order description info (if successful).
        /// </summary>
        /// <value>
        /// The order description info (if successful).
        /// </value>
        [JsonProperty("descr")]
        public string Description { get; private set; }

        /// <summary>
        /// Gets the error message. (if unsuccessful)
        /// </summary>
        /// <value>
        /// The error message. (if unsuccessful)
        /// </value>
        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; private set; }
    }
}
