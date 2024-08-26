/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
    /// Trading tab
    /// </summary>
    public class BotTabSimple : IIBotTab
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public BotTabSimple(string name, StartProgram startProgram)
        {
            TabName = name;
            StartProgram = startProgram;

            try
            {
                _connector = new ConnectorCandles(TabName, startProgram, true);
                _connector.OrderChangeEvent += _connector_OrderChangeEvent;
                _connector.MyTradeEvent += _connector_MyTradeEvent;
                _connector.BestBidAskChangeEvent += _connector_BestBidAskChangeEvent;
                _connector.PortfolioOnExchangeChangedEvent += _connector_PortfolioOnExchangeChangedEvent;
                _connector.GlassChangeEvent += _connector_GlassChangeEvent;
                _connector.TimeChangeEvent += StrategOneSecurity_TimeServerChangeEvent;
                _connector.NewCandlesChangeEvent += LogicToEndCandle;
                _connector.LastCandlesChangeEvent += LogicToUpdateLastCandle;
                _connector.TickChangeEvent += _connector_TickChangeEvent;
                _connector.LogMessageEvent += SetNewLogMessage;
                _connector.ConnectorStartedReconnectEvent += _connector_ConnectorStartedReconnectEvent;
                _connector.SecuritySubscribeEvent += _connector_SecuritySubscribeEvent;

                if (startProgram != StartProgram.IsOsOptimizer)
                {
                    _marketDepthPainter = new MarketDepthPainter(TabName);
                    _marketDepthPainter.LogMessageEvent += SetNewLogMessage;
                }

                _journal = new Journal.Journal(TabName, startProgram);

                _journal.PositionStateChangeEvent += _journal_PositionStateChangeEvent;
                _journal.PositionNetVolumeChangeEvent += _journal_PositionNetVolumeChangeEvent;
                _journal.UserSelectActionEvent += _journal_UserSelectActionEvent;
                _journal.LogMessageEvent += SetNewLogMessage;

                _connector.ComissionType = _journal.ComissionType;
                _connector.ComissionValue = _journal.ComissionValue;

                _chartMaster = new ChartCandleMaster(TabName, StartProgram);
                _chartMaster.LogMessageEvent += SetNewLogMessage;
                _chartMaster.SetNewSecurity(_connector.SecurityName, _connector.TimeFrameBuilder, _connector.PortfolioName, _connector.ServerType);
                _chartMaster.SetPosition(_journal.AllPosition);
                _chartMaster.IndicatorUpdateEvent += _chartMaster_IndicatorUpdateEvent;

                if (StartProgram != StartProgram.IsOsOptimizer)
                {
                    _alerts = new AlertMaster(TabName, _connector, _chartMaster);
                    _alerts.LogMessageEvent += SetNewLogMessage;
                }
                _dealCreator = new PositionCreator();

                ManualPositionSupport = new BotManualControl(TabName, this, startProgram);
                ManualPositionSupport.LogMessageEvent += SetNewLogMessage;
                ManualPositionSupport.DontOpenOrderDetectedEvent += _dealOpeningWatcher_DontOpenOrderDetectedEvent;

                _acebergMaker = new AcebergMaker();
                _acebergMaker.NewOrderNeadToExecute += _acebergMaker_NewOrderNeadToExecute;
                _acebergMaker.NewOrderNeadToCansel += _acebergMaker_NewOrderNeadToCansel;

                if (startProgram == StartProgram.IsOsTrader)
                {// load the latest orders for robots to the general storage in ServerMaster

                    List<Order> oldOrders = _journal.GetLastOrdersToPositions(50);

                    for (int i = 0; i < oldOrders.Count; i++)
                    {
                        _connector.LoadOrderInOrderStorage(oldOrders[i]);
                    }
                }

                PositionOpenerToStop = new List<PositionOpenerToStopLimit>();

                if (startProgram == StartProgram.IsOsTrader)
                {
                    List<PositionOpenerToStopLimit> stopLimitsFromJournal = _journal.LoadStopLimits();

                    if (stopLimitsFromJournal != null &&
                        stopLimitsFromJournal.Count > 0)
                    {
                        PositionOpenerToStop = stopLimitsFromJournal;
                    }
                    UpdateStopLimits();

                    if(_senderThreadIsStarted == false)
                    {
                        _senderThreadIsStarted = true;

                        Thread worker = new Thread(PositionsSenderThreadArea);
                        worker.Name = "Static. BotTabSimple. PositionsSenderThreadArea";
                        worker.Start();
                    }

                    _tabsToCheckPositionEvent.Add(this);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// source type
        /// </summary>
        public BotTabType TabType 
        {
            get
            {
                return BotTabType.Simple;
            } 
        }

        /// <summary>
        /// The connector has started the reconnection procedure
        /// </summary>
        /// <param name="securityName">security name</param>
        /// <param name="timeFrame">timeframe DateTime</param>
        /// <param name="timeFrameSpan">timeframe TimeSpan</param>
        /// <param name="portfolioName">porrtfolio name</param>
        /// <param name="serverType">server type</param>
        void _connector_ConnectorStartedReconnectEvent(string securityName, TimeFrame timeFrame, TimeSpan timeFrameSpan, string portfolioName, ServerType serverType)
        {
            _lastTradeTime = DateTime.MinValue;
            _lastTradeIndex = 0;

            if (_chartMaster == null)
            {
                return;
            }
            _chartMaster.ClearTimePoints();

            if (string.IsNullOrEmpty(securityName))
            {
                return;
            }

            _chartMaster.SetNewSecurity(securityName, _connector.TimeFrameBuilder, portfolioName, serverType);
        }

        // control

        /// <summary>
        /// Start drawing this robot
        /// </summary>
        public void StartPaint(Grid gridChart, WindowsFormsHost hostChart, WindowsFormsHost hostGlass, WindowsFormsHost hostOpenDeals,
                     WindowsFormsHost hostCloseDeals, Rectangle rectangleChart, WindowsFormsHost hostAlerts, TextBox textBoxLimitPrice, Grid gridChartControlPanel, TextBox textBoxVolume)
        {
            try
            {
                if (Securiti != null
                    && Portfolio != null)
                {
                    _chartMaster?.SetNewSecurity(Securiti.Name, _connector.TimeFrameBuilder, Portfolio.Number, Connector.ServerType);
                }

                _chartMaster?.StartPaint(gridChart, hostChart, rectangleChart);
                _marketDepthPainter?.StartPaint(hostGlass, textBoxLimitPrice, textBoxVolume);
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
        /// Stop drawing this robot
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
        /// Unique robot name
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// Tab number
        /// </summary>
        public int TabNum { get; set; }
		
        /// <summary>
        /// has this BotTabScreener tab been created
        /// создана ли вкладка BotTabScreener
        /// </summary>

        public bool IsCreatedByScreener { get; set; }

        /// <summary>
        /// are events sent to the top from the tab?
        /// </summary>
        public bool EventsIsOn 
        { 
            get 
            {
                if(Connector == null)
                {
                    return false;
                }

                return Connector.EventsIsOn;
            } 
            set 
            {
                if (Connector == null)
                {
                    return;
                }

                if(Connector.EventsIsOn == value)
                {
                    return;
                }

                Connector.EventsIsOn = value;
            } 
        
        }

        /// <summary>
        /// custom name robot
        /// пользовательское имя робота
        /// </summary>
        public string NameStrategy
        {
            get
            {
                if (!TabName.Contains("tab"))
                {
                    return "";
                }
                string _nameStrategy = TabName.Remove(TabName.LastIndexOf("tab"), TabName.Length - TabName.LastIndexOf("tab"));
                if (IsCreatedByScreener == true)
                {
                    _nameStrategy = _nameStrategy.Remove(0, _nameStrategy.IndexOf(" ") + 1);
                }
                return _nameStrategy;
            }
        }		

        /// <summary>
        /// Clear
        /// </summary>
        public void Clear()
        {
            try
            {
                ClearAceberg();

                BuyAtStopCancel();

                SellAtStopCancel();

                if (_journal != null)
                {
                    _journal.Clear();
                }

                if (_alerts != null)
                {
                    _alerts.Clear();
                }

                if (_chartMaster != null)
                {
                    _chartMaster.Clear();
                }

                _lastTradeTime = DateTime.MinValue;
                _lastTradeIndex = 0;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool _isDelete;

        /// <summary>
        /// Remove tab and all child structures
        /// </summary>
        public void Delete()
        {
            try
            {
                _isDelete = true;

                if (_connector != null)
                {
                    _connector.OrderChangeEvent -= _connector_OrderChangeEvent;
                    _connector.MyTradeEvent -= _connector_MyTradeEvent;
                    _connector.BestBidAskChangeEvent -= _connector_BestBidAskChangeEvent;
                    _connector.GlassChangeEvent -= _connector_GlassChangeEvent;
                    _connector.TimeChangeEvent -= StrategOneSecurity_TimeServerChangeEvent;
                    _connector.NewCandlesChangeEvent -= LogicToEndCandle;
                    _connector.LastCandlesChangeEvent -= LogicToUpdateLastCandle;
                    _connector.TickChangeEvent -= _connector_TickChangeEvent;
                    _connector.ConnectorStartedReconnectEvent -= _connector_ConnectorStartedReconnectEvent;
                    _connector.PortfolioOnExchangeChangedEvent -= _connector_PortfolioOnExchangeChangedEvent;
                    _connector.Delete();
                    _connector.LogMessageEvent -= SetNewLogMessage;
                    _connector.SecuritySubscribeEvent -= _connector_SecuritySubscribeEvent;

                    _connector = null;
                }

                if (_journal != null)
                {
                    _journal.PositionStateChangeEvent -= _journal_PositionStateChangeEvent;
                    _journal.PositionNetVolumeChangeEvent -= _journal_PositionNetVolumeChangeEvent;
                    _journal.UserSelectActionEvent -= _journal_UserSelectActionEvent;
                    _journal.Delete();
                    _journal.LogMessageEvent -= SetNewLogMessage;
                    _journal = null;
                }

                if (_alerts != null)
                {
                    _alerts.Delete();
                    _alerts.LogMessageEvent -= SetNewLogMessage;
                    _alerts = null;
                }

                if (_acebergMaker != null)
                {
                    _acebergMaker.NewOrderNeadToExecute -= _acebergMaker_NewOrderNeadToExecute;
                    _acebergMaker.NewOrderNeadToCansel -= _acebergMaker_NewOrderNeadToCansel;
                    _acebergMaker = null;
                }

                if (ManualPositionSupport != null)
                {
                    ManualPositionSupport.DontOpenOrderDetectedEvent -= _dealOpeningWatcher_DontOpenOrderDetectedEvent;
                    ManualPositionSupport.Delete();
                    ManualPositionSupport.LogMessageEvent -= SetNewLogMessage;
                    ManualPositionSupport = null;
                }

                if (_chartMaster != null)
                {
                    _chartMaster.IndicatorUpdateEvent -= _chartMaster_IndicatorUpdateEvent;
                    _chartMaster.Delete();
                    _chartMaster.LogMessageEvent -= SetNewLogMessage;
                    _chartMaster = null;
                }

                if (_marketDepthPainter != null)
                {
                    _marketDepthPainter.Delete();
                    _marketDepthPainter.LogMessageEvent -= SetNewLogMessage;
                    _marketDepthPainter = null;
                }

                if (PositionOpenerToStop != null)
                {
                    PositionOpenerToStop.Clear();
                    PositionOpenerToStop = null;
                }

                if (_dealCreator != null)
                {
                    _dealCreator = null;
                }

                if (StartProgram != StartProgram.IsOsOptimizer)
                {
                    if (File.Exists(@"Engine\" + TabName + @"SettingsBot.txt"))
                    {
                        File.Delete(@"Engine\" + TabName + @"SettingsBot.txt");
                    }
                }

                if (DeleteBotEvent != null)
                {
                    DeleteBotEvent(TabNum);
                }

                if(TabDeletedEvent != null)
                {
                    TabDeletedEvent();
                }

                if(StartProgram == StartProgram.IsOsTrader)
                {
                    try
                    {
                        for (int i = 0; i < _tabsToCheckPositionEvent.Count; i++)
                        {
                            if (_tabsToCheckPositionEvent[i].NameStrategy == this.NameStrategy)
                            {
                                _tabsToCheckPositionEvent.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }

                
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Whether the connector is connected to download data
        /// </summary>
        public bool IsConnected
        {
            get
            {

                if (_connector == null)
                {
                    return false;
                }

                return _connector.IsConnected;
            }
        }

        /// <summary>
        /// Connector is ready to send Orders
        /// </summary>
        public bool IsReadyToTrade
        {
            get
            {
                if (_connector == null)
                {
                    return false;
                }

                return _connector.IsReadyToTrade;
            }
        }

        /// <summary>
        /// The program that created the object
        /// </summary>
        public StartProgram StartProgram;

        // logging

        /// <summary>
        /// Put a new message in the log
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
        /// New log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        // indicator management

        /// <summary>
        /// Create indicator
        /// </summary>
        /// <param name="indicator">indicator</param>
        /// <param name="nameArea">the name of the area on which it will be placed. Default: "Prime"</param>
        public IIndicator CreateCandleIndicator(IIndicator indicator, string nameArea)
        {
            if (indicator == null)
                throw new Exception("Error! Indicator with name " + nameArea + " not found");

            return _chartMaster.CreateIndicator(indicator, nameArea);
        }

        /// <summary>
        /// Create and save indicator
        /// </summary>
        /// <param name="indicator">indicator</param>
        /// <param name="nameArea">the name of the area on which it will be placed. Default: "Prime"</param>
        public T CreateIndicator<T>(T indicator, string nameArea = "Prime") where T : IIndicator
        {
            T newIndicator = (T)_chartMaster.CreateIndicator(indicator, nameArea);
            newIndicator.Save();
            return newIndicator;
        }

        /// <summary>
        /// Remove indicator
        /// </summary>
        public void DeleteCandleIndicator(IIndicator indicator)
        {
            _chartMaster.DeleteIndicator(indicator);
        }

        /// <summary>
        /// All available indicators in the system
        /// </summary>
        public List<IIndicator> Indicators
        {
            get
            {
                if (_chartMaster == null)
                {
                    return null;
                }
                return _chartMaster.Indicators;
            }
        }

        // drawing elements

        /// <summary>
        /// Add custom element to the chart
        /// </summary>
        public void SetChartElement(IChartElement element)
        {
            _chartMaster.SetChartElement(element);
        }

        /// <summary>
        /// Remove user element from chart
        /// </summary>
        public void DeleteChartElement(IChartElement element)
        {
            _chartMaster.DeleteChartElement(element);
        }

        /// <summary>
        /// Remove all custom elements from the graphic
        /// </summary>
        public void DeleteAllChartElement()
        {
            _chartMaster.DeleteAllChartElement();
        }

        /// <summary>
        /// Get chart information
        /// </summary>
        public string GetChartLabel()
        {
            return _chartMaster.GetChartLabel();
        }

        /// <summary>
        /// Move the chart view all the way to the right
        /// </summary>
        public void MoveChartToTheRight()
        {
            _chartMaster.MoveChartToTheRight();
        }

        // closed components

        /// <summary>
        /// Class responsible for connecting the tab to the exchange
        /// </summary>
        public ConnectorCandles Connector
        {
            get { return _connector; }
        }
        private ConnectorCandles _connector;

        /// <summary>
        /// An object that holds settings for assembling candles
        /// </summary>
        public TimeFrameBuilder TimeFrameBuilder
        {
            get
            {
                if (_connector == null)
                {
                    return null;
                }
                return _connector.TimeFrameBuilder;
            }
        }

        /// <summary>
        /// Chart drawing master
        /// </summary>
        private ChartCandleMaster _chartMaster;

        /// <summary>
        /// Class drawing a marketDepth
        /// </summary>
        private MarketDepthPainter _marketDepthPainter;

        /// <summary>
        /// Transaction creation wizard
        /// </summary>
        public PositionCreator _dealCreator;

        /// <summary>
        /// Journal positions
        /// </summary>
        public Journal.Journal _journal;

        /// <summary>
        /// Settings maintenance settings
        /// </summary>
        public BotManualControl ManualPositionSupport;

        /// <summary>
        /// Alerts wizard
        /// </summary>
        public AlertMaster _alerts;

        /// <summary>
        /// New alert event
        /// </summary>
        public event Action AlertSignalEvent;

        public ChartCandleMaster GetChartMaster()
        {
            return _chartMaster;
        }

        // properties

        /// <summary>
        /// Flag indicates whether order emulation is enabled in the system
        /// </summary>
        public bool EmulatorIsOn
        {
            get
            {
                if (_connector == null)
                {
                    return false;
                }

                return _connector.EmulatorIsOn;
            }
            set
            {
                if (_connector == null || _journal == null)
                {
                    return;
                }

                List<Position> openPoses = _journal.OpenPositions;

                if (openPoses.Count > 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label192 + this.TabName, LogMessageType.Error);
                    return;
                }

                if (_connector.EmulatorIsOn == value)
                {
                    return;
                }

                _connector.EmulatorIsOn = value;
                _connector.Save();

                if (EmulatorIsOnChangeStateEvent != null)
                {
                    EmulatorIsOnChangeStateEvent(value);
                }
            }
        }

        public event Action<bool> EmulatorIsOnChangeStateEvent;

        /// <summary>
        ///  The status of the server to which the tab is connected
        /// </summary>
        public ServerConnectStatus ServerStatus
        {
            get
            {
                try
                {
                    if (StartProgram == StartProgram.IsOsOptimizer)
                    {
                        return ServerConnectStatus.Connect;
                    }

                    if (_connector == null)
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
                catch
                {
                    return ServerConnectStatus.Disconnect;
                }
            }
        }

        /// <summary>
        /// Security to trading
        /// </summary>
        public Security Securiti
        {
            get
            {
                try
                {
                    if (_connector == null)
                    {
                        return null;
                    }
                    if (_security == null ||
                        _security.Name != _connector.SecurityName)
                    {
                        _security = _connector.Security;
                    }
                    return _security;
                }
                catch
                {
                    return null;
                }
            }

            set { _security = value; }
        }
        private Security _security;

        /// <summary>
        /// Timeframe data received
        /// </summary>
        public TimeSpan TimeFrame
        {
            get
            {
                if (_connector == null)
                {
                    return TimeSpan.Zero;
                }
                return _connector.TimeFrameTimeSpan;
            }
        }

        /// <summary>
        /// Trading account
        /// </summary>
        public Portfolio Portfolio
        {
            get
            {
                if (_connector == null)
                {
                    return null;
                }

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
        /// Commission type for positions
        /// </summary>
        public ComissionType ComissionType
        {
            get
            {
                if (_journal == null)
                {
                    return ComissionType.None;
                }
                return _journal.ComissionType;
            }
            set
            {
                _journal.ComissionType = value;
                _connector.ComissionType = value;
            }
        }

        /// <summary>
        /// Commission amount
        /// </summary>
        public decimal ComissionValue
        {
            get
            {
                if (_journal == null)
                {
                    return 0;
                }

                return _journal.ComissionValue;
            }
            set
            {
                _journal.ComissionValue = value;
                _connector.ComissionValue = value;
            }
        }

        /// <summary>
        /// All positions are owned by bot. Open, closed and with errors
        /// </summary>
        public List<Position> PositionsAll
        {
            get
            {
                if (_journal == null)
                {
                    return null;
                }
                return _journal.AllPosition;
            }
        }

        /// <summary>
        /// All open, partially open and opening positions owned by bot
        /// </summary>
        public List<Position> PositionsOpenAll
        {
            get
            {
                if (_journal == null)
                {
                    return null;
                }
                return _journal.OpenPositions;
            }
        }

        /// <summary>
        /// Stop-limit orders
        /// </summary>
        public List<PositionOpenerToStopLimit> PositionOpenerToStopsAll
        {
            get { return PositionOpenerToStop; }
        }

        /// <summary>
        /// All closed, error positions owned by bot
        /// </summary>
        public List<Position> PositionsCloseAll
        {
            get
            {
                if (_journal == null)
                {
                    return null;
                }
                return _journal.CloseAllPositions;
            }
        }

        /// <summary>
        /// Last open position
        /// </summary>
        public Position PositionsLast
        {
            get
            {
                if (_journal == null)
                {
                    return null;
                }
                return _journal.LastPosition;
            }
        }

        /// <summary>
        /// All open positions are short
        /// </summary>
        public List<Position> PositionOpenShort
        {
            get
            {
                if (_journal == null)
                {
                    return null;
                }
                return _journal.OpenAllShortPositions;
            }
        }

        /// <summary>
        /// All open positions long
        /// </summary>
        public List<Position> PositionOpenLong
        {
            get
            {
                if (_journal == null)
                {
                    return null;
                }
                return _journal.OpenAllLongPositions;
            }
        }

        /// <summary>
        /// Exchange position for security
        /// </summary>
        public List<PositionOnBoard> PositionsOnBoard
        {
            get
            {
                try
                {
                    if (Portfolio == null
                        || Securiti == null)
                    {
                        return null;
                    }

                    List<PositionOnBoard> positionsOnBoard = Portfolio.GetPositionOnBoard();

                    List<PositionOnBoard> posesWithMySecurity = new List<PositionOnBoard>();

                    for (int i = 0; positionsOnBoard != null && i < positionsOnBoard.Count; i++)
                    {
                        if (positionsOnBoard[i] == null)
                        {
                            continue;
                        }

                        if (positionsOnBoard[i].SecurityNameCode.Contains(Securiti.Name))
                        {
                            posesWithMySecurity.Add(positionsOnBoard[i]);
                        }
                    }

                    return posesWithMySecurity;
                }
                catch (Exception error)
                {
                    SetNewLogMessage(error.ToString(), LogMessageType.Error);
                }

                return null;
            }
        }

        /// <summary>
        /// Net position recruited by the robot
        /// </summary>
        public decimal VolumeNetto
        {
            get
            {
                try
                {
                    List<Position> openPos = PositionsOpenAll;

                    if (openPos == null)
                    {
                        return 0;
                    }

                    decimal volume = 0;

                    for (int i = 0; openPos != null && i < openPos.Count; i++)
                    {
                        if (openPos[i].Direction == Side.Buy)
                        {
                            volume += openPos[i].OpenVolume;
                        }
                        else
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
        /// Were there closed positions on the current bar
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
        /// All candles of the instrument. Both molded and completed
        /// </summary>
        public List<Candle> CandlesAll
        {
            get
            {
                if (_connector == null)
                {
                    return null;
                }
                return _connector.Candles(false);
            }
        }

        /// <summary>
        /// All candles of the instrument. Only completed
        /// </summary>
        public List<Candle> CandlesFinishedOnly
        {
            get
            {
                if (_connector == null)
                {
                    return null;
                }
                return _connector.Candles(true);
            }
        }

        /// <summary>
        /// All instrument trades
        /// </summary>
        public List<Trade> Trades
        {
            get
            {
                if (_connector == null)
                {
                    return null;
                }
                return _connector.Trades;
            }
        }

        /// <summary>
        /// Server time
        /// </summary>
        public DateTime TimeServerCurrent
        {
            get
            {
                if (_connector == null)
                {
                    return DateTime.MinValue;
                }
                return _connector.MarketTime;
            }
        }

        /// <summary>
        /// MarketDepth
        /// </summary>
        public MarketDepth MarketDepth { get; set; }

        /// <summary>
        /// Best selling price
        /// </summary>
        public decimal PriceBestAsk
        {
            get
            {
                if (_connector == null)
                {
                    return 0;
                }
                return _connector.BestAsk;
            }
        }

        /// <summary>
        /// Best buy price
        /// </summary>
        public decimal PriceBestBid
        {
            get
            {
                if (_connector == null)
                {
                    return 0;
                }
                return _connector.BestBid;
            }
        }

        /// <summary>
        /// MarketDepth center price
        /// </summary>
        public decimal PriceCenterMarketDepth
        {
            get
            {
                if (_connector == null)
                {
                    return 0;
                }
                return (_connector.BestAsk + _connector.BestBid) / 2;
            }
        }

        /// <summary>
        /// Does the server support market orders
        /// </summary>
        public bool ServerIsSupportMarketOrders
        {
            get
            {
                if(_connector == null)
                {
                    return false;
                }

                return _connector.MarketOrdersIsSupport;
            }
        }

        /// <summary>
        /// Does the server support order price change
        /// </summary>
        public bool ServerIsSupportChangeOrderPrice
        {
            get
            {
                if (_connector == null)
                {
                    return false;
                }

                return _connector.IsCanChangeOrderPrice;
            }
        }

        // call control windows

        /// <summary>
        /// Show connector settings window
        /// </summary>
        public void ShowConnectorDialog()
        {
            _connector.ShowDialog(true);

            _journal.ComissionType = _connector.ComissionType;
            _journal.ComissionValue = _connector.ComissionValue;
        }

        /// <summary>
        /// Show custom settings window
        /// </summary>
        public void ShowManualControlDialog()
        {
            bool stopIsOnStartValue = ManualPositionSupport.StopIsOn;
            decimal stopDistance = ManualPositionSupport.StopDistance;
            decimal stopSlipage = ManualPositionSupport.StopSlipage;

            bool profitOnStartValue = ManualPositionSupport.ProfitIsOn;
            decimal profitDistance = ManualPositionSupport.ProfitDistance;
            decimal profitSlipage = ManualPositionSupport.ProfitSlipage;

            ManualPositionSupport.ShowDialog();

            bool neadToReplaceStop = false;

            if(ManualPositionSupport.StopIsOn == true
                && stopIsOnStartValue == false)
            {
                neadToReplaceStop = true;
            }
            if(ManualPositionSupport.StopIsOn == true
                && stopDistance != ManualPositionSupport.StopDistance)
            {
                neadToReplaceStop = true;
            }
            if (ManualPositionSupport.StopIsOn == true
                && stopSlipage != ManualPositionSupport.StopSlipage)
            {
                neadToReplaceStop = true;
            }

            if (ManualPositionSupport.ProfitIsOn == true
                && profitOnStartValue == false)
            {
                neadToReplaceStop = true;
            }
            if (ManualPositionSupport.ProfitIsOn == true
                && profitDistance != ManualPositionSupport.ProfitDistance)
            {
                neadToReplaceStop = true;
            }
            if (ManualPositionSupport.ProfitIsOn == true
                && profitSlipage != ManualPositionSupport.ProfitSlipage)
            {
                neadToReplaceStop = true;
            }

            List<Position> positions = this.PositionsOpenAll;

            if(positions.Count > 0 &&
                neadToReplaceStop &&
                IsCreatedByScreener == false)
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label386);

                ui.ShowDialog();

                if(ui.UserAcceptActioin)
                {
                    for(int i = 0;i < positions.Count;i++)
                    {
                        ManualReloadStopsAndProfitToPosition(positions[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Show position opening window
        /// </summary>
        public void ShowOpenPositionDialog()
        {
            BotTabSimple activTab = this;

            for (int i = 0; i < _guisOpenPos.Count; i++)
            {
                if (_guisOpenPos[i].Tab.TabName == activTab.TabName)
                {
                    _guisOpenPos[i].Activate();
                    return;
                }
            }

            PositionOpenUi2 ui = new PositionOpenUi2(activTab);
            ui.Show();

            _guisOpenPos.Add(ui);

            ui.Closing += Ui_Closing;
        }

        private List<PositionOpenUi2> _guisOpenPos = new List<PositionOpenUi2>();

        /// <summary>
        /// Window close event handler
        /// </summary>
        private void Ui_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                PositionOpenUi2 myUi = (PositionOpenUi2)sender;

                for (int i = 0; i < _guisOpenPos.Count; i++)
                {
                    if (_guisOpenPos[i].Tab.TabName == myUi.Tab.TabName)
                    {
                        _guisOpenPos.RemoveAt(i);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private List<PositionCloseUi2> _guisClosePos = new List<PositionCloseUi2>();

        /// <summary>
        /// Show position closing window
        /// </summary>
        /// <param name="position">position to be closed</param>
        public void ShowClosePositionDialog(Position position)
        {
            try
            {
                for (int i = 0; i < _guisClosePos.Count; i++)
                {
                    if (_guisClosePos[i].Position.Number == position.Number)
                    {
                        _guisClosePos[i].Activate();
                        _guisClosePos[i].SelectTabIndx(ClosePositionType.Limit);
                        return;
                    }
                }


                PositionCloseUi2 ui = new PositionCloseUi2(this, ClosePositionType.Limit, position);
                ui.Show();
                _guisClosePos.Add(ui);
                ui.Closing += Ui_Closing1;

            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Ui_Closing1(object sender, System.ComponentModel.CancelEventArgs e)
        {
            PositionCloseUi2 myUi = (PositionCloseUi2)sender;

            for (int i = 0; i < _guisClosePos.Count; i++)
            {
                if (_guisClosePos[i].Position.Number == myUi.Position.Number)
                {
                    _guisClosePos.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Show stop order window
        /// </summary>
        public void ShowStopSendDialog(Position position)
        {
            try
            {
                for (int i = 0; i < _guisClosePos.Count; i++)
                {
                    if (_guisClosePos[i].Position.Number == position.Number)
                    {
                        _guisClosePos[i].Activate();
                        _guisClosePos[i].SelectTabIndx(ClosePositionType.Stop);
                        return;
                    }
                }


                PositionCloseUi2 ui = new PositionCloseUi2(this, ClosePositionType.Stop, position);
                ui.Show();
                _guisClosePos.Add(ui);
                ui.Closing += Ui_Closing1;

            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Show profit order window
        /// </summary>
        public void ShowProfitSendDialog(Position position)
        {
            try
            {
                for (int i = 0; i < _guisClosePos.Count; i++)
                {
                    if (_guisClosePos[i].Position.Number == position.Number)
                    {
                        _guisClosePos[i].Activate();
                        _guisClosePos[i].SelectTabIndx(ClosePositionType.Profit);
                        return;
                    }
                }

                PositionCloseUi2 ui = new PositionCloseUi2(this, ClosePositionType.Profit, position);
                ui.Show();
                _guisClosePos.Add(ui);
                ui.Closing += Ui_Closing1;

            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Move the graph to the current time
        /// </summary>
        public void GoChartToThisTime(DateTime time)
        {
            _chartMaster.GoChartToTime(time);
        }

        /// <summary>
        /// Take the context menu for setting the chart and indicators
        /// </summary>
        public System.Windows.Forms.ContextMenu GetContextDialog()
        {
            return _chartMaster.GetContextMenu();
        }

        // standard public functions for position management

        /// <summary>
        /// Enter a long position at any price
        /// </summary>
        /// <param name="volume">volume</param>
        public Position BuyAtMarket(decimal volume)
        {
            try
            {
                if (_connector.IsConnected == false
                   || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return null;
                }
                decimal price = _connector.BestAsk;

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label290, LogMessageType.System);
                    return null;
                }

                if (!Connector.EmulatorIsOn)
                {
                    price = price + Securiti.PriceStep * 40;
                }

                OrderPriceType type = OrderPriceType.Market;

                TimeSpan timeLife = ManualPositionSupport.SecondToOpen;

                if (_connector.MarketOrdersIsSupport)
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
        /// Enter a long position at any price
        /// </summary>
        /// <param name="volume">volume to be entered</param>
        /// <param name="signalType">open position signal name</param>
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
        /// Enter position Long at a limit price
        /// </summary>
        /// <param name="volume">position volume</param>
        /// <param name="priceLimit">order price</param>
        public Position BuyAtLimit(decimal volume, decimal priceLimit)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return null;
                }

                return LongCreate(priceLimit, volume, OrderPriceType.Limit, ManualPositionSupport.SecondToOpen, false);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// Enter position Long at a limit price
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">opder price</param>
        /// <param name="signalType">>open position signal nameа. Will be written to position property: SignalTypeOpen</param>
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
        /// Enter position Long at iceberg
        /// </summary>
        /// <param name="volume">volum</param>
        /// <param name="price">order price</param>
        /// <param name="orderCount">iceberg orders count</param>
        public Position BuyAtAceberg(decimal volume, decimal price, int orderCount)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return null;
                }

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
                    SetNewLogMessage(OsLocalization.Trader.Label291, LogMessageType.System);
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
        /// Enter position Long at iceberg
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="price">order price</param>
        /// <param name="orderCount">iceberg orders count</param>
        /// <param name="signalType">>open position signal nameа. Will be written to position property: SignalTypeOpen</param>
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
        /// Enter position Long at price intersection
        /// </summary>
        /// <param name="volume">volum</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candels count</param>
        /// <param name="signalType">the opening signal. It will be written to the position as SignalTypeOpen</param>
        /// <param name="lifeTimeType">order life type</param>
        public void BuyAtStop(decimal volume, decimal priceLimit, decimal priceRedLine,
            StopActivateType activateType, int expiresBars, string signalType, PositionOpenerToStopLifeTimeType lifeTimeType)
        {
            try
            {
                if (_connector.IsConnected == false
                   || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

                PositionOpenerToStopLimit positionOpener = new PositionOpenerToStopLimit();

                positionOpener.Volume = volume;
                positionOpener.Security = Securiti.Name;
                positionOpener.Number = NumberGen.GetNumberDeal(StartProgram);
                positionOpener.ExpiresBars = expiresBars;
                positionOpener.TimeCreate = TimeServerCurrent;
                positionOpener.OrderCreateBarNumber = CandlesFinishedOnly.Count;
                positionOpener.TabName = TabName;
                positionOpener.LifeTimeType = lifeTimeType;
                positionOpener.PriceOrder = priceLimit;
                positionOpener.PriceRedLine = priceRedLine;
                positionOpener.ActivateType = activateType;
                positionOpener.Side = Side.Buy;
                positionOpener.SignalType = signalType;
                positionOpener.OrderPriceType = OrderPriceType.Limit;

                PositionOpenerToStop.Add(positionOpener);
                UpdateStopLimits();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Enter position Long at price intersection
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// /// <param name="expiresBars">life time in candels count</param>
        public void BuyAtStop(decimal volume, decimal priceLimit, decimal priceRedLine,
            StopActivateType activateType, int expiresBars, string signalType)
        {
            BuyAtStop(volume, priceLimit, priceRedLine, activateType, expiresBars, signalType, PositionOpenerToStopLifeTimeType.CandlesCount);
        }

        /// <summary>
        /// Enter position Long at price intersection
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// /// <param name="expiresBars">life time in candels count</param>
        public void BuyAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, int expiresBars)
        {
            BuyAtStop(volume, priceLimit, priceRedLine, activateType, expiresBars, "", PositionOpenerToStopLifeTimeType.CandlesCount);
        }

        /// <summary>
        /// Enter position Long at price intersection. work one candle
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        public void BuyAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType)
        {
            BuyAtStop(volume, priceLimit, priceRedLine, activateType, 1, "", PositionOpenerToStopLifeTimeType.CandlesCount);
        }

        /// <summary>
        /// Enter position Long at price intersection. work one candle
        /// </summary>
        /// /// <param name="volume">volum</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="signalType">the opening signal. It will be written to the position as SignalTypeOpen</param>
        public void BuyAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, string signalType)
        {
            BuyAtStop(volume, priceLimit, priceRedLine, activateType, 1, signalType, PositionOpenerToStopLifeTimeType.CandlesCount);
        }

        /// <summary>
        /// Enter position Long at price intersection by Market
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candles count</param>
        /// <param name="signalType">the opening signal. It will be written to the position as SignalTypeOpen</param>
        /// <param name="lifeTimeType">order life type</param>
        public void BuyAtStopMarket(decimal volume, decimal priceLimit, decimal priceRedLine,
            StopActivateType activateType, int expiresBars, string signalType, PositionOpenerToStopLifeTimeType lifeTimeType)
        {
            try
            {
                if (_connector.IsConnected == false
                   || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

                PositionOpenerToStopLimit positionOpener = new PositionOpenerToStopLimit();

                positionOpener.Volume = volume;
                positionOpener.Security = Securiti.Name;
                positionOpener.Number = NumberGen.GetNumberDeal(StartProgram);
                positionOpener.ExpiresBars = expiresBars;
                positionOpener.TimeCreate = TimeServerCurrent;
                positionOpener.OrderCreateBarNumber = CandlesFinishedOnly.Count;
                positionOpener.TabName = TabName;
                positionOpener.LifeTimeType = lifeTimeType;
                positionOpener.PriceOrder = priceLimit;
                positionOpener.PriceRedLine = priceRedLine;
                positionOpener.ActivateType = activateType;
                positionOpener.Side = Side.Buy;
                positionOpener.SignalType = signalType;
                positionOpener.OrderPriceType = OrderPriceType.Market;

                PositionOpenerToStop.Add(positionOpener);
                UpdateStopLimits();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Add new order to Long position at limit
        /// </summary>
        /// <param name="position">position to which the order will be added</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="volume">volume</param>
        public void BuyAtLimitToPosition(Position position, decimal priceLimit, decimal volume)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

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
        /// Add new order to Short position at market
        /// </summary>
        /// <param name="position">position to which the order will be added</param>
        /// <param name="volume">volume</param>
        public void BuyAtMarketToPosition(Position position, decimal volume)
        {
            try
            {
                if (_connector.IsConnected == false
                   || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

                if (position.Direction == Side.Sell)
                {
                    SetNewLogMessage(TabName + OsLocalization.Trader.Label65, LogMessageType.Error);

                    return;
                }

                decimal price = _connector.BestAsk;

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label290, LogMessageType.System);
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

                if (_connector.MarketOrdersIsSupport)
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
        /// Add new order to exist position at market
        /// </summary>
        /// <param name="position">position to which the order will be added</param>
        /// <param name="volume">volume</param>
        /// <param name="signalType">the opening signal. It will be written to the position as SignalTypeOpen</param>
        public void BuyAtMarketToPosition(Position position, decimal volume, string signalType)
        {
            position.SignalTypeOpen = signalType;
            BuyAtMarketToPosition(position, volume);
        }

        /// <summary>
        /// Add new order to Long position at iceberg
        /// </summary>
        /// <param name="position">position to which the order will be added</param>
        /// <param name="price">order price</param>
        /// <param name="volume">volume</param>
        /// <param name="orderCount">iceberg orders count</param>
        public void BuyAtAcebergToPosition(Position position, decimal price, decimal volume, int orderCount)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

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
                    SetNewLogMessage(OsLocalization.Trader.Label291, LogMessageType.System);
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
        /// Cancel all purchase requisitions at level cross
        /// </summary>
        public void BuyAtStopCancel()
        {
            try
            {
                if (PositionOpenerToStop == null || PositionOpenerToStop.Count == 0)
                {
                    return;
                }

                for (int i = 0; PositionOpenerToStop.Count != 0 && i < PositionOpenerToStop.Count; i++)
                {
                    if (PositionOpenerToStop[i].Side == Side.Buy)
                    {
                        PositionOpenerToStop.RemoveAt(i);
                        i--;
                    }
                }
                UpdateStopLimits();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Enter a FAKE long position
        /// </summary>
        /// <param name="volume">volume</param>
        public Position BuyAtFake(decimal volume, decimal price, DateTime time)
        {
            try
            {
                Side direction = Side.Buy;

                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63,
                        LogMessageType.System);
                    return null;
                }

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label291, LogMessageType.System);
                    return null;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }
                price = RoundPrice(price, Securiti, direction);

                Position newDeal = _dealCreator.CreatePosition(TabName, direction, price, volume, OrderPriceType.Limit,
                    ManualPositionSupport.SecondToOpen, Securiti, Portfolio, StartProgram);

                _journal.SetNewDeal(newDeal);

                OrderFakeExecute(newDeal.OpenOrders[0], time);
                return newDeal;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;

        }

        /// <summary>
        /// Enter a FAKE long position
        /// </summary>
        /// <param name="volume">volume</param>
        public Position SellAtFake(decimal volume, decimal price, DateTime time)
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
                    SetNewLogMessage(OsLocalization.Trader.Label291, LogMessageType.System);
                    return null;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                price = RoundPrice(price, Securiti, direction);

                Position newDeal = _dealCreator.CreatePosition(TabName, direction, price, volume, OrderPriceType.Limit,
                    ManualPositionSupport.SecondToOpen, Securiti, Portfolio, StartProgram);

                _journal.SetNewDeal(newDeal);

                OrderFakeExecute(newDeal.OpenOrders[0], time);
                return newDeal;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;

        }

        /// <summary>
        /// Execute order in Fake mode
        /// </summary>
        public void OrderFakeExecute(Order order, DateTime timeExecute)
        {
            try
            {
                order.TimeCreate = timeExecute;
                order.TimeCallBack = timeExecute;

                Order newOrder = new Order();
                newOrder.NumberMarket = "fakeOrder " + NumberGen.GetNumberOrder(StartProgram);
                newOrder.NumberUser = order.NumberUser;
                newOrder.State = OrderStateType.Done;
                newOrder.Volume = order.Volume;
                newOrder.VolumeExecute = order.Volume;
                newOrder.Price = order.Price;
                newOrder.TimeCreate = timeExecute;
                newOrder.TypeOrder = order.TypeOrder;
                newOrder.TimeCallBack = timeExecute;
                newOrder.Side = order.Side;
                newOrder.SecurityNameCode = order.SecurityNameCode;
                newOrder.PortfolioNumber = order.PortfolioNumber;
                newOrder.ServerType = order.ServerType;

                _connector_OrderChangeEvent(newOrder);

                MyTrade trade = new MyTrade();

                trade.Volume = order.Volume;
                trade.Time = timeExecute;
                trade.Price = order.Price;
                trade.SecurityNameCode = order.SecurityNameCode;
                trade.NumberTrade = "fakeTrade " + NumberGen.GetNumberOrder(StartProgram);
                trade.Side = order.Side;
                trade.NumberOrderParent = newOrder.NumberMarket;

                _connector_MyTradeEvent(trade);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Enter the short position at any price
        /// </summary>
        /// <param name="volume">volume</param>
        public Position SellAtMarket(decimal volume)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return null;
                }

                decimal price = _connector.BestBid;

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label290, LogMessageType.System);
                    return null;
                }

                if (!Connector.EmulatorIsOn)
                {
                    price = price - Securiti.PriceStep * 40;
                }

                OrderPriceType type = OrderPriceType.Market;

                TimeSpan timeLife = ManualPositionSupport.SecondToOpen;

                if (_connector.MarketOrdersIsSupport)
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
        /// Enter the short position at any price
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="signalType">open position signal name</param>
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
        /// Enter the short position at limit price
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        public Position SellAtLimit(decimal volume, decimal priceLimit)
        {
            try
            {
                if (_connector.IsConnected == false
                   || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return null;
                }

                return ShortCreate(priceLimit, volume, OrderPriceType.Limit, ManualPositionSupport.SecondToOpen, false);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// Enter the short position at limit price
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="signalType">open position signal name. Will be written to position property: SignalTypeOpen</param>
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
        /// Enter the short position at iceberg
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="price">price</param>
        /// <param name="orderCount">iceberg orders count</param>
        public Position SellAtAceberg(decimal volume, decimal price, int orderCount)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return null;
                }

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
                    SetNewLogMessage(OsLocalization.Trader.Label291, LogMessageType.System);
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
        /// enter the short position at iceberg
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="price">price</param>
        /// <param name="orderCount">orders count</param>
        /// <param name="signalType">open position signal name. Will be written to position property: SignalTypeOpen</param>
        public Position SellAtAceberg(decimal volume, decimal price, int orderCount, string signalType)
        {
            if (_connector.IsConnected == false
                || _connector.IsReadyToTrade == false)
            {
                SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                return null;
            }

            Position position = SellAtAceberg(volume, price, orderCount);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// Enter position Short at price intersection
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">line price, after reaching which a sell order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candels count</param>
        /// <param name="signalType">the opening signal. It will be written to the position as SignalTypeOpen</param>
        /// <param name="lifeTimeType">order life type</param>
        public void SellAtStop(decimal volume, decimal priceLimit, decimal priceRedLine,
            StopActivateType activateType, int expiresBars, string signalType, PositionOpenerToStopLifeTimeType lifeTimeType)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

                PositionOpenerToStopLimit positionOpener = new PositionOpenerToStopLimit();

                positionOpener.Volume = volume;
                positionOpener.Security = Securiti.Name;
                positionOpener.Number = NumberGen.GetNumberDeal(StartProgram);
                positionOpener.TabName = TabName;
                positionOpener.ExpiresBars = expiresBars;
                positionOpener.TimeCreate = TimeServerCurrent;
                positionOpener.OrderCreateBarNumber = CandlesFinishedOnly.Count;
                positionOpener.LifeTimeType = lifeTimeType;
                positionOpener.PriceOrder = priceLimit;
                positionOpener.PriceRedLine = priceRedLine;
                positionOpener.ActivateType = activateType;
                positionOpener.Side = Side.Sell;
                positionOpener.SignalType = signalType;
                positionOpener.OrderPriceType = OrderPriceType.Limit;

                PositionOpenerToStop.Add(positionOpener);
                UpdateStopLimits();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Enter position Short at price intersection
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">line price, after reaching which a sell order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candels count </param>
        /// <param name="signalType">the opening signal. It will be written to the position as SignalTypeOpen</param>
        public void SellAtStop(decimal volume, decimal priceLimit, decimal priceRedLine,
            StopActivateType activateType, int expiresBars, string signalType)
        {
            SellAtStop(volume, priceLimit, priceRedLine, activateType, expiresBars, signalType, PositionOpenerToStopLifeTimeType.CandlesCount);
        }

        /// <summary>
        /// Enter position Short at price intersection
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">line price, after reaching which a sell order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candels count</param>
        public void SellAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, int expiresBars)
        {
            SellAtStop(volume, priceLimit, priceRedLine, activateType, expiresBars, "", PositionOpenerToStopLifeTimeType.CandlesCount);
        }

        /// <summary>
        /// Enter position Short at price intersection. Work one candle
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">line price, after reaching which a sell order will be placed</param>
        /// <param name="activateType">activation type</param>
        public void SellAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType)
        {
            SellAtStop(volume, priceLimit, priceRedLine, activateType, 1, "", PositionOpenerToStopLifeTimeType.CandlesCount);
        }

        /// <summary>
        /// Enter position Short at price intersection. Work one candle
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">line price, after reaching which a sell order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="signalType">the opening signal. It will be written to the position as SignalTypeOpen</param>
        public void SellAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, string signalType)
        {
            SellAtStop(volume, priceLimit, priceRedLine, activateType, 1, signalType, PositionOpenerToStopLifeTimeType.CandlesCount);
        }

        /// <summary>
        /// Enter position Short at price intersection
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">line price, after reaching which a sell order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candles count</param>
        /// <param name="signalType">the opening signal. It will be written to the position as SignalTypeOpen</param>
        /// <param name="lifeTimeType">order life type</param>
        public void SellAtStopMarket(decimal volume, decimal priceLimit, decimal priceRedLine,
            StopActivateType activateType, int expiresBars, string signalType, PositionOpenerToStopLifeTimeType lifeTimeType)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

                PositionOpenerToStopLimit positionOpener = new PositionOpenerToStopLimit();

                positionOpener.Volume = volume;
                positionOpener.Security = Securiti.Name;
                positionOpener.Number = NumberGen.GetNumberDeal(StartProgram);
                positionOpener.TabName = TabName;
                positionOpener.ExpiresBars = expiresBars;
                positionOpener.TimeCreate = TimeServerCurrent;
                positionOpener.OrderCreateBarNumber = CandlesFinishedOnly.Count;
                positionOpener.LifeTimeType = lifeTimeType;
                positionOpener.PriceOrder = priceLimit;
                positionOpener.PriceRedLine = priceRedLine;
                positionOpener.ActivateType = activateType;
                positionOpener.Side = Side.Sell;
                positionOpener.SignalType = signalType;
                positionOpener.OrderPriceType = OrderPriceType.Market;

                PositionOpenerToStop.Add(positionOpener);
                UpdateStopLimits();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }


        /// <summary>
        /// Add new order to Short position at limit
        /// </summary>
        /// <param name="position">position to which the order will be added</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="volume">volume</param>
        public void SellAtLimitToPosition(Position position, decimal priceLimit, decimal volume)
        {
            try
            {
                if (_connector.IsConnected == false
                   || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }
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
        /// Add new order to Short position at market 
        /// </summary>
        /// <param name="position">position to which the order will be added</param>
        /// <param name="volume">volume</param>
        public void SellAtMarketToPosition(Position position, decimal volume)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

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

                if (_connector.MarketOrdersIsSupport)
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
        /// Add new order to Short position at market 
        /// </summary>
        /// <param name="position">position to which the order will be added</param>
        /// <param name="volume">volume</param>
        /// <param name="signalType">the opening signal. It will be written to the position as SignalTypeOpen</param>
        public void SellAtMarketToPosition(Position position, decimal volume, string signalType)
        {
            position.SignalTypeOpen = signalType;
            SellAtMarketToPosition(position, volume);
        }

        /// <summary>
        /// Add new order to Short position at iceberg
        /// </summary>
        /// <param name="position">position to which the order will be added</param>
        /// <param name="price">order price</param>
        /// <param name="volume">volum</param>
        /// <param name="orderCount">iceberg orders count</param>
        public void SellAtAcebergToPosition(Position position, decimal price, decimal volume, int orderCount)
        {
            try
            {
                if (_connector.IsConnected == false
                   || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

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
                    SetNewLogMessage(OsLocalization.Trader.Label291, LogMessageType.System);
                    return;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return;
                }

                if (Securiti != null)
                {// if we do not test, then we cut the price by the minimum step of the instrument
                    if (Convert.ToDouble(Securiti.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    { // truncate if decimal places
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
                    { // truncate if decimal places
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
        /// Cancel all purchase requisitions at level cross
        /// </summary>
        public void SellAtStopCancel()
        {
            try
            {
                if (PositionOpenerToStop == null || PositionOpenerToStop.Count == 0)
                {
                    return;
                }

                for (int i = 0; PositionOpenerToStop.Count != 0 && i < PositionOpenerToStop.Count; i++)
                {
                    if (PositionOpenerToStop[i].Side == Side.Sell)
                    {
                        PositionOpenerToStop.RemoveAt(i);
                        i--;
                    }
                }

                UpdateStopLimits();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Close all positions on the market
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
        /// Close all positions at market
        /// </summary>
        /// <param name="signalType">close position signal name</param>
        public void CloseAllAtMarket(string signalType)
        {
            try
            {
                List<Position> positions = _journal.OpenPositions;

                if (positions != null)
                {
                    for (int i = 0; i < positions.Count; i++)
                    {
                        CloseAtMarket(positions[i], positions[i].OpenVolume, signalType);
                    }
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Close the position in Fake mode
        /// </summary>
        /// <param name="position">position to be closed</param>
        public void CloseAtFake(Position position, decimal volume, decimal price, DateTime time)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

                if (volume <= 0 || position.OpenVolume <= 0)
                {
                    return;
                }

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

                Order closeOrder
                    = _dealCreator.CreateCloseOrderForDeal(Securiti, position, price, OrderPriceType.Limit, new TimeSpan(1, 1, 1, 1), StartProgram); ;

                closeOrder.SecurityNameCode = Securiti.Name;
                closeOrder.SecurityClassCode = Securiti.NameClass;
                closeOrder.PortfolioNumber = Portfolio.Number;

                if(volume < position.OpenVolume &&
                    closeOrder.Volume != volume)
                {
                    closeOrder.Volume = volume;
                }

                position.AddNewCloseOrder(closeOrder);

                OrderFakeExecute(closeOrder, time);

            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Close a position at any price
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="volume">volume</param>
        public void CloseAtMarket(Position position, decimal volume)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

                if (volume <= 0 || position.OpenVolume <= 0)
                {
                    return;
                }
                decimal price = _connector.BestAsk;

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label290, LogMessageType.System);
                    return;
                }

                if (position.Direction == Side.Buy)
                {
                    if (!Connector.EmulatorIsOn)
                    {
                        price = _connector.BestBid - Securiti.PriceStep * 40;
                    }
                    else
                    {
                        price = _connector.BestBid;
                    }
                }
                else
                {
                    if (!Connector.EmulatorIsOn)
                    {
                        price = price + Securiti.PriceStep * 40;
                    }
                }

                if (_connector.MarketOrdersIsSupport)
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
        /// Close a position at any price
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="volume">volume</param>
        /// <param name="signalType">close position signal name. Will be written to position property: SignalTypeClose</param>
        public void CloseAtMarket(Position position, decimal volume, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtMarket(position, volume);
        }

        /// <summary>
        /// Close a position at a limit price
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="volume">volume required to close</param>
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
        /// Close a position at a limit price
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="volume">volume required to close</param>
        /// <param name="signalType">close position signal name. Will be written to position property: SignalTypeClose</param>
        public void CloseAtLimit(Position position, decimal priceLimit, decimal volume, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtLimit(position, priceLimit, volume);
        }

        /// <summary>
        /// Close position at iceberg
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="volume">volume required to close</param>
        /// <param name="orderCount">iceberg orders count</param>
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
        /// Close position at iceberg
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="volume">volume required to close</param>
        /// <param name="orderCount">iceberg orders count</param>
        /// <param name="signalType">close position signal name</param>
        public void CloseAtAceberg(Position position, decimal priceLimit, decimal volume, int orderCount, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtAceberg(position, priceLimit, volume, orderCount);
        }

        /// <summary>
        /// Place a stop order for a position
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        /// <param name="priceOrder">order price</param>
        public void CloseAtStop(Position position, decimal priceActivation, decimal priceOrder)
        {
            TryReloadStop(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// Place a stop order for a position
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        /// <param name="priceOrder">order price</param>
        /// <param name="signalType">close position signal name</param>
        public void CloseAtStop(Position position, decimal priceActivation, decimal priceOrder, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtStop(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// Place a stop market order for a position
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        public void CloseAtStopMarket(Position position, decimal priceActivation)
        {
            try
            {
                if (position == null)
                {
                    return;
                }

                if (position.State == PositionStateType.Done ||
                    position.State == PositionStateType.OpeningFail)
                {
                    return;
                }

                if (position.StopOrderIsActiv &&
                    position.StopOrderPrice == priceActivation &&
                    position.StopOrderRedLine == priceActivation &&
                    position.StopIsMarket == true)
                {
                    return;
                }

                decimal volume = position.OpenVolume;

                if (volume == 0)
                {
                    return;
                }

                position.StopOrderIsActiv = false;

                position.StopIsMarket = true;
                position.StopOrderPrice = priceActivation;
                position.StopOrderRedLine = priceActivation;
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
        /// Place a stop market order for a position
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        /// <param name="signalType">close position signal name</param>
        public void CloseAtStopMarket(Position position, decimal priceActivation, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtStopMarket(position, priceActivation);
        }

        /// <summary>
        /// Place a trailing stop order for a position
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        /// <param name="priceOrder">order price</param>
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
        /// Place a trailing stop order for a position
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        /// <param name="priceOrder">order price</param>
        /// <param name="signalType">close position signal name</param>
        public void CloseAtTrailingStop(Position position, decimal priceActivation, decimal priceOrder, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtTrailingStop(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// Place a trailing stop order for a position by Market
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        public void CloseAtTrailingStopMarket(Position position, decimal priceActivation)
        {
            if (position.StopOrderIsActiv &&
                position.Direction == Side.Buy &&
                position.StopOrderRedLine > priceActivation)
            {
                return;
            }

            if (position.StopOrderIsActiv &&
                position.Direction == Side.Sell &&
                position.StopOrderRedLine < priceActivation)
            {
                return;
            }

            decimal volume = position.OpenVolume;

            if (volume == 0)
            {
                return;
            }

            position.StopOrderIsActiv = false;

            position.StopIsMarket = true;
            position.StopOrderPrice = priceActivation;
            position.StopOrderRedLine = priceActivation;
            position.StopOrderIsActiv = true;

            _chartMaster.SetPosition(_journal.AllPosition);
            _journal.PaintPosition(position);
            _journal.Save();
        }

        /// <summary>
        /// Place a trailing stop order for a position by Market
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        /// <param name="signalType">close position signal name</param>
        public void CloseAtTrailingStopMarket(Position position, decimal priceActivation, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtTrailingStopMarket(position, priceActivation);
        }

        /// <summary>
        /// Place profit order for a position
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        /// <param name="priceOrder">order price</param>
        public void CloseAtProfit(Position position, decimal priceActivation, decimal priceOrder)
        {
            TryReloadProfit(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// Place profit order for a position
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        /// <param name="priceOrder">order price</param>
        /// <param name="signalType">close position signal name</param>
        public void CloseAtProfit(Position position, decimal priceActivation, decimal priceOrder, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtProfit(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// Place profit market order for a position
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        public void CloseAtProfitMarket(Position position, decimal priceActivation)
        {
            try
            {
                if (position == null)
                {
                    return;
                }

                if (position.State == PositionStateType.Done ||
                    position.State == PositionStateType.OpeningFail)
                {
                    return;
                }

                if (position.ProfitOrderIsActiv &&
                    position.ProfitOrderPrice == priceActivation &&
                    position.ProfitOrderRedLine == priceActivation &&
                    position.ProfitIsMarket == true)
                {
                    return;
                }

                decimal volume = position.OpenVolume;

                if (volume == 0)
                {
                    return;
                }


                position.ProfitOrderIsActiv = false;

                position.ProfitOrderPrice = priceActivation;
                position.ProfitOrderRedLine = priceActivation;
                position.ProfitIsMarket = true;
                
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
        /// Place profit market order for a position
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        /// <param name="signalType">close position signal name</param>
        public void CloseAtProfitMarket(Position position, decimal priceActivation, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtProfitMarket(position, priceActivation);
        }

        /// <summary>
        /// Withdraw all robot open orders from the system
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
        /// Close all robot open orders from the system
        /// </summary>
        /// <param name="signalType">close position signal name</param>
        public void CloseAllOrderInSystem(string signalType)
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
                    CloseAllOrderToPosition(positions[i], signalType);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Withdraw all orders from the system associated with this transaction
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
                        if (order.State == OrderStateType.Activ)
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

                        if (closeOrder.State == OrderStateType.Activ)
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
        /// Withdraw all orders from the system associated with this transaction
        /// </summary>
		/// <param name = "signalType" > close position signal name. Will be written to position property: SignalTypeClose</param>
        public void CloseAllOrderToPosition(Position position, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAllOrderToPosition(position);
        }

        /// <summary>
        /// Withdraw order
        /// </summary>
        public void CloseOrder(Order order)
        {
            _connector.OrderCancel(order);
        }

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            if(order == null)
            {
                return;
            }

            if(StartProgram != StartProgram.IsOsTrader)
            {
                SetNewLogMessage(OsLocalization.Trader.Label371, LogMessageType.Error);
                return;
            }

            if(IsConnected == false ||
                IsReadyToTrade == false)
            {
                SetNewLogMessage(OsLocalization.Trader.Label372, LogMessageType.Error);
                return;
            }

            _connector.ChangeOrderPrice(order, newPrice);
        }

        // internal position management functions

        /// <summary>
        /// Create short position
        /// </summary>
        /// <param name="price">price order</param>
        /// <param name="volume">volume</param>
        /// <param name="priceType">price type</param>
        /// <param name="timeLife">life time</param>
        /// <param name="isStopOrProfit">whether the order is a result of a stop or a profit </param>
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
                    SetNewLogMessage(OsLocalization.Trader.Label291, LogMessageType.System);
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
        /// Modify position by short order
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="price">order price</param>
        /// <param name="volume">volume</param>
        /// <param name="timeLife">life time</param>
        /// <param name="isStopOrProfit">whether the order is a result of a stop or a profit</param>
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
                    SetNewLogMessage(OsLocalization.Trader.Label291, LogMessageType.System);
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

                Order newOrder = _dealCreator.CreateOrder(Securiti, Side.Sell, price, volume, OrderType,
                    ManualPositionSupport.SecondToOpen, StartProgram, OrderPositionConditionType.Open);
                newOrder.IsStopOrProfit = isStopOrProfit;
                newOrder.LifeTime = timeLife;
                position.AddNewOpenOrder(newOrder);

                SetNewLogMessage(Securiti.Name + " short position modification", LogMessageType.Trade);

                if (position.OpenOrders[0].SecurityNameCode.EndsWith(" TestPaper"))
                {
                    _connector.OrderExecute(newOrder, true);
                }
                else
                {
                    _connector.OrderExecute(newOrder);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Create long position
        /// </summary>
        /// <param name="price">price order</param>
        /// <param name="volume">volume</param>
        /// <param name="priceType">price type</param>
        /// <param name="timeLife">life time</param>
        /// <param name="isStopOrProfit">whether the order is a result of a stop or a profit</param>
        private Position LongCreate(decimal price, decimal volume, OrderPriceType priceType, TimeSpan timeLife,
            bool isStopOrProfit)
        {
            try
            {
                Side direction = Side.Buy;

                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63, LogMessageType.System);
                    return null;
                }

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label291, LogMessageType.System);
                    return null;
                }

                if (Securiti == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                price = RoundPrice(price, Securiti, Side.Buy);

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
        /// Modify position by long order
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="price">order price</param>
        /// <param name="volume">volume</param>
        /// <param name="timeLife">life time</param>
        /// <param name="isStopOrProfit">whether the order is a result of a stop or a profit</param>
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
                    SetNewLogMessage(OsLocalization.Trader.Label291, LogMessageType.System);
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

                Order newOrder = _dealCreator.CreateOrder(Securiti, Side.Buy, price, volume, OrderType,
                    ManualPositionSupport.SecondToOpen, StartProgram, OrderPositionConditionType.Open);
                newOrder.IsStopOrProfit = isStopOrProfit;
                newOrder.LifeTime = timeLife;
                newOrder.SecurityNameCode = Securiti.Name;
                newOrder.SecurityClassCode = Securiti.NameClass;

                position.AddNewOpenOrder(newOrder);

                if (position.OpenOrders[0].SecurityNameCode.EndsWith(" TestPaper"))
                {
                    _connector.OrderExecute(newOrder, true);
                }
                else
                {
                    _connector.OrderExecute(newOrder);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Close position
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="priceType">price type</param>
        /// <param name="price">price</param>
        /// <param name="lifeTime">life time order</param>
        /// <param name="isStopOrProfit">whether the order is a result of a stop or a profit</param>
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

                Order closeOrder = _dealCreator.CreateCloseOrderForDeal(Securiti, position, price, priceType, lifeTime, StartProgram);

                closeOrder.SecurityNameCode = Securiti.Name;
                closeOrder.SecurityClassCode = Securiti.NameClass;

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

                if (position.OpenOrders[0].SecurityNameCode.EndsWith(" TestPaper"))
                {
                    _connector.OrderExecute(closeOrder, true);
                }
                else
                {
                    _connector.OrderExecute(closeOrder);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Partially close a position
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="priceType">price type</param>
        /// <param name="price">price</param>
        /// <param name="lifeTime">life time</param>
        /// <param name="volume">volume</param>
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

                Order closeOrder = _dealCreator.CreateCloseOrderForDeal(Securiti, position, price, priceType, lifeTime, StartProgram);


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

                if (position.OpenOrders[0].SecurityNameCode.EndsWith(" TestPaper"))
                {
                    _connector.OrderExecute(closeOrder, true);
                }
                else
                {
                    _connector.OrderExecute(closeOrder);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Restart stop
        /// </summary>
        /// <param name="position">positin</param>
        /// <param name="priceActivate">price activation</param>
        /// <param name="priceOrder">order price</param>
        private void TryReloadStop(Position position, decimal priceActivate, decimal priceOrder)
        {
            try
            {
                if (position == null)
                {
                    return;
                }

                if (position.State == PositionStateType.Done ||
                    position.State == PositionStateType.OpeningFail)
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

                if (StartProgram == StartProgram.IsOsOptimizer ||
                    StartProgram == StartProgram.IsTester)
                {
                    // check that the stop is no further than the activation price deep in the market

                    decimal lastBid = PriceBestBid;
                    decimal lastAsk = PriceBestAsk;

                    if (lastAsk != 0 && lastBid != 0)
                    {
                        if (position.Direction == Side.Buy &&
                            priceActivate > lastAsk)
                        {
                            //priceActivate = lastAsk;
                            //SetNewLogMessage(
                            //    OsLocalization.Trader.Label180
                            //    , LogMessageType.Error);
                        }
                        if (position.Direction == Side.Sell &&
                            priceActivate < lastBid)
                        {
                            // priceActivate = lastBid;
                            //SetNewLogMessage(
                            //    OsLocalization.Trader.Label180
                            //    , LogMessageType.Error);
                        }
                    }

                    priceOrder = priceActivate;
                }

                position.StopOrderIsActiv = false;

                if (StartProgram == StartProgram.IsOsOptimizer ||
                    StartProgram == StartProgram.IsTester)
                {
                    position.StopOrderPrice = priceActivate;
                }
                else
                {
                    position.StopOrderPrice = priceOrder;
                }
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
        /// Restart profit
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="priceActivate">price after which the order will be placed</param>
        /// <param name="priceOrder">order price</param>
        private void TryReloadProfit(Position position, decimal priceActivate, decimal priceOrder)
        {
            try
            {
                if (position == null)
                {
                    return;
                }

                if (position.State == PositionStateType.Done ||
                    position.State == PositionStateType.OpeningFail)
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

                if (StartProgram == StartProgram.IsOsOptimizer ||
                    StartProgram == StartProgram.IsTester)
                {
                    // check that the profit is no further than the activation price deep in the market

                    decimal lastBid = PriceBestBid;
                    decimal lastAsk = PriceBestAsk;

                    if (lastAsk != 0 && lastBid != 0)
                    {
                        if (position.Direction == Side.Buy &&
                            priceActivate < lastBid)
                        {
                            // priceActivate = lastBid;
                            // SetNewLogMessage(
                            //    OsLocalization.Trader.Label181
                            //    , LogMessageType.Error);
                        }
                        if (position.Direction == Side.Sell &&
                            priceActivate > lastAsk)
                        {
                            // priceActivate = lastAsk;
                            //SetNewLogMessage(
                            //   OsLocalization.Trader.Label181
                            //   , LogMessageType.Error);
                        }
                    }

                    priceOrder = priceActivate;
                }


                position.ProfitOrderIsActiv = false;

                if (StartProgram == StartProgram.IsOsOptimizer ||
                    StartProgram == StartProgram.IsTester)
                {
                    position.ProfitOrderPrice = priceActivate;
                }
                else
                {
                    position.ProfitOrderPrice = priceOrder;
                }

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
        /// Adjust order price to the needs of the exchange
        /// </summary>
        /// <param name="price">the current price at which the high-level interface wanted to close the position</param>
        /// <param name="security">security</param>
        /// <param name="side">side</param>
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

        private object _lockerManualReload = new object();

        /// <summary>
        /// Check the manual support of the stop and profi
        /// </summary>
        /// <param name="position">position</param>
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
        /// Check if the trade has a stop or profit
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

                        if(position.StopIsMarket == false
                            || StartProgram == StartProgram.IsTester 
                            || StartProgram == StartProgram.IsOsOptimizer)
                        {
                            CloseDeal(position, OrderPriceType.Limit, position.StopOrderPrice, ManualPositionSupport.SecondToClose, true);
                        }
                        else
                        {
                            CloseDeal(position, OrderPriceType.Market, position.StopOrderPrice, ManualPositionSupport.SecondToClose, true);
                        }

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

                        if (position.StopIsMarket == false
                           || StartProgram == StartProgram.IsTester
                           || StartProgram == StartProgram.IsOsOptimizer)
                        {
                            CloseDeal(position, OrderPriceType.Limit, position.StopOrderPrice, ManualPositionSupport.SecondToClose, true);
                        }
                        else
                        {
                            CloseDeal(position, OrderPriceType.Market, position.StopOrderPrice, ManualPositionSupport.SecondToClose, true);
                        }

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

                        if (position.ProfitIsMarket == false
                            || StartProgram == StartProgram.IsTester
                            || StartProgram == StartProgram.IsOsOptimizer)
                        {
                            CloseDeal(position, OrderPriceType.Limit, position.ProfitOrderPrice, ManualPositionSupport.SecondToClose, true);
                        }
                        else
                        {
                            CloseDeal(position, OrderPriceType.Market, position.ProfitOrderPrice, ManualPositionSupport.SecondToClose, true);
                        }

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

                        if (position.ProfitIsMarket == false
                           || StartProgram == StartProgram.IsTester
                           || StartProgram == StartProgram.IsOsOptimizer)
                        {
                            CloseDeal(position, OrderPriceType.Limit, position.ProfitOrderPrice, ManualPositionSupport.SecondToClose, true);
                        }
                        else
                        {
                            CloseDeal(position, OrderPriceType.Market, position.ProfitOrderPrice, ManualPositionSupport.SecondToClose, true);
                        }

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
        /// alert check
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

                if (AlertSignalEvent != null)
                {
                    AlertSignalEvent();
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
        /// Get journal
        /// </summary>
        public Journal.Journal GetJournal()
        {
            return _journal;
        }

        /// <summary>
        /// Add a new alert to the system
        /// </summary>
        public void SetNewAlert(IIAlert alert)
        {
            _alerts.SetNewAlert(alert);
        }

        /// <summary>
        /// Remove alert from system
        /// </summary>
        public void DeleteAlert(IIAlert alert)
        {
            _alerts.Delete(alert);
        }

        /// <summary>
        /// Remove all alerts from the system
        /// </summary>
        public void DeleteAllAlerts()
        {
            _alerts.Delete();
        }

        // closing a deal if at closing we took more volume than necessary

        /// <summary>
        /// time to close the deal
        /// </summary>
        private DateTime _lastClosingSurplusTime;

        /// <summary>
        /// check whether it is not necessary to close the transactions for which the search was at the close
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

        // Stop Limit`s

        /// <summary>
        /// Stop opening waiting for its price
        /// </summary>
        public List<PositionOpenerToStopLimit> PositionOpenerToStop;

        /// <summary>
        /// Cancel orders with expired lifetime
        /// </summary>
        /// <param name="candles">candles</param>
        private void CancelStopOpenerByNewCandle(List<Candle> candles)
        {
            bool neadSave = false;

            for (int i = 0; PositionOpenerToStop != null && i < PositionOpenerToStop.Count; i++)
            {
                if (PositionOpenerToStop[i].LifeTimeType == PositionOpenerToStopLifeTimeType.NoLifeTime)
                {
                    continue;
                }

                if (PositionOpenerToStop[i].ExpiresBars <= 1)
                {
                    PositionOpenerToStop.RemoveAt(i);
                    i--;
                    neadSave = true;
                    continue;
                }

                if (candles[candles.Count - 1].TimeStart > PositionOpenerToStop[i].LastCandleTime)
                {
                    PositionOpenerToStop[i].LastCandleTime = candles[candles.Count - 1].TimeStart;
                    PositionOpenerToStop[i].ExpiresBars = PositionOpenerToStop[i].ExpiresBars - 1;
                    neadSave = true;
                }
            }

            if (neadSave == true)
            {
                UpdateStopLimits();
            }
        }

        /// <summary>
        /// Check whether it is time to open positions on stop openings
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
                bool neadSave = false;

                for (int i = 0;
                    i > -1 && PositionOpenerToStop != null && PositionOpenerToStop.Count != 0 && i < PositionOpenerToStop.Count;
                    i++)
                {
                    if ((PositionOpenerToStop[i].ActivateType == StopActivateType.HigherOrEqual &&
                         price >= PositionOpenerToStop[i].PriceRedLine)
                        ||
                        (PositionOpenerToStop[i].ActivateType == StopActivateType.LowerOrEqyal &&
                         price <= PositionOpenerToStop[i].PriceRedLine))
                    {
                        if (PositionOpenerToStop[i].Side == Side.Buy)
                        {
                            PositionOpenerToStopLimit opener = PositionOpenerToStop[i];
                            Position pos = LongCreate(PositionOpenerToStop[i].PriceOrder, 
                                PositionOpenerToStop[i].Volume, PositionOpenerToStop[i].OrderPriceType,
                                ManualPositionSupport.SecondToOpen, true);

                            if (pos != null
                                && !string.IsNullOrEmpty(opener.SignalType))
                            {
                                pos.SignalTypeOpen = opener.SignalType;
                            }

                            if (PositionOpenerToStop.Count == 0)
                            { // the user can remove himself from the layer when he sees that the deal is opening
                                return;
                            }

                            PositionOpenerToStop.RemoveAt(i);
                            i = -1;
                            if (PositionBuyAtStopActivateEvent != null && pos != null)
                            {
                                PositionBuyAtStopActivateEvent(pos);
                            }
                            neadSave = true;
                            continue;
                        }
                        else if (PositionOpenerToStop[i].Side == Side.Sell)
                        {
                            PositionOpenerToStopLimit opener = PositionOpenerToStop[i];
                            Position pos = ShortCreate(PositionOpenerToStop[i].PriceOrder, 
                                PositionOpenerToStop[i].Volume, PositionOpenerToStop[i].OrderPriceType,
                                ManualPositionSupport.SecondToOpen, true);

                            if (pos != null
                                && !string.IsNullOrEmpty(opener.SignalType))
                            {
                                pos.SignalTypeOpen = opener.SignalType;
                            }

                            if (PositionOpenerToStop.Count == 0)
                            { // the user can remove himself from the layer when he sees that the deal is opening
                                return;
                            }

                            PositionOpenerToStop.RemoveAt(i);
                            i = -1;

                            if (PositionSellAtStopActivateEvent != null && pos != null)
                            {
                                PositionSellAtStopActivateEvent(pos);
                            }
                            neadSave = true;
                            continue;
                        }
                        i--;
                    }
                }

                if (neadSave == true)
                {
                    UpdateStopLimits();
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void UpdateStopLimits()
        {
            if (StartProgram != StartProgram.IsOsOptimizer)
            {
                _chartMaster?.SetStopLimits(PositionOpenerToStop);
            }

            if (StartProgram == StartProgram.IsOsTrader)
            {
                _journal?.SetStopLimits(PositionOpenerToStop);
            }
        }

        // icebergs control

        /// <summary>
        /// Icebergs master
        /// </summary>
        private AcebergMaker _acebergMaker;

        /// <summary>
        /// Iceberg Master Requests To Cancel Order
        /// </summary>
        void _acebergMaker_NewOrderNeadToCansel(Order order)
        {
            _connector.OrderCancel(order);
        }

        /// <summary>
        /// Icebergs master requires you to place an order
        /// </summary>
        void _acebergMaker_NewOrderNeadToExecute(Order order)
        {
            _connector.OrderExecute(order);
        }

        /// <summary>
        /// Clear all icebergs from the system
        /// </summary>
        public void ClearAceberg()
        {
            try
            {
                _acebergMaker?.ClearAcebergs();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // incoming data processing

        /// <summary>
        /// On the stock market has changed the state of the portfolio
        /// </summary>
        private void _connector_PortfolioOnExchangeChangedEvent(Portfolio portfolio)
        {
            if(PortfolioOnExchangeChangedEvent != null)
            {
                PortfolioOnExchangeChangedEvent(portfolio);
            }
        }

        /// <summary>
        /// New MarketDepth event handler
        /// </summary>
        void _connector_GlassChangeEvent(MarketDepth marketDepth)
        {
            if (_isDelete)
            {
                return;
            }
            MarketDepth = marketDepth;

            if (_marketDepthPainter != null)
            {
                _marketDepthPainter.ProcessMarketDepth(marketDepth);
            }

            if (MarketDepthUpdateEvent != null)
            {
                MarketDepthUpdateEvent(marketDepth);
            }

            if (StartProgram != StartProgram.IsOsTrader)
            {
                if ((marketDepth.Asks == null || marketDepth.Asks.Count == 0)
                    &&
                   (marketDepth.Bids == null || marketDepth.Bids.Count == 0))
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

                        if(marketDepth.Asks != null && marketDepth.Asks.Count > 0)
                        {
                            CheckStop(openPositions[i], marketDepth.Asks[0].Price);
                        }

                        if (openPositions.Count <= i)
                        {
                            continue;
                        }

                        if(marketDepth.Bids != null && marketDepth.Bids.Count > 0)
                        {
                            CheckStop(openPositions[i], marketDepth.Bids[0].Price);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// It's time to close the order for this deal
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
        /// Position status has changed
        /// </summary>
        private void _journal_PositionStateChangeEvent(Position position)
        {
            try
            {
                if (position.State == PositionStateType.Done)
                {
                    CloseAllOrderToPosition(position);

                    if (StartProgram == StartProgram.IsOsTrader)
                    {
                        // высылаем оповещение, только если уже есть закрывающие MyTrades

                        if (position.CloseOrders[position.CloseOrders.Count - 1].MyTrades != null
                            && position.CloseOrders[position.CloseOrders.Count - 1].MyTrades.Count > 0)
                        {
                            SetNewLogMessage(
                                OsLocalization.Trader.Label408 
                                + ", " + OsLocalization.Trader.Label409 + ": " + NameStrategy + "\n" 
                                + position.PositionSpecification, LogMessageType.Trade);

                            if (PositionClosingSuccesEvent != null)
                            {
                                PositionClosingSuccesEvent(position);
                            }
                        }
                        else
                        {// иначе, высылаем в очередь ожидания MyTrades

                            PositionAwaitMyTradesToSendEvent awaitPos = new PositionAwaitMyTradesToSendEvent();
                            awaitPos.Position = position;
                            awaitPos.TimeForcibleRemoval = DateTime.Now.AddSeconds(3);
                            awaitPos.StateAwaitToSend = PositionStateType.Done;
                            _positionsAwaitSendInEventsQueue.Enqueue(awaitPos);
                        }
                    }
                    else
                    {
                        if (PositionClosingSuccesEvent != null)
                        {
                            PositionClosingSuccesEvent(position);
                        }

                        decimal profit = position.ProfitPortfolioPunkt;

                        if (_connector.ServerType == ServerType.Tester)
                        {
                            SetNewLogMessage(
                            OsLocalization.Trader.Label408
                            + ", " + OsLocalization.Trader.Label409 + ": " + NameStrategy + "\n"
                            + position.PositionSpecification, LogMessageType.Trade);

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
                    if (StartProgram != StartProgram.IsOsOptimizer)
                    {
                        SetNewLogMessage(
                        OsLocalization.Trader.Label72
                        + ", " + OsLocalization.Trader.Label409 + ": " + NameStrategy + "\n"
                        + position.PositionSpecification, LogMessageType.Trade);
                    }

                    if (PositionOpeningFailEvent != null)
                    {
                        PositionOpeningFailEvent(position);
                    }
                }
                else if (position.State == PositionStateType.Open)
                {
                    if(StartProgram == StartProgram.IsOsTrader)
                    {
                        // высылаем оповещение, только если уже есть закрывающие MyTrades

                        if (position.OpenOrders[position.OpenOrders.Count - 1].MyTrades != null
                            && position.OpenOrders[position.OpenOrders.Count - 1].MyTrades.Count > 0)
                        {
                            SetNewLogMessage(
                            OsLocalization.Trader.Label407
                            + ", " + OsLocalization.Trader.Label409 + ": " + NameStrategy + "\n"
                            + position.PositionSpecification, LogMessageType.Trade);

                            if (PositionOpeningSuccesEvent != null)
                            {
                                PositionOpeningSuccesEvent(position);
                            }
                            ManualReloadStopsAndProfitToPosition(position);
                        }
                        else
                        {// иначе, высылаем в очередь ожидания MyTrades

                            PositionAwaitMyTradesToSendEvent awaitPos = new PositionAwaitMyTradesToSendEvent();
                            awaitPos.Position = position;
                            awaitPos.TimeForcibleRemoval = DateTime.Now.AddSeconds(3);
                            awaitPos.StateAwaitToSend = PositionStateType.Open;
                            _positionsAwaitSendInEventsQueue.Enqueue(awaitPos);
                        }
                    }
                    else
                    {
                        if(StartProgram == StartProgram.IsTester)
                        {
                            SetNewLogMessage(
                            OsLocalization.Trader.Label407
                            + ", " + OsLocalization.Trader.Label409 + ": " + NameStrategy + "\n"
                            + position.PositionSpecification, LogMessageType.Trade);
                        }

                        if (PositionOpeningSuccesEvent != null)
                        {
                            PositionOpeningSuccesEvent(position);
                        }
                        ManualReloadStopsAndProfitToPosition(position);
                    }
                }
                else if (position.State == PositionStateType.ClosingFail)
                {
                    if (StartProgram != StartProgram.IsOsOptimizer)
                    {
                        SetNewLogMessage(
                        OsLocalization.Trader.Label74
                        + ", " + OsLocalization.Trader.Label409 + ": " + NameStrategy + "\n"
                        + position.PositionSpecification, LogMessageType.Trade);
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
        /// Open position volume changed
        /// </summary>
        private void _journal_PositionNetVolumeChangeEvent(Position position)
        {
            if (PositionNetVolumeChangeEvent != null)
            {
                PositionNetVolumeChangeEvent(position);
            }
        }

        /// <summary>
        /// candle is finished
        /// </summary>
        /// <param name="candles">candles</param>
        private void LogicToEndCandle(List<Candle> candles)
        {
            try
            {
                if (_isDelete)
                {
                    return;
                }
                if (candles == null)
                {
                    return;
                }
                AlertControlPosition();

                if (PositionOpenerToStop != null &&
                    PositionOpenerToStop.Count != 0)
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

        /// <summary>
        /// Time of the last update of the candle
        /// </summary>
        public DateTime LastTimeCandleUpdate { get; set; }

        /// <summary>
        /// candle is update
        /// </summary>
        private void LogicToUpdateLastCandle(List<Candle> candles)
        {
            try
            {
                if (_isDelete)
                {
                    return;
                }
                LastTimeCandleUpdate = Connector.MarketTime;

                AlertControlPosition();

                while (_chartMaster == null)
                {
                    Task delay = new Task(() =>
                    {
                        Thread.Sleep(100);
                    });

                    delay.Start();
                    delay.Wait();
                }

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
        /// user ordered a position change
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
        /// has the session started today?
        /// </summary>
        private bool _firstTickToDaySend;

        private DateTime _lastTradeTime;

        private int _lastTradeIndex;

        /// <summary>
        /// new tiki came
        /// </summary>
        private void _connector_TickChangeEvent(List<Trade> trades)
        {
            if (_isDelete)
            {
                return;
            }

            if (trades == null ||
                trades.Count == 0)
            {
                return;
            }

            if (_chartMaster == null)
            {
                return;
            }

            if ((StartProgram == StartProgram.IsOsOptimizer
                || StartProgram == StartProgram.IsTester)
                && trades.Count < 10)
            {
                _lastTradeTime = DateTime.MinValue;
                _lastTradeIndex = 0;
            }

            if(StartProgram == StartProgram.IsOsTrader)
            {
                if(ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if(_lastTradeTime == DateTime.MinValue &&
                    _lastTradeIndex == 0)
                {
                    _lastTradeIndex = trades.Count;
                    _lastTradeTime = trades[trades.Count - 1].Time;
                    return;
                }
            }
            else if(StartProgram == StartProgram.IsTester)
            {
                if(trades[trades.Count - 1].TimeFrameInTester != Entity.TimeFrame.Sec1 &&
                    trades[trades.Count - 1].TimeFrameInTester != Connector.TimeFrame)
                {
                    return;
                }
            }

            Trade trade = trades[trades.Count - 1];

            if (trade != null && _firstTickToDaySend == false && FirstTickToDayEvent != null)
            {
                if (trade.Time.Hour == 10
                    && (trade.Time.Minute == 1 || trade.Time.Minute == 0))
                {
                    _firstTickToDaySend = true;
                    FirstTickToDayEvent(trade);
                }
            }

            List<Trade> newTrades = new List<Trade>();

            if (trades.Count > 1000)
            { // if deleting trades from the system is disabled

                int newTradesCount = trades.Count - _lastTradeIndex;

                if (newTradesCount <= 0)
                {
                    return;
                }

                newTrades = trades.GetRange(_lastTradeIndex, newTradesCount);
            }
            else
            {
                if (_lastTradeTime == DateTime.MinValue)
                {
                    newTrades = trades;
                }
                else
                {
                    for (int i = 0; i < trades.Count; i++)
                    {
                        try
                        {
                            if (trades[i].Time <= _lastTradeTime)
                            {
                                continue;
                            }
                            newTrades.Add(trades[i]);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }

            if (newTrades.Count == 0)
            {
                return;
            }

            for (int i2 = 0; i2 < newTrades.Count; i2++)
            {
                if (newTrades[i2] == null)
                {
                    newTrades.RemoveAt(i2);
                    i2--;
                    continue;
                }
            }

            if(_journal == null)
            {
                return;
            }

            if (_isDelete)
            {
                return;
            }

            List<Position> openPositions = _journal.OpenPositions;

            if (openPositions != null)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    if (openPositions[i].StopOrderIsActiv == false &&
                        openPositions[i].ProfitOrderIsActiv == false)
                    {
                        continue;
                    }

                    for (int i2 = 0; i < openPositions.Count && i2 < newTrades.Count; i2++)
                    {
                        if (CheckStop(openPositions[i], newTrades[i2].Price))
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

            if (PositionOpenerToStop != null &&
                PositionOpenerToStop.Count != 0)
            {
                for (int i2 = 0; i2 < newTrades.Count; i2++)
                {
                    CheckStopOpener(newTrades[i2].Price);
                }
            }
            if (NewTickEvent != null)
            {
                for (int i2 = 0; i2 < newTrades.Count; i2++)
                {
                    try
                    {
                        NewTickEvent(newTrades[i2]);
                    }
                    catch (Exception error)
                    {
                        SetNewLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }

            _lastTradeIndex = trades.Count;
            _lastTradeTime = newTrades[newTrades.Count - 1].Time;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                CheckSurplusPositions();
            }
        }

        /// <summary>
        /// Incoming my deal
        /// </summary>
        private void _connector_MyTradeEvent(MyTrade trade)
        {
            if (_isDelete)
            {
                return;
            }
            _journal.SetNewMyTrade(trade);

            if (MyTradeEvent != null)
            {
                MyTradeEvent(trade);
            }
        }

        /// <summary>
        /// Security for connector defined
        /// </summary>
        private void _connector_SecuritySubscribeEvent(Security security)
        {
            if (SecuritySubscribeEvent != null)
            {
                SecuritySubscribeEvent(security);
            }
        }

        /// <summary>
        /// Server time has changed
        /// </summary>
        void StrategOneSecurity_TimeServerChangeEvent(DateTime time)
        {
            if (_isDelete)
            {
                return;
            }
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
        /// Incoming orders
        /// </summary>
        private void _connector_OrderChangeEvent(Order order)
        {
            if(_isDelete)
            {
                return;
            }
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
        /// Incoming new bid with ask
        /// </summary>
        private void _connector_BestBidAskChangeEvent(decimal bestBid, decimal bestAsk)
        {
            if (_isDelete)
            {
                return;
            }
            _journal?.SetNewBidAsk(bestBid, bestAsk);
            _marketDepthPainter?.ProcessBidAsk(bestBid, bestAsk);
            BestBidAskChangeEvent?.Invoke(bestBid, bestAsk);
        }

        /// <summary>
        /// Indicator parameters changed
        /// </summary>
        private void _chartMaster_IndicatorUpdateEvent()
        {
            if (IndicatorUpdateEvent != null)
            {
                IndicatorUpdateEvent();
            }
        }

        // Sending events about changing position statuses, with MyTrades waiting

        private static void PositionsSenderThreadArea()
        {
            Thread.Sleep(5000);

            while (true)
            {
                try
                {
                    Thread.Sleep(10);

                    if(MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    for (int i = 0; i < _tabsToCheckPositionEvent.Count; i++)
                    {
                        if (_tabsToCheckPositionEvent[i] == null)
                        {
                            continue;
                        }

                        _tabsToCheckPositionEvent[i].CheckAwaitPositionsArea();
                    }
                }
                catch
                {
                   // ignore
                }
            }
        }

        private static bool _senderThreadIsStarted = false;

        private static List<BotTabSimple> _tabsToCheckPositionEvent = new List<BotTabSimple>();

        ConcurrentQueue<PositionAwaitMyTradesToSendEvent> _positionsAwaitSendInEventsQueue 
            = new ConcurrentQueue<PositionAwaitMyTradesToSendEvent> ();

        List<PositionAwaitMyTradesToSendEvent> _positionsAwaitSendInEventsList 
            = new List<PositionAwaitMyTradesToSendEvent>();

        public void CheckAwaitPositionsArea()
        {
            try
            {
                // 1 разбираем очередь
                while (_positionsAwaitSendInEventsQueue.IsEmpty == false)
                {
                    PositionAwaitMyTradesToSendEvent newPos = null;

                    if (_positionsAwaitSendInEventsQueue.TryDequeue(out newPos))
                    {
                        _positionsAwaitSendInEventsList.Add(newPos);
                    }
                }

                // 2 проверяем, не пора ли что-то выслать наверх

                for (int i = 0; i < _positionsAwaitSendInEventsList.Count; i++)
                {
                    PositionAwaitMyTradesToSendEvent curPos = _positionsAwaitSendInEventsList[i];

                    if (curPos.StateAwaitToSend == PositionStateType.Open)
                    {
                        if (curPos.TimeForcibleRemoval < DateTime.Now
                           ||
                           (curPos.Position.OpenOrders[curPos.Position.OpenOrders.Count - 1].MyTrades != null
                                && curPos.Position.OpenOrders[curPos.Position.OpenOrders.Count - 1].MyTrades.Count > 0))
                        {
                            try
                            {
                                ManualReloadStopsAndProfitToPosition(curPos.Position);

                                SetNewLogMessage(
                                OsLocalization.Trader.Label407
                                + ", " + OsLocalization.Trader.Label409 + ": " + NameStrategy + "\n"
                                + curPos.Position.PositionSpecification, LogMessageType.Trade);

                                if (PositionOpeningSuccesEvent != null)
                                {
                                    PositionOpeningSuccesEvent(curPos.Position);
                                }
                            }
                            catch (Exception ex)
                            {
                                SetNewLogMessage(ex.ToString(), LogMessageType.Error);
                            }

                            _positionsAwaitSendInEventsList.RemoveAt(i);
                            i--;
                            continue;
                        }
                    }
                    else if (curPos.StateAwaitToSend == PositionStateType.Done)
                    {
                        if (curPos.TimeForcibleRemoval < DateTime.Now
                           ||
                           (curPos.Position.CloseOrders[curPos.Position.CloseOrders.Count - 1].MyTrades != null
                                && curPos.Position.CloseOrders[curPos.Position.CloseOrders.Count - 1].MyTrades.Count > 0))
                        {
                            try
                            {
                                SetNewLogMessage(
                                OsLocalization.Trader.Label408
                                +", " + OsLocalization.Trader.Label409 + ": " + NameStrategy + "\n"
                                + curPos.Position.PositionSpecification, LogMessageType.Trade);

                                if (PositionClosingSuccesEvent != null)
                                {
                                    PositionClosingSuccesEvent(curPos.Position);
                                }
                            }
                            catch (Exception ex)
                            {
                                SetNewLogMessage(ex.ToString(), LogMessageType.Error);
                            }
                            _positionsAwaitSendInEventsList.RemoveAt(i);
                            i--;
                            continue;
                        }
                    }
                }
            }
            catch(Exception error)
            {
                SetNewLogMessage(error.ToString(),LogMessageType.Error);
            }
        }

        // Outgoing events. Handlers for strategy

        /// <summary>
        /// My new trade event
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// The morning session started. Send the first trades
        /// </summary>
        public event Action<Trade> FirstTickToDayEvent;

        /// <summary>
        /// New trades
        /// </summary>
        public event Action<Trade> NewTickEvent;

        /// <summary>
        /// New server time
        /// </summary>
        public event Action<DateTime> ServerTimeChangeEvent;

        /// <summary>
        /// Last candle finished
        /// </summary>
        public event Action<List<Candle>> CandleFinishedEvent;

        /// <summary>
        /// Last candle update
        /// </summary>
        public event Action<List<Candle>> CandleUpdateEvent;

        /// <summary>
        /// New marketDepth
        /// </summary>
        public event Action<MarketDepth> MarketDepthUpdateEvent;

        /// <summary>
        /// Bid ask change
        /// </summary>
        public event Action<decimal, decimal> BestBidAskChangeEvent;

        /// <summary>
        /// Position successfully closed
        /// </summary>
        public event Action<Position> PositionClosingSuccesEvent;

        /// <summary>
        /// Position successfully opened
        /// </summary>
        public event Action<Position> PositionOpeningSuccesEvent;

        /// <summary>
        /// Open position volume has changed
        /// </summary>
        public event Action<Position> PositionNetVolumeChangeEvent;

        /// <summary>
        /// Opening position failed
        /// </summary>
        public event Action<Position> PositionOpeningFailEvent;

        /// <summary>
        /// Position closing failed
        /// </summary>
        public event Action<Position> PositionClosingFailEvent;

        /// <summary>
        /// A stop order is activated for the position
        /// </summary>
        public event Action<Position> PositionStopActivateEvent;

        /// <summary>
        /// A profit order is activated for the position
        /// </summary>
        public event Action<Position> PositionProfitActivateEvent;

        /// <summary>
        /// Stop order buy activated
        /// </summary>
        public event Action<Position> PositionBuyAtStopActivateEvent;

        /// <summary>
        /// Stop order sell activated
        /// </summary>
        public event Action<Position> PositionSellAtStopActivateEvent;

        /// <summary>
        /// Portfolio on exchange changed
        /// </summary>
        public event Action<Portfolio> PortfolioOnExchangeChangedEvent;

        /// <summary>
        /// The robot is removed from the system
        /// </summary>
        public event Action<int> DeleteBotEvent;

        /// <summary>
        /// Updated order
        /// </summary>
        public event Action<Order> OrderUpdateEvent;

        /// <summary>
        /// Indicator parameters changed
        /// </summary>
        public event Action IndicatorUpdateEvent;

        /// <summary>
        /// Security for connector defined
        /// </summary>
        public event Action<Security> SecuritySubscribeEvent;

        /// <summary>
        /// Source removed
        /// </summary>
        public event Action TabDeletedEvent;
    }

    /// <summary>
    /// Re-sends the position to the top
    /// </summary>
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

    public class PositionAwaitMyTradesToSendEvent
    {
        public Position Position;

        public DateTime TimeForcibleRemoval;

        public PositionStateType StateAwaitToSend;
    }
}