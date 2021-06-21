/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Connectors;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Market.Servers.Tester
{

    /// <summary>
	/// server for testing
	/// Delivers synchronized data. Executes applications. Keeps track of positions
    /// сервер для тестирования. 
    /// Поставляет синхронизированные данные. Исполняет заявки. Следит за позициями
    /// </summary>
    public class TesterServer: IServer
    {

        private static readonly CultureInfo CultureInfo = new CultureInfo("ru-RU");

        /// <summary>
		/// constructor
        /// конструктор
        /// </summary>
        public TesterServer()
        {
            _portfolios = new List<Portfolio>();
            _logMaster = new Log("TesterServer", StartProgram.IsTester);
            _logMaster.Listen(this);
            _serverConnectStatus = ServerConnectStatus.Disconnect;
            ServerStatus = ServerConnectStatus.Disconnect;
            _testerRegime = TesterRegime.Pause;
            _slipageToSimpleOrder = 0;
            _slipageToStopOrder = 0;
            StartPortfolio = 1000000;
            TypeTesterData = TesterDataType.Candle;
            Load();

            if (_activSet != null)
            {
                _needToReloadSecurities = true;
            }

            if (_worker == null)
            {
                _worker = new Thread(WorkThreadArea);
                _worker.CurrentCulture = CultureInfo;
                _worker.IsBackground = true;
                _worker.Name = "TesterServerThread";
                _worker.Start();
            }

            _candleManager = new CandleManager(this);
            _candleManager.CandleUpdateEvent += _candleManager_CandleUpdateEvent;
            _candleManager.LogMessageEvent += SendLogMessage;
            _candleManager.TypeTesterData = TypeTesterData;

            _candleSeriesTesterActivate = new List<SecurityTester>();

            OrdersActiv = new List<Order>();

            CheckSet();
        }

        /// <summary>
		/// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.Tester; }
        }

        private TesterServerUi _ui;

        /// <summary>
		/// show settings window
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            if (_ui == null)
            {
                _ui = new TesterServerUi(this, _logMaster);
                _ui.Show();
                _ui.Closing += _ui_Closing;
            }
            else
            {
                _ui.Focus();
            }

        }

        void _ui_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _ui = null;
        }

        /// <summary>
		/// data type that the tester orders
        /// тип данных которые заказывает тестер
        /// </summary>
        public TesterDataType TypeTesterData
        {
            get { return _typeTesterData; }
            set
            {
                if (_typeTesterData == value)
                {
                    return;
                }

                if (_candleManager != null)
                {
                    _candleManager.Clear();
                    _candleManager.TypeTesterData = value;
                }
                _typeTesterData = value;
                Save();
                ReloadSecurities();
            }

        }
        private TesterDataType _typeTesterData;

        /// <summary>
		/// download settings from the file
        /// загрузить настройки из файла
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + @"TestServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"TestServer.txt"))
                {
                    _activSet = reader.ReadLine();
                    _slipageToSimpleOrder = Convert.ToInt32(reader.ReadLine());
                    StartPortfolio = reader.ReadLine().ToDecimal();
                    Enum.TryParse(reader.ReadLine(), out _typeTesterData);
                    Enum.TryParse(reader.ReadLine(), out _sourceDataType);
                    _pathToFolder = reader.ReadLine();
                    _slipageToStopOrder = Convert.ToInt32(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out _orderExecutionType);
                    _profitMarketIsOn = Convert.ToBoolean(reader.ReadLine());
                    
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
		/// save settings
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"TestServer.txt", false))
                {
                    writer.WriteLine(_activSet);
                    writer.WriteLine(_slipageToSimpleOrder);
                    writer.WriteLine(StartPortfolio);
                    writer.WriteLine(_typeTesterData);
                    writer.WriteLine(_sourceDataType);
                    writer.WriteLine(_pathToFolder);
                    writer.WriteLine(_slipageToStopOrder);
                    writer.WriteLine(_orderExecutionType);
                    writer.WriteLine(_profitMarketIsOn);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// save security test settings
        /// сохранить тестовые настройки инструмента
        /// </summary>
        public void SaveSecurityTestSettings()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(GetSecurityTestSettingsPath(), false))
                {
                    writer.WriteLine(TimeStart.ToString(CultureInfo));
                    writer.WriteLine(TimeEnd.ToString(CultureInfo));
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// load security test settings
        /// загрузить тестовые настройки инструмента
        /// </summary>
        public void LoadSecurityTestSettings()
        {
            try
            {
                string pathToSettings = GetSecurityTestSettingsPath();
                if (!File.Exists(pathToSettings))
                {
                    return;
                }

                using (StreamReader reader = new StreamReader(pathToSettings))
                {
                    string timeStart = reader.ReadLine();
                    if (timeStart != null)
                    {
                        TimeStart = Convert.ToDateTime(timeStart, CultureInfo);
                    }
                    string timeEnd = reader.ReadLine();
                    if (timeEnd != null)
                    {
                        TimeEnd = Convert.ToDateTime(timeEnd, CultureInfo);
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private string GetSecurityTestSettingsPath()
        {
            string pathToSettings;
            
            if (SourceDataType == TesterSourceDataType.Set)
            {
                if (string.IsNullOrWhiteSpace(_activSet))
                {
                    return "";
                }
                pathToSettings = _activSet + "\\SecurityTestSettings.txt";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_pathToFolder))
                {
                    return "";
                }
                pathToSettings = _pathToFolder + "\\SecurityTestSettings.txt";
            }

            return pathToSettings;
        }

        private string GetSecuritiesSettingsPath()
        {
            string pathToSettings;

            if (SourceDataType == TesterSourceDataType.Set)
            {
                if (string.IsNullOrWhiteSpace(_activSet))
                {
                    return "";
                }
                pathToSettings = _activSet + "\\SecuritiesSettings.txt";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_pathToFolder))
                {
                    return "";
                }
                pathToSettings = _pathToFolder + "\\SecuritiesSettings.txt";
            }

            return pathToSettings;
        }

        // additional part from standart servers
        // аппендикс от нормальных серверов

        /// <summary>
		/// isn't used in the test server
        /// в тестовом сервере не используется
        /// </summary>
        public void StartServer(){}

        /// <summary>
		/// isn't used in the test server
        /// в тестовом сервере не используется
        /// </summary>
        public void StopServer(){}
       
        /// <summary>
        /// server time of last starting
        /// время последнего старта сервера
        /// </summary>
        public DateTime LastStartServerTime { get; set; }

        // Managment
        // Управление

        /// <summary>
        /// start testing
        /// начать тестирование
        /// </summary>
        public void TestingStart()
        {
            _testerRegime = TesterRegime.Pause;
            Thread.Sleep(2000);
            _serverTime = DateTime.MinValue;

            ServerMaster.ClearOrders();

            SendLogMessage(OsLocalization.Market.Message35, LogMessageType.System);

            if (TestingStartEvent != null)
            {
                TestingStartEvent();
            }

            if (_candleSeriesTesterActivate != null)
            {
                for (int i = 0; i < _candleSeriesTesterActivate.Count; i++)
                {
                    _candleSeriesTesterActivate[i].Clear();
                }
            }

            _candleSeriesTesterActivate = new List<SecurityTester>();

            if (NeadToReconnectEvent != null)
            {
                NeadToReconnectEvent();
            }

            Thread.Sleep(5000);
            _candleManager.Clear();

            _allTrades = null;

            if (TimeStart == DateTime.MinValue)
            {
                SendLogMessage(OsLocalization.Market.Message47, LogMessageType.System);
                return;
            }

            TimeNow = TimeStart;

            while (TimeNow.Minute != 0)
            {
               TimeNow = TimeNow.AddMinutes(-1);
            }

            while (TimeNow.Second != 0)
            {
                TimeNow = TimeNow.AddSeconds(-1);
            }

            while (TimeNow.Millisecond != 0)
            {
                TimeNow = TimeNow.AddMilliseconds(-1);
            }

            if (_portfolios != null && _portfolios.Count != 0)
            {
                _portfolios[0].ValueCurrent = StartPortfolio;
                _portfolios[0].ValueBegin = StartPortfolio;
                _portfolios[0].ValueBlocked = 0;
                _portfolios[0].ClearPositionOnBoard();
            }

            ProfitArray = new List<decimal>();

            _dataIsActive = false;

            _testerRegime = TesterRegime.Play;
        }

        /// <summary>
		/// speed testing by hiding areas
        /// ускорить тестирование, спрятав области
        /// </summary>
        public void TestingFast()
        {
            if (_dataIsActive == false)
            {
                return;
            }
            if (TestingFastEvent != null)
            {
                TestingFastEvent();
            }
            _testerRegime = TesterRegime.Play;
        }

        /// <summary>
		/// stops testing, or starts
        /// останавливает тестирование, или запускает
        /// </summary>
        public void TestingPausePlay()
        {
            if (_testerRegime == TesterRegime.Play)
            {
                _testerRegime = TesterRegime.Pause;
            }
            else
            {
                _testerRegime = TesterRegime.Play;
            }
        }

        /// <summary>
		/// get the next second and stop testing
        /// прогрузить следующую секунду и остановить тестирование
        /// </summary>
        public void TestingPlusOne()
        {
            _testerRegime = TesterRegime.PlusOne;
        }

        /// <summary>
		/// Testing started
        /// Тестирование запущено
        /// </summary>
        public event Action TestingStartEvent;

        /// <summary>
		/// rewind mode enabled
        /// включен режим перемотки
        /// </summary>
        public event Action TestingFastEvent;

        /// <summary>
		/// testing stopped
        /// тестирование прервано
        /// </summary>
        public event Action TestingEndEvent;

        /// <summary>
		/// new securities in tester
        /// новые бумаги в тестере
        /// </summary>
        public event Action TestingNewSecurityEvent;

// work with data connection
// работа с подключением данных

        public TesterSourceDataType SourceDataType
        {
            get { return _sourceDataType; }
            set
            {
                if (value == _sourceDataType)
                {
                    return;
                }

                _sourceDataType = value;
                ReloadSecurities();
            }
        }
        private TesterSourceDataType _sourceDataType;

        /// <summary>
		/// data sets
        /// сеты данных
        /// </summary>
        private List<string> _sets;
        public List<string> Sets 
        {
            get
            {
                return _sets; 
            }
            private set
            {
                _sets = value;
            } 
        } 

        /// <summary>
		/// take all sets from folder
        /// взять все сеты из папки
        /// </summary>
        private void CheckSet()
        {
            if (!Directory.Exists(@"Data"))
            {
                Directory.CreateDirectory(@"Data");
            }

            string[] folders = Directory.GetDirectories(@"Data" + @"\");

            if (folders.Length == 0)
            {
                SendLogMessage(OsLocalization.Market.Message25, LogMessageType.System);
            }

            List<string> sets = new List<string>();

            for (int i = 0; i < folders.Length; i++)
            {
                if (folders[i].Split('_').Length == 2)
                {
                    sets.Add(folders[i].Split('_')[1]);
                    SendLogMessage("Найден сет: " + folders[i].Split('_')[1], LogMessageType.System);
                }
            }

            if (sets.Count == 0)
            {
                SendLogMessage(OsLocalization.Market.Message25, LogMessageType.System);
            }
            Sets = sets;
        }

        /// <summary>
		/// bind to the tester a new data set
        /// привязать к тестору новый сет данных
        /// </summary>
        /// <param name="setName">имя сета</param>
        public void SetNewSet(string setName)
        {
            string newSet = @"Data" + @"\" + @"Set_" + setName;
            if (newSet == _activSet)
            {
                return;
            }

            SendLogMessage(OsLocalization.Market.Message27 + setName, LogMessageType.System);
            _activSet = newSet;

            if (_sourceDataType == TesterSourceDataType.Set)
            {
                ReloadSecurities();
            }
            Save();
        }

        public void ReloadSecurities()
        {
            // clear all data and disconnect / чистим все данные, отключаемся
            _testerRegime = TesterRegime.Pause;
            _dataIsReady = false;
            ServerStatus = ServerConnectStatus.Disconnect;
            _securities = null;
            SecuritiesTester = null;
            _candleManager.Clear();
            _candleSeriesTesterActivate = new List<SecurityTester>();
            Save();

            // update / обновляем
            
            _needToReloadSecurities = true;

            if (NeadToReconnectEvent != null)
            {
                NeadToReconnectEvent();
            }
        }

        /// <summary>
		/// path to the data folder
        /// путь к папке с данными
        /// </summary>
        public string PathToFolder
        {
            get { return _pathToFolder; }
        }
        private string _pathToFolder;

        /// <summary>
		/// call folder path selection window
        /// вызвать окно выбора пути к папке
        /// </summary>
        public void ShowPathSenderDialog()
        {
            _testerRegime = TesterRegime.Pause;

            System.Windows.Forms.FolderBrowserDialog myDialog = new System.Windows.Forms.FolderBrowserDialog();

            if (string.IsNullOrWhiteSpace(_pathToFolder))
            {
                myDialog.SelectedPath = _pathToFolder;
            }

            myDialog.ShowDialog();

            if (myDialog.SelectedPath != "" && 
                _pathToFolder != myDialog.SelectedPath) // если хоть что-то выбрано
            {
                _pathToFolder = myDialog.SelectedPath;
                if (_sourceDataType == TesterSourceDataType.Folder)
                {
                    ReloadSecurities();
                }
            }
        }

// Synchronizer
// Синхронизатор

        /// <summary>
		/// path to the data folder. He is the name of the active set
        /// путь к папке с данными. Он же название активного сета
        /// </summary>
        private string _activSet;
        public string ActiveSet
        {
            get { return _activSet; }
        }

        /// <summary>
		/// minimum time that can be set for synchronization
        /// минимальное время которое можно задать для синхронизации
        /// </summary>
        public DateTime TimeMin;

        /// <summary>
		/// maximum time that can be set for synchronization
        /// максимальное время которое можно задать для синхронизации
        /// </summary>
        public DateTime TimeMax;

        /// <summary>
		/// start time of testing selected by the user
        /// время начала тестирования выбранное пользователем
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
		/// finish time of testing selected by the user
        /// время конца тестирования выбранное пользователем
        /// </summary>
        public DateTime TimeEnd;

        /// <summary>
		/// time synchronizer at now time of history
        /// время синхронизатора в данный момент подачи истории
        /// </summary>
        public DateTime TimeNow;

        /// <summary>
		/// are data ready for loading
        /// готовы ли данные к загрузке
        /// </summary>
        private bool _dataIsReady;

        /// <summary>
		/// securities available for loading
        /// бумаги доступные для загрузки 
        /// </summary>
        public List<SecurityTester> SecuritiesTester;

        /// <summary>
		/// removes unnecessary connections by synchronizing them with loaded bots
        /// удаляет лишние подключения синхронизируя их с загруженными ботами
        /// </summary>
        public void SynhSecurities(List<BotPanel> bots)
        {
            if (bots == null || bots.Count == 0 ||
              SecuritiesTester == null || SecuritiesTester.Count == 0)
            {
                return;
            }

            List<string> namesSecurity = new List<string>();

            for (int i = 0; i < bots.Count; i++)
            {
                List<BotTabSimple> currentTabs = bots[i].TabsSimple;

                for (int i2 = 0; currentTabs != null && i2 < currentTabs.Count; i2++)
                {
                    if (currentTabs[i2].Securiti != null)
                    {
                        namesSecurity.Add(currentTabs[i2].Securiti.Name);
                    }
                }
            }

            for (int i = 0; i < bots.Count; i++)
            {
                List<BotTabCluster> currentTabs = bots[i].TabsCluster;

                for (int i2 = 0; currentTabs != null && i2 < currentTabs.Count; i2++)
                {
                    namesSecurity.Add(currentTabs[i2].CandleConnector.NamePaper);
                }
            }

            for (int i = 0; i < bots.Count; i++)
            {
                List<BotTabIndex> currentTabsSpread = bots[i].TabsIndex;

                for (int i2 = 0; currentTabsSpread != null && i2 < currentTabsSpread.Count; i2++)
                {
                    BotTabIndex index = currentTabsSpread[i2];

                    for (int i3 = 0; index.Tabs != null && i3 < index.Tabs.Count; i3++)
                    {
                        ConnectorCandles currentConnector = index.Tabs[i3];

                        if (!string.IsNullOrWhiteSpace(currentConnector.NamePaper))
                        {
                            namesSecurity.Add(currentConnector.NamePaper);
                        }
                    }

                }
            }

            for (int i = 0; i < SecuritiesTester.Count; i++)
            {
                if (namesSecurity.Find(name => name == SecuritiesTester[i].Security.Name) == null)
                {
                    SecuritiesTester[i].IsActiv = false;
                }
                else
                {
                    SecuritiesTester[i].IsActiv = true;
                }
            }

            _candleManager.SynhSeries(namesSecurity);

            if (TypeTesterData == TesterDataType.TickAllCandleState ||
                TypeTesterData == TesterDataType.TickOnlyReadyCandle)
            {
                _timeAddType = TimeAddInTestType.Second;
            }
            else if (TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                     TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
            {
                _timeAddType = TimeAddInTestType.MilliSecond;
            }
            else if (TypeTesterData == TesterDataType.Candle)
            {

                if (SecuritiesTester.Find(name => name.TimeFrameSpan < new TimeSpan(0, 0, 1, 0)) == null)
                {
                    _timeAddType = TimeAddInTestType.Minute;
                }
                else
                {
                    _timeAddType = TimeAddInTestType.Second;
                }
            }
        }

// work place of main thread
// место работы основного потока

        /// <summary>
		/// synchronizer accuracy. For candles above a minute - minutes. For ticks - seconds. For depths - milliseconds
        /// точность синхронизатора. Для свечек выше минуты - минутки. Для тиков - секунды. Для стаканов - миллисекунды
		/// set in the SynhSecurities method
        /// устанавливается в методе SynhSecurities
        /// </summary>
        private TimeAddInTestType _timeAddType;

        /// <summary>
		/// did the data from data series go
        /// пошли ли данные из серий данных
        /// </summary>
        private bool _dataIsActive;

        /// <summary>
		/// is it time to reload securities in the directory
        /// пора ли перезагружать бумаги в директории
        /// </summary>
        private bool _needToReloadSecurities;

        /// <summary>
		/// test mode
        /// режим тестирования
        /// </summary>
        private TesterRegime _testerRegime;

        /// <summary>
		/// main thread that downloads all data
        /// основной поток, которые занимается прогрузкой всех данных
        /// </summary>
        private Thread _worker;

        /// <summary>
		/// work place of main thread
        /// место работы основного потока
        /// </summary>
        private void WorkThreadArea()
        {
            Thread.Sleep(2000);
            while (true)
            {
                try
                {
                    if (_serverConnectStatus != ServerConnectStatus.Connect)
                    {
                        if (Securities != null && Securities.Count != 0)
                        {
                            ServerStatus = ServerConnectStatus.Connect;
                        }
                    }

                    if (_needToReloadSecurities)
                    {
                        _needToReloadSecurities = false;
                        _testerRegime = TesterRegime.Pause;
                        LoadSecurities();
                    }
                    if (_portfolios.Count == 0)
                    {
                        CreatePortfolio();
                    }

                    if (_testerRegime == TesterRegime.Pause)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (!_dataIsReady)
                    {
                        
                        SendLogMessage(OsLocalization.Market.Message48, LogMessageType.System);
                        _testerRegime = TesterRegime.Pause;
                        continue;
                    }


                    if (_testerRegime == TesterRegime.PlusOne)
                    {
                        if (_testerRegime != TesterRegime.Pause)
                        {
                            LoadNextData();
                        }
                        CheckOrders();
                        continue;
                    }
                    if (_testerRegime == TesterRegime.Play)
                    {
                        LoadNextData();
                        CheckOrders();
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        /// <summary>
		/// create portfolio for test server
        /// создать портфель для тестового сервера
        /// </summary>
        private void CreatePortfolio()
        {

            Portfolio portfolio = new Portfolio();
            portfolio.Number = "GodMode";
            portfolio.ValueBegin = 1000000;
            portfolio.ValueBlocked = 0;
            portfolio.ValueCurrent = 1000000;
            ProfitArray = new List<decimal>();

            _portfolios = new List<Portfolio>();

            UpdatePortfolios(new[] { portfolio }.ToList());
        }

        /// <summary>
		/// download data about securities from the directory
        /// загрузить данные о бумагах из директории
        /// </summary>
        private void LoadSecurities()
        {
            if ((_sourceDataType == TesterSourceDataType.Set && (string.IsNullOrWhiteSpace(_activSet) || !Directory.Exists(_activSet))) ||
                (_sourceDataType == TesterSourceDataType.Folder && (string.IsNullOrWhiteSpace(_pathToFolder) || !Directory.Exists(_pathToFolder))))
            {
                return;
            }

            if (_sourceDataType == TesterSourceDataType.Set)
            { // Hercules data sets/сеты данных Геркулеса
                string[] directories = Directory.GetDirectories(_activSet);

                if (directories.Length == 0)
                {
                    SendLogMessage(OsLocalization.Market.Message28, LogMessageType.System);
                    return;
                }

                for (int i = 0; i < directories.Length; i++)
                {
                    LoadSeciruty(directories[i]);
                }

                _dataIsReady = true;
            }
            else // if (_sourceDataType == TesterSourceDataType.Folder)
            { // simple files from folder/простые файлы из папки

                string[] files = Directory.GetFiles(_pathToFolder);

                if (files.Length == 0)
                {
                    SendLogMessage(OsLocalization.Market.Message49, LogMessageType.Error);
                }

                LoadCandleFromFolder(_pathToFolder);
                LoadTickFromFolder(_pathToFolder);
                LoadMarketDepthFromFolder(_pathToFolder);
                _dataIsReady = true;
            }
        }

        /// <summary>
		/// unload one tool from folder
        /// выгрузить один инструмент из папки
        /// </summary>
        /// <param name="path">folder path to instrument / путь к папке с инструментом</param>
        private void LoadSeciruty(string path)
        {
            string[] directories = Directory.GetDirectories(path);

            if (directories.Length == 0)
            {
                return;
            }

            TimeMax = DateTime.MinValue;
            TimeEnd = DateTime.MaxValue;
            TimeMin = DateTime.MaxValue;
            TimeStart = DateTime.MinValue;
            TimeNow = DateTime.MinValue;

            for (int i = 0; i < directories.Length; i++)
            {
                string name = directories[i].Split('\\')[3];

                if (name == "MarketDepth")
                {
                    LoadMarketDepthFromFolder(directories[i]);
                }
                else if (name == "Tick")
                {
                    LoadTickFromFolder(directories[i]);
                }
                else
                {
                    LoadCandleFromFolder(directories[i]);
                }
            }
        }

        private void LoadCandleFromFolder(string folderName)
        {

            string[] files = Directory.GetFiles(folderName);

            if (files.Length == 0)
            {
                return;
            }

            List<SecurityTester> security = new List<SecurityTester>();

            for (int i = 0; i < files.Length; i++)
            {
                security.Add(new SecurityTester());
                security[security.Count - 1].FileAdress = files[i];
                security[security.Count - 1].NewCandleEvent += TesterServer_NewCandleEvent;
                security[security.Count - 1].NewTradesEvent += TesterServer_NewTradesEvent;
                security[security.Count - 1].NewMarketDepthEvent += TesterServer_NewMarketDepthEvent;
                security[security.Count - 1].LogMessageEvent += TesterServer_LogMessageEvent;

                string name = files[i].Split('\\')[files[i].Split('\\').Length - 1];

                security[security.Count - 1].Security = new Security();
                security[security.Count - 1].Security.Name = name;
                security[security.Count - 1].Security.Lot = 1;
                security[security.Count - 1].Security.NameClass = "TestClass";
                security[security.Count - 1].Security.Go = 1;
                security[security.Count - 1].Security.PriceStepCost = 1;
                security[security.Count - 1].Security.PriceStep = 1;
                // timeframe / тф
                // price step / шаг цены
                // begin / начало
                // end / конец

                StreamReader reader = new StreamReader(files[i]);

                // candles / свечи: 20110111,100000,19577.00000,19655.00000,19533.00000,19585.00000,2752
                // ticks ver.1 / тики 1 вар: 20150401,100000,86160.000000000,2
                // ticks ver.2 / тики 2 вар: 20151006,040529,3010,5,Buy/Sell/Unknown

                string str = reader.ReadLine();

                try
                {
                    // check whether candles are in the file / смотрим свечи ли в файле
                    Candle candle = new Candle();
                    candle.SetCandleFromString(str);
                    // candles are in the file. We look at which ones / в файле свечи. Смотрим какие именно

                    security[security.Count - 1].TimeStart = candle.TimeStart;

                    Candle candle2 = new Candle();
                    candle2.SetCandleFromString(reader.ReadLine());

                    security[security.Count - 1].DataType = SecurityTesterDataType.Candle;
                    security[security.Count - 1].TimeFrameSpan = GetTimeSpan(reader);
                    security[security.Count - 1].TimeFrame = GetTimeFrame(security[security.Count - 1].TimeFrameSpan);
                    // step price / шаг цены

                    decimal minPriceStep = decimal.MaxValue;
                    int countFive = 0;

                    CultureInfo culture = CultureInfo;

                    for (int i2 = 0; i2 < 20; i2++)
                    {
                        Candle candleN = new Candle();
                        candleN.SetCandleFromString(reader.ReadLine());

                        decimal open = (decimal) Convert.ToDouble(candleN.Open);
                        decimal high = (decimal) Convert.ToDouble(candleN.High);
                        decimal low = (decimal) Convert.ToDouble(candleN.Low);
                        decimal close = (decimal) Convert.ToDouble(candleN.Close);

                        if (open.ToString(culture).Split(',').Length > 1 ||
                            high.ToString(culture).Split(',').Length > 1 ||
                            low.ToString(culture).Split(',').Length > 1 ||
                            close.ToString(culture).Split(',').Length > 1)
                        {
                            // if the real part takes place / если имеет место вещественная часть
                            int lenght = 1;

                            if (open.ToString(culture).Split(',').Length > 1 &&
                                open.ToString(culture).Split(',')[1].Length > lenght)
                            {
                                lenght = open.ToString(culture).Split(',')[1].Length;
                            }

                            if (high.ToString(culture).Split(',').Length > 1 &&
                                high.ToString(culture).Split(',')[1].Length > lenght)
                            {
                                lenght = high.ToString(culture).Split(',')[1].Length;
                            }

                            if (low.ToString(culture).Split(',').Length > 1 &&
                                low.ToString(culture).Split(',')[1].Length > lenght)
                            {
                                lenght = low.ToString(culture).Split(',')[1].Length;
                            }

                            if (close.ToString(culture).Split(',').Length > 1 &&
                                close.ToString(culture).Split(',')[1].Length > lenght)
                            {
                                lenght = close.ToString(culture).Split(',')[1].Length;
                            }

                            if (lenght == 1 && minPriceStep > 0.1m)
                            {
                                minPriceStep = 0.1m;
                            }
                            if (lenght == 2 && minPriceStep > 0.01m)
                            {
                                minPriceStep = 0.01m;
                            }
                            if (lenght == 3 && minPriceStep > 0.001m)
                            {
                                minPriceStep = 0.001m;
                            }
                            if (lenght == 4 && minPriceStep > 0.0001m)
                            {
                                minPriceStep = 0.0001m;
                            }
                            if (lenght == 5 && minPriceStep > 0.00001m)
                            {
                                minPriceStep = 0.00001m;
                            }
                            if (lenght == 6 && minPriceStep > 0.000001m)
                            {
                                minPriceStep = 0.000001m;
                            }
                            if (lenght == 7 && minPriceStep > 0.0000001m)
                            {
                                minPriceStep = 0.0000001m;
                            }
                            if (lenght == 8 && minPriceStep > 0.00000001m)
                            {
                                minPriceStep = 0.00000001m;
                            }
                            if (lenght == 9 && minPriceStep > 0.000000001m)
                            {
                                minPriceStep = 0.000000001m;
                            }
                        }
                        else
                        {
                            // if the real part doesn't take place / если вещественной части нет
                            int lenght = 1;

                            for (int i3 = open.ToString(culture).Length - 1; open.ToString(culture)[i3] == '0'; i3--)
                            {
                                lenght = lenght*10;
                            }

                            int lengthLow = 1;

                            for (int i3 = low.ToString(culture).Length - 1; low.ToString(culture)[i3] == '0'; i3--)
                            {
                                lengthLow = lengthLow*10;

                                if (lenght > lengthLow)
                                {
                                    lenght = lengthLow;
                                }
                            }

                            int lengthHigh = 1;

                            for (int i3 = high.ToString(culture).Length - 1; high.ToString(culture)[i3] == '0'; i3--)
                            {
                                lengthHigh = lengthHigh*10;

                                if (lenght > lengthHigh)
                                {
                                    lenght = lengthHigh;
                                }
                            }

                            int lengthClose = 1;

                            for (int i3 = close.ToString(culture).Length - 1; close.ToString(culture)[i3] == '0'; i3--)
                            {
                                lengthClose = lengthClose*10;

                                if (lenght > lengthClose)
                                {
                                    lenght = lengthClose;
                                }
                            }
                            if (minPriceStep > lenght)
                            {
                                minPriceStep = lenght;
                            }

                            if (minPriceStep == 1 &&
                                open%5 == 0 && high%5 == 0 &&
                                close%5 == 0 && low%5 == 0)
                            {
                                countFive++;
                            }
                        }
                    }


                    if (minPriceStep == 1 &&
                        countFive == 20)
                    {
                        minPriceStep = 5;
                    }


                    security[security.Count - 1].Security.PriceStep = minPriceStep;
                    security[security.Count - 1].Security.PriceStepCost = minPriceStep;


                    // last data / последняя дата
                    string lastString = null;

                    while (!reader.EndOfStream)
                    {
                        lastString = reader.ReadLine();
                    }


                    Candle candle3 = new Candle();
                    candle3.SetCandleFromString(lastString);
                    security[security.Count - 1].TimeEnd = candle3.TimeStart;
                    continue;
                }
                catch (Exception)
                {
                    security.Remove(security[security.Count - 1]);
                }
                finally
                {
                    reader.Close();
                }
            }
 
 // save securities 
 // сохраняем бумаги

            if (security == null || 
                security.Count == 0)
            {
                return;
            }

            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            if (SecuritiesTester == null)
            {
                SecuritiesTester = new List<SecurityTester>();
            }

            for (int i = 0; i < security.Count; i++)
            {
                if (_securities.Find(security1 => security1.Name == security[i].Security.Name) == null)
                {
                    _securities.Add(security[i].Security);
                }
                
                SecuritiesTester.Add(security[i]);
            }

// count the time
// считаем время

            if (SecuritiesTester.Count != 0)
            {
                for (int i = 0; i < SecuritiesTester.Count; i++)
                {
                    if ((TimeMin == DateTime.MinValue && SecuritiesTester[i].TimeStart != DateTime.MinValue) ||
                        (SecuritiesTester[i].TimeStart != DateTime.MinValue &&SecuritiesTester[i].TimeStart < TimeMin))
                    {
                        TimeMin = SecuritiesTester[i].TimeStart;
                        TimeStart = SecuritiesTester[i].TimeStart;
                        TimeNow = SecuritiesTester[i].TimeStart;
                    }
                    if (SecuritiesTester[i].TimeEnd != DateTime.MinValue &&
                        SecuritiesTester[i].TimeEnd > TimeMax)
                    {
                        TimeMax = SecuritiesTester[i].TimeEnd;
                        TimeEnd = SecuritiesTester[i].TimeEnd;
                    }
                }
            }

            // check in tester file data on the presence of multipliers and GO for securities
            // проверяем в файле тестера данные о наличии мультипликаторов и ГО для бумаг


            string pathToSecuritySettings = GetSecuritiesSettingsPath();
            List<string[]> array = LoadSecurityDopSettings(pathToSecuritySettings);

            for (int i = 0; array != null && i < array.Count; i++)
            {
                Security secu = GetSecurityForName(array[i][0]);

                if (secu != null)
                {
                    secu.Lot = array[i][1].ToDecimal();
                    secu.Go = array[i][2].ToDecimal();
                    secu.PriceStepCost = array[i][3].ToDecimal();
                    secu.PriceStep = array[i][4].ToDecimal();

                    if (SecuritiesTester[SecuritiesTester.Count -1].Security.Name == secu.Name)
                    {
                        SecuritiesTester[SecuritiesTester.Count - 1].Security.Lot = array[i][1].ToDecimal();
                        SecuritiesTester[SecuritiesTester.Count - 1].Security.Go = array[i][2].ToDecimal();
                        SecuritiesTester[SecuritiesTester.Count - 1].Security.PriceStepCost = array[i][3].ToDecimal();
                        SecuritiesTester[SecuritiesTester.Count - 1].Security.PriceStep = array[i][4].ToDecimal();

                    }
                }
            }

            if (TestingNewSecurityEvent != null)
            {
                TestingNewSecurityEvent();
            }
        }

        private void LoadTickFromFolder(string folderName)
        {
            string[] files = Directory.GetFiles(folderName);

            if (files.Length == 0)
            {
                return;
            }

            List<SecurityTester> security = new List<SecurityTester>();

            for (int i = 0; i < files.Length; i++)
            {
                security.Add(new SecurityTester());
                security[security.Count-1].FileAdress = files[i];
                security[security.Count - 1].NewCandleEvent += TesterServer_NewCandleEvent;
                security[security.Count - 1].NewTradesEvent += TesterServer_NewTradesEvent;
                security[security.Count - 1].NewMarketDepthEvent += TesterServer_NewMarketDepthEvent;
                security[security.Count - 1].LogMessageEvent += TesterServer_LogMessageEvent;

                string name = files[i].Split('\\')[files[i].Split('\\').Length - 1];

                security[security.Count - 1].Security = new Security();
                security[security.Count - 1].Security.Name = name;
                security[security.Count - 1].Security.Lot = 1;
                security[security.Count - 1].Security.NameClass = "TestClass";
                security[security.Count - 1].Security.Go = 1;
                security[security.Count - 1].Security.PriceStepCost = 1;
                security[security.Count - 1].Security.PriceStep = 1;
				// timeframe / тф
				// price step / шаг цены
				// begin / начало
				// end / конец

                StreamReader reader = new StreamReader(files[i]);

                // candles / свечи: 20110111,100000,19577.00000,19655.00000,19533.00000,19585.00000,2752
                // ticks ver.1 / тики 1 вар: 20150401,100000,86160.000000000,2
                // ticks ver.2 / тики 2 вар: 20151006,040529,3010,5,Buy/Sell/Unknown

                string str = reader.ReadLine();

                try
                {
                    // check whether ticks are in the file / смотрим тики ли в файле
                    Trade trade = new Trade();
                    trade.SetTradeFromString(str);
                    // ticks are in the file / в файле тики

                    security[security.Count - 1].TimeStart = trade.Time;
                    security[security.Count - 1].DataType = SecurityTesterDataType.Tick;

                    // price step / шаг цены

                    decimal minPriceStep = decimal.MaxValue;
                    int countFive = 0;

                    CultureInfo culture = CultureInfo;

                    for (int i2 = 0; i2 < 20; i2++)
                    {
                        Trade tradeN = new Trade();
                        tradeN.SetTradeFromString(reader.ReadLine());

                        decimal open = (decimal)Convert.ToDouble(tradeN.Price);


                        if (open.ToString(culture).Split(',').Length > 1)
                        {
                            // if the real part takes place / если имеет место вещественная часть
                            int lenght = 1;

                            if (open.ToString(culture).Split(',').Length > 1 &&
                                open.ToString(culture).Split(',')[1].Length > lenght)
                            {
                                lenght = open.ToString(culture).Split(',')[1].Length;
                            }


                            if (lenght == 1 && minPriceStep > 0.1m)
                            {
                                minPriceStep = 0.1m;
                            }
                            if (lenght == 2 && minPriceStep > 0.01m)
                            {
                                minPriceStep = 0.01m;
                            }
                            if (lenght == 3 && minPriceStep > 0.001m)
                            {
                                minPriceStep = 0.001m;
                            }
                            if (lenght == 4 && minPriceStep > 0.0001m)
                            {
                                minPriceStep = 0.0001m;
                            }
                            if (lenght == 5 && minPriceStep > 0.00001m)
                            {
                                minPriceStep = 0.00001m;
                            }
                            if (lenght == 6 && minPriceStep > 0.000001m)
                            {
                                minPriceStep = 0.000001m;
                            }
                            if (lenght == 7 && minPriceStep > 0.0000001m)
                            {
                                minPriceStep = 0.0000001m;
                            }
                            if (lenght == 8 && minPriceStep > 0.00000001m)
                            {
                                minPriceStep = 0.00000001m;
                            }
                            if (lenght == 9 && minPriceStep > 0.000000001m)
                            {
                                minPriceStep = 0.000000001m;
                            }
                        }
                        else
                        {
                            // if the real part doesn't take place / если вещественной части нет
                            int lenght = 1;

                            for (int i3 = open.ToString(culture).Length - 1; open.ToString(culture)[i3] == '0'; i3--)
                            {
                                lenght = lenght * 10;
                            }

                            if (minPriceStep > lenght)
                            {
                                minPriceStep = lenght;
                            }

                            if (lenght == 1 &&
                                open % 5 == 0)
                            {
                                countFive++;
                            }
                        }
                    }


                    if (minPriceStep == 1 &&
                        countFive == 20)
                    {
                        minPriceStep = 5;
                    }


                    security[security.Count - 1].Security.PriceStep = minPriceStep;
                    security[security.Count - 1].Security.PriceStepCost = minPriceStep;

                    // last data / последняя дата
                    string lastString2 = null;

                    while (!reader.EndOfStream)
                    {
                        lastString2 = reader.ReadLine();
                    }

                    Trade trade2 = new Trade();
                    trade2.SetTradeFromString(lastString2);
                    security[security.Count - 1].TimeEnd = trade2.Time;
                }
                catch (Exception)
                {
                    security.Remove(security[security.Count - 1]);
                }

                reader.Close();


            }

            // save securities / сохраняем бумаги

            if (security.Count == 0)
            {
                return;
            }

            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            if (SecuritiesTester == null)
            {
                SecuritiesTester = new List<SecurityTester>();
            }

            for (int i = 0; i < security.Count; i++)
            {
                if (_securities.Find(security1 => security1.Name == security[i].Security.Name) == null)
                {
                    _securities.Add(security[i].Security);
                }
                SecuritiesTester.Add(security[i]);
            }

            // count the time / считаем время 

            if (SecuritiesTester.Count != 0)
            {
                for (int i = 0; i < SecuritiesTester.Count; i++)
                {
                    if ((TimeMin == DateTime.MinValue && SecuritiesTester[i].TimeStart != DateTime.MinValue) ||
                        (SecuritiesTester[i].TimeStart != DateTime.MinValue && SecuritiesTester[i].TimeStart < TimeMin))
                    {
                        TimeMin = SecuritiesTester[i].TimeStart;
                        TimeStart = SecuritiesTester[i].TimeStart;
                        TimeNow = SecuritiesTester[i].TimeStart;
                    }
                    if (SecuritiesTester[i].TimeEnd != DateTime.MinValue &&
                        SecuritiesTester[i].TimeEnd > TimeMax)
                    {
                        TimeMax = SecuritiesTester[i].TimeEnd;
                        TimeEnd = SecuritiesTester[i].TimeEnd;
                    }
                }
            }

            // check in the tester file data on the presence of multipliers and GO for securities
            // проверяем в файле тестера данные о наличии мультипликаторов и ГО для бумаг

            string pathToSecuritySettings = GetSecuritiesSettingsPath();
            List<string[]> array = LoadSecurityDopSettings(pathToSecuritySettings);

            for (int i = 0; array != null && i < array.Count; i++)
            {
                Security secu = GetSecurityForName(array[i][0]);

                if (secu != null)
                {
                    secu.Lot = array[i][1].ToDecimal();
                    secu.Go = array[i][2].ToDecimal();
                    secu.PriceStepCost = array[i][3].ToDecimal();
                    secu.PriceStep = array[i][4].ToDecimal();
                }
            }

            if (TestingNewSecurityEvent != null)
            {
                TestingNewSecurityEvent();
            }
        }

        private void LoadMarketDepthFromFolder(string folderName)
        {
            string[] files = Directory.GetFiles(folderName);

            if (files.Length == 0)
            {
                return;
            }

            List<SecurityTester> security = new List<SecurityTester>();

            for (int i = 0; i < files.Length; i++)
            {
                security.Add(new SecurityTester());
                security[security.Count - 1].FileAdress = files[i];
                security[security.Count - 1].NewCandleEvent += TesterServer_NewCandleEvent;
                security[security.Count - 1].NewTradesEvent += TesterServer_NewTradesEvent;
                security[security.Count - 1].LogMessageEvent += TesterServer_LogMessageEvent;
                security[security.Count - 1].NewMarketDepthEvent += TesterServer_NewMarketDepthEvent;

                string name = files[i].Split('\\')[files[i].Split('\\').Length - 1];

                security[security.Count - 1].Security = new Security();
                security[security.Count - 1].Security.Name = name;
                security[security.Count - 1].Security.Lot = 1;
                security[security.Count - 1].Security.NameClass = "TestClass";
                security[security.Count - 1].Security.Go = 1;
                security[security.Count - 1].Security.PriceStepCost = 1;
                security[security.Count - 1].Security.PriceStep = 1;
				// timeframe / тф
				// price step / шаг цены
				// begin / начало
				// end / конец

                StreamReader reader = new StreamReader(files[i]);

                // NameSecurity_Time_Bids_Asks
                // Bids: level*level*level
                // level: Bid&Ask&Price

                string str = reader.ReadLine();

                try
                {
                    // check whether depth is in the file / смотрим стакан ли в файле

                    MarketDepth trade = new MarketDepth();
                    trade.SetMarketDepthFromString(str);

                    // depth is in the file / в файле стаканы

                    security[security.Count - 1].TimeStart = trade.Time;
                    security[security.Count - 1].DataType = SecurityTesterDataType.MarketDepth;

                    // price step / шаг цены

                    decimal minPriceStep = decimal.MaxValue;
                    int countFive = 0;

                    CultureInfo culture = CultureInfo;

                    for (int i2 = 0; i2 < 20; i2++)
                    {
                        MarketDepth tradeN = new MarketDepth();
                        tradeN.SetMarketDepthFromString(reader.ReadLine());

                        decimal open = (decimal)Convert.ToDouble(tradeN.Bids[0].Price);

                        if (open == 0)
                        {
                            open = (decimal)Convert.ToDouble(tradeN.Asks[0].Price);
                        }

                        if (open.ToString(culture).Split(',').Length > 1)
                        {
                            // if the real part takes place / если имеет место вещественная часть
                            int lenght = 1;

                            if (open.ToString(culture).Split(',').Length > 1 &&
                                open.ToString(culture).Split(',')[1].Length > lenght)
                            {
                                lenght = open.ToString(culture).Split(',')[1].Length;
                            }


                            if (lenght == 1 && minPriceStep > 0.1m)
                            {
                                minPriceStep = 0.1m;
                            }
                            if (lenght == 2 && minPriceStep > 0.01m)
                            {
                                minPriceStep = 0.01m;
                            }
                            if (lenght == 3 && minPriceStep > 0.001m)
                            {
                                minPriceStep = 0.001m;
                            }
                            if (lenght == 4 && minPriceStep > 0.0001m)
                            {
                                minPriceStep = 0.0001m;
                            }
                            if (lenght == 5 && minPriceStep > 0.00001m)
                            {
                                minPriceStep = 0.00001m;
                            }
                            if (lenght == 6 && minPriceStep > 0.000001m)
                            {
                                minPriceStep = 0.000001m;
                            }
                            if (lenght == 7 && minPriceStep > 0.0000001m)
                            {
                                minPriceStep = 0.0000001m;
                            }
                            if (lenght == 8 && minPriceStep > 0.00000001m)
                            {
                                minPriceStep = 0.00000001m;
                            }
                            if (lenght == 9 && minPriceStep > 0.000000001m)
                            {
                                minPriceStep = 0.000000001m;
                            }
                        }
                        else
                        {
                            // if the real part doesn't take place / если вещественной части нет
                            int lenght = 1;

                            for (int i3 = open.ToString(culture).Length - 1; open.ToString(culture)[i3] == '0'; i3--)
                            {
                                lenght = lenght * 10;
                            }

                            if (minPriceStep > lenght)
                            {
                                minPriceStep = lenght;
                            }

                            if (lenght == 1 &&
                                open % 5 == 0)
                            {
                                countFive++;
                            }
                        }
                    }


                    if (minPriceStep == 1 &&
                        countFive == 20)
                    {
                        minPriceStep = 5;
                    }


                    security[security.Count - 1].Security.PriceStep = minPriceStep;
                    security[security.Count - 1].Security.PriceStepCost = minPriceStep;

                    // last data / последняя дата
                    string lastString2 = null;

                    while (!reader.EndOfStream)
                    {
                        lastString2 = reader.ReadLine();
                    }

                    MarketDepth trade2 = new MarketDepth();
                    trade2.SetMarketDepthFromString(lastString2);
                    security[security.Count - 1].TimeEnd = trade2.Time;
                }
                catch
                {
                    security.Remove(security[security.Count - 1]);
                }

                reader.Close();
            }

			// save securities
            // сохраняем бумаги

            if (security == null ||
                security.Count == 0)
            {
                return;
            }

            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            if (SecuritiesTester == null)
            {
                SecuritiesTester = new List<SecurityTester>();
            }

            for (int i = 0; i < security.Count; i++)
            {
                if (_securities.Find(security1 => security1.Name == security[i].Security.Name) == null)
                {
                    _securities.Add(security[i].Security);
                }
                SecuritiesTester.Add(security[i]);
            }

			// count the time
            // считаем время 

            if (SecuritiesTester.Count != 0)
            {
                for (int i = 0; i < SecuritiesTester.Count; i++)
                {
                    if ((TimeMin == DateTime.MinValue && SecuritiesTester[i].TimeStart != DateTime.MinValue) ||
                        (SecuritiesTester[i].TimeStart != DateTime.MinValue && SecuritiesTester[i].TimeStart < TimeMin))
                    {
                        TimeMin = SecuritiesTester[i].TimeStart;
                        TimeStart = SecuritiesTester[i].TimeStart;
                        TimeNow = SecuritiesTester[i].TimeStart;
                    }
                    if (SecuritiesTester[i].TimeEnd != DateTime.MinValue &&
                        SecuritiesTester[i].TimeEnd > TimeMax)
                    {
                        TimeMax = SecuritiesTester[i].TimeEnd;
                        TimeEnd = SecuritiesTester[i].TimeEnd;
                    }
                }
            }

            // check in the tester file data on the presence of multipliers and GO for securities
            // проверяем в файле тестера данные о наличии мультипликаторов и ГО для бумаг

            string pathToSecuritySettings = GetSecuritiesSettingsPath();
            List<string[]> array = LoadSecurityDopSettings(pathToSecuritySettings);

            for (int i = 0; array != null && i < array.Count; i++)
            {
                Security secu = GetSecurityForName(array[i][0]);

                if (secu != null)
                {
                    secu.Lot = array[i][1].ToDecimal();
                    secu.Go = array[i][2].ToDecimal();
                    secu.PriceStepCost = array[i][3].ToDecimal();
                    secu.PriceStep = array[i][4].ToDecimal();
                }
            }

            if (TestingNewSecurityEvent != null)
            {
                TestingNewSecurityEvent();
            }
        }

        // получить истинный TimeFrameSpan
        // get true TimeFrameSpan
        private TimeSpan GetTimeSpan(StreamReader reader)
        {

            Candle lastCandle = null;

            TimeSpan lastTimeSpan = TimeSpan.MaxValue;

            int counter = 0;

            while (true)
            {
                if (reader.EndOfStream)
                {
                    return TimeSpan.Zero;
                }

                if (lastCandle == null)
                {
                    lastCandle = new Candle();
                    lastCandle.SetCandleFromString(reader.ReadLine());
                    continue;
                }

                var currentCandle = new Candle();
                currentCandle.SetCandleFromString(reader.ReadLine());

                var currentTimeSpan = currentCandle.TimeStart - lastCandle.TimeStart;

                lastCandle = currentCandle;

                if (currentTimeSpan < lastTimeSpan)
                {
                    lastTimeSpan = currentTimeSpan;
                    continue;
                }

                if (currentTimeSpan == lastTimeSpan)
                {
                    counter++;
                }

                if (counter >= 100)
                {
                    return lastTimeSpan;
                }
            }
        }

        /// <summary>
		/// unload the next batch of data from files
        /// выгрузить из файлов следующую партию данных
        /// </summary>
        private void LoadNextData()
        {
            if (_testerRegime == TesterRegime.Pause)
            {
                return;
            }
            if (TimeStart > TimeEnd || TimeNow > TimeEnd)
            {
                _testerRegime = TesterRegime.Pause;

                SendLogMessage(OsLocalization.Market.Message37, LogMessageType.System);
                if (TestingEndEvent != null)
                {
                    TestingEndEvent();
                }
                return;
            }

            if (_candleSeriesTesterActivate == null ||
                _candleSeriesTesterActivate.Count == 0)
            {
                _testerRegime = TesterRegime.Pause;

                SendLogMessage(OsLocalization.Market.Message38,
                    LogMessageType.System);
                if (TestingEndEvent != null)
                {
                    TestingEndEvent();
                }
                return;
            }

            if (_dataIsActive == false)
            {
                TimeNow = TimeNow.AddSeconds(1);
            }
            else if (_timeAddType == TimeAddInTestType.MilliSecond)
            {
                TimeNow = TimeNow.AddMilliseconds(1);
            }
            else if (_timeAddType == TimeAddInTestType.Second)
            {
                TimeNow = TimeNow.AddSeconds(1);
            }
            else if (_timeAddType == TimeAddInTestType.Minute)
            {
                TimeNow = TimeNow.AddMinutes(1);
            }

    for (int i = 0;_candleSeriesTesterActivate != null && i < _candleSeriesTesterActivate.Count; i++)
            {
                _candleSeriesTesterActivate[i].Load(TimeNow);
            }
        }

// check order execution
// проверка исполнения ордеров

        /// <summary>
		/// check order execution
        /// проверить ордера на исполненность
        /// </summary>
        private void CheckOrders()
        {
            if (OrdersActiv.Count == 0)
            {
                return;
            }

            for (int i = 0; i < OrdersActiv.Count; i++)
            {

                Order order = OrdersActiv[i];
                // check availability of securities on the market / проверяем наличие инструмента на рынке
                SecurityTester security =
                    _candleSeriesTesterActivate.Find(
                        tester =>
                            tester.Security.Name == order.SecurityNameCode &&
                            (tester.LastCandle != null || tester.LastTradeSeries != null ||
                             tester.LastMarketDepth != null));

                if (security == null)
                {
                    return;
                }

                if (security.DataType == SecurityTesterDataType.Tick)
                { // test with using ticks / прогон на тиках
                    List<Trade> trades = security.LastTradeSeries;

                    for (int indexTrades = 0; trades != null && indexTrades < trades.Count; indexTrades++)
                    {
                        if (CheckOrdersInTickTest(order, trades[indexTrades],false))
                        {
                            i--;
                            break;
                        }
                    }
                }
                else if(security.DataType == SecurityTesterDataType.Candle)
                { // test with using candles / прогон на свечках
                    Candle lastCandle = security.LastCandle;
                    if (CheckOrdersInCandleTest(order, lastCandle))
                    {
                        i--;
                    }
                }
                else if (security.DataType == SecurityTesterDataType.MarketDepth)
                {
                    // HERE!!!!!!!!!!!! / ЗДЕСЬ!!!!!!!!!!!!!!!!!!!!
                    MarketDepth depth = security.LastMarketDepth;

                    if (CheckOrdersInMarketDepthTest(order, depth))
                    {
                        i--;
                    }
                }
            }
        }

        /// <summary>
		/// check order execution with using candle testing
        /// проверить исполнение ордера при тестировании на свечках
        /// </summary>
        /// <param name="order">order/ордер</param>
        /// <param name="lastCandle">candle for checking execution/свеча на которой проверяем исполнение</param>
        /// <returns>if it is completed or responded in time, return true/если исполнилось или отозвалось по времени, возвратиться true</returns>
        private bool CheckOrdersInCandleTest(Order order, Candle lastCandle)
        {
            decimal minPrice = decimal.MaxValue;
            decimal maxPrice = 0;
            decimal openPrice = 0;
            DateTime time = ServerTime;

            if (lastCandle != null)
            {
                minPrice = lastCandle.Low;
                maxPrice = lastCandle.High;
                openPrice = lastCandle.Open;
                time = lastCandle.TimeStart;
            }

            if (time <= order.TimeCallBack && !order.IsStopOrProfit)
            {
                //CanselOnBoardOrder(order);
                return false;
            }

            if (order.IsStopOrProfit)
            {
                int slipage = 0;
                if (_slipageToStopOrder > 0)
                {
                    slipage = _slipageToStopOrder;
                }
                decimal realPrice = order.Price;
                if (order.Side == Side.Buy)
                {
                    if (minPrice > realPrice)
                    {
                        realPrice = lastCandle.Open;
                    }
                }
                if (order.Side == Side.Sell)
                {
                    if (maxPrice < realPrice)
                    {
                        realPrice = lastCandle.Open;
                    }
                }

                ExecuteOnBoardOrder(order, realPrice, time, slipage);
                return true;
            }


            // check whether the order passed / проверяем, прошёл ли ордер
            if (order.Side == Side.Buy)
            {
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price > minPrice) 
                    ||
                    (OrderExecutionType == OrderExecutionType.Touch && order.Price >= minPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty && 
                    _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection && 
                    order.Price > minPrice) 
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                    _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                    order.Price >= minPrice)
                    )
                {// execute / исполняем

                    decimal realPrice = order.Price;

                    if (realPrice > openPrice && order.IsStopOrProfit == false)
                    {
                        // if order is not quotation and put into the market / если заявка не котировачная и выставлена в рынок
                        realPrice = openPrice;
                    }
                    else if (order.IsStopOrProfit && order.Price > maxPrice)
                    {
                        realPrice = maxPrice;
                    }

                    int slipage = 0;

                    if (order.IsStopOrProfit && _slipageToStopOrder > 0)
                    {
                        slipage = _slipageToStopOrder;
                    }
                    else if (order.IsStopOrProfit == false && _slipageToSimpleOrder > 0)
                    {
                        slipage = _slipageToSimpleOrder;
                    }

                    if (realPrice > maxPrice)
                    {
                        realPrice = maxPrice;
                    }

                    ExecuteOnBoardOrder(order, realPrice, time, slipage);

                    for (int i = 0; i < OrdersActiv.Count; i++)
                    {
                        if (OrdersActiv[i].NumberUser == order.NumberUser)
                        {
                            OrdersActiv.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        {_lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection;}
                        else
                        {_lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch;}
                    }

                    return true;
                }
            }

            if (order.Side == Side.Sell)
            {
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price < maxPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.Touch && order.Price <= maxPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                     _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                     order.Price < maxPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                     _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                     order.Price <= maxPrice)
                    )
                {
// execute / исполняем
                    decimal realPrice = order.Price;

                    if (realPrice < openPrice && order.IsStopOrProfit == false)
                    {
                        // if order is not quotation and put into the market / если заявка не котировачная и выставлена в рынок
                        realPrice = openPrice;
                    }
                    else if (order.IsStopOrProfit && order.Price < minPrice)
                    {
                        realPrice = minPrice;
                    }

                    int slipage = 0;
                    if (order.IsStopOrProfit && _slipageToStopOrder > 0)
                    {
                        slipage = _slipageToStopOrder;
                    }
                    else if (order.IsStopOrProfit == false && _slipageToSimpleOrder > 0)
                    {
                        slipage = _slipageToSimpleOrder;
                    }

                    if (realPrice < minPrice)
                    {
                        realPrice = minPrice;
                    }

                    ExecuteOnBoardOrder(order, realPrice, time, slipage);

                    for (int i = 0; i < OrdersActiv.Count; i++)
                    {
                        if (OrdersActiv[i].NumberUser == order.NumberUser)
                        {
                            OrdersActiv.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection; }
                        else
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch; }
                    }

                    return true;
                }
            }

            // order didn't execute. check if it's time to recall / ордер не `исполнился. проверяем, не пора ли отзывать

            if (order.TimeCallBack.Add(order.LifeTime) <= ServerTime)
            {
                CanselOnBoardOrder(order);
                return true;
            }
            return false;
        }

        /// <summary>
		/// check order execution with using ticks testing
        /// проверить исполнение ордера при тиковом прогоне
        /// </summary>
        /// <param name="order">order for execution/ордер для исполнения</param>
        /// <param name="lastTrade">last price on the instrument/последняя цена по инструменту</param>
        /// <param name="firstTime">Is this the first execution check? If the first is possible execution at the current price./первая ли эта проверка на исполнение. Если первая то возможно исполнение по текущей цене.
        /// if false then execution is only by order price. In this case, we quote/есил false, то исполнение только по цене ордером. В этом случае мы котируем</param>
        /// <returns></returns>
        private bool CheckOrdersInTickTest(Order order, Trade lastTrade, bool firstTime)
        {
            SecurityTester security = SecuritiesTester.Find(tester => tester.Security.Name == order.SecurityNameCode);

            if (security == null)
            {
                return false;
            }

            // check whether the order passed/проверяем, прошёл ли ордер
            if (order.Side == Side.Buy)
            {
                 if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price > lastTrade.Price) 
                    ||
                    (OrderExecutionType == OrderExecutionType.Touch && order.Price >= lastTrade.Price)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty && 
                    _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                    order.Price > lastTrade.Price) 
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                    _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                    order.Price >= lastTrade.Price)
                    )
                {// execute/исполняем
                    int slipage = 0;

                    if (order.IsStopOrProfit && _slipageToStopOrder > 0)
                    {
                        slipage = _slipageToStopOrder;
                    }
                    else if (order.IsStopOrProfit == false && _slipageToSimpleOrder > 0)
                    {
                        slipage = _slipageToSimpleOrder;
                    }

                    ExecuteOnBoardOrder(order, lastTrade.Price, ServerTime, slipage);

                    for (int i = 0; i < OrdersActiv.Count; i++)
                    {
                        if (OrdersActiv[i].NumberUser == order.NumberUser)
                        {
                            OrdersActiv.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection; }
                        else
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch; }
                    }

                    return true;
                }
            }

            if (order.Side == Side.Sell)
            {
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price < lastTrade.Price)
                   ||
                   (OrderExecutionType == OrderExecutionType.Touch && order.Price <= lastTrade.Price)
                   ||
                   (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                   _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                   order.Price < lastTrade.Price)
                   ||
                   (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                   _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                   order.Price <= lastTrade.Price)
                   )
                {// execute/исполняем
                    int slipage = 0;

                    if (order.IsStopOrProfit && _slipageToStopOrder > 0)
                    {
                        slipage = _slipageToStopOrder;
                    }
                    else if (order.IsStopOrProfit == false && _slipageToSimpleOrder > 0)
                    {
                        slipage = _slipageToSimpleOrder;
                    }
                    ExecuteOnBoardOrder(order, lastTrade.Price, ServerTime, slipage);

                    for (int i = 0; i < OrdersActiv.Count; i++)
                    {
                        if (OrdersActiv[i].NumberUser == order.NumberUser)
                        {
                            OrdersActiv.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection; }
                        else
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch; }
                    }

                    return true;
                }
            }

            // order is not executed. check if it's time to recall / ордер не исполнился. проверяем, не пора ли отзывать

            if (order.TimeCallBack.Add(order.LifeTime) <= ServerTime)
            {
                CanselOnBoardOrder(order);
                return true;
            }
            return false;
        }

        /// <summary>
		/// check order execution by testing with using depth
        /// проверить исполнение ордера при тестировании на свечках
        /// </summary>
        /// <param name="order">order/ордер</param>
        /// <param name="lastMarketDepth">depth for checking execution/стакан на которой проверяем исполнение</param>
        /// <returns>if it is executed or responded in time, return true/если исполнилось или отозвалось по времени, возвратиться true</returns>
        private bool CheckOrdersInMarketDepthTest(Order order, MarketDepth lastMarketDepth)
        {
            if (lastMarketDepth == null)
            {
                return false;
            }
            decimal minPrice = lastMarketDepth.Asks[0].Price;
            decimal maxPrice = lastMarketDepth.Bids[0].Price;

            DateTime time = lastMarketDepth.Time;

            if (time <= order.TimeCallBack && !order.IsStopOrProfit)
            {
                CanselOnBoardOrder(order);
                return false;
            }

            // check whether the order passed / проверяем, прошёл ли ордер
            if (order.Side == Side.Buy)
            {
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price > minPrice)
                   ||
                   (OrderExecutionType == OrderExecutionType.Touch && order.Price >= minPrice)
                   ||
                   (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                   _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                   order.Price > minPrice)
                   ||
                   (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                   _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                   order.Price >= minPrice)
                   )
                {
                    decimal realPrice = order.Price;

                    int slipage = 0;

                    if (order.IsStopOrProfit && _slipageToStopOrder > 0)
                    {
                        slipage = _slipageToStopOrder;
                    }
                    else if (order.IsStopOrProfit == false && _slipageToSimpleOrder > 0)
                    {
                        slipage = _slipageToSimpleOrder;
                    }
                    ExecuteOnBoardOrder(order, realPrice, time, slipage);
                    for (int i = 0; i < OrdersActiv.Count; i++)
                    {
                        if (OrdersActiv[i].NumberUser == order.NumberUser)
                        {
                            OrdersActiv.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection; }
                        else
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch; }
                    }
                    return true;
                }
            }

            if (order.Side == Side.Sell)
            {
                if ((OrderExecutionType == OrderExecutionType.Intersection && order.Price < maxPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.Touch && order.Price <= maxPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                     _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Intersection &&
                     order.Price < maxPrice)
                    ||
                    (OrderExecutionType == OrderExecutionType.FiftyFifty &&
                     _lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch &&
                     order.Price <= maxPrice)
                    )
                {
                    // execute / исполняем
                    decimal realPrice = order.Price;

                    int slipage = 0;

                    if (order.IsStopOrProfit && _slipageToStopOrder > 0)
                    {
                        slipage = _slipageToStopOrder;
                    }
                    else if (order.IsStopOrProfit == false && _slipageToSimpleOrder > 0)
                    {
                        slipage = _slipageToSimpleOrder;
                    }

                    ExecuteOnBoardOrder(order, realPrice, time, slipage);
                    for (int i = 0; i < OrdersActiv.Count; i++)
                    {
                        if (OrdersActiv[i].NumberUser == order.NumberUser)
                        {
                            OrdersActiv.RemoveAt(i);
                            break;
                        }
                    }

                    if (OrderExecutionType == OrderExecutionType.FiftyFifty)
                    {
                        if (_lastOrderExecutionTypeInFiftyFiftyType == OrderExecutionType.Touch)
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Intersection; }
                        else
                        { _lastOrderExecutionTypeInFiftyFiftyType = OrderExecutionType.Touch; }
                    }
                    return true;
                }
            }

            // order didn't execute. check if it's time to recall / ордер не `исполнился. проверяем, не пора ли отзывать

            if (order.TimeCallBack.Add(order.LifeTime) <= ServerTime)
            {
                CanselOnBoardOrder(order);
                return true;
            }
            return false;
        }

        /// <summary>
		/// order execution type
        /// тип исполнения ордеров
        /// </summary>
        public OrderExecutionType OrderExecutionType
        {
            get { return _orderExecutionType; }
            set
            {
                _orderExecutionType = value;
                Save();
            }
        }
        private OrderExecutionType _orderExecutionType;

        /// <summary>
		/// the next type of order execution, if we chose type 50 * 50 and they should alternate
        /// cледующий по очереди тип исполнения заявки, 
        /// если мы выбрали тип 50*50 и они должны чередоваться
        /// </summary>
        private OrderExecutionType _lastOrderExecutionTypeInFiftyFiftyType;

        public int SlipageToSimpleOrder
        {
            get { return _slipageToSimpleOrder; }
            set
            {
                _slipageToSimpleOrder = value;
                Save();
            }
        }
        private int _slipageToSimpleOrder;

        public int SlipageToStopOrder
        {
            get { return _slipageToStopOrder; }
            set
            {
                _slipageToStopOrder = value;
                Save();
            }
        }
        private int _slipageToStopOrder;

