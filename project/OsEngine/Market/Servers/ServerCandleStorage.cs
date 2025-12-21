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
using OsEngine.Logging;

namespace OsEngine.Market.Servers
{
    public class ServerCandleStorage
    {
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="server"> server for saving candles </param>
        public ServerCandleStorage(AServer server)
        {
            _server = server;

            if (!Directory.Exists(@"Data"))
            {
                Directory.CreateDirectory(@"Data");
            }

            _pathName = @"Data" + @"\" + server.ServerNameUnique + @"Candles";

            Thread saver = new Thread(CandleSaverSpaceInOneFile);
            saver.CurrentCulture = new CultureInfo("RU-ru");
            saver.IsBackground = false;
            saver.Start();
        }

        private AServer _server;

        /// <summary>
        /// directory for saving data
        /// </summary>
        private string _pathName;

        /// <summary>
        /// is the service enabled
        /// </summary>
        public bool NeedToSave;

        /// <summary>
        /// number of candles to be saved to the file system
        /// </summary>
        public int CandlesSaveCount;

        /// <summary>
        /// securities for saving
        /// </summary>
        private List<CandleSeries> _series = new List<CandleSeries>();

        /// <summary>
        /// save security data 
        /// </summary>
        public void SetSeriesToSave(CandleSeries series)
        {
            string spec = series.Specification;

            if(string.IsNullOrEmpty(spec))
            {
                return;
            }

            for (int i = 0; i < _series.Count; i++)
            {
                if(_series[i] == null)
                {
                    continue;
                }
                if (_series[i].Specification == spec)
                {
                    _series.RemoveAt(i);
                    break;
                }
            }

            for (int i = 0; i < _series.Count; i++)
            {
                if (_series[i] == null)
                {
                    continue;
                }
                if (_series[i].UID == series.UID)
                {
                    _series.RemoveAt(i);
                    break;
                }
            }

            _series.Add(series);
        }

        /// <summary>
        /// delete the data series from the save
        /// </summary>
        public void RemoveSeries(CandleSeries series)
        {
            for (int i = 0; i < _series.Count; i++)
            {
                if (_series[i] == null)
                {
                    continue;
                }
                if (_series[i].UID == series.UID)
                {
                    _series.RemoveAt(i);
                    break;
                }
            }
        }

        // saving in file

