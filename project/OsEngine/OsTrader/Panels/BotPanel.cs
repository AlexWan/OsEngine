/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Attributes;
using OsEngine.Journal.Internal;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.RiskManager;

namespace OsEngine.OsTrader.Panels
{
    /// <summary>
    /// types of sources for the robot 
    /// </summary>
    public enum BotTabType
    {
        /// <summary>
        /// source for trading one security
        /// </summary>
        Simple,

        /// <summary>
        /// source for index creation
        /// </summary>
        Index,

        /// <summary>
        /// source for creating and displaying a cluster chart
        /// </summary>
        Cluster,

        /// <summary>
        /// source for trading multiple securities
        /// </summary>
        Screener,

        /// <summary>
        ///  source for trading pairs
        /// </summary>
        Pair,

        /// <summary>
        /// source for trading currency arbitrage
        /// </summary>
        Polygon,

        /// <summary>
        ///  source for the news feed
        /// </summary>
        News,

        /// <summary>
        /// source for options trading
        /// </summary>
        Options
    }

    /// <summary>
    /// parent for all robots in the program
    /// </summary>
    public abstract class BotPanel
    {
        #region Service

        protected BotPanel(string name, StartProgram startProgram)
        {
            NameStrategyUniq = name;
            StartProgram = startProgram;

            ReloadTab();

            _riskManager = new RiskManager.RiskManager(NameStrategyUniq, startProgram);
            _riskManager.RiskManagerAlarmEvent += _riskManager_RiskManagerAlarmEvent;

            Log = new Log(name, startProgram);
            Log.Listen(this);

            ParamGuiSettings = new ParamGuiSettings();
            ParamGuiSettings.LogMessageEvent += SendNewLogMessage;

            OsTraderMaster.CriticalErrorEvent += OsTraderMaster_CriticalErrorEvent;

	    AttributeInitializer attributeInitializer = new(this);
            attributeInitializer.InitAttributes();
        }

        private void OsTraderMaster_CriticalErrorEvent()
        {
            new Thread(() =>
            {
                Thread.Sleep(20000);
                try
                {
                    if (CriticalErrorEvent != null)
                    {
                        CriticalErrorEvent(CriticalErrorHandler.ErrorMessage);
                    }

                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.Message, LogMessageType.Error);
                }
            }).Start();

        }

        protected event Action<string> CriticalErrorEvent;

