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

namespace OsEngine.OsTrader.Panels.Tab
{
    public class BotTabScreener : IIBotTab
    {
        #region staticPart

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

                        if(curScreener._host != null)
                        {
                            for (int i2 = 0; curScreener.Tabs != null &&
                                 curScreener.Tabs.Count != 0 &&
                                 i2 < curScreener.Tabs.Count; i2++)
                            {
                                PaintLastBidAsk(_screeners[i].Tabs[i2], _screeners[i].SecuritiesDataGrid);
                            }
                        }

                        _screeners[i].TryLoadTabs();
                        _screeners[i].TryReLoadTabs();
                    }
                }
                catch (Exception ex)
                {
                    // do nothin
                }

            }
        }

        /// <summary>
        /// Draw the last ask, bid, last and number of positions
        /// </summary>
        private static void PaintLastBidAsk(BotTabSimple tab, DataGridView securitiesDataGrid)
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

                    if (row.Cells == null || row.Cells.Count == 0 || row.Cells.Count < 4 || row.Cells[2].Value == null)
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

                    row.Cells[3].Value = last.ToString();
                    row.Cells[4].Value = bid.ToString();
                    row.Cells[5].Value = ask.ToString();
                    row.Cells[6].Value = posCurr.ToString() + "/" + posTotal.ToString();
                }
            }
            catch (Exception error)
            {
                tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region service

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

            AddNewTabToWatch(this);
            this.TabDeletedEvent += BotTabScreener_DeleteEvent;
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
                        Tabs[i].Connector.EventsIsOn = value;
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
                    ServerMaster.SetServerToAutoConnection(ServerType); //AVP
                }
                return;
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
                        SubscribleOnTab(newTab);
                        UpdateTabSettings(Tabs[Tabs.Count - 1]);
                        PaintNewRow();

                        if (NewTabCreateEvent != null)
                        {
                            NewTabCreateEvent(newTab);
                        }

                        if (Tabs.Count == 1)
                        {
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
                    writer.WriteLine(ServerType);
                    writer.WriteLine(_emulatorIsOn);
                    writer.WriteLine(CandleMarketDataType);
                    writer.WriteLine(CandleCreateMethodType);
                    writer.WriteLine(ComissionType);
                    writer.WriteLine(ComissionValue);
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
                    Enum.TryParse(reader.ReadLine(), out ServerType);

                    _emulatorIsOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out CandleMarketDataType);
                    CandleCreateMethodType = reader.ReadLine();

                    try
                    {
                        Enum.TryParse(reader.ReadLine(), out ComissionType);
                        ComissionValue = reader.ReadLine().ToDecimal();
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

        #endregion

        #region working with tabs

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
        public ComissionType ComissionType;

        /// <summary>
        /// Commission amount
        /// </summary>
        public decimal ComissionValue;

        /// <summary>
        /// Whether it is necessary to save trades inside the candle they belong to
        /// </summary>
        public bool SaveTradesInCandles;

        public ACandlesSeriesRealization CandleSeriesRealization;

        public bool IsLoadTabs = false;

        public bool NeadToReloadTabs = false;

        /// <summary>
        /// Reload tabs
        /// </summary>
        private void TryReLoadTabs()
        {
            try
            {
                if (NeadToReloadTabs == false)
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
                    Tabs[0].IndicatorUpdateEvent -= BotTabScreener_IndicatorUpdateEvent;
                    Tabs[0].IndicatorUpdateEvent += BotTabScreener_IndicatorUpdateEvent;
                }

                for (int i = 0; Tabs != null && i < Tabs.Count; i++)
                {
                    UpdateTabSettings(Tabs[i]);
                }

                SaveTabs();

                NeadToReloadTabs = false;
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
            tab.Connector.PortfolioName = PortfolioName;
            tab.Connector.ServerType = ServerType;
            tab.Connector.EmulatorIsOn = _emulatorIsOn;
            tab.Connector.CandleMarketDataType = CandleMarketDataType;
            tab.Connector.CandleCreateMethodType = CandleCreateMethodType;
            tab.Connector.TimeFrame = this.TimeFrame;
            tab.Connector.TimeFrameBuilder.CandleSeriesRealization.SetSaveString(CandleSeriesRealization.GetSaveString());
            tab.Connector.SaveTradesInCandles = SaveTradesInCandles;
            tab.Connector.ComissionType = ComissionType;
            tab.Connector.ComissionValue = ComissionValue;
            tab.ComissionType = ComissionType;
            tab.ComissionValue = ComissionValue;
            tab.IsCreatedByScreener = true;
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
            newTab.TimeFrameBuilder.TimeFrame = frame;
            newTab.Connector.PortfolioName = PortfolioName;
            newTab.Connector.ServerType = ServerType;
            newTab.Connector.EmulatorIsOn = _emulatorIsOn;
            newTab.Connector.CandleMarketDataType = CandleMarketDataType;
            newTab.Connector.CandleCreateMethodType = CandleCreateMethodType;
            newTab.Connector.TimeFrame = frame;
            newTab.Connector.TimeFrameBuilder.CandleSeriesRealization.SetSaveString(CandleSeriesRealization.GetSaveString());
            newTab.Connector.TimeFrameBuilder.CandleSeriesRealization.OnStateChange(CandleSeriesState.ParametersChange);
            newTab.Connector.SaveTradesInCandles = SaveTradesInCandles;
            newTab.ComissionType = ComissionType;
            newTab.ComissionValue = ComissionValue;
            newTab.IsCreatedByScreener = true;

            curTabs.Add(newTab);

            SubscribleOnTab(newTab);
        }

        /// <summary>
        /// Check if the tab exists
        /// </summary>
        private bool TabIsAlive(List<ActivatedSecurity> securities, TimeFrame frame, BotTabSimple tab)
        {
            ActivatedSecurity sec = securities.Find(s => s.SecurityName == tab.Connector.SecurityName);

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

            if(tab.TimeFrameBuilder.CandleSeriesRealization.GetType().Name 
                != CandleSeriesRealization.GetType().Name)
            {
                return false;
            }

            for(int i = 0;i < tab.TimeFrameBuilder.CandleSeriesRealization.Parameters.Count;i++)
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

            if (TimeFrame == TimeFrame.Sec1)
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
        /// Updated indicator inside tab number one
        /// </summary>
        private void BotTabScreener_IndicatorUpdateEvent()
        {
            if (Tabs.Count <= 1)
            {
                return;
            }

            List<IIndicator> indicators = Tabs[0].Indicators;

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
            }

            SuncFirstTab();
            SaveIndicators();
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

        #endregion

        #region drawing and working with the GUI

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

                BotTabScreenerUi ui = new BotTabScreenerUi(this);
                ui.ShowDialog();
            }
            catch(Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Things to call individual windows by tool
        /// </summary>
        List<CandleEngine> _chartEngines = new List<CandleEngine>();

        /// <summary>
        /// Show GUI
        /// </summary>
        public void ShowChart(int tabyNum)
        {
            try
            {
                string botName = Tabs[tabyNum].TabName + "_Engine";

                if (_chartEngines.Find(b => b.NameStrategyUniq == botName) != null)
                {
                    return;
                }

                CandleEngine bot = new CandleEngine(botName, _startProgram);

                BotTabSimple myTab = Tabs[tabyNum];

                //bot.TabCreate(BotTabType.Simple);
                bot.GetTabs().Clear();
                bot.GetTabs().Add(myTab);
                bot.TabsSimple[0] = myTab;
                bot.ActivTab = myTab;

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
        public void StartPaint(WindowsFormsHost host)
        {
            try
            {
                _host = host;
                RePaintSecuritiesGrid();
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

                _host.Child = null;
                _host = null;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
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
            colum0.HeaderText = OsLocalization.Trader.Label165;
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
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
        }

        private int prevActiveRow;

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
                    }

                    SecuritiesDataGrid.Rows[prevActiveRow].DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(154, 156, 158);
                    SecuritiesDataGrid.Rows[tabRow].DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(255, 255, 255);
                    prevActiveRow = tabRow;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(),LogMessageType.Error);
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
            nRow.Cells[1].Value = this.SecuritiesClass;

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

                System.Windows.Forms.ContextMenu menu = tab.GetContextDialog();

                SecuritiesDataGrid.ContextMenu = menu;

                SecuritiesDataGrid.ContextMenu.Show(SecuritiesDataGrid, new System.Drawing.Point(mouse.X, mouse.Y));

                //SuncFirstTab();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Настройки сопровождения позиций

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
                SuncFirstTab();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion


        #region создание / удаление / хранение индикаторов

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
                    NeadToReloadTabs = true;
                    return;
                }

                IndicatorOnTabs indicator = new IndicatorOnTabs();
                indicator.Num = num;
                indicator.Type = type;
                indicator.NameArea = nameArea;

                if (param != null)
                {
                    indicator.Params = param;
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
                if (Tabs != null
                    && Tabs.Count != 0)
                {
                    _indicators = new List<IndicatorOnTabs>();

                    List<IIndicator> indic = Tabs[0].Indicators;

                    for (int i = 0; indic != null && i < indic.Count; i++)
                    {
                        if (indic[i].GetType().BaseType.Name != "Aindicator")
                        {
                            continue;
                        }

                        Aindicator aindicator = (Aindicator)indic[i];

                        IndicatorOnTabs indicator = new IndicatorOnTabs();
                        indicator.Num = i + 1;

                        indicator.Type = indic[i].GetType().Name;
                        indicator.NameArea = indic[i].NameArea;

                        List<string> parameters = new List<string>();


                        for (int i2 = 0; i2 < aindicator.Parameters.Count; i2++)
                        {
                            string curParam = "";

                            if (aindicator.Parameters[i2].Type == IndicatorParameterType.Int)
                            {
                                curParam = ((IndicatorParameterInt)aindicator.Parameters[i2]).ValueInt.ToString();
                            }
                            if (aindicator.Parameters[i2].Type == IndicatorParameterType.Decimal)
                            {
                                curParam = ((IndicatorParameterDecimal)aindicator.Parameters[i2]).ValueDecimal.ToString();
                            }
                            if (aindicator.Parameters[i2].Type == IndicatorParameterType.Bool)
                            {
                                curParam = ((IndicatorParameterBool)aindicator.Parameters[i2]).ValueBool.ToString();
                            }
                            if (aindicator.Parameters[i2].Type == IndicatorParameterType.String)
                            {
                                curParam = ((IndicatorParameterString)aindicator.Parameters[i2]).ValueString;
                            }
                            parameters.Add(curParam);
                        }

                        indicator.Params = parameters;

                        _indicators.Add(indicator);

                    }
                }

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
        private void ReloadIndicatorsOnTabs()
        {
            for (int i = 0; i < _indicators.Count; i++)
            {
                CreateIndicatorOnTabs(_indicators[i]);
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
                newIndicator.CanDelete = false;

                try
                {
                    if (ind.Params != null && ind.Params.Count != 0)
                    {
                        for (int i2 = 0; i2 < ind.Params.Count; i2++)
                        {
                            if (newIndicator.Parameters[i2].Type == IndicatorParameterType.Int)
                            {
                                ((IndicatorParameterInt)newIndicator.Parameters[i2]).ValueInt = Convert.ToInt32(ind.Params[i2]);
                            }
                            if (newIndicator.Parameters[i2].Type == IndicatorParameterType.Decimal)
                            {
                                ((IndicatorParameterDecimal)newIndicator.Parameters[i2]).ValueDecimal = Convert.ToDecimal(ind.Params[i2]);
                            }
                            if (newIndicator.Parameters[i2].Type == IndicatorParameterType.Bool)
                            {
                                ((IndicatorParameterBool)newIndicator.Parameters[i2]).ValueBool = Convert.ToBoolean(ind.Params[i2]);
                            }
                            if (newIndicator.Parameters[i2].Type == IndicatorParameterType.String)
                            {
                                ((IndicatorParameterString)newIndicator.Parameters[i2]).ValueString = ind.Params[i2];
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }

                newIndicator = (Aindicator)Tabs[i].CreateCandleIndicator(newIndicator, ind.NameArea);
                newIndicator.Save();
            }
        }

        /// <summary>
        /// синхронизировать первую вкладку с остальными
        /// </summary>
        public void SuncFirstTab()
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
                    SyncTabsIndicators(firstTab, Tabs[i]);
                    SyncTabsManualPositionControl(firstTab, Tabs[i]);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SyncTabsManualPositionControl(BotTabSimple first, BotTabSimple second)
        {
            second.ManualPositionSupport.SecondToClose = first.ManualPositionSupport.SecondToClose;
            second.ManualPositionSupport.SecondToOpen = first.ManualPositionSupport.SecondToOpen;
            second.ManualPositionSupport.DoubleExitIsOn = first.ManualPositionSupport.DoubleExitIsOn;
            second.ManualPositionSupport.DoubleExitSlipage = first.ManualPositionSupport.DoubleExitSlipage;
            second.ManualPositionSupport.ProfitDistance = first.ManualPositionSupport.ProfitDistance;
            second.ManualPositionSupport.ProfitIsOn = first.ManualPositionSupport.ProfitIsOn;
            second.ManualPositionSupport.ProfitSlipage = first.ManualPositionSupport.ProfitSlipage;
            second.ManualPositionSupport.SecondToCloseIsOn = first.ManualPositionSupport.SecondToCloseIsOn;
            second.ManualPositionSupport.SecondToOpenIsOn = first.ManualPositionSupport.SecondToOpenIsOn;
            second.ManualPositionSupport.SetbackToCloseIsOn = first.ManualPositionSupport.SetbackToCloseIsOn;
            second.ManualPositionSupport.SetbackToClosePosition = first.ManualPositionSupport.SetbackToOpenPosition;
            second.ManualPositionSupport.SetbackToOpenIsOn = first.ManualPositionSupport.SetbackToOpenIsOn;
            second.ManualPositionSupport.SetbackToOpenPosition = first.ManualPositionSupport.SetbackToOpenPosition;
            second.ManualPositionSupport.StopDistance = first.ManualPositionSupport.StopDistance;
            second.ManualPositionSupport.StopIsOn = first.ManualPositionSupport.StopIsOn;
            second.ManualPositionSupport.StopSlipage = first.ManualPositionSupport.StopSlipage;
            second.ManualPositionSupport.TypeDoubleExitOrder = first.ManualPositionSupport.TypeDoubleExitOrder;
            second.ManualPositionSupport.ValuesType = first.ManualPositionSupport.ValuesType;
            second.ManualPositionSupport.Save();
        }

        /// <summary>
        /// синхронизировать две вкладки
        /// </summary>
        private void SyncTabsIndicators(BotTabSimple first, BotTabSimple second)
        {
            List<IIndicator> indicatorsFirst = first.Indicators;

            if (indicatorsFirst == null ||
                 indicatorsFirst.Count == 0)
            { // удаляем все индикаторы во второй вкладке

                for (int i = 0;
                    second.Indicators != null &&
                    i < second.Indicators.Count; i++)
                {
                    second.DeleteCandleIndicator(second.Indicators[i]);
                    break;
                }
            }

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

                if (SuncIndicatorsSettings(indFirst, indSecond))
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
        private bool SuncIndicatorsSettings(Aindicator indFirst, Aindicator second)
        {
            bool isChange = false;

            for (int i = 0; i < indFirst.Parameters.Count; i++)
            {
                IndicatorParameter paramFirst = indFirst.Parameters[i];
                IndicatorParameter paramSecond = second.Parameters[i];

                if (paramFirst.Type == IndicatorParameterType.String
                    &&
                    ((IndicatorParameterString)paramSecond).ValueString != ((IndicatorParameterString)paramFirst).ValueString)
                {
                    ((IndicatorParameterString)paramSecond).ValueString = ((IndicatorParameterString)paramFirst).ValueString;
                    isChange = true;
                }
                if (paramFirst.Type == IndicatorParameterType.Bool &&
                    ((IndicatorParameterBool)paramSecond).ValueBool != ((IndicatorParameterBool)paramFirst).ValueBool)
                {
                    ((IndicatorParameterBool)paramSecond).ValueBool = ((IndicatorParameterBool)paramFirst).ValueBool;
                    isChange = true;
                }
                if (paramFirst.Type == IndicatorParameterType.Decimal &&
                    ((IndicatorParameterDecimal)paramSecond).ValueDecimal != ((IndicatorParameterDecimal)paramFirst).ValueDecimal)
                {
                    ((IndicatorParameterDecimal)paramSecond).ValueDecimal = ((IndicatorParameterDecimal)paramFirst).ValueDecimal;
                    isChange = true;
                }
                if (paramFirst.Type == IndicatorParameterType.Int &&
                    ((IndicatorParameterInt)paramSecond).ValueInt != ((IndicatorParameterInt)paramFirst).ValueInt)
                {
                    ((IndicatorParameterInt)paramSecond).ValueInt = ((IndicatorParameterInt)paramFirst).ValueInt;
                    isChange = true;
                }
            }

            for (int i = 0; i < indFirst.DataSeries.Count; i++)
            {
                IndicatorDataSeries paramFirst = indFirst.DataSeries[i];
                IndicatorDataSeries paramSecond = second.DataSeries[i];

                if (paramFirst.ChartPaintType != paramSecond.ChartPaintType)
                {
                    paramSecond.ChartPaintType = paramFirst.ChartPaintType;
                    isChange = true;
                }

                if (paramFirst.Color != paramSecond.Color)
                {
                    paramSecond.Color = paramFirst.Color;
                    isChange = true;
                }

                if (paramFirst.IsPaint != paramSecond.IsPaint)
                {
                    paramSecond.IsPaint = paramFirst.IsPaint;
                    isChange = true;
                }
            }


            return isChange;
        }

        #endregion

        // log

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

        // external position management

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

        // outgoing events

        /// <summary>
        /// New tab creation event
        /// </summary>
        public event Action<BotTabSimple> NewTabCreateEvent;

        /// <summary>
        /// Subscribe to events in the tab
        /// </summary>
        private void SubscribleOnTab(BotTabSimple tab)
        {
            tab.LogMessageEvent += LogMessageEvent;

            tab.CandleFinishedEvent += (List<Candle> candles) =>
            {
                if (CandleFinishedEvent != null && EventsIsOn)
                {
                    CandleFinishedEvent(candles, tab);
                }
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
        /// Last candle finishede
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
        public List<string> Params = new List<string>();

        /// <summary>
        /// Take the save string
        /// </summary>
        public string GetSaveStr()
        {
            string result = "";

            result += Type + "$" + NameArea + "$" + Num;

            for (int i = 0; Params != null && i < Params.Count; i++)
            {
                result += "$";

                result += Params[i];

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

            Params = new List<string>();

            for (int i = 3; i < str.Length; i++)
            {
                Params.Add(str[i]);
            }
        }

    }
}