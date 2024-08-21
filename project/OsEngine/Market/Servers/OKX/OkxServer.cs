using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.OKX.Entity;
using OsEngine.Market.Servers.Entity;
using WebSocket4Net;
using System.Net.Http;
using SuperSocket.ClientEngine;
using OsEngine.Language;

namespace OsEngine.Market.Servers.OKX
{
    public class OkxServer : AServer
    {
        public OkxServer()
        {
            OkxServerRealization realization = new OkxServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamPassword, "");
        }
    }

    public class OkxServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public OkxServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread ThreadCleaningDoneOrders = new Thread(CleanDoneOrders);
            ThreadCleaningDoneOrders.CurrentCulture = new CultureInfo("ru-RU");
            ThreadCleaningDoneOrders.IsBackground = true;
            ThreadCleaningDoneOrders.Start();

            Thread keepAliveSokcets = new Thread(KeepAliveSockets);
            keepAliveSokcets.CurrentCulture = new CultureInfo("ru-RU");
            keepAliveSokcets.IsBackground = true;
            keepAliveSokcets.Start();

            Thread converterOrers = new Thread(ConverterOrders);
            converterOrers.CurrentCulture = new CultureInfo("ru-RU");
            converterOrers.IsBackground = true;
            converterOrers.Start();

            Thread converterPosiotions = new Thread(ConverterErrorPositions);
            converterPosiotions.CurrentCulture = new CultureInfo("ru-RU");
            converterPosiotions.IsBackground = true;
            converterPosiotions.Start();

            Thread converterTrades = new Thread(ConverterTrades);
            converterTrades.CurrentCulture = new CultureInfo("ru-RU");
            converterTrades.IsBackground = true;
            converterTrades.Start();

            Thread converterDepths = new Thread(ConverterDepths);
            converterDepths.CurrentCulture = new CultureInfo("ru-RU");
            converterDepths.IsBackground = true;
            converterDepths.Start();

            Thread portfolioData = new Thread(UpdatePortfolios);
            portfolioData.CurrentCulture = new CultureInfo("ru-RU");
            portfolioData.IsBackground = true;
            portfolioData.Start();
        }

        public ServerType ServerType
        {
            get { return ServerType.OKX; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            PublicKey = ((ServerParameterString)ServerParameters[0]).Value;
            SeckretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            Password = ((ServerParameterPassword)ServerParameters[2]).Value;

            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls |
                    SecurityProtocolType.Tls11 |
                    SecurityProtocolType.Tls12 |
                    SecurityProtocolType.Tls13 |
                    SecurityProtocolType.Ssl3 |
                    SecurityProtocolType.SystemDefault;


                var response = _httpPublicClient.GetAsync(_baseUrl + "api/v5/public/time").Result;

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage("Server is not available or there is no internet. \n" +
                         " \n You may have forgotten to turn on the VPN", LogMessageType.Error);
                    return;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Server is not available or there is no internet. \n" +
                    exception.Message +
                    " \n You may have forgotten to turn on the VPN", LogMessageType.Error);
                return;
            }

            try
            {
                SetPositionMode();
                CreateTradeChanel();
                CreateDepthsChanel();
                CreateOrderChanel();
                CreatePositionChanell();
            }
            catch(Exception exception) 
            {
                SendLogMessage("Server is not available or there is no internet. \n" +
                    exception.Message +
                      " \n You may have forgotten to turn on the VPN", LogMessageType.Error);
                return;
            }
        }

        public void Dispose()
        {
            if (ServerStatus == ServerConnectStatus.Connect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }

            IsConnectedPositions = false;
            IsConnectedOrders = false;
            IsConnectedTrades = false;
            IsConnectedDepths = false;
            _clientPrivateBalanceAndOrders = null;
            try
            {
                if(_wsClientPositions != null)
                {
                    _wsClientPositions.Closed -= new EventHandler(DisconnectPsoitonsChanel);
                    _wsClientPositions.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(PushMessagePositions);
                    DisposeSocket(_wsClientPositions);
                }
            }
            catch
            {
                // ignore
            }
            _wsClientPositions = null;

            try
            {
                if(_wsClientOrders != null)
                {
                    _wsClientOrders.Closed -= new EventHandler(DisconnectOrdersChanel);
                    _wsClientOrders.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(PushMessageOrders);
                    DisposeSocket(_wsClientOrders);
                }
            }
            catch
            {
                // ignore
            }
            _wsClientOrders = null;

            try
            {
                if(_wsClientDepths != null)
                {
                    _wsClientDepths.Closed -= new EventHandler(DisconnectDepthsChanel);
                    _wsClientDepths.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(PushMessageDepths);
                    DisposeSocket(_wsClientDepths);
                }
            }
            catch
            {
                // ignore
            }
            _wsClientDepths = null;

            try
            {
                if(_wsClientTrades != null)
                {
                    _wsClientTrades.Closed -= new EventHandler(DisconnectTradesChanel);
                    _wsClientTrades.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(PushMessageTrade);
                    DisposeSocket(_wsClientTrades);
                }
            }
            catch
            {
                // ignore
            }
            _wsClientTrades = null;

            _subscribledSecurities.Clear();
            _securities = new List<Security>();
        }

        private void DisposeSocket(WebSocket socket)
        {
            try
            {
                socket.Close();
            }
            catch
            {
                // ignore
            }
            try
            {
                socket.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string PublicKey;

        private string SeckretKey;

        private string Password;

        private string _baseUrl = "https://www.okx.com/";

        private string _publicWebSocket = "wss://ws.okx.com:8443/ws/v5/public";

        private string _privateWebSocket = "wss://ws.okx.com:8443/ws/v5/private";

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            try
            {
                SecurityResponce securityResponceFutures = GetFuturesSecurities();
                SecurityResponce securityResponceSpot = GetSpotSecurities();
                securityResponceFutures.data.AddRange(securityResponceSpot.data);
                UpdatePairs(securityResponceFutures);
            }
            catch (Exception error)
            {
                if (error.Message.Equals("Unexpected character encountered while parsing value: <. Path '', line 0, position 0."))
                {
                    SendLogMessage("service is unavailable", LogMessageType.Error);
                    return;
                }
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private SecurityResponce GetFuturesSecurities()
        {
            var response = _httpPublicClient.GetAsync("https://www.okx.com" + "/api/v5/public/instruments?instType=SWAP").Result;

            string json = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage(json, LogMessageType.Error);
            }

            SecurityResponce securityResponce = JsonConvert.DeserializeAnonymousType(json, new SecurityResponce());

            return securityResponce;
        }

        private SecurityResponce GetSpotSecurities()
        {
            var response = _httpPublicClient.GetAsync("https://www.okx.com" + "/api/v5/public/instruments?instType=SPOT").Result;
            var json = response.Content.ReadAsStringAsync().Result;
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage(json, LogMessageType.Error);
            }

            SecurityResponce securityResponce = JsonConvert.DeserializeAnonymousType(json, new SecurityResponce());

            return securityResponce;
        }

        private List<Security> _securities = new List<Security>();

        private void UpdatePairs(SecurityResponce securityResponce)
        {
            for (int i = 0; i < securityResponce.data.Count; i++)
            {
                SecurityResponceItem item = securityResponce.data[i];

                Security security = new Security();

                SecurityType securityType = SecurityType.CurrencyPair;

                if (item.instType.Equals("SWAP"))
                {
                    securityType = SecurityType.Futures;
                }

                security.Lot = item.minSz.ToDecimal();

                string volStep = item.minSz.Replace(',', '.');

                if (volStep != null
                        && volStep.Length > 0 &&
                        volStep.Split('.').Length > 1)
                {
                    security.DecimalsVolume = volStep.Split('.')[1].Length;
                }

                if (securityType == SecurityType.CurrencyPair)
                {
                    security.Name = item.instId;
                    security.NameFull = item.instId;
                    security.NameClass = "SPOT_" + item.quoteCcy;
                }
                if (securityType == SecurityType.Futures)
                {
                    security.Name = item.instId;
                    security.NameFull = item.instId;

                    if (item.instId.Contains("-USD-"))
                    {
                        security.NameClass = "SWAP_USD";
                    }
                    else
                    {
                        security.NameClass = "SWAP_" + item.settleCcy;
                    }
                }

                security.Exchange = ServerType.OKX.ToString();

                security.NameId = item.instId;
                security.SecurityType = securityType;

                security.PriceStep = item.tickSz.ToDecimal();
                security.PriceStepCost = security.PriceStep;


                if (security.PriceStep < 1)
                {
                    string prStep = security.PriceStep.ToString(CultureInfo.InvariantCulture);
                    security.Decimals = Convert.ToString(prStep).Split('.')[1].Split('1')[0].Length + 1;
                }
                else
                {
                    security.Decimals = 0;
                }

                security.State = SecurityStateType.Activ;
                _securities.Add(security);
            }

            if (SecurityEvent != null)
            {
                SecurityEvent(_securities);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        DateTime _timeUpdatePortfolio;

        private void UpdatePortfolios()
        {
            Thread.Sleep(30000);

            while (true)
            {
                Thread.Sleep(1000);

                if(ServerStatus == ServerConnectStatus.Disconnect)
                {
                    _timeUpdatePortfolio = DateTime.Now;
                    continue;
                }

                try
                {
                    if (_timeUpdatePortfolio.AddSeconds(30) > DateTime.Now)
                    {
                        continue;
                    }

                    _timeUpdatePortfolio = DateTime.Now;

                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    var json = GetBalance();

                    if (json.StartsWith("{\"code\":\"0\"") == false)
                    {
                        throw new Exception(json);
                    }

                    PorfolioResponse portfolio = JsonConvert.DeserializeAnonymousType(json, new PorfolioResponse());
                    portfolio.data[0].details.AddRange(GeneratePositionToContracts());
                    UpdatePortfolio(portfolio);
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        public RateGate _rateGateGetBalance = new RateGate(1, TimeSpan.FromMilliseconds(500));

        HttpClient _clientPrivateBalanceAndOrders = null;

        public void GetPortfolios()
        {
            try
            {

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var json = GetBalance();


                if (json.StartsWith("{\"code\":\"0\"") == false)
                {
                    if (json.Contains("API key doesn't exist"))
                    {
                        SendLogMessage("OKX error. Api key invalid", LogMessageType.Error);
                        
                        if(ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            
                            if(DisconnectEvent != null)
                            {
                                DisconnectEvent();
                            }
                        }
                    }

                    throw new Exception(json);
                }

                PorfolioResponse portfolio = JsonConvert.DeserializeAnonymousType(json, new PorfolioResponse());

                portfolio.data[0].details.AddRange(GeneratePositionToContracts());


                    UpdatePortfolio(portfolio);
                


            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private List<PortdolioDetails> GeneratePositionToContracts()
        {
            List<PorfolioData> potfolios = new List<PorfolioData>();
            List<PortdolioDetails> details = new List<PortdolioDetails>();


            try
            {
                string blockBalance = GetBlockBalance();

                PositonsResponce positons = JsonConvert.DeserializeAnonymousType(blockBalance, new PositonsResponce());

                for (int i = 0; i < positons.data.Count; i++)
                {
                    PorfolioData porfolioData = new PorfolioData()
                    {
                        details = new List<PortdolioDetails> { new PortdolioDetails()
                        {
                            ccy = positons.data[i].instId + "_" + positons.data[i].posSide.ToUpper(),
                            availEq = positons.data[i].pos, //notionalUsd
                            frozenBal = "0"
                    }   }
                    };

                    potfolios.Add(porfolioData);
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }

            for (int i = 0; i < potfolios.Count; i++)
            {
                details.AddRange(potfolios[i].details);
            }
            return details;
        }

        private string GetBlockBalance()
        {
            _rateGateGetBalance.WaitToProceed();
            var url = $"{_baseUrl}{"api/v5/account/positions"}";
            var res = GetPrivateRequest(url);
            var contentStr = res.Content.ReadAsStringAsync().Result;

            if (res.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage(contentStr, LogMessageType.Error);
            }

            return contentStr;
        }

        private string GetBalance()
        {
            _rateGateGetBalance.WaitToProceed();
            var url = $"{_baseUrl}{"api/v5/account/balance"}";

            var res = GetPrivateRequest(url);
            var contentStr = res.Content.ReadAsStringAsync().Result;

            if (res.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage(contentStr, LogMessageType.Error);
            }

            return contentStr;
        }

        private string _lockerBalanceGeter = "locker balance and orders geter";

        List<PositionOnBoard> CoinsWithNonZeroBalance = new List<PositionOnBoard>();

        private void SetCoinZeroBalance(Portfolio portfolio)
        {

            var array = portfolio.GetPositionOnBoard();

            if (array == null)
            {
                return;
            }

            for (int i = 0; i < array.Count; i++)
            {
                var coin = CoinsWithNonZeroBalance.Find(pos => pos.SecurityNameCode.Equals(array[i].SecurityNameCode));

                if (coin == null)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = array[i].SecurityNameCode;
                    newPortf.ValueCurrent = 0;
                    newPortf.ValueBlocked = 0;

                    portfolio.SetNewPosition(newPortf);
                }
            }

            CoinsWithNonZeroBalance.Clear();
        }

        List<Portfolio> _portfolios = new List<Portfolio>();

        private void UpdatePortfolio(PorfolioResponse portfs)
        {
            try
            {
                Portfolio myPortfolio = null;

                if (_portfolios.Count != 0)
                {
                    myPortfolio = _portfolios[0];
                }

                if (myPortfolio == null)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = "OKX";
                    newPortf.ValueBegin = 1;
                    newPortf.ValueCurrent = 1;
                    _portfolios.Add(newPortf);
                    myPortfolio = newPortf;
                }

                if (portfs.data == null)
                {
                    return;
                }

                for (int i = 0; i < portfs.data[0].details.Count; i++)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();

                    PortdolioDetails pos = portfs.data[0].details[i];

                    if (pos.ccy.Contains("SWAP"))
                    {
                        newPortf.SecurityNameCode = pos.ccy;

                        if (pos.ccy.Contains("LONG"))
                        {
                            newPortf.ValueBegin = pos.availEq.ToDecimal();
                            newPortf.ValueCurrent = pos.availEq.ToDecimal();
                            newPortf.ValueBlocked = pos.frozenBal.ToDecimal();
                        }
                        else if (pos.ccy.Contains("SHORT"))
                        {
                            newPortf.ValueBegin = -pos.availEq.ToDecimal();
                            newPortf.ValueCurrent = -pos.availEq.ToDecimal();
                            newPortf.ValueBlocked = pos.frozenBal.ToDecimal();
                        }
                    }
                    else
                    {
                        newPortf.SecurityNameCode = pos.ccy;
                        newPortf.ValueBegin = pos.availBal.ToDecimal();
                        newPortf.ValueCurrent = pos.availBal.ToDecimal();
                        newPortf.ValueBlocked = pos.frozenBal.ToDecimal();
                    }

                    CoinsWithNonZeroBalance.Add(newPortf);

                    myPortfolio.SetNewPosition(newPortf);
                }

                SetCoinZeroBalance(myPortfolio);

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public RateGate _rateGateCandles = new RateGate(1, TimeSpan.FromMilliseconds(500));

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
           return GetLastCandleHistoryRecursive(security, timeFrameBuilder, candleCount, 1);
        }

        public List<Candle> GetLastCandleHistoryRecursive(
            Security security, TimeFrameBuilder timeFrameBuilder, int candleCount, int recurseNumber)
        {
            try
            {
                _rateGateCandles.WaitToProceed();

                CandlesResponce securityResponce = GetResponseCandles(security.Name, timeFrameBuilder.TimeFrameTimeSpan);

                if (securityResponce == null)
                {
                    securityResponce = GetResponseCandles(security.Name, timeFrameBuilder.TimeFrameTimeSpan);
                }

                if (securityResponce == null)
                {
                    return null;
                }

                List<Candle> candles = new List<Candle>();

                ConvertCandles(securityResponce, candles);

                if (candles == null ||
                   candles.Count == 0)
                {
                    return null;
                }

                candles.Reverse();

                if (candles != null && candles.Count != 0)
                {
                    for (int i = 0; i < candles.Count; i++)
                    {
                        candles[i].State = CandleState.Finished;
                    }
                    candles[candles.Count - 1].State = CandleState.Started;
                }

                return candles;
            }
            catch
            {

            }

            if(recurseNumber < 5)
            {
                recurseNumber++;
                return GetLastCandleHistoryRecursive(security, timeFrameBuilder, candleCount, recurseNumber);
            }

            return null;
        }

        private CandlesResponce GetResponseCandles(string nameSec, TimeSpan tf)
        {

            int NumberCandlesToLoad = GetCountCandlesToLoad();

            string bar = GetStringBar(tf);

            CandlesResponce candlesResponce = new CandlesResponce();
            candlesResponce.data = new List<List<string>>();

            do
            {

                int limit = NumberCandlesToLoad;
                if (NumberCandlesToLoad > 100)
                {
                    limit = 100;
                }

                string after = String.Empty;

                if (candlesResponce.data.Count != 0)
                {
                    after = $"&after={candlesResponce.data[candlesResponce.data.Count - 1][0]}";
                }


                string url = _baseUrl + $"api/v5/market/history-candles?instId={nameSec}&bar={bar}&limit={limit}" + after;

                var responce = _httpPublicClient.GetAsync(url).Result;
                var json = responce.Content.ReadAsStringAsync().Result;
                candlesResponce.data.AddRange(JsonConvert.DeserializeAnonymousType(json, new CandlesResponce()).data);

                if (responce.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage(json, LogMessageType.Error);
                }

                NumberCandlesToLoad -= limit;


            } while (NumberCandlesToLoad > 0);

            return candlesResponce;
        }

        private int GetCountCandlesToLoad()
        {
            var server = (AServer)ServerMaster.GetServers().Find(server => server.ServerType == ServerType.OKX);

            for (int i = 0; i < server.ServerParameters.Count; i++)
            {
                if (server.ServerParameters[i].Name.Equals("Candles to load"))
                {
                    var Param = (ServerParameterInt)server.ServerParameters[i];
                    return Param.Value;
                }
            }

            return 100;
        }

        private void ConvertCandles(CandlesResponce candlesResponce, List<Candle> candles)
        {
            for (int j = 0; j < candlesResponce.data.Count; j++)
            {
                Candle candle = new Candle();
                try
                {
                    candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(candlesResponce.data[j][0]));

                    candle.Open = candlesResponce.data[j][1].ToDecimal();
                    candle.High = candlesResponce.data[j][2].ToDecimal();
                    candle.Low = candlesResponce.data[j][3].ToDecimal();
                    candle.Close = candlesResponce.data[j][4].ToDecimal();
                    candle.Volume = candlesResponce.data[j][5].ToDecimal();
                    var VolCcy = candlesResponce.data[j][6];

                    candles.Add(candle);
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if(timeFrameBuilder.TimeFrame == TimeFrame.Min1
                || timeFrameBuilder.TimeFrame == TimeFrame.Min2
                || timeFrameBuilder.TimeFrame == TimeFrame.Min10)
            {
                return null;
            }

            if(actualTime > endTime)
            {
                return null;
            }

            if(startTime > endTime)
            {
                return null;
            }

            if (endTime > DateTime.Now)
            {
                endTime = DateTime.Now;
            }

            var CountCandlesNeedToLoad = GetCountCandlesFromTimeInterval(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

            List<Candle> candles = GetCandleDataHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan, CountCandlesNeedToLoad, TimeManager.GetTimeStampMilliSecondsToDateTime(endTime));

            for (int i = 0; i < candles.Count; i++)
            {
                if (candles[i].TimeStart > endTime)
                {
                    candles.RemoveAt(i);
                    i--;
                }
            }

            for (int i = 1; i < candles.Count; i++)
            {
                if (candles[i - 1].TimeStart == candles[i].TimeStart)
                {
                    candles.RemoveAt(i);
                    i--;
                }
            }

            return candles;
        }

        private int GetCountCandlesFromTimeInterval(DateTime startTime, DateTime endTime, TimeSpan timeFrameSpan)
        {
            TimeSpan timeSpanInterval = endTime - startTime;

            if (timeFrameSpan.Hours != 0)
            {
                return Convert.ToInt32(timeSpanInterval.TotalHours / timeFrameSpan.Hours);
            }
            else if (timeFrameSpan.Days != 0)
            {
                return Convert.ToInt32(timeSpanInterval.TotalDays / timeFrameSpan.Days);
            }
            else
            {
                return Convert.ToInt32(timeSpanInterval.TotalMinutes / timeFrameSpan.Minutes);
            }
        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public List<Candle> GetCandleDataHistory(string nameSec, TimeSpan tf, int NumberCandlesToLoad, long DataEnd)
        {
            CandlesResponce securityResponce = GetResponseDataCandles(nameSec, tf, NumberCandlesToLoad, DataEnd);

            List<Candle> candles = new List<Candle>();

            ConvertCandles(securityResponce, candles);

            candles.Reverse();

            return candles;
        }

        private CandlesResponce GetResponseDataCandles(string nameSec, TimeSpan tf, int NumberCandlesToLoad, long DataEnd)
        {
            string bar = GetStringBar(tf);

            CandlesResponce candlesResponce = new CandlesResponce();
            candlesResponce.data = new List<List<string>>();

            do
            {
                int limit = NumberCandlesToLoad;
                if (NumberCandlesToLoad > 100)
                {
                    limit = 100;
                }

                string after = $"&after={Convert.ToString(DataEnd)}";

                if (candlesResponce.data.Count != 0)
                {
                    after = $"&after={candlesResponce.data[candlesResponce.data.Count - 1][0]}";
                }


                string url = _baseUrl + $"api/v5/market/history-candles?instId={nameSec}&bar={bar}&limit={limit}" + after;

                var responce = _httpPublicClient.GetAsync(url).Result;
                var json = responce.Content.ReadAsStringAsync().Result;
                candlesResponce.data.AddRange(JsonConvert.DeserializeAnonymousType(json, new CandlesResponce()).data);

                if (responce.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage(json, LogMessageType.Error);
                }

                NumberCandlesToLoad -= limit;


            } while (NumberCandlesToLoad > 0);

            return candlesResponce;
        }

        private string GetStringBar(TimeSpan tf)
        {
            try
            {
                if (tf.Hours != 0)
                {
                    return $"{tf.Hours}H";
                }
                if (tf.Minutes != 0)
                {
                    return $"{tf.Minutes}m";
                }
                if (tf.Days != 0)
                {
                    return $"{tf.Days}D";
                }

            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }

            return String.Empty;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private void CreateTradeChanel()
        {
            _wsClientTrades = new WebSocket(_publicWebSocket); // create web-socket / создаем вебсоке

            _wsClientTrades.Opened += new EventHandler((sender, e) => {
                ;
                ConnectTradesChanel(sender, e);
            });

            _wsClientTrades.Closed += new EventHandler(DisconnectTradesChanel);

            _wsClientTrades.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>((sender, e) => { WsError(sender, e, "", "Trades Chanell"); });

            _wsClientTrades.MessageReceived += new EventHandler<MessageReceivedEventArgs>(PushMessageTrade);

            _wsClientTrades.Open();
        }

        private void CreateDepthsChanel()
        {
            _wsClientDepths = new WebSocket(_publicWebSocket);

            _wsClientDepths.Opened += new EventHandler((sender, e) => {
                ConnectDepthsChanel(sender, e);
            });

            _wsClientDepths.Closed += new EventHandler(DisconnectDepthsChanel);

            _wsClientTrades.Error += 
                new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>((sender, e) => 
                { WsError(sender, e, "", "Depths Chanell"); });

            _wsClientDepths.MessageReceived += new EventHandler<MessageReceivedEventArgs>(PushMessageDepths);

            _wsClientDepths.Open();
        }

        private void CreateOrderChanel()
        {
            _wsClientOrders = new WebSocket(_privateWebSocket);

            _wsClientOrders.Opened += new EventHandler((sender, e) => {
                ConnectOrdersChanel(sender, e);
            });

            _wsClientOrders.Closed += new EventHandler(DisconnectOrdersChanel);

            _wsClientTrades.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>((sender, e) => { WsError(sender, e, "", "Orders Chanell"); });

            _wsClientOrders.MessageReceived += new EventHandler<MessageReceivedEventArgs>(PushMessageOrders);

            _wsClientOrders.Open();
        }

        public void CreatePositionChanell()
        {
            _wsClientPositions = new WebSocket(_privateWebSocket);

            _wsClientPositions.Opened += new EventHandler((sender, e) => {
                ConnectPositionsChanel(sender, e);
            });

            _wsClientPositions.Closed += new EventHandler(DisconnectPsoitonsChanel);
            _wsClientTrades.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>((sender, e) => { WsError(sender, e, "", "Position Chanell"); });
            _wsClientPositions.MessageReceived += new EventHandler<MessageReceivedEventArgs>(PushMessagePositions);
            _wsClientPositions.Open();
        }

        private WebSocket _wsClientPositions;

        private WebSocket _wsClientOrders;

        private WebSocket _wsClientDepths;

        private WebSocket _wsClientTrades;

        private object lockerPositionsWs = new object();

        private object lockerOrdersWs = new object();

        private object lockerTradesWs = new object();

        private object lockerDepthsWs = new object();

        private bool IsConnectedPositions;

        private bool IsConnectedOrders;

        private bool IsConnectedTrades;

        private bool IsConnectedDepths;

        private string _fullActivationCheckLocker = "fullActivationCheckLocker";

        private void CheckFullActivation()
        {
            lock(_fullActivationCheckLocker)
            {
                if (ServerStatus == ServerConnectStatus.Connect)
                {
                    return;
                }

                if (IsConnectedPositions == false)
                {
                    return;
                }

                if (IsConnectedOrders == false)
                {
                    return;
                }

                if (IsConnectedDepths == false)
                {
                    return;
                }

                if (IsConnectedTrades == false)
                {
                    return;
                }

                SendLogMessage("WebSockets activated", LogMessageType.System);

                ServerStatus = ServerConnectStatus.Connect;

                if (ConnectEvent != null)
                {
                    ConnectEvent();
                }
            }
        }

        public string SetLeverage(Security security)
        {
            Dictionary<string, string> requstObject = new Dictionary<string, string>();

            requstObject["instId"] = security.Name;
            requstObject["lever"] = "1";
            requstObject["mgnMode"] = "cross";

            var url = $"{_baseUrl}{"api/v5/account/set-leverage"}";
            var bodyStr = JsonConvert.SerializeObject(requstObject);
            using (var client = new HttpClient(new HttpInterceptor(PublicKey, SeckretKey, Password, bodyStr)))
            {
                var res = client.PostAsync(url, new StringContent(bodyStr, Encoding.UTF8, "application/json")).Result;

                var contentStr = res.Content.ReadAsStringAsync().Result;
                return contentStr;
            }
        }

        private void SetPositionMode()
        {
            var dict = new Dictionary<string, string>();

            dict["posMode"] = "long_short_mode";

            try
            {
                string res = PushPositionMode(dict);
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private string PushPositionMode(Dictionary<string, string> requestParams)
        {
            var url = $"{_baseUrl}{"api/v5/account/set-position-mode"}";
            var bodyStr = JsonConvert.SerializeObject(requestParams);
            using (var client = new HttpClient(new HttpInterceptor(PublicKey, SeckretKey, Password, bodyStr)))
            {
                var res = client.PostAsync(url, new StringContent(bodyStr, Encoding.UTF8, "application/json")).Result;

                var contentStr = res.Content.ReadAsStringAsync().Result;
                return contentStr;
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WsError(object sender, EventArgs e, string CoinPairs, string Chanell)
        {
            var q = (ErrorEventArgs)e;
            if (q.Exception != null)
            {
                SendLogMessage($"{Chanell} Error from ws4net {CoinPairs} :" + q.Exception, LogMessageType.Error);
            }
        }

        private void ConnectTradesChanel(object sender, EventArgs e)
        {
            SendLogMessage("Trades channel is open", LogMessageType.System);
            IsConnectedTrades = true;
            CheckFullActivation();
        }

        private void DisconnectTradesChanel(object sender, EventArgs e)
        {
            if(ServerStatus == ServerConnectStatus.Connect)
            {
                SendLogMessage("WebSocket disconnect. TradesChannel", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;

                if(DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }
        }

        private void PushMessageTrade(object sender, MessageReceivedEventArgs e)
        {
            if (e.Message.Equals("pong"))
            {
                return;
            }
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            _newMessageTrade.Enqueue(e.Message);
        }

        private void ConnectDepthsChanel(object sender, EventArgs e)
        {
            SendLogMessage("Market depths channel is open", LogMessageType.System);
            IsConnectedDepths = true;
            CheckFullActivation();
        }

        private void DisconnectDepthsChanel(object sender, EventArgs e)
        {
            if (ServerStatus == ServerConnectStatus.Connect)
            {
                SendLogMessage("WebSocket disconnect. DepthsChannel", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }
        }

        private void PushMessageDepths(object sender, MessageReceivedEventArgs e)
        {
            if (e.Message.Equals("pong"))
            {
                return;
            }

            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }
            _newMessageDepths.Enqueue(e.Message);
        }

        private void ConnectOrdersChanel(object sender, EventArgs e)
        {
            try
            {
                //Авторизация 
                var client = (WebSocket)sender;
                client.Send(Encryptor.MakeAuthRequest(PublicKey, SeckretKey, Password));

                string TypeInst = "SPOT";

                //Подписываемся на нужный канал
                RequestSubscribe<SubscribeArgsAccount> requestTradeSpot = new RequestSubscribe<SubscribeArgsAccount>();
                requestTradeSpot.args = new List<SubscribeArgsAccount>();
                requestTradeSpot.args.Add(new SubscribeArgsAccount()
                {
                    channel = "orders",
                    instType = TypeInst
                });

                var jsonSpot = JsonConvert.SerializeObject(requestTradeSpot);
                client.Send(jsonSpot);

                TypeInst = "SWAP";
                //Подписываемся на нужный канал
                RequestSubscribe<SubscribeArgsAccount> requestTradeSwap = new RequestSubscribe<SubscribeArgsAccount>();
                requestTradeSwap.args = new List<SubscribeArgsAccount>();
                requestTradeSwap.args.Add(new SubscribeArgsAccount()
                {
                    channel = "orders",
                    instType = TypeInst
                });
                var jsonSwap = JsonConvert.SerializeObject(requestTradeSwap);
                client.Send(jsonSwap);

                SendLogMessage("Orders channel is open", LogMessageType.System);
                IsConnectedOrders = true;
                CheckFullActivation();
            }
            catch (Exception error)
            {
                IsConnectedOrders = false;

                SendLogMessage("Orders channel connection CRITICAL ERROR " + "\n" + error.ToString(), LogMessageType.System);

                if (ServerStatus == ServerConnectStatus.Connect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;

                    if (DisconnectEvent != null)
                    {
                        DisconnectEvent();
                    }
                }
            }
        }

        private void DisconnectOrdersChanel(object sender, EventArgs e)
        {
            if (ServerStatus == ServerConnectStatus.Connect)
            {
                SendLogMessage("WebSocket disconnect. Orders Channel", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }
        }

        private void PushMessageOrders(object sender, MessageReceivedEventArgs e)
        {
            if (e.Message.Equals("pong"))
            {
                return;
            }

            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            _newMessageOrders.Enqueue(e.Message);
        }

        private void ConnectPositionsChanel(object sender, EventArgs e)
        {
            try
            {
                //Авторизация 
                var client = (WebSocket)sender;
                client.Send(Encryptor.MakeAuthRequest(PublicKey, SeckretKey, Password));

                string TypeInst = "SPOT";
                RequestSubscribe<SubscribeArgsAccount> requestTradeSpot = new RequestSubscribe<SubscribeArgsAccount>();
                requestTradeSpot.args = new List<SubscribeArgsAccount>();
                requestTradeSpot.args.Add(new SubscribeArgsAccount()
                {
                    channel = "positions",
                    instType = TypeInst
                });

                var jsonSpot = JsonConvert.SerializeObject(requestTradeSpot);
                client.Send(jsonSpot);


                TypeInst = "SWAP";
                RequestSubscribe<SubscribeArgsAccount> requestTradeSwap = new RequestSubscribe<SubscribeArgsAccount>();
                requestTradeSwap.args = new List<SubscribeArgsAccount>();
                requestTradeSwap.args.Add(new SubscribeArgsAccount()
                {
                    channel = "positions",
                    instType = TypeInst
                });
                var jsonSwap = JsonConvert.SerializeObject(requestTradeSwap);
                client.Send(jsonSwap);

                SendLogMessage("Positions channel is open", LogMessageType.System);
                IsConnectedPositions = true;
                CheckFullActivation();
            }
            catch (Exception error)
            {
                IsConnectedPositions = false;
                SendLogMessage("Positions channel connection CRITICAL ERROR " + "\n" + error.ToString(), LogMessageType.System);

                if (ServerStatus == ServerConnectStatus.Connect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;

                    if (DisconnectEvent != null)
                    {
                        DisconnectEvent();
                    }
                }
            }
        }

        private void DisconnectPsoitonsChanel(object sender, EventArgs e)
        {
            if (ServerStatus == ServerConnectStatus.Connect)
            {
                SendLogMessage("WebSocket disconnect. Positons Channel", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }
        }

        private void PushMessagePositions(object sender, MessageReceivedEventArgs e)
        {
            if (e.Message.Equals("pong"))
            {
                return;
            }

            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            _newMessagePositions.Enqueue(e.Message);
        }

        #endregion

        #region 8 WebSocket check alive

        private void KeepAliveSockets()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(10000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    lock (lockerPositionsWs)
                    {
                        if (_wsClientPositions.State == WebSocketState.Open)
                        {
                            _wsClientPositions.Send("ping");
                        }
                    }
                    lock (lockerOrdersWs)
                    {
                        if (_wsClientOrders.State == WebSocketState.Open)
                        {
                            _wsClientOrders.Send("ping");
                        }
                    }
                    lock (lockerTradesWs)
                    {
                        if (_wsClientTrades.State == WebSocketState.Open)
                        {
                            _wsClientTrades.Send("ping");
                        }
                    }
                    lock (lockerDepthsWs)
                    {
                        if (_wsClientDepths.State == WebSocketState.Open)
                        {
                            _wsClientDepths.Send("ping");
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        #endregion

        #region 9 Security subscrible

        public RateGate _rateGateWebSocket = new RateGate(1, TimeSpan.FromMilliseconds(500));

        private List<Security> _subscribledSecurities = new List<Security>();

        public void Subscrible(Security security)
        {
            for (int i = 0; i < _subscribledSecurities.Count; i++)
            {
                if (_subscribledSecurities[i].Name == security.Name
                    && _subscribledSecurities[i].NameClass == security.NameClass)
                {
                    return;
                }
            }

            _subscribledSecurities.Add(security);

            _rateGateWebSocket.WaitToProceed();

            SubscribleTrades(security);
            SubscribleDepths(security);
        }

        public void SubscribleTrades(Security security)
        {
            RequestSubscribe<SubscribeArgs> requestTrade = new RequestSubscribe<SubscribeArgs>();
            requestTrade.args = new List<SubscribeArgs>();
            requestTrade.args.Add(new SubscribeArgs()
            {
                channel = "trades",
                instId = security.Name
            });

            var json = JsonConvert.SerializeObject(requestTrade);

            _wsClientTrades.Send(json);
        }

        public void SubscribleDepths(Security security)
        {
            RequestSubscribe<SubscribeArgs> requestTrade = new RequestSubscribe<SubscribeArgs>();
            requestTrade.args = new List<SubscribeArgs>();
            requestTrade.args.Add(new SubscribeArgs()
            {
                //channel = "books",
                channel = "books5",
                instId = security.Name
            });

            var json = JsonConvert.SerializeObject(requestTrade);

            _wsClientDepths.Send(json);
        }

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> _newMessageOrders = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _newMessagePositions = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _newMessageTrade = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _newMessageDepths = new ConcurrentQueue<string>();

        private void ConverterOrders()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_newMessageOrders.IsEmpty == true)
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        string mes;

                        if (_newMessageOrders.TryDequeue(out mes))
                        {

                            if (mes.StartsWith("{\"event\": \"error\""))
                            {
                                SendLogMessage(mes, LogMessageType.Error);
                            }

                            else if (mes.StartsWith("{\"arg\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new ObjectChanel<OrderResponseData>());
                                UpdateOrders(quotes);
                                continue;
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void ConverterErrorPositions()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_newMessagePositions.IsEmpty == true)
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        string mes;

                        if (_newMessagePositions.TryDequeue(out mes))
                        {
                            Order order = null;

                            var quotes = JsonConvert.DeserializeAnonymousType(mes, new ErrorMessage<ErrorObjectOrders>());

                            if (quotes.data == null || quotes.data.Count == 0)
                            {
                                continue;

                            }
                            if (quotes.data[0].sCode == null)
                            {
                                continue;
                            }
                            if (quotes.data[0].sCode.Equals("0"))
                            {
                                continue;
                            }

                            SendLogMessage(quotes.data[0].clOrdId + quotes.data[0].sMsg, LogMessageType.Error);

                            order = FindNeedOrder(quotes.data[0].clOrdId);

                            if (MyOrderEvent != null && order != null)
                            {
                                order.State = OrderStateType.Fail;

                                if (MyOrderEvent != null)
                                {
                                    MyOrderEvent(order);
                                }
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void ConverterTrades()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_newMessageTrade.IsEmpty == true)
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        string mes;

                        if (_newMessageTrade.TryDequeue(out mes))
                        {

                            if (mes.StartsWith("{\"event\": \"error\""))
                            {
                                SendLogMessage(mes, LogMessageType.Error);
                            }

                            else if (mes.StartsWith("{\"arg\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new TradeResponse());
                                UpdateTrades(quotes);
                                
                                continue;
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void ConverterDepths()
        {
            while (true)
            {
                try
                {
                    if(ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if(_newMessageDepths.IsEmpty == true)
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        string mes;

                        if (_newMessageDepths.TryDequeue(out mes))
                        {
                            if (mes.StartsWith("{\"event\": \"error\""))
                            {
                                SendLogMessage(mes, LogMessageType.Error);
                            }

                            else if (mes.StartsWith("{\"arg\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new DepthResponse());
                                UpdateMarketDepth(quotes);
                                
                                continue;
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private List<MarketDepth> _depths;

        private void UpdateMarketDepth(DepthResponse depthResponse)
        {
            try
            {

                if (_depths == null)
                {
                    _depths = new List<MarketDepth>();
                }

                if (depthResponse.data[0].asks == null || depthResponse.data[0].asks.Count == 0 ||
                    depthResponse.data[0].bids == null || depthResponse.data[0].bids.Count == 0)
                {
                    return;
                }

                string secName = depthResponse.arg.instId;

                var needDepth = _depths.Find(depth => depth.SecurityNameCode == secName);

                if (needDepth == null)
                {
                    needDepth = new MarketDepth();
                    needDepth.SecurityNameCode = secName;
                    _depths.Add(needDepth);
                }


                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                for (int i = 0; i < depthResponse.data[0].asks.Count; i++)
                {
                    MarketDepthLevel level = new MarketDepthLevel();

                    level.Ask = depthResponse.data[0].asks[i][1].ToString().ToDecimal();

                    level.Price = depthResponse.data[0].asks[i][0].ToString().ToDecimal();
                    ascs.Add(level);
                }

                for (int i = 0; i < depthResponse.data[0].bids.Count; i++)
                {
                    MarketDepthLevel level = new MarketDepthLevel();

                    level.Bid = depthResponse.data[0].bids[i][1].ToString().ToDecimal();

                    level.Price = depthResponse.data[0].bids[i][0].ToString().ToDecimal();

                    bids.Add(level);
                }

                needDepth.Asks = ascs;
                needDepth.Bids = bids;

                needDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(depthResponse.data[0].ts));

                if (needDepth.Time == DateTime.MinValue)
                {
                    return;
                }

                //needDepth = RefreshDepthSupport(needDepth, depthResponse.arg.instId);

                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(needDepth.GetCopy());
                }

            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateTrades(TradeResponse tradeRespone)
        {
            if (tradeRespone.data == null)
            {
                return;
            }

            Trade trade = new Trade();
            trade.SecurityNameCode = tradeRespone.data[0].instId;

            if (trade.SecurityNameCode != tradeRespone.data[0].instId)
            {
                return;
            }

            trade.Price = tradeRespone.data[0].px.ToDecimal();
            trade.Id = tradeRespone.data[0].tradeId;
            trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(tradeRespone.data[0].ts));
            trade.Volume = tradeRespone.data[0].sz.ToDecimal();

            if (tradeRespone.data[0].side.Equals("buy"))
            {
                trade.Side = Side.Buy;
            }
            if (tradeRespone.data[0].side.Equals("sell"))
            {
                trade.Side = Side.Sell;
            }

            NewTradesEvent?.Invoke(trade);
        }

        private void UpdateOrders(ObjectChanel<OrderResponseData> OrderResponse)
        {
            lock (lokerOrder)
            {
                if (OrderResponse.data == null || OrderResponse.data.Count == 0)
                {
                    return;
                }

                for(int i = 0;i < OrderResponse.data.Count;i++)
                {
                    Order newOrder = null;

                    if ((OrderResponse.data[i].ordType.Equals("limit") ||
                    OrderResponse.data[i].ordType.Equals("market"))
                    &&
                    OrderResponse.data[i].state.Equals("filled"))
                    {
                        newOrder = OrderUpdate(OrderResponse.data[i], OrderStateType.Done);
                    }

                    else if ((OrderResponse.data[i].ordType.Equals("limit") ||
                       OrderResponse.data[i].ordType.Equals("market"))
                        &&
                        OrderResponse.data[i].state.Equals("live"))
                    {
                        newOrder = OrderUpdate(OrderResponse.data[i], OrderStateType.Activ);
                    }

                    else if ((OrderResponse.data[i].ordType.Equals("limit") ||
                        OrderResponse.data[i].ordType.Equals("market"))
                        &&
                        OrderResponse.data[i].state.Equals("canceled"))
                    {
                        newOrder = OrderUpdate(OrderResponse.data[i], OrderStateType.Cancel);
                    }

                    if(newOrder == null)
                    {
                        continue;
                    }

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(newOrder);
                    }

                    if (newOrder.State == OrderStateType.Patrial ||
                        newOrder.State == OrderStateType.Done)
                    {
                        Thread.Sleep(500);
                        List<MyTrade> tradesInOrder = GenerateTradesToOrder(newOrder, 1);

                        for (int i2 = 0; tradesInOrder != null && i2 < tradesInOrder.Count; i2++)
                        {
                            MyTradeEvent(tradesInOrder[i2]);
                        }
                    }
                }
            }

            GetPortfolios();
        }

        private Order OrderUpdate(OrderResponseData OrderResponse, OrderStateType stateType)
        {
            var item = OrderResponse;

            Order newOrder = new Order();
            newOrder.SecurityNameCode = item.instId;
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));

            if (stateType == OrderStateType.Done)
            {
                newOrder.TimeDone = newOrder.TimeCallBack;
            }
            else if (stateType == OrderStateType.Cancel)
            {
                newOrder.TimeCancel = newOrder.TimeCallBack;
            }

            if (!item.clOrdId.Equals(String.Empty))
            {
                try
                {
                    newOrder.NumberUser = Convert.ToInt32(item.clOrdId);
                }
                catch
                {
                    // ignore
                }
            }

            newOrder.NumberMarket = item.ordId.ToString();

            if (item.posSide == "net"
                || item.posSide == "")
            {
                newOrder.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
            }
            else
            {
                newOrder.Side = item.posSide.Equals("long") ? Side.Buy : Side.Sell;
            }

            newOrder.State = stateType;
            newOrder.Volume = item.sz.ToDecimal();
            newOrder.PortfolioNumber = "OKX";

            if (string.IsNullOrEmpty(item.avgPx) == false
                && item.avgPx != "0")
            {
                newOrder.Price = item.avgPx.ToDecimal();
            }
            else if (string.IsNullOrEmpty(item.px) == false
                && item.px != "0")
            {
                newOrder.Price = item.px.ToDecimal();
            }

            if (item.ordType == "market")
            {
                newOrder.TypeOrder = OrderPriceType.Market;
            }
            else
            {
                newOrder.TypeOrder = OrderPriceType.Limit;
            }

            newOrder.ServerType = ServerType.OKX;

            return newOrder;
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        #endregion

        #region 11 Trade

        private object _lockOrder = new object();

        public void SendOrder(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    if (_wsClientPositions != null)
                    {
                        if (order.SecurityNameCode.Contains("SWAP"))
                        {
                            SendOrderSwap(order);
                        }
                        else
                        {
                            SendOrderSpot(order);
                        }
                        GetPortfolios();
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} {error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        private void SendOrderSpot(Order order)
        {

            OrderRequest<OrderRequestArgsSwap> orderRequest = new OrderRequest<OrderRequestArgsSwap>();
            orderRequest.id = order.NumberUser.ToString();

            OrderRequestArgsSwap requesOrder = new OrderRequestArgsSwap();

            requesOrder.side = order.Side.ToString().ToLower();
            requesOrder.instId = order.SecurityNameCode;
            requesOrder.tdMode = "cash";
            requesOrder.ordType = "limit";
            requesOrder.sz = order.Volume.ToString().Replace(",", ".");
            requesOrder.px = order.Price.ToString().Replace(",", ".");
            requesOrder.clOrdId = order.NumberUser.ToString();
            requesOrder.tag = "5faf8b0e85c1BCDE";

            orderRequest.args.Add(requesOrder);

            string json = JsonConvert.SerializeObject(orderRequest);

            MyOrderRequest.Add(order);
            _wsClientPositions.Send(json);
        }

        private void SendOrderSwap(Order order)
        {
            var side = String.Empty;
            if (order.PositionConditionType == OrderPositionConditionType.Close)
            {
                side = order.Side == Side.Buy ? "short" : "long";
            }
            else
            {
                side = order.Side == Side.Buy ? "long" : "short";
                //side = order.Side.ToString().ToLower();
            }

            OrderRequest<OrderRequestArgsSwap> orderRequest = new OrderRequest<OrderRequestArgsSwap>();
            orderRequest.id = order.NumberUser.ToString();
            orderRequest.args.Add(new OrderRequestArgsSwap()
            {
                side = order.Side.ToString().ToLower(),
                posSide = side,
                instId = order.SecurityNameCode,
                tdMode = "cross",
                ordType = order.TypeOrder.ToString().ToLower(),
                sz = Convert.ToInt32(order.Volume).ToString(),
                px = order.Price.ToString().Replace(",", "."),
                clOrdId = order.NumberUser.ToString(),
                reduceOnly = order.PositionConditionType == OrderPositionConditionType.Close ? true : false,
                tag = "5faf8b0e85c1BCDE"
            });

            string json = JsonConvert.SerializeObject(orderRequest);

            MyOrderRequest.Add(order);
            _wsClientPositions.Send(json);
        }

        public void CancelOrder(Order order)
        {
            List<InstIdOrdId> arg = new List<InstIdOrdId>();
            arg.Add(new InstIdOrdId()
            {
                instId = order.SecurityNameCode,
                ordId = order.NumberMarket
            });

            var q = new
            {
                id = order.NumberUser.ToString(),
                op = "cancel-order",
                args = arg,
            };

            string json = JsonConvert.SerializeObject(q);

            _wsClientOrders.Send(json);
        }

        public void CancelAllOrdersToSecurity(Security security)
        {

        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetActivOrders();

            if (orders == null)
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                CancelOrder(orders[i]);
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetActivOrders();

            if(orders == null)
            {
                return;
            }

            for(int i = 0;i < orders.Count;i++)
            {
                if(MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        public void GetOrderStatus(Order order)
        {
            // GET / Order details
            // GET /api/v5/trade/order?ordId=680800019749904384&instId=BTC-USDT

            string url = null;

            if(string.IsNullOrEmpty(order.NumberMarket))
            {
                url =
                    $"{"https://www.okx.com/"}{"api/v5/trade/order"}"
                    + $"?clOrdId={order.NumberUser}&"
                    + $"instId={order.SecurityNameCode}";
            }
            else
            {
                url =
                    $"{"https://www.okx.com/"}{"api/v5/trade/order"}"
                    + $"?ordId={order.NumberMarket}&"
                    + $"clOrdId={order.NumberUser}&"
                    + $"instId={order.SecurityNameCode}";
            }

            var res = GetPrivateRequest(url);

            var contentStr = res.Content.ReadAsStringAsync().Result;

            if (res.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage(contentStr, LogMessageType.Error);
                return;
            }

            OrdersResponce OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new OrdersResponce());

            List<Order> orders = new List<Order>();

            for (int i = 0; i < OrderResponse.data.Count; i++)
            {
                Order newOrder = null;

                if ((OrderResponse.data[i].ordType.Equals("limit") ||
                OrderResponse.data[i].ordType.Equals("market"))
                &&
                OrderResponse.data[i].state.Equals("filled"))
                {
                    newOrder = OrderUpdate(OrderResponse.data[i], OrderStateType.Done);
                }

                else if ((OrderResponse.data[i].ordType.Equals("limit") ||
                   OrderResponse.data[i].ordType.Equals("market"))
                    &&
                    OrderResponse.data[i].state.Equals("live"))
                {
                    newOrder = OrderUpdate(OrderResponse.data[i], OrderStateType.Activ);
                }

                else if ((OrderResponse.data[i].ordType.Equals("limit") ||
                    OrderResponse.data[i].ordType.Equals("market"))
                    &&
                    OrderResponse.data[i].state.Equals("canceled"))
                {
                    newOrder = OrderUpdate(OrderResponse.data[i], OrderStateType.Cancel);
                }

                if (newOrder == null)
                {
                    continue;
                }

                orders.Add(newOrder);
            }

            if(orders == null 
                || orders.Count == 0)
            {
                return;
            }

            Order myOrder = orders[0];

            if(MyOrderEvent != null)
            {
                MyOrderEvent(myOrder);
            }

            if(myOrder.State == OrderStateType.Done 
                || myOrder.State == OrderStateType.Patrial)
            {
                List<MyTrade> myTrades = GenerateTradesToOrder(myOrder, 1);

                for(int i = 0; myTrades != null &&  i < myTrades.Count; i++)
                {
                    MyTradeEvent(myTrades[i]);
                }
            }
        }

        private List<Order> GetActivOrders()
        {
            // GET / Order List
            // GET /api/v5/trade/orders-pending?ordType=post_only,fok,ioc&instType=SPOT

            var url = $"{"https://www.okx.com/"}{"api/v5/trade/orders-pending"}";

            var res = GetPrivateRequest(url);

            var contentStr = res.Content.ReadAsStringAsync().Result;

            if (res.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage(contentStr, LogMessageType.Error);
                return null;
            }

            OrdersResponce OrderResponse = JsonConvert.DeserializeAnonymousType(contentStr, new OrdersResponce());

            List<Order> orders = new List<Order>();

            for (int i = 0; i < OrderResponse.data.Count; i++)
            {
                Order newOrder = null;

                if ((OrderResponse.data[i].ordType.Equals("limit") ||
                OrderResponse.data[i].ordType.Equals("market"))
                &&
                OrderResponse.data[i].state.Equals("filled"))
                {
                    newOrder = OrderUpdate(OrderResponse.data[i], OrderStateType.Done);
                }

                else if ((OrderResponse.data[i].ordType.Equals("limit") ||
                   OrderResponse.data[i].ordType.Equals("market"))
                    &&
                    OrderResponse.data[i].state.Equals("live"))
                {
                    newOrder = OrderUpdate(OrderResponse.data[i], OrderStateType.Activ);
                }

                else if ((OrderResponse.data[i].ordType.Equals("limit") ||
                    OrderResponse.data[i].ordType.Equals("market"))
                    &&
                    OrderResponse.data[i].state.Equals("canceled"))
                {
                    newOrder = OrderUpdate(OrderResponse.data[i], OrderStateType.Cancel);
                }

                if (newOrder == null)
                {
                    continue;
                }

                orders.Add(newOrder);
            }

            return orders;
        }

        private RateGate _rateGateGenerateToTrate = new RateGate(1, TimeSpan.FromMilliseconds(300));

        private List<MyTrade> GenerateTradesToOrder(Order order, int SeriasCalls)
        {
            _rateGateGenerateToTrate.WaitToProceed();

            List<MyTrade> myTrades = new List<MyTrade>();

            if (SeriasCalls >= 8)
            {
                SendLogMessage($"Trade is not found to order: {order.NumberUser}", LogMessageType.Error);
                return myTrades;
            }

            string TypeInstr = order.SecurityNameCode.EndsWith("SWAP") ? "SWAP" : "SPOT";

            var url = $"{"https://www.okx.com/"}{"api/v5/trade/fills-history"}" + $"?ordId={order.NumberMarket}&" + $"instId={order.SecurityNameCode}&" + $"instType={TypeInstr}";

            var res = GetPrivateRequest(url);

            var contentStr = res.Content.ReadAsStringAsync().Result;

            if (res.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage(contentStr, LogMessageType.Error);
            }

            var quotes = JsonConvert.DeserializeAnonymousType(contentStr, new TradeDetailsResponce());

            if (quotes == null ||
                quotes.data == null ||
                quotes.data.Count == 0)
            {
                Thread.Sleep(500 * SeriasCalls);

                SeriasCalls++;

                return GenerateTradesToOrder(order, SeriasCalls);
            }

            CreateListTrades(myTrades, quotes);

            return myTrades;

        }

        private void CreateListTrades(List<MyTrade> myTrades, TradeDetailsResponce quotes)
        {
            for (int i = 0; i < quotes.data.Count; i++)
            {
                var item = quotes.data[i];

                MyTrade myTrade = new MyTrade();

                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));
                myTrade.NumberOrderParent = item.ordId.ToString();
                myTrade.NumberTrade = item.tradeId.ToString();

                if (string.IsNullOrEmpty(item.fee))
                {
                    myTrade.Volume = item.fillSz.ToDecimal();
                }
                else
                {// комиссия есть

                    if (item.instId.StartsWith(item.feeCcy))
                    { // комиссия взята в торгуемой валюте, а не в валюте биржи
                        myTrade.Volume = item.fillSz.ToDecimal() + item.fee.ToDecimal();
                    }
                    else
                    {
                        myTrade.Volume = item.fillSz.ToDecimal();
                    }
                }

                if (!item.fillPx.Equals(String.Empty))
                {
                    myTrade.Price = item.fillPx.ToDecimal();
                }
                myTrade.SecurityNameCode = item.instId;

                if (item.posSide == "net")
                {
                    myTrade.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
                }
                else
                {
                    myTrade.Side = item.posSide.Equals("long") ? Side.Buy : Side.Sell;
                }


                myTrades.Add(myTrade);

            }
        }

        private object lokerOrder = new object();

        public void GetOrdersState(List<Order> orders)
        {

        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        private List<Order> MyOrderRequest = new List<Order>();

        private object lockerCleaningDoneOrders = new object();

        private Order FindNeedOrder(string ClOrdId)
        {
            lock (lockerCleaningDoneOrders)
            {
                for (int i = 0; i < MyOrderRequest.Count; i++)
                {
                    if (MyOrderRequest[i].NumberUser == Convert.ToInt32(ClOrdId))
                    {
                        return MyOrderRequest[i];
                    }
                }
                return null;
            }
        }

        private void CleanDoneOrders()
        {
            while (true)
            {
                Thread.Sleep(30000);

                lock (lockerCleaningDoneOrders)
                {
                    for (int i = 0; i < MyOrderRequest.Count; i++)
                    {
                        if (MyOrderRequest[i].State == OrderStateType.Done)
                        {
                            MyOrderRequest.Remove(MyOrderRequest[i]);
                        }
                    }
                }
            }
        }

        #endregion

        #region 12 Queries

        HttpClient _httpPublicClient = new HttpClient();

        public HttpResponseMessage GetPrivateRequest(string url)
        {
            lock (_lockerBalanceGeter)
            {
                if (_clientPrivateBalanceAndOrders == null)
                {
                    _clientPrivateBalanceAndOrders = new HttpClient(new HttpInterceptor(PublicKey, SeckretKey, Password, null));
                }

                return _clientPrivateBalanceAndOrders.GetAsync(url).Result;
            }
        }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}