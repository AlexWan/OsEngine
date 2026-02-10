/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;


namespace OsEngine.Market.Servers.QscalpMarketDepth
{
    internal class QscalpMarketDepthServer : AServer
    {
        public QscalpMarketDepthServer()
        {
            QscalpMarketDepthServerRealization realization = new QscalpMarketDepthServerRealization();
            ServerRealization = realization;
            NeedToHideParameters = true;
        }
    }

    public class QscalpMarketDepthServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public QscalpMarketDepthServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            if (!Directory.Exists(_tempDirectory))
            {
                Directory.CreateDirectory(_tempDirectory);
            }
        }

        public void Connect(WebProxy proxy)
        {
            RestRequest request = new(Method.GET);

            RestClient client = new("https://erinrv.qscalp.ru/");

            request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");

            IRestResponse response = client.Execute(request);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                _availableDates = ExtractDatesFromHtml(response.Content);

                if (_availableDates.Count > 0)
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();
                }
            }
            else
            {
                SendLogMessage($"Connect server error: {response.StatusCode}", LogMessageType.Error);
            }
        }

        private List<DateTime> ExtractDatesFromHtml(string htmlContent)
        {
            List<DateTime> dates = [];

            MatchCollection matches = Regex.Matches(htmlContent, @"\b\d{4}-\d{2}-\d{2}\b");

            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];

                DateTime date = Convert.ToDateTime(match.Value, CultureInfo.InvariantCulture);

                if (!dates.Contains(date))
                    dates.Add(date);
            }

            return dates;
        }

        public void Dispose()
        {
            string[] files = Directory.GetFiles(_tempDirectory);

            if (files.Length > 0)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    File.Delete(files[i]);
                }
            }

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.QscalpMarketDepth; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        public bool IsCompletelyDeleted { get; set; }

        #endregion

        #region 2 Properties

        public DateTime ServerTime { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        private List<DateTime> _availableDates;

        private string _tempDirectory = @"Data\Temp\QscalpMarketDepthTempFiles\";

        private string _mainFtpUrl = "https://erinrv.qscalp.ru/";

        #endregion

        #region 3 Securities

        List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            SendLogMessage("Downloading the list of securities...", LogMessageType.System);

            (HashSet<string> uniqueSecurities, DateTime fileCreateDate) = GetSecuritiesFromFile();

            if (uniqueSecurities != null && uniqueSecurities.Count > 0 && fileCreateDate != DateTime.MinValue)
            {
                List<DateTime> newDates = _availableDates.FindAll(p => p > fileCreateDate);

                if (newDates.Count > 0)
                {
                    AddNewSecurities(newDates, uniqueSecurities);
                }
            }

            if (uniqueSecurities != null && uniqueSecurities.Count > 0)
            {
                CreateSecurities(uniqueSecurities);
            }

            if (_securities.Count > 0)
            {
                SendLogMessage($"{_securities.Count} securities loaded", LogMessageType.System);
            }

            SecurityEvent?.Invoke(_securities);
        }

        private void AddNewSecurities(List<DateTime> dates, HashSet<string> uniqueSecurities)
        {
            for (int i = 0; i < dates.Count; i++)
            {
                string secOnDate = GetStringRequest(_mainFtpUrl + dates[i].ToString("yyyy-MM-dd") + "/");

                if (secOnDate != null)
                {
                    List<string> secWithMD = GetSecuritiesFromResponse(secOnDate);

                    if (secWithMD.Count > 0)
                    {
                        for (int j = 0; j < secWithMD.Count; j++)
                        {
                            if (!uniqueSecurities.Contains(secWithMD[j]))
                                uniqueSecurities.Add(secWithMD[j]);
                        }
                    }
                }
            }
        }

        private (HashSet<string> uniqueSecurities, DateTime fileCreateDate) GetSecuritiesFromFile()
        {
            try
            {
                if (!File.Exists(@"QscalpMDSecurities.txt"))
                {
                    SendLogMessage($"QscalpMDSecurities.txt file with securities not found.", LogMessageType.Error);
                    return (null, DateTime.MinValue);
                }

                HashSet<string> uniqueSecurities = [];

                string[] secStr = File.ReadAllLines(@"QscalpMDSecurities.txt");

                string[] securities = secStr[0].Split('%');

                DateTime fileDate = Convert.ToDateTime(secStr[1], CultureInfo.InvariantCulture);

                for (int i = 0; i < securities.Length; i++)
                {
                    uniqueSecurities.Add(securities[i]);
                }

                return (uniqueSecurities, fileDate);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error of receiving securities. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return (null, DateTime.MinValue);
            }
        }

        private List<string> GetSecuritiesFromResponse(string htmlContent)
        {
            List<string> securities = new List<string>();

            string[] parts = htmlContent.Split(new string[] { "<A HREF=" }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];

                if (part.Contains("Quotes.qsh") && part.Contains(".qsh"))
                {
                    int startQuote = part.IndexOf('>');
                    int endQuote = part.IndexOf("</A>", startQuote);

                    if (startQuote > 0 && endQuote > startQuote)
                    {
                        string fileName = part.Substring(startQuote + 1, endQuote - startQuote - 1);

                        if (fileName.EndsWith(".Quotes.qsh"))
                        {
                            string withoutExtension = fileName.Substring(0, fileName.Length - 11);

                            int lastDotIndex = withoutExtension.LastIndexOf('.');
                            if (lastDotIndex > 0)
                            {
                                string secName = withoutExtension.Substring(0, lastDotIndex);
                                securities.Add(secName);
                            }
                        }
                    }
                }
            }

            return securities;
        }

        private void CreateSecurities(HashSet<string> uniqueSecurities)
        {
            try
            {
                HashSet<string>.Enumerator secList = uniqueSecurities.GetEnumerator();

                while (secList.MoveNext())
                {
                    Security security = new Security();

                    security.NameFull = secList.Current;
                    security.Name = secList.Current;
                    security.NameId = secList.Current;

                    if (secList.Current.Contains("TOD") || secList.Current.Contains("TOM"))
                    {
                        security.SecurityType = SecurityType.CurrencyPair;
                        security.NameClass = "Currency";
                    }
                    else if (Regex.IsMatch(secList.Current, @"\d"))
                    {
                        security.SecurityType = SecurityType.Futures;
                        security.NameClass = "Futures";
                    }
                    else
                    {
                        security.SecurityType = SecurityType.Fund;
                        security.NameClass = "Stocks";
                    }

                    security.PriceStep = 0;
                    security.PriceStepCost = 0;
                    security.State = SecurityStateType.UnKnown;
                    security.Exchange = "MOEX";
                    security.Lot = 1;

                    _securities.Add(security);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities convert error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            Portfolio newPortfolio = new Portfolio();
            newPortfolio.Number = "QscalpMarketDepth Virtual Portfolio";
            newPortfolio.ValueCurrent = 1;
            _myPortfolios.Add(newPortfolio);

            if (_myPortfolios.Count != 0)
            {
                PortfolioEvent(_myPortfolios);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;
        #endregion

        #region 5 Data

        public List<string> GetQshHistoryFileToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return null;
                }

                if (actualTime > endTime)
                {
                    return null;
                }

                List<string> qshFilesPaths = [];

                DateTime firstArchiveDate = Convert.ToDateTime(_availableDates[0], CultureInfo.InvariantCulture);

                if (startTime.Date < firstArchiveDate)
                {
                    SendLogMessage($"The data starts from  {firstArchiveDate.Date}", LogMessageType.System);
                    startTime = firstArchiveDate;
                }

                List<DateTime> dates = GetDateRangeInclusive(startTime, endTime);

                if (dates.Count == 0)
                    return null;

                for (int i = 0; i < dates.Count; i++)
                {
                    string dayStr = dates[i].ToString("yyyy-MM-dd");

                    string secOnDate = GetStringRequest(_mainFtpUrl + dayStr + "/");

                    if (secOnDate != null)
                    {
                        string link = GetLinkQshFile(security.Name, secOnDate);

                        if (link != null)
                        {
                            string qshFilePath = DownloadQshFile(_mainFtpUrl + dayStr + "/" + link);

                            if (qshFilePath != null)
                                qshFilesPaths.Add(qshFilePath);
                        }
                    }
                }

                return qshFilesPaths;
            }
            catch (Exception error)
            {
                SendLogMessage($"Candles data downloading error: {error}", LogMessageType.Error);
                return null;
            }
        }

        private List<DateTime> GetDateRangeInclusive(DateTime startTime, DateTime endTime)
        {
            List<DateTime> dates = new List<DateTime>();

            for (int i = 0; i < _availableDates.Count; i++)
            {
                if (_availableDates[i] >= startTime && _availableDates[i] <= endTime)
                    dates.Add(_availableDates[i]);
            }

            return dates;
        }

        private static string GetLinkQshFile(string instrumentName, string htmlContent)
        {
            if (htmlContent.IndexOf(instrumentName, StringComparison.Ordinal) == -1)
            {
                return null;
            }

            string[] parts = htmlContent.Split(new string[] { "<A HREF=" }, StringSplitOptions.RemoveEmptyEntries);

            string quotesPattern = instrumentName + ".20";

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];

                if (part.IndexOf(quotesPattern, StringComparison.Ordinal) != -1 &&
                    part.Contains("Quotes.qsh"))
                {
                    int startQuote = part.IndexOf('>');
                    int endQuote = part.IndexOf("</A>", startQuote);

                    if (startQuote > 0 && endQuote > startQuote)
                    {
                        string fileName = part.Substring(startQuote + 1, endQuote - startQuote - 1);

                        return fileName;
                    }
                }
            }

            return null;
        }

        #endregion

        #region Queries

        private RateGate _rateGateData = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private string GetStringRequest(string url)
        {
            _rateGateData.WaitToProceed();

            try
            {
                RestRequest request = new(Method.GET);
                RestClient client = new(url);

                request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return response.Content;
                }
                else
                {
                    SendLogMessage($"Error response: {response.StatusCode}-{response.Content}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);

                return null;
            }
        }

        private string DownloadQshFile(string path)
        {
            _rateGateData.WaitToProceed();

            try
            {
                string fileName = Path.GetFileName(path);
                string tempFilePath = Path.Combine(_tempDirectory, fileName);

                RestRequest request = new RestRequest(Method.GET);
                RestClient client = new RestClient(path);

                request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                request.AddHeader("Accept-Encoding", "gzip, deflate, br, zstd");
                request.AddHeader("Accept-Language", "ru,en;q=0.9,de;q=0.8,kk;q=0.7,fr;q=0.6,zh;q=0.5");
                request.AddHeader("Sec-Ch-Ua", "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"YaBrowser\";v=\"25.10\", \"Yowser\";v=\"2.5\"");
                request.AddHeader("Sec-Ch-Ua-Mobile", "?0");
                request.AddHeader("Sec-Ch-Ua-Platform", "\"Windows\"");
                request.AddHeader("Sec-Fetch-Dest", "document");
                request.AddHeader("Sec-Fetch-Mode", "navigate");
                request.AddHeader("Sec-Fetch-Site", "same-origin");
                request.AddHeader("Sec-Fetch-User", "?1");
                request.AddHeader("Upgrade-Insecure-Requests", "1");
                request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 YaBrowser/25.10.0.0 Safari/537.36");

                Uri uri = new Uri(path);
                string referer = uri.GetLeftPart(UriPartial.Authority) + "/" + uri.Segments[1];
                request.AddHeader("Referer", referer);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK && response.RawBytes != null && response.RawBytes.Length > 0)
                {
                    using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fileStream.Write(response.RawBytes, 0, response.RawBytes.Length);
                    }
                }
                else
                {
                    SendLogMessage($"Сouldn't upload qsh file\n. Http status: {response.StatusCode} - {response.ErrorMessage}", LogMessageType.Error);
                    return null;
                }

                return tempFilePath;
            }
            catch (Exception ex)
            {
                SendLogMessage("Сouldn't upload qsh file.\n" + ex, LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 6 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;
        #endregion

        #region 7 Unused methods

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

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime) { return null; }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime) { return null; }

        public event Action<News> NewsEvent { add { } remove { } }
        public event Action<MarketDepth> MarketDepthEvent { add { } remove { } }
        public event Action<Trade> NewTradesEvent { add { } remove { } }
        public event Action<Order> MyOrderEvent { add { } remove { } }
        public event Action<MyTrade> MyTradeEvent { add { } remove { } }
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }
        public event Action<Funding> FundingUpdateEvent { add { } remove { } }
        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        public void SetLeverage(string securityName, string className, string leverage, string leverageLong, string leverageShort) { }

        public void SetHedgeMode(string securityName, string className, string hedgeMode) { }

        public void SetMarginMode(string securityName, string className, string marginMode) { }

        public void SetCommonLeverage(string selectedClass, string leverage) { }

        public void SetCommonHedgeMode(string selectedClass, string hedgeMode) { }

        public void SetCommonMarginMode(string selectedClass, string marginMode) { }

        #endregion

    }
}