// storage of additional security data: GO, Multipliers, Lots
// хранение дополнительных данных о бумагах: ГО, Мультипликаторы, Лоты

        private List<string[]> LoadSecurityDopSettings(string path)
        {
            if (SecuritiesTester.Count == 0)
            {
                return null;
            }

            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    List<string[]> array = new List<string[]>();

                    while (!reader.EndOfStream)
                    {
                        string[] set = reader.ReadLine().Split('$');
                        array.Add(set);
                    }

                    reader.Close();
                    return array;
                }
            }
            catch (Exception)
            {
                // send to the log / отправить в лог
            }
            return null;
        }

        public void SaveSecurityDopSettings(Security securityToSave)
        {
            if (SecuritiesTester.Count == 0)
            {
                return;
            }

            string pathToSettings = GetSecuritiesSettingsPath();

            List<string[]> saves = LoadSecurityDopSettings(pathToSettings);

            if (saves == null)
            {
                saves = new List<string[]>();
            }

            CultureInfo culture = CultureInfo;

            for (int i = 0; i < saves.Count; i++)
            { // delete the same / удаляем совпадающие

                if (saves[i][0] == securityToSave.Name)
                {
                    saves.Remove(saves[i]);
                    saves.Add(new[] { securityToSave.Name, 
                    securityToSave.Lot.ToString(culture), 
                    securityToSave.Go.ToString(culture), 
                    securityToSave.PriceStepCost.ToString(culture),
                    securityToSave.PriceStep.ToString(culture)
                    });
                }
            }

            if (saves.Count == 0)
            {
                saves.Add(new[]
                {
                    securityToSave.Name,
                    securityToSave.Lot.ToString(culture),
                    securityToSave.Go.ToString(culture),
                    securityToSave.PriceStepCost.ToString(culture),
                    securityToSave.PriceStep.ToString(culture)
                });
            }

            bool isInArray = false;

            for (int i = 0; i < saves.Count; i++)
            {
                if (saves[i][0] == securityToSave.Name)
                {
                    isInArray = true;
                }
            }

            if (isInArray == false)
            {
                saves.Add(new[]
                {
                    securityToSave.Name,
                    securityToSave.Lot.ToString(culture),
                    securityToSave.Go.ToString(culture),
                    securityToSave.PriceStepCost.ToString(culture),
                    securityToSave.PriceStep.ToString(culture)
                });
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(pathToSettings, false))
                {
                    // name, lot, GO, price step, cost of price step / Имя, Лот, ГО, Цена шага, стоимость цены шага
                    for (int i = 0; i < saves.Count; i++)
                    {
                        writer.WriteLine(
                            saves[i][0] + "$" +
                            saves[i][1] + "$" +
                            saves[i][2] + "$" +
                            saves[i][3] + "$" +
                            saves[i][4]
                            );
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // send to the log / отправить в лог
            }

            if (NeadToReconnectEvent != null)
            {
                NeadToReconnectEvent();
            }
        }

// server status
// статус сервера

        private ServerConnectStatus _serverConnectStatus;

        /// <summary>
		/// server status
        /// статус сервера
        /// </summary>
        public ServerConnectStatus ServerStatus
        {
            get { return _serverConnectStatus; }
            private set
            {
                if (value != _serverConnectStatus)
                {
                    _serverConnectStatus = value;
                    SendLogMessage(_serverConnectStatus + OsLocalization.Market.Message7, LogMessageType.Connect);
                    if (ConnectStatusChangeEvent != null)
                    {
                        ConnectStatusChangeEvent(_serverConnectStatus.ToString());
                    }
                }
            }
        }

        /// <summary>
		/// changed connection status
        /// изменился статус соединения
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

        public int CountDaysTickNeadToSave { get; set; }

        public bool NeadToSaveTicks { get; set; }

// server time
// время сервера

        private DateTime _serverTime;
        /// <summary>
		/// server time
        /// время сервера
        /// </summary>
        public DateTime ServerTime
        {
            get { return _serverTime; }

            private set
            {
                if (value > _serverTime)
                {
                    DateTime lastTime = _serverTime;
                    _serverTime = value;

                    if (_serverTime != lastTime &&
                        TimeServerChangeEvent != null)
                    {
                        TimeServerChangeEvent(_serverTime);
                    }
                }
            }
        }

        /// <summary>
		/// changed server time
        /// изменилось время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

// profits and losses of exchange
// прибыли и убытки биржи

        public List<decimal> ProfitArray;

        /// <summary>
		/// whether calculation of internal profit of the exchange is included
        /// включен ли рассчёт внутреннего профита биржи
        /// </summary>
        public bool ProfitMarketIsOn
        {
            get { return _profitMarketIsOn; }
            set
            {
                _profitMarketIsOn = value;
                Save();
            }
        }
        private bool _profitMarketIsOn;

        public void AddProfit(decimal profit)
        {
            if(_profitMarketIsOn == false)
            {
                return;
            }
            _portfolios[0].ValueCurrent += profit;
            ProfitArray.Add(_portfolios[0].ValueCurrent);

            if (NewCurrentValue != null)
            {
                NewCurrentValue(_portfolios[0].ValueCurrent);
            }
        }

        public event Action<decimal> NewCurrentValue; 

// portfolios and positions on the exchange
// портфели и позиция на бирже

        /// <summary>
		/// initial portfolio value when testing
        /// начальное значение портфеля при тестировании
        /// </summary>
        public decimal StartPortfolio;

        private List<Portfolio> _portfolios;

        /// <summary>
		/// portfolios
        /// портфели
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }

        /// <summary>
		/// changed portfolio position
        /// изменить позицию по портфелю
        /// </summary>
        /// <param name="orderExecute">executed order that will affect the position/исполненный ордер который повлияет на позицию</param>
        private void ChangePosition(Order orderExecute)
        {
            List<PositionOnBoard> positions = _portfolios[0].GetPositionOnBoard();

            if(positions == null ||
                orderExecute == null)
            {
                return;
            }

            PositionOnBoard myPositioin =
                positions.Find(board => board.SecurityNameCode == orderExecute.SecurityNameCode);

            if (myPositioin == null)
            {
                myPositioin = new PositionOnBoard();
                myPositioin.SecurityNameCode = orderExecute.SecurityNameCode;
                myPositioin.PortfolioName = orderExecute.PortfolioNumber;
                myPositioin.ValueBegin = 0;
            }

            if (orderExecute.Side == Side.Buy)
            {
                myPositioin.ValueCurrent += orderExecute.Volume;
            }

            if (orderExecute.Side == Side.Sell)
            {
                myPositioin.ValueCurrent -= orderExecute.Volume;
            }

            _portfolios[0].SetNewPosition(myPositioin);

            if (PortfoliosChangeEvent != null)
            {
                PortfoliosChangeEvent(_portfolios);
            }
        }

        /// <summary>
		/// incoming new portfolios
        /// входящие новые портфели из ДДЕ сервера
        /// </summary>
        void UpdatePortfolios(List<Portfolio> portfoliosNew)
        {

           _portfolios = portfoliosNew;

            if (PortfoliosChangeEvent != null)
            {
                PortfoliosChangeEvent(_portfolios);
            }
        }

        /// <summary>
		/// take portfolios by number/name
        /// взять портфель по номеру/названию
        /// </summary>
        public Portfolio GetPortfolioForName(string name)
        {
            if (_portfolios == null)
            {
                return null;
            }

            return _portfolios.Find(portfolio => portfolio.Number == name);
        }

        /// <summary>
		/// changed the portfolio
        /// изменился портфель
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

 // securities / бумаги


        private List<Security> _securities;

        /// <summary>
		/// all securities available for trading
        /// все бумаги доступные для торгов
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }

        /// <summary>
		/// take security as Security class by name
        /// взять бумагу в виде класса Security по названию
        /// </summary>
        public Security GetSecurityForName(string name)
        {
            if (_securities == null)
            {
                return null;
            }

            return _securities.Find(security => security.Name == name);
        }

        /// <summary>
		/// incoming candles from CandleManager
        /// входящие свечки из CandleManager
        /// </summary>
        void _candleManager_CandleUpdateEvent(CandleSeries series)
        {
            if (_testerRegime == TesterRegime.PlusOne)
            {
                _testerRegime = TesterRegime.Pause;
            }

            // write last tick time in server time / перегружаем последним временем тика время сервера
            ServerTime = series.CandlesAll[series.CandlesAll.Count - 1].TimeStart;

            if (NewCandleIncomeEvent != null)
            {
                NewCandleIncomeEvent(series);
            }
        }

        /// <summary>
		/// tester instrument changed
        /// инструменты тестера изменились
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        /// <summary>
		/// show instruments
        /// показать инструменты 
        /// </summary>
        public void ShowSecuritiesDialog()
        {
            SecuritiesUi ui = new SecuritiesUi(this);
            ui.ShowDialog();
        }

