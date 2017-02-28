/*
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

            if (_server.ServerType != ServerType.InteractivBrokers)
            {
                Thread saver = new Thread(TickSaverSpaceInSeveralFiles);
                saver.CurrentCulture = new CultureInfo("RU-ru");
                saver.IsBackground = false;
                saver.Start();

                _pathName = @"Data" + @"\" + server.GetType().Name + @"Trades";
            }
            else
            {
                Thread saver = new Thread(TickSaverSpaceInOneFile);
                saver.CurrentCulture = new CultureInfo("RU-ru");
                saver.IsBackground = false;
                saver.Start();

                _pathName = @"Data" + @"\" + server.GetType().Name + @"Trades";
            }


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

       // сохранение во множество файлов

        /// <summary>
        /// загрузить тики из файлов
        /// </summary>
        public void LoadTick()
        {
            if (_server.ServerType == ServerType.InteractivBrokers)
            {
                LoadTickInOnFile();
                return;
            }

            if (!Directory.Exists(_pathName))
            {
                return;
            }

            if (DaysToLoad == 0)
            {
                return;
            }

            string[] saves = Directory.GetFiles(_pathName);

            List<Trade>[] allTrades = null;

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
                    nameSecurity = nameSecurity.Split('@')[0];
                }
                catch
                {
                    continue;
                }

                try
                {
                    while (!reader.EndOfStream)
                    {
                        Trade newTrade = new Trade();
                        newTrade.SetTradeFromString(reader.ReadLine());
                        newTrade.SecurityNameCode = nameSecurity;
                        newList.Add(newTrade);
                    }
                }
                catch (Exception)
                {
                    // ignore
                }

                if(newList.Count == 0)
                {
                    continue;
                }

// сохраняем в общий массив

                bool isInArray = false;

                for (int indAllTrades = 0; allTrades != null && indAllTrades < allTrades.Length; indAllTrades++)
                {
                    if (allTrades[indAllTrades][0].SecurityNameCode == newList[0].SecurityNameCode)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (allTrades == null ||
                    !isInArray)
                { // если у нас такого тикера в коллекции ещё нет.
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
                }
                else
                {// если такой тикер уже есть. И надо сортировать
                    List<Trade> listOld = allTrades.First(list => list[0].SecurityNameCode == newList[0].SecurityNameCode);

                    if (listOld.FindIndex(trade => trade.Time < newList[0].Time) == -1)
                    { // старый массив целиком больше по времени чем новый

                        newList.AddRange(listOld);
                        
                        for (int indLists = 0; indLists < allTrades.Length; indLists++)
                        {
                            if (allTrades[indLists][0].SecurityNameCode == newList[0].SecurityNameCode)
                            {
                                allTrades[indLists] = newList;
                                break;
                            }
                        }
                    }
                    else if (listOld[listOld.Count - 1].Time < newList[0].Time)
                    { // старый массив целиком меньше по времени чем новый
                          listOld.AddRange(newList);
                    }
                    else
                    { // в старый массив нужно вставить новый в середину
                       /* for (int indexList = 0; indexList < newList.Count; indexList++)
                        {
                            int index = listOld.FindIndex(trade => trade.Time < newList[0].Time);

                            listOld.Insert(index, newList[indexList]);
                        }*/
                    }
                }

                reader.Close();
            }

// считаем сколько дней в архиве и обрезаем

            for (int i = 0; allTrades != null && i < allTrades.Length; i++)
            {
                List<Trade> lastList = allTrades[i];

                int dayCount = 0;
                int lastDay = -1;
                for (int iii = 0; iii < lastList.Count; iii++)
                {
                    if (lastDay == -1 ||
                        lastDay != lastList[iii].Time.Day )
                    {
                        dayCount++;
                        lastDay = lastList[iii].Time.Day;
                    }
                }

                if (dayCount > DaysToLoad)
                { // обрезаме лишнее
                    int dayToLost = dayCount - DaysToLoad;

                    lastDay = -1;
                    dayCount = 0;
                    List<Trade> newNewList = new List<Trade>();

                    for (int iii = 0; iii < lastList.Count; iii++)
                    {
                        if (lastDay == -1 ||
                            lastDay != lastList[iii].Time.Day)
                        {
                            dayToLost--;
                            dayCount++;
                            lastDay = lastList[iii].Time.Day;
                        }

                        if (dayToLost < 0)
                        {
                            newNewList.Add(lastList[iii]);
                        }
                    }

                    for (int indLists = 0; indLists < allTrades.Length; indLists++)
                    {
                        if (allTrades[indLists][0].SecurityNameCode == newNewList[0].SecurityNameCode)
                        {
                            allTrades[indLists] = newNewList;
                            break;
                        }
                    }
                }
            }

//собираем объекты хранящие данные для хранения тиков в файле
            for (int i = 0; allTrades != null && i < allTrades.Length; i++)
            {
                if (_tradeSaveInfo == null)
                {
                    _tradeSaveInfo = new List<TradeSaveInfo>();
                }

                TradeSaveInfo tradeSaveInfo =
                    _tradeSaveInfo.Find(info => info.NameSecurity == allTrades[i][0].SecurityNameCode);

                if (tradeSaveInfo == null)
                {
                    tradeSaveInfo = new TradeSaveInfo();
                    tradeSaveInfo.NameSecurity = allTrades[i][0].SecurityNameCode;
                    tradeSaveInfo.LastSaveObjectTime = allTrades[i][allTrades[i].Count - 1].Time;
                    _tradeSaveInfo.Add(tradeSaveInfo);
                }
            }

