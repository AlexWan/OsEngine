using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;


namespace OsEngine.Market.Servers
{
    public class AServerRealization : IServerRealization
    {
        public AServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public virtual ServerType ServerType
        {
            get { throw new NotImplementedException(); }
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

        public virtual void CancelOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public virtual void Connect()
        {
            throw new NotImplementedException();
        }

        public virtual void Dispose()
        {
            throw new NotImplementedException();
        }

        public virtual List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public virtual void GetOrdersState(List<Order> orders)
        {
            throw new NotImplementedException();
        }

        public virtual void GetPortfolios()
        {
            throw new NotImplementedException();
        }

        public virtual void GetSecurities()
        {
            throw new NotImplementedException();
        }

        public virtual List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public virtual void SendOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public virtual void Subscrible(Security security)
        {
            throw new NotImplementedException();
        }

        #region events

        public event Action<List<Security>> SecurityEvent;
        protected void OnSecurityEvent(List<Security> securities)
        {
            SecurityEvent?.Invoke(securities);
        }

        public event Action<MarketDepth> MarketDepthEvent;
        protected void OnMarketDepthEvent(MarketDepth marketDepth)
        {
            MarketDepthEvent?.Invoke(marketDepth);
        }

        public event Action<Trade> NewTradesEvent;
        protected void OnTradeEvent(Trade trade)
        {
            NewTradesEvent?.Invoke(trade);
        }

        public event Action<List<Portfolio>> PortfolioEvent;
        protected void OnPortfolioEvent(List<Portfolio> portfolios)
        {
            PortfolioEvent?.Invoke(portfolios);
        }

        public event Action<MyTrade> MyTradeEvent;
        protected void OnMyTradeEvent(MyTrade myTrade)
        {
            MyTradeEvent?.Invoke(myTrade);
        }

        public event Action<Order> MyOrderEvent;
        protected void OnOrderEvent(Order order)
        {
            MyOrderEvent?.Invoke(order);
        }

        public virtual event Action ConnectEvent;
        protected void OnConnectEvent()
        {
            ServerStatus = ServerConnectStatus.Connect;
            ConnectEvent?.Invoke();
        }

        public event Action DisconnectEvent;
        protected void OnDisconnectEvent()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            DisconnectEvent?.Invoke();
        }

        #endregion

        // log messages
        // сообщения для лога

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        protected void SendLogMessage(string message, LogMessageType type)
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
    }
}
