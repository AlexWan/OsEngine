using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using OkonkwoOandaV20;
using OkonkwoOandaV20.TradeLibrary.DataTypes.Account;
using OkonkwoOandaV20.TradeLibrary.DataTypes.Communications;
using OkonkwoOandaV20.TradeLibrary.DataTypes.Communications.Requests.Order;
using OkonkwoOandaV20.TradeLibrary.DataTypes.Instrument;
using OkonkwoOandaV20.TradeLibrary.DataTypes.Order;
using OkonkwoOandaV20.TradeLibrary.DataTypes.Transaction;
using OkonkwoOandaV20.TradeLibrary.Primitives;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using Order = OsEngine.Entity.Order;

namespace OsEngine.Market.Servers.Oanda
{
    public class OandaClient
    {
        // connect / коннект

        private bool _isConnected;

        private EEnvironment _environment;

        /// <summary>
        /// connect to Oanda server
        /// установить соединение сервером Oanda
        /// </summary>
        public void Connect(string id, string token, bool isJuniorConnect)
        {
            if (isJuniorConnect)
            {
                _environment = EEnvironment.Practice;
            }
            else
            {
                _environment = EEnvironment.Trade;
            }

            if (string.IsNullOrEmpty(token))
            {

                SendLogMessage(OsLocalization.Market.Message77 + " https://www.oanda.com/account/tpa/personal_token", LogMessageType.Error);
                return;
            }

            Credentials.SetCredentials(_environment, token, id);

            if (Credentials.GetDefaultCredentials().HasServer(EServer.Account))
            {

                _isConnected = true;

                if (ConnectionSucsess != null)
                {
                    ConnectionSucsess();
                }

                _transactionsSession = new TransactionsSession(Credentials.GetDefaultCredentials().DefaultAccountId);
                _transactionsSession.DataReceived += _transactionsSession_DataReceived;
                _transactionsSession.StartSession();
            }
        }

        /// <summary>
        /// disconnect
        /// отключиться от TCP сервера TWS
        /// </summary>
        public void Dispose()
        {
            if (ConnectionFail != null)
            {
                ConnectionFail();
            }

            if (_pricingStream != null)
            {
                _pricingStream.StopSession();
                _pricingStream = null;
            }

            if (_transactionsSession != null)
            {
                _transactionsSession.StopSession();
                _transactionsSession = null;
            }

            _isDisposed = true;
        }

        private bool _isDisposed;


// portfolios
// Портфели

