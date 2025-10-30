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
using OsEngine.Market.Servers.ExMo.ExmoSpot.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;



namespace OsEngine.Market.Servers.ExMo.ExmoSpot
{
    public class ExmoSpotServer : AServer
    {
        public ExmoSpotServer()
        {
            ExmoSpotServerRealization realization = new ExmoSpotServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
        }
    }

    public class ExmoSpotServerRealization : IServerRealization
    {

        #region 1 Constructor, Status, Connection

        public ExmoSpotServerRealization()
        {
            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.IsBackground = true;
            threadMessageReaderPublic.Name = "MessageReaderPublicExmoSpot";
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.IsBackground = true;
            threadMessageReaderPrivate.Name = "MessageReaderPrivateExmoSpot";
            threadMessageReaderPrivate.Start();
        }

        public void Connect(WebProxy proxy)
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(_publicKey)
                || string.IsNullOrEmpty(_secretKey))
            {
                SendLogMessage("Connection terminated. You must specify the public and private keys. You can get it on the ExmoSpot website",
                    LogMessageType.Error);
                return;
            }

            try
            {
                RestRequest requestRest = new RestRequest("/currency", Method.POST);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
                }
                else
                {
                    SendLogMessage("Connection can be open. ExmoSpot. Error request", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
                _subscribedSecurities.Clear();
                DeleteWebSocketConnection();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
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

        public ServerType ServerType => ServerType.ExmoSpot;

        public DateTime ServerTime { get; set; }

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public string _publicKey;

        public string _secretKey;

        public string _baseUrl = "http://api.exmo.me/v1.1";

        public List<IServerParameter> ServerParameters { get; set; }

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            _rateGate.WaitToProceed();

            try
            {
                RestRequest requestRest = new RestRequest("/pair_settings", Method.POST);
                RestClient client = new RestClient(_baseUrl);

                //if (_myProxy != null)
                //{
                //    client.Proxy = _myProxy;
                //}

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MarketInfo symbolsResponse = JsonConvert.DeserializeAnonymousType(response.Content, new MarketInfo());

                    if (symbolsResponse == null)
                    {
                        return;
                    }

                    List<Security> securities = new List<Security>();

                    foreach (var symbol in symbolsResponse)
                    {
                        Security newSecurity = new Security();

                        newSecurity.Name = symbol.Key;
                        newSecurity.NameFull = symbol.Key;
                        newSecurity.NameClass = symbol.Key.Split('_')[1];

                        SymbolItem item = symbol.Value;

                        newSecurity.NameId = symbol.Key + item.min_price;
                        newSecurity.State = SecurityStateType.Activ;
                        newSecurity.Decimals = Convert.ToInt32(item.price_precision);
                        newSecurity.DecimalsVolume = item.min_quantity.DecimalsCount();
                        newSecurity.PriceStep = Convert.ToInt32(item.price_precision).GetValueByDecimals();
                        newSecurity.PriceStepCost = newSecurity.PriceStep;
                        newSecurity.Lot = 1;
                        newSecurity.SecurityType = SecurityType.CurrencyPair;
                        newSecurity.Exchange = ServerType.ExmoSpot.ToString();
                        newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                        newSecurity.MinTradeAmount = item.min_quantity.ToDecimal();
                        newSecurity.VolumeStep = newSecurity.DecimalsVolume.GetValueByDecimals();

                        securities.Add(newSecurity);
                    }

                    SecurityEvent(securities);
                }
                else
                {
                    SendLogMessage($"Securities request error. {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private string PortfolioName = "ExmoSpotPortfolio";

        private bool startPortfolio = true;

        public void GetPortfolios()
        {
            _rateGate.WaitToProceed();

            try
            {
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string queryParam = $"nonce={timeStamp}";

                string sign = Sign(queryParam);

                RestRequest requestRest = new RestRequest("/user_info", Method.POST);
                RestClient client = new RestClient(_baseUrl);

                //if (_myProxy != null)
                //{
                //    client.Proxy = _myProxy;
                //}

                requestRest.AddHeader("Key", _publicKey);
                requestRest.AddHeader("Sign", sign);
                requestRest.AddParameter("application/x-www-form-urlencoded", queryParam, ParameterType.RequestBody);

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BalanceResponse balanceResponse = JsonConvert.DeserializeAnonymousType(response.Content, new BalanceResponse());

                    Portfolio portfolio = new Portfolio();
                    portfolio.Number = this.PortfolioName;
                    portfolio.ValueBegin = 1;
                    portfolio.ValueCurrent = 1;


                    foreach (var balance in balanceResponse.balances)
                    {
                        PositionOnBoard pos = new PositionOnBoard();

                        pos.PortfolioName = this.PortfolioName;
                        pos.SecurityNameCode = balance.Key;
                        pos.ValueCurrent = Math.Round(balance.Value.ToDecimal(), 5);

                        if (startPortfolio)
                        {
                            pos.ValueBegin = Math.Round(balance.Value.ToDecimal(), 5);
                        }

                        portfolio.SetNewPosition(pos);
                    }

                    if (PortfolioEvent != null)
                    {
                        PortfolioEvent(new List<Portfolio> { portfolio });
                    }

                    startPortfolio = false;
                }
                else
                {
                    SendLogMessage($"Portfolio request error. {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Portfolio error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.UtcNow;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {

            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!CheckTf(tfTotalMinutes))
            {
                return null;
            }

            List<Candle> candles = new List<Candle>();

            int maximumLimitForLoadingCandles = 3000;

            TimeSpan span = endTime - startTime;

            if (maximumLimitForLoadingCandles > span.TotalMinutes / tfTotalMinutes)
            {
                maximumLimitForLoadingCandles = (int)Math.Round(span.TotalMinutes / tfTotalMinutes, MidpointRounding.AwayFromZero);
            }

            DateTime startTimeData = startTime;
            DateTime endTimeData = startTimeData.AddMinutes(tfTotalMinutes * maximumLimitForLoadingCandles);

            do
            {
                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);
                List<Candle> newCandles = GetHistoryCandle(security, interval, startTimeData, endTimeData);

                if (newCandles == null || newCandles.Count == 0)
                {
                    break;
                }

                Candle last = newCandles[newCandles.Count - 1];

                if (candles.Count > 0)
                {
                    if (candles[candles.Count - 1].TimeStart == newCandles[0].TimeStart)
                    {
                        newCandles.RemoveAt(0);
                    }
                }

                if (last.TimeStart >= endTime)
                {
                    for (int i = 0; i < newCandles.Count; i++)
                    {
                        if (newCandles[i].TimeStart <= endTime)
                        {
                            candles.Add(newCandles[i]);
                        }
                    }
                    break;
                }

                candles.AddRange(newCandles);

                startTimeData = endTimeData;
                endTimeData = startTimeData.AddMinutes(tfTotalMinutes * maximumLimitForLoadingCandles);

                if (startTimeData >= endTime)
                {
                    break;
                }

                if (endTimeData > endTime)
                {
                    endTimeData = endTime;
                }

                span = endTimeData - startTimeData;

                if (maximumLimitForLoadingCandles > span.TotalMinutes / tfTotalMinutes)
                {
                    maximumLimitForLoadingCandles = (int)Math.Round(span.TotalMinutes / tfTotalMinutes, MidpointRounding.AwayFromZero);
                }

            } while (true);


            return candles;
        }

        private string GetInterval(TimeSpan timeFrame)
        {
            if (timeFrame.Minutes != 0)
            {
                return $"{timeFrame.Minutes}";
            }
            else if (timeFrame.Hours != 0)
            {
                return $"{timeFrame.TotalMinutes}";
            }
            else
            {
                return $"D";
            }
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime
                || startTime >= DateTime.UtcNow
                || actualTime > endTime
                || actualTime > DateTime.UtcNow)
            {
                return false;
            }
            return true;
        }

        private bool CheckTf(int timeFrameMinutes)
        {
            if (timeFrameMinutes == 1
                || timeFrameMinutes == 5
                || timeFrameMinutes == 15
                || timeFrameMinutes == 30
                || timeFrameMinutes == 60
                || timeFrameMinutes == 120
                || timeFrameMinutes == 240
                || timeFrameMinutes == 1440)
            {
                return true;
            }
            return false;
        }

        private List<Candle> GetHistoryCandle(Security security, string interval, DateTime startTime, DateTime endTime)
        {
            string endPoint = "/candles_history?symbol=" + security.Name;

            endPoint += "&resolution=" + interval;
            endPoint += "&from=" + TimeManager.GetTimeStampSecondsToDateTime(startTime);
            endPoint += "&to=" + TimeManager.GetTimeStampSecondsToDateTime(endTime);

            _rateGate.WaitToProceed();

            try
            {
                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    CandlesResponse responseCandles = JsonConvert.DeserializeAnonymousType(response.Content, new CandlesResponse());

                    if (responseCandles == null
                        || responseCandles.candles == null
                        || responseCandles.s == "error")
                    {
                        SendLogMessage($"Candles request error: {responseCandles.s} - {responseCandles.errmsg}", LogMessageType.Error);
                        return null;
                    }

                    List<Candle> candles = new List<Candle>();

                    for (int i = 0; i < responseCandles.candles.Count; i++)
                    {
                        CandleItem item = responseCandles.candles[i];

                        if (CheckCandlesToZeroData(item))
                        {
                            continue;
                        }

                        Candle candle = new Candle();

                        candle.State = CandleState.Finished;
                        candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.t));
                        candle.Volume = item.v.ToDecimal();
                        candle.Close = item.c.ToDecimal();
                        candle.High = item.h.ToDecimal();
                        candle.Low = item.l.ToDecimal();
                        candle.Open = item.o.ToDecimal();

                        candles.Add(candle);
                    }

                    return candles;

                }
                else
                {
                    SendLogMessage("Candles request error to url='" + endPoint + "'. Status: " + response.StatusCode, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Candles request error:" + exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private bool CheckCandlesToZeroData(CandleItem item)
        {
            if (item.o.ToDecimal() == 0 ||
                item.c.ToDecimal() == 0 ||
                item.h.ToDecimal() == 0 ||
                item.l.ToDecimal() == 0)
            {
                return true;
            }

            return false;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private string _webSocketUrlPublic = "wss://ws-api.exmo.me:443/v1/public";

        private string _webSocketUrlPrivate = "wss://ws-api.exmo.me:443/v1/private";

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

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
                webSocketPublicNew.ConnectAsync();

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

                _webSocketPrivate = new WebSocket(_webSocketUrlPrivate);

                //if (_myProxy != null)
                //{
                //    _webSocketPrivate.SetProxy(_myProxy);
                //}

                _webSocketPrivate.EmitOnPing = true;
                _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                _webSocketPrivate.OnError += _webSocketPrivate_OnError;
                _webSocketPrivate.ConnectAsync();
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

        private void CreateAuthMessageWebSocekt()
        {
            string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string sign = GenerateSignature(timeStamp);

            _webSocketPrivate.SendAsync($"{{\"method\":\"login\",\"id\": 1,\"api_key\":\"{_publicKey}\",\"sign\":\"{sign}\",\"nonce\":{timeStamp}}}");
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublicNew_OnClose(object sender, CloseEventArgs e)
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

        private void WebSocketPublicNew_OnError(object sender, OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs e)
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

        private void WebSocketPublicNew_OnMessage(object sender, MessageEventArgs e)
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

                if (string.IsNullOrEmpty(e.ToString()))
                {
                    return;
                }

                if (FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                if (e.IsText)
                {
                    if (e.Data.Contains("error"))
                    {
                        SendLogMessage(e.Data, LogMessageType.Error);
                        return;
                    }

                    FIFOListWebSocketPublicMessage.Enqueue(e.Data);
                }
            }
            catch (Exception error)
            {
                SendLogMessage("Trade socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("ExmoSpot WebSocket Public connection open", LogMessageType.System);
                    CheckSocketsActivate();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnError(object sender, OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs e)
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

        private void _webSocketPrivate_OnMessage(object sender, MessageEventArgs e)
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

                if (string.IsNullOrEmpty(e.ToString()))
                {
                    return;
                }

                if (FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                if (e.IsText)
                {
                    if (e.Data.Contains("error"))
                    {
                        SendLogMessage(e.Data, LogMessageType.Error);
                        return;
                    }

                    if (e.Data.Contains("logged_in"))
                    {
                        SubscribePrivate();
                        return;
                    }

                    FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnClose(object sender, CloseEventArgs e)
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

        private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
        {
            try
            {
                CreateAuthMessageWebSocekt();
                SendLogMessage("ExmoSpot WebSocket Private connection open", LogMessageType.System);
                CheckSocketsActivate();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket Security subscribe

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

                WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 30 == 0)
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
                    webSocketPublic.SendAsync($"{{\"id\":33,\"method\":\"subscribe\",\"topics\":[\"spot/trades:{security.Name}\",\"spot/order_book_snapshots:{security.Name}\"]}}");
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void SubscribePrivate()
        {
            try
            {
                _webSocketPrivate.SendAsync($"{{ \"id\":22,\"method\":\"subscribe\",\"topics\":[\"spot/orders\",\"spot/user_trades\",\"spot/wallet\"]}}");
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
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

                                        webSocketPublic.SendAsync($"{{\"id\":55,\"method\":\"unsubscribe\",\"topics\":[\"spot/trades:{securityName}\",\"spot/order_book_snapshots:{securityName}\"]}}");
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
                    _webSocketPrivate.SendAsync($"{{ \"id\":22,\"method\":\"unsubscribe\",\"topics\":[\"spot/orders\",\"spot/user_trades\",\"spot/wallet\"]}}");
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

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private void MessageReaderPublic()
        {
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

                    if (message.Contains("info")
                        || message.Contains("subscribed"))
                    {
                        continue;
                    }

                    if (message.Contains("spot/trades"))
                    {
                        UpdateTrade(message);
                        continue;
                    }
                    else if (message.Contains("spot/order_book"))
                    {
                        UpdateMarketDepth(message);
                        continue;
                    }
                    else
                    {
                        SendLogMessage("Unknown message: " + message, LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(2000);
                }
            }
        }

        private void MessageReaderPrivate()
        {
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

                    if (message.Contains("info")
                        || message.Contains("subscribed"))
                    {
                        continue;
                    }

                    if (message.Contains("spot/wallet"))
                    {
                        UpdatePortfolio(message);
                        continue;
                    }
                    else if (message.Contains("spot/orders"))
                    {
                        ResponseWebSocketMessage<object> responseMessage = JsonConvert.DeserializeObject<ResponseWebSocketMessage<object>>(message);
                        UpdateOrder(message);
                        continue;
                    }
                    else if (message.Contains("spot/user_trades"))
                    {
                        UpdateMyTrade(message);
                        continue;
                    }
                    else
                    {
                        SendLogMessage("Unknown message: " + message, LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(2000);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            try
            {
                ResponseWebSocketMessage<List<WebSocketTrade>> responseTrade = JsonConvert.DeserializeObject<ResponseWebSocketMessage<List<WebSocketTrade>>>(message);

                if (responseTrade == null
                    || responseTrade.data == null
                    || responseTrade.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < responseTrade.data.Count; i++)
                {
                    WebSocketTrade item = responseTrade.data[i];

                    Trade trade = new Trade();
                    trade.SecurityNameCode = responseTrade.topic.Split(':')[1];
                    trade.Price = item.price.ToDecimal();
                    trade.Time = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(item.date)).UtcDateTime;
                    trade.Id = item.trade_id;

                    if (item.type == "buy")
                    {
                        trade.Side = Side.Buy;
                    }
                    else
                    {
                        trade.Side = Side.Sell;
                    }

                    trade.Volume = item.quantity.ToDecimal();

                    if (NewTradesEvent != null)
                    {
                        NewTradesEvent(trade);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMarketDepth(string message)
        {
            try
            {
                ResponseWebSocketMessage<OrderBookData> responseDepth = JsonConvert.DeserializeObject<ResponseWebSocketMessage<OrderBookData>>(message);

                if (responseDepth.data == null)
                {
                    return;
                }

                if (responseDepth.data.bid.Count == 0 ||
                    responseDepth.data.ask.Count == 0)
                {
                    return;
                }

                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = responseDepth.topic.Split(':')[1];
                depth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.ts));

                for (int i = 0; i < responseDepth.data.bid.Count; i++)
                {
                    MarketDepthLevel newBid = new MarketDepthLevel();
                    newBid.Price = responseDepth.data.bid[i][0].ToDouble();
                    newBid.Bid = responseDepth.data.bid[i][1].ToDouble();
                    depth.Bids.Add(newBid);
                }

                for (int i = 0; i < responseDepth.data.ask.Count; i++)
                {
                    MarketDepthLevel newAsk = new MarketDepthLevel();
                    newAsk.Price = responseDepth.data.ask[i][0].ToDouble();
                    newAsk.Ask = responseDepth.data.ask[i][1].ToDouble();
                    depth.Asks.Add(newAsk);
                }

                if (_lastMdTime != DateTime.MinValue &&
                    _lastMdTime >= depth.Time)
                {
                    depth.Time = _lastMdTime.AddTicks(1);
                }

                _lastMdTime = depth.Time;

                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(depth);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        private void UpdateMyTrade(string message)
        {
            try
            {
                ResponseWebSocketMessage<MyTradeResponse> response = JsonConvert.DeserializeObject<ResponseWebSocketMessage<MyTradeResponse>>(message);

                if (response == null
                    || response.data == null)
                {
                    return;
                }

                MyTradeResponse item = response.data;

                MyTrade myTrade = new MyTrade();

                myTrade.Time = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(item.date)).UtcDateTime; //TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));
                myTrade.NumberOrderParent = item.order_id;
                myTrade.NumberTrade = item.trade_id;
                myTrade.Price = item.price.ToDecimal();
                myTrade.SecurityNameCode = item.pair;

                if (item.type == "buy")
                {
                    myTrade.Side = Side.Buy;
                }
                else
                {
                    myTrade.Side = Side.Sell;
                }

                string commissionSecName = item.commission_currency;

                if (myTrade.SecurityNameCode.StartsWith(commissionSecName))
                {
                    myTrade.Volume = item.quantity.ToDecimal() - item.commission_amount.ToDecimal();
                }
                else
                {
                    myTrade.Volume = item.quantity.ToDecimal();
                }

                MyTradeEvent(myTrade);
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
                ResponseWebSocketMessage<object> response = JsonConvert.DeserializeObject<ResponseWebSocketMessage<object>>(message);

                if (response.@event == "snapshot")
                {
                    return;
                }

                ResponseWebSocketMessage<OrderUpdate> responseOrderUpdate = JsonConvert.DeserializeObject<ResponseWebSocketMessage<OrderUpdate>>(message);

                if (responseOrderUpdate == null
                    || responseOrderUpdate.data == null)
                {
                    return;
                }

                OrderUpdate item = responseOrderUpdate.data;

                Order newOrder = new Order();
                newOrder.SecurityNameCode = item.pair;
                newOrder.TimeCallBack = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(item.created)).UtcDateTime;
                newOrder.TimeCreate = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(item.created)).UtcDateTime;

                try
                {
                    newOrder.NumberUser = Convert.ToInt32(item.client_id);
                }
                catch
                {
                    // ignore
                }

                newOrder.NumberMarket = item.order_id;

                if (item.type.Contains("buy"))
                {
                    newOrder.Side = Side.Buy;
                }
                else
                {
                    newOrder.Side = Side.Sell;
                }

                newOrder.Volume = item.original_quantity.ToDecimal();
                newOrder.VolumeExecute = item.quantity.ToDecimal() - item.original_quantity.ToDecimal();
                newOrder.Price = item.price.ToDecimal();

                if (item.type.Contains("market"))
                {
                    newOrder.TypeOrder = OrderPriceType.Market;
                }
                else
                {
                    newOrder.TypeOrder = OrderPriceType.Limit;
                }

                newOrder.ServerType = ServerType.ExmoSpot;
                newOrder.PortfolioNumber = this.PortfolioName;

                if (item.status == "open")
                {
                    newOrder.State = OrderStateType.Active;
                }
                else if (item.status == "executing")
                {
                    newOrder.State = OrderStateType.Partial;
                }
                else if (item.status == "cancelled"
                    || item.status == "executing")
                {
                    if (newOrder.VolumeExecute < 0)
                    {
                        newOrder.State = OrderStateType.Done;
                    }
                    else
                    {
                        newOrder.State = OrderStateType.Cancel;
                    }
                }

                MyOrderEvent(newOrder);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(string message)
        {
            try
            {
                ResponseWebSocketMessage<object> response = JsonConvert.DeserializeObject<ResponseWebSocketMessage<object>>(message);

                if (response.@event == "snapshot")
                {
                    return;
                }

                ResponseWebSocketMessage<WalletData> responseWallet = JsonConvert.DeserializeObject<ResponseWebSocketMessage<WalletData>>(message);

                Portfolio portfolio = new Portfolio();
                portfolio.Number = this.PortfolioName;
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = this.PortfolioName;
                pos.SecurityNameCode = responseWallet.data.currency;
                pos.ValueCurrent = Math.Round(responseWallet.data.balance.ToDecimal(), 5);

                if (responseWallet.data.currency == "USDT")
                {
                    pos.ValueBlocked = Math.Round(responseWallet.data.reserved.ToDecimal(), 5);
                }

                portfolio.SetNewPosition(pos);

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(new List<Portfolio> { portfolio });
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion

        #region 11 Trade

        public void SendOrder(Order order)
        {
            _rateGate.WaitToProceed();

            try
            {
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                string queryParam = $"pair={order.SecurityNameCode}&";
                queryParam += $"quantity={order.Volume.ToString().Replace(",", ".")}&";

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    queryParam += $"price={order.Price.ToString().Replace(",", ".")}&";

                    if (order.Side == Side.Buy)
                    {
                        queryParam += $"type=buy&";
                    }
                    else
                    {
                        queryParam += $"type=sell&";
                    }
                }
                else if (order.TypeOrder == OrderPriceType.Market)
                {
                    queryParam += $"price=0&";

                    if (order.Side == Side.Buy)
                    {
                        queryParam += $"type=market_buy&";
                    }
                    else
                    {
                        queryParam += $"type=market_sell&";
                    }
                }

                queryParam += $"client_id={order.NumberUser}&";
                queryParam += $"nonce={timeStamp}";

                string sign = Sign(queryParam);

                RestRequest requestRest = new RestRequest("/order_create", Method.POST);
                RestClient client = new RestClient(_baseUrl);

                //if (_myProxy != null)
                //{
                //    client.Proxy = _myProxy;
                //}

                requestRest.AddHeader("Key", _publicKey);
                requestRest.AddHeader("Sign", sign);
                requestRest.AddParameter("application/x-www-form-urlencoded", queryParam, ParameterType.RequestBody);

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    SendOrderResponse orderResponse = JsonConvert.DeserializeAnonymousType(response.Content, new SendOrderResponse());

                    if (orderResponse.result == "true")
                    {
                        //
                    }
                    else
                    {
                        SendLogMessage($"Order Fail. {orderResponse.error}", LogMessageType.Error);
                        CreateOrderFail(order);
                    }
                }
                else
                {
                    SendLogMessage($"Order Fail. Status:  {response.StatusCode}, {response.Content}", LogMessageType.Error);
                    CreateOrderFail(order);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public bool CancelOrder(Order order)
        {
            _rateGate.WaitToProceed();

            try
            {
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                string queryParam = $"order_id={order.NumberMarket}&";
                queryParam += $"nonce={timeStamp}";

                string sign = Sign(queryParam);

                RestRequest requestRest = new RestRequest("/order_cancel", Method.POST);
                RestClient client = new RestClient(_baseUrl);

                //if (_myProxy != null)
                //{
                //    client.Proxy = _myProxy;
                //}

                requestRest.AddHeader("Key", _publicKey);
                requestRest.AddHeader("Sign", sign);
                requestRest.AddParameter("application/x-www-form-urlencoded", queryParam, ParameterType.RequestBody);

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    SendOrderResponse orderResponse = JsonConvert.DeserializeAnonymousType(response.Content, new SendOrderResponse());

                    if (orderResponse.result == "true")
                    {
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Order cancellation error: {orderResponse.error}", LogMessageType.Error);
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    OrderStateType state = GetOrderStatus(order);

                    if (state == OrderStateType.None)
                    {
                        SendLogMessage($"Order cancellation error: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }

            return false;
        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {

        }

        public void GetAllActivOrders()
        {
            List<Order> ordersOnBoard = GetAllOpenOrders();

            if (ordersOnBoard == null)
            {
                return;
            }

            for (int i = 0; i < ordersOnBoard.Count; i++)
            {
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(ordersOnBoard[i]);
                }
            }
        }

        private List<Order> GetAllOpenOrders()
        {
            _rateGate.WaitToProceed();

            try
            {
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string queryParam = $"nonce={timeStamp}";
                string sign = Sign(queryParam);

                RestRequest requestRest = new RestRequest("/user_open_orders", Method.POST);
                RestClient client = new RestClient(_baseUrl);

                //if (_myProxy != null)
                //{
                //    client.Proxy = _myProxy;
                //}

                requestRest.AddHeader("Key", _publicKey);
                requestRest.AddHeader("Sign", sign);
                requestRest.AddParameter("application/x-www-form-urlencoded", queryParam, ParameterType.RequestBody);

                IRestResponse response = client.Execute(requestRest);

                List<Order> orders = new List<Order>();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    GetOrderResponse responseOrder = JsonConvert.DeserializeAnonymousType(response.Content, new GetOrderResponse());

                    if (responseOrder == null)
                    {
                        return null;
                    }

                    foreach (var order in responseOrder)
                    {
                        List<GetOrderItem> itemCount = order.Value;

                        for (int i = 0; i < itemCount.Count; i++)
                        {
                            GetOrderItem item = itemCount[i];

                            Order newOrder = new Order();
                            newOrder.SecurityNameCode = item.pair;
                            newOrder.TimeCallBack = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(item.created)).UtcDateTime;
                            newOrder.TimeCreate = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(item.created)).UtcDateTime;

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(item.client_id);
                            }
                            catch
                            {
                                // ignore
                            }

                            newOrder.NumberMarket = item.order_id;

                            if (item.type.Contains("buy"))
                            {
                                newOrder.Side = Side.Buy;
                            }
                            else
                            {
                                newOrder.Side = Side.Sell;
                            }

                            newOrder.Volume = item.quantity.ToDecimal();
                            newOrder.Price = item.price.ToDecimal();

                            if (item.type.Contains("market"))
                            {
                                newOrder.TypeOrder = OrderPriceType.Market;
                            }
                            else
                            {
                                newOrder.TypeOrder = OrderPriceType.Limit;
                            }

                            newOrder.ServerType = ServerType.ExmoSpot;
                            newOrder.PortfolioNumber = this.PortfolioName;
                            newOrder.State = OrderStateType.Active;

                            orders.Add(newOrder);
                        }
                    }
                }
                else
                {
                    SendLogMessage($"Get open orders error: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }

                return orders;
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            List<Order> orderFromExchange = GetAllOpenOrders();

            if (orderFromExchange == null
                || orderFromExchange.Count == 0)
            {
                orderFromExchange = GetAllCancelledOrders();
            }

            if (orderFromExchange == null
               || orderFromExchange.Count == 0)
            {
                return OrderStateType.None;
            }

            Order orderOnMarket = null;

            for (int i = 0; i < orderFromExchange.Count; i++)
            {
                Order curOder = orderFromExchange[i];

                if (order.NumberUser != 0
                    && curOder.NumberUser != 0
                    && curOder.NumberUser == order.NumberUser)
                {
                    orderOnMarket = curOder;
                    break;
                }

                if (string.IsNullOrEmpty(order.NumberMarket) == false
                    && order.NumberMarket == curOder.NumberMarket)
                {
                    orderOnMarket = curOder;
                    break;
                }
            }

            if (orderOnMarket == null)
            {
                orderOnMarket = GetOrderFromExchange(order.SecurityNameCode, order.NumberUser.ToString());

            }

            if (orderOnMarket == null)
            {
                return OrderStateType.None;
            }

            if (orderOnMarket != null &&
                MyOrderEvent != null)
            {
                MyOrderEvent(orderOnMarket);
            }

            return orderOnMarket.State;
        }

        private List<Order> GetAllCancelledOrders()
        {
            _rateGate.WaitToProceed();

            try
            {
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string queryParam = $"nonce={timeStamp}";
                string sign = Sign(queryParam);

                RestRequest requestRest = new RestRequest("/user_cancelled_orders", Method.POST);
                RestClient client = new RestClient(_baseUrl);

                //if (_myProxy != null)
                //{
                //    client.Proxy = _myProxy;
                //}

                requestRest.AddHeader("Key", _publicKey);
                requestRest.AddHeader("Sign", sign);
                requestRest.AddParameter("application/x-www-form-urlencoded", queryParam, ParameterType.RequestBody);

                IRestResponse response = client.Execute(requestRest);

                List<Order> orders = new List<Order>();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    List<GetOrderItem> responseOrder = JsonConvert.DeserializeAnonymousType(response.Content, new List<GetOrderItem>());

                    if (responseOrder == null)
                    {
                        return null;
                    }

                    for (int i = 0; i < responseOrder.Count; i++)
                    {
                        GetOrderItem item = responseOrder[i];

                        Order newOrder = new Order();
                        newOrder.SecurityNameCode = item.pair;
                        newOrder.TimeCallBack = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(item.created)).UtcDateTime;
                        newOrder.TimeCreate = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(item.created)).UtcDateTime;

                        try
                        {
                            newOrder.NumberUser = Convert.ToInt32(item.client_id);
                        }
                        catch
                        {
                            // ignore
                        }

                        newOrder.NumberMarket = item.order_id;

                        if (item.type.Contains("buy"))
                        {
                            newOrder.Side = Side.Buy;
                        }
                        else
                        {
                            newOrder.Side = Side.Sell;
                        }

                        newOrder.Volume = item.quantity.ToDecimal();
                        newOrder.Price = item.price.ToDecimal();

                        if (item.type.Contains("market"))
                        {
                            newOrder.TypeOrder = OrderPriceType.Market;
                        }
                        else
                        {
                            newOrder.TypeOrder = OrderPriceType.Limit;
                        }

                        newOrder.ServerType = ServerType.ExmoSpot;
                        newOrder.PortfolioNumber = this.PortfolioName;
                        newOrder.State = OrderStateType.Cancel;

                        orders.Add(newOrder);
                    }
                }
                else
                {
                    SendLogMessage($"Get cancelled orders error: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }

                return orders;
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private Order GetOrderFromExchange(string nameSecurity, string userOrderId)
        {
            _rateGate.WaitToProceed();

            try
            {
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string queryParam = $"pair={nameSecurity}&";
                queryParam += $"nonce={timeStamp}";

                string sign = Sign(queryParam);

                RestRequest requestRest = new RestRequest("/user_trades", Method.POST);
                RestClient client = new RestClient(_baseUrl);

                //if (_myProxy != null)
                //{
                //    client.Proxy = _myProxy;
                //}

                requestRest.AddHeader("Key", _publicKey);
                requestRest.AddHeader("Sign", sign);
                requestRest.AddParameter("application/x-www-form-urlencoded", queryParam, ParameterType.RequestBody);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    GetOrderTrade responseOrder = JsonConvert.DeserializeAnonymousType(response.Content, new GetOrderTrade());

                    if (responseOrder == null)
                    {
                        return null;
                    }

                    foreach (var order in responseOrder)
                    {
                        for (int i = 0; i < order.Value.Count; i++)
                        {
                            OrderTradeItem item = order.Value[i];

                            if (item.client_id == null
                                || userOrderId != item.client_id)
                            {
                                continue;
                            }

                            Order newOrder = new Order();
                            newOrder.SecurityNameCode = item.pair;
                            newOrder.TimeCallBack = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(item.date)).UtcDateTime;
                            newOrder.TimeCreate = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(item.date)).UtcDateTime;

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(item.client_id);
                            }
                            catch
                            {
                                // ignore
                            }

                            newOrder.NumberMarket = item.order_id;

                            if (item.type.Contains("buy"))
                            {
                                newOrder.Side = Side.Buy;
                            }
                            else
                            {
                                newOrder.Side = Side.Sell;
                            }

                            newOrder.Volume = item.quantity.ToDecimal();
                            newOrder.Price = item.price.ToDecimal();

                            if (item.exec_type == "taker")
                            {
                                newOrder.TypeOrder = OrderPriceType.Limit;
                            }
                            else
                            {
                                newOrder.TypeOrder = OrderPriceType.Market;
                            }

                            newOrder.ServerType = ServerType.ExmoSpot;
                            newOrder.PortfolioNumber = this.PortfolioName;

                            newOrder.State = OrderStateType.Done;

                            if (newOrder.State == OrderStateType.Done
                                || newOrder.State == OrderStateType.Partial)
                            {
                                MyTrade myTrade = new MyTrade();

                                myTrade.Time = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(item.date)).UtcDateTime;
                                myTrade.NumberOrderParent = item.order_id;
                                myTrade.NumberTrade = item.trade_id;
                                myTrade.Price = item.price.ToDecimal();
                                myTrade.SecurityNameCode = item.pair;

                                if (item.type == "buy")
                                {
                                    myTrade.Side = Side.Buy;
                                }
                                else
                                {
                                    myTrade.Side = Side.Sell;
                                }

                                string commissionSecName = item.commission_currency;

                                if (myTrade.SecurityNameCode.StartsWith(commissionSecName))
                                {
                                    myTrade.Volume = item.quantity.ToDecimal() - item.commission_amount.ToDecimal();
                                }
                                else
                                {
                                    myTrade.Volume = item.quantity.ToDecimal();
                                }

                                MyTradeEvent(myTrade);
                            }

                            return newOrder;
                        }
                    }
                }
                else
                {
                    SendLogMessage($"Get cancelled orders error: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }
                return null;
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        #endregion

        #region 12 Query

        private RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public string GenerateSignature(string nonce)
        {
            string message = _publicKey + nonce;

            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            byte[] secretBytes = Encoding.UTF8.GetBytes(_secretKey);

            using (var hmac = new HMACSHA512(secretBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(messageBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

        public string Sign(string message)
        {
            using (HMACSHA512 hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_secretKey)))
            {
                byte[] b = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return ByteToString(b);
            }
        }

        public static string ByteToString(byte[] buff)
        {
            string sbinary = "";

            for (int i = 0; i < buff.Length; i++)
            {
                sbinary += buff[i].ToString("X2"); // hex format
            }
            return (sbinary).ToLowerInvariant();
        }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}
