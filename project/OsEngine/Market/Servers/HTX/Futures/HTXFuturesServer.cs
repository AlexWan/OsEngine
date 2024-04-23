﻿using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.HTX.Entity;
using OsEngine.Market.Servers.HTX.Futures.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using WebSocketSharp;

namespace OsEngine.Market.Servers.HTX.Futures
{
    public class HTXFuturesServer : AServer
    {
        public HTXFuturesServer()
        {
            HTXFuturesServerRealization realization = new HTXFuturesServerRealization();
            ServerRealization = realization;

            CreateParameterString("Access Key", "");
            CreateParameterString("Secret Key", "");            
        }
    }

    public class HTXFuturesServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public HTXFuturesServerRealization()
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
        }

        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            _accessKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterString)ServerParameters[1]).Value;
            
            if(string.IsNullOrEmpty(_accessKey) ||
                string.IsNullOrEmpty(_secretKey))
            {
                SendLogMessage("Connection can be open. No keys", LogMessageType.Error);
                if(ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
                return;
            }

            string url = $"https://{_baseUrl}/api/v1/timestamp";
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
                    SendLogMessage("Connection can be open. HTXFutures. Error request", LogMessageType.Error);
                    
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
            else
            {
                SendLogMessage("Connection can be open. HTXFutures. Error request", LogMessageType.Error);
                
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
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
                       
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.HTXFutures; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _accessKey;

        private string _secretKey;

        private string _baseUrl = "api.hbdm.com";

        private string _webSocketPathPublic = "/ws";
               
        private string _webSocketPathPrivate = "/notification";

        private int _limitCandles = 1990;
               
        private List<string> _arrayPrivateChannels = new List<string>();

        private List<string> _arrayPublicChannels = new List<string>();

        private ConcurrentQueue<string> _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private PrivateUrlBuilder _privateUriBuilder;

        private Signer _signer;

        #endregion

        #region 3 Securities

        public void GetSecurities() 
        {
            try
            {      
                string url = $"https://{_baseUrl}/api/v1/contract_contract_info";
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

                if (item.contract_status == "1")
                {
                    Security newSecurity = new Security();

                    newSecurity.Exchange = ServerType.HTXFutures.ToString();
                    newSecurity.Name = JoinSecurityName(item.symbol, item.contract_type); 
                    newSecurity.NameFull = item.contract_code;
                    newSecurity.NameClass = "Futures";
                    newSecurity.NameId = item.contract_code;
                    newSecurity.SecurityType = SecurityType.Futures;
                    newSecurity.DecimalsVolume = item.contract_size.DecimalsCount();
                    newSecurity.Lot = item.contract_size.ToDecimal();
                    newSecurity.PriceStep = item.price_tick.ToDecimal();
                    newSecurity.Decimals = item.price_tick.DecimalsCount();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.State = SecurityStateType.Activ;
                    newSecurity.MinTradeAmount = item.contract_size.ToDecimal();

                    securities.Add(newSecurity);
                }
            }
            SecurityEvent(securities);
        }

        private string JoinSecurityName(string symbol, string contractType)
        {
            string postfix = "_CQ";

            if (contractType == "this_week")
            {
                postfix = "_CW";
            }
            else if (contractType == "next_week")
            {
                postfix = "_NW";
            }
            return symbol + postfix;
        }

        public event Action<List<Security>> SecurityEvent;

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
            if (endTimeData > DateTime.Now)
            {
                endTimeData = DateTime.Now;
            }

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

                if (allCandles.Count > 0 && allCandles[allCandles.Count - 1].TimeStart == candles[0].TimeStart)
                {
                    candles.RemoveAt(0);
                }

                if (candles.Count == 0)
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

                if (startTimeData >= DateTime.Now)
                {
                    break;
                }

                if (endTimeData > DateTime.Now)
                {
                    endTimeData = DateTime.Now;
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
                string queryParam = $"symbol={security}&";
                queryParam += $"period={interval}&";                
                queryParam += $"from={fromTimeStamp}&";
                queryParam += $"to={toTimeStamp}";

                string url = $"https://{_baseUrl}/market/history/kline?{queryParam}";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);
                string JsonResponse = responseMessage.Content;

                if (!JsonResponse.Contains("error"))
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

            List<ResponseMessageCandles.Data> item = response.data;

            if (item == null)
            {
                return null;
            }

            if (item.Count == 0)
            {
                return null;
            }

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
             _publicSocketOpen = false;
             _privateSocketOpen = false;

            _webSocketPublic = new WebSocket($"wss://{_baseUrl}{_webSocketPathPublic}");            
            _webSocketPublic.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;
            _webSocketPublic.OnOpen += webSocketPublic_OnOpen;
            _webSocketPublic.OnMessage += webSocketPublic_OnMessage;
            _webSocketPublic.OnError += webSocketPublic_OnError;
            _webSocketPublic.OnClose += webSocketPublic_OnClose;

            _webSocketPublic.Connect();

            _webSocketPrivate = new WebSocket($"wss://{_baseUrl}{_webSocketPathPrivate}");
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
                try
                {
                    _webSocketPublic.OnOpen -= webSocketPublic_OnOpen;
                    _webSocketPublic.OnMessage -= webSocketPublic_OnMessage;
                    _webSocketPublic.OnError -= webSocketPublic_OnError;
                    _webSocketPublic.OnClose -= webSocketPublic_OnClose;
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
                try
                {
                    _webSocketPrivate.OnOpen -= webSocketPrivate_OnOpen;
                    _webSocketPrivate.OnMessage -= webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError -= webSocketPrivate_OnError;
                    _webSocketPrivate.OnClose -= webSocketPrivate_OnClose;
                    _webSocketPrivate.Close();
                }
                catch
                {
                    // ignore
                }

                _webSocketPrivate = null;
            }
        }

        private string _socketAcvateLocker = "socketAcvateLocker";

        private void CheckSocketsActivate()
        {
            lock(_socketAcvateLocker)
            {
                if (_publicSocketOpen
                    && _privateSocketOpen
                    && ServerStatus == ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Connect;

                    if (ConnectEvent != null)
                    {
                        ConnectEvent();
                    }
                }
            }
        }

        private bool _publicSocketOpen = false;

        private bool _privateSocketOpen = false;

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
                & ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by HTXSpot. WebSocket Public Closed Event", LogMessageType.System);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void webSocketPublic_OnOpen(object sender, EventArgs e)
        {
            SendLogMessage("Connection Websocket Public Open", LogMessageType.System);
            _publicSocketOpen = true;
            CheckSocketsActivate();
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
                && ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by HTXFutures. WebSocket Private Closed Event", LogMessageType.System);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void webSocketPrivate_OnOpen(object sender, EventArgs e)
        {
            try
            {
                SendLogMessage("Connection Websocket Private Open", LogMessageType.System);

                string authRequest = BuildSign(DateTime.UtcNow);
                _webSocketPrivate.Send(authRequest);
                _privateSocketOpen = true;

                CheckSocketsActivate();
            }
            catch(Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        #endregion

        #region 9 Security subscrible

        private RateGate _rateGateSubscrible = new RateGate(1, TimeSpan.FromMilliseconds(200));

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

                        if (message.Contains("depth"))
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
                        
                        if (message.Contains("orders."))
                        {
                            UpdateOrder(message);
                            continue;
                        }
                        if (message.Contains("accounts."))
                        {
                            UpdatePortfolioFromSubscrible(message);
                            continue;
                        }
                        if (message.Contains("positions."))
                        {
                            UpdatePositionFromSubscrible(message);
                            continue;
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
                trade.Id = item[i].id;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item[i].ts));
                trade.Volume = item[i].amount.ToDecimal();
                trade.Side = item[i].direction.Equals("buy") ? Side.Buy : Side.Sell;

                NewTradesEvent(trade);
            }
        }

        private void UpdateDepth(string message)
        {
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

            List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            marketDepth.SecurityNameCode = responseDepth.ch.Split('.')[1];

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

                    if (ask == 0 ||
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

            if (ascs.Count == 0 ||
                bids.Count == 0)
            {
                return;
            }

            marketDepth.Asks = ascs;
            marketDepth.Bids = bids;
            marketDepth.Time
                = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.ts));

            if (marketDepth.Time < _lastTimeMd)
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

        private string GetSecurityName(string ch)
        {
            string[] strings = ch.Split('.');
            return strings[1];
        }
       
        private void UpdateMyTrade(ResponseChannelUpdateOrder response)
        {           
            for (int i = 0; i < response.trade.Count; i++)
            {
                MyTrade myTrade = new MyTrade();

                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(response.trade[i].created_at));
                myTrade.NumberOrderParent = response.order_id;
                myTrade.NumberTrade = response.trade[i].id;
                myTrade.Price = response.trade[i].trade_price.ToDecimal();
                myTrade.SecurityNameCode = JoinSecurityName(response.symbol, response.contract_type); 
                myTrade.Side = response.direction.Equals("buy") ? Side.Buy : Side.Sell;
                myTrade.Volume = response.trade[i].trade_volume.ToDecimal();

                MyTradeEvent(myTrade);
            }            
        }

        private void UpdateOrder(string message)
        {
            ResponseChannelUpdateOrder response = JsonConvert.DeserializeObject<ResponseChannelUpdateOrder>(message);
         
            if (response == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(response.order_id))
            {
                return;
            }

            Order newOrder = new Order();
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(response.ts));
            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(response.created_at));
            newOrder.ServerType = ServerType.HTXFutures;
            newOrder.SecurityNameCode = JoinSecurityName(response.symbol, response.contract_type);

            try
            {
                newOrder.NumberUser = Convert.ToInt32(response.client_order_id);
            }
            catch
            {
                // ignore
            }

            newOrder.NumberMarket = response.order_id.ToString();

            newOrder.Side = response.direction.Equals("buy") ? Side.Buy : Side.Sell;
            newOrder.State = GetOrderState(response.status);
            newOrder.Volume = response.volume.ToDecimal();
            newOrder.Price = response.price.ToDecimal();
            newOrder.PortfolioNumber = $"HTXFuturesPortfolio";
            newOrder.PositionConditionType = response.offset == "open" ? OrderPositionConditionType.Open : OrderPositionConditionType.Close;

            MyOrderEvent(newOrder);

            if (response.trade != null)
            {
                UpdateMyTrade(response);
            }           
        }               

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("1"):
                    stateType = OrderStateType.Pending;
                    break;
                case ("2"):
                    stateType = OrderStateType.Pending;
                    break;                
                case ("3"):
                    stateType = OrderStateType.Activ;
                    break;
                case ("4"):
                    stateType = OrderStateType.Patrial;
                    break;
                case ("5"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("6"):
                    stateType = OrderStateType.Done; 
                    break;
                case ("7"):
                    stateType = OrderStateType.Cancel;
                    break;
                default:
                    stateType = OrderStateType.None;
                    break;
            }
            return stateType;
        }

        private void UpdatePortfolioFromSubscrible(string message)
        {
            ResponseChannelPortfolio response = JsonConvert.DeserializeObject<ResponseChannelPortfolio>(message);

            List<ResponseChannelPortfolio.Data> item = response.data;

            if (item == null)
            {
                return;
            }

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "HTXFuturesPortfolio";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            if (item.Count != 0) 
            {
                for (int i = 0; i < item.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "HTXFuturesPortfolio";
                    pos.SecurityNameCode = item[i].symbol;
                    pos.ValueBlocked = item[i].margin_frozen.ToDecimal();
                    pos.ValueCurrent = item[i].margin_balance.ToDecimal();

                    portfolio.SetNewPosition(pos);
                }
            }
            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void UpdatePositionFromSubscrible(string message)
        {
            ResponseChannelUpdatePositions response = JsonConvert.DeserializeObject<ResponseChannelUpdatePositions>(message);

            List<ResponseChannelUpdatePositions.Data> item = response.data;

            if (item == null)
            {
                return;
            }

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "HTXFuturesPortfolio";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            if (item.Count != 0)
            {
                for (int i = 0; i < item.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "HTXFuturesPortfolio";
                    pos.SecurityNameCode = JoinSecurityName(item[i].symbol, item[i].contract_type);
                    pos.ValueBlocked = item[i].frozen.ToDecimal();
                    pos.ValueCurrent = item[i].volume.ToDecimal();

                    portfolio.SetNewPosition(pos);
                }
            }
            PortfolioEvent(new List<Portfolio> { portfolio });
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
                string contractData = order.SecurityNameCode.Split('_')[1];

                string contractType = "quarter";

                if (contractData == "CW")
                {
                    contractType = "this_week";
                }
                else if (contractData == "NW")
                {
                    contractType = "next_week";
                }

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("symbol", order.SecurityNameCode.Split('_')[0]);
                jsonContent.Add("contract_type", contractType);
                jsonContent.Add("client_order_id", order.NumberUser.ToString());
                jsonContent.Add("price", order.Price.ToString().Replace(",", "."));
                jsonContent.Add("volume", order.Volume.ToString().Replace(",", "."));
                jsonContent.Add("direction", order.Side == Side.Buy ? "buy" : "sell");

                if (order.PositionConditionType == OrderPositionConditionType.Close)
                {
                    jsonContent.Add("offset", "close");
                }
                else
                {
                    jsonContent.Add("offset", "open");
                }

                jsonContent.Add("lever_rate", "10");
                jsonContent.Add("order_price_type", "limit");

                string url = _privateUriBuilder.Build("POST", "/api/v1/contract_order");
                          
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);
                                
                PlaceOrderResponse orderResponse = JsonConvert.DeserializeObject<PlaceOrderResponse>(responseMessage.Content);

                if (responseMessage.Content.Contains("error"))
                {
                    order.State = OrderStateType.Fail;
                    MyOrderEvent(order);
                    SendLogMessage($"SendOrder. Error: {responseMessage.Content}", LogMessageType.Error);
                }
                else
                {
                    order.NumberMarket = orderResponse.data.order_id;
                    order.State = OrderStateType.Pending;
                    MyOrderEvent(order);
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

        public void CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            if (order.State == OrderStateType.Cancel)
            {
                return;
            }
            try
            {
                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("order_id", order.NumberMarket);
                jsonContent.Add("symbol", order.SecurityNameCode.Split('_')[0]);

                string url = _privateUriBuilder.Build("POST", $"/api/v1/contract_cancel");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
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

        public void CancelAllOrders()
        {           
        }

        public void CancelAllOrdersToSecurity(Security security)
        {            
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
                string url = _privateUriBuilder.Build("POST", "/api/v1/contract_openorders");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);                        
                IRestResponse responseMessage = client.Execute(request);

                ResponseMessageAllOrders response = JsonConvert.DeserializeObject<ResponseMessageAllOrders>(responseMessage.Content);

                List<ResponseMessageAllOrders.Orders> item = response.data.orders;

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
                            Order newOrder = new Order();

                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].update_time));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].created_at));
                            newOrder.ServerType = ServerType.HTXFutures;
                            newOrder.SecurityNameCode = JoinSecurityName(item[i].symbol, item[i].contract_type);

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(item[i].client_order_id);
                            }
                            catch
                            {
                                // ignore
                            }

                            newOrder.NumberMarket = item[i].order_id.ToString();
                            newOrder.Side = item[i].direction.Equals("buy") ? Side.Buy : Side.Sell;
                            newOrder.State = GetOrderState(item[i].status);
                            newOrder.Volume = item[i].volume.ToDecimal();
                            newOrder.Price = item[i].price.ToDecimal();
                            newOrder.PortfolioNumber = $"HTXFuturesPortfolio";
                            newOrder.PositionConditionType = item[i].offset == "open" ? OrderPositionConditionType.Open : OrderPositionConditionType.Close;

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
            Order orderFromExchange = GetOrderFromExchange(order.SecurityNameCode, order.NumberMarket);

            if (orderFromExchange == null)
            {
                return;
            }

            Order orderOnMarket = null;

            if (order.NumberUser != 0
                && orderFromExchange.NumberUser != 0
                && orderFromExchange.NumberUser == order.NumberUser)
            {
                orderOnMarket = orderFromExchange;                    
            }

            if (string.IsNullOrEmpty(order.NumberMarket) == false
                && order.NumberMarket == orderFromExchange.NumberMarket)
            {
                orderOnMarket = orderFromExchange;                   
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
                    = GetMyTradesBySecurity(order.SecurityNameCode, order.NumberMarket, order.TimeCreate);

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

        private Order GetOrderFromExchange(string securityNameCode, string numberMarket)
        {
            Order newOrder = new Order();

            try
            {
                Dictionary<string, string> jsonContent = new Dictionary<string, string>();
                
                jsonContent.Add("order_id", numberMarket);
                jsonContent.Add("symbol", securityNameCode.Split('_')[0]);

                string url = _privateUriBuilder.Build("POST", "/api/v1/contract_order_info");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                ResponseMessageGetOrder response = JsonConvert.DeserializeObject<ResponseMessageGetOrder>(responseMessage.Content);

                List<ResponseMessageGetOrder.Data> item = response.data;

                if (responseMessage.Content.Contains("error"))
                {
                    SendLogMessage($"GetAllOrder. Http State Code: {responseMessage.Content}", LogMessageType.Error);
                }
                else
                {
                    if (item != null && item.Count > 0)
                    { 
                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[0].created_at));
                        newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[0].created_at));
                        newOrder.ServerType = ServerType.HTXFutures;
                        newOrder.SecurityNameCode = JoinSecurityName(item[0].symbol, item[0].contract_type);
                        newOrder.NumberUser = Convert.ToInt32(item[0].client_order_id);
                        newOrder.NumberMarket = item[0].order_id.ToString();
                        newOrder.Side = item[0].direction.Equals("buy") ? Side.Buy : Side.Sell;
                        newOrder.State = GetOrderState(item[0].status);

                        if(newOrder.State == OrderStateType.Done)
                        {
                            newOrder.TimeDone = newOrder.TimeCreate;
                        }
                        else if (newOrder.State == OrderStateType.Done)
                        {
                            newOrder.TimeCancel = newOrder.TimeCreate;
                        }

                        newOrder.Volume = item[0].volume.ToDecimal();
                        newOrder.Price = item[0].price.ToDecimal();
                        newOrder.PortfolioNumber = $"HTXFuturesPortfolio";
                        newOrder.PositionConditionType = item[0].offset == "open" ? OrderPositionConditionType.Open : OrderPositionConditionType.Close;
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return newOrder;
        }

        private List<MyTrade> GetMyTradesBySecurity(string security, string orderId, DateTime createdOrderTime)
        {
            try
            {
                Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>();

                jsonContent.Add("symbol", security.Split('_')[0]);
                jsonContent.Add("order_id", Convert.ToInt64(orderId));
                jsonContent.Add("created_at", TimeManager.GetTimeStampMilliSecondsToDateTime(createdOrderTime));
                jsonContent.Add("order_type", 1);

                string url = _privateUriBuilder.Build("POST", "/api/v1/contract_order_detail");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                string respString = responseMessage.Content;                

                if (!respString.Contains("error"))
                {                      
                    ResponseMessageGetMyTradesBySecurity orderResponse = JsonConvert.DeserializeObject<ResponseMessageGetMyTradesBySecurity>(respString);

                    List<MyTrade> osEngineOrders = new List<MyTrade>();

                    if (orderResponse.data.trades != null && orderResponse.data.trades.Count > 0)
                    {
                        for (int i = 0; i < orderResponse.data.trades.Count; i++)
                        {
                            MyTrade newTrade = new MyTrade();
                            newTrade.SecurityNameCode = orderResponse.data.symbol;
                            newTrade.NumberTrade = orderResponse.data.trades[i].trade_id;
                            newTrade.NumberOrderParent = orderResponse.data.order_id;
                            newTrade.Volume = orderResponse.data.trades[i].trade_volume.ToDecimal();
                            newTrade.Price = orderResponse.data.trades[i].trade_price.ToDecimal();
                            newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderResponse.data.trades[i].created_at));

                            if (orderResponse.data.direction == "buy")
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
                else if (responseMessage.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    SendLogMessage("Get all orders request error. ", LogMessageType.Error);

                    if (responseMessage.Content != null)
                    {
                        SendLogMessage("Fail reasons: "
                      + responseMessage.Content, LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
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

            string topic = $"market.{security.Name}.depth.step0";
            _webSocketPublic.Send($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");
            _arrayPublicChannels.Add(topic);

            topic = $"market.{security.Name}.trade.detail";
            _webSocketPublic.Send($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");
            _arrayPublicChannels.Add(topic);                        
        }

        private void SendSubscriblePrivate()
        {          
            string clientId = "";
            string channelOrders = "orders.*";
            string channelAccounts = "accounts.*";
            string channelPositions = "positions.*";
            _webSocketPrivate.Send($"{{\"op\":\"sub\", \"topic\":\"{channelOrders}\", \"cid\": \"{clientId}\" }}");
            _webSocketPrivate.Send($"{{\"op\":\"sub\", \"topic\":\"{channelAccounts}\", \"cid\": \"{clientId}\" }}");
            _webSocketPrivate.Send($"{{\"op\":\"sub\", \"topic\":\"{channelPositions}\", \"cid\": \"{clientId}\" }}");
            _arrayPrivateChannels.Add(channelAccounts);
            _arrayPrivateChannels.Add(channelOrders);
            _arrayPrivateChannels.Add(channelPositions);           
        }

        private void CreatePingMessageWebSocketPublic(string message)
        {
            ResponsePingPublic response = JsonConvert.DeserializeObject<ResponsePingPublic>(message);

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
            ResponsePingPrivate response = JsonConvert.DeserializeObject<ResponsePingPrivate>(message);

            if (_webSocketPrivate == null)
            {
                return;
            }
            else
            {                
                _webSocketPrivate.Send($"{{\"pong\": \"{response.ts}\"}}");
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
                string url = _privateUriBuilder.Build("POST", $"/api/v1/contract_account_info");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
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
            List<ResponseMessagePortfolios.Data> item = response.data;

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "HTXFuturesPortfolio";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            for (int i = 0; i < item.Count; i++)
            {
                if (item[i].margin_balance == "0")
                {
                    continue;
                }
                PositionOnBoard pos = new PositionOnBoard();
                pos.PortfolioName = "HTXFuturesPortfolio";
                pos.SecurityNameCode = item[i].symbol.ToString();
                pos.ValueBlocked = item[i].margin_frozen.ToDecimal();
                pos.ValueCurrent = item[i].margin_balance.ToDecimal();

                portfolio.SetNewPosition(pos);
            }

            string url = _privateUriBuilder.Build("POST", $"/api/v1/contract_position_info");

            RestClient client = new RestClient(url);
            RestRequest request = new RestRequest(Method.POST);
            IRestResponse responseMessage = client.Execute(request);

            string JsonResponse = responseMessage.Content;

            if (JsonResponse.Contains("\"status\":\"ok\""))
            {
                ResponseMessagePositions responsePosition = JsonConvert.DeserializeObject<ResponseMessagePositions>(JsonResponse);

                List<ResponseMessagePositions.Data> itemPosition = responsePosition.data;

                for (int j = 0; j < itemPosition.Count; j++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "HTXFuturesPortfolio";
                    pos.SecurityNameCode = itemPosition[j].symbol;
                    pos.ValueBlocked = itemPosition[j].frozen.ToDecimal(); 
                    pos.ValueCurrent = itemPosition[j].volume.ToDecimal();

                    if (IsUpdateValueBegin)
                    {
                        pos.ValueBegin = itemPosition[j].volume.ToDecimal();
                    }
                    portfolio.SetNewPosition(pos);
                }
                PortfolioEvent(new List<Portfolio> { portfolio });                      
            }
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
            request.AddParam("AccessKeyId", _accessKey);
            request.AddParam("SignatureMethod", "HmacSHA256");
            request.AddParam("SignatureVersion", "2");
            request.AddParam("Timestamp", strDateTime);

            string signature = _signer.Sign("GET", _baseUrl, _webSocketPathPrivate, request.BuildParams());

            WebSocketAuthenticationRequestFutures auth = new WebSocketAuthenticationRequestFutures();            
            auth.AccessKeyId = _accessKey;
            auth.Signature = signature;
            auth.Timestamp = strDateTime;
            
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