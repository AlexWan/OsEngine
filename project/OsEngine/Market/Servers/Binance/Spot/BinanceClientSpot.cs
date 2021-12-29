using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using WebSocket4Net;
using TradeResponse = OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity.TradeResponse;

namespace OsEngine.Market.Servers.Binance.Spot
{
    public class BinanceClient
    {
        public BinanceClient(string pubKey, string secKey)
        {
            ApiKey = pubKey;
            SecretKey = secKey;
        }

        public string ApiKey;
        public string SecretKey;

        private string _baseUrl = "https://api.binance.com/api";

        /// <summary>
        /// connecto to the exchange
        /// установить соединение с биржей 
        /// </summary>
        public void Connect()
        {
            if (string.IsNullOrEmpty(ApiKey) ||
                string.IsNullOrEmpty(SecretKey))
            {
                return;
            }

            // check server availability for HTTP communication with it / проверяем доступность сервера для HTTP общения с ним
            Uri uri = new Uri(_baseUrl + "/v1/time");
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

            if (Connected != null)
            {
                Connected();
            }

            CreateDataStreams();

            _timeStart = DateTime.Now;
        }

        private string _spotListenKey = "";

        private string _marginListenKey = "";

        private WebSocket _spotSocketClient;

        private WebSocket _marginSocketClient;

        private void CreateDataStreams()
        {
            _spotSocketClient = CreateUserDataStream("api/v1/userDataStream", BinanceExchangeType.SpotExchange);
            _wsStreams.Add("userDataStream", _spotSocketClient);

            if (_spotSocketClient == null)
            {
                Thread.Sleep(5000);
                return;
            }
            _spotSocketClient.MessageReceived += delegate (object sender, MessageReceivedEventArgs args)
            {
                UserDataMessageHandler(sender, args, BinanceExchangeType.SpotExchange);
            };

            try
            {
                _marginSocketClient = CreateUserDataStream("/sapi/v1/userDataStream", BinanceExchangeType.MarginExchange);
                _wsStreams.Add("userDataStreamMargine", _marginSocketClient);
                _marginSocketClient.MessageReceived += delegate (object sender, MessageReceivedEventArgs args)
                {
                    UserDataMessageHandler(sender, args, BinanceExchangeType.MarginExchange);
                };
            }
            catch
            {
                _notMargineAccount = true;
            }


            Thread keepalive = new Thread(KeepaliveUserDataStream);
            keepalive.CurrentCulture = new CultureInfo("ru-RU");
            keepalive.IsBackground = true;
            keepalive.Start();

            Thread converter = new Thread(Converter);
            converter.CurrentCulture = new CultureInfo("ru-RU");
            converter.IsBackground = true;
            converter.Start();

            Thread converterUserData = new Thread(ConverterUserData);
            converterUserData.CurrentCulture = new CultureInfo("ru-RU");
            converterUserData.IsBackground = true;
            converterUserData.Start();
        }

        private bool _notMargineAccount;

        /// <summary>
        /// create user data thread
        /// создать поток пользовательских данных
        /// </summary>
        /// <returns></returns>
        private WebSocket CreateUserDataStream(string url, BinanceExchangeType exType)
        {
            try
            {
                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.POST, url, null, false);
                string urlStr = "";

                if (exType == BinanceExchangeType.SpotExchange)
                {
                    _spotListenKey = JsonConvert.DeserializeAnonymousType(res, new ListenKey()).listenKey;
                    urlStr = "wss://stream.binance.com:9443/ws/" + _spotListenKey;
                }
                else if (exType == BinanceExchangeType.MarginExchange)
                {
                    _marginListenKey = JsonConvert.DeserializeAnonymousType(res, new ListenKey()).listenKey;
                    urlStr = "wss://stream.binance.com:9443/ws/" + _marginListenKey;
                }

                WebSocket client = new WebSocket(urlStr); //create a web socket / создаем вебсокет

                client.Opened += Connect;
                client.Closed += Disconnect;
                client.Error += WsError;
                client.Open();

                return client;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Connect);
                return null;
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
                foreach (var ws in _wsStreams)
                {
                    ws.Value.Opened -= new EventHandler(Connect);
                    ws.Value.Closed -= new EventHandler(Disconnect);
                    ws.Value.Error -= new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(WsError);
                    ws.Value.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(GetRes);

                    ws.Value.Close();
                    ws.Value.Dispose();
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                CloseUserDataStream();
            }
            catch
            {
                // ignore
            }

            IsConnected = false;

