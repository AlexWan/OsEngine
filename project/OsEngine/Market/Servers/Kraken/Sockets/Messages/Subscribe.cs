using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Subscribe message to be send to the websocket.
    /// </summary>
    public class Subscribe : KrakenMessage
    {
        /// <summary>
        /// The subscribe message key.
        /// </summary>
        internal const string SubscribeMessageKey = "subscribe";

        /// <summary>
        /// Gets the pairs.
        /// </summary>
        /// <value>The pairs.</value>
        [JsonProperty("pair")]
        public IEnumerable<string> Pairs { get; }

        /// <summary>
        /// Gets the options.
        /// </summary>
        /// <value>The options.</value>
        [JsonProperty("subscription")]
        public SubscribeOptions Options { get; }

        /// <summary>
        /// Gets the request identifier.
        /// </summary>
        /// <value>
        /// The request identifier.
        /// </value>
        [JsonProperty("reqid")]
        public int? RequestId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Kraken.WebSockets.Messages.Subscribe"/> class.
        /// </summary>
        /// <param name="pairs">Pairs.</param>
        /// <param name="options">Options.</param>
        /// <param name="requestId">Requst identifier.</param>
        public Subscribe(IEnumerable<string> pairs, SubscribeOptions options, int? requestId = null)
            : base(SubscribeMessageKey)
        {
            Pairs = pairs;
            Options = options ?? throw new ArgumentNullException(nameof(options));
            RequestId = requestId;
        }
    }
}
