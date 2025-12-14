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
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Net;
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
            CreateParameterBoolean("Extended Data", false);

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.ServerParam13;
            ServerParameters[3].Comment = OsLocalization.Market.Label270;
        }
    }

    public class CoinExServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public CoinExServerRealization()
        {
            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.Name = "MessageReaderPublicCoinExFutures";
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.Name = "MessageReaderPrivateCoinExFutures";
            threadMessageReaderPrivate.Start();

            Thread threadConnectionCheck = new Thread(ConnectionCheckThread);
            threadConnectionCheck.Name = "CheckAliveCoinEx";
            threadConnectionCheck.Start();

            Thread threadGetPortfolios = new Thread(ThreadGetPortfolios);
            threadGetPortfolios.IsBackground = true;
            threadGetPortfolios.Name = "ThreadCoinExFuturesPortfolios";
            threadGetPortfolios.Start();
        }

        public void Connect(WebProxy proxy = null)
        {

            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _marketDepth = Int16.Parse(((ServerParameterEnum)ServerParameters[2]).Value);

            if (string.IsNullOrEmpty(_publicKey)
                || string.IsNullOrEmpty(_secretKey))
            {
                SendLogMessage("Connection terminated. You must specify the public and private keys. You can get it on the CoinEx website.",
                    LogMessageType.Error);
                return;
            }

            if (((ServerParameterBool)ServerParameters[3]).Value == true)
            {
                _extendedMarketData = true;
            }
            else
            {
                _extendedMarketData = false;
            }
            try
            {
                RestRequest requestRest = new RestRequest("/time", Method.GET);
                IRestResponse response = new RestClient(_baseUrl).Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
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
                UnsubscribeFromAllWebSockets();
                DeleteWebSocketConnection();

                SendLogMessage("Dispose. Connection Closed by CoinExFutures. WebSocket Closed Event", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            _subscribedSecurities.Clear();
            FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

            Disconnect();
        }

        public void Disconnect()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

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

        private bool _extendedMarketData;

        #endregion

        #region 3 Securities

        private List<Security> _securities;

        public void GetSecurities()
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

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

                            if (item.status != "online")
                            {
                                continue;
                            }

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

                        if (_securities.Count > 0)
                        {
                            SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                            if (SecurityEvent != null)
                            {
                                SecurityEvent.Invoke(_securities);
                            }
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

        public string getPortfolioName()
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
                    Thread.Sleep(15000);

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

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

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
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.UtcNow;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);

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
                List<ResponseCandle> history = cexGetCandleHistory(security, tfTotalMinutes, actualTime, endTimeReal);
                if (history != null && history.Count > 0)
                {
                    for (int i = 0; i < history.Count; i++)
                    {
                        ResponseCandle cexCandle = history[i];

                        Candle candle = new Candle();
                        candle.Open = cexCandle.open.ToString().ToDecimal();
                        candle.High = cexCandle.high.ToString().ToDecimal();
                        candle.Low = cexCandle.low.ToString().ToDecimal();
                        candle.Close = cexCandle.close.ToString().ToDecimal();
                        candle.Volume = cexCandle.volume.ToString().ToDecimal();
                        candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp((long)cexCandle.created_at.ToDecimal());

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

        private List<ResponseCandle> cexGetCandleHistory(Security security, int tfTotalMinutes,
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
                RestRequest requestRest = new RestRequest(Method.GET);
                IRestResponse response = new RestClient(url).Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<List<object>>> responseCandles = JsonConvert.DeserializeObject<ResponseRestMessage<List<List<object>>>>(response.Content);

                    if (responseCandles.code == "0")
                    {
                        List<ResponseCandle> cexCandles = new List<ResponseCandle>();

                        for (int i = 0; i < responseCandles.data.Count; i++)
                        {
                            ResponseCandle candle = new ResponseCandle();
                            candle.market = security.Name;

                            List<object> data = responseCandles.data[i];

                            candle.created_at = (1000 * (long)data[0]).ToString();
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
                            SendLogMessage($"Empty Candles response to url {url}", LogMessageType.System);
                            return cexCandles;
                        }
                    }
                    else
                    {
                        SendLogMessage($"Candles request error. {responseCandles.code} || msg: {responseCandles.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Candles request error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Candles request error:" + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
            /*
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

        #endregion

        #region 6 WebSocket creation

        private string _wsUrl = "wss://socket.coinex.com/v2/futures";

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (FIFOListWebSocketPublicMessage == null)
                {
                    FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                }

                _webSocketPublic.Add(CreateNewPublicSocket());
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewPublicSocket()
        {
            try
            {
                WebSocket webSocketPublicNew = new WebSocket(_wsUrl);

                //if (_myProxy != null)
                //{
                //    webSocketPublicNew.SetProxy(_myProxy);
                //}

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += WebSocketPublicNew_OnOpen;
                webSocketPublicNew.OnMessage += WebSocketPublicNew_OnMessage;
                webSocketPublicNew.OnError += WebSocketPublicNew_OnError;
                webSocketPublicNew.OnClose += WebSocketPublicNew_OnClose;
                webSocketPublicNew.ConnectAsync();

                return webSocketPublicNew;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void CreatePrivateWebSocketConnect()
        {
            try
            {
                if (_webSocketPrivate != null)
                {
                    return;
                }

                _webSocketPrivate = new WebSocket(_wsUrl);

                //if (_myProxy != null)
                //{
                //    _webSocketPrivate.SetProxy(_myProxy);
                //}

                _webSocketPrivate.EmitOnPing = true;
                _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                _webSocketPrivate.OnError += _webSocketPrivate_OnError;
                _webSocketPrivate.ConnectAsync();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebSocketConnection()
        {
            if (_webSocketPublic != null)
            {
                try
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        webSocketPublic.OnOpen -= WebSocketPublicNew_OnOpen;
                        webSocketPublic.OnClose -= WebSocketPublicNew_OnClose;
                        webSocketPublic.OnMessage -= WebSocketPublicNew_OnMessage;
                        webSocketPublic.OnError -= WebSocketPublicNew_OnError;

                        if (webSocketPublic.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.CloseAsync();
                        }

                        webSocketPublic = null;
                    }
                }
                catch
                {
                    // ignore
                }

                _webSocketPublic.Clear();
            }

            if (_webSocketPrivate != null)
            {
                try
                {
                    _webSocketPrivate.OnOpen -= _webSocketPrivate_OnOpen;
                    _webSocketPrivate.OnClose -= _webSocketPrivate_OnClose;
                    _webSocketPrivate.OnMessage -= _webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError -= _webSocketPrivate_OnError;
                    _webSocketPrivate.CloseAsync();
                }
                catch
                {
                    // ignore
                }

                _webSocketPrivate = null;
            }
        }

        private void AuthInSocket(WebSocket wsClient)
        {
            //if (_wsClients.Count > 1) return;

            long timestamp = TimeManager.GetUnixTimeStampMilliseconds();
            string sign = Sign(timestamp.ToString());

            wsClient.SendAsync($"{{\"method\":\"server.sign\",\"params\":{{\"access_id\":\"{_publicKey}\",\"signed_str\":\"{sign}\",\"timestamp\":{timestamp}}},\"id\":1}}");
        }

        private string _socketActivateLocker = "socketAcvateLocker";

        private void CheckActivationSockets()
        {
            lock (_socketActivateLocker)
            {

                if (_webSocketPrivate == null
                    || _webSocketPrivate.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (_webSocketPublic.Count == 0)
                {
                    Disconnect();
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[0];

                if (webSocketPublic == null
                    || webSocketPublic?.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Connect;

                    if (ConnectEvent != null)
                    {
                        ConnectEvent();
                    }
                }
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublicNew_OnClose(object sender, CloseEventArgs e)
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

        private void WebSocketPublicNew_OnError(object sender, ErrorEventArgs e)
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

        private void WebSocketPublicNew_OnMessage(object sender, MessageEventArgs e)
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

                if (FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                if (e.IsBinary)
                {
                    string message = Decompress(e.RawData);

                    if (message.Contains("pong"))
                    {
                        return;
                    }

                    FIFOListWebSocketPublicMessage.Enqueue(message);
                }
            }
            catch (Exception error)
            {
                SendLogMessage("Web socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    CheckActivationSockets();
                    SendLogMessage("CoinExFutures WebSocket Public connection open", LogMessageType.System);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnError(object sender, ErrorEventArgs e)
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

        private void _webSocketPrivate_OnMessage(object sender, MessageEventArgs e)
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

                if (FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                if (e.IsBinary)
                {
                    string message = Decompress(e.RawData);

                    if (message.Contains("{\"id\":1,\"code\":0,\"message\":\"OK\"}"))
                    {
                        SubscribePrivate();
                    }

                    if (message.Contains("pong"))
                    {
                        return;
                    }

                    FIFOListWebSocketPrivateMessage.Enqueue(message);
                }
            }
            catch (Exception error)
            {
                SendLogMessage("Web socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnClose(object sender, CloseEventArgs e)
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

        private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
        {
            try
            {
                AuthInSocket((WebSocket)sender);
                CheckActivationSockets();
                SendLogMessage("CoinExFutures WebSocket Private connection open", LogMessageType.System);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        private void ConnectionCheckThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(25000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        if (webSocketPublic != null
                            && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.SendAsync($"{{\"method\": \"server.ping\",\"params\": {{}},\"id\": 11}}");
                        }
                        else
                        {
                            Disconnect();
                        }
                    }

                    if (_webSocketPrivate != null &&
                        (_webSocketPrivate.ReadyState == WebSocketState.Open ||
                        _webSocketPrivate.ReadyState == WebSocketState.Connecting)
                        )
                    {
                        _webSocketPrivate.SendAsync($"{{\"method\": \"server.ping\",\"params\": {{}},\"id\": 22}}");
                    }
                    else
                    {
                        Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        #endregion

        #region 9 Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<Security> _subscribedSecurities = new List<Security>();

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].NameClass == security.NameClass
                    && _subscribedSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }

                _subscribedSecurities.Add(security);

                if (_webSocketPublic.Count == 0)
                {
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 40 == 0)
                {
                    // creating a new socket
                    WebSocket newSocket = CreateNewPublicSocket();

                    DateTime timeEnd = DateTime.Now.AddSeconds(10);

                    while (newSocket.ReadyState != WebSocketState.Open)
                    {
                        Thread.Sleep(1000);

                        if (timeEnd < DateTime.Now)
                        {
                            break;
                        }
                    }

                    if (newSocket.ReadyState == WebSocketState.Open)
                    {
                        _webSocketPublic.Add(newSocket);
                        webSocketPublic = newSocket;
                    }
                }

                if (webSocketPublic != null)
                {
                    webSocketPublic.SendAsync($"{{\"method\":\"deals.subscribe\",\"params\":{{\"market_list\":[\"{security.Name}\"]}},\"id\":2}}");
                    webSocketPublic.SendAsync($"{{\"method\":\"depth.subscribe\",\"params\":{{\"market_list\":[[\"{security.Name}\",{_marketDepth},\"0\",true]]}},\"id\":3}}");

                    if (_extendedMarketData)
                    {
                        webSocketPublic.SendAsync($"{{\"method\":\"state.subscribe\",\"params\":{{\"market_list\":[\"{security.Name}\"]}},\"id\":9}}");
                        GetFundingData(security.Name);
                        GetFundingHistory(security.Name);
                    }
                }
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private void SubscribePrivate()
        {
            try
            {
                _webSocketPrivate.SendAsync($"{{\"method\":\"balance.subscribe\",\"params\":{{\"ccy_list\":[]}},\"id\":4}}");
                _webSocketPrivate.SendAsync($"{{\"method\":\"order.subscribe\",\"params\":{{\"market_list\":[]}},\"id\":5}}");
                //_webSocketPrivate.SendAsync($"{{\"method\":\"position.subscribe\",\"params\":{{\"market_list\":[]}},\"id\":6}}");
                _webSocketPrivate.SendAsync($"{{\"method\":\"user_deals.subscribe\",\"params\":{{\"market_list\":[]}},\"id\":7}}");
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        private void GetFundingData(string security)
        {
            try
            {
                string path = $"/futures/funding-rate";
                string requestStr = $"{path}?market={security}";

                IRestResponse response = CreatePrivateQuery(requestStr, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<FundingRateRestResponse>> fundingResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<FundingRateRestResponse>>());

                    if (fundingResponse.code == "0")
                    {
                        for (int i = 0; i < fundingResponse.data.Count; i++)
                        {
                            Funding funding = new Funding();

                            funding.MinFundingRate = fundingResponse.data[i].min_funding_rate.ToDecimal();
                            funding.MaxFundingRate = fundingResponse.data[i].max_funding_rate.ToDecimal();

                            FundingUpdateEvent?.Invoke(funding);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Funding error: {fundingResponse.code} || msg: {fundingResponse.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Funding error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void GetFundingHistory(string security)
        {
            try
            {
                string path = $"/futures/funding-rate-history";
                string requestStr = $"{path}?market={security}&limit=5";

                IRestResponse response = CreatePrivateQuery(requestStr, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<FundingDataHistory>> fundingResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<FundingDataHistory>>());

                    if (fundingResponse.code == "0")
                    {
                        Funding funding = new Funding();

                        funding.PreviousValue = fundingResponse.data[0].actual_funding_rate.ToDecimal();
                        funding.PreviousFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)fundingResponse.data[0].funding_time.ToDecimal());

                        FundingUpdateEvent?.Invoke(funding);
                    }
                    else
                    {
                        SendLogMessage($"FundingHistory error: {fundingResponse.code} || msg: {fundingResponse.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"FundingHistory error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            try
            {
                if (_webSocketPublic.Count != 0
                    && _webSocketPublic != null)
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        try
                        {
                            if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open)
                            {
                                if (_subscribedSecurities != null)
                                {
                                    for (int j = 0; j < _subscribedSecurities.Count; j++)
                                    {
                                        string securityName = _subscribedSecurities[j].Name;

                                        webSocketPublic.SendAsync($"{{\"method\":\"deals.unsubscribe\",\"params\":{{\"market_list\":[\"{securityName}\"]}},\"id\":4}}");
                                        webSocketPublic.SendAsync($"{{\"method\":\"depth.unsubscribe\",\"params\":{{\"market_list\":[[\"{securityName}\",{_marketDepth},\"0\",true]]}},\"id\":5}}");

                                        if (_extendedMarketData)
                                        {
                                            webSocketPublic.SendAsync($"{{\"method\":\"state.unsubscribe\",\"params\":{{\"market_list\":[\"{securityName}\"]}},\"id\":9}}");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            if (_webSocketPrivate != null
                && _webSocketPrivate.ReadyState == WebSocketState.Open)
            {
                try
                {
                    _webSocketPrivate.SendAsync($"{{\"method\":\"balance.unsubscribe\",\"params\":{{\"ccy_list\":[]}},\"id\":4}}");
                    _webSocketPrivate.SendAsync($"{{\"method\":\"order.unsubscribe\",\"params\":{{\"market_list\":[]}},\"id\":5}}");
                    //_webSocketPrivate.SendAsync($"{{\"method\":\"position.unsubscribe\",\"params\":{{\"market_list\":[]}},\"id\":6}}");
                    _webSocketPrivate.SendAsync($"{{\"method\":\"user_deals.unsubscribe\",\"params\":{{\"market_list\":[]}},\"id\":7}}");
                }
                catch
                {
                    // ignore
                }
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

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

                    if (FIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    FIFOListWebSocketPublicMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    ResponseWebSocketMessage<Object> baseMessage = JsonConvert.DeserializeObject<ResponseWebSocketMessage<Object>>(message);

                    if (baseMessage.code != "0"
                        && baseMessage.code != null)
                    {
                        SendLogMessage($"WebSocketPublic error: {baseMessage.code} || {baseMessage.message}", LogMessageType.Error);
                    }

                    if (baseMessage.method == null)
                    {
                        continue;
                    }

                    if (baseMessage.method == "depth.update")
                    {
                        UpdateMarketDepth(baseMessage.data.ToString());
                    }
                    else if (baseMessage.method == "deals.update")
                    {
                        UpdateTrade(baseMessage.data.ToString());
                    }
                    else if (baseMessage.method == "state.update")
                    {
                        UpdateState(baseMessage.data.ToString());
                    }
                    else
                    {
                        SendLogMessage("Unknown message method: " + baseMessage.message, LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(2000);
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

                    if (FIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    FIFOListWebSocketPrivateMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    ResponseWebSocketMessage<Object> baseMessage = JsonConvert.DeserializeObject<ResponseWebSocketMessage<Object>>(message);

                    if (baseMessage.code != "0"
                        && baseMessage.code != null)
                    {
                        SendLogMessage($"WebSocketPrivate error: {baseMessage.code} || {baseMessage.message}", LogMessageType.Error);
                    }

                    if (baseMessage.method == null)
                    {
                        continue;
                    }

                    if (baseMessage.method == "balance.update")
                    {
                        UpdateMyPortfolio(baseMessage.data.ToString());
                    }
                    else if (baseMessage.method == "order.update")
                    {
                        UpdateMyOrder(baseMessage.data.ToString());
                    }
                    else if (baseMessage.method == "user_deals.update")
                    {
                        UpdateMyTrade(baseMessage.data.ToString());
                    }
                    else
                    {
                        SendLogMessage("Unknown message method: " + baseMessage.message, LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(2000);
                }
            }
        }

        private void UpdateTrade(string data)
        {
            try
            {
                ResponseTrade responseTrade = JsonConvert.DeserializeObject<ResponseTrade>(data);

                if (responseTrade.deal_list == null
                    || responseTrade.deal_list.Count == 0)
                {
                    return;
                }

                if (string.IsNullOrEmpty(responseTrade.market))
                {
                    return;
                }

                for (int i = 0; i < responseTrade.deal_list.Count; i++)
                {
                    DealData cexTrade = responseTrade.deal_list[i];

                    Trade trade = new Trade();
                    trade.Price = cexTrade.price.ToString().ToDecimal();
                    trade.Time = TimeManager.GetDateTimeFromTimeStamp((long)cexTrade.created_at.ToDecimal());
                    trade.Id = cexTrade.deal_id;
                    trade.Side = cexTrade.side == "buy" ? Side.Buy : trade.Side = Side.Sell;
                    trade.Volume = cexTrade.amount.ToDecimal();
                    trade.SecurityNameCode = responseTrade.market;

                    if (trade.Price == 0 || trade.Volume == 0 || string.IsNullOrEmpty(trade.Id))
                    {
                        continue;
                    }

                    if (_extendedMarketData)
                    {
                        trade.OpenInterest = GetOpenInterestValue(trade.SecurityNameCode);
                    }

                    if (NewTradesEvent != null)
                    {
                        NewTradesEvent(trade);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private List<OpenInterestData> _openInterest = new List<OpenInterestData>();

        private decimal GetOpenInterestValue(string securityNameCode)
        {
            if (_openInterest.Count == 0
                 || _openInterest == null)
            {
                return 0;
            }

            for (int i = 0; i < _openInterest.Count; i++)
            {
                if (_openInterest[i].SecurityName == securityNameCode)
                {
                    return _openInterest[i].OpenInterestValue.ToDecimal();
                }
            }

            return 0;
        }

        private void UpdateMarketDepth(string data)
        {
            try
            {
                ResponseDepth responseDepth = JsonConvert.DeserializeObject<ResponseDepth>(data);

                if (responseDepth.depth.asks.Count == 0 && responseDepth.depth.bids.Count == 0)
                {
                    return;
                }

                DepthData cexDepth = responseDepth.depth;

                MarketDepth depth = new MarketDepth();
                depth.Time = TimeManager.GetDateTimeFromTimeStamp((long)cexDepth.updated_at.ToDecimal());

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

                depth.SecurityNameCode = responseDepth.market;

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
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        private void UpdateState(string data)
        {
            try
            {
                ResponseWSState responseState = JsonConvert.DeserializeObject<ResponseWSState>(data);

                if (responseState.state_list == null
                    || responseState.state_list.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < responseState.state_list.Count; i++)
                {
                    StateWSData item = responseState.state_list[i];

                    OpenInterestData openInterest = new OpenInterestData();

                    openInterest.SecurityName = item.market;

                    if (item.open_interest_size != null)
                    {
                        openInterest.OpenInterestValue = item.open_interest_size;

                        bool isInArray = false;

                        for (int j = 0; j < _openInterest.Count; j++)
                        {
                            if (_openInterest[j].SecurityName == openInterest.SecurityName)
                            {
                                _openInterest[j].OpenInterestValue = openInterest.OpenInterestValue;
                                isInArray = true;
                                break;
                            }
                        }

                        if (isInArray == false)
                        {
                            _openInterest.Add(openInterest);
                        }
                    }

                    Funding funding = new Funding();

                    funding.SecurityNameCode = item.market;
                    funding.CurrentValue = item.latest_funding_rate.ToDecimal() * 100;
                    funding.NextFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)item.latest_funding_time.ToDecimal());
                    funding.TimeUpdate = DateTime.UtcNow;
                    TimeSpan timePeriod = TimeManager.GetDateTimeFromTimeStamp((long)item.next_funding_time.ToDecimal()) - TimeManager.GetDateTimeFromTimeStamp((long)item.latest_funding_time.ToDecimal());
                    funding.FundingIntervalHours = int.Parse(timePeriod.Hours.ToString());

                    FundingUpdateEvent?.Invoke(funding);

                    SecurityVolumes volume = new SecurityVolumes();

                    volume.SecurityNameCode = item.market;
                    volume.Volume24h = item.volume.ToDecimal();
                    volume.Volume24hUSDT = item.value.ToDecimal();

                    Volume24hUpdateEvent?.Invoke(volume);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMyOrder(string data)
        {
            try
            {
                ResponseWSOrder responseOrder = JsonConvert.DeserializeObject<ResponseWSOrder>(data);

                if (responseOrder.order.order_id == "0")
                {
                    return;
                }

                OrderWSData cexOrder = responseOrder.order;

                Order order = new Order();
                order.State = OrderStateType.Active;

                try
                {
                    order.NumberUser = Convert.ToInt32(cexOrder.client_id);
                }
                catch
                {

                }

                order.SecurityNameCode = cexOrder.market;
                // Cex.Amount - объём в единицах тикера
                // Cex.Value - объём в деньгах
                order.Volume = cexOrder.amount.ToString().ToDecimal();
                order.VolumeExecute = cexOrder.filled_amount.ToString().ToDecimal(); // FIX Разобраться с названием параметра!

                if (cexOrder.type == "limit")
                {
                    order.Price = cexOrder.price.ToString().ToDecimal();
                    order.TypeOrder = OrderPriceType.Limit;
                }
                else if (cexOrder.type == "market")
                {
                    order.TypeOrder = OrderPriceType.Market;
                }

                order.ServerType = ServerType.CoinExFutures;
                order.NumberMarket = cexOrder.order_id.ToString();
                order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp((long)cexOrder.updated_at.ToDecimal());
                order.TimeCreate = TimeManager.GetDateTimeFromTimeStamp((long)cexOrder.created_at.ToDecimal());
                order.Side = cexOrder.side == "buy" ? Side.Buy : Side.Sell;

                order.PortfolioNumber = getPortfolioName();
                decimal cexAmount = cexOrder.amount.ToString().ToDecimal();
                decimal cexFilledAmount = cexOrder.filled_amount.ToString().ToDecimal();
                decimal cexFilledValue = cexOrder.filled_value.ToString().ToDecimal();
                if (responseOrder.@event == "put")
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
                else if (responseOrder.@event == "update")
                {
                    // Order updated (partially filled)
                    order.State = OrderStateType.Partial;
                }
                else if (responseOrder.@event == "finish")
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
                else if (responseOrder.@event == "modify")
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

                if (order == null || order.NumberUser == 0)
                {
                    return;
                }

                MyOrderEvent?.Invoke(order);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(string data)
        {
            try
            {
                ResponseUserDeal responseMyTrade = JsonConvert.DeserializeObject<ResponseUserDeal>(data);
                MyTrade newTrade = new MyTrade();

                newTrade.Time = TimeManager.GetDateTimeFromTimeStamp((long)responseMyTrade.created_at.ToDecimal());
                newTrade.SecurityNameCode = responseMyTrade.market;
                newTrade.NumberOrderParent = responseMyTrade.order_id;
                newTrade.Price = responseMyTrade.price.ToDecimal();
                newTrade.NumberTrade = responseMyTrade.deal_id;
                newTrade.Side = responseMyTrade.side == "buy" ? Side.Buy : Side.Sell;
                newTrade.Volume = responseMyTrade.amount.ToDecimal();
                MyTradeEvent(newTrade);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMyPortfolio(string data)
        {
            try
            {
                ResponseWSBalance responswBalance = JsonConvert.DeserializeObject<ResponseWSBalance>(data);

                if (responswBalance.balance_list.Count == 0)
                {
                    return;
                }

                if (_portfolios == null)
                {
                    return;
                }

                string portfolioName = getPortfolioName();
                Portfolio myPortfolio = _portfolios.Find(p => p.Number == portfolioName);

                if (myPortfolio == null)
                {
                    return;
                }

                for (int i = 0; i < responswBalance.balance_list.Count; i++)
                {
                    BalanceWSData cexPosition = responswBalance.balance_list[i];

                    PositionOnBoard pos = new PositionOnBoard();

                    pos.SecurityNameCode = cexPosition.ccy;
                    pos.ValueCurrent = Math.Round(cexPosition.available.ToDecimal(), 2);
                    pos.UnrealizedPnl = Math.Round(cexPosition.unrealized_pnl.ToDecimal(), 2);
                    pos.ValueBlocked = Math.Round(cexPosition.margin.ToDecimal(), 2);

                    myPortfolio.SetNewPosition(pos);
                }

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }


        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent;

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(30, TimeSpan.FromMilliseconds(950));

        private RateGate _rateGateCancelOrder = new RateGate(60, TimeSpan.FromMilliseconds(950));

        private RateGate _rateGateGetOrder = new RateGate(50, TimeSpan.FromMilliseconds(950));

        private RateGate _rateGateOrdersHistory = new RateGate(10, TimeSpan.FromMilliseconds(950));

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("market", order.SecurityNameCode);
                parameters.Add("market_type", "FUTURES");
                parameters.Add("side", order.Side == Side.Buy ? "buy" : "sell");
                parameters.Add("amount", order.Volume.ToString(CultureInfo.InvariantCulture).Replace(",", "."));
                parameters.Add("client_id", order.NumberUser.ToString());

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    parameters.Add("type", "limit");
                    parameters.Add("price", order.Price.ToString(CultureInfo.InvariantCulture).Replace(",", "."));
                }
                else if (order.TypeOrder == OrderPriceType.Market)
                {
                    parameters.Add("type", "market");
                }

                string jsonRequest = JsonConvert.SerializeObject(parameters);

                IRestResponse response = CreatePrivateQuery("/futures/order", Method.POST, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<OrderRestResponse> orderResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<OrderRestResponse>());

                    if (orderResponse.code == "0")
                    {
                        //order.State = OrderStateType.Active;
                        //order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.updated_at);
                        //order.TimeCreate = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.created_at);
                        //order.NumberMarket = cexOrder.order_id.ToString();
                        //MyOrderEvent?.Invoke(order);
                        //SendLogMessage("Order executed", LogMessageType.Trade);
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Send Order error. {orderResponse.code} || msg: {orderResponse.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Send Order error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;
            MyOrderEvent?.Invoke(order);
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("market", order.SecurityNameCode);
                parameters.Add("order_id", (long)order.NumberMarket.ToDecimal());
                parameters.Add("market_type", "FUTURES");
                parameters.Add("price", newPrice.ToString(CultureInfo.InvariantCulture).Replace(",", "."));

                string jsonRequest = JsonConvert.SerializeObject(parameters);

                IRestResponse response = CreatePrivateQuery("/futures/modify-order", Method.POST, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<OrderRestResponse> orderResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<OrderRestResponse>());

                    if (orderResponse.code == "0")
                    {
                        //order.Price = newPrice;
                        //order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.updated_at);
                        //MyOrderEvent?.Invoke(order);
                        //SendLogMessage("Order price changed", LogMessageType.Trade);
                    }
                    else
                    {
                        SendLogMessage($"Order change price send error: {orderResponse.code} || msg: {orderResponse.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Order change price send error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
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

            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("market", order.SecurityNameCode);
                parameters.Add("market_type", "FUTURES");
                parameters.Add("order_id", (long)order.NumberMarket.ToDecimal());

                string jsonRequest = JsonConvert.SerializeObject(parameters);

                IRestResponse response = CreatePrivateQuery("/futures/cancel-order", Method.POST, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<OrderRestResponse> orderResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<OrderRestResponse>());

                    if (orderResponse.code == "0")
                    {
                        //order.State = OrderStateType.Cancel;
                        //order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(cexOrder.updated_at);
                        //order.TimeCancel = order.TimeCallBack;
                        //MyOrderEvent?.Invoke(order);
                        //SendLogMessage("Order cancelled", LogMessageType.Trade);
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Cancel order error error: {orderResponse.code} || msg: {orderResponse.message}", LogMessageType.Error);
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    OrderStateType state = GetOrderStatus(order);

                    if (state == OrderStateType.None)
                    {
                        SendLogMessage($"Cancel order error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
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

            return false;
        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("market_type", "FUTURES");
                parameters.Add("market", security.Name);

                string jsonRequest = JsonConvert.SerializeObject(parameters);

                IRestResponse response = CreatePrivateQuery("/futures/cancel-all-order", Method.POST, jsonRequest);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"Cancel all orders to security error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Cancel all orders request error. " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> openOrders = GetAllOpenOrders();

            if (openOrders == null)
            {
                return;
            }

            for (int i = 0; i < openOrders.Count; i++)
            {
                MyOrderEvent?.Invoke(openOrders[i]);
            }
        }

        private List<Order> GetAllOpenOrders()
        {
            try
            {
                _rateGateGetOrder.WaitToProceed();

                string path = $"/futures/pending-order";
                string requestStr = $"{path}?market_type=FUTURES&limit=1000";

                IRestResponse response = CreatePrivateQuery(requestStr, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<OrderRestResponse>> orderResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<OrderRestResponse>>());

                    if (orderResponse.code == "0")
                    {
                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < orderResponse.data.Count; i++)
                        {
                            OrderRestResponse item = orderResponse.data[i];

                            Order order = GetOrderOsEngineFromOrder(item);
                            orders.Add(order);
                        }

                        return orders;
                    }
                    else
                    {
                        SendLogMessage($"Get All Open Orders error: {orderResponse.code} || msg: {orderResponse.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get All Open Orders error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all opened orders request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            List<Order> orderFromExchange = GetAllOpenOrders();

            if (orderFromExchange == null
                || orderFromExchange.Count == 0)
            {
                orderFromExchange = GetOrderFromExchange(order.SecurityNameCode, order.NumberUser.ToString());
            }

            if (orderFromExchange == null
               || orderFromExchange.Count == 0)
            {
                return OrderStateType.None;
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
                return OrderStateType.None;
            }

            if (orderOnMarket != null &&
                MyOrderEvent != null)
            {
                MyOrderEvent(orderOnMarket);
            }

            if (orderOnMarket.State == OrderStateType.Done
                || orderOnMarket.State == OrderStateType.Partial)
            {
                GetTradesForOrder(orderOnMarket.SecurityNameCode, orderOnMarket.NumberMarket);
            }
            return orderOnMarket.State;
        }


        private List<Order> GetOrderFromExchange(string securityName, string numberUser)
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string path = $"/futures/finished-order";
                string requestStr = $"{path}?market_type=FUTURES&limit=1000";

                IRestResponse response = CreatePrivateQuery(requestStr, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<OrderRestResponse>> orderResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<OrderRestResponse>>());

                    if (orderResponse.code == "0")
                    {
                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < orderResponse.data.Count; i++)
                        {
                            OrderRestResponse item = orderResponse.data[i];

                            Order order = GetOrderOsEngineFromOrder(item);
                            orders.Add(order);
                        }

                        return orders;
                    }
                    else
                    {
                        if (orderResponse.message.Contains("order not exists"))
                        {
                            //
                        }
                        else
                        {
                            SendLogMessage($"Get order request error: {orderResponse.code} || msg: {orderResponse.message}", LogMessageType.Error);
                        }
                    }
                }
                else
                {
                    SendLogMessage($"Get order request error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get order request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private Order GetOrderOsEngineFromOrder(OrderRestResponse cexOrder)
        {
            Order order = new Order();

            try
            {
                order.NumberUser = Convert.ToInt32(cexOrder.client_id);
            }
            catch
            {
                // ignore
            }

            order.SecurityNameCode = cexOrder.market;
            order.SecurityClassCode = cexOrder.market.Substring(cexOrder.market.Length - 4);
            order.Volume = cexOrder.amount.ToString().ToDecimal(); // FIX Разобраться с названием параметра!
            order.VolumeExecute = cexOrder.filled_amount.ToString().ToDecimal(); // FIX Разобраться с названием параметра!
            order.Price = cexOrder.price.ToString().ToDecimal();

            order.TypeOrder = cexOrder.type == "limit" ? OrderPriceType.Limit : OrderPriceType.Market;

            order.ServerType = ServerType.CoinExFutures;

            order.NumberMarket = cexOrder.order_id.ToString();

            order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp((long)cexOrder.updated_at.ToDecimal());
            order.TimeCreate = TimeManager.GetDateTimeFromTimeStamp((long)cexOrder.created_at.ToDecimal());
            order.PortfolioNumber = getPortfolioName();
            order.Side = cexOrder.side == "buy" ? Side.Buy : Side.Sell;

            // Order placed successfully (unfilled/partially filled)
            order.State = OrderStateType.None;
            if (!string.IsNullOrEmpty(cexOrder.status))
            {
                if (cexOrder.status == "open")
                {
                    order.State = OrderStateType.Active;
                }
                else if (cexOrder.status == "part_filled")
                {
                    order.State = OrderStateType.Partial;
                }
                else if (cexOrder.status == "filled")
                {
                    order.State = OrderStateType.Done;
                    order.TimeDone = order.TimeCallBack;
                }
                else if (cexOrder.status == "part_canceled")
                {
                    order.State = OrderStateType.Cancel;
                    order.TimeCancel = order.TimeCallBack;
                }
                else if (cexOrder.status == "canceled")
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
                if (cexOrder.unfilled_amount.ToDecimal() > 0)
                {
                    order.State = cexOrder.amount == cexOrder.unfilled_amount ? OrderStateType.Active : OrderStateType.Partial;
                }
                else if (cexOrder.filled_amount.ToDecimal() > 0)
                {
                    order.State = cexOrder.amount == cexOrder.filled_amount ? OrderStateType.Done : OrderStateType.Partial;
                }
            }

            return order;
        }

        private List<MyTrade> GetTradesForOrder(string securityName, string numberMarket)
        {
            _rateGateOrdersHistory.WaitToProceed();

            try
            {
                string path = $"/futures/order-deals";
                string requestStr = $"{path}?market={securityName}&order_id={(long)numberMarket.ToDecimal()}&market_type=FUTURES&limit=100";

                IRestResponse response = CreatePrivateQuery(requestStr, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<MyTradeRestResponse>> orderResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<MyTradeRestResponse>>());

                    if (orderResponse.code == "0")
                    {
                        for (int i = 0; i < orderResponse.data.Count; i++)
                        {
                            MyTradeRestResponse cexTrade = orderResponse.data[i];

                            MyTrade myTrade = new MyTrade();
                            myTrade.NumberOrderParent = cexTrade.order_id.ToString();
                            myTrade.NumberTrade = cexTrade.deal_id.ToString();
                            myTrade.SecurityNameCode = cexTrade.market;
                            myTrade.Time = TimeManager.GetDateTimeFromTimeStamp((long)cexTrade.created_at.ToDecimal());
                            myTrade.Side = cexTrade.side == "buy" ? Side.Buy : Side.Sell;
                            myTrade.Price = cexTrade.price.ToString().ToDecimal();
                            myTrade.Volume = cexTrade.amount.ToString().ToDecimal();

                            MyTradeEvent(myTrade);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Order trade error: {orderResponse.code} || msg: {orderResponse.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Order trade error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
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

        public string Sign(string message)
        {
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey)))
            {
                byte[] r = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(r).Replace("-", "").ToLower();
            }
        }

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

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public class OpenInterestData
    {
        public string SecurityName { get; set; }
        public string OpenInterestValue { get; set; }
    }
}