// высылаем данные на верх          


            if (TickLoadedEvent != null)
            {
                TickLoadedEvent(allTrades);
            }
        }

        /// <summary>
        /// метод в котором работает поток сохраняющий тики
        /// </summary>
        private void TickSaverSpaceInSeveralFiles()
        {
            try
            {
                Thread.Sleep(30000);

                while (true)
                {
                    Thread.Sleep(2000);

                    if (_server.ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(10000);
                        continue;
                    }

                    if (!Directory.Exists(_pathName))
                    {
                        Directory.CreateDirectory(_pathName);
                    }

                    List<Trade>[] allTrades = _server.AllTrades;

                    int day = DateTime.Now.Day;

                    for (int i1 = 0; allTrades != null && i1 < allTrades.Length; i1++)
                    {
                        if (MainWindow.ProccesIsWorked == false)
                        { // если приложение закрывается
                            return;
                        }

                        if (_securities != null &&
                            _securities.Find(security => security.Name == allTrades[i1][0].SecurityNameCode) != null)
                        {
                            int indexFirst = 0;

                            for (int i = 0; i < allTrades[i1].Count; i++)
                            {
                                if (allTrades[i1][i].Time.Day == day)
                                {
                                   indexFirst = i;
                                    break;
                                }
                            }

                            string path = _pathName + @"\" + allTrades[i1][0].SecurityNameCode + "@" + Convert.ToInt32((allTrades[i1][indexFirst].Time - DateTime.MinValue).TotalDays) + ".txt";

                            SaveThisTick(allTrades[i1], path, allTrades[i1][0].SecurityNameCode, indexFirst);
                        }
                    }
                    Thread.Sleep(60000);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// сервисная информация для сохранения трейдов
        /// </summary>
        private List<TradeSaveInfo> _tradeSaveInfo;

        /// <summary>
        /// сохранить серию тиков
        /// </summary>
        /// <param name="trades">тики</param>
        /// <param name="path">путь</param>
        /// <param name="securityName">имя бумаги</param>
        /// <param name="startIndex">индекс для старта сохранения</param>
        private void SaveThisTick(List<Trade> trades, string path, string securityName, int startIndex)
        {
            if (_tradeSaveInfo == null)
            {
                _tradeSaveInfo = new List<TradeSaveInfo>();
            }

            // берём хранилище тиков

            TradeSaveInfo tradeSaveInfo =
                _tradeSaveInfo.Find(info => info.NameSecurity == securityName);

            if (tradeSaveInfo == null)
            {
                tradeSaveInfo = new TradeSaveInfo();
                tradeSaveInfo.NameSecurity = securityName;
                tradeSaveInfo.LastSaveObjectTime = trades[startIndex].Time;
                _tradeSaveInfo.Add( tradeSaveInfo);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            int firstCandle = 0;

            if (tradeSaveInfo.LastSaveObjectTime ==
                trades[trades.Count - 1].Time)
            {
                // если у нас старые тики совпадают с новыми.
                return;
            }

            for (int i = trades.Count - 1; i > -1; i--)
            {
                if (trades[i].Time <= tradeSaveInfo.LastSaveObjectTime ||
                    startIndex >= i)
                {
                    firstCandle = i + 1;
                    break;
                }
            }

            tradeSaveInfo.LastSaveObjectTime = trades[trades.Count - 1].Time;
            // записываем

            try
            {
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    for (int i = firstCandle; i < trades.Count; i++)
                    {
                        writer.WriteLine(trades[i].GetSaveString());
                    }
                }
            }
            catch 
            {

            }
        }

        // для сохранения в один файл

        /// <summary>
        /// метод в котором работает поток сохраняющий тики
        /// </summary>
        private void TickSaverSpaceInOneFile()
        {
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

                    List<Trade>[] allTrades = _server.AllTrades;

                    for (int i1 = 0; allTrades != null && Thread.CurrentThread.Name != "deleteThread" && i1 < allTrades.Length; i1++)
                    {
                        if (MainWindow.ProccesIsWorked == false)
                        { // если приложение закрывается
                            return;
                        }
                        if (_securities != null &&
                            _securities.Find(security => security.Name == allTrades[i1][0].SecurityNameCode) != null)
                        {
                            StreamWriter writer = new StreamWriter(_pathName + @"\" + allTrades[i1][0].SecurityNameCode + ".txt", false);

                            string saveStr = "";

                            for (int i = 0; i < allTrades[i1].Count; i++)
                            {
                                saveStr += allTrades[i1][i].GetSaveString() + "\r\n";
                            }

                            writer.Write(saveStr);

                            writer.Close();
                        }
                        else
                        {
                            if (File.Exists(_pathName + allTrades[i1][0].SecurityNameCode + ".txt"))
                            {
                                File.Delete(_pathName + allTrades[i1][0].SecurityNameCode + ".txt");
                            }
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// загрузить тики
        /// </summary>
        private void LoadTickInOnFile()
        {
            try
            {
                if (!Directory.Exists(_pathName))
                {
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
                        while (!reader.EndOfStream)
                        {
                            Trade newTrade = new Trade();
                            newTrade.SetTradeFromString(reader.ReadLine());
                            newTrade.SecurityNameCode = nameSecurity;
                            newList.Add(newTrade);
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    // сохраняем
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
