using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.MoexAlgopack.Entity;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Collections.Concurrent;

namespace OsEngine.Market.Servers.MoexAlgopack
{
    public class MoexAlgopackServer : AServer
    {
        public MoexAlgopackServer()
        {
            MoexAlgopackServerRealization realization = new MoexAlgopackServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamId, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamPassword, "");
            //CreateParameterBoolean(OsLocalization.Market.ServerParamSubscription, false);
        }

        public class MoexAlgopackServerRealization : IServerRealization
        {
            #region 1 Constructor, Status, Connection

            public MoexAlgopackServerRealization()
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                
                Thread threadGetter = new Thread(GetNewData);
                threadGetter.IsBackground = true;
                threadGetter.Name = "GetAlgopackData";
                threadGetter.Start();

                Thread threadUpdater = new Thread(UpdateAllData);
                threadUpdater.IsBackground = true;
                threadUpdater.Name = "UpdateAlgopackData";
                threadUpdater.Start();

                GetPortfolios();
            }

            private bool _isAuthorized = false;

            public void Connect(WebProxy proxy)
            {
                _username = ((ServerParameterString)ServerParameters[0]).Value;
                _password = ((ServerParameterPassword)ServerParameters[1]).Value;
                _isPaidSubscription = true;
                
                MoexAlgopackAuth auth = new MoexAlgopackAuth(_username, _password);
                
                SendLogMessage($"Authorization: status code : {auth.LastStatus}, status message : {auth.LastStatusText}", LogMessageType.Connect);
                
                if (auth == null || !auth.IsRealTime())
                {
                    SendLogMessage("Cannot access real-time and historical data. Try to reauthenticate.", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent?.Invoke();
                    return;
                }

                if (!_isAuthorized)
                {
                    handler.CookieContainer = new CookieContainer();
                    handler.CookieContainer.Add(new Uri("https://www.moex.com"), auth.Passport);
                    _isAuthorized = true;
                }
                
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent?.Invoke();
            }

            public void Dispose()
            {
                securities.Clear();
                SendLogMessage("Connection Closed by MoexAlgopack.", LogMessageType.System);
                
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent?.Invoke();
                }
            }

            public ServerType ServerType
            {
                get { return ServerType.MoexAlgopack; }
            }

            public ServerConnectStatus ServerStatus { get; set; }

            public event Action ConnectEvent;

            public event Action DisconnectEvent;

            public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

            #endregion

            #region 2 Properties

            public DateTime ServerTime { get; set; }

            static HttpClientHandler handler = new HttpClientHandler();

            public List<IServerParameter> ServerParameters { get; set; }

            private string _username;

            private string _password;

            private bool _isPaidSubscription;

            private bool _isFakeDepth;

            private const string BaseUrl = "https://iss.moex.com/iss";

            public event Action<Order> MyOrderEvent;

            public event Action<MyTrade> MyTradeEvent { add { } remove { } }

            public event Action<MarketDepth> MarketDepthEvent;

            public event Action<Trade> NewTradesEvent;

            public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

            #endregion

            #region 3 Securities

