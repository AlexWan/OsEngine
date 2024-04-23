using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WebSocketSharp;
using OsEngine.Market.Servers.HTX.Spot.Entity;
using OsEngine.Market.Servers.HTX.Entity;
using RestSharp;
using System.IO.Compression;
using System.IO;
using System.Security.Authentication;

namespace OsEngine.Market.Servers.HTX.Spot
{
    public class HTXSpotServer : AServer
    {
        public HTXSpotServer()
        {
            HTXSpotServerRealization realization = new HTXSpotServerRealization();
            ServerRealization = realization;

            CreateParameterString("Access Key", "");
            CreateParameterString("Secret Key", "");            
        }
    }

    public class HTXSpotServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public HTXSpotServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.IsBackground = true;
            threadMessageReaderPublic.Name = "MessageReaderPublic";
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.IsBackground = true;
            threadMessageReaderPrivate.Name = "MessageReaderPrivate";
            threadMessageReaderPrivate.Start();

            Thread threadUpdatePortfolio = new Thread(ThreadUpdatePortfolio);
            threadUpdatePortfolio.IsBackground = true;
            threadUpdatePortfolio.Name = "ThreadUpdatePortfolio";
            threadUpdatePortfolio.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            _accessKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterString)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(_accessKey) ||
                string.IsNullOrEmpty(_secretKey))
            {
                SendLogMessage("Can`t run HTX connector. No keys", LogMessageType.Error);
                return;
            }

            string url = $"https://{_baseUrl}/v2/market-status";
            RestClient client = new RestClient(url);
            RestRequest request = new RestRequest(Method.GET);
            IRestResponse responseMessage = client.Execute(request);
  
            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                try
                {
                    _privateUriBuilder = new PrivateUrlBuilder(_accessKey, _secretKey, _baseUrl);
                    _signer = new Signer(_secretKey);

                    _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                    _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
                    CreateWebSocketConnection();
                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    SendLogMessage("Connection can be open. HTXSpot. Error request", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            else
            {
                SendLogMessage("Connection can be open. HTXSpot. Error request", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public void Dispose()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }

            try
            {
                UnsubscribeFromAllWebSockets();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _subscribledSecurities.Clear();
            _arrayPrivateChannels.Clear();
            _arrayPublicChannels.Clear();
            
            try
            {
                DeleteWebscoektConnection();
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }

            _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
        }

        public ServerType ServerType
        {
            get { return ServerType.HTXSpot; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _accessKey;

        private string _secretKey;

        private string _baseUrl = "api.huobi.pro";

        private string _webSocketUrlPublic = "wss://api.huobi.pro/ws";
               
        private string _webSocketUrlPrivate = "wss://api.huobi.pro/ws/v2";

        private int _limitCandles = 300;
               
        private List<string> _arrayPrivateChannels = new List<string>();

        private List<string> _arrayPublicChannels = new List<string>();

        private ConcurrentQueue<string> _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private PrivateUrlBuilder _privateUriBuilder;

        private Signer _signer;

        private string _allCandleSeries;

        #endregion

        #region 3 Securities

        public void GetSecurities() 
        {
            try
            {      
                string url = $"https://{_baseUrl}/v1/settings/common/market-symbols";
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

            for (int i = 0; i < response.data.Count; i++)
            {
                ResponseMessageSecurities.Data item = response.data[i];

                if (item.state == "online")
                {   
                    Security newSecurity = new Security();

                    newSecurity.Exchange = ServerType.HTXSpot.ToString();
                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.symbol;
                    newSecurity.NameClass = item.qc;
                    newSecurity.NameId = item.symbol;
                    newSecurity.SecurityType = SecurityType.CurrencyPair;
                    newSecurity.DecimalsVolume = Convert.ToInt32(item.ap);
                    newSecurity.Lot = GetDecimalsFromPrecision(Convert.ToInt32(item.ap));
                    newSecurity.PriceStep = GetDecimalsFromPrecision(Convert.ToInt32(item.pp));
                    newSecurity.Decimals = Convert.ToInt32(item.pp);
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.State = SecurityStateType.Activ;
                    newSecurity.MinTradeAmount = item.minov.ToDecimal();

                    securities.Add(newSecurity);
                }
            }
            SecurityEvent(securities);
        }

        public event Action<List<Security>> SecurityEvent;

        private decimal GetDecimalsFromPrecision(int value)
        {
            switch (value)
            {
                case 0:
                    return 1;
                case 1:
                    return 0.1m;
                case 2:
                    return 0.01m;
                case 3:
                    return 0.001m;
                case 4:
                    return 0.0001m;
                case 5:
                    return 0.00001m;
                case 6:
                    return 0.000001m;
                case 7:
                    return 0.0000001m;
                case 8:
                    return 0.00000001m;
                case 9:
                    return 0.000000001m;
                case 10:
                    return 0.0000000001m;
                case 11:
                    return 0.00000000001m;
                case 12:
                    return 0.000000000001m;
                case 13:
                    return 0.0000000000001m;
                case 14:
                    return 0.00000000000001m;
                case 15:
                    return 0.000000000000001m;
                default:
                    return 0;
            }
        }

        #endregion

        #region 4 Portfolios

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(200));
     
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
            DateTime endTimeData = startTimeData.AddMinutes(tfTotalMinutes * _limitCandles);

            do
            {
                long from = TimeManager.GetTimeStampSecondsToDateTime(startTimeData);
                long to = TimeManager.GetTimeStampSecondsToDateTime(endTimeData);

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

                startTimeData = endTimeData;
                endTimeData = startTimeData.AddMinutes(tfTotalMinutes * _limitCandles);

                if (startTimeData >= DateTime.UtcNow)
                {
                    break;
                }

                if (endTimeData > DateTime.Now)
                {
                    endTimeData = DateTime.Now;
                }

            } while (true);

            if(allCandles != null && allCandles.Count > 0)
            {
                for(int i = 1;i < allCandles.Count;i++)
                {
                    if (allCandles[i-1].TimeStart == allCandles[i].TimeStart)
                    {
                        allCandles.RemoveAt(i);
                        i--;
                    }
                }
            }

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
            switch (timeFrame.TotalMinutes)
            {
                case 1:
                    return "1min";
                case 5:
                    return "5min";
                case 15:
                    return "15min";
                case 30:
                    return "30min";
                case 60:
                    return "60min";
                case 240:
                    return "4hour";
                case 1440:
                    return "1day";
                default:
                    return null;
            }           
        }

        private readonly RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(100));
               
        private List<Candle> RequestCandleHistory(string security, string interval, long fromTimeStamp, long toTimeStamp)
        {
            _rgCandleData.WaitToProceed();

            try
            {
                string clientId = "";

                string topic = $"market.{security}.kline.{interval}";

                string request = $"{{ \"req\": \"{topic}\",\"id\": \"{clientId}\", \"from\":{fromTimeStamp}, \"to\":{toTimeStamp} }}";

                _webSocketPublic.Send(request);

                DateTime startLoadingTime = DateTime.Now;

                while (startLoadingTime.AddSeconds(30) > DateTime.Now)
                {
                    if (_allCandleSeries != null)
                    {
                        List<Candle> candles = ConvertCandles(_allCandleSeries);
                        _allCandleSeries = null;

                        return candles;
                    }
                    Thread.Sleep(100);
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

            List<ResponseMessageCandles.Data> item = response.data;

            for (int i = 0; i < item.Count; i++)
            {
                if (CheckCandlesToZeroData(item[i]))
                {
                    continue;
                }

                Candle candle = new Candle();

                candle.State = CandleState.Finished;
                candle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(long.Parse(item[i].id));
                candle.Volume = item[i].vol.ToDecimal();
                candle.Close = item[i].close.ToDecimal();
                candle.High = item[i].high.ToDecimal();
                candle.Low = item[i].low.ToDecimal();
                candle.Open = item[i].open.ToDecimal();

                candles.Add(candle);
            }
            return candles;
        }

        private bool CheckCandlesToZeroData(ResponseMessageCandles.Data item)
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
            _publicSocketActivate = false;
            _privateSocketActivate = false;

            _webSocketPublic = new WebSocket(_webSocketUrlPublic);
            _webSocketPublic.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;
            _webSocketPublic.OnOpen += webSocketPublic_OnOpen;
            _webSocketPublic.OnMessage += webSocketPublic_OnMessage;
            _webSocketPublic.OnError += webSocketPublic_OnError;
            _webSocketPublic.OnClose += webSocketPublic_OnClose;

            _webSocketPublic.Connect();

            _webSocketPrivate = new WebSocket(_webSocketUrlPrivate);
            _webSocketPrivate.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;
            _webSocketPrivate.OnOpen += webSocketPrivate_OnOpen;
            _webSocketPrivate.OnMessage += webSocketPrivate_OnMessage;
            _webSocketPrivate.OnError += webSocketPrivate_OnError;
            _webSocketPrivate.OnClose += webSocketPrivate_OnClose;

            _webSocketPrivate.Connect();
        }

        private void DeleteWebscoektConnection()
        {
            if (_webSocketPublic != null)
            {
                _webSocketPublic.OnOpen -= webSocketPublic_OnOpen;
                _webSocketPublic.OnMessage -= webSocketPublic_OnMessage;
                _webSocketPublic.OnError -= webSocketPublic_OnError;
                _webSocketPublic.OnClose -= webSocketPublic_OnClose;
               
                try
                {
                    _webSocketPublic.Close();
                }
                catch
                {
                    // ignore
                }
                _webSocketPublic = null;
            }

            if (_webSocketPrivate != null)
            {
                _webSocketPrivate.OnOpen -= webSocketPrivate_OnOpen;
                _webSocketPrivate.OnMessage -= webSocketPrivate_OnMessage;
                _webSocketPrivate.OnError -= webSocketPrivate_OnError;
                _webSocketPrivate.OnClose -= webSocketPrivate_OnClose;
               
                try
                {
                    _webSocketPrivate.Close();
                }
                catch
                {
                    // ignore
                }
                _webSocketPrivate = null;
            }
        }

        private bool _publicSocketActivate = false;

        private bool _privateSocketActivate = false;

        private string _lockerCheckActivateionSockets = "lockerCheckActivateionSockets";

        private void CheckActivationSockets()
        {
            lock (_lockerCheckActivateionSockets)
            {
                if(_publicSocketActivate == false)
                {
                    return;
                }
                if (_privateSocketActivate == false)
                {
                    return;
                }

                if(ServerStatus == ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Connect;

                    if(ConnectEvent != null)
                    {
                        ConnectEvent();
                    }
                }
            }
        }

        #endregion

        #region 7 WebSocket events

        private void webSocketPublic_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            WebSocketSharp.ErrorEventArgs error = e;

            if (error.Exception != null)
            {
                SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void webSocketPublic_OnMessage(object sender, MessageEventArgs e)
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
                if (_FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }
                if (e.IsBinary)
                {
                    _FIFOListWebSocketPublicMessage.Enqueue(Decompress(e.RawData));
                }
                else if (e.IsText)
                {
                    _FIFOListWebSocketPublicMessage.Enqueue(e.Data);
                }
                                
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void webSocketPublic_OnClose(object sender, CloseEventArgs e)
        {
            if (DisconnectEvent != null 
                && ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by HTXSpot. WebSocket Public Closed Event", LogMessageType.System);
            }
            ServerStatus = ServerConnectStatus.Disconnect;
            DisconnectEvent();
        }

        private void webSocketPublic_OnOpen(object sender, EventArgs e)
        {
            SendLogMessage("Connection Websocket Public Open", LogMessageType.System);

            _publicSocketActivate = true;
            CheckActivationSockets();
        }

        private void webSocketPrivate_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            WebSocketSharp.ErrorEventArgs error = e;

            if (error.Exception != null)
            {
                SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void webSocketPrivate_OnMessage(object sender, MessageEventArgs e)
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
                if (_FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }
                if (e.IsBinary)
                {
                    _FIFOListWebSocketPrivateMessage.Enqueue(Decompress(e.RawData));
                }
                else if (e.IsText)
                {
                    _FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void webSocketPrivate_OnClose(object sender, CloseEventArgs e)
        {
            if (DisconnectEvent != null 
                & ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by HTXSpot. WebSocket Private Closed Event", LogMessageType.System);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void webSocketPrivate_OnOpen(object sender, EventArgs e)
        {
            SendLogMessage("Connection Websocket Private Open", LogMessageType.System);

            _privateSocketActivate = true;
            CheckActivationSockets();

            try
            {
                string authRequest = BuildSign(DateTime.UtcNow);
                _webSocketPrivate.Send(authRequest);
            }
            catch
            {

            }
        }

        #endregion

        #region 8 WebSocket check alive

        private void ThreadUpdatePortfolio()
        {
            Thread.Sleep(5000);

            while (true)
            {
                try
                {
                    Thread.Sleep(5000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }
                    CreateQueryPortfolio(false);
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

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
                            CreatePingMessageWebSocketPublic(message);
                            continue;
                        }

                        if (message.Contains("kline"))
                        {
                            _allCandleSeries = message;
                            continue;
                        }

                        if (message.Contains("mbp"))
                        {
                            UpdateDepth(message);
                            continue;
                        }

                        if (message.Contains("trade.detail"))
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
                        if (message.Contains("ping"))
                        {
                            CreatePingMessageWebSocketPrivate(message);                            
                            continue;
                        }

                        if (message.Contains("auth"))
                        {                            
                            SendSubscriblePrivate();
                            continue;
                        }
                        
                        if (message.Contains("orders#"))
                        {
                            UpdateOrder(message);
                        }
                        if (message.Contains("trade.clearing"))
                        {
                            UpdateMyTrade(message);
                        }                      
                    }
                    catch (Exception exeption)
                    {
                        SendLogMessage(exeption.ToString(), LogMessageType.Error);
                        SendLogMessage("Message str: \n" + message, LogMessageType.Error);
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

            if (responseTrade.tick == null)
            {
                return;
            }                      

            List<ResponseChannelTrades.Data> item = responseTrade.tick.data;
                
            for (int i = 0;  i < item.Count; i++)
            {
                Trade trade = new Trade();
                trade.SecurityNameCode = GetSecurityName(responseTrade.ch);
                trade.Price = item[i].price.ToDecimal();
                trade.Id = item[i].tradeId;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item[i].ts));
                trade.Volume = item[i].amount.ToDecimal();
                trade.Side = item[i].direction.Equals("buy") ? Side.Buy : Side.Sell;

                NewTradesEvent(trade);
            }
        }

        private void UpdateDepth(string message)
        {
            Thread.Sleep(1);

            ResponseChannelBook responseDepth = JsonConvert.DeserializeObject<ResponseChannelBook>(message);

            ResponseChannelBook.Tick item = responseDepth.tick;

            if (item == null)
            {
                return;
            }

            if (item.asks.Count == 0 && item.bids.Count == 0)
            {
                return;
            }

            MarketDepth marketDepth = new MarketDepth();

            List<MarketDepthLevel> asks = new List<MarketDepthLevel>();
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            marketDepth.SecurityNameCode = GetSecurityName(responseDepth.ch);

            if (item.asks.Count > 0)
            {
                for (int i = 0; i < item.asks.Count; i++)
                {
                    decimal ask = item.asks[i][1].ToString().ToDecimal();
                    decimal price = item.asks[i][0].ToString().ToDecimal();

                    if (ask == 0 ||
                        price == 0)
                    {
                        continue;
                    }

                    MarketDepthLevel level = new MarketDepthLevel();
                    level.Ask = ask;
                    level.Price = price;
                    asks.Add(level);                               
                }
            }

            if (item.bids.Count > 0)
            {
                for (int i = 0; i < item.bids.Count; i++)
                {
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

            if (asks.Count == 0 ||
                bids.Count == 0)
            {
                return;
            }

            marketDepth.Asks = asks;
            marketDepth.Bids = bids;

            marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.ts));

            if (marketDepth.Time <= _lastMdTime)
            {
                marketDepth.Time = _lastMdTime.AddTicks(1);
            }

            _lastMdTime = marketDepth.Time;


            MarketDepthEvent(marketDepth);
        }

        DateTime _lastMdTime;

        private string GetSecurityName(string ch)
        {
            string[] strings = ch.Split('.');
            return strings[1];
        }
       
        private void UpdateMyTrade(string message)
        {
            ResponseChannelUpdateMyTrade response = JsonConvert.DeserializeObject<ResponseChannelUpdateMyTrade>(message);

            if (response.code != null)
            {
                return;
            }

            ResponseChannelUpdateMyTrade.Data item = response.data;                    

            MyTrade myTrade = new MyTrade();

            myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.tradeTime));
            myTrade.NumberOrderParent = item.orderId;
            myTrade.NumberTrade = item.tradeId;
            myTrade.Price = item.tradePrice.ToDecimal();
            myTrade.SecurityNameCode = item.symbol;
            myTrade.Side = item.orderSide.Equals("buy") ? Side.Buy : Side.Sell;
            myTrade.Volume = item.tradeVolume.ToDecimal();

            MyTradeEvent(myTrade);

            if (item.orderStatus.Equals("partial-filled") || item.orderStatus.Equals("filled"))
            {
                Order newOrder = new Order();
                newOrder.ServerType = ServerType.HTXSpot;
                newOrder.SecurityNameCode = item.symbol;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.tradeTime));
                newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.orderCreateTime));

                try
                {
                    newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                }
                catch
                {
                    //ignore
                }
               
                newOrder.NumberMarket = item.orderId.ToString();
                newOrder.Side = item.orderSide.Equals("buy") ? Side.Buy : Side.Sell;
                newOrder.State = GetOrderState(item.orderStatus);
                newOrder.Volume = item.orderSize.ToDecimal();
                newOrder.VolumeExecute = item.tradeVolume.ToDecimal();
                newOrder.Price = item.orderPrice.ToDecimal();

                string source = "spot";
                if (item.source == "margin-api")
                {
                    source = "margin";
                }
                if (item.source == "super-margin-api")
                {
                    source = "super-margin";
                }

                newOrder.PortfolioNumber = $"HTX_{source}_{item.accountId}_Portfolio";

                MyOrderEvent(newOrder);
            }
            CreateQueryPortfolio(false);
        }

        private void UpdateOrder(string message)
        {
            ResponseChannelUpdateOrder response = JsonConvert.DeserializeObject<ResponseChannelUpdateOrder>(message);

            ResponseChannelUpdateOrder.Data item = response.data;

            if (response.code != null)
            {
                return;
            }

            if (item.eventType.Equals("creation") || item.eventType.Equals("cancellation"))
            {
                Order newOrder = new Order();

                if (item.eventType.Equals("creation"))
                {
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.orderCreateTime));
                    newOrder.TimeCreate = newOrder.TimeCallBack;
                }
                else if (item.eventType.Equals("cancellation"))
                {
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.lastActTime));
                }
                
                newOrder.ServerType = ServerType.HTXSpot;
                newOrder.SecurityNameCode = item.symbol;           
                
                try
                {
                    newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                }
                catch
                {
                    // ignore
                }

                newOrder.NumberMarket = item.orderId.ToString();
                newOrder.Side = item.type.Split('-')[0].Equals("buy") ? Side.Buy : Side.Sell;
                newOrder.State = GetOrderState(item.orderStatus);
                newOrder.Volume = item.orderSize.ToDecimal();                
                newOrder.Price = item.orderPrice.ToDecimal();

                if (item.type.Split('-')[1] == "market")
                {
                    newOrder.TypeOrder = OrderPriceType.Market;
                }
                if (item.type.Split('-')[1] == "limit")
                {
                    newOrder.TypeOrder = OrderPriceType.Limit;
                }

                string source = "spot";
                if (item.orderSource == "margin-api")
                {
                    source = "margin";
                }
                if (item.orderSource == "super-margin-api")
                {
                    source = "super-margin";
                }

                newOrder.PortfolioNumber = $"HTX_{source}_{item.accountId}_Portfolio";

                MyOrderEvent(newOrder);
            }        
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("submitted"):
                    stateType = OrderStateType.Activ;
                    break;
                case ("filled"):
                    stateType = OrderStateType.Done;
                    break;                
                case ("partial-filled"):
                    stateType = OrderStateType.Patrial;
                    break;
                case ("canceled"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("created"):
                    stateType = OrderStateType.Pending; 
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
                string[] accountData = order.PortfolioNumber.Split('_');

                string source_portfolio = "spot-api";

                if (accountData[1] == "margin")
                {
                    source_portfolio = "margin-api";
                }
                else if (accountData[1] == "super-margin")
                {
                    source_portfolio = "super-margin-api";
                }

                string typeOrder = order.TypeOrder == OrderPriceType.Market ? "market" : "limit";

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("account-id", accountData[2]);
                jsonContent.Add("symbol", order.SecurityNameCode);
                jsonContent.Add("type", order.Side == Side.Buy ? $"buy-{typeOrder}" : $"sell-{typeOrder}");
                jsonContent.Add("amount", order.Volume.ToString().Replace(",","."));
                if (order.TypeOrder != OrderPriceType.Market)
                {
                    jsonContent.Add("price", order.Price.ToString().Replace(",", "."));
                }
                jsonContent.Add("source", source_portfolio);
                jsonContent.Add("client-order-id", order.NumberUser.ToString());

                string url = _privateUriBuilder.Build("POST", $"/v1/order/orders/place");
                          
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);
                                
                PlaceOrderResponse orderResponse = JsonConvert.DeserializeObject<PlaceOrderResponse>(responseMessage.Content);

                if (orderResponse.status != "ok")
                {
                    order.State = OrderStateType.Fail;
                    MyOrderEvent(order);
                    SendLogMessage($"SendOrder. Error: {responseMessage.Content}", LogMessageType.Error);
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

            if (order.State == OrderStateType.Cancel)
            {
                return;
            }
            try
            {
                string url = _privateUriBuilder.Build("POST", $"/v1/order/orders/{order.NumberMarket}/submitcancel");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                IRestResponse responseMessage = client.Execute(request);

                PlaceOrderResponse response = JsonConvert.DeserializeObject<PlaceOrderResponse>(responseMessage.Content);

                if (response.status != "ok")
                {
                    SendLogMessage($"CancelOrder. Http State Code: {responseMessage.Content}", LogMessageType.Error);
                }
                else
                {
                    order.State = OrderStateType.Cancel;
                    MyOrderEvent(order);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange();

            for (int i = 0; orders != null && i < orders.Count; i++)
            {
                if (orders[i] == null)
                {
                    continue;
                }

                if (orders[i].State != OrderStateType.Activ
                    && orders[i].State != OrderStateType.Patrial
                    && orders[i].State != OrderStateType.Pending)
                {
                    continue;
                }

                orders[i].TimeCreate = orders[i].TimeCallBack;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        private List<Order> GetAllOrdersFromExchange()
        {
            List<Order> orders = new List<Order>();

            try
            {
                string url = _privateUriBuilder.Build("GET", $"/v1/order/history");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                ResponseMessageAllOrders response = JsonConvert.DeserializeObject<ResponseMessageAllOrders>(responseMessage.Content);

                List<ResponseMessageAllOrders.Data> item = response.data;

                if (responseMessage.Content.Contains("error"))
                {
                    SendLogMessage($"GetAllOrder. Http State Code: {responseMessage.Content}", LogMessageType.Error);
                }
                else
                {
                    if (item != null && item.Count > 0)
                    {
                        for (int i = 0; i < item.Count; i++)
                        {
                            if (item[i].client_order_id == null || item[i].client_order_id == "")
                            {
                                continue;
                            }

                            if (!item[i].source.Contains("api"))
                            {
                                continue;
                            }

                            Order newOrder = new Order();

                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].created_at));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].created_at));
                            newOrder.ServerType = ServerType.HTXSpot;
                            newOrder.SecurityNameCode = item[i].symbol;
                            newOrder.NumberUser = Convert.ToInt32(item[i].client_order_id);
                            newOrder.NumberMarket = item[i].id.ToString();                            
                            newOrder.State = GetOrderState(item[i].state);
                            newOrder.Volume = item[i].amount.ToDecimal();
                            newOrder.Price = item[i].price.ToDecimal();
                            
                            if (item[i].type.Split('-')[1] == "market")
                            {
                                newOrder.TypeOrder = OrderPriceType.Market;
                            }
                            if (item[i].type.Split('-')[1] == "limit")
                            {
                                newOrder.TypeOrder = OrderPriceType.Limit;
                            }

                            if (item[i].type.Split('-')[0] == "buy")
                            {
                                newOrder.Side = Side.Buy;
                            }
                            else
                            {
                                newOrder.Side = Side.Sell;
                            }

                            string source = "spot";
                            if (item[i].source == "margin-api")
                            {
                                source = "margin";
                            }
                            if (item[i].source == "super-margin-api")
                            {
                                source = "super-margin";
                            }

                            newOrder.PortfolioNumber = $"HTX_{source}_{item[i].account_id}_Portfolio";

                            orders.Add(newOrder);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return orders;
        }

        public void GetOrderStatus(Order order)
        {
            List<Order> orders = GetAllOrdersFromExchange();

            if (orders == null ||
                orders.Count == 0)
            {
                return;
            }

            Order orderOnMarket = null;

            for (int i = 0; i < orders.Count; i++)
            {
                Order curOder = orders[i];

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
                || orderOnMarket.State == OrderStateType.Patrial)
            {
                List<MyTrade> tradesBySecurity
                    = GetMyTradesBySecurity(orderOnMarket.NumberMarket);

                if (tradesBySecurity == null)
                {
                    return;
                }

                List<MyTrade> tradesByMyOrder = new List<MyTrade>();

                for (int i = 0; i < tradesBySecurity.Count; i++)
                {
                    if (tradesBySecurity[i].NumberOrderParent == orderOnMarket.NumberMarket)
                    {
                        tradesByMyOrder.Add(tradesBySecurity[i]);
                    }
                }

                for (int i = 0; i < tradesByMyOrder.Count; i++)
                {
                    if (MyTradeEvent != null)
                    {
                        MyTradeEvent(tradesByMyOrder[i]);
                    }
                }
            }
        }

        private Order GetOrderFromExchange(string numberMarket)
        {
            Order newOrder = new Order();

            try
            {                
                string url = _privateUriBuilder.Build("GET", $"/v1/order/orders/{numberMarket}");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);                
                IRestResponse responseMessage = client.Execute(request);

                ResponseMessageGetOrder response = JsonConvert.DeserializeObject<ResponseMessageGetOrder>(responseMessage.Content);

                ResponseMessageGetOrder.Data item = response.data;

                if (responseMessage.Content.Contains("error"))
                {
                    SendLogMessage($"GetOrderFromExchange. Http State Code: {responseMessage.Content}", LogMessageType.Error);
                }
                else
                {
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.created_at));
                    newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.created_at));
                    newOrder.ServerType = ServerType.HTXSpot;
                    newOrder.SecurityNameCode = item.symbol;
                    newOrder.NumberUser = Convert.ToInt32(item.client_order_id);
                    newOrder.NumberMarket = item.id.ToString();
                    newOrder.State = GetOrderState(item.state);
                    newOrder.Volume = item.amount.ToDecimal();
                    newOrder.Price = item.price.ToDecimal();

                    if (item.type.Split('-')[1] == "market")
                    {
                        newOrder.TypeOrder = OrderPriceType.Market;
                    }
                    if (item.type.Split('-')[1] == "limit")
                    {
                        newOrder.TypeOrder = OrderPriceType.Limit;
                    }

                    if (item.type.Split('-')[0] == "buy")
                    {
                        newOrder.Side = Side.Buy;
                    }
                    else
                    {
                        newOrder.Side = Side.Sell;
                    }

                    string source = "spot";
                    if (item.source == "margin-api")
                    {
                        source = "margin";
                    }
                    if (item.source == "super-margin-api")
                    {
                        source = "super-margin";
                    }

                    newOrder.PortfolioNumber = $"HTX_{source}_{item.account_id}_Portfolio";

                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return newOrder;
        }

        private List<MyTrade> GetMyTradesBySecurity(string orderId)
        {
            try
            {
                string url = _privateUriBuilder.Build("GET", $"/v1/order/orders/{orderId}/matchresults");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                string respString = responseMessage.Content;

                if (!respString.Contains("error"))
                {
                    ResponseMessageGetMyTradesBySecurity orderResponse = JsonConvert.DeserializeObject<ResponseMessageGetMyTradesBySecurity>(respString);

                    List<MyTrade> osEngineOrders = new List<MyTrade>();

                    List<ResponseMessageGetMyTradesBySecurity.Data> item = orderResponse.data;

                    if (item != null && item.Count > 0)
                    {
                        for (int i = 0; i < item.Count; i++)
                        {
                            MyTrade newTrade = new MyTrade();
                            newTrade.SecurityNameCode = item[i].symbol;
                            newTrade.NumberTrade = item[i].trade_id;
                            newTrade.NumberOrderParent = item[i].order_id;
                            newTrade.Volume = item[i].filled_amount.ToDecimal();
                            newTrade.Price = item[i].price.ToDecimal();
                            newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item[i].created_at));

                            if (item[i].type.Split('-')[0] == "buy")
                            {
                                newTrade.Side = Side.Buy;
                            }
                            else
                            {
                                newTrade.Side = Side.Sell;
                            }
                            osEngineOrders.Add(newTrade);
                        }
                    }
                    return osEngineOrders;
                }
                else if (responseMessage.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    SendLogMessage("GetMyTradesBySecurity request error. ", LogMessageType.Error);

                    if (responseMessage.Content != null)
                    {
                        SendLogMessage("Fail reasons: "
                      + responseMessage.Content, LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("GetMyTradesBySecurity request error." + exception.ToString(), LogMessageType.Error);
            }
            return null;
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

            string clientId = "";

            string topic = $"market.{security.Name}.mbp.refresh.20";
            _webSocketPublic.Send($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");
            _arrayPublicChannels.Add(topic);

            topic = $"market.{security.Name}.trade.detail";
            _webSocketPublic.Send($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");
            _arrayPublicChannels.Add(topic);
        }

        private void SendSubscriblePrivate()
        {  
            string chOrders = "orders#*";
            string chTrades = "trade.clearing#*#0";
            _webSocketPrivate.Send($"{{\"action\": \"sub\",\"ch\": \"{chOrders}\"}}");
            _webSocketPrivate.Send($"{{\"action\": \"sub\",\"ch\": \"{chTrades}\"}}");
            _arrayPrivateChannels.Add(chTrades);
            _arrayPrivateChannels.Add(chOrders);
        }

        private void CreatePingMessageWebSocketPublic(string message)
        {
            ResponsePing response = JsonConvert.DeserializeObject<ResponsePing>(message);

            if (_webSocketPublic == null)
            {
                return;
            }
            else
            {
                _webSocketPublic.Send($"{{\"pong\": \"{response.ping}\"}}");
            }            
        }

        private void CreatePingMessageWebSocketPrivate(string message)
        {
            ResponsePing response = JsonConvert.DeserializeObject<ResponsePing>(message);

            if (_webSocketPrivate == null)
            {
                return;
            }
            else
            {
                _webSocketPrivate.Send($"{{\"pong\": \"{response.ping}\"}}");
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            if (_webSocketPublic != null)
            {
                try
                {
                    for (int i = 0; i < _arrayPublicChannels.Count; i++)
                    {
                        _webSocketPublic.Send($"{{\"action\": \"unsub\",\"ch\": \"{_arrayPublicChannels[i]}\"}}");
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (_webSocketPrivate != null)
            {
                try
                {
                    for (int i = 0; i < _arrayPrivateChannels.Count; i++)
                    {

                        _webSocketPrivate.Send($"{{\"action\": \"unsub\",\"ch\": \"{_arrayPrivateChannels[i]}\"}}");
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void CreateQueryPortfolio(bool IsUpdateValueBegin)
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {   
                string url = _privateUriBuilder.Build("GET", "/v1/account/accounts");
                       
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                string JsonResponse = responseMessage.Content;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    UpdatePorfolio(JsonResponse, IsUpdateValueBegin);                   
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

        private void UpdatePorfolio(string json, bool IsUpdateValueBegin)
        {
            ResponseMessagePortfolios response = JsonConvert.DeserializeObject<ResponseMessagePortfolios>(json);

            List<Portfolio> portfolios = new List<Portfolio>();

            List<ResponseMessagePortfolios.Data> item = response.data;

            for (int i = 0; i < item.Count; i++)
            {
                Portfolio portfolio = new Portfolio();

                portfolio.Number = $"HTX_{item[i].type}_{item[i].id}_Portfolio";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                string url = _privateUriBuilder.Build("GET", $"/v1/account/accounts/{item[i].id}/balance");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                string JsonResponse = responseMessage.Content;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseMessagePositions responsePosition = JsonConvert.DeserializeObject<ResponseMessagePositions>(JsonResponse);

                    if(responsePosition.data == null)
                    {
                        continue;
                    }

                    List<ResponseMessagePositions.Lists> positions = responsePosition.data.list;

                    for (int j = 0; j < positions.Count; j++)
                    {
                        PositionOnBoard pos = new PositionOnBoard();

                        if (positions[j].type == "trade" && positions[j].balance == "0")
                        {
                            continue;
                        }
                        if (positions[j].type == "frozen")
                        {
                            continue;
                        }
                        pos.PortfolioName = $"HTX_{item[i].type}_{item[i].id}_Portfolio";
                        pos.SecurityNameCode = positions[j].currency;
                        
                        if (positions[j].type == "trade")
                        {                           
                            pos.ValueCurrent = positions[j].balance.ToDecimal();
                            
                            if (j != positions.Count-1)
                            {
                                if (positions[j + 1].type == "frozen" 
                                    && positions[j].currency == positions[j + 1].currency)
                                {
                                    pos.ValueBlocked = positions[j+1].balance.ToDecimal();
                                }
                            }

                            if (IsUpdateValueBegin)
                            {
                                pos.ValueBegin = positions[j].balance.ToDecimal();                                
                            }
                        }
                        portfolio.SetNewPosition(pos);                        
                    }
                    portfolios.Add(portfolio);
                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.Error);
                }
            }

            PortfolioEvent(portfolios);
        }

        public static string Decompress(byte[] input)
        {
            using (GZipStream stream = new GZipStream(new MemoryStream(input), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];

                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    } while (count > 0);

                    return Encoding.UTF8.GetString(memory.ToArray());
                }
            }
        }

        public string BuildSign(DateTime utcDateTime)
        {
            string strDateTime = utcDateTime.ToString("s");

            GetRequest request = new GetRequest();
            request.AddParam("accessKey", _accessKey);
            request.AddParam("signatureMethod", "HmacSHA256");
            request.AddParam("signatureVersion", "2.1");
            request.AddParam("timestamp", strDateTime);

            string signature = _signer.Sign("GET", _baseUrl, "/ws/v2", request.BuildParams());

            WebSocketAuthenticationRequestV2 auth = new WebSocketAuthenticationRequestV2();
            auth.@params = new WebSocketAuthenticationRequestV2.Params();           
            auth.@params.accessKey = _accessKey;
            auth.@params.signature = signature;
            auth.@params.timestamp = strDateTime;
            
            return JsonConvert.SerializeObject(auth);
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