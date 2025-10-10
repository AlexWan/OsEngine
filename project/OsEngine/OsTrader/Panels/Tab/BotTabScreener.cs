/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Logging;
using OsEngine.Market;
using System.Windows.Forms;
using System.Threading;
using OsEngine.Robots.Engines;
using OsEngine.Language;
using OsEngine.Alerts;
using OsEngine.Market.Connectors;
using OsEngine.Candles.Series;
using OsEngine.Candles;
using OsEngine.Market.Servers;
using OsEngine.Candles.Factory;
using OsEngine.OsTrader.Panels.Tab.Internal;
using System.Drawing;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.OsTrader.Panels.Tab
{
    public class BotTabScreener : IIBotTab
    {
        #region Static part

        /// <summary>
        /// Activate grid drawing
        /// </summary>
        private static void StaticThreadActivation()
        {
            lock (_staticThreadLocker)
            {
                if (_staticThread != null)
                {
                    return;
                }

                _staticThread = new Thread(StaticThreadArea);
                _staticThread.Start();
            }
        }

        /// <summary>
        /// Screener tabs
        /// </summary>
        private static List<BotTabScreener> _screeners = new List<BotTabScreener>();

        private static string _screenersListLocker = "scrLocker";

        /// <summary>
        /// Add a new tracking tab
        /// </summary>
        /// <param name="tab">screener tab</param>
        private static void AddNewTabToWatch(BotTabScreener tab)
        {
            lock (_screenersListLocker)
            {
                _screeners.Add(tab);
            }
        }

        /// <summary>
        /// Remove a tab from being followed
        /// </summary>
        /// <param name="tab">screener tab</param>
        private static void RemoveTabFromWatch(BotTabScreener tab)
        {
            lock (_screenersListLocker)
            {
                for (int i = 0; i < _screeners.Count; i++)
                {
                    if (_screeners[i].TabName == tab.TabName)
                    {
                        _screeners.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Blocker of multi-threaded access to the activation of the rendering of screeners
        /// </summary>
        private static object _staticThreadLocker = new object();

        /// <summary>
        /// Thread rendering screeners
        /// </summary>
        private static Thread _staticThread;

        /// <summary>
        /// Place of work for the thread that draws screeners
        /// </summary>
        private static void StaticThreadArea()
        {
            Thread.Sleep(3000);

            while (true)
            {
                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                Thread.Sleep(500);

                try
                {
                    for (int i = 0; _screeners != null &&
                        _screeners.Count != 0 &&
                        i < _screeners.Count; i++)
                    {
                        BotTabScreener curScreener = _screeners[i];

                        if (curScreener._host != null)
                        {
                            for (int i2 = 0; curScreener.Tabs != null &&
                                 curScreener.Tabs.Count != 0 &&
                                 i2 < curScreener.Tabs.Count; i2++)
                            {
                                PaintLastBidAsk(_screeners[i].Tabs[i2], _screeners[i].SecuritiesDataGrid);
                            }
                        }

                        if (curScreener.ServerType != ServerType.Optimizer)
                        {
                            _screeners[i].TryLoadTabs();
                            _screeners[i].TryReLoadTabs();
                        }
                    }
                }
                catch
                {
                    // ignore
                }

            }
        }

        /// <summary>
        /// Draw the last ask, bid, last and number of positions
        /// </summary>
        public static void PaintLastBidAsk(BotTabSimple tab, DataGridView securitiesDataGrid)
        {
            if (securitiesDataGrid.InvokeRequired)
            {
                securitiesDataGrid.Invoke(new Action<BotTabSimple, DataGridView>(PaintLastBidAsk), tab, securitiesDataGrid);
                return;
            }

            try
            {
                if (tab.Connector == null
                || tab.Connector.SecurityName == null)
                {
                    return;
                }
                for (int i = 0; i < securitiesDataGrid.Rows.Count; i++)
                {
                    DataGridViewRow row = securitiesDataGrid.Rows[i];

                    if (row.Cells == null
                        || row.Cells.Count == 0
                        || row.Cells.Count < 4
                        || row.Cells[2].Value == null)
                    {
                        continue;
                    }

                    string secName = row.Cells[2].Value.ToString();

                    if (tab.Connector.SecurityName != secName)
                    {
                        continue;
                    }

                    decimal ask = tab.PriceBestAsk;
                    decimal bid = tab.PriceBestBid;

                    decimal last = 0;

                    int posCurr = tab.PositionsOpenAll.Count;
                    int posTotal = tab.PositionsAll.Count;

                    if (tab.CandlesAll != null && tab.CandlesAll.Count != 0)
                    {
                        last = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;
                    }

                    string lastInStr = last.ToString();

                    if (row.Cells[3].Value == null ||
                        row.Cells[3].Value.ToString() != lastInStr)
                    {
                        row.Cells[3].Value = lastInStr;
                    }

                    string bidInStr = bid.ToString();

                    if (row.Cells[4].Value == null ||
                        row.Cells[4].Value.ToString() != bidInStr)
                    {
                        row.Cells[4].Value = bidInStr;
                    }

                    string askInStr = ask.ToString();

                    if (row.Cells[5].Value == null ||
                        row.Cells[5].Value.ToString() != askInStr)
                    {
                        row.Cells[5].Value = askInStr;
                    }

                    string curPoses = posCurr.ToString() + "/" + posTotal.ToString();

                    if (row.Cells[6].Value == null
                        || row.Cells[6].Value.ToString() != curPoses)
                    {
                        if (posCurr > 0)
                        {
                            row.Cells[6].Style.ForeColor = Color.Green;
                        }
                        else
                        {
                            row.Cells[6].Style.ForeColor = row.Cells[5].Style.ForeColor;
                        }

                        row.Cells[6].Value = curPoses;
                    }
                }
            }
            catch (Exception error)
            {
                tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Service

        /// <summary>
        /// Tab for portfolio securities
        /// </summary>
        /// <param name="name">uniq name</param>
        /// <param name="startProgram">start program</param>
        public BotTabScreener(string name, StartProgram startProgram)
        {
            TabName = name;
            _startProgram = startProgram;
            Tabs = new List<BotTabSimple>();

            LoadSettings();
            CreateSecuritiesGrid();

            StaticThreadActivation();

            LoadIndicators();
            ReloadIndicatorsOnTabs();

            if (startProgram != StartProgram.IsOsOptimizer)
            {
                AddNewTabToWatch(this);
            }

            this.TabDeletedEvent += BotTabScreener_DeleteEvent;

            if (startProgram == StartProgram.IsTester)
            {
                List<IServer> servers = ServerMaster.GetServers();

                if (servers != null &&
                    servers.Count > 0
                    && servers[0].ServerType == ServerType.Tester)
                {
                    ((TesterServer)servers[0]).TestingStartEvent += BotTabScreener_TestingStartEvent;
                    ((TesterServer)servers[0]).TestingEndEvent += BotTabScreener_TestingEndEvent;
                    ServerType = ServerType.Tester;
                    ServerName = ServerType.Tester.ToString();
                }
                else if (servers != null &&
                    servers.Count > 0
                    && servers[0].ServerType == ServerType.Optimizer)
                {
                    ServerType = ServerType.Optimizer;
                    ServerName = ServerType.Optimizer.ToString();
                }
            }
            else if (startProgram == StartProgram.IsOsOptimizer)
            {
                ServerType = ServerType.Optimizer;
                ServerName = ServerType.Optimizer.ToString();
            }
        }

        /// <summary>
        /// source type
        /// </summary>
        public BotTabType TabType
        {
            get
            {
                return BotTabType.Screener;
            }
        }

        /// <summary>
        /// Tab delete event handler
        /// </summary>
        private void BotTabScreener_DeleteEvent()
        {
            RemoveTabFromWatch(this);
        }

        /// <summary>
        /// Program that created the robot
        /// </summary>
        public StartProgram StartProgram
        {
            get { return _startProgram; }
        }
        private StartProgram _startProgram;

        /// <summary>
        /// Storage location for all simple tabs owned by the screener
        /// </summary>
        public List<BotTabSimple> Tabs = new List<BotTabSimple>();

        /// <summary>
        /// Unique robot name
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// Tab number
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
        /// custom name robot
        /// пользовательское имя робота
        /// </summary>
        public string NameStrategy
        {
            get
            {
                if (TabName.Contains("tab"))
                {
                    return TabName.Remove(TabName.LastIndexOf("tab"), TabName.Length - TabName.LastIndexOf("tab"));
                }
                return "";
            }
        }

        /// <summary>
        /// unique server number. Service data for the optimizer
        /// </summary>
        public int ServerUid;

        /// <summary>
        /// Time of the last update of the candle
        /// </summary>
        public DateTime LastTimeCandleUpdate { get; set; }

        /// <summary>
        /// Clear
        /// </summary>
        public void Clear()
        {
            LastTimeCandleUpdate = DateTime.MinValue;

            for (int i = 0; Tabs != null && i < Tabs.Count; i++)
            {
                Tabs[i].Clear();
            }
        }

        /// <summary>
        /// Save settings to a file
        /// </summary>
        private void SaveTabs()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"ScreenerTabSet.txt", false))
                {
                    string save = "";
                    for (int i = 0; i < Tabs.Count; i++)
                    {
                        save += Tabs[i].TabName + "#";
                        Tabs[i].Connector.Save();
                        Tabs[i].TimeFrameBuilder.Save();
                        Tabs[i].ManualPositionSupport.Save();
                    }
                    writer.WriteLine(save);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private bool _tabIsLoad = false;

        /// <summary>
        /// Whether the submission of events to the top is enabled or not
        /// </summary>
        public bool EventsIsOn
        {
            get
            {
                return _eventsIsOn;
            }
            set
            {
                if (_eventsIsOn == value)
                {
                    return;
                }

                _eventsIsOn = value;

                try
                {
                    for (int i = 0; i < Tabs.Count; i++)
                    {
                        Tabs[i].EventsIsOn = value;
                    }
                }
                catch
                {
                    // ignore
                }

                SaveSettings();
            }
        }

        private bool _eventsIsOn = true;

        /// <summary>
        /// Load settings from file
        /// </summary>
        public void TryLoadTabs()
        {
            if (!ServerMaster.HasActiveServers())
            {
                if (ServerType != ServerType.None)  //AVP so that the server autostart function works
                {
                    ServerMaster.SetServerToAutoConnection(ServerType, ServerName); //AVP
                }
            }

            if (_tabIsLoad == true)
            {
                return;
            }
            _tabIsLoad = true;

            if (!File.Exists(@"Engine\" + TabName + @"ScreenerTabSet.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"ScreenerTabSet.txt"))
                {
                    string[] save2 = reader.ReadLine().Split('#');
                    for (int i = 0; i < save2.Length - 1; i++)
                    {
                        BotTabSimple newTab = new BotTabSimple(save2[i], _startProgram);
                        newTab.Connector.SaveTradesInCandles = false;

                        Tabs.Add(newTab);
                        SubscribeOnTab(newTab);
                        UpdateTabSettings(Tabs[Tabs.Count - 1]);
                        PaintNewRow();

                        if (NewTabCreateEvent != null)
                        {
                            NewTabCreateEvent(newTab);
                        }

                        if (Tabs.Count == 1)
                        {
                            Tabs[0].IndicatorManuallyCreateEvent += BotTabScreener_IndicatorManuallyCreateEvent;
                            Tabs[0].IndicatorManuallyDeleteEvent += BotTabScreener_IndicatorManuallyDeleteEvent;
                            Tabs[0].IndicatorUpdateEvent += BotTabScreener_IndicatorUpdateEvent;
                        }
                    }

                    reader.Close();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            ReloadIndicatorsOnTabs();
            SetJournalsInPosViewer();
        }

        /// <summary>
        /// Save settings
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"ScreenerSet.txt", false))
                {
                    writer.WriteLine(PortfolioName);
                    writer.WriteLine(SecuritiesClass);
                    writer.WriteLine(TimeFrame);
                    writer.WriteLine(ServerType + "&" + ServerName);
                    writer.WriteLine(_emulatorIsOn);
                    writer.WriteLine(CandleMarketDataType);
                    writer.WriteLine(CandleCreateMethodType);
                    writer.WriteLine(CommissionType);
                    writer.WriteLine(CommissionValue);
                    writer.WriteLine(SaveTradesInCandles);
                    writer.WriteLine(_eventsIsOn);

                    writer.WriteLine(CandleSeriesRealization.GetType().Name);
                    writer.WriteLine(CandleSeriesRealization.GetSaveString());

                    for (int i = 0; i < SecuritiesNames.Count; i++)
                    {
                        writer.WriteLine(SecuritiesNames[i].GetSaveStr());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// Load settings
        /// </summary>
        public void LoadSettings()
        {
            if (!File.Exists(@"Engine\" + TabName + @"ScreenerSet.txt"))
            {
                _candleCreateMethodType = "Simple";
                CandleSeriesRealization = CandleFactory.CreateCandleSeriesRealization("Simple");
                CandleSeriesRealization.Init(_startProgram);
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"ScreenerSet.txt"))
                {
                    PortfolioName = reader.ReadLine();
                    SecuritiesClass = reader.ReadLine();

                    Enum.TryParse(reader.ReadLine(), out TimeFrame);

                    string server = reader.ReadLine();

                    Enum.TryParse(server.Split('&')[0], out ServerType);

                    if (server.Split('&').Length > 1)
                    {
                        ServerName = server.Split('&')[1];
                    }
                    else
                    {
                        ServerName = ServerType.ToString();
                    }

                    _emulatorIsOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out CandleMarketDataType);
                    CandleCreateMethodType = reader.ReadLine();

                    try
                    {
                        Enum.TryParse(reader.ReadLine(), out CommissionType);
                        CommissionValue = reader.ReadLine().ToDecimal();
                        SaveTradesInCandles = Convert.ToBoolean(reader.ReadLine());
                        _eventsIsOn = Convert.ToBoolean(reader.ReadLine());

                        string seriesName = reader.ReadLine();
                        CandleSeriesRealization = CandleFactory.CreateCandleSeriesRealization(seriesName);
                        CandleSeriesRealization.Init(_startProgram);
                        CandleSeriesRealization.SetSaveString(reader.ReadLine());
                    }
                    catch
                    {
                        // ignore
                    }



                    while (reader.EndOfStream == false)
                    {
                        string str = reader.ReadLine();

                        if (string.IsNullOrEmpty(str))
                        {
                            continue;
                        }
                        ActivatedSecurity sec = new ActivatedSecurity();
                        sec.SetFromStr(str);
                        SecuritiesNames.Add(sec);
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                _candleCreateMethodType = "Simple";
                CandleSeriesRealization = CandleFactory.CreateCandleSeriesRealization("Simple");
                CandleSeriesRealization.Init(_startProgram);

                // ignore
            }
        }

        //// <summary>
        /// Remove tab and all child structures
        /// </summary>
        public void Delete()
        {
            if (_ui != null)
            {
                _ui.Close();
            }

            for (int i = 0; Tabs != null && i < Tabs.Count; i++)
            {
                Tabs[i].Clear();
                Tabs[i].Delete();
            }

            if (File.Exists(@"Engine\" + TabName + @"ScreenerSet.txt"))
            {
                File.Delete(@"Engine\" + TabName + @"ScreenerSet.txt");
            }

            if (File.Exists(@"Engine\" + TabName + @"ScreenerTabSet.txt"))
            {
                File.Delete(@"Engine\" + TabName + @"ScreenerTabSet.txt");
            }

            if (File.Exists(@"Engine\" + TabName + @"ScreenerIndicators.txt"))
            {
                File.Delete(@"Engine\" + TabName + @"ScreenerIndicators.txt");
            }

            if (_positionViewer != null)
            {
                _positionViewer.UserSelectActionEvent -= _globalController_UserSelectActionEvent;
                _positionViewer.UserClickOnPositionShowBotInTableEvent -= _globalPositionViewer_UserClickOnPositionShowBotInTableEvent;
                _positionViewer.Delete();
            }

            if (_startProgram == StartProgram.IsTester)
            {
                List<IServer> servers = ServerMaster.GetServers();

                if (servers != null &&
                    servers.Count > 0
                    && servers[0].ServerType == ServerType.Tester)
                {
                    ((TesterServer)servers[0]).TestingStartEvent -= BotTabScreener_TestingStartEvent;
                    ((TesterServer)servers[0]).TestingEndEvent -= BotTabScreener_TestingEndEvent;
                }
            }

            if (TabDeletedEvent != null)
            {
                TabDeletedEvent();
            }
        }

        /// <summary>
        /// Get journal
        /// </summary>
        public List<Journal.Journal> GetJournals()
        {
            try
            {
                List<Journal.Journal> journals = new List<Journal.Journal>();

                for (int i = 0; i < Tabs.Count; i++)
                {
                    journals.Add(Tabs[i].GetJournal());
                }

                return journals;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// Whether the connector is connected to download data
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (Tabs == null ||
                    Tabs.Count == 0)
                {
                    return false;
                }

                List<BotTabSimple> tabs = Tabs;

                for (int i2 = 0; i2 < tabs.Count; i2++)
                {
                    if (tabs[i2] == null
                        || tabs[i2].IsConnected == false)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private void BotTabScreener_TestingEndEvent()
        {
            if (TestOverEvent != null)
            {
                try
                {
                    TestOverEvent();
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void BotTabScreener_TestingStartEvent()
        {
            if (TestStartEvent != null)
            {
                try
                {
                    TestStartEvent();
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// testing finished
        /// </summary>
        public event Action TestOverEvent;

        /// <summary>
        /// testing started
        /// </summary>
        public event Action TestStartEvent;

        #endregion

        #region Working with tabs

        /// <summary>
        /// Trade portfolio name
        /// </summary>
        public string PortfolioName;

        /// <summary>
        /// Class of papers in the screener
        /// </summary>
        public string SecuritiesClass;

        /// <summary>
        /// Names of securities added to the connection
        /// </summary>
        public List<ActivatedSecurity> SecuritiesNames = new List<ActivatedSecurity>();

        /// <summary>
        /// Timeframe
        /// </summary>
        public TimeFrame TimeFrame = TimeFrame.Min1;

        /// <summary>
        /// Server type
        /// </summary>
        public ServerType ServerType;

        /// <summary>
        /// Server name in multi-connect regime
        /// </summary>
        public string ServerName;

        /// <summary>
        /// Is the emulator enabled
        /// </summary>
        public bool EmulatorIsOn
        {
            get
            {
                return _emulatorIsOn;
            }
            set
            {
                if (_emulatorIsOn == value)
                {
                    return;
                }

                for (int i = 0; Tabs != null && i < Tabs.Count; i++)
                {
                    try
                    {
                        Tabs[i].EmulatorIsOn = value;
                    }
                    catch
                    {
                        // ignore. Не все вкладки запустились
                    }
                }

                _emulatorIsOn = value;
                SaveSettings();
            }
        }
        private bool _emulatorIsOn;

        /// <summary>
        /// Data type for calculating candles in a series of candles
        /// </summary>
        public CandleMarketDataType CandleMarketDataType;

        /// <summary>
        /// Method for creating candles
        /// </summary>
        public string CandleCreateMethodType
        {
            get { return _candleCreateMethodType; }
            set
            {
                string newType = value;

                if (newType == _candleCreateMethodType)
                {
                    return;
                }

                if (CandleSeriesRealization != null)
                {
                    CandleSeriesRealization.Delete();
                    CandleSeriesRealization = null;
                }
                _candleCreateMethodType = newType;
                CandleSeriesRealization = CandleFactory.CreateCandleSeriesRealization(newType);
                CandleSeriesRealization.Init(_startProgram);

                SaveSettings();

            }
        }
        private string _candleCreateMethodType;

        /// <summary>
        /// Commission type for positions
        /// </summary>
        public CommissionType CommissionType;

        /// <summary>
        /// Commission amount
        /// </summary>
        public decimal CommissionValue;

        /// <summary>
        /// Whether it is necessary to save trades inside the candle they belong to
        /// </summary>
        public bool SaveTradesInCandles;

        public ACandlesSeriesRealization CandleSeriesRealization;

        public bool IsLoadTabs = false;

        public bool NeedToReloadTabs = false;

        /// <summary>
        /// Reload tabs
        /// </summary>
        public void TryReLoadTabs()
        {
            try
            {
                if (NeedToReloadTabs == false)
                {
                    return;
                }

                if (_tabIsLoad == false)
                {
                    return;
                }

                if (TabsReadyToLoad() == false)
                {
                    return;
                }

                // 1 remove unwanted tabs

                bool deleteSomeTabs = false;

                for (int i = 0; i < Tabs.Count; i++)
                {
                    if (TabIsAlive(SecuritiesNames, TimeFrame, Tabs[i]) == false)
                    {
                        string chartName = Tabs[i].TabName + "_Engine";

                        for (int i2 = 0; _chartEngines != null && i2 < _chartEngines.Count; i2++)
                        {
                            if (chartName == _chartEngines[i2].NameStrategyUniq)
                            {
                                _chartEngines[i2].CloseGui();
                                break;
                            }
                        }

                        Tabs[i].Clear();
                        Tabs[i].Delete();
                        Tabs.RemoveAt(i);
                        i--;
                        deleteSomeTabs = true;
                    }
                }

                if (deleteSomeTabs)
                {
                    RePaintSecuritiesGrid();
                }

                // 2 update data in tabs

                //for (int i = 0; i < Tabs.Count; i++)
                //{
                //    UpdateTabSettings(Tabs[i]);
                //}

                // 3 create missing tabs
                IsLoadTabs = true;
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    int tabCount = Tabs.Count;

                    TryCreateTab(SecuritiesNames[i], TimeFrame, Tabs);

                    if (tabCount != Tabs.Count)
                    {
                        UpdateTabSettings(Tabs[Tabs.Count - 1]);
                        PaintNewRow();

                        if (NewTabCreateEvent != null)
                        {
                            NewTabCreateEvent(Tabs[Tabs.Count - 1]);
                        }
                    }
                }
                IsLoadTabs = false;
                ReloadIndicatorsOnTabs();

                if (Tabs.Count != 0)
                {
                    Tabs[0].IndicatorManuallyCreateEvent -= BotTabScreener_IndicatorManuallyCreateEvent;
                    Tabs[0].IndicatorManuallyCreateEvent += BotTabScreener_IndicatorManuallyCreateEvent;

                    Tabs[0].IndicatorManuallyDeleteEvent -= BotTabScreener_IndicatorManuallyDeleteEvent;
                    Tabs[0].IndicatorManuallyDeleteEvent += BotTabScreener_IndicatorManuallyDeleteEvent;

                    Tabs[0].IndicatorUpdateEvent -= BotTabScreener_IndicatorUpdateEvent;
                    Tabs[0].IndicatorUpdateEvent += BotTabScreener_IndicatorUpdateEvent;
                }

                for (int i = 0; Tabs != null && i < Tabs.Count; i++)
                {
                    UpdateTabSettings(Tabs[i]);
                }

                SaveTabs();

                SetJournalsInPosViewer();

                NeedToReloadTabs = false;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Update settings for tabs
        /// </summary>
        private void UpdateTabSettings(BotTabSimple tab)
        {
            bool haveNewSettings = false;

            if (tab.Connector.PortfolioName != PortfolioName)
            {
                tab.Connector.PortfolioName = PortfolioName;
                haveNewSettings = true;
            }

            if (tab.Connector.ServerType != ServerType)
            {
                tab.Connector.ServerType = ServerType;
                haveNewSettings = true;
            }

            if (tab.Connector.ServerFullName != ServerName)
            {
                tab.Connector.ServerFullName = ServerName;
                haveNewSettings = true;
            }

            if (tab.Connector.EmulatorIsOn != _emulatorIsOn)
            {
                tab.Connector.EmulatorIsOn = _emulatorIsOn;
                haveNewSettings = true;
            }

            if (tab.Connector.CandleMarketDataType != CandleMarketDataType)
            {
                tab.Connector.CandleMarketDataType = CandleMarketDataType;
                haveNewSettings = true;
            }

            if (tab.Connector.CandleCreateMethodType != CandleCreateMethodType)
            {
                tab.Connector.CandleCreateMethodType = CandleCreateMethodType;
                haveNewSettings = true;
            }

            if (tab.Connector.TimeFrame != this.TimeFrame)
            {
                tab.Connector.TimeFrame = this.TimeFrame;
                haveNewSettings = true;
            }

            if (tab.Connector.TimeFrameBuilder.CandleSeriesRealization.GetSaveString() != CandleSeriesRealization.GetSaveString())
            {
                tab.Connector.TimeFrameBuilder.CandleSeriesRealization.SetSaveString(CandleSeriesRealization.GetSaveString());
                haveNewSettings = true;
            }

            if (tab.Connector.SaveTradesInCandles != SaveTradesInCandles)
            {
                tab.Connector.SaveTradesInCandles = SaveTradesInCandles;
                haveNewSettings = true;
            }

            if (tab.Connector.CommissionType != CommissionType)
            {
                tab.Connector.CommissionType = CommissionType;
                haveNewSettings = true;
            }

            if (tab.Connector.CommissionValue != CommissionValue)
            {
                tab.Connector.CommissionValue = CommissionValue;
                haveNewSettings = true;
            }

            if (tab.CommissionType != CommissionType)
            {
                tab.CommissionType = CommissionType;
                haveNewSettings = true;
            }

            if (tab.CommissionValue != CommissionValue)
            {
                tab.CommissionValue = CommissionValue;
                haveNewSettings = true;
            }

            if (tab.Connector.ServerUid != ServerUid)
            {
                tab.Connector.ServerUid = ServerUid;
            }

            tab.IsCreatedByScreener = true;



            if (haveNewSettings)
            {
                tab.Connector.ReconnectHard();
            }
        }

        /// <summary>
        /// Try creating a tab with these options
        /// </summary>
        private void TryCreateTab(ActivatedSecurity sec, TimeFrame frame, List<BotTabSimple> curTabs)
        {
            if (sec.IsOn == false)
            {
                return;
            }

            if (curTabs.Find(tab => tab.Connector.SecurityName == sec.SecurityName) != null)
            {
                return;
            }

            string nameStart = curTabs.Count + " " + TabName;

            int numerator = 1;

            while (Tabs.Find(t => t.TabName == nameStart) != null)
            {
                nameStart = numerator + " " + TabName;
                numerator++;
            }

            BotTabSimple newTab = new BotTabSimple(nameStart, _startProgram);
            newTab.Connector.SecurityName = sec.SecurityName;
            newTab.Connector.SecurityClass = sec.SecurityClass;
            newTab.Connector.ServerUid = ServerUid;
            newTab.TimeFrameBuilder.TimeFrame = frame;
            newTab.Connector.PortfolioName = PortfolioName;
            newTab.Connector.ServerType = ServerType;
            newTab.Connector.ServerFullName = ServerName;
            newTab.Connector.EmulatorIsOn = _emulatorIsOn;
            newTab.Connector.CandleMarketDataType = CandleMarketDataType;
            newTab.Connector.CandleCreateMethodType = CandleCreateMethodType;
            newTab.Connector.TimeFrame = frame;
            newTab.Connector.TimeFrameBuilder.CandleSeriesRealization.SetSaveString(CandleSeriesRealization.GetSaveString());
            newTab.Connector.TimeFrameBuilder.CandleSeriesRealization.OnStateChange(CandleSeriesState.ParametersChange);
            newTab.Connector.SaveTradesInCandles = SaveTradesInCandles;
            newTab.CommissionType = CommissionType;
            newTab.CommissionValue = CommissionValue;
            newTab.IsCreatedByScreener = true;

            curTabs.Add(newTab);

            SubscribeOnTab(newTab);
        }

        /// <summary>
        /// Check if the tab exists
        /// </summary>
        private bool TabIsAlive(List<ActivatedSecurity> securities, TimeFrame frame, BotTabSimple tab)
        {
            if (StartProgram == StartProgram.IsOsTrader)
            {
                if (tab.Connector.ServerFullName != ServerName)
                {
                    return false;
                }
            }

            ActivatedSecurity sec = null;

            for (int i = 0; i < securities.Count; i++)
            {
                if (securities[i].SecurityName == tab.Connector.SecurityName
                    && securities[i].SecurityClass == tab.Connector.SecurityClass)
                {
                    sec = securities[i];
                    break;
                }
            }

            if (sec == null)
            {
                return false;
            }

            if (sec.IsOn == false)
            {
                return false;
            }

            if (tab.Connector.TimeFrame != frame)
            {
                return false;
            }

            if (tab.TimeFrameBuilder.CandleSeriesRealization.GetType().Name
                != CandleSeriesRealization.GetType().Name)
            {
                return false;
            }

            for (int i = 0; i < tab.TimeFrameBuilder.CandleSeriesRealization.Parameters.Count; i++)
            {
                ICandleSeriesParameter paramInTab = tab.TimeFrameBuilder.CandleSeriesRealization.Parameters[i];
                ICandleSeriesParameter paramInScreener = CandleSeriesRealization.Parameters[i];

                if (paramInTab.Type == CandlesParameterType.StringCollection
                    && ((CandlesParameterString)paramInTab).ValueString != ((CandlesParameterString)paramInScreener).ValueString)
                {
                    return false;
                }
                else if (paramInTab.Type == CandlesParameterType.Int
                    && ((CandlesParameterInt)paramInTab).ValueInt != ((CandlesParameterInt)paramInScreener).ValueInt)
                {
                    return false;
                }
                else if (paramInTab.Type == CandlesParameterType.Bool
                    && ((CandlesParameterBool)paramInTab).ValueBool != ((CandlesParameterBool)paramInScreener).ValueBool)
                {
                    return false;
                }
                else if (paramInTab.Type == CandlesParameterType.Decimal
                    && ((CandlesParameterDecimal)paramInTab).ValueDecimal != ((CandlesParameterDecimal)paramInScreener).ValueDecimal)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Is it possible to create tabs now
        /// </summary>
        private bool TabsReadyToLoad()
        {
            if (SecuritiesNames.Count == 0)
            {
                return false;
            }

            if (String.IsNullOrEmpty(PortfolioName))
            {
                return false;
            }

            if (ServerType == ServerType.None)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Correct program removal of security from the screener
        /// </summary>
        public void RemoveTabBySecurityName(string securityName, string securityClass)
        {
            bool securityDeleted = false;

            for (int i = 0; i < SecuritiesNames.Count; i++)
            {
                if (SecuritiesNames[i].SecurityName == securityName
                    && SecuritiesNames[i].SecurityClass == securityClass)
                {
                    SecuritiesNames.RemoveAt(i);
                    NeedToReloadTabs = true;
                    securityDeleted = true;
                    break;
                }
            }

            if (securityDeleted == true)
            {
                SaveSettings();
            }
        }

        public BotManualControl ManualPositionSupportFromOptimizer;

        #endregion

        #region Drawing and working with the GUI

        /// <summary>
        /// Show GUI
        /// </summary>
        public void ShowDialog()
        {
            try
            {
                if (ServerMaster.GetServers() == null ||
                    ServerMaster.GetServers().Count == 0)
                {
                    AlertMessageSimpleUi uiMessage = new AlertMessageSimpleUi(OsLocalization.Market.Message1);
                    uiMessage.Show();
                    return;
                }

                if (StartProgram == StartProgram.IsTester)
                {
                    IServer server = ServerMaster.GetServers()[0];

                    if (server.Portfolios == null
                        ||
                        server.Portfolios.Count == 0)
                    {
                        return;
                    }
                }

                if (_ui == null)
                {
                    _ui = new BotTabScreenerUi(this);
                    _ui.LogMessageEvent += SendNewLogMessage;
                    _ui.Closed += _ui_Closed;
                    _ui.Show();
                }
                else
                {
                    if (_ui.WindowState == System.Windows.WindowState.Minimized)
                    {
                        _ui.WindowState = System.Windows.WindowState.Normal;
                    }

                    _ui.Activate();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _ui_Closed(object sender, EventArgs e)
        {
            try
            {
                _ui.LogMessageEvent -= SendNewLogMessage;
                _ui.Closed -= _ui_Closed;
                _ui = null;

                if (DialogClosed != null)
                {
                    DialogClosed();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private BotTabScreenerUi _ui;

        public event Action DialogClosed;

        /// <summary>
        /// Things to call individual windows by tool
        /// </summary>
        List<CandleEngine> _chartEngines = new List<CandleEngine>();

        private GlobalPositionViewer _positionViewer;

        /// <summary>
        /// Show GUI
        /// </summary>
        public void ShowChart(int tabNum)
        {
            try
            {
                string botName = Tabs[tabNum].TabName + "_Engine";

                for (int i = 0; i < _chartEngines.Count; i++)
                {
                    if (_chartEngines[i].NameStrategyUniq == botName)
                    {
                        _chartEngines[i].ShowChartDialog();
                        return;
                    }
                }

                CandleEngine bot = new CandleEngine(botName, _startProgram);

                BotTabSimple myTab = Tabs[tabNum];

                //bot.TabCreate(BotTabType.Simple);
                bot.GetTabs().Clear();
                bot.GetTabs().Add(myTab);
                bot.TabsSimple[0] = myTab;
                bot.ActiveTab = myTab;

                bot.ChartClosedEvent += (string nameBot) =>
                {
                    for (int i = 0; i < _chartEngines.Count; i++)
                    {
                        if (_chartEngines[i].NameStrategyUniq == nameBot)
                        {
                            _chartEngines.RemoveAt(i);
                            break;
                        }
                    }
                };

                _chartEngines.Add(bot);
                bot.ShowChartDialog();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// start drawing this robot
        /// </summary> 
        public void StartPaint(WindowsFormsHost host,
            WindowsFormsHost hostOpenDeals,
            WindowsFormsHost hostCloseDeals)
        {
            try
            {
                _host = host;
                RePaintSecuritiesGrid();

                if (_positionViewer == null)
                {
                    _positionViewer = new GlobalPositionViewer(StartProgram);
                    _positionViewer.LogMessageEvent += SendNewLogMessage;
                    _positionViewer.UserSelectActionEvent += _globalController_UserSelectActionEvent;
                    _positionViewer.UserClickOnPositionShowBotInTableEvent += _globalPositionViewer_UserClickOnPositionShowBotInTableEvent;

                }

                SetJournalsInPosViewer();
                _positionViewer.StartPaint(hostOpenDeals, hostCloseDeals);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Stop drawing this robot
        /// </summary>
        public void StopPaint()
        {
            try
            {
                if (_host == null)
                {
                    return;
                }

                if (_host.Dispatcher.CheckAccess() == false)
                {
                    _host.Dispatcher.Invoke(new Action(StopPaint));
                    return;
                }

                if (_positionViewer != null)
                {
                    _positionViewer.StopPaint();
                }

                _host.Child = null;
                _host = null;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void SetJournalsInPosViewer()
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            if (_positionViewer == null)
            {
                return;
            }

            try
            {
                _positionViewer.ClearJournalsArray();

                List<Journal.Journal> journals = new List<Journal.Journal>();

                for (int i = 0; i < Tabs.Count; i++)
                {
                    if (Tabs[i] != null)
                    {
                        Journal.Journal journal = Tabs[i].GetJournal();
                        journals.Add(journal);
                    }
                }

                if (journals.Count > 0)
                {
                    _positionViewer.SetJournals(journals);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Grid screener date
        /// </summary>
        public DataGridView SecuritiesDataGrid;

        /// <summary>
        /// Host where the screener grid is stored
        /// </summary>
        WindowsFormsHost _host;

        /// <summary>
        /// Create table for screener
        /// </summary>
        private void CreateSecuritiesGrid()
        {
            // number, class, instrument code, last, bid and ask prices, number of positions, Chart

            DataGridView newGrid =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            newGrid.ScrollBars = ScrollBars.Vertical;
            DataGridViewCellStyle style = newGrid.DefaultCellStyle;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "#";
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum1 = new DataGridViewColumn();
            colum1.CellTemplate = cell0;
            colum1.HeaderText = OsLocalization.Trader.Label166;
            colum1.ReadOnly = true;
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum1);

            DataGridViewColumn colum3 = new DataGridViewColumn();
            colum3.CellTemplate = cell0;
            colum3.HeaderText = OsLocalization.Trader.Label168;
            colum3.ReadOnly = true;
            colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum3);

            DataGridViewColumn colum4 = new DataGridViewColumn();
            colum4.CellTemplate = cell0;
            colum4.HeaderText = "Last";
            colum4.ReadOnly = true;
            colum4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum4);

            DataGridViewColumn colum5 = new DataGridViewColumn();
            colum5.CellTemplate = cell0;
            colum5.HeaderText = "Bid";
            colum5.ReadOnly = true;
            colum5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum5);

            DataGridViewColumn colum6 = new DataGridViewColumn();
            colum6.CellTemplate = cell0;
            colum6.HeaderText = "Ask";
            colum6.ReadOnly = true;
            colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum6);

            DataGridViewColumn colum7 = new DataGridViewColumn();
            colum7.CellTemplate = cell0;
            colum7.HeaderText = "Pos. (curr/total)";
            colum7.ReadOnly = true;
            colum7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum7);

            DataGridViewButtonColumn colum8 = new DataGridViewButtonColumn();
            //colum6.CellTemplate = cell0;
            colum8.ReadOnly = false;
            colum8.Width = 70;
            newGrid.Columns.Add(colum8);

            SecuritiesDataGrid = newGrid;

            newGrid.Click += NewGrid_Click;
            SecuritiesDataGrid.DataError += SecuritiesDataGrid_DataError;
        }

        private void SecuritiesDataGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private int _previousActiveRow;

        /// <summary>
        /// Click on table
        /// </summary>
        private void NewGrid_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;

                if (mouse.Button == MouseButtons.Right)
                {
                    // refer to the creation of the settings window
                    CreateGridDialog(mouse);
                }
                if (mouse.Button == MouseButtons.Left)
                {
                    if (SecuritiesDataGrid.ContextMenuStrip != null)
                    {
                        SecuritiesDataGrid.ContextMenuStrip = null;
                    }

                    // send to watch the chart
                    if (SecuritiesDataGrid.SelectedCells == null ||
                        SecuritiesDataGrid.SelectedCells.Count == 0)
                    {
                        return;
                    }
                    int tabRow = SecuritiesDataGrid.SelectedCells[0].RowIndex;
                    int tabColumn = SecuritiesDataGrid.SelectedCells[0].ColumnIndex;

                    if (tabColumn == 7)
                    {
                        ShowChart(tabRow);
                        SecuritiesDataGrid.Rows[tabRow].Cells[0].Selected = true;
                    }

                    if (_previousActiveRow < SecuritiesDataGrid.Rows.Count)
                    {
                        SecuritiesDataGrid.Rows[_previousActiveRow].DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(154, 156, 158);
                    }

                    SecuritiesDataGrid.Rows[tabRow].DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(255, 255, 255);
                    _previousActiveRow = tabRow;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Redraw the grid
        /// </summary>
        private void RePaintSecuritiesGrid()
        {
            try
            {
                if (_host == null)
                {
                    return;
                }

                if (_host.Dispatcher.CheckAccess() == false)
                {
                    _host.Dispatcher.Invoke(new Action(RePaintSecuritiesGrid));
                    return;
                }

                int showRow = SecuritiesDataGrid.FirstDisplayedScrollingRowIndex;

                SecuritiesDataGrid.Rows.Clear();

                for (int i = 0; i < Tabs.Count; i++)
                {
                    SecuritiesDataGrid.Rows.Add(GetRowFromTab(Tabs[i], i));
                    PaintLastBidAsk(Tabs[i], SecuritiesDataGrid);
                }

                if (_host != null)
                {
                    _host.Child = SecuritiesDataGrid;
                }

                if (showRow > 0 &&
                    showRow < SecuritiesDataGrid.Rows.Count)
                {
                    SecuritiesDataGrid.FirstDisplayedScrollingRowIndex = showRow;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Draw a new row
        /// </summary>
        private void PaintNewRow()
        {
            try
            {
                if (_host == null)
                {
                    return;
                }

                if (_host.Dispatcher.CheckAccess() == false)
                {
                    _host.Dispatcher.Invoke(new Action(PaintNewRow));
                    return;
                }

                SecuritiesDataGrid.Rows.Add(GetRowFromTab(Tabs[Tabs.Count - 1], Tabs.Count - 1));
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Take row by tab for grid
        /// </summary>
        private DataGridViewRow GetRowFromTab(BotTabSimple tab, int num)
        {
            // Num, Class, Type, Sec code, Last, Bid, Ask, Positions count, Chart 

            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = num;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = tab.Connector.SecurityClass;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = tab.Connector.SecurityName;

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            DataGridViewButtonCell button = new DataGridViewButtonCell();
            button.Value = OsLocalization.Trader.Label172;
            nRow.Cells.Add(button);

            return nRow;
        }

        /// <summary>
        /// Create settings popup for grid
        /// </summary>
        private void CreateGridDialog(MouseEventArgs mouse)
        {
            try
            {

                if (Tabs.Count == 0)
                {
                    return;
                }

                BotTabSimple tab = Tabs[0];

                System.Windows.Forms.ContextMenuStrip menu = tab.GetContextDialog();

                SecuritiesDataGrid.ContextMenuStrip = menu;

                SecuritiesDataGrid.ContextMenuStrip.Show(SecuritiesDataGrid, new System.Drawing.Point(mouse.X, mouse.Y));
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// The user clicked on the table with positions
        /// </summary>
        /// <param name="botTabName">The name of the tab that was active when the event was generated</param>
        private void _globalPositionViewer_UserClickOnPositionShowBotInTableEvent(string botTabName)
        {
            if (UserClickOnPositionShowBotInTableEvent != null)
            {
                UserClickOnPositionShowBotInTableEvent(botTabName);
            }
        }

        public event Action<string> UserClickOnPositionShowBotInTableEvent;

        public event Action<Position, SignalType> UserSelectActionEvent;

        /// <summary>
        /// The user has selected a position
        /// </summary>
        /// <param name="pos">position</param>
        /// <param name="signal">Action signal</param>
        private void _globalController_UserSelectActionEvent(Position pos, SignalType signal)
        {
            if (UserSelectActionEvent != null)
            {
                UserSelectActionEvent(pos, signal);
            }
        }

        #endregion

        #region Position tracking settings

        public void ShowManualControlDialog()
        {
            try
            {
                if (Tabs.Count == 0)
                {
                    SendNewLogMessage(OsLocalization.Trader.Label231, LogMessageType.Error);
                    return;
                }

                Tabs[0].ShowManualControlDialog();
                SynchFirstTab();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Creating / deleting / storing indicators

        /// <summary>
        /// create indicator / 
        /// создать индикатор
        /// </summary>
        /// <param name="indicator">indicator / индикатор</param>
        /// <param name="nameArea">the name of the area on which it will be placed. Default: "Prime" / название области на которую он будет помещён. По умолчанию: "Prime"</param>
        /// <returns></returns>
        public void CreateCandleIndicator(int num, string type, List<string> param, string nameArea = "Prime")
        {
            try
            {

                if (_indicators.Find(ind => ind.Num == num) != null)
                {
                    NeedToReloadTabs = true;
                    return;
                }

                IndicatorOnTabs indicator = new IndicatorOnTabs();
                indicator.Num = num;
                indicator.Type = type;
                indicator.NameArea = nameArea;

                if (param != null)
                {
                    indicator.Parameters = param;
                }

                _indicators.Add(indicator);

                SaveIndicators();
                ReloadIndicatorsOnTabs();

            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// загрузить индикаторы
        /// </summary>
        private void LoadIndicators()
        {
            if (!File.Exists(@"Engine\" + TabName + @"ScreenerIndicators.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"ScreenerIndicators.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string str = reader.ReadLine();

                        IndicatorOnTabs ind = new IndicatorOnTabs();
                        ind.SetFromStr(str);
                        _indicators.Add(ind);
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// сохранить индикаторы
        /// </summary>
        private void SaveIndicators()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"ScreenerIndicators.txt", false))
                {
                    for (int i = 0; i < _indicators.Count; i++)
                    {
                        writer.WriteLine(_indicators[i].GetSaveStr());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// индикаторы на вкладках
        /// </summary>
        public List<IndicatorOnTabs> _indicators = new List<IndicatorOnTabs>();

        /// <summary>
        /// активировать индикаторы
        /// </summary>
        public void ReloadIndicatorsOnTabs()
        {
            for (int i = 0; i < _indicators.Count; i++)
            {
                CreateIndicatorOnTabs(_indicators[i]);
            }
        }

        public void UpdateIndicatorsParameters()
        {
            try
            {
                for (int i1 = 0; i1 < _indicators.Count; i1++)
                {
                    IndicatorOnTabs ind = (IndicatorOnTabs)_indicators[i1];

                    for (int i = 0; i < Tabs.Count; i++)
                    {
                        Aindicator newIndicator = IndicatorsFactory.CreateIndicatorByName(ind.Type, ind.Num + ind.Type + TabName, false);
                        newIndicator.CanDelete = ind.CanDelete;

                        try
                        {
                            if (ind.Parameters.Count == newIndicator.Parameters.Count)
                            {
                                if (ind.Parameters != null && ind.Parameters.Count != 0)
                                {
                                    for (int i2 = 0; i2 < ind.Parameters.Count; i2++)
                                    {
                                        if (newIndicator.Parameters[i2].Type == IndicatorParameterType.Int)
                                        {
                                            ((IndicatorParameterInt)newIndicator.Parameters[i2]).ValueInt = Convert.ToInt32(ind.Parameters[i2]);
                                        }
                                        if (newIndicator.Parameters[i2].Type == IndicatorParameterType.Decimal)
                                        {
                                            ((IndicatorParameterDecimal)newIndicator.Parameters[i2]).ValueDecimal = ind.Parameters[i2].ToDecimal();
                                        }
                                        if (newIndicator.Parameters[i2].Type == IndicatorParameterType.Bool)
                                        {
                                            ((IndicatorParameterBool)newIndicator.Parameters[i2]).ValueBool = Convert.ToBoolean(ind.Parameters[i2]);
                                        }
                                        if (newIndicator.Parameters[i2].Type == IndicatorParameterType.String)
                                        {
                                            ((IndicatorParameterString)newIndicator.Parameters[i2]).ValueString = ind.Parameters[i2];
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception error)
                        {
                            SendNewLogMessage(error.ToString(), LogMessageType.Error);
                        }

                        newIndicator = (Aindicator)Tabs[i].CreateCandleIndicator(newIndicator, ind.NameArea);
                        newIndicator.CanDelete = ind.CanDelete;

                        try
                        {
                            bool parametersChanged = false;

                            if (ind.Parameters.Count == newIndicator.Parameters.Count)
                            {

                                if (ind.Parameters != null && ind.Parameters.Count != 0)
                                {
                                    for (int i2 = 0; i2 < ind.Parameters.Count; i2++)
                                    {
                                        if (newIndicator.Parameters[i2].Type == IndicatorParameterType.Int
                                            && ((IndicatorParameterInt)newIndicator.Parameters[i2]).ValueInt != Convert.ToInt32(ind.Parameters[i2]))
                                        {
                                            ((IndicatorParameterInt)newIndicator.Parameters[i2]).ValueInt = Convert.ToInt32(ind.Parameters[i2]);
                                            parametersChanged = true;
                                        }
                                        if (newIndicator.Parameters[i2].Type == IndicatorParameterType.Decimal
                                            && ((IndicatorParameterDecimal)newIndicator.Parameters[i2]).ValueDecimal != ind.Parameters[i2].ToDecimal())
                                        {
                                            ((IndicatorParameterDecimal)newIndicator.Parameters[i2]).ValueDecimal = ind.Parameters[i2].ToDecimal();
                                            parametersChanged = true;
                                        }
                                        if (newIndicator.Parameters[i2].Type == IndicatorParameterType.Bool
                                            && ((IndicatorParameterBool)newIndicator.Parameters[i2]).ValueBool != Convert.ToBoolean(ind.Parameters[i2]))
                                        {
                                            ((IndicatorParameterBool)newIndicator.Parameters[i2]).ValueBool = Convert.ToBoolean(ind.Parameters[i2]);
                                            parametersChanged = true;
                                        }
                                        if (newIndicator.Parameters[i2].Type == IndicatorParameterType.String
                                            && ((IndicatorParameterString)newIndicator.Parameters[i2]).ValueString != ind.Parameters[i2])
                                        {
                                            ((IndicatorParameterString)newIndicator.Parameters[i2]).ValueString = ind.Parameters[i2];
                                            parametersChanged = true;
                                        }
                                    }
                                }
                            }

                            if (parametersChanged)
                            {
                                newIndicator.Reload();
                                newIndicator.Save();
                            }
                        }
                        catch (Exception error)
                        {
                            SendNewLogMessage(error.ToString(), LogMessageType.Error);
                        }
                        newIndicator.Save();
                    }
                }

                SaveIndicators();
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// создать индикатор для вкладок
        /// </summary>
        private void CreateIndicatorOnTabs(IndicatorOnTabs ind)
        {
            for (int i = 0; i < Tabs.Count; i++)
            {
                Aindicator newIndicator = IndicatorsFactory.CreateIndicatorByName(ind.Type, ind.Num + ind.Type + TabName, false);
                newIndicator.CanDelete = ind.CanDelete;

                try
                {
                    if (ind.Parameters.Count == newIndicator.Parameters.Count)
                    {
                        if (ind.Parameters != null && ind.Parameters.Count != 0)
                        {
                            for (int i2 = 0; i2 < ind.Parameters.Count; i2++)
                            {
                                if (newIndicator.Parameters[i2].Type == IndicatorParameterType.Int)
                                {
                                    ((IndicatorParameterInt)newIndicator.Parameters[i2]).ValueInt = Convert.ToInt32(ind.Parameters[i2]);
                                }
                                if (newIndicator.Parameters[i2].Type == IndicatorParameterType.Decimal)
                                {
                                    ((IndicatorParameterDecimal)newIndicator.Parameters[i2]).ValueDecimal = ind.Parameters[i2].ToDecimal();
                                }
                                if (newIndicator.Parameters[i2].Type == IndicatorParameterType.Bool)
                                {
                                    ((IndicatorParameterBool)newIndicator.Parameters[i2]).ValueBool = Convert.ToBoolean(ind.Parameters[i2]);
                                }
                                if (newIndicator.Parameters[i2].Type == IndicatorParameterType.String)
                                {
                                    ((IndicatorParameterString)newIndicator.Parameters[i2]).ValueString = ind.Parameters[i2];
                                }
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }

                newIndicator = (Aindicator)Tabs[i].CreateCandleIndicator(newIndicator, ind.NameArea);
                newIndicator.CanDelete = ind.CanDelete;
                newIndicator.Save();
            }
        }

        /// <summary>
        /// синхронизировать первую вкладку с остальными
        /// </summary>
        public void SynchFirstTab()
        {
            try
            {
                if (Tabs.Count <= 1)
                {
                    return;
                }

                BotTabSimple firstTab = Tabs[0];

                for (int i = 1; i < Tabs.Count; i++)
                {
                    SynchTabsIndicators(firstTab, Tabs[i]);
                }

                BotManualControl control = Tabs[0].ManualPositionSupport;

                int startIndex = 1;

                if (ManualPositionSupportFromOptimizer != null)
                {
                    control = ManualPositionSupportFromOptimizer;
                    startIndex = 0;
                }

                for (int i = startIndex; i < Tabs.Count; i++)
                {
                    SyncTabsManualPositionControl(control, Tabs[i]);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SyncTabsManualPositionControl(BotManualControl control, BotTabSimple second)
        {
            second.ManualPositionSupport.SecondToClose = control.SecondToClose;
            second.ManualPositionSupport.SecondToOpen = control.SecondToOpen;
            second.ManualPositionSupport.DoubleExitIsOn = control.DoubleExitIsOn;
            second.ManualPositionSupport.DoubleExitSlippage = control.DoubleExitSlippage;
            second.ManualPositionSupport.ProfitDistance = control.ProfitDistance;
            second.ManualPositionSupport.ProfitIsOn = control.ProfitIsOn;
            second.ManualPositionSupport.ProfitSlippage = control.ProfitSlippage;
            second.ManualPositionSupport.SecondToCloseIsOn = control.SecondToCloseIsOn;
            second.ManualPositionSupport.SecondToOpenIsOn = control.SecondToOpenIsOn;
            second.ManualPositionSupport.SetbackToCloseIsOn = control.SetbackToCloseIsOn;
            second.ManualPositionSupport.SetbackToClosePosition = control.SetbackToOpenPosition;
            second.ManualPositionSupport.SetbackToOpenIsOn = control.SetbackToOpenIsOn;
            second.ManualPositionSupport.SetbackToOpenPosition = control.SetbackToOpenPosition;
            second.ManualPositionSupport.StopDistance = control.StopDistance;
            second.ManualPositionSupport.StopIsOn = control.StopIsOn;
            second.ManualPositionSupport.StopSlippage = control.StopSlippage;
            second.ManualPositionSupport.TypeDoubleExitOrder = control.TypeDoubleExitOrder;
            second.ManualPositionSupport.ValuesType = control.ValuesType;
            second.ManualPositionSupport.OrderTypeTime = control.OrderTypeTime;
            second.ManualPositionSupport.Save();
        }

        /// <summary>
        /// синхронизировать две вкладки
        /// </summary>
        private void SynchTabsIndicators(BotTabSimple first, BotTabSimple second)
        {
            List<IIndicator> indicatorsFirst = first.Indicators;

            // удаляем не нужные индикаторы

            for (int i = 0;
                second.Indicators != null
                && indicatorsFirst != null
                && indicatorsFirst.Count != 0
                && i < second.Indicators.Count;
                i++)
            {
                if (TryRemoveThisIndicator((Aindicator)second.Indicators[i], indicatorsFirst, second))
                {
                    i--;
                }
            }

            // проверяем чтобы были нужные индикаторы везде

            for (int i = 0; indicatorsFirst != null && i < indicatorsFirst.Count; i++)
            {
                TryCreateThisIndicator((Aindicator)indicatorsFirst[i], second.Indicators, second);
            }

            // синхронизируем настройки для индикаторов

            for (int i = 0; indicatorsFirst != null && i < indicatorsFirst.Count; i++)
            {
                Aindicator indFirst = (Aindicator)indicatorsFirst[i];
                Aindicator indSecond = (Aindicator)second.Indicators[i];

                if (SynchIndicatorsSettings(indFirst, indSecond))
                {
                    indSecond.Save();
                    indSecond.Reload();
                }
            }
        }

        /// <summary>
        /// попытаться удалить индикатор с вкладки если на первой его не существует
        /// </summary>
        private bool TryRemoveThisIndicator(Aindicator indSecond, List<IIndicator> indicatorsFirst, BotTabSimple tabsSecond)
        {
            // проверяем, существует ли индикатор в первом параметре у первой вкладки.
            // если не существует. Удаляем его

            string nameIndToRemove = indSecond.Name;

            if (indicatorsFirst.Find(ind => ind.Name == nameIndToRemove) != null)
            {
                return false;
            }

            // удаляем. Нет такого

            tabsSecond.DeleteCandleIndicator(indSecond);

            return true;
        }

        /// <summary>
        /// попытаться создать индикатор если на первой он есть, а на другой его ещё нет
        /// </summary>
        private void TryCreateThisIndicator(Aindicator indFirst, List<IIndicator> indicatorsSecond, BotTabSimple tabsSecond)
        {
            string nameIndToCreate = indFirst.Name;

            if (indicatorsSecond != null &&
                indicatorsSecond.Find(ind => ind.Name.Contains(nameIndToCreate)) != null)
            {
                return;
            }

            // создаём индикатор

            Aindicator newIndicator = IndicatorsFactory.CreateIndicatorByName(indFirst.GetType().Name, indFirst.Name, false);
            newIndicator = (Aindicator)tabsSecond.CreateCandleIndicator(newIndicator, indFirst.NameArea);
            newIndicator.Save();


        }

        /// <summary>
        /// синхронизировать настройки для индикатора
        /// </summary>
        private bool SynchIndicatorsSettings(Aindicator indFirst, Aindicator second)
        {
            bool isChange = false;

            if (second.CanDelete != indFirst.CanDelete)
            {
                second.CanDelete = indFirst.CanDelete;
                isChange = true;
            }

            for (int i = 0; i < indFirst.Parameters.Count; i++)
            {
                IndicatorParameter parameterFirst = indFirst.Parameters[i];
                IndicatorParameter parameterSecond = second.Parameters[i];

                if (parameterFirst.Type == IndicatorParameterType.String
                    &&
                    ((IndicatorParameterString)parameterSecond).ValueString != ((IndicatorParameterString)parameterFirst).ValueString)
                {
                    ((IndicatorParameterString)parameterSecond).ValueString = ((IndicatorParameterString)parameterFirst).ValueString;
                    isChange = true;
                }
                if (parameterFirst.Type == IndicatorParameterType.Bool &&
                    ((IndicatorParameterBool)parameterSecond).ValueBool != ((IndicatorParameterBool)parameterFirst).ValueBool)
                {
                    ((IndicatorParameterBool)parameterSecond).ValueBool = ((IndicatorParameterBool)parameterFirst).ValueBool;
                    isChange = true;
                }
                if (parameterFirst.Type == IndicatorParameterType.Decimal &&
                    ((IndicatorParameterDecimal)parameterSecond).ValueDecimal != ((IndicatorParameterDecimal)parameterFirst).ValueDecimal)
                {
                    ((IndicatorParameterDecimal)parameterSecond).ValueDecimal = ((IndicatorParameterDecimal)parameterFirst).ValueDecimal;
                    isChange = true;
                }
                if (parameterFirst.Type == IndicatorParameterType.Int &&
                    ((IndicatorParameterInt)parameterSecond).ValueInt != ((IndicatorParameterInt)parameterFirst).ValueInt)
                {
                    ((IndicatorParameterInt)parameterSecond).ValueInt = ((IndicatorParameterInt)parameterFirst).ValueInt;
                    isChange = true;
                }
            }

            for (int i = 0; i < indFirst.DataSeries.Count; i++)
            {
                IndicatorDataSeries parameterFirst = indFirst.DataSeries[i];
                IndicatorDataSeries parameterSecond = second.DataSeries[i];

                if (parameterFirst.ChartPaintType != parameterSecond.ChartPaintType)
                {
                    parameterSecond.ChartPaintType = parameterFirst.ChartPaintType;
                    isChange = true;
                }

                if (parameterFirst.Color != parameterSecond.Color)
                {
                    parameterSecond.Color = parameterFirst.Color;
                    isChange = true;
                }

                if (parameterFirst.IsPaint != parameterSecond.IsPaint)
                {
                    parameterSecond.IsPaint = parameterFirst.IsPaint;
                    isChange = true;
                }
            }

            return isChange;
        }

        private void BotTabScreener_IndicatorManuallyDeleteEvent(IIndicator indicator, BotTabSimple tab)
        {
            for (int i = 0; i < _indicators.Count; i++)
            {
                IndicatorOnTabs ind = _indicators[i];
                string name = ind.Num + ind.Type + TabName;

                if (indicator.Name == name)
                {
                    _indicators.RemoveAt(i);
                    SaveIndicators();
                    SynchFirstTab();
                    return;

                }
            }
        }

        private void BotTabScreener_IndicatorManuallyCreateEvent(IIndicator indicator, BotTabSimple tab)
        {
            if (Tabs.Count <= 1)
            {
                return;
            }

            if (Tabs[0].Indicators == null
                || Tabs[0].Indicators.Count == 0)
            {
                return;
            }

            bool oldIndicatorInArray = false;

            for (int i = 0; Tabs[0].Indicators != null && i < Tabs[0].Indicators.Count; i++)
            {
                try
                {
                    Aindicator ind = (Aindicator)Tabs[0].Indicators[i];
                }
                catch
                {
                    Tabs[0].DeleteCandleIndicator(Tabs[0].Indicators[i]);
                    i--;
                    oldIndicatorInArray = true;
                }
            }

            if (oldIndicatorInArray)
            {
                Tabs[0].SetNewLogMessage(OsLocalization.Trader.Label177, LogMessageType.Error);
                SynchFirstTab();
                SaveIndicators();
                return;
            }

            // Если появляется новый индикатор - надо: 
            // 1) Проверить чтобы это был индикатор из скриптов
            // 2) Удалить его.
            // 3) И создать такой же через меню создания индикаторов в скринере. Чтобы было правильное имя

            Aindicator indNew = (Aindicator)tab.Indicators[tab.Indicators.Count - 1];

            IndicatorParameter[] parameters = indNew.Parameters.ToArray();

            int indicatorsNow = tab.Indicators.Count;
            string indicatorType = indNew.GetType().Name;
            string area = indNew.NameArea;

            Tabs[0].DeleteCandleIndicator(indNew);
            CreateCandleIndicator(indicatorsNow, indicatorType, null, area);

            _indicators[_indicators.Count - 1].CanDelete = true;
            SaveIndicators();

            Aindicator indNew2 = (Aindicator)tab.Indicators[tab.Indicators.Count - 1];

            for (int i = 0; i < parameters.Length; i++)
            {
                indNew2.Parameters[i] = parameters[i];
            }

            indNew2.CanDelete = true;

            SynchFirstTab();
            SaveIndicators();
        }

        private void BotTabScreener_IndicatorUpdateEvent()
        {
            SynchFirstTab();
        }

        #endregion

        #region Log

        /// <summary>
        /// Send log message
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
        /// New log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region External position management

        /// <summary>
        /// Close all market positions
        /// </summary>
        public void CloseAllPositionAtMarket()
        {
            try
            {
                if (Tabs == null)
                {
                    return;
                }

                for (int i = 0; i < Tabs.Count; i++)
                {
                    Tabs[i].CloseAllAtMarket();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Get tab by position number
        /// </summary>
        /// <param name="positionNum">position number</param>
        public BotTabSimple GetTabWithThisPosition(int positionNum)
        {
            try
            {
                BotTabSimple tabWithPosition = null;

                for (int i = 0; i < Tabs.Count; i++)
                {
                    List<Position> posOnThisTab = Tabs[i].PositionsAll;

                    for (int i2 = 0; i2 < posOnThisTab.Count; i2++)
                    {
                        if (posOnThisTab[i2].Number == positionNum)
                        {
                            tabWithPosition = Tabs[i];
                        }
                    }

                    if (tabWithPosition != null)
                    {
                        break;
                    }
                }

                return tabWithPosition;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// All tab positions
        /// </summary>
        public List<Position> PositionsOpenAll
        {
            get
            {
                List<Position> positions = new List<Position>();

                for (int i = 0; i < Tabs.Count; i++)
                {
                    List<Position> curPoses = Tabs[i].PositionsOpenAll;

                    if (curPoses.Count != 0)
                    {
                        positions.AddRange(curPoses);
                    }
                }

                return positions;
            }
        }

        /// <summary>
        /// Number of sources with open or opening positions.
        /// </summary>
        public int SourceWithOpenPositionsCount
        {
            get
            {
                int result = 0;

                for (int i = 0; i < Tabs.Count; i++)
                {
                    List<Position> curPoses = Tabs[i].PositionsOpenAll;

                    if (curPoses.Count != 0)
                    {
                        result++;
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Number of sources with grids
        /// </summary>
        public int SourceWithGridsCount
        {
            get
            {
                int result = 0;

                for (int i = 0; i < Tabs.Count; i++)
                {
                    BotTabSimple tab = Tabs[i];

                    if (tab.GridsMaster.TradeGrids.Count != 0)
                    {
                        result++;
                    }
                }

                return result;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Subscribe to events in the tab
        /// </summary>
        private void SubscribeOnTab(BotTabSimple tab)
        {
            tab.LogMessageEvent += LogMessageEvent;

            tab.CandleFinishedEvent += (List<Candle> candles) =>
            {
                if (CandleFinishedEvent != null && EventsIsOn)
                {
                    CandleFinishedEvent(candles, tab);
                }

                SynchFinishCandlesMethod(candles, tab);
            };

            tab.CandleUpdateEvent += (List<Candle> candles) =>
            {
                if (CandleUpdateEvent != null && EventsIsOn)
                {
                    CandleUpdateEvent(candles, tab);
                }
            };
            tab.NewTickEvent += (Trade trade) =>
            {
                if (NewTickEvent != null && EventsIsOn)
                {
                    NewTickEvent(trade, tab);
                }
            };
            tab.MyTradeEvent += (MyTrade trade) =>
            {
                if (MyTradeEvent != null && EventsIsOn)
                {
                    MyTradeEvent(trade, tab);
                }
            };
            tab.MyTradeEvent += (MyTrade trade) =>
            {
                if (MyTradeEvent != null && EventsIsOn)
                {
                    MyTradeEvent(trade, tab);
                }
            };
            tab.OrderUpdateEvent += (Order order) =>
            {
                if (OrderUpdateEvent != null && EventsIsOn)
                {
                    OrderUpdateEvent(order, tab);
                }
            };
            tab.MarketDepthUpdateEvent += (MarketDepth md) =>
            {
                if (MarketDepthUpdateEvent != null && EventsIsOn)
                {
                    MarketDepthUpdateEvent(md, tab);
                }
            };

            tab.BestBidAskChangeEvent += (decimal bid, decimal ask) =>
            {
                if (BestBidAskChangeEvent != null && EventsIsOn)
                {
                    BestBidAskChangeEvent(bid, ask, tab);
                }
            };

            tab.PositionClosingSuccesEvent += (Position pos) =>
            {
                if (PositionClosingSuccesEvent != null && EventsIsOn)
                {
                    PositionClosingSuccesEvent(pos, tab);
                }
            };
            tab.PositionOpeningSuccesEvent += (Position pos) =>
            {
                if (PositionOpeningSuccesEvent != null && EventsIsOn)
                {
                    PositionOpeningSuccesEvent(pos, tab);
                }
            };
            tab.PositionNetVolumeChangeEvent += (Position pos) =>
            {
                if (PositionNetVolumeChangeEvent != null && EventsIsOn)
                {
                    PositionNetVolumeChangeEvent(pos, tab);
                }
            };
            tab.PositionOpeningFailEvent += (Position pos) =>
            {
                if (PositionOpeningFailEvent != null && EventsIsOn)
                {
                    PositionOpeningFailEvent(pos, tab);
                }
            };
            tab.PositionClosingFailEvent += (Position pos) =>
            {
                if (PositionClosingFailEvent != null && EventsIsOn)
                {
                    PositionClosingFailEvent(pos, tab);
                }
            };
            tab.PositionStopActivateEvent += (Position pos) =>
            {
                if (PositionStopActivateEvent != null && EventsIsOn)
                {
                    PositionStopActivateEvent(pos, tab);
                }
            };
            tab.PositionProfitActivateEvent += (Position pos) =>
            {
                if (PositionProfitActivateEvent != null && EventsIsOn)
                {
                    PositionProfitActivateEvent(pos, tab);
                }
            };
            tab.PositionBuyAtStopActivateEvent += (Position pos) =>
            {
                if (PositionBuyAtStopActivateEvent != null && EventsIsOn)
                {
                    PositionBuyAtStopActivateEvent(pos, tab);
                }
            };
            tab.PositionSellAtStopActivateEvent += (Position pos) =>
            {
                if (PositionSellAtStopActivateEvent != null && EventsIsOn)
                {
                    PositionSellAtStopActivateEvent(pos, tab);
                }
            };
        }

        /// <summary>
        /// New tab creation event
        /// </summary>
        public event Action<BotTabSimple> NewTabCreateEvent;

        /// <summary>
        /// Last candle finished
        /// </summary>
        public event Action<List<Candle>, BotTabSimple> CandleFinishedEvent;

        /// <summary>
        /// Last candle update
        /// </summary>
        public event Action<List<Candle>, BotTabSimple> CandleUpdateEvent;

        /// <summary>
        /// New trades
        /// </summary>
        public event Action<Trade, BotTabSimple> NewTickEvent;

        /// <summary>
        /// My new trade event
        /// </summary>
        public event Action<MyTrade, BotTabSimple> MyTradeEvent;

        /// <summary>
        /// Updated order
        /// </summary>
        public event Action<Order, BotTabSimple> OrderUpdateEvent;

        /// <summary>
        /// New marketDepth
        /// </summary>
        public event Action<MarketDepth, BotTabSimple> MarketDepthUpdateEvent;

        /// <summary>
        /// Bid ask change
        /// </summary>
        public event Action<decimal, decimal, BotTabSimple> BestBidAskChangeEvent;

        /// <summary>
        /// Position successfully closed
        /// </summary>
        public event Action<Position, BotTabSimple> PositionClosingSuccesEvent;

        /// <summary>
        /// Position successfully opened
        /// </summary>
        public event Action<Position, BotTabSimple> PositionOpeningSuccesEvent;

        /// <summary>
        /// Open position volume has changed
        /// </summary>
        public event Action<Position, BotTabSimple> PositionNetVolumeChangeEvent;

        /// <summary>
        /// Opening position failed
        /// </summary>
        public event Action<Position, BotTabSimple> PositionOpeningFailEvent;

        /// <summary>
        /// Position closing failed
        /// </summary>
        public event Action<Position, BotTabSimple> PositionClosingFailEvent;

        /// <summary>
        /// A stop order is activated for the position
        /// </summary>
        public event Action<Position, BotTabSimple> PositionStopActivateEvent;

        /// <summary>
        /// A profit order is activated for the position
        /// </summary>
        public event Action<Position, BotTabSimple> PositionProfitActivateEvent;

        /// <summary>
        /// Stop order buy activated
        /// </summary>
        public event Action<Position, BotTabSimple> PositionBuyAtStopActivateEvent;

        /// <summary>
        /// Stop order sell activated
        /// </summary>
        public event Action<Position, BotTabSimple> PositionSellAtStopActivateEvent;

        /// <summary>
        /// Source removed
        /// </summary>
        public event Action TabDeletedEvent;

        #endregion

        #region Synch finish candles Event

        private void SynchFinishCandlesMethod(List<Candle> candles, BotTabSimple tab)
        {
            try
            {
                if (CandlesSyncFinishedEvent == null)
                {
                    return;
                }

                if (candles == null || candles.Count == 0)
                {
                    return;
                }

                DateTime candleTime = candles[^1].TimeStart;

                // 1 смотрим чтобы по всем источникам в завершённых свечках было одно время

                for (int i = 0; i < Tabs.Count; i++)
                {
                    BotTabSimple tabCurrent = Tabs[i];

                    List<Candle> candlesCurrent = tabCurrent.CandlesFinishedOnly;

                    if (candlesCurrent == null
                        || candlesCurrent.Count == 0)
                    {
                        return;
                    }

                    DateTime candleCurrentTime = candlesCurrent[^1].TimeStart;

                    if (candleCurrentTime != candleTime)
                    {
                        return;
                    }
                }

                // 2 выбрасываем событие

                CandlesSyncFinishedEvent(Tabs);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Candles have finished for all screener sources.
        /// </summary>
        public event Action<List<BotTabSimple>> CandlesSyncFinishedEvent;

        #endregion

    }

    /// <summary>
    /// Class for storing indicators that should be on tabs
    /// </summary>
    public class IndicatorOnTabs
    {
        /// <summary>
        /// Indicator number
        /// </summary>
        public int Num;

        /// <summary>
        /// Indicator type
        /// </summary>
        public string Type;

        /// <summary>
        /// Area name on the chart
        /// </summary>
        public string NameArea;

        /// <summary>
        /// Parameters for the indicator
        /// </summary>
        public List<string> Parameters = new List<string>();

        /// <summary>
        /// Can the indicator be removed
        /// </summary>
        public bool CanDelete;

        /// <summary>
        /// Take the save string
        /// </summary>
        public string GetSaveStr()
        {
            string result = "";

            result += Type + "$" + NameArea + "$" + Num + "$" + CanDelete;

            for (int i = 0; Parameters != null && i < Parameters.Count; i++)
            {
                result += "$";

                result += Parameters[i];

            }

            return result;
        }

        /// <summary>
        /// Set class from save string
        /// </summary>
        public void SetFromStr(string saveStr)
        {
            string[] str = saveStr.Split('$');

            Type = str[0];
            NameArea = str[1];
            Num = Convert.ToInt32(str[2]);

            int startInd = 3;

            if (str.Length > 3)
            {
                CanDelete = Convert.ToBoolean(str[3]);
                startInd++;
            }

            Parameters = new List<string>();

            for (int i = startInd; i < str.Length; i++)
            {
                Parameters.Add(str[i]);
            }
        }

    }
}
