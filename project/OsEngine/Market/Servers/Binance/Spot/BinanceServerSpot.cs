using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity;
using OsEngine.Market.Servers.Entity;
using RestSharp;

namespace OsEngine.Market.Servers.Binance.Spot
{
    public class BinanceServer : AServer
    {
        public BinanceServer()
        {
            BinanceServerRealization realization = new BinanceServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }

        /// <summary>
        /// instrument history query
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((BinanceServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }

    public class BinanceServerRealization : IServerRealization
    {
        public BinanceServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.Binance; }
        }

        /// <summary>
        /// server parameters
        /// параметры сервера
        /// </summary>
        public List<IServerParameter> ServerParameters { get; set; }

        /// <summary>
        /// server time
        /// время сервера
        /// </summary>
        public DateTime ServerTime { get; set; }

        // requests
        // запросы

        /// <summary>
        /// binance client
        /// </summary>
        private BinanceClient _client;

        /// <summary>
        /// release API
        /// освободить апи
        /// </summary>
        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();

                _client.Connected -= _client_Connected;
                _client.UpdatePairs -= _client_UpdatePairs;
                _client.Disconnected -= _client_Disconnected;
                _client.NewPortfolioSpot -= _client_NewPortfolioSpot;
                _client.NewPortfolioMargin -= _client_NewPortfolioMargin;
                _client.UpdatePortfolio -= _client_UpdatePortfolioSpot;
                _client.UpdateMarketDepth -= _client_UpdateMarketDepth;
                _client.NewTradesEvent -= _client_NewTradesEvent;
                _client.MyTradeEvent -= _client_MyTradeEvent;
                _client.MyOrderEvent -= _client_MyOrderEvent;
                _client.LogMessageEvent -= SendLogMessage;
            }

            _client = null;
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// connect to API
        /// подсоединиться к апи
        /// </summary>
        public void Connect()
        {
            if (_client == null)
            {
                _client = new BinanceClient(
                    ((ServerParameterString)ServerParameters[0]).Value,
                    ((ServerParameterPassword)ServerParameters[1]).Value);
                _client.Connected += _client_Connected;
                _client.UpdatePairs += _client_UpdatePairs;
                _client.Disconnected += _client_Disconnected;
                _client.NewPortfolioSpot += _client_NewPortfolioSpot;
                _client.NewPortfolioMargin += _client_NewPortfolioMargin;

                _client.UpdatePortfolio += _client_UpdatePortfolioSpot;
                _client.UpdateMarketDepth += _client_UpdateMarketDepth;
                _client.NewTradesEvent += _client_NewTradesEvent;
                _client.MyTradeEvent += _client_MyTradeEvent;
                _client.MyOrderEvent += _client_MyOrderEvent;
                _client.LogMessageEvent += SendLogMessage;
            }

            _client.Connect();
        }

        /// <summary>
        /// request securities
        /// запросить бумаги
        /// </summary>
        public void GetSecurities()
        {
            _client.GetSecurities();
        }

        /// <summary>
        /// request portfolios
        /// запросить портфели
        /// </summary>
        public void GetPortfolios()
        {
            _client.GetBalanceSpot();
            _client.GetBalanceMargin();
        }

