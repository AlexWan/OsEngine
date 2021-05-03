using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Binance.Futures.Entity;
using OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Binance.Futures
{
    public enum FuturesType
    {
        USDT,
        COIN
    }

    public class BinanceServerFutures : AServer
    {
        public BinanceServerFutures()
        {
            BinanceServerFuturesRealization realization = new BinanceServerFuturesRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterEnum("Futures Type", "USDT-M", new List<string> { "USDT-M", "COIN-M" });
            CreateParameterBoolean("HedgeMode", false);
        }

        /// <summary>
        /// instrument history query
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((BinanceServerFuturesRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }

    public class BinanceServerFuturesRealization : IServerRealization
    {
        public BinanceServerFuturesRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        private FuturesType futures_type;

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.BinanceFutures; }
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
        private BinanceClientFutures _client;

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
                _client.NewPortfolio -= _client_NewPortfolio;
                _client.UpdatePortfolio -= _client_UpdatePortfolio;
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
                _client = new BinanceClientFutures(
                    ((ServerParameterString)ServerParameters[0]).Value,
                    ((ServerParameterPassword)ServerParameters[1]).Value);
                _client.Connected += _client_Connected;
                _client.UpdatePairs += _client_UpdatePairs;
                _client.Disconnected += _client_Disconnected;
                _client.NewPortfolio += _client_NewPortfolio;
                _client.UpdatePortfolio += _client_UpdatePortfolio;
                _client.UpdateMarketDepth += _client_UpdateMarketDepth;
                _client.NewTradesEvent += _client_NewTradesEvent;
                _client.MyTradeEvent += _client_MyTradeEvent;
                _client.MyOrderEvent += _client_MyOrderEvent;
                _client.LogMessageEvent += SendLogMessage;
            }

            if (((ServerParameterEnum)ServerParameters[2]).Value == "USDT-M")
            {
                this.futures_type = FuturesType.USDT;
                _client._baseUrl = "https://fapi.binance.com";
                _client.wss_point = "wss://fstream.binance.com";
                _client.type_str_selector = "fapi";
            }
            else if (((ServerParameterEnum)ServerParameters[2]).Value == "COIN-M")
            {
                this.futures_type = FuturesType.COIN;
                _client._baseUrl = "https://dapi.binance.com";
                _client.wss_point = "wss://dstream.binance.com";
                _client.type_str_selector = "dapi";
            }

            _client.futures_type = this.futures_type;
            _client.HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;
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
            _client.GetBalance();
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
        /// cancel order
        /// отозвать ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            _client.CanсelOrder(order);
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
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (endTime > DateTime.Now - new TimeSpan(0, 0, 1, 0))
                endTime = DateTime.Now - new TimeSpan(0, 0, 1, 0);

            int interval = 500 * (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes ;

            List<Candle> candles = new List<Candle>();

            var startTimeStep = startTime;
            var endTimeStep = startTime;

            while (endTime > endTimeStep)
            {
                endTimeStep = endTimeStep + new TimeSpan(0, 0, interval, 0);

                if (endTimeStep > DateTime.Now - new TimeSpan(0, 0, (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes, 0))
                    endTimeStep = DateTime.Now - new TimeSpan(0, 0, (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes, 0);

                List<Candle> stepCandles = _client.GetCandlesForTimes(security.Name, timeFrameBuilder.TimeFrameTimeSpan, startTimeStep, endTimeStep);

                if (stepCandles != null)
                    candles.AddRange(stepCandles);


                startTimeStep = endTimeStep + new TimeSpan(0, 0, (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes, 0);

                Thread.Sleep(300);
            }

            if (candles.Count == 0)
            {
                return null;
            }

            return candles;
        }


        /// <summary>
        /// take ticks data on instrument for period
        /// взять тиковые данные по инструменту за период
        /// </summary>
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime lastDate)
        {
            string markerDateTime = "";

            List<Trade> trades = new List<Trade>();

            DateTime startOver = startTime;

            if (endTime > DateTime.Now - new TimeSpan(0, 0, 1, 0))
                endTime = DateTime.Now - new TimeSpan(0, 0, 1, 0);

            while (true)
            {
                if (startOver >= endTime)
                {
                    break;
                }

                List<Trade> newTrades = _client.GetTickHistoryToSecurity(security.Name, startOver);

                if (newTrades != null && newTrades.Count != 0)
                    trades.AddRange(newTrades);
                else
                    break;

                startOver = trades[trades.Count - 1].Time.AddMilliseconds(1);


                if (markerDateTime != startOver.ToShortDateString())
                {
                    markerDateTime = startOver.ToShortDateString();
                    SendLogMessage(security.Name + " Binance Futures start loading: " + markerDateTime, LogMessageType.System);
                }

                Thread.Sleep(300);
            }

            if (trades.Count == 0)
            {
                return null;
            }

            while (trades.Last().Time >= endTime)
                trades.Remove(trades.Last());


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
            return _client.GetCandles(nameSec, tf);
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
                trade.SecurityNameCode = trades.stream.ToString().ToUpper().Split('@')[0];

                if (trade.SecurityNameCode != trades.data.s)
                {
                    return;
                }

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

        void _client_UpdateMarketDepth(DepthResponseFutures myDepth)
        {
            try
            {
                lock (_depthLocker)
                {
                    if (_depths == null)
                    {
                        _depths = new List<MarketDepth>();
                    }

                    if (myDepth.data.a == null || myDepth.data.a.Count == 0 ||
                        myDepth.data.b == null || myDepth.data.b.Count == 0)
                    {
                        return;
                    }

                    var needDepth = _depths.Find(depth =>
                        depth.SecurityNameCode == myDepth.stream.Split('@')[0].ToUpper());

                    if (needDepth == null)
                    {
                        needDepth = new MarketDepth();
                        needDepth.SecurityNameCode = myDepth.stream.Split('@')[0].ToUpper();
                        _depths.Add(needDepth);
                    }

                    List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                    List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                    for (int i = 0; i < myDepth.data.a.Count; i++)
                    {
                        ascs.Add(new MarketDepthLevel()
                        {
                            Ask =
                                myDepth.data.a[i][1].ToString().ToDecimal()
                            ,
                            Price =
                                myDepth.data.a[i][0].ToString().ToDecimal()

                        });
                    }

                    for (int i = 0; i < myDepth.data.b.Count; i++)
                    {
                        bids.Add(new MarketDepthLevel()
                        {
                            Bid =
                                myDepth.data.b[i][1].ToString().ToDecimal()
                            ,
                            Price =
                                myDepth.data.b[i][0].ToString().ToDecimal()
                        });
                    }

                    needDepth.Asks = ascs;
                    needDepth.Bids = bids;

                    needDepth.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(myDepth.data.T));

                    if (needDepth.Time == DateTime.MinValue)
                    {
                        return;
                    }

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

        #region Портфели

        private List<Portfolio> _portfolios = new List<Portfolio>();

        void _client_UpdatePortfolio(AccountResponseFuturesFromWebSocket portfs)
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

                portfolio = _portfolios.Find(p => p.Number == "BinanceFutures");


                if (portfolio == null)
                {
                    return;
                }

                foreach (var onePortf in portfs.a.B)
                {
                    if (onePortf == null ||
                        onePortf.a == null ||
                        onePortf.wb == null)
                    {
                        continue;
                    }

                    PositionOnBoard neeedPortf =
                        portfolio.GetPositionOnBoard().Find(p => p.SecurityNameCode == onePortf.a);

                    if (neeedPortf == null)
                    {
                        continue;
                    }

                    neeedPortf.ValueCurrent =
                        onePortf.wb.ToDecimal();
                }

                foreach (var onePortf in portfs.a.P)
                {
                    if (onePortf == null ||
                        onePortf.s == null ||
                        onePortf.pa == null)
                    {
                        continue;
                    }

                    PositionOnBoard neeedPortf =
                        portfolio.GetPositionOnBoard().Find(p => p.SecurityNameCode == onePortf.s);

                    if (neeedPortf == null)
                    {
                        PositionOnBoard newPositionOnBoard = new PositionOnBoard();
                        newPositionOnBoard.SecurityNameCode = onePortf.s;
                        newPositionOnBoard.PortfolioName = portfolio.Number;
                        newPositionOnBoard.ValueBegin =
                            onePortf.pa.ToDecimal();
                        portfolio.SetNewPosition(newPositionOnBoard);
                        neeedPortf = newPositionOnBoard;
                    }

                    neeedPortf.ValueCurrent =
                        onePortf.pa.ToDecimal();
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

        void _client_NewPortfolio(AccountResponseFutures portfs)
        {
            try
            {
                Portfolio myPortfolio = _portfolios.Find(p => p.Number == "BinanceFutures");

                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = "BinanceFutures";
                    newPortf.ValueBegin = 1;
                    newPortf.ValueCurrent = 1;
                    _portfolios.Add(newPortf);
                    myPortfolio = newPortf;
                }

                if (portfs.assets == null)
                {
                    return;
                }

                foreach (var onePortf in portfs.assets)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = onePortf.asset;
                    newPortf.ValueBegin =
                        onePortf.marginBalance.ToDecimal();
                    newPortf.ValueCurrent =
                        onePortf.marginBalance.ToDecimal();

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
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
            ServerStatus = ServerConnectStatus.Disconnect;
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
                Security security = new Security();
                security.Name = sec.symbol;
                security.NameFull = sec.symbol;
                security.NameClass = sec.quoteAsset;
                security.NameId = sec.symbol + sec.quoteAsset;
                security.SecurityType = SecurityType.Futures;
                security.Lot = sec.filters[1].minQty.ToDecimal();
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

            //Выставить HedgeMode
            _client.SetPositionMode();

            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
            ServerStatus = ServerConnectStatus.Connect;
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

        /// <summary>
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
