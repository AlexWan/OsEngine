/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitMartFutures.Json;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using OsEngine.Entity.WebSocketOsEngine;
using RestSharp;
using System.Security.Cryptography;

namespace OsEngine.Market.Servers.BitMartFutures
{
    public class BitMartFuturesServer : AServer
    {
        public BitMartFuturesServer()
        {
            BitMartFuturesServerRealization realization = new BitMartFuturesServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterString(OsLocalization.Market.Memo, "");
        }
    }

    public class BitMartFuturesServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BitMartFuturesServerRealization()
        {
            Thread worker = new Thread(ConnectionCheckThread);
            worker.Name = "CheckAliveBitMartFutures";
            worker.Start();

            Thread worker2 = new Thread(DataMessageReader);
            worker2.Name = "DataMessageReaderBitMartFutures";
            worker2.Start();

            Thread worker3 = new Thread(PortfolioMessageReader);
            worker3.Name = "PortfolioMessageReaderBitMartFutures";
            worker3.Start();
        }

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
                _memo = ((ServerParameterString)ServerParameters[2]).Value;

                if (string.IsNullOrEmpty(_publicKey)
                    || string.IsNullOrEmpty(_secretKey)
                    || string.IsNullOrEmpty(_memo))
                {
                    SendLogMessage("Connection terminated. You must specify the public and private keys. You can get it on the BitMartFutures website",
                        LogMessageType.Error);
                    return;
                }

                _restClient = new BitMartRestClient(_publicKey, _secretKey, _memo);

                if (CheckConnection() == false)
                {
                    SendLogMessage("Authorization Error. Probably an invalid keys are specified. You can see it on the BitMartFutures website.",
                    LogMessageType.Error);
                    return;
                }

                CreatePublicWebSocketConnect();
                CreatePrivateWebSocketConnect();
                //CheckActivationSockets();
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
                UnsubscribeFromAllWebSockets();
                _securities.Clear();
                _myPortfolious.Clear();
                _serverOrderIDs.Clear();

                DeleteWebSocketConnection();

