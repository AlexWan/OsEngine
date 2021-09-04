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

    /// <summary>
    /// server ticks storage
    /// хранилище тиков для сервера
    /// </summary>
    public class ServerTickStorage
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="server"> server for saving ticks / сервер с которого будем сохранять тики </param>
        public ServerTickStorage(IServer server)
        {
            _server = server;

            if (!Directory.Exists(@"Data"))
            {
                Directory.CreateDirectory(@"Data");
            }

            _pathName = @"Data" + @"\" + server.GetType().Name + @"Trades";

            Thread saver = new Thread(TickSaverSpaceInOneFile);
            saver.CurrentCulture = new CultureInfo("RU-ru");
            saver.IsBackground = false;
            saver.Start();
        }

        private IServer _server;

        /// <summary>
        /// shows whether need to save trades
        /// нужно ли сохранять сделки
        /// </summary>
        public bool NeadToSave;

        /// <summary>
        /// how many days upload from history
        /// сколько дней надо грузить из истории
        /// </summary>
        public int DaysToLoad;

        /// <summary>
        /// directory for saving data
        /// название папки для хранения данных
        /// </summary>
        private string _pathName;

        /// <summary>
        /// securities for saving
        /// инструменты которые нужно сохранять
        /// </summary>
        private List<Security> _securities;

        /// <summary>
        /// save security data 
        /// сохранять данные по бумаге
        /// </summary>
        public void SetSecurityToSave(Security security)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            if (_securities.Find(security1 => security1.Name == security.Name) == null)
            {
                _securities.Add(security);
            }
        }

        /// <summary>
        /// upload ticks for some instrument
        /// по какому-то инструменту загрузили тики
        /// </summary>
        public event Action<List<Trade>[]> TickLoadedEvent;


        /// <summary>
        /// service information for saving trades
        /// сервисная информация для сохранения трейдов
        /// </summary>
        private List<TradeSaveInfo> _tradeSaveInfo;

        // for saving in one file
        // для сохранения в один файл

        /// <summary>
        /// method with tick saving thread
        /// метод в котором работает поток сохраняющий тики
        /// </summary>
        private void TickSaverSpaceInOneFile()
        {
            _tradeSaveInfo = new List<TradeSaveInfo>();
            try
            {
                if (!Directory.Exists(_pathName))
                {
                    Directory.CreateDirectory(_pathName);
                }
                while (true)
                {
                    Thread.Sleep(15000);

                    if (_server.ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    if (NeadToSave == false)
                    {
                        continue;
                    }

                    if (_weLoadTrades == false)
                    {
                        continue;
                    }

                    List<Trade>[] allTrades = _server.AllTrades;

                    for (int i1 = 0;
                        allTrades != null && Thread.CurrentThread.Name != "deleteThread" && i1 < allTrades.Length;
                        i1++)
                    {
                        if (allTrades[i1].Count == 0)
                        {
                            continue;
                        }
                        if (MainWindow.ProccesIsWorked == false)
                        {
                            // если приложение закрывается
                            return;
                        }

                        if (_securities == null ||
                            (_securities != null &&
                             _securities.Find(security => security.Name == allTrades[i1][0].SecurityNameCode) == null))
                        {
                            continue;
                        }

                        TradeSaveInfo tradeInfo =
                            _tradeSaveInfo.Find(s => s.NameSecurity == allTrades[i1][0].SecurityNameCode);

                        if (tradeInfo == null)
                        {
                            tradeInfo = new TradeSaveInfo();
                            tradeInfo.NameSecurity = allTrades[i1][0].SecurityNameCode;
                            _tradeSaveInfo.Add(tradeInfo);
                        }

                        if (tradeInfo.LastSaveIndex == allTrades[i1].Count)
                        {
                            continue;
                        }

                        int lastSecond = allTrades[i1][tradeInfo.LastSaveIndex].Time.Second;
                        int lastMillisecond = allTrades[i1][tradeInfo.LastSaveIndex].MicroSeconds;

                        StreamWriter writer =
                            new StreamWriter(_pathName + @"\" + allTrades[i1][0].SecurityNameCode + ".txt", true);
                        for (int i = tradeInfo.LastSaveIndex; i < allTrades[i1].Count - 1; i++)
                        {
                            if (allTrades[i1][i].MicroSeconds == 0)
                            { // for some time in microseconds if the connector did not issue them to us / генерим какое-то время микросекунд, если нам коннектор их не выдал
                                if (lastSecond != allTrades[i1][i].Time.Second)
                                {
                                    lastMillisecond = 0;
                                    lastSecond = allTrades[i1][i].Time.Second;
                                }

                                allTrades[i1][i].MicroSeconds = lastMillisecond += 10;
                            }

                            writer.WriteLine(allTrades[i1][i].GetSaveString());
                        }
                        tradeInfo.LastSaveIndex = allTrades[i1].Count - 1;
                        writer.Close();

                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool _weLoadTrades;

        /// <summary>
        /// upload ticks
        /// загрузить тики
        /// </summary>
        /// <param name="dayCount"> number of days for uploading / количество дней которые нужно подгрузить </param>
        public void LoadTick()
        {
            try
            {
                if (!Directory.Exists(_pathName))
                {
                    _weLoadTrades = true;
                    return;
                }

                List<Trade>[] allTrades = _server.AllTrades;

                string[] saves = Directory.GetFiles(_pathName);

                for (int i = 0; i < saves.Length; i++)
                {
                    // upload / загружаем
                    StreamReader reader = new StreamReader(saves[i]);

                    List<Trade> newList = new List<Trade>();

                    string nameSecurity;

                    try
                    {
                        string[] array = saves[i].Split('\\');

                        nameSecurity = array[2].Split('.')[0];
                    }
                    catch
                    {
                        continue;
                    }

                    try
                    {
                        DateTime timeStart = DateTime.Now.AddDays(-DaysToLoad - 1);

                        if (timeStart.Month == 1 && timeStart.Day < 10)
                        {
                            timeStart = timeStart.AddDays(-10);
                        }

                        List<string> tradesInStr = new List<string>();

                        while (!reader.EndOfStream)
                        {
                            tradesInStr.Add(reader.ReadLine());
                        }

                        for (int i2 = 0; i2 < tradesInStr.Count; i2++)
                        {
                            Trade newTrade = new Trade();

                            try
                            {
                                newTrade.SetTradeFromString(tradesInStr[i2]);
                            }
                            catch
                            {
                                continue;
                            }

                            newTrade.SecurityNameCode = nameSecurity;

                            if (newTrade.Time.Date < timeStart.Date)
                            {
                                i2 += 100;
                                continue;
                            }

                            newList.Add(newTrade);
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    // save / сохраняем

                    if (newList.Count == 0)
                    {
                        continue;
                    }

                    if (_tradeSaveInfo.Find(s => s.NameSecurity == newList[0].SecurityNameCode) == null)
                    {
                        TradeSaveInfo tradeInfo = new TradeSaveInfo();
                        tradeInfo.NameSecurity = newList[0].SecurityNameCode;
                        tradeInfo.LastSaveIndex = newList.Count;
                        _tradeSaveInfo.Add(tradeInfo);
                    }

                    if (allTrades == null)
                    {
                        allTrades = new[] { newList };
                    }
                    else
                    {
                        List<Trade>[] newListsArray = new List<Trade>[allTrades.Length + 1];
                        for (int ii = 0; ii < allTrades.Length; ii++)
                        {
                            newListsArray[ii] = allTrades[ii];
                        }
                        newListsArray[newListsArray.Length - 1] = newList;
                        allTrades = newListsArray;
                    }

                    reader.Close();
                }

                if (TickLoadedEvent != null)
                {
                    TickLoadedEvent(allTrades);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            _weLoadTrades = true;
        }

        // log messages
        // сообщения в лог 

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
}
