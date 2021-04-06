using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Kraken.WebSockets.Sockets
{
    /// <summary>
    /// General interface for communication over a websocket. (Humble Object Interface)
    /// </summary>
    /// <seealso cref="IDisposable" />
    public interface IWebSocket : IDisposable
    {
        WebSocketState State { get; }

        Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
        Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken);
        Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken);
        Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);
    }
}
