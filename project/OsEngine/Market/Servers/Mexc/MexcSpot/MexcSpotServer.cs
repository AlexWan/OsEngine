/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Mexc.Json;
using OsEngine.Market.Servers.Mexc.MexcSpot.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;



namespace OsEngine.Market.Servers.Mexc
{
    public class MexcSpotServer : AServer
    {
        public MexcSpotServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            MexcSpotServerRealization realization = new MexcSpotServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
        }
    }

    public class MexcSpotServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public MexcSpotServerRealization()
        {
            Thread worker = new Thread(ConnectionCheckThread);
            worker.Name = "CheckAliveMexc";
            worker.Start();

            Thread worker2 = new Thread(MessageReaderPublic);
            worker2.Name = "MessageReaderPublicMexcSpot";
            worker2.Start();

            Thread worker3 = new Thread(MessageReaderPrivate);
            worker3.Name = "MessageReaderPrivateMexcSpot";
            worker3.Start();
        }

        public void Connect(WebProxy proxy)
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(_publicKey)
                || string.IsNullOrEmpty(_secretKey))
            {
                SendLogMessage("Connection terminated. You must specify the public and private keys. You can get it on the MexcSpot website",
                    LogMessageType.Error);
                return;
            }

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/api/v3/time", Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    _lastTimeProlongListenKey = DateTime.Now;
                    _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                    _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

                    CreateWebSocketPrivateConnection();
                    //CheckActivationSockets();
                }
                else
                {
                    SendLogMessage("Authorization Error. Probably an invalid keys are specified.", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromPrivateWebSockets();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            try
            {
                UnsubscribeFromPublicWebSockets();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            try
            {
                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _securities.Clear();
            _myPortfolios.Clear();
            _subscribedSecurities.Clear();

            Disconnect();
        }

        public void Disconnect()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.MexcSpot;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        private string _publicKey;

        private string _secretKey;

        public List<IServerParameter> ServerParameters { get; set; }

        private string _baseUrl = "https://api.mexc.com";

        #endregion

        #region 3 Securities

        private List<Security> _securities = new List<Security>();

        List<string> _activeSecurities = new List<string>();

        private RateGate _rateGateSecurities = new RateGate(50, TimeSpan.FromMilliseconds(10000));

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
            _rateGateSecurities.WaitToProceed();

            try
            {
                RestRequest requestRest = new RestRequest("/api/v3/exchangeInfo", Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcSecurityList securities = JsonConvert.DeserializeAnonymousType(response.Content, new MexcSecurityList());

                    if (securities == null
                        || securities.symbols == null
                        || securities.symbols.Count == 0)
                    {
                        return;
                    }

                    for (int i = 0; i < securities.symbols.Count; i++)
                    {
                        MexcSecurity sec = securities.symbols[i];

                        if (sec.isSpotTradingAllowed == "false")
                        {
                            continue;
                        }

                        if (sec.status != "1")
                        {
                            continue;
                        }

                        Security security = new Security();
                        security.Name = sec.symbol;
                        security.NameFull = sec.symbol;
                        security.NameClass = sec.quoteAsset;
                        security.NameId = sec.symbol + sec.quoteAsset;
                        security.SecurityType = SecurityType.CurrencyPair;
                        security.Exchange = ServerType.MexcSpot.ToString();
                        security.State = SecurityStateType.Activ;
                        security.Lot = 1;
                        security.PriceStep = GetStep(Convert.ToInt32(sec.quoteAssetPrecision));
                        security.PriceStepCost = security.PriceStep;
                        security.Decimals = Convert.ToInt32(sec.quoteAssetPrecision);
                        security.DecimalsVolume = Convert.ToInt32(sec.baseAssetPrecision);
                        security.MinTradeAmount = sec.quoteAmountPrecision.ToDecimal();
                        security.MinTradeAmountType = MinTradeAmountType.C_Currency;
                        security.VolumeStep = GetStep(Convert.ToInt32(sec.baseAssetPrecision));

                        _securities.Add(security);
                    }
                }
                else
                {
                    SendLogMessage($"Securities error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Securities request error: " + exception.ToString(), LogMessageType.Error);
            }
        }

        private decimal GetStep(int ScalePrice)
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

        private string _portfolioName = "MexcSpot";

        private RateGate _rateGatePortfolio = new RateGate(50, TimeSpan.FromMilliseconds(10000));

        public void GetPortfolios()
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                IRestResponse response = CreatePrivateQuery(Method.GET, "/api/v3/account");

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcPortfolioRest portfolio = JsonConvert.DeserializeAnonymousType(response.Content, new MexcPortfolioRest());

                    ConvertToPortfolio(portfolio);
                }
                else
                {
                    if (response.Content.Contains("Api key info invalid")
                        || response.Content.Contains("Signature for this request is not valid"))
                    {
                        SendLogMessage("Portfolio request error. Status: " + response.StatusCode + "  " + this._portfolioName + ", " + response.Content, LogMessageType.Error);
                        Disconnect();
                    }
                    else
                    {
                        SendLogMessage("Portfolio request error. Status: " + response.StatusCode + "  " + this._portfolioName + ", " + response.Content, LogMessageType.Error);
                    }

                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio request error " + exception.ToString(), LogMessageType.Error);
            }
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

        private RateGate _rateGateGetData = new RateGate(500, TimeSpan.FromMilliseconds(10000));

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.UtcNow;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);

            while (candles.Count > candleCount)
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
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!_allowedTf.ContainsKey(tfTotalMinutes))
            {
                return null;
            }

            string tf = _allowedTf[tfTotalMinutes];

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            int limitCandles = 500;

            TimeSpan span = endTime - startTime;

            if (limitCandles > span.TotalMinutes / tfTotalMinutes)
            {
                limitCandles = (int)Math.Round(span.TotalMinutes / tfTotalMinutes, MidpointRounding.AwayFromZero);
            }

            List<Candle> allCandles = new List<Candle>();

            DateTime startTimeData = startTime;
            DateTime endTimeData = startTimeData.AddMinutes(tfTotalMinutes * limitCandles);

            do
            {
                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTimeData);
                long to = TimeManager.GetTimeStampMilliSecondsToDateTime(endTimeData);

                MexcCandlesHistory history = GetHistoryCandle(security, tf, from, to);
                List<Candle> candles = ConvertToOsEngineCandles(history);

                if (candles == null || candles.Count == 0)
                {
                    break;
                }

                Candle last = candles[candles.Count - 1];

                if (allCandles.Count > 0)
                {
                    if (allCandles[allCandles.Count - 1].TimeStart == candles[0].TimeStart)
                    {
                        candles.RemoveAt(0);
                    }
                }

                if (last.TimeStart >= endTime)

                {
                    for (int i = 0; i < candles.Count; i++)
                    {
                        if (candles[i].TimeStart <= endTime)
                        {
                            allCandles.Add(candles[i]);
                        }
                    }
                    break;
                }

                allCandles.AddRange(candles);

                startTimeData = endTimeData;
                endTimeData = startTimeData.AddMinutes(tfTotalMinutes * limitCandles);

                if (startTimeData >= endTime)
                {
                    break;
                }

                if (endTimeData > endTime)
                {
                    endTimeData = endTime;
                }

                span = endTimeData - startTimeData;

                if (limitCandles > span.TotalMinutes / tfTotalMinutes)
                {
                    limitCandles = (int)Math.Round(span.TotalMinutes / tfTotalMinutes, MidpointRounding.AwayFromZero);
                }

            } while (true);

            return allCandles;
        }

        private MexcCandlesHistory GetHistoryCandle(Security security, string timeFrame,
            long startTime, long endTime)
        {
            _rateGateGetData.WaitToProceed();

            string endPoint = "/api/v3/klines?symbol=" + security.Name;
            endPoint += "&interval=" + timeFrame;
            endPoint += "&startTime=" + startTime;
            endPoint += "&endTime=" + endTime;
            endPoint += "&limit=500";

            try
            {
                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcCandlesHistory parsed = JsonConvert.DeserializeAnonymousType(response.Content, new MexcCandlesHistory());

                    return parsed;
                }
                else
                {
                    SendLogMessage("Candles request error to url='" + endPoint + "'. Status: " +
                        response.StatusCode + ". Message: " + response.Content, LogMessageType.Error);
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

            for (int i = 0; i < candles.Count; i++)
            {
                List<object> curCandle = candles[i];

                Candle newCandle = new Candle();
                newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStamp((long)curCandle[0]);

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
                {
                    newCandle.Open = newCandle.Low;
                }

                if (newCandle.Open > newCandle.High)
                {
                    newCandle.Open = newCandle.High;
                }

                if (newCandle.Close < newCandle.Low)
                {
                    newCandle.Close = newCandle.Low;
                }

                if (newCandle.Close > newCandle.High)
                {
                    newCandle.Close = newCandle.High;
                }

                result.Add(newCandle);
            }

            return result;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime
                || startTime >= DateTime.UtcNow
                || actualTime > endTime
                || actualTime > DateTime.UtcNow)
            {
                return false;
            }
            return true;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;

        }

        #endregion

        #region 6 WebSocket creation

        private readonly string _ws = "ws://wbs-api.mexc.com/ws";

        private readonly string _wsPrivate = "ws://wbs-api.mexc.com/ws";

        private List<WebSocket> _webSocketPublicSpot = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

        private string _socketLocker = "webSocketLockerMexc";

        private string _listenKey = "";

        private WebSocket CreateWebSocketPublicConnection()
        {
            try
            {
                lock (_socketLocker)
                {
                    if (_FIFOListWebSocketPublicMessage == null)
                    {
                        _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                    }

                    WebSocket _webSocketPublic = new WebSocket(_ws);

                    _webSocketPublic.EmitOnPing = true;
                    _webSocketPublic.OnOpen += _webSocketPublic_OnOpen;
                    _webSocketPublic.OnClose += _webSocketPublic_OnClose;
                    _webSocketPublic.OnMessage += _webSocketPublic_OnMessage;
                    _webSocketPublic.OnError += _webSocketPublic_OnError;
                    _webSocketPublic.ConnectAsync();

                    return _webSocketPublic;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private void CreateWebSocketPrivateConnection()
        {
            try
            {
                lock (_socketLocker)
                {
                    _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

                    if (_webSocketPrivate != null)
                    {
                        return;
                    }

                    //get Listen Key
                    _listenKey = GetListenKey();
                    string uri = _wsPrivate + "?listenKey=" + _listenKey;

                    _webSocketPrivate = new WebSocket(uri);

                    _webSocketPrivate.EmitOnPing = true;
                    _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                    _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                    _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError += _webSocketPrivate_OnError;

                    _webSocketPrivate.ConnectAsync();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebSocketConnection()
        {
            lock (_socketLocker)
            {
                if (_webSocketPrivate != null)
                {
                    try
                    {
                        _webSocketPrivate.OnOpen -= _webSocketPrivate_OnOpen;
                        _webSocketPrivate.OnClose -= _webSocketPrivate_OnClose;
                        _webSocketPrivate.OnMessage -= _webSocketPrivate_OnMessage;
                        _webSocketPrivate.OnError -= _webSocketPrivate_OnError;
                        _webSocketPrivate.CloseAsync();
                    }
                    catch
                    {
                        // ignore
                    }

                    _webSocketPrivate = null;
                }
            }
        }

        private string GetListenKey()
        {
            try
            {
                string endPoint = "/api/v3/userDataStream";
                IRestResponse response = CreatePrivateQuery(Method.POST, endPoint);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcListenKey parsed = JsonConvert.DeserializeAnonymousType(response.Content, new MexcListenKey());

                    return parsed.listenKey;
                }
                else
                {
                    SendLogMessage("GetListenKey Fail. Status: " + response.StatusCode + ", " + response.Content, LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("GetListenKey error " + exception.ToString(), LogMessageType.Error);
                Disconnect();
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

                IRestResponse response = CreatePrivateQuery(Method.PUT, endPoint, query);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcListenKey parsed = JsonConvert.DeserializeAnonymousType(response.Content, new MexcListenKey());
                    //SendLogMessage("ProlongListenKey Success. Key: " + parsed.listenKey, LogMessageType.Connect);
                }
                else
                {
                    SendLogMessage("ProlongListenKey Fail. Status: "
                        + response.StatusCode + ", " + response.Content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("ProlongListenKey send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private string _lockerCheckActivateionSockets = "lockerCheckActivateionSocketsMexcSpot";

        private void CheckActivationSockets()
        {
            lock (_lockerCheckActivateionSockets)
            {
                if (_subscribedSecurities.Count > 0)
                {
                    if (_webSocketPublicSpot == null)
                    {
                        //Disconnect();
                        return;
                    }

                    if (_webSocketPublicSpot.Count == 0)
                    {
                        // Disconnect();
                        return;
                    }

                    WebSocket _webSocketPublic = _webSocketPublicSpot[0];

                    if (_webSocketPublic == null
                        || _webSocketPublic?.ReadyState != WebSocketState.Open)
                    {
                        Disconnect();
                        return;
                    }
                }

                if (_webSocketPrivate == null
                    || _webSocketPrivate.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Connect;

                    if (ConnectEvent != null)
                    {
                        ConnectEvent();
                    }
                }
            }
        }

        #endregion

        #region 7 WebSocket events

        private void _webSocketPublic_OnError(object sender, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPublic_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e == null)
                {
                    return;
                }

                if (_FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                if (e.IsBinary)
                {
                    if (e.RawData == null)
                    {
                        return;
                    }

                    PushDataV3ApiWrapper response = PushDataV3ApiWrapper.Parser.ParseFrom(e.RawData);

                    if (response == null)
                    {
                        return;
                    }

                    _FIFOListWebSocketPublicMessage.Enqueue(response.ToString());
                }
                else if (e.IsText)
                {
                    if (e.Data.Contains("PONG"))
                    { // pong message
                        return;
                    }

                    if (e.Data.IndexOf("\"msg\"") >= 0)
                    {
                        //SendLogMessage("WebSocketData, message:" + e.Message, LogMessageType.Connect);
                        return;
                    }

                    _FIFOListWebSocketPublicMessage.Enqueue(e.Data);
                }
            }
            catch (Exception error)
            {
                SendLogMessage("Trade socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPublic_OnClose(object sender, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPublic_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("MexcSpot WebSocket Public connection open", LogMessageType.System);
                    CheckActivationSockets();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnError(object sender, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e == null)
                {
                    return;
                }

                if (_FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                if (e.IsBinary)
                {
                    if (e.RawData == null)
                    {
                        return;
                    }

                    PushDataV3ApiWrapper response = PushDataV3ApiWrapper.Parser.ParseFrom(e.RawData);

                    if (response == null)
                    {
                        return;
                    }

                    _FIFOListWebSocketPrivateMessage.Enqueue(response.ToString());
                }
                else if (e.IsText)
                {
                    if (e.Data.Contains("PONG"))
                    { // pong message
                        return;
                    }

                    if (e.Data.IndexOf("\"msg\"") >= 0)
                    {
                        return;
                    }

                    _FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
                }
            }
            catch (Exception error)
            {
                SendLogMessage("Private socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnClose(object sender, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    string message = this.GetType().Name + OsLocalization.Market.Message101 + "\n";
                    message += OsLocalization.Market.Message102;

                    SendLogMessage(message, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
        {
            try
            {
                SendLogMessage("MexcSpot WebSocket Private connection open", LogMessageType.System);
                CheckActivationSockets();

                _webSocketPrivate.SendAsync($"{{ \"method\": \"SUBSCRIPTION\", \"params\": [\"spot@private.orders.v3.api.pb\"] }}");
                _webSocketPrivate.SendAsync($"{{ \"method\": \"SUBSCRIPTION\", \"params\": [\"spot@private.account.v3.api.pb\"] }}");
                _webSocketPrivate.SendAsync($"{{ \"method\": \"SUBSCRIPTION\", \"params\": [\"spot@private.deals.v3.api.pb\"] }}");
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        private DateTime _lastTimeProlongListenKey = DateTime.Now;

        private void ConnectionCheckThread()
        {
            while (true)
            {
                Thread.Sleep(12000);

                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    if (_webSocketPublicSpot != null)
                    {
                        for (int i = 0; i < _webSocketPublicSpot.Count; i++)
                        {
                            WebSocket _webSocketPublic = _webSocketPublicSpot[i];
                            if (_webSocketPublic != null && _webSocketPublic?.ReadyState == WebSocketState.Open
                                || _webSocketPublic.ReadyState == WebSocketState.Connecting)
                            {
                                _webSocketPublic.SendAsync("{\"method\":\"PING\"}");
                            }
                            else
                            {
                                SendLogMessage("PingSocket: WebSocketPublic is not active", LogMessageType.Connect);
                                Disconnect();
                            }
                        }
                    }

                    if (_webSocketPrivate != null &&
                        (_webSocketPrivate.ReadyState == WebSocketState.Open ||
                        _webSocketPrivate.ReadyState == WebSocketState.Connecting)
                        )
                    {
                        _webSocketPrivate.SendAsync("{\"method\":\"PING\"}");

                        if (_lastTimeProlongListenKey.AddMinutes(30) < DateTime.Now)
                        {
                            ProlongListenKey();
                            _lastTimeProlongListenKey = DateTime.Now;
                        }
                    }
                    else
                    {
                        SendLogMessage("PingSocket: WebSocketPrivate is not active", LogMessageType.Connect);
                        Disconnect();
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

        private RateGate _rateGateSubscribe = new RateGate(30, TimeSpan.FromMilliseconds(1000));

        private List<string> _subscribedSecurities = new List<string>();

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();

                if (_activeSecurities.Exists(s => s == security.Name) == false)
                {
                    _activeSecurities.Add(security.Name);
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Equals(security.Name))
                    {
                        return;
                    }
                }

                _subscribedSecurities.Add(security.Name);

                if (_subscribedSecurities.Count > 0
                    && _webSocketPublicSpot.Count == 0)
                {
                    WebSocket newSocket = CreateWebSocketPublicConnection();

                    DateTime timeEnd = DateTime.Now.AddSeconds(10);
                    while (newSocket.ReadyState != WebSocketState.Open)
                    {
                        Thread.Sleep(1000);

                        if (timeEnd < DateTime.Now)
                        {
                            break;
                        }
                    }

                    if (newSocket.ReadyState == WebSocketState.Open)
                    {
                        _webSocketPublicSpot.Add(newSocket);
                    }
                }

                if (_webSocketPublicSpot.Count == 0)
                {
                    return;
                }

                WebSocket _webSocketPublic = _webSocketPublicSpot[_webSocketPublicSpot.Count - 1];

                if (_webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 10 == 0)
                {
                    WebSocket newSocket = CreateWebSocketPublicConnection();

                    DateTime timeEnd = DateTime.Now.AddSeconds(10);
                    while (newSocket.ReadyState != WebSocketState.Open)
                    {
                        Thread.Sleep(1000);

                        if (timeEnd < DateTime.Now)
                        {
                            break;
                        }
                    }

                    if (newSocket.ReadyState == WebSocketState.Open)
                    {
                        _webSocketPublicSpot.Add(newSocket);
                        _webSocketPublic = newSocket;
                    }
                }

                if (_webSocketPublic != null)
                {
                    _webSocketPublic.SendAsync($"{{ \"method\": \"SUBSCRIPTION\", \"params\": [\"spot@public.aggre.deals.v3.api.pb@100ms@{security.Name}\"] }}");
                    _webSocketPublic.SendAsync($"{{ \"method\": \"SUBSCRIPTION\", \"params\": [\"spot@public.limit.depth.v3.api.pb@{security.Name}@20\"] }}");
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UnsubscribeFromPrivateWebSockets()
        {
            if (_webSocketPrivate == null)
            {
                return;
            }

            _webSocketPrivate.SendAsync($"{{ \"method\": \"UNSUBSCRIPTION\", \"params\": [\"spot@private.orders.v3.api.pb\"] }}");
            _webSocketPrivate.SendAsync($"{{ \"method\": \"UNSUBSCRIPTION\", \"params\": [\"spot@private.account.v3.api.pb\"] }}");
            _webSocketPrivate.SendAsync($"{{ \"method\": \"UNSUBSCRIPTION\", \"params\": [\"spot@private.deals.v3.api.pb\"] }}");
        }

        private void UnsubscribeFromPublicWebSockets()
        {
            if (_webSocketPublicSpot == null)
            {
                return;
            }

            try
            {
                lock (_socketLocker)
                {
                    for (int i = 0; i < _webSocketPublicSpot.Count; i++)
                    {
                        WebSocket _webSocketPublic = _webSocketPublicSpot[i];

                        _webSocketPublic.OnOpen -= _webSocketPublic_OnOpen;
                        _webSocketPublic.OnClose -= _webSocketPublic_OnClose;
                        _webSocketPublic.OnMessage -= _webSocketPublic_OnMessage;
                        _webSocketPublic.OnError -= _webSocketPublic_OnError;

                        try
                        {
                            if (_webSocketPublic != null && _webSocketPublic?.ReadyState == WebSocketState.Open)
                            {
                                for (int j = 0; j < _subscribedSecurities.Count; j++)
                                {
                                    string securityName = _subscribedSecurities[j];

                                    _webSocketPublic.SendAsync($"{{ \"method\": \"UNSUBSCRIPTION\", \"params\": [\"spot@public.aggre.deals.v3.api.pb@100ms@{securityName}\"] }}");
                                    _webSocketPublic.SendAsync($"{{ \"method\": \"UNSUBSCRIPTION\", \"params\": [\"spot@public.limit.depth.v3.api.pb@{securityName}@20\"] }}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                        }

                        if (_webSocketPublic.ReadyState == WebSocketState.Open)
                        {
                            _webSocketPublic.CloseAsync();
                        }
                        _webSocketPublic = null;
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            _webSocketPublicSpot.Clear();
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private void MessageReaderPublic()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_FIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    _FIFOListWebSocketPublicMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains(".depth."))
                    {
                        UpdateMarketDepth(message);
                    }
                    else if (message.Contains(".deals."))
                    {
                        UpdateTrade(message);
                    }
                    else
                    {
                        SendLogMessage("Unknown message: " + message, LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(2000);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            try
            {
                DealsWebSocket deals = JsonConvert.DeserializeAnonymousType(message, new DealsWebSocket());

                if (deals == null
                    || deals.publicAggreDeals.deals == null)
                {
                    return;
                }

                for (int i = 0; i < deals.publicAggreDeals.deals.Count; i++)
                {
                    MexcDeal deal = deals.publicAggreDeals.deals[i];

                    Trade trade = new Trade();
                    trade.SecurityNameCode = deals.symbol;
                    trade.Price = deal.price.ToDecimal();
                    trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(deal.time));// ConvertToDateTimeFromUnixFromMilliseconds(deal.time);
                    trade.Id = deal.time + deal.tradeType + trade.SecurityNameCode;

                    if (deal.tradeType == "1")
                    {
                        trade.Side = Side.Buy;
                    }
                    else
                    {
                        trade.Side = Side.Sell;
                    }

                    trade.Volume = deal.quantity.ToDecimal();

                    NewTradesEvent?.Invoke(trade);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMarketDepth(string message)
        {
            try
            {
                DepthsWebSocket baseDepth = JsonConvert.DeserializeAnonymousType(message, new DepthsWebSocket());

                if (baseDepth == null
                    || baseDepth.publicLimitDepths == null)
                {
                    return;
                }

                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = baseDepth.symbol;
                depth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(baseDepth.sendTime));// ConvertToDateTimeFromUnixFromMilliseconds(baseDepth.sendTime);

                for (int i = 0; baseDepth.publicLimitDepths.bids != null && i < baseDepth.publicLimitDepths.bids.Count; i++)
                {
                    MarketDepthLevel newBid = new MarketDepthLevel();
                    newBid.Price = baseDepth.publicLimitDepths.bids[i].price.ToDouble();
                    newBid.Bid = baseDepth.publicLimitDepths.bids[i].quantity.ToDouble();
                    depth.Bids.Add(newBid);
                }

                for (int i = 0; baseDepth.publicLimitDepths.asks != null && i < baseDepth.publicLimitDepths.asks.Count; i++)
                {
                    MarketDepthLevel newAsk = new MarketDepthLevel();
                    newAsk.Price = baseDepth.publicLimitDepths.asks[i].price.ToDouble();
                    newAsk.Ask = baseDepth.publicLimitDepths.asks[i].quantity.ToDouble();
                    depth.Asks.Add(newAsk);
                }

                if (_lastMdTime != DateTime.MinValue &&
                    _lastMdTime >= depth.Time)
                {
                    depth.Time = _lastMdTime.AddTicks(1);
                }

                _lastMdTime = depth.Time;

                MarketDepthEvent?.Invoke(depth);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Value was either too large or too small for a Decimal."))
                {

                }
                else
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }   
            }
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        private void MessageReaderPrivate()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_FIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    _FIFOListWebSocketPrivateMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Contains("\"msg\""))
                    {
                        continue;
                    }

                    if (message.Contains(".account."))
                    {
                        UpdateMyPortfolio(message);
                    }
                    else if (message.Contains(".orders."))
                    {
                        UpdateMyOrder(message);
                    }
                    else if (message.Contains(".deals."))
                    {
                        UpdateMyTrade(message);
                    }
                    else
                    {
                        SendLogMessage("Unknown message: " + message, LogMessageType.Error);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(2000);
                }
            }
        }

        private void UpdateMyTrade(string message)
        {
            try
            {
                MyTradeWebSocket baseMyTrade = JsonConvert.DeserializeAnonymousType(message.ToString(), new MyTradeWebSocket());

                if (baseMyTrade == null
                    || baseMyTrade.privateDeals == null)
                {
                    return;
                }
                MexcSocketMyTrade item = baseMyTrade.privateDeals;

                MyTrade myTrade = new MyTrade();
                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.time));
                myTrade.NumberOrderParent = item.orderId;
                myTrade.NumberTrade = item.tradeId;
                myTrade.Price = item.price.ToDecimal();
                myTrade.SecurityNameCode = baseMyTrade.symbol;

                myTrade.Side = Side.Buy;

                if (item.tradeType == "2")
                {
                    myTrade.Side = Side.Sell;
                }

                myTrade.Volume = item.quantity.ToDecimal();

                MyTradeEvent(myTrade);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMyOrder(string message)
        {
            try
            {
                OrderWebSocket baseOrder = JsonConvert.DeserializeAnonymousType(message.ToString(), new OrderWebSocket());

                if (baseOrder == null
                    || baseOrder.privateOrders == null)
                {
                    return;
                }

                MexcSocketOrder item = baseOrder.privateOrders;

                Order order = new Order();

                order.NumberMarket = item.id;

                try
                {
                    order.NumberUser = Convert.ToInt32(item.clientId);
                }
                catch
                {

                }

                order.SecurityNameCode = baseOrder.symbol;
                order.Side = Side.Buy;

                if (item.tradeType == "2")
                {
                    order.Side = Side.Sell;
                }

                order.PortfolioNumber = this._portfolioName;
                order.Volume = item.quantity.ToDecimal();
                order.VolumeExecute = item.cumulativeQuantity.ToDecimal() - item.lastDealQuantity.ToDecimal();
                order.Price = item.price.ToDecimal();

                //LIMIT_ORDER(1),POST_ONLY(2),IMMEDIATE_OR_CANCEL(3),
                //FILL_OR_KILL(4),MARKET_ORDER(5); STOP_LIMIT(100)
                if (item.orderType == "1")
                {
                    order.TypeOrder = OrderPriceType.Limit;
                }
                else /*if (item.orderType == "5")*/
                {
                    order.TypeOrder = OrderPriceType.Market;
                }

                order.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createTime));
                order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createTime));
                order.ServerType = ServerType.MexcSpot;

                //status 1:New order 2:Filled 3:Partially filled 4:Order canceled
                //5:Order filled partially, and then the rest of the order is canceled

                if (item.status == "1")
                {
                    order.State = OrderStateType.Active;
                }
                else if (item.status == "2")
                {
                    order.State = OrderStateType.Done;
                }
                else if (item.status == "3")
                {
                    order.State = OrderStateType.Partial;
                }
                else if (item.status == "4" || item.status == "5")
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

                //if (order.State == OrderStateType.Done || order.State == OrderStateType.Partial)
                //{
                //    UpdateTrades(order);
                //}
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMyPortfolio(string message)
        {
            try
            {
                AccountWebSocket balance = JsonConvert.DeserializeAnonymousType(message.ToString(), new AccountWebSocket());

                if (balance == null
                    || balance.privateAccount == null)
                {
                    return;
                }

                Portfolio portf = null;

                if (_myPortfolios != null && _myPortfolios.Count > 0)
                {
                    portf = _myPortfolios[0];
                }

                if (portf == null)
                {
                    return;
                }

                PositionOnBoard pos = new PositionOnBoard();
                pos.ValueCurrent = balance.privateAccount.balanceAmount.ToDecimal();
                pos.ValueBlocked = balance.privateAccount.frozenAmount.ToDecimal();
                pos.PortfolioName = this._portfolioName;
                pos.SecurityNameCode = balance.privateAccount.vcoinName;
                portf.SetNewPosition(pos);

                PortfolioEvent?.Invoke(_myPortfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrderIP = new RateGate(500, TimeSpan.FromMilliseconds(10000));

        private RateGate _rateGateSendOrderUID = new RateGate(500, TimeSpan.FromMilliseconds(10000));

        private RateGate _rateGateCancelOrder = new RateGate(500, TimeSpan.FromMilliseconds(10000));

        public void SendOrder(Order order)
        {
            if (_activeSecurities.Exists(s => s == order.SecurityNameCode) == false)
            {
                _activeSecurities.Add(order.SecurityNameCode);
            }

            _rateGateSendOrderIP.WaitToProceed();
            _rateGateSendOrderUID.WaitToProceed();

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

                IRestResponse response = CreatePrivateQuery(Method.POST, endPoint, query);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcNewOrderResponse parsed =
                        JsonConvert.DeserializeAnonymousType(response.Content, new MexcNewOrderResponse());

                    if (parsed != null && !string.IsNullOrEmpty(parsed.orderId))
                    {
                        //Everything is OK
                        SendLogMessage($"Order created: {response.Content}", LogMessageType.Connect);
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Order created, but answer is wrong: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage("Order Fail. Status: "
                        + response.StatusCode + "  " + order.SecurityNameCode + ", " + response.Content, LogMessageType.Error);

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

        public bool CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            try
            {
                string endPoint = "/api/v3/order";

                Dictionary<string, string> query = new Dictionary<string, string>();
                query.Add("symbol", order.SecurityNameCode);
                query.Add("origClientOrderId", order.NumberUser.ToString());

                IRestResponse response = CreatePrivateQuery(Method.DELETE, endPoint, query);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcOrderResponse parsed =
                        JsonConvert.DeserializeAnonymousType(response.Content, new MexcOrderResponse());

                    if (parsed != null)
                    {
                        return true;
                    }
                    else
                    {
                        GetOrderStatus(order);
                        SendLogMessage($"Cancel order, answer is wrong: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    if (response.Content.Contains("-2011"))
                    {
                        GetOrderStatus(order);
                    }
                    else
                    {
                        GetOrderStatus(order);
                        SendLogMessage("Cancel order failed. Status: "
                            + response.StatusCode + "  " + order.SecurityNameCode + ", " + response.Content, LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Cancel order error." + exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange();

            if (orders == null)
                return;

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Active)
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
            string[] symbols = _activeSecurities.ToArray();

            List<Order> ret = null;

            for (int i = 0; i < symbols.Length; i++)
            {
                List<Order> orders = GetAllOrdersFromExchange(symbols[i]);

                if (orders == null)
                {
                    continue;
                }

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

        private RateGate _rateGateOpenOrders = new RateGate(166, TimeSpan.FromMilliseconds(10000));

        private List<Order> GetAllOrdersFromExchange(string symbol)
        {
            _rateGateOpenOrders.WaitToProceed();

            try
            {
                string endPoint = "/api/v3/openOrders";

                Dictionary<string, string> query = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(symbol))
                {
                    query.Add("symbol", symbol);
                }

                IRestResponse response = CreatePrivateQuery(Method.GET, endPoint, query);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    try
                    {
                        MexcOrderListResponse parsed =
                            JsonConvert.DeserializeAnonymousType(response.Content, new MexcOrderListResponse());

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
                        SendLogMessage("Get all orders. Failed to parse: " + response.Content + "\n exception: " + exception.ToString(), LogMessageType.Error);
                    }
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    SendLogMessage("Get all orders request error. ", LogMessageType.Error);

                    if (response.Content != null)
                    {
                        SendLogMessage("Fail reasons: " + response.Content, LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private RateGate _rateGateOrdersStatus = new RateGate(250, TimeSpan.FromMilliseconds(10000));

        private Order GetOrderFromExchange(string userOrderId, string symbol)
        {
            if (string.IsNullOrEmpty(userOrderId))
            {
                SendLogMessage("Order ID is empty", LogMessageType.Connect);
                return null;
            }

            _rateGateOrdersStatus.WaitToProceed();

            try
            {
                string endPoint = "/api/v3/order";

                Dictionary<string, string> query = new Dictionary<string, string>
                {
                    { "symbol", symbol },
                    { "origClientOrderId", userOrderId }
                };

                IRestResponse response = CreatePrivateQuery(Method.GET, endPoint, query);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcOrderResponse parsed =
                        JsonConvert.DeserializeAnonymousType(response.Content, new MexcOrderResponse());

                    if (parsed != null)
                    {
                        //Everything is OK
                        MexcOrderResponse baseOrder = JsonConvert.DeserializeAnonymousType(response.Content, new MexcOrderResponse());

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
                    SendLogMessage("Get order request " + userOrderId + " error: " + response.Content,
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

        public OrderStateType GetOrderStatus(Order order)
        {
            if (_activeSecurities.Exists(s => s == order.SecurityNameCode) == false)
            {
                _activeSecurities.Add(order.SecurityNameCode);
            }

            Order myOrder = GetOrderFromExchange(order.NumberUser.ToString(), order.SecurityNameCode);

            if (myOrder == null)
            {
                return OrderStateType.None;
            }

            MyOrderEvent?.Invoke(myOrder);

            if (myOrder.State == OrderStateType.Done || myOrder.State == OrderStateType.Partial)
            {
                UpdateTrades(order);
            }

            return myOrder.State;
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

            order.NumberMarket = baseOrder.orderId;

            if (baseOrder.updateTime != null)
            {
                order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(baseOrder.updateTime));
            }
            else
            {
                order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(baseOrder.time));
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
            else if (baseOrder.status == "CANCELED" || baseOrder.status == "PARTIALLY_CANCELED")
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

        private RateGate _rateGateMyTrades = new RateGate(50, TimeSpan.FromMilliseconds(10000));

        private void UpdateTrades(Order order)
        {
            List<MyTrade> trades = GetTradesForOrder(order.NumberMarket, order.SecurityNameCode);

            if (trades == null)
            {
                return;
            }

            for (int i = 0; i < trades.Count; i++)
            {
                MyTradeEvent?.Invoke(trades[i]);
            }
        }

        private List<MyTrade> GetTradesForOrder(string orderId, string symbol)
        {
            _rateGateMyTrades.WaitToProceed();

            try
            {
                string endPoint = $"/api/v3/myTrades";

                Dictionary<string, string> query = new Dictionary<string, string>
                {
                    { "symbol", symbol },
                    { "orderId", orderId }
                };

                IRestResponse response = CreatePrivateQuery(Method.GET, endPoint, query);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcTrades baseTrades =
                        JsonConvert.DeserializeAnonymousType(response.Content, new MexcTrades());

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
                        ", " + response.Content, LogMessageType.Error);
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
            trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(baseTrade.time));

            if (baseTrade.isBuyer == "true")
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

        //public RateGate _rateGateRestIP = new RateGate(500, TimeSpan.FromMilliseconds(10000));
        //public RateGate _rateGateRestUID = new RateGate(500, TimeSpan.FromMilliseconds(10000));

        private IRestResponse CreatePrivateQuery(Method method, string endPoint, Dictionary<string, string> queryParams = null)
        {
            try
            {
                string query = GetSecuredQueryString(queryParams);
                string url = /*_baseUrl +*/ endPoint;

                if (!string.IsNullOrEmpty(query))
                {
                    url += "?" + query;
                }

                RestRequest request = new RestRequest(url, method);
                request.AddHeader("X-MEXC-APIKEY", _publicKey);
                IRestResponse response = new RestClient(_baseUrl).Execute(request);

                return response;
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
            return null;
        }

        private string GetQueryString(Dictionary<string, string> queryParams = null)
        {
            string query = "";

            if (queryParams != null)
            {
                foreach (var onePar in queryParams)
                {
                    if (query.Length > 0)
                        query += "&";
                    query += onePar.Key + "=" + onePar.Value;
                }
            }

            return query;
        }

        private string GetSecuredQueryString(Dictionary<string, string> queryParams = null)
        {
            string query = GetQueryString(queryParams);

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            if (query.Length > 0)
                query += "&";

            query += "recvWindow=10000&timestamp=" + timestamp;

            string signature = GenerateSignature(query, _secretKey);

            query += "&signature=" + signature;

            return query;
        }

        public static string GenerateSignature(string source, string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            using (HMACSHA256 hmacsha256 = new HMACSHA256(keyBytes))
            {
                byte[] sourceBytes = Encoding.UTF8.GetBytes(source);

                byte[] hash = hmacsha256.ComputeHash(sourceBytes);

                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        public void SetLeverage(Security security, decimal leverage) { }

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