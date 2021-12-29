using System;

namespace Kraken.WebSockets.Messages
{
    /// <summary>
    /// Base class for private messages sent to the Kraken Websockets API
    /// </summary>
    /// <seealso cref="Kraken.WebSockets.Messages.KrakenMessage" />
    public abstract class PrivateKrakenMessage : KrakenMessage
    {
        /// <summary>
        /// Gets the authentication token.
        /// </summary>
        /// <value>
        /// The authentication token.
        /// </value>
        public string Token { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivateKrakenMessage"/> class.
        /// </summary>
        /// <param name="eventType">Type of the event.</param>
        /// <param name="token">The token.</param>
        /// <exception cref="ArgumentNullException">If token is null</exception>
        protected PrivateKrakenMessage(string eventType, string token) 
            : base(eventType)
        {
            Token = token ?? throw new ArgumentNullException(nameof(token));
        }
    }
}