        /// <summary>
        /// method with candles saving thread
        /// </summary>
        private void CandleSaverSpaceInOneFile()
        {
            if (!Directory.Exists(_pathName))
            {
                Directory.CreateDirectory(_pathName);
            }

            while (true)
            {
                try
                {
                    Thread.Sleep(60000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if(_server.IsDeleted == true)
                    {
                        _server = null;
                        return;
                    }

                    if (NeedToSave == false)
                    {
                        continue;
                    }

                    for (int i = 0; i < _series.Count; i++)
                    {
                        if (_series[i] == null)
                        {
                            continue;
                        }
                        if (MainWindow.ProccesIsWorked == false)
                        {
                            return;
                        }

                        SaveSeries(_series[i]);
                    }
                }
                catch (Exception error)
                {
                    string msg = error.Message;

                    if(msg.Contains("ThrowArgumentOutOfRangeException") == false)
                    {
                        SendNewLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }

        }

        /// <summary>
        /// objects storing information on collections of stored data
        /// </summary>
        private List<CandleSeriesSaveInfo> _candleSeriesSaveInfos = new List<CandleSeriesSaveInfo>();

        /// <summary>
        /// blocker of multithreaded access to specifications of data stored by the object
        /// </summary>
        private object _lockerSpec = new object();

        /// <summary>
        /// request an object that stores information on the data to be saved
        /// </summary>
        public CandleSeriesSaveInfo GetSpecInfo(string specification)
        {
            lock (_lockerSpec)
            {
                CandleSeriesSaveInfo mySaveInfo = _candleSeriesSaveInfos.Find(s => s.Specification == specification);

                if (mySaveInfo == null)
                {
                    mySaveInfo = TryLoadCandle(specification);

                    if (mySaveInfo == null)
                    {
                        mySaveInfo = new CandleSeriesSaveInfo();
                        mySaveInfo.Specification = specification;
                    }

                    _candleSeriesSaveInfos.Add(mySaveInfo);
                }

                return mySaveInfo;
            }
        }

        /// <summary>
        /// save the data series
        /// </summary>
        private void SaveSeries(CandleSeries series)
        {
            CandleSeriesSaveInfo mySaveInfo = GetSpecInfo(series.Specification);

            if (series.CandlesAll == null ||
                series.CandlesAll.Count == 0)
            {
                return;
            }

            Candle firstCandle = series.CandlesAll[0];
            Candle lastCandle = series.CandlesAll[series.CandlesAll.Count - 1];

            if (mySaveInfo.LastCandleTime != DateTime.MinValue
                && mySaveInfo.AllCandlesInFile != null)
            {
                if (firstCandle.TimeStart == mySaveInfo.LastCandleTime &&
                    lastCandle.TimeStart == mySaveInfo.StartCandleTime &&
                    lastCandle.Close == mySaveInfo.LastCandlePrice)
                {
                    return;
                }
            }

            mySaveInfo.InsertCandles(series.CandlesAll, CandlesSaveCount);

            if (Directory.Exists(_pathName) == false)
            {
                Directory.CreateDirectory(_pathName);
            }

            using (StreamWriter writer = new StreamWriter(_pathName + "\\" + series.Specification + ".txt"))
            {
                for (int i = 0; i < mySaveInfo.AllCandlesInFile.Count; i++)
                {
                    writer.WriteLine(mySaveInfo.AllCandlesInFile[i].StringToSave);
                }

                writer.Close();
            }
        }

        /// <summary>
        /// query previously saved security candles
        /// </summary>
        public List<Candle> GetCandles(string specification, int count)
        {
            CandleSeriesSaveInfo mySaveInfo = GetSpecInfo(specification);

            List<Candle> candles = mySaveInfo.AllCandlesInFile;

            if (candles != null &&
                candles.Count != 0 &&
                candles.Count - 1 - count > 0)
            {
                candles = candles.GetRange(candles.Count - 1 - count, count);
            }

            if (candles == null)
            {
                return null;
            }

            List<Candle> newArray = new List<Candle>();

            for (int i = 0; i < candles.Count; i++)
            {
                newArray.Add(candles[i]);
            }

            return newArray;
        }

        /// <summary>
        /// try to load paper candle data from the file system
        /// </summary>
        public CandleSeriesSaveInfo TryLoadCandle(string specification)
        {
            List<Candle> candlesFromServer = new List<Candle>();

            if (File.Exists(_pathName + "\\" + specification + ".txt"))
            {
                try
                {
                    using (StreamReader reader = new StreamReader(_pathName + "\\" + specification + ".txt"))
                    {
                        while (reader.EndOfStream == false)
                        {
                            string str = reader.ReadLine();
                            Candle newCandle = new Candle();
                            newCandle.SetCandleFromString(str);
                            candlesFromServer.Add(newCandle);
                        }

                        reader.Close();
                    }
                }
                catch
                {
                    // ignore
                }
            }

            // далее смотрим есть ли сохранение в глобальном хранилище


            List<Candle> candlesFromOsData = new List<Candle>();

            string path = "Data\\ServersCandleTempData\\" + specification + ".txt";
            if (Directory.Exists("Data\\ServersCandleTempData") &&
                File.Exists(path))
            {
                try
                {
                    using (StreamReader reader = new StreamReader(path))
                    {
                        while (reader.EndOfStream == false)
                        {
                            string str = reader.ReadLine();
                            Candle newCandle = new Candle();
                            newCandle.SetCandleFromString(str);
                            candlesFromOsData.Add(newCandle);
                        }

                        reader.Close();
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (candlesFromOsData.Count == 0 &&
                candlesFromServer.Count == 0)
            {
                return null;
            }

            List<Candle> resultCandles = new List<Candle>();

            if (candlesFromOsData.Count != 0 &&
                candlesFromServer.Count != 0)
            {
                resultCandles = candlesFromServer;
                resultCandles = resultCandles.Merge(candlesFromOsData);
            }
            else if (candlesFromServer.Count != 0)
            {
                resultCandles = candlesFromServer;
            }
            else if (candlesFromOsData.Count != 0)
            {
                resultCandles = candlesFromOsData;
            }

            CandleSeriesSaveInfo myInfo = new CandleSeriesSaveInfo();
            myInfo.Specification = specification;
            myInfo.InsertCandles(resultCandles, CandlesSaveCount);

            return myInfo;
        }

        // log messages сообщения в лог 

        /// <summary>
        /// send a new message to up
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // if nobody is subscribed to us and there is a log error / если на нас никто не подписан и в логе ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing log message
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// information to save candles
    /// </summary>
    public class CandleSeriesSaveInfo
    {
        private int _lastCandleCount;

        private DateTime _lastCandleTime;

        public void InsertCandles(List<Candle> candles, int maxCount)
        {
            if (candles == null)
            {
                return;
            }

            if (AllCandlesInFile == null
                || AllCandlesInFile.Count == 0)
            { 
                // первая прогрузка свечками
                AllCandlesInFile = new List<Candle>();

                for (int i = 0; i < candles.Count; i++)
                {
                    AllCandlesInFile.Add(candles[i]);
                }
            }
            else if(_lastCandleCount == candles.Count &&
                candles[candles.Count-1].TimeStart == _lastCandleTime)
            { 
                // обновилась последняя свеча
                AllCandlesInFile[AllCandlesInFile.Count - 1] = candles[candles.Count - 1];
            }
            else if(candles.Count > 1 
                && _lastCandleCount + 1 == candles.Count
                && candles[candles.Count - 2].TimeStart == _lastCandleTime)
            { 
                // добавилась одна свечка
                AllCandlesInFile.Add(candles[candles.Count - 1]);
            }
            else
            { 
                // добавилось не ясное кол-во свечей
                AllCandlesInFile = AllCandlesInFile.Merge(candles);
            }

            if (AllCandlesInFile.Count == 0)
            {
                return;
            }

            _lastCandleCount = candles.Count;
            _lastCandleTime = candles[candles.Count - 1].TimeStart;

            LastCandleTime = AllCandlesInFile[AllCandlesInFile.Count - 1].TimeStart;
            StartCandleTime = AllCandlesInFile[0].TimeStart;
            LastCandlePrice = AllCandlesInFile[AllCandlesInFile.Count - 1].Close;

            TryTrim(maxCount);
        }

        private void TryTrim(int count)
        {
            if (AllCandlesInFile.Count < count)
            {
                return;
            }

            AllCandlesInFile = AllCandlesInFile.GetRange(AllCandlesInFile.Count - count, count);

            StartCandleTime = AllCandlesInFile[0].TimeStart;
        }

        public List<Candle> AllCandlesInFile;

        public string Specification;

        public DateTime LastCandleTime;

        public DateTime StartCandleTime;

        public decimal LastCandlePrice;
    }
}
