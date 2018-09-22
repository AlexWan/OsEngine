using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Binance.BinanceEntity;
using OsEngine.Market.Servers.Bitfinex.BitfitnexEntity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocket4Net;


namespace OsEngine.Market.Servers.Bitfinex
{
    class BitfinexClient
    {
        string _apiKey;
        string _secretKey;

        public BitfinexClient()
        {
            _restClient = new RestClient(_baseUrlV1);

            for (int i = 0; i < 5; i++)
            {
               Thread converter = new Thread(Converter);
                converter.CurrentCulture = new CultureInfo("ru-RU");
                converter.Name = "BitFinexConverterFread" + i;
                converter.IsBackground = true;
                converter.Start();
            }
        }

        private string _baseUrlV1 = "https://api.bitfinex.com/v1";

        private string _baseUrlV2 = "https://api.bitfinex.com/v2";

        /// <summary>
        /// работает ли соединение
        /// </summary>
        public bool IsConnected;

        /// <summary>
        /// установить соединение с биржей 
        /// </summary>
        public void Connect(string pubKey, string secKey)
        {
            _apiKey = pubKey;
            _secretKey = secKey;

            if (string.IsNullOrWhiteSpace(_apiKey) ||
                string.IsNullOrWhiteSpace(_secretKey))
            {
                return;
            }

            // проверяем доступность сервера для HTTP общения с ним
            Uri uri = new Uri(_baseUrlV1 + "/symbols");

            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);

                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (Exception exception)
            {
                SendLogMessage("Сервер не доступен. Отсутствует интернет. ", LogMessageType.Error);
                return;
            }

            IsConnected = true;

            CreateNewWebSocket();
        }

        /// <summary>
        /// произошёл запрос на очистку объекта
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        public void Dispose()
        {
            if (_wsClient != null)
            {
                _wsClient.Dispose();
                _wsClient = null;
            }
           
            IsConnected = false;

            _isDisposed = true;
        }

