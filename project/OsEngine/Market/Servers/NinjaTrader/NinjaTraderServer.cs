using System;
using System.Collections.Generic;
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

        public void Connect()
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

        public void CancelOrder(Order order)
        {
            _client.CancelOrder(order);
        }

        public void Subscrible(Security security)
        {
            _client.SubscribleTradesAndDepths(security);
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
