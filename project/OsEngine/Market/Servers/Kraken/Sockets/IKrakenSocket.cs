using System;
using System.Threading;
using System.Threading.Tasks;
using Kraken.WebSockets.Messages;
using Kraken.WebSockets.Events;

namespace Kraken.WebSockets
{
    /// <summary>
    /// This interface describes a Kraken socket.
    /// </summary>
    public interface IKrakenSocket
    {
        /// <summary>
        /// Occurs when connected.
        /// </summary>
        event EventHandler Connected;

        /// <summary>
        /// Occurs when data received.
        /// </summary>
        event EventHandler<KrakenMessageEventArgs> DataReceived;

        /// <summary>
        /// Connect to the websocket server.
        /// </summary>
        /// <returns>The connect.</returns>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends the message throught the open websocket.
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="message">Message.</param>
        Task SendAsync<TKrakenMessage>(TKrakenMessage message, CancellationToken cancellationToken = default)
            where TKrakenMessage : class, IKrakenMessage;

        /// <summary>
        /// Closes the websocket.
        /// </summary>
        /// <returns>The async.</returns>
        Task CloseAsync(CancellationToken cancellationToken = default);
    }
}