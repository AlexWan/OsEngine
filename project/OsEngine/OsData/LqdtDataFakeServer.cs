/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Xml;


namespace OsEngine.OsData
{
    public class LqdtDataFakeServer : IServer
    {
        public LqdtDataFakeServer(string exchange)
        {
            _exchange = exchange;
        }

        #region 1 Properties

        private readonly string _exchange;

        private List<(DateTime Date, double Rate)> _rates;

        public bool IsRatesDownloaded { get; set; }

        private decimal _lastPricePrePie;

        private Candle _lastCandlePreRie;

        private TimeFrame _lastTf;

        #endregion

        #region 2 Connection

        public void StartServer()
        {
            _rates = new List<(DateTime Date, double Rate)>();

            List<(DateTime Date, double Rate)> oldRates = GetRatesFromFile();

            if (oldRates != null)
                _rates.AddRange(oldRates);
            else
            {
                IsRatesDownloaded = false;
                return;
            }

            List<(DateTime Date, double Rate)> rates = null;

            if (_exchange.Equals("MOEX"))
            {
                rates = GetCbrKeyRates(_rates[0].Date.AddDays(1), DateTime.Now);
            }
            else
            {
                rates = GetFrsNewKeyRates();
            }

            if (rates != null)
                _rates.InsertRange(0, rates);
            else
            {
                IsRatesDownloaded = false;
                return;
            }

            IsRatesDownloaded = true;
        }

