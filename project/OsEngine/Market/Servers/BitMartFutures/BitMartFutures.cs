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
using WebSocket4Net;

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
            worker.Name = "CheckAliveBitMart";
            worker.Start();

            Thread worker2 = new Thread(DataMessageReader);
            worker2.Name = "DataMessageReaderBitMart";
            worker2.Start();

            Thread worker3 = new Thread(PortfolioMessageReader);
            worker3.Name = "PortfolioMessageReaderBitMart";
            worker3.Start();
        }

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                
                _securities.Clear();
                _myPortfolious.Clear();
                _securitiesSubscriptions.Clear();
                _orderSubcriptions.Clear();

                SendLogMessage("Start BitMartFutures Connection", LogMessageType.System);

                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
                _memo = ((ServerParameterString)ServerParameters[2]).Value;

                if (string.IsNullOrEmpty(_publicKey) 
                    || string.IsNullOrEmpty(_secretKey) 
                    || string.IsNullOrEmpty(_memo))
                {
                    SendLogMessage("Connection terminated. You must specify the public and private keys. You can get it on the BitMart website",
                        LogMessageType.Error);
                    return;
                }

                _restClient = new BitMartRestClient(_publicKey, _secretKey, _memo);

                if (CheckConnection() == false)
                {
                    SendLogMessage("Authorization Error. Probably an invalid keys are specified. You can see it on the BitMart website.",
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
                _myPortfolious.Clear();
                _securitiesSubscriptions.Clear();
                _orderSubcriptions.Clear();
                _serverOrderIDs.Clear();

                DeleteWebSocketConnection();

                SendLogMessage("Dispose. Connection Closed by BitMart. WebSocket Data Closed Event", LogMessageType.System);
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

        public ServerType ServerType => ServerType.BitMartFutures;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        private string _publicKey;

        private string _secretKey;

        private string _memo;

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

                        List <BitMartSecurityRest> securities =
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

                for(int i = 0; i < stocks.Count; i++)
                {
                    BitMartSecurityRest item = stocks[i];

                    Security newSecurity = new Security();

                    newSecurity.Name = item.symbol;
                    newSecurity.NameFull = item.symbol;
                    newSecurity.NameClass = item.quote_currency;
                    newSecurity.NameId = item.symbol;
                    newSecurity.State = SecurityStateType.Activ;
                    newSecurity.Decimals = GetDecimalsVolume(item.price_precision);
                    newSecurity.DecimalsVolume = GetDecimalsVolume(item.vol_precision);
                    newSecurity.PriceStep = GetPriceStep( newSecurity.Decimals );
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.Lot = 1;
                    newSecurity.SecurityType = SecurityType.Futures;
                    newSecurity.Exchange = ServerType.BitMartFutures.ToString();
                    newSecurity.MinTradeAmount = item.min_volume.ToDecimal() ;

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
            { // add weekends
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

        private readonly HashSet<int> _allowedTf = new HashSet<int> {
            1, 3, 5, 15, 30, 60, 120, 240, 360, 720, 1440, 10080};

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, 
                        DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            _rateGateSendOrder.WaitToProceed();

            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;

            if (!_allowedTf.Contains(tfTotalMinutes))
                return null;

            if(startTime != actualTime)
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

                if(newCandles != null &&
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
            DateTime maxStartTime = endTime.AddMinutes( -500 * tfTotalMinutes);

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
                HttpResponseMessage response = _restClient.Get(endPoint, secured: false);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content.ReadAsStringAsync().Result;
                    //SendLogMessage("GetHistoryCandle resp: " + content, LogMessageType.Connect);
                    BitMartBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());

                    if (parsed != null && parsed.data != null)
                    {
                        string history = parsed.data.ToString();
                        BitMartCandlesHistory candles =
                            JsonConvert.DeserializeAnonymousType(history, new BitMartCandlesHistory());

                        return candles;

                    } else
                    {
                        SendLogMessage("Empty Candles request error. Status: " + response.StatusCode, LogMessageType.Error);
                    }

                }
                else
                {
                    SendLogMessage("Candles request error to url='" + endPoint+ "'. Status: " + response.StatusCode, LogMessageType.Error);
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

            for(int i = 0; i < candles.Count;i++)
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

        private readonly string _wsPublic = "wss://openapi-ws-v2.bitmart.com/api?protocol=1.1";

        private readonly string _wsPrivate = "wss://openapi-ws-v2.bitmart.com/user?protocol=1.1";

        private string _socketLocker = "webSocketLockerBitMart";

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
                    _webSocketData.AutoSendPingInterval = 4;
                    _webSocketData.Opened += WebSocketData_Opened;
                    _webSocketData.Closed += WebSocketData_Closed;
                    _webSocketData.MessageReceived += WebSocketData_MessageReceived;
                    _webSocketData.Error += WebSocketData_Error;
                    _webSocketData.Open();


                    _webSocketPortfolio = new WebSocket(_wsPrivate);
                    
                    _webSocketPortfolio.EnableAutoSendPing = true;
                    _webSocketPortfolio.AutoSendPingInterval = 4;
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

        private void AuthInSocket()
        {
            WSRequestAuth.AuthArgs auth = BitMartEncriptor.GetWSAuthArgs(this._publicKey, this._secretKey, this._memo);
            WSRequestAuth authObj = new WSRequestAuth(auth);

            string message = authObj.GetJson(); 
            SendLogMessage("Porfolio Send: " + message, LogMessageType.Connect);
            _webSocketPortfolio.Send(message);
        }

        HashSet<string> _orderSubcriptions = new HashSet<string>();

        private void SubcribeToOrderData()
        {
            WSRequestOrder obj = new WSRequestOrder();
            string message = obj.GetJson();
            SendLogMessage("SubcribeToOrderData: " + message, LogMessageType.Connect);
            _webSocketPortfolio.Send(message);

            Thread.Sleep(1000);
        }

        private void ActivateCurrentPortfolioListening()
        {
            AuthInSocket();

            Thread.Sleep(2000);

            List<string> currencies = new List<string>() { "USDT", "BTC", "ETH" };
            WSRequestBalance bObj = new WSRequestBalance(currencies);
            string message = bObj.GetJson();
            SendLogMessage("Porfolio assets send: " + message, LogMessageType.Connect);
            _webSocketPortfolio.Send(message);

            WSRequestPosition pObj = new WSRequestPosition();
            message = pObj.GetJson();
            SendLogMessage("Porfolio positions send: " + message, LogMessageType.Connect);
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
                SendLogMessage("WebSocketData. Connection Closed by BitMart. WebSocket Data Closed Event", LogMessageType.Error);

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

            SubcribeToOrderData();
            ActivateCurrentPortfolioListening();
        }

        private void WebSocketPortfolio_Closed(object sender, EventArgs e)
        {
            try
            {
                SendLogMessage("Portfolio Connection Closed by BitMart. WebSocket Portfolio Closed Event", LogMessageType.Error);

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

                string message = Decompress(e.Data);

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

                    if (_lastTimeCheckConnection.AddSeconds(30) < DateTime.Now)
                    {
                        if (CheckConnection() == false)
                        {
                            if (ServerStatus == ServerConnectStatus.Connect)
                            {
                                ServerStatus = ServerConnectStatus.Disconnect;
                                DisconnectEvent();
                            }
                        }

                        _lastTimeCheckConnection = DateTime.Now;
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

        private readonly object _securitiesSubcriptionsLock = new object();

        private HashSet<string> _securitiesSubscriptions = new HashSet<string>();

        public void Subscrible(Security security)
        {
            try
            {
                lock (_securitiesSubcriptionsLock)
                {
                    if (_securitiesSubscriptions.Contains(security.Name))
                    {
                        return;
                    }
                    _securitiesSubscriptions.Add(security.Name);
                }

                _rateGateSubscrible.WaitToProceed();

                // trades subscription
                string name = security.Name;

                WSRequestSubscribe tradeSubscribe = new WSRequestSubscribe(WSRequestSubscribe.Channel.Trade, security.Name);
                string messageTradeSub = tradeSubscribe.GetJson(); 
                SendLogMessage("Send to WS: " + messageTradeSub, LogMessageType.Connect);
                _webSocketData.Send(messageTradeSub);

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
                        UpDateMarketDepth(baseMessage.data.ToString());
                    }
                    else if (baseMessage.group.Contains("/trade"))
                    {
                        UpDateTrade(baseMessage.data.ToString());
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

        private void UpDateTrade(string data)
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

        private void UpDateMarketDepth(string data)
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

                if (level == null ) 
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
                depth.Time = _lastMdTime.AddMilliseconds(1);
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
                        UpDateMyPortfolio(baseMessage.data.ToString());

                    }
                    else if (baseMessage.group.Contains("futures/position"))
                    {
                        UpDateMyPositions(baseMessage.data.ToString());

                    }
                    else if (baseMessage.group.Contains("futures/order"))
                    {
                        UpDateMyOrder(baseMessage.data.ToString());
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

        private void UpDateMyOrder(string data)
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

        private void UpDateMyPortfolio(string data)
        {
            BitMartBalanceDetail balanceDetail =
                JsonConvert.DeserializeAnonymousType(data, new BitMartBalanceDetail());

            Portfolio portf = null;
            if (_myPortfolious != null && _myPortfolious.Count > 0)
            {
                portf = _myPortfolious[0];
            }

            if(portf == null)
            {
                return;
            }

            List<PositionOnBoard> positions = portf.GetPositionOnBoard();

            for (int i = 0; i < positions.Count; i++) 
            {
                PositionOnBoard position = positions[i];
                if (position.SecurityNameCode != balanceDetail.currency )
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

        private void UpDateMyPositions(string data)
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

                        if(content.Contains("You do not have the permissions to perform this operation. "))
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

            if(order.Side == Side.Buy)
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

        public void CancelOrder(Order order)
        {
            string order_id = GetServerOrderId(order);
            if (string.IsNullOrEmpty(order_id))
            {
                return;
            }

            _rateGateCancelOrder.WaitToProceed();

            try { 
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
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Cancel order, answer is wrong: {content}",                           LogMessageType.Error);
                    }
                }
                else
                {
                    string message = content;
                    if (parsed != null && parsed.message != null)
                    {
                        message = parsed.message;
                    }

                    SendLogMessage("Cancel order failed. Status: "
                        + response.StatusCode + "  " + order.SecurityNameCode + ", " + message, LogMessageType.Error);

                    CreateOrderFail(order);
                }

            }
            catch (Exception exception)
            {
                SendLogMessage("Cancel order error." + exception.ToString(), LogMessageType.Error);
            }

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
                else if(response.StatusCode == HttpStatusCode.NotFound)
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
                string endPoint = "/contract/private/order-history?symbol="+symbol;
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

                        for (int i = 0; i < baseOrders.Count; i ++)
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
                endPoint += "&order_id=" + serverOrderId.ToString() ;

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

        public void GetOrderStatus(Order order)
        {
            string serverOrderId = GetServerOrderId(order);
            if (serverOrderId == null)
            {
                SendLogMessage("Fail to get server order_id for user order_id=" + order.NumberUser , LogMessageType.Error);
            }

            Order myOrder = GetOrderFromExchangeByID(serverOrderId, order.SecurityNameCode);
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
                
                string endPoint = $"/contract/private/trades?symbol="+order.SecurityNameCode;
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