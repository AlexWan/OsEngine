/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Net;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers
{
    /// <summary>
    /// connection implementation for AServer
    /// реализация подключения для AServer
    /// </summary>
    public interface IServerRealization
    {
        #region  Service, Status, Connection

        /// <summary>
        /// Server type
        /// </summary>
        ServerType ServerType { get; }

        /// <summary>
        /// Server state
        /// </summary>
        ServerConnectStatus ServerStatus { get; set; }

        /// <summary>
        /// Server time
        /// </summary>
        DateTime ServerTime { get; set; }

        /// <summary>
        /// Strategy parameters
        /// </summary>
        List<IServerParameter> ServerParameters { get; set; }

        /// <summary>
        /// Request to connect to the source. guaranteed to be called no more than 60 seconds
        /// </summary>
        void Connect(WebProxy proxy);

        /// <summary>
        /// Dispose resources of API
        /// </summary>
        void Dispose();

        /// <summary>
        /// API connection established
        /// </summary>
        event Action ConnectEvent;

        /// <summary>
        /// API connection broke
        /// </summary>
        event Action DisconnectEvent;

        /// <summary>
        /// Need to re-request order statuses from the connector
        /// </summary>
        event Action ForceCheckOrdersAfterReconnectEvent;

        #endregion

        #region Securities

        /// <summary>
        /// Request security
        /// </summary>
        void GetSecurities();

        /// <summary>
        /// New securities in the system
        /// </summary>
        event Action<List<Security>> SecurityEvent;

        #endregion

        #region Portfolios

        /// <summary>
        /// Request portfolios
        /// </summary>
        void GetPortfolios();

        /// <summary>
        /// Portfolios updates
        /// </summary>
        event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region Security subscribe

        /// <summary>
        /// Subscribe to trades and market depth
        /// </summary>
        void Subscribe(Security security);

        /// <summary>
        /// Unsubscribe from trades and orderbooks
        /// </summary>
        void Unsubscribe(Security security) { } // default empty implementation

        /// <summary>
        /// Subscribe to news
        /// </summary>
        bool SubscribeNews();

        /// <summary>
        /// The news has come out
        /// </summary>
        event Action<News> NewsEvent;

        /// <summary>
        /// Depth updated
        /// </summary>
        event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
        /// Ticks updated
        /// </summary>
        event Action<Trade> NewTradesEvent;

        /// <summary>
        /// Funding data
        /// </summary>
        event Action<Funding> FundingUpdateEvent;

        /// <summary>
        /// Volumes 24h data
        /// </summary>
        event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region Data upload

        /// <summary>
        /// Interface for getting the last candlesticks for a security. Used to activate candlestick series in live trades
        /// </summary>
        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount);

        /// <summary>
        /// Take candles history for period
        /// </summary>
        List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime);

        /// <summary>
        /// Take ticks data for period
        /// </summary>
        List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime);

        #endregion

        #region Work with orders

        /// <summary>
        /// Place order
        /// </summary>
        void SendOrder(Order order);

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        void ChangeOrderPrice(Order order, decimal newPrice);

        /// <summary>
        /// Cancel order
        /// </summary>
        bool CancelOrder(Order order);

        /// <summary>
        /// Cancel all orders from trading system
        /// </summary>
        void CancelAllOrders();

        /// <summary>
        /// Cancel all orders from trading system to security
        /// </summary>
        void CancelAllOrdersToSecurity(Security security);

        /// <summary>
        /// Query list of orders that are currently in the market
        /// </summary>
        void GetAllActivOrders();

        /// <summary>
        /// Query order status
        /// </summary>
        OrderStateType GetOrderStatus(Order order);

        /// <summary>
        /// Returns a list of active orders. Starting from the startIndex order and up to count
        /// </summary>
        /// <param name="startIndex">index 0 - the newest orders </param>
        /// <param name="count">number of orders in the request. Maximum 100</param>
        List<Order> GetActiveOrders(int startIndex, int count);

        /// <summary>
        /// Returns a list of historical orders. Starting from the startIndex order and up to count
        /// </summary>
        /// <param name="startIndex">index 0 - the newest orders </param>
        /// <param name="count">number of orders in the request. Maximum 100</param>
        List<Order> GetHistoricalOrders(int startIndex, int count);

        /// <summary>
        /// My new orders event
        /// </summary>
        event Action<Order> MyOrderEvent;

        /// <summary>
        /// My new trades event
        /// </summary>
        event Action<MyTrade> MyTradeEvent;

        #endregion

        #region Log messages

        /// <summary>
        /// Send log message
        /// </summary>
        event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region AdditionalMarketData

        /// <summary>
        /// Additional market data
        /// </summary>
        event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        #endregion
    }
}