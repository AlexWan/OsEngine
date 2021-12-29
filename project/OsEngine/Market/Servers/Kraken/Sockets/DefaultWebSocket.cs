using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Kraken.WebSockets.Sockets
{
    public sealed class DefaultWebSocket : IWebSocket
    {
        private readonly ClientWebSocket webSocket;

        internal DefaultWebSocket(ClientWebSocket webSocket)
        {
            this.webSocket = webSocket;
        }

        public WebSocketState State => webSocket.State;

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
            => webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
            => webSocket.ConnectAsync(uri, cancellationToken);

        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            => webSocket.ReceiveAsync(buffer, cancellationToken);

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            => webSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    webSocket.Dispose();
                }

                disposedValue = true;
            }
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
