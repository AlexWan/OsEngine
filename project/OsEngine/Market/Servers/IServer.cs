/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Logging;

namespace OsEngine.Market.Servers
{
    /// <summary>
    /// стандартный интерфейс сервера для подключения к бирже
    /// </summary>
    public interface IServer
    {

        /// <summary>
        /// взять тип сервера. 
        /// </summary>
        /// <returns></returns>
        ServerType ServerType { get;}

//сервис

        /// <summary>
        /// показать настройки
        /// </summary>
        void ShowDialog();

// подключение/отключение
        /// <summary>
        /// запустить сервер. Подключиться к торговой системе
        /// </summary>
        void StartServer();

        /// <summary>
        /// остановить сервер
        /// </summary>
        void StopServer();

// статус соединения
        /// <summary>
        /// взять статус сервера
        /// </summary>
        ServerConnectStatus ServerStatus { get; }

        /// <summary>
        /// изменилось состояние соединения
        /// </summary>
        event Action<string> ConnectStatusChangeEvent;

// время сервера
        /// <summary>
        /// время сервера
        /// </summary>
        DateTime ServerTime { get; }

        /// <summary>
        /// изменилось время сервера
        /// </summary>
        event Action<DateTime> TimeServerChangeEvent;

// портфели
        /// <summary>
        /// взять все портфели
        /// </summary>
        List<Portfolio> Portfolios { get;}

        /// <summary>
        /// взять портфель по номеру
        /// </summary>
        Portfolio GetPortfolioForName(string name);

        /// <summary>
        /// изменились портфели
        /// </summary>
        event Action<List<Portfolio>> PortfoliosChangeEvent;

// инструменты
        /// <summary>
        /// взять инструменты
        /// </summary>
        List<Security> Securities { get; }

        /// <summary>
        /// взять инструмент по короткому имени инструмента
        /// </summary>
        Security GetSecurityForName(string name);

        /// <summary>
        /// изменились инструменты
        /// </summary>
        event Action<List<Security>> SecuritiesChangeEvent;

// Подпись на данные

        /// <summary>
        /// начать выкачивать данный иснтрументн
        /// </summary>
        /// <param name="namePaper"> название инструмента</param>
        /// <param name="timeFrameBuilder">объект несущий в себе данные о ТаймФрейме нужном для серии</param>
        /// <returns>в случае успешного запуска возвращает CandleSeries, объект генерирующий свечи</returns>
        CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder);

        /// <summary>
        /// остановить скачивание свечек
        /// </summary>
        /// <param name="series"> серия свечек которую надо остановить</param>
        void StopThisSecurity(CandleSeries series);

        /// <summary>
        /// необходимо перезаказать данные у сервера
        /// </summary>
        event Action NeadToReconnectEvent;

// свечи

        /// <summary>
        /// новые свечи
        /// </summary>
        event Action<CandleSeries> NewCandleIncomeEvent;

// стакан
        /// <summary>
        /// изменился лучший бид / аск по инструменту
        /// </summary>
        event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        event Action<MarketDepth> NewMarketDepthEvent;

// тики
        /// <summary>
        /// взять все сделки по инстурументу имеющиеся в системе
        /// </summary>
        /// <param name="security"> инстурмент</param>
        /// <returns>сделки</returns>
        List<Trade> GetAllTradesToSecurity(Security security);

        /// <summary>
        /// все тики имеющиеся у сервера
        /// </summary>
        List<Trade>[] AllTrades { get; }

        /// <summary>
        /// новый тик
        /// </summary>
        event Action<List<Trade>> NewTradeEvent;

// новая моя сделка

        /// <summary>
        /// взять мои сделки
        /// </summary>
        List<MyTrade> MyTrades { get; }

        /// <summary>
        /// изменилась моя сделка
        /// </summary>
        event Action<MyTrade> NewMyTradeEvent;

// работа с ордерами

        /// <summary>
        /// выслать ордер на исполнение в торговую систему
        /// </summary>
        /// <param name="order">ордер</param>
        void ExecuteOrder(Order order);

        /// <summary>
        /// отозвать ордер из торговой системы
        /// </summary>
        /// <param name="order">ордер</param>
        void CanselOrder(Order order);

        /// <summary>
        /// изменился ордер
        /// </summary>
        event Action<Order> NewOrderIncomeEvent;

// сообщения для лога

        event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// статус сервера
    /// </summary>
    public enum ServerConnectStatus
    {
        /// <summary>
        /// подключен
        /// </summary>
        Connect,

        /// <summary>
        /// отключен
        /// </summary>
        Disconnect,
    }
}
