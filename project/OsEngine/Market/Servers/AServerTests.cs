/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OkonkwoOandaV20.TradeLibrary.DataTypes.Trade;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using ru.micexrts.cgate;
using Trade = OsEngine.Entity.Trade;

namespace OsEngine.Market.Servers
{
    /// <summary>
    /// automatic tests for servers implemented with AServer
    /// Автоматические тесты для серверов реализованных через AServer
    /// </summary>
    public class AServerTests
    {

        /// <summary>
        /// start listening to the server
        /// начать прослушку сервера
        /// </summary>
        public void Listen(AServer server)
        {
            // стандартные события IServer которые отправляются на верх
            server.ConnectStatusChangeEvent += server_ConnectStatusChangeEvent;
            server.NeadToReconnectEvent += server_NeadToReconnectEvent;
            server.NewMarketDepthEvent += server_NewMarketDepthEvent;
            server.NewMyTradeEvent += server_NewMyTradeEvent;
            server.NewOrderIncomeEvent += server_NewOrderIncomeEvent;
            server.NewTradeEvent += server_NewTradeEvent;
            server.PortfoliosChangeEvent += server_PortfoliosChangeEvent;
            server.SecuritiesChangeEvent += server_SecuritiesChangeEvent;
            server.TimeServerChangeEvent += server_TimeServerChangeEvent;
            server.NewBidAscIncomeEvent += server_NewBidAscIncomeEvent;

            server.UserSetOrderOnCancel += server_UserSetOrderOnCancel;
            server.UserSetOrderOnExecute += server_UserSetOrderOnExecute;
            server.UserWhantConnect += server_UserWhantConnect;
            server.UserWhantDisconnect += server_UserWhantDisconnect;

            Thread marketDepthCheker = new Thread(CheckMarketDepth);
            marketDepthCheker.IsBackground = true;
            marketDepthCheker.Start();

            Thread securitiesCheker = new Thread(CheckSecurity);
            securitiesCheker.IsBackground = true;
            securitiesCheker.Start();

            Thread portfolioCheker = new Thread(CheckPortfolio);
            portfolioCheker.IsBackground = true;
            portfolioCheker.Start();

            Thread tradesChecker = new Thread(CheckTrade);
            tradesChecker.IsBackground = true;
            tradesChecker.Start();

            Thread myTradesChecker = new Thread(CheckMyTrades);
            myTradesChecker.IsBackground = true;
            myTradesChecker.Start();

            Thread ordersChecker = new Thread(CheckOrders);
            ordersChecker.IsBackground = true;
            ordersChecker.Start();

            Thread bidAskCheker = new Thread(CheckBidAsk);
            bidAskCheker.IsBackground = true;
            bidAskCheker.Start();

            if (server.ServerStatus == ServerConnectStatus.Connect)
            {
                SendLogMessage("Server Tests Error. Server create with Connect status ", LogMessageType.Error);
            }
        }

        void server_UserWhantDisconnect()
        {
            
        }

        void server_UserWhantConnect()
        {
            
        }

        void server_UserSetOrderOnExecute(Order order)
        {
            _lastTimeUserSetOrderToExecute = DateTime.Now;
        }

        void server_UserSetOrderOnCancel(Order order)
        {
            _lastTimeUserSetOrderToCansel = DateTime.Now;
        }

// standard events
// стандартные события

        void server_NewBidAscIncomeEvent(decimal bid, decimal ask, Security security)
        {
            if (_bidAskCheck == false)
            {
                return;
            }
            _bidAsk.Bid = bid;
            _bidAsk.Ask = ask;
            _bidAsk.Security = security;
            _bidAskCheck = false;
        }

        void server_TimeServerChangeEvent(DateTime time)
        {
            
        }

        void server_SecuritiesChangeEvent(List<Security> securities)
        {
            if (_securitiesToCheck.Count > 100)
            {
                return;
            }
            for (int i = 0; i < securities.Count; i++)
            {
                _securitiesToCheck.Enqueue(securities[i]);
            }
        }

        void server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
            if (_portfolios.Count > 100)
            {
                return;
            }
            for (int i = 0; i < portfolios.Count; i++)
            {
                _portfolios.Enqueue(portfolios[i]);
            }
        }

        void server_NewTradeEvent(List<Trade> trades)
        {
            if (_trades.Count > 100)
            {
                return;
            }
            for (int i = trades.Count-1; 
                i > 0 && i > trades.Count-10; 
                i--)
            {
                _trades.Enqueue(trades[i]);
            }
        }

        void server_NewOrderIncomeEvent(Order order)
        {
           _orders.Enqueue(order);
        }

