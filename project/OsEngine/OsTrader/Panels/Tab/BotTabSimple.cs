/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Alerts;
using OsEngine.Charts.CandleChart;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels.Tab.Internal;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// trading tab / 
    /// вкладка для торговли 
    /// </summary>
    public class BotTabSimple : IIBotTab
    {
        /// <summary>
        /// constructor / 
        /// конструктор
        /// </summary>
        public BotTabSimple(string name, StartProgram startProgram)
        {
            TabName = name;
            StartProgram = startProgram;

            try
            {
                _connector = new ConnectorCandles(TabName, startProgram);
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

                _journal = new Journal.Journal(TabName, startProgram);

                _journal.PositionStateChangeEvent += _journal_PositionStateChangeEvent;
                _journal.PositionNetVolumeChangeEvent += _journal_PositionNetVolumeChangeEvent;
                _journal.UserSelectActionEvent += _journal_UserSelectActionEvent;
                _journal.LogMessageEvent += SetNewLogMessage;

                _connector.ComissionType = _journal.ComissionType;
                _connector.ComissionValue = _journal.ComissionValue;

                _chartMaster = new ChartCandleMaster(TabName, StartProgram);
                _chartMaster.LogMessageEvent += SetNewLogMessage;
                _chartMaster.SetNewSecurity(_connector.NamePaper, _connector.TimeFrameBuilder, _connector.PortfolioName, _connector.ServerType);
                _chartMaster.SetPosition(_journal.AllPosition);

                if (StartProgram != StartProgram.IsOsOptimizer)
                {
                    _alerts = new AlertMaster(TabName, _connector, _chartMaster);
                    _alerts.LogMessageEvent += SetNewLogMessage;
                }
                _dealCreator = new PositionCreator();

                ManualPositionSupport = new BotManualControl(TabName, this, startProgram);
                ManualPositionSupport.LogMessageEvent += SetNewLogMessage;
                ManualPositionSupport.DontOpenOrderDetectedEvent += _dealOpeningWatcher_DontOpenOrderDetectedEvent;

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
        /// the connector has started the reconnection procedure / 
        /// коннектор запустил процедуру переподключения
        /// </summary>
        /// <param name="securityName">security name / имя бумаги</param>
        /// <param name="timeFrame">timeframe DateTime/ таймфрейм бумаги</param>
        /// <param name="timeFrameSpan">timeframe TimeSpan / таймфрейм в виде времени</param>
        /// <param name="portfolioName">porrtfolio name / номер портфеля</param>
        /// <param name="serverType">server type / тип сервера у коннектора</param>
        void _connector_ConnectorStartedReconnectEvent(string securityName, TimeFrame timeFrame, TimeSpan timeFrameSpan, string portfolioName, ServerType serverType)
        {
            _chartMaster.ClearTimePoints();
            if (string.IsNullOrEmpty(securityName))
            {
                return;
            }

            _chartMaster.SetNewSecurity(securityName, _connector.TimeFrameBuilder, portfolioName, serverType);
        }

        // control / управление

        /// <summary>
        /// start drawing this robot / 
        /// начать прорисовку этого робота
        /// </summary> 
        public void StartPaint(Grid gridChart, WindowsFormsHost hostChart, WindowsFormsHost hostGlass, WindowsFormsHost hostOpenDeals,
                     WindowsFormsHost hostCloseDeals, Rectangle rectangleChart, WindowsFormsHost hostAlerts, TextBox textBoxLimitPrice, Grid gridChartControlPanel)
        {
            try
            {
                _chartMaster?.StartPaint(gridChart, hostChart, rectangleChart);
                _marketDepthPainter?.StartPaint(hostGlass, textBoxLimitPrice);
                _journal?.StartPaint(hostOpenDeals, hostCloseDeals);

                _alerts?.StartPaint(hostAlerts);

                _chartMaster?.StartPaintChartControlPanel(gridChartControlPanel);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// stop drawing this robot / 
        /// остановить прорисовку этого робота
        /// </summary>
        public void StopPaint()
        {
            try
            {
                _chartMaster?.StopPaint();
                _marketDepthPainter?.StopPaint();
                _journal?.StopPaint();
                _alerts?.StopPaint();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// unique robot name / 
        /// уникальное имя робота
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// tab num /
        /// номер вкладки
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
        /// clear data in the robot / 
        /// очистить данные в роботе
        /// </summary>
        public void Clear()
        {
            try
            {
                ClearAceberg();
                BuyAtStopCancel();
                SellAtStopCancel();
                _journal.Clear();
                _chartMaster.Clear();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// remove the robot and all child structures / 
        /// удалить робота и все дочерние структуры
        /// </summary>
        public void Delete()
        {
            try
            {
                _connector.OrderChangeEvent -= _connector_OrderChangeEvent;
                _connector.MyTradeEvent -= _connector_MyTradeEvent;
                _connector.BestBidAskChangeEvent -= _connector_BestBidAskChangeEvent;
                _connector.GlassChangeEvent -= _connector_GlassChangeEvent;
                _connector.TimeChangeEvent -= StrategOneSecurity_TimeServerChangeEvent;
                _connector.NewCandlesChangeEvent -= LogicToEndCandle;
                _connector.LastCandlesChangeEvent -= LogicToUpdateLastCandle;
                _connector.TickChangeEvent -= _connector_TickChangeEvent;
                _connector.LogMessageEvent -= SetNewLogMessage;
                _connector.ConnectorStartedReconnectEvent -= _connector_ConnectorStartedReconnectEvent;

                _journal.PositionStateChangeEvent -= _journal_PositionStateChangeEvent;
                _journal.PositionNetVolumeChangeEvent -= _journal_PositionNetVolumeChangeEvent;
                _journal.UserSelectActionEvent -= _journal_UserSelectActionEvent;
                _journal.LogMessageEvent -= SetNewLogMessage;
                _chartMaster.LogMessageEvent -= SetNewLogMessage;

                if (_alerts != null)
                {
                    _alerts.LogMessageEvent -= SetNewLogMessage;
                    _alerts.DeleteAll();
                }

                ManualPositionSupport.LogMessageEvent -= SetNewLogMessage;
                ManualPositionSupport.DontOpenOrderDetectedEvent -= _dealOpeningWatcher_DontOpenOrderDetectedEvent;
                _acebergMaker.NewOrderNeadToExecute -= _acebergMaker_NewOrderNeadToExecute;
                _acebergMaker.NewOrderNeadToCansel -= _acebergMaker_NewOrderNeadToCansel;

                _journal.Delete();
                _connector.Delete();
                ManualPositionSupport.Delete();
                _chartMaster.Delete();

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
        /// whether the connector is connected to download data / 
        /// подключен ли коннектор на скачивание данных
        /// </summary>
        public bool IsConnected
        {
            get { return _connector.IsConnected; }
        }

        /// <summary>
        /// connector is ready to send Orders / 
        /// готов ли коннектор к выставленю заявок
        /// </summary>
        public bool IsReadyToTrade
        {
            get { return _connector.IsReadyToTrade; }
        }

        /// <summary>
        /// the program that created the object / 
        /// программа создавшая объект
        /// </summary>
        public StartProgram StartProgram;

        // logging / работа с логом

        /// <summary>
        /// put a new message in the log / 
        /// положить в лог новое сообщение
        /// </summary>
        public void SetNewLogMessage(string message, LogMessageType messageType)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, messageType);
            }
            else if (messageType == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing message for log / 
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        // indicator management / менеджмент индикаторов

        /// <summary>
        /// create indicator / 
        /// создать индикатор
        /// </summary>
        /// <param name="indicator">indicator / индикатор</param>
        /// <param name="nameArea">the name of the area on which it will be placed. Default: "Prime" / название области на которую он будет помещён. По умолчанию: "Prime"</param>
        /// <returns></returns>
        public IIndicator CreateCandleIndicator(IIndicator indicator, string nameArea)
        {
            return _chartMaster.CreateIndicator(indicator, nameArea);
        }

        /// <summary>
        /// create and save indicator / 
        /// создать и сохранить индикатор
        /// </summary>
        /// <param name="indicator">indicator / индикатор</param>
        /// <param name="nameArea">the name of the area on which it will be placed. Default: "Prime" / название области на которую он будет помещён. По умолчанию: "Prime"</param>
        /// <returns></returns>
        public T CreateIndicator<T>(T indicator, string nameArea = "Prime") where T : IIndicator
        {
            T newIndicator = (T)_chartMaster.CreateIndicator(indicator, nameArea);
            newIndicator.Save();
            return newIndicator;
        }

        /// <summary>
        /// remove indicator / 
        /// удалить индикатор 
        /// </summary>
        public void DeleteCandleIndicator(IIndicator indicator)
        {
            _chartMaster.DeleteIndicator(indicator);
        }

        /// <summary>
        /// all available indicators in the system / 
        /// все доступные индикаторы в системе
        /// </summary>
        public List<IIndicator> Indicators
        {
            get { return _chartMaster.Indicators; }
        }

        // drawing elements / рисование элементов

        /// <summary>
        /// add custom element to the chart / 
        /// добавить на график пользовательский элемент
        /// </summary>
        public void SetChartElement(IChartElement element)
        {
            _chartMaster.SetChartElement(element);
        }

        /// <summary>
        /// remove user element from chart / 
        /// удалить с графика пользовательский элемент
        /// </summary>
        public void DeleteChartElement(IChartElement element)
        {
            _chartMaster.DeleteChartElement(element);
        }

        /// <summary>
        /// remove all custom elements from the graphic / 
        /// удалить все пользовательские элементы с графика
        /// </summary>
        public void DeleteAllChartElement()
        {
            _chartMaster.DeleteAllChartElement();
        }

        /// <summary>
        /// get chart information
        /// получить информацию о чарте
        /// </summary>
        public string GetChartLabel()
        {
            return _chartMaster.GetChartLabel();
        }

        // closed components / закрытые составные части

        /// <summary>
        /// class responsible for connecting the tab to the exchange
        /// класс отвечающий за подключение вкладки к бирже
        /// </summary>
        public ConnectorCandles Connector
        {
            get { return _connector; }
        }
        private ConnectorCandles _connector;

        /// <summary>
        /// an object that holds settings for assembling candles / 
        /// объект хранящий в себе настройки для сборки свечей
        /// </summary>
        public TimeFrameBuilder TimeFrameBuilder
        {
            get { return _connector.TimeFrameBuilder; }
        }

        /// <summary>
        /// chart drawing master / 
        /// мастер прорисовки чарта
        /// </summary>
        private ChartCandleMaster _chartMaster;

        /// <summary>
        /// class drawing a marketDepth / 
        /// класс прорисовывающий движения стакана котировок
        /// </summary>
        private MarketDepthPainter _marketDepthPainter;

        /// <summary>
        /// transaction creation wizard / 
        /// мастер создания сделок
        /// </summary>
        private PositionCreator _dealCreator;

        /// <summary>
        /// Journal positions / 
        /// журнал
        /// </summary>
        private Journal.Journal _journal;

        /// <summary>
        /// settings maintenance settings / 
        /// настройки ручного сопровождения
        /// </summary>
        public BotManualControl ManualPositionSupport;

        /// <summary>
        /// alerts wizard /
        /// мастер Алертов
        /// </summary>
        private AlertMaster _alerts;

        // properties / свойства 

        /// <summary>
        ///  the status of the server to which the tab is connected /
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
        /// security to trading / 
        /// инструмент для торговли
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
        /// timeframe data received / 
        /// таймФрейм получаемых данных
        /// </summary>
        public TimeSpan TimeFrame
        {
            get { return _connector.TimeFrameTimeSpan; }
        }

        /// <summary>
        /// trading account / 
        /// счёт для торговли
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
        /// тип комиссии для позиций
        /// </summary>
        public ComissionType ComissionType
        {
            get { return _journal.ComissionType; }
            set { _journal.ComissionType = value; }
        }

        /// <summary>
        /// размер комиссии
        /// </summary>
        public decimal ComissionValue
        {
            get { return _journal.ComissionValue; }
            set { _journal.ComissionValue = value; }
        }

        /// <summary>
        /// All positions are owned by bot. Open, closed and with errors / 
        /// все позиции принадлежащие боту. Открытые, закрытые и с ошибками
        /// </summary>
        public List<Position> PositionsAll
        {
            get { return _journal.AllPosition; }
        }

        /// <summary>
        /// all open, partially open and opening positions owned by bot
        /// все открытые, частично открытые и открывающиеся позиции принадлежащие боту
        /// </summary>
        public List<Position> PositionsOpenAll
        {
            get { return _journal.OpenPositions; }
        }

        /// <summary>
        /// stop-limit orders
        /// все ожидающие цены ордера бота
        /// </summary>
        public List<PositionOpenerToStop> PositionOpenerToStopsAll
        {
            get { return _stopsOpener; }
        }

        /// <summary>
        /// all closed, error positions owned by bot / 
        /// все закрытые, с ошибками позиции принадлежащие боту
        /// </summary>
        public List<Position> PositionsCloseAll
        {
            get { return _journal.CloseAllPositions; }
        }

        /// <summary>
        /// last open position / 
        /// последняя открытая позиция
        /// </summary>
        public Position PositionsLast
        {
            get { return _journal.LastPosition; }
        }

        /// <summary>
        /// all open positions are short / 
        /// все открытые позиции шорт
        /// </summary>
        public List<Position> PositionOpenShort
        {
            get { return _journal.OpenAllShortPositions; }
        }

        /// <summary>
        /// all open positions long / 
        /// все открытые позиции лонг
        /// </summary>
        public List<Position> PositionOpenLong
        {
            get { return _journal.OpenAllLongPositions; }
        }

        /// <summary>
        /// exchange position for security
        /// позиция на бирже по инструменту
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
        /// net position recruited by the robot / 
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
                    SetNewLogMessage(error.ToString(), LogMessageType.Error);
                    return 0;
                }
            }
        }

        /// <summary>
        /// were there closed positions on the current bar / 
        /// были ли закрытые позиции на текущем баре
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
        /// all candles of the instrument. Both molded and completed / 
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
        /// all candles of the instrument. Only completed / 
        /// все свечи инструмента. Только завершённые
        /// </summary>
        public List<Candle> CandlesFinishedOnly
        {
            get { return _connector.Candles(true); }
        }

        /// <summary>
        /// all instrument trades / 
        /// все тики по инструменту
        /// </summary>
        public List<Trade> Trades
        {
            get { return _connector.Trades; }
        }

        /// <summary>
        /// server time / 
        /// текущее время сервера
        /// </summary>
        public DateTime TimeServerCurrent
        {
            get { return _connector.MarketTime; }
        }

        /// <summary>
        /// marketDepth / 
        /// стакан по инструменту
        /// </summary>
        public MarketDepth MarketDepth { get; set; }

        /// <summary>
        /// best selling price / 
        /// лучшая цена продажи инструмента
        /// </summary>
        public decimal PriceBestAsk
        {
            get { return _connector.BestAsk; }
        }

        /// <summary>
        /// best buy price / 
        /// лучшая цена покупки инструмента этой вкладки
        /// </summary>
        public decimal PriceBestBid
        {
            get { return _connector.BestBid; }
        }

        /// <summary>
        /// marketDepth center price /
        /// цена центра стакана
        /// </summary>
        public decimal PriceCenterMarketDepth
        {
            get
            {
                return (_connector.BestAsk + _connector.BestBid) / 2;
            }
        }

        // call control windows / вызыв окон управления

        /// <summary>
        /// show connector settings window / 
        /// показать окно настроек коннектора
        /// </summary>
        public void ShowConnectorDialog()
        {
            _connector.ShowDialog(true);

            _journal.ComissionType = _connector.ComissionType;
            _journal.ComissionValue = _connector.ComissionValue;
        }

        /// <summary>
        /// show custom settings window / 
        /// показать индивидуальное окно настроек
        /// </summary>
        public void ShowManualControlDialog()
        {
            ManualPositionSupport.ShowDialog();
        }

        /// <summary>
        /// show position closing window / 
        /// показать окно закрытия позиции
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
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
        /// show position opening window / 
        /// показать окно открытия позиции
        /// </summary>
        public void ShowOpenPositionDialog()
        {
            try
            {
                if (Securiti == null ||
                    _connector.IsConnected == false)
                {
                    return;
                }

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
        /// show window for position modification / 
        /// показать окно для модификации позиции
        /// </summary>
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

                if (ui.OpenType == PositionOpenType.Market)
                {
                    if (ui.Side == Side.Buy)
                    {
                        if (position.Direction == Side.Buy)
                        {
                            BuyAtMarketToPosition(position, ui.Volume);
                        }
                        else
                        {
                            if (position.OpenVolume > ui.Volume)
                            {
                                CloseAtMarket(position, ui.Volume);
                            }
                            else
                            {
                                CloseAtMarket(position, position.OpenVolume);
                            }
                        }
                    }
                    else
                    {
                        if (position.Direction == Side.Sell)
                        {
                            SellAtMarketToPosition(position, ui.Volume);
                        }
                        else
                        {
                            if (position.OpenVolume > ui.Volume)
                            {
                                CloseAtMarket(position, ui.Volume);
                            }
                            else
                            {
                                CloseAtMarket(position, position.OpenVolume);
                            }
                        }
                    }
                }

                else if (ui.OpenType == PositionOpenType.Limit ||
                    ui.OpenType == PositionOpenType.Aceberg && ui.CountAcebertOrder == 1)
                {
                    if (ui.Side == Side.Buy)
                    {
                        if (position.Direction == Side.Buy)
                        {
                            BuyAtLimitToPosition(position, ui.Price, ui.Volume);
                        }
                        else
                        {
                            if (position.OpenVolume > ui.Volume)
                            {
                                CloseAtLimit(position, ui.Price, ui.Volume);
                            }
                            else
                            {
                                CloseAtLimit(position, ui.Price, position.OpenVolume);
                            }
                        }
                    }
                    else
                    {
                        if (position.Direction == Side.Sell)
                        {
                            SellAtLimitToPosition(position, ui.Price, ui.Volume);
                        }
                        else
                        {
                            if (position.OpenVolume > ui.Volume)
                            {
                                CloseAtLimit(position, ui.Price, ui.Volume);
                            }
                            else
                            {
                                CloseAtLimit(position, ui.Price, position.OpenVolume);
                            }
                        }
                    }
                }
                else if (ui.OpenType == PositionOpenType.Aceberg)
                {
                    if (ui.Side == Side.Buy)
                    {
                        if (position.Direction == Side.Buy)
                        {
                            BuyAtAcebergToPosition(position, ui.Price, ui.Volume, ui.CountAcebertOrder);
                        }
                        else
                        {
                            if (position.OpenVolume > ui.Volume)
                            {
                                CloseAtAceberg(position, ui.Price, ui.Volume, ui.CountAcebertOrder);
                            }
                            else
                            {
                                CloseAtAceberg(position, ui.Price, position.OpenVolume, ui.CountAcebertOrder);
                            }
                        }
                    }
                    else
                    {
                        if (position.Direction == Side.Sell)
                        {
                            SellAtAcebergToPosition(position, ui.Price, ui.Volume, ui.CountAcebertOrder);
                        }
                        else
                        {
                            if (position.OpenVolume > ui.Volume)
                            {
                                CloseAtAceberg(position, ui.Price, ui.Volume, ui.CountAcebertOrder);
                            }
                            else
                            {
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
        /// show stop order window 
        /// показать окно выставления стопа для позиции
        /// </summary>
        public void ShowStopSendDialog(Position position)
        {
            try
            {
                PositionStopUi ui = new PositionStopUi(position, _connector.BestBid, OsLocalization.Trader.Label107);
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
        /// show profit order window 
        /// показать окно выставления профита для позиции
        /// </summary>
        public void ShowProfitSendDialog(Position position)
        {
            try
            {
                PositionStopUi ui = new PositionStopUi(position, _connector.BestBid, OsLocalization.Trader.Label110);
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
        /// move the graph to the current time / 
        /// переместить график к текущему времени
        /// </summary>
        public void GoChartToThisTime(DateTime time)
        {
            _chartMaster.GoChartToTime(time);
        }

        // standard public functions for position management
        // стандартные публичные функции для управления позицией

        private bool IsMarketOrderSupport()
        {
            if (_connector.ServerType == ServerType.InteractivBrokers ||
                _connector.ServerType == ServerType.Lmax ||
                _connector.ServerType == ServerType.BitMax ||
                _connector.ServerType == ServerType.FTX ||
                _connector.ServerType == ServerType.BinanceFutures)
            {
                return true;
            }

            return false;
        }
        private bool IsMarketStopOrderSupport()
        {
            if (_connector.ServerType == ServerType.BinanceFutures)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// enter a long position at any price / 
        /// войти в позицию Лонг по любой цене
        /// </summary>
        /// <param name="volume">volume / объём которым следует войти</param>
        public Position BuyAtMarket(decimal volume)
        {
            try
            {
                decimal price = _connector.BestAsk;

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label62, LogMessageType.System);
                    return null;
                }

                price = price + Securiti.PriceStep * 20;

                OrderPriceType type = OrderPriceType.Market;

                TimeSpan timeLife = ManualPositionSupport.SecondToOpen;

                if (IsMarketOrderSupport())
                {
                    return LongCreate(price, volume, type, timeLife, false);
                }
                else
                {
                    return BuyAtLimit(volume, price);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;

        }

        /// <summary>
        /// enter a long position at any price / 
        /// войти в позицию Лонг по любой цене
        /// </summary>
        /// <param name="volume">volume / объём которым следует войти</param>
        /// <param name="signalType">open position signal name / название сигнала для входа </param>
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
        /// enter position Long at a limit price
        /// войти в позицию Лонг по определённой цене
        /// </summary>
        /// <param name="volume">volume / объём позиции</param>
        /// <param name="priceLimit">order price / цена выставляемой заявки</param>
        public Position BuyAtLimit(decimal volume, decimal priceLimit)
        {
            try
            {
                return LongCreate(priceLimit, volume, OrderPriceType.Limit, ManualPositionSupport.SecondToOpen, false);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// enter position Long at a limit price
        /// войти в позицию Лонг по определённой цене
        /// </summary>
        /// <param name="volume">volume / объём позиции</param>
        /// <param name="priceLimit">opder price / цена выставляемой заявки</param>
        /// <param name="signalType">>open position signal name / название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public Position BuyAtLimit(decimal volume, decimal priceLimit, string signalType)
        {
            Position position = BuyAtLimit(volume, priceLimit);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// enter position Long at iceberg / 
        /// войти в позицию Лонг айсбергом
        /// </summary>
        /// <param name="volume">volume / объём позиции</param>
        /// <param name="price">order price / цена выставляемой заявки</param>
        /// /// <param name="orderCount">iceberg orders count / количество ордеров в айсберге</param>
        public Position BuyAtAceberg(decimal volume, decimal price, int orderCount)
        {
            try
            {
                if (StartProgram != StartProgram.IsOsTrader || orderCount <= 1)
                {
                    return BuyAtLimit(volume, price);
                }

                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63, LogMessageType.System);
                    return null;
                }

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label62, LogMessageType.System);
                    return null;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                if (Securiti != null)
                {
                    if (Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    {
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
                    {
                        int point = lastPrice.ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                    }
                }

                Position newDeal = new Position();
                newDeal.Number = NumberGen.GetNumberDeal(StartProgram);
                newDeal.Direction = Side.Buy;
                newDeal.State = PositionStateType.Opening;

                newDeal.NameBot = TabName;
                newDeal.Lots = Securiti.Lot;
                newDeal.PriceStepCost = Securiti.PriceStepCost;
                newDeal.PriceStep = Securiti.PriceStep;
                newDeal.PortfolioValueOnOpenPosition = Portfolio.ValueCurrent;

                _journal.SetNewDeal(newDeal);

                _acebergMaker.MakeNewAceberg(price, ManualPositionSupport.SecondToOpen, orderCount, newDeal, AcebergType.Open, volume, this);

                return newDeal;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// enter position Long at iceberg / 
        /// войти в позицию Лонг айсбергом
        /// </summary>
        /// <param name="volume">volume / объём позиции</param>
        /// <param name="price">order price / цена выставляемой заявки</param>
        /// /// <param name="orderCount">iceberg orders count / количество ордеров в айсберге</param>
        /// <param name="signalType">open position signal name / название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
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
        /// enter position Long at price intersection / 
        /// купить по пересечению цены
        /// </summary>
        /// <param name="volume">volume / объём</param>
        /// <param name="priceLimit">order price / цена ордера</param>
        /// <param name="priceRedLine">line price / цена линии, после достижения которой будет выставлен ордер на покупку</param>
        /// <param name="activateType">activation type / тип активации ордера</param>
        /// /// <param name="expiresBars">life time in candels count / время жизни ордера в барах</param>
        public void BuyAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, int expiresBars, string signalType)
        {
            try
            {
                PositionOpenerToStop positionOpener = 
                    new PositionOpenerToStop(CandlesFinishedOnly.Count, expiresBars,TimeServerCurrent);
                positionOpener.Volume = volume;
                positionOpener.PriceOrder = priceLimit;
                positionOpener.PriceRedLine = priceRedLine;
                positionOpener.ActivateType = activateType;
                positionOpener.Side = Side.Buy;
                positionOpener.SignalType = signalType;

                _stopsOpener.Add(positionOpener);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
        /// enter position Long at price intersection / 
        /// купить по пересечению цены
        /// </summary>
        /// <param name="volume">volume / объём</param>
        /// <param name="priceLimit">order price / цена ордера</param>
        /// <param name="priceRedLine">line price / цена линии, после достижения которой будет выставлен ордер на покупку</param>
        /// <param name="activateType">activation type / тип активации ордера</param>
        /// /// <param name="expiresBars">life time in candels count / время жизни ордера в барах</param>
        public void BuyAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, int expiresBars)
        {
            BuyAtStop(volume, priceLimit, priceRedLine, activateType, expiresBars, "");
        }

        /// <summary>
        /// enter position Long at price intersection. work one candle / 
        /// купить по пересечению цены. Действует одну свечку
        /// </summary>
        /// <param name="volume">volume / объём</param>
        /// <param name="priceLimit">order price / цена ордера</param>
        /// <param name="priceRedLine">line price / цена линии, после достижения которой будет выставлен ордер на покупку</param>
        /// <param name="activateType">activation type / тип активации ордера</param>
        public void BuyAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType)
        {
            BuyAtStop(volume, priceLimit, priceRedLine, activateType, 1, "");
        }

        /// <summary>
        /// enter position Long at price intersection. work one candle / 
        /// купить по пересечению цены. Действует одну свечку
        /// </summary>
        /// <param name="volume">volume / объём</param>
        /// <param name="priceLimit">order price / цена ордера</param>
        /// <param name="priceRedLine">line price / цена линии, после достижения которой будет выставлен ордер на покупку</param>
        /// <param name="activateType">activation type / тип активации ордера</param>
        /// <param name="signalType">the opening signal. It will be written to the position as SignalTypeOpen / тип сигнала на открытие. Будет записано в позицию как SignalTypeOpen</param>
        public void BuyAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, string signalType)
        {
            BuyAtStop(volume, priceLimit, priceRedLine, activateType, 1, signalType);
        }

        /// <summary>
        /// add new order to Long position at limit
        /// добавить в Лонг позицию новую заявку по лимиту
        /// </summary>
        /// <param name="position">position to which the order will be added / позиция к которой будет добавлена заявка</param>
        /// <param name="priceLimit">order price / цена заявки</param>
        /// <param name="volume">volume / объём</param>
        public void BuyAtLimitToPosition(Position position, decimal priceLimit, decimal volume)
        {
            try
            {
                if (position.Direction == Side.Sell)
                {
                    SetNewLogMessage(TabName + OsLocalization.Trader.Label65, LogMessageType.Error);
                    return;
                }

                LongUpdate(position, priceLimit, volume, ManualPositionSupport.SecondToOpen, false);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// add new order to Short position at market / 
        /// добавить в позицию Лонг новую заявку по маркету 
        /// </summary>
        /// <param name="position">position to which the order will be added / позиция к которой будет добавлена заявка</param>
        /// <param name="volume">volume / объём</param>
        public void BuyAtMarketToPosition(Position position, decimal volume)
        {
            try
            {
                if (position.Direction == Side.Sell)
                {
                    SetNewLogMessage(TabName + OsLocalization.Trader.Label65, LogMessageType.Error);

                    return;
                }

                decimal price = _connector.BestAsk;

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label62, LogMessageType.System);
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

                if (IsMarketOrderSupport())
                {
                    LongUpdate(position, price, volume, ManualPositionSupport.SecondToOpen, false, OrderPriceType.Market);
                }
                else
                {
                    LongUpdate(position, price, volume, ManualPositionSupport.SecondToOpen, false);
                }

            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// add new order to Long position at iceberg / 
        /// добавить в позицию Лонг новую заявку айсберг
        /// </summary>
        /// <param name="position">position to which the order will be added / позиция к которой будет добавлена заявка</param>
        /// <param name="price">order price / цена заявок</param>
        /// <param name="volume">volume / объём</param>
        /// <param name="orderCount">iceberg orders count / количество ордеров для айсберга</param>
        public void BuyAtAcebergToPosition(Position position, decimal price, decimal volume, int orderCount)
        {
            try
            {
                if (StartProgram != StartProgram.IsOsTrader || orderCount <= 1)
                {
                    if (position.Direction == Side.Sell)
                    {
                        ClosePeaceOfDeal(position, OrderPriceType.Limit, price, ManualPositionSupport.SecondToClose, volume);

                        return;
                    }

                    LongUpdate(position, price, volume, ManualPositionSupport.SecondToOpen, false);
                    return;
                }

                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63, LogMessageType.System);
                    return;
                }

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label62, LogMessageType.System);
                    return;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return;
                }

                if (Securiti != null)
                {
                    if (Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    {
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
                    {
                        int point = lastPrice.ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                    }
                }


                _acebergMaker.MakeNewAceberg(price, ManualPositionSupport.SecondToOpen, orderCount, position, AcebergType.ModificateBuy, volume, this);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// cancel all purchase requisitions at level cross / 
        /// отменить все заявки на покупку по пробитию уровня
        /// </summary>
        public void BuyAtStopCancel()
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
                        _stopsOpener.RemoveAt(i);
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
        /// enter the short position at any price / 
        /// войти в позицию Шорт по любой цене
        /// </summary>
        /// <param name="volume">volume / объём позиции</param>
        public Position SellAtMarket(decimal volume)
        {
            try
            {
                decimal price = _connector.BestBid;

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label62, LogMessageType.System);
                    return null;
                }

                price = price - Securiti.PriceStep * 20;

                OrderPriceType type = OrderPriceType.Market;

                TimeSpan timeLife = ManualPositionSupport.SecondToOpen;

                if (IsMarketOrderSupport())
                {
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
        /// enter the short position at any price / 
        /// войти в позицию Шорт по любой цене
        /// </summary>
        /// <param name="volume">volume / объём позиции</param>
        /// <param name="signalType">open position signal name / название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
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
        /// enter the short position at limit price / 
        /// войти в позицию Шорт по определённой цене
        /// </summary>
        /// <param name="volume">volume / объём позиции</param>
        /// <param name="priceLimit">order price / цена заявки</param>
        public Position SellAtLimit(decimal volume, decimal priceLimit)
        {
            try
            {
                return ShortCreate(priceLimit, volume, OrderPriceType.Limit, ManualPositionSupport.SecondToOpen, false);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// enter the short position at limit price / 
        /// войти в позицию Шорт по определённой цене
        /// </summary>
        /// <param name="volume">volume / объём позиции</param>
        /// <param name="priceLimit">order price / цена заявки</param>
        /// <param name="signalType">open position signal name / название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
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
        /// enter the short position at iceberg / 
        /// войти в позицию Шорт айсбергом
        /// </summary>
        /// <param name="volume">volume / объём позиции</param>
        /// <param name="price">price / цена</param>
        /// <param name="orderCount">iceberg orders count / количество ордеров в айсберге</param>
        public Position SellAtAceberg(decimal volume, decimal price, int orderCount)
        {
            try
            {
                if (StartProgram != StartProgram.IsOsTrader || orderCount <= 1)
                {
                    return SellAtLimit(volume, price);
                }

                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63, LogMessageType.System);
                    return null;
                }

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label62, LogMessageType.System);
                    return null;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                if (Securiti != null)
                {
                    if (Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    {
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
                    {
                        int point = lastPrice.ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                    }
                }

                Position newDeal = new Position();
                newDeal.Number = NumberGen.GetNumberDeal(StartProgram);
                newDeal.Direction = Side.Sell;
                newDeal.State = PositionStateType.Opening;

                newDeal.NameBot = TabName;
                newDeal.Lots = Securiti.Lot;
                newDeal.PriceStepCost = Securiti.PriceStepCost;
                newDeal.PriceStep = Securiti.PriceStep;
                newDeal.PortfolioValueOnOpenPosition = Portfolio.ValueCurrent;

                _journal.SetNewDeal(newDeal);

                _acebergMaker.MakeNewAceberg(price, ManualPositionSupport.SecondToOpen, orderCount, newDeal, AcebergType.Open, volume, this);

                return newDeal;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// enter the short position at iceberg / 
        /// войти в позицию Шорт айсбергом
        /// </summary>
        /// <param name="volume">volume / объём позиции</param>
        /// <param name="price">price / цена</param>
        /// <param name="orderCount">orders count / количество ордеров в айсберге</param>
        /// <param name="signalType">open position signal name / название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public Position SellAtAceberg(decimal volume, decimal price, int orderCount, string signalType)
        {
            Position position = SellAtAceberg(volume, price, orderCount);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// enter position Short at price intersection / 
        /// продать по пересечению цены
        /// </summary>
        /// <param name="volume">volume / объём</param>
        /// <param name="priceLimit">order price / цена ордера</param>
        /// <param name="priceRedLine">line price / цена линии, после достижения которой будет выставлен ордер на продажу</param>
        /// <param name="activateType">activation type /тип активации ордера</param>
        /// <param name="expiresBars">life time in candels count / через сколько свечей заявка будет снята</param>
        /// <param name="signalType">the opening signal. It will be written to the position as SignalTypeOpen / тип сигнала на открытие. Будет записано в позицию как SignalTypeOpen</param>
        public void SellAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, int expiresBars, string signalType)
        {
            try
            {
                PositionOpenerToStop positionOpener = 
                    new PositionOpenerToStop(CandlesFinishedOnly.Count, expiresBars, TimeServerCurrent);

                positionOpener.Volume = volume;
                positionOpener.PriceOrder = priceLimit;
                positionOpener.PriceRedLine = priceRedLine;
                positionOpener.ActivateType = activateType;
                positionOpener.Side = Side.Sell;
                positionOpener.SignalType = signalType;

                _stopsOpener.Add(positionOpener);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// enter position Short at price intersection / 
        /// продать по пересечению цены
        /// </summary>
        /// <param name="volume">volume / объём</param>
        /// <param name="priceLimit">order price / цена ордера</param>
        /// <param name="priceRedLine">line price / цена линии, после достижения которой будет выставлен ордер на продажу</param>
        /// <param name="activateType">activation type /тип активации ордера</param>
        /// <param name="expiresBars">life time in candels count / через сколько свечей заявка будет снята</param>
        public void SellAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, int expiresBars)
        {
            SellAtStop(volume, priceLimit, priceRedLine, activateType, expiresBars, "");
        }

        /// <summary>
        /// enter position Short at price intersection. Work one candle / 
        /// продать по пересечению цены. Работает одну свечу
        /// </summary>
        /// <param name="volume">volume / объём</param>
        /// <param name="priceLimit">order price / цена ордера</param>
        /// <param name="priceRedLine">line price / цена линии, после достижения которой будет выставлен ордер на продажу</param>
        /// <param name="activateType">activation type /тип активации ордера</param>
        public void SellAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType)
        {
            SellAtStop(volume, priceLimit, priceRedLine, activateType, 1, "");
        }

        /// <summary>
        /// enter position Short at price intersection. Work one candle / 
        /// продать по пересечению цены. Работает одну свечу
        /// </summary>
        /// <param name="volume">volume / объём</param>
        /// <param name="priceLimit">order price / цена ордера</param>
        /// <param name="priceRedLine">line price / цена линии, после достижения которой будет выставлен ордер на продажу</param>
        /// <param name="activateType">activation type /тип активации ордера</param>
        /// <param name="signalType">the opening signal. It will be written to the position as SignalTypeOpen / тип сигнала на открытие. Будет записано в позицию как SignalTypeOpen</param>
        public void SellAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, string signalType)
        {
            SellAtStop(volume, priceLimit, priceRedLine, activateType, 1, signalType);
        }

        /// <summary>
        /// add new order to Short position at limit
        /// добавить в позицию Шорт новую заявку по лимиту
        /// </summary>
        /// <param name="position">position to which the order will be added / позиция к которой будет добавлена заявка</param>
        /// <param name="priceLimit">order price / цена заявки</param>
        /// <param name="volume">volume / объём</param>
        public void SellAtLimitToPosition(Position position, decimal priceLimit, decimal volume)
        {
            try
            {
                if (position.Direction == Side.Buy)
                {
                    SetNewLogMessage(TabName + OsLocalization.Trader.Label66, LogMessageType.Error);

                    return;
                }

                ShortUpdate(position, priceLimit, volume, ManualPositionSupport.SecondToOpen, false);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// add new order to Short position at market / 
        /// добавить в позицию Short новую заявку по маркету 
        /// </summary>
        /// <param name="position">position to which the order will be added / позиция к которой будет добавлена заявка</param>
        /// <param name="volume">volume / объём</param>
        public void SellAtMarketToPosition(Position position, decimal volume)
        {
            try
            {
                if (position.Direction == Side.Buy)
                {
                    SetNewLogMessage(TabName + OsLocalization.Trader.Label66, LogMessageType.Error);

                    return;
                }

                decimal price = _connector.BestBid;

                if (price == 0)
                {
                    SetNewLogMessage(TabName + OsLocalization.Trader.Label66, LogMessageType.Error);
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

                if (IsMarketOrderSupport())
                {
                    ShortUpdate(position, price, volume, ManualPositionSupport.SecondToOpen, false, OrderPriceType.Market);
                }
                else
                {
                    ShortUpdate(position, price, volume, ManualPositionSupport.SecondToOpen, false);
                }

            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// add new order to Short position at iceberg / 
        /// добавить в позицию Short новую заявку айсберг
        /// </summary>
        /// <param name="position">position to which the order will be added / позиция к которой будет добавлена заявка</param>
        /// <param name="price">order price / цена заявок</param>
        /// <param name="volume">volume / объём</param>
        /// <param name="orderCount">iceberg orders count / количество ордеров для айсберга</param>
        public void SellAtAcebergToPosition(Position position, decimal price, decimal volume, int orderCount)
        {
            try
            {
                if (StartProgram != StartProgram.IsOsTrader || orderCount <= 1)
                {
                    if (position.Direction == Side.Buy)
                    {
                        ClosePeaceOfDeal(position, OrderPriceType.Limit, price, ManualPositionSupport.SecondToClose, volume);
                        return;
                    }

                    ShortUpdate(position, price, volume, ManualPositionSupport.SecondToOpen, false);
                    return;
                }

                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63, LogMessageType.System);
                    return;
                }

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label62, LogMessageType.System);
                    return;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
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


                _acebergMaker.MakeNewAceberg(price, ManualPositionSupport.SecondToOpen, orderCount, position, AcebergType.ModificateSell, volume, this);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// cancel all purchase requisitions at level cross / 
        /// отменить все заявки на продажу по пробитию уровня
        /// </summary>
        public void SellAtStopCancel()
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
                        _stopsOpener.RemoveAt(i);// будет работать
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
        /// close all positions on the market / 
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
        /// close a position at any price / 
        /// закрыть позицию по любой цене
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
        /// <param name="volume">volume / объём нужный к закрытию</param>
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
                    SetNewLogMessage(OsLocalization.Trader.Label62, LogMessageType.System);
                    return;
                }

                if (IsMarketOrderSupport())
                {
                    if (position.OpenVolume <= volume)
                    {
                        CloseDeal(position, OrderPriceType.Market, price, ManualPositionSupport.SecondToClose, false);
                    }
                    else if (position.OpenVolume > volume)
                    {
                        ClosePeaceOfDeal(position, OrderPriceType.Market, price, ManualPositionSupport.SecondToClose, volume);
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
        /// close a position at any price / 
        /// закрыть позицию по любой цене
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
        /// <param name="volume">volume / объём нужный к закрытию</param>
        /// <param name="signalType">close position signal name / название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public void CloseAtMarket(Position position, decimal volume, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtMarket(position, volume);
        }

        /// <summary>
        /// close a position at a limit price / 
        /// закрыть позицию по определённой цене
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
        /// <param name="priceLimit">order price / цена ордера</param>
        /// <param name="volume">volume required to close / объём нужный к закрытию</param>
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
                    CloseDeal(position, OrderPriceType.Limit, priceLimit, ManualPositionSupport.SecondToClose, false);
                }
                else if (position.OpenVolume > volume)
                {
                    ClosePeaceOfDeal(position, OrderPriceType.Limit, priceLimit, ManualPositionSupport.SecondToClose, volume);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// close a position at a limit price / 
        /// закрыть позицию по определённой цене
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
        /// <param name="priceLimit">order price / цена ордера</param>
        /// <param name="volume">volume required to close / объём нужный к закрытию</param>
        /// <param name="signalType">close position signal name / название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public void CloseAtLimit(Position position, decimal priceLimit, decimal volume, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtLimit(position, priceLimit, volume);
        }

        /// <summary>
        /// close position at iceberg / 
        /// закрыть позицию по айсбергу определённой цене
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
        /// <param name="priceLimit">order price / цена ордера</param>
        /// <param name="volume">volume required to close / объём нужный к закрытию</param>
        /// <param name="orderCount">iceberg orders count / количество ордеров для айсберга</param>
        public void CloseAtAceberg(Position position, decimal priceLimit, decimal volume, int orderCount)
        {
            try
            {
                if (volume <= 0 || position.OpenVolume <= 0)
                {
                    return;
                }
                if (StartProgram != StartProgram.IsOsTrader || orderCount <= 1)
                {
                    if (position.OpenVolume <= volume)
                    {
                        CloseDeal(position, OrderPriceType.Limit, priceLimit, ManualPositionSupport.SecondToClose, false);
                    }
                    else if (position.OpenVolume > volume)
                    {
                        ClosePeaceOfDeal(position, OrderPriceType.Limit, priceLimit, ManualPositionSupport.SecondToClose, volume);
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
        /// close position at iceberg / 
        /// закрыть позицию по айсбергу определённой цене
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
        /// <param name="priceLimit">order price / цена ордера</param>
        /// <param name="volume">volume required to close / объём нужный к закрытию</param>
        /// <param name="orderCount">iceberg orders count / количество ордеров для айсберга</param>
        /// <param name="signalType">close position signal name / название сигнала для входа. Будет записано в свойство позиции: SignalTypeOpen</param>
        public void CloseAtAceberg(Position position, decimal priceLimit, decimal volume, int orderCount, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtAceberg(position, priceLimit, volume, orderCount);
        }

        /// <summary>
        /// place a stop order for a position / 
        /// выставить стоп-ордер для позиции
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
        /// <param name="priceActivation">price activation / цена стоп приказа, после достижения которой выставиться ордер</param>
        /// <param name="priceOrder">order price / цена ордера</param>
        public void CloseAtStop(Position position, decimal priceActivation, decimal priceOrder)
        {
            TryReloadStop(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// place a stop order for a position / 
        /// выставить стоп-ордер для позиции
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
        /// <param name="priceActivation">price activation / цена стоп приказа, после достижения которой выставиться ордер</param>
        /// <param name="priceOrder">order price / цена ордера</param>
        /// <param name="signalType">close position signal name / название сигнала для выхода. Будет записано в свойство позиции: SignalTypeClose</param>
        public void CloseAtStop(Position position, decimal priceActivation, decimal priceOrder, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtStop(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// place a trailing stop order for a position / 
        /// выставить трейлинг стоп-ордер для позиции 
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
        /// <param name="priceActivation">price activation / цена стоп приказа, после достижения которой выставиться ордер</param>
        /// <param name="priceOrder">order price / цена ордера</param>
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
        /// place a trailing stop order for a position / 
        /// выставить трейлинг стоп-ордер для позиции 
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
        /// <param name="priceActivation">price activation / цена стоп приказа, после достижения которой выставиться ордер</param>
        /// <param name="priceOrder">order price / цена ордера</param>
        /// <param name="signalType">close position signal name / название сигнала для выхода. Будет записано в свойство позиции: SignalTypeClose</param>
        public void CloseAtTrailingStop(Position position, decimal priceActivation, decimal priceOrder, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtTrailingStop(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// place profit order for a position / 
        /// выставить профит ордер для позиции 
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
        /// <param name="priceActivation">price activation / цена стоп приказа, после достижения которой выставиться ордер</param>
        /// <param name="priceOrder">order price / цена ордера</param>
        public void CloseAtProfit(Position position, decimal priceActivation, decimal priceOrder)
        {
            TryReloadProfit(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// place profit order for a position / 
        /// выставить профит ордер для позиции 
        /// </summary>
        /// <param name="position">position to be closed / позиция которую будем закрывать</param>
        /// <param name="priceActivation">price activation / цена стоп приказа, после достижения которой выставиться ордер</param>
        /// <param name="priceOrder">order price / цена ордера</param>
        /// <param name="signalType">close position signal name / название сигнала для выхода. Будет записано в свойство позиции: SignalTypeClose</param>
        public void CloseAtProfit(Position position, decimal priceActivation, decimal priceOrder, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtProfit(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// withdraw all robot open orders from the system / 
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
        /// withdraw all orders from the system associated with this transaction / 
        /// отозвать все ордера из системы, связанные с этой сделкой
        /// </summary>
        public void CloseAllOrderToPosition(Position position)
        {
            try
            {
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
        /// withdraw order / 
        /// отозвать ордер
        /// </summary>
        public void CloseOrder(Order order)
        {
            _connector.OrderCancel(order);
        }

        // внутренние функции управления позицией
        // internal position management functions

        /// <summary>
        /// Create short position
        /// Создать позицию шорт
        /// </summary>
        /// <param name="price">price order / цена заявки</param>
        /// <param name="volume">volume / объём</param>
        /// <param name="priceType">price type / тип цены</param>
        /// <param name="timeLife">life time / время жизни</param>
        /// <param name="isStopOrProfit">whether the order is a result of a stop or a profit / является ли ордер следствием срабатывания стопа или профита</param>
        private Position ShortCreate(decimal price, decimal volume, OrderPriceType priceType, TimeSpan timeLife,
            bool isStopOrProfit)
        {
            try
            {
                Side direction = Side.Sell;

                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63,
                        LogMessageType.System);
                    return null;
                }

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label62, LogMessageType.System);
                    return null;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                price = RoundPrice(price, Securiti, Side.Sell);

                Position newDeal = _dealCreator.CreatePosition(TabName, direction, price, volume, priceType,
                    timeLife, Securiti, Portfolio, StartProgram);
                newDeal.OpenOrders[0].IsStopOrProfit = isStopOrProfit;
                _journal.SetNewDeal(newDeal);

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
        /// modify position by short order / 
        /// модифицировать позицию ордером шорт
        /// </summary>
        /// <param name="position">position / позиция</param>
        /// <param name="price">order price / цена заявки</param>
        /// <param name="volume">volume / объём</param>
        /// <param name="timeLife">life time / время жизни</param>
        /// <param name="isStopOrProfit">whether the order is a result of a stop or a profit / является ли ордер следствием срабатывания стопа или профита</param>
        private void ShortUpdate(Position position, decimal price, decimal volume, TimeSpan timeLife,
            bool isStopOrProfit, OrderPriceType OrderType = OrderPriceType.Limit)
        {
            try
            {
                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63, LogMessageType.System);
                    return;
                }

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label62, LogMessageType.System);
                    return;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return;
                }

                price = RoundPrice(price, Securiti, Side.Sell);

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


                Order newOrder = _dealCreator.CreateOrder(Side.Sell, price, volume, OrderType,
                    ManualPositionSupport.SecondToOpen, StartProgram, OrderPositionConditionType.Open);
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
        /// Create long position
        /// Создать позицию long
        /// </summary>
        /// <param name="price">price order / цена заявки</param>
        /// <param name="volume">volume / объём</param>
        /// <param name="priceType">price type / тип цены</param>
        /// <param name="timeLife">life time / время жизни</param>
        /// <param name="isStopOrProfit">whether the order is a result of a stop or a profit / является ли ордер следствием срабатывания стопа или профита</param>
        private Position LongCreate(decimal price, decimal volume, OrderPriceType priceType, TimeSpan timeLife,
            bool isStopOrProfit) // купить
        {
            try
            {
                //SetNewLogMessage(DateTime.Now.Millisecond.ToString(), LogMessageType.System);
                Side direction = Side.Buy;

                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63, LogMessageType.System);
                    return null;
                }

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label62, LogMessageType.System);
                    return null;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                //SetNewLogMessage(DateTime.Now.Millisecond.ToString(), LogMessageType.System);

                price = RoundPrice(price, Securiti, Side.Buy);


                Position newDeal = _dealCreator.CreatePosition(TabName, direction, price, volume, priceType,
                    timeLife, Securiti, Portfolio, StartProgram);
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
        /// modify position by long order / 
        /// модифицировать позицию ордером лонг
        /// </summary>
        /// <param name="position">position / позиция</param>
        /// <param name="price">order price / цена заявки</param>
        /// <param name="volume">volume / объём</param>
        /// <param name="timeLife">life time / время жизни</param>
        /// <param name="isStopOrProfit">whether the order is a result of a stop or a profit / является ли ордер следствием срабатывания стопа или профита</param>
        private void LongUpdate(Position position, decimal price, decimal volume, TimeSpan timeLife,
            bool isStopOrProfit, OrderPriceType OrderType = OrderPriceType.Limit)
        {
            try
            {
                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63, LogMessageType.System);
                    return;
                }

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label62, LogMessageType.System);
                    return;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return;
                }

                price = RoundPrice(price, Securiti, Side.Buy);

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

                Order newOrder = _dealCreator.CreateOrder(Side.Buy, price, volume, OrderType,
                    ManualPositionSupport.SecondToOpen, StartProgram, OrderPositionConditionType.Open);
                newOrder.IsStopOrProfit = isStopOrProfit;
                newOrder.LifeTime = timeLife;
                position.AddNewOpenOrder(newOrder);

                _connector.OrderExecute(newOrder);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// close position / 
        /// закрыть позицию
        /// </summary>
        /// <param name="position">position / позиция</param>
        /// <param name="priceType">price type / тип цены</param>
        /// <param name="price">price / цена</param>
        /// <param name="lifeTime">life time order / время жизни позиции</param>
        /// <param name="isStopOrProfit">whether the order is a result of a stop or a profit / является ли закрытие следствием срабатывания стопа или профита</param>
        private void CloseDeal(Position position, OrderPriceType priceType, decimal price, TimeSpan lifeTime,
            bool isStopOrProfit)
        {
            try
            {
                if (position == null)
                {
                    return;
                }

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

                position.State = PositionStateType.Closing;

                Order closeOrder = _dealCreator.CreateCloseOrderForDeal(position, price, priceType, lifeTime, StartProgram);

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
        /// partially close a position / 
        /// закрыть позицию частично
        /// </summary>
        /// <param name="position">position / позиция</param>
        /// <param name="priceType">price type / тип цены</param>
        /// <param name="price">price / цена</param>
        /// <param name="lifeTime">life time / время жизни позиции</param>
        /// <param name="volume">volume / объём на который следует закрыть позицию</param>
        private void ClosePeaceOfDeal(Position position, OrderPriceType priceType, decimal price, TimeSpan lifeTime,
            decimal volume)
        {
            try
            {
                if (position == null)
                {
                    return;
                }

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

                Order closeOrder = _dealCreator.CreateCloseOrderForDeal(position, price, priceType, lifeTime, StartProgram);


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
        /// restart stop / 
        /// перезагрузить стоп
        /// </summary>
        /// <param name="position">positin / позиция</param>
        /// <param name="priceActivate">price activation / цена после которой ордер будет выставлен</param>
        /// <param name="priceOrder">order price / цена ордера для стопа</param>
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
                {
                    return;
                }

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
        /// restart profit / 
        /// перезагрузить профит
        /// </summary>
        /// <param name="position">position / позиция</param>
        /// <param name="priceActivate">activation price / цена после которой ордер будет выставлен</param>
        /// <param name="priceOrder">order price / цена ордера для профита</param>
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
                {
                    return;
                }

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
        /// adjust order price to the needs of the exchange / 
        /// подогнать цену контракта под нужды биржи
        /// </summary>
        /// <param name="price">current price / текущая цена по которой интерфейс высокого уровня захотел закрыть позицию</param>
        /// <param name="security">security / бумага</param>
        /// <param name="side">side / сторона входа</param>
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

        // handling alerts and stop maintenance
        // обработка алертов и сопровождения стопов

        private object _lockerManualReload = new object();

        /// <summary>
        /// check the manual support of the stop and profit / 
        /// проверить ручное сопровождение стопа и профита
        /// </summary>
        /// <param name="position">position / позиция</param>
        private void ManualReloadStopsAndProfitToPosition(Position position)
        {
            try
            {
                lock (_lockerManualReload)
                {
                    if (position.CloseOrders != null &&
                        position.CloseOrders[position.CloseOrders.Count - 1].State == OrderStateType.Activ)
                    {
                        return;
                    }

                    Order openOrder = position.OpenOrders[position.OpenOrders.Count - 1];

                    if (openOrder.TradesIsComing == false)
                    {
                        PositionToSecondLoopSender sender = new PositionToSecondLoopSender() { Position = position };
                        sender.PositionNeadToStopSend += ManualReloadStopsAndProfitToPosition;

                        Task task = new Task(sender.Start);
                        task.Start();
                        return;
                    }

                    ManualPositionSupport.TryReloadStopAndProfit(this, position);
                }
            }
            catch (Exception e)
            {
                SetNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// check if the trade has a stop or profit / 
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
                    {
                        position.ProfitOrderIsActiv = false;
                        position.StopOrderIsActiv = false;

                        SetNewLogMessage(
                            "Close Position at Stop. StopPrice: " +
                            position.StopOrderRedLine
                            + " LastMarketPrice: " + lastTrade,
                            LogMessageType.System);
                        if(IsMarketStopOrderSupport())
                            CloseDeal(position, OrderPriceType.Market, position.StopOrderPrice, ManualPositionSupport.SecondToClose, true);
                        else
                            CloseDeal(position, OrderPriceType.Limit, position.StopOrderPrice, ManualPositionSupport.SecondToClose, true);
                        PositionStopActivateEvent?.Invoke(position);
                        return true;
                    }

                    if (position.Direction == Side.Sell &&
                        position.StopOrderRedLine <= lastTrade)
                    {
                        position.StopOrderIsActiv = false;
                        position.ProfitOrderIsActiv = false;

                        SetNewLogMessage(
                            "Close Position at Stop. StopPrice: " +
                            position.StopOrderRedLine
                            + " LastMarketPrice: " + lastTrade,
                            LogMessageType.System);

                        if (IsMarketStopOrderSupport())
                            CloseDeal(position, OrderPriceType.Market, position.StopOrderPrice, ManualPositionSupport.SecondToClose, true);
                        else
                            CloseDeal(position, OrderPriceType.Limit, position.StopOrderPrice, ManualPositionSupport.SecondToClose, true);
                        PositionStopActivateEvent?.Invoke(position);
                        return true;
                    }
                }

                if (position.ProfitOrderIsActiv)
                {
                    if (position.Direction == Side.Buy &&
                        position.ProfitOrderRedLine <= lastTrade)
                    {
                        position.StopOrderIsActiv = false;
                        position.ProfitOrderIsActiv = false;

                        SetNewLogMessage(
                            "Close Position at Profit. ProfitPrice: " +
                            position.ProfitOrderRedLine
                            + " LastMarketPrice: " + lastTrade,
                            LogMessageType.System);

                        CloseDeal(position, OrderPriceType.Limit, position.ProfitOrderPrice, ManualPositionSupport.SecondToClose, true);
                        PositionProfitActivateEvent?.Invoke(position);
                        return true;
                    }

                    if (position.Direction == Side.Sell &&
                        position.ProfitOrderRedLine >= lastTrade)
                    {
                        position.StopOrderIsActiv = false;
                        position.ProfitOrderIsActiv = false;

                        SetNewLogMessage(
                            "Close Position at Profit. ProfitPrice: " +
                            position.ProfitOrderRedLine
                            + " LastMarketPrice: " + lastTrade,
                            LogMessageType.System);

                        CloseDeal(position, OrderPriceType.Limit, position.ProfitOrderPrice, ManualPositionSupport.SecondToClose, true);
                        PositionProfitActivateEvent?.Invoke(position);
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
        /// alert check / 
        /// проверка алертов
        /// </summary>
        private void AlertControlPosition()
        {
            try
            {
                if (_alerts == null)
                {
                    return;
                }

                AlertSignal signal = _alerts.CheckAlerts();

                if (signal == null)
                {
                    return;
                }

                if (signal.SignalType == SignalType.CloseAll)
                {
                    SetNewLogMessage(Securiti.Name + OsLocalization.Trader.Label67 +
                                        _connector.BestBid, LogMessageType.Signal);

                    CloseAllAtMarket();
                }

                if (signal.SignalType == SignalType.CloseOne)
                {
                    Position position = _journal.GetPositionForNumber(signal.NumberClosingPosition);

                    if (position == null)
                    {
                        return;
                    }

                    SetNewLogMessage(Securiti.Name + OsLocalization.Trader.Label67 +
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

                        CloseDeal(position, OrderPriceType.Limit, price, ManualPositionSupport.SecondToClose, true);
                    }
                }

                if (signal.SignalType == SignalType.Buy)
                {
                    SetNewLogMessage(Securiti.Name + OsLocalization.Trader.Label69 + _connector.BestBid, LogMessageType.Signal);

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
                    SetNewLogMessage(Securiti.Name + OsLocalization.Trader.Label68 + _connector.BestBid, LogMessageType.Signal);

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
        /// get journal / 
        /// взять журнал
        /// </summary>
        public Journal.Journal GetJournal()
        {
            return _journal;
        }

        /// <summary>
        /// add a new alert to the system / 
        /// добавить новый алерт в систему
        /// </summary>
        public void SetNewAlert(IIAlert alert)
        {
            _alerts.SetNewAlert(alert);
        }

        /// <summary>
        /// remove alert from system / 
        /// удалить алерт из системы
        /// </summary>
        public void DeleteAlert(IIAlert alert)
        {
            _alerts.Delete(alert);
        }

        /// <summary>
        /// remove all alerts from the system / 
        /// удалить все алерты из системы
        /// </summary>
        public void DeleteAllAlerts()
        {
            _alerts.DeleteAll();
        }

        // дозакрытие сделки если на закрытии мы взяли больший объём чем нужно
        // closing a deal if at closing we took more volume than necessary

        /// <summary>
        /// time to close the deal / 
        /// время когда надо совершить дозакрытие сделки
        /// </summary>
        private DateTime _lastClosingSurplusTime;

        /// <summary>
        /// check whether it is not necessary to close the transactions for which the search was at the close / 
        /// проверить, не надо ли закрыть сделки по которым был перебор на закрытии
        /// </summary>
        private void CheckSurplusPositions()
        {
            if (StartProgram == StartProgram.IsOsTrader && _lastClosingSurplusTime != DateTime.MinValue &&
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

            if (positions.Count == 0)
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
                {
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

        // opening deals by the deferred method
        // открытие сделок отложенным методом

        /// <summary>
        /// stop opening waiting for its price / 
        /// стоп - открытия ожидающие своей цены
        /// </summary>
        private List<PositionOpenerToStop> _stopsOpener;

        private void CancelStopOpenerByNewCandle(List<Candle> candles)
        {
            for (int i = 0; _stopsOpener != null && i < _stopsOpener.Count; i++)
            {
                if (_stopsOpener[i].ExpiresBars <= 1)
                {
                    _stopsOpener.RemoveAt(i);
                    i--;
                    continue;
                }

                if (candles[candles.Count - 1].TimeStart > _stopsOpener[i].LastCandleTime)
                {
                    _stopsOpener[i].LastCandleTime = candles[candles.Count - 1].TimeStart;
                    _stopsOpener[i].ExpiresBars = _stopsOpener[i].ExpiresBars - 1;
                }
            }
        }

        /// <summary>
        /// check whether it is time to open positions on stop openings / 
        /// проверить, не пора ли открывать позиции по стопОткрытиям
        /// </summary>
        private void CheckStopOpener(decimal price)
        {
            if (ServerStatus != ServerConnectStatus.Connect ||
                Securiti == null || Portfolio == null)
            {
                return;
            }

            try
            {
                for (int i = 0;
                    i > -1 && _stopsOpener != null && _stopsOpener.Count != 0 && i < _stopsOpener.Count;
                    i++)
                {
                    if ((_stopsOpener[i].ActivateType == StopActivateType.HigherOrEqual &&
                         price >= _stopsOpener[i].PriceRedLine)
                        ||
                        (_stopsOpener[i].ActivateType == StopActivateType.LowerOrEqyal &&
                         price <= _stopsOpener[i].PriceRedLine))
                    {
                        if (_stopsOpener[i].Side == Side.Buy)
                        {
                            PositionOpenerToStop opener = _stopsOpener[i];
                            Position pos = LongCreate(_stopsOpener[i].PriceOrder, _stopsOpener[i].Volume, OrderPriceType.Limit,
                                ManualPositionSupport.SecondToOpen, true);

                            if (pos != null)
                            {
                                pos.SignalTypeOpen = opener.SignalType;
                            }

                            if (_stopsOpener.Count == 0)
                            { // пользователь может удалить сам из слоя увидив что сделка открыается
                                return;
                            }

                            _stopsOpener.RemoveAt(i);
                            i--;
                            if (PositionBuyAtStopActivateEvent != null && pos != null)
                            { PositionBuyAtStopActivateEvent(pos); }
                            continue;
                        }
                        else if (_stopsOpener[i].Side == Side.Sell)
                        {
                            PositionOpenerToStop opener = _stopsOpener[i];
                            Position pos = ShortCreate(_stopsOpener[i].PriceOrder, _stopsOpener[i].Volume, OrderPriceType.Limit,
                                ManualPositionSupport.SecondToOpen, true);

                            if (pos != null)
                            {
                                pos.SignalTypeOpen = opener.SignalType;
                            }

                            if (_stopsOpener.Count == 0)
                            { // пользователь может удалить сам из слоя увидив что сделка открыается
                                return;
                            }

                            _stopsOpener.RemoveAt(i);
                            i--;

                            if (PositionSellAtStopActivateEvent != null && pos != null)
                            { PositionSellAtStopActivateEvent(pos); }
                            continue;
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

        // icebergs control
        // управления айсбергами

        /// <summary>
        /// icebergs master
        /// мастер управления айсбергами
        /// </summary>
        private AcebergMaker _acebergMaker;

        /// <summary>
        /// Iceberg Master Requests To Cancel Order / 
        /// мастер айсбергов требует отозвать ордер
        /// </summary>
        void _acebergMaker_NewOrderNeadToCansel(Order order)
        {
            _connector.OrderCancel(order);
        }

        /// <summary>
        /// icebergs master requires you to place an order / 
        /// мастер айсбергов требует выставить ордер
        /// </summary>
        void _acebergMaker_NewOrderNeadToExecute(Order order)
        {
            _connector.OrderExecute(order);
        }

        /// <summary>
        /// clear all icebergs from the system / 
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

        // incoming data processing
        // обработка входящих данных

        /// <summary>
        /// new MarketDepth / 
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

            if (StartProgram != StartProgram.IsOsTrader)
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
                        // CheckStop(openPositions[i], marketDepth.Asks[0].Price);

                        if (openPositions.Count <= i)
                        {
                            continue;
                        }
                        //CheckStop(openPositions[i], marketDepth.Bids[0].Price);
                    }
                }
            }
        }

        /// <summary>
        /// it's time to close the order for this deal / 
        /// пора закрывать ордер у этой сделки
        /// </summary>
        private void _dealOpeningWatcher_DontOpenOrderDetectedEvent(Order order, Position deal)
        {
            try
            {
                _connector.OrderCancel(order);

                for (int i = 0; deal.CloseOrders != null && i < deal.CloseOrders.Count; i++)
                {
                    if (order.NumberUser == deal.CloseOrders[i].NumberUser && ManualPositionSupport.DoubleExitIsOn)
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
        /// position status has changed
        /// изменился статус сделки
        /// </summary>
        private void _journal_PositionStateChangeEvent(Position position)
        {
            try
            {
                if (position.State == PositionStateType.Done)
                {
                    CloseAllOrderToPosition(position);

                    if (PositionClosingSuccesEvent != null)
                    {
                        PositionClosingSuccesEvent(position);
                    }

                    if (StartProgram == StartProgram.IsOsTrader)
                    {
                        SetNewLogMessage(TabName + OsLocalization.Trader.Label71 + position.Number, LogMessageType.Trade);
                    }
                    else
                    {
                        decimal profit = position.ProfitPortfolioPunkt;

                        if (_connector.ServerType == ServerType.Tester)
                        {
                            ((TesterServer)_connector.MyServer).AddProfit(profit);
                        }
                        else if (_connector.ServerType == ServerType.Optimizer)
                        {
                            ((OptimizerServer)_connector.MyServer).AddProfit(profit);
                        }
                    }
                }
                else if (position.State == PositionStateType.OpeningFail)
                {
                    SetNewLogMessage(TabName + OsLocalization.Trader.Label72 + position.Number, LogMessageType.System);

                    if (PositionOpeningFailEvent != null)
                    {
                        PositionOpeningFailEvent(position);
                    }
                }
                else if (position.State == PositionStateType.Open)
                {
                    SetNewLogMessage(TabName + OsLocalization.Trader.Label73 + position.Number, LogMessageType.Trade);
                    if (PositionOpeningSuccesEvent != null)
                    {
                        PositionOpeningSuccesEvent(position);
                    }
                    ManualReloadStopsAndProfitToPosition(position);
                }
                else if (position.State == PositionStateType.ClosingFail)
                {
                    if (StartProgram == StartProgram.IsOsTrader)
                    {
                        SetNewLogMessage(TabName + OsLocalization.Trader.Label74 + position.Number, LogMessageType.System);
                    }

                    if (ManualPositionSupport.DoubleExitIsOn &&
                        position.CloseOrders.Count < 5)
                    {
                        ManualPositionSupport.TryEmergencyClosePosition(this, position);
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
        /// open position volume changed
        /// изменился открытый объём по сделке
        /// </summary>
        void _journal_PositionNetVolumeChangeEvent(Position position)
        {
            if (PositionNetVolumeChangeEvent != null)
            {
                PositionNetVolumeChangeEvent(position);
            }
        }

        /// <summary>
        /// candle is finished / 
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
                    CancelStopOpenerByNewCandle(candles);
                }

                if (_chartMaster != null)
                {
                    _chartMaster.SetCandles(candles);
                }

                try
                {
                    CandleFinishedEvent?.Invoke(candles);
                }
                catch (Exception error)
                {
                    SetNewLogMessage(error.ToString(), LogMessageType.Error);
                }


            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public DateTime LastTimeCandleUpdate { get; set; }

        /// <summary>
        /// candle is update / 
        /// обновилась последняя свеча
        /// </summary>
        private void LogicToUpdateLastCandle(List<Candle> candles)
        {
            try
            {
                LastTimeCandleUpdate = DateTime.Now;

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
        /// user ordered a position change / 
        /// пользователь заказал изменение позиции
        /// </summary>
        private void _journal_UserSelectActionEvent(Position position, SignalType signalType)
        {
            try
            {
                if (signalType == SignalType.CloseAll)
                {
                    SetNewLogMessage(TabName + OsLocalization.Trader.Label75, LogMessageType.User);

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

                if (signalType == SignalType.FindPosition)
                {
                    if (position == null)
                    {
                        return;
                    }

                    GoChartToThisTime(position.TimeCreate);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// has the session started today? / 
        /// стартовала ли сегодня сессия
        /// </summary>
        private bool _firstTickToDaySend;

        /// <summary>
        /// last tick index / 
        /// последний индекс тика
        /// </summary>
        private int _lastTickIndex;

        /// <summary>
        /// new tiki came / 
        /// пришли новые тики
        /// </summary>
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

            if (curCount == _lastTickIndex)
            {
                return;
            }

            List<Position> openPositions = _journal.OpenPositions;

            if (openPositions != null)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    for (int i2 = _lastTickIndex; i < openPositions.Count && i2 < curCount && trades[i2] != null; i2++)
                    {
                        if (CheckStop(openPositions[i], trades[i2].Price))
                        {
                            if (StartProgram != StartProgram.IsOsTrader)
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

            _lastTickIndex = curCount;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                CheckSurplusPositions();
            }

        }

        /// <summary>
        /// incoming my deal / 
        /// входящая моя сделка
        /// </summary>
        private void _connector_MyTradeEvent(MyTrade trade)
        {
            _journal.SetNewMyTrade(trade);
        }

        /// <summary>
        /// server time has changed / 
        /// изменилось время сервера
        /// </summary>
        void StrategOneSecurity_TimeServerChangeEvent(DateTime time)
        {
            if (ManualPositionSupport != null)
            {
                ManualPositionSupport.ServerTime = time;
            }

            if (ServerTimeChangeEvent != null)
            {
                ServerTimeChangeEvent(time);
            }
        }

        /// <summary>
        /// incoming orders / 
        /// входящие ордера
        /// </summary>
        private void _connector_OrderChangeEvent(Order order)
        {
            Order orderInJournal = _journal.IsMyOrder(order);

            if (orderInJournal == null)
            {
                return;
            }
            _journal.SetNewOrder(order);
            _acebergMaker.SetNewOrder(order);

            if (OrderUpdateEvent != null)
            {
                OrderUpdateEvent(orderInJournal);
            }
        }

        /// <summary>
        /// incoming new bid with ask / 
        /// входящие новые бид с аском
        /// </summary>
        private void _connector_BestBidAskChangeEvent(decimal bestBid, decimal bestAsk)
        {
            _journal?.SetNewBidAsk(bestBid, bestAsk);
            _marketDepthPainter?.ProcessBidAsk(bestBid, bestAsk);
            BestBidAskChangeEvent?.Invoke(bestBid, bestAsk);
        }

        // исходящие события. Обработчики для стратегии
        // outgoing events. Handlers for strategy

        /// <summary>
        /// The morning session started. Send the first trades
        /// утренняя сессия стартовала. Пошли первые тики
        /// </summary>
        public event Action<Trade> FirstTickToDayEvent;

        /// <summary>
        /// new trades
        /// пришли новые тики
        /// </summary>
        public event Action<Trade> NewTickEvent;

        /// <summary>
        /// new server time
        /// изменилось время сервера
        /// </summary>
        public event Action<DateTime> ServerTimeChangeEvent;

        /// <summary>
        /// last candle finished / 
        /// завершилась новая свечка
        /// </summary>
        public event Action<List<Candle>> CandleFinishedEvent;

        /// <summary>
        /// last candle update /
        /// обновилась последняя свечка
        /// </summary>
        public event Action<List<Candle>> CandleUpdateEvent;

        /// <summary>
        /// new marketDepth
        /// пришёл новый стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthUpdateEvent;

        /// <summary>
        /// bid ask change
        /// изменился лучший бид/аск (лучшая цена покупки, лучшая цена продажи)
        /// </summary>
        public event Action<decimal, decimal> BestBidAskChangeEvent;

        /// <summary>
        /// position successfully closed / 
        /// позиция успешно закрыта
        /// </summary>
        public event Action<Position> PositionClosingSuccesEvent;

        /// <summary>
        /// position successfully opened /
        /// позиция успешно открыта
        /// </summary>
        public event Action<Position> PositionOpeningSuccesEvent;

        /// <summary>
        /// open position volume has changed / 
        /// у позиции изменился открытый объём
        /// </summary>
        public event Action<Position> PositionNetVolumeChangeEvent;

        /// <summary>
        /// opening position failed / 
        /// открытие позиции не случилось
        /// </summary>
        public event Action<Position> PositionOpeningFailEvent;

        /// <summary>
        /// position closing failed / 
        /// закрытие позиции не прошло
        /// </summary>
        public event Action<Position> PositionClosingFailEvent;

        /// <summary>
        /// a stop order is activated for the position
        /// по позиции активирован стоп-ордер
        /// </summary>
        public event Action<Position> PositionStopActivateEvent;

        /// <summary>
        /// a profit order is activated for the position
        /// по позиции активирован профит-ордер
        /// </summary>
        public event Action<Position> PositionProfitActivateEvent;

        /// <summary>
        /// stop order buy activated
        /// активирована покупка по стоп-приказу
        /// </summary>
        public event Action<Position> PositionBuyAtStopActivateEvent;

        /// <summary>
        /// stop order sell activated
        /// активирована продажа по стоп-приказу
        /// </summary>
        public event Action<Position> PositionSellAtStopActivateEvent;

        /// <summary>
        /// the robot is removed from the system / 
        /// робот удаляется из системы
        /// </summary>
        public event Action<int> DeleteBotEvent;

        /// <summary>
        /// updated order
        /// обновился ордер
        /// </summary>
        public event Action<Order> OrderUpdateEvent;

    }


    public class PositionToSecondLoopSender
    {

        public Position Position;

        public async void Start()
        {
            await Task.Delay(3000);
            if (PositionNeadToStopSend != null)
            {
                PositionNeadToStopSend(Position);
            }
        }

        public event Action<Position> PositionNeadToStopSend;

    }

    /// <summary>
    /// activation type stop order / 
    /// тип активации стоп приказа
    /// </summary>
    public enum StopActivateType
    {

        /// <summary>
        /// activate when the price is higher or equal
        /// активировать когда цена будет выше или равно
        /// </summary>
        HigherOrEqual,

        /// <summary>
        /// activate when the price is lower or equal / 
        /// активировать когда цена будет ниже или равно
        /// </summary>
        LowerOrEqyal
    }
}
