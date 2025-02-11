/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Mexc.Json;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Globalization;
using WebSocket4Net;
using System.Linq;

namespace OsEngine.Market.Servers.Mexc
{
    public class MexcServer : AServer
    {
        public MexcServer()
        {
            MexcServerRealization realization = new MexcServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }
    }

    public class MexcServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public MexcServerRealization()
        {
            Thread worker = new Thread(ConnectionCheckThread);
            worker.Name = "CheckAliveMexc";
            worker.Start();

            Thread worker2 = new Thread(DataMessageReader);
            worker2.Name = "DataMessageReaderMexc";
            worker2.Start();

            Thread worker3 = new Thread(PortfolioMessageReader);
            worker3.Name = "PortfolioMessageReaderMexc";
            worker3.Start();
        }

        public void Connect()
        {
            try
            {
                _securities.Clear();
                _myPortfolios.Clear();
                //_activeSecurities.Clear();

                SendLogMessage("Start Mexc Connection", LogMessageType.System);

                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

                if (string.IsNullOrEmpty(_publicKey) 
                    || string.IsNullOrEmpty(_secretKey) )
                {
                    SendLogMessage("Connection terminated. You must specify the public and private keys. You can get it on the Mexc website",
                        LogMessageType.Error);
                    return;
                }

                _restClient = new MexcRestClient(_publicKey, _secretKey);

                if (CheckConnection() == false)
                {
                    SendLogMessage("Authorization Error. Probably an invalid keys are specified. You can see it on the Mexc website.",
                    LogMessageType.Error);
                    return;
                }

                CreateWebSocketConnection();
                
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
            }
        }

        private bool CheckConnection()
        {
            return GetCurrentPortfolio();
        }

