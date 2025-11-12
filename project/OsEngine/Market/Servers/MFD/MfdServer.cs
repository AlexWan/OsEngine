using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;


namespace OsEngine.Market.Servers.MFD
{
    class MfdServer : AServer
    {
        public MfdServer()
        {
            MfdServerRealization realization = new MfdServerRealization();
            ServerRealization = realization;
            NeedToHideParameters = true;
        }
    }

    public class MfdServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

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

        public void Connect(WebProxy proxy)
        {
            _connectResult = GetStringRequest("http://mfd.ru/export/");

            if (!string.IsNullOrEmpty(_connectResult))
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
            else
            {
                SendLogMessage($"Connect server error", LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        private string _connectResult;

        public ServerConnectStatus ServerStatus { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            SendLogMessage("Securities downloading...", LogMessageType.System);

            string[] lines = _connectResult.Split('\n');

            List<string> classes = GetClasses(lines);

            List<Security> securities = new List<Security>();

            for (int i = 0; i < classes.Count; i++)
            {
                if (classes[i].Contains("Опционы"))
                {
                    continue;
                }

                List<Security> curSecs = GetAllSecuritiesToClass(classes[i]);

                if (curSecs != null &&
                    curSecs.Count > 0)
                {
                    securities.AddRange(curSecs);
                }
            }

            if (SecurityEvent != null)
            {
                SecurityEvent(securities);
            }

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

                line = line.Split('=')[line.Split('=').Length - 1];
                line = line.Split('<')[0];
                line = line.Replace("\"", "");

                classes[i] = line.Split('>')[1] + "#" + line.Split('>')[0];
            }

            return classes;
        }

        private List<Security> GetAllSecuritiesToClass(string curclass)
        {
            string request = "http://mfd.ru/export/?groupId=" + curclass.Split('#')[1];

            string response = GetStringRequest(request);

            if (!string.IsNullOrEmpty(response))
            {
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
                    if (secInLines[i].Contains("select"))
                    {
                        continue;
                    }

                    Security newSecurity = new Security();
                    newSecurity.NameClass = curclass;
                    newSecurity.Name = secInLines[i].Split('#')[0];
                    newSecurity.NameFull = newSecurity.Name;
                    newSecurity.NameId = secInLines[i];

                    if (string.IsNullOrEmpty(newSecurity.Name))
                    {
                        newSecurity.Name = newSecurity.NameId;
                        newSecurity.NameFull = newSecurity.NameId;
                    }

                    securities.Add(newSecurity);
                }

                return securities;
            }
            else
            {
                SendLogMessage("Securities downloading by classes error", LogMessageType.Error);
                return null;
            }
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
            res.RemoveAt(res.Count - 1);

            return res;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Porfolio

        public void GetPortfolios()
        {
            Portfolio portfolio = new Portfolio();
            portfolio.ValueCurrent = 1;
            portfolio.Number = "Mfd fake portfolio";

            if (PortfolioEvent != null)
            {
                PortfolioEvent(new List<Portfolio>() { portfolio });
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
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

            List<Candle> candles = GetCandles(security, startTime, endTime, minutes);
            return candles;
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
            requestStr += "&Alias=false&Period=" + minutesCount;
            requestStr += "&timeframeValue =" + minutesCount;
            requestStr += "&timeframeDatePart=day&StartDate=" + startTime.Date.ToString("dd/MM/yyyy").Replace("/", ".");
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

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                string[] line = lines[i].Split(';');

                DateTime timeStart = DateTimeParseHelper.ParseFromTwoStrings(line[2], line[3]);

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

        //  private object locker = new object();

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            DateTime availableStartDate = DateTime.Now.Date.AddDays(-30);

            // глубина тиков 1 мес.
            if (startTime < availableStartDate && endTime < availableStartDate)
            {
                SendLogMessage("Attention! The trades data depth is not more than one month", LogMessageType.System);
                return null;
            }
            else if (startTime < availableStartDate && endTime >= availableStartDate)
            {
                startTime = availableStartDate;
            }

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

            List<Trade> trades = new List<Trade>();

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                string[] line = lines[i].Split(';');

                DateTime timeStart = DateTimeParseHelper.ParseFromTwoStrings(line[2], line[3]);

                Trade trade = new Trade();
                trade.Price = line[4].ToDecimal();
                trade.Volume = line[5].ToDecimal();
                trade.Time = timeStart;
                trade.SecurityNameCode = security.Name;

                trades.Add(trade);
            }

            return trades;
        }

        #endregion

        #region 6 Queries

        private string GetStringRequest(string url)
        {
            try
            {
                RestRequest requestRest = new(Method.GET);
                RestClient client = new(url);

                string responseMessage = client.Execute(requestRest).Content;

                return responseMessage;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);

                return null;
            }
        }

        private string GetFileRequest(string url)
        {
            if (!Directory.Exists(@"Data\Temp\MfdTempFiles\"))
            {
                Directory.CreateDirectory(@"Data\Temp\MfdTempFiles\");
            }

            string fileName = @"Data\Temp\MfdTempFiles\tmpData" + ".txt";

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            RestRequest request = new RestRequest(Method.GET);
            RestClient client = new RestClient(url);

            try
            {
                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK && response.RawBytes != null && response.RawBytes.Length > 0)
                {
                    using (FileStream fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fileStream.Write(response.RawBytes, 0, response.RawBytes.Length);
                    }
                }
                else
                {
                    SendLogMessage($"File downloading error: {response.ErrorMessage}, Status: {response.StatusCode}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"File downloading error.\n{ex.Message}", LogMessageType.Error);
            }

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

        #endregion

        #region 7 Log

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

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region 8 Unused methods

        public void Subscribe(Security security) { }

        public void SendOrder(Order order) { }

        public void CancelAllOrders() { }

        public void CancelAllOrdersToSecurity(Security security) { }

        public bool CancelOrder(Order order) { return false; }

        public void ChangeOrderPrice(Order order, decimal newPrice) { }

        public void GetAllActivOrders() { }

        public OrderStateType GetOrderStatus(Order order) { return OrderStateType.None; }

        public bool SubscribeNews() { return false; }

        public List<Order> GetActiveOrders(int startIndex, int count) { return null; }

        public List<Order> GetHistoricalOrders(int startIndex, int count) { return null; }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount) { return null; }

        public event Action<News> NewsEvent { add { } remove { } }
        public event Action<Order> MyOrderEvent { add { } remove { } }
        public event Action<MyTrade> MyTradeEvent { add { } remove { } }
        public event Action<MarketDepth> MarketDepthEvent { add { } remove { } }
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }
        public event Action<Trade> NewTradesEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion
    }
}
