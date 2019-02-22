﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Binance.BinanceEntity;
using RestSharp;
using WebSocket4Net;
using TradeResponse = OsEngine.Market.Servers.Binance.BinanceEntity.TradeResponse;

namespace OsEngine.Market.Servers.Binance
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
        /// установить соединение с биржей 
        /// </summary>
        public void Connect()
        {
            if (string.IsNullOrEmpty(ApiKey) ||
                string.IsNullOrEmpty(SecretKey))
            {
                return;
            }

            // проверяем доступность сервера для HTTP общения с ним
            Uri uri = new Uri(_baseUrl+"/v1/time");
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (Exception exception)
            {
                SendLogMessage("Сервер не доступен. Отсутствуюет интернет. ", LogMessageType.Error);
                return;
            }

            IsConnected = true;

            Connected?.Invoke();

            Thread converter = new Thread(Converter);
            converter.CurrentCulture = new CultureInfo("ru-RU");
            converter.IsBackground = true;
            converter.Start();

            Thread converterUserData = new Thread(ConverterUserData);
            converterUserData.CurrentCulture = new CultureInfo("ru-RU");
            converterUserData.IsBackground = true;
            converterUserData.Start();

            CreateUserDataStream();

            _timeStart = DateTime.Now;

            Thread keepalive = new Thread(KeepaliveUserDataStream);
            keepalive.CurrentCulture = new CultureInfo("ru-RU");
            keepalive.IsBackground = true;
            keepalive.Start();
        }

        private string _listenKey = "";

        private WebSocket _userDataClient;

        /// <summary>
        /// создать поток пользовательских данных
        /// </summary>
        /// <returns></returns>
        private bool CreateUserDataStream()
        {
            try
            {
                var res = CreateQuery(Method.POST, "api/v1/userDataStream", null, false);

                _listenKey = JsonConvert.DeserializeAnonymousType(res, new ListenKey()).listenKey;

                string urlStr = "wss://stream.binance.com:9443/ws/" + _listenKey;

                _userDataClient = new WebSocket(urlStr); //создаем вебсокет

                _userDataClient.Opened += Connect;

                _userDataClient.Closed += Disconnect;

                _userDataClient.Error += WsError;

                _userDataClient.MessageReceived += UserDataMessageHandler;

                _userDataClient.Open();

                _wsStreams.Add("userDataStream", _userDataClient);

                return true;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Connect);
                return false;
            }

        }

        /// <summary>
        /// привести программу к моменту запуска. Очистить все объекты участвующие в подключении к серверу
        /// </summary>
        public void Dispose()
        {
            foreach (var ws in _wsStreams)
            {
                ws.Value.Close();
            }
            IsConnected = false;

            Disconnected?.Invoke();

            _isDisposed = true;
        }

        /// <summary>
        /// произошёл запрос на очистку объекта
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<string> _newUserDataMessage = new ConcurrentQueue<string>();

        /// <summary>
        /// обработчик пользовательских данных
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserDataMessageHandler(object sender, MessageReceivedEventArgs e)
        {
            _newUserDataMessage.Enqueue(e.Message);
        }

        /// <summary>
        /// закрыть поток пользовательских данных
        /// </summary>
        private void CloseUserDataStream()
        {
            if (_listenKey != "")
            {
                CreateQuery(Method.DELETE, "api/v1/userDataStream", new Dictionary<string, string>() { { "listenKey=", _listenKey } }, false);
            }
        }

        private DateTime _timeStart;

        /// <summary>
        /// каждые полчаса отправляем сообщение, чтобы поток не закрылся
        /// </summary>
        private void KeepaliveUserDataStream()
        {
            while (true)
            {
                Thread.Sleep(300000);

                if (_listenKey == "")
                {
                    return;
                }

                if (_timeStart.AddMinutes(30) < DateTime.Now)
                {
                    _timeStart = DateTime.Now;

                    CreateQuery(Method.PUT, "api/v1/userDataStream", new Dictionary<string, string>() { { "listenKey=", _listenKey } }, false);
                }
            }
        }


        /// <summary>
        /// работает ли соединение
        /// </summary>
        public bool IsConnected;

        private object _lock = new object();

        /// <summary>
        /// взять баланс
        /// </summary>
        public AccountResponse GetBalance()
        {
            lock (_lock)
            {
                try
                {
                    var res = CreateQuery(Method.GET, "api/v3/account", null, true);

                    if (res == null)
                    {
                        return null;
                    }

                    AccountResponse resp = JsonConvert.DeserializeAnonymousType(res, new AccountResponse());
                    NewPortfolio?.Invoke(resp);
                    return resp;
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    return null;
                }
            }
        }

        /// <summary>
        /// взять бумаги
        /// </summary>
        public SecurityResponce GetSecurities()
        {
            lock (_lock)
            {
                try
                {
                    var res = CreateQuery(Method.GET, "api/v1/exchangeInfo", null, false);

                    SecurityResponce secResp = JsonConvert.DeserializeAnonymousType(res, new SecurityResponce());

                    UpdatePairs?.Invoke(secResp);

                    return secResp;
                }
                catch (Exception ex)
                {
                    LogMessageEvent?.Invoke(ex.ToString(), LogMessageType.Error);

                    return null;
                }
            }
        }

        /// <summary>
        /// свечи
        /// </summary>
        private List<Candle> _candles;

        private readonly object _candleLocker = new object();

        /// <summary>
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
                            newCandle.Low = Convert.ToDecimal(param[3].Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }));
                            newCandle.High = Convert.ToDecimal(param[2].Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }));
                            newCandle.Open = Convert.ToDecimal(param[1].Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }));
                            newCandle.Close = Convert.ToDecimal(param[4].Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }));
                            newCandle.Volume = Convert.ToDecimal(param[5].Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }));

                            _candles.Add(newCandle);
                        }
                        else
                        {
                            var param = res2[i].Split(new char[] { ',' });

                            newCandle = new Candle();
                            newCandle.TimeStart = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(param[0]));
                            newCandle.Low = Convert.ToDecimal(param[3].Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }));
                            newCandle.High = Convert.ToDecimal(param[2].Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }));
                            newCandle.Open = Convert.ToDecimal(param[1].Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }));
                            newCandle.Close = Convert.ToDecimal(param[4].Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }));
                            newCandle.Volume = Convert.ToDecimal(param[5].Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }));

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
                var param = new Dictionary<string, string>
                {
                    { "symbol=" + nameSec.ToUpper(), "&interval=" + needTf + "&startTime=" + startTime + "&endTime=" + endTime }
                };

                var res = CreateQuery(Method.GET, endPoint, param, false);

                var candles = _deserializeCandles(res);
                return candles;

            }
            else
            {
                if (needTf == "2m")
                {
                    var param = new Dictionary<string, string>
                    {
                        { "symbol=" + nameSec.ToUpper(), "&interval=1m" + "&startTime=" + startTime + "&endTime=" + endTime }
                    };
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);

                    var newCandles = BuildCandles(candles, 2, 1);
                    return newCandles;
                }
                else if (needTf == "10m")
                {
                    var param = new Dictionary<string, string>
                    {
                        { "symbol=" + nameSec.ToUpper(), "&interval=5m" + "&startTime=" + startTime + "&endTime=" + endTime }
                    };
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 10, 5);
                    return newCandles;
                }
                else if (needTf == "20m")
                {
                    var param = new Dictionary<string, string>
                    {
                        { "symbol=" + nameSec.ToUpper(), "&interval=5m" + "&startTime=" + startTime + "&endTime=" + endTime }
                    };
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 20, 5);
                    return newCandles;
                }
                else if (needTf == "45m")
                {
                    var param = new Dictionary<string, string>
                    {
                        { "symbol=" + nameSec.ToUpper(), "&interval=15m" + "&startTime=" + startTime + "&endTime=" + endTime }
                    };
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 45, 15);
                    return newCandles;
                }
            }

            return null;
        }

        /// <summary>
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
            }

            string endPoint = "api/v1/klines";

            if (needTf != "2m" && needTf != "10m" && needTf != "20m" && needTf != "45m")
            {
                var param = new Dictionary<string, string>
                {
                    { "symbol=" + nameSec.ToUpper(), "&interval=" + needTf }
                };

                var res = CreateQuery(Method.GET, endPoint, param, false);

                var candles = _deserializeCandles(res);
                return candles;

            }
            else
            {
                if (needTf == "2m")
                {
                    var param = new Dictionary<string, string>
                    {
                        { "symbol=" + nameSec.ToUpper(), "&interval=1m" }
                    };
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);

                    var newCandles = BuildCandles(candles, 2, 1);
                    return newCandles;
                }
                else if (needTf == "10m")
                {
                    var param = new Dictionary<string, string>
                    {
                        { "symbol=" + nameSec.ToUpper(), "&interval=5m" }
                    };
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 10, 5);
                    return newCandles;
                }
                else if (needTf == "20m")
                {
                    var param = new Dictionary<string, string>
                    {
                        { "symbol=" + nameSec.ToUpper(), "&interval=5m" }
                    };
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 20, 5);
                    return newCandles;
                }
                else if (needTf == "45m")
                {
                    var param = new Dictionary<string, string>
                    {
                        { "symbol=" + nameSec.ToUpper(), "&interval=15m" }
                    };
                    var res = CreateQuery(Method.GET, endPoint, param, false);
                    var candles = _deserializeCandles(res);
                    var newCandles = BuildCandles(candles, 45, 15);
                    return newCandles;
                }
            }

            return null;
        }

        /// <summary>
        /// преобразует свечи одного таймфрейма в больший
        /// </summary>
        /// <param name="oldCandles"></param>
        /// <param name="needTf"></param>
        /// <param name="oldTf"></param>
        /// <returns></returns>
        private List<Candle> BuildCandles(List<Candle> oldCandles, int needTf, int oldTf)
        {
            List < Candle > newCandles = new List<Candle>();

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
        /// <summary>
        /// блокиратор многопоточного доступа к http запросам
        /// </summary>
        private object _queryHttpLocker = new object();

        /// <summary>
        /// метод отправляет запрос и возвращает ответ от сервера
        /// </summary>
        public string CreateQuery(Method method, string endpoint, Dictionary<string, string> param = null,
            bool auth = false)
        {
            try
            {
                lock (_queryHttpLocker)
                {
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
                            message = fullUrl + "&timestamp="+ timeStamp;
                            fullUrl += "&timestamp="+ timeStamp + "&signature=" + CreateSignature(message.Trim('?'));
                        }
                    }

                    var request = new RestRequest(endpoint + fullUrl, method);
                    request.AddHeader("X-MBX-APIKEY", ApiKey);

                    var response = new RestClient("https://api.binance.com").Execute(request).Content;

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
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #region Аутентификация запроса

        private string GetNonce()
        {
            var resTime = CreateQuery(Method.GET, "api/v1/time", null, false);
            var result = JsonConvert.DeserializeAnonymousType(resTime, new BinanceTime());
            return (result.serverTime+500).ToString();

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


        //работа с ордерами


        /// <summary>
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
                        return;
                    }

                    Dictionary<string, string> param = new Dictionary<string, string>
                    {
                        { "symbol=", order.SecurityNameCode.ToUpper() },
                        { "&side=", order.Side == Side.Buy ? "BUY" : "SELL" },
                        { "&type=", "LIMIT" },
                        { "&timeInForce=", "GTC" },
                        { "&newClientOrderId=", order.NumberUser.ToString() },
                        {
                            "&quantity=",
                            order.Volume.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, ".")
                        },
                        {
                            "&price=",
                            order.Price.ToString(CultureInfo.InvariantCulture)
                            .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, ".")
                        }
                    };

                    var res = CreateQuery(Method.POST, "api/v3/order", param, true);

                    if (res != null && res.Contains("clientOrderId"))
                    {
                        SendLogMessage(res, LogMessageType.Trade);
                    }
                    else
                    {
                        order.State = OrderStateType.Fail;
                        MyOrderEvent?.Invoke(order);
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
        /// отменить оредр
        /// </summary>
        public void CanselOrder(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    Dictionary<string, string> param = new Dictionary<string, string>
                    {
                        { "symbol=", order.SecurityNameCode.ToUpper() },
                        { "&orderId=", order.NumberMarket }
                    };

                    var res = CreateQuery(Method.DELETE, "api/v3/order", param, true);
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);

                }
            }
        }

        /// <summary>
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
                var param = new Dictionary<string, string>
                {
                    { "symbol=", namesSec[i].ToUpper() },
                    //param.Add("&recvWindow=" , "100");
                    //param.Add("&limit=", GetNonce());
                    { "&limit=", "500" }
                };
                //"symbol={symbol.ToUpper()}&recvWindow={recvWindow}"

                var res = CreateQuery(Method.GET, endPoint, param, true);

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
                { // ордер активен. Ничего не делаем
                    continue;
                }

                else if (myOrder.status == "FILLED" ||
                    myOrder.status == "PARTIALLY_FILLED")
                { // ордер исполнен

                    MyTrade trade = new MyTrade();
                    trade.NumberOrderParent = oldOpenOrders[i].NumberMarket;
                    trade.NumberTrade = NumberGen.GetNumberOrder(StartProgram.IsOsTrader).ToString();
                    trade.SecurityNameCode = oldOpenOrders[i].SecurityNameCode;
                    trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(myOrder.updateTime));
                    trade.Side = oldOpenOrders[i].Side;

                    MyTradeEvent?.Invoke(trade);
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

                    MyOrderEvent?.Invoke(newOrder);
                }
            }
            return true;
        }


        // потоковые данные из WEBSOCKET

        /// <summary>
        /// клиент вебсокет
        /// </summary>
        private WebSocket _wsClient;

        /// <summary>
        /// берет пришедшие через ws сообщения и кладет их в общую очередь
        /// </summary>        
        private void GetRes(object sender, MessageReceivedEventArgs e)
        {
            _newMessage.Enqueue(e.Message);
        }

        /// <summary>
        /// соединение по ws открыто
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Connect(object sender, EventArgs e)
        {
            IsConnected = true;
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

                _wsStreams.Clear();

                Disconnected?.Invoke();
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
        /// коллекция потоков данных
        /// </summary>
        private Dictionary<string, WebSocket> _wsStreams = new Dictionary<string, WebSocket>();

        /// <summary>
        /// подписать данную бумагу на получение стаканов и трейдов
        /// </summary>
        public void SubscribleTradesAndDepths(Security security)
        {
            if (!_wsStreams.ContainsKey(security.Name))
            {
                string urlStr = "wss://stream.binance.com:9443/stream?streams=" + security.Name.ToLower() + "@depth20/" +security.Name.ToLower() + "@trade";

                _wsClient = new WebSocket(urlStr); //создаем вебсокет

                _wsClient.Opened += new EventHandler(Connect);

                _wsClient.Closed += new EventHandler(Disconnect);

                _wsClient.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(WsError);

                _wsClient.MessageReceived += new EventHandler<MessageReceivedEventArgs>(GetRes);

                _wsClient.Open();

                _wsStreams.Add(security.Name, _wsClient);
            }

        }

        /// <summary>
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
                        string mes;

                        if (_newUserDataMessage.TryDequeue(out mes))
                        {
                            if (mes.Contains("code"))
                            {
                                SendLogMessage(JsonConvert.DeserializeAnonymousType(mes, new ErrorMessage()).msg, LogMessageType.Error);
                            }

                            else if (mes.Contains("\"e\"" + ":" + "\"executionReport\""))
                            {
                                var order = JsonConvert.DeserializeAnonymousType(mes, new ExecutionReport());

                                string orderNumUser = order.C;

                                if (string.IsNullOrEmpty(orderNumUser) ||
                                    orderNumUser == "null")
                                {
                                    orderNumUser = order.c;
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
                                    newOrder.Volume = Convert.ToDecimal(order.q.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                                    newOrder.Price = Convert.ToDecimal(order.p.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                                    newOrder.ServerType = ServerType.Binance;
                                    newOrder.PortfolioNumber = newOrder.SecurityNameCode;

                                    MyOrderEvent?.Invoke(newOrder);
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
                                    newOrder.Volume = Convert.ToDecimal(order.q.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                                    newOrder.Price = Convert.ToDecimal(order.p.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                                    newOrder.ServerType = ServerType.Binance;
                                    newOrder.PortfolioNumber = newOrder.SecurityNameCode;

                                    MyOrderEvent?.Invoke(newOrder);
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
                                    newOrder.Volume = Convert.ToDecimal(order.q.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                                    newOrder.Price = Convert.ToDecimal(order.p.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                                    newOrder.ServerType = ServerType.Binance;
                                    newOrder.PortfolioNumber = newOrder.SecurityNameCode;

                                    MyOrderEvent?.Invoke(newOrder);
                                }
                                else if (order.x == "TRADE")
                                {

                                    MyTrade trade = new MyTrade();
                                    trade.Time = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(order.T));
                                    trade.NumberOrderParent = order.i.ToString();
                                    trade.NumberTrade = order.t.ToString();
                                    trade.Volume = Convert.ToDecimal(order.l.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                                    trade.Price = Convert.ToDecimal(order.L.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                                    trade.SecurityNameCode = order.s;
                                    trade.Side = order.S == "BUY" ? Side.Buy : Side.Sell;

                                    MyTradeEvent?.Invoke(trade);
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
                                    newOrder.Volume = Convert.ToDecimal(order.q.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                                    newOrder.Price = Convert.ToDecimal(order.p.Replace(".", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                                    newOrder.ServerType = ServerType.Binance;
                                    newOrder.PortfolioNumber = newOrder.SecurityNameCode;

                                    MyOrderEvent?.Invoke(newOrder);
                                }

                                continue;
                            }

                            else if (mes.Contains("\"e\"" + ":" + "\"outboundAccountInfo\""))
                            {
                                var portfolios = JsonConvert.DeserializeAnonymousType(mes, new OutboundAccountInfo());

                                UpdatePortfolio?.Invoke(portfolios);
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

                                NewTradesEvent?.Invoke(quotes);
                                continue;
                            }

                            else if (mes.Contains("\"lastUpdateId\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new DepthResponse());

                                UpdateMarketDepth?.Invoke(quotes);
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
        public event Action<AccountResponse> NewPortfolio;

        /// <summary>
        /// обновились портфели
        /// </summary>
        public event Action<OutboundAccountInfo> UpdatePortfolio;

        /// <summary>
        /// новые бумаги в системе
        /// </summary>
        public event Action<SecurityResponce> UpdatePairs;

        /// <summary>
        /// обновился стакан
        /// </summary>
        public event Action<DepthResponse> UpdateMarketDepth;

        /// <summary>
        /// обновились тики
        /// </summary>
        public event Action<TradeResponse> NewTradesEvent;

        /// <summary>
        /// соединение с API установлено
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// соединение с API разорвано
        /// </summary>
        public event Action Disconnected;

        #endregion

        #region сообщения для лога

        /// <summary>
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            LogMessageEvent?.Invoke(message, type);
        }

        /// <summary>
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}
