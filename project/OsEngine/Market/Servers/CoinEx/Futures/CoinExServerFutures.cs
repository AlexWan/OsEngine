/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.CoinEx.Futures.Entity;
using OsEngine.Market.Servers.CoinEx.Futures.Entity.Enums;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace OsEngine.Market.Servers.CoinEx.Futures
{
    public class CoinExServerFutures : AServer
    {
        public CoinExServerFutures()
        {
            CoinExServerRealization realization = new CoinExServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterEnum("Market depth", "20", new List<string> { "5", "10", "20", "50" });
        }
    }

    public class CoinExServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public CoinExServerRealization()
        {
            Thread worker = new Thread(DataMessageReaderThread);
            worker.Name = "DataMessageReaderCoinEx";
            worker.Start();

            Thread worker1 = new Thread(ConnectionCheckThread);
            worker1.Name = "CheckAliveCoinEx";
            worker1.Start();

            Thread threadGetPortfolios = new Thread(ThreadGetPortfolios);
            threadGetPortfolios.IsBackground = true;
            threadGetPortfolios.Name = "ThreadCoinExFuturesPortfolios";
            threadGetPortfolios.Start();
        }

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                _securities.Clear();
                //_portfolios.Clear();
                _wsClients.Clear();
                _subscribedSecurities.Clear();

                SendLogMessage("Start CoinEx Futures Connection", LogMessageType.Connect);

                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
                _marketDepth = Int16.Parse(((ServerParameterEnum)ServerParameters[2]).Value);
                _marketMode = CexMarketType.FUTURES.ToString();

                if (string.IsNullOrEmpty(_publicKey) || string.IsNullOrEmpty(_secretKey))
                {
                    SendLogMessage("Connection terminated. You must specify the public and private keys. You can get it on the CoinEx website.",
                        LogMessageType.Error);
                    return;
                }

                _restClient = new CoinExRestClient(_publicKey, _secretKey);
                _restClient.LogMessageEvent += SendLogMessage;

                RestRequest requestRest = new RestRequest("/time", Method.GET);
                IRestResponse response = new RestClient(_baseUrl).Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    _wsClients.Add(CreateWebSocketConnection());
                }
                else
                {
                    SendLogMessage("Connection cannot be open. CoinExFutures. Error request", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
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

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public DateTime ServerTime { get; set; }

        public ServerType ServerType
        {
            get { return ServerType.CoinExFutures; }
        }

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _publicKey;

        private string _secretKey;

        private string _baseUrl = "https://api.coinex.com/v2";

        private int _marketDepth;

        // Futures only
        private string _marketMode;
        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            UpdateSecurity();

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                if (SecurityEvent != null)
                {
                    SecurityEvent.Invoke(_securities);
                }
            }
        }

        private List<Security> _securities = new List<Security>();

        private void UpdateSecurity()
        {
            // https://docs.coinex.com/api/v2/futures/market/http/list-market
            try
            {
                RestRequest requestRest = new RestRequest("/futures/market", Method.GET);
                IRestResponse response = new RestClient(_baseUrl).Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<MarketInfoData>> responseMarket = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<MarketInfoData>>());

                    if (responseMarket.code == "0")
                    {
                        for (int i = 0; i < responseMarket.data.Count; i++)
                        {
                            MarketInfoData item = responseMarket.data[i];

                            Security security = new Security();
                            security.Name = item.market;
                            security.NameId = item.market;
                            security.NameFull = item.market;
                            security.NameClass = item.quote_ccy;
                            security.State = SecurityStateType.Activ;
                            security.Decimals = Convert.ToInt32(item.quote_ccy_precision);
                            security.MinTradeAmount = item.min_amount.ToDecimal();
                            security.DecimalsVolume = Convert.ToInt32(item.base_ccy_precision);
                            security.PriceStep = item.tick_size.ToDecimal();
                            security.PriceStepCost = security.PriceStep;
                            security.Lot = 1;
                            security.SecurityType = SecurityType.Futures;
                            security.Exchange = ServerType.CoinExFutures.ToString();
                            security.MinTradeAmountType = MinTradeAmountType.Contract;
                            security.VolumeStep = security.DecimalsVolume.GetValueByDecimals();

                            _securities.Add(security);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Securities error. Code:{responseMarket.code} || msg: {responseMarket.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Securities error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Securities request error: " + exception.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public string getPortfolioName(string securityName = "")
        {
            return ServerType.CoinExFutures.ToString();
        }

        public void GetPortfolios()
        {
            if (_portfolios == null)
            {
                GetNewPortfolio();
            }

            GetCurrentPortfolios(true);
            GetCurrentPositions();
        }

        private List<Portfolio> _portfolios;

        private void GetNewPortfolio()
        {
            _portfolios = new List<Portfolio>();
            string portfolioName = getPortfolioName();
            Portfolio portfolioInitial = new Portfolio();
            portfolioInitial.Number = portfolioName;
            portfolioInitial.ValueBegin = 1;
            portfolioInitial.ValueCurrent = 1;
            portfolioInitial.ValueBlocked = 0;

            _portfolios.Add(portfolioInitial);

            PortfolioEvent(_portfolios);
        }

        private void ThreadGetPortfolios()
        {
            Thread.Sleep(10000);

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                    continue;
                }

                try
                {
                    Thread.Sleep(5000);

                    GetCurrentPortfolios(false);
                    GetCurrentPositions();
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }
        }

        private void GetCurrentPortfolios(bool IsUpdateValueBegin)
        {
            _rateGateAccountStatus.WaitToProceed();

            try
            {
                IRestResponse response = CreatePrivateQuery("/assets/futures/balance", Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<BalanceData>> responseBalance = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<BalanceData>>());

                    string portfolioName = getPortfolioName();
                    Portfolio myPortfolio = _portfolios[0];

                    if (responseBalance.data == null
                        || responseBalance.data.Count == 0)
                    {
                        return;
                    }

                    if (responseBalance.code == "0")
                    {
                        decimal positionInUSDT = 0;
                        decimal positionPnL = 0;
                        decimal positionBlocked = 0;

                        for (int i = 0; i < responseBalance.data.Count; i++)
                        {
                            BalanceData item = responseBalance.data[i];

                            if (item.ccy == "USDT")
                            {
                                positionInUSDT = item.available.ToDecimal();
                            }
                            else if (item.ccy == "USDC"
                                || item.ccy == "BTC"
                                || item.ccy == "ETH")
                            {
                                //positionInUSDT += GetPriceSecurity(item.ccy + "USDT") * item.available.ToDecimal();
                            }

                            PositionOnBoard pos = new PositionOnBoard();

                            pos.PortfolioName = portfolioName;
                            pos.SecurityNameCode = item.ccy;
                            pos.ValueCurrent = Math.Round(item.available.ToDecimal(), 5);
                            pos.ValueBlocked = Math.Round(item.frozen.ToDecimal(), 5);
                            pos.UnrealizedPnl = Math.Round(item.unrealized_pnl.ToDecimal(), 5);

                            if (IsUpdateValueBegin)
                            {
                                pos.ValueBegin = Math.Round(item.available.ToDecimal(), 5);
                            }

                            positionPnL += pos.UnrealizedPnl;
                            positionBlocked += pos.ValueBlocked;

                            myPortfolio.SetNewPosition(pos);
                        }

                        myPortfolio.ValueCurrent = Math.Round(positionInUSDT, 5);
                        myPortfolio.UnrealizedPnl = positionPnL;
                        myPortfolio.ValueBlocked = positionBlocked;

                        if (IsUpdateValueBegin)
                        {
                            myPortfolio.ValueBegin = Math.Round(positionInUSDT, 5);
                        }

                        PortfolioEvent(_portfolios);
                    }
                    else
                    {
                        SendLogMessage($"Portfolio error. Code:{responseBalance.code} || msg: {responseBalance.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio request error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void GetCurrentPositions()
        {
            _rateGateAccountStatus.WaitToProceed();

            if (_portfolios == null)
            {
                return;
            }

            try
            {
                IRestResponse response = CreatePrivateQuery("/futures/pending-position?market_type=FUTURES", Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<PositionData>> responsePositions = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<PositionData>>());

                    if (responsePositions.data == null)
                    {
                        return;
                    }

                    if (responsePositions.code == "0")
                    {
                        string portfolioName = getPortfolioName();
                        Portfolio portfolio = _portfolios[0];
                        List<PositionData> positionsOnBoard = responsePositions.data;

                        if (responsePositions.data.Count != 0)
                        {
                            for (int i = 0; i < responsePositions.data.Count; i++)
                            {
                                PositionData item = responsePositions.data[i];
                                PositionOnBoard pos = new PositionOnBoard();

                                pos.PortfolioName = portfolioName;
                                pos.SecurityNameCode = item.market;
                                pos.UnrealizedPnl = item.unrealized_pnl.ToDecimal();

                                if (item.side == "long")
                                {
                                    pos.ValueCurrent = item.close_avbl.ToDecimal();
                                }
                                else
                                {
                                    pos.ValueCurrent = -item.close_avbl.ToDecimal();
                                }

                                portfolio.SetNewPosition(pos);
                            }

                            ClosePositions(portfolio, positionsOnBoard);
                        }
                        else
                        {
                            ClosePositions(portfolio, positionsOnBoard);
                        }

                        PortfolioEvent(_portfolios);
                    }
                    else
                    {
                        SendLogMessage($"Positions error. Code:{responsePositions.code} || msg: {responsePositions.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Positions request error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Positions request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void ClosePositions(Portfolio portfolio, List<PositionData> positionsOnBoard)
        {
            List<PositionOnBoard> positionInPortfolio = portfolio.GetPositionOnBoard();

            if (positionInPortfolio == null)
            {
                return;
            }

            for (int j = 0; j < positionInPortfolio.Count; j++)
            {
                if (positionInPortfolio[j].SecurityNameCode == "USDT")
                {
                    continue;
                }

                bool isInArray = false;

                for (int i = 0; i < positionsOnBoard.Count; i++)
                {
                    PositionData item = positionsOnBoard[i];

                    string curNameSec = item.market;

                    if (curNameSec == positionInPortfolio[j].SecurityNameCode)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    positionInPortfolio[j].ValueCurrent = 0;
                }
            }
        }

        public List<CexMarketInfoItem> GetMarketsInfo(List<string> securities)
        {
            // https://docs.coinex.com/api/v2/futures/market/http/list-market-ticker
            List<CexMarketInfoItem> cexInfo = new List<CexMarketInfoItem>();

            string endPoint = "/futures/ticker";
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

        public event Action<List<Portfolio>> PortfolioEvent;

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

        private readonly string _wsUrl = "wss://socket.coinex.com/v2/futures";

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
                     = System.Security.Authentication.SslProtocols.Tls12
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

        public bool SubscribeNews()
        {
            return false;
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
            // https://docs.coinex.com/api/v2/futures/market/ws/market-deals
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
            // https://docs.coinex.com/api/v2/futures/market/ws/market-depth
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

                //Dictionary<string, Object> parameters = (new CexRequestGetPendingPosition(_marketMode)).parameters;
                //List<CexPositionItem>? cexPendingPositions = _restClient.Get<List<CexPositionItem>>("/futures/pending-position", true, parameters);

                wsUpdateFuturesPortfolio(data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void wsUpdateFuturesPortfolio(CexWsBalance data)
        {
            string portfolioName = getPortfolioName();
            Portfolio myPortfolio = _portfolios.Find(p => p.Number == portfolioName);

            if (myPortfolio == null)
            {
                return;
            }

            for (int i = 0; i < data.balance_list.Length; i++)
            {
                CexWsBalanceItem cexPosition = data.balance_list[i];

                PositionOnBoard pos = new PositionOnBoard();

                pos.SecurityNameCode = cexPosition.ccy;
                pos.ValueCurrent = Math.Round(cexPosition.available.ToDecimal(), 2);
                pos.UnrealizedPnl = Math.Round(cexPosition.unrealized_pnl.ToDecimal(), 2);
                pos.ValueBlocked = Math.Round(cexPosition.margin.ToDecimal(), 2);

                myPortfolio.SetNewPosition(pos);
            }

            //if (pendingPositions != null && pendingPositions.Count > 0)
            //{
            //    for (int i = 0; i < pendingPositions.Count; i++)
            //    {
            //        CexPositionItem cexPositionItem = pendingPositions[i];

            //        PositionOnBoard pos = new PositionOnBoard();

            //        pos.SecurityNameCode = cexPositionItem.market;
            //        pos.ValueCurrent = cexPositionItem.open_interest.ToDecimal();
            //        pos.ValueBlocked = pos.ValueCurrent;
            //        pos.PortfolioName = portfolioName;
            //        pos.UnrealizedPnl = cexPositionItem.unrealized_pnl.ToDecimal();
            //        if (pos.ValueBegin == 1)
            //        {
            //            pos.ValueBegin = pos.ValueCurrent + pos.ValueBlocked;
            //        }

            //        if (cexPositionItem.side == "short")
            //        {
            //            pos.ValueCurrent = -pos.ValueCurrent;
            //            pos.ValueBlocked = -pos.ValueBlocked;
            //        }

            //        if (Math.Abs(pos.ValueBlocked + pos.ValueCurrent) > 0)
            //        {
            //            myPortfolio.SetNewPosition(pos);
            //        }
            //    }

            //}

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
                // https://docs.coinex.com/api/v2/futures/order/http/put-order#http-request
                Dictionary<string, Object> body = (new CexRequestSendOrder(_marketMode, order)).parameters;

                CexOrder cexOrder = _restClient.Post<CexOrder>("/futures/order", body, true);

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
                // https://docs.coinex.com/api/v2/futures/order/http/edit-order
                Dictionary<string, Object> body = (new CexRequestEditOrder(_marketMode, order, newPrice)).parameters;
                CexOrder cexOrder = _restClient.Post<CexOrder>("/futures/modify-order", body, true);

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
                    // https://docs.coinex.com/api/v2/futures/order/http/cancel-order
                    Dictionary<string, Object> body = (new CexRequestCancelOrder(_marketMode, order.NumberMarket, order.SecurityNameCode)).parameters;
                    CexOrder cexOrder = _restClient.Post<CexOrder>("/futures/cancel-order", body, true);

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
                _rateGateGetOrder.WaitToProceed();
                List<CexOrder> cexOrders = new List<CexOrder>();
                // https://docs.coinex.com/api/v2/futures/order/http/list-pending-order
                // https://docs.coinex.com/api/v2/futures/position/http/list-pending-position

                Dictionary<string, Object> parameters = (new CexRequestPendingOrders(_marketMode)).parameters;
                cexOrders = _restClient.Get<List<CexOrder>>("/futures/pending-order", true, parameters);

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
                // https://docs.coinex.com/api/v2/futures/order/http/get-order-status
                Dictionary<string, Object> parameters = (new CexRequestOrderStatus(orderId, market)).parameters;
                CexOrder cexOrder = _restClient.Get<CexOrder>("/futures/order-status", true, parameters);

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
                    // https://docs.coinex.com/api/v2/futures/order/http/cancel-all-order
                    Dictionary<string, Object> body = (new CexRequestCancelAllOrders(_marketMode, security)).parameters;
                    Object result = _restClient.Post<Object>("/futures/cancel-all-order", body, true);
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
            order.SecurityClassCode = cexOrder.market.Substring(cexOrder.market.Length - 4);
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

            order.ServerType = ServerType.CoinExFutures;

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
                // https://docs.coinex.com/api/v2/futures/deal/http/list-user-order-deals#http-request
                Dictionary<string, Object> parameters = (new CexRequestOrderDeals(_marketMode, orderId, market)).parameters;
                List<CexOrderTransaction> cexTrades = _restClient.Get<List<CexOrderTransaction>>("/futures/order-deals", true, parameters);

                if (cexTrades != null)
                {
                    List<MyTrade> trades = new List<MyTrade>();

                    for (int i = 0; i < cexTrades.Count; i++)
                    {
                        CexOrderTransaction cexTrade = cexTrades[i];
                        MyTrade trade = new MyTrade();
                        trade.NumberOrderParent = cexTrade.order_id.ToString();
                        trade.NumberTrade = cexTrade.deal_id.ToString();
                        trade.SecurityNameCode = cexTrade.market;
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

        private IRestResponse CreatePrivateQuery(string path, Method method, string body = null)
        {
            try
            {
                //string requestPath = path;
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature(path, method.ToString(), timestamp, body);

                RestRequest requestRest = new RestRequest(path, method);
                requestRest.AddHeader("X-COINEX-KEY", _publicKey);
                requestRest.AddHeader("X-COINEX-SIGN", signature);
                requestRest.AddHeader("X-COINEX-TIMESTAMP", timestamp);

                if (body != null)
                {
                    requestRest.AddParameter("application/json", body, ParameterType.RequestBody);
                }

                IRestResponse response = new RestClient(_baseUrl).Execute(requestRest);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        public string GenerateSignature(string path, string method, string timestamp, string body)
        {
            string message = method + "/v2" + path + body + timestamp;

            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey)))
            {
                byte[] r = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(r).Replace("-", "").ToLower();
            }
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
            /*
            // https://docs.coinex.com/api/v2/futures/market/http/list-market-deals#http-request
            // Max 1000 deals at all
            List<Trade> trades = new List<Trade>();
            try
            {
                Dictionary<string, Object> parameters = (new CexRequestGetDeals(security.Name)).parameters;
                List<CexTransaction> cexDeals = _restClient.Get<List<CexTransaction>>("/futures/deals", false, parameters);

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
            if (startTime > DateTime.UtcNow) return null;
            long tsStartTime = TimeManager.GetTimeStampSecondsToDateTime(startTime);
            long tsEndTime = (endTime > DateTime.UtcNow) ? TimeManager.GetTimeStampSecondsToDateTime(DateTime.UtcNow) : TimeManager.GetTimeStampSecondsToDateTime(endTime);

            if (tsStartTime > tsEndTime || tsStartTime < 0 || tsEndTime < 0) { return null; }

            //https://www.coinex.com/res/contract/market/kline?market=TONUSDT&start_time=1741330800&end_time=1741374900&interval=900
            string url = string.Format("https://www.coinex.com/res/contract/market/kline?market={0}&start_time={1}&end_time={2}&interval={3}",
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

            order.ServerType = ServerType.CoinExFutures;
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