using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.OKX.Entity;
using OsEngine.Market.Servers.Entity;
using WebSocket4Net;
using System.Net.Http;
using SuperSocket.ClientEngine;


namespace OsEngine.Market.Servers.OKX
{
    public class OkxClient
    {
        private string PublicKey;
        private string SeckretKey;
        private string Password;
        private bool HedgeModeIsOn;

        private string _baseUrl = "https://www.okx.com/";
        private string _publicWebSocket = "wss://ws.okx.com:8443/ws/v5/public";
        private string _privateWebSocket = "wss://ws.okx.com:8443/ws/v5/private";

        private bool _isDisposed = false;


        #region LockersWebSockets

        private object lockerPositionsWs = new object();
        private object lockerOrdersWs = new object();
        private object lockerTradesWs = new object();
        private object lockerDepthsWs = new object();

        #endregion


        //Задержка на подписку для вебсокетов
        public RateGate _rateGateWebSocket = new RateGate(1, TimeSpan.FromMilliseconds(1500));

        public OkxClient(string PublicKey, string SeckretKey, string Password, bool HedgeModeIsOn)
        {
            this.PublicKey = PublicKey;
            this.SeckretKey = SeckretKey;
            this.Password = Password;
            this.HedgeModeIsOn = HedgeModeIsOn;


            Thread ThreadCleaningDoneOrders = new Thread(CleanDoneOrders);
            ThreadCleaningDoneOrders.CurrentCulture = new CultureInfo("ru-RU");
            ThreadCleaningDoneOrders.IsBackground = true;
            ThreadCleaningDoneOrders.Start();
        }

        public void Connect()
        {
            if (string.IsNullOrEmpty(PublicKey) ||
                string.IsNullOrEmpty(SeckretKey))
            {
                return;
            }

            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                HttpClient httpClient = new HttpClient();
                var response = httpClient.GetAsync(_baseUrl + "api/v5/public/time").Result;

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception(response.StatusCode.ToString());
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Сервер не доступен или отсутствует интернет. " +
                    exception.Message +
                    " Возможно вы забыли включить VPN ;)", LogMessageType.Error);
                return;
            }

            IsConnectedTrades = true;
            IsConnectedDepths = true;

            if (Connected != null)
            {
                Connected();
            }

            SetPositionMode();

