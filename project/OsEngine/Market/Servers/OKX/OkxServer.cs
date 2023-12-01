using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.OKX.Entity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

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

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((OkxServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }

    public class OkxServerRealization : IServerRealization
    {
        public OkxServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        OkxClient _client;
        
        public ServerType ServerType
        {
            get { return ServerType.OKX; }
        }

        public ServerConnectStatus ServerStatus { get; set; }
        public List<IServerParameter> ServerParameters { get; set; }
        public DateTime ServerTime { get; set; }

        public void Connect()
        {
            if (_client == null)
            {
                _client = new OkxClient(
                    ((ServerParameterString)ServerParameters[0]).Value,
                    ((ServerParameterPassword)ServerParameters[1]).Value,
                    ((ServerParameterPassword)ServerParameters[2]).Value
                    );
                _client.Connected += _client_Connected;
                _client.UpdatePairs += _client_UpdatePairs;
                _client.Disconnected += _client_Disconnected;
                _client.NewPortfolio += _client_NewPortfolio;
                _client.UpdatePortfolio += _client_UpdatePortfolio;
                _client.UpdateMarketDepth += _client_UpdateMarketDepth;
                _client.NewTradesEvent += _client_NewTradesEvent;
                _client.MyOrderEvent += _client_MyOrderEvent;
                _client.LogMessageEvent += SendLogMessage;
                _client.MyOrderEventFail += _client_MyOrderEventFail;
            }
            _client.Connect();
        }

        public void Dispose()
        {
            _subscribledSecurities.Clear();

            if (_client != null)
            {
                _client.Dispose();

                _client.Connected -= _client_Connected;
                _client.UpdatePairs -= _client_UpdatePairs;
                _client.Disconnected -= _client_Disconnected;
                _client.NewPortfolio -= _client_NewPortfolio;
                _client.UpdatePortfolio -= _client_UpdatePortfolio;
                _client.UpdateMarketDepth -= _client_UpdateMarketDepth;
                _client.NewTradesEvent -= _client_NewTradesEvent;
                _client.MyOrderEvent -= _client_MyOrderEvent;
                _client.LogMessageEvent -= SendLogMessage;
                _client.MyOrderEventFail -= _client_MyOrderEventFail;
            }

            _client = null;
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        private void _client_Connected()
        {

            if (ConnectEvent != null)
            {
                ConnectEvent();
            }

            ServerStatus = ServerConnectStatus.Connect;
        }

        private void _client_Disconnected()
        {
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        private List<Security> _subscribledSecurities = new List<Security>();

        public void Subscrible(Security security)
        {
            for(int i = 0;i < _subscribledSecurities.Count;i++)
            {
                if (_subscribledSecurities[i].Name ==  security.Name 
                    && _subscribledSecurities[i].NameClass == security.NameClass)
                {
                    return;
                }
            }

            _subscribledSecurities.Add(security);

            _client._rateGateWebSocket.WaitToProceed();

            _client.SubscribleTrades(security);
            _client.SubscribleDepths(security);
        }

        #region Trade

        public void CancelAllOrdersToSecurity(Security security)
        {

        }

        public void CancelAllOrders()
        {
            //Empty
        }

        public void CancelOrder(Order order)
        {
            _client.CancelOrder(order);
        }

        private void _client_MyOrderEventFail(Order order)
        {
            order.State = OrderStateType.Fail;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        private void OrderUpdate(ObjectChanel<OrderResponseData> OrderResponse, OrderStateType stateType)
        {
            var item = OrderResponse.data[0];

            Order newOrder = new Order();
            newOrder.SecurityNameCode = item.instId;
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));

            if (item.clOrdId.Equals(String.Empty))
            {
                return;
            }

            if (!item.clOrdId.Equals(String.Empty))
            {
                newOrder.NumberUser = Convert.ToInt32(item.clOrdId);
            }

            newOrder.NumberMarket = item.ordId.ToString();

            if(item.posSide == "net")
            {
                newOrder.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
            }
            else
            {
                newOrder.Side = item.posSide.Equals("long") ? Side.Buy : Side.Sell;
            }
            
            newOrder.State = stateType;
            newOrder.Volume = item.sz.Replace('.', ',').ToDecimal();
            newOrder.Price = item.avgPx.Replace('.', ',').ToDecimal() != 0 ? item.avgPx.Replace('.', ',').ToDecimal() : item.px.Replace('.', ',').ToDecimal();
            newOrder.ServerType = ServerType.OKX;
            newOrder.PortfolioNumber = newOrder.SecurityNameCode;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(newOrder);
            }


            if (stateType == OrderStateType.Patrial ||
                stateType == OrderStateType.Done)
            {
                List<MyTrade> tradesInOrder = GenerateTradesToOrder(newOrder, 1);

                for (int i = 0; i < tradesInOrder.Count; i++)
                {
                    MyTradeEvent(tradesInOrder[i]);
                }
            }
        }

        private RateGate _rateGateGenerateToTrate = new RateGate(1, TimeSpan.FromMilliseconds(300));

        private List<MyTrade> GenerateTradesToOrder(Order order, int SeriasCalls)
        {
            _rateGateGenerateToTrate.WaitToProceed();

            List<MyTrade> myTrades = new List<MyTrade>();

            if (SeriasCalls >= 4)
            {
                SendLogMessage($"Trade is not found to order: {order.NumberUser}", LogMessageType.Error);
                return myTrades;
            }

            string TypeInstr = order.SecurityNameCode.EndsWith("SWAP") ? "SWAP" : "SPOT";

            var url = $"{"https://www.okx.com/"}{"api/v5/trade/fills-history"}" + $"?ordId={order.NumberMarket}&" + $"instId={order.SecurityNameCode}&" + $"instType={TypeInstr}";

            var res = _client.GetBalanseOrMyTradesRequest(url);

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
                Thread.Sleep(200 * SeriasCalls);

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

                if(string.IsNullOrEmpty(item.fee))
                {
                    myTrade.Volume = item.fillSz.ToDecimal();
                }
                else
                {// комиссия есть

                    if(item.instId.StartsWith(item.feeCcy))
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

                if(item.posSide == "net")
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

        private void _client_MyOrderEvent(ObjectChanel<OrderResponseData> OrderResponse)
        {
            lock (lokerOrder)
            {
                if (OrderResponse.data == null || OrderResponse.data.Count == 0)
                {
                    return;
                }

                if ((OrderResponse.data[0].ordType.Equals("limit") ||
                OrderResponse.data[0].ordType.Equals("market"))
                &&
                OrderResponse.data[0].state.Equals("filled"))
                {
                    OrderUpdate(OrderResponse, OrderStateType.Done);
                }

                else if ((OrderResponse.data[0].ordType.Equals("limit") ||
                   OrderResponse.data[0].ordType.Equals("market"))
                    &&
                    OrderResponse.data[0].state.Equals("live"))
                {
                    OrderUpdate(OrderResponse, OrderStateType.Activ);
                }

                else if ((OrderResponse.data[0].ordType.Equals("limit") ||
                    OrderResponse.data[0].ordType.Equals("market"))
                    &&
                    OrderResponse.data[0].state.Equals("canceled"))
                {
                    OrderUpdate(OrderResponse, OrderStateType.Cancel);
                }
            }

            _client.GetPortfolios();

        }

        public void GetOrdersState(List<Order> orders)
        {
            
        }

        public void SendOrder(Order order)
        {
            _client.ExecuteOrder(order);
        }

        #endregion

        #region Ticks

        private string _newTradelocked = "okxNewTradesLocker";

        private void _client_NewTradesEvent(TradeResponse tradeRespone)
        {

            lock (_newTradelocked)
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

        }

        #endregion

        #region MarketDepth 

        private List<MarketDepth> _depths;

        private string _depthLocker = "okxNewMdLocker";

        private void _client_UpdateMarketDepth(DepthResponse depthResponse)
        {
            try
            {
                lock (_depthLocker)
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
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
            }
        }

        #endregion

        #region Pofrfolio

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

        private void _client_UpdatePortfolio(PorfolioResponse portfs)
        {
            _client_NewPortfolio(portfs);
        }

        private void _client_NewPortfolio(PorfolioResponse portfs)
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
                SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
            }
        }

        public void GetPortfolios()
        {
            if (_client != null)
            {
                _client.GetPortfolios();
            }
        }

        #endregion

        #region Securities 

        private List<Security> _securities;

        private void _client_UpdatePairs(SecurityResponce securityResponce)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

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

        public void GetSecurities()
        {
            if (_client != null)
            {
                _client.GetSecurities();
            }

        }

        #endregion

        #region Data

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

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (endTime > DateTime.Now)
            {
                endTime = DateTime.Now;
            }

            var CountCandlesNeedToLoad = GetCountCandlesFromTimeInterval(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

            return _client.GetCandleDataHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan, CountCandlesNeedToLoad, TimeManager.GetTimeStampMilliSecondsToDateTime(endTime));
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            List<Candle> candles = _client.GetCandleHistory(nameSec, tf);


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

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Outgoing events

        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<List<Security>> SecurityEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action ConnectEvent;
        public event Action DisconnectEvent;
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

    }
}
