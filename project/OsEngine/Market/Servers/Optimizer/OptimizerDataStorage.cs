﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Market.Servers.Optimizer
{
    /// <summary>
    /// хранилище данных для оптимизатора
    /// </summary>
    public class OptimizerDataStorage
    {

        /// <summary>
        /// конструктор
        /// </summary>
        public OptimizerDataStorage()
        {
            _logMaster = new Log("TesterServer");
            _logMaster.Listen(this);
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
                _worker.Name = "OptimizerStorageThread";
                _worker.Start();
            }

            CheckSet();
        }

        /// <summary>
        /// основной поток, которые занимается прогрузкой всех данных
        /// </summary>
        private Thread _worker;

        /// <summary>
        /// пора ли перезагружать бумаги в директории
        /// </summary>
        private bool _needToReloadSecurities;

        /// <summary>
        /// место работы основного потока
        /// </summary>
        private void WorkThreadArea()
        {
            while (true)
            {
                Thread.Sleep(2000);
                try
                {
                    if (_needToReloadSecurities)
                    {
                        _needToReloadSecurities = false;
                        LoadSecurities();
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
            if (!File.Exists(@"Engine\" + @"OptimizerDataStorage.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"OptimizerDataStorage.txt"))
                {
                    _activSet = reader.ReadLine();
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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"OptimizerDataStorage.txt", false))
                {
                    writer.WriteLine(_activSet);
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

        /// <summary>
        /// источник данных
        /// </summary>
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
            Save();
        }

        /// <summary>
        /// перезагрузить бумаги
        /// </summary>
        public void ReloadSecurities()
        {
            // чистим все данные, отключаемся
            SecuritiesTester = null;
            _needToReloadSecurities = true;
            Save();
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
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            OptimizerDataStorageUi ui = new OptimizerDataStorageUi(this,_logMaster);
            ui.ShowDialog();
        }

        /// <summary>
        /// вызвать окно выбора пути к папке
        /// </summary>
        public void ShowPathSenderDialog()
        {

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

// Загрузка данных по тем бумагам которые есть в хранилище

        /// <summary>
        /// путь к папке с данными. Он же название активного сета
        /// </summary>
        private string _activSet;
        public string ActiveSet
        {
            get { return _activSet; }
        }

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
        /// бумаги доступные для загрузки 
        /// </summary>
        public List<SecurityTester> SecuritiesTester;

        public event Action<DateTime, DateTime> TimeChangeEvent;

        /// <summary>
        /// бумаги доступные для скачивания
        /// </summary>
        public List<Security> Securities; 

        /// <summary>
        /// событие: изменился список доступных бумаг
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        /// <summary>
        /// загрузить данные о бумагах из директории
        /// </summary>
        private void LoadSecurities()
        {
            if (!Directory.Exists(_activSet))
            {
                return;
            }
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

            }
            else if (_sourceDataType == TesterSourceDataType.Folder && !string.IsNullOrWhiteSpace(_pathToFolder))
            { // простые файлы из папки

                string[] files = Directory.GetFiles(_pathToFolder);

                if (files.Length == 0)
                {
                    SendLogMessage("Загрузка бумаг прервана. В указанной папке не содержиться ни одного файла.", LogMessageType.Error);
                }
                if (TypeTesterData == TesterDataType.Candle)
                {
                    LoadCandleFromFolder(_pathToFolder);
                }
                if (TypeTesterData == TesterDataType.Candle)
                {
                    LoadTickFromFolder(_pathToFolder);
                }
                if (TypeTesterData == TesterDataType.Candle)
                {
                    LoadMarketDepthFromFolder(_pathToFolder);
                }
                
            }

            if (SecuritiesChangeEvent != null)
            {
                SecuritiesChangeEvent(Securities);
            }

            if (TimeChangeEvent != null)
            {
                TimeChangeEvent(TimeStart, TimeEnd);
            }
        }

        /// <summary>
        /// выгрузить один инструмент из папки
        /// </summary>
        /// <param name="path">путь к папке с инструментом</param>
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

                if (name == "MarketDepth" && 
                    (_typeTesterData == TesterDataType.MarketDepthAllCandleState ||
                    _typeTesterData == TesterDataType.MarketDepthOnlyReadyCandle))
                {
                    LoadMarketDepthFromFolder(directories[i]);
                }
                else if (name == "Tick" &&
                    (_typeTesterData == TesterDataType.TickAllCandleState ||
                    _typeTesterData == TesterDataType.TickOnlyReadyCandle))
                {
                    LoadTickFromFolder(directories[i]);
                }
                else if(_typeTesterData == TesterDataType.Candle)
                {
                    LoadCandleFromFolder(directories[i]);
                }
            }
        }

        /// <summary>
        /// загрузить бумаги из папки со свечками
        /// </summary>
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

            if (Securities == null)
            {
                Securities = new List<Security>();
            }

            if (SecuritiesTester == null)
            {
                SecuritiesTester = new List<SecurityTester>();
            }

            for (int i = 0; i < security.Count; i++)
            {
                if (Securities.Find(security1 => security1.Name == security[i].Security.Name) == null)
                {
                    Securities.Add(security[i].Security);
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
                Security secu = Securities.Find(s => s.Name == array[i][0]);

                if (secu != null)
                {
                    secu.Lot = Convert.ToDecimal(array[i][1]);
                    secu.Go = Convert.ToDecimal(array[i][2]);
                    secu.PriceStepCost = Convert.ToDecimal(array[i][3]);
                    secu.PriceStep = Convert.ToDecimal(array[i][4]);

                    if (SecuritiesTester[SecuritiesTester.Count - 1].Security.Name == secu.Name)
                    {
                        SecuritiesTester[SecuritiesTester.Count - 1].Security.Lot = Convert.ToDecimal(array[i][1]);
                        SecuritiesTester[SecuritiesTester.Count - 1].Security.Go = Convert.ToDecimal(array[i][2]);
                        SecuritiesTester[SecuritiesTester.Count - 1].Security.PriceStepCost = Convert.ToDecimal(array[i][3]);
                        SecuritiesTester[SecuritiesTester.Count - 1].Security.PriceStep = Convert.ToDecimal(array[i][4]);

                    }
                }
            }
        }

        /// <summary>
        /// загрузить бумаги из папки с трейдами
        /// </summary>
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
                security[security.Count - 1].FileAdress = files[i];

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

            if (Securities == null)
            {
                Securities = new List<Security>();
            }

            if (SecuritiesTester == null)
            {
                SecuritiesTester = new List<SecurityTester>();
            }

            for (int i = 0; i < security.Count; i++)
            {
                if (Securities.Find(security1 => security1.Name == security[i].Security.Name) == null)
                {
                    Securities.Add(security[i].Security);
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
                Security secu = Securities.Find(s => s.Name == array[i][0]);

                if (secu != null)
                {
                    secu.Lot = Convert.ToDecimal(array[i][1]);
                    secu.Go = Convert.ToDecimal(array[i][2]);
                    secu.PriceStepCost = Convert.ToDecimal(array[i][3]);
                    secu.PriceStep = Convert.ToDecimal(array[i][4]);
                }
            }
        }

        /// <summary>
        /// загрузить бумаги из папки со стаканами
        /// </summary>
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

            if (Securities == null)
            {
                Securities = new List<Security>();
            }

            if (SecuritiesTester == null)
            {
                SecuritiesTester = new List<SecurityTester>();
            }

            for (int i = 0; i < security.Count; i++)
            {
                if (Securities.Find(security1 => security1.Name == security[i].Security.Name) == null)
                {
                    Securities.Add(security[i].Security);
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
                Security secu = Securities.Find(s => s.Name == array[i][0]);

                if (secu != null)
                {
                    secu.Lot = Convert.ToDecimal(array[i][1]);
                    secu.Go = Convert.ToDecimal(array[i][2]);
                    secu.PriceStepCost = Convert.ToDecimal(array[i][3]);
                    secu.PriceStep = Convert.ToDecimal(array[i][4]);
                }
            }
        }

        /// <summary>
        /// взять таймфрейм из TimeSpan
        /// </summary>
        private TimeFrame GetTimeFrame(TimeSpan frameSpan)
        {
            TimeFrame timeFrame = TimeFrame.Min1;

            if (frameSpan == new TimeSpan(0, 0, 0, 1))
            {
                timeFrame = TimeFrame.Sec1;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 2))
            {
                timeFrame = TimeFrame.Sec2;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 5))
            {
                timeFrame = TimeFrame.Sec5;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 10))
            {
                timeFrame = TimeFrame.Sec10;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 15))
            {
                timeFrame = TimeFrame.Sec15;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 20))
            {
                timeFrame = TimeFrame.Sec20;
            }
            else if (frameSpan == new TimeSpan(0, 0, 0, 30))
            {
                timeFrame = TimeFrame.Sec30;
            }
            else if (frameSpan == new TimeSpan(0, 0, 1, 0))
            {
                timeFrame = TimeFrame.Min1;
            }
            else if (frameSpan == new TimeSpan(0, 0, 2, 0))
            {
                timeFrame = TimeFrame.Min2;
            }
            else if (frameSpan == new TimeSpan(0, 0, 5, 0))
            {
                timeFrame = TimeFrame.Min5;
            }
            else if (frameSpan == new TimeSpan(0, 0, 10, 0))
            {
                timeFrame = TimeFrame.Min10;
            }
            else if (frameSpan == new TimeSpan(0, 0, 15, 0))
            {
                timeFrame = TimeFrame.Min15;
            }
            else if (frameSpan == new TimeSpan(0, 0, 20, 0))
            {
                timeFrame = TimeFrame.Min20;
            }
            else if (frameSpan == new TimeSpan(0, 0, 30, 0))
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
            else if (frameSpan == new TimeSpan(24, 0, 0, 0))
            {
                timeFrame = TimeFrame.Day;
            }

            return timeFrame;
        }

