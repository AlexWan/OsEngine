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
using OsEngine.Market.Servers.GateIo.Futures.Request;
using OsEngine.Market.Servers.GateIo.GateIoSpot.Entities;
using SuperSocket.ClientEngine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocket4Net;

namespace OsEngine.Market.Servers.GateIo.GateIoSpot
{
    public class GateIoServerSpot : AServer
    {
        public GateIoServerSpot()
        {
            ServerRealization = new GateIoServerSpotRealization();
            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
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

        public void Connect()
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            HttpResponseMessage responseMessage = null;

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
                throw new Exception("Connection can`t be open. GateIo. Error request");
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
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
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

        public void GetSecurities()
        {
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
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecurity(List<CurrencyPair> currencyPairs)
        {
            List<Security> securities = new List<Security>();

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
                security.Lot = security.DecimalsVolume.GetValueByDecimals();
                security.Decimals = Int32.Parse(current.precision);
                security.PriceStep = security.Decimals.GetValueByDecimals();
                security.PriceStepCost = security.PriceStep;

                if (current.min_base_amount != null)
                {
                    security.MinTradeAmount = current.min_base_amount.ToDecimal();
                }

                securities.Add(security);
            }

            SecurityEvent(securities);
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

                Candle last = candles.Last();

                if (last.TimeStart >= endTime)
                {
                    allCandles.AddRange(candles.Where(c => c.TimeStart <= endTime));
                    break;
                }

                allCandles.AddRange(candles);

                startTimeData = partEndTime.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
                partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 500);

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

        private readonly RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<Candle> RequestCandleHistory(string security, string interval, int fromTimeStamp, int toTimeStamp)
        {
            _rgCandleData.WaitToProceed(100);

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
                    trades = GetTickDataById(security, long.Parse(firstRange.Id));

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
            string queryParam = $"currency_pair={security}&";
            queryParam += "limit=1000&";
            queryParam += $"to={startTimeStamp}";

            string requestUri = HttpUrl + "/spot/trades?" + queryParam;

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
                _rgTickData.WaitToProceed(100);

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

        private void WebSocket_Error(object sender, ErrorEventArgs e)
        {
            if (e.Exception != null)
            {
                SendLogMessage(e.Exception.ToString(),LogMessageType.Error);
            }
        }

        private void WebSocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
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
                    if(ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        _timeLastSendPing = DateTime.Now;
                        Thread.Sleep(1000);
                        continue;
                    }

                    Thread.Sleep(3000);

                    if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                    {
                        if (_timeLastSendPing.AddSeconds(30) < DateTime.Now)
                        {
                            SendPing();
                            _timeLastSendPing = DateTime.Now;
                        }
                    }
                }
                catch(Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void SendPing()
        {
            FuturesPing ping = new FuturesPing { Time = TimeManager.GetUnixTimeStampSeconds(), Channel = "spot.ping" };
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
                SubscribeUserTrades(security.Name);
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private void SubscribeMarketDepth(string security)
        {
            AddMarketDepth(security);

            object message = new
            {
                time = TimeManager.GetUnixTimeStampSeconds(),
                channel = "spot.order_book",
                @event = "subscribe",
                payload = new string[] { security, "20", "100ms" }
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

            JObject authObject = new JObject {
            { "method", "api_key" },
            { "KEY", apiKey },
            { "SIGN", sign }
        };

            JObject payloadObject = new JObject {
            { "id", timestamp * 1000000 },
            { "time", timestamp },
            { "channel", channel },
            { "event", eventName },
            { "payload", new JArray { security } },
            { "auth", authObject }
        };

            string jsonRequest = JsonConvert.SerializeObject(payloadObject);

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

            JObject authObject = new JObject {
            { "method", "api_key" },
            { "KEY", apiKey },
            { "SIGN", sign }
        };

            JObject payloadObject = new JObject {
            { "id", timestamp * 1000000 },
            { "time", timestamp },
            { "channel", channel },
            { "event", eventName },
            { "payload", new JArray { security } },
            { "auth", authObject }
        };

            string jsonRequest = JsonConvert.SerializeObject(payloadObject);

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

            JObject authObject = new JObject {
                { "method", "api_key" },
                { "KEY", apiKey },
                { "SIGN", sign }
            };

            JObject payloadObject = new JObject {
                { "id", timestamp * 1000000 },
                { "time", timestamp },
                { "channel", channel },
                { "event", eventName },
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
                        Thread.Sleep(2000);
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
                catch (Exception exeption)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            ResponceWebsocketMessage<MessagePublicTrades> responceTrades = JsonConvert.DeserializeAnonymousType(message, new ResponceWebsocketMessage<MessagePublicTrades>());

            Trade trade = new Trade();
            trade.SecurityNameCode = responceTrades.result.currency_pair;

            trade.Price = Convert.ToDecimal(responceTrades.result.price.Replace('.', ','));
            trade.Id = responceTrades.result.id;
            trade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responceTrades.result.create_time));
            trade.Volume = Convert.ToDecimal(responceTrades.result.amount.Replace('.', ','));
            trade.Side = responceTrades.result.side.Equals("sell") ? Side.Sell : Side.Buy;

            NewTradesEvent(trade);
        }

        private readonly Dictionary<string, MarketDepth> _allDepths = new Dictionary<string, MarketDepth>();

        private void UpdateDepth(string message)
        {
            ResponceWebsocketMessage<MessageDepths> responceDepths = JsonConvert.DeserializeAnonymousType(message, new ResponceWebsocketMessage<MessageDepths>());

            try
            {
                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = responceDepths.result.s;

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                for (int i = 0; i < responceDepths.result.asks.Count; i++)
                {
                    ascs.Add(new MarketDepthLevel()
                    {
                        Ask = responceDepths.result.asks[i][1].ToDecimal(),
                        Price = responceDepths.result.asks[i][0].ToDecimal()
                    });
                }

                for (int i = 0; i < responceDepths.result.bids.Count; i++)
                {
                    bids.Add(new MarketDepthLevel()
                    {
                        Bid = responceDepths.result.bids[i][1].ToDecimal(),
                        Price = responceDepths.result.bids[i][0].ToDecimal()
                    });
                }

                depth.Asks = ascs;
                depth.Bids = bids;

                depth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responceDepths.result.t));

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
            ResponceWebsocketMessage<List<MessageUserTrade>> responceDepths = JsonConvert.DeserializeAnonymousType(message, new ResponceWebsocketMessage<List<MessageUserTrade>>());

            for (int i = 0; i < responceDepths.result.Count; i++)
            {
                string security = responceDepths.result[i].currency_pair;

                long time = Convert.ToInt64(responceDepths.result[i].create_time);

                MyTrade newTrade = new MyTrade();

                newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(time);
                newTrade.SecurityNameCode = security;
                newTrade.NumberOrderParent = responceDepths.result[i].order_id;
                newTrade.Price = responceDepths.result[i].price.ToDecimal();
                newTrade.NumberTrade = responceDepths.result[i].id;
                newTrade.Side = responceDepths.result[i].side.Equals("sell") ? Side.Sell : Side.Buy;
                newTrade.Volume = responceDepths.result[i].amount.ToDecimal();
                MyTradeEvent(newTrade);
            }
        }

        private void UpdateOrder(string message)
        {
            ResponceWebsocketMessage<List<MessageUserOrder>> responceDepths = JsonConvert.DeserializeAnonymousType(message, new ResponceWebsocketMessage<List<MessageUserOrder>>());

            for (int i = 0; i < responceDepths.result.Count; i++)
            {
                Order newOrder = new Order();
                newOrder.SecurityNameCode = responceDepths.result[i].currency_pair;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responceDepths.result[i].create_time));

                OrderStateType orderState = OrderStateType.None;

                if (responceDepths.result[i].Event.Equals("put"))
                {
                    orderState = OrderStateType.Activ;
                }
                else if (responceDepths.result[i].Event.Equals("update"))
                {
                    orderState = OrderStateType.Patrial;
                }
                else
                {
                    if (responceDepths.result[i].finish_as.Equals("cancelled"))
                    {
                        orderState = OrderStateType.Cancel;
                    }
                    else
                    {
                        orderState = OrderStateType.Done;
                    }
                }
                newOrder.NumberUser = Convert.ToInt32(responceDepths.result[i].text.Replace("t-", ""));
                newOrder.NumberMarket = responceDepths.result[i].id;
                newOrder.Side = responceDepths.result[i].side.Equals("buy") ? Side.Buy : Side.Sell;
                newOrder.State = orderState;
                newOrder.Volume = responceDepths.result[i].amount.Replace('.', ',').ToDecimal();
                newOrder.Price = responceDepths.result[i].price.Replace('.', ',').ToDecimal();
                newOrder.ServerType = ServerType.GateIoSpot;
                newOrder.PortfolioNumber = "GateIO_Spot";

                MyOrderEvent(newOrder);
            }
        }