            CreateDataChanels();
        }
        public void Dispose()
        {
            Thread.Sleep(1000);
            try
            {
                foreach (var ws in _wsChanelPositions)
                {
                    ws.Value.Closed -= new EventHandler(DisconnectPsoitonsChanel);
                    ws.Value.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(PushMessagePositions);

                    ws.Value.Close();
                    ws.Value.Dispose();
                }

                IsConnectedPositions = false;
            }
            catch
            {
                // ignore
            }

            try
            {
                foreach (var ws in _wsChanelOrders)
                {
                    ws.Value.Closed -= new EventHandler(DisconnectOrdersChanel);
                    ws.Value.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(PushMessageOrders);

                    ws.Value.Close();
                    ws.Value.Dispose();
                }

                IsConnectedOrders = false;
            }
            catch
            {
                // ignore
            }

            try
            {
                foreach (var ws in _wsChanelDepths)
                {
                    ws.Value.Closed -= new EventHandler(DisconnectDepthsChanel);
                    ws.Value.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(PushMessageDepths);

                    ws.Value.Close();
                    ws.Value.Dispose();
                }

                IsConnectedDepths = false;
            }
            catch
            {
                // ignore
            }


            try
            {
                foreach (var ws in _wsChanelTrades)
                {
                    ws.Value.Closed -= new EventHandler(DisconnectTradesChanel);
                    ws.Value.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(PushMessageTrade);

                    ws.Value.Close();
                    ws.Value.Dispose();
                }

                IsConnectedTrades = false;
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
        private void CreateDataChanels()
        {

            Thread keepAliveSokcets = new Thread(KeepAliveSokcets);
            keepAliveSokcets.CurrentCulture = new CultureInfo("ru-RU");
            keepAliveSokcets.IsBackground = true;
            keepAliveSokcets.Start();

            Thread converterOrers = new Thread(ConverterOrders);
            converterOrers.CurrentCulture = new CultureInfo("ru-RU");
            converterOrers.IsBackground = true;
            converterOrers.Start();

            Thread converterPosiotions = new Thread(ConverterErrorPositions);
            converterPosiotions.CurrentCulture = new CultureInfo("ru-RU");
            converterPosiotions.IsBackground = true;
            converterPosiotions.Start();

            Thread converterTrades = new Thread(ConverterTrades);
            converterTrades.CurrentCulture = new CultureInfo("ru-RU");
            converterTrades.IsBackground = true;
            converterTrades.Start();

            Thread converterDepths = new Thread(ConverterDepths);
            converterDepths.CurrentCulture = new CultureInfo("ru-RU");
            converterDepths.IsBackground = true;
            converterDepths.Start();

            Thread portfolioData = new Thread(UpdatePortfolios);
            portfolioData.CurrentCulture = new CultureInfo("ru-RU");
            portfolioData.IsBackground = true;
            portfolioData.Start();
        }
        private void KeepAliveSokcets()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(10000);

                    lock (lockerPositionsWs)
                    {
                        foreach (var wsPos in _wsChanelPositions)
                        {
                            if (wsPos.Value.State == WebSocketState.Open)
                            {
                                wsPos.Value.Send("ping");
                            }
                        }
                    }
                    lock (lockerOrdersWs)
                    {
                        foreach (var wsOrd in _wsChanelOrders)
                        {
                            if (wsOrd.Value.State == WebSocketState.Open)
                            {
                                wsOrd.Value.Send("ping");
                            }
                        }
                    }
                    lock (lockerTradesWs)
                    {
                        foreach (var wsTrade in _wsChanelTrades)
                        {
                            if (wsTrade.Value.State == WebSocketState.Open)
                            {
                                wsTrade.Value.Send("ping");
                            }
                        }
                    }
                    lock (lockerDepthsWs)
                    {
                        foreach (var wsDepth in _wsChanelDepths)
                        {
                            if (wsDepth.Value.State == WebSocketState.Open)
                            {
                                wsDepth.Value.Send("ping");
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
                }
            }
        }
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        #region Trade

        private object _lockOrder = new object();
        public void ExecuteOrder(Order order)
        {
            lock (_lockOrder)
            {
                try
                {
                    if (_wsClientPositions != null)
                    {
                        if (order.SecurityNameCode.EndsWith("SWAP"))
                        {
                            SendOrderSwap(order);
                        }
                        else
                        {
                            SendOrderSpot(order);
                        }
                        GetPortfolios();
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
                }
            }
        }
        private void SendOrderSpot(Order order)
        {

            OrderRequest<OrderRequestArgsSwap> orderRequest = new OrderRequest<OrderRequestArgsSwap>();
            orderRequest.id = order.NumberUser.ToString();
            orderRequest.args.Add(new OrderRequestArgsSwap()
            {
                side = order.Side.ToString().ToLower(),
                instId = order.SecurityNameCode,
                tdMode = "cash",
                ordType = order.TypeOrder.ToString().ToLower(),
                sz = order.Volume.ToString().Replace(",", "."),
                px = order.Price.ToString().Replace(",", "."),
                clOrdId = order.NumberUser.ToString()

            });

            string json = JsonConvert.SerializeObject(orderRequest);

            MyOrderRequest.Add(order);
            _wsClientPositions.Send(json);
        }
        private void SendOrderSwap(Order order)
        {


            var side = String.Empty;
            if (order.PositionConditionType == OrderPositionConditionType.Close)
            {
                side = order.Side == Side.Buy ? "short" : "long";
            }
            else
            {
                side = order.Side == Side.Buy ? "long" : "short";
            }

            OrderRequest<OrderRequestArgsSwap> orderRequest = new OrderRequest<OrderRequestArgsSwap>();
            orderRequest.id = order.NumberUser.ToString();
            orderRequest.args.Add(new OrderRequestArgsSwap()
            {
                side = order.Side.ToString().ToLower(),
                posSide = side,
                instId = order.SecurityNameCode,
                tdMode = "cross",
                ordType = order.TypeOrder.ToString().ToLower(),
                sz = Convert.ToInt32(order.Volume).ToString(),
                px = order.Price.ToString().Replace(",", "."),
                clOrdId = order.NumberUser.ToString(),
                reduceOnly = order.PositionConditionType == OrderPositionConditionType.Close ? true : false
            });

            string json = JsonConvert.SerializeObject(orderRequest);

            MyOrderRequest.Add(order);
            _wsClientPositions.Send(json);
        }
        public void CancelOrder(Order order)
        {
            List<InstIdOrdId> arg = new List<InstIdOrdId>();
            arg.Add(new InstIdOrdId()
            {
                instId = order.SecurityNameCode,
                ordId = order.NumberMarket
            });

            var q = new
            {
                id = order.NumberUser.ToString(),
                op = "cancel-order",
                args = arg,
            };

            string json = JsonConvert.SerializeObject(q);

            _wsClientOrders.Send(json);

        }
        private string GetListActivOrders()
        {
            var url = $"{_baseUrl}{"api/v5/trade/orders-pending"}";
            using (var client = new HttpClient(new HttpInterceptor(PublicKey, SeckretKey, Password, null)))
            {
                var res = client.GetAsync(url).Result;
                var contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage(contentStr, LogMessageType.Error);
                }

                return contentStr;
            }
        }

        #endregion

        #region MyTradeEvent

        private bool IsConnectedPositions;

        private ConcurrentQueue<string> _newMessagePositions = new ConcurrentQueue<string>();

        private Dictionary<string, WebSocket> _wsChanelPositions = new Dictionary<string, WebSocket>();

        private WebSocket _wsClientPositions;

        private void ConverterErrorPositions()
        {
            while (true)
            {
                try
                {
                    if (!_newMessagePositions.IsEmpty)
                    {
                        string mes;

                        if (_newMessagePositions.TryDequeue(out mes))
                        {
                            Order order = null;

                            var quotes = JsonConvert.DeserializeAnonymousType(mes, new ErrorMessage<ErrorObjectOrders>());

                            if (quotes.data == null || quotes.data.Count == 0)
                            {
                                continue;

                            }
                            if (quotes.data[0].sCode == null)
                            {
                                continue;
                            }
                            if (quotes.data[0].sCode.Equals("0"))
                            {
                                continue;
                            }
                            SendLogMessage(quotes.data[0].clOrdId + quotes.data[0].sMsg, LogMessageType.Error);

                            order = FindNeedOrder(quotes.data[0].clOrdId);

                            if (MyOrderEvent != null && order != null)
                            {
                                MyOrderEventFail(order);
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

                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        public void SubscriblePositions(Security security)
        {
            if (!_wsChanelPositions.ContainsKey(security.Name))
            {
                _wsClientPositions = new WebSocket(_privateWebSocket);

                _wsClientPositions.Opened += new EventHandler((sender, e) => {
                    //_rateGateWebSocketPositions.WaitToProceed();
                    ConnectPositionsChanel(sender, e, security.Name);
                });

                _wsClientPositions.Closed += new EventHandler(DisconnectPsoitonsChanel);

                _wsClientTrades.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>((sender, e) => { WsError(sender, e, security.Name, "Position Chanell"); });

                _wsClientPositions.MessageReceived += new EventHandler<MessageReceivedEventArgs>(PushMessagePositions);



                if (_wsChanelPositions.ContainsKey(security.Name))
                {
                    _wsChanelPositions[security.Name].Close();
                    _wsChanelPositions.Remove(security.Name);
                }

                _wsClientPositions.Open();
                lock (lockerPositionsWs)
                {
                    _wsChanelPositions.Add(security.Name, _wsClientPositions);
                }
            }
        }

        private void ConnectPositionsChanel(object sender, EventArgs e, string securityName)
        {
            //Авторизация 
            var client = (WebSocket)sender;
            client.Send(Encryptor.MakeAuthRequest(PublicKey, SeckretKey, Password));


            string TypeInst = "SPOT";
            if (securityName.EndsWith("SWAP"))
            {
                TypeInst = "SWAP";
            }

            //Подписываемся на нужный канал
            RequestSubscribe<SubscribeArgsAccount> requestTrade = new RequestSubscribe<SubscribeArgsAccount>();
            requestTrade.args = new List<SubscribeArgsAccount>();
            requestTrade.args.Add(new SubscribeArgsAccount()
            {
                channel = "positions",
                instType = TypeInst
            });

            var json = JsonConvert.SerializeObject(requestTrade);
            client.Send(json);

            IsConnectedPositions = true;
        }

        private void DisconnectPsoitonsChanel(object sender, EventArgs e)
        {
            if (IsConnectedPositions)
            {
                IsConnectedPositions = false;

                _wsChanelPositions.Clear();

                if (Disconnected != null)
                {
                    Disconnected();
                }
            }
        }

        private void PushMessagePositions(object sender, MessageReceivedEventArgs e)
        {
            if (e.Message.Equals("pong"))
            {
                return;
            }
            if (_isDisposed == true)
            {
                return;
            }
            _newMessagePositions.Enqueue(e.Message);
        }

        private List<Order> MyOrderRequest = new List<Order>();

        private object lockerCleaningDoneOrders = new object();

        private Order FindNeedOrder(string ClOrdId)
        {
            lock (lockerCleaningDoneOrders)
            {
                for (int i = 0; i < MyOrderRequest.Count; i++)
                {
                    if (MyOrderRequest[i].NumberUser == Convert.ToInt32(ClOrdId))
                    {
                        return MyOrderRequest[i];
                    }
                }
                return null;
            }
        }


        private void CleanDoneOrders()
        {
            while (true)
            {
                Thread.Sleep(30000);

                lock (lockerCleaningDoneOrders)
                {
                    for (int i = 0; i < MyOrderRequest.Count; i++)
                    {
                        if (MyOrderRequest[i].State == OrderStateType.Done)
                        {
                            MyOrderRequest.Remove(MyOrderRequest[i]);
                        }
                    }
                }
            }
        }


        #endregion

        #region MyOrderEvent

        private bool IsConnectedOrders;

        private ConcurrentQueue<string> _newMessageOrders = new ConcurrentQueue<string>();

        private Dictionary<string, WebSocket> _wsChanelOrders = new Dictionary<string, WebSocket>();

        private WebSocket _wsClientOrders;

        private void ConverterOrders()
        {
            while (true)
            {
                try
                {

                    if (!_newMessageOrders.IsEmpty)
                    {
                        string mes;

                        if (_newMessageOrders.TryDequeue(out mes))
                        {

                            if (mes.StartsWith("{\"event\": \"error\""))
                            {
                                SendLogMessage(mes, LogMessageType.Error);
                            }

                            else if (mes.StartsWith("{\"arg\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new ObjectChanel<OrderResponseData>());

                                if (MyOrderEvent != null)
                                {
                                    MyOrderEvent(quotes);
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

                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        public void SubscribleOrders(Security security)
        {
            if (!_wsChanelOrders.ContainsKey(security.Name))
            {
                _wsClientOrders = new WebSocket(_privateWebSocket);

                _wsClientOrders.Opened += new EventHandler((sender, e) => {
                    //_rateGateWebSocketOrders.WaitToProceed();
                    ConnectOrdersChanel(sender, e, security.Name);
                });

                _wsClientOrders.Closed += new EventHandler(DisconnectOrdersChanel);

                _wsClientTrades.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>((sender, e) => { WsError(sender, e, security.Name, "Orders Chanell"); });

                _wsClientOrders.MessageReceived += new EventHandler<MessageReceivedEventArgs>(PushMessageOrders);



                if (_wsChanelOrders.ContainsKey(security.Name))
                {
                    _wsChanelOrders[security.Name].Close();
                    _wsChanelOrders.Remove(security.Name);
                }

                _wsClientOrders.Open();

                lock (lockerOrdersWs)
                {
                    _wsChanelOrders.Add(security.Name, _wsClientOrders);
                }
            }
        }

        private void ConnectOrdersChanel(object sender, EventArgs e, string securityName)
        {
            //Авторизация 
            var client = (WebSocket)sender;
            client.Send(Encryptor.MakeAuthRequest(PublicKey, SeckretKey, Password));

            string TypeInst = "SPOT";
            if (securityName.EndsWith("SWAP"))
            {
                TypeInst = "SWAP";
            }

            //Подписываемся на нужный канал
            RequestSubscribe<SubscribeArgsAccount> requestTrade = new RequestSubscribe<SubscribeArgsAccount>();
            requestTrade.args = new List<SubscribeArgsAccount>();
            requestTrade.args.Add(new SubscribeArgsAccount()
            {
                channel = "orders",
                instType = TypeInst
            });

            var json = JsonConvert.SerializeObject(requestTrade);
            client.Send(json);

            IsConnectedOrders = true;
        }

        private void DisconnectOrdersChanel(object sender, EventArgs e)
        {
            if (IsConnectedOrders)
            {
                IsConnectedOrders = false;

                _wsChanelOrders.Clear();

                if (Disconnected != null)
                {
                    Disconnected();
                }
            }
        }

        private void PushMessageOrders(object sender, MessageReceivedEventArgs e)
        {
            if (e.Message.Equals("pong"))
            {
                return;
            }

            if (_isDisposed == true)
            {
                return;
            }
            _newMessageOrders.Enqueue(e.Message);
        }


        /// <summary>
        /// проверить статус ордеров на бирже
        /// </summary>
        /// <param name="orders"></param>
        public void GetOrdersState(List<Order> orders)
        {
            for (int i = 0; i < orders.Count; i++)
            {
                try
                {
                    string json = GetOrderState(orders[i]);

                    var oldOrder = JsonConvert.DeserializeAnonymousType(json, new ObjectChanel<OrderResponseData>());


                    //if (oldOrder.data.Count == 0)
                    //{
                    //    throw new Exception(json + orders[i].NumberMarket + "  " + orders[i].NumberUser);
                    //}

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(oldOrder);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        private string GetOrderState(Order order)
        {
            var url = $"{_baseUrl}{"api/v5/trade/order?"}{"ordId=" + order.NumberMarket}{"&instId=" + order.SecurityNameCode}";
            using (var client = new HttpClient(new HttpInterceptor(PublicKey, SeckretKey, Password, null)))
            {
                var res = client.GetAsync(url).Result;
                var contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage(contentStr, LogMessageType.Error);
                }

                return contentStr;
            }
        }

        #endregion

        #region Portfolio

        private void UpdatePortfolios()
        {
            Thread.Sleep(30000);
            while (true)
            {
                Thread.Sleep(30000);
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    var json = GetBalance();


                    if (json.StartsWith("{\"code\":\"0\"") == false)
                    {
                        throw new Exception(json);
                    }

                    PorfolioResponse portfolio = JsonConvert.DeserializeAnonymousType(json, new PorfolioResponse());


                    portfolio.data[0].details.AddRange(GeneratePositionToContracts());


                    if (UpdatePortfolio != null)
                    {
                        UpdatePortfolio(portfolio);
                    }

                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        private string GetBalance()
        {
            var url = $"{_baseUrl}{"api/v5/account/balance"}";
            using (var client = new HttpClient(new HttpInterceptor(PublicKey, SeckretKey, Password, null)))
            {
                var res = client.GetAsync(url).Result;
                var contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage(contentStr, LogMessageType.Error);
                }

                return contentStr;
            }
        }


        private List<PortdolioDetails> GeneratePositionToContracts()
        {
            List<PorfolioData> potfolios = new List<PorfolioData>();
            List<PortdolioDetails> details = new List<PortdolioDetails>();


            try
            {
                string blockBalance = GetBlockBalance();

                PositonsResponce positons = JsonConvert.DeserializeAnonymousType(blockBalance, new PositonsResponce());

                for (int i = 0; i < positons.data.Count; i++)
                {
                    PorfolioData porfolioData = new PorfolioData()
                    {
                        details = new List<PortdolioDetails> { new PortdolioDetails()
                        {
                            ccy = positons.data[i].instId + "_" + positons.data[i].posSide.ToUpper(),
                            availEq = positons.data[i].pos, //notionalUsd
                            frozenBal = "0"
                    }   }
                    };

                    potfolios.Add(porfolioData);
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
            }

            for (int i = 0; i < potfolios.Count; i++)
            {
                details.AddRange(potfolios[i].details);
            }
            return details;
        }

        private string GetBlockBalance()
        {

            var url = $"{_baseUrl}{"api/v5/account/positions"}";
            using (var client = new HttpClient(new HttpInterceptor(PublicKey, SeckretKey, Password, null)))
            {
                var res = client.GetAsync(url).Result;
                var contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage(contentStr, LogMessageType.Error);
                }

                return contentStr;
            }
        }

        public void GetPortfolios()
        {
            try
            {

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var json = GetBalance();


                if (json.StartsWith("{\"code\":\"0\"") == false)
                {
                    throw new Exception(json);
                }

                PorfolioResponse portfolio = JsonConvert.DeserializeAnonymousType(json, new PorfolioResponse());

                portfolio.data[0].details.AddRange(GeneratePositionToContracts());

                if (NewPortfolio != null && portfolio != null)
                {
                    NewPortfolio(portfolio);
                }


            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
            }
        }

        #endregion

        #region Securities 

        private object lockedSecuity = new object();

        private SecurityResponce GetFuturesSecurities(HttpClient httpClient)
        {
            var response = httpClient.GetAsync("https://www.okx.com" + "/api/v5/public/instruments?instType=SWAP").Result;

            string json = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage(json, LogMessageType.Error);
            }

            SecurityResponce securityResponce = JsonConvert.DeserializeAnonymousType(json, new SecurityResponce());

            return securityResponce;
        }

        private SecurityResponce GetSpotSecurities(HttpClient httpClient)
        {
            var response = httpClient.GetAsync("https://www.okx.com" + "/api/v5/public/instruments?instType=SPOT").Result;

            var json = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                SendLogMessage(json, LogMessageType.Error);
            }

            SecurityResponce securityResponce = JsonConvert.DeserializeAnonymousType(json, new SecurityResponce());

            return securityResponce;
        }

        public void GetSecurities()
        {
            lock (lockedSecuity)
            {
                try
                {
                    HttpClient httpClient = new HttpClient();

                    SecurityResponce securityResponceFutures = GetFuturesSecurities(httpClient);

                    SecurityResponce securityResponceSpot = GetSpotSecurities(httpClient);

                    securityResponceFutures.data.AddRange(securityResponceSpot.data);

                    if (UpdatePairs != null)
                    {
                        UpdatePairs(securityResponceFutures);
                    }
                }
                catch (Exception error)
                {
                    if (error.Message.Equals("Unexpected character encountered while parsing value: <. Path '', line 0, position 0."))
                    {
                        SendLogMessage("service is unavailable", LogMessageType.Error);
                        return;
                    }
                    SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        #endregion

        #region Candles

        private string GetStringBar(TimeSpan tf)
        {
            try
            {
                if (tf.Hours != 0)
                {
                    return $"{tf.Hours}H";
                }
                if (tf.Minutes != 0)
                {
                    return $"{tf.Minutes}m";
                }
                if (tf.Days != 0)
                {
                    return $"{tf.Days}D";
                }

            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
            }

            return String.Empty;
        }

        private int GetCountCandlesToLoad()
        {
            var server = (AServer)ServerMaster.GetServers().Find(server => server.ServerType == ServerType.OKX);

            for (int i = 0; i < server.ServerParameters.Count; i++)
            {
                if (server.ServerParameters[i].Name.Equals("Candles to load"))
                {
                    var Param = (ServerParameterInt)server.ServerParameters[i];
                    return Param.Value;
                }
            }

            return 100;
        }

        private CandlesResponce GetResponseCandles(string nameSec, TimeSpan tf)
        {

            int NumberCandlesToLoad = GetCountCandlesToLoad();

            string bar = GetStringBar(tf);

            CandlesResponce candlesResponce = new CandlesResponce();
            candlesResponce.data = new List<List<string>>();

            do
            {

                int limit = NumberCandlesToLoad;
                if (NumberCandlesToLoad > 100)
                {
                    limit = 100;
                }

                string after = String.Empty;

                if (candlesResponce.data.Count != 0)
                {
                    after = $"&after={candlesResponce.data[candlesResponce.data.Count - 1][0]}";
                }


                string url = _baseUrl + $"api/v5/market/history-candles?instId={nameSec}&bar={bar}&limit={limit}" + after;

                HttpClient httpClient = new HttpClient();
                var responce = httpClient.GetAsync(url).Result;
                var json = responce.Content.ReadAsStringAsync().Result;
                candlesResponce.data.AddRange(JsonConvert.DeserializeAnonymousType(json, new CandlesResponce()).data);

                if (responce.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    SendLogMessage(json, LogMessageType.Error);
                }

                NumberCandlesToLoad -= limit;


            } while (NumberCandlesToLoad > 0);

            return candlesResponce;
        }

        private void ConvertCandles(CandlesResponce candlesResponce, List<Candle> candles)
        {
            for (int j = 0; j < candlesResponce.data.Count; j++)
            {
                Candle candle = new Candle();
                try
                {
                    candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(candlesResponce.data[j][0]));

                    candle.Open = Convert.ToDecimal(candlesResponce.data[j][1].Replace(".", ","));
                    candle.High = Convert.ToDecimal(candlesResponce.data[j][2].Replace(".", ","));
                    candle.Low = Convert.ToDecimal(candlesResponce.data[j][3].Replace(".", ","));
                    candle.Close = Convert.ToDecimal(candlesResponce.data[j][4].Replace(".", ","));
                    candle.Volume = Convert.ToDecimal(candlesResponce.data[j][5].Replace(".", ","));
                    var VolCcy = candlesResponce.data[j][6];

                    candles.Add(candle);
                }
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            CandlesResponce securityResponce = GetResponseCandles(nameSec, tf);

            List<Candle> candles = new List<Candle>();

            ConvertCandles(securityResponce, candles);

            candles.Reverse();

            return candles;
        }

        #endregion

        #region NewMarketDepthsEvent

        private bool IsConnectedDepths;

        private ConcurrentQueue<string> _newMessageDepths = new ConcurrentQueue<string>();

        private Dictionary<string, WebSocket> _wsChanelDepths = new Dictionary<string, WebSocket>();

        private WebSocket _wsClientDepths;


        public void SubscribleDepths(Security security)
        {
            if (!_wsChanelDepths.ContainsKey(security.Name))
            {
                _wsClientDepths = new WebSocket(_publicWebSocket);

                _wsClientDepths.Opened += new EventHandler((sender, e) => {
                    //_rateGateWebSocketDepths.WaitToProceed();
                    ConnectDepthsChanel(sender, e, security.Name);
                });

                _wsClientDepths.Closed += new EventHandler(DisconnectDepthsChanel);

                _wsClientTrades.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>((sender, e) => { WsError(sender, e, security.Name, "Depths Chanell"); });

                _wsClientDepths.MessageReceived += new EventHandler<MessageReceivedEventArgs>(PushMessageDepths);



                if (_wsChanelDepths.ContainsKey(security.Name))
                {
                    _wsChanelDepths[security.Name].Close();
                    _wsChanelDepths.Remove(security.Name);
                }

                _wsClientDepths.Open();

                lock (lockerDepthsWs)
                {
                    _wsChanelDepths.Add(security.Name, _wsClientDepths);
                }
            }
        }

        private void PushMessageDepths(object sender, MessageReceivedEventArgs e)
        {
            if (e.Message.Equals("pong"))
            {
                return;
            }
            if (_isDisposed == true)
            {
                return;
            }
            _newMessageDepths.Enqueue(e.Message);
        }

        private void ConverterDepths()
        {
            while (true)
            {
                try
                {
                    if (!_newMessageDepths.IsEmpty)
                    {
                        string mes;

                        if (_newMessageDepths.TryDequeue(out mes))
                        {
                            if (mes.StartsWith("{\"event\": \"error\""))
                            {
                                SendLogMessage(mes, LogMessageType.Error);
                            }

                            else if (mes.StartsWith("{\"arg\""))
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
                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        private void ConnectDepthsChanel(object sender, EventArgs e, string securityName)
        {
            var client = (WebSocket)sender;
            RequestSubscribe<SubscribeArgs> requestTrade = new RequestSubscribe<SubscribeArgs>();
            requestTrade.args = new List<SubscribeArgs>();
            requestTrade.args.Add(new SubscribeArgs()
            {
                channel = "books5",
                instId = securityName
            });

            var json = JsonConvert.SerializeObject(requestTrade);

            client.Send(json);

            IsConnectedDepths = true;
        }

        private void DisconnectDepthsChanel(object sender, EventArgs e)
        {
            if (IsConnectedDepths)
            {
                IsConnectedDepths = false;

                _wsChanelDepths.Clear();

                if (Disconnected != null)
                {
                    Disconnected();
                }
            }
        }

        #endregion

        #region NewTicksEvent

        private bool IsConnectedTrades;

        private ConcurrentQueue<string> _newMessageTrade = new ConcurrentQueue<string>();

        private Dictionary<string, WebSocket> _wsChanelTrades = new Dictionary<string, WebSocket>();

        private WebSocket _wsClientTrades;

        public void SubscribleTrades(Security security)
        {
            if (!_wsChanelTrades.ContainsKey(security.Name))
            {
                _wsClientTrades = new WebSocket(_publicWebSocket); // create web-socket / создаем вебсоке

                _wsClientTrades.Opened += new EventHandler((sender, e) => {
                    //_rateGateWebSocketTicks.WaitToProceed();
                    ConnectTradesChanel(sender, e, security.Name);
                }); //Connect

                _wsClientTrades.Closed += new EventHandler(DisconnectTradesChanel);

                _wsClientTrades.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>((sender, e) => { WsError(sender, e, security.Name, "Trades Chanell"); });

                _wsClientTrades.MessageReceived += new EventHandler<MessageReceivedEventArgs>(PushMessageTrade);



                if (_wsChanelTrades.ContainsKey(security.Name))
                {
                    _wsChanelTrades[security.Name].Close();
                    _wsChanelTrades.Remove(security.Name);
                }

                _wsClientTrades.Open();

                lock (lockerTradesWs)
                {
                    _wsChanelTrades.Add(security.Name, _wsClientTrades);
                }
            }
        }

        private void ConverterTrades()
        {
            while (true)
            {
                try
                {

                    if (!_newMessageTrade.IsEmpty)
                    {
                        string mes;

                        if (_newMessageTrade.TryDequeue(out mes))
                        {

                            if (mes.StartsWith("{\"event\": \"error\""))
                            {
                                SendLogMessage(mes, LogMessageType.Error);
                            }

                            else if (mes.StartsWith("{\"arg\""))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new TradeResponse());

                                if (NewTradesEvent != null)
                                {
                                    NewTradesEvent(quotes);
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

                catch (Exception error)
                {
                    SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
                }
            }
        }

        private void PushMessageTrade(object sender, MessageReceivedEventArgs e)
        {
            if (e.Message.Equals("pong"))
            {
                return;
            }
            if (_isDisposed == true)
            {
                return;
            }
            _newMessageTrade.Enqueue(e.Message);
        }

        private void WsError(object sender, EventArgs e, string CoinPairs, string Chanell)
        {
            var q = (ErrorEventArgs)e;
            if (q.Exception != null)
            {
                SendLogMessage($"{Chanell} Ошибка из ws4net {CoinPairs} :" + q.Exception, LogMessageType.Error);
            }
        }

        private void ConnectTradesChanel(object sender, EventArgs e, string securityName)
        {
            var client = (WebSocket)sender;
            RequestSubscribe<SubscribeArgs> requestTrade = new RequestSubscribe<SubscribeArgs>();
            requestTrade.args = new List<SubscribeArgs>();
            requestTrade.args.Add(new SubscribeArgs()
            {
                channel = "trades",
                instId = securityName
            });

            var json = JsonConvert.SerializeObject(requestTrade);

            client.Send(json);

            IsConnectedTrades = true;
        }

        private void DisconnectTradesChanel(object sender, EventArgs e)
        {
            if (IsConnectedTrades)
            {
                IsConnectedTrades = false;

                _wsChanelTrades.Clear();

                if (Disconnected != null)
                {
                    Disconnected();
                }
            }
        }

        #endregion

        #region PositionMode
        private string GetConfigurationAccount()
        {
            var url = $"{_baseUrl}{"api/v5/account/config"}";
            using (var client = new HttpClient(new HttpInterceptor(PublicKey, SeckretKey, Password, null)))
            {
                var res = client.GetAsync(url).Result;
                var contentStr = res.Content.ReadAsStringAsync().Result;

                if (res.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage(contentStr, LogMessageType.Error);
                }

                return contentStr;
            }
        }

        public string SetLeverage(Security security)
        {
            Dictionary<string, string> requstObject = new Dictionary<string, string>();

            requstObject["instId"] = security.Name;
            requstObject["lever"] = "1";
            requstObject["mgnMode"] = "cross";

            var url = $"{_baseUrl}{"api/v5/account/set-leverage"}";
            var bodyStr = JsonConvert.SerializeObject(requstObject);
            using (var client = new HttpClient(new HttpInterceptor(PublicKey, SeckretKey, Password, bodyStr)))
            {
                var res = client.PostAsync(url, new StringContent(bodyStr, Encoding.UTF8, "application/json")).Result;

                var contentStr = res.Content.ReadAsStringAsync().Result;
                return contentStr;
            }
        }

        private void SetPositionMode()
        {
            var dict = new Dictionary<string, string>();

            if (HedgeModeIsOn == true)
            {
                dict["posMode"] = "long_short_mode";
            }
            if (HedgeModeIsOn == false)
            {
                dict["posMode"] = "net_mode";
            }
            try
            {
                string res = PushPositionMode(dict);
            }
            catch (Exception error)
            {
                SendLogMessage($"{error.Message} { error.StackTrace}", LogMessageType.Error);
            }
        }

        private string PushPositionMode(Dictionary<string, string> requestParams)
        {
            var url = $"{_baseUrl}{"api/v5/account/set-position-mode"}";
            var bodyStr = JsonConvert.SerializeObject(requestParams);
            using (var client = new HttpClient(new HttpInterceptor(PublicKey, SeckretKey, Password, bodyStr)))
            {
                var res = client.PostAsync(url, new StringContent(bodyStr, Encoding.UTF8, "application/json")).Result;

                var contentStr = res.Content.ReadAsStringAsync().Result;
                return contentStr;
            }
        }

        #endregion

        #region OSData 

        public List<Candle> GetCandleDataHistory(string nameSec, TimeSpan tf, int NumberCandlesToLoad, long DataEnd)
        {
            CandlesResponce securityResponce = GetResponseDataCandles(nameSec, tf, NumberCandlesToLoad, DataEnd);

            List<Candle> candles = new List<Candle>();

            ConvertCandles(securityResponce, candles);

            candles.Reverse();

            return candles;
        }

        private CandlesResponce GetResponseDataCandles(string nameSec, TimeSpan tf, int NumberCandlesToLoad, long DataEnd)
        {
            string bar = GetStringBar(tf);

            CandlesResponce candlesResponce = new CandlesResponce();
            candlesResponce.data = new List<List<string>>();

            do
            {
                int limit = NumberCandlesToLoad;
                if (NumberCandlesToLoad > 100)
                {
                    limit = 100;
                }

                string after = $"&after={Convert.ToString(DataEnd)}";

                if (candlesResponce.data.Count != 0)
                {
                    after = $"&after={candlesResponce.data[candlesResponce.data.Count - 1][0]}";
                }


                string url = _baseUrl + $"api/v5/market/history-candles?instId={nameSec}&bar={bar}&limit={limit}" + after;

                HttpClient httpClient = new HttpClient();
                var responce = httpClient.GetAsync(url).Result;
                var json = responce.Content.ReadAsStringAsync().Result;
                candlesResponce.data.AddRange(JsonConvert.DeserializeAnonymousType(json, new CandlesResponce()).data);

                if (responce.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage(json, LogMessageType.Error);
                }

                NumberCandlesToLoad -= limit;


            } while (NumberCandlesToLoad > 0);

            return candlesResponce;
        }

        #endregion

        #region Outgoing events 

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<PorfolioResponse> NewPortfolio;

        /// <summary>
        /// my new orders
        /// новые мои ордера
        /// </summary>
        public event Action<ObjectChanel<OrderResponseData>> MyOrderEvent;

        /// <summary>
        /// my new orders
        /// новые мои фейлы
        /// </summary>
        public event Action<Order> MyOrderEventFail;

        /// <summary>
        /// portfolios updated
        /// обновились портфели
        /// </summary>
        public event Action<PorfolioResponse> UpdatePortfolio;

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
    }
}
