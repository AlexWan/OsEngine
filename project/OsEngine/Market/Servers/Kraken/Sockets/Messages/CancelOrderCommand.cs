using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Cancel order or list of orders.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For every cancelOrder message, an update message 'closeOrderStatus' is sent. 
    /// For multiple orderid in cancelOrder, multiple update messages for 'closeOrderStatus' will be sent.
    /// </para>
    /// <para>
    /// For example, if a cancelOrder request is sent for cancelling three orders[A, B, C], then if two 
    /// update messages for 'closeOrderStatus' are received along with an error such as 'EOrder: Unknown order', 
    /// then it would imply that the third order is not cancelled.The error message could be different based on 
    /// the condition which was not met by the 'cancelOrder' request.
    /// </para>
    /// </remarks>
    /// <seealso cref="Kraken.WebSockets.Messages.PrivateKrakenMessage" />
    public class CancelOrderCommand : PrivateKrakenMessage
    {
        /// <summary>
        /// The event name
        /// </summary>
        public const string EventName = "cancelOrder";

        /// <summary>
        /// Initializes a new instance of the <see cref="CancelOrderCommand"/> class.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="transactions">The transactions.</param>
        /// <exception cref="ArgumentNullException">transactions</exception>
        public CancelOrderCommand(string token, IEnumerable<string> transactions)
            : base(EventName, token)
        {
            Transactions = transactions?.ToArray() ?? throw new ArgumentNullException(nameof(transactions));
        }

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
        /// Gets the transactions.
        /// </summary>
        /// <remarks>
        /// Array of order IDs to be canceled. These can be user reference IDs.
        /// </remarks>
        /// <value>
        /// The transactions.
        /// </value>
        [JsonProperty("txid")]
        public string[] Transactions { get; }
    }
}