// subscribe instruments to download / Заказ инструмента на скачивание


        /// <summary>
        /// candle series of Tester for downloading
        /// серии свечек Тестера запущенные на скачивание
        /// </summary>
        private List<SecurityTester> _candleSeriesTesterActivate;

        /// <summary>
		/// tick candle loading wizard
        /// мастер загрузки свечек из тиков
        /// </summary>
        private CandleManager _candleManager;

        private object _starterLocker = new object();

        /// <summary>
		/// start downloading data by instrument
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="namePaper">security name for testing / имя бумаги которую будем запускать</param>
        /// <param name="timeFrameBuilder">object with timeframe / объект несущий в себе данные о таймФрейме</param>
        /// <returns>In case of success returns CandleSeries / В случае удачи возвращает CandleSeries
        /// in case of failure null / в случае неудачи null</returns>
        public CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder)
        {
            lock (_starterLocker)
            {
                if (namePaper == "")
                {
                    return null;
                }
                // need to start the server if it is still disabled / надо запустить сервер если он ещё отключен
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return null;
                }

                if (_securities == null || _portfolios == null)
                {
                    return null;
                }

                Security security = null;

                for (int i = 0; i < _securities.Count; i++)
                {
                    if (_securities[i].Name == namePaper)
                    {
                        security = _securities[i];
                        break;
                    }
                }

                if (security == null)
                {
                    return null;
                }

                // find security / находим бумагу

                if (TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                    TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
                {
                    timeFrameBuilder.CandleMarketDataType = CandleMarketDataType.MarketDepth;
                }

                if (TypeTesterData == TesterDataType.TickAllCandleState ||
                    TypeTesterData == TesterDataType.TickOnlyReadyCandle)
                {
                    timeFrameBuilder.CandleMarketDataType = CandleMarketDataType.Tick;
                }

                CandleSeries series = new CandleSeries(timeFrameBuilder, security, StartProgram.IsTester);

   // start security for unloading / запускаем бумагу на выгрузку

                if (TypeTesterData != TesterDataType.Candle &&
                    timeFrameBuilder.CandleMarketDataType == CandleMarketDataType.Tick)
                {
                    if (_candleSeriesTesterActivate.Find(tester => tester.Security.Name == namePaper &&
                                                                   tester.DataType == SecurityTesterDataType.Tick) == null)
                    {
                        if (SecuritiesTester.Find(tester => tester.Security.Name == namePaper &&
                                                            tester.DataType == SecurityTesterDataType.Tick) != null)
                        {
                            _candleSeriesTesterActivate.Add(
                                    SecuritiesTester.Find(tester => tester.Security.Name == namePaper &&
                                           tester.DataType == SecurityTesterDataType.Tick));
                        }
                        else
                        { // there is nothing to run the series / нечем запускать серию
                            return null;
                        }
                    }
                }

                else if (TypeTesterData != TesterDataType.Candle &&
                         timeFrameBuilder.CandleMarketDataType == CandleMarketDataType.MarketDepth)
                {
                    if (_candleSeriesTesterActivate.Find(tester => tester.Security.Name == namePaper &&
                                                                   tester.DataType == SecurityTesterDataType.MarketDepth) == null)
                    {
                        if (SecuritiesTester.Find(tester => tester.Security.Name == namePaper &&
                                                            tester.DataType == SecurityTesterDataType.MarketDepth) != null)
                        {
                            _candleSeriesTesterActivate.Add(
                                    SecuritiesTester.Find(tester => tester.Security.Name == namePaper &&
                                           tester.DataType == SecurityTesterDataType.MarketDepth));
                        }
                        else
                        { // there is nothing to run the series / нечем запускать серию
                            return null;
                        }
                    }
                }
                else if (TypeTesterData == TesterDataType.Candle)
                {
                    TimeSpan time = GetTimeFremeInSpan(timeFrameBuilder.TimeFrame);
                    if (_candleSeriesTesterActivate.Find(tester => tester.Security.Name == namePaper &&
                                                                   tester.DataType == SecurityTesterDataType.Candle &&
                                                                   tester.TimeFrameSpan == time) == null)
                    {
                        if (SecuritiesTester.Find(tester => tester.Security.Name == namePaper &&
                                                            tester.DataType == SecurityTesterDataType.Candle &&
                                                            tester.TimeFrameSpan == time) == null)
                        {
                            return null;
                        }

                        _candleSeriesTesterActivate.Add(
                            SecuritiesTester.Find(tester => tester.Security.Name == namePaper &&
                                                            tester.DataType == SecurityTesterDataType.Candle &&
                                                            tester.TimeFrameSpan == time));
                    }
                }

                _candleManager.StartSeries(series);

                SendLogMessage(OsLocalization.Market.Message14 + series.Security.Name +
                               OsLocalization.Market.Message15 + series.TimeFrame +
                               OsLocalization.Market.Message16, LogMessageType.System);

                return series;
            }
        }

        /// <summary>
		/// start data downloading on instrument 
        /// Начать выгрузку данных по инструменту
        /// </summary>
        public CandleSeries GetCandleDataToSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder, DateTime startTime,
            DateTime endTime, DateTime actualTime, bool neadToUpdate)
        {
            return StartThisSecurity(namePaper, timeFrameBuilder);
        }

        /// <summary>
		/// take ticks data on instrument for period
        /// взять тиковые данные по инструменту за определённый период
        /// </summary>
        public bool GetTickDataToSecurity(string namePaper, DateTime startTime, DateTime endTime, DateTime actualTime,
            bool neadToUpdete)
        {
            return true;
        }

        private TimeSpan GetTimeFremeInSpan(TimeFrame frame)
        {
            TimeSpan result = new TimeSpan(0,0,1,0);

            if (frame == TimeFrame.Day)
            {
                result = new TimeSpan(1, 0, 0, 0);
            }
            if (frame == TimeFrame.Hour1)
            {
                result = new TimeSpan(0, 1, 0, 0);
            }
            if (frame == TimeFrame.Hour2)
            {
                result = new TimeSpan(0, 2, 0, 0);
            }
            if (frame == TimeFrame.Hour4)
            {
                result = new TimeSpan(0, 4, 0, 0);
            }
            if (frame == TimeFrame.Min1)
            {
                result = new TimeSpan(0, 0, 1, 0);
            }
            if (frame == TimeFrame.Min10)
            {
                result = new TimeSpan(0, 0, 10, 0);
            }
            if (frame == TimeFrame.Min15)
            {
                result = new TimeSpan(0, 0, 15, 0);
            }
            if (frame == TimeFrame.Min2)
            {
                result = new TimeSpan(0, 0, 2, 0);
            }
            if (frame == TimeFrame.Min20)
            {
                result = new TimeSpan(0, 0, 20, 0);
            }
            if (frame == TimeFrame.Min30)
            {
                result = new TimeSpan(0, 0, 30, 0);
            }
            if (frame == TimeFrame.Min5)
            {
                result = new TimeSpan(0, 0, 5, 0);
            }
            if (frame == TimeFrame.Sec1)
            {
                result = new TimeSpan(0, 0, 0, 1);
            }
            if (frame == TimeFrame.Sec10)
            {
                result = new TimeSpan(0, 0, 0, 10);
            }
            if (frame == TimeFrame.Sec15)
            {
                result = new TimeSpan(0, 0, 0, 15);
            }
            if (frame == TimeFrame.Sec2)
            {
                result = new TimeSpan(0, 0, 0, 2);
            }
            if (frame == TimeFrame.Sec20)
            {
                result = new TimeSpan(0, 0, 0, 20);
            }
            if (frame == TimeFrame.Sec30)
            {
                result = new TimeSpan(0, 0, 0, 30);
            }
            if (frame == TimeFrame.Sec5)
            {
                result = new TimeSpan(0, 0, 0, 5);
            }

            return result;
        }

        private TimeFrame GetTimeFrame(TimeSpan frameSpan)
        {
            TimeFrame timeFrame = TimeFrame.Min1;

            if (frameSpan == new TimeSpan(0, 0, 0, 1))
            {
                timeFrame = TimeFrame.Sec1;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 2) )
            {
                timeFrame = TimeFrame.Sec2;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 5) )
            {
                timeFrame = TimeFrame.Sec5;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 10) )
            {
                timeFrame = TimeFrame.Sec10;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 15) )
            {
                timeFrame = TimeFrame.Sec15;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 20) )
            {
                timeFrame = TimeFrame.Sec20;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 30) )
            {
                timeFrame = TimeFrame.Sec30;
            }
            else if (frameSpan ==  new TimeSpan(0, 0, 1, 0) )
            {
                timeFrame = TimeFrame.Min1;
            }
            else if (frameSpan == new TimeSpan(0, 0, 2, 0) )
            {
                timeFrame = TimeFrame.Min2;
            }
            else if (frameSpan == new TimeSpan(0, 0, 5, 0))
            {
                timeFrame = TimeFrame.Min5;
            }
            else if (frameSpan == new TimeSpan(0, 0, 10, 0) )
            {
                timeFrame = TimeFrame.Min10;
            }
            else if (frameSpan == new TimeSpan(0, 0, 15, 0) )
            {
                timeFrame = TimeFrame.Min15;
            }
            else if (frameSpan == new TimeSpan(0, 0, 20, 0) )
            {
                timeFrame = TimeFrame.Min20;
            }
            else if (frameSpan == new TimeSpan(0, 0, 30, 0) )
            {
                timeFrame = TimeFrame.Min30;
            }
            else if (frameSpan == new TimeSpan(0, 1, 0, 0))
            {
                timeFrame = TimeFrame.Hour1;
            }
            else if (frameSpan == new TimeSpan(0, 2, 0, 0))
            {
                timeFrame = TimeFrame.Hour2;
            }
            else if (frameSpan == new TimeSpan(24, 0, 0, 0) )
            {
                timeFrame = TimeFrame.Day;
            }

           return timeFrame;
        }

        /// <summary>
		/// stop getting data on instrument
        /// прекратить принимать данные по бумаге 
        /// </summary>
        public void StopThisSecurity(CandleSeries series)
        {
            if (series != null)
            {
                _candleManager.StopSeries(series);
            }
        }

        /// <summary>
		/// connectors connected to the server need to get a new data
        /// коннекторам подключеным к серверу необходимо перезаказать данные
        /// </summary>
        public event Action NeadToReconnectEvent;

