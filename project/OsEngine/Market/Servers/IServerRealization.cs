using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers
{
    /// <summary>
    /// реализация подключения для AServer
    /// </summary>
    public interface IServerRealization
    {
        /// <summary>
        /// тип сервера
        /// </summary>
        ServerType ServerType { get;}

        /// <summary>
        /// состояние сервера
        /// </summary>
        ServerConnectStatus ServerStatus { get; set; }

        /// <summary>
        /// параметры стратегии
        /// </summary>
        List<IServerParameter> ServerParameters { get; set; }

            /// <summary>
        /// время сервера
        /// </summary>
        DateTime ServerTime { get; set; }

        /// <summary>
        /// запрос подключения к источнику.
        /// гарантированно вызывается не чаще чем в 60 секунд
        /// </summary>
        void Connect();

        /// <summary>
        /// освободить ресурсы АПИ
        /// </summary>
        void Dispose();

        /// <summary>
        /// запросить бумаги
        /// </summary>
        void GetSecurities();

        /// <summary>
        /// запросить портфели
        /// </summary>
        void GetPortfolios();

        /// <summary>
        /// исполнить ордер
        /// </summary>
        void SendOrder(Order order);

        /// <summary>
        /// отозвать ордер
        /// </summary>
        void CanselOrder(Order order);

        /// <summary>
        /// подписаться на свечи
        /// </summary>
        void Subscrible(Security security);

        /// <summary>
        /// взять историю свечей за период
        /// </summary>
        List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime);

        /// <summary>
        /// взять тиковые данные за период
        /// </summary>
        List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime);

        /// <summary>
        /// взять текущие состояния ордеров
        /// </summary>
        void GetOrdersState(List<Order> orders);

        /// <summary>
        /// новые мои ордера
        /// </summary>
        event Action<Order> MyOrderEvent;

        /// <summary>
        /// новые мои сделки
        /// </summary>
        event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// обновились портфели
        /// </summary>
        event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
        /// новые бумаги в системе
        /// </summary>
        event Action<List<Security>> SecurityEvent;

        /// <summary>
        /// обновился стакан
        /// </summary>
        event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
        /// обновились тики
        /// </summary>
        event Action<Trade> NewTradesEvent;

        /// <summary>
        /// соединение с API установлено
        /// </summary>
        event Action ConnectEvent;

        /// <summary>
        /// соединение с API разорвано
        /// </summary>
        event Action DisconnectEvent;

        /// <summary>
        /// отправляет сообщение
        /// </summary>
        event Action<string, LogMessageType> LogMessageEvent;
    }
}
