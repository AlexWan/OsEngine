using System;
using System.Collections.Generic;
using System.Net;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.NinjaTrader
{
    /// <summary>
	/// Ninja server
    /// сервер Ninja
    /// </summary>
    public class NinjaTraderServer: AServer
    {
        public NinjaTraderServer()
        {
            ServerRealization = new NinjaTraderServerRealization();

            CreateParameterString("ServerAddress", "localhost");
            CreateParameterPassword("Port", "11000");
        }
    }
    
    public class NinjaTraderServerRealization : IServerRealization
    {
        public NinjaTraderServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public ServerType ServerType => ServerType.NinjaTrader;

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

 // requests
 // запросы

        private NinjaTraderClient _client;

        public void Connect(WebProxy proxy)
        {
            if (_client == null)
            {
                _client = new NinjaTraderClient(((ServerParameterPassword)ServerParameters[1]).Value, ((ServerParameterString)ServerParameters[0]).Value);
                _client.Connected += ClientOnConnected;
                _client.UpdateSecuritiesEvent += ClientOnUpdateSecuritiesEvent;
                _client.Disconnected += ClientOnDisconnected;
                _client.UpdatePortfolio += ClientOnUpdatePortfolio;
                _client.UpdateMarketDepth += ClientOnUpdateMarketDepth;
                _client.NewTradesEvent += ClientOnNewTradesEvent;
                _client.MyTradeEvent += ClientOnMyTradeEvent;
                _client.MyOrderEvent += ClientOnMyOrderEvent;
                _client.LogMessageEvent += ClientOnLogMessageEvent;
                _client.Connect();
            }
        }

        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();

                _client.Connected -= ClientOnConnected;
                _client.UpdateSecuritiesEvent -= ClientOnUpdateSecuritiesEvent;
                _client.Disconnected -= ClientOnDisconnected;
                _client.UpdatePortfolio -= ClientOnUpdatePortfolio;
                _client.UpdateMarketDepth -= ClientOnUpdateMarketDepth;
                _client.NewTradesEvent -= ClientOnNewTradesEvent;
                _client.MyTradeEvent -= ClientOnMyTradeEvent;
                _client.MyOrderEvent -= ClientOnMyOrderEvent;
                _client.LogMessageEvent -= ClientOnLogMessageEvent;
            }

            _client = null;
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public void GetSecurities()
        {
            _client.GetSecurities();
        }

        public void GetPortfolios()
        {
           
            _client.GetPortfolios();
        }

        public void SendOrder(Order order)
        {
            _client.ExecuteOrder(order);
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public bool CancelOrder(Order order)
        {
            _client.CancelOrder(order);

            return true;
        }

        public void CancelAllOrders()
        {

        }

        public void GetAllActivOrders()
        {

        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            return OrderStateType.None;
        }

        public void Subscribe(Security security)
        {
            _client.SubscribeTradesAndDepths(security);
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

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        // parsing incoming data
        // разбор входящих данных

        private void ClientOnLogMessageEvent(string message, LogMessageType type)
        {
            SendLogMessage(message, type);
        }

        private void ClientOnMyOrderEvent(Order order)
        {
            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        private void ClientOnMyTradeEvent(MyTrade myTrade)
        {
            if (MyTradeEvent != null)
            {
                MyTradeEvent(myTrade);
            }
        }

        private void ClientOnNewTradesEvent(Trade trade)
        {
            if (NewTradesEvent != null)
            {
                NewTradesEvent(trade);
            }
        }

        private void ClientOnUpdateMarketDepth(MarketDepth marketDepth)
        {
            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(marketDepth);
            }
        }

        private void ClientOnUpdatePortfolio(List<Portfolio> portfolios)
        {
            if (PortfolioEvent != null)
            {
                PortfolioEvent(portfolios);
            }
        }

        private void ClientOnDisconnected()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
        }

        private void ClientOnUpdateSecuritiesEvent(List<Security> securities)
        {
            if (SecurityEvent != null)
            {
                SecurityEvent(securities);
            }
        }

        private void ClientOnConnected()
        {
            ServerStatus = ServerConnectStatus.Connect;
            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
        }

		// outgoing events
        // исходящие события

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

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

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

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        public void SetLeverage(Security security, decimal leverage) { }
    }
}
