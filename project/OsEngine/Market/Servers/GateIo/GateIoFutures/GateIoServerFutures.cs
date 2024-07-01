/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.GateIo.Futures.Entities;
using OsEngine.Market.Servers.GateIo.Futures.Request;
using OsEngine.Market.Servers.GateIo.GateIoFutures.Entities;
using OsEngine.Market.Servers.GateIo.GateIoFutures.Entities.Response;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocket4Net;
using GfSecurity = OsEngine.Market.Servers.GateIo.GateIoFutures.Entities.GfSecurity;
using RestRequestBuilder = OsEngine.Market.Servers.GateIo.GateIoFutures.Entities.RestRequestBuilder;

namespace OsEngine.Market.Servers.GateIo.GateIoFutures
{
    internal class GateIoServerFutures : AServer
    {
        public GateIoServerFutures()
        {
            ServerRealization = new GateIoServerFuturesRealization();
            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterString("User ID", "");
            CreateParameterEnum("Base Wallet", "USDT", new List<string> { "USDT", "BTC" });
            CreateParameterEnum("Position Mode", "Single", new List<string> { "Single", "Double" });
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

        public void Connect()
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _userId = ((ServerParameterString)ServerParameters[2]).Value;
            _baseWallet = ((ServerParameterEnum)ServerParameters[3]).Value;
            _positionMode = ((ServerParameterEnum)ServerParameters[4]).Value;

            if (_baseWallet == "BTC")
                _wallet = "btc";

            _requestRest = new RestRequestBuilder();
            _signer = new Signer(_secretKey);

            HttpResponseMessage responseMessage = null;

            int tryCounter = 0;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    responseMessage = _httpPublicClient.GetAsync(HTTP_URL + "/futures/usdt/contracts/BTC_USDT").Result;
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

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                _timeLastSendPing = DateTime.Now;
                _fifoListWebSocketMessage = new ConcurrentQueue<string>();

                SetDualMode();

                CreateWebSocketConnection();
            }
            else
            {
                throw new Exception("Connection can`t be open. GateIo. Error request");
            }
        }

