/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitMex.BitMexEntity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace OsEngine.Market.Servers.BitMex
{
    /// <summary>
    /// класс - клиент, обрабатывающий посылку запросов и прием информации с BitMEX API
    /// </summary>
    public class BitMexClient
    {

        private ClientWebSocket _ws;

        private string _serverAdress;

        /// <summary>
        /// адрес сервера для подключения через websocket
        /// </summary>
        public string ServerAdres
        {
            set { _serverAdress = value; }
            private get { return _serverAdress; }
        }

        private string _secKey;

        /// <summary>
        /// секретный ключ пользователя
        /// </summary>
        public string SecKey
        {
            set { _secKey = value; }
            private get { return _secKey;}
        }

        private string _id;

        /// <summary>
        /// публичный ключ пользователя
        /// </summary>
        public string Id
        {
            set { _id = value; }
            private get { return _id; }
        }

        public bool IsConnected;

        /// <summary>
        /// установить соединение с сервером
        /// </summary>
        public void Connect()
        {
            if (_ws != null)
            {
                Disconnect();
            }

            _ws = new ClientWebSocket();

            Uri uri = new Uri(_serverAdress);
            _ws.ConnectAsync(uri, CancellationToken.None).Wait();

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

            Thread wspinger = new Thread(_pinger);
            wspinger.CurrentCulture = new CultureInfo("ru-RU");
            wspinger.IsBackground = true;
            wspinger.Start();

            Auth();
        }

        /// <summary>
        /// отключение
        /// </summary>
        public void Disconnect()
        {
            _neadToStopAllThreads = true;

            Thread.Sleep(1000);
            if (_ws != null)
            {
                _ws.Abort();
                _ws.Dispose();
                Thread.Sleep(1000);
                _ws = null;
            }
            IsConnected = false;
        }

        /// <summary>
        /// флаг того что нужно все потоки остановить при дисконнекте
        /// </summary>
        private bool _neadToStopAllThreads;

        /// <summary>
        /// проверка соединения
        /// </summary>
        private void _pinger()
        {
            while (true)
            {
                for (int i = 0; i < 100; i++)
                {
                    Thread.Sleep(100);
                    if (_neadToStopAllThreads == true)
                    {
                        return;
                    }
                }

                if (_ws != null && _ws.State != WebSocketState.Open && IsConnected)
                {
                    IsConnected = false;
                    Thread woker = new Thread(ReconnectArea);
                    woker.IsBackground = true;
                    woker.Start();
                    return;
                }
            }
        }

        private void ReconnectArea()
        {
            if (Disconnected != null)
            {
                Disconnected();
            }
        }

        /// <summary>
        /// регистрируем ключ на бирже для доступа к закрытым данным
        /// </summary>
        /// <param name="id"></param>
        /// <param name="key"></param>
        private void Auth()
        {
            string nonce = GetNonce().ToString();
            byte[] signatureBytes = Hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes("GET/realtime" + nonce));
            string signatureString = ByteArrayToString(signatureBytes);
            string que = "{\"op\": \"authKey\", \"args\": [\"" + _id + "\"," + nonce + ",\"" + signatureString + "\"]}";
            var reqAsBytes = Encoding.UTF8.GetBytes(que);
            var ticksRequest = new ArraySegment<byte>(reqAsBytes);

                _ws.SendAsync(ticksRequest,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None).Wait();

        }

        /// <summary>
        /// блокиратор доступа к подписчику
        /// </summary>
        private object _queryLocker = new object();

        /// <summary>
        /// подписаться на данные через websocket
        /// </summary>
        public void SendQuery(string que)
        {
            lock (_queryLocker)
            {
                var reqAsBytes = Encoding.UTF8.GetBytes(que);
                var ticksRequest = new ArraySegment<byte>(reqAsBytes);

                _ws.SendAsync(ticksRequest,WebSocketMessageType.Text,
                             true, CancellationToken.None).Wait();
            }
        }

        /// <summary>
        /// очередь новых сообщений, пришедших с сервера биржи
        /// </summary>
        private ConcurrentQueue<string> _newMessage = new ConcurrentQueue<string>();

        /// <summary>
        /// метод, который в отдельном потоке принимает все новые сообщения от биржи и кладет их в общую очередь
        /// </summary>
        /// <param name="clientWebSocket">вебсокет клиент</param>
        private  void GetRes(object clientWebSocket)
        {
            ClientWebSocket ws = (ClientWebSocket) clientWebSocket;

            string res = "";

            while (true)
            {
                try
                {
                    Thread.Sleep(1);

                    if (_neadToStopAllThreads == true)
                    {
                        return;
                    }

                    if (ws.State != WebSocketState.Open)
                    {
                        continue;
                    }

                    if (IsConnected)
                    {
                        var buffer = new ArraySegment<byte>(new byte[1024]);
                        var result = ws.ReceiveAsync(buffer, CancellationToken.None).Result;

                        if(result.Count == 0)
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
                    if (BitMexLogMessageEvent != null)
                    {
                        BitMexLogMessageEvent(exception.ToString(), LogMessageType.Error);
                    }
                }

            }
        }

        /// <summary>
        /// метод конвертирует JSON в классы C# и отправляет на верх
        /// </summary>
        private void Converter()
        {
            string myTradesStr = "{\"table\"" + ":" + "\"execution\"";
            string ordersStr = "{\"table\"" + ":" + "\"order\"";
            string marginStr = "{\"table\"" + ":" + "\"margin\"";
            string positionsStr = "{\"table\"" + ":" + "\"position\"";
            string marketDepthStr = "{\"table\"" + ":" + "\"orderBookL2\"";
            string tradesStr = "{\"table\"" + ":" + "\"trade\"";

            while (true)
            {
                try
                {
                    if (_neadToStopAllThreads == true)
                    {
                        return;
                    }
                    if (!_newMessage.IsEmpty)
                    {
                        string mes;

                        if (_newMessage.TryDequeue(out mes))
                        {


                            if (mes.StartsWith(myTradesStr))
                            {
                                var myOrder = JsonConvert.DeserializeAnonymousType(mes, new BitMexMyOrders());

                                if (MyTradeEvent != null && myOrder.data.Count != 0 && myOrder.data[0].execType == "Trade")
                                {
                                    MyTradeEvent(myOrder);
                                }
                                continue;
                            }

                            if (mes.StartsWith(ordersStr))
                            {
                                var order = JsonConvert.DeserializeAnonymousType(mes, new BitMexOrder());

                                if (MyOrderEvent != null && order.data.Count != 0)
                                {
                                    MyOrderEvent(order);
                                }
                                continue;
                            }

                            if (mes.StartsWith(marginStr))
                            {
                                var portf = JsonConvert.DeserializeAnonymousType(mes, new BitMexPortfolio());

                                if (UpdatePortfolio != null)
                                {
                                    UpdatePortfolio(portf);
                                }
                                continue;
                            }

                            if (mes.StartsWith(positionsStr))
                            {
                                var pos = JsonConvert.DeserializeAnonymousType(mes, new BitMexPosition());

                                if (UpdatePosition != null)
                                {
                                    UpdatePosition(pos);
                                }
                                continue;
                            }

                            if (mes.StartsWith(marketDepthStr))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new BitMexQuotes());

                                if (UpdateMarketDepth != null && quotes.data.Count != 0 && quotes.data != null)
                                {
                                    UpdateMarketDepth(quotes);
                                }
                                continue;
                            }

                            if (mes.StartsWith(tradesStr))
                            {
                                var trade = JsonConvert.DeserializeAnonymousType(mes, new BitMexTrades());

                                if (NewTradesEvent != null)
                                {
                                    NewTradesEvent(trade);
                                }
                                continue;
                            }

                            if (mes.Contains("error"))
                            {
                                if (ErrorEvent != null)
                                {
                                    ErrorEvent(mes);
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
                    if (BitMexLogMessageEvent != null)
                    {
                        BitMexLogMessageEvent(exception.ToString(), LogMessageType.Error);
                    }
                }
            }
        }
        /// <summary>
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> BitMexLogMessageEvent;

        /// <summary>
        /// новые мои ордера
        /// </summary>
        public event Action<BitMexOrder> MyOrderEvent;

        /// <summary>
        /// новые мои сделки
        /// </summary>
        public event Action<BitMexMyOrders> MyTradeEvent;

        /// <summary>
        /// событие обновления портфеля
        /// </summary>
        public event Action<BitMexPortfolio> UpdatePortfolio;

        /// <summary>
        /// событие обновления позиций
        /// </summary>
        public event Action<BitMexPosition> UpdatePosition;

        /// <summary>
        /// обновился стакан
        /// </summary>
        public event Action<BitMexQuotes> UpdateMarketDepth;

        /// <summary>
        /// обновились тики
        /// </summary>
        public event Action<BitMexTrades> NewTradesEvent;

        /// <summary>
        /// ошибка http запроса или websocket
        /// </summary>
        public event Action<string> ErrorEvent;

        /// <summary>
        /// соединение с BitMEX API установлено
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// соединение с BitMEX API разорвано
        /// </summary>
        public event Action Disconnected;

        /// <summary>
        /// блокиратор многопоточного доступа к http запросам
        /// </summary>
        private object _queryHttpLocker = new object();

        private string _domain;

        /// <summary>
        /// адрес для отправки запросов
        /// </summary>
        public string Domain
        {
            set { _domain = value; }
            private get { return _domain; }
        }

        /// <summary>
        /// метод отправляет запрос и возвращает ответ от сервера
        /// </summary>
        /// <param name="method">метод запроса</param>
        /// <param name="function"></param>
        /// <param name="param">коллекция параметров</param>
        /// <param name="auth">нужна ли аутентификация для этого запроса</param>
        /// <returns></returns>
        public string CreateQuery(string method, string function, Dictionary<string, string> param = null, bool auth = false)
        {
            lock (_queryHttpLocker)
            {
                string paramData = BuildQueryData(param);
                string url = "/api/v1" + function + ((method == "GET" && paramData != "") ? "?" + paramData : "");
                string postData = (method != "GET") ? paramData : "";

                HttpWebRequest webRequest = (HttpWebRequest) WebRequest.Create(_domain + url);
                webRequest.Method = method;

                if (auth)
                {
                    string nonce = GetNonce().ToString();
                    string message = method + url + nonce + postData;
                    byte[] signatureBytes = Hmacsha256(Encoding.UTF8.GetBytes(_secKey), Encoding.UTF8.GetBytes(message));
                    string signatureString = ByteArrayToString(signatureBytes);

                    webRequest.Headers.Add("api-nonce", nonce);
                    webRequest.Headers.Add("api-key", _id);
                    webRequest.Headers.Add("api-signature", signatureString);
                }

                try
                {
                    if (postData != "")
                    {
                        webRequest.ContentType = "application/x-www-form-urlencoded";
                        var data = Encoding.UTF8.GetBytes(postData);
                        using (var stream = webRequest.GetRequestStream())
                        {
                            stream.Write(data, 0, data.Length);
                        }
                    }

                    using (WebResponse webResponse = webRequest.GetResponse())
                    using (Stream str = webResponse.GetResponseStream())
                    using (StreamReader sr = new StreamReader(str))
                    {
                        return sr.ReadToEnd();
                    }
                }
                catch (WebException wex)
                {
                    using (HttpWebResponse response = (HttpWebResponse) wex.Response)
                    {
                        if (response == null)
                            throw;

                        using (Stream str = response.GetResponseStream())
                        {
                            using (StreamReader sr = new StreamReader(str))
                            {
                                string error = sr.ReadToEnd();

                                if (ErrorEvent != null)
                                {
                                    ErrorEvent(error);
                                }
                                return sr.ReadToEnd();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// преобразовать коллекцию параметров в строку запроса
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        private string BuildQueryData(Dictionary<string, string> param)
        {
            if (param == null)
                return "";

            StringBuilder b = new StringBuilder();
            foreach (var item in param)
                b.Append(string.Format("&{0}={1}", item.Key, item.Value));

            try { return b.ToString().Substring(1); }
            catch (Exception) { return ""; }
        }

        /// <summary>
        /// взять уникальный номер
        /// </summary>
        /// <returns></returns>
        private long GetNonce()
        {
            DateTime yearBegin = new DateTime(1990, 1, 1);
            long nonce = DateTime.UtcNow.Ticks - yearBegin.Ticks;
            long shortNonce =  nonce - 8000000000000000;
            return shortNonce;
        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        private byte[] Hmacsha256(byte[] keyByte, byte[] messageBytes)
        {
            using (var hash = new HMACSHA256(keyByte))
            {
                return hash.ComputeHash(messageBytes);
            }
        }
    }
}
