﻿/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitMart.Json;
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


namespace OsEngine.Market.Servers.BitMart
{
    public class BitMartSpotServer : AServer
    {
        public BitMartSpotServer()
        {
            BitMartServerRealization realization = new BitMartServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterString(OsLocalization.Market.Memo, "");
        }
    }

    public class BitMartServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BitMartServerRealization()
        {
            Thread worker = new Thread(ConnectionCheckThread);
            worker.Name = "CheckAliveBitMart";
            worker.Start();

            Thread worker2 = new Thread(MessageReaderPublic);
            worker2.Name = "MessageReaderPublicBitMart";
            worker2.Start();

            Thread worker3 = new Thread(MessageReaderPrivate);
            worker3.Name = "MessageReaderPrivateBitMart";
            worker3.Start();
        }

        public void Connect(WebProxy proxy = null)
        {
            _securities.Clear();
            _myPortfolious.Clear();
            //_securitiesSubscriptions.Clear();
            //_orderSubcriptions.Clear();

            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _memo = ((ServerParameterString)ServerParameters[2]).Value;

            if (string.IsNullOrEmpty(_publicKey)
                || string.IsNullOrEmpty(_secretKey)
                || string.IsNullOrEmpty(_memo))
            {
                SendLogMessage("Connection terminated. You must specify the public and private keys. You can get it on the BitMartSpot website",
                    LogMessageType.Error);
                return;
            }

            try
            {
                _restClient = new BitMartRestClient(_publicKey, _secretKey, _memo);

                RestRequest requestRest = new RestRequest("/system/time", Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    
                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
                    //CheckSocketsActivate();
                }
                else
                {
                    SendLogMessage("Connection can be open. BitMartSpot. Error request", LogMessageType.Error);

                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
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
                UnsubscribeFromAllWebSockets();
                _securities.Clear();
                _myPortfolious.Clear();
                _subscribedSecurities.Clear();
                //_orderSubcriptions.Clear();

                DeleteWebSocketConnection();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

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

        public ServerType ServerType => ServerType.BitMartSpot;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public string _publicKey;

        public string _secretKey;

        public string _memo;

        public string _baseUrl = "https://api-cloud.bitmart.com";

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
            // https://api-cloud.bitmart.com/spot/v1/symbols/details

            try
            {
                RestRequest requestRest = new RestRequest("/spot/v1/symbols/details", Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitMartBaseMessage parsed =
                    JsonConvert.DeserializeAnonymousType(response.Content, new BitMartBaseMessage());

                    if (parsed != null && parsed.data != null
                        && parsed.message == "OK")
                    {
                        string symbols = parsed.data["symbols"].ToString();

                        List<BitMartSecurityRest> securities =
                            JsonConvert.DeserializeAnonymousType(symbols, new List<BitMartSecurityRest>());
                        UpdateSecuritiesFromServer(securities);
                    }
                    else
                    {
                        string message = "";
                        if (parsed != null)
                        {
                            message = parsed.message;
                        }
                    }
                }
                else
                {

                    SendLogMessage("Securities request error. Status: " +
                        response.StatusCode + ", " + response.Content, LogMessageType.Error);
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

                    if (item.trade_status != "trading")
                    {
                        continue;
                    }

                    Security newSecurity = new Security();

                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.symbol;
                    newSecurity.NameClass = item.quote_currency;
                    newSecurity.NameId = item.symbol_id;
                    newSecurity.State = SecurityStateType.Activ;

                    newSecurity.Decimals = Convert.ToInt32(item.price_max_precision);
                    newSecurity.DecimalsVolume = GetDecimalsVolume(item.quote_increment);
                    newSecurity.PriceStep = GetPriceStep(newSecurity.Decimals);
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.Lot = 1;
                    newSecurity.SecurityType = SecurityType.CurrencyPair;
                    newSecurity.Exchange = ServerType.BitMartSpot.ToString();
                    newSecurity.MinTradeAmount = item.min_buy_amount.ToDecimal();

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

        private string PortfolioName = "BitMartSpot";

        public void GetPortfolios()
        {
            GetCurrentPortfolio();
        }

        private bool GetCurrentPortfolio()
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string endPoint = $"/spot/v1/wallet";

                HttpResponseMessage response = _restClient.Get(endPoint, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;
                BitMartBaseMessage parsed =
                    JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (parsed != null && parsed.data != null && parsed.data.ContainsKey("wallet"))
                    {
                        string wallet = parsed.data["wallet"].ToString();
                        BitMartSpotPortfolioItems portfolio =
                            JsonConvert.DeserializeAnonymousType(wallet, new BitMartSpotPortfolioItems());

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
                        + response.StatusCode + "  " + PortfolioName +
                        ", " + message, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio request error " + exception.ToString(), LogMessageType.Error);
            }

            return false;
        }

        private void ConvertToPortfolio(BitMartSpotPortfolioItems portfolioItems)
        {
            if (portfolioItems == null)
            {
                return;
            }

            Portfolio portfolio = new Portfolio();
            portfolio.Number = this.PortfolioName;
            portfolio.ValueBegin = 1;
            portfolio.ValueCurrent = 1;

            for (int i = 0; i < portfolioItems.Count; i++)
            {
                BitMartSpotPortfolioItem item = portfolioItems[i];

                PositionOnBoard pos = new PositionOnBoard()
                {
                    PortfolioName = this.PortfolioName,
                    SecurityNameCode = item.id,
                    ValueBlocked = item.frozen.ToDecimal(),
                    ValueCurrent = item.available.ToDecimal()
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
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.Now;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);

            while (candles.Count > candleCount)
            {
                candles.RemoveAt(0);
            }

            return candles;
        }

        private readonly HashSet<int> _allowedTf = new HashSet<int> { 1, 3, 5, 15, 30, 60, 120, 240, 1440 };

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
                        DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Local);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Local);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Local);

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!_allowedTf.Contains(tfTotalMinutes))
                return null;

            List<Candle> candles = new List<Candle>();

            int countNeedToLoad = GetCountCandlesFromSliceTime(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

            DateTime fromTime = endTime - TimeSpan.FromMinutes(tfTotalMinutes * countNeedToLoad);

            int limitData = 100;

            do
            {
                int limit = countNeedToLoad;

                if (countNeedToLoad > limitData)
                {
                    limit = limitData;

                }
                else
                {
                    limit = countNeedToLoad;
                }

                DateTime slidingFrom = endTime - TimeSpan.FromMinutes(tfTotalMinutes * limit);

                BitMartCandle history = GetHistoryCandle(security, tfTotalMinutes, slidingFrom, endTime);
                List<Candle> rangeCandles = ConvertToOsEngineCandles(history);

                if (rangeCandles == null)
                    return null;

                if (rangeCandles != null && candles.Count != 0 && rangeCandles.Count != 0)
                {
                    for (int i = 0; i < rangeCandles.Count; i++)
                    {
                        if (candles[0].TimeStart <= rangeCandles[i].TimeStart)
                        {
                            rangeCandles.RemoveAt(i);
                            i--;
                        }
                    }
                }

                candles.InsertRange(0, rangeCandles);

                if (candles.Count != 0)
                {
                    endTime = candles[0].TimeStart;
                }

                countNeedToLoad -= limit;

            } while (countNeedToLoad > 0);

            return candles;
        }

        private int GetCountCandlesFromSliceTime(DateTime startTime, DateTime endTime, TimeSpan tf)
        {
            if (tf.Hours != 0)
            {
                TimeSpan TimeSlice = endTime - startTime;

                return Convert.ToInt32(TimeSlice.TotalHours / tf.TotalHours);
            }
            else
            {
                TimeSpan TimeSlice = endTime - startTime;
                return Convert.ToInt32(TimeSlice.TotalMinutes / tf.Minutes);
            }
        }

        private BitMartCandle GetHistoryCandle(Security security, int tfTotalMinutes,
          DateTime startTime, DateTime endTime)
        {
            string endPoint = "/spot/quotation/v3/lite-klines?symbol=" + security.Name;

            endPoint += "&step=" + tfTotalMinutes;
            endPoint += "&after=" + ConvertToUnixTimestamp(startTime);
            endPoint += "&before=" + ConvertToUnixTimestamp(endTime);
            //endPoint += "&limit =" + 200;

            try
            {
                RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                RestClient client = new RestClient(_baseUrl);
                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitMartCandle symbols =
                                JsonConvert.DeserializeAnonymousType(response.Content, new BitMartCandle());

                    if (symbols.code == "1000")
                    {
                        if (symbols != null && symbols.data != null)
                        {
                            return symbols;
                        }
                        else
                        {
                            SendLogMessage("Empty Candles request error. Status: " + response.StatusCode, LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Candles request error. Status: {symbols.code} msg: {symbols.message} ", LogMessageType.Error);
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

        private List<Candle> ConvertToOsEngineCandles(BitMartCandle symbols)
        {
            List<Candle> candles = new List<Candle>();

            if (symbols == null)
            {
                return null;
            }

            for (int i = 0; i < symbols.data.Count; i++)
            {
                if (CheckCandlesToZeroData(symbols.data[i]))
                {
                    continue;
                }

                List<string> item = symbols.data[i];

                Candle candle = new Candle();

                candle.State = CandleState.Finished;
                candle.TimeStart = ConvertToDateTimeFromUnixFromSeconds(item[0]);
                candle.Volume = item[5].ToDecimal();
                candle.Close = item[4].ToDecimal();
                candle.High = item[2].ToDecimal();
                candle.Low = item[3].ToDecimal();
                candle.Open = item[1].ToDecimal();

                candles.Add(candle);
            }

            return candles;
        }

        private bool CheckCandlesToZeroData(List<string> item)
        {
            if (item[1].ToDecimal() == 0 ||
                item[2].ToDecimal() == 0 ||
                item[3].ToDecimal() == 0 ||
                item[4].ToDecimal() == 0)
            {
                return true;
            }

            return false;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
                startTime >= DateTime.Now ||
                actualTime > endTime ||
                actualTime > DateTime.Now)
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

        private string _webSocketUrlPublic = "wss://ws-manager-compress.bitmart.com/api?protocol=1.1";

        private string _webSocketUrlPrivate = "wss://ws-manager-compress.bitmart.com/user?protocol=1.1";

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

        private void CheckSocketsActivate()
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

        private void CreateAuthMessageWebSocekt()
        {
            string timeStamp = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            string sign = GenerateSignature(timeStamp, "bitmart.WebSocket");

            _webSocketPrivate.Send($"{{\"op\": \"login\", \"args\": [\"{_publicKey}\", \"{timeStamp}\", \"{sign}\"]}}");
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublicNew_OnClose(object arg1, CloseEventArgs e)
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

                if (string.IsNullOrEmpty(e.ToString()))
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
                    SendLogMessage("BitMartSpot WebSocket Public connection open", LogMessageType.System);
                    CheckSocketsActivate();
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
                    if (e.Data.StartsWith("{\"errorMessage\""))
                    {
                        SendLogMessage(e.Data, LogMessageType.Error);
                        return;
                    }

                    if (e.Data.Contains("login"))
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
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnClose(object arg1, CloseEventArgs arg2)
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
                CreateAuthMessageWebSocekt();
                SendLogMessage("BitMartSpot WebSocket Private connection open", LogMessageType.System);
                CheckSocketsActivate();
                //GetAllOrdersFromExchange();
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
                            webSocketPublic.Send("ping");
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
                        _webSocketPrivate.Send("ping");
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

        private List<string> _subscribedSecurities = new List<string>();

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscrible.WaitToProceed();

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

                if (_webSocketPublic.Count == 0)
                {
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 90 == 0)
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
                    webSocketPublic.Send($"{{\"op\": \"subscribe\", \"args\": [\"spot/trade:{security.Name}\"]}}");
                    webSocketPublic.Send($"{{\"op\": \"subscribe\", \"args\": [\"spot/depth20:{security.Name}\"]}}");
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
                _webSocketPrivate.Send($"{{\"op\": \"subscribe\", \"args\": [\"spot/user/orders:ALL_SYMBOLS\"]}}");
                _webSocketPrivate.Send($"{{\"op\": \"subscribe\", \"args\": [\"spot/user/balance:BALANCE_UPDATE\"]}}");
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
                                        string securityName = _subscribedSecurities[j];

                                        webSocketPublic.Send($"{{\"op\": \"unsubscribe\", \"args\": [\"spot/trade:{securityName}\"]}}");
                                        webSocketPublic.Send($"{{\"op\": \"unsubscribe\", \"args\": [\"spot/depth20:{securityName}\"]}}");
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
                    _webSocketPrivate.Send($"{{\"op\": \"unsubscribe\", \"args\": [\"spot/user/orders:ALL_SYMBOLS\"]}}");
                    _webSocketPrivate.Send($"{{\"op\": \"unsubscribe\", \"args\": [\"spot/user/balance:BALANCE_UPDATE\"]}}");
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

        private void MessageReaderPublic()
        {
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
                        || string.IsNullOrEmpty(baseMessage.table))
                    {
                        continue;
                    }

                    if (baseMessage.table.Contains("/depth"))
                    {
                        UpdateMarketDepth(message);

                    }
                    else if (baseMessage.table.Contains("/trade"))
                    {
                        UpdateTrade(message);
                    }
                    else
                    {
                        SendLogMessage("Unknown message: " + baseMessage.table, LogMessageType.Error);
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
            MarketQuotesMessage baseMessage =
            JsonConvert.DeserializeAnonymousType(data, new MarketQuotesMessage());

            if (baseMessage == null || baseMessage.data == null || baseMessage.data.Count == 0)
            {
                SendLogMessage("Wrong 'Trade' message:" + data, LogMessageType.Error);
                return;
            }

            for (int i = 0; i < baseMessage.data.Count; i++)
            {
                QuotesBitMart quotes = baseMessage.data[i];

                if (string.IsNullOrEmpty(quotes.symbol))
                {
                    continue;
                }

                Trade trade = new Trade();
                trade.SecurityNameCode = quotes.symbol;
                trade.Price = quotes.price.ToDecimal();
                trade.Time = ConvertToDateTimeFromUnixFromSeconds(quotes.s_t.ToString());
                trade.Id = quotes.s_t.ToString() + quotes.side + quotes.symbol;

                if (quotes.side == "buy")
                {
                    trade.Side = Side.Buy;
                }
                else
                {
                    trade.Side = Side.Sell;
                }

                trade.Volume = quotes.size.ToDecimal();

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(trade);
                }
            }
        }

        private void UpdateMarketDepth(string data)
        {
            MarketDepthFullMessage baseMessage =
            JsonConvert.DeserializeAnonymousType(data, new MarketDepthFullMessage());

            if (baseMessage.data.Count == 0)
            {
                return;
            }

            for (int i = 0; i < baseMessage.data.Count; i++)
            {
                MarketDepthBitMart messDepth = baseMessage.data[i];

                if (messDepth == null || String.IsNullOrEmpty(messDepth.symbol))
                {
                    continue;
                }

                if (messDepth.bids == null ||
                    messDepth.asks == null)
                {
                    continue;
                }

                if (messDepth.bids.Count == 0 ||
                    messDepth.asks.Count == 0)
                {
                    return;
                }

                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = messDepth.symbol;
                depth.Time = ConvertToDateTimeFromUnixFromMilliseconds(messDepth.ms_t.ToString());

                for (int k = 0; k < messDepth.bids.Count; k++)
                {
                    MarketDepthLevel newBid = new MarketDepthLevel();
                    newBid.Price = messDepth.bids[k][0].ToDecimal();
                    newBid.Bid = messDepth.bids[k][1].ToDecimal();
                    depth.Bids.Add(newBid);
                }

                for (int k = 0; k < messDepth.asks.Count; k++)
                {
                    MarketDepthLevel newAsk = new MarketDepthLevel();
                    newAsk.Price = messDepth.asks[k][0].ToDecimal();
                    newAsk.Ask = messDepth.asks[k][1].ToDecimal();
                    depth.Asks.Add(newAsk);
                }

                //TODO: Maybe error
                if (_lastMdTime != DateTime.MinValue &&
                    _lastMdTime >= depth.Time)
                {
                    depth.Time = _lastMdTime.AddTicks(1);
                }

                _lastMdTime = depth.Time;

                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(depth);
                }
            }
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        private void MessageReaderPrivate()
        {
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

                    if (message.Contains("\"event\""))
                    {
                        continue;
                    }

                    SoketBaseMessage baseMessage = JsonConvert.DeserializeAnonymousType(message, new SoketBaseMessage());

                    if (baseMessage == null
                        || string.IsNullOrEmpty(baseMessage.table))
                    {
                        continue;
                    }

                    if (baseMessage.table.Contains("/user/balance"))
                    {
                        UpdateMyPortfolio(baseMessage.data.ToString());

                    }
                    else if (baseMessage.table.Contains("/user/order"))
                    {
                        UpdateMyOrder(baseMessage.data.ToString());
                    }
                    else
                    {
                        SendLogMessage("Unknown message: " + baseMessage.table, LogMessageType.Error);
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
            List<MyTrade> trades = GetTradesForOrder(order.NumberMarket);

            if (trades == null)
                return;

            for (int i = 0; i < trades.Count; i++)
            {
                MyTradeEvent?.Invoke(trades[i]);
            }
        }

        private void UpdateMyOrder(string data)
        {
            BitMartOrders baseOrders =
                JsonConvert.DeserializeAnonymousType(data, new BitMartOrders());

            if (baseOrders == null || baseOrders.Count == 0)
            {
                return;
            }

            for (int k = 0; k < baseOrders.Count; k++)
            {
                BitMartOrder baseOrder = baseOrders[k];

                Order order = ConvertToOsEngineOrder(baseOrder);

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

        private Order ConvertToOsEngineOrder(BitMartOrder baseOrder)
        {
            Order order = new Order();

            order.SecurityNameCode = baseOrder.symbol;
            order.Volume = baseOrder.size.ToDecimal();
            order.VolumeExecute = baseOrder.filled_size.ToDecimal();

            order.PortfolioNumber = this.PortfolioName;

            if (baseOrder.type == "limit")
            {
                order.Price = baseOrder.price.ToDecimal();
                order.TypeOrder = OrderPriceType.Limit;
            }
            else if (baseOrder.type == "market")
            {
                order.TypeOrder = OrderPriceType.Market;
                if (order.Volume <= 0)
                {   // service could send zero size for marker orders
                    order.Volume = order.VolumeExecute;
                }
            }

            try
            {
                order.NumberUser = Convert.ToInt32(baseOrder.client_order_id);
            }
            catch
            {
                SendLogMessage("strage order num: " + baseOrder.client_order_id, LogMessageType.Error);
                return null;
            }

            order.NumberMarket = baseOrder.order_id;
            order.ServerType = ServerType.BitMartSpot;

            order.TimeCreate = ConvertToDateTimeFromUnixFromMilliseconds(baseOrder.create_time);
            order.TimeCallBack = ConvertToDateTimeFromUnixFromMilliseconds(baseOrder.update_time);


            if (baseOrder.side == "buy")
            {
                order.Side = Side.Buy;
            }
            else
            {
                order.Side = Side.Sell;
            }

            // -new= The order has been accepted by the engine.
            //- partially_filled = A part of the order has been filled.
            //- filled = The order has been completed.
            //-canceled = The order has been canceled by the user.
            //- partially_canceled = A part of the order has been filled , and the order has been canceled.

            if (baseOrder.order_state == "new")
            {
                order.State = OrderStateType.Active;
            }
            else if (baseOrder.order_state == "filled")
            {
                order.State = OrderStateType.Done;
            }
            else if (baseOrder.order_state == "partially_filled")
            {
                order.State = OrderStateType.Partial;
            }
            else if (baseOrder.order_state == "canceled" || baseOrder.order_state == "partially_canceled")
            {

                if (string.IsNullOrEmpty(baseOrder.filled_size))
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
            //https://developer-pro.bitmart.com/en/spot/#private-balance-change

            BitMartPortfolioSocket porfMessage =
            JsonConvert.DeserializeAnonymousType(data, new BitMartPortfolioSocket());

            Portfolio portf = null;
            if (_myPortfolious != null && _myPortfolious.Count > 0)
            {
                portf = _myPortfolious[0];
            }

            if (portf == null)
            {
                return;
            }

            if (porfMessage != null && porfMessage.Count > 0 && porfMessage[0].balance_details.Count > 0)
            {
                for (int i = 0; i < porfMessage[0].balance_details.Count; i++)
                {
                    BitMartBalanceDetail details = porfMessage[0].balance_details[i];

                    PositionOnBoard pos = new PositionOnBoard();
                    pos.ValueCurrent = details.av_bal.ToDecimal();
                    pos.ValueBlocked = details.fz_bal.ToDecimal();
                    pos.PortfolioName = this.PortfolioName;
                    pos.SecurityNameCode = details.ccy;

                    portf.SetNewPosition(pos);
                }
            }

            if (PortfolioEvent != null)
            {
                PortfolioEvent(_myPortfolious);
            }
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
                //SubcribeToOrderData(order.SecurityNameCode);

                string endPoint = "/spot/v2/submit_order";

                NewOrderBitMartRequest body = GetOrderRequestObj(order);
                string bodyStr = JsonConvert.SerializeObject(body);
                SendLogMessage("Order New: " + bodyStr, LogMessageType.Connect);
                HttpResponseMessage response = _restClient.Post(endPoint, bodyStr, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;
                BitMartBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (parsed != null && parsed.data != null && parsed.data.ContainsKey("order_id"))
                    {
                        //Everything is OK

                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Order created, but answer is wrong: {content}", LogMessageType.Error);
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
                requestObj.side = "buy";
            }
            else
            {
                requestObj.side = "sell";

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

            requestObj.symbol = order.SecurityNameCode;
            requestObj.size = order.Volume.ToString().Replace(',', '.');
            requestObj.client_order_id = order.NumberUser.ToString();

            return requestObj;
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

                string endPoint = "/spot/v3/cancel_order";

                CancelOrderBitMartRequest body = new CancelOrderBitMartRequest();
                body.client_order_id = order.NumberUser.ToString();
                body.symbol = order.SecurityNameCode;

                string bodyStr = JsonConvert.SerializeObject(body);
                SendLogMessage("Order Cancel: " + bodyStr, LogMessageType.Connect);
                HttpResponseMessage response = _restClient.Post(endPoint, bodyStr, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;
                BitMartBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());


                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (parsed != null && parsed.data != null && parsed.data.ContainsKey("result"))
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

            List<Order> actualOrders = GetAllOrdersFromExchange();

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
            List<Order> orders = GetAllOrdersFromExchange();

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
            List<Order> orders = GetAllOrdersFromExchange();

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
            _rateGateGetOrder.WaitToProceed();

            //try
            //{
            //    string endPoint = "/spot/v4/query/open-orders";

            //    HttpResponseMessage response = _restClient.Post(endPoint,
            //                "{ \"orderMode\": \"spot\" }",
            //                secured: true);

            //    if (response.StatusCode == HttpStatusCode.OK)
            //    {

            //        string content = response.Content.ReadAsStringAsync().Result;
            //        BitMartRestOrdersBaseMessage parsed =
            //            JsonConvert.DeserializeAnonymousType(content, new BitMartRestOrdersBaseMessage());

            //        if (parsed != null && parsed.data != null)
            //        {
            //            //Everything is OK
            //            BitMartRestOrders orders = JsonConvert.DeserializeAnonymousType(parsed.data.ToString(), new BitMartRestOrders());

            //            List<Order> osEngineOrders = new List<Order>();

            //            for (int i = 0; i < orders.Count; i++)
            //            {
            //                Order newOrd = ConvertRestOrdersToOsEngineOrder(orders[i]);

            //                if (newOrd == null)
            //                {
            //                    continue;
            //                }

            //                osEngineOrders.Add(newOrd);

            //                //SubcribeToOrderData(newOrd.SecurityNameCode);
            //            }

            //            return osEngineOrders;

            //        }

            //    }
            //    else if (response.StatusCode == HttpStatusCode.NotFound)
            //    {
            //        return null;
            //    }
            //    else
            //    {
            //        SendLogMessage("Get all orders request error. ", LogMessageType.Error);

            //        if (response.Content != null)
            //        {
            //            SendLogMessage("Fail reasons: "
            //          + response.Content, LogMessageType.Error);
            //        }
            //    }
            //}
            //catch (Exception exception)
            //{
            //    SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
            //}

            return null;
        }

        private Order GetOrderFromExchange(string userOrderId)
        {
            _rateGateGetOrder.WaitToProceed();

            if (string.IsNullOrEmpty(userOrderId))
            {
                SendLogMessage("Order ID is empty", LogMessageType.Connect);
                return null;
            }

            try
            {
                string endPoint = "/spot/v4/query/client-order";

                string body = "{ \"clientOrderId\": \"" + userOrderId + "\", \"recvWindow\": 60000  }";
                SendLogMessage("Request Order: " + body, LogMessageType.Connect);

                HttpResponseMessage response = _restClient.Post(endPoint, body, secured: true);

                if (response.StatusCode == HttpStatusCode.OK)
                {

                    string content = response.Content.ReadAsStringAsync().Result;
                    BitMartRestOrdersBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartRestOrdersBaseMessage());

                    if (parsed != null && parsed.data != null)
                    {
                        //Everything is OK
                        BitMartRestOrder baseOrder = JsonConvert.DeserializeAnonymousType(parsed.data.ToString(), new BitMartRestOrder());

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
                    SendLogMessage("Get order request error. ", LogMessageType.Error);

                    if (response.Content != null)
                    {
                        SendLogMessage("Fail reasons: "
                      + response.Content, LogMessageType.Error);
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
            Order myOrder = GetOrderFromExchange(order.NumberUser.ToString());

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

        private Order ConvertRestOrdersToOsEngineOrder(BitMartRestOrder baseOrder)
        {
            Order order = new Order();

            order.SecurityNameCode = baseOrder.symbol;
            order.Volume = baseOrder.size.ToDecimal();
            order.VolumeExecute = baseOrder.filledSize.ToDecimal();

            order.PortfolioNumber = this.PortfolioName;

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
                order.NumberUser = Convert.ToInt32(baseOrder.clientOrderId);
            }
            catch
            {
                return null;
            }

            order.NumberMarket = baseOrder.orderId;

            order.TimeCallBack = ConvertToDateTimeFromUnixFromMilliseconds(baseOrder.updateTime.ToString());

            if (baseOrder.side == "buy")
            {
                order.Side = Side.Buy;
            }
            else
            {
                order.Side = Side.Sell;
            }

            // -new = The order has been accepted by the engine.
            // -partially_filled = A part of the order has been filled.


            if (baseOrder.state == "new")
            {
                order.State = OrderStateType.Active;
            }
            else if (baseOrder.state == "partially_filled")
            {
                order.State = OrderStateType.Partial;
            }


            if (baseOrder.state == "new")
            {
                order.State = OrderStateType.Active;
            }
            else if (baseOrder.state == "filled")
            {
                order.State = OrderStateType.Done;
            }
            else if (baseOrder.state == "partially_filled")
            {
                order.State = OrderStateType.Partial;
            }
            else if (baseOrder.state == "canceled" || baseOrder.state == "partially_canceled")
            {

                if (string.IsNullOrEmpty(baseOrder.filledSize))
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

        private List<MyTrade> GetTradesForOrder(string orderId)
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string endPoint = $"/spot/v4/query/order-trades";

                GetTradesBitMartRequest body = new GetTradesBitMartRequest();
                body.orderId = orderId;
                body.recvWindow = 60000;

                string bodyStr = JsonConvert.SerializeObject(body);
                SendLogMessage("Order trades: " + bodyStr, LogMessageType.Connect);
                HttpResponseMessage response = _restClient.Post(endPoint, bodyStr, secured: true);

                string content = response.Content.ReadAsStringAsync().Result;

                //SendLogMessage("Order trades resp: " + content, LogMessageType.Connect);

                BitMartRestOrdersBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartRestOrdersBaseMessage());

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (parsed != null && parsed.data != null && parsed.data != null)
                    {

                        BitMartTrades baseTrades =
                            JsonConvert.DeserializeAnonymousType(parsed.data.ToString(), new BitMartTrades());

                        List<MyTrade> trades = new List<MyTrade>();

                        for (int i = 0; i < baseTrades.Count; i++)
                        {
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
                        + response.StatusCode + "  " + orderId +
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
            trade.NumberOrderParent = baseTrade.orderId;
            trade.NumberTrade = baseTrade.tradeId;
            trade.SecurityNameCode = baseTrade.symbol;
            trade.Time = ConvertToDateTimeFromUnixFromMilliseconds(baseTrade.createTime.ToString());
            if (baseTrade.side == "buy")
            {
                trade.Side = Side.Buy;
            }
            else
            {
                trade.Side = Side.Sell;
            }
            trade.Price = baseTrade.price.ToDecimal();
            trade.Volume = baseTrade.size.ToDecimal();

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
        }

        public long ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return Convert.ToInt64(diff.TotalSeconds);
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
        public event Action<PublicMarketData> PublicMarketDataEvent;

        #endregion
    }
}