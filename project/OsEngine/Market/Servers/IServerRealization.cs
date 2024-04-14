/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
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
        /// server type
        /// тип сервера
        /// </summary>
        ServerType ServerType { get; }

        /// <summary>
        /// server state
        /// состояние сервера
        /// </summary>
        ServerConnectStatus ServerStatus { get; set; }

        /// <summary>
        /// server time
        /// время сервера
        /// </summary>
        DateTime ServerTime { get; set; }

        /// <summary>
        /// strategy parameters
        /// параметры стратегии
        /// </summary>
        List<IServerParameter> ServerParameters { get; set; }

        /// <summary>
        /// request to connect to the source. guaranteed to be called no more than 60 seconds
        /// запрос подключения к источнику. гарантированно вызывается не чаще чем в 60 секунд
        /// </summary>
        void Connect();

        /// <summary>
        /// dispose resources of API
        /// освободить ресурсы АПИ
        /// </summary>
        void Dispose();

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        event Action ConnectEvent;

        /// <summary>
        /// API connection broke
        /// соединение с API разорвано
        /// </summary>
        event Action DisconnectEvent;

        #endregion

        #region Securities

        /// <summary>
        /// request security
        /// запросить бумаги
        /// </summary>
        void GetSecurities();

        /// <summary>
        /// new securities in the system
        /// новые бумаги в системе
        /// </summary>
        event Action<List<Security>> SecurityEvent;

        #endregion

        #region Portfolios

        /// <summary>
        /// request portfolios
        /// запросить портфели
        /// </summary>
        void GetPortfolios();

        /// <summary>
        /// portfolios updates
        /// обновились портфели
        /// </summary>
        event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region Security subscrible

        /// <summary>
        /// subscribe to trades and market depth
        /// подписаться на трейды и стаканы
        /// </summary>
        void Subscrible(Security security);

        /// <summary>
        /// depth updated
        /// обновился стакан
        /// </summary>
        event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
        /// ticks updated
        /// обновились тики
        /// </summary>
        event Action<Trade> NewTradesEvent;

        #endregion

        #region Data upload

        /// <summary>
        /// Интерфейс для получения последний свечек по инструменту. Используется для активации серий свечей в боевых торгах
        /// Interface for getting the last candlesticks for a security. Used to activate candlestick series in live trades
        /// </summary>
        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount);

        /// <summary>
        /// take candles history for period
        /// взять историю свечей за период
        /// </summary>
        List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime);

        /// <summary>
        /// take ticks data for period
        /// взять тиковые данные за период
        /// </summary>
        List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime);

        #endregion

        #region Work with orders

        /// <summary>
        /// place order
        /// исполнить ордер
        /// </summary>
        void SendOrder(Order order);

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        void ChangeOrderPrice(Order order, decimal newPrice);

        /// <summary>
        /// cancel order
        /// отозвать ордер
        /// </summary>
        void CancelOrder(Order order);

        /// <summary>
        /// cancel all orders from trading system
        /// отозвать все ордера из торговой системы
        /// </summary>
        void CancelAllOrders();

        /// <summary>
        /// cancel all orders from trading system to security
        /// отозвать все ордера из торговой системы по названию инструмента
        /// </summary>
        void CancelAllOrdersToSecurity(Security security);

        /// <summary>
        /// Query list of orders that are currently in the market
        /// </summary>
        void GetAllActivOrders();

        /// <summary>
        /// Query order status
        /// </summary>
        void GetOrderStatus(Order order);

        /// <summary>
        /// новые мои ордера
        /// my new orders
        /// </summary>
        event Action<Order> MyOrderEvent;

        /// <summary>
        /// my new trades
        /// новые мои сделки
        /// </summary>
        event Action<MyTrade> MyTradeEvent;

        #endregion

        #region Log messages

        /// <summary>
        /// send the message
        /// отправляет сообщение
        /// </summary>
        event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}