            public void GetSecurities()
            {
                try
                {
                    string json;
                    HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(BaseUrl + "/engines/stock/markets/shares/boards/tqbr/securities.json?iss.meta=off&iss.only=securities").Result;
                    
                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        json = responseMessage.Content.ReadAsStringAsync().Result;
                    }
                    else
                    {
                        SendLogMessage("GetSecurities Http error: " + responseMessage.StatusCode, LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent?.Invoke();
                        return;
                    }
                    
                    if (json != null)
                    {
                       UpdateSecurity(json);
                    }
                    else
                    {
                        SendLogMessage($"Error getting securities : json is null.", LogMessageType.Error);
                    }

                    responseMessage = _httpPublicClient.GetAsync(BaseUrl + "/engines/futures/markets/forts/securities.json?iss.meta=off&iss.only=securities").Result;
                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        json = responseMessage.Content.ReadAsStringAsync().Result;
                    }
                    else
                    {
                        SendLogMessage("GetSecurities Http error: " + responseMessage.StatusCode, LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent?.Invoke();
                        return;
                    }

                    if (json != null)
                    {
                        UpdateFutures(json);
                    }
                    else
                    {
                        SendLogMessage($"Error getting futures : json is null.", LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage("GetSecurities error: " + exception, LogMessageType.Error);
                }
            }

            List<Security> securities = new List<Security>();
            
            private void UpdateFutures(string json)
            {
                ResponseSecurities symbols = JsonConvert.DeserializeAnonymousType(json, new ResponseSecurities());
                
                for (int i = 0; i < symbols.securities.data.Count; i++)
                {
                    List<string> item = symbols.securities.data[i];
                    Security newSecurity = new Security();
                    
                    newSecurity.Exchange = ServerType.MoexAlgopack.ToString();
                    newSecurity.Lot = item[13].ToDecimal();
                    newSecurity.Name = item[0];
                    newSecurity.NameFull = item[2];
                    newSecurity.NameClass = "Фьючерсы/Фортс";
                    newSecurity.NameId = item[0]; 
                    newSecurity.SecurityType = SecurityType.Futures;
                    newSecurity.Decimals = Convert.ToInt32(item[5]);
                    newSecurity.DecimalsVolume = 0;
                    newSecurity.PriceStep = item[6].ToDecimal();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.MarginBuy = item[14].ToDecimal();
                    newSecurity.Expiration = DateTime.Parse(item[7],CultureInfo.InvariantCulture); 
                    newSecurity.State = SecurityStateType.Activ;
                    securities.Add(newSecurity);
                }

                SecurityEvent?.Invoke(securities);
            }
            
            private void UpdateSecurity(string json)
            {
                ResponseSecurities symbols = JsonConvert.DeserializeAnonymousType(json, new ResponseSecurities());

                for (int i = 0; i < symbols.securities.data.Count; i++)
                {
                    List<string> item = symbols.securities.data[i];
                    Security newSecurity = new Security();
                    
                    newSecurity.Exchange = ServerType.MoexAlgopack.ToString();
                    newSecurity.Lot = item[4].ToDecimal();
                    newSecurity.Name = item[0];
                    newSecurity.NameFull = item[2];
                    newSecurity.NameClass = "Акции/TQBR";
                    newSecurity.NameId = item[0]; 
                    newSecurity.SecurityType = SecurityType.Stock;
                    newSecurity.Decimals = Convert.ToInt32(item[8]);
                    newSecurity.DecimalsVolume = 0;
                    newSecurity.PriceStep = item[14].ToDecimal();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.State = SecurityStateType.Activ;
                    securities.Add(newSecurity);
                }

                SecurityEvent?.Invoke(securities);
            }

            public event Action<List<Security>> SecurityEvent;

            #endregion

            #region 4 Portfolios

            public void GetPortfolios()
            {
                Portfolio portfolio = new Portfolio();
                portfolio.ValueCurrent = 1;
                portfolio.Number = "MoexAlgopack Virtual Portfolio";

                PortfolioEvent?.Invoke(new List<Portfolio>() {portfolio});
            }

            public event Action<List<Portfolio>> PortfolioEvent;
            
            #endregion

            #region 5 Data

            private readonly object _locker = new object();

            private ConcurrentQueue<Trade> _recievedTrades = new ConcurrentQueue<Trade>();

            private DateTime _lastMdTime = DateTime.MinValue;
            
            public void GetNewData()
            {
                Thread.Sleep(2000);

                Dictionary<string, string> lastTradeId = new Dictionary<string, string>();
                Dictionary<string, DateTime> lastMarketDepthTime = new Dictionary<string, DateTime>();
                
                while (true)
                {
                    if (ServerStatus != ServerConnectStatus.Connect || _subscribedSecurities.Count == 0)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }
                    
                    for (int i = 0; i < _subscribedSecurities.Count; i++)
                    {
                        if (_isPaidSubscription && _isAuthorized)
                        {
                            MarketDepth marketDepth = GetQueryDepth(_subscribedSecurities[i]);
                            
                            if (marketDepth != null && marketDepth.Asks.Count !=0 && marketDepth.Bids.Count != 0)
                            {
                                if(lastMarketDepthTime.ContainsKey(marketDepth.SecurityNameCode))
                                {
                                    _lastMdTime = lastMarketDepthTime[marketDepth.SecurityNameCode];
                                }
                                else
                                {
                                    _lastMdTime = DateTime.MinValue;
                                    lastMarketDepthTime[marketDepth.SecurityNameCode] = marketDepth.Time;
                                }
                                
                                
                                if (marketDepth.Time > _lastMdTime)
                                {
                                    MarketDepthEvent?.Invoke(marketDepth);
                                    lastMarketDepthTime[marketDepth.SecurityNameCode] = marketDepth.Time;
                                }
                            }
                        }
                        
                        string tradeId = lastTradeId.ContainsKey(_subscribedSecurities[i].NameId) ? lastTradeId[_subscribedSecurities[i].NameId] : "0";
                        
                        List<Trade> lastTrades = GetQueryTrades(tradeId, _subscribedSecurities[i]);

                        if (lastTrades == null || lastTrades.Count == 0) continue;
                        
                        tradeId = lastTrades[lastTrades.Count - 1].Id;
                        lastTradeId[_subscribedSecurities[i].NameId] = tradeId;
                        
                        for(int j = 0; j < lastTrades.Count; j++)
                        {
                            Trade trade = lastTrades[j];
                            _recievedTrades.Enqueue(trade);
                        }
                    }
                }
            }
            
            public void UpdateAllData()
            {
                while (true)
                {
                    Thread.Sleep(5);
                    
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_recievedTrades == null || _recievedTrades.Count == 0)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    UpdateTradesDepths();
                }
            }

