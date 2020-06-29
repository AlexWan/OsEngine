/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Tinkoff
{
    public class TinkoffClient
    {
        public TinkoffClient(string token)
        {
            _token = token;

            Task worker = new Task(WorkerPlace);
            worker.Start();

            _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(500));
        }

        RateGate _rateGate;

        public string _token;

        private string _url = "https://api-invest.tinkoff.ru/openapi/";

       // private string _urlWebSocket = "wss://api-invest.tinkoff.ru/openapi/md/v1/md-openapi/ws";

        /// <summary>
        /// connecto to the exchange
        /// установить соединение с биржей 
        /// </summary>
        public void Connect()
        {
            if (string.IsNullOrEmpty(_token))
            {
                return;
            }

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

        private object _queryLocker = new object();

        public string ApiQuery(string url, string type, IDictionary<string, string> req)
        {
            try
            {
                lock (_queryLocker)
                {
                    _rateGate.WaitToProceed();

                    using (var wb = new WebClient())
                    {
                        wb.Headers.Add("Authorization", "Bearer " + _token);

                        byte[] response;

                        if (type == "POST" && req.Count != 0)
                        {
                            string str = "{";
                            bool isFirst = true;

                            foreach (var r in req)
                            {
                                if (isFirst == false)
                                {
                                    str += ",";
                                }

                                isFirst = false;

                                str += "\"" + r.Key + "\":";

                                try
                                {
                                    r.Value.ToDecimal();
                                    str += r.Value.Replace(",", ".");
                                }
                                catch
                                {
                                    str += "\"" + r.Value + "\"";
                                }
                            }
                            str += "}";

                            byte[] postData = Encoding.UTF8.GetBytes(str);

                            //MessageBox.Show(str);
                            response = wb.UploadData(url, type, postData);
                        }
                        else // if(type == "GET")
                        {
                            response = wb.DownloadData(url);
                        }

                        return Encoding.UTF8.GetString(response);
                    }
                }
            }
            catch (Exception error)
            {
                // MessageBox.Show(error.ToString());
                return null;
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

        #region Подписка на дату

        List<Security> _securitiesSubscrible = new List<Security>();

        public void SubscribleDepths(Security security)
        {
            if (_securitiesSubscrible.Find(s => s.Name == security.Name) != null)
            {
                return;
            }
            _securitiesSubscrible.Add(security);
        }

        private DateTime _lastBalanceUpdTime;

        private async void WorkerPlace()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(5000);

                    if (_isDisposed)
                    {
                        return;
                    }

                    if (IsConnected == false)
                    {
                        continue;
                    }

                    if (_portfolios != null &&
                        _lastBalanceUpdTime.AddSeconds(10) < DateTime.Now)
                    {
                        UpdBalance();
                        UpdPositions();
                        UpdateMyTrades();
                        _lastBalanceUpdTime = DateTime.Now;
                    }

                    for (int i = 0; i < _securitiesSubscrible.Count; i++)
                    {
                        GetMarketDepth(_securitiesSubscrible[i]);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    await Task.Delay(5000);
                }
            }
        }

        #endregion

        #region Запросы

        private List<Portfolio> _portfolios = new List<Portfolio>();

        public void UpdBalance()
        {
            try
            {
                string url = _url + "/portfolio/currencies";

                var jsonCurrency = ApiQuery(url, "GET", new Dictionary<string, string>());

                if (jsonCurrency == null)
                {
                    return;
                }

                var jProperties = JToken.Parse(jsonCurrency).SelectToken("payload");//.Children<JProperty>();
                var children = jProperties.Children().Children().Children();

                foreach (var portfolio in children)
                {
                    Portfolio newPortfolio = new Portfolio();

                    newPortfolio.Number = portfolio.SelectToken("currency").ToString();

                    newPortfolio.ValueBegin = portfolio.SelectToken("balance").ToString().ToDecimal();
                    newPortfolio.ValueCurrent = newPortfolio.ValueBegin;

                    Portfolio oldPortfolio =
                        _portfolios.Find(p => p.Number == newPortfolio.Number);
                    if (oldPortfolio == null)
                    {
                        _portfolios.Add(newPortfolio);
                    }
                    else
                    {

                        oldPortfolio.ValueCurrent = newPortfolio.ValueCurrent;
                    }
                }


                if (UpdatePortfolio != null)
                {
                    for (int i = 0; i < _portfolios.Count; i++)
                    {
                        UpdatePortfolio(_portfolios[i]);
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        public void UpdPositions()
        {
            string url = _url + "portfolio";

            var jsonCurrency = ApiQuery(url, "GET", new Dictionary<string, string>());

            if (jsonCurrency == null)
            {
                return;
            }
            var jProperties = JToken.Parse(jsonCurrency).SelectToken("payload");//.Children<JProperty>();
            var children = jProperties.Children().Children().Children();

            string res = children.ToString();

            if (res == "")
            {
                return;
            }

            foreach (var position in children)
            {
                PositionOnBoard pos = new PositionOnBoard();

                pos.SecurityNameCode = position.SelectToken("ticker").ToString();
                pos.ValueCurrent = position.SelectToken("balance").ToString().ToDecimal();
                pos.PortfolioName = position.SelectToken("expectedYield").SelectToken("currency").ToString();

                Portfolio myPortfolio =
                    _portfolios.Find(p => p.Number == pos.PortfolioName);

                if (myPortfolio == null)
                {
                    continue;
                }

                myPortfolio.SetNewPosition(pos);
            }


            if (UpdatePortfolio != null)
            {
                for (int i = 0; i < _portfolios.Count; i++)
                {
                    UpdatePortfolio(_portfolios[i]);
                }
            }
        }

        public void GetSecurities()
        {
            List<Security> securities = new List<Security>();

            securities.AddRange(GetSecurities(_url + "market/stocks", SecurityType.Stock));
            securities.AddRange(GetSecurities(_url + "market/bonds", SecurityType.Bond));
            securities.AddRange(GetSecurities(_url + "market/etfs", SecurityType.Stock));
            securities.AddRange(GetSecurities(_url + "market/currencies", SecurityType.CurrencyPair));

            if (UpdatePairs != null)
            {
                UpdatePairs(securities);
            }

            _allSecurities = securities;
        }

        private List<Security> _allSecurities = new List<Security>();

        private List<Security> GetSecurities(string url, SecurityType type)
        {
            string secClass = type.ToString();

            var jsonCurrency = ApiQuery(url, "GET", new Dictionary<string, string>());

            if (jsonCurrency == null)
            {
                return null;
            }

            var jProperties = JToken.Parse(jsonCurrency).SelectToken("payload").Children().Children().Children();

            List<Security> securities = new List<Security>();

            foreach (var jtSecurity in jProperties)
            {
                Security newSecurity = new Security();

                try
                {
                    newSecurity.Name = jtSecurity.SelectToken("ticker").ToString();
                    newSecurity.NameId = jtSecurity.SelectToken("figi").ToString();
                    newSecurity.NameFull = jtSecurity.SelectToken("name").ToString();
                    string str = jtSecurity.ToString();

                    if (str.Contains("minPriceIncrement"))
                    {
                        newSecurity.PriceStep = jtSecurity.SelectToken("minPriceIncrement").ToString().ToDecimal();
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                }
                catch
                {
                    continue;
                }

                if (secClass == "Stock" &&
                    jtSecurity.SelectToken("currency").ToString() == "RUB")
                {
                    newSecurity.NameClass = secClass + " Ru";
                }
                else if (secClass == "Stock" &&
                         (jtSecurity.SelectToken("currency").ToString() == "EUR" ||
                          jtSecurity.SelectToken("currency").ToString() == "USD"))
                {
                    newSecurity.NameClass = secClass + " US";
                }
                else
                {
                    newSecurity.NameClass = secClass;
                }

                newSecurity.SecurityType = type;
                newSecurity.Lot = jtSecurity.SelectToken("lot").ToString().ToDecimal();

                securities.Add(newSecurity);
            }

            return securities;
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeFrame tf, DateTime from, DateTime to)
        {
            List<Candle> candles = new List<Candle>();
            while (from <= to)
            {
                candles.AddRange(GetCandleHistoryFromDay(from, nameSec, tf));

                from = from.AddDays(1);
            }

            return candles;
        }

        private List<Candle> GetCandleHistoryFromDay(DateTime time, string nameSec, TimeFrame tf)
        {
            string url = CreateCandleUrl(
                nameSec,
                new DateTime(time.Year, time.Month, time.Day, 1, 0, 0),
                new DateTime(time.Year, time.Month, time.Day, 23, 55, 0),
                tf);

            var jsonCurrency = ApiQuery(url, "GET", new Dictionary<string, string>());


            if (jsonCurrency == null)
            {
                return null;
            }

            var jProperties = JToken.Parse(jsonCurrency).SelectToken("payload").Children().Children().Children();

            List<Candle> candles = new List<Candle>();

            foreach (var jtSecurity in jProperties)
            {
                Candle newCandle = new Candle();

                newCandle.TimeStart = FromIso8601(jtSecurity.SelectToken("time").ToString());

                newCandle.Open = jtSecurity.SelectToken("o").ToString().ToDecimal();
                newCandle.High = jtSecurity.SelectToken("h").ToString().ToDecimal();
                newCandle.Low = jtSecurity.SelectToken("l").ToString().ToDecimal();
                newCandle.Close = jtSecurity.SelectToken("c").ToString().ToDecimal();
                newCandle.Volume = jtSecurity.SelectToken("v").ToString().ToDecimal();


                candles.Add(newCandle);
            }

            return candles;
        }

        private string CreateCandleUrl(string figi, DateTime from, DateTime to, TimeFrame tf)
        {
            string result = "https://api-invest.tinkoff.ru/openapi/market/candles?";

            result += "figi=" + figi + "&";
            result += "from=" + ToIso8601(from) + "&";
            result += "to=" + ToIso8601(to) + "&";
            result += "interval=";

            if (tf == TimeFrame.Min1)
            {
                result += "1min";
            }
            else if (tf == TimeFrame.Min2)
            {
                result += "2min";
            }
            else if (tf == TimeFrame.Min3)
            {
                result += "3min";
            }
            else if (tf == TimeFrame.Min5)
            {
                result += "5min";
            }
            else if (tf == TimeFrame.Min10)
            {
                result += "10min";
            }
            else if (tf == TimeFrame.Min15)
            {
                result += "15min";
            }
            else if (tf == TimeFrame.Min30)
            {
                result += "30min";
            }
            else if (tf == TimeFrame.Hour1)
            {
                result += "hour";
            }
            else if (tf == TimeFrame.Day)
            {
                result += "day";
            }

            // Available values : 1min, 2min, 3min, 5min, 10min, 15min, 30min, hour, day, week, month

            //"curl -X GET "https://api-invest.tinkoff.ru/openapi/market/candles?figi=BBG000BBJQV0&from=2019-10-21T10%3A00%3A00.0%2B00%3A00&to=2019-10-21T23%3A55%3A00.0%2B00%3A00&interval=5min"
            //-H "accept: application/json" -H "Authorization: Bearer t.ZmEaoirPe5unR6Cw0o7YSq-Hl4lCkGES-0XgZmOg9XGl_Ds6OeAzdS9P-x1lRmQjzu7Ol6cMTgN-QUv9ISvyGQ"

            return result;
        }

        private string ToIso8601(DateTime time)
        {
            // 2019-10-21T10:00:00.0+00:00
            // 2019-10-21T10%3A00%3A00.0%2B00%3A00

            // 2019-10-21T23:55:00.0+00:00
            // 2019-10-21T23%3A55%3A00.0%2B00%3A00&
            string result = "";

            result += time.Year + "-";

            if (time.Month < 10)
            {
                result += "0" + time.Month + "-";
            }
            else
            {
                result += time.Month + "-";
            }

            if (time.Day < 10)
            {
                result += "0" + time.Day + "T";
            }
            else
            {
                result += time.Day + "T";
            }

            if (time.Hour < 10)
            {
                result += "0" + time.Hour + "%3A";
            }
            else
            {
                result += time.Hour + "%3A";
            }

            if (time.Minute < 10)
            {
                result += "0" + time.Minute + "%3A";
            }
            else
            {
                result += time.Minute + "%3A";
            }

            if (time.Second < 10)
            {
                result += "0" + time.Second + ".0%2B03%3A00";
            }
            else
            {
                result += time.Second + ".0%2B03%3A00";
            }

            return result;
        }

        private DateTime FromIso8601(string str)
        {
            DateTime time = Convert.ToDateTime(str);

            return time;
        }

        public void GetMarketDepth(Security security)
        {
            if (_securitiesSubscrible.Count == 0)
            {
                return;
            }

            //https://api-invest.tinkoff.ru/openapi/market/orderbook?figi=BBG001DJNR51&depth=10

            string url = _url + "market/orderbook?";
            url += "figi=" + security.NameId;
            url += "&depth=7";

            var jsonCurrency = ApiQuery(url, "GET", new Dictionary<string, string>());


            if (jsonCurrency == null)
            {
                return;
            }

            MarketDepth depth = new MarketDepth();
            depth.SecurityNameCode = security.Name;
            depth.Time = DateTime.Now;
            //  var time = JToken.Parse(jsonCurrency).SelectToken("payload").SelectToken("time");

            var jBid = JToken.Parse(jsonCurrency).SelectToken("payload").SelectToken("bids");

            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            foreach (var bid in jBid)
            {
                MarketDepthLevel newBid = new MarketDepthLevel();

                newBid.Bid = bid.SelectToken("quantity").ToString().ToDecimal();
                newBid.Price = bid.SelectToken("price").ToString().ToDecimal();

                bids.Add(newBid);
            }

            depth.Bids = bids;


            var jAsk = JToken.Parse(jsonCurrency).SelectToken("payload").SelectToken("asks");

            List<MarketDepthLevel> asks = new List<MarketDepthLevel>();

            foreach (var ask in jAsk)
            {
                MarketDepthLevel newAsk = new MarketDepthLevel();

                newAsk.Ask = ask.SelectToken("quantity").ToString().ToDecimal();
                newAsk.Price = ask.SelectToken("price").ToString().ToDecimal();

                asks.Add(newAsk);
            }

            depth.Asks = asks;

            if (depth.Asks == null ||
                depth.Asks.Count == 0 ||
                depth.Bids == null ||
                depth.Bids.Count == 0)
            {
                return;
            }

            if (UpdateMarketDepth != null)
            {
                UpdateMarketDepth(depth);
            }
        }

        private List<MyTrade> _myTrades = new List<MyTrade>();

        private List<Order> _canselOrders = new List<Order>();

        public void UpdateMyTrades()
        {
            try
            {

                //curl - X GET "https://api-invest.tinkoff.ru/openapi/operations?from=2019-11-01T01%3A00%3A00.0%2B00%3A00&to=2019-11-01T23%3A50%3A00.0%2B00%3A00"
                //- H "accept: application/json"
                //- H "Authorization: Bearer t.ZmEaoirPe5unR6Cw0o7YSq-Hl4lCkGES-0XgZmOg9XGl_Ds6OeAzdS9P-x1lRmQjzu7Ol6cMTgN-QUv9ISvyGQ"

                DateTime now = DateTime.Now;
                DateTime from = new DateTime(now.Year, now.Month, now.Day, 1, 0, 0);
                DateTime to = new DateTime(now.Year, now.Month, now.Day, 23, 50, 0);

                string url = _url + "operations?";
                url += "from=" + ToIso8601(from) + "&";
                url += "to=" + ToIso8601(to) + "&";


                var jsonCurrency = ApiQuery(url, "GET", new Dictionary<string, string>());

                if (jsonCurrency == null)
                {
                    return;
                }

                var jOrders = JToken.Parse(jsonCurrency).SelectToken("payload").SelectToken("operations");//.Children<JProperty>();

                foreach (var order in jOrders)
                {
                    Order newOrder = new Order();
                    newOrder.NumberMarket = order.SelectToken("id").ToString();
                    string state = order.SelectToken("status").ToString();

                    if (state == "Decline")
                    {
                        newOrder.State = OrderStateType.Cancel;

                        if (_canselOrders.Find(ord => ord.NumberMarket == newOrder.NumberMarket) == null &&
                            MyOrderEvent != null)
                        {
                            string figa = order.SelectToken("figi").ToString();
                            Security security = _allSecurities.Find(sec => sec.NameId == figa);
                            if (security != null)
                            {
                                newOrder.SecurityNameCode = security.Name;
                            }

                            _canselOrders.Add(newOrder);
                            MyOrderEvent(newOrder);
                        }
                    }

                    var jTrades = order.SelectToken("trades");
                    if (jTrades != null)
                    {
                        foreach (var trade in jTrades)
                        {
                            string num = trade.SelectToken("tradeId").ToString();

                            if (_myTrades.Find(t => t.NumberTrade == num) == null && MyTradeEvent != null)
                            {
                                // "operationType": "Sell",
                                MyTrade myTrade = new MyTrade();
                                myTrade.NumberTrade = num;
                                myTrade.NumberOrderParent = newOrder.NumberMarket;
                                myTrade.Price = trade.SelectToken("price").ToString().ToDecimal();
                                myTrade.Volume = trade.SelectToken("quantity").ToString().ToDecimal();
                                myTrade.Time = Convert.ToDateTime(trade.SelectToken("date").ToString());
                                Enum.TryParse(order.SelectToken("operationType").ToString(), out myTrade.Side);

                                string figa = order.SelectToken("figi").ToString();
                                Security security = _allSecurities.Find(sec => sec.NameId == figa);
                                if (security != null)
                                {
                                    myTrade.SecurityNameCode = security.Name;
                                }

                                MyTradeEvent(myTrade);
                                _myTrades.Add(myTrade);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
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


                    string request = "";

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("lots", order.Volume.ToString());
                    param.Add("operation", order.Side.ToString());
                    param.Add("price", order.Price.ToString());

                    // curl - X POST "https://api-invest.tinkoff.ru/openapi/orders/limit-order?figi=BBG004730N88" -
                    // H "accept: application/json" - H "Authorization: Bearer t.ZmEaoirPe5unR6Cw0o7YSq-Hl4lCkGES-0XgZmOg9XGl_Ds6OeAzdS9P-x1lRmQjzu7Ol6cMTgN-QUv9ISvyGQ"
                    // - 
                    //H "Content-Type: application/json" - d "{\"lots\":1,\"operation\":\"Buy\",\"price\":230}"

                    Security security = _allSecurities.Find(sec => sec.Name == order.SecurityNameCode);

                    if (security == null)
                    {
                        return;
                    }

                    string url = _url + "orders/limit-order?figi=" + security.NameId;

                    var jsonCurrency = ApiQuery(url, "POST", param);


                    if (jsonCurrency == null)
                    {
                        order.State = OrderStateType.Fail;


                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }

                        return;
                    }

                    var jorder = JToken.Parse(jsonCurrency).SelectToken("payload");

                    order.NumberMarket = jorder.SelectToken("orderId").ToString();
                    //order.NumberUser = Convert.ToInt32(Convert.ToInt64(order.NumberMarket) - 100000000);
                    order.State = OrderStateType.Activ;
                    order.TimeCallBack = DateTime.Now;


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
                        SendLogMessage("Cansel order fail. Exchange isnt work", LogMessageType.Error);
                        return;
                    }

                    //curl -X POST "https://api-invest.tinkoff.ru/openapi/orders/cancel?orderId=18846767867" -H "accept: application/json"
                    //-H "Authorization: Bearer t.ZmEaoirPe5unR6Cw0o7YSq-Hl4lCkGES-0XgZmOg9XGl_Ds6OeAzdS9P-x1lRmQjzu7Ol6cMTgN-QUv9ISvyGQ"
                    //-H "Content-Type: application/json"

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("trackingId", "string");

                    string url = _url + "orders/cancel?orderId=" + order.NumberMarket;

                    string result = ApiQuery(url, "POST", param);

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
