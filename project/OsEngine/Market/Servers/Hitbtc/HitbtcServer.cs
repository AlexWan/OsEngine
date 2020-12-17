using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Hitbtc
{
    public class HitbtcServer:AServer
    {
        public HitbtcServer()
        {
            HitbtcServerRealization realization = new HitbtcServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");


        }

        /// <summary>
        /// take candles by instrument
        /// взять свечи по инструменту
        /// </summary>
        /// <param name="nameSec"></param>
        /// <param name="tf"></param>
        /// <returns></returns>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((HitbtcServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }

    public class HitbtcServerRealization : IServerRealization
    {
        public HitbtcServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        #region Server / Сервер

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.Hitbtc; }
        }

        /// <summary>
        /// server status
        /// статус серверов
        /// </summary>
        public ServerConnectStatus ServerStatus { get; set; }

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

        #endregion


        #region Request / Запросы

        /// <summary>
        /// Hitbtc Client
        /// </summary>
        private HitbtcClient _client;

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
                _client.Disconnected -= _client_Disconnected;
                _client.NewPairs -= _client_NewPairs;
                _client.NewPortfolio -= _client_NewPortfolio;
                _client.NewMarketDepth -= _client_NewMarketDepth;
                _client.UpdateMarketDepth -= _client_UpdateMarketDepth;
                _client.NewTradesEvent -= _client_NewTradesEvent;
                _client.UpdatePortfolio -= _client_UpdatePortfolio;
                _client.MyOrderEvent -= _client_MyOrderEvent;

                // to do

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
                _client = new HitbtcClient(((ServerParameterString)ServerParameters[0]).Value,
                    ((ServerParameterPassword)ServerParameters[1]).Value);
                _client.Connected += _client_Connected;
                _client.Disconnected += _client_Disconnected;
                _client.NewPairs += _client_NewPairs;
                _client.NewPortfolio += _client_NewPortfolio;
                _client.NewMarketDepth += _client_NewMarketDepth;
                _client.UpdateMarketDepth += _client_UpdateMarketDepth;
                _client.NewTradesEvent += _client_NewTradesEvent;
                _client.UpdatePortfolio += _client_UpdatePortfolio;
                _client.MyOrderEvent += _client_MyOrderEvent;

                // to do

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
            _client.GetPairs();
        }

        /// <summary>
        /// request portfolios
        /// запросить портфели
        /// </summary>
        public void GetPortfolios()
        {
            _client.GetPortfolio();
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
        /// request instrument history
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return _client.GetCandles(nameSec, tf);
        }

        #endregion


        #region work with orders / работа с ордерами

        /// <summary>
        /// sent order to execution in the trading systme
        /// выслать ордер на исполнение в торговую систему
        /// </summary>
        /// <param name="order">order / ордер</param>
        public void SendOrder(Order order)
        {
            var guid = Guid.NewGuid().ToString().Replace('-', '0');

            var needId = guid.Remove(0, guid.Length - 32);

            _couplers.Add(new OrderCoupler()
            {
                OsOrderNumberUser = order.NumberUser,
                OrderNumberMarket = needId,
            });

            _client.SendOrder(order, needId);

            Task t = new Task(async () =>
            {
                await Task.Delay(7000);

                if (_incominOrders.Find(o => o.NumberUser == order.NumberUser) == null)
                {
                    order.State = OrderStateType.Fail;
                    MyOrderEvent?.Invoke(order);
                    SendLogMessage("Order miss. Id: " + order.NumberUser, LogMessageType.Error);
                }
            });
            t.Start();
        }

        /// <summary>
        /// cancel orders from the trading system
        /// отозвать ордер из торговой системы
        /// </summary>
        /// <param name="order">order/ордер</param>
        public void CancelOrder(Order order)
        {
            if (order == null)
            {
                return;
            }

            var needCoupler = _couplers.Find(x => x.OsOrderNumberUser == order.NumberUser);

            if (needCoupler != null)
            {
                _client.CancelOrder(needCoupler.OrderNumberMarket);
            }
        }

        #endregion

        #region parsing incoming data / разбор входящих данных

        private readonly List<OrderCoupler> _couplers = new List<OrderCoupler>();

        private List<Order> _incominOrders = new List<Order>();

        private void _client_MyOrderEvent(Result result)
        {
            OrderCoupler needCoupler;

            needCoupler = _couplers.Find(c => c.OrderNumberMarket == result.clientOrderId);
            
            if (needCoupler == null)
            {
                return;
            }

            if (result.status == "partiallyFilled" || result.status == "filled")
            {
                var partialVolume =
                    result.quantity.ToDecimal();

                var tradeVolume = partialVolume - needCoupler.CurrentVolume;
                needCoupler.CurrentVolume += tradeVolume;

                MyTrade myTrade = new MyTrade()
                {
                    NumberOrderParent = result.clientOrderId,
                    Side = result.side == "sell" ? Side.Sell : Side.Buy,
                    NumberPosition = Convert.ToString(needCoupler.OsOrderNumberUser),
                    SecurityNameCode = result.symbol,
                    Price =
                        result.price.ToDecimal()
                    ,
                    Volume = tradeVolume,
                    NumberTrade = result.id,
                    Time = result.updatedAt,
                };

                MyTradeEvent?.Invoke(myTrade);
            }

            Order order = new Order();
            order.NumberUser = needCoupler.OsOrderNumberUser;
            order.NumberMarket = result.clientOrderId;
            order.PortfolioNumber = result.symbol.Substring(result.symbol.Length-3);
            order.Price =
                result.price.ToDecimal();
            order.Volume =
                result.quantity.ToDecimal();
            order.Side = result.side == "sell" ? Side.Sell : Side.Buy;
            order.SecurityNameCode = result.symbol;
            order.ServerType = ServerType;
            order.TimeCallBack = Convert.ToDateTime(result.createdAt);
            order.TypeOrder = result.type == "limit" ? OrderPriceType.Limit : OrderPriceType.Market;

            if (result.status == "new")
            {
                order.State = OrderStateType.Activ;
            }
            else if (result.status == "partiallyFilled")
            {
                order.State = OrderStateType.Patrial;
            }
            else if (result.status == "filled")
            {
                order.State = OrderStateType.Done;
                _couplers.Remove(needCoupler);
            }
            else if (result.status == "canceled")
            {
                order.State = OrderStateType.Cancel;
                _couplers.Remove(needCoupler);
            }
            else if (result.status == "expired")
            {
                order.State = OrderStateType.Fail;
            }
            MyOrderEvent?.Invoke(order);

            _incominOrders.Add(order);

            _client.GetBalance();
        }

        /// <summary>
        /// multi-threaded access locker to ticks
        /// блокиратор многопоточного доступа к тикам
        /// </summary>
        private readonly object _newTradesLoker = new object();

        private void _client_NewTradesEvent(RootTrade trades)
        {
            lock (_newTradesLoker)
            {
                try
                {
                    if (trades.@params.data == null)
                    {
                        return;
                    }


                    foreach (Datum datum in trades.@params.data)
                    {
                        Trade trade = new Trade();
                        trade.SecurityNameCode = trades.@params.symbol;

                        trade.Price = datum.price.ToDecimal();
                        trade.Id = datum.id.ToString();
                        trade.Time = Convert.ToDateTime(datum.timestamp);
                        trade.Volume = datum.quantity.ToDecimal();
                        trade.Side = datum.side == "sell" ? Side.Sell : Side.Buy;

                        ServerTime = trade.Time;


                        if (NewTradesEvent != null)
                        {
                            NewTradesEvent(trade);
                        }
                    }

                }
                catch (Exception e)
                {
                    SendLogMessage(e.Message,LogMessageType.Error);
                }

            }
        }


        /// <summary>
        /// all depths
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths;

        private readonly object _depthLocker = new object();

        private void _client_NewMarketDepth(RootDepth hitDepth)
        {
            try
            {
                lock (_depthLocker)
                {
                    if (_depths == null)
                    {
                        _depths = new List<MarketDepth>();
                    }

                    var needDepth = _depths.Find(depth =>
                        depth.SecurityNameCode == hitDepth.@params.symbol);

                    if (needDepth == null)
                    {
                        needDepth = new MarketDepth();
                        needDepth.SecurityNameCode = hitDepth.@params.symbol;
                        _depths.Add(needDepth);
                    }

                    List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                    List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                    // bid
                    foreach (var bid in hitDepth.@params.bid)
                    {
                        bids.Add(new MarketDepthLevel()
                            {
                                Bid = bid.size.ToDecimal(),
                                Price = bid.price.ToDecimal()
                            });
                    
                    }

                    // ask
                    foreach (var ask in hitDepth.@params.ask)
                    {
                        ascs.Add(new MarketDepthLevel()
                        {
                            Ask = ask.size.ToDecimal(),
                            Price = ask.price.ToDecimal()
                        });
                    }

                    needDepth.Asks = ascs;
                    needDepth.Bids = bids;
                    needDepth.Time = Convert.ToDateTime(hitDepth.@params.timestamp);

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

        private bool _needSortAsks = false;
        private bool _needSortBids = false;

        private void _client_UpdateMarketDepth(UpdateDepth updDepth)
        {
            try
            {
                var needDepth = _depths.Find(d => d.SecurityNameCode == updDepth.@params.symbol);

                if (needDepth == null)
                {
                    return;
                }

                if (updDepth.@params.bid != null)
                {
                    foreach (var bid in updDepth.@params.bid)
                    {
                        decimal price = bid.price.ToDecimal();
                        decimal volume = bid.size.ToDecimal();

                        var needLevel = needDepth.Bids.Find(l => l.Price == price);

                        if (needLevel != null)
                        {
                            if (volume == 0)
                            {
                                needDepth.Bids.Remove(needLevel);
                            }
                            else
                            {
                                needLevel.Bid = volume;
                            }
                        }
                        else
                        {
                            needDepth.Bids.Add(new MarketDepthLevel
                            {
                                Bid = volume,
                                Price = price
                            });
                            _needSortBids = true;
                        }
                    }
                }

                if (updDepth.@params.ask != null)
                {

                    foreach (var ask in updDepth.@params.ask)
                    {
                        decimal price = ask.price.ToDecimal();
                        decimal volume = ask.size.ToDecimal();

                        var needLevel = needDepth.Asks.Find(l => l.Price == price);

                        if (needLevel != null)
                        {
                            if (volume == 0)
                            {
                                needDepth.Asks.Remove(needLevel);
                            }
                            else
                            {
                                needLevel.Ask = volume;
                            }
                        }
                        else
                        {
                            needDepth.Asks.Add(new MarketDepthLevel
                            {
                                Ask = volume,
                                Price = price
                            });
                            _needSortAsks = true;
                        }
                    }
                }


                needDepth.Time = DateTime.UtcNow;

                if (_needSortAsks)
                {
                    needDepth.Asks.Sort((a, b) =>
                    {
                        if (a.Price > b.Price)
                        {
                            return 1;
                        }
                        else if (a.Price < b.Price)
                        {
                            return -1;
                        }
                        else
                        {
                            return 0;
                        }
                    });
                    _needSortAsks = false;
                }

                if (_needSortBids)
                {
                    needDepth.Bids.Sort((a, b) =>
                    {
                        if (a.Price > b.Price)
                        {
                            return -1;
                        }
                        else if (a.Price < b.Price)
                        {
                            return 1;
                        }
                        else
                        {
                            return 0;
                        }
                    });
                    _needSortBids = false;
                }

                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(needDepth.GetCopy());
                }
                
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<Portfolio> _portfolios;

        private readonly object _locker = new object();

        private void _client_UpdatePortfolio(string portfolio, UpdateBalance updBalance)
        {
            lock (_locker)
            {
                var needPortfolio = _portfolios.Find(p => p.Number == portfolio);

                if (needPortfolio == null)
                {
                    return;
                }

                if (updBalance.result == null)
                {
                    return;
                }

                foreach (var balance in updBalance.result)
                {
                    decimal avail = balance.available.ToDecimal(),
                        reser = balance.reserved.ToDecimal();

                    var allPos = needPortfolio.GetPositionOnBoard();
                    
                    PositionOnBoard needPos = null;
                    if(allPos!=null)
                    needPos = allPos.Find(pos => pos.SecurityNameCode == balance.currency);

                    if (needPos == null)
                    {
                        continue;
                    }

                    
                    needPos.ValueCurrent = avail;

                    needPos.ValueBlocked = reser;
                    
                }

                PortfolioEvent?.Invoke(_portfolios);
            }
        }
        private void _client_NewPortfolio(BalanceInfo balance)
        {
            try
            {
                if (balance == null)
                {
                    return;
                }
                
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                var cryptoPortfolio = new Portfolio();
                
                cryptoPortfolio.Number = balance.Name;

                var positionsOnBoard = new List<PositionOnBoard>();

                foreach (var portfolio in balance.Balances)
                {
                    var needPos = positionsOnBoard.Find(p => p.SecurityNameCode == portfolio.currency);

                    decimal avail = portfolio.available.ToDecimal();
                    decimal reser = portfolio.reserved.ToDecimal();

                    if (needPos == null)
                    {
                        needPos = new PositionOnBoard();
                        needPos.SecurityNameCode = portfolio.currency;
                        
                        needPos.ValueCurrent = avail;
                        needPos.ValueBlocked = reser;
                        
                        if (needPos.ValueCurrent != 0 || needPos.ValueBlocked != 0)
                        {
                            positionsOnBoard.Add(needPos);
                        }
                    }
                    else
                    {
                        if (avail != 0)
                        {
                            needPos.ValueCurrent = avail;
                        }
                        else if (reser != 0)
                        {
                            needPos.ValueBlocked = reser;
                        }
                    }
                }

                foreach (var pos in positionsOnBoard)
                {
                    cryptoPortfolio.SetNewPosition(pos);
                }

                _portfolios.Add(cryptoPortfolio);

                PortfolioEvent?.Invoke(_portfolios);
                
            }
            catch (Exception e)
            {
                if (LogMessageEvent != null)
                {
                    SendLogMessage(e.ToString(),LogMessageType.Error);
                }
            }
        }

        private List<Security> _securities;
        


        private void _client_NewPairs(List<Symbols> symbols)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            foreach (var symbol in symbols)
            {
                Security security = new Security();
                security.Name = symbol.id;
                security.NameFull = symbol.id;
                security.NameClass = symbol.quoteCurrency;
                security.NameId = symbol.id;
                security.SecurityType = SecurityType.CurrencyPair;
                security.Lot = 1;
                security.PriceStep = symbol.tickSize.ToDecimal();
                security.PriceStepCost = security.PriceStep;

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

        /// <summary>
        /// client lost connection
        /// клиент потерял соединение
        /// </summary>
        private void _client_Disconnected()
        {
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
            ServerStatus = ServerConnectStatus.Disconnect;
        }
        /// <summary>
        /// client connected to exchange
        /// клиент подключился к бирже
        /// </summary>
        private void _client_Connected()
        {
            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
            ServerStatus = ServerConnectStatus.Connect;
        }

        #endregion


        #region Реализовать по возможности
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public void GetOrdersState(List<Order> orders)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region events / события

        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<List<Security>> SecurityEvent;
        public event Action ConnectEvent;
        public event Action DisconnectEvent;


        #endregion

        #region log messages / сообщения для лога

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
        /// outgoing lom message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
        #endregion

        internal class OrderCoupler
        {
            public int OsOrderNumberUser;
            public string OrderNumberMarket;
            public string OrderCancelId;
            public decimal CurrentVolume = 0;
        }
    }
}
