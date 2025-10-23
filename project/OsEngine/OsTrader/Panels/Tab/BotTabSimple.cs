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
using OsEngine.OsTrader.Grids;

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
                _connector.CancelOrderFailEvent += _connector_CancelOrderFailEvent;
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
                _connector.DialogClosed += _connector_DialogClosed;
                _connector.FundingChangedEvent += _connector_FundingChangedEvent;
                _connector.NewVolume24hChangedEvent += _connector_NewVolume24hChangedEvent;

                if (startProgram != StartProgram.IsOsOptimizer)
                {
                    _marketDepthPainter = new MarketDepthPainter(TabName, _connector);
                    _marketDepthPainter.LogMessageEvent += SetNewLogMessage;
                }

                _journal = new Journal.Journal(TabName, startProgram);

                _journal.PositionStateChangeEvent += _journal_PositionStateChangeEvent;
                _journal.PositionNetVolumeChangeEvent += _journal_PositionNetVolumeChangeEvent;
                _journal.UserSelectActionEvent += _journal_UserSelectActionEvent;
                _journal.LogMessageEvent += SetNewLogMessage;

                _connector.CommissionType = _journal.CommissionType;
                _connector.CommissionValue = _journal.CommissionValue;

                _chartMaster = new ChartCandleMaster(TabName, StartProgram);
                _chartMaster.LogMessageEvent += SetNewLogMessage;
                _chartMaster.SetNewSecurity(_connector.SecurityName, _connector.TimeFrameBuilder, _connector.PortfolioName, _connector.ServerFullName);
                _chartMaster.SetPosition(_journal.AllPosition);
                _chartMaster.IndicatorUpdateEvent += _chartMaster_IndicatorUpdateEvent;
                _chartMaster.IndicatorManuallyCreateEvent += _chartMaster_IndicatorManuallyCreateEvent;
                _chartMaster.IndicatorManuallyDeleteEvent += _chartMaster_IndicatorManuallyDeleteEvent;

                if (StartProgram != StartProgram.IsOsOptimizer)
                {
                    _alerts = new AlertMaster(TabName, _connector, _chartMaster);
                    _alerts.LogMessageEvent += SetNewLogMessage;
                }
                _dealCreator = new PositionCreator();

                ManualPositionSupport = new BotManualControl(TabName, this, startProgram);
                ManualPositionSupport.LogMessageEvent += SetNewLogMessage;
                ManualPositionSupport.DontOpenOrderDetectedEvent += _dealOpeningWatcher_DontOpenOrderDetectedEvent;

                GridsMaster = new TradeGridsMaster(startProgram, name, this);
                GridsMaster.LogMessageEvent += SetNewLogMessage;

                _icebergMaker = new IcebergMaker();
                _icebergMaker.NewOrderNeedToExecute += _icebergMaker_NewOrderNeedToExecute;
                _icebergMaker.NewOrderNeedToCancel += _icebergMaker_NewOrderNeedToCancel;

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

                    if (_senderThreadIsStarted == false)
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
        /// <param name="portfolioName">portfolio name</param>
        /// <param name="serverType">server type</param>
        void _connector_ConnectorStartedReconnectEvent(string securityName, TimeFrame timeFrame, TimeSpan timeFrameSpan, string portfolioName, string serverType)
        {
            _lastTradeTime = DateTime.MinValue;
            _lastTradeIndex = 0;
            _lastTradeIdInTester = 0;

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
                     WindowsFormsHost hostCloseDeals, Rectangle rectangleChart, WindowsFormsHost hostAlerts, TextBox textBoxLimitPrice, 
                     Grid gridChartControlPanel, TextBox textBoxVolume, WindowsFormsHost hostGrids)
        {
            try
            {
                if (Security != null
                    && Portfolio != null)
                {
                    _chartMaster?.SetNewSecurity(Security.Name, _connector.TimeFrameBuilder, Portfolio.Number, Connector.ServerFullName);
                }

                _chartMaster?.StartPaint(gridChart, hostChart, rectangleChart);
                _marketDepthPainter?.StartPaint(hostGlass, textBoxLimitPrice, textBoxVolume);
                _journal?.StartPaint(hostOpenDeals, hostCloseDeals);
                _alerts?.StartPaint(hostAlerts);
                GridsMaster?.StartPaint(hostGrids);

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
                GridsMaster?.StopPaint();
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

        public int SelectedControlTab = 0;

        /// <summary>
        /// are events sent to the top from the tab?
        /// </summary>
        public bool EventsIsOn
        {
            get
            {
                if (Connector == null)
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

                if (Connector.EventsIsOn == value)
                {
                    return;
                }

                Connector.EventsIsOn = value;

                if(value == false)
                {
                    _chartMaster.EventIsOn = value;
                }
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
                ClearIceberg();

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

                if (GridsMaster != null)
                {
                    GridsMaster.Clear();
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
                    _connector.CancelOrderFailEvent -= _connector_CancelOrderFailEvent;
                    _connector.MyTradeEvent -= _connector_MyTradeEvent;
                    _connector.BestBidAskChangeEvent -= _connector_BestBidAskChangeEvent;
                    _connector.GlassChangeEvent -= _connector_GlassChangeEvent;
                    _connector.TimeChangeEvent -= StrategOneSecurity_TimeServerChangeEvent;
                    _connector.NewCandlesChangeEvent -= LogicToEndCandle;
                    _connector.LastCandlesChangeEvent -= LogicToUpdateLastCandle;
                    _connector.TickChangeEvent -= _connector_TickChangeEvent;
                    _connector.ConnectorStartedReconnectEvent -= _connector_ConnectorStartedReconnectEvent;
                    _connector.PortfolioOnExchangeChangedEvent -= _connector_PortfolioOnExchangeChangedEvent;
                    _connector.SecuritySubscribeEvent -= _connector_SecuritySubscribeEvent;
                    _connector.DialogClosed -= _connector_DialogClosed;
                    _connector.FundingChangedEvent -= _connector_FundingChangedEvent;
                    _connector.NewVolume24hChangedEvent -= _connector_NewVolume24hChangedEvent;

                    _connector.Delete();
                    _connector.LogMessageEvent -= SetNewLogMessage;

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

                if (_icebergMaker != null)
                {
                    _icebergMaker.NewOrderNeedToExecute -= _icebergMaker_NewOrderNeedToExecute;
                    _icebergMaker.NewOrderNeedToCancel -= _icebergMaker_NewOrderNeedToCancel;
                    _icebergMaker = null;
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
                    _chartMaster.IndicatorManuallyCreateEvent -= _chartMaster_IndicatorManuallyCreateEvent;
                    _chartMaster.IndicatorManuallyDeleteEvent -= _chartMaster_IndicatorManuallyDeleteEvent;
                    _chartMaster.Delete();
                    _chartMaster.LogMessageEvent -= SetNewLogMessage;
                    _chartMaster = null;
                }

                if (GridsMaster != null)
                {
                    GridsMaster.Delete();
                    GridsMaster.LogMessageEvent -= SetNewLogMessage;
                    GridsMaster = null;
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

                if (TabDeletedEvent != null)
                {
                    TabDeletedEvent();
                }

                if (StartProgram == StartProgram.IsOsTrader)
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

            if (_chartMaster == null)
            {
                return null;
            }

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
        /// Creates a new indicator of the specified type, configures its parameters and adds it to the chart in the specified area.<br/>
        /// The method combines creation via a factory, setting parameters and binding to the chart area.
        /// </summary>
        /// <param name="bot">Current bot</param>
        /// <param name="typeName">Indicator type (e.g. "Sma", "ATR"). Must match the indicator class name.</param>
        /// <param name="area">The name of the area on which it will be placed. Default: "Prime" </param>
        /// <param name="canDelete">Determines whether the user can remove the indicator from the chart.</param>
        /// <param name="parameters">Array of indicator parameter values. The order should match the expected parameters.</param>
        public Aindicator CreateIndicator(BotPanel bot, string typeName, string area, bool canDelete, params decimal[] parameters)
        {
            Aindicator indicator = IndicatorsFactory.CreateIndicatorByName(typeName, $"{bot.NameStrategyUniq}{typeName}", canDelete);
            indicator = (Aindicator)CreateCandleIndicator(indicator, area);

            int parametersDigitCount = indicator.ParametersDigit.Count;
            var parameterDigits = indicator.ParametersDigit;

            if (parametersDigitCount != parameters.Length)
                MessageBox.Show($"Count of parameters ({parameters.Length}) must be equal to the count of indicator parameters ({parametersDigitCount})");
            
            for (int i = 0; i < parametersDigitCount; i++)
                parameterDigits[i].Value = parameters[i];

            return indicator;
        }

        /// <summary>
        /// Creates a new indicator of the specified type, configures its parameters and adds it to the chart in the specified area.<br/>
        /// The method combines creation via a factory, setting parameters and binding to the chart area.<br/><br/>
        /// Can't be deleted from the chart.
        /// </summary>
        /// <param name="bot">Current bot</param>
        /// <param name="typeName">Indicator type (e.g. "Sma", "ATR"). Must match the indicator class name.</param>
        /// <param name="area">The name of the area on which it will be placed. Default: "Prime" </param>
        /// <param name="parameters">Array of indicator parameter values. The order should match the expected parameters.</param>
        public Aindicator CreateIndicator(BotPanel bot, string typeName, string area, params decimal[] parameters)
        {
            return CreateIndicator(bot, typeName, area, false, parameters);
        }

        /// <summary>
        /// Creates a new indicator of the specified type in the Prime area, configures its parameters and adds it to the chart in the specified area.<br/>
        /// The method combines creation via a factory, setting parameters and binding to the chart area.<br/><br/>
        /// Can't be deleted from the chart.
        /// </summary>
        /// <param name="bot">Current bot</param>
        /// <param name="typeName">Indicator type (e.g. "Sma", "ATR"). Must match the indicator class name.</param>
        /// <param name="area">The name of the area on which it will be placed. Default: "Prime" </param>
        /// <param name="parameters">Array of indicator parameter values. The order should match the expected parameters.</param>
        public Aindicator CreateIndicator(BotPanel bot, string typeName, params decimal[] parameters)
        {
            return CreateIndicator(bot, typeName, "Prime", parameters);
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
        /// Automatic trading grids
        /// </summary>
        public TradeGridsMaster GridsMaster;

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
        public Security Security
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
        /// Security to trading
        /// </summary>
        [Obsolete("Obsolete. Use Security")]
        public Security Securiti
        {
            get
            {
                return Security;
            }

            set { Security = value; }
        }

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
                else if (StartProgram == StartProgram.IsOsTrader)
                {
                    _portfolio = _connector.Portfolio;
                }

                return _portfolio;
            }
            set { _portfolio = value; }
        }
        private Portfolio _portfolio;

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
                        || Security == null)
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

                        if (positionsOnBoard[i].ValueCurrent == 0)
                        {
                            continue;
                        }

                        string nameShort = Security.Name + "_SHORT";
                        string nameLong = Security.Name + "_LONG";
                        string nameBoth = Security.Name + "_BOTH";

                        if (positionsOnBoard[i].SecurityNameCode.Equals(Security.Name)
                            || positionsOnBoard[i].SecurityNameCode.Equals(nameShort)
                            || positionsOnBoard[i].SecurityNameCode.Equals(nameLong)
                            || positionsOnBoard[i].SecurityNameCode.Equals(nameBoth))
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
        /// Commission type for positions
        /// </summary>
        public CommissionType CommissionType
        {
            get
            {
                if (_journal == null)
                {
                    return CommissionType.None;
                }
                return _journal.CommissionType;
            }
            set
            {
                _journal.CommissionType = value;
                _connector.CommissionType = value;
            }
        }

        /// <summary>
        /// Commission amount
        /// </summary>
        public decimal CommissionValue
        {
            get
            {
                if (_journal == null)
                {
                    return 0;
                }

                return _journal.CommissionValue;
            }
            set
            {
                _journal.CommissionValue = value;
                _connector.CommissionValue = value;
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
        /// Net position recruited by the robot
        /// </summary>
        public decimal VolumeNet
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
        /// Stop-limit orders
        /// </summary>
        public List<PositionOpenerToStopLimit> PositionOpenerToStopsAll
        {
            get { return PositionOpenerToStop; }
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
        /// лучший Bid в стакане
        /// </summary>
        public decimal PriceBestBid
        {
            get
            {
                if (Connector == null)
                {
                    return 0;
                }
                return (decimal)Connector.BestBid;
            }
        }
        
        /// <summary>
        /// лучший Аск в стакане
        /// </summary>
        public decimal PriceBestAsk
        {
            get
            {
                if (Connector == null)
                {
                    return 0;
                }
                return (decimal)Connector.BestAsk;
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
                return (decimal)(_connector.BestAsk + _connector.BestBid) / 2;
            }
        }

        /// <summary>
        /// Does the server support market orders
        /// </summary>
        public bool ServerIsSupportMarketOrders
        {
            get
            {
                if (_connector == null)
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
        }

        private void _connector_DialogClosed()
        {
            if (DialogClosed != null)
            {
                DialogClosed();
            }

            _journal.CommissionType = _connector.CommissionType;
            _journal.CommissionValue = _connector.CommissionValue;
        }

        public event Action DialogClosed;

        /// <summary>
        /// Show custom settings window
        /// </summary>
        public void ShowManualControlDialog()
        {
            bool stopIsOnStartValue = ManualPositionSupport.StopIsOn;
            decimal stopDistance = ManualPositionSupport.StopDistance;
            decimal stopSlippage = ManualPositionSupport.StopSlippage;

            bool profitOnStartValue = ManualPositionSupport.ProfitIsOn;
            decimal profitDistance = ManualPositionSupport.ProfitDistance;
            decimal profitSlippage = ManualPositionSupport.ProfitSlippage;

            ManualPositionSupport.ShowDialog(StartProgram);

            bool needToReplaceStop = false;

            if (ManualPositionSupport.StopIsOn == true
                && stopIsOnStartValue == false)
            {
                needToReplaceStop = true;
            }
            if (ManualPositionSupport.StopIsOn == true
                && stopDistance != ManualPositionSupport.StopDistance)
            {
                needToReplaceStop = true;
            }
            if (ManualPositionSupport.StopIsOn == true
                && stopSlippage != ManualPositionSupport.StopSlippage)
            {
                needToReplaceStop = true;
            }

            if (ManualPositionSupport.ProfitIsOn == true
                && profitOnStartValue == false)
            {
                needToReplaceStop = true;
            }
            if (ManualPositionSupport.ProfitIsOn == true
                && profitDistance != ManualPositionSupport.ProfitDistance)
            {
                needToReplaceStop = true;
            }
            if (ManualPositionSupport.ProfitIsOn == true
                && profitSlippage != ManualPositionSupport.ProfitSlippage)
            {
                needToReplaceStop = true;
            }

            List<Position> positions = this.PositionsOpenAll;

            if (positions.Count > 0 &&
                needToReplaceStop &&
                IsCreatedByScreener == false)
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label386);

                ui.ShowDialog();

                if (ui.UserAcceptAction)
                {
                    for (int i = 0; i < positions.Count; i++)
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
        public System.Windows.Forms.ContextMenuStrip GetContextDialog()
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
                decimal price = (decimal)_connector.BestAsk;

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label290, LogMessageType.System);
                    return null;
                }

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    if (!Connector.EmulatorIsOn)
                    {
                        price = price + Security.PriceStep * 40;
                    }
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
        /// <param name="priceLimit">order price</param>
        /// <param name="signalType">>open position signal name. Will be written to position property: SignalTypeOpen</param>
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
        /// <param name="volume">volume</param>
        /// <param name="price">order price</param>
        /// <param name="ordersCount">iceberg orders count</param>
        public Position BuyAtIceberg(decimal volume, decimal price, int ordersCount)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return null;
                }

                if (StartProgram != StartProgram.IsOsTrader || ordersCount <= 1)
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

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                if (Security != null)
                {
                    if (Convert.ToDouble(Security.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    {
                        int point = Convert.ToDouble(Security.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                        while (price % Security.PriceStep != 0)
                        {
                            price = price - 1;
                        }
                    }
                }
                else
                {
                    decimal lastPrice = (decimal)_connector.BestBid;
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
                newDeal.Lots = Security.Lot;
                newDeal.PriceStepCost = Security.PriceStepCost;
                newDeal.PriceStep = Security.PriceStep;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    newDeal.PortfolioValueOnOpenPosition = Portfolio.ValueCurrent;
                }
                else
                { // Tester, Optimizer, etc
                    newDeal.PortfolioValueOnOpenPosition = Math.Round(Portfolio.ValueCurrent, 2);
                }

                _journal.SetNewDeal(newDeal);

                _icebergMaker.MakeNewIceberg(price, ManualPositionSupport.SecondToOpen,
                    ordersCount, newDeal, IcebergType.Open, volume, this, OrderPriceType.Limit, 0);

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
        /// <param name="ordersCount">iceberg orders count</param>
        /// <param name="signalType">>open position signal nameа. Will be written to position property: SignalTypeOpen</param>
        public Position BuyAtIceberg(decimal volume, decimal price, int ordersCount, string signalType)
        {
            Position position = BuyAtIceberg(volume, price, ordersCount);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// Enter position Long at iceberg with MARKET orders
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="ordersCount">iceberg orders count</param>
        /// <param name="minMillisecondsDistance">minimum time interval between orders in milliseconds</param>
        public Position BuyAtIcebergMarket(decimal volume, int ordersCount, int minMillisecondsDistance)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return null;
                }

                if (StartProgram != StartProgram.IsOsTrader || ordersCount <= 1)
                {
                    return BuyAtMarket(volume);
                }

                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63, LogMessageType.System);
                    return null;
                }

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                Position newDeal = new Position();
                newDeal.Number = NumberGen.GetNumberDeal(StartProgram);
                newDeal.Direction = Side.Buy;
                newDeal.State = PositionStateType.Opening;

                newDeal.NameBot = TabName;
                newDeal.Lots = Security.Lot;
                newDeal.PriceStepCost = Security.PriceStepCost;
                newDeal.PriceStep = Security.PriceStep;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    newDeal.PortfolioValueOnOpenPosition = Portfolio.ValueCurrent;
                }
                else
                { // Tester, Optimizer, etc
                    newDeal.PortfolioValueOnOpenPosition = Math.Round(Portfolio.ValueCurrent, 2);
                }

                _journal.SetNewDeal(newDeal);

                decimal price = (decimal)PriceBestAsk;

                _icebergMaker.MakeNewIceberg(price, ManualPositionSupport.SecondToOpen, ordersCount,
                    newDeal, IcebergType.Open, volume, this, OrderPriceType.Market, minMillisecondsDistance);

                return newDeal;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// Enter position Long at iceberg with MARKET orders
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="ordersCount">iceberg orders count</param>
        /// <param name="minMillisecondsDistance">minimum time interval between orders in milliseconds</param>
        /// <param name="signalType">>open position signal name. Will be written to position property: SignalTypeOpen</param>
        public Position BuyAtIcebergMarket(decimal volume, int ordersCount, int minMillisecondsDistance, string signalType)
        {
            Position position = BuyAtIcebergMarket(volume, ordersCount, minMillisecondsDistance);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// Create a FAKE long position
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="price">order price</param>
        /// <param name="time">order time</param>
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

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }
                price = RoundPrice(price, Security, direction);

                Position newDeal = _dealCreator.CreatePosition(
                    TabName, direction, price, volume, 
                    OrderPriceType.Limit, ManualPositionSupport.SecondToOpen, 
                    Security, Portfolio, StartProgram, 
                    ManualPositionSupport.OrderTypeTime,
                    ManualPositionSupport.LimitsMakerOnly);

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
        /// Create a FAKE long position
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="price">order price</param>
        /// <param name="time">order time</param>
        /// <param name="signalType">open position signal name. Will be written to position property: SignalTypeOpen</param>
        public Position BuyAtFake(decimal volume, decimal price, DateTime time, string signalType)
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

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }
                price = RoundPrice(price, Security, direction);

                Position newDeal = _dealCreator.CreatePosition(
                    TabName, direction, price, volume, OrderPriceType.Limit,
                    ManualPositionSupport.SecondToOpen, Security, Portfolio, 
                    StartProgram, ManualPositionSupport.OrderTypeTime,
                    ManualPositionSupport.LimitsMakerOnly);

                newDeal.SignalTypeOpen = signalType;

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
        /// Enter position Long at price intersection
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candles count</param>
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
                positionOpener.Security = Security.Name;
                positionOpener.Number = NumberGen.GetNumberDeal(StartProgram);
                positionOpener.ExpiresBars = expiresBars;
                positionOpener.TimeCreate = TimeServerCurrent;
                positionOpener.OrderCreateBarNumber = CandlesFinishedOnly.Count;
                positionOpener.TabName = TabName;
                positionOpener.LifeTimeType = lifeTimeType;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    positionOpener.PriceOrder = priceLimit;
                }
                else
                {
                    positionOpener.PriceOrder = priceRedLine;
                }

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
        /// /// <param name="expiresBars">life time in candles count</param>
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
        /// /// <param name="expiresBars">life time in candles count</param>
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
        /// /// <param name="volume">volume</param>
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
                positionOpener.Security = Security.Name;
                positionOpener.Number = NumberGen.GetNumberDeal(StartProgram);
                positionOpener.ExpiresBars = expiresBars;
                positionOpener.TimeCreate = TimeServerCurrent;
                positionOpener.OrderCreateBarNumber = CandlesFinishedOnly.Count;
                positionOpener.TabName = TabName;
                positionOpener.LifeTimeType = lifeTimeType;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    positionOpener.PriceOrder = priceLimit;
                }
                else
                {
                    positionOpener.PriceOrder = priceRedLine;
                }

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
        /// Cancel all buyAtStop orders
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

                LongUpdate(position, priceLimit, volume, ManualPositionSupport.SecondToOpen, false, OrderPriceType.Limit, true);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Add new order to Long position at limit
        /// Active orders already on the exchange will not be withdrawn from the market. 
        /// </summary>
        /// <param name="position">position to which the order will be added</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="volume">volume</param>
        public void BuyAtLimitToPositionUnsafe(Position position, decimal priceLimit, decimal volume)
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

                LongUpdate(position, priceLimit, volume, ManualPositionSupport.SecondToOpen, false, OrderPriceType.Limit, false);
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

                decimal price = (decimal)_connector.BestAsk;

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label290, LogMessageType.System);
                    return;
                }

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    if (!Connector.EmulatorIsOn)
                    {
                        price = price + Security.PriceStep * 40;
                    }
                }

                if (Security != null && Security.PriceStep < 1 && Convert.ToDouble(Security.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                {
                    int countPoint = Convert.ToDouble(Security.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                    price = Math.Round(price, countPoint);
                }
                else if (Security != null && Security.PriceStep >= 1)
                {
                    price = Math.Round(price, 0);
                    while (price % Security.PriceStep != 0)
                    {
                        price = price - 1;
                    }
                }

                if (_connector.MarketOrdersIsSupport)
                {
                    LongUpdate(position, price, volume, ManualPositionSupport.SecondToOpen, false, OrderPriceType.Market, true);
                }
                else
                {
                    LongUpdate(position, price, volume, ManualPositionSupport.SecondToOpen, false, OrderPriceType.Limit, true);
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
        /// <param name="ordersCount">iceberg orders count</param>
        public void BuyAtIcebergToPosition(Position position, decimal price, decimal volume, int ordersCount)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

                if (StartProgram != StartProgram.IsOsTrader || ordersCount <= 1)
                {
                    if (position.Direction == Side.Sell)
                    {
                        ClosePeaceOfDeal(position, OrderPriceType.Limit, price, ManualPositionSupport.SecondToClose, volume, true, false);

                        return;
                    }

                    LongUpdate(position, price, volume, ManualPositionSupport.SecondToOpen, false, OrderPriceType.Limit, true);
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

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return;
                }

                if (Security != null)
                {
                    if (Convert.ToDouble(Security.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    {
                        int point = Convert.ToDouble(Security.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                        while (price % Security.PriceStep != 0)
                        {
                            price = price - 1;
                        }
                    }
                }
                else
                {
                    decimal lastPrice = (decimal)_connector.BestBid;
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


                _icebergMaker.MakeNewIceberg(price, ManualPositionSupport.SecondToOpen, ordersCount,
                    position, IcebergType.ModifyBuy, volume, this, OrderPriceType.Limit, 0);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Add new order to Long position at iceberg with MARKET orders
        /// </summary>
        /// <param name="position">position to which the order will be added</param>
        /// <param name="volume">volume</param>
        /// <param name="ordersCount">iceberg orders count</param>
        /// <param name="minMillisecondsDistance">minimum time interval between orders in milliseconds</param>
        public void BuyAtIcebergToPositionMarket(Position position, decimal volume, int ordersCount, int minMillisecondsDistance)
        {
            try
            {
                if (_connector.IsConnected == false
                    || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

                if (StartProgram != StartProgram.IsOsTrader || ordersCount <= 1)
                {
                    BuyAtMarketToPosition(position, volume);
                    return;
                }

                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63, LogMessageType.System);
                    return;
                }

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return;
                }

                decimal price = (decimal)PriceBestAsk;

                _icebergMaker.MakeNewIceberg(price, ManualPositionSupport.SecondToOpen,
                    ordersCount, position, IcebergType.ModifyBuy, volume, this, OrderPriceType.Market, minMillisecondsDistance);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Add new limit order Buy to position at price intersection
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candles count</param>
        /// <param name="lifeTimeType">order life type</param>
        public void BuyAtStopToPosition(Position position, decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, int expiresBars, PositionOpenerToStopLifeTimeType lifeTimeType)
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
                positionOpener.Security = Security.Name;
                positionOpener.Number = NumberGen.GetNumberDeal(StartProgram);
                positionOpener.ExpiresBars = expiresBars;
                positionOpener.TimeCreate = TimeServerCurrent;
                positionOpener.OrderCreateBarNumber = CandlesFinishedOnly.Count;
                positionOpener.TabName = TabName;
                positionOpener.LifeTimeType = lifeTimeType;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    positionOpener.PriceOrder = priceLimit;
                }
                else
                {
                    positionOpener.PriceOrder = priceRedLine;
                }

                positionOpener.PriceRedLine = priceRedLine;
                positionOpener.ActivateType = activateType;
                positionOpener.Side = Side.Buy;
                positionOpener.OrderPriceType = OrderPriceType.Limit;
                positionOpener.PositionNumber = position.Number;

                PositionOpenerToStop.Add(positionOpener);
                UpdateStopLimits();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Add new limit order Buy to position at price intersection
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candles count</param>
        public void BuyAtStopToPosition(Position position, decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, int expiresBars)
        {
            BuyAtStopToPosition(position, volume, priceLimit, priceRedLine, activateType, expiresBars, PositionOpenerToStopLifeTimeType.CandlesCount);
        }

        /// <summary>
        /// Add new limit order Buy to position at price intersection
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        public void BuyAtStopToPosition(Position position, decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType)
        {
            BuyAtStopToPosition(position, volume, priceLimit, priceRedLine, activateType, 1);
        }

        /// <summary>
        /// Add new market order Buy to position at price intersection
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="volume">volume</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candles count</param>
        /// <param name="lifeTimeType">order life type</param>
        public void BuyAtStopMarketToPosition(Position position, decimal volume, decimal priceRedLine, StopActivateType activateType, int expiresBars, PositionOpenerToStopLifeTimeType lifeTimeType)
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
                positionOpener.Security = Security.Name;
                positionOpener.Number = NumberGen.GetNumberDeal(StartProgram);
                positionOpener.ExpiresBars = expiresBars;
                positionOpener.TimeCreate = TimeServerCurrent;
                positionOpener.OrderCreateBarNumber = CandlesFinishedOnly.Count;
                positionOpener.TabName = TabName;
                positionOpener.LifeTimeType = lifeTimeType;

                positionOpener.PriceOrder = priceRedLine;
                
                positionOpener.PriceRedLine = priceRedLine;
                positionOpener.ActivateType = activateType;
                positionOpener.Side = Side.Buy;
                positionOpener.OrderPriceType = OrderPriceType.Market;
                positionOpener.PositionNumber = position.Number;

                PositionOpenerToStop.Add(positionOpener);
                UpdateStopLimits();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Add new market order Buy to position at price intersection
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="volume">volume</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candles count</param>
        public void BuyAtStopMarketToPosition(Position position, decimal volume, decimal priceRedLine, StopActivateType activateType, int expiresBars)
        {
            BuyAtStopMarketToPosition(position, volume, priceRedLine, activateType, expiresBars, PositionOpenerToStopLifeTimeType.CandlesCount);
        }

        /// <summary>
        /// Add new market order Buy to position at price intersection
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="volume">volume</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a buy order will be placed</param>
        /// <param name="activateType">activation type</param>
        public void BuyAtStopMarketToPosition(Position position, decimal volume, decimal priceRedLine, StopActivateType activateType)
        {
            BuyAtStopMarketToPosition(position, volume, priceRedLine, activateType, 1);
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

                decimal price = (decimal)_connector.BestBid;

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label290, LogMessageType.System);
                    return null;
                }

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    if (!Connector.EmulatorIsOn)
                    {
                        price = price - Security.PriceStep * 40;
                    }
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
        public Position SellAtIceberg(decimal volume, decimal price, int orderCount)
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

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                if (Security != null)
                {
                    if (Convert.ToDouble(Security.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    {
                        int point = Convert.ToDouble(Security.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                        while (price % Security.PriceStep != 0)
                        {
                            price = price - 1;
                        }
                    }
                }
                else
                {
                    decimal lastPrice = (decimal)_connector.BestBid;
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
                newDeal.Lots = Security.Lot;
                newDeal.PriceStepCost = Security.PriceStepCost;
                newDeal.PriceStep = Security.PriceStep;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    newDeal.PortfolioValueOnOpenPosition = Portfolio.ValueCurrent;
                }
                else
                { // Tester, Optimizer, etc
                    newDeal.PortfolioValueOnOpenPosition = Math.Round(Portfolio.ValueCurrent, 2);
                }

                _journal.SetNewDeal(newDeal);

                _icebergMaker.MakeNewIceberg(price, ManualPositionSupport.SecondToOpen, orderCount,
                    newDeal, IcebergType.Open, volume, this, OrderPriceType.Limit, 0);

                return newDeal;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// Enter the short position at iceberg 
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="price">price</param>
        /// <param name="orderCount">orders count</param>
        /// <param name="signalType">open position signal name. Will be written to position property: SignalTypeOpen</param>
        public Position SellAtIceberg(decimal volume, decimal price, int orderCount, string signalType)
        {
            if (_connector.IsConnected == false
                || _connector.IsReadyToTrade == false)
            {
                SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                return null;
            }

            Position position = SellAtIceberg(volume, price, orderCount);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// Enter the short position at iceberg with MARKET orders
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="orderCount">iceberg orders count</param>
        /// <param name="minMillisecondsDistance">minimum time interval between orders in milliseconds</param>
        public Position SellAtIcebergMarket(decimal volume, int orderCount, int minMillisecondsDistance)
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
                    return SellAtMarket(volume);
                }

                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63, LogMessageType.System);
                    return null;
                }

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }


                Position newDeal = new Position();
                newDeal.Number = NumberGen.GetNumberDeal(StartProgram);
                newDeal.Direction = Side.Sell;
                newDeal.State = PositionStateType.Opening;

                newDeal.NameBot = TabName;
                newDeal.Lots = Security.Lot;
                newDeal.PriceStepCost = Security.PriceStepCost;
                newDeal.PriceStep = Security.PriceStep;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    newDeal.PortfolioValueOnOpenPosition = Portfolio.ValueCurrent;
                }
                else
                { // Tester, Optimizer, etc
                    newDeal.PortfolioValueOnOpenPosition = Math.Round(Portfolio.ValueCurrent, 2);
                }

                _journal.SetNewDeal(newDeal);

                decimal price = (decimal)PriceBestBid;

                _icebergMaker.MakeNewIceberg(price, ManualPositionSupport.SecondToOpen, orderCount,
                    newDeal, IcebergType.Open, volume, this, OrderPriceType.Market, minMillisecondsDistance);

                return newDeal;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// Enter the short position at iceberg with MARKET orders
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="orderCount">orders count</param>
        /// <param name="minMillisecondsDistance">minimum time interval between orders in milliseconds</param>
        /// <param name="signalType">open position signal name. Will be written to position property: SignalTypeOpen</param>
        public Position SellAtIcebergMarket(decimal volume, int orderCount, int minMillisecondsDistance, string signalType)
        {
            if (_connector.IsConnected == false
                || _connector.IsReadyToTrade == false)
            {
                SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                return null;
            }

            Position position = SellAtIcebergMarket(volume, orderCount, minMillisecondsDistance);

            if (position != null)
            {
                position.SignalTypeOpen = signalType;
            }

            return position;
        }

        /// <summary>
        /// Create a FAKE short position
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="price">order price</param>
        /// <param name="time">order time</param>
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

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                price = RoundPrice(price, Security, direction);

                Position newDeal = _dealCreator.CreatePosition(
                    TabName, direction, price, volume, OrderPriceType.Limit,
                    ManualPositionSupport.SecondToOpen, Security, Portfolio, 
                    StartProgram, ManualPositionSupport.OrderTypeTime,
                    ManualPositionSupport.LimitsMakerOnly);

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
        /// Create a FAKE short position
        /// </summary>
        /// <param name="volume">volume</param>
        /// <param name="price">order price</param>
        /// <param name="time">order time</param>
        /// <param name="signalType">open position signal name. Will be written to position property: SignalTypeOpen</param>
        public Position SellAtFake(decimal volume, decimal price, DateTime time, string signalType)
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

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                price = RoundPrice(price, Security, direction);

                Position newDeal = _dealCreator.CreatePosition(
                    TabName, direction, price, volume, OrderPriceType.Limit,
                    ManualPositionSupport.SecondToOpen, Security, Portfolio, 
                    StartProgram, ManualPositionSupport.OrderTypeTime,
                    ManualPositionSupport.LimitsMakerOnly);

                newDeal.SignalTypeOpen = signalType;

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
                positionOpener.Security = Security.Name;
                positionOpener.Number = NumberGen.GetNumberDeal(StartProgram);
                positionOpener.TabName = TabName;
                positionOpener.ExpiresBars = expiresBars;
                positionOpener.TimeCreate = TimeServerCurrent;
                positionOpener.OrderCreateBarNumber = CandlesFinishedOnly.Count;
                positionOpener.LifeTimeType = lifeTimeType;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    positionOpener.PriceOrder = priceLimit;
                }
                else
                {
                    positionOpener.PriceOrder = priceRedLine;
                }

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
                positionOpener.Security = Security.Name;
                positionOpener.Number = NumberGen.GetNumberDeal(StartProgram);
                positionOpener.TabName = TabName;
                positionOpener.ExpiresBars = expiresBars;
                positionOpener.TimeCreate = TimeServerCurrent;
                positionOpener.OrderCreateBarNumber = CandlesFinishedOnly.Count;
                positionOpener.LifeTimeType = lifeTimeType;
                positionOpener.PriceRedLine = priceRedLine;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    positionOpener.PriceOrder = priceLimit;
                }
                else
                {
                    positionOpener.PriceOrder = priceRedLine;
                }

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
        /// Cancel all sellAtStop orders
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

                ShortUpdate(position, priceLimit, volume, ManualPositionSupport.SecondToOpen, false, OrderPriceType.Limit, true);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Add new order to Short position at limit
        /// Active orders already on the exchange will not be withdrawn from the market. 
        /// </summary>
        /// <param name="position">position to which the order will be added</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="volume">volume</param>
        public void SellAtLimitToPositionUnsafe(Position position, decimal priceLimit, decimal volume)
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

                ShortUpdate(position, priceLimit, volume, ManualPositionSupport.SecondToOpen, false, OrderPriceType.Limit, false);
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

                decimal price = (decimal)_connector.BestBid;

                if (price == 0)
                {
                    SetNewLogMessage(TabName + OsLocalization.Trader.Label66, LogMessageType.Error);
                    return;
                }

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    if (!Connector.EmulatorIsOn)
                    {
                        price = price - Security.PriceStep * 40;
                    }
                }

                if (Security != null && Security.PriceStep < 1 && Convert.ToDouble(Security.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                {
                    int countPoint = Convert.ToDouble(Security.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                    price = Math.Round(price, countPoint);
                }
                else if (Security != null && Security.PriceStep >= 1)
                {
                    price = Math.Round(price, 0);
                    while (price % Security.PriceStep != 0)
                    {
                        price = price - 1;
                    }
                }

                if (_connector.MarketOrdersIsSupport)
                {
                    ShortUpdate(position, price, volume, ManualPositionSupport.SecondToOpen, false, OrderPriceType.Market, true);
                }
                else
                {
                    ShortUpdate(position, price, volume, ManualPositionSupport.SecondToOpen, false, OrderPriceType.Market, true);
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
        /// <param name="ordersCount">iceberg orders count</param>
        public void SellAtIcebergToPosition(Position position, decimal price, decimal volume, int ordersCount)
        {
            try
            {
                if (_connector.IsConnected == false
                   || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

                if (StartProgram != StartProgram.IsOsTrader || ordersCount <= 1)
                {
                    if (position.Direction == Side.Buy)
                    {
                        ClosePeaceOfDeal(position, OrderPriceType.Limit, price, ManualPositionSupport.SecondToClose, volume, true, false);
                        return;
                    }

                    ShortUpdate(position, price, volume, ManualPositionSupport.SecondToOpen, false, OrderPriceType.Limit, true);
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

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return;
                }

                if (Security != null)
                {// if we do not test, then we cut the price by the minimum step of the instrument
                    if (Convert.ToDouble(Security.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',').Length != 1)
                    { // truncate if decimal places
                        int point = Convert.ToDouble(Security.PriceStep).ToString(new CultureInfo("ru-RU")).Split(',')[1].Length;
                        price = Math.Round(price, point);
                    }
                    else
                    {
                        price = Math.Round(price, 0);
                        while (price % Security.PriceStep != 0)
                        {
                            price = price - 1;
                        }
                    }
                }
                else
                {
                    decimal lastPrice = (decimal)_connector.BestBid;
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


                _icebergMaker.MakeNewIceberg(price, ManualPositionSupport.SecondToOpen,
                    ordersCount, position, IcebergType.ModifySell, volume, this, OrderPriceType.Limit, 0);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Add new order to Short position at iceberg with MARKET orders
        /// </summary>
        /// <param name="position">position to which the order will be added</param>
        /// <param name="volume">volume</param>
        /// <param name="ordersCount">iceberg orders count</param>
        /// <param name="minMillisecondsDistance">minimum time interval between orders in milliseconds</param>
        public void SellAtIcebergToPositionMarket(Position position, decimal volume, int ordersCount, int minMillisecondsDistance)
        {
            try
            {
                if (_connector.IsConnected == false
                   || _connector.IsReadyToTrade == false)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                    return;
                }

                if (StartProgram != StartProgram.IsOsTrader || ordersCount <= 1)
                {
                    SellAtMarketToPosition(position, volume);
                    return;
                }

                if (volume == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label63, LogMessageType.System);
                    return;
                }

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return;
                }

                decimal price = (decimal)PriceBestBid;

                _icebergMaker.MakeNewIceberg(price, ManualPositionSupport.SecondToOpen, ordersCount,
                    position, IcebergType.ModifySell, volume, this, OrderPriceType.Market, minMillisecondsDistance);
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Add new limit order Sell to position at price intersection
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a sell order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candles count</param>
        /// <param name="lifeTimeType">order life type</param>
        public void SellAtStopToPosition(Position position, decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, int expiresBars, PositionOpenerToStopLifeTimeType lifeTimeType)
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
                positionOpener.Security = Security.Name;
                positionOpener.Number = NumberGen.GetNumberDeal(StartProgram);
                positionOpener.ExpiresBars = expiresBars;
                positionOpener.TimeCreate = TimeServerCurrent;
                positionOpener.OrderCreateBarNumber = CandlesFinishedOnly.Count;
                positionOpener.TabName = TabName;
                positionOpener.LifeTimeType = lifeTimeType;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    positionOpener.PriceOrder = priceLimit;
                }
                else
                {
                    positionOpener.PriceOrder = priceRedLine;
                }

                positionOpener.PriceRedLine = priceRedLine;
                positionOpener.ActivateType = activateType;
                positionOpener.Side = Side.Sell;
                positionOpener.OrderPriceType = OrderPriceType.Limit;
                positionOpener.PositionNumber = position.Number;

                PositionOpenerToStop.Add(positionOpener);
                UpdateStopLimits();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Add new limit order Sell to position at price intersection
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a sell order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candles count</param>
        public void SellAtStopToPosition(Position position, decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType, int expiresBars)
        {
            SellAtStopToPosition(position, volume, priceLimit, priceRedLine, activateType, expiresBars, PositionOpenerToStopLifeTimeType.CandlesCount);
        }

        /// <summary>
        /// Add new limit order Sell to position at price intersection
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="volume">volume</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a sell order will be placed</param>
        /// <param name="activateType">activation type</param>
        public void SellAtStopToPosition(Position position, decimal volume, decimal priceLimit, decimal priceRedLine, StopActivateType activateType)
        {
            SellAtStopToPosition(position, volume, priceLimit, priceRedLine, activateType, 1);
        }

        /// <summary>
        /// Add new market order Sell to position at price intersection
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="volume">volume</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a sell order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candles count</param>
        /// <param name="lifeTimeType">order life type</param>
        public void SellAtStopMarketToPosition(Position position, decimal volume, decimal priceRedLine, StopActivateType activateType, int expiresBars, PositionOpenerToStopLifeTimeType lifeTimeType)
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
                positionOpener.Security = Security.Name;
                positionOpener.Number = NumberGen.GetNumberDeal(StartProgram);
                positionOpener.ExpiresBars = expiresBars;
                positionOpener.TimeCreate = TimeServerCurrent;
                positionOpener.OrderCreateBarNumber = CandlesFinishedOnly.Count;
                positionOpener.TabName = TabName;
                positionOpener.LifeTimeType = lifeTimeType;

                positionOpener.PriceOrder = priceRedLine;

                positionOpener.PriceRedLine = priceRedLine;
                positionOpener.ActivateType = activateType;
                positionOpener.Side = Side.Sell;
                positionOpener.OrderPriceType = OrderPriceType.Market;
                positionOpener.PositionNumber = position.Number;

                PositionOpenerToStop.Add(positionOpener);
                UpdateStopLimits();
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Add new market order Sell to position at price intersection
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="volume">volume</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a sell order will be placed</param>
        /// <param name="activateType">activation type</param>
        /// <param name="expiresBars">life time in candles count</param>
        public void SellAtStopMarketToPosition(Position position, decimal volume, decimal priceRedLine, StopActivateType activateType, int expiresBars)
        {
            SellAtStopMarketToPosition(position, volume, priceRedLine, activateType, expiresBars, PositionOpenerToStopLifeTimeType.CandlesCount);
        }

        /// <summary>
        /// Add new market order Sell to position at price intersection
        /// </summary>
        /// <param name="position">position</param>
        /// <param name="volume">volume</param>
        /// <param name="priceRedLine">the price of the line, after reaching which a sell order will be placed</param>
        /// <param name="activateType">activation type</param>
        public void SellAtStopMarketToPosition(Position position, decimal volume, decimal priceRedLine, StopActivateType activateType)
        {
            SellAtStopMarketToPosition(position, volume, priceRedLine, activateType, 1);
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
                        if (positions[i] == null)
                        {
                            continue;
                        }
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

                position.ProfitOrderIsActive = false;
                position.StopOrderIsActive = false;

                for (int i = 0; position.CloseOrders != null && i < position.CloseOrders.Count; i++)
                {
                    if (position.CloseOrders[i].State == OrderStateType.Active)
                    {
                        _connector.OrderCancel(position.CloseOrders[i]);
                    }
                }

                for (int i = 0; position.OpenOrders != null && i < position.OpenOrders.Count; i++)
                {
                    if (position.OpenOrders[i].State == OrderStateType.Active)
                    {
                        _connector.OrderCancel(position.OpenOrders[i]);
                    }
                }

                if (Security == null)
                {
                    return;
                }

                Side sideCloseOrder = Side.Buy;

                if (position.Direction == Side.Buy)
                {
                    sideCloseOrder = Side.Sell;
                }

                price = RoundPrice(price, Security, sideCloseOrder);

                if (position.State == PositionStateType.Done &&
                    position.OpenVolume == 0)
                {
                    return;
                }

                position.State = PositionStateType.Closing;

                Order closeOrder
                    = _dealCreator.CreateCloseOrderForDeal(Security, position, price,
                    OrderPriceType.Limit, new TimeSpan(1, 1, 1, 1), 
                    StartProgram, ManualPositionSupport.OrderTypeTime, 
                    _connector.ServerFullName, ManualPositionSupport.LimitsMakerOnly);

                closeOrder.SecurityNameCode = Security.Name;
                closeOrder.SecurityClassCode = Security.NameClass;
                closeOrder.PortfolioNumber = Portfolio.Number;

                if (volume < position.OpenVolume &&
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

                position.ProfitOrderIsActive = false;
                position.StopOrderIsActive = false;

                if (volume <= 0 || position.OpenVolume <= 0)
                {
                    return;
                }
                decimal price = (decimal)_connector.BestAsk;

                if (price == 0)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label290, LogMessageType.System);
                    return;
                }
                if (StartProgram == StartProgram.IsOsTrader)
                {
                    if (position.Direction == Side.Buy)
                    {
                        if (!Connector.EmulatorIsOn)
                        {
                            price = (decimal)_connector.BestBid - Security.PriceStep * 40;
                        }
                    }
                    else //if (position.Direction == Side.Sell)
                    {
                        if (!Connector.EmulatorIsOn)
                        {
                            price = price + Security.PriceStep * 40;
                        }
                    }
                }
                if (_connector.MarketOrdersIsSupport)
                {
                    if (position.OpenVolume <= volume)
                    {
                        CloseDeal(position, OrderPriceType.Market, price, ManualPositionSupport.SecondToClose, false, true);
                    }
                    else if (position.OpenVolume > volume)
                    {
                        ClosePeaceOfDeal(position, OrderPriceType.Market, price, ManualPositionSupport.SecondToClose, volume, true, false);
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
                    CloseDeal(position, OrderPriceType.Limit, priceLimit, ManualPositionSupport.SecondToClose, false, true);
                }
                else if (position.OpenVolume > volume)
                {
                    ClosePeaceOfDeal(position, OrderPriceType.Limit, priceLimit, ManualPositionSupport.SecondToClose, volume, true, false);
                }

                if (position.CloseOrders[^1].State == OrderStateType.None)
                {

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
        /// Close a position at a limit price in UnSafe regime. 
        /// Active orders already on the exchange will not be withdrawn from the market. 
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceLimit">order price</param>
        /// <param name="volume">volume required to close</param>
        public void CloseAtLimitUnsafe(Position position, decimal priceLimit, decimal volume)
        {
            try
            {
                if (volume <= 0 || position.OpenVolume <= 0)
                {
                    return;
                }

                if (position.OpenVolume <= volume)
                {
                    CloseDeal(position, OrderPriceType.Limit, priceLimit, ManualPositionSupport.SecondToClose, false, false);
                }
                else if (position.OpenVolume > volume)
                {
                    ClosePeaceOfDeal(position, OrderPriceType.Limit, priceLimit, ManualPositionSupport.SecondToClose, volume, false, false);
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
        /// <param name="ordersCount">iceberg orders count</param>
        public void CloseAtIceberg(Position position, decimal priceLimit, decimal volume, int ordersCount)
        {
            try
            {
                if (volume <= 0 || position.OpenVolume <= 0)
                {
                    return;
                }
                if (StartProgram != StartProgram.IsOsTrader || ordersCount <= 1)
                {
                    CloseAtLimit(position, priceLimit, volume);
                    return;
                }

                if (position.Direction == Side.Buy)
                {
                    SellAtIcebergToPosition(position, priceLimit, volume, ordersCount);
                }
                else
                {
                    BuyAtIcebergToPosition(position, priceLimit, volume, ordersCount);
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
        /// <param name="ordersCount">iceberg orders count</param>
        /// <param name="signalType">close position signal name</param>
        public void CloseAtIceberg(Position position, decimal priceLimit, decimal volume, int ordersCount, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtIceberg(position, priceLimit, volume, ordersCount);
        }

        /// <summary>
        /// Close position at iceberg with MARKET orders
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="volume">volume required to close</param>
        /// <param name="ordersCount">iceberg orders count</param>
        /// <param name="minMillisecondsDistance">minimum time interval between orders in milliseconds</param>
        public void CloseAtIcebergMarket(Position position, decimal volume, int ordersCount, int minMillisecondsDistance)
        {
            try
            {
                if (volume <= 0 || position.OpenVolume <= 0)
                {
                    return;
                }
                if (StartProgram != StartProgram.IsOsTrader || ordersCount <= 1)
                {
                    CloseAtMarket(position, volume);
                    return;
                }

                if (position.Direction == Side.Buy)
                {
                    SellAtIcebergToPositionMarket(position, volume, ordersCount, minMillisecondsDistance);
                }
                else
                {
                    BuyAtIcebergToPositionMarket(position, volume, ordersCount, minMillisecondsDistance);
                }
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Close position at iceberg with MARKET orders
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="volume">volume required to close</param>
        /// <param name="ordersCount">iceberg orders count</param>
        /// <param name="minMillisecondsDistance">minimum time interval between orders in milliseconds</param>
        /// <param name="signalType">close position signal name</param>
        public void CloseAtIcebergMarket(Position position, decimal volume, int ordersCount, int minMillisecondsDistance, string signalType)
        {
            position.SignalTypeClose = signalType;
            CloseAtIcebergMarket(position, volume, ordersCount, minMillisecondsDistance);
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
            position.SignalTypeStop = signalType;
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

                if (position.StopOrderIsActive &&
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

                position.StopOrderIsActive = false;

                position.StopIsMarket = true;
                position.StopOrderPrice = priceActivation;
                position.StopOrderRedLine = priceActivation;
                position.StopOrderIsActive = true;

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
            position.SignalTypeStop = signalType;
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
            if (position.Direction == Side.Buy &&
                position.StopOrderRedLine > priceActivation)
            {
                position.StopOrderIsActive = true;
                return;
            }

            if (position.Direction == Side.Sell &&
                position.StopOrderRedLine != 0 && 
                position.StopOrderRedLine < priceActivation)
            {
                position.StopOrderIsActive = true;
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
            position.SignalTypeStop = signalType;
            CloseAtTrailingStop(position, priceActivation, priceOrder);
        }

        /// <summary>
        /// Place a trailing stop order for a position by Market
        /// </summary>
        /// <param name="position">position to be closed</param>
        /// <param name="priceActivation">the price of the stop order, after reaching which the order is placed</param>
        public void CloseAtTrailingStopMarket(Position position, decimal priceActivation)
        {
            if (position.Direction == Side.Buy &&
                position.StopOrderRedLine > priceActivation)
            {
                position.StopOrderIsActive = true;
                return;
            }

            if (position.Direction == Side.Sell &&
                position.StopOrderRedLine != 0 &&
                position.StopOrderRedLine < priceActivation)
            {
                position.StopOrderIsActive = true;
                return;
            }

            decimal volume = position.OpenVolume;

            if (volume == 0)
            {
                return;
            }

            position.StopOrderIsActive = false;

            position.StopIsMarket = true;
            position.StopOrderPrice = priceActivation;
            position.StopOrderRedLine = priceActivation;
            position.StopOrderIsActive = true;

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
            position.SignalTypeStop = signalType;
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
            position.SignalTypeProfit = signalType;
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

                if (position.ProfitOrderIsActive &&
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


                position.ProfitOrderIsActive = false;

                position.ProfitOrderPrice = priceActivation;
                position.ProfitOrderRedLine = priceActivation;
                position.ProfitIsMarket = true;

                position.ProfitOrderIsActive = true;

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
            position.SignalTypeProfit = signalType;
            CloseAtProfitMarket(position, priceActivation);
        }

        /// <summary>
        /// Withdraw all robot open orders from the system
        /// </summary>
        public void CloseAllOrderInSystem()
        {
            try
            {
                Position[] positions = _journal.OpenPositions.ToArray();

                if (positions == null)
                {
                    return;
                }

                for (int i = 0; i < positions.Length; i++)
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
                position.StopOrderIsActive = false;
                position.ProfitOrderIsActive = false;


                if (position.OpenOrders != null &&
                   position.OpenOrders.Count > 0)
                {
                    for (int i = 0; i < position.OpenOrders.Count; i++)
                    {
                        Order order = position.OpenOrders[i];

                        if(order == null)
                        {
                            continue;
                        }

                        if (order.State == OrderStateType.Active)
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

                        if(closeOrder == null)
                        {
                            continue;
                        }

                        if (closeOrder.State == OrderStateType.Active)
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
            if (order == null)
            {
                return;
            }

            if (StartProgram != StartProgram.IsOsTrader)
            {
                SetNewLogMessage(OsLocalization.Trader.Label371, LogMessageType.Error);
                return;
            }

            if (IsConnected == false ||
                IsReadyToTrade == false)
            {
                SetNewLogMessage(OsLocalization.Trader.Label372, LogMessageType.Error);
                return;
            }

            _connector.ChangeOrderPrice(order, newPrice);
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
                newOrder.OrderTypeTime = order.OrderTypeTime;
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

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                price = RoundPrice(price, Security, Side.Sell);

                Position newDeal = _dealCreator.CreatePosition(
                    TabName, direction, price, volume, priceType,
                    timeLife, Security, Portfolio, StartProgram, 
                    ManualPositionSupport.OrderTypeTime,
                    ManualPositionSupport.LimitsMakerOnly);

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
        /// <param name="orderType">whether the order is a result of a stop or a profit</param>
        /// <param name="safeRegime">if True - active orders to close the position will be withdrawn.</param>
        private void ShortUpdate(Position position, decimal price, decimal volume, TimeSpan timeLife,
            bool isStopOrProfit, OrderPriceType orderType, bool safeRegime)
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

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return;
                }

                price = RoundPrice(price, Security, Side.Sell);

                if (safeRegime == true)
                {
                    if (position.OpenOrders != null &&
                        position.OpenOrders.Count > 0)
                    {
                        for (int i = 0; i < position.OpenOrders.Count; i++)
                        {
                            if (position.OpenOrders[i].State == OrderStateType.Active)
                            {
                                _connector.OrderCancel(position.OpenOrders[i]);
                            }
                        }
                    }
                }

                Order newOrder = 
                    _dealCreator.CreateOrder(
                        Security, Side.Sell, price, volume, 
                    orderType, ManualPositionSupport.SecondToOpen, 
                    StartProgram, OrderPositionConditionType.Open, 
                    ManualPositionSupport.OrderTypeTime, _connector.ServerFullName,
                    ManualPositionSupport.LimitsMakerOnly, position.Number);

                newOrder.IsStopOrProfit = isStopOrProfit;
                newOrder.LifeTime = timeLife;
                position.AddNewOpenOrder(newOrder);

                SetNewLogMessage(Security.Name + " short position modification \n"
                    + "Order direction: " + Side.Sell.ToString() + "\n"
                    + "Price: " + price.ToString() + "\n"
                    + "Volume: " + volume.ToString() + "\n"
                    + "Position num: " + position.Number.ToString()
                    , LogMessageType.Trade);

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

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return null;
                }

                price = RoundPrice(price, Security, Side.Buy);

                Position newDeal = _dealCreator.CreatePosition(
                    TabName, direction, price, volume, priceType,
                    timeLife, Security, Portfolio, StartProgram, 
                    ManualPositionSupport.OrderTypeTime,
                    ManualPositionSupport.LimitsMakerOnly);

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
        /// <param name="orderType">whether the order is a result of a stop or a profit</param>
        /// <param name="safeRegime">if True - active orders to close the position will be withdrawn.</param>
        private void LongUpdate(Position position, decimal price, decimal volume, TimeSpan timeLife,
            bool isStopOrProfit, OrderPriceType orderType, bool safeRegime)
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

                if (Security == null || Portfolio == null)
                {
                    SetNewLogMessage(OsLocalization.Trader.Label64, LogMessageType.System);
                    return;
                }

                price = RoundPrice(price, Security, Side.Buy);

                if (safeRegime == true)
                {
                    if (position.OpenOrders != null &&
                        position.OpenOrders.Count > 0)
                    {
                        for (int i = 0; i < position.OpenOrders.Count; i++)
                        {
                            if (position.OpenOrders[i].State == OrderStateType.Active)
                            {
                                _connector.OrderCancel(position.OpenOrders[i]);
                            }
                        }
                    }
                }

                Order newOrder = _dealCreator.CreateOrder(
                    Security, Side.Buy, price, volume, orderType,
                    ManualPositionSupport.SecondToOpen, StartProgram, 
                    OrderPositionConditionType.Open,
                    ManualPositionSupport.OrderTypeTime, 
                    _connector.ServerFullName,
                    ManualPositionSupport.LimitsMakerOnly, position.Number);

                newOrder.IsStopOrProfit = isStopOrProfit;
                newOrder.LifeTime = timeLife;
                newOrder.SecurityNameCode = Security.Name;
                newOrder.SecurityClassCode = Security.NameClass;
                position.AddNewOpenOrder(newOrder);

                SetNewLogMessage(Security.Name + " long position modification \n"
                   + "Order direction: " + Side.Buy.ToString() + "\n"
                   + "Price: " + price.ToString() + "\n"
                   + "Volume: " + volume.ToString() + "\n"
                   + "Position num: " + position.Number.ToString()
                   , LogMessageType.Trade);

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
        /// <param name="safeRegime">if True - active orders to close the position will be withdrawn.</param>
        private void CloseDeal(Position position, OrderPriceType priceType, decimal price, TimeSpan lifeTime,
            bool isStopOrProfit, bool safeRegime)
        {
            try
            {
                if (position == null)
                {
                    return;
                }
                if (safeRegime)
                {
                    for (int i = 0; position.CloseOrders != null && i < position.CloseOrders.Count; i++)
                    {
                        if(position.CloseOrders[i] == null)
                        {
                            continue;
                        }

                        if (position.CloseOrders[i].State == OrderStateType.Active)
                        {
                            _connector.OrderCancel(position.CloseOrders[i]);
                        }
                    }

                    for (int i = 0; position.OpenOrders != null && i < position.OpenOrders.Count; i++)
                    {
                        if (position.OpenOrders[i] == null)
                        {
                            continue;
                        }
                        if (position.OpenOrders[i].State == OrderStateType.Active)
                        {
                            _connector.OrderCancel(position.OpenOrders[i]);
                        }
                    }
                }

                if (Security == null)
                {
                    return;
                }

                Side sideCloseOrder = Side.Buy;
                if (position.Direction == Side.Buy)
                {
                    sideCloseOrder = Side.Sell;
                }
                price = RoundPrice(price, Security, sideCloseOrder);

                if (position.State == PositionStateType.Done &&
                    position.OpenVolume == 0)
                {
                    return;
                }

                position.State = PositionStateType.Closing;

                Order closeOrder = _dealCreator.CreateCloseOrderForDeal(Security, position, price,
                    priceType, lifeTime, StartProgram, 
                    ManualPositionSupport.OrderTypeTime, 
                    _connector.ServerFullName, ManualPositionSupport.LimitsMakerOnly);

                closeOrder.SecurityNameCode = Security.Name;
                closeOrder.SecurityClassCode = Security.NameClass;

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
        /// <param name="safeRegime">if True - active orders to close the position will be withdrawn.</param>
        /// <param name="isStopOrProfit">whether the order is a result of a stop or a profit</param>
        private void ClosePeaceOfDeal(Position position, OrderPriceType priceType, decimal price, TimeSpan lifeTime,
            decimal volume, bool safeRegime, bool isStopOrProfit)
        {
            try
            {
                if (position == null)
                {
                    return;
                }

                if (safeRegime == true)
                {
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
                            if (position.OpenOrders[i].State == OrderStateType.Active)
                            {
                                _connector.OrderCancel(position.OpenOrders[i]);
                            }
                        }
                    }
                }

                if (Security == null)
                {
                    return;
                }

                Side sideCloseOrder = Side.Buy;
                if (position.Direction == Side.Buy)
                {
                    sideCloseOrder = Side.Sell;
                }
                price = RoundPrice(price, Security, sideCloseOrder);

                Order closeOrder = _dealCreator.CreateCloseOrderForDeal(
                    Security, position, price,
                    priceType, lifeTime, StartProgram, 
                    ManualPositionSupport.OrderTypeTime, 
                    _connector.ServerFullName, ManualPositionSupport.LimitsMakerOnly);

                if (closeOrder == null)
                {
                    if (position.OpenVolume == 0)
                    {
                        position.State = PositionStateType.OpeningFail;
                    }

                    return;
                }

                closeOrder.Volume = volume;
                closeOrder.IsStopOrProfit = isStopOrProfit;

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

                if (position.StopOrderIsActive &&
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

                    decimal lastBid = (decimal)PriceBestBid;
                    decimal lastAsk = (decimal)PriceBestAsk;

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

                position.StopOrderIsActive = false;

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
                position.StopOrderIsActive = true;

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

                if (position.ProfitOrderIsActive &&
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

                    decimal lastBid = (decimal)PriceBestBid;
                    decimal lastAsk = (decimal)PriceBestAsk;

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


                position.ProfitOrderIsActive = false;

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
                position.ProfitOrderIsActive = true;

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
        public decimal RoundPrice(decimal price, Security security, Side side)
        {
            try
            {
                if (Security.PriceStep == 0)
                {
                    return price;
                }

                if (security.Decimals > 0)
                {
                    price = Math.Round(price, Security.Decimals);

                    decimal minStep = 0.1m;

                    for (int i = 1; i < security.Decimals; i++)
                    {
                        minStep = minStep * 0.1m;
                    }

                    while (price % Security.PriceStep != 0)
                    {
                        price = price - minStep;
                    }
                }
                else
                {

                    price = Math.Round(price, 0);

                    while (price % Security.PriceStep != 0)
                    {
                        price = price - 1;
                    }
                }

                return price;
            }
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return 0;
        }

        /// <summary>
        /// Is it possible to make a trade with such a volume?
        /// </summary>
        /// <param name="volume">QTY for trade</param>
        /// <returns></returns>
        public bool CanTradeThisVolume(decimal volume)
        {
            if(this.StartProgram != StartProgram.IsOsTrader)
            {
                return true;
            }

            if(volume <= 0)
            {
                return false;
            }

            Security sec = this.Security;

            if (sec == null)
            {
                return false;
            }

            if(sec.VolumeStep != 0)
            {
                if(volume <  sec.VolumeStep)
                {
                    return false;
                }
            }

            if(sec.MinTradeAmount != 0)
            {
                if (sec.MinTradeAmountType == MinTradeAmountType.Contract)
                { // внутри бумаги минимальный объём одного ордера указан в контрактах

                    if (sec.MinTradeAmount > volume)
                    {
                        return false;
                    }
                }
                else if(sec.MinTradeAmountType == MinTradeAmountType.C_Currency)
                { // внутри бумаги минимальный объём для одного ордера указан в валюте контракта

                    // 1 пытаемся взять текущую цену из стакана
                    decimal lastPrice = (decimal)PriceBestAsk;

                    if(lastPrice == 0)
                    {
                        lastPrice = (decimal)this.PriceBestBid;
                    }

                    // 2 пытаемся взять текущую цену из свечей
                    
                    if(lastPrice == 0)
                    {
                        List<Candle> candles = this.CandlesAll;

                        if(candles != null 
                            && candles.Count > 0)
                        {
                            lastPrice = candles[^1].Close;
                        }
                    }

                    if(lastPrice != 0)
                    {
                        decimal qtyInContractCurrency = volume * lastPrice;
                        
                        if(qtyInContractCurrency < sec.MinTradeAmount)
                        {
                            return false;
                        }

                    }
                }

            }

            return true;
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
                if (position == null ||
                    ManualPositionSupport == null)
                {
                    return;
                }

                lock (_lockerManualReload)
                {
                    if (position.CloseOrders != null &&
                        position.CloseOrders[position.CloseOrders.Count - 1] != null &&
                        position.CloseOrders[position.CloseOrders.Count - 1].State == OrderStateType.Active)
                    {
                        return;
                    }

                    if (StartProgram == StartProgram.IsOsTrader)
                    {
                        if (position.OpenOrders[position.OpenOrders.Count - 1] == null)
                        {
                            return;
                        }

                        Order openOrder = position.OpenOrders[position.OpenOrders.Count - 1];

                        if (openOrder.TradesIsComing == false)
                        {
                            PositionToSecondLoopSender sender = new PositionToSecondLoopSender() { Position = position };
                            sender.PositionNeedToStopSend += ManualReloadStopsAndProfitToPosition;

                            Task task = new Task(sender.Start);
                            task.Start();
                            return;
                        }
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
                if (!position.StopOrderIsActive && !position.ProfitOrderIsActive)
                {
                    return false;
                }

                if (ServerStatus != ServerConnectStatus.Connect ||
                    Security == null ||
                    Portfolio == null)
                {
                    return false;
                }

                if (position.StopOrderIsActive)
                {

                    if (position.Direction == Side.Buy &&
                        position.StopOrderRedLine >= lastTrade)
                    {
                        position.ProfitOrderIsActive = false;
                        position.StopOrderIsActive = false;

                        if (string.IsNullOrEmpty(position.SignalTypeStop) == false)
                        {
                            position.SignalTypeClose = position.SignalTypeStop;
                        }

                        SetNewLogMessage(
                            "Close Position at Stop. StopPrice: " +
                            position.StopOrderRedLine
                            + " LastMarketPrice: " + lastTrade,
                            LogMessageType.System);

                        if (position.StopIsMarket == false)
                        {
                            CloseDeal(position, OrderPriceType.Limit, position.StopOrderPrice, ManualPositionSupport.SecondToClose, true, true);
                        }
                        else
                        {
                            CloseDeal(position, OrderPriceType.Market, position.StopOrderPrice, ManualPositionSupport.SecondToClose, true, true);
                        }

                        PositionStopActivateEvent?.Invoke(position);
                        return true;
                    }

                    if (position.Direction == Side.Sell &&
                        position.StopOrderRedLine <= lastTrade)
                    {
                        position.StopOrderIsActive = false;
                        position.ProfitOrderIsActive = false;

                        if (string.IsNullOrEmpty(position.SignalTypeStop) == false)
                        {
                            position.SignalTypeClose = position.SignalTypeStop;
                        }

                        SetNewLogMessage(
                            "Close Position at Stop. StopPrice: " +
                            position.StopOrderRedLine
                            + " LastMarketPrice: " + lastTrade,
                            LogMessageType.System);

                        if (position.StopIsMarket == false)
                        {
                            CloseDeal(position, OrderPriceType.Limit, position.StopOrderPrice, ManualPositionSupport.SecondToClose, true, true);
                        }
                        else
                        {
                            CloseDeal(position, OrderPriceType.Market, position.StopOrderPrice, ManualPositionSupport.SecondToClose, true, true);
                        }

                        PositionStopActivateEvent?.Invoke(position);
                        return true;
                    }
                }

                if (position.ProfitOrderIsActive)
                {
                    if (position.Direction == Side.Buy &&
                        position.ProfitOrderRedLine <= lastTrade)
                    {
                        position.StopOrderIsActive = false;
                        position.ProfitOrderIsActive = false;

                        if (string.IsNullOrEmpty(position.SignalTypeProfit) == false)
                        {
                            position.SignalTypeClose = position.SignalTypeProfit;
                        }

                        SetNewLogMessage(
                            "Close Position at Profit. ProfitPrice: " +
                            position.ProfitOrderRedLine
                            + " LastMarketPrice: " + lastTrade,
                            LogMessageType.System);

                        if (position.ProfitIsMarket == false)
                        {
                            CloseDeal(position, OrderPriceType.Limit, position.ProfitOrderPrice, ManualPositionSupport.SecondToClose, true, true);
                        }
                        else
                        {
                            CloseDeal(position, OrderPriceType.Market, position.ProfitOrderPrice, ManualPositionSupport.SecondToClose, true, true);
                        }

                        PositionProfitActivateEvent?.Invoke(position);
                        return true;
                    }

                    if (position.Direction == Side.Sell &&
                        position.ProfitOrderRedLine >= lastTrade)
                    {
                        position.StopOrderIsActive = false;
                        position.ProfitOrderIsActive = false;

                        if (string.IsNullOrEmpty(position.SignalTypeProfit) == false)
                        {
                            position.SignalTypeClose = position.SignalTypeProfit;
                        }

                        SetNewLogMessage(
                            "Close Position at Profit. ProfitPrice: " +
                            position.ProfitOrderRedLine
                            + " LastMarketPrice: " + lastTrade,
                            LogMessageType.System);

                        if (position.ProfitIsMarket == false)
                        {
                            CloseDeal(position, OrderPriceType.Limit, position.ProfitOrderPrice, ManualPositionSupport.SecondToClose, true, true);
                        }
                        else
                        {
                            CloseDeal(position, OrderPriceType.Market, position.ProfitOrderPrice, ManualPositionSupport.SecondToClose, true, true);
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
                    SetNewLogMessage(Security.Name + OsLocalization.Trader.Label67 +
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

                    SetNewLogMessage(Security.Name + OsLocalization.Trader.Label67 +
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
                            price = (decimal)_connector.BestBid - signal.Slippage;
                        }
                        else
                        {
                            price = (decimal)_connector.BestAsk + signal.Slippage;
                        }

                        CloseDeal(position, OrderPriceType.Limit, price, ManualPositionSupport.SecondToClose, true, true);
                    }
                }

                if (signal.SignalType == SignalType.Buy)
                {
                    SetNewLogMessage(Security.Name + OsLocalization.Trader.Label69 + _connector.BestBid, LogMessageType.Signal);

                    if (signal.PriceType == OrderPriceType.Market)
                    {
                        BuyAtMarket(signal.Volume);
                    }
                    else
                    {
                        BuyAtLimit(signal.Volume, (decimal)_connector.BestAsk + signal.Slippage);
                    }
                }
                else if (signal.SignalType == SignalType.Sell)
                {
                    SetNewLogMessage(Security.Name + OsLocalization.Trader.Label68 + _connector.BestBid, LogMessageType.Signal);

                    if (signal.PriceType == OrderPriceType.Market)
                    {
                        SellAtMarket(signal.Volume);
                    }
                    else
                    {
                        SellAtLimit(signal.Volume, (decimal)_connector.BestBid - signal.Slippage);
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
                Security == null ||
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
                if(positions[i] == null)
                {
                    continue;
                }
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

            positions = PositionsAll.FindAll(position => position != null && position.State == PositionStateType.ClosingSurplus ||
                position.OpenVolume < 0);

            if (positions.Count == 0)
            {
                return;
            }
            bool haveOpenOrders = false;

            for (int i = 0; i < positions.Count; i++)
            {
                Position position = positions[i];

                if (position.CloseActive)
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
                    ShortUpdate(position, (decimal)PriceBestBid - Security.PriceStep * 30,
                        Math.Abs(position.OpenVolume), new TimeSpan(0, 0, 1, 0), false, OrderPriceType.Limit, true);
                }
                if (position.Direction == Side.Buy && position.OpenVolume < 0)
                {
                    LongUpdate(position, (decimal)PriceBestAsk + Security.PriceStep * 30,
                        Math.Abs(position.OpenVolume), new TimeSpan(0, 0, 1, 0), false, OrderPriceType.Limit, true);
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
            bool needSave = false;

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
                    needSave = true;
                    continue;
                }

                if (candles[candles.Count - 1].TimeStart > PositionOpenerToStop[i].LastCandleTime)
                {
                    PositionOpenerToStop[i].LastCandleTime = candles[candles.Count - 1].TimeStart;
                    PositionOpenerToStop[i].ExpiresBars = PositionOpenerToStop[i].ExpiresBars - 1;
                    needSave = true;
                }
            }

            if (needSave == true)
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
                Security == null || Portfolio == null)
            {
                return;
            }

            try
            {
                bool needSave = false;

                for (int i = 0;
                    i > -1 && PositionOpenerToStop != null && PositionOpenerToStop.Count != 0 && i < PositionOpenerToStop.Count;
                    i++)
                {
                    if ((PositionOpenerToStop[i].ActivateType == StopActivateType.HigherOrEqual &&
                         price >= PositionOpenerToStop[i].PriceRedLine)
                        ||
                        (PositionOpenerToStop[i].ActivateType == StopActivateType.LowerOrEqual &&
                         price <= PositionOpenerToStop[i].PriceRedLine))
                    {
                        if (PositionOpenerToStop[i].Side == Side.Buy)
                        {
                            PositionOpenerToStopLimit opener = PositionOpenerToStop[i];

                            Position pos = null;

                            if (opener.PositionNumber == 0)
                            {
                                 pos = LongCreate(PositionOpenerToStop[i].PriceOrder,
                                  PositionOpenerToStop[i].Volume, PositionOpenerToStop[i].OrderPriceType,
                                  ManualPositionSupport.SecondToOpen, true);

                                if (pos != null
                                    && !string.IsNullOrEmpty(opener.SignalType))
                                {
                                    pos.SignalTypeOpen = opener.SignalType;
                                }
                            }
                            else
                            {
                                List<Position> openPoses = PositionsOpenAll;

                                for(int f = 0;f < openPoses.Count;f++)
                                {
                                    if (openPoses[f].Number == opener.PositionNumber)
                                    {
                                        pos = openPoses[f];
                                        break;
                                    }
                                }

                                if(pos != null)
                                {
                                    if(pos.Direction == Side.Buy)
                                    {
                                        LongUpdate(pos, PositionOpenerToStop[i].PriceOrder,
                                            PositionOpenerToStop[i].Volume, ManualPositionSupport.SecondToOpen, true,
                                            PositionOpenerToStop[i].OrderPriceType, false);
                                    }
                                    else if(pos.Direction == Side.Sell)
                                    {
                                        ClosePeaceOfDeal(pos,PositionOpenerToStop[i].OrderPriceType,
                                            PositionOpenerToStop[i].PriceOrder, ManualPositionSupport.SecondToClose, 
                                            PositionOpenerToStop[i].Volume, true, true);
                                    }
                                }
                            }

                            if (PositionOpenerToStop.Count == 0)
                            { // the user can remove himself from the layer when he sees that the deal is opening
                                return;
                            }

                            PositionOpenerToStop.RemoveAt(i);
                            i = -1;

                            if (PositionBuyAtStopActivateEvent != null 
                                && pos != null)
                            {
                                PositionBuyAtStopActivateEvent(pos);
                            }
                            needSave = true;
                            continue;
                        }
                        else if (PositionOpenerToStop[i].Side == Side.Sell)
                        {
                            PositionOpenerToStopLimit opener = PositionOpenerToStop[i];

                            Position pos = null;

                            if(opener.PositionNumber == 0)
                            {
                                pos = ShortCreate(PositionOpenerToStop[i].PriceOrder,
                                    PositionOpenerToStop[i].Volume, PositionOpenerToStop[i].OrderPriceType,
                                    ManualPositionSupport.SecondToOpen, true);

                                if (pos != null
                                    && !string.IsNullOrEmpty(opener.SignalType))
                                {
                                    pos.SignalTypeOpen = opener.SignalType;
                                }
                            }
                            else
                            {
                                List<Position> openPoses = PositionsOpenAll;

                                for (int f = 0; f < openPoses.Count; f++)
                                {
                                    if (openPoses[f].Number == opener.PositionNumber)
                                    {
                                        pos = openPoses[f];
                                        break;
                                    }
                                }

                                if (pos != null)
                                {
                                    if (pos.Direction == Side.Sell)
                                    {
                                        ShortUpdate(pos, PositionOpenerToStop[i].PriceOrder,
                                            PositionOpenerToStop[i].Volume, ManualPositionSupport.SecondToOpen, true,
                                            PositionOpenerToStop[i].OrderPriceType, false);
                                    }
                                    else if (pos.Direction == Side.Buy)
                                    {
                                        ClosePeaceOfDeal(pos, PositionOpenerToStop[i].OrderPriceType,
                                            PositionOpenerToStop[i].PriceOrder, ManualPositionSupport.SecondToClose,
                                            PositionOpenerToStop[i].Volume, true, true);
                                    }
                                }
                            }

                            if (PositionOpenerToStop.Count == 0)
                            { // the user can remove himself from the layer when he sees that the deal is opening
                                return;
                            }

                            PositionOpenerToStop.RemoveAt(i);
                            i = -1;

                            if (PositionSellAtStopActivateEvent != null 
                                && pos != null)
                            {
                                PositionSellAtStopActivateEvent(pos);
                            }
                            needSave = true;
                            continue;
                        }
                        i--;
                    }
                }

                if (needSave == true)
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
        private IcebergMaker _icebergMaker;

        /// <summary>
        /// Iceberg Master Requests To Cancel Order
        /// </summary>
        void _icebergMaker_NewOrderNeedToCancel(Order order)
        {
            _connector.OrderCancel(order);
        }

        /// <summary>
        /// Icebergs master requires you to place an order
        /// </summary>
        void _icebergMaker_NewOrderNeedToExecute(Order order)
        {
            _connector.OrderExecute(order);
        }

        /// <summary>
        /// Clear all icebergs from the system
        /// </summary>
        public void ClearIceberg()
        {
            try
            {
                _icebergMaker?.ClearIcebergs();
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
            if (PortfolioOnExchangeChangedEvent != null)
            {
                PortfolioOnExchangeChangedEvent(portfolio);
            }
        }

        /// <summary>
        /// New MarketDepth event handler
        /// </summary>
        private void _connector_GlassChangeEvent(MarketDepth marketDepth)
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

                        if (marketDepth.Asks != null && marketDepth.Asks.Count > 0)
                        {
                            CheckStop(openPositions[i], marketDepth.Asks[0].Price.ToDecimal());
                        }

                        if (openPositions.Count <= i)
                        {
                            continue;
                        }

                        if (marketDepth.Bids != null && marketDepth.Bids.Count > 0)
                        {
                            CheckStop(openPositions[i], marketDepth.Bids[0].Price.ToDecimal());
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
                if(position.State == PositionStateType.Deleted)
                {
                    return;
                }

                if (position.State == PositionStateType.Done)
                {
                    CloseAllOrderToPosition(position);

                    if(position.CloseOrders == null)
                    {
                        return;
                    }

                    if (StartProgram == StartProgram.IsOsTrader)
                    {
                        // высылаем оповещение, только если уже есть закрывающие MyTrades

                        if (position.CloseOrders[^1] != null
                            && position.CloseOrders[^1].MyTrades != null
                            && position.CloseOrders[^1].MyTrades.Count > 0)
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

                        decimal profit = position.ProfitPortfolioAbs;

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
                    if (position.OpenOrders == null)
                    {
                        return;
                    }

                    if (StartProgram == StartProgram.IsOsTrader)
                    {
                        // высылаем оповещение, только если уже есть закрывающие MyTrades

                        if (position.OpenOrders[^1] != null
                            && position.OpenOrders[^1].MyTrades != null
                            && position.OpenOrders[^1].MyTrades.Count > 0)
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
                        if (StartProgram == StartProgram.IsTester)
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

        private DateTime _firstTradeTimeInDayLastSendTime;

        private DateTime _lastTradeTime;

        private decimal _lastTradeQty;

        private decimal _lastTradePrice;

        private int _lastTradeIndex;

        private long _lastTradeIdInTester;

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
                trades.Count == 0 ||
                trades[trades.Count - 1] == null)
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
                _lastTradeIdInTester = 0;
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader)
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (_lastTradeTime == DateTime.MinValue &&
                    _lastTradeIndex == 0)
                {
                    _lastTradeIndex = trades.Count;
                    _lastTradeTime = trades[trades.Count - 1].Time;
                    return;
                }
            }
            else if (StartProgram == StartProgram.IsTester ||
                StartProgram == StartProgram.IsOsOptimizer)
            {
                if (trades[trades.Count - 1].TimeFrameInTester != Entity.TimeFrame.Sec1 &&
                    trades[trades.Count - 1].TimeFrameInTester != Connector.TimeFrame)
                {
                    return;
                }
            }

            Trade trade = trades[trades.Count - 1];

            if (FirstTickToDayEvent != null
                && trade != null
                &&
                (_firstTradeTimeInDayLastSendTime == DateTime.MinValue
                || _firstTradeTimeInDayLastSendTime.Date != trade.Time.Date))
            {
                _firstTradeTimeInDayLastSendTime = trade.Time;
                FirstTickToDayEvent(trade);
            }

            List<Trade> newTrades = new List<Trade>();

            if (StartProgram == StartProgram.IsOsTrader)
            {
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
                                if (trades[i] == null)
                                {
                                    continue;
                                }

                                if (trades[i].Time < _lastTradeTime)
                                {
                                    continue;
                                }
                                if (trades[i].Time == _lastTradeTime
                                    && trades[i].Price == _lastTradePrice
                                    && trades[i].Volume == _lastTradeQty)
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
            }
            else // Tester, Optimizer
            {
                if (_lastTradeTime == DateTime.MinValue)
                {
                    newTrades = trades;
                    _lastTradeIdInTester = newTrades[newTrades.Count - 1].IdInTester;
                }
                else
                {
                    for (int i = trades.Count - 1; i < trades.Count; i--)
                    {
                        try
                        {
                            if (trades[i].IdInTester <= _lastTradeIdInTester)
                            {
                                break;
                            }

                            newTrades.Insert(0, trades[i]);
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

            if (_journal == null)
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
                    if(openPositions[i] == null)
                    {
                        continue;
                    }

                    if (openPositions[i].StopOrderIsActive == false &&
                        openPositions[i].ProfitOrderIsActive == false)
                    {
                        continue;
                    }

                    for (int i2 = 0; i < openPositions.Count && i2 < newTrades.Count; i2++)
                    {
                        if (openPositions[i] == null)
                        {
                            continue;
                        }

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

            if(_connector.EmulatorIsOn == true)
            {
                for (int i2 = 0; i2 < newTrades.Count; i2++)
                {
                    try
                    {
                        _connector.CheckEmulatorExecution(newTrades[i2].Price);
                    }
                    catch (Exception error)
                    {
                        SetNewLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }

            _lastTradeIndex = trades.Count;
            _lastTradeTime = newTrades[newTrades.Count - 1].Time;
            _lastTradeIdInTester = newTrades[newTrades.Count - 1].IdInTester;
            _lastTradeQty = newTrades[newTrades.Count - 1].Volume;
            _lastTradePrice = newTrades[newTrades.Count - 1].Price;

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
            if (_journal.SetNewMyTrade(trade) == false)
            {
                return;
            }

            if (MyTradeEvent != null)
            {
                MyTradeEvent(trade);
            }

            if (StartProgram == StartProgram.IsTester
                || StartProgram == StartProgram.IsOsOptimizer)
            { // назначаем трейду номер свечи в тестере и оптимизаторе
                List<Candle> candles = CandlesAll;

                if (candles != null && candles.Count > 0)
                {
                    if (Connector.MyServer.ServerType == ServerType.Tester)
                    {
                        TesterServer server = (TesterServer)Connector.MyServer;

                        if (server.TypeTesterData == TesterDataType.Candle)
                        {
                            trade.NumberCandleInTester = candles.Count;
                        }
                        else
                        {
                            trade.NumberCandleInTester = candles.Count - 1;
                        }
                    }
                    if (Connector.MyServer.ServerType == ServerType.Optimizer)
                    {
                        OptimizerServer server = (OptimizerServer)Connector.MyServer;

                        if (server.TypeTesterData == TesterDataType.Candle)
                        {
                            trade.NumberCandleInTester = candles.Count;
                        }
                        else
                        {
                            trade.NumberCandleInTester = candles.Count - 1;
                        }
                    }
                }
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
            if (_isDelete)
            {
                return;
            }
            Order orderInJournal = _journal.IsMyOrder(order);

            if (orderInJournal == null)
            {
                return;
            }
            _journal.SetNewOrder(order);
            _icebergMaker.SetNewOrder(order);

            if (OrderUpdateEvent != null)
            {
                OrderUpdateEvent(orderInJournal);
            }
            _chartMaster.SetPosition(PositionsAll);
        }

        /// <summary>
        /// An attempt to revoke the order ended in an error
        /// </summary>
        private void _connector_CancelOrderFailEvent(Order order)
        {
            if (_isDelete)
            {
                return;
            }

            Order orderInJournal = _journal.IsMyOrder(order);

            if (orderInJournal == null)
            {
                return;
            }

            if(CancelOrderFailEvent != null)
            {
                CancelOrderFailEvent(orderInJournal);
            }
        }

        /// <summary>
        /// Incoming new bid with ask
        /// </summary>
        private void _connector_BestBidAskChangeEvent(decimal bid, decimal ask)
        {
            if (_isDelete)
            {
                return;
            }
            _journal?.SetNewBidAsk(bid, ask);
            _marketDepthPainter?.ProcessBidAsk(bid, ask);
            BestBidAskChangeEvent?.Invoke(bid, ask);
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

        private void _chartMaster_IndicatorManuallyCreateEvent(IIndicator newIndicator)
        {
            if (IndicatorManuallyCreateEvent != null)
            {
                IndicatorManuallyCreateEvent(newIndicator, this);
            }
        }

        private void _chartMaster_IndicatorManuallyDeleteEvent(IIndicator indicator)
        {
            if (IndicatorManuallyDeleteEvent != null)
            {
                IndicatorManuallyDeleteEvent(indicator, this);
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

                    if (MainWindow.ProccesIsWorked == false)
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

        private ConcurrentQueue<PositionAwaitMyTradesToSendEvent> _positionsAwaitSendInEventsQueue
            = new ConcurrentQueue<PositionAwaitMyTradesToSendEvent>();

        private List<PositionAwaitMyTradesToSendEvent> _positionsAwaitSendInEventsList
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
                           (curPos.Position.OpenOrders[^1] != null 
                            && curPos.Position.OpenOrders[^1].MyTrades != null
                            && curPos.Position.OpenOrders[^1].MyTrades.Count > 0))
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
                           (curPos.Position.CloseOrders[^1] != null 
                                &&  curPos.Position.CloseOrders[^1].MyTrades != null
                                && curPos.Position.CloseOrders[^1].MyTrades.Count > 0))
                        {
                            try
                            {
                                SetNewLogMessage(
                                OsLocalization.Trader.Label408
                                + ", " + OsLocalization.Trader.Label409 + ": " + NameStrategy + "\n"
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
            catch (Exception error)
            {
                SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _connector_NewVolume24hChangedEvent(SecurityVolumes data)
        {
            _securityVolumes.SecurityNameCode = data.SecurityNameCode;
            _securityVolumes.Volume24h = data.Volume24h;
            _securityVolumes.Volume24hUSDT = data.Volume24hUSDT;
            _securityVolumes.TimeUpdate = data.TimeUpdate;
        }

        private void _connector_FundingChangedEvent(Funding data)
        {
            _funding.SecurityNameCode = data.SecurityNameCode;
            _funding.CurrentValue = data.CurrentValue;
            _funding.NextFundingTime = data.NextFundingTime;
            _funding.FundingIntervalHours = data.FundingIntervalHours;
            _funding.MaxFundingRate = data.MaxFundingRate;
            _funding.MinFundingRate = data.MinFundingRate;
            _funding.TimeUpdate = data.TimeUpdate;
        }

        /// <summary>
        /// Data of Funding
        /// </summary>
        public Funding Funding
        {
            get { return _funding; }
        }

        private Funding _funding = new Funding();

        /// <summary>
        /// Volume24h
        /// </summary>
        public SecurityVolumes SecurityVolumes
        {
            get { return _securityVolumes; }
        }

        private SecurityVolumes _securityVolumes = new SecurityVolumes();

        // Outgoing events. Handlers for strategy

        /// <summary>
        /// My new trade event
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// Updated order
        /// </summary>
        public event Action<Order> OrderUpdateEvent;

        /// <summary>
        /// An attempt to revoke the order ended in an error
        /// </summary>
        public event Action<Order> CancelOrderFailEvent;

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
        /// The morning session started. Send the first trades
        /// </summary>
        public event Action<Trade> FirstTickToDayEvent;

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

        /// <summary>
        /// The robot is removed from the system
        /// </summary>
        public event Action<int> DeleteBotEvent;

        public event Action<IIndicator, BotTabSimple> IndicatorManuallyCreateEvent;

        public event Action<IIndicator, BotTabSimple> IndicatorManuallyDeleteEvent;
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
            if (PositionNeedToStopSend != null)
            {
                PositionNeedToStopSend(Position);
            }
        }

        public event Action<Position> PositionNeedToStopSend;
    }

    public class PositionAwaitMyTradesToSendEvent
    {
        public Position Position;

        public DateTime TimeForcibleRemoval;

        public PositionStateType StateAwaitToSend;
    }
}
