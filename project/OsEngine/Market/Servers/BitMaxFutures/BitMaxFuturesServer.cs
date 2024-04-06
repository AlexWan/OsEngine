using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using SuperSocket.ClientEngine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocket4Net;

namespace OsEngine.Market.Servers.BitMaxFutures
{
    public class BitMaxFuturesServer : AServer
    {
        public BitMaxFuturesServer()
        {
            BitMaxFuturesServerRealization realization = new BitMaxFuturesServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }

        /// <summary>
        /// instrument history query
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {

            return ((BitMaxFuturesServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);

        }
    }

    public class BitMaxFuturesServerRealization : IServerRealization
    {

        public BitMaxFuturesServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;


            Thread thread = new Thread(ConvertDataRealTime);
            thread.IsBackground = true;
            thread.Name = "ConvertDataRealTime";
            thread.Start();

        }

        public ServerType ServerType => ServerType.Bitmax_AscendexFutures;

        public ServerConnectStatus ServerStatus { get; set; }
        public List<IServerParameter> ServerParameters { get; set; }
        public DateTime ServerTime { get; set; }

        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;
        public event Action<List<Portfolio>> PortfolioEvent;
        public event Action<List<Security>> SecurityEvent;
        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action ConnectEvent;
        public event Action DisconnectEvent;
        public event Action<string, LogMessageType> LogMessageEvent;

        private const string BaseUrl = "https://ascendex.com/";
        private const string WebsocketPublicChanel = "wss://ascendex.com:443/api/pro/v2/stream";
        private const string WebsocketPrivateChanel = "wss://ascendex.com:443/<grp>/api/pro/v2/stream";
        private string PublicKey = String.Empty;
        private string SeckretKey = String.Empty;
        private string AccGroup = String.Empty;

