using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WebSocket4Net;
using OsEngine.Market.Servers.Woo.Entity;
using System.Security.Cryptography;
using System.Collections;
using RestSharp;

namespace OsEngine.Market.Servers.Woo
{
    public class WooServer : AServer
    {
        public WooServer()
        {
            WooServerRealization realization = new WooServerRealization();
            ServerRealization = realization;

            CreateParameterString("Api Key", "");
            CreateParameterString("Secret Key", "");
            CreateParameterString("Application ID", "");
        }
    }

    public class WooServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public WooServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.IsBackground = true;
            threadMessageReaderPublic.Name = "MessageReaderPublicWoo";
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.IsBackground = true;
            threadMessageReaderPrivate.Name = "MessageReaderPrivateWoo";
            threadMessageReaderPrivate.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            _apiKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterString)ServerParameters[1]).Value;
            _appID = ((ServerParameterString)ServerParameters[2]).Value;

            string url = $"{_baseUrl}/v1/public/system_info";
            RestClient client = new RestClient(url);
            RestRequest request = new RestRequest(Method.GET);
            IRestResponse responseMessage = client.Execute(request);
  
            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                try
                {
                    _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                    _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
                    CreateWebSocketConnection();
                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    SendLogMessage("Connection can be open. Woo. Error request", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            else
            {
                SendLogMessage("Connection can be open. Woo. Error request", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
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

            _subscribledSecurities.Clear();
            _arrayChannelsAccount.Clear();
            _arrayChannelsBook.Clear();
            _arrayChannelsTrade.Clear();

            try
            {
                DeleteWebscoektConnection();
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }

            _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

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

        private string _baseUrl = "https://api.woo.org";

        private string _webSocketUrlPublic = "wss://wss.woo.org/ws/stream/";
               
        private string _webSocketUrlPrivate = "wss://wss.woo.org/v2/ws/private/stream/";

        private int _limitCandles = 100;

        private List<string> _arrayChannelsBook = new List<string>();
        
        private List<string> _arrayChannelsTrade = new List<string>();
        
        private List<string> _arrayChannelsAccount = new List<string>();

        private List<string> _arrayChannels = new List<string>();

        private List<string> _listPrivateChannel = new List<string> { "executionreport", "position", "balance" };

        private ConcurrentQueue<string> _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        #endregion

        #region 3 Securities

        public void GetSecurities() 
        {
            try
            {      
                string url = $"{_baseUrl}/v1/public/info";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);                
                IRestResponse responseMessage = client.Execute(request);                
                string JsonResponse = responseMessage.Content;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    UpdateSecurity(JsonResponse);
                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.Error);
                }
               
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecurity(string json)
        {
            ResponseMessageSecurities response = JsonConvert.DeserializeObject<ResponseMessageSecurities>(json); ;

            List<Security> securities = new List<Security>();

            for (int i = 0; i < response.rows.Count; i++)
            {
                ResponseMessageSecurities.Rows item = response.rows[i];

                if (item.status == "TRADING")
                {   
                    Security newSecurity = new Security();

                    newSecurity.Exchange = ServerType.Woo.ToString();
                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.symbol;
                    newSecurity.NameClass = item.symbol.StartsWith("SPOT") ? "Spot" : "Futures";
                    newSecurity.NameId = item.symbol;
                    newSecurity.SecurityType = item.symbol.StartsWith("SPOT") ? SecurityType.CurrencyPair : SecurityType.Futures;
                    newSecurity.DecimalsVolume = item.base_min.DecimalsCount();
                    newSecurity.Lot = item.base_min.ToDecimal();
                    newSecurity.PriceStep = item.quote_tick.ToDecimal();
                    newSecurity.Decimals = item.quote_tick.DecimalsCount();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.State = SecurityStateType.Activ;
                    newSecurity.MinTradeAmount = item.base_min.ToDecimal();

                    securities.Add(newSecurity);
                }
            }
            SecurityEvent(securities);
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(6000));
        
        private RateGate _rateGatePositions = new RateGate(1, TimeSpan.FromMilliseconds(6000));
        
        public void GetPortfolios()
        {          
            CreateQueryPortfolio(true);           
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

        private WebSocket _webSocketPublic;

        private WebSocket _webSocketPrivate;

        private void CreateWebSocketConnection()
        {            
            _webSocketPublic = new WebSocket(_webSocketUrlPublic + _appID);           
            _webSocketPublic.Opened += webSocketPublic_Opened;
            _webSocketPublic.Closed += webSocketPublic_Closed;
            _webSocketPublic.MessageReceived += webSocketPublic_MessageReceived;
            _webSocketPublic.Error += webSocketPublic_Error;

            _webSocketPublic.Open();

            _webSocketPrivate = new WebSocket(_webSocketUrlPrivate + _appID);
            _webSocketPrivate.Opened += webSocketPrivate_Opened;
            _webSocketPrivate.Closed += webSocketPrivate_Closed;
            _webSocketPrivate.MessageReceived += webSocketPrivate_MessageReceived;
            _webSocketPrivate.Error += webSocketPrivate_Error;

            _webSocketPrivate.Open();

        }

        private void DeleteWebscoektConnection()
        {
            if (_webSocketPublic != null)
            {
                try
                {
                    _webSocketPublic.Close();
                }
                catch
                {
                    // ignore
                }

                _webSocketPublic.Opened -= webSocketPublic_Opened;
                _webSocketPublic.Closed -= webSocketPublic_Closed;
                _webSocketPublic.MessageReceived -= webSocketPublic_MessageReceived;
                _webSocketPublic.Error -= webSocketPublic_Error;
                _webSocketPublic = null;
            }

            if (_webSocketPrivate != null)
            {
                try
                {
                    _webSocketPrivate.Close();
                }
                catch
                {
                    // ignore
                }

                _webSocketPrivate.Opened -= webSocketPrivate_Opened;
                _webSocketPrivate.Closed -= webSocketPrivate_Closed;
                _webSocketPrivate.MessageReceived -= webSocketPrivate_MessageReceived;
                _webSocketPrivate.Error -= webSocketPrivate_Error;
                _webSocketPrivate = null;
            }
        }

        #endregion

        #region 7 WebSocket events

        private void webSocketPublic_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            SuperSocket.ClientEngine.ErrorEventArgs error = (SuperSocket.ClientEngine.ErrorEventArgs)e;
            if (error.Exception != null)
            {
                SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void webSocketPublic_MessageReceived(object sender, MessageReceivedEventArgs e)
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

                if (string.IsNullOrEmpty(e.Message))
                {
                    return;
                }

                if (_FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }
                
                _FIFOListWebSocketPublicMessage.Enqueue(e.Message);
                                
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void webSocketPublic_Closed(object sender, EventArgs e)
        {
            if (DisconnectEvent != null && ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by Woo. WebSocket Public Closed Event", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void webSocketPublic_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Connection Websocket Public Open", LogMessageType.System);
            if (ServerStatus != ServerConnectStatus.Connect 
                && _webSocketPublic != null
                && _webSocketPublic.State == WebSocketState.Open)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
                      
        }

        private void webSocketPrivate_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            SuperSocket.ClientEngine.ErrorEventArgs error = (SuperSocket.ClientEngine.ErrorEventArgs)e;
            if (error.Exception != null)
            {
                SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void webSocketPrivate_MessageReceived(object sender, MessageReceivedEventArgs e)
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

                if (string.IsNullOrEmpty(e.Message))
                {
                    return;
                }

                if (_FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                _FIFOListWebSocketPrivateMessage.Enqueue(e.Message);

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void webSocketPrivate_Closed(object sender, EventArgs e)
        {
            if (DisconnectEvent != null && ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by Woo. WebSocket Private Closed Event", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void webSocketPrivate_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Connection Websocket Private Open", LogMessageType.System);
            
            if (ServerStatus != ServerConnectStatus.Connect
                && _webSocketPrivate != null
                && _webSocketPrivate.State == WebSocketState.Open)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
            CreateAuthMessageWebSocket();
        }

        #endregion

        #region 8 WebSocket check alive

        // Пинг приходит каждые 10 секунд от сервера, мы на него должны ответить. Это реализовано при парсинге сообщений от вебсокета
               
        #endregion

        #region 9 Security subscrible

        private RateGate _rateGateSubscrible = new RateGate(1, TimeSpan.FromMilliseconds(300));

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscrible.WaitToProceed();
                CreateSubscribleSecurityMessageWebSocket(security);
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 10 WebSocket parsing the messages
       
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

                    if (_FIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    _FIFOListWebSocketPublicMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (message.Contains("ping"))
                        {                            
                            CreatePingMessageWebSocketPublic();
                            continue;
                        }

                        ResponseWebSocketMessage<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());
                                               
                        if (_arrayChannelsBook.Contains(action.topic))
                        {
                            UpdateDepth(message);
                            continue;
                        }

                        if (_arrayChannelsTrade.Contains(action.topic))
                        {
                            UpdateTrade(message);
                            continue;
                        }     
                    }
                    catch (Exception exeption)
                    {
                        SendLogMessage(exeption.ToString(), LogMessageType.Error);
                        SendLogMessage("message str: \n" + message, LogMessageType.Error);
                        Thread.Sleep(5000);
                    }

                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
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

                    if (_FIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    _FIFOListWebSocketPrivateMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (message.Contains("ping") || message.Contains("pong"))
                        {
                            CreatePingMessageWebSocketPrivate();                            
                            continue;
                        }

                        if (message.Contains("auth"))
                        {                            
                            SendSubscriblePrivate();
                            continue;
                        }

                        if (message.Contains("topic"))
                        {
                            ResponseWebSocketMessage<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                            if (action.topic.Equals("executionreport"))
                            {
                                UpdateOrder(message);
                            }
                            if (action.topic.Equals("position"))
                            {
                                UpdatePositionFromSubscrible(message);
                            }
                            if (action.topic.Equals("balance"))
                            {
                                UpdatePortfolioFromSubscrible(message);
                            }
                        }      
                    }
                    catch (Exception exeption)
                    {
                        SendLogMessage(exeption.ToString(), LogMessageType.Error);
                        SendLogMessage("message str: \n" + message, LogMessageType.Error);
                        Thread.Sleep(5000);
                    }
                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            ResponseChannelTrades responseTrade = JsonConvert.DeserializeObject<ResponseChannelTrades>(message);

            if (responseTrade == null)
            {
                return;
            }

            if (responseTrade.data == null)
            {
                return;
            }                      

            ResponseChannelTrades.Data item = responseTrade.data;
                        
            Trade trade = new Trade();
            trade.SecurityNameCode = item.symbol;
            trade.Price = item.price.ToDecimal();
            trade.Id = responseTrade.ts;
            trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.ts));
            trade.Volume = item.size.ToDecimal();
            trade.Side = item.side.Equals("BUY") ? Side.Buy : Side.Sell;

            NewTradesEvent(trade);            
        }

        private void UpdateDepth(string message)
        {
            Thread.Sleep(1);

            ResponseChannelBook responseDepth = JsonConvert.DeserializeObject<ResponseChannelBook>(message);

            ResponseChannelBook.Data item = responseDepth.data;

            if (item == null)
            {
                return;
            }

            if (item.asks.Count == 0 && item.bids.Count == 0)
            {
                return;
            }

            MarketDepth marketDepth = new MarketDepth();

            List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            marketDepth.SecurityNameCode = item.symbol;

            if (item.asks.Count > 0)
            {
                for (int i = 0; i < 25 && i < item.asks.Count; i++)
                {
                    if (item.asks[i].Count < 2)
                    {
                        continue;
                    }

                    decimal ask = item.asks[i][1].ToString().ToDecimal();
                    decimal price = item.asks[i][0].ToString().ToDecimal();

                    if(ask == 0 ||
                        price == 0)
                    {
                        continue;
                    }

                    MarketDepthLevel level = new MarketDepthLevel();
                    level.Ask = ask;
                    level.Price = price;
                    ascs.Add(level);
                }
            }

            if (item.bids.Count > 0)
            {
                for (int i = 0; i < 25 && i < item.bids.Count; i++)
                {
                    if (item.bids[i].Count < 2)
                    {
                        continue;
                    }

                    decimal bid = item.bids[i][1].ToString().ToDecimal();
                    decimal price = item.bids[i][0].ToString().ToDecimal();

                    if (bid == 0 ||
                        price == 0)
                    {
                        continue;
                    }

                    MarketDepthLevel level = new MarketDepthLevel();
                    level.Bid = bid;
                    level.Price = price;
                    bids.Add(level);
                }
            }

            if(ascs.Count == 0 ||
                bids.Count == 0)
            {
                return;
            }

            marketDepth.Asks = ascs;
            marketDepth.Bids = bids;
            marketDepth.Time 
                = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.ts));

            if(marketDepth.Time < _lastTimeMd)
            {
                marketDepth.Time = _lastTimeMd;
            }
            else if (marketDepth.Time == _lastTimeMd)
            {
                _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);
                marketDepth.Time = _lastTimeMd;
            }

            _lastTimeMd = marketDepth.Time;

            MarketDepthEvent(marketDepth);
        }

        private DateTime _lastTimeMd;

        private void UpdatePortfolioFromSubscrible(string message)
        {
            ResponseChannelPortfolio response = JsonConvert.DeserializeObject<ResponseChannelPortfolio>(message);

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "WooPortfolio";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            IDictionaryEnumerator enumerator = response.data.balances.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ResponseChannelPortfolio.Symbol balanceDetails = (ResponseChannelPortfolio.Symbol)enumerator.Value;

                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "WooPortfolio";
                pos.SecurityNameCode = enumerator.Key.ToString();
                pos.ValueBlocked = balanceDetails.frozen.ToDecimal();
                pos.ValueCurrent = balanceDetails.holding.ToDecimal();

                portfolio.SetNewPosition(pos);
            }
            enumerator.Reset();

            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void UpdatePositionFromSubscrible(string message)
        {
            ResponseChannelUpdatePositions response = JsonConvert.DeserializeObject<ResponseChannelUpdatePositions>(message);

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "WooPortfolio";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            IDictionaryEnumerator enumerator = response.data.positions.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ResponseChannelUpdatePositions.Symbol balanceDetails = (ResponseChannelUpdatePositions.Symbol)enumerator.Value;

                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "WooPortfolio";
                pos.SecurityNameCode = enumerator.Key.ToString();
                pos.ValueBlocked = 0;
                pos.ValueCurrent = balanceDetails.holding.ToDecimal();

                portfolio.SetNewPosition(pos);
            }
            enumerator.Reset();

            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void UpdateOrder(string message)
        {
            ResponseChannelUpdateOrder response = JsonConvert.DeserializeObject<ResponseChannelUpdateOrder>(message);

            ResponseChannelUpdateOrder.Data item = response.data;

            if (string.IsNullOrEmpty(item.clientOrderId))
            {
                return;
            }

            if (item.msgType != "0")
            {
                switch (item.msgType)
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

            OrderStateType stateType = GetOrderState(item.status);                   

            Order newOrder = new Order();
            newOrder.SecurityNameCode = item.symbol;
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.timestamp));
            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.timestamp));             
            newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
            newOrder.NumberMarket = item.orderId.ToString();
            newOrder.Side = item.side.Equals("BUY") ? Side.Buy : Side.Sell;
            newOrder.State = stateType;
            newOrder.Volume = item.quantity.ToDecimal();
            newOrder.VolumeExecute = item.executedQuantity.ToDecimal();
            newOrder.Price = item.price.ToDecimal();

            if (item.type == "MARKET")
            {
                newOrder.TypeOrder = OrderPriceType.Market;
            }
            if (item.type == "LIMIT")
            {
                newOrder.TypeOrder = OrderPriceType.Limit;
            }
            newOrder.ServerType = ServerType.Woo;
            newOrder.PortfolioNumber = "WooPortfolio";

            MyOrderEvent(newOrder);

            if (newOrder.State == OrderStateType.Done)
            {
                MyTrade myTrade = new MyTrade();

                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.timestamp));
                myTrade.NumberOrderParent = item.orderId;
                myTrade.NumberTrade = item.tradeId;
                myTrade.Price = item.executedPrice.ToDecimal();
                myTrade.SecurityNameCode = item.symbol;
                myTrade.Side = item.side.Equals("BUY") ? Side.Buy : Side.Sell;
                myTrade.Volume = item.quantity.ToDecimal();

                MyTradeEvent(myTrade);
            }            
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("NEW"):
                    stateType = OrderStateType.Activ;
                    break;
                case ("FILLED"):
                    stateType = OrderStateType.Done;
                    break;
                case ("REJECTED"):
                    stateType = OrderStateType.Fail;
                    break;
                case ("PARTIAL_FILLED"):
                    stateType = OrderStateType.Patrial;
                    break;
                case ("CANCELLED"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("REPLACED"):
                    stateType = OrderStateType.Activ; 
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

        public void CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            if (OrderStateType.Cancel == order.State)
            {
                return;
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
                    SendLogMessage($"CancelOrder. Http State Code: {response.StatusCode}, {response}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
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

        private List<string> _subscribledSecurities = new List<string>();

        private void CreateSubscribleSecurityMessageWebSocket(Security security)
        {
            if (_webSocketPublic == null)
            {
                return;
            }

            for (int i = 0; i < _subscribledSecurities.Count; i++)
            {
                if (_subscribledSecurities[i].Equals(security.Name))
                {
                    return;
                }
            }

            _subscribledSecurities.Add(security.Name);

            _arrayChannels = new List<string>();

            _arrayChannels.Add($"{security.Name}@orderbook"); // массив каналов для получения стаканов
            _arrayChannelsBook.Add($"{security.Name}@orderbook"); 
            _arrayChannels.Add($"{security.Name}@trade"); // массив каналов для получения сделок
            _arrayChannelsTrade.Add($"{security.Name}@trade"); 
            
            if (_arrayChannels.Count >= 76)
            {
                SendLogMessage($"Лимит подписки на каналы (80 каналов)", LogMessageType.Error);
                return;
            }
            for (int i = 0; i < _arrayChannels.Count;i++)
            {
                SendSubscrible(_arrayChannels[i]);
            }           
        }

        private void SendSubscrible(string channel)
        {
            string json = JsonConvert.SerializeObject(new
            {
                id = "10",
                topic = channel,
                @event = "subscribe"
            });
            _webSocketPublic.Send(json);
        }

        private void SendSubscriblePrivate()
        {
            for (int i = 0; i < _listPrivateChannel.Count; i++)
            {
                string json = JsonConvert.SerializeObject(new
                {
                    id = "10",
                    topic = _listPrivateChannel[i],
                    @event = "subscribe"
                });
                _webSocketPrivate.Send(json);
            }
        }

        private void CreatePingMessageWebSocketPublic()
        {
            string json = JsonConvert.SerializeObject(new
            {
                @event = "ping"
            });

            if (_webSocketPublic == null)
            {
                return;
            }
            else
            {
                _webSocketPublic.Send(json);
            }            
        }

        private void CreatePingMessageWebSocketPrivate()
        {
            string json = JsonConvert.SerializeObject(new
            {
                @event = "ping"
            });

            if (_webSocketPrivate == null)
            {
                return;
            }
            else
            {
                _webSocketPrivate.Send(json);
            }
        }

        private void CreateAuthMessageWebSocket()
        {
            if (_webSocketPrivate == null)
            {
                return;
            }

            string ts = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();

            string json = JsonConvert.SerializeObject(new
            {           
                id = "auth",
                @event = "auth",
                @params = new {
                    apikey = _apiKey,
                    sign = GenerateSignature($"|{ts}"),
                    timestamp = ts   
            }
        });
            _webSocketPrivate.Send(json);
        }

        private void UnsubscribeFromAllWebSockets()
        {
            if (_webSocketPublic == null)
            {
                return;
            }
            else
            {
                for (int i = 0; i < _arrayChannels.Count; i++)
                {

                    string json = JsonConvert.SerializeObject(new
                    {
                        id = "11",
                        topic = _arrayChannels[i],
                        @event = "unsubscribe"
                    });

                    _webSocketPublic.Send(json);
                }
            }

            if (_webSocketPrivate == null)
            {
                return;
            }
            else
            {
                for (int i = 0; i < _listPrivateChannel.Count; i++)
                {
                    string json = JsonConvert.SerializeObject(new
                    {
                        id = "11",
                        topic = _listPrivateChannel[i],
                        @event = "unsubscribe"
                    });

                    _webSocketPrivate.Send(json);
                }
            }
        }

        private void CreateQueryPortfolio(bool IsUpdateValueBegin)
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {      
                string timeStamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                string data = $"application_id={_appID}|{timeStamp}";
                string signature = GenerateSignature(data);

                RestClient client = new RestClient($"{_baseUrl}/v1/sub_account/asset_detail?application_id={_appID}");
                RestRequest request = new RestRequest(Method.GET);                
                request.AddHeader("x-api-timestamp", timeStamp);
                request.AddHeader("x-api-key", _apiKey);
                request.AddHeader("x-api-signature", signature);
                
                IRestResponse responseMessage = client.Execute(request);
                string JsonResponse = responseMessage.Content;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    UpdatePorfolio(JsonResponse, IsUpdateValueBegin);
                    CreateQueryPosition(true);
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

        private void UpdatePorfolio(string json, bool IsUpdateValueBegin)
        {
            ResponseMessagePortfolios response = JsonConvert.DeserializeObject<ResponseMessagePortfolios>(json);

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "WooPortfolio";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            IDictionaryEnumerator enumerator = response.balances.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ResponseMessagePortfolios.Symbol balanceDetails = (ResponseMessagePortfolios.Symbol)enumerator.Value;

                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "WooPortfolio";
                pos.SecurityNameCode = enumerator.Key.ToString();
                pos.ValueBlocked = balanceDetails.frozen.ToDecimal();
                pos.ValueCurrent = balanceDetails.holding.ToDecimal();
                
                if (IsUpdateValueBegin)
                {
                    pos.ValueBegin = balanceDetails.holding.ToDecimal();
                }

                portfolio.SetNewPosition(pos);
            }
            enumerator.Reset();

            PortfolioEvent(new List<Portfolio> { portfolio });
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

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        #endregion
    }
}