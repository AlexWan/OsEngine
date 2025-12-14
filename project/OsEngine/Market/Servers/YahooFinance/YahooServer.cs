using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.YahooFinance.Entity;
using OsEngine.Market.Servers.Entity;
using System.IO;
using RestSharp;
using BytesRoad.Net.Ftp;
using System.Net;

namespace OsEngine.Market.Servers.YahooFinance
{
    public class YahooServer : AServer
    {
        public YahooServer()
        {
            YahooServerRealization realization = new YahooServerRealization();
            ServerRealization = realization;

            CreateParameterBoolean("Pre-post-market data", false);
        }
    }

    public class YahooServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public YahooServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public ServerType ServerType
        {
            get { return ServerType.YahooFinance; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy)
        {
            try
            {
                if (((ServerParameterBool)ServerParameters[0]).Value == true)
                {
                    _premarket = "true";
                }
                else
                {
                    _premarket = "false";
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Connect;

                    if (ConnectEvent != null)
                    {
                        ConnectEvent();
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Error connect: {exception.Message}", LogMessageType.Error);

                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();

                return;
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

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private RestClient _httpClient = new RestClient("https://query2.finance.yahoo.com/v8/finance/chart/");

        private string _premarket = "false";

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            GetFileFromFtp();

            List<Security> securities = new List<Security>();

            if (!File.Exists(@"Engine\YahooSecurities.txt"))
            {
                return;
            }
            try
            {
                List<string> list = new List<string>();

                using (StreamReader reader = new StreamReader(@"Engine\YahooSecurities.txt"))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            string[] split = line.Split('|');
                            if (split[0] == "Y")
                            {
                                Security security = new Security();
                                security.Name = split[1];
                                security.NameFull = split[2];

                                if (security.NameFull.Contains("ETF"))
                                {
                                    security.NameClass = "ETF";
                                }
                                else if (security.NameFull.Contains("Common Stock")
                                    || security.NameFull.Contains("ordinary share")
                                    || security.NameFull.Contains("Ordinary Shares")
                                    || security.NameFull.Contains("Ordinary Share")
                                    //|| security.NameFull.Contains("Inc")
                                    || security.NameFull.Contains("Shares")
                                    || security.NameFull.Contains("Units")
                                    || security.NameFull.Contains("Common Share")
                                    || security.NameFull.Contains("common shares"))
                                {
                                    security.NameClass = "Stock";
                                }
                                else
                                {
                                    security.NameClass = "Else";
                                }

                                security.NameId = security.Name;
                                security.SecurityType = SecurityType.Stock;
                                security.Lot = 1;
                                security.PriceStep = 1;
                                security.Decimals = 0;
                                security.PriceStepCost = 1;
                                security.State = SecurityStateType.Activ;
                                security.Exchange = ServerType.YahooFinance.ToString();

                                securities.Add(security);
                            }
                        }
                    }                    
                }

                if (SecurityEvent != null)
                {
                    SecurityEvent(securities);
                }

            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
        }

        private void GetFileFromFtp()
        {
            try
            {
                FtpClient client = new FtpClient();

                client.PassiveMode = true; //Включаем пассивный режим.
                int TimeoutFtp = 30000; //Таймаут.
                string ftpServer = "ftp.nasdaqtrader.com";
                int ftpPort = 21;
                string ftpUser = "anonymous";
                string ftpPassword = "root@example.com";

                client.Connect(TimeoutFtp, ftpServer, ftpPort);
                client.Login(TimeoutFtp, ftpUser, ftpPassword);

                client.GetFile(TimeoutFtp, "Engine/YahooSecurities.txt", "/symboldirectory/nasdaqtraded.txt");

                client.Disconnect(TimeoutFtp);
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion
                
        #region 4 Data

        public RateGate _rateGateCandles = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return null;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            try
            {
                if (!CheckTime(startTime, endTime, actualTime))
                {
                    return null;
                }

                int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

                if (!CheckTf(tfTotalMinutes))
                {
                    return null;
                }

                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

                DateTime startTimeLimit = GetStartTime(startTime, interval);
                DateTime endTimeLimit = DateTime.Now;

                if (endTime < startTimeLimit)
                {
                    return null;
                }

                long from = 0;
                long to = 0;

                if (startTime < startTimeLimit &&
                    endTime > startTimeLimit)
                {
                    from = TimeManager.GetTimeStampSecondsToDateTime(startTimeLimit);
                    to = TimeManager.GetTimeStampSecondsToDateTime(endTime);
                }
                else if (startTime >= startTimeLimit)
                {
                    from = TimeManager.GetTimeStampSecondsToDateTime(startTime);
                    to = TimeManager.GetTimeStampSecondsToDateTime(endTime);
                }

                List<Candle> allCandles = RequestCandleHistory(security.Name, interval, from, to);

                if (allCandles == null || allCandles.Count == 0)
                {
                    return null;
                }

                return allCandles;
            }
            catch (Exception ex)
            {
                SendLogMessage("GetCandleDataToSecurity: " + ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private DateTime GetStartTime(DateTime startTime, string interval)
        {
            DateTime endTime = DateTime.Now;

            switch (interval)
            {
                case ("1m"):
                    return endTime.AddDays(-8);
                case ("2m"):
                    return endTime.AddDays(-60);
                case ("5m"):
                    return endTime.AddDays(-60);
                case ("15m"):
                    return endTime.AddDays(-60);
                case ("30m"):
                    return endTime.AddDays(-60);
                case ("1h"):
                    return endTime.AddDays(-730);
                case ("1d"):
                    return startTime;                
            }

            return endTime;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
                startTime >= DateTime.Now ||
                actualTime > endTime ||
                actualTime > DateTime.Now ||
                endTime < DateTime.UtcNow.AddYears(-20))
            {
                return false;
            }
            return true;
        }

        private bool CheckTf(int timeFrameMinutes)
        {
            if (timeFrameMinutes == 1 ||
                timeFrameMinutes == 2 ||
                timeFrameMinutes == 5 ||
                timeFrameMinutes == 15 ||
                timeFrameMinutes == 30 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 1440)
            {
                return true;
            }
            return false;
        }

        private string GetInterval(TimeSpan timeFrame)
        {
            if (timeFrame.TotalMinutes < 60)
            {
                return $"{timeFrame.TotalMinutes}m";
            }
            else if (timeFrame.TotalMinutes >= 60 &&
                timeFrame.TotalMinutes < 1440)
            {
                return $"{timeFrame.TotalHours}h";
            }
            else
            {
                return $"{timeFrame.Days}d";
            }           
        }

        private readonly RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private List<Candle> RequestCandleHistory(string security, string resolution, long fromTimeStamp, long toTimeStamp)
        {
            _rgCandleData.WaitToProceed(100);

            try
            {
                string queryParam = $"{security}?";
                queryParam += $"symbol={security}&";
                queryParam += $"interval={resolution}&";
                queryParam += $"period1={fromTimeStamp}&";
                queryParam += $"period2={toTimeStamp}&";
                queryParam += $"includePrePost={_premarket}";
                
                RestRequest request = new RestRequest(queryParam, Method.GET);
                IRestResponse responseMessage = _httpClient.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return ConvertCandles(responseMessage.Content);
                }
                else
                {
                    SendLogMessage($"RequestCandleHistory: {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Candle> ConvertCandles(string json)
        {
            CandlesResponce response = JsonConvert.DeserializeObject<CandlesResponce>(json);

            List<Candle> candles = new List<Candle>();

            List<string> timeStamp = response.chart.result[0].timestamp;

            Quote quotes = response.chart.result[0].indicators.quote[0];

            if (timeStamp == null 
                || timeStamp.Count == 0 
                || quotes.close.Count == 0)
            {
                return null;
            }

            if (timeStamp.Count != quotes.close.Count)
            {
                return null;
            }

            for (int i = 0; i < timeStamp.Count; i++)
            {               
                if (CheckCandlesToZeroData(quotes, i))
                {
                    continue;
                }

                Candle candle = new Candle();

                candle.State = CandleState.Finished;
                candle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(long.Parse(timeStamp[i]));
                candle.Volume = quotes.volume[i].ToDecimal();
                candle.Close = quotes.close[i].ToDecimal();
                candle.High = quotes.high[i].ToDecimal();
                candle.Low = quotes.low[i].ToDecimal();
                candle.Open = quotes.open[i].ToDecimal();

                candles.Add(candle);
            }
            return candles;
        }

        private bool CheckCandlesToZeroData(Quote item, int i)
        {
            if (item.close[i].ToDecimal() == 0 ||
                item.open[i].ToDecimal() == 0 ||
                item.high[i].ToDecimal() == 0 ||
                item.low[i].ToDecimal() == 0)
            {
                return true;
            }
            return false;
        }

        #endregion

        #region 5 Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region 6 No Work

        public void GetPortfolios() { }

        public void Subscribe(Security security) { }

        public void SendOrder(Order order) { }

        public bool CancelOrder(Order order){ return true; }

        public void CancelAllOrdersToSecurity(Security security) { }

        public void CancelAllOrders() { }

        public void GetAllActivOrders() { }

        public OrderStateType GetOrderStatus(Order order) 
        {
            return OrderStateType.None;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice) { }

        public bool SubscribeNews()
        {
            return false;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        public void SetLeverage(Security security, decimal leverage) { }

        public event Action<News> NewsEvent { add { } remove { } }

        public event Action<Order> MyOrderEvent { add { } remove { } }

        public event Action<MyTrade> MyTradeEvent { add { } remove { } }

        public event Action<MarketDepth> MarketDepthEvent { add { } remove { } }

        public event Action<Trade> NewTradesEvent { add { } remove { } }

        public event Action<List<Portfolio>> PortfolioEvent { add { } remove { } }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion
    }
}