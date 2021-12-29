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
using OsEngine.Market.Servers.Kraken;
using OsEngine.Market.Servers.Kraken.KrakenEntity;
using Trade = OsEngine.Entity.Trade;

namespace OsEngine.Market.Servers.Kraken
{
    public class KrakenClient
    {
        public KrakenClient(string key, string priv)
        {
            publicKey = key;
            privateKey = priv;

            Thread worker = new Thread(OrdersCheckThread);
            worker.Start();
        }

        private string publicKey;
        private string privateKey;
        private bool _isConnected;
        private bool _isDisposed;

        private KrakenRestApi _kraken;

        /// <summary>
        /// connecto to the exchange
        /// установить соединение с биржей 
        /// </summary>
        public void Connect()
        {
            if (_isConnected)
            {
                return;
            }
            try
            {
                _kraken = new KrakenRestApi(publicKey, privateKey);


                if (_kraken.GetServerTime() == null)
                {
                    SendLogMessage(OsLocalization.Market.Label56, LogMessageType.Error);
                    return;
                }

                _isConnected = true;

                if (Connected != null)
                {
                    Connected();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// bring the program to the start. Clear all objects involved in connecting to the server
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        public void Dispose()
        {
            try
            {
                _kraken.Dispose();
                _kraken = null;
            }
            catch
            {
                // ignore
            }

            if (Disconnected != null)
            {
                Disconnected();
            }

            _isDisposed = true;
        }

        public void GetBalanceAndPortfolio()
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

                if (NewPortfolioEvent != null)
                {
                    NewPortfolioEvent(newPortfolio);
                }

                _myPortfolio = newPortfolio;
                UpdatePositionOnBoard();

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private Portfolio _myPortfolio;

        public void UpdatePositionOnBoard()
        {
            var balances = _kraken.GetBalance();

            // var ret2 = JsonConvert.DeserializeObject<GetTradeBalanceResponse>(tradeBalance.ToString());

            var jProperties = JToken.Parse(balances.ToString()).SelectToken("result");//.Children<JProperty>();
            var children = jProperties.Children();

            foreach (var portfolio in children)
            {
                string res = portfolio.ToString().Replace("{", "").Replace("}", "").Replace("\"", "").Replace(" ", "");

                string[] resArr = res.Split(':');

                PositionOnBoard position = new PositionOnBoard();

                position.PortfolioName = "KrakenTradePortfolio";
                position.SecurityNameCode = resArr[0];
                position.ValueCurrent = resArr[1].ToDecimal();
                position.ValueBegin = resArr[1].ToDecimal();

                _myPortfolio.SetNewPosition(position);
            }

            if (NewPortfolioEvent != null)
            {
                NewPortfolioEvent(_myPortfolio);
            }
        }

        public event Action<Portfolio> NewPortfolioEvent;

        /// <summary>
        /// take securities
        /// взять бумаги
        /// </summary>
        public void GetSecurities()
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
                    if (assets.Keys.ElementAt(i).EndsWith(".d"))
                    {
                        continue;
                    }
                    // if (assets.Keys.ElementAt(i).StartsWith("X"))
                    // {
                    //     continue;
                    // }
                    /* if (assets.Keys.ElementAt(i).StartsWith("XZ"))
                     {
                         continue;
                     }*/

                    AssetPair pair = assets[assets.Keys.ElementAt(i)];
                    Security sec = new Security();
                    sec.Name = assets.Keys.ElementAt(i);
                    sec.NameFull = pair.WsName;
                    sec.NameClass = pair.AclassQuote;
                    sec.Decimals = pair.PairDecimals;
                    sec.NameId = sec.Name;
                    sec.SecurityType = SecurityType.CurrencyPair;
                    sec.Lot = 1;

                    decimal step = 1;

                    for (int i2 = 0; i2 < pair.PairDecimals; i2++)
                    {
                        step *= 0.1m;
                    }

                    sec.PriceStep = step;
                    sec.PriceStepCost = step;

                    securities.Add(sec);
                }

                if (UpdatePairs != null)
                {
                    for (int i = 0; i < securities.Count; i++)
                    {
                        UpdatePairs(securities[i]);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// take candles
        /// взять свечи
        /// </summary>
        /// <returns></returns>
        public List<Candle> GetCandles(string name, TimeSpan tf, int? since = null)
        {
            try
            {
                var candlesArray = _kraken.GetOHLC(name, (int)tf.TotalMinutes, since);
                //var ret = JsonConvert.DeserializeObject<GetOHLCResponse>(candlesArray.ToString());
                JObject obj = (JObject)JsonConvert.DeserializeObject(candlesArray.ToString());
                JArray err = (JArray)obj["error"];

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
                                Time = (int)v[0],
                                Open = v[1],
                                High = v[2],
                                Low = v[3],
                                Close = v[4],
                                Vwap = v[5],
                                Volume = v[6],
                                Count = (int)v[7]
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
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        #region work with orders работа с ордерами

        public void ExecuteOrder(Order order, string leverage, DateTime serverTime)
        {

            string lev = "none";

            if (leverage == "Two")
            {
                lev = "2";
            }
            if (leverage == "Three")
            {
                lev = "3";
            }
            if (leverage == "Four")
            {
                lev = "4";
            }
            if (leverage == "Five")
            {
                lev = "5";
            }

            KrakenOrder orderKraken = new KrakenOrder();
            orderKraken.Pair = order.SecurityNameCode;

            if (order.Side == Side.Buy)
            {
                orderKraken.Type = "buy";
                orderKraken.Leverage = lev;
            }
            else
            {
                orderKraken.Type = "sell";
                orderKraken.Leverage = lev;
            }
            orderKraken.Price = order.Price;
            orderKraken.OrderType = "limit";
            orderKraken.Volume = order.Volume;
            orderKraken.Validate = false;

            Execute(orderKraken, order, serverTime);
        }

        private List<Order> _osEngineOrders = new List<Order>();

        private List<KrakenOrder> _orders = new List<KrakenOrder>();

        private object _lockerListen = new object();

        private void Execute(KrakenOrder order, Order osOrder, DateTime time)
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

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(newOrder);
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

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(newOrder);
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private PlaceOrderResult PlaceOrder(ref KrakenOrder order, bool wait)
        {
            PlaceOrderResult placeOrderResult = new PlaceOrderResult();

            try
            {
                JsonObject res = _kraken.AddOrder(order);

                JsonArray error = (JsonArray)res["error"];
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
                    JsonObject result = (JsonObject)res["result"];
                    JsonObject descr = (JsonObject)result["descr"];
                    JsonArray txid = (JsonArray)result["txid"];

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

        private RefreshOrderResult RefreshOrder(ref KrakenOrder order)
        {
            RefreshOrderResult refreshOrderResult = new RefreshOrderResult();

            try
            {
                JsonObject res = _kraken.QueryOrders(order.TxId);

                JsonArray error = (JsonArray)res["error"];
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
                    JsonObject result = (JsonObject)res["result"];
                    JsonObject orderDetails = (JsonObject)result[order.TxId];

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
                        JsonArray tradesArray = (JsonArray)orderDetails["trades"];
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
        /// отменить ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            try
            {
                _kraken.CancelOrder(order.NumberMarket, true);
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

            if (MyOrderEvent != null)
            {
                MyOrderEvent(newOrder);
            }

            _closedOrders.Add(newOrder);
        }

        List<Order> _closedOrders = new List<Order>();

        private void OrdersCheckThread()
        {
            while (true)
            {
                Thread.Sleep(5000);

                if (_isDisposed == true)
                {
                    return;
                }

                GetOrders();
            }
        }

        public bool GetAllOrders(List<Order> oldOpenOrders)
        {
            return true;
        }

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
                    GenerateTrades(order);

                    Order osOrder = _osEngineOrders.Find(o => o.NumberMarket == order.TxId);

                    if (osOrder != null)
                    {
                        Order newOrder = new Order();
                        newOrder.SecurityNameCode = osOrder.SecurityNameCode;
                        newOrder.NumberUser = osOrder.NumberUser;
                        newOrder.PortfolioNumber = osOrder.PortfolioNumber;
                        newOrder.Side = osOrder.Side;
                        newOrder.State = OrderStateType.Cancel;

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(newOrder);
                        }
                    }
                }
                else
                {


                }
            }
        }

        private void GenerateTrades(KrakenOrder order)
        {
            return;
            Order osOrder = _osEngineOrders.Find(ord => ord.NumberMarket == order.TxId);

            if (order.Status == null)
            {
                return;
            }

            if (order.Status != "closed")
            {
                if (osOrder.VolumeExecute == 0)
                {
                    return;
                }
            }


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

            if (order.VolumeExecuted != null &&
                order.VolumeExecuted != 0)
            {
                newTrade.Volume = order.VolumeExecuted.ToString().ToDecimal();
            }
            else
            {
                newTrade.Volume = order.Volume.ToString().ToDecimal();
            }


            newTrade.SecurityNameCode = osOrder.SecurityNameCode;

            if (MyTradeEvent != null)
            {
                MyTradeEvent(newTrade);
            }
        }

        #endregion

        #region outgoing events / исходящие события

        /// <summary>
        /// my new orders
        /// новые мои ордера
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// my new trades
        /// новые мои сделки
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// new security in the system
        /// новые бумаги в системе
        /// </summary>
        public event Action<Security> UpdatePairs;

        /// <summary>
        /// depth updated
        /// обновился стакан
        /// </summary>
        public event Action<MarketDepth> UpdateMarketDepth;

        /// <summary>
        /// ticks updated
        /// обновились тики
        /// </summary>
        public event Action<OsEngine.Entity.Trade> NewTradesEvent;

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action Disconnected;

        #endregion

        #region log messages / сообщения для лога

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
        /// send exeptions
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion


        public static string ConvertToSocketSecName(string nameInRest)
        {
            string result = "";
            //XXBTZEUR = "XBT/EUR"

            if (nameInRest == "XZECXXBT")
            {
                return "ZEC/XBT";
            }
            if (nameInRest == "XZECZEUR")
            {
                return "ZEC/EUR";
            }
            if (nameInRest == "XZECZUSD")
            {
                return "ZEC/USD";
            }

            if (nameInRest.StartsWith("XX"))
            {
                result = nameInRest.Replace("XX", "X");
                result = result.Replace("Z", "/");
                return result;
            }
            if (nameInRest.StartsWith("XETH"))
            {
                result = nameInRest.Replace("XX", "X");
                result = result.Replace("Z", "/");

                return result;
            }
            if (nameInRest.EndsWith("ETH"))
            {
                string pap = nameInRest.Replace("ETH", "");

                if (pap.Length == 3)
                {
                    result = pap + "/" + "ETH";
                }
                else
                {
                    result = pap + "/" + "ETH";
                }

            }
            if (nameInRest.EndsWith("EUR"))
            {
                string pap = nameInRest.Replace("EUR", "");
                if (pap.Length == 3)
                {
                    result = pap + "/" + "EUR";
                }
                else
                {
                    result = pap + "/" + "EUR";
                }

            }
            if (nameInRest.EndsWith("USD"))
            {
                if (nameInRest == "XETHZUSD")
                {

                }
                string pap = nameInRest.Replace("USD", "");

                if (pap.StartsWith("X") &&
                    pap.EndsWith("Z"))
                {
                    pap = pap.Replace("Z", "");
                    pap = pap.Replace("X", "");
                }

                if (pap.Length == 3)
                {
                    result = pap + "/" + "USD";
                }
                else
                {
                    result = pap + "/" + "USD";
                }
            }
            if (nameInRest.EndsWith("USDT"))
            {
                string pap = nameInRest.Replace("USDT", "");
                if (pap.Length == 3)
                {
                    result = pap + "/" + "USDT";
                }
                else
                {
                    result = pap + "/" + "USDT";
                }

            }
            if (nameInRest.EndsWith("USDC"))
            {
                string pap = nameInRest.Replace("USDC", "");
                if (pap.Length == 3)
                {
                    result = pap + "/" + "USDC";
                }
                else
                {
                    result = pap + "/" + "USDC";
                }

            }
            if (nameInRest.EndsWith("XBT"))
            {
                string pap = nameInRest.Replace("XBT", "");
                if (pap.Length == 3)
                {
                    result = pap + "/" + "XBT";
                }
                else
                {
                    result = pap + "/" + "XBT";
                }
            }

            return result;
        }


    }


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

        [JsonProperty(PropertyName = "wsname")] public string WsName;

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