        public void Clear()
        {
            try
            {
                if (_botTabs == null
                || _botTabs.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < _botTabs.Count; i++)
                {
                    _botTabs[i].Clear();
                }

                Log?.Clear();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Delete()
        {
            try
            {
                try
                {
                    _chartUi?.Close();
                }
                catch
                {
                    // ignore
                }

                OsTraderMaster.CriticalErrorEvent -= OsTraderMaster_CriticalErrorEvent;

                if (_riskManager != null)
                {
                    _riskManager.RiskManagerAlarmEvent -= _riskManager_RiskManagerAlarmEvent;
                    _riskManager.Delete();
                    _riskManager = null;
                }

                if (_botTabs != null)
                {
                    for (int i = 0; i < _botTabs.Count; i++)
                    {
                        _botTabs[i].StopPaint();
                        _botTabs[i].Clear();
                        _botTabs[i].Delete();
                        _botTabs[i].LogMessageEvent -= SendNewLogMessage;
                    }
                    _botTabs.Clear();
                    _botTabs = null;
                }

                if (ParamGuiSettings != null)
                {
                    ParamGuiSettings.LogMessageEvent -= SendNewLogMessage;
                    ParamGuiSettings = null;

                    if (File.Exists(@"Engine\" + NameStrategyUniq + @"Parametrs.txt"))
                    {
                        File.Delete(@"Engine\" + NameStrategyUniq + @"Parametrs.txt");
                    }
                }

                if (Log != null)
                {
                    Log.Delete();
                    Log = null;
                }

                if (Parameters != null)
                {
                    for (int i = 0; i < Parameters.Count; i++)
                    {
                        Parameters[i].ValueChange -= Parameter_ValueChange;
                    }
                    Parameters.Clear();
                    Parameters = null;
                }

                if (_tabBotTab != null)
                {
                    _tabBotTab.SelectionChanged -= _tabBotTab_SelectionChanged;
                    _tabBotTab = null;
                }

                _gridChart = null;
                _hostChart = null;
                _hostGlass = null;
                _hostOpenDeals = null;
                _hostCloseDeals = null;
                _rectangle = null;
                _hostAlerts = null;
                _textBoxLimitPrice = null;
                _gridChartControlPanel = null;
                _tabControlControl = null;

                if (DeleteEvent != null)
                {
                    try
                    {
                        DeleteEvent();
                    }
                    catch (Exception ex)
                    {
                        SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Settings and Properties

        /// <summary>
        /// unique robot name
        /// </summary>
        public string NameStrategyUniq;

        /// <summary>
        /// bot class name
        /// </summary>
        public virtual string GetNameStrategyType() => GetType().Name;

        /// <summary>
        /// file name if it is a robot from the file system
        /// </summary>
        public string FileName;

        /// <summary>
        /// the name the user wants to see in the interface
        /// </summary>
        public string PublicName;

        /// <summary>
        /// the program that launched the robot. Tester  Robot  Optimizer
        /// </summary>
        public StartProgram StartProgram;

        /// <summary>
        /// indicates if the robot is an included script
        /// </summary>
        public bool IsScript;

        /// <summary>
        /// a description of the robot's operating logic. Displayed in the menu for selecting a robot to create
        /// </summary>
        public string Description;

        /// <summary>
        /// take logs panel  
        /// </summary>
        public List<Journal.Journal> GetJournals()
        {
            List<Journal.Journal> journals = new List<Journal.Journal>();

            for (int i = 0; _botTabs != null && i < _botTabs.Count; i++)
            {
                if (_botTabs[i].TabType == BotTabType.Simple)
                {
                    journals.Add(((BotTabSimple)_botTabs[i]).GetJournal());
                }
                else if (_botTabs[i].TabType == BotTabType.Screener)
                {
                    List<Journal.Journal> journalsOnTab = ((BotTabScreener)_botTabs[i]).GetJournals();

                    if (journalsOnTab == null ||
                        journalsOnTab.Count == 0)
                    {
                        continue;
                    }

                    journals.AddRange(journalsOnTab);
                }
                else if (_botTabs[i].TabType == BotTabType.Pair)
                {
                    List<Journal.Journal> journalsOnTab = ((BotTabPair)_botTabs[i]).GetJournals();

                    if (journalsOnTab == null ||
                        journalsOnTab.Count == 0)
                    {
                        continue;
                    }

                    journals.AddRange(journalsOnTab);
                }
                else if (_botTabs[i].TabType == BotTabType.Polygon)
                {
                    List<Journal.Journal> journalsOnTab = ((BotTabPolygon)_botTabs[i]).GetJournals();

                    if (journalsOnTab == null ||
                        journalsOnTab.Count == 0)
                    {
                        continue;
                    }

                    journals.AddRange(journalsOnTab);
                }
            }

            return journals;
        }

        /// <summary>
        /// has the robot connected to the exchange of all tabs
        /// </summary>
        public bool IsConnected
        {
            get
            {
                for (int i = 0; TabsSimple != null && i < TabsSimple.Count; i++)
                {
                    if (TabsSimple[i].IsConnected == false)
                    {
                        return false;
                    }
                }

                for (int i = 0; TabsScreener != null && i < TabsScreener.Count; i++)
                {
                    if (TabsScreener[i].IsConnected == false)
                    {
                        return false;
                    }
                }

                for (int i = 0; TabsIndex != null && i < TabsIndex.Count; i++)
                {
                    if (TabsIndex[i].IsConnected == false)
                    {
                        return false;
                    }
                }

                if (TabsSimple == null &&
                    TabsIndex == null)
                {
                    return false;
                }

                return true;
            }
        }

        public List<Security> GetSecuritiesInTradeSources()
        {
            try
            {
                if (_botTabs == null
                   || _botTabs.Count == 0)
                {
                    return null;
                }

                List<Security> securities = new List<Security>();

                for (int i = 0; i < _botTabs.Count; i++)
                {
                    if (_botTabs[i].TabType == BotTabType.Simple)
                    {
                        BotTabSimple tab = (BotTabSimple)_botTabs[i];

                        if(tab.Security != null)
                        {
                            securities.Add(tab.Security);
                        }
                    }
                    if (_botTabs[i].TabType == BotTabType.Screener)
                    {
                        BotTabScreener tab = (BotTabScreener)_botTabs[i];

                        List<BotTabSimple> tabs = tab.Tabs;

                        for (int j = 0;j < tabs.Count; j++)
                        {
                            if (tabs[j].Security != null)
                            {
                                securities.Add(tabs[j].Security);
                            }
                        }
                    }
                }

                return securities;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public List<Position> GetPositionsBySecurity(Security security)
        {
            List<Position> result = new List<Position>();

            List<Position> openPoses = OpenPositions;

            for(int i = 0;i < openPoses.Count;i++)
            {
                Position position = openPoses[i];

                string nameInPosition = position.SecurityName.Replace(" TestPaper", "");

                if (nameInPosition == security.Name
                    || nameInPosition == security.Name + "_LONG"
                    || nameInPosition == security.Name + "_SHORT"
                    || nameInPosition == security.Name + "_Long"
                    || nameInPosition == security.Name + "_Short"
                    || nameInPosition == security.Name + "_long"
                    || nameInPosition == security.Name + "_short"
                    )
                {
                    result.Add(position);
                }
            }

            return result;
        }

        public Portfolio GetFirstPortfolio()
        {
            Portfolio portfolio = null;

            for(int i = 0;_botTabs != null && i < _botTabs.Count;i++)
            {
                IIBotTab tab = _botTabs[i];

                if(tab.TabType == BotTabType.Simple)
                {
                    BotTabSimple simple = (BotTabSimple)tab;

                    if(simple.Portfolio != null)
                    {
                        portfolio = simple.Portfolio;
                        break;
                    }
                }
                else if (tab.TabType == BotTabType.Screener)
                {
                    BotTabScreener screener = (BotTabScreener)tab;

                    for(int j = 0;j < screener.Tabs.Count;j++)
                    {
                        if (screener.Tabs[j].Portfolio != null)
                        {
                            portfolio = screener.Tabs[j].Portfolio;
                            break;
                        }
                    }

                    if(portfolio != null)
                    {
                        break;
                    }
                }
                else if (tab.TabType == BotTabType.Pair)
                {
                    BotTabPair pair = (BotTabPair)tab;

                    for (int j = 0; j < pair.Pairs.Count; j++)
                    {
                        if (pair.Pairs[j].Tab1.Portfolio != null)
                        {
                            portfolio = pair.Pairs[j].Tab1.Portfolio;
                            break;
                        }
                        if (pair.Pairs[j].Tab2.Portfolio != null)
                        {
                            portfolio = pair.Pairs[j].Tab2.Portfolio;
                            break;
                        }
                    }

                    if (portfolio != null)
                    {
                        break;
                    }
                }
            }

            return portfolio;
        }

        #endregion

        #region Chart and GUI

        /// <summary>
        /// start drawing this robot
        /// </summary> 
        public void StartPaint(Grid gridChart, WindowsFormsHost hostChart, WindowsFormsHost glass,
            WindowsFormsHost hostOpenDeals, WindowsFormsHost hostCloseDeals,
            WindowsFormsHost boxLog, Rectangle rectangle, WindowsFormsHost hostAlerts,
            TabControl tabBotTab, TextBox textBoxLimitPrice, Grid gridChartControlPanel,
            TextBox textBoxVolume, TabControl tabControlControl, WindowsFormsHost hostGrids)
        {
            if (_isPainting)
            {
                return;
            }

            _gridChart = gridChart;
            _tabBotTab = tabBotTab;
            _hostChart = hostChart;
            _hostGlass = glass;
            _hostOpenDeals = hostOpenDeals;
            _hostCloseDeals = hostCloseDeals;
            _rectangle = rectangle;
            _hostAlerts = hostAlerts;
            _textBoxLimitPrice = textBoxLimitPrice;
            _gridChartControlPanel = gridChartControlPanel;
            _textBoxVolume = textBoxVolume;
            _tabControlControl = tabControlControl;

            if (_tabControlControl != null)
            {
                _tabControlControl.SelectionChanged += _tabControlControl_SelectionChanged;
            }

            _hostGrids = hostGrids;

            try
            {
                if (_tabBotTab == null)
                {
                    return;
                }

                if (!_tabBotTab.Dispatcher.CheckAccess())
                {
                    _tabBotTab.Dispatcher.Invoke(new Action<Grid, WindowsFormsHost, WindowsFormsHost, WindowsFormsHost,
                    WindowsFormsHost, WindowsFormsHost, Rectangle, WindowsFormsHost, TabControl, TextBox,
                    Grid, TextBox, TabControl, WindowsFormsHost>
                    (StartPaint), gridChart, hostChart, glass, hostOpenDeals, hostCloseDeals,
                    boxLog, rectangle, hostAlerts, tabBotTab, textBoxLimitPrice,
                    gridChartControlPanel, textBoxVolume, tabControlControl, hostGrids);
                    return;
                }

                Log.StartPaint(boxLog);

                _isPainting = true;

                ReloadTab();

                if (ActiveTab != null)
                {
                    ChangeActiveTab(ActiveTab.TabNum);
                }
                else
                {
                    if (_tabBotTab != null
                        && _tabBotTab.Items.Count != 0
                        && _tabBotTab.SelectedItem != null)
                    {
                        ChangeActiveTab(_tabBotTab.SelectedIndex);
                    }
                    else if (_tabBotTab != null
                             && _tabBotTab.Items.Count != 0
                             && _tabBotTab.SelectedItem == null)
                    {
                        ChangeActiveTab(0);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// stop drawing this robot
        /// </summary>
        public void StopPaint()
        {
            if (_isPainting == false)
            {
                return;
            }
            try
            {
                if (!_tabBotTab.Dispatcher.CheckAccess())
                {
                    _tabBotTab.Dispatcher.Invoke(StopPaint);
                    return;
                }

                for (int i = 0; _botTabs != null && i < _botTabs.Count; i++)
                {
                    _botTabs[i].StopPaint();
                }

                try
                {
                    Log.StopPaint();
                }
                catch (Exception error)
                {
                    LogMessageEvent(error.ToString(), LogMessageType.Error);
                }

                if (_tabBotTab != null)
                {
                    _tabBotTab.SelectionChanged -= _tabBotTab_SelectionChanged;
                }

                _tabBotTab = null;
                _hostChart = null;
                _hostGlass = null;
                _hostOpenDeals = null;
                _hostCloseDeals = null;
                _rectangle = null;
                _hostAlerts = null;
                _textBoxLimitPrice = null;
                _gridChartControlPanel = null;
                _textBoxVolume = null;
                _hostGrids = null;

                if (_tabControlControl != null)
                {
                    _tabControlControl.SelectionChanged -= _tabControlControl_SelectionChanged;
                    _tabControlControl = null;
                }

                _isPainting = false;
                ReloadTab();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool _isPainting;

        public void MoveChartToTheRight()
        {
            if (ActiveTab is BotTabSimple botTab)
            {
                botTab.MoveChartToTheRight();
            }
        }

        private Grid _gridChart;
        private WindowsFormsHost _hostChart;
        private WindowsFormsHost _hostGlass;
        private WindowsFormsHost _hostOpenDeals;
        private WindowsFormsHost _hostCloseDeals;
        private Rectangle _rectangle;
        private WindowsFormsHost _hostAlerts;
        private TextBox _textBoxLimitPrice;
        private TextBox _textBoxVolume;
        private Grid _gridChartControlPanel;
        private TabControl _tabControlControl;
        private WindowsFormsHost _hostGrids;

        /// <summary>
        /// show the chart window with deals
        /// </summary>
        public BotPanelChartUi ShowChartDialog()
        {
            if (_chartUi == null)
            {
                _chartUi = new BotPanelChartUi(this);
                _chartUi.Show();
                _chartUi.Closed += _chartUi_Closed;
            }
            else
            {
                if (_chartUi.WindowState == WindowState.Minimized)
                {
                    _chartUi.WindowState = WindowState.Normal;
                }
                _chartUi.Activate();
                _chartUi.Focus();
            }

            return _chartUi;
        }

        public BotPanelChartUi _chartUi;

        public void CloseGui()
        {
            try
            {
                if (_chartUi == null)
                {
                    return;
                }

                if (_chartUi.Dispatcher.CheckAccess() == false)
                {
                    _chartUi.Dispatcher.Invoke(CloseGui);
                    return;
                }

                _chartUi.Close();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _chartUi_Closed(object sender, EventArgs e)
        {
            _chartUi.Closed -= _chartUi_Closed;
            _chartUi = null;

            ChartClosedEvent?.Invoke(this.NameStrategyUniq);
        }

        public event Action<string> ChartClosedEvent;

        #endregion

        #region Statistic

        /// <summary>
        /// total profit
        /// </summary>
        public decimal TotalProfitInPercent
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return 0;
                }

                decimal result = 0;

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i].AllPosition == null ||
                        journals[i].AllPosition.Count == 0)
                    {
                        continue;
                    }

                    List<Position> positions = journals[i].AllPosition.FindAll((
position => position.State != PositionStateType.OpeningFail
&& position.EntryPrice != 0 && position.ClosePrice != 0));

                    result += PositionStatisticGenerator.GetAllProfitPercent(positions.ToArray());
                }
                return result;
            }
        }

        /// <summary>
        /// total profit absolute
        /// </summary>
        public decimal TotalProfitAbs
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return 0;
                }

                decimal result = 0;

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i].AllPosition == null ||
                        journals[i].AllPosition.Count == 0)
                    {
                        continue;
                    }

                    List<Position> positions = journals[i].AllPosition.FindAll((
position => position.State != PositionStateType.OpeningFail
&& position.EntryPrice != 0 && position.ClosePrice != 0));

                    result += PositionStatisticGenerator.GetAllProfitInAbsolute(positions.ToArray());
                }
                return result;
            }
        }

        /// <summary>
        /// average profit from the transaction
        /// </summary>
        public decimal MiddleProfitInPercent
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return 0;
                }

                decimal result = 0;

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i].AllPosition == null ||
                        journals[i].AllPosition.Count == 0)
                    {
                        continue;
                    }

                    List<Position> positions = journals[i].AllPosition.FindAll((
                    position => position.State != PositionStateType.OpeningFail
                    && position.EntryPrice != 0 && position.ClosePrice != 0));

                    result += PositionStatisticGenerator.GetMiddleProfitInPercentOneContract(positions.ToArray());
                }
                return result;
            }
        }

        /// <summary>
        /// profit factor
        /// </summary>
        public decimal ProfitFactor
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return 0;
                }

