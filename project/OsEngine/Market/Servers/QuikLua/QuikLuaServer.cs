using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.Utils;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using QuikSharp;
using QuikSharp.DataStructures;
using QuikSharp.DataStructures.Transaction;
using Candle = OsEngine.Entity.Candle;
using Order = OsEngine.Entity.Order;
using Trade = OsEngine.Entity.Trade;

namespace OsEngine.Market.Servers.QuikLua
{
    public class QuikLuaServer : AServer
    {
        public QuikLuaServer()
        {
            ServerRealization = new QuikLuaServerRealization();
        }

        /// <summary>
        /// tame candles by instrument
        /// взять свечи по инструменту
        /// </summary>
        /// <param name="security"> short security name/короткое название бумаги</param>
        /// <param name="timeSpan">timeframe/таймФрейм</param>
        /// <returns>failure will return null/в случае неудачи вернётся null</returns>
        public List<Candle> GetQuikLuaCandleHistory(string security, TimeSpan timeSpan)
        {
            return ((QuikLuaServerRealization) ServerRealization).GetQuikLuaCandleHistory(security, timeSpan);
        }
    }

    public class QuikLuaServerRealization : IServerRealization
    {
        public QuikLuaServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread updateSpotPos = new Thread(UpdateSpotPosition);
            updateSpotPos.CurrentCulture = new CultureInfo("ru-RU");
            updateSpotPos.IsBackground = true;
            updateSpotPos.Start();

            Thread getPos = new Thread(GetPortfoliosArea);
            getPos.CurrentCulture = new CultureInfo("ru-RU");
            getPos.IsBackground = true;
            getPos.Start();
        }

        public ServerType ServerType => ServerType.QuikLua;