            private void UpdateTradesDepths()
            {
                Trade newTrade;
                decimal priceStep = 0;
                
                _recievedTrades.TryDequeue(out newTrade);
                    
                if (newTrade == null)
                {
                    return;
                }
                
                NewTradesEvent?.Invoke(newTrade);

                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if(_subscribedSecurities[i].Name.Equals(newTrade.SecurityNameCode))
                    {
                        priceStep = _subscribedSecurities[i].PriceStep;
                        break;
                    }
                }

                if (!_isFakeDepth) return;

                MarketDepth marketDepth = new MarketDepth();
                marketDepth.SecurityNameCode = newTrade.SecurityNameCode;
                marketDepth.Time = newTrade.Time;
                MarketDepthLevel newBid = new MarketDepthLevel();
                newBid.Price = Convert.ToDouble(newTrade.Price - priceStep);
                newBid.Bid = Convert.ToDouble(newTrade.Volume);
                marketDepth.Bids.Add(newBid);
                MarketDepthLevel newAsk = new MarketDepthLevel();
                newAsk.Price = Convert.ToDouble(newTrade.Price + priceStep);
                newAsk.Ask = Convert.ToDouble(newTrade.Volume);
                marketDepth.Asks.Add(newAsk);
                
                if(_lastMdTime != DateTime.MinValue &&
                   _lastMdTime >= marketDepth.Time)
                {
                    marketDepth.Time = _lastMdTime.AddMilliseconds(0.001);
                }

                _lastMdTime = marketDepth.Time;
                
