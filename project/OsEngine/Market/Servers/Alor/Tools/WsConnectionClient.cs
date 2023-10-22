using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OsEngine.Market.Servers.Alor.Tools.Events;
using WebSocketState = System.Net.WebSockets.WebSocketState;

namespace OsEngine.Market.Servers.Alor.Tools
{
    public sealed class WsConnectionClient
    {
        private string Name { get; set; }

        public string WsToken { get; set; }

        private bool _stop;

        /// <summary> Current WebSocket associated with this client connection. </summary>
        private ClientWebSocket _socket;

        /// <summary> Indicates whether connection thread is stopped by users</summary>
        public bool Stop
        {
            get => _stop;
            set
            {
                _stop = value;
                if (!_stop || _socket == null) return;
                _socket.Abort();
                _socket.Dispose();
            }
        }

        /// <summary> This is used to cancel operations when closing the application. </summary>
        private CancellationTokenSource Cts { get; set; }

        /// <summary> URI to connect the WebSocket to. </summary>
        private Uri Host { get; set; }

        private int SendBufferSize { get; set; } = 8192;
        private int RecvBufferSize { get; set; } = 8192;

        /// <summary>
        /// Connection Retry Interval period(in millisecond) after the connection is Aborted or Closed. Adapter will try to reconnecting to the server.
        /// Default value is 3000 which is 3 second.
        /// </summary>
        private int ConnectionRetryInterval { get; set; } = 3000;

        public WsConnectionClient(string name, Uri hostUri)
        {
            Name = name;
            Host = hostUri;
            Cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Creates a WebSocket connection and connecting to the server. Raise connection event.
        /// </summary>
        private async Task Connect()
        {

            _socket = new ClientWebSocket();
            _socket.Options.SetBuffer(RecvBufferSize, SendBufferSize);

            try
            {
                var task = _socket.ConnectAsync(Host, CancellationToken.None);

                if (_socket.State != WebSocketState.Open)
                    RaiseConnectionEvent(DateTime.Now, _socket.State, $"Connecting to {Host}");

                await task;
            }
            catch (Exception ex)
            {
                RaiseConnectionEvent(DateTime.Now, _socket.State, $"Connect():: {ex.Message}");
                RaiseErrorEvent(DateTime.Now, _socket.State, $"Connect():: {ex.Message}");
            }

            if (_socket.State == WebSocketState.Open)
            {
                RaiseConnectionEvent(DateTime.Now, _socket.State, $"Connected to {Host}");

            }
        }

        /// <summary>
        /// Closes the existing connection and creates a new one.
        /// </summary>
        private async Task Reconnect()
        {
            Console.WriteLine("The WebSocket connection is closed for " + Name);
            while (_socket != null && !Stop && _socket.State != WebSocketState.Open)
            {

                Console.WriteLine("Reconnect to the server for " + Name + " after 3 seconds...");
                RaiseConnectionEvent(DateTime.Now, WebSocketState.Connecting, $"Attempting to connect to {Host}");
                Thread.Sleep(ConnectionRetryInterval);
                if (_socket == null) continue;
                _socket.Dispose();
                _socket = null;
                await Connect();
            }
        }

        /// <summary>
        /// The main loop of the WebsocketConnectionClient
        /// </summary>
        public async Task Run(CancellationToken cancellationToken)
        {
            if (Cts != null)
                Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await Task.Factory.StartNew(async () =>
            {
                await Connect();

                while (!Stop && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        ReceiveMessage();

                        if (_socket.State == WebSocketState.Aborted)
                        {
                            await Reconnect().ConfigureAwait(false);
                        }
                    }
                    catch (AggregateException ex)
                    {
                        RaiseErrorEvent(DateTime.Now, _socket.State, ex.Message);
                        RaiseConnectionEvent(DateTime.Now, _socket.State, ex.Message);
                        await Reconnect().ConfigureAwait(false);

                    }
                }

                Console.WriteLine("Exit Task");
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).ConfigureAwait(false);
        }


        /// <summary>Reads data from the WebSocket and parses to JSON message</summary>
        private void ReceiveMessage()
        {
            var readBuffer = new ArraySegment<byte>(new byte[RecvBufferSize]);
            MemoryStream memoryStream = null;
            byte[] dataBuffer = null;

            while (!Stop && !Cts.IsCancellationRequested)
            {
                var result = _socket.ReceiveAsync(readBuffer, Cts.Token);
                if (!result.IsFaulted)
                {
                    if (result.Result.EndOfMessage)
                    {
                        if (memoryStream != null)
                        {
                            memoryStream.Write(readBuffer.Array ?? throw new InvalidOperationException(),
                                readBuffer.Offset, readBuffer.Count);
                            dataBuffer = memoryStream.GetBuffer();
                            memoryStream.Dispose();
                        }
                        else
                        {
                            dataBuffer = readBuffer.Array;
                        }

                        break;
                    }
                    else
                    {
                        memoryStream ??= new MemoryStream(RecvBufferSize * 5);

                        memoryStream.Write(readBuffer.Array ?? throw new InvalidOperationException(),
                            readBuffer.Offset, readBuffer.Count);
                        readBuffer = new ArraySegment<byte>(new byte[RecvBufferSize]);
                    }
                }
                else
                {
                    RaiseErrorEvent(DateTime.Now, _socket.State,
                        "Unhandled Exception occured inside ReceiveMessage()");
                    break;
                }
            }

            // Pass the data buffer back to app layer via callback.
            RaiseMessageEvent(DateTime.Now, dataBuffer);

        }
        
        public async Task SendTextMessage(string message, bool endOfMessage = true, CancellationToken token = default)
        {
            await Task.Run(async () =>
            {
                var sendBytes = Encoding.UTF8.GetBytes(message);
                var sendBuffer = new ArraySegment<byte>(sendBytes);
                await _socket.SendAsync(sendBuffer, WebSocketMessageType.Text, endOfMessage: endOfMessage,
                    cancellationToken: token).ConfigureAwait(false);
            }, token);
        }


        private void RaiseConnectionEvent(DateTime timestamp, WebSocketState state, string message)
        {
            var connectionCallback = new ConnectionEventArgs
                { TimeStamp = timestamp, State = state, StatusText = message };
            OnConnection(connectionCallback);
        }

        private void RaiseMessageEvent(DateTime timestamp, byte[] message)
        {
            var messageCallback = new MessageEventArgs { Buffer = message, TimeStamp = timestamp };
            OnMessage(messageCallback);
        }

        private void RaiseErrorEvent(DateTime timestamp, WebSocketState state, string errorMsg)
        {
            var errorCallback = new Events.ErrorEventArgs
                { ErrorDetails = errorMsg, TimeStamp = timestamp, ClientWebSocketState = state };
            OnError(errorCallback);
        }

        private void OnConnection(ConnectionEventArgs e)
        {
            var handler = ConnectionEvent;
            handler?.Invoke(this, e);
        }

        private void OnMessage(MessageEventArgs e)
        {
            var handler = MessageEvent;
            handler?.Invoke(this, e);
        }

        private void OnError(Events.ErrorEventArgs e)
        {
            var handler = ErrorEvent;
            handler?.Invoke(this, e);
        }

        public event EventHandler<ConnectionEventArgs> ConnectionEvent;
        public event EventHandler<MessageEventArgs> MessageEvent;
        public event EventHandler<Events.ErrorEventArgs> ErrorEvent;
    }
}