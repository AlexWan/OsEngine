using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Polygon.Entity;
using OsEngine.Market.Servers.Entity;
using System.IO;
using System.Net.Http;
using System.Net;

namespace OsEngine.Market.Servers.Polygon
{
    public class PolygonServer : AServer
    {
        public PolygonServer()
        {
            PolygonServerRealization realization = new PolygonServerRealization();
            ServerRealization = realization;
            CreateParameterString("Api key", "");
            CreateParameterBoolean("Load tickers from server", false);
            CreateParameterEnum("Tickers market", "All", new List<string> { "All", "stocks", "crypto", "fx", "otc", "indices" });
            CreateParameterBoolean("Whether or not the results are adjusted for splits", true);
        }
    }

    public class PolygonServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public PolygonServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        public ServerType ServerType
        {
            get { return ServerType.Polygon; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy)
        {
            try
            {
                _apiKey = ((ServerParameterString)ServerParameters[0]).Value;
                _loadTickers = ((ServerParameterBool)ServerParameters[1]).Value;

                if (((ServerParameterEnum)ServerParameters[2]).Value == "All")
                {
                    _tickersMarket = "";
                }
                else
                {
                    _tickersMarket = $"&market={((ServerParameterEnum)ServerParameters[2]).Value}";
                }

                if (((ServerParameterBool)ServerParameters[3]).Value == true)
                {
                    _adjusted = "true";
                }
                else
                {
                    _adjusted = "false";
                }
                
                if (!CheckApiKey())
                {
                    return;
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
                SendLogMessage($"Connect: Error connect - {exception.Message}", LogMessageType.Error);

                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();

                return;
            }                        
        }

        private bool CheckApiKey()
        {
            try
            {               
                _rateGateFreePlan.WaitToProceed();                

                HttpResponseMessage responseMessage = _httpClient.GetAsync(_baseUrl + $"/v3/trades/AAPL?limit=10&apiKey={_apiKey}").Result;
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                RestResponceMessage<ResponceTrades> response = JsonConvert.DeserializeObject<RestResponceMessage<ResponceTrades>>(json);

                if (response.status == "NOT_AUTHORIZED")
                {
                    _freePlan = true;

                    if (_freePlan)
                    {
                        _rateGateFreePlan.WaitToProceed();
                    }

                    responseMessage = _httpClient.GetAsync(_baseUrl + $"/v3/reference/tickers?active=true&limit=10&apiKey={_apiKey}").Result;
                    json = responseMessage.Content.ReadAsStringAsync().Result;

                    RestResponceMessage<Tickers> responsTickers = JsonConvert.DeserializeObject<RestResponceMessage<Tickers>>(json);

                    if (responsTickers.status != "OK")
                    {
                        SendLogMessage($"CheckApiKey: Not Authorized - missing or incorrect API Key", LogMessageType.Error);

                        return false;
                    }
                    SendLogMessage($"Limited API requests", LogMessageType.System);
                }
                else if (response.status == "OK")
                {
                    _freePlan = false;
                    SendLogMessage($"Unlimited API requests", LogMessageType.System);
                }
                else
                {
                    SendLogMessage($"CheckApiKey: Error connect", LogMessageType.Error);
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                SendLogMessage($"CheckApiKey: Error connect - {exception.Message}", LogMessageType.Error);
                
                return false;
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

        public List<IServerParameter> ServerParameters { get; set; }

        public RateGate _rateGateFreePlan = new RateGate(1, TimeSpan.FromMilliseconds(12000));

        private string _apiKey;

        private bool _freePlan;

        private HttpClient _httpClient = new HttpClient();

        private string _baseUrl = "https://api.polygon.io";

        private bool _loadTickers;

        private string _adjusted;

        private string _tickersMarket;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            if (_loadTickers)
            {
                GetSecurityData();
            }

            try
            {
                if (_freePlan)
                {
                    _rateGateFreePlan.WaitToProceed();
                }

                HttpResponseMessage responseMessage = _httpClient.GetAsync(_baseUrl + $"/v3/reference/tickers/types?&apiKey={_apiKey}").Result;
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                RestResponceMessage<TickerType> responseTickerType = JsonConvert.DeserializeObject<RestResponceMessage<TickerType>>(json);

                if (_freePlan)
                {
                    _rateGateFreePlan.WaitToProceed();
                }

                responseMessage = _httpClient.GetAsync(_baseUrl + $"/v3/reference/exchanges?&apiKey={_apiKey}").Result;
                json = responseMessage.Content.ReadAsStringAsync().Result;

                RestResponceMessage<TickerExchange> responseTickerExchange = JsonConvert.DeserializeObject<RestResponceMessage<TickerExchange>>(json);

                List<Security> securities = new List<Security>();

                if (!File.Exists(@"Engine\PolygonSecurities.csv"))
                {
                    return;
                }

                List<string> list = new List<string>();

                using (StreamReader reader = new StreamReader(@"Engine\PolygonSecurities.csv"))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            string[] split = line.Split(',');
                           
                            Security security = new Security();
                            security.Name = split[0];
                            security.NameFull = split[1];
                            security.NameClass = GetClassSecurity(split[2], responseTickerType);
                            security.NameId = security.Name;
                            security.SecurityType = SecurityType.Stock;
                            security.Lot = 1;
                            security.PriceStep = 1;
                            security.Decimals = 0;
                            security.PriceStepCost = 1;
                            security.State = SecurityStateType.Activ;
                            security.Exchange = GetExchangeSecurity(split[3], responseTickerExchange);

                            securities.Add(security);                           
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

        private string GetExchangeSecurity(string str, RestResponceMessage<TickerExchange> responseTickerExchange)
        {
            for (int i = 0; i < responseTickerExchange.results.Count; i++)
            {
                if (str == responseTickerExchange.results[i].mic)
                {
                    return responseTickerExchange.results[i].name;
                }
            }

            return null;
        }

        private string GetClassSecurity(string str, RestResponceMessage<TickerType> responseTickerType)
        {
            for (int i = 0; i < responseTickerType.results.Count; i++)
            {
                if (str == responseTickerType.results[i].code)
                {
                    return responseTickerType.results[i].description;
                }
            }

            return null;
        }

        private void GetSecurityData()
        {
            try
            {
                if (_freePlan)
                {
                    _rateGateFreePlan.WaitToProceed();
                }

                HttpResponseMessage responseMessage = _httpClient.GetAsync(_baseUrl + $"/v3/reference/tickers?active=true&limit=1000&apiKey={_apiKey}" + _tickersMarket).Result;
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                RestResponceMessage<Tickers> response = JsonConvert.DeserializeObject<RestResponceMessage<Tickers>>(json);

                SaveSecurityToFile(response, false);

                while (response.next_url != null)
                {
                    if (_freePlan)
                    {
                        _rateGateFreePlan.WaitToProceed();
                    }

                    HttpResponseMessage responseMessageNextUrl = _httpClient.GetAsync($"{response.next_url}&apiKey={_apiKey}").Result;
                    string jsonNextUrl = responseMessageNextUrl.Content.ReadAsStringAsync().Result;

                    response = JsonConvert.DeserializeObject<RestResponceMessage<Tickers>>(jsonNextUrl);

                    SaveSecurityToFile(response, true);
                }

                SendLogMessage($"Writing to file is finished.", LogMessageType.System);
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
        }

        private void SaveSecurityToFile(RestResponceMessage<Tickers> response, bool resave)
        {
            try
            {                
                using (StreamWriter writer = new StreamWriter(@"Engine\PolygonSecurities.csv", resave))
                {                    
                    for (int i = 0; i < response.results.Count; i++)
                    {
                        string saveString = response.results[i].ticker + ",";
                        saveString += response.results[i].name + ",";
                        saveString += response.results[i].type + ",";
                        saveString += response.results[i].primary_exchange;

                        writer.WriteLine(saveString);

                    }
                    SendLogMessage($"Write to file {response.results.Count} tickers", LogMessageType.System);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"SaveSecurityToFile: {ex.Message}", LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion
                
        #region 4 Data

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime lastDate)
        {
            if (_freePlan)
            {
                SendLogMessage("It is not possible to download trades on the free plan", LogMessageType.System);
                return null;
            }

            if (lastDate > startTime ||
                startTime > endTime)
            {
                return null;
            }

            List<Trade> trades = new List<Trade>();

            long timeStampFrom = TimeManager.GetTimeStampMilliSecondsToDateTime(startTime) * 1000000;
            long timeStampTo = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime) * 1000000;

            string strUrl = $"/v3/trades/{security.Name}?order=asc&sort=timestamp&limit=50000&timestamp.gte={timeStampFrom}&timestamp.lte={timeStampTo}&apiKey={_apiKey}";

            HttpResponseMessage responseMessage = _httpClient.GetAsync(_baseUrl + strUrl).Result;
            string json = responseMessage.Content.ReadAsStringAsync().Result;

            RestResponceMessage<ResponceTrades> response = JsonConvert.DeserializeObject<RestResponceMessage<ResponceTrades>>(json);

            List<Trade> trade = ConvertTrades(security.Name, response);

            if (trade == null)
            {
                return null;
            }

            trades.AddRange(trade);

            while (response.next_url != null)
            {
                HttpResponseMessage responseMessageNextUrl = _httpClient.GetAsync($"{response.next_url}&apiKey={_apiKey}").Result;
                string jsonNextUrl = responseMessageNextUrl.Content.ReadAsStringAsync().Result;

                response = JsonConvert.DeserializeObject<RestResponceMessage<ResponceTrades>>(jsonNextUrl);

                trade = ConvertTrades(security.Name, response);

                if (trade == null)
                {
                    break;
                }

                trades.AddRange(trade);
            }

            if (trades.Count == 0)
            {
                return null;
            }

            return trades;
        }

        private List<Trade> ConvertTrades(string security, RestResponceMessage<ResponceTrades> response)
        {
            List<Trade> trades = new List<Trade>();

            for (int i = 0; i < response.results.Count; i++)
            {
                if (response.results[i].conditions != null) 
                {
                    if (!response.results[i].conditions.Contains("12"))
                    {
                        continue;
                    }
                    else
                    {
                        if (response.results[i].conditions.Contains("37") ||
                            response.results[i].conditions.Contains("2"))
                        {
                            continue;
                        }
                    }
                }                

                Trade trade = new Trade();

                decimal.TryParse(response.results[i].sip_timestamp, out decimal timestamp);

                trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(Math.Floor(timestamp / 1000000)));
                trade.MicroSeconds = Convert.ToInt32(((timestamp / 1000000) % 1) * 1000000);
                trade.Price = response.results[i].price.ToDecimal();                
                trade.Id = response.results[i].sequence_number;
                trade.Volume = response.results[i].size.ToDecimal();
                trade.SecurityNameCode = security;

                trades.Add(trade);
            }

            return trades;
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

                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

                string fromData = startTime.ToString("yyyy-MM-dd");
                string toData = endTime.ToString("yyyy-MM-dd");

                List<Candle> allCandles = new List<Candle>();

                if (_freePlan)
                {
                    _rateGateFreePlan.WaitToProceed();
                }

                string strUrl = $"/v2/aggs/ticker/{security.Name}/range/{interval}/{fromData}/{toData}?adjusted={_adjusted}&sort=asc&limit=50000&apiKey={_apiKey}";

                HttpResponseMessage responseMessage = _httpClient.GetAsync(_baseUrl + strUrl).Result;
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                RestResponceMessage<ResponceCandles> response = JsonConvert.DeserializeObject<RestResponceMessage<ResponceCandles>>(json);

                if (response.status == "NOT_AUTHORIZED")
                {
                    SendLogMessage(response.message, LogMessageType.Error);
                    return null;
                }

                List<Candle> candles = ConvertCandles(response);

                if (candles == null)
                {
                    return null;
                }

                allCandles.AddRange(candles);

                while (response.next_url != null)
                {
                    if (_freePlan)
                    {
                        _rateGateFreePlan.WaitToProceed();
                    }

                    HttpResponseMessage responseMessageNextUrl = _httpClient.GetAsync($"{response.next_url}&apiKey={_apiKey}").Result;
                    string jsonNextUrl = responseMessageNextUrl.Content.ReadAsStringAsync().Result;

                    response = JsonConvert.DeserializeObject<RestResponceMessage<ResponceCandles>>(jsonNextUrl);

                    candles = ConvertCandles(response);

                    if (candles == null)
                    {
                        break;
                    }

                    allCandles.AddRange(candles);
                }

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

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
                startTime >= DateTime.Now ||
                actualTime > endTime ||
                actualTime > DateTime.Now)
            {
                return false;
            }
            return true;
        }

        private string GetInterval(TimeSpan timeFrame)
        {           
            if (timeFrame.TotalMinutes >= 1 &&
                timeFrame.TotalMinutes < 60)
            {
                return $"{timeFrame.TotalMinutes}/minute";
            }
            else if (timeFrame.TotalMinutes >= 60 &&
                timeFrame.TotalMinutes < 1440)
            {
                return $"{timeFrame.TotalHours}/hour";
            }
            else
            {
                return $"{timeFrame.Days}/day";
            }           
        }
                
        private List<Candle> ConvertCandles(RestResponceMessage<ResponceCandles> responce)
        {
            try
            {
                List<Candle> candles = new List<Candle>();

                if (responce.results == null)
                {
                    return null;
                }

                for (int i = 0; i < responce.results.Count; i++)
                {
                    if (CheckCandlesToZeroData(responce.results[i]))
                    {
                        continue;
                    }

                    Candle candle = new Candle();

                    candle.State = CandleState.Finished;
                    candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(long.Parse(responce.results[i].t));
                    candle.Volume = responce.results[i].v.ToDecimal();
                    candle.Close = responce.results[i].c.ToDecimal();
                    candle.High = responce.results[i].h.ToDecimal();
                    candle.Low = responce.results[i].l.ToDecimal();
                    candle.Open = responce.results[i].o.ToDecimal();

                    candles.Add(candle);
                }
                return candles;
            }
            catch (Exception ex)
            {
                SendLogMessage("ConvertCandles: " + ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private bool CheckCandlesToZeroData(ResponceCandles item)
        {
            if (item.c.ToDecimal() == 0 ||
                item.o.ToDecimal() == 0 ||
                item.h.ToDecimal() == 0 ||
                item.l.ToDecimal() == 0)
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

        public bool CancelOrder(Order order){ return false; }

        public void CancelAllOrdersToSecurity(Security security) { }

        public void CancelAllOrders() { }

        public void GetAllActivOrders() { }

        public OrderStateType GetOrderStatus(Order order) 
        {
            return OrderStateType.None;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice) { }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        public event Action<Order> MyOrderEvent { add { } remove { } }

        public event Action<MyTrade> MyTradeEvent { add { } remove { } }

        public event Action<MarketDepth> MarketDepthEvent { add { } remove { } }

        public event Action<Trade> NewTradesEvent { add { } remove { } }

        public event Action<List<Portfolio>> PortfolioEvent { add { } remove { } }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion
    }
}