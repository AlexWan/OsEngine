using System;

namespace Kraken.WebSockets.Events
{
    /// <summary>
    /// Kraken message event arguments.
    /// </summary>
    public class KrakenMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the event.
        /// </summary>
        /// <value>The event.</value>
        /// <remarks>Can be null or empty string</remarks>
        public string Event { get; }

        /// <summary>
        /// Gets the channel identifier.
        /// </summary>
        /// <value>
        /// The channel identifier.
        /// </value>
        public int? ChannelId { get; }

        /// <summary>
        /// Gets the raw content of the message.
        /// </summary>
        /// <value>The content of the raw.</value>
        /// <remarks>Can be null or empty string</remarks>
        public string RawContent { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Kraken.WebSockets.Events.KrakenMessageEventArgs"/> class.
        /// </summary>
        /// <param name="event">Event identifier</param>
        /// <param name="rawContent">Raw content.</param>
        public KrakenMessageEventArgs(string @event, string rawContent, int? channelId = null)
        {
            Event = @event;
            RawContent = rawContent;
            ChannelId = channelId;
        }
    }
}
 