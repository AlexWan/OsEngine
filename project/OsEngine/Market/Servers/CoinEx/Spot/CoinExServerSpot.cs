using System;
using System.Collections.Generic;
using System.Text;
using OsEngine.Language;
using OsEngine.Market.Servers.Entity;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;
using System.Security.Cryptography;
using OsEngine.Market.Servers.CoinEx.Spot.Entity;
using OsEngine.Market.Servers.CoinEx.Spot.Entity.Enums;
using WebSocket4Net;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.IO;
using Newtonsoft.Json;
using System.Net.Http;

namespace OsEngine.Market.Servers.CoinEx.Spot
{
    public class CoinExServerSpot : AServer
    {
        public CoinExServerSpot()
        {
            CoinExServerRealization realization = new CoinExServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterEnum("Market depth", "20", new List<string> { "5", "10", "20", "50" });
        }
    }

    public class CoinExServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection
        public ServerConnectStatus ServerStatus { get; set; }
        public event Action ConnectEvent;
        public event Action DisconnectEvent;
        public DateTime ServerTime { get; set; }
        public ServerType ServerType
        {
            get { return ServerType.CoinExSpot; }
        }


        public CoinExServerRealization()
        {
            Thread worker = new Thread(ConnectionCheckThread);
            worker.Name = "CheckAliveCoinEx";
            worker.Start();

            Thread worker2 = new Thread(DataMessageReaderThread);
            worker2.Name = "DataMessageReaderCoinEx";
            worker2.Start();
        }

        public void Connect()
        {
            try
            {
                _securities.Clear();
                _portfolios.Clear();
                _subscribledSecurities.Clear();

                SendLogMessage("Start CoinEx Spot Connection", LogMessageType.Connect);

                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
                //_marketMode = ((ServerParameterEnum)ServerParameters[2]).Value;
                _marketMode = MARKET_MODE_SPOT;
                _marketDepth = Int16.Parse(((ServerParameterEnum)ServerParameters[2]).Value);

                if (string.IsNullOrEmpty(_publicKey) || string.IsNullOrEmpty(_secretKey))
                {
                    SendLogMessage("Connection terminated. You must specify the public and private keys. You can get it on the CoinEx website.",
                        LogMessageType.Error);
                    return;
                }

                _restClient = new CoinExRestClient(_publicKey, _secretKey);
                _restClient.LogMessageEvent += SendLogMessage;

                // Check rest auth
                if (!GetCurrentPortfolios())
                {
                    SendLogMessage("Authorization Error. Probably an invalid keys are specified, check it!",
                        LogMessageType.Error);
                }

                CreateWebSocketConnection();
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
                if (_wsClient != null)
                {
                    CexRequestSocketUnsubscribe message = new CexRequestSocketUnsubscribe(CexWsOperation.MARKET_DEPTH_UNSUBSCRIBE.ToString(), new List<string>());
                    SendLogMessage("CoinEx server market depth unsubscribe: " + message, LogMessageType.Connect);
                    _wsClient.Send(message.ToString());

                    message = new CexRequestSocketUnsubscribe(CexWsOperation.BALANCE_UNSUBSCRIBE.ToString(), new List<string>());
                    SendLogMessage("CoinEx server portfolios unsubscribe: " + message, LogMessageType.Connect);
                    _wsClient.Send(message.ToString());

                    message = new CexRequestSocketUnsubscribe(CexWsOperation.DEALS_UNSUBSCRIBE.ToString(), new List<string>());
                    SendLogMessage("CoinEx server trades unsubscribe: " + message, LogMessageType.Connect);
                    _wsClient.Send(message.ToString());

                    message = new CexRequestSocketUnsubscribe(CexWsOperation.USER_DEALS_UNSUBSCRIBE.ToString(), new List<string>());
                    SendLogMessage("CoinEx server my trades unsubscribe: " + message, LogMessageType.Connect);
                    _wsClient.Send(message.ToString());

                    message = new CexRequestSocketUnsubscribe(CexWsOperation.ORDER_UNSUBSCRIBE.ToString(), new List<string>());
                    SendLogMessage("CoinEx server orders unsubscribe: " + message, LogMessageType.Connect);
                    _wsClient.Send(message.ToString());
                }

                _securities.Clear();
                _portfolios.Clear();
                _subscribledSecurities.Clear();
                _securities = new List<Security>();
                _restClient?.Dispose();
                DeleteWebSocketConnection();
                SendLogMessage("Dispose. Connection Closed by CoinEx. WebSocket Data Closed Event", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent?.Invoke();
            }
        }
        #endregion

        #region 2 Properties
        public List<IServerParameter> ServerParameters { get; set; }
        private string _publicKey;
        private string _secretKey;
        private int _marketDepth;

        // Spot or Margin
        private string _marketMode;
        private const string MARKET_MODE_SPOT = "spot";
        private const string MARKET_MODE_MARGIN = "margin";
        #endregion

        #region 3 Securities
        private List<Security> _securities = new List<Security>();
        public event Action<List<Security>> SecurityEvent;

        public void GetSecurities()
        {
            UpdateSec();

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                if (SecurityEvent != null)
                {
                    SecurityEvent.Invoke(_securities);
                }
            }
        }