        /// <summary>
        /// запросить инструменты
        /// </summary>
        /// <returns></returns>
        public List<BitfinexSecurity> GetSecurities()
        {
            try
            {
                var res = CreateQuery(_baseUrlV1, Method.GET, "symbols_details", null);

                var parsSecurities = JsonConvert.DeserializeAnonymousType(res, new List<BitfinexSecurity>());

                return parsSecurities;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// подписаться на получение личных данных, таких как портфели, позиции, ордера, сделки
        /// </summary>
        public void SubscribeUserData()
        {
            string subscribeString = TakeSubscriptionMesAuthWebSoket();

            _wsClient.Send(subscribeString);
        }

        private readonly object _candlesLocker = new object();

        /// <summary>
        /// запросить свечи
        /// </summary>
        public string GetCandles(Dictionary<string, string> param)
        {
            try
            {
                lock (_candlesLocker)
                {
                    var res = CreateQuery(_baseUrlV2, Method.GET, "candles", param);

                    return res;
                    //var parsSecurities = JsonConvert.DeserializeAnonymousType(res, new List<BitfinexSecurity>());
                }                
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private object _lockOrder = new object();

        /// <summary>
        /// исполнить ордер
        /// </summary>
        public void ExecuteOrder(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    if (IsConnected == false)
                    {
                        return;
                    }

                    NewOrderPayload newOrder = new NewOrderPayload();

                    newOrder.type = "exchange limit";
                    newOrder.exchange = "bitfinex";
                    newOrder.request = "/v1/order/new";
                    newOrder.side = order.Side == Side.Buy ? "buy" : "sell";
                    newOrder.price = order.Price.ToString(CultureInfo.InvariantCulture);
                    newOrder.amount = order.Volume.ToString(CultureInfo.InvariantCulture);
                    newOrder.symbol = order.SecurityNameCode;
                    newOrder.nonce = GetNonce();

                    var jsonPayload = JsonConvert.SerializeObject(newOrder);

                    var res = CreateAuthQuery(_baseUrlV1, "/order/new", jsonPayload);

                    BitfinexResponseOrder newCreatedOrder;

                    if (res != null)
                    {
                        newCreatedOrder = JsonConvert.DeserializeObject<BitfinexResponseOrder>(res);

                        _osOrders.Add(newCreatedOrder.id.ToString(), order.NumberUser);

                        var newOsOrder = new Order();
                        newOsOrder.SecurityNameCode = newCreatedOrder.symbol;
                        newOsOrder.PortfolioNumber = newCreatedOrder.symbol.Substring(3);
                        newOsOrder.Side = newCreatedOrder.side == "buy" ? Side.Buy : Side.Sell;
                        newOsOrder.NumberMarket = newCreatedOrder.order_id.ToString();
                        newOsOrder.NumberUser = order.NumberUser;

                        newOsOrder.Price = Convert.ToDecimal(newCreatedOrder.price.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                        newOsOrder.Volume = Convert.ToDecimal(newCreatedOrder.original_amount.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                        newOsOrder.VolumeExecute = Convert.ToDecimal(newCreatedOrder.executed_amount.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);

                        newOsOrder.TimeCallBack = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(Math.Round(Convert.ToDouble(newCreatedOrder.timestamp.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture)));

                        newOsOrder.State = OrderStateType.Activ;

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(newOsOrder);
                        }
                    }
                    else
                    {
                         order.State = OrderStateType.Fail;
                         if (MyOrderEvent != null)
                         {
                             MyOrderEvent(order);
                         }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// id ордеров принадлежащих осе и номера в роботе
        /// </summary>
        Dictionary<string,int> _osOrders = new Dictionary<string, int>();

        /// <summary>
        /// отменить оредр
        /// </summary>
        public void CanselOrder(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    var jsonPayload = string.Format("{{\"request\": \"/v1/order/cancel\",\"nonce\": \"{0}\",\"order_id\": {1}}}", GetNonce(), order.NumberMarket);

                    var res = CreateAuthQuery(_baseUrlV1, "/order/cancel", jsonPayload);

                    BitfinexResponseOrder CanceledOrder = JsonConvert.DeserializeObject<BitfinexResponseOrder>(res);
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// блокиратор многопоточного доступа к http запросам
        /// </summary>
        private readonly object _queryHttpLocker = new object();

        private readonly IRestClient _restClient;

        /// <summary>
        /// создать аутентифицированный запрос
        /// </summary>
        public string CreateAuthQuery(string apiVersionUrl, string endpoint, string payload)
        {
            try
            {
                lock (_queryHttpLocker)
                {
                    string payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

                    var request = new RestRequest(endpoint, Method.POST);
                    request.AddHeader("X-BFX-APIKEY", _apiKey);
                    request.AddHeader("X-BFX-PAYLOAD", payloadBase64);
                    request.AddHeader("X-BFX-SIGNATURE", CreateSignature(payloadBase64, _secretKey));
                    
                    var response = _restClient.Execute(request);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return response.Content;
                    }
                    else
                    {
                        SendLogMessage("Ошибка в запросе" + response.Content, LogMessageType.Error);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// метод отправляет запрос и возвращает ответ от сервера
        /// </summary>
        public string CreateQuery(string apiVersionUrl, Method method, string endpoint, Dictionary<string, string> param = null)
        {
            try
            {
                lock (_queryHttpLocker)
                {
                    string fullUrl = "";

                    if (param != null)
                    {
                        fullUrl += "/";

                        foreach (var onePar in param)
                        {
                            fullUrl += onePar.Key + onePar.Value;
                        }
                    }

                    var request = new RestRequest(endpoint + fullUrl, method);

                    var response = new RestClient(apiVersionUrl).Execute(request);

                    var result = response.Content;

                    if (string.IsNullOrEmpty(result) || response.StatusCode != HttpStatusCode.OK)
                    {
                        SendLogMessage("Ошибка в запросе:" + result, LogMessageType.Error);
                        return null;
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #region Аутентификация запроса

        /// <summary>
        /// взять сообщение подписки аутентифицированного вебсокета
        /// </summary>
        /// <returns></returns>
        private string TakeSubscriptionMesAuthWebSoket()
        {
            string authNonce = GetNonce();

            string authPayload = "AUTH" + authNonce;

            string authSig = CreateSignature(authPayload, _secretKey);

            string payload = string.Format("{{\"apiKey\":\"{0}\", \"authSig\":\"{1}\", \"authNonce\":{2}, \"authPayload\":\"{3}\", \"event\":\"auth\"}}", _apiKey, authSig, authNonce, authPayload);

            return payload;
        }

        /// <summary>
        /// взять время в милисекундах, прошедшее с 1970, 1, 1 года
        /// </summary>
        /// <returns></returns>
        private string GetNonce()
        {
            DateTime yearBegin = new DateTime(1970, 1, 1);
            var timeStamp = DateTime.UtcNow - yearBegin;
            var r = timeStamp.TotalMilliseconds;
            var re = Convert.ToInt64(r);
            return re.ToString();
        }

        /// <summary>
        /// закодировать сообщение
        /// </summary>
        /// <param name="payload">сообщение</param>
        /// <param name="apiSecret">секретный ключ</param>
        /// <returns></returns>
        public string CreateSignature(string payload, string apiSecret)
        {
            var keyBytes = Encoding.UTF8.GetBytes(payload);
            var secretBytes = Encoding.UTF8.GetBytes(apiSecret);
            
            /*string ByteToString(byte[] buff)
            {
                var builder = new StringBuilder();

                for (var i = 0; i < buff.Length; i++)
                {
                    builder.Append(buff[i].ToString("X2")); // hex format
                }
                return builder.ToString();
            }*/

            using (var hmacsha384 = new HMACSHA384(secretBytes))
            {
                byte[] hashmessage = hmacsha384.ComputeHash(keyBytes);
                return ByteToString(hashmessage).ToLower();
            }
        }

        private string ByteToString(byte[] buff)
        {
            var builder = new StringBuilder();

            for (var i = 0; i < buff.Length; i++)
            {
                builder.Append(buff[i].ToString("X2")); // hex format
            }
            return builder.ToString();

        }

        #endregion

        #region сообщения для лога

        /// <summary>
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
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region Работа с потоковыми данными через вебсокеты

        /// <summary>
        /// адрес для подключения к вебсокетам
        /// </summary>
        private string _wsUrl = "wss://api.bitfinex.com/ws";

        private WebSocket _wsClient;

        /// <summary>
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
                    SendLogMessage("Подключение к бирже битфайнекс не удалось. Отсутствует связь с биржей",LogMessageType.Error);
                    Disconnected();
                }
            }
        }

        private List<Security> _subscribedSecurities = new List<Security>();

        /// <summary>
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
                    // подписаться на стаканы
                    string subscribeTrades = string.Format("{{\"event\": \"subscribe\", \"channel\": \"book\", \"pair\": \"{0}\", \"prec\": \"P0\", \"freq\": \"F0\"}}", security.Name);
                    _wsClient.Send(subscribeTrades);

                    // подписаться на тики
                    string subscribeOrderBook = string.Format("{{\"event\": \"subscribe\", \"channel\": \"trades\", \"pair\": \"{0}\"}}", security.Name);
                    _wsClient.Send(subscribeOrderBook);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// отписаться от данных
        /// </summary>
        /// <param name="security"></param>
        public void UnsubscribleTradesAndDepths(Security security)
        {
            // подписаться на стаканы
            string subscribeTrades = string.Format("{{\"event\": \"unsubscribe\", \"chanId\": \"{0}\"}}", security.Name);
            _wsClient.Send(subscribeTrades);

            // подписаться на тики
            string subscribeOrderBook = string.Format("{{\"event\": \"unsubscribe\", \"channel\": \"trades\", \"pair\": \"{0}\"}}", security.Name);
            _wsClient.Send(subscribeOrderBook);
        }

        /// <summary>
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

            SendLogMessage("Соединение через вебсокет успешно установлено", LogMessageType.System);
        }

        /// <summary>
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
        /// ошибка из ws4net
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WsError(object sender, EventArgs e)
        {
            SendLogMessage("Ошибка из ws4net :" + e, LogMessageType.Error);            
        }

        /// <summary>
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();

        /// <summary>
        /// берет пришедшие через ws сообщения и кладет их в общую очередь
        /// </summary>        
        private void GetRes(object sender, MessageReceivedEventArgs e)
        {
            _newMessage.Enqueue(e.Message);
        }
        
        /// <summary>
        /// коллекция инструментов подписанных на тики
        /// </summary>
        private Dictionary<string, int> _subscribedTradesSecurity = new Dictionary<string, int>();

        /// <summary>
        /// коллекция инструментов подписанных на стаканы
        /// </summary>
        private Dictionary<string, int> _subscribedBooksSecurity = new Dictionary<string, int>();

        /// <summary>
        /// блокировка многопоточной высылки даных на верх
        /// </summary>
        private object _senderLocker = new object();

        /// <summary>
        /// берет сообщения из общей очереди, конвертирует их в классы C# и отправляет на верх
        /// </summary>
        public void Converter()
        {
            while (true)
            {
                try
                {
                    if (_isDisposed)
                    {
                        return;
                    }

                    if (!_newMessage.IsEmpty)
                    {
                        string mes;

                        if (_newMessage.TryDequeue(out mes))
                        {
                            if (mes.Contains("\"event\":\"error\""))
                            {
                                lock (_senderLocker)
                                {
                                    SendLogMessage(mes, LogMessageType.Error);
                                }
                            }
                            else if (mes.StartsWith("{\"event\":\"info\""))
                            {
                                lock (_senderLocker)
                                {
                                    if (mes.Contains("\"code\":20051") || mes.Contains("\"code\":20060"))
                                    {
                                        SendLogMessage("остановка/перезапуск сервера Websocket", LogMessageType.Error);

                                        if (Disconnected != null)
                                        {
                                            Disconnected();
                                        }
                                    }
                                }
                            }
                            else if (mes.Contains("\"event\":\"auth\""))
                            {

                            }
                            else if (mes.Contains("\"os\"")) // снимок моих ордеров
                            {


                            }
                            else if(mes.Contains("\"on\""))
                            {
                                
                            }
                            else if ( mes.Contains("\"ou\"") || mes.Contains("\"oc\"")) // обновление или закрытие моего ордера
                            {
                                lock (_senderLocker)
                                {
                                    Thread.Sleep(300);

                                    var values = ParseData(mes);

                                    int numUser = _osOrders[values[0]];

                                    var order = new Order();

                                    order.NumberUser = numUser;
                                    order.SecurityNameCode = values[1];
                                    order.PortfolioNumber = values[1].Substring(3);
                                    order.Side = Convert.ToDecimal(values[2].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator)
                                                                                        , CultureInfo.InvariantCulture) > 0 ? Side.Buy : Side.Sell;
                                    order.NumberMarket = values[0];
                                    order.Price = Convert.ToDecimal(values[6].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                                    order.Volume = Math.Abs(Convert.ToDecimal(values[2].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture));

                                    order.TimeCallBack = DateTime.Parse(values[8].TrimEnd('Z'));

                                    if (values[5].Contains("EXECUTED"))
                                    {
                                        order.State = OrderStateType.Done;
                                    }
                                    else
                                    {
                                        switch (values[5])
                                        {
                                            case "ACTIVE":
                                                order.State = OrderStateType.Activ;
                                                break;

                                            case "PARTIALLY FILLED":
                                                order.State = OrderStateType.Patrial;
                                                break;

                                            case "CANCELED":
                                                order.State = OrderStateType.Cancel;
                                                _osOrders.Remove(order.NumberMarket);
                                                break;

                                            default:
                                                order.State = OrderStateType.None;
                                                break;
                                        }
                                    }
                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(order);
                                    }
                                }
                            }
                            else if (mes.Contains("[0,\"tu\",[")) // новая моя сделка
                            {
                                lock (_senderLocker)
                                {
                                    // [0,"tu",["5809001-IOTBTC",275774974,"IOTBTC",1533415991,15073784940,13,0.00012184,"EXCHANGE LIMIT",0.00012184,-0.026,"IOT"]] моя сделка
                                    var valuesMyTrade = ParseData(mes);

                                    Thread.Sleep(300);

                                    MyTrade myTrade = new MyTrade();
                                    myTrade.Price = Convert.ToDecimal(valuesMyTrade[6].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                                    myTrade.NumberTrade = valuesMyTrade[1];
                                    myTrade.SecurityNameCode = valuesMyTrade[2];
                                    myTrade.Side = valuesMyTrade[5].Contains("-") ? Side.Sell : Side.Buy;
                                    myTrade.Volume = Math.Abs(Convert.ToDecimal(valuesMyTrade[5].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture));
                                    myTrade.Time = new DateTime(1970, 01, 01) + TimeSpan.FromSeconds(Convert.ToDouble(valuesMyTrade[3]));
                                    myTrade.NumberOrderParent = valuesMyTrade[4];

                                    if (MyTradeEvent != null)
                                    {
                                        MyTradeEvent(myTrade);
                                    }
                                }
                            }
                            else if (mes.Contains("\"ws\"")) // снимок портфелей
                            {
                                lock (_senderLocker)
                                {
                                    var res = Walets.FromJson(mes)[2].AllWallets;

                                    if (NewPortfolio != null)
                                    {
                                        NewPortfolio(res);
                                    }
                                }
                            }
                            else if (mes.Contains("\"wu\"")) // обновление портфеля
                            {
                                lock (_senderLocker)
                                {
                                    var res = WalletUpdate.FromJson(mes)[2].UpdatedWallet; ;

                                    if (UpdatePortfolio != null)
                                    {
                                        UpdatePortfolio(res);
                                    }
                                }
                            }
                            else if (mes.Contains("[0,\"ps\",[")) // снимок позиций
                            {
                                
                            }
                            else if (mes.Contains("\"pn\"")|| mes.Contains("\"pu\"")|| mes.Contains("\"pc\"")) // снимок позиций
                            {

                            }
                            else if (mes.Contains("\"event\":\"subscribed\"") && mes.Contains("\"chanId\""))
                            {
                                lock (_senderLocker)
                                {
                                    // информируем об успешной подписке
                                    var info = JsonConvert.DeserializeAnonymousType(mes, new SubscriptionInformation());

                                    if (info.channel == "trades")
                                    {
                                        _subscribedTradesSecurity.Add(info.pair, info.chanId);
                                    }

                                    if (info.channel == "book")
                                    {
                                        _subscribedBooksSecurity.Add(info.pair, info.chanId);
                                    }

                                    string msgInfo =
                                        string.Format(
                                            "Инструмент {0} успешно подписан на канал : {1}, Id канала = {2}", info.pair,
                                            info.channel, info.chanId);

                                    SendLogMessage(msgInfo, LogMessageType.System);

                                }
                            }
                            else if (mes.Contains("\"tu\"")) // новый тик
                            {
                                lock (_senderLocker)
                                {
                                    var bitfinexTick = UpdateDataBitfinex.FromJson(mes);

                                    // находим бумагу которой принадлежит этот снимок
                                    var namePair =
                                        _subscribedTradesSecurity.FirstOrDefault(
                                            dic => dic.Value == Convert.ToInt32(bitfinexTick[0].Double));

                                    if (NewTradesEvent != null)
                                    {
                                        NewTradesEvent(bitfinexTick, namePair.Key);
                                    }
                                }
                            }
                            else if (mes.Contains("[["))
                            {
                                lock (_senderLocker)
                                {
                                    var countParams = NumberParametersSnapshot(mes);

                                    if (countParams == 3)
                                    {
                                        // обрабатываем снимок стакана

                                        var orderBook = BitfinexSnapshotParser.FromJson(mes);

                                        // находим бумагу которой принадлежит этот снимок
                                        var namePair =
                                            _subscribedBooksSecurity.FirstOrDefault(
                                                dic => dic.Value == Convert.ToInt32(orderBook[0].IdChanel));

                                        if (NewMarketDepth != null)
                                        {
                                            NewMarketDepth(orderBook, namePair.Key);
                                        }
                                    }
                                }
                            }
                            else if (mes.Contains("hb"))
                            {
                                // пришло сообщение серцебиения
                            }
                            else if (!mes.Contains("[[") && !mes.Contains("\"te\"") && !mes.Contains("\"ws\""))
                            {
                                lock (_senderLocker)
                                {
                                    var bitfinexChangeOrderBook = UpdateDataBitfinex.FromJson(mes);

                                    // находим бумагу которой принадлежит этот снимок
                                    var namePair =
                                        _subscribedBooksSecurity.FirstOrDefault(
                                            dic => dic.Value == Convert.ToInt32(bitfinexChangeOrderBook[0].Double));

                                    if (UpdateMarketDepth != null)
                                    {
                                        UpdateMarketDepth(bitfinexChangeOrderBook, namePair.Key);
                                    }
                                }
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
                    SendLogMessage(exception.Message, LogMessageType.Connect);
                }                
            }
        }

        /// <summary>
        /// распарсить данные
        /// </summary>
        private string[] ParseData(string msg)
        {
            var iterFirst = msg.Substring(9);

            int index = iterFirst.Length - 2;

            var iterSecond = iterFirst.Remove(index).Replace("\"", "");

            string[] values = iterSecond.Split(',');

            return values;
        }

        /// <summary>
        /// находит кол-во параметров в подмассиве чтобы определить к какому каналу принадлежит сообщение
        /// </summary>
        /// <param name="msg">строка которую нужно распарсить</param>
        /// <returns>кол-во параметров, если 3 значит это снимок стакана, если 4 значит снимок трейдов</returns>
        private int NumberParametersSnapshot(string msg)
        {
            // Находим индекс начала первого подмассива поиском двойных квадратных открывающихся скобок
            int indexStart = msg.IndexOf("[[", StringComparison.Ordinal);

            // находим индекс конца подмассива
            int infexEnd = msg.IndexOf("]", StringComparison.Ordinal);

            // вырезаем первый подмассив
            var str = msg.Substring(indexStart, infexEnd-indexStart);

            // делим его по запятым
            var res = str.Split(new char[] { ',' });
            
            // отправляем кол-во параметров
            int countParams = res.Length;

            return countParams;
        }

        #endregion

        #region исходящие события

        /// <summary>
        /// новые мои ордера
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// новые мои сделки
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// новые портфели
        /// </summary>
        public event Action<List<List<WaletWalet>>> NewPortfolio;

        /// <summary>
        /// обновились портфели
        /// </summary>
        public event Action<List<WalletUpdateWalletUpdate>> UpdatePortfolio;

        /// <summary>
        /// новые бумаги в системе
        /// </summary>
        public event Action<SecurityResponce> UpdatePairs;

        /// <summary>
        /// обновился стакан
        /// </summary>
        public event Action<List<DataObject>, string> NewMarketDepth;

        /// <summary>
        /// обновился стакан
        /// </summary>
        public event Action<List<ChangedElement>,string> UpdateMarketDepth;

        /// <summary>
        /// обновились тики
        /// </summary>
        public event Action<List<ChangedElement>, string> NewTradesEvent;

        /// <summary>
        /// соединение с API установлено
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// соединение с API разорвано
        /// </summary>
        public event Action Disconnected;

        #endregion

    }


}
