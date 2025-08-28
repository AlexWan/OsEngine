/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Woo.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;



namespace OsEngine.Market.Servers.Woo
{
    public class WooServer : AServer
    {
        public WooServer()
        {
            WooServerRealization realization = new WooServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterString("Application ID", "");
        }
    }

    public class WooServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public WooServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadCheckAliveWebSocket = new Thread(CheckAliveWebSocketThread);
            threadCheckAliveWebSocket.IsBackground = true;
            threadCheckAliveWebSocket.Name = "CheckAliveWebSocketWooX";
            threadCheckAliveWebSocket.Start();

            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.IsBackground = true;
            threadMessageReaderPublic.Name = "MessageReaderPublicWooX";
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.IsBackground = true;
            threadMessageReaderPrivate.Name = "MessageReaderPrivateWooX";
            threadMessageReaderPrivate.Start();
        }

        public void Connect(WebProxy proxy)
        {
            _apiKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _appID = ((ServerParameterString)ServerParameters[2]).Value;

            try
            {
                string url = $"{_baseUrl}/v3/public/systemInfo";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseSystemStatus> responseStatus = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseMessageRest<ResponseSystemStatus>());

                    if (responseStatus.data.status == "0")
                    {
                        CreatePublicWebSocketConnect();
                        CreatePrivateWebSocketConnect();
                    }
                    else
                    {
                        SendLogMessage("Connection can be open. WooX. " + response.Content, LogMessageType.Error);
                        Disconnect();
                    }
                }
                else
                {
                    SendLogMessage("Connection can be open. WooX. Error request", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
                Disconnect();
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _subscribedSecurities.Clear();

            try
            {
                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

            Disconnect();
        }

        public void Disconnect()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType
        {
            get { return ServerType.Woo; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _apiKey;

        private string _secretKey;

        private string _appID;

        private string _baseUrl = "https://api.woox.io";

        private int _limitCandles = 100;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            try
            {
                string url = $"{_baseUrl}/v3/public/instruments";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseSecurities> response = JsonConvert.DeserializeObject<ResponseMessageRest<ResponseSecurities>>(responseMessage.Content);

                    if (response.success == "true")
                    {
                        List<Security> securities = new List<Security>();

                        for (int i = 0; i < response.data.rows.Count; i++)
                        {
                            RowsSymbol item = response.data.rows[i];

                            if (item.status == "TRADING")
                            {
                                Security newSecurity = new Security();

                                newSecurity.Exchange = ServerType.Woo.ToString();
                                newSecurity.Name = item.symbol;
                                newSecurity.NameFull = item.symbol;
                                newSecurity.NameClass = item.symbol.StartsWith("SPOT") ? "Spot_" + item.quoteAsset : "Futures_" + item.quoteAsset;
                                newSecurity.NameId = item.symbol;
                                newSecurity.SecurityType = item.symbol.StartsWith("SPOT") ? SecurityType.CurrencyPair : SecurityType.Futures;
                                newSecurity.DecimalsVolume = item.baseMin.DecimalsCount();
                                newSecurity.Lot = 1;
                                newSecurity.PriceStep = item.quoteTick.ToDecimal();
                                newSecurity.Decimals = item.quoteTick.DecimalsCount();
                                newSecurity.PriceStepCost = newSecurity.PriceStep;
                                newSecurity.State = SecurityStateType.Activ;
                                newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                                newSecurity.MinTradeAmount = item.baseMin.ToDecimal();
                                newSecurity.VolumeStep = item.baseMin.ToDecimal();

                                securities.Add(newSecurity);
                            }
                        }

                        SecurityEvent(securities);
                    }
                    else
                    {
                        SendLogMessage($"Securities error. {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Securities request error. Code: {responseMessage.StatusCode}, {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(6000));

        private RateGate _rateGatePositions = new RateGate(1, TimeSpan.FromMilliseconds(6000));

        private List<Portfolio> _portfolios;

        public void GetPortfolios()
        {
            CreateACommonPortfolio();
            CreateQueryPortfolio(true);
        }

        private void CreateACommonPortfolio()
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                string requestPath = "/v3/account/info";
                string requestBody = null;
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string signString = $"{timestamp}GET{requestPath}{requestBody}";

                string apiSignature;
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey)))
                {
                    byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signString));
                    apiSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                }

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(requestPath, Method.GET);

                request.AddHeader("x-api-key", _apiKey);
                request.AddHeader("x-api-signature", apiSignature);
                request.AddHeader("x-api-timestamp", timestamp.ToString());
                //request.AddHeader("Content-Type", "application/json");

                request.AddParameter("application/json", requestBody, ParameterType.RequestBody);

                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseCommonPortfolio> response = JsonConvert.DeserializeObject<ResponseMessageRest<ResponseCommonPortfolio>>(responseMessage.Content);

                    if (response.success == "true")
                    {
                        _portfolios = new List<Portfolio>();

                        Portfolio portfolio = new Portfolio();
                        portfolio.Number = "WooPortfolio";
                        portfolio.ValueBegin = response.data.totalAccountValue.ToDecimal();
                        portfolio.ValueCurrent = response.data.totalTradingValue.ToDecimal();
                        _portfolios.Add(portfolio);

                        PortfolioEvent(_portfolios);
                    }
                    else
                    {
                        SendLogMessage($"Common portfolio error. {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Common portfolio request error. Code: {responseMessage.StatusCode}, {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateQueryPortfolio(bool IsUpdateValueBegin)
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                string requestPath = "/v3/asset/balances";
                string requestBody = null;
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string signString = $"{timestamp}GET{requestPath}{requestBody}";

                string apiSignature;
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey)))
                {
                    byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signString));
                    apiSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                }

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(requestPath, Method.GET);

                request.AddHeader("x-api-key", _apiKey);
                request.AddHeader("x-api-signature", apiSignature);
                request.AddHeader("x-api-timestamp", timestamp.ToString());
                //request.AddHeader("Content-Type", "application/json");

                request.AddParameter("application/json", requestBody, ParameterType.RequestBody);

                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponsePortfolios> response = JsonConvert.DeserializeObject<ResponseMessageRest<ResponsePortfolios>>(responseMessage.Content);

                    if (response.success == "true")
                    {
                        Portfolio portfolio = _portfolios[0];

                        for (int i = 0; i < response.data.holding.Count; i++)
                        {
                            Holding balanceDetails = response.data.holding[i];

                            PositionOnBoard pos = new PositionOnBoard();

                            pos.PortfolioName = "WooPortfolio";
                            pos.SecurityNameCode = balanceDetails.token;
                            pos.ValueBlocked = balanceDetails.frozen.ToDecimal();
                            pos.ValueCurrent = balanceDetails.holding.ToDecimal();
                            pos.UnrealizedPnl = balanceDetails.pnl24H.ToDecimal();

                            if (IsUpdateValueBegin)
                            {
                                pos.ValueBegin = balanceDetails.holding.ToDecimal();
                            }

                            portfolio.SetNewPosition(pos);
                        }

                        PortfolioEvent(_portfolios);
                    }
                    else
                    {
                        SendLogMessage($"Portfolio error. {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio request error. Code: {responseMessage.StatusCode}, {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateQueryPosition(bool IsUpdateValueBegin)
        {
            _rateGatePositions.WaitToProceed();

            try
            {
                string timeStamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                string data = $"{timeStamp}GET/v3/positions";
                string signature = GenerateSignature(data);

                RestClient client = new RestClient($"{_baseUrl}/v3/positions");
                RestRequest request = new RestRequest(Method.GET);
                request.AddHeader("x-api-timestamp", timeStamp);
                request.AddHeader("x-api-key", _apiKey);
                request.AddHeader("x-api-signature", signature);

                IRestResponse responseMessage = client.Execute(request);
                string JsonResponse = responseMessage.Content;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    UpdatePosition(JsonResponse, IsUpdateValueBegin);
                }
                else
                {
                    SendLogMessage($"Http State Code1: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePosition(string json, bool IsUpdateValueBegin)
        {
            ResponseMessagePositions response = JsonConvert.DeserializeObject<ResponseMessagePositions>(json);

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "WooPortfolio";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            List<ResponseMessagePositions.Positions> item = response.data.positions;

            for (int i = 0; i < item.Count; i++)
            {
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "WooPortfolio";
                pos.SecurityNameCode = item[i].symbol;
                pos.ValueBlocked = 0;
                pos.ValueCurrent = item[i].holding.ToDecimal();

                if (IsUpdateValueBegin)
                {
                    pos.ValueBegin = item[i].holding.ToDecimal();
                }
                portfolio.SetNewPosition(pos);
            }
            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!CheckTf(tfTotalMinutes))
            {
                return null;
            }

            List<Candle> allCandles = new List<Candle>();

            DateTime startTimeData = startTime;

            do
            {
                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTimeData);

                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

                List<Candle> candles = RequestCandleHistory(security.Name, interval, from);

                if (candles == null || candles.Count == 0)
                {
                    break;
                }

                Candle last = candles[candles.Count - 1];

                if (last.TimeStart >= endTime)

                {
                    for (int i = 0; i < candles.Count; i++)
                    {
                        if (candles[i].TimeStart <= endTime)
                        {
                            allCandles.Add(candles[i]);
                        }
                    }
                    break;
                }

                allCandles.AddRange(candles);

                startTimeData = startTimeData.AddMinutes(tfTotalMinutes * _limitCandles);

                if (startTimeData >= DateTime.UtcNow)
                {
                    break;
                }

            } while (true);

            return allCandles;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
                startTime >= DateTime.Now ||
                actualTime > endTime ||
                actualTime > DateTime.Now ||
                endTime < DateTime.UtcNow.AddYears(-20))
            {
                return false;
            }
            return true;
        }

        private bool CheckTf(int timeFrameMinutes)
        {
            if (timeFrameMinutes == 1 ||
                timeFrameMinutes == 5 ||
                timeFrameMinutes == 15 ||
                timeFrameMinutes == 30 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 240 ||
                timeFrameMinutes == 1440)
            {
                return true;
            }
            return false;
        }

        private string GetInterval(TimeSpan timeFrame)
        {
            if (timeFrame.Minutes != 0)
            {
                return $"{timeFrame.Minutes}m";
            }
            else
            {
                return $"{timeFrame.Hours}h";
            }
        }

        private readonly RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private List<Candle> RequestCandleHistory(string security, string resolution, long fromTimeStamp)
        {
            _rgCandleData.WaitToProceed();

            try
            {
                string queryParam = $"start_time={fromTimeStamp}&";
                queryParam += $"symbol={security}&";
                queryParam += $"type={resolution}";

                string url = "https://api-pub.woo.org/v1/hist/kline?" + queryParam;
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);
                string JsonResponse = responseMessage.Content;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return ConvertCandles(JsonResponse);
                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode} - {JsonResponse}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Candle> ConvertCandles(string json)
        {
            ResponseMessageCandles response = JsonConvert.DeserializeObject<ResponseMessageCandles>(json);

            List<Candle> candles = new List<Candle>();

            List<ResponseMessageCandles.Rows> item = response.data.rows;

            for (int i = 0; i < item.Count; i++)
            {

                if (CheckCandlesToZeroData(item[i]))
                {
                    continue;
                }

                Candle candle = new Candle();

                candle.State = CandleState.Finished;
                candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].start_timestamp));
                candle.Volume = item[i].volume.ToDecimal();
                candle.Close = response.data.rows[i].close.ToDecimal();
                candle.High = item[i].high.ToDecimal();
                candle.Low = item[i].low.ToDecimal();
                candle.Open = item[i].open.ToDecimal();

                candles.Add(candle);
            }

            return candles;
        }

        private bool CheckCandlesToZeroData(ResponseMessageCandles.Rows item)
        {
            if (item.close.ToDecimal() == 0 ||
                item.open.ToDecimal() == 0 ||
                item.high.ToDecimal() == 0 ||
                item.low.ToDecimal() == 0)
            {
                return true;
            }
            return false;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.Now;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, endTime);
        }

        #endregion

        #region 6 WebSocket creation

        private string _webSocketUrlPublic = "wss://wss.woox.io/v3/public";

        private string _webSocketUrlPrivate = "wss://wss.woox.io/v3/private";

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

        private string _listenKey = "";

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (FIFOListWebSocketPublicMessage == null)
                {
                    FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                }

                _webSocketPublic.Add(CreateNewPublicSocket());
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewPublicSocket()
        {
            try
            {
                WebSocket webSocketPublicNew = new WebSocket(_webSocketUrlPublic);

                //if (_myProxy != null)
                //{
                //    webSocketPublicNew.SetProxy(_myProxy);
                //}

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += WebSocketPublicNew_OnOpen;
                webSocketPublicNew.OnMessage += WebSocketPublicNew_OnMessage;
                webSocketPublicNew.OnError += WebSocketPublicNew_OnError;
                webSocketPublicNew.OnClose += WebSocketPublicNew_OnClose;
                webSocketPublicNew.Connect();

                return webSocketPublicNew;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void CreatePrivateWebSocketConnect()
        {
            try
            {
                if (_webSocketPrivate != null)
                {
                    return;
                }

                _listenKey = CreateListenKey();

                if (_listenKey == null)
                {
                    SendLogMessage("Autorization error. Listen key is note created", LogMessageType.Error);
                    return;
                }

                string urlStr = $"{_webSocketUrlPrivate}?key={_listenKey}";

                _webSocketPrivate = new WebSocket(urlStr);

                //if (_myProxy != null)
                //{
                //    _webSocketPrivate.SetProxy(_myProxy);
                //}

                _webSocketPrivate.EmitOnPing = true;
                _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                _webSocketPrivate.OnError += _webSocketPrivate_OnError;
                _webSocketPrivate.Connect();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebSocketConnection()
        {
            if (_webSocketPublic != null)
            {
                try
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        webSocketPublic.OnOpen -= WebSocketPublicNew_OnOpen;
                        webSocketPublic.OnClose -= WebSocketPublicNew_OnClose;
                        webSocketPublic.OnMessage -= WebSocketPublicNew_OnMessage;
                        webSocketPublic.OnError -= WebSocketPublicNew_OnError;

                        if (webSocketPublic.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.CloseAsync();
                        }

                        webSocketPublic = null;
                    }
                }
                catch
                {
                    // ignore
                }

                _webSocketPublic.Clear();
            }

            if (_webSocketPrivate != null)
            {
                try
                {
                    _webSocketPrivate.OnOpen -= _webSocketPrivate_OnOpen;
                    _webSocketPrivate.OnClose -= _webSocketPrivate_OnClose;
                    _webSocketPrivate.OnMessage -= _webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError -= _webSocketPrivate_OnError;
                    _webSocketPrivate.CloseAsync();
                }
                catch
                {
                    // ignore
                }

                _webSocketPrivate = null;
            }
        }

        private string _socketActivateLocker = "socketAcvateLocker";

        private void CheckSocketsActivate()
        {
            lock (_socketActivateLocker)
            {

                if (_webSocketPrivate == null
                    || _webSocketPrivate.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (_webSocketPublic.Count == 0)
                {
                    Disconnect();
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[0];

                if (webSocketPublic == null
                    || webSocketPublic?.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Connect;

                    if (ConnectEvent != null)
                    {
                        ConnectEvent();
                    }
                }
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublicNew_OnClose(object arg1, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnError(object arg1, OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnMessage(object arg1, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                FIFOListWebSocketPublicMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage("Trade socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnOpen(object arg1, EventArgs arg2)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("BitMartSpot WebSocket Public connection open", LogMessageType.System);
                    CheckSocketsActivate();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnError(object arg1, OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnMessage(object arg1, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }
                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnClose(object arg1, CloseEventArgs arg2)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnOpen(object arg1, EventArgs arg2)
        {
            try
            {
                CheckSocketsActivate();
                SendLogMessage("BitMartSpot WebSocket Private connection open", LogMessageType.System);

                _webSocketPrivate.Send($"{{\"id\": 1, \"cmd\": \"SUBSCRIBE\", \"params\": [\"account\",\"balance\", \"position\", \"executionreport\"]}}");
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        private void CheckAliveWebSocketThread()
        {
            while (true)
            {
                Thread.Sleep(15000);

                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        if (webSocketPublic != null
                            && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            webSocketPublic.Send($"{{ \"cmd\": \"PING\", \"ts\": {timestamp} }}");
                        }
                        else
                        {
                            Disconnect();
                        }
                    }

                    if (_webSocketPrivate != null &&
                        (_webSocketPrivate.ReadyState == WebSocketState.Open ||
                        _webSocketPrivate.ReadyState == WebSocketState.Connecting))
                    {
                        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        _webSocketPrivate.Send($"{{ \"cmd\": \"PING\", \"ts\": {timestamp} }}");
                    }
                    else
                    {
                        Disconnect();
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        #endregion

        #region 9 Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(300));

        private List<string> _subscribedSecurities = new List<string>();

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Equals(security.Name))
                    {
                        return;
                    }
                }

                _subscribedSecurities.Add(security.Name);

                if (_webSocketPublic.Count == 0)
                {
                    return;
                }

                //if (_webSocketPublic.Count >= 20)
                //{
                //    //SendLogMessage($"Limit 20 connections {_webSocketPublic.Count}", LogMessageType.Error);
                //    return;
                //}

                WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 50 == 0)
                {
                    // creating a new socket
                    WebSocket newSocket = CreateNewPublicSocket();

                    DateTime timeEnd = DateTime.Now.AddSeconds(10);

                    while (newSocket.ReadyState != WebSocketState.Open)
                    {
                        Thread.Sleep(1000);

                        if (timeEnd < DateTime.Now)
                        {
                            break;
                        }
                    }

                    if (newSocket.ReadyState == WebSocketState.Open)
                    {
                        _webSocketPublic.Add(newSocket);
                        webSocketPublic = newSocket;
                    }
                }

                if (webSocketPublic != null)
                {
                    webSocketPublic.Send($"{{\"id\": 1, \"cmd\": \"SUBSCRIBE\", \"params\": [\"trade@{security.Name}\"]}}");
                    webSocketPublic.Send($"{{\"id\": 1, \"cmd\": \"SUBSCRIBE\", \"params\": [\"orderbookupdate@{security.Name}@50\"]}}");
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }


        private void UnsubscribeFromAllWebSockets()
        {
            try
            {
                if (_webSocketPublic.Count != 0
                    && _webSocketPublic != null)
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        try
                        {
                            if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open)
                            {
                                if (_subscribedSecurities != null)
                                {
                                    for (int j = 0; j < _subscribedSecurities.Count; j++)
                                    {
                                        string securityName = _subscribedSecurities[j];

                                        webSocketPublic.Send($"{{\"id\": 1, \"cmd\": \"UN_SUBSCRIBE\", \"params\": [\"trade@{securityName}\"]}}");
                                        webSocketPublic.Send($"{{\"id\": 1, \"cmd\": \"UN_SUBSCRIBE\", \"params\": [\"orderbookupdate@{securityName}@50\"]}}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            if (_webSocketPrivate != null
                && _webSocketPrivate.ReadyState == WebSocketState.Open)
            {
                try
                {
                    _webSocketPrivate.Send($"{{\"id\": 1, \"cmd\": \"UN_SUBSCRIBE\", \"params\": [\"account\",\"balance\", \"position\", \"executionreport\"]}}");
                }
                catch
                {
                    // ignore
                }
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent;

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private void MessageReaderPublic()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (FIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    FIFOListWebSocketPublicMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("PONG"))
                    {
                        continue;
                    }

                    if (message.Contains("\"cmd\":\"SUBSCRIBE\""))
                    {
                        continue;
                    }

                    if (message.Contains("orderbook"))
                    {
                        UpdateDepth(message);
                        continue;
                    }

                    if (message.Contains("trade"))
                    {
                        UpdateTrade(message);
                        continue;
                    }

                    if (message.Contains("ERROR"))
                    {
                        SendLogMessage(message, LogMessageType.Error);
                        continue;
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void MessageReaderPrivate()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (FIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    FIFOListWebSocketPrivateMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("PONG"))
                    {
                        continue;
                    }

                    if (message.Contains("\"cmd\":\"SUBSCRIBE\""))
                    {
                        continue;
                    }

                    if (message.Contains("account"))
                    {
                        UpdateAccount(message);
                        continue;
                    }

                    if (message.Contains("executionreport"))
                    {
                        UpdateOrder(message);
                        continue;
                    }

                    if (message.Contains("position"))
                    {
                        UpdatePositionFromSubscribe(message);
                        continue;
                    }

                    if (message.Contains("balance"))
                    {
                        UpdatePortfolioFromSubscribe(message);
                        continue;
                    }

                    if (message.Contains("ERROR"))
                    {
                        SendLogMessage(message, LogMessageType.Error);
                        continue;
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            try
            {
                ResponseWebSocketMessage<ResponseChannelTrades> responseTrade = JsonConvert.DeserializeObject<ResponseWebSocketMessage<ResponseChannelTrades>>(message);

                if (responseTrade == null)
                {
                    return;
                }

                if (responseTrade.data == null)
                {
                    return;
                }

                ResponseChannelTrades item = responseTrade.data;

                Trade trade = new Trade();
                trade.SecurityNameCode = item.s;
                trade.Price = item.px.ToDecimal();
                trade.Id = responseTrade.ts;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.ts));
                trade.Volume = item.sx.ToDecimal();
                trade.Side = item.sd.Equals("BUY") ? Side.Buy : Side.Sell;

                NewTradesEvent(trade);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private List<MarketDepth> _marketDepths = new List<MarketDepth>();

        private DateTime _lastTimeMd = DateTime.MinValue;


        private void SnapshotDepth(string nameSecurity)
        {
            try
            {
                string url = $"{_baseUrl}/v3/public/orderbook?maxLevel=25&symbol={nameSecurity}";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseMessageRest<MarketDepthData> response = JsonConvert.DeserializeObject<ResponseMessageRest<MarketDepthData>>(responseMessage.Content);

                    if (response.success == "true")
                    {
                        MarketDepth marketDepth = null;

                        for (int i = 0; i < _marketDepths.Count; i++)
                        {
                            if (_marketDepths[i].SecurityNameCode == nameSecurity)
                            {
                                marketDepth = _marketDepths[i];
                                break;
                            }
                        }

                        if (marketDepth == null)
                        {
                            marketDepth = new MarketDepth();
                            _marketDepths.Add(marketDepth);
                        }
                        else
                        {
                            marketDepth.Asks.Clear();
                            marketDepth.Bids.Clear();
                        }

                        List<MarketDepthLevel> asks = new List<MarketDepthLevel>();
                        List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                        marketDepth.SecurityNameCode = nameSecurity;

                        if (response.data.asks != null)
                        {
                            for (int i = 0; i < response.data.asks.Count; i++)
                            {
                                asks.Add(new MarketDepthLevel()
                                {
                                    Ask = response.data.asks[i].quantity.ToDecimal(),
                                    Price = response.data.asks[i].price.ToDecimal(),
                                });
                            }
                        }

                        if (response.data.bids.Count != null)
                        {
                            for (int i = 0; i < response.data.bids.Count; i++)
                            {
                                bids.Add(new MarketDepthLevel()
                                {
                                    Bid = response.data.bids[i].quantity.ToDecimal(),
                                    Price = response.data.bids[i].price.ToDecimal(),
                                });
                            }
                        }

                        marketDepth.Asks = asks;
                        marketDepth.Bids = bids;

                        marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(response.timestamp));

                        if (MarketDepthEvent != null)
                        {
                            MarketDepthEvent(marketDepth.GetCopy());
                        }
                    }
                    else
                    {
                        SendLogMessage($"MarketDepth snapshot error. {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"MarketDepth snapshot request error. Code: {responseMessage.StatusCode}, {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateDepth(string message)
        {
            try
            {
                ResponseWebSocketMessage<ResponseChannelBook> responseDepth = JsonConvert.DeserializeObject<ResponseWebSocketMessage<ResponseChannelBook>>(message);
                ResponseChannelBook item = responseDepth.data;

                if (item == null)
                {
                    return;
                }

                if (_marketDepths == null
                       || _marketDepths.Count == 0)
                {
                    SnapshotDepth(item.s);
                    return;
                }

                MarketDepth marketDepth = null;

                for (int i = 0; i < _marketDepths.Count; i++)
                {
                    if (_marketDepths[i].SecurityNameCode == responseDepth.data.s)
                    {
                        marketDepth = _marketDepths[i];
                        break;
                    }
                }

                if (marketDepth == null)
                {
                    SnapshotDepth(item.s);
                    return;
                }

                if (marketDepth.Asks.Count == 0
                    || marketDepth.Bids.Count == 0)
                {
                    SnapshotDepth(item.s);
                    return;
                }

                marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));

                if (marketDepth.Time <= _lastTimeMd)
                {
                    _lastTimeMd = _lastTimeMd.AddTicks(1);
                    marketDepth.Time = _lastTimeMd;
                }
                else
                {
                    _lastTimeMd = marketDepth.Time;
                }

                _lastTimeMd = marketDepth.Time;

                ApplyLevels(item.bids, marketDepth.Bids, isBid: true);
                ApplyLevels(item.asks, marketDepth.Asks, isBid: false);

                List<MarketDepthLevel> topBids = new List<MarketDepthLevel>();

                for (int i = 0; i < marketDepth.Bids.Count && i < 25; i++)
                {
                    topBids.Add(marketDepth.Bids[i]);
                }

                marketDepth.Bids = topBids;

                List<MarketDepthLevel> topAsks = new List<MarketDepthLevel>();

                for (int i = 0; i < marketDepth.Asks.Count && i < 25; i++)
                {
                    topAsks.Add(marketDepth.Asks[i]);
                }

                marketDepth.Asks = topAsks;

                if (marketDepth.Bids.Count == 0
                    || marketDepth.Asks.Count == 0)
                {
                    return;
                }

                MarketDepthEvent?.Invoke(marketDepth.GetCopy());
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void ApplyLevels(List<List<string>> updates, List<MarketDepthLevel> levels, bool isBid)
        {
            for (int i = 0; i < updates.Count; i++)
            {
                decimal price = updates[i][0].ToDecimal();
                decimal size = updates[i][1].ToDecimal();

                MarketDepthLevel existing = levels.Find(x => x.Price == price);

                if (size == 0)
                {
                    if (existing != null)
                    {
                        levels.Remove(existing);
                    }
                }
                else
                {
                    if (existing != null)
                    {
                        if (isBid)
                        {
                            existing.Bid = size;
                        }
                        else
                        {
                            existing.Ask = size;
                        }
                    }
                    else
                    {
                        MarketDepthLevel level = new MarketDepthLevel { Price = price };

                        if (isBid)
                        {
                            level.Bid = size;
                        }
                        else
                        {
                            level.Ask = size;
                        }

                        levels.Add(level);
                    }
                }
            }

            if (isBid)
            {
                levels.Sort((a, b) => b.Price.CompareTo(a.Price));
            }
            else
            {
                levels.Sort((a, b) => a.Price.CompareTo(b.Price));
            }
        }


        private void UpdateAccount(string message)
        {
            if (_portfolios == null)
            {
                return;
            }

            try
            {
                ResponseWebSocketMessage<ResponseChannelAccount> responseBalance = JsonConvert.DeserializeObject<ResponseWebSocketMessage<ResponseChannelAccount>>(message);

                Portfolio portfolio = _portfolios[0];

                portfolio.ValueBegin = responseBalance.data.v.ToDecimal();
                portfolio.ValueCurrent = responseBalance.data.fc.ToDecimal();
                portfolio.ValueBlocked = responseBalance.data.tc.ToDecimal() - responseBalance.data.fc.ToDecimal();

                PortfolioEvent(_portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePortfolioFromSubscribe(string message)
        {
            try
            {
                ResponseWebSocketMessage<BalanceData> responseBalance = JsonConvert.DeserializeObject<ResponseWebSocketMessage<BalanceData>>(message);

                Portfolio portfolio = _portfolios[0];

                for (int i = 0; i < responseBalance.data.balances.Count; i++)
                {
                    ResponseChannelPortfolio balanceDetails = responseBalance.data.balances[i];

                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "WooPortfolio";
                    pos.SecurityNameCode = balanceDetails.t;
                    pos.ValueBlocked = balanceDetails.f.ToDecimal();
                    pos.ValueCurrent = balanceDetails.h.ToDecimal();
                    pos.UnrealizedPnl = balanceDetails.pnl.ToDecimal();
                    portfolio.SetNewPosition(pos);
                }

                PortfolioEvent(_portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePositionFromSubscribe(string message)
        {
            try
            {

                ResponseWebSocketMessage<PositionData> responsePositions = JsonConvert.DeserializeObject<ResponseWebSocketMessage<PositionData>>(message);

                Portfolio portfolio = _portfolios[0];

                for (int i = 0; i < responsePositions.data.positions.Count; i++)
                {
                    ResponseChannelPositions balanceDetails = responsePositions.data.positions[i];

                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "WooPortfolio";
                    pos.SecurityNameCode = balanceDetails.s;
                    pos.ValueBlocked = 0;
                    pos.ValueCurrent = balanceDetails.h.ToDecimal();
                    pos.UnrealizedPnl = balanceDetails.pnl.ToDecimal();

                    portfolio.SetNewPosition(pos);
                }

                PortfolioEvent(_portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                ResponseWebSocketMessage<ResponseChannelOrder> responseOrder = JsonConvert.DeserializeObject<ResponseWebSocketMessage<ResponseChannelOrder>>(message);


                ResponseChannelOrder item = responseOrder.data;

                if (string.IsNullOrEmpty(item.cid))
                {
                    return;
                }

                if (item.mt != "0")
                {
                    switch (item.mt)
                    {
                        case "1":
                            SendLogMessage("Editing order be rejected", LogMessageType.Error);
                            break;
                        case "2":
                            SendLogMessage("Canceling order be rejected", LogMessageType.Error);
                            break;
                        case "3":
                            SendLogMessage("Canceling ALL orders be rejected", LogMessageType.Error);
                            break;
                    }
                    return;
                }

                OrderStateType stateType = GetOrderState(item.ss);

                Order newOrder = new Order();
                newOrder.SecurityNameCode = item.s;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.ts));
                newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.ts));
                newOrder.NumberUser = Convert.ToInt32(item.cid);
                newOrder.NumberMarket = item.oid.ToString();
                newOrder.Side = item.sd.Equals("BUY") ? Side.Buy : Side.Sell;
                newOrder.State = stateType;
                newOrder.Volume = item.sx.ToDecimal();
                newOrder.VolumeExecute = item.esx.ToDecimal();
                newOrder.Price = item.px.ToDecimal();

                if (item.t == "MARKET")
                {
                    newOrder.TypeOrder = OrderPriceType.Market;
                }
                if (item.t == "LIMIT")
                {
                    newOrder.TypeOrder = OrderPriceType.Limit;
                }
                newOrder.ServerType = ServerType.Woo;
                newOrder.PortfolioNumber = "WooPortfolio";

                MyOrderEvent(newOrder);

                if (newOrder.State == OrderStateType.Done)
                {
                    MyTrade myTrade = new MyTrade();

                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));
                    myTrade.NumberOrderParent = item.oid;
                    myTrade.NumberTrade = item.tid;
                    myTrade.Price = item.epx.ToDecimal();
                    myTrade.SecurityNameCode = item.s;
                    myTrade.Side = item.sd.Equals("BUY") ? Side.Buy : Side.Sell;
                    myTrade.Volume = item.esx.ToDecimal();

                    MyTradeEvent(myTrade);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("NEW"):
                    stateType = OrderStateType.Active;
                    break;
                case ("FILLED"):
                    stateType = OrderStateType.Done;
                    break;
                case ("REJECTED"):
                    stateType = OrderStateType.Fail;
                    break;
                case ("PARTIAL_FILLED"):
                    stateType = OrderStateType.Partial;
                    break;
                case ("CANCELLED"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("REPLACED"):
                    stateType = OrderStateType.Active;
                    break;
                default:
                    stateType = OrderStateType.None;
                    break;
            }
            return stateType;
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        public event Action<Funding> FundingUpdateEvent;

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string strTypeOrder = order.TypeOrder.ToString() == "Limit" ? "LIMIT" : "MARKET";
                string strSide = order.Side.ToString() == "Buy" ? "BUY" : "SELL";

                string strRequest = $"client_order_id={order.NumberUser}&";
                strRequest += $"order_price={order.Price.ToString().Replace(",", ".")}&";
                strRequest += $"order_quantity={order.Volume.ToString().Replace(",", ".")}&";
                strRequest += $"order_type={strTypeOrder}&";
                strRequest += $"side={strSide}&";
                strRequest += $"symbol={order.SecurityNameCode}";

                string url = $"{_baseUrl}/v1/order";
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature($"{strRequest}|{timeStamp}");

                RestClient client = new RestClient(url);

                RestRequest request = new RestRequest(Method.POST);
                request.AddHeader("x-api-timestamp", timeStamp);
                request.AddHeader("x-api-key", _apiKey);
                request.AddHeader("x-api-signature", signature);
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddParameter("application/x-www-form-urlencoded", strRequest, ParameterType.RequestBody);

                IRestResponse responseMessage = client.Execute(request);
                string JsonResponse = responseMessage.Content;

                if (responseMessage.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    order.State = OrderStateType.Fail;
                    MyOrderEvent(order);
                    SendLogMessage($"SendOrder. Http State Code: Http State Code: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string json = JsonConvert.SerializeObject(new
                {
                    price = newPrice.ToString().Replace(",", ".")
                });

                string ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string data = $"{ts}PUT/v3/order/{order.NumberMarket}{json}";
                string signature = GenerateSignature(data);

                RestClient client = new RestClient($"https://api.woo.org/v3/order/{order.NumberMarket}");
                RestRequest request = new RestRequest(Method.PUT);
                request.AddHeader("x-api-timestamp", ts);
                request.AddHeader("x-api-key", _apiKey);
                request.AddHeader("x-api-signature", signature);
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", json, ParameterType.RequestBody);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage($"ChangeOrderPrice FAIL. Http State Code: {response.StatusCode}, {response}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public void CancelAllOrders()
        {
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            _rateGateCancelOrder.WaitToProceed();

            try
            {
                string ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                string data = $"symbol={security.Name}";
                string signature = GenerateSignature($"{data}|{ts}");

                RestClient client = new RestClient($"{_baseUrl}/v1/orders");
                RestRequest request = new RestRequest(Method.DELETE);
                request.AddHeader("x-api-timestamp", ts);
                request.AddHeader("x-api-key", _apiKey);
                request.AddHeader("x-api-signature", signature);
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/x-www-form-urlencoded", data, ParameterType.RequestBody);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage($"CancelOrder. Http State Code: {response.StatusCode}, {response}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            if (OrderStateType.Cancel == order.State)
            {
                return false;
            }
            try
            {
                string ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                string data = $"order_id={order.NumberMarket}&symbol={order.SecurityNameCode}";
                string signature = GenerateSignature($"{data}|{ts}");

                RestClient client = new RestClient($"{_baseUrl}/v1/order");
                RestRequest request = new RestRequest(Method.DELETE);
                request.AddHeader("x-api-timestamp", ts);
                request.AddHeader("x-api-key", _apiKey);
                request.AddHeader("x-api-signature", signature);
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/x-www-form-urlencoded", data, ParameterType.RequestBody);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return true;
                    SendLogMessage($"CancelOrder. Http State Code: {response.StatusCode}, {response}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public void GetAllActivOrders()
        {

        }

        public OrderStateType GetOrderStatus(Order order)
        {
            return OrderStateType.None;
        }

        #endregion

        #region 12 Queries

        private RateGate _rateGateListenKey = new RateGate(5, TimeSpan.FromMilliseconds(10000));

        private string CreateListenKey()
        {
            _rateGateListenKey.WaitToProceed();

            try
            {
                string requestPath = "/v3/account/listenKey";

                var requestBodyObject = new
                {
                    type = "WEBSOCKET",
                };

                string requestBody = JsonConvert.SerializeObject(requestBodyObject);

                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                string signString = $"{timestamp}POST{requestPath}{requestBody}";

                string apiSignature;
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey)))
                {
                    byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signString));
                    apiSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                }

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(requestPath, Method.POST);

                request.AddHeader("x-api-key", _apiKey);
                request.AddHeader("x-api-signature", apiSignature);
                request.AddHeader("x-api-timestamp", timestamp.ToString());
                //request.AddHeader("Content-Type", "application/json");

                request.AddParameter("application/json", requestBody, ParameterType.RequestBody);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseListenKey> responseKey = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseMessageRest<ResponseListenKey>());

                    if (responseKey.success == "true")
                    {
                        return responseKey.data.authKey;
                    }
                    else
                    {
                        SendLogMessage("Listen Key request error. " + response.Content, LogMessageType.Error);
                        return null;
                    }

                }
                else
                {
                    SendLogMessage("Listen Key error. Code: " + response.StatusCode + ", " + response.Content, LogMessageType.Error);
                    return null;
                }

            }
            catch (Exception ex)
            {
                SendLogMessage($"Listen Key error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }



        private string GenerateSignature(string data)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(_secretKey);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
            {
                byte[] hash = hmac.ComputeHash(dataBytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}