        private void UpdateSec()
        {
            // https://docs.coinex.com/api/v2/spot/market/http/list-market
            try
            {
                List<CexSecurity> securities = _restClient.Get<List<CexSecurity>>("/spot/market").Result;
                UpdateSecuritiesFromServer(securities);
            }
            catch (Exception exception)
            {
                SendLogMessage("Securities request error: " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecuritiesFromServer(List<CexSecurity> stocks)
        {
            try
            {
                if (stocks == null ||
                    stocks.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < stocks.Count; i++)
                {
                    _securities.Add((Security)stocks[i]);
                }

                _securities.Sort(delegate (Security x, Security y)
                {
                    return String.Compare(x.NameFull, y.NameFull);
                });
            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading stocks: {e.Message}" + e.ToString(), LogMessageType.Error);
            }
        }
        #endregion

        #region 4 Portfolios
        public event Action<List<Portfolio>> PortfolioEvent;
        private List<Portfolio> _portfolios = new List<Portfolio>();
        private string _portfolioName = "CoinExSpot";

        public void GetPortfolios()
        {
            GetCurrentPortfolios();
        }

        public bool GetCurrentPortfolios()
        {
            _rateGateAccountStatus.WaitToProceed();

            try
            {
                List<CexPortfolioItem>? cexPortfolio = _restClient.Get<List<CexPortfolioItem>>("/assets/spot/balance", true).Result;

                //endPoint = "/assets/margin/balance";
                //List<CexMarginPortfolioItem>? cexMarginPortfolio = _restClient.Get<List<CexMarginPortfolioItem>>(endPoint, true).Result;

                ConvertToPortfolio(cexPortfolio);
                return _portfolios.Count > 0;
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio request error " + exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        private void ConvertToPortfolio(List<CexPortfolioItem> portfolioItems)
        {
            try
            {
                Portfolio myPortfolio = _portfolios.Find(p => p.Number == "BinanceSpot");

                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = _portfolioName;
                    newPortf.ServerType = ServerType;
                    newPortf.ValueBegin = 1;
                    newPortf.ValueCurrent = 1;
                    _portfolios.Add(newPortf);
                    myPortfolio = newPortf;
                }

                if (portfolioItems == null || portfolioItems.Count == 0)
                {
                    SendLogMessage("No portfolios detected!", LogMessageType.System);
                    return;
                }

                for (int i = 0; i < portfolioItems.Count; i++)
                {
                    PositionOnBoard pos = (PositionOnBoard)portfolioItems[i];
                    pos.PortfolioName = _portfolioName;
                    pos.ValueBegin = pos.ValueCurrent;

                    myPortfolio.SetNewPosition(pos);
                }

                myPortfolio.ValueCurrent = getPortfolioValue(myPortfolio);

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public decimal getPortfolioValue(Portfolio portfolio)
        {
            List<PositionOnBoard> poses = portfolio.GetPositionOnBoard();
            string mainCurrency = "";
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == "USDT"
                 || poses[i].SecurityNameCode == "USDC"
                 || poses[i].SecurityNameCode == "USD"
                 || poses[i].SecurityNameCode == "RUB"
                 || poses[i].SecurityNameCode == "EUR")
                {
                    mainCurrency = poses[i].SecurityNameCode;
                    break;
                }
            }

            if (string.IsNullOrEmpty(mainCurrency)) { return 0; }

            List<string> securities = new List<string>();
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == mainCurrency)
                {
                    continue;
                }
                securities.Add(poses[i].SecurityNameCode + mainCurrency);
            }

            List<CexMarketInfoItem> marketInfo = GetMarketsInfo(securities);

            decimal val = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == mainCurrency)
                {
                    val += poses[i].ValueCurrent;
                    continue;
                }
                else
                {
                    if (marketInfo != null)
                    {
                        for (int j = 0; j < marketInfo.Count; j++)
                        {
                            if (marketInfo[j].market == poses[i].SecurityNameCode + mainCurrency)
                            {
                                val += poses[i].ValueCurrent * marketInfo[j].last.ToString().ToDecimal();
                                break;
                            }
                        }
                    }
                }
            }

            return Math.Round(val, 2);
        }

        public List<CexMarketInfoItem> GetMarketsInfo(List<string> securities)
        {
            // https://docs.coinex.com/api/v2/spot/market/http/list-market-ticker
            List<CexMarketInfoItem> cexInfo = new List<CexMarketInfoItem>();

            string endPoint = "/spot/ticker";
            try
            {
                if (securities.Count > 10)
                {
                    // If empty list gets all markets info
                    securities = new List<string>();
                }

                cexInfo = _restClient.Get<List<CexMarketInfoItem>>(endPoint, false, new Dictionary<string, Object>()
                {
                    { "market", String.Join(",", securities.ToArray())},
                }).Result;
            }
            catch (Exception exception)
            {
                SendLogMessage("Market info request error:" + exception.ToString(), LogMessageType.Error);
            }
            return cexInfo;
        }
        #endregion

        #region 5 Data
        private CoinExRestClient _restClient;