        void server_NewMyTradeEvent(MyTrade myTrade)
        {
            _myTrades.Enqueue(myTrade);
        }

        void server_NewMarketDepthEvent(MarketDepth marketDepth)
        {
            if (_marketDepthCheckIsOver)
            {
                _marketDepthCheckIsOver = false;
                _marketDepthToCheck = marketDepth;
            }
        }

        void server_NeadToReconnectEvent()
        {
          
        }

        void server_ConnectStatusChangeEvent(string status)
        {
           
        }

        #region Валидация простых данных

// bid/ask validation
// валидация бида с аском

        private BidAskSender _bidAsk = new BidAskSender();

        private bool _bidAskCheck = true;

        private void CheckBidAsk()
        {
            while (true)
            {
                Thread.Sleep(1000);

                if (_bidAskCheck == true)
                {
                    continue;
                }

                if (_bidAsk == null)
                {
                    Thread.Sleep(1000);
                    _bidAskCheck = true; continue;
                }

                if (_bidAsk.Ask == 0)
                {
                    SendLogMessage("bidAsk Error. No Ask ", LogMessageType.Error);
                    _bidAskCheck = true; continue;
                }


                if (_bidAsk.Bid == 0)
                {
                    SendLogMessage("bidAsk Error. No Bid ", LogMessageType.Error);
                    _bidAskCheck = true; continue;
                }

                if (_bidAsk.Ask < _bidAsk.Bid)
                {
                    SendLogMessage("bidAsk Error. Ask < Bid ", LogMessageType.Error);
                    _bidAskCheck = true; continue;
                }

                if (_bidAsk.Security == null)
                {
                    SendLogMessage("bidAsk Error. No Security ", LogMessageType.Error);
                    _bidAskCheck = true; continue;
                }
                _bidAskCheck = true;
            }

        }

// orders validation
// валидация ордеров

        private ConcurrentQueue<Order> _orders = new ConcurrentQueue<Order>();

        private DateTime _lastTimeExecuteOrder = DateTime.MinValue;

        private DateTime _lastTimeIncomeOrder = DateTime.MinValue;

        private DateTime _lastTimeUserSetOrderToExecute = DateTime.MinValue;

        private DateTime _lastTimeUserSetOrderToCansel = DateTime.MinValue;

