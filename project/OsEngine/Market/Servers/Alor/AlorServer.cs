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
using OsEngine.Entity.WebSocketOsEngine;

namespace OsEngine.Market.Servers.Alor
{
    public class AlorServer : AServer
    {
        public AlorServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            AlorServerRealization realization = new AlorServerRealization();
            ServerRealization = realization;

            CreateParameterPassword(OsLocalization.Market.ServerParamToken, ""); //
            CreateParameterString(OsLocalization.Market.Label112, "");
            CreateParameterString(OsLocalization.Market.Label113, "");
            CreateParameterString(OsLocalization.Market.Label114, "");
            CreateParameterString(OsLocalization.Market.Label115, "");
            CreateParameterBoolean(OsLocalization.Market.UseStock, true);
            CreateParameterBoolean(OsLocalization.Market.UseFutures, true);
            CreateParameterBoolean(OsLocalization.Market.UseCurrency, true);
            CreateParameterBoolean(OsLocalization.Market.UseOptions, false);
            CreateParameterBoolean(OsLocalization.Market.UseOther, false);
            CreateParameterEnum(OsLocalization.Market.ServerParam13, "10", new List<string> { "1", "10", "20"});
            CreateParameterBoolean(OsLocalization.Market.IgnoreMorningAuctionTrades, false);
        }
    }

    public class AlorServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public AlorServerRealization()
        {
            Thread worker = new Thread(ConnectionCheckThread);
            worker.Name = "AlorCheckAlive";
            worker.Start();

            Thread worker2 = new Thread(DataMessageReader);
            worker2.Name = "AlorDataMessageReader";
            worker2.Start();

            Thread worker3 = new Thread(PortfolioMessageReader);
            worker3.Name = "AlorPortfolioMessageReader";
            worker3.Start();
        }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                _myProxy = proxy;
                _securities.Clear();
                _myPortfolios.Clear();
                _subscribedSecurities.Clear();
                _lastGetLiveTimeTokenTime = DateTime.MinValue;

                SendLogMessage("Start Alor Connection", LogMessageType.System);

                _apiTokenRefresh = ((ServerParameterPassword)ServerParameters[0]).Value;
                _portfolioSpotId = ((ServerParameterString)ServerParameters[1]).Value;
                _portfolioFutId = ((ServerParameterString)ServerParameters[2]).Value;
                _portfolioCurrencyId = ((ServerParameterString)ServerParameters[3]).Value;
                _portfolioSpareId = ((ServerParameterString)ServerParameters[4]).Value;
                _ignoreMorningAuctionTrades = ((ServerParameterBool)ServerParameters[11]).Value;

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

                if (_lastGetLiveTimeTokenTime.AddMinutes(20) < DateTime.Now)
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

        DateTime _lastGetLiveTimeTokenTime = DateTime.MinValue;

        private bool GetCurSessionToken()
        {
            try
            {
                string endPoint = "/refresh?token=" + _apiTokenRefresh;
                RestRequest requestRest = new RestRequest(endPoint, Method.POST);
                RestClient client = new RestClient(_oauthApiHost);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content;
                    TokenResponse newLiveToken = JsonConvert.DeserializeAnonymousType(content, new TokenResponse());

                    _lastGetLiveTimeTokenTime = DateTime.Now;
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
            _myPortfolios.Clear();
            _lastGetLiveTimeTokenTime = DateTime.MinValue;

            DeleteWebSocketConnection();

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

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        private readonly string _restApiHost = "https://api.alor.ru";
        private readonly string _oauthApiHost = "https://oauth.alor.ru";

        private bool _useStock = false;
        private bool _useFutures = false;
        private bool _useOptions = false;
        private bool _useCurrency = false;
        private bool _useOther = false;
        private bool _ignoreMorningAuctionTrades = true; // ignore trades before 7:00 MSK for stocks and before 9:00 for futures

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

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

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
                    newSecurity.DecimalsVolume = 0;
                    newSecurity.VolumeStep = 1;
                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.symbol + "_" + item.board;

                    newSecurity.Lot = item.lotsize.ToDecimal();

                    if (instrumentType == SecurityType.Futures
                        || instrumentType == SecurityType.Option)
                    {
                        newSecurity.UsePriceStepCostToCalculateVolume = true;
                    }

                    if (newSecurity.SecurityType == SecurityType.Option)
                    {
                        newSecurity.MarginBuy = item.marginbuy.ToDecimal();
                        newSecurity.MarginSell = item.marginsell.ToDecimal();

                        if(item.type != null &&
                            item.type.Contains("Прем. европ. Call "))
                        {
                            newSecurity.NameClass = "Option_Eur";
                            newSecurity.OptionType = OptionType.Call;
                            string strike = item.type.Replace("Прем. европ. Call ", "");
                            strike = strike.Split(' ')[0];
                            newSecurity.Strike = strike.ToDecimal();
                        }
                        else if (item.type != null &&
                            item.type.Contains("Нед. прем. европ. Call "))
                        {
                            newSecurity.NameClass = "Option_Eur";
                            newSecurity.OptionType = OptionType.Call;
                            string strike = item.type.Replace("Нед. прем. европ. Call ", "");
                            strike = strike.Split(' ')[0];
                            newSecurity.Strike = strike.ToDecimal();
                        }
                        else if (item.type != null &&
                            item.type.Contains("Прем. европ. Put "))
                        {
                            newSecurity.NameClass = "Option_Eur";
                            newSecurity.OptionType = OptionType.Put;
                            string strike = item.type.Replace("Прем. европ. Put ", "");
                            strike = strike.Split(' ')[0];
                            newSecurity.Strike = strike.ToDecimal();
                        }
                        else if (item.type != null &&
                            item.type.Contains("Нед. прем. европ. Put "))
                        {
                            newSecurity.NameClass = "Option_Eur";
                            newSecurity.OptionType = OptionType.Put;
                            string strike = item.type.Replace("Нед. прем. европ. Put ", "");
                            strike = strike.Split(' ')[0];
                            newSecurity.Strike = strike.ToDecimal();
                        }
                        else if (item.type != null &&
                            item.type.Contains("Нед. марж. амер. Call "))
                        {
                            newSecurity.NameClass = "Option_Us";
                            newSecurity.OptionType = OptionType.Call;
                            string strike = item.type.Replace("Нед. марж. амер. Call ", "");
                            strike = strike.Split(' ')[0];
                            newSecurity.Strike = strike.ToDecimal();
                        }
                        else if (item.type != null &&
                            item.type.Contains("Марж. амер. Call "))
                        {
                            newSecurity.NameClass = "Option_Us";
                            newSecurity.OptionType = OptionType.Call;
                            string strike = item.type.Replace("Марж. амер. Call ", "");
                            strike = strike.Split(' ')[0];
                            newSecurity.Strike = strike.ToDecimal();
                        }
                        else if (item.type != null &&
                            item.type.Contains("Марж. амер. Put "))
                        {
                            newSecurity.NameClass = "Option_Us";
                            newSecurity.OptionType = OptionType.Put;
                            string strike = item.type.Replace("Марж. амер. Put ", "");
                            strike = strike.Split(' ')[0];
                            newSecurity.Strike = strike.ToDecimal();
                        }
                        else if (item.type != null &&
                            item.type.Contains("Нед. марж. амер. Put "))
                        {
                            newSecurity.NameClass = "Option_Us";
                            newSecurity.OptionType = OptionType.Put;
                            string strike = item.type.Replace("Нед. марж. амер. Put ", "");
                            strike = strike.Split(' ')[0];
                            newSecurity.Strike = strike.ToDecimal();
                        }
                        else
                        {

                        }

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
                        newSecurity.MarginBuy = item.marginbuy.ToDecimal();
                        newSecurity.MarginSell = item.marginsell.ToDecimal();
                    }
                    else if (newSecurity.SecurityType == SecurityType.CurrencyPair)
                    {
                        newSecurity.NameClass = "Currency";
                    }
                    else if (item.type == "CS")
                    {
                        if (item.board == "TQBR")
                        {
                            newSecurity.NameClass = "Stock";
                        }
                        else if (item.board == "FQBR")
                        {
                            newSecurity.NameClass = "Stock World";
                        }
                        else 
                        {
                            newSecurity.NameClass = "Stock";
                        }
                    }
		            else if (item.type == "CORP")
                    {
                        newSecurity.NameClass = "Bond";
                    }
                    else if (item.type == "PS")
                    {
                        newSecurity.NameClass = "Stock";
                    }
                    else if (newSecurity.SecurityType == SecurityType.Fund)
                    {
                        newSecurity.NameClass = "Fund";
                    }
                    else
                    {
                        newSecurity.NameClass = item.type;
                    }

                    if (string.IsNullOrEmpty(item.cancellation) == false 
                        && (newSecurity.SecurityType == SecurityType.Futures ||
                        newSecurity.SecurityType == SecurityType.Option ||
                        newSecurity.NameClass == "Futures spread"))
                    {
                        int year = Convert.ToInt32(item.cancellation.Substring(0, 4));
                        int month = Convert.ToInt32(item.cancellation.Substring(5, 2));
                        int day = Convert.ToInt32(item.cancellation.Substring(8, 2));

                        newSecurity.Expiration = new DateTime(year, month, day);
                    }

                    newSecurity.NameId = item.shortname;
                   
                    newSecurity.Decimals = GetDecimals(item.minstep.ToDecimal());
                    newSecurity.PriceStep = item.minstep.ToDecimal();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.State = SecurityStateType.Activ;

                    if (newSecurity.SecurityType == SecurityType.Futures 
                        || newSecurity.SecurityType == SecurityType.Option)
                    {
                        newSecurity.PriceStepCost = item.pricestep.ToDecimal();

                        if(newSecurity.PriceStepCost <= 0)
                        {
                            newSecurity.PriceStepCost = newSecurity.PriceStep;
                        }
                    }

                    if(string.IsNullOrEmpty(item.priceMax) == false)
                    {
                        newSecurity.PriceLimitHigh = item.priceMax.ToDecimal();
                    }
                    if (string.IsNullOrEmpty(item.priceMin) == false)
                    {
                        newSecurity.PriceLimitLow = item.priceMin.ToDecimal();
                    }

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

            if (cfiCode.StartsWith("F"))
            {
                return SecurityType.Futures;
            }
            else if (cfiCode.StartsWith("O"))
            {
                return SecurityType.Option;
            }
            else if (cfiCode.StartsWith("ES") || cfiCode.StartsWith("EP"))
            {
                return SecurityType.Stock;
            }
            else if (cfiCode.StartsWith("DB"))
            { 
                return SecurityType.Bond; 
            }
            else if(cfiCode.StartsWith("EUX"))
            {
                return SecurityType.Fund;
            }
            else if(security.description.Contains("Индекс"))
            {
                return SecurityType.Index;
            }

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
                case SecurityType.Fund when _useOther:
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

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

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

            if(_myPortfolios.Count != 0)
            {
                if(PortfolioEvent != null)
                {
                    PortfolioEvent(_myPortfolios);
                }
            }

            ActivatePortfolioSocket();
        }

        private void GetCurrentPortfolio(string portfolioId, string namePrefix)
        {
            try
            {
                string exchange = "MOEX";
                if (portfolioId.StartsWith("E"))
                {
                    exchange = "UNITED";
                }

                string endPoint = $"/md/v2/clients/{exchange}/{portfolioId}/summary?format=Simple";
                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                requestRest.AddHeader("accept", "application/json");

                RestClient client = new RestClient(_restApiHost);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content;
                    AlorPortfolioRest portfolio = JsonConvert.DeserializeAnonymousType(content, new AlorPortfolioRest());

                    ConvertToPortfolio(portfolio, portfolioId, namePrefix);
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
            newPortfolio.UnrealizedPnl = portfolio.profit.ToDecimal();
            _myPortfolios.Add(newPortfolio);
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

            if (endTime.DayOfWeek == DayOfWeek.Monday)
            {
                startTime = startTime.AddDays(-2);
            }
            if (endTime.DayOfWeek == DayOfWeek.Tuesday)
            {
                startTime = startTime.AddDays(-1);
            }

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

            DateTime requestedStartTime = startTime;

            List<Candle> candles = new List<Candle>();

            TimeSpan additionTime = TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 2500);

            DateTime endTimeReal = startTime.Add(additionTime);

            if (endTimeReal > endTime) 
                endTimeReal = endTime;

            while (startTime < endTime)
            {
                CandlesHistoryAlor history = GetHistoryCandle(security, timeFrameBuilder, startTime, endTimeReal);

                List<Candle> newCandles = ConvertToOsEngineCandles(history, timeFrameBuilder.TimeFrameTimeSpan.Days != 1);

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

                if (endTimeReal > endTime)
                    endTimeReal = endTime;
            }

            while (candles != null &&
                candles.Count != 0 && 
                candles[candles.Count - 1].TimeStart > endTime)
            {
                candles.RemoveAt(candles.Count - 1);
            }

            while (candles != null &&
                candles.Count != 0 && 
                candles[0].TimeStart < requestedStartTime)
            {
                candles.RemoveAt(0);
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

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

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

        private List<Candle> ConvertToOsEngineCandles(CandlesHistoryAlor candles, bool applyOffsetToMsk = true)
        {
            List<Candle> result = new List<Candle>();

            if(candles == null 
                || candles.history == null 
                || candles.history.Count == 0)
            {
                return result;
            }

            for(int i = 0;i < candles.history.Count;i++)
            {
                if(candles.history[i] == null)
                {
                    continue;
                }

                AlorCandle curCandle = candles.history[i];

                Candle newCandle = new Candle();
                newCandle.Open = curCandle.open.ToDecimal();
                newCandle.High = curCandle.high.ToDecimal();
                newCandle.Low = curCandle.low.ToDecimal();
                newCandle.Close = curCandle.close.ToDecimal();
                newCandle.Volume = curCandle.volume.ToDecimal();
                newCandle.TimeStart = ConvertToDateTimeFromUnixFromSeconds(curCandle.time, applyOffsetToMsk);

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
            return null; // так как указано, что данные не поддерживаются

           /* List<Trade> trades = new List<Trade>();

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

            return trades;*/
        }

        private TradesHistoryAlor GetHistoryTrades(Security security, DateTime startTime, DateTime endTime)
        {
            // /md/v2/Securities/MOEX/SBER/alltrades/history?instrumentGroup=TQBR&from=1593430060&to=1593430560&limit=100&offset=10&format=Simple

            string endPoint = "/md/v2/Securities/MOEX/" + security.Name;
            endPoint += "/alltrades/history?";

            endPoint += "instrumentGroup=" + security.NameFull.Split('_')[security.NameFull.Split('_').Length - 1];

            endPoint += "&from=" + ConvertToUnixTimestamp(startTime);
            endPoint += "&to=" + ConvertToUnixTimestamp(endTime);
            endPoint += "&limit=50000";
            endPoint += "&format=Simple";

            try
            {
                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                requestRest.AddHeader("accept", "application/json");
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                RestClient client = new RestClient(_restApiHost);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

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
            Guid newUid = Guid.NewGuid();
            return newUid.ToString();
        }

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
                    _webSocketData.EmitOnPing = true;
                    _webSocketData.OnOpen += WebSocketData_Opened;
                    _webSocketData.OnClose += WebSocketData_Closed;
                    _webSocketData.OnMessage += WebSocketData_MessageReceived;
                    _webSocketData.OnError += WebSocketData_Error;

                    if(_myProxy != null)
                    {
                        _webSocketData.SetProxy(_myProxy);
                    }

                    _webSocketData.ConnectAsync();

                    _webSocketPortfolio = new WebSocket(_wsHost);
                    _webSocketPortfolio.EmitOnPing = true;
                    _webSocketPortfolio.OnOpen += _webSocketPortfolio_Opened;
                    _webSocketPortfolio.OnClose += _webSocketPortfolio_Closed;
                    _webSocketPortfolio.OnMessage += _webSocketPortfolio_MessageReceived;
                    _webSocketPortfolio.OnError += _webSocketPortfolio_Error;

                    if (_myProxy != null)
                    {
                        _webSocketPortfolio.SetProxy(_myProxy);
                    }

                    _webSocketPortfolio.ConnectAsync();

                }

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
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
                            _webSocketData.OnOpen -= WebSocketData_Opened;
                            _webSocketData.OnClose -= WebSocketData_Closed;
                            _webSocketData.OnMessage -= WebSocketData_MessageReceived;
                            _webSocketData.OnError -= WebSocketData_Error;
                            _webSocketData.CloseAsync();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            _webSocketPortfolio.OnOpen -= _webSocketPortfolio_Opened;
                            _webSocketPortfolio.OnClose -= _webSocketPortfolio_Closed;
                            _webSocketPortfolio.OnMessage -= _webSocketPortfolio_MessageReceived;
                            _webSocketPortfolio.OnError -= _webSocketPortfolio_Error;
                            _webSocketPortfolio.CloseAsync();
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }
            catch
            {

            }
            finally
            {
                _webSocketData = null;
                _webSocketPortfolio = null;
            }
        }

        private bool _socketDataIsActive;

        private bool _socketPortfolioIsActive;

        private string _activationLocker = "activationLocker";

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
                lock(_activationLocker)
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        SendLogMessage("All sockets activated. Connect State", LogMessageType.System);
                        ServerStatus = ServerConnectStatus.Connect;
                        ConnectEvent();
                    }
                }
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

            RequestSocketSubscribeMyTrades subObjTrades = new RequestSocketSubscribeMyTrades();
            subObjTrades.guid = GetGuid();
            subObjTrades.token = _apiTokenReal;
            subObjTrades.portfolio = portfolioName;

            if (portfolioName.StartsWith("E"))
            {
                subObjTrades.exchange = "UNITED";
            }

            string messageTradeSub = JsonConvert.SerializeObject(subObjTrades);

            AlorSocketSubscription myTradesSub = new AlorSocketSubscription();
            myTradesSub.SubType = AlorSubType.MyTrades;
            myTradesSub.Guid = subObjTrades.guid;

            _subscriptionsPortfolio.Add(myTradesSub);
            _webSocketPortfolio.SendAsync(messageTradeSub);

            Thread.Sleep(1000);

            // orders subscription

            RequestSocketSubscribeOrders subObjOrders = new RequestSocketSubscribeOrders();
            subObjOrders.guid = GetGuid();
            subObjOrders.token = _apiTokenReal;
            subObjOrders.portfolio = portfolioName;

            if (portfolioName.StartsWith("E"))
            {
                subObjOrders.exchange = "UNITED";
            }

            string messageOrderSub = JsonConvert.SerializeObject(subObjOrders);

            AlorSocketSubscription ordersSub = new AlorSocketSubscription();
            ordersSub.SubType = AlorSubType.Orders;
            ordersSub.Guid = subObjOrders.guid;
            ordersSub.ServiceInfo = portfolioName;

            _subscriptionsPortfolio.Add(ordersSub);
            _webSocketPortfolio.SendAsync(messageOrderSub);

            Thread.Sleep(1000);

            // portfolio subscription

            RequestSocketSubscribePortfolio subObjPortf = new RequestSocketSubscribePortfolio();
            subObjPortf.guid = GetGuid();
            subObjPortf.token = _apiTokenReal;
            subObjPortf.portfolio = portfolioName;

            if (portfolioName.StartsWith("E"))
            {
                subObjPortf.exchange = "UNITED";
            }

            string messagePortfolioSub = JsonConvert.SerializeObject(subObjPortf);

            AlorSocketSubscription portfSub = new AlorSocketSubscription();
            portfSub.SubType = AlorSubType.Portfolio;
            portfSub.ServiceInfo = portfolioName;
            portfSub.Guid = subObjPortf.guid;

            _subscriptionsPortfolio.Add(portfSub);
            _webSocketPortfolio.SendAsync(messagePortfolioSub);

            Thread.Sleep(1000);

            // positions subscription

            RequestSocketSubscribePositions subObjPositions = new RequestSocketSubscribePositions();
            subObjPositions.guid = GetGuid();
            subObjPositions.token = _apiTokenReal;
            subObjPositions.portfolio = portfolioName;

            if (portfolioName.StartsWith("E"))
            {
                subObjPositions.exchange = "UNITED";
            }

            string messagePositionsSub = JsonConvert.SerializeObject(subObjPositions);

            AlorSocketSubscription positionsSub = new AlorSocketSubscription();
            positionsSub.SubType = AlorSubType.Positions;
            positionsSub.ServiceInfo = portfolioName;
            positionsSub.Guid = subObjPositions.guid;

            _subscriptionsPortfolio.Add(positionsSub);
            _webSocketPortfolio.SendAsync(messagePositionsSub);
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketData_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Data activated", LogMessageType.System);
            _socketDataIsActive = true;
            CheckActivationSockets();
        }

        private void WebSocketData_Closed(object sender, CloseEventArgs e)
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

        private void WebSocketData_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                if(ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if(message.Contains("The remote party closed the WebSocket connection"))
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

        private void WebSocketData_MessageReceived(object sender, MessageEventArgs e)
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

                if (e.Data.StartsWith("{\"requestGuid"))
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

                WebSocketDataMessage.Enqueue(e.Data);
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

        private void _webSocketPortfolio_Closed(object sender, CloseEventArgs e)
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

        private void _webSocketPortfolio_Error(object sender, ErrorEventArgs e)
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

        private void _webSocketPortfolio_MessageReceived(object sender, MessageEventArgs e)
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

                if (e.Data.StartsWith("{\"requestGuid"))
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

                WebSocketPortfolioMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage("Portfolio socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        #endregion

        #region 9 WebSocket Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(50));

        List<Security> _subscribedSecurities = new List<Security>();

        public void Subscribe(Security security)
        {
            try
            {
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }

                _rateGateSubscribe.WaitToProceed();

                _subscribedSecurities.Add(security);

                // trades subscription

                //curl - X GET "https://apidev.alor.ru/md/v2/Securities/MOEX/LKOH/alltrades?format=Simple&from=1593430060&to=1593430560&fromId=7796897024&toId=7796897280&take=10" - H "accept: application/json"

                RequestSocketSubscribeTrades subObjTrades = new RequestSocketSubscribeTrades();
                subObjTrades.code = security.Name;
                subObjTrades.guid = GetGuid();
                subObjTrades.token = _apiTokenReal;

                string messageTradeSub = JsonConvert.SerializeObject(subObjTrades);

                AlorSocketSubscription tradeSub = new AlorSocketSubscription();
                tradeSub.SubType = AlorSubType.Trades;
                tradeSub.ServiceInfo = security.Name;
                tradeSub.Guid = subObjTrades.guid;
                _subscriptionsData.Add(tradeSub);

                _webSocketData.SendAsync(messageTradeSub);

                // market depth subscription

                RequestSocketSubscribeMarketDepth subObjMarketDepth = new RequestSocketSubscribeMarketDepth();
                subObjMarketDepth.code = security.Name;
                subObjMarketDepth.guid = GetGuid();
                subObjMarketDepth.token = _apiTokenReal;

                if (((ServerParameterBool)ServerParameters[19]).Value == false)
                {
                    subObjMarketDepth.depth = "1";
                }
                else
                {
                    subObjMarketDepth.depth = ((ServerParameterEnum)ServerParameters[10]).Value;

                }

                AlorSocketSubscription mdSub = new AlorSocketSubscription();
                mdSub.SubType = AlorSubType.MarketDepth;
                mdSub.ServiceInfo = security.Name;
                mdSub.Guid = subObjMarketDepth.guid;
                _subscriptionsData.Add(mdSub);

                string messageMdSub = JsonConvert.SerializeObject(subObjMarketDepth);

                _webSocketData.SendAsync(messageMdSub);
            }
            catch (Exception exception)
            {
                SendLogMessage($"Subscribe error {security.Name} " + exception.ToString(),LogMessageType.Error);
            }
        }

        public void Unsubscribe(Security security)
        {
            try
            {
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Name == security.Name)
                    {
                        _subscribedSecurities.RemoveAt(i);
                        break;
                    }
                }

                _rateGateSubscribe.WaitToProceed();

                List<AlorSocketSubscription> subsToRemove = new List<AlorSocketSubscription>();

                for (int i = 0; i < _subscriptionsData.Count; i++)
                {
                    if (_subscriptionsData[i].ServiceInfo == security.Name)
                    {
                        subsToRemove.Add(_subscriptionsData[i]);
                    }
                }

                for (int i = 0; i < subsToRemove.Count; i++)
                {
                    AlorSocketSubscription sub = subsToRemove[i];
                    RequestSocketUnsubscribe unsubscribeRequest = new RequestSocketUnsubscribe();
                    unsubscribeRequest.guid = sub.Guid;
                    unsubscribeRequest.token = _apiTokenReal;

                    string message = JsonConvert.SerializeObject(unsubscribeRequest);

                    if (_webSocketData != null)
                    {
                        _webSocketData.SendAsync(message);
                    }

                    _subscriptionsData.Remove(sub);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Unsubscribe error. {security.Name}: " + exception.ToString(), LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion

        #region 10 WebSocket parsing the messages

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

                    SocketMessageBase baseMessage = 
                        JsonConvert.DeserializeAnonymousType(message, new SocketMessageBase());

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
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
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

            if(string.IsNullOrEmpty(baseMessage.oi) == false)
            {
                trade.OpenInterest = baseMessage.oi.ToDecimal();
            }

            if (baseMessage.side == "sell")
            {
                trade.Side = Side.Sell;
            }
            else
            {
                trade.Side = Side.Buy;
            }
            
            trade.Volume = baseMessage.qty.ToDecimal();

            if(trade.Price < 0)
            {

            }

            if (_ignoreMorningAuctionTrades && trade.Time.Hour < 9) // process only mornings
            {
                Security security = _subscribedSecurities[0];
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Name == trade.SecurityNameCode)
                    {
                        security = _subscribedSecurities[i];
                        break;
                    }
                }

                if (security.SecurityType == SecurityType.Futures)
                {
                    if (trade.Time < trade.Time.Date.AddHours(9))
                    {
                        return;
                    }
                }
                else
                {
                    if (trade.Time < trade.Time.Date.AddHours(7))
                    {
                        return;
                    }
                }
            }


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
                newBid.Price = baseMessage.data.bids[i].price.ToDouble();
                newBid.Bid = baseMessage.data.bids[i].volume.ToDouble();
                depth.Bids.Add(newBid);
            }

            for (int i = 0; i < baseMessage.data.asks.Count; i++)
            {
                MarketDepthLevel newAsk = new MarketDepthLevel();
                newAsk.Price = baseMessage.data.asks[i].price.ToDouble();
                newAsk.Ask = baseMessage.data.asks[i].volume.ToDouble();
                depth.Asks.Add(newAsk);
            }

            if(_lastMdTime != DateTime.MinValue &&
                _lastMdTime >= depth.Time)
            {
                depth.Time = _lastMdTime.AddTicks(1);
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

                    SocketMessageBase baseMessage =
                        JsonConvert.DeserializeAnonymousType(message, new SocketMessageBase());

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

                        if (_subscriptionsPortfolio[i].SubType == AlorSubType.Portfolio)
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
                            UpDateMyOrder(baseMessage.data.ToString(), _subscriptionsPortfolio[i].ServiceInfo);
                            break;
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private List<MyTrade> _spreadMyTrades = new List<MyTrade>();

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

            lock(_spreadOrdersLocker)
            {
                if (_spreadOrders.Count > 0)
                {
                    _spreadMyTrades.Add(trade);

                    for (int i = 0; i < _spreadOrders.Count; i++)
                    {
                        if (TryGenerateFakeMyTradeToOrderBySpread(_spreadOrders[i]))
                        {
                            _spreadOrders.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        private void UpDatePositionOnBoard(string data, string portfolioName)
        {
            PositionOnBoardAlor baseMessage =
                       JsonConvert.DeserializeAnonymousType(data, new PositionOnBoardAlor());

            Portfolio portf = null;

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                string realPortfName = _myPortfolios[i].Number.Split('_')[0];
                if (realPortfName == portfolioName)
                {
                    portf = _myPortfolios[i];
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
            newPos.UnrealizedPnl = baseMessage.dailyUnrealisedPl.ToDecimal();

            portf.SetNewPosition(newPos);

            if (PortfolioEvent != null)
            {
                PortfolioEvent(_myPortfolios);
            }
        }

        private void UpDateMyOrder(string data, string portfolioName)
        {
            OrderAlor baseMessage =
            JsonConvert.DeserializeAnonymousType(data, new OrderAlor());

            Order order = ConvertToOsEngineOrder(baseMessage, portfolioName);

            if(order == null)
            {
                return;
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

            lock(_spreadOrdersLocker)
            {
                if (order.State == OrderStateType.Done)
                {
                    // Проверяем, является ли бумага спредом

                    for (int i = 0; i < _spreadOrders.Count; i++)
                    {
                        if (_spreadOrders[i].NumberUser == order.NumberUser
                            && _spreadOrders[i].NumberMarket == "")
                        {
                            _spreadOrders[i].NumberMarket = order.NumberMarket;
                        }
                    }

                    for (int i = 0; i < _spreadOrders.Count; i++)
                    {
                        if (_spreadOrders[i].SecurityNameCode == order.SecurityNameCode)
                        {
                            if (TryGenerateFakeMyTradeToOrderBySpread(order))
                            {
                                _spreadOrders.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
                else if (order.State == OrderStateType.Cancel
                        || order.State == OrderStateType.Fail)
                {
                    for (int i = 0; i < _spreadOrders.Count; i++)
                    {
                        if (_spreadOrders[i].NumberUser == order.NumberUser)
                        {
                            _spreadOrders.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        private bool TryGenerateFakeMyTradeToOrderBySpread(Order order)
        {
            MyTrade tradeFirst = null;

            MyTrade tradeSecond = null;

            for(int i = 0;i < _spreadMyTrades.Count;i++)
            {
                if (_spreadMyTrades[i].NumberOrderParent == order.NumberMarket)
                {
                    if(tradeFirst == null)
                    {
                        tradeFirst = _spreadMyTrades[i];
                    }
                    else if(tradeSecond == null)
                    {
                        tradeSecond = _spreadMyTrades[i];
                        break;
                    }
                }
            }

            if(tradeFirst != null && 
                tradeSecond != null)
            {
                if(order.SecurityNameCode.StartsWith(tradeFirst.SecurityNameCode) == false)
                {
                    MyTrade third = tradeFirst;
                    tradeFirst = tradeSecond;
                    tradeSecond = third;
                }

                MyTrade trade = new MyTrade();
                trade.SecurityNameCode = order.SecurityNameCode;
                trade.Price = tradeSecond.Price - tradeFirst.Price;
                trade.Volume = order.Volume;
                trade.NumberOrderParent = order.NumberMarket;
                trade.NumberTrade = order.NumberMarket + "fakeSpreadTrade";
                trade.Time = order.TimeCallBack;
                trade.Side = order.Side;

                if (MyTradeEvent != null)
                {
                    MyTradeEvent(trade);
                }

                for (int i = 0; i < _spreadMyTrades.Count; i++)
                {
                    if (_spreadMyTrades[i].NumberOrderParent == order.NumberMarket)
                    {
                        _spreadMyTrades.RemoveAt(i);
                        i--;
                    }
                }

                return true;
            }

            return false;
        }

        private Order ConvertToOsEngineOrder(OrderAlor baseMessage, string portfolioName)
        {
            Order order = new Order();

            order.SecurityNameCode = baseMessage.symbol;

            if(string.IsNullOrEmpty(baseMessage.filled) == false 
                && baseMessage.filled != "0")
            {
                order.Volume = baseMessage.filled.ToDecimal();
            }
            else
            {
                order.Volume = baseMessage.qty.ToDecimal();
            }

            order.PortfolioNumber = portfolioName;
            
            if (baseMessage.type == "limit")
            {
                order.Price = baseMessage.price.ToDecimal();
                order.TypeOrder = OrderPriceType.Limit;
            }
            else if (baseMessage.type == "market")
            {
                order.TypeOrder = OrderPriceType.Market;
            }

            try
            {
                order.NumberUser = Convert.ToInt32(baseMessage.comment);
            }
            catch
            {
                // ignore
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
                order.State = OrderStateType.Active;
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
                            return null;
                        }
                    }
                }

                if (string.IsNullOrEmpty(baseMessage.filledQtyUnits))
                {
                    order.State = OrderStateType.Cancel;
                }
                else if (baseMessage.filledQtyUnits == "0")
                {
                    order.State = OrderStateType.Cancel;
                }
                else
                {
                    try
                    {
                        decimal volFilled = baseMessage.filledQtyUnits.ToDecimal();

                        if (volFilled > 0)
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

            return order;
        }

        private void UpDateMyPortfolio(string data, string portfolioName)
        {
            AlorPortfolioSocket baseMessage =
            JsonConvert.DeserializeAnonymousType(data, new AlorPortfolioSocket());

            Portfolio portf = null;

            for(int i = 0;i < _myPortfolios.Count;i++)
            {
                string realPortfName = _myPortfolios[i].Number.Split('_')[0];
                if (realPortfName == portfolioName)
                {
                    portf = _myPortfolios[i];
                    break;
                }
            }

            if(portf == null)
            {
                return;
            }

            if(portf.ValueBegin == 0)
            {
                portf.ValueBegin = baseMessage.portfolioLiquidationValue.ToDecimal();
            }

            portf.ValueCurrent = baseMessage.portfolioLiquidationValue.ToDecimal();
            
            portf.ValueBlocked = baseMessage.portfolioLiquidationValue.ToDecimal() - baseMessage.buyingPower.ToDecimal();
           
            portf.UnrealizedPnl = baseMessage.profit.ToDecimal();

            if (PortfolioEvent != null)
            {
                PortfolioEvent(_myPortfolios);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(10));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(10));

        private RateGate _rateGateChangePriceOrder = new RateGate(1, TimeSpan.FromMilliseconds(10));

        private List<AlorSecuritiesAndPortfolios> _securitiesAndPortfolios = new List<AlorSecuritiesAndPortfolios>();

        private List<Order> _sendOrders = new List<Order>();

        private string _sendOrdersArrayLocker = "alorSendOrdersArrayLocker";

        private List<Order> _spreadOrders = new List<Order>();
        private string _spreadOrdersLocker = "_spreadOrdersLocker";

        public void SendOrder(Order order)
        {
            //_rateGateSendOrder.WaitToProceed();

            try
            {
                if (order.SecurityClassCode == "Futures spread")
                { // календарный спред
                  // сохраняем бумагу для дальнейшего использования
                    lock (_spreadOrdersLocker)
                    {
                        _spreadOrders.Add(order);
                    }
                }

                if (order.TypeOrder == OrderPriceType.Market)
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
                requestRest.AddHeader("X-REQID", order.NumberUser.ToString() + "|" + GetGuid());
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

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    bool isInArray = false;
                    for(int i = 0;i < _securitiesAndPortfolios.Count;i++)
                    {
                        if (_securitiesAndPortfolios[i].Security == order.SecurityNameCode)
                        {
                            isInArray = true;
                            break;
                        }
                    }
                    if(isInArray == false)
                    {
                        AlorSecuritiesAndPortfolios newValue = new AlorSecuritiesAndPortfolios();
                        newValue.Security = order.SecurityNameCode;
                        newValue.Portfolio = order.PortfolioNumber;
                        _securitiesAndPortfolios.Add(newValue);
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

            if (order.LimitsMakerOnly == true)
            {
                requestObj.timeInForce = "bookorcancel";
            }
            else
            {
                requestObj.timeInForce = "goodtillcancelled";
            }

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
                _rateGateChangePriceOrder.WaitToProceed();

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    SendLogMessage("Can`t change price to market order", LogMessageType.Error);
                    return;
                }
                
                string endPoint = "/commandapi/warptrans/TRADE/v2/client/orders/actions/limit/";

                endPoint += order.NumberMarket;

                RestRequest requestRest = new RestRequest(endPoint, Method.PUT);
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                requestRest.AddHeader("X-REQID", order.NumberUser.ToString() + "|" + GetGuid()); ;
                requestRest.AddHeader("accept", "application/json");

                LimitOrderAlorRequest body = GetLimitRequestObj(order);
                body.price = newPrice;

                int qty = Convert.ToInt32(order.Volume - order.VolumeExecute);

                if(qty <= 0 ||
                    order.State != OrderStateType.Active)
                {
                    SendLogMessage("Can`t change price to order. It's not in Active state", LogMessageType.Error);
                    return;
                }

                requestRest.AddJsonBody(body);
                
                RestClient client = new RestClient(_restApiHost);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                AlorChangePriceOrder alorChangePriceOrder = new AlorChangePriceOrder();
                alorChangePriceOrder.MarketId = order.NumberMarket;
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

                    //return;
                }
                else
                {
                    SendLogMessage("Change price order Fail. Status: "
                        + response.StatusCode + "  " + order.SecurityNameCode, LogMessageType.Error);

                    if (response.Content != null)
                    {
                        SendLogMessage("Fail reasons: "
                      + response.Content, LogMessageType.Error);
                    }
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        List<string> _cancelOrderNums = new List<string>();
        private string _cancelOrderNumsLocker = "_cancelOrderNumsLocker";

        public bool CancelOrder(Order order)
        {
            //_rateGateCancelOrder.WaitToProceed();

            //curl -X DELETE "/commandapi/warptrans/TRADE/v2/client/orders/93713183?portfolio=D39004&exchange=MOEX&stop=false&format=Simple" -H "accept: application/json"

            try
            {
                if(order.NumberMarket == null)
                {
                    return false;
                }

                int countTryRevokeOrder = 0;

                lock (_cancelOrderNumsLocker)
                {
                    for (int i = 0; i < _cancelOrderNums.Count; i++)
                    {
                        if(_cancelOrderNums[i] == null)
                        {
                            continue;
                        }
                        if (_cancelOrderNums[i].Equals(order.NumberMarket))
                        {
                            countTryRevokeOrder++;
                        }
                    }
                }

                if(countTryRevokeOrder >= 5)
                {
                    SendLogMessage("Order cancel request error. The order has already been revoked " + order.SecurityClassCode, LogMessageType.Error);
                    return false;
                }

                lock (_cancelOrderNumsLocker)
                {
                    if (order.NumberMarket != null)
                    {
                        _cancelOrderNums.Add(order.NumberMarket);
                    }

                    while (_cancelOrderNums.Count > 1000)
                    {
                        _cancelOrderNums.RemoveAt(0);
                    }
                }

                string portfolio = order.PortfolioNumber.Split('_')[0];

                string exchange = "MOEX";
                string endPoint 
                    = $"/commandapi/warptrans/TRADE/v2/client/orders/{order.NumberMarket}?portfolio={portfolio}&exchange={exchange}&stop=false&jsonResponse=true&format=Simple";

                RestRequest requestRest = new RestRequest(endPoint, Method.DELETE);
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                requestRest.AddHeader("accept", "application/json");

                RestClient client = new RestClient(_restApiHost);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
                else
                {
                    OrderStateType state = GetOrderStatus(order);

                    if (state == OrderStateType.None)
                    {
                        SendLogMessage("Order cancel request error. Status: "
                            + response.StatusCode + "  " + order.SecurityClassCode, LogMessageType.Error);

                        if (response.Content != null)
                        {
                            SendLogMessage("Fail reasons: "
                          + response.Content, LogMessageType.Error);
                        }

                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order cancel request error " + exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange();

            for (int i = 0; i < orders.Count;i++)
            {
                Order order = orders[i];

                if(order.State == OrderStateType.Active)
                {
                    CancelOrder(order);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            List<Order> orders = GetAllOrdersFromExchange();

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Active
                    && order.SecurityNameCode == security.Name)
                {
                    CancelOrder(order);
                }
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange();

            for(int i = 0; orders != null && i < orders.Count; i++)
            {
                if(orders[i] == null)
                {
                    continue;
                }

                if (orders[i].State != OrderStateType.Active
                    && orders[i].State != OrderStateType.Partial
                    && orders[i].State != OrderStateType.Pending)
                {
                    continue;
                }

                orders[i].TimeCreate = orders[i].TimeCallBack;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            List<Order> orders = GetAllOrdersFromExchange();

            if(orders == null ||
                orders.Count == 0)
            {
                return OrderStateType.None;
            }

            Order orderOnMarket = null;

            for(int i = 0;i < orders.Count;i++)
            {
                Order curOder = orders[i];

                if (order.NumberUser != 0
                    && curOder.NumberUser != 0
                    && curOder.NumberUser == order.NumberUser)
                {
                    orderOnMarket = curOder;
                    break;
                }

                if(string.IsNullOrEmpty(order.NumberMarket) == false 
                    && order.NumberMarket == curOder.NumberMarket)
                {
                    orderOnMarket = curOder;
                    break;
                }
            }

            if(orderOnMarket == null)
            {
                return OrderStateType.None;
            }

            if (orderOnMarket != null && 
                MyOrderEvent != null)
            {
                MyOrderEvent(orderOnMarket);
            }

            if(orderOnMarket.State == OrderStateType.Done 
                || orderOnMarket.State == OrderStateType.Partial)
            {
                List<MyTrade> tradesBySecurity 
                    = GetMyTradesBySecurity(order.SecurityNameCode, order.PortfolioNumber.Split('_')[0]);

                if(tradesBySecurity == null)
                {
                    return orderOnMarket.State;
                }

                List<MyTrade> tradesByMyOrder = new List<MyTrade>();

                for(int i = 0;i < tradesBySecurity.Count;i++)
                {
                    if (tradesBySecurity[i].NumberOrderParent == orderOnMarket.NumberMarket)
                    {
                        tradesByMyOrder.Add(tradesBySecurity[i]);
                    }
                }

                for(int i = 0;i < tradesByMyOrder.Count;i++)
                {
                    if(MyTradeEvent != null)
                    {
                        MyTradeEvent(tradesByMyOrder[i]);
                    }
                }
            }

            return orderOnMarket.State;
        }

        private List<Order> GetAllOrdersFromExchange()
        {
            List<Order> orders = new List<Order>();

            if (string.IsNullOrEmpty(_portfolioSpotId) == false)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_portfolioSpotId);

                if (newOrders != null &&
                    newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            if (string.IsNullOrEmpty(_portfolioFutId) == false)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_portfolioFutId);

                if (newOrders != null &&
                    newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            if (string.IsNullOrEmpty(_portfolioCurrencyId) == false)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_portfolioCurrencyId);

                if (newOrders != null &&
                    newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            if (string.IsNullOrEmpty(_portfolioSpareId) == false)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_portfolioSpareId);

                if (newOrders != null &&
                    newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            return orders;
        }

        private List<Order> GetAllOrdersFromExchangeByPortfolio(string portfolio)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string exchange = "MOEX";
                if (portfolio.StartsWith("E"))
                {
                    exchange = "UNITED";
                }

                string endPoint = $"/md/v2/clients/{exchange}/" + portfolio + "/orders?format=Simple";

                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                requestRest.AddHeader("accept", "application/json");

                RestClient client = new RestClient(_restApiHost);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);


                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string respString = response.Content;

                    if(respString == "[]")
                    {
                        return null;
                    }
                    else
                    {

                        List<OrderAlor> orders = JsonConvert.DeserializeAnonymousType(respString, new List<OrderAlor>());

                        List<Order> osEngineOrders = new List<Order>();

                        for(int i = 0;i < orders.Count;i++)
                        {
                            Order newOrd = ConvertToOsEngineOrder(orders[i], portfolio);

                            if(newOrd == null)
                            {
                                continue;
                            }

                            osEngineOrders.Add(newOrd);
                        }

                        return osEngineOrders;
                        
                    }
                }
                else if(response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    SendLogMessage("Get all orders request error. ", LogMessageType.Error);

                    if (response.Content != null)
                    {
                        SendLogMessage("Fail reasons: "
                      + response.Content, LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<MyTrade> GetMyTradesBySecurity(string security, string portfolio)
        {
            try
            {
                // /md/v2/Clients/MOEX/D39004/LKOH/trades?format=Simple

                string exchange = "MOEX";
                if (portfolio.StartsWith("E"))
                {
                    exchange = "UNITED";
                }

                string endPoint = $"/md/v2/clients/{exchange}/{portfolio}/{security}/trades?format=Simple";

                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                requestRest.AddHeader("accept", "application/json");

                RestClient client = new RestClient(_restApiHost);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string respString = response.Content;

                    if (respString == "[]")
                    {
                        return null;
                    }
                    else
                    {
                        List<MyTradeAlorRest> allTradesJson 
                            = JsonConvert.DeserializeAnonymousType(respString, new List<MyTradeAlorRest>());

                        List<MyTrade> osEngineOrders = new List<MyTrade>();

                        for (int i = 0; i < allTradesJson.Count; i++)
                        {
                            MyTradeAlorRest tradeRest = allTradesJson[i];

                            MyTrade newTrade = new MyTrade();
                            newTrade.SecurityNameCode = security;
                            newTrade.NumberTrade = tradeRest.id;
                            newTrade.NumberOrderParent = tradeRest.orderno;
                            newTrade.Volume = tradeRest.qty.ToDecimal();
                            newTrade.Price = tradeRest.price.ToDecimal();
                            newTrade.Time =  ConvertToDateTimeFromTimeAlorData(tradeRest.date);

                            if (tradeRest.side == "buy")
                            {
                                newTrade.Side = Side.Buy;
                            }
                            else
                            {
                                newTrade.Side = Side.Sell;
                            }

                            osEngineOrders.Add(newTrade);
                        }

                        return osEngineOrders;

                    }
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    SendLogMessage("Get all orders request error. ", LogMessageType.Error);

                    if (response.Content != null)
                    {
                        SendLogMessage("Fail reasons: "
                      + response.Content, LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        #endregion

        #region 12 Helpers

        public long ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return Convert.ToInt64(diff.TotalSeconds);
        }

        private DateTime ConvertToDateTimeFromUnixFromSeconds(string seconds, bool applyOffsetToMsk = true)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTime result = origin.AddSeconds(seconds.ToDouble());

            if (applyOffsetToMsk)
                result = result.AddHours(3);

            return result;
        }

        private DateTime ConvertToDateTimeFromUnixFromMilliseconds(string seconds)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTime result = origin.AddMilliseconds(seconds.ToDouble()).AddHours(3); // force to Moscow time zone gmt+3

            return result;
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

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion
    }

    public enum AlorAvailableExchanges
    {
        MOEX,
        SPBX,
        UNITED
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

    public class AlorSecuritiesAndPortfolios
    {
       public string Security;

       public string Portfolio;
    }

    public enum AlorSubType
    {
        Trades,
        MarketDepth,
        Portfolio,
        Positions,
        Orders,
        MyTrades
    }
}