/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Charts;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Finam;

namespace OsEngine.OsData
{
    /// <summary>
    /// сет хранения данных
    /// </summary>
    public class OsDataSet
    {
        // регулируемые настройки

        /// <summary>
        /// включен ли к сохранению 1 секундный ТаймФрейм
        /// </summary>
        public bool Tf1SecondIsOn;

        /// <summary>
        /// включен ли к сохранению 2ух секундный ТаймФрейм
        /// </summary>
        public bool Tf2SecondIsOn;

        /// <summary>
        /// включен ли к сохранению пяти секундный ТаймФрейм
        /// </summary>
        public bool Tf5SecondIsOn;

        /// <summary>
        /// включен ли к сохранению 10 секундный ТаймФрейм
        /// </summary>
        public bool Tf10SecondIsOn;

        /// <summary>
        /// включен ли к сохранению 15 секундный ТаймФрейм
        /// </summary>
        public bool Tf15SecondIsOn;

        /// <summary>
        /// включен ли к сохранению 20 секундный ТаймФрейм
        /// </summary>
        public bool Tf20SecondIsOn;

        /// <summary>
        /// включен ли к сохранению 30 секундный ТаймФрейм
        /// </summary>
        public bool Tf30SecondIsOn;

        /// <summary>
        /// включен ли к сохранению 1 минутный ТаймФрейм
        /// </summary>
        public bool Tf1MinuteIsOn;

        /// <summary>
        /// включен ли к сохранению 2  минутный ТаймФрейм
        /// </summary>
        public bool Tf2MinuteIsOn;

        /// <summary>
        /// включен ли к сохранению 5  минутный ТаймФрейм
        /// </summary>
        public bool Tf5MinuteIsOn;

        /// <summary>
        /// включен ли к сохранению 10 минутный ТаймФрейм
        /// </summary>
        public bool Tf10MinuteIsOn;

        /// <summary>
        /// включен ли к сохранению 15 минутный ТаймФрейм
        /// </summary>
        public bool Tf15MinuteIsOn;

        /// <summary>
        /// включен ли к сохранению 30 минутный ТаймФрейм
        /// </summary>
        public bool Tf30MinuteIsOn;

        /// <summary>
        /// включен ли к сохранению 1 часовой ТаймФрейм
        /// </summary>
        public bool Tf1HourIsOn;

        /// <summary>
        /// включен ли к сохранению 2 часовой ТаймФрейм
        /// </summary>
        public bool Tf2HourIsOn;

        /// <summary>
        /// включен ли к сохранению тиковый ТаймФрейм
        /// </summary>
        public bool TfTickIsOn;

        /// <summary>
        /// включен ли к сохранению стакан
        /// </summary>
        public bool TfMarketDepthIsOn;

        /// <summary>
        /// глубина сохранения стакана
        /// </summary>
        public int MarketDepthDepth;

        /// <summary>
        /// тип создания свечек - из стакана или из тиков
        /// </summary>
        public CandleMarketDataType CandleCreateType;

        /// <summary>
        /// уникальное имя сета
        /// </summary>
        public string SetName;

        /// <summary>
        /// источник сета
        /// </summary>
        public ServerType Source;

        /// <summary>
        /// время старта скачивания
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
        /// время завершения 
        /// </summary>
        public DateTime TimeEnd;

        /// <summary>
        /// имена бумаг на которые мы подписаны
        /// </summary>
        public List<SecurityToLoad> SecuritiesNames;

        /// <summary>
        /// нужно ли обновлять данные автоматически
        /// </summary>
        public bool NeadToUpdate;

        /// <summary>
        /// нужно ли загрузить данные в боевые сервера
        /// </summary>
        public bool NeadToLoadDataInServers;

        // сервис
        /// <summary>
        /// конструктор
        /// </summary>
        public OsDataSet(string nameUniq, System.Windows.Controls.ComboBox comboBoxSecurity,
            System.Windows.Controls.ComboBox comboBoxTimeFrame)
        {
            SetName = nameUniq;
            _regime = DataSetState.Off;
            Tf1SecondIsOn = false;
            Tf2SecondIsOn = false;
            Tf5SecondIsOn = false;
            Tf10SecondIsOn = false;
            Tf15SecondIsOn = false;
            Tf20SecondIsOn = false;
            Tf30SecondIsOn = false;
            Tf1MinuteIsOn = false;
            Tf2MinuteIsOn = false;
            Tf5MinuteIsOn = true;
            Tf10MinuteIsOn = false;
            Tf15MinuteIsOn = false;
            Tf30MinuteIsOn = true;
            Tf1HourIsOn = false;
            Tf2HourIsOn = false;
            TfTickIsOn = false;
            TfMarketDepthIsOn = false;
            Source = ServerType.None;
            SecuritiesNames = new List<SecurityToLoad>();
            TimeStart = DateTime.Now.AddDays(-5);
            TimeEnd = DateTime.Now.AddDays(5);
            MarketDepthDepth = 5;

            Load();

            Thread worker = new Thread(WorkerArea);
            worker.IsBackground = true;
            worker.Start();

            _chartMaster = new ChartMaster(nameUniq,StartProgram.IsOsData);
            _chartMaster.StopPaint();

            _comboBoxSecurity = comboBoxSecurity;
            _comboBoxTimeFrame = comboBoxTimeFrame;
            _comboBoxSecurity.SelectionChanged += _comboBoxSecurity_SelectionChanged;
            _comboBoxTimeFrame.SelectionChanged += _comboBoxTimeFrame_SelectionChanged;
        }

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                if (SetName == "Set_")
                {
                    return;
                }