                decimal result = 0;

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i].AllPosition == null ||
                        journals[i].AllPosition.Count == 0)
                    {
                        continue;
                    }
                    result += PositionStatisticGenerator.GetProfitFactor(journals[i].AllPosition.ToArray());
                }
                return result;
            }
        }

        /// <summary>
        /// maximum drawdown
        /// </summary>
        public decimal MaxDrawDown
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return 0;
                }

                decimal result = 0;

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i].AllPosition == null ||
                        journals[i].AllPosition.Count == 0)
                    {
                        continue;
                    }
                    result += PositionStatisticGenerator.GetMaxDownPercent(journals[i].AllPosition.ToArray());
                }
                return result;
            }
        }

        /// <summary>
        /// profit position count
        /// </summary>
        public decimal WinPositionPercent
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return 0;
                }

                decimal winPoses = 0;

                decimal allPoses = 0;

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i].AllPosition == null ||
                        journals[i].AllPosition.Count == 0)
                    {
                        continue;
                    }

                    allPoses += journals[i].AllPosition.Count;
                    List<Position> winPositions = journals[i].AllPosition.FindAll(pos => pos.ProfitOperationAbs > 0);
                    winPoses += (winPositions.Count);
                }
                return winPoses / allPoses;
            }
        }

        /// <summary>
        /// the number of positions at the tabs of the robot
        /// </summary>
        public int PositionsCount
        {
            get
            {

                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return 0;
                }

                List<Position> pos = new List<Position>();

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i] == null)
                    {
                        continue;
                    }
                    if (journals[i].OpenPositions == null ||
                        journals[i].OpenPositions.Count == 0)
                    {
                        continue;
                    }
                    pos.AddRange(journals[i].OpenPositions);
                }
                return pos.Count;
            }
        }

        /// <summary>
        /// the number of all positions at the tabs of the robot
        /// </summary>
        public int AllPositionsCount
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null || journals.Count == 0)
                {
                    return 0;
                }

                List<Position> pos = new List<Position>();

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i] == null)
                    {
                        continue;
                    }
                    if (journals[i].AllPosition == null || journals[i].AllPosition.Count == 0)
                    {
                        continue;
                    }

                    List<Position> allPositionOpen = new List<Position>();

                    for(int i2 = 0;i2 < journals[i].AllPosition.Count;i2++)
                    {
                        Position position = journals[i].AllPosition[i2];

                        if (position == null)
                        {
                            continue;
                        }

                        if (position.State == PositionStateType.OpeningFail)
                        {
                            continue;
                        }
                        allPositionOpen.Add(position);
                    }

                    if (allPositionOpen == null || allPositionOpen.Count == 0)
                    {
                        continue;
                    }

                    pos.AddRange(allPositionOpen);
                }
                return pos.Count;
            }
        }

        /// <summary>
        /// open positions on robot sources
        /// </summary>
        public List<Position> OpenPositions
        {
            get
            {
                List<Position> result = new List<Position>();

                List<Journal.Journal> journals = GetJournals();

                if (journals == null ||
                    journals.Count == 0)
                {
                    return result;
                }

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i] == null)
                    {
                        continue;
                    }
                    if (journals[i].OpenPositions == null ||
                        journals[i].OpenPositions.Count == 0)
                    {
                        continue;
                    }
                    result.AddRange(journals[i].OpenPositions);
                }
                return result;
            }
        }

        /// <summary>
        /// number of long positions on the robot tabs
        /// </summary>
        public int AllPositionsLongCount
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null
                    || journals.Count == 0)
                {
                    return 0;
                }

                List<Position> pos = new List<Position>();

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i] == null)
                    {
                        continue;
                    }
                    if (journals[i].OpenAllLongPositions == null
                        || journals[i].OpenAllLongPositions.Count == 0)
                    {
                        continue;
                    }
                    pos.AddRange(journals[i].OpenAllLongPositions);
                }
                return pos.Count;
            }
        }

        /// <summary>
        /// number of short positions on the robot tabs
        /// </summary>
        public int AllPositionsShortCount
        {
            get
            {
                List<Journal.Journal> journals = GetJournals();

                if (journals == null
                    || journals.Count == 0)
                {
                    return 0;
                }

                List<Position> pos = new List<Position>();

                for (int i = 0; i < journals.Count; i++)
                {
                    if (journals[i] == null)
                    {
                        continue;
                    }
                    if (journals[i].OpenAllShortPositions == null
                        || journals[i].OpenAllShortPositions.Count == 0)
                    {
                        continue;
                    }
                    pos.AddRange(journals[i].OpenAllShortPositions);
                }
                return pos.Count;
            }
        }

        #endregion

        #region Parameters

        /// <summary>
        /// show parameter settings window
        /// </summary>
        public void ShowParameterDialog()
        {
            if (Parameters == null ||
                Parameters.Count == 0)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label51);
                ui.ShowDialog();
                return;
            }

            if (_parametersUi == null)
            {
                _parametersUi = new StrategyParametersUi(Parameters, ParamGuiSettings, this);
                _parametersUi.Show();
                _parametersUi.Closing += _parametersUi_Closing;
            }
            else
            {
                if (_parametersUi.WindowState == WindowState.Minimized)
                {
                    _parametersUi.WindowState = WindowState.Normal;
                }

                _parametersUi.Activate();
            }
        }

        private void _parametersUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _parametersUi.Closing -= _parametersUi_Closing;
            _parametersUi = null;
        }

        /// <summary>
        /// parameters window
        /// </summary>
        private StrategyParametersUi _parametersUi;

        /// <summary>
        /// close the options window
        /// </summary>
        public void CloseParameterDialog() => _parametersUi?.Close();

        /// <summary>
        /// Whether the parameter window is open for display. True - is open
        /// </summary>
        public bool ParamGuiIsOpen => _parametersUi != null;

        /// <summary>       
        /// Gui Settings
        /// </summary>
        public ParamGuiSettings ParamGuiSettings;

        /// <summary>
        /// create a Decimal type parameter
        /// </summary>
        /// <param name="name">parameter name </param>
        /// <param name="value">default value </param>
        /// <param name="start">first value </param>
        /// <param name="stop">last value </param>
        /// <param name="step">value step </param>
        /// <param name="tabName">name of the tab in the parameter window </param>
        public StrategyParameterDecimal CreateParameter(string name, decimal value, decimal start, decimal stop, decimal step, string tabControlName = null)
        {
            StrategyParameterDecimal newParameter = new StrategyParameterDecimal(name, value, start, stop, step, tabControlName); 

            if (Parameters.Find(p => p.Name == name) != null)
            {
                throw new Exception(OsLocalization.Trader.Label52);
            }

            return (StrategyParameterDecimal)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// create a TimeOfDay type parameter
        /// </summary>
        /// <param name="name">parameter name </param>
        /// <param name="value">default value </param>
        /// <param name="start">first value </param>
        /// <param name="stop">last value </param>
        /// <param name="step">value step </param>
        public StrategyParameterTimeOfDay CreateParameterTimeOfDay(string name, int hour, int minute, int second, int millisecond, string tabControlName = null)
        {
            StrategyParameterTimeOfDay newParameter =
                new StrategyParameterTimeOfDay(name, hour, minute, second, millisecond, tabControlName);

            if (Parameters.Find(p => p.Name == name) != null)
            {
                throw new Exception(OsLocalization.Trader.Label52);
            }

            return (StrategyParameterTimeOfDay)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// create int parameter
        /// </summary>
        /// <param name="name">parameter name </param>
        /// <param name="value">default value </param>
        /// <param name="start">first value </param>
        /// <param name="stop">last value </param>
        /// <param name="step">value step </param>
        public StrategyParameterInt CreateParameter(string name, int value, int start, int stop, int step, string tabControlName = null)
        {
            StrategyParameterInt newParameter = new StrategyParameterInt(name, value, start, stop, step, tabControlName);

            if (Parameters.Find(p => p.Name == name) != null)
            {
                throw new Exception(OsLocalization.Trader.Label52);
            }

            return (StrategyParameterInt)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// create string parameter
        /// </summary>
        /// <param name="name">parameter name </param>
        /// <param name="value">default value </param>
        /// <param name="collection">values </param>
        public StrategyParameterString CreateParameter(string name, string value, string[] collection, string tabControlName = null)
        {
            if (Parameters.Find(p => p.Name == name) != null)
            {
                throw new Exception(OsLocalization.Trader.Label52);
            }

            bool isInArray = false;

            for (int i = 0; i < collection.Length; i++)
            {
                if (collection[i] == value)
                {
                    isInArray = true;
                    break;
                }
            }

            if (isInArray == false)
            {
                List<string> col = collection.ToList();
                col.Add(value);
                collection = col.ToArray();
            }

            StrategyParameterString newParameter = new StrategyParameterString(name, value, collection.ToList(), tabControlName);

            StrategyParameterString paramFromFileSys = (StrategyParameterString)LoadParameterValues(newParameter);

            if(paramFromFileSys.ValuesString != null &&
                collection != null)
            {// проверяем, чтобы программист не изменил названия для коллекции
                if(paramFromFileSys.ValuesString.Count != collection.Length)
                {
                    paramFromFileSys.ValuesString = collection.ToList();
                }
                else
                {
                    for (int i = 0; i < collection.Length; i++)
                    {
                        if (collection[i] != paramFromFileSys.ValuesString[i])
                        {
                            paramFromFileSys.ValuesString = collection.ToList();
                            break;
                        }
                    }
                }
            }

            return paramFromFileSys;
        }

        /// <summary>
        /// create string parameter 
        /// </summary>
        /// <param name="name">parameter name </param>
        /// <param name="value">default value </param>
        public StrategyParameterString CreateParameter(string name, string value, string tabControlName = null)
        {
            StrategyParameterString newParameter = new StrategyParameterString(name, value, tabControlName);

            if (Parameters.Find(p => p.Name == name) != null)
            {
                throw new Exception(OsLocalization.Trader.Label52);
            }

            return (StrategyParameterString)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// create bool type parameter 
        /// </summary>
        /// <param name="name">parameter name </param>
        /// <param name="value">default value </param>
        public StrategyParameterBool CreateParameter(string name, bool value, string tabControlName = null)
        {
            StrategyParameterBool newParameter = new StrategyParameterBool(name, value, tabControlName);

            if (Parameters.Find(p => p.Name == name) != null)
            {
                throw new Exception(OsLocalization.Trader.Label52);
            }

            return (StrategyParameterBool)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// create button type parameter
        /// </summary>
        /// <param name="buttonLabel">label for button</param>
        public StrategyParameterButton CreateParameterButton(string buttonLabel, string tabControlName = null)
        {
            StrategyParameterButton newParameter = new StrategyParameterButton(buttonLabel, tabControlName);

            if (Parameters.Find(p => p.Name == buttonLabel) != null)
            {
                throw new Exception(OsLocalization.Trader.Label52);
            }

            return (StrategyParameterButton)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// create checkbox type parameter
        /// </summary>
        public StrategyParameterCheckBox CreateParameterCheckBox(string checkBoxLabel, bool isChecked, string tabControlName = null)
        {
            StrategyParameterCheckBox newParameter = new StrategyParameterCheckBox(checkBoxLabel, isChecked, tabControlName);

            if (Parameters.Find(p => p.Name == checkBoxLabel) != null)
            {
                throw new Exception(OsLocalization.Trader.Label52);
            }

            return (StrategyParameterCheckBox)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// create a Decimal type parameter with CheckBox
        /// </summary>
        public StrategyParameterDecimalCheckBox CreateParameterDecimalCheckBox(string name, decimal value, decimal start, decimal stop, decimal step, bool isChecked, string tabControlName = null)
        {
            StrategyParameterDecimalCheckBox newParameter = new StrategyParameterDecimalCheckBox(name, value, start, stop, step, isChecked, tabControlName);

            if (Parameters.Find(p => p.Name == name) != null)
            {
                throw new Exception(OsLocalization.Trader.Label52);
            }

            return (StrategyParameterDecimalCheckBox)LoadParameterValues(newParameter);
        }

        /// <summary>
        /// create label type parameter
        /// </summary>
        public StrategyParameterLabel CreateParameterLabel(string name, string label, string value, int rowHeight, int textHeight, System.Drawing.Color color, string tabControlName = null)
        {
            StrategyParameterLabel newParameter = new StrategyParameterLabel(name, label, value, rowHeight, textHeight, color, tabControlName);

            if (Parameters.Find(p => p.Name == name) != null)
            {
                throw new Exception(OsLocalization.Trader.Label52);
            }

            return (StrategyParameterLabel)LoadParameterValues(newParameter);
        }

        private DateTime _lastParamLoadTime = DateTime.MinValue;

        /// <summary>
        /// load parameter settings
        /// </summary>
        /// <param name="newParameter">setting parameter you want to load </param>
        private IIStrategyParameter LoadParameterValues(IIStrategyParameter newParameter)
        {
            if (StartProgram != StartProgram.IsOsOptimizer)
            {
                _lastParamLoadTime = DateTime.Now;
                GetValueParameterSaveByUser(newParameter);
            }

            newParameter.ValueChange += Parameter_ValueChange;

            Parameters.Add(newParameter);

            return newParameter;
        }

        /// <summary>
        /// load parameter settings from file
        /// </summary>
        private void GetValueParameterSaveByUser(IIStrategyParameter parameter)
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"Parametrs.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"Parametrs.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        string[] save = reader.ReadLine().Split('#');

                        if (save[0] == parameter.Name)
                        {
                            parameter.LoadParamFromString(save);
                        }
                    }
                    reader.Close();
                }
            }
            catch (Exception error)
            {
                LogMessageEvent(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the list of options available in the panel
        /// </summary>
        public List<IIStrategyParameter> Parameters { get; private set; } = new();

        /// <summary>
        /// parameter has changed settings
        /// </summary>
        private void Parameter_ValueChange()
        {
            if (StartProgram != StartProgram.IsOsOptimizer)
            {
                SaveParameters();
            }

            try
            {
                if (ParametrsChangeByUser != null)
                {
                    ParametrsChangeByUser();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// save parameter values
        /// </summary>
        public void SaveParameters()
        {
            if (_lastParamLoadTime.AddSeconds(3) > DateTime.Now)
            {
                return;
            }

            if (Parameters == null ||
                Parameters.Count == 0)
            {
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"Parametrs.txt", false)
                    )
                {
                    for (int i = 0; i < Parameters.Count; i++)
                    {
                        writer.WriteLine(Parameters[i].GetStringToSave());
                    }

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                LogMessageEvent(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// parameter has changed state
        /// </summary>
        public event Action ParametrsChangeByUser;

        #endregion

        #region Risk manager

        /// <summary>
        /// risk manager
        /// </summary>
        private RiskManager.RiskManager _riskManager;

        /// <summary>
        /// an alert came from a risk manager
        /// </summary>
        void _riskManager_RiskManagerAlarmEvent(RiskManagerReactionType reactionType)
        {
            try
            {
                if (reactionType == RiskManagerReactionType.CloseAndOff)
                {
                    CloseAndOffAllToMarket();
                }
                else if (reactionType == RiskManagerReactionType.ShowDialog)
                {
                    string message = OsLocalization.Trader.Label53;
                    ShowMessageInNewThread(message);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// draw a window with a message in a new thread
        /// </summary>
        private void ShowMessageInNewThread(string message)
        {
            try
            {
                if (!_hostChart.CheckAccess())
                {
                    _hostChart.Dispatcher.Invoke(new Action<string>(ShowMessageInNewThread), message);
                    return;
                }

                AlertMessageSimpleUi ui = new AlertMessageSimpleUi(message);
                ui.Show();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// emergency closing of all positions. And Off robots
        /// </summary>
        public void CloseAndOffAllToMarket()
        {
            try
            {
                string message = OsLocalization.Trader.Label54 + NameStrategyUniq;
                ShowMessageInNewThread(message);

                for (int i = 0; i < _botTabs.Count; i++)
                {
                    if (_botTabs[i] is BotTabSimple bot)
                    {
                        bot.CloseAllAtMarket();
                        bot.EventsIsOn = false;

                        if (bot.Connector.ServerType == ServerType.Tester)
                        {
                            List<IServer> allServers = ServerMaster.GetServers();
                            TesterServer testServer = (TesterServer)allServers.Find(server => server.ServerType == ServerType.Tester);
                            testServer.TesterRegime = TesterRegime.Pause;
                        }

                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// emergency closing of all positions
        /// </summary>
        public void CloseAllToMarket()
        {
            try
            {
                for (int i = 0; i < _botTabs.Count; i++)
                {
                    if (_botTabs[i] is BotTabSimple bot)
                    {
                        bot.CloseAllAtMarket();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Tab management

        /// <summary>
        /// tabbed tabs
        /// </summary>
        private List<IIBotTab> _botTabs;

        /// <summary>
        /// get all tabs
        /// </summary>
        public List<IIBotTab> GetTabs() => _botTabs;

        /// <summary>
        /// active tab
        /// </summary>
        public IIBotTab ActiveTab;

        /// <summary>
        /// control which tabs are located
        /// </summary>
        private TabControl _tabBotTab;

        /// <summary>
        /// open tab number
        /// </summary>
        public int ActiveTabNumber
        {
            get
            {
                try
                {
                    if (ActiveTab == null
                        || _tabBotTab == null
                        || _tabBotTab.Items == null
                        || _tabBotTab.Items.Count == 0)
                    {
                        return -1;
                    }
                    if (_tabBotTab.SelectedItem != null)
                    {
                        return Convert.ToInt32(_tabBotTab.SelectedItem.ToString());
                    }
                    return 0;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return 0;
            }
        }

        /// <summary> 
        /// trade tabs 
        /// </summary>
        public List<BotTabSimple> TabsSimple
        {
            get
            {
                return _botTabs != null
                    ? _botTabs.OfType<BotTabSimple>().ToList()
                    : new();
            }
        }

        /// <summary>
        /// index tabs
        /// </summary>
        public List<BotTabIndex> TabsIndex
        {
            get
            {
                return _botTabs != null
                    ? _botTabs.OfType<BotTabIndex>().ToList()
                    : new();
            }
        }

        /// <summary>
        /// clustered tabs
        /// </summary>
        public List<BotTabCluster> TabsCluster
        {
            get
            {
                return _botTabs != null
                    ? _botTabs.OfType<BotTabCluster>().ToList()
                    : new();
            }
        }

        /// <summary>
        /// pair tabs
        /// </summary>
        public List<BotTabPair> TabsPair
        {
            get
            {
                return _botTabs != null
                    ? _botTabs.OfType<BotTabPair>().ToList()
                    : new();
            }
        }

        /// <summary>
        /// screener tabs
        /// </summary>
        public List<BotTabScreener> TabsScreener
        {
            get
            {
                return _botTabs != null
                    ? _botTabs.OfType<BotTabScreener>().ToList()
                    : new();
            }
        }

        /// <summary>
        /// polygon tabs
        /// </summary>
        public List<BotTabPolygon> TabsPolygon
        {
            get
            {
                return _botTabs != null
                    ? _botTabs.OfType<BotTabPolygon>().ToList()
                    : new();
            }
        }

        /// <summary>
        /// news tabs
        /// </summary>
        public List<BotTabNews> TabsNews
        {
            get
            {
                return _botTabs != null
                    ? _botTabs.OfType<BotTabNews>().ToList()
                    : new();
            }
        }

        public DateTime TimeServer
        {
            get
            {
                DateTime result = DateTime.MinValue;

                if (TabsSimple != null
                    && TabsSimple.Count > 0)
                {
                    for (int i = 0; i < TabsSimple.Count; i++)
                    {
                        if (TabsSimple[i].IsConnected == false)
                        {
                            continue;
                        }

                        if (TabsSimple[i].TimeServerCurrent > result)
                        {
                            result = TabsSimple[i].TimeServerCurrent;
                        }
                    }
                }

                if (TabsScreener != null
                    && TabsScreener.Count > 0)
                {
                    for (int i = 0; i < TabsScreener.Count; i++)
                    {
                        for (int j = 0; j < TabsScreener[i].Tabs.Count; j++)
                        {
                            BotTabSimple tab = TabsScreener[i].Tabs[j];

                            if (tab.IsConnected == false)
                            {
                                continue;
                            }

                            if (tab.TimeServerCurrent > result)
                            {
                                result = tab.TimeServerCurrent;
                            }
                        }
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// user toggled tabs
        /// </summary>
        private void _tabBotTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_tabBotTab != null && _tabBotTab.Items.Count != 0)
                {
                    ChangeActiveTab(_tabBotTab.SelectedIndex);
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _tabControlControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if(ActiveTab == null)
                {
                    return;
                }

                if(_tabControlControl == null)
                {
                    return;
                }

                if (ActiveTab.TabType == BotTabType.Simple)
                {
                   ((BotTabSimple)ActiveTab).SelectedControlTab = _tabControlControl.SelectedIndex;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// create tab
        /// </summary>
        public IIBotTab TabCreate(BotTabType tabType)
        {
            try
            {
                if (ValidateTabCreation(out int number, out string nameTab) == false)
                    return null;

                IIBotTab newTab;

                if (tabType == BotTabType.Simple)
                {
                    newTab = new BotTabSimple(nameTab, StartProgram);
                }
                else if (tabType == BotTabType.Index)
                {
                    newTab = new BotTabIndex(nameTab, StartProgram);
                }
                else if (tabType == BotTabType.Cluster)
                {
                    newTab = new BotTabCluster(nameTab, StartProgram);
                }
                else if (tabType == BotTabType.Pair)
                {
                    newTab = new BotTabPair(nameTab, StartProgram);

                    ((BotTabPair)newTab).UserSelectActionEvent += UserSetPositionAction;
                }
                else if (tabType == BotTabType.Polygon)
                {
                    newTab = new BotTabPolygon(nameTab, StartProgram);
                }
                else if (tabType == BotTabType.News)
                {
                    newTab = new BotTabNews(nameTab, StartProgram);
                }
                else if (tabType == BotTabType.Screener)
                {
                    newTab = new BotTabScreener(nameTab, StartProgram);

                    ((BotTabScreener)newTab).UserSelectActionEvent += UserSetPositionAction;
                    ((BotTabScreener)newTab).NewTabCreateEvent += (tab) => NewTabCreateEvent?.Invoke();
                }
                else if (tabType == BotTabType.Options)
                {
                    newTab = new BotTabOptions(nameTab, StartProgram);
                }
                else
                {
                    return null;
                }

                ActivateTab(newTab);
                return newTab;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        public T TabCreate<T>() where T : IIBotTab
        {
            try
            {
                if (ValidateTabCreation(out int number, out string nameTab) == false)
                    return default(T);

                var constructor = typeof(T).GetConstructor(new[] { typeof(string), typeof(StartProgram) });

                if (constructor == null)
                    throw new InvalidOperationException($"Type {typeof(T)} does not have a public constructor with parameters (string, StartProgram).");

                T newTab = (T)Activator.CreateInstance(typeof(T), nameTab, StartProgram);
                
                if(newTab is BotTabPair botTabPair)
                {
                    botTabPair.UserSelectActionEvent += UserSetPositionAction;
                }
                else if(newTab is BotTabScreener botTabScreener)
                {
                    botTabScreener.UserSelectActionEvent += UserSetPositionAction;
                    botTabScreener.NewTabCreateEvent += (tab) => NewTabCreateEvent?.Invoke();
                }

                ActivateTab(newTab);
                return newTab;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return default(T);
            }
        }

        private void ActivateTab(IIBotTab newTab)
        {
            _botTabs.Add(newTab);

            newTab.LogMessageEvent += SendNewLogMessage;
            newTab.TabNum = _botTabs.Count - 1;

            ChangeActiveTab(_botTabs.Count - 1);
            ReloadTab();

            NewTabCreateEvent?.Invoke();
        }

        private bool ValidateTabCreation(out int number, out string nameTab)
        {
             number = 0;

            if (_botTabs != null && _botTabs.Count != 0)
            {
                number = _botTabs.Count;
            }

            nameTab = $"{NameStrategyUniq}tab{number}";

            string name = nameTab;

            if (_botTabs != null && _botTabs.Find(strategy => strategy.TabName == name) != null)
            {
                return false;
            }

            if (_botTabs == null)
            {
                _botTabs = new List<IIBotTab>();
            }

            return true;
        }

        /// <summary>
        /// delete active tab
        /// </summary>
        public void TabDelete()
        {
            try
            {
                if (ActiveTab == null)
                {
                    return;
                }

                ActiveTab.Delete();

                _botTabs.Remove(ActiveTab);

                if (_botTabs != null && _botTabs.Count != 0)
                {
                    ChangeActiveTab(0);
                }

                ReloadTab();

                NewTabCreateEvent?.Invoke();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// delete tab for num
        /// </summary>
        public void TabDelete(int index)
        {
            try
            {
                if (ActiveTab == null)
                {
                    return;
                }

                if (index >= _botTabs.Count)
                {
                    return;
                }

                _botTabs[index].Delete();

                _botTabs.RemoveAt(index);
                if (_botTabs != null && _botTabs.Count != 0)
                {
                    ChangeActiveTab(0);
                }

                ReloadTab();

                NewTabCreateEvent?.Invoke();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// set new active tab
        /// </summary>
        private void ChangeActiveTab(int tabNumber)
        {
            try
            {
                if (!_isPainting)
                {
                    return;
                }

                if (!_tabBotTab.Dispatcher.CheckAccess())
                {
                    _tabBotTab.Dispatcher.Invoke(new Action<int>(ChangeActiveTab), tabNumber);
                    return;
                }

                if (ActiveTab != null)
                {
                    ActiveTab.StopPaint();
                }

                if (_botTabs == null ||
                    _botTabs.Count <= tabNumber)
                {
                    return;
                }

                ActiveTab = _botTabs[tabNumber];

                if (ActiveTab.TabType == BotTabType.Simple)
                {
                    ((BotTabSimple)ActiveTab).StartPaint(_gridChart, _hostChart, _hostGlass, _hostOpenDeals,
                        _hostCloseDeals, _rectangle, _hostAlerts, _textBoxLimitPrice,
                        _gridChartControlPanel, _textBoxVolume, _hostGrids);

                    for (int i = 0; i < _tabControlControl.Items.Count; i++)
                    {
                        TabItem itemN = (TabItem)_tabControlControl.Items[i];
                        itemN.IsEnabled = true;
                    }
                    _tabControlControl.SelectedIndex = ((BotTabSimple)ActiveTab).SelectedControlTab;
                }
                else
                {
                    TabItem item1 = (TabItem)_tabControlControl.Items[0];
                    item1.IsEnabled = false;
                    TabItem item2 = (TabItem)_tabControlControl.Items[1];
                    item2.IsEnabled = false;
                    TabItem item3 = (TabItem)_tabControlControl.Items[2];
                    item3.IsEnabled = false;

                    _tabControlControl.SelectedIndex = 3;

                    if (ActiveTab.TabType == BotTabType.Index)
                    {
                        ((BotTabIndex)ActiveTab).StartPaint(_gridChart, _hostChart, _rectangle);
                    }
                    else if (ActiveTab.TabType == BotTabType.Cluster)
                    {
                        ((BotTabCluster)ActiveTab).StartPaint(_hostChart, _rectangle);
                    }
                    else if (ActiveTab.TabType == BotTabType.Screener)
                    {
                        ((BotTabScreener)ActiveTab).StartPaint(_hostChart, _hostOpenDeals, _hostCloseDeals);
                    }
                    else if (ActiveTab.TabType == BotTabType.Options)
                    {
                        ((BotTabOptions)ActiveTab).StartPaint(_hostChart, _hostOpenDeals, _hostCloseDeals);
                    }
                    else if (ActiveTab.TabType == BotTabType.Pair)
                    {
                        ((BotTabPair)ActiveTab).StartPaint(_hostChart, _hostOpenDeals, _hostCloseDeals);
                    }
                    else if (ActiveTab.TabType == BotTabType.Polygon)
                    {
                        ((BotTabPolygon)ActiveTab).StartPaint(_hostChart);
                    }
                    else if (ActiveTab.TabType == BotTabType.News)
                    {
                        ((BotTabNews)ActiveTab).StartPaint(_hostChart);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
        /// reload tabs on control
        /// </summary>
        private void ReloadTab()
        {
            try
            {
                if (_tabBotTab == null)
                {
                    return;
                }
                if (!_tabBotTab.Dispatcher.CheckAccess())
                {
                    _tabBotTab.Dispatcher.Invoke(ReloadTab);
                    return;
                }
                _tabBotTab.SelectionChanged -= _tabBotTab_SelectionChanged;


                _tabBotTab.Items.Clear();

                if (_isPainting)
                {
                    if (_botTabs != null && _botTabs.Count != 0)
                    {
                        for (int i = 0; i < _botTabs.Count; i++)
                        {
                            _tabBotTab.Items.Add(" " + (i + 1));

                        }
                    }

                    if (ActiveTab != null && _botTabs != null && _botTabs.Count != 0)
                    {
                        int index = _botTabs.FindIndex(tab => tab.TabName == ActiveTab.TabName);

                        if (index >= 0)
                        {
                            _tabBotTab.SelectedIndex = index;
                        }
                    }

                    if (_tabBotTab.SelectedIndex == -1 && _botTabs != null && _botTabs.Count != 0)
                    {
                        _tabBotTab.SelectedIndex = 0;
                    }
                }

                _tabBotTab.SelectionChanged += _tabBotTab_SelectionChanged;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// clear and delete all tabs
        /// </summary>
        public void ClearTabs()
        {
            _botTabs?.Clear();
            ActiveTab = null;
            NewTabCreateEvent?.Invoke();
        }

        #endregion

        #region Control windows

        /// <summary>
        /// show general risk manager window
        /// </summary>
        public void ShowPanelRiskManagerDialog()
        {
            try
            {
                if (ActiveTab == null)
                {
                    return;
                }
                _riskManager.ShowDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// show individual settings
        /// </summary>
        public virtual void ShowIndividualSettingsDialog() { }

        #endregion

        #region Global position reaction

        /// <summary>
        /// command handler for manual position control
        /// </summary>
        public void UserSetPositionAction(Position position, SignalType signal)
        {
            try
            {
                if (signal == SignalType.CloseAll)
                {
                    for (int i = 0; i < TabsSimple.Count; i++)
                    {
                        TabsSimple[i].CloseAllAtMarket();
                    }
                    for (int i = 0; i < TabsScreener.Count; i++)
                    {
                        TabsScreener[i].CloseAllPositionAtMarket();
                    }
                    for (int i = 0; i < TabsPair.Count; i++)
                    {
                        TabsPair[i].CloseAllPositionAtMarket();
                    }

                    return;
                }

                // check that the position belongs to this particular robot

                if (position == null)
                {
                    return;
                }

                BotTabSimple tabWithPosition = null;

                for (int i = 0; i < TabsSimple.Count; i++)
                {
                    List<Position> posOnThisTab = TabsSimple[i].PositionsAll;

                    for (int i2 = 0; i2 < posOnThisTab.Count; i2++)
                    {
                        if (posOnThisTab[i2].Number == position.Number)
                        {
                            tabWithPosition = TabsSimple[i];
                        }
                    }

                    if (tabWithPosition != null)
                    {
                        break;
                    }
                }

                if (tabWithPosition == null)
                {
                    for (int i = 0; i < TabsScreener.Count; i++)
                    {
                        tabWithPosition = TabsScreener[i].GetTabWithThisPosition(position.Number);

                        if (tabWithPosition != null)
                        {
                            break;
                        }
                    }
                }

                if (tabWithPosition == null)
                {
                    for (int i = 0; i < TabsPair.Count; i++)
                    {
                        tabWithPosition = TabsPair[i].GetTabWithThisPosition(position.Number);

                        if (tabWithPosition != null)
                        {
                            break;
                        }
                    }
                }

                if (tabWithPosition == null)
                {
                    return;
                }

                if (signal == SignalType.CloseOne)
                {
                    tabWithPosition.ShowClosePositionDialog(position);
                }
                else if (signal == SignalType.ReloadStop)
                {
                    tabWithPosition.ShowStopSendDialog(position);
                }
                else if (signal == SignalType.ReloadProfit)
                {
                    tabWithPosition.ShowProfitSendDialog(position);
                }
                else if (signal == SignalType.DeletePos)
                {
                    tabWithPosition._journal.DeletePosition(position);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// is event feed enabled
        /// </summary>
        public bool OnOffEventsInTabs
        {
            get
            {
                if(_botTabs== null
                    ||  _botTabs.Count == 0)
                {
                    return false;
                }

                 return _botTabs[0].EventsIsOn;
            }
            set
            {
                for (int i = 0; _botTabs != null && i < _botTabs.Count; i++)
                {
                    _botTabs[i].EventsIsOn = value;
                }
            }
        }

        /// <summary>
        /// is emulation enabled
        /// </summary>
        public bool OnOffEmulatorsInTabs
        {
            get
            {
                for (int i = 0; _botTabs != null && i < _botTabs.Count; i++)
                {

                    if (_botTabs[i].TabType == BotTabType.Index
                        || _botTabs[i].TabType == BotTabType.Cluster)
                    {
                        continue;
                    }

                    return _botTabs[i].EmulatorIsOn;
                }

                return false;
            }
            set
            {
                if (StartProgram != StartProgram.IsOsTrader)
                {
                    return;
                }

                for (int i = 0; _botTabs != null && i < _botTabs.Count; i++)
                {
                    _botTabs[i].EmulatorIsOn = value;
                }
            }
        }

        #endregion

        #region Log

        public Log Log;

        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if(type  == LogMessageType.Error)
            {
                message = NameStrategyUniq + " " + this.GetNameStrategyType() + "\n" + message;
            }

            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action DeleteEvent;

        public event Action NewTabCreateEvent;

        #endregion

    }

    /// <summary>
    /// gui settings
    /// </summary>
    public class ParamGuiSettings
    {
        /// <summary>
        /// label for parameter window
        /// </summary>
        public string Title;

        /// <summary>
        /// default tab name
        /// </summary>
        public string FirstTabLabel = "Prime";

        /// <summary>
        /// starting height of the parameter window
        /// </summary>
        public decimal Height = 370;

        /// <summary>
        /// starting parameter window width
        /// </summary>
        public decimal Width = 600;

        /// <summary>
        /// custom tabs
        /// </summary>
        public List<CustomTabToParametersUi> CustomTabs = new List<CustomTabToParametersUi>();

        /// <summary>
        /// create a tab for the options window
        /// </summary>
        /// <param name="tabLabel">tab name</param>
        public CustomTabToParametersUi CreateCustomTab(string tabLabel)
        {
            CustomTabToParametersUi newTab = CustomTabs.Find(tab => tab.Label == tabLabel);

            if (newTab != null)
            {
                SendNewLogMessage
                    ("An attempt was intercepted to create a second tab of parameters with the same name that is already in the collection.",
                    LogMessageType.Error);
                return newTab;
            }

            newTab = new CustomTabToParametersUi(tabLabel);

            CustomTabs.Add(newTab);

            return newTab;
        }

        /// <summary>
        /// send new message
        /// </summary>
        protected void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                MessageBox.Show(message);
            }
        }

        /// <summary>
        /// log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
		
        /// <summary>
        /// set border under of Parameter
        /// </summary>
        /// <param name="parameterName">Parameter name (do not empty names "" of StrategyParameter; for StrategyParameterButton use "buttonLabel"; for StrategyParameterLabel use "label")</param>
        /// <param name="color">border color</param>
        /// <param name="thickness">border thickness (min 1, max 10)</param>
        public void SetBorderUnderParameter(string parameterName, System.Drawing.Color color, int thickness)
        {
            try
            {
                ParamDesign newBorderSet = new ParamDesign(ParamDesignType.BorderUnder, parameterName, color, thickness);

                if (_parameterDesigns.ContainsKey(parameterName + ParamDesignType.BorderUnder.ToString()))
                {
                    _parameterDesigns[parameterName + ParamDesignType.BorderUnder.ToString()] = newBorderSet;
                }
                else
                {
                    _parameterDesigns.Add(parameterName + ParamDesignType.BorderUnder.ToString(), newBorderSet);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// set fore color of Parameter (text color)
        /// </summary>
        /// <param name="parameterName">Parameter name (do not empty names "" of StrategyParameter; for StrategyParameterButton use "buttonLabel"; for StrategyParameterLabel use "label")</param>
        /// <param name="color">color</param>
        public void SetForeColorParameter(string parameterName, System.Drawing.Color color)
        {
            try
            {
                ParamDesign newForeColorSet = new ParamDesign(ParamDesignType.ForeColor, parameterName, color, 1);

                if (_parameterDesigns.ContainsKey(parameterName + ParamDesignType.ForeColor.ToString()))
                {
                    _parameterDesigns[parameterName + ParamDesignType.ForeColor.ToString()] = newForeColorSet;
                }
                else
                {
                    _parameterDesigns.Add(parameterName + ParamDesignType.ForeColor.ToString(), newForeColorSet);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
   
        /// <summary>
        /// set selection color of Parameter
        /// </summary>
        /// <param name="parameterName">Parameter name (do not empty names "" of StrategyParameter; for StrategyParameterButton use "buttonLabel"; for StrategyParameterLabel use "label")</param>
        /// <param name="color">color</param>
        public void SetSelectionColorParameter(string parameterName, System.Drawing.Color color)
        {
            try
            {
                ParamDesign newSelectionColorSet = new ParamDesign(ParamDesignType.SelectionColor, parameterName, color, 1);

                if (_parameterDesigns.ContainsKey(parameterName + ParamDesignType.SelectionColor.ToString()))
                {
                    _parameterDesigns[parameterName + ParamDesignType.SelectionColor.ToString()] = newSelectionColorSet;
                }
                else
                {
                    _parameterDesigns.Add(parameterName + ParamDesignType.SelectionColor.ToString(), newSelectionColorSet);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// visual designs of Parameters
        /// </summary>
        public IReadOnlyDictionary<string, ParamDesign> ParameterDesigns => _parameterDesigns;

        private Dictionary<string, ParamDesign> _parameterDesigns = new Dictionary<string, ParamDesign>();

        /// <summary>	
        ///  repaint Parameter tables (it is not recommended to call often, recommended >100 ms)
        /// </summary>
        public void RePaintParameterTables()
        {
            IsRePaintParameterTables = IsRePaintParameterTables == false;
        }

        /// <summary>
        /// status of Parameter tables repaint (it is not specific pointer)
        /// </summary>
        public bool IsRePaintParameterTables { get; private set; }
    }

    /// <summary>
    /// custom tab options
    /// </summary>
    public class CustomTabToParametersUi
    {
        public CustomTabToParametersUi(string label)
        {
            Label = label;

            CreateGrid();
        }

        public void CreateGrid()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(CreateGrid));
                return;
            }

            GridToPaint = new Grid();
        }

        private CustomTabToParametersUi()
        {
        }

        /// <summary>
        /// tab title
        /// </summary>
        public string Label { get; private set; }

        /// <summary>
        /// the element to be placed on the tab
        /// </summary>
        public Grid GridToPaint;

        public void AddChildren(object children)
        {
            if (GridToPaint.Dispatcher.CheckAccess() == false)
            {
                GridToPaint.Dispatcher.Invoke(new Action<object>(AddChildren), children);
                return;
            }

            GridToPaint.Children.Add((UIElement)children);
        }
    }
	
    /// <summary>
    /// visual design of Parameter
    /// </summary>
    public readonly struct ParamDesign
    {
        public ParamDesign(ParamDesignType designType, string parameterName, System.Drawing.Color color, int thickness)
        {
            DesignType = designType;
            ParameterName = parameterName;
            Color = color;
            Thickness = thickness;
        }

        /// <summary>
        /// type of Parameter visual design
        /// </summary>
        public ParamDesignType DesignType { get; }

        /// <summary>
        /// Parameter name
        /// </summary>
        public string ParameterName { get; }

        public System.Drawing.Color Color { get; }

        public int Thickness { get; }
    }
	
    /// <summary>
    /// robot trade regime
    /// </summary>
    public enum BotTradeRegime
    {
        /// <summary>
        /// is on
        /// </summary>
        On,

        /// <summary>
        /// on only long position
        /// </summary>
        OnlyLong,

        /// <summary>
        /// on only short position
        /// </summary>
        OnlyShort,

        /// <summary>
        /// on only close position
        /// </summary>
        OnlyClosePosition,

        /// <summary>
        /// robot is off
        /// </summary>
        Off
    }
	
    /// <summary>
    /// type of Parameter visual design
    /// </summary>
    public enum ParamDesignType
    {
        /// <summary>
        /// fore color of Parameter
        /// </summary>
        ForeColor,

        /// <summary>
        /// border under of Parameter 
        /// </summary>
        BorderUnder,
		
        /// <summary>
        /// selection color of Parameter
        /// </summary>
        SelectionColor		
    }	
}
