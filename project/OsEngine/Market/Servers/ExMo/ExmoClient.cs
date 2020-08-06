using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using RestSharp.Extensions.MonoHttp;

namespace OsEngine.Market.Servers.ExMo
{
    public class ExmoClient
    {
        public ExmoClient(string pubKey, string secKey)
        {
            _key = pubKey;
            _secret = secKey;

            Thread worker = new Thread(WorkerPlace);
            worker.IsBackground = true;
            worker.Start();

            _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(500));
        }

        RateGate _rateGate;

        public string _key;

        public string _secret;

        private string _url = "http://api.exmo.me/v1/";

        private static long _nounce;

        /// <summary>
        /// connecto to the exchange
        /// установить соединение с биржей 
        /// </summary>
        public void Connect()
        {
            if (string.IsNullOrEmpty(_key) ||
                string.IsNullOrEmpty(_secret))
            {
                return;
            }

            _nounce = GetTimestamp();

            IsConnected = true;

            if (Connected != null)
            {
                Connected();
            }

        }

        /// <summary>
        /// bring the program to the start. Clear all objects involved in connecting to the server
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        public void Dispose()
        {
            IsConnected = false;

            if (Disconnected != null)
            {
                Disconnected();
            }

            _isDisposed = true;
        }

        private bool _isDisposed;

        /// <summary>
        /// shows whether connection works
        /// работает ли соединение
        /// </summary>
        public bool IsConnected;

        /// <summary>
        /// multi-threaded access locker to http-requests
        /// блокиратор многопоточного доступа к http запросам
        /// </summary>
        private object _queryHttpLocker = new object();

        public string ApiQuery(string apiName, IDictionary<string, string> req, string tradeCouples = null,
            int? limit = null)
        {
            try
            {
                lock (_queryHttpLocker)
                {
                    _rateGate.WaitToProceed();

                    using (var wb = new WebClient())
                    {
                        req.Add("nonce", Convert.ToString(_nounce++));

                        if (limit != null)
                        {
                            req.Add("limit", limit.ToString());
                        }

                        if (tradeCouples != null)
                        {
                            req.Add("pair", tradeCouples);
                        }

                        var message = ToQueryString(req);



                        var sign = Sign(_secret, message);

                        wb.Headers.Add("Sign", sign);
                        wb.Headers.Add("Key", _key);

                        var data = req.ToNameValueCollection();
                        //var response = wb.UploadValues(string.Format(_url, apiName), "POST", data);
                        byte[] response;

                        response = wb.UploadValues(_url + apiName + "//", "POST", data);
                        return Encoding.UTF8.GetString(response);
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);

                IsConnected = false;

                if (Disconnected != null)
                {
                    Disconnected();
                }

                return "";
            }
        }

        private string ToQueryString(IDictionary<string, string> dic)
        {
            var array = (from key in dic.Keys
                    select string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(dic[key])))
                .ToArray();
            return string.Join("&", array);
        }