                if (!Directory.Exists("Data\\" + SetName))
                {
                    Directory.CreateDirectory("Data\\" + SetName);
                }
                using (StreamWriter writer = new StreamWriter("Data\\" + SetName + @"\\Settings.txt", false))
                {
                    writer.WriteLine(_regime);
                    writer.WriteLine(Tf1SecondIsOn);
                    writer.WriteLine(Tf2SecondIsOn);
                    writer.WriteLine(Tf5SecondIsOn);
                    writer.WriteLine(Tf10SecondIsOn);
                    writer.WriteLine(Tf15SecondIsOn);
                    writer.WriteLine(Tf20SecondIsOn);
                    writer.WriteLine(Tf30SecondIsOn);
                    writer.WriteLine(Tf1MinuteIsOn);
                    writer.WriteLine(Tf2MinuteIsOn);
                    writer.WriteLine(Tf5MinuteIsOn);
                    writer.WriteLine(Tf10MinuteIsOn);
                    writer.WriteLine(Tf15MinuteIsOn);
                    writer.WriteLine(Tf30MinuteIsOn);
                    writer.WriteLine(Tf1HourIsOn);
                    writer.WriteLine(Tf2HourIsOn);
                    writer.WriteLine(TfTickIsOn);
                    writer.WriteLine(TfMarketDepthIsOn);
                    writer.WriteLine(Source);

                    writer.WriteLine(TimeStart);
                    writer.WriteLine(TimeEnd);

                    writer.WriteLine(_selectedTf);
                    writer.WriteLine(_selectedSecurity);

                    string securities = "";

                    for (int i = 0; SecuritiesNames != null && i < SecuritiesNames.Count; i++)
                    {
                        securities += SecuritiesNames[i].GetSaveStr() + "@";
                    }

                    writer.WriteLine(securities);

                    writer.WriteLine(CandleCreateType);
                    writer.WriteLine(MarketDepthDepth);
                    writer.WriteLine(NeadToUpdate);
                    writer.WriteLine(NeadToLoadDataInServers);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists("Data\\" + SetName + @"\\Settings.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader("Data\\" + SetName + @"\\Settings.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), out _regime);
                    Tf1SecondIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf2SecondIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf5SecondIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf10SecondIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf15SecondIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf20SecondIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf30SecondIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf1MinuteIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf2MinuteIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf5MinuteIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf10MinuteIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf15MinuteIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf30MinuteIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf1HourIsOn = Convert.ToBoolean(reader.ReadLine());
                    Tf2HourIsOn = Convert.ToBoolean(reader.ReadLine());
                    TfTickIsOn = Convert.ToBoolean(reader.ReadLine());
                    TfMarketDepthIsOn = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out Source);
                    TimeStart = Convert.ToDateTime(reader.ReadLine());
                    TimeEnd = Convert.ToDateTime(reader.ReadLine());

                    Enum.TryParse(reader.ReadLine(), out _selectedTf);
                    _selectedSecurity = reader.ReadLine();

                    string[] securities = reader.ReadLine().Split('@');

                    for (int i = 0; i < securities.Length-1; i++)
                    {
                        SecuritiesNames.Add(new SecurityToLoad());
                        SecuritiesNames[SecuritiesNames.Count - 1].Load(securities[i]);
                    }

                    Enum.TryParse(reader.ReadLine(), out CandleCreateType);
                    MarketDepthDepth = Convert.ToInt32(reader.ReadLine());

                    NeadToUpdate = Convert.ToBoolean(reader.ReadLine());

                    NeadToLoadDataInServers = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
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
            if (File.Exists("Data\\" + SetName + @"\\Settings.txt"))
            {
                File.Delete("Data\\" + SetName + @"\\Settings.txt");
            }
            if (Directory.Exists("Data\\" + SetName))
            {
                try
                {
                    DirectoryInfo info = new DirectoryInfo("Data\\" + SetName);
                    info.Delete(true);
                }
                catch (Exception)
                {
                    // ignore
                }
            }
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public bool ShowDialog()
        {
            OsDataSetUi ui = new OsDataSetUi(this);
            ui.ShowDialog();
            ReBuildComboBox();
            return ui.IsSaved;
        }

        /// <summary>
        /// режим работы 
        /// </summary>
        public DataSetState Regime
        {
            set { _regime = value; }
            get { return _regime; }
        }
        private DataSetState _regime;

        // управление

        /// <summary>
        /// подключить новый инструмент
        /// </summary>
        public void AddNewSecurity()
        {
            if (ServerMaster.GetServers() == null)
            {
                return;
            }

            IServer myServer = ServerMaster.GetServers().Find(server => server.ServerType == Source);

            if (myServer == null)
            {
                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent("Источник не настроен", LogMessageType.System);
                }

                return;
            }

            List<Security> securities = myServer.Securities;

