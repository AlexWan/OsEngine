using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.TraderNet.Entity;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using OsEngine.Entity.WebSocketOsEngine;
using System.Collections;

namespace OsEngine.Market.Servers.TraderNet
{
    public class TraderNetServer : AServer
    {
        public TraderNetServer()
        {
            TraderNetServerRealization realization = new TraderNetServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
        }
    }

    public class TraderNetServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public TraderNetServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadMessageReader = new Thread(MessageReader);
            threadMessageReader.IsBackground = true;
            threadMessageReader.Name = "MessageReader";
            threadMessageReader.Start();
        }

        public void Connect(WebProxy proxy)
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(_publicKey) ||
                string.IsNullOrEmpty(_secretKey))
            {
                SendLogMessage("Can`t run TraderNet connector. No API keys",
                    LogMessageType.Error);
                return;
            }

            try
            {
                Dictionary<string, dynamic> data = new Dictionary<string, dynamic>();
                data.Add("apiKey", _publicKey);
                data.Add("cmd", "getSidInfo");

                HttpResponseMessage responseMessage = CreateAuthQuery($"/api/v2/cmd/getSidInfo", "POST", null, data);
                string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

                GetSID result = JsonConvert.DeserializeObject<GetSID>(JsonResponse);
                _sid = result.SID;

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    _FIFOListWebSocketMessage = new ConcurrentQueue<string>();
                    CreateWebSocketConnection();
                }
                else
                {
                    SendLogMessage("Connection can be open. TraderNet. Error request", LogMessageType.Error);

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage("Connection can be open. TraderNet. Error request", LogMessageType.Error);

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _subscribedSecurities.Clear();
                _listMD.Clear();
                _listTrades.Clear();

                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _FIFOListWebSocketMessage = null;

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.TraderNet; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _baseUrl = "https://tradernet.ru";

        private string _publicKey;

        private string _secretKey;

        private string _sid;              

        private Dictionary<string, List<ListMdTiker>> _listMD = new Dictionary<string, List<ListMdTiker>>();

        private Dictionary<string, ListTrades> _listTrades = new Dictionary<string, ListTrades>();

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {           
            GetSecuritiesFromExchange();
        }

        private void GetSecuritiesFromExchange()
        {
            try
            {
                List<string> listSecurities = GetSecList(_sid);

                if (listSecurities == null)
                {
                    return;
                }

                string strListSec = "";
                int count = 0;

                while (listSecurities.Count != 0)
                {
                    strListSec += listSecurities[0];
                    listSecurities.RemoveAt(0);
                    count++;

                    if (count == 50 || listSecurities.Count == 0)
                    {
                        GetQuerySecurities(strListSec);
                        strListSec = "";
                        count = 0;
                    }
                    else
                    {
                        strListSec += ", ";
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }       
        }

        private List<string> GetSecList(string sid)
        {
            try
            {
                Dictionary<string, dynamic> data = new Dictionary<string, dynamic>();

                data.Add("cmd", "getUserStockLists");
                data.Add("SID", sid);

                Dictionary<string, object> qData = new Dictionary<string, object>();

                qData.Add("q", data);

                HttpResponseMessage responseMessage = CreateQuery($"/api/", "POST", null, qData);

                if (responseMessage == null)
                {
                    return null;
                }

                string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

                if (!JsonResponse.Contains("osengine"))
                {
                    return null;
                }

                ResponseUserStockLists response = JsonConvert.DeserializeObject<ResponseUserStockLists>(JsonResponse);

                List<string> listSecurities = new List<string>();

                for (int i = 0; i < response.userStockLists.Count; i++)
                {
                    if (!response.userStockLists[i].name.Equals("osengine"))
                    {
                        continue;
                    }

                    listSecurities.AddRange(response.userStockLists[i].tickers);
                }

                return listSecurities;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private RateGate _rateGateSecurity = new RateGate(6, TimeSpan.FromMilliseconds(60000));

        private void GetQuerySecurities(string strListSec)
        {
            try
            {
                _rateGateSecurity.WaitToProceed(100);

                RequestSecurity reqData = new RequestSecurity();
                reqData.q = new RequestSecurity.Q();
                reqData.q.cmd = "getAllSecurities";
                reqData.q.@params = new RequestSecurity.Params();
                reqData.q.@params.take = 50;
                reqData.q.@params.filter = new RequestSecurity.Filter();
                reqData.q.@params.filter.filters = new List<RequestSecurity.FilterItem>();                
                reqData.q.@params.filter.filters.Add(new RequestSecurity.FilterItem());
                reqData.q.@params.filter.filters[0].field = "ticker";
                reqData.q.@params.filter.filters[0].@operator = "in";
                reqData.q.@params.filter.filters[0].value = strListSec;

                HttpResponseMessage responseMessage = CreateQuery("/api/", "POST", null, reqData);
                string jsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

                DeserializeDataSecurity(jsonResponse);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private List<Security> _securities;

        private void DeserializeDataSecurity(string jsonResponse)
        {
            try
            {
                ResponseMessageSecurities result = JsonConvert.DeserializeObject<ResponseMessageSecurities>(jsonResponse);

                _securities = new List<Security>();

                if (result == null)
                {
                    return;
                }

                if (result.securities.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < result.securities.Count; i++)
                {
                    ListSecurities item = result.securities[i];

                    Security newSecurity = new Security();

                    newSecurity.Exchange = ServerType.TraderNet.ToString();
                    newSecurity.DecimalsVolume = item.lot_size_q.DecimalsCount();
                    newSecurity.Lot = item.lot_size_q.ToDecimal();
                    newSecurity.Name = item.ticker;
                    newSecurity.NameFull = item.ticker;
                    newSecurity.NameId = item.instr_id;
                    newSecurity.SecurityType = GetSecurityType(Convert.ToInt32(item.instr_type_c));
                    newSecurity.NameClass = $"{item.mkt_short_code}_{newSecurity.SecurityType}";
                    newSecurity.Decimals = item.min_step.DecimalsCount();
                    newSecurity.PriceStep = item.step_price.ToDecimal();
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.State = SecurityStateType.Activ;
                    newSecurity.MinTradeAmount = item.quotes.x_lot.ToDecimal();
                    newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                    newSecurity.VolumeStep = item.quotes.x_lot.ToDecimal();

                    _securities.Add(newSecurity);
                }
                SecurityEvent(_securities);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private SecurityType GetSecurityType(int code)
        {
            SecurityType _securityType = SecurityType.None;

            switch (code)
            {
                case (1):
                    _securityType = SecurityType.Stock;
                    break;
                case (2):
                    _securityType = SecurityType.Bond;
                    break;
                case (3):
                    _securityType = SecurityType.Futures;
                    break;
                case (4):
                    _securityType = SecurityType.Option;
                    break;
                case (5):
                    _securityType = SecurityType.Index;
                    break;
                case (6):
                    _securityType = SecurityType.CurrencyPair;
                    break;
                case (7):
                    _securityType = SecurityType.CurrencyPair;
                    break;
            }
            return _securityType;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public void GetPortfolios()
        {
        }
              
        private bool _portfolioIsStarted = false;

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.Now;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            List<Candle> result = GetCandleData(security, timeFrameBuilder, startTime, endTime, endTime);

            return result;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return GetCandleData(security, timeFrameBuilder, startTime, endTime, actualTime);
        }

        private List<Candle> GetCandleData(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!CheckTf(tfTotalMinutes))
            {
                return null;
            }

            List<Candle> allCandles = new List<Candle>();

            DateTime startTimeReq = startTime;
            DateTime endTimeReq = startTimeReq.AddMinutes(tfTotalMinutes * 100000);

            if (endTimeReq > endTime)
            {
                endTimeReq = endTime;
            }

            do
            {
                List<Candle> candles = RequestCandleHistory(security, tfTotalMinutes, startTimeReq, endTimeReq);

                if (candles == null)
                {
                    return null;
                }

                if (allCandles.Count == 0)
                {
                    allCandles.AddRange(candles);
                }

                if (candles.Count == 1 &&
                    allCandles[allCandles.Count - 1].TimeStart == candles[0].TimeStart)
                {
                    break;
                }

                while (true)
                {
                    if (candles.Count == 0)
                    {
                        break;
                    }

                    if (candles[0].TimeStart <= allCandles[allCandles.Count - 1].TimeStart)
                    {
                        candles.RemoveAt(0);
                    }
                    else
                    {
                        allCandles.AddRange(candles);
                        break;
                    }
                }

                if (allCandles[allCandles.Count - 1].TimeStart < endTime)
                {
                    startTimeReq = TimeZoneInfo.ConvertTimeFromUtc(allCandles[allCandles.Count - 1].TimeStart, TimeZoneInfo.Local);
                    endTimeReq = startTimeReq.AddMinutes(tfTotalMinutes * 100000);

                    if (endTimeReq > endTime)
                    {
                        endTimeReq = endTime;
                    }
                }
                else
                {
                    break;
                }

            } while (true);

            if (allCandles.Count > 1)
            {
                if (allCandles[0].TimeStart.Date != allCandles[1].TimeStart.Date)
                {
                    allCandles.RemoveAt(0);
                }
            }

            return allCandles;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
                startTime >= DateTime.Now ||
                actualTime > endTime ||
                actualTime > DateTime.Now ||
                endTime < DateTime.UtcNow.AddYears(-20))
            {
                return false;
            }
            return true;
        }

        private bool CheckTf(int timeFrameMinutes)
        {
            if (timeFrameMinutes == 1 ||
                timeFrameMinutes == 5 ||
                timeFrameMinutes == 15 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 1440)
            {
                return true;
            }
            return false;
        }

        private readonly RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(3000));

        private List<Candle> RequestCandleHistory(Security security, int interval, DateTime startTime, DateTime endTime)
        {
            _rgCandleData.WaitToProceed();

            try
            {
                RequestCandle reqData = new RequestCandle();
                reqData.q = new RequestCandle.Q();
                reqData.q.cmd = "getHloc";
                reqData.q.@params = new RequestCandle.Params();
                reqData.q.@params.id = security.Name;
                reqData.q.@params.timeframe = interval;
                reqData.q.@params.count = -1;
                reqData.q.@params.date_from = startTime.ToString("dd.MM.yyyy HH:mm");
                reqData.q.@params.date_to = endTime.ToString("dd.MM.yyyy HH:mm");

                HttpResponseMessage responseMessage = CreateQuery($"/api/", "POST", null, reqData);
                string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

                return ConvertCandles(JsonResponse);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private List<Candle> ConvertCandles(string JsonResponse)
        {
            try
            {
                ResponseCandle result = JsonConvert.DeserializeObject<ResponseCandle>(JsonResponse);

                if (result.hloc == null)
                {
                    return null;
                }

                List<List<string>> listHloc = new List<List<string>>();
                List<string> listVl = new List<string>();
                List<string> listSeries = new List<string>();

                IDictionaryEnumerator enumerator = result.hloc.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    listHloc = (List<List<string>>)enumerator.Value;
                }
                enumerator.Reset();

                enumerator = result.vl.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    listVl = (List<string>)enumerator.Value;
                }
                enumerator.Reset();

                enumerator = result.xSeries.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    listSeries = (List<string>)enumerator.Value;
                }
                enumerator.Reset();

                List<Candle> candles = new List<Candle>();

                for (int i = 0; i < listHloc.Count; i++)
                {
                    if (CheckCandlesToZeroData(listHloc[i]))
                    {
                        continue;
                    }

                    Candle candle = new Candle();

                    candle.State = CandleState.Finished;
                    candle.High = listHloc[i][0].ToDecimal();
                    candle.Low = listHloc[i][1].ToDecimal();
                    candle.Open = listHloc[i][2].ToDecimal();
                    candle.Close = listHloc[i][3].ToDecimal();
                    candle.Volume = listVl[i].ToDecimal();
                    DateTime time = TimeManager.GetDateTimeFromTimeStampSeconds(long.Parse(listSeries[i]));
                    candle.TimeStart = time.AddHours(3);

                    candles.Add(candle);
                }
                return candles;
            }
            catch (Exception ex)
            {
                SendLogMessage($"ConvertCandles: {ex.Message}", LogMessageType.Error);
                return null;
            }
        }

        private bool CheckCandlesToZeroData(List<string> item)
        {
            if (item[0].ToDecimal() == 0 ||
                item[1].ToDecimal() == 0 ||
                item[2].ToDecimal() == 0 ||
                item[3].ToDecimal() == 0)
            {
                return true;
            }
            return false;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private string _webSocketUrl = "wss://wss.tradernet.ru";

        private WebSocket _webSocket;

        private void CreateWebSocketConnection()
        {
            try
            {
                if (_webSocket != null)
                {
                    return;
                }

                string url = _webSocketUrl + $"/?SID={_sid}";
                
                _webSocket = new WebSocket(url);
                
                /*_webSocket.SslConfiguration.EnabledSslProtocols
                    = System.Security.Authentication.SslProtocols.Tls12
                   | System.Security.Authentication.SslProtocols.Tls13;*/
                _webSocket.EmitOnPing = true;
                _webSocket.OnOpen += WebSocket_Opened;
                _webSocket.OnClose += WebSocket_Closed;
                _webSocket.OnMessage += WebSocket_MessageReceived;
                _webSocket.OnError += WebSocket_Error;
                _webSocket.ConnectAsync();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebSocketConnection()
        {
            if (_webSocket != null)
            {
                try
                {
                    _webSocket.OnOpen -= WebSocket_Opened;
                    _webSocket.OnClose -= WebSocket_Closed;
                    _webSocket.OnMessage -= WebSocket_MessageReceived;
                    _webSocket.OnError -= WebSocket_Error;
                    _webSocket.CloseAsync();
                }
                catch
                {
                    // ignore
                }

                _webSocket = null;
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocket_Opened(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("TraderNet WebSocket connection open", LogMessageType.System);

                    ServerStatus = ServerConnectStatus.Connect;

                    if (ConnectEvent != null)
                    {
                        ConnectEvent();
                    }

                    _listMD = new Dictionary<string, List<ListMdTiker>>();
                    _listTrades = new Dictionary<string, ListTrades>();

                    CreateSubcribePrivate();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_Closed(object sender, CloseEventArgs e)
        {
            try
            {
                if (DisconnectEvent != null
                 & ServerStatus != ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("Connection Closed by TraderNet. WebSocket Closed Event", LogMessageType.System);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }
                /*if (e.Message.Length == 4)
                { // pong message
                    return;
                }*/

                if (_FIFOListWebSocketMessage == null)
                {
                    return;
                }

                _FIFOListWebSocketMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocket_Error(object sender, ErrorEventArgs e)
        {
            if (e.Exception != null)
            {
                SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        #endregion

        #region 9 Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(550));

        private List<string> _subscribedSecurities = new List<string>();

        private void CreateSubcribePrivate()
        {
            _rateGateSubscribe.WaitToProceed();

            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }
                                
                _webSocket.SendAsync("[\"portfolio\"]");
                _webSocket.SendAsync("[\"orders\"]");

            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();
                CreateSubscribeSecurityMessageWebSocket(security);

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSubscribeSecurityMessageWebSocket(Security security)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (_subscribedSecurities != null)
                {
                    for (int i = 0; i < _subscribedSecurities.Count; i++)
                    {
                        if (_subscribedSecurities[i].Equals(security.Name))
                        {
                            return;
                        }
                    }
                }

                _subscribedSecurities.Add(security.Name);

                string quotesResponse = $"[\"quotes\", {GetStringFromList(_subscribedSecurities)}]";
                string orderbookResponse = $"[\"orderBook\", {GetStringFromList(_subscribedSecurities)}]";

                _webSocket.SendAsync(quotesResponse);
                _webSocket.SendAsync(orderbookResponse);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private string GetStringFromList(List<string> list)
        {
            string strFromList = "[";
            for (int i = 0; i < list.Count; i++)
            {
                strFromList += $"\"{list[i]}\"";

                if (i < list.Count - 1)
                {
                    strFromList += ", ";
                }
                else
                {
                    strFromList += "]";
                }
            }
            return strFromList;
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> _FIFOListWebSocketMessage = new ConcurrentQueue<string>();

        private void MessageReader()
        {
            Thread.Sleep(5000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_FIFOListWebSocketMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message = null;

                    _FIFOListWebSocketMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.StartsWith("[\"portfolio"))
                    {
                        UpdatePortfolio(message);
                        continue;
                    }

                    if (message.StartsWith("[\"orders"))
                    {
                        UpdateOrder(message);
                        continue;
                    }

                    if (message.StartsWith("[\"q\""))
                    {
                        UpdateTrade(message);
                        continue;
                    }

                    if (message.StartsWith("[\"b\""))
                    {
                        UpdateDepth(message);
                        continue;
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private string CutString(string message)
        {
            int count = message.IndexOf("{");

            string str = message.Remove(0, count);
            str = str.Remove(str.Length - 1);
            count = str.LastIndexOf(",");
            str = str.Remove(count);

            return str;
        }

        private void UpdatePortfolio(string message)
        {
            try
            {
                string str = CutString(message);
                ResponsePortfolio positions = JsonConvert.DeserializeObject<ResponsePortfolio>(str);

                if (positions == null)
                {
                    return;
                }

                Portfolio portfolio = new Portfolio();
                portfolio.Number = "TraderNet";
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;

                if (positions.acc.Count > 0)
                {
                    for (int i = 0; i < positions.acc.Count; i++)
                    {
                        PositionOnBoard pos = new PositionOnBoard();

                        pos.PortfolioName = "TraderNet";
                        pos.SecurityNameCode = positions.acc[i].curr;
                        pos.ValueBlocked = 0;
                        pos.ValueCurrent = positions.acc[i].s.ToDecimal();

                        if (_portfolioIsStarted == false)
                        {
                            pos.ValueBegin = pos.ValueCurrent;
                        }

                        portfolio.SetNewPosition(pos);
                    }
                }

                if (positions.pos.Count > 0)
                {
                    for (int i = 0; i < positions.pos.Count; i++)
                    {
                        PositionOnBoard pos = new PositionOnBoard();
                        pos.PortfolioName = "TraderNet";
                        pos.SecurityNameCode = positions.pos[i].i;
                        pos.ValueCurrent = positions.pos[i].q.ToDecimal();
                        pos.ValueBlocked = 0;

                        if (_portfolioIsStarted == false)
                        {
                            pos.ValueBegin = pos.ValueCurrent;
                        }

                        portfolio.SetNewPosition(pos);
                    }
                }
               
                _portfolioIsStarted = true;

                PortfolioEvent(new List<Portfolio> { portfolio });
            }
            catch (Exception e)
            {
                SendLogMessage("TraderNet - UpdatePortfolio: " + e.Message, LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                List<ResponseOrders> responseOrder = GetJsonString(message);

                for (int i = 0; i < responseOrder.Count; i++)
                {
                    Order newOrder = ConvertResponseToOrder(responseOrder[i]);

                    MyOrderEvent(newOrder);

                    if (newOrder.State == OrderStateType.Partial ||
                    newOrder.State == OrderStateType.Done)
                    {
                        for (int j = 0; j < responseOrder[i].trade.Count; j++)
                        {
                            MyTrade myTrade = new MyTrade();
                            DateTime.TryParse(responseOrder[i].trade[j].date, out myTrade.Time);
                            myTrade.SecurityNameCode = responseOrder[i].instr;
                            myTrade.NumberOrderParent = responseOrder[i].id.ToString();
                            myTrade.NumberTrade = responseOrder[i].trade[j].id;
                            myTrade.Volume = responseOrder[i].trade[j].q.ToDecimal();
                            myTrade.Price = responseOrder[i].trade[j].p.ToDecimal();
                            myTrade.Side = GetOrderSide(responseOrder[i].oper);

                            MyTradeEvent(myTrade);
                        }                    
                    }
                }
            }
            
            catch (Exception ex)
            {
                SendLogMessage($"TraderNet - UpdateOrder: {ex.Message}", LogMessageType.Error);
            }
        }

        private OrderPriceType GetTypeOrder(string type)
        {
            if (type == "2")
            {
                return OrderPriceType.Limit;
            }
            return OrderPriceType.Market;
        }

        private Side GetOrderSide(string side)
        {     
            if (side == "1" || side == "2")
            {
                return Side.Buy;
            }

            return Side.Sell;            
        }

        private void UpdateTrade(string message)
        {
            try
            {
                if (!message.Contains("ltp") ||
                    !message.Contains("lts") ||
                    !message.Contains("ltt"))
                {
                    return;
                }

                string str = CutString(message);
                ResponseTrade responseTrade = JsonConvert.DeserializeObject<ResponseTrade>(str);

                if (responseTrade == null)
                {
                    return;
                }

                if (responseTrade.rev == null)
                {
                    return;
                }

                GetSnapshotTrades(responseTrade);

                Trade trade = new Trade();
                trade.SecurityNameCode = responseTrade.c;
                trade.Price = _listTrades[responseTrade.c].ltp.ToDecimal();
                trade.Id = responseTrade.rev;
                DateTime.TryParse(_listTrades[responseTrade.c].ltt, out trade.Time);                
                trade.Volume = _listTrades[responseTrade.c].lts.ToDecimal();
                trade.Side = Side.Buy;

                NewTradesEvent(trade);
            }
            catch (Exception ex)
            {
                SendLogMessage("TraderNet - UpdateTrade: " + ex.Message, LogMessageType.Error);
            }
        }

        private void GetSnapshotTrades(ResponseTrade responseTrade)
        {
            if (!_listTrades.ContainsKey(responseTrade.c))
            {
                _listTrades.Add(responseTrade.c, new ListTrades());
            }

            _listTrades[responseTrade.c].ltp = responseTrade.ltp ?? _listTrades[responseTrade.c].ltp;
            _listTrades[responseTrade.c].lts = responseTrade.lts ?? _listTrades[responseTrade.c].lts;
            _listTrades[responseTrade.c].ltt = responseTrade.ltt ?? _listTrades[responseTrade.c].ltt;
        }

        private void UpdateDepth(string message)
        {
            try
            {
                string str = CutString(message);
                ResponseDepth responseDepth = JsonConvert.DeserializeObject<ResponseDepth>(str);

                if (responseDepth == null)
                {
                    return;
                }

                GetSnapshotMD(responseDepth);

                MarketDepth marketDepth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                marketDepth.SecurityNameCode = responseDepth.i;

                for (int j = 0; j < _listMD[responseDepth.i].Count; j++)
                {
                    if (_listMD[responseDepth.i][j].s == "S")
                    {
                        MarketDepthLevel level = new MarketDepthLevel();
                        level.Ask = _listMD[responseDepth.i][j].q.ToDouble();
                        level.Price = _listMD[responseDepth.i][j].p.ToDouble();
                        ascs.Add(level);
                    }
                    else
                    {
                        MarketDepthLevel level = new MarketDepthLevel();
                        level.Bid = _listMD[responseDepth.i][j].q.ToDouble();
                        level.Price = _listMD[responseDepth.i][j].p.ToDouble();
                        bids.Add(level);
                    }
                }

                ascs.Reverse();

                marketDepth.Asks = ascs;
                marketDepth.Bids = bids;

                marketDepth.Time = DateTime.UtcNow;

                if (marketDepth.Time < _lastTimeMd)
                {
                    marketDepth.Time = _lastTimeMd;
                }
                if (marketDepth.Time == _lastTimeMd)
                {
                    _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);
                    marketDepth.Time = _lastTimeMd;
                }

                _lastTimeMd = marketDepth.Time;

                MarketDepthEvent(marketDepth);

            }
            catch (Exception ex)
            {
                SendLogMessage("TraderNet - UpdateDepth: " + ex.Message, LogMessageType.Error);
            }
        }

        private void GetSnapshotMD(ResponseDepth responseDepth)
        {
            if (_listMD.ContainsKey(responseDepth.i) && responseDepth.n == "0")
            {
                _listMD.Remove(responseDepth.i);
            }

            if (!_listMD.ContainsKey(responseDepth.i))
            {
                _listMD.Add(responseDepth.i, new List<ListMdTiker>());
            }

            if (responseDepth.del.Count > 0)
            {
                for (int i = 0; i < responseDepth.del.Count; i++)
                {
                    _listMD[responseDepth.i].RemoveAt(Convert.ToInt32(responseDepth.del[i].k));
                }
            }

            if (responseDepth.ins.Count > 0)
            {
                for (int i = 0; i < responseDepth.ins.Count; i++)
                {
                    ListMdTiker list = new ListMdTiker();
                    list.p = responseDepth.ins[i].p;
                    list.q = responseDepth.ins[i].q;
                    list.s = responseDepth.ins[i].s;

                    _listMD[responseDepth.i].Insert(Convert.ToInt32(responseDepth.ins[i].k), list);
                }
            }

            if (responseDepth.upd.Count > 0)
            {
                for (int i = 0; i < responseDepth.upd.Count; i++)
                {
                    ListMdTiker list = new ListMdTiker();
                    list.p = responseDepth.upd[i].p;
                    list.q = responseDepth.upd[i].q;
                    list.s = responseDepth.upd[i].s;

                    _listMD[responseDepth.i].RemoveAt(Convert.ToInt32(responseDepth.upd[i].k));
                    _listMD[responseDepth.i].Insert(Convert.ToInt32(responseDepth.upd[i].k), list);
                }
            }
        }

        private DateTime _lastTimeMd;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public void SendOrder(Order order)
        {
            try
            {                
                _rateGateSendOrder.WaitToProceed();
                                
                Dictionary<string, dynamic> data = new Dictionary<string, dynamic>();

                Dictionary<string, dynamic> paramsDict = new Dictionary<string, dynamic>();

                paramsDict.Add("instr_name", order.SecurityNameCode);
                paramsDict.Add("action_id", order.Side == Side.Buy ? "1" : "3");
                paramsDict.Add("order_type_id", order.TypeOrder == OrderPriceType.Market ? "1" : "2");
                paramsDict.Add("qty", order.Volume.ToString());
                paramsDict.Add("limit_price", order.Price.ToString().Replace(",", "."));
                paramsDict.Add("user_order_id", order.NumberUser.ToString());

                data.Add("apiKey", _publicKey);
                data.Add("cmd", "putTradeOrder");
                data.Add("params", paramsDict);

                HttpResponseMessage responseMessage = CreateAuthQuery($"/api/v2/cmd/putTradeOrder", "POST", null, data);
                string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

                if (!JsonResponse.Contains("order_id"))
                {
                    order.State = OrderStateType.Fail;

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }

                    SendLogMessage($"SendOrder: {JsonResponse}", LogMessageType.Error);
                }                               
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            try
            {
                _rateGateCancelOrder.WaitToProceed();

                Dictionary<string, dynamic> data = new Dictionary<string, dynamic>();

                Dictionary<string, dynamic> paramsDict = new Dictionary<string, dynamic>();

                paramsDict.Add("order_id", order.NumberMarket.ToString());
               
                data.Add("apiKey", _publicKey);
                data.Add("cmd", "delTradeOrder");
                data.Add("params", paramsDict);

                HttpResponseMessage responseMessage = CreateAuthQuery($"/api/v2/cmd/delTradeOrder", "POST", null, data);
                string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

                if (!JsonResponse.Contains("result"))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }

            return false;
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOpenOrders();

            if (orders == null)
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            try
            {
                Dictionary<string, dynamic> data = new Dictionary<string, dynamic>();

                Dictionary<string, dynamic> paramsDict = new Dictionary<string, dynamic>();

                paramsDict.Add("active_only", "0");

                data.Add("apiKey", _publicKey);
                data.Add("cmd", "getNotifyOrderJson");
                data.Add("params", paramsDict);

                HttpResponseMessage responseMessage = CreateAuthQuery($"/api/v2/cmd/getNotifyOrderJson", "POST", null, data);
                string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

                if (JsonResponse.Contains("errMsg"))
                {
                    SendLogMessage($"GetOrderStatus: {JsonResponse}", LogMessageType.Error);
                    return OrderStateType.None;
                }

                ResponseRestOrders response = JsonConvert.DeserializeObject<ResponseRestOrders>(JsonResponse);

                for (int i = 0; i < response.result.orders.order.Count; i++)
                {
                    ResponseOrders item = response.result.orders.order[i];

                    Int32.TryParse(item.userOrderId.Split(':')[1], out int number);

                    if (number != order.NumberUser)
                    {
                        continue;
                    }

                    Order newOrder = ConvertResponseToOrder(item);

                    if (newOrder != null
                    && MyOrderEvent != null)
                    {
                        MyOrderEvent(newOrder);
                    }

                    if (newOrder.State == OrderStateType.Done ||
                        newOrder.State == OrderStateType.Partial)
                    {
                        for (int j = 0; j < item.trade.Count; j++)
                        {
                            MyTrade myTrade = new MyTrade();
                            DateTime.TryParse(item.trade[j].date, out myTrade.Time);
                            myTrade.SecurityNameCode = item.instr;
                            myTrade.NumberOrderParent = item.id.ToString();
                            myTrade.NumberTrade = item.trade[j].id;
                            myTrade.Volume = item.trade[j].q.ToDecimal();
                            myTrade.Price = item.trade[j].p.ToDecimal();
                            myTrade.Side = GetOrderSide(item.oper);

                            MyTradeEvent(myTrade);
                        }
                    }

                    return newOrder.State;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetOrderStatus: {ex.Message}", LogMessageType.Error);
            }

            return OrderStateType.None;
        }

        private List<ResponseOrders> GetJsonString(string jsonResponse)
        {
            int count = jsonResponse.IndexOf(",");
            string str = jsonResponse.Remove(0, count+1);
            count = str.LastIndexOf(",");
            str = str.Remove(count);

            List<ResponseOrders> responseOrder = JsonConvert.DeserializeObject<List<ResponseOrders>>(str);

            if (responseOrder == null)
            {
                return null;
            }

            return responseOrder;
        }

        private Order ConvertResponseToOrder(ResponseOrders responseOrder)
        {
            if (string.IsNullOrEmpty(responseOrder.id))
            {
                return null;
            }

            Order newOrder = new Order();

            newOrder.SecurityNameCode = responseOrder.instr;
            newOrder.SecurityClassCode = GetClassSecurity(newOrder.SecurityNameCode);
            DateTime.TryParse(responseOrder.date, out newOrder.TimeCallBack);            
            newOrder.NumberMarket = responseOrder.id.ToString();
            newOrder.Side = GetOrderSide(responseOrder.oper);
            newOrder.State = GetOrderState(responseOrder.stat);
            newOrder.Volume = responseOrder.q.ToDecimal();
            newOrder.Price = responseOrder.p.ToDecimal();
            newOrder.ServerType = ServerType.TraderNet;
            newOrder.PortfolioNumber = "TraderNet";
            newOrder.TypeOrder = GetTypeOrder(responseOrder.type);

            if (responseOrder.userOrderId != null)
            {
                int count = responseOrder.userOrderId.IndexOf(":");
                string str = responseOrder.userOrderId.Remove(0, count + 1);
                int.TryParse(str, out newOrder.NumberUser);
            }

            return newOrder;
        }

        private string GetClassSecurity(string securityNameCode)
        {
            if (_securities == null)
            {
                return null;
            }

            for (int i = 0; i < _securities.Count; i++)
            {
                if (_securities[i].Name == securityNameCode)
                {
                    return _securities[i].NameClass;
                }
            }

            return null;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
        }

        public void CancelAllOrdersToSecurity(Security security)
        {            
        }

        public void CancelAllOrders()
        {
        }

        public List<Order> GetAllOpenOrders()
        {
            try
            {
                Dictionary<string, dynamic> data = new Dictionary<string, dynamic>();

                Dictionary<string, dynamic> paramsDict = new Dictionary<string, dynamic>();

                paramsDict.Add("active_only", "1");

                data.Add("cmd", "getNotifyOrderJson");
                data.Add("apiKey", _publicKey);
                data.Add("params", paramsDict);

                HttpResponseMessage responseMessage = CreateAuthQuery($"/api/v2/cmd/getNotifyOrderJson", "POST", null, data);
                string JsonResponse = responseMessage.Content.ReadAsStringAsync().Result;

                if (JsonResponse.Contains("errMsg"))
                {
                    SendLogMessage($"GetAllOpenOrders: {JsonResponse}", LogMessageType.Error);
                    return null;
                }

                if (!JsonResponse.Contains("order_id"))
                {
                    return null;
                }

                ResponseRestOrders response = JsonConvert.DeserializeObject<ResponseRestOrders>(JsonResponse);
                                
                List<Order> orders = new List<Order>();

                for (int i = 0; i < response.result.orders.order.Count; i++)
                {                   
                    if (GetOrderState(response.result.orders.order[i].stat) == OrderStateType.Active)
                    {
                        orders.Add(ConvertResponseToOrder(response.result.orders.order[i]));
                    }                    
                }

                return orders;
            }
            catch (Exception e)
            {
                SendLogMessage($"GetAllOpenOrders: {e.Message}", LogMessageType.Error);
                return null;
            }
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("10"):
                    stateType = OrderStateType.Active;
                    break;
                case ("11"):
                    stateType = OrderStateType.Pending;
                    break;
                case ("1"):
                    stateType = OrderStateType.Pending;
                    break;
                case ("20"):
                    stateType = OrderStateType.Partial;
                    break;
                case ("21"):
                    stateType = OrderStateType.Done;
                    break;
                case ("31"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("2"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("30"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("71"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("0"):
                    stateType = OrderStateType.Fail;
                    break;
                case ("70"):
                    stateType = OrderStateType.Fail;
                    break;
                case ("74"):
                    stateType = OrderStateType.Fail;
                    break;
                case ("75"):
                    stateType = OrderStateType.Fail;
                    break;
                default:
                    stateType = OrderStateType.None;
                    break;
            }

            return stateType;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        #endregion

        #region 12 Queries

        private HttpClient _httpClient = new HttpClient();

        private HttpResponseMessage CreateAuthQuery(string path, string method, string queryString, dynamic reqData)
        {
            try
            {
                string str = QueryData(reqData);

                string url = $"{_baseUrl}{path}";
                string strFromDict = StrFromDict(reqData);
                string signature = GenerateSignature(_secretKey, strFromDict);

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-NtApi-Sig", signature);

                return _httpClient.PostAsync(url, new StringContent(str, Encoding.UTF8, "application/x-www-form-urlencoded")).Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        private string QueryData(Dictionary<string, object> reqData)
        {
            string str = "";

            IDictionaryEnumerator enumFirst = reqData.GetEnumerator();

            while (enumFirst.MoveNext())
            {
                if (enumFirst.Value is Dictionary<string, dynamic>)
                {
                    Dictionary<string, dynamic> value = (Dictionary<string, dynamic>)enumFirst.Value;

                    IDictionaryEnumerator enumSec = value.GetEnumerator();

                    while (enumSec.MoveNext())
                    {                       
                        if (enumSec.Value as string == null)
                        {
                            continue;
                        }
                        if (str != "")
                        {
                            str += "&";
                        }

                        string s = enumSec.Value as string;
                        s = s.Replace(" ", "%20");
                        s = s.Replace(":", "%3A");
                        str += $"{enumFirst.Key}[{enumSec.Key}]={s}";
                    }
                    enumSec.Reset();

                    continue;
                }
                if (str != "")
                {
                    str += "&";
                }

                str += $"{enumFirst.Key}={enumFirst.Value}";
            }
            enumFirst.Reset();

            // Пример сборки строки:
            //"apiKey=0e54f1028e8&cmd=getHloc&params[count]=-1&params[date_from]=15.08.2024%2000%3A00&params[date_to]=16.08.2024%2000%3A00&params[id]=TATN&params[intervalMode]=ClosedRay&params[timeframe]=1440";

            return str;
        }

        public static string StrFromDict(Dictionary<string, object> dictionary)
        {
            List<string> strings = new List<string>();

            SortedDictionary<string, object> sortedDict = new SortedDictionary<string, object>(dictionary);
            
            IDictionaryEnumerator enumerator = sortedDict.GetEnumerator();

            while (enumerator.MoveNext())
            {
                object value = enumerator.Value;

                if (value is Dictionary<string, object>)
                    value = StrFromDict((Dictionary<string, object>)value);
                else if (value is List<object>)
                    value = SimpleList((List<object>)value);
                else
                    value = value.ToString();

                strings.Add($"{enumerator.Key}={value}");
            }
            enumerator.Reset();

            // Пример сборки строки:
            // apiKey=80dc85c96c1d0&cmd=getNotifyOrderJson&params=active_only=1

            return string.Join("&", strings);
        }

        private static string SimpleList(List<object> rawList)
        {
            List<string> stringValues = new List<string>();

            for (int i = 0; i < rawList.Count; i++)
            {
                string stringValue = rawList[i].ToString();
                stringValues.Add($"'{stringValue}'");
            }
            
            string stringList = string.Join(", ", stringValues);
            return $"[{stringList}]";
        }

        private HttpResponseMessage CreateQuery(string path, string method, string stringData, dynamic jsonData)
        {
            try
            {
                string json = stringData;
                string contentType = "application/x-www-form-urlencoded";

                if (jsonData != null)
                {
                    json = JsonConvert.SerializeObject(jsonData);
                    contentType = "application/json";
                }

                string url = $"{_baseUrl}{path}";

                _httpClient.DefaultRequestHeaders.Clear();

                if (method.Equals("POST"))
                {
                    return _httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, contentType)).Result;
                }
                else
                {
                    return _httpClient.GetAsync(url).Result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        public string GenerateSignature(string key, string message, string algorithmName = "sha256")
        {
            using HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));

            byte[] hash;
            if (string.IsNullOrEmpty(message))
            {
                hash = hmac.ComputeHash(new byte[0]);
            }
            else
            {
                hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            }

            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion
    }
}