        public static string Sign(string key, string message)
        {
            using (HMACSHA512 hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key)))
            {
                byte[] b = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return ByteToString(b);
            }
        }

        public static string ByteToString(byte[] buff)
        {
            string sbinary = "";

            for (int i = 0; i < buff.Length; i++)
            {
                sbinary += buff[i].ToString("X2"); // hex format
            }

            return (sbinary).ToLowerInvariant();
        }

        public static long GetTimestamp()
        {
            var d = (DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds;
            return (long) d;
        }

        #region Подписка на дату

        List<Security> _securitiesSubscrible = new List<Security>();

        public void SubscribleTradesAndDepths(Security security)
        {
            if (_securitiesSubscrible.Find(s => s.Name == security.Name) != null)
            {
                return;
            }

            _securitiesSubscrible.Add(security);
        }

        private DateTime _lastBalanceUpdTime;

        private void WorkerPlace()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (_isDisposed)
                    {
                        return;
                    }

                    if (IsConnected == false)
                    {
                        continue;
                    }

                    if (_portfolio != null &&
                        _lastBalanceUpdTime.AddSeconds(15) < DateTime.Now)
                    {
                        UpdateBalance();
                        _lastBalanceUpdTime = DateTime.Now;
                    }

                    string secStr = GetSecuritiesString(_securitiesSubscrible);

                    GetTrade(secStr);
                    GetMarketDepth(secStr);

                    List<MyTrade> myTrades = UpdMyTrades(secStr);

                    for (int i = 0; i < myTrades.Count; i++)
                    {
                        if (_myTrades.Find(
                                t => t.NumberTrade == myTrades[i].NumberTrade) == null)
                        {
                            _myTrades.Add(myTrades[i]);

                            if (MyTradeEvent != null)
                            {
                                MyTradeEvent(myTrades[i]);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                    continue;
                }
            }
        }

        List<MyTrade> _myTrades = new List<MyTrade>();

        #endregion

        #region Запросы

        private Portfolio _portfolio;

        /// <summary>
        /// take balance
        /// взять баланс
        /// </summary>
        public void GetBalance()
        {
            _portfolio = new Portfolio();
            _portfolio.Number = "ExmoPortfolio";

            List<string> currencies = GetCurrencies();

            for (int i = 0; i < currencies.Count; i++)
            {
                PositionOnBoard positionOnBoard = new PositionOnBoard();
                positionOnBoard.PortfolioName = _portfolio.Number;
                positionOnBoard.SecurityNameCode = currencies[i];
                _portfolio.SetNewPosition(positionOnBoard);
            }
        }

        private List<string> GetCurrencies()
        {
            List<string> currencies = new List<string>();


            var jsonCurrency = ApiQuery("currency", new Dictionary<string, string>());

            var parserStrings = jsonCurrency.ToString()
                .Replace("[", "")
                .Replace("]", "")
                .Replace("\"", "")
                .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            currencies = parserStrings.Cast<string>().ToList();


            return currencies;
        }

        private void UpdateBalance()
        {
            if (_portfolio == null)
            {
                return;
            }

            var jsonCurrency = ApiQuery("user_info", new Dictionary<string, string>());
            var jProperties = JToken.Parse(jsonCurrency).SelectToken("balances").Children<JProperty>();

            foreach (var jtSecurity in jProperties)
            {
                List<string> obj = jtSecurity.ToString().Split(':').ToList();

                PositionOnBoard positionOnBoard = new PositionOnBoard();
                positionOnBoard.PortfolioName = _portfolio.Number;
                positionOnBoard.SecurityNameCode = obj[0]
                    .Replace('"', ' ')
                    .Replace('/', ' ')
                    .Replace(" ", "");

                positionOnBoard.ValueCurrent = obj[1]
                    .Replace('"', ' ')
                    .Replace('/', ' ')
                    .Replace(" ", "").ToDecimal();
                positionOnBoard.ValueBegin = positionOnBoard.ValueCurrent;

                _portfolio.SetNewPosition(positionOnBoard);
            }


            if (UpdatePortfolio != null)
            {
                UpdatePortfolio(_portfolio);
            }
        }

        /// <summary>
        /// take securities
        /// взять бумаги
        /// </summary>
        public void GetSecurities()
        {
            var jsonCurrency = ApiQuery("pair_settings", new Dictionary<string, string>());
            var jProperties = JToken.Parse(jsonCurrency).SelectToken("").Children<JProperty>();

            List<Security> securities = new List<Security>();

            foreach (var jtSecurity in jProperties)
            {
                Security newSecurity = new Security();

                var name = jtSecurity.Name;
                newSecurity.Name = name;
                newSecurity.NameFull = name;
                newSecurity.NameId = name;
                newSecurity.NameClass = name.Split('_')[1];
                newSecurity.SecurityType = SecurityType.CurrencyPair;
                newSecurity.Decimals
                    = jtSecurity.Value.SelectToken("min_quantity").ToString().DecimalsCount();
                newSecurity.PriceStep =
                    jtSecurity.Value.SelectToken("min_quantity").ToString().ToDecimal();

                newSecurity.PriceStepCost = newSecurity.PriceStep;

                newSecurity.State = SecurityStateType.Activ;
                newSecurity.Lot = 1;
                newSecurity.PriceLimitHigh =
                    jtSecurity.Value.SelectToken("max_price").ToString().DecimalsCount();
                newSecurity.PriceLimitLow =
                    jtSecurity.Value.SelectToken("min_price").ToString().DecimalsCount();

                securities.Add(newSecurity);
            }

            // "min_quantity": "0.001",
            // "max_quantity": "100",
            // "min_price": "1",
            //"max_price": "10000",
            //"max_amount": "30000",
            // "min_amount": "1"
            if (UpdatePairs != null)
            {
                UpdatePairs(securities);
            }

        }

        public void GetTrade(string securities)
        {
            if (securities == "")
            {
                return;
            }

            var jsonCurrency = ApiQuery("trades", new Dictionary<string, string>(), securities);

            string[] secs = securities.Split(',');

            for (int i = 0; i < secs.Length; i++)
            {
                var jProperties = JToken.Parse(jsonCurrency)[secs[i]]; //.Children<JProperty>();
                List<Trade> trades = new List<Trade>();

                foreach (var jtTrade in jProperties)
                {
                    Trade newTrade = new Trade();

                    newTrade.SecurityNameCode = secs[i];
                    newTrade.Price =
                        jtTrade.SelectToken("price").ToString().ToDecimal();

                    newTrade.Volume =
                        jtTrade.SelectToken("amount").ToString().ToDecimal();

                    newTrade.Id =
                        jtTrade.SelectToken("trade_id").ToString();

                    newTrade.Time =
                        GetDateTime(jtTrade.SelectToken("date").ToString());

                    string side = jtTrade.SelectToken("type").ToString();

                    if (side == "sell")
                    {
                        newTrade.Side = Side.Sell;
                    }
                    else
                    {
                        newTrade.Side = Side.Buy;
                    }

                    trades.Insert(0,newTrade);
                }

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(trades);
                }
            }

            // "trade_id": 3,
           // "type": "sell",
           // "price": "100",
           // "quantity": "1",
           // "amount": "100",
           // "date": 1435488248


        }

        public void GetMarketDepth(string securities)
        {
            if (securities == "")
            {
                return;
            }

            // var jsonCurrency = ApiQuery("order_book", null, securities);

            // ask_quantity - объем всех ордеров на продажу
            // ask_amount - сумма всех ордеров на продажу
            // ask_top - минимальная цена продажи
            // bid_quantity - объем всех ордеров на покупку
            // bid_amount - сумма всех ордеров на покупку
            // bid_top - максимальная цена покупки
            // bid - список ордеров на покупку, где каждая строка это цена, количество и сумма
            // ask - список ордеров на продажу, где каждая строка это цена, количество и сумма

            var jsonCurrency = ApiQuery("order_book", new Dictionary<string, string>(), securities, 20);

            string[] secs = securities.Split(',');

            for (int i = 0; i < secs.Length; i++)
            {
                var jProperties = JToken.Parse(jsonCurrency)[secs[i]]; //.Children<JProperty>();

                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = secs[i];

                var jBid =
                    JToken.Parse(jProperties.SelectToken("bid").ToString());

                List<MarketDepthLevel> Bids = new List<MarketDepthLevel>();

                foreach (var bid in jBid)
                {
                    MarketDepthLevel newBid = new MarketDepthLevel();

                    var str = bid.ToArray();
                    newBid.Price = str[0].ToString()
                        .Replace("{","")
                        .Replace("}", "")
                        .ToDecimal();

                    newBid.Bid = str[1].ToString()
                        .Replace("{", "")
                        .Replace("}", "")
                        .ToDecimal();
                    //newBid.Bid = bid..SelectToken("price").ToString().ToDecimal();
                    Bids.Add(newBid);
                }

                depth.Bids = Bids;


                var jAsk =
                    JToken.Parse(jProperties.SelectToken("ask").ToString());

                List<MarketDepthLevel> Ask = new List<MarketDepthLevel>();

                foreach (var ask in jAsk)
                {
                    MarketDepthLevel newAsk = new MarketDepthLevel();

                    var str = ask.ToArray();
                    newAsk.Price = str[0].ToString()
                        .Replace("{", "")
                        .Replace("}", "")
                        .ToDecimal();

                    newAsk.Ask = str[1].ToString()
                        .Replace("{", "")
                        .Replace("}", "")
                        .ToDecimal();
                    //newBid.Bid = bid..SelectToken("price").ToString().ToDecimal();
                    Ask.Add(newAsk);
                }

                depth.Asks = Ask;

                if (UpdateMarketDepth != null)
                {
                    UpdateMarketDepth(depth);
                }
            }
        }

        private string GetSecuritiesString(List<Security> securities)
        {
            string res = "";

            for (int i = 0; i < securities.Count; i++)
            {
                res += securities[i].Name;

                if (i + 1 != securities.Count)
                {
                    res += ",";
                }
            }

            return res;
        }

        private DateTime GetDateTime(string str)
        {
            return new DateTime(1970, 1, 1).AddSeconds(Convert.ToInt64(str));
            //var d = (DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds
        }

        public void GetMyOrdersAllInFirstTime()
        {
            List<Order> openOrders = GetMyOpenOrdersFromExmo();

            if (openOrders != null)
            {

                for (int i = 0; i < openOrders.Count; i++)
                {
                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(openOrders[i]);
                    }
                }
            }

            List<Order> closeOrders = GetMyCloseOrdersFromExmo();

            if (closeOrders != null)
            {
                for (int i = 0; i < closeOrders.Count; i++)
                {
                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(closeOrders[i]);
                    }
                }
            }
        }

        private List<Order> GetMyOpenOrdersFromExmo()
        {
            return GetOrdersFromExmo("user_open_orders");
        }

        private List<Order> GetMyCloseOrdersFromExmo()
        {
            return GetOrdersFromExmo("user_cancelled_orders");
        }

        private List<Order> GetOrdersFromExmo(string orderType)
        {
            var jsonCurrency = ApiQuery(orderType, new Dictionary<string, string>());
            var jProperties = JToken.Parse(jsonCurrency).SelectToken("").Children<JProperty>();

            List<Order> orders = new List<Order>();

            foreach (var prop in jProperties)
            {
                string security = prop.Name;

                var orderArray = JToken.Parse(jsonCurrency)[security];

                foreach (var ord in orderArray)
                {
                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = security;
                    newOrder.NumberMarket =
                    ord.SelectToken("order_id").ToString();
                    newOrder.NumberUser = Convert.ToInt32(newOrder.NumberMarket);
                    newOrder.TimeCallBack = GetDateTime(ord.SelectToken("created").ToString());
                    newOrder.Price = ord.SelectToken("price").ToString().ToDecimal();
                    newOrder.Volume = ord.SelectToken("quantity").ToString().ToDecimal();
                    newOrder.ServerType = ServerType.Exmo;

                    string type = ord.SelectToken("type").ToString();

                    if (type == "buy")
                    {
                        newOrder.Side = Side.Buy;
                    }
                    else
                    {
                        newOrder.Side = Side.Sell;
                    }

                    if (orderType == "user_open_orders")
                    {
                        newOrder.State = OrderStateType.Activ;
                    }
                    else
                    {
                        newOrder.State = OrderStateType.Cancel;
                    }

                    decimal executeVolume =
                        ord.SelectToken("amount").ToString().ToDecimal();

                    if (executeVolume == newOrder.Volume)
                    {
                        newOrder.State = OrderStateType.Done;
                    }

                    orders.Add(newOrder);
                    //"order_id": "14",
                    //"created": "1435517311",
                    //"type": "buy",
                    //"pair": "BTC_USD",
                    //"price": "100",
                    //"quantity": "1",
                    //"amount": "100"
                }
            }

            return orders;
        }

        public List<MyTrade> UpdMyTrades(string securities)
        {
            var jsonCurrency = 
                ApiQuery("user_trades", new Dictionary<string, string>(), securities, 30);

            var jProperties = 
                JToken.Parse(jsonCurrency).SelectToken("").Children<JProperty>();

            List<MyTrade> trades = new List<MyTrade>();

            foreach (var prop in jProperties)
            {
                string security = prop.Name;

                var orderArray = JToken.Parse(jsonCurrency)[security];

                foreach (var trade in orderArray)
                {
                    MyTrade newMyTrade = new MyTrade();

                    newMyTrade.SecurityNameCode = security;
                    newMyTrade.Volume 
                        = trade.SelectToken("quantity").ToString().ToDecimal();
                    newMyTrade.Price
                        = trade.SelectToken("price").ToString().ToDecimal();
                    newMyTrade.Time
                        = GetDateTime(trade.SelectToken("date").ToString());

                    string type = trade.SelectToken("type").ToString();

                    if (type == "buy")
                    {newMyTrade.Side = Side.Buy;}
                    else
                    {newMyTrade.Side = Side.Sell; }

                    newMyTrade.NumberTrade = 
                        trade.SelectToken("trade_id").ToString();

                    newMyTrade.NumberOrderParent =
                        trade.SelectToken("order_id").ToString();


                    trades.Add(newMyTrade);
                }
            }

            return trades;
        }

        #endregion

        #region work with orders

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        /// <param name="order"></param>
        public void ExecuteOrder(Order order)
        {

            lock (_lockOrder)
            {
                try
                {
                    if (IsConnected == false)
                    {
                        order.State = OrderStateType.Fail;

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }

                        return;
                    }

                    /* pair - валютная пара
                     quantity - кол - во по ордеру
                     price - цена по ордеру
                     type - тип ордера, может принимать следующие значения:
                     buy - ордер на покупку
                     sell - ордер на продажу*/

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("pair", order.SecurityNameCode);
                    param.Add("type", order.Side == Side.Buy ? "buy" : "sell");
                    param.Add("quantity",
                        order.Volume.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));
                    param.Add("price",
                        order.Price.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));

                    var jsonCurrency = ApiQuery("order_create", param);

                    string res =
                        JToken.Parse(jsonCurrency).SelectToken("result").ToString();

                    if (Convert.ToBoolean(res.ToLower()) != true)
                    {
                        order.State = OrderStateType.Fail;

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }

                        string error =
                            JToken.Parse(jsonCurrency).SelectToken("error").ToString();

                        SendLogMessage("Exmo order execution error. " + error, LogMessageType.Error);

                        return;
                    }

                    string id =
                        JToken.Parse(jsonCurrency).SelectToken("order_id").ToString();

                    order.NumberMarket = id;
                    order.State = OrderStateType.Activ;

                    /* "result": true,
                     "error": "",
                     "order_id": 123456*/

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }

                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private object _lockOrder = new object();

        /// <summary>
        /// cancel order
        /// отменить ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    if (IsConnected == false)
                    {
                        SendLogMessage("Cansel order fail. Exchange isnt work",LogMessageType.Error);
                        return;
                    }

                    /* pair - валютная пара
                     quantity - кол - во по ордеру
                     price - цена по ордеру
                     type - тип ордера, может принимать следующие значения:
                     buy - ордер на покупку
                     sell - ордер на продажу*/

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("order_id", order.NumberMarket);

                    ApiQuery("order_cancel", param);

                    order.State = OrderStateType.Cancel;

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }

                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);

                }
            }
        }

        /// <summary>
        /// chack order state
        /// проверить ордера на состояние
        /// </summary>
        public bool GetAllOrders(List<Order> oldOpenOrders)
        {

            return false;
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
        /// portfolios updated
        /// обновились портфели
        /// </summary>
        public event Action<Portfolio> UpdatePortfolio;

        /// <summary>
        /// new security in the system
        /// новые бумаги в системе
        /// </summary>
        public event Action<List<Security>> UpdatePairs;

        /// <summary>
        /// depth updated
        /// обновился стакан
        /// </summary>
        public event Action<MarketDepth> UpdateMarketDepth;

        /// <summary>
        /// ticks updated
        /// обновились тики
        /// </summary>
        public event Action<List<Trade>> NewTradesEvent;

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
    }

    public static class Helpers
    {
        public static NameValueCollection ToNameValueCollection<TKey, TValue>(this IDictionary<TKey, TValue> dict)
        {
            var nameValueCollection = new NameValueCollection();

            foreach (var kvp in dict)
            {
                string value = string.Empty;
                if (kvp.Value != null)
                    value = kvp.Value.ToString();

                nameValueCollection.Add(kvp.Key.ToString(), value);
            }

            return nameValueCollection;
        }

    }
}
