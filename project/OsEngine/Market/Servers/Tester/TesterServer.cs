﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Market.Servers.Tester
{

    /// <summary>
    /// сервер для тестирования. 
    /// Поставляет синхронизированные данные. Исполняет заявки. Следит за позициями
    /// </summary>
    public class TesterServer: IServer
    {
// сервис

        /// <summary>
        /// конструктор
        /// </summary>
        public TesterServer()
        {
            _portfolios = new List<Portfolio>();
            _logMaster = new Log("TesterServer");
            _logMaster.Listen(this);
            _serverConnectStatus = ServerConnectStatus.Disconnect;
            ServerStatus = ServerConnectStatus.Disconnect;
            _testerRegime = TesterRegime.Pause;
            Commiss = 0;
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
                _worker.CurrentCulture = new CultureInfo("ru-RU");
                _worker.IsBackground = true;
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
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.Tester; }
        }

        private TesterServerUi _ui;

        /// <summary>
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
                    Commiss = Convert.ToInt32(reader.ReadLine());
                    StartPortfolio = Convert.ToDecimal(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out _typeTesterData);
                    Enum.TryParse(reader.ReadLine(), out _sourceDataType);
                    _pathToFolder = reader.ReadLine();
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"TestServer.txt", false))
                {
                    writer.WriteLine(_activSet);
                    writer.WriteLine(Commiss);
                    writer.WriteLine(StartPortfolio);
                    writer.WriteLine(_typeTesterData);
                    writer.WriteLine(_sourceDataType);
                    writer.WriteLine(_pathToFolder);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

// аппендикс от нормальных серверов

        /// <summary>
        /// в тестовом сервере не используется
        /// </summary>
        public void StartServer(){}

        /// <summary>
        /// в тестовом сервере не используется
        /// </summary>
        public void StopServer(){}

// Управление

        /// <summary>
        /// начать тестирование
        /// </summary>
        public void TestingStart()
        {
            _testerRegime = TesterRegime.Pause;
            Thread.Sleep(2000);
            _serverTime = DateTime.MinValue;

            ServerMaster.ClearOrders();



            SendLogMessage("Включен процесс тестирования с самого начала", LogMessageType.System);

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

            _candleManager.Clear();

            _allTrades = null;

            if (TimeStart == DateTime.MinValue)
            {
                SendLogMessage("Процесс тестирования прерван, т.к. не определена начальная дата тестирования", LogMessageType.System);
                return;
            }

            TimeNow = TimeStart;

            

            while (TimeNow.Hour != 10)
            {
               TimeNow = TimeNow.AddHours(-1);
            }

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
        /// ускорить тестирование, спрятав области
        /// </summary>
        public void TestingFast()
        {
            if (TestingFastEvent != null)
            {
                TestingFastEvent();
            }
            _testerRegime = TesterRegime.Play;
        }

        /// <summary>
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
        /// прогрузить следующую секунду и остановить тестирование
        /// </summary>
        public void TestingPlusOne()
        {
            _testerRegime = TesterRegime.PlusOne;
        }

        /// <summary>
        /// Тестирование запущено
        /// </summary>
        public event Action TestingStartEvent;

        /// <summary>
        /// включен режим перемотки
        /// </summary>
        public event Action TestingFastEvent;

        /// <summary>
        /// тестирование прервано
        /// </summary>
        public event Action TestingEndEvent;

        /// <summary>
        /// новые бумаги в тестере
        /// </summary>
        public event Action TestingNewSecurityEvent;

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
                SendLogMessage("В папке Data не обнаружено ни одного сета. Тестовый сервер не будет работать", LogMessageType.System);
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
                SendLogMessage("В папке Data не обнаружено ни одного сета. Тестовый сервер не будет работать", LogMessageType.System);
            }
            Sets = sets;
        }

        /// <summary>
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

            SendLogMessage("Подключаем новый сет данных: " + setName, LogMessageType.System);
            _activSet = newSet;

            if (_sourceDataType == TesterSourceDataType.Set)
            {
                ReloadSecurities();
            }
        }

        public void ReloadSecurities()
        {
            // чистим все данные, отключаемся
            _testerRegime = TesterRegime.Pause;
            _dataIsReady = false;
            ServerStatus = ServerConnectStatus.Disconnect;
            _securities = null;
            SecuritiesTester = null;
            _candleManager.Clear();
            _candleSeriesTesterActivate = new List<SecurityTester>();
            Save();

            // обновляем
            
            _needToReloadSecurities = true;

            if (NeadToReconnectEvent != null)
            {
                NeadToReconnectEvent();
            }
        }

        /// <summary>
        /// путь к папке с данными
        /// </summary>
        public string PathToFolder
        {
            get { return _pathToFolder; }
        }
        private string _pathToFolder;

        /// <summary>
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

