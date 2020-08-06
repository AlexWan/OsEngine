/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Logging;

namespace OsEngine.Market.Servers
{
    /// <summary>
    /// standard server interface to connect to the exchange
    /// стандартный интерфейс сервера для подключения к бирже
    /// </summary>
    public interface IServer
    {

        /// <summary>
        /// take server type
        /// взять тип сервера. 
        /// </summary>
        /// <returns></returns>
        ServerType ServerType { get;}

//service
//сервис

        /// <summary>
        /// show settings
        /// показать настройки
        /// </summary>
        void ShowDialog();

// connect/disconnect
// подключение/отключение
        /// <summary>
        /// start server. Connect to the trading system
        /// запустить сервер. Подключиться к торговой системе
        /// </summary>
        void StartServer();

        /// <summary>
        /// stop server
        /// остановить сервер
        /// </summary>
        void StopServer();

// connect status
// статус соединения
        /// <summary>
        /// take server status
        /// взять статус сервера
        /// </summary>
        ServerConnectStatus ServerStatus { get; }

        /// <summary>
        /// connection status changed
        /// изменилось состояние соединения
        /// </summary>
        event Action<string> ConnectStatusChangeEvent;

// server time
// время сервера
        /// <summary>
        /// server time
        /// время сервера
        /// </summary>
        DateTime ServerTime { get; }

        /// <summary>
        /// server time changed
        /// изменилось время сервера
        /// </summary>
        event Action<DateTime> TimeServerChangeEvent;

        /// <summary>
        /// server time of last starting
        /// время последнего старта сервера
        /// </summary>
        DateTime LastStartServerTime { get; }

        // portfolios
        // портфели
        /// <summary>
        /// take all portfolios
        /// взять все портфели
        /// </summary>
        List<Portfolio> Portfolios { get;}

        /// <summary>
        /// take portfolio by number
        /// взять портфель по номеру
        /// </summary>
        Portfolio GetPortfolioForName(string name);

        /// <summary>
        /// portfolios changed
        /// изменились портфели
        /// </summary>
        event Action<List<Portfolio>> PortfoliosChangeEvent;

// securities
// инструменты
        /// <summary>
        /// take securities
        /// взять инструменты
        /// </summary>
        List<Security> Securities { get; }

        /// <summary>
        /// take the security by the short name
        /// взять инструмент по короткому имени инструмента
        /// </summary>
        Security GetSecurityForName(string name);

        /// <summary>
        /// securities changed
        /// изменились инструменты
        /// </summary>
        event Action<List<Security>> SecuritiesChangeEvent;

// data subscribetion
// Подпись на данные

        /// <summary>
        /// start downloading security
        /// начать выкачивать данный иснтрументн
        /// </summary>
        /// <param name="namePaper"> security name / название инструмента </param>
        /// <param name="timeFrameBuilder"> needed for series timeframe / объект несущий в себе данные о ТаймФрейме нужном для серии </param>
        /// <returns> if everything is successfully completed, returns candle generated object / в случае успешного запуска возвращает CandleSeries, объект генерирующий свечи</returns>
        CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder);

        /// <summary>
        /// stop candle downloading
        /// остановить скачивание свечек
        /// </summary>
        /// <param name="series"> candle seria that needs to stop / серия свечек которую надо остановить </param>
        void StopThisSecurity(CandleSeries series);

        /// <summary>
        /// need to reconnect data from server
        /// необходимо перезаказать данные у сервера
        /// </summary>
        event Action NeadToReconnectEvent;

// request data downloading
// Запрос данных на выкачивание

        /// <summary>
        /// start data downloading on the instrument
        /// Начать выгрузку данных по инструменту
        /// </summary>
        CandleSeries GetCandleDataToSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime, bool neadToUpdate);

        /// <summary>
        /// take ticks instrument data for a certain period
        /// взять тиковые данные по инструменту за определённый период
        /// </summary>
        bool GetTickDataToSecurity(string namePaper, DateTime startTime, DateTime endTime, DateTime actualTime,
            bool neadToUpdete);

// candles
// свечи

        /// <summary>
        /// new candles
        /// новые свечи
        /// </summary>
        event Action<CandleSeries> NewCandleIncomeEvent;

// depths
// стакан
        /// <summary>
        /// best bid / ask by instrument changed
        /// изменился лучший бид / аск по инструменту
        /// </summary>
        event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        event Action<MarketDepth> NewMarketDepthEvent;

// ticks
// тики
        /// <summary>
        /// take all trades on the instrument that have in the system
        /// взять все сделки по инстурументу имеющиеся в системе
        /// </summary>
        /// <param name="security"> instrument / инстурмент </param>
        /// <returns> trades / сделки </returns>
        List<Trade> GetAllTradesToSecurity(Security security);

        /// <summary>
        /// all ticks from server
        /// все тики имеющиеся у сервера
        /// </summary>
        List<Trade>[] AllTrades { get; }

        /// <summary>
        /// new tick
        /// новый тик
        /// </summary>
        event Action<List<Trade>> NewTradeEvent;

// my new trade
// новая моя сделка

        /// <summary>
        /// take my trades
        /// взять мои сделки
        /// </summary>
        List<MyTrade> MyTrades { get; }

        /// <summary>
        /// my trade updated
        /// изменилась моя сделка
        /// </summary>
        event Action<MyTrade> NewMyTradeEvent;

// work with orders
// работа с ордерами

        /// <summary>
        /// send order to execute in the trading system
        /// выслать ордер на исполнение в торговую систему
        /// </summary>
        /// <param name="order"> order / ордер </param>
        void ExecuteOrder(Order order);

        /// <summary>
        /// cancel order from trading system
        /// отозвать ордер из торговой системы
        /// </summary>
        /// <param name="order"> order / ордер </param>
        void CancelOrder(Order order);

        /// <summary>
        /// order changed
        /// изменился ордер
        /// </summary>
        event Action<Order> NewOrderIncomeEvent;

// log messages
// сообщения для лога

        event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// server status
    /// статус сервера
    /// </summary>
    public enum ServerConnectStatus
    {
        /// <summary>
        /// connected
        /// подключен
        /// </summary>
        Connect,

        /// <summary>
        /// disconnected
        /// отключен
        /// </summary>
        Disconnect,
    }
}
