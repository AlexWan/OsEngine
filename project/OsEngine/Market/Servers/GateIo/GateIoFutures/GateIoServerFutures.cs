﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.GateIo.Futures.Request;
using OsEngine.Market.Servers.GateIo.GateIoFutures.Entities;
using OsEngine.Market.Servers.GateIo.GateIoFutures.Entities.Response;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocketSharp;


namespace OsEngine.Market.Servers.GateIo.GateIoFutures
{
    public class GateIoServerFutures : AServer
    {
        public GateIoServerFutures(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            ServerRealization = new GateIoServerFuturesRealization();
            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterString("User ID", "");
            CreateParameterEnum("Base Wallet", "USDT", new List<string> { "USDT", "BTC" });
            CreateParameterEnum("Hedge Mode", "On", new List<string> { "On", "Off" });
        }
    }

    public sealed class GateIoServerFuturesRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public GateIoServerFuturesRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread thread = new Thread(CheckAliveWebSocket);
            thread.IsBackground = true;
            thread.Name = "CheckAliveWebSocket";
            thread.Start();

            Thread messageReaderThread = new Thread(MessageReader);
            messageReaderThread.IsBackground = true;
            messageReaderThread.Name = "MessageReaderGateIo";
            messageReaderThread.Start();

            Thread thread3 = new Thread(PortfolioRequester);
            thread3.IsBackground = true;
            thread3.Name = "PortfolioRequester";
            thread3.Start();
        }

        public DateTime ServerTime { get; set; }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy)
        {
            _myProxy = proxy;
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _userId = ((ServerParameterString)ServerParameters[2]).Value;

            if (((ServerParameterEnum)ServerParameters[3]).Value == "USDT")
            {
                _wallet = "usdt";
            }
            else
            {
                _wallet = "btc";
            }

            if (((ServerParameterEnum)ServerParameters[4]).Value == "On")
            {
                _hedgeMode = true;
            }
            else
            {
                _hedgeMode = false;
            }

            IRestResponse response = null;

            int tryCounter = 0;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    string requestStr = "/futures/usdt/contracts/BTC_USDT";
                    RestRequest requestRest = new RestRequest(requestStr, Method.GET);

                    RestClient client = new RestClient(HTTP_URL);

                    if (_myProxy != null)
                    {
                        client.Proxy = _myProxy;
                    }

                    response = client.Execute(requestRest);
                    break;
                }
                catch (Exception e)
                {
                    tryCounter++;
                    Thread.Sleep(1000);
                    if (tryCounter >= 3)
                    {
                        SendLogMessage(e.Message, LogMessageType.Connect);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                        return;
                    }
                }
            }

            if (response.StatusCode == HttpStatusCode.OK)
            {
                _timeLastSendPing = DateTime.Now;
                _fifoListWebSocketMessage = new ConcurrentQueue<string>();

                SetDualMode();

                CreateWebSocketConnection();
            }
            else
            {
                SendLogMessage("Connection can`t be open. GateIoFutures. Error request", LogMessageType.Error);
            }
        }

        private void SetDualMode()
        {
            try
            {
                string mode = _hedgeMode == true ? "true" : "false";
                string queryParam = $"dual_mode={mode}";
                string endpoint = $"{_path}/{_wallet}/dual_mode?{queryParam}";

                IRestResponse result = SendPostQuery(Method.POST, _host, endpoint, null,
                    _path + "/" + _wallet + "/dual_mode", queryParam, "");

                if (result.StatusCode != HttpStatusCode.OK)
                {
                    if (result.Content == "{\"label\":\"NO_CHANGE\"}")
                    {
                        // If the hedging mode was not switched. Ignore
                    }
                    else
                    {
                        SendLogMessage($"SetDualMode> Http State Code: {result.StatusCode}, {result.Content}", LogMessageType.Error);
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        public void Dispose()
        {
            try
            {
                _subscribedSecurities.Clear();
                _allDepths.Clear();

                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _fifoListWebSocketMessage = new ConcurrentQueue<string>();

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.GateIoFutures; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        private const string HTTP_URL = "https://api.gateio.ws/api/v4";

        private string _host = "https://api.gateio.ws";

        private string _path = "/api/v4/futures";

        private string _wallet;

        private string _userId = "";

        private const string WEB_SOCKET_URL = "wss://fx-ws.gateio.ws/v4/ws/";

        private bool _hedgeMode;

        public List<IServerParameter> ServerParameters { get; set; }

        private string _publicKey;

        private string _secretKey;

        #endregion

        #region 3 Securities

        private RateGate _rateGateSecurities = new RateGate(2, TimeSpan.FromMilliseconds(100));

        public void GetSecurities()
        {
            _rateGateSecurities.WaitToProceed();

            try
            {
                string request = HTTP_URL + $"/futures/{_wallet}";

                IRestResponse securitiesJson = SendGetQuery(Method.GET, request, "/contracts", null, false, "");

                if (securitiesJson.StatusCode == HttpStatusCode.OK)
                {
                    List<GfSecurity> securities = JsonConvert.DeserializeObject<List<GfSecurity>>(securitiesJson.Content);

                    UpdateSecurity(securities);
                }
                else
                {
                    SendLogMessage($"GetSecurities> Http State Code: {securitiesJson.StatusCode}, {securitiesJson.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecurity(List<GfSecurity> currencyPairs)
        {
            List<Security> securities = new List<Security>();

            for (int i = 0; i < currencyPairs.Count; i++)
            {
                GfSecurity current = currencyPairs[i];

                if (current.in_delisting == "true")
                {
                    continue;
                }

                string name = current.name.ToUpper();

                Security security = new Security();
                security.Exchange = nameof(ServerType.GateIoFutures);
                security.State = SecurityStateType.Activ;
                security.Name = name;
                security.NameFull = name;
                security.NameClass = _wallet.ToUpper();
                security.NameId = name;
                security.SecurityType = SecurityType.Futures;
                security.PriceStep = current.order_price_round.ToDecimal();
                security.PriceStepCost = security.PriceStep;
                security.Lot = 1;
                security.Decimals = current.order_price_round.DecimalsCount();
                security.DecimalsVolume = current.quanto_multiplier.DecimalsCount();

                security.VolumeStep = current.quanto_multiplier.ToDecimal();
                security.MinTradeAmountType = MinTradeAmountType.Contract;
                security.MinTradeAmount = current.quanto_multiplier.ToDecimal();

                securities.Add(security);
            }

            SecurityEvent(securities);
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public List<Portfolio> Portfolios;

        private RateGate _rateGatePortfolio = new RateGate(2, TimeSpan.FromMilliseconds(250));

        private void PortfolioRequester()
        {
            Thread.Sleep(5000);

            if (Portfolios == null)
            {
                Portfolios = new List<Portfolio>();

                Portfolio portfolioInitial = new Portfolio();
                portfolioInitial.Number = "GateIoFutures";
                portfolioInitial.ValueBegin = 1;
                portfolioInitial.ValueCurrent = 1;
                portfolioInitial.ValueBlocked = 1;

                Portfolios.Add(portfolioInitial);

                PortfolioEvent(Portfolios);
            }

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    Thread.Sleep(3000);

                    GetPortfolios();
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(exception.Message, LogMessageType.Error);
                }
            }
        }

        public void GetPortfolios()
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                IRestResponse result = SendGetQuery(Method.GET, _host + _path + "/" + _wallet, "/accounts", _path + "/" + _wallet + "/accounts", true, "");

                if (result.StatusCode == HttpStatusCode.OK)
                {
                    // 1 Portfolio. USDT

                    GfAccount accountInfo = JsonConvert.DeserializeObject<GfAccount>(result.Content);

                    Portfolio portfolio = Portfolios[0];

                    decimal totalUsdt = Math.Round(accountInfo.total.ToDecimal(), 3);
                    decimal pnl = Math.Round(accountInfo.unrealised_pnl.ToDecimal(), 3);
                    decimal totalFunds = totalUsdt + pnl;
                    decimal availableUsdt = Math.Round(accountInfo.available.ToDecimal(), 3);

                    PositionOnBoard posAllPortfolio = new PositionOnBoard();
                    posAllPortfolio.SecurityNameCode = accountInfo.currency;
                    posAllPortfolio.ValueBegin = availableUsdt;

                    posAllPortfolio.ValueCurrent = availableUsdt;
                    posAllPortfolio.ValueBlocked = Math.Round(accountInfo.position_margin.ToDecimal() + accountInfo.order_margin.ToDecimal(), 3);

                    portfolio.SetNewPosition(posAllPortfolio);

                    if (portfolio.ValueBegin == 0
                        || portfolio.ValueBegin == 1)
                    {
                        portfolio.ValueBegin = totalFunds;
                    }

                    portfolio.ValueCurrent = totalFunds;
                    portfolio.ValueBlocked = posAllPortfolio.ValueBlocked;
                    portfolio.UnrealizedPnl = pnl;

                    // 2 Positions on board
                    string jsonPosition = GetPositionSwap();

                    List<PositionResponseSwap> accountPosition = JsonConvert.DeserializeObject<List<PositionResponseSwap>>(jsonPosition);

                    for (int i = 0; i < accountPosition.Count; i++)
                    {
                        PositionResponseSwap item = accountPosition[i];

                        //string mode = item.mode.Contains("single") ? "Single" : item.mode;
                        //string SellBuy = /*mode == "Single" ? "_Single" :*/ item.mode.Contains("short") ? "_SHORT" : "_LONG";
                        PositionOnBoard position = new PositionOnBoard();
                        position.PortfolioName = "GateIoFutures";

                        if (item.mode.Contains("short"))
                        {
                            position.SecurityNameCode = item.contract + "_SHORT";
                        }
                        else if (item.mode.Contains("long"))
                        {
                            position.SecurityNameCode = item.contract + "_LONG";
                        }
                        else
                        {
                            position.SecurityNameCode = item.contract;
                        }

                        position.ValueBegin = item.size.ToDecimal();
                        position.ValueCurrent = item.size.ToDecimal();
                        position.UnrealizedPnl = item.unrealised_pnl.ToDecimal();

                        portfolio.SetNewPosition(position);
                    }

                    PortfolioEvent(Portfolios);
                }
                else
                {
                    SendLogMessage($"Portfolio> Http State Code: {result.StatusCode}, {result.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        private string GetPositionSwap()
        {
            try
            {
                IRestResponse response = SendGetQuery(Method.GET, _host, $"{_path}/{_wallet}/positions", "/api/v4" + $"/futures/{_wallet}/positions", true, "");

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return response.Content;
                }
                else
                {
                    SendLogMessage($"GetPositionSwap> Http State Code: {response.StatusCode}, {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
            return null;
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;

            if (startTime < DateTime.UtcNow.AddYears(-3) ||
                endTime < DateTime.UtcNow.AddYears(-3) ||
                !CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            List<Trade> allTrades = GetNeedRange(security.Name, startTime, endTime);

            return ClearTrades(allTrades);
        }

        private List<Trade> GetNeedRange(string security, DateTime startTime, DateTime endTime)
        {
            try
            {
                long initTimeStamp = TimeManager.GetTimeStampSecondsToDateTime(DateTime.UtcNow);

                List<Trade> trades = GetTickDataFrom(security, initTimeStamp);

                if (trades == null)
                {
                    return null;
                }

                List<Trade> allTrades = new List<Trade>(100000);

                allTrades.AddRange(trades);

                Trade firstRange = trades[trades.Count - 1];

                List<Trade> allNeedTrades = new List<Trade>();

                while (firstRange.Time > startTime)
                {
                    int ts = TimeManager.GetTimeStampSecondsToDateTime(firstRange.Time);
                    trades = GetTickDataFrom(security, ts);

                    if (trades.Count == 0)
                    {
                        break;
                    }

                    firstRange = trades[trades.Count - 1];
                    allTrades.AddRange(trades);
                }

                allTrades.Reverse();

                for (int i = 0; i < allTrades.Count; i++)
                {
                    if (allTrades[i].Time >= startTime
                        && allTrades[i].Time <= endTime)
                    {
                        allNeedTrades.Add(allTrades[i]);
                    }
                }

                return allNeedTrades;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private RateGate _rateGateData = new RateGate(2, TimeSpan.FromMilliseconds(100));

        private List<Trade> GetTickDataFrom(string security, long startTimeStamp)
        {
            try
            {
                _rateGateData.WaitToProceed();

                string queryParam = $"contract={security}&";
                queryParam += "limit=1000&";
                queryParam += $"to={startTimeStamp}";

                string requestUri = HTTP_URL + $"/futures/{_wallet}/trades?" + queryParam;

                RestRequest requestRest = new RestRequest(Method.GET);

                RestClient client = new RestClient(requestUri);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse responseMessage = client.Execute(requestRest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    List<DataTrade> tradeResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new List<DataTrade>());

                    List<Trade> trades = new List<Trade>();

                    for (int i = 0; i < tradeResponse.Count; i++)
                    {
                        DataTrade current = tradeResponse[i];

                        Trade trade = new Trade();

                        trade.Id = current.id;
                        trade.Price = current.price.ToDecimal();
                        trade.Volume = Math.Abs(current.size.ToDecimal());
                        trade.SecurityNameCode = current.contract;
                        trade.Side = current.size.ToDecimal() > 0 ? Side.Buy : Side.Sell;
                        string[] timeData = current.create_time_ms.Split('.');
                        DateTime time = TimeManager.GetDateTimeFromTimeStampSeconds(long.Parse(timeData[0]));

                        if (timeData.Length > 1)
                        {
                            trade.Time = time.AddMilliseconds(double.Parse(timeData[1]));
                        }

                        trades.Add(trade);
                    }

                    return trades;
                }
                else
                {
                    SendLogMessage($"Execute Trade> Http State Code: {responseMessage.StatusCode}- {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Trade> ClearTrades(List<Trade> trades)
        {
            List<Trade> newTrades = new List<Trade>();

            Trade last = null;

            for (int i = 0; i < trades.Count; i++)
            {
                Trade current = trades[i];

                if (last != null)
                {
                    if (current.Id == last.Id && current.Time == last.Time)
                    {
                        continue;
                    }
                }

                newTrades.Add(current);

                last = current;
            }

            return newTrades;
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

            int timeRange = tfTotalMinutes * 10000;

            DateTime maxStartTime = DateTime.UtcNow.AddMinutes(-timeRange);

            DateTime startTimeData = startTime;

            if (maxStartTime > startTime)
            {
                SendLogMessage("Maximum interval is 10,000 candles from today!", LogMessageType.Error);
                return null;
            }

            DateTime partEndTime = startTimeData.AddMinutes(tfTotalMinutes * 900);

            do
            {
                int from = TimeManager.GetTimeStampSecondsToDateTime(startTimeData);
                int to = TimeManager.GetTimeStampSecondsToDateTime(partEndTime);

                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

                List<Candle> candles = RequestCandleHistory(security.Name, interval, from, to);

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

                startTimeData = partEndTime.AddMinutes(tfTotalMinutes) + TimeSpan.FromMinutes(tfTotalMinutes);
                partEndTime = startTimeData.AddMinutes(tfTotalMinutes * 900);

                if (startTimeData >= DateTime.UtcNow)
                {
                    break;
                }

                if (partEndTime > DateTime.UtcNow)
                {
                    partEndTime = DateTime.UtcNow;
                }

            } while (true);

            return allCandles;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            TimeSpan interval = timeFrameBuilder.TimeFrameTimeSpan;

            int tfTotalMinutes = (int)interval.TotalMinutes;

            int timeRange = tfTotalMinutes * 900;

            DateTime maxStartTime = DateTime.UtcNow.AddMinutes(-timeRange);

            int from = TimeManager.GetTimeStampSecondsToDateTime(maxStartTime);
            int to = TimeManager.GetTimeStampSecondsToDateTime(DateTime.UtcNow);

            string tf = GetInterval(interval);

            List<Candle> candles = RequestCandleHistory(security.Name, tf, from, to);

            return candles;
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

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (endTime > DateTime.UtcNow ||
                startTime >= endTime ||
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
            if (timeFrameMinutes == 1 ||
                timeFrameMinutes == 5 ||
                timeFrameMinutes == 15 ||
                timeFrameMinutes == 30 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 240)
            {
                return true;
            }

            return false;
        }

        private List<Candle> RequestCandleHistory(string security, string interval, int fromTimeStamp, int toTimeStamp)
        {
            _rateGateData.WaitToProceed();

            try
            {
                string queryParam = $"contract={security}&";
                queryParam += $"interval={interval}&";
                queryParam += $"from={fromTimeStamp}&";
                queryParam += $"to={toTimeStamp}";

                string requestUri = HTTP_URL + $"/futures/{_wallet}/candlesticks?" + queryParam;

                RestRequest requestRest = new RestRequest(Method.GET);

                RestClient client = new RestClient(requestUri);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse responseCandleHistory = client.Execute(requestRest);

                if (responseCandleHistory.StatusCode == HttpStatusCode.OK)
                {
                    List<DataCandle> responseData = JsonConvert.DeserializeAnonymousType(responseCandleHistory.Content, new List<DataCandle>());

                    List<Candle> candles = new List<Candle>();

                    for (int i = 0; i < responseData.Count; i++)
                    {
                        DataCandle current = responseData[i];

                        Candle candle = new Candle();

                        candle.State = CandleState.Finished;
                        candle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(int.Parse(current.t));
                        candle.Volume = current.sum.ToDecimal();
                        candle.Close = current.c.ToDecimal();
                        candle.High = current.h.ToDecimal();
                        candle.Low = current.l.ToDecimal();
                        candle.Open = current.o.ToDecimal();

                        candles.Add(candle);
                    }

                    return candles;
                }
                else
                {
                    SendLogMessage($"RequestCandleHistory> Http State Code: {responseCandleHistory.StatusCode} - {responseCandleHistory.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private WebSocket _webSocket;

        private void CreateWebSocketConnection()
        {
            _webSocket = new WebSocket(WEB_SOCKET_URL + _wallet);

            if (_myProxy != null)
            {
                NetworkCredential credential = (NetworkCredential)_myProxy.Credentials;
                _webSocket.SetProxy(_myProxy.Address.ToString(), credential.UserName, credential.Password);
            }

            _webSocket.SslConfiguration.EnabledSslProtocols
                = System.Security.Authentication.SslProtocols.Ssl3
                | System.Security.Authentication.SslProtocols.Tls11
                | System.Security.Authentication.SslProtocols.None
                | System.Security.Authentication.SslProtocols.Tls12
                | System.Security.Authentication.SslProtocols.Tls13
                | System.Security.Authentication.SslProtocols.Tls;
            _webSocket.EmitOnPing = true;

            _webSocket.OnOpen += WebSocket_Opened;
            _webSocket.OnClose += WebSocket_Closed;
            _webSocket.OnMessage += WebSocket_MessageReceived;
            _webSocket.OnError += WebSocket_Error;

            _webSocket.Connect();
        }

        private void DeleteWebSocketConnection()
        {
            if (_webSocket != null)
            {
                try
                {
                    _webSocket.OnOpen -= WebSocket_Opened;
                    _webSocket.OnClose -= WebSocket_Closed;
                    _webSocket.OnMessage -= WebSocket_MessageReceived;
                    _webSocket.OnError -= WebSocket_Error;
                    _webSocket.CloseAsync();
                }
                catch
                {
                    // ignore
                }

                _webSocket = null;
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocket_Error(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            if (e.Exception != null)
            {
                SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_MessageReceived(object sender, MessageEventArgs e)
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

                if (_fifoListWebSocketMessage == null)
                {
                    return;
                }

                if (e.Data.Contains("pong"))
                { // pong message
                    return;
                }

                if (e.Data.Contains("payload"))
                {
                    // responce message - ignore
                    //SendLogMessage("WebSocketData, message:" + e.Message, LogMessageType.Connect);
                    return;
                }

                _fifoListWebSocketMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_Closed(object sender, EventArgs e)
        {
            if (DisconnectEvent != null && ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by GateIo. WebSocket Closed Event", LogMessageType.Connect);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void WebSocket_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Connection Open", LogMessageType.Connect);

            if (ConnectEvent != null && ServerStatus != ServerConnectStatus.Connect)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
        }

        #endregion

        #region 8 WebSocket check alive

        private DateTime _timeLastSendPing = DateTime.Now;

        private void CheckAliveWebSocket()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        _timeLastSendPing = DateTime.Now;
                        Thread.Sleep(1000);
                        continue;
                    }

                    Thread.Sleep(3000);

                    if (_webSocket != null &&
                        _webSocket.ReadyState == WebSocketState.Open)
                    {
                        if (_timeLastSendPing.AddSeconds(20) < DateTime.Now)
                        {
                            SendPing();
                            _timeLastSendPing = DateTime.Now;
                        }
                    }
                }
                catch (Exception e)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                }
            }
        }

        private void SendPing()
        {
            FuturesPing ping = new FuturesPing { time = TimeManager.GetUnixTimeStampSeconds(), channel = "futures.ping" };
            string message = JsonConvert.SerializeObject(ping);
            _webSocket.Send(message);
        }

        #endregion

        #region 9 WebSocket security subscrible

        private readonly Dictionary<string, Security> _subscribedSecurities = new Dictionary<string, Security>();

        public void Subscrible(Security security)
        {
            try
            {
                if (!_subscribedSecurities.ContainsKey(security.Name))
                {
                    _subscribedSecurities.Add(security.Name, security);
                }

                SubscribePortfolio();
                SubscribeMarketDepth(security.Name);
                SubscribeTrades(security.Name);
                SubscribeOrders(security.Name);
                SubscribeMyTrades(security.Name);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void SubscribeMarketDepth(string security)
        {
            AddMarketDepth(security);

            List<string> payload = new List<string>();
            payload.Add(security);

            if (((ServerParameterBool)ServerParameters[12]).Value == true)
            {
                payload.Add("20");
            }
            else
            {
                payload.Add("1");
            }

            payload.Add("0");

            GateFuturesWsRequest payloadMarketDepth = new GateFuturesWsRequest()
            {
                time = TimeManager.GetUnixTimeStampSeconds(),
                channel = "futures.order_book",
                @event = "subscribe",
                payload = payload.ToArray()
            };

            string jsonRequest = JsonConvert.SerializeObject(payloadMarketDepth);
            _webSocket?.Send(jsonRequest);
        }

        private void SubscribeTrades(string security)
        {
            List<string> payload = new List<string>();
            payload.Add(security);

            GateFuturesWsRequest payloadTrades = new GateFuturesWsRequest()
            {
                time = TimeManager.GetUnixTimeStampSeconds(),
                channel = "futures.trades",
                @event = "subscribe",
                payload = payload.ToArray()
            };

            string jsonRequest = JsonConvert.SerializeObject(payloadTrades);
            _webSocket?.Send(jsonRequest);
        }

        private void SubscribeOrders(string security)
        {
            List<string> payload = new List<string>();
            payload.Add(_userId);
            payload.Add(security);

            long timeStamp = TimeManager.GetUnixTimeStampSeconds();
            string param = string.Format("channel={0}&event={1}&time={2}", "futures.orders", "subscribe", timeStamp);

            Auth authOrders = new Auth()
            {
                method = "api_key",
                KEY = _publicKey,
                SIGN = SingData(param)
            };

            GateFuturesWsRequest payloadOrders = new GateFuturesWsRequest()
            {
                time = timeStamp,
                channel = "futures.orders",
                @event = "subscribe",
                payload = payload.ToArray(),
                auth = authOrders
            };

            string jsonRequest = JsonConvert.SerializeObject(payloadOrders);
            _webSocket.Send(jsonRequest);
        }

        private void SubscribeMyTrades(string security)
        {
            List<string> payload = new List<string>();
            payload.Add(_userId);
            payload.Add(security);

            long timeStamp = TimeManager.GetUnixTimeStampSeconds();
            string param = string.Format("channel={0}&event={1}&time={2}", "futures.usertrades", "subscribe", timeStamp);

            Auth authMyTrades = new Auth()
            {
                method = "api_key",
                KEY = _publicKey,
                SIGN = SingData(param)
            };

            GateFuturesWsRequest payloadMyTrades = new GateFuturesWsRequest()
            {
                time = timeStamp,
                channel = "futures.usertrades",
                @event = "subscribe",
                payload = payload.ToArray(),
                auth = authMyTrades
            };

            string jsonRequest = JsonConvert.SerializeObject(payloadMyTrades);
            _webSocket?.Send(jsonRequest);
        }

        private void AddMarketDepth(string name)
        {
            if (!_allDepths.ContainsKey(name))
            {
                _allDepths.Add(name, new MarketDepth());
            }
        }

        private void SubscribePortfolio()
        {
            List<string> payload = new List<string>();
            payload.Add(_userId);

            long timeStamp = TimeManager.GetUnixTimeStampSeconds();
            string param = string.Format("channel={0}&event={1}&time={2}", "futures.balances", "subscribe", timeStamp);

            Auth authPortfolio = new Auth()
            {
                method = "api_key",
                KEY = _publicKey,
                SIGN = SingData(param)
            };

            GateFuturesWsRequest payloadPortfolio = new GateFuturesWsRequest()
            {
                id = timeStamp * 1000000,
                time = timeStamp,
                channel = "futures.balances",
                @event = "subscribe",
                payload = payload.ToArray(),
                auth = authPortfolio
            };

            string jsonRequest = JsonConvert.SerializeObject(payloadPortfolio);
            _webSocket?.Send(jsonRequest);
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent;

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> _fifoListWebSocketMessage = new ConcurrentQueue<string>();

        private void MessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_fifoListWebSocketMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                    }

                    if (_fifoListWebSocketMessage.TryDequeue(out string message))
                    {
                        ResponseWebsocketMessage<object> responseWebsocketMessage;

                        try
                        {
                            responseWebsocketMessage = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<object>());
                        }
                        catch
                        {
                            continue;
                        }

                        if (responseWebsocketMessage.channel.Equals("futures.pong"))
                        {
                            continue;
                        }

                        if (responseWebsocketMessage.channel.Equals("futures.usertrades") && responseWebsocketMessage.Event.Equals("update"))
                        {
                            UpdateMyTrade(message);
                            continue;
                        }
                        else if (responseWebsocketMessage.channel.Equals("futures.orders") && responseWebsocketMessage.Event.Equals("update"))
                        {
                            UpdateOrder(message);
                        }
                        else if (responseWebsocketMessage.channel.Equals("futures.balances") && responseWebsocketMessage.Event.Equals("update"))
                        {
                            UpdatePortfolio(message);
                            continue;
                        }
                        else if (responseWebsocketMessage.channel.Equals("futures.order_book") && responseWebsocketMessage.Event.Equals("all"))
                        {
                            UpdateDepth(message);
                            continue;
                        }
                        else if (responseWebsocketMessage.channel.Equals("futures.trades") && responseWebsocketMessage.Event.Equals("update"))
                        {
                            UpdateTrade(message);
                            continue;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            try
            {
                GfTrades responseTrades = JsonConvert.DeserializeObject<GfTrades>(message);

                for (int i = 0; i < responseTrades.result.Count; i++)
                {
                    long time = Convert.ToInt64(responseTrades.result[i].create_time);

                    Trade newTrade = new Trade();

                    newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(time);
                    newTrade.SecurityNameCode = responseTrades.result[i].contract;
                    newTrade.Price = responseTrades.result[i].price.ToDecimal();
                    newTrade.Id = responseTrades.result[i].id.ToString();
                    newTrade.Volume = Math.Abs(responseTrades.result[i].size.ToDecimal());
                    newTrade.Side = responseTrades.result[i].size.ToString().StartsWith("-") ? Side.Sell : Side.Buy;

                    NewTradesEvent(newTrade);
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private readonly Dictionary<string, MarketDepth> _allDepths = new Dictionary<string, MarketDepth>();

        private void UpdateDepth(string message)
        {
            try
            {
                ResponseWebsocketMessage<MdResponse> responseDepths
               = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<MdResponse>());

                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = responseDepths.result.contract;

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                for (int i = 0; i < responseDepths.result.asks.Count; i++)
                {
                    ascs.Add(new MarketDepthLevel()
                    {
                        Ask = responseDepths.result.asks[i].s.ToDecimal(),
                        Price = responseDepths.result.asks[i].p.ToDecimal()
                    });
                }

                for (int i = 0; i < responseDepths.result.bids.Count; i++)
                {
                    bids.Add(new MarketDepthLevel()
                    {
                        Bid = responseDepths.result.bids[i].s.ToDecimal(),
                        Price = responseDepths.result.bids[i].p.ToDecimal()
                    });
                }

                depth.Asks = ascs;
                depth.Bids = bids;

                depth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepths.result.t));

                if (depth.Time <= _lastMdTime)
                {
                    depth.Time = _lastMdTime.AddTicks(1);
                }

                _lastMdTime = depth.Time;

                _allDepths[depth.SecurityNameCode] = depth;

                MarketDepthEvent(depth);
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        DateTime _lastMdTime;

        private void UpdateMyTrade(string message)
        {
            try
            {
                ResponseWebsocketMessage<List<UserTradeResponse>> responseMyTrade
                = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<List<UserTradeResponse>>());

                for (int i = 0; i < responseMyTrade.result.Count; i++)
                {
                    string security = responseMyTrade.result[i].contract;

                    if (security == null)
                    {
                        continue;
                    }

                    long time = Convert.ToInt64(responseMyTrade.result[i].create_time);

                    MyTrade newTrade = new MyTrade();

                    newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(time);
                    newTrade.SecurityNameCode = security;
                    newTrade.NumberOrderParent = responseMyTrade.result[i].order_id;
                    newTrade.Price = responseMyTrade.result[i].price.ToDecimal();
                    newTrade.NumberTrade = responseMyTrade.result[i].id;
                    newTrade.Side = responseMyTrade.result[i].size.ToDecimal() < 0 ? Side.Sell : Side.Buy;
                    newTrade.Volume = Math.Abs(responseMyTrade.result[i].size.ToDecimal());
                    MyTradeEvent(newTrade);
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                ResponseWebsocketMessage<List<CreateOrderResponse>> responseOrders
                = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<List<CreateOrderResponse>>());

                for (int i = 0; i < responseOrders.result.Count; i++)
                {
                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = responseOrders.result[i].contract;
                    newOrder.TimeCallBack = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(responseOrders.result[i].create_time_ms)).UtcDateTime;
                    newOrder.TimeCreate = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(responseOrders.result[i].create_time_ms)).UtcDateTime;

                    OrderStateType orderState = OrderStateType.None;

                    if (responseOrders.result[i].status.Equals("open"))
                    {
                        orderState = OrderStateType.Active;
                    }
                    else
                    {
                        if (responseOrders.result[i].finish_as.Equals("cancelled"))
                        {
                            orderState = OrderStateType.Cancel;
                        }
                        else if (responseOrders.result[i].finish_as.Equals("liquidated"))
                        {
                            orderState = OrderStateType.Fail;
                        }
                        else if (responseOrders.result[i].finish_as.Equals("filled"))
                        {
                            orderState = OrderStateType.Done;
                        }
                    }

                    try
                    {
                        newOrder.NumberUser = Convert.ToInt32(responseOrders.result[i].text.Replace("t-", ""));
                    }
                    catch
                    {
                        // ignore
                    }

                    newOrder.NumberMarket = responseOrders.result[i].id;
                    newOrder.Side = responseOrders.result[i].size.ToDecimal() > 0 ? Side.Buy : Side.Sell;

                    if (responseOrders.result[i].price == "0")
                    {
                        newOrder.TypeOrder = OrderPriceType.Market;
                    }
                    else
                    {
                        newOrder.TypeOrder = OrderPriceType.Limit;
                    }

                    newOrder.State = orderState;
                    newOrder.Volume = Math.Abs(responseOrders.result[i].size.ToDecimal());
                    newOrder.Price = responseOrders.result[i].price.ToDecimal();
                    newOrder.ServerType = ServerType.GateIoFutures;
                    newOrder.PortfolioNumber = "GateIoFutures";

                    MyOrderEvent(newOrder);
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(string message)
        {
            try
            {
                ResponseWebsocketMessage<List<BalanceResponse>> responsePortfolio
                = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<List<BalanceResponse>>());

                for (int i = 0; i < responsePortfolio.result.Count; i++)
                {
                    BalanceResponse current = responsePortfolio.result[i];

                    PositionOnBoard positionOnBoard = new PositionOnBoard();

                    positionOnBoard.SecurityNameCode = current.currency;
                    positionOnBoard.ValueCurrent = current.balance.ToDecimal();

                    //_myPortfolio.SetNewPosition(positionOnBoard);
                }

                //PortfolioEvent(new List<Portfolio> { _myPortfolio });
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        #endregion

        #region 11 Trade

        private readonly RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(10));

        private readonly RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(20));

        private readonly RateGate _rateGateGetOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                decimal outputVolume = order.Volume;

                if (order.Side == Side.Sell)
                {
                    outputVolume = -1 * order.Volume;
                }

                string size = outputVolume.ToString();
                string price = order.Price.ToString(CultureInfo.InvariantCulture);
                string timeInForce = "gtc";
                string reduceOnly = "false";

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    price = "0";
                    timeInForce = "ioc";
                }

                string bodyContent;

                if (_hedgeMode)
                {
                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        reduceOnly = "true";
                    }
                }

                CreateOrderRequest jOrder = new CreateOrderRequest()
                {
                    contract = order.SecurityNameCode,
                    iceberg = "0",
                    price = price,
                    size = size,
                    tif = timeInForce,
                    text = $"t-{order.NumberUser}",
                    amend_text = $"{order.Side}",
                    reduce_only = reduceOnly,
                };

                bodyContent = JsonConvert.SerializeObject(jOrder).Replace(" ", "").Replace(Environment.NewLine, "");


                IRestResponse responseMessage = SendPostQuery(Method.POST, _host + _path + "/" + _wallet, "/orders", Encoding.UTF8.GetBytes(bodyContent),
                    _path + "/" + _wallet + "/orders", "", bodyContent);

                if (responseMessage.StatusCode != System.Net.HttpStatusCode.Created)
                {
                    SendLogMessage($"SendOrder. Error: {responseMessage.Content}", LogMessageType.Error);
                    order.State = OrderStateType.Fail;
                    MyOrderEvent.Invoke(order);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void CancelAllOrders()
        {
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
        }

        public void CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            try
            {
                IRestResponse result = SendGetQuery(Method.DELETE, _host + _path + "/" + _wallet, $"/orders/{order.NumberMarket}",
                    _path + "/" + _wallet + $"/orders/{order.NumberMarket}", true, "");

                if (result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    CancelOrderResponse cancelResponse = JsonConvert.DeserializeObject<CancelOrderResponse>(result.Content);

                    if (cancelResponse.finish_as == "cancelled")
                    {
                        SendLogMessage($"Order num {order.NumberUser} canceled.", LogMessageType.Trade);
                        order.State = OrderStateType.Cancel;
                        MyOrderEvent(order);
                    }
                    else
                    {
                        GetOrderStatus(order);
                        SendLogMessage($"Error on order cancel num {order.NumberUser}", LogMessageType.Error);
                    }
                }
                else
                {
                    GetOrderStatus(order);
                    SendLogMessage($"CancelOrder> Http State Code: {result.StatusCode}, {result.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetOrderFromExchange(null, "open");

            for (int i = 0; orders != null && i < orders.Count; i++)
            {
                if (orders[i] == null)
                {
                    continue;
                }

                if (orders[i].State != OrderStateType.Active
                    && orders[i].State != OrderStateType.Partial
                    && orders[i].State != OrderStateType.Pending)
                {
                    continue;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        public void GetOrderStatus(Order order)
        {
            List<Order> orderFromExchange = GetOrderFromExchange(order.SecurityNameCode, "open");

            if (orderFromExchange == null
                || orderFromExchange.Count == 0)
            {
                orderFromExchange = GetOrderFromExchange(order.SecurityNameCode, "finished");
            }

            if (orderFromExchange == null
               || orderFromExchange.Count == 0)
            {
                return;
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
                return;
            }

            if (orderOnMarket != null &&
                MyOrderEvent != null)
            {
                MyOrderEvent(orderOnMarket);
            }

            if (orderOnMarket.State == OrderStateType.Done
                || orderOnMarket.State == OrderStateType.Partial)
            {
                FindMyTradesToOrder(order.NumberUser);
            }
        }

        private List<Order> GetOrderFromExchange(string securityNameCode, string status)
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string queryParam = $"status={status}";

                if (securityNameCode != null)
                {
                    queryParam = $"contract={securityNameCode}&";
                    queryParam += $"status={status}";
                }

                string endpoint = $"{_path}/{_wallet}/orders?{queryParam}";

                IRestResponse responseMessage = SendGetQuery(Method.GET, _host, endpoint,
                    _path + "/" + _wallet + "/orders", true, queryParam);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    List<CreateOrderResponse> responseOrders
               = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new List<CreateOrderResponse>());

                    List<Order> orders = new List<Order>();

                    for (int i = 0; i < responseOrders.Count; i++)
                    {
                        Order newOrder = new Order();
                        newOrder.SecurityNameCode = responseOrders[i].contract;

                        string time = responseOrders[i].create_time.Split('.')[0];
                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(time));
                        newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(time));

                        try
                        {
                            newOrder.NumberUser = Convert.ToInt32(responseOrders[i].text.Replace("t-", ""));
                        }
                        catch
                        {
                            // ignore
                        }

                        newOrder.NumberMarket = responseOrders[i].id;
                        newOrder.Side = responseOrders[i].size.ToDecimal() > 0 ? Side.Buy : Side.Sell;

                        if (responseOrders[i].price == "0")
                        {
                            newOrder.TypeOrder = OrderPriceType.Market;
                        }
                        else
                        {
                            newOrder.TypeOrder = OrderPriceType.Limit;
                        }

                        OrderStateType orderState = OrderStateType.None;

                        if (responseOrders[i].status.Equals("open"))
                        {
                            orderState = OrderStateType.Active;
                        }
                        else
                        {
                            if (responseOrders[i].finish_as.Equals("cancelled"))
                            {
                                orderState = OrderStateType.Cancel;
                            }
                            else if (responseOrders[i].finish_as.Equals("liquidated"))
                            {
                                orderState = OrderStateType.Fail;
                            }
                            else if (responseOrders[i].finish_as.Equals("filled"))
                            {
                                orderState = OrderStateType.Done;
                            }
                        }

                        newOrder.State = orderState;
                        newOrder.Volume = Math.Abs(responseOrders[i].size.ToDecimal());
                        newOrder.Price = responseOrders[i].price.ToDecimal();
                        newOrder.ServerType = ServerType.GateIoFutures;
                        newOrder.PortfolioNumber = "GateIoFutures";

                        orders.Add(newOrder);
                    }

                    return orders;
                }
                else
                {
                    SendLogMessage($"GetOrderFromExchange>. Error: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private void FindMyTradesToOrder(int numberUser)
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {

                string endpoint = $"{_path}/{_wallet}/my_trades";

                IRestResponse responseMessage = SendGetQuery(Method.GET, _host, endpoint,
                    _path + "/" + _wallet + "/my_trades", true, "");

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    List<UserTradeResponse> responseMyTrade
                 = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new List<UserTradeResponse>());

                    for (int i = 0; i < responseMyTrade.Count; i++)
                    {
                        string security = responseMyTrade[i].contract;

                        if (security == null)
                        {
                            continue;
                        }

                        int userNumber = 0;

                        if (responseMyTrade[i].text.Contains("t"))
                        {
                            userNumber = Convert.ToInt32(responseMyTrade[i].text.Replace("t-", ""));
                        }
                        else
                        {
                            continue;
                        }

                        if (userNumber == numberUser)
                        {
                            string time = responseMyTrade[i].create_time.Split('.')[0];

                            MyTrade newTrade = new MyTrade();

                            newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(time));
                            newTrade.SecurityNameCode = security;
                            newTrade.NumberOrderParent = responseMyTrade[i].order_id;
                            newTrade.Price = responseMyTrade[i].price.ToDecimal();
                            newTrade.NumberTrade = responseMyTrade[i].id;
                            newTrade.Side = responseMyTrade[i].size.ToDecimal() < 0 ? Side.Sell : Side.Buy;
                            newTrade.Volume = Math.Abs(responseMyTrade[i].size.ToDecimal());

                            MyTradeEvent(newTrade);
                        }
                    }
                }
                else
                {
                    SendLogMessage($"FindMyTradesToOrder>. Error: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 12 Queries

        public IRestResponse SendGetQuery(Method method, string baseUri, string endPoint, string fullPath, bool signer = false, string queryParam = null)
        {
            try
            {
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                string fullUrl = baseUri + endPoint;

                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest requestRest = new RestRequest(method);

                if (signer)
                {
                    string sign = GetSignStringRest(method.ToString(), fullPath, queryParam, "", timeStamp);

                    requestRest.AddHeader("Timestamp", timeStamp);
                    requestRest.AddHeader("KEY", _publicKey);
                    requestRest.AddHeader("SIGN", sign);
                }
                else
                {
                    requestRest.AddHeader("Timestamp", timeStamp);
                }

                IRestResponse response = client.Execute(requestRest);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        public IRestResponse SendPostQuery(Method method, string url, string endPoint, byte[] data,
            string fullPath, string queryParam, string bodyContent)
        {
            try
            {
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                string sign = GetSignStringRest(method.ToString(), fullPath, queryParam, bodyContent, timeStamp);

                string fullUrl = url + endPoint;

                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest requestRest = new RestRequest(method);

                requestRest.AddHeader("Timestamp", timeStamp);
                requestRest.AddHeader("KEY", _publicKey);
                requestRest.AddHeader("SIGN", sign);
                requestRest.AddHeader("X-Gate-Channel-Id", "osa");

                if (data != null)
                {
                    requestRest.AddParameter("application/json", data, ParameterType.RequestBody);
                }

                IRestResponse response = client.Execute(requestRest);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        public string GetSignStringRest(string method, string fullPath, string queryParam, string bodyContent, string timeStamp)
        {
            string bodyHash = SHA512HexHashString(bodyContent);

            StringBuilder sb = new StringBuilder();

            sb.Append(method + "\n");
            sb.Append(fullPath + "\n");
            sb.Append(queryParam + "\n");
            sb.Append(bodyHash + "\n");
            sb.Append(timeStamp);

            return SingData(sb.ToString());
        }

        public string SingData(string signatureString)
        {
            HMACSHA512 hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_secretKey));

            byte[] buffer = Encoding.UTF8.GetBytes(signatureString);

            return BitConverter.ToString(hmac.ComputeHash(buffer)).Replace("-", "").ToLower();
        }

        private string SHA512HexHashString(string stringIn)
        {
            string hashString;
            using (var sha256 = SHA512Managed.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(stringIn));
                hashString = ToHex(hash, false);
            }

            return hashString;
        }

        private string ToHex(byte[] bytes, bool upperCase)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));
            return result.ToString();
        }

        #endregion

        #region 13 Log

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            if (LogMessageEvent != null)
                LogMessageEvent(message, messageType);
        }

        #endregion
    }
}