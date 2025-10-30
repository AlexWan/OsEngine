/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.KuCoin.KuCoinFutures.Json;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


namespace OsEngine.Market.Servers.KuCoin.KuCoinFutures
{
    public class KuCoinFuturesServer : AServer
    {
        public KuCoinFuturesServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            KuCoinFuturesServerRealization realization = new KuCoinFuturesServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterPassphrase, "");
            CreateParameterBoolean("HedgeMode", true);
            ServerParameters[3].ValueChange += KuCoinFuturesServer_ValueChange;
            CreateParameterEnum("Margin Mode", "Cross", new List<string> { "Cross", "Isolated" });
            CreateParameterString("Leverage", "1");
            CreateParameterBoolean("Extended Data", false);

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label271;
            ServerParameters[3].Comment = OsLocalization.Market.Label250;
            ServerParameters[4].Comment = OsLocalization.Market.Label249;
            ServerParameters[5].Comment = OsLocalization.Market.Label256;
            ServerParameters[6].Comment = OsLocalization.Market.Label270;
        }

        private void KuCoinFuturesServer_ValueChange()
        {
            ((KuCoinFuturesServerRealization)ServerRealization).HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;
        }
    }

    public class KuCoinFuturesServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public KuCoinFuturesServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadForPublicMessages = new Thread(PublicMessageReader);
            threadForPublicMessages.IsBackground = true;
            threadForPublicMessages.Name = "PublicMessageReaderKuCoin";
            threadForPublicMessages.Start();

            Thread threadForPrivateMessages = new Thread(PrivateMessageReader);
            threadForPrivateMessages.IsBackground = true;
            threadForPrivateMessages.Name = "PrivateMessageReaderKuCoin";
            threadForPrivateMessages.Start();

            Thread threadCheckAliveWebSocket = new Thread(CheckAliveWebSocket);
            threadCheckAliveWebSocket.IsBackground = true;
            threadCheckAliveWebSocket.Name = "CheckAliveWebSocketKuCoinFutures";
            threadCheckAliveWebSocket.Start();

            Thread threadGetPortfolios = new Thread(ThreadGetPortfolios);
            threadGetPortfolios.IsBackground = true;
            threadGetPortfolios.Name = "ThreadKuCoinFuturesPortfolios";
            threadGetPortfolios.Start();

            Thread threadExtendedData = new Thread(ThreadExtendedData);
            threadExtendedData.IsBackground = true;
            threadExtendedData.Name = "ThreadKuCoinFuturesExtendedData";
            threadExtendedData.Start();
        }

        public void Connect(WebProxy proxy)
        {
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _passphrase = ((ServerParameterPassword)ServerParameters[2]).Value;
            HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;

            if (string.IsNullOrEmpty(_publicKey) ||
            string.IsNullOrEmpty(_secretKey) ||
            string.IsNullOrEmpty(_passphrase))
            {
                SendLogMessage("Can`t run KuCoin Futures connector. No keys or passphrase",
                    LogMessageType.Error);
                return;
            }

            if (((ServerParameterEnum)ServerParameters[4]).Value == "Cross")
            {
                _marginMode = "CROSS";
            }
            else
            {
                _marginMode = "ISOLATED";
            }

            _leverage = ((ServerParameterString)ServerParameters[5]).Value;

            if (((ServerParameterBool)ServerParameters[6]).Value == true)
            {
                _extendedMarketData = true;
            }
            else
            {
                _extendedMarketData = false;
            }

            try
            {
                RestRequest requestRest = new RestRequest("/api/v1/timestamp", Method.GET);
                IRestResponse response = new RestClient(_baseUrl).Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
                }
                else
                {
                    SendLogMessage("Connection cannot be open. KuCoinFutures. Error request", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                Disconnect();
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            try
            {
                DeleteWebsocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _subscribedSecurities.Clear();

            _webSocketPublicMessages = new ConcurrentQueue<string>();
            _webSocketPrivateMessages = new ConcurrentQueue<string>();

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

        public ServerType ServerType
        {
            get { return ServerType.KuCoinFutures; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _publicKey;

        private string _secretKey;

        private string _passphrase;

        private string _baseUrl = "https://api-futures.kucoin.com";

        private bool _extendedMarketData;

        private string _marginMode;

        private string _leverage;

        private List<string> _listCurrency = new List<string>() { "XBT", "ETH", "USDC", "USDT", "SOL", "DOT", "XRP" }; // list of currencies on the exchange

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

        public void SetPositionMode()
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                Dictionary<string, string> mode = new Dictionary<string, string>();
                mode["positionMode"] = "0";

                if (HedgeMode)
                {
                    mode["positionMode"] = "1";
                }

                string jsonRequest = JsonConvert.SerializeObject(mode);

                IRestResponse responseMessage = CreatePrivateQuery("/api/v2/position/switchPositionMode", Method.POST, jsonRequest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {

                }
                else
                {
                    SendLogMessage($"PositionMode error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);

            }
        }

        #endregion

        #region 3 Securities

        private RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            _rateGateSecurity.WaitToProceed();

            try
            {
                string requestStr = $"/api/v1/contracts/active";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                IRestResponse responseMessage = new RestClient(_baseUrl).Execute(requestRest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<List<ResponseSymbol>> symbols = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<List<ResponseSymbol>>());

                    if (symbols.code.Equals("200000") == true)
                    {
                        for (int i = 0; i < symbols.data.Count; i++)
                        {
                            ResponseSymbol item = symbols.data[i];

                            if (item.status.Equals("Open"))
                            {
                                Security newSecurity = new Security();

                                newSecurity.Exchange = ServerType.KuCoinFutures.ToString();
                                newSecurity.State = SecurityStateType.Activ;
                                newSecurity.Name = item.symbol;
                                newSecurity.NameFull = item.symbol;

                                if (item.isInverse == "true")
                                {
                                    newSecurity.NameClass = "Inverse_" + item.quoteCurrency;
                                }
                                else
                                {
                                    newSecurity.NameClass = item.quoteCurrency;
                                }

                                newSecurity.NameId = item.symbol;
                                newSecurity.SecurityType = SecurityType.Futures;

                                newSecurity.PriceStep = item.tickSize.ToDecimal();
                                newSecurity.PriceStepCost = newSecurity.PriceStep;
                                newSecurity.Lot = item.lotSize.ToDecimal() /* Math.Abs(item.multiplier.ToDecimal())*/;

                                newSecurity.Decimals = item.tickSize.DecimalsCount();
                                newSecurity.DecimalsVolume = item.multiplier.DecimalsCount();
                                newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                                newSecurity.MinTradeAmount = Math.Abs(item.multiplier.ToDecimal());
                                newSecurity.VolumeStep = Math.Abs(item.multiplier.ToDecimal());

                                _securities.Add(newSecurity);
                            }
                        }

                        SecurityEvent(_securities);
                    }
                    else
                    {
                        SendLogMessage($"Securities error: {symbols.code} || Message: {symbols.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Securities request error: {responseMessage.Content}", LogMessageType.Error);
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

        public List<Portfolio> Portfolios;

        private void ThreadGetPortfolios()
        {
            Thread.Sleep(10000);

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                    continue;
                }

                try
                {
                    Thread.Sleep(15000);

                    for (int i = 0; i < _listCurrency.Count; i++)
                    {
                        CreatePortfolio(false, _listCurrency[i]); // create portfolios from a list of currencies
                    }

                    CreatePositions(false);
                    GetUSDTMasterPortfolio(false);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }
        }

        public void GetPortfolios()
        {
            if (Portfolios == null)
            {
                Portfolios = new List<Portfolio>();

                Portfolio portfolioInitial = new Portfolio();
                portfolioInitial.Number = "KuCoinFutures";
                portfolioInitial.ValueBegin = 1;
                portfolioInitial.ValueCurrent = 1;
                portfolioInitial.ValueBlocked = 0;

                Portfolios.Add(portfolioInitial);

                PortfolioEvent(Portfolios);
            }

            for (int i = 0; i < _listCurrency.Count; i++)
            {
                CreatePortfolio(true, _listCurrency[i]); // create portfolios from a list of currencies
            }

            CreatePositions(true);
            GetUSDTMasterPortfolio(true);
        }

        private void CreatePortfolio(bool IsUpdateValueBegin, string currency = "USDT")
        {
            try
            {
                string path = $"/api/v1/account-overview";
                string requestStr = $"{path}?currency={currency}";

                IRestResponse responseMessage = CreatePrivateQuery(requestStr, Method.GET, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseAsset> asset = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<ResponseAsset>());

                    if (asset.code == "200000")
                    {
                        Portfolio portfolio = Portfolios[0];

                        ResponseAsset item = asset.data;

                        PositionOnBoard pos = new PositionOnBoard();

                        pos.PortfolioName = "KuCoinFutures";
                        pos.SecurityNameCode = item.currency;
                        pos.ValueCurrent = item.availableBalance.ToDecimal();
                        pos.ValueBlocked = item.orderMargin.ToDecimal();

                        if (IsUpdateValueBegin)
                        {
                            pos.ValueBegin = item.availableBalance.ToDecimal();
                        }

                        portfolio.SetNewPosition(pos);
                        PortfolioEvent(Portfolios);
                    }
                    else
                    {
                        SendLogMessage($"Portfolio error: {asset.code}\n" + $"Message: {asset.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Portfolio request error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void CreatePositions(bool IsUpdateValueBegin)
        {
            try
            {
                IRestResponse responseMessage = CreatePrivateQuery("/api/v1/positions", Method.GET, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<List<ResponsePosition>> assets = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<List<ResponsePosition>>());

                    if (assets.code == "200000")
                    {
                        Portfolio portfolio = Portfolios[0];

                        for (int i = 0; i < assets.data.Count; i++)
                        {
                            ResponsePosition item = assets.data[i];
                            PositionOnBoard pos = new PositionOnBoard();

                            pos.PortfolioName = "KuCoinFutures";

                            if (item.positionSide == "LONG")
                            {
                                pos.SecurityNameCode = item.symbol + "_LONG";
                            }
                            else if (item.positionSide == "SHORT")
                            {
                                pos.SecurityNameCode = item.symbol + "_SHORT";
                            }
                            else
                            {
                                pos.SecurityNameCode = item.symbol;
                            }

                            pos.UnrealizedPnl = item.unrealisedPnl.ToDecimal();
                            pos.ValueCurrent = item.currentQty.ToDecimal() * GetVolume(item.symbol);

                            if (IsUpdateValueBegin)
                            {
                                pos.ValueBegin = item.currentQty.ToDecimal() * GetVolume(item.symbol);
                            }

                            portfolio.SetNewPosition(pos);
                        }

                        PortfolioEvent(Portfolios);
                    }
                    else
                    {
                        SendLogMessage($"Positions error: {assets.code}\n" + $"Message: {assets.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Positions error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Positions request error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void GetUSDTMasterPortfolio(bool IsUpdateValueBegin)
        {
            Portfolio portfolio = Portfolios[0];

            List<PositionOnBoard> positionOnBoard = Portfolios[0].GetPositionOnBoard();

            if (positionOnBoard == null)
            {
                return;
            }

            decimal positionInUSDT = 0;
            decimal sizeUSDT = 0;

            for (int i = 0; i < positionOnBoard.Count; i++)
            {
                if (positionOnBoard[i].SecurityNameCode == "USDT")
                {
                    sizeUSDT = positionOnBoard[i].ValueCurrent;
                }
                else if (positionOnBoard[i].SecurityNameCode.Contains("USDTM")
                    || positionOnBoard[i].SecurityNameCode.Contains("USDCM")
                    || positionOnBoard[i].SecurityNameCode.Contains("USDM"))
                {
                    //positionInUSDT += GetPriceSecurity(positionOnBoard[i].SecurityNameCode)  * positionOnBoard[i].ValueCurrent;
                }
                else
                {
                    positionInUSDT += GetPriceSecurity(positionOnBoard[i].SecurityNameCode + "USDTM") * positionOnBoard[i].ValueCurrent;
                }
            }

            if (IsUpdateValueBegin)
            {
                portfolio.ValueBegin = Math.Round(sizeUSDT + positionInUSDT, 4);
            }

            portfolio.ValueCurrent = Math.Round(sizeUSDT + positionInUSDT, 4);

            if (portfolio.ValueCurrent == 0)
            {
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;
            }
        }

        private decimal GetPriceSecurity(string security)
        {
            try
            {
                string path = $"/api/v1/ticker";
                string requestStr = $"{path}?symbol={security}";

                IRestResponse responseMessage = CreatePrivateQuery(requestStr, Method.GET, null);
                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code == "200000")
                    {
                        ResponseMessageRest<Ticker> ticker = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<Ticker>());

                        decimal priceSecurity = ticker.data.price.ToDecimal();

                        return priceSecurity;
                    }
                    else
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n" + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"GetPriceSecurity> Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.code != null)
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n" + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return 0;
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime timeEnd = DateTime.UtcNow;
            DateTime timeStart = timeEnd.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime != actualTime)
            {
                startTime = actualTime;
            }

            int countNeedToLoad = GetCountCandlesFromSliceTime(startTime, endTime, timeFrameBuilder.TimeFrameTimeSpan);

            return GetCandleHistory(security.NameFull, timeFrameBuilder.TimeFrameTimeSpan, true, countNeedToLoad, endTime);
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool IsOsData, int CountToLoad, DateTime timeEnd)
        {
            // From technical support chat: Right now the kucoin servers are only returning 24 hours for the lower timeframes and no more than 30 days for higher timeframes. Hopefully their new API fixes this, but no word yet when this will be.

            int needToLoadCandles = CountToLoad;

            List<Candle> candles = new List<Candle>();

            DateTime fromTime = timeEnd - TimeSpan.FromMinutes(tf.TotalMinutes * CountToLoad);

            const int KuCoinFuturesDataLimit = 200; // KuCoin limitation: For each query, the system would return at most 200 pieces of data. To obtain more data, please page the data by time.

            do
            {
                int limit = needToLoadCandles;

                if (needToLoadCandles > KuCoinFuturesDataLimit)
                {
                    limit = KuCoinFuturesDataLimit;

                }
                else
                {
                    limit = needToLoadCandles;
                }

                List<Candle> rangeCandles = new List<Candle>();

                DateTime slidingFrom = timeEnd - TimeSpan.FromMinutes(tf.TotalMinutes * limit);
                rangeCandles = CreateQueryCandles(nameSec, GetStringInterval(tf), slidingFrom, timeEnd);

                if (rangeCandles == null)
                    return null; // no data

                candles.InsertRange(0, rangeCandles);

                if (rangeCandles.Count < KuCoinFuturesDataLimit) // hard limit
                {
                    if ((candles.Count > rangeCandles.Count) && (candles[rangeCandles.Count].TimeStart == candles[rangeCandles.Count - 1].TimeStart))
                    { // HACK: exchange returns one element twice when data in the past ends
                        candles.RemoveAt(rangeCandles.Count);
                    }

                    // this happens when the server does not provide new data further into the past
                    return candles;
                }

                if (candles.Count != 0)
                {
                    timeEnd = candles[0].TimeStart;
                }

                needToLoadCandles -= limit;
            } while (needToLoadCandles > 0);


            return candles;
        }

        private readonly RateGate _rateGateCandleHistory = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<Candle> CreateQueryCandles(string nameSec, string stringInterval, DateTime timeFrom, DateTime timeTo)
        {
            try
            {
                _rateGateCandleHistory.WaitToProceed();

                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(timeFrom);
                long to = TimeManager.GetTimeStampMilliSecondsToDateTime(timeTo);
                string requestStr = $"/api/v1/kline/query?symbol={nameSec}&granularity={stringInterval}&from={from}&to={to}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                IRestResponse responseMessage = new RestClient(_baseUrl).Execute(requestRest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<List<List<string>>> symbols = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<List<List<string>>>());

                    if (symbols.code.Equals("200000") == true)
                    {
                        List<Candle> candles = new List<Candle>();

                        for (int i = 0; i < symbols.data.Count; i++)
                        {
                            List<string> item = symbols.data[i];

                            Candle newCandle = new Candle();

                            newCandle.Open = item[1].ToDecimal();
                            newCandle.Close = item[4].ToDecimal();
                            newCandle.High = item[2].ToDecimal();
                            newCandle.Low = item[3].ToDecimal();
                            newCandle.Volume = item[5].ToDecimal();
                            newCandle.State = CandleState.Finished;
                            newCandle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item[0]));
                            candles.Add(newCandle);
                        }

                        return candles;
                    }
                    else
                    {
                        SendLogMessage($"Code: {symbols.code}\n"
                            + $"Message: {symbols.msg}", LogMessageType.Error);
                        return null;
                    }
                }
                else
                {
                    SendLogMessage($"CreateQueryCandles> State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
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

        private string GetStringInterval(TimeSpan tf)
        {
            // The granularity (granularity parameter of K-line) represents the number of minutes, the available granularity scope is: 1,5,15,30,60,120,240,480,720,1440,10080. Requests beyond the above range will be rejected.
            if (tf.Minutes != 0)
            {
                return $"{tf.Minutes}";
            }
            else
            {
                return $"{tf.TotalMinutes}";
            }
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private WebSocket _webSocketPrivate;

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private string _webSocketPrivateUrl = "wss://ws-api-futures.kucoin.com/?token=xxx&[connectId=xxxxx]";

        private string _webSocketPublicUrl = "wss://ws-api-futures.kucoin.com/?token=xxx&[connectId=xxxxx]";

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (_webSocketPublicMessages == null)
                {
                    _webSocketPublicMessages = new ConcurrentQueue<string>();
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
                IRestResponse responseMessage = CreatePrivateQuery("/api/v1/bullet-public", Method.POST, String.Empty);

                if (responseMessage.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage("KuCoin public keys are wrong. Message from server: " + responseMessage.Content, LogMessageType.Error);
                    return null;
                }

                ResponsePrivateWebSocketConnection wsResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponsePrivateWebSocketConnection());

                // set dynamic server address ws
                _webSocketPublicUrl = wsResponse.data.instanceServers[0].endpoint + "?token=" + wsResponse.data.token;

                WebSocket webSocketPublicNew = new WebSocket(_webSocketPublicUrl);
                /*webSocketPublicNew.SslConfiguration.EnabledSslProtocols
                   = System.Security.Authentication.SslProtocols.Tls12;*/

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += _webSocketPublic_OnOpen;
                webSocketPublicNew.OnMessage += _webSocketPublic_OnMessage;
                webSocketPublicNew.OnError += _webSocketPublic_OnError;
                webSocketPublicNew.OnClose += _webSocketPublic_OnClose;
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
            // 1. get websocket address
            IRestResponse responseMessage = CreatePrivateQuery("/api/v1/bullet-private", Method.POST, String.Empty);

            if (responseMessage.StatusCode != HttpStatusCode.OK)
            {
                SendLogMessage("KuCoin private keys are wrong. Message from server: " + responseMessage.Content, LogMessageType.Error);
                return;
            }

            string JsonResponse = responseMessage.Content;

            ResponsePrivateWebSocketConnection wsResponse = JsonConvert.DeserializeAnonymousType(JsonResponse, new ResponsePrivateWebSocketConnection());

            // set dynamic server address ws
            _webSocketPrivateUrl = wsResponse.data.instanceServers[0].endpoint + "?token=" + wsResponse.data.token;

            _webSocketPrivate = new WebSocket(_webSocketPrivateUrl);

            /*_webSocketPrivate.SslConfiguration.EnabledSslProtocols
                = System.Security.Authentication.SslProtocols.Tls12;*/

            _webSocketPrivate.EmitOnPing = true;
            _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
            _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
            _webSocketPrivate.OnError += _webSocketPrivate_OnError;
            _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
            _webSocketPrivate.ConnectAsync();
        }

        private string _lockerCheckActivateionSockets = "lockerCheckActivateionSocketsKuCoinFutures";

        private void CheckActivationSockets()
        {
            lock (_lockerCheckActivateionSockets)
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

        private void DeleteWebsocketConnection()
        {

            if (_webSocketPublic != null)
            {
                try
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        webSocketPublic.OnOpen -= _webSocketPublic_OnOpen;
                        webSocketPublic.OnClose -= _webSocketPublic_OnClose;
                        webSocketPublic.OnMessage -= _webSocketPublic_OnMessage;
                        webSocketPublic.OnError -= _webSocketPublic_OnError;

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
                    _webSocketPrivate.OnMessage -= _webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError -= _webSocketPrivate_OnError;
                    _webSocketPrivate.OnClose -= _webSocketPrivate_OnClose;
                    _webSocketPrivate.CloseAsync();
                }
                catch
                {
                    // ignore
                }

                _webSocketPrivate = null;
            }
        }

        #endregion

        #region 7 WebSocket events

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

                if (e == null
                    || e.Data == null
                    || e.Data.Length == 0)
                {
                    return;
                }

                if (_webSocketPublicMessages == null)
                {
                    return;
                }

                _webSocketPublicMessages.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPublic_OnOpen(object sender, EventArgs e)
        {
            SendLogMessage("Connection to public data is Open", LogMessageType.System);
            CheckActivationSockets();
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

                if (e == null
                    || e.Data == null
                    || e.Data.Length == 0)
                {
                    return;
                }

                if (_webSocketPrivateMessages == null)
                {
                    return;
                }

                _webSocketPrivateMessages.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
        {
            SendLogMessage("Connection to private data is Open", LogMessageType.System);

            CheckActivationSockets();

            _webSocketPrivate.SendAsync($"{{\"type\": \"subscribe\", \"privateChannel\": \"true\", \"topic\": \"/contractMarket/tradeOrders\"}}");
            _webSocketPrivate.SendAsync($"{{\"type\": \"subscribe\", \"privateChannel\": \"true\", \"topic\": \"/contractAccount/wallet\"}}");
        }

        #endregion

        #region 8 WebSocket check alive

        private void CheckAliveWebSocket()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(10000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_webSocketPrivate != null && _webSocketPrivate.ReadyState == WebSocketState.Open ||
                        _webSocketPrivate.ReadyState == WebSocketState.Connecting)
                    {
                        _webSocketPrivate.SendAsync($"{{\"type\": \"ping\"}}");
                    }
                    else
                    {
                        Disconnect();
                    }

                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.SendAsync($"{{\"type\": \"ping\"}}");
                        }
                        else
                        {
                            Disconnect();
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        #endregion

        #region 9 Security Subscribed

        private RateGate _rateGateSubscribed = new RateGate(1, TimeSpan.FromMilliseconds(220));

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribed.WaitToProceed();

                CreateSubscribedSecurityMessageWebSocket(security);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private List<string> _subscribedSecurities = new List<string>();

        private void CreateSubscribedSecurityMessageWebSocket(Security security)
        {
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
                && _subscribedSecurities.Count % 100 == 0)
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
                webSocketPublic.SendAsync($"{{\"type\": \"subscribe\",\"topic\": \"/contractMarket/execution:{security.Name}\"}}");
                webSocketPublic.SendAsync($"{{\"type\": \"subscribe\",\"topic\": \"/contractMarket/level2Depth5:{security.Name}\"}}");

                if (_extendedMarketData)
                {
                    webSocketPublic.SendAsync($"{{\"type\": \"subscribe\",\"topic\": \"/contract/instrument:{security.Name}\"}}");
                    GetFundingHistory(security.Name);
                }
            }

            if (_webSocketPrivate != null)
            {
                _webSocketPrivate.SendAsync($"{{\"type\": \"subscribe\", \"privateChannel\": \"true\", \"topic\": \"/contract/position:{security.Name}\"}}");
            }
        }

        private void GetFundingHistory(string name)
        {
            try
            {
                DateTime timeEnd = DateTime.UtcNow;
                DateTime timeStart = timeEnd.AddDays(-2);

                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(timeStart);
                long to = TimeManager.GetTimeStampMilliSecondsToDateTime(timeEnd);
                string requestStr = $"/api/v1/contract/funding-rates?symbol={name}&from={from}&to={to}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                IRestResponse responseMessage = new RestClient(_baseUrl).Execute(requestRest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<List<FundingItemHistory>> responseFunding = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<List<FundingItemHistory>>());

                    if (responseFunding.code == "200000")
                    {
                        FundingItemHistory item = responseFunding.data[0];

                        Funding data = new Funding();

                        data.SecurityNameCode = item.symbol;
                        data.PreviousFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)item.timepoint.ToDecimal());

                        FundingUpdateEvent?.Invoke(data);
                    }
                    else
                    {
                        SendLogMessage($"GetFundingHistory> - Code: {responseFunding.code} - {responseFunding.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"GetFundingHistory> State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
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
                                    for (int i2 = 0; i2 < _subscribedSecurities.Count; i2++)
                                    {
                                        string securityName = _subscribedSecurities[i2];

                                        webSocketPublic.SendAsync($"{{\"type\": \"unsubscribe\",\"topic\": \"/contractMarket/execution:{securityName}\"}}");
                                        webSocketPublic.SendAsync($"{{\"type\": \"unsubscribe\",\"topic\": \"/contractMarket/level2Depth5:{securityName}\"}}"); // marketDepth
                                        _webSocketPrivate.SendAsync($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/contract/position:{securityName}\"}}"); // change of positions

                                        if (_extendedMarketData)
                                        {
                                            webSocketPublic.SendAsync($"{{\"type\": \"unsubscribe\",\"topic\": \"/contract/instrument:{securityName}\"}}");
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
                    _webSocketPrivate.SendAsync($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/contractMarket/tradeOrders\"}}"); // changing orders
                    _webSocketPrivate.SendAsync($"{{\"type\": \"unsubscribe\", \"privateChannel\": \"true\", \"topic\": \"/contractAccount/wallet\"}}"); // portfolio change
                }
                catch
                {
                    // ignore
                }
            }
        }

        private List<OpenInterestData> _openInterest = new List<OpenInterestData>();

        private DateTime _timeLastUpdateExtendedData = DateTime.Now;

        private readonly RateGate _rateGateOpenInterest = new RateGate(1, TimeSpan.FromMilliseconds(350));

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
            _rateGateOpenInterest.WaitToProceed();

            try
            {
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    string requestStr = $"/api/v1/contracts/{_subscribedSecurities[i]}";

                    RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                    IRestResponse responseMessage = new RestClient(_baseUrl).Execute(requestRest);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        ResponseMessageRest<ResponseSymbol> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<ResponseSymbol>());

                        if (stateResponse.code == "200000")
                        {
                            ResponseSymbol item = stateResponse.data;

                            OpenInterestData openInterestData = new OpenInterestData();

                            openInterestData.SecutityName = item.symbol;

                            if (stateResponse.data.openInterest != null)
                            {
                                openInterestData.OpenInterestValue = stateResponse.data.openInterest;

                                bool isInArray = false;

                                for (int j = 0; j < _openInterest.Count; j++)
                                {
                                    if (_openInterest[j].SecutityName == openInterestData.SecutityName)
                                    {
                                        _openInterest[j].OpenInterestValue = openInterestData.OpenInterestValue;
                                        isInArray = true;
                                        break;
                                    }
                                }

                                if (isInArray == false)
                                {
                                    _openInterest.Add(openInterestData);
                                }
                            }

                            Funding funding = new Funding();

                            funding.SecurityNameCode = item.symbol;
                            funding.NextFundingTime = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.nextFundingRateDateTime));
                            funding.MinFundingRate = item.fundingRateFloor.ToDecimal();
                            funding.MaxFundingRate = item.fundingRateCap.ToDecimal();
                            funding.FundingIntervalHours = int.Parse(item.fundingRateGranularity) / 60000 / 60;

                            FundingUpdateEvent?.Invoke(funding);

                            SecurityVolumes volume = new SecurityVolumes();

                            volume.SecurityNameCode = item.symbol;
                            volume.Volume24h = item.volumeOf24h.ToDecimal();
                            volume.Volume24hUSDT = item.turnoverOf24h.ToDecimal();

                            Volume24hUpdateEvent?.Invoke(volume);
                        }
                        else
                        {
                            SendLogMessage($"GetOpenInterest> - Code: {stateResponse.code} - {stateResponse.msg}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"GetOpenInterest> - Code: {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> _webSocketPublicMessages = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _webSocketPrivateMessages = new ConcurrentQueue<string>();

        private void PublicMessageReader()
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

                    if (_webSocketPublicMessages.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    _webSocketPublicMessages.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Equals("pong"))
                    {
                        continue;
                    }

                    ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                    if (action.subject != null && action.type != "welcome")
                    {
                        if (action.subject.Equals("level2"))
                        {
                            UpdateDepth(message);
                            continue;
                        }
                        else if (action.subject.Equals("match"))
                        {
                            UpdateTrade(message);
                            continue;
                        }
                        else if (action.subject.Equals("funding.rate"))
                        {
                            UpdateFundingRate(message);
                            continue;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void PrivateMessageReader()
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

                    if (_webSocketPrivateMessages.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    _webSocketPrivateMessages.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Equals("pong"))
                    {
                        continue;
                    }

                    ResponseWebSocketMessageAction<object> action = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<object>());

                    if (action.subject != null && action.type != "welcome")
                    {
                        if (action.subject.Equals("orderChange"))
                        {
                            UpdateOrder(message);
                            continue;
                        }

                        if (action.subject.Equals("symbolOrderChange"))
                        {
                            UpdateOrder(message);
                            continue;
                        }

                        if (action.subject.Equals("position.change"))
                        {
                            UpdatePosition(message);
                            continue;
                        }

                        if (action.subject.Equals("walletBalance.change"))
                        {
                            UpdatePortfolio(message);
                            continue;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<ResponseWebSocketMessageTrade> responseTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketMessageTrade>());

                if (responseTrade == null
                    || responseTrade.data == null)
                {
                    return;
                }

                Trade trade = new Trade();
                trade.SecurityNameCode = responseTrade.data.symbol;
                trade.Price = responseTrade.data.price.ToDecimal();
                trade.Id = responseTrade.data.tradeId;
                trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data.ts) / 1000000); // from nanoseconds to ms))
                trade.Volume = responseTrade.data.size.ToDecimal();

                if (responseTrade.data.side == "sell")
                {
                    trade.Side = Side.Sell;
                }
                else //(responseTrade.data.side == "buy")
                {
                    trade.Side = Side.Buy;
                }

                if (_extendedMarketData)
                {
                    trade.OpenInterest = GetOpenInterestValue(trade.SecurityNameCode);
                }

                NewTradesEvent(trade);
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

        private void UpdateDepth(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<ResponseWebSocketDepthItem> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketDepthItem>());

                if (responseDepth.data == null)
                {
                    return;
                }

                MarketDepth marketDepth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                marketDepth.SecurityNameCode = responseDepth.topic.Split(':')[1];

                for (int i = 0; i < responseDepth.data.asks.Count; i++)
                {
                    MarketDepthLevel newMDLevel = new MarketDepthLevel();
                    newMDLevel.Ask = responseDepth.data.asks[i][1].ToDouble();
                    newMDLevel.Price = responseDepth.data.asks[i][0].ToDouble();
                    ascs.Add(newMDLevel);
                }

                for (int i = 0; i < responseDepth.data.bids.Count; i++)
                {
                    MarketDepthLevel newMDLevel = new MarketDepthLevel();
                    newMDLevel.Bid = responseDepth.data.bids[i][1].ToDouble();
                    newMDLevel.Price = responseDepth.data.bids[i][0].ToDouble();

                    bids.Add(newMDLevel);
                }

                marketDepth.Asks = ascs;
                marketDepth.Bids = bids;

                marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.data.timestamp));

                MarketDepthEvent(marketDepth);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<ResponseWebSocketPortfolio> Portfolio = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketPortfolio>());

                Portfolio portfolio = Portfolios[0];

                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "KuCoinFutures";
                pos.SecurityNameCode = Portfolio.data.currency;
                pos.ValueCurrent = Portfolio.data.availableBalance.ToDecimal();

                portfolio.SetNewPosition(pos);
                PortfolioEvent(Portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePosition(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<ResponseWebSocketPosition> posResponse = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketPosition>());

                Portfolio portfolio = Portfolios[0];

                ResponseWebSocketPosition data = posResponse.data;

                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = "KuCoinFutures";

                if (data.positionSide == "LONG")
                {
                    pos.SecurityNameCode = data.symbol + "_LONG";
                }
                else if (data.positionSide == "SHORT")
                {
                    pos.SecurityNameCode = data.symbol + "_SHORT";
                }
                else
                {
                    pos.SecurityNameCode = data.symbol;
                }

                pos.ValueCurrent = data.currentQty.ToDecimal() * GetVolume(data.symbol);
                portfolio.SetNewPosition(pos);
                PortfolioEvent(Portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<ResponseWebSocketOrder> Order = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<ResponseWebSocketOrder>());

                if (Order.data == null)
                {
                    return;
                }

                ResponseWebSocketOrder item = Order.data;

                OrderStateType stateType = GetOrderState(item.status, item.type);

                if (item.orderType != null && item.orderType.Equals("market") && stateType == OrderStateType.Active)
                {
                    return;
                }

                Order newOrder = new Order();
                newOrder.SecurityNameCode = item.symbol;

                if (item.clientOid != null)
                {
                    try
                    {
                        newOrder.NumberUser = Convert.ToInt32(item.clientOid);
                    }
                    catch
                    {
                        SendLogMessage("Strange order num: " + item.clientOid, LogMessageType.Error);
                        return;
                    }
                }

                newOrder.NumberMarket = item.orderId;

                OrderPriceType.TryParse(item.orderType, true, out newOrder.TypeOrder);
                newOrder.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
                newOrder.Volume = item.size.Replace('.', ',').ToDecimal() * GetVolume(newOrder.SecurityNameCode);
                //newOrder.VolumeExecute = item.remainSize.ToDecimal();
                newOrder.Price = item.price != null ? item.price.Replace('.', ',').ToDecimal() : 0;
                newOrder.State = stateType;
                newOrder.ServerType = ServerType.KuCoinFutures;
                newOrder.PortfolioNumber = "KuCoinFutures";
                newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts) / 1000000);
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts) / 1000000);

                if (newOrder.State == OrderStateType.Cancel)
                {
                    newOrder.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts) / 1000000);
                }

                if (newOrder.State == OrderStateType.Done)
                {
                    newOrder.TimeDone = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts) / 1000000);
                }

                if (newOrder.State == OrderStateType.Partial)
                {
                    MyTrade myTrade = new MyTrade();

                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts) / 1000000); //from nanoseconds to ms
                    myTrade.NumberOrderParent = item.orderId;
                    myTrade.NumberTrade = item.tradeId;
                    myTrade.Price = item.matchPrice.ToDecimal();
                    myTrade.SecurityNameCode = item.symbol;
                    myTrade.Side = item.side.Equals("buy") ? Side.Buy : Side.Sell;
                    myTrade.Volume = item.matchSize.ToDecimal() * GetVolume(item.symbol);

                    MyTradeEvent(myTrade);
                }

                MyOrderEvent(newOrder);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private OrderStateType GetOrderState(string status, string type)
        {
            if (type == "open")
            {
                return OrderStateType.Active;
            }
            else if (type == "match")
            {
                return OrderStateType.Partial;
            }
            else if (type == "filled")
            {
                return OrderStateType.Done;
            }
            else if (type == "canceled")
            {
                return OrderStateType.Cancel;
            }

            return OrderStateType.None;
        }

        private void UpdateFundingRate(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<FundingItem> responseFunding = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<FundingItem>());

                if (responseFunding == null
                    || responseFunding.data == null)
                {
                    return;
                }

                Funding funding = new Funding();

                funding.SecurityNameCode = responseFunding.topic.Split(':')[1];
                funding.CurrentValue = responseFunding.data.fundingRate.ToDecimal() * 100;
                funding.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)responseFunding.data.timestamp.ToDecimal());

                FundingUpdateEvent?.Invoke(funding);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent;

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 11 Trade

        public void SendOrder(Order order)
        {
            try
            {
                string posSide = "BOTH";

                if (HedgeMode)
                {
                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        posSide = order.Side == Side.Buy ? "SHORT" : "LONG";
                    }
                    else
                    {
                        posSide = order.Side == Side.Buy ? "LONG" : "SHORT";
                    }
                }

                SendOrderRequestData data = new SendOrderRequestData();
                data.clientOid = order.NumberUser.ToString();
                data.symbol = order.SecurityNameCode;
                data.side = order.Side.ToString().ToLower();
                data.type = order.TypeOrder.ToString().ToLower();
                data.price = order.TypeOrder == OrderPriceType.Market ? null : order.Price.ToString().Replace(",", ".");

                decimal volume = order.Volume / GetVolume(order.SecurityNameCode);
                data.size = volume.ToString().Replace(",", ".");
                data.leverage = _leverage;
                data.positionSide = posSide;
                data.marginMode = _marginMode;

                JsonSerializerSettings dataSerializerSettings = new JsonSerializerSettings();
                dataSerializerSettings.NullValueHandling = NullValueHandling.Ignore;// if it's a market order, then we ignore the price parameter

                string jsonRequest = JsonConvert.SerializeObject(data, dataSerializerSettings);

                // for the test you can use "/api/v1/orders/test"
                IRestResponse responseMessage = CreatePrivateQueryOrders("/api/v1/orders", Method.POST, jsonRequest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponsePlaceOrder> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<ResponsePlaceOrder>());

                    if (stateResponse.code.Equals("200000") == true)
                    {
                        //SendLogMessage($"Order num {order.NumberUser} on exchange.", LogMessageType.Trade);
                        //order.State = OrderStateType.Active;
                        //order.NumberMarket = stateResponse.data.orderId;

                        //if (MyOrderEvent != null)
                        //{
                        //    MyOrderEvent(order);
                        //}
                    }
                    else
                    {
                        if (responseMessage.Content.Contains("No open positions to close"))
                        {
                            // ignore
                        }
                        else
                        {
                            CreateOrderFail(order);
                            SendLogMessage($"Order Fail: {stateResponse.code} Message: {stateResponse.msg}", LogMessageType.Error);
                        }
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Order Fail. Status: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);

                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Order send error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private decimal GetVolume(string securityName)
        {
            decimal minVolume = 1;

            for (int i = 0; i < _securities.Count; i++)
            {
                if (_securities[i].Name == securityName)
                {
                    minVolume = _securities[i].MinTradeAmount;
                }
            }

            if (minVolume <= 0)
            {
                return 1;
            }

            return minVolume;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                CancelAllOrdersRequestData data = new CancelAllOrdersRequestData();
                data.symbol = security.Name;

                string jsonRequest = JsonConvert.SerializeObject(data);

                IRestResponse responseMessage = CreatePrivateQueryOrders("/api/v1/orders", Method.DELETE, jsonRequest);
                ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<object>());

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    if (stateResponse.code.Equals("200000") == true)
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"CancelAllOrdersToSecurity> Http State Code: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);

                    if (stateResponse != null && stateResponse.code != null)
                    {
                        SendLogMessage($"Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            try
            {
                IRestResponse responseMessage = CreatePrivateQueryOrders("/api/v1/orders/" + order.NumberMarket, Method.DELETE, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<object> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<object>());

                    if (stateResponse.code.Equals("200000") == true)
                    {
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Cancel order failed: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                            return false;
                        }
                        else
                        {
                            if (responseMessage.Content.Contains("The order cannot be canceled"))
                            {
                                // 
                            }
                            else
                            {
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    OrderStateType state = GetOrderStatus(order);

                    if (state == OrderStateType.None)
                    {
                        SendLogMessage("Cancel order failed. Status: " + responseMessage.StatusCode + "  " + order.SecurityNameCode + ", " + responseMessage.Content, LogMessageType.Error);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
            return false;
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOpenOrders();

            for (int i = 0; orders != null && i < orders.Count; i++)
            {
                if (orders[i] == null)
                {
                    continue;
                }

                if (orders[i].State != OrderStateType.Active
                    && orders[i].State != OrderStateType.Partial
                    && orders[i].State != OrderStateType.Pending)
                {
                    continue;
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        private List<Order> GetAllOpenOrders()
        {
            try
            {
                string path = $"/api/v1/orders";
                string requestStr = $"{path}?status=active";

                IRestResponse responseMessage = CreatePrivateQueryOrders(requestStr, Method.GET, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseAllOrders> order = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<ResponseAllOrders>());

                    if (order.code == "200000")
                    {
                        List<Order> orders = new List<Order>();

                        for (int i = 0; i < order.data.items.Count; i++)
                        {
                            if (order.data.items[i].isActive == "false")
                            {
                                continue;
                            }

                            Order newOrder = new Order();

                            newOrder.SecurityNameCode = order.data.items[i].symbol;
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.items[i].updatedAt));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.items[i].orderTime) / 1000000); //from nanoseconds to ms
                            newOrder.ServerType = ServerType.KuCoinFutures;

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(order.data.items[i].clientOid);
                            }
                            catch
                            {

                            }

                            newOrder.NumberMarket = order.data.items[i].id;
                            newOrder.Side = order.data.items[i].side.Equals("buy") ? Side.Buy : Side.Sell;

                            if (order.data.items[i].type == "market")
                            {
                                newOrder.TypeOrder = OrderPriceType.Market;
                            }
                            if (order.data.items[i].type == "limit")
                            {
                                newOrder.TypeOrder = OrderPriceType.Limit;
                            }

                            if (order.data.items[i].status == "open")
                            {
                                newOrder.State = OrderStateType.Active;
                            }
                            else if (order.data.items[i].status == "done")
                            {
                                newOrder.State = OrderStateType.Done;
                            }

                            newOrder.Volume = order.data.items[i].size == null ? order.data.items[i].filledSize.Replace('.', ',').ToDecimal() * GetVolume(newOrder.SecurityNameCode) : order.data.items[i].size.Replace('.', ',').ToDecimal() * GetVolume(newOrder.SecurityNameCode);

                            newOrder.Price = order.data.items[i].price != null ? order.data.items[i].price.Replace('.', ',').ToDecimal() : 0;
                            newOrder.PortfolioNumber = "KuCoinFutures";

                            orders.Add(newOrder);
                        }
                        return orders;
                    }
                    else
                    {
                        SendLogMessage($"GetAllOpenOrders> Code: {order.code}\n" + $"Message: {order.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"GetAllOpenOrders>: {responseMessage.StatusCode}\n" + $"Message: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            Order orderFromExchange = GetOrderFromExchange(order.SecurityNameCode, order.NumberMarket, order.NumberUser.ToString());

            if (orderFromExchange == null)
            {
                return OrderStateType.None;
            }

            Order orderOnMarket = null;

            if (order.NumberUser != 0
                && orderFromExchange.NumberUser != 0
                && orderFromExchange.NumberUser == order.NumberUser)
            {
                orderOnMarket = orderFromExchange;
            }

            if (string.IsNullOrEmpty(order.NumberMarket) == false
                && order.NumberMarket == orderFromExchange.NumberMarket)
            {
                orderOnMarket = orderFromExchange;
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
                GetMyTradesBySecurity(order.SecurityNameCode, order.NumberMarket);
            }

            return orderOnMarket.State;
        }

        private Order GetOrderFromExchange(string securityNameCode, string numberMarket, string numberUser)
        {
            try
            {
                string path = null;

                if (numberMarket != null
                    && numberMarket != "")
                {
                    path = $"/api/v1/orders/{numberMarket}";
                }
                else
                {
                    path = $"/api/v1/orders/byClientOid?clientOid={numberUser}";
                }

                if (path == null)
                {
                    return null;
                }

                IRestResponse responseMessage = CreatePrivateQueryOrders(path, Method.GET, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseOrder> order = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<ResponseOrder>());

                    if (order.code == "200000")
                    {
                        Order newOrder = new Order();

                        if (order.data != null)
                        {
                            newOrder.SecurityNameCode = order.data.symbol;
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.updatedAt));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.data.orderTime) / 1000000); //from nanoseconds to ms
                            newOrder.ServerType = ServerType.KuCoinFutures;

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(order.data.clientOid);
                            }
                            catch
                            {

                            }

                            if (order.data.type == "market")
                            {
                                newOrder.TypeOrder = OrderPriceType.Market;
                            }
                            if (order.data.type == "limit")
                            {
                                newOrder.TypeOrder = OrderPriceType.Limit;
                            }

                            newOrder.NumberMarket = order.data.id;
                            newOrder.Side = order.data.side.Equals("buy") ? Side.Buy : Side.Sell;

                            if (order.data.status == "open")
                            {
                                newOrder.State = OrderStateType.Active;
                            }
                            else if (order.data.status == "done")
                            {
                                newOrder.State = OrderStateType.Done;
                            }

                            if (newOrder.State == OrderStateType.Done)
                            {
                                newOrder.TimeDone = newOrder.TimeCreate;
                                newOrder.TimeCancel = newOrder.TimeCreate;
                            }

                            newOrder.Volume = order.data.size == null ? order.data.filledSize.Replace('.', ',').ToDecimal() * GetVolume(newOrder.SecurityNameCode) : order.data.size.Replace('.', ',').ToDecimal() * GetVolume(newOrder.SecurityNameCode);
                            newOrder.Price = order.data.price != null ? order.data.price.Replace('.', ',').ToDecimal() : 0;
                            newOrder.PortfolioNumber = "KuCoinFutures";
                        }

                        return newOrder;
                    }
                    else
                    {
                        if (responseMessage.Content.Contains("error.getOrder.orderNotExist"))
                        {
                            // ignore
                        }
                        else
                        {
                            SendLogMessage($"GetOrderFromExchange> Code: {order.code}\n" + $"Message: {order.msg}", LogMessageType.Error);
                        }
                    }
                }
                else
                {
                    SendLogMessage($"GetOrderFromExchange>: {responseMessage.StatusCode}\n" + $"Message: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private void GetMyTradesBySecurity(string nameSec, string OrdId)
        {
            try
            {
                string path = $"/api/v1/fills";
                string requestStr = $"{path}?symbol={nameSec}&orderId={OrdId}";

                IRestResponse responseMessage = CreatePrivateQueryOrders(requestStr, Method.GET, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseMessageRest<ResponseMyTrades> responseMyTrades = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseMessageRest<ResponseMyTrades>());

                    if (responseMyTrades.code.Equals("200000") == true)
                    {
                        for (int i = 0; i < responseMyTrades.data.items.Count; i++)
                        {
                            ResponseMyTrade responseT = responseMyTrades.data.items[i];

                            MyTrade myTrade = new MyTrade();

                            myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseT.tradeTime) / 1000000); //from nanoseconds to ms
                            myTrade.NumberOrderParent = responseT.orderId;
                            myTrade.NumberTrade = responseT.tradeId;
                            myTrade.Price = responseT.price.ToDecimal();
                            myTrade.SecurityNameCode = responseT.symbol;
                            myTrade.Side = responseT.side.Equals("buy") ? Side.Buy : Side.Sell;
                            myTrade.Volume = responseT.size.ToDecimal() * GetVolume(myTrade.SecurityNameCode);

                            MyTradeEvent(myTrade);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Get my trades request error: {responseMyTrades.code} Message: {responseMyTrades.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Get my trades by security error: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get my trades by security request error." + exception.ToString(), LogMessageType.Error);
            }
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

        private RateGate _rateGate = new RateGate(20, TimeSpan.FromMilliseconds(300));

        private IRestResponse CreatePrivateQueryOrders(string path, Method method, string body)
        {
            _rateGate.WaitToProceed();

            try
            {
                string requestPath = path;
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature(timestamp, method.ToString(), path, body, _secretKey);
                string signaturePartner = GenerateSignaturePartner(timestamp);

                RestRequest requestRest = new RestRequest(path, method);
                requestRest.AddHeader("KC-API-KEY", _publicKey);
                requestRest.AddHeader("KC-API-SIGN", signature);
                requestRest.AddHeader("KC-API-TIMESTAMP", timestamp);
                requestRest.AddHeader("KC-API-PASSPHRASE", SignHMACSHA256(_passphrase, _secretKey));
                requestRest.AddHeader("KC-API-PARTNER", "VANTECHNOLOGIESFUTURES");
                requestRest.AddHeader("KC-API-PARTNER-SIGN", signaturePartner);
                requestRest.AddHeader("KC-BROKER-NAME", "VANTECHNOLOGIESFUTURES");
                requestRest.AddHeader("KC-API-PARTNER-VERIFY", "true");
                requestRest.AddHeader("KC-API-KEY-VERSION", "2");

                if (body != null)
                {
                    requestRest.AddParameter("application/json", body, ParameterType.RequestBody);
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

        private IRestResponse CreatePrivateQuery(string path, Method method, string body)
        {
            _rateGate.WaitToProceed();

            try
            {
                string requestPath = path;
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature(timestamp, method.ToString(), path, body, _secretKey);

                RestRequest requestRest = new RestRequest(path, method);
                requestRest.AddHeader("KC-API-KEY", _publicKey);
                requestRest.AddHeader("KC-API-SIGN", signature);
                requestRest.AddHeader("KC-API-TIMESTAMP", timestamp);
                requestRest.AddHeader("KC-API-PASSPHRASE", SignHMACSHA256(_passphrase, _secretKey));
                requestRest.AddHeader("KC-API-KEY-VERSION", "2");

                if (body != null)
                {
                    requestRest.AddParameter("application/json", body, ParameterType.RequestBody);
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

        private string SignHMACSHA256(string data, string secretKey)
        {
            byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            using (HMACSHA256 hmac = new HMACSHA256(secretKeyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(hashBytes);
            }
        }

        private string GenerateSignature(string timestamp, string method, string requestPath, string body, string secretKey)
        {
            method = method.ToUpper();
            body = string.IsNullOrEmpty(body) ? string.Empty : body;
            string preHash = timestamp + method + Uri.UnescapeDataString(requestPath) + body;

            return SignHMACSHA256(preHash, secretKey);
        }

        private string GenerateSignaturePartner(string timestamp)
        {
            string preHash = timestamp + "VANTECHNOLOGIESFUTURES" + _publicKey;

            return SignHMACSHA256(preHash, "e5b87448-971c-4d2b-b5e8-80b51e9d9bd7");
        }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent(message, messageType);
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