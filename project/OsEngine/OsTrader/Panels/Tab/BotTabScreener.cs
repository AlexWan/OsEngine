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

namespace OsEngine.OsTrader.Panels.Tab
{
    public class BotTabScreener : IIBotTab
    {
        #region staticPart

        /// <summary>
        /// активировать прорисовку гридов
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
        /// вкладки со скринерами
        /// </summary>
        private static List<BotTabScreener> _screeners = new List<BotTabScreener>();

        private static string _screenersListLocker = "scrLocker";

        private static void AddNewTabToWatch(BotTabScreener tab)
        {
            lock(_screenersListLocker)
            {
                _screeners.Add(tab);
            }
            
        }

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
        /// блокиратор многопоточного доступа к активации прорисовки скринеров
        /// </summary>
        private static object _staticThreadLocker = new object();

        /// <summary>
        /// поток прорисовывающий скринеры
        /// </summary>
        private static Thread _staticThread;

        /// <summary>
        /// место работы потока прорисовывающего скринеры
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

                for (int i = 0; i < _screeners.Count; i++)
                {
                    for (int i2 = 0; i2 < _screeners[i].Tabs.Count; i2++)
                    {
                        PaintLastBidAsk(_screeners[i].Tabs[i2], _screeners[i].SecuritiesDataGrid);
                    }

                    _screeners[i].TryLoadTabs();
                    _screeners[i].TryReLoadTabs();
                }
            }
        }

        /// <summary>
        /// прорисовать последние аск, бид, ласт и кол-во позиций
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


        #region сервис

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
            DeleteEvent += BotTabScreener_DeleteEvent;
        }

        private void BotTabScreener_DeleteEvent()
        {
            RemoveTabFromWatch(this);
        }

        /// <summary>
        /// program that created the robot / 
        /// программа создавшая робота
        /// </summary>
        public StartProgram StartProgram
        {
            get { return _startProgram; }
        }
        private StartProgram _startProgram;

        /// <summary>
        /// connectors array
        /// Массив для хранения списка интсрументов
        /// </summary>
        public List<BotTabSimple> Tabs = new List<BotTabSimple>();

        /// <summary>
        /// bot name /
        /// уникальное имя робота.
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// bot number / 
        /// номер вкладки
        /// </summary>
        public int TabNum { get; set; }

        public DateTime LastTimeCandleUpdate { get; set; }

        /// <summary>
        /// clear / 
        /// очистить журнал и графики
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
        /// save / 
        /// сохранить настройки в файл
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
        /// load / 
        /// загрузить настройки из файла
        /// </summary>
        public void TryLoadTabs()
        {
            if (!ServerMaster.HasActiveServers())
            {
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
        /// сохранить настройки
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
                    writer.WriteLine(EmulatorIsOn);
                    writer.WriteLine(CandleMarketDataType);
                    writer.WriteLine(CandleCreateMethodType);
                    writer.WriteLine(SetForeign);
                    writer.WriteLine(CountTradeInCandle);
                    writer.WriteLine(VolumeToCloseCandleInVolumeType);
                    writer.WriteLine(RencoPunktsToCloseCandleInRencoType);
                    writer.WriteLine(RencoIsBuildShadows);
                    writer.WriteLine(DeltaPeriods);
                    writer.WriteLine(RangeCandlesPunkts);
                    writer.WriteLine(ReversCandlesPunktsMinMove);
                    writer.WriteLine(ReversCandlesPunktsBackMove);
                    writer.WriteLine(ComissionType);
                    writer.WriteLine(ComissionValue);
                    writer.WriteLine(SaveTradesInCandles);

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
        /// загрузить настройки
        /// </summary>
        private void LoadSettings()
        {
            if (!File.Exists(@"Engine\" + TabName + @"ScreenerSet.txt"))
            {
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

                    EmulatorIsOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out CandleMarketDataType);
                    Enum.TryParse(reader.ReadLine(), out CandleCreateMethodType);

                    SetForeign = Convert.ToBoolean(reader.ReadLine());
                    CountTradeInCandle = Convert.ToInt32(reader.ReadLine());
                    VolumeToCloseCandleInVolumeType = reader.ReadLine().ToDecimal();
                    RencoPunktsToCloseCandleInRencoType = reader.ReadLine().ToDecimal();
                    RencoIsBuildShadows = Convert.ToBoolean(reader.ReadLine());
                    DeltaPeriods = reader.ReadLine().ToDecimal();
                    RangeCandlesPunkts = reader.ReadLine().ToDecimal();
                    ReversCandlesPunktsMinMove = reader.ReadLine().ToDecimal();
                    ReversCandlesPunktsBackMove = reader.ReadLine().ToDecimal();
                    Enum.TryParse(reader.ReadLine(), out ComissionType);
                    ComissionValue = reader.ReadLine().ToDecimal();
                    SaveTradesInCandles = Convert.ToBoolean(reader.ReadLine());

                    while (reader.EndOfStream == false)
                    {
                        string str = reader.ReadLine();

                        if (string.IsNullOrEmpty(str))
                        {
                            break;
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
                // ignore
            }
        }

        /// <summary>
        /// remove tab and all child structures
        /// удалить робота и все дочерние структуры
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
            
            if(DeleteEvent != null)
            {
                DeleteEvent();
            }
        }

        public event Action DeleteEvent;

        /// <summary>
        /// get journal / 
        /// взять журнал
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

        #region работа с вкладками

        /// <summary>
        /// имя портфеля для торговли
        /// </summary>
        public string PortfolioName;

        /// <summary>
        /// класс бумаг в скринере
        /// </summary>
        public string SecuritiesClass;

        /// <summary>
        /// имена бумаг добавленых в подключение
        /// </summary>
        public List<ActivatedSecurity> SecuritiesNames = new List<ActivatedSecurity>();

        /// <summary>
        /// таймфрейм
        /// </summary>
        public TimeFrame TimeFrame = TimeFrame.Min30;

        /// <summary>
        /// тип сервера
        /// </summary>
        public ServerType ServerType;

        /// <summary>
        /// включен ли эмулятор
        /// </summary>
        public bool EmulatorIsOn;

        /// <summary>
        /// тип данных для рассчёта свечек в серии свечей
        /// </summary>
        public CandleMarketDataType CandleMarketDataType;

        /// <summary>
        /// метод для создания свечек
        /// </summary>
        public CandleCreateMethodType CandleCreateMethodType;

        /// <summary>
        /// нужно ли запрашивать не торговые интервалы
        /// </summary>
        public bool SetForeign;

        /// <summary>
        /// кол-во трейдов в свече
        /// </summary>
        public int CountTradeInCandle;

        /// <summary>
        /// объём для закрытия свечи
        /// </summary>
        public decimal VolumeToCloseCandleInVolumeType;

        /// <summary>
        /// движение для закрытия свечи в свечах типа Renco
        /// </summary>
        public decimal RencoPunktsToCloseCandleInRencoType;

        /// <summary>
        /// сторим ли тени в свечках типа Renco
        /// </summary>
        public bool RencoIsBuildShadows;

        /// <summary>
        /// период дельты
        /// </summary>
        public decimal DeltaPeriods;

        /// <summary>
        /// пункты для свечек Range
        /// </summary>
        public decimal RangeCandlesPunkts;

        /// <summary>
        /// Минимальный откат для свечек Range
        /// </summary>
        public decimal ReversCandlesPunktsMinMove;

        /// <summary>
        /// Откат для создания свечи вниз для свечек Range
        /// </summary>
        public decimal ReversCandlesPunktsBackMove;

        /// <summary>
        /// тип комиссии для позиций
        /// </summary>
        public ComissionType ComissionType;

        /// <summary>
        /// размер комиссии
        /// </summary>
        public decimal ComissionValue;

        /// <summary>
        /// нужно ли сохранять трейды внутри свечи которой они принадлежат
        /// </summary>
        public bool SaveTradesInCandles;

        public bool NeadToReloadTabs = false;

        /// <summary>
        /// перезагрузить вкладки
        /// </summary>
        private void TryReLoadTabs()
        {
            if(NeadToReloadTabs == false)
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

            // 1 удаляем не нужные вкладки

            bool deleteSomeTabs = false;

            for (int i = 0; i < Tabs.Count; i++)
            {
                if (TabIsAlive(SecuritiesNames, TimeFrame, Tabs[i]) == false)
                {
                    Tabs[i].Clear();
                    Tabs[i].Delete();
                    Tabs.RemoveAt(i);
                    i--;
                    deleteSomeTabs = true;
                }
            }

            if(deleteSomeTabs)
            {
                RePaintSecuritiesGrid();
            }

            // 2 обновляем во вкладках данные

            for (int i = 0; i < Tabs.Count; i++)
            {
                UpdateTabSettings(Tabs[i]);
            }

            // 3 создаём не достающие вкладки

            for (int i = 0; i < SecuritiesNames.Count; i++)
            {
                int tabCount = Tabs.Count;

                TryCreateTab(SecuritiesNames[i], TimeFrame, Tabs);

                if(tabCount != Tabs.Count)
                {
                    UpdateTabSettings(Tabs[Tabs.Count-1]);
                    PaintNewRow();

                    if (NewTabCreateEvent != null)
                    {
                        NewTabCreateEvent(Tabs[Tabs.Count - 1]);
                    }
                }
            }

            ReloadIndicatorsOnTabs();

            if (Tabs.Count != 0)
            {
                Tabs[0].IndicatorUpdateEvent -= BotTabScreener_IndicatorUpdateEvent;
                Tabs[0].IndicatorUpdateEvent += BotTabScreener_IndicatorUpdateEvent;
            }

            SaveTabs();

            NeadToReloadTabs = false;
        }

        /// <summary>
        /// обновить настройки для вкладок
        /// </summary>
        private void UpdateTabSettings(BotTabSimple tab)
        {
            tab.Connector.PortfolioName = PortfolioName;
            tab.Connector.ServerType = ServerType;
            tab.Connector.EmulatorIsOn = EmulatorIsOn;
            tab.Connector.CandleMarketDataType = CandleMarketDataType;
            tab.Connector.CandleCreateMethodType = CandleCreateMethodType;
            tab.Connector.SetForeign = SetForeign;
            tab.Connector.CountTradeInCandle = CountTradeInCandle;
            tab.Connector.VolumeToCloseCandleInVolumeType = VolumeToCloseCandleInVolumeType;
            tab.Connector.RencoPunktsToCloseCandleInRencoType = RencoPunktsToCloseCandleInRencoType;
            tab.Connector.RencoIsBuildShadows = RencoIsBuildShadows;
            tab.Connector.DeltaPeriods = DeltaPeriods;
            tab.Connector.RangeCandlesPunkts = RangeCandlesPunkts;
            tab.Connector.ReversCandlesPunktsMinMove = ReversCandlesPunktsMinMove;
            tab.Connector.ReversCandlesPunktsBackMove = ReversCandlesPunktsBackMove;
            tab.Connector.SaveTradesInCandles = SaveTradesInCandles;
            tab.ComissionType = ComissionType;
            tab.ComissionValue = ComissionValue;
        }

        /// <summary>
        /// попробовать создать вкладку с такими параметрами
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

            while(Tabs.Find(t => t.TabName == nameStart) != null)
            {
                nameStart = numerator + " " + TabName;
                numerator++;
            }

            BotTabSimple newTab = new BotTabSimple(nameStart, _startProgram);
            newTab.Connector.SecurityName = sec.SecurityName;
            newTab.Connector.SecurityClass = sec.SecurityClass;
            newTab.TimeFrameBuilder.TimeFrame = frame;
            curTabs.Add(newTab);

            SubscribleOnTab(newTab);
        }

        /// <summary>
        /// проверяем, существует ли вкладка
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

            return true;
        }

        /// <summary>
        /// можно ли сейчас создавать вкладки
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
        /// обновился индикатор внутри вкладки номер один
        /// </summary>
        private void BotTabScreener_IndicatorUpdateEvent()
        {
            if (Tabs.Count <= 1)
            {
                return;
            }

            List<IIndicator> indicators = Tabs[0].Indicators;

            bool oldIndicatorInArray = false;

            for (int i = 0; Tabs[0].Indicators != null &&i < Tabs[0].Indicators.Count; i++)
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

            if(oldIndicatorInArray)
            {
                Tabs[0].SetNewLogMessage(OsLocalization.Trader.Label177, LogMessageType.Error);
            }

            SuncFirstTab();
            SaveIndicators();
        }

        /// <summary>
        /// все позиции по вкладкам
        /// </summary>
        public List<Position> PositionsOpenAll
        {
            get
            {
                List<Position> positions = new List<Position>();

                for(int i = 0;i < Tabs.Count;i++)
                {
                    List<Position> curPoses = Tabs[i].PositionsOpenAll;

                    if(curPoses.Count != 0)
                    {
                        positions.AddRange(curPoses);
                    }
                }

                return positions;
            }
        }

        #endregion

        #region прорисовка и работа с ГУИ


        /// <summary>
        /// show GUI
        /// вызвать окно управления
        /// </summary>
        public void ShowDialog()
        {
            if (ServerMaster.GetServers() == null ||
    ServerMaster.GetServers().Count == 0)
            {
                AlertMessageSimpleUi uiMessage = new AlertMessageSimpleUi(OsLocalization.Market.Message1);
                uiMessage.Show();
                return;
            }

            BotTabScreenerUi ui = new BotTabScreenerUi(this);
            ui.ShowDialog();

        }

        /// <summary>
        /// штуки для вызова отдельных окон по инструментам
        /// </summary>
        List<CandleEngine> _chartEngines = new List<CandleEngine>();

        /// <summary>
        /// show GUI
        /// вызвать окно управления
        /// </summary>
        public void ShowChart(int tabyNum)
        {
            string botName = this.TabName + "Engine" + tabyNum;

            if (_chartEngines.Find(b => b.NameStrategyUniq == botName) != null)
            {
                return;
            }

            CandleEngine bot = new CandleEngine(botName, _startProgram);
            // bot.TabCreate(BotTabType.Simple);
            bot.GetTabs().Clear();
            bot.GetTabs().Add(Tabs[tabyNum]);

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

        /// <summary>
        /// start drawing this robot / 
        /// начать прорисовку этого робота
        /// </summary> 
        public void StartPaint(WindowsFormsHost host)
        {
            _host = host;
            RePaintSecuritiesGrid();
        }

        /// <summary>
        /// stop drawing this robot / 
        /// остановить прорисовку этого робота
        /// </summary>
        public void StopPaint()
        {
            if (_host == null)
            {
                return;
            }
            _host.Child = null;
            _host = null;
        }

        /// <summary>
        /// дата грид скринера
        /// </summary>
        public DataGridView SecuritiesDataGrid;

        /// <summary>
        /// хост на котором храниться грид скринера
        /// </summary>
        WindowsFormsHost _host;

        /// <summary>
        /// создать таблицу для скринера
        /// </summary>
        private void CreateSecuritiesGrid()
        {
            // номер, класс, код инструмента, цены ласт, бид и аск, кол-во позиций, Чарт

            DataGridView newGrid =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.DisplayedCells);

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

        /// <summary>
        /// клик по таблице
        /// </summary>
        private void NewGrid_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;

            if (mouse.Button == MouseButtons.Right)
            {
                // отсылаем к созданию окна настроек
                CreateGridDialog(mouse);
            }
            if (mouse.Button == MouseButtons.Left)
            {
                // отсылаем смотреть чарт
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
            }
        }

        /// <summary>
        /// перерисовать грид
        /// </summary>
        private void RePaintSecuritiesGrid()
        {
            if (_host == null)
            {
                return;
            }

            if(_host.Dispatcher.CheckAccess() == false)
            {
                _host.Dispatcher.Invoke(new Action(RePaintSecuritiesGrid));
                return;
            }

            SecuritiesDataGrid.Rows.Clear();

            for (int i = 0; i < Tabs.Count; i++)
            {
                SecuritiesDataGrid.Rows.Add(GetRowFromTab(Tabs[i], i));
            }

            if (_host != null)
            {
                _host.Child = SecuritiesDataGrid;
            }
        }

        private void PaintNewRow()
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

            SecuritiesDataGrid.Rows.Add(GetRowFromTab(Tabs[Tabs.Count-1], Tabs.Count - 1));
            
        }

        /// <summary>
        /// взять строку по вкладке для грида
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
        /// создать всплывающее окно настроек для грида
        /// </summary>
        private void CreateGridDialog(MouseEventArgs mouse)
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
            //return _chartMaster.CreateIndicator(indicator, nameArea);

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
                if(Tabs != null 
                    && Tabs.Count != 0)
                {
                    _indicators = new List<IndicatorOnTabs>();

                    List<IIndicator> indic = Tabs[0].Indicators;

                    for (int i = 0; i < indic.Count; i++)
                    {
                        if(indic[i].GetType().BaseType.Name != "Aindicator")
                        {
                            continue;
                        }

                        Aindicator aindicator = (Aindicator)indic[i];

                        IndicatorOnTabs indicator = new IndicatorOnTabs();
                        indicator.Num = i+1;

                        indicator.Type = indic[i].GetType().Name;
                        indicator.NameArea = indic[i].NameArea;

                        List<string> parameters = new List<string>();


                        for (int i2 = 0; i2 < aindicator.Parameters.Count; i2++)
                        {
                            string curParam = "";

                            if (aindicator.Parameters[i2].Type == IndicatorParameterType.Int)
                            {
                                curParam = ((IndicatorParameterInt)aindicator.Parameters[i2]).ValueInt.ToString() ;
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
        private List<IndicatorOnTabs> _indicators = new List<IndicatorOnTabs>();

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
            if (Tabs.Count <= 1)
            {
                return;
            }

            BotTabSimple firstTab = Tabs[0];

            for (int i = 1; i < Tabs.Count; i++)
            {
                SyncTabs(firstTab, Tabs[i]);
            }
        }

        /// <summary>
        /// синхронизировать две вкладки
        /// </summary>
        private void SyncTabs(BotTabSimple first, BotTabSimple second)
        {
            List<IIndicator> indicatorsFirst = first.Indicators;

            if (indicatorsFirst == null ||
                 indicatorsFirst.Count == 0)
            { // удаляем все индикаторы во второй вкладке

                for(int i = 0;
                    second.Indicators != null && 
                    i < second.Indicators.Count;i++)
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

            return isChange;
        }

        #endregion

        // log / логирование

        /// <summary>
        /// send log message / 
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
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
        /// сообщение в лог
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        // исходящие события

        /// <summary>
        /// событие создания новой вкладки 
        /// </summary>
        public event Action<BotTabSimple> NewTabCreateEvent;

        /// <summary>
        /// подписаться на события во вкладке
        /// </summary>
        private void SubscribleOnTab(BotTabSimple tab)
        {
            tab.LogMessageEvent += LogMessageEvent;

            tab.CandleFinishedEvent += (List<Candle> candles) =>
            {
                if(CandleFinishedEvent != null)
                {
                    CandleFinishedEvent(candles, tab);
                }
            };

            tab.CandleUpdateEvent += (List<Candle> candles) =>
            {
                if (CandleUpdateEvent != null)
                {
                    CandleUpdateEvent(candles, tab);
                }
            };
            tab.NewTickEvent += (Trade trade) =>
            {
                if (NewTickEvent != null)
                {
                    NewTickEvent(trade, tab);
                }
            };
            tab.MyTradeEvent += (MyTrade trade) =>
            {
                if (MyTradeEvent != null)
                {
                    MyTradeEvent(trade, tab);
                }
            };
            tab.MyTradeEvent += (MyTrade trade) =>
            {
                if (MyTradeEvent != null)
                {
                    MyTradeEvent(trade, tab);
                }
            };
            tab.OrderUpdateEvent += (Order order) =>
            {
                if (OrderUpdateEvent != null)
                {
                    OrderUpdateEvent(order, tab);
                }
            };
            tab.MarketDepthUpdateEvent += (MarketDepth md) =>
            {
                if (MarketDepthUpdateEvent != null)
                {
                    MarketDepthUpdateEvent(md, tab);
                }
            };

            tab.PositionClosingSuccesEvent += (Position pos) =>
            {
                if (PositionClosingSuccesEvent != null)
                {
                    PositionClosingSuccesEvent(pos, tab);
                }
            };
            tab.PositionOpeningSuccesEvent += (Position pos) =>
            {
                if (PositionOpeningSuccesEvent != null)
                {
                    PositionOpeningSuccesEvent(pos, tab);
                }
            };
            tab.PositionNetVolumeChangeEvent += (Position pos) =>
            {
                if (PositionNetVolumeChangeEvent != null)
                {
                    PositionNetVolumeChangeEvent(pos, tab);
                }
            };
            tab.PositionOpeningFailEvent += (Position pos) =>
            {
                if (PositionOpeningFailEvent != null)
                {
                    PositionOpeningFailEvent(pos, tab);
                }
            };
            tab.PositionClosingFailEvent += (Position pos) =>
            {
                if (PositionClosingFailEvent != null)
                {
                    PositionClosingFailEvent(pos, tab);
                }
            };
            tab.PositionStopActivateEvent += (Position pos) =>
            {
                if (PositionStopActivateEvent != null)
                {
                    PositionStopActivateEvent(pos, tab);
                }
            };
            tab.PositionProfitActivateEvent += (Position pos) =>
            {
                if (PositionProfitActivateEvent != null)
                {
                    PositionProfitActivateEvent(pos, tab);
                }
            };
            tab.PositionBuyAtStopActivateEvent += (Position pos) =>
            {
                if (PositionBuyAtStopActivateEvent != null)
                {
                    PositionBuyAtStopActivateEvent(pos, tab);
                }
            };
            tab.PositionSellAtStopActivateEvent += (Position pos) =>
            {
                if (PositionSellAtStopActivateEvent != null)
                {
                    PositionSellAtStopActivateEvent(pos, tab);
                }
            };
        }

        /// <summary>
        /// last candle finished / 
        /// завершилась новая свечка
        /// </summary>
        public event Action<List<Candle>,BotTabSimple> CandleFinishedEvent;

        /// <summary>
        /// last candle update /
        /// обновилась последняя свечка
        /// </summary>
        public event Action<List<Candle>, BotTabSimple> CandleUpdateEvent;

        /// <summary>
        /// new trades
        /// пришли новые тики
        /// </summary>
        public event Action<Trade, BotTabSimple> NewTickEvent;

        /// <summary>
        /// my new trade event /
        /// событие моей новой сделки
        /// </summary>
        public event Action<MyTrade, BotTabSimple> MyTradeEvent;

        /// <summary>
        /// updated order
        /// обновился ордер
        /// </summary>
        public event Action<Order, BotTabSimple> OrderUpdateEvent;

        /// <summary>
        /// new marketDepth
        /// пришёл новый стакан
        /// </summary>
        public event Action<MarketDepth, BotTabSimple> MarketDepthUpdateEvent;

        /// <summary>
        /// position successfully closed / 
        /// позиция успешно закрыта
        /// </summary>
        public event Action<Position, BotTabSimple> PositionClosingSuccesEvent;

        /// <summary>
        /// position successfully opened /
        /// позиция успешно открыта
        /// </summary>
        public event Action<Position, BotTabSimple> PositionOpeningSuccesEvent;

        /// <summary>
        /// open position volume has changed / 
        /// у позиции изменился открытый объём
        /// </summary>
        public event Action<Position, BotTabSimple> PositionNetVolumeChangeEvent;

        /// <summary>
        /// opening position failed / 
        /// открытие позиции не случилось
        /// </summary>
        public event Action<Position, BotTabSimple> PositionOpeningFailEvent;

        /// <summary>
        /// position closing failed / 
        /// закрытие позиции не прошло
        /// </summary>
        public event Action<Position, BotTabSimple> PositionClosingFailEvent;

        /// <summary>
        /// a stop order is activated for the position
        /// по позиции активирован стоп-ордер
        /// </summary>
        public event Action<Position, BotTabSimple> PositionStopActivateEvent;

        /// <summary>
        /// a profit order is activated for the position
        /// по позиции активирован профит-ордер
        /// </summary>
        public event Action<Position, BotTabSimple> PositionProfitActivateEvent;

        /// <summary>
        /// stop order buy activated
        /// активирована покупка по стоп-приказу
        /// </summary>
        public event Action<Position, BotTabSimple> PositionBuyAtStopActivateEvent;

        /// <summary>
        /// stop order sell activated
        /// активирована продажа по стоп-приказу
        /// </summary>
        public event Action<Position, BotTabSimple> PositionSellAtStopActivateEvent;

    }

    /// <summary>
    /// класс для хранения индикаторов которые должны быть на вкладках
    /// </summary>
    public class IndicatorOnTabs
    {
        /// <summary>
        /// номер индикатора
        /// </summary>
        public int Num;

        /// <summary>
        /// тип индикатора
        /// </summary>
        public string Type;

        /// <summary>
        /// название области на чарте
        /// </summary>
        public string NameArea;

        /// <summary>
        /// параметры для индикатора
        /// </summary>
        public List<string> Params = new List<string>();

        /// <summary>
        /// взять строку сохранения
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
        /// настроить класс из строки сохранения
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