            if (Disconnected != null)
            {
                Disconnected();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// there was a request to clear the object
        /// произошёл запрос на очистку объекта
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// queue of new messages from the exchange server
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<BinanceUserMessage> _newUserDataMessage = new ConcurrentQueue<BinanceUserMessage>();

        /// <summary>
        /// user data handler
        /// обработчик пользовательских данных
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserDataMessageHandler(object sender, MessageReceivedEventArgs e, BinanceExchangeType type)
        {
            if (_isDisposed)
            {
                return;
            }
            BinanceUserMessage message = new BinanceUserMessage();
            message.MessageStr = e.Message;
            message.ExchangeType = type;

            _newUserDataMessage.Enqueue(message);
        }

        /// <summary>
        /// close user data stream
        /// закрыть поток пользовательских данных
        /// </summary>
        private void CloseUserDataStream()
        {
            if (_spotListenKey != "")
            {
                CreateQuery(BinanceExchangeType.SpotExchange, Method.DELETE, "api/v1/userDataStream", new Dictionary<string, string>() { { "listenKey=", _spotListenKey } }, false);
            }
            if (_marginListenKey != "")
            {
                CreateQuery(BinanceExchangeType.MarginExchange, Method.DELETE, "sapi/v1/userDataStream", new Dictionary<string, string>() { { "listenKey=", _marginListenKey } }, false);
            }
        }

        private DateTime _timeStart;

        /// <summary>
        /// every half hour we send the message that the stream does not close
        /// каждые полчаса отправляем сообщение, чтобы поток не закрылся
        /// </summary>
        private void KeepaliveUserDataStream()
        {
            while (true)
            {
                Thread.Sleep(30000);


                if (_spotListenKey == "" ||
                    _marginListenKey == "")
                {
                    continue;
                }


                if (_timeStart.AddMinutes(25) < DateTime.Now)
                {
                    _timeStart = DateTime.Now;

                    CreateQuery(BinanceExchangeType.SpotExchange, Method.PUT,
                        "api/v1/userDataStream", new Dictionary<string, string>()
                            { { "listenKey=", _spotListenKey } }, false);

                    if(_notMargineAccount == false)
                    {
                        CreateQuery(BinanceExchangeType.MarginExchange, Method.PUT,
                            "sapi/v1/userDataStream", new Dictionary<string, string>()
                                { { "listenKey=", _marginListenKey } }, false);
                    }
                }
            }
        }

        /// <summary>
        /// shows whether connection works
        /// работает ли соединение
        /// </summary>
        public bool IsConnected;

        private object _lock = new object();

        public void GetBalanceSpot()
        {
            lock (_lock)
            {
                try
                {
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, "api/v3/account", null, true);

                    if (res == null)
                    {
                        return;
                    }

                    AccountResponse resp = JsonConvert.DeserializeAnonymousType(res, new AccountResponse());
                    if (NewPortfolioSpot != null)
                    {
                        NewPortfolioSpot(resp);
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        public void GetBalanceMargin()
        {
            lock (_lock)
            {
                try
                {
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, "/sapi/v1/margin/account", null, true);

                    if (res == null)
                    {
                        return;
                    }

                    AccountResponseMargin resp = JsonConvert.DeserializeAnonymousType(res, new AccountResponseMargin());

                    if (NewPortfolioMargin != null)
                    {
                        NewPortfolioMargin(resp);
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }

                // Query Margin Account Details (USER_DATA)
                // GET /sapi/v1/margin/account (HMAC SHA256)
            }
        }

        public event Action<AccountResponse> NewPortfolioSpot;

        public event Action<AccountResponseMargin> NewPortfolioMargin;

        /// <summary>
        /// take securities
        /// взять бумаги
        /// </summary>
        public void GetSecurities()
        {
            lock (_lock)
            {
                try
                {
                    //Get All Margin Pairs (MARKET_DATA)
                    //GET /sapi/v1/margin/allPairs

                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, "api/v1/exchangeInfo", null, false);

                    SecurityResponce secResp = JsonConvert.DeserializeAnonymousType(res, new SecurityResponce());

                    if (UpdatePairs != null)
                    {
                        UpdatePairs(secResp);
                    }

                }
                catch (Exception ex)
                {
                    if (LogMessageEvent != null)
                    {
                        LogMessageEvent(ex.ToString(), LogMessageType.Error);
                    }
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
        /// convert JSON to candles
        /// преобразует JSON в свечи
        /// </summary>
        /// <param name="jsonCandles"></param>
        /// <returns></returns>
        private List<Candle> _deserializeCandles(string jsonCandles)
        {
            try
            {
                lock (_candleLocker)
                {
                    string res = jsonCandles.Trim(new char[] { '[', ']' });
                    var res2 = res.Split(new char[] { ']' });

                    _candles = new List<Candle>();

                    Candle newCandle;

                    for (int i = 0; i < res2.Length; i++)
                    {
                        if (i != 0)
                        {
                            string upd = res2[i].Substring(2);
                            var param = upd.Split(new char[] { ',' });

                            newCandle = new Candle();
                            newCandle.TimeStart = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(param[0]));
                            newCandle.Low = Convert.ToDecimal(param[3].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                            newCandle.High = Convert.ToDecimal(param[2].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                            newCandle.Open = Convert.ToDecimal(param[1].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                            newCandle.Close = Convert.ToDecimal(param[4].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                            newCandle.Volume = Convert.ToDecimal(param[5].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);

                            _candles.Add(newCandle);
                        }
                        else
                        {
                            var param = res2[i].Split(new char[] { ',' });

                            newCandle = new Candle();
                            newCandle.TimeStart = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(param[0]));
                            newCandle.Low = Convert.ToDecimal(param[3].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                            newCandle.High = Convert.ToDecimal(param[2].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                            newCandle.Open = Convert.ToDecimal(param[1].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                            newCandle.Close = Convert.ToDecimal(param[4].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                            newCandle.Volume = Convert.ToDecimal(param[5].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);

                            _candles.Add(newCandle);
                        }

                    }

                    return _candles;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        public List<Candle> GetCandlesForTimes(string nameSec, TimeSpan tf, DateTime timeStart, DateTime timeEnd)
        {
            DateTime yearBegin = new DateTime(1970, 1, 1);

            var timeStampStart = timeStart - yearBegin;
            var r = timeStampStart.TotalMilliseconds;
            string startTime = Convert.ToInt64(r).ToString();

            var timeStampEnd = timeEnd - yearBegin;
            var rEnd = timeStampEnd.TotalMilliseconds;
            string endTime = Convert.ToInt64(rEnd).ToString();


            string needTf = "";

            switch ((int)tf.TotalMinutes)
            {
                case 1:
                    needTf = "1m";
                    break;
                case 2:
                    needTf = "2m";
                    break;
                case 3:
                    needTf = "3m";
                    break;
                case 5:
                    needTf = "5m";
                    break;
                case 10:
                    needTf = "10m";
                    break;
                case 15:
                    needTf = "15m";
                    break;
                case 20:
                    needTf = "20m";
                    break;
                case 30:
                    needTf = "30m";
                    break;
                case 45:
                    needTf = "45m";
                    break;
                case 60:
                    needTf = "1h";
                    break;
                case 120:
                    needTf = "2h";
                    break;
            }

            string endPoint = "api/v1/klines";

            if (needTf != "2m" && needTf != "10m" && needTf != "20m" && needTf != "45m")
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=" + nameSec.ToUpper(), "&interval=" + needTf + "&startTime=" + startTime + "&endTime=" + endTime);

                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                if (res == "")
                {
                    return null;
                }

                var candles = _deserializeCandles(res);
                return candles;

            }
            else
            {
                if (needTf == "2m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=1m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);

                    var newCandles = BuildCandles(candles, 2, 1);
                    return newCandles;
                }
                else if (needTf == "10m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 10, 5);
                    return newCandles;
                }
                else if (needTf == "20m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 20, 5);
                    return newCandles;
                }
                else if (needTf == "45m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=15m" + "&startTime=" + startTime + "&endTime=" + endTime);
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 45, 15);
                    return newCandles;
                }
            }

            return null;
        }

        public List<Trade> GetTickHistoryToSecurity(Security security, string lastId)
        {
            try
            {

                List<Trade> newTrades = new List<Trade>();

                Dictionary<string, string> param = new Dictionary<string, string>();

                if (string.IsNullOrEmpty(lastId) == false)
                {
                    param.Add("symbol=" + security.Name, "&limit=1000" + "&fromId=" + lastId);
                }
                else
                {
                    param.Add("symbol=" + security.Name, "&limit=1000");
                }


                string endPoint = "api/v1/historicalTrades";

                var res2 = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);

                List<HistoryTrade> tradeHistory = JsonConvert.DeserializeAnonymousType(res2, new List<HistoryTrade>());

                //tradeHistory.Reverse();

                foreach (var trades in tradeHistory)
                {
                    Trade trade = new Trade();
                    trade.SecurityNameCode = security.Name;
                    trade.Price = trades.price.ToDecimal();

                    trade.Id = trades.id.ToString();
                    trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(trades.time));
                    trade.Volume =
                            trades.qty.ToDecimal();
                    trade.Side = Convert.ToBoolean(trades.isBuyerMaker) == true ? Side.Buy : Side.Sell;

                    newTrades.Add(trade);
                }

                return newTrades;

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

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
                    needTf = "1m";
                    break;
                case 2:
                    needTf = "2m";
                    break;
                case 3:
                    needTf = "3m";
                    break;
                case 5:
                    needTf = "5m";
                    break;
                case 10:
                    needTf = "10m";
                    break;
                case 15:
                    needTf = "15m";
                    break;
                case 20:
                    needTf = "20m";
                    break;
                case 30:
                    needTf = "30m";
                    break;
                case 45:
                    needTf = "45m";
                    break;
                case 60:
                    needTf = "1h";
                    break;
                case 120:
                    needTf = "2h";
                    break;
                case 240:
                    needTf = "4h";
                    break;
                case 1440:
                    needTf = "1d";
                    break;
            }

            string endPoint = "api/v1/klines";

            if (needTf != "2m" && needTf != "10m" && needTf != "20m" && needTf != "45m")
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=" + nameSec.ToUpper(), "&interval=" + needTf);

                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);

                var candles = _deserializeCandles(res);
                return candles;

            }
            else
            {
                if (needTf == "2m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=1m");
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);

                    var newCandles = BuildCandles(candles, 2, 1);
                    return newCandles;
                }
                else if (needTf == "10m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m");
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 10, 5);
                    return newCandles;
                }
                else if (needTf == "20m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=5m");
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 20, 5);
                    return newCandles;
                }
                else if (needTf == "45m")
                {
                    var param = new Dictionary<string, string>();
                    param.Add("symbol=" + nameSec.ToUpper(), "&interval=15m");
                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 45, 15);
                    return newCandles;
                }
            }

            return null;
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

        #region Аутентификация запроса

        /// <summary>
        /// multi-threaded access locker to http-requests
        /// блокиратор многопоточного доступа к http запросам
        /// </summary>
        private object _queryHttpLocker = new object();

        RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(100));

        /// <summary>
        /// method sends a request and returns a response from the server
        /// метод отправляет запрос и возвращает ответ от сервера
        /// </summary>
        public string CreateQuery(BinanceExchangeType startUri, Method method, string endpoint, Dictionary<string, string> param = null, bool auth = false)
        {
            try
            {
                lock (_queryHttpLocker)
                {
                    _rateGate.WaitToProceed();
                    string fullUrl = "";

                    if (param != null)
                    {
                        fullUrl += "?";

                        foreach (var onePar in param)
                        {
                            fullUrl += onePar.Key + onePar.Value;
                        }
                    }

                    if (auth)
                    {
                        string message = "";

                        string timeStamp = GetNonce();

                        message += "timestamp=" + timeStamp;

                        if (fullUrl == "")
                        {
                            fullUrl = "?timestamp=" + timeStamp + "&signature=" + CreateSignature(message);
                        }
                        else
                        {
                            message = fullUrl + "&timestamp=" + timeStamp;
                            fullUrl += "&timestamp=" + timeStamp + "&signature=" + CreateSignature(message.Trim('?'));
                        }
                    }

                    var request = new RestRequest(endpoint + fullUrl, method);
                    request.AddHeader("X-MBX-APIKEY", ApiKey);

                    string baseUrl = "";

                    if (startUri == BinanceExchangeType.SpotExchange)
                    {
                        baseUrl = "https://api.binance.com";
                    }
                    else if (startUri == BinanceExchangeType.MarginExchange)
                    {
                        baseUrl = "https://api.binance.com";
                    }

                    var response = new RestClient(baseUrl).Execute(request).Content;

                    if (response.Contains("code"))
                    {
                        var error = JsonConvert.DeserializeAnonymousType(response, new ErrorMessage());
                        throw new Exception(error.msg);
                    }

                    return response;
                }
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("This listenKey does not exist"))
                {
                    IsConnected = false;
                    Disconnected?.Invoke();
                }

                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private string GetNonce()
        {
            var resTime = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, "api/v1/time", null, false);
            var result = JsonConvert.DeserializeAnonymousType(resTime, new BinanceTime());
            return (result.serverTime + 500).ToString();

            /*DateTime yearBegin = new DateTime(1970, 1, 1);
            var res = DateTime.UtcNow;
            var timeStamp = DateTime.UtcNow - yearBegin;
            var r = timeStamp.TotalMilliseconds;
            var re = Convert.ToInt64(r);
            return re.ToString();*/
        }

        private string CreateSignature(string message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var keyBytes = Encoding.UTF8.GetBytes(SecretKey);
            var hash = new HMACSHA256(keyBytes);
            var computedHash = hash.ComputeHash(messageBytes);
            return BitConverter.ToString(computedHash).Replace("-", "").ToLower();
        }

        private byte[] Hmacsha256(byte[] keyByte, byte[] messageBytes)
        {
            using (var hash = new HMACSHA256(keyByte))
            {
                return hash.ComputeHash(messageBytes);
            }
        }

        #endregion

        // work with orders работа с ордерами

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        /// <param name="order"></param>
        public void ExecuteOrder(Order order)
        {
            if (order.PortfolioNumber == "BinanceSpot")
            {
                ExecuteOrderOnSpotExchange(order);
            }
            else if (order.PortfolioNumber == "BinanceMargin")
            {
                ExecuteOrderOnMarginExchange(order);
            }
        }

        private void ExecuteOrderOnMarginExchange(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    if (IsConnected == false)
                    {
                        return;
                    }

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=", order.SecurityNameCode.ToUpper());
                    param.Add("&side=", order.Side == Side.Buy ? "BUY" : "SELL");
                    param.Add("&type=", "LIMIT");
                    param.Add("&timeInForce=", "GTC");
                    param.Add("&newClientOrderId=", "x-RKXTQ2AK" + order.NumberUser.ToString());

                    if (order.PositionConditionType == OrderPositionConditionType.Open)
                    {
                        param.Add("&sideEffectType=", "MARGIN_BUY");
                    }
                    else
                    {
                        param.Add("&sideEffectType=", "AUTO_REPAY");
                    }

                    param.Add("&quantity=",
                        order.Volume.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));
                    param.Add("&price=",
                        order.Price.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));

                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.POST, "/sapi/v1/margin/order", param, true);