                MarketDepthEvent?.Invoke(marketDepth);
            }
            
            public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
            {
                return null;
            }
            
            public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
            {
                DateTime endTime = DateTime.Now.ToUniversalTime().AddHours(3);

                while(endTime.Hour != 23)
                {
                    endTime = endTime.AddHours(1);
                }

                int candlesInDay;

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
                { 
                    daysCount += (daysCount / 5) * 2;
                }

                DateTime startTime = endTime.AddDays(-daysCount);

                List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);
        
                while(candles.Count > candleCount)
                {
                    candles.RemoveAt(0);
                }

                return candles;
            }

            public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
            {
                if(startTime != actualTime)
                {
                    startTime = actualTime;
                }
                if (startTime >= endTime || startTime >= DateTime.Now)
                {
                    return null;
                }
                
                lock (_locker)
                {
                    List<Candle> candles = new List<Candle>();

                    int minutesInTf = Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);

                    if (minutesInTf >= 1 && minutesInTf < 10)
                    {
                        List<Candle> sourseCandle = GetAllCandles(security, startTime, 1, endTime);
                        candles = ConcateCandles(sourseCandle, 1, minutesInTf);
                    }
                    else if (minutesInTf == 15 || minutesInTf == 45)
                    {
                        List<Candle> sourseCandle = GetAllCandles(security, startTime, 1, endTime);
                        candles = ConcateCandles(sourseCandle, 1, minutesInTf);
                    }
                    else if (minutesInTf >= 10 && minutesInTf < 60)
                    {
                        List<Candle> sourseCandle = GetAllCandles(security, startTime, 10, endTime);
                        candles = ConcateCandles(sourseCandle, 10, minutesInTf);
                    }
                    else if (minutesInTf >= 60)
                    {
                        List<Candle> sourseCandle = GetAllCandles(security, startTime, 60, endTime);
                        candles = ConcateCandles(sourseCandle, 60, minutesInTf);
                    }
                    
                    while (candles != null &&
                           candles.Count != 0 && 
                           candles[candles.Count - 1].TimeStart > endTime)
                    {
                        candles.RemoveAt(candles.Count - 1);
                    }
                    return candles;
                }
            }

            private List<Candle> ConcateCandles(List<Candle> candlesOld, int startTf, int endTf)
            {
                if (startTf == endTf)
                {
                    return candlesOld;
                }

                TimeSpan candleMinuteLen = new TimeSpan(0, endTf, 0);

                int countOldCandlesInOneNew = endTf / startTf;

                List<Candle> candlesNew = new List<Candle>();

                for (int i = 0; i < candlesOld.Count; i++)
                {
                    Candle newCandle = new Candle();
                    newCandle.TimeStart = candlesOld[i].TimeStart;

                    if (startTf == 1
                        && (endTf == 2 || endTf == 5 || endTf == 15))
                    {
                        while (newCandle.TimeStart.Minute != 0
                               && newCandle.TimeStart.Minute % endTf != 0)
                        {
                            newCandle.TimeStart = newCandle.TimeStart.AddMinutes(1);
                        }
                    }

                    if (startTf == 10
                        && (endTf == 30))
                    {
                        while (newCandle.TimeStart.Minute != 0
                               && newCandle.TimeStart.Minute != 30)
                        {
                            newCandle.TimeStart = newCandle.TimeStart.AddMinutes(1);
                        }
                    }

                    if (startTf == 1
                        && (endTf == 45))
                    {
                        while (newCandle.TimeStart.Minute != 0
                               && newCandle.TimeStart.Minute % 5 != 0)
                        {
                            newCandle.TimeStart = newCandle.TimeStart.AddMinutes(1);
                        }
                    }

                    newCandle.Open = candlesOld[i].Open;
                    newCandle.High = candlesOld[i].High;
                    newCandle.Low = candlesOld[i].Low;
                    newCandle.Close = candlesOld[i].Close;
                    newCandle.Volume += candlesOld[i].Volume;
                    newCandle.State = CandleState.Finished;
                    i++;

                    for (int i2 = 0; i2 < countOldCandlesInOneNew - 1 && i < candlesOld.Count; i2++)
                    {
                        if (newCandle.TimeStart.Hour != 10 &&
                            newCandle.TimeStart.Minute != 0 &&
                            candlesOld[i].TimeStart.Hour == 10 &&
                            candlesOld[i].TimeStart.Minute == 0)
                        {
                            i--;
                            break;
                        }

                        if (newCandle.TimeStart.Day != candlesOld[i].TimeStart.Day)
                        {
                            i--;
                            break;
                        }

                        DateTime endCandleTime = newCandle.TimeStart.Add(candleMinuteLen);

                        if (candlesOld[i].TimeStart >= endCandleTime)
                        {
                            i--;
                            break;
                        }

                        if (candlesOld[i].High > newCandle.High)
                        {
                            newCandle.High = candlesOld[i].High;
                        }

                        if (candlesOld[i].Low < newCandle.Low)
                        {
                            newCandle.Low = candlesOld[i].Low;
                        }

                        newCandle.Close = candlesOld[i].Close;
                        newCandle.Volume += candlesOld[i].Volume;

                        if (i2 + 1 < countOldCandlesInOneNew - 1)
                        {
                            i++;
                        }
                    }

                    candlesNew.Add(newCandle);
                }

                return candlesNew;
            }

            public List<Candle> GetAllCandles(Security security, DateTime startTime, int minutesCount, DateTime endTime)
            {
                int startCandle = 0;

                DateTime lastTime = startTime;

                List<Candle> candles = new List<Candle>();

                while (lastTime < endTime)
                {
                    List<Candle> curCandles = Get500LastCandles(security, startTime, startCandle, minutesCount);
                    startCandle += 500;

                    if (curCandles == null ||
                        curCandles.Count == 0)
                    {
                        break;
                    }

                    candles.AddRange(curCandles);
                    lastTime = curCandles[curCandles.Count - 1].TimeStart;
                }

                return candles;
            }

            public List<Candle> Get500LastCandles(Security security, DateTime startTime, int startCandle, int minutesCount)
            {
                
                string engine = "futures";
                string market = "forts";
                string board = "rfud";
                
                if (security.NameClass.Equals("Акции/TQBR"))
                {
                    engine ="stock";
                    market = "shares";
                    board = "tqbr";
                }
                
                string str = "http://iss.moex.com/iss/engines/";
                str += engine;
                str += "/markets/";
                str += market;
                str += "/boards/";
                str += board;
                str += "/securities/";
                str += security.NameId;
                str += "/candles.json?iss.meta=off&iss.only=candles&from=";
                str += startTime.Year + "-";

                if (startTime.Month < 10)
                {
                    str += "0" + startTime.Month + "-";
                }
                else
                {
                    str += startTime.Month + "-";
                }

                if (startTime.Day < 10)
                {
                    str += "0" + startTime.Day + "-";
                }
                else
                {
                    str += startTime.Day;
                }
                str += "&interval=";
                str += minutesCount;
                str += "&start=";
                str += startCandle;

                List<Candle> result = CreateQueryCandles(str);
                
                return result;
            }

            #endregion

            #region 6 Security Subscribed

            private readonly RateGate _rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(200));

            public void Subscribe(Security security)
            {
                try
                {
                    _rateGateSubscribed.WaitToProceed();
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        return;
                    }

                    CreateSubscribedSecurityMessage(security);
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }

            public bool SubscribeNews()
            {
                return false;
            }

            public event Action<News> NewsEvent { add { } remove { } }

            #endregion

            #region 7 Trade

            public void SendOrder(Order order)
            {
                
            }

            public void ChangeOrderPrice(Order order, decimal newPrice)
            {

            }

            public void CancelAllOrders()
            {
                
            }

            public void CancelAllOrdersToSecurity(Security security)
            {
                
            }

            public bool CancelOrder(Order order)
            {
                return false;
            }
           
            private void CreateOrderFail(Order order)
            {
                order.State = OrderStateType.Fail;

                MyOrderEvent?.Invoke(order);
            }

            public void GetAllActivOrders()
            {

            }


            public List<Order> GetActiveOrders(int startIndex, int count)
            {
                return null;
            }

            public List<Order> GetHistoricalOrders(int startIndex, int count)
            {
                return null;
            }

            public OrderStateType GetOrderStatus(Order order)
            {
                return OrderStateType.None;
            }

            #endregion

            #region 8 Queries

            private readonly RateGate _rateGateGetData = new RateGate(1, TimeSpan.FromMilliseconds(100));

            HttpClient _httpPublicClient = new HttpClient(handler);

            private List<Security> _subscribedSecurities = new List<Security>();

            private void CreateSubscribedSecurityMessage(Security security)
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

                    _subscribedSecurities.Add(security);

                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(),LogMessageType.Error);
                }
            }

            private List<Candle> CreateQueryCandles(string str)
            {
                _rateGateGetData.WaitToProceed();

                try
                {
                    HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(str).Result;
                    string content = responseMessage.Content.ReadAsStringAsync().Result;

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        ResponseCandles symbols = JsonConvert.DeserializeAnonymousType(content, new ResponseCandles());

                        if (symbols != null && symbols.candles.data.Count > 0)
                        {
                            List<Candle> candles = new List<Candle>();

                            for (int i = 0; i < symbols.candles.data.Count; i++)
                            {
                                List<string> item = symbols.candles.data[i];

                                Candle newCandle = new Candle();

                                newCandle.Open = item[0].ToDecimal();
                                newCandle.Close = item[1].ToDecimal();
                                newCandle.High = item[2].ToDecimal();
                                newCandle.Low = item[3].ToDecimal();
                                newCandle.Volume = item[5].ToDecimal();
                                newCandle.State = CandleState.Finished;
                                newCandle.TimeStart = DateTime.Parse(item[6], CultureInfo.InvariantCulture);
                                candles.Add(newCandle);
                            }

                            return candles;
                        }

                        return null;
                    }

                    SendLogMessage($"CreateQueryCandles error, State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                    return null;
                }
                catch(Exception exc)
                {
                    SendLogMessage($"CreateQueryCandles error, {exc.Message}", LogMessageType.Error);
                }

                return null;
            }
            
            private MarketDepth GetQueryDepth(Security sec)
            {
                //https://iss.moex.com/iss/engines/stock/markets/shares/boards/TQBR/securities/SBER/orderbook.json?iss.meta=off&iss.only=orderbook
                //https://iss.moex.com/iss/engines/futures/markets/forts/securities/BRJ5/orderbook.json?iss.meta=off&iss.only=orderbook
                
                _rateGateGetData.WaitToProceed();
                
                string uriDepth = "";
                MarketDepth marketDepth = new MarketDepth();
                ResponseDepth data;
                
                string engine = "futures";
                string market = "forts";
                string board = "rfud";
                
                if (sec.NameClass.Equals("Акции/TQBR"))
                {
                    engine = "stock";
                    market = "shares";
                    board = "tqbr";
                }
                
                uriDepth = $"/engines/{engine}/markets/{market}/boards/{board}/securities/{sec.NameId}/orderbook.json?iss.meta=off&iss.only=orderbook";

                try
                {
                    HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(BaseUrl + uriDepth).Result;
                    string content = responseMessage.Content.ReadAsStringAsync().Result;
                    
                    if (content.Contains("<!DOCTYPE html>"))
                    {
                        _isFakeDepth = true;
                        return null;
                    }
                    
                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        data = JsonConvert.DeserializeAnonymousType(content, new ResponseDepth());

                        if (data == null || data.orderbook.data.Count == 0)
                            return null;

                        for (int i = data.orderbook.data.Count / 2 - 1; i >= 0; i--)
                        {
                            List<string> item = data.orderbook.data[i];

                            marketDepth.SecurityNameCode = item[1];
                            marketDepth.Time = Convert.ToDateTime(item[6]);

                            if (!item[2].Equals("B"))
                                continue;

                            MarketDepthLevel newBid = new MarketDepthLevel();
                            newBid.Price = item[3].ToDouble();
                            newBid.Bid = item[4].ToDouble();
                            marketDepth.Bids.Add(newBid);
                        }

                        for (int i = data.orderbook.data.Count / 2; i < data.orderbook.data.Count; i++)
                        {
                            List<string> item = data.orderbook.data[i];

                            if (!item[2].Equals("S"))
                                continue;

                            MarketDepthLevel newAsk = new MarketDepthLevel();
                            newAsk.Price = item[3].ToDouble();
                            newAsk.Ask = item[4].ToDouble();
                            marketDepth.Asks.Add(newAsk);
                        }

                        _isFakeDepth = false;
                        return marketDepth;
                    }

                    _isFakeDepth = true;
                    return null;
                }
                catch (Exception exc)
                {
                    SendLogMessage($"GetQueryDepth Error: {exc.Message}", LogMessageType.Error);
                    _isFakeDepth = true;
                }

                return null;
            }
            
            private List<Trade> GetQueryTrades(string tradeId, Security sec)
            {
                //https://iss.moex.com/iss/engines/stock/markets/shares/securities/sber/trades.json?iss.meta=off&iss.only=trades&tradeno=9917096593
                
                _rateGateGetData.WaitToProceed();
                
                string uriCandles = "";
                List<Trade> trades = new List<Trade>();
                ResponseTrades data;
                string newTradeId = tradeId;
                
                string engine = "futures";
                string market = "forts";
                string board = "rfud";
                
                if (sec.NameClass.Equals("Акции/TQBR"))
                {
                    engine = "stock";
                    market = "shares";
                    board = "tqbr";
                }
                
                if(newTradeId.Equals("0"))
                {
                    uriCandles = $"/engines/{engine}/markets/{market}/boards/{board}/securities/{sec.NameId}/trades.json?iss.only=trades&iss.meta=off&reversed=1&limit=1";
                    try
                    {
                        HttpResponseMessage responseMessageStart = _httpPublicClient.GetAsync(BaseUrl + uriCandles).Result;
                        string contentStart = responseMessageStart.Content.ReadAsStringAsync().Result;

                        if (responseMessageStart.StatusCode == HttpStatusCode.OK)
                        {
                            data = JsonConvert.DeserializeAnonymousType(contentStart, new ResponseTrades());

                            if (data == null || data.trades.data.Count == 0)
                                return null;

                            newTradeId = data.trades.data[0][0];
                        }
                        else
                        {
                            SendLogMessage($"CreateQueryTrades error, State Code: {responseMessageStart.StatusCode}",
                                LogMessageType.Error);
                            return null;
                        }
                    }
                    catch
                    {
                        //ignore
                    }
                }
                
                uriCandles = $"/engines/{engine}/markets/{market}/boards/{board}/securities/{sec.NameId}/trades.json?iss.only=trades&iss.meta=off&tradeno={newTradeId}&next_trade=1";

                try
                {
                    HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(BaseUrl + uriCandles).Result;
                    string content = responseMessage.Content.ReadAsStringAsync().Result;

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        data = JsonConvert.DeserializeAnonymousType(content, new ResponseTrades());

                        if (data == null || data.trades.data.Count == 0)
                            return null;
                        if (board.Equals("tqbr"))
                        {
                            for (int i = 0; i < data.trades.data.Count; i++)
                            {
                                List<string> item = data.trades.data[i];

                                Trade newTrade = new Trade();

                                newTrade.Id = item[0];
                                newTrade.SecurityNameCode = item[3];
                                newTrade.Price = item[4].ToDecimal();
                                newTrade.Time = Convert.ToDateTime(item[1]);
                                newTrade.Side = item[10].Equals("S") ? Side.Sell : Side.Buy;
                                newTrade.Volume = item[5].ToDecimal();
                                trades.Add(newTrade);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < data.trades.data.Count; i++)
                            {
                                List<string> item = data.trades.data[i];

                                Trade newTrade = new Trade();

                                newTrade.Id = item[0];
                                newTrade.SecurityNameCode = item[2];
                                newTrade.Price = item[5].ToDecimal();
                                newTrade.Time = Convert.ToDateTime(item[4]);
                                newTrade.Side = item[11].Equals("S") ? Side.Sell : Side.Buy;
                                newTrade.Volume = item[6].ToDecimal();
                                trades.Add(newTrade);
                            }
                        }

                        return trades;
                    }
                    SendLogMessage($"CreateQueryTrades error, State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    return null;
                }
                catch
                {
                    //ignore
                }

                return null;
            }
            
            #endregion

            #region 9 Log

            public void SendLogMessage(string message, LogMessageType messageType)
            {
                LogMessageEvent?.Invoke(message, messageType);
            }

            public event Action<string, LogMessageType> LogMessageEvent;

            public event Action<Funding> FundingUpdateEvent { add { } remove { } }

            public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

            #endregion
        }
    }
}