/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitStamp.BitStampEntity;
using RestSharp;
using Trade = OsEngine.Entity.Trade;
using Newtonsoft.Json.Linq;


namespace OsEngine.Market.Servers.BitStamp
{

    /// <summary>
    /// client for access to cryptocurrency exchange BitStamp
    /// клиент доступа к бирже криптовалют BitStamp
    /// </summary>
    public class BitstampClient
    {

        // service 
        // сервис

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="apiKey">API-key / ключ для апи</param>
        /// <param name="apiSecret">Secret key / секретный ключ</param>
        /// <param name="clientId">client id / Id клиента</param>
        public BitstampClient(string apiKey, string apiSecret, string clientId)
        {
            _requestAuthenticator = new RequestAuthenticator(apiKey, apiSecret, clientId);

            _apiKeyPublic = apiKey;
            _apiKeySecret = apiSecret;
            _clientId = clientId;

            Thread worker = new Thread(ThreadListenDataArea);
            worker.IsBackground = true;
            worker.Start();
        }

        /// <summary>
        /// public API-key
        /// публичный ключ для апи
        /// </summary>
        private string _apiKeyPublic;

        /// <summary>
        /// secret API-key
        /// секретный ключ для апи
        /// </summary>
        private string _apiKeySecret;

        /// <summary>
        /// client number on the exchange
        /// номер клиента на бирже
        /// </summary>
        private string _clientId;

        /// <summary>
        /// object is responsible for the correct headers and tags when sending messages with using HTTP
        /// объект отвечающий за правильные заголовки и метки при отправке сообщений через HTTP
        /// </summary>
        private readonly RequestAuthenticator _requestAuthenticator;