            if (securities == null || securities.Count
                == 0)
            {
                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent("В источнике нет доступных бумаг", LogMessageType.System);
                }
                return;
            }

            NewSecurityUi ui = new NewSecurityUi(securities);
            ui.ShowDialog();

            if (ui.SelectedSecurity != null)
            {
                if (SecuritiesNames == null)
                {
                    SecuritiesNames = new List<SecurityToLoad>();
                }

               SecurityToLoad record = new SecurityToLoad();
                record.Name = ui.SelectedSecurity.Name;
                record.Id = ui.SelectedSecurity.NameId;

                if (SecuritiesNames.Find(s => s.Id == record.Id) == null)
                {
                    SecuritiesNames.Add(record);
                }
            }
            Save();
            ReBuildComboBox();
        }

        /// <summary>
        /// удалить инструмент для скачивания
        /// </summary>
        /// <param name="index">индекс инструмента в массиве</param>
        public void DeleteSecurity(int index)
        {
            if (SecuritiesNames == null ||
                SecuritiesNames.Count <= index)
            {
                return;
            }

            for (int i = 0; _mySeries != null && i < _mySeries.Count; i++)
            {
                if (_mySeries[i].Security.NameId == SecuritiesNames[index].Id)
                {
                    _mySeries[i].Stop();
                    _mySeries.Remove(_mySeries[i]);
                    i--;
                }
            }

            SecuritiesNames.RemoveAt(index);

            Save();
        }

        // сохранение данных

        /// <summary>
        /// серии свечек созданные для скачивания
        /// </summary>
        private List<CandleSeries> _mySeries;

        /// <summary>
        /// выбранный сет
        /// </summary>
        private bool _setIsActive;

        /// <summary>
        /// сервер к которому подключен сет
        /// </summary>
        private IServer _myServer;

        /// <summary>
        /// работа основного потока
        /// </summary>
        private void WorkerArea()
        {
            try
            {
                Thread.Sleep(5000);

                LoadSets();

                Paint();

                while (true)
                {
                    Thread.Sleep(10000);

                    if (_regime == DataSetState.Off &&
                        _setIsActive == false)
                    {
                        // полностью выключены
                        continue;
                    }

                    if (_regime == DataSetState.Off &&
                        _setIsActive == true)
                    {
                        // пользователь запросил отключить скачивание
                        _setIsActive = false;
                        StopSets();
                        continue;
                    }

                    if (_regime == DataSetState.On &&
                        _setIsActive == false)
                    {
                        // пользователь запросил включение
                        StartSets();
                        continue;
                    }



                    if (_regime == DataSetState.On &&
                        _setIsActive == true)
                    {
                        // тут по идее можно сохранять 
                        SaveData();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void LoadSets()
        {
            _candleSaveInfo = new List<CandleSaveInfo>();

            for (int i = 0; i < SecuritiesNames.Count; i++)
            {
                if (Tf1MinuteIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*","") ,TimeFrame.Min1);
                }
                if (Tf2MinuteIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Min2);
                }
                if (Tf5MinuteIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Min5);
                }
                if (Tf10MinuteIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Min10);
                }
                if (Tf15MinuteIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Min15);
                }
                if (Tf30MinuteIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Min30);
                }
                if (Tf1HourIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Hour1);
                }
                if (Tf2HourIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Hour2);
                }

                if (Tf1SecondIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Sec1);
                }
                if (Tf2SecondIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Sec2);
                }
                if (Tf5SecondIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Sec5);
                }
                if (Tf10SecondIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Sec10);
                }
                if (Tf15SecondIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Sec15);
                }
                if (Tf20SecondIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Sec20);
                }
                if (Tf30SecondIsOn)
                {
                    LoadSetsFromFile(SecuritiesNames[i].Name.Replace("*", ""), TimeFrame.Sec30);
                }
            }
        }

        private void LoadSetsFromFile(string securityName, TimeFrame frame)
        {


            string path = "Data\\" + SetName + "\\" + securityName.Replace("/", "") +"\\" + frame;

            if (!Directory.Exists(path))
            {
                return;
            }

            CandleSaveInfo candleSaveInfo = new CandleSaveInfo();
            candleSaveInfo.Frame = frame;
            candleSaveInfo.NameSecurity = securityName;
            _candleSaveInfo.Add(candleSaveInfo);

            List<Candle> candles = new List<Candle>();

            string[] files = Directory.GetFiles(path);

            if (files.Length != 0)
            {
                try
                {
                    using (StreamReader reader = new StreamReader(files[0]))
                    {

                        while (!reader.EndOfStream)
                        {
                            Candle candle = new Candle();
                            candle.SetCandleFromString(reader.ReadLine());
                            candles.Add(candle);
                        }

                    }
                }
                catch (Exception error)
                {
                    if (NewLogMessageEvent != null)
                    {
                        NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                    }
                }
            }
            candleSaveInfo.LoadNewCandles(candles);
            if (candles.Count != 0)
            {
                candleSaveInfo.LastSaveObjectTime = candles[candles.Count - 1].TimeStart;
            }
           
        }

        /// <summary>
        /// создать серии свечек и подписаться на данные
        /// </summary>
        private void StartSets()
        {

            // сначала сервер

            if (_myServer != null)
            {
                _myServer.NewMarketDepthEvent -= _myServer_NewMarketDepthEvent;
            }

            if (ServerMaster.GetServers() == null)
            {
                return;
            }

            _myServer = ServerMaster.GetServers().Find(server => server.ServerType == Source);

            if (_myServer == null || _myServer.ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            _myServer.NewMarketDepthEvent += _myServer_NewMarketDepthEvent;

            // теперь свечи

            if (SecuritiesNames == null ||
                SecuritiesNames.Count == 0)
            {
                // проверяем бумаги
                return;
            }

            if (_mySeries != null &&
                _mySeries.Count != 0)
            {
                // убираем старые серии
                for (int i = 0; i < _mySeries.Count; i++)
                {
                    _mySeries[i].Stop();
                    _myServer.StopThisSecurity(_mySeries[i]);
                }
            }
            _mySeries = new List<CandleSeries>();

            if (Tf1SecondIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Sec1);
                }
            }
            if (Tf2SecondIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Sec2);
                }
            }
            if (Tf5SecondIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Sec5);
                }
            }
            if (Tf10SecondIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Sec10);
                }
            }
            if (Tf15SecondIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Sec15);
                }
            }
            if (Tf20SecondIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Sec20);
                }
            }
            if (Tf30SecondIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Sec30);
                }
            }

            if (Tf1MinuteIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Min1);
                }
            }
            if (Tf2MinuteIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Min2);
                }
            }
            if (Tf5MinuteIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Min5);
                }
            }
            if (Tf10MinuteIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Min10);
                }
            }
            if (Tf15MinuteIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Min15);
                }
            }
            if (Tf30MinuteIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Min30);
                }
            }
            if (Tf1HourIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Hour1);
                }
            }
            if (Tf2HourIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    StartThis(SecuritiesNames[i], TimeFrame.Hour2);
                }
            }
            if (TfTickIsOn && _myServer != null && _myServer.ServerType == ServerType.Finam)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    while (
                        ((FinamServer) _myServer).StartTickToSecurity(SecuritiesNames[i].Id, TimeStart, TimeEnd,
                            GetActualTimeToTrade("Data\\" + SetName + "\\" + SecuritiesNames[i].Name.Replace("/", "") + "\\Tick"), NeadToUpdate) == false)
                    {
                        Thread.Sleep(5000);
                    }
                }
            }

            _setIsActive = true;
        }

        /// <summary>
        /// запустить на скачивание
        /// </summary>
        /// <param name="name">название бумаги</param>
        /// <param name="timeFrame">тайм фрейм</param>
        private void StartThis(SecurityToLoad loadSec, TimeFrame timeFrame)
        {
            CandleSeries series = null;
            while (series == null)
            {
                TimeFrameBuilder timeFrameBuilder = new TimeFrameBuilder();
                timeFrameBuilder.TimeFrame = timeFrame;

                if (_myServer.ServerType == ServerType.Finam)
                {
                    series = ((FinamServer)_myServer).StartThisSecurity(loadSec.Id, timeFrameBuilder, TimeStart,
                        TimeEnd, GetActualTimeToCandle("Data\\" + SetName + "\\" + loadSec.Name.Replace("/", "") + "\\" + timeFrame), NeadToUpdate);
                }
                else
                {
                    series = _myServer.StartThisSecurity(loadSec.Name, timeFrameBuilder);
                }

                Thread.Sleep(10);
            }

            _mySeries.Add(series);
        }

        /// <summary>
        /// остановить скачивание данных
        /// </summary>
        private void StopSets()
        {
            // сначала сервер

            if (_myServer != null)
            {
                _myServer.NewMarketDepthEvent -= _myServer_NewMarketDepthEvent;
            }

            if (_myServer == null)
            {
                for (int i = 0; _mySeries != null && i < _mySeries.Count; i++)
                {
                    _mySeries[i].Stop();
                }
                _mySeries = new List<CandleSeries>();
                return;
            }

            // теперь свечи

            if (_mySeries != null &&
                _mySeries.Count != 0)
            {
                // убираем старые серии
                for (int i = 0; i < _mySeries.Count; i++)
                {
                    _mySeries[i].Stop();
                    _myServer.StopThisSecurity(_mySeries[i]);
                }
            }
            _mySeries = new List<CandleSeries>();
        }

        /// <summary>
        /// сохранить данные
        /// </summary>
        private void SaveData()
        {
            // создаём папки под все инструменты

            // Data\Имя сета\Имя бумаги

            if (!Directory.Exists("Data"))
            {
                Directory.CreateDirectory("Data");
            }

            if (!Directory.Exists("Data\\" + SetName))
            {
                Directory.CreateDirectory("Data\\" + SetName);
            }

            for (int i = 0; i < SecuritiesNames.Count; i++)
            {
                string s = SecuritiesNames[i].Name.Replace("/","");

                if (!Directory.Exists("Data\\" + SetName + "\\" + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "")))
                {
                    Directory.CreateDirectory("Data\\" + SetName + "\\" + SecuritiesNames[i].Name.Replace("/", "").Replace("*",""));
                }
            }

            string pathToSet = "Data\\" + SetName + "\\";

            // свечи

            for (int i = 0; i < _mySeries.Count; i++)
            {
                List<Candle> candles = _mySeries[i].CandlesOnlyReady;

                if (candles == null || candles.Count == 0)
                {
                    continue;
                }
                SaveThisCandles(candles, pathToSet + _mySeries[i].Security.Name.Replace("/", "").Replace("*", "") + "\\" + _mySeries[i].TimeFrame,
                    _mySeries[i].TimeFrame, _mySeries[i].Security.Name);
            }

            Paint();

            // тики
            if (TfTickIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    if (_myServer.ServerType != ServerType.Finam)
                    {
                        List<Trade> trades = _myServer.GetAllTradesToSecurity(_myServer.GetSecurityForName(SecuritiesNames[i].Name));

                        if (trades == null ||
                            trades.Count == 0)
                        {
                            continue;
                        }

                        string path = pathToSet + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "");

                        for (int i2 = 0; i2 < trades.Count; i2++)
                        {

                            SaveThisTick(trades[i2],
                                path, SecuritiesNames[i].Name.Replace("*", ""), null, path + "\\" + "Tick");
                        }
                    }
                    else
                    { // Финам
                        List<string> trades = ((FinamServer)_myServer).GetAllFilesWhithTradeToSecurity(SecuritiesNames[i].Name);

                         SaveThisTickFromFiles(trades,
                            pathToSet + SecuritiesNames[i].Name.Replace("*", "").Replace("/", "") + "\\" + "Tick" + "\\", SecuritiesNames[i].Name.Replace("*", ""));

                    }
                }
            }

            // стаканы

            if (TfMarketDepthIsOn)
            {
                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    SaveThisMarketDepth(
                        pathToSet + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + "\\" + "MarketDepth", SecuritiesNames[i].Name.Replace("*", ""));
                }
            }

            if (NeadToLoadDataInServers)
            {
                if (_lastUpdateTradesInServerTime != DateTime.MinValue &&
                    _lastUpdateTradesInServerTime.AddSeconds(20) > DateTime.Now)
                {
                    return;
                }

                _lastUpdateTradesInServerTime = DateTime.Now;

                if (!Directory.Exists("Data\\QuikServerTrades"))
                {
                    Directory.CreateDirectory("Data\\QuikServerTrades");
                }
                if (!Directory.Exists("Data\\SmartComServerTrades"))
                {
                    Directory.CreateDirectory("Data\\SmartComServerTrades");
                }
                if (!Directory.Exists("Data\\InteractivBrokersServerTrades"))
                {
                    Directory.CreateDirectory("Data\\InteractivBrokersServerTrades");
                }
                if (!Directory.Exists("Data\\AstsBridgeServerTrades"))
                {
                    Directory.CreateDirectory("Data\\AstsBridgeServerTrades");
                }
                if (!Directory.Exists("Data\\PlazaServerTrades"))
                {
                    Directory.CreateDirectory("Data\\PlazaServerTrades");
                }


                for (int i = 0; i < SecuritiesNames.Count; i++)
                {
                    if (
                        !File.Exists(pathToSet + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + "\\" + "Tick" + "\\" +
                                     SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + ".txt"))
                    {
                        continue;
                    }

                    Security sec = _myServer.GetSecurityForName(SecuritiesNames[i].Name);

                    string nameSecurityToSave = sec.NameFull.Replace("'", "").Replace("*", "");

                    if (File.Exists(pathToSet + SecuritiesNames[i].Name.Replace("*", "") + "\\" + "Tick" + "\\" +
                                    nameSecurityToSave + ".txt")
                                    &&
                         File.Exists("Data\\QuikServerTrades\\" + nameSecurityToSave.Replace("*", "") + ".txt"))
                    {
                        FileInfo info = new FileInfo(pathToSet + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + "\\" + "Tick" + "\\" + nameSecurityToSave + ".txt");

                        FileInfo info2 = new FileInfo("Data\\QuikServerTrades\\" + nameSecurityToSave + ".txt");

                        if (info.Length == info2.Length)
                        {
                            continue;
                        }
                    }

                    File.Delete("Data\\QuikServerTrades\\" + nameSecurityToSave.Replace("*", "") + ".txt");
                    File.Delete("Data\\SmartComServerTrades\\" + nameSecurityToSave.Replace("*", "") + ".txt");
                    File.Delete("Data\\InteractivBrokersServerTrades\\" + nameSecurityToSave.Replace("*", "") + ".txt");
                    File.Delete("Data\\AstsBridgeServerTrades\\" + nameSecurityToSave.Replace("*", "") + ".txt");
                    File.Delete("Data\\PlazaServerTrades\\" + nameSecurityToSave.Replace("*", "") + ".txt");

                    File.Copy(pathToSet + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + "\\" + "Tick" + "\\" + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + ".txt",
                        "Data\\QuikServerTrades\\" + nameSecurityToSave + ".txt");
                    File.Copy(pathToSet + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + "\\" + "Tick" + "\\" + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + ".txt",
                        "Data\\SmartComServerTrades\\" + nameSecurityToSave + ".txt");
                    File.Copy(pathToSet + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + "\\" + "Tick" + "\\" + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + ".txt",
                        "Data\\InteractivBrokersServerTrades\\" + nameSecurityToSave + ".txt");
                    File.Copy(pathToSet + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + "\\" + "Tick" + "\\" + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + ".txt",
                        "Data\\AstsBridgeServerTrades\\" + nameSecurityToSave + ".txt");
                    File.Copy(pathToSet + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + "\\" + "Tick" + "\\" + SecuritiesNames[i].Name.Replace("/", "").Replace("*", "") + ".txt",
                        "Data\\PlazaServerTrades\\" + nameSecurityToSave + ".txt");
                }
            }
        }

        private DateTime _lastUpdateTradesInServerTime;


        /// <summary>
        /// взять актуальное время из файла
        /// </summary>
        /// <param name="pathToFile">путь к файлу</param>
        private DateTime GetActualTimeToCandle(string pathToFile)
        {
            if(!Directory.Exists(pathToFile))
            {
                return DateTime.MinValue;
            }
            string[] files = Directory.GetFiles(pathToFile);

            if (files.Length != 0)
            {
                try
                {
                    using (StreamReader reader = new StreamReader(files[0]))
                    {
                        string lastStr = "";

                        while (!reader.EndOfStream)
                        {
                            lastStr = reader.ReadLine();
                        }

                        if (!string.IsNullOrWhiteSpace(lastStr))
                        {
                            Candle candle = new Candle();
                            candle.SetCandleFromString(lastStr);
                            return candle.TimeStart;
                        }
                    }
                }
                catch (Exception error)
                {
                    if (NewLogMessageEvent != null)
                    {
                        NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                    }
                }
            }

            return DateTime.MinValue;
        }

        /// <summary>
        /// взять актуальное время из файла
        /// </summary>
        /// <param name="pathToFile">путь к файлу</param>
        private DateTime GetActualTimeToTrade(string pathToFile)
        {
            if (!Directory.Exists(pathToFile))
            {
                return DateTime.MinValue;
            }
            string[] files = Directory.GetFiles(pathToFile);

            if (files.Length != 0)
            {
                try
                {
                    using (StreamReader reader = new StreamReader(files[0]))
                    {
                        Trade trade = new Trade();
                        string str = "";
                        while (!reader.EndOfStream)
                        {

                            str = reader.ReadLine();

                        }
                        if (str != "")
                        {
                            trade.SetTradeFromString(str);

                            return trade.Time;
                        }

                    }
                }
                catch (Exception error)
                {
                    if (NewLogMessageEvent != null)
                    {
                        NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                    }
                }
            }

            return DateTime.MinValue;
        }

        // свечи

        /// <summary>
        /// сервисная информация для сохранения свечек
        /// </summary>
        private List<CandleSaveInfo> _candleSaveInfo;

        /// <summary>
        /// сохранить новые свечеки по инструменту
        /// </summary>
        /// <param name="candles">свечи</param>
        /// <param name="path">путь</param>
        /// <param name="frame">таймфрейм</param>
        /// <param name="securityName">название инструмента</param>
        private void SaveThisCandles(List<Candle> candles, string path, TimeFrame frame, string securityName)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (_candleSaveInfo == null)
            {
                _candleSaveInfo = new List<CandleSaveInfo>();
            }

            // берём хранилище свечек

            CandleSaveInfo candleSaveInfo =
                _candleSaveInfo.Find(info => info.NameSecurity == securityName && info.Frame == frame);

            if (candleSaveInfo == null)
            {
                LoadSetsFromFile(securityName, frame);
                candleSaveInfo =
                _candleSaveInfo.Find(info => info.NameSecurity == securityName && info.Frame == frame);
            }

            if (candleSaveInfo == null)
            {

                // если сохраняем эти свечи в первый раз, пробуем поднять их из файла
                candleSaveInfo = new CandleSaveInfo();
                candleSaveInfo.Frame = frame;
                candleSaveInfo.NameSecurity = securityName;
                _candleSaveInfo.Add(candleSaveInfo);

                string[] files = Directory.GetFiles(path);

                if (files.Length != 0)
                {
                    try
                    {
                        using (StreamReader reader = new StreamReader(files[0]))
                        {
                            string lastStr = "";

                            while (!reader.EndOfStream)
                            {
                                lastStr = reader.ReadLine();
                            }

                            if (!string.IsNullOrWhiteSpace(lastStr))
                            {
                                Candle candle = new Candle();
                                candle.SetCandleFromString(lastStr);
                                candleSaveInfo.LastSaveObjectTime = candle.TimeStart;
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        if (NewLogMessageEvent != null)
                        {
                            NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                        }
                    }
                }
            }

            // обновляем свечи в хранилище
            candleSaveInfo.LoadNewCandles(candles);

            int firstCandle = 0;

            for (int i = candles.Count - 1; i > -1; i--)
            {
                if (candles[i].TimeStart <= candleSaveInfo.LastSaveObjectTime)
                {
                    firstCandle = i + 1;
                    break;
                }
            }

            // записываем

            try
            {
                using (StreamWriter writer = new StreamWriter(path + "\\" + securityName.Replace("/", "").Replace("*", "") + ".txt", true))
                {
                    for (int i = firstCandle; i < candles.Count; i++)
                    {
                        if (candles[i].TimeStart > TimeEnd)
                        {
                            break;
                        }
                        if (candles[i].TimeStart == candleSaveInfo.LastSaveObjectTime)
                        { // нужно перезаписать последнюю свечку
                            //writer.Write(candles[i].StringToSave, 1);
                        }
                        else
                        {
                            writer.WriteLine(candles[i].StringToSave);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }

            candleSaveInfo.LastSaveObjectTime = candles[candles.Count - 1].TimeStart;
        }

        // тики

        /// <summary>
        /// сервисная информация для сохранения свечек
        /// </summary>
        private List<TradeSaveInfo> _tradeSaveInfo;

        /// <summary>
        /// сохранить серию тиков
        /// </summary>
        /// <param name="tradeLast">тики</param>
        /// <param name="pathToFolder">путь</param>
        /// <param name="securityName">имя бумаги</param>
        private void SaveThisTick(Trade tradeLast, string pathToFolder, string securityName, StreamWriter writer, string pathToFile)
        {
            if (!Directory.Exists(pathToFolder))
            {
                Directory.CreateDirectory(pathToFolder);
            }

            if (_tradeSaveInfo == null)
            {
                _tradeSaveInfo = new List<TradeSaveInfo>();
            }

            // берём хранилище тиков

            TradeSaveInfo tradeSaveInfo =
                _tradeSaveInfo.Find(info => info.NameSecurity == securityName);

            if (tradeSaveInfo == null)
            {
                // если сохраняем эти свечи в первый раз, пробуем поднять их из файла
                tradeSaveInfo = new TradeSaveInfo();
                tradeSaveInfo.NameSecurity = securityName;

                _tradeSaveInfo.Add(tradeSaveInfo);

                string[] files = Directory.GetFiles(pathToFolder);

                if (files.Length != 0 )
                {
                    if (writer != null)
                    {
                        writer.Close();
                        writer = null;
                    }

                    try
                    {
                        using (StreamReader reader = new StreamReader(files[0]))
                        {
                            string str = "";
                            while (!reader.EndOfStream)
                            {

                                str = reader.ReadLine();

                            }
                            if (str != "")
                            {
                                Trade trade = new Trade();
                                trade.SetTradeFromString(str);
                                tradeSaveInfo.LastSaveObjectTime = trade.Time;
                                tradeSaveInfo.LastTradeId = trade.Id;
                            }

                        }
                    }
                    catch (Exception error)
                    {
                        if (NewLogMessageEvent != null)
                        {
                            NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                        }

                        return;
                    }
                }
            }

            if (tradeSaveInfo.LastSaveObjectTime >
                tradeLast.Time ||
                (tradeLast.Id != null && tradeLast.Id == tradeSaveInfo.LastTradeId)
                )
            {
                // если у нас старые тики совпадают с новыми.
                return;
            }


            tradeSaveInfo.LastSaveObjectTime = tradeLast.Time;
            tradeSaveInfo.LastTradeId = tradeLast.Id;
            // записываем

            try
            {
                if (writer != null)
                {
                    writer.WriteLine(tradeLast.GetSaveString());
                }
                else
                {
                    using (
                   StreamWriter writer2 =
                new StreamWriter(pathToFolder + "\\" + securityName.Replace("/", "") + ".txt", true))
                    {
                        writer2.WriteLine(tradeLast.GetSaveString());

                    }
                }
            }
            catch (Exception error)
            {
                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void SaveThisTickFromFiles(List<string> files, string path, string securityName)
        {
            Trade newTrade = new Trade();

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            for (int i = 0; files != null && i < files.Count; i++)
            {
                if (files[i] == null)
                {
                    continue;
                }

                if (_savedTradeFiles.Find(str => str == files[i]) != null &&
                    files.Count - 1 != i)
                {
                    // уже сохранили этот файл
                    continue;
                }

                if (_savedTradeFiles.Find(str => str == files[i]) == null)
                {
                    _savedTradeFiles.Add(files[i]);
                }

                StreamReader reader = new StreamReader(files[i]);

                if ((!reader.EndOfStream))
                {
                    try
                    {
                        newTrade.SetTradeFromString(reader.ReadLine());
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    
                    SaveThisTick(newTrade,
                        path, securityName, null, path + securityName.Replace("/", "") + ".txt");
                }

                using ( StreamWriter writer =
                        new StreamWriter(path + securityName.Replace("/", "") + ".txt", true))
                {
                    while (!reader.EndOfStream)
                    {
                        newTrade.SetTradeFromString(reader.ReadLine());

                        if (newTrade.Time.Hour < 10)
                        {
                            continue;
                        }

                        SaveThisTick(newTrade,
                            path, securityName, writer, path + securityName.Replace("/", "") + ".txt");
                    }
                }
                reader.Close();
            }
        }

        private List<string> _savedTradeFiles = new List<string>(); 

        // стаканы

        /// <summary>
        /// сервисная информация для сохранения стаканов
        /// </summary>
        private List<MarketDepthSaveInfo> _marketDepthSaveInfo;

        /// <summary>
        /// сохранить стаканы по инструменту
        /// </summary>
        /// <param name="path">путь</param>
        /// <param name="securityName">название бумаги</param>
        private void SaveThisMarketDepth(string path, string securityName)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (_tradeSaveInfo == null)
            {
                _tradeSaveInfo = new List<TradeSaveInfo>();
            }
            if (_marketDepthSaveInfo == null)
            {
                return;
            }

            MarketDepthSaveInfo marketDepthSave =
                _marketDepthSaveInfo.Find(info => info.NameSecurity == securityName);

            if (marketDepthSave == null)
            {
                // если сохраняем эти свечи в первый раз, пробуем поднять их из файла
                marketDepthSave = new MarketDepthSaveInfo();
                marketDepthSave.NameSecurity = securityName;
                _marketDepthSaveInfo.Add(marketDepthSave);

                string[] files = Directory.GetFiles(path);

                if (files.Length != 0)
                {
                    try
                    {
                        using (StreamReader reader = new StreamReader(files[0]))
                        {
                            string save = "";

                            while (!reader.EndOfStream)
                            {
                                save = reader.ReadLine();
                            }

                            if (save != "")
                            {
                                MarketDepth marketDepth = new MarketDepth();
                                marketDepth.SetMarketDepthFromString(save);
                                marketDepthSave.LastSaveTime = marketDepth.Time;
                                marketDepthSave.NameSecurity = securityName;
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        if (NewLogMessageEvent != null)
                        {
                            NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                        }
                    }
                }
            }


            int firstCandle = 0;

            List<MarketDepth> depths = marketDepthSave.MarketDepths;

            if (depths == null ||
                depths.Count == 0 ||
                marketDepthSave.LastSaveTime == depths[depths.Count - 1].Time)
            {
                // если у нас старые тики совпадают с новыми.
                return;
            }

            for (int i = depths.Count - 1; i > -1; i--)
            {
                if (depths[i].Time <= marketDepthSave.LastSaveTime)
                {
                    firstCandle = i + 1;
                    break;
                }
            }

            marketDepthSave.LastSaveTime = depths[depths.Count - 1].Time;

            // записываем

            try
            {
                using (StreamWriter writer = new StreamWriter(path + "\\" + securityName.Replace("/", "") + ".txt", true))
                {
                    for (int i = firstCandle; i < depths.Count; i++)
                    {
                        writer.WriteLine(depths[i].GetSaveStringToAllDepfh(MarketDepthDepth));
                    }
                }
            }
            catch (Exception error)
            {
                if (NewLogMessageEvent != null)
                {
                    NewLogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// из сервера пришёл новый стакан
        /// </summary>
        /// <param name="depth"></param>
        private void _myServer_NewMarketDepthEvent(MarketDepth depth)
        {
            try
            {
                if (_marketDepthSaveInfo == null)
                {
                    _marketDepthSaveInfo = new List<MarketDepthSaveInfo>();
                }

                MarketDepthSaveInfo mydDepth = _marketDepthSaveInfo.Find(list => list.NameSecurity == depth.SecurityNameCode);

                if (mydDepth == null)
                {
                    return;
                }

                if (mydDepth.MarketDepths == null)
                {
                    mydDepth.MarketDepths = new List<MarketDepth>();
                }
                mydDepth.MarketDepths.Add(depth);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // прорисовка графика

        /// <summary>
        /// выбранный таймфрейм
        /// </summary>
        private TimeFrame _selectedTf;

        /// <summary>
        /// выбранный инструмент
        /// </summary>
        private string _selectedSecurity;

        /// <summary>
        /// мастер прорисовки чарта
        /// </summary>
        private ChartMaster _chartMaster;

        /// <summary>
        /// меню выбора инструмента
        /// </summary>
        private System.Windows.Controls.ComboBox _comboBoxSecurity;

        /// <summary>
        /// меню выбора таймфрейма
        /// </summary>
        private System.Windows.Controls.ComboBox _comboBoxTimeFrame;

        /// <summary>
        /// выбран ли текущий сет для прорисовки
        /// </summary>
        private bool _isSelected;

        /// <summary>
        /// событие изменения тайфрейма в меню его выбора
        /// </summary>
        void _comboBoxTimeFrame_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (_isSelected == false ||
                   _comboBoxTimeFrame.SelectedItem == null)
                {
                    return;
                }

                Enum.TryParse(_comboBoxTimeFrame.SelectedItem.ToString(), out _selectedTf);
                Paint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// событие изменения бумаги в меню его выбора
        /// </summary>
        void _comboBoxSecurity_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (_isSelected == false ||
                    _comboBoxSecurity.SelectedItem == null)
                {
                    return;
                }

                _selectedSecurity = _comboBoxSecurity.SelectedItem.ToString();
                _chartMaster.Clear();
                Paint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// включить прорисовку этого сета
        /// </summary>
        public void StartPaint(WindowsFormsHost hostChart, Rectangle rectangle)
        {
            try
            {
                if (!_comboBoxTimeFrame.Dispatcher.CheckAccess())
                {
                    _comboBoxTimeFrame.Dispatcher.Invoke(new Action<WindowsFormsHost, Rectangle>(StartPaint), hostChart, rectangle);
                    return;
                }
                _chartMaster.Clear();
                _chartMaster.StartPaint(hostChart, rectangle);

                ReBuildComboBox();

                _isSelected = true;
                
                Paint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// остановить прорисовку этого сета
        /// </summary>
        public void StopPaint()
        {
            _chartMaster.StopPaint();
            _isSelected = false;
        }

        /// <summary>
        /// перестроить меню выбора
        /// </summary>
        private void ReBuildComboBox()
        {
            try
            {
                _comboBoxTimeFrame.SelectionChanged -= _comboBoxTimeFrame_SelectionChanged;
                _comboBoxSecurity.SelectionChanged -= _comboBoxSecurity_SelectionChanged;
                _comboBoxTimeFrame.Items.Clear();

                if (Tf1HourIsOn)
                {
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Hour1;
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Hour1);
                }
                if (Tf2HourIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Hour2);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Hour2;
                }

                if (Tf1MinuteIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Min1);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Min1;
                }
                if (Tf2MinuteIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Min2);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Min2;
                }
                if (Tf5MinuteIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Min5);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Min5;
                }
                if (Tf10MinuteIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Min10);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Min10;
                }
                if (Tf15MinuteIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Min15);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Min15;
                }
                if (Tf30MinuteIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Min30);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Min30;
                }

                if (Tf1SecondIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Sec1);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Sec1;
                }
                if (Tf2SecondIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Sec2);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Sec2;
                }
                if (Tf5SecondIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Sec5);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Sec5;
                }
                if (Tf10SecondIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Sec10);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Sec10;
                }
                if (Tf15SecondIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Sec15);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Sec15;
                }
                if (Tf20SecondIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Sec20);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Sec20;
                }
                if (Tf30SecondIsOn)
                {
                    _comboBoxTimeFrame.Items.Add(TimeFrame.Sec30);
                    _comboBoxTimeFrame.SelectedItem = TimeFrame.Sec30;
                }

                bool haveTf = false;

                for (int i = 0; i < _comboBoxTimeFrame.Items.Count; i++)
                {
                    if (_comboBoxTimeFrame.Items[i].ToString() == _selectedTf.ToString())
                    {
                        haveTf = true;
                        break;
                    }
                }

                if (haveTf == true)
                {
                    _comboBoxTimeFrame.SelectedItem = _selectedTf;
                }
                else if (_comboBoxTimeFrame.Items.Count != 0)
                {
                    Enum.TryParse(_comboBoxTimeFrame.Items[0].ToString(), out _selectedTf);
                    _comboBoxTimeFrame.SelectedItem = _selectedTf;
                }



                _comboBoxSecurity.Items.Clear();

                for (int i = 0; SecuritiesNames != null && i < SecuritiesNames.Count; i++)
                {
                    _comboBoxSecurity.Items.Add(SecuritiesNames[i].Name);
                }

                if (string.IsNullOrWhiteSpace(_selectedSecurity) &&
                    SecuritiesNames != null && SecuritiesNames.Count != 0)
                {
                    _selectedSecurity = SecuritiesNames[0].Name;
                }

                if (!string.IsNullOrWhiteSpace(_selectedSecurity))
                {
                    _comboBoxSecurity.SelectedItem = _selectedSecurity;
                }

                if ((_comboBoxSecurity.SelectedItem == null ||
                    _comboBoxSecurity.SelectedItem.ToString() == "") &&
                    SecuritiesNames != null && SecuritiesNames.Count != 0)
                {
                    _selectedSecurity = SecuritiesNames[0].Name;
                    _comboBoxSecurity.SelectedItem = _selectedSecurity;
                }

                _comboBoxTimeFrame.SelectionChanged += _comboBoxTimeFrame_SelectionChanged;
                _comboBoxSecurity.SelectionChanged += _comboBoxSecurity_SelectionChanged;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// прорисовать выбранный инстрмент и ТФ на графике
        /// </summary>
        private void Paint()
        {
            try
            {
                if (_candleSaveInfo == null)
                {
                    return;
                }

                if (_isSelected == false ||
                    _selectedSecurity == null)
                {
                    return;
                }



                CandleSaveInfo series =
                    _candleSaveInfo.Find(
                        candleSeries =>
                            candleSeries.NameSecurity == _selectedSecurity && candleSeries.Frame == _selectedTf);

                if (series == null ||
                    series.Candles == null)
                {
                    return;
                }
                if ( _selectedTf != TimeFrame.Sec1 &&
                    _selectedTf != TimeFrame.Sec2 &&
                    _selectedTf != TimeFrame.Sec5 &&
                    _selectedTf != TimeFrame.Sec10 &&
                    _selectedTf != TimeFrame.Sec15 &&
                    _selectedTf != TimeFrame.Sec30)
                {
                    _chartMaster.SetCandles(series.Candles);
                }
                
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// сообщения в лог 

        /// <summary>
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (NewLogMessageEvent != null)
            {
                NewLogMessageEvent(message, type);
            }
            else
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// выслать новое сообщение в лог
        /// </summary>
        public event Action<string, LogMessageType> NewLogMessageEvent;

    }

    /// <summary>
    /// статус серии данных
    /// </summary>
    public enum DataSetState
    {
        /// <summary>
        /// вкл
        /// </summary>
        On,
        /// <summary>
        /// выкл
        /// </summary>
        Off
    }

    /// <summary>
    /// информация для сохранения свечек
    /// </summary>
    public class CandleSaveInfo
    {
        /// <summary>
        /// имя бумаги
        /// </summary>
        public string NameSecurity;

        /// <summary>
        /// таймфрейм
        /// </summary>
        public TimeFrame Frame;

        /// <summary>
        /// последнее время сохранения
        /// </summary>
        public DateTime LastSaveObjectTime;

        /// <summary>
        /// свечи инструмента
        /// </summary>
        public List<Candle> Candles;

        /// <summary>
        /// добавить свечи
        /// </summary>
        public void LoadNewCandles(List<Candle> candles)
        {
            if (candles == null || candles.Count == 0)
            {
                return;
            }

            if (Candles == null || Candles.Count == 0)
            {
                Candles = candles;
            }
            else if (Candles[Candles.Count - 1].TimeStart == candles[candles.Count - 1].TimeStart)
            {
                Candles[Candles.Count - 1] = candles[candles.Count - 1];
            }
            else if (Candles[Candles.Count - 1].TimeStart < candles[0].TimeStart)
            {
                Candles.AddRange(candles);
            }
            else if (candles[candles.Count - 1].TimeStart < Candles[0].TimeStart)
            {
                candles.AddRange(Candles);
                Candles = candles;
            }
            else
            {
                int firstIndex = 0;

                for (int i = candles.Count - 1; i > -1; i--)
                {
                    if (Candles[Candles.Count - 1].TimeStart > candles[i].TimeStart)
                    {
                        firstIndex = i+1;
                        break;
                    }
                }

                for (int i = firstIndex; i < candles.Count; i++)
                {
                    if (candles[i].TimeStart == Candles[Candles.Count - 1].TimeStart)
                    {
                        continue;
                    }
                    Candles.Add(candles[i]);
                }
            }
        }
    }

    /// <summary>
    /// информация для сохранения тиков
    /// </summary>
    public class TradeSaveInfo
    {
        /// <summary>
        /// имя бумаги
        /// </summary>
        public string NameSecurity;

        /// <summary>
        /// последнее время сохранения
        /// </summary>
        public DateTime LastSaveObjectTime;

        /// <summary>
        /// последний Id трейда который мы сохранили
        /// </summary>
        public string LastTradeId;

        /// <summary>
        /// последнее сохранённый индекс
        /// </summary>
        public int LastSaveIndex;
    }

    /// <summary>
    /// информация для сохранения стаканов
    /// </summary>
    public class MarketDepthSaveInfo
    {
        /// <summary>
        /// название бумаги
        /// </summary>
        public string NameSecurity;

        /// <summary>
        /// коллекция стаканов по инструменту
        /// </summary>
        public List<MarketDepth> MarketDepths;

        /// <summary>
        /// последнее время сохранения
        /// </summary>
        public DateTime LastSaveTime;
    }

    public class SecurityToLoad
    {
        public string Name;

        public string Id;

        public void Load(string saveStr)
        {
            Name = saveStr.Split('*')[0];
            Id = saveStr.Split('*')[1];
        }

        public string GetSaveStr()
        {
            string result = Name + "*";
            result += Id;

            return result;
        }

    }
}