                    if (res != null && res.Contains("clientOrderId"))
                    {
                        SendLogMessage(res, LogMessageType.Trade);
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

        private void ExecuteOrderOnSpotExchange(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    if (IsConnected == false)
                    {
                        return;
                    }

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=", order.SecurityNameCode.ToUpper());
                    param.Add("&side=", order.Side == Side.Buy ? "BUY" : "SELL");
                    param.Add("&type=", "LIMIT");
                    param.Add("&timeInForce=", "GTC");
                    param.Add("&newClientOrderId=", "x-RKXTQ2AK" + order.NumberUser.ToString());
                    param.Add("&quantity=",
                        order.Volume.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));
                    param.Add("&price=",
                        order.Price.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, "."));

                    var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.POST, "api/v3/order", param, true);

                    if (res != null && res.Contains("clientOrderId"))
                    {
                        SendLogMessage(res, LogMessageType.Trade);
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
                    if (string.IsNullOrEmpty(order.NumberMarket))
                    {
                        Order onBoard = GetOrderState(order);

                        if (onBoard == null)
                        {
                            order.State = OrderStateType.Fail;
                            SendLogMessage("При отзыве ордера не нашли такого на бирже. считаем что он уже отозван",
                                LogMessageType.Error);
                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(order);
                            }
                            return;
                        }

                        order.NumberMarket = onBoard.NumberMarket;
                        order = onBoard;
                    }

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=", order.SecurityNameCode.ToUpper());
                    param.Add("&orderId=", order.NumberMarket);

                    if (order.PortfolioNumber == "BinanceSpot")
                    {
                        CreateQuery(BinanceExchangeType.SpotExchange, Method.DELETE, "api/v3/order", param, true);
                    }
                    else if (order.PortfolioNumber == "BinanceMargin")
                    {
                        CreateQuery(BinanceExchangeType.SpotExchange, Method.DELETE, "/sapi/v1/margin/order", param, true);
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
            List<string> namesSec = new List<string>();

            for (int i = 0; i < oldOpenOrders.Count; i++)
            {
                if (namesSec.Find(name => name.Contains(oldOpenOrders[i].SecurityNameCode)) == null)
                {
                    namesSec.Add(oldOpenOrders[i].SecurityNameCode);
                }
            }


            string endPoint = "/api/v3/allOrders";

            List<HistoryOrderReport> allOrders = new List<HistoryOrderReport>();

            for (int i = 0; i < namesSec.Count; i++)
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=", namesSec[i].ToUpper());
                //param.Add("&recvWindow=" , "100");
                //param.Add("&limit=", GetNonce());
                param.Add("&limit=", "500");
                //"symbol={symbol.ToUpper()}&recvWindow={recvWindow}"

                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, true);

                if (res == null)
                {
                    continue;
                }

                HistoryOrderReport[] orders = JsonConvert.DeserializeObject<HistoryOrderReport[]>(res);

                if (orders != null && orders.Length != 0)
                {
                    allOrders.AddRange(orders);
                }
            }

            for (int i = 0; i < oldOpenOrders.Count; i++)
            {
                HistoryOrderReport myOrder = allOrders.Find(ord => ord.orderId == oldOpenOrders[i].NumberMarket);

                if (myOrder == null)
                {
                    continue;
                }

                if (myOrder.status == "NEW")
                { // order is active. Do nothing / ордер активен. Ничего не делаем
                    continue;
                }

                else if (myOrder.status == "FILLED" ||
                    myOrder.status == "PARTIALLY_FILLED")
                { // order executed / ордер исполнен

                    MyTrade trade = new MyTrade();
                    trade.NumberOrderParent = oldOpenOrders[i].NumberMarket;
                    trade.NumberTrade = NumberGen.GetNumberOrder(StartProgram.IsOsTrader).ToString();
                    trade.SecurityNameCode = oldOpenOrders[i].SecurityNameCode;
                    trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(myOrder.updateTime));
                    trade.Side = oldOpenOrders[i].Side;

                    if (MyTradeEvent != null)
                    {
                        MyTradeEvent(trade);
                    }
                }
                else
                {
                    Order newOrder = new Order();
                    newOrder.NumberMarket = oldOpenOrders[i].NumberMarket;
                    newOrder.NumberUser = oldOpenOrders[i].NumberUser;
                    newOrder.SecurityNameCode = oldOpenOrders[i].SecurityNameCode;
                    newOrder.State = OrderStateType.Cancel;

                    newOrder.Volume = oldOpenOrders[i].Volume;
                    newOrder.VolumeExecute = oldOpenOrders[i].VolumeExecute;
                    newOrder.Price = oldOpenOrders[i].Price;
                    newOrder.TypeOrder = oldOpenOrders[i].TypeOrder;
                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(myOrder.updateTime));
                    newOrder.TimeCancel = newOrder.TimeCallBack;
                    newOrder.ServerType = ServerType.Binance;
                    newOrder.PortfolioNumber = oldOpenOrders[i].PortfolioNumber;

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(newOrder);
                    }
                }
            }
            return true;
        }

        private Order GetOrderState(Order oldOrder)
        {
            List<string> namesSec = new List<string>();
            namesSec.Add(oldOrder.SecurityNameCode);

            string endPoint = "/api/v3/allOrders";

            List<HistoryOrderReport> allOrders = new List<HistoryOrderReport>();

            for (int i = 0; i < namesSec.Count; i++)
            {
                var param = new Dictionary<string, string>();
                param.Add("symbol=", namesSec[i].ToUpper());
                //param.Add("&recvWindow=" , "100");
                //param.Add("&limit=", GetNonce());
                param.Add("&limit=", "500");
                //"symbol={symbol.ToUpper()}&recvWindow={recvWindow}"

                var res = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, true);

                if (res == null)
                {
                    continue;
                }

                HistoryOrderReport[] orders = JsonConvert.DeserializeObject<HistoryOrderReport[]>(res);

                if (orders != null && orders.Length != 0)
                {
                    allOrders.AddRange(orders);
                }
            }

            HistoryOrderReport orderOnBoard =
                allOrders.Find(ord => ord.clientOrderId == oldOrder.NumberUser.ToString());

            if (orderOnBoard == null)
            {
                return null;
            }

            Order newOrder = new Order();
            newOrder.NumberMarket = orderOnBoard.orderId;
            newOrder.NumberUser = oldOrder.NumberUser;
            newOrder.SecurityNameCode = oldOrder.SecurityNameCode;
            newOrder.State = OrderStateType.Cancel;

            newOrder.Volume = oldOrder.Volume;
            newOrder.VolumeExecute = oldOrder.VolumeExecute;
            newOrder.Price = oldOrder.Price;
            newOrder.TypeOrder = oldOrder.TypeOrder;
            newOrder.TimeCallBack = oldOrder.TimeCallBack;
            newOrder.TimeCancel = newOrder.TimeCallBack;
            newOrder.ServerType = ServerType.Binance;
            newOrder.PortfolioNumber = oldOrder.PortfolioNumber;

            if (orderOnBoard.status == "NEW" ||
                orderOnBoard.status == "PARTIALLY_FILLED")
            { // order is active. Do nothing / ордер активен. Ничего не делаем
                newOrder.State = OrderStateType.Activ;
            }
            else if (orderOnBoard.status == "FILLED")
            {
                newOrder.State = OrderStateType.Done;
            }
            else
            {
                newOrder.State = OrderStateType.Cancel;
            }

            if (MyOrderEvent != null)
            {
                MyOrderEvent(newOrder);
            }

            return newOrder;
        }

        // stream data from WEBSOCKET потоковые данные из WEBSOCKET

        /// <summary>
        /// WebSocket client
        /// клиент вебсокет
        /// </summary>
        private WebSocket _wsClient;

        /// <summary>
        /// takes messages that came through ws and puts them in a general queue
        /// берет пришедшие через ws сообщения и кладет их в общую очередь
        /// </summary>        
        private void GetRes(object sender, MessageReceivedEventArgs e)
        {
            if (_isDisposed == true)
            {
                return;
            }
            _newMessage.Enqueue(e.Message);
        }

        /// <summary>
        /// ws-connection is opened
        /// соединение по ws открыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Connect(object sender, EventArgs e)
        {
            IsConnected = true;
        }

        /// <summary>
        /// ws-connection is closed
        /// соединение по ws закрыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Disconnect(object sender, EventArgs e)
        {
            if (IsConnected)
            {
                IsConnected = false;

                _wsStreams.Clear();

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
            if (e.ToString().Contains("Unknown order"))
            {
                return;
            }

            SendLogMessage("Ошибка из ws4net :" + e, LogMessageType.Error);
        }

        /// <summary>
        /// queue of new messages from the exchange server
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();

        /// <summary>
        /// data stream collection
        /// коллекция потоков данных
        /// </summary>
        private Dictionary<string, WebSocket> _wsStreams = new Dictionary<string, WebSocket>();

        /// <summary>
        /// subscribe this security to get depths and trades
        /// подписать данную бумагу на получение стаканов и трейдов
        /// </summary>
        public void SubscribleTradesAndDepths(Security security)
        {
            if (!_wsStreams.ContainsKey(security.Name))
            {
                string urlStr = "wss://stream.binance.com:9443/stream?streams="
                                + security.Name.ToLower()
                                + "@depth20/"
                                + security.Name.ToLower() + "@trade";

                _wsClient = new WebSocket(urlStr); // create web-socket / создаем вебсокет

                _wsClient.Opened += new EventHandler(Connect);

                _wsClient.Closed += new EventHandler(Disconnect);

                _wsClient.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(WsError);

                _wsClient.MessageReceived += new EventHandler<MessageReceivedEventArgs>(GetRes);

                _wsClient.Open();

                _wsStreams.Add(security.Name, _wsClient);
            }

        }

        /// <summary>
        /// takes messages from the general queue, converts them to C # classes and sends them to up
        /// берет сообщения из общей очереди, конвертирует их в классы C# и отправляет на верх
        /// </summary>
        public void ConverterUserData()
        {
            while (true)
            {
                try
                {
                    if (!_newUserDataMessage.IsEmpty)
                    {
                        BinanceUserMessage messsage;

                        if (_newUserDataMessage.TryDequeue(out messsage))
                        {
                            string mes = messsage.MessageStr;

                            if (mes.Contains("code"))
                            {
                                SendLogMessage(JsonConvert.DeserializeAnonymousType(mes, new ErrorMessage()).msg, LogMessageType.Error);
                            }

                            else if (mes.Contains("\"e\"" + ":" + "\"executionReport\""))
                            {
                                var order = JsonConvert.DeserializeAnonymousType(mes, new ExecutionReport());

                                string orderNumUser = order.C.Replace("x-RKXTQ2AK", "");

                                if (string.IsNullOrEmpty(orderNumUser) ||
                                    orderNumUser == "null")
                                {
                                    orderNumUser = order.c.Replace("x-RKXTQ2AK", "");
                                }

                                try
                                {
                                    Convert.ToInt32(orderNumUser);
                                }
                                catch (Exception)
                                {
                                    continue;
                                }

                                if (order.x == "NEW")
                                {
                                    Order newOrder = new Order();
                                    newOrder.SecurityNameCode = order.s;
                                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(order.E));
                                    newOrder.NumberUser = Convert.ToInt32(orderNumUser);

                                    newOrder.NumberMarket = order.i.ToString();
                                    //newOrder.PortfolioNumber = order.PortfolioNumber; добавить в сервере
                                    newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                                    newOrder.State = OrderStateType.Activ;
                                    newOrder.Volume = order.q.ToDecimal();
                                    newOrder.Price = order.p.ToDecimal();
                                    newOrder.ServerType = ServerType.Binance;
                                    newOrder.PortfolioNumber = newOrder.SecurityNameCode;

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(newOrder);
                                    }
                                }
                                else if (order.x == "CANCELED")
                                {
                                    Order newOrder = new Order();
                                    newOrder.SecurityNameCode = order.s;
                                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(order.E));
                                    newOrder.TimeCancel = newOrder.TimeCallBack;
                                    newOrder.NumberUser = Convert.ToInt32(orderNumUser);
                                    newOrder.NumberMarket = order.i.ToString();
                                    newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                                    newOrder.State = OrderStateType.Cancel;
                                    newOrder.Volume = order.q.ToDecimal();
                                    newOrder.Price = order.p.ToDecimal();
                                    newOrder.ServerType = ServerType.Binance;
                                    newOrder.PortfolioNumber = newOrder.SecurityNameCode;

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(newOrder);
                                    }
                                }
                                else if (order.x == "REJECTED")
                                {
                                    Order newOrder = new Order();
                                    newOrder.SecurityNameCode = order.s;
                                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(order.E));
                                    newOrder.NumberUser = Convert.ToInt32(orderNumUser);
                                    newOrder.NumberMarket = order.i.ToString();
                                    newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                                    newOrder.State = OrderStateType.Fail;
                                    newOrder.Volume = order.q.ToDecimal();
                                    newOrder.Price = order.p.ToDecimal();
                                    newOrder.ServerType = ServerType.Binance;
                                    newOrder.PortfolioNumber = newOrder.SecurityNameCode;

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(newOrder);
                                    }
                                }
                                else if (order.x == "TRADE")
                                {

                                    MyTrade trade = new MyTrade();
                                    trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(order.T));
                                    trade.NumberOrderParent = order.i.ToString();
                                    trade.NumberTrade = order.t.ToString();
                                    trade.Volume = order.l.ToDecimal();
                                    trade.Price = order.L.ToDecimal();
                                    trade.SecurityNameCode = order.s;
                                    trade.Side = order.S == "BUY" ? Side.Buy : Side.Sell;

                                    if (MyTradeEvent != null)
                                    {
                                        MyTradeEvent(trade);
                                    }
                                }
                                else if (order.x == "EXPIRED")
                                {
                                    Order newOrder = new Order();
                                    newOrder.SecurityNameCode = order.s;
                                    newOrder.TimeCallBack = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(order.E));
                                    newOrder.TimeCancel = newOrder.TimeCallBack;
                                    newOrder.NumberUser = Convert.ToInt32(orderNumUser);
                                    newOrder.NumberMarket = order.i.ToString();
                                    newOrder.Side = order.S == "BUY" ? Side.Buy : Side.Sell;
                                    newOrder.State = OrderStateType.Cancel;
                                    newOrder.Volume = order.q.ToDecimal();
                                    newOrder.Price = order.p.ToDecimal();
                                    newOrder.ServerType = ServerType.Binance;
                                    newOrder.PortfolioNumber = newOrder.SecurityNameCode;

                                    if (MyOrderEvent != null)
                                    {
                                        MyOrderEvent(newOrder);
                                    }
                                }

                                continue;
                            }

                            else if (mes.Contains("\"e\"" + ":" + "\"outboundAccountPosition\""))
                            {
                                var portfolios = JsonConvert.DeserializeAnonymousType(mes, new OutboundAccountInfo());

                                if (UpdatePortfolio != null)
                                {
                                    if (messsage.ExchangeType == BinanceExchangeType.SpotExchange)
                                    {
                                        UpdatePortfolio(portfolios, BinanceExchangeType.SpotExchange);
                                    }
                                    if (messsage.ExchangeType == BinanceExchangeType.MarginExchange)
                                    {
                                        UpdatePortfolio(portfolios, BinanceExchangeType.MarginExchange);
                                    }
                                }
                                continue;
                            }
                        }
                    }
                    else
                    {
                        if (_isDisposed)
                        {
                            return;
                        }
                        Thread.Sleep(1);
                    }
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }

            }
        }

        /// <summary>
        /// takes messages from the general queue, converts them to C # classes and sends them to up
        /// берет сообщения из общей очереди, конвертирует их в классы C# и отправляет на верх
        /// </summary>
        public void Converter()
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
                            if (mes.Contains("error"))
                            {
                                SendLogMessage(mes, LogMessageType.Error);
                            }

                            else if (mes.Contains("\"e\"" + ":" + "\"trade\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new TradeResponse());

                                if (NewTradesEvent != null)
                                {
                                    NewTradesEvent(quotes);
                                }
                                continue;
                            }

                            else if (mes.Contains("\"lastUpdateId\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new DepthResponse());

                                if (UpdateMarketDepth != null)
                                {
                                    UpdateMarketDepth(quotes);
                                }
                                continue;
                            }
                        }
                    }
                    else
                    {
                        if (_isDisposed)
                        {
                            return;
                        }
                        Thread.Sleep(1);
                    }
                }

                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

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
        public event Action<OutboundAccountInfo, BinanceExchangeType> UpdatePortfolio;

        /// <summary>
        /// new security in the system
        /// новые бумаги в системе
        /// </summary>
        public event Action<SecurityResponce> UpdatePairs;

        /// <summary>
        /// depth updated
        /// обновился стакан
        /// </summary>
        public event Action<DepthResponse> UpdateMarketDepth;

        /// <summary>
        /// ticks updated
        /// обновились тики
        /// </summary>
        public event Action<TradeResponse> NewTradesEvent;

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

        public List<Trade> GetTickHistoryToSecurity(string security, DateTime startTime, DateTime endTime, long fromId)
        {
            try
            {
                string timeStamp = TimeManager.GetUnixTimeStampMilliseconds().ToString();
                Dictionary<string, string> param = new Dictionary<string, string>();

                if (startTime != new DateTime() && endTime != new DateTime())
                {
                    long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTime);
                    long to = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);

                    param.Add("symbol=", security);
                    param.Add("&startTime=", from.ToString());
                    param.Add("&endTime=", to.ToString());
                    param.Add("&limit=", "1000");
                }
                else if (fromId != 0)
                {
                    param.Add("symbol=", security);
                    param.Add("&fromId=", fromId.ToString());
                    param.Add("&limit=", "1000");
                }

                string endPoint = "api/v3/aggTrades";

                var res2 = CreateQuery(BinanceExchangeType.SpotExchange, Method.GET, endPoint, param, false);

                AgregatedHistoryTrade[] tradeHistory = JsonConvert.DeserializeObject<AgregatedHistoryTrade[]>(res2);

                var oldTrades = CreateTradesFromJson(security, tradeHistory);

                return oldTrades;
            }
            catch
            {
                SendLogMessage(OsLocalization.Market.Message95 + security, LogMessageType.Error);

                return null;
            }
        }

        public decimal StringToDecimal(string value)
        {
            string sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            return Convert.ToDecimal(value.Replace(",", sep).Replace(".", sep));
        }

        private List<Trade> CreateTradesFromJson(string secName, AgregatedHistoryTrade[] binTrades)
        {
            List<Trade> trades = new List<Trade>();

            foreach (var jtTrade in binTrades)
            {
                var trade = new Trade();

                trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(jtTrade.T));
                trade.Price = StringToDecimal(jtTrade.P);
                trade.MicroSeconds = 0;
                trade.Id = jtTrade.A.ToString();
                trade.Volume = Math.Abs(StringToDecimal(jtTrade.Q));
                trade.SecurityNameCode = secName;

                if (StringToDecimal(jtTrade.Q) >= 0)
                {
                    trade.Side = Side.Buy;
                    trade.Ask = 0;
                    trade.AsksVolume = 0;
                    trade.Bid = trade.Price;
                    trade.BidsVolume = trade.Volume;
                }
                else if (StringToDecimal(jtTrade.Q) < 0)
                {
                    trade.Side = Side.Sell;
                    trade.Ask = trade.Price;
                    trade.AsksVolume = trade.Volume;
                    trade.Bid = 0;
                    trade.BidsVolume = 0;
                }


                trades.Add(trade);
            }

            return trades;
        }
    }

    public class BinanceUserMessage
    {
        public string MessageStr;

        public BinanceExchangeType ExchangeType;

    }

    public enum BinanceExchangeType
    {
        SpotExchange,
        FuturesExchange,
        MarginExchange
    }

    public class AgregatedHistoryTrade
    {
        [JsonProperty("a")]
        public long A { get; set; }

        [JsonProperty("p")]
        public string P { get; set; }

        [JsonProperty("q")]
        public string Q { get; set; }

        [JsonProperty("f")]
        public long F { get; set; }

        [JsonProperty("l")]
        public long L { get; set; }

        [JsonProperty("T")]
        public long T { get; set; }

        [JsonProperty("m")]
        public bool M { get; set; }
    }
}