// candles / свечи
        /// <summary>
		/// new candle appeared in the server
        /// в сервере появилась новая свечка
        /// </summary>
        void TesterServer_NewCandleEvent(Candle candle, string nameSecurity, TimeSpan timeFrame)
        {
            ServerTime = candle.TimeStart;

            if (_dataIsActive == false)
            {
                _dataIsActive = true;
            }

            if (NewBidAscIncomeEvent != null)
            {
                NewBidAscIncomeEvent(candle.Close, candle.Close,GetSecurityForName(nameSecurity));
            }

            _candleManager.SetNewCandleInSeries(candle, nameSecurity, timeFrame);
        }

        /// <summary>
		/// new candle appeared
        /// появилась новая свеча
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

// bid and ask / бид и аск

        /// <summary>
        /// bid and ask updated / обновился бид с аском
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

// depth / стакан

        void TesterServer_NewMarketDepthEvent(MarketDepth marketDepth)
        {
            if (_dataIsActive == false)
            {
                _dataIsActive = true;  
            }
            
            if (NewMarketDepthEvent != null)
            {
                NewMarketDepthEvent(marketDepth);
            }
        }

        /// <summary>
		/// updated depth
        /// обновился стакан
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

// all trades table
// таблица всех сделок

        /// <summary>
		/// all trades in the storage
        /// все сделки в хранилище
        /// </summary>
        private List<Trade>[] _allTrades;

        /// <summary>
		/// all ticks from server
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<Trade>[] AllTrades { get { return _allTrades; } }

        /// <summary>
		/// send new trades from server
        /// пришли новые сделки из сервера
        /// </summary>
        void TesterServer_NewTradesEvent(List<Trade> tradesNew)
        {
            if (_dataIsActive == false)
            {
                _dataIsActive = true;
            }

            if (_allTrades == null)
            {
                _allTrades = new List<Trade>[1];
                _allTrades[0] = new List<Trade>(tradesNew);
            }
            else
            {// sort trades by storages / сортируем сделки по хранилищам

                for (int indTrade = 0; indTrade < tradesNew.Count; indTrade++)
                {
                   Trade trade = tradesNew[indTrade];
                   bool isSave = false;
                   for (int i = 0; i < _allTrades.Length; i++)
                   {
                       if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                           _allTrades[i][0].SecurityNameCode == trade.SecurityNameCode)
                       { // if there is already a storage for this instrument, save it/ если для этого инструметна уже есть хранилище, сохраняем и всё
                           isSave = true;
                           if (_allTrades[i][0].Time > trade.Time)
                           {
                               break;
                           }
                           _allTrades[i].Add(trade);
                           break;
                       }
                   }
                   if (isSave == false)
                   { // there is no storage for instrument / хранилища для инструмента нет
                       List<Trade>[] allTradesNew = new List<Trade>[_allTrades.Length + 1];
                       for (int i = 0; i < _allTrades.Length; i++)
                       {
                           allTradesNew[i] = _allTrades[i];
                       }
                       allTradesNew[allTradesNew.Length - 1] = new List<Trade>();
                       allTradesNew[allTradesNew.Length - 1].Add(trade);
                       _allTrades = allTradesNew;
                   }
                }
            }

            ServerTime = tradesNew[tradesNew.Count - 1].Time;

            if (NewTradeEvent != null)
            {

                foreach (var trades in _allTrades)
                {
                    if (tradesNew[0].SecurityNameCode == trades[0].SecurityNameCode)
                    {
                        NewTradeEvent(trades);
                        break;
                    }
                }
            }

            if (NewBidAscIncomeEvent != null)
            {
                NewBidAscIncomeEvent(tradesNew[tradesNew.Count - 1].Price, tradesNew[tradesNew.Count - 1].Price, GetSecurityForName(tradesNew[tradesNew.Count - 1].SecurityNameCode));
            }
        }

        /// <summary>
		/// take all trades on instrument
        /// взять все сделки по инструменту
        /// </summary>
        public List<Trade> GetAllTradesToSecurity(Security security)
        {
            for (int i = 0; _allTrades != null && i < _allTrades.Length; i++)
            {
                if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                    _allTrades[i][0].SecurityNameCode == security.Name)
                {
                    return _allTrades[i];
                }
            }
            return null;
        }

        /// <summary>
		/// called when comes new deals on instrument
        /// вызывается когда по инструменту приходят новые сделки
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

