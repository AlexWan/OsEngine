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
using OsEngine.Market.Servers.GateIo.GateIoSpot.Entities;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


namespace OsEngine.Market.Servers.GateIo.GateIoSpot
{
    public class GateIoServerSpot : AServer
    {
        public GateIoServerSpot(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            ServerRealization = new GateIoServerSpotRealization();

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterBoolean("Extended Data", false);

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label269;
        }
    }

    public sealed class GateIoServerSpotRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public GateIoServerSpotRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread thread = new Thread(CheckAliveWebSocket);
            thread.Name = "CheckAliveWebSocket";
            thread.Start();

            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.Name = "MessageReaderPublic";
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.Name = "MessageReaderPrivate";
            threadMessageReaderPrivate.Start();

            Thread threadMarketDepthParsing = new Thread(ThreadMarketDepthParsing);
            threadMarketDepthParsing.Name = "ThreadMarketDepthParsingGateIoSpot";
            threadMarketDepthParsing.Start();

            Thread threadTradesParsing = new Thread(ThreadTradesParsing);
            threadTradesParsing.Name = "ThreadTradesParsingGateIoSpot";
            threadTradesParsing.Start();
        }

        public DateTime ServerTime { get; set; }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy)
        {
            _myProxy = proxy;
            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(_publicKey) ||
                string.IsNullOrEmpty(_secretKey))
            {
                SendLogMessage("Can`t run GateIo Spot connector. No keys",
                    LogMessageType.Error);
                return;
            }

            if (((ServerParameterBool)ServerParameters[2]).Value == true)
            {
                _extendedMarketData = true;
            }
            else
            {
                _extendedMarketData = false;
            }

            try
            {
                IRestResponse responseMessage = null;
                RestClient client = new RestClient(HttpUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                int tryCounter = 0;

                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        RestRequest requestRest = new RestRequest("/spot/time", Method.GET);
                        responseMessage = client.Execute(requestRest);
                        break;
                    }
                    catch (Exception e)
                    {
                        tryCounter++;

                        if (tryCounter >= 3)
                        {
                            SendLogMessage(e.Message, LogMessageType.Connect);
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                            return;
                        }
                    }
                }

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
                }
                else
                {
                    SendLogMessage("Connection can`t be open. GateIoSpot. Error request", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
                _subscribedSecurities.Clear();
                _allDepths.Clear();

                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }

            _fIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            _fIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
            _queueTrades = new ConcurrentQueue<string>();
            _queueMarketDepths = new ConcurrentQueue<string>();

            _isBalanceSubscribed = false;

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

        public ServerType ServerType
        {
            get { return ServerType.GateIoSpot; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        public bool IsCompletelyDeleted { get; set; }

        #endregion

        #region 2 Properties

        private string _host = "https://api.gateio.ws";

        private string _prefix = "/api/v4";

        private string HttpUrl = "https://api.gateio.ws/api/v4";

        public List<IServerParameter> ServerParameters { get; set; }

        private string _publicKey;

        private string _secretKey;

        private bool _extendedMarketData;

        #endregion

        #region 3 Securities

        private Dictionary<string, Security> _securitiesDict = new Dictionary<string, Security>();

        private RateGate _rateGateSecurities = new RateGate(1, TimeSpan.FromMilliseconds(60));

        public void GetSecurities()
        {
            _rateGateSecurities.WaitToProceed();

            if (_securitiesDict == null)
            {
                _securitiesDict = new Dictionary<string, Security>();
            }

            try
            {
                RestRequest requestRest = new RestRequest("/spot/currency_pairs", Method.GET);

                RestClient client = new RestClient(HttpUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse responseMessage = client.Execute(requestRest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    List<CurrencyPair> currencyPairs = JsonConvert.DeserializeAnonymousType<List<CurrencyPair>>(responseMessage.Content, new List<CurrencyPair>());

                    List<Security> securities = new List<Security>();

                    for (int i = 0; i < currencyPairs.Count; i++)
                    {
                        CurrencyPair current = currencyPairs[i];

                        if (current.trade_status != "tradable")
                        {
                            continue;
                        }

                        Security security = new Security();
                        security.Exchange = nameof(ServerType.GateIoSpot);
                        security.State = SecurityStateType.Activ;
                        security.Name = current.id;
                        security.NameFull = current.id;
                        security.NameClass = current.quote;
                        security.NameId = current.id;

                        security.SecurityType = SecurityType.CurrencyPair;

                        security.DecimalsVolume = Int32.Parse(current.amount_precision);
                        security.Lot = 1;
                        security.Decimals = Int32.Parse(current.precision);
                        security.PriceStep = security.Decimals.GetValueByDecimals();
                        security.PriceStepCost = security.PriceStep;

                        if (current.min_base_amount != null)
                        {
                            security.VolumeStep = current.min_base_amount.ToDecimal();
                        }

                        security.MinTradeAmount = current.min_quote_amount.ToDecimal();
                        security.MinTradeAmountType = MinTradeAmountType.C_Currency;

                        securities.Add(security);
                    }

                    securities = securities.OrderBy(s => s.Name).ToList();

                    foreach (Security sec in securities)
                    {
                        _securitiesDict[sec.Name] = sec;
                    }

                    SecurityEvent?.Invoke(securities);
                }
                else
                {
                    SendLogMessage($"Securities error. Code: {responseMessage.StatusCode}, {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(150));

        public void GetPortfolios()
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                string method = "GET";
                string url = "/spot/accounts";
                string query_param = "";
                string bodyParam = "";

                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                string bodyHash = GetHash(bodyParam);
                string signString = $"{method}\n{_prefix}{url}\n{query_param}\n{bodyHash}\n{timeStamp}";
                string sign = GetHashHMAC(signString, _secretKey);

                string fullUrl = $"{_host}{_prefix}{url}";
                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(Method.GET);
                request.AddHeader("KEY", _publicKey);
                request.AddHeader("SIGN", sign);
                request.AddHeader("Timestamp", timeStamp);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    List<GetCurrencyVolumeResponse> getCurrencyVolumeResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new List<GetCurrencyVolumeResponse>());

                    UpdatePortfolio(getCurrencyVolumeResponse);
                }
                else
                {
                    if (responseMessage.Content.Contains("\"INVALID_KEY\"")
                        || responseMessage.Content.Contains("\"INVALID_SIGNATURE\""))
                    {
                        Disconnect();
                    }

                    SendLogMessage($"Portfolio> Http State Code: {responseMessage.StatusCode}, {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }


        private Portfolio _myPortfolio;

        private void UpdatePortfolio(List<GetCurrencyVolumeResponse> getCurrencyVolumeResponse)
        {
            try
            {
                _myPortfolio = new Portfolio();

                _myPortfolio.Number = "GateIO_Spot";
                _myPortfolio.ValueBegin = 1;
                _myPortfolio.ValueCurrent = 1;

                if (getCurrencyVolumeResponse == null || getCurrencyVolumeResponse.Count == 0)
                {
                    PortfolioEvent(new List<Portfolio> { _myPortfolio });
                    return;
                }

                for (int i = 0; i < getCurrencyVolumeResponse.Count; i++)
                {
                    PositionOnBoard newPortf = new PositionOnBoard();
                    newPortf.SecurityNameCode = getCurrencyVolumeResponse[i].currency;
                    newPortf.ValueBegin = Math.Round(getCurrencyVolumeResponse[i].available.ToDecimal(), 5);
                    newPortf.ValueCurrent = Math.Round(getCurrencyVolumeResponse[i].available.ToDecimal(), 5);
                    newPortf.ValueBlocked = Math.Round(getCurrencyVolumeResponse[i].locked.ToDecimal(), 5);
                    _myPortfolio.SetNewPosition(newPortf);
                }

                PortfolioEvent?.Invoke(new List<Portfolio> { _myPortfolio });
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
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

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

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

            int timeRange = tfTotalMinutes * 10000;

            DateTime maxStartTime = DateTime.UtcNow.AddMinutes(-timeRange);

            if (maxStartTime > startTime)
            {
                SendLogMessage("Maximum interval is 9990 candles from today!", LogMessageType.Error);
                return null;
            }

            DateTime startTimeData = startTime;
            DateTime partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 999);

            do
            {
                int from = TimeManager.GetTimeStampSecondsToDateTime(startTimeData);
                int to = TimeManager.GetTimeStampSecondsToDateTime(partEndTime);

                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

                List<Candle> candles = RequestCandleHistory(security.Name, interval, from, to);

                if (candles == null || candles.Count == 0)
                {
                    break;
                }

                Candle last = candles[candles.Count - 1];

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

                startTimeData = partEndTime.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
                partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 999);

                if (startTimeData >= DateTime.UtcNow)
                {
                    break;
                }

                if (partEndTime > DateTime.UtcNow)
                {
                    partEndTime = DateTime.UtcNow;
                }

            } while (true);

            return allCandles;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (endTime > DateTime.UtcNow ||
                startTime >= endTime ||
                startTime >= DateTime.UtcNow ||
                actualTime > endTime ||
                actualTime > DateTime.UtcNow)
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
                timeFrameMinutes == 30 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 240)
            {
                return true;
            }

            return false;
        }

        private string GetInterval(TimeSpan timeFrame)
        {
            if (timeFrame.Minutes != 0)
            {
                return $"{timeFrame.Minutes}m";
            }
            else if (timeFrame.Hours != 0)
            {
                return $"{timeFrame.Hours}h";
            }
            else
            {
                return $"{timeFrame.Days}d";
            }
        }

        private RateGate _rateGateData = new RateGate(1, TimeSpan.FromMilliseconds(150));

        private List<Candle> RequestCandleHistory(string security, string interval, int fromTimeStamp, int toTimeStamp)
        {
            _rateGateData.WaitToProceed();

            try
            {
                string queryParam = $"currency_pair={security}&";
                queryParam += $"interval={interval}&";
                queryParam += $"from={fromTimeStamp}&";
                queryParam += $"to={toTimeStamp}";

                string requestStr = "/spot/candlesticks?" + queryParam;
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(HttpUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse responseMessage = client.Execute(requestRest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    List<string[]> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new List<string[]>());

                    List<Candle> candles = new List<Candle>();

                    for (int i = 0; i < response.Count; i++)
                    {
                        string[] current = response[i];

                        if (CheckCandlesToZeroData(current))
                        {
                            continue;
                        }

                        Candle candle = new Candle();

                        candle.State = CandleState.Finished;
                        candle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(int.Parse(current[0]));
                        candle.Volume = current[1].ToDecimal();
                        candle.Close = current[2].ToDecimal();
                        candle.High = current[3].ToDecimal();
                        candle.Low = current[4].ToDecimal();
                        candle.Open = current[5].ToDecimal();

                        candles.Add(candle);
                    }

                    return candles;
                }
                else
                {
                    SendLogMessage($"CandleHistory error. Code: {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }

            return null;
        }

        private bool CheckCandlesToZeroData(string[] item)
        {
            if (item[5].ToDecimal() == 0 ||
                item[2].ToDecimal() == 0 ||
                item[3].ToDecimal() == 0 ||
                item[4].ToDecimal() == 0)
            {
                return true;
            }

            return false;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
            /*
            if (startTime < DateTime.UtcNow.AddYears(-3) ||
                endTime < DateTime.UtcNow.AddYears(-3) ||
                !CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            List<Trade> allTrades = GetNeedRange(security.Name, startTime, endTime);

            return ClearTrades(allTrades);*/
        }

        private List<Trade> GetNeedRange(string security, DateTime startTime, DateTime endTime)
        {
            try
            {
                long initTimeStamp = TimeManager.GetTimeStampSecondsToDateTime(DateTime.UtcNow);

                List<Trade> trades = GetTickDataFrom(security, initTimeStamp);

                if (trades == null)
                {
                    return null;
                }

                List<Trade> allTrades = new List<Trade>(100000);

                allTrades.AddRange(trades);

                Trade firstRange = trades[trades.Count - 1];

                while (firstRange.Time > startTime)
                {
                    int ts = TimeManager.GetTimeStampSecondsToDateTime(firstRange.Time);
                    trades = GetTickDataById(security, long.Parse(firstRange.Id));

                    if (trades.Count == 0)
                    {
                        break;
                    }

                    firstRange = trades[trades.Count - 1];
                    allTrades.AddRange(trades);
                }

                allTrades.Reverse();

                List<Trade> allNeedTrades = allTrades.FindAll(t => t.Time >= startTime && t.Time <= endTime);

                return allNeedTrades;
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }

            return null;
        }

        private List<Trade> GetTickDataFrom(string security, long startTimeStamp)
        {
            string queryParam = $"currency_pair={security}&";
            queryParam += "limit=1000&";
            queryParam += $"to={startTimeStamp}";

            string requestStr = "/spot/trades?" + queryParam;

            return Execute(requestStr);
        }

        private List<Trade> Execute(string requestStr)
        {
            try
            {
                _rateGateData.WaitToProceed();

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(HttpUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse responseMessage = client.Execute(requestRest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    List<ApiEntities> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new List<ApiEntities>());

                    return ConvertTrades(response);
                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }

            return null;
        }

        private List<Trade> GetTickDataById(string security, long lastId)
        {
            try
            {
                _rateGateData.WaitToProceed();

                string queryParam = $"currency_pair={security}&";
                queryParam += "limit=1000&";
                queryParam += $"last_id={lastId}&";
                queryParam += $"reverse=true";
                string requestStr = "/spot/trades?" + queryParam;

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(HttpUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse responseMessage = client.Execute(requestRest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    List<ApiEntities> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new List<ApiEntities>());

                    return ConvertTrades(response);
                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }

            return null;
        }

        private List<Trade> ConvertTrades(List<ApiEntities> tradeResponse)
        {
            List<Trade> trades = new List<Trade>();

            for (int i = 0; i < tradeResponse.Count; i++)
            {
                ApiEntities current = tradeResponse[i];

                Trade trade = new Trade();

                trade.Id = current.id;
                trade.Price = current.price.ToDecimal();
                trade.Volume = current.amount.ToDecimal();
                trade.SecurityNameCode = current.currency_pair;
                trade.Side = current.side == "buy" ? Side.Buy : Side.Sell;

                long timeMs = long.Parse(current.create_time_ms.Split('.')[0]);

                trade.Time = TimeManager.GetDateTimeFromTimeStamp(timeMs);

                trades.Add(trade);
            }

            return trades;
        }

        private List<Trade> ClearTrades(List<Trade> trades)
        {
            List<Trade> newTrades = new List<Trade>();

            Trade last = null;

            for (int i = 0; i < trades.Count; i++)
            {
                Trade current = trades[i];

                if (last != null)
                {
                    if (current.Id == last.Id && current.Time == last.Time)
                    {
                        continue;
                    }
                }

                newTrades.Add(current);

                last = current;
            }

            return newTrades;
        }

        #endregion

        #region 6 WebSocket creation

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

        private string WEB_SOCKET_URL = "wss://api.gateio.ws/ws/v4/";

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (_fIFOListWebSocketPublicMessage == null)
                {
                    _fIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
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
                WebSocket webSocketPublicNew = new WebSocket(WEB_SOCKET_URL);

                if (_myProxy != null)
                {
                    webSocketPublicNew.SetProxy(_myProxy);
                }

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

                _webSocketPrivate = new WebSocket(WEB_SOCKET_URL);

                if (_myProxy != null)
                {
                    _webSocketPrivate.SetProxy(_myProxy);
                }

                _webSocketPrivate.EmitOnPing = true;
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
                        webSocketPublic.OnMessage -= WebSocketPublicNew_OnMessage;
                        webSocketPublic.OnError -= WebSocketPublicNew_OnError;
                        webSocketPublic.OnClose -= WebSocketPublicNew_OnClose;

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

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublicNew_OnClose(object sender, CloseEventArgs e)
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

        private void WebSocketPublicNew_OnError(object sender, ErrorEventArgs e)
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

                    if (message.Contains("The remote party closed the GateIoSpot WebSocket connection"))
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

        private void WebSocketPublicNew_OnMessage(object sender, MessageEventArgs e)
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

                if (_fIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                if (e.Data.Contains("spot.pong"))
                { // pong message
                    return;
                }

                if (e.Data.Contains("payload"))
                {
                    return;
                }

                _fIFOListWebSocketPublicMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("GateIoSpot WebSocket Public connection open", LogMessageType.System);
                    CheckSocketsActivate();
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

                    if (message.Contains("The remote party closed the GateIoSpot WebSocket connection"))
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

                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (_fIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                if (e.Data.Contains("spot.pong"))
                { // pong message
                    return;
                }

                if (e.Data.Contains("payload"))
                {
                    return;
                }

                _fIFOListWebSocketPrivateMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
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
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("GateIoSpot WebSocket Private connection open", LogMessageType.System);
                    CheckSocketsActivate();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket check alive

        private void CheckAliveWebSocket()
        {
            while (true)
            {
                try
                {
                    if (IsCompletelyDeleted == true)
                    {
                        return;
                    }

                    Thread.Sleep(14000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        if (webSocketPublic != null
                            && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            long time = TimeManager.GetUnixTimeStampSeconds();
                            webSocketPublic.SendAsync($"{{\"time\":{time},\"channel\":\"spot.ping\"}}");
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
                        long time = TimeManager.GetUnixTimeStampSeconds();
                        _webSocketPrivate.SendAsync($"{{\"time\":{time},\"channel\":\"spot.ping\"}}");
                    }
                    else
                    {
                        Disconnect();
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        #endregion

        #region 9 WebSocket security subscribe

        private readonly Dictionary<string, Security> _subscribedSecurities = new Dictionary<string, Security>();

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private bool _isBalanceSubscribed = false;

        public void Subscribe(Security security)
        {
            _rateGateSubscribe.WaitToProceed();

            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (!_subscribedSecurities.ContainsKey(security.Name))
                {
                    _subscribedSecurities.Add(security.Name, security);
                }

                if (_webSocketPublic.Count == 0)
                {
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecurities.Count != 0
                    && _subscribedSecurities.Count % 20 == 0)
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
                    SubscribeMarketDepth(security.Name, webSocketPublic);
                    SubscribeTrades(security.Name, webSocketPublic);

                    if (_extendedMarketData)
                    {
                        SubscribeTicker(security.Name, webSocketPublic);
                    }
                }

                if (_webSocketPrivate != null)
                {
                    if (!_isBalanceSubscribed)
                    {
                        SubscribePortfolio();
                        _isBalanceSubscribed = true;
                    }

                    SubscribeOrders(security.Name);
                    SubscribeUserTrades(security.Name);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void SubscribeTicker(string security, WebSocket webSocketPublic)
        {
            try
            {
                long time = TimeManager.GetUnixTimeStampSeconds();
                webSocketPublic?.SendAsync($"{{\"time\":{time},\"channel\":\"spot.tickers\",\"event\":\"subscribe\",\"payload\":[\"{security}\"]}}");
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void SubscribeMarketDepth(string security, WebSocket webSocketPublic)
        {
            try
            {
                AddMarketDepth(security);

                string level = "5";

                if (((ServerParameterBool)ServerParameters[10]).Value == true)
                {
                    level = "20";
                }

                long time = TimeManager.GetUnixTimeStampSeconds();
                webSocketPublic?.SendAsync($"{{\"time\":{time},\"channel\":\"spot.order_book\",\"event\":\"subscribe\",\"payload\":[\"{security}\",\"{level}\",\"100ms\"]}}");
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void AddMarketDepth(string name)
        {
            if (!_allDepths.ContainsKey(name))
            {
                _allDepths.Add(name, new MarketDepth());
            }
        }

        private void SubscribeTrades(string security, WebSocket webSocketPublic)
        {
            try
            {
                long time = TimeManager.GetUnixTimeStampSeconds();
                webSocketPublic?.SendAsync($"{{\"time\":{time},\"channel\":\"spot.trades\",\"event\":\"subscribe\",\"payload\":[\"{security}\"]}}");
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void SubscribeOrders(string security)
        {
            try
            {
                long timeStamp = TimeManager.GetUnixTimeStampSeconds();
                string param = $"channel=spot.orders&event=subscribe&time={timeStamp}";
                string sign = SingData(param);

                _webSocketPrivate.SendAsync($"{{\"id\": {timeStamp * 1000000},\"time\": {timeStamp},\"channel\": \"spot.orders\",\"event\": \"subscribe\",\"payload\": [\"{security}\"],\"auth\": {{\"method\": \"api_key\",\"KEY\": \"{_publicKey}\",\"SIGN\": \"{sign}\"}}}}");
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void SubscribeUserTrades(string security)
        {
            try
            {
                long timeStamp = TimeManager.GetUnixTimeStampSeconds();
                string param = $"channel=spot.usertrades&event=subscribe&time={timeStamp}";
                string sign = SingData(param);

                _webSocketPrivate.SendAsync($"{{\"id\": {timeStamp * 1000000},\"time\": {timeStamp},\"channel\": \"spot.usertrades\",\"event\": \"subscribe\",\"payload\": [\"{security}\"],\"auth\": {{\"method\": \"api_key\",\"KEY\": \"{_publicKey}\",\"SIGN\": \"{sign}\"}}}}");
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void SubscribePortfolio()
        {
            try
            {
                long timeStamp = TimeManager.GetUnixTimeStampSeconds();
                string param = $"channel=spot.balances&event=subscribe&time={timeStamp}";
                string sign = SingData(param);

                _webSocketPrivate.SendAsync($"{{\"id\": {timeStamp * 1000000},\"time\": {timeStamp},\"channel\": \"spot.balances\",\"event\": \"subscribe\",\"auth\": {{\"method\": \"api_key\",\"KEY\": \"{_publicKey}\",\"SIGN\": \"{sign}\"}}}}");
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            try
            {
                if (_webSocketPublic != null
                    && _webSocketPublic.Count != 0)
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        try
                        {
                            if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open)
                            {
                                foreach (var item in _subscribedSecurities)
                                {
                                    string name = item.Key;
                                    long time = TimeManager.GetUnixTimeStampSeconds();
                                    string level = "5";

                                    if (((ServerParameterBool)ServerParameters[10]).Value == true)
                                    {
                                        level = "20";
                                    }

                                    webSocketPublic?.SendAsync($"{{\"time\":{time},\"channel\":\"spot.order_book\",\"event\":\"unsubscribe\",\"payload\":[\"{name}\",\"{level}\",\"100ms\"]}}");
                                    webSocketPublic?.SendAsync($"{{\"time\":{time},\"channel\":\"spot.trades\",\"event\":\"unsubscribe\",\"payload\":[\"{name}\"]}}");

                                    if (_extendedMarketData)
                                    {
                                        webSocketPublic?.SendAsync($"{{\"time\":{time},\"channel\":\"spot.tickers\",\"event\":\"unsubscribe\",\"payload\":[\"{name}\"]}}");
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

            if (_webSocketPrivate != null)
            {
                try
                {
                    UnsubscribePortfolio();

                    foreach (var item in _subscribedSecurities)
                    {
                        string name = item.Key;

                        UnsubscribeOrders(name);
                        UnsubscribeMyTrades(name);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void UnsubscribeOrders(string security)
        {
            long timeStamp = TimeManager.GetUnixTimeStampSeconds();
            string param = $"channel=spot.orders&event=subscribe&time={timeStamp}";
            string sign = SingData(param);

            _webSocketPrivate.SendAsync($"{{\"id\": {timeStamp * 1000000},\"time\": {timeStamp},\"channel\": \"spot.orders\",\"event\": \"unsubscribe\",\"payload\": [\"{security}\"],\"auth\": {{\"method\": \"api_key\",\"KEY\": \"{_publicKey}\",\"SIGN\": \"{sign}\"}}}}");
        }

        private void UnsubscribeMyTrades(string security)
        {
            long timeStamp = TimeManager.GetUnixTimeStampSeconds();
            string param = $"channel=spot.usertrades&event=subscribe&time={timeStamp}";
            string sign = SingData(param);

            _webSocketPrivate.SendAsync($"{{\"id\": {timeStamp * 1000000},\"time\": {timeStamp},\"channel\": \"spot.usertrades\",\"event\": \"unsubscribe\",\"payload\": [\"{security}\"],\"auth\": {{\"method\": \"api_key\",\"KEY\": \"{_publicKey}\",\"SIGN\": \"{sign}\"}}}}");
        }

        private void UnsubscribePortfolio()
        {
            long timeStamp = TimeManager.GetUnixTimeStampSeconds();
            string param = $"channel=spot.balances&event=subscribe&time={timeStamp}";
            string sign = SingData(param);

            _webSocketPrivate.SendAsync($"{{\"id\": {timeStamp * 1000000},\"time\": {timeStamp},\"channel\": \"spot.balances\",\"event\": \"unsubscribe\",\"auth\": {{\"method\": \"api_key\",\"KEY\": \"{_publicKey}\",\"SIGN\": \"{sign}\"}}}}");
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> _fIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMarketDepths = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueTrades = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _fIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private void MessageReaderPublic()
        {
            while (true)
            {
                try
                {
                    if (_fIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        Thread.Sleep(1);
                    }
                    else
                    {
                        if (_fIFOListWebSocketPublicMessage.TryDequeue(out string message))
                        {
                            if (message.Contains("spot.order_book") && message.Contains("update"))
                            {
                                _queueMarketDepths.Enqueue(message);
                                continue;
                            }
                            else if (message.Contains("spot.trades") && message.Contains("update"))
                            {
                                _queueTrades.Enqueue(message);
                                continue;
                            }
                            else if (message.Contains("spot.tickers") && message.Contains("update"))
                            {
                                UpdateTickers(message);
                                continue;
                            }
                            else
                            {
                                continue;
                            }
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

        private void MessageReaderPrivate()
        {
            while (true)
            {
                try
                {
                    if (_fIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        Thread.Sleep(1);
                    }
                    else
                    {
                        if (_fIFOListWebSocketPrivateMessage.TryDequeue(out string message))
                        {
                            if (message.Contains("spot.usertrades") && message.Contains("update"))
                            {
                                UpdateMyTrade(message);
                                continue;
                            }
                            else if (message.Contains("spot.orders") && message.Contains("update"))
                            {
                                UpdateOrder(message);
                                continue;
                            }
                            else if (message.Contains("spot.balances") && message.Contains("update"))
                            {
                                UpdatePortfolio(message);
                                continue;
                            }
                            else
                            {
                                continue;
                            }
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

        private void ThreadMarketDepthParsing()
        {
            while (true)
            {
                try
                {
                    if (_queueMarketDepths.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        Thread.Sleep(1);
                    }
                    else
                    {
                        string message = null;

                        _queueMarketDepths.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        MarketDepth marketDepth = UpdateDepth(message);

                        if (marketDepth == null) continue;

                        MarketDepthEvent?.Invoke(marketDepth);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void ThreadTradesParsing()
        {
            while (true)
            {
                try
                {
                    if (_queueTrades.IsEmpty)
                    {
                        if (IsCompletelyDeleted == true)
                        {
                            return;
                        }

                        Thread.Sleep(1);
                    }
                    else
                    {
                        string message = null;

                        _queueTrades.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        UpdateTrade(message);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            try
            {
                ResponseWebsocketMessage<MessagePublicTrades> responseTrades = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<MessagePublicTrades>());

                Trade trade = new Trade();
                trade.SecurityNameCode = responseTrades.result.currency_pair;

                trade.Price = responseTrades.result.price.ToDecimal();
                trade.Id = responseTrades.result.id;
                trade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responseTrades.result.create_time));
                trade.Volume = responseTrades.result.amount.ToDecimal();
                trade.Side = responseTrades.result.side.Equals("sell") ? Side.Sell : Side.Buy;

                NewTradesEvent?.Invoke(trade);
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateTickers(string message)
        {
            try
            {
                ResponseWebsocketMessage<TickerItem> responseTicker = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<TickerItem>());

                if (responseTicker == null
                     || responseTicker.result == null)
                {
                    return;
                }

                SecurityVolumes volume = new SecurityVolumes();

                volume.SecurityNameCode = responseTicker.result.currency_pair;
                volume.Volume24h = responseTicker.result.base_volume.ToDecimal();
                volume.Volume24hUSDT = responseTicker.result.quote_volume.ToDecimal();
                volume.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)responseTicker.time_ms.ToDecimal());

                Volume24hUpdateEvent?.Invoke(volume);
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private readonly Dictionary<string, MarketDepth> _allDepths = new Dictionary<string, MarketDepth>();

        private MarketDepth UpdateDepth(string message)
        {
            try
            {
                ResponseWebsocketMessage<MessageDepths> responseDepths = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<MessageDepths>());

                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = responseDepths.result.s;

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                for (int i = 0; i < responseDepths.result.asks.Count; i++)
                {
                    ascs.Add(new MarketDepthLevel()
                    {
                        Ask = responseDepths.result.asks[i][1].ToDouble(),
                        Price = responseDepths.result.asks[i][0].ToDouble()
                    });
                }

                for (int i = 0; i < responseDepths.result.bids.Count; i++)
                {
                    bids.Add(new MarketDepthLevel()
                    {
                        Bid = responseDepths.result.bids[i][1].ToDouble(),
                        Price = responseDepths.result.bids[i][0].ToDouble()
                    });
                }

                depth.Asks = ascs;
                depth.Bids = bids;

                depth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepths.result.t));

                if (depth.Time <= _lastMdTime)
                {
                    depth.Time = _lastMdTime.AddTicks(1);
                }

                _lastMdTime = depth.Time;

                _allDepths[depth.SecurityNameCode] = depth;

                return depth;
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        DateTime _lastMdTime;

        private void UpdateMyTrade(string message)
        {
            try
            {
                ResponseWebsocketMessage<List<MessageUserTrade>> responseMyTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<List<MessageUserTrade>>());

                for (int i = 0; i < responseMyTrade.result.Count; i++)
                {
                    string security = responseMyTrade.result[i].currency_pair;

                    long time = Convert.ToInt64(responseMyTrade.result[i].create_time);

                    MyTrade newTrade = new MyTrade();

                    newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(time);
                    newTrade.SecurityNameCode = security;
                    newTrade.NumberOrderParent = responseMyTrade.result[i].order_id;
                    newTrade.Price = responseMyTrade.result[i].price.ToDecimal();
                    newTrade.NumberTrade = responseMyTrade.result[i].id;
                    newTrade.Side = responseMyTrade.result[i].side.Equals("sell") ? Side.Sell : Side.Buy;

                    string commissionSecName = responseMyTrade.result[i].fee_currency;

                    if (newTrade.SecurityNameCode.StartsWith(commissionSecName))
                    {
                        newTrade.Volume = responseMyTrade.result[i].amount.ToDecimal() - responseMyTrade.result[i].fee.ToDecimal();
                        int decimalVolum = GetDecimalsVolume(security);
                        if (decimalVolum > 0)
                        {
                            newTrade.Volume = Math.Floor(newTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                        }
                    }
                    else
                    {
                        newTrade.Volume = responseMyTrade.result[i].amount.ToDecimal();
                    }

                    MyTradeEvent?.Invoke(newTrade);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private int GetDecimalsVolume(string security)
        {
            if (_securitiesDict.TryGetValue(security, out Security sec))
            {
                return sec.DecimalsVolume;
            }

            return 0;
        }

        private void UpdateOrder(string message)
        {
            try
            {
                ResponseWebsocketMessage<List<MessageUserOrder>> responseOrders = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<List<MessageUserOrder>>());

                for (int i = 0; i < responseOrders.result.Count; i++)
                {
                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = responseOrders.result[i].currency_pair;
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responseOrders.result[i].create_time));

                    OrderStateType orderState = OrderStateType.None;

                    if (responseOrders.result[i].Event.Equals("put"))
                    {
                        orderState = OrderStateType.Active;
                    }
                    else if (responseOrders.result[i].Event.Equals("update"))
                    {
                        orderState = OrderStateType.Partial;
                    }
                    else
                    {
                        if (responseOrders.result[i].finish_as.Equals("cancelled"))
                        {
                            orderState = OrderStateType.Cancel;
                        }
                        else
                        {
                            orderState = OrderStateType.Done;
                        }
                    }

                    try
                    {
                        newOrder.NumberUser = Convert.ToInt32(responseOrders.result[i].text.Replace("t-", ""));
                    }
                    catch
                    {
                        // ignore
                    }

                    newOrder.NumberMarket = responseOrders.result[i].id;
                    newOrder.Side = responseOrders.result[i].side.Equals("buy") ? Side.Buy : Side.Sell;

                    if (responseOrders.result[i].type == "market")
                    {
                        newOrder.TypeOrder = OrderPriceType.Market;
                    }
                    if (responseOrders.result[i].type == "limit")
                    {
                        newOrder.TypeOrder = OrderPriceType.Limit;
                    }

                    newOrder.State = orderState;
                    newOrder.Volume = responseOrders.result[i].amount.Replace('.', ',').ToDecimal();
                    newOrder.Price = responseOrders.result[i].price.Replace('.', ',').ToDecimal();
                    newOrder.ServerType = ServerType.GateIoSpot;
                    newOrder.PortfolioNumber = "GateIO_Spot";

                    MyOrderEvent?.Invoke(newOrder);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(string message)
        {
            try
            {
                ResponseWebsocketMessage<List<CurrencyBalance>> responsePortfolio = JsonConvert.DeserializeAnonymousType(message, new ResponseWebsocketMessage<List<CurrencyBalance>>());

                for (int i = 0; i < responsePortfolio.result.Count; i++)
                {
                    CurrencyBalance current = responsePortfolio.result[i];

                    PositionOnBoard positionOnBoard = new PositionOnBoard();

                    positionOnBoard.SecurityNameCode = current.currency;
                    positionOnBoard.ValueBegin = Math.Round(current.total.ToDecimal(), 5);
                    positionOnBoard.ValueCurrent = Math.Round(current.available.ToDecimal(), 5);
                    positionOnBoard.ValueBlocked = Math.Round(current.freeze.ToDecimal(), 5);

                    _myPortfolio.SetNewPosition(positionOnBoard);
                }

                PortfolioEvent?.Invoke(new List<Portfolio> { _myPortfolio });
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 11 Trade

        private readonly RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private readonly RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(20));

        private readonly RateGate _rateGateGetOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string side = order.Side == Side.Buy ? "buy" : "sell";
                string secName = order.SecurityNameCode;
                string price = order.Price.ToString().Replace(",", ".");
                string volume = order.Volume.ToString().Replace(",", ".");

                string method = "POST";
                string url = "/spot/orders";
                string query_param = "";

                string bodyParam = $"{{\"text\":\"t-{order.NumberUser}\",\"currency_pair\":" +
                                    $"\"{secName}\",\"type\":\"limit\",\"account\":\"spot\",\"side\":\"{side}\",\"iceberg\":\"0\",\"amount\":\"{volume}\",\"price\":\"{price}\",\"time_in_force\":\"gtc\"}}";

                string bodyHash = GetHash(bodyParam);

                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();

                string signString = $"{method}\n{_prefix}{url}\n{query_param}\n{bodyHash}\n{timeStamp}";
                string sign = GetHashHMAC(signString, _secretKey);

                string fullUrl = $"{_host}{_prefix}{url}";

                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(Method.POST);
                request.AddHeader("KEY", _publicKey);
                request.AddHeader("SIGN", sign);
                request.AddHeader("Timestamp", timeStamp);
                request.AddHeader("X-Gate-Channel-Id", "osa");
                request.AddParameter("application/json", bodyParam, ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode != HttpStatusCode.Created)
                {
                    SendLogMessage($"SendOrder. Error: {responseMessage.Content}", LogMessageType.Error);
                    order.State = OrderStateType.Fail;
                    MyOrderEvent.Invoke(order);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {

        }

        public void CancelAllOrders()
        {
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
        }

        public bool CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            try
            {
                string method = "DELETE";
                string url = $"/spot/orders/{order.NumberMarket}";
                string queryParam = $"currency_pair={order.SecurityNameCode}";
                string bodyParam = "";
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                string bodyHash = GetHash(bodyParam);
                string signString = $"{method}\n{_prefix}{url}\n{queryParam}\n{bodyHash}\n{timeStamp}";
                string sign = GetHashHMAC(signString, _secretKey);
                string fullUrl = $"{_host}{_prefix}{url}?{queryParam}";

                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(Method.DELETE);
                request.AddHeader("KEY", _publicKey);
                request.AddHeader("SIGN", sign);
                request.AddHeader("Timestamp", timeStamp);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode != HttpStatusCode.OK)
                {
                    OrderStateType state = GetOrderStatus(order);

                    if (state == OrderStateType.None)
                    {
                        SendLogMessage($"CancelOrder. Error: {responseMessage.Content}", LogMessageType.Error);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
            return false;
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllActivOrdersFromExchange();

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

        private List<Order> GetAllActivOrdersFromExchange()
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string method = "GET";
                string url = $"/spot/open_orders";
                string queryParam = "";
                string bodyParam = "";
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                string bodyHash = GetHash(bodyParam);
                string signString = $"{method}\n{_prefix}{url}\n{queryParam}\n{bodyHash}\n{timeStamp}";
                string sign = GetHashHMAC(signString, _secretKey);
                string fullUrl = $"{_host}{_prefix}{url}?{queryParam}";

                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(Method.GET);
                request.AddHeader("KEY", _publicKey);
                request.AddHeader("SIGN", sign);
                request.AddHeader("Timestamp", timeStamp);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    List<OrderResponse> responseOrders = JsonConvert.DeserializeObject<List<OrderResponse>>(responseMessage.Content);

                    List<Order> orders = new List<Order>();

                    for (int i = 0; i < responseOrders.Count; i++)
                    {
                        List<MessageUserOrder> itemOrders = responseOrders[i].orders;

                        for (int j = 0; j < itemOrders.Count; j++)
                        {
                            if (itemOrders[j].status != "open")
                            {
                                continue;
                            }

                            Order newOrder = new Order();
                            newOrder.SecurityNameCode = itemOrders[j].currency_pair;
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(itemOrders[j].create_time));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(itemOrders[j].create_time));

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(itemOrders[j].text.Replace("t-", ""));
                            }
                            catch
                            {
                                // ignore
                            }

                            newOrder.NumberMarket = itemOrders[j].id;
                            newOrder.Side = itemOrders[j].side.Equals("buy") ? Side.Buy : Side.Sell;

                            if (itemOrders[j].type == "market")
                            {
                                newOrder.TypeOrder = OrderPriceType.Market;
                            }
                            if (itemOrders[j].type == "limit")
                            {
                                newOrder.TypeOrder = OrderPriceType.Limit;
                            }

                            newOrder.State = OrderStateType.Active;
                            newOrder.Volume = itemOrders[j].amount.Replace('.', ',').ToDecimal();
                            newOrder.Price = itemOrders[j].price.Replace('.', ',').ToDecimal();
                            newOrder.ServerType = ServerType.GateIoSpot;
                            newOrder.PortfolioNumber = "GateIO_Spot";

                            orders.Add(newOrder);
                        }
                    }

                    return orders;
                }
                else
                {
                    SendLogMessage($"Get Activ Order. Error: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
            return null;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            List<Order> orderFromExchange = GetAllActivOrdersFromExchange();  //GetOrderFromExchange(order.SecurityNameCode, order.NumberMarket, order.NumberUser, order.State);

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
                FindMyTradesToOrder(order.SecurityNameCode, order.NumberUser);
            }

            return orderOnMarket.State;
        }

        private List<Order> GetOrderFromExchange(string securityNameCode)
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string state = "finished";

                string method = "GET";

                string queryParam = $"currency_pair={securityNameCode}&";
                queryParam += $"status={state}";

                string url = $"/spot/orders";

                string bodyParam = "";
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                string bodyHash = GetHash(bodyParam);
                string signString = $"{method}\n{_prefix}{url}\n{queryParam}\n{bodyHash}\n{timeStamp}";
                string sign = GetHashHMAC(signString, _secretKey);
                string fullUrl = $"{_host}{_prefix}{url}?{queryParam}";

                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(Method.GET);
                request.AddHeader("KEY", _publicKey);
                request.AddHeader("SIGN", sign);
                request.AddHeader("Timestamp", timeStamp);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    List<MessageUserOrder> responseOrders = JsonConvert.DeserializeObject<List<MessageUserOrder>>(responseMessage.Content);

                    List<Order> orders = new List<Order>();

                    for (int i = 0; i < responseOrders.Count; i++)
                    {
                        Order newOrder = new Order();
                        newOrder.SecurityNameCode = responseOrders[i].currency_pair;
                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responseOrders[i].create_time));
                        newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStampSeconds(Convert.ToInt64(responseOrders[i].create_time));

                        try
                        {
                            newOrder.NumberUser = Convert.ToInt32(responseOrders[i].text.Replace("t-", ""));
                        }
                        catch
                        {
                            // ignore
                        }

                        newOrder.NumberMarket = responseOrders[i].id;
                        newOrder.Side = responseOrders[i].side.Equals("buy") ? Side.Buy : Side.Sell;

                        if (responseOrders[i].type == "market")
                        {
                            newOrder.TypeOrder = OrderPriceType.Market;
                        }
                        if (responseOrders[i].type == "limit")
                        {
                            newOrder.TypeOrder = OrderPriceType.Limit;
                        }

                        OrderStateType orderState = OrderStateType.None;

                        if (responseOrders[i].status.Equals("open"))
                        {
                            orderState = OrderStateType.Active;
                        }
                        else if (responseOrders[i].status.Equals("cancelled"))
                        {
                            orderState = OrderStateType.Cancel;
                        }
                        else if (responseOrders[i].status.Equals("closed"))
                        {
                            orderState = OrderStateType.Done;
                        }
                        else
                        {
                            orderState = OrderStateType.None;
                        }

                        newOrder.State = orderState;
                        newOrder.Volume = responseOrders[i].amount.Replace('.', ',').ToDecimal();
                        newOrder.Price = responseOrders[i].price.Replace('.', ',').ToDecimal();
                        newOrder.ServerType = ServerType.GateIoSpot;
                        newOrder.PortfolioNumber = "GateIO_Spot";

                        orders.Add(newOrder);
                    }
                    return orders;
                }
                else
                {
                    SendLogMessage($"Get status Order. Error: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }

            return null;
        }

        private void FindMyTradesToOrder(string nameSecurity, int numberUser)
        {
            _rateGateGetOrder.WaitToProceed();

            try
            {
                string method = "GET";
                string queryParam = $"currency_pair={nameSecurity}";
                string url = $"/spot/my_trades";

                string bodyParam = "";
                string timeStamp = TimeManager.GetUnixTimeStampSeconds().ToString();
                string bodyHash = GetHash(bodyParam);
                string signString = $"{method}\n{_prefix}{url}\n{queryParam}\n{bodyHash}\n{timeStamp}";
                string sign = GetHashHMAC(signString, _secretKey);
                string fullUrl = $"{_host}{_prefix}{url}?{queryParam}";

                RestClient client = new RestClient(fullUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                RestRequest request = new RestRequest(Method.GET);
                request.AddHeader("KEY", _publicKey);
                request.AddHeader("SIGN", sign);
                request.AddHeader("Timestamp", timeStamp);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    List<MessageUserTrade> responseMyTrade = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new List<MessageUserTrade>());

                    for (int i = 0; i < responseMyTrade.Count; i++)
                    {
                        int userNumber = 0;

                        if (responseMyTrade[i].text.Contains("t"))
                        {
                            userNumber = Convert.ToInt32(responseMyTrade[i].text.Replace("t-", ""));
                        }
                        else
                        {
                            continue;
                        }

                        if (userNumber == numberUser)
                        {
                            string security = responseMyTrade[i].currency_pair;

                            long time = Convert.ToInt64(responseMyTrade[i].create_time);

                            MyTrade newTrade = new MyTrade();

                            newTrade.Time = TimeManager.GetDateTimeFromTimeStampSeconds(time);
                            newTrade.SecurityNameCode = security;
                            newTrade.NumberOrderParent = responseMyTrade[i].order_id;
                            newTrade.Price = responseMyTrade[i].price.ToDecimal();
                            newTrade.NumberTrade = responseMyTrade[i].id;
                            newTrade.Side = responseMyTrade[i].side.Equals("sell") ? Side.Sell : Side.Buy;

                            string commissionSecName = responseMyTrade[i].fee_currency;

                            if (newTrade.SecurityNameCode.StartsWith(commissionSecName))
                            {
                                newTrade.Volume = responseMyTrade[i].amount.ToDecimal() - responseMyTrade[i].fee.ToDecimal();
                                int decimalVolum = GetDecimalsVolume(security);
                                if (decimalVolum > 0)
                                {
                                    newTrade.Volume = Math.Floor(newTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                                }
                            }
                            else
                            {
                                newTrade.Volume = responseMyTrade[i].amount.ToDecimal();
                            }

                            MyTradeEvent?.Invoke(newTrade);
                        }
                    }
                }
                else
                {
                    SendLogMessage($"MyTrades to order error. Code:{responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
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

        public string SingData(string message)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(_secretKey);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            using (HMACSHA512 hash = new HMACSHA512(keyBytes))
            {
                byte[] hashBytes = hash.ComputeHash(messageBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private string GetHash(string input)
        {
            using (SHA512 sha512 = SHA512.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = sha512.ComputeHash(inputBytes);
                string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                return hash;
            }
        }

        private string GetHashHMAC(string input, string key)
        {
            using (HMACSHA512 hmacsha512 = new HMACSHA512(Encoding.ASCII.GetBytes(key)))
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = hmacsha512.ComputeHash(inputBytes);
                string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                return hash;
            }
        }

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, messageType);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}