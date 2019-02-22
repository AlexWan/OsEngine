﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Market.Servers.Miner
{
    /// <summary>
    /// класс реализующий  предоставление данных для Майнера
    /// </summary>
    public class OsMinerServer
    {
        /// <summary>
        /// конструктор
        /// </summary>
        public OsMinerServer(string name)
        {
            Log = new Log(name + "DataServer",StartProgram.IsOsMiner);
            Log.Listen(this);
            _name = name;

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
                _worker.Name = name + "DataServerThread";
                _worker.Start();
            }

            CheckSet();
        }

        /// <summary>
        /// имя сервера
        /// </summary>
        private string _name;

        /// <summary>
        /// лог
        /// </summary>
        public Log Log;

        /// <summary>
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.Miner; }
        }

        /// <summary>
        /// загрузить настройки из файла
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + _name + "DataServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _name + "DataServer.txt"))
                {
                    _activSet = reader.ReadLine();
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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + "DataServer.txt", false))
                {
                    writer.WriteLine(_activSet);
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
        /// удалить настройки
        /// </summary>
        public void Delete()
        {
            if (File.Exists(@"Engine\" + _name + "DataServer.txt"))
            {
                File.Delete(@"Engine\" + _name + "DataServer.txt");
            }
            _neadToStopThread = true;
        }

// работа с подключением данных

        /// <summary>
        /// тип источника данных
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
        public List<string> Sets
        {
            get { return _sets; }
            private set { _sets = value; }
        }
        private List<string> _sets;

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
                SendLogMessage(OsLocalization.Market.Message25,
                    LogMessageType.System);
            }

            List<string> sets = new List<string>();

            for (int i = 0; i < folders.Length; i++)
            {
                if (folders[i].Split('_').Length == 2)
                {
                    sets.Add(folders[i].Split('_')[1]);
                    SendLogMessage(OsLocalization.Market.Message26 + folders[i].Split('_')[1], LogMessageType.System);
                }
            }

            if (sets.Count == 0)
            {
                SendLogMessage(OsLocalization.Market.Message25,
                    LogMessageType.System);
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

            SendLogMessage(OsLocalization.Market.Message27 + setName, LogMessageType.System);
            _activSet = newSet;

            if (_sourceDataType == TesterSourceDataType.Set)
            {
                ReloadSecurities();
            }
            Save();
        }

        /// <summary>
        /// перезакачать данные
        /// </summary>
        public void ReloadSecurities()
        {
            // чистим все данные, отключаемся
            _securities = null;
            Save();

            _needToReloadSecurities = true;
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
        public string ActiveSet
        {
            get { return _activSet; }
        }
        private string _activSet;

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

// место работы основного потока

        /// <summary>
        /// пора ли перезагружать бумаги в директории
        /// </summary>
        private bool _needToReloadSecurities;

        /// <summary>
        /// основной поток, которые занимается прогрузкой всех данных
        /// </summary>
        private Thread _worker;

        private static object _workerLocker = new object();

        /// <summary>
        /// флаг о том что пора отрубать рабочий поток
        /// </summary>
        private bool _neadToStopThread;

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
                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                  

                    if (_neadToStopThread)
                    {
                        return;
                    }

                    if (_needToReloadSecurities)
                    {
                        lock (_workerLocker)
                        {
                            _needToReloadSecurities = false;
                            LoadSecurities();
                        }
                    }
                    
                    Thread.Sleep(2000);
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        /// <summary>
        /// загрузить данные о бумагах из директории
        /// </summary>
        private void LoadSecurities()
        {
            if ((_sourceDataType == TesterSourceDataType.Set &&
                 (string.IsNullOrWhiteSpace(_activSet) || !Directory.Exists(_activSet))) ||
                (_sourceDataType == TesterSourceDataType.Folder &&
                 (string.IsNullOrWhiteSpace(_pathToFolder) || !Directory.Exists(_pathToFolder))))
            {
                return;
            }

            SecuritiesTester.Clear();

            if (_sourceDataType == TesterSourceDataType.Set)
            {
                // сеты данных Геркулеса
                string[] directories = Directory.GetDirectories(_activSet);

                if (directories.Length == 0)
                {
                    SendLogMessage(OsLocalization.Market.Message28,
                        LogMessageType.System);
                    return;
                }

                for (int i = 0; i < directories.Length; i++)
                {
                    LoadSeciruty(directories[i]);
                }

            }
            else // if (_sourceDataType == TesterSourceDataType.Folder)
            {
                // простые файлы из папки

                string[] files = Directory.GetFiles(_pathToFolder);

                if (files.Length == 0)
                {
                    SendLogMessage(OsLocalization.Market.Message49,
                        LogMessageType.Error);
                }

                LoadCandleFromFolder(_pathToFolder);
            }

            CandleSeriesChangeEvent?.Invoke(SecuritiesTester);

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

                LoadCandleFromFolder(directories[i]);
                
            }
        }

        /// <summary>
        /// загрузить данные из папки
        /// </summary>
        private void LoadCandleFromFolder(string folderName)
        {

            string[] files = Directory.GetFiles(folderName);

            if (files.Length == 0)
            {
                return;
            }

            List<MinerCandleSeries> security = new List<MinerCandleSeries>();

            for (int i = 0; i < files.Length; i++)
            {
                security.Add(new MinerCandleSeries());
                security[security.Count - 1].FileAdress = files[i];
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

                        decimal open = (decimal) Convert.ToDouble(candleN.Open);
                        decimal high = (decimal) Convert.ToDouble(candleN.High);
                        decimal low = (decimal) Convert.ToDouble(candleN.Low);
                        decimal close = (decimal) Convert.ToDouble(candleN.Close);

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


                    // последняя дата
                    string lastString = null;

                    while (!reader.EndOfStream)
                    {
                        lastString = reader.ReadLine();
                    }


                    Candle candle3 = new Candle();
                    candle3.SetCandleFromString(lastString);
                    security[security.Count - 1].TimeEnd = candle3.TimeStart;

                    reader.Close();

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

            for (int i = 0; i < security.Count; i++)
            {
                if (_securities.Find(security1 => security1.Name == security[i].Security.Name) == null)
                {
                    _securities.Add(security[i].Security);
                }
            }

// считаем время
            
            SecuritiesTester.AddRange(security);

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
                    SecuritiesTester[i].LoadCandles();
                }
            }
        }

        /// <summary>
        /// изменились бумаги и данные по серверу
        /// </summary>
        public event Action<List<MinerCandleSeries>> CandleSeriesChangeEvent; 

// бумаги

        /// <summary>
        /// данные хранящиеся в текущий момент в сервере
        /// </summary>
        public List<MinerCandleSeries> SecuritiesTester = new List<MinerCandleSeries>();

        /// <summary>
        /// все бумаги доступные для торгов
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }
        private List<Security> _securities;

        /// <summary>
        /// взять таймфрейм в формате OsEngine
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


// работа с логами

        /// <summary>
        /// отправить в лог новый мессадж
        /// </summary>
        private void TesterServer_LogMessageEvent(string logMessage)
        {
            SendLogMessage(logMessage, LogMessageType.Error);
        }

        /// <summary>
        /// сохранить новую запись в лог
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            LogMessageEvent?.Invoke(message, type);
        }

        /// <summary>
        /// вызывается когда есть новое сообщение в логе
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    /// <summary>
    /// хранилище инструмента и данных по нему
    /// </summary>
    public class MinerCandleSeries
    {
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
        /// таймфрейм в виде интервала  времени TimeSpan
        /// </summary>
        public TimeSpan TimeFrameSpan;

        /// <summary>
        /// таймфрейм в формате OsEngine
        /// </summary>
        public TimeFrame TimeFrame;

        /// <summary>
        /// доступные свечи 
        /// </summary>
        public List<Candle> Candles = new List<Candle>(); 

// разбор свечных файлов

        /// <summary>
        /// загрузка свечек из файла
        /// </summary>
        public void LoadCandles()
        {
            try
            {
                StreamReader reader = new StreamReader(FileAdress);

                Candles = new List<Candle>();

                while (!reader.EndOfStream)
                {
                    Candle candle = new Candle();
                    candle.SetCandleFromString(reader.ReadLine());

                    if (candle.TimeStart < TimeStart ||
                        candle.TimeStart > TimeEnd)
                    {
                        continue;
                    }

                    Candles.Add(candle);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString());
            }
        }


// работа с логами

        /// <summary>
        /// сохранить новую запись в лог
        /// </summary>
        private void SendLogMessage(string message)
        {
            LogMessageEvent?.Invoke(message);
        }

        /// <summary>
        /// вызывается когда есть новое сообщение в логе
        /// </summary>
        public event Action<string> LogMessageEvent;

    }
}