                SendLogMessage("Dispose. Connection Closed by BitMartFutures. WebSocket Closed Event", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
            _subscribledSecurities.Clear();
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

        public ServerType ServerType => ServerType.BitMartFutures;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        private string _publicKey;

        private string _secretKey;

        private string _memo;

        private string _baseUrl = "https://api-cloud-v2.bitmart.com";

        public List<IServerParameter> ServerParameters { get; set; }

        private BitMartRestClient _restClient;

        #endregion

        #region 3 Securities

        private List<Security> _securities = new List<Security>();

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
            string endPoint = "/contract/public/details";

            try
            {
                HttpResponseMessage response = _restClient.Get(endPoint, secured: false);

                string content = response.Content.ReadAsStringAsync().Result;
                //SendLogMessage("UpdateSec resp: " + content, LogMessageType.Connect);
                BitMartBaseMessageDict parsed =
                    JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessageDict());

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (parsed != null && parsed.data != null && parsed.data.ContainsKey("symbols"))
                    {
                        string symbols = parsed.data["symbols"].ToString();

                        List<BitMartSecurityRest> securities =
                            JsonConvert.DeserializeAnonymousType(symbols, new List<BitMartSecurityRest>());
                        UpdateSecuritiesFromServer(securities);
                    }
                }
                else
                {
                    string message = "";
                    if (parsed != null)
                    {
                        message = parsed.message;
                    }
                    SendLogMessage("Securities request error. Status: " +
                        response.StatusCode + ", " + message, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Securities request error: " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecuritiesFromServer(List<BitMartSecurityRest> stocks)
        {
            try
            {
                if (stocks == null ||
                    stocks.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < stocks.Count; i++)
                {
                    BitMartSecurityRest item = stocks[i];

                    if (item.last_price == "0"
                        || item.volume_24h == "0")
                    {
                        continue;
                    }

                    Security newSecurity = new Security();

                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.symbol;
                    newSecurity.NameClass = item.quote_currency;
                    newSecurity.NameId = item.symbol;
                    newSecurity.State = SecurityStateType.Activ;
                    newSecurity.Decimals = GetDecimalsVolume(item.price_precision);
                    newSecurity.DecimalsVolume = GetDecimalsVolume(item.vol_precision);
                    newSecurity.PriceStep = GetPriceStep(newSecurity.Decimals);
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.Lot = 1;
                    newSecurity.SecurityType = SecurityType.Futures;
                    newSecurity.Exchange = ServerType.BitMartFutures.ToString();
                    newSecurity.MinTradeAmount = item.min_volume.ToDecimal();

                    _securities.Add(newSecurity);
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading stocks: {e.Message}" + e.ToString(), LogMessageType.Error);
            }
        }

        private static int GetDecimalsVolume(string str)
        {
            string[] s = str.Split('.');
            if (s.Length > 1)
            {
                return s[1].Length;
            }
            else
            {
                return 0;
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

        private List<Portfolio> _myPortfolious = new List<Portfolio>();

        private string _portfolioName = "BitMartFutures";

        public void GetPortfolios()
        {
            GetCurrentPortfolio();
        }

        private bool GetCurrentPortfolio()
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string endPoint = $"/contract/private/assets-detail";

                //SendLogMessage("GetCurrentPortfolio request: " + endPoint, LogMessageType.Connect);
                HttpResponseMessage response = _restClient.Get(endPoint, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;
                //SendLogMessage("GetCurrentPortfolio message: " + content, LogMessageType.Connect);
                BitMartBaseMessage parsed =
                    JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (parsed != null && parsed.data != null)
                    {
                        string wallet = parsed.data.ToString();
                        BitMartFuturesPortfolioItems portfolio =
                            JsonConvert.DeserializeAnonymousType(wallet, new BitMartFuturesPortfolioItems());

                        ConvertToPortfolio(portfolio);

                        return true;
                    }
                }
                else
                {
                    string message = "";
                    if (parsed != null)
                    {
                        message = parsed.message;
                    }
                    SendLogMessage("Portfolio request error. Status: "
                        + response.StatusCode + "  " + _portfolioName +
                        ", " + message, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio request error " + exception.ToString(), LogMessageType.Error);
            }

            return false;
        }

        private void ConvertToPortfolio(BitMartFuturesPortfolioItems portfolioItems)
        {
            if (portfolioItems == null)
            {
                return;
            }

            Portfolio portfolio = new Portfolio();
            portfolio.Number = this._portfolioName;
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            for (int i = 0; i < portfolioItems.Count; i++)
            {
                BitMartFuturesPortfolioItem item = portfolioItems[i];

                PositionOnBoard pos = new PositionOnBoard()
                {
                    PortfolioName = this._portfolioName,
                    SecurityNameCode = item.currency,
                    ValueBlocked = item.frozen_balance.ToDecimal(),
                    ValueCurrent = item.available_balance.ToDecimal()
                };

                portfolio.SetNewPosition(pos);
            }

            if (_myPortfolious.Count > 0)
            {
                _myPortfolious[0] = portfolio;
            }
            else
            {
                _myPortfolious.Add(portfolio);
            }


            if (PortfolioEvent != null)
            {
                PortfolioEvent(_myPortfolious);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            DateTime endTime = DateTime.Now.ToUniversalTime();

            while (endTime.Hour != 23)
            {
                endTime = endTime.AddHours(1);
            }

            int candlesInDay = 0;

            if (timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes >= 1)
            {
                candlesInDay = 900 / Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
            }
            else
            {
                candlesInDay = 54000 / Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalSeconds);
            }

            if (candlesInDay == 0)
            {
                candlesInDay = 1;
            }

            int daysCount = candleCount / candlesInDay;

            if (daysCount == 0)
            {
                daysCount = 1;
            }

            daysCount++;

            if (daysCount > 5)
            { // add weekends
                daysCount = daysCount + (daysCount / 5) * 2;
            }

            DateTime startTime = endTime.AddDays(-daysCount);

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);

            while (candles.Count > candleCount)
            {
                candles.RemoveAt(0);
            }

            return candles;
        }

        private readonly HashSet<int> _allowedTf = new HashSet<int> {
            1, 3, 5, 15, 30, 60, 120, 240, 360, 720, 1440, 10080};

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
                        DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            _rateGateSendOrder.WaitToProceed();

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!_allowedTf.Contains(tfTotalMinutes))
                return null;

            if (startTime != actualTime)
            {
                startTime = actualTime;
            }

            List<Candle> candles = new List<Candle>();

            // 500 - max candles at BitMart
            TimeSpan additionTime = TimeSpan.FromMinutes(tfTotalMinutes * 500);

            DateTime endTimeReal = startTime.Add(additionTime);

            while (startTime < endTime)
            {
                BitMartCandlesHistory history = GetHistoryCandle(security, tfTotalMinutes, startTime, endTimeReal);
                List<Candle> newCandles = ConvertToOsEngineCandles(history);

                if (newCandles != null &&
                    newCandles.Count > 0)
                {
                    //It could be 2 same candles from different requests - check and fix
                    DateTime lastTime = DateTime.MinValue;
                    if (candles.Count > 0)
                    {
                        lastTime = candles[candles.Count - 1].TimeStart;
                    }

                    for (int i = 0; i < newCandles.Count; i++)
                    {
                        if (newCandles[i].TimeStart > lastTime)
                        {
                            candles.Add(newCandles[i]);
                            lastTime = newCandles[i].TimeStart;
                        }
                    }
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

        private BitMartCandlesHistory GetHistoryCandle(Security security, int tfTotalMinutes,
            DateTime startTime, DateTime endTime)
        {
            DateTime maxStartTime = endTime.AddMinutes(-500 * tfTotalMinutes);

            if (maxStartTime > startTime)
            {
                SendLogMessage($"Too much candels for TF {tfTotalMinutes}", LogMessageType.Error);
                return null;
            }

            string endPoint = "/contract/public/kline?symbol=" + security.Name;

            //use Unix Time Seconds
            endPoint += "&step=" + tfTotalMinutes;
            endPoint += "&start_time=" + TimeManager.GetTimeStampSecondsToDateTime(startTime);
            endPoint += "&end_time=" + TimeManager.GetTimeStampSecondsToDateTime(endTime);

            //SendLogMessage("Get Candles: " + endPoint, LogMessageType.Connect);
            try
            {
                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content;
                    //SendLogMessage("GetHistoryCandle resp: " + content, LogMessageType.Connect);
                    BitMartBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());

                    if (parsed != null && parsed.data != null)
                    {
                        string history = parsed.data.ToString();
                        BitMartCandlesHistory candles =
                            JsonConvert.DeserializeAnonymousType(history, new BitMartCandlesHistory());

                        return candles;

                    }
                    else
                    {
                        SendLogMessage("Empty Candles request error. Status: " + response.StatusCode, LogMessageType.Error);
                    }

                }
                else
                {
                    SendLogMessage("Candles request error to url='" + endPoint + "'. Status: " + response.StatusCode, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Candles request error:" + exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private List<Candle> ConvertToOsEngineCandles(BitMartCandlesHistory candles)
        {
            if (candles == null)
                return null;

            List<Candle> result = new List<Candle>();

            for (int i = 0; i < candles.Count; i++)
            {
                BitMartCandle curCandle = candles[i];

                Candle newCandle = new Candle();
                newCandle.Open = curCandle.open_price.ToDecimal();
                newCandle.High = curCandle.high_price.ToDecimal();
                newCandle.Low = curCandle.low_price.ToDecimal();
                newCandle.Close = curCandle.close_price.ToDecimal();
                newCandle.Volume = curCandle.volume.ToDecimal();
                newCandle.TimeStart = ConvertToDateTimeFromUnixFromSeconds(curCandle.timestamp.ToString());

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

        private readonly string _webSocketUrlPublic = "wss://openapi-ws-v2.bitmart.com/api?protocol=1.1";

        private readonly string _webSocketUrlPrivate = "wss://openapi-ws-v2.bitmart.com/user?protocol=1.1";

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (FIFOListWebSocketPublicMessage == null)
                {
                    FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                }

                _webSocketPublic.Add(CreateNewPublicSocket());
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewPublicSocket()
        {
            try
            {
                WebSocket webSocketPublicNew = new WebSocket(_webSocketUrlPublic);

                //if (_myProxy != null)
                //{
                //    webSocketPublicNew.SetProxy(_myProxy);
                //}

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += WebSocketPublicNew_OnOpen;
                webSocketPublicNew.OnMessage += WebSocketPublicNew_OnMessage;
                webSocketPublicNew.OnError += WebSocketPublicNew_OnError;
                webSocketPublicNew.OnClose += WebSocketPublicNew_OnClose;
                webSocketPublicNew.Connect();

                return webSocketPublicNew;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void CreatePrivateWebSocketConnect()
        {
            try
            {
                if (_webSocketPrivate != null)
                {
                    return;
                }

                _webSocketPrivate = new WebSocket(_webSocketUrlPrivate);

                //if (_myProxy != null)
                //{
                //    _webSocketPrivate.SetProxy(_myProxy);
                //}

                _webSocketPrivate.EmitOnPing = true;
                /* _webSocketPrivate.SslConfiguration.EnabledSslProtocols
                     = System.Security.Authentication.SslProtocols.Tls12
                    | System.Security.Authentication.SslProtocols.Tls13;*/
                _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
                _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
                _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
                _webSocketPrivate.OnError += _webSocketPrivate_OnError;
                _webSocketPrivate.Connect();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebSocketConnection()
        {
            if (_webSocketPublic != null)
            {
                try
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        webSocketPublic.OnOpen -= WebSocketPublicNew_OnOpen;
                        webSocketPublic.OnClose -= WebSocketPublicNew_OnClose;
                        webSocketPublic.OnMessage -= WebSocketPublicNew_OnMessage;
                        webSocketPublic.OnError -= WebSocketPublicNew_OnError;

                        if (webSocketPublic.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.CloseAsync();
                        }

                        webSocketPublic = null;
                    }
                }
                catch
                {
                    // ignore
                }

                _webSocketPublic.Clear();
            }

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

        private string _socketActivateLocker = "socketAcvateLocker";

        private void CheckActivationSockets()
        {
            lock (_socketActivateLocker)
            {

                if (_webSocketPrivate == null
                    || _webSocketPrivate.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (_webSocketPublic.Count == 0)
                {
                    Disconnect();
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[0];

                if (webSocketPublic == null
                    || webSocketPublic?.ReadyState != WebSocketState.Open)
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

        private void CreateAuthMessageWebSocKet()
        {
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string sign = GenerateSignature(timestamp, "bitmart.WebSocket");

            _webSocketPrivate.Send($"{{\"action\": \"access\", \"args\": [\"{_publicKey}\", \"{timestamp}\", \"{sign}\",\"web\"]}}");
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublicNew_OnClose(object arg1, CloseEventArgs arg2)
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

        private void WebSocketPublicNew_OnError(object arg1, OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs e)
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

        private void WebSocketPublicNew_OnMessage(object arg1, MessageEventArgs e)
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

                if (FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                if (e.IsBinary)
                {
                    string message = Decompress(e.RawData);
                    FIFOListWebSocketPublicMessage.Enqueue(message);
                }

                if (e.IsText)
                {
                    //if (e.Data.StartsWith("{\"errorMessage\""))
                    //{
                    //    SendLogMessage(e.Data, LogMessageType.Error);
                    //    return;
                    //}

                    if (e.Data.Contains("pong"))
                    { // pong message
                        return;
                    }

                    FIFOListWebSocketPublicMessage.Enqueue(e.Data);
                }
            }
            catch (Exception error)
            {
                SendLogMessage("Trade socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnOpen(object arg1, EventArgs arg2)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("BitMartFutures WebSocket Public connection open", LogMessageType.System);
                    CheckActivationSockets();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnError(object arg1, OsEngine.Entity.WebSocketOsEngine.ErrorEventArgs e)
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
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnMessage(object arg1, MessageEventArgs e)
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

                if (string.IsNullOrEmpty(e.ToString()))
                {
                    return;
                }

                if (FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                if (e.IsBinary)
                {
                    string message = Decompress(e.RawData);
                    FIFOListWebSocketPrivateMessage.Enqueue(message);
                }

                if (e.IsText)
                {
                    if (e.Data.Contains("\"success\":false,\"error\""))
                    {
                        SendLogMessage(e.Data, LogMessageType.Error);
                        return;
                    }

                    if (e.Data.Contains("{\"action\":\"access\",\"success\":true}\n"))
                    {
                        SubscriblePrivate();
                    }

                    if (e.Data.Contains("pong"))
                    { // pong message
                        return;
                    }

                    FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
                }
            }
            catch (Exception error)
            {
                SendLogMessage("Portfolio socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnClose(object arg1, CloseEventArgs e)
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

        private void _webSocketPrivate_OnOpen(object arg1, EventArgs arg2)
        {
            try
            {
                CreateAuthMessageWebSocKet();
                SendLogMessage("BitMartFutures WebSocket Private connection open", LogMessageType.System);
                CheckActivationSockets();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private static string Decompress(byte[] data)
        {
            using (MemoryStream msi = new MemoryStream(data))
            using (MemoryStream mso = new MemoryStream())
            {
                using DeflateStream decompressor = new DeflateStream(msi, CompressionMode.Decompress);
                decompressor.CopyTo(mso);

                return Encoding.UTF8.GetString(mso.ToArray());
            }
        }

        #endregion

        #region 8 WebSocket check alive

        private void ConnectionCheckThread()
        {
            while (true)
            {
                Thread.Sleep(10000);

                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        if (webSocketPublic != null
                            && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.Send("{\"action\":\"ping\"}");
                        }
                        else
                        {
                            Disconnect();
                        }
                    }

                    if (_webSocketPrivate != null &&
                        (_webSocketPrivate.ReadyState == WebSocketState.Open ||
                        _webSocketPrivate.ReadyState == WebSocketState.Connecting)
                        )
                    {
                        _webSocketPrivate.Send("{\"action\":\"ping\"}");
                    }
                    else
                    {
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

        #region 9 WebSocket Security subscrible

        private RateGate _rateGateSubscrible = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<Security> _subscribledSecurities = new List<Security>();

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscrible.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _subscribledSecurities.Count; i++)
                {
                    if (_subscribledSecurities[i].NameClass == security.NameClass
                    && _subscribledSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }

                _subscribledSecurities.Add(security);

                if (_webSocketPublic.Count == 0)
                {
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribledSecurities.Count != 0
                    && _subscribledSecurities.Count % 50 == 0)
                {
                    // creating a new socket
                    WebSocket newSocket = CreateNewPublicSocket();

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
                        _webSocketPublic.Add(newSocket);
                        webSocketPublic = newSocket;
                    }
                }

                if (webSocketPublic != null)
                {
                    webSocketPublic.Send($" {{ \"action\":\"subscribe\", \"args\":[\"futures/trade:{security.Name}\"]}}");
                    webSocketPublic.Send($"{{ \"action\":\"subscribe\",\"args\":[\"futures/depth20:{security.Name}@100ms\"]}}");
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void SubscriblePrivate()
        {
            try
            {
                _webSocketPrivate.Send($"{{\"action\": \"subscribe\",\"args\":[\"futures/asset:USDT\", \"futures/asset:BTC\", \"futures/asset:ETH\", \"futures/asset:USDC\"]}}");
                _webSocketPrivate.Send($"{{\"action\": \"subscribe\",\"args\":[\"futures/position\"]}}");
                _webSocketPrivate.Send($"{{\"action\": \"subscribe\",\"args\": [\"futures/order\"]}}");
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.Message, LogMessageType.Error);
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            try
            {
                if (_webSocketPublic.Count != 0
                    && _webSocketPublic != null)
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        try
                        {
                            if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open)
                            {
                                if (_subscribledSecurities != null)
                                {
                                    for (int j = 0; j < _subscribledSecurities.Count; j++)
                                    {
                                        string securityName = _subscribledSecurities[j].Name;

                                        webSocketPublic.Send($" {{ \"action\":\"unsubscribe\", \"args\":[\"futures/trade:{securityName}\"]}}");
                                        webSocketPublic.Send($"{{ \"action\":\"unsubscribe\",\"args\":[\"futures/depth20:{securityName}@100ms\"]}}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            if (_webSocketPrivate != null
                && _webSocketPrivate.ReadyState == WebSocketState.Open)
            {
                try
                {
                    _webSocketPrivate.Send($"{{\"action\": \"unsubscribe\",\"args\":[\"futures/asset:USDT\", \"futures/asset:BTC\", \"futures/asset:ETH\", \"futures/asset:USDC\"]}}");
                    _webSocketPrivate.Send($"{{\"action\": \"unsubscribe\",\"args\":[\"futures/position\"]}}");
                    _webSocketPrivate.Send($"{{\"action\": \"unsubscribe\",\"args\": [\"futures/order\"]}}");
                }
                catch
                {
                    // ignore
                }
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent;

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private void DataMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (FIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    FIFOListWebSocketPublicMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    SoketBaseMessage baseMessage =
                        JsonConvert.DeserializeAnonymousType(message, new SoketBaseMessage());

                    if (baseMessage == null
                        || string.IsNullOrEmpty(baseMessage.group))
                    {
                        continue;
                    }

                    if (baseMessage.data == null)
                    {
                        SendLogMessage("DataMessageReader empty data: " + message, LogMessageType.Connect);
                        continue;
                    }

                    //SendLogMessage("DataMessageReader message: " + message, LogMessageType.Connect);

                    if (baseMessage.group.Contains("/depth"))
                    {
                        UpdateMarketDepth(baseMessage.data.ToString());
                    }
                    else if (baseMessage.group.Contains("/trade"))
                    {
                        UpdateTrade(baseMessage.data.ToString());
                    }
                    else
                    {
                        SendLogMessage("Unknown message: " + baseMessage.group, LogMessageType.Error);
                    }

                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(2000);
                }
            }
        }

        private void UpdateTrade(string data)
        {
            MarketTrades baseTrades =
                JsonConvert.DeserializeAnonymousType(data, new MarketTrades());


            for (int i = 0; i < baseTrades.Count; i++)
            {
                MarketTrade baseTrade = baseTrades[i];

                if (string.IsNullOrEmpty(baseTrade.symbol))
                {
                    continue;
                }

                Trade trade = new Trade();
                trade.SecurityNameCode = baseTrade.symbol;
                trade.Price = baseTrade.deal_price.ToDecimal();
                trade.Volume = baseTrade.deal_vol.ToDecimal();
                trade.Time = DateTime.Parse(baseTrade.created_at);
                trade.Id = baseTrade.trade_id.ToString();

                if (baseTrade.way <= 4)
                {
                    trade.Side = Side.Buy;
                }
                else
                {
                    trade.Side = Side.Sell;
                }

                NewTradesEvent?.Invoke(trade);
            }
        }

        private readonly object _lastMarketDepthLock = new object();
        Dictionary<string, MarketDepth> _lastMarketDepth = new Dictionary<string, MarketDepth>();

        private void UpdateMarketDepth(string data)
        {
            MarketDepthBitMart baseDepth =
                JsonConvert.DeserializeAnonymousType(data, new MarketDepthBitMart());


            if (String.IsNullOrEmpty(baseDepth.symbol) || baseDepth.depths.Count == 0)
            {
                return;
            }



            MarketDepth depth = new MarketDepth();
            depth.SecurityNameCode = baseDepth.symbol;
            depth.Time = ConvertToDateTimeFromUnixFromMilliseconds(baseDepth.ms_t.ToString());

            decimal maxBid = 0;
            decimal minAsk = decimal.MaxValue;

            for (int i = 0; i < baseDepth.depths.Count; i++)
            {
                MarketDepthLevelBitMart level = baseDepth.depths[i];

                if (level == null)
                {
                    continue;
                }

                MarketDepthLevel newBid = new MarketDepthLevel();
                newBid.Price = level.price.ToDecimal();


                if (baseDepth.way == 1) //bids
                {
                    newBid.Bid = level.vol.ToDecimal();
                    depth.Bids.Add(newBid);
                    maxBid = Math.Max(newBid.Price, maxBid);
                }
                else //asks
                {
                    newBid.Ask = level.vol.ToDecimal();
                    depth.Asks.Add(newBid);
                    minAsk = Math.Min(newBid.Price, minAsk);
                }
            }

            if (_lastMdTime != DateTime.MinValue &&
                _lastMdTime >= depth.Time)
            {
                depth.Time = _lastMdTime.AddTicks(1);
            }

            _lastMdTime = depth.Time;
            bool skipEvent = false;

            lock (this._lastMarketDepthLock)
            {
                // server sends asks or bits in one message only
                // this is workaround: save asks (bits) from previous message and put
                // it to current

                if (this._lastMarketDepth.ContainsKey(depth.SecurityNameCode))
                {
                    MarketDepth prev = this._lastMarketDepth[depth.SecurityNameCode];
                    if (depth.Asks.Count == 0)
                    {
                        for (int i = 0; i < prev.Asks.Count; i++)
                        {
                            if (prev.Asks[i].Price > maxBid)
                            {
                                depth.Asks.Add(prev.Asks[i]);
                            }
                        }
                    }

                    if (depth.Bids.Count == 0)
                    {
                        for (int i = 0; i < prev.Bids.Count; i++)
                        {
                            if (prev.Bids[i].Price < minAsk)
                            {
                                depth.Bids.Add(prev.Bids[i]);
                            }
                        }
                    }
                }
                else
                {
                    skipEvent = true;
                }
                _lastMarketDepth[depth.SecurityNameCode] = depth;
            }

            if (skipEvent)
            {
                return;
            }

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
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (FIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    FIFOListWebSocketPrivateMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    SoketBaseMessage baseMessage = JsonConvert.DeserializeAnonymousType(message, new SoketBaseMessage());

                    if (baseMessage == null
                        || string.IsNullOrEmpty(baseMessage.group))
                    {
                        continue;
                    }

                    if (baseMessage.data == null)
                    {
                        SendLogMessage("PorfolioWebSocket empty message: " + message, LogMessageType.Connect);
                        continue;
                    }

                    //SendLogMessage("PorfolioWebSocket Reader: " + message, LogMessageType.Connect);

                    if (baseMessage.group.Contains("futures/asset"))
                    {
                        UpdateMyPortfolio(baseMessage.data.ToString());

                    }
                    else if (baseMessage.group.Contains("futures/position"))
                    {
                        UpdateMyPositions(baseMessage.data.ToString());

                    }
                    else if (baseMessage.group.Contains("futures/order"))
                    {
                        UpdateMyOrder(baseMessage.data.ToString());
                    }
                    else
                    {
                        SendLogMessage("Unknown message: " + baseMessage.group, LogMessageType.Error);
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
            List<MyTrade> trades = GetTradesForOrder(order);

            if (trades == null)
                return;

            for (int i = 0; i < trades.Count; i++)
            {
                MyTradeEvent?.Invoke(trades[i]);
            }
        }

        private void UpdateMyOrder(string data)
        {
            //SendLogMessage("UpDateMyOrder: " + data, LogMessageType.Connect);

            BitMartOrderActions baseOrderActions =
                JsonConvert.DeserializeAnonymousType(data, new BitMartOrderActions());

            if (baseOrderActions == null || baseOrderActions.Count == 0)
            {
                return;
            }

            for (int k = 0; k < baseOrderActions.Count; k++)
            {
                BitMartOrderAction baseOrderAction = baseOrderActions[k];

                Order order = ConvertToOsEngineOrder(baseOrderAction.order, baseOrderAction.action);

                if (order == null)
                {
                    return;
                }

                MyOrderEvent?.Invoke(order);

                if (MyTradeEvent != null &&
                    (order.State == OrderStateType.Done || order.State == OrderStateType.Partial))
                {
                    UpdateTrades(order);
                }

            }
        }

        private Order ConvertToOsEngineOrder(BitMartOrder baseOrder, int action)
        {
            Order order = new Order();

            order.SecurityNameCode = baseOrder.symbol;
            if (baseOrder.symbol.EndsWith("USDT"))
            {
                order.SecurityClassCode = "USDT";
            }
            else if (baseOrder.symbol.EndsWith("USD"))
            {
                order.SecurityClassCode = "USD";
            }
            order.Volume = baseOrder.size.ToDecimal();
            order.VolumeExecute = baseOrder.deal_size.ToDecimal();

            order.PortfolioNumber = this._portfolioName;

            if (baseOrder.type == "limit")
            {
                order.Price = baseOrder.price.ToDecimal();
                order.TypeOrder = OrderPriceType.Limit;
            }
            else if (baseOrder.type == "market")
            {
                order.TypeOrder = OrderPriceType.Market;
            }

            try
            {
                order.NumberUser = Convert.ToInt32(baseOrder.client_order_id);
            }
            catch
            {
                //SendLogMessage("strage order num: " + baseOrder.client_order_id, LogMessageType.Error);
                //return null;
            }

            order.NumberMarket = baseOrder.order_id;
            order.ServerType = ServerType.BitMartFutures;

            order.TimeCreate = ConvertToDateTimeFromUnixFromMilliseconds(baseOrder.create_time.ToString());
            order.TimeCallBack = ConvertToDateTimeFromUnixFromMilliseconds(baseOrder.update_time.ToString());

            SetOrderSide(order, baseOrder.side);

            //Action
            //- 1 = match deal
            //- 2 = submit order
            //- 3 = cancel order
            //- 4 = liquidate cancel order
            //- 5 = adl cancel order
            //- 6 = part liquidate
            //- 7 = bankruptcy order
            //- 8 = passive adl match deal
            //- 9 = active adl match deal

            if (action == 2)
            {
                order.State = OrderStateType.Active;
            }
            else if (action == 1)
            {
                order.State = OrderStateType.Done;
            }
            else
            {

                if (string.IsNullOrEmpty(baseOrder.deal_size))
                {
                    order.State = OrderStateType.Cancel;
                }
                else
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
            }

            return order;
        }

        private void UpdateMyPortfolio(string data)
        {
            BitMartBalanceDetail balanceDetail =
                JsonConvert.DeserializeAnonymousType(data, new BitMartBalanceDetail());

            Portfolio portf = null;
            if (_myPortfolious != null && _myPortfolious.Count > 0)
            {
                portf = _myPortfolious[0];
            }

            if (portf == null)
            {
                return;
            }

            List<PositionOnBoard> positions = portf.GetPositionOnBoard();

            for (int i = 0; i < positions.Count; i++)
            {
                PositionOnBoard position = positions[i];
                if (position.SecurityNameCode != balanceDetail.currency)
                {
                    continue;
                }

                position.ValueCurrent = balanceDetail.available_balance.ToDecimal();
                position.ValueBlocked = balanceDetail.frozen_balance.ToDecimal();

                portf.SetNewPosition(position);
            }


            if (PortfolioEvent != null)
            {
                PortfolioEvent(_myPortfolious);
            }
        }

        private void UpdateMyPositions(string data)
        {
            BitMartPositions basePositions =
                JsonConvert.DeserializeAnonymousType(data, new BitMartPositions());

            Portfolio portf = null;
            if (_myPortfolious != null && _myPortfolious.Count > 0)
            {
                portf = _myPortfolious[0];
            }

            if (portf == null)
            {
                return;
            }

            List<PositionOnBoard> positions = portf.GetPositionOnBoard();

            for (int k = 0; k < basePositions.Count; k++)
            {
                BitMartPosition basePos = basePositions[k];

                string name = basePos.symbol;
                decimal volume = basePos.hold_volume.ToDecimal();
                if (basePos.position_type == 1)
                {
                    name += "_LONG";
                }
                else
                {
                    name += "_SHORT";
                    volume = -volume;
                }

                bool found = false;

                for (int i = 0; i < positions.Count; i++)
                {
                    PositionOnBoard position = positions[i];
                    if (position.SecurityNameCode != name)
                    {
                        continue;
                    }

                    found = true;

                    position.ValueCurrent = volume;
                    position.ValueBlocked = basePos.frozen_volume.ToDecimal();

                    portf.SetNewPosition(position);
                }

                if (!found)
                {
                    PositionOnBoard newPos = new PositionOnBoard()
                    {
                        PortfolioName = this._portfolioName,
                        SecurityNameCode = name,
                        ValueCurrent = volume,
                        ValueBlocked = basePos.frozen_volume.ToDecimal(),
                        ValueBegin = 0
                    };

                    portf.SetNewPosition(newPos);
                }

            }



            PortfolioEvent?.Invoke(_myPortfolious);

        }


        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(300));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(300));

        private RateGate _rateGateGetOrder = new RateGate(1, TimeSpan.FromMilliseconds(300));

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string endPoint = "/contract/private/submit-order";

                NewOrderBitMartRequest body = GetOrderRequestObj(order);
                string bodyStr = JsonConvert.SerializeObject(body);
                //SendLogMessage("Order New: " + bodyStr, LogMessageType.Connect);
                HttpResponseMessage response = _restClient.Post(endPoint, bodyStr, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;
                BitMartBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string order_id = null;

                    if (parsed != null && parsed.data != null)
                    {

                        NewOrderBitMartResponce parsed_data =
        JsonConvert.DeserializeAnonymousType(parsed.data.ToString(), new NewOrderBitMartResponce());

                        if (parsed_data != null && parsed_data.order_id != 0)
                        {
                            //Everything is OK
                            order_id = parsed_data.order_id.ToString();
                            _serverOrderIDs.TryAdd(order.NumberUser.ToString(), order_id);
                        }
                    }

                    if (string.IsNullOrEmpty(order_id))
                    {
                        SendLogMessage($"Order creation answer is wrong: {content}", LogMessageType.Error);

                        if (content.Contains("You do not have the permissions to perform this operation. "))
                        {
                            CreateOrderFail(order);
                        }
                    }
                }
                else
                {
                    string message = content;
                    if (parsed != null && parsed.message != null)
                    {
                        message = parsed.message;
                    }

                    SendLogMessage("Order Fail. Status: "
                        + response.StatusCode + "  " + order.SecurityNameCode + ", " + message, LogMessageType.Error);

                    CreateOrderFail(order);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private NewOrderBitMartRequest GetOrderRequestObj(Order order)
        {
            NewOrderBitMartRequest requestObj = new NewOrderBitMartRequest();

            if (order.Side == Side.Buy)
            {
                if (order.PositionConditionType == OrderPositionConditionType.Close)
                {
                    requestObj.side = 2; // close short
                }
                else
                {
                    requestObj.side = 1; // open long
                }
            }
            else
            {
                if (order.PositionConditionType == OrderPositionConditionType.Close)
                {
                    requestObj.side = 3; // close long
                }
                else
                {
                    requestObj.side = 4; // open short
                }
            }

            if (order.TypeOrder == OrderPriceType.Limit)
            {
                requestObj.type = "limit";
                requestObj.price = order.Price.ToString().Replace(',', '.');
            }
            else if (order.TypeOrder == OrderPriceType.Market)
            {
                requestObj.type = "market";
            }
            requestObj.open_type = "cross";
            requestObj.symbol = order.SecurityNameCode;
            requestObj.size = (int)order.Volume;
            requestObj.client_order_id = order.NumberUser.ToString();

            return requestObj;
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            MyOrderEvent?.Invoke(order);
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            //unsupported by API
        }

        public bool CancelOrder(Order order)
        {
            string order_id = GetServerOrderId(order);

            if (string.IsNullOrEmpty(order_id))
            {
                return false;
            }

            _rateGateCancelOrder.WaitToProceed();

            try
            {
                string endPoint = "/contract/private/cancel-order";

                CancelOrderBitMartRequest body = new CancelOrderBitMartRequest();
                body.order_id = order_id;
                body.symbol = order.SecurityNameCode;

                string bodyStr = JsonConvert.SerializeObject(body);
                //SendLogMessage("Order Cancel: " + bodyStr, LogMessageType.Connect);
                HttpResponseMessage response = _restClient.Post(endPoint, bodyStr, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;
                BitMartBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());


                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (parsed != null && parsed.data != null)
                    {
                        //Everything is OK - do nothing
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Cancel order, answer is wrong: {content}", LogMessageType.Error);
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    OrderStateType state = GetOrderStatus(order);

                    if (state == OrderStateType.None)
                    {
                        string message = content;

                        if (parsed != null && parsed.message != null)
                        {
                            message = parsed.message;
                        }

                        SendLogMessage("Cancel order failed. Status: "
                            + response.StatusCode + "  " + order.SecurityNameCode + ", " + message, LogMessageType.Error);

                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }

            }
            catch (Exception exception)
            {
                SendLogMessage("Cancel order error." + exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public void GetOrdersState(List<Order> orders)
        {
            if (orders == null && orders.Count == 0)
            {
                return;
            }

            List<Order> actualOrders = GetOpenOrdersFromExchange();

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];
                bool found = false;
                for (int j = 0; j < actualOrders.Count; j++)
                {
                    if (actualOrders[j].SecurityNameCode != order.SecurityNameCode)
                    {
                        continue;
                    }

                    order.State = actualOrders[j].State;
                    found = true;
                    break;
                }

                if (!found)
                {
                    order.State = OrderStateType.Cancel;
                }
            }

        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetOpenOrdersFromExchange();

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
            List<Order> orders = GetOpenOrdersFromExchange();

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

        private List<Order> GetOpenOrdersFromExchange()
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string endPoint = "/contract/private/get-open-orders";

                HttpResponseMessage response = _restClient.Get(endPoint, secured: true);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content.ReadAsStringAsync().Result;
                    //SendLogMessage("GetAllOrdersFromExchange resp: " + content, LogMessageType.Connect);
                    BitMartBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());

                    if (parsed != null && parsed.data != null)
                    {
                        //Everything is OK
                        BitMartRestOrders baseOrders = JsonConvert.DeserializeAnonymousType(parsed.data.ToString(), new BitMartRestOrders());

                        List<Order> osEngineOrders = new List<Order>();

                        for (int i = 0; i < baseOrders.Count; i++)
                        {
                            Order order = ConvertRestOrdersToOsEngineOrder(baseOrders[i], false);
                            if (order == null)
                            {
                                continue;
                            }

                            osEngineOrders.Add(order);
                            _serverOrderIDs.TryAdd(order.NumberUser.ToString(), order.NumberMarket);
                        }

                        return osEngineOrders;

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
                        SendLogMessage("Fail reasons: "
                      + response.Content, LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        // map: client_order_id -> server_order_id
        ConcurrentDictionary<string, string> _serverOrderIDs = new ConcurrentDictionary<string, string>();

        private string GetServerOrderId(Order order)
        {
            string order_id = order.NumberMarket;
            if (string.IsNullOrEmpty(order_id))
            {
                if (_serverOrderIDs.TryGetValue(order.NumberUser.ToString(), out order_id))
                    return order_id;

                //refresh open order IDs
                GetOpenOrdersFromExchange();

                if (_serverOrderIDs.TryGetValue(order.NumberUser.ToString(), out order_id))
                    return order_id;

                //search in history orders
                Order marketOrder = GetOrderFromExchange(order.NumberUser.ToString(), order.SecurityNameCode);
                if (marketOrder != null)
                {
                    return order.NumberMarket;
                }

                SendLogMessage($"Failed to get server order_id " + order.NumberUser, LogMessageType.Error);
            }

            return order_id;
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
                DateTime endTime = DateTime.Now.ToUniversalTime();
                string endPoint = "/contract/private/order-history?symbol=" + symbol;
                endPoint += "&start_time=" + TimeManager.GetTimeStampSecondsToDateTime(endTime.AddDays(-1));
                endPoint += "&end_time=" + TimeManager.GetTimeStampSecondsToDateTime(endTime.AddDays(1));

                //SendLogMessage("Request Orders: " + endPoint, LogMessageType.Connect);

                HttpResponseMessage response = _restClient.Get(endPoint, secured: true);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content.ReadAsStringAsync().Result;
                    //SendLogMessage("GetOrderFromExchange resp: " + content, LogMessageType.Connect);
                    BitMartBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());

                    if (parsed != null && parsed.data != null)
                    {
                        //Everything is OK
                        BitMartRestOrders baseOrders = JsonConvert.DeserializeAnonymousType(parsed.data.ToString(), new BitMartRestOrders());

                        Order outOrder = null;

                        for (int i = 0; i < baseOrders.Count; i++)
                        {
                            if (baseOrders[i] == null)
                            {
                                continue;
                            }

                            Order order = ConvertRestOrdersToOsEngineOrder(baseOrders[i], true);
                            if (order == null || order.NumberUser == 0)
                            {
                                continue;
                            }

                            _serverOrderIDs.TryAdd(order.NumberUser.ToString(), order.NumberMarket);

                            if (order.NumberUser.ToString() == userOrderId)
                            {
                                outOrder = order;
                            }
                        }

                        if (outOrder == null)
                        {
                            SendLogMessage("Order not found: " + userOrderId, LogMessageType.Error);
                        }


                        return outOrder;

                    }

                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    SendLogMessage("Order not found: " + userOrderId, LogMessageType.Connect);
                    return null;
                }
                else
                {
                    SendLogMessage("Get order request error: " + response.ReasonPhrase + ",  " + response.ToString(), LogMessageType.Error);

                    if (response.Content != null)
                    {
                        SendLogMessage("Fail reasons: " + response.Content, LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get order request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private Order GetOrderFromExchangeByID(string serverOrderId, string symbol)
        {
            _rateGateGetOrder.WaitToProceed();

            if (string.IsNullOrEmpty(serverOrderId))
            {
                SendLogMessage("Server Order ID is empty", LogMessageType.Connect);
                return null;
            }

            try
            {
                DateTime endTime = DateTime.Now.ToUniversalTime();
                string endPoint = "/contract/private/order?symbol=" + symbol;
                endPoint += "&order_id=" + serverOrderId.ToString();

                //SendLogMessage("Request Order: " + endPoint, LogMessageType.Connect);

                HttpResponseMessage response = _restClient.Get(endPoint, secured: true);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content.ReadAsStringAsync().Result;
                    //SendLogMessage("GetOrderFromExchangeByID resp: " + content, LogMessageType.Connect);
                    BitMartBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());

                    if (parsed != null && parsed.data != null)
                    {
                        //Everything is OK
                        BitMartRestOrder baseOrder = JsonConvert.DeserializeAnonymousType(parsed.data.ToString(), new BitMartRestOrder());

                        Order order = ConvertRestOrdersToOsEngineOrder(baseOrder, false);
                        if (order == null)
                        {
                            SendLogMessage("Order not found: " + serverOrderId, LogMessageType.Error);
                        }
                        else
                        {
                            _serverOrderIDs.TryAdd(order.NumberUser.ToString(), order.NumberMarket);
                        }

                        return order;
                    }

                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    SendLogMessage("Order not found: " + serverOrderId, LogMessageType.Connect);
                    return null;
                }
                else
                {
                    SendLogMessage("Get order request error: " + response.ReasonPhrase + ",  " + response.ToString(), LogMessageType.Error);

                    if (response.Content != null)
                    {
                        SendLogMessage("Fail reasons: " + response.Content, LogMessageType.Error);
                    }
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
            List<Order> ordersOnBoard = GetOpenOrdersFromExchange();
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
            string serverOrderId = GetServerOrderId(order);
            if (serverOrderId == null)
            {
                SendLogMessage("Fail to get server order_id for user order_id=" + order.NumberUser, LogMessageType.Error);
            }

            Order myOrder = GetOrderFromExchangeByID(serverOrderId, order.SecurityNameCode);
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

        private void SetOrderSide(Order order, int exchangeSide)
        {
            /*
             Order side
                -1=buy_open_long
                -2=buy_close_short
                -3=sell_close_long
                -4=sell_open_short
             */
            order.PositionConditionType = OrderPositionConditionType.Open;

            if (exchangeSide == 1 || exchangeSide == 2)
            {
                order.Side = Side.Buy;

                if (exchangeSide == 2)
                {
                    order.PositionConditionType = OrderPositionConditionType.Close;
                }
            }
            else
            {
                order.Side = Side.Sell;

                if (exchangeSide == 3)
                {
                    order.PositionConditionType = OrderPositionConditionType.Close;
                }
            }
        }

        private Order ConvertRestOrdersToOsEngineOrder(BitMartRestOrder baseOrder, bool from_history)
        {
            Order order = new Order();

            order.SecurityNameCode = baseOrder.symbol;
            order.Volume = baseOrder.size.ToDecimal();
            order.VolumeExecute = baseOrder.deal_size.ToDecimal();
            order.PortfolioNumber = this._portfolioName;

            /*
             * Order type
                -limit
                - market
                - liquidate
                - bankruptcy
                -adl
             */

            if (baseOrder.type == "limit")
            {
                order.Price = baseOrder.price.ToDecimal();
                order.TypeOrder = OrderPriceType.Limit;
            }
            else if (baseOrder.type == "market")
            {
                order.TypeOrder = OrderPriceType.Market;
            }
            else
            {
                //unknown type
            }

            try
            {
                order.NumberUser = Convert.ToInt32(baseOrder.client_order_id);
            }
            catch
            {
                order.NumberUser = 0;
            }

            order.NumberMarket = baseOrder.order_id;

            order.TimeCallBack = ConvertToDateTimeFromUnixFromMilliseconds(baseOrder.update_time.ToString());

            SetOrderSide(order, baseOrder.side);

            //Order status
            //    -1 = status_approval
            //    - 2 = status_check
            //    - 4 = status_finish
            if (order.Volume == order.VolumeExecute)
            {
                order.State = OrderStateType.Done;
            }
            else if (order.VolumeExecute == 0m)
            {
                if (from_history || baseOrder.state == 4)
                {
                    order.State = OrderStateType.Cancel;
                }
                else
                {
                    order.State = OrderStateType.Active;
                }
            }
            else //  (order.Volume != order.VolumeExecute)
            {
                order.State = OrderStateType.Partial;
            }





            return order;
        }

        private List<MyTrade> GetTradesForOrder(Order order)
        {
            _rateGateGetOrder.WaitToProceed();

            //BitMart don't provide API to get order by client_order_id, only by server order_id
            //Plan
            //1. get client_order_id by orderId (search in order history)
            string serverOrderId = GetServerOrderId(order);

            //2. get trades, filter by orderId  (search in trade history)
            try
            {
                DateTime curTime = DateTime.Now.ToUniversalTime();

                string endPoint = $"/contract/private/trades?symbol=" + order.SecurityNameCode;
                endPoint += "&start_time=" + TimeManager.GetTimeStampSecondsToDateTime(curTime.AddHours(-24));
                endPoint += "&end_time=" + TimeManager.GetTimeStampSecondsToDateTime(curTime.AddHours(10));

                //SendLogMessage("Order trades: " + endPoint, LogMessageType.Connect);
                HttpResponseMessage response = _restClient.Get(endPoint, secured: true);
                string content = response.Content.ReadAsStringAsync().Result;

                //SendLogMessage("Order trades resp: " + content, LogMessageType.Connect);

                BitMartBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (parsed != null && parsed.data != null && parsed.data != null)
                    {
                        BitMartTrades baseTrades =
                            JsonConvert.DeserializeAnonymousType(parsed.data.ToString(), new BitMartTrades());

                        List<MyTrade> trades = new List<MyTrade>();

                        for (int i = 0; i < baseTrades.Count; i++)
                        {
                            if (baseTrades[i] == null || baseTrades[i].order_id != serverOrderId)
                            {
                                continue;
                            }

                            MyTrade trade = ConvertRestTradeToOsEngineTrade(baseTrades[i]);
                            trades.Add(trade);
                        }

                        return trades;
                    }
                }
                else
                {
                    string message = "";
                    if (parsed != null)
                    {
                        message = parsed.message;
                    }
                    SendLogMessage("Order trade request error. Status: "
                        + response.StatusCode + "  " + order.NumberUser +
                        ", " + message, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order trade request error " + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private MyTrade ConvertRestTradeToOsEngineTrade(BitMartTrade baseTrade)
        {
            MyTrade trade = new MyTrade();
            trade.NumberOrderParent = baseTrade.order_id;
            trade.NumberTrade = baseTrade.trade_id;
            trade.SecurityNameCode = baseTrade.symbol;
            trade.Time = ConvertToDateTimeFromUnixFromMilliseconds(baseTrade.create_time.ToString());
            if (baseTrade.side <= 2)
            {
                trade.Side = Side.Buy;
            }
            else
            {
                trade.Side = Side.Sell;
            }
            trade.Price = baseTrade.price.ToDecimal();
            trade.Volume = baseTrade.vol.ToDecimal();

            return trade;
        }

        #endregion

        #region 12 Helpers

        public string GenerateSignature(string timestamp, string body)
        {
            string message = $"{timestamp}#{_memo}#{body}";
            using HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();

            //byte[] keyBytes = Encoding.UTF8.GetBytes(_secretKey);
            //byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            //// Вычисляем HMAC-SHA256
            //using (var hmac = new HMACSHA256(keyBytes))
            //{
            //    byte[] hashBytes = hmac.ComputeHash(messageBytes);

            //    // Конвертируем хеш в HEX-строку
            //    StringBuilder hexBuilder = new StringBuilder(hashBytes.Length * 2);
            //    foreach (byte b in hashBytes)
            //    {
            //        hexBuilder.AppendFormat("{0:x2}", b);
            //    }
            //    return hexBuilder.ToString();
            //}

        }

        private DateTime ConvertToDateTimeFromUnixFromSeconds(string seconds)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime result = origin.AddSeconds(seconds.ToDouble()).ToLocalTime();

            return result;
        }

        private DateTime ConvertToDateTimeFromUnixFromMilliseconds(string seconds)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime result = origin.AddMilliseconds(seconds.ToDouble());

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