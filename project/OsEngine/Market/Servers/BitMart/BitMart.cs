/*
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
using WebSocket4Net;


namespace OsEngine.Market.Servers.BitMart
{
    public class BitMartServer : AServer
    {
        public BitMartServer()
        {
            BitMartServerRealization realization = new BitMartServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
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

            Thread worker2 = new Thread(DataMessageReader);
            worker2.Name = "DataMessageReaderBitMart";
            worker2.Start();

            Thread worker3 = new Thread(PortfolioMessageReader);
            worker3.Name = "PortfolioMessageReaderBitMart";
            worker3.Start();
        }

        public void Connect()
        {
            try
            {
                
                _securities.Clear();
                _myPortfolious.Clear();
                _securitiesSubscriptions.Clear();
                _orderSubcriptions.Clear();

                SendLogMessage("Start BitMart Connection", LogMessageType.System);

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

        public ServerType ServerType => ServerType.BitMart;

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
            // https://api-cloud.bitmart.com/spot/v1/symbols/details

            string endPoint = "/spot/v1/symbols/details";

            try
            {
                HttpResponseMessage response = _restClient.Get(endPoint, secured: false);

                string content = response.Content.ReadAsStringAsync().Result;
                BitMartBaseMessage parsed =
                    JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (parsed != null && parsed.data != null && parsed.data.ContainsKey("symbols"))
                    {
                        string symbols = parsed.data["symbols"].ToString();

                        //SendLogMessage("symbols: " + symbols, LogMessageType.Connect);

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
                    newSecurity.NameId = item.symbol_id;

                    if (item.trade_status == "trading")
                    {
                        newSecurity.State = SecurityStateType.Activ;
                    }
                    newSecurity.Decimals =  Convert.ToInt32(item.price_max_precision);
                    newSecurity.DecimalsVolume = GetDecimalsVolume(item.quote_increment);
                    newSecurity.PriceStep = GetPriceStep( newSecurity.Decimals );
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.Lot = 1;
                    newSecurity.SecurityType = SecurityType.CurrencyPair;
                    newSecurity.Exchange = ServerType.BitMart.ToString();
                    newSecurity.MinTradeAmount = item.min_buy_amount.ToDecimal() ;

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

        private string PortfolioName = "BitMart";

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

        private readonly HashSet<int> _allowedTf = new HashSet<int> {1,3,5,15,30,45,60,120,240,1440,10080,43200};

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

            string endPoint = "/spot/v1/symbols/kline?symbol=" + security.Name;
            
            //Начало отрезка времени (UTC) в формате Unix Time Seconds
            endPoint += "&step=" + tfTotalMinutes;
            endPoint += "&from=" + TimeManager.GetTimeStampSecondsToDateTime(startTime);
            endPoint += "&to=" + TimeManager.GetTimeStampSecondsToDateTime(endTime);

            //SendLogMessage("Get Candles: " + endPoint, LogMessageType.Connect);

            try
            {
                HttpResponseMessage response = _restClient.Get(endPoint, secured: false);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string content = response.Content.ReadAsStringAsync().Result;
                    BitMartBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartBaseMessage());

                    if (parsed != null && parsed.data != null && parsed.data.ContainsKey("klines"))
                    {
                        string history = parsed.data["klines"].ToString();
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
                //SendLogMessage("Candles request error:" + endPoint + ",  " + exception.ToString(), LogMessageType.Connect);
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
                newCandle.Open = curCandle.open.ToDecimal();
                newCandle.High = curCandle.high.ToDecimal();
                newCandle.Low = curCandle.low.ToDecimal();
                newCandle.Close = curCandle.close.ToDecimal();
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

        private readonly string _wsPublic = "wss://ws-manager-compress.bitmart.com/api?protocol=1.1";

        private readonly string _wsPrivate = "wss://ws-manager-compress.bitmart.com/user?protocol=1.1";

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
                    _webSocketData.AutoSendPingInterval = 15;
                    _webSocketData.Opened += WebSocketData_Opened;
                    _webSocketData.Closed += WebSocketData_Closed;
                    _webSocketData.MessageReceived += WebSocketData_MessageReceived;
                    _webSocketData.Error += WebSocketData_Error;
                    _webSocketData.Open();


                    _webSocketPortfolio = new WebSocket(_wsPrivate);
                    
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
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(), LogMessageType.Error);
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
            catch (Exception exeption)
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
            //SendLogMessage("Porfolio Send: " + message, LogMessageType.Connect);
            _webSocketPortfolio.Send(message);
        }

        private readonly object _orderSubcriptionsLock = new object();
        HashSet<string> _orderSubcriptions = new HashSet<string>();

        private void SubcribeToOrderData(string symbol)
        {
            if (symbol == null) 
            {
                return; 
            }

            lock (this._orderSubcriptionsLock)
            {
                if (_orderSubcriptions.Contains(symbol))
                {
                    return;
                }
                _orderSubcriptions.Add(symbol);
            }

            WSRequestOrder obj = new WSRequestOrder(symbol);
            string message = obj.GetJson();
            SendLogMessage("SubcribeToOrderData: " + message, LogMessageType.Connect);
            _webSocketPortfolio.Send(message);

            Thread.Sleep(1500);
        }

        private void ActivateCurrentPortfolioListening()
        {
            AuthInSocket();

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

            ActivateCurrentPortfolioListening();

            //Subscribe to all current securities
            GetAllOrdersFromExchange();
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
            catch (Exception exeption)
            {
                SendLogMessage(exeption.ToString(),LogMessageType.Error);
            }
        }

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
                        || string.IsNullOrEmpty(baseMessage.table))
                    {
                        continue;
                    }

                    if (baseMessage.table.Contains("/depth"))
                    {
                        UpDateMarketDepth(message);

                    }
                    else if (baseMessage.table.Contains("/trade"))
                    {
                        UpDateTrade(message);
                    }
                    else
                    {
                        SendLogMessage("Unknown message: " + baseMessage.table, LogMessageType.Error);
                    }

                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
                    Thread.Sleep(2000);
                }
            }
        }

        private void UpDateTrade(string data)
        {
            MarketQuotesMessage baseMessage =
            JsonConvert.DeserializeAnonymousType(data, new MarketQuotesMessage());

            if (baseMessage == null || baseMessage.data == null || baseMessage.data.Count == 0) 
            {
                SendLogMessage("Wrong 'Trade' message:" + data , LogMessageType.Error);
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
                } else
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

        private void UpDateMarketDepth(string data)
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
                    depth.Time = _lastMdTime.AddMilliseconds(1);
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
                        UpDateMyPortfolio(baseMessage.data.ToString());

                    }
                    else if (baseMessage.table.Contains("/user/order"))
                    {
                        UpDateMyOrder(baseMessage.data.ToString());
                    }
                    else
                    {
                        SendLogMessage("Unknown message: " + baseMessage.table, LogMessageType.Error);
                    }

                }
                catch (Exception exeption)
                {
                    SendLogMessage(exeption.ToString(), LogMessageType.Error);
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

        private void UpDateMyOrder(string data)
        {
            //SendLogMessage("UpDateMyOrder: " + data, LogMessageType.Connect);

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
                    (order.State == OrderStateType.Done || order.State == OrderStateType.Patrial))
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
            order.ServerType = ServerType.BitMart;

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
                order.State = OrderStateType.Activ;
            }
            else if (baseOrder.order_state == "filled")
            {
                order.State = OrderStateType.Done;
            }
            else if (baseOrder.order_state == "partially_filled")
            {
                order.State = OrderStateType.Patrial;
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

        private void UpDateMyPortfolio(string data)
        {
            //https://developer-pro.bitmart.com/en/spot/#private-balance-change

            BitMartPortfolioSocket porfMessage =
            JsonConvert.DeserializeAnonymousType(data, new BitMartPortfolioSocket());

            Portfolio portf = null;
            if (_myPortfolious != null && _myPortfolious.Count > 0)
            {
                portf = _myPortfolious[0];
            }

            if(portf == null)
            {
                return;
            }

            if (porfMessage !=  null && porfMessage.Count > 0 && porfMessage[0].balance_details.Count > 0) 
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
                SubcribeToOrderData(order.SecurityNameCode);

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
                        
                    } else
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

            if(order.Side == Side.Buy)
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

        public void CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            try { 

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

            for (int i = 0; i < orders.Count;i++)
            {
                Order order = orders[i];

                if(order.State == OrderStateType.Activ)
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

                if (order.State == OrderStateType.Activ
                    && order.SecurityNameCode == security.Name)
                {
                    CancelOrder(order);
                }
            }
        }

        private List<Order> GetAllOrdersFromExchange()
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string endPoint = "/spot/v4/query/open-orders";

                HttpResponseMessage response = _restClient.Post(endPoint,
                            "{ \"orderMode\": \"spot\" }",
                            secured: true);

                if (response.StatusCode == HttpStatusCode.OK)
                {

                    string content = response.Content.ReadAsStringAsync().Result;
                    BitMartRestOrdersBaseMessage parsed =
                        JsonConvert.DeserializeAnonymousType(content, new BitMartRestOrdersBaseMessage());

                    if (parsed != null && parsed.data != null)
                    {
                        //Everything is OK
                        BitMartRestOrders orders = JsonConvert.DeserializeAnonymousType(parsed.data.ToString(), new BitMartRestOrders());

                        List<Order> osEngineOrders = new List<Order>();

                        for (int i = 0; i < orders.Count; i++)
                        {
                            Order newOrd = ConvertRestOrdersToOsEngineOrder(orders[i]);

                            if (newOrd == null)
                            {
                                continue;
                            }

                            osEngineOrders.Add(newOrd);

                            SubcribeToOrderData(newOrd.SecurityNameCode);
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

        public void GetOrderStatus(Order order)
        {
            Order myOrder = GetOrderFromExchange(order.NumberUser.ToString());

            if (myOrder == null)
            {
                return;
            }

            MyOrderEvent?.Invoke(myOrder);

            if (myOrder.State == OrderStateType.Done || myOrder.State == OrderStateType.Patrial)
            {
                UpdateTrades(myOrder);
            }
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
                order.State = OrderStateType.Activ;
            }
            else if (baseOrder.state == "partially_filled")
            {
                order.State = OrderStateType.Patrial;
            }


            if (baseOrder.state == "new")
            {
                order.State = OrderStateType.Activ;
            }
            else if (baseOrder.state == "filled")
            {
                order.State = OrderStateType.Done;
            }
            else if (baseOrder.state == "partially_filled")
            {
                order.State = OrderStateType.Patrial;
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

        #endregion
    }
}