// my trades
// мои сделки

        /// <summary>
		/// my incoming trades
        /// входящие мои сделки
        /// </summary>
        private void MyTradesIncome(MyTrade trade)
        {
            if (_myTrades == null)
            {
                _myTrades = new List<MyTrade>();
            }

            _myTrades.Add(trade);

            if (NewMyTradeEvent != null)
            {
                NewMyTradeEvent(trade);
            }
        }

        private List<MyTrade> _myTrades;

        /// <summary>
		/// my trades
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }

        /// <summary>
		/// called when new My Deal comes
        /// вызывается когда приходит новая Моя Сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

// work with placing and cancellation of my orders
// работа по выставлению и снятию моих ордеров

        /// <summary>
		/// placed order on the exchange
        /// выставленные на биржу ордера
        /// </summary>
        private List<Order> OrdersActiv;

        /// <summary>
		/// iterator of order numbers on the exchange
        /// итератор номеров ордеров на бирже
        /// </summary>
        private int _iteratorNumbersOrders;

        /// <summary>
		/// iterator of trade numbers on the exchange
        /// итератор номеров трэйдов на бирже
        /// </summary>
        private int _iteratorNumbersMyTrades;


        /// <summary>
		/// place order to the exchange
        /// выставить ордер на биржу
        /// </summary>
        public void ExecuteOrder(Order order)
        {

            order.TimeCreate = ServerTime;

            if (OrdersActiv.Count != 0)
            {
                for (int i = 0; i < OrdersActiv.Count; i++)
                {
                    if (OrdersActiv[i].NumberUser == order.NumberUser)
                    {
                        SendLogMessage(OsLocalization.Market.Message39, LogMessageType.Error);
                        FailedOperationOrder(order);
                        return;
                    }
                }
            }

            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                SendLogMessage(OsLocalization.Market.Message40, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            if (order.Price <= 0)
            {
                SendLogMessage(OsLocalization.Market.Message41 + order.Price, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            if (order.Volume <= 0)
            {
                SendLogMessage(OsLocalization.Market.Message42 + order.Volume, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            if (string.IsNullOrWhiteSpace(order.PortfolioNumber))
            {
                SendLogMessage(OsLocalization.Market.Message43, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            if (string.IsNullOrWhiteSpace(order.SecurityNameCode))
            {
                SendLogMessage(OsLocalization.Market.Message44, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            Order orderOnBoard = new Order();
            orderOnBoard.NumberMarket = _iteratorNumbersOrders++.ToString();
            orderOnBoard.NumberUser = order.NumberUser;
            orderOnBoard.PortfolioNumber = order.PortfolioNumber;
            orderOnBoard.Price = order.Price;
            orderOnBoard.SecurityNameCode = order.SecurityNameCode;
            orderOnBoard.Side = order.Side;
            orderOnBoard.State = OrderStateType.Activ;
            orderOnBoard.TimeCallBack = ServerTime;
            orderOnBoard.TimeCreate = ServerTime;
            orderOnBoard.TypeOrder = order.TypeOrder;
            orderOnBoard.Volume = order.Volume;
            orderOnBoard.Comment = order.Comment;
            orderOnBoard.LifeTime = order.LifeTime;
            orderOnBoard.IsStopOrProfit = order.IsStopOrProfit;

            OrdersActiv.Add(orderOnBoard);

            if (NewOrderIncomeEvent != null)
            {
                NewOrderIncomeEvent(orderOnBoard);
            }

            if (SecuritiesTester[0].DataType == SecurityTesterDataType.Tick)
            {
                SecurityTester security = SecuritiesTester.Find(tester => tester.Security.Name == order.SecurityNameCode);

                decimal f = order.Price/security.LastTradeSeries[security.LastTradeSeries.Count - 1].Price;
                if (f > 1.02m ||
                    f < 0.98m)
                {
                    
                }

                //CheckOrdersInTickTest(orderOnBoard, security.LastTradeSeries[security.LastTradeSeries.Count - 1].Price, toMarket);
                
            }

            if (orderOnBoard.IsStopOrProfit)
            {
                SecurityTester security = _candleSeriesTesterActivate.Find(tester => tester.Security.Name == order.SecurityNameCode);
                if (security.DataType == SecurityTesterDataType.Candle)
                { // testing with using candles / прогон на свечках
                    if (CheckOrdersInCandleTest(orderOnBoard, security.LastCandle))
                    {
                        OrdersActiv.Remove(orderOnBoard);
                    }
                }
                else if (security.DataType == SecurityTesterDataType.Tick)
                { // testing with using candles / прогон на свечках
                    if (CheckOrdersInTickTest(orderOnBoard, security.LastTrade, true))
                    {
                        OrdersActiv.Remove(orderOnBoard);
                    }
                }
            }
        }

        /// <summary>
		/// cancel order from the exchange
        /// отозвать ордер с биржи
        /// </summary>
        public void CancelOrder(Order order)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                SendLogMessage(OsLocalization.Market.Message45, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            CanselOnBoardOrder(order);
        }

        /// <summary>
		/// updated order on the exchange
        /// обновился ордер на бирже
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

// internal operations of the "exchange" on orders
// внутренние операции "биржи" над ордерами

        /// <summary>
		/// cancel order from the exchange
        /// провести отзыв ордера с биржи 
        /// </summary>
        private void CanselOnBoardOrder(Order order)
        {
            Order orderToClose = null;

            if (OrdersActiv.Count != 0)
            {
                for (int i = 0; i < OrdersActiv.Count; i++)
                {
                    if (OrdersActiv[i].NumberUser == order.NumberUser)
                    {
                        orderToClose = OrdersActiv[i];
                    }
                }
            }

            if (orderToClose == null)
            {
                SendLogMessage(OsLocalization.Market.Message46, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            for (int i = 0; i < OrdersActiv.Count; i++)
            {
                if (OrdersActiv[i].NumberUser == order.NumberUser)
                {
                    OrdersActiv.RemoveAt(i);
                    break;
                }
            }

            orderToClose.State = OrderStateType.Cancel;

            if (NewOrderIncomeEvent != null)
            {
                NewOrderIncomeEvent(orderToClose);
            }
        }

        /// <summary>
		/// reject order on the stock exchange
        /// провести отбраковку ордера на бирже
        /// </summary>
        private void FailedOperationOrder(Order order)
        {
            Order orderOnBoard = new Order();
            orderOnBoard.NumberMarket = _iteratorNumbersOrders++.ToString();
            orderOnBoard.NumberUser = order.NumberUser;
            orderOnBoard.PortfolioNumber = order.PortfolioNumber;
            orderOnBoard.Price = order.Price;
            orderOnBoard.SecurityNameCode = order.SecurityNameCode;
            orderOnBoard.Side = order.Side;
            orderOnBoard.State = OrderStateType.Fail;
            orderOnBoard.TimeCallBack = ServerTime;
            orderOnBoard.TimeCreate = order.TimeCreate;
            orderOnBoard.TypeOrder = order.TypeOrder;
            orderOnBoard.Volume = order.Volume;
            orderOnBoard.Comment = order.Comment;

            if (NewOrderIncomeEvent != null)
            {
                NewOrderIncomeEvent(orderOnBoard);
            }
        }
        
        /// <summary>
		/// execute order on the exchange
        /// исполнить ордер на бирже
        /// </summary>
        private void ExecuteOnBoardOrder(Order order,decimal price, DateTime time, int slipage)
        {
            decimal realPrice = price;

            if(order.Volume == order.VolumeExecute ||
                order.State == OrderStateType.Done)
            {
                return;
            }


            if (slipage != 0)
            {
                if (order.Side == Side.Buy)
                {
                    Security mySecurity = GetSecurityForName(order.SecurityNameCode);

                    if (mySecurity != null && mySecurity.PriceStep != 0)
                    {
                        realPrice += mySecurity.PriceStep * slipage;
                    }
                }

                if (order.Side == Side.Sell)
                {
                    Security mySecurity = GetSecurityForName(order.SecurityNameCode);

                    if (mySecurity != null && mySecurity.PriceStep != 0)
                    {
                        realPrice -= mySecurity.PriceStep * slipage;
                    }
                }
            }

            MyTrade trade = new MyTrade();
            trade.NumberOrderParent = order.NumberMarket;
            trade.NumberTrade = _iteratorNumbersMyTrades++.ToString();
            trade.SecurityNameCode = order.SecurityNameCode;
            trade.Volume = order.Volume;
            trade.Time = time;
            trade.Price = realPrice;
            trade.Side = order.Side;

            MyTradesIncome(trade);

            order.State = OrderStateType.Done;
            order.Price = realPrice;

            if (NewOrderIncomeEvent != null)
            {
                NewOrderIncomeEvent(order);
            }

            ChangePosition(order);
        }

// logging
// работа с логами

        /// <summary>
		/// send a new log message
        /// отправить в лог новый мессадж
        /// </summary>
        void TesterServer_LogMessageEvent(string logMessage)
        {
            SendLogMessage(logMessage,LogMessageType.Error);
        }

        /// <summary>
		/// save a log message new 
        /// сохранить новую запись в лог
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
		/// log manager
        /// лог менеджер
        /// </summary>
        /// 
        private Log _logMaster;

        /// <summary>
		/// called when there is a new message in the log
        /// вызывается когда есть новое сообщение в логе
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    /// <summary>
	/// Tester security. Encapsulates test data and data upload methods.
    /// бумага в тестере. 
    /// Инкапсулирует данные для тестирования и методы прогрузки данных
    /// </summary>
    public class SecurityTester
    {
        public SecurityTester()
        {
            if (ServerMaster.GetServers() != null &&
                ServerMaster.GetServers()[0] != null)
            {
                ServerMaster.GetServers()[0].NewCandleIncomeEvent += SecurityTester_NewCandleIncomeEvent;
            }
        }

        void SecurityTester_NewCandleIncomeEvent(CandleSeries series)
        {
            if (series.Security.Name == Security.Name && DataType != SecurityTesterDataType.Candle ||
                series.Security.Name == Security.Name && series.TimeFrame == TimeFrame)
            {
                LastCandle = series.CandlesAll[series.CandlesAll.Count - 1];
            }
        }

        /// <summary>
		/// security
        /// бумага которой принадлежит объект
        /// </summary>
        public Security Security;

        /// <summary>
		/// address of file with instrument data
        /// адрес файла с данными инструмента
        /// </summary>
        public string FileAdress;

        /// <summary>
		/// start time of data in the file
        /// время начала данных в файле
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
		/// end time of data in the file
        /// время конца данных в файле
        /// </summary>
        public DateTime TimeEnd;

        /// <summary>
		/// data type stored in the object
        /// Тип данных хранящихся в объекте
        /// </summary>
        public SecurityTesterDataType DataType;

        public TimeSpan TimeFrameSpan;

        public TimeFrame TimeFrame;

// data upload management
// управление выгрузгой данных

        /// <summary>
		/// whether the series is activated for unloading
        /// активирована ли серия для выгрузки
        /// </summary>
        public bool IsActiv;

        /// <summary>
		/// thread control reading the data file
        /// поток управляющий считыванием файла с данными
        /// </summary>
        private StreamReader _reader;

        /// <summary>
		/// clear object and bring it to the initial state ready for testing
        /// очистить объект и привести к начальному, готовому к тестированию состоянию
        /// </summary>
        public void Clear()
        {
            try
            {
                _reader = new StreamReader(FileAdress);
                LastCandle = null;
                LastTrade = null;
                LastMarketDepth = null;
            }
            catch (Exception errror)
            {
                SendLogMessage(errror.ToString());
            }
        }

        /// <summary>
		/// get new time object
		/// this method loads candles or ticks
        /// прогрузить объект новым временем
        /// этот метод и прогружает свечи или тики
        /// </summary>
        /// <param name="now">время для синхронизации</param>
        public void Load(DateTime now)
        {
            if (IsActiv == false)
            {
                return;
            }
            if (DataType == SecurityTesterDataType.Tick)
            {
                CheckTrades(now);
            }
            else if(DataType == SecurityTesterDataType.Candle)
            {
                CheckCandles(now);
            }
            else if (DataType == SecurityTesterDataType.MarketDepth)
            {
                CheckMarketDepth(now);
            }
        }

// parsing tick files
// разбор файлов тиковых

        /// <summary>
		/// last instrument trade from the file
        /// последний трейд инструмента из файла
        /// </summary>
        public Trade LastTrade;

        /// <summary>
		/// last downloaded ticks for the last second
        /// последние подгруженные тики за последнюю секунду
        /// </summary>
        public List<Trade> LastTradeSeries; 

        /// <summary>
		/// the last line from the reader, participates in loading
        /// последняя строка из ридера, участвует в прогрузке
        /// </summary>
        private string _lastString;

        /// <summary>
		/// check whether it is time to send a new batch of ticks
        /// проверить, не пора ли высылать новую партию тиков
        /// </summary>
        private void CheckTrades(DateTime now)
        {
            if (_reader == null || (_reader.EndOfStream && LastTrade == null))
            {
                _reader = new StreamReader(FileAdress);
            }
            if (now > TimeEnd ||
                now < TimeStart)
            {
                return;
            }

            if (LastTrade != null &&
                LastTrade.Time.AddMilliseconds(-LastTrade.Time.Millisecond) > now)
            {
                return;
            }

            // swing the first second if / качаем первую секунду если 

            if (LastTrade == null)
            {
                _lastString = _reader.ReadLine();
                LastTrade = new Trade();
                LastTrade.SetTradeFromString(_lastString);
                LastTrade.SecurityNameCode = Security.Name;
            }

            while (!_reader.EndOfStream &&
                   LastTrade.Time.AddMilliseconds(-LastTrade.Time.Millisecond) < now)
            {
                _lastString = _reader.ReadLine();
                LastTrade.SetTradeFromString(_lastString);
            }

            if (LastTrade.Time.AddMilliseconds(-LastTrade.Time.Millisecond) > now)
            {
                return;
            }

            // here we have the first trade in the current second / здесь имеем первый трейд в текущей секунде

            List<Trade> lastTradesSeries = new List<Trade>();

            Trade trade = new Trade() { SecurityNameCode = Security.Name };
            trade.SetTradeFromString(_lastString);
            lastTradesSeries.Add(trade);

            while (!_reader.EndOfStream)
            {
                _lastString = _reader.ReadLine();
                Trade tradeN = new Trade() { SecurityNameCode = Security.Name };
                tradeN.SetTradeFromString(_lastString);
                
                if (tradeN.Time.AddMilliseconds(-tradeN.Time.Millisecond) <= now)
                {
                    lastTradesSeries.Add(tradeN);
                }
                else
                {
                    LastTrade = tradeN;
                    break;
                }
            }

            LastTradeSeries = lastTradesSeries;

            if (NewTradesEvent != null)
            {
                NewTradesEvent(lastTradesSeries);
            }
        }

// parsing candle files
// разбор свечных файлов

        private Candle _lastCandle;

        /// <summary>
		/// last candle
        /// последняя свеча
        /// </summary>
        public Candle LastCandle
        {
            get { return _lastCandle; } 
            set { _lastCandle = value; }
        }

        /// <summary>
		/// check, is it time to send the candle
        /// провирить, не пора ли высылать свечку
        /// </summary>
        private void CheckCandles(DateTime now)
        {
            if (_reader == null || _reader.EndOfStream)
            {
                _reader = new StreamReader(FileAdress);
            }
            if (now > TimeEnd || 
                now < TimeStart)
            {
                return;
            }

            if (LastCandle != null &&
                LastCandle.TimeStart > now)
            {
                return;
            }

            if (LastCandle != null &&
                LastCandle.TimeStart == now)
            {
                List<Trade> lastTradesSeries = new List<Trade>();

                lastTradesSeries.Add(new Trade() { Price = LastCandle.Open, Volume = 1, Side = Side.Sell, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });
                lastTradesSeries.Add(new Trade() { Price = LastCandle.High, Volume = 1, Side = Side.Buy, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });
                lastTradesSeries.Add(new Trade() { Price = LastCandle.Low, Volume = 1, Side = Side.Sell, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });
                lastTradesSeries.Add(new Trade() { Price = LastCandle.Close, Volume = 1, Side = Side.Sell, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(lastTradesSeries);
                }

                if (NewCandleEvent != null)
                {
                    NewCandleEvent(LastCandle,Security.Name, TimeFrameSpan);
                }
                return;
            }

            while (LastCandle == null ||
                LastCandle.TimeStart < now)
            {
                LastCandle = new Candle();
                LastCandle.SetCandleFromString(_reader.ReadLine());
            }



            if (LastCandle.TimeStart <= now)
            {
                List<Trade> lastTradesSeries = new List<Trade>();

                lastTradesSeries.Add(new Trade() { Price = LastCandle.Open, Volume = 1, Side = Side.Sell, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });
                lastTradesSeries.Add(new Trade() { Price = LastCandle.High, Volume = 1, Side = Side.Buy, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });
                lastTradesSeries.Add(new Trade() { Price = LastCandle.Low, Volume = 1, Side = Side.Sell, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });
                lastTradesSeries.Add(new Trade() { Price = LastCandle.Close, Volume = 1, Side = Side.Sell, Time = LastCandle.TimeStart, SecurityNameCode = Security.Name });

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(lastTradesSeries);
                }

                if (NewCandleEvent != null)
                {
                    NewCandleEvent(LastCandle, Security.Name,TimeFrameSpan);
                }
                
            }
        }

        /// <summary>
		/// new ticks appeared
        /// новые тики появились
        /// </summary>
        public event Action<List<Trade>> NewTradesEvent;

        /// <summary>
		/// new candles appeared
        /// новые свечи появились
        /// </summary>
        public event Action<Candle, string,TimeSpan> NewCandleEvent;

        /// <summary>
		/// new depths appeared
        /// новые тики появились
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

// parsing depths
// разбор стаканов

        /// <summary>
		/// last trade of instrumnet from the file
        /// последний трейд инструмента из файла
        /// </summary>
        public MarketDepth LastMarketDepth;

        private void CheckMarketDepth(DateTime now)
        {
            if (_reader == null || _reader.EndOfStream)
            {
                _reader = new StreamReader(FileAdress);
            }

            if (now > TimeEnd ||
                now < TimeStart)
            {
                return;
            }

            if (LastMarketDepth != null &&
                LastMarketDepth.Time > now)
            {
                return;
            }

            // if download the first second / качаем первую секунду если 

            if (LastMarketDepth == null)
            {
                _lastString = _reader.ReadLine();
                LastMarketDepth = new MarketDepth();
                LastMarketDepth.SetMarketDepthFromString(_lastString);
                LastMarketDepth.SecurityNameCode = Security.Name;
            }

            while (!_reader.EndOfStream &&
                   LastMarketDepth.Time < now)
            {
                _lastString = _reader.ReadLine();
                LastMarketDepth.SetMarketDepthFromString(_lastString);
            }

            if (LastMarketDepth.Time > now)
            {
                return;
            }

            if (NewMarketDepthEvent != null)
            {
                NewMarketDepthEvent(LastMarketDepth);
            }
        }


// logging
// работа с логами

        /// <summary>
		/// save a new log message
        /// сохранить новую запись в лог
        /// </summary>
        private void SendLogMessage(string message)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message);
            }
        }

        /// <summary>
		/// called when there is a new log message
        /// вызывается когда есть новое сообщение в логе
        /// </summary>
        public event Action<string> LogMessageEvent;

    }

    /// <summary>
	/// data type
    /// тип данных 
    /// </summary>
    public enum TesterSourceDataType
    {

        /// <summary>
		/// data set
        /// Сет данных
        /// </summary>
        Set,

        /// <summary>
		/// folder with data
        /// папка с данными
        /// </summary>
        Folder
    }

    /// <summary>
	/// testing mode
    /// режимы тестирования
    /// </summary>
    public enum TesterRegime
    {
        /// <summary>
		/// pause
        /// пауза
        /// </summary>
        Pause,

        /// <summary>
		/// play
        /// работает
        /// </summary>
        Play,

        /// <summary>
		/// load the next data and pause
        /// надо прогрузить следующие данные и поставить на паузу
        /// </summary>
        PlusOne
    }

    /// <summary>
	/// data type from Tester
    /// тип данных идущих из тестера
    /// </summary>
    public enum TesterDataType
    {
        /// <summary>
		/// candles
        /// свечи
        /// </summary>
        Candle,

        /// <summary>
		/// ticks. all candles states
        /// тики. Все состояния свечей
        /// </summary>
        TickAllCandleState,

        /// <summary>
		/// ticks. only ready candles
        /// тики. Только готовые свечи
        /// </summary>
        TickOnlyReadyCandle,

        /// <summary>
		/// depth. all candle states
        /// стаканы. Все состояния свечей
        /// </summary>
        MarketDepthAllCandleState,

        /// <summary>
		/// depth. only ready ticks
        /// стаканы. Только готовые свечи
        /// </summary>
        MarketDepthOnlyReadyCandle,

        /// <summary>
		/// unknown
        /// неизвестно
        /// </summary>
        Unknown
    }

    /// <summary>
	/// data type stored in test security
    /// тип данных хранящийся в тестовой бумаге
    /// </summary>
    public enum SecurityTesterDataType
    {
        /// <summary>
		/// candles
        /// свечи
        /// </summary>
        Candle,

        /// <summary>
		/// ticks
        /// тики
        /// </summary>
        Tick,

        /// <summary>
		/// depth
        /// стакан
        /// </summary>
        MarketDepth
    }

    /// <summary>
	/// time step in the synchronizer
    /// шаг времени в синхронизаторе. 
    /// </summary>
    public enum TimeAddInTestType
    {
        /// <summary>
		/// minute
        /// минута
        /// </summary>
        Minute,
        /// <summary>
		/// second
        /// секунда
        /// </summary>
        Second,
        /// <summary>
		/// milisecond
        /// миллисекунда
        /// </summary>
        MilliSecond
    }

    /// <summary>
	/// type of order execution
    /// тип исполнения ордера
    /// </summary>
    public enum OrderExecutionType
    {
        /// <summary>
		/// intersection
        /// Пересечение
        /// </summary>
        Intersection,

        /// <summary>
		/// Touch
        /// Прикосновение
        /// </summary>
        Touch,

        /// <summary>
        /// 50 / 50
        /// </summary>
        FiftyFifty

    }
}