        public void Connect()
        {
            PublicKey = ((ServerParameterString)ServerParameters[0]).Value;
            SeckretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            try
            {
                AccoutInfo serialiseObject = GetInfoAccount();

                if (serialiseObject.code.Equals("0") == true)
                {
                    AccGroup = serialiseObject.data.accountGroup;

                    ConnectEvent();
                    ServerStatus = ServerConnectStatus.Connect;
                    IsConnectedWebSocket = true;
                    IsConnectedWebSocketPrivate = true;
                    SubscriblePrivateChanel();
                }
                else
                {
                    SendNewLogMessage(serialiseObject.message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.Message, LogMessageType.Error);
            }

        }

        public void Dispose()
        {
            StopWsChanels();
            DisconnectEvent();
            ServerStatus = ServerConnectStatus.Disconnect;
        }

        private void StopWsChanels()
        {
            Thread.Sleep(1000);
            try
            {
                foreach (var ws in _wsChanels)
                {
                    ws.Value.Closed -= new EventHandler(DisconnectChanel);
                    ws.Value.MessageReceived -= new EventHandler<MessageReceivedEventArgs>(PushMessage);
                    ws.Value.Close();
                    ws.Value.Dispose();
                }

                IsConnectedWebSocket = false;
            }
            catch
            {
                // ignore
            }
        }

        private void ConvertDataRealTime()
        {
            while (true)
            {
                try
                {
                    if (!_newMessageWebSocket.IsEmpty)
                    {
                        string mes;

                        if (_newMessageWebSocket.TryDequeue(out mes))
                        {
                            if (mes.Contains("bbo"))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new BBOMessage());

                                MarketDepthLevel depthLevel = new MarketDepthLevel();
                                depthLevel.Ask =
                                quotes.data.ask[1].ToDecimal();
                                depthLevel.Price = quotes.data.ask[0].ToDecimal();

                                MarketDepthLevel depthLevelBid = new MarketDepthLevel();
                                depthLevelBid.Bid =
                                quotes.data.bid[1].ToDecimal();
                                depthLevelBid.Price =
                                quotes.data.bid[0].ToDecimal();

                                MarketDepth depth = new MarketDepth();
                                depth.SecurityNameCode = quotes.symbol;
                                depth.Asks.Add(depthLevel);
                                depth.Bids.Add(depthLevelBid);

                                if (MarketDepthEvent != null)
                                {
                                    MarketDepthEvent(depth);
                                }
                            }
                            if (mes.Contains("trades"))
                            {
                                var quotes = JsonConvert.DeserializeAnonymousType(mes, new ResponseTrades());


                                if (quotes.data == null || quotes.data.Count == 0)
                                {
                                    continue;

                                }

                                Trade trade = new Trade();
                                trade.SecurityNameCode = quotes.symbol;
                                trade.Price = Convert.ToDecimal(quotes.data[0].p.Replace('.', ','));
                                trade.Id = quotes.data[0].seqnum;
                                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(quotes.data[0].ts));
                                trade.Volume = Convert.ToDecimal(quotes.data[0].q.Replace('.', ','));
                                if (quotes.data[0].bm)
                                {
                                    trade.Side = Side.Buy;
                                }
                                if (!quotes.data[0].bm)
                                {
                                    trade.Side = Side.Sell;
                                }

                                NewTradesEvent(trade);
                            }


                        }
                    }

                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.Message, LogMessageType.Error);
                }
            }
        }

        private AccoutInfo GetInfoAccount()
        {
            try
            {
                string pkey = PublicKey;
                string secret = SeckretKey;

                string timestamp = TimeManager.GetUnixTimeStampMilliseconds().ToString();
                var sign = CreateSignature(timestamp + "v2/account/info", secret);
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                httpClient.DefaultRequestHeaders.Add("x-auth-key", pkey);
                httpClient.DefaultRequestHeaders.Add("x-auth-signature", sign);
                httpClient.DefaultRequestHeaders.Add("x-auth-timestamp", timestamp);
                HttpResponseMessage responseMessage = httpClient.GetAsync(BaseUrl + "/api/pro/v2/account/info").Result;
                string json = responseMessage.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeAnonymousType(json, new AccoutInfo());
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.Message, LogMessageType.Error);
            }

            return null;
        }

        private void SendNewLogMessage(string message, LogMessageType logType)
        {
            LogMessageEvent(message, logType);
        }

        private string CreateSignature(string message, string _secretKey)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var keyBytes = Encoding.UTF8.GetBytes(_secretKey);
            byte[] computedHash;
            using (var hash = new HMACSHA256(keyBytes))
            {
                computedHash = hash.ComputeHash(messageBytes);
            }
            return Convert.ToBase64String(computedHash);
        }

        #region Trade

        public void CancelOrder(Order order)
        {

        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {

        }

        public void SendOrder(Order order)
        {

            string pkey = PublicKey;
            string secret = SeckretKey;
            string volume = order.Volume.ToString().Replace(",", ".");
            string price = order.Price.ToString().Replace(",", ".");
            string secName = order.SecurityNameCode.Replace("-PERP", "");

            string timestamp = TimeManager.GetUnixTimeStampMilliseconds().ToString();
            var sign = CreateSignature(timestamp + "v2/futures/order", secret);
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("x-auth-key", pkey);
            httpClient.DefaultRequestHeaders.Add("x-auth-signature", sign);
            httpClient.DefaultRequestHeaders.Add("x-auth-timestamp", timestamp);



            StringContent content = new StringContent($"{{ \"time\" : {timestamp}, \"symbol\" : \"{order.SecurityNameCode}\", \"orderPrice\" : \"{price}\","
                + $"\"orderQty\" : \"{volume}\", \"orderType\" : \"Limit\", \"side\" :  \"{order.Side}\" }}",
                Encoding.UTF8, "application/json");

            HttpResponseMessage responseMessage = httpClient.PostAsync(BaseUrl + AccGroup + "/api/pro/v2/futures/order", content).Result;
            string json = responseMessage.Content.ReadAsStringAsync().Result;


            // торговля по фьчерсам временна приостановленна...
        }

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void GetOrdersState(List<Order> orders)
        {
            //null
        }

        public void ResearchTradesToOrders(List<Order> orders)
        {
            //null
        }

        public void GetAllActivOrders()
        {

        }

        public void GetOrderStatus(Order order)
        {

        }

        #endregion

        #region Portfolio

        private Portfolio myPortfolio = new Portfolio();

        public void GetPortfolios()
        {
            try
            {
                PositionResponceFutures serialiseObject = GetPositionAccendex();

                myPortfolio.ClearPositionOnBoard();

                List<Portfolio> _portfolios = new List<Portfolio>();

                UpdatePortfolios(serialiseObject);

                _portfolios.Add(myPortfolio);

                PortfolioEvent.Invoke(_portfolios);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.Message, LogMessageType.Error);
            }
        }

        private void UpdatePortfolios(PositionResponceFutures serialiseObject)
        {

            try
            {
                myPortfolio.Number = serialiseObject.data.ac;

                foreach (var collateral in serialiseObject.data.collaterals)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();

                    newPortf.SecurityNameCode = collateral.asset;

                    newPortf.ValueBegin = collateral.balance.ToDecimal();
                    newPortf.ValueCurrent = collateral.balance.ToDecimal();
                    newPortf.ValueBlocked = 0;

                    myPortfolio.SetNewPosition(newPortf);
                }

                foreach (var contract in serialiseObject.data.contracts)
                {


                    PositionOnBoard newPortf = new PositionOnBoard();

                    newPortf.SecurityNameCode = contract.symbol + "_" + contract.side;

                    newPortf.ValueBegin = contract.position.ToDecimal();
                    newPortf.ValueCurrent = contract.position.ToDecimal();
                    newPortf.ValueBlocked = 0;

                    myPortfolio.SetNewPosition(newPortf);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.Message, LogMessageType.Error);
            }

        }

        private PositionResponceFutures GetPositionAccendex()
        {
            try
            {
                string pkey = PublicKey;
                string secret = SeckretKey;

                string timestamp = TimeManager.GetUnixTimeStampMilliseconds().ToString();
                var sign = CreateSignature(timestamp + "v2/futures/position", secret);

                HttpClient http = new HttpClient();
                http.DefaultRequestHeaders.Add("Accept", "application/json");
                http.DefaultRequestHeaders.Add("x-auth-key", pkey);
                http.DefaultRequestHeaders.Add("x-auth-signature", sign);
                http.DefaultRequestHeaders.Add("x-auth-timestamp", timestamp);

                var response = http.GetAsync(BaseUrl + AccGroup + "/api/pro/v2/futures/position").Result; // изменить на acc Group из инфо
                var json = response.Content.ReadAsStringAsync().Result;

                return JsonConvert.DeserializeAnonymousType(json, new PositionResponceFutures());
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.Message, LogMessageType.Error);
            }

            return null;
        }

        #endregion

        #region Securities

        public void GetSecurities()
        {
            try
            {
                HttpClient http = new HttpClient();
                var responce = http.GetAsync(BaseUrl + "api/pro/v2/futures/contract").Result;
                var json = responce.Content.ReadAsStringAsync().Result;

                var serialiseObject = JsonConvert.DeserializeAnonymousType(json, new SecurityResponce()).data;

                List<Security> securities = new List<Security>();

                foreach (var product in serialiseObject)
                {
                    var newSec = new Security();

                    newSec.Name = product.symbol;
                    newSec.NameClass = product.settlementAsset;
                    newSec.NameFull = product.symbol;
                    newSec.NameId = product.symbol;
                    newSec.Decimals = product.priceFilter.tickSize.DecimalsCount();
                    newSec.SecurityType = SecurityType.CurrencyPair;
                    newSec.State = product.status == "Normal" ? SecurityStateType.Activ : SecurityStateType.Close;
                    newSec.Lot = 1;
                    newSec.PriceStep = product.priceFilter.tickSize.DecimalsCount();
                    newSec.PriceStepCost = newSec.PriceStep;
                    newSec.Go = product.lotSizeFilter.minQty.ToDecimal();

                    securities.Add(newSec);
                }

                SecurityEvent.Invoke(securities);

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.Message, LogMessageType.Error);
            }

        }
        #endregion

        #region DataRealTime

        private bool IsConnectedWebSocket;
        private Dictionary<string, WebSocket> _wsChanels = new Dictionary<string, WebSocket>();
        private ConcurrentQueue<string> _newMessageWebSocket = new ConcurrentQueue<string>();
        private object lockerWs = new object();

        public void Subscrible(Security security)
        {

            WebSocket _wsClient;

            if (!_wsChanels.ContainsKey(security.NameFull))
            {
                _wsClient = new WebSocket(WebsocketPublicChanel);

                _wsClient.Opened += new EventHandler((sender, e) => {
                    ConnectTradesChanel(sender, e, security.NameFull);
                });

                _wsClient.Closed += new EventHandler(DisconnectChanel);

                _wsClient.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>((sender, e) => { WsError(sender, e, security.NameFull); });

                _wsClient.MessageReceived += new EventHandler<MessageReceivedEventArgs>(PushMessage);



                if (_wsChanels.ContainsKey(security.NameFull))
                {
                    _wsChanels[security.NameFull].Close();
                    _wsChanels.Remove(security.NameFull);
                }

                _wsClient.Open();
                lock (lockerWs)
                {
                    _wsChanels.Add(security.NameFull, _wsClient);
                }
            }
        }

        private void PushMessage(object sender, MessageReceivedEventArgs e)
        {
            if (e.Message.Contains("ping"))
            {
                var client = (WebSocket)sender;
                client.Send("{\"op\": \"pong\"}");
                return;
            }
            else if (e.Message.Contains("connected"))
            {
                return;
            }
            else if (e.Message.Contains("code"))
            {
                return;
            }
            else
            {
                _newMessageWebSocket.Enqueue(e.Message);
            }
        }

        private void WsError(object sender, ErrorEventArgs e, string name)
        {
            var error = (ErrorEventArgs)e;
            if (error.Exception != null)
            {
                SendNewLogMessage(error.Exception.Message, LogMessageType.Error);
            }
        }

        private void DisconnectChanel(object sender, EventArgs e)
        {
            if (IsConnectedWebSocket)
            {
                IsConnectedWebSocket = false;

                _wsChanels.Clear();

                Dispose();
            }
        }

        private void ConnectTradesChanel(object sender, EventArgs e, string secName)
        {
            try
            {
                string jsonTrades = $"{{\"op\": \"sub\", \"ch\":\"trades: {secName}\"}}";
                var client = (WebSocket)sender;
                client.Send(jsonTrades);
                string jsonDepth = $"{{\"op\": \"sub\", \"id\": \"abc123\", \"ch\":\"bbo: {secName}\" }}";
                client.Send(jsonDepth);
                IsConnectedWebSocket = true;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.Message, LogMessageType.Error);
            }

        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            List<Candle> candles = new List<Candle>();

            try
            {
                int CountcandleToLoad = ((ServerParameterInt)ServerParameters.Find(param => param.Name.Equals("Candles to load"))).Value;

                HttpClient httpClient = new HttpClient();
                var response = httpClient.GetAsync(BaseUrl + $"api/pro/v1/barhist?symbol={nameSec}&interval={tf.TotalMinutes}&n={CountcandleToLoad}").Result;
                var json = response.Content.ReadAsStringAsync().Result;

                var serialiseObject = JsonConvert.DeserializeAnonymousType(json, new CandlesResponse());



                for (int j = 0; j < serialiseObject.data.Count; j++)
                {
                    Candle candle = new Candle();

                    candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(serialiseObject.data[j].data.ts));

                    candle.Open = Convert.ToDecimal(serialiseObject.data[j].data.o.Replace(".", ","));
                    candle.High = Convert.ToDecimal(serialiseObject.data[j].data.h.Replace(".", ","));
                    candle.Low = Convert.ToDecimal(serialiseObject.data[j].data.l.Replace(".", ","));
                    candle.Close = Convert.ToDecimal(serialiseObject.data[j].data.c.Replace(".", ","));
                    candle.Volume = Convert.ToDecimal(serialiseObject.data[j].data.v.Replace(".", ","));
                    var VolCcy = serialiseObject.data[j].data.i;

                    candles.Add(candle);


                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.Message, LogMessageType.Error);
            }


            return candles;
        }

        #endregion

        #region DataHistory

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region OrderChanel


        private bool IsConnectedWebSocketPrivate;
        private WebSocket _wsClientPrivate;
        private ConcurrentQueue<string> _newMessageWebSocketPrivate = new ConcurrentQueue<string>();

        public void SubscriblePrivateChanel()
        {
            _wsClientPrivate = new WebSocket(WebsocketPrivateChanel.Replace("<grp>", AccGroup));

            _wsClientPrivate.Opened += ConnectTradesChanelPrivate;

            _wsClientPrivate.Closed += new EventHandler(DisconnectChanelPrivate);

            _wsClientPrivate.Error += WsErrorPrivate;

            _wsClientPrivate.MessageReceived += new EventHandler<MessageReceivedEventArgs>(PushMessagePrivate);


            _wsClientPrivate.Open();

        }

        private void PushMessagePrivate(object sender, MessageReceivedEventArgs e)
        {
            if (e.Message.Contains("ping"))
            {
                var client = (WebSocket)sender;
                client.Send("{\"op\": \"pong\"}");
                return;
            }
            else if (e.Message.Contains("connected"))
            {
                return;
            }
            else if (e.Message.Contains("code"))
            {
                return;
            }
            else
            {
                // конвертер к ордерам сделать и генерирование трейдов из них
                _newMessageWebSocketPrivate.Enqueue(e.Message);
            }
        }

        private void WsErrorPrivate(object sender, ErrorEventArgs e)
        {
            var error = (ErrorEventArgs)e;
            if (error.Exception != null)
            {

                SendNewLogMessage(error.Exception.Message, LogMessageType.Error);

            }
        }

        private void DisconnectChanelPrivate(object sender, EventArgs e)
        {
            if (IsConnectedWebSocketPrivate)
            {
                IsConnectedWebSocketPrivate = false;

                _wsClientPrivate = null;

                Dispose();

            }
        }

        private void ConnectTradesChanelPrivate(object sender, EventArgs e)
        {
            try
            {
                var timeStamp = TimeManager.GetUnixTimeStampMilliseconds();

                var msg = $"{timeStamp}v2/stream";

                var auth = $"{{\"op\":\"auth\", \"t\": {timeStamp}, \"key\": \"{PublicKey}\", \"sig\": \"{CreateSignature(msg, SeckretKey)}\"}}";

                var client = (WebSocket)sender;
                client.Send(auth);

                client.Send("{\"op\":\"sub\", \"id\":\"sample-id\", \"ch\":\"futures-order\"}");
                IsConnectedWebSocketPrivate = true;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.Message, LogMessageType.Error);
            }

        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    // перенести в Entity

    #region Security

    public class SecurityResponce
    {
        public string code;
        public List<SecurityDetailsResponce> data;
    }

    public class SecurityDetailsResponce
    {
        public string symbol;
        public string status;
        public string displayName;
        public string settlementAsset;
        public string underlying;
        public string tradingStartTime;
        public PriceFilterSecurity priceFilter;
        public SizeFilterSecurity lotSizeFilter;
        public string commissionType;
        public string commissionReserveRate;
        public string marketOrderPriceMarkUp;


    }

    public class PriceFilterSecurity
    {
        public string minPrice;
        public string maxPrice;
        public string tickSize;
    }

    public class SizeFilterSecurity
    {
        public string minQty;
        public string maxQty;
        public string lotSize;
    }

    #endregion

    #region PositionFutures

    public class PositionResponceFutures
    {
        public string code;
        public PositionResponceFuturesDetail data;
    }

    public class PositionResponceFuturesDetail
    {
        public string accountId;
        public string ac;
        public List<PositionResponceFuturesCollaterals> collaterals;
        public List<PositionResponceFuturesContract> contracts;
    }


    public class PositionResponceFuturesCollaterals
    {
        public string asset;
        public string balance;
        public string referencePrice;
        public string discountfactor;
    }

    public class PositionResponceFuturesContract
    {
        public string symbol;
        public string positionMode;
        public string side;
        public string position;
        public string referenceCost;
        public string unrealizedPnl;
        public string realizedPnl;
        public string avgOpenPrice;
        public string marginType;
        public string isolatedMargin;
        public string leverage;
        public string takeProfitPrice;
        public string takeProfitTrigger;
        public string stopLossPrice;
        public string stopLossTrigger;
        public string buyOpenOrderNotional;
        public string sellOpenOrderNotional;
        public string markPrice;
    }

    #endregion

    #region Trades

    public class ResponseTrades
    {
        public string m;
        public string symbol;
        public List<ResponseTradeDetail> data;
    }

    public class ResponseTradeDetail
    {
        public string p;
        public string q;
        public string ts;
        public bool bm;
        public string seqnum;
    }

    #endregion

    #region Candle

    public class CandlesResponse
    {
        public string code;
        public List<CandlesResposeDetails> data;
    }

    public class CandlesResposeDetails
    {
        public string m;
        public string s;
        public CandleDataDetail data;
    }

    public class CandleDataDetail
    {
        public string i;
        public string ts;
        public string o;
        public string c;
        public string h;
        public string l;
        public string v;
    }

    #endregion

    #region AccoutInfo

    public class AccoutInfo
    {
        public string code;
        public string message;
        public AccountDetails data;
    }

    public class AccountDetails
    {
        public string accountGroup;
        public string email;
        public long expireTime;
        public string[] allowedIps;
        public string[] cashAccount;
        public string[] marginAccount;
        public string[] futuresAccount;
        public string userUID;
        public bool tradePermission;
        public bool transferPermission;
        public bool viewPermission;
        public string limitQuota;
    }

    #endregion

    #region DepthData

    public class BBOMessage
    {
        public string m;
        public string symbol;
        public BBODetails data;
    }

    public class BBODetails
    {
        public long ts;
        public string[] bid;
        public string[] ask;
    }

    #endregion
}
