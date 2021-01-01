using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OkonkwoOandaV20.TradeLibrary.DataTypes.Order;
using OsEngine.Entity;
using OsEngine.Logging;
using WebSocket4Net;
using Order = OsEngine.Entity.Order;

namespace OsEngine.Market.Servers.Hitbtc
{
    public class HitbtcClient
    {
        // поля апи ключи
        private readonly string _pubKey;
        private readonly string _secKey;
        // урл апи
        private readonly string _baseUrl = "https://api.hitbtc.com";

        // название портфеля
        private readonly string _portfolioName;
        public string GetPortfolioName()
        {
            return _portfolioName;
        }

        // конструктор
        public HitbtcClient(string pubKey, string secKey, string portfolioname = null)
        {
            _pubKey = pubKey;
            _secKey = secKey;

            _portfolioName = "HitBtcPortfolio" + portfolioname;
        }



        /// <summary>
        /// shows whether connection works
        /// работает ли соединение
        /// </summary>
        public bool IsConnected;

        /// <summary>
        /// connecto to the exchange
        /// установить соединение с биржей 
        /// </summary>
        public void Connect()
        {
            if (string.IsNullOrEmpty(_pubKey)||
                string.IsNullOrEmpty(_secKey))
            {
                return;
            }

            // check server availability for HTTP communication with it / проверяем доступность сервера для HTTP общения с ним
            Uri uri = new Uri(_baseUrl+"/api/2");

            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                var httpWebRequest = (HttpWebRequest) WebRequest.Create(uri);

                var httpWebResponse = (HttpWebResponse) httpWebRequest.GetResponse();

            }
            catch (Exception exception)
            {
                SendLogMessage("Server is not available. Check internet connection." + exception.Message, LogMessageType.Error);
                return;
            }

            IsConnected = true;

            //if (Connected != null)
            //{
            //    Connected();
            //}

            Task converter = new Task(Converter);
            converter.Start();

            CreateNewWebSocket();
        }

        /// <summary>
        /// there was a request to clear the object
        /// произошёл запрос на очистку объекта
        /// </summary>
        private bool _isDisposed;

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

            try
            {
                _wsClient.Close();
            }
            catch
            {
                // ignore
            }
            
