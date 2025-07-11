/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Mexc.Json;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using OsEngine.Entity.WebSocketOsEngine;


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

            Thread worker2 = new Thread(DataMessageReader);
            worker2.Name = "DataMessageReaderMexc";
            worker2.Start();

            Thread worker3 = new Thread(PortfolioMessageReader);
            worker3.Name = "PortfolioMessageReaderMexc";
            worker3.Start();
        }

        public void Connect(WebProxy proxy)
        {
            SendLogMessage("Start MexcSpot Connection", LogMessageType.System);

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
                    _timeToSendPingPublic = DateTime.Now;
                    _timeToSendPingPrivate = DateTime.Now;
                    _lastTimeProlongListenKey = DateTime.Now;
                    _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                    _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

                    CreateWebSocketPrivateConnection();
                    CheckActivationSockets();
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
                HttpResponseMessage response = Query(HttpMethod.Get, endPoint);

                string content = response.Content.ReadAsStringAsync().Result;
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
                    security.Lot = 1;
                    security.PriceStep = GetStep(Convert.ToInt32(sec.quoteAssetPrecision));
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

                    security.DecimalsVolume = Convert.ToInt32(sec.baseAssetPrecision);

                    security.MinTradeAmount = sec.quoteAmountPrecision.ToDecimal();
                    security.MinTradeAmountType = MinTradeAmountType.C_Currency;
                    security.VolumeStep = GetStep(Convert.ToInt32(sec.baseAssetPrecision));

                    _securities.Add(security);
                }
            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading stocks: {e.Message}" + e.ToString(), LogMessageType.Error);
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

                HttpResponseMessage response = Query(HttpMethod.Get, endPoint, null, true); //_restClient.Get(endPoint, secured: true);

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

            // (UTC) in Unix Time Seconds
            endPoint += "&interval=" + timeFrame;
            endPoint += "&startTime=" + startTime;
            endPoint += "&endTime=" + endTime;
            endPoint += "&limit=500";

            try
            {
                HttpResponseMessage response = Query(HttpMethod.Get, endPoint); //_restClient.Get(endPoint, secured: false);

                string content = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcCandlesHistory parsed =
                        JsonConvert.DeserializeAnonymousType(content, new MexcCandlesHistory());

                    return parsed;
                }
                else
                {
                    SendLogMessage("Candles request error to url='" + endPoint + "'. Status: " +
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
            if (startTime >= endTime ||
                startTime >= DateTime.UtcNow ||
                actualTime > endTime ||
                actualTime > DateTime.UtcNow)
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

        private readonly string _ws = "wss://wbs.mexc.com/ws";

        private readonly string _wsPrivate = "wss://wbs.mexc.com/ws";

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
                    /*_webSocketPublic.SslConfiguration.EnabledSslProtocols
                    = System.Security.Authentication.SslProtocols.None
                    | System.Security.Authentication.SslProtocols.Tls12
                    | System.Security.Authentication.SslProtocols.Tls13;*/
                    _webSocketPublic.EmitOnPing = true;
                    _webSocketPublic.OnOpen += _webSocketPublic_OnOpen;
                    _webSocketPublic.OnClose += _webSocketPublic_OnClose;
                    _webSocketPublic.OnMessage += _webSocketPublic_OnMessage;
                    _webSocketPublic.OnError += _webSocketPublic_OnError;
                    _webSocketPublic.Connect();

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
                    /*_webSocketPrivate.SslConfiguration.EnabledSslProtocols
                    = System.Security.Authentication.SslProtocols.None
                   | System.Security.Authentication.SslProtocols.Tls12
                   | System.Security.Authentication.SslProtocols.Tls13;*/
                    _webSocketPrivate.EmitOnPing = true;
                    _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                    _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                    _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError += _webSocketPrivate_OnError;

                    _webSocketPrivate.Connect();
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

        private void UnsubscribeFromPrivateWebSockets()
        {
            if (_webSocketPrivate == null)
            {
                return;
            }

            _webSocketPrivate.Send($"{{ \"method\": \"UNSUBSCRIPTION\", \"params\": [\"spot@private.orders.v3.api\"] }}");// Order
            _webSocketPrivate.Send($"{{ \"method\": \"UNSUBSCRIPTION\", \"params\": [\"spot@private.account.v3.api\"] }}"); // Portfolio
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

                                    _webSocketPublic?.Send($"{{ \"method\": \"UNSUBSCRIPTION\", \"params\": [\"spot@public.deals.v3.api@{securityName}\"] }}"); // Trades
                                    _webSocketPublic.Send($"{{ \"method\": \"UNSUBSCRIPTION\", \"params\": [\"spot@public.limit.depth.v3.api@{securityName}@20\"] }}"); // MarketDepth
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

        private string GetListenKey()
        {
            try
            {
                string endPoint = "/api/v3/userDataStream";

                HttpResponseMessage response = Query(HttpMethod.Post, endPoint, null, true); //_restClient.Post(endPoint, secured: true);

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

                HttpResponseMessage response = Query(HttpMethod.Put, endPoint, query, true); //query_restClient.Put(endPoint, query, secured: true);

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

                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (_FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                if (e.Data.Contains("PONG"))
                { // pong message
                    return;
                }

                if (e.Data.IndexOf("\"msg\"") >= 0)
                {
                    // responce message - ignore
                    //SendLogMessage("WebSocketData, message:" + e.Message, LogMessageType.Connect);
                    return;
                }

                _FIFOListWebSocketPublicMessage.Enqueue(e.Data);
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
            SendLogMessage("Socket Public activated", LogMessageType.System);
            CheckActivationSockets();
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

                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (_FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                if (e.Data.Contains("PONG"))
                { // pong message
                    return;
                }

                if (e.Data.IndexOf("\"msg\"") >= 0)
                {
                    // responce message - ignore
                    //SendLogMessage("WebSocketData, message:" + e.Message, LogMessageType.Connect);
                    return;
                }

                _FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
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
            SendLogMessage("Socket Private activated", LogMessageType.System);

            CheckActivationSockets();

            _webSocketPrivate.Send($"{{ \"method\": \"SUBSCRIPTION\", \"params\": [\"spot@private.orders.v3.api\"] }}");// Order
            _webSocketPrivate.Send($"{{ \"method\": \"SUBSCRIPTION\", \"params\": [\"spot@private.account.v3.api\"] }}"); // Portfolio
        }

        #endregion

        #region 8 WebSocket check alive

        private DateTime _timeToSendPingPublic = DateTime.Now;
        private DateTime _timeToSendPingPrivate = DateTime.Now;
        private DateTime _lastTimeProlongListenKey = DateTime.Now;

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

                    if (_webSocketPublicSpot != null)
                    {
                        for (int i = 0; i < _webSocketPublicSpot.Count; i++)
                        {
                            WebSocket _webSocketPublic = _webSocketPublicSpot[i];
                            if (_webSocketPublic != null && _webSocketPublic?.ReadyState == WebSocketState.Open
                                || _webSocketPublic.ReadyState == WebSocketState.Connecting)
                            {
                                if (_timeToSendPingPublic.AddSeconds(15) < DateTime.Now)
                                {
                                    _webSocketPublic.Send("{\"method\":\"PING\"}");
                                    _timeToSendPingPublic = DateTime.Now;
                                }
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
                        if (_timeToSendPingPrivate.AddSeconds(15) < DateTime.Now)
                        {
                            _webSocketPrivate.Send("{\"method\":\"PING\"}");
                            _timeToSendPingPrivate = DateTime.Now;
                        }

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

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(150));

        private List<string> _subscribedSecurities = new List<string>();

        public void Subscrible(Security security)
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
                    && _subscribedSecurities.Count % 15 == 0)
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
                    _webSocketPublic.Send($"{{ \"method\": \"SUBSCRIPTION\", \"params\": [\"spot@public.deals.v3.api@{security.Name}\"] }}"); // Trades
                    _webSocketPublic.Send($"{{ \"method\": \"SUBSCRIPTION\", \"params\": [\"spot@public.limit.depth.v3.api@{security.Name}@20\"] }}"); // MarketDepth

                    if (_subscribedSecurities.Exists(s => s == security.Name) == false)
                    {
                        _subscribedSecurities.Add(security.Name);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent;

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private void DataMessageReader()
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

                    SoketBaseMessage baseMessage =
                        JsonConvert.DeserializeAnonymousType(message, new SoketBaseMessage());

                    if (baseMessage == null
                        || string.IsNullOrEmpty(baseMessage.c))
                    {
                        continue;
                    }

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
                SendLogMessage("Wrong 'Trade' message:" + baseMessage, LogMessageType.Error);
                return;
            }

            for (int i = 0; i < deals.deals.Count; i++)
            {
                MexcDeal deal = deals.deals[i];

                Trade trade = new Trade();
                trade.SecurityNameCode = baseMessage.s;
                trade.Price = deal.p.ToDecimal();
                trade.Time = ConvertToDateTimeFromUnixFromMilliseconds(deal.t);
                trade.Id = deal.t + deal.S + baseMessage.s;

                if (deal.S == "1")
                {
                    trade.Side = Side.Buy;
                }
                else
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
                SendLogMessage("Wrong 'MarketDepth' message:" + baseMessage, LogMessageType.Error);
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

            if (baseOrder.S == "2")
            {
                order.Side = Side.Sell;
            }

            order.PortfolioNumber = this._portfolioName;
            order.Volume = baseOrder.v.ToDecimal();
            order.VolumeExecute = order.Volume - baseOrder.V.ToDecimal();
            order.Price = baseOrder.p.ToDecimal();

            //LIMIT_ORDER(1),POST_ONLY(2),IMMEDIATE_OR_CANCEL(3),
            //FILL_OR_KILL(4),MARKET_ORDER(5); STOP_LIMIT(100)
            if (baseOrder.o == "1")
            {
                order.TypeOrder = OrderPriceType.Limit;
            }
            else
            {
                order.TypeOrder = OrderPriceType.Market;
            }

            order.TimeCreate = ConvertToDateTimeFromUnixFromMilliseconds(baseOrder.O);
            order.TimeCallBack = ConvertToDateTimeFromUnixFromMilliseconds(baseMessage.t.ToString());
            order.ServerType = ServerType.MexcSpot;

            //status 1:New order 2:Filled 3:Partially filled 4:Order canceled
            //5:Order filled partially, and then the rest of the order is canceled

            if (baseOrder.s == "1")
            {
                order.State = OrderStateType.Active;
            }
            else if (baseOrder.s == "2")
            {
                order.State = OrderStateType.Done;
            }
            else if (baseOrder.s == "3")
            {
                order.State = OrderStateType.Partial;
            }
            else if (baseOrder.s == "4" || baseOrder.s == "5")
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

            if (portf == null)
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

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(1000));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(500));

        private RateGate _rateGateGetOrder = new RateGate(1, TimeSpan.FromMilliseconds(500));

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            if (_activeSecurities.Exists(s => s == order.SecurityNameCode) == false)
            {
                _activeSecurities.Add(order.SecurityNameCode);
            }

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

                HttpResponseMessage response = Query(HttpMethod.Post, endPoint, query, true); //_restClient.Post(endPoint, query, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcNewOrderResponse parsed =
                        JsonConvert.DeserializeAnonymousType(content, new MexcNewOrderResponse());

                    if (parsed != null && !string.IsNullOrEmpty(parsed.orderId))
                    {
                        //Everything is OK
                        SendLogMessage($"Order created: {content}", LogMessageType.Connect);
                    }
                    else
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

        public bool CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            try
            {
                string endPoint = "/api/v3/order";

                Dictionary<string, string> query = new Dictionary<string, string>();
                query.Add("symbol", order.SecurityNameCode);
                query.Add("origClientOrderId", order.NumberUser.ToString());

                HttpResponseMessage response = Query(HttpMethod.Delete, endPoint, query, true);// _restClient.Delete(endPoint, query, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    MexcOrderResponse parsed =
                        JsonConvert.DeserializeAnonymousType(content, new MexcOrderResponse());

                    if (parsed != null)
                    {
                        //Everything is OK - do nothing
                        SendLogMessage("Cancel order - OK: " + content, LogMessageType.Connect);
                        return true;
                    }
                    else
                    {
                        GetOrderStatus(order);
                        SendLogMessage($"Cancel order, answer is wrong: {content}", LogMessageType.Error);
                    }
                }
                else
                {
                    if (content.Contains("-2011"))
                    {
                        GetOrderStatus(order);
                    }
                    else
                    {
                        GetOrderStatus(order);
                        SendLogMessage("Cancel order failed. Status: "
                            + response.StatusCode + "  " + order.SecurityNameCode + ", " + content, LogMessageType.Error);
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

                HttpResponseMessage response = Query(HttpMethod.Get, endPoint, query, true); //_restClient.Get(endPoint,query, secured: true);

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
                else if (response.StatusCode == HttpStatusCode.NotFound)
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

                HttpResponseMessage response = Query(HttpMethod.Get, endPoint, query, true); //_restClient.Get(endPoint, query, secured: true);
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
                    SendLogMessage("Get order request " + userOrderId + " error: " + content,
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
                UpdateTrades(myOrder);
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
                order.TimeCallBack = ConvertToDateTimeFromUnixFromMilliseconds(baseOrder.updateTime);
            }
            else
            {
                order.TimeCallBack = ConvertToDateTimeFromUnixFromMilliseconds(baseOrder.time);
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

                HttpResponseMessage response = Query(HttpMethod.Get, endPoint, query, true); //_restClient.Get(endPoint, query, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;

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
            trade.Time = ConvertToDateTimeFromUnixFromMilliseconds(baseTrade.time);

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

        #endregion

        #region 12 Queries

        private HttpClient _client = new HttpClient();

        public RateGate _rateGateRest = new RateGate(1, TimeSpan.FromMilliseconds(30));

        private HttpResponseMessage Query(HttpMethod method, string endPoint, Dictionary<string, string> queryParams = null, bool secured = false)
        {
            _rateGateRest.WaitToProceed();

            HttpRequestMessage request = null;

            if (secured)
            {
                string query = GetSecuredQueryString(queryParams);

                string url = _baseUrl + endPoint;
                if (!string.IsNullOrEmpty(query))
                {
                    url += "?" + query;
                }

                request = new HttpRequestMessage(method, url);
                request.Headers.Add("X-MEXC-APIKEY", _publicKey);
            }
            else
            {
                string query = GetQueryString(queryParams);

                string url = _baseUrl + endPoint;
                if (!string.IsNullOrEmpty(query))
                {
                    url += "?" + query;
                }
                request = new HttpRequestMessage(method, url);
            }

            HttpResponseMessage response = _client.SendAsync(request).Result;

            return response;
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

            string timestamp = GetTimestamp();

            if (query.Length > 0)
                query += "&";

            query += "recvWindow=10000&timestamp=" + timestamp;

            string signature = GenerateSignature(query, _secretKey);

            query += "&signature=" + signature;

            return query;
        }

        public static string GetTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
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

        public event Action<Funding> FundingUpdateEvent;

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion
    }
}