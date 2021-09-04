using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.MFD
{
    class MfdServer : AServer
    {
        public MfdServer()
        {
            MfdServerRealization realization = new MfdServerRealization();
            ServerRealization = realization;
            NeedToHideParams = true;
        }
    }

    public class MfdServerRealization : IServerRealization
    {
        public MfdServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public ServerType ServerType
        {
            get
            {
                return ServerType.MfdWeb;
            }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            string result = GetRequest("http://mfd.ru/export/");

            if (result == null)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
            }
            else
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent?.Invoke();
            }
        }

        #region NotUsedChleny

        public void Dispose()
        {

        }

        public void GetPortfolios()
        {
            Portfolio portfolio = new Portfolio();
            portfolio.ValueCurrent = 1;
            portfolio.Number = "Mfd fake portfolio";

            PortfolioEvent?.Invoke(new List<Portfolio>() { portfolio });
        }

        public void SendOrder(Order order)
        {

        }

        public void CancelOrder(Order order)
        {

        }

        public void Subscrible(Security security)
        {

        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<MarketDepth> MarketDepthEvent;

        #endregion

        #region Securities

        public void GetSecurities()
        {
            SendLogMessage("Securities downloading...", LogMessageType.System);

            string requestString =
                "http://mfd.ru/export/";

            string response = GetRequest(requestString);

            string[] lines = response.Split('\n');

            List<string> classes = GetClasses(lines);

            List<Security> securities = new List<Security>();

            for (int i = 0; i < classes.Count; i++)
            {
                securities.AddRange(GetAllSecuritiesToClass(classes[i]));
            }

            SecurityEvent?.Invoke(securities);

            SendLogMessage("Securities downloaded. Count: " + securities.Count, LogMessageType.System);

        }

        private List<string> GetClasses(string[] pageLines)
        {
            List<string> classes = new List<string>();

            for (int i = 0; i < pageLines.Length; i++)
            {
                if (pageLines[i].Contains("<select name=\"TickerGroup\">"))
                {
                    while (true)
                    {
                        i++;

                        string line = pageLines[i];

                        if (line.Contains("</select>"))
                        {
                            break;
                        }

                        classes.Add(pageLines[i]);
                    }
                    break;
                }
            }

            for (int i = 0; i < classes.Count; i++)
            {
                string line = classes[i];

                line = line.Split('=')[line.Split('=').Length-1];
                line = line.Split('<')[0];
                line = line.Replace("\"", "");

                classes[i] = line.Split('>')[1] + "#" + line.Split('>')[0];
            }

            return classes;
        }

        private List<Security> GetAllSecuritiesToClass(string curclass)
        {
            //

            string request = "http://mfd.ru/export/?groupId=" + curclass.Split('#')[1];

            string response = GetRequest(request);

            string[] pageLines = response.Split('\n');

            string secInstr = "";

            for (int i = 0; i < pageLines.Length; i++)
            {
                if (pageLines[i].Contains("<select name='AvailableTickers'"))
                {
                    secInstr = pageLines[i];
                    break;
                }
            }

            List<string> secInLines = ConvertToLines(secInstr);

            // обрезаем строку

            for (int i = 0; i < secInLines.Count; i++)
            {
                string line = secInLines[i];

                line = line.Split('=')[line.Split('=').Length - 1];
                line = line.Replace("\'", "");

                secInLines[i] = line.Split('>')[1] + "#" + line.Split('>')[0];
            }

            List<Security> securities = new List<Security>();

            for (int i = 0; i < secInLines.Count; i++)
            {
                Security newSecurity = new Security();
                newSecurity.NameClass = curclass;
                newSecurity.Name = secInLines[i].Split('#')[0];
                newSecurity.NameFull = newSecurity.Name;
                newSecurity.NameId = secInLines[i];
                securities.Add(newSecurity);
            }

            return securities;
        }

        private List<string> ConvertToLines(string str)
        {
            List<string> res = new List<string>();

            string curStr = "";

            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '<' && i != 0)
                {
                    res.Add(curStr);
                    curStr = "";
                }

                curStr += str[i];
            }

            res.RemoveAt(0);
            res.RemoveAt(res.Count-1);

            return res;
        }

        #endregion

        #region Candles

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            lock (locker)
            {
                string minutes = "";

                if (timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes == 1)
                {
                    minutes = "1";
                }
                if (timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes == 5)
                {
                    minutes = "2";
                }
                if (timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes == 10)
                {
                    minutes = "3";
                }
                if (timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes == 15)
                {
                    minutes = "4";
                }
                if (timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes == 30)
                {
                    minutes = "5";
                }
                if (timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes == 60)
                {
                    minutes = "6";
                }

                List<Candle> candles = GetCandles(security, startTime, endTime,  minutes);
                return candles;
            }
        }

        public List<Candle> GetCandles(Security security, DateTime startTime, DateTime endTime, string minutesCount)
        {
            //http://mfd.ru/export/handler.ashx/temp.txt?TickerGroup=16&Tickers=1463
            //&Alias=false&Period=1&timeframeValue=1
            //&timeframeDatePart=day&StartDate=01.01.2010
            //&EndDate=24.04.2020
            //&SaveFormat=0&SaveMode=1&FileName=%D0%A1%D0%B1%D0%B5%D1%80%D0%B1%D0%B0%D0%BD%D0%BA_1min_01012010_24042020.txt
            //&FieldSeparator=%253b&DecimalSeparator=.&DateFormat=yyyyMMdd&TimeFormat=HHmmss&DateFormatCustom=&TimeFormatCustom=&AddHeader=true&RecordFormat=0&Fill=false

            string fileName = "tempFile" + ".txt";

            string requestStr = "http://mfd.ru/export/handler.ashx/;" + fileName; 

            requestStr += "?TickerGroup=" + security.NameClass.Split('#')[1];
            requestStr += "&Tickers=" + security.NameId.Split('#')[1];
            requestStr += "&Alias=false&Period="+ minutesCount;
            requestStr += "&timeframeValue =" + minutesCount;
            requestStr += "&timeframeDatePart=day&StartDate=" + startTime.Date.ToString("dd/MM/yyyy").Replace("/",".");
            requestStr += "&EndDate=" + endTime.Date.ToString("dd/MM/yyyy").Replace("/", ".");
            requestStr += "&SaveFormat=0&SaveMode=1&FileName=" + fileName;
            requestStr += "&FieldSeparator=%253b&DecimalSeparator=.&DateFormat=yyyyMMdd&TimeFormat=HHmmss&DateFormatCustom=&TimeFormatCustom=&AddHeader=true&RecordFormat=0&Fill=false";

            //https://mfd.ru/export/handler.ashx/_RTS_(RI)_1hour_01042010_28042020.txt?
            //TickerGroup=26
            //    &Tickers=5*
            //    &Alias=false
            //&Period=6
            //    &timeframeValue=1
            // &timeframeDatePart=day
            // &StartDate=01.04.2010
            // &EndDate=28.04.2020
            // &SaveFormat=0
            // &SaveMode=0
            // &FileName=_RTS_(RI)_1hour_01042010_28042020.txt
            // &FieldSeparator=%253b
            // &DecimalSeparator=.
            // &DateFormat=yyyyMMdd
            // &TimeFormat=HHmmss
            // &DateFormatCustom=
            // &TimeFormatCustom=
            // &AddHeader=false
            // &RecordFormat=3
            // &Fill=false

            string response = GetFileRequest(requestStr);

            string[] lines = response.Split('\n');

            List<Candle> result = new List<Candle>();

            for (int i = 1;i < lines.Length;i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                string[] line = lines[i].Split(';');

                int year = Convert.ToInt32(line[2].Substring(0, 4));
                int month = Convert.ToInt32(line[2].Substring(4, 2));
                int day = Convert.ToInt32(line[2].Substring(6, 2));

                int hour = Convert.ToInt32(line[3].Substring(0, 2));
                int minute = Convert.ToInt32(line[3].Substring(2, 2));
                int second = Convert.ToInt32(line[3].Substring(4, 2));

                DateTime  timeStart = new DateTime(year, month, day, hour, minute, second);

                Candle candle = new Candle();
                candle.Open = line[4].ToDecimal();
                candle.High = line[5].ToDecimal();
                candle.Low = line[6].ToDecimal();
                candle.Close = line[7].ToDecimal();
                candle.Volume = line[8].ToDecimal();
                candle.TimeStart = timeStart;

                result.Add(candle);
            }

            return result;
        }

        #endregion

        private object locker = new object();

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            lock (locker)
            {
                DateTime curTime = startTime;

                DateTime curEndTime = curTime.AddDays(20);

                List<Trade> result = new List<Trade>();

                while (true)
                {
                    List<Trade> trades = GetTradesByMonth(security, curTime, curEndTime);

                    result.AddRange(trades);

                    if (curEndTime.Date == endTime.Date)
                    {
                        break;
                    }

                    curTime = curTime.AddDays(20);
                    curEndTime = curEndTime.AddDays(20);

                    if (curEndTime > endTime)
                    {
                        curEndTime = endTime;
                    }

                    if (curTime >= endTime)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        private List<Trade> GetTradesByMonth(Security security, DateTime startTime, DateTime endTime)
        {
            string fileName = "tick.txt";

            string requestStr = "http://mfd.ru/export/handler.ashx/" + fileName;

            requestStr += "?TickerGroup=" + security.NameClass.Split('#')[1];
            requestStr += "&Tickers=" + security.NameId.Split('#')[1];
            requestStr += "&Alias=false&Period=" + "0";
            requestStr += "&timeframeValue=" + "1";
            requestStr += "&timeframeDatePart=day&StartDate=" + startTime.Date.ToString("dd/MM/yyyy").Replace("/", ".");
            requestStr += "&EndDate=" + endTime.Date.ToString("dd/MM/yyyy").Replace("/", ".");
            requestStr += "&SaveFormat=0&SaveMode=1&FileName=" + fileName;
            requestStr += "&FieldSeparator=%253b&DecimalSeparator=.&DateFormat=yyyyMMdd&TimeFormat=HHmmss&DateFormatCustom=&TimeFormatCustom=&AddHeader=false&RecordFormat=2&Fill=false";

            string response = GetFileRequest(requestStr);

            if (response == null)
            {
                return new List<Trade>();
            }

            string[] lines = response.Split('\n');

            List<Trade> result = new List<Trade>();

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                string[] line = lines[i].Split(';');

                int year = Convert.ToInt32(line[2].Substring(0, 4));
                int month = Convert.ToInt32(line[2].Substring(4, 2));
                int day = Convert.ToInt32(line[2].Substring(6, 2));

                int hour = Convert.ToInt32(line[3].Substring(0, 2));
                int minute = Convert.ToInt32(line[3].Substring(2, 2));
                int second = Convert.ToInt32(line[3].Substring(4, 2));

                DateTime timeStart = new DateTime(year, month, day, hour, minute, second);

                Trade trade = new Trade();
                trade.Price = line[4].ToDecimal();
                trade.Volume = line[5].ToDecimal();
                trade.Time = timeStart;
                trade.SecurityNameCode = security.Name;

                result.Add(trade);
            }

            return result;
        }

        private string GetRequest(string url)
        {
            WebClient wb = new WebClient();
            wb.Encoding = Encoding.UTF8;

            try
            {
                string str = wb.DownloadString(new Uri(url, UriKind.Absolute));
                wb.Dispose();
                return str;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                wb.Dispose();
                return null;
            }
        }

        private string GetFileRequest(string url)
        {
            if (Directory.Exists(@"Data\Temp\") == false)
            {
                Directory.CreateDirectory(@"Data\Temp\");
            }
            string fileName = @"Data\Temp\tmpData" + ".txt";


           if (File.Exists(fileName))
           {
               File.Delete(fileName);
           }

           WebClient wb = new WebClient();
           bool _tickLoaded = false;

            try
           {
               
               wb.DownloadFileAsync(new Uri(url, UriKind.Absolute), fileName);
               wb.DownloadFileCompleted += delegate(object sender, AsyncCompletedEventArgs args)
               {
                   _tickLoaded = true;
               };
               
           }
           catch (Exception)
           {
               wb.Dispose();
               return null;
           }

           while (true)
           {
               Thread.Sleep(1000);
               if (_tickLoaded)
               {
                   break;
               }
           }
           wb.Dispose();

           if (!File.Exists(fileName))
           { // file is not uploaded / файл не загружен
               return null;
           }

           StringBuilder builder = new StringBuilder();

            StreamReader reader = new StreamReader(fileName);

           while (!reader.EndOfStream)
           {
               builder.Append(reader.ReadLine() + "\n");
           }

           reader.Close();

           return builder.ToString();
        }

        public event Action<List<Security>> SecurityEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action<string, LogMessageType> LogMessageEvent;

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else
            {
                MessageBox.Show(message);
            }
        }

    }
}
