using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Bitfinex.BitfitnexEntity;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Bitfinex
{

    /// <summary>
    /// Bitfinex server
    /// сервер Bitfinex
    /// </summary>
    public class BitfinexServer:AServer
    {
        public BitfinexServer()
        {
            IServerRealization realization = new BitfinexServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterBoolean(OsLocalization.Market.ServerParam4, false);
        }
        
        /// <summary>
        /// take candles by instrument
        /// взять свечи по инструменту
        /// </summary>
        /// <param name="securityName"></param>
        /// <param name="seriesTimeFrameSpan"></param>
        /// <returns></returns>
        public List<Candle> GetCandleHistory(string securityName, TimeSpan seriesTimeFrameSpan)
        {
           return ((BitfinexServerRealization)ServerRealization).GetCandleHistory(securityName, seriesTimeFrameSpan, 500, DateTime.MinValue, DateTime.MinValue);
        }
    }

    /// <summary>
    /// implementation of Bitfinex server
    /// реализация сервера битфайнекс
    /// </summary>
    public class BitfinexServerRealization : IServerRealization
    {
        public BitfinexServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread worker = new Thread(ThreadCheckConnectionArea);
            worker.IsBackground = true;
            worker.Name = "BitfinexServerRealization_StatusCheck";
            worker.Start();
        }

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.Bitfinex; }
        }

        /// <summary>
        /// server status
        /// статус сервера
        /// </summary>
        public ServerConnectStatus ServerStatus { get; set; }

        /// <summary>
        /// parameters
        /// параметры
        /// </summary>
        public List<IServerParameter> ServerParameters { get; set; }

        /// <summary>
        /// server time
        /// время сервера
        /// </summary>
        public DateTime ServerTime { get; set; }

        /// <summary>
        /// thread checking that data reception did not break
        /// поток проверяющий чтобы приём данных не сломался
        /// </summary>
        private void ThreadCheckConnectionArea()
        {
            DateTime lastServerTimeChange = DateTime.Now;

            DateTime lastServerTime = DateTime.Now;

            while (true)
            {
                Thread.Sleep(3000);

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                if (ServerTime == DateTime.MinValue)
                {
                    continue;
                }

                if (lastServerTime != ServerTime)
                {
                    lastServerTime = ServerTime;
                    lastServerTimeChange = DateTime.Now;
                }

                if (lastServerTimeChange.AddMinutes(5) < DateTime.Now)
                {
                    Dispose();

                    if (DisconnectEvent != null)
                    {
                        DisconnectEvent();
                    }
                    lastServerTimeChange = DateTime.Now;
                    Thread.Sleep(60000);
                }

            }
        }

        // requests
        // запросы

        /// <summary>
        /// client
        /// </summary>
        private BitfinexClient _client;

        /// <summary>
        /// last starting servet time
        /// время последнего запуска сервера
        /// </summary>
        private DateTime _lastStartServerTime;

        /// <summary>
        /// dispose API
        /// освободить апи
        /// </summary>
        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();

                _client.Connected -= ClientOnConnected;
                _client.Disconnected -= ClientOnDisconnected;
                _client.NewPortfolio -= ClientOnNewPortfolio;
                _client.UpdatePortfolio -= ClientOnUpdatePortfolio;
                _client.UpdateMarketDepth -= ClientOnUpdateMarketDepth;
                _client.NewMarketDepth -= ClientOnNewMarketDepth;
                _client.NewTradesEvent -= ClientOnNewTradesEvent;
                _client.MyTradeEvent -= ClientOnMyTradeEvent;
                _client.MyOrderEvent -= ClientOnMyOrderEvent;
                _client.LogMessageEvent -= ClientOnLogMessageEvent;
            }

            _client = null;
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// connecto to API
        /// подсоединиться к апи
        /// </summary>
        public void Connect()
        {
            if (_client == null)
            {
                _client = new BitfinexClient();
                _client.Connected += ClientOnConnected;
                _client.Disconnected += ClientOnDisconnected;
                _client.NewPortfolio += ClientOnNewPortfolio;
                _client.UpdatePortfolio += ClientOnUpdatePortfolio;
                _client.UpdateMarketDepth += ClientOnUpdateMarketDepth;
                _client.NewMarketDepth += ClientOnNewMarketDepth;
                _client.NewTradesEvent += ClientOnNewTradesEvent;
                _client.MyTradeEvent += ClientOnMyTradeEvent;
                _client.MyOrderEvent += ClientOnMyOrderEvent;
                _client.LogMessageEvent += ClientOnLogMessageEvent;
            }

            _lastStartServerTime = DateTime.Now;

            _client.Connect(((ServerParameterString)ServerParameters[0]).Value, ((ServerParameterPassword)ServerParameters[1]).Value);
        }

        /// <summary>
        /// request securities
        /// запросить бумаги
        /// </summary>
        public void GetSecurities()
        {
            try
            {
                var securities = _client.GetSecurities();

                List<Security> securitiisOsa = new List<Security>();

                for(int i = 0;i < securities.Count;i++)
                {
                    var sec = securities[i];
                    Security security = new Security();

                    security.Name = sec.pair.ToUpper();
                    security.NameFull = sec.pair;
                    security.NameId = sec.pair;

                    if (sec.pair.Contains(":") == false)
                    {
                        security.NameClass = sec.pair.Substring(3);
                        security.SecurityType = SecurityType.CurrencyPair;
                    }
                    else
                    {
                        security.NameClass = sec.pair.Split(':')[0];
                        security.SecurityType = SecurityType.Futures;
                    }

                    /*
                     sec.price_precision - для любого инструмента битфайнекс выдаёт 5(пять) что не соответствует действительности
                     security.Decimals = sec.price_precision;
                     security.PriceStep = sec.price_precision;

                     security.PriceStepCost = sec.price_precision;

                     if (sec.price_precision != 5)
                     {

                     }*/

                    security.Lot = 1m;

                    security.State = SecurityStateType.Activ;

                    securitiisOsa.Add(security);
                }

                if (SecurityEvent != null)
                {
                    SecurityEvent(securitiisOsa);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// find price accuracy for security
        /// найти точность цены для бумаги
        /// </summary>
        private void SetPriceStepInSecurity(Security security)
        {
           BitfinexTickerTradeInfo info = _client.GetTradeInfo(security.Name);

            // find the longest string / находим самую длинную строку 

            string maxLengthStr = info.ask;

            if (info.bid.Length > maxLengthStr.Length)
            {
                maxLengthStr = info.bid;
            }

            if (info.high.Length > maxLengthStr.Length)
            {
                maxLengthStr = info.high;
            }

            if (info.low.Length > maxLengthStr.Length)
            {
                maxLengthStr = info.low;
            }

            maxLengthStr = maxLengthStr.Replace('.', ',');

            decimal step = 1;
            int decimals = 0;

            if (maxLengthStr.Split(',').Length > 1)
            {
                decimals = maxLengthStr.Split(',')[1].Length;

                for (int i = 0; i < decimals; i++)
                {
                    step *= 0.1m;
                }
            }

            security.Decimals = decimals;
            security.PriceStep = step;
            security.PriceStepCost = step;
            Thread.Sleep(3300);
        }

        /// <summary>
        /// request portfolios
        /// запросить портфели
        /// </summary>
        public void GetPortfolios()
        {
            _client.SubscribeUserData();
        }

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        public void SendOrder(Order order)
        {
            _client.ExecuteOrder(order, ((ServerParameterBool)ServerParameters[2]).Value);
        }

        /// <summary>
        /// cancel order
        /// отозвать ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            _client.CancelOrder(order);
        }

        /// <summary>
        /// subscribe
        /// подписаться 
        /// </summary>
        public void Subscrible(Security security)
        {
            SetPriceStepInSecurity(security);
            _client.SubscribleTradesAndDepths(security);
        }

        /// <summary>
        /// request orders state
        /// запросить статус ордеров
        /// </summary>
        public void GetOrdersState(List<Order> orders)
        {
            
        }

// parsing incoming data
// разбор входящих данных

        /// <summary>
        /// receive new message from client
        /// из клиента поступило новое сообщение
        /// </summary>
        private void ClientOnLogMessageEvent(string message, LogMessageType logType)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, logType);
            }
        }

        /// <summary>
        /// receive new order from client
        /// из клиента пришёл ордер
        /// </summary>
        private void ClientOnMyOrderEvent(Order myOrder)
        {
            myOrder.ServerType = ServerType.Bitfinex;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(myOrder);
            }
        }

        /// <summary>
        /// receive new trade from client
        /// из клиента пришёл мой трейд
        /// </summary>
        private void ClientOnMyTradeEvent(MyTrade trade)
        {
            if (MyTradeEvent != null)
            {
                MyTradeEvent(trade);
            }
        }

        /// <summary>
        /// receive trades from client
        /// из клиента пришли трейды
        /// </summary>
        private void ClientOnNewTradesEvent(List<ChangedElement> trades, string secName)
        {
            try
            {
                if (trades == null || trades.Count == 0)
                {
                    return;
                }

                Trade trade = new Trade();
                trade.SecurityNameCode = secName;
                trade.Price = Convert.ToDecimal(trades[5].Double);
                trade.Id = trades[3].Double.ToString();
                trade.Time = TakeDateFromTicks(trades[4].Double);

                var amount = Convert.ToDecimal(trades[6].Double);

                trade.Volume = Convert.ToDecimal(Math.Abs(amount));
                trade.Side = amount > 0 ? Side.Buy : Side.Sell;

                // write the last tick time in server time / перегружаем последним временем тика время сервера
                ServerTime = trade.Time;

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(trade);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// depths
        /// стаканы
        /// </summary>
        private List<MarketDepth> _depths;

        /// <summary>
        /// multi-threaded access locker to depths
        /// блокиратор многопоточного доступа к стаканам
        /// </summary>
        private readonly object _depthLocker = new object();

        /// <summary>
        /// receive new depths from client
        /// из клиента пришли новые стаканы
        /// </summary>
        private void ClientOnNewMarketDepth(List<DataObject> newOrderBook, string nameSecurity)
        {
            try
            {
                lock (_depthLocker)
                {
                    if (_depths == null)
                    {
                        _depths = new List<MarketDepth>();
                    }

                    var needDepth = _depths.Find(depth =>
                        depth.SecurityNameCode == nameSecurity);

                    if (needDepth == null)
                    {
                        needDepth = new MarketDepth();
                        needDepth.SecurityNameCode = nameSecurity;
                        _depths.Add(needDepth);
                    }

                    List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                    List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                    foreach (var value in newOrderBook[1].Values)
                    {
                        // value[2] - volume in the level, if > 0, then bid, else ask / value[2] - объем на уровне, если > 0 значит бид, иначе аск
                        if (value[2] > 0)
                        {
                            bids.Add(new MarketDepthLevel()
                            {
                                Bid = Convert.ToDecimal(value[2]),
                                Price = Convert.ToDecimal(value[0]),
                            });
                        }
                        else
                        {
                            ascs.Add(new MarketDepthLevel()
                            {
                                Ask = Convert.ToDecimal(Math.Abs(value[2])),
                                Price = Convert.ToDecimal(value[0]),
                            });
                        }
                    }

                    needDepth.Asks = ascs;
                    needDepth.Bids = bids;
                    needDepth.Time = ServerTime;

                    if (MarketDepthEvent != null)
                    {
                        MarketDepthEvent(needDepth.GetCopy());
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// receive depth updates from client
        /// из клиента пришли обновления по стаканам
        /// </summary>
        private void ClientOnUpdateMarketDepth(List<ChangedElement> newData, string nameSecurity)
        {
            try
            {
                lock (_depthLocker)
                {
                    if (_depths == null)
                    {
                        return;
                    }
                    var needDepth = _depths.Find(depth =>
                        depth.SecurityNameCode == nameSecurity);

                    if (needDepth == null)
                    {
                        return;
                    }

                    if (newData.Count < 4)
                    {
                        return;
                    }

                    var price = Convert.ToDecimal(newData[1].Double);

                    var count = Convert.ToDecimal(newData[2].Double);

                    var amount = Convert.ToDecimal(newData[3].Double);

                    needDepth.Time = ServerTime;

                    // if the number of orders is 0, then you need to find the level of this price and delete it / если колл-во ореров равно 0, значит надо найти уровень этой цены и удалить его
                    if (count == 0)
                    {
                        // delete level / удаляем уровень
                        if (amount < 0)  // delete from asks / удаляем из асков
                        {
                            needDepth.Asks.Remove(needDepth.Asks.Find(level => level.Price == price));
                        }

                        if (amount > 0)  // delete from bids / удаляем из бидов
                        {
                            needDepth.Bids.Remove(needDepth.Bids.Find(level => level.Price == price));
                        }
                        return;
                    }

                    // if the volume is greater than zero, then some bid has changed, we find it and update / если объем больше нуля, значит изменился какой-то бид, находим его и обновляем
                    else if (amount > 0)
                    {
                        var needLevel = needDepth.Bids.Find(bid => bid.Price == price);

                        if (needLevel == null)  // if there is no such level, add it / если такого уровня нет, добавляем его
                        {
                            needDepth.Bids.Add(new MarketDepthLevel()
                            {
                                Bid = amount,
                                Price = price
                            });

                            needDepth.Bids.Sort((level, depthLevel) => level.Price > depthLevel.Price ? -1 : level.Price < depthLevel.Price ? 1 : 0);
                        }
                        else
                        {
                            needLevel.Bid = amount;
                        }

                    }
                    // if less, then update the ask / если меньше, значит обновляем аск
                    else if (amount < 0)
                    {
                        var needLevel = needDepth.Asks.Find(asc => asc.Price == price);

                        if (needLevel == null)  // if there is no such level, add it / если такого уровня нет, добавляем его
                        {
                            needDepth.Asks.Add(new MarketDepthLevel()
                            {
                                Ask = Math.Abs(amount),
                                Price = price
                            });

                            needDepth.Asks.Sort((level, depthLevel) => level.Price > depthLevel.Price ? 1 : level.Price < depthLevel.Price ? -1 : 0);
                        }
                        else
                        {
                            needLevel.Ask = Math.Abs(amount);
                        }

                    }

                    if (needDepth.Asks.Count < 2 ||
                        needDepth.Bids.Count < 2)
                    {
                        return;
                    }

                    if (needDepth.Asks[0].Price > needDepth.Asks[1].Price)
                    {
                        needDepth.Asks.RemoveAt(0);
                    }
                    if (needDepth.Bids[0].Price < needDepth.Bids[1].Price)
                    {
                        needDepth.Asks.RemoveAt(0);
                    }

                    if (needDepth.Asks[0].Price < needDepth.Bids[0].Price)
                    {
                        if (needDepth.Asks[0].Price < needDepth.Bids[1].Price)
                        {
                            needDepth.Asks.Remove(needDepth.Asks[0]);
                        }
                        else if (needDepth.Bids[0].Price > needDepth.Asks[1].Price)
                        {
                            needDepth.Bids.Remove(needDepth.Bids[0]);
                        }
                    }

                    if (MarketDepthEvent != null)
                    {
                        MarketDepthEvent(needDepth.GetCopy());
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// portfolios
        /// портфели
        /// </summary>
        private List<Portfolio> _portfolios;

        /// <summary>
        /// receive new portfolios from client
        /// из клиента пришли новые портфели
        /// </summary>
        private void ClientOnNewPortfolio(List<List<WaletWalet>> portfolios)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                for (int i = 0; i < portfolios.Count; i++)
                {
                    List<WaletWalet> portfolio = portfolios[i];

                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = portfolio[1].String;

                    if (_portfolios.Find(p => p.Number == newPortf.Number) != null)
                    {
                        newPortf = _portfolios.Find(p => p.Number == newPortf.Number);
                    }

                    newPortf.ValueCurrent = Convert.ToDecimal(portfolio[2].Double);
                    newPortf.ValueBlocked = Convert.ToDecimal(portfolio[3].Double);
                    newPortf.ValueBegin = Convert.ToDecimal(portfolio[2].Double);

                    if (_portfolios.Find(p => p.Number == newPortf.Number) == null)
                    {
                        _portfolios.Add(newPortf);
                    }
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// receive updated portfolios from client
        /// из клиента пришли обновления портфелей
        /// </summary>
        private void ClientOnUpdatePortfolio(List<WalletUpdateWalletUpdate> walletUpdateWalletUpdates)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                var needPortfolio = _portfolios.Find(p => p.Number == walletUpdateWalletUpdates[1].String);

                if (needPortfolio == null)
                {
                    needPortfolio = new Portfolio();

                    needPortfolio.Number = walletUpdateWalletUpdates[1].String;
                    needPortfolio.ValueCurrent = Convert.ToDecimal(walletUpdateWalletUpdates[2].Double);
                    needPortfolio.ValueBlocked = Convert.ToDecimal(walletUpdateWalletUpdates[3].Double);

                    _portfolios.Add(needPortfolio);
                }
                else
                {
                    needPortfolio.Number = walletUpdateWalletUpdates[1].String;
                    needPortfolio.ValueCurrent = Convert.ToDecimal(walletUpdateWalletUpdates[2].Double);
                    needPortfolio.ValueBlocked = Convert.ToDecimal(walletUpdateWalletUpdates[3].Double);
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// client lost connection
        /// клиент потерял соединение
        /// </summary>
        private void ClientOnDisconnected()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
        }

        /// <summary>
        /// client connected to exchange
        /// клиент подключился к бирже
        /// </summary>
        private void ClientOnConnected()
        {
            ServerStatus = ServerConnectStatus.Connect;
            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
        }

        /// <summary>
        /// convert timestamp in datetime
        /// преобразует timestamp в datetime
        /// </summary>
        /// <param name="timeStamp"></param>
        /// <returns></returns>
        private DateTime TakeDateFromTicks(double? timeStamp)
        {
            DateTime yearBegin = new DateTime(1970, 1, 1);

            if (timeStamp.HasValue)
            {
                var time = yearBegin + TimeSpan.FromSeconds((double)timeStamp);

                return time;
            }

            return DateTime.MinValue;
        }

        private readonly object _candlesLocker = new object();

        /// <summary>
        /// take ticks data on instrument for period
        /// взять тиковые данные по инструменту за период
        /// </summary>
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            List<Trade> trades = new List<Trade>();

            List<Trade> lastTrades =
                GetBitFinexTrades(security.Name, 5000, DateTime.MaxValue);

            trades.AddRange(lastTrades);

            DateTime curEnd = trades[0].Time;

            DateTime cu = trades[trades.Count-1].Time;

            while (curEnd <= startTime == false)
            {
                List<Trade> newTrades =
                    GetBitFinexTrades(security.Name, 2000, curEnd);

                if (trades.Count != 0 && newTrades.Count != 0)
                {
                    if (newTrades[newTrades.Count - 1].Time >= trades[0].Time)
                    {
                        newTrades.RemoveAt(newTrades.Count - 1);
                    }
                }

                if (newTrades.Count == 0)
                {
                    return trades;
                }

                trades.InsertRange(0, newTrades);

                curEnd = trades[0].Time;
            }

            if (trades.Count == 0)
            {
                return null;
            }

            return trades;
        }

        private List<Trade> GetBitFinexTrades(string security, int count, DateTime end)
        {
            try
            {
                Thread.Sleep(8000);
                Dictionary<string, string> param = new Dictionary<string, string>();

                if (end == DateTime.MaxValue)
                {
                    param.Add("","t" + security + "/hist" + "?"
                                  + "limit=" + count);
                }
                else
                {
                    param.Add("", "t" + security + "/hist" + "?"
                                  + "limit=" + count
                                  + "&end=" + (end - new DateTime(1970, 1, 1)).TotalMilliseconds);
                }

                var response = _client.GetTrades(param);

                var tradesHist = Candles.FromJson(response);

                List<Trade> trades = new List<Trade>();

                for (int i = 0; i < tradesHist.Count; i++)
                {
                    DateTime time = new DateTime(1970, 1, 1) + TimeSpan.FromMilliseconds(tradesHist[i][1]);

                    Trade newTrade = new Trade();
                    newTrade.Time = time;
                    newTrade.SecurityNameCode = security;
                    double vol = tradesHist[i][2];
                    newTrade.Volume= Math.Abs(Convert.ToDecimal(vol));
                    newTrade.Price = Convert.ToDecimal(tradesHist[i][3]);
                    if (vol > 0)
                    {
                        newTrade.Side = Side.Buy;
                    }
                    else
                    {
                        newTrade.Side = Side.Sell;
                    }

                    trades.Insert(0,newTrade);
                }

                return trades;

            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// take candle history for period
        /// взять историю свечек за период
        /// </summary>
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            List<Candle> candles = new List<Candle>();

            List<Candle> lastCandles =
                GetCandleHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan, 5000, DateTime.MinValue, DateTime.MaxValue);

            candles.AddRange(lastCandles);

            DateTime curEnd = candles[0].TimeStart;

            while (curEnd <= startTime == false)
            {
                List<Candle> newCandles =
                    GetCandleHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan, 2000, startTime, curEnd);

                if (candles.Count != 0 && newCandles.Count != 0)
                {
                    if (newCandles[newCandles.Count - 1].TimeStart >= candles[0].TimeStart)
                    {
                        newCandles.RemoveAt(newCandles.Count - 1);
                    }
                }

                if (newCandles.Count == 0)
                {
                    return candles;
                }

                candles.InsertRange(0, newCandles);

                curEnd = candles[0].TimeStart;
            }

            if (candles.Count == 0)
            {
                return null;
            }

            return candles;
        }

        /// <summary>
        /// take candles on instruments
        /// взять свечи по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string securityName, TimeSpan seriesTimeFrameSpan, int count, DateTime start, DateTime end)
        {
            try
            {
                lock (_candlesLocker)
                {

                    List<Candle> newCandles = null;


                    int tf = Convert.ToInt32(seriesTimeFrameSpan.TotalMinutes);

                    if (tf == 1 || tf == 2 || tf == 3)
                    {
                        // building candles from 1 minute / строим свечи из минуток
                        var rawCandles = GetBitfinexCandles("1m", securityName, count, start, end);
                        newCandles = TransformCandles(1, tf, rawCandles);
                    }
                    else if (tf == 5 || tf == 10 || tf == 20)
                    {
                        // building candles from 5 minutes / строим свечи из 5минуток
                        var rawCandles = GetBitfinexCandles("5m", securityName, count, start, end);
                        newCandles = TransformCandles(5, tf, rawCandles);
                    }
                    else if (tf == 15 || tf == 30 || tf == 45)
                    {
                        // building candles from 15 minutes / строим свечи из 15минуток
                        var rawCandles = GetBitfinexCandles("15m", securityName, count, start, end);
                        newCandles = TransformCandles(15, tf, rawCandles);
                    }
                    else if (tf == 60 || tf == 120 || tf == 240)
                    {
                        // building candles from 1 hour / строим свечи из часовиков
                        var rawCandles = GetBitfinexCandles("1h", securityName, count, start, end);
                        newCandles = TransformCandles(60, tf, rawCandles);
                    }
                    else if (tf == 1440)
                    {
                        // building candles from 1 day / строим свечи из дневок
                        var rawCandles = GetBitfinexCandles("1D", securityName, count, start, end);

                        List<Candle> daily = new List<Candle>();

                        for (int i = rawCandles.Count - 1; i > 0; i--)
                        {
                            Candle candle = new Candle();
                            candle.TimeStart = new DateTime(1970, 1, 1) + TimeSpan.FromMilliseconds(rawCandles[i][0]);
                            candle.Open = Convert.ToDecimal(rawCandles[i][1]);
                            candle.Close = Convert.ToDecimal(rawCandles[i][2]);
                            candle.High = Convert.ToDecimal(rawCandles[i][3]);
                            candle.Low = Convert.ToDecimal(rawCandles[i][4]);
                            candle.Volume = Convert.ToDecimal(rawCandles[i][5]);

                            daily.Add(candle);
                        }

                        newCandles = daily;
                    }


                    return newCandles;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// take candles from exchange with needed timeframe
        /// получить с биржи свечи нужного ТФ
        /// </summary>
        /// <param name="tf"></param>
        /// <param name="security"></param>
        /// <returns></returns>
        private List<List<double>> GetBitfinexCandles(string tf, string security, int count, DateTime start, DateTime end)
        {
            try
            {
                Thread.Sleep(8000);
                Dictionary<string, string> param = new Dictionary<string, string>();

                if (start != DateTime.MinValue)
                {
                    param.Add("trade:" + tf, ":t" + security + "/hist" + "?"
                                             + "limit=" + count
                                             //+ "&start=" + (start - new DateTime(1970, 1, 1)).TotalMilliseconds);
                                             + "&end=" + (end - new DateTime(1970, 1, 1)).TotalMilliseconds);
                }
                else
                {
                    param.Add("trade:" + tf, ":t" + security + "/hist" + "?limit=" + count);
                }

                var candles = _client.GetCandles(param);
                var candleHist = Candles.FromJson(candles);

                return candleHist;
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// transform candles
        /// преобразовать свечи
        /// </summary>
        /// <param name="tf">received timeframe / пришедший таймфрейм</param>
        /// <param name="needTf">needed timeframe / тф который нужно получить</param>
        /// <param name="rawCandles">raw candles / заготовки для свечей</param>
        /// <returns></returns>
        private List<Candle> TransformCandles(int tf, int needTf, List<List<double>> rawCandles)
        {
            var entrance = needTf / tf;

            List<Candle> newCandles = new List<Candle>();

            //MTS,  - 0
            //OPEN, - 1 
            //CLOSE,- 2
            //HIGH, - 3
            //LOW,  - 4
            //VOLUME- 5

            int count = 0;

            Candle newCandle = new Candle();

            bool isStart = true;

            for (int i = rawCandles.Count - 1; i > 0; i--)
            {
                var time = new DateTime(1970, 1, 1) + TimeSpan.FromMilliseconds(rawCandles[i][0]);

                if (time.Minute % needTf != 0 && isStart)
                {
                    continue;
                }

                if (needTf > 60)
                {
                    if (time.Hour % (needTf / 60) != 0 && isStart)
                    {
                        continue;
                    }
                }

                isStart = false;

                count++;
                if (count == 1)
                {
                    newCandle = new Candle();
                    var resOpen = rawCandles[i];
                    newCandle.Open = Convert.ToDecimal(rawCandles[i][1]);
                    newCandle.TimeStart = time;
                    newCandle.Low = Decimal.MaxValue;
                    newCandle.High = Decimal.MinValue;
                }

                newCandle.High = Convert.ToDecimal(rawCandles[i][3]) > newCandle.High
                    ? Convert.ToDecimal(rawCandles[i][3])
                    : newCandle.High;

                newCandle.Low = Convert.ToDecimal(rawCandles[i][4]) < newCandle.Low
                    ? Convert.ToDecimal(rawCandles[i][4])
                    : newCandle.Low;

                newCandle.Volume += Convert.ToDecimal(rawCandles[i][5]);

                if (i == 1 && count != entrance)
                {
                    newCandle.Close = Convert.ToDecimal(rawCandles[i][2]);
                    newCandle.State = CandleState.None;
                    newCandles.Add(newCandle);
                }

                if (count == entrance)
                {
                    newCandle.Close = Convert.ToDecimal(rawCandles[i][2]);
                    newCandle.State = CandleState.Finished;
                    newCandles.Add(newCandle);
                    count = 0;
                }
            }

            if (newCandles.Count > 1)
            {
                newCandles.RemoveAt(0);
            }

            return newCandles;
        }

        // outgoing events / исходящие события

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<List<Portfolio>> PortfolioEvent;

        public event Action<List<Security>> SecurityEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
