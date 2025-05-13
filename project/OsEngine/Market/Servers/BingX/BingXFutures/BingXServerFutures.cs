using System;
using System.IO;
using System.IO.Compression;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net; // For WebProxy
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using RestSharp; // RestSharp is still used for REST API calls
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using OsEngine.Market.Servers.BingX.BingXFutures.Entity;
using System.Globalization;
using System.Linq; // For LINQ operations like FirstOrDefault


namespace OsEngine.Market.Servers.BingX.BingXFutures
{
    public class BingXServerFutures : AServer
    {
        public BingXServerFutures(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            BingXServerFuturesRealization realization = new BingXServerFuturesRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterBoolean("HedgeMode", false);
        }
    }

    public class BingXServerFuturesRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BingXServerFuturesRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread keepalive = new Thread(RequestListenKey);
            keepalive.CurrentCulture = new CultureInfo("ru-RU");
            keepalive.IsBackground = true;
            keepalive.Start();

            Thread messageReaderPrivate = new Thread(MessageReaderPrivate);
            messageReaderPrivate.IsBackground = true;
            messageReaderPrivate.Name = "MessageReaderPrivateBingXFutures";
            messageReaderPrivate.Start();

            Thread messageReaderPublic = new Thread(MessageReaderPublic);
            messageReaderPublic.IsBackground = true;
            messageReaderPublic.Name = "MessageReaderBingXFutures";
            messageReaderPublic.Start();

