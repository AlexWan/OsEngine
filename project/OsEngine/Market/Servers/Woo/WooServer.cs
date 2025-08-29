﻿/*
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

        #endregion

        #region 3 Securities

        private List<Security> _securities;

        public void GetSecurities()
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

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
                        for (int i = 0; i < response.data.rows.Count; i++)
                        {
                            RowSymbols item = response.data.rows[i];

                            if (item.status == "TRADING")
                            {
                                Security newSecurity = new Security();

                                newSecurity.Exchange = ServerType.Woo.ToString();
                                newSecurity.Name = item.symbol;
                                newSecurity.NameFull = item.symbol;
                                newSecurity.NameClass = item.symbol.StartsWith("SPOT") ? "Spot_" + item.quoteAsset : "Futures_" + item.quoteAsset;
                                newSecurity.NameId = item.symbol;
                                newSecurity.SecurityType = item.symbol.StartsWith("SPOT") ? SecurityType.CurrencyPair : SecurityType.Futures;
                                newSecurity.DecimalsVolume = item.baseTick.DecimalsCount();
                                newSecurity.Lot = 1;
                                newSecurity.PriceStep = item.quoteTick.ToDecimal();
                                newSecurity.Decimals = item.quoteTick.DecimalsCount();
                                newSecurity.PriceStepCost = newSecurity.PriceStep;
                                newSecurity.State = SecurityStateType.Activ;

                                if (item.symbol.StartsWith("SPOT"))
                                {
                                    newSecurity.MinTradeAmountType = MinTradeAmountType.C_Currency;
                                    newSecurity.MinTradeAmount = item.minNotional.ToDecimal();
                                }
                                else
                                {
                                    newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                                    newSecurity.MinTradeAmount = item.baseMin.ToDecimal();
                                }

                                newSecurity.VolumeStep = item.baseTick.ToDecimal();

                                _securities.Add(newSecurity);
                            }
                        }

                        SecurityEvent(_securities);
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

                IRestResponse responseMessage = CreatePrivateQuery(requestPath, Method.GET);

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

                IRestResponse responseMessage = CreatePrivateQuery(requestPath, Method.GET);

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

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.UtcNow;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, endTime);
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
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

            List<Candle> allCandles = new List<Candle>();

            DateTime startTimeData = startTime;

            do
            {
                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTimeData);

                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

                List<Candle> candles = RequestCandleHistory(security.Name, interval, from);

                if (candles == null
                    || candles.Count == 0)
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

                startTimeData = startTimeData.AddMinutes(tfTotalMinutes * 100);

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
                startTime >= DateTime.UtcNow ||
                actualTime > endTime ||
                actualTime > DateTime.UtcNow)
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

        private string GetInterval(TimeSpan timeFrame)
        {
            if (timeFrame.Minutes != 0)
            {
                return $"{timeFrame.Minutes}m";
            }
            else if (timeFrame.Hours != 0)
            {
                return $"{timeFrame.Hours}h";
            }
            else
            {
                return $"{timeFrame.Days}d";
            }
        }

        private readonly RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private List<Candle> RequestCandleHistory(string security, string resolution, long fromTimeStamp)
        {
            _rgCandleData.WaitToProceed();

            try
            {
                string queryParam = $"after={fromTimeStamp}&";
                queryParam += $"symbol={security}&";
                queryParam += $"limit=100&";
                queryParam += $"type={resolution}";

                string url = $"{_baseUrl}/v3/public/klineHistory?" + queryParam;
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseCandles> response = JsonConvert.DeserializeObject<ResponseMessageRest<ResponseCandles>>(responseMessage.Content);

                    if (response.success == "true")
                    {
                        List<Candle> candles = new List<Candle>();

                        for (int i = 0; i < response.data.rows.Count; i++)
                        {
                            RowCandles item = response.data.rows[i];

                            if (CheckCandlesToZeroData(item))
                            {
                                continue;
                            }

                            Candle candle = new Candle();

                            candle.State = CandleState.Finished;
                            candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.startTimestamp));
                            candle.Volume = item.volume.ToDecimal();
                            candle.Close = response.data.rows[i].close.ToDecimal();
                            candle.High = item.high.ToDecimal();
                            candle.Low = item.low.ToDecimal();
                            candle.Open = item.open.ToDecimal();

                            candles.Add(candle);
                        }

                        candles.Reverse();
                        return candles;
                    }
                    else
                    {
                        SendLogMessage($"Candles error. {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Candles request error: {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private bool CheckCandlesToZeroData(RowCandles item)
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

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
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

                if (newOrder.State == OrderStateType.Done
                    || newOrder.State == OrderStateType.Partial)
                {
                    MyTrade myTrade = new MyTrade();

                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));
                    myTrade.NumberOrderParent = item.oid;
                    myTrade.NumberTrade = item.tid;
                    myTrade.Price = item.epx.ToDecimal();
                    myTrade.SecurityNameCode = item.s;
                    myTrade.Side = item.sd.Equals("BUY") ? Side.Buy : Side.Sell;

                    string commissionSecName = item.tfc;
                    string security = myTrade.SecurityNameCode.Split('_')[1];

                    if (security == commissionSecName)
                    {
                        myTrade.Volume = item.esx.ToDecimal() - item.tf.ToDecimal();

                        int decimalVolum = GetVolumeDecimals(myTrade.SecurityNameCode);

                        if (decimalVolum > 0)
                        {
                            myTrade.Volume = Math.Floor(myTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                        }
                    }
                    else
                    {
                        myTrade.Volume = item.esx.ToDecimal();
                    }

                    MyTradeEvent(myTrade);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private int GetVolumeDecimals(string security)
        {
            for (int i = 0; i < _securities.Count; i++)
            {
                if (security == _securities[i].Name)
                {
                    return _securities[i].DecimalsVolume;
                }
            }

            return 0;
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
                string requestPath = "/v3/trade/order";

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();
                jsonContent.Add("symbol", order.SecurityNameCode);
                jsonContent.Add("side", order.Side.ToString() == "Buy" ? "BUY" : "SELL");
                jsonContent.Add("type", order.TypeOrder.ToString() == "Limit" ? "LIMIT" : "MARKET");
                jsonContent.Add("clientOrderId", order.NumberUser.ToString());
                jsonContent.Add("price", order.Price.ToString().Replace(",", "."));
                jsonContent.Add("quantity", order.Volume.ToString().Replace(",", "."));

                string requestBody = JsonConvert.SerializeObject(jsonContent);

                IRestResponse responseMessage = CreatePrivateQuery(requestPath, Method.POST, requestBody);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseMessageRest<OrderData> response = JsonConvert.DeserializeObject<ResponseMessageRest<OrderData>>(responseMessage.Content);

                    if (response.success == "true")
                    {
                        //Everything is OK
                    }
                    else
                    {
                        SendLogMessage($"Order Fail. {responseMessage.Content}", LogMessageType.Error);
                        CreateOrderFail(order);
                    }
                }
                else
                {
                    SendLogMessage($"Order Fail. Status:  {responseMessage.StatusCode}, {responseMessage.Content}", LogMessageType.Error);
                    CreateOrderFail(order);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
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
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string requestPath = "/v3/trade/order";

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();
                jsonContent.Add("orderId", order.NumberMarket);
                jsonContent.Add("clientOrderId", order.NumberUser.ToString());
                jsonContent.Add("price", newPrice.ToString());

                string requestBody = JsonConvert.SerializeObject(jsonContent);

                IRestResponse responseMessage = CreatePrivateQuery(requestPath, Method.PUT, requestBody);

                if (responseMessage.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage($"ChangeOrderPrice FAIL. {responseMessage.StatusCode}, {responseMessage.Content}", LogMessageType.Error);
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
                string requestPath = "/v3/trade/allOrders";
                requestPath += $"?symbol={security.Name}&";

                IRestResponse responseMessage = CreatePrivateQuery(requestPath, Method.DELETE);

                if (responseMessage.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage($"CancelAllOrdersToSecurity. Http State Code: {responseMessage.StatusCode}, {responseMessage.Content}", LogMessageType.Error);
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

            try
            {
                string requestPath = "/v3/trade/order";
                requestPath += $"?orderId={order.NumberMarket}&";
                requestPath += $"symbol={order.SecurityNameCode}&";
                requestPath += $"clientOrderId={order.NumberUser.ToString()}";

                IRestResponse responseMessage = CreatePrivateQuery(requestPath, Method.DELETE);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseMessageRest<object> response = JsonConvert.DeserializeObject<ResponseMessageRest<object>>(responseMessage.Content);

                    if (response.success == "true")
                    {
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Order cancellation error: {responseMessage.Content}", LogMessageType.Error);
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
                        SendLogMessage($"Order cancellation error: {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);

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
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return false;
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

        private RateGate _rateGateGetAllOpenOrders = new RateGate(1, TimeSpan.FromMilliseconds(110));

        private List<Order> GetAllOpenOrders()
        {
            _rateGateGetAllOpenOrders.WaitToProceed();

            try
            {
                string requestPath = "/v3/trade/orders";
                requestPath += $"?status=NEW";

                IRestResponse responseMessage = CreatePrivateQuery(requestPath, Method.GET);

                List<Order> orders = new List<Order>();

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<OpenOrdersData> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<OpenOrdersData>());

                    if (response.success == "true")
                    {
                        for (int i = 0; i < response.data.rows.Count; i++)
                        {
                            RowOpenOrders item = response.data.rows[i];

                            OrderStateType stateType = GetOrderState(item.status);

                            Order newOrder = new Order();

                            newOrder.SecurityNameCode = item.symbol;
                            newOrder.TimeCallBack = UnixTimeMilliseconds(item.createdTime);

                            if (string.IsNullOrEmpty(item.clientOrderId))
                            {
                                return null;
                            }

                            newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                            newOrder.NumberMarket = item.orderId;
                            newOrder.Side = item.side.Equals("BUY") ? Side.Buy : Side.Sell;
                            newOrder.State = stateType;
                            newOrder.TypeOrder = item.type.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                            newOrder.Volume = item.quantity.ToDecimal();
                            newOrder.VolumeExecute = item.executed.ToDecimal();
                            newOrder.Price = item.price.ToDecimal();
                            newOrder.ServerType = ServerType.Woo;
                            newOrder.PortfolioNumber = "WooPortfolio";

                            orders.Add(newOrder);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Open orders: {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get open orders error: {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);
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
            Order myOrder = GetOrderFromExchange(order.SecurityNameCode, order.NumberUser.ToString(), order.NumberMarket);

            if (myOrder == null)
            {
                return OrderStateType.None;
            }

            MyOrderEvent?.Invoke(myOrder);

            if (myOrder.State == OrderStateType.Done
                || myOrder.State == OrderStateType.Partial)
            {
                GetTradesForOrder(myOrder.SecurityNameCode, myOrder.NumberMarket);
            }

            return myOrder.State;
        }

        private RateGate _rateGateGetOrderFromExchange = new RateGate(1, TimeSpan.FromMilliseconds(210));

        private Order GetOrderFromExchange(string nameSecurity, string userOrderId, string numberMarket)
        {
            _rateGateGetOrderFromExchange.WaitToProceed();

            try
            {
                string requestPath = "/v3/trade/order";
                requestPath += $"?orderId={numberMarket}&";
                requestPath += $"clientOrderId={userOrderId}";

                IRestResponse responseMessage = CreatePrivateQuery(requestPath, Method.GET);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<RowOpenOrders> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<RowOpenOrders>());

                    if (response.success == "true")
                    {
                        RowOpenOrders item = response.data;

                        if (item.clientOrderId != userOrderId)
                        {
                            return null;
                        }

                        OrderStateType stateType = GetOrderState(item.status);

                        Order newOrder = new Order();

                        newOrder.SecurityNameCode = item.symbol;
                        newOrder.TimeCallBack = UnixTimeMilliseconds(item.createdTime);

                        if (string.IsNullOrEmpty(item.clientOrderId))
                        {
                            return null;
                        }

                        newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                        newOrder.NumberMarket = item.orderId;
                        newOrder.Side = item.side.Equals("BUY") ? Side.Buy : Side.Sell;
                        newOrder.State = stateType;
                        newOrder.TypeOrder = item.type.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                        newOrder.Volume = item.quantity.ToDecimal();
                        newOrder.VolumeExecute = item.executed.ToDecimal();
                        newOrder.Price = item.price.ToDecimal();
                        newOrder.ServerType = ServerType.Woo;
                        newOrder.PortfolioNumber = "WooPortfolio";

                        return newOrder;
                    }
                    else
                    {
                        SendLogMessage($"Get order from exchange error: {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get order from exchange error: {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);
                }

                return null;
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private RateGate _rateGateGetMyTrade = new RateGate(1, TimeSpan.FromMilliseconds(210));

        private List<MyTrade> GetTradesForOrder(string nameSecurity, string orderId)
        {
            _rateGateGetMyTrade.WaitToProceed();

            try
            {
                string requestPath = "/v3/trade/transactionHistory";
                requestPath += $"?symbol={nameSecurity}";

                IRestResponse responseMessage = CreatePrivateQuery(requestPath, Method.GET);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<MyTradeData> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<MyTradeData>());

                    if (response.success == "true")
                    {
                        for (int i = 0; i < response.data.rows.Count; i++)
                        {
                            RowMyTrade item = response.data.rows[i];

                            if (item.orderId != orderId)
                            {
                                continue;
                            }

                            MyTrade newTrade = new MyTrade();

                            newTrade.Time = UnixTimeMilliseconds(item.executedTimestamp);
                            newTrade.SecurityNameCode = item.symbol;
                            newTrade.NumberOrderParent = item.orderId;
                            newTrade.Price = item.executedPrice.ToDecimal();
                            newTrade.NumberTrade = item.id;
                            newTrade.Side = item.side.Equals("SELL") ? Side.Sell : Side.Buy;

                            string commissionSecName = item.feeAsset;
                            string security = newTrade.SecurityNameCode.Split('_')[1];

                            if (security == commissionSecName)
                            {
                                newTrade.Volume = item.executedQuantity.ToDecimal() - item.fee.ToDecimal();

                                int decimalVolum = GetVolumeDecimals(newTrade.SecurityNameCode);

                                if (decimalVolum > 0)
                                {
                                    newTrade.Volume = Math.Floor(newTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                                }
                            }
                            else
                            {
                                newTrade.Volume = item.executedQuantity.ToDecimal();
                            }


                            MyTradeEvent(newTrade);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Order trade request error: code {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Order trade error: {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);
                }

                return null;
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 12 Queries

        private IRestResponse CreatePrivateQuery(string requestPath, Method method, string requestBody = null)
        {
            try
            {
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest(requestPath, method);

                string signature = GenerateSignature(timestamp, method.ToString().ToUpper(), requestPath, requestBody);

                request.AddHeader("x-api-key", _apiKey);
                request.AddHeader("x-api-signature", signature);
                request.AddHeader("x-api-timestamp", timestamp.ToString());
                request.AddParameter("application/json", requestBody, ParameterType.RequestBody);

                IRestResponse responseMessage = client.Execute(request);

                return responseMessage;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private RateGate _rateGateListenKey = new RateGate(5, TimeSpan.FromMilliseconds(10000));

        private string CreateListenKey()
        {
            _rateGateListenKey.WaitToProceed();

            try
            {
                string requestPath = "/v3/account/listenKey";

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();
                jsonContent.Add("type", "WEBSOCKET");

                string requestBody = JsonConvert.SerializeObject(jsonContent);

                IRestResponse responseMessage = CreatePrivateQuery(requestPath, Method.POST, requestBody);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseListenKey> responseKey = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<ResponseListenKey>());

                    if (responseKey.success == "true")
                    {
                        return responseKey.data.authKey;
                    }
                    else
                    {
                        SendLogMessage("Listen Key request error. " + responseMessage.Content, LogMessageType.Error);
                        return null;
                    }
                }
                else
                {
                    SendLogMessage("Listen Key error. Code: " + responseMessage.StatusCode + ", " + responseMessage.Content, LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Listen Key error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private string GenerateSignature(long timestamp, string method, string requestPath, string requestBody = null)
        {
            string signString = $"{timestamp}{method}{requestPath}{requestBody}";

            try
            {
                string apiSignature;
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey)))
                {
                    byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signString));
                    apiSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                }

                return apiSignature;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Listen Key error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private DateTime UnixTimeMilliseconds(string timestamp)
        {
            double timestampSeconds = Convert.ToDouble(timestamp);
            long milliseconds = (long)(timestampSeconds * 1000);

            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime dateTime = epoch.AddMilliseconds(milliseconds);

            return dateTime;
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