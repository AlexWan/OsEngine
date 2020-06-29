using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Plaza.Internal;

namespace OsEngine.Market.Servers.Plaza
{
    public class PlazaServer : AServer
    {
        public PlazaServer()
        {
            PlazaServerRealization realization = new PlazaServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "11111111");
        }
    }

    public class PlazaServerRealization : IServerRealization
    {
        public PlazaServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public ServerType ServerType => ServerType.Plaza;

        public ServerConnectStatus ServerStatus { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

        private PlazaClient _plazaController;

        public void Connect()
        {
            if (_plazaController == null)
            {
                _plazaController = new PlazaClient(((ServerParameterString)ServerParameters[0]).Value);
                _plazaController.LogMessageEvent += PlazaControllerOnLogMessageEvent;
                _plazaController.ConnectStatusChangeEvent += PlazaControllerOnConnectStatusChangeEvent;
                _plazaController.MarketDepthChangeEvent += PlazaControllerOnMarketDepthChangeEvent;
                _plazaController.NewMyTradeEvent += PlazaControllerOnNewMyTradeEvent;
                _plazaController.NewMyOrderEvent += PlazaControllerOnNewMyOrderEvent;
                _plazaController.UpdatePortfolio += PlazaControllerOnUpdatePortfolio;
                _plazaController.UpdatePosition += PlazaControllerOnUpdatePosition;
                _plazaController.UpdateSecurity += PlazaControllerOnUpdateSecurity;
                _plazaController.NewTradeEvent += PlazaControllerOnNewTradeEvent;
            }
            _plazaController.Start();
        }

        public void Dispose()
        {
            if (_plazaController != null)
            {
                _plazaController.Stop();
            }

            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public void GetSecurities()
        {
            
        }

        public void GetPortfolios()
        {
            
        }

        public void SendOrder(Order order)
        {
            _plazaController.ExecuteOrder(order);
        }

        public void CancelOrder(Order order)
        {
            _plazaController.CancelOrder(order);
        }

        public void Subscrible(Security security)
        {
            _plazaController.StartMarketDepth(security);
        }

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
            
        }

        // parsing incoming data
        // разбор входящих данных

        private void PlazaControllerOnNewTradeEvent(Trade trade, bool isOnLine)
        {
            if (_securities == null)
            {
                return;
            }
            Security security = _securities.Find(s => s.NameId == trade.SecurityNameCode);

            if (security == null)
            {
                return;
            }

            trade.SecurityNameCode = security.Name;

            if (NewTradesEvent != null)
            {
                NewTradesEvent(trade);
            }
        }

        private List<Security> _securities;

        private void PlazaControllerOnUpdateSecurity(Security security)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            if (_securities.Find(security1 => security1.NameId == security.NameId) == null)
            {
                _securities.Add(security);

                if (SecurityEvent != null)
                {
                    SecurityEvent(_securities);
                }
            }
        }

        private List<Portfolio> _portfolios;

        private void PlazaControllerOnUpdatePortfolio(Portfolio portfolio)
        {
            if (_portfolios == null)
            {
                _portfolios = new List<Portfolio>();
            }

            if (_portfolios.Find(portfolio1 => portfolio1.Number == portfolio.Number) == null)
            {
                _portfolios.Add(portfolio);

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
        }

        private object _lockerUpdatePosition = new object();

        private void PlazaControllerOnUpdatePosition(PositionOnBoard positionOnBoard)
        {
            lock (_lockerUpdatePosition)
            {
                // write in the security name right description, becaus before in this line some ID / забиваем в название инструмента правдивое описание, т.к. до этого в этой строке некий ID

                Security security = null;

                if (_securities != null)
                {
                    security = _securities.Find(security1 =>
                        security1.NameId == positionOnBoard.SecurityNameCode
                        || security1.Name == positionOnBoard.SecurityNameCode);
                }

                if (security == null)
                {
                    PositionOnBoardSander sender = new PositionOnBoardSander();
                    sender.PositionOnBoard = positionOnBoard;
                    sender.TimeSendPortfolio += PlazaControllerOnUpdatePosition;

                    Thread worker = new Thread(sender.Go);
                    worker.CurrentCulture = new CultureInfo("ru-RU");
                    worker.IsBackground = true;
                    worker.Start();
                    return;
                }

                positionOnBoard.SecurityNameCode = security.Name;
                Portfolio myPortfolio = null;
                if (_portfolios != null)
                {
                    myPortfolio = _portfolios.Find(portfolio => portfolio.Number == positionOnBoard.PortfolioName);
                }

                if (myPortfolio == null)
                {
                    PositionOnBoardSander sender = new PositionOnBoardSander();
                    sender.PositionOnBoard = positionOnBoard;
                    sender.TimeSendPortfolio += PlazaControllerOnUpdatePosition;

                    Thread worker = new Thread(sender.Go);
                    worker.CurrentCulture = new CultureInfo("ru-RU");
                    worker.IsBackground = true;
                    worker.Start();
                    return;
                }

                myPortfolio.SetNewPosition(positionOnBoard);

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
        }

        private List<Order> _orders = new List<Order>();

        private object _lockerUpdateOrders = new object();

        private void PlazaControllerOnNewMyOrderEvent(Order order)
        {
            lock (_lockerUpdateOrders)
            {
                if (_orders != null)
                {
                    Order oldOrder = _orders.Find(order1 => order1.NumberUser == order.NumberUser);

                    if (oldOrder != null && order.Price != 0)
                    {
                        order.Volume = oldOrder.Volume;
                        order.VolumeExecute = oldOrder.Volume - order.VolumeExecute;
                    }
                }

                if (_securities == null)
                {
                    if (order.State != OrderStateType.Activ)
                    {
                        return;
                    }
                    OrderSender sender = new OrderSender();
                    sender.Order = order;
                    sender.UpdeteOrderEvent += PlazaControllerOnNewMyOrderEvent;
                    Thread worker = new Thread(sender.Sand);
                    worker.CurrentCulture = new CultureInfo("ru-RU");
                    worker.IsBackground = true;
                    worker.Start();
                    return;
                }

                Security security = _securities.Find(security1 => security1.NameId == order.SecurityNameCode);

                if (order.SecurityNameCode != null)
                {
                    if (security == null)
                    {
                        if (order.State != OrderStateType.Activ)
                        {
                            return;
                        }
                        OrderSender sender = new OrderSender();
                        sender.Order = order;
                        sender.UpdeteOrderEvent += PlazaControllerOnNewMyOrderEvent;
                        Thread worker = new Thread(sender.Sand);
                        worker.CurrentCulture = new CultureInfo("ru-RU");
                        worker.IsBackground = true;
                        worker.Start();
                        return;
                    }

                    order.SecurityNameCode = security.Name;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }
            }
        }

        private void PlazaControllerOnNewMyTradeEvent(MyTrade myTrade)
        {
            if (_securities == null ||
                _securities.Count == 0)
            {
                return;
            }
            Security security = _securities.Find(security1 => security1.NameId == myTrade.SecurityNameCode);

            if (security == null)
            {
                return;
            }

            myTrade.SecurityNameCode = security.Name;

            if (MyTradeEvent != null)
            {
                MyTradeEvent(myTrade);
            }
        }

        private void PlazaControllerOnMarketDepthChangeEvent(MarketDepth depth)
        {
            depth.Time = DateTime.Now;

            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(depth);
            }
        }

        private void PlazaControllerOnConnectStatusChangeEvent(ServerConnectStatus status)
        {
            ServerStatus = status;

            if (ServerStatus == ServerConnectStatus.Connect &&
                ConnectEvent != null)
            {
                ConnectEvent();
            }

            if (ServerStatus == ServerConnectStatus.Disconnect &&
                DisconnectEvent != null)
            {
                DisconnectEvent();
            }
        }

        private void PlazaControllerOnLogMessageEvent(string message)
        {
            SendLogMessage(message, LogMessageType.System);
        }

        // outgoing events / исходящие события

        /// <summary>
        /// called when order has changed
        /// вызывается когда изменился ордер
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// called when my trade has changed
        /// вызывается когда изменился мой трейд
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// appeared new portfolios
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