// хранение дополнительных данных о бумагах: ГО, Мультипликаторы, Лоты

        /// <summary>
        /// загрузить спецификацию по бумагам
        /// </summary>
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

        /// <summary>
        /// сохранить спецификацию по бумагам
        /// </summary>
        /// <param name="securityToSave"></param>
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
        }


// загрузка данных из файлов

        /// <summary>
        /// все хранилища данных
        /// </summary>
        private List<DataStorage> _storages = new List<DataStorage>();

        private object _storageLocker = new object();

        /// <summary>
        /// взять хранилище с данными по бумаге
        /// </summary>
        /// <returns></returns>
        public DataStorage GetStorageToSecurity(Security security, TimeFrame timeFrame, DateTime timeStart, DateTime timeEnd)
        {
            lock (_storageLocker)
            {
                if (_typeTesterData == TesterDataType.Candle)
                {
                    DataStorage storage =
                                   _storages.Find(s => s.Security.Name == security.Name && s.TimeFrame == timeFrame &&
                                                              s.TimeStart == timeStart && s.TimeEnd == timeEnd && s.StorageType == TesterDataType.Candle);

                    if (storage != null)
                    {
                        return storage;
                    }
                    storage = LoadCandlesFromFolder(security, timeFrame, timeStart, timeEnd);

                    if (storage == null)
                    {
                        SendLogMessage("В хранилище не найдены соответствующие данные. Бумага: " + security.Name + " Тип данных: " + _typeTesterData, LogMessageType.Error);
                        return null;
                    }

                    _storages.Add(storage);
                    return storage;
                }
                if (_typeTesterData == TesterDataType.TickOnlyReadyCandle)
                {
                    DataStorage storage =
                                   _storages.Find(s => s.Security.Name == security.Name &&
                                                              s.TimeStart == timeStart && s.TimeEnd == timeEnd && s.StorageType == TesterDataType.TickOnlyReadyCandle);

                    if (storage != null)
                    {
                        return storage;
                    }
                    storage = LoadTradesFromFolder(security, timeStart, timeEnd);

                    if (storage == null)
                    {
                        SendLogMessage("В хранилище не найдены соответствующие данные. Бумага: " + security.Name + " Тип данных: " + _typeTesterData, LogMessageType.Error);
                        return null;
                    }

                    _storages.Add(storage);
                    return storage;
                }
                if (_typeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
                {
                    DataStorage storage =
                                   _storages.Find(s => s.Security.Name == security.Name &&
                                                              s.TimeStart == timeStart && s.TimeEnd == timeEnd && s.StorageType == TesterDataType.MarketDepthOnlyReadyCandle);

                    if (storage != null)
                    {
                        return storage;
                    }
                    storage = LoadMarketDepthFromFolder(security, timeStart, timeEnd);

                    if (storage == null)
                    {
                        SendLogMessage("В хранилище не найдены соответствующие данные. Бумага: " + security.Name + " Тип данных: " + _typeTesterData, LogMessageType.Error);
                        return null;
                    }

                    _storages.Add(storage);
                    return storage;
                }

                SendLogMessage("В хранилище не найдены соответствующие данные. Бумага: " + security.Name + " Тип данных: " + _typeTesterData, LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// загрузить свечи из файла
        /// </summary>
        private DataStorage LoadCandlesFromFolder(Security security, TimeFrame timeFrame, DateTime timeStart,
            DateTime timeEnd)
        {
            SecurityTester sec =
                SecuritiesTester.Find(
                    s =>
                        s.Security.Name == security.Name && s.TimeFrame == timeFrame &&
                        s.DataType == SecurityTesterDataType.Candle);

            if (sec == null)
            {
                return null;
            }

            StreamReader reader = new StreamReader(sec.FileAdress);
            List<Candle> candles = new List<Candle>();

            while (!reader.EndOfStream)
            {
                Candle candle = new Candle();
                try
                {
                    candle.SetCandleFromString(reader.ReadLine());
                    candle.State = CandleStates.Finished;
                }
                catch
                {
                    SendLogMessage("Ошибка в формате данных внутри файла с данными " + sec.FileAdress, LogMessageType.Error);
                    break;
                }
                if (candle.TimeStart < timeStart)
                {
                    continue;
                }
                else if (candle.TimeStart > timeEnd)
                {
                    break;
                }
                candles.Add(candle);
            }

            if (candles.Count == 0)
            {
                SendLogMessage("За период не найдено ни одной свечи. Начало периода: " + timeStart.ToShortDateString() + 
                    " конец периода: " + timeEnd.ToShortDateString() + " Бумага: " + security.Name, LogMessageType.Error);
            }

            DataStorage storage = new DataStorage();
            storage.Candles = candles;
            storage.Security = security;
            storage.TimeEnd = timeEnd;
            storage.TimeStart = timeStart;
            storage.StorageType = TesterDataType.Candle;

            return storage;
        }

        /// <summary>
        /// загрузить трейды из файла
        /// </summary>
        private DataStorage LoadTradesFromFolder(Security security, DateTime timeStart,
    DateTime timeEnd)
        {
            SecurityTester sec = SecuritiesTester.Find(s => s.Security.Name == security.Name && s.DataType == SecurityTesterDataType.Tick);

            if (sec == null)
            {
                return null;
            }

            StreamReader reader = new StreamReader(sec.FileAdress);
            List<Trade> trades = new List<Trade>();

            while (!reader.EndOfStream)
            {
                Trade trade = new Trade();
                try
                {
                    trade.SetTradeFromString(reader.ReadLine());
                    trade.SecurityNameCode = security.Name;
                }
                catch
                {
                    SendLogMessage("Ошибка в формате данных внутри файла с данными " + sec.FileAdress, LogMessageType.Error);
                    break;
                }
                if (trade.Time < timeStart)
                {
                    continue;
                }
                else if (trade.Time > timeEnd)
                {
                    break;
                }
                trades.Add(trade);
            }

            if (trades.Count == 0)
            {
                SendLogMessage("За период не найдено ни одного трейда. Начало периода: " + timeStart.ToShortDateString() +
                    " конец периода: " + timeEnd.ToShortDateString() + " Бумага: " + security.Name, LogMessageType.Error);
            }

            DataStorage storage = new DataStorage();
            storage.Trades = trades;
            storage.Security = security;
            storage.TimeEnd = timeEnd;
            storage.TimeStart = timeStart;
            storage.StorageType = TesterDataType.TickOnlyReadyCandle;

            return storage;
        }

        /// <summary>
        /// загрузить стаканы из файла
        /// </summary>
        private DataStorage LoadMarketDepthFromFolder(Security security, DateTime timeStart,
DateTime timeEnd)
        {
            SecurityTester sec = SecuritiesTester.Find(s => s.Security.Name == security.Name && s.DataType == SecurityTesterDataType.Tick);

            if (sec == null)
            {
                return null;
            }

            StreamReader reader = new StreamReader(sec.FileAdress);
            List<MarketDepth> marketDepths = new List<MarketDepth>();

            while (!reader.EndOfStream)
            {
                MarketDepth depth = new MarketDepth();
                try
                {
                    depth.SetMarketDepthFromString(reader.ReadLine());
                    depth.SecurityNameCode = sec.Security.Name;
                }
                catch
                {
                    SendLogMessage("Ошибка в формате данных внутри файла с данными " + sec.FileAdress, LogMessageType.Error);
                    break;
                }
                if (depth.Time < timeStart)
                {
                    continue;
                }
                else if (depth.Time > timeEnd)
                {
                    break;
                }
                marketDepths.Add(depth);
            }

            if (marketDepths.Count == 0)
            {
                SendLogMessage("За период не найдено ни одного трейда. Начало периода: " + timeStart.ToShortDateString() +
                    " конец периода: " + timeEnd.ToShortDateString() + " Бумага: " + security.Name, LogMessageType.Error);
            }

            DataStorage storage = new DataStorage();
            storage.MaketDepths = marketDepths;
            storage.Security = security;
            storage.TimeEnd = timeEnd;
            storage.TimeStart = timeStart;
            storage.StorageType = TesterDataType.MarketDepthOnlyReadyCandle;

            return storage;
        }


// логирование
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
    /// загруженные свечки
    /// </summary>
    public class DataStorage
    {
        /// <summary>
        /// что хранит в себе хранилище
        /// </summary>
        public TesterDataType StorageType;

        /// <summary>
        /// бумага по которой свечи загружены
        /// </summary>
        public Security Security;

        /// <summary>
        /// свечи этой бумаги
        /// </summary>
        public List<Candle> Candles;

        /// <summary>
        /// трейды этой бумаги
        /// </summary>
        public List<Trade> Trades;

        /// <summary>
        /// стаканы этой бумаги
        /// </summary>
        public List<MarketDepth> MaketDepths; 

        /// <summary>
        /// таймФрейм свечек
        /// </summary>
        public TimeFrame TimeFrame;

        /// <summary>
        /// время старта скачивания этой серии свечек
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
        /// время завершения скачивания этой серии свечек
        /// </summary>
        public DateTime TimeEnd;
    }

}
