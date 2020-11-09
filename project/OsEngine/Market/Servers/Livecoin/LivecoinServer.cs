using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Livecoin.LivecoinEntity;
using protobuf.ws;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Order = OsEngine.Entity.Order;

namespace OsEngine.Market.Servers.Livecoin
{

    public class LivecoinServer : AServer
    {
        public LivecoinServer()
        {
            LivecoinServerRealization realization = new LivecoinServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");

        }
    }

    public class LivecoinServerRealization : IServerRealization
    {
        public LivecoinServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.Livecoin; }
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

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action<OsEngine.Entity.Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<List<Security>> SecurityEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<OsEngine.Entity.Trade> NewTradesEvent;
        public event Action ConnectEvent;
        public event Action DisconnectEvent;


        // requests
        // запросы


        LivecoinClient _client;

        /// <summary>
        /// connect to API
        /// подсоединиться к апи
        /// </summary>
        public void Connect()
        {
            if (_client == null)
            {
                _client = new LivecoinClient(((ServerParameterString)ServerParameters[0]).Value,
                    ((ServerParameterPassword)ServerParameters[1]).Value,
                    ServerType.ToString());
                _client.Connected += Client_Connected;
                _client.UpdatePairs += Client_UpdatePairs;
                _client.Disconnected += Client_Disconnected;
                _client.NewPortfolio += Client_NewPortfolio;
                _client.UpdatePortfolio += Client_UpdatePortfolio;
                _client.NewMarketDepth += Client_NewMarketDepth;
                _client.UpdateMarketDepth += Client_UpdateMarketDepth;
                _client.NewTradesEvent += Client_NewTradesEvent;
                _client.MyTradeEvent += Client_MyTradeEvent;
                _client.MyOrderEvent += Client_MyOrderEvent;
                _client.LogMessageEvent += SendLogMessage;
            }

            _client.Connect();
        }

        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();

                _client.Connected -= Client_Connected;
                _client.UpdatePairs -= Client_UpdatePairs;
                _client.Disconnected -= Client_Disconnected;
                _client.NewPortfolio -= Client_NewPortfolio;
                _client.UpdatePortfolio -= Client_UpdatePortfolio;
                _client.NewMarketDepth -= Client_NewMarketDepth;
                _client.UpdateMarketDepth -= Client_UpdateMarketDepth;
                _client.NewTradesEvent -= Client_NewTradesEvent;
                _client.MyTradeEvent -= Client_MyTradeEvent;
                _client.MyOrderEvent -= Client_MyOrderEvent;
                _client.LogMessageEvent -= SendLogMessage;

