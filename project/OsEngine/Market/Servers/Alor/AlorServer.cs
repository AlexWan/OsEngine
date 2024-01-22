/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Alor.Json;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using WebSocket4Net;

namespace OsEngine.Market.Servers.Alor
{
    public class AlorServer : AServer
    {
        public AlorServer()
        {
            AlorServerRealization realization = new AlorServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamToken, "");
            CreateParameterString(OsLocalization.Market.Label112, "");
            CreateParameterString(OsLocalization.Market.Label113, "");
            CreateParameterString(OsLocalization.Market.Label114, "");
            CreateParameterString(OsLocalization.Market.Label115, "");
            CreateParameterBoolean(OsLocalization.Market.UseStock, true);
            CreateParameterBoolean(OsLocalization.Market.UseFutures, true);
            CreateParameterBoolean(OsLocalization.Market.UseCurrency, true);
            CreateParameterBoolean(OsLocalization.Market.UseOptions, false);
            CreateParameterBoolean(OsLocalization.Market.UseOther, false);
        }
    }

    public class AlorServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public AlorServerRealization()
        {
            Thread worker = new Thread(ConnectionCheckThread);
            worker.Name = "CheckAliveAlor";
            worker.Start();

            Thread worker2 = new Thread(DataMessageReader);
            worker2.Name = "DataMessageReaderAlor";
            worker2.Start();

            Thread worker3 = new Thread(PortfolioMessageReader);
            worker3.Name = "PortfolioMessageReaderAlor";
            worker3.Start();
        }

        public void Connect()
        {
            try
            {
                
                _securities.Clear();
                _myPortfolious.Clear();
                _subscribledSecurities.Clear();
                _lastGetLiveTimeToketTime = DateTime.MinValue;

                SendLogMessage("Start Alor Connection", LogMessageType.System);

                _apiTokenRefresh = ((ServerParameterString)ServerParameters[0]).Value;
                _portfolioSpotId = ((ServerParameterString)ServerParameters[1]).Value;
                _portfolioFutId = ((ServerParameterString)ServerParameters[2]).Value;
                _portfolioCurrencyId = ((ServerParameterString)ServerParameters[3]).Value;
                _portfolioSpareId = ((ServerParameterString)ServerParameters[4]).Value;

                if (string.IsNullOrEmpty(_apiTokenRefresh))
                {
                    SendLogMessage("Connection terminated. You must specify the api token. You can get it on the Alor website",
                        LogMessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(_portfolioSpotId)
                    && string.IsNullOrEmpty(_portfolioFutId)
                    && string.IsNullOrEmpty(_portfolioCurrencyId)
                    && string.IsNullOrEmpty(_portfolioSpareId))
                {
                    SendLogMessage("Connection terminated. You must specify the name of the portfolio to be traded. You can see it on the Alor website.",
                    LogMessageType.Error);
                    return;
                }

                if (GetCurSessionToken() == false)
                {
                    SendLogMessage("Authorization Error. Probably an invalid token is specified. You can see it on the Alor website.",
                    LogMessageType.Error);
                    return;
                }

                CreateWebSocketConnection();
                
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
            }
        }

        private void ConnectionCheckThread()
        {
            while (true)
            {
                Thread.Sleep(10000);

                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    continue;
                }

                if (_lastGetLiveTimeToketTime.AddMinutes(20) < DateTime.Now)
                {
                    if (GetCurSessionToken() == false)
                    {
                        if (ServerStatus == ServerConnectStatus.Connect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }
                }
            }
        }

        DateTime _lastGetLiveTimeToketTime = DateTime.MinValue;

        private bool GetCurSessionToken()
        {
            try
            {
                string endPoint = "/refresh?token=" + _apiTokenRefresh;
                RestRequest requestRest = new RestRequest(endPoint, Method.POST);
                RestClient client = new RestClient(_oauthApiHost);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content;
                    TokenResponse newLiveToken = JsonConvert.DeserializeAnonymousType(content, new TokenResponse());

                    _lastGetLiveTimeToketTime = DateTime.Now;
                    _apiTokenReal = newLiveToken.AccessToken;
                    return true;
                }
                else
                {
                    SendLogMessage("Token request error", LogMessageType.Error);
                    return false;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Token request error: " + exception.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public void Dispose()
        {
            _securities.Clear();
            _myPortfolious.Clear();
            _lastGetLiveTimeToketTime = DateTime.MinValue;

            DeleteWebSocketConnection();

            SendLogMessage("Connection Closed by Alor. WebSocket Data Closed Event", LogMessageType.System);

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.Alor;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        private readonly string _restApiHost = "https://api.alor.ru";
        private readonly string _oauthApiHost = "https://oauth.alor.ru";

        private bool _useStock = false;
        private bool _useFutures = false;
        private bool _useOptions = false;
        private bool _useCurrency = false;
        private bool _useOther = false;

        private string _portfolioSpotId;
        private string _portfolioFutId;
        private string _portfolioCurrencyId;
        private string _portfolioSpareId;
        private string _apiTokenRefresh;
        private string _apiTokenReal; // life time 30 minutes

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            //securities?sector=FOND&limit=1000
            _useStock = ((ServerParameterBool)ServerParameters[5]).Value;
            _useFutures = ((ServerParameterBool)ServerParameters[6]).Value;
            _useCurrency = ((ServerParameterBool)ServerParameters[7]).Value;
            _useOptions = ((ServerParameterBool)ServerParameters[8]).Value;
            _useOther = ((ServerParameterBool)ServerParameters[9]).Value;

            string apiEndpoint;

            if (_useStock || _useOther)
            {
                apiEndpoint = $"/md/v2/Securities/MOEX?format=Simple&market=FOND&includeOld=false";
                UpdateSec(apiEndpoint);
            }

            if (_useCurrency)
            {
                apiEndpoint = $"/md/v2/Securities/MOEX?format=Simple&market=CURR&includeOld=false";
                UpdateSec(apiEndpoint);
            }

            if (_useFutures)
            {
                apiEndpoint = $"/md/v2/Securities/MOEX?format=Simple&market=FORTS&includeOld=false";
                UpdateSec(apiEndpoint);
            }

            if (_useOptions)
            {
                apiEndpoint = $"/md/v2/Securities/MOEX?format=Simple&market=SPBX&includeOld=false";
                UpdateSec(apiEndpoint);
            }

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                if (SecurityEvent != null)
                {
                    SecurityEvent.Invoke(_securities);
                }
            }

        }

        private List<Security> _securities = new List<Security>();

        private void UpdateSec(string endPoint)
        {
            //curl - X GET "https://api.alor.ru/md/v2/Securities/MOEX?format=Simple&market=FOND&includeOld=false" - H "accept: application/json"

            try
            {
                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                RestClient client = new RestClient(_restApiHost);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content;
                    List<AlorSecurity> securities = JsonConvert.DeserializeAnonymousType(content, new List<AlorSecurity>());
                    UpdateSecuritiesFromServer(securities);
                }
                else
                {
                    SendLogMessage("Securities request error. Status: " + response.StatusCode, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Securities request error" + exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecuritiesFromServer(List<AlorSecurity> stocks)
        {
            try
            {
                if (stocks == null ||
                    stocks.Count == 0)
                {
                    return;
                }

                for(int i = 0;i < stocks.Count;i++)
                {
                    AlorSecurity item = stocks[i];

                    SecurityType instrumentType = GetSecurityType(item);

                    if (!CheckNeedSecurity(instrumentType))
                    {
                        continue;
                    }

                    if(instrumentType == SecurityType.None)
                    {
                        continue;
                    }

                    Security newSecurity = new Security();
                    newSecurity.SecurityType = instrumentType;
                    newSecurity.Exchange = item.exchange;
                    newSecurity.DecimalsVolume = 1;
                    newSecurity.Lot = item.lotsize.ToDecimal();
                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.description;

                    if (newSecurity.SecurityType == SecurityType.Option)
                    {
                        newSecurity.NameClass = "Option";
                    }
                    else if (item.type == null)
                    {
                        if(item.description.StartsWith("Индекс"))
                        {
                            newSecurity.NameClass = "Index";
                            newSecurity.SecurityType = SecurityType.Index;
                        }
                        else
                        {
                            newSecurity.NameClass = "Unknown";
                            newSecurity.SecurityType = SecurityType.None;
                        }
                    }
                    else if (item.type.StartsWith("Календарный спред"))
                    {
                        newSecurity.NameClass = "Futures spread";
                    }
                    else if (newSecurity.SecurityType == SecurityType.Futures)
                    {
                        newSecurity.NameClass = "Futures";
                    }
                    else if (newSecurity.SecurityType == SecurityType.CurrencyPair)
                    {
                        newSecurity.NameClass = "Currency";
                    }
                    else if (item.type == "CS")
                    {
                        newSecurity.NameClass = "Stock";
                    }
		    else if (item.type == "CORP")
                    {
                        newSecurity.NameClass = "Bond";
                    }
                    else if (item.type == "PS")
                    {
                        newSecurity.NameClass = "Stock Pref";
                    }
                    else
                    {
                        newSecurity.NameClass = item.type;
                    }

                    newSecurity.NameId = item.shortname;
                   
                    newSecurity.Decimals = GetDecimals(item.minstep.ToDecimal());
                    newSecurity.PriceStep = item.minstep.ToDecimal();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }
                   
            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading stocks: {e.Message}", LogMessageType.Error);
            }
        }

        private SecurityType GetSecurityType(AlorSecurity security)
        {
            var cfiCode = security.cfiCode;
            if (cfiCode.StartsWith("F")) return SecurityType.Futures;
            if (cfiCode.StartsWith("O")) return SecurityType.Option;
            if (cfiCode.StartsWith("ES") || cfiCode.StartsWith("EP")) return SecurityType.Stock;
            if (cfiCode.StartsWith("DB")) return SecurityType.Bond;

            var board = security.board;
            if (board == "CETS") return SecurityType.CurrencyPair;

            return SecurityType.None;
        }

        private bool CheckNeedSecurity(SecurityType instrumentType)
        {
            switch (instrumentType)
            {
                case SecurityType.Stock when _useStock:
                case SecurityType.Futures when _useFutures:
                case SecurityType.Option when _useOptions:
                case SecurityType.CurrencyPair when _useCurrency:
                case SecurityType.None when _useOther:
                case SecurityType.Bond when _useOther:
                case SecurityType.Index when _useOther:
                    return true;
                default:
                    return false;
            }
        }

        private int GetDecimals(decimal x)
        {
            var precision = 0;
            while (x * (decimal)Math.Pow(10, precision) != Math.Round(x * (decimal)Math.Pow(10, precision)))
                precision++;
            return precision;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolious = new List<Portfolio>();

        public void GetPortfolios()
        {
            if(string.IsNullOrEmpty(_portfolioSpotId) == false)
            {
                GetCurrentPortfolio(_portfolioSpotId, "SPOT");
            }

            if (string.IsNullOrEmpty(_portfolioFutId) == false)
            {
                GetCurrentPortfolio(_portfolioFutId, "FORTS");
            }

            if (string.IsNullOrEmpty(_portfolioCurrencyId) == false)
            {
                GetCurrentPortfolio(_portfolioCurrencyId, "CURR");
            }

            if (string.IsNullOrEmpty(_portfolioSpareId) == false)
            {
                GetCurrentPortfolio(_portfolioSpareId, "SPARE");
            }

            if(_myPortfolious.Count != 0)
            {
                if(PortfolioEvent != null)
                {
                    PortfolioEvent(_myPortfolious);
                }
            }

            ActivatePortfolioSocket();
        }

        private void GetCurrentPortfolio(string portfoliId, string namePrefix)
        {
            try
            {
                string endPoint = $"/md/v2/clients/MOEX/{portfoliId}/summary?format=Simple";
                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                requestRest.AddHeader("accept", "application/json");

                RestClient client = new RestClient(_restApiHost);

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content;
                    AlorPortfolioRest portfolio = JsonConvert.DeserializeAnonymousType(content, new AlorPortfolioRest());

                    ConvertToPortfolio(portfolio, portfoliId, namePrefix);
                }
                else
                {
                    SendLogMessage("Portfolio request error. Status: " 
                        + response.StatusCode + "  " + namePrefix, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void ConvertToPortfolio(AlorPortfolioRest portfolio, string name, string prefix)
        {
            Portfolio newPortfolio = new Portfolio();
            newPortfolio.Number = name + "_" + prefix;
            newPortfolio.ValueCurrent = portfolio.buyingPower.ToDecimal();
            _myPortfolious.Add(newPortfolio);
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            DateTime endTime = DateTime.Now.ToUniversalTime();

            while(endTime.Hour != 23)
            {
                endTime = endTime.AddHours(1);
            }

            int candlesInDay = 0;

            if(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes >= 1)
            {
                candlesInDay = 900 / Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
            }
            else
            {
                candlesInDay = 54000/ Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalSeconds);
            }

            if(candlesInDay == 0)
            {
                candlesInDay = 1;
            }

            int daysCount = candleCount / candlesInDay;

            if(daysCount == 0)
            {
                daysCount = 1;
            }

            daysCount++;

            if(daysCount > 5)
            { // добавляем выходные
                daysCount = daysCount + (daysCount / 5) * 2;
            }

            DateTime startTime = endTime.AddDays(-daysCount);

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);
        
            while(candles.Count > candleCount)
            {
                candles.RemoveAt(0);
            }

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        { 
            if(startTime != actualTime)
            {
                startTime = actualTime;
            }

            List<Candle> candles = new List<Candle>();

            TimeSpan additionTime = TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 2500);

            DateTime endTimeReal = startTime.Add(additionTime);

            while (startTime < endTime)
            {
                CandlesHistoryAlor history = GetHistoryCandle(security, timeFrameBuilder, startTime, endTimeReal);

                List<Candle> newCandles = ConvertToOsEngineCandles(history);

                if(newCandles != null &&
                    newCandles.Count > 0)
                {
                    candles.AddRange(newCandles);
                }

                if(string.IsNullOrEmpty(history.prev) 
                    && string.IsNullOrEmpty(history.next))
                {// на случай если указаны очень старые данные, и их там нет
                    startTime = startTime.Add(additionTime);
                    endTimeReal = startTime.Add(additionTime);
                    continue;
                }

                if (string.IsNullOrEmpty(history.next))
                {
                    break;
                }

                DateTime realStart = ConvertToDateTimeFromUnixFromSeconds(history.next);

                startTime = realStart;
                endTimeReal = realStart.Add(additionTime);
            }

            while (candles != null &&
                candles.Count != 0 && 
                candles[candles.Count - 1].TimeStart > endTime)
            {
                candles.RemoveAt(candles.Count - 1);
            }

            return candles;
        }

        private CandlesHistoryAlor GetHistoryCandle(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime)
        {
            // curl -X GET "https://api.alor.ru/md/v2/history?symbol=SBER&exchange=MOEX&tf=60&from=1549000661&to=1550060661&format=Simple" -H "accept: application/json"

            string endPoint = "md/v2/history?symbol=" + security.Name;
            endPoint += "&exchange=MOEX";

            //Начало отрезка времени (UTC) в формате Unix Time Seconds

            endPoint += "&tf=" + GetAlorTf(timeFrameBuilder);
            endPoint += "&from=" + ConvertToUnixTimestamp(startTime);
            endPoint += "&to=" + ConvertToUnixTimestamp(endTime);
            endPoint += "&format=Simple";

            try
            {
                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                requestRest.AddHeader("accept", "application/json");
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                RestClient client = new RestClient(_restApiHost);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content;
                    CandlesHistoryAlor candles = JsonConvert.DeserializeAnonymousType(content, new CandlesHistoryAlor());
                    return candles;
                }
                else
                {
                    SendLogMessage("Candles request error. Status: " + response.StatusCode, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Candles request error" + exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private List<Candle> ConvertToOsEngineCandles(CandlesHistoryAlor candles)
        {
            List<Candle> result = new List<Candle>();

            for(int i = 0;i < candles.history.Count;i++)
            {
                AlorCandle curCandle = candles.history[i];

                Candle newCandle = new Candle();
                newCandle.Open = curCandle.open.ToDecimal();
                newCandle.High = curCandle.high.ToDecimal();
                newCandle.Low = curCandle.low.ToDecimal();
                newCandle.Close = curCandle.close.ToDecimal();
                newCandle.Volume = curCandle.volume.ToDecimal();
                newCandle.TimeStart = ConvertToDateTimeFromUnixFromSeconds(curCandle.time);

                result.Add(newCandle);
            }

            return result;
        }

        private string GetAlorTf(TimeFrameBuilder timeFrameBuilder)
        {
            //Длительность таймфрейма в секундах или код ("D" - дни, "W" - недели, "M" - месяцы, "Y" - годы)
            // 15
            // 60
            // 300
            // 3600
            // D - Day
            // W - Week
            // M - Month
            // Y - Year

            string result = "";

            if(timeFrameBuilder.TimeFrame == TimeFrame.Day)
            {
                result = "D";
            }
            else
            {
                result = timeFrameBuilder.TimeFrameTimeSpan.TotalSeconds.ToString();
            }

            return result;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;

            // blocked

            List<Trade> trades = new List<Trade>();

            TimeSpan additionTime = TimeSpan.FromMinutes(1440);

            DateTime endTimeReal = startTime.Add(additionTime);

            while (startTime < endTime)
            {
                TradesHistoryAlor history = GetHistoryTrades(security, startTime, endTimeReal);

                List<Trade> newTrades = ConvertToOsEngineTrades(history);

                if (newTrades != null &&
                    newTrades.Count > 0)
                {
                    trades.AddRange(newTrades);
                    DateTime realStart = newTrades[newTrades.Count - 1].Time;
                    startTime = realStart;
                    endTimeReal = realStart.Add(additionTime);
                }
                else
                {
                    startTime = startTime.Add(additionTime);
                    endTimeReal = startTime.Add(additionTime);
                }
            }

            return trades;
        }

        private TradesHistoryAlor GetHistoryTrades(Security security, DateTime startTime, DateTime endTime)
        {
            // curl -X GET "https://api.alor.ru/md/v2/Securities/MOEX/LKOH/alltrades/history?from=1593430060&to=1593430560&limit=100&offset=10&format=Simple" -H "accept: application/json"

            string endPoint = "/md/v2/Securities/MOEX/" + security.Name;
            endPoint += "/alltrades/history?";

            endPoint += "from=" + ConvertToUnixTimestamp(startTime);
            endPoint += "&to=" + ConvertToUnixTimestamp(endTime);
            endPoint += "&limit=50000";
            endPoint += "&format=Simple";

            try
            {
                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                requestRest.AddHeader("accept", "application/json");
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                RestClient client = new RestClient(_restApiHost);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content;
                    TradesHistoryAlor trades = JsonConvert.DeserializeAnonymousType(content, new TradesHistoryAlor());
                    return trades;
                }
                else
                {
                    SendLogMessage("Trades request error. Status: " + response.StatusCode, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Trades request error" + exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private List<Trade> ConvertToOsEngineTrades(TradesHistoryAlor trades)
        {
            List<Trade> result = new List<Trade>();

            if(trades.list == null)
            {
                return result;
            }

            for (int i = 0; i < trades.list.Count; i++)
            {
                AlorTrade curTrade = trades.list[i];

                Trade newTrade = new Trade();
                newTrade.Volume = curTrade.qty.ToDecimal();
                newTrade.Time = ConvertToDateTimeFromTimeAlorData(curTrade.time);
                newTrade.Price = curTrade.price.ToDecimal();
                newTrade.Id = curTrade.id;
                newTrade.SecurityNameCode = curTrade.symbol;

                if(curTrade.side == "buy")
                {
                    newTrade.Side = Side.Buy;
                }
                else
                {
                    newTrade.Side = Side.Sell;
                }

                result.Add(newTrade);
            }

            return result;
        }

        #endregion

        #region 6 WebSocket creation

        private readonly string _wsHost = "wss://api.alor.ru/ws";

        private string _socketLocker = "webSocketLockerAlor";

        private string GetGuid()
        {
            lock (_guidLocker)
            {
                iterator++;
                return iterator.ToString();
            }
        }

        int iterator = 0;

        string _guidLocker = "guidLocker";

        private void CreateWebSocketConnection()
        {
            try
            {
                _subscriptionsData.Clear();
                _subscriptionsPortfolio.Clear();

                if (_webSocketData != null)
                {
                    return;
                }

                _socketDataIsActive = false;
                _socketPortfolioIsActive = false;

                lock (_socketLocker)
                {
                    WebSocketDataMessage = new ConcurrentQueue<string>();
                    WebSocketPortfolioMessage = new ConcurrentQueue<string>();

                    _webSocketData = new WebSocket(_wsHost);
                    _webSocketData.EnableAutoSendPing = true;
                    _webSocketData.AutoSendPingInterval = 10;
                    _webSocketData.Opened += WebSocketData_Opened;
                    _webSocketData.Closed += WebSocketData_Closed;
                    _webSocketData.MessageReceived += WebSocketData_MessageReceived;
                    _webSocketData.Error += WebSocketData_Error;
                    _webSocketData.Open();


                    _webSocketPortfolio = new WebSocket(_wsHost);
                    _webSocketPortfolio.EnableAutoSendPing = true;
                    _webSocketPortfolio.AutoSendPingInterval = 10;
                    _webSocketPortfolio.Opened += _webSocketPortfolio_Opened;
                    _webSocketPortfolio.Closed += _webSocketPortfolio_Closed;
                    _webSocketPortfolio.MessageReceived += _webSocketPortfolio_MessageReceived;
                    _webSocketPortfolio.Error += _webSocketPortfolio_Error;
                    _webSocketPortfolio.Open();

                }

            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebSocketConnection()
        {
            try
            {
                lock (_socketLocker)
                {
                    if (_webSocketData != null)
                    {
                        try
                        {
                            _webSocketData.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _webSocketPortfolio.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        _webSocketData.Opened -= WebSocketData_Opened;
                        _webSocketData.Closed -= WebSocketData_Closed;
                        _webSocketData.MessageReceived -= WebSocketData_MessageReceived;
                        _webSocketData.Error -= WebSocketData_Error;
                        _webSocketData = null;

                        _webSocketPortfolio.Opened -= _webSocketPortfolio_Opened;
                        _webSocketPortfolio.Closed -= _webSocketPortfolio_Closed;
                        _webSocketPortfolio.MessageReceived -= _webSocketPortfolio_MessageReceived;
                        _webSocketPortfolio.Error -= _webSocketPortfolio_Error;
                        _webSocketPortfolio = null;
                    }
                }
            }
            catch (Exception exeption)
            {

            }
            finally
            {
                _webSocketData = null;
            }
        }

        private bool _socketDataIsActive;

        private bool _socketPortfolioIsActive;

        private void CheckActivationSockets()
        {
            if (_socketDataIsActive == false)
            {
                return;
            }

            if (_socketPortfolioIsActive == false)
            {
                return;
            }

            try
            {
                SendLogMessage("All sockets activated. Connect State", LogMessageType.System);
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

        }

        private WebSocket _webSocketData;

        private WebSocket _webSocketPortfolio;

        private void ActivatePortfolioSocket()
        {
            if (string.IsNullOrEmpty(_portfolioSpotId) == false)
            {
                ActivateCurrentPortfolioListening(_portfolioSpotId);
            }
            if (string.IsNullOrEmpty(_portfolioFutId) == false)
            {
                ActivateCurrentPortfolioListening(_portfolioFutId);
            }
            if (string.IsNullOrEmpty(_portfolioCurrencyId) == false)
            {
                ActivateCurrentPortfolioListening(_portfolioCurrencyId);
            }
            if (string.IsNullOrEmpty(_portfolioSpareId) == false)
            {
                ActivateCurrentPortfolioListening(_portfolioSpareId);
            }
        }

        private void ActivateCurrentPortfolioListening(string portfolioName)
        {
            // myTrades subscription

            RequestSocketSubscribleMyTrades subObjTrades = new RequestSocketSubscribleMyTrades();
            subObjTrades.guid = GetGuid();
            subObjTrades.token = _apiTokenReal;
            subObjTrades.portfolio = portfolioName;

            string messageTradeSub = JsonConvert.SerializeObject(subObjTrades);

            AlorSocketSubscription myTradesSub = new AlorSocketSubscription();
            myTradesSub.SubType = AlorSubType.MyTrades;
            myTradesSub.Guid = subObjTrades.guid;

            _subscriptionsPortfolio.Add(myTradesSub);
            _webSocketPortfolio.Send(messageTradeSub);

            Thread.Sleep(1000);

            // orders subscription

            RequestSocketSubscribleOrders subObjOrders = new RequestSocketSubscribleOrders();
            subObjOrders.guid = GetGuid();
            subObjOrders.token = _apiTokenReal;
            subObjOrders.portfolio = portfolioName;

            string messageOrderSub = JsonConvert.SerializeObject(subObjOrders);

            AlorSocketSubscription ordersSub = new AlorSocketSubscription();
            ordersSub.SubType = AlorSubType.Orders;
            ordersSub.Guid = subObjOrders.guid;

            _subscriptionsPortfolio.Add(ordersSub);
            _webSocketPortfolio.Send(messageOrderSub);

            Thread.Sleep(1000);

            // portfolio subscription

            RequestSocketSubscriblePoftfolio subObjPortf = new RequestSocketSubscriblePoftfolio();
            subObjPortf.guid = GetGuid();
            subObjPortf.token = _apiTokenReal;
            subObjPortf.portfolio = portfolioName;

            string messagePortfolioSub = JsonConvert.SerializeObject(subObjPortf);

            AlorSocketSubscription portfSub = new AlorSocketSubscription();
            portfSub.SubType = AlorSubType.Porfolio;
            portfSub.ServiceInfo = portfolioName;
            portfSub.Guid = subObjPortf.guid;

            _subscriptionsPortfolio.Add(portfSub);
            _webSocketPortfolio.Send(messagePortfolioSub);

            Thread.Sleep(1000);

            // positions subscription

            RequestSocketSubscriblePositions subObjPositions = new RequestSocketSubscriblePositions();
            subObjPositions.guid = GetGuid();
            subObjPositions.token = _apiTokenReal;
            subObjPositions.portfolio = portfolioName;

            string messagePositionsSub = JsonConvert.SerializeObject(subObjPositions);

            AlorSocketSubscription positionsSub = new AlorSocketSubscription();
            positionsSub.SubType = AlorSubType.Positions;
            positionsSub.ServiceInfo = portfolioName;
            positionsSub.Guid = subObjPositions.guid;

            _subscriptionsPortfolio.Add(positionsSub);
            _webSocketPortfolio.Send(messagePositionsSub);
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketData_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Data activated", LogMessageType.System);
            _socketDataIsActive = true;
            CheckActivationSockets();
        }

        private void WebSocketData_Closed(object sender, EventArgs e)
        {
            try
            {
                SendLogMessage("Connection Closed by Alor. WebSocket Data Closed Event", LogMessageType.Error);

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketData_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            try
            {
                var error = e;

                if (error.Exception != null)
                {
                    SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketData_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(e.Message))
                {
                    return;
                }
                if (e.Message.Length == 4)
                { // pong message
                    return;
                }

                if (e.Message.StartsWith("{\"requestGuid"))
                {
                    return;
                }

                if (WebSocketDataMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                WebSocketDataMessage.Enqueue(e.Message);
            }
            catch (Exception error)
            {
                SendLogMessage("Trade socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPortfolio_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Portfolio activated", LogMessageType.System);
            _socketPortfolioIsActive = true;
            CheckActivationSockets();
        }

        private void _webSocketPortfolio_Closed(object sender, EventArgs e)
        {
            try
            {
                SendLogMessage("Connection Closed by Alor. WebSocket Portfolio Closed Event", LogMessageType.Error);

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPortfolio_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            try
            {
                var error = e;

                if (error.Exception != null)
                {
                    SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Portfolio socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPortfolio_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(e.Message))
                {
                    return;
                }
                if (e.Message.Length == 4)
                { // pong message
                    return;
                }

                if (e.Message.StartsWith("{\"requestGuid"))
                {
                    return;
                }

                if (WebSocketPortfolioMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                WebSocketPortfolioMessage.Enqueue(e.Message);
            }
            catch (Exception error)
            {
                SendLogMessage("Portfolio socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket Security subscrible

        private RateGate rateGateSubscrible = new RateGate(1, TimeSpan.FromMilliseconds(50));

        List<Security> _subscribledSecurities = new List<Security>();

        public void Subscrible(Security security)
        {
            try
            {
                for (int i = 0; i < _subscribledSecurities.Count; i++)
                {
                    if (_subscribledSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }

                rateGateSubscrible.WaitToProceed();

                _subscribledSecurities.Add(security);

                // trades subscription

                //curl - X GET "https://apidev.alor.ru/md/v2/Securities/MOEX/LKOH/alltrades?format=Simple&from=1593430060&to=1593430560&fromId=7796897024&toId=7796897280&take=10" - H "accept: application/json"

                RequestSocketSubscribleTrades subObjTrades = new RequestSocketSubscribleTrades();
                subObjTrades.code = security.Name;
                subObjTrades.guid = GetGuid();
                subObjTrades.token = _apiTokenReal;
                string messageTradeSub = JsonConvert.SerializeObject(subObjTrades);

                AlorSocketSubscription tradeSub = new AlorSocketSubscription();
                tradeSub.SubType = AlorSubType.Trades;
                tradeSub.ServiceInfo = security.Name;
                tradeSub.Guid = subObjTrades.guid;
                _subscriptionsData.Add(tradeSub);

                _webSocketData.Send(messageTradeSub);

                // market depth subscription

                RequestSocketSubscribleMarketDepth subObjMarketDepth = new RequestSocketSubscribleMarketDepth();
                subObjMarketDepth.code = security.Name;
                subObjMarketDepth.guid = GetGuid();
                subObjMarketDepth.token = _apiTokenReal;

                AlorSocketSubscription mdSub = new AlorSocketSubscription();
                mdSub.SubType = AlorSubType.MarketDepth;
                mdSub.ServiceInfo = security.Name;
                mdSub.Guid = subObjMarketDepth.guid;
                _subscriptionsData.Add(mdSub);

                string messageMdSub = JsonConvert.SerializeObject(subObjMarketDepth);

                _webSocketData.Send(messageMdSub);

            }
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(),LogMessageType.Error);
            }
        }

        #endregion

        #region 9 WebSocket parsing the messages

        private List<AlorSocketSubscription> _subscriptionsData = new List<AlorSocketSubscription>();

        private List<AlorSocketSubscription> _subscriptionsPortfolio = new List<AlorSocketSubscription>();

        private ConcurrentQueue<string> WebSocketDataMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> WebSocketPortfolioMessage = new ConcurrentQueue<string>();

        private void DataMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (WebSocketDataMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    WebSocketDataMessage.TryDequeue(out message);
                    
                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Equals("pong"))
                    {
                        continue;
                    }

                    SoketMessageBase baseMessage = 
                        JsonConvert.DeserializeAnonymousType(message, new SoketMessageBase());

                    if(baseMessage == null 
                        || string.IsNullOrEmpty(baseMessage.guid))
                    {
                        continue;
                    }

                    for(int i = 0;i < _subscriptionsData.Count;i++)
                    {
                        if (_subscriptionsData[i].Guid != baseMessage.guid)
                        {
                            continue;
                        }

                        if (_subscriptionsData[i].SubType == AlorSubType.Trades)
                        {
                            UpDateTrade(baseMessage.data.ToString(), _subscriptionsData[i].ServiceInfo);
                            break;
                        }
                        else if (_subscriptionsData[i].SubType == AlorSubType.MarketDepth)
                        {
                            UpDateMarketDepth(message, _subscriptionsData[i].ServiceInfo);
                            break;
                        }
                    }
                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpDateTrade(string data, string secName)
        {
            QuotesAlor baseMessage =
            JsonConvert.DeserializeAnonymousType(data, new QuotesAlor());

            if(string.IsNullOrEmpty(baseMessage.timestamp))
            {
                return;
            }

            Trade trade = new Trade();
            trade.SecurityNameCode = baseMessage.symbol;
            trade.Price = baseMessage.price.ToDecimal();
            trade.Time = ConvertToDateTimeFromUnixFromMilliseconds(baseMessage.timestamp);
            trade.Id = baseMessage.id;
            trade.Side = Side.Buy;
            trade.Volume = 1;

            if (NewTradesEvent != null)
            {
                NewTradesEvent(trade);
            }
        }

        private void UpDateMarketDepth(string data, string secName)
        {
            MarketDepthFullMessage baseMessage =
            JsonConvert.DeserializeAnonymousType(data, new MarketDepthFullMessage());

            if (baseMessage.data.bids == null ||
                baseMessage.data.asks == null)
            {
                return;
            }

            if (baseMessage.data.bids.Count == 0 ||
                baseMessage.data.asks.Count == 0)
            {
                return;
            }

            MarketDepth depth = new MarketDepth();
            depth.SecurityNameCode = secName;
            depth.Time = ConvertToDateTimeFromUnixFromMilliseconds(baseMessage.data.ms_timestamp);

            for (int i = 0; i < baseMessage.data.bids.Count; i++)
            {
                MarketDepthLevel newBid = new MarketDepthLevel();
                newBid.Price = baseMessage.data.bids[i].price.ToDecimal();
                newBid.Bid = baseMessage.data.bids[i].volume.ToDecimal();
                depth.Bids.Add(newBid);
            }

            for (int i = 0; i < baseMessage.data.asks.Count; i++)
            {
                MarketDepthLevel newAsk = new MarketDepthLevel();
                newAsk.Price = baseMessage.data.asks[i].price.ToDecimal();
                newAsk.Ask = baseMessage.data.asks[i].volume.ToDecimal();
                depth.Asks.Add(newAsk);
            }

            if(_lastMdTime != DateTime.MinValue &&
                _lastMdTime >= depth.Time)
            {
                depth.Time = _lastMdTime.AddMilliseconds(1);
            }

            _lastMdTime = depth.Time;

            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(depth);
            }
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        private void PortfolioMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (WebSocketPortfolioMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    WebSocketPortfolioMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Equals("pong"))
                    {
                        continue;
                    }

                    SoketMessageBase baseMessage =
                        JsonConvert.DeserializeAnonymousType(message, new SoketMessageBase());

                    if (baseMessage == null
                        || string.IsNullOrEmpty(baseMessage.guid))
                    {
                        continue;
                    }

                    for (int i = 0; i < _subscriptionsPortfolio.Count; i++)
                    {
                        if (_subscriptionsPortfolio[i].Guid != baseMessage.guid)
                        {
                            continue;
                        }

                        if (_subscriptionsPortfolio[i].SubType == AlorSubType.Porfolio)
                        {
                            UpDateMyPortfolio(baseMessage.data.ToString(), _subscriptionsPortfolio[i].ServiceInfo);
                            break;
                        }
                        else if (_subscriptionsPortfolio[i].SubType == AlorSubType.Positions)
                        {
                            UpDatePositionOnBoard(baseMessage.data.ToString(), _subscriptionsPortfolio[i].ServiceInfo);
                            break;
                        }
                        else if (_subscriptionsPortfolio[i].SubType == AlorSubType.MyTrades)
                        {
                            UpDateMyTrade(baseMessage.data.ToString());
                            break;
                        }
                        else if (_subscriptionsPortfolio[i].SubType == AlorSubType.Orders)
                        {
                            UpDateMyOrder(baseMessage.data.ToString());
                            break;
                        }
                    }
                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpDateMyTrade(string data)
        {
            MyTradeAlor baseMessage =
            JsonConvert.DeserializeAnonymousType(data, new MyTradeAlor());

            MyTrade trade = new MyTrade();

            trade.SecurityNameCode = baseMessage.symbol;
            trade.Price = baseMessage.price.ToDecimal();
            trade.Volume = baseMessage.qty.ToDecimal();
            trade.NumberOrderParent = baseMessage.orderno;
            trade.NumberTrade = baseMessage.id;
            trade.Time = ConvertToDateTimeFromTimeAlorData(baseMessage.date);
           
            if(baseMessage.side == "buy")
            {
                trade.Side = Side.Buy;
            }
            else
            {
                trade.Side = Side.Sell;
            }

            if (MyTradeEvent != null)
            {
                MyTradeEvent(trade);
            }
        }

        private void UpDatePositionOnBoard(string data, string portfolioName)
        {
            PositionOnBoardAlor baseMessage =
                       JsonConvert.DeserializeAnonymousType(data, new PositionOnBoardAlor());

            Portfolio portf = null;

            for (int i = 0; i < _myPortfolious.Count; i++)
            {
                string realPortfName = _myPortfolious[i].Number.Split('_')[0];
                if (realPortfName == portfolioName)
                {
                    portf = _myPortfolious[i];
                    break;
                }
            }

            if (portf == null)
            {
                return;
            }

            PositionOnBoard newPos = new PositionOnBoard();
            newPos.PortfolioName = portf.Number;
            newPos.ValueCurrent = baseMessage.qty.ToDecimal();
            newPos.SecurityNameCode = baseMessage.symbol;
            portf.SetNewPosition(newPos);

            if (PortfolioEvent != null)
            {
                PortfolioEvent(_myPortfolious);
            }
        }

        private void UpDateMyOrder(string data)
        {
            OrderAlor baseMessage =
            JsonConvert.DeserializeAnonymousType(data, new OrderAlor());

            if(string.IsNullOrEmpty(baseMessage.comment))
            {
                return;
            }

            Order order = new Order();

            order.SecurityNameCode = baseMessage.symbol;
            order.Volume = baseMessage.qty.ToDecimal();

            bool securityInArray = false;
            for(int i = 0;i < _securitiesAndPortfolious.Count;i++)
            {
                if (_securitiesAndPortfolious[i].Security == order.SecurityNameCode)
                {
                    order.PortfolioNumber = _securitiesAndPortfolious[i].Portfolio;
                    securityInArray = true;
                    break;
                }
            }

            if(securityInArray == false)
            {
                order.PortfolioNumber = baseMessage.exchange;
            }

            if(baseMessage.type == "limit")
            {
                order.Price = baseMessage.price.ToDecimal();
                order.TypeOrder = OrderPriceType.Limit;
            }
            else if(baseMessage.type == "market")
            {
                order.TypeOrder = OrderPriceType.Market;
            }
            
            try
            {
                order.NumberUser = Convert.ToInt32(baseMessage.comment);
            }
            catch
            {
                return;
            }
            
            order.NumberMarket = baseMessage.id;

            order.TimeCallBack = ConvertToDateTimeFromTimeAlorData(baseMessage.transTime);

            if (baseMessage.side == "buy")
            {
                order.Side = Side.Buy;
            }
            else
            {
                order.Side = Side.Sell;
            }

            //working - На исполнении
            //filled - Исполнена
            //canceled - Отменена
            //rejected - Отклонена

            if (baseMessage.status == "working")
            {
                order.State = OrderStateType.Activ;
            }
            else if (baseMessage.status == "filled")
            {
                order.State = OrderStateType.Done;
            }
            else if (baseMessage.status == "canceled")
            {
                lock (_changePriceOrdersArrayLocker)
                {
                    DateTime now = DateTime.Now;
                    for (int i = 0; i < _changePriceOrders.Count; i++)
                    {
                        if (_changePriceOrders[i].TimeChangePriceOrder.AddSeconds(2) < now)
                        {
                            _changePriceOrders.RemoveAt(i);
                            i--;
                            continue;
                        }

                        if (_changePriceOrders[i].MarketId == order.NumberMarket)
                        {
                            return;
                        }
                    }
                }

                if(string.IsNullOrEmpty(baseMessage.filledQtyUnits))
                {
                    order.State = OrderStateType.Cancel;
                }
                else if(baseMessage.filledQtyUnits == "0")
                {
                    order.State = OrderStateType.Cancel;
                }
                else
                {
                    try
                    {
                        decimal volFilled = baseMessage.filledQtyUnits.ToDecimal();

                        if(volFilled > 0)
                        {
                            order.State = OrderStateType.Done;
                        }
                        else
                        {
                            order.State = OrderStateType.Cancel;
                        }
                    }
                    catch
                    {
                        order.State = OrderStateType.Cancel;
                    }
                }
            }
            else if (baseMessage.status == "rejected")
            {
                order.State = OrderStateType.Fail;
            }

            lock (_sendOrdersArrayLocker)
            {
                for (int i = 0; i < _sendOrders.Count; i++)
                {
                    if (_sendOrders[i] == null)
                    {
                        continue;
                    }

                    if (_sendOrders[i].NumberUser == order.NumberUser)
                    {
                        order.TypeOrder = _sendOrders[i].TypeOrder;
                        break;
                    }
                }
            }

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        private void UpDateMyPortfolio(string data, string portfolioName)
        {
            AlorPortfolioSocket baseMessage =
            JsonConvert.DeserializeAnonymousType(data, new AlorPortfolioSocket());

            Portfolio portf = null;

            for(int i = 0;i < _myPortfolious.Count;i++)
            {
                string realPortfName = _myPortfolious[i].Number.Split('_')[0];
                if (realPortfName == portfolioName)
                {
                    portf = _myPortfolious[i];
                    break;
                }
            }

            if(portf == null)
            {
                return;
            }

            portf.ValueBegin = baseMessage.portfolioLiquidationValue.ToDecimal();
            portf.ValueCurrent = baseMessage.portfolioLiquidationValue.ToDecimal();
            portf.Profit = baseMessage.profit.ToDecimal();
            

            if (PortfolioEvent != null)
            {
                PortfolioEvent(_myPortfolious);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        #endregion

        #region 10 Trade

        private RateGate rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate rateGateChangePriceOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private List<AlorSecuritiesAndPortfolious> _securitiesAndPortfolious = new List<AlorSecuritiesAndPortfolious>();

        private List<Order> _sendOrders = new List<Order>();

        private string _sendOrdersArrayLocker = "alorSendOrdersArrayLocker";

        public void SendOrder(Order order)
        {
            rateGateSendOrder.WaitToProceed();

            try
            {
                if(order.TypeOrder == OrderPriceType.Market)
                {
                    lock (_sendOrdersArrayLocker)
                    {
                        _sendOrders.Add(order);

                        while (_sendOrders.Count > 100)
                        {
                            _sendOrders.RemoveAt(0);
                        }
                    }
                }

                string endPoint = "";

                if(order.TypeOrder == OrderPriceType.Limit)
                {
                    endPoint = "/commandapi/warptrans/TRADE/v2/client/orders/actions/limit";
                }
                else if (order.TypeOrder == OrderPriceType.Market)
                {
                    endPoint = "/commandapi/warptrans/TRADE/v2/client/orders/actions/market";
                }

                RestRequest requestRest = new RestRequest(endPoint, Method.POST);
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                requestRest.AddHeader("X-ALOR-REQID", order.NumberUser.ToString());
                requestRest.AddHeader("accept", "application/json");

                if(order.TypeOrder == OrderPriceType.Market)
                {
                    MarketOrderAlorRequest body = GetMarketRequestObj(order);
                    requestRest.AddJsonBody(body);
                }
                else if(order.TypeOrder == OrderPriceType.Limit)
                {
                    LimitOrderAlorRequest body = GetLimitRequestObj(order);
                    requestRest.AddJsonBody(body);
                }

                RestClient client = new RestClient(_restApiHost);

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    bool isInArray = false;
                    for(int i = 0;i < _securitiesAndPortfolious.Count;i++)
                    {
                        if (_securitiesAndPortfolious[i].Security == order.SecurityNameCode)
                        {
                            isInArray = true;
                            break;
                        }
                    }
                    if(isInArray == false)
                    {
                        AlorSecuritiesAndPortfolious newValue = new AlorSecuritiesAndPortfolious();
                        newValue.Security = order.SecurityNameCode;
                        newValue.Portfolio = order.PortfolioNumber;
                        _securitiesAndPortfolious.Add(newValue);
                    }

                    return;
                }
                else
                {
                    SendLogMessage("Order Fail. Status: "
                        + response.StatusCode + "  " + order.SecurityNameCode , LogMessageType.Error);

                    if(response.Content != null)
                    {
                        SendLogMessage("Fail reasons: "
                      + response.Content, LogMessageType.Error);
                    }

                    order.State = OrderStateType.Fail;

                    if(MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private LimitOrderAlorRequest GetLimitRequestObj(Order order)
        {
            LimitOrderAlorRequest requestObj = new LimitOrderAlorRequest();

            if(order.Side == Side.Buy)
            {
                requestObj.side = "buy";
            }
            else
            {
                requestObj.side = "sell";

            }
            requestObj.type = "limit";
            requestObj.quantity = Convert.ToInt32(order.Volume);
            requestObj.price = order.Price;
            requestObj.comment = order.NumberUser.ToString();
            requestObj.instrument = new instrumentAlor();
            requestObj.instrument.symbol = order.SecurityNameCode;
            requestObj.user = new User();
            requestObj.user.portfolio = order.PortfolioNumber.Split('_')[0];

            return requestObj;
        }

        private MarketOrderAlorRequest GetMarketRequestObj(Order order)
        {
            MarketOrderAlorRequest requestObj = new MarketOrderAlorRequest();

            if (order.Side == Side.Buy)
            {
                requestObj.side = "buy";
            }
            else
            {
                requestObj.side = "sell";
            }
            requestObj.type = "market";
            requestObj.quantity = Convert.ToInt32(order.Volume);
            requestObj.comment = order.NumberUser.ToString();
            requestObj.instrument = new instrumentAlor();
            requestObj.instrument.symbol = order.SecurityNameCode;
            requestObj.user = new User();
            requestObj.user.portfolio = order.PortfolioNumber.Split('_')[0];

            return requestObj;
        }

        List<AlorChangePriceOrder> _changePriceOrders = new List<AlorChangePriceOrder>();

        private string _changePriceOrdersArrayLocker = "cangePriceArrayLocker";

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            try
            {
                rateGateChangePriceOrder.WaitToProceed();

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    SendLogMessage("Can`t change price to market order", LogMessageType.Error);
                    return;
                }
                
                string endPoint = "/commandapi/warptrans/TRADE/v2/client/orders/actions/limit/";

                endPoint += order.NumberMarket;

                RestRequest requestRest = new RestRequest(endPoint, Method.PUT);
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                requestRest.AddHeader("X-ALOR-REQID", order.NumberUser.ToString() + GetGuid()); ;
                requestRest.AddHeader("accept", "application/json");

                LimitOrderAlorRequest body = GetLimitRequestObj(order);
                body.price = newPrice;

                int qty = Convert.ToInt32(order.Volume - order.VolumeExecute);

                if(qty <= 0 ||
                    order.State != OrderStateType.Activ)
                {
                    SendLogMessage("Can`t change price to order. It`s don`t in Activ state", LogMessageType.Error);
                    return;
                }

                requestRest.AddJsonBody(body);
                
                RestClient client = new RestClient(_restApiHost);

                AlorChangePriceOrder alorChangePriceOrder = new AlorChangePriceOrder();
                alorChangePriceOrder.MarketId = order.NumberMarket.ToString();
                alorChangePriceOrder.TimeChangePriceOrder = DateTime.Now;

                lock(_changePriceOrdersArrayLocker)
                {
                    _changePriceOrders.Add(alorChangePriceOrder);
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    SendLogMessage("Order change price. New price: " + newPrice
                        + "  " + order.SecurityNameCode, LogMessageType.System);

                    order.Price = newPrice;
                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }

                    return;
                }
                else
                {
                    SendLogMessage("Order Fail. Status: "
                        + response.StatusCode + "  " + order.SecurityNameCode, LogMessageType.Error);

                    if (response.Content != null)
                    {
                        SendLogMessage("Fail reasons: "
                      + response.Content, LogMessageType.Error);
                    }

                    order.State = OrderStateType.Fail;

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        List<string> _cancelOrderNums = new List<string>();

        public void CancelOrder(Order order)
        {
            rateGateCancelOrder.WaitToProceed();

            //curl -X DELETE "/commandapi/warptrans/TRADE/v2/client/orders/93713183?portfolio=D39004&exchange=MOEX&stop=false&format=Simple" -H "accept: application/json"

            try
            {
                int countTryRevokeOrder = 0;

                for(int i = 0; i< _cancelOrderNums.Count;i++)
                {
                    if (_cancelOrderNums[i].Equals(order.NumberMarket))
                    {
                        countTryRevokeOrder++;
                    }
                }

                if(countTryRevokeOrder >= 2)
                {
                    SendLogMessage("Order cancel request error. The order has already been revoked " + order.SecurityClassCode, LogMessageType.Error);
                    return;
                }

                _cancelOrderNums.Add(order.NumberMarket);

                while(_cancelOrderNums.Count > 100)
                {
                    _cancelOrderNums.RemoveAt(0);
                }

                string portfolio = order.PortfolioNumber.Split('_')[0];

                string endPoint 
                    = $"/commandapi/warptrans/TRADE/v2/client/orders/{order.NumberMarket}?portfolio={portfolio}&exchange=MOEX&stop=false&jsonResponse=true&format=Simple";

                RestRequest requestRest = new RestRequest(endPoint, Method.DELETE);
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                requestRest.AddHeader("accept", "application/json");

                RestClient client = new RestClient(_restApiHost);

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
                else
                {
                    SendLogMessage("Order cancel request error. Status: "
                        + response.StatusCode + "  " + order.SecurityClassCode, LogMessageType.Error);

                    if (response.Content != null)
                    {
                        SendLogMessage("Fail reasons: "
                      + response.Content, LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order cancel request error " + exception.ToString(), LogMessageType.Error);
            }

        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {

        }

        #endregion

        #region 11 Helpers

        public long ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return Convert.ToInt64(diff.TotalSeconds);
        }

        private DateTime ConvertToDateTimeFromUnixFromSeconds(string seconds)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime result = origin.AddSeconds(seconds.ToDouble()).ToLocalTime();

            return result;
        }

        private DateTime ConvertToDateTimeFromUnixFromMilliseconds(string seconds)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime result = origin.AddMilliseconds(seconds.ToDouble());

            return result.ToLocalTime();
        }

        private DateTime ConvertToDateTimeFromTimeAlorData(string alorTime)
        {
            //"time": "2018-08-07T08:40:03.445Z",

            string date = alorTime.Split('T')[0];

            int year = Convert.ToInt32(date.Substring(0,4));
            int month = Convert.ToInt32(date.Substring(5, 2));
            int day = Convert.ToInt32(date.Substring(8, 2));

            string time = alorTime.Split('T')[1];

            int hour = Convert.ToInt32(time.Substring(0, 2));

            if (alorTime.EndsWith("+00:00"))
            {
                hour += 3;
            }

            if (alorTime.EndsWith("+01:00"))
            {
                hour += 2;
            }

            if (alorTime.EndsWith("+02:00"))
            {
                hour += 1;
            }
            int minute = Convert.ToInt32(time.Substring(3, 2));
            int second = Convert.ToInt32(time.Substring(6, 2));
            int ms = Convert.ToInt32(time.Substring(10, 3));

            DateTime dateTime = new DateTime(year, month, day, hour, minute, second, ms);

            return dateTime;
        }

        #endregion

        #region 12 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public enum AlorAvailableExchanges
    {
        MOEX,
        SPBX
    }

    public class AlorSocketSubscription
    {
        public string Guid;

        public AlorSubType SubType;

        public string ServiceInfo;
    }

    public class AlorChangePriceOrder
    {
        public string MarketId;

        public DateTime TimeChangePriceOrder;
    }

    public class AlorSecuritiesAndPortfolious
    {
       public string Security;

       public string Portfolio;
    }

    public enum AlorSubType
    {
        Trades,
        MarketDepth,
        Porfolio,
        Positions,
        Orders,
        MyTrades
    }
}