        /// <summary>
        /// send order
        /// исполнить ордер
        /// </summary>
        public void SendOrder(Order order)
        {
            _client.ExecuteOrder(order);
        }

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        /// <summary>
        /// cancel order
        /// отозвать ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            _client.CancelOrder(order);
        }

        /// <summary>
        /// cancel all orders from trading system
        /// отозвать все ордера из торговой системы
        /// </summary>
        public void CancelAllOrders()
        {
            _client.CancelAllOrders();
        }

        /// <summary>
        /// subscribe
        /// подписаться 
        /// </summary>
        public void Subscrible(Security security)
        {
            _client.SubscribleTradesAndDepths(security);
        }

        /// <summary>
        /// take candle history for period
        /// взять историю свечек за период
        /// </summary>
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            List<Candle> candles = new List<Candle>();

            if(actualTime > endTime)
            {
                return null;
            }

            actualTime = startTime;

            while (actualTime < endTime)
            {
                List<Candle> newCandles = _client.GetCandlesForTimes(security.Name,
                    timeFrameBuilder.TimeFrameTimeSpan,
                    actualTime, endTime);

                if (newCandles != null && candles.Count != 0 && newCandles.Count != 0)
                {
                    for (int i = 0; i < newCandles.Count; i++)
                    {
                        if (candles[candles.Count - 1].TimeStart >= newCandles[i].TimeStart)
                        {
                            newCandles.RemoveAt(i);
                            i--;
                        }

                    }
                }

                if (newCandles == null)
                {
                    actualTime = actualTime.AddDays(5);
                    continue;
                }

                if (newCandles.Count == 0)
                {
                    break;
                }

                candles.AddRange(newCandles);

                actualTime = candles[candles.Count - 1].TimeStart;

                Thread.Sleep(60);
            }

            if (candles.Count == 0)
            {
                return null;
            }

            for(int i = candles.Count-1;i >= 0;i--)
            {
                if (candles[i].TimeStart <= endTime)
                {
                    break;
                }
                if (candles[i].TimeStart > endTime)
                {
                    candles.RemoveAt(i);
                }
            }

            for (int i = 1; i < candles.Count; i++)
            {
                Candle candleNow = candles[i];
                Candle candleLast = candles[i - 1];

                if (candleLast.TimeStart == candleNow.TimeStart)
                {
                    candles.RemoveAt(i);
                    i --;
                    continue;
                }
            }

            for (int i = 0; i < candles.Count; i++)
            {
                Candle candleNow = candles[i];

                if (candleNow.Volume == 0)
                {
                    candles.RemoveAt(i);
                    i --;
                    continue;
                }
            }

            return candles;
        }

        /// <summary>
        /// take ticks data on instrument for period
        /// взять тиковые данные по инструменту за период
        /// </summary>
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime lastDate)
        {
            if (lastDate > endTime)
            {
                return null;
            }

            string markerDateTime = "";

            List<Trade> trades = new List<Trade>();

            DateTime startOver = startTime;

            long lastId = 0;

            while (true)
            {
                if (startOver >= endTime)
                {
                    break;
                }

                List<Trade> newTrades = new List<Trade>();

                if (lastId == 0)
                {
                    List<Trade> firstTrades = new List<Trade>();

                    int countMinutesAddToFindFirstTrade = 0;
                    int countDaysAddToFindFirstTrade = 0;

                    do
                    {
                        firstTrades = _client.GetTickHistoryToSecurity(security.Name, startOver, startOver.AddSeconds(60), 0);
                        startOver = startOver.AddSeconds(60);

                        if((firstTrades == null || firstTrades.Count == 0) &&
                            countMinutesAddToFindFirstTrade < 10)
                        {
                            countMinutesAddToFindFirstTrade++;
                            startOver = startOver.AddMinutes(60);
                        }
                        else if ((firstTrades == null || firstTrades.Count == 0) &&
                            countDaysAddToFindFirstTrade < 10)
                        {
                            countDaysAddToFindFirstTrade++;
                            startOver = startOver.AddDays(1);
                        }
                        else if(firstTrades == null || firstTrades.Count == 0)
                        {
                            startOver = startOver.AddDays(30);
                        }

                        if (startOver >= endTime)
                        {
                            return null;
                        }
                    }
                    while (firstTrades == null || firstTrades.Count == 0);


                    Trade firstTrade = firstTrades.First();

                    lastId = Convert.ToInt64(firstTrade.Id);

                    newTrades.Add(firstTrade);
                }
                else
                {
                    newTrades = _client.GetTickHistoryToSecurity(security.Name, new DateTime(), new DateTime(), lastId + 1);

                    try
                    {
                        lastId = Convert.ToInt64(newTrades[newTrades.Count - 1].Id);
                    }
                    catch { } // Если дата по которую скачиваем свечки превышает сегодняшнюю: Ignore 
                }

                if (newTrades != null && newTrades.Count != 0)
                    trades.AddRange(newTrades);
                else
                    break;

                startOver = trades[trades.Count - 1].Time.AddMilliseconds(1);


                if (markerDateTime != startOver.ToShortDateString())
                {
                    if (startOver >= endTime)
                    {
                        break;
                    }
                    markerDateTime = startOver.ToShortDateString();
                    SendLogMessage(security.Name + " Binance Spot start loading: " + markerDateTime, LogMessageType.System);
                }
            }

            if (trades.Count == 0)
            {
                return null;
            }

            for (int i = trades.Count - 1; i >= 0; i--)
            {
                if (trades[i].Time <= endTime)
                {
                    break;
                }
                if (trades[i].Time > endTime)
                {
                    trades.RemoveAt(i);
                }
            }

            for (int i = 1; i < trades.Count; i++)
            {
                Trade tradeNow = trades[i];
                Trade tradeLast = trades[i - 1];

                if (tradeLast.Time == tradeNow.Time)
                {
                    trades.RemoveAt(i);
                    i = 0;
                    continue;
                }
            }

            return trades;
        }

        /// <summary>
        /// request order state
        /// запросить статус ордеров
        /// </summary>
        public void GetOrdersState(List<Order> orders)
        {
            _client.GetAllOrders(orders);
        }

        /// <summary>
        /// server status
        /// статус серверов
        /// </summary>
        public ServerConnectStatus ServerStatus { get; set; }

        /// <summary>
        /// request instrument history
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            List<Candle> candles = _client.GetCandles(nameSec, tf);

            if (candles != null && candles.Count != 0)
            {
                for (int i = 0; i < candles.Count; i++)
                {
                    candles[i].State = CandleState.Finished;
                }
                candles[candles.Count - 1].State = CandleState.Started;
            }

            return candles;
        }

        //parsing incoming data
        // разбор входящих данных

        void _client_MyOrderEvent(Order order)
        {
            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        void _client_MyTradeEvent(MyTrade myTrade)
        {
            if (MyTradeEvent != null)
            {
                MyTradeEvent(myTrade);
            }
        }

        /// <summary>
        /// multi-threaded access locker to ticks
        /// блокиратор многопоточного доступа к тикам
        /// </summary>
        private readonly object _newTradesLoker = new object();

        void _client_NewTradesEvent(TradeResponse trades)
        {
            lock (_newTradesLoker)
            {
                if (trades.data == null)
                {
                    return;
                }
                Trade trade = new Trade();
                trade.SecurityNameCode = trades.data.s;
                trade.Price =
                        trades.data.p.ToDecimal();
                trade.Id = trades.data.t.ToString();
                trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(trades.data.T));
                trade.Volume =
                        trades.data.q.ToDecimal();
                trade.Side = trades.data.m == true ? Side.Sell : Side.Buy;

                NewTradesEvent?.Invoke(trade);
            }
        }

        /// <summary>
        /// all depths
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths;

        private readonly object _depthLocker = new object();

        void _client_UpdateMarketDepth(DepthResponse myDepth)
        {
            try
            {
                lock (_depthLocker)
                {
                    if (_depths == null)
                    {
                        _depths = new List<MarketDepth>();
                    }

                    if (myDepth.data.asks == null || myDepth.data.asks.Count == 0 ||
                        myDepth.data.bids == null || myDepth.data.bids.Count == 0)
                    {
                        return;
                    }

                    string secName = myDepth.stream.Split('@')[0].ToUpper();

                    MarketDepth needDepth = null;

                    for(int i = 0;i < _depths.Count;i++)
                    {
                        if(_depths[i].SecurityNameCode == secName)
                        {
                            needDepth = _depths[i];
                            break;
                        }
                    }

                    if (needDepth == null)
                    {
                        needDepth = new MarketDepth();
                        needDepth.SecurityNameCode = secName;
                        _depths.Add(needDepth);
                    }

                    List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                    List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                    for (int i = 0; i < myDepth.data.asks.Count; i++)
                    {
                        ascs.Add(new MarketDepthLevel()
                        {
                            Ask =
                                myDepth.data.asks[i][1].ToString().ToDecimal()
                            ,
                            Price =
                                myDepth.data.asks[i][0].ToString().ToDecimal()

                        });
                    }

                    for (int i = 0; i < myDepth.data.bids.Count; i++)
                    {
                        bids.Add(new MarketDepthLevel()
                        {
                            Bid =
                                myDepth.data.bids[i][1].ToString().ToDecimal()
                            ,
                            Price =
                                myDepth.data.bids[i][0].ToString().ToDecimal()
                        });
                    }

                    needDepth.Asks = ascs;
                    needDepth.Bids = bids;
                    needDepth.Time = ServerTime;
                   
                    if (needDepth.Time == DateTime.MinValue)
                    {
                        return;
                    }

                    if(needDepth.Time == _lastTimeMd)
                    {
                        _lastTimeMd = _lastTimeMd.AddMilliseconds(1);
                        needDepth.Time = _lastTimeMd;
                    }

                    _lastTimeMd = needDepth.Time;

                    if (MarketDepthEvent != null)
                    {
                        MarketDepthEvent(needDepth.GetCopy());
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private DateTime _lastTimeMd = DateTime.MinValue;

        #region Портфели

        private List<Portfolio> _portfolios = new List<Portfolio>();

        void _client_UpdatePortfolioSpot(OutboundAccountInfo portfs, BinanceExchangeType source)
        {
            try
            {
                if (portfs == null)
                {
                    return;
                }

                if (_portfolios == null)
                {
                    return;
                }

                Portfolio portfolio = null;

                if (source == BinanceExchangeType.SpotExchange)
                {
                    portfolio = _portfolios.Find(p => p.Number == "BinanceSpot");
                }
                else if (source == BinanceExchangeType.MarginExchange)
                {
                    portfolio = _portfolios.Find(p => p.Number == "BinanceMargin");
                }

                if (portfolio == null)
                {
                    return;
                }

                foreach (var onePortf in portfs.B)
                {
                    if (onePortf == null ||
                        onePortf.f == null ||
                        onePortf.l == null)
                    {
                        continue;
                    }

                    PositionOnBoard neeedPortf =
                        portfolio.GetPositionOnBoard().Find(p => p.SecurityNameCode == onePortf.a);

                    if (neeedPortf == null)
                    {
                        PositionOnBoard newPos = new PositionOnBoard();
                        newPos.PortfolioName = portfolio.Number;
                        newPos.SecurityNameCode = onePortf.a;
                        newPos.ValueBegin = onePortf.f.ToDecimal();
                        newPos.ValueCurrent = onePortf.f.ToDecimal();
                        portfolio.SetNewPosition(newPos);

                        continue;
                    }

                    neeedPortf.ValueCurrent =
                        onePortf.f.ToDecimal();
                    neeedPortf.ValueBlocked =
                        onePortf.l.ToDecimal();
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void _client_NewPortfolioSpot(AccountResponse portfs)
        {
            try
            {
                Portfolio myPortfolio = _portfolios.Find(p => p.Number == "BinanceSpot");

                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = "BinanceSpot";
                    newPortf.ValueBegin = 1;
                    newPortf.ValueCurrent = 1;
                    _portfolios.Add(newPortf);
                    myPortfolio = newPortf;
                }

                if (portfs.balances == null)
                {
                    return;
                }

                foreach (var onePortf in portfs.balances)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = onePortf.asset;

                    newPortf.ValueBegin =
                        onePortf.free.ToDecimal();
                    newPortf.ValueCurrent =
                        onePortf.free.ToDecimal();
                    newPortf.ValueBlocked =
                        onePortf.locked.ToDecimal();

                    myPortfolio.SetNewPosition(newPortf);
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _client_NewPortfolioMargin(AccountResponseMargin portfs)
        {
            try
            {
                if (portfs == null)
                {
                    return;
                }

                if (portfs.userAssets == null)
                {
                    return;
                }

                Portfolio myPortfolio = _portfolios.Find(p => p.Number == "BinanceMargin");

                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = "BinanceMargin";
                    newPortf.ValueBegin = 1;
                    newPortf.ValueCurrent = 1;

                    _portfolios.Add(newPortf);
                    myPortfolio = newPortf;
                }


                foreach (var onePortf in portfs.userAssets)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = onePortf.asset;
                    newPortf.ValueBegin =
                        onePortf.free.ToDecimal();
                    newPortf.ValueCurrent =
                        onePortf.free.ToDecimal();
                    newPortf.ValueBlocked =
                        onePortf.locked.ToDecimal();

                    myPortfolio.SetNewPosition(newPortf);
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        void _client_Disconnected()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
        }

        private List<Security> _securities;

        void _client_UpdatePairs(SecurityResponce pairs)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            foreach (var sec in pairs.symbols)
            {
                if(sec.status != "TRADING")
                {
                    continue;
                }

                Security security = new Security();
                security.Name = sec.symbol;
                security.NameFull = sec.symbol;
                security.NameClass = sec.quoteAsset;
                security.NameId = sec.symbol + sec.quoteAsset;
                security.SecurityType = SecurityType.CurrencyPair;
                security.Exchange = ServerType.Binance.ToString();
                // sec.filters[1] - минимальный объем равный цена * объем

                security.PriceStep = sec.filters[0].tickSize.ToDecimal();
                security.PriceStepCost = security.PriceStep;

                security.PriceLimitLow = sec.filters[0].minPrice.ToDecimal();
                security.PriceLimitHigh = sec.filters[0].maxPrice.ToDecimal();
                
                if (security.PriceStep < 1)
                {
                    string prStep = security.PriceStep.ToString(CultureInfo.InvariantCulture);
                    security.Decimals = Convert.ToString(prStep).Split('.')[1].Split('1')[0].Length + 1;
                }
                else
                {
                    security.Decimals = 0;
                }

                if (sec.filters.Count > 1 &&
                   sec.filters[1] != null &&
                   sec.filters[1].minQty != null)
                {
                    decimal minQty = sec.filters[1].minQty.ToDecimal();
                   
                    security.Lot = minQty;
                    string qtyInStr = minQty.ToStringWithNoEndZero().Replace(",", ".");

                    if(qtyInStr.Split('.').Length > 1)
                    {
                        security.DecimalsVolume = qtyInStr.Split('.')[1].Length;
                    }
                }

                security.State = SecurityStateType.Activ;
                _securities.Add(security);
            }

            if (SecurityEvent != null)
            {
                SecurityEvent(_securities);
            }
        }

        void _client_Connected()
        {
            ServerStatus = ServerConnectStatus.Connect;

            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
            
        }

        // outgoing messages
        // исходящие события

        /// <summary>
        /// called when order changed
        /// вызывается когда изменился ордер
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// called when my trade changed
        /// вызывается когда изменился мой трейд
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// new portfolios appeared
        /// появились новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
        /// new securities
        /// новые бумаги
        /// </summary>
        public event Action<List<Security>> SecurityEvent;

        /// <summary>
        /// new depth
        /// новый стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
        /// new trade
        /// новый трейд
        /// </summary>
        public event Action<Trade> NewTradesEvent;

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action ConnectEvent;

        /// <summary>
        /// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action DisconnectEvent;

        // log messages
        // сообщения для лога

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();

                param.Add("symbol=", security.Name.ToUpper());

                _client.CreateQuery(BinanceExchangeType.SpotExchange, Method.DELETE, "api/v3/openOrders", param, true);
            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
            
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
