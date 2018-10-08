/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Alerts;
using OsEngine.Charts;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels.Tab.Internal;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// стратегия. СуперКонтроллер с логикой работы робота
    /// </summary>
    public class BotTabSimple : IIBotTab
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public BotTabSimple(string name)
        {
            TabName = name;

            try
            {
                _connector = new Connector(TabName);
                _connector.OrderChangeEvent += _connector_OrderChangeEvent;
                _connector.MyTradeEvent += _connector_MyTradeEvent;
                _connector.BestBidAskChangeEvent += _connector_BestBidAskChangeEvent;
                _connector.GlassChangeEvent += _connector_GlassChangeEvent;
                _connector.TimeChangeEvent += StrategOneSecurity_TimeServerChangeEvent;
                _connector.NewCandlesChangeEvent += LogicToEndCandle;
                _connector.LastCandlesChangeEvent += LogicToUpdateLastCandle;
                _connector.TickChangeEvent += _connector_TickChangeEvent;
                _connector.LogMessageEvent += SetNewLogMessage;
                _connector.ConnectorStartedReconnectEvent += _connector_ConnectorStartedReconnectEvent;

                _marketDepthPainter = new MarketDepthPainter(TabName);
                _marketDepthPainter.LogMessageEvent += SetNewLogMessage;

                _journal = new Journal.Journal(TabName);

                _journal.PositionStateChangeEvent += _journal_PositionStateChangeEvent;
                _journal.PositionNetVolumeChangeEvent += _journal_PositionNetVolumeChangeEvent;
                _journal.UserSelectActionEvent += _journal_UserSelectActionEvent;
                _journal.LogMessageEvent += SetNewLogMessage;

                _chartMaster = new ChartMaster(TabName);
                _chartMaster.LogMessageEvent += SetNewLogMessage;
                _chartMaster.SetNewSecurity(_connector.NamePaper, _connector.TimeFrame, _connector.TimeFrameTimeSpan, _connector.PortfolioName, _connector.ServerType);
                _chartMaster.SetPosition(_journal.AllPosition);

                _alerts = new AlertMaster(TabName, _connector, _chartMaster);
                _alerts.LogMessageEvent += SetNewLogMessage;
                _dealCreator = new PositionCreator();

                _manualControl = new BotManualControl(TabName, this);
                _manualControl.LogMessageEvent += SetNewLogMessage;
                _manualControl.DontOpenOrderDetectedEvent += _dealOpeningWatcher_DontOpenOrderDetectedEvent;

                _lastTickIndex = 0;

                _stopsOpener = new List<PositionOpenerToStop>();

                _acebergMaker = new AcebergMaker();
                _acebergMaker.NewOrderNeadToExecute += _acebergMaker_NewOrderNeadToExecute;
                _acebergMaker.NewOrderNeadToCansel += _acebergMaker_NewOrderNeadToCansel;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// коннектор запустил процедуру переподключения
        /// </summary>
        /// <param name="securityName">имя бумаги</param>
        /// <param name="timeFrame">таймфрейм бумаги</param>
        /// <param name="timeFrameSpan">таймфрейм в виде времени</param>
        /// <param name="portfolioName">номер портфеля</param>
        /// <param name="serverType">тип сервера у коннектора</param>
        void _connector_ConnectorStartedReconnectEvent(string securityName, TimeFrame timeFrame, TimeSpan timeFrameSpan, string portfolioName, ServerType serverType)
        {
            if (string.IsNullOrEmpty(securityName)// ||
                //string.IsNullOrEmpty(portfolioName)
                )
            {
                return;
            }

            _chartMaster.SetNewSecurity(securityName, timeFrame,timeFrameSpan, portfolioName, serverType);
        }

// управление

        /// <summary>
        /// начать прорисовку этого робота
        /// </summary> 
        public void StartPaint(WindowsFormsHost hostChart, WindowsFormsHost hostGlass, WindowsFormsHost hostOpenDeals,
                     WindowsFormsHost hostCloseDeals, Rectangle rectangleChart, WindowsFormsHost hostAlerts, TextBox textBoxLimitPrice, Grid gridChartControlPanel)
        {
            try
            {
                _chartMaster.StartPaint(hostChart,rectangleChart);
                _marketDepthPainter.StartPaint(hostGlass, textBoxLimitPrice);
                _journal.StartPaint(hostOpenDeals,hostCloseDeals);
                _alerts.StartPaint(hostAlerts);
                _chartMaster.StartPaintChartControlPanel(gridChartControlPanel);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// остановить прорисовку этого робота
        /// </summary>
        public void StopPaint()
        {
            try
            {
                _chartMaster.StopPaint();
                _marketDepthPainter.StopPaint();
                _journal.StopPaint();
                _alerts.StopPaint();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// уникальное имя робота. Передаётся в конструктор. Участвует в процессе сохранения всех данных связанных с ботом
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// номер вкладки
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
        /// очистить журнал и графики
        /// </summary>
        public void Clear()
        {
            try
            {
                _journal.Clear();
                _chartMaster.Clear();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// удалить робота и все дочерние структуры
        /// </summary>
        public void Delete()
        {
            try
            {
                _journal.Delete();
                _connector.Delete();
                _manualControl.Delete();
                _chartMaster.Delete();
                _alerts.DeleteAll();
                _marketDepthPainter.Delete();

                if (File.Exists(@"Engine\" + TabName + @"SettingsBot.txt"))
                {
                    File.Delete(@"Engine\" + TabName + @"SettingsBot.txt");
                }

                if (DeleteBotEvent != null)
                {
                    DeleteBotEvent(TabNum);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// подключен ли коннектор на скачивание данных
        /// </summary>
        public bool IsConnected
        {
            get { return _connector.IsConnected; }
        }

// работа с логом

        /// <summary>
        /// положить в Бот-лог новое сообщение
        /// </summary>
        public void SetNewLogMessage(string message, LogMessageType messageType)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, messageType);
            }
            else if(messageType == LogMessageType.Error)
            { // если никто на нас не подписан и происходит ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

// менеджмент индикаторов

        /// <summary>
        /// создать индикатор на свечном графике. Начать его прорисовку на графике. Прогрузить его и подписать на обновление.
        /// </summary>
        /// <param name="indicator">индикатор</param>
        /// <param name="nameArea">название области на которую он будет помещён. Главная по умолчанию "Prime"</param>
        /// <returns></returns>
        public IIndicatorCandle CreateCandleIndicator(IIndicatorCandle indicator, string nameArea)
        {
            return _chartMaster.CreateIndicator(indicator, nameArea);
        }

        /// <summary>
        /// удалить индикатор со свечного графика. Удалить область индикатора
        /// </summary>
        /// <param name="indicator">индикатор</param>
        public void DeleteCandleIndicator(IIndicatorCandle indicator)
        {
            _chartMaster.DeleteIndicator(indicator);
        }

        /// <summary>
        /// все доступные индикаторы в системе
        /// </summary>
        public List<IIndicatorCandle> Indicators
        {
            get { return _chartMaster.Indicators; }
        }

// рисование элементов

        /// <summary>
        /// добавить на график пользовательский элемент
        /// </summary>
        public void SetChartElement(IChartElement element)
        {
            _chartMaster.SetChartElement(element);
        }

        /// <summary>
        /// удалить с графика пользовательский элемент
        /// </summary>
        public void DeleteChartElement(IChartElement element)
        {
            _chartMaster.DeleteChartElement(element);
        }

        /// <summary>
        /// удалить все пользовательские элементы с графика
        /// </summary>
        public void DeleteAllChartElement()
        {
            _chartMaster.DeleteAllChartElement();
        }

// закрытые составные части

        /// <summary>
        /// класс отвечающий за подключение вкладки к бирже
        /// </summary>
        public Connector Connector
        {
            get { return _connector; }
        }

        /// <summary>
        /// коннектор к бирже
        /// </summary>
        private Connector _connector;

        /// <summary>
        /// мастер прорисовки чарта
        /// </summary>
        private ChartMaster _chartMaster;

        /// <summary>
        /// класс прорисовывающий движения стакана котировок
        /// </summary>
        private MarketDepthPainter _marketDepthPainter;

        /// <summary>
        /// мастер создания сделок
        /// </summary>
        private PositionCreator _dealCreator;

        /// <summary>
        /// журнал
        /// </summary>
        private Journal.Journal _journal;

        /// <summary>
        /// настройки ручного сопровождения
        /// </summary>
        private BotManualControl _manualControl;

        /// <summary>
        /// мастер Алертов
        /// </summary>
        private AlertMaster _alerts;

// свойства 

        /// <summary>
        /// статус сервера к которому подключена вкладка
        /// </summary>
        public ServerConnectStatus ServerStatus
        {
            get
            {
                if (ServerMaster.GetServers() == null)
                {
                    return ServerConnectStatus.Disconnect;
                }
                IServer myServer = _connector.MyServer;

                if (myServer == null)
                {
                    return ServerConnectStatus.Disconnect;
                }

                return myServer.ServerStatus;
            }
        }

        /// <summary>
        /// инструмент для торговли номер 1
        /// </summary>
        public Security Securiti
        {
            get
            {
                if (_security == null ||
                    _security.Name != _connector.NamePaper)
                {
                    _security = _connector.Security;
                }
                return _security;
            }
            set { _security = value; }
        }
        private Security _security;

        /// <summary>
        /// таймФрейм получаемых данных
        /// </summary>
        public TimeSpan TimeFrame
        {
            get { return _connector.TimeFrameTimeSpan; }
        }

        /// <summary>
        /// счёт для торговли инструмента 1
        /// </summary>
        public Portfolio Portfolio
        {
            get
            {
                if (_portfolio == null)
                {
                    _portfolio = _connector.Portfolio;
                }
                return _portfolio;
            }
            set { _portfolio = value; }
        }
        private Portfolio _portfolio;

        /// <summary>
        /// все позиции принадлежащие боту. Открытые, закрытые и с ошибками
        /// </summary>
        public List<Position> PositionsAll
        {
            get {return _journal.AllPosition; }
        }

        /// <summary>
        /// все открытые, частично открытые и открывающиеся позиции принадлежащие боту. 
        /// </summary>
        public List<Position> PositionsOpenAll
        {
            get {return _journal.OpenPositions; }
        }

        /// <summary>
        /// все ожидающие цены ордера бота
        /// </summary>
        public List<PositionOpenerToStop> PositionOpenerToStopsAll
        {
            get { return _stopsOpener; }
        }

        /// <summary>
        /// все закрытые, с ошибками позиции принадлежащие боту
        /// </summary>
        public List<Position> PositionsCloseAll
        {
            get { return _journal.CloseAllPositions; }
        }

        /// <summary>
        /// последняя открытая позиция
        /// </summary>
        public Position PositionsLast
        {
            get { return _journal.LastPosition; }
        }

        /// <summary>
        /// взять все открытые позиции шорт
        /// </summary>
        public List<Position> PositionOpenShort
        {
            get { return _journal.OpenAllShortPositions; }
        }

        /// <summary>
        /// взять все открытые позиции лонг
        /// </summary>
        public List<Position> PositionOpenLong
        {
            get { return _journal.OpenAllLongPositions; }
        }

        /// <summary>
        /// позиция на бирже по инструменту стратега
        /// </summary>
        public PositionOnBoard PositionsOnBoard
        {
            get
            {
                try
                {
                    if (Portfolio == null || Securiti == null)
                    {
                        return null;
                    }

                    List<PositionOnBoard> positionsOnBoard = Portfolio.GetPositionOnBoard();

                    if (positionsOnBoard != null && positionsOnBoard.Count != 0 &&
                        positionsOnBoard.Find(pose => pose.PortfolioName == Portfolio.Number && pose.SecurityNameCode == Securiti.Name) != null)
                    {
                        return positionsOnBoard.Find(pose => pose.SecurityNameCode == Securiti.Name);
                    }
                }
                catch (Exception error)
                {
                    SetNewLogMessage(error.ToString(), LogMessageType.Error);
                }

                return null;
            }
        }

        /// <summary>
        /// нетто позиция набранная роботом
        /// </summary>
        public decimal VolumeNetto
        {
            get
            {
                try
                {
                    List<Position> openPos = PositionsOpenAll;

                    decimal volume = 0;

                    for (int i = 0; openPos != null && i < openPos.Count; i++)
                    {
                        if (openPos[i].Direction == Side.Buy)
                        {
                            volume += openPos[i].OpenVolume;
                        }
                        else // if (openPos[i].Direction == Side.Sell)
                        {
                            volume -= openPos[i].OpenVolume;
                        }
                    }
                    return volume;
                }
                catch (Exception error)
                {
                    SetNewLogMessage(error.ToString(),LogMessageType.Error);
                    return 0;
                }
            }
        }

        /// <summary>
        /// проверить были ли закрытые позиции на текущем баре
        /// </summary>
        public bool CheckTradeClosedThisBar()
        {
            List<Position> allClosedPositions = PositionsCloseAll;

            if (allClosedPositions == null)
            {
                return false;
            }

            int totalClosedPositions = allClosedPositions.Count;

            if (totalClosedPositions >= 20)
            {
                allClosedPositions = allClosedPositions.GetRange(totalClosedPositions - 20, 20);
            }

            Candle lastCandle = CandlesAll[CandlesAll.Count - 1];

            foreach (Position position in allClosedPositions)
            {
                if (position.TimeClose >= lastCandle.TimeStart)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// все свечи инструмента. И формируемые и завершённые
        /// </summary>
        public List<Candle> CandlesAll
        {
            get
            {
                return _connector.Candles(false);
            }
        }

        /// <summary>
        /// все свечи инструмента. Только завершённые
        /// </summary>
        public List<Candle> CandlesFinishedOnly
        {
            get { return _connector.Candles(true); }
        }

        /// <summary>
        /// все тики по инструменту
        /// </summary>
        public List<Trade> Trades
        {
            get { return _connector.Trades; }
        }

        /// <summary>
        /// текущее время сервера
        /// </summary>
        public DateTime TimeServerCurrent
        {
            get { return _connector.MarketTime; }
        }

        /// <summary>
        /// стакан по инструменту
        /// </summary>
        public MarketDepth MarketDepth { get; set; }

        /// <summary>
        /// лучшая цена продажи инструмента этой вкладки
        /// </summary>
        public decimal PriceBestAsk
        {
            get { return _connector.BestAsk; }
        }

        /// <summary>
        /// лучшая цена покупки инструмента этой вкладки
        /// </summary>
        public decimal PriceBestBid
        {
            get { return _connector.BestBid; }
        }

        /// <summary>
        /// цена центра стакана по инструменту этой вкладки
        /// </summary>
        public decimal PriceCenterMarketDepth
        {
            get
            {
                return (_connector.BestAsk + _connector.BestBid) / 2; 
            }
        }

// вызыв окон управления

        /// <summary>
        /// показать окно настроек коннектора
        /// </summary>
        public void ShowConnectorDialog()
        {
            _connector.ShowDialog();
        }

        /// <summary>
        /// показать окно настроек стратега
        /// </summary>
        public void ShowManualControlDialog()
        {
            _manualControl.ShowDialog();
        }

        /// <summary>
        /// показать окно закрытия позиции
        /// </summary>
        /// <param name="position">позиция которую будем крыть</param>
        public void ShowClosePositionDialog(Position position)
        {
            try
            {
                ClosePositionUi ui = new ClosePositionUi(position, _connector.BestBid);
                ui.ShowDialog();

                if (ui.IsAccept == false)
                {
                    return;
                }

                if (ui.OpenType == PositionOpenType.Market)
                {
                    CloseAtMarket(position, position.OpenVolume);
                }
                else if (ui.OpenType == PositionOpenType.Limit)
                {
                    if (ui.Price <= 0)
                    {
                        return;
                    }
                    CloseAtLimit(position, ui.Price, position.OpenVolume);
                }
                else if (ui.OpenType == PositionOpenType.Aceberg)
                {
                    CloseAtAceberg(position, ui.Price, position.OpenVolume, ui.CountAcebertOrder);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// показать окно открытия позиции
        /// </summary>
        public void ShowOpenPositionDialog()
        {
            try
            {
                PositionOpenUi ui = new PositionOpenUi(_connector.BestBid, Securiti.Name);
                ui.ShowDialog();

                if (ui.IsAccept == false)
                {
                    return;
                }

                if (ui.OpenType == PositionOpenType.Market)
                {
                    if (ui.Side == Side.Buy)
                    {
                        BuyAtMarket(ui.Volume);
                    }
                    else
                    {

                        SellAtMarket(ui.Volume);
                    }
                }

                else if (ui.OpenType == PositionOpenType.Limit)
                {
                    if (ui.Price <= 0)
                    {
                        return;
                    }
                    if (ui.Side == Side.Buy)
                    {
                        BuyAtLimit(ui.Volume, ui.Price);
                    }
                    else
                    {
                        SellAtLimit(ui.Volume, ui.Price);
                    }
                }

                else if (ui.OpenType == PositionOpenType.Aceberg)
                {
                    if (ui.Price <= 0)
                    {
                        return;
                    }

                    if (ui.CountAcebertOrder == 1 || ui.CountAcebertOrder == 0 ||
                        ui.Volume == 1)
                    {
                        if (ui.Side == Side.Buy)
                        {
                            BuyAtLimit(ui.Volume, ui.Price);
                        }
                        else
                        {
                            SellAtLimit(ui.Volume, ui.Price);
                        }
                    }
                    else
                    {
                        if (ui.Side == Side.Buy)
                        {
                            BuyAtAceberg(ui.Volume, ui.Price, ui.CountAcebertOrder);
                        }
                        else
                        {
                            SellAtAceberg(ui.Volume, ui.Price, ui.CountAcebertOrder);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// показать окно для модификации позиции
        /// </summary>
        /// <param name="position"></param>
        public void ShowPositionModificateDialog(Position position)
        {
            try
            {
                PositionModificateUi ui = new PositionModificateUi(_connector.BestBid, Securiti.Name);
                ui.ShowDialog();

                if (ui.IsAccept == false)
                {
                    return;
                }
                // маркеты
                if (ui.OpenType == PositionOpenType.Market)
                {
                    if (ui.Side == Side.Buy)
                    {
                        if (position.Direction == Side.Buy)
                        { // если докупаем по позиции
                            BuyAtMarketToPosition(position, ui.Volume);
                        }
                        else
                        {
                            if (position.OpenVolume > ui.Volume)
                            {// если закрываем часть позиции
                                CloseAtMarket(position, ui.Volume);
                            }
                            else
                            {// если закрываем всю позицию
                                CloseAtMarket(position, position.OpenVolume);
                            }
                        }
                    }
                    else
                    {
                        if (position.Direction == Side.Sell)
                        { // если докупаем по позиции
                            SellAtMarketToPosition(position, ui.Volume);
                        }
                        else
                        {
                            if (position.OpenVolume > ui.Volume)
                            {// если закрываем часть позиции
                                CloseAtMarket(position, ui.Volume);
                            }
                            else
                            {// если закрываем всю позицию
                                CloseAtMarket(position, position.OpenVolume);
                            }
                        }
                    }
                }
                // лимиты
                else if (ui.OpenType == PositionOpenType.Limit ||
                    ui.OpenType == PositionOpenType.Aceberg && ui.CountAcebertOrder == 1)
                {
                    if (ui.Side == Side.Buy)
                    {
                        if (position.Direction == Side.Buy)
                        { // если докупаем по позиции
                            BuyAtLimitToPosition(position, ui.Price, ui.Volume);
                        }
                        else
                        {
                            if (position.OpenVolume > ui.Volume)
                            {// если закрываем часть позиции
                                CloseAtLimit(position, ui.Price,  ui.Volume);
                            }
                            else
                            {// если закрываем всю позицию
                                CloseAtLimit(position, ui.Price,  position.OpenVolume);
                            }
                        }
                    }
                    else
                    {
                        if (position.Direction == Side.Sell)
                        { // если докупаем по позиции
                            SellAtLimitToPosition(position, ui.Price, ui.Volume);
                        }
                        else
                        {
                            if (position.OpenVolume > ui.Volume)
                            {// если закрываем часть позиции
                                CloseAtLimit(position, ui.Price,  ui.Volume);
                            }
                            else
                            {// если закрываем всю позицию
                                CloseAtLimit(position, ui.Price,  position.OpenVolume);
                            }
                        }
                    }
                }
                else if (ui.OpenType == PositionOpenType.Aceberg)
                {
                    if (ui.Side == Side.Buy)
                    {
                        if (position.Direction == Side.Buy)
                        { // если докупаем по позиции
                            BuyAtAcebergToPosition(position, ui.Price, ui.Volume, ui.CountAcebertOrder);
                        }
                        else
                        {
                            if (position.OpenVolume > ui.Volume)
                            {// если закрываем часть позиции
                                CloseAtAceberg(position, ui.Price, ui.Volume, ui.CountAcebertOrder);
                            }
                            else
                            {// если закрываем всю позицию
                                CloseAtAceberg(position, ui.Price, position.OpenVolume, ui.CountAcebertOrder);
                            }
                        }
                    }
                    else
                    {
                        if (position.Direction == Side.Sell)
                        { // если докупаем по позиции
                            SellAtAcebergToPosition(position, ui.Price, ui.Volume, ui.CountAcebertOrder);
                        }
                        else
                        {
                            if (position.OpenVolume > ui.Volume)
                            {// если закрываем часть позиции
                                CloseAtAceberg(position, ui.Price, ui.Volume, ui.CountAcebertOrder);
                            }
                            else
                            {// если закрываем всю позицию
                                CloseAtAceberg(position, ui.Price, position.OpenVolume, ui.CountAcebertOrder);
                            }
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// показать окно выставления стопа для позиции
        /// </summary>
        /// <param name="position">позиция для которой будет переставлен стоп</param>
        public void ShowStopSendDialog(Position position)
        {
            try
            {
                PositionStopUi ui = new PositionStopUi(position, _connector.BestBid, "Стоп для позиции");
                ui.ShowDialog();

                if (ui.IsAccept == false)
                {
                    return;
                }

                if (ui.PriceActivate <= 0 || ui.PriceOrder <= 0)
                {
                    return;
                }

                CloseAtStop(position, ui.PriceActivate, ui.PriceOrder);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// показать окно выставления профита для позиции
        /// </summary>
        /// <param name="position">позиция для которой будет переставлен профит</param>
        public void ShowProfitSendDialog(Position position)
        {
            try
            {
                PositionStopUi ui = new PositionStopUi(position, _connector.BestBid, "Профит для позиции");
                ui.ShowDialog();

                if (ui.IsAccept == false)
                {
                    return;
                }
                if (ui.PriceActivate <= 0 || ui.PriceOrder <= 0)
                {
                    return;
                }

                CloseAtProfit(position, ui.PriceActivate, ui.PriceOrder);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// переместить график к текущему времени
        /// </summary>
        /// <param name="time"></param>
        public void GoChartToThisTime(DateTime time)
        {
            _chartMaster.GoChartToTime(time);
        }

// стандартные публичные функции для управления позицией

        /// <summary>
        /// войти в позицию Лонг по любой цене
        /// </summary>
        /// <param name="volume">объём которым следует войти</param>
        public Position BuyAtMarket(decimal volume)
        {
            try
            {
                decimal price = _connector.BestAsk;

                if (price == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Коннектор не активен!", LogMessageType.System);
                    return null;
                }

                price = price + Securiti.PriceStep * 20;

                OrderPriceType type = OrderPriceType.Market;

                TimeSpan timeLife = _manualControl.SecondToOpen;

                if (_connector.ServerType == ServerType.InteractivBrokers)
                { // маркет заявки запрещены везде кроме IB
                    return LongCreate(price, volume, type, timeLife, false);
                }
                else
                {
                    return BuyAtLimit(volume, price);
                }
                

                // если верхнюю строку закоментить а нижнюю раскомментить то вновь появятся настоящие маркет заявки
                // на многих рынках не работает, поэтому убрали чтобы небыло проблем

                // 
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;

        }

        /// <summary>
        /// войти в позицию Лонг по любой цене
        /// </summary>
        /// <param name="volume">объём которым следует войти</param>
        /// <param name="signalType">название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen </param>
        /// <returns></returns>
        public Position BuyAtMarket(decimal volume, string signalType)
        {
            Position position = BuyAtMarket(volume);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// войти в позицию Лонг по определённой цене
        /// </summary>
        /// <param name="volume">объём позиции</param>
        /// <param name="priceLimit">цена выставляемой заявки</param>
        public Position BuyAtLimit(decimal volume, decimal priceLimit)
        {
            try
            {
                return LongCreate(priceLimit, volume, OrderPriceType.Limit, _manualControl.SecondToOpen, false);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// войти в позицию Лонг по определённой цене
        /// </summary>
        /// <param name="volume">объём позиции</param>
        /// <param name="priceLimit">цена выставляемой заявки</param>
        /// <param name="signalType">>название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public Position BuyAtLimit(decimal volume, decimal priceLimit, string signalType)
        {
            Position position = BuyAtLimit(volume,priceLimit);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// войти в позицию Лонг айсбергом
        /// в тестовом сервере будет выставлен один ордер
        /// </summary>
        /// <param name="volume">объём позиции</param>
        /// <param name="price">цена выставляемой заявки</param>
        /// /// <param name="orderCount">количество ордеров в айсберге</param>
        public Position BuyAtAceberg(decimal volume, decimal price, int orderCount)
        {
            try
            {
                if (ServerMaster.StartProgram != ServerStartProgramm.IsOsTrader || orderCount <= 1)
                {
                    return BuyAtLimit(volume, price);
                }

                if (volume == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Объём не может быть равен нулю!", LogMessageType.System);
                    return null;
                }

                if (price == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Коннектор не активен!", LogMessageType.System);
                    return null;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Нет портфеля или бумаги!", LogMessageType.System);
                    return null;
                }

                if (Securiti != null)
                {// если не тестируем, то обрезаем цену по минимальному шагу инструмента
                    if (Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    { // обрезаем если знаки после запятой
                        int point = Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                        while (price % Securiti.PriceStep != 0)
                        {
                            price = price - 1;
                        }
                    }
                }
                else
                {
                    decimal lastPrice = _connector.BestBid;
                    if (lastPrice.ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    { // обрезаем если знаки после запятой
                        int point = lastPrice.ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                    }
                }

                Position newDeal = new Position();
                newDeal.Number = NumberGen.GetNumberDeal();
                newDeal.Direction = Side.Buy;
                newDeal.State = PositionStateType.Opening;

                newDeal.NameBot = TabName;
                newDeal.Lots = Securiti.Lot;
                newDeal.PriceStepCost = Securiti.PriceStepCost;
                newDeal.PriceStep = Securiti.PriceStep;
                newDeal.PortfolioValueOnOpenPosition = Portfolio.ValueCurrent;

                _journal.SetNewDeal(newDeal);

                _acebergMaker.MakeNewAceberg(price, _manualControl.SecondToOpen, orderCount, newDeal, AcebergType.Open, volume, this);

                return newDeal;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// войти в позицию Лонг айсбергом
        /// в тестовом сервере будет выставлен один ордер
        /// </summary>
        /// <param name="volume">объём позиции</param>
        /// <param name="price">цена выставляемой заявки</param>
        /// /// <param name="orderCount">количество ордеров в айсберге</param>
        /// <param name="signalType">название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public Position BuyAtAceberg(decimal volume, decimal price, int orderCount, string signalType)
        {
            Position position = BuyAtAceberg(volume, price, orderCount);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// купить по пересечению цены
        /// </summary>
        /// <param name="volume">объём</param>
        /// <param name="priceLimit">цена ордера</param>
        /// <param name="priceRedLine">цена линии, после достижения которой будет выставлен ордер на покупку</param>
        /// <param name="activateType">тип активации ордера</param>
        /// /// <param name="expiresBars">время жизни ордера в барах</param>
        public void BuyAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, int expiresBars)
        {
            try
            {
                PositionOpenerToStop positionOpener = new PositionOpenerToStop(CandlesFinishedOnly.Count, expiresBars);
                positionOpener.Volume = volume;
                positionOpener.PriceOrder = priceLimit;
                positionOpener.PriceRedLine = priceRedLine;
                positionOpener.ActivateType = activateType;
                positionOpener.Side = Side.Buy;

                _stopsOpener.Add(positionOpener);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
            /// купить по пересечению цены. Действует одну свечку
            /// </summary>
            /// <param name="volume">объём</param>
            /// <param name="priceLimit">цена ордера</param>
            /// <param name="priceRedLine">цена линии, после достижения которой будет выставлен ордер на покупку</param>
            /// <param name="activateType">тип активации ордера</param>
        public void BuyAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType)
        {
            BuyAtStop(volume, priceLimit, priceRedLine, activateType, 1);
        }

        /// <summary>
        /// добавить в позицию новую заявку 
        /// </summary>
        /// <param name="position">позиция к которой будет добавлена заявка</param>
        /// <param name="priceLimit">цена заявки</param>
        /// <param name="volume">объём</param>
        public void BuyAtLimitToPosition(Position position, decimal priceLimit, decimal volume)
        {
            try
            {
                if (position.Direction == Side.Sell)
                {
                    SetNewLogMessage("Робот " + TabName + " попытка добавить в шорт ордер лонг. Блокировано", LogMessageType.Error);
                    return;
                }

                LongUpdate(position, priceLimit, volume, _manualControl.SecondToOpen, false);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// добавить в позицию Лонг новую заявку по маркету 
        /// </summary>
        /// <param name="position">позиция к которой будет добавлена заявка</param>
        /// <param name="volume">объём</param>
        public void BuyAtMarketToPosition(Position position, decimal volume)
        {
            try
            {
                if (position.Direction == Side.Sell)
                {
                    SetNewLogMessage("Робот " + TabName + " попытка добавить в шорт ордер лонг. Блокировано", LogMessageType.Error);

                    return;
                }

                decimal price = _connector.BestAsk;

                if (price == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Коннектор не активен!", LogMessageType.System);
                    return;
                }

                price = price + price * 0.01m;

                if (Securiti != null && Securiti.PriceStep < 1 && Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                {
                    int countPoint = Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                    price = Math.Round(price, countPoint);
                }
                else if (Securiti != null && Securiti.PriceStep >= 1)
                {
                    price = Math.Round(price, 0);
                    while (price % Securiti.PriceStep != 0)
                    {
                        price = price - 1;
                    }
                }

                LongUpdate(position, price, volume, _manualControl.SecondToOpen, false);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// добавить в позицию Лонг новую заявку по маркету 
        /// </summary>
        /// <param name="position">позиция к которой будет добавлена заявка</param>
        /// <param name="price">цена заявок</param>
        /// <param name="volume">объём</param>
        /// <param name="orderCount">количество ордеров для айсберга</param>
        public void BuyAtAcebergToPosition(Position position, decimal price, decimal volume, int orderCount)
        {
            try
            {
                if (ServerMaster.StartProgram != ServerStartProgramm.IsOsTrader || orderCount <= 1)
                {
                    if (position.Direction == Side.Sell)
                    {
                        ClosePeaceOfDeal(position, OrderPriceType.Limit, price, _manualControl.SecondToClose, volume);

                        return;
                    }

                    LongUpdate(position, price, volume, _manualControl.SecondToOpen, false);
                    return;
                }

                if (volume == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Объём не может быть равен нулю!", LogMessageType.System);
                    return;
                }

                if (price == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Коннектор не активен!", LogMessageType.System);
                    return;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Нет портфеля или бумаги!", LogMessageType.System);
                    return;
                }

                if (Securiti != null)
                {// если не тестируем, то обрезаем цену по минимальному шагу инструмента
                    if (Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    { // обрезаем если знаки после запятой
                        int point = Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                        while (price % Securiti.PriceStep != 0)
                        {
                            price = price - 1;
                        }
                    }
                }
                else
                {
                    decimal lastPrice = _connector.BestBid;
                    if (lastPrice.ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    { // обрезаем если знаки после запятой
                        int point = lastPrice.ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                    }
                }


                _acebergMaker.MakeNewAceberg(price, _manualControl.SecondToOpen, orderCount, position, AcebergType.ModificateBuy, volume, this);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// отменить все заявки на покупку по пробитию уровня
        /// </summary>
        public void BuyAtStopCanсel()
        {
            try
            {
                if (_stopsOpener == null || _stopsOpener.Count == 0)
                {
                    return;
                }

                for (int i = 0; _stopsOpener.Count != 0 && i < _stopsOpener.Count; i++)
                {
                    if (_stopsOpener[i].Side == Side.Buy)
                    {
                        _stopsOpener.Remove(_stopsOpener[i]);
                        i--;
                    }
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// войти в позицию Шорт по любой цене
        /// </summary>
        /// <param name="volume">объём позиции</param>
        public Position SellAtMarket(decimal volume)
        {
            try
            {
                decimal price = _connector.BestBid;

                if (price == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Коннектор не активен!", LogMessageType.System);
                    return null;
                }

                price = price - Securiti.PriceStep * 20;

                OrderPriceType type = OrderPriceType.Market;

                TimeSpan timeLife = _manualControl.SecondToOpen;

                if (_connector.ServerType == ServerType.InteractivBrokers)
                { // везде кроме IB маркет заявки запрещены
                    return ShortCreate(price, volume, type, timeLife, false);
                }
                else
                {
                    return SellAtLimit(volume, price);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// войти в позицию Шорт по любой цене
        /// </summary>
        /// <param name="volume">объём позиции</param>
        /// <param name="signalType">название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public Position SellAtMarket(decimal volume, string signalType)
        {
            Position position = SellAtMarket(volume);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// войти в позицию Шорт по определённой цене
        /// </summary>
        /// <param name="volume">объём позиции</param>
        /// <param name="priceLimit">цена заявки</param>
        public Position SellAtLimit(decimal volume, decimal priceLimit)
        {
            try
            {
                return ShortCreate(priceLimit, volume, OrderPriceType.Limit, _manualControl.SecondToOpen, false);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// войти в позицию Шорт по определённой цене
        /// </summary>
        /// <param name="volume">объём позиции</param>
        /// <param name="priceLimit">цена заявки</param>
        /// <param name="signalType">название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public Position SellAtLimit(decimal volume, decimal priceLimit, string signalType)
        {
            Position position = SellAtLimit(volume, priceLimit);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// войти в позицию Лонг айсбергом
        /// в тестовом сервере будет выставлен один ордер
        /// </summary>
        /// <param name="volume">объём позиции</param>
        /// <param name="price">цена</param>
        /// <param name="orderCount">количество ордеров в айсберге</param>
        public Position SellAtAceberg(decimal volume, decimal price, int orderCount)
        {
            try
            {
                if (ServerMaster.StartProgram != ServerStartProgramm.IsOsTrader || orderCount <= 1)
                {
                    return SellAtLimit(volume, price);
                }

                if (volume == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Объём не может быть равен нулю!", LogMessageType.System);
                    return null;
                }

                if (price == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Коннектор не активен!", LogMessageType.System);
                    return null;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Нет портфеля или бумаги!", LogMessageType.System);
                    return null;
                }

                if (Securiti != null)
                {// если не тестируем, то обрезаем цену по минимальному шагу инструмента
                    if (Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    { // обрезаем если знаки после запятой
                        int point = Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                        while (price % Securiti.PriceStep != 0)
                        {
                            price = price - 1;
                        }
                    }
                }
                else
                {
                    decimal lastPrice = _connector.BestBid;
                    if (lastPrice.ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    { // обрезаем если знаки после запятой
                        int point = lastPrice.ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                    }
                }

                Position newDeal = new Position();
                newDeal.Number = NumberGen.GetNumberDeal();
                newDeal.Direction = Side.Sell;
                newDeal.State = PositionStateType.Opening;

                newDeal.NameBot = TabName;
                newDeal.Lots = Securiti.Lot;
                newDeal.PriceStepCost = Securiti.PriceStepCost;
                newDeal.PriceStep = Securiti.PriceStep;
                newDeal.PortfolioValueOnOpenPosition = Portfolio.ValueCurrent;

                _journal.SetNewDeal(newDeal);

                _acebergMaker.MakeNewAceberg(price, _manualControl.SecondToOpen, orderCount, newDeal, AcebergType.Open, volume, this);

                return newDeal;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// войти в позицию Лонг айсбергом
        /// в тестовом сервере будет выставлен один ордер
        /// </summary>
        /// <param name="volume">объём позиции</param>
        /// <param name="price">цена</param>
        /// <param name="orderCount">количество ордеров в айсберге</param>
        /// <param name="signalType">название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public Position SellAtAceberg(decimal volume, decimal price, int orderCount, string signalType)
        {
            Position position = SellAtAceberg(volume, price,orderCount);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// продать по пересечению цены
        /// </summary>
        /// <param name="volume">объём</param>
        /// <param name="priceLimit">цена ордера</param>
        /// <param name="priceRedLine">цена линии, после достижения которой будет выставлен ордер на продажу</param>
        /// <param name="activateType">тип активации ордера</param>
        /// <param name="expiresBars">через сколько свечей заявка будет снята</param>
        public void SellAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, int expiresBars)
        {
            try
            {
                PositionOpenerToStop positionOpener = new PositionOpenerToStop(CandlesFinishedOnly.Count, expiresBars);
                positionOpener.Volume = volume;
                positionOpener.PriceOrder = priceLimit;
                positionOpener.PriceRedLine = priceRedLine;
                positionOpener.ActivateType = activateType;
                positionOpener.Side = Side.Sell;

                _stopsOpener.Add(positionOpener);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// продать по пересечению цены. Действует одну свечку
        /// </summary>
        /// <param name="volume">объём</param>
        /// <param name="priceLimit">цена ордера</param>
        /// <param name="priceRedLine">цена линии, после достижения которой будет выставлен ордер на продажу</param>
        /// <param name="activateType">тип активации ордера</param>
        public void SellAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType)
        {
            SellAtStop(volume, priceLimit, priceRedLine, activateType, 1);
        }

        /// <summary>
        /// добавить в позицию Шорт новую заявку по лимиту
        /// </summary>
        /// <param name="position">позиция к которой будет добавлена заявка</param>
        /// <param name="priceLimit">цена заявки</param>
        /// <param name="volume">объём</param>
        public void SellAtLimitToPosition(Position position, decimal priceLimit, decimal volume)
        {
            try
            {
                if (position.Direction == Side.Buy)
                {
                    SetNewLogMessage("Робот " + TabName + " попытка добавить в лонг ордер шорт. Блокировано", LogMessageType.Error);

                    return;
                }

                ShortUpdate(position, priceLimit, volume, _manualControl.SecondToOpen, false);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// добавить в позицию Шорт новую заявку по маркету
        /// </summary>
        /// <param name="position">позиция к которой будет добавлена заявка</param>
        /// <param name="volume">объём</param>
        public void SellAtMarketToPosition(Position position, decimal volume)
        {
            try
            {
                if (position.Direction == Side.Buy)
                {
                    SetNewLogMessage("Робот " + TabName + " попытка добавить в лонг ордер шорт. Блокировано", LogMessageType.Error);

                    return;
                }

                decimal price = _connector.BestBid;

                if (price == 0)
                {
                    SetNewLogMessage("Робот " + TabName + " Не возможно открыть сделку! Коннектор не активен!", LogMessageType.Error);
                    return;
                }

                price = price - price * 0.01m;

                if (Securiti != null && Securiti.PriceStep < 1 && Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                {
                    int countPoint = Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                    price = Math.Round(price, countPoint);
                }
                else if (Securiti != null && Securiti.PriceStep >= 1)
                {
                    price = Math.Round(price, 0);
                    while (price % Securiti.PriceStep != 0)
                    {
                        price = price - 1;
                    }
                }

                ShortUpdate(position, price, volume, _manualControl.SecondToOpen, false);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// добавить в позицию Лонг новую заявку по маркету 
        /// </summary>
        /// <param name="position">позиция к которой будет добавлена заявка</param>
        /// <param name="price">цена заявок</param>
        /// <param name="volume">объём</param>
        /// <param name="orderCount">количество ордеров для айсберга</param>
        public void SellAtAcebergToPosition(Position position, decimal price, decimal volume, int orderCount)
        {
            try
            {
                if (ServerMaster.StartProgram != ServerStartProgramm.IsOsTrader || orderCount <= 1)
                {
                    if (position.Direction == Side.Buy)
                    {
                        ClosePeaceOfDeal(position, OrderPriceType.Limit, price, _manualControl.SecondToClose, volume);
                        return;
                    }

                    ShortUpdate(position, price, volume, _manualControl.SecondToOpen, false);
                    return;
                }

                if (volume == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Объём не может быть равен нулю!", LogMessageType.System);
                    return;
                }

                if (price == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Коннектор не активен!", LogMessageType.System);
                    return;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Нет портфеля или бумаги!", LogMessageType.System);
                    return;
                }

                if (Securiti != null)
                {// если не тестируем, то обрезаем цену по минимальному шагу инструмента
                    if (Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    { // обрезаем если знаки после запятой
                        int point = Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                        while (price % Securiti.PriceStep != 0)
                        {
                            price = price - 1;
                        }
                    }
                }
                else
                {
                    decimal lastPrice = _connector.BestBid;
                    if (lastPrice.ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    { // обрезаем если знаки после запятой
                        int point = lastPrice.ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                    }
                }


                _acebergMaker.MakeNewAceberg(price, _manualControl.SecondToOpen, orderCount, position, AcebergType.ModificateSell, volume, this);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// отменить все заявки на продажу по пробитию уровня
        /// </summary>
        public void SellAtStopCanсel()
        {
            try
            {
                if (_stopsOpener == null || _stopsOpener.Count == 0)
                {
                    return;
                }

                for (int i = 0; _stopsOpener.Count != 0 && i < _stopsOpener.Count; i++)
                {
                    if (_stopsOpener[i].Side == Side.Sell)
                    {
                        _stopsOpener.Remove(_stopsOpener[i]);
                        i--;
                    }
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// закрыть все позиции по рынку
        /// </summary>
        public void CloseAllAtMarket()
        {
            try
            {
                List<Position> positions = _journal.OpenPositions;

                if (positions != null)
                {
                    for (int i = 0; i < positions.Count; i++)
                    {
                        CloseAtMarket(positions[i], positions[i].OpenVolume);
                    }
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// закрыть позицию по любой цене
        /// </summary>
        /// <param name="position">позиция которую будем закрывать</param>
        /// <param name="volume">объём нужный к закрытию</param>
        public void CloseAtMarket(Position position, decimal volume)
        {
            try
            {
                if (volume <= 0 || position.OpenVolume <= 0)
                {
                    return;
                }
                decimal price = _connector.BestAsk;

                if (position.Direction == Side.Buy)
                {
                    price = _connector.BestBid - Securiti.PriceStep * 20;
                }
                else
                {
                    price = price + Securiti.PriceStep * 20;
                }

                if (price == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Коннектор не активен!", LogMessageType.System);
                    return;
                }

                if (_connector.ServerType == ServerType.InteractivBrokers)
                { // маркет заявки разрешены только в IB
                    if (position.OpenVolume <= volume)
                    {
                        CloseDeal(position, OrderPriceType.Market, price, _manualControl.SecondToClose, false);
                    }
                    else if (position.OpenVolume > volume)
                    {
                        ClosePeaceOfDeal(position, OrderPriceType.Market, price, _manualControl.SecondToClose, volume);
                    }
                }
                else
                {
                    CloseAtLimit(position, price, volume);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// закрыть позицию по любой цене
        /// </summary>
        /// <param name="position">позиция которую будем закрывать</param>
        /// <param name="volume">объём нужный к закрытию</param>
        /// <param name="signalType">название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public void CloseAtMarket(Position position, decimal volume, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtMarket(position,volume);
        }

        /// <summary>
        /// закрыть позицию по определённой цене
        /// </summary>
        /// <param name="position">позиция которую будем закрывать</param>
        /// <param name="priceLimit">цена ордера</param>
        /// Не закрытая/Частично закрытая заявка попадёт в обработчик закрытых заявок PositionClosingFail(Position position). 
        /// <param name="volume">объём нужный к закрытию</param>
        public void CloseAtLimit(Position position, decimal priceLimit, decimal volume)
        {
            try
            {
                if (volume <= 0 || position.OpenVolume <= 0)
                {
                    return;
                }
                if (position.OpenVolume <= volume)
                {
                    CloseDeal(position, OrderPriceType.Limit, priceLimit, _manualControl.SecondToClose, false);
                }
                else if (position.OpenVolume > volume)
                {
                    ClosePeaceOfDeal(position, OrderPriceType.Limit, priceLimit, _manualControl.SecondToClose, volume);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// закрыть позицию по определённой цене
        /// </summary>
        /// <param name="position">позиция которую будем закрывать</param>
        /// <param name="priceLimit">цена ордера</param>
        /// Не закрытая/Частично закрытая заявка попадёт в обработчик закрытых заявок PositionClosingFail(Position position). 
        /// <param name="volume">объём нужный к закрытию</param>
        /// <param name="signalType">название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public void CloseAtLimit(Position position, decimal priceLimit, decimal volume, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtLimit(position, priceLimit,volume);
        }

        /// <summary>
        /// закрыть позицию по айсбергу определённой цене
        /// </summary>
        /// <param name="position">позиция которую будем закрывать</param>
        /// <param name="priceLimit">цена ордера</param>
        /// <param name="volume">объём нужный к закрытию</param>
        /// <param name="orderCount">количество ордеров для айсберга</param>
        public void CloseAtAceberg(Position position, decimal priceLimit, decimal volume, int orderCount)
        {
            try
            {
                if (volume <= 0 || position.OpenVolume <= 0)
                {
                    return;
                }
                if (ServerMaster.StartProgram != ServerStartProgramm.IsOsTrader || orderCount <= 1)
                {
                    if (position.OpenVolume <= volume)
                    {
                        CloseDeal(position, OrderPriceType.Limit, priceLimit, _manualControl.SecondToClose, false);
                    }
                    else if (position.OpenVolume > volume)
                    {
                        ClosePeaceOfDeal(position, OrderPriceType.Limit, priceLimit, _manualControl.SecondToClose, volume);
                    }
                    return;
                }

                if (position.Direction == Side.Buy)
                {
                    SellAtAcebergToPosition(position, priceLimit, volume, orderCount);
                }
                else
                {
                    BuyAtAcebergToPosition(position, priceLimit, volume, orderCount);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// закрыть позицию по айсбергу определённой цене
        /// </summary>
        /// <param name="position">позиция которую будем закрывать</param>
        /// <param name="priceLimit">цена ордера</param>
        /// <param name="volume">объём нужный к закрытию</param>
        /// <param name="orderCount">количество ордеров для айсберга</param>
        /// <param name="signalType">название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public void CloseAtAceberg(Position position, decimal priceLimit, decimal volume, int orderCount, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtAceberg(position, priceLimit, volume, orderCount);
        }

        /// <summary>
        /// выставить стоп-ордер для позиции
        /// </summary>
        /// <param name="position">позиция которую будем закрывать</param>
        /// <param name="priceActivation">цена стоп приказа, после достижения которой выставиться ордер</param>
        /// <param name="priceOrder">цена ордера</param>
        public void CloseAtStop(Position position, decimal priceActivation, decimal priceOrder)
        {
            TryReloadStop(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// выставить трейлинг стоп-ордер для позиции 
        /// </summary>
        /// <param name="position">позиция которую будем закрывать</param>
        /// <param name="priceActivation">цена стоп приказа, после достижения которой выставиться ордер</param>
        /// <param name="priceOrder">цена ордера</param>
        public void CloseAtTrailingStop(Position position, decimal priceActivation, decimal priceOrder)
        {
            if (position.StopOrderIsActiv &&
                position.Direction == Side.Buy &&
                position.StopOrderPrice > priceOrder)
            {
                return;
            }

            if (position.StopOrderIsActiv &&
                position.Direction == Side.Sell &&
                position.StopOrderPrice < priceOrder)
            {
                return;
            }

            TryReloadStop(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// выставить профит-ордер для позиции
        /// </summary>
        /// <param name="position">позиция которую будем закрывать</param>
        /// <param name="priceActivation">цена профит приказа, после достижения которого выставиться ордер</param>
        /// <param name="priceOrder">цена ордера</param>
        public void CloseAtProfit(Position position, decimal priceActivation, decimal priceOrder)
        {
            TryReloadProfit(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// отозвать все открытые роботом ордера из системы
        /// </summary>
        public void CloseAllOrderInSystem()
        {
            try
            {
                List<Position> positions = _journal.OpenPositions;

                if (positions == null)
                {
                    return;
                }

                for (int i = 0; i < positions.Count; i++)
                {
                    CloseAllOrderToPosition(positions[i]);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// отозвать все ордера из системы, связанные с этой сделкой
        /// </summary>
        /// <param name="position">позиция, по которой нужно отозвать ордера</param>
        public void CloseAllOrderToPosition(Position position)
        {
            try
            {
                // надо снять оставшиеся ордера
                position.StopOrderIsActiv = false;
                position.ProfitOrderIsActiv = false;


                if (position.OpenOrders != null &&
                   position.OpenOrders.Count > 0)
                {
                    for (int i = 0; i < position.OpenOrders.Count; i++)
                    {
                        Order order = position.OpenOrders[i];
                        if (order.State != OrderStateType.Done
                            && order.State != OrderStateType.Fail && order.State != OrderStateType.Cancel)
                        {
                            _connector.OrderCancel(position.OpenOrders[i]);
                        }
                    }
                }


                if (position.CloseOrders != null)
                {
                    for (int i = 0; i < position.CloseOrders.Count; i++)
                    {
                        Order closeOrder = position.CloseOrders[i];
                        if (closeOrder.State != OrderStateType.Done
                        && closeOrder.State != OrderStateType.Fail && closeOrder.State != OrderStateType.Cancel)
                        {
                            _connector.OrderCancel(closeOrder);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// отозвать ордер
        /// </summary>
        public void CloseOrder(Order order)
        {
            _connector.OrderCancel(order);
        }

// внутренние функции управления позицией

        /// <summary>
        /// продать. Создать позицию шорт
        /// </summary>
        /// <param name="price">цена заявки</param>
        /// <param name="volume">объём</param>
        /// <param name="priceType">тип цены</param>
        /// <param name="timeLife">время жизни</param>
        /// <param name="isStopOrProfit">является ли ордер следствием срабатывания стопа или профита</param>
        /// <returns></returns>
        private Position ShortCreate(decimal price, decimal volume, OrderPriceType priceType, TimeSpan timeLife,
            bool isStopOrProfit)
        {
            try
            {
                Side direction = Side.Sell;

                if (volume == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Объём не может быть равен нулю!",
                        LogMessageType.System);
                    return null;
                }

                if (price == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Коннектор не активен!", LogMessageType.System);
                    return null;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Нет портфеля или бумаги!", LogMessageType.System);
                    return null;
                }

                // обрезаем цену по минимальному шагу инструмента

                price = RoundPrice(price, Securiti, Side.Sell);

                Position newDeal = _dealCreator.CreatePosition(TabName, direction, price, volume, priceType,
                    timeLife, Securiti, Portfolio);
                newDeal.OpenOrders[0].IsStopOrProfit = isStopOrProfit;
                _journal.SetNewDeal(newDeal);

                //SetNewLogMessage(Securiti.Name + " Шорт", LogMessageType.Trade);

                _connector.OrderExecute(newDeal.OpenOrders[0]);
                return newDeal;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// модифицировать позицию ордером шорт
        /// </summary>
        /// <param name="position">позиция</param>
        /// <param name="price">цена заявки</param>
        /// <param name="volume">объём</param>
        /// <param name="timeLife">время жизни</param>
        /// <param name="isStopOrProfit">является ли ордер следствием срабатывания стопа или профита</param>
        /// <returns></returns>
        private void ShortUpdate(Position position, decimal price, decimal volume, TimeSpan timeLife,
            bool isStopOrProfit)
        {
            try
            {
                if (volume == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Объём не может быть равен нулю!", LogMessageType.System);
                    return;
                }

                if (price == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Коннектор не активен!", LogMessageType.System);
                    return;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Нет портфеля или бумаги!", LogMessageType.System);
                    return;
                }

                // обрезаем цену по минимальному шагу инструмента

                price = RoundPrice(price, Securiti, Side.Sell);

                // закрываыем другие открывающие ордера, если они есть

                if (position.OpenOrders != null &&
                    position.OpenOrders.Count > 0)
                {
                    for (int i = 0; i < position.OpenOrders.Count; i++)
                    {
                        if (position.OpenOrders[i].State == OrderStateType.Activ)
                        {
                            _connector.OrderCancel(position.OpenOrders[i]);
                        }
                    }
                }


                Order newOrder = _dealCreator.CreateOrder(Side.Sell, price, volume, OrderPriceType.Limit,
                    _manualControl.SecondToOpen);
                newOrder.IsStopOrProfit = isStopOrProfit;
                newOrder.LifeTime = timeLife;
                position.AddNewOpenOrder(newOrder);

                SetNewLogMessage(Securiti.Name + "модификация позиции шорт", LogMessageType.Trade);

                _connector.OrderExecute(newOrder);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Купить. Создать позицию лонг
        /// </summary>
        /// <param name="price">цена заявки</param>
        /// <param name="volume">объём</param>
        /// <param name="priceType">тип цены</param>
        /// <param name="timeLife">время жизни</param>
        /// <param name="isStopOrProfit">является ли ордер следствием срабатывания стопа или профита</param>
        /// <returns></returns>
        private Position LongCreate(decimal price, decimal volume, OrderPriceType priceType, TimeSpan timeLife,
            bool isStopOrProfit) // купить
        {
            try
            {
                //SetNewLogMessage(DateTime.Now.Millisecond.ToString(), LogMessageType.System);
                Side direction = Side.Buy;

                if (volume == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Объём не может быть равен нулю!", LogMessageType.System);
                    return null;
                }

                if (price == 0)
                {  
                    SetNewLogMessage("Не возможно открыть сделку! Коннектор не активен!", LogMessageType.System);
                    return null;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Нет портфеля или бумаги!", LogMessageType.System);
                    return null;
                }

                //SetNewLogMessage(DateTime.Now.Millisecond.ToString(), LogMessageType.System);

                price = RoundPrice(price, Securiti, Side.Buy);


                Position newDeal = _dealCreator.CreatePosition(TabName, direction, price, volume, priceType,
                    timeLife, Securiti, Portfolio);
                newDeal.OpenOrders[0].IsStopOrProfit = isStopOrProfit;
                _journal.SetNewDeal(newDeal);
 
                _connector.OrderExecute(newDeal.OpenOrders[0]);
                //SetNewLogMessage(DateTime.Now.Millisecond.ToString(), LogMessageType.System);

                return newDeal;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// модифицировать позицию ордером лонг
        /// </summary>
        /// <param name="position">позиция</param>
        /// <param name="price">цена заявки</param>
        /// <param name="volume">объём</param>
        /// <param name="timeLife">время жизни</param>
        /// <param name="isStopOrProfit">является ли ордер следствием срабатывания стопа или профита</param>
        /// <returns></returns>
        private void LongUpdate(Position position, decimal price, decimal volume, TimeSpan timeLife,
            bool isStopOrProfit)
        {
            try
            {
                if (volume == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Объём не может быть равен нулю!", LogMessageType.System);
                    return;
                }

                if (price == 0)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Коннектор не активен!", LogMessageType.System);
                    return;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage("Не возможно открыть сделку! Нет портфеля или бумаги!", LogMessageType.System);
                    return;
                }

                // обрезаем цену по минимальному шагу инструмента

                price = RoundPrice(price, Securiti,Side.Buy);

                // закрываыем другие открывающие ордера, если они есть

                if (position.OpenOrders != null &&
                    position.OpenOrders.Count > 0)
                {
                    for (int i = 0; i < position.OpenOrders.Count; i++)
                    {
                        if (position.OpenOrders[i].State == OrderStateType.Activ)
                        {
                            _connector.OrderCancel(position.OpenOrders[i]);
                        }
                    }
                }


                Order newOrder = _dealCreator.CreateOrder(Side.Buy, price, volume, OrderPriceType.Limit,
                    _manualControl.SecondToOpen);
                newOrder.IsStopOrProfit = isStopOrProfit;
                newOrder.LifeTime = timeLife;
                position.AddNewOpenOrder(newOrder);

                SetNewLogMessage(Securiti.Name + "модификация позиции шорт", LogMessageType.Trade);

                _connector.OrderExecute(newOrder);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// закрыть позицию
        /// </summary>
        /// <param name="position">позиция</param>
        /// <param name="priceType">тип цены</param>
        /// <param name="price">цена</param>
        /// <param name="lifeTime">время жизни позиции</param>
        /// <param name="isStopOrProfit">является ли закрытие следствием срабатывания стопа или профита</param>
        private void CloseDeal(Position position, OrderPriceType priceType, decimal price, TimeSpan lifeTime,
            bool isStopOrProfit) // закрыть сделку 
        {
            try
            {
                //1 берём все открытые сделки

                if (position == null)
                {
                    return;
                }

                // 3 закрываем все ордера в сделке

                position.ProfitOrderIsActiv = false;
                position.StopOrderIsActiv = false;

                for (int i = 0; position.CloseOrders != null && i < position.CloseOrders.Count; i++)
                {
                    if (position.CloseOrders[i].State == OrderStateType.Activ)
                    {
                        _connector.OrderCancel(position.CloseOrders[i]);
                    }
                }

                for (int i = 0; position.OpenOrders != null && i < position.OpenOrders.Count; i++)
                {
                    if (position.OpenOrders[i].State == OrderStateType.Activ)
                    {
                        _connector.OrderCancel(position.OpenOrders[i]);
                    }
                }

                // обрезаем цену по минимальному шагу инструмента
                if (Securiti == null)
                {
                    return;
                }

                Side sideCloseOrder = Side.Buy;
                if (position.Direction == Side.Buy)
                {
                    sideCloseOrder = Side.Sell;
                }
                price = RoundPrice(price, Securiti, sideCloseOrder);

                if (position.State == PositionStateType.Done &&
                    position.OpenVolume == 0)
                {
                    return;
                }

                // 4 формируем закрывающую сделку
                position.State = PositionStateType.Closing;

                Order closeOrder = _dealCreator.CreateCloseOrderForDeal(position, price, priceType, lifeTime);

                if (closeOrder == null)
                {
                    if (position.OpenVolume == 0)
                    {
                        position.State = PositionStateType.OpeningFail;
                    }

                    return;
                }

                if (isStopOrProfit)
                {
                    closeOrder.IsStopOrProfit = true;
                }
                position.AddNewCloseOrder(closeOrder);
                _connector.OrderExecute(closeOrder);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// закрыть позицию частично
        /// </summary>
        /// <param name="position">позиция</param>
        /// <param name="priceType">тип цены</param>
        /// <param name="price">цена</param>
        /// <param name="lifeTime">время жизни позиции</param>
        /// <param name="volume">объём на который следует закрыть позицию</param>
        private void ClosePeaceOfDeal(Position position, OrderPriceType priceType, decimal price, TimeSpan lifeTime,
            decimal volume)
        {
            try
            {
                //1 берём все открытые сделки

                if (position == null)
                {
                    return;
                }

                // 3 закрываем все ордера в сделке

                if (position.CloseOrders != null)
                {
                    for (int i = 0; i < position.CloseOrders.Count; i++)
                    {
                        Order order = position.CloseOrders[i];
                        if (order.State != OrderStateType.Done && order.State != OrderStateType.Cancel)
                        {
                            _connector.OrderCancel(order);
                        }
                    }
                }

                if (position.OpenOrders != null &&
                    position.OpenOrders.Count > 0)
                {
                    for (int i = 0; i < position.OpenOrders.Count; i++)
                    {
                        if (position.OpenOrders[i].State == OrderStateType.Activ)
                        {
                            _connector.OrderCancel(position.OpenOrders[i]);
                        }
                    }
                }

                // обрезаем цену по минимальному шагу инструмента
                if (Securiti == null)
                {
                    return;
                }

                Side sideCloseOrder = Side.Buy;
                if (position.Direction == Side.Buy)
                {
                    sideCloseOrder = Side.Sell;
                }
                price = RoundPrice(price, Securiti, sideCloseOrder);

                // 4 формируем закрывающую сделку

                Order closeOrder = _dealCreator.CreateCloseOrderForDeal(position, price, priceType, lifeTime);


                if (closeOrder == null)
                {
                    if (position.OpenVolume == 0)
                    {
                        position.State = PositionStateType.OpeningFail;
                    }

                    return;
                }

                closeOrder.Volume = volume;
                position.AddNewCloseOrder(closeOrder);
                _connector.OrderExecute(closeOrder);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// перезагрузить стоп
        /// </summary>
        /// <param name="position">позиция</param>
        /// <param name="priceActivate">цена после которой ордер будет выставлен</param>
        /// <param name="priceOrder">цена ордера для стопа</param>
        private void TryReloadStop(Position position, decimal priceActivate, decimal priceOrder)
        {
            try
            {
                if (position == null)
                {
                    return;
                }

                if (position.State == PositionStateType.Done ||
                    position.State == PositionStateType.OpeningFail ||
                    position.State == PositionStateType.Closing)
                {
                    return;
                }

                if (position.StopOrderIsActiv &&
                    position.StopOrderPrice == priceOrder &&
                    position.StopOrderRedLine == priceActivate)
                { // всё совпадает, выходим
                    return;
                }

                // выставляем стоп
                decimal volume = position.OpenVolume;

                if (volume == 0)
                {
                    return;
                }

                position.StopOrderIsActiv = false;
                position.StopOrderPrice = priceOrder;
                position.StopOrderRedLine = priceActivate;
                position.StopOrderIsActiv = true;

                _chartMaster.SetPosition(_journal.AllPosition);
                _journal.PaintPosition(position);
                _journal.Save();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// перезагрузить профит
        /// </summary>
        /// <param name="position">позиция</param>
        /// <param name="priceActivate">цена после которой ордер будет выставлен</param>
        /// <param name="priceOrder">цена ордера для профита</param>
        private void TryReloadProfit(Position position, decimal priceActivate, decimal priceOrder)
        {
            try
            {
                if (position == null)
                {
                    return;
                }

                if (position.State == PositionStateType.Done ||
                    position.State == PositionStateType.OpeningFail ||
                    position.State == PositionStateType.Closing)
                {
                    return;
                }

                if (position.ProfitOrderIsActiv &&
                    position.ProfitOrderPrice == priceOrder &&
                    position.ProfitOrderRedLine == priceActivate)
                { // всё совпадает, выходим
                    return;
                }

                // выставляем профит

                decimal volume = position.OpenVolume;

                if (volume == 0)
                {
                    return;
                }


                position.ProfitOrderIsActiv = false;
                position.ProfitOrderPrice = priceOrder;
                position.ProfitOrderRedLine = priceActivate;
                position.ProfitOrderIsActiv = true;

                _chartMaster.SetPosition(_journal.AllPosition);
                _journal.PaintPosition(position);
                _journal.Save();

            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// подогнать цену контракта под нужды биржи
        /// </summary>
        /// <param name="price">текущая цена по которой интерфейс высокого уровня захотел закрыть позицию</param>
        /// <param name="security">бумага</param>
        /// <param name="side">сторона входа</param>
        /// <returns>обрезанное значение которое будет принято биржей</returns>
        private decimal RoundPrice(decimal price, Security security, Side side)
        {
            try
            {
                if (Securiti.PriceStep == 0)
                {
                    return price;
                }

                if (security.Decimals > 0)
                {

                    // обрезаем если знаки после запятой
                    price = Math.Round(price, Securiti.Decimals);

                    decimal minStep = 0.1m;

                    for (int i = 1; i < security.Decimals; i++)
                    {
                        minStep = minStep * 0.1m;
                    }

                    while (price % Securiti.PriceStep != 0)
                    {
                        price = price - minStep;
                    }
                }
                else
                {
                    price = Math.Round(price, 0);
                    while (price % Securiti.PriceStep != 0)
                    {
                        price = price - 1;
                    }
                }

                if (side == Side.Buy && 
                    Securiti.PriceLimitHigh != 0 && price > Securiti.PriceLimitHigh)
                {
                    price = Securiti.PriceLimitHigh;
                }

                if (side == Side.Sell &&
                    Securiti.PriceLimitLow != 0 && price < Securiti.PriceLimitLow)
                {
                    price = Securiti.PriceLimitLow;
                }

                return price;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return 0;
        }

// обработка алертов и сопровождения стопов

        private object _lockerManualReload = new object();

        /// <summary>
        /// проверить ручное сопровождение стопа и профита
        /// </summary>
        /// <param name="position">позиция</param>
        private void ManualReloadStopsAndProfitToPosition(Position position)
        {
            try
            {
                if (position.CloseOrders != null &&
                    position.CloseOrders[position.CloseOrders.Count - 1].State == OrderStateType.Activ)
                { // если последний ордер на закрытие ещё открыт
                    return;
                }

                lock (_lockerManualReload)
                {
                    Order openOrder = position.OpenOrders[position.OpenOrders.Count - 1];

                    if (openOrder.TradesIsComing == false)
                    { // значит ещё ни одного трейда не загружено
                        // это второй круг для того чтобы подгрузились трэйды сделки, чтобы определить цену входа
                        // иначе происходит беда
                        PositionToSecondLoopSender sender = new PositionToSecondLoopSender() { Position = position };
                        sender.PositionNeadToStopSend += ManualReloadStopsAndProfitToPosition;
                        Thread worker = new Thread(sender.Start);
                        worker.CurrentCulture = new CultureInfo("ru-RU");
                        worker.IsBackground = true;
                        worker.Start();
                        return;
                    }

                    if (_manualControl.StopIsOn)
                    {
                        if (position.Direction == Side.Buy)
                        {
                            decimal priceRedLine = position.EntryPrice - Securiti.PriceStep*_manualControl.StopDistance;
                            decimal priceOrder = priceRedLine - Securiti.PriceStep * _manualControl.StopSlipage;

                            TryReloadStop(position,priceRedLine,priceOrder);
                        }
                        if (position.Direction == Side.Sell)
                        {
                            decimal priceRedLine = position.EntryPrice + Securiti.PriceStep * _manualControl.StopDistance;
                            decimal priceOrder = priceRedLine + Securiti.PriceStep * _manualControl.StopSlipage;

                            TryReloadStop(position, priceRedLine, priceOrder);
                        }
                    }
                    if (_manualControl.ProfitIsOn)
                    {
                        if (position.Direction == Side.Buy)
                        {
                            decimal priceRedLine = position.EntryPrice + Securiti.PriceStep * _manualControl.ProfitDistance;
                            decimal priceOrder = priceRedLine - Securiti.PriceStep * _manualControl.ProfitSlipage;

                            TryReloadProfit(position, priceRedLine, priceOrder);
                        }
                        if (position.Direction == Side.Sell)
                        {
                            decimal priceRedLine = position.EntryPrice - Securiti.PriceStep * _manualControl.ProfitDistance;
                            decimal priceOrder = priceRedLine + Securiti.PriceStep * _manualControl.ProfitSlipage;

                            TryReloadProfit(position,priceRedLine,priceOrder);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
           
        }

        /// <summary>
        /// проверить, не сработал ли стоп или профит у сделки
        /// </summary>
        private bool CheckStop(Position position, decimal lastTrade)
        {
            try
            {
                if (!position.StopOrderIsActiv && !position.ProfitOrderIsActiv)
                {
                    return false;
                }

                if (ServerStatus != ServerConnectStatus.Connect ||
                    Securiti == null ||
                    Portfolio == null)
                {
                    return false;
                }

                if (position.StopOrderIsActiv)
                {

                    if (position.Direction == Side.Buy &&
                        position.StopOrderRedLine >= lastTrade)
                    { // позиция в лонг, значит стоп снизу, на продажу
                        position.ProfitOrderIsActiv = false;
                        position.StopOrderIsActiv = false;

                        CloseDeal(position, OrderPriceType.Limit, position.StopOrderPrice, _manualControl.SecondToClose, true);
                        return true;
                    }

                    if (position.Direction == Side.Sell &&
                        position.StopOrderRedLine <= lastTrade)
                    {// позиция шорт, значит стоп сверху на покупку
                        position.StopOrderIsActiv = false;
                        position.ProfitOrderIsActiv = false;
                        CloseDeal(position, OrderPriceType.Limit, position.StopOrderPrice, _manualControl.SecondToClose, true);
                        return true;
                    }
                }

                if (position.ProfitOrderIsActiv)
                {
                    if (position.Direction == Side.Buy &&
                        position.ProfitOrderRedLine <= lastTrade)
                    { // позиция в лонг, значит профит сверху, на продажу
                        position.StopOrderIsActiv = false;
                        position.ProfitOrderIsActiv = false;

                        CloseDeal(position, OrderPriceType.Limit, position.ProfitOrderPrice, _manualControl.SecondToClose, true);
                        return true;
                    }

                    if (position.Direction == Side.Sell &&
                        position.ProfitOrderRedLine >= lastTrade)
                    {// позиция шорт, значит стоп снизу на покупку
                        position.StopOrderIsActiv = false;
                        position.ProfitOrderIsActiv = false;
                        CloseDeal(position, OrderPriceType.Limit, position.ProfitOrderPrice, _manualControl.SecondToClose, true);
                        return true;
                    }
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return false;
        }

        /// <summary>
        /// проверка алертов
        /// </summary>
        private void AlertControlPosition()
        {
            try
            {
                AlertSignal signal = _alerts.CheckAlerts();

                if (signal == null)
                {
                    return;
                }

                if (signal.SignalType == SignalType.CloseAll)
                {
                    // пришёл сигнал закрыть все позиции

                    SetNewLogMessage(Securiti.Name + " Алерт кроем все позиции." + " Цена" +
                                        _connector.BestBid, LogMessageType.Signal);

                    CloseAllAtMarket();
                }

                if (signal.SignalType == SignalType.CloseOne)
                {
                    // пришёл сигнал закрыть одну позицию

                    Position position = _journal.GetPositionForNumber(signal.NumberClosingPosition);

                    if (position == null)
                    {
                        return;
                    }

                    SetNewLogMessage(Securiti.Name + " Алерт кроем позицию." + " Цена" +
                                        _connector.BestBid, LogMessageType.Signal);

                    if (signal.PriceType == OrderPriceType.Market)
                    {
                        CloseAtMarket(position, position.OpenVolume);
                    }
                    else
                    {
                        decimal price;
                        if (position.Direction == Side.Buy)
                        {
                            price = _connector.BestBid - signal.Slipage;
                        }
                        else
                        {
                            price = _connector.BestAsk + signal.Slipage;
                        }

                        CloseDeal(position, OrderPriceType.Limit, price, _manualControl.SecondToClose, true);
                    }
                }

                // проверить объём
                if (signal.SignalType == SignalType.Buy)
                {
                    SetNewLogMessage(Securiti.Name + "Алерт Сигнал Лонг " + " Цена" + _connector.BestBid, LogMessageType.Signal);

                    if (signal.PriceType == OrderPriceType.Market)
                    {
                        BuyAtMarket(signal.Volume);
                    }
                    else
                    {
                        BuyAtLimit(signal.Volume, _connector.BestAsk + signal.Slipage);
                    }
                }
                else if (signal.SignalType == SignalType.Sell)
                {
                    SetNewLogMessage(Securiti.Name + "Алерт Сигнал Шорт " + " Цена" + _connector.BestBid, LogMessageType.Signal);

                    if (signal.PriceType == OrderPriceType.Market)
                    {
                        SellAtMarket(signal.Volume);
                    }
                    else
                    {
                        SellAtLimit(signal.Volume, _connector.BestBid - signal.Slipage);
                    }
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// взять журнал
        /// </summary>
        /// <returns></returns>
        public Journal.Journal GetJournal()
        {
            return _journal;
        }

        /// <summary>
        /// добавить новый алерт в систему
        /// </summary>
        public void SetNewAlert(IIAlert alert)
        {
            _alerts.SetNewAlert(alert);
        }

        /// <summary>
        /// удалить алерт из системы
        /// </summary>
        public void DeleteAlert(IIAlert alert)
        {
            _alerts.Delete(alert);
        }

        /// <summary>
        /// удалить алерт из системы
        /// </summary>
        public void DeleteAllAlerts()
        {
            _alerts.DeleteAll();
        }

// дозакрытие сделки если на закрытии мы взяли больший объём чем нужно

        /// <summary>
        /// время когда надо совершить дозакрытие сделки
        /// </summary>
        private DateTime _lastClosingSurplusTime;

        /// <summary>
        /// проверить, не надо ли закрыть сделки по которым был перебор на закрытии
        /// </summary>
        private void CheckSurplusPositions()
        {
            if (ServerMaster.StartProgram == ServerStartProgramm.IsOsTrader && _lastClosingSurplusTime != DateTime.MinValue &&
                _lastClosingSurplusTime.AddSeconds(10) > DateTime.Now)
            {
                return;
            }

            _lastClosingSurplusTime = DateTime.Now;

            if (PositionsAll == null)
            {
                return;
            }

            if (ServerStatus != ServerConnectStatus.Connect ||
                Securiti == null ||
                Portfolio == null)
            {
                return;
            }

            bool haveSurplusPos = false;

            List<Position> positions = PositionsAll;

            if (positions == null ||
                positions.Count == 0)
            {
                return;
            }

            for (int i = positions.Count - 1; i > -1 && i > positions.Count - 10; i--)
            {
                if (positions[i].State == PositionStateType.ClosingSurplus)
                {
                    haveSurplusPos = true;
                    break;
                }
            }

            if (haveSurplusPos == false)
            {
                return;
            }

            positions = PositionsAll.FindAll(position => position.State == PositionStateType.ClosingSurplus ||
                position.OpenVolume < 0);

            if ( positions.Count == 0)
            {
                return;
            }
            bool haveOpenOrders = false;

            for (int i = 0; i < positions.Count; i++)
            {
                Position position = positions[i];

                if (position.CloseActiv)
                {
                    CloseAllOrderToPosition(position);
                    haveOpenOrders = true;
                }
            }

            if (haveOpenOrders)
            {
                return;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                Position position = positions[i];

                if (position.OpenOrders.Count > 20)
                { // чего-то не закрывается. очевидно что ордера не проходят
                    continue;
                }

                if (position.Direction == Side.Sell && position.OpenVolume < 0)
                {
                    ShortUpdate(position, PriceBestBid - Securiti.PriceStep * 30, Math.Abs(position.OpenVolume), new TimeSpan(0, 0, 1, 0), false);
                }
                if (position.Direction == Side.Buy && position.OpenVolume < 0)
                {
                    LongUpdate(position, PriceBestAsk + Securiti.PriceStep * 30, Math.Abs(position.OpenVolume), new TimeSpan(0, 0, 1, 0), false);
                }
            }
        }

// открытие сделок отложенным методом

        /// <summary>
        /// стоп - открытия ожидающие своей цены
        /// </summary>
        private List<PositionOpenerToStop> _stopsOpener;

        /// <summary>
        /// проверить, не пора ли открывать позиции по стопОткрытиям
        /// </summary>
        private void CheckStopOpener(decimal price)
        {
            if (ServerStatus != ServerConnectStatus.Connect ||
                Securiti == null ||
                Portfolio == null)
            {
                return;
            }

            try
            {
                for (int i = 0;
                    i > -1 && _stopsOpener != null && _stopsOpener.Count != 0 && i < _stopsOpener.Count;
                    i++)
                {
                    if (_stopsOpener.Count != 0)
                    {
                        if (_stopsOpener[i].ExpiresBars > 0)
                        {
                            int passedBars = CandlesFinishedOnly.Count - _stopsOpener[i].OrderCreateBarNumber;
                            if (passedBars >= _stopsOpener[i].ExpiresBars)
                            {
                                _stopsOpener.RemoveAt(i);
                                i--;
                                continue;
                            }
                        }
                        else
                        {
                            _stopsOpener.RemoveAt(i);
                            i--;
                            continue;
                        }
                    }

                    if ((_stopsOpener[i].ActivateType == StopActivateType.HigherOrEqual &&
                         price >= _stopsOpener[i].PriceRedLine) // пробили вниз
                        ||
                        (_stopsOpener[i].ActivateType == StopActivateType.LowerOrEqyal &&
                         price <= _stopsOpener[i].PriceRedLine)) // пробили вверх
                    {
                        if (_stopsOpener[i].Side == Side.Buy)
                        {
                            PositionOpenerToStop opener = _stopsOpener[i];
                            LongCreate(_stopsOpener[i].PriceOrder, _stopsOpener[i].Volume, OrderPriceType.Limit,
                                _manualControl.SecondToOpen, true);
                            _stopsOpener.Remove(opener);
                            i--;
                            continue;
                            //BuyAtLimit(_stopsOpener[i].Volume, _stopsOpener[i].PriceOrder);
                        }
                        else if (_stopsOpener[i].Side == Side.Sell)
                        {
                            PositionOpenerToStop opener = _stopsOpener[i];
                            ShortCreate(_stopsOpener[i].PriceOrder, _stopsOpener[i].Volume, OrderPriceType.Limit,
                                _manualControl.SecondToOpen, true);
                            _stopsOpener.Remove(opener);
                            i--;
                            continue;
                            //SellAtLimit(_stopsOpener[i].Volume, _stopsOpener[i].PriceOrder);
                        }
                        i--;
                    }
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// управления айсбергами

        /// <summary>
        /// мастер управления айсбергами
        /// </summary>
        private AcebergMaker _acebergMaker;

        /// <summary>
        /// мастер айсбергов требует отозвать ордер
        /// </summary>
        /// <param name="order">ордер</param>
        void _acebergMaker_NewOrderNeadToCansel(Order order)
        {
            _connector.OrderCancel(order);
        }

        /// <summary>
        /// мастер айсбергов требует выставить ордер
        /// </summary>
        /// <param name="order">ордер</param>
        void _acebergMaker_NewOrderNeadToExecute(Order order)
        {
            _connector.OrderExecute(order);
        }

        /// <summary>
        /// очистить все айсберги из системы
        /// </summary>
        public void ClearAceberg()
        {
            try
            {
                _acebergMaker.ClearAcebergs();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// обработка входящих данных

        /// <summary>
        /// пришёл новый стакан
        /// </summary>
        void _connector_GlassChangeEvent(MarketDepth marketDepth)
        {
            MarketDepth = marketDepth;

            _marketDepthPainter.ProcessMarketDepth(marketDepth);

            if (MarketDepthUpdateEvent != null)
            {
                MarketDepthUpdateEvent(marketDepth);
            }

            if (ServerMaster.StartProgram != ServerStartProgramm.IsOsTrader)
            {
                if (marketDepth.Asks == null || marketDepth.Asks.Count == 0 ||
                    marketDepth.Bids == null || marketDepth.Bids.Count == 0)
                {
                    return;
                }

                List<Position> openPositions = _journal.OpenPositions;

                if (openPositions != null)
                {
                    for (int i = 0; i < openPositions.Count; i++)
                    {
                        if (openPositions[i].State != PositionStateType.Open)
                        {
                            continue;
                        }
                        CheckStop(openPositions[i], marketDepth.Asks[0].Price);

                        if (openPositions.Count <= i)
                        {
                            continue;
                        }
                        CheckStop(openPositions[i], marketDepth.Bids[0].Price);
                    }
                }
            }
        }

        /// <summary>
        /// пора закрывать ордер у этой сделки
        /// </summary>
        /// <param name="order">ордер</param>
        /// <param name="deal">позиция</param>
        private void _dealOpeningWatcher_DontOpenOrderDetectedEvent(Order order, Position deal)
        {// событие приходит если ордер на открытие или закрытие не правильно исполнился
            try
            {
                _connector.OrderCancel(order);

                SetNewLogMessage("Робот " + TabName + " Отозвали ордер по времени, номер " + order.NumberMarket,
                    LogMessageType.Trade);

                for (int i = 0; deal.CloseOrders != null && i < deal.CloseOrders.Count; i++)
                {
                    if (order.NumberUser == deal.CloseOrders[i].NumberUser && _manualControl.DoubleExitIsOn)
                    {
                        CloseAtMarket(deal, deal.OpenVolume);
                    }
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// изменился статус сделки
        /// </summary>
        /// <param name="position">позиция</param>
        private void _journal_PositionStateChangeEvent(Position position)
        {
            try
            {
                if (position.State == PositionStateType.Done)
                { // сделка закрыта штатно
                    CloseAllOrderToPosition(position);

                    if (PositionClosingSuccesEvent != null)
                    {
                        PositionClosingSuccesEvent(position);
                    }

                    if (ServerMaster.StartProgram == ServerStartProgramm.IsOsTrader)
                    {
                        SetNewLogMessage("Робот " + TabName + " Закрытие сделки номер " + position.Number + ". Инструмент: " +
                                   Securiti.Name + "Закрытый объём: " + position.MaxVolume, LogMessageType.Trade);
                    }

                    else if (ServerMaster.StartProgram == ServerStartProgramm.IsTester)
                    {
                        decimal profit = position.ProfitPortfolioPunkt;

                        ((TesterServer)_connector.MyServer).AddProfit(profit);
                    }
                    else if (ServerMaster.StartProgram == ServerStartProgramm.IsOsOptimizer)
                    {
                        decimal profit = position.ProfitPortfolioPunkt;

                        ((OptimizerServer)_connector.MyServer).AddProfit(profit);
                    }
                }
                else if (position.State == PositionStateType.OpeningFail)
                { // ОШИБКА НА ОТКРЫТИИ
                    SetNewLogMessage("Робот " + TabName + " Сделка не открылась номер " + position.Number, LogMessageType.System);

                    if (PositionOpeningFailEvent != null)
                    {
                        PositionOpeningFailEvent(position);
                    }
                }
                else if (position.State == PositionStateType.Open)
                {
                    SetNewLogMessage("Робот " + TabName + " Открытие сделки номер " + position.Number + ". Инструмент: " +
                        Securiti.Name + "Объём: " + position.MaxVolume, LogMessageType.Trade);
                    if (PositionOpeningSuccesEvent != null)
                    {
                        PositionOpeningSuccesEvent(position);
                    }
                    ManualReloadStopsAndProfitToPosition(position);
                }
                else if (position.State == PositionStateType.ClosingFail)
                { // ОШИБКА НА ЗАКРЫТИИ
                    if (ServerMaster.StartProgram == ServerStartProgramm.IsOsTrader)
                    {
                        SetNewLogMessage("Робот " + TabName + " Сделка не закрылась номер " + position.Number, LogMessageType.System);
                    }

                    if (_manualControl.DoubleExitIsOn &&
                        position.CloseOrders.Count < 5)
                    {
                        if (_manualControl.TypeDoubleExitOrder == OrderPriceType.Market)
                        {
                            CloseAtMarket(position, position.OpenVolume);
                        } 
                        else if(_manualControl.TypeDoubleExitOrder == OrderPriceType.Limit)
                        {
                            decimal price;
                            if (position.Direction == Side.Buy)
                            {
                                price = PriceBestBid - Securiti.PriceStep*_manualControl.DoubleExitSlipage;
                            }
                            else
                            {
                                price = PriceBestAsk + Securiti.PriceStep * _manualControl.DoubleExitSlipage;
                            }

                            CloseAtLimit(position, price, position.OpenVolume);
                        }
                    }

                    if (PositionClosingFailEvent != null)
                    {
                        PositionClosingFailEvent(position);
                    }
                }

                _chartMaster.SetPosition(PositionsAll);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// изменился открытый объём по сделке
        /// </summary>
        /// <param name="position">позиция</param>
        void _journal_PositionNetVolumeChangeEvent(Position position)
        {
            if (PositionNetVolumeChangeEvent != null)
            {
                PositionNetVolumeChangeEvent(position);
            }
            //SetNewLogMessage("Pos num " + position.Number + "Vol " + position.OpenVolume, LogMessageType.Error);
        }

        /// <summary>
        /// завершилась свеча
        /// </summary>
        /// <param name="candles">свечи</param>
        private void LogicToEndCandle(List<Candle> candles)
        {
            try
            {
                if (candles == null)
                {
                    return;
                }
                AlertControlPosition();

                if (_stopsOpener != null &&
                    _stopsOpener.Count != 0)
                {
                    //_stopsOpener.Clear();
                }

                _chartMaster.SetCandles(candles);

                if (CandleFinishedEvent != null)
                {
                    CandleFinishedEvent(candles);
                }
               
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// обновилась последняя свеча
        /// </summary>
        /// <param name="candles">свеча</param>
        private void LogicToUpdateLastCandle(List<Candle> candles)
        {
            try
            {
                AlertControlPosition();

                _chartMaster.SetCandles(candles);
                if (CandleUpdateEvent != null)
                {
                    CandleUpdateEvent(candles);
                }
                
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// пользователь из журнала заказывает манипулящию с позицией
        /// </summary>
        /// <param name="position">позиция</param>
        /// <param name="signalType">тип манипуляции</param>
        private void _journal_UserSelectActionEvent(Position position, SignalType signalType)
        {
            try
            {
                if (signalType == SignalType.CloseAll)
                {
                    SetNewLogMessage("Робот " + TabName + " Пользователь заказал закрытие всех сделок", LogMessageType.User);

                    CloseAllAtMarket();
                }

                if (signalType == SignalType.CloseOne)
                {
                    if (position == null)
                    {
                        return;
                    }

                    ShowClosePositionDialog(position);
                }

                if (signalType == SignalType.ReloadProfit)
                {
                    if (position == null)
                    {
                        return;
                    }
                    ShowProfitSendDialog(position);
                }

                if (signalType == SignalType.ReloadStop)
                {
                    if (position == null)
                    {
                        return;
                    }
                    ShowStopSendDialog(position);
                }

                if (signalType == SignalType.OpenNew)
                {
                    ShowOpenPositionDialog();
                }

                if (signalType == SignalType.Modificate)
                {
                    if (position == null)
                    {
                        return;
                    }
                    ShowPositionModificateDialog(position);
                }

                if (signalType == SignalType.DeletePos)
                {
                    if (position == null)
                    {
                        return;
                    }
                    _journal.DeletePosition(position);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// стартовала ли сегодня сессия
        /// </summary>
        private bool _firstTickToDaySend;

        /// <summary>
        /// последний индекс тика
        /// </summary>
        private int _lastTickIndex;

        /// <summary>
        /// пришли новые тики
        /// </summary>
        /// <param name="trades">тики</param>
        private void _connector_TickChangeEvent(List<Trade> trades)
        {
            if (trades == null || 
                trades.Count == 0)
            {
                return;
            }

            if (_chartMaster == null)
            {
                return;
            }

            _chartMaster.SetTick(trades);

            Trade trade = trades[trades.Count - 1];

            if (_firstTickToDaySend == false && FirstTickToDayEvent != null)
            {
                // высылаем событие начала сессии
                if (trade.Time.Hour == 10
                    && (trade.Time.Minute == 1 || trade.Time.Minute == 0))
                {
                    _firstTickToDaySend = true;
                    FirstTickToDayEvent(trade);
                }
            }

            if (_lastTickIndex == 0)
            {
                _lastTickIndex = trades.Count - 1;
                return;
            }

            int curCount = trades.Count;

            List<Position> openPositions = _journal.OpenPositions;

            if (openPositions != null)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    for (int i2 = _lastTickIndex; i < openPositions.Count && i2 < curCount && trades[i2] != null; i2++)
                    {
                        if (CheckStop(openPositions[i], trades[i2].Price))
                        {
                            if (ServerMaster.StartProgram != ServerStartProgramm.IsOsTrader)
                            {
                                i--;
                            }
                            break;
                        }
                    }
                }
            }

            for (int i2 = _lastTickIndex; i2 < curCount && trades[i2] != null; i2++)
            {
                if (trades[i2] == null)
                {
                    trades.RemoveAt(i2);
                    return;
                }
                CheckStopOpener(trades[i2].Price);

                if (NewTickEvent != null)
                {
                    // высылаем событие "новые тики"
                    try
                    {
                        NewTickEvent(trades[i2]);
                    }
                    catch (Exception error)
                    {
                        SetNewLogMessage(error.ToString(), LogMessageType.Error);
                    }

                }
            }

            _lastTickIndex = curCount-1;

            if (ServerMaster.StartProgram == ServerStartProgramm.IsOsTrader)
            {
                CheckSurplusPositions();
            }

        }

        /// <summary>
        /// входящая моя сделка
        /// </summary>
        /// <param name="trade">сделка</param>
        private void _connector_MyTradeEvent(MyTrade trade)
        {
            _journal.SetNewMyTrade(trade);
        }

        /// <summary>
        /// изменилось время сервера
        /// </summary>
        /// <param name="time">новое время</param>
        void StrategOneSecurity_TimeServerChangeEvent(DateTime time)
        {
            if (_manualControl != null)
            {
                _manualControl.ServerTime = time;
            }
            
            if (ServerTimeChangeEvent != null)
            {
                ServerTimeChangeEvent(time);
            }
        }

        /// <summary>
        /// входящие ордера
        /// </summary>
        /// <param name="order">ордер</param>
        private void _connector_OrderChangeEvent(Order order)
        {
            if (_journal.IsMyOrder(order) == false)
            {
                return;
            }
            _journal.SetNewOrder(order);
            _acebergMaker.SetNewOrder(order);

            if (OrderUpdateEvent != null)
            {
                OrderUpdateEvent(order);
            }
        }

        /// <summary>
        /// входящие новые бид с аском
        /// </summary>
        /// <param name="bestBid">лучшая продажа</param>
        /// <param name="bestAsk">лучшая покупка</param>
        private void _connector_BestBidAskChangeEvent(decimal bestBid, decimal bestAsk)
        {
            _journal.SetNewBidAsk(bestBid, bestAsk);

            _marketDepthPainter.ProcessBidAsk(bestBid,bestAsk);

            if (BestBidAskChangeEvent != null)
            {
                BestBidAskChangeEvent(bestBid, bestAsk);
            }
        }

// исходящие события. Обработчики для стратегии

        /// <summary>
        /// утренняя сессия стартовала. Пошли первые тики
        /// </summary>
        public event Action<Trade> FirstTickToDayEvent;

        /// <summary>
        /// пришли новые тики
        /// </summary>
        public event Action<Trade> NewTickEvent;

        /// <summary>
        /// изменилось время сервера
        /// </summary>
        public event Action<DateTime> ServerTimeChangeEvent;
  
        /// <summary>
        /// завершилась новая свечка
        /// </summary>
        public event Action<List<Candle>> CandleFinishedEvent;

        /// <summary>
        /// обновилась последняя свечка
        /// </summary>
        public event Action<List<Candle>> CandleUpdateEvent;

        /// <summary>
        /// пришёл новый стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthUpdateEvent;

        /// <summary>
        /// изменился лучший бид/аск (лучшая цена продажи, лучшая цена покупки)
        /// </summary>
        public event Action<decimal, decimal> BestBidAskChangeEvent;

        /// <summary>
        /// позиция успешно закрыта
        /// </summary>
        public event Action<Position> PositionClosingSuccesEvent;

        /// <summary>
        /// позиция успешно открыта
        /// </summary>
        public event Action<Position> PositionOpeningSuccesEvent;

        /// <summary>
        /// у позиции изменился открытый объём. 
        /// Вызывается каждый раз когда по ордерам позиции проходит какой-то трейд.
        /// </summary>
        public event Action<Position> PositionNetVolumeChangeEvent;

        /// <summary>
        /// открытие позиции не случилось
        /// </summary>
        public event Action<Position> PositionOpeningFailEvent;

        /// <summary>
        /// закрытие позиции не прошло
        /// </summary>
        public event Action<Position> PositionClosingFailEvent;

        /// <summary>
        /// вызывается в момент когда экземпляр данного бота удаляется.
        /// нужно использовать чтобы удалить 
        /// </summary>
        public event Action<int> DeleteBotEvent;

        /// <summary>
        /// в системе обновился какой-то ордер
        /// </summary>
        public event Action<Order> OrderUpdateEvent;

    }

    /// <summary>
    /// объект отправляющий позиции на второй круг
    /// Отправляются они на второй круг для того чтобы подгрузились трэйды сделки, чтобы определить цену входа
    /// иначе сделка получается Open, но цена входа не известна. Т.к. ордера(Order) иногда приходят раньше сделок(MyTrade)
    /// </summary>
    public class PositionToSecondLoopSender
    {
        /// <summary>
        /// позиция
        /// </summary>
        public Position Position;

        /// <summary>
        /// начать ожидание
        /// </summary>
        public void Start()
        {
            Thread.Sleep(3000);
            if (PositionNeadToStopSend != null)
            {
                PositionNeadToStopSend(Position);
            }
        }

        /// <summary>
        /// выслать оповещение с новой сделкой
        /// </summary>
        public event Action<Position> PositionNeadToStopSend;

    }

    /// <summary>
    /// тип активации стоп приказа
    /// </summary>
    public enum StopActivateType
    {

        /// <summary>
        /// активировать когда цена будет выше или равно
        /// </summary>
        HigherOrEqual,

        /// <summary>
        /// активировать когда цена будет ниже или равно
        /// </summary>
        LowerOrEqyal
    }
}