        /// <summary>
        /// Загрузка ставок ФРС с сайта https://ru.investing.com/economic-calendar/interest-rate-decision-168
        /// </summary>
        private List<(DateTime Date, double Rate)> GetFrsNewKeyRates()
        {
            try
            {
                HttpClient HttpClient = new HttpClient();

                string url = "https://sbcharts.investing.com/events_charts/eu/168.json";

                HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");

                var response = HttpClient.GetAsync(url).GetAwaiter().GetResult();

                var cont = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    FedRates fedRates = JsonConvert.DeserializeObject<FedRates>(cont);

                    if (fedRates.attr != null && fedRates.attr.Length > 0)
                    {
                        List<(DateTime Date, double Rate)> rates = new List<(DateTime Date, double Rate)>();

                        (DateTime Date, double Rate) lastRate = (DateTime.Now, 0);

                        for (int i = fedRates.attr.Length - 1; i >= 0; i--)
                        {
                            DateTime dateRate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(fedRates.attr[i].timestamp)).Date;
                            double rate = fedRates.attr[i].actual_formatted.TrimEnd('%').ToDouble();

                            if (dateRate == _rates[0].Date)
                                break;

                            if (lastRate.Rate == 0)
                            {
                                lastRate = (dateRate, rate);

                                if (Math.Abs(rate - _rates[0].Rate) > 0.001)
                                {
                                    rates.Add(lastRate);
                                }
                            }
                            else
                            {
                                if (Math.Abs(rate - lastRate.Rate) > 0.001)
                                {
                                    rates.Add(lastRate);
                                }
                            }

                            lastRate = (dateRate, rate);
                        }

                        return rates;
                    }
                    else
                    {
                        SendNewLogMessage("Ошибка загрузки ставок ФРС", LogMessageType.Error);
                        return null;
                    }
                }
                else
                {
                    SendNewLogMessage($"Error response: {response.StatusCode}-{response.Content}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка загрузки ставок ФРС\n{ex.Message}", LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// Загрузка из файла процентных ставок. В файле: Строка 1 - ставки ЦБ РФ, Строка 2 - ставки ФРС США 
        /// </summary>
        /// <returns>Список(дата принятия, значение %)</returns>
        private List<(DateTime Date, double Rate)> GetRatesFromFile()
        {
            List<(DateTime Date, double Rate)> cbrRates = new List<(DateTime Date, double Rate)>();

            if (!File.Exists(@"KeyRates.txt"))
            {
                SendNewLogMessage("Файл KeyRates.txt отсутствует", LogMessageType.Error);
                return null;
            }

            try
            {
                string[] lines = File.ReadAllLines(@"KeyRates.txt");

                string rates = _exchange.Equals("MOEX") ? lines[0] : lines[2];

                lines = rates.Split('+');

                for (int i = 0; i < lines.Length; i++)
                {
                    string[] dateAndKey = lines[i].Split('-', StringSplitOptions.RemoveEmptyEntries);

                    if (DateTime.TryParse(dateAndKey[0], out DateTime date))
                    {
                        cbrRates.Add((date, dateAndKey[1].ToDouble()));
                    }
                }

                return cbrRates;
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка загрузки ставок ЦБ до 2013 года.\n: {ex.Message}", LogMessageType.Error);
                return null;
            }
        }

        // загрузить данные по ставке ЦБ
        private List<(DateTime Date, double Rate)> GetCbrKeyRates(DateTime fromDate, DateTime toDate)
        {
            RestClient client = new RestClient("https://www.cbr.ru");

            // SOAP-запрос
            string soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                    <soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
                     xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
                     xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
                      <soap12:Body>
                        <KeyRateXML xmlns=""http://web.cbr.ru/"">
                          <fromDate>{fromDate:yyyy-MM-dd}</fromDate>
                          <ToDate>{toDate:yyyy-MM-dd}</ToDate>
                        </KeyRateXML>
                      </soap12:Body>
                    </soap12:Envelope>";

            try
            {
                RestRequest request = new RestRequest("/DailyInfoWebServ/DailyInfo.asmx", Method.POST);

                request.AddHeader("Content-Type", "application/soap+xml; charset=utf-8");
                request.AddHeader("SOAPAction", "http://web.cbr.ru/KeyRateXML");
                request.AddParameter("application/soap+xml; charset=utf-8", soapRequest, ParameterType.RequestBody);

                IRestResponse response = client.Execute(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string soapResponse = response.Content;

                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(soapResponse);

                    XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
                    nsmgr.AddNamespace("soap", "http://www.w3.org/2003/05/soap-envelope");
                    nsmgr.AddNamespace("ns", "http://web.cbr.ru/");

                    XmlNode resultNode = xmlDoc.SelectSingleNode("//soap:Body/ns:KeyRateXMLResponse/ns:KeyRateXMLResult", nsmgr);

                    if (resultNode == null)
                    {
                        SendNewLogMessage("Получен некорректный ответ на запрос о ключевой ставке ЦБР", LogMessageType.Error);
                        return null;
                    }

                    XmlDocument dataDoc = new XmlDocument();

                    string xmlData = resultNode.InnerXml;

                    if (!xmlData.StartsWith("<KeyRate>"))
                    {
                        xmlData = "<KeyRate>" + xmlData + "</KeyRate>";
                    }

                    dataDoc.LoadXml(xmlData);

                    XmlNodeList keyRateNodes = dataDoc.SelectNodes("//KR");

                    if (keyRateNodes == null || keyRateNodes.Count == 0)
                    {
                        SendNewLogMessage("За указанный период данные о ключевой ставке не найдены.", LogMessageType.Error);
                        return null;
                    }

                    List<(DateTime Date, double Rate)> rates = new List<(DateTime Date, double Rate)>();

                    (DateTime Date, double Rate) lastRate = (DateTime.Now, 0);

                    for (int i = 0; i < keyRateNodes.Count; i++)
                    {
                        string dateStr = keyRateNodes[i].SelectSingleNode("DT")?.InnerText;
                        string rateStr = keyRateNodes[i].SelectSingleNode("Rate")?.InnerText;

                        if (!string.IsNullOrEmpty(dateStr) && !string.IsNullOrEmpty(rateStr))
                        {
                            DateTime dateRate = DateTime.Parse(dateStr);
                            double rate = rateStr.ToDouble();

                            if (lastRate.Rate == 0)
                            {
                                lastRate = (dateRate, rate);

                            }
                            else
                            {
                                if (Math.Abs(rate - lastRate.Rate) > 0.001)
                                {
                                    rates.Add(lastRate);
                                }
                            }

                            lastRate = (dateRate, rate);
                        }
                    }
                    return rates;
                }
                else
                {
                    SendNewLogMessage($"Error response: {response.StatusCode}-{response.Content}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage($"Ошибка загрузки ставок с сайта ЦБ РФ\n{ex.Message}", LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 3 Data

        public List<Candle> GetCandleDataToSecurity(string securityName, string securityClass, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime, bool needToUpdate)
        {
            if (_rates == null || _rates.Count == 0 || !IsRatesDownloaded)
                return null;

            if (actualTime > endTime)
            {
                return null;
            }

            List<Candle> candles = new List<Candle>();

            DateTime currentCandleStart = startTime;

            decimal currentPrice = 1.0m;

            if (timeFrameBuilder.TimeFrame != _lastTf)
                _lastCandlePreRie = null;

            if (_lastCandlePreRie != null)
            {
                currentCandleStart = _lastCandlePreRie.TimeStart + timeFrameBuilder.TimeFrameTimeSpan;

                currentPrice = _lastCandlePreRie.Close;

                if (currentCandleStart.Date == _lastCandlePreRie.TimeStart.Date.AddDays(1) && currentCandleStart.DayOfWeek != DayOfWeek.Sunday)
                {
                    double currRate = GetRateForDate(currentCandleStart);

                    currentPrice = Math.Round(currentPrice + currentPrice * ((decimal)currRate / 100m / 365m), 5);
                }
            }

            while (currentCandleStart < endTime)
            {
                Candle candle = new Candle();

                candle.TimeStart = currentCandleStart;
                candle.Open = currentPrice;
                candle.High = currentPrice;
                candle.Low = currentPrice;
                candle.Close = currentPrice;
                candle.Volume = 0;
                candle.State = CandleState.Finished;

                candles.Add(candle);

                currentCandleStart = currentCandleStart + timeFrameBuilder.TimeFrameTimeSpan;

                if (currentCandleStart.Date == candle.TimeStart.Date.AddDays(1) && currentCandleStart.DayOfWeek != DayOfWeek.Sunday)
                {
                    double currRate = GetRateForDate(currentCandleStart);

                    currentPrice = Math.Round(currentPrice + currentPrice * ((decimal)currRate / 100m / 365m), 5);
                }
            }

            // записать последнюю свечку предыдущего куска
            _lastCandlePreRie = candles[^1];
            _lastTf = timeFrameBuilder.TimeFrame;

            return candles;
        }

        public List<Trade> GetTickDataToSecurity(string securityName, string securityClass, DateTime startTime, DateTime endTime, DateTime actualTime, bool needToUpdete)
        {
            if (_rates == null || _rates.Count == 0 || !IsRatesDownloaded)
                return null;

            if (actualTime > endTime)
            {
                return null;
            }

            List<Trade> trades = new List<Trade>();

            DateTime currentTradeTime = startTime;

            TimeSpan tradeTf = TimeSpan.FromSeconds(1);

            decimal currentPrice = _lastPricePrePie > 0 ? _lastPricePrePie : 1.0m;

            while (currentTradeTime < endTime)
            {
                Trade trade = new Trade();

                string newId = Guid.NewGuid().ToString().Split('-')[^1];

                trade.SecurityNameCode = securityName;
                trade.Id = newId;
                trade.Time = currentTradeTime;
                trade.Price = currentPrice;
                trade.Side = Side.None;
                trade.Volume = 0;

                trades.Add(trade);

                currentTradeTime = currentTradeTime + tradeTf;

                if (currentTradeTime.Date == trade.Time.Date.AddDays(1) && currentTradeTime.DayOfWeek != DayOfWeek.Sunday)
                {
                    double currRate = GetRateForDate(currentTradeTime);

                    currentPrice = Math.Round(currentPrice + currentPrice * ((decimal)currRate / 100m / 365m), 5);
                }
            }

            _lastPricePrePie = currentPrice;

            return trades;
        }

        private double GetRateForDate(DateTime date)
        {
            DateTime latestRateDate = DateTime.MinValue;
            double latestRate = 0;

            for (int i = 0; i < _rates.Count; i++)
            {
                (DateTime Date, double Rate) rate = _rates[i];

                if (rate.Date <= date && rate.Date > latestRateDate)
                {
                    latestRateDate = rate.Date;
                    latestRate = rate.Rate;
                    break;
                }
            }

            return latestRate;
        }

        #endregion

        #region 4 Logging

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

        #endregion

        #region 5 Unused interface members

        public ServerType ServerType { get; set; }

        public string ServerNameAndPrefix { get; set; }

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        public DateTime LastStartServerTime { get; set; }

        public List<Portfolio> Portfolios { get; set; }

        public List<Security> Securities { get; set; }

        public List<Trade>[] AllTrades { get; set; }

        public List<MyTrade> MyTrades { get; set; }

        public void ShowDialog(int num = 0) { }

        public void StopServer() { }

        public Portfolio GetPortfolioForName(string name) { return null; }

        public Security GetSecurityForName(string securityName, string securityClass) { return null; }

        public CandleSeries StartThisSecurity(string securityName, TimeFrameBuilder timeFrameBuilder, string securityClass) { return null; }

        public void StopThisSecurity(CandleSeries series) { }

        public List<Trade> GetAllTradesToSecurity(Security security) { return null; }

        public void ExecuteOrder(Order order) { }

        public void Subscribe(Security security) { }

        public void SendOrder(Order order) { }

        public void CancelAllOrders() { }

        public void CancelAllOrdersToSecurity(Security security) { }

        public void ChangeOrderPrice(Order order, decimal newPrice) { }

        public void GetAllActivOrders() { }

        public OrderStateType GetOrderStatus(Order order) { return OrderStateType.None; }

        public bool SubscribeNews() { return false; }

        public List<Order> GetActiveOrders(int startIndex, int count) { return null; }

        public List<Order> GetHistoricalOrders(int startIndex, int count) { return null; }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount) { return null; }

        void IServer.CancelOrder(Order order) { }

        public event Action<string, LogMessageType> NewLogMessageEvent;
        public event Action<string> ConnectStatusChangeEvent;
        public event Action NeedToReconnectEvent;
        public event Action<DateTime> TimeServerChangeEvent;
        public event Action<List<Portfolio>> PortfoliosChangeEvent;
        public event Action<List<Security>> SecuritiesChangeEvent;
        public event Action<News> NewsEvent;
        public event Action<CandleSeries> NewCandleIncomeEvent;
        public event Action<decimal, decimal, Security> NewBidAskIncomeEvent;
        public event Action<MarketDepth> NewMarketDepthEvent;
        public event Action<List<Trade>> NewTradeEvent;
        public event Action<OptionMarketData> NewAdditionalMarketDataEvent;
        public event Action<Funding> NewFundingEvent;
        public event Action<SecurityVolumes> NewVolume24hUpdateEvent;
        public event Action<Order> NewOrderIncomeEvent;
        public event Action<MyTrade> NewMyTradeEvent;
        public event Action<Order> CancelOrderFailEvent;
        public event Action<string, LogMessageType> LogMessageEvent;
        #endregion
    }

    #region 6 Entity
    public class FedRates
    {
        public string[][] data { get; set; }
        public Attr[] attr { get; set; }
    }

    public class Attr
    {
        public string timestamp { get; set; }
        public string actual_state { get; set; }
        public string actual { get; set; }
        public string actual_formatted { get; set; }
        public string forecast { get; set; }
        public string forecast_formatted { get; set; }
        public string revised { get; set; }
        public string revised_formatted { get; set; }
    }
    #endregion
}
