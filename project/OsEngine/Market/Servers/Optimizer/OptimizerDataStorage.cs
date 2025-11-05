/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
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
using OsEngine.OsOptimizer;

namespace OsEngine.Market.Servers.Optimizer
{
    public class OptimizerDataStorage
    {
        #region Service and base settings

        public OptimizerDataStorage(string name, bool needToCreateThread)
        {
            _name = name;

            _logMaster = new Log("OptimizerServer", StartProgram.IsTester);
            _logMaster.Listen(this)
                ;
            TypeTesterData = TesterDataType.Candle;
            Load();

            if (_activeSet != null)
            {
                _needToReloadSecurities = true;
            }

            if (needToCreateThread == true)
            {
                _worker = new Thread(WorkThreadArea);
                _worker.CurrentCulture = new CultureInfo("ru-RU");
                _worker.IsBackground = true;
                _worker.Name = "OptimizerStorageThread";
                _worker.Start();
            }

            CheckSet();
        }

        private string _name;

        public void ClearDelete()
        {
            _isDeleted = true;
            _tradesId = 0;

            if (_storages != null)
            {
                for (int i = 0; i < _storages.Count; i++)
                {
                    _storages[i].ClearDelete();
                }

                _storages.Clear();
                _storages = null;
            }
            if (SecuritiesTester != null)
            {
                for (int i = 0; i < SecuritiesTester.Count; i++)
                {
                    SecuritiesTester[i].Clear();
                }
                SecuritiesTester.Clear();
                SecuritiesTester = null;
            }
        }

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
                ReloadSecurities(false);
            }
        }
        private TesterDataType _typeTesterData;

        private void Load()
        {
            if (!File.Exists(@"Engine\" + _name + @"OptimizerDataStorage.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"OptimizerDataStorage.txt"))
                {
                    _activeSet = reader.ReadLine();
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

        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"OptimizerDataStorage.txt", false))
                {
                    writer.WriteLine(_activeSet);
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
                ReloadSecurities(false);
            }
        }
        private TesterSourceDataType _sourceDataType;

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
                string pathCurrent = folders[i];

                if (pathCurrent.Contains("Set_") == false)
                {
                    continue;
                }

                if (pathCurrent.Split('_').Length == 2)
                {
                    string setName = pathCurrent.Split('_')[1];

                    sets.Add(setName);
                    SendLogMessage(OsLocalization.Market.Label244 + ": " + setName, LogMessageType.System);
                }
            }

            if (sets.Count == 0)
            {
                SendLogMessage(OsLocalization.Market.Message25, LogMessageType.System);
            }

            Sets = sets;
        }

        public void SetNewSet(string setName)
        {
            string newSet = @"Data" + @"\" + @"Set_" + setName;
            if (newSet == _activeSet)
            {
                return;
            }

            SendLogMessage(OsLocalization.Market.Message27 + setName, LogMessageType.System);
            _activeSet = newSet;

            if (_sourceDataType == TesterSourceDataType.Set)
            {
                ReloadSecurities(false);
            }
            Save();
        }

        public string PathToFolder
        {
            get { return _pathToFolder; }
            set { _pathToFolder = value; }
        }
        private string _pathToFolder;

        public void ShowDialog(OptimizerMaster master)
        {
            OptimizerDataStorageUi ui = new OptimizerDataStorageUi(this, _logMaster, master);
            ui.ShowDialog();
        }

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
                    ReloadSecurities(false);
                }
            }
        }

        #endregion

        #region Main thread work place

        private void WorkThreadArea()
        {
            while (true)
            {
                try
                {
                    if (_isDeleted == true)
                    {
                        return;
                    }
                    if (_needToReloadSecurities)
                    {
                        _needToReloadSecurities = false;
                        LoadSecurities();
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(1000);
                }

                Thread.Sleep(50);
            }
        }

        public void ReloadSecurities(bool thisThread)
        {
            try
            {
                // чистим все данные, отключаемся
                SecuritiesTester = null;

                if (thisThread == false)
                {
                    _needToReloadSecurities = true;
                    Save();
                }
                else
                {
                    LoadSecurities();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private bool _isDeleted;

        private Thread _worker;

        private bool _needToReloadSecurities;

        #endregion

        #region Get securities data from file system

        public string ActiveSet
        {
            get { return _activeSet; }
        }
        private string _activeSet;

        public DateTime TimeMin;

        public DateTime TimeMax;

        public DateTime TimeStart;

        public DateTime TimeEnd;

        public DateTime TimeNow;

        public List<SecurityTester> SecuritiesTester;

        public event Action<DateTime, DateTime> TimeChangeEvent;

        public List<Security> Securities;

        public event Action<List<Security>> SecuritiesChangeEvent;

        private void LoadSecurities()
        {
            try
            {
                Securities = new List<Security>();

                TimeMax = DateTime.MinValue;
                TimeEnd = DateTime.MaxValue;
                TimeMin = DateTime.MaxValue;
                TimeStart = DateTime.MinValue;
                TimeNow = DateTime.MinValue;

                if (_sourceDataType == TesterSourceDataType.Set && !string.IsNullOrWhiteSpace(_activeSet))
                {
                    if (!Directory.Exists(_activeSet))
                    {
                        return;
                    }
                    string[] directories = Directory.GetDirectories(_activeSet);

                    if (directories.Length == 0)
                    {
                        SendLogMessage(OsLocalization.Market.Message28, LogMessageType.System);
                        return;
                    }

                    for (int i = 0; i < directories.Length; i++)
                    {
                        LoadSingleSecurity(directories[i]);
                    }

                }
                else if (_sourceDataType == TesterSourceDataType.Folder && !string.IsNullOrWhiteSpace(_pathToFolder))
                { // simple files from folder / простые файлы из папки

                    string[] files = Directory.GetFiles(_pathToFolder);

                    if (files.Length == 0)
                    {
                        SendLogMessage(OsLocalization.Market.Message28, LogMessageType.Error);
                    }
                    if (TypeTesterData == TesterDataType.Candle)
                    {
                        LoadCandleFromFolder(_pathToFolder);
                    }
                    if (TypeTesterData == TesterDataType.TickAllCandleState ||
                        TypeTesterData == TesterDataType.TickOnlyReadyCandle)
                    {
                        LoadTickFromFolder(_pathToFolder);
                    }
                    if (TypeTesterData == TesterDataType.MarketDepthAllCandleState ||
                        TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle)
                    {
                        LoadMarketDepthFromFolder(_pathToFolder);
                    }

                }

                LoadSetSecuritiesTimeFrameSettings();

                if (SecuritiesChangeEvent != null)
                {
                    SecuritiesChangeEvent(Securities);
                }

                if (TimeChangeEvent != null)
                {
                    TimeChangeEvent(TimeStart, TimeEnd);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void LoadSingleSecurity(string path)
        {
            try
            {
                string[] directories = Directory.GetDirectories(path);

                if (directories.Length == 0)
                {
                    return;
                }

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
                    else if (_typeTesterData == TesterDataType.Candle)
                    {
                        LoadCandleFromFolder(directories[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void LoadCandleFromFolder(string folderName)
        {
            lock (_lockerLoadCandles)
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
                    security[security.Count - 1].FileAddress = files[i];

                    string name = files[i].Split('\\')[files[i].Split('\\').Length - 1];

                    security[security.Count - 1].Security = new Security();
                    security[security.Count - 1].Security.Name = name;
                    security[security.Count - 1].Security.Lot = 1;
                    security[security.Count - 1].Security.NameClass = "TestClass";
                    security[security.Count - 1].Security.MarginBuy = 1;
                    security[security.Count - 1].Security.MarginSell = 1;
                    security[security.Count - 1].Security.PriceStepCost = 1;
                    security[security.Count - 1].Security.PriceStep = 1;
                    // timeframe / тф
                    // price step / шаг цены
                    // begin / начало
                    // end / конец

                    StreamReader reader = new StreamReader(files[i]);

                    // candles / свечи: 20110111,100000,19577.00000,19655.00000,19533.00000,19585.00000,2752
                    // ticks ver.1/тики 1 вар: 20150401,100000,86160.000000000,2
                    // ticks ver.2/тики 2 вар: 20151006,040529,3010,5,Buy/Sell/Unknown

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
                        // price step / шаг цены

                        decimal minPriceStep = decimal.MaxValue;
                        int countFive = 0;

                        //CultureInfo culture = new CultureInfo("ru-RU");
                        CultureInfo culture = CultureInfo.InvariantCulture;

                        for (int i2 = 0; i2 < 20; i2++)
                        {
                            if (reader.EndOfStream == true)
                            {
                                reader.Close();
                                reader = new StreamReader(files[i]);
                                if (reader.EndOfStream == true)
                                {
                                    break;
                                }

                                continue;
                            }

                            Candle candleN = new Candle();
                            candleN.SetCandleFromString(reader.ReadLine());

                            decimal open = (decimal)Convert.ToDouble(candleN.Open);
                            decimal high = (decimal)Convert.ToDouble(candleN.High);
                            decimal low = (decimal)Convert.ToDouble(candleN.Low);
                            decimal close = (decimal)Convert.ToDouble(candleN.Close);

                            if (open.ToString(culture).Split('.').Length > 1 ||
                                high.ToString(culture).Split('.').Length > 1 ||
                                low.ToString(culture).Split('.').Length > 1 ||
                                close.ToString(culture).Split('.').Length > 1)
                            {
                                // if the real part takes place / если имеет место вещественная часть
                                int length = 1;

                                if (open.ToString(culture).Split('.').Length > 1 &&
                                    open.ToString(culture).Split('.')[1].Length > length)
                                {
                                    length = open.ToString(culture).Split('.')[1].Length;
                                }

                                if (high.ToString(culture).Split('.').Length > 1 &&
                                    high.ToString(culture).Split('.')[1].Length > length)
                                {
                                    length = high.ToString(culture).Split('.')[1].Length;
                                }

                                if (low.ToString(culture).Split('.').Length > 1 &&
                                    low.ToString(culture).Split('.')[1].Length > length)
                                {
                                    length = low.ToString(culture).Split('.')[1].Length;
                                }

                                if (close.ToString(culture).Split('.').Length > 1 &&
                                    close.ToString(culture).Split('.')[1].Length > length)
                                {
                                    length = close.ToString(culture).Split('.')[1].Length;
                                }

                                if (length == 1 && minPriceStep > 0.1m)
                                {
                                    minPriceStep = 0.1m;
                                }
                                if (length == 2 && minPriceStep > 0.01m)
                                {
                                    minPriceStep = 0.01m;
                                }
                                if (length == 3 && minPriceStep > 0.001m)
                                {
                                    minPriceStep = 0.001m;
                                }
                                if (length == 4 && minPriceStep > 0.0001m)
                                {
                                    minPriceStep = 0.0001m;
                                }
                                if (length == 5 && minPriceStep > 0.00001m)
                                {
                                    minPriceStep = 0.00001m;
                                }
                                if (length == 6 && minPriceStep > 0.000001m)
                                {
                                    minPriceStep = 0.000001m;
                                }
                                if (length == 7 && minPriceStep > 0.0000001m)
                                {
                                    minPriceStep = 0.0000001m;
                                }
                                if (length == 8 && minPriceStep > 0.00000001m)
                                {
                                    minPriceStep = 0.00000001m;
                                }
                                if (length == 9 && minPriceStep > 0.000000001m)
                                {
                                    minPriceStep = 0.000000001m;
                                }
                                if (length == 10 && minPriceStep > 0.0000000001m)
                                {
                                    minPriceStep = 0.0000000001m;
                                }
                            }
                            else
                            {
                                // if the real part doesn't take place / если вещественной части нет
                                int length = 1;

                                for (int i3 = open.ToString(culture).Length - 1; open.ToString(culture)[i3] == '0'; i3--)
                                {
                                    length = length * 10;
                                }

                                int lengthLow = 1;

                                for (int i3 = low.ToString(culture).Length - 1; low.ToString(culture)[i3] == '0'; i3--)
                                {
                                    lengthLow = lengthLow * 10;

                                    if (length > lengthLow)
                                    {
                                        length = lengthLow;
                                    }
                                }

                                int lengthHigh = 1;

                                for (int i3 = high.ToString(culture).Length - 1; high.ToString(culture)[i3] == '0'; i3--)
                                {
                                    lengthHigh = lengthHigh * 10;

                                    if (length > lengthHigh)
                                    {
                                        length = lengthHigh;
                                    }
                                }

                                int lengthClose = 1;

                                for (int i3 = close.ToString(culture).Length - 1; close.ToString(culture)[i3] == '0'; i3--)
                                {
                                    lengthClose = lengthClose * 10;

                                    if (length > lengthClose)
                                    {
                                        length = lengthClose;
                                    }
                                }
                                if (minPriceStep > length)
                                {
                                    minPriceStep = length;
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


                        // last date / последняя дата
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

                // save securities / сохраняем бумаги

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

                // count the time / считаем время

                if (SecuritiesTester.Count != 0)
                {
                    for (int i = 0; i < SecuritiesTester.Count; i++)
                    {
                        if ((TimeMin == DateTime.MinValue && SecuritiesTester[i].TimeStart != DateTime.MinValue)
                            ||
                            (SecuritiesTester[i].TimeStart != DateTime.MinValue && SecuritiesTester[i].TimeStart < TimeMin))
                        {
                            TimeMin = SecuritiesTester[i].TimeStart;
                            TimeStart = SecuritiesTester[i].TimeStart;
                            TimeNow = SecuritiesTester[i].TimeStart;
                        }
                        if (SecuritiesTester[i].TimeEnd != DateTime.MinValue
                            &&
                            SecuritiesTester[i].TimeEnd > TimeMax)
                        {
                            TimeMax = SecuritiesTester[i].TimeEnd;
                            TimeEnd = SecuritiesTester[i].TimeEnd;
                        }
                    }
                }

                // check in tester file data on presence of multipliers and GO for securities
                // проверяем в файле тестера данные о наличии мультипликаторов и ГО для бумаг

                SetToSecuritiesDopSettings(folderName);
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
                security[security.Count - 1].FileAddress = files[i];

                string name = files[i].Split('\\')[files[i].Split('\\').Length - 1];

                security[security.Count - 1].Security = new Security();
                security[security.Count - 1].Security.Name = name;
                security[security.Count - 1].Security.Lot = 1;
                security[security.Count - 1].Security.NameClass = "TestClass";
                security[security.Count - 1].Security.MarginBuy = 1;
                security[security.Count - 1].Security.MarginSell = 1;
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
                    // checks whether ticks are in the file / смотрим тики ли в файле
                    Trade trade = new Trade();
                    trade.SetTradeFromString(str);
                    // ticks are in the file / в файле тики

                    security[security.Count - 1].TimeStart = trade.Time;
                    security[security.Count - 1].DataType = SecurityTesterDataType.Tick;

                    // price step / шаг цены

                    decimal minPriceStep = decimal.MaxValue;
                    int countFive = 0;

                    CultureInfo culture = CultureInfo.InvariantCulture;

                    for (int i2 = 0; i2 < 100; i2++)
                    {
                        Trade tradeN = new Trade();
                        tradeN.SetTradeFromString(reader.ReadLine());

                        decimal open = (decimal)Convert.ToDouble(tradeN.Price);


                        if (open.ToString(culture).Split('.').Length > 1)
                        {
                            // if the real part takes place / если имеет место вещественная часть
                            int length = 1;

                            if (open.ToString(culture).Split('.').Length > 1 &&
                                open.ToString(culture).Split('.')[1].Length > length)
                            {
                                length = open.ToString(culture).Split('.')[1].Length;
                            }


                            if (length == 1 && minPriceStep > 0.1m)
                            {
                                minPriceStep = 0.1m;
                            }
                            if (length == 2 && minPriceStep > 0.01m)
                            {
                                minPriceStep = 0.01m;
                            }
                            if (length == 3 && minPriceStep > 0.001m)
                            {
                                minPriceStep = 0.001m;
                            }
                            if (length == 4 && minPriceStep > 0.0001m)
                            {
                                minPriceStep = 0.0001m;
                            }
                            if (length == 5 && minPriceStep > 0.00001m)
                            {
                                minPriceStep = 0.00001m;
                            }
                            if (length == 6 && minPriceStep > 0.000001m)
                            {
                                minPriceStep = 0.000001m;
                            }
                            if (length == 7 && minPriceStep > 0.0000001m)
                            {
                                minPriceStep = 0.0000001m;
                            }
                            if (length == 8 && minPriceStep > 0.00000001m)
                            {
                                minPriceStep = 0.00000001m;
                            }
                            if (length == 9 && minPriceStep > 0.000000001m)
                            {
                                minPriceStep = 0.000000001m;
                            }
                            if (length == 10 && minPriceStep > 0.0000000001m)
                            {
                                minPriceStep = 0.0000000001m;
                            }
                        }
                        else
                        {
                            // if the real part doesn't take place / если вещественной части нет
                            int length = 1;

                            for (int i3 = open.ToString(culture).Length - 1; open.ToString(culture)[i3] == '0'; i3--)
                            {
                                length = length * 10;
                            }

                            if (minPriceStep > length)
                            {
                                minPriceStep = length;
                            }

                            if (length == 1 &&
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

                    // last date / последняя дата
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

            SetToSecuritiesDopSettings(folderName);
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
                security[security.Count - 1].FileAddress = files[i];

                string name = files[i].Split('\\')[files[i].Split('\\').Length - 1];

                security[security.Count - 1].Security = new Security();
                security[security.Count - 1].Security.Name = name;
                security[security.Count - 1].Security.Lot = 1;
                security[security.Count - 1].Security.NameClass = "TestClass";
                security[security.Count - 1].Security.MarginBuy = 1;
                security[security.Count - 1].Security.MarginSell = 1;
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
                    // check whether depths are in the file / смотрим стакан ли в файле

                    MarketDepth trade = new MarketDepth();
                    trade.SetMarketDepthFromString(str);

                    // depths are in the file / в файле стаканы

                    security[security.Count - 1].TimeStart = trade.Time;
                    security[security.Count - 1].DataType = SecurityTesterDataType.MarketDepth;

                    // price step / шаг цены

                    decimal minPriceStep = decimal.MaxValue;
                    int countFive = 0;

                    CultureInfo culture = CultureInfo.InvariantCulture;

                    for (int i2 = 0; i2 < 20; i2++)
                    {
                        MarketDepth tradeN = new MarketDepth();
                        tradeN.SetMarketDepthFromString(reader.ReadLine());

                        decimal open = (decimal)Convert.ToDouble(tradeN.Bids[0].Price);

                        if (open == 0)
                        {
                            open = (decimal)Convert.ToDouble(tradeN.Asks[0].Price);
                        }

                        if (open.ToString(culture).Split('.').Length > 1)
                        {
                            // if the real part takes place / если имеет место вещественная часть
                            int length = 1;

                            if (open.ToString(culture).Split('.').Length > 1 &&
                                open.ToString(culture).Split('.')[1].Length > length)
                            {
                                length = open.ToString(culture).Split('.')[1].Length;
                            }


                            if (length == 1 && minPriceStep > 0.1m)
                            {
                                minPriceStep = 0.1m;
                            }
                            if (length == 2 && minPriceStep > 0.01m)
                            {
                                minPriceStep = 0.01m;
                            }
                            if (length == 3 && minPriceStep > 0.001m)
                            {
                                minPriceStep = 0.001m;
                            }
                            if (length == 4 && minPriceStep > 0.0001m)
                            {
                                minPriceStep = 0.0001m;
                            }
                            if (length == 5 && minPriceStep > 0.00001m)
                            {
                                minPriceStep = 0.00001m;
                            }
                            if (length == 6 && minPriceStep > 0.000001m)
                            {
                                minPriceStep = 0.000001m;
                            }
                            if (length == 7 && minPriceStep > 0.0000001m)
                            {
                                minPriceStep = 0.0000001m;
                            }
                            if (length == 8 && minPriceStep > 0.00000001m)
                            {
                                minPriceStep = 0.00000001m;
                            }
                            if (length == 9 && minPriceStep > 0.000000001m)
                            {
                                minPriceStep = 0.000000001m;
                            }
                            if (length == 10 && minPriceStep > 0.0000000001m)
                            {
                                minPriceStep = 0.0000000001m;
                            }
                        }
                        else
                        {
                            // if the real part doesn't take place / если вещественной части нет
                            int length = 1;

                            for (int i3 = open.ToString(culture).Length - 1; open.ToString(culture)[i3] == '0'; i3--)
                            {
                                length = length * 10;
                            }

                            if (minPriceStep > length)
                            {
                                minPriceStep = length;
                            }

                            if (length == 1 &&
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

            // save securities / сохраняем бумаги

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

            SetToSecuritiesDopSettings(folderName);

        }

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
            else if (frameSpan == new TimeSpan(0, 4, 0, 0))
            {
                timeFrame = TimeFrame.Hour4;
            }
            else if (frameSpan == new TimeSpan(1, 0, 0, 0))
            {
                timeFrame = TimeFrame.Day;
            }

            return timeFrame;
        }

        private TimeSpan GetTimeSpan(StreamReader reader)
        {

            Candle lastCandle = null;

            TimeSpan lastTimeSpan = TimeSpan.MaxValue;

            int counter = 0;

            while (true)
            {
                if (reader.EndOfStream)
                {
                    if (lastTimeSpan != TimeSpan.MaxValue)
                    {
                        return lastTimeSpan;
                    }
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

        public void SaveSetSecuritiesTimeFrameSettings()
        {
            try
            {
                string fileName = @"Engine\OptimizerServerSecuritiesTf"
                    + _sourceDataType.ToString()
                    + TypeTesterData.ToString();

                if (_sourceDataType == TesterSourceDataType.Set)
                {
                    if (string.IsNullOrEmpty(_activeSet))
                    {
                        return;
                    }
                    fileName += _activeSet.RemoveExcessFromSecurityName();
                }
                else if (_sourceDataType == TesterSourceDataType.Folder)
                {
                    if (string.IsNullOrEmpty(_pathToFolder))
                    {
                        return;
                    }
                    fileName += _pathToFolder.RemoveExcessFromSecurityName();
                }

                fileName += ".txt";

                using (StreamWriter writer = new StreamWriter(fileName, false))
                {
                    for (int i = 0; i < SecuritiesTester.Count; i++)
                    {
                        writer.WriteLine(SecuritiesTester[i].Security.Name + "#" + SecuritiesTester[i].TimeFrame);
                    }

                    writer.Close();
                }
            }
            catch
            {
                // ignored
            }
        }

        private void LoadSetSecuritiesTimeFrameSettings()
        {
            string fileName = @"Engine\OptimizerServerSecuritiesTf"
                  + _sourceDataType.ToString()
                  + TypeTesterData.ToString();

            if (_sourceDataType == TesterSourceDataType.Set)
            {
                if (string.IsNullOrEmpty(_activeSet))
                {
                    return;
                }
                fileName += _activeSet.RemoveExcessFromSecurityName();
            }
            else if (_sourceDataType == TesterSourceDataType.Folder)
            {
                if (string.IsNullOrEmpty(_pathToFolder))
                {
                    return;
                }
                fileName += _pathToFolder.RemoveExcessFromSecurityName();
            }

            fileName += ".txt";

            if (!File.Exists(fileName))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(fileName))
                {
                    for (int i = 0; i < SecuritiesTester.Count; i++)
                    {
                        if (reader.EndOfStream == true)
                        {
                            return;
                        }

                        string[] security = reader.ReadLine().Split('#');

                        if (SecuritiesTester[i].Security.Name != security[0])
                        {
                            return;
                        }

                        TimeFrame frame;

                        if (Enum.TryParse(security[1], out frame))
                        {
                            SecuritiesTester[i].TimeFrame = frame;
                        }
                    }

                    reader.Close();
                }
            }
            catch
            {
                // ignored
            }

        }

        #endregion

        #region Storage of additional security data: GO, Multipliers, Lots

        private void SetToSecuritiesDopSettings(string folderName)
        {
            List<string[]> array = LoadSecurityDopSettings(folderName + "\\SecuritiesSettings.txt");

            if (array == null)
            {
                array = LoadSecurityDopSettings(_activeSet + "\\SecuritiesSettings.txt");
            }

            for (int i = 0; array != null && i < array.Count; i++)
            {
                List<Security> secuAll = Securities.FindAll(s => s.Name == array[i][0]);

                if (secuAll != null && secuAll.Count != 0)
                {
                    for (int i2 = 0; i2 < secuAll.Count; i2++)
                    {
                        Security secu = secuAll[i2];

                        decimal lot = array[i][1].ToDecimal();
                        decimal go = array[i][2].ToDecimal();
                        decimal priceStepCost = array[i][3].ToDecimal();
                        decimal priceStep = array[i][4].ToDecimal();

                        int volDecimals = 0;
                        decimal goSell = 0;

                        if (array[i].Length > 5)
                        {
                            volDecimals = Convert.ToInt32(array[i][5]);
                        }
                        if (array[i].Length > 6)
                        {
                            goSell = Convert.ToInt32(array[i][6]);
                        }

                        if (lot != 0)
                        {
                            secu.Lot = lot;
                        }
                            
                        if (go != 0)
                        {
                            secu.MarginBuy = go;
                        }
                           
                        if (priceStepCost != 0)
                        {
                            secu.PriceStepCost = priceStepCost;
                        }
                           
                        if (priceStep != 0)
                        {
                            secu.PriceStep = priceStep;
                        }
                        
                        secu.DecimalsVolume = volDecimals;
                        secu.MarginSell = goSell;
                    }
                }
            }

            for(int i = 0;i < Securities.Count;i++)
            {
                Security etalonSecurity = Securities[i];

                for(int j = 0;j < SecuritiesTester.Count;j++)
                {
                    Security currentSecurity = SecuritiesTester[j].Security;
                    if (currentSecurity.Name == etalonSecurity.Name
                        && currentSecurity.NameClass == etalonSecurity.NameClass)
                    {
                        currentSecurity.LoadFromString(etalonSecurity.GetSaveStr());
                    }
                }
            }

        }

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
                // send to log / отправить в лог
            }
            return null;
        }

        public void SaveSecurityDopSettings(Security securityToSave)
        {
            if (SecuritiesTester.Count == 0)
            {
                return;
            }

            for (int i = 0; i < Securities.Count; i++)
            {
                if (Securities[i].Name == securityToSave.Name)
                {
                    Securities[i].LoadFromString(securityToSave.GetSaveStr());
                }
            }

            for (int i = 0; i < SecuritiesTester.Count; i++)
            {
                if (SecuritiesTester[i].Security.Name == securityToSave.Name)
                {
                    SecuritiesTester[i].Security.LoadFromString(securityToSave.GetSaveStr());
                }
            }

            string pathToSettings = "";

            if (SourceDataType == TesterSourceDataType.Set)
            {
                if (string.IsNullOrWhiteSpace(_activeSet))
                {
                    return;
                }
                pathToSettings = _activeSet + "\\SecuritiesSettings.txt";
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

            CultureInfo culture = CultureInfo.InvariantCulture;

            for (int i = 0; i < saves.Count; i++)
            { // delete the same / удаляем совпадающие

                if (saves[i][0] == securityToSave.Name)
                {
                    saves.RemoveAt(i);
                    i--;
                }
            }

            if (saves.Count == 0)
            {
                saves.Add(new[]
                {
                    securityToSave.Name,
                    securityToSave.Lot.ToString(culture),
                    securityToSave.MarginBuy.ToString(culture),
                    securityToSave.PriceStepCost.ToString(culture),
                    securityToSave.PriceStep.ToString(culture),
                    securityToSave.DecimalsVolume.ToString(culture),
                    securityToSave.MarginSell.ToString(culture)
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
                    securityToSave.MarginBuy.ToString(culture),
                    securityToSave.PriceStepCost.ToString(culture),
                    securityToSave.PriceStep.ToString(culture),
                    securityToSave.DecimalsVolume.ToString(culture),
                    securityToSave.MarginSell.ToString(culture)
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
                            saves[i][4] + "$" +
                            saves[i][5] + "$" +
                            saves[i][6]
                            );
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // send to the log / отправить в лог
            }
        }

        #endregion

        #region Download data from files

        private List<DataStorage> _storages = new List<DataStorage>();

        private object _storageLocker = new object();

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
                        return null;
                    }

                    storage.TimeFrame = timeFrame;

                    if (storage == null)
                    {
                        SendLogMessage(OsLocalization.Market.Message29 + security.Name + OsLocalization.Market.Message30 + _typeTesterData, LogMessageType.Error);
                        return null;
                    }

                    _storages.Add(storage);
                    return storage;


                }
                if (_typeTesterData == TesterDataType.TickOnlyReadyCandle)
                {
                    DataStorage storage =
                                   _storages.Find(s => s.Security.Name == security.Name 
                                   && s.TimeStart == timeStart 
                                   && s.TimeEnd == timeEnd 
                                   && s.StorageType == TesterDataType.TickOnlyReadyCandle);

                    if (storage != null)
                    {
                        return storage;
                    }
                    storage = LoadTradesFromFolder(security, timeStart, timeEnd);
                    storage.StorageType = TesterDataType.TickOnlyReadyCandle;

                    if (storage == null)
                    {
                        SendLogMessage(OsLocalization.Market.Message29 + security.Name + OsLocalization.Market.Message30 + _typeTesterData, LogMessageType.Error);
                        return null;
                    }

                    _storages.Add(storage);
                    return storage;
                }
                if (_typeTesterData == TesterDataType.TickAllCandleState)
                {
                    DataStorage storage =
                                   _storages.Find(s => s.Security.Name == security.Name 
                                   && s.TimeStart == timeStart 
                                   && s.TimeEnd == timeEnd 
                                   && s.StorageType == TesterDataType.TickAllCandleState);

                    if (storage != null)
                    {
                        return storage;
                    }

                    storage = LoadTradesFromFolder(security, timeStart, timeEnd);

                    storage.StorageType = TesterDataType.TickAllCandleState;

                    if (storage == null)
                    {
                        SendLogMessage(OsLocalization.Market.Message29 + security.Name + OsLocalization.Market.Message30 + _typeTesterData, LogMessageType.Error);
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
                    storage.StorageType = TesterDataType.MarketDepthOnlyReadyCandle;

                    if (storage == null)
                    {
                        SendLogMessage(OsLocalization.Market.Message29 + security.Name + OsLocalization.Market.Message30 + _typeTesterData, LogMessageType.Error);
                        return null;
                    }

                    _storages.Add(storage);
                    return storage;
                }

                SendLogMessage(OsLocalization.Market.Message29 + security.Name + OsLocalization.Market.Message30 + _typeTesterData, LogMessageType.Error);
                return null;
            }
        }

        private object _lockerLoadCandles = "candlesLocker";

        private long _tradesId;

        private DataStorage LoadCandlesFromFolder(Security security, TimeFrame timeFrame, DateTime timeStart,
            DateTime timeEnd)
        {
            try
            {
                lock (_lockerLoadCandles)
                {

                    SecurityTester sec =
                    SecuritiesTester.Find(
                        s =>
                            s != null &&
                            s.Security.Name == security.Name && s.TimeFrame == timeFrame &&
                            s.DataType == SecurityTesterDataType.Candle);

                    if (sec == null)
                    {
                        return null;
                    }

                    StreamReader reader = new StreamReader(sec.FileAddress);
                    List<Candle> candles = new List<Candle>();

                    while (!reader.EndOfStream)
                    {
                        Candle candle = new Candle();
                        try
                        {
                            candle.SetCandleFromString(reader.ReadLine());
                            candle.State = CandleState.Finished;
                        }
                        catch
                        {
                            //SendLogMessage(OsLocalization.Market.Message31 + sec.FileAdress, LogMessageType.Error);
                            continue;
                        }
                        if (candle.TimeStart < timeStart)
                        {
                            continue;
                        }
                        else if (candle.TimeStart > timeEnd.AddDays(1))
                        {
                            break;
                        }
                        candles.Add(candle);
                    }

                    if (candles.Count == 0)
                    {
                        SendLogMessage(OsLocalization.Market.Message32 + timeStart.ToShortDateString() +
                                       OsLocalization.Market.Message33 + timeEnd.ToShortDateString() +
                                       OsLocalization.Market.Message14 + security.Name, LogMessageType.Error);
                    }

                    DataStorage storage = new DataStorage();
                    storage.Candles = candles;
                    storage.Security = security;
                    storage.TimeEnd = timeEnd;
                    storage.TimeStart = timeStart;
                    storage.StorageType = TesterDataType.Candle;

                    return storage;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private DataStorage LoadTradesFromFolder(Security security, DateTime timeStart, DateTime timeEnd)
        {
            try
            {

                SecurityTester sec = SecuritiesTester.Find(s => s.Security.Name == security.Name && s.DataType == SecurityTesterDataType.Tick);

                if (sec == null)
                {
                    return null;
                }

                StreamReader reader = new StreamReader(sec.FileAddress);
                List<Trade> trades = new List<Trade>();

                while (!reader.EndOfStream)
                {
                    Trade trade = new Trade();
                    try
                    {
                        trade.SetTradeFromString(reader.ReadLine());
                        trade.IdInTester = _tradesId++;
                        trade.SecurityNameCode = security.Name;
                    }
                    catch
                    {
                        SendLogMessage(OsLocalization.Market.Message31 + sec.FileAddress, LogMessageType.Error);
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
                    SendLogMessage(OsLocalization.Market.Message34 + timeStart.ToShortDateString() +
                                   OsLocalization.Market.Message33 + timeEnd.ToShortDateString() + OsLocalization.Market.Message14 + security.Name, LogMessageType.Error);
                }

                DataStorage storage = new DataStorage();
                storage.Trades = trades;
                storage.Security = security;
                storage.TimeEnd = timeEnd;
                storage.TimeStart = timeStart;
                storage.StorageType = TesterDataType.TickOnlyReadyCandle;

                return storage;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private DataStorage LoadMarketDepthFromFolder(Security security, DateTime timeStart, DateTime timeEnd)
        {
            try
            {

                SecurityTester sec = SecuritiesTester.Find(s => s.Security.Name == security.Name && s.DataType == SecurityTesterDataType.Tick);

                if (sec == null)
                {
                    return null;
                }

                StreamReader reader = new StreamReader(sec.FileAddress);
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
                        SendLogMessage(OsLocalization.Market.Message31 + sec.FileAddress, LogMessageType.Error);
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
                    SendLogMessage(OsLocalization.Market.Message34 + timeStart.ToShortDateString() +
                                   OsLocalization.Market.Message33 + timeEnd.ToShortDateString() + " Бумага: " + security.Name, LogMessageType.Error);
                }

                DataStorage storage = new DataStorage();
                storage.MarketDepths = marketDepths;
                storage.Security = security;
                storage.TimeEnd = timeEnd;
                storage.TimeStart = timeStart;
                storage.StorageType = TesterDataType.MarketDepthOnlyReadyCandle;

                return storage;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
            return null;
        }

        #endregion

        #region Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        private Log _logMaster;

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public class DataStorage
    {
        public TesterDataType StorageType;

        public Security Security;

        public List<Candle> Candles;

        public List<Trade> Trades;

        public List<MarketDepth> MarketDepths;

        public TimeFrame TimeFrame;

        public DateTime TimeStart;

        public DateTime TimeEnd;

        public DateTime TimeEndAddDay
        {
            get
            {
                if (_timeEndAddDay == DateTime.MinValue)
                {
                    _timeEndAddDay = TimeEnd.AddDays(1);
                }
                return _timeEndAddDay;
            }
        }

        private DateTime _timeEndAddDay;

        public void ClearDelete()
        {
            Security = null;

            if (Candles != null)
            {
                Candles.Clear();
                Candles = null;
            }

            if (Trades != null)
            {
                Trades.Clear();
                Trades = null;
            }

            if (MarketDepths != null)
            {
                MarketDepths.Clear();
                MarketDepths = null;
            }

        }
    }
}