        /// <summary>
        /// take portfolios
        /// взять портфели
        /// </summary>
        public async void GetPortfolios()
        {
            // string tags = "AccountType,NetLiquidation";
            // reqAccountSummary(50000001, "All", tags);
            if (_isConnected == false)
            {
                return;
            }
            try
            {
                List<AccountProperties> result = await Rest20.GetAccountListAsync();

                // in the result, something is / в result что-то лежит

                foreach (var accountName in result)
                {
                    Account account = await Rest20.GetAccountDetailsAsync(accountName.id);
                    Portfolio newPortfolio = new Portfolio();
                    newPortfolio.Number = account.id;
                    newPortfolio.ValueBegin = Convert.ToDecimal(account.balance);
                    newPortfolio.ValueCurrent = Convert.ToDecimal(account.balance);

                    if (PortfolioChangeEvent != null)
                    {
                        PortfolioChangeEvent(newPortfolio);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

// instruments 
// инструменты


        public async void GetSecurities(List<Portfolio> portfolios)
        {
            if (portfolios == null || portfolios.Count == 0)
            {
                return;
            }

            _allInstruments = new List<Instrument>();

            // Get an instrument list (basic)

            for (int i = 0; i < portfolios.Count; i++)
            {
                List<Instrument> result = await Rest20.GetAccountInstrumentsAsync(portfolios[i].Number);
                _allInstruments.AddRange(result);
            }

            List<Security> _securities = new List<Security>();

            for (int i = 0; i < _allInstruments.Count; i++)
            {
                Security newSecurity = new Security();

                newSecurity.Name = _allInstruments[i].name;
                newSecurity.NameFull = _allInstruments[i].displayName;
                newSecurity.NameClass = _allInstruments[i].type;
                newSecurity.SecurityType = SecurityType.CurrencyPair;
                newSecurity.NameId = "none";

                newSecurity.Lot = 1;

                decimal step = 1;

                for (int i2 = 0; i2 < _allInstruments[i].displayPrecision; i2++)
                {
                    step *= 0.1m;
                }

                newSecurity.PriceStep = step;
                newSecurity.PriceStepCost = step;
                _securities.Add(newSecurity);
            }

            if (NewSecurityEvent != null)
            {
                NewSecurityEvent(_securities);
            }
        }

        public event Action<List<Security>> NewSecurityEvent;

        private List<Instrument> _allInstruments = new List<Instrument>();

// thread requesting trades and depths
// поток запрашивающий трейды и стаканы

        private PricingSession _pricingStream;
        private TransactionsSession _transactionsSession;

        private List<Instrument> _subscrubleInstruments = new List<Instrument>();

        public void StartStreamThreads(Security security)
        {
            Instrument myInstrument = _allInstruments.Find(inst => inst.name == security.Name);

            if (_subscrubleInstruments.Find(subInst => subInst.name == myInstrument.name) != null)
            {
                return;
            }

            _subscrubleInstruments.Add(myInstrument);

            if (_pricingStream != null)
            {
                _pricingStream.StopSession();
                _pricingStream = null;
            }

            _pricingStream = new PricingSession(Credentials.GetDefaultCredentials().DefaultAccountId, _subscrubleInstruments);
            _pricingStream.DataReceived += _marketDepthStream_DataReceived;

            _pricingStream.StartSession();
        }

        void _transactionsSession_DataReceived(OkonkwoOandaV20.TradeLibrary.DataTypes.Stream.TransactionStreamResponse data)
        {
            if (_isDisposed)
            {
                return;
            }

            if (data.transaction.type == "LIMIT_ORDER")
            {
                LimitOrderTransaction order = (LimitOrderTransaction) data.transaction;

                Order newOrder  = new Order();
                newOrder.NumberUser = Convert.ToInt32(order.clientExtensions.id);
                newOrder.NumberMarket = order.id.ToString();
                newOrder.SecurityNameCode = order.instrument;
                newOrder.Price = Convert.ToDecimal(order.price);
                newOrder.TimeCallBack = DateTime.Parse(order.time);
                newOrder.State = OrderStateType.Activ;

                if (NewOrderEvent != null)
                {
                    NewOrderEvent(newOrder);
                }
            }
            else if (data.transaction.type == "LIMIT_ORDER_REJECT")
            {
                LimitOrderRejectTransaction order = (LimitOrderRejectTransaction)data.transaction;

                Order newOrder = new Order();
                newOrder.NumberUser = Convert.ToInt32(order.clientExtensions.id);
                newOrder.NumberMarket = order.id.ToString();
                newOrder.SecurityNameCode = order.instrument;
                newOrder.Price = Convert.ToDecimal(order.price);
                newOrder.State = OrderStateType.Fail;
                newOrder.TimeCallBack = DateTime.Parse(order.time);

                if (NewOrderEvent != null)
                {
                    NewOrderEvent(newOrder);
                }
            }
            else if (data.transaction.type == "ORDER_FILL")
            {
                OrderFillTransaction order = (OrderFillTransaction)data.transaction;

                MyTrade trade = new MyTrade();
                trade.NumberOrderParent = order.clientOrderID;
                trade.NumberTrade = order.id.ToString();
                trade.Price = Convert.ToDecimal(order.price);
                trade.Volume = Convert.ToDecimal(order.units);
                trade.Time = DateTime.Parse(order.time);
                trade.SecurityNameCode = order.instrument;

                Order myOrder = _orders.Find(o => o.NumberUser.ToString() == order.clientOrderID);

                if (myOrder == null)
                {
                    return;
                }
                trade.Volume = myOrder.Volume;

                if (order.units == 1)
                {
                    trade.Side = Side.Buy;
                }
                else
                {
                    trade.Side = Side.Sell;
                }

                if (NewMyTradeEvent != null)
                {
                    NewMyTradeEvent(trade);
                }
            }
            else if (data.transaction.type == "SystemOrderReject")
            {
                Order newOrder = new Order();
               // newOrder.NumberUser = order.NumberUser;
                newOrder.State = OrderStateType.Fail;
                //trade.Time = DateTime.Parse(order.time);
                if (NewOrderEvent != null)
                {
                    NewOrderEvent(newOrder);
                }
            }
            else if (data.transaction.type == "ORDER_CANCEL")
            {
                OrderCancelTransaction order = (OrderCancelTransaction)data.transaction;

                Order newOrder= new Order();
                newOrder.NumberUser = Convert.ToInt32(order.clientOrderID);
                newOrder.State = OrderStateType.Cancel;
                newOrder.TimeCallBack = DateTime.Parse(order.time);
                newOrder.TimeCancel = newOrder.TimeCallBack;
                if (NewOrderEvent != null)
                {
                    NewOrderEvent(newOrder);
                }
            }
        }

        void _marketDepthStream_DataReceived(OkonkwoOandaV20.TradeLibrary.DataTypes.Stream.PricingStreamResponse data)
        {
            if (_isDisposed)
            {
                return;
            }

            Trade newTrade = new Trade();
            Trade newTrade2 = new Trade();
            newTrade.Price = Convert.ToDecimal(data.price.closeoutAsk);
            newTrade2.Price = Convert.ToDecimal(data.price.closeoutAsk);
            newTrade.SecurityNameCode = data.price.instrument;
            newTrade2.SecurityNameCode = data.price.instrument;
            newTrade.Time = DateTime.Parse(data.price.time);
            newTrade2.Time = DateTime.Parse(data.price.time);
            newTrade.MicroSeconds = newTrade.Time.Millisecond * 1000;
            newTrade2.MicroSeconds = newTrade.Time.Millisecond * 1000;

            if (NewTradeEvent != null)
            {
                NewTradeEvent(newTrade);
                NewTradeEvent(newTrade2);
            }

            MarketDepth depth = new MarketDepth();
            depth.Time = newTrade.Time;

            depth.SecurityNameCode = data.price.instrument;

            for (int i = 0; i < data.price.asks.Count; i++)
            {
                depth.Asks.Add(new MarketDepthLevel() { Ask = data.price.asks[i].liquidity, Price = Convert.ToDecimal(data.price.asks[i].price) });
            }

            for (int i = 0; i < data.price.bids.Count; i++)
            {
                depth.Bids.Add(new MarketDepthLevel() { Bid = data.price.bids[i].liquidity, Price = Convert.ToDecimal(data.price.bids[i].price) });
            }


            if (MarketDepthChangeEvent != null)
            {
                MarketDepthChangeEvent(depth);
            }
        }

// work with orders
// работа с ордерами

        private List<Order> _orders = new List<Order>(); 

        /// <summary>
        /// execute orders in the exchange
        /// исполнить ордер на бирже
        /// </summary>
        public async void ExecuteOrder(Order order)
        {
            if (_isConnected == false)
            {
                return;
            }

            _orders.Add(order);

            try
            {
                string expiry = ConvertDateTimeToAcceptDateFormat(DateTime.Now.AddMonths(1));

                decimal volume = order.Volume;

                if (order.Side == Side.Sell)
                {
                    volume = -order.Volume;
                }
                string price = order.Price.ToString(new CultureInfo("en-US"));

                Instrument myInstrument = _allInstruments.Find(inst => inst.name == order.SecurityNameCode);

                // create new pending order
                var request = new LimitOrderRequest(myInstrument)
                {
                    instrument = order.SecurityNameCode,
                    units = Convert.ToInt64(volume),
                    timeInForce = TimeInForce.GoodUntilDate,
                    gtdTime = expiry,
                    price = price.ToDecimal(),

                    clientExtensions = new ClientExtensions()
                    {
                        id = order.NumberUser.ToString(),
                        comment = "",
                        tag = ""
                    },
                    tradeClientExtensions = new ClientExtensions()
                    {
                        id = order.NumberUser.ToString(),
                        comment = "",
                        tag = ""
                    }
                };
                
                var response = await Rest20.PostOrderAsync(Credentials.GetDefaultCredentials().DefaultAccountId, request);
                var orderTransaction = response.orderCreateTransaction;

                if (orderTransaction.id > 0)
                {
                    order.NumberMarket = orderTransaction.id.ToString();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// cancel order
        /// отозвать ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            if (string.IsNullOrWhiteSpace(order.NumberMarket))
            {
                return;
            }
            if (string.IsNullOrEmpty(order.NumberMarket))
            {
                return;
            }
            try
            {
                var openOrders = Rest20.GetPendingOrderListAsync(Credentials.GetDefaultCredentials().DefaultAccountId);

                while (!openOrders.IsCanceled &&
                        !openOrders.IsCompleted &&
                        !openOrders.IsFaulted) 
                {
                    Thread.Sleep(20);
                }
                

                for (int i = 0; i < openOrders.Result.Count; i++)
                {
                    if (openOrders.Result[i].id == Convert.ToInt64(order.NumberMarket))
                    {
                        Rest20.CancelOrderAsync(Credentials.GetDefaultCredentials().DefaultAccountId, openOrders.Result[i].id);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
/*
        /// <summary>
        /// work place of thread monitoring order state in the system
        /// место работы потока следящего за состоянием ордеров в системе
        /// </summary>
        private void OrderListenThreadArea()
        {
            while (true)
            {
                Thread.Sleep(1000);

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                if (_isConnected == false)
                {
                    continue;
                }

               Task<List<IOrder>> allOrdersTask = Rest20.GetOrderListAsync(Credentials.GetDefaultCredentials().DefaultAccountId);

                while (!allOrdersTask.IsCanceled &&
                    !allOrdersTask.IsCompleted &&
                    !allOrdersTask.IsFaulted)
                {
                    Thread.Sleep(50);
                }

                List<IOrder> orders = allOrdersTask.Result;

                for (int i = 0; i < orders.Count; i++)
                {
                    if (string.IsNullOrEmpty(orders[i].clientExtensions.id))
                    {
                        continue;
                    }
                    CheckOrder(orders[i]);
                }
            }
        }

        /// <summary>
        /// list of orders in the system
        /// список ордеров в системе
        /// </summary>
        private List<IOrder> _orders = new List<IOrder>();

        private void CheckOrder(IOrder order)
        {
            IOrder oldOrder = _orders.Find(o => o.id == order.id);

            if (oldOrder == null)
            { // нужно выслать данный ордер на верх, т.к. у нас его вообще не было в системе
                SendUpOrder(order);
                _orders.Add(order);
                return;
            }

            if (oldOrder.state != order.state )
            {
                SendUpOrder(order);

                for (int i = 0; i < _orders.Count; i++)
                {
                    if (_orders[i].id == order.id)
                    {
                        _orders[i] = order;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// send order to up
        /// выслать ордер на верх
        /// </summary>
        /// <param name="order"></param>
        private void SendUpOrder(IOrder order)
        {
            Task<IOrder> orderDeteilTask =
                (Rest20.GetOrderDetailsAsync(Credentials.GetDefaultCredentials().DefaultAccountId, order.id));

            while (!orderDeteilTask.IsCanceled &&
                   !orderDeteilTask.IsCompleted &&
                   !orderDeteilTask.IsFaulted)
            {
                Thread.Sleep(50);
            }

            LimitOrder orderOanda = (LimitOrder)orderDeteilTask.Result;

            if (string.IsNullOrWhiteSpace(orderOanda.clientExtensions.id))
            { // ордер без номера пользователя. Значит выставлен не нами
                return;
            }

            Order newOrder = new Order();
            newOrder.NumberMarket = orderOanda.id.ToString();
            newOrder.Price = Convert.ToDecimal(orderOanda.price);
            newOrder.NumberUser = Convert.ToInt32(orderOanda.clientExtensions.id);
            newOrder.Volume = Convert.ToInt32(orderOanda.units);
            newOrder.TimeCallBack = DateTime.Parse(orderOanda.createTime);

            if (orderOanda.state == "fail")
            {
                newOrder.State = OrderStateType.Fail;
            }
            else if (orderOanda.state == "FILLED")
            {
                newOrder.State = OrderStateType.Activ;
            }
            else if (orderOanda.state == "done")
            {
                newOrder.State = OrderStateType.Done;
            }

            if (NewOrderEvent != null)
            {
                NewOrderEvent(newOrder);
            }
        }

        private List<MyTrade> myTrades = new List<MyTrade>(); 

        private void SendUpTrade(LimitOrder order)
        {
            int sendPosition = 0;

            List<MyTrade> tradesThisOrder = myTrades.FindAll(t => t.NumberOrderParent == order.id.ToString());


            int position = Convert.ToInt32(order.units);

        }*/

// outgoing events
// исходящие события

        /// <summary>
        /// new trade
        /// новый трейд
        /// </summary>
        public event Action<Trade> NewTradeEvent;

        /// <summary>
        /// new order in the system
        /// новый ордер в системе
        /// </summary>
        public event Action<Order> NewOrderEvent;

        /// <summary>
        /// my new trade in the system
        /// новая моя сделка в системе
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        /// <summary>
        /// updated portfolio in the system
        /// в системе обновился портфель
        /// </summary>
        public event Action<Portfolio> PortfolioChangeEvent;

        /// <summary>
        /// pdated depth in the system
        /// в системе обновился портфель
        /// </summary>
        public event Action<MarketDepth> MarketDepthChangeEvent;

        /// <summary>
        /// successfully connected to server
        /// успешно подключились к серверу TWS
        /// </summary>
        public event Action ConnectionSucsess;

        /// <summary>
        /// connection with server is lost
        /// соединение с TWS разорвано
        /// </summary>
        public event Action ConnectionFail;

        // logging
        // логирование работы

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
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        private string ConvertDateTimeToAcceptDateFormat(DateTime time, AcceptDatetimeFormat format = AcceptDatetimeFormat.RFC3339)
        {
            // look into doing this within the JsonSerializer so that objects can use DateTime instead of string

            if (format == AcceptDatetimeFormat.RFC3339)
                return XmlConvert.ToString(time, "yyyy-MM-ddTHH:mm:ssZ");
            else if (format == AcceptDatetimeFormat.Unix)
                return ((int)(time.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString();
            else
                throw new ArgumentException(string.Format("The value ({0}) of the format parameter is invalid.", (short)format));
        }

    }
}
