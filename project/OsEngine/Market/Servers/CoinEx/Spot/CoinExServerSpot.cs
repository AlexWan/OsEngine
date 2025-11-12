/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

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
using OsEngine.Entity.WebSocketOsEngine;
using System.Collections.Concurrent;
using System.IO.Compression;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net;

namespace OsEngine.Market.Servers.CoinEx.Spot
{
    public class CoinExServerSpot : AServer
    {
        public CoinExServerSpot()
        {
            CoinExServerRealization realization = new CoinExServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterEnum("Market depth", "20", new List<string> { "5", "10", "20", "50" });
            CreateParameterEnum("Market Mode", CexMarketType.SPOT.ToString(), new List<string> { CexMarketType.SPOT.ToString(), CexMarketType.MARGIN.ToString() });

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.ServerParam13;
            ServerParameters[3].Comment = OsLocalization.Market.Label273;
        }
    }

    public class CoinExServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType
        {
            get { return ServerType.CoinExSpot; }
        }

        public CoinExServerRealization()
        {
            Thread worker = new Thread(DataMessageReaderThread);
            worker.Name = "DataMessageReaderCoinEx";
            worker.Start();

            Thread worker1 = new Thread(ConnectionCheckThread);
            worker1.Name = "CheckAliveCoinEx";
            worker1.Start();
        }

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                _securities.Clear();
                _portfolios.Clear();
                _wsClients.Clear();
                _subscribedSecurities.Clear();

                SendLogMessage("Start CoinEx Spot Connection", LogMessageType.Connect);

                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
                _marketDepth = Int16.Parse(((ServerParameterEnum)ServerParameters[2]).Value);
                _marketMode = ((ServerParameterEnum)ServerParameters[3]).Value;

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

