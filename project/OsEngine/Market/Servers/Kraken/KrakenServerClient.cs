/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Jayrock.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Kraken.KrakenEntity;

namespace OsEngine.Market.Servers.Kraken
{
    /// <summary>
    /// client for connection to Kraken
    /// клиент для подключения к кракену
    /// </summary>
    public class KrakenServerClient
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public KrakenServerClient()
        {
            Thread worker = new Thread(ThreadListenDataArea);
            worker.IsBackground = true;
            worker.Name = "KrakenKlientListenThread";
            worker.Start();
        }

        public void InsertProxies(List<ProxyHolder> proxies)
        {
            _proxies = proxies;
            if (_kraken != null)
            {
                _kraken.InsertProxies(proxies);
            }
        }

        private List<ProxyHolder> _proxies;

// connect
// коннект

        /// <summary>
        /// API
        /// апи
        /// </summary>
        private KrakenApi _kraken;

        /// <summary>
        /// is there a connection
        /// есть ли подключение
        /// </summary>
        private bool _isConnected;

        /// <summary>
        /// connect
        /// установить соединение
        /// </summary>
        public void Connect(string publicKey, string privateKey)
        {
            if (_isConnected)
            {
                return;
            }
            try
            {
                _kraken = new KrakenApi(publicKey, privateKey);

                if (_proxies != null)
                {
                    _kraken.InsertProxies(_proxies);
                }

                if (_kraken.GetServerTime() == null)
                {
                    SendLogMessage(OsLocalization.Market.Label56, LogMessageType.Error);
                    return;
                }

                _isConnected = true;

                if (ConnectionSucsess != null)
                {
                    ConnectionSucsess();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// disconnect
        /// отключиться
        /// </summary>
        public void Disconnect()
        {
            _isConnected = false;

            if (_kraken != null)
            {
                lock (_lockerListen)
                {
                    _kraken.Dispose();

                    _kraken = null;
                }
            }

            if (ConnectionFail != null)
            {
                ConnectionFail();
            }
        }

// thread work getting data
// Работа потока запрашивающего данные

        /// <summary>
        /// type of requested data 
        /// тип запрашиваемых данных
        /// </summary>
        public KrakenDateType DataType;

        /// <summary>
        /// multi-threaded access locker to API
        /// объект для блокировки многопоточного доступа к АПИ
        /// </summary>
        private object _lockerListen = new object();

        /// <summary>
        /// work place of thread that download data from API
        /// место работы потока который скачивает данные из апи
        /// </summary>
        private void ThreadListenDataArea()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1);
                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (_kraken == null ||
                        _isConnected == false)
                    {
                        continue;
                    }

                    // every ten seconds request a portfolio and its components / раз в десять секунд запрашиваем портфель и его составляющие

                    lock (_lockerListen)
                    {
                        // once a second request our orders and my trades / раз в секунду запрашиваем наши ордера и мои трейды

                        if (_lastTimeGetOrders.AddSeconds(2) < DateTime.Now)
                        {
                            _lastTimeGetOrders = DateTime.Now;
                            GetOrders();
                        }

                        // once in half a second request a spread on the connected securities / раз в пол секунды запрашиваем спред по подключенным бумагам

                        if (DataType == KrakenDateType.OnlyMarketDepth)
                        {
                            GetSpreads();
                        }

                        // request trades without interruption / трейды запрашиваем без перерыва

                        if (DataType == KrakenDateType.OnlyTrades)
                        {
                            GetTrades();
                        }

                        if (DataType == KrakenDateType.AllData)
                        {
                            GetSpreads();
                            GetTrades();
                        }

                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private DateTime _lastTimeData = DateTime.MinValue;

        private DateTime _lastTimeGetOrders = DateTime.MinValue;

// portfolios
// Портфели

        /// <summary>
        /// start listening to data from API
        /// начать считывание данных из АПИ
        /// </summary>
        public void InizialazeListening()
        {
            GetPortfolios();
            GetSecurities();
        }

        /// <summary>
        /// take candles by instrument
        /// взять свечи по инструменту
        /// </summary>
        /// <param name="name">instrument name / имя инструмента</param>
        /// <param name="minuteCount">number of minutes in candle/кол-во минут в свече</param>
        public List<Candle> GetCandles(string name, int minuteCount)
        {
            try
            {
                lock (_lockerListen)
                {
                    var candlesArray = _kraken.GetOHLC(name, minuteCount);

                    //var ret = JsonConvert.DeserializeObject<GetOHLCResponse>(candlesArray.ToString());
                    JObject obj = (JObject) JsonConvert.DeserializeObject(candlesArray.ToString());
                    JArray err = (JArray) obj["error"];

                    if (err.Count != 0)
                    {
                        return null;
                    }

                    JObject result = obj["result"].Value<JObject>();

                    var ret = new GetOHLCResult();
                    ret.Pairs = new Dictionary<string, List<OHLC>>();

                    foreach (var o in result)
                    {
                        if (o.Key == "last")
                        {
                            ret.Last = o.Value.Value<long>();
                        }
                        else
                        {
                            var ohlc = new List<OHLC>();
                            foreach (var v in o.Value.ToObject<decimal[][]>())
                                ohlc.Add(new OHLC()
                                {
                                    Time = (int) v[0],
                                    Open = v[1],
                                    High = v[2],
                                    Low = v[3],
                                    Close = v[4],
                                    Vwap = v[5],
                                    Volume = v[6],
                                    Count = (int) v[7]
                                });
                            ret.Pairs.Add(o.Key, ohlc);
                        }
                    }


                    var candles = ret.Pairs[name];

                    List<Candle> candlesReady = new List<Candle>();

                    for (int i = 0; i < candles.Count; i++)
                    {
                        Candle newCandle = new Candle();

                        newCandle.TimeStart = (new DateTime(1970, 1, 1, 0, 0, 0, 0)).AddSeconds(candles[i].Time);

                        newCandle.Open = candles[i].Open;
                        newCandle.High = candles[i].High;
                        newCandle.Low = candles[i].Low;
                        newCandle.Close = candles[i].Close;
                        newCandle.Volume = candles[i].Volume;

                        candlesReady.Add(newCandle);
                    }

                    return candlesReady;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// take portfolios
        /// взять портфели
        /// </summary>
        private void GetPortfolios()
        {
            if (_isConnected == false)
            {
                return;
            }
            try
            {
                var tradeBalance = _kraken.GetTradeBalance("currency", "ZUSD");

                var ret = JsonConvert.DeserializeObject<GetTradeBalanceResponse>(tradeBalance.ToString());

                Portfolio newPortfolio = new Portfolio();
                newPortfolio.Number = "KrakenTradePortfolio";

                if (ret == null ||
                    ret.Result == null)
                {
                    return;
                }

                newPortfolio.ValueCurrent = ret.Result.TradeBalance;
                newPortfolio.ValueBegin = ret.Result.TradeBalance;
                newPortfolio.ValueBlocked = ret.Result.MarginAmount;

                if (NewPortfolio != null)
                {
                    NewPortfolio(newPortfolio);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// take securities
        /// взять бумаги
        /// </summary>
        private void GetSecurities()
        {
            if (_isConnected == false)
            {
                return;
            }
            try
            {
                var message = _kraken.GetAssetPairs(null);
                var obj = JsonConvert.DeserializeObject<GetAssetPairsResponse>(message.ToString());

                if (obj.Error.Count != 0)
                {
                    SendLogMessage(obj.Error[0], LogMessageType.Error);
                    return;
                }

                var assets = obj.Result;

                List<Security> securities = new List<Security>();

                for (int i = 0; i < assets.Count; i++)
                {
                    AssetPair pair = assets[assets.Keys.ElementAt(i)];
                    Security sec = new Security();
                    sec.Name = assets.Keys.ElementAt(i);
                    sec.NameFull = pair.Base;
                    sec.NameClass = pair.AclassQuote;
                    sec.Decimals = pair.PairDecimals;

                    decimal step = 1;

                    for (int i2 = 0; i2 < pair.PairDecimals; i2++)
                    {
                        step *= 0.1m;
                    }

                    sec.PriceStep = step;
                    sec.PriceStepCost = step;

                    securities.Add(sec);
                }

                if (NewSecuritiesEvent != null)
                {
                    NewSecuritiesEvent(securities);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// update order data
        /// обновить данные по ордерам
        /// </summary>
        private void GetOrders()
        {
            for (int i = 0; i < _orders.Count; i++)
            {
                Thread.Sleep(1000);

                KrakenOrder order = _orders[i];
                

                RefreshOrder(ref order);

                if (order.Status == "closed")
                {
                    GenerateTrades(order);
                    _orders.Remove(order);
                }
                else if (order.Status == "canceled" ||
                         order.Status == "expired" ||
                         order.Status == null)
                {
                    _orders.Remove(order);

                    Order osOrder = _osEngineOrders.Find(o => o.NumberMarket == order.TxId);

                    if (osOrder != null)
                    {
                        Order newOrder = new Order();
                        newOrder.SecurityNameCode = osOrder.SecurityNameCode;
                        newOrder.NumberUser = osOrder.NumberUser;
                        newOrder.PortfolioNumber = osOrder.PortfolioNumber;
                        newOrder.Side = osOrder.Side;
                        newOrder.State = OrderStateType.Cancel;

                        if (NewOrderEvent != null)
                        {
                            NewOrderEvent(newOrder);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// generete my trade from order
        /// сгенерировать мои трейды из ордера
        /// </summary>
        /// <param name="order"></param>
        private void GenerateTrades(KrakenOrder order)
        {
            Order osOrder = _osEngineOrders.Find(ord => ord.NumberMarket == order.TxId);

            MyTrade newTrade = new MyTrade();
            newTrade.NumberOrderParent = order.TxId;
            newTrade.NumberTrade = NumberGen.GetNumberOrder(StartProgram.IsOsTrader).ToString();

            newTrade.Side = osOrder.Side;
            if (order.AveragePrice != null &&
                order.AveragePrice != 0)
            {
                newTrade.Price = Convert.ToDecimal(order.AveragePrice);
            }
            else
            {
                newTrade.Price = osOrder.Price;
            }

            newTrade.Volume = order.Volume;
            newTrade.SecurityNameCode = osOrder.SecurityNameCode;

            if (NewMyTradeEvent != null)
            {
                NewMyTradeEvent(newTrade);
            }
        }

        /// <summary>
        /// list of instruments for listening
        /// список инструментов подключеннных на обновление
        /// </summary>
        private List<string> _namesListenSecurities = new List<string>();

        /// <summary>
        /// take depths from API
        /// взять стаканы из АПИ
        /// </summary>
        private void GetSpreads()
        {
            if (_lastTimeData == DateTime.MinValue)
            {
                _lastTimeData = DateTime.Now;
            }

            for (int i = 0; i < _namesListenSecurities.Count; i++)
            {

                var json = _kraken.GetOrderBook(_namesListenSecurities[i], 6);
                var obj = JsonConvert.DeserializeObject<GetOrderBookResponse>(json.ToString());

                if (obj.Error.Count != 0)
                {
                    SendLogMessage(obj.Error[0], LogMessageType.Error);
                    return;
                }

                var trades = obj.Result.ToList();

                for (int i2 = 0; i2 < trades.Count; i2++)
                {
                    OrderBook info = trades[i2].Value;

                    MarketDepth newDepth = new MarketDepth();
                    newDepth.SecurityNameCode = trades[i2].Key;

                    int countElem = info.Bids.GetLength(0);
                    for (int i3 = 0; i3 < countElem; i3++)
                    {
                        newDepth.Bids.Add(new MarketDepthLevel() {Bid = info.Bids[i3][1], Price = info.Bids[i3][0]});
                        newDepth.Time =
                            (new DateTime(1970, 1, 1, 0, 0, 0, 0)).AddSeconds(Convert.ToInt32(info.Bids[0][2]));
                    }

                    int countElemAsk = info.Asks.GetLength(0);
                    for (int i3 = 0; i3 < countElemAsk; i3++)
                    {
                        newDepth.Asks.Add(new MarketDepthLevel() {Ask = info.Asks[i3][1], Price = info.Asks[i3][0]});
                    }

                    if (newDepth.Asks.Count == 0 ||
                        newDepth.Bids.Count == 0)
                    {
                        return;
                    }

                    if (NewMarketDepthEvent != null)
                    {
                        NewMarketDepthEvent(newDepth);
                    }
                }
            }

        }

        /// <summary>
        /// objects required for loading of trades from API
        /// объекты необходимые для загрузки трейдов из АПИ
        /// </summary>
        private List<DataSinece> _timeTrades = new List<DataSinece>();

        /// <summary>
        /// take trades from API
        /// взять трейды из АПИ
        /// </summary>
        private void GetTrades()
        {
            for (int i = 0; i < _namesListenSecurities.Count; i++)
            {

                DataSinece myTimeTrades = _timeTrades.Find(t => t.NameSecurity == _namesListenSecurities[i]);

                if (myTimeTrades == null)
                {
                    myTimeTrades = new DataSinece();
                    myTimeTrades.NameSecurity = _namesListenSecurities[i];
                    _timeTrades.Add(myTimeTrades);
                }

                JsonObject message = null;

                if (myTimeTrades.Time == 0)
                {
                    message = _kraken.GetRecentTrades(_namesListenSecurities[i]);
                }
                else
                {
                    message = _kraken.GetRecentTrades(_namesListenSecurities[i], myTimeTrades.Time);
                }

                JObject obj = (JObject) JsonConvert.DeserializeObject(message.ToString());
                JArray err = (JArray) obj["error"];
                if (err.Count != 0)
                {
                    SendLogMessage(err[0].ToString(), LogMessageType.Error);
                }

                var ret = new GetRecentTradesResult();
                ret.Trades = new Dictionary<string, List<Trade>>();

                JObject result = obj["result"].Value<JObject>();
                foreach (var o in result)
                {
                    if (o.Key == "last")
                    {
                        ret.Last = o.Value.Value<long>();
                    }
                    else
                    {
                        var trade = new List<Trade>();

                        foreach (var v in (JArray) o.Value)
                        {
                            var a = (JArray) v;

                            trade.Add(new Trade()
                            {
                                Price = a[0].Value<decimal>(),
                                Volume = a[1].Value<decimal>(),
                                Time = a[2].Value<int>(),
                                Side = a[3].Value<string>(),
                                Type = a[4].Value<string>(),
                                Misc = a[5].Value<string>()
                            });
                        }

                        ret.Trades.Add(o.Key, trade);
                    }
                }

                var trades = ret;

                for (int i2 = 0; i2 < trades.Trades.Count; i2++)
                {
                    List<Trade> info = trades.Trades[trades.Trades.Keys.ElementAt(i2)];

                    for (int i3 = 0; i3 < info.Count; i3++)
                    {
                        OsEngine.Entity.Trade newTrade = new OsEngine.Entity.Trade();

                        newTrade.SecurityNameCode = _namesListenSecurities[i];
                        newTrade.Price = info[i3].Price;
                        newTrade.Volume = info[i3].Volume;

                        newTrade.Time = (new DateTime(1970, 1, 1, 0, 0, 0, 0)).AddSeconds(info[i3].Time);

                        if (info[i3].Side == "s")
                        {
                            newTrade.Side = Side.Sell;
                        }
                        else
                        {
                            newTrade.Side = Side.Buy;
                        }

                        if (NewTradeEvent != null)
                        {
                            NewTradeEvent(newTrade);
                        }

                        myTimeTrades.Time = trades.Last;

                        if (i3 + 1 == info.Count)
                        {
                            MarketDepth newDepth = new MarketDepth();
                            newDepth.SecurityNameCode = _namesListenSecurities[i];
                            newDepth.Bids.Add(new MarketDepthLevel() {Bid = 1, Price = newTrade.Price});
                            newDepth.Asks.Add(new MarketDepthLevel() {Ask = 1, Price = newTrade.Price});

                            newDepth.Time = newTrade.Time;

                            if (NewMarketDepthEvent != null)
                            {
                                NewMarketDepthEvent(newDepth);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// start listenin to data instrument
        /// включить инструмент на подгрузку данных
        /// </summary>
        public void ListenSecurity(string name)
        {
            if (_namesListenSecurities.Find(s => s == name) == null)
            {
                _namesListenSecurities.Add(name);
            }
        }

        /// <summary>
        /// initial orders in OsEngine format
        /// исходные ордера в формате OsEngine
        /// </summary>
        private List<Order> _osEngineOrders = new List<Order>();

        /// <summary>
        /// execute order in the exchange
        /// исполнить ордер на бирже
        /// </summary>
        public void ExecuteOrder(KrakenOrder order, Order osOrder, DateTime time)
        {
            if (_isConnected == false)
            {
                return;
            }

            if (_orders == null)
            {
                _orders = new List<KrakenOrder>();
            }
            _orders.Add(order);
            _osEngineOrders.Add(osOrder);

            lock (_lockerListen)
            {
                try
                {
                    PlaceOrderResult result = PlaceOrder(ref order, false);

                    SendLogMessage(result.ResultType.ToString(), LogMessageType.System);

                    if (order.TxId != null &&
                        result.ResultType == PlaceOrderResultType.success)
                    {
                        Order newOrder = new Order();
                        newOrder.SecurityNameCode = osOrder.SecurityNameCode;
                        newOrder.NumberUser = osOrder.NumberUser;
                        newOrder.NumberMarket = order.TxId;
                        newOrder.PortfolioNumber = osOrder.PortfolioNumber;
                        newOrder.Side = osOrder.Side;
                        newOrder.State = OrderStateType.Activ;
                        newOrder.TimeCallBack = time;

                        if (NewOrderEvent != null)
                        {
                            NewOrderEvent(newOrder);
                        }
                    }
                    else
                    {
                        Order newOrder = new Order();
                        newOrder.SecurityNameCode = osOrder.SecurityNameCode;
                        newOrder.NumberUser = osOrder.NumberUser;
                        newOrder.PortfolioNumber = osOrder.PortfolioNumber;
                        newOrder.Side = osOrder.Side;
                        newOrder.State = OrderStateType.Fail;

                        if (NewOrderEvent != null)
                        {
                            NewOrderEvent(newOrder);
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// Submit an order to Kraken. The order passed by reference will be updated with info set by Kraken.
        /// </summary>
        /// <param name="order">Order to submit.</param>
        /// <param name="wait">If set to true, the function will wait until the order is closed or canceled.</param>
        /// <returns>PlaceOrderResult containing info about eventual success or failure of the request</returns>
        private PlaceOrderResult PlaceOrder(ref KrakenOrder order, bool wait)
        {
            PlaceOrderResult placeOrderResult = new PlaceOrderResult();

            try
            {
                JsonObject res = _kraken.AddOrder(order);

                JsonArray error = (JsonArray) res["error"];
                if (error.Count() > 0)
                {
                    placeOrderResult.ResultType = PlaceOrderResultType.error;
                    List<string> errorList = new List<string>();
                    foreach (var item in error)
                    {
                        errorList.Add(item.ToString());
                    }
                    placeOrderResult.Errors = errorList;
                    return placeOrderResult;
                }
                else
                {
                    JsonObject result = (JsonObject) res["result"];
                    JsonObject descr = (JsonObject) result["descr"];
                    JsonArray txid = (JsonArray) result["txid"];

                    if (txid == null)
                    {
                        placeOrderResult.ResultType = PlaceOrderResultType.txid_null;
                        return placeOrderResult;
                    }
                    else
                    {
                        string transactionIds = "";

                        foreach (var item in txid)
                        {
                            transactionIds += item.ToString() + ",";
                        }
                        transactionIds = transactionIds.TrimEnd(',');

                        order.TxId = transactionIds;

                        if (wait)
                        {
                            #region Repeatedly check order status by calling RefreshOrder

                            bool keepSpinning = true;
                            while (keepSpinning)
                            {
                                RefreshOrderResult refreshOrderResult = RefreshOrder(ref order);
                                switch (refreshOrderResult.ResultType)
                                {
                                    case RefreshOrderResultType.success:
                                        switch (order.Status)
                                        {
                                            case "closed":
                                                placeOrderResult.ResultType = PlaceOrderResultType.success;
                                                return placeOrderResult;
                                            case "pending":
                                                break;
                                            case "open":
                                                break;
                                            case "canceled":
                                                if (order.VolumeExecuted > 0)
                                                {
                                                    placeOrderResult.ResultType = PlaceOrderResultType.partial;
                                                    return placeOrderResult;
                                                }
                                                else
                                                {
                                                    placeOrderResult.ResultType =
                                                        PlaceOrderResultType.canceled_not_partial;
                                                    return placeOrderResult;
                                                }
                                            default:
                                                throw new Exception(string.Format("Unknown type of order status: {0}",
                                                    order.Status));
                                        }
                                        break;
                                    case RefreshOrderResultType.error:
                                        throw new Exception(
                                            string.Format(
                                                "An error occured while trying to refresh the order.\nError List: {0}",
                                                refreshOrderResult.Errors.ToString()));

                                    case RefreshOrderResultType.order_not_found:
                                        throw new Exception(
                                            "An error occured while trying to refresh the order.\nOrder not found");

                                    case RefreshOrderResultType.exception:
                                        throw new Exception(
                                            "An unexpected exception occured while trying to refresh the order.",
                                            refreshOrderResult.Exception);

                                    default:
                                        keepSpinning = false;
                                        break;
                                }
                                Thread.Sleep(5000);
                            }

                            #endregion
                        }

                        placeOrderResult.ResultType = PlaceOrderResultType.success;
                        return placeOrderResult;
                    }
                }
            }
            catch (Exception ex)
            {
                placeOrderResult.ResultType = PlaceOrderResultType.exception;
                placeOrderResult.Exception = ex;
                return placeOrderResult;
            }

        }

        /// <summary>
        /// Call Kraken to update info about order execution.
        /// </summary>
        /// <param name="order">Order to update</param>
        /// <returns>RefreshOrderResult containing info about eventual success or failure of the request</returns>
        private RefreshOrderResult RefreshOrder(ref KrakenOrder order)
        {
            RefreshOrderResult refreshOrderResult = new RefreshOrderResult();

            try
            {
                JsonObject res = _kraken.QueryOrders(order.TxId);

                JsonArray error = (JsonArray) res["error"];
                if (error.Count() > 0)
                {
                    refreshOrderResult.ResultType = RefreshOrderResultType.error;
                    List<string> errorList = new List<string>();
                    foreach (var item in error)
                    {
                        errorList.Add(item.ToString());
                    }
                    refreshOrderResult.Errors = errorList;
                    return refreshOrderResult;
                }
                else
                {
                    JsonObject result = (JsonObject) res["result"];
                    JsonObject orderDetails = (JsonObject) result[order.TxId];

                    if (orderDetails == null)
                    {
                        refreshOrderResult.ResultType = RefreshOrderResultType.order_not_found;
                        return refreshOrderResult;
                    }
                    else
                    {
                        string status = (orderDetails["status"] != null) ? orderDetails["status"].ToString() : null;
                        string reason = (orderDetails["reason"] != null) ? orderDetails["reason"].ToString() : null;
                        string openTime = (orderDetails["opentm"] != null) ? orderDetails["opentm"].ToString() : null;
                        string closeTime = (orderDetails["closetm"] != null) ? orderDetails["closetm"].ToString() : null;
                        string vol_exec = (orderDetails["vol_exec"] != null)
                            ? orderDetails["vol_exec"].ToString()
                            : null;
                        string cost = (orderDetails["cost"] != null) ? orderDetails["cost"].ToString() : null;
                        string fee = (orderDetails["fee"] != null) ? orderDetails["fee"].ToString() : null;
                        string price = (orderDetails["price"] != null) ? orderDetails["price"].ToString() : null;
                        string misc = (orderDetails["misc"] != null) ? orderDetails["misc"].ToString() : null;
                        string oflags = (orderDetails["oflags"] != null) ? orderDetails["oflags"].ToString() : null;
                        JsonArray tradesArray = (JsonArray) orderDetails["trades"];
                        string trades = null;
                        if (tradesArray != null)
                        {

                            foreach (var item in tradesArray)
                            {
                                trades += item.ToString() + ",";
                            }
                            trades = trades.TrimEnd(',');
                        }

                        order.Status = status;
                        order.Reason = reason;
                        order.OpenTime = openTime;
                        order.CloseTime = closeTime;
                        order.VolumeExecuted = double.Parse(vol_exec.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                        order.Cost = decimal.Parse(cost.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                        order.Fee = decimal.Parse(fee.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                        order.AveragePrice = decimal.Parse(price.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                        order.Info = misc;
                        order.OFlags = oflags;
                        order.Trades = trades;

                        refreshOrderResult.ResultType = RefreshOrderResultType.success;
                        return refreshOrderResult;
                    }
                }
            }
            catch (Exception ex)
            {
                refreshOrderResult.ResultType = RefreshOrderResultType.exception;
                refreshOrderResult.Exception = ex;
                return refreshOrderResult;
            }
        }

        /// <summary>
        /// cancel order
        /// отозвать ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            try
            {
                _kraken.CancelOrder(order.NumberMarket);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            Order newOrder = new Order();
            newOrder.SecurityNameCode = order.SecurityNameCode;
            newOrder.NumberUser = order.NumberUser;
            newOrder.NumberMarket = order.NumberMarket;
            newOrder.PortfolioNumber = order.PortfolioNumber;
            newOrder.Side = order.Side;
            newOrder.State = OrderStateType.Cancel;

            if (NewOrderEvent != null)
            {
                NewOrderEvent(newOrder);
            }
        }

        /// <summary>
        /// list of orders
        /// список ордеров
        /// </summary>
        private List<KrakenOrder> _orders = new List<KrakenOrder>();

        /// <summary>
        /// new position by portfolio
        /// новая позиция по портфелю
        /// </summary>
        public event Action<Portfolio> NewPortfolio;

        /// <summary>
        /// new trade
        /// новый трейд
        /// </summary>
        public event Action<OsEngine.Entity.Trade> NewTradeEvent;

// outgoin event
// исходящие события

        /// <summary>
        /// new order in the system
        /// новый ордер в системе
        /// </summary>
        public event Action<Order> NewOrderEvent;

        /// <summary>
        /// new securities in the system
        /// новые бумаги в системе
        /// </summary>
        public event Action<List<Security>> NewSecuritiesEvent;

        /// <summary>
        /// new my trade in the system
        /// новая моя сделка в системе
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        /// <summary>
        /// new depth in the system
        /// новый стакан в системе
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

        /// <summary>
        /// successfully connected to TWS server
        /// успешно подключились к серверу TWS
        /// </summary>
        public event Action ConnectionSucsess;

        /// <summary>
        /// connection with TWS lost
        /// соединение с TWS разорвано
        /// </summary>
        public event Action ConnectionFail;

// logging
// логирование работы

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

    // next are the objects needed to convert JSon messages / далее идут объекты необходимые для конвертации JSon сообщений

    public class DataSinece
    {
        public string NameSecurity;

        public long Time;
    }

    public class TradeBalanceInfo
    {
        /// <summary>
        /// Equivalent balance(combined balance of all currencies).
        /// </summary>
        [JsonProperty(PropertyName = "eb")] public decimal EquivalentBalance;

        /// <summary>
        /// Trade balance(combined balance of all equity currencies).
        /// </summary>
        [JsonProperty(PropertyName = "tb")] public decimal TradeBalance;

        /// <summary>
        /// Margin amount of open positions.
        /// </summary>
        [JsonProperty(PropertyName = "m")] public decimal MarginAmount;

        /// <summary>
        /// Unrealized net profit/loss of open positions.
        /// </summary>
        [JsonProperty(PropertyName = "n")] public decimal UnrealizedProfitAndLoss;

        /// <summary>
        /// Cost basis of open positions.
        /// </summary>
        [JsonProperty(PropertyName = "c")] public decimal CostBasis;

        /// <summary>
        /// Current floating valuation of open positions.
        /// </summary>
        [JsonProperty(PropertyName = "v")] public decimal FloatingValutation;

        /// <summary>
        /// Equity = trade balance + unrealized net profit/loss.
        /// </summary>
        [JsonProperty(PropertyName = "e")] public decimal Equity;

        /// <summary>
        /// Free margin = equity - initial margin(maximum margin available to open new positions).
        /// </summary>
        [JsonProperty(PropertyName = "mf")] public decimal FreeMargin;

        /// <summary>
        /// Margin level = (equity / initial margin) * 100
        /// </summary>
        [JsonProperty(PropertyName = "ml")] public decimal MarginLevel;
    }

    public class GetTradeBalanceResponse : ResponseBase
    {
        public TradeBalanceInfo Result;
    }

    public class ResponseBase
    {
        public List<string> Error;
    }

    public class GetAssetPairsResponse : ResponseBase
    {
        public Dictionary<string, AssetPair> Result;
    }

    public class AssetPair
    {
        /// <summary>
        /// Alternate pair name.
        /// </summary>
        public string Altname;

        /// <summary>
        /// Asset private class of base component.
        /// </summary>
        [JsonProperty(PropertyName = "aclass_base")] public string AclassBase;

        /// <summary>
        /// Asset id of base component
        /// </summary>
        public string Base;

        /// <summary>
        /// Asset class of quote component.
        /// </summary>
        [JsonProperty(PropertyName = "aclass_quote")] public string AclassQuote;

        /// <summary>
        /// Asset id of quote component.
        /// </summary>
        public string Quote;

        /// <summary>
        /// Volume lot size.
        /// </summary>
        public string Lot;

        /// <summary>
        /// Scaling decimal places for pair.
        /// </summary>
        [JsonProperty(PropertyName = "pair_decimals")] public int PairDecimals;

        /// <summary>
        /// Scaling decimal places for volume.
        /// </summary>
        [JsonProperty(PropertyName = "lot_decimals")] public int LotDecimals;

        /// <summary>
        /// Amount to multiply lot volume by to get currency volume.
        /// </summary>
        [JsonProperty(PropertyName = "lot_multiplier")] public int LotMultiplier;

        /// <summary>
        /// Array of leverage amounts available when buying.
        /// </summary>
        [JsonProperty(PropertyName = "leverage_buy")] public decimal[] LeverageBuy;

        /// <summary>
        /// Array of leverage amounts available when selling.
        /// </summary>
        [JsonProperty(PropertyName = "leverage_sell")] public decimal[] LeverageSell;

        /// <summary>
        /// Fee schedule array in [volume, percent fee].
        /// </summary>
        public decimal[][] Fees;

        /// <summary>
        /// Maker fee schedule array in [volume, percent fee] tuples(if on maker/taker).
        /// </summary>
        [JsonProperty(PropertyName = "fees_maker")] public decimal[][] FeesMaker;

        /// <summary>
        /// Volume discount currency
        /// </summary>
        [JsonProperty(PropertyName = "fee_volume_currency")] public string FeeVolumeCurrency;

        /// <summary>
        /// Margin call level.
        /// </summary>
        [JsonProperty(PropertyName = "margin_call")] public decimal MarginCall;

        /// <summary>
        /// Stop-out/liquidation margin level.
        /// </summary>
        [JsonProperty(PropertyName = "margin_stop")] public decimal MarginStop;
    }

    public class OrderBook
    {
        /// <summary>
        /// Ask side array of array entries(<price>, <volume>, <timestamp>)
        /// </summary>
        public decimal[][] Asks;

        /// <summary>
        /// Bid side array of array entries(<price>, <volume>, <timestamp>)
        /// </summary>
        public decimal[][] Bids;
    }

    public class GetOrderBookResponse : ResponseBase
    {
        public Dictionary<string, OrderBook> Result;
    }

    public class Trade
    {
        public decimal Price;
        public decimal Volume;
        public int Time;
        public string Side;
        public string Type;
        public string Misc;
    }

    public class GetRecentTradesResult
    {
        public Dictionary<string, List<Trade>> Trades;

        /// <summary>
        /// Id to be used as since when polling for new trade data.
        /// </summary>
        public long Last;
    }

    public class PlaceOrderResult
    {
        public PlaceOrderResultType ResultType { get; set; }

        //Set only if ResultType = error
        public List<string> Errors { get; set; }

        //Set only if ResultType = exception
        public Exception Exception { get; set; }
    }

    public enum PlaceOrderResultType
    {
        error,
        txid_null,
        success,
        partial,
        canceled_not_partial,
        exception,
    }

    public class RefreshOrderResult
    {
        public RefreshOrderResultType ResultType { get; set; }

        //Set only if ResultType = error
        public List<string> Errors { get; set; }

        //Set only if ResultType = exception
        public Exception Exception { get; set; }
    }

    public enum RefreshOrderResultType
    {
        error,
        exception,
        order_not_found,
        success,
    }

    public class OHLC
    {
        public int Time;
        public decimal Open;
        public decimal High;
        public decimal Low;
        public decimal Close;
        public decimal Vwap;
        public decimal Volume;
        public int Count;
    }

    public class GetOHLCResult
    {
        public Dictionary<string, List<OHLC>> Pairs;

        // <summary>
        /// Id to be used as since when polling for new, committed OHLC data.
        /// </summary>
        public long Last;
    }

    public class GetOHLCResponse : ResponseBase
    {
        public GetOHLCResult Result;
    }
}
