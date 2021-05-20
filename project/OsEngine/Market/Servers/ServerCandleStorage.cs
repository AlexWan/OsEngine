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
using OsEngine.OsData;

namespace OsEngine.Market.Servers
{
    public class ServerCandleStorage
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="server"> server for saving ticks / сервер с которого будем сохранять тики </param>
        public ServerCandleStorage(IServer server)
        {
            _server = server;

            if (!Directory.Exists(@"Data"))
            {
                Directory.CreateDirectory(@"Data");
            }

            _pathName = @"Data" + @"\" + server.GetType().Name + @"Candles";

            Thread saver = new Thread(CandleSaverSpaceInOneFile);
            saver.CurrentCulture = new CultureInfo("RU-ru");
            saver.IsBackground = false;
            saver.Start();
        }

        private IServer _server;

        /// <summary>
        /// directory for saving data
        /// название папки для хранения данных
        /// </summary>
        private string _pathName;

        public bool NeadToSave;

        /// <summary>
        /// securities for saving
        /// инструменты которые нужно сохранять
        /// </summary>
        private List<CandleSeries> _series = new List<CandleSeries>();

        /// <summary>
        /// save security data 
        /// сохранять данные по бумаге
        /// </summary>
        public void SetSeriesToSave(CandleSeries series)
        {
            for (int i = 0; i < _series.Count; i++)
            {
                if (_series[i].Specification == series.Specification)
                {
                    _series.RemoveAt(i);
                    break;
                }
            }

            _series.Add(series);
        }

        // for saving in one file
        // для сохранения в один файл

        /// <summary>
        /// method with tick saving thread
        /// метод в котором работает поток сохраняющий тики
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

                    if (NeadToSave == false)
                    {
                        continue;
                    }

                    for (int i = 0; i < _series.Count; i++)
                    {
                        if (MainWindow.ProccesIsWorked == false)
                        {
                            return;
                        }

                        SaveSeries(_series[i]);
                    }
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }

        }
        private List<CandleSeriesSaveInfo> _candleSeriesSaveInfos = new List<CandleSeriesSaveInfo>();
        private object _lockerSpec = new object();

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

        private void SaveSeries(CandleSeries series)
        {
            CandleSeriesSaveInfo mySaveInfo = GetSpecInfo(series.Specification);

            if (mySaveInfo.AllCandlesInFile == null)
            {
                mySaveInfo.AllCandlesInFile = series.CandlesAll;

                int indexSpec = _candleSeriesSaveInfos.FindIndex(s => s.Specification == series.Specification);
                _candleSeriesSaveInfos[indexSpec].AllCandlesInFile = series.CandlesAll;
            }
            if (series.CandlesAll == null ||
                series.CandlesAll.Count == 0)
            {
                return;
            }

            Candle firstCandle = series.CandlesAll[0];
            Candle lastCandle = series.CandlesAll[series.CandlesAll.Count - 1];

            if (firstCandle.TimeStart == mySaveInfo.LastCandleTime &&
                lastCandle.TimeStart == mySaveInfo.StartCandleTime &&
                lastCandle.Close == mySaveInfo.LastCandlePrice)
            {
                return;
            }

            mySaveInfo.InsertCandles(series.CandlesAll);

            if (Directory.Exists(_pathName) == false)
            {
                Directory.CreateDirectory(_pathName);
            }

            StreamWriter writer = new StreamWriter(_pathName + "\\" + series.Specification + ".txt");

            for (int i = 0; i < mySaveInfo.AllCandlesInFile.Count; i++)
            {
                writer.WriteLine(mySaveInfo.AllCandlesInFile[i].StringToSave);
            }

            writer.Close();
        }

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

            return candles;
        }

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
                catch (Exception e)
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
                catch (Exception e)
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
                resultCandles.Merge(candlesFromOsData);
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
            myInfo.InsertCandles(resultCandles);

            return myInfo;
        }

        // log messages сообщения в лог 

        /// <summary>
        /// send a new message to up
        /// выслать новое сообщение на верх
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
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// information to save trades/информация для сохранения тиков
    /// </summary>
    public class CandleSeriesSaveInfo
    {
        public void InsertCandles(List<Candle> candles)
        {
            if (AllCandlesInFile == null)
            {
                AllCandlesInFile = new List<Candle>();
            }

            AllCandlesInFile.Merge(candles);

            if (AllCandlesInFile.Count == 0)
            {
                return;
            }

            LastCandleTime = AllCandlesInFile[AllCandlesInFile.Count - 1].TimeStart;
            StartCandleTime = AllCandlesInFile[0].TimeStart;
            LastCandlePrice = AllCandlesInFile[AllCandlesInFile.Count - 1].Close;
        }

        public List<Candle> AllCandlesInFile;

        public string Specification;

        public DateTime LastCandleTime;

        public DateTime StartCandleTime;

        public decimal LastCandlePrice;
    }
}
