using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using WebSocket4Net;
using OsEngine.Market.Servers.Deribit.Entity;
using System.Security.Cryptography;

// API doc - https://docs.deribit.com/

namespace OsEngine.Market.Servers.Deribit
{
    public class DeribitServer : AServer
    {
        public DeribitServer()
        {
            DeribitServerRealization realization = new DeribitServerRealization();
            ServerRealization = realization;

            CreateParameterString("Client ID", "");
            CreateParameterString("Client Secret", "");
            CreateParameterEnum("Server", "Real", new List<string> { "Real", "Test" }); // можно выбрать сервер - реальный или тестовый, на каждам свои аккауты и api ключи
            CreateParameterBoolean("Post Only for Limit Orders", false);
        }
    }

    public class DeribitServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public DeribitServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadMessageReader = new Thread(MessageReader);
            threadMessageReader.IsBackground = true;
            threadMessageReader.Name = "MessageReaderDeribit";
            threadMessageReader.Start();

            Thread threadCheckAliveWebsocket = new Thread(CheckAliveWebSocket);
            threadCheckAliveWebsocket.IsBackground = true;
            threadCheckAliveWebsocket.Name = "CheckAliveWebSocket";
            threadCheckAliveWebsocket.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            _clientID = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterString)ServerParameters[1]).Value;
            if (((ServerParameterEnum)ServerParameters[2]).Value == "Real")
            {
                _baseUrl = "https://www.deribit.com";
                _webSocketUrl = "wss://www.deribit.com/ws/api/v2";
            }
            else
            {
                _baseUrl = "https://test.deribit.com";
                _webSocketUrl = "wss://test.deribit.com/ws/api/v2";
            }

            if (((ServerParameterBool)ServerParameters[3]).Value == true)
            {
                _postOnly = "true";
            }
            else
            {
                _postOnly = "false";
            }

            HttpResponseMessage responseMessage = _httpClient.GetAsync(_baseUrl + "/api/v2/public/get_currencies?").Result;
            string json = responseMessage.Content.ReadAsStringAsync().Result;
                        
            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                try
                {
                    FIFOListWebSocketMessage = new ConcurrentQueue<string>();
                    CreateWebSocketConnection();
                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    SendLogMessage("Connection can be open. Deribit. Error request", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            else
            {
                SendLogMessage("Connection can be open. Deribit. Error request", LogMessageType.Error);
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

            FIFOListWebSocketMessage = new ConcurrentQueue<string>();            
        }

        public ServerType ServerType
        {
            get { return ServerType.Deribit; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _clientID;

        private string _secretKey;

        private string _accessToken;

        private string _baseUrl;

        private string _webSocketUrl;

        private string _postOnly;

        private int _limitCandles = 5000;

        private List<string> _listCurrency = new List<string>() { "BTC", "ETH", "USDC", "USDT", "SOL", "MATIC", "XRP" }; // список валют на бирже
        
        private List<string> _arrayChannelsBook = new List<string>();
        
        private List<string> _arrayChannelsTrade = new List<string>();
        
        private List<string> _arrayChannelsAccount = new List<string>();

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            try
            {
                for (int i = 0; i < _listCurrency.Count; i++) // получаем список бумаг по всем валютам
                {
                    string stringApiRequests = $"/api/v2/public/get_instruments?currency={_listCurrency[i]}";

                    HttpResponseMessage responseMessage = _httpClient.GetAsync(_baseUrl + stringApiRequests).Result;
                    string json = responseMessage.Content.ReadAsStringAsync().Result;

                    if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        UpdateSecurity(json);
                    }
                    else
                    {
                        SendLogMessage($"Http State Code: {responseMessage.StatusCode}, {json}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecurity(string json)
        {
            ResponseMessageSecurities response = JsonConvert.DeserializeObject<ResponseMessageSecurities>(json);

            List<Security> securities = new List<Security>();

            for (int i = 0; i < response.result.Count; i++)
            {
                ResponseMessageSecurities.Result item = response.result[i];

                if (item.is_active == "true")
                {
                    if (GetSecurityType(item.kind) == SecurityType.None)
                    {
                        continue;
                    }
                    Security newSecurity = new Security();

                    newSecurity.Exchange = ServerType.Deribit.ToString();
                    newSecurity.Name = item.instrument_name;
                    newSecurity.NameFull = item.instrument_name;
                    newSecurity.NameClass = "Futures";
                    newSecurity.NameId = item.instrument_id;
                    newSecurity.SecurityType = GetSecurityType(item.kind);
                    newSecurity.DecimalsVolume = item.contract_size.DecimalsCount();
                    newSecurity.Lot = item.contract_size.ToDecimal();
                    newSecurity.PriceStep = item.tick_size.ToDecimal();
                    newSecurity.Decimals = item.tick_size.DecimalsCount();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.State = SecurityStateType.Activ;
                    newSecurity.MinTradeAmount = item.min_trade_amount.ToDecimal();

                    securities.Add(newSecurity);
                }
            }

            SecurityEvent(securities);
        }

        private SecurityType GetSecurityType(string kind)
        {
            SecurityType _securityType = SecurityType.None;

            switch (kind)
            {
                case "future":
                    _securityType = SecurityType.Futures;
                    break;
                case "spot":
                    _securityType = SecurityType.CurrencyPair;
                    break;
            }
            return _securityType;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(200));
        
        private RateGate _rateGatePositions = new RateGate(1, TimeSpan.FromMilliseconds(200));
        
        public void GetPortfolios()
        {
            for (int i = 0; i < _listCurrency.Count; i++)
            {
                CreateQueryPortfolio(true, _listCurrency[i]); // создаем портфели из списка валют
            }
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
                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTimeData);
                long to = TimeManager.GetTimeStampMilliSecondsToDateTime(endTimeData);

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

                startTimeData = endTimeData.AddMinutes(tfTotalMinutes);
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
                timeFrameMinutes == 3 ||
                timeFrameMinutes == 5 ||
                timeFrameMinutes == 15 ||
                timeFrameMinutes == 30 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 120 ||
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
                return $"{timeFrame.Minutes}";
            }
            else
            {
                return $"{timeFrame.Hours * 60}";
            }
        }

        private readonly RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private List<Candle> RequestCandleHistory(string security, string resolution, long fromTimeStamp, long toTimeStamp)
        {
            _rgCandleData.WaitToProceed(100);

            try
            {
                string queryParam = $"instrument_name={security}&";
                queryParam += $"resolution={resolution}&";
                queryParam += $"start_timestamp={fromTimeStamp}&";
                queryParam += $"end_timestamp={toTimeStamp}";

                string requestUri = _baseUrl + $"/api/v2/public/get_tradingview_chart_data?" + queryParam;

                HttpResponseMessage responseMessage = _httpClient.GetAsync(requestUri).Result;
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return ConvertCandles(json);
                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode} - {json}", LogMessageType.Error);
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

            ResponseMessageCandles.Result item = response.result;

            for (int i = 0; i < item.ticks.Count; i++)
            {

                if (CheckCandlesToZeroData(item, i))
                {
                    continue;
                }

                Candle candle = new Candle();

                candle.State = CandleState.Finished;
                candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.ticks[i]));
                candle.Volume = item.volume[i].ToDecimal();
                candle.Close = item.close[i].ToDecimal();
                candle.High = item.high[i].ToDecimal();
                candle.Low = item.low[i].ToDecimal();
                candle.Open = item.open[i].ToDecimal();

                candles.Add(candle);
            }
            return candles;
        }

        private bool CheckCandlesToZeroData(ResponseMessageCandles.Result item, int i)
        {
            if (item.close[i].ToDecimal() == 0 ||
                item.open[i].ToDecimal() == 0 ||
                item.high[i].ToDecimal() == 0 ||
                item.low[i].ToDecimal() == 0)
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

        private WebSocket webSocket;

        private void CreateWebSocketConnection()
        {
            webSocket = new WebSocket(_webSocketUrl);            
            webSocket.Opened += WebSocket_Opened;
            webSocket.Closed += WebSocket_Closed;
            webSocket.MessageReceived += WebSocket_MessageReceived;
            webSocket.Error += WebSocket_Error;
            webSocket.Open();

        }

        private void DeleteWebscoektConnection()
        {
            if (webSocket != null)
            {
                try
                {
                    webSocket.Close();
                }
                catch
                {
                    // ignore
                }

                webSocket.Opened -= WebSocket_Opened;
                webSocket.Closed -= WebSocket_Closed;
                webSocket.MessageReceived -= WebSocket_MessageReceived;
                webSocket.Error -= WebSocket_Error;
                webSocket = null;
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocket_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            SuperSocket.ClientEngine.ErrorEventArgs error = (SuperSocket.ClientEngine.ErrorEventArgs)e;
            if (error.Exception != null)
            {
                SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_MessageReceived(object sender, MessageReceivedEventArgs e)
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

                if (FIFOListWebSocketMessage == null)
                {
                    return;
                }

                FIFOListWebSocketMessage.Enqueue(e.Message);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_Closed(object sender, EventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Connection Closed by Deribit. WebSocket Closed Event", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void WebSocket_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Connection Open", LogMessageType.System);
            ServerStatus = ServerConnectStatus.Connect;
            ConnectEvent();
            CreateAuthMessageWebSocket();
        }

        #endregion

        #region 8 WebSocket check alive

        private DateTime _timeLastSendPing = DateTime.Now;

        private DateTime _timeLastSendAccessToken = DateTime.Now;

        private void CheckAliveWebSocket()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(3000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (_timeLastSendPing.AddSeconds(30) < DateTime.Now)
                    {
                        JsonRequest jsonRequest = new JsonRequest();
                        jsonRequest.id = 0;
                        jsonRequest.method = "public/test";
                        jsonRequest.@params = new JsonRequest.Params();
                        
                        string json = JsonConvert.SerializeObject(jsonRequest);

                        webSocket.Send(json);

                        _timeLastSendPing = DateTime.Now;
                    }

                    if (_timeLastSendAccessToken.AddSeconds(800) < DateTime.Now)
                    {
                        CreateAuthMessageWebSocket();
                        _timeLastSendAccessToken = DateTime.Now;
                    }
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

        private ConcurrentQueue<string> FIFOListWebSocketMessage = new ConcurrentQueue<string>();

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

                    if (FIFOListWebSocketMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    FIFOListWebSocketMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (message.Contains("access_token"))
                        {
                            ResponseWebSocketMessageAuth response = JsonConvert.DeserializeObject<ResponseWebSocketMessageAuth>(message);

                            if (response.result.access_token != null)
                            {
                                _accessToken = response.result.access_token;
                                continue;
                            }
                        }

                        ResponseWebSocketMessage<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                        if (action.method == "subscription")
                        {
                            if (_arrayChannelsBook.Contains(action.@params.channel))
                            {
                                UpdateDepth(message);
                                continue;
                            }

                            if (_arrayChannelsTrade.Contains(action.@params.channel))
                            {
                                UpdateTrade(message);
                                continue;
                            }

                            if (_arrayChannelsAccount.Contains(action.@params.channel))
                            {
                                UpdatePortfolioFromSubscrible(message);
                                continue;
                            }

                            if (action.@params.channel.Equals("user.changes.any.any.raw"))
                            {
                                ResponseChannelUserChanges response = JsonConvert.DeserializeObject<ResponseChannelUserChanges>(message);

                                if (response.@params.data.orders != null)
                                {
                                    if (response.@params.data.orders.Count != 0)
                                    {
                                        UpdateOrder(message);
                                    }
                                }

                                if (response.@params.data.trades != null)
                                {
                                    if (response.@params.data.trades.Count != 0)
                                    {
                                        UpdateMytrade(message);
                                    }
                                }

                                if (response.@params.data.positions != null)
                                {
                                    if (response.@params.data.positions.Count != 0)
                                    {
                                        UpdatePositionFromSubscrible(message);
                                    }
                                }
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

            if (responseTrade.@params.data == null)
            {
                return;
            }

            if (responseTrade.@params.data.Count == 0)
            {
                return;
            }

            if (responseTrade.@params.data[0] == null)
            {
                return;
            }

            List<ResponseChannelTrades.Data> item = responseTrade.@params.data;

            for (int i = 0; i < item.Count; i++)
            {
                Trade trade = new Trade();
                trade.SecurityNameCode = item[i].instrument_name;
                trade.Price = item[i].price.ToDecimal();
                trade.Id = item[i].trade_id;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item[i].timestamp));
                trade.Volume = item[i].amount.ToDecimal();
                trade.Side = item[i].direction.Equals("buy") ? Side.Buy : Side.Sell;

                NewTradesEvent(trade);
            }
        }

        private void UpdateDepth(string message)
        {
            Thread.Sleep(1);

            ResponseChannelBook responseDepth = JsonConvert.DeserializeObject<ResponseChannelBook>(message);

            ResponseChannelBook.Data item = responseDepth.@params.data;

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

            marketDepth.SecurityNameCode = item.instrument_name;

            if (item.asks.Count > 0)
            {
                for (int i = 0; i < item.asks.Count; i++)
                {
                    MarketDepthLevel level = new MarketDepthLevel();
                    level.Ask = item.asks[i][1].ToString().ToDecimal();
                    level.Price = item.asks[i][0].ToString().ToDecimal();
                    ascs.Add(level);                                        
                }
            }

            if (item.bids.Count > 0)
            {
                for (int i = 0; i < item.bids.Count; i++)
                {
                    MarketDepthLevel level = new MarketDepthLevel();
                    level.Bid = item.bids[i][1].ToString().ToDecimal();
                    level.Price = item.bids[i][0].ToString().ToDecimal();
                    bids.Add(level);
                }
            }

            marketDepth.Asks = ascs;
            marketDepth.Bids = bids;
            marketDepth.Time = DateTime.UtcNow;

            MarketDepthEvent(marketDepth);
        }

        private void UpdateMytrade(string message)
        {
            ResponseChannelUserChanges response = JsonConvert.DeserializeObject<ResponseChannelUserChanges>(message);

            List<ResponseChannelUserChanges.Trades> item = response.@params.data.trades;

            for (int i = 0; i < response.@params.data.trades.Count; i++)
            {
                MyTrade myTrade = new MyTrade();

                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item[i].timestamp));
                myTrade.NumberOrderParent = item[i].order_id;
                myTrade.NumberTrade = item[i].trade_id;
                myTrade.Price = item[i].price.ToDecimal();
                myTrade.SecurityNameCode = item[i].instrument_name;
                myTrade.Side = item[i].direction.Equals("buy") ? Side.Buy : Side.Sell;
                myTrade.Volume = item[i].amount.ToDecimal();

                MyTradeEvent(myTrade);
            }
        }

        private void UpdatePortfolioFromSubscrible(string message)
        {
            ResponseChannelPortfolio response = JsonConvert.DeserializeObject<ResponseChannelPortfolio>(message);

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "DeribitPortfolio";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            PositionOnBoard pos = new PositionOnBoard();

            pos.PortfolioName = "DeribitPortfolio";
            pos.SecurityNameCode = response.@params.data.currency;
            pos.ValueBlocked = 0;
            pos.ValueCurrent = response.@params.data.equity.ToDecimal();

            portfolio.SetNewPosition(pos);
            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void UpdatePositionFromSubscrible(string message)
        {
            ResponseChannelUserChanges response = JsonConvert.DeserializeObject<ResponseChannelUserChanges>(message);

            List<ResponseChannelUserChanges.Positions> item = response.@params.data.positions;

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "DeribitPortfolio";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            for (int i = 0; i < item.Count; i++)
            {
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "DeribitPortfolio";
                pos.SecurityNameCode = item[i].instrument_name;
                pos.ValueBlocked = 0;
                pos.ValueCurrent = item[i].size.ToDecimal();

                portfolio.SetNewPosition(pos);
                PortfolioEvent(new List<Portfolio> { portfolio });
            }
        }

        private void UpdateOrder(string message)
        {
            ResponseChannelUserChanges response = JsonConvert.DeserializeObject<ResponseChannelUserChanges>(message);

            List<ResponseChannelUserChanges.Orders> item = response.@params.data.orders;

            for (int i = 0; i < item.Count; i++)
            {
                OrderStateType stateType = GetOrderState(item[i].order_state);

                Order newOrder = new Order();
                newOrder.SecurityNameCode = item[i].instrument_name;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].creation_timestamp));
                newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].creation_timestamp));
               
                if (string.IsNullOrEmpty(item[i].label))
                {
                    continue;
                }

                newOrder.NumberUser = Convert.ToInt32(item[i].label);
                newOrder.NumberMarket = item[i].order_id.ToString();
                newOrder.Side = item[i].direction.Equals("buy") ? Side.Buy : Side.Sell;
                newOrder.State = stateType;
                newOrder.Volume = item[i].amount.ToDecimal();
                newOrder.VolumeExecute = item[i].filled_amount.ToDecimal();
                newOrder.Price = item[i].price.ToDecimal();

                if (item[i].order_type == "market")
                {
                    newOrder.TypeOrder = OrderPriceType.Market;
                }
                if (item[i].order_type == "limit")
                {
                    newOrder.TypeOrder = OrderPriceType.Limit;
                }
                newOrder.ServerType = ServerType.Deribit;
                newOrder.PortfolioNumber = "DeribitPortfolio";

                MyOrderEvent(newOrder);
            }
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("open"):
                    stateType = OrderStateType.Activ;
                    break;
                case ("filled"):
                    stateType = OrderStateType.Done;
                    break;
                case ("rejected"):
                    stateType = OrderStateType.Fail;
                    break;
                case ("cancelled"):
                    stateType = OrderStateType.Cancel;
                    break;
                case "untriggered":
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

            JsonRequest jsonRequest = new JsonRequest();
            jsonRequest.id = 20;
            jsonRequest.method = order.Side.ToString() == "Buy" ? "private/buy" : "private/sell";
            jsonRequest.@params = new JsonRequest.Params();
            jsonRequest.@params.instrument_name = order.SecurityNameCode;
            jsonRequest.@params.amount = order.Volume;
            jsonRequest.@params.label = order.NumberUser.ToString();
            jsonRequest.@params.price = order.Price.ToString().ToDecimal();

            if (order.TypeOrder == OrderPriceType.Limit)
            {
                jsonRequest.@params.type = "limit";                
                jsonRequest.@params.post_only = _postOnly;
            }
            else
            {
                jsonRequest.@params.type = "market";
            }            

            string json = JsonConvert.SerializeObject(jsonRequest);
           
            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v2/", "POST", json);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                ResponseMessageSendOrder response = JsonConvert.DeserializeObject<ResponseMessageSendOrder>(JsonResponse);
                if (response.result.order.order_state == "open" || response.result.order.order_state == "filled")
                {
                    order.NumberMarket = response.result.order.order_id;
                }
            }
            else
            {
                order.State = OrderStateType.Fail;
                MyOrderEvent(order);
                SendLogMessage($"SendOrder. Http State Code: Http State Code: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            _rateGateSendOrder.WaitToProceed();
          
            JsonRequest jsonRequest = new JsonRequest();
            jsonRequest.id = 21;
            jsonRequest.method = "private/edit";
            jsonRequest.@params = new JsonRequest.Params();
            jsonRequest.@params.order_id = order.NumberMarket;
            jsonRequest.@params.price = newPrice.ToString().ToDecimal();
            jsonRequest.@params.amount = order.Volume;

            string json = JsonConvert.SerializeObject(jsonRequest);

            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v2/", "POST", json);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

            if (responseMessage.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage($"ChangeOrderPrice FAIL. Http State Code: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.System);
            }
        }

        public void CancelAllOrders()
        {
            _rateGateCancelOrder.WaitToProceed();

            JsonRequest jsonRequest = new JsonRequest();
            jsonRequest.id = 52;
            jsonRequest.method = "private/cancel_all";
            jsonRequest.@params = new JsonRequest.Params();
            
            string json = JsonConvert.SerializeObject(jsonRequest);

            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v2/", "POST", json);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

            if (responseMessage.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage($"CancelAllOrders. Http State Code: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.Error);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            _rateGateCancelOrder.WaitToProceed();

            JsonRequest jsonRequest = new JsonRequest();
            jsonRequest.id = 51;
            jsonRequest.method = "private/cancel_all_by_instrument";
            jsonRequest.@params = new JsonRequest.Params();
            jsonRequest.@params.instrument_name = security.Name;
            jsonRequest.@params.type = "all";

            string json = JsonConvert.SerializeObject(jsonRequest);

            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v2/", "POST", json);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

            if (responseMessage.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage($"CancelAllOrdersToSecurity. Http State Code: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.Error);
            }
        }

        public void CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            if (OrderStateType.Cancel == order.State)
            {
                return;
            }

            JsonRequest jsonRequest = new JsonRequest();
            jsonRequest.id = 50;
            jsonRequest.method = "private/cancel";
            jsonRequest.@params = new JsonRequest.Params();
            jsonRequest.@params.order_id = order.NumberMarket;

            string json = JsonConvert.SerializeObject(jsonRequest);

            HttpResponseMessage responseMessage = CreatePrivateQuery("/api/v2/", "POST", json);
            string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

            if (responseMessage.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage($"CancelOrder. Http State Code: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.Error);
            }
            else
            {
                order.State = OrderStateType.Cancel;
                MyOrderEvent(order);
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
                for (int i = 0; i < _listCurrency.Count; i++)
                {
                    string stringPositionRequests = $"/api/v2/private/get_open_orders_by_currency?currency={_listCurrency[i]}";

                    HttpResponseMessage responseMessage = CreatePrivateQuery(stringPositionRequests, "GET", null);
                    string json = responseMessage.Content.ReadAsStringAsync().Result;

                    ResponseMessageAllOrders response = JsonConvert.DeserializeObject<ResponseMessageAllOrders>(json);

                    List<ResponseMessageAllOrders.Result> item = response.result;

                    if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        if (item != null && item.Count > 0)
                        {
                            for (int j = 0; j < item.Count; j++)
                            {
                                Order newOrder = new Order();

                                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[j].last_update_timestamp));
                                newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[j].creation_timestamp));
                                newOrder.ServerType = ServerType.Deribit;
                                newOrder.SecurityNameCode = item[j].instrument_name;
                                try
                                {
                                    newOrder.NumberUser = Convert.ToInt32(item[j].label);
                                }
                                catch
                                {
                                    // ignore
                                }                    
                                newOrder.NumberMarket = item[j].order_id.ToString();
                                newOrder.Side = item[j].direction.Equals("buy") ? Side.Buy : Side.Sell;
                                newOrder.State = GetOrderState(item[j].order_state);
                                newOrder.Volume = item[j].amount.ToDecimal();
                                newOrder.Price = item[j].price.ToDecimal();
                                newOrder.PortfolioNumber = "DeribitPortfolio";

                                orders.Add(newOrder);
                            }
                        }                        
                    }
                    else
                    {
                        SendLogMessage($"GetAllOrder. Http State Code: {responseMessage.Content}", LogMessageType.Error);
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
            Order orderFromExchange = GetOrderFromExchange(order.NumberMarket);

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
                    = GetMyTradesBySecurity(order.NumberMarket);

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
                string stringPositionRequests = $"/api/v2/private/get_order_state?order_id={numberMarket}";

                HttpResponseMessage responseMessage = CreatePrivateQuery(stringPositionRequests, "GET", null);
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                ResponseMessageGetOrder response = JsonConvert.DeserializeObject<ResponseMessageGetOrder>(json);

                ResponseMessageGetOrder.Result item = response.result;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.last_update_timestamp));
                    newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.creation_timestamp));
                    newOrder.ServerType = ServerType.Deribit;
                    newOrder.SecurityNameCode = item.instrument_name;
                    newOrder.NumberUser = Convert.ToInt32(item.label);
                    newOrder.NumberMarket = item.order_id.ToString();
                    newOrder.Side = item.direction.Equals("buy") ? Side.Buy : Side.Sell;
                    newOrder.State = GetOrderState(item.order_state);
                    newOrder.Volume = item.amount.ToDecimal();
                    newOrder.Price = item.price.ToDecimal();
                    newOrder.PortfolioNumber = "DeribitPortfolio";
                }
                else
                {
                    SendLogMessage($"GetOrderState. Http State Code: {responseMessage.Content}", LogMessageType.Error);
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
                string stringPositionRequests = $"/api/v2/private/get_user_trades_by_order?order_id={orderId}";

                HttpResponseMessage responseMessage = CreatePrivateQuery(stringPositionRequests, "GET", null);
                string json = responseMessage.Content.ReadAsStringAsync().Result;
                                
                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseMessageGetMyTradesBySecurity orderResponse = JsonConvert.DeserializeObject<ResponseMessageGetMyTradesBySecurity>(json);

                    List<ResponseMessageGetMyTradesBySecurity.Result> item = orderResponse.result;

                    List<MyTrade> osEngineOrders = new List<MyTrade>();

                    if (item != null && item.Count > 0)
                    {
                        for (int i = 0; i < item.Count; i++)
                        {
                            MyTrade newTrade = new MyTrade();
                            newTrade.SecurityNameCode = item[i].instrument_name;
                            newTrade.NumberTrade = item[i].trade_id;
                            newTrade.NumberOrderParent = item[i].order_id;
                            newTrade.Volume = item[i].amount.ToDecimal();
                            newTrade.Price = item[i].price.ToDecimal();
                            newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item[i].timestamp));

                            if (item[i].direction == "buy")
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

        HttpClient _httpClient = new HttpClient();

        private List<string> _subscribledSecurities = new List<string>();

        private void CreateSubscribleSecurityMessageWebSocket(Security security)
        {
            if (webSocket == null)
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

            _arrayChannelsBook.Add($"book.{security.Name}.none.20.100ms"); // массив каналов для получения стаканов
            _arrayChannelsTrade.Add($"trades.{security.Name}.raw"); // массив каналов для получения сделок

            List<string> arrayChannels = new List<string> { "user.changes.any.any.raw" }; // собираем все каналы со всех массивов в один массив

            for (int i = 0; i < _arrayChannelsAccount.Count; i++)
            {
                if (!arrayChannels.Contains(_arrayChannelsAccount[i]))
                {
                    arrayChannels.Add(_arrayChannelsAccount[i]);
                }
            }

            for (int i = 0; i < _arrayChannelsBook.Count; i++)
            {
                if (!arrayChannels.Contains(_arrayChannelsBook[i]))
                {
                    arrayChannels.Add(_arrayChannelsBook[i]);
                }
            }

            for (int i = 0; i < _arrayChannelsTrade.Count; i++)
            {
                if (!arrayChannels.Contains(_arrayChannelsTrade[i]))
                {
                    arrayChannels.Add(_arrayChannelsTrade[i]);
                }
            }
            if (arrayChannels.Count > 800)
            {
                return;
            }
            SendSubscrible(arrayChannels);
        }

        private void SendSubscrible(List<string> arrayChannels)
        {
            JsonRequest jsonRequest = new JsonRequest();
            jsonRequest.id = 4;
            jsonRequest.method = "public/subscribe";
            jsonRequest.@params = new JsonRequest.Params();
            jsonRequest.@params.channels = arrayChannels;
            jsonRequest.@params.access_token = _accessToken;

            string json = JsonConvert.SerializeObject(jsonRequest);

            webSocket.Send(json);
        }

        private void CreateAuthMessageWebSocket()
        {
            if (webSocket == null)
            {
                return;
            }

            long timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            string nonce = "abcd";
            string data = "";
            string signature = GenerateSignature(_secretKey, timestamp, nonce, data);

            JsonRequest jsonRequest = new JsonRequest();
            jsonRequest.id = 8748;
            jsonRequest.method = "public/auth";
            jsonRequest.@params = new JsonRequest.Params();
            jsonRequest.@params.grant_type = "client_signature";
            jsonRequest.@params.client_id = _clientID;
            jsonRequest.@params.timestamp = timestamp;
            jsonRequest.@params.signature = signature;
            jsonRequest.@params.nonce = nonce;
            jsonRequest.@params.data = data;

            string json = JsonConvert.SerializeObject(jsonRequest);

            webSocket.Send(json);
        }

        private void UnsubscribeFromAllWebSockets()
        {
            if (webSocket == null)
            {
                return;
            }

            JsonRequest jsonRequest = new JsonRequest();
            jsonRequest.id = 6;
            jsonRequest.method = "public/unsubscribe_all";
            jsonRequest.@params = new JsonRequest.Params();
           
            string json = JsonConvert.SerializeObject(jsonRequest);

            webSocket.Send(json);
        }

        private void CreateQueryPortfolio(bool IsUpdateValueBegin, string currency)
        {
            _rateGatePortfolio.WaitToProceed();

            _arrayChannelsAccount.Add($"user.portfolio.{currency.ToLower()}"); // массив каналов с портфелями

            try
            {
                string stringPositionRequests = $"/api/v2/private/get_account_summary?currency={currency}";

                HttpResponseMessage responseMessage = CreatePrivateQuery(stringPositionRequests, "GET", null);
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    UpdatePorfolio(json, IsUpdateValueBegin);
                    CreateQueryPosition(true, currency);
                }

                else
                {
                    SendLogMessage($"CreateQueryPortfolio: {responseMessage.StatusCode}, {json}", LogMessageType.Error);
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
            portfolio.Number = "DeribitPortfolio";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            PositionOnBoard pos = new PositionOnBoard()
            {
                PortfolioName = "DeribitPortfolio",
                SecurityNameCode = response.result.currency,
                ValueBlocked = 0,
                ValueCurrent = response.result.equity.ToDecimal()
            };

            if (IsUpdateValueBegin)
            {
                pos.ValueBegin = response.result.equity.ToDecimal();
            }

            portfolio.SetNewPosition(pos);

            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private void CreateQueryPosition(bool IsUpdateValueBegin, string currency)
        {
            _rateGatePositions.WaitToProceed();

            try
            {
                string stringPositionRequests = $"/api/v2/private/get_positions?currency={currency}";

                HttpResponseMessage responseMessage = CreatePrivateQuery(stringPositionRequests, "GET", null);
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    UpdatePosition(json, IsUpdateValueBegin);
                }

                else
                {
                    SendLogMessage($"CreateQueryPosition: {responseMessage.StatusCode}, {json}", LogMessageType.Error);
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
            portfolio.Number = "DeribitPortfolio";
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            for (int i = 0; i < response.result.Count; i++)
            {
                ResponseMessagePositions.Result item = response.result[i];
                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "DeribitPortfolio";
                pos.SecurityNameCode = item.instrument_name;
                pos.ValueBlocked = 0;
                pos.ValueCurrent = item.size.ToDecimal();

                if (IsUpdateValueBegin)
                {
                    pos.ValueBegin = item.size.ToDecimal();
                }

                portfolio.SetNewPosition(pos);
            }
            PortfolioEvent(new List<Portfolio> { portfolio });
        }

        private HttpResponseMessage CreatePrivateQuery(string requestPath, string method, string requestBody)
        {
            long timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            string nonce = "abcd";
            string requestData = $"{method}\n{requestPath}\n{requestBody}\n";
            string data = requestData;
            string signature = GenerateSignature(_secretKey, timestamp, nonce, data);
            string url = _baseUrl + requestPath;
            string authValue = $"id={_clientID},ts={timestamp},sig={signature},nonce={nonce}";

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("deri-hmac-sha256", authValue);

            if (method.Equals("POST"))
            {
                return _httpClient.PostAsync(url, new StringContent(requestBody, Encoding.UTF8, "application/json")).Result;
            }
            else
            {
                return _httpClient.GetAsync(url).Result;
            }
        }

        static string GenerateSignature(string clientSecret, long timestamp, string nonce, string data)
        {
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(clientSecret)))
            {
                string message = $"{timestamp}\n{nonce}\n{data}";
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
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