        private void UpdatePortfolio(string message)
        {
            ResponceWebsocketMessage<List<CurrencyBalance>> responsePortfolio = JsonConvert.DeserializeAnonymousType(message, new ResponceWebsocketMessage<List<CurrencyBalance>>());

            for (int i = 0; i < responsePortfolio.result.Count; i++)
            {
                CurrencyBalance current = responsePortfolio.result[i];

                PositionOnBoard positionOnBoard = new PositionOnBoard();

                positionOnBoard.SecurityNameCode = current.Currency;
                positionOnBoard.ValueBegin = current.Total.ToDecimal();
                positionOnBoard.ValueCurrent = current.Available.ToDecimal();
                positionOnBoard.ValueBlocked = current.Freeze.ToDecimal();

                _myPortfolio.SetNewPosition(positionOnBoard);
            }

            PortfolioEvent(new List<Portfolio> { _myPortfolio });
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
            _rgSendOrder.WaitToProceed(100);

            string side = order.Side == Side.Buy ? "buy" : "sell";
            string secName = order.SecurityNameCode;
            string price = order.Price.ToString().Replace(",", ".");
            string volume = order.Volume.ToString().Replace(",", ".");

            string key = _publicKey;
            string secret = _secretKey;
           
            string method = "POST";
            string url = "/spot/orders";
            string query_param = "";

            string bodyParam = $"{{\"text\":\"t-{order.NumberUser}\",\"currency_pair\":" +
                                $"\"{secName}\",\"type\":\"limit\",\"account\":\"spot\",\"side\":\"{side}\",\"iceberg\":\"0\",\"amount\":\"{volume}\",\"price\":\"{price}\",\"time_in_force\":\"gtc\"}}";

            string bodyHash = GetHash(bodyParam);

            string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();

            string signString = $"{method}\n{_prefix}{url}\n{query_param}\n{bodyHash}\n{timeStamp}";
            string sign = GetHashHMAC(signString, secret);
            
            string fullUrl = $"{_host}{_prefix}{url}";

            HttpResponseMessage response;

            using (HttpClient client = new HttpClient())
            {
                StringContent content = new StringContent(bodyParam, Encoding.UTF8, "application/json");

                client.DefaultRequestHeaders.Add("KEY", key);
                client.DefaultRequestHeaders.Add("SIGN", sign);
                client.DefaultRequestHeaders.Add("Timestamp", timeStamp);

                response = client.PostAsync(fullUrl, content).Result;
            }

            string responseString = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode != HttpStatusCode.Created)
            {
                SendLogMessage(responseString, LogMessageType.Trade);
                order.State = OrderStateType.Fail;
                MyOrderEvent.Invoke(order);
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

            string method = "DELETE";
            string url = $"/spot/orders/{order.NumberMarket}";
            string queryParam = $"currency_pair={order.SecurityNameCode}";
            string bodyParam = "";
            string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
            string bodyHash = GetHash(bodyParam);
            string signString = $"{method}\n{_prefix}{url}\n{queryParam}\n{bodyHash}\n{timeStamp}";
            string sign = GetHashHMAC(signString, _secretKey);
            string fullUrl = $"{_host}{_prefix}{url}?{queryParam}";

            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Timestamp", timeStamp);
                httpClient.DefaultRequestHeaders.Add("KEY", _publicKey);
                httpClient.DefaultRequestHeaders.Add("SIGN", sign);

                HttpResponseMessage response = httpClient.DeleteAsync(fullUrl).Result;
                string responseBody = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage(responseBody, LogMessageType.Error);
                }
            }
        }

        public void GetAllActivOrders()
        {

        }

        public void GetOrderStatus(Order order)
        {

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

        #region 12 Queries

        private const string HttpUrl = "https://api.gateio.ws/api/v4";

        private readonly HttpClient _httpPublicClient = new HttpClient();

        private readonly Dictionary<string, Security> _subscribedSecurities = new Dictionary<string, Security>();

        private void CreateQueryPortfolio()
        {
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

                string responseContent;

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Timestamp", timeStamp);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("KEY", _publicKey);
                    client.DefaultRequestHeaders.TryAddWithoutValidation("SIGN", sign);

                    HttpResponseMessage response = client.GetAsync(fullUrl).Result;
                    responseContent = response.Content.ReadAsStringAsync().Result;
                }

                List<GetCurrencyVolumeResponce> getCurrencyVolumeResponse = JsonConvert.DeserializeAnonymousType(responseContent, new List<GetCurrencyVolumeResponce>());

                UpdatePortfolio(getCurrencyVolumeResponse);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private Portfolio _myPortfolio;

        private void UpdatePortfolio(List<GetCurrencyVolumeResponce> getCurrencyVolumeResponse)
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
                    newPortf.ValueBegin = getCurrencyVolumeResponse[i].available.ToDecimal();
                    newPortf.ValueCurrent = getCurrencyVolumeResponse[i].available.ToDecimal();
                    newPortf.ValueBlocked = getCurrencyVolumeResponse[i].locked.ToDecimal();
                    _myPortfolio.SetNewPosition(newPortf);
                }

                PortfolioEvent(new List<Portfolio> { _myPortfolio });
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
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