        // https://docs.coinex.com/api/v2/rate-limit
        private RateGate _rateGateSendOrder = new RateGate(30, TimeSpan.FromMilliseconds(950));
        private RateGate _rateGateCancelOrder = new RateGate(60, TimeSpan.FromMilliseconds(950));
        private RateGate _rateGateGetOrder = new RateGate(50, TimeSpan.FromMilliseconds(950));
        private RateGate _rateGateOrdersHistory = new RateGate(10, TimeSpan.FromMilliseconds(950));
        private RateGate _rateGateAccountStatus = new RateGate(10, TimeSpan.FromMilliseconds(950));
        private RateGate _rateGateCandlesHistory = new RateGate(60, TimeSpan.FromMilliseconds(950));

        // Max candles in history
        private const int _maxCandlesHistory = 5000;
        private readonly Dictionary<int, string> _allowedTf = new Dictionary<int, string>() {
            { 1,  "1min" },
            { 3,  "3min" },
            { 5,  "5min" },
            { 15,  "15min" },
            { 30,  "30min" },
            { 60,  "1hour" },
            { 120,  "2hour" },
            { 240,  "4hour" },
            { 360,  "6hour" },
            { 720,  "12hour" },
            { 1440,  "1day" },
            { 4320,  "3day" },
            { 10080,  "1week" },
        };

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            DateTime endTime = DateTime.Now.ToUniversalTime();

            while (endTime.Hour != 23)
            {
                endTime = endTime.AddHours(1);
            }

            int candlesInDay = 0;