        public void Dispose()
        {
            try
            {
                _securities.Clear();
                _myPortfolios.Clear();
                //_activeSecurities.Clear();

                DeleteWebSocketConnection();

                SendLogMessage("Dispose. Connection Closed by Mexc. WebSocket Data Closed Event", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.Mexc;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        private string _publicKey;

        private string _secretKey;

        public List<IServerParameter> ServerParameters { get; set; }

        private MexcRestClient _restClient;

        #endregion

        #region 3 Securities

        private List<Security> _securities = new List<Security>();

        // map: client_order_id -> bool.
        ConcurrentDictionary<string, bool> _activeSecurities = new ConcurrentDictionary<string, bool>();

        public void GetSecurities()
        {
            UpdateSec();

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                if (SecurityEvent != null)
                {
                    SecurityEvent.Invoke(_securities);
                }
            }

        }

        private void UpdateSec()
        {
            string endPoint = "/api/v3/exchangeInfo";

            try
            {
                HttpResponseMessage response = _restClient.Get(endPoint, secured: false);

                string content = response.Content.ReadAsStringAsync().Result;

                //SendLogMessage("symbols: " + content, LogMessageType.Connect);

                MexcSecurityList securities = JsonConvert.DeserializeAnonymousType(content, new MexcSecurityList());

                UpdateSecuritiesFromServer(securities);
            }
            catch (Exception exception)
            {
                SendLogMessage("Securities request error: " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecuritiesFromServer(MexcSecurityList securities)
        {
            try
            {
                if (securities == null || securities.symbols == null ||
                    securities.symbols.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < securities.symbols.Count; i++)
                {
                    MexcSecurity sec = securities.symbols[i];

                    if (!sec.isSpotTradingAllowed)
                    {
                        continue;
                    }

                    Security security = new Security();
                    security.Name = sec.symbol;
                    security.NameFull = sec.symbol;
                    security.NameClass = sec.quoteAsset;
                    security.NameId = sec.symbol + sec.quoteAsset;
                    security.SecurityType = SecurityType.CurrencyPair;
                    security.Exchange = ServerType.Mexc.ToString();
                    security.Lot = 1;
                    security.PriceStep = GetPriceStep(sec.baseAssetPrecision);
                    security.PriceStepCost = security.PriceStep;


                    if (security.PriceStep < 1)
                    {
                        string prStep = security.PriceStep.ToString(CultureInfo.InvariantCulture);
                        security.Decimals = Convert.ToString(prStep).Split('.')[1].Split('1')[0].Length + 1;
                    }
                    else
                    {
                        security.Decimals = 0;
                    }

                    if (sec.status == "1")
                    {
                        security.State = SecurityStateType.Activ;
                    }
                    else
                    {
                        security.State = SecurityStateType.Close;
                    }

                    _securities.Add(security);
                }
            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading stocks: {e.Message}" + e.ToString(), LogMessageType.Error);
            }
        }

        private decimal GetPriceStep(int ScalePrice)
        {
            if (ScalePrice == 0)
            {
                return 1;
            }
            string priceStep = "0,";
            for (int i = 0; i < ScalePrice - 1; i++)
            {
                priceStep += "0";
            }

            priceStep += "1";

            return priceStep.ToDecimal();
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        private string _portfolioName = "Mexc";

        public void GetPortfolios()
        {
            GetCurrentPortfolio();
        }

        private bool GetCurrentPortfolio()
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string endPoint = $"/api/v3/account";

                HttpResponseMessage response = _restClient.Get(endPoint, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {


                    MexcPortfolioRest portfolio =
                        JsonConvert.DeserializeAnonymousType(content, new MexcPortfolioRest());

                    ConvertToPortfolio(portfolio);

                    return true;

                }
                else
                {
                    SendLogMessage("Portfolio request error. Status: " 
                        + response.StatusCode + "  " + this._portfolioName + 
                        ", " + content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio request error " + exception.ToString(), LogMessageType.Error);
            }

            return false;
        }

        private void ConvertToPortfolio(MexcPortfolioRest basePortfolio)
        {
            if (basePortfolio == null)
            {
                return;
            } 

            Portfolio portfolio = new Portfolio();
            portfolio.Number = this._portfolioName;
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            for (int i = 0; i < basePortfolio.balances.Count; i++)
            {
                MexcBalance item = basePortfolio.balances[i];

                PositionOnBoard pos = new PositionOnBoard()
                {
                    PortfolioName = this._portfolioName,
                    SecurityNameCode = item.asset,
                    ValueBlocked = item.locked.ToDecimal(),
                    ValueCurrent = item.free.ToDecimal()
                };

                portfolio.SetNewPosition(pos);
            }

            if (_myPortfolios.Count > 0)
            {
                _myPortfolios[0] = portfolio;
            }
            else
            {
                _myPortfolios.Add(portfolio);
            }
            

            if (PortfolioEvent != null)
            {
                PortfolioEvent(_myPortfolios);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        private RateGate _rateGateGetData = new RateGate(1, TimeSpan.FromMilliseconds(250));

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            DateTime endTime = DateTime.Now.ToUniversalTime();

            while(endTime.Hour != 23)
            {
                endTime = endTime.AddHours(1);
            }

            int candlesInDay = 0;

            if(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes >= 1)
            {
                candlesInDay = 900 / Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
            }
            else
            {
                candlesInDay = 54000/ Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalSeconds);
            }

            if(candlesInDay == 0)
            {
                candlesInDay = 1;
            }

            int daysCount = candleCount / candlesInDay;

            if(daysCount == 0)
            {
                daysCount = 1;
            }

            daysCount++;

            if(daysCount > 5)
            { // добавляем выходные
                daysCount = daysCount + (daysCount / 5) * 2;
            }

            DateTime startTime = endTime.AddDays(-daysCount);

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);
        
            while(candles.Count > candleCount)
            {
                candles.RemoveAt(0);
            }

            return candles;
        }

        //private readonly HashSet<int> _allowedTf = new HashSet<int> {1,5,15,30,60,240,1440,10080};
        private readonly Dictionary<int, string> _allowedTf = new Dictionary<int, string>()
        {
            { 1, "1m"},
            { 5, "5m"  },
            { 15,  "15m"  },
            { 30, "30m"  },
            { 60,  "60m"  },
            { 240,  "4h" },
            { 1440, "1d" },
            { 10080,  "1W"  },
        };

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, 
                        DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!_allowedTf.ContainsKey(tfTotalMinutes))
                return null;

            
            string tf = _allowedTf[tfTotalMinutes];

            if (startTime != actualTime)
            {
                startTime = actualTime;
            }

            List<Candle> candles = new List<Candle>();

            // 500 - max candles at Mexc
            TimeSpan additionTime = TimeSpan.FromMinutes(tfTotalMinutes * 500);

            DateTime endTimeReal = startTime.Add(additionTime);

            while (startTime < endTime)
            {
                MexcCandlesHistory history = GetHistoryCandle(security, tf, startTime, endTimeReal);
                List<Candle> newCandles = ConvertToOsEngineCandles(history);

                if (newCandles != null &&
                    newCandles.Count > 0)
                {

                    //SendLogMessage("Get Candles: " + security + " tf=" + tfTotalMinutes +
                    //    " start=" + startTime.ToString() + " first=" + newCandles[0].TimeStart +
                    //    " end=" + endTimeReal.ToString() + " last=" + newCandles[newCandles.Count - 1].TimeStart
                    //    , LogMessageType.Connect);

                    //It could be 2 same candles from different requests - check and fix
                    if (candles.Count > 0)
                    {
                        Candle last = candles[candles.Count - 1];
                        for (int i = 0; i < newCandles.Count; i++) {
                            if (newCandles[i].TimeStart > last.TimeStart)
                            {
                                candles.Add(newCandles[i]);
                            }     
                        }
                    } 
                    else
                    {
                        candles = newCandles;
                    }
                } 
                else
                {
                    SendLogMessage("Got Empty Candles: " + security + " tf=" + tfTotalMinutes +
                        " start=" + startTime.ToString() + 
                        " end=" + endTimeReal.ToString() 
                        , LogMessageType.Connect);
                }

                startTime = endTimeReal;
                endTimeReal = startTime.Add(additionTime);
            }

            while (candles != null &&
                candles.Count != 0 && 
                candles[candles.Count - 1].TimeStart > endTime)
            {
                candles.RemoveAt(candles.Count - 1);
            }

            return candles;
        }

        private MexcCandlesHistory GetHistoryCandle(Security security, string timeFrame,
            DateTime startTime, DateTime endTime)
        {
            _rateGateGetData.WaitToProceed();

            string endPoint = "/api/v3/klines?symbol=" + security.Name;
            
            // (UTC) in Unix Time Seconds
            endPoint += "&interval=" + timeFrame;
            endPoint += "&startTime=" + TimeManager.GetTimeStampMilliSecondsToDateTime(startTime);
            endPoint += "&endTime=" + TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);
            endPoint += "&limit=500";

            //SendLogMessage("Get Candles: " + endPoint, LogMessageType.Connect);

            try
            {
                HttpResponseMessage response = _restClient.Get(endPoint, secured: false);

                string content = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcCandlesHistory parsed =
                        JsonConvert.DeserializeAnonymousType(content, new MexcCandlesHistory());

                    return parsed;

                }
                else
                {
                    SendLogMessage("Candles request error to url='" + endPoint+ "'. Status: " + 
                        response.StatusCode + ". Message: " + content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Candles request error:" + exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private List<Candle> ConvertToOsEngineCandles(MexcCandlesHistory candles)
        {
            if (candles == null)
                return null;

            List<Candle> result = new List<Candle>();

            for(int i = 0; i < candles.Count;i++)
            {
                List<object> curCandle = candles[i];

                Candle newCandle = new Candle();
                newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStamp( (long) curCandle[0]);
                try
                {
                    newCandle.Open = curCandle[1].ToString().ToDecimal();
                    newCandle.High = curCandle[2].ToString().ToDecimal();
                    newCandle.Low = curCandle[3].ToString().ToDecimal();
                    newCandle.Close = curCandle[4].ToString().ToDecimal();
                    newCandle.Volume = curCandle[5].ToString().ToDecimal();
                }
                catch (Exception ex)
                {
                    SendLogMessage("Candles conversion error:" + ex.ToString(), LogMessageType.Error);
                }

                //fix candle
                if (newCandle.Open < newCandle.Low)
                    newCandle.Open = newCandle.Low;
                if (newCandle.Open > newCandle.High)
                    newCandle.Open = newCandle.High;

                if (newCandle.Close < newCandle.Low)
                    newCandle.Close = newCandle.Low;
                if (newCandle.Close > newCandle.High)
                    newCandle.Close = newCandle.High;

                result.Add(newCandle);
            }

            return result;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;

        }

        #endregion

        #region 6 WebSocket creation

        private readonly string _wsPublic = "wss://wbs.mexc.com/ws";

        private readonly string _wsPrivate = "wss://wbs.mexc.com/ws";

        private string _socketLocker = "webSocketLockerMexc";

        private string _listenKey = "";

        private void CreateWebSocketConnection()
        {
            try
            {
                if (_webSocketData != null)
                {
                    return;
                }

                _socketDataIsActive = false;
                _socketPortfolioIsActive = false;

                lock (_socketLocker)
                {
                    _webSocketDataMessage = new ConcurrentQueue<string>();
                    _webSocketPortfolioMessage = new ConcurrentQueue<string>();

                    _webSocketData = new WebSocket(_wsPublic);
                    _webSocketData.EnableAutoSendPing = true;
                    _webSocketData.AutoSendPingInterval = 15;
                    _webSocketData.Opened += WebSocketData_Opened;
                    _webSocketData.Closed += WebSocketData_Closed;
                    _webSocketData.MessageReceived += WebSocketData_MessageReceived;
                    _webSocketData.Error += WebSocketData_Error;
                    _webSocketData.Open();

                    //get Listen Key
                    _listenKey = GetListenKey();
                    string uri = _wsPrivate + "?listenKey=" + _listenKey;

                    _webSocketPortfolio = new WebSocket(uri);

                    _webSocketPortfolio.EnableAutoSendPing = true;
                    _webSocketPortfolio.AutoSendPingInterval = 15;
                    _webSocketPortfolio.Opened += WebSocketPortfolio_Opened;
                    _webSocketPortfolio.Closed += WebSocketPortfolio_Closed;
                    _webSocketPortfolio.MessageReceived += WebSocketPortfolio_MessageReceived;
                    _webSocketPortfolio.Error += WebSocketPortfolio_Error;
                    _webSocketPortfolio.DataReceived += _webSocketPortfolio_DataReceived;
                    _webSocketPortfolio.Open();

                }

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebSocketConnection()
        {
            try
            {
                lock (_socketLocker)
                {
                    if (_webSocketData != null)
                    {
                        try
                        {
                            _webSocketData.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        try
                        {
                            SendLogMessage("Close webSocketPortfolio", LogMessageType.Connect);
                            _webSocketPortfolio.Close();
                        }
                        catch
                        {
                            // ignore
                        }

                        _webSocketData.Opened -= WebSocketData_Opened;
                        _webSocketData.Closed -= WebSocketData_Closed;
                        _webSocketData.MessageReceived -= WebSocketData_MessageReceived;
                        _webSocketData.Error -= WebSocketData_Error;
                        _webSocketData = null;

                        _webSocketPortfolio.Opened -= WebSocketPortfolio_Opened;
                        _webSocketPortfolio.Closed -= WebSocketPortfolio_Closed;
                        _webSocketPortfolio.MessageReceived -= WebSocketPortfolio_MessageReceived;
                        _webSocketPortfolio.Error -= WebSocketPortfolio_Error;
                        _webSocketPortfolio = null;
                    }
                }
            }
            catch
            {

            }
            finally
            {
                _webSocketData = null;
            }
        }

        private string GetListenKey()
        {
            try
            {
                string endPoint = "/api/v3/userDataStream";

                HttpResponseMessage response = _restClient.Post(endPoint, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcListenKey parsed =
                            JsonConvert.DeserializeAnonymousType(content, new MexcListenKey());

                    return parsed.listenKey;
                }
                else
                {

                    SendLogMessage("GetListenKey Fail. Status: "
                        + response.StatusCode + ", " + content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("GetListenKey error " + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private void ProlongListenKey()
        {
            if (string.IsNullOrEmpty(_listenKey))
            {
                SendLogMessage("Can't prolong empty ListenKey", LogMessageType.Connect);
                return;
            }

            try
            {
                string endPoint = "/api/v3/userDataStream";

                Dictionary<string, string> query = new Dictionary<string, string>
                {
                    { "listenKey", _listenKey }
                };

                HttpResponseMessage response = _restClient.Put(endPoint, query, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcListenKey parsed =
                            JsonConvert.DeserializeAnonymousType(content, new MexcListenKey());
                    //SendLogMessage("ProlongListenKey Success. Key: " + parsed.listenKey, LogMessageType.Connect);
                }
                else
                {

                    SendLogMessage("ProlongListenKey Fail. Status: "
                        + response.StatusCode + ", " + content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("ProlongListenKey send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private bool _socketDataIsActive;

        private bool _socketPortfolioIsActive;

        private void CheckActivationSockets()
        {
            if (_socketDataIsActive == false)
            {
                return;
            }

            if (_socketPortfolioIsActive == false)
            {
                return;
            }

            try
            {
                SendLogMessage("All sockets activated. Connect State", LogMessageType.Connect);
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

        }

        private WebSocket _webSocketData;

        private WebSocket _webSocketPortfolio;

        private void PingSocket()
        {
            if ( _socketDataIsActive == false)
            {
                SendLogMessage("PingSocket: Socket is not active", LogMessageType.Connect);
                return;
            }
            string message = "{\"method\":\"PING\"}";
            _webSocketData.Send(message);
        }

        private void SubcribeToOrderData()
        {
            WSRequestOrder obj = new WSRequestOrder();
            string message = obj.GetJson();
            SendLogMessage("SubcribeToOrderData: " + message, LogMessageType.Connect);
            _webSocketPortfolio.Send(message);

            Thread.Sleep(1500);
        }

        private void SubcribeToPortfolio()
        {
            Thread.Sleep(2000); 
            
            WSRequestBalance bObj = new WSRequestBalance();
            string message = bObj.GetJson();
            SendLogMessage("Porfolio Send: " + message, LogMessageType.Connect);
            _webSocketPortfolio.Send(message);
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketData_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Data activated", LogMessageType.System);
            _socketDataIsActive = true;
            CheckActivationSockets();
        }

        private void WebSocketData_Closed(object sender, EventArgs e)
        {
            try
            {
                SendLogMessage("WebSocketData. Connection Closed by Mexc. WebSocket Data Closed Event", LogMessageType.Error);

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketData_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs error)
        {
            try
            {
                if (error.Exception != null)
                {
                    SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketData_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(e.Message))
                {
                    return;
                }

                if (e.Message.Length == 4)
                { // pong message
                    return;
                }

                if ( e.Message.IndexOf("\"msg\"") >= 0 )
                {
                    // responce message - ignore
                    //SendLogMessage("WebSocketData, message:" + e.Message, LogMessageType.Connect);
                    return;
                }

                if (_webSocketDataMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                _webSocketDataMessage.Enqueue(e.Message);
            }
            catch (Exception error)
            {
                SendLogMessage("Trade socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPortfolio_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Portfolio activated", LogMessageType.System);
            _socketPortfolioIsActive = true;

            CheckActivationSockets();

            SubcribeToPortfolio();

            SubcribeToOrderData();
        }

        private void WebSocketPortfolio_Closed(object sender, EventArgs e)
        {
            try
            {
                SendLogMessage("Portfolio Connection Closed by Mexc. WebSocket Portfolio Closed Event", LogMessageType.Error);

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPortfolio_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs error)
        {
            try
            {
                if (error.Exception != null)
                {
                    SendLogMessage("webSocketPortfolio Error" + error.Exception.ToString(), LogMessageType.Error);
                }
                else
                {
                    SendLogMessage("webSocketPortfolio Error" + error.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Portfolio socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPortfolio_DataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    SendLogMessage("PorfolioWebSocket DataReceived Empty message: State=" + ServerStatus.ToString(),
                        LogMessageType.Connect);

                    return;
                }


                if (e.Data.Length == 0)
                { // pong message
                    return;
                }


                if (_webSocketPortfolioMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                string message = e.Data.ToString(); 

                _webSocketPortfolioMessage.Enqueue(message);

            }
            catch (Exception error)
            {
                SendLogMessage("Portfolio socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPortfolio_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(e.Message))
                {
                    return;
                }

                if (e.Message.Length == 4)
                { // pong message
                    return;
                }

                if (_webSocketPortfolioMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                _webSocketPortfolioMessage.Enqueue(e.Message);
            }
            catch (Exception error)
            {
                SendLogMessage("Portfolio socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        private DateTime _lastTimeCheckConnection = DateTime.MinValue;

        private DateTime _lastTimeProlongListenKey = DateTime.MinValue;

        private void ConnectionCheckThread()
        {
            while (true)
            {
                Thread.Sleep(10000);

                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    if (_lastTimeCheckConnection.AddSeconds(15) < DateTime.Now)
                    {
                        if (CheckConnection() == false)
                        {
                            // try again
                            if (CheckConnection() == false)
                            {
                                if (ServerStatus == ServerConnectStatus.Connect)
                                {
                                    ServerStatus = ServerConnectStatus.Disconnect;
                                    DisconnectEvent();
                                }
                            }
                        }

                        PingSocket();

                        _lastTimeCheckConnection = DateTime.Now;
                    }

                    if (_lastTimeProlongListenKey.AddMinutes(30) < DateTime.Now)
                    {
                        ProlongListenKey();

                        _lastTimeProlongListenKey = DateTime.Now;
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        #endregion

        #region 9 WebSocket Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(150));

        public void Subscrible(Security security)
        {
            try
            {
                _activeSecurities.TryAdd(security.Name, true);

                _rateGateSubscribe.WaitToProceed();

                // trades subscription
                string name = security.Name;

                WSRequestSubscribe tradeSubscribe = new WSRequestSubscribe(WSRequestSubscribe.Channel.Trade, security.Name);
                string messageTradeSub = tradeSubscribe.GetJson(); 
                SendLogMessage("Send to WS: " + messageTradeSub, LogMessageType.Connect);
                _webSocketData.Send(messageTradeSub);

                _rateGateSubscribe.WaitToProceed();

                // market depth subscription
                WSRequestSubscribe depthSubscribe = new WSRequestSubscribe(WSRequestSubscribe.Channel.Depth, security.Name);
                string messageMdSub = depthSubscribe.GetJson();
                SendLogMessage("Send to WS: " + messageMdSub, LogMessageType.Connect);
                _webSocketData.Send(messageMdSub);

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(),LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent;

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> _webSocketDataMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _webSocketPortfolioMessage = new ConcurrentQueue<string>();

        private void DataMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_webSocketDataMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    _webSocketDataMessage.TryDequeue(out message);
                    
                    if (message == null)
                    {
                        continue;
                    }

                    SoketBaseMessage baseMessage = 
                        JsonConvert.DeserializeAnonymousType(message, new SoketBaseMessage());

                    if(baseMessage == null 
                        || string.IsNullOrEmpty(baseMessage.c))
                    {
                        continue;
                    }

                    //SendLogMessage("message: " + message, LogMessageType.Connect);

                    if (baseMessage.c.Contains(".depth."))
                    {
                        UpDateMarketDepth(baseMessage);

                    }
                    else if (baseMessage.c.Contains(".deals."))
                    {
                        UpDateTrade(baseMessage);
                    }
                    else
                    {
                        SendLogMessage("Unknown message: " + baseMessage.c, LogMessageType.Error);
                    }

                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(2000);
                }
            }
        }

        private void UpDateTrade(SoketBaseMessage baseMessage)
        {
            MexcDeals deals =
                JsonConvert.DeserializeAnonymousType(baseMessage.d.ToString(), new MexcDeals());

            if (deals == null || deals.deals == null || deals.deals.Count == 0 || string.IsNullOrEmpty(baseMessage.s)) 
            {
                SendLogMessage("Wrong 'Trade' message:" + baseMessage.ToString(), LogMessageType.Error);
                return;
            }

            for (int i = 0; i < deals.deals.Count; i++)
            {
                MexcDeal deal = deals.deals[i];

                Trade trade = new Trade();
                trade.SecurityNameCode = baseMessage.s;
                trade.Price = deal.p.ToDecimal();
                trade.Time = ConvertToDateTimeFromUnixFromMilliseconds(deal.t.ToString());
                trade.Id = deal.t.ToString() + deal.S + baseMessage.s;

                if (deal.S == 1) 
                {
                    trade.Side = Side.Buy;
                } else
                {
                    trade.Side = Side.Sell; 
                }
                
                trade.Volume = deal.v.ToDecimal();

                NewTradesEvent?.Invoke(trade);
            }
        }

        private void UpDateMarketDepth(SoketBaseMessage baseMessage)
        {
            MexcDepth baseDepth =
                JsonConvert.DeserializeAnonymousType(baseMessage.d.ToString(), new MexcDepth());

            if (baseDepth == null) 
            {
                SendLogMessage("Wrong 'MarketDepth' message:" + baseMessage.ToString(), LogMessageType.Error);
                return;
            }

            MarketDepth depth = new MarketDepth();
            depth.SecurityNameCode = baseMessage.s;
            depth.Time = ConvertToDateTimeFromUnixFromMilliseconds(baseMessage.t.ToString());


            for (int k = 0; k < baseDepth.bids.Count; k++)
            {
                MarketDepthLevel newBid = new MarketDepthLevel();
                newBid.Price = baseDepth.bids[k].p.ToDecimal();
                newBid.Bid = baseDepth.bids[k].v.ToDecimal();
                depth.Bids.Add(newBid);
            }

            for (int k = 0; k < baseDepth.asks.Count; k++)
            {
                MarketDepthLevel newAsk = new MarketDepthLevel();
                newAsk.Price = baseDepth.asks[k].p.ToDecimal();
                newAsk.Ask = baseDepth.asks[k].v.ToDecimal();
                depth.Asks.Add(newAsk);
            }

            if (_lastMdTime != DateTime.MinValue &&
                _lastMdTime >= depth.Time)
            {
                depth.Time = _lastMdTime.AddMilliseconds(1);
            }

            _lastMdTime = depth.Time;

            MarketDepthEvent?.Invoke(depth);
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        private void PortfolioMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_webSocketPortfolioMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    _webSocketPortfolioMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    //SendLogMessage("PorfolioWebSocket Reader: " + message, LogMessageType.Connect);

                    if (message.Contains("\"msg\""))
                    {
                        continue;
                    }

                    SoketBaseMessage baseMessage = JsonConvert.DeserializeAnonymousType(message, new SoketBaseMessage());

                    if (baseMessage == null
                        || string.IsNullOrEmpty(baseMessage.c))
                    {
                        continue;
                    }

                    if (baseMessage.c.Contains(".account."))
                    {
                        UpDateMyPortfolio(baseMessage);

                    }
                    else if (baseMessage.c.Contains(".orders."))
                    {
                        UpDateMyOrder(baseMessage);
                    }
                    //else if (baseMessage.c.Contains(".deals."))
                    //{
                    //    // Ignore
                    //}
                    else
                    {
                        SendLogMessage("Unknown message: " + baseMessage.c, LogMessageType.Error);
                    }

                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(2000);
                }
            }
        }

        private void UpdateTrades(Order order)
        {
            if (string.IsNullOrEmpty(order.NumberMarket))
            {
                SendLogMessage("UpdateTrades: Empty NumberMarket", LogMessageType.Error);
                return;
            }
            List<MyTrade> trades = GetTradesForOrder(order.NumberMarket, order.SecurityNameCode);

            if (trades == null)
                return;

            for (int i = 0; i < trades.Count; i++)
            {
                MyTradeEvent?.Invoke(trades[i]);
            }
        }

        private void UpDateMyOrder(SoketBaseMessage baseMessage)
        {
            //SendLogMessage("UpDateMyOrder: " + data, LogMessageType.Connect);

            MexcSocketOrder baseOrder =
                JsonConvert.DeserializeAnonymousType(baseMessage.d.ToString(), new MexcSocketOrder());

            if (baseOrder == null)
            {
                return;
            }

            Order order = new Order();

            order.NumberMarket = baseOrder.i;

            try
            {
                order.NumberUser = Convert.ToInt32(baseOrder.c);
            }
            catch
            {
                SendLogMessage("Wrong client order id: " + baseOrder.c, LogMessageType.Connect);
                return;
            }

            order.SecurityNameCode = baseMessage.s;
            order.Side = Side.Buy;
            if (baseOrder.S == 2) 
            {
                order.Side = Side.Sell;
            }
            order.PortfolioNumber = this._portfolioName;
            order.Volume = baseOrder.v;
            order.VolumeExecute = order.Volume - baseOrder.V;
            order.Price = baseOrder.p;

            //LIMIT_ORDER(1),POST_ONLY(2),IMMEDIATE_OR_CANCEL(3),
            //FILL_OR_KILL(4),MARKET_ORDER(5); STOP_LIMIT(100)
            if (baseOrder.o == 1)
            {
                order.TypeOrder = OrderPriceType.Limit;
            } else
            {
                order.TypeOrder = OrderPriceType.Market;
            }

            order.TimeCreate = ConvertToDateTimeFromUnixFromMilliseconds(baseOrder.O.ToString());
            order.TimeCallBack = ConvertToDateTimeFromUnixFromMilliseconds(baseMessage.t.ToString());
            order.ServerType = ServerType.Mexc;

            //status 1:New order 2:Filled 3:Partially filled 4:Order canceled
            //5:Order filled partially, and then the rest of the order is canceled

            if (baseOrder.s == 1)
            {
                order.State = OrderStateType.Active;
            }
            else if (baseOrder.s == 2)
            {
                order.State = OrderStateType.Done;
            }
            else if (baseOrder.s == 3)
            {
                order.State = OrderStateType.Partial;
            }
            else if (baseOrder.s == 4 || baseOrder.s == 5)
            {
                if (order.VolumeExecute > 0)
                {
                    order.State = OrderStateType.Done;
                }
                else
                {
                    order.State = OrderStateType.Cancel;
                }
            }

            MyOrderEvent?.Invoke(order);

            if (MyTradeEvent != null &&
                (order.State == OrderStateType.Done || order.State == OrderStateType.Partial))
            {
                UpdateTrades(order);
            }
        }

        private void UpDateMyPortfolio(SoketBaseMessage baseMessage)
        {

            MexcSocketBalance balance =
                JsonConvert.DeserializeAnonymousType(baseMessage.d.ToString(), new MexcSocketBalance());

            Portfolio portf = null;
            if (_myPortfolios != null && _myPortfolios.Count > 0)
            {
                portf = _myPortfolios[0];
            }

            if(portf == null)
            {
                return;
            }

            if (balance != null && !string.IsNullOrEmpty(balance.a)) 
            {
                PositionOnBoard pos = new PositionOnBoard();
                pos.ValueCurrent = balance.f.ToDecimal();
                pos.ValueBlocked = balance.l.ToDecimal();
                pos.PortfolioName = this._portfolioName;
                pos.SecurityNameCode = balance.a;

                portf.SetNewPosition(pos);
            }

            PortfolioEvent?.Invoke(_myPortfolios);
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(1000));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(500));

        private RateGate _rateGateGetOrder = new RateGate(1, TimeSpan.FromMilliseconds(500));

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();
            _activeSecurities.TryAdd(order.SecurityNameCode, true);

            try
            {
                string endPoint = "/api/v3/order";

                string side = "BUY";
                if (order.Side == Side.Sell)
                {
                    side = "SELL";
                }

                Dictionary<string, string> query = new Dictionary<string, string>()
                {
                    {"symbol", order.SecurityNameCode},
                    {"side", side},
                    {"type", "LIMIT"},
                    {"quantity", order.Volume.ToString().Replace(',', '.') },
                    {"price",  order.Price.ToString().Replace(',', '.') },
                    {"newClientOrderId", order.NumberUser.ToString() },
                };

                HttpResponseMessage response = _restClient.Post(endPoint, query, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcNewOrderResponse parsed =
                        JsonConvert.DeserializeAnonymousType(content, new MexcNewOrderResponse());

                    if (parsed != null &&  !string.IsNullOrEmpty(parsed.orderId))
                    {
                        //Everything is OK
                        SendLogMessage($"Order created: {content}", LogMessageType.Connect);

                    } else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Order created, but answer is wrong: {content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage("Order Fail. Status: "
                        + response.StatusCode + "  " + order.SecurityNameCode + ", " + content, LogMessageType.Error);

                    CreateOrderFail(order);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            //unsupported by API
        }

        public void CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            try { 

                string endPoint = "/api/v3/order";

                Dictionary<string, string> query = new Dictionary<string, string>();
                query.Add("symbol", order.SecurityNameCode);
                query.Add("origClientOrderId", order.NumberUser.ToString());

                HttpResponseMessage response = _restClient.Delete(endPoint, query, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;



                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcOrderResponse parsed =
                        JsonConvert.DeserializeAnonymousType(content, new MexcOrderResponse());

                    if (parsed != null)
                    {
                        //Everything is OK - do nothing
                        SendLogMessage("Cancel order - OK: " + content, LogMessageType.Connect);
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Cancel order, answer is wrong: {content}",                           LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage("Cancel order failed. Status: "
                        + response.StatusCode + "  " + order.SecurityNameCode + ", " + content, LogMessageType.Error);

                    CreateOrderFail(order);
                }

            }
            catch (Exception exception)
            {
                SendLogMessage("Cancel order error." + exception.ToString(), LogMessageType.Error);
            }
        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange();

            if (orders == null)
                return;

            for (int i = 0; i < orders.Count;i++)
            {
                Order order = orders[i];

                if(order.State == OrderStateType.Active)
                {
                    CancelOrder(order);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            List<Order> orders = GetAllOrdersFromExchange(security.Name);

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Active
                    && order.SecurityNameCode == security.Name)
                {
                    CancelOrder(order);
                }
            }
        }

        private List<Order> GetAllOrdersFromExchange()
        {
            string[] symbols = _activeSecurities.Keys.ToArray();

            List<Order> ret = null;

            for (int i = 0; i < symbols.Length; i++)
            {
                List<Order> orders = GetAllOrdersFromExchange(symbols[i]);
                if (ret == null)
                {
                    ret = orders;
                }
                else
                {
                    ret.AddRange(orders);
                }
            }

            return ret;
        }

        private List<Order> GetAllOrdersFromExchange(string symbol)
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string endPoint = "/api/v3/openOrders";

                Dictionary<string, string> query = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(symbol))
                {
                    query.Add("symbol", symbol);
                }

                HttpResponseMessage response = _restClient.Get(endPoint,
                            query,
                            secured: true);

                string content = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    try
                    {
                        MexcOrderListResponse parsed =
                            JsonConvert.DeserializeAnonymousType(content, new MexcOrderListResponse());

                        if (parsed != null && parsed.Count > 0)
                        {
                            List<Order> osEngineOrders = new List<Order>();

                            for (int i = 0; i < parsed.Count; i++)
                            {
                                Order newOrd = ConvertRestOrdersToOsEngineOrder(parsed[i]);

                                if (newOrd == null)
                                {
                                    continue;
                                }

                                osEngineOrders.Add(newOrd);
                            }

                            return osEngineOrders;
                        }

                    }
                    catch (Exception exception)
                    {
                        SendLogMessage("Get all orders. Failed to parse: " + content + "\n exception: " + exception.ToString(), LogMessageType.Error);
                    }
                }
                else if(response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    SendLogMessage("Get all orders request error. ", LogMessageType.Error);

                    if (content != null)
                    {
                        SendLogMessage("Fail reasons: " + content, LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private Order GetOrderFromExchange(string userOrderId, string symbol)
        {
            _rateGateGetOrder.WaitToProceed();

            if (string.IsNullOrEmpty(userOrderId))
            {
                SendLogMessage("Order ID is empty", LogMessageType.Connect);
                return null;
            }

            try
            {
                string endPoint = "/api/v3/order";

                Dictionary<string, string> query = new Dictionary<string, string>
                {
                    { "symbol", symbol },
                    { "origClientOrderId", userOrderId }
                };

                HttpResponseMessage response = _restClient.Get(endPoint, query, secured: true);
                string content = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcOrderResponse parsed =
                        JsonConvert.DeserializeAnonymousType(content, new MexcOrderResponse());

                    if (parsed != null)
                    {
                        //Everything is OK
                        MexcOrderResponse baseOrder = JsonConvert.DeserializeAnonymousType(content, new MexcOrderResponse());

                        Order order = ConvertRestOrdersToOsEngineOrder(baseOrder);

                        return order;
                    }
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    SendLogMessage("Order not found: " + userOrderId, LogMessageType.Connect);
                    return null;
                }
                else
                {
                    SendLogMessage("Get order request "+ userOrderId +" error: " + content, 
                        LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get order request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public void GetAllActivOrders()
        {
            List<Order> ordersOnBoard = GetAllOrdersFromExchange();

            if (ordersOnBoard == null)
            {
                return;
            }

            for (int i = 0; i < ordersOnBoard.Count; i++)
            {
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(ordersOnBoard[i]);
                }
            }
        }

        public void GetOrderStatus(Order order)
        {
            _activeSecurities.TryAdd(order.SecurityNameCode, true);

            Order myOrder = GetOrderFromExchange(order.NumberUser.ToString(), order.SecurityNameCode);

            if (myOrder == null)
            {
                return;
            }

            MyOrderEvent?.Invoke(myOrder);

            if (myOrder.State == OrderStateType.Done || myOrder.State == OrderStateType.Partial)
            {
                UpdateTrades(myOrder);
            }
        }

        private Order ConvertRestOrdersToOsEngineOrder(MexcOrderResponse baseOrder)
        {
            Order order = new Order();

            order.SecurityNameCode = baseOrder.symbol;
            order.Volume = baseOrder.origQty.ToDecimal();
            order.VolumeExecute = baseOrder.executedQty.ToDecimal();

            order.PortfolioNumber = this._portfolioName;

            if (baseOrder.type == "LIMIT")
            {
                order.Price = baseOrder.price.ToDecimal();
                order.TypeOrder = OrderPriceType.Limit;
            }
            else if (baseOrder.type == "MARKET")
            {
                order.TypeOrder = OrderPriceType.Market;
            }

            try
            {
                order.NumberUser = Convert.ToInt32(baseOrder.clientOrderId);
            }
            catch
            {
                return null;
            }

            order.NumberMarket = baseOrder.orderId.ToString();

            if (baseOrder.updateTime != null)
            {
                order.TimeCallBack = ConvertToDateTimeFromUnixFromMilliseconds(baseOrder.updateTime.ToString());
            } 
            else
            {
                order.TimeCallBack = ConvertToDateTimeFromUnixFromMilliseconds(baseOrder.time.ToString());
            }

            if (baseOrder.side == "BUY")
            {
                order.Side = Side.Buy;
            }
            else
            {
                order.Side = Side.Sell;
            }

            if (baseOrder.status == "NEW")
            {
                order.State = OrderStateType.Active;
            }
            else if (baseOrder.status == "PARTIALLY_FILLED")
            {
                order.State = OrderStateType.Partial;
            }
            else if (baseOrder.status == "FILLED")
            {
                order.State = OrderStateType.Done;
            }
            else if (baseOrder.status == "CANCELED" || baseOrder.status  == "PARTIALLY_CANCELED")
            {
                if (order.VolumeExecute > 0)
                {
                    order.State = OrderStateType.Done;
                }
                else
                {
                    order.State = OrderStateType.Cancel;
                }
            }

            return order;
        }

        private List<MyTrade> GetTradesForOrder(string orderId, string symbol)
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string endPoint = $"/api/v3/myTrades";

                Dictionary<string, string> query = new Dictionary<string, string>
                {
                    { "symbol", symbol },
                    { "orderId", orderId }
                };

                HttpResponseMessage response = _restClient.Get(endPoint, query, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;

                //SendLogMessage("Order trades resp: " + content, LogMessageType.Connect);

                if (response.StatusCode == HttpStatusCode.OK)
                {

                    MexcTrades baseTrades =
                        JsonConvert.DeserializeAnonymousType(content, new MexcTrades());

                    List<MyTrade> trades = new List<MyTrade>();

                    for (int i = 0; i < baseTrades.Count; i++)
                    {
                        MyTrade trade = ConvertRestTradeToOsEngineTrade(baseTrades[i]);
                        trades.Add(trade);
                    }

                    return trades;

                }
                else
                {
                    SendLogMessage("Order trade request error. Status: "
                        + response.StatusCode + "  " + orderId +
                        ", " + content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order trade request error " + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private MyTrade ConvertRestTradeToOsEngineTrade(MexcTrade baseTrade)
        {
            MyTrade trade = new MyTrade();
            trade.NumberOrderParent = baseTrade.orderId;
            trade.NumberTrade = baseTrade.id;
            trade.SecurityNameCode = baseTrade.symbol;
            trade.Time = ConvertToDateTimeFromUnixFromMilliseconds(baseTrade.time.ToString());
            if (baseTrade.isBuyer)
            {
                trade.Side = Side.Buy;
            }
            else
            {
                trade.Side = Side.Sell;
            }
            trade.Price = baseTrade.price.ToDecimal();
            trade.Volume = baseTrade.qty.ToDecimal();

            return trade;
        }

        #endregion

        #region 12 Helpers

        private DateTime ConvertToDateTimeFromUnixFromMilliseconds(string miliseconds)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime result = origin.AddMilliseconds(miliseconds.ToDouble());

            return result.ToLocalTime();
        }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}