                _client = null;
            }

            _depths = null;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public void GetOrdersState(List<OsEngine.Entity.Order> orders)
        {

        }

        public void GetPortfolios()
        {
            _client.GetPortfolios();
        }

        public void GetSecurities()
        {
            _client.GetSecurities();
        }

        public List<OsEngine.Entity.Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public void SendOrder(OsEngine.Entity.Order order)
        {
            lock (_locker)
            {
                _myOrders.Add(order);
            }

            _client.SendOrder(order);
        }

        public void CancelOrder(OsEngine.Entity.Order order)
        {
            _client.CancelLimitOrder(order);
        }

        public void Subscrible(Security security)
        {
            _client.Subscribe(security.Name.Replace('_','/'));
        }

        //parsing incoming data
        // разбор входящих данных

        void Client_Connected()
        {
            ConnectEvent?.Invoke();

            ServerStatus = ServerConnectStatus.Connect;
        }

        void Client_Disconnected()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            DisconnectEvent?.Invoke();
        }

        private List<Security> _securities;

        void Client_UpdatePairs(RestrictionSecurities securitiesInfo)
        {

            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            foreach (var sec in securitiesInfo.restrictions)
            {
                Security security = new Security();
                security.Name = sec.currencyPair.Replace('/','_');
                security.NameFull = security.Name;
                security.NameClass = GetCurrency(sec.currencyPair);
                security.NameId = security.Name + "_" + security.NameClass;
                security.SecurityType = SecurityType.CurrencyPair;
                security.Lot = 1;
                security.Decimals = Convert.ToInt32(sec.priceScale);
                security.PriceStep = CalculatePriceStep(security.Decimals);
                security.PriceStepCost = security.PriceStep;
                security.State = SecurityStateType.Activ;
                _securities.Add(security);
            }

            if (SecurityEvent != null)
            {
                SecurityEvent(_securities);
            }
        }

        private string GetCurrency(string securityName)
        {
            return securityName.Split('/')[1];
        }

        private decimal CalculatePriceStep(int decimals)
        {
            decimal priceStep = 1;
            for (int i = 0; i < decimals; i++)
            {
                priceStep *= 0.1m;
            }
            return priceStep;
        }

        private List<Portfolio> _portfolios;

        void Client_NewPortfolio(BalanceInfo balance)
        {
            lock (_locker)
            {
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

                    if (needPos == null)
                    {
                        needPos = new PositionOnBoard();
                        needPos.SecurityNameCode = portfolio.currency;
                        if (portfolio.type == "available")
                        {
                            needPos.ValueCurrent = ParseDecimal(portfolio.value);
                        }
                        else if (portfolio.type == "trade")
                        {

                            needPos.ValueBlocked = ParseDecimal(portfolio.value);
                        }
                        if (needPos.ValueCurrent != 0 || needPos.ValueBlocked != 0)
                        {
                            positionsOnBoard.Add(needPos);
                        }
                    }
                    else
                    {
                        if (portfolio.type == "available")
                        {
                            needPos.ValueCurrent = ParseDecimal(portfolio.value);
                        }
                        else if (portfolio.type == "trade")
                        {
                            needPos.ValueBlocked = ParseDecimal(portfolio.value);
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
        }

        private readonly object _locker = new object();

        private void Client_UpdatePortfolio(string portfolio, PrivateChangeBalanceNotification balanceNotification)
        {
            lock (_locker)
            {
                var needPortfolio = _portfolios.Find(p => p.Number == portfolio);

                if (needPortfolio == null)
                {
                    return;
                }

                foreach (var balance in balanceNotification.Datas)
                {
                    var allPos = needPortfolio.GetPositionOnBoard();
                    var needPos = allPos.Find(pos => pos.SecurityNameCode == balance.Currency);

                    if (needPos == null)
                    {
                        continue;
                    }

                    if (balance.Type == BalanceResponse.BalanceType.Available)
                    {
                        needPos.ValueCurrent = ParseDecimal(balance.Value);
                    }
                    else if (balance.Type == BalanceResponse.BalanceType.Trade)
                    {
                        needPos.ValueBlocked = ParseDecimal(balance.Value);
                    }
                }

                PortfolioEvent?.Invoke(_portfolios);
            }
        }

        private bool _needSortAsks = false;
        private bool _needSortBids = false;

        private void Client_UpdateMarketDepth(OrderBookNotification orderBook)
        {
            var needDepth = _depths.Find(d => d.SecurityNameCode == orderBook.CurrencyPair);

            if (needDepth == null)
            {
                return;
            }

            foreach (var level in orderBook.Datas)
            {
                var price = ParseDecimal(level.Price);
                var volume = ParseDecimal(level.Quantity);

                if (level.order_type == OrderBookEvent.OrderType.Ask)
                {
                    var needLevel = needDepth.Asks.Find(l => l.Price == price);

                    if (needLevel != null)
                    {
                        if (level.Quantity == "0")
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
                else if (level.order_type == OrderBookEvent.OrderType.Bid)
                {
                    var needLevel = needDepth.Bids.Find(l => l.Price == price);

                    if (needLevel != null)
                    {
                        if (level.Quantity == "0")
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
                needDepth.Time = DateTime.UtcNow;
            }

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

            while (needDepth.Asks.Count > 20)
            {
                needDepth.Asks.RemoveAt(needDepth.Asks.Count-1);
            }

            while (needDepth.Bids.Count > 20)
            {
                needDepth.Bids.RemoveAt(needDepth.Bids.Count - 1);
            }

            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(needDepth.GetCopy());
            }
        }

        /// <summary>
        /// all depths
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths;

        private void Client_NewMarketDepth(OrderBookChannelSubscribedResponse orderBook)
        {
            if (_depths == null)
            {
                _depths = new List<MarketDepth>();
            }

            var newDepth = new MarketDepth();
            newDepth.SecurityNameCode = orderBook.CurrencyPair;

            List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            long biggestTimeStamp = 0;

            foreach (var level in orderBook.Datas)
            {

                if (level.order_type == OrderBookEvent.OrderType.Ask)
                {
                    ascs.Add(new MarketDepthLevel
                    {
                        Price = ParseDecimal(level.Price),
                        Ask = ParseDecimal(level.Quantity),
                    });
                }
                else
                {
                    bids.Add(new MarketDepthLevel
                    {
                        Price = ParseDecimal(level.Price),
                        Bid = ParseDecimal(level.Quantity),
                    });
                }

                if (level.Timestamp > biggestTimeStamp)
                {
                    biggestTimeStamp = level.Timestamp;
                }
            }

            newDepth.Asks = ascs;
            newDepth.Bids = bids;

            if (biggestTimeStamp != 0)
            {
                newDepth.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(biggestTimeStamp));
                ServerTime = newDepth.Time;
            }

            _depths.Add(newDepth);

            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(newDepth.GetCopy());
            }


        }

        private void Client_NewTradesEvent(TradeNotification newTrade)
        {
            foreach (var t in newTrade.Datas)
            {
                OsEngine.Entity.Trade trade = new OsEngine.Entity.Trade();
                trade.SecurityNameCode = newTrade.CurrencyPair;

                trade.Id = t.Id.ToString();
                trade.Price =
                   Convert.ToDecimal(
                       t.Price.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                       CultureInfo.InvariantCulture);
                trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(t.Timestamp));
                trade.Volume =
                    Convert.ToDecimal(
                        t.Quantity.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);
                trade.Side = t.trade_type == TradeEvent.TradeType.Sell ? Side.Sell : Side.Buy;

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(trade);
                }
            }

        }

        private object _orderLocker = new object();

        private void Client_MyOrderEvent(int numUser, string portfolio, PrivateOrderRawEvent orderInfo)
        {
            lock (_orderLocker)
            {
                var needOrder = _myOrders.Find(o => o.NumberUser == numUser);

                if (needOrder == null)
                {
                    return;
                }

                var order = new OsEngine.Entity.Order();
                order.NumberUser = numUser;
                order.PortfolioNumber = portfolio;
                order.SecurityNameCode = needOrder.SecurityNameCode;

                if (orderInfo.Id == -1)
                {
                    order.State = OrderStateType.Fail;
                    order.Price = needOrder.Price;
                    order.Volume = needOrder.Volume;
                    order.Side = needOrder.Side;
                    order.NumberMarket = needOrder.NumberMarket;

                    MyOrderEvent?.Invoke(order);

                    return;
                }

                order.NumberMarket = orderInfo.Id.ToString();
                order.Price = ParseDecimal(orderInfo.Price);
                order.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(orderInfo.Timestamp));
                order.Volume = needOrder.Volume;
                order.Side = orderInfo.order_type == PrivateOrderRawEvent.OrderType.Bid ? Side.Buy : Side.Sell;

                decimal quantity = ParseDecimal(orderInfo.Quantity);
                decimal cancelVolume = ParseDecimal(orderInfo.QuantityLeftBeforeCancellation);

                if (quantity == 0 && cancelVolume == needOrder.Volume)
                {
                    order.State = OrderStateType.Cancel;
                    DelOrder(needOrder);
                }
                else if (quantity == 0 && cancelVolume == 0)
                {
                    order.State = OrderStateType.Done;
                    DelOrder(needOrder);
                }
                else if (quantity == order.Volume)
                {
                    order.State = OrderStateType.Activ;
                }
                else if (needOrder.Volume != quantity && quantity != 0)
                {
                    order.State = OrderStateType.Patrial;
                }

                MyOrderEvent?.Invoke(order);
            }
        }

        private void DelOrder(Order order)
        {
            lock (_locker)
            {
                _myOrders.Remove(order);
            }
        }

        private void Client_MyTradeEvent(string orderNumberParent, PrivateTradeEvent tradeInfo)
        {
            MyTrade myTrade = new MyTrade();
            myTrade.NumberTrade = tradeInfo.Id.ToString();
            myTrade.Price = ParseDecimal(tradeInfo.Price);
            myTrade.SecurityNameCode = tradeInfo.CurrencyPair;
            myTrade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(tradeInfo.Timestamp));
            myTrade.Side = tradeInfo.trade_type == PrivateTradeEvent.TradeType.Buy ? Side.Buy : Side.Sell;
            myTrade.NumberOrderParent = orderNumberParent;
            myTrade.Volume = ParseDecimal(tradeInfo.Quantity);

            MyTradeEvent?.Invoke(myTrade);
        }

        private List<Order> _myOrders = new List<Order>();

        private decimal ParseDecimal(string number)
        {
            return Decimal.Parse(number, System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo);
        }



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