                _wsClients.Add(CreateWebSocketConnection());
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
                for (int i = 0; i < _wsClients.Count; i++)
                {
                    DeleteWebSocketConnection(_wsClients[i]);
                    Thread.Sleep(10);
                }
                _securities.Clear();
                _portfolios.Clear();
                _subscribedSecurities.Clear();
                _securities = new List<Security>();
                _restClient?.Dispose();
                SendLogMessage("Dispose. Connection Closed.", LogMessageType.System);
                Thread.Sleep(1000);
                _wsClients.Clear();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            SetDisconnected();
        }

        private void SetConnected()
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                SendLogMessage("Socket activated.", LogMessageType.Connect);
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent?.Invoke();
            }
        }

        public void SetDisconnected()
        {
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
                List<CexSecurity> securities = _restClient.Get<List<CexSecurity>>("/spot/market");
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
                    CexSecurity cexSecurity = stocks[i];

                    Security security = new Security();
                    security.Name = cexSecurity.market;
                    security.NameId = cexSecurity.market;
                    security.NameFull = cexSecurity.market;
                    security.NameClass = cexSecurity.quote_ccy;
                    security.State = SecurityStateType.Activ;
                    security.Decimals = Convert.ToInt32(cexSecurity.quote_ccy_precision);
                    security.MinTradeAmount = cexSecurity.min_amount.ToDecimal();
                    security.DecimalsVolume = Convert.ToInt32(cexSecurity.base_ccy_precision);
                    security.PriceStep = security.Decimals.GetValueByDecimals();
                    security.PriceStepCost = security.PriceStep; // FIX Сомнительно! Проверить!
                    security.Lot = 1;
                    security.SecurityType = SecurityType.CurrencyPair;
                    security.Exchange = ServerType.CoinExSpot.ToString();

                    _securities.Add(security);
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

        public string getPortfolioName(string securityName = "")
        {
            if (_marketMode == CexMarketType.SPOT.ToString())
            {
                return "CoinExSpot";
            }

            return "Margin " + securityName;
        }

        public void GetPortfolios()
        {
            GetCurrentPortfolios();
        }

        public bool GetCurrentPortfolios()
        {
            _rateGateAccountStatus.WaitToProceed();

            try
            {
                if (_marketMode == CexMarketType.SPOT.ToString())
                {
                    List<CexSpotPortfolioItem> cexPortfolio = _restClient.Get<List<CexSpotPortfolioItem>>("/assets/spot/balance", true);
                    ConvertSpotToPortfolio(cexPortfolio);
                }
                if (_marketMode == CexMarketType.MARGIN.ToString())
                {
                    List<CexMarginPortfolioItem> cexPortfolio = _restClient.Get<List<CexMarginPortfolioItem>>("/assets/margin/balance", true);
                    ConvertMarginToPortfolio(cexPortfolio);
                }
                return _portfolios.Count > 0;
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio request error " + exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        private void ConvertSpotToPortfolio(List<CexSpotPortfolioItem> portfolioItems)
        {
            string portfolioName = getPortfolioName();
            try
            {
                Portfolio myPortfolio = _portfolios.Find(p => p.Number == portfolioName);

                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = portfolioName;
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
                    CexSpotPortfolioItem cexPortfolioItem = portfolioItems[i];

                    PositionOnBoard pos = new PositionOnBoard();

                    pos.SecurityNameCode = cexPortfolioItem.ccy;
                    pos.ValueBlocked = cexPortfolioItem.frozen.ToString().ToDecimal();
                    pos.ValueCurrent = cexPortfolioItem.available.ToString().ToDecimal();
                    pos.PortfolioName = portfolioName;
                    if (pos.ValueBegin == 1)
                    {
                        pos.ValueBegin = pos.ValueCurrent + pos.ValueBlocked;
                    }

                    if (pos.ValueBlocked + pos.ValueCurrent > 0)
                    {
                        myPortfolio.SetNewPosition(pos);
                    }
                }

                myPortfolio.ValueCurrent = getPortfolioValue(myPortfolio);

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ConvertMarginToPortfolio(List<CexMarginPortfolioItem> portfolioItems)
        {
            try
            {
                if (portfolioItems == null || portfolioItems.Count == 0)
                {
                    SendLogMessage("No portfolios detected!", LogMessageType.System);
                    return;
                }

                for (int i = 0; i < portfolioItems.Count; i++)
                {
                    CexMarginPortfolioItem cexPortfolioItem = portfolioItems[i];
                    string portfolioName = getPortfolioName(cexPortfolioItem.margin_account);
                    Portfolio myPortfolio = _portfolios.Find(p => p.Number == portfolioName);

                    if (myPortfolio == null)
                    {
                        Portfolio newPortf = new Portfolio();
                        newPortf.Number = portfolioName;
                        newPortf.ServerType = ServerType;
                        newPortf.ValueBegin = 1;
                        newPortf.ValueCurrent = 1;
                        myPortfolio = newPortf;
                    }

                    PositionOnBoard pos = new PositionOnBoard();
                    pos.SecurityNameCode = cexPortfolioItem.base_ccy;
                    pos.ValueBlocked = cexPortfolioItem.frozen.base_ccy.ToString().ToDecimal();
                    pos.ValueCurrent = cexPortfolioItem.available.base_ccy.ToString().ToDecimal();
                    pos.PortfolioName = portfolioName;
                    pos.ValueBegin = pos.ValueCurrent;

                    if (pos.ValueBlocked + pos.ValueCurrent > 0)
                    {
                        myPortfolio.SetNewPosition(pos);
                    }

                    pos = new PositionOnBoard();
                    pos.SecurityNameCode = cexPortfolioItem.quote_ccy;
                    pos.ValueBlocked = cexPortfolioItem.frozen.quote_ccy.ToString().ToDecimal();
                    pos.ValueCurrent = cexPortfolioItem.available.quote_ccy.ToString().ToDecimal();
                    pos.PortfolioName = portfolioName;
                    pos.ValueBegin = pos.ValueCurrent;

                    if (pos.ValueBlocked + pos.ValueCurrent > 0)
                    {
                        myPortfolio.SetNewPosition(pos);
                    }

                    List<PositionOnBoard> poses = myPortfolio.GetPositionOnBoard();

                    if (poses == null || poses.Count == 0) { continue; }

                    _portfolios.Add(myPortfolio);

                    myPortfolio.ValueCurrent = getPortfolioValue(myPortfolio);

                    PortfolioEvent?.Invoke(_portfolios);
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public decimal getPortfolioValue(Portfolio portfolio)
        {
            List<PositionOnBoard> poses = portfolio.GetPositionOnBoard();
            if (poses == null || poses.Count == 0) return 0;
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
                    // If list is empty - gets all markets info
                    securities = new List<string>();
                }

                cexInfo = _restClient.Get<List<CexMarketInfoItem>>(endPoint, false, new Dictionary<string, Object>()
                {
                    { "market", String.Join(",", securities.ToArray())},
                });
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
                        CexCandle cexCandle = history[i];

                        Candle candle = new Candle();
                        candle.Open = cexCandle.open.ToString().ToDecimal();
                        candle.High = cexCandle.high.ToString().ToDecimal();
                        candle.Low = cexCandle.low.ToString().ToDecimal();
                        candle.Close = cexCandle.close.ToString().ToDecimal();
                        candle.Volume = cexCandle.volume.ToString().ToDecimal();
                        candle.TimeStart = new DateTime(1970, 1, 1).AddMilliseconds(cexCandle.created_at);

                        //fix candle
                        if (candle.Open < candle.Low)
                            candle.Open = candle.Low;
                        if (candle.Open > candle.High)
                            candle.Open = candle.High;

                        if (candle.Close < candle.Low)
                            candle.Close = candle.Low;
                        if (candle.Close > candle.High)
                            candle.Close = candle.High;

                        newCandles.Add(candle);
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

        private List<WebSocket> _wsClients = new List<WebSocket>();

        private WebSocket CreateWebSocketConnection()
        {
            WebSocket _wsClient = new WebSocket(_wsUrl);
            try
            {
                lock (_socketLocker)
                {
                    _webSocketMessage = new ConcurrentQueue<string>();
                    _wsClient.EmitOnPing = true;
                    /*_wsClient.SslConfiguration.EnabledSslProtocols
                     =  System.Security.Authentication.SslProtocols.Tls12
                      | System.Security.Authentication.SslProtocols.Tls13;*/

                    _wsClient.OnOpen += WebSocket_Opened;
                    _wsClient.OnClose += WebSocket_Closed;
                    _wsClient.OnError += WebSocketData_Error;
                    _wsClient.OnMessage += WebSocket_DataReceived;
                    _wsClient.ConnectAsync();
                }
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }

            return _wsClient;
        }

        private void DeleteWebSocketConnection(WebSocket wsClient)
        {
            try
            {
                lock (_socketLocker)
                {
                    if (wsClient == null)
                    {
                        return;
                    }

                    CexRequestSocketUnsubscribe message = new CexRequestSocketUnsubscribe(CexWsOperation.MARKET_DEPTH_UNSUBSCRIBE.ToString(), new List<string>());
                    SendLogMessage("CoinEx server market depth unsubscribe: " + message, LogMessageType.Connect);
                    wsClient.SendAsync(message.ToString());

                    message = new CexRequestSocketUnsubscribe(CexWsOperation.BALANCE_UNSUBSCRIBE.ToString(), new List<string>());
                    SendLogMessage("CoinEx server portfolios unsubscribe: " + message, LogMessageType.Connect);
                    wsClient.SendAsync(message.ToString());

                    message = new CexRequestSocketUnsubscribe(CexWsOperation.DEALS_UNSUBSCRIBE.ToString(), new List<string>());
                    SendLogMessage("CoinEx server trades unsubscribe: " + message, LogMessageType.Connect);
                    wsClient.SendAsync(message.ToString());

                    message = new CexRequestSocketUnsubscribe(CexWsOperation.USER_DEALS_UNSUBSCRIBE.ToString(), new List<string>());
                    SendLogMessage("CoinEx server my trades unsubscribe: " + message, LogMessageType.Connect);
                    wsClient.SendAsync(message.ToString());

                    message = new CexRequestSocketUnsubscribe(CexWsOperation.ORDER_UNSUBSCRIBE.ToString(), new List<string>());
                    SendLogMessage("CoinEx server orders unsubscribe: " + message, LogMessageType.Connect);
                    wsClient.SendAsync(message.ToString());

                    wsClient.OnOpen += WebSocket_Opened;
                    wsClient.OnClose += WebSocket_Closed;
                    wsClient.OnError += WebSocketData_Error;
                    wsClient.OnMessage += WebSocket_DataReceived;
                    wsClient.CloseAsync();
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                wsClient = null;
            }
        }

        private void AuthInSocket(WebSocket wsClient)
        {
            if (_wsClients.Count > 1) return;
            CexRequestSocketSign message = new CexRequestSocketSign(_publicKey, _secretKey);
            SendLogMessage("Auth in socket", LogMessageType.Connect);
            wsClient.SendAsync(message.ToString());
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocket_Opened(Object sender, EventArgs e)
        {
            if (_wsClients.Count > 1) return;
            SendLogMessage("Socket Data activated", LogMessageType.System);
            SetConnected();

            AuthInSocket((WebSocket)sender);
            Thread.Sleep(2000);

            CexRequestSocketSubscribePortfolio message = new CexRequestSocketSubscribePortfolio();
            SendLogMessage("Subscribe to portfolios data", LogMessageType.Connect);
            ((WebSocket)sender).SendAsync(message.ToString());
        }

        private void WebSocket_Closed(Object sender, CloseEventArgs e)
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

        private void WebSocketData_Error(object sender, ErrorEventArgs e)
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

        private void WebSocket_DataReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    SendLogMessage("PorfolioWebSocket DataReceived Empty message: State=" + ServerStatus.ToString(),
                        LogMessageType.Connect);
                    return;
                }

                if (e.RawData.Length == 0)
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

                string message = Decompress(e.RawData);

                _webSocketMessage.Enqueue(message);

            }
            catch (Exception error)
            {
                SendLogMessage("Web socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        private DateTime _lastTimeWsCheckConnection = DateTime.MinValue;

        private void ConnectionCheckThread()
        {
            while (true)
            {
                Thread.Sleep(50000); // Sleep1

                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    SendWsPing();
                    Thread.Sleep(3000); // Sleep2

                    // Sleep1 + Sleep2 + some overhead
                    // Trigger when twice fail
                    if (_lastTimeWsCheckConnection.AddSeconds(5) < DateTime.Now && _lastTimeWsCheckConnection > DateTime.MinValue)
                    {
                        if (ServerStatus == ServerConnectStatus.Connect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }
                }
                catch (Exception error)
                {
                    if (ServerStatus == ServerConnectStatus.Connect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        private void SendWsPing()
        {
            if (_wsClients.Count == 0) { return; }
            CexRequestSocketPing message = new CexRequestSocketPing();
            _wsClients[0].SendAsync(message.ToString());
        }
        #endregion

        #region 9 Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<Security> _subscribedSecurities = new List<Security>();

        private List<Security> _currentSubscribedSecurities = new List<Security>();

        public void Subscribe(Security security)
        {
            try
            {
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].NameClass == security.NameClass
                        && _subscribedSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }

                _rateGateSubscribe.WaitToProceed();

                if (_wsClients.Count == 0)
                {
                    return;
                }
                WebSocket wsClient = _wsClients[_wsClients.Count - 1];

                if (wsClient.ReadyState == WebSocketState.Open
                        && _subscribedSecurities.Count != 0
                        && _subscribedSecurities.Count % 50 == 0)
                {
                    WebSocket newSocket = CreateWebSocketConnection();

                    DateTime timeEnd = DateTime.Now.AddSeconds(10);
                    while (newSocket.ReadyState != WebSocketState.Open)
                    {
                        Thread.Sleep(500);

                        if (timeEnd < DateTime.Now)
                        {
                            break;
                        }
                    }

                    if (newSocket.ReadyState == WebSocketState.Open)
                    {
                        _wsClients.Add(newSocket);
                        wsClient = newSocket;
                        _currentSubscribedSecurities.Clear();
                        ServerMaster.SendNewLogMessage("Next 300 securities", LogMessageType.System);
                    }
                    else
                    {
                        SendLogMessage("Error while creating new socket!", LogMessageType.Error);
                    }
                }
                _subscribedSecurities.Add(security);
                _currentSubscribedSecurities.Add(security);

                // Trades subscription
                CexRequestSocketSubscribeDeals message = new CexRequestSocketSubscribeDeals(_currentSubscribedSecurities);
                SendLogMessage("SubcribeToTradesData: " + message, LogMessageType.Connect);
                wsClient.SendAsync(message.ToString());

                // Market depth subscription
                CexRequestSocketSubscribeMarketDepth message1 = new CexRequestSocketSubscribeMarketDepth(_currentSubscribedSecurities, _marketDepth);
                SendLogMessage("SubcribeToMarketDepthData: " + message1, LogMessageType.Connect);
                wsClient.SendAsync(message1.ToString());

                // My orders subscription
                CexRequestSocketSubscribeMyOrders message2 = new CexRequestSocketSubscribeMyOrders(_currentSubscribedSecurities);
                SendLogMessage("SubcribeToMyOrdersData: " + message2, LogMessageType.Connect);
                _wsClients[0].SendAsync(message2.ToString());
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
                    if (ServerStatus != ServerConnectStatus.Connect)
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
                    CexTransactionItem cexTrade = data.deal_list[i];

                    Trade trade = new Trade();
                    //trade.SecurityNameCode = cexTrade.Market;
                    trade.Price = cexTrade.price.ToString().ToDecimal();
                    trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(cexTrade.created_at);
                    //trade.Id = quotes.s_t.ToString() + quotes.side + quotes.symbol;
                    trade.Id = cexTrade.deal_id.ToString();
                    trade.Side = (cexTrade.side == CexOrderSide.BUY.ToString()) ? Side.Buy : trade.Side = Side.Sell;
                    trade.Volume = cexTrade.amount.ToDecimal();
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
                CexWsDepth cexDepth = data.depth;

                MarketDepth depth = new MarketDepth();
                depth.Time = new DateTime(1970, 1, 1).AddMilliseconds(cexDepth.updated_at);
                for (int k = 0; k < cexDepth.bids.Count; k++)
                {
                    (string price, string size) = (cexDepth.bids[k][0], cexDepth.bids[k][1]);
                    MarketDepthLevel newBid = new MarketDepthLevel();
                    newBid.Price = price.ToString().ToDouble();
                    newBid.Bid = size.ToString().ToDouble();
                    if (newBid.Bid > 0)
                    {
                        depth.Bids.Add(newBid);
                    }
                }

                for (int k = 0; k < cexDepth.asks.Count; k++)
                {
                    (string price, string size) = (cexDepth.asks[k][0], cexDepth.asks[k][1]);
                    MarketDepthLevel newAsk = new MarketDepthLevel();
                    newAsk.Price = price.ToString().ToDouble();
                    newAsk.Ask = size.ToString().ToDouble();
                    if (newAsk.Ask > 0)
                    {
                        depth.Asks.Add(newAsk);
                    }
                }

                depth.SecurityNameCode = data.market;

                if (_lastMdTime != DateTime.MinValue &&
                    _lastMdTime >= depth.Time)
                {
                    depth.Time = _lastMdTime.AddMilliseconds(1);
                }

                _lastMdTime = depth.Time;

                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(depth);
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

            if (MyTradeEvent != null)
            //(order.State == OrderStateType.Done || order.State == OrderStateType.Partial ))
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

                if (_marketMode == CexMarketType.SPOT.ToString())
                {
                    wsUpdateSpotPortfolio(data);
                }
                if (_marketMode == CexMarketType.MARGIN.ToString())
                {
                    wsUpdateMarginPortfolio(data);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void wsUpdateSpotPortfolio(CexWsBalance data)
        {
            string portfolioName = getPortfolioName();
            Portfolio portfolio = _portfolios.Find(p => p.Number == portfolioName);

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
                    CexWsBalanceItem cexPosition = data.balance_list[i];

                    pos = new PositionOnBoard();
                    pos.ValueCurrent = cexPosition.available.ToString().ToDecimal();
                    pos.ValueBlocked = cexPosition.frozen.ToString().ToDecimal();
                    pos.SecurityNameCode = cexPosition.ccy;
                    pos.PortfolioName = portfolioName;

                    portfolio.SetNewPosition(pos);
                    continue;
                }

                pos.ValueCurrent = data.balance_list[i].available.ToString().ToDecimal();
                pos.ValueBlocked = data.balance_list[i].frozen.ToString().ToDecimal();
            }

            PortfolioEvent?.Invoke(_portfolios);
        }

        private void wsUpdateMarginPortfolio(CexWsBalance data)
        {
            for (int i = 0; i < data.balance_list.Length; i++)
            {
                string portfolioName = getPortfolioName(data.balance_list[i].margin_market);
                Portfolio portfolio = _portfolios.Find(p => p.Number == portfolioName);

                if (portfolio == null)
                {
                    continue;
                }

                PositionOnBoard pos =
                    portfolio.GetPositionOnBoard().Find(p => p.SecurityNameCode == data.balance_list[i].ccy);

                if (pos == null)
                {
                    CexWsBalanceItem cexPosition = data.balance_list[i];

                    pos = new PositionOnBoard();
                    pos.ValueCurrent = cexPosition.available.ToString().ToDecimal();
                    pos.ValueBlocked = cexPosition.frozen.ToString().ToDecimal();
                    pos.SecurityNameCode = cexPosition.ccy;
                    pos.PortfolioName = portfolioName;

                    portfolio.SetNewPosition(pos);
                    continue;
                }

                pos.ValueCurrent = data.balance_list[i].available.ToString().ToDecimal();
                pos.ValueBlocked = data.balance_list[i].frozen.ToString().ToDecimal();
            }

            PortfolioEvent?.Invoke(_portfolios);
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
                MyOrderEvent?.Invoke(openOrders[i]);
            }
        }

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                // https://docs.coinex.com/api/v2/spot/order/http/put-order#http-request
                Dictionary<string, Object> body = (new CexRequestSendOrder(_marketMode, order)).parameters;

                CexOrder cexOrder = _restClient.Post<CexOrder>("/spot/order", body, true);

                if (cexOrder.order_id > 0)
                {
                    order.State = OrderStateType.Active;
                    order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.updated_at);
                    order.TimeCreate = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.created_at);
                    order.NumberMarket = cexOrder.order_id.ToString();
                    MyOrderEvent?.Invoke(order);
                    SendLogMessage("Order executed", LogMessageType.Trade);
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

        public OrderStateType GetOrderStatus(Order order)
        {
            Order myOrder = cexGetOrderFromExchange(order.SecurityNameCode, order.NumberMarket);

            if (myOrder == null)
            {
                return OrderStateType.None;
            }

            MyOrderEvent?.Invoke(myOrder);

            if (myOrder.State == OrderStateType.Done || myOrder.State == OrderStateType.Partial)
            {
                UpdateTrades(myOrder);
            }

            return myOrder.State;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                // https://docs.coinex.com/api/v2/spot/order/http/edit-order
                Dictionary<string, Object> body = (new CexRequestEditOrder(_marketMode, order, newPrice)).parameters;
                CexOrder cexOrder = _restClient.Post<CexOrder>("/spot/modify-order", body, true);

                if (cexOrder.order_id > 0)
                {
                    order.Price = newPrice;
                    order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.updated_at);
                    MyOrderEvent?.Invoke(order);
                    SendLogMessage("Order price changed", LogMessageType.Trade);
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

        public bool CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();
            lock (_lockOrder)
            {
                try
                {
                    // https://docs.coinex.com/api/v2/spot/order/http/cancel-order
                    Dictionary<string, Object> body = (new CexRequestCancelOrder(_marketMode, order.NumberMarket, order.SecurityNameCode)).parameters;
                    CexOrder cexOrder = _restClient.Post<CexOrder>("/spot/cancel-order", body, true);

                    if (cexOrder.order_id > 0)
                    {
                        order.State = OrderStateType.Cancel;
                        order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.updated_at);
                        order.TimeCancel = order.TimeCallBack;
                        MyOrderEvent?.Invoke(order);
                        SendLogMessage("Order cancelled", LogMessageType.Trade);
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Cancel Order Error. Code: {order.NumberUser}.", LogMessageType.Error);
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
                    SendLogMessage("Cancel order error. " + exception.ToString(), LogMessageType.Error);
                }
            }
            return false;
        }

        public void CancelAllOrders()
        {
            for (int i = 0; i < _subscribedSecurities.Count; i++)
            {
                CancelAllOrdersToSecurity(_subscribedSecurities[i]);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            cexCancelAllOrdersToSecurity(security.NameFull);
        }

        private List<Order> cexGetAllActiveOrders()
        {
            try
            {
                //_subscribedSecurities
                List<CexOrder> cexOrders = new List<CexOrder>();
                // https://docs.coinex.com/api/v2/spot/order/http/list-pending-order
                if (_marketMode == CexMarketType.MARGIN.ToString())
                {
                    for (int i = 0; i < _subscribedSecurities.Count; i++)
                    {
                        _rateGateGetOrder.WaitToProceed();
                        Dictionary<string, Object> parameters = (new CexRequestPendingOrders(_marketMode, _subscribedSecurities[i].Name)).parameters;
                        List<CexOrder> tmpCexOrders = _restClient.Get<List<CexOrder>>("/spot/pending-order", true, parameters);
                        if (tmpCexOrders != null && tmpCexOrders.Count > 0)
                        {
                            cexOrders.AddRange(tmpCexOrders);
                        }
                    }
                }
                else
                {
                    Dictionary<string, Object> parameters = (new CexRequestPendingOrders(_marketMode)).parameters;
                    cexOrders = _restClient.Get<List<CexOrder>>("/spot/pending-order", true, parameters);
                }

                if (cexOrders == null || cexOrders.Count == 0)
                {
                    return null;
                }

                List<Order> orders = new List<Order>();

                for (int i = 0; i < cexOrders.Count; i++)
                {
                    if (string.IsNullOrEmpty(cexOrders[i].client_id))
                    {
                        SendLogMessage("Non OS Engine order with id:" + cexOrders[i].order_id + ". Skipped.", LogMessageType.System);
                        continue;
                    }

                    Order order = GetOrderOsEngineFromCexOrder(cexOrders[i]);

                    if (order.NumberUser == 0)
                    {
                        continue;
                    }
                    order.PortfolioNumber = getPortfolioName(order.SecurityNameCode);

                    orders.Add(order);
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
                CexOrder cexOrder = _restClient.Get<CexOrder>("/spot/order-status", true, parameters);

                if (!string.IsNullOrEmpty(cexOrder.client_id))
                {
                    Order order = GetOrderOsEngineFromCexOrder(cexOrder);
                    order.PortfolioNumber = getPortfolioName(order.SecurityNameCode);
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
                    Object result = _restClient.Post<Object>("/spot/cancel-all-order", body, true);
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

        private Order GetOrderOsEngineFromCexOrder(CexOrder cexOrder)
        {
            Order order = new Order();

            order.NumberUser = Convert.ToInt32(cexOrder.client_id);

            order.SecurityNameCode = cexOrder.market;
            order.SecurityClassCode = cexOrder.market.Substring(cexOrder.ccy?.Length ?? cexOrder.market.Length - 3); // Fix for Futures (no Currency info)
            order.Volume = cexOrder.amount.ToString().ToDecimal(); // FIX Разобраться с названием параметра!
            order.VolumeExecute = cexOrder.filled_amount.ToString().ToDecimal(); // FIX Разобраться с названием параметра!

            order.Price = cexOrder.price.ToString().ToDecimal();
            if (cexOrder.type == CexOrderType.LIMIT.ToString())
            {
                order.TypeOrder = OrderPriceType.Limit;
            }
            else if (cexOrder.type == CexOrderType.MARKET.ToString())
            {
                order.TypeOrder = OrderPriceType.Market;
            }

            order.ServerType = ServerType.CoinExSpot;

            order.NumberMarket = cexOrder.order_id.ToString();

            order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.updated_at);
            order.TimeCreate = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.created_at);

            order.Side = (cexOrder.side == CexOrderSide.BUY.ToString()) ? Side.Buy : Side.Sell;


            // Order placed successfully (unfilled/partially filled)
            order.State = OrderStateType.None;
            if (!string.IsNullOrEmpty(cexOrder.status))
            {
                if (cexOrder.status == CexOrderStatus.OPEN.ToString())
                {
                    order.State = OrderStateType.Active;
                }
                else if (cexOrder.status == CexOrderStatus.PART_FILLED.ToString())
                {
                    order.State = OrderStateType.Partial;
                }
                else if (cexOrder.status == CexOrderStatus.FILLED.ToString())
                {
                    order.State = OrderStateType.Done;
                    order.TimeDone = order.TimeCallBack;
                }
                else if (cexOrder.status == CexOrderStatus.PART_CANCELED.ToString())
                {
                    order.State = OrderStateType.Cancel;
                    order.TimeCancel = order.TimeCallBack;
                }
                else if (cexOrder.status == CexOrderStatus.CANCELED.ToString())
                {
                    order.State = OrderStateType.Cancel;
                    order.TimeCancel = order.TimeCallBack;
                }
                else
                {
                    order.State = OrderStateType.Fail;
                }
            }
            else
            {
                if (cexOrder.unfilled_amount.ToString().ToDecimal() > 0)
                {
                    order.State = cexOrder.amount == cexOrder.unfilled_amount ? OrderStateType.Active : OrderStateType.Partial;
                }
            }

            return order;
        }

        private List<MyTrade> GetTradesForOrder(string orderId, string market)
        {
            _rateGateOrdersHistory.WaitToProceed();

            try
            {
                // https://docs.coinex.com/api/v2/spot/deal/http/list-user-order-deals#http-request
                Dictionary<string, Object> parameters = (new CexRequestOrderDeals(_marketMode, orderId, market)).parameters;
                List<CexOrderTransaction> cexTrades = _restClient.Get<List<CexOrderTransaction>>("/spot/order-deals", true, parameters);

                if (cexTrades != null)
                {
                    List<MyTrade> trades = new List<MyTrade>();

                    for (int i = 0; i < cexTrades.Count; i++)
                    {
                        CexOrderTransaction cexTrade = cexTrades[i];
                        MyTrade trade = new MyTrade();
                        trade.NumberOrderParent = cexTrade.order_id.ToString();
                        trade.NumberTrade = cexTrade.deal_id.ToString();
                        trade.SecurityNameCode = string.IsNullOrEmpty(cexTrade.margin_market) ? cexTrade.market : cexTrade.margin_market;
                        trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(cexTrade.created_at);
                        trade.Side = (cexTrade.side == CexOrderSide.BUY.ToString()) ? Side.Buy : Side.Sell;
                        trade.Price = cexTrade.price.ToString().ToDecimal();
                        trade.Volume = cexTrade.amount.ToString().ToDecimal();
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

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        #endregion

        #region 12 Queries

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
           /* // https://docs.coinex.com/api/v2/spot/market/http/list-market-deals#http-request
            // Max 1000 deals at all
            List<Trade> trades = new List<Trade>();
            try
            {
                Dictionary<string, Object> parameters = (new CexRequestGetDeals(security.Name)).parameters;
                List<CexTransaction> cexDeals = _restClient.Get<List<CexTransaction>>("/spot/deals", false, parameters);

                for (int i = cexDeals.Count - 1; i >= 0; i--)
                {
                    CexTransaction cexTrade = cexDeals[i];

                    Trade trade = new Trade();
                    trade.Id = cexTrade.deal_id.ToString();
                    //trade.SecurityNameCode = cexTrade.market;
                    trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(cexTrade.created_at);
                    trade.Side = (cexTrade.side == CexOrderSide.BUY.ToString()) ? Side.Buy : Side.Sell;
                    trade.Price = cexTrade.price.ToString().ToDecimal();
                    trade.Volume = cexTrade.amount.ToString().ToDecimal();

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
            return trades.Count > 0 ? trades : null;*/
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
                    CexCandle candle = new CexCandle();
                    candle.market = security.Name;

                    List<object> data = resp.data[i];

                    candle.created_at = 1000 * (long)data[0];
                    candle.open = data[1].ToString();
                    candle.close = data[2].ToString();
                    candle.high = data[3].ToString();
                    candle.low = data[4].ToString();
                    candle.volume = data[5].ToString();
                    candle.value = data[6].ToString();

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
                SendLogMessage("Candles request error:" + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        #endregion

        #region 13 Log

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<News> NewsEvent { add { } remove { } }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        #endregion

        #region 14 Helpers

        private static string Decompress(byte[] data)
        {
            using (System.IO.MemoryStream msi = new System.IO.MemoryStream(data))
            using (System.IO.MemoryStream mso = new System.IO.MemoryStream())
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

            Order order = new Order();
            order.State = OrderStateType.Active;
            order.NumberUser = string.IsNullOrEmpty(cexOrder.client_id) ? 0 : Convert.ToInt32(cexOrder.client_id);

            order.SecurityNameCode = cexOrder.market;
            // Cex.Amount - объём в единицах тикера
            // Cex.Value - объём в деньгах
            order.Volume = cexOrder.amount.ToString().ToDecimal();
            order.VolumeExecute = cexOrder.filled_amount.ToString().ToDecimal(); // FIX Разобраться с названием параметра!

            if (cexOrder.type == CexOrderType.LIMIT.ToString())
            {
                order.Price = cexOrder.price.ToString().ToDecimal();
                order.TypeOrder = OrderPriceType.Limit;
            }
            else if (cexOrder.type == CexOrderType.MARKET.ToString())
            {
                order.TypeOrder = OrderPriceType.Market;
            }

            order.ServerType = ServerType.CoinExSpot;
            order.NumberMarket = cexOrder.order_id.ToString();
            order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.updated_at);
            order.TimeCreate = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.created_at);
            order.Side = (cexOrder.side == CexOrderSide.BUY.ToString()) ? OsEngine.Entity.Side.Buy : OsEngine.Entity.Side.Sell;

            if (order == null)
            {
                string msg = string.Format("Failed to convert CexWsOrderUpdate в Os Engine Order!{0}cexEventUpdate: {1}{0}order: null", Environment.NewLine,
                    JsonConvert.SerializeObject(cexEventUpdate)
                    );
                SendLogMessage(msg, LogMessageType.Error);
            }

            if (order.NumberUser == 0)
            {
                return null;
                //string msg = string.Format("Unknown order!{0}Empty NumberUser! {0}cexEventUpdate: {1}{0}order: {2}", Environment.NewLine,
                //    JsonConvert.SerializeObject(cexEventUpdate),
                //    JsonConvert.SerializeObject(order)
                //    );
                //SendLogMessage(msg, LogMessageType.Error);
            }

            order.PortfolioNumber = getPortfolioName(order.SecurityNameCode);
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

        public bool SubscribeNews()
        {
            throw new NotImplementedException();
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