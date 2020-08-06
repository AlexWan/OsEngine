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
        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        ServerType ServerType { get;}

        /// <summary>
        /// server state
        /// состояние сервера
        /// </summary>
        ServerConnectStatus ServerStatus { get; set; }

        /// <summary>
        /// strategy parameters
        /// параметры стратегии
        /// </summary>
        List<IServerParameter> ServerParameters { get; set; }

        /// <summary>
        /// server time
        /// время сервера
        /// </summary>
        DateTime ServerTime { get; set; }

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
        /// request security
        /// запросить бумаги
        /// </summary>
        void GetSecurities();

        /// <summary>
        /// request portfolios
        /// запросить портфели
        /// </summary>
        void GetPortfolios();

        /// <summary>
        /// place order
        /// исполнить ордер
        /// </summary>
        void SendOrder(Order order);

        /// <summary>
        /// cancel order
        /// отозвать ордер
        /// </summary>
        void CancelOrder(Order order);

        /// <summary>
        /// subscribe to candles
        /// подписаться на свечи
        /// </summary>
        void Subscrible(Security security);

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

        /// <summary>
        /// take the current orders state
        /// взять текущие состояния ордеров
        /// </summary>
        void GetOrdersState(List<Order> orders);

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

        /// <summary>
        /// portfolios updates
        /// обновились портфели
        /// </summary>
        event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
        /// new securities in the system
        /// новые бумаги в системе
        /// </summary>
        event Action<List<Security>> SecurityEvent;

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

        /// <summary>
        /// send the message
        /// отправляет сообщение
        /// </summary>
        event Action<string, LogMessageType> LogMessageEvent;
    }
}