        private void SetDualMode()
        {
            string mode = _positionMode.Equals("Single") ? "false" : "true";

            string apiKey = _publicKey;
            string apiSecret = _secretKey;
            string prefix = "/api/v4";
            string method = "POST";
            string url = "/futures/usdt/dual_mode";
            string queryParam = $"dual_mode={mode}";
            string bodyParam = "";
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            byte[] bodyBytes = Encoding.UTF8.GetBytes(bodyParam);
            string bodyHash = BitConverter.ToString(SHA512.Create().ComputeHash(bodyBytes)).Replace("-", "").ToLower();

            string signString = $"{method}\n{prefix}{url}\n{queryParam}\n{bodyHash}\n{timestamp}";
            byte[] secretBytes = Encoding.UTF8.GetBytes(apiSecret);
            byte[] signBytes = new HMACSHA512(secretBytes).ComputeHash(Encoding.UTF8.GetBytes(signString));
            string sign = BitConverter.ToString(signBytes).Replace("-", "").ToLower();

            string endpoint = $"{_path}/{_wallet}/dual_mode?{queryParam}";

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Timestamp", timestamp);
            headers.Add("KEY", apiKey);
            headers.Add("SIGN", sign);

            try
            {
                _requestRest.SendPostQuery("POST", _host, endpoint, null, headers);
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

                DeleteWebsocketConnection();
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(),LogMessageType.Error);
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

        private string _host = "https://api.gateio.ws";

        private string _path = "/api/v4/futures";

        private string _wallet = "usdt";

        private string _positionMode = "Double";

        private string _userId = "";

        private string _baseWallet = "";

        private const string WEB_SOCKET_URL = "wss://fx-ws.gateio.ws/v4/ws/";

        private RestRequestBuilder _requestRest;

        private Signer _signer;

        public List<IServerParameter> ServerParameters { get; set; }

        private string _publicKey;

        private string _secretKey;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            try
            {
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();

                Dictionary<string, string> headers = new Dictionary<string, string>();

                headers.Add("Timestamp", timeStamp);

                string request = HTTP_URL + $"/futures/{_wallet}";

                string securitiesJson = _requestRest.SendGetQuery("GET", request, "/contracts", headers);

                GfSecurity[] securities = JsonConvert.DeserializeObject<GfSecurity[]>(securitiesJson);

                UpdateSecurity(securities);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecurity(GfSecurity[] currencyPairs)
        {
            List<Security> securities = new List<Security>();

            for (int i = 0; i < currencyPairs.Length; i++)
            {
                GfSecurity current = currencyPairs[i];

                if (current.InDelisting == "true")
                {
                    continue;
                }

                string name = current.Name.ToUpper();

                Security security = new Security();
                security.Exchange = nameof(ServerType.GateIoFutures);
                security.State = SecurityStateType.Activ;
                security.Name = name;
                security.NameFull = name;
                security.NameClass = _wallet.ToUpper();
                security.NameId = name;
                security.SecurityType = SecurityType.Futures;
                security.PriceStep = current.OrderPriceRound.ToDecimal();
                security.PriceStepCost = security.PriceStep;
                security.Lot = current.MarkPriceRound.ToDecimal();
                security.Decimals = current.OrderPriceRound.Split('.')[1].Count();
                security.DecimalsVolume = current.MarkPriceRound.Split('.')[1].Count();

                if (current.OrderSizeMin != null)
                {
                    security.MinTradeAmount = current.OrderSizeMin.ToDecimal();
                }

                securities.Add(security);
            }

            SecurityEvent(securities);
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public List<Portfolio> Portfolios;

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
                    if(ServerStatus == ServerConnectStatus.Disconnect)
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
            try
            {
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                Dictionary<string, string> headers = new Dictionary<string, string>();

                headers.Add("Timestamp", timeStamp);
                headers.Add("KEY", _publicKey);
                headers.Add("SIGN", _signer.GetSignStringRest("GET", _path + "/" + _wallet + "/accounts", "", "", timeStamp));

                string result = _requestRest.SendGetQuery("GET", _host + _path + "/" + _wallet, "/accounts", headers);

                string jsonPosition = GetPositionSwap(timeStamp);

                if (result.Contains("failed"))
                {
                    SendLogMessage("GateIFutures: Cant get porfolios", LogMessageType.Error);
                    return;
                }

                List<PositionResponceSwap> accountPosition = JsonConvert.DeserializeObject<List<PositionResponceSwap>>(jsonPosition);

                GfAccount accountInfo = JsonConvert.DeserializeObject<GfAccount>(result);

                Portfolio portfolio = Portfolios[0];

                portfolio.ClearPositionOnBoard();

                PositionOnBoard pos = new PositionOnBoard();
                pos.SecurityNameCode = accountInfo.Currency;
                pos.ValueBegin = accountInfo.Total.ToDecimal();
                pos.ValueCurrent = accountInfo.Available.ToDecimal();
                pos.ValueBlocked = accountInfo.PositionMargin.ToDecimal() + accountInfo.OrderMargin.ToDecimal();

                portfolio.SetNewPosition(pos);

                for(int i = 0; i < accountPosition.Count; i++)
                {
                    PositionResponceSwap item = accountPosition[i];

                    string mode = item.mode.Contains("single") ? "Single" : item.mode;
                    string SellBuy = mode == "Single" ? "_Single" : item.mode.Contains("short") ? "_SHORT" : "_LONG";
                    PositionOnBoard position = new PositionOnBoard();
                    position.PortfolioName = "GateIoFutures";
                    position.SecurityNameCode = item.contract + SellBuy;
                    position.ValueBegin = item.size.ToDecimal();
                    position.ValueCurrent = item.size.ToDecimal();
                    portfolio.SetNewPosition(position);
                }

                PortfolioEvent(Portfolios);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        private string GetPositionSwap(string timeStamp)
        {
            string sign = _signer.GetSignStringRest("GET", "/api/v4" + $"/futures/{_wallet}/positions", "", "", timeStamp);

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Timestamp", timeStamp);
            headers.Add("KEY", _publicKey);
            headers.Add("SIGN", sign);

            string response = _requestRest.SendGetQuery("GET", _host, $"{_path}/{_wallet}/positions", headers);

            return response;
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime < DateTime.Now.AddYears(-3) ||
                endTime < DateTime.Now.AddYears(-3) ||
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
                long initTimeStamp = TimeManager.GetTimeStampSecondsToDateTime(DateTime.Now);

                List<Trade> trades = GetTickDataFrom(security, initTimeStamp);

                if (trades == null)
                {
                    return null;
                }

                List<Trade> allTrades = new List<Trade>(100000);

                allTrades.AddRange(trades);

                Trade firstRange = trades.Last();

                while (firstRange.Time > startTime)
                {
                    int ts = TimeManager.GetTimeStampSecondsToDateTime(firstRange.Time);
                    trades = GetTickDataFrom(security, ts);

                    if (trades.Count == 0)
                    {
                        break;
                    }

                    firstRange = trades.Last();
                    allTrades.AddRange(trades);
                }

                allTrades.Reverse();

                List<Trade> allNeedTrades = allTrades.FindAll(t => t.Time >= startTime && t.Time <= endTime);

                return allNeedTrades;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Trade> GetTickDataFrom(string security, long startTimeStamp)
        {
            string queryParam = $"contract={security}&";
            queryParam += "limit=1000&";
            queryParam += $"to={startTimeStamp}";

            string requestUri = HTTP_URL + $"/futures/{_wallet}/trades?" + queryParam;

            return Execute(requestUri);
        }

        private readonly RateGate _rgTickData = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<Trade> Execute(string requestUri)
        {
            try
            {
                _rgTickData.WaitToProceed(50);

                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(requestUri).Result;

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;

                    List<DataTrade> response = JsonConvert.DeserializeAnonymousType(json, new List<DataTrade>());

                    return ConvertTrades(response);
                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Trade> ConvertTrades(List<DataTrade> tradeResponse)
        {
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

            DateTime maxStartTime = DateTime.Now.AddMinutes(-timeRange);

            DateTime startTimeData = startTime;

            if (maxStartTime > startTime)
            {
                SendLogMessage("Maximal interval 10000 candles!", LogMessageType.Error);
                return null;
            }

            DateTime partEndTime = startTimeData.AddMinutes(tfTotalMinutes * _limit);

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

                Candle last = candles.Last();

                if (last.TimeStart >= endTime)
                {
                    allCandles.AddRange(candles.Where(c => c.TimeStart <= endTime));
                    break;
                }

                allCandles.AddRange(candles);

                startTimeData = partEndTime.AddMinutes(tfTotalMinutes) + TimeSpan.FromMinutes(tfTotalMinutes);
                partEndTime = startTimeData.AddMinutes(tfTotalMinutes * _limit);

                if (startTimeData >= DateTime.Now)
                {
                    break;
                }

                if (partEndTime > DateTime.Now)
                {
                    partEndTime = DateTime.Now;
                }

            } while (true);

            return allCandles;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            TimeSpan interval = timeFrameBuilder.TimeFrameTimeSpan;

            int tfTotalMinutes = (int)interval.TotalMinutes;

            int timeRange = tfTotalMinutes * 900;

            DateTime maxStartTime = DateTime.Now.AddMinutes(-timeRange);

            int from = TimeManager.GetTimeStampSecondsToDateTime(maxStartTime);
            int to = TimeManager.GetTimeStampSecondsToDateTime(DateTime.UtcNow);

            string tf = GetInterval(interval);

            List<Candle> candles = RequestCandleHistory(security.Name, tf, from, to);

            return candles;
        }

        private int _limit = 900;

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

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (endTime > DateTime.Now ||
                startTime >= endTime ||
                startTime >= DateTime.Now ||
                actualTime > endTime ||
                actualTime > DateTime.Now)
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

        private readonly RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<Candle> RequestCandleHistory(string security, string interval, int fromTimeStamp, int toTimeStamp)
        {
            _rgCandleData.WaitToProceed(100);

            try
            {
                string queryParam = $"contract={security}&";
                queryParam += $"interval={interval}&";
                queryParam += $"from={fromTimeStamp}&";
                queryParam += $"to={toTimeStamp}";

                string requestUri = HTTP_URL + $"/futures/{_wallet}/candlesticks?" + queryParam;

                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(requestUri).Result;

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;

                    List<DataCandle> response = JsonConvert.DeserializeAnonymousType(json, new List<DataCandle>());

                    return ConvertCandles(response);
                }
                else
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode} - {json}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Candle> ConvertCandles(List<DataCandle> rawList)
        {
            List<Candle> candles = new List<Candle>();

            for (int i = 0; i < rawList.Count; i++)
            {
                DataCandle current = rawList[i];

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

        #endregion

        #region 6 WebSocket creation

        private WebSocket _webSocket;

        private void CreateWebSocketConnection()
        {
            _webSocket = new WebSocket(WEB_SOCKET_URL + _wallet);
            _webSocket.EnableAutoSendPing = true;
            _webSocket.AutoSendPingInterval = 10;

            _webSocket.Opened += WebSocket_Opened;
            _webSocket.Closed += WebSocket_Closed;
            _webSocket.MessageReceived += WebSocket_MessageReceived;
            _webSocket.Error += WebSocket_Error;

            _webSocket.Open();
        }

        private void DeleteWebsocketConnection()
        {
            if (_webSocket != null)
            {
                try
                {
                    _webSocket.Close();
                }
                catch
                {
                    // ignore
                }

                _webSocket.Opened -= WebSocket_Opened;
                _webSocket.Closed -= WebSocket_Closed;
                _webSocket.MessageReceived -= WebSocket_MessageReceived;
                _webSocket.Error -= WebSocket_Error;
                _webSocket = null;
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocket_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs error)
        {
            if (error.Exception != null)
            {
                SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                if(ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.Message))
                {
                    return;
                }

                if (_fifoListWebSocketMessage == null)
                {
                    return;
                }

                _fifoListWebSocketMessage.Enqueue(e.Message);
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
                        _webSocket.State == WebSocketState.Open)
                    {
                        if (_timeLastSendPing.AddSeconds(30) < DateTime.Now)
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
            FuturesPing ping = new FuturesPing { Time = TimeManager.GetUnixTimeStampSeconds(), Channel = "futures.ping" };
            string message = JsonConvert.SerializeObject(ping);
            _webSocket.Send(message);
        }

        #endregion

        #region 9 WebSocket security subscrible

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
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private void SubscribeMarketDepth(string security)
        {
            AddMarketDepth(security);

            List<string> payload = new List<string>();
            payload.Add(security);
            payload.Add("20");
            payload.Add("0");

            WsRequestBuilder request = new WsRequestBuilder(TimeManager.GetUnixTimeStampSeconds(), "futures.order_book", "subscribe", payload.ToArray());

            string message = request.GetPublicRequest();

            _webSocket?.Send(message);
        }

        private void SubscribeTrades(string security)
        {
            List<string> payload = new List<string>();
            payload.Add(security);

            WsRequestBuilder request = new WsRequestBuilder(TimeManager.GetUnixTimeStampSeconds(), "futures.trades", "subscribe", payload.ToArray());

            string message = request.GetPublicRequest();

            _webSocket?.Send(message);
        }

        private void SubscribeOrders(string security)
        {
            List<string> payload = new List<string>();
            payload.Add(_userId);
            payload.Add(security);

            WsRequestBuilder request = new WsRequestBuilder(TimeManager.GetUnixTimeStampSeconds(), "futures.orders", "subscribe", payload.ToArray());

            string message = request.GetPrivateRequest(_publicKey, _secretKey);

            _webSocket?.Send(message);
        }

        private void SubscribeMyTrades(string security)
        {
            List<string> payload = new List<string>();
            payload.Add(_userId);
            payload.Add(security);

            WsRequestBuilder request = new WsRequestBuilder(TimeManager.GetUnixTimeStampSeconds(), "futures.usertrades", "subscribe", payload.ToArray());

            string message = request.GetPrivateRequest(_publicKey, _secretKey);

            _webSocket?.Send(message);
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
            string channel = "futures.balances";
            string eventName = "subscribe";
            long timestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            string apiKey = _publicKey;
            string apiSecret = _secretKey;

            string s = string.Format("channel={0}&event={1}&time={2}", channel, eventName, timestamp);
            byte[] keyBytes = Encoding.UTF8.GetBytes(apiSecret);
            byte[] messageBytes = Encoding.UTF8.GetBytes(s);
            byte[] hashBytes;

            using (HMACSHA512 hash = new HMACSHA512(keyBytes))
            {
                hashBytes = hash.ComputeHash(messageBytes);
            }

            string sign = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

            JObject authObject = new JObject {
                { "method", "api_key" },
                { "KEY", apiKey },
                { "SIGN", sign }
            };

            JObject payloadObject = new JObject {
                
                { "time", timestamp },
                { "channel", channel },
                { "event", eventName },
                { "payload", new JArray(new object[]{_userId}) },
                { "auth", authObject }
            };

            string jsonRequest = JsonConvert.SerializeObject(payloadObject);

            _webSocket.Send(jsonRequest);
        }

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
                        ResponceWebsocketMessage<object> responseWebsocketMessage;

                        try
                        {
                            responseWebsocketMessage = JsonConvert.DeserializeAnonymousType(message, new ResponceWebsocketMessage<object>());
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
                catch (Exception exeption)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            GfTrades jt = JsonConvert.DeserializeObject<GfTrades>(message);

            foreach (GfTradeResult trade in jt.Result)
            {
                string security = trade.Contract;

                long time = trade.CreateTime;

                Trade newTrade = new Trade();

                newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(time);
                newTrade.SecurityNameCode = security;
                newTrade.Price = trade.Price.ToDecimal();
                newTrade.Id = trade.Id.ToString();
                newTrade.Volume = Math.Abs((decimal)trade.Size);
                newTrade.Side = trade.Size.ToString().StartsWith("-") ? Side.Sell : Side.Buy;

                NewTradesEvent(newTrade);
            }
        }

        private readonly Dictionary<string, MarketDepth> _allDepths = new Dictionary<string, MarketDepth>();

        private void UpdateDepth(string message)
        {
            ResponceWebsocketMessage<MdResponse> responseDepths
                = JsonConvert.DeserializeAnonymousType(message, new ResponceWebsocketMessage<MdResponse>());
            try
            {
                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = responseDepths.result.Contract;

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                for (int i = 0; i < responseDepths.result.Asks.Count; i++)
                {
                    ascs.Add(new MarketDepthLevel()
                    {
                        Ask = responseDepths.result.Asks[i].S.ToDecimal(),
                        Price = responseDepths.result.Asks[i].P.ToDecimal()
                    });
                }

                for (int i = 0; i < responseDepths.result.Bids.Count; i++)
                {
                    bids.Add(new MarketDepthLevel()
                    {
                        Bid = responseDepths.result.Bids[i].S.ToDecimal(),
                        Price = responseDepths.result.Bids[i].P.ToDecimal()
                    });
                }

                depth.Asks = ascs;
                depth.Bids = bids;

                depth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepths.result.T));

                _allDepths[depth.SecurityNameCode] = depth;

                MarketDepthEvent(depth);
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(string message)
        {
            ResponceWebsocketMessage<List<UserTradeResponse>> responseDepths 
                = JsonConvert.DeserializeAnonymousType(message, new ResponceWebsocketMessage<List<UserTradeResponse>>());

            for (int i = 0; i < responseDepths.result.Count; i++)
            {
                string security = responseDepths.result[i].Contract;

                if (security == null)
                {
                    continue;
                }

                long time = Convert.ToInt64(responseDepths.result[i].CreateTime);

                MyTrade newTrade = new MyTrade();

                newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(time);
                newTrade.SecurityNameCode = security;
                newTrade.NumberOrderParent = responseDepths.result[i].OrderId;
                newTrade.Price = responseDepths.result[i].Price.ToDecimal();
                newTrade.NumberTrade = responseDepths.result[i].Id;
                newTrade.Side = responseDepths.result[i].Size.ToDecimal() < 0 ? Side.Sell : Side.Buy;
                newTrade.Volume = Math.Abs(responseDepths.result[i].Size.ToDecimal());
                MyTradeEvent(newTrade);
            }
        }

        private void UpdateOrder(string message)
        {
            ResponceWebsocketMessage<List<CreateOrderResponse>> responseDepths 
                = JsonConvert.DeserializeAnonymousType(message, new ResponceWebsocketMessage<List<CreateOrderResponse>>());

            for (int i = 0; i < responseDepths.result.Count; i++)
            {
                if (responseDepths.result[i].Text == "web")
                {
                    continue;
                }

                Order newOrder = new Order();
                newOrder.SecurityNameCode = responseDepths.result[i].Contract;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responseDepths.result[i].CreateTime));

                OrderStateType orderState = OrderStateType.None;

                if (responseDepths.result[i].Status.Equals("open"))
                {
                    orderState = OrderStateType.Activ;
                }
                else
                {
                    if (responseDepths.result[i].FinishAs.Equals("cancelled"))
                    {
                        orderState = OrderStateType.Cancel;
                    }
                    else if (responseDepths.result[i].FinishAs.Equals("liquidated"))
                    {
                        orderState = OrderStateType.Fail;
                    }
                    else if (responseDepths.result[i].FinishAs.Equals("filled"))
                    {
                        orderState = OrderStateType.Done;
                    }
                }

                newOrder.NumberUser = Convert.ToInt32(responseDepths.result[i].Text.Replace("t-", ""));
                newOrder.NumberMarket = responseDepths.result[i].Id;
                newOrder.Side = responseDepths.result[i].Size.ToDecimal() > 0 ? Side.Buy : Side.Sell;
                newOrder.State = orderState;
                newOrder.Volume = Math.Abs(responseDepths.result[i].Size.ToDecimal());
                newOrder.Price = responseDepths.result[i].Price.ToDecimal();
                newOrder.ServerType = ServerType.GateIoFutures;
                newOrder.PortfolioNumber = "GateIoFuturesWallet";

                MyOrderEvent(newOrder);
            }
        }

        private void UpdatePortfolio(string message)
        {
            ResponceWebsocketMessage<List<BalanceResponse>> responsePortfolio 
                = JsonConvert.DeserializeAnonymousType(message, new ResponceWebsocketMessage<List<BalanceResponse>>());

            for (int i = 0; i < responsePortfolio.result.Count; i++)
            {
                BalanceResponse current = responsePortfolio.result[i];

                PositionOnBoard positionOnBoard = new PositionOnBoard();

                positionOnBoard.SecurityNameCode = current.Currency;
                positionOnBoard.ValueBegin = current.Balance.ToDecimal();
                //positionOnBoard.ValueCurrent = current.Available.ToDecimal();
                //positionOnBoard.ValueBlocked = current.Freeze.ToDecimal();

                //_myPortfolio.SetNewPosition(positionOnBoard);
            }

            //PortfolioEvent(new List<Portfolio> { _myPortfolio });
        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        #endregion

        #region 11 Trade

        private readonly RateGate _rgSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private readonly RateGate _rgCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public void SendOrder(Order order)
        {
            if (order.Volume < 0)
            {
                order.State = OrderStateType.Fail;
                MyOrderEvent(order);
                return;
            }

            _rgSendOrder.WaitToProceed(100);

            decimal outputVolume = order.Volume;
            if (order.Side == Side.Sell)
                outputVolume = -1 * order.Volume;

            string bodyContent;
            if (_positionMode.Equals("Double") &&
                order.PositionConditionType == OrderPositionConditionType.Close)
            {
                string close = order.Side == Side.Sell ? "close_long" : "close_short";
                
                CreateOrderRequstDoubleModeClose jOrder = new CreateOrderRequstDoubleModeClose()
                {
                    Contract = order.SecurityNameCode,
                    Iceberg = 0,
                    Price = order.Price.ToString(CultureInfo.InvariantCulture),
                    Size = Convert.ToInt64(outputVolume),
                    Tif = "gtc",
                    Text = $"t-{order.NumberUser}",
                    Close = false,
                    Reduce_only = true
                };

                bodyContent = JsonConvert.SerializeObject(jOrder).Replace(" ", "").Replace(Environment.NewLine, "");
            }
            else
            {
                CreateOrderRequst jOrder = new CreateOrderRequst()
                {
                    Contract = order.SecurityNameCode,
                    Iceberg = 0,
                    Price = order.Price.ToString(CultureInfo.InvariantCulture),
                    Size = Convert.ToInt64(outputVolume),
                    Tif = "gtc",
                    Text = $"t-{order.NumberUser}",
                    AmendText = $"{order.Side}"
                };

                bodyContent = JsonConvert.SerializeObject(jOrder).Replace(" ", "").Replace(Environment.NewLine, "");
            }

            string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();

            Dictionary<string, string> headers = new Dictionary<string, string>();

            headers.Add("Timestamp", timeStamp);
            headers.Add("KEY", _publicKey);
            headers.Add("SIGN", _signer.GetSignStringRest("POST", _path + "/" + _wallet + "/orders", "", bodyContent, timeStamp));
            headers.Add("X-Gate-Channel-Id", "osa");

            try
            {
                string result = _requestRest.SendPostQuery("POST", _host + _path + "/" + _wallet, "/orders", Encoding.UTF8.GetBytes(bodyContent), headers);

                CreateOrderResponse orderResponse = JsonConvert.DeserializeObject<CreateOrderResponse>(result);

                if (orderResponse.Status == "open" || orderResponse.Status == "finished")
                {
                    order.NumberMarket = orderResponse.Id;
                    order.State = OrderStateType.Activ;
                    SendLogMessage($"Order num {order.NumberUser} wait to execution on exchange.", LogMessageType.Trade);
                }
                else
                {
                    dynamic errorData = JToken.Parse(result);
                    string errorMsg = errorData.err_msg;
                    SendLogMessage($"Order exchange error num {order.NumberUser} : {errorMsg}", LogMessageType.Error);
                    order.State = OrderStateType.Fail;
                    MyOrderEvent(order);
                }
            }
            catch (Exception exception)
            {
                order.State = OrderStateType.Fail;
                MyOrderEvent(order);
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void GetOrdersState(List<Order> orders)
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
            _rgCancelOrder.WaitToProceed(100);

            try
            {
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();

                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Timestamp", timeStamp);
                headers.Add("KEY", _publicKey);
                headers.Add("SIGN", _signer.GetSignStringRest("DELETE", _path + "/" + _wallet + $"/orders/{order.NumberMarket}", "", "", timeStamp));

                string result = _requestRest.SendGetQuery("DELETE", _host + _path + "/" + _wallet, $"/orders/{order.NumberMarket}", headers);

                CancelOrderResponse cancelResponse = JsonConvert.DeserializeObject<CancelOrderResponse>(result);

                if (cancelResponse.FinishAs == "cancelled")
                {
                    SendLogMessage($"Order num {order.NumberUser} canceled.", LogMessageType.Trade);
                    order.State = OrderStateType.Cancel;
                    MyOrderEvent(order);
                }
                else
                {
                    SendLogMessage($"Error on order cancel num {order.NumberUser}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                if (exception is WebException)
                {
                    WebException ex = (WebException)exception;
                    HttpWebResponse httpResponse = (HttpWebResponse)ex.Response;
                    if (ex.Response != null)
                    {
                        using (Stream stream = ex.Response.GetResponseStream())
                        {
                            StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                            string log = reader.ReadToEnd();
                            SendLogMessage(log, LogMessageType.Error);
                            return;
                        }
                    }
                }
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {

        }

        public void GetOrderStatus(Order order)
        {

        }

        #endregion

        #region 12 Queries

        private const string HTTP_URL = "https://api.gateio.ws/api/v4";

        private readonly HttpClient _httpPublicClient = new HttpClient();

        private readonly Dictionary<string, Security> _subscribedSecurities = new Dictionary<string, Security>();

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