        private void CheckOrders()
        {
            while (true)
            {
                Thread.Sleep(1000);

                if (_lastTimeUserSetOrderToCansel != DateTime.MinValue &&
                    _lastTimeUserSetOrderToCansel.AddSeconds(30) > DateTime.Now &&
                    _lastTimeUserSetOrderToCansel.AddSeconds(20) < DateTime.Now &&
                    _lastTimeIncomeOrder.AddSeconds(20) < _lastTimeUserSetOrderToCansel)
                { // order placed on server but no response / ордер выставлен в сервер, но ответной реакции нет
                    SendLogMessage("Order Error. No server reaction on cansel order. ", LogMessageType.Error);
                }

                if (_lastTimeUserSetOrderToExecute != DateTime.MinValue &&
                    _lastTimeUserSetOrderToExecute.AddSeconds(30) > DateTime.Now &&
                    _lastTimeUserSetOrderToExecute.AddSeconds(20) < DateTime.Now &&
                    _lastTimeIncomeOrder.AddSeconds(20) < _lastTimeUserSetOrderToExecute)
                { // order placed on server but no response / ордер выставлен в сервер, но ответной реакции нет
                    SendLogMessage("Order Error. No server reaction on execute order. ", LogMessageType.Error);
                }

                if (_orders.IsEmpty)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                Order order = null;
                _orders.TryDequeue(out order);

                if (order == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                _lastTimeIncomeOrder = DateTime.Now;

                if (order.State == OrderStateType.Done)
                {
                    _lastTimeExecuteOrder = DateTime.Now;
                }

                if (string.IsNullOrEmpty(order.SecurityNameCode))
                {
                    SendLogMessage("Order Error. No SecurityNameCode ", LogMessageType.Error);
                    continue;
                }

                if (order.State != OrderStateType.Fail &&
                    string.IsNullOrEmpty(order.NumberMarket))
                {
                    SendLogMessage("Order Error. No NumberMarket ", LogMessageType.Error);
                    continue;
                }

                if (order.NumberUser == 0)
                {
                    SendLogMessage("Order Error. No NumberUser ", LogMessageType.Error);
                    continue;
                }
                if (order.Price == 0)
                {
                    SendLogMessage("Order Error. No Price ", LogMessageType.Error);
                    continue;
                }

                if (order.ServerType == ServerType.None)
                {
                    SendLogMessage("Order Error. No ServerType", LogMessageType.Error);
                    continue;
                }

                if (order.Side == Side.None)
                {
                    SendLogMessage("Order Error. No Side", LogMessageType.Error);
                    continue;
                }

                if (order.Volume == 0)
                {
                    SendLogMessage("Order Error. No Volume", LogMessageType.Error);
                    continue;
                }

                if (order.State == OrderStateType.None)
                {
                    SendLogMessage("Order Error. No State", LogMessageType.Error);
                    continue;
                }

                if (string.IsNullOrEmpty(order.PortfolioNumber))
                {
                    SendLogMessage("Order Error. No PortfolioNumber ", LogMessageType.Error);
                    continue;
                }
            }
        }

// MyTrade validation
// валидация сделок MyTrade

        private ConcurrentQueue<MyTrade> _myTrades = new ConcurrentQueue<MyTrade>();

        private void CheckMyTrades()
        {
            while (true)
            {
                Thread.Sleep(1000);

                if (_myTrades.IsEmpty)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                MyTrade myTrade = null;
                _myTrades.TryDequeue(out myTrade);

                if (myTrade == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                if (string.IsNullOrEmpty(myTrade.SecurityNameCode))
                {
                    SendLogMessage("MyTrade Error. No SecurityNameCode ", LogMessageType.Error);
                    continue;
                }

                if (string.IsNullOrEmpty(myTrade.NumberOrderParent))
                {
                    SendLogMessage("MyTrade Error. No number Order Parent. " + myTrade.SecurityNameCode, LogMessageType.Error);
                    continue;
                }

                if (string.IsNullOrEmpty(myTrade.NumberTrade))
                {
                    SendLogMessage("MyTrade Error. No NumberTrade " + myTrade.SecurityNameCode, LogMessageType.Error);
                    continue;
                }

                if (myTrade.Price == 0)
                {
                    SendLogMessage("MyTrade Error. No Price " + myTrade.SecurityNameCode, LogMessageType.Error);
                    continue;
                }

                if (myTrade.Side == Side.None)
                {
                    SendLogMessage("MyTrade Error. No Side " + myTrade.SecurityNameCode, LogMessageType.Error);
                    continue;
                }

                if (myTrade.Time == DateTime.MinValue)
                {
                    SendLogMessage("MyTrade Error. No Time " + myTrade.SecurityNameCode, LogMessageType.Error);
                    continue;
                }

                if (myTrade.Volume == 0)
                {
                    SendLogMessage("MyTrade Error. No Volume " + myTrade.SecurityNameCode, LogMessageType.Error);
                    continue;
                }
            }
        }

// market depth data validation
// валидация данных стакана

        private MarketDepth _marketDepthToCheck;
        private bool _marketDepthCheckIsOver = true;

        private void CheckMarketDepth()
        {
            while (true)
            {
                Thread.Sleep(1000);

                try
                {
                    if (_marketDepthToCheck == null)
                    {
                        _marketDepthCheckIsOver = true;
                        continue;
                    }

                    if (_marketDepthCheckIsOver == true)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(_marketDepthToCheck.SecurityNameCode))
                    {
                        SendLogMessage("MarketDepth Error. No Security Name " + _marketDepthToCheck.SecurityNameCode, LogMessageType.Error);
                        _marketDepthCheckIsOver = true;
                        continue;
                    }
                    if (_marketDepthToCheck.Time == DateTime.MinValue)
                    {
                        SendLogMessage("MarketDepth Error. No Time " + _marketDepthToCheck.SecurityNameCode, LogMessageType.Error);
                        _marketDepthCheckIsOver = true;
                        continue;
                    }
                    if (_marketDepthToCheck.Asks.Count == 0)
                    {
                        SendLogMessage("MarketDepth Error. No Asks. " + _marketDepthToCheck.SecurityNameCode, LogMessageType.Error);
                        _marketDepthCheckIsOver = true; continue;
                    }
                    if (_marketDepthToCheck.Bids.Count == 0)
                    {
                        SendLogMessage("MarketDepth Error. No Bids. " + _marketDepthToCheck.SecurityNameCode, LogMessageType.Error);
                        _marketDepthCheckIsOver = true; continue;
                    }

                    decimal askSumm = _marketDepthToCheck.AskSummVolume;
                    decimal bidsSumm = _marketDepthToCheck.BidSummVolume;

                    if (_marketDepthToCheck.Bids[0].Price > _marketDepthToCheck.Asks[0].Price)
                    {
                        SendLogMessage("MarketDepth Error. Bid higher Ask. " + _marketDepthToCheck.SecurityNameCode, LogMessageType.Error);
                        _marketDepthCheckIsOver = true; continue;
                    }

                    for (int i = 0; i < _marketDepthToCheck.Bids.Count; i++)
                    {
                        if (_marketDepthToCheck.Bids[i] == null)
                        {
                            SendLogMessage("MarketDepth Error. Bid null value. " + _marketDepthToCheck.SecurityNameCode, LogMessageType.Error);
                            _marketDepthCheckIsOver = true; continue;
                        }
                    }

                    for (int i = 0; i < _marketDepthToCheck.Asks.Count; i++)
                    {
                        if (_marketDepthToCheck.Asks[i] == null)
                        {
                            SendLogMessage("MarketDepth Error. Ask null value. " + _marketDepthToCheck.SecurityNameCode, LogMessageType.Error);
                            _marketDepthCheckIsOver = true; continue;
                        }
                    }

                    for (int i = 0; i < _marketDepthToCheck.Bids.Count; i++)
                    {
                        if (_marketDepthToCheck.Bids[i].Price == 0)
                        {
                            SendLogMessage("MarketDepth Error. Bid zero value. " + _marketDepthToCheck.SecurityNameCode, LogMessageType.Error);
                            _marketDepthCheckIsOver = true; continue;
                        }
                    }

                    for (int i = 0; i < _marketDepthToCheck.Asks.Count; i++)
                    {
                        if (_marketDepthToCheck.Asks[i].Price == 0)
                        {
                            SendLogMessage("MarketDepth Error. Ask zero value. " + _marketDepthToCheck.SecurityNameCode, LogMessageType.Error);
                            _marketDepthCheckIsOver = true; continue;
                        }
                    }

                    for (int i = 1; i < _marketDepthToCheck.Bids.Count; i++)
                    {
                        if (_marketDepthToCheck.Bids[i].Price > _marketDepthToCheck.Bids[i - 1].Price)
                        {
                            SendLogMessage("MarketDepth Error. Bids wrong Sort. " + _marketDepthToCheck.SecurityNameCode, LogMessageType.Error);
                            _marketDepthCheckIsOver = true; continue;
                        }
                    }

                    for (int i = 1; i < _marketDepthToCheck.Asks.Count; i++)
                    {
                        if (_marketDepthToCheck.Asks[i].Price < _marketDepthToCheck.Asks[i - 1].Price)
                        {
                            SendLogMessage("MarketDepth Error. Ask wrong Sort. " + _marketDepthToCheck.SecurityNameCode, LogMessageType.Error);
                            _marketDepthCheckIsOver = true; continue;
                        }
                    }
                    Thread.Sleep(3000);
                    if (askSumm != _marketDepthToCheck.AskSummVolume
                        ||
                        bidsSumm != _marketDepthToCheck.BidSummVolume)
                    {
                        SendLogMessage("MarketDepth Error. Bid or Ask SummVolume Change. Use GetCopy in Server realization to send Up marketDepth," +
                            " and dont touch marketDepth if you send that up" + _marketDepthToCheck.SecurityNameCode, LogMessageType.Error);
                        _marketDepthCheckIsOver = true; continue;
                    }
                    _marketDepthCheckIsOver = true;
                }
                catch (Exception error)
                {
                    SendLogMessage("MarketDepth Error" + _marketDepthToCheck.SecurityNameCode + error.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

// security data validation
// валидация данных по бумагам

        private ConcurrentQueue<Security> _securitiesToCheck = new ConcurrentQueue<Security>();  

        private void CheckSecurity()
        {
            while (true)
            {
                if (_securitiesToCheck.IsEmpty)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                Security security = null;
                _securitiesToCheck.TryDequeue(out security);

                if (security == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                if (string.IsNullOrEmpty(security.Name))
                {
                    SendLogMessage("Security Error. No Name. ", LogMessageType.Error);
                    continue;
                }
                if (string.IsNullOrEmpty(security.NameClass))
                {
                    SendLogMessage("Security Error. No Name Class. ", LogMessageType.Error);
                     continue;
                }
                if (string.IsNullOrEmpty(security.NameFull))
                {
                    SendLogMessage("Security Error. No Full Name. ", LogMessageType.Error);
                    continue;
                }
                if (string.IsNullOrEmpty(security.NameId))
                {
                    SendLogMessage("Security Error. No NameId. ", LogMessageType.Error);
                    continue;
                }
                if (security.Decimals == -1)
                {
                    SendLogMessage("Security Error. No Decimals ", LogMessageType.Error);
                    continue;
                }
                if (security.SecurityType == SecurityType.None)
                {
                    SendLogMessage("Security Error. No SecurityType ", LogMessageType.Error);
                    continue;
                }
                if (security.SecurityType == SecurityType.Option &&
                    security.OptionType == OptionType.None)
                {
                    SendLogMessage("Security Error. Option Security have type None. No put. No call. ", LogMessageType.Error);
                    continue;
                }
                if (security.SecurityType == SecurityType.Option &&
                    security.Expiration == DateTime.MinValue)
                {
                    SendLogMessage("Security Error. Option Security have none expiration date. ", LogMessageType.Error);
                    continue;
                }
                if (security.SecurityType == SecurityType.Option &&
                    security.Strike == 0)
                {
                    SendLogMessage("Security Error. Option Security have none Strike price. ", LogMessageType.Error);
                    continue;
                }
                if (security.PriceStep <= 0)
                {
                    SendLogMessage("Security Error. Security have zero PriceStep. ", LogMessageType.Error);
                    continue;
                }
                if (security.PriceStepCost <= 0)
                {
                    SendLogMessage("Security Error. Security have zero PriceStepCost. ", LogMessageType.Error);
                    continue;
                }
                if (security.Lot <= 0)
                {
                    SendLogMessage("Security Error. Security have zero Lot. Send 1 in this field in Server realization if dont now what is it. ", LogMessageType.Error);
                    continue;
                }
            }
        }

// anonymous trade table validation
// валидация таблицы обезличенных сделок

        private ConcurrentQueue<Trade> _trades = new ConcurrentQueue<Trade>(); 

        private void CheckTrade()
        {
            while (true)
            {
                if (_trades.IsEmpty)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                Trade trade = null;
                _trades.TryDequeue(out  trade);

                if (trade == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                if (string.IsNullOrEmpty(trade.SecurityNameCode))
                {
                    SendLogMessage("Trade Error. No security Name. ", LogMessageType.Error);
                    continue;
                }

                if (trade.Price == 0)
                {
                    SendLogMessage("Trade Error. No price. " + trade.SecurityNameCode, LogMessageType.Error);
                    continue;
                }
                if (trade.Volume == 0)
                {
                    SendLogMessage("Trade Error. No volume. " + trade.SecurityNameCode, LogMessageType.Error);
                    continue;
                }
                if (trade.Time == DateTime.MinValue)
                {
                    SendLogMessage("Trade Error. No time. " + trade.SecurityNameCode, LogMessageType.Error);
                    continue;
                }
                if (trade.Side == Side.None)
                {
                    SendLogMessage("Trade Error. No Side. " + trade.SecurityNameCode, LogMessageType.Error);
                    continue;
                }
            }
        }

// portfolios validation
// валидация портфелей

        private DateTime _lastTimeChangePortfolio = DateTime.MinValue;

        private ConcurrentQueue<Portfolio> _portfolios = new ConcurrentQueue<Portfolio>(); 

        private void CheckPortfolio()
        {
            while (true)
            {
                Thread.Sleep(1000);

                if (_lastTimeExecuteOrder != DateTime.MinValue &&
                    _lastTimeExecuteOrder.AddSeconds(20) > DateTime.Now &&
                    _lastTimeExecuteOrder.AddSeconds(10) < DateTime.Now&&
                    _lastTimeChangePortfolio.AddSeconds(10) < _lastTimeExecuteOrder)
                { // order executed, the portfolio has not changed / ордер исполнился, портфель не поменялся.
                    SendLogMessage("Portfolio Error. No reaction or change position after execute order. ", LogMessageType.Error);
                }

                if (_portfolios.IsEmpty)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                Portfolio portfolio = null;
                _portfolios.TryDequeue(out  portfolio);

                if (portfolio == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                if (string.IsNullOrEmpty(portfolio.Number))
                {
                    SendLogMessage("Portfolio Error. No Number. ", LogMessageType.Error);
                    continue;
                }

                _lastTimeChangePortfolio = DateTime.Now;

                if ((portfolio.ValueBegin != 0 || portfolio.ValueBlocked != 0) 
                    &&
                    portfolio.ValueCurrent == 0)
                {
                    SendLogMessage("Portfolio Error. Portfolio ValueCurrent have zero value. But ValueBegin or BalueBlocked dont zero " + portfolio.Number, LogMessageType.Error);
                    continue;
                }
            }
        }

        #endregion

        // logging
        // логирование

        /// <summary>
        /// add new message to log
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
    }
}
