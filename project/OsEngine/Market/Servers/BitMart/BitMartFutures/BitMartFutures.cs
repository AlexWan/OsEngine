/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitMartFutures.Json;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;



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
            CreateParameterBoolean("Hedge Mode", true);
            CreateParameterBoolean("Extended Data", false);
            ServerParameters[3].ValueChange += BitMartFuturesServer_ValueChange;

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label271;
            ServerParameters[3].Comment = OsLocalization.Market.Label250;
            ServerParameters[4].Comment = OsLocalization.Market.Label270;
        }

        private void BitMartFuturesServer_ValueChange()
        {
            ((BitMartFuturesServerRealization)ServerRealization).HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;
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

            Thread worker2 = new Thread(MessageReaderPublic);
            worker2.Name = "MessageReaderPublicBitMartFutures";
            worker2.Start();

            Thread worker3 = new Thread(MessageReaderPrivate);
            worker3.Name = "MessageReaderPrivateBitMartFutures";
            worker3.Start();

            Thread threadGetPortfolios = new Thread(ThreadGetPortfolios);
            threadGetPortfolios.Name = "ThreadBitMartFuturesPortfolios";
            threadGetPortfolios.Start();

            Thread threadExtendedData = new Thread(ThreadExtendedData);
            threadExtendedData.IsBackground = true;
            threadExtendedData.Name = "ThreadBitGetFuturesExtendedData";
            threadExtendedData.Start();
        }

        public void Connect(WebProxy proxy = null)
        {

            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _memo = ((ServerParameterString)ServerParameters[2]).Value;
            HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;

            if (string.IsNullOrEmpty(_publicKey)
                || string.IsNullOrEmpty(_secretKey)
                || string.IsNullOrEmpty(_memo))
            {
                SendLogMessage("Connection terminated. You must specify the public and private keys. You can get it on the BitMartFutures website",
                    LogMessageType.Error);
                return;
            }

            if (((ServerParameterBool)ServerParameters[4]).Value == true)
            {
                _extendedMarketData = true;
            }
            else
            {
                _extendedMarketData = false;
            }

            try
            {
                RestRequest requestRest = new RestRequest("/system/time", Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
                }
                else
                {
                    SendLogMessage("Connection can be open. BitMartFutures. Error request", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
                Disconnect();
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
                DeleteWebSocketConnection();

                SendLogMessage("Dispose. Connection Closed by BitMartFutures. WebSocket Closed Event", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
            _subscribedSecurities.Clear();
            _securities = new List<Security>();
            Disconnect();
        }

        public void Disconnect()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.BitMartFutures;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        private string _publicKey;

        private string _secretKey;

        private string _memo;

        private string _baseUrl = "https://api-cloud-v2.bitmart.com";

        public List<IServerParameter> ServerParameters { get; set; }

        public bool HedgeMode
        {
            get { return _hedgeMode; }
            set
            {
                if (value == _hedgeMode)
                {
                    return;
                }
                _hedgeMode = value;

                SetPositionMode();
            }
        }

        private bool _hedgeMode;

        private bool _extendedMarketData;

        public void SetPositionMode()
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                Dictionary<string, string> mode = new Dictionary<string, string>();
                mode["position_mode"] = "one_way_mode";

                if (HedgeMode)
                {
                    mode["position_mode"] = "hedge_mode";
                }

                string jsonRequest = JsonConvert.SerializeObject(mode);
                string path = "/contract/private/set-position-mode";
                IRestResponse response = CreatePrivateQuery(path, Method.POST, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {

                }
                else
                {
                    SendLogMessage($"PositionMode error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 3 Securities

        private List<Security> _securities = new List<Security>();

        private RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public void GetSecurities()
        {
            UpdateSecurity();

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                if (SecurityEvent != null)
                {
                    SecurityEvent.Invoke(_securities);
                }
            }
        }

        private void UpdateSecurity()
        {
            _rateGateSecurity.WaitToProceed();

            try
            {
                RestRequest requestRest = new RestRequest("/contract/public/details", Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitMartSecurityRest symbolsResponse = JsonConvert.DeserializeAnonymousType(response.Content, new BitMartSecurityRest());

                    if (symbolsResponse.code == "1000")
                    {
                        for (int i = 0; i < symbolsResponse.data.symbols.Count; i++)
                        {
                            BitMartSymbol item = symbolsResponse.data.symbols[i];

                            if (item.status != "Trading")
                            {
                                continue;
                            }

                            Security newSecurity = new Security();

                            newSecurity.Name = item.symbol;
                            newSecurity.NameFull = item.symbol;
                            newSecurity.NameClass = item.quote_currency;
                            newSecurity.NameId = item.symbol + "_" + item.last_price;
                            newSecurity.State = SecurityStateType.Activ;
                            newSecurity.Decimals = item.price_precision.DecimalsCount();
                            newSecurity.DecimalsVolume = item.contract_size.DecimalsCount();
                            newSecurity.PriceStep = item.price_precision.ToDecimal();
                            newSecurity.PriceStepCost = newSecurity.PriceStep;
                            newSecurity.Lot = 1;
                            newSecurity.SecurityType = SecurityType.Futures;
                            newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                            newSecurity.Exchange = ServerType.BitMartFutures.ToString();
                            newSecurity.MinTradeAmount = item.contract_size.ToDecimal();
                            newSecurity.VolumeStep = item.contract_size.ToDecimal();

                            _securities.Add(newSecurity);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Securities error. Code:{symbolsResponse.code} || msg: {symbolsResponse.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Securities error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities request error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _portfolios;

        private string _portfolioName = "BitMartFutures";

        private bool _portfolioIsStarted = false;

        public void GetPortfolios()
        {
            if (_securities.Count == 0)
            {
                GetSecurities();
            }

            if (_portfolios == null)
            {
                GetNewPortfolio();
            }

            GetCurrentPortfolio(true);
            _portfolioIsStarted = true;
        }

        private void GetNewPortfolio()
        {
            _portfolios = new List<Portfolio>();

            Portfolio portfolioInitial = new Portfolio();
            portfolioInitial.Number = this._portfolioName;
            portfolioInitial.ValueBegin = 1;
            portfolioInitial.ValueCurrent = 1;
            portfolioInitial.ValueBlocked = 0;

            _portfolios.Add(portfolioInitial);

            PortfolioEvent(_portfolios);
        }

        private void ThreadGetPortfolios()
        {
            Thread.Sleep(15000);

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                    continue;
                }

                try
                {
                    Thread.Sleep(5000);

                    if (_portfolioIsStarted == false)
                    {
                        continue;
                    }

                    if (_portfolios == null)
                    {
                        GetNewPortfolio();
                    }

                    GetCurrentPortfolio(false);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }
        }

        private void GetCurrentPortfolio(bool IsUpdateValueBegin)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string path = $"/contract/private/assets-detail";

                IRestResponse response = CreatePrivateQuery(path, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitMartPortfolioRest portfolioResponse = JsonConvert.DeserializeAnonymousType(response.Content, new BitMartPortfolioRest());

                    if (portfolioResponse.code == "1000")
                    {
                        if (_portfolios == null)
                        {
                            return;
                        }

                        Portfolio portfolio = _portfolios[0];

                        decimal positionInUSDT = 0;
                        decimal positionPnL = 0;
                        decimal positionBlocked = 0;

                        for (int i = 0; i < portfolioResponse.data.Count; i++)
                        {
                            BalanceData item = portfolioResponse.data[i];

                            if (item.currency == "USDT")
                            {
                                positionInUSDT = item.available_balance.ToDecimal();
                            }
                            else if (item.currency == "USDC"
                                || item.currency == "BTC"
                                || item.currency == "ETH")
                            {
                                positionInUSDT += GetPriceSecurity(item.currency + "USDT") * item.available_balance.ToDecimal();
                            }

                            PositionOnBoard pos = new PositionOnBoard();

                            pos.PortfolioName = this._portfolioName;
                            pos.SecurityNameCode = item.currency;
                            pos.ValueBlocked = Math.Round(item.frozen_balance.ToDecimal(), 5);
                            pos.ValueCurrent = Math.Round(item.available_balance.ToDecimal(), 5);
                            pos.UnrealizedPnl = Math.Round(item.unrealized.ToDecimal(), 5);
                            positionPnL += pos.UnrealizedPnl;
                            positionBlocked += pos.ValueBlocked;
                            portfolio.SetNewPosition(pos);
                        }

                        if (IsUpdateValueBegin)
                        {
                            portfolio.ValueBegin = Math.Round(positionInUSDT, 5);
                        }

                        portfolio.ValueCurrent = Math.Round(positionInUSDT, 5);
                        portfolio.UnrealizedPnl = positionPnL;
                        portfolio.ValueBlocked = positionBlocked;

                        if (PortfolioEvent != null)
                        {
                            PortfolioEvent(_portfolios);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Portfolio error. Code:{portfolioResponse.code} || msg: {portfolioResponse.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Portfolio request error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
            }
        }

        private decimal GetPriceSecurity(string security)
        {
            for (int i = 0; i < _securities.Count; i++)
            {
                if (security == _securities[i].Name)
                {
                    string price = _securities[i].NameId.ToString().Split('_')[1];

                    return price.ToDecimal();
                }
            }

            return 0;
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

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

        private readonly HashSet<int> _allowedTf = new HashSet<int> {
            1, 3, 5, 15, 30, 60, 120, 240, 360, 720, 1440, 10080};

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
                        DateTime startTime, DateTime endTime, DateTime actualTime)
        {

            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!_allowedTf.Contains(tfTotalMinutes))
            {
                return null;
            }

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            List<Candle> candles = new List<Candle>();

            // 500 - max candles at BitMart
            TimeSpan additionTime = TimeSpan.FromMinutes(tfTotalMinutes * 500);

            DateTime endTimeReal = startTime.Add(additionTime);

            while (startTime < endTime)
            {
                List<Candle> newCandles = GetHistoryCandle(security, tfTotalMinutes, startTime, endTimeReal);
                // List<Candle> newCandles = ConvertToOsEngineCandles(history);

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

        private List<Candle> GetHistoryCandle(Security security, int tfTotalMinutes,
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

            _rateGateSendOrder.WaitToProceed();

            try
            {
                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitMartCandlesHistory candlesResponse = JsonConvert.DeserializeAnonymousType(response.Content, new BitMartCandlesHistory());

                    if (candlesResponse.code == "1000")
                    {
                        List<Candle> result = new List<Candle>();

                        for (int i = 0; i < candlesResponse.data.Count; i++)
                        {
                            BitMartCandle curCandle = candlesResponse.data[i];

                            Candle newCandle = new Candle();
                            newCandle.Open = curCandle.open_price.ToDecimal();
                            newCandle.High = curCandle.high_price.ToDecimal();
                            newCandle.Low = curCandle.low_price.ToDecimal();
                            newCandle.Close = curCandle.close_price.ToDecimal();
                            newCandle.Volume = curCandle.volume.ToDecimal();
                            newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(curCandle.timestamp));

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
                    else
                    {
                        SendLogMessage($"Candles request error. Code:{candlesResponse.code} || msg: {candlesResponse.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Candles error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Candles request error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
            }
            return null;
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

        private string _webSocketUrlPublic = "wss://openapi-ws-v2.bitmart.com/api?protocol=1.1";

        private string _webSocketUrlPrivate = "wss://openapi-ws-v2.bitmart.com/user?protocol=1.1";

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
                webSocketPublicNew.ConnectAsync();

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
                _webSocketPrivate.ConnectAsync();
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

                    SetPositionMode();
                }
            }
        }

        private void CreateAuthMessageWebSocKet()
        {
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string sign = GenerateSignature(timestamp, "bitmart.WebSocket");

            _webSocketPrivate.SendAsync($"{{\"action\": \"access\", \"args\": [\"{_publicKey}\", \"{timestamp}\", \"{sign}\",\"web\"]}}");
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
                SendLogMessage("WebSocket public error" + ex.ToString(), LogMessageType.Error);
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
                        SubscribePrivate();
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
                SendLogMessage("WebSocket private error. " + error.ToString(), LogMessageType.Error);
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
                Thread.Sleep(15000);

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
                            webSocketPublic.SendAsync("{\"action\":\"ping\"}");
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
                        _webSocketPrivate.SendAsync("{\"action\":\"ping\"}");
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

        #region 9 WebSocket Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<Security> _subscribedSecurities = new List<Security>();

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].NameClass == security.NameClass
                    && _subscribedSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }

                _subscribedSecurities.Add(security);

                if (_webSocketPublic.Count == 0)
                {
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 40 == 0)
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
                    webSocketPublic.SendAsync($" {{ \"action\":\"subscribe\", \"args\":[\"futures/trade:{security.Name}\"]}}");
                    webSocketPublic.SendAsync($"{{ \"action\":\"subscribe\",\"args\":[\"futures/depth20:{security.Name}@100ms\"]}}");

                    if (_extendedMarketData)
                    {
                        webSocketPublic.SendAsync($" {{ \"action\":\"subscribe\", \"args\":[\"futures/fundingRate:{security.Name}\"]}}");
                        GetFundingHistory(security.Name);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void GetFundingHistory(string name)
        {
            _rateGateSecurity.WaitToProceed();

            try
            {
                RestRequest requestRest = new RestRequest($"/contract/public/funding-rate-history?symbol={name}&limit=5", Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitMartBaseMessage<FundingItem> responseFunding = JsonConvert.DeserializeAnonymousType(response.Content, new BitMartBaseMessage<FundingItem>());

                    if (responseFunding.code == "1000")
                    {
                        FundingItemHistory item = responseFunding.data.list[0];

                        Funding data = new Funding();

                        data.SecurityNameCode = item.symbol;
                        data.PreviousFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)item.funding_time.ToDecimal());

                        FundingUpdateEvent?.Invoke(data);
                    }
                    else
                    {
                        SendLogMessage($"FundingHistory error. Code:{responseFunding.code} || msg: {responseFunding.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"FundingHistory error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"FundingHistory request error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void SubscribePrivate()
        {
            try
            {
                _webSocketPrivate.SendAsync($"{{\"action\": \"subscribe\",\"args\":[\"futures/asset:USDT\", \"futures/asset:BTC\", \"futures/asset:ETH\", \"futures/asset:USDC\"]}}");
                _webSocketPrivate.SendAsync($"{{\"action\": \"subscribe\",\"args\":[\"futures/position\"]}}");
                _webSocketPrivate.SendAsync($"{{\"action\": \"subscribe\",\"args\": [\"futures/order\"]}}");
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
                                if (_subscribedSecurities != null)
                                {
                                    for (int j = 0; j < _subscribedSecurities.Count; j++)
                                    {
                                        string securityName = _subscribedSecurities[j].Name;

                                        webSocketPublic.SendAsync($" {{ \"action\":\"unsubscribe\", \"args\":[\"futures/trade:{securityName}\"]}}");
                                        webSocketPublic.SendAsync($"{{ \"action\":\"unsubscribe\",\"args\":[\"futures/depth20:{securityName}@100ms\"]}}");

                                        if (_extendedMarketData)
                                        {
                                            webSocketPublic.SendAsync($" {{ \"action\":\"unsubscribe\", \"args\":[\"futures/fundingRate:{securityName}\"]}}");
                                        }
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
                    _webSocketPrivate.SendAsync($"{{\"action\": \"unsubscribe\",\"args\":[\"futures/asset:USDT\", \"futures/asset:BTC\", \"futures/asset:ETH\", \"futures/asset:USDC\"]}}");
                    _webSocketPrivate.SendAsync($"{{\"action\": \"unsubscribe\",\"args\":[\"futures/position\"]}}");
                    _webSocketPrivate.SendAsync($"{{\"action\": \"unsubscribe\",\"args\": [\"futures/order\"]}}");
                }
                catch
                {
                    // ignore
                }
            }
        }

        private DateTime _timeLastUpdateExtendedData = DateTime.Now;

        private List<OpenInterestData> _openInterest = new List<OpenInterestData>();

        private void ThreadExtendedData()
        {
            while (true)
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    Thread.Sleep(3000);
                    continue;
                }

                try
                {
                    if (_subscribedSecurities != null
                    && _subscribedSecurities.Count > 0
                    && _extendedMarketData)
                    {
                        if (_timeLastUpdateExtendedData.AddSeconds(20) < DateTime.Now)
                        {
                            GetExtendedData();
                            _timeLastUpdateExtendedData = DateTime.Now;
                        }
                        else
                        {
                            Thread.Sleep(1000);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }
        }

        private void GetExtendedData()
        {
            _rateGateSecurity.WaitToProceed();

            try
            {
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    RestRequest requestRest = new RestRequest("/contract/public/details?symbol=" + _subscribedSecurities[i].Name, Method.GET);
                    RestClient client = new RestClient(_baseUrl);
                    IRestResponse response = client.Execute(requestRest);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        BitMartSecurityRest symbolsResponse = JsonConvert.DeserializeAnonymousType(response.Content, new BitMartSecurityRest());

                        if (symbolsResponse.code == "1000")
                        {
                            BitMartSymbol item = symbolsResponse.data.symbols[0];

                            Funding funding = new Funding();

                            funding.SecurityNameCode = item.symbol;
                            funding.FundingIntervalHours = int.Parse(item.funding_interval_hours);

                            FundingUpdateEvent?.Invoke(funding);

                            SecurityVolumes volume = new SecurityVolumes();

                            volume.SecurityNameCode = item.symbol;
                            volume.Volume24h = item.volume_24h.ToDecimal();
                            volume.Volume24hUSDT = item.turnover_24h.ToDecimal();

                            Volume24hUpdateEvent?.Invoke(volume);

                            OpenInterestData openInterestData = new OpenInterestData();

                            openInterestData.SecutityName = item.symbol;

                            if (item.open_interest != null)
                            {
                                openInterestData.OpenInterestValue = item.open_interest;

                                bool isInArray = false;

                                for (int k = 0; k < _openInterest.Count; k++)
                                {
                                    if (_openInterest[k].SecutityName == openInterestData.SecutityName)
                                    {
                                        _openInterest[k].OpenInterestValue = openInterestData.OpenInterestValue;
                                        isInArray = true;
                                        break;
                                    }
                                }

                                if (isInArray == false)
                                {
                                    _openInterest.Add(openInterestData);
                                }
                            }
                        }
                        else
                        {
                            if (symbolsResponse.message.Contains("<HTML><HEAD>"))
                            {
                                // 
                            }
                            else
                            {
                                SendLogMessage($"ExtendedData error. Code:{symbolsResponse.code} || msg: {symbolsResponse.message}", LogMessageType.Error);
                            }
                        }
                    }
                    else
                    {
                        if (response.Content.Contains("<HTML><HEAD>"))
                        {
                            // 
                        }
                        else
                        {
                            SendLogMessage($"ExtendedData error. {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"ExtendedData request error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private void MessageReaderPublic()
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

                    SoketBaseMessage<object> baseMessage =
                        JsonConvert.DeserializeAnonymousType(message, new SoketBaseMessage<object>());

                    if (baseMessage == null
                        || string.IsNullOrEmpty(baseMessage.group))
                    {
                        continue;
                    }

                    if (baseMessage.data == null)
                    {
                        SendLogMessage("PublicWebSocket empty data: " + message, LogMessageType.Connect);
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
                    else if (baseMessage.group.Contains("/fundingRate"))
                    {
                        UpdateFundingRate(baseMessage.data.ToString());
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

        private void UpdateFundingRate(string data)
        {
            try
            {
                FundingData response = JsonConvert.DeserializeAnonymousType(data, new FundingData());

                Funding funding = new Funding();

                funding.SecurityNameCode = response.symbol;
                funding.CurrentValue = response.fundingRate.ToDecimal() * 100;
                funding.NextFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)response.nextFundingTime.ToDecimal());
                funding.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)response.ts.ToDecimal());
                funding.MinFundingRate = response.funding_lower_limit.ToDecimal();
                funding.MaxFundingRate = response.funding_upper_limit.ToDecimal();

                FundingUpdateEvent?.Invoke(funding);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateTrade(string data)
        {
            try
            {
                List<MarketTrade> baseTrades =
                JsonConvert.DeserializeAnonymousType(data, new List<MarketTrade>());

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
                    trade.Time = DateTimeOffset.Parse(baseTrade.created_at).UtcDateTime;
                    trade.Id = baseTrade.trade_id;

                    if (baseTrade.m == "true")
                    {
                        trade.Side = Side.Buy;
                    }
                    else
                    {
                        trade.Side = Side.Sell;
                    }

                    if (_extendedMarketData)
                    {
                        trade.OpenInterest = GetOpenInterestValue(trade.SecurityNameCode);
                    }

                    NewTradesEvent?.Invoke(trade);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private decimal GetOpenInterestValue(string securityNameCode)
        {
            if (_openInterest.Count == 0
                 || _openInterest == null)
            {
                return 0;
            }

            for (int i = 0; i < _openInterest.Count; i++)
            {
                if (_openInterest[i].SecutityName == securityNameCode)
                {
                    return _openInterest[i].OpenInterestValue.ToDecimal();
                }
            }

            return 0;
        }

        private readonly object _lastMarketDepthLock = new object();
        Dictionary<string, MarketDepth> _lastMarketDepth = new Dictionary<string, MarketDepth>();

        private void UpdateMarketDepth(string data)
        {
            try
            {
                MarketDepthBitMart baseDepth =
                JsonConvert.DeserializeAnonymousType(data, new MarketDepthBitMart());

                if (String.IsNullOrEmpty(baseDepth.symbol) || baseDepth.depths.Count == 0)
                {
                    return;
                }

                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = baseDepth.symbol;
                depth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(baseDepth.ms_t));

                double maxBid = 0;
                double minAsk = double.MaxValue;

                for (int i = 0; i < baseDepth.depths.Count; i++)
                {
                    MarketDepthLevelBitMart level = baseDepth.depths[i];

                    if (level == null)
                    {
                        continue;
                    }

                    MarketDepthLevel newBid = new MarketDepthLevel();
                    newBid.Price = level.price.ToDouble();

                    if (baseDepth.way == "1") //bids
                    {
                        newBid.Bid = level.vol.ToDouble();
                        depth.Bids.Add(newBid);
                        maxBid = Math.Max(newBid.Price, maxBid);
                    }
                    else //asks
                    {
                        newBid.Ask = level.vol.ToDouble();
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
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        private void MessageReaderPrivate()
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

                    SoketBaseMessage<object> baseMessage = JsonConvert.DeserializeAnonymousType(message, new SoketBaseMessage<object>());

                    if (baseMessage == null
                        || string.IsNullOrEmpty(baseMessage.group))
                    {
                        continue;
                    }

                    if (baseMessage.data == null)
                    {
                        SendLogMessage("PrivateWebSocket empty message: " + message, LogMessageType.Connect);
                        continue;
                    }

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
            try
            {
                List<BitMartOrderAction> baseOrderActions =
                JsonConvert.DeserializeAnonymousType(data, new List<BitMartOrderAction>());

                if (baseOrderActions == null || baseOrderActions.Count == 0)
                {
                    return;
                }

                for (int k = 0; k < baseOrderActions.Count; k++)
                {
                    BitMartOrderAction baseOrderAction = baseOrderActions[k];

                    Order order = ConvertToOsEngineOrder(baseOrderAction.order, Convert.ToInt32(baseOrderAction.action));

                    if (order == null)
                    {
                        return;
                    }

                    MyOrderEvent?.Invoke(order);

                    if (MyTradeEvent != null &&
                        (order.State == OrderStateType.Done || order.State == OrderStateType.Partial))
                    {
                        UpdateTrades(order);

                        //BitMartOrder baseOrder = baseOrderAction.order;

                        //MyTrade myTrade = new MyTrade();

                        //myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(baseOrder.update_time));
                        //myTrade.NumberOrderParent = baseOrder.order_id.ToString();
                        //myTrade.NumberTrade = baseOrder.last_trade.lastTradeID;
                        //myTrade.Price = baseOrder.last_trade.fillPrice.ToDecimal();
                        //myTrade.Volume = baseOrder.last_trade.fillQty.ToDecimal();
                        //myTrade.SecurityNameCode = baseOrder.symbol;
                        //SetOrderSide(order, Convert.ToInt32(baseOrder.side));

                        //MyTradeEvent(myTrade);
                    }

                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
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

            order.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(baseOrder.create_time));
            order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(baseOrder.update_time));

            SetOrderSide(order, Convert.ToInt32(baseOrder.side));

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
            if (_portfolioIsStarted == false)
            {
                return;
            }

            try
            {
                BitMartBalanceDetail balanceDetail =
                JsonConvert.DeserializeAnonymousType(data, new BitMartBalanceDetail());

                Portfolio portf = null;
                if (_portfolios != null && _portfolios.Count > 0)
                {
                    portf = _portfolios[0];
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
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMyPositions(string data)
        {
            try
            {
                List<BitMartPosition> basePositions =
                JsonConvert.DeserializeAnonymousType(data, new List<BitMartPosition>());

                Portfolio portf = null;
                if (_portfolios != null && _portfolios.Count > 0)
                {
                    portf = _portfolios[0];
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

                    if (basePos.position_mode == "hedge_mode")
                    {
                        if (basePos.position_type == "1")
                        {
                            name += "_LONG";
                        }
                        else
                        {
                            name += "_SHORT";
                            volume = -volume;
                        }
                    }

                    if (basePos.position_mode == "one_way_mode")
                    {
                        if (basePos.position_type == "1")
                        {
                            // name;
                        }
                        else
                        {
                            //name;
                            volume = -volume;
                        }
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

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent;

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

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

                IRestResponse response = CreatePrivateQuery(endPoint, Method.POST, bodyStr);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitMartBaseMessage<NewOrderBitMartResponce> parsed =
                        JsonConvert.DeserializeAnonymousType(response.Content, new BitMartBaseMessage<NewOrderBitMartResponce>());

                    if (parsed.code == "1000")
                    {
                        //Everything is OK
                        //order_id = parsed.data.order_id.ToString();
                    }
                    else
                    {
                        SendLogMessage("Order Fail. " + parsed.code + "  " + order.SecurityNameCode + ", " + parsed.message, LogMessageType.Error);
                        CreateOrderFail(order);
                    }
                }
                else
                {
                    SendLogMessage("Order Fail. Status: " + response.StatusCode + "  " + order.SecurityNameCode + ", " + response.Content, LogMessageType.Error);
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
            string order_id = order.NumberMarket;

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
                IRestResponse response = CreatePrivateQuery(endPoint, Method.POST, bodyStr);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitMartBaseMessage<object> parsed =
                        JsonConvert.DeserializeAnonymousType(response.Content, new BitMartBaseMessage<object>());

                    if (parsed.code == "1000")
                    {
                        //Everything is OK - do nothing
                        return true;
                    }
                    else
                    {
                        GetOrderStatus(order);
                        SendLogMessage($"Cancel order, answer is wrong: {parsed.code} || {parsed.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage("Cancel order failed. Status: " + response.StatusCode + "  " + order.SecurityNameCode + ", " + response.Content, LogMessageType.Error);
                    GetOrderStatus(order);
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
            List<Order> orders = GetAllOpenOrders();

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
            List<Order> orders = GetAllOpenOrders();

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

        private List<Order> GetAllOpenOrders()
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string endPoint = "/contract/private/get-open-orders";

                IRestResponse response = CreatePrivateQuery(endPoint, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitMartBaseMessage<BitMartRestOrders> parsed = JsonConvert.DeserializeAnonymousType(response.Content, new BitMartBaseMessage<BitMartRestOrders>());

                    if (parsed.code == "1000")
                    {
                        List<Order> osEngineOrders = new List<Order>();

                        for (int i = 0; i < parsed.data.Count; i++)
                        {
                            Order order = ConvertRestOrdersToOsEngineOrder(parsed.data[i], false);

                            if (order == null)
                            {
                                continue;
                            }

                            osEngineOrders.Add(order);
                        }

                        return osEngineOrders;
                    }
                    else
                    {
                        SendLogMessage($"Get all orders error. {parsed.code} || {parsed.message}", LogMessageType.Error);
                    }
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    SendLogMessage("Get all orders request error. " + response.StatusCode + ",  " + response.Content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Order> GetOrderFromExchange(string symbol)
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                DateTime endTime = DateTime.Now.ToUniversalTime();
                string endPoint = "/contract/private/order-history?symbol=" + symbol;
                //endPoint += "&start_time=" + TimeManager.GetTimeStampSecondsToDateTime(endTime.AddDays(-1));
                //endPoint += "&end_time=" + TimeManager.GetTimeStampSecondsToDateTime(endTime.AddDays(1));

                IRestResponse response = CreatePrivateQuery(endPoint, Method.GET);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitMartBaseMessage<BitMartRestOrders> parsed =
                        JsonConvert.DeserializeAnonymousType(response.Content, new BitMartBaseMessage<BitMartRestOrders>());

                    if (parsed.code == "1000")
                    {
                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < parsed.data.Count; i++)
                        {
                            if (parsed.data[i] == null)
                            {
                                continue;
                            }

                            Order order = ConvertRestOrdersToOsEngineOrder(parsed.data[i], true);
                            if (order == null || order.NumberUser == 0)
                            {
                                continue;
                            }

                            orders.Add(order);
                        }

                        return orders;
                    }
                    else
                    {
                        SendLogMessage("Get order error: " + parsed.code + ",  " + parsed.message, LogMessageType.Error);
                    }
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    SendLogMessage("Order not found: ", LogMessageType.Connect);
                    return null;
                }
                else
                {
                    SendLogMessage("Get order request error: " + response.StatusCode + ",  " + response.Content, LogMessageType.Error);
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
            List<Order> ordersOnBoard = GetAllOpenOrders();

            if (ordersOnBoard == null)
            {
                return;
            }

            for (int i = 0; i < ordersOnBoard.Count; i++)
            {
                if (ordersOnBoard[i] == null)
                {
                    continue;
                }

                if (ordersOnBoard[i].State != OrderStateType.Active
                    && ordersOnBoard[i].State != OrderStateType.Partial
                    && ordersOnBoard[i].State != OrderStateType.Pending)
                {
                    continue;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(ordersOnBoard[i]);
                }
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            List<Order> orderFromExchange = GetAllOpenOrders();

            if (orderFromExchange == null
                || orderFromExchange.Count == 0)
            {
                orderFromExchange = GetOrderFromExchange(order.SecurityNameCode);
            }

            if (orderFromExchange == null
               || orderFromExchange.Count == 0)
            {
                return OrderStateType.None;
            }

            Order orderOnMarket = null;

            for (int i = 0; i < orderFromExchange.Count; i++)
            {
                Order curOder = orderFromExchange[i];

                if (order.NumberUser != 0
                    && curOder.NumberUser != 0
                    && curOder.NumberUser == order.NumberUser)
                {
                    orderOnMarket = curOder;
                    break;
                }

                if (string.IsNullOrEmpty(order.NumberMarket) == false
                    && order.NumberMarket == curOder.NumberMarket)
                {
                    orderOnMarket = curOder;
                    break;
                }
            }

            if (orderOnMarket == null)
            {
                return OrderStateType.None;
            }

            if (orderOnMarket != null &&
                MyOrderEvent != null)
            {
                MyOrderEvent(orderOnMarket);
            }

            if (orderOnMarket.State == OrderStateType.Done
                || orderOnMarket.State == OrderStateType.Partial)
            {
                UpdateTrades(orderOnMarket);
            }

            return orderOnMarket.State;
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

            order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(baseOrder.update_time));

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

            string serverOrderId = order.NumberMarket;

            try
            {
                DateTime curTime = DateTime.Now.ToUniversalTime();

                string endPoint = $"/contract/private/trades?symbol=" + order.SecurityNameCode;
                //endPoint += "&start_time=" + TimeManager.GetTimeStampSecondsToDateTime(curTime.AddHours(-24));
                //endPoint += "&end_time=" + TimeManager.GetTimeStampSecondsToDateTime(curTime.AddHours(10));

                IRestResponse response = CreatePrivateQuery(endPoint, Method.GET);//_restClient.Get(endPoint, secured: true);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitMartBaseMessage<BitMartTrades> parsed = JsonConvert.DeserializeAnonymousType(response.Content, new BitMartBaseMessage<BitMartTrades>());

                    if (parsed.code == "1000")
                    {
                        List<MyTrade> trades = new List<MyTrade>();

                        for (int i = 0; i < parsed.data.Count; i++)
                        {
                            if (parsed.data[i] == null || parsed.data[i].order_id != serverOrderId)
                            {
                                continue;
                            }

                            MyTrade trade = ConvertRestTradeToOsEngineTrade(parsed.data[i]);
                            trades.Add(trade);
                        }

                        return trades;
                    }
                    else
                    {
                        SendLogMessage("Order trade error. Code: "
                        + parsed.code + "  " + order.NumberUser + ",  " + parsed.message, LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage("Order trade request error. Status: "
                        + response.StatusCode + "  " + order.NumberUser + ", " + response.Content, LogMessageType.Error);
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
            trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(baseTrade.create_time));
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

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        #endregion

        #region 12 Query

        private IRestResponse CreatePrivateQuery(string path, Method method, string bodyStr = null)
        {
            try
            {
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                RestRequest requestRest = new RestRequest(path, method);
                requestRest.AddHeader("X-BM-KEY", _publicKey);

                if (bodyStr != null)
                {
                    requestRest.AddParameter("application/json", bodyStr, ParameterType.RequestBody);
                }

                if (method == Method.POST)
                {
                    string signature = GenerateSignature(timestamp, bodyStr);

                    requestRest.AddHeader("X-BM-TIMESTAMP", timestamp);
                    requestRest.AddHeader("X-BM-SIGN", signature);
                }

                IRestResponse response = new RestClient(_baseUrl).Execute(requestRest);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        public string GenerateSignature(string timestamp, string body)
        {
            string message = $"{timestamp}#{_memo}#{body}";
            using HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
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

    public class OpenInterestData
    {
        public string SecutityName { get; set; }
        public string OpenInterestValue { get; set; }
    }
}