// Синхронизатор

        /// <summary>
        /// путь к папке с данными. Он же название активного сета
        /// </summary>
        private string _activSet;
        public string ActiveSet
        {
            get { return _activSet; }
        }
        /// <summary>
        /// количество пунктов заложеные на комиссию / проскальзывание
        /// </summary>
        public int Commiss;

        /// <summary>
        /// минимальное время которое можно задать для синхронизации
        /// </summary>
        public DateTime TimeMin;

        /// <summary>
        /// максимальное время которое можно задать для синхронизации
        /// </summary>
        public DateTime TimeMax;

        /// <summary>
        /// время начала тестирования выбранное пользователем
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
        /// время конца тестирования выбранное пользователем
        /// </summary>
        public DateTime TimeEnd;

        /// <summary>
        /// время синхронизатора в данный момент подачи истории
        /// </summary>
        public DateTime TimeNow;

        /// <summary>
        /// готовы ли данные к загрузке
        /// </summary>
        private bool _dataIsReady;

        /// <summary>
        /// бумаги доступные для загрузки 
        /// </summary>
        public List<SecurityTester> SecuritiesTester;

        /// <summary>
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
                List<BotTabIndex> currentTabsSpread = bots[i].TabsIndex;

                for (int i2 = 0; currentTabsSpread != null && i2 < currentTabsSpread.Count; i2++)
                {
                    BotTabIndex index = currentTabsSpread[i2];

                    for (int i3 = 0; index.Tabs != null && i3 < index.Tabs.Count; i3++)
                    {
                        Connector currentConnector = index.Tabs[i3];

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

// место работы основного потока

        /// <summary>
        /// точность синхронизатора. Для свечек выше минуты - минутки. Для тиков - секунды. Для стаканов - миллисекунды
        /// устанавливается в методе SynhSecurities
        /// </summary>
        private TimeAddInTestType _timeAddType;

        /// <summary>
        /// пошли ли данные из серий данных
        /// </summary>
        private bool _dataIsActive;

        /// <summary>
        /// пора ли перезагружать бумаги в директории
        /// </summary>
        private bool _needToReloadSecurities;

        /// <summary>
        /// режим тестирования
        /// </summary>
        private TesterRegime _testerRegime;

        /// <summary>
        /// основной поток, которые занимается прогрузкой всех данных
        /// </summary>
        private Thread _worker;

        /// <summary>
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
                        SendLogMessage("Тестирование остановлено. Не подключены данные", LogMessageType.System);
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
                    SendLogMessage("Ошибка в основном потоке", LogMessageType.Error);
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        /// <summary>
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
            ProfitArray.Add(portfolio.ValueBegin);

            _portfolios = new List<Portfolio>();

            UpdatePortfolios(new[] { portfolio }.ToList());
        }

        /// <summary>
        /// загрузить данные о бумагах из директории
        /// </summary>
        private void LoadSecurities()
        {
            if (_sourceDataType == TesterSourceDataType.Set && !string.IsNullOrWhiteSpace(_activSet))
            { // сеты данных Геркулеса
                string[] directories = Directory.GetDirectories(_activSet);

                if (directories.Length == 0)
                {
                    SendLogMessage("Загрузка бумаг прервана. В указанном сете нет загруженных инструментов.", LogMessageType.System);
                    return;
                }

                for (int i = 0; i < directories.Length; i++)
                {
                    LoadSeciruty(directories[i]);
                }

                _dataIsReady = true;
            }
            else if (_sourceDataType == TesterSourceDataType.Folder&& !string.IsNullOrWhiteSpace(_pathToFolder))
            { // простые файлы из папки

                string[] files = Directory.GetFiles(_pathToFolder);

                if (files.Length == 0)
                {
                    SendLogMessage("Загрузка бумаг прервана. В указанной папке не содержиться ни одного файла.", LogMessageType.Error);
                }

                LoadCandleFromFolder(_pathToFolder);
                LoadTickFromFolder(_pathToFolder);
                LoadMarketDepthFromFolder(_pathToFolder);
                _dataIsReady = true;
            }
        }

        /// <summary>
        /// выгрузить один инструмент из папки
        /// </summary>
        /// <param name="path">путь к папке с инструментом</param>
        private void LoadSeciruty(string path)
        {
            TimeMax = DateTime.MinValue;
            TimeEnd = DateTime.MaxValue;
            TimeMin = DateTime.MaxValue;
            TimeStart = DateTime.MinValue;
            TimeNow = DateTime.MinValue;

            string[] directories = Directory.GetDirectories(path);

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
                // тф
                // шаг цены
                // начало
                // конец

                StreamReader reader = new StreamReader(files[i]);

                // свечи: 20110111,100000,19577.00000,19655.00000,19533.00000,19585.00000,2752
                // тики 1 вар: 20150401,100000,86160.000000000,2
                // тики 2 вар: 20151006,040529,3010,5,Buy/Sell/Unknown

                string str = reader.ReadLine();

                try
                {
                    // смотрим свечи ли в файле
                    Candle candle = new Candle();
                    candle.SetCandleFromString(str);
                    // в файле свечи. Смотрим какие именно

                    security[security.Count - 1].TimeStart = candle.TimeStart;

                    Candle candle2 = new Candle();
                    candle2.SetCandleFromString(reader.ReadLine());

                    security[security.Count - 1].DataType = SecurityTesterDataType.Candle;
                    security[security.Count - 1].TimeFrameSpan = candle2.TimeStart - candle.TimeStart;
                    security[security.Count - 1].TimeFrame = GetTimeFrame(security[security.Count - 1].TimeFrameSpan);
                    // шаг цены

                    decimal minPriceStep = decimal.MaxValue;
                    int countFive = 0;

                    CultureInfo culture = new CultureInfo("ru-RU");

                    for (int i2 = 0; i2 < 20; i2++)
                    {
                        Candle candleN = new Candle();
                        candleN.SetCandleFromString(reader.ReadLine());

                        decimal open = (decimal)Convert.ToDouble(candleN.Open);
                        decimal high = (decimal)Convert.ToDouble(candleN.High);
                        decimal low = (decimal)Convert.ToDouble(candleN.Low);
                        decimal close = (decimal)Convert.ToDouble(candleN.Close);

                        if (open.ToString(culture).Split(',').Length > 1 ||
                            high.ToString(culture).Split(',').Length > 1 ||
                            low.ToString(culture).Split(',').Length > 1 ||
                            close.ToString(culture).Split(',').Length > 1)
                        {
                            // если имеет место вещественная часть
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
                        }
                        else
                        {
                            // если вещественной части нет
                            int lenght = 1;

                            for (int i3 = open.ToString(culture).Length - 1; open.ToString(culture)[i3] == '0'; i3--)
                            {
                                lenght = lenght * 10;
                            }

                            int lengthLow = 1;

                            for (int i3 = low.ToString(culture).Length - 1; low.ToString(culture)[i3] == '0'; i3--)
                            {
                                lengthLow = lengthLow * 10;

                                if (lenght > lengthLow)
                                {
                                    lenght = lengthLow;
                                }
                            }

                            int lengthHigh = 1;

                            for (int i3 = high.ToString(culture).Length - 1; high.ToString(culture)[i3] == '0'; i3--)
                            {
                                lengthHigh = lengthHigh * 10;

                                if (lenght > lengthHigh)
                                {
                                    lenght = lengthHigh;
                                }
                            }

                            int lengthClose = 1;

                            for (int i3 = close.ToString(culture).Length - 1; close.ToString(culture)[i3] == '0'; i3--)
                            {
                                lengthClose = lengthClose * 10;

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
                                open % 5 == 0 && high % 5 == 0 &&
                                close % 5 == 0 && low % 5 == 0)
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


                    // последняя дата
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

                reader.Close();
            }
            
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

// проверяем в файле тестера данные о наличии мультипликаторов и ГО для бумаг

            List<string[]> array = LoadSecurityDopSettings(folderName + "\\SecuritiesSettings.txt");

            for (int i = 0; array != null && i < array.Count; i++)
            {
                Security secu = GetSecurityForName(array[i][0]);

                if (secu != null)
                {
                    secu.Lot = Convert.ToDecimal(array[i][1]);
                    secu.Go = Convert.ToDecimal(array[i][2]);
                    secu.PriceStepCost = Convert.ToDecimal(array[i][3]);
                    secu.PriceStep = Convert.ToDecimal(array[i][4]);

                    if(SecuritiesTester[SecuritiesTester.Count -1].Security.Name == secu.Name)
                    {
                        SecuritiesTester[SecuritiesTester.Count - 1].Security.Lot = Convert.ToDecimal(array[i][1]);
                        SecuritiesTester[SecuritiesTester.Count - 1].Security.Go = Convert.ToDecimal(array[i][2]);
                        SecuritiesTester[SecuritiesTester.Count - 1].Security.PriceStepCost = Convert.ToDecimal(array[i][3]);
                        SecuritiesTester[SecuritiesTester.Count - 1].Security.PriceStep = Convert.ToDecimal(array[i][4]);

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
                // тф
                // шаг цены
                // начало
                // конец

                StreamReader reader = new StreamReader(files[i]);

                // свечи: 20110111,100000,19577.00000,19655.00000,19533.00000,19585.00000,2752
                // тики 1 вар: 20150401,100000,86160.000000000,2
                // тики 2 вар: 20151006,040529,3010,5,Buy/Sell/Unknown

                string str = reader.ReadLine();

                try
                {
                    // смотрим тики ли в файле
                    Trade trade = new Trade();
                    trade.SetTradeFromString(str);
                    // в файле тики

                    security[security.Count - 1].TimeStart = trade.Time;
                    security[security.Count - 1].DataType = SecurityTesterDataType.Tick;

                    // шаг цены

                    decimal minPriceStep = decimal.MaxValue;
                    int countFive = 0;

                    CultureInfo culture = new CultureInfo("ru-RU");

                    for (int i2 = 0; i2 < 20; i2++)
                    {
                        Trade tradeN = new Trade();
                        tradeN.SetTradeFromString(reader.ReadLine());

                        decimal open = (decimal)Convert.ToDouble(tradeN.Price);


                        if (open.ToString(culture).Split(',').Length > 1)
                        {
                            // если имеет место вещественная часть
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
                        }
                        else
                        {
                            // если вещественной части нет
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

                    // последняя дата
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

            // сохраняем бумаги

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

            // проверяем в файле тестера данные о наличии мультипликаторов и ГО для бумаг

            List<string[]> array = LoadSecurityDopSettings(folderName + "\\SecuritiesSettings.txt");

            for (int i = 0; array != null && i < array.Count; i++)
            {
                Security secu = GetSecurityForName(array[i][0]);

                if (secu != null)
                {
                    secu.Lot = Convert.ToDecimal(array[i][1]);
                    secu.Go = Convert.ToDecimal(array[i][2]);
                    secu.PriceStepCost = Convert.ToDecimal(array[i][3]);
                    secu.PriceStep = Convert.ToDecimal(array[i][4]);
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
                // тф
                // шаг цены
                // начало
                // конец

                StreamReader reader = new StreamReader(files[i]);

                // NameSecurity_Time_Bids_Asks
                // Bids: level*level*level
                // level: Bid&Ask&Price

                string str = reader.ReadLine();

                try
                {
                    // смотрим стакан ли в файле

                    MarketDepth trade = new MarketDepth();
                    trade.SetMarketDepthFromString(str);

                    // в файле стаканы

                    security[security.Count - 1].TimeStart = trade.Time;
                    security[security.Count - 1].DataType = SecurityTesterDataType.MarketDepth;

                    // шаг цены

                    decimal minPriceStep = decimal.MaxValue;
                    int countFive = 0;

                    CultureInfo culture = new CultureInfo("ru-RU");

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
                            // если имеет место вещественная часть
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
                        }
                        else
                        {
                            // если вещественной части нет
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

                    // последняя дата
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

            // проверяем в файле тестера данные о наличии мультипликаторов и ГО для бумаг

            List<string[]> array = LoadSecurityDopSettings(folderName + "\\SecuritiesSettings.txt");

            for (int i = 0; array != null && i < array.Count; i++)
            {
                Security secu = GetSecurityForName(array[i][0]);

                if (secu != null)
                {
                    secu.Lot = Convert.ToDecimal(array[i][1]);
                    secu.Go = Convert.ToDecimal(array[i][2]);
                    secu.PriceStepCost = Convert.ToDecimal(array[i][3]);
                    secu.PriceStep = Convert.ToDecimal(array[i][4]);
                }
            }

            if (TestingNewSecurityEvent != null)
            {
                TestingNewSecurityEvent();
            }
        }

        /// <summary>
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

                SendLogMessage("Тестирование завершилось т.к. время таймера подошло к концу ", LogMessageType.System);
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

                SendLogMessage("Тестирование завершилось т.к. на инструменты сервера не подписан ни один бот ",
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

        /// <summary>
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
                // проверяем наличие инструмента на рынке
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
                { // прогон на тиках
                    List<Trade> trades = security.LastTradeSeries;

                    for (int indexTrades = 0; trades != null && indexTrades < trades.Count; indexTrades++)
                    {
                        if (CheckOrdersInTickTest(order, trades[indexTrades].Price,false))
                        {
                            i--;
                            break;
                        }
                    }
                }
                else if(security.DataType == SecurityTesterDataType.Candle)
                { // прогон на свечках
                    Candle lastCandle = security.LastCandle;
                    if (CheckOrdersInCandleTest(order, lastCandle))
                    {
                        i--;
                    }
                }
                else if (security.DataType == SecurityTesterDataType.MarketDepth)
                {
                    // ЗДЕСЬ!!!!!!!!!!!!!!!!!!!!
                    MarketDepth depth = security.LastMarketDepth;

                    if (CheckOrdersInMarketDepthTest(order, depth))
                    {
                        i--;
                    }
                }
            }
        }

        /// <summary>
        /// проверить исполнение ордера при тестировании на свечках
        /// </summary>
        /// <param name="order">ордер</param>
        /// <param name="lastCandle">свеча на которой проверяем исполнение</param>
        /// <returns>если исполнилось или отозвалось по времени, возвратиться true</returns>
        private bool CheckOrdersInCandleTest(Order order, Candle lastCandle)
        {
            decimal minPrice = decimal.MaxValue;
            decimal maxPrice = 0;
            decimal openPrice = 0;
            DateTime time = DateTime.MinValue;

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

            // проверяем, прошёл ли ордер
            if (order.Side == Side.Buy)
            {
                if ((order.Price > minPrice && order.IsStopOrProfit == false) ||
                    (order.Price > minPrice && order.IsStopOrProfit))
                {// исполняем

                    decimal realPrice = order.Price;

                    if (realPrice > openPrice && order.IsStopOrProfit == false)
                    {
                        // если заявка не котировачная и выставлена в рынок
                        realPrice = openPrice;
                    }
                    else if (order.IsStopOrProfit && order.Price > maxPrice)
                    {
                        realPrice = maxPrice;
                    }

                    ExecuteOnBoardOrder(order, realPrice, time);
                    OrdersActiv.Remove(order);
                    return true;
                }
            }

            if (order.Side == Side.Sell)
            {
                if ((order.Price < maxPrice && order.IsStopOrProfit == false) ||
                    (order.Price < maxPrice && order.IsStopOrProfit))
                {// исполняем
                    decimal realPrice = order.Price;

                    if (realPrice < openPrice && order.IsStopOrProfit == false)
                    { // если заявка не котировачная и выставлена в рынок
                        realPrice = openPrice;
                    }
                    else if (order.IsStopOrProfit && order.Price < minPrice)
                    {
                        realPrice = minPrice;
                    }

                    ExecuteOnBoardOrder(order, realPrice, time);
                    OrdersActiv.Remove(order);
                    return true;
                }
            }

            // ордер не `исполнился. проверяем, не пора ли отзывать

            if (order.TimeCallBack.Add(order.LifeTime) <= ServerTime)
            {
                CanselOnBoardOrder(order);
                return true;
            }
            return false;
        }

        /// <summary>
        /// проверить исполнение ордера при тиковом прогоне
        /// </summary>
        /// <param name="order">ордер для исполнения</param>
        /// <param name="lastTrade">последняя цена по инструменту</param>
        /// <param name="firstTime">первая ли эта проверка на исполнение. Если первая то возможно исполнение по текущей цене.
        /// есил false, то исполнение только по цене ордером. В этом случае мы котируем</param>
        /// <returns></returns>
        private bool CheckOrdersInTickTest(Order order, decimal lastTrade, bool firstTime)
        {
            SecurityTester security = SecuritiesTester.Find(tester => tester.Security.Name == order.SecurityNameCode);

            if (security == null)
            {
                return false;
            }

            // проверяем, прошёл ли ордер
            if (order.Side == Side.Buy)
            {
                if ((order.Price > lastTrade) 
                   // &&!firstTime
                    )
                {// исполняем

                    ExecuteOnBoardOrder(order, lastTrade, ServerTime);

                    OrdersActiv.Remove(order);
                    return true;
                }
            }

            if (order.Side == Side.Sell)
            {
                if ((order.Price < lastTrade)
                    //&&!firstTime
                    )
                {// исполняем

                    ExecuteOnBoardOrder(order, lastTrade, ServerTime);

                    OrdersActiv.Remove(order);
                    return true;
                }
            }

            // ордер не исполнился. проверяем, не пора ли отзывать

            if (order.TimeCallBack.Add(order.LifeTime) <= ServerTime)
            {
                CanselOnBoardOrder(order);
                return true;
            }
            return false;
        }

        /// <summary>
        /// проверить исполнение ордера при тестировании на свечках
        /// </summary>
        /// <param name="order">ордер</param>
        /// <param name="lastMarketDepth">стакан на которой проверяем исполнение</param>
        /// <returns>если исполнилось или отозвалось по времени, возвратиться true</returns>
        private bool CheckOrdersInMarketDepthTest(Order order, MarketDepth lastMarketDepth)
        {
            if (lastMarketDepth == null)
            {
                return false;
            }
            decimal minPrice = lastMarketDepth.Asks[0].Price;
            decimal maxPrice = lastMarketDepth.Bids[0].Price;
            decimal openPrice = lastMarketDepth.Asks[0].Price;

            DateTime time = lastMarketDepth.Time;

            if (time <= order.TimeCallBack && !order.IsStopOrProfit)
            {
                CanselOnBoardOrder(order);
                return false;
            }

            // проверяем, прошёл ли ордер
            if (order.Side == Side.Buy)
            {
                if ((order.Price > minPrice && order.IsStopOrProfit == false) ||
                    (order.Price > minPrice && order.IsStopOrProfit))
                {// исполняем

                    decimal realPrice = order.Price;

                    if (realPrice > openPrice && order.IsStopOrProfit == false)
                    {
                        // если заявка не котировачная и выставлена в рынок
                        realPrice = openPrice;
                    }
                    else if (order.IsStopOrProfit && order.Price > maxPrice)
                    {
                        realPrice = maxPrice;
                    }

                    ExecuteOnBoardOrder(order, realPrice, time);
                    OrdersActiv.Remove(order);
                    return true;
                }
            }

            if (order.Side == Side.Sell)
            {
                if ((order.Price < maxPrice && order.IsStopOrProfit == false) ||
                    (order.Price < maxPrice && order.IsStopOrProfit))
                {// исполняем
                    decimal realPrice = order.Price;

                    if (realPrice < openPrice && order.IsStopOrProfit == false)
                    { // если заявка не котировачная и выставлена в рынок
                        realPrice = openPrice;
                    }
                    else if (order.IsStopOrProfit && order.Price < minPrice)
                    {
                        realPrice = minPrice;
                    }

                    ExecuteOnBoardOrder(order, realPrice, time);
                    OrdersActiv.Remove(order);
                    return true;
                }
            }

            // ордер не `исполнился. проверяем, не пора ли отзывать

            if (order.TimeCallBack.Add(order.LifeTime) <= ServerTime)
            {
                CanselOnBoardOrder(order);
                return true;
            }
            return false;
        }

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
                // отправить в лог
            }
            return null;
        }

        public void SaveSecurityDopSettings(Security securityToSave)
        {
            if (SecuritiesTester.Count == 0)
            {
                return;
            }

            string pathToSettings = "";

            if (SourceDataType == TesterSourceDataType.Set)
            {
                if (string.IsNullOrWhiteSpace(_activSet))
                {
                    return;
                }
                pathToSettings = _activSet + "\\SecuritiesSettings.txt";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_pathToFolder))
                {
                    return;
                }
                pathToSettings = _pathToFolder + "\\SecuritiesSettings.txt";
            }

            List<string[]> saves = LoadSecurityDopSettings(pathToSettings);

            if (saves == null)
            {
                saves = new List<string[]>();
            }

            CultureInfo culture = new CultureInfo("ru-RU");

            for (int i = 0; i < saves.Count; i++)
            { // удаляем совпадающие

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
                    // Имя, Лот, ГО, Цена шага, стоимость цены шага
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
                // отправить в лог
            }

            if (NeadToReconnectEvent != null)
            {
                NeadToReconnectEvent();
            }
        }

// статус сервера

        private ServerConnectStatus _serverConnectStatus;

        /// <summary>
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
                    SendLogMessage(_serverConnectStatus + " Изменилось состояние соединения", LogMessageType.Connect);
                    if (ConnectStatusChangeEvent != null)
                    {
                        ConnectStatusChangeEvent(_serverConnectStatus.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// изменился статус соединения
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

// время сервера

        private DateTime _serverTime;
        /// <summary>
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
        /// изменилось время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;


// прибыли и убытки биржи

        public List<decimal> ProfitArray;

        public void AddProfit(decimal profit)
        {
            _portfolios[0].ValueCurrent += profit;
            ProfitArray.Add(_portfolios[0].ValueCurrent);

            if (NewCurrentValue != null)
            {
                NewCurrentValue(_portfolios[0].ValueCurrent);
            }
        }

        public event Action<decimal> NewCurrentValue; 

// портфели и позиция на бирже

        /// <summary>
        /// начальное значение портфеля при тестировании
        /// </summary>
        public decimal StartPortfolio;

        private List<Portfolio> _portfolios;

        /// <summary>
        /// портфели
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }

        /// <summary>
        /// изменить позицию по портфелю
        /// </summary>
        /// <param name="orderExecute">исполненный ордер который повлияет на позицию</param>
        private void ChangePosition(Order orderExecute)
        {
            List<PositionOnBoard> positions = _portfolios[0].GetPositionOnBoard();

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
        /// изменился портфель
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

// бумаги

        private List<Security> _securities;

        /// <summary>
        /// все бумаги доступные для торгов
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }

        /// <summary>
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
        /// входящие свечки из CandleManager
        /// </summary>
        void _candleManager_CandleUpdateEvent(CandleSeries series)
        {
            if (_testerRegime == TesterRegime.PlusOne)
            {
                _testerRegime = TesterRegime.Pause;
            }

            // перегружаем последним временем тика время сервера
            ServerTime = series.CandlesAll[series.CandlesAll.Count - 1].TimeStart;

            if (NewCandleIncomeEvent != null)
            {
                NewCandleIncomeEvent(series);
            }
        }

        /// <summary>
        /// инструменты тестера изменились
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        /// <summary>
        /// показать инструменты 
        /// </summary>
        public void ShowSecuritiesDialog()
        {
            SecuritiesUi ui = new SecuritiesUi(this);
            ui.ShowDialog();
        }

// Заказ инструмента на скачивание

        /// <summary>
        /// серии свечек Тестера запущенные на скачивание
        /// </summary>
        private List<SecurityTester> _candleSeriesTesterActivate;

        /// <summary>
        /// мастер загрузки свечек из тиков
        /// </summary>
        private CandleManager _candleManager;

        private object _starterLocker = new object();

        /// <summary>
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="namePaper">имя бумаги которую будем запускать</param>
        /// <param name="timeFrameBuilder">объект несущий в себе данные о таймФрейме</param>
        /// <returns>В случае удачи возвращает CandleSeries
        /// в случае неудачи null</returns>
        public CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder)
        {
            lock (_starterLocker)
            {
                if (namePaper == "")
                {
                    return null;
                }
                // надо запустить сервер если он ещё отключен
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

                // находим бумагу

                if (TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                    TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
                {
                    timeFrameBuilder.CandleCreateType = CandleSeriesCreateDataType.MarketDepth;
                }

                if (TypeTesterData == TesterDataType.TickAllCandleState ||
                    TypeTesterData == TesterDataType.TickOnlyReadyCandle)
                {
                    timeFrameBuilder.CandleCreateType = CandleSeriesCreateDataType.Tick;
                }

                CandleSeries series = new CandleSeries(timeFrameBuilder, security);

   // запускаем бумагу на выгрузку

                if (TypeTesterData != TesterDataType.Candle &&
                    timeFrameBuilder.CandleCreateType == CandleSeriesCreateDataType.Tick)
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
                        { // нечем запускать серию
                            return null;
                        }
                    }
                }

                else if (TypeTesterData != TesterDataType.Candle &&
                         timeFrameBuilder.CandleCreateType == CandleSeriesCreateDataType.MarketDepth)
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
                        { // нечем запускать серию
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

                SendLogMessage("Инструмент " + series.Security.Name + "ТаймФрейм " + series.TimeFrame +
               " успешно подключен на получение данных и прослушивание свечек", LogMessageType.System);

                return series;
            }
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
        /// коннекторам подключеным к серверу необходимо перезаказать данные
        /// </summary>
        public event Action NeadToReconnectEvent;

// свечи
        /// <summary>
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
        /// появилась новая свеча
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

// бид и аск

        /// <summary>
        /// обновился бид с аском
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

// стакан

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
        /// обновился стакан
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

// таблица всех сделок

        /// <summary>
        /// все сделки в хранилище
        /// </summary>
        private List<Trade>[] _allTrades;

        /// <summary>
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<Trade>[] AllTrades { get { return _allTrades; } }

        /// <summary>
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
            {// сортируем сделки по хранилищам

                for (int indTrade = 0; indTrade < tradesNew.Count; indTrade++)
                {
                   Trade trade = tradesNew[indTrade];
                   bool isSave = false;
                   for (int i = 0; i < _allTrades.Length; i++)
                   {
                       if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                           _allTrades[i][0].SecurityNameCode == trade.SecurityNameCode)
                       { // если для этого инструметна уже есть хранилище, сохраняем и всё
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
                   { // хранилища для инструмента нет
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
        /// вызывается когда по инструменту приходят новые сделки
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

// мои сделки

        /// <summary>
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
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }

        /// <summary>
        /// вызывается когда приходит новая Моя Сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

// работа по выставлению и снятию моих ордеров

        /// <summary>
        /// выставленные на биржу ордера
        /// </summary>
        private List<Order> OrdersActiv;

        /// <summary>
        /// итератор номеров ордеров на бирже
        /// </summary>
        private int _iteratorNumbersOrders;

        /// <summary>
        /// итератор номеров трэйдов на бирже
        /// </summary>
        private int _iteratorNumbersMyTrades;

        /// <summary>
        /// выставить ордер на биржу
        /// </summary>
        public void ExecuteOrder(Order order)
        {
            if (OrdersActiv.Count != 0)
            {
                for (int i = 0; i < OrdersActiv.Count; i++)
                {
                    if (OrdersActiv[i].NumberUser == order.NumberUser)
                    {
                        SendLogMessage("Ошибка в выставлении ордера. Ордер с таким номером уже есть в системе.", LogMessageType.Error);
                        FailedOperationOrder(order);
                        return;
                    }
                }
            }

            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Ошибка в выставлении ордера. Сервер не активен.", LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            if (order.Price <= 0)
            {
                SendLogMessage("Ошибка в выставлении ордера. Цена заявки находиться за пределами диапазона. order.Price = " + order.Price, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            if (order.Volume <= 0)
            {
                SendLogMessage("Ошибка в выставлении ордера. Неправильный объём. order.Volume = " + order.Volume, LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            if (string.IsNullOrWhiteSpace(order.PortfolioNumber))
            {
                SendLogMessage("Ошибка в выставлении ордера. Не указан номер портфеля", LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            if (string.IsNullOrWhiteSpace(order.SecurityNameCode))
            {
                SendLogMessage("Ошибка в выставлении ордера. Не указан инструмент", LogMessageType.Error);
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
                SecurityTester security = SecuritiesTester.Find(tester => tester.Security.Name == order.SecurityNameCode);
                CheckOrdersInCandleTest(order, security.LastCandle);
            }
        }

        /// <summary>
        /// отозвать ордер с биржи
        /// </summary>
        public void CanselOrder(Order order)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Ошибка в снятии ордера. Сервер не активен.", LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            CanselOnBoardOrder(order);
        }

        /// <summary>
        /// обновился ордер на бирже
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

// внутренние операции "биржи" над ордерами

        /// <summary>
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
                SendLogMessage("Ошибка в снятии ордера. Такого ордера нет в системе", LogMessageType.Error);
                FailedOperationOrder(order);
                return;
            }

            OrdersActiv.Remove(orderToClose);
            orderToClose.State = OrderStateType.Cancel;

            if (NewOrderIncomeEvent != null)
            {
                NewOrderIncomeEvent(orderToClose);
            }
        }

        /// <summary>
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
        /// исполнить ордер на бирже
        /// </summary>
        private void ExecuteOnBoardOrder(Order order,decimal price, DateTime time)
        {
            decimal realPrice = price;

            if(order.Volume == order.VolumeExecute ||
                order.State == OrderStateType.Done)
            {
                return;
            }


            if (Commiss != 0)
            {
                if (order.Side == Side.Buy)
                {
                    Security mySecurity = GetSecurityForName(order.SecurityNameCode);

                    if (mySecurity != null && mySecurity.PriceStep != 0)
                    {
                        realPrice += mySecurity.PriceStep*Commiss;
                    }
                }

                if (order.Side == Side.Sell)
                {
                    Security mySecurity = GetSecurityForName(order.SecurityNameCode);

                    if (mySecurity != null && mySecurity.PriceStep != 0)
                    {
                        realPrice -= mySecurity.PriceStep * Commiss;
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


// работа с логами

        /// <summary>
        /// отправить в лог новый мессадж
        /// </summary>
        void TesterServer_LogMessageEvent(string logMessage)
        {
            SendLogMessage(logMessage,LogMessageType.Error);
        }

        /// <summary>
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
        /// лог менеджер
        /// </summary>
        /// 
        private Log _logMaster;

        /// <summary>
        /// вызывается когда есть новое сообщение в логе
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    /// <summary>
    /// бумага в тестере. 
    /// Инкапсулирует данные для тестирования и методы прогрузки данных
    /// </summary>
    public class SecurityTester
    {
        public SecurityTester()
        {
            ServerMaster.GetServers()[0].NewCandleIncomeEvent += SecurityTester_NewCandleIncomeEvent;
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
        /// бумага которой принадлежит объект
        /// </summary>
        public Security Security;

        /// <summary>
        /// адрес файла с данными инструмента
        /// </summary>
        public string FileAdress;

        /// <summary>
        /// время начала данных в файле
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
        /// время конца данных в файле
        /// </summary>
        public DateTime TimeEnd;

        /// <summary>
        /// Тип данных хранящихся в объекте
        /// </summary>
        public SecurityTesterDataType DataType;

        public TimeSpan TimeFrameSpan;

        public TimeFrame TimeFrame;

// управление выгрузгой данных

        /// <summary>
        /// активирована ли серия для выгрузки
        /// </summary>
        public bool IsActiv;

        /// <summary>
        /// поток управляющий считыванием файла с данными
        /// </summary>
        private StreamReader _reader;

        /// <summary>
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

// разбор файлов тиковых

        /// <summary>
        /// последний трейд инструмента из файла
        /// </summary>
        public Trade LastTrade;

        /// <summary>
        /// последние подгруженные тики за последнюю секунду
        /// </summary>
        public List<Trade> LastTradeSeries; 

        /// <summary>
        /// последняя строка из ридера, участвует в прогрузке
        /// </summary>
        private string _lastString;

        /// <summary>
        /// проверить, не пора ли высылать новую партию тиков
        /// </summary>
        private void CheckTrades(DateTime now)
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

            if (LastTrade != null &&
                LastTrade.Time > now)
            {
                return;
            }

            // качаем первую секунду если 

            if (LastTrade == null)
            {
                _lastString = _reader.ReadLine();
                LastTrade = new Trade();
                LastTrade.SetTradeFromString(_lastString);
                LastTrade.SecurityNameCode = Security.Name;
            }

            while (!_reader.EndOfStream &&
                   LastTrade.Time < now)
            {
                _lastString = _reader.ReadLine();
                LastTrade.SetTradeFromString(_lastString);
            }

            if (LastTrade.Time > now)
            {
                return;
            }

            // здесь имеем первый трейд в текущей секунде

            List<Trade> lastTradesSeries = new List<Trade>();

            Trade trade = new Trade() { SecurityNameCode = Security.Name };
            trade.SetTradeFromString(_lastString);
            lastTradesSeries.Add(trade);

            while (!_reader.EndOfStream)
            {
                _lastString = _reader.ReadLine();
                Trade tradeN = new Trade() { SecurityNameCode = Security.Name };
                tradeN.SetTradeFromString(_lastString);
                
                if (tradeN.Time == now)
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

// разбор свечных файлов

        private Candle _lastCandle;

        /// <summary>
        /// последняя свеча
        /// </summary>
        public Candle LastCandle
        {
            get { return _lastCandle; } 
            set { _lastCandle = value; }
        }

        /// <summary>
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
        /// новые тики появились
        /// </summary>
        public event Action<List<Trade>> NewTradesEvent;

        /// <summary>
        /// новые свечи появились
        /// </summary>
        public event Action<Candle, string,TimeSpan> NewCandleEvent;

        /// <summary>
        /// новые тики появились
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

// разбор стаканов

        /// <summary>
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

            // качаем первую секунду если 

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



// работа с логами

        /// <summary>
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
        /// вызывается когда есть новое сообщение в логе
        /// </summary>
        public event Action<string> LogMessageEvent;

    }

    /// <summary>
    /// тип данных 
    /// </summary>
    public enum TesterSourceDataType
    {

        /// <summary>
        /// Сет данных
        /// </summary>
        Set,

        /// <summary>
        /// папка с данными
        /// </summary>
        Folder
    }

    /// <summary>
    /// режимы тестирования
    /// </summary>
    public enum TesterRegime
    {
        /// <summary>
        /// пауза
        /// </summary>
        Pause,

        /// <summary>
        /// работает
        /// </summary>
        Play,

        /// <summary>
        /// надо прогрузить следующие данные и поставить на паузу
        /// </summary>
        PlusOne
    }

    /// <summary>
    /// тип данных идущих из тестера
    /// </summary>
    public enum TesterDataType
    {
        /// <summary>
        /// свечи
        /// </summary>
        Candle,

        /// <summary>
        /// тики. Все состояния свечей
        /// </summary>
        TickAllCandleState,

        /// <summary>
        /// тики. Только готовые свечи
        /// </summary>
        TickOnlyReadyCandle,

        /// <summary>
        /// стаканы. Все состояния свечей
        /// </summary>
        MarketDepthAllCandleState,

        /// <summary>
        /// стаканы. Только готовые свечи
        /// </summary>
        MarketDepthOnlyReadyCandle,

        /// <summary>
        /// неизвестно
        /// </summary>
        Unknown
    }

    /// <summary>
    /// тип данных хранящийся в тестовой бумаге
    /// </summary>
    public enum SecurityTesterDataType
    {
        /// <summary>
        /// свечи
        /// </summary>
        Candle,

        /// <summary>
        /// тики
        /// </summary>
        Tick,

        /// <summary>
        /// стакан
        /// </summary>
        MarketDepth
    }

    /// <summary>
    /// шаг времени в синхронизаторе. 
    /// </summary>
    public enum TimeAddInTestType
    {
        /// <summary>
        /// минута
        /// </summary>
        Minute,
        /// <summary>
        /// секунда
        /// </summary>
        Second,
        /// <summary>
        /// миллисекунда
        /// </summary>
        MilliSecond
    }
}