        /// <summary>
        /// connect to exchange
        /// установить соединение с биржей 
        /// </summary>
        public void Connect()
        {
            if (string.IsNullOrWhiteSpace(_apiKeyPublic) ||
                string.IsNullOrWhiteSpace(_apiKeySecret) ||
                string.IsNullOrWhiteSpace(_clientId))
            {
                return;
            }
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            // check server availability for HTTP communication with it / проверяем доступность сервера для HTTP общения с ним
            Uri uri = new Uri("https://www.bitstamp.net");
            try
            {
                HttpWebRequest httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(uri);

                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch
            {
                LogMessageEvent("Сервер не доступен. Отсутствуюет интернет. ", LogMessageType.Error);
                return;
            }

            IsConnected = true;

            if (Connected != null)
            {
                Connected();
            }

            // start stream data through WebSocket / запускаем потоковые данные через WebSocket

            _ws = new ClientWebSocket();

            Uri wsUri = new Uri("wss://ws.bitstamp.net");
            _ws.ConnectAsync(wsUri, CancellationToken.None).Wait();

            if (_ws.State == WebSocketState.Open)
            {
                if (Connected != null)
                {
                    Connected.Invoke();
                }
                IsConnected = true;
            }

            Thread worker = new Thread(GetRes);
            worker.CurrentCulture = new CultureInfo("ru-RU");
            worker.IsBackground = true;
            worker.Start(_ws);

            Thread converter = new Thread(Converter);
            converter.CurrentCulture = new CultureInfo("ru-RU");
            converter.IsBackground = true;
            converter.Start();

        }

        ClientWebSocket _ws;

        /// <summary>
        /// queue of new messages from the exchange server
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();

        private void GetRes(object clientWebSocket)
        {
            ClientWebSocket ws = (ClientWebSocket)clientWebSocket;

            string res = "";

            while (true)
            {
                if (_isDisposed)
                {
                    return;
                }
                try
                {
                    if (_ws.State == WebSocketState.CloseReceived)
                    {
                        Thread.Sleep(10000);

                        if (_ws.State == WebSocketState.CloseReceived)
                        {
                            IsConnected = false;
                            Disconnected();
                            return;
                        }

                    }

                    if (ws.State != WebSocketState.Open)
                    {
                        continue;
                    }

                    if (IsConnected)
                    {
                        var buffer = new ArraySegment<byte>(new byte[1024]);
                        var result = ws.ReceiveAsync(buffer, CancellationToken.None).Result;

                        if (result.Count == 0)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        if (result.EndOfMessage == false)
                        {
                            res += Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                        }
                        else
                        {
                            res += Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                            _newMessage.Enqueue(res);
                            res = "";
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void Converter()
        {
            while (true)
            {
                if (_isDisposed)
                {
                    return;
                }
                try
                {
                    if (!_newMessage.IsEmpty)
                    {
                        string mes;

                        if (_newMessage.TryDequeue(out mes))
                        {
                            List<string> values = new List<string>();
                            int firstInd = 0;
                            for (int i = 1; i < mes.Length - 1; i++)
                            {
                                if (string.Equals((mes[i].ToString() + mes[i + 1].ToString()), "}{"))
                                {
                                    String newString = mes.Substring(firstInd, i - firstInd + 1);
                                    firstInd = i + 1;
                                    values.Add(newString);
                                }
                            }

                            String lastString = mes.Substring(firstInd, mes.Length - firstInd);
                            values.Add(lastString);

                            for (int i = 0; i < values.Count; i++)
                            {
                                ParseMessage(values[i]);
                            }

                        }

                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        string _orderBookStr = "order_book";
        string _tradesStr = "live_trades";

        private void ParseMessage(string mes)
        {
            mes = "[" + mes + "]";
            var jProperties = JToken.Parse(mes);

            foreach (var data in jProperties)
            {
                BitstampData d = new BitstampData();
                d.Event = data.SelectToken("event").ToString();
                d.channel = data.SelectToken("channel").ToString();
                d.data = data.SelectToken("data").ToString();


                if (d.data.Length < 10)
                {
                    return;
                }

                if (d.channel.StartsWith(_orderBookStr))
                {
                    OrderBook_Income(d.channel.Split('_')[2], d.data);
                }
                if (d.channel.StartsWith(_tradesStr))
                {
                    Trades_Income(d.channel.Split('_')[2], d.data);
                }
            }
        }

        /// <summary>
        /// bring the program to the start. Clear all objects involved in connecting to the server
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        public void Dispose()
        {
            IsConnected = false;

            _requestAuthenticator.Dispose();

            if (_ws != null)
            {
                _ws.Abort();
            }

            if (Disconnected != null)
            {
                Disconnected();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// shows whether connection works
        /// работает ли соединение
        /// </summary>
        public bool IsConnected;

        // stream data from WEBSOCKET
        // потоковые данные из WEBSOCKET


        /// <summary>
        /// subscribed to depths and trades securities
        /// бумаги уже подписаные на получение стаканов и трейдов
        /// </summary>
        private List<Security> _subscribedSec = new List<Security>();

        /// <summary>
        /// subscribe this security to get depths and trades
        /// подписать данную бумагу на получение стаканов и трейдов
        /// </summary>
        public void SubscribleTradesAndDepths(Security security)
        {
            if (_subscribedSec.Find(s => s.Name == security.Name) == null)
            {
                _subscribedSec.Add(security);

                string str1 = "{\"event\": \"bts:subscribe\",\"data\": {\"channel\": \"";

                string channelTrades = str1 + "live_trades_" + security.Name + "\"}}"; //[currency_pair]
                string depthTrades = str1 + "order_book_" + security.Name + "\"}}"; //[currency_pair]

                var reqAsBytes = Encoding.UTF8.GetBytes(channelTrades);
                var ticksRequest = new ArraySegment<byte>(reqAsBytes);

                _ws.SendAsync(ticksRequest,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None).Wait();


                var reqAsBytes2 = Encoding.UTF8.GetBytes(depthTrades);
                var depthRequest = new ArraySegment<byte>(reqAsBytes2);

                _ws.SendAsync(depthRequest,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None).Wait();


                /*  Channel channelTrades = null;

                  if (security.Name == "btcusd")
                  {
                      channelTrades = _pusher.Subscribe("live_trades");
                  }
                  else
                  {
                      channelTrades = _pusher.Subscribe("live_trades_" + security.Name);
                  }

                  if (security.Name == "btcusd")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("btcusd", data.ToString()); }); }
                  else if (security.Name == "btceur")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("btceur", data.ToString()); }); }
                  else if (security.Name == "eurusd")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("eurusd", data.ToString()); }); }
                  else if (security.Name == "xrpusd")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("xrpusd", data.ToString()); }); }
                  else if (security.Name == "xrpeur")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("xrpeur", data.ToString()); }); }
                  else if (security.Name == "xrpbtc")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("xrpbtc", data.ToString()); }); }
                  else if (security.Name == "ltcusd")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("ltcusd", data.ToString()); }); }
                  else if (security.Name == "ltceur")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("ltceur", data.ToString()); }); }
                  else if (security.Name == "ltcbtc")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("ltcbtc", data.ToString()); }); }
                  else if (security.Name == "ethusd")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("ethusd", data.ToString()); }); }
                  else if (security.Name == "etheur")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("etheur", data.ToString()); }); }
                  else if (security.Name == "ethbtc")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("ethbtc", data.ToString()); }); }
                  else if (security.Name == "bcheur")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("bcheur", data.ToString()); }); }
                  else if (security.Name == "bchusd")
                  { channelTrades.Bind("trade", (dynamic data) => { Trades_Income("bchusd", data.ToString()); }); }

                  Channel channelBook = null;

                  if (security.Name == "btcusd")
                  {
                      channelBook = _pusher.Subscribe("order_book");
                  }
                  else
                  {
                      channelBook = _pusher.Subscribe("order_book_" + security.Name);
                  }


                  if (security.Name == "btcusd")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("btcusd", data.ToString()); }); }
                  else if (security.Name == "btceur")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("btceur", data.ToString()); }); }
                  else if (security.Name == "eurusd")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("eurusd", data.ToString()); }); }
                  else if (security.Name == "xrpusd")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("xrpusd", data.ToString()); }); }
                  else if (security.Name == "xrpeur")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("xrpeur", data.ToString()); }); }
                  else if (security.Name == "xrpbtc")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("xrpbtc", data.ToString()); }); }
                  else if (security.Name == "ltcusd")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("ltcusd", data.ToString()); }); }
                  else if (security.Name == "ltceur")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("ltceur", data.ToString()); }); }
                  else if (security.Name == "ltcbtc")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("ltcbtc", data.ToString()); }); }
                  else if (security.Name == "ethusd")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("ethusd", data.ToString()); }); }
                  else if (security.Name == "etheur")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("etheur", data.ToString()); }); }
                  else if (security.Name == "ethbtc")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("ethbtc", data.ToString()); }); }
                  else if (security.Name == "bcheur")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("bcheur", data.ToString()); }); }
                  else if (security.Name == "bchusd")
                  { channelBook.Bind("data", (dynamic data) => { OrderBook_Income("bchusd", data.ToString()); }); }
                  */
            }
        }

        /// <summary>
        /// incoming depth
        /// входящий стакан
        /// </summary>
        /// <param name="namePaper">instrument name / название инструмента</param>
        /// <param name="message">depth / стакан</param>
        private void OrderBook_Income(string namePaper, string message)
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                var resp = JsonConvert.DeserializeObject<OrderBookResponse>(message);

                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = namePaper;

                for (int i = 0; i < 10; i++)
                {
                    MarketDepthLevel ask = new MarketDepthLevel();
                    ask.Price = resp.asks[i][0].ToDecimal();
                    ask.Ask = resp.asks[i][1].ToDecimal();
                    depth.Asks.Add(ask);

                    MarketDepthLevel bid = new MarketDepthLevel();
                    bid.Price = resp.bids[i][0].ToDecimal();
                    bid.Bid = resp.bids[i][1].ToDecimal();
                    depth.Bids.Add(bid);
                }

                if (UpdateMarketDepth != null)
                {
                    UpdateMarketDepth(depth);
                }
            }
            catch (Exception error)
            {
                if (LogMessageEvent != null)
                {
                    //   LogMessageEvent(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// incoming depth
        /// входящий стакан
        /// </summary>
        /// <param name="namePaper">security name/название инструмента</param>
        /// <param name="message">trade/трейд</param>
        private void Trades_Income(string namePaper, string message)
        {
            if (_isDisposed)
            {
                return;
            }
            try
            {
                var resp = JsonConvert.DeserializeObject<TradeResponse>(message);

                Trade trade = new Trade();
                trade.SecurityNameCode = namePaper;
                trade.Price = resp.price.ToDecimal();
                trade.Volume = resp.amount.ToDecimal();
                trade.Id = resp.id;
                trade.Time = (new DateTime(1970, 1, 1, 0, 0, 0, 0)).AddSeconds(Convert.ToInt32(resp.timestamp));

                if (resp.type == "0")
                {
                    trade.Side = Side.Buy;
                }
                else
                {
                    trade.Side = Side.Sell;
                }

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(trade);
                }
            }
            catch (Exception error)
            {

                if (LogMessageEvent != null)
                {
                    // LogMessageEvent(error.ToString(),LogMessageType.Error);
                }
            }
        }

        // listening thread of data throw HTTP
        // поток прослушки данных через HTTP

        private RestRequest GetAuthenticatedRequest(Method method)
        {
            var request = new RestRequest(method);
            _requestAuthenticator.Authenticate(request);
            return request;
        }

        private object _lock = new object();

        /// <summary>
        /// request to clear the object
        /// произошёл запрос на очистку объекта
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// work place of the thread that downloads data from API
        /// место работы потока который скачивает данные из апи
        /// </summary>
        private void ThreadListenDataArea()
        {
            DateTime lastTimeGetPortfolios = DateTime.MinValue;
            DateTime lastTimeGetOrders = DateTime.MinValue;

            while (true)
            {
                try
                {
                    Thread.Sleep(1);
                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (_isDisposed)
                    {
                        return;
                    }

                    if (IsConnected == false)
                    {
                        continue;
                    }


                    // once every ten seconds we request the portfolio and its components / раз в десять секунд запрашиваем портфель и его составляющие
                    if (lastTimeGetPortfolios.AddSeconds(10) < DateTime.Now)
                    {
                        lastTimeGetPortfolios = DateTime.Now;
                        GetBalance();
                    }

                    // once a second we update the data on our orders / раз секунду обновляем данные по своим ордерам
                    if (lastTimeGetOrders.AddSeconds(1) < DateTime.Now)
                    {
                        lastTimeGetOrders = DateTime.Now;
                        GetOrders();
                    }

                }
                catch (Exception error)
                {
                    if (LogMessageEvent != null)
                    {
                        LogMessageEvent(error.ToString(), LogMessageType.Error);
                    }
                }
            }
        }

        /// <summary>
        /// take balance
        /// взять баланс
        /// </summary>
        public BalanceResponse GetBalance()
        {
            lock (_lock)
            {
                try
                {
                    var request = GetAuthenticatedRequest(Method.POST);
                    var response = new RestClient("https://www.bitstamp.net/api/v2/balance/").Execute(request);

                    if (response.Content.Contains("error"))
                    {
                        SendLogMessage("BitStamp Response Error " + response.Content, LogMessageType.Error);

                        return null;
                    }

                    if (UpdatePortfolio != null)
                    {
                        UpdatePortfolio(JsonConvert.DeserializeObject<BalanceResponse>(response.Content));
                    }

                    return JsonConvert.DeserializeObject<BalanceResponse>(response.Content);
                }
                catch (Exception ex)
                {
                    LogMessageEvent(ex.ToString(), LogMessageType.Error);
                    return null;
                }
            }
        }

        private List<Order> _ordersCanseled = new List<Order>();

        /// <summary>
        /// update order data
        /// обновить данные по ордерам
        /// </summary>
        private void GetOrders()
        {

            for (int i = 0; i < _osEngineOrders.Count; i++)
            {
                Thread.Sleep(300);

                if (_ordersCanseled.Find(ord => ord.NumberMarket == _osEngineOrders[i].NumberMarket) != null)
                {
                    _osEngineOrders.RemoveAt(i);
                    i--;
                    continue;
                }

                Order order = _osEngineOrders[i];

                OrderStatusResponse response = GetOrderStatus(order.NumberMarket);

                if (response.transactions != null &&
                    response.transactions.Length != 0)
                {
                    MyTrade trade = new MyTrade();
                    trade.NumberOrderParent = order.NumberMarket;
                    trade.NumberTrade = response.transactions[0].tid;
                    trade.SecurityNameCode = order.SecurityNameCode;
                    trade.Volume = order.Volume;
                    trade.Price =
                            response.transactions[0].price.ToDecimal();
                    trade.Side = order.Side;

                    _osEngineOrders.RemoveAt(i);
                    i--;

                    if (MyTradeEvent != null)
                    {
                        MyTradeEvent(trade);
                    }
                }
                else if (response.status == "Finished" ||
                         (response.status == null && string.IsNullOrEmpty(order.NumberMarket)))
                {
                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = order.SecurityNameCode;
                    newOrder.NumberUser = order.NumberUser;
                    newOrder.NumberMarket = order.NumberMarket;
                    newOrder.PortfolioNumber = order.PortfolioNumber;
                    newOrder.Side = order.Side;
                    newOrder.Price = order.Price;
                    newOrder.State = OrderStateType.Cancel;
                    newOrder.Volume = order.Volume;

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(newOrder);
                    }

                    _osEngineOrders.RemoveAt(i);
                    i--;
                }
            }
        }

        /// <summary>
        /// update order status
        /// обновить статус ордера
        /// </summary>
        public OrderStatusResponse GetOrderStatus(string orderId)
        {
            lock (_lock)
            {
                try
                {
                    var request = GetAuthenticatedRequest(Method.POST);
                    request.AddParameter("id", orderId);

                    var response = new RestClient("https://www.bitstamp.net/api/order_status/").Execute(request);

                    return JsonConvert.DeserializeObject<OrderStatusResponse>(response.Content);
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    return null;
                }
            }
        }

        // public methods for requesting data throw HTTP
        // публичные методы запроса данных через HTTP   

        /// <summary>
        /// take securities
        /// взять бумаги
        /// </summary>
        public List<PairInfoResponse> GetSecurities()
        {
            lock (_lock)
            {
                try
                {

                    var request = GetAuthenticatedRequest(Method.GET);
                    var response = new RestClient("https://www.bitstamp.net/api/v2/trading-pairs-info/").Execute(request);

                    if (UpdatePairs != null)
                    {
                        UpdatePairs(JsonConvert.DeserializeObject<List<PairInfoResponse>>(response.Content));
                    }

                    return JsonConvert.DeserializeObject<List<PairInfoResponse>>(response.Content);
                }
                catch (Exception ex)
                {
                    if (LogMessageEvent != null)
                    {
                        LogMessageEvent(ex.ToString(), LogMessageType.Error);
                    }

                    return null;
                }
            }
        }

        /// <summary>
        /// cancel order
        /// отменить оредр
        /// </summary>
        public void CancelOrder(Order order)
        {
            if (CancelOrder(order.NumberMarket))
            {
                order.State = OrderStateType.Cancel;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }

                _ordersCanseled.Add(order);
            }
        }

        /// <summary>
        /// cancel order by id
        /// снять ордер по id
        /// </summary>
        private bool CancelOrder(string orderId)
        {
            lock (_lock)
            {
                try
                {
                    var request = GetAuthenticatedRequest(Method.POST);
                    request.AddParameter("id", orderId);

                    var response = new RestClient("https://www.bitstamp.net/api/v2/cancel_order/").Execute(request);

                    return true;

                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    return false;
                }
            }
        }

        /// <summary>
        /// cancel all orders
        /// отменить все ордера
        /// </summary>
        public bool CancelAllOrders()
        {
            lock (_lock)
            {
                try
                {
                    var request = GetAuthenticatedRequest(Method.POST);

                    var response = new RestClient("https://www.bitstamp.net/api/cancel_all_orders/").Execute(request);

                    return (response.Content == "true") ? true : false;
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    return false;
                }
            }
        }

        /// <summary>
        /// outgoing orders in OsEngine format
        /// исходящие ордера в формате OsEngine
        /// </summary>
        private List<Order> _osEngineOrders = new List<Order>();

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        /// <param name="order"></param>
        public void ExecuteOrder(Order order)
        {
            if (IsConnected == false)
            {
                return;
            }

            _osEngineOrders.Add(order);

            string url = "https://www.bitstamp.net/api/v2";

            if (order.Side == Side.Buy)
            {
                url += "/buy/";
            }
            else
            {
                url += "/sell/";
            }

            url += order.SecurityNameCode + "/";

            BuySellResponse result = Execute((double)order.Volume, (double)order.Price, url);

            if (result == null)
            {
                return;
            }

            SendLogMessage("order status: " + result.status + "  " + result.reason, LogMessageType.System);

            if (result.id != null)
            {
                Order newOrder = new Order();
                newOrder.SecurityNameCode = order.SecurityNameCode;
                newOrder.TimeCallBack = DateTime.Parse(result.datetime);
                newOrder.NumberUser = order.NumberUser;
                newOrder.NumberMarket = result.id;
                newOrder.PortfolioNumber = order.PortfolioNumber;
                newOrder.Side = order.Side;
                newOrder.Price = order.Price;
                newOrder.State = OrderStateType.Activ;
                newOrder.Volume = order.Volume;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(newOrder);
                }

            }
            else
            {
                Order newOrder = new Order();
                newOrder.SecurityNameCode = order.SecurityNameCode;
                newOrder.NumberUser = order.NumberUser;
                newOrder.PortfolioNumber = order.PortfolioNumber;
                newOrder.Side = order.Side;
                newOrder.State = OrderStateType.Fail;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(newOrder);
                }
            }
        }

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        /// <param name="amount">volume / объем</param>
        /// <param name="price">price / цена</param>
        /// <param name="securityUrl">security / бумага</param>
        /// <returns></returns>
        private BuySellResponse Execute(double amount, double price, string securityUrl)
        {
            lock (_lock)
            {
                try
                {
                    var request = GetAuthenticatedRequest(Method.POST);
                    request.AddParameter("amount", amount.ToString().Replace(",", "."));
                    request.AddParameter("price", price.ToString().Replace(",", "."));

                    var response = new RestClient(securityUrl).Execute(request);

                    if (response.Content.IndexOf("error", StringComparison.Ordinal) > 0)
                    {
                        SendLogMessage("Ошибка на выставлении ордера", LogMessageType.Error);
                        SendLogMessage(response.Content, LogMessageType.Error);
                        return null;
                    }


                    return JsonConvert.DeserializeObject<BuySellResponse>(response.Content);
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    return null;
                }
            }
        }

        // outgoing events
        // исходящие события

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
        /// portfolio update event
        /// событие обновления портфеля
        /// </summary>
        public event Action<BalanceResponse> UpdatePortfolio;

        /// <summary>
        /// new securities in the system
        /// новые бумаги в системе
        /// </summary>
        public event Action<List<PairInfoResponse>> UpdatePairs;

        /// <summary>
        /// depth updated
        /// обновился стакан
        /// </summary>
        public event Action<MarketDepth> UpdateMarketDepth;

        /// <summary>
        /// ticks updated
        /// обновились тики
        /// </summary>
        public event Action<Trade> NewTradesEvent;

        /// <summary>
        /// connection to BitStamp API established
        /// соединение с BitStamp API установлено
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// connection to BitStamp API lost
        /// соединение с BitStamp API разорвано
        /// </summary>
        public event Action Disconnected;

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
        /// send exeptions
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }
}