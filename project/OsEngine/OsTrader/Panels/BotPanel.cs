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
        News
    }

    /// <summary>
    /// parent for all robots in the program
    /// </summary>
    public abstract class BotPanel
    {
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
        }

        /// <summary>
        /// critical error and system restart event
        /// </summary>
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

        /// <summary>
        /// unique robot name
        /// </summary>
        public string NameStrategyUniq;

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

        // control

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
                if(_chartUi.WindowState == WindowState.Minimized)
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
                SendNewLogMessage(ex.ToString(),LogMessageType.Error);
            }
        }

        void _chartUi_Closed(object sender, EventArgs e)
        {
            _chartUi.Closed -= _chartUi_Closed;
            _chartUi = null;

            if (ChartClosedEvent != null)
            {
                ChartClosedEvent(this.NameStrategyUniq);
            }
        }

        public event Action<string> ChartClosedEvent;

        /// <summary>
        /// is drawing included
        /// </summary>
        private bool _isPainting;

        /// <summary>
        /// start drawing this robot
        /// </summary> 
        public void StartPaint(Grid gridChart, WindowsFormsHost hostChart, WindowsFormsHost glass, 
            WindowsFormsHost hostOpenDeals,WindowsFormsHost hostCloseDeals, 
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
        /// bot name
        /// </summary>
        public virtual string GetNameStrategyType() => GetType().Name;

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

        /// <summary>
        /// clear data
        /// </summary>
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

                if (Log != null)
                {
                    Log.Clear();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// remove the robot and all child structures
        /// </summary>
        public void Delete()
        {
            try
            {
                try
                {
                    if (_chartUi != null)
                    {
                        _chartUi.Close();
                    }
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

                if (_tabSimple != null)
                {
                    _tabSimple.Clear();
                    _tabSimple = null;
                }

                if (_tabsCluster != null)
                {
                    _tabsCluster.Clear();
                    _tabsCluster = null;
                }

                if (_tabsPair != null)
                {
                    _tabsPair.Clear();
                    _tabsPair = null;
                }

                if (_tabsScreener != null)
                {
                    _tabsScreener.Clear();
                    _tabsScreener = null;
                }

                if (_tabsPolygon != null)
                {
                    _tabsPolygon.Clear();
                    _tabsPolygon = null;
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

                if (_parameters != null)
                {
                    for (int i = 0; i < _parameters.Count; i++)
                    {
                        _parameters[i].ValueChange -= Parameter_ValueChange;
                    }
                    _parameters.Clear();
                    _parameters = null;
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
                    catch(Exception ex)
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

        /// <summary>
        /// move the chart view all the way to the right. Needed for a tester. Moved if BotTabSimple is selected
        /// </summary>
        public void MoveChartToTheRight()
        {
            if (ActiveTab == null)
            {
                return;
            }

            if (ActiveTab.GetType().Name == "BotTabSimple")
            {
                ((BotTabSimple)ActiveTab).MoveChartToTheRight();
            }
        }

        // robot trading figures

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
                        if (journals[i].AllPosition[i2].State == PositionStateType.OpeningFail)
                        {
                            continue;
                        }
                        allPositionOpen.Add(journals[i].AllPosition[i2]);
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

        // working with strategy parameters

        /// <summary>
        /// show parameter settings window
        /// </summary>
        public void ShowParameterDialog()
        {
            if (_parameters == null ||
                _parameters.Count == 0)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label51);
                ui.ShowDialog();
                return;
            }

            if (_parametersUi == null)
            {
                _parametersUi = new StrategyParametersUi(_parameters, ParamGuiSettings, this);
                _parametersUi.Show();
                _parametersUi.Closing += _parametersUi_Closing;
            }
            else
            {
                if (_parametersUi.WindowState == System.Windows.WindowState.Minimized)
                {
                    _parametersUi.WindowState = System.Windows.WindowState.Normal;
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
        public void CloseParameterDialog()
        {
            if (_parametersUi != null)
            {
                _parametersUi.Close();
            }
        }

        /// <summary>
        /// Whether the parameter window is open for display. True - is open
        /// </summary>
        public bool ParamGuiIsOpen
        {
            get
            {
                if(_parametersUi == null)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

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

            if (_parameters.Find(p => p.Name == name) != null)
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

            if (_parameters.Find(p => p.Name == name) != null)
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

            if (_parameters.Find(p => p.Name == name) != null)
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
            if (_parameters.Find(p => p.Name == name) != null)
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

            if (_parameters.Find(p => p.Name == name) != null)
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

            if (_parameters.Find(p => p.Name == name) != null)
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

            if (_parameters.Find(p => p.Name == buttonLabel) != null)
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

            if (_parameters.Find(p => p.Name == checkBoxLabel) != null)
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

            if (_parameters.Find(p => p.Name == name) != null)
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

            if (_parameters.Find(p => p.Name == name) != null)
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

            _parameters.Add(newParameter);

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
        public List<IIStrategyParameter> Parameters
        {
            get { return _parameters; }
        }

        private List<IIStrategyParameter> _parameters = new List<IIStrategyParameter>();

        /// <summary>
        /// parameter has changed settings
        /// </summary>
        void Parameter_ValueChange()
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

            if (_parameters == null ||
                _parameters.Count == 0)
            {
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"Parametrs.txt", false)
                    )
                {
                    for (int i = 0; i < _parameters.Count; i++)
                    {
                        writer.WriteLine(_parameters[i].GetStringToSave());
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

        // risk manager panel

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
        /// emergency closing of all positions
        /// </summary>
        public void CloseAndOffAllToMarket()
        {
            try
            {
                string message = OsLocalization.Trader.Label54 + NameStrategyUniq;
                ShowMessageInNewThread(message);

                for (int i = 0; i < _botTabs.Count; i++)
                {
                    if (_botTabs[i].GetType().Name == "BotTabSimple")
                    {
                        BotTabSimple bot = (BotTabSimple)_botTabs[i];
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

        // tab management

        /// <summary>
        /// tabbed tabs
        /// </summary>
        private List<IIBotTab> _botTabs;

        /// <summary>
        /// get all tabs
        /// </summary>
        public List<IIBotTab> GetTabs()
        {
            return _botTabs;
        }

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
                return _tabSimple;
            }
        }

        private List<BotTabSimple> _tabSimple = new List<BotTabSimple>();

        /// <summary>
        /// index tabs
        /// </summary>
        public List<BotTabIndex> TabsIndex
        {
            get
            {
                return _tabsIndex;
            }
        }

        private List<BotTabIndex> _tabsIndex = new List<BotTabIndex>();

        /// <summary>
        /// clustered tabs
        /// </summary>
        public List<BotTabCluster> TabsCluster
        {
            get
            {
                return _tabsCluster;
            }
        }

        private List<BotTabCluster> _tabsCluster = new List<BotTabCluster>();

        /// <summary>
        /// pair tabs
        /// </summary>
        public List<BotTabPair> TabsPair
        {
            get
            {
                return _tabsPair;
            }
        }
        private List<BotTabPair> _tabsPair = new List<BotTabPair>();

        /// <summary>
        /// screener tabs
        /// </summary>
        public List<BotTabScreener> TabsScreener
        {
            get
            {
                return _tabsScreener;
            }
        }
        private List<BotTabScreener> _tabsScreener = new List<BotTabScreener>();

        /// <summary>
        /// polygon tabs
        /// </summary>
        public List<BotTabPolygon> TabsPolygon
        {
            get
            {
                return _tabsPolygon;
            }
        }
        private List<BotTabPolygon> _tabsPolygon = new List<BotTabPolygon>();

        /// <summary>
        /// news tabs
        /// </summary>
        public List<BotTabNews> TabsNews
        {
            get
            {
                return _tabsNews;
            }
        }
        private List<BotTabNews> _tabsNews = new List<BotTabNews>();

        public DateTime TimeServer
        {
            get
            {
                DateTime result = DateTime.MinValue;

                if (_tabSimple != null
                    && _tabSimple.Count > 0)
                {
                    for (int i = 0; i < _tabSimple.Count; i++)
                    {
                        if (_tabSimple[i].IsConnected == false)
                        {
                            continue;
                        }

                        if (_tabSimple[i].TimeServerCurrent > result)
                        {
                            result = _tabSimple[i].TimeServerCurrent;
                        }
                    }
                }

                if (_tabsScreener != null
                    && _tabsScreener.Count > 0)
                {
                    for (int i = 0; i < _tabsScreener.Count; i++)
                    {
                        for (int j = 0; j < _tabsScreener[i].Tabs.Count; j++)
                        {
                            BotTabSimple tab = _tabsScreener[i].Tabs[j];

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
        public void TabCreate(BotTabType tabType)
        {
            try
            {
                int number;

                if (_botTabs == null || _botTabs.Count == 0)
                {
                    number = 0;
                }
                else
                {
                    number = _botTabs.Count;
                }

                string nameTab = NameStrategyUniq + "tab" + number;

                if (_botTabs != null && _botTabs.Find(strategy => strategy.TabName == nameTab) != null)
                {
                    return;
                }

                if (_botTabs == null)
                {
                    _botTabs = new List<IIBotTab>();
                }
                IIBotTab newTab;

                if (tabType == BotTabType.Simple)
                {
                    newTab = new BotTabSimple(nameTab, StartProgram);
                    _tabSimple.Add((BotTabSimple)newTab);
                }
                else if (tabType == BotTabType.Index)
                {
                    newTab = new BotTabIndex(nameTab, StartProgram);
                    _tabsIndex.Add((BotTabIndex)newTab);
                }
                else if (tabType == BotTabType.Cluster)
                {
                    newTab = new BotTabCluster(nameTab, StartProgram);
                    _tabsCluster.Add((BotTabCluster)newTab);
                }
                else if (tabType == BotTabType.Pair)
                {
                    newTab = new BotTabPair(nameTab, StartProgram);
                    _tabsPair.Add((BotTabPair)newTab);
                    ((BotTabPair)newTab).UserSelectActionEvent += UserSetPositionAction;
                }
                else if (tabType == BotTabType.Polygon)
                {
                    newTab = new BotTabPolygon(nameTab, StartProgram);
                    _tabsPolygon.Add((BotTabPolygon)newTab);
                }
                else if (tabType == BotTabType.News)
                {
                    newTab = new BotTabNews(nameTab, StartProgram);
                    _tabsNews.Add((BotTabNews)newTab);
                }
                else if (tabType == BotTabType.Screener)
                {
                    newTab = new BotTabScreener(nameTab, StartProgram);
                    _tabsScreener.Add((BotTabScreener)newTab);

                    ((BotTabScreener)newTab).UserSelectActionEvent += UserSetPositionAction;

                     ((BotTabScreener)newTab).NewTabCreateEvent += (tab) =>
                    {
                        if (NewTabCreateEvent != null)
                        {
                            NewTabCreateEvent();
                        }
                    };
                }
                else
                {
                    return;
                }

                _botTabs.Add(newTab);
                newTab.LogMessageEvent += SendNewLogMessage;

                newTab.TabNum = _botTabs.Count - 1;

                ChangeActiveTab(_botTabs.Count - 1);

                ReloadTab();

                if (NewTabCreateEvent != null)
                {
                    NewTabCreateEvent();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
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

                if (NewTabCreateEvent != null)
                {
                    NewTabCreateEvent();
                }
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

                if (NewTabCreateEvent != null)
                {
                    NewTabCreateEvent();
                }
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

                if (_tabBotTab.IsVisible == false)
                {

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

                    for(int i = 0;i < _tabControlControl.Items.Count;i++)
                    {
                        System.Windows.Controls.TabItem itemN = (System.Windows.Controls.TabItem)_tabControlControl.Items[i];
                        itemN.IsEnabled = true;
                    }
                    _tabControlControl.SelectedIndex = ((BotTabSimple)ActiveTab).SelectedControlTab;
                }
                else
                {
                    System.Windows.Controls.TabItem item1 = (System.Windows.Controls.TabItem)_tabControlControl.Items[0];
                    item1.IsEnabled = false;
                    System.Windows.Controls.TabItem item2 = (System.Windows.Controls.TabItem)_tabControlControl.Items[1];
                    item2.IsEnabled = false;
                    System.Windows.Controls.TabItem item3 = (System.Windows.Controls.TabItem)_tabControlControl.Items[2];
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
            for (int i = 0; TabsSimple != null && i < TabsSimple.Count; i++)
            {
                TabsSimple[i].Clear();
            }
            for (int i = 0; TabsIndex != null && i < TabsIndex.Count; i++)
            {
                TabsIndex[i].Clear();
            }
            for (int i = 0; TabsCluster != null && i < TabsCluster.Count; i++)
            {
                TabsCluster[i].Clear();
            }
            for (int i = 0; TabsScreener != null && i < TabsScreener.Count; i++)
            {
                TabsScreener[i].Clear();
            }
            for (int i = 0; TabsPair != null && i < TabsPair.Count; i++)
            {
                TabsPair[i].Clear();
            }

            if (_botTabs != null)
            {
                _botTabs.Clear();
            }

            ActiveTab = null;

            if (NewTabCreateEvent != null)
            {
                NewTabCreateEvent();
            }
        }

        // call control windows

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

        // global position reaction

        /// <summary>
        /// command handler for manual position control
        /// </summary>
        public void UserSetPositionAction(Position position, SignalType signal)
        {
            try
            {
                if (signal == SignalType.CloseAll)
                {
                    for (int i = 0; i < _tabSimple.Count; i++)
                    {
                        _tabSimple[i].CloseAllAtMarket();
                    }
                    for (int i = 0; i < _tabsScreener.Count; i++)
                    {
                        _tabsScreener[i].CloseAllPositionAtMarket();
                    }
                    for (int i = 0; i < _tabsPair.Count; i++)
                    {
                        _tabsPair[i].CloseAllPositionAtMarket();
                    }

                    return;
                }

                // check that the position belongs to this particular robot

                if (position == null)
                {
                    return;
                }

                BotTabSimple tabWithPosition = null;

                for (int i = 0; i < _tabSimple.Count; i++)
                {
                    List<Position> posOnThisTab = _tabSimple[i].PositionsAll;

                    for (int i2 = 0; i2 < posOnThisTab.Count; i2++)
                    {
                        if (posOnThisTab[i2].Number == position.Number)
                        {
                            tabWithPosition = _tabSimple[i];
                        }
                    }

                    if (tabWithPosition != null)
                    {
                        break;
                    }
                }

                if (tabWithPosition == null)
                {
                    for (int i = 0; i < _tabsScreener.Count; i++)
                    {
                        tabWithPosition = _tabsScreener[i].GetTabWithThisPosition(position.Number);

                        if (tabWithPosition != null)
                        {
                            break;
                        }
                    }
                }

                if (tabWithPosition == null)
                {
                    for (int i = 0; i < _tabsPair.Count; i++)
                    {
                        tabWithPosition = _tabsPair[i].GetTabWithThisPosition(position.Number);

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
                for (int i = 0; _botTabs != null && i < _botTabs.Count; i++)
                {
                    return _botTabs[i].EventsIsOn;
                }

                return false;
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

        // log

        public Log Log;

        /// <summary>
        /// send new message
        /// </summary>
        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        /// <summary>
        /// delete bot event
        /// </summary>
        public event Action DeleteEvent;

        /// <summary>
        /// sourse count change
        /// </summary>
        public event Action NewTabCreateEvent;

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
                System.Windows.MessageBox.Show(message);
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
        public IReadOnlyDictionary<string, ParamDesign> ParameterDesigns
        {
            get 
            { 
                return _parameterDesigns; 
            }
        }
        private Dictionary<string, ParamDesign> _parameterDesigns = new Dictionary<string, ParamDesign>();

        /// <summary>	
        ///  repaint Parameter tables (it is not recommended to call often, recommended >100 ms)
        /// </summary>
        public void RePaintParameterTables()
        {
            if (_isRePaintParameterTables == false)
            {
                _isRePaintParameterTables = true;
            }
            else
            {
                _isRePaintParameterTables = false;
            }
        }

        /// <summary>
        /// status of Parameter tables repaint (it is not specific pointer)
        /// </summary>
        public bool IsRePaintParameterTables
        {
            get
            {
                return _isRePaintParameterTables;
            }
        }
        private bool _isRePaintParameterTables;

    }

    /// <summary>
    /// custom tab options
    /// </summary>
    public class CustomTabToParametersUi
    {
        public CustomTabToParametersUi(string label)
        {
            _label = label;

            CreateGrid();
        }

        public void CreateGrid()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(CreateGrid));
                return;
            }

            GridToPaint = new System.Windows.Controls.Grid();
        }

        private CustomTabToParametersUi()
        {
        }

        /// <summary>
        /// tab title
        /// </summary>
        public string Label
        {
            get
            {
                return _label;
            }
        }
        private string _label;

        /// <summary>
        /// the element to be placed on the tab
        /// </summary>
        public System.Windows.Controls.Grid GridToPaint;

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