            _isDisposed = true;
        }

        private object _lock = new object();
        public List<Balances> GetPortfolio()
        {
            lock (_lock)
            {
                try
                {
                    var result = CreateQuery("GET", "/api/2/trading/balance", _pubKey, _secKey, true);

                    List<Balances> balances;
                    if (result != null)
                        balances = JsonConvert.DeserializeAnonymousType(result, new List<Balances>());
                    else
                        balances = new List<Balances>();

                    BalanceInfo balanceinfo = new BalanceInfo();

                    balanceinfo.Balances = balances;
                    balanceinfo.Name = _portfolioName;

                    if (NewPortfolio != null)
                    {
                        NewPortfolio(balanceinfo);
                    }

                    return balances;
                }
                catch (Exception exception)
                {
                    if (LogMessageEvent != null)
                    {
                        SendLogMessage(exception.ToString(), LogMessageType.Error);
                    }
                    return null;
                }
            }
        }

        /// <summary>
        /// take securities
        /// взять бумаги
        /// </summary>
        public List<Symbols> GetPairs()
        {
            lock (_lock)
            {
                try
                {
                    var result = CreateQuery("GET", "/api/2/public/symbol");
                    List<Symbols> symbols = JsonConvert.DeserializeAnonymousType(result, new List<Symbols>());

                    if (NewPairs != null)
                    {
                        NewPairs(symbols);
                    }

                    return symbols;


                }
                catch (Exception exception)
                {
                    if (LogMessageEvent != null)
                    {
                        SendLogMessage(exception.ToString(), LogMessageType.Error);
                    }

                    return null;
                }
            }
        }

        /// <summary>
        /// multi-threaded access locker to http-requests
        /// блокиратор многопоточного доступа к http запросам
        /// </summary>
        private object _queryHttpLocker = new object();
        public string CreateQuery(string method, string endpoint, string pubKey = "", string secKey = "", bool isAuth = false)
        {
            lock (_queryHttpLocker)
            {

                try
                {

                    HttpWebRequest request = (HttpWebRequest) WebRequest.Create(_baseUrl + endpoint);
                    request.Method = method.ToUpper();
                    request.Accept = "application/json; charset=utf-8";

                    if (isAuth)
                    {
                        //For Basic Authentication
                        string authInfo = pubKey + ":" + secKey;
                        authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
                        request.Headers["Authorization"] = "Basic " + authInfo;

                        var response = (HttpWebResponse) request.GetResponse();

                        string strResponse = "";
                        using (var sr = new StreamReader(response.GetResponseStream()))
                        {
                            strResponse = sr.ReadToEnd();
                        }

                        return strResponse;
                    }
                    else
                    {
                        var response = (HttpWebResponse) request.GetResponse();

                        string strResponse = "";
                        using (var sr = new StreamReader(response.GetResponseStream()))
                        {
                            strResponse = sr.ReadToEnd();
                        }

                        return strResponse;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return null;
                }
            }
        }

        /// <summary>
        /// candles
        /// свечи
        /// </summary>
        private List<Candle> _candles;

        private readonly object _candleLocker = new object();

        /// <summary>
        /// take candles
        /// взять свечи
        /// </summary>
        /// <returns></returns>
        public List<Candle> GetCandles(string nameSec, TimeSpan tf)
        {
            string needTf = "";

            switch ((int)tf.TotalMinutes)
            {
                case 1:
                    needTf = "M1";
                    break;
                case 2:
                    needTf = "M2";
                    break;
                case 3:
                    needTf = "M3";
                    break;
                case 5:
                    needTf = "M5";
                    break;
                case 10:
                    needTf = "M10";
                    break;
                case 15:
                    needTf = "M15";
                    break;
                case 20:
                    needTf = "M20";
                    break;
                case 30:
                    needTf = "M30";
                    break;
                case 45:
                    needTf = "M45";
                    break;
                case 60:
                    needTf = "H1";
                    break;
                case 120:
                    needTf = "H2";
                    break;
            }

            string endPoint = "/api/2/public/candles/";
            try
            {
                lock (_candleLocker)
                {
                    if (needTf != "M2" && needTf != "M10" && needTf != "M20" && needTf != "M45" && needTf != "H2")
                    {
                        endPoint += nameSec.ToUpper() + "?" + "period=" + needTf;
                        var res = CreateQuery("GET", endPoint);
                        List<HitCandle> hitCandles = JsonConvert.DeserializeAnonymousType(res, new List<HitCandle>());

                        _candles = new List<Candle>();

                        Candle newCandle;

                        if (hitCandles != null)
                        {
                            foreach (var candle in hitCandles)
                            {
                                newCandle = new Candle();
                                newCandle.TimeStart = candle.timestamp;
                                newCandle.Low = candle.min.ToDecimal();
                                newCandle.High = candle.max.ToDecimal();
                                newCandle.Open = candle.open.ToDecimal();
                                newCandle.Close = candle.close.ToDecimal();
                                newCandle.Volume = candle.volume.ToDecimal();

                                _candles.Add(newCandle);
                            }
                        }
                        return _candles;

                    }
                    else
                    {
                        if (needTf == "M2")
                        {
                            endPoint += nameSec.ToUpper() + "?" + "period=" + "M1";
                            var res = CreateQuery("GET", endPoint);
                            List<HitCandle> hitCandles = JsonConvert.DeserializeAnonymousType(res, new List<HitCandle>());

                            _candles = new List<Candle>();

                            Candle newCandle;

                            if (hitCandles != null)
                            {
                                foreach (var candle in hitCandles)
                                {
                                    newCandle = new Candle();
                                    newCandle.TimeStart = candle.timestamp;
                                    newCandle.Low = candle.min.ToDecimal();
                                    newCandle.High = candle.max.ToDecimal();
                                    newCandle.Open = candle.open.ToDecimal();
                                    newCandle.Close = candle.close.ToDecimal();
                                    newCandle.Volume = candle.volume.ToDecimal();

                                    _candles.Add(newCandle);
                                }
                            }

                            var newCandles = BuildCandles(_candles, 2, 1);
                            return newCandles;
                        }
                        else if (needTf == "M10")
                        {
                            endPoint += nameSec.ToUpper() + "?" + "period=" + "M5";
                            var res = CreateQuery("GET", endPoint);
                            List<HitCandle> hitCandles = JsonConvert.DeserializeAnonymousType(res, new List<HitCandle>());

                            _candles = new List<Candle>();

                            Candle newCandle;

                            if (hitCandles != null)
                            {
                                foreach (var candle in hitCandles)
                                {
                                    newCandle = new Candle();
                                    newCandle.TimeStart = candle.timestamp;
                                    newCandle.Low = candle.min.ToDecimal();
                                    newCandle.High = candle.max.ToDecimal();
                                    newCandle.Open = candle.open.ToDecimal();
                                    newCandle.Close = candle.close.ToDecimal();
                                    newCandle.Volume = candle.volume.ToDecimal();

                                    _candles.Add(newCandle);
                                }
                            }

                            var newCandles = BuildCandles(_candles, 10, 5);
                            return newCandles;
                        }
                        else if (needTf == "M20")
                        {
                            endPoint += nameSec.ToUpper() + "?" + "period=" + "M5";
                            var res = CreateQuery("GET", endPoint);
                            List<HitCandle> hitCandles = JsonConvert.DeserializeAnonymousType(res, new List<HitCandle>());

                            _candles = new List<Candle>();

                            Candle newCandle;

                            if (hitCandles != null)
                            {
                                foreach (var candle in hitCandles)
                                {
                                    newCandle = new Candle();
                                    newCandle.TimeStart = candle.timestamp;
                                    newCandle.Low = candle.min.ToDecimal();
                                    newCandle.High = candle.max.ToDecimal();
                                    newCandle.Open = candle.open.ToDecimal();
                                    newCandle.Close = candle.close.ToDecimal();
                                    newCandle.Volume = candle.volume.ToDecimal();

                                    _candles.Add(newCandle);
                                }
                            }


                            var newCandles = BuildCandles(_candles, 20, 5);
                            return newCandles;
                        }
                        else if (needTf == "M45")
                        {
                            endPoint += nameSec.ToUpper() + "?" + "period=" + "M15";
                            var res = CreateQuery("GET", endPoint);
                            List<HitCandle> hitCandles = JsonConvert.DeserializeAnonymousType(res, new List<HitCandle>());

                            _candles = new List<Candle>();

                            Candle newCandle;

                            if (hitCandles != null)
                            {
                                foreach (var candle in hitCandles)
                                {
                                    newCandle = new Candle();
                                    newCandle.TimeStart = candle.timestamp;
                                    newCandle.Low = candle.min.ToDecimal();
                                    newCandle.High = candle.max.ToDecimal();
                                    newCandle.Open = candle.open.ToDecimal();
                                    newCandle.Close = candle.close.ToDecimal();
                                    newCandle.Volume = candle.volume.ToDecimal();

                                    _candles.Add(newCandle);
                                }
                            }
                            var newCandles = BuildCandles(_candles, 45, 15);
                            return newCandles;
                        }
                        else if (needTf == "H2")
                        {
                            endPoint += nameSec.ToUpper() + "?" + "period=" + "H1";
                            var res = CreateQuery("GET", endPoint);
                            List<HitCandle> hitCandles = JsonConvert.DeserializeAnonymousType(res, new List<HitCandle>());

                            _candles = new List<Candle>();

                            Candle newCandle;

                            if (hitCandles != null)
                            {
                                foreach (var candle in hitCandles)
                                {
                                    newCandle = new Candle();
                                    newCandle.TimeStart = candle.timestamp;
                                    newCandle.Low = candle.min.ToDecimal();
                                    newCandle.High = candle.max.ToDecimal();
                                    newCandle.Open = candle.open.ToDecimal();
                                    newCandle.Close = candle.close.ToDecimal();
                                    newCandle.Volume = candle.volume.ToDecimal();

                                    _candles.Add(newCandle);
                                }
                            }
                            var newCandles = BuildCandles(_candles, 120, 60);
                            return newCandles;
                        }
                    }

                    return null;
                }
            }
            catch (Exception e)
            {
                return null;
            }
        }

        /// <summary>
        /// converts candles of one timeframe to a larger
        /// преобразует свечи одного таймфрейма в больший
        /// </summary>
        /// <param name="oldCandles"></param>
        /// <param name="needTf"></param>
        /// <param name="oldTf"></param>
        /// <returns></returns>
        private List<Candle> BuildCandles(List<Candle> oldCandles, int needTf, int oldTf)
        {
            List<Candle> newCandles = new List<Candle>();

            int index = oldCandles.FindIndex(can => can.TimeStart.Minute % needTf == 0);

            int count = needTf / oldTf;

            int counter = 0;

            Candle newCandle = new Candle();

            for (int i = index; i < oldCandles.Count; i++)
            {
                counter++;

                if (counter == 1)
                {
                    newCandle = new Candle();
                    newCandle.Open = oldCandles[i].Open;
                    newCandle.TimeStart = oldCandles[i].TimeStart;
                    newCandle.Low = Decimal.MaxValue;
                }

                newCandle.High = oldCandles[i].High > newCandle.High
                    ? oldCandles[i].High
                    : newCandle.High;

                newCandle.Low = oldCandles[i].Low < newCandle.Low
                    ? oldCandles[i].Low
                    : newCandle.Low;

                newCandle.Volume += oldCandles[i].Volume;

                if (counter == count)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.Finished;
                    newCandles.Add(newCandle);
                    counter = 0;
                }

                if (i == oldCandles.Count - 1 && counter != count)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.None;
                    newCandles.Add(newCandle);
                }
            }

            return newCandles;
        }
        #region Work with orders / Работа с ордерами


        #endregion

        #region Stream data from WEBSOCKET / Потоковые данные из WEBSOCKET

        /// <summary>
        /// adress for web-socket connection
        /// адрес для подключения к вебсокетам
        /// </summary>
        private string _wsUrl = "wss://api.hitbtc.com/api/2/ws";

        private WebSocket _wsClient;

        /// <summary>
        /// create a new web-socket connection
        /// создать новое подключение по сокетам
        /// </summary>
        private void CreateNewWebSocket()
        {
            if (_wsClient == null)
            {
                _wsClient = new WebSocket(_wsUrl);
            }

            _wsClient.Opened += new EventHandler(Connect);

            _wsClient.Closed += new EventHandler(Disconnect);

            _wsClient.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(WsError);

            _wsClient.MessageReceived += new EventHandler<MessageReceivedEventArgs>(GetRes);

            _wsClient.Open();

            Thread.Sleep(2000);

            if (_wsClient.State != WebSocketState.Open)
            {
                if (Disconnected != null)
                {
                    SendLogMessage("Cannot connect to exchange. Lost connection with exchange", LogMessageType.Error);
                    Disconnected();
                }
            }
        }

        public void CancelOrder(string genId)
        {
            string request =
                $"{{ \"method\": \"cancelOrder\", \"params\": {{ \"clientOrderId\": \"{genId}\" }}, \"id\": 500 }}";

            _wsClient.Send(request);
        }

        public void SendOrder(Order order, string genId)
        {

            string needId = genId;

            string side = order.Side == Side.Buy ? "buy" : "sell";

            string request =
                $"{{\"method\": \"newOrder\", \"params\": {{ \"clientOrderId\": \"{needId}\", \"symbol\": \"{order.SecurityNameCode}\", \"side\": \"{side}\",";

            if (order.TypeOrder == OrderPriceType.Limit)
            {
                request += $"\"type\": \"limit\", \"price\": \"{order.Price.ToString(CultureInfo.InvariantCulture).Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, ".")}\",";
            }

            request += $"\"quantity\": \"{order.Volume.ToString(CultureInfo.InvariantCulture).Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, ".")}\" }}, \"id\": 200 }}";

            _wsClient.Send(request);
        }

        private List<Security> _subscribedSecurities = new List<Security>();

        /// <summary>
        /// subscribe to this security to receive depths and trades
        /// подписать данную бумагу на получение стаканов и трейдов
        /// </summary>
        public void SubscribleTradesAndDepths(Security security)
        {
            try
            {
                Thread.Sleep(1000);
                var needSec = _subscribedSecurities.Find(s => s.Name == security.Name);

                if (needSec == null)
                {
                    _subscribedSecurities.Add(security);
                    // subscribe to depth / подписаться на стаканы
                    string subscribeOrderBook = string.Format("{{\"method\": \"subscribeOrderbook\",\"params\": {{\"symbol\": \"{0}\" }},\"id\": 123}}", security.Name);
                    _wsClient.Send(subscribeOrderBook);
                    SendLogMessage(subscribeOrderBook, LogMessageType.System);

                    //// subscribe to ticks / подписаться на тики
                    //string subscribeOrderBook = string.Format("{{\"method\": \"subscribeTicker\",\"params\": {{\"symbol\": \"{0}\", \"limit\": 1 }},\"id\": 124}}", security.Name);
                    //_wsClient.Send(subscribeOrderBook);
                    //SendLogMessage(subscribeOrderBook, LogMessageType.System);

                    // subcribe to trades / подписаться на трейды
                    string subscribeTrades = string.Format("{{\"method\": \"subscribeTrades\",\"params\": {{\"symbol\": \"{0}\", \"limit\": 100 }},\"id\": 124}}", security.Name);
                    _wsClient.Send(subscribeTrades);
                    SendLogMessage(subscribeTrades, LogMessageType.System);

                    

                }

                // auth method
                string authmethod =
                    string.Format(
                        "{{\"method\": \"login\", \"params\": {{\"algo\": \"BASIC\", \"pKey\": \"{0}\", \"sKey\": \"{1}\"}}}}",
                        _pubKey, _secKey);
                _wsClient.Send(authmethod);
                //SendLogMessage(authmethod, LogMessageType.System);
                // get balance update
                string getbalance =
                    string.Format("{{ \"method\": \"getTradingBalance\", \"params\": {{ }}, \"id\": 125 }}");
                _wsClient.Send(getbalance);
                //SendLogMessage(getbalance,LogMessageType.System);

                // subcribe to reports about trades / подписаться на статус по трейдам
                string subscribeReports = string.Format("{{\"method\": \"subscribeReports\",\"params\": {{}}}}");
                _wsClient.Send(subscribeReports);
                SendLogMessage(subscribeReports, LogMessageType.System);

            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void GetBalance()
        {
            string getbalance =
                string.Format("{{ \"method\": \"getTradingBalance\", \"params\": {{ }}, \"id\": 125 }}");
            _wsClient.Send(getbalance);
        }

        /// <summary>
        /// unsubscribe to data
        /// отписаться от данных
        /// </summary>
        /// <param name="security"></param>
        public void UnsubscribleTradesAndDepths(Security security)
        {
            // unsubscribe to depth / подписаться на стаканы
            string subscribeTrades = string.Format("{{\"method\": \"unsubscribeOrderbook\",\"params\": {{\"symbol\": \"{0}\" }},\"id\": 123}}", security.Name);
            _wsClient.Send(subscribeTrades);

            // unsubscribe to ticks / подписаться на тики
            string subscribeOrderBook = string.Format("{{\"method\": \"unsubscribeTicker\",\"params\": {{\"symbol\": \"{0}\" }},\"id\": 124}}", security.Name);
            _wsClient.Send(subscribeOrderBook);
        }

        /// <summary>
        /// ws-connection opened
        /// соединение по ws открыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Connect(object sender, EventArgs e)
        {
            IsConnected = true;

            if (Connected != null)
            {
                Connected();
            }

            SendLogMessage("Connected over websocket", LogMessageType.System);
        }

        /// <summary>
        /// ws-connection closed
        /// соединение по ws закрыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Disconnect(object sender, EventArgs e)
        {
            if (IsConnected)
            {
                IsConnected = false;

                SendLogMessage("Соединение через вебсокет разорвано", LogMessageType.System);

                if (Disconnected != null)
                {
                    Disconnected();
                }
            }
        }

        /// <summary>
        /// error from ws4net
        /// ошибка из ws4net
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WsError(object sender, EventArgs e)
        {
            SendLogMessage("Ошибка из ws4net :" + e, LogMessageType.Error);
        }

        /// <summary>
        /// queue of new messages from the exchange server
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();

        /// <summary>
        /// takes ws-messages and puts them in a general queue
        /// берет пришедшие через ws сообщения и кладет их в общую очередь
        /// </summary>        
        private void GetRes(object sender, MessageReceivedEventArgs e)
        {
            if (_isDisposed)
            {
                return;
            }

            _newMessage.Enqueue(e.Message);
            //SendLogMessage(e.Message, LogMessageType.System);
        }

        /// <summary>
        /// takes messages from the common queue, converts them to C # classes and sends them to up
        /// берет сообщения из общей очереди, конвертирует их в классы C# и отправляет на верх
        /// </summary>
        public async void Converter()
        {
            while (true)
            {
                try
                {
                    if (!_newMessage.IsEmpty)
                    {
                        string mes;

                        if (_newMessage.TryDequeue(out mes))
                        {
                            if (mes.Contains("snapshotOrderbook"))
                            {
                                var depth = JsonConvert.DeserializeAnonymousType(mes, new RootDepth());

                                if (NewMarketDepth != null)
                                {
                                    NewMarketDepth(depth);
                                }
                                continue;
                            }

                            if (mes.Contains("updateOrderbook"))
                            {
                                var updDepth = JsonConvert.DeserializeAnonymousType(mes, new UpdateDepth());

                                if (UpdateMarketDepth != null)
                                {
                                    UpdateMarketDepth(updDepth);
                                }
                                continue;
                            }

                            if (mes.Contains("snapshotTrades"))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new RootTrade());

                                if (NewTradesEvent != null)
                                {
                                    NewTradesEvent(quotes);
                                }
                                continue;
                            }

                            if (mes.Contains("updateTrades"))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new RootTrade());

                                if (NewTradesEvent != null)
                                {
                                    NewTradesEvent(quotes);
                                }
                                continue;
                            }

                            //if (mes.Contains("ticker"))
                            //{
                            //    var quotes = JsonConvert.DeserializeAnonymousType(mes, new RootTick());

                            //    if (NewTradesEvent != null)
                            //    {
                            //        NewTradesEvent(quotes);
                            //    }
                            //    continue;
                            //}

                            if (mes.Contains("\"id\":125"))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new UpdateBalance());

                                if (UpdatePortfolio != null)
                                {
                                    UpdatePortfolio(GetPortfolioName(),quotes);
                                }
                                continue;
                            }

                            //if (mes.Contains("\"reportType\":\"new\""))
                            //{
                            //    RootOrder quotes = JsonConvert.DeserializeAnonymousType(mes, new RootOrder());

                            //    Result res = quotes.result;

                            //    //SendLogMessage(mes, LogMessageType.Error);

                            //    if (MyOrderEvent != null)
                            //    {
                            //        MyOrderEvent(res);
                            //    }
                            //    continue;
                            //}

                            //if (mes.Contains("\"reportType\":\"canceled\""))
                            //{
                            //    RootOrder quotes = JsonConvert.DeserializeAnonymousType(mes, new RootOrder());

                            //    Result res = quotes.result;

                            //    //SendLogMessage(mes, LogMessageType.Error);

                            //    if (MyOrderEvent != null)
                            //    {
                            //        MyOrderEvent(res);
                            //    }
                            //    continue;
                            //}

                            if (mes.Contains("\"method\":\"report\""))
                            {
                                RootReport quotes = JsonConvert.DeserializeAnonymousType(mes, new RootReport());

                                Result res = new Result
                                {
                                    clientOrderId = quotes.@params.clientOrderId,
                                    id = quotes.@params.id,
                                    createdAt = quotes.@params.createdAt,
                                    cumQuantity = quotes.@params.cumQuantity,
                                    postOnly = quotes.@params.postOnly,
                                    price = quotes.@params.price,
                                    quantity = quotes.@params.quantity,
                                    side = quotes.@params.side,
                                    symbol = quotes.@params.symbol,
                                    status = quotes.@params.status,
                                    reportType = quotes.@params.reportType,
                                    timeInForce = quotes.@params.timeInForce,
                                    type = quotes.@params.type,
                                    updatedAt = quotes.@params.updatedAt
                                };



                                //SendLogMessage(mes, LogMessageType.Error);

                                if (MyOrderEvent != null)
                                {
                                    MyOrderEvent(res);
                                }
                                continue;
                            }

                            if (mes.Contains("error"))
                            {
                                SendLogMessage(mes, LogMessageType.Error);
                            }
                        }
                    }
                    else
                    {
                        if (_isDisposed)
                        {
                            return;
                        }

                        if (MainWindow.ProccesIsWorked == false)
                        {
                            return;
                        }

                        await Task.Delay(1);
                    }
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }


        #endregion

        #region outgoing messages / исходящие события

        public event Action<Result> MyOrderEvent;

        /// <summary>
        /// ticks created
        /// создали тики
        /// </summary>
        public event Action<RootTrade> NewTradesEvent;

        /// <summary>
        /// depth updated
        /// обновился стакан
        /// </summary>
        public event Action<UpdateDepth> UpdateMarketDepth;

        /// <summary>
        /// depth New
        /// Новый стакан
        /// </summary>
        public event Action<RootDepth> NewMarketDepth;

        /// <summary>
        /// new portfolios
        /// новые портфели
        /// </summary>
        public event Action<BalanceInfo> NewPortfolio;

        public event Action<string, UpdateBalance> UpdatePortfolio;

        /// <summary>
        /// new security in the system
        /// новые бумаги в системе
        /// </summary>
        public event Action<List<Symbols>> NewPairs;

        /// <summary>
        /// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action Disconnected;

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action Connected;

        #endregion

        #region log message / сообщения для лога

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
}
