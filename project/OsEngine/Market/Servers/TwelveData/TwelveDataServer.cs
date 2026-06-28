/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.TwelveData.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace OsEngine.Market.Servers.TwelveData
{
    public class TwelveDataServer : AServer
    {
        public TwelveDataServer()
        {
            TwelveDataServerRealization realization = new TwelveDataServerRealization();
            ServerRealization = realization;

            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterBoolean("Use stocks USA", true);
            CreateParameterBoolean("Use stocks India", false);
            CreateParameterBoolean("Use forex", false);
            CreateParameterBoolean("Use ETFs USA", false);
            CreateParameterBoolean("Use ETFs UK", false);
            CreateParameterBoolean("Use commodities", false);
            CreateParameterEnum("Time zone", "Exchange", new List<string> { "Exchange", "UTC", "Msc" });
        }
    }

    public class TwelveDataServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public TwelveDataServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            try
            {
                _mskTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
            }
            catch
            {
                _mskTimeZone = null;
            }

            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.Name = "TwelveDataMessageReaderPublic";
            threadMessageReaderPublic.IsBackground = true;
            threadMessageReaderPublic.Start();

            Thread threadCheckAlive = new Thread(CheckAliveWebSocket);
            threadCheckAlive.Name = "TwelveDataCheckAliveWebSocket";
            threadCheckAlive.IsBackground = true;
            threadCheckAlive.Start();
        }

        public void Connect(WebProxy proxy)
        {
            _myProxy = proxy;
            _apiKey = ((ServerParameterPassword)ServerParameters[0]).Value;
            _stocksUsa = ((ServerParameterBool)ServerParameters[1]).Value;
            _stocksIndia = ((ServerParameterBool)ServerParameters[2]).Value;
            _forex = ((ServerParameterBool)ServerParameters[3]).Value;
            _etfsUsa = ((ServerParameterBool)ServerParameters[4]).Value;
            _etfsLondon = ((ServerParameterBool)ServerParameters[5]).Value;
            _commodities = ((ServerParameterBool)ServerParameters[6]).Value;

            if (string.IsNullOrEmpty(_apiKey))
            {
                SendLogMessage("Can`t run TwelveData connector. No keys", LogMessageType.Error);
                return;
            }

            if (((ServerParameterEnum)ServerParameters[7]).Value == "Exchange")
            {
                _timezone = "Exchange";
            }
            else if (((ServerParameterEnum)ServerParameters[7]).Value == "UTC")
            {
                _timezone = "UTC";
            }
            else
            {
                _timezone = "Europe/Moscow";
            }

            try
            {
                string requestStr = $"/api_usage?apikey={_apiKey}";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ApiUsageResponse usageResponse = JsonConvert.DeserializeObject<ApiUsageResponse>(response.Content);

                    if (usageResponse != null)
                    {
                        if (usageResponse.plan_category != "basic")
                        {
                            int limit = 60000 / Convert.ToInt32(usageResponse.plan_limit);

                            _rateGateApiLimit = new RateGate(1, TimeSpan.FromMilliseconds(limit));
                        }

                        CreatePublicWebSocketConnect();

                        //if (ServerStatus == ServerConnectStatus.Disconnect)
                        //{
                        //    ServerStatus = ServerConnectStatus.Connect;

                        //    if (ConnectEvent != null)
                        //    {
                        //        ConnectEvent();
                        //    }
                        //}
                    }
                    else
                    {
                        SendLogMessage("Connection can be open. TwelveData. Error request", LogMessageType.Error);
                        Disconnect();
                    }
                }
                else
                {
                    SendLogMessage("Connection can be open. TwelveData. Error request", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage("Connection can be open. TwelveData. Error request", LogMessageType.Error);
                Disconnect();
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
                _subscribedSecurities.Clear();
                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _fifoListWebSocketMessage = new ConcurrentQueue<string>();

            Disconnect();
        }

        public void Disconnect()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.TwelveData; }
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

        private string _apiKey;

        private string _baseUrl = "https://api.twelvedata.com";

        private bool _stocksUsa;

        private bool _stocksIndia;

        private bool _forex;

        private bool _etfsUsa;

        private bool _etfsLondon;

        private bool _commodities;

        private string _timezone;

        private TimeZoneInfo _mskTimeZone;

        private ConcurrentDictionary<string, string> _securityTimeZones = new ConcurrentDictionary<string, string>();

        private ConcurrentDictionary<string, TimeZoneInfo> _timeZoneInfoCache = new ConcurrentDictionary<string, TimeZoneInfo>();

        private ConcurrentDictionary<string, decimal> _lastPrices = new ConcurrentDictionary<string, decimal>();

        private WebProxy _myProxy;

        private RateGate _rateGateApiLimit = new RateGate(1, TimeSpan.FromMilliseconds(7550));

        private RateGate _rateGateDayLimitForBasic = new RateGate(800, TimeSpan.FromDays(1));

        #endregion

        #region 3 Securities

        private List<Security> _securities;

        public void GetSecurities()
        {

            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            if (_stocksUsa)
            {
                GetStockUsaSecurities();
            }

            if (_stocksIndia)
            {
                GetStockIndiaSecurities();
            }

            if (_forex)
            {
                GetForexSecurities();
            }

            if (_etfsUsa)
            {
                GetEtfsUsaSecurities();
            }

            if (_etfsLondon)
            {
                GetEtfsLondonSecurities();
            }

            if (_commodities)
            {
                GetCommoditiesSecurities();
            }

            SecurityEvent?.Invoke(_securities);
        }

        private void GetEtfsLondonSecurities()
        {
            try
            {
                _rateGateApiLimit.WaitToProceed();

                string requestStr = $"/etfs?country=UK&apikey={_apiKey}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    SecurityResponse<List<EtfData>> securityResponse = JsonConvert.DeserializeObject<SecurityResponse<List<EtfData>>>(response.Content);

                    if (securityResponse.status == "ok")
                    {
                        for (int i = 0; i < securityResponse.data.Count; i++)
                        {
                            EtfData item = securityResponse.data[i];

                            Security newSecurity = new Security();

                            newSecurity.Exchange = item.exchange;
                            newSecurity.Lot = 1;
                            newSecurity.Name = item.exchange + "_" + item.symbol;
                            newSecurity.NameFull = item.name;
                            newSecurity.NameClass = "ETF" + "_" + item.exchange + "_" + item.country;
                            newSecurity.NameId = item.mic_code + "_" + item.figi_code;
                            newSecurity.SecurityType = SecurityType.CurrencyPair;
                            newSecurity.PriceStep = 0;
                            newSecurity.PriceStepCost = 0;

                            _securities.Add(newSecurity);
                        }
                    }
                    else
                    {
                        SendLogMessage($"EtfsSecurities request error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"EtfsSecurities request error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"EtfsSecurities request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void GetCommoditiesSecurities()
        {
            try
            {
                _rateGateApiLimit.WaitToProceed();

                string requestStr = $"/commodities?apikey={_apiKey}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    SecurityResponse<List<CommoditiesData>> securityResponse = JsonConvert.DeserializeObject<SecurityResponse<List<CommoditiesData>>>(response.Content);

                    if (securityResponse.status == "ok")
                    {
                        for (int i = 0; i < securityResponse.data.Count; i++)
                        {
                            CommoditiesData item = securityResponse.data[i];

                            if (item.name.Contains("Futures"))
                            {
                                continue;
                            }

                            Security newSecurity = new Security();

                            newSecurity.Exchange = "Commodities";
                            newSecurity.Lot = 1;
                            newSecurity.Name = "Commodities" + "_" + item.symbol;
                            newSecurity.NameFull = item.name;
                            newSecurity.NameClass = "Commodities" + "_" + item.category;
                            newSecurity.NameId = item.symbol;
                            newSecurity.SecurityType = SecurityType.CurrencyPair;
                            newSecurity.PriceStep = 0;
                            newSecurity.PriceStepCost = 0;

                            _securities.Add(newSecurity);
                        }
                    }
                    else
                    {
                        SendLogMessage($"EtfsSecurities request error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"EtfsSecurities request error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"EtfsSecurities request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void GetEtfsUsaSecurities()
        {
            try
            {
                _rateGateApiLimit.WaitToProceed();

                string requestStr = $"/etfs?country=US&apikey={_apiKey}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    SecurityResponse<List<EtfData>> securityResponse = JsonConvert.DeserializeObject<SecurityResponse<List<EtfData>>>(response.Content);

                    if (securityResponse.status == "ok")
                    {
                        for (int i = 0; i < securityResponse.data.Count; i++)
                        {
                            EtfData item = securityResponse.data[i];

                            Security newSecurity = new Security();

                            newSecurity.Exchange = item.exchange;
                            newSecurity.Lot = 1;
                            newSecurity.Name = item.exchange + "_" + item.symbol;
                            newSecurity.NameFull = item.name;
                            newSecurity.NameClass = "ETF" + "_" + item.exchange + "_" + item.country;
                            newSecurity.NameId = item.mic_code + "_" + item.figi_code;
                            newSecurity.SecurityType = SecurityType.CurrencyPair;
                            newSecurity.PriceStep = 0;
                            newSecurity.PriceStepCost = 0;

                            _securities.Add(newSecurity);
                        }
                    }
                    else
                    {
                        SendLogMessage($"EtfsSecurities request error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"EtfsSecurities request error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"EtfsSecurities request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void GetStockIndiaSecurities()
        {
            try
            {
                _rateGateApiLimit.WaitToProceed();

                string requestStr = $"/stocks?country=India&apikey={_apiKey}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    SecurityResponse<List<StockData>> securityResponse = JsonConvert.DeserializeObject<SecurityResponse<List<StockData>>>(response.Content);

                    if (securityResponse.status == "ok")
                    {
                        for (int i = 0; i < securityResponse.data.Count; i++)
                        {
                            StockData item = securityResponse.data[i];

                            Security newSecurity = new Security();

                            newSecurity.Exchange = item.exchange;
                            newSecurity.Lot = 1;
                            newSecurity.Name = item.exchange + "_" + item.symbol;
                            newSecurity.NameFull = item.name;
                            newSecurity.NameClass = item.type + "_" + item.exchange + "_" + item.country;
                            newSecurity.NameId = item.mic_code + "_" + item.cusip;
                            newSecurity.SecurityType = SecurityType.CurrencyPair;
                            newSecurity.PriceStep = 0;
                            newSecurity.PriceStepCost = 0;

                            _securities.Add(newSecurity);
                        }
                    }
                    else
                    {
                        SendLogMessage($"StockIndiaSecurities request error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"StockIndiaSecurities request error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"StockIndiaSecurities request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void GetForexSecurities()
        {
            try
            {
                _rateGateApiLimit.WaitToProceed();

                string requestStr = $"/forex_pairs?apikey={_apiKey}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    SecurityResponse<List<ForexData>> securityResponse = JsonConvert.DeserializeObject<SecurityResponse<List<ForexData>>>(response.Content);

                    if (securityResponse.status == "ok")
                    {
                        for (int i = 0; i < securityResponse.data.Count; i++)
                        {
                            ForexData item = securityResponse.data[i];

                            Security newSecurity = new Security();

                            newSecurity.Exchange = "Forex";
                            newSecurity.Lot = 1;
                            newSecurity.Name = "Forex" + "_" + item.symbol;
                            newSecurity.NameFull = item.symbol;
                            newSecurity.NameClass = "Forex";
                            newSecurity.NameId = item.symbol;
                            newSecurity.SecurityType = SecurityType.CurrencyPair;
                            newSecurity.PriceStep = 0;
                            newSecurity.PriceStepCost = 0;

                            _securities.Add(newSecurity);
                        }
                    }
                    else
                    {
                        SendLogMessage($"ForexSecurities error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"ForexSecurities request error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"ForexSecurities request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void GetStockUsaSecurities()
        {
            try
            {
                _rateGateApiLimit.WaitToProceed();

                string requestStr = $"/stocks?country=US&apikey={_apiKey}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    SecurityResponse<List<StockData>> securityResponse = JsonConvert.DeserializeObject<SecurityResponse<List<StockData>>>(response.Content);

                    if (securityResponse.status == "ok")
                    {
                        for (int i = 0; i < securityResponse.data.Count; i++)
                        {
                            StockData item = securityResponse.data[i];

                            Security newSecurity = new Security();

                            newSecurity.Exchange = item.exchange;
                            newSecurity.Lot = 1;
                            newSecurity.Name = item.exchange + "_" + item.symbol;
                            newSecurity.NameFull = item.name;
                            newSecurity.NameClass = item.type + "_" + item.exchange + "_" + item.country;
                            newSecurity.NameId = item.mic_code + "_" + item.cusip;
                            newSecurity.SecurityType = SecurityType.CurrencyPair;
                            newSecurity.PriceStep = 0;
                            newSecurity.PriceStepCost = 0;

                            _securities.Add(newSecurity);
                        }
                    }
                    else
                    {
                        SendLogMessage($"StockUsaSecurities request error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"StockUsaSecurities request error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"StockUsaSecurities request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public void GetPortfolios()
        {
            Portfolio newPortfolio = new Portfolio();
            newPortfolio.Number = "TwelveData Virtual Portfolio";
            newPortfolio.ValueBegin = 1;
            newPortfolio.ValueCurrent = 1;

            PortfolioEvent?.Invoke(new List<Portfolio> { newPortfolio });
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            try
            {
                _rateGateApiLimit.WaitToProceed();

                string nameSecurity = security.Name.Split('_')[1];
                string requestStr = $"/quote?symbol={nameSecurity}&timezone={_timezone}&apikey={_apiKey}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                List<Candle> candles = new List<Candle>();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    CandleResponse candleResponse = JsonConvert.DeserializeObject<CandleResponse>(response.Content);

                    if (candleResponse == null)
                    {
                        SendLogMessage($"Candle error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                        return null;
                    }

                    Candle candle = new Candle();

                    candle.State = CandleState.Finished;

                    DateTime time = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(candleResponse.last_quote_at));
                    candle.TimeStart = ConvertTradeTime(time, nameSecurity);
                    candle.Volume = candleResponse.volume.ToDecimal();
                    candle.Close = candleResponse.close.ToDecimal();
                    candle.High = candleResponse.high.ToDecimal();
                    candle.Low = candleResponse.low.ToDecimal();
                    candle.Open = candleResponse.open.ToDecimal();


                    candles.Add(candle);

                    //return candles;
                }
                else
                {
                    SendLogMessage($"Candle error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    return null;
                }

                int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
                DateTime endTime = candles[0].TimeStart;//DateTime.UtcNow;
                DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

                return GetCandleData(security, timeFrameBuilder, startTime, endTime, startTime, null);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Candles error: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return GetCandleData(security, timeFrameBuilder, startTime, endTime, actualTime, null);
        }

        public List<Candle> GetCandleData(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime, List<Candle> allCandles)
        {
            if (startTime != actualTime)
            {
                startTime = actualTime;
            }

            DateTime timeEx = new DateTime(2021, 10, 12, 11, 0, 0);

            if (security.Exchange == "LSE" && startTime < timeEx)
            {
                startTime = timeEx;
            }

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!CheckTf(tfTotalMinutes))
            {
                return null;
            }

            int needToLoadCandles = GetCountCandlesFromSliceTime(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

            if (needToLoadCandles > 5000)
            {
                needToLoadCandles = 5000;
            }

            string nameSecurity = security.Name.Split('_')[1];
            string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

            if (allCandles == null
                || allCandles.Count == 0)
            {
                allCandles = new List<Candle>();
            }

            DateTime currentEndTime = endTime;

            if (security.Exchange != "Commodities"
                && security.Exchange != "Forex")
            {
                currentEndTime = SkipWeekends(endTime);
            }

            while (currentEndTime > startTime)
            {
                DateTime requestStartTime = currentEndTime.AddMinutes(-(needToLoadCandles * tfTotalMinutes));

                //if (security.Exchange != "Commodities"
                //&& security.Exchange != "Forex")
                //{
                //    requestStartTime = SkipWeekends(currentEndTime.AddMinutes(-(needToLoadCandles * tfTotalMinutes)));
                //}

                if (requestStartTime < startTime)
                {
                    requestStartTime = startTime;
                }

                string startDateStr = requestStartTime.ToString("yyyy-MM-dd HH:mm:ss");
                string endDateStr = currentEndTime.ToString("yyyy-MM-dd HH:mm:ss");

                List<Candle> newCandles = RequestCandleHistory(security.Name, nameSecurity, interval, startDateStr, endDateStr);

                if (newCandles == null || newCandles.Count == 0)
                {
                    break;
                }

                if (allCandles.Count > 0)
                {
                    DateTime earliestExistingTime = allCandles[allCandles.Count - 1].TimeStart;

                    for (int i = newCandles.Count - 1; i >= 0; i--)
                    {
                        if (newCandles[i].TimeStart >= earliestExistingTime)
                        {
                            newCandles.RemoveAt(i);
                        }
                    }
                }

                if (newCandles.Count == 0)
                {
                    break;
                }

                allCandles.AddRange(newCandles);

                DateTime oldestCandleTime = allCandles[allCandles.Count - 1].TimeStart;

                if (oldestCandleTime <= startTime)
                {
                    break;
                }

                currentEndTime = oldestCandleTime.AddMinutes(-1);

                if (security.Exchange != "Commodities"
                && security.Exchange != "Forex")
                {
                    currentEndTime = SkipWeekends(oldestCandleTime.AddMinutes(-1));
                }
            }

            for (int i = allCandles.Count - 1; i >= 0; i--)
            {
                if (allCandles[i].TimeStart < startTime
                    || allCandles[i].TimeStart > endTime)
                {
                    allCandles.RemoveAt(i);
                }
            }

            allCandles.Reverse();

            return allCandles;

        }

        private List<Candle> RequestCandleHistory(string securityName, string nameSecurity, string interval, string startDate, string endDate)
        {
            try
            {
                _rateGateApiLimit.WaitToProceed();

                string requestStr = $"/time_series?symbol={nameSecurity}&interval={interval}&start_date={startDate}&end_date={endDate}&timezone={_timezone}&apikey={_apiKey}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    CandleHistoryResponse candleResponse = JsonConvert.DeserializeObject<CandleHistoryResponse>(response.Content);

                    if (candleResponse.status == "ok")
                    {
                        if (candleResponse.meta != null &&
                            !string.IsNullOrEmpty(candleResponse.meta.exchange_timezone))
                        {
                            _securityTimeZones[securityName] = candleResponse.meta.exchange_timezone;
                        }

                        List<Candle> candles = new List<Candle>();

                        for (int i = 0; i < candleResponse.values.Count; i++)
                        {
                            Candle candle = new Candle();

                            candle.State = CandleState.Finished;
                            candle.TimeStart = DateTime.Parse(candleResponse.values[i].datetime);
                            candle.Volume = candleResponse.values[i].volume.ToDecimal();
                            candle.Close = candleResponse.values[i].close.ToDecimal();
                            candle.High = candleResponse.values[i].high.ToDecimal();
                            candle.Low = candleResponse.values[i].low.ToDecimal();
                            candle.Open = candleResponse.values[i].open.ToDecimal();

                            candles.Add(candle);
                        }

                        return candles;
                    }
                    else
                    {
                        if (!response.Content.StartsWith("{\"code\":400,\"message\":\"No data is available on the specified dates. "))
                        {
                            SendLogMessage($"Candle history error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                        }

                        return null;
                    }
                }
                else
                {
                    if (!response.Content.StartsWith("{\"code\":400,\"message\":\"No data is available on the specified dates. "))
                    {
                        SendLogMessage($"Candle history error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Candles data error: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private DateTime SkipWeekends(DateTime date)
        {
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                return date.AddDays(-2);
            }

            if (date.DayOfWeek == DayOfWeek.Saturday)
            {
                return date.AddDays(-1);
            }

            return date;
        }

        private int GetCountCandlesFromSliceTime(DateTime startTime, DateTime endTime, TimeSpan tf)
        {
            if (tf.Hours != 0)
            {
                TimeSpan TimeSlice = endTime - startTime;

                return Convert.ToInt32(TimeSlice.TotalHours / tf.TotalHours);
            }
            else
            {
                TimeSpan TimeSlice = endTime - startTime;
                return Convert.ToInt32(TimeSlice.TotalMinutes / tf.TotalMinutes);
            }
        }

        private bool CheckTf(int timeFrameMinutes)
        {
            if (timeFrameMinutes == 1
                || timeFrameMinutes == 5
                || timeFrameMinutes == 15
                || timeFrameMinutes == 30
                || timeFrameMinutes == 60
                || timeFrameMinutes == 120
                || timeFrameMinutes == 240
                || timeFrameMinutes == 1440)
            {
                return true;
            }
            return false;
        }

        private string GetInterval(TimeSpan tf)
        {
            if (tf.Minutes != 0)
            {
                return $"{tf.Minutes}min";
            }
            else if (tf.Hours != 0)
            {
                return $"{tf.Hours}h";
            }
            else
            {
                return $"{tf.Days}day";
            }
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private string _webSocketUrl = "wss://ws.twelvedata.com";

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (_fifoListWebSocketMessage == null)
                {
                    _fifoListWebSocketMessage = new ConcurrentQueue<string>();
                }

                WebSocket socket = CreateNewPublicSocket();

                if (socket != null)
                {
                    _webSocketPublic.Add(socket);
                }
                else
                {
                    SendLogMessage("TwelveData failed to create public WebSocket", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewPublicSocket()
        {
            try
            {
                WebSocket webSocketPublicNew = new WebSocket(_webSocketUrl + "/v1/quotes/price?apikey=" + _apiKey);

                if (_myProxy != null)
                {
                    webSocketPublicNew.SetProxy(_myProxy);
                }

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += WebSocketPublic_Opened;
                webSocketPublicNew.OnMessage += WebSocketPublic_MessageReceived;
                webSocketPublicNew.OnError += WebSocketPublic_Error;
                webSocketPublicNew.OnClose += WebSocketPublic_Closed;
                webSocketPublicNew.ConnectAsync();

                return webSocketPublicNew;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void DeleteWebSocketConnection()
        {
            if (_webSocketPublic != null)
            {
                try
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        webSocketPublic.OnOpen -= WebSocketPublic_Opened;
                        webSocketPublic.OnClose -= WebSocketPublic_Closed;
                        webSocketPublic.OnMessage -= WebSocketPublic_MessageReceived;
                        webSocketPublic.OnError -= WebSocketPublic_Error;

                        if (webSocketPublic.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.CloseAsync();
                        }

                        webSocketPublic.Dispose();
                    }
                }
                catch
                {
                    // ignore
                }

                _webSocketPublic.Clear();
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublic_Opened(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Connect;

                    if (ConnectEvent != null)
                    {
                        ConnectEvent();
                    }

                    SendLogMessage("TwelveData WebSocket connection open", LogMessageType.System);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublic_Closed(object sender, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublic_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (e.Data.Length == 4)
                { // pong message
                    return;
                }

                if (_fifoListWebSocketMessage == null)
                {
                    return;
                }

                _fifoListWebSocketMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublic_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        private void CheckAliveWebSocket()
        {
            while (true)
            {
                try
                {
                    if (IsCompletelyDeleted == true)
                    {
                        return;
                    }

                    Thread.Sleep(10000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        if (webSocketPublic != null
                            && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.SendAsync("{\"action\":\"heartbeat\"}");
                        }
                        else
                        {
                            Disconnect();
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        #endregion

        #region 9 Security subscribe

        private List<string> _subscribedSecurities = new List<string>();

        List<string> symbolsToSend = new List<string>();

        public void Subscribe(Security security)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (_subscribedSecurities.Contains(security.Name))
                {
                    return;
                }

                _subscribedSecurities.Add(security.Name);

                string symbol = GetTwelveDataSymbol(security.Name);

                if (string.IsNullOrEmpty(symbol))
                {
                    return;
                }

                symbolsToSend.Add(symbol);

                if (_webSocketPublic.Count == 0)
                {
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 1500 == 0)
                {
                    // creating a new socket
                    WebSocket newSocket = CreateNewPublicSocket();

                    DateTime timeEnd = DateTime.Now.AddSeconds(10);

                    while (newSocket.ReadyState != WebSocketState.Open)
                    {
                        Thread.Sleep(1000);

                        if (timeEnd < DateTime.Now)
                        {
                            break;
                        }
                    }

                    if (newSocket.ReadyState == WebSocketState.Open)
                    {
                        _webSocketPublic.Add(newSocket);
                        webSocketPublic = newSocket;
                    }
                }

                if (webSocketPublic != null)
                {
                    webSocketPublic.SendAsync($"{{\"action\":\"subscribe\",\"params\":{{\"symbols\":\"{symbol}\"}}}}");
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("TwelveData subscribe error: " + ex.Message + " " + ex.StackTrace, LogMessageType.Error);
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            try
            {
                if (_webSocketPublic != null
                    && _webSocketPublic.Count != 0)
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        try
                        {
                            if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open)
                            {
                                if (_subscribedSecurities != null)
                                {
                                    for (int i2 = 0; i2 < _subscribedSecurities.Count; i2++)
                                    {
                                        string symbol = GetTwelveDataSymbol(_subscribedSecurities[i2]);

                                        if (string.IsNullOrEmpty(symbol))
                                        {
                                            continue;
                                        }

                                        webSocketPublic.SendAsync($"{{\"action\":\"unsubscribe\",\"params\":{{\"symbols\":\"{symbol}\"}}}}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private string GetTwelveDataSymbol(string securityName)
        {
            try
            {
                if (string.IsNullOrEmpty(securityName))
                {
                    return null;
                }

                string[] parts = securityName.Split('_');

                if (parts.Length > 1)
                {
                    return parts[1];
                }

                return securityName;
            }
            catch
            {
                return securityName;
            }
        }

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> _fifoListWebSocketMessage = new ConcurrentQueue<string>();

        private void MessageReaderPublic()
        {
            while (true)
            {
                try
                {
                    if (_fifoListWebSocketMessage.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        Thread.Sleep(1);
                    }
                    else
                    {
                        string message = null;

                        _fifoListWebSocketMessage.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.Contains("\"event\":\"price\""))
                        {
                            WebSocketPriceMessage priceMsg = JsonConvert.DeserializeObject<WebSocketPriceMessage>(message);
                            UpdatePrice(priceMsg);
                        }
                        else if (message.Contains("\"event\":\"status\"")
                            || message.Contains("\"event\":\"subscribe-status\""))
                        {
                            WebSocketStatusMessage statusMsg = JsonConvert.DeserializeObject<WebSocketStatusMessage>(message);

                            if (statusMsg == null)
                            {
                                continue;
                            }

                            string successSymbols = statusMsg.success != null ? statusMsg.success.ToString() : "";
                            string failSymbols = statusMsg.fails != null ? statusMsg.fails.ToString() : "";

                            SendLogMessage($"TwelveData subscribe status: {statusMsg.status}. Success: {successSymbols}. Fails: {failSymbols}. Message: {statusMsg.message}", LogMessageType.System);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private DateTime ConvertTradeTime(DateTime timeUtc, string securityName)
        {
            try
            {
                if (_timezone == "UTC")
                {
                    return timeUtc;
                }

                if (_timezone == "Europe/Moscow" && _mskTimeZone != null)
                {
                    return TimeZoneInfo.ConvertTimeFromUtc(timeUtc, _mskTimeZone);
                }

                if (_timezone == "Exchange" &&
                    _securityTimeZones.TryGetValue(securityName, out string tzId) &&
                    !string.IsNullOrEmpty(tzId))
                {
                    if (!_timeZoneInfoCache.TryGetValue(tzId, out TimeZoneInfo tz))
                    {
                        try
                        {
                            tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                            _timeZoneInfoCache[tzId] = tz;
                        }
                        catch
                        {
                            return timeUtc;
                        }
                    }

                    return TimeZoneInfo.ConvertTimeFromUtc(timeUtc, tz);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("TwelveData time convert error: " + ex.Message, LogMessageType.Error);
            }

            return timeUtc;
        }

        private void UpdatePrice(WebSocketPriceMessage priceMsg)
        {
            try
            {
                if (priceMsg == null || string.IsNullOrEmpty(priceMsg.symbol))
                {
                    return;
                }

                string securityName = GetSecurityNameBySymbol(priceMsg.symbol);

                if (string.IsNullOrEmpty(securityName))
                {
                    SendLogMessage($"TwelveData price symbol not subscribed: {priceMsg.symbol}", LogMessageType.System);
                    return;
                }

                decimal price = 0;

                if (string.IsNullOrEmpty(priceMsg.price) == false)
                {
                    price = priceMsg.price.ToDecimal();
                }

                long timestamp = 0;

                if (string.IsNullOrEmpty(priceMsg.timestamp) == false)
                {
                    long.TryParse(priceMsg.timestamp, out timestamp);
                }

                _lastPrices.TryGetValue(securityName, out decimal lastPrice);

                Trade trade = new Trade();
                trade.SecurityNameCode = securityName;
                trade.Price = price;
                trade.Volume = 1;
                trade.Side = price >= lastPrice ? Side.Buy : Side.Sell;
                trade.MicroSeconds = 0;

                if (timestamp > 0)
                {
                    DateTime time = TimeManager.GetDateTimeFromTimeStampSeconds(timestamp);
                    trade.Time = ConvertTradeTime(time, securityName);
                    trade.Id = trade.Time.Ticks.ToString();
                }
                else
                {
                    trade.Id = priceMsg.timestamp + "_" + priceMsg.symbol;
                }

                _lastPrices[securityName] = price;

                NewTradesEvent?.Invoke(trade);

                decimal bid = 0;
                decimal ask = 0;

                if (string.IsNullOrEmpty(priceMsg.bid) == false)
                {
                    bid = priceMsg.bid.ToDecimal();
                }

                if (string.IsNullOrEmpty(priceMsg.ask) == false)
                {
                    ask = priceMsg.ask.ToDecimal();
                }

                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = securityName;

                if (timestamp > 0)
                {
                    DateTime depthTimeUtc = TimeManager.GetDateTimeFromTimeStampSeconds(timestamp);
                    depth.Time = ConvertTradeTime(depthTimeUtc, securityName);
                }

                MarketDepthLevel bidLevel = new MarketDepthLevel();

                if (bid > 0)
                {
                    bidLevel.Price = (double)bid;
                }
                else
                {
                    bidLevel.Price = (double)price;
                }

                bidLevel.Bid = 1;
                depth.Bids.Add(bidLevel);

                MarketDepthLevel askLevel = new MarketDepthLevel();

                if (ask > 0)
                {
                    askLevel.Price = (double)ask;
                }
                else
                {
                    askLevel.Price = (double)price;
                }

                askLevel.Ask = 1;
                depth.Asks.Add(askLevel);

                MarketDepthEvent?.Invoke(depth);
            }
            catch (Exception ex)
            {
                SendLogMessage("TwelveData price message handle error: " + ex.Message + " " + ex.StackTrace, LogMessageType.Error);
            }
        }

        private string GetSecurityNameBySymbol(string symbol)
        {
            try
            {

                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    string security = _subscribedSecurities[i];

                    if (security == null)
                    {
                        continue;
                    }

                    string twelveSymbol = GetTwelveDataSymbol(security);

                    if (twelveSymbol == symbol)
                    {
                        return security;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        #endregion

        #region 11 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, messageType);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region 12 Unused methods

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

        public void SetLeverage(Security security, decimal leverage) { }

        public event Action<News> NewsEvent { add { } remove { } }

        public event Action<Order> MyOrderEvent { add { } remove { } }

        public event Action<MyTrade> MyTradeEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion
    }
}