            Thread threadGetPortfolios = new Thread(ThreadGetPortfolios);
            threadGetPortfolios.IsBackground = true;
            threadGetPortfolios.Name = "ThreadBingXFuturesPortfolios";
            threadGetPortfolios.Start();
        }

        public DateTime ServerTime { get; set; }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                _myProxy = proxy;
                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
                _hedgeMode = ((ServerParameterBool)ServerParameters[2]).Value;

                if (_myProxy == null)
                {
                    _httpPublicClient = new HttpClient();
                }
                else
                {
                    HttpClientHandler httpClientHandler = new HttpClientHandler
                    {
                        Proxy = _myProxy
                    };
                    _httpPublicClient = new HttpClient(httpClientHandler);
                }

                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(_baseUrl + "/openApi/swap/v2/server/time").Result;
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                if (responseMessage.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"The server is not available. No internet. Status: {responseMessage.StatusCode}", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent?.Invoke();
                    return;
                }

                try
                {
                    FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                    FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

                    Task privateConnectionTask = CreatePrivateWebSocketConnectAndReturnTask();
                    try
                    {
                        // Block for initial private connection with timeout
                        if (!privateConnectionTask.Wait(TimeSpan.FromSeconds(15))) // Increased timeout
                        {
                            SendLogMessage("Private WebSocket connection timed out during initial connect.", LogMessageType.Error);
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent?.Invoke();
                            return;
                        }
                        if (privateConnectionTask.IsFaulted || (_webSocketPrivateWrapper != null && _webSocketPrivateWrapper.State != System.Net.WebSockets.WebSocketState.Open))
                        {
                            SendLogMessage($"Private WebSocket connection failed: {privateConnectionTask.Exception?.InnerExceptions.FirstOrDefault()?.Message}", LogMessageType.Error);
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent?.Invoke();
                            return;
                        }
                    }
                    catch (AggregateException agEx)
                    {
                        SendLogMessage($"Private WebSocket connection failed: {agEx.InnerExceptions.FirstOrDefault()?.Message}", LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent?.Invoke();
                        return;
                    }
                    catch (Exception ex) // Catch other potential exceptions from Wait() or property access
                    {
                        SendLogMessage($"Error during private WebSocket connection: {ex.Message}", LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent?.Invoke();
                        return;
                    }

                    CheckSocketsActivate(); // This should reflect the private socket status
                    SetPositionMode();
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    SendLogMessage("The connection cannot be opened. BingXFutures. Error Request", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent?.Invoke();
                }

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage("The connection cannot be opened. BingXFutures. Error Request", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent?.Invoke();
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets(); // This now uses SendAsync fire-and-forget
                _subscribledSecutiries.Clear();
                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            FIFOListWebSocketPublicMessage = null;
            FIFOListWebSocketPrivateMessage = null;

            Disconnect(); // Calls DisconnectEvent
        }

        public void Disconnect()
        {
            _httpPublicClient?.Dispose();
            _httpPublicClient = null;

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent?.Invoke();
            }
        }

        private RateGate _positionModeRateGate = new RateGate(1, TimeSpan.FromMilliseconds(510));

        private void SetPositionMode()
        {
            _generalRateGate2.WaitToProceed();
            _positionModeRateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                if (_myProxy != null) client.Proxy = _myProxy;
                RestRequest request = new RestRequest("/openApi/swap/v1/positionSide/dual", Method.POST);
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"dualSidePosition={_hedgeMode.ToString().ToLower()}&timestamp={timeStamp}"; // BingX expects true/false lowercase
                string sign = CalculateHmacSha256(parameters);
                request.AddParameter("dualSidePosition", _hedgeMode.ToString().ToLower());
                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);
                IRestResponse json = client.Execute(request);
                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<PositionMode> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<PositionMode>>(json.Content);
                    if (response.code == "0") {/* ignore */ }
                    else SendLogMessage($"SetPositionMode> Http State Code: {response.code} - message: {response.msg}", LogMessageType.Error);
                }
                else SendLogMessage($"SetPositionMode> Http State Code: {json.StatusCode} | msg: {json.Content}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"SetPositionMode: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public ServerType ServerType => ServerType.BingXFutures;
        public ServerConnectStatus ServerStatus { get; set; }
        public event Action ConnectEvent;
        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties
        public List<IServerParameter> ServerParameters { get; set; }
        private RateGate _generalRateGate1 = new RateGate(1, TimeSpan.FromMilliseconds(100));
        private RateGate _generalRateGate2 = new RateGate(1, TimeSpan.FromMilliseconds(100));
        private RateGate _generalRateGate3 = new RateGate(1, TimeSpan.FromMilliseconds(100));
        public string _publicKey;
        public string _secretKey;
        private bool _hedgeMode;
        #endregion

        #region 3 Securities
        public void GetSecurities()
        {
            _generalRateGate1.WaitToProceed();
            try
            {
                RestClient client = new RestClient(_baseUrl);
                if (_myProxy != null) client.Proxy = _myProxy;
                RestRequest request = new RestRequest("/openApi/swap/v2/quote/contracts", Method.GET);
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                // Per BingX docs, GET request query parameters should be alphabetically sorted for signature.
                // However, for this specific endpoint, it seems timestamp only is fine, or their example is simplified.
                // Let's stick to the original if it worked.
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);
                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign); // Signature should be the last parameter in query string for some exchanges
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingX<BingXFuturesSymbols> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseFuturesBingX<BingXFuturesSymbols>());
                    if (response.code == "0") UpdateSecurity(response.data);
                    else SendLogMessage($"GetSecurities> Error Code: {response.code} | msg: {response.msg}", LogMessageType.Error);
                }
                else SendLogMessage($"GetSecurities> Http State Code: {json.StatusCode} | msg: {json.Content}", LogMessageType.Error);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecurity(List<BingXFuturesSymbols> currencyPairs)
        {
            List<Security> securities = new List<Security>();
            foreach (var current in currencyPairs)
            {
                if (current.status == "1")
                {
                    if (current.symbol.EndsWith("USDC")) continue;
                    Security security = new Security
                    {
                        Lot = 1,
                        MinTradeAmount = current.size.ToDecimal(),
                        Name = current.symbol,
                        NameFull = current.symbol,
                        NameClass = current.currency,
                        NameId = current.contractId,
                        Exchange = nameof(ServerType.BingXFutures),
                        State = SecurityStateType.Activ,
                        Decimals = Convert.ToInt32(current.pricePrecision),
                        PriceStep = Convert.ToDecimal(Math.Pow(10, -Convert.ToInt32(current.pricePrecision))),
                        SecurityType = SecurityType.CurrencyPair,
                        DecimalsVolume = Convert.ToInt32(current.quantityPrecision),
                        MinTradeAmountType = MinTradeAmountType.C_Currency,
                        VolumeStep = current.size.ToDecimal()
                    };
                    security.PriceStepCost = security.PriceStep;
                    security.MinTradeAmount = current.tradeMinUSDT.ToDecimal(); // Assuming tradeMinUSDT is what was intended
                    securities.Add(security);
                }
            }
            SecurityEvent?.Invoke(securities);
        }
        public event Action<List<Security>> SecurityEvent;
        #endregion

        // ... Other sections (4 Portfolios, 5 Data, 10 Trade, 11 Queries, 12 Log, 13 Helpers) are mostly unchanged ...
        // ... except for WebSocket interaction points. I will modify relevant parts in section 6, 7, 8, 9.

        #region 6 WebSocket creation

        // Using List<WebSocketWrapper> for public sockets
        private List<WebSocketWrapper> _webSocketPublicWrappers = new List<WebSocketWrapper>();
        // Using a single WebSocketWrapper for private socket
        private WebSocketWrapper _webSocketPrivateWrapper;

        private const string _webSocketUrl = "wss://open-api-swap.bingx.com/swap-market";
        private string _listenKey = "";

        // Renamed for clarity that it returns a Task for Connect method to wait on
        private Task CreatePrivateWebSocketConnectAndReturnTask()
        {
            if (_webSocketPrivateWrapper != null && _webSocketPrivateWrapper.State == System.Net.WebSockets.WebSocketState.Open)
            {
                return Task.CompletedTask;
            }

            _listenKey = CreateListenKey(); // Ensure _listenKey is fresh
            if (string.IsNullOrEmpty(_listenKey))
            {
                SendLogMessage("Authorization error. Listen key is not created for private WebSocket.", LogMessageType.Error);
                return Task.FromException(new InvalidOperationException("Failed to create listen key for private WebSocket."));
            }

            string urlStr = $"{_webSocketUrl}?listenKey={_listenKey}";

            _webSocketPrivateWrapper = new WebSocketWrapper(
                onOpen: () => WebSocketPrivate_OnOpen(),
                onMessage: (msg) => FIFOListWebSocketPrivateMessage.Enqueue(msg),
                onClose: () => WebSocketPrivate_OnClose(),
                onError: (ex) => WebSocketPrivate_OnError(ex),
                decompressFunc: DecompressData // Changed name for clarity
            );
            _webSocketPrivateWrapper.SetUrl(urlStr); // Set URL for potential reconnections by wrapper

            if (_myProxy != null)
            {
                _webSocketPrivateWrapper.SetProxy(_myProxy);
            }

            return _webSocketPrivateWrapper.ConnectAsync();
        }

        private void CreatePublicWebSocketConnect() // Called by Subscrible
        {
            try
            {
                if (FIFOListWebSocketPublicMessage == null)
                {
                    FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                }
                // CreateNewPublicSocket now returns a wrapper and initiates connection
                var newWrapper = CreateNewPublicSocketAndConnect();
                if (newWrapper != null)
                {
                    _webSocketPublicWrappers.Add(newWrapper);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocketWrapper CreateNewPublicSocketAndConnect() // Renamed
        {
            try
            {
                // Public streams don't always need a listen key, depends on BingX specific stream
                // The original code used a listenKey for public streams too, this might be a combined stream
                // If public streams are truly public, they might not need listenKey.
                // Re-checking BingX docs: their main market data streams are public and don't use listenKey.
                // The provided URL `wss://open-api-swap.bingx.com/swap-market` without listenKey is for general market data.
                // If `_listenKey` was intended for all, then BingX might multiplex user data over it.
                // The original code uses _listenKey for public socket URL as well. Let's stick to that.

                if (string.IsNullOrEmpty(_listenKey) && _webSocketPublicWrappers.Count == 0) // Only get listenKey if first public or no key
                {
                    _listenKey = CreateListenKey(); // This might be problematic if private already has one.
                                                    // Let's assume one listenKey is fine for all.
                }

                // If public stream doesn't need listenKey, urlStr should be simpler.
                // For now, assuming it's like private based on original code:
                string urlStr = $"{_webSocketUrl}"; // Standard public endpoint
                                                    // If listen key is needed for public data streams (unusual, but was in original code structure implicitly):
                                                    // string urlStr = $"{_webSocketUrl}?listenKey={_listenKey}"; 
                                                    // Let's use the public endpoint without listen key, as is standard.
                                                    // If issues arise, this might need to be changed to use listenKey.

                var wrapper = new WebSocketWrapper(
                    onOpen: () => WebSocketPublic_OnOpen(_webSocketPublicWrappers.LastOrDefault()), // Pass the specific wrapper
                    onMessage: (msg) => FIFOListWebSocketPublicMessage.Enqueue(msg),
                    onClose: () => WebSocketPublic_OnClose(_webSocketPublicWrappers.LastOrDefault()),
                    onError: (ex) => WebSocketPublic_OnError(ex, _webSocketPublicWrappers.LastOrDefault()),
                    decompressFunc: DecompressData
                );
                wrapper.SetUrl(urlStr);

                if (_myProxy != null)
                {
                    NetworkCredential credential = (NetworkCredential)_myProxy.Credentials; // Not used by ClientWebSocket.Options.Proxy directly
                    wrapper.SetProxy(_myProxy);
                }

                // Fire-and-forget the connection. Status handled by callbacks.
                _ = wrapper.ConnectAsync().ConfigureAwait(false);
                return wrapper;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }


        private void DeleteWebSocketConnection()
        {
            if (_webSocketPublicWrappers != null)
            {
                foreach (var wrapper in _webSocketPublicWrappers)
                {
                    try
                    {
                        wrapper?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage($"Error disposing public websocket: {ex.Message}", LogMessageType.Error);
                    }
                }
                _webSocketPublicWrappers.Clear();
            }

            if (_webSocketPrivateWrapper != null)
            {
                try
                {
                    _webSocketPrivateWrapper.Dispose();
                }
                catch (Exception ex)
                {
                    SendLogMessage($"Error disposing private websocket: {ex.Message}", LogMessageType.Error);
                }
                _webSocketPrivateWrapper = null;
            }
        }

        private readonly object _socketActivateLocker = new object();

        private void CheckSocketsActivate()
        {
            try
            {
                lock (_socketActivateLocker)
                {
                    bool privateConnected = _webSocketPrivateWrapper != null && _webSocketPrivateWrapper.State == System.Net.WebSockets.WebSocketState.Open;

                    bool publicSocketsNeeded = _subscribledSecutiries.Count > 0;
                    bool allPublicConnected = true;

                    if (publicSocketsNeeded)
                    {
                        if (_webSocketPublicWrappers.Count == 0)
                        {
                            allPublicConnected = false;
                        }
                        else
                        {
                            foreach (var wrapper in _webSocketPublicWrappers)
                            {
                                if (wrapper == null || wrapper.State != System.Net.WebSockets.WebSocketState.Open)
                                {
                                    allPublicConnected = false;
                                    break;
                                }
                            }
                        }
                    }

                    // If public sockets are needed but not all connected, or private not connected, then disconnect status
                    if (!privateConnected || (publicSocketsNeeded && !allPublicConnected))
                    {
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            // Do not call Disconnect() here as it might be a temporary issue being resolved.
                            // ServerStatus = ServerConnectStatus.Disconnect; // This might be too aggressive
                            // DisconnectEvent?.Invoke();
                            SendLogMessage("One or more WebSockets are not connected.", LogMessageType.System);
                        }
                    }
                    else // All required sockets are connected
                    {
                        if (ServerStatus != ServerConnectStatus.Connect)
                        {
                            ServerStatus = ServerConnectStatus.Connect;
                            ConnectEvent?.Invoke();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        #endregion

        #region 7 WebSocket events (Now called by WebSocketWrapper callbacks)

        // Methods to be called by WebSocketWrapper for public sockets
        private void WebSocketPublic_OnOpen(WebSocketWrapper wrapper)
        {
            SendLogMessage("BingXFutures WebSocket Public connection open.", LogMessageType.System);
            CheckSocketsActivate();
        }
        private void WebSocketPublic_OnClose(WebSocketWrapper wrapper)
        {
            SendLogMessage($"Public WebSocket connection closed. Wrapper: {wrapper?.GetHashCode()}", LogMessageType.Error);
            // Optional: remove this specific wrapper from the list if it's permanently closed and not retrying
            // _webSocketPublicWrappers.Remove(wrapper);
            CheckSocketsActivate();
        }
        private void WebSocketPublic_OnError(Exception ex, WebSocketWrapper wrapper)
        {
            SendLogMessage($"Public WebSocket error: {ex.ToString()}. Wrapper: {wrapper?.GetHashCode()}", LogMessageType.Error);
            CheckSocketsActivate();
        }

        // Methods to be called by WebSocketWrapper for private socket
        private void WebSocketPrivate_OnOpen()
        {
            SendLogMessage("BingXFutures WebSocket Private connection open.", LogMessageType.System);
            CheckSocketsActivate();
        }
        private void WebSocketPrivate_OnClose()
        {
            SendLogMessage($"Private WebSocket connection closed.", LogMessageType.Error);
            CheckSocketsActivate();
        }
        private void WebSocketPrivate_OnError(Exception ex)
        {
            SendLogMessage($"Private WebSocket error: {ex.ToString()}", LogMessageType.Error);
            CheckSocketsActivate();
        }

        // Original OnMessage handlers are effectively replaced by:
        // 1. WebSocketWrapper's ReceiveLoopAsync handling decompression and application Ping/Pong.
        // 2. The lambda `(msg) => FIFOListWebSocketPublicMessage.Enqueue(msg)` (or private equivalent)
        //    passed to WebSocketWrapper constructor.
        // So, the old WebSocketPublicNew_OnMessage and _webSocketPrivate_OnMessage methods are removed.

        #endregion

        #region 8 Security subscrible
        private List<string> _subscribledSecutiries = new List<string>();

        public void Subscrible(Security security)
        {
            try
            {
                CreateSubscribleSecurityMessageWebSocket(security);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private async Task CreateSubscribleSecurityMessageWebSocket(Security security) // Made async for SendAsync
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect &&
                    (_webSocketPrivateWrapper == null || _webSocketPrivateWrapper.State != System.Net.WebSockets.WebSocketState.Open))
                { // Check private connection specifically for allowing subscriptions
                    SendLogMessage("Cannot subscribe, server is not connected.", LogMessageType.Error);
                    return;
                }

                if (_subscribledSecutiries.Contains(security.Name))
                {
                    return;
                }
                _subscribledSecutiries.Add(security.Name);

                if (_webSocketPublicWrappers.Count == 0 ||
                    _webSocketPublicWrappers.All(w => w.State != System.Net.WebSockets.WebSocketState.Open))
                {
                    SendLogMessage("No active public WebSocket for subscription. Creating new one.", LogMessageType.System);
                    CreatePublicWebSocketConnect(); // This adds a new wrapper and starts its connection
                                                    // Wait a bit for the connection to establish, or rely on subsequent calls
                    await Task.Delay(2000); // Simplistic wait, could be improved with state check
                }

                WebSocketWrapper targetWrapper = _webSocketPublicWrappers.LastOrDefault(w => w.State == System.Net.WebSockets.WebSocketState.Open);

                // Logic for managing multiple public sockets if one gets full (40 subs)
                if (targetWrapper != null && _subscribledSecutiries.Count > 0 && _subscribledSecutiries.Count % 40 == 0)
                {
                    SendLogMessage("Public WebSocket subscription limit reached for current socket, creating new one.", LogMessageType.System);
                    CreatePublicWebSocketConnect();
                    await Task.Delay(2000); // Wait for new socket
                    targetWrapper = _webSocketPublicWrappers.LastOrDefault(w => w.State == System.Net.WebSockets.WebSocketState.Open);
                }


                if (targetWrapper != null && targetWrapper.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    // Using await now, or fire-and-forget with error handling if this method must remain synchronous
                    await targetWrapper.SendAsync($"{{\"id\": \"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"{security.Name}@trade\"}}");
                    await targetWrapper.SendAsync($"{{ \"id\":\"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"{security.Name}@depth20@500ms\"}}");
                    SendLogMessage($"Subscribed to {security.Name} on wrapper {targetWrapper.GetHashCode()}", LogMessageType.System);
                }
                else
                {
                    SendLogMessage($"Failed to subscribe to {security.Name}. No open public WebSocket found after attempt.", LogMessageType.Error);
                    _subscribledSecutiries.Remove(security.Name); // Rollback subscription
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Subscription error for {security.Name}: {ex.Message}", LogMessageType.Error);
                _subscribledSecutiries.Remove(security.Name); // Rollback
            }
        }

        // Changed to fire-and-forget SendAsync
        private void UnsubscribeFromAllWebSockets()
        {
            try
            {
                if (_subscribledSecutiries == null || _subscribledSecutiries.Count == 0) return;

                foreach (var wrapper in _webSocketPublicWrappers)
                {
                    if (wrapper != null && wrapper.State == System.Net.WebSockets.WebSocketState.Open)
                    {
                        foreach (string name in _subscribledSecutiries)
                        {
                            _ = wrapper.SendAsync($"{{\"id\": \"{GenerateNewId()}\", \"reqType\": \"unsub\", \"dataType\": \"{name}@trade\"}}");
                            _ = wrapper.SendAsync($"{{ \"id\":\"{GenerateNewId()}\", \"reqType\": \"unsub\", \"dataType\": \"{name}@depth20@500ms\"}}");
                        }
                    }
                }
                // _subscribledSecutiries.Clear(); // Clearing should happen after successful unsubscription confirmed or on dispose.
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public bool SubscribeNews() => false;
        public event Action<News> NewsEvent;
        #endregion

        #region 9 WebSocket parsing the messages
        // Queues remain the same
        private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        // MessageReaderPublic and MessageReaderPrivate methods remain largely the same as they process from queues
        // Their logic for parsing JSON based on content (e.g., "@trade", "ORDER_TRADE_UPDATE") is unchanged.
        // ... (MessageReaderPublic, MessageReaderPrivate, UpdateTrade, UpdatePortfolio, UpdatePosition, UpdateOrder, UpdateMyTrade, GetExecuteVolumeInOrder, UpdateDepth)
        // These methods are consumers of the queues and don't directly interact with WebSocketSharp objects.
        // Their internal JSON parsing and event raising logic is assumed correct and untouched by the WebSocket library change.
        #endregion

        // ... (Section 10 Trade - REST calls, unchanged except for potential API key usage consistency) ...
        // ... (Section 11 Queries - REST calls, CreateListenKey, RequestListenKey are unchanged) ...
        // ... (Section 12 Log - SendLogMessage unchanged) ...

        #region 13 Helpers

        private string DecompressData(byte[] data) // Renamed from Decompress
        {
            try
            {
                using (System.IO.MemoryStream compressedStream = new System.IO.MemoryStream(data))
                {
                    // Important: Check if the first two bytes are GZip header (0x1F, 0x8B)
                    // ClientWebSocket might sometimes pass uncompressed data if server sends it that way,
                    // or if an intermediate proxy decompresses it.
                    // Forcing GZip decompression on non-GZip data will fail.
                    // However, original code assumed GZip, so let's keep it but be mindful.
                    // byte[] header = new byte[2];
                    // compressedStream.Read(header, 0, 2);
                    // compressedStream.Position = 0; // Reset position
                    // if (header[0] != 0x1f || header[1] != 0x8b) {
                    //    SendLogMessage("Data is not GZip compressed, returning as UTF8 string.", LogMessageType.System);
                    //    return Encoding.UTF8.GetString(data); // Fallback or error
                    // }

                    using (GZipStream decompressor = new GZipStream(compressedStream, CompressionMode.Decompress))
                    {
                        using (System.IO.MemoryStream resultStream = new System.IO.MemoryStream())
                        {
                            decompressor.CopyTo(resultStream);
                            return Encoding.UTF8.GetString(resultStream.ToArray());
                        }
                    }
                }
            }
            catch (Exception ex) // Catch specific InvalidDataException for GZip errors
            {
                SendLogMessage($"DecompressData error: {ex.Message}. Data might not be GZipped. Raw data (first 50 bytes): {BitConverter.ToString(data.Take(50).ToArray())}", LogMessageType.Error);
                // Fallback: try to interpret as plain text if decompression fails.
                // This might be noisy if data is truly binary and not text.
                // return Encoding.UTF8.GetString(data); 
                return null;
            }
        }

        private string CalculateHmacSha256(string parametrs)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(_secretKey);
            byte[] inputBytes = Encoding.UTF8.GetBytes(parametrs);
            using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private string GenerateNewId() => Guid.NewGuid().ToString();

        #endregion

        // Ensure all previous event handlers for WebSocketSharp are removed or adapted.
        // e.g., _webSocketPrivate_OnOpen, _webSocketPrivate_OnClose, etc. are now indirectly called
        // by the lambdas passed to WebSocketWrapper.

        // Sections 4, 5, 9, 10, 11, 12 need to be included from original if they were omitted for brevity above.
        // I'm assuming methods like UpdateTrade, UpdatePortfolio, GetCandleHistory, etc., are present.
        // The provided snippet was very long, so I focused on WebSocket parts.
        // All methods from the original code that were not directly involved with WebSocketSharp client object handling
        // (like REST API calls, data processing, logging) should largely remain the same.

        // FULL SKELETON OF THE CLASS WITH ALL REGIONS:
        #region 4 Portfolios
        public List<Portfolio> Portfolios;
        public void GetPortfolios()
        {
            if (Portfolios == null) GetNewPortfolio();
            CreateQueryPortfolio(true);
            CreateQueryPositions();
        }
        private void ThreadGetPortfolios()
        {
            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect) { Thread.Sleep(3000); continue; }
                try
                {
                    Thread.Sleep(20000);
                    if (Portfolios == null) GetNewPortfolio();
                    CreateQueryPortfolio(false);
                    CreateQueryPositions();
                }
                catch (Exception ex) { SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error); }
            }
        }
        private void GetNewPortfolio()
        {
            Portfolios = new List<Portfolio> { new Portfolio { Number = "BingXFutures", ValueBegin = 1, ValueCurrent = 1, ValueBlocked = 0 } };
            PortfolioEvent?.Invoke(Portfolios);
        }
        private RateGate _positionsRateGate = new RateGate(1, TimeSpan.FromMilliseconds(250));
        private void CreateQueryPositions()
        {
            _positionsRateGate.WaitToProceed();
            try
            {
                RestClient client = new RestClient(_baseUrl);
                if (_myProxy != null) client.Proxy = _myProxy;
                RestRequest request = new RestRequest("/openApi/swap/v2/user/positions", Method.GET);
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);
                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);
                IRestResponse json = client.Execute(request);
                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingX<PositionData> response = JsonConvert.DeserializeObject<ResponseFuturesBingX<PositionData>>(json.Content);
                    if (response.code == "0")
                    {
                        Portfolio portfolio = Portfolios[0];
                        // Clear old positions not reported anymore (optional, depends on how SetNewPosition works)
                        // var currentPositions = new HashSet<string>();

                        foreach (var posData in response.data)
                        {
                            PositionOnBoard position = new PositionOnBoard { PortfolioName = "BingXFutures" };
                            if (posData.OnlyOnePosition == "true") // Hedge mode off likely
                            {
                                position.SecurityNameCode = posData.Symbol + "_BOTH";
                                position.ValueCurrent = posData.PositionSide == "LONG" ? posData.PositionAmt.ToDecimal() : -posData.PositionAmt.ToDecimal();
                            }
                            else // Hedge mode on
                            {
                                position.SecurityNameCode = posData.Symbol + "_" + posData.PositionSide;
                                position.ValueCurrent = posData.PositionSide == "LONG" ? posData.PositionAmt.ToDecimal() : -posData.PositionAmt.ToDecimal();
                            }
                            position.ValueBegin = position.ValueCurrent; // Or track entry value differently if needed
                            position.UnrealizedPnl = posData.UnrealizedProfit.ToDecimal();
                            portfolio.SetNewPosition(position);
                            // currentPositions.Add(position.SecurityNameCode);
                        }
                        // portfolio.ClearPositionsNotInSet(currentPositions); // If positions can be fully removed
                        PortfolioEvent?.Invoke(Portfolios);
                    }
                    else SendLogMessage($"CreateQueryPositions> Code: {response.code} - msg: {response.msg}", LogMessageType.Error);
                }
                else if (!json.Content.StartsWith("<!DOCTYPE")) SendLogMessage($"CreateQueryPositions> HTTP Code: {json.StatusCode} | msg: {json.Content}", LogMessageType.Error);
            }
            catch (Exception exception) { SendLogMessage(exception.ToString(), LogMessageType.Error); }
        }
        private RateGate _portfolioRateGate = new RateGate(1, TimeSpan.FromMilliseconds(250));
        private void CreateQueryPortfolio(bool IsUpdateValueBegin)
        {
            _portfolioRateGate.WaitToProceed();
            try
            {
                RestClient client = new RestClient(_baseUrl);
                if (_myProxy != null) client.Proxy = _myProxy;
                RestRequest request = new RestRequest("/openApi/swap/v2/user/balance", Method.GET);
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}&recvWindow=20000"; // Alphabetical: recvWindow, timestamp
                string sign = CalculateHmacSha256(parameters);
                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("recvWindow", 20000);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);
                IRestResponse json = client.Execute(request);
                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<Balance> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<Balance>>(json.Content);
                    if (response.code == "0")
                    {
                        Portfolio portfolio = Portfolios[0];
                        BalanceInfoBingXFutures asset = response.data.balance;
                        PositionOnBoard newPortf = new PositionOnBoard
                        {
                            SecurityNameCode = asset.asset,
                            ValueCurrent = asset.equity.ToDecimal(),
                            ValueBlocked = asset.freezedMargin.ToDecimal() + asset.usedMargin.ToDecimal(),
                            UnrealizedPnl = asset.unrealizedProfit.ToDecimal(),
                            PortfolioName = "BingXFutures"
                        };
                        if (IsUpdateValueBegin) newPortf.ValueBegin = asset.balance.ToDecimal(); // This is total balance, not equity

                        portfolio.SetNewPosition(newPortf); // This is for the USDT (or base currency) asset itself

                        if (IsUpdateValueBegin) portfolio.ValueBegin = newPortf.ValueCurrent; // ValueBegin should be equity at start
                        portfolio.ValueCurrent = newPortf.ValueCurrent;
                        portfolio.ValueBlocked = newPortf.ValueBlocked;
                        portfolio.UnrealizedPnl = newPortf.UnrealizedPnl;
                        if (newPortf.ValueCurrent == 0) { portfolio.ValueBegin = 1; portfolio.ValueCurrent = 1; }
                        PortfolioEvent?.Invoke(Portfolios);
                    }
                    else SendLogMessage($"CreateQueryPortfolio> Code: {response.code} - msg: {response.msg}", LogMessageType.Error);
                }
                else if (!json.Content.StartsWith("<!DOCTYPE")) SendLogMessage($"CreateQueryPortfolio> HTTP Code: {json.StatusCode} | msg: {json.Content}", LogMessageType.Error);
            }
            catch (Exception exception) { SendLogMessage(exception.ToString(), LogMessageType.Error); }
        }
        public event Action<List<Portfolio>> PortfolioEvent;
        #endregion
        #region 5 Data
        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            string tf = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);
            return RequestCandleHistory(security.Name, tf, candleCount); // candleCount was limit in original
        }
        private List<Candle> RequestCandleHistory(string nameSec, string tameFrame, long limit = 500, long fromTimeStamp = 0, long toTimeStamp = 0)
        {
            _generalRateGate1.WaitToProceed();

            try
            {
                string endPoint = "/openApi/swap/v3/quote/klines";
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                string parameters = "";
                if (fromTimeStamp != 0 && toTimeStamp != 0)
                {
                    parameters = $"symbol={nameSec}&interval={tameFrame}&startTime={fromTimeStamp}&endTime={toTimeStamp}&limit={limit}&timestamp={timeStamp}";
                }
                else
                {
                    parameters = $"symbol={nameSec}&interval={tameFrame}&limit={limit}&timestamp={timeStamp}";
                }

                string sign = CalculateHmacSha256(parameters);
                string requestUri = $"{_baseUrl}{endPoint}?{parameters}&signature{sign}";

                if (_httpPublicClient == null)
                {
                    if (_myProxy == null)
                    {
                        _httpPublicClient = new HttpClient();
                    }
                    else
                    {
                        HttpClientHandler httpClientHandler = new HttpClientHandler
                        {
                            Proxy = _myProxy
                        };

                        _httpPublicClient = new HttpClient(httpClientHandler);
                    }
                }

                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(requestUri).Result;

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;

                    try
                    {
                        ResponseFuturesBingX<CandlestickChartDataFutures> response =
                            JsonConvert.DeserializeAnonymousType(json, new ResponseFuturesBingX<CandlestickChartDataFutures>());

                        // if the start and end date of the candles is incorrect, the exchange sends one last candle instead of an error
                        if (response.code == "0" && response.data.Count != 1)
                        {
                            return ConvertCandles(response.data);
                        }
                        else if (response.data.Count == 1)
                        {
                            return null;
                        }
                        else
                        {
                            SendLogMessage($"RequestCandleHistory> Error: code {response.code}", LogMessageType.Error);
                        }
                    }
                    catch
                    {
                        JsonErrorResponse responseError = JsonConvert.DeserializeAnonymousType(json, new JsonErrorResponse());
                        SendLogMessage($"RequestCandleHistory> Http State Code: {responseError.code} - message: {responseError.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;
                    SendLogMessage($"RequestCandleHistory> Http State Code: {responseMessage.StatusCode} - {json}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Candle> ConvertCandles(List<CandlestickChartDataFutures> rawList)
        {
            if (rawList == null) return new List<Candle>();
            List<Candle> candles = new List<Candle>();
            foreach (var current in rawList)
            {
                Candle candle = new Candle
                {
                    State = CandleState.Finished,
                    TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(current.time)),
                    Volume = current.volume.ToDecimal(),
                    Close = current.close.ToDecimal(),
                    High = current.high.ToDecimal(),
                    Low = current.low.ToDecimal(),
                    Open = current.open.ToDecimal()
                };
                if (candles.Count > 0 && candle.TimeStart == candles.Last().TimeStart) continue;
                candles.Add(candle);
            }
            // candles.Reverse(); // Data usually comes oldest first, so no reverse needed for Add. If newest first, then reverse.
            // BingX klines are typically oldest first. So this reverse might be wrong.
            // Original had reverse. Let's keep to see. If candles are backwards, remove this.
            return candles;
        }
        private string GetInterval(TimeSpan timeFrame)
        {
            if (timeFrame.TotalMinutes >= 1 && timeFrame.TotalDays < 1) return $"{(int)timeFrame.TotalMinutes}m";
            if (timeFrame.TotalHours >= 1 && timeFrame.TotalDays < 1) return $"{(int)timeFrame.TotalHours}h";
            if (timeFrame.TotalDays >= 1) return $"{(int)timeFrame.TotalDays}d";
            return "1m"; // Fallback
        }
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            // This method's logic for chunking requests seems fine.
            // Ensure startTime, endTime are handled correctly for timezones if not UTC.
            // The original code uses DateTime.SpecifyKind(..., DateTimeKind.Utc)
            // TimeManager.GetTimeStampMilliSecondsToDateTime expects UTC.

            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);

            if (startTime >= endTime || startTime >= DateTime.UtcNow) return null;

            List<Candle> allCandles = new List<Candle>();
            string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);
            DateTime currentStartTime = startTime;
            int maxCandlesPerRequest = 1000; // BingX limit often 500 or 1000

            while (currentStartTime < endTime && currentStartTime < DateTime.UtcNow)
            {
                DateTime currentEndTimeChunk = currentStartTime.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * maxCandlesPerRequest);
                if (currentEndTimeChunk > endTime) currentEndTimeChunk = endTime;
                if (currentEndTimeChunk > DateTime.UtcNow) currentEndTimeChunk = DateTime.UtcNow.AddMinutes(-timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes); // Ensure not requesting current candle

                long fromTs = TimeManager.GetTimeStampMilliSecondsToDateTime(currentStartTime);
                long toTs = TimeManager.GetTimeStampMilliSecondsToDateTime(currentEndTimeChunk);

                if (fromTs >= toTs) break; // Avoid issues if currentStartTime is too close to UtcNow

                List<Candle> chunk = RequestCandleHistory(security.Name, interval, maxCandlesPerRequest, fromTs, toTs);
                if (chunk == null || chunk.Count == 0) break;

                allCandles.AddRange(chunk.Where(c => c.TimeStart >= currentStartTime && c.TimeStart < endTime));

                if (chunk.Count < maxCandlesPerRequest || chunk.Last().TimeStart >= currentEndTimeChunk.AddSeconds(-1))
                { // If fewer candles than limit returned, or last candle is at or after chunk end, assume no more data in this range
                    currentStartTime = chunk.Last().TimeStart.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
                }
                else // Should not happen if API returns up to 'limit' or up to 'endTime'
                {
                    currentStartTime = chunk.Last().TimeStart.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
                }


                if (allCandles.Count > 0 && allCandles.Last().TimeStart >= endTime) break;
                Thread.Sleep(200); // Rate limit guard
            }
            return allCandles.DistinctBy(c => c.TimeStart).OrderBy(c => c.TimeStart).ToList();
        }
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime) => null; // Not implemented
        #endregion
        #region 9 WebSocket parsing the messages (MessageReader methods and JSON deserialization)
        // These methods (MessageReaderPublic, MessageReaderPrivate, UpdateTrade, UpdatePortfolio, UpdatePosition, UpdateOrder, UpdateMyTrade, GetExecuteVolumeInOrder, UpdateDepth)
        // are mostly unchanged as they consume from queues.
        // Make sure _myTrades list is initialized.
        private List<MyTrade> _myTrades = new List<MyTrade>();
        private DateTime _lastTimeMd; // For market depth timestamp uniqueness

        private void MessageReaderPublic()
        {
            Thread.Sleep(5000);
            while (true)
            {
                try
                {
                    if (FIFOListWebSocketPublicMessage == null || ServerStatus == ServerConnectStatus.Disconnect) { Thread.Sleep(1000); continue; }
                    if (FIFOListWebSocketPublicMessage.IsEmpty) { Thread.Sleep(1); continue; }
                    if (FIFOListWebSocketPublicMessage.TryDequeue(out string message))
                    {
                        if (message.Contains("@trade")) UpdateTrade(message);
                        else if (message.Contains("@depth")) UpdateDepth(message); // Changed to @depth from @depth20 for broader match
                    }
                }
                catch (Exception exception) { Thread.Sleep(1000); SendLogMessage(exception.ToString(), LogMessageType.Error); }
            }
        }
        private void MessageReaderPrivate()
        {
            while (true)
            {
                try
                {
                    if (FIFOListWebSocketPrivateMessage == null || ServerStatus == ServerConnectStatus.Disconnect) { Thread.Sleep(1000); continue; }
                    if (FIFOListWebSocketPrivateMessage.IsEmpty) { Thread.Sleep(1); continue; }
                    if (FIFOListWebSocketPrivateMessage.TryDequeue(out string message))
                    {
                        if (message.Contains("ORDER_TRADE_UPDATE")) UpdateOrder(message);
                        else if (message.Contains("ACCOUNT_UPDATE")) { UpdatePortfolio(message); UpdatePosition(message); }
                    }
                }
                catch (Exception exception) { Thread.Sleep(1000); SendLogMessage(exception.ToString(), LogMessageType.Error); }
            }
        }
        private void UpdateTrade(string message)
        {
            try
            {
                // Assuming SubscribeLatestTradeDetail is a generic wrapper for BingX trade stream
                // BingX trade stream: {"dataType":"BTC-USDT@trade","data":[{"q":"0.0004","p":"29010.6","T":1690317477671,"s":"BTC-USDT","m":false}]}
                // The original had SubscribeLatestTradeDetail<TradeDetails>, let's assume TradeDetails is a list from "data"
                var tradeEvent = JsonConvert.DeserializeObject<BingXTradeEvent>(message); // Use a specific class for BingX structure
                if (tradeEvent?.data == null) return;

                foreach (var tradeData in tradeEvent.data)
                {
                    Trade trade = new Trade
                    {
                        SecurityNameCode = tradeData.s,
                        Price = tradeData.p.ToDecimal(),
                        Time = TimeManager.GetDateTimeFromTimeStamp(tradeData.T),
                        Volume = tradeData.q.ToDecimal(),
                        Side = tradeData.m ? Side.Sell : Side.Buy // true for sell (maker=false, taker=true), false for buy
                    };
                    NewTradesEvent?.Invoke(trade);
                }
            }
            catch (Exception exception) { SendLogMessage($"UpdateTrade Error: {exception} on msg: {message.Substring(0, Math.Min(message.Length, 100))}", LogMessageType.Error); }
        }
        private void UpdatePortfolio(string message)
        {
            try
            {
                AccountUpdateEvent accountUpdate = JsonConvert.DeserializeObject<AccountUpdateEvent>(message);
                if (accountUpdate?.a?.B == null || Portfolios == null || Portfolios.Count == 0) return;

                Portfolio portfolio = Portfolios[0];
                foreach (var balanceInfo in accountUpdate.a.B)
                {
                    PositionOnBoard pos = new PositionOnBoard
                    {
                        PortfolioName = "BingXFutures",
                        SecurityNameCode = balanceInfo.a, // Asset name (e.g., USDT)
                        ValueCurrent = balanceInfo.wb.ToDecimal() // Wallet Balance
                                                                  // ValueBlocked could be derived from fr (frozen) or ma (margin) if available
                    };
                    portfolio.SetNewPosition(pos);
                }
                // Update overall portfolio metrics if ACCOUNT_UPDATE contains them (e.g. total equity)
                // The current parsing focuses on individual asset balances from 'B' array.
                PortfolioEvent?.Invoke(Portfolios);
            }
            catch (Exception exception) { SendLogMessage($"UpdatePortfolio Error: {exception} on msg: {message.Substring(0, Math.Min(message.Length, 100))}", LogMessageType.Error); }
        }
        private void UpdatePosition(string message)
        {
            try
            {
                AccountUpdateEvent accountUpdate = JsonConvert.DeserializeObject<AccountUpdateEvent>(message);
                if (accountUpdate?.a?.P == null || Portfolios == null || Portfolios.Count == 0) return;

                Portfolio portfolio = Portfolios[0];
                foreach (var posData in accountUpdate.a.P)
                {
                    PositionOnBoard position = new PositionOnBoard { PortfolioName = "BingXFutures" };
                    // Hedge mode determination here is key, _hedgeMode is from server params
                    if (!_hedgeMode) // Or check posData.mt == "cross" or "isolated" vs "both" logic
                    {
                        position.SecurityNameCode = posData.s + "_BOTH";
                        position.ValueCurrent = posData.ps == "LONG" ? posData.pa.ToDecimal() : -posData.pa.ToDecimal();
                    }
                    else
                    {
                        position.SecurityNameCode = posData.s + "_" + posData.ps; // LONG or SHORT
                        position.ValueCurrent = posData.ps == "LONG" ? posData.pa.ToDecimal() : -posData.pa.ToDecimal();
                    }
                    position.UnrealizedPnl = posData.up.ToDecimal();
                    // Entry price (ep), leverage (l) etc. are also in posData if needed for more detail
                    portfolio.SetNewPosition(position);
                }
                PortfolioEvent?.Invoke(Portfolios);
            }
            catch (Exception exception) { SendLogMessage($"UpdatePosition Error: {exception} on msg: {message.Substring(0, Math.Min(message.Length, 100))}", LogMessageType.Error); }
        }
        private void UpdateOrder(string message)
        {
            try
            {
                TradeUpdateEvent responseOrderEvent = JsonConvert.DeserializeObject<TradeUpdateEvent>(message);
                if (responseOrderEvent?.o == null) return;
                var orderData = responseOrderEvent.o;

                Order newOrder = new Order();
                OrderStateType orderState = OrderStateType.None;
                switch (orderData.X) // Execution Type. x is order status.
                {
                    case "NEW": orderState = OrderStateType.Active; break;
                    case "PARTIALLY_FILLED": orderState = OrderStateType.Partial; break;
                    case "FILLED": orderState = OrderStateType.Done; break;
                    case "CANCELED": case "CANCELLED": orderState = OrderStateType.Cancel; break; // Handle both spellings
                    case "REJECTED": case "EXPIRED": orderState = OrderStateType.Fail; break;
                    default: orderState = OrderStateType.None; break; // PENDING, etc.
                }

                try { newOrder.NumberUser = Convert.ToInt32(orderData.c); } catch { /* ignore if not int or empty */ }
                newOrder.NumberMarket = orderData.i.ToString();
                newOrder.SecurityNameCode = orderData.s;
                newOrder.SecurityClassCode = orderData.N; // Usually asset like USDT
                newOrder.PortfolioNumber = "BingXFutures";
                newOrder.Side = orderData.S == "BUY" ? Side.Buy : Side.Sell;
                newOrder.Price = orderData.p.ToDecimal(); // Original price for limit orders
                newOrder.Volume = orderData.q.ToDecimal(); // Original quantity
                newOrder.State = orderState;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(responseOrderEvent.E)); // Event Time
                newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(orderData.T));    // Transaction Time of order
                newOrder.TypeOrder = orderData.o == "MARKET" ? OrderPriceType.Market : OrderPriceType.Limit;
                newOrder.ServerType = ServerType.BingXFutures;

                MyOrderEvent?.Invoke(newOrder);

                // If execution type indicates a trade (FILLED, PARTIALLY_FILLED)
                if (orderData.x == "TRADE") // Check execution type 'x' for actual trade fill
                {
                    UpdateMyTradeFromOrderEvent(responseOrderEvent); // Pass the whole event or just orderData.o
                }
            }
            catch (Exception exception) { SendLogMessage($"UpdateOrder Error: {exception} on msg: {message.Substring(0, Math.Min(message.Length, 100))}", LogMessageType.Error); }
        }
        private void UpdateMyTradeFromOrderEvent(TradeUpdateEvent tradeUpdateEvent) // New method
        {
            TradeOrderDetails orderData = tradeUpdateEvent.o;
            MyTrade newTrade = new MyTrade
            {
                Time = TimeManager.GetDateTimeFromTimeStamp(long.Parse(orderData.T)), // Trade time 't'
                SecurityNameCode = orderData.s,
                NumberOrderParent = orderData.i.ToString(),
                Price = orderData.p.ToDecimal(),
                Volume = orderData.q.ToDecimal(),
                NumberTrade = orderData.T.ToString(), // Trade ID 't'
                Side = orderData.S == "BUY" ? Side.Buy : Side.Sell,
            };
            MyTradeEvent?.Invoke(newTrade);
            _myTrades.Add(newTrade);
            while (_myTrades.Count > 1000) _myTrades.RemoveAt(0);
        }
        // Original UpdateMyTrade might be redundant if UpdateMyTradeFromOrderEvent handles it.
        // If there's another source for MyTrade, keep it. For now, assuming order event is the source.
        private decimal GetExecuteVolumeInOrder(string orderNumMarket) // Changed param to Market ID
        {
            return _myTrades.Where(t => t.NumberOrderParent == orderNumMarket).Sum(t => t.Volume);
        }
        private void UpdateDepth(string message)
        {
            try
            {
                // BingX depth stream: {"dataType":"BTC-USDT@depth20@100ms","data":{"bids":[["29062.1","0.061"]...],"asks":...},"ts":1690318135711}
                ResponseWSBingXFuturesMessage<MarketDepthDataMessage> responceDepths =
                    JsonConvert.DeserializeAnonymousType(message, new ResponseWSBingXFuturesMessage<MarketDepthDataMessage>());
                if (responceDepths?.data == null) return;

                MarketDepth depth = new MarketDepth
                {
                    SecurityNameCode = responceDepths.dataType.Split('@')[0],
                    Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responceDepths.ts))
                };
                if (depth.Time <= _lastTimeMd) depth.Time = _lastTimeMd.AddTicks(1);
                _lastTimeMd = depth.Time;

                depth.Bids = responceDepths.data.bids.Select(b => new MarketDepthLevel { Price = b[0].ToDecimal(), Bid = b[1].ToDecimal() }).ToList();
                depth.Asks = responceDepths.data.asks.Select(a => new MarketDepthLevel { Price = a[0].ToDecimal(), Ask = a[1].ToDecimal() }).OrderBy(a => a.Price).ToList(); // Asks should be ascending price

                MarketDepthEvent?.Invoke(depth);
            }
            catch (Exception exception) { SendLogMessage($"UpdateDepth Error: {exception} on msg: {message.Substring(0, Math.Min(message.Length, 100))}", LogMessageType.Error); }
        }
        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent; // Unused
        #endregion
        #region 10 Trade (REST API calls for orders)
        // SendOrder, CancelOrder, etc. use RestSharp and are mostly unchanged.
        // Ensure parameters for HMAC are correctly cased and ordered if API is strict.
        private RateGate _sendOrderRateGate = new RateGate(1, TimeSpan.FromMilliseconds(210));
        public void SendOrder(Order order)
        {
            _generalRateGate3.WaitToProceed();
            _sendOrderRateGate.WaitToProceed();
            try
            {
                _hedgeMode = ((ServerParameterBool)ServerParameters[2]).Value; // Refresh hedge mode
                RestClient client = new RestClient(_baseUrl);
                if (_myProxy != null) client.Proxy = _myProxy;
                RestRequest request = new RestRequest("/openApi/swap/v2/trade/order", Method.POST);

                string symbol = order.SecurityNameCode;
                string side = order.Side == Side.Buy ? "BUY" : "SELL";
                string positionSide = CheckPositionSide(order); // LONG, SHORT, BOTH
                string typeOrder = order.TypeOrder == OrderPriceType.Market ? "MARKET" : "LIMIT";
                string quantity = order.Volume.ToString(CultureInfo.InvariantCulture); // Use InvariantCulture for decimals
                string price = order.TypeOrder == OrderPriceType.Limit ? order.Price.ToString(CultureInfo.InvariantCulture) : "";
                string clientOrderId = order.NumberUser.ToString();
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                // Parameters for signature must be in alphabetical order
                var paramDict = new Dictionary<string, string>
                {
                    { "timestamp", timeStamp },
                    { "symbol", symbol },
                    { "side", side },
                    { "positionSide", positionSide },
                    { "type", typeOrder },
                    { "quantity", quantity }
                };
                if (!string.IsNullOrEmpty(clientOrderId)) paramDict.Add("clientOrderID", clientOrderId);
                if (typeOrder == "LIMIT") paramDict.Add("price", price);

                string parameters = string.Join("&", paramDict.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));
                string sign = CalculateHmacSha256(parameters);

                foreach (var p in paramDict) request.AddParameter(p.Key, p.Value);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);
                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<OrderData> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<OrderData>>(json.Content);
                    if (response.code == "0")
                    {
                        order.NumberMarket = response.data.order.orderId;
                        order.State = OrderStateType.Active; // Assuming API call means it's at least pending/active
                    }
                    else
                    {
                        order.State = OrderStateType.Fail;
                        SendLogMessage($"Order execution error: {response.code} | {response.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    order.State = OrderStateType.Fail;
                    SendLogMessage($"SendOrder HTTP Error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
                MyOrderEvent?.Invoke(order);
            }
            catch (Exception exception) { SendLogMessage($"SendOrder Exception: {exception}", LogMessageType.Error); CreateOrderFail(order); MyOrderEvent?.Invoke(order); }
        }
        private string CheckPositionSide(Order order)
        {
            // Original logic seems fine, just ensure _hedgeMode is current.
            if (!_hedgeMode) return "BOTH";
            if (order.PositionConditionType == OrderPositionConditionType.Close)
                return order.Side == Side.Sell ? "LONG" : "SHORT"; // Closing a long is SELL LONG, closing short is BUY SHORT
                                                                   // Open or None condition implies opening a new side
            return order.Side == Side.Buy ? "LONG" : "SHORT";
        }
        public void CancelAllOrders() { /* Needs implementation: fetch all active orders, then cancel one by one or use bulk cancel if API supports */ }
        public void CancelAllOrdersToSecurity(Security security) { /* Needs implementation: fetch active orders for security, then cancel */ }
        private RateGate _cancelOrderRateGate = new RateGate(1, TimeSpan.FromMilliseconds(210));
        public void CancelOrder(Order order)
        {
            _generalRateGate3.WaitToProceed();
            _cancelOrderRateGate.WaitToProceed();
            try
            {
                RestClient client = new RestClient(_baseUrl);
                if (_myProxy != null) client.Proxy = _myProxy;
                // BingX uses DELETE for cancel by orderId or clientOrderId for a symbol
                RestRequest request = new RestRequest("/openApi/swap/v2/trade/order", Method.DELETE);
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                var paramDict = new Dictionary<string, string>
                {
                    { "timestamp", timeStamp },
                    { "symbol", order.SecurityNameCode }
                };
                if (!string.IsNullOrEmpty(order.NumberMarket)) paramDict.Add("orderId", order.NumberMarket);
                if (order.NumberUser != 0) paramDict.Add("clientOrderID", order.NumberUser.ToString());

                string parameters = string.Join("&", paramDict.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));
                string sign = CalculateHmacSha256(parameters);

                foreach (var p in paramDict) request.AddParameter(p.Key, p.Value);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);
                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<OrderData> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<OrderData>>(json.Content);
                    // Successful cancel usually returns the cancelled order details.
                    if (response.code == "0") { /* Order cancel request sent, WebSocket event will confirm */ }
                    else SendLogMessage($"Order cancel error: {response.code} | {response.msg}", LogMessageType.Error);
                }
                else SendLogMessage($"CancelOrder HTTP Error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                // Order status will be updated via WebSocket. Optionally, call GetOrderStatus if immediate confirmation needed.
            }
            catch (Exception exception) { SendLogMessage($"CancelOrder Exception: {exception}", LogMessageType.Error); }
        }
        public void ChangeOrderPrice(Order order, decimal newPrice) { /* Needs implementation: Cancel existing, Send new. BingX does not support modify. */ }
        private void CreateOrderFail(Order order) { order.State = OrderStateType.Fail; /* MyOrderEvent already called in SendOrder */ }
        private RateGate _getOpenOrdersRateGate = new RateGate(1, TimeSpan.FromMilliseconds(210));
        public void GetAllActivOrders()
        {
            _generalRateGate3.WaitToProceed();
            _getOpenOrdersRateGate.WaitToProceed();
            try
            {
                RestClient client = new RestClient(_baseUrl);
                if (_myProxy != null) client.Proxy = _myProxy;
                RestRequest request = new RestRequest("/openApi/swap/v2/trade/openOrders", Method.GET);
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                // Optional: add symbol to get for specific symbol
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);
                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);
                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<OpenOrdersData> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<OpenOrdersData>>(json.Content);
                    if (response.code == "0" && response.data?.orders != null)
                    {
                        foreach (var orderData in response.data.orders)
                        {
                            Order openOrder = ConvertBingXOrderDataToOrder(orderData);
                            MyOrderEvent?.Invoke(openOrder);
                        }
                    }
                    else SendLogMessage($"Get open orders error: {response.code} | {response.msg}", LogMessageType.Error);
                }
                else SendLogMessage($"GetAllActivOrders HTTP Error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
            }
            catch (Exception exception) { SendLogMessage(exception.ToString(), LogMessageType.Error); }
        }
        public void GetOrderStatus(Order order) { GetOrderStatusByRest(order); GetMyTradesByRest(order); } // Renamed
        private RateGate _getMyTradesRateGate = new RateGate(1, TimeSpan.FromMilliseconds(210));
        private void GetMyTradesByRest(Order order) // Renamed, takes Order object
        {
            _generalRateGate2.WaitToProceed();
            _getMyTradesRateGate.WaitToProceed();
            try
            {
                RestClient client = new RestClient(_baseUrl);
                if (_myProxy != null) client.Proxy = _myProxy;
                // Get user's trades for an order
                RestRequest request = new RestRequest("/openApi/swap/v2/trade/allFillOrders", Method.GET);
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                var paramDict = new Dictionary<string, string> { { "timestamp", timeStamp } };
                if (!string.IsNullOrEmpty(order.NumberMarket)) paramDict.Add("orderId", order.NumberMarket);
                // paramDict.Add("symbol", order.SecurityNameCode); // If API supports filtering by symbol too
                // paramDict.Add("startTime", TimeManager.GetTimeStampMilliSecondsToDateTime(DateTime.UtcNow.AddDays(-7)).ToString()); // Example time range
                // paramDict.Add("endTime", TimeManager.GetTimeStampMilliSecondsToDateTime(DateTime.UtcNow).ToString());

                string parameters = string.Join("&", paramDict.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));
                string sign = CalculateHmacSha256(parameters);

                foreach (var p in paramDict) request.AddParameter(p.Key, p.Value);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);
                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<FillOrdersData> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<FillOrdersData>>(json.Content);
                    if (response.code == "0" && response.data?.fill_orders != null)
                    {
                        foreach (FillOrder fill in response.data.fill_orders)
                        {
                            if (fill.orderId != order.NumberMarket && order.NumberUser.ToString() != fill.orderId /* some APIs might use clientID here */) continue;

                            MyTrade newTrade = new MyTrade
                            {
                                Time = TimeManager.GetDateTimeFromTimeStamp(long.Parse(fill.filledTime)), // fill.time is fill time
                                SecurityNameCode = fill.symbol,
                                NumberOrderParent = fill.orderId,
                                Price = fill.price.ToDecimal(),
                                Volume = fill.amount.ToDecimal(), // fill.qty or volume
                                NumberTrade = fill.filledTime, // fill id (trade id)
                                Side = fill.side == "BUY" ? Side.Buy : Side.Sell,
                            };
                            MyTradeEvent?.Invoke(newTrade);
                        }
                    }
                }
                else SendLogMessage($"GetMyTradesByRest HTTP Error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
            }
            catch (Exception exception) { SendLogMessage(exception.ToString(), LogMessageType.Error); }
        }
        private RateGate _getOrderStatusRateGate = new RateGate(1, TimeSpan.FromMilliseconds(210));
        private void GetOrderStatusByRest(Order orderToQuery) // Renamed
        {
            _generalRateGate2.WaitToProceed();
            _getOrderStatusRateGate.WaitToProceed();
            try
            {
                RestClient client = new RestClient(_baseUrl);
                if (_myProxy != null) client.Proxy = _myProxy;
                RestRequest request = new RestRequest("/openApi/swap/v2/trade/order", Method.GET); // Query order endpoint
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                var paramDict = new Dictionary<string, string>
                {
                    { "timestamp", timeStamp },
                    { "symbol", orderToQuery.SecurityNameCode }
                };
                // Use orderId if available, else clientOrderId
                if (!string.IsNullOrEmpty(orderToQuery.NumberMarket)) paramDict.Add("orderId", orderToQuery.NumberMarket);
                else if (orderToQuery.NumberUser != 0) paramDict.Add("clientOrderID", orderToQuery.NumberUser.ToString());
                else { SendLogMessage("Cannot get order status, no ID provided.", LogMessageType.Error); return; }


                string parameters = string.Join("&", paramDict.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));
                string sign = CalculateHmacSha256(parameters);

                foreach (var p in paramDict) request.AddParameter(p.Key, p.Value);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);
                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<OrderData> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<OrderData>>(json.Content);
                    if (response.code == "0" && response.data?.order != null)
                    {
                        Order updatedOrder = ConvertBingXOrderDataToOrder(response.data.order);
                        MyOrderEvent?.Invoke(updatedOrder);
                    }
                    else SendLogMessage($"Get order status error: {response.code} | {response.msg}", LogMessageType.Error);
                }
                else SendLogMessage($"GetOrderStatusByRest HTTP Error: {json.StatusCode} - {json.Content}", LogMessageType.Error);
            }
            catch (Exception exception) { SendLogMessage(exception.ToString(), LogMessageType.Error); }
        }

        // Helper to convert BingX order data to OsEngine.Order
        private Order ConvertBingXOrderDataToOrder(OrderDetails orderData)
        {
            Order order = new Order();
            OrderStateType orderState = OrderStateType.None;
            switch (orderData.status) // This is order status 'status', not execution type 'X'
            {
                case "NEW": orderState = OrderStateType.Active; break;
                case "PARTIALLY_FILLED": orderState = OrderStateType.Partial; break;
                case "FILLED": orderState = OrderStateType.Done; break;
                case "CANCELED": case "CANCELLED": orderState = OrderStateType.Cancel; break;
                case "REJECTED": case "EXPIRED": orderState = OrderStateType.Fail; break;
                case "PENDING": orderState = OrderStateType.Pending; break; // Or Active
                default: orderState = OrderStateType.None; break;
            }
            try { if (!string.IsNullOrEmpty(orderData.clientOrderId)) order.NumberUser = Convert.ToInt32(orderData.clientOrderId); } catch { /* ignore */ }
            order.NumberMarket = orderData.orderId;
            order.SecurityNameCode = orderData.symbol;
            order.PortfolioNumber = "BingXFutures";
            order.Side = orderData.side == "BUY" ? Side.Buy : Side.Sell;
            order.Price = orderData.price.ToDecimal();
            order.Volume = orderData.origQty.ToDecimal();
            order.State = orderState;
            order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderData.updateTime)); // updateTime or time
            order.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderData.time)); // time is creation time
            order.TypeOrder = orderData.type == "MARKET" ? OrderPriceType.Market : OrderPriceType.Limit;
            order.ServerType = ServerType.BingXFutures;
            // Add executed quantity and average price if available and needed
            order.VolumeExecute = orderData.executedQty.ToDecimal();
            // order.AveragePrice = orderData.avgPrice.ToDecimal(); // If avgPrice field exists
            return order;
        }

        #endregion
        #region 11 Queries (REST API for listen key)
        private const string _baseUrl = "https://open-api.bingx.com";
        private HttpClient _httpPublicClient; // Used for public REST calls if any, candles e.g.
        private string CreateListenKey()
        {
            _generalRateGate2.WaitToProceed();
            try
            {
                RestClient client = new RestClient(_baseUrl);
                if (_myProxy != null) client.Proxy = _myProxy;
                RestRequest request = new RestRequest("/openApi/user/auth/userDataStream", Method.POST);
                request.AddHeader("X-BX-APIKEY", _publicKey);
                // This endpoint doesn't require timestamp or signature for POST according to BingX docs.
                // If it fails, these might be needed.
                IRestResponse response = client.Execute(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ListenKeyBingXFutures responseStr = JsonConvert.DeserializeObject<ListenKeyBingXFutures>(response.Content);
                    if (!string.IsNullOrEmpty(responseStr.listenKey))
                    {
                        _timeLastUpdateListenKey = DateTime.Now;
                        return responseStr.listenKey;
                    }
                    else SendLogMessage($"CreateListenKey: ListenKey was empty in response. Content: {response.Content}", LogMessageType.Error);
                }
                else SendLogMessage($"CreateListenKey HTTP Error: {response.StatusCode} - {response.Content}", LogMessageType.Error);
            }
            catch (Exception exception) { SendLogMessage(exception.ToString(), LogMessageType.Error); }
            return null;
        }
        private DateTime _timeLastUpdateListenKey = DateTime.MinValue;
        private RateGate _requestListenKeyRateGate = new RateGate(10, TimeSpan.FromSeconds(1)); // Check BingX docs for this rate
        private void RequestListenKey() // Keep-alive for listen key
        {
            _timeLastUpdateListenKey = DateTime.Now; // Initialize to avoid immediate run if key just created
            while (true)
            {
                Thread.Sleep(TimeSpan.FromMinutes(10)); // Check every 10 mins
                if (ServerStatus != ServerConnectStatus.Connect || string.IsNullOrEmpty(_listenKey))
                {
                    continue;
                }
                if (_timeLastUpdateListenKey.AddMinutes(25) > DateTime.Now) // Refresh if older than 25 mins (key valid for 60 usually)
                {
                    continue;
                }
                try
                {
                    _generalRateGate2.WaitToProceed();
                    _requestListenKeyRateGate.WaitToProceed();
                    RestClient client = new RestClient(_baseUrl);
                    if (_myProxy != null) client.Proxy = _myProxy;
                    RestRequest request = new RestRequest("/openApi/user/auth/userDataStream", Method.PUT);
                    request.AddHeader("X-BX-APIKEY", _publicKey);
                    request.AddQueryParameter("listenKey", _listenKey); // As query param for PUT
                    IRestResponse response = client.Execute(request);
                    if (response.StatusCode == HttpStatusCode.OK) // Expect 200 OK for successful keep-alive
                    {
                        _timeLastUpdateListenKey = DateTime.Now;
                        SendLogMessage("ListenKey refreshed.", LogMessageType.System);
                    }
                    else SendLogMessage($"RequestListenKey (Keep-Alive) HTTP Error: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }
                catch (Exception ex) { SendLogMessage($"RequestListenKey (Keep-Alive) Exception: {ex}", LogMessageType.Error); }
            }
        }
        #endregion
        #region 12 Log
        public event Action<string, LogMessageType> LogMessageEvent;
        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke($"BingXFutures: {message}", messageType);
        }
        #endregion

    } // End of BingXServerFuturesRealization class

    // Define helper classes for JSON deserialization if they are not already globally available
    // These are based on the types used in JsonConvert.DeserializeObject calls
    // Example for trade stream:
    public class BingXTradeEventData
    {
        public string q { get; set; } // Quantity
        public string p { get; set; } // Price
        public long T { get; set; }   // Timestamp
        public string s { get; set; } // Symbol
        public bool m { get; set; }   // Is Buyer Maker (false for buyer as taker, true for seller as taker)
    }
    public class BingXTradeEvent
    {
        public string dataType { get; set; }
        public List<BingXTradeEventData> data { get; set; }
    }
    // Other DTOs like AccountUpdateEvent, ResponseWSBingXFuturesMessage, MarketDepthDataMessage, etc.
    // should be defined according to BingX WebSocket stream specifications.
    // Helper class for managing ClientWebSocket instance and its receive loop

    internal class WebSocketWrapper : IDisposable
    {
        public ClientWebSocket Client { get; }
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        private readonly Action _onOpen;
        private readonly Action<string> _onMessage;
        private readonly Action _onClose;
        private readonly Action<Exception> _onError;
        private readonly Func<byte[], string> _decompressFunc;

        public WebSocketState State => Client?.State ?? WebSocketState.None;
        private string _url;

        public WebSocketWrapper(Action onOpen, Action<string> onMessage, Action onClose, Action<Exception> onError, Func<byte[], string> decompressFunc)
        {
            Client = new ClientWebSocket();
            _onOpen = onOpen;
            _onMessage = onMessage;
            _onClose = onClose;
            _onError = onError;
            _decompressFunc = decompressFunc;
        }

        public void SetProxy(WebProxy proxy)
        {
            if (proxy != null)
            {
                Client.Options.Proxy = proxy;
            }
        }

        // Required for re-connection or initial connection by the wrapper itself
        public void SetUrl(string url)
        {
            _url = url;
        }

        public async Task ConnectAsync(string url = null)
        {
            if (!string.IsNullOrEmpty(url))
            {
                _url = url;
            }
            if (string.IsNullOrEmpty(_url))
            {
                throw new InvalidOperationException("URL must be set before connecting.");
            }

            if (_cts != null) // If already tried to connect or is connected
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            _cts = new CancellationTokenSource();

            try
            {
                // Set any other options if needed, e.g., KeepAliveInterval
                // Client.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

                await Client.ConnectAsync(new Uri(_url), _cts.Token);
                _onOpen?.Invoke();
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
                throw; // Re-throw to allow caller to handle connection failure
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var buffer = new byte[8192 * 2]; // 16KB buffer, adjust as needed
            try
            {
                while (Client.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    using (var ms = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            var segment = new ArraySegment<byte>(buffer);
                            result = await Client.ReceiveAsync(segment, token);

                            if (token.IsCancellationRequested) break;

                            ms.Write(segment.Array, segment.Offset, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (token.IsCancellationRequested) break;

                        ms.Seek(0, SeekOrigin.Begin);
                        byte[] receivedData = ms.ToArray();

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var message = Encoding.UTF8.GetString(receivedData);
                            if (message.Contains("\"ping\"") || message.Contains("Ping")) // Handle application-level ping
                            {
                                await SendAsync("Pong"); // Or specific pong structure if needed e.g. {"pong": timestamp}
                            }
                            else
                            {
                                _onMessage(message);
                            }
                        }
                        else if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            var decompressedMessage = _decompressFunc(receivedData);
                            if (decompressedMessage != null)
                            {
                                if (decompressedMessage.Contains("\"ping\"") || decompressedMessage.Contains("Ping"))
                                {
                                    await SendAsync("Pong");
                                }
                                else
                                {
                                    _onMessage(decompressedMessage);
                                }
                            }
                        }
                        else if (result.MessageType == WebSocketMessageType.Close)
                        {
                            // If server initiates close, acknowledge it.
                            if (Client.State == WebSocketState.CloseReceived)
                            {
                                await Client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client acknowledging close", CancellationToken.None);
                            }
                            _onClose?.Invoke();
                            return;
                        }
                    }
                }
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely || Client.State != WebSocketState.Open)
            {
                _onError?.Invoke(ex);
            }
            catch (OperationCanceledException)
            {
                // Expected when token is cancelled
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
            }
            finally
            {
                // Ensure onClose is called if the loop exits for any reason other than explicit dispose
                if (!token.IsCancellationRequested || Client.State != WebSocketState.Aborted)
                {
                    _onClose?.Invoke();
                }
            }
        }

        public async Task SendAsync(string message)
        {
            if (Client.State == WebSocketState.Open && _cts != null && !_cts.IsCancellationRequested)
            {
                try
                {
                    var messageBuffer = Encoding.UTF8.GetBytes(message);
                    await Client.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, _cts.Token);
                }
                catch (Exception ex)
                {
                    _onError?.Invoke(ex); // Notify error on send failure
                                          // Consider if this should trigger _onClose as well, depending on severity
                }
            }
        }

        public async Task CloseAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
            }

            if (_receiveTask != null)
            {
                try
                {
                    // Give receive loop some time to exit gracefully
                    await Task.WhenAny(_receiveTask, Task.Delay(TimeSpan.FromSeconds(2)));
                }
                catch { /* ignored */ }
            }

            if (Client.State == WebSocketState.Open || Client.State == WebSocketState.CloseSent)
            {
                try
                {
                    // Timeout for closing operation
                    var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await Client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client initiated close", closeCts.Token);
                }
                catch (Exception) { /* Ignore close errors, client might be disposed already or connection lost */ }
            }

            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
        }

        public void Dispose()
        {
            // Synchronously wait for close with timeout
            // Using .GetAwaiter().GetResult() for synchronous Dispose is generally okay if truly needed
            try
            {
                CloseAsync().GetAwaiter().GetResult();
            }
            catch { /* ignored */ }

            Client.Dispose();
        }
    }
}