        public ServerConnectStatus ServerStatus { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

        public QuikSharp.Quik QuikLua;

        private object _serverLocker = new object();

        private static readonly Char Separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
        
        private static readonly string SecuritiesCachePath = @"Engine\QuikLuaSecuritiesCache.txt";

        public void Connect()
        {
            if (QuikLua == null)
            {
                QuikLua = new QuikSharp.Quik(QuikSharp.Quik.DefaultPort, new InMemoryStorage());
                QuikLua.Events.OnConnected += EventsOnOnConnected;
                QuikLua.Events.OnDisconnected += EventsOnOnDisconnected;
                QuikLua.Events.OnConnectedToQuik += EventsOnOnConnectedToQuik;
                QuikLua.Events.OnDisconnectedFromQuik += EventsOnOnDisconnectedFromQuik;
                QuikLua.Events.OnTrade += EventsOnOnTrade;
                QuikLua.Events.OnOrder += EventsOnOnOrder;
                QuikLua.Events.OnQuote += EventsOnOnQuote;
                QuikLua.Events.OnFuturesClientHolding += EventsOnOnFuturesClientHolding;
                QuikLua.Events.OnFuturesLimitChange += EventsOnOnFuturesLimitChange;

                QuikLua.Service.QuikService.Start();
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent?.Invoke();
            }
        }

        public void Dispose()
        {
            try
            {
                if (QuikLua != null && QuikLua.Service.IsConnected().Result)
                {
                    QuikLua.Service.QuikService.Stop();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            if (QuikLua != null)
            {
                QuikLua.Events.OnConnected -= EventsOnOnConnected;
                QuikLua.Events.OnDisconnected -= EventsOnOnDisconnected;
                QuikLua.Events.OnConnectedToQuik -= EventsOnOnConnectedToQuik;
                QuikLua.Events.OnDisconnectedFromQuik -= EventsOnOnDisconnectedFromQuik;
                QuikLua.Events.OnTrade -= EventsOnOnTrade;
                QuikLua.Events.OnOrder -= EventsOnOnOrder;
                QuikLua.Events.OnQuote -= EventsOnOnQuote;
                QuikLua.Events.OnFuturesClientHolding -= EventsOnOnFuturesClientHolding;
                QuikLua.Events.OnFuturesLimitChange -= EventsOnOnFuturesLimitChange;
            }

            ServerStatus = ServerConnectStatus.Disconnect;
            DisconnectEvent?.Invoke();
            subscribedBook = new List<string>();
            QuikLua = null;
        }

        private List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            try
            {
                _securities = IsLoadSecuritiesFromCache() ? LoadSecuritiesFromCache() : LoadSecuritiesFromQuik();

                SendLogMessage(OsLocalization.Market.Message52 + _securities.Count, LogMessageType.System);
                if (SecurityEvent != null)
                {
                    SecurityEvent(_securities);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool IsLoadSecuritiesFromCache()
        {
            if (!File.Exists(SecuritiesCachePath))
            {
                return false;
            }

            DateTime lastWriteTime = File.GetLastWriteTime(SecuritiesCachePath);
            return DateTime.Now < lastWriteTime.AddHours(1);
        }

        private List<Security> LoadSecuritiesFromQuik()
        {
            string[] classesList;

            lock (_serverLocker)
            {
                classesList = QuikLua.Class.GetClassesList().Result;
            }

            List<SecurityInfo> allSec = new List<SecurityInfo>();

            for (int i = 0; i < classesList.Length; i++)
            {
                if (classesList[i].EndsWith("INFO"))
                {
                    continue;
                }

                string[] secCodes = QuikLua.Class.GetClassSecurities(classesList[i]).Result;
                for (int j = 0; j < secCodes.Length; j++)
                {
                    allSec.Add(QuikLua.Class.GetSecurityInfo(classesList[i], secCodes[j]).Result);
                }
            }

            List<Security> securities = new List<Security>();
            foreach (var oneSec in allSec)
            {
                BuildSecurity(oneSec, securities);
            }

            if (securities.Count > 0)
            {
                SaveToCache(securities);
            }

            return securities;
        }

        private void BuildSecurity(SecurityInfo oneSec, List<Security> securities)
        {
            try
            {
                if (oneSec == null)
                {
                    return;
                }

                Security newSec = new Security();
                string secCode = oneSec.SecCode;
                string classCode = oneSec.ClassCode;
                if (oneSec.ClassCode == "SPBFUT")
                {
                    newSec.SecurityType = SecurityType.Futures;
                    var exp = oneSec.MatDate;
                    newSec.Expiration = new DateTime(Convert.ToInt32(exp.Substring(0, 4))
                        , Convert.ToInt32(exp.Substring(4, 2))
                        , Convert.ToInt32(exp.Substring(6, 2)));

                    newSec.Go = Convert.ToDecimal(QuikLua.Trading
                        .GetParamEx(classCode, secCode, "SELLDEPO")
                        .Result.ParamValue.Replace('.', Separator));
                }
                else if (oneSec.ClassCode == "SPBOPT")
                {
                    newSec.SecurityType = SecurityType.Option;

                    newSec.OptionType = QuikLua.Trading.GetParamEx(classCode, secCode, "OPTIONTYPE")
                        .Result.ParamImage == "Put"
                        ? OptionType.Put
                        : OptionType.Call;

                    var exp = oneSec.MatDate;
                    newSec.Expiration = new DateTime(Convert.ToInt32(exp.Substring(0, 4))
                        , Convert.ToInt32(exp.Substring(4, 2))
                        , Convert.ToInt32(exp.Substring(6, 2)));

                    newSec.Go = Convert.ToDecimal(QuikLua.Trading
                        .GetParamEx(classCode, secCode, "SELLDEPO")
                        .Result.ParamValue.Replace('.', Separator));

                    newSec.Strike = Convert.ToDecimal(QuikLua.Trading
                        .GetParamEx(classCode, secCode, "STRIKE")
                        .Result.ParamValue.Replace('.', Separator));
                }
                else
                {
                    newSec.SecurityType = SecurityType.Stock;
                }

                newSec.Name = oneSec.SecCode; // тест
                newSec.NameFull = oneSec.Name;
                newSec.NameId = oneSec.Name;

                newSec.Decimals = Convert.ToInt32(oneSec.Scale);
                newSec.Lot = Convert.ToDecimal(oneSec.LotSize);
                newSec.NameClass = oneSec.ClassCode;

                newSec.PriceLimitHigh = Convert.ToDecimal(QuikLua.Trading
                    .GetParamEx(classCode, secCode, "PRICEMAX")
                    .Result.ParamValue.Replace('.', Separator));

                newSec.PriceLimitLow = Convert.ToDecimal(QuikLua.Trading
                    .GetParamEx(classCode, secCode, "PRICEMIN")
                    .Result.ParamValue.Replace('.', Separator));

                newSec.PriceStep = Convert.ToDecimal(QuikLua.Trading
                    .GetParamEx(classCode, secCode, "SEC_PRICE_STEP")
                    .Result.ParamValue.Replace('.', Separator));

                newSec.PriceStepCost = Convert.ToDecimal(QuikLua.Trading
                    .GetParamEx(classCode, secCode, "STEPPRICET")
                    .Result.ParamValue.Replace('.', Separator));

                if (newSec.PriceStep == 0 &&
                    newSec.Decimals > 0)
                {
                    newSec.PriceStep = newSec.Decimals * 0.1m;
                }

                if (newSec.PriceStep == 0)
                {
                    newSec.PriceStep = 1;
                }

                if (newSec.PriceStepCost == 0)
                {
                    newSec.PriceStepCost = 1;
                }

                securities.Add(newSec);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
        
        private void SaveToCache(List<Security> list)
        {
            if (list == null)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(SecuritiesCachePath, false))
                {
                    string data = CompressionUtils.Compress(list.ToJson());
                    writer.WriteLine(data);
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private List<Security> LoadSecuritiesFromCache()
        {
            try
            {
                using (StreamReader reader = new StreamReader(SecuritiesCachePath))
                {
                    string data = CompressionUtils.Decompress(reader.ReadToEnd());
                    List<Security> list = JsonConvert.DeserializeObject<List<Security>>(data);
                    return list != null && list.Count != 0 ? list : LoadSecuritiesFromQuik();
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
                return LoadSecuritiesFromQuik();
            }
        }

        private List<Portfolio> _portfolios;

        public void GetPortfolios()
        {
        }

        private void GetPortfoliosArea()
        {
            try
            {
                while (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    Thread.Sleep(5000);
                }

                Char separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                List<TradesAccounts> accaunts = QuikLua.Class.GetTradeAccounts().Result;
                var clientCode = QuikLua.Class.GetClientCode().Result;

                while (true)
                {
                    Thread.Sleep(5000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (QuikLua == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < accaunts.Count; i++)
                    {
                        if (String.IsNullOrWhiteSpace(accaunts[i].ClassCodes))
                        {
                            continue;
                        }

                        Portfolio myPortfolio = _portfolios.Find(p => p.Number == accaunts[i].TrdaccId);

                        if (myPortfolio == null)
                        {
                            myPortfolio = new Portfolio();
                        }

                        myPortfolio.Number = accaunts[i].TrdaccId;

                        if (myPortfolio.Number.Contains("SPBFUT") == false)
                        {
                            var qPortfolio = QuikLua.Trading.GetPortfolioInfo(accaunts[i].Firmid, clientCode).Result;

                            if (qPortfolio != null && qPortfolio.InAssets != null)
                            {
                                var begin = qPortfolio.InAssets.Replace('.', separator);
                                myPortfolio.ValueBegin = Convert.ToDecimal(begin.Remove(begin.Length - 4));
                            }

                            if (qPortfolio != null && qPortfolio.Assets != null)
                            {
                                var current = qPortfolio.Assets.Replace('.', separator);
                                myPortfolio.ValueCurrent = Convert.ToDecimal(current.Remove(current.Length - 4));
                            }

                            if (qPortfolio != null && qPortfolio.TotalLockedMoney != null)
                            {
                                var blocked = qPortfolio.TotalLockedMoney.Replace('.', separator);
                                myPortfolio.ValueBlocked = Convert.ToDecimal(blocked.Remove(blocked.Length - 4));
                            }

                            if (qPortfolio != null && qPortfolio.ProfitLoss != null)
                            {
                                var profit = qPortfolio.ProfitLoss.Replace('.', separator);
                                myPortfolio.Profit = Convert.ToDecimal(profit.Remove(profit.Length - 4));
                            }
                        }
                        else
                        {
                            // TODO make information on futures limits for accounts without EBU
                            // сделать получение информации по фьючерсным лимитам для счетов без ЕБС
                        }

                        _portfolios.Add(myPortfolio);
                    }


                    if (PortfolioEvent != null)
                    {
                        PortfolioEvent(_portfolios);
                    }
                }
            }

            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSpotPosition()
        {
            while (true)
            {
                Thread.Sleep(5000);

                if (QuikLua != null)
                {
                    bool quikStateIsActiv = QuikLua.Service.IsConnected().Result;
                }

                if (QuikLua == null)
                {
                    continue;
                }


                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    continue;
                }

                List<DepoLimitEx> spotPos = QuikLua.Trading.GetDepoLimits().Result;
                Portfolio needPortf;
                foreach (var pos in spotPos)
                {
                    if (pos.LimitKind == LimitKind.T0)
                    {
                        needPortf = _portfolios.Find(p => p.Number == pos.TrdAccId);

                        PositionOnBoard position = new PositionOnBoard();

                        if (needPortf != null)
                        {
                            position.PortfolioName = pos.TrdAccId;
                            position.ValueBegin = pos.OpenBalance;
                            position.ValueCurrent = pos.CurrentBalance;
                            position.ValueBlocked = pos.LockedSell;
                            position.SecurityNameCode = pos.SecCode;

                            needPortf.SetNewPosition(position);
                        }
                    }
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
        }

        public void SendOrder(Order order)
        {
            QuikSharp.DataStructures.Transaction.Order qOrder = new QuikSharp.DataStructures.Transaction.Order();

            qOrder.SecCode = order.SecurityNameCode;
            qOrder.Account = order.PortfolioNumber;
            qOrder.ClassCode = _securities.Find(sec => sec.Name == order.SecurityNameCode).NameClass;
            qOrder.Quantity = Convert.ToInt32(order.Volume);
            qOrder.Operation = order.Side == Side.Buy ? Operation.Buy : Operation.Sell;
            qOrder.Price = order.Price;
            qOrder.Comment = order.NumberUser.ToString();

            lock (_serverLocker)
            {
                var res = QuikLua.Orders.CreateOrder(qOrder).Result;

                if (res > 0)
                {
                    order.NumberUser = Convert.ToInt32(res);

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }

                if (res < 0)
                {
                    order.State = OrderStateType.Fail;
                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }
            }
        }

        private List<Order> _ordersAllReadyCanseled = new List<Order>();

        public void CancelOrder(Order order)
        {
            _ordersAllReadyCanseled.Add(order);

            QuikSharp.DataStructures.Transaction.Order qOrder = new QuikSharp.DataStructures.Transaction.Order();

            qOrder.SecCode = order.SecurityNameCode;
            qOrder.Account = order.PortfolioNumber;
            qOrder.ClassCode = _securities.Find(sec => sec.Name == order.SecurityNameCode).NameClass;

            if (order.NumberMarket == "")
            {
                qOrder.OrderNum = 0;
            }
            else
            {
                qOrder.OrderNum = Convert.ToInt64(order.NumberMarket);
            }
            //qOrder.OrderNum = Convert.ToInt64(order.NumberMarket);

            lock (_serverLocker)
            {
                var res = QuikLua.Orders.KillOrder(qOrder).Result;
            }
        }

        private List<string> subscribedBook = new List<string>();

        public void Subscrible(Security security)
        {
            if (subscribedBook.Find(s => s == security.Name) != null)
            {
                return;
            }

            lock (_serverLocker)
            {
                QuikLua.OrderBook.Subscribe(security.NameClass, security.Name);
                subscribedBook.Add(security.Name);
                QuikLua.Events.OnAllTrade -= EventsOnOnAllTrade;
                QuikLua.Events.OnAllTrade += EventsOnOnAllTrade;
            }
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            return null;
        }

        public void GetOrdersState(List<Order> orders)
        {
        }

        private object _getCandlesLocker = new object();

        /// <summary>
        /// take candles by instrument
        /// взять свечи по инструменту
        /// </summary>
        /// <param name="security"> short security name/короткое название бумаги</param>
        /// <param name="timeSpan">timeframe/таймФрейм</param>
        /// <returns>failure will return null/в случае неудачи вернётся null</returns>
        public List<Candle> GetQuikLuaCandleHistory(string security, TimeSpan timeSpan)
        {
            try
            {
                lock (_getCandlesLocker)
                {
                    if (timeSpan.TotalMinutes > 60 ||
                        timeSpan.TotalMinutes < 1)
                    {
                        return null;
                    }

                    CandleInterval tf = CandleInterval.M5;

                    if (Convert.ToInt32(timeSpan.TotalMinutes) == 1)
                    {
                        tf = CandleInterval.M1;
                    }
                    else if (Convert.ToInt32(timeSpan.TotalMinutes) == 2)
                    {
                        tf = CandleInterval.M2;
                    }
                    else if (Convert.ToInt32(timeSpan.TotalMinutes) == 5)
                    {
                        tf = CandleInterval.M5;
                    }
                    else if (Convert.ToInt32(timeSpan.TotalMinutes) == 10)
                    {
                        tf = CandleInterval.M10;
                    }
                    else if (Convert.ToInt32(timeSpan.TotalMinutes) == 15)
                    {
                        tf = CandleInterval.M15;
                    }
                    else if (Convert.ToInt32(timeSpan.TotalMinutes) == 30)
                    {
                        tf = CandleInterval.M30;
                    }
                    else if (Convert.ToInt32(timeSpan.TotalMinutes) == 60)
                    {
                        tf = CandleInterval.H1;
                    }
                    else if (Convert.ToInt32(timeSpan.TotalMinutes) == 120)
                    {
                        tf = CandleInterval.H2;
                    }


                    #region MyRegion

                    _candles = null;

                    var needSec = _securities.Find(sec => sec.Name == security);

                    if (needSec != null)
                    {
                        _candles = new List<Candle>();
                        string classCode = needSec.NameClass;

                        var allCandlesForSec = QuikLua.Candles.GetAllCandles(classCode, needSec.Name, tf).Result;

                        for (int i = 0; i < allCandlesForSec.Count; i++)
                        {
                            if (allCandlesForSec[i] != null)
                            {
                                Candle newCandle = new Candle();

                                newCandle.Close = allCandlesForSec[i].Close;
                                newCandle.High = allCandlesForSec[i].High;
                                newCandle.Low = allCandlesForSec[i].Low;
                                newCandle.Open = allCandlesForSec[i].Open;
                                newCandle.Volume = allCandlesForSec[i].Volume;

                                if (i == allCandlesForSec.Count - 1)
                                {
                                    newCandle.State = CandleState.None;
                                }
                                else
                                {
                                    newCandle.State = CandleState.Finished;
                                }

                                newCandle.TimeStart = new DateTime(allCandlesForSec[i].Datetime.year,
                                    allCandlesForSec[i].Datetime.month,
                                    allCandlesForSec[i].Datetime.day,
                                    allCandlesForSec[i].Datetime.hour,
                                    allCandlesForSec[i].Datetime.min,
                                    allCandlesForSec[i].Datetime.sec);

                                _candles.Add(newCandle);
                            }
                        }
                    }

                    #endregion

                    return _candles;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// candles downloadin with using method GetQuikLuaCandleHistory
        /// свечи скаченные из метода GetQuikLuaCandleHistory
        /// </summary>
        private List<Candle> _candles;

        // parsing incoming data
        // разбор входящих данных

        private object _newTradesLoker = new object();

        private void EventsOnOnAllTrade(AllTrade allTrade)
        {
            try
            {
                if (allTrade == null)
                {
                    return;
                }

                lock (_newTradesLoker)
                {
                    Trade trade = new Trade();
                    trade.SecurityNameCode = allTrade.SecCode;
                    trade.Id = allTrade.TradeNum.ToString();
                    trade.Price = Convert.ToDecimal(allTrade.Price);
                    trade.Volume = Convert.ToInt32(allTrade.Qty);

                    var side = Convert.ToInt32(allTrade.Flags);

                    if (side == 1025 || side == 1)
                    {
                        trade.Side = Side.Sell;
                    }
                    else //if(side == 1026 || side == 2)
                    {
                        trade.Side = Side.Buy;
                    }

                    trade.Time = new DateTime(allTrade.Datetime.year, allTrade.Datetime.month, allTrade.Datetime.day,
                        allTrade.Datetime.hour, allTrade.Datetime.min, allTrade.Datetime.sec);
                    if (NewTradesEvent != null)
                    {
                        NewTradesEvent(trade);
                    }

                    // write last tick time in server time / перегружаем последним временем тика время сервера
                    ServerTime = trade.Time;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private object changeFutPortf = new object();

        private void EventsOnOnFuturesLimitChange(FuturesLimits futLimit)
        {
            lock (changeFutPortf)
            {
                Portfolio needPortf = _portfolios.Find(p => p.Number == futLimit.TrdAccId);

                if (needPortf != null)
                {
                    needPortf.ValueBegin = Convert.ToDecimal(futLimit.CbpPrevLimit);
                    needPortf.ValueCurrent = Convert.ToDecimal(futLimit.CbpLimit);
                    needPortf.ValueBlocked =
                        Convert.ToDecimal(futLimit.CbpLUsedForOrders + futLimit.CbpLUsedForPositions);
                    needPortf.Profit = Convert.ToDecimal(futLimit.VarMargin);

                    if (PortfolioEvent != null)
                    {
                        PortfolioEvent(_portfolios);
                    }
                }
            }
        }

        private object changeFutPosLocker = new object();

        private void EventsOnOnFuturesClientHolding(FuturesClientHolding futPos)
        {
            lock (changeFutPosLocker)
            {
                if (_portfolios != null)
                {
                    Portfolio needPortfolio = _portfolios.Find(p => p.Number == futPos.trdAccId);

                    PositionOnBoard newPos = new PositionOnBoard();

                    newPos.PortfolioName = futPos.trdAccId;
                    newPos.SecurityNameCode = futPos.secCode;
                    newPos.ValueBegin = Convert.ToDecimal(futPos.startNet);
                    newPos.ValueCurrent = Convert.ToDecimal(futPos.totalNet);
                    newPos.ValueBlocked = 0;

                    needPortfolio.SetNewPosition(newPos);

                    if (PortfolioEvent != null)
                    {
                        PortfolioEvent(_portfolios);
                    }
                }
            }
        }

        private object quoteLock = new object();

        private void EventsOnOnQuote(OrderBook orderBook)
        {
            lock (quoteLock)
            {
                if (subscribedBook.Find(name => name == orderBook.sec_code) == null)
                {
                    return;
                }

                if (orderBook.bid == null || orderBook.offer == null)
                {
                    return;
                }

                MarketDepth myDepth = new MarketDepth();

                myDepth.SecurityNameCode = orderBook.sec_code;
                myDepth.Time = DateTime.Now;

                myDepth.Bids = new List<MarketDepthLevel>();
                for (int i = 0; i < orderBook.bid.Length; i++)
                {
                    myDepth.Bids.Add(new MarketDepthLevel()
                    {
                        Bid = Convert.ToDecimal(orderBook.bid[i].quantity),
                        Price = Convert.ToDecimal(orderBook.bid[i].price),
                        Ask = 0
                    });
                }

                myDepth.Bids.Reverse();

                myDepth.Asks = new List<MarketDepthLevel>();
                for (int i = 0; i < orderBook.offer.Length; i++)
                {
                    myDepth.Asks.Add(new MarketDepthLevel()
                    {
                        Ask = Convert.ToDecimal(orderBook.offer[i].quantity),
                        Price = Convert.ToDecimal(orderBook.offer[i].price),
                        Bid = 0
                    });
                }

                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(myDepth);
                }
            }
        }

        private object orderLocker = new object();

        private void EventsOnOnOrder(QuikSharp.DataStructures.Transaction.Order qOrder)
        {
            lock (orderLocker)
            {
                try
                {
                    if (qOrder.TransID == 0)
                    {
                        return;
                    }

                    Order order = new Order();
                    order.NumberUser = Convert.ToInt32(qOrder.TransID); //Convert.qOrder.OrderNum;TransID
                    order.NumberMarket = qOrder.OrderNum.ToString(new CultureInfo("ru-RU"));
                    order.TimeCallBack = ServerTime;
                    order.SecurityNameCode = qOrder.SecCode;
                    order.Price = qOrder.Price;
                    order.Volume = qOrder.Quantity;
                    order.VolumeExecute = qOrder.Quantity - qOrder.Balance;
                    order.PortfolioNumber = qOrder.Account;
                    order.TypeOrder = qOrder.Flags.ToString().Contains("IsLimit")
                        ? OrderPriceType.Limit
                        : OrderPriceType.Market;
                    order.ServerType = ServerType.QuikLua;

                    if (qOrder.State == State.Active)
                    {
                        order.State = OrderStateType.Activ;
                        order.TimeCallBack = new DateTime(qOrder.Datetime.year, qOrder.Datetime.month,
                            qOrder.Datetime.day,
                            qOrder.Datetime.hour, qOrder.Datetime.min, qOrder.Datetime.sec);
                    }
                    else if (qOrder.State == State.Completed)
                    {
                        order.State = OrderStateType.Done;
                        order.VolumeExecute = qOrder.Quantity;
                        order.TimeDone = order.TimeCallBack;
                    }
                    else if (qOrder.State == State.Canceled)
                    {
                        order.TimeCancel = new DateTime(qOrder.WithdrawDatetime.year, qOrder.WithdrawDatetime.month,
                            qOrder.WithdrawDatetime.day,
                            qOrder.WithdrawDatetime.hour, qOrder.WithdrawDatetime.min, qOrder.WithdrawDatetime.sec);
                        order.State = OrderStateType.Cancel;
                        order.VolumeExecute = 0;
                    }
                    else if (qOrder.Balance != 0)
                    {
                        order.State = OrderStateType.Patrial;
                        order.VolumeExecute = qOrder.Quantity - qOrder.Balance;
                    }

                    if (_ordersAllReadyCanseled.Find(o => o.NumberUser == qOrder.TransID) != null)
                    {
                        order.State = OrderStateType.Cancel;
                        order.TimeCancel = order.TimeCallBack;
                    }

                    if (qOrder.Operation == Operation.Buy)
                    {
                        order.Side = Side.Buy;
                    }
                    else
                    {
                        order.Side = Side.Sell;
                    }

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private object myTradeLocker = new object();

        private void EventsOnOnTrade(QuikSharp.DataStructures.Transaction.Trade qTrade)
        {
            lock (myTradeLocker)
            {
                try
                {
                    MyTrade trade = new MyTrade();
                    trade.NumberTrade = qTrade.TradeNum.ToString();
                    trade.SecurityNameCode = qTrade.SecCode;
                    trade.NumberOrderParent = qTrade.OrderNum.ToString();
                    trade.Price = Convert.ToDecimal(qTrade.Price);
                    trade.Volume = qTrade.Quantity;
                    trade.Time = new DateTime(qTrade.QuikDateTime.year, qTrade.QuikDateTime.month,
                        qTrade.QuikDateTime.day, qTrade.QuikDateTime.hour,
                        qTrade.QuikDateTime.min, qTrade.QuikDateTime.sec);
                    trade.Side = qTrade.Flags == OrderTradeFlags.IsSell ? Side.Sell : Side.Buy;

                    if (MyTradeEvent != null)
                    {
                        MyTradeEvent(trade);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void EventsOnOnDisconnectedFromQuik()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            DisconnectEvent?.Invoke();
        }

        private void EventsOnOnConnectedToQuik(int port)
        {
            ServerStatus = ServerConnectStatus.Connect;
            ConnectEvent?.Invoke();
        }

        private void EventsOnOnDisconnected()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            DisconnectEvent?.Invoke();
        }

        private void EventsOnOnConnected()
        {
            ServerStatus = ServerConnectStatus.Connect;

            ConnectEvent?.Invoke();
        }

        // outgoing events
        // исходящие события

        /// <summary>
        /// called when order changed
        /// вызывается когда изменился ордер
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// called when my trade changed
        /// вызывается когда изменился мой трейд
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// appeared new portfolios
        /// появились новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
        /// new securities
        /// новые бумаги
        /// </summary>
        public event Action<List<Security>> SecurityEvent;

        /// <summary>
        /// new depth
        /// новый стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
        /// new trade
        /// новый трейд
        /// </summary>
        public event Action<Trade> NewTradesEvent;

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action ConnectEvent;

        /// <summary>
        /// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action DisconnectEvent;

        // log messages
        // сообщения для лога

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}