            if (timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes >= 1)
            {
                candlesInDay = 900 / Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
            }
            else
            {
                candlesInDay = 54000 / Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalSeconds);
            }

            if (candlesInDay == 0)
            {
                candlesInDay = 1;
            }

            int daysCount = candleCount / candlesInDay;

            if (daysCount == 0)
            {
                daysCount = 1;
            }

            daysCount++;

            DateTime startTime = endTime.AddDays(-daysCount);

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);

            while (candles.Count > candleCount)
            {
                candles.RemoveAt(0);
            }

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
                                DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!_allowedTf.ContainsKey(tfTotalMinutes))
                return null;

            if (actualTime > endTime)
            {
                return null;
            }

            actualTime = startTime;

            List<Candle> candles = new List<Candle>();

            TimeSpan additionTime = TimeSpan.FromMinutes(tfTotalMinutes * _maxCandlesHistory);

            DateTime endTimeReal = startTime.Add(additionTime);

            while (actualTime < endTime)
            {
                List<Candle> newCandles = new List<Candle>();
                List<CexCandle> history = cexGetCandleHistory(security, tfTotalMinutes, actualTime, endTimeReal);
                if (history != null && history.Count > 0)
                {
                    for (int i = 0; i < history.Count; i++)
                    {
                        newCandles.Add((Candle)history[i]);
                    }
                    history.Clear();

                    if (newCandles != null &&
                        newCandles.Count > 0)
                    {
                        //It could be 2 same candles from different requests - check and fix
                        if (candles.Count > 0)
                        {
                            Candle last = candles[candles.Count - 1];
                            for (int i = 0; i < newCandles.Count; i++)
                            {
                                if (newCandles[i].TimeStart > last.TimeStart)
                                {
                                    candles.Add(newCandles[i]);
                                }
                            }
                        }
                        else
                        {
                            candles = newCandles;
                        }
                    }
                }

                actualTime = endTimeReal;
                endTimeReal = actualTime.Add(additionTime);
            }

            while (candles != null &&
                candles.Count != 0 &&
                candles[candles.Count - 1].TimeStart > endTime)
            {
                candles.RemoveAt(candles.Count - 1);
            }

            return candles.Count > 0 ? candles : null;
        }
        #endregion

        #region 6 WebSocket creation
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        private ConcurrentQueue<string> _webSocketMessage = new ConcurrentQueue<string>();
        private readonly string _wsUrl = "wss://socket.coinex.com/v2/spot";
        private string _socketLocker = "webSocketLockerCoinEx";
        private bool _socketIsActive;
        private WebSocket _wsClient;

        private void CreateWebSocketConnection()
        {
            try
            {
                if (_wsClient != null)
                {
                    return;
                }

                _socketIsActive = false;

                lock (_socketLocker)
                {
                    _webSocketMessage = new ConcurrentQueue<string>();

                    _wsClient = new WebSocket(_wsUrl);
                    _wsClient.EnableAutoSendPing = true;
                    _wsClient.AutoSendPingInterval = 15;
                    _wsClient.Opened += WebSocket_Opened;
                    _wsClient.Closed += WebSocket_Closed;
                    _wsClient.Error += WebSocketData_Error;
                    _wsClient.DataReceived += WebSocket_DataReceived;
                    _wsClient.Open();
                }
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebSocketConnection()
        {
            try
            {
                lock (_socketLocker)
                {
                    if (_wsClient == null)
                    {
                        return;
                    }

                    try
                    {
                        _wsClient.Close();
                    }
                    catch
                    {
                        // ignore
                    }

                    _wsClient.Opened -= WebSocket_Opened;
                    _wsClient.Closed -= WebSocket_Closed;
                    _wsClient.DataReceived -= WebSocket_DataReceived;
                    _wsClient.Error -= WebSocketData_Error;
                    _wsClient = null;
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                _wsClient = null;
            }
        }
        private void AuthInSocket()
        {
            CexRequestSocketSign message = new CexRequestSocketSign(_publicKey, _secretKey);

            SendLogMessage("CoinEx server auth: " + message, LogMessageType.Connect);
            _wsClient.Send(message.ToString());
        }

        private void CheckActivationSockets()
        {
            if (_socketIsActive == false)
            {
                return;
            }

            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    SendLogMessage("All sockets activated. Connect State", LogMessageType.Connect);
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent?.Invoke();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }
        #endregion

        #region 7 WebSocket events
        private void WebSocket_Opened(Object sender, EventArgs e)
        {
            SendLogMessage("Socket Data activated", LogMessageType.System);
            _socketIsActive = true;
            CheckActivationSockets();

            AuthInSocket();
            Thread.Sleep(2000);

            CexRequestSocketSubscribePortfolio message = new CexRequestSocketSubscribePortfolio();
            SendLogMessage("Subcribe to portfolios data: " + message, LogMessageType.Connect);
            _wsClient.Send(message.ToString());
        }

        private void WebSocket_Closed(Object sender, EventArgs e)
        {
            try
            {
                SendLogMessage("WebSocket. Connection Closed by CoinEx. WebSocket Data Closed Event", LogMessageType.Error);

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    _socketIsActive = false;
                    DisconnectEvent?.Invoke();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketData_Error(Object sender, SuperSocket.ClientEngine.ErrorEventArgs error)
        {
            try
            {
                if (error.Exception != null)
                {
                    SendLogMessage("Web Socket Error: " + error.Exception.ToString(), LogMessageType.Error);
                }
                else
                {
                    SendLogMessage("Web Socket Error: " + error.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Web socket error: " + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_DataReceived(Object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    SendLogMessage("PorfolioWebSocket DataReceived Empty message: State=" + ServerStatus.ToString(),
                        LogMessageType.Connect);
                    return;
                }

                if (e.Data.Length == 0)
                {
                    return;
                }

                if (_webSocketMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                string message = Decompress(e.Data);

                _webSocketMessage.Enqueue(message);

            }
            catch (Exception error)
            {
                SendLogMessage("Portfolio socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive
        private DateTime _lastTimeWsCheckConnection = DateTime.MinValue;

        private void ConnectionCheckThread()
        {
            while (true)
            {
                Thread.Sleep(17000); // Sleep1

                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    SendWsPing();
                    Thread.Sleep(13000); // Sleep2

                    // Sleep1 + Sleep2 + some overhead
                    // Trigger disconnect when fail twice
                    if (_lastTimeWsCheckConnection.AddSeconds(63) < DateTime.Now)
                    {
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            _socketIsActive = false;
                            DisconnectEvent?.Invoke();
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        _socketIsActive = false;
                        DisconnectEvent?.Invoke();
                    }
                }
            }
        }

        private void SendWsPing()
        {
            CexRequestSocketPing message = new CexRequestSocketPing();
            _wsClient?.Send(message.ToString());
        }
        #endregion

        #region 9 Security subscrible
        private RateGate _rateGateSubscrible = new RateGate(1, TimeSpan.FromMilliseconds(50));
        private List<Security> _subscribledSecurities = new List<Security>();

        public void Subscrible(Security security)
        {
            try
            {
                for (int i = 0; i < _subscribledSecurities.Count; i++)
                {
                    if (_subscribledSecurities[i].NameClass == security.NameClass
                        && _subscribledSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }

                _rateGateSubscrible.WaitToProceed();

                _subscribledSecurities.Add(security);

                // Trades subscription
                CexRequestSocketSubscribeDeals message = new CexRequestSocketSubscribeDeals(_subscribledSecurities);
                SendLogMessage("SubcribeToTradesData: " + message, LogMessageType.Connect);
                _wsClient.Send(message.ToString());

                // Market depth subscription
                CexRequestSocketSubscribeMarketDepth message1 = new CexRequestSocketSubscribeMarketDepth(_subscribledSecurities, _marketDepth);
                SendLogMessage("SubcribeToMarketDepthData: " + message1, LogMessageType.Connect);
                _wsClient.Send(message1.ToString());

                // My orders subscription
                CexRequestSocketSubscribeMyOrders message2 = new CexRequestSocketSubscribeMyOrders(_subscribledSecurities);
                SendLogMessage("SubcribeToMyOrdersData: " + message2, LogMessageType.Connect);
                _wsClient.Send(message2.ToString());
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 10 WebSocket parsing the messages
        private DateTime _lastMdTime = DateTime.MinValue;
        private void DataMessageReaderThread()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (!_socketIsActive)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_webSocketMessage.IsEmpty)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    string message;

                    _webSocketMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    CoinExWsResp<Object> baseMessage = JsonConvert.DeserializeObject<CoinExWsResp<Object>>(message);
                    baseMessage.EnsureSuccessStatusCode();

                    if (baseMessage.method == null)
                    {
                        if (baseMessage.data != null && baseMessage.data.ToString().Contains("pong"))
                        {
                            _lastTimeWsCheckConnection = DateTime.Now;
                        }
                        continue;
                    }

                    if (baseMessage.method == "depth.update")
                    {
                        CexWsDepthUpdate data = JsonConvert.DeserializeObject<CexWsDepthUpdate>(baseMessage.data.ToString());
                        UpdateMarketDepth(data);
                    }
                    else if (baseMessage.method == "deals.update")
                    {
                        CexWsTransactionUpdate data = JsonConvert.DeserializeObject<CexWsTransactionUpdate>(baseMessage.data.ToString());
                        UpdateTrade(data);
                    }
                    else if (baseMessage.method == "balance.update")
                    {
                        CexWsBalance data = JsonConvert.DeserializeObject<CexWsBalance>(baseMessage.data.ToString());
                        UpdateMyPortfolio(data);
                    }
                    else if (baseMessage.method == "order.update")
                    {
                        CexWsOrderUpdate data = JsonConvert.DeserializeObject<CexWsOrderUpdate>(baseMessage.data.ToString());
                        UpdateMyOrder(data);
                    }
                    else
                    {
                        SendLogMessage("Unknown message method: " + baseMessage.message, LogMessageType.Error);
                    }
                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    Thread.Sleep(2000);
                }
            }
        }

        private readonly Object _updateTradesLocker = new Object();
        private void UpdateTrade(CexWsTransactionUpdate data)
        {
            // https://docs.coinex.com/api/v2/spot/market/ws/market-deals
            lock (_updateTradesLocker)
            {
                if (data.deal_list == null || data.deal_list.Count == 0)
                {
                    SendLogMessage("Wrong 'Trade' message for market: " + data.market, LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(data.market))
                {
                    return;
                }

                for (int i = data.deal_list.Count - 1; i >= 0; i--)
                {
                    Trade trade = (Trade)data.deal_list[i];
                    trade.SecurityNameCode = data.market;

                    if (trade.Price == 0 || trade.Volume == 0 || string.IsNullOrEmpty(trade.Id))
                    {
                        continue;
                    }

                    if (NewTradesEvent != null)
                    {
                        NewTradesEvent(trade);
                    }
                }
            }
        }

        private readonly Object _updateMarketDepthLocker = new Object();
        private void UpdateMarketDepth(CexWsDepthUpdate data)
        {
            // https://docs.coinex.com/api/v2/spot/market/ws/market-depth
            lock (_updateMarketDepthLocker)
            {
                if (data.depth.asks.Count == 0 && data.depth.bids.Count == 0)
                {
                    return;
                }

                MarketDepth newMD = (MarketDepth)data.depth;
                newMD.SecurityNameCode = data.market;

                if (_lastMdTime != DateTime.MinValue &&
                    _lastMdTime >= newMD.Time)
                {
                    newMD.Time = _lastMdTime.AddMilliseconds(1);
                }

                _lastMdTime = newMD.Time;

                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(newMD);
                }
            }
        }

        private void UpdateMyOrder(CexWsOrderUpdate data)
        {
            if (data.order.order_id == 0)
            {
                return;
            }

            CexOrderUpdate cexOrder = data.order;

            Order order = ConvertWsUpdateToOsEngineOrder(data);

            if (order == null || order.NumberUser == 0)
            {
                return;
            }

            MyOrderEvent?.Invoke(order);

            if (MyTradeEvent != null &&
                (order.State == OrderStateType.Done || order.State == OrderStateType.Partial))
            {
                UpdateTrades(order);
            }
        }

        private void UpdateMyPortfolio(CexWsBalance data)
        {
            try
            {
                if (data.balance_list.Length == 0)
                {
                    return;
                }

                if (_portfolios == null)
                {
                    return;
                }

                Portfolio portfolio = null;

                portfolio = _portfolios.Find(p => p.Number == _portfolioName);

                if (portfolio == null)
                {
                    return;
                }

                for (int i = 0; i < data.balance_list.Length; i++)
                {
                    PositionOnBoard pos =
                        portfolio.GetPositionOnBoard().Find(p => p.SecurityNameCode == data.balance_list[i].ccy);
                    if (pos == null)
                    {
                        pos = (PositionOnBoard)data.balance_list[i];
                        pos.PortfolioName = _portfolioName;
                        portfolio.SetNewPosition(pos);
                        continue;
                    }

                    pos.ValueCurrent = data.balance_list[i].available.ToString().ToDecimal();
                    pos.ValueBlocked = data.balance_list[i].frozen.ToString().ToDecimal();
                }

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
        #endregion

        #region 11 Trade
        private string _lockOrder = "lockOrder";
        public void GetAllActivOrders()
        {
            List<Order> openOrders = cexGetAllActiveOrders();

            if (openOrders == null)
            {
                return;
            }

            for (int i = 0; i < openOrders.Count; i++)
            {
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(openOrders[i]);
                }
            }
        }

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                // https://docs.coinex.com/api/v2/spot/order/http/put-order#http-request
                Dictionary<string, Object> body = (new CexRequestSendOrder(_marketMode, order)).parameters;
                CexOrder cexOrder = _restClient.Post<CexOrder>("/spot/order", body, true).Result;

                if (cexOrder.order_id > 0)
                {
                    order.State = OrderStateType.Active;
                    order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.updated_at);
                    order.TimeCreate = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.created_at);
                    order.NumberMarket = cexOrder.order_id.ToString();
                    MyOrderEvent?.Invoke(order);
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage("Error while send order. Check it manually on CoinEx!", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void GetOrderStatus(Order order)
        {
            Order myOrder = cexGetOrderFromExchange(order.SecurityNameCode, order.NumberMarket);

            if (myOrder == null)
            {
                return;
            }

            MyOrderEvent?.Invoke(myOrder);

            if (myOrder.State == OrderStateType.Done || myOrder.State == OrderStateType.Partial)
            {
                UpdateTrades(myOrder);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                // https://docs.coinex.com/api/v2/spot/order/http/edit-order
                Dictionary<string, Object> body = (new CexRequestEditOrder(_marketMode, order, newPrice)).parameters;
                CexOrder cexOrder = _restClient.Post<CexOrder>("/spot/modify-order", body, true).Result;

                if (cexOrder.order_id > 0)
                {
                    order.Price = newPrice;
                    order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.updated_at);
                    MyOrderEvent?.Invoke(order);
                }
                else
                {
                    SendLogMessage("Price change command executed, but price not changed. Not valid price?", LogMessageType.System);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order change price send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();
            lock (_lockOrder)
            {
                try
                {
                    // https://docs.coinex.com/api/v2/spot/order/http/cancel-order
                    Dictionary<string, Object> body = (new CexRequestCancelOrder(_marketMode, order.NumberMarket, order.SecurityNameCode)).parameters;
                    CexOrder cexOrder = _restClient.Post<CexOrder>("/spot/cancel-order", body, true).Result;

                    if (cexOrder.order_id > 0)
                    {
                        order.State = OrderStateType.Cancel;
                        order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.updated_at);
                        order.TimeCancel = order.TimeCallBack;
                        MyOrderEvent?.Invoke(order);
                    }
                    else
                    {
                        CreateOrderFail(order);
                        string msg = string.Format("Cancel order executed, but answer is wrong! {0}cexOrder: {1}{0}order: {2}", Environment.NewLine,
                            cexOrder.ToString(),
                            order.GetStringForSave().ToString()
                        );
                        SendLogMessage(msg, LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("Cancel order error. " + exception.ToString(), LogMessageType.Error);
                }
            }
        }

        public void CancelAllOrders()
        {
            for (int i = 0; i < _subscribledSecurities.Count; i++)
            {
                CancelAllOrdersToSecurity(_subscribledSecurities[i]);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            cexCancelAllOrdersToSecurity(security.NameFull);
        }

        private List<Order> cexGetAllActiveOrders()
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                // https://docs.coinex.com/api/v2/spot/order/http/list-pending-order
                Dictionary<string, Object> parameters = (new CexRequestPendingOrders(_marketMode, null)).parameters;
                List<CexOrder>? cexOrders = _restClient.Get<List<CexOrder>>("/spot/pending-order", true, parameters).Result;

                if (cexOrders == null || cexOrders.Count == 0)
                {
                    return null;
                }

                List<Order> orders = new List<Order>();

                HashSet<string> securities = new HashSet<string>();

                for (int i = 0; i < cexOrders.Count; i++)
                {
                    if (string.IsNullOrEmpty(cexOrders[i].client_id))
                    {
                        SendLogMessage("Non OS Engine order with id:" + cexOrders[i].order_id + ". Skipped.", LogMessageType.System);
                        continue;
                    }

                    Order order = (Order)cexOrders[i];
                    order.PortfolioNumber = _portfolioName;

                    if (order == null)
                    {
                        continue;
                    }

                    orders.Add(order);
                    securities.Add(order.SecurityNameCode);
                }

                return orders;
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all opened orders request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private Order cexGetOrderFromExchange(string market, string orderId)
        {
            _rateGateGetOrder.WaitToProceed();

            if (string.IsNullOrEmpty(orderId))
            {
                SendLogMessage("Market order ID is empty", LogMessageType.Connect);
                return null;
            }

            try
            {
                // https://docs.coinex.com/api/v2/spot/order/http/get-order-status
                Dictionary<string, Object> parameters = (new CexRequestOrderStatus(orderId, market)).parameters;
                CexOrder cexOrder = _restClient.Get<CexOrder>("/spot/order-status", true, parameters).Result;

                if (!string.IsNullOrEmpty(cexOrder.client_id))
                {
                    Order order = (Order)cexOrder;
                    order.PortfolioNumber = _portfolioName;
                    return order;
                }
                else
                {
                    SendLogMessage("Order not found or non OS Engine Order. User Order Id: " + orderId + " Order Id: " + cexOrder.order_id, LogMessageType.System);
                    return null;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get order request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public void cexCancelAllOrdersToSecurity(string security)
        {
            lock (_lockOrder)
            {
                try
                {
                    // https://docs.coinex.com/api/v2/spot/order/http/cancel-all-order
                    Dictionary<string, Object> body = (new CexRequestCancelAllOrders(_marketMode, security)).parameters;
                    Object result = _restClient.Post<Object>("/spot/cancel-all-order", body, true).Result;
                }
                catch (Exception exception)
                {
                    SendLogMessage("Cancel all orders request error. " + exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdateTrades(Order order)
        {
            if (string.IsNullOrEmpty(order.NumberMarket))
            {
                SendLogMessage("UpdateTrades: Empty NumberMarket", LogMessageType.System);
                return;
            }
            List<MyTrade> trades = GetTradesForOrder(order.NumberMarket, order.SecurityNameCode);

            if (trades == null)
            {
                return;
            }

            for (int i = 0; i < trades.Count; i++)
            {
                MyTradeEvent?.Invoke(trades[i]);
            }
        }

        private List<MyTrade> GetTradesForOrder(string orderId, string market)
        {
            _rateGateOrdersHistory.WaitToProceed();

            try
            {
                // https://docs.coinex.com/api/v2/spot/deal/http/list-user-order-deals#http-request
                Dictionary<string, Object> parameters = (new CexRequestOrderDeals(_marketMode, orderId, market)).parameters;
                List<CexOrderTransaction> cexTrades = _restClient.Get<List<CexOrderTransaction>>("/spot/order-deals", true, parameters).Result;

                if (cexTrades != null)
                {
                    List<MyTrade> trades = new List<MyTrade>();

                    for (int i = 0; i < cexTrades.Count; i++)
                    {
                        MyTrade trade = (MyTrade)cexTrades[i];
                        trade.NumberOrderParent = orderId; // Patch CEX API error
                        trades.Add(trade);
                    }

                    return trades;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order trade request error " + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }
        #endregion

        #region 12 Queries
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
            // https://docs.coinex.com/api/v2/spot/market/http/list-market-deals#http-request
            // Max 1000 deals at all
            List<Trade> trades = new List<Trade>();
            try
            {
                Dictionary<string, Object> parameters = (new CexRequestGetDeals(security.Name)).parameters;
                List<CexTransaction> cexDeals = _restClient.Get<List<CexTransaction>>("/spot/deals", false, parameters).Result;

                for (int i = cexDeals.Count - 1; i >= 0; i--)
                {
                    Trade trade = (Trade)cexDeals[i];
                    if (trade.Time >= startTime && trade.Time <= endTime && trade.Price > 0 && !string.IsNullOrEmpty(trade.Id))
                    {
                        trades.Add(trade);
                    }
                }

                return trades;
            }
            catch (Exception ex)
            {
                SendLogMessage("Trades request error:" + ex.ToString(), LogMessageType.Error);
            }
            return trades.Count > 0 ? trades : null;
        }

        private List<CexCandle> cexGetCandleHistory(Security security, int tfTotalMinutes,
            DateTime startTime, DateTime endTime)
        {
            _rateGateCandlesHistory.WaitToProceed();
            int candlesCount = Convert.ToInt32(endTime.Subtract(startTime).TotalMinutes / tfTotalMinutes);
            int tfSeconds = tfTotalMinutes * 60;

            if (candlesCount > _maxCandlesHistory)
            {
                SendLogMessage($"Too much candles for TF {tfTotalMinutes}", LogMessageType.Error);
                return null;
            }
            long tsStartTime = (startTime > DateTime.UtcNow) ? TimeManager.GetTimeStampSecondsToDateTime(DateTime.UtcNow) : TimeManager.GetTimeStampSecondsToDateTime(startTime);
            long tsEndTime = (endTime > DateTime.UtcNow) ? TimeManager.GetTimeStampSecondsToDateTime(DateTime.UtcNow) : TimeManager.GetTimeStampSecondsToDateTime(endTime);

            if (tsStartTime > tsEndTime || tsStartTime < 0 || tsEndTime < 0) { return null; }

            // https://www.coinex.com/res/market/kline?market=XRPUSDT&start_time=1719781200&end_time=1725138000&interval=300
            string url = string.Format("https://www.coinex.com/res/market/kline?market={0}&start_time={1}&end_time={2}&interval={3}",
                security.Name,
                tsStartTime,
                tsEndTime,
                tfSeconds
                );
            try
            {
                HttpClient _client = new HttpClient();
                HttpRequestMessage req = new HttpRequestMessage(new HttpMethod("GET"), url);
                HttpResponseMessage response = _client.SendAsync(req).Result;
                response.EnsureSuccessStatusCode();
                string responseContent = response.Content.ReadAsStringAsync().Result;
                if (!responseContent.Contains("Success")) { return null; }
                CoinExHttpResp<List<List<object>>> resp = JsonConvert.DeserializeObject<CoinExHttpResp<List<List<object>>>>(responseContent);
                resp!.EnsureSuccessStatusCode();

                List<CexCandle> cexCandles = new List<CexCandle>();
                for (int i = 0; i < resp.data.Count; i++)
                {
                    CexCandle candle = (CexCandle)(new CexCandleExtra { data = resp.data[i], market = security.Name });
                    cexCandles.Add(candle);
                }
                if (cexCandles != null && cexCandles.Count > 0)
                {
                    return cexCandles;
                }
                SendLogMessage($"Empty Candles response to url {url}", LogMessageType.System);
                _client.Dispose();
            }
            catch (Exception ex)
            {
                //SendLogMessage(ex.Message, LogMessageType.Error);
                SendLogMessage("Candles request error:" + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }
        #endregion

        #region 13 Log
        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }
        #endregion

        #region 14 Helpers
        public static decimal GetPriceStep(int ScalePrice)
        {
            if (ScalePrice == 0)
            {
                return 1;
            }
            string priceStep = "0,";
            for (int i = 0; i < ScalePrice - 1; i++)
            {
                priceStep += "0";
            }

            priceStep += "1";

            return priceStep.ToString().ToDecimal();
        }

        private static string Decompress(byte[] data)
        {
            using (MemoryStream msi = new MemoryStream(data))
            using (MemoryStream mso = new MemoryStream())
            {
                //using DeflateStream decompressor = new DeflateStream(msi, CompressionMode.Decompress);
                using GZipStream decompressor = new GZipStream(msi, CompressionMode.Decompress);
                decompressor.CopyTo(mso);

                return Encoding.UTF8.GetString(mso.ToArray());
            }
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;
            MyOrderEvent?.Invoke(order);
        }

        private Order ConvertWsUpdateToOsEngineOrder(CexWsOrderUpdate cexEventUpdate)
        {
            CexOrderUpdate cexOrder = cexEventUpdate.order;
            Order order = (Order)cexOrder;
            if (order == null)
            {
                string msg = string.Format("Failed to convert CexWsOrderUpdate в Os Engine Order!{0}cexEventUpdate: {1}{0}order: null", Environment.NewLine,
                    JsonConvert.SerializeObject(cexEventUpdate)
                    );
                SendLogMessage(msg, LogMessageType.Error);
            }

            if (order.NumberUser == 0)
            {
                string msg = string.Format("Unknown order!{0}Empty NumberUser! {0}cexEventUpdate: {1}{0}order: {2}", Environment.NewLine,
                    JsonConvert.SerializeObject(cexEventUpdate),
                    JsonConvert.SerializeObject(order)
                    );
                SendLogMessage(msg, LogMessageType.Error);
            }

            order.PortfolioNumber = _portfolioName;
            decimal cexAmount = cexOrder.amount.ToString().ToDecimal();
            decimal cexFilledAmount = cexOrder.filled_amount.ToString().ToDecimal();
            decimal cexFilledValue = cexOrder.filled_value.ToString().ToDecimal();
            if (cexEventUpdate.@event == CexOrderEvent.PUT.ToString())
            {
                // Order placed successfully (unfilled/partially filled)
                if (cexAmount == cexOrder.unfilled_amount.ToString().ToDecimal())
                {
                    order.State = OrderStateType.Active;
                }
                else if (cexAmount == cexFilledAmount || cexAmount == cexFilledValue)
                {
                    // Undocumented behavior
                    order.State = OrderStateType.Done;
                    order.TimeDone = order.TimeCallBack;
                }
                else
                {
                    order.State = OrderStateType.Partial;
                }
            }
            else if (cexEventUpdate.@event == CexOrderEvent.UPDATE.ToString())
            {
                // Order updated (partially filled)
                order.State = OrderStateType.Partial;
            }
            else if (cexEventUpdate.@event == CexOrderEvent.FINISH.ToString())
            {
                // Order completed (filled or canceled)
                order.State = OrderStateType.Cancel;
                if (cexAmount > 0)
                {
                    decimal relAmount = Math.Abs(1 - cexFilledAmount / cexAmount);
                    decimal relValue = Math.Abs(1 - cexFilledValue / cexAmount);
                    if (relAmount < 0.001m || relValue < 0.001m)
                    {
                        order.State = OrderStateType.Done;
                        order.TimeDone = order.TimeCallBack;
                    }
                }

                if (order.State == OrderStateType.Cancel)
                {
                    order.TimeCancel = order.TimeCallBack;
                }
            }
            else if (cexEventUpdate.@event == CexOrderEvent.MODIFY.ToString())
            {
                // Order modified successfully (unfilled/partially filled)
                if (cexFilledAmount == 0)
                {
                    order.State = OrderStateType.Active;
                }
                else if (cexFilledAmount < cexAmount)
                {
                    order.State = OrderStateType.Partial;
                }
                else
                {
                    throw new Exception("Unknown my trade state! Event: modify.");
                }
            }
            else
            {
                throw new Exception("Unknown my trade event! General conversion.");
            }

            return order;
        }
        #endregion
    }

    #region 15 Signer
    public static class Signer
    {
        public static string Sign(string message, string secret)
        {
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] r = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(r).Replace("-", "").ToLower();
            }
        }

        public static string RestSign(string method, string path, string body, long timestamp, string secret)
        {
            string message = method + path + body + timestamp.ToString();
            return Sign(message, secret);
        }
    }
    #endregion
}
