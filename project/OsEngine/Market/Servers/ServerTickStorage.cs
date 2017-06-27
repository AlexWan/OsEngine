/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsData;

namespace OsEngine.Market.Servers
{

    /// <summary>
    /// хранилище тиков для сервера
    /// </summary>
    public class ServerTickStorage
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="server">сервер с которого будем сохранять тики</param>
        public ServerTickStorage(IServer server)
        {
            _server = server;

            if (!Directory.Exists(@"Data"))
            {
                Directory.CreateDirectory(@"Data");
            }

            Thread saver = new Thread(TickSaverSpaceInOneFile);
            saver.CurrentCulture = new CultureInfo("RU-ru");
            saver.IsBackground = false;
            saver.Start();

            _pathName = @"Data" + @"\" + server.GetType().Name + @"Trades";

        }

        private IServer _server;

        /// <summary>
        /// нужно ли сохранять сделки
        /// </summary>
        public bool NeadToSave;

        /// <summary>
        /// сколько дней надо грузить из истории
        /// </summary>
        public int DaysToLoad;

        /// <summary>
        /// название папки для хранения данных
        /// </summary>
        private string _pathName;

        /// <summary>
        /// инструменты которые нужно сохранять
        /// </summary>
        private List<Security> _securities;

        /// <summary>
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
        /// по какому-то инструменту загрузили тики
        /// </summary>
        public event Action<List<Trade>[]> TickLoadedEvent;


        /// <summary>
        /// сервисная информация для сохранения трейдов
        /// </summary>
        private List<TradeSaveInfo> _tradeSaveInfo;

        // для сохранения в один файл

        /// <summary>
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

                            StreamWriter writer =
                                new StreamWriter(_pathName + @"\" + allTrades[i1][0].SecurityNameCode + ".txt", false);

                            StringBuilder saveStr = new StringBuilder();

                            for (int i = 0; i < allTrades[i1].Count - 1; i++)
                            {
                                saveStr.Append(allTrades[i1][i].GetSaveString() + "\r\n");
                            }
                            tradeInfo.LastSaveIndex = allTrades[i1].Count - 1;
                            writer.Write(saveStr);
                            writer.Close();
                        }
                        else
                        {
                            if (tradeInfo.LastSaveIndex == allTrades[i1].Count)
                            {
                                continue;
                            }
                            StreamWriter writer =
                                new StreamWriter(_pathName + @"\" + allTrades[i1][0].SecurityNameCode + ".txt", true);


                            for (int i = tradeInfo.LastSaveIndex; i < allTrades[i1].Count - 1; i++)
                            {
                                writer.WriteLine(allTrades[i1][i].GetSaveString());
                            }
                            tradeInfo.LastSaveIndex = allTrades[i1].Count - 1;
                            writer.Close();
                        }
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
        /// загрузить тики
        /// </summary>
        /// <param name="dayCount">количество дней которые нужно подгрузить</param>
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
                    // загружаем
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
                        DateTime timeStart = DateTime.Now.AddDays(-DaysToLoad - 2);

                        if (timeStart.Month == 1 && timeStart.Day < 10)
                        {
                            timeStart = timeStart.AddDays(-10);
                        }

                        while (!reader.EndOfStream)
                        {
                            Trade newTrade = new Trade();

                            try
                            {
                                newTrade.SetTradeFromString(reader.ReadLine());
                            }
                            catch
                            {
                               continue;
                            }

                            newTrade.SecurityNameCode = nameSecurity;

                            if (newTrade.Time.Date < timeStart.Date)
                            {
                                continue;
                            }

                            newList.Add(newTrade);
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    // сохраняем

                    if (newList.Count == 0)
                    {
                        continue;
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

        // сообщения в лог 

        /// <summary>
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // если на нас никто не подписан и в логе ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }
}
