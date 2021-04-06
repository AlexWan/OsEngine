using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kraken.WebSockets.Messages;
using Newtonsoft.Json.Linq;
using Kraken.WebSockets.Events;
using Kraken.WebSockets.Sockets;


namespace Kraken.WebSockets
{
    /// <summary>
    /// Kraken websocket.
    /// </summary>
    public sealed class KrakenSocket : IKrakenSocket
    {

        private static readonly Encoding DEFAULT_ENCODING = Encoding.UTF8;


        private readonly IWebSocket webSocket;
        private readonly string uri;
        private readonly IKrakenMessageSerializer serializer;

        /// <summary>
        /// Occurs when connected.
        /// </summary>
        public event EventHandler Connected;

        /// <summary>
        /// Occurs when data received.
        /// </summary>
        public event EventHandler<KrakenMessageEventArgs> DataReceived;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Kraken.WebSockets.KrakenWebsocket" /> class.
        /// </summary>
        /// <param name="uri">URI.</param>
        /// <param name="serializer">The serializer.</param>
        /// <param name="webSocket">The web socket.</param>
        /// <exception cref="ArgumentNullException">
        /// uri
        /// or
        /// serializer
        /// or
        /// webSocket
        /// </exception>
        public KrakenSocket(string uri, IKrakenMessageSerializer serializer, IWebSocket webSocket)
        {
            this.uri = uri ?? throw new ArgumentNullException(nameof(uri));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        }

        /// <summary>
        /// Connect to the websocket server.
        /// </summary>
        /// <param name="cancellationToken"></param>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
               // logger?.LogInformation("Trying to connect to '{uri}'", uri);

                await webSocket.ConnectAsync(new Uri(uri), cancellationToken);
                InvokeConnected();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                StartListening();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
            catch (Exception ex)
            {
                //logger?.LogError(ex, "Error while connecting to '{uri}'", uri);
                throw;
            }
        }

        /// <summary>
        /// Sends the message throught the open websocket.
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="message">Message.</param>
        public async Task SendAsync<TKrakenMessage>(TKrakenMessage message, CancellationToken cancellationToken = default)
            where TKrakenMessage : class, IKrakenMessage
        {
            if (webSocket.State == WebSocketState.Open)
            {
                var jsonMessage = serializer.Serialize(message);

              //  logger?.LogTrace("Trying to send: {@message}", jsonMessage);

                var messageBytes = DEFAULT_ENCODING.GetBytes(jsonMessage);
                await webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cancellationToken);
               // logger?.LogTrace("Successfully sent: {@message}", jsonMessage);
                return;
            }

            //logger?.LogWarning("WebSocket is not open. Current state: {state}",
            //    webSocket.State);
        }

        /// <summary>
        /// Closes the websocket.
        /// </summary>
        /// <returns>The async.</returns>
        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed by consumer", cancellationToken);
            }
        }

        #region Private Helper

#pragma warning disable S3241 // Methods should not return values that are never used
        private async Task StartListening(CancellationToken cancellationToken = default)
#pragma warning restore S3241 // Methods should not return values that are never used
        {
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                   // logger?.LogDebug("Waiting for new message");
                    var message = await ReadNextMessage(cancellationToken);

                   // logger?.LogDebug("Received new message from websocket");
                   // logger?.LogTrace("Received: '{message}'", message);

                    string eventString = null;
                    int? channelId = null;

                    if (!string.IsNullOrEmpty(message))
                    {
                        var token = JToken.Parse(message);
                        switch (token)
                        {
                            case JObject _:
                                var messageObj = JObject.Parse(message);
                                eventString = (string)messageObj.GetValue("event");
                                break;

                            case JArray arrayToken:
                                // Data / private messages
                                if (int.TryParse(arrayToken.First.ToString(), out var localChannelId))
                                {
                                    channelId = localChannelId;
                                }

                                eventString = channelId != null ? "data" : "private";
                                break;
                        }

                        InvokeDataReceived(new KrakenMessageEventArgs(eventString, message, channelId));
                    }
                }
            }
            catch (Exception ex)
            {
               // logger?.LogError(ex, "Error while listening or reading new messages from WebSocket");
                // TODO: Disconnected-Event
                throw;
            }
            finally
            {
              //  logger?.LogInformation("Closing WebSocket");
                webSocket.Dispose();
            }
        }

        private async Task<string> ReadNextMessage(CancellationToken cancellationToken = default)
        {
            var buffer = new byte[1024];
            var messageParts = new StringBuilder();

            WebSocketReceiveResult result;
            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                  //  logger?.LogDebug("Closing connection to socket");
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken);
                  //  logger?.LogDebug("Connection successfully closed");
                    // TODO: Disconnected-Event
                }
                else
                {
                    var str = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    messageParts.Append(str);
                }

            } while (!result.EndOfMessage);

            var message = messageParts.ToString();
            return message;
        }

        private void InvokeConnected()
        {
            var connectedHandler = Connected;
            if (connectedHandler == null) return;

            InvokeAllHandlers(connectedHandler.GetInvocationList(), new EventArgs());
        }

        private void InvokeDataReceived(KrakenMessageEventArgs krakenMessageEventArgs)
        {
            var dataReceivedHandler = DataReceived;
            if (dataReceivedHandler == null) return;

            InvokeAllHandlers(dataReceivedHandler.GetInvocationList(), krakenMessageEventArgs);
        }

        private void InvokeAllHandlers(Delegate[] handlers, EventArgs eventArgs)
        {
            if (handlers != null)
            {
                foreach (var handler in handlers)
                {
                    handler.DynamicInvoke(this, eventArgs);
                }
            }
        }

        #endregion
    }
}
