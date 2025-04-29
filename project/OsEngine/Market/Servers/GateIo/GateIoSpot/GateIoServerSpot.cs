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
using OsEngine.Market.Servers.GateIo.GateIoSpot.Entities;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocketSharp;

namespace OsEngine.Market.Servers.GateIo.GateIoSpot
{
    public class GateIoServerSpot : AServer
    {
        public GateIoServerSpot(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            ServerRealization = new GateIoServerSpotRealization();
            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
        }
    }

    public sealed class GateIoServerSpotRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public GateIoServerSpotRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread thread = new Thread(CheckAliveWebSocket);
            thread.IsBackground = true;
            thread.Name = "CheckAliveWebSocket";
            thread.Start();

            Thread _messageReaderThread = new Thread(MessageReader);
            _messageReaderThread.IsBackground = true;
            _messageReaderThread.Name = "MessageReaderGateIo";
            _messageReaderThread.Start();
        }

        public DateTime ServerTime { get; set; }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy)
        {
            _myProxy = proxy;
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            HttpResponseMessage responseMessage = null;

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

            int tryCounter = 0;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    responseMessage = _httpPublicClient.GetAsync(HttpUrl + "/spot/time").Result;
                    break;
                }
                catch (Exception e)
                {
                    tryCounter++;

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
                CreateWebSocketConnection();
            }
            else
            {
                SendLogMessage("Connection can`t be open. GateIo. Error request", LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            try
            {
                _subscribedSecurities.Clear();
                _allDepths.Clear();
                _securities.Clear();

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
            get { return ServerType.GateIoSpot; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        private string _host = "https://api.gateio.ws";

        private string _prefix = "/api/v4";

        public List<IServerParameter> ServerParameters { get; set; }

        private string _publicKey;

        private string _secretKey;

        #endregion

        #region 3 Securities

        List<Security> _securities = new List<Security>();

        private RateGate _rateGateSecurities = new RateGate(2, TimeSpan.FromMilliseconds(100));

        public void GetSecurities()
        {
            _rateGateSecurities.WaitToProceed();

            try
            {
                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(HttpUrl + "/spot/currency_pairs").Result;
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                List<CurrencyPair> currencyPairs = JsonConvert.DeserializeAnonymousType<List<CurrencyPair>>(json, new List<CurrencyPair>());

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    UpdateSecurity(currencyPairs);
                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}, {json}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecurity(List<CurrencyPair> currencyPairs)
        {
            for (int i = 0; i < currencyPairs.Count; i++)
            {
                CurrencyPair current = currencyPairs[i];

                if (current.trade_status != "tradable")
                {
                    continue;
                }

                Security security = new Security();
                security.Exchange = nameof(ServerType.GateIoSpot);
                security.State = SecurityStateType.Activ;
                security.Name = current.id;
                security.NameFull = current.id;
                security.NameClass = current.quote;
                security.NameId = current.id;

                security.SecurityType = SecurityType.CurrencyPair;

                security.DecimalsVolume = Int32.Parse(current.amount_precision);
                security.Lot = 1;
                security.Decimals = Int32.Parse(current.precision);
                security.PriceStep = security.Decimals.GetValueByDecimals();
                security.PriceStepCost = security.PriceStep;

                if (current.min_base_amount != null)
                {
                    security.VolumeStep = current.min_base_amount.ToDecimal();
                }

                security.MinTradeAmount = current.min_quote_amount.ToDecimal();
                security.MinTradeAmountType = MinTradeAmountType.C_Currency;

                _securities.Add(security);
            }

            SecurityEvent(_securities);
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public void GetPortfolios()
        {
            CreateQueryPortfolio();
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

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

            if (maxStartTime > startTime)
            {
                SendLogMessage("Maximum interval is 10,000 candles from today!", LogMessageType.Error);
                return null;
            }

            DateTime startTimeData = startTime;
            DateTime partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 500);

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

                startTimeData = partEndTime.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
                partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 500);

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

        private RateGate _rateGateData = new RateGate(2, TimeSpan.FromMilliseconds(100));

        private List<Candle> RequestCandleHistory(string security, string interval, int fromTimeStamp, int toTimeStamp)
        {
            _rateGateData.WaitToProceed();

            try
            {
                string queryParam = $"currency_pair={security}&";
                queryParam += $"interval={interval}&";
                queryParam += $"from={fromTimeStamp}&";
                queryParam += $"to={toTimeStamp}";

                string requestUri = HttpUrl + "/spot/candlesticks?" + queryParam;

                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(requestUri).Result;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;

                    List<string[]> response = JsonConvert.DeserializeAnonymousType(json, new List<string[]>());

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

        private List<Candle> ConvertCandles(List<string[]> rawList)
        {
            List<Candle> candles = new List<Candle>();

            for (int i = 0; i < rawList.Count; i++)
            {
                string[] current = rawList[i];

                Candle candle = new Candle();

                candle.State = CandleState.Finished;
                candle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(int.Parse(current[0]));
                candle.Volume = current[1].ToDecimal();
                candle.Close = current[2].ToDecimal();
                candle.High = current[3].ToDecimal();
                candle.Low = current[4].ToDecimal();
                candle.Open = current[5].ToDecimal();

                candles.Add(candle);
            }

            return candles;
        }

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

                while (firstRange.Time > startTime)
                {
                    int ts = TimeManager.GetTimeStampSecondsToDateTime(firstRange.Time);
                    trades = GetTickDataById(security, long.Parse(firstRange.Id));

                    if (trades.Count == 0)
                    {
                        break;
                    }

                    firstRange = trades[trades.Count - 1];
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
            string queryParam = $"currency_pair={security}&";
            queryParam += "limit=1000&";
            queryParam += $"to={startTimeStamp}";

            string requestUri = HttpUrl + "/spot/trades?" + queryParam;

            return Execute(requestUri);
        }

        private List<Trade> Execute(string requestUri)
        {
            try
            {
                _rateGateData.WaitToProceed();

                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(requestUri).Result;

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;

                    List<ApiEntities> response = JsonConvert.DeserializeAnonymousType(json, new List<ApiEntities>());

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

        private List<Trade> GetTickDataById(string security, long lastId)
        {
            try
            {
                _rateGateData.WaitToProceed();

                string queryParam = $"currency_pair={security}&";
                queryParam += "limit=1000&";
                queryParam += $"last_id={lastId}&";
                queryParam += $"reverse=true";
                string requestUri = HttpUrl + "/spot/trades?" + queryParam;

                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(requestUri).Result;

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;

                    List<ApiEntities> response = JsonConvert.DeserializeAnonymousType(json, new List<ApiEntities>());

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

        private List<Trade> ConvertTrades(List<ApiEntities> tradeResponse)
        {
            List<Trade> trades = new List<Trade>();

            for (int i = 0; i < tradeResponse.Count; i++)
            {
                ApiEntities current = tradeResponse[i];

                Trade trade = new Trade();

                trade.Id = current.id;
                trade.Price = current.price.ToDecimal();
                trade.Volume = current.amount.ToDecimal();
                trade.SecurityNameCode = current.currency_pair;
                trade.Side = current.side == "buy" ? Side.Buy : Side.Sell;

                long timeMs = long.Parse(current.create_time_ms.Split('.')[0]);

                trade.Time = TimeManager.GetDateTimeFromTimeStamp(timeMs);

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

        #endregion

        #region 6 WebSocket creation

        private WebSocket _webSocket;

        private const string WEB_SOCKET_URL = "wss://api.gateio.ws/ws/v4/";

        private void CreateWebSocketConnection()
        {
            _webSocket = new WebSocket(WEB_SOCKET_URL);

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

            if (_myProxy != null)
            {
                NetworkCredential credential = (NetworkCredential)_myProxy.Credentials;
                _webSocket.SetProxy(_myProxy.Address.ToString(), credential.UserName, credential.Password);
            }

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

        private void WebSocket_Error(object sender, ErrorEventArgs e)
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

                if (e.Data.Contains("spot.pong"))
                {
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

                    if (_webSocket != null && _webSocket.ReadyState == WebSocketState.Open)
                    {
                        if (_timeLastSendPing.AddSeconds(20) < DateTime.Now)
                        {
                            SendPing();
                            _timeLastSendPing = DateTime.Now;
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void SendPing()
        {
            FuturesPing ping = new FuturesPing { time = TimeManager.GetUnixTimeStampSeconds(), channel = "spot.ping" };
            string message = JsonConvert.SerializeObject(ping);
            _webSocket.Send(message);
        }

        #endregion

        #region 9 WebSocket security subscrible

        private RateGate _rateGateSubscrible = new RateGate(2, TimeSpan.FromMilliseconds(100));

        public void Subscrible(Security security)
        {
            _rateGateSubscrible.WaitToProceed();

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
                SubscribeUserTrades(security.Name);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void SubscribeMarketDepth(string security)
        {
            AddMarketDepth(security);

            string[] payloadStr = null;

            if (((ServerParameterBool)ServerParameters[9]).Value == true)
            {
                payloadStr = new string[] { security, "20", "100ms" };
            }
            else
            {
                payloadStr = new string[] { security, "5", "100ms" };
            }

            object message = new
            {
                time = TimeManager.GetUnixTimeStampSeconds(),
                channel = "spot.order_book",
                @event = "subscribe",
                payload = payloadStr
            };

            _webSocket.Send(JsonConvert.SerializeObject(message));
        }

        private void AddMarketDepth(string name)
        {
            if (!_allDepths.ContainsKey(name))
            {
                _allDepths.Add(name, new MarketDepth());
            }
        }

        private void SubscribeTrades(string security)
        {
            object message = new
            {
                time = TimeManager.GetUnixTimeStampSeconds(),
                channel = "spot.trades",
                @event = "subscribe",
                payload = new string[] { security }
            };

            _webSocket.Send(JsonConvert.SerializeObject(message));
        }

        private void SubscribeOrders(string security)
        {
            string channel = "spot.orders";
            string eventName = "subscribe";
            long timestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            // GateAPIv4 key pair
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

            object auth = new
            {
                method = "api_key",
                KEY = apiKey,
                SIGN = sign
            };

            object payload = new
            {
                id = timestamp * 1000000,
                time = timestamp,
                channel = channel,
                @event = eventName,
                payload = new string[] { security },
                auth = auth
            };

            string jsonRequest = JsonConvert.SerializeObject(payload);

            _webSocket.Send(jsonRequest);
        }

        private void SubscribeUserTrades(string security)
        {
            string channel = "spot.usertrades";
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

            object auth = new
            {
                method = "api_key",
                KEY = apiKey,
                SIGN = sign
            };

            object payload = new
            {
                id = timestamp * 1000000,
                time = timestamp,
                channel = channel,
                @event = eventName,
                payload = new string[] { security },
                auth = auth
            };

            string jsonRequest = JsonConvert.SerializeObject(payload);

            _webSocket.Send(jsonRequest);
        }

        private void SubscribePortfolio()
        {
            string channel = "spot.balances";
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

            object auth = new
            {
                method = "api_key",
                KEY = apiKey,
                SIGN = sign
            };

            object payload = new
            {
                id = timestamp * 1000000,
                time = timestamp,
                channel = channel,
                @event = eventName,
                auth = auth
            };

            string jsonRequest = JsonConvert.SerializeObject(payload);

            _webSocket.Send(jsonRequest);
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
                        Thread.Sleep(2000);
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

                        if (responseWebsocketMessage.channel.Equals("spot.usertrades") && responseWebsocketMessage.Event.Equals("update"))
                        {
                            UpdateMyTrade(message);
                            continue;
                        }
                        else if (responseWebsocketMessage.channel.Equals("spot.orders") && responseWebsocketMessage.Event.Equals("update"))
                        {
                            UpdateOrder(message);
                        }
                        else if (responseWebsocketMessage.channel.Equals("spot.balances") && responseWebsocketMessage.Event.Equals("update"))
                        {
                            UpdatePortfolio(message);
                            continue;
                        }
                        else if (responseWebsocketMessage.channel.Equals("spot.order_book") && responseWebsocketMessage.Event.Equals("update"))
                        {
                            UpdateDepth(message);
                            continue;
                        }
                        else if (responseWebsocketMessage.channel.Equals("spot.trades") && responseWebsocketMessage.Event.Equals("update"))
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
                ResponseWebsocketMessage<MessagePublicTrades> responseTrades = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<MessagePublicTrades>());

                Trade trade = new Trade();
                trade.SecurityNameCode = responseTrades.result.currency_pair;

                trade.Price = responseTrades.result.price.ToDecimal();
                trade.Id = responseTrades.result.id;
                trade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responseTrades.result.create_time));
                trade.Volume = responseTrades.result.amount.ToDecimal();
                trade.Side = responseTrades.result.side.Equals("sell") ? Side.Sell : Side.Buy;

                NewTradesEvent(trade);
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
                ResponseWebsocketMessage<MessageDepths> responseDepths = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<MessageDepths>());

                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = responseDepths.result.s;

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                for (int i = 0; i < responseDepths.result.asks.Count; i++)
                {
                    ascs.Add(new MarketDepthLevel()
                    {
                        Ask = responseDepths.result.asks[i][1].ToDecimal(),
                        Price = responseDepths.result.asks[i][0].ToDecimal()
                    });
                }

                for (int i = 0; i < responseDepths.result.bids.Count; i++)
                {
                    bids.Add(new MarketDepthLevel()
                    {
                        Bid = responseDepths.result.bids[i][1].ToDecimal(),
                        Price = responseDepths.result.bids[i][0].ToDecimal()
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
                ResponseWebsocketMessage<List<MessageUserTrade>> responseMyTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<List<MessageUserTrade>>());

                for (int i = 0; i < responseMyTrade.result.Count; i++)
                {
                    string security = responseMyTrade.result[i].currency_pair;

                    long time = Convert.ToInt64(responseMyTrade.result[i].create_time);

                    MyTrade newTrade = new MyTrade();

                    newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(time);
                    newTrade.SecurityNameCode = security;
                    newTrade.NumberOrderParent = responseMyTrade.result[i].order_id;
                    newTrade.Price = responseMyTrade.result[i].price.ToDecimal();
                    newTrade.NumberTrade = responseMyTrade.result[i].id;
                    newTrade.Side = responseMyTrade.result[i].side.Equals("sell") ? Side.Sell : Side.Buy;

                    string commissionSecName = responseMyTrade.result[i].fee_currency;

                    if (newTrade.SecurityNameCode.StartsWith(commissionSecName))
                    {
                        newTrade.Volume = responseMyTrade.result[i].amount.ToDecimal() - responseMyTrade.result[i].fee.ToDecimal();
                        int decimalVolum = GetDecimalsVolume(security);
                        if (decimalVolum > 0)
                        {
                            newTrade.Volume = Math.Floor(newTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                        }
                    }
                    else
                    {
                        newTrade.Volume = responseMyTrade.result[i].amount.ToDecimal();
                    }

                    MyTradeEvent(newTrade);
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private int GetDecimalsVolume(string security)
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

        private void UpdateOrder(string message)
        {
            try
            {
                ResponseWebsocketMessage<List<MessageUserOrder>> responseOrders = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<List<MessageUserOrder>>());

                for (int i = 0; i < responseOrders.result.Count; i++)
                {
                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = responseOrders.result[i].currency_pair;
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responseOrders.result[i].create_time));

                    OrderStateType orderState = OrderStateType.None;

                    if (responseOrders.result[i].Event.Equals("put"))
                    {
                        orderState = OrderStateType.Active;
                    }
                    else if (responseOrders.result[i].Event.Equals("update"))
                    {
                        orderState = OrderStateType.Partial;
                    }
                    else
                    {
                        if (responseOrders.result[i].finish_as.Equals("cancelled"))
                        {
                            orderState = OrderStateType.Cancel;
                        }
                        else
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
                    newOrder.Side = responseOrders.result[i].side.Equals("buy") ? Side.Buy : Side.Sell;

                    if (responseOrders.result[i].type == "market")
                    {
                        newOrder.TypeOrder = OrderPriceType.Market;
                    }
                    if (responseOrders.result[i].type == "limit")
                    {
                        newOrder.TypeOrder = OrderPriceType.Limit;
                    }

                    newOrder.State = orderState;
                    newOrder.Volume = responseOrders.result[i].amount.Replace('.', ',').ToDecimal();
                    newOrder.Price = responseOrders.result[i].price.Replace('.', ',').ToDecimal();
                    newOrder.ServerType = ServerType.GateIoSpot;
                    newOrder.PortfolioNumber = "GateIO_Spot";

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
                ResponseWebsocketMessage<List<CurrencyBalance>> responsePortfolio = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<List<CurrencyBalance>>());

                for (int i = 0; i < responsePortfolio.result.Count; i++)
                {
                    CurrencyBalance current = responsePortfolio.result[i];

                    PositionOnBoard positionOnBoard = new PositionOnBoard();

                    positionOnBoard.SecurityNameCode = current.currency;
                    positionOnBoard.ValueBegin = Math.Round(current.total.ToDecimal(), 5);
                    positionOnBoard.ValueCurrent = Math.Round(current.available.ToDecimal(), 5);
                    positionOnBoard.ValueBlocked = Math.Round(current.freeze.ToDecimal(), 5);

                    _myPortfolio.SetNewPosition(positionOnBoard);
                }

                PortfolioEvent(new List<Portfolio> { _myPortfolio });
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

        private readonly RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private readonly RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(20));

        private readonly RateGate _rateGateGetOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string side = order.Side == Side.Buy ? "buy" : "sell";
                string secName = order.SecurityNameCode;
                string price = order.Price.ToString().Replace(",", ".");
                string volume = order.Volume.ToString().Replace(",", ".");

                string method = "POST";
                string url = "/spot/orders";
                string query_param = "";

                string bodyParam = $"{{\"text\":\"t-{order.NumberUser}\",\"currency_pair\":" +
                                    $"\"{secName}\",\"type\":\"limit\",\"account\":\"spot\",\"side\":\"{side}\",\"iceberg\":\"0\",\"amount\":\"{volume}\",\"price\":\"{price}\",\"time_in_force\":\"gtc\"}}";

                string bodyHash = GetHash(bodyParam);

                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();

                string signString = $"{method}\n{_prefix}{url}\n{query_param}\n{bodyHash}\n{timeStamp}";
                string sign = GetHashHMAC(signString, _secretKey);

                string fullUrl = $"{_host}{_prefix}{url}";

                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(Method.POST);
                request.AddHeader("KEY", _publicKey);
                request.AddHeader("SIGN", sign);
                request.AddHeader("Timestamp", timeStamp);
                request.AddHeader("X-Gate-Channel-Id", "osa");
                request.AddParameter("application/json", bodyParam, ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

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
                string method = "DELETE";
                string url = $"/spot/orders/{order.NumberMarket}";
                string queryParam = $"currency_pair={order.SecurityNameCode}";
                string bodyParam = "";
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                string bodyHash = GetHash(bodyParam);
                string signString = $"{method}\n{_prefix}{url}\n{queryParam}\n{bodyHash}\n{timeStamp}";
                string sign = GetHashHMAC(signString, _secretKey);
                string fullUrl = $"{_host}{_prefix}{url}?{queryParam}";

                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(Method.DELETE);
                request.AddHeader("KEY", _publicKey);
                request.AddHeader("SIGN", sign);
                request.AddHeader("Timestamp", timeStamp);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    GetOrderStatus(order);
                    SendLogMessage($"CancelOrder. Error: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllActivOrdersFromExchange();

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

        private List<Order> GetAllActivOrdersFromExchange()
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string method = "GET";
                string url = $"/spot/open_orders";
                string queryParam = "";
                string bodyParam = "";
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                string bodyHash = GetHash(bodyParam);
                string signString = $"{method}\n{_prefix}{url}\n{queryParam}\n{bodyHash}\n{timeStamp}";
                string sign = GetHashHMAC(signString, _secretKey);
                string fullUrl = $"{_host}{_prefix}{url}?{queryParam}";

                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(Method.GET);
                request.AddHeader("KEY", _publicKey);
                request.AddHeader("SIGN", sign);
                request.AddHeader("Timestamp", timeStamp);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    List<OrderResponse> responseOrders = JsonConvert.DeserializeObject<List<OrderResponse>>(responseMessage.Content);

                    List<Order> orders = new List<Order>();

                    for (int i = 0; i < responseOrders.Count; i++)
                    {
                        List<MessageUserOrder> itemOrders = responseOrders[i].orders;

                        for (int j = 0; j < itemOrders.Count; j++)
                        {
                            if (itemOrders[j].status != "open")
                            {
                                continue;
                            }

                            Order newOrder = new Order();
                            newOrder.SecurityNameCode = itemOrders[j].currency_pair;
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(itemOrders[j].create_time));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(itemOrders[j].create_time));

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(itemOrders[j].text.Replace("t-", ""));
                            }
                            catch
                            {
                                // ignore
                            }

                            newOrder.NumberMarket = itemOrders[j].id;
                            newOrder.Side = itemOrders[j].side.Equals("buy") ? Side.Buy : Side.Sell;

                            if (itemOrders[j].type == "market")
                            {
                                newOrder.TypeOrder = OrderPriceType.Market;
                            }
                            if (itemOrders[j].type == "limit")
                            {
                                newOrder.TypeOrder = OrderPriceType.Limit;
                            }

                            newOrder.State = OrderStateType.Active;
                            newOrder.Volume = itemOrders[j].amount.Replace('.', ',').ToDecimal();
                            newOrder.Price = itemOrders[j].price.Replace('.', ',').ToDecimal();
                            newOrder.ServerType = ServerType.GateIoSpot;
                            newOrder.PortfolioNumber = "GateIO_Spot";

                            orders.Add(newOrder);
                        }
                    }

                    return orders;
                }
                else
                {
                    SendLogMessage($"Get Activ Order. Error: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        public void GetOrderStatus(Order order)
        {
            List<Order> orderFromExchange = GetAllActivOrdersFromExchange();  //GetOrderFromExchange(order.SecurityNameCode, order.NumberMarket, order.NumberUser, order.State);

            if (orderFromExchange == null
                || orderFromExchange.Count == 0)
            {
                orderFromExchange = GetOrderFromExchange(order.SecurityNameCode);
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
                FindMyTradesToOrder(order.SecurityNameCode, order.NumberUser);
            }
        }

        private List<Order> GetOrderFromExchange(string securityNameCode)
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string state = "finished";

                string method = "GET";

                string queryParam = $"currency_pair={securityNameCode}&";
                queryParam += $"status={state}";

                string url = $"/spot/orders";

                string bodyParam = "";
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                string bodyHash = GetHash(bodyParam);
                string signString = $"{method}\n{_prefix}{url}\n{queryParam}\n{bodyHash}\n{timeStamp}";
                string sign = GetHashHMAC(signString, _secretKey);
                string fullUrl = $"{_host}{_prefix}{url}?{queryParam}";

                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(Method.GET);
                request.AddHeader("KEY", _publicKey);
                request.AddHeader("SIGN", sign);
                request.AddHeader("Timestamp", timeStamp);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    List<MessageUserOrder> responseOrders = JsonConvert.DeserializeObject<List<MessageUserOrder>>(responseMessage.Content);

                    List<Order> orders = new List<Order>();

                    for (int i = 0; i < responseOrders.Count; i++)
                    {
                        Order newOrder = new Order();
                        newOrder.SecurityNameCode = responseOrders[i].currency_pair;
                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responseOrders[i].create_time));
                        newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responseOrders[i].create_time));

                        try
                        {
                            newOrder.NumberUser = Convert.ToInt32(responseOrders[i].text.Replace("t-", ""));
                        }
                        catch
                        {
                            // ignore
                        }

                        newOrder.NumberMarket = responseOrders[i].id;
                        newOrder.Side = responseOrders[i].side.Equals("buy") ? Side.Buy : Side.Sell;

                        if (responseOrders[i].type == "market")
                        {
                            newOrder.TypeOrder = OrderPriceType.Market;
                        }
                        if (responseOrders[i].type == "limit")
                        {
                            newOrder.TypeOrder = OrderPriceType.Limit;
                        }

                        OrderStateType orderState = OrderStateType.None;

                        if (responseOrders[i].status.Equals("open"))
                        {
                            orderState = OrderStateType.Active;
                        }
                        else if (responseOrders[i].status.Equals("cancelled"))
                        {
                            orderState = OrderStateType.Cancel;
                        }
                        else if (responseOrders[i].status.Equals("closed"))
                        {
                            orderState = OrderStateType.Done;
                        }
                        else
                        {
                            orderState = OrderStateType.None;
                        }

                        newOrder.State = orderState;
                        newOrder.Volume = responseOrders[i].amount.Replace('.', ',').ToDecimal();
                        newOrder.Price = responseOrders[i].price.Replace('.', ',').ToDecimal();
                        newOrder.ServerType = ServerType.GateIoSpot;
                        newOrder.PortfolioNumber = "GateIO_Spot";

                        orders.Add(newOrder);
                    }
                    return orders;
                }
                else
                {
                    SendLogMessage($"Get status Order. Error: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private void FindMyTradesToOrder(string nameSecurity, int numberUser)
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string method = "GET";
                string queryParam = $"currency_pair={nameSecurity}";
                string url = $"/spot/my_trades";

                string bodyParam = "";
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                string bodyHash = GetHash(bodyParam);
                string signString = $"{method}\n{_prefix}{url}\n{queryParam}\n{bodyHash}\n{timeStamp}";
                string sign = GetHashHMAC(signString, _secretKey);
                string fullUrl = $"{_host}{_prefix}{url}?{queryParam}";

                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(Method.GET);
                request.AddHeader("KEY", _publicKey);
                request.AddHeader("SIGN", sign);
                request.AddHeader("Timestamp", timeStamp);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    List<MessageUserTrade> responseMyTrade = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new List<MessageUserTrade>());

                    for (int i = 0; i < responseMyTrade.Count; i++)
                    {
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
                            string security = responseMyTrade[i].currency_pair;

                            long time = Convert.ToInt64(responseMyTrade[i].create_time);

                            MyTrade newTrade = new MyTrade();

                            newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(time);
                            newTrade.SecurityNameCode = security;
                            newTrade.NumberOrderParent = responseMyTrade[i].order_id;
                            newTrade.Price = responseMyTrade[i].price.ToDecimal();
                            newTrade.NumberTrade = responseMyTrade[i].id;
                            newTrade.Side = responseMyTrade[i].side.Equals("sell") ? Side.Sell : Side.Buy;

                            string commissionSecName = responseMyTrade[i].fee_currency;

                            if (newTrade.SecurityNameCode.StartsWith(commissionSecName))
                            {
                                newTrade.Volume = responseMyTrade[i].amount.ToDecimal() - responseMyTrade[i].fee.ToDecimal();
                                int decimalVolum = GetDecimalsVolume(security);
                                if (decimalVolum > 0)
                                {
                                    newTrade.Volume = Math.Floor(newTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                                }
                            }
                            else
                            {
                                newTrade.Volume = responseMyTrade[i].amount.ToDecimal();
                            }

                            MyTradeEvent(newTrade);
                        }
                    }
                }
                else
                {
                    SendLogMessage($"Get status Order. Error: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 12 Queries

        private const string HttpUrl = "https://api.gateio.ws/api/v4";

        private HttpClient _httpPublicClient;

        private readonly Dictionary<string, Security> _subscribedSecurities = new Dictionary<string, Security>();

        private RateGate _rateGatePortfolio = new RateGate(2, TimeSpan.FromMilliseconds(250));

        private void CreateQueryPortfolio()
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                string method = "GET";
                string url = "/spot/accounts";
                string query_param = "";
                string bodyParam = "";

                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                string bodyHash = GetHash(bodyParam);
                string signString = $"{method}\n{_prefix}{url}\n{query_param}\n{bodyHash}\n{timeStamp}";
                string sign = GetHashHMAC(signString, _secretKey);

                string fullUrl = $"{_host}{_prefix}{url}";

                _httpPublicClient.DefaultRequestHeaders.TryAddWithoutValidation("Timestamp", timeStamp);
                _httpPublicClient.DefaultRequestHeaders.TryAddWithoutValidation("KEY", _publicKey);
                _httpPublicClient.DefaultRequestHeaders.TryAddWithoutValidation("SIGN", sign);

                HttpResponseMessage response = _httpPublicClient.GetAsync(fullUrl).Result;
                string responseContent = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    List<GetCurrencyVolumeResponse> getCurrencyVolumeResponse = JsonConvert.DeserializeAnonymousType(responseContent, new List<GetCurrencyVolumeResponse>());

                    UpdatePortfolio(getCurrencyVolumeResponse);
                }
                else
                {
                    SendLogMessage($"CreateQueryPortfolio> Http State Code: {response.StatusCode}, {responseContent}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private Portfolio _myPortfolio;

        private void UpdatePortfolio(List<GetCurrencyVolumeResponse> getCurrencyVolumeResponse)
        {
            try
            {
                _myPortfolio = new Portfolio();

                _myPortfolio.Number = "GateIO_Spot";
                _myPortfolio.ValueBegin = 1;
                _myPortfolio.ValueCurrent = 1;

                if (getCurrencyVolumeResponse == null || getCurrencyVolumeResponse.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < getCurrencyVolumeResponse.Count; i++)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = getCurrencyVolumeResponse[i].currency;
                    newPortf.ValueBegin = Math.Round(getCurrencyVolumeResponse[i].available.ToDecimal(), 5);
                    newPortf.ValueCurrent = Math.Round(getCurrencyVolumeResponse[i].available.ToDecimal(), 5);
                    newPortf.ValueBlocked = Math.Round(getCurrencyVolumeResponse[i].locked.ToDecimal(), 5);
                    _myPortfolio.SetNewPosition(newPortf);
                }

                PortfolioEvent(new List<Portfolio> { _myPortfolio });
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private string GetHash(string input)
        {
            using (SHA512 sha512 = SHA512.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = sha512.ComputeHash(inputBytes);
                string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                return hash;
            }
        }

        private string GetHashHMAC(string input, string key)
        {
            using (HMACSHA512 hmacsha512 = new HMACSHA512(Encoding.ASCII.GetBytes(key)))
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = hmacsha512.ComputeHash(inputBytes);
                string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                return hash;
            }
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