﻿using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using WebSocketSharp;
using System.Threading;
using System.Security.Cryptography;
using OsEngine.Market.Servers.BingX.BingXFutures.Entity;
using System.Globalization;


namespace OsEngine.Market.Servers.BingX.BingXFutures
{
    public class BingXServerFutures : AServer
    {
        public BingXServerFutures(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            BingXServerFuturesRealization realization = new BingXServerFuturesRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterBoolean("HedgeMode", false);
        }
    }

    public class BingXServerFuturesRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BingXServerFuturesRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread keepalive = new Thread(RequestListenKey);
            keepalive.CurrentCulture = new CultureInfo("ru-RU");
            keepalive.IsBackground = true;
            keepalive.Start();

            Thread messageReaderPrivate = new Thread(MessageReaderPrivate);
            messageReaderPrivate.IsBackground = true;
            messageReaderPrivate.Name = "MessageReaderPrivateBingXFutures";
            messageReaderPrivate.Start();

            Thread messageReaderPublic = new Thread(MessageReaderPublic);
            messageReaderPublic.IsBackground = true;
            messageReaderPublic.Name = "MessageReaderBingXFutures";
            messageReaderPublic.Start();

            Thread threadGetPortfolios = new Thread(ThreadGetPortfolios);
            threadGetPortfolios.IsBackground = true;
            threadGetPortfolios.Name = "ThreadBingXFuturesPortfolios";
            threadGetPortfolios.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
                _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
                _hedgeMode = ((ServerParameterBool)ServerParameters[2]).Value;

                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(_baseUrl + "/openApi/swap/v2/server/time").Result;
                string json = responseMessage.Content.ReadAsStringAsync().Result;

                if (responseMessage.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"The server is not available. No internet", LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
                else
                {
                    try
                    {
                        FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                        FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
                        CreatePublicWebSocketConnect();
                        CreatePrivateWebSocketConnect();
                        CheckSocketsActivate();
                        SetPositionMode();
                    }
                    catch (Exception exception)
                    {
                        SendLogMessage(exception.ToString(), LogMessageType.Error);
                        SendLogMessage("The connection cannot be opened. BingXFutures. Error Request", LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage("The connection cannot be opened. BingXFutures. Error Request", LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
                _subscribledSecutiries.Clear();
                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            FIFOListWebSocketPublicMessage = null;
            FIFOListWebSocketPrivateMessage = null;

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

        private RateGate _positionModeRateGate = new RateGate(1, TimeSpan.FromMilliseconds(510)); // individual IP speed limit is 2 requests per 1 second 

        private void SetPositionMode()
        {
            _generalRateGate2.WaitToProceed();
            _positionModeRateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/swap/v1/positionSide/dual", Method.POST);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"dualSidePosition={_hedgeMode}&timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("dualSidePosition", _hedgeMode);
                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<PositionMode> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<PositionMode>>(json.Content);

                    if (response.code == "0")
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage($"SetPositionMode> Http State Code: {response.code} - message: {response.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"SetPositionMode> Http State Code: {json.StatusCode} | msg: {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"SetPositionMode: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public ServerType ServerType
        {
            get { return ServerType.BingXFutures; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private RateGate _generalRateGate1 = new RateGate(1, TimeSpan.FromMilliseconds(130));

        private RateGate _generalRateGate2 = new RateGate(1, TimeSpan.FromMilliseconds(110));

        private RateGate _generalRateGate3 = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public string _publicKey;

        public string _secretKey;

        private bool _hedgeMode;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            _generalRateGate1.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/swap/v2/quote/contracts", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingX<BingXFuturesSymbols> response = JsonConvert.DeserializeAnonymousType(json.Content, new ResponseFuturesBingX<BingXFuturesSymbols>());
                    List<BingXFuturesSymbols> currencyPairs = new List<BingXFuturesSymbols>();

                    if (response.code == "0")
                    {
                        for (int i = 0; i < response.data.Count; i++)
                        {
                            currencyPairs.Add(response.data[i]);
                        }
                        UpdateSecurity(currencyPairs);
                    }
                    else
                    {
                        SendLogMessage($"GetSecurities> Error Code: {response.code} | msg: {response.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"GetSecurities> Http State Code: {json.StatusCode} | msg: {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecurity(List<BingXFuturesSymbols> currencyPairs)
        {
            List<Security> securities = new List<Security>();

            for (int i = 0; i < currencyPairs.Count; i++)
            {
                BingXFuturesSymbols current = currencyPairs[i];

                if (current.status == "1")
                {
                    Security security = new Security();

                    if (current.symbol.EndsWith("USDC"))
                    {
                        continue;
                    }

                    security.Lot = 1;
                    security.MinTradeAmount = current.size.ToDecimal();
                    security.Name = current.symbol;
                    security.NameFull = current.symbol;
                    security.NameClass = current.currency;
                    security.NameId = current.contractId;
                    security.Exchange = nameof(ServerType.BingXFutures);
                    security.State = SecurityStateType.Activ;
                    security.Decimals = Convert.ToInt32(current.pricePrecision);
                    security.PriceStep = security.Decimals.GetValueByDecimals();
                    security.PriceStepCost = security.PriceStep;
                    security.SecurityType = SecurityType.CurrencyPair;
                    security.DecimalsVolume = Convert.ToInt32(current.quantityPrecision);
                    security.MinTradeAmount = current.tradeMinUSDT.ToDecimal();
                    security.MinTradeAmountType = MinTradeAmountType.C_Currency;
                    security.VolumeStep = current.size.ToDecimal();

                    securities.Add(security);
                }
            }

            SecurityEvent(securities);
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        public List<Portfolio> Portfolios;

        public void GetPortfolios()
        {
            if (Portfolios == null)
            {
                GetNewPortfolio();
            }

            CreateQueryPortfolio(true);
            CreateQueryPositions();
        }

        private void ThreadGetPortfolios()
        {
            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                    continue;
                }

                try
                {
                    Thread.Sleep(20000);

                    if (Portfolios == null)
                    {
                        GetNewPortfolio();
                    }

                    CreateQueryPortfolio(false);
                    CreateQueryPositions();
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }
        }

        private void GetNewPortfolio()
        {
            Portfolios = new List<Portfolio>();

            Portfolio portfolioInitial = new Portfolio();
            portfolioInitial.Number = "BingXFutures";
            portfolioInitial.ValueBegin = 1;
            portfolioInitial.ValueCurrent = 1;
            portfolioInitial.ValueBlocked = 0;

            Portfolios.Add(portfolioInitial);

            PortfolioEvent(Portfolios);
        }

        private RateGate _positionsRateGate = new RateGate(1, TimeSpan.FromMilliseconds(250));

        private void CreateQueryPositions()
        {
            _positionsRateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/swap/v2/user/positions", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingX<PositionData> response = JsonConvert.DeserializeObject<ResponseFuturesBingX<PositionData>>(json.Content);

                    if (response.code == "0")
                    {
                        Portfolio portfolio = Portfolios[0];

                        List<PositionData> positionData = response.data;

                        for (int i = 0; i < positionData.Count; i++)
                        {
                            PositionOnBoard position = new PositionOnBoard();

                            position.PortfolioName = "BingXFutures";

                            if (positionData[i].OnlyOnePosition == "true")
                            {
                                position.SecurityNameCode = positionData[i].Symbol + "_BOTH";

                                if (positionData[i].PositionSide == "LONG")
                                {
                                    position.ValueCurrent = positionData[i].PositionAmt.ToDecimal();
                                    position.ValueBegin = positionData[i].PositionAmt.ToDecimal();
                                }
                                else if (positionData[i].PositionSide == "SHORT")
                                {
                                    position.ValueCurrent = -(positionData[i].PositionAmt.ToDecimal());
                                    position.ValueBegin = -(positionData[i].PositionAmt.ToDecimal());
                                }

                                position.UnrealizedPnl = positionData[i].UnrealizedProfit.ToDecimal();
                                portfolio.SetNewPosition(position);
                                continue;
                            }
                            else
                            {
                                if (positionData[i].PositionSide == "LONG")
                                {
                                    position.SecurityNameCode = positionData[i].Symbol + "_LONG";
                                    position.ValueCurrent = positionData[i].PositionAmt.ToDecimal();
                                    position.ValueBegin = positionData[i].PositionAmt.ToDecimal();
                                    position.UnrealizedPnl = positionData[i].UnrealizedProfit.ToDecimal();
                                    portfolio.SetNewPosition(position);
                                    continue;
                                }
                                else if (positionData[i].PositionSide == "SHORT")
                                {
                                    position.SecurityNameCode = positionData[i].Symbol + "_SHORT";
                                    position.ValueCurrent = -(positionData[i].PositionAmt.ToDecimal());
                                    position.ValueBegin = -(positionData[i].PositionAmt.ToDecimal());
                                    position.UnrealizedPnl = positionData[i].UnrealizedProfit.ToDecimal();
                                    portfolio.SetNewPosition(position);
                                    continue;
                                }
                            }
                        }

                        PortfolioEvent(Portfolios);
                    }
                    else
                    {
                        SendLogMessage($"CreateQueryPositions> Http State Code: {response.code} - message: {response.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    if (json.Content.StartsWith("<!DOCTYPE"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage($"CreateQueryPositions> Http State Code: {json.StatusCode} | msg: {json.Content}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private RateGate _portfolioRateGate = new RateGate(1, TimeSpan.FromMilliseconds(250));

        private void CreateQueryPortfolio(bool IsUpdateValueBegin)
        {
            _portfolioRateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/swap/v2/user/balance", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}&recvWindow=20000";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("recvWindow", 20000);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<Balance> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<Balance>>(json.Content);

                    if (response.code == "0")
                    {
                        Portfolio portfolio = Portfolios[0];

                        BalanceInfoBingXFutures asset = response.data.balance;

                        PositionOnBoard newPortf = new PositionOnBoard();
                        newPortf.SecurityNameCode = asset.asset;

                        if (IsUpdateValueBegin)
                        {
                            newPortf.ValueBegin = asset.balance.ToDecimal();
                        }

                        newPortf.ValueCurrent = asset.equity.ToDecimal();
                        newPortf.ValueBlocked = asset.freezedMargin.ToDecimal() + asset.usedMargin.ToDecimal();
                        newPortf.UnrealizedPnl = asset.unrealizedProfit.ToDecimal();
                        newPortf.PortfolioName = "BingXFutures";
                        portfolio.SetNewPosition(newPortf);


                        if (IsUpdateValueBegin)
                        {
                            portfolio.ValueBegin = newPortf.ValueBegin;
                        }

                        portfolio.ValueCurrent = newPortf.ValueCurrent;
                        portfolio.ValueBlocked = newPortf.ValueBlocked;
                        portfolio.UnrealizedPnl = newPortf.UnrealizedPnl;

                        if (newPortf.ValueCurrent == 0)
                        {
                            portfolio.ValueBegin = 1;
                            portfolio.ValueCurrent = 1;
                        }

                        PortfolioEvent(Portfolios);
                    }
                    else
                    {
                        SendLogMessage($"CreateQueryPortfolio> Http State Code: {response.code} - message: {response.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    if (json.Content.StartsWith("<!DOCTYPE"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage($"CreateQueryPortfolio> Http State Code: {json.StatusCode} | msg: {json.Content}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            string tf = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);
            return RequestCandleHistory(security.Name, tf);
        }

        private List<Candle> RequestCandleHistory(string nameSec, string tameFrame, long limit = 500, long fromTimeStamp = 0, long toTimeStamp = 0)
        {
            _generalRateGate1.WaitToProceed();

            try
            {
                string endPoint = "/openApi/swap/v3/quote/klines";
                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                string parameters = "";
                if (fromTimeStamp != 0 && toTimeStamp != 0)
                {
                    parameters = $"symbol={nameSec}&interval={tameFrame}&startTime={fromTimeStamp}&endTime={toTimeStamp}&limit={limit}&timestamp={timeStamp}";
                }
                else
                {
                    parameters = $"symbol={nameSec}&interval={tameFrame}&limit={limit}&timestamp={timeStamp}";
                }

                string sign = CalculateHmacSha256(parameters);
                string requestUri = $"{_baseUrl}{endPoint}?{parameters}&signature{sign}";

                HttpResponseMessage responseMessage = _httpPublicClient.GetAsync(requestUri).Result;

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;

                    try
                    {
                        ResponseFuturesBingX<CandlestickChartDataFutures> response =
                            JsonConvert.DeserializeAnonymousType(json, new ResponseFuturesBingX<CandlestickChartDataFutures>());

                        // if the start and end date of the candles is incorrect, the exchange sends one last candle instead of an error
                        if (response.code == "0" && response.data.Count != 1)
                        {
                            return ConvertCandles(response.data);
                        }
                        else if (response.data.Count == 1)
                        {
                            return null;
                        }
                        else
                        {
                            SendLogMessage($"RequestCandleHistory> Error: code {response.code}", LogMessageType.Error);
                        }
                    }
                    catch
                    {
                        JsonErrorResponse responseError = JsonConvert.DeserializeAnonymousType(json, new JsonErrorResponse());
                        SendLogMessage($"RequestCandleHistory> Http State Code: {responseError.code} - message: {responseError.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    string json = responseMessage.Content.ReadAsStringAsync().Result;
                    SendLogMessage($"RequestCandleHistory> Http State Code: {responseMessage.StatusCode} - {json}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Candle> ConvertCandles(List<CandlestickChartDataFutures> rawList)
        {
            try
            {
                List<Candle> candles = new List<Candle>();

                for (int i = 0; i < rawList.Count; i++)
                {
                    CandlestickChartDataFutures current = rawList[i];

                    Candle candle = new Candle();

                    candle.State = CandleState.Finished;
                    candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(current.time));
                    candle.Volume = current.volume.ToDecimal();
                    candle.Close = current.close.ToDecimal();
                    candle.High = current.high.ToDecimal();
                    candle.Low = current.low.ToDecimal();
                    candle.Open = current.open.ToDecimal();

                    // We check that the list is not empty and the current candle does not duplicate the last one
                    if (candles.Count > 0 && candle.TimeStart == candles[candles.Count - 1].TimeStart)
                    {
                        continue;
                    }

                    candles.Add(candle);
                }

                candles.Reverse();
                return candles;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
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
            string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

            DateTime startTimeData = startTime;
            DateTime partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 1200);

            do
            {
                List<Candle> candles = new List<Candle>();

                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTimeData);
                long to = TimeManager.GetTimeStampMilliSecondsToDateTime(partEndTime);

                candles = RequestCandleHistory(security.Name, interval, 1200, from, to); // maximum 1440 candles

                if (candles == null || candles.Count == 0)
                {
                    break;
                }

                Candle last = candles[candles.Count - 1];
                Candle first = candles[0];

                // We check that the list is not empty and the current candle does not duplicate the last one
                if (allCandles.Count > 0 && first.TimeStart == allCandles[allCandles.Count - 1].TimeStart)
                {
                    candles.RemoveAt(0);
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

                startTimeData = partEndTime;
                partEndTime = startTimeData.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 1200);

                if (startTimeData >= DateTime.UtcNow)
                {
                    break;
                }

                if (partEndTime > DateTime.UtcNow)
                {
                    partEndTime = DateTime.UtcNow;
                }
            }
            while (true);

            return allCandles;
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

        private bool CheckTf(int timeFrameMinutes)
        {
            if (timeFrameMinutes == 1 ||
                timeFrameMinutes == 5 ||
                timeFrameMinutes == 15 ||
                timeFrameMinutes == 30 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 120 ||
                timeFrameMinutes == 240)
            {
                return true;
            }

            return false;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

        private const string _webSocketUrl = "wss://open-api-swap.bingx.com/swap-market";

        private string _listenKey = "";

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
                _listenKey = CreateListenKey();

                if (_listenKey == null)
                {
                    SendLogMessage("Authorization error. Listen key is note created", LogMessageType.Error);
                    return null;
                }

                string urlStr = $"{_webSocketUrl}?listenKey={_listenKey}";

                WebSocket webSocketPublicNew = new WebSocket(urlStr);
                webSocketPublicNew.SslConfiguration.EnabledSslProtocols
                    = System.Security.Authentication.SslProtocols.Ssl3
                    | System.Security.Authentication.SslProtocols.Tls11
                    | System.Security.Authentication.SslProtocols.None
                    | System.Security.Authentication.SslProtocols.Tls12
                    | System.Security.Authentication.SslProtocols.Tls13
                    | System.Security.Authentication.SslProtocols.Tls;
                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += WebSocketPublicNew_OnOpen;
                webSocketPublicNew.OnClose += WebSocketPublicNew_OnClose;
                webSocketPublicNew.OnMessage += WebSocketPublicNew_OnMessage;
                webSocketPublicNew.OnError += WebSocketPublicNew_OnError;
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
            if (_webSocketPrivate != null)
            {
                return;
            }

            _listenKey = CreateListenKey();

            if (_listenKey == null)
            {
                SendLogMessage("Authorization error. Listen key is note created", LogMessageType.Error);
                return;
            }

            string urlStr = $"{_webSocketUrl}?listenKey={_listenKey}";

            _webSocketPrivate = new WebSocket(urlStr);
            _webSocketPrivate.SslConfiguration.EnabledSslProtocols
                = System.Security.Authentication.SslProtocols.Ssl3
                | System.Security.Authentication.SslProtocols.Tls11
                | System.Security.Authentication.SslProtocols.None
                | System.Security.Authentication.SslProtocols.Tls12
                | System.Security.Authentication.SslProtocols.Tls13
                | System.Security.Authentication.SslProtocols.Tls;
            _webSocketPrivate.OnOpen += _webSocketPrivate_OnOpen;
            _webSocketPrivate.OnClose += _webSocketPrivate_OnClose;
            _webSocketPrivate.OnMessage += _webSocketPrivate_OnMessage;
            _webSocketPrivate.OnError += _webSocketPrivate_OnError;

            _webSocketPrivate.Connect();
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

        private string _socketActivateLocker = "socketActivateLocker";

        private void CheckSocketsActivate()
        {
            try
            {
                lock (_socketActivateLocker)
                {
                    if (_webSocketPrivate == null
                       || _webSocketPrivate?.ReadyState != WebSocketState.Open)
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

                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        ServerStatus = ServerConnectStatus.Connect;
                        ConnectEvent();
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublicNew_OnError(object sender, ErrorEventArgs e)
        {
            if (e.Exception != null)
            {
                SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
            }
            else
            {
                SendLogMessage("WebSocket Public error" + e.ToString(), LogMessageType.Error);
                CheckSocketsActivate();
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

                if (e.RawData == null
                    || e.RawData.Length == 0)
                {
                    return;
                }

                if (FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                string item = Decompress(e.RawData);

                if (item.Contains("Ping")) // send immediately upon receipt.
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        _webSocketPublic[i].Send("Pong");
                    }

                    return;
                }

                FIFOListWebSocketPublicMessage.Enqueue(item);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnClose(object sender, CloseEventArgs e)
        {
            try
            {
                SendLogMessage($"Connection Closed by BingXFutures. {e.Code} {e.Reason}. WebSocket Public Closed Event", LogMessageType.Error);
                CheckSocketsActivate();
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void WebSocketPublicNew_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("BingXFutures WebSocket Public connection open", LogMessageType.System);
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
            if (e.Exception != null)
            {
                SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
            }
            else
            {
                SendLogMessage("WebSocket Private error" + e.ToString(), LogMessageType.Error);
                CheckSocketsActivate();
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

                if (e.RawData == null
                    || e.RawData.Length == 0)
                {
                    return;
                }

                if (FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                string item = Decompress(e.RawData);

                if (item.Contains("Ping")) // send immediately upon receipt. 
                {
                    _webSocketPrivate.Send("Pong");
                    return;
                }

                FIFOListWebSocketPrivateMessage.Enqueue(item);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage($"Error message read. Error: {exception}", LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnClose(object sender, CloseEventArgs e)
        {
            try
            {
                SendLogMessage($"Connection Closed by BingXFutures. {e.Code} {e.Reason}. WWebSocket Private Closed Event", LogMessageType.Error);
                CheckSocketsActivate();
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void _webSocketPrivate_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("BingXFutures WebSocket Private connection open", LogMessageType.System);
                    CheckSocketsActivate();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 Security subscrible

        private List<string> _subscribledSecutiries = new List<string>();

        public void Subscrible(Security security)
        {
            try
            {
                CreateSubscribleSecurityMessageWebSocket(security);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSubscribleSecurityMessageWebSocket(Security security)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                for (int i = 0; i < _subscribledSecutiries.Count; i++)
                {
                    if (_subscribledSecutiries[i].Equals(security.Name))
                    {
                        return;
                    }
                }

                _subscribledSecutiries.Add(security.Name);

                if (_webSocketPublic.Count == 0)
                {
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribledSecutiries.Count != 0
                    && _subscribledSecutiries.Count % 40 == 0)
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
                    webSocketPublic.Send($"{{\"id\": \"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"{security.Name}@trade\"}}");
                    webSocketPublic.Send($"{{ \"id\":\"{GenerateNewId()}\", \"reqType\": \"sub\", \"dataType\": \"{security.Name}@depth20@500ms\"}}");
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UnsubscribeFromAllWebSockets()
        {
            try
            {
                for (int i = 0; i < _webSocketPublic.Count; i++)
                {
                    WebSocket webSocketPublic = _webSocketPublic[i];

                    try
                    {
                        if (webSocketPublic != null && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            if (_subscribledSecutiries != null)
                            {
                                for (int i2 = 0; i2 < _subscribledSecutiries.Count; i2++)
                                {
                                    string name = _subscribledSecutiries[i];

                                    webSocketPublic.Send($"{{\"id\": \"{GenerateNewId()}\", \"reqType\": \"unsub\", \"dataType\": \"{name}@trade\"}}");
                                    webSocketPublic.Send($"{{ \"id\":\"{GenerateNewId()}\", \"reqType\": \"unsub\", \"dataType\": \"{name}@depth20@500ms\"}}");
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
            catch
            {
                // ignore
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent;

        #endregion

        #region 9 WebSocket parsing the messages

        private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private void MessageReaderPublic()
        {
            Thread.Sleep(5000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (FIFOListWebSocketPublicMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (FIFOListWebSocketPublicMessage.TryDequeue(out string message))
                    {
                        if (message.Contains("@trade"))
                        {
                            UpdateTrade(message);
                            continue;
                        }
                        else if (message.Contains("@depth20"))
                        {
                            UpdateDepth(message);
                            continue;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(2000);
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
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (FIFOListWebSocketPrivateMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (FIFOListWebSocketPrivateMessage.TryDequeue(out string message))
                    {
                        if (message.Contains("ORDER_TRADE_UPDATE"))
                        {
                            UpdateOrder(message);
                            continue;
                        }
                        else if (message.Contains("ACCOUNT_UPDATE"))
                        {
                            UpdatePortfolio(message);
                            UpdatePosition(message);
                            continue;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Thread.Sleep(2000);
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            try
            {
                SubscribeLatestTradeDetail<TradeDetails> response = JsonConvert.DeserializeObject<SubscribeLatestTradeDetail<TradeDetails>>(message);

                Trade trade = new Trade();

                for (int i = 0; i < response.data.Count; i++)
                {
                    trade.SecurityNameCode = response.data[i].s;

                    trade.Price = response.data[i].p.Replace('.', ',').ToDecimal();
                    // trade.Id = // the exchange does not send trade id
                    trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(response.data[i].T));
                    trade.Volume = response.data[i].q.Replace('.', ',').ToDecimal();
                    if (response.data[i].m == "true")
                        trade.Side = Side.Sell;
                    else trade.Side = Side.Buy;

                    NewTradesEvent(trade);
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
                AccountUpdateEvent accountUpdate = JsonConvert.DeserializeObject<AccountUpdateEvent>(message);

                Portfolio portfolio = Portfolios[0];

                for (int i = 0; i < accountUpdate.a.B.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "BingXFutures";
                    pos.SecurityNameCode = accountUpdate.a.B[i].a;
                    pos.ValueCurrent = accountUpdate.a.B[i].wb.ToDecimal();

                    portfolio.SetNewPosition(pos);

                    PortfolioEvent(Portfolios);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePosition(string message)
        {
            try
            {
                AccountUpdateEvent accountUpdate = JsonConvert.DeserializeObject<AccountUpdateEvent>(message);

                Portfolio portfolio = Portfolios[0];

                PositionOnBoard position = new PositionOnBoard();

                for (int i = 0; i < accountUpdate.a.P.Count; i++)
                {
                    position.PortfolioName = "BingXFutures";

                    if (!_hedgeMode)
                    {
                        position.SecurityNameCode = accountUpdate.a.P[i].s + "_BOTH";

                        if (accountUpdate.a.P[i].ps.Equals("LONG"))
                        {
                            position.ValueCurrent = accountUpdate.a.P[i].pa.ToDecimal();
                        }
                        else if (accountUpdate.a.P[i].ps.Equals("SHORT"))
                        {
                            position.ValueCurrent = -(accountUpdate.a.P[i].pa.ToDecimal());
                        }

                        portfolio.SetNewPosition(position);

                        PortfolioEvent(new List<Portfolio> { portfolio });

                        continue;
                    }

                    if (accountUpdate.a.P[i].ps.Equals("LONG"))
                    {
                        position.ValueCurrent = accountUpdate.a.P[i].pa.ToDecimal();
                        position.SecurityNameCode = accountUpdate.a.P[i].s + "_LONG";

                        portfolio.SetNewPosition(position);
                    }
                    else if (accountUpdate.a.P[i].ps.Equals("SHORT"))
                    {
                        position.ValueCurrent = -(accountUpdate.a.P[i].pa.ToDecimal());
                        position.SecurityNameCode = accountUpdate.a.P[i].s + "_SHORT";

                        portfolio.SetNewPosition(position);
                    }

                    position.UnrealizedPnl = accountUpdate.a.P[i].up.ToDecimal();

                    PortfolioEvent(Portfolios);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                TradeUpdateEvent responseOrder = JsonConvert.DeserializeObject<TradeUpdateEvent>(message);

                Order newOrder = new Order();

                OrderStateType orderState = OrderStateType.None;

                switch (responseOrder.o.X)
                {
                    case "FILLED":
                        orderState = OrderStateType.Done;
                        break;
                    case "PARTIALLY_FILLED":
                        orderState = OrderStateType.Partial;
                        break;
                    case "CANCELED":
                        orderState = OrderStateType.Cancel;
                        break;
                    case "NEW":
                        orderState = OrderStateType.Active;
                        break;
                    case "EXPIRED":
                        orderState = OrderStateType.Fail;
                        break;
                    default:
                        orderState = OrderStateType.None;
                        break;
                }

                try
                {
                    newOrder.NumberUser = Convert.ToInt32(responseOrder.o.c);
                }
                catch
                {
                    // ignore
                }

                newOrder.NumberMarket = responseOrder.o.i.ToString();
                newOrder.SecurityNameCode = responseOrder.o.s;
                newOrder.SecurityClassCode = responseOrder.o.N;
                newOrder.PortfolioNumber = "BingXFutures";
                newOrder.Side = responseOrder.o.S.Equals("BUY") ? Side.Buy : Side.Sell;
                newOrder.Price = responseOrder.o.p.Replace('.', ',').ToDecimal();
                newOrder.Volume = responseOrder.o.q.Replace('.', ',').ToDecimal();
                newOrder.State = orderState;
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseOrder.E));
                newOrder.TypeOrder = responseOrder.o.o.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                newOrder.ServerType = ServerType.BingXFutures;

                MyOrderEvent(newOrder);

                if (orderState == OrderStateType.Done
                    || orderState == OrderStateType.Partial)
                {
                    UpdateMyTrade(message);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(string message)
        {
            try
            {
                TradeUpdateEvent responseOrder = JsonConvert.DeserializeObject<TradeUpdateEvent>(message);
                MyTrade newTrade = new MyTrade();

                newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseOrder.E));
                newTrade.SecurityNameCode = responseOrder.o.s;
                newTrade.NumberOrderParent = responseOrder.o.i;
                newTrade.Price = responseOrder.o.ap.ToDecimal();
                newTrade.NumberTrade = TimeManager.GetTimeStampMilliSecondsToDateTime(DateTime.Now).ToString();
                newTrade.Side = responseOrder.o.S.Contains("BUY") ? Side.Buy : Side.Sell;

                decimal previousVolume = GetExecuteVolumeInOrder(newTrade.NumberOrderParent);

                newTrade.Volume = responseOrder.o.z.ToDecimal() - previousVolume;

                MyTradeEvent(newTrade);

                _myTrades.Add(newTrade);

                while (_myTrades.Count > 1000)
                {
                    _myTrades.RemoveAt(0);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private decimal GetExecuteVolumeInOrder(string orderNum)
        {
            decimal result = 0;

            for (int i = 0; i < _myTrades.Count; i++)
            {
                if (_myTrades[i].NumberOrderParent == orderNum)
                {
                    result += _myTrades[i].Volume;
                }
            }

            return result;
        }

        private List<MyTrade> _myTrades = new List<MyTrade>();

        private DateTime _lastTimeMd;

        private void UpdateDepth(string message)
        {
            try
            {
                ResponseWSBingXFuturesMessage<MarketDepthDataMessage> responceDepths =
                    JsonConvert.DeserializeAnonymousType(message, new ResponseWSBingXFuturesMessage<MarketDepthDataMessage>());

                MarketDepth depth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                depth.SecurityNameCode = responceDepths.dataType.Split('@')[0]; // from BTC-USDT@depth20@500ms we get BTC-USDT

                for (int i = 0; i < responceDepths.data.asks.Count; i++)
                {
                    MarketDepthLevel level = new MarketDepthLevel()
                    {
                        Price = responceDepths.data.asks[i][0].ToDecimal(),
                        Ask = responceDepths.data.asks[i][1].ToDecimal()
                    };

                    ascs.Insert(0, level);
                }

                for (int i = 0; i < responceDepths.data.bids.Count; i++)
                {
                    bids.Add(new MarketDepthLevel()
                    {
                        Price = responceDepths.data.bids[i][0].ToDecimal(),
                        Bid = responceDepths.data.bids[i][1].ToDecimal()
                    });
                }

                depth.Asks = ascs;
                depth.Bids = bids;

                depth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responceDepths.ts));

                if (depth.Time <= _lastTimeMd)
                {
                    depth.Time = _lastTimeMd.AddTicks(1);
                }

                _lastTimeMd = depth.Time;

                MarketDepthEvent(depth);
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        #endregion

        #region 10 Trade

        private RateGate _sendOrderRateGate = new RateGate(1, TimeSpan.FromMilliseconds(210)); // individual IP speed limit is 5 requests per 1 second

        public void SendOrder(Order order)
        {
            _generalRateGate3.WaitToProceed();
            _sendOrderRateGate.WaitToProceed();

            try
            {
                _hedgeMode = ((ServerParameterBool)ServerParameters[2]).Value;

                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/swap/v2/trade/order", Method.POST);

                string symbol = order.SecurityNameCode;
                string side = order.Side == Side.Buy ? "BUY" : "SELL";

                string positionSide = CheckPositionSide(order);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string quantity = order.Volume.ToString().Replace(",", ".");
                string typeOrder = "";
                string parameters = "";
                string price = "";

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    typeOrder = "MARKET";
                    parameters = $"timestamp={timeStamp}&symbol={symbol}&side={side}&positionSide={positionSide}" +
                        $"&type={typeOrder}&quantity={quantity}&clientOrderID={order.NumberUser}";
                }
                else if (order.TypeOrder == OrderPriceType.Limit)
                {
                    typeOrder = "LIMIT";
                    price = order.Price.ToString().Replace(",", ".");
                    parameters = $"timestamp={timeStamp}&symbol={symbol}&side={side}&positionSide={positionSide}" +
                        $"&type={typeOrder}&quantity={quantity}&price={price}&clientOrderID={order.NumberUser}";
                }
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("symbol", symbol);
                request.AddParameter("side", side);
                request.AddParameter("positionSide", positionSide);
                request.AddParameter("type", typeOrder);
                request.AddParameter("quantity", quantity);

                if (typeOrder == "LIMIT")
                    request.AddParameter("price", price);

                request.AddParameter("clientOrderID", order.NumberUser);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<OrderData> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<OrderData>>(json.Content);
                    if (response.code == "0")
                    {
                        order.State = OrderStateType.Active;
                        order.NumberMarket = response.data.order.orderId;
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Order execution error: code - {response.code} | message - {response.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }

                MyOrderEvent.Invoke(order);
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
            }
        }

        private string CheckPositionSide(Order order)
        {
            try
            {
                string positionSide = "";

                if (!_hedgeMode)
                {
                    positionSide = "BOTH";
                    return positionSide;
                }

                if (order.PositionConditionType == OrderPositionConditionType.Close)
                {
                    // Combinations of opening/closing trades
                    // open / buy LONG: side = BUY & positionSide = LONG
                    // close / sell LONG: side = SELL & positionSide = LONG
                    // open / sell SHORT: side = SELL & positionSide = SHORT
                    // close / buy SHORT: side = BUY & positionSide = SHORT

                    if (order.Side == Side.Sell)
                    {
                        positionSide = "LONG";
                    }
                    else if (order.Side == Side.Buy)
                    {
                        positionSide = "SHORT";
                    }
                }
                else if (order.PositionConditionType == OrderPositionConditionType.Open || order.PositionConditionType == OrderPositionConditionType.None)
                {
                    positionSide = order.Side == Side.Buy ? "LONG" : "SHORT";
                }

                return positionSide;
            }
            catch (Exception exception)
            {
                SendLogMessage($"{exception.Message} {exception.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        public void CancelAllOrders()
        {

        }

        public void CancelAllOrdersToSecurity(Security security)
        {

        }

        private RateGate _cancelOrderRateGate = new RateGate(1, TimeSpan.FromMilliseconds(210)); // individual IP speed limit is 5 requests per 1 second

        public void CancelOrder(Order order)
        {
            _generalRateGate3.WaitToProceed();
            _cancelOrderRateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/swap/v2/trade/order", Method.DELETE);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}&symbol={order.SecurityNameCode}&orderId={order.NumberMarket}&clientOrderID={order.NumberUser}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("symbol", order.SecurityNameCode);
                request.AddParameter("orderId", order.NumberMarket);
                request.AddParameter("clientOrderID", order.NumberUser);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<OrderData> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<OrderData>>(json.Content);
                    if (response.code == "0")
                    {

                    }
                    else
                    {
                        GetOrderStatus(order);
                        SendLogMessage($"Order cancel error: code - {response.code} | message - {response.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    GetOrderStatus(order);
                    SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
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

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        private RateGate _getOpenOrdersRateGate = new RateGate(1, TimeSpan.FromMilliseconds(210)); // individual IP speed limit is 5 requests per 1 second

        public void GetAllActivOrders()
        {
            _generalRateGate3.WaitToProceed();
            _getOpenOrdersRateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/swap/v2/trade/openOrders", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<OpenOrdersData> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<OpenOrdersData>>(json.Content);
                    if (response.code == "0")
                    {
                        for (int i = 0; i < response.data.orders.Count; i++)
                        {
                            Order openOrder = new Order();

                            switch (response.data.orders[i].status)
                            {
                                case "FILLED":
                                    openOrder.State = OrderStateType.Done;
                                    break;
                                case "PARTIALLY_FILLED":
                                    openOrder.State = OrderStateType.Partial;
                                    break;
                                case "CANCELED":
                                    openOrder.State = OrderStateType.Cancel;
                                    break;
                                case "NEW":
                                    openOrder.State = OrderStateType.Active;
                                    break;
                                case "EXPIRED":
                                    openOrder.State = OrderStateType.Fail;
                                    break;
                                case "PENDING":
                                    openOrder.State = OrderStateType.Active;
                                    break;
                                default:
                                    openOrder.State = OrderStateType.None;
                                    break;
                            }

                            string numberUser = response.data.orders[i].clientOrderId;

                            if (numberUser != "")
                            {
                                openOrder.NumberUser = Convert.ToInt32(response.data.orders[i].clientOrderId);
                            }
                            openOrder.NumberMarket = response.data.orders[i].orderId.ToString();
                            openOrder.SecurityNameCode = response.data.orders[i].symbol;
                            openOrder.SecurityClassCode = response.data.orders[i].symbol.Split('-')[1];
                            openOrder.PortfolioNumber = "BingXFutures";
                            openOrder.Side = response.data.orders[i].side.Equals("BUY") ? Side.Buy : Side.Sell;
                            openOrder.Price = response.data.orders[i].price.Replace('.', ',').ToDecimal();
                            openOrder.Volume = response.data.orders[i].origQty.Replace('.', ',').ToDecimal();
                            openOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(response.data.orders[i].time));
                            openOrder.TypeOrder = response.data.orders[i].type.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                            openOrder.ServerType = ServerType.BingXFutures;

                            if (MyOrderEvent != null)
                            {
                                MyOrderEvent(openOrder);
                            }
                        }
                    }
                    else
                    {
                        SendLogMessage($"Get open orders error: code - {response.code} | message - {response.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public void GetOrderStatus(Order order)
        {
            GetOrderStatusBySecurity(order);

            GetMyTradesBySecurity(order);
        }

        private RateGate _getMyTradesRateGate = new RateGate(1, TimeSpan.FromMilliseconds(210)); // individual IP speed limit is 5 requests per 1 second

        private void GetMyTradesBySecurity(Order order)
        {
            _generalRateGate2.WaitToProceed();
            _getMyTradesRateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/swap/v2/trade/allFillOrders", Method.GET);

                string startTs = TimeManager.GetTimeStampMilliSecondsToDateTime(DateTime.Now.AddDays(-1)).ToString();
                string endTs = TimeManager.GetTimeStampMilliSecondsToDateTime(DateTime.Now.AddDays(1)).ToString();

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}&orderId={order.NumberMarket}&tradingUnit=COIN&startTs={startTs}&endTs={endTs}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("orderId", order.NumberMarket);
                request.AddParameter("tradingUnit", "COIN");
                request.AddParameter("startTs", startTs);
                request.AddParameter("endTs", endTs);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<FillOrdersData> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<FillOrdersData>>(json.Content);

                    if (response.code == "0")
                    {
                        for (int i = 0; i < response.data.fill_orders.Count; i++)
                        {
                            if (response.data.fill_orders[i].orderId != order.NumberMarket)
                            {
                                continue;
                            }

                            MyTrade newTrade = new MyTrade();

                            newTrade.Time = Convert.ToDateTime(response.data.fill_orders[i].filledTime);
                            newTrade.SecurityNameCode = response.data.fill_orders[i].symbol;
                            newTrade.NumberOrderParent = response.data.fill_orders[i].orderId;
                            newTrade.Price = response.data.fill_orders[i].price.ToDecimal();
                            newTrade.NumberTrade = TimeManager.GetTimeStampMilliSecondsToDateTime(DateTime.Now).ToString();
                            newTrade.Side = response.data.fill_orders[i].side.Contains("BUY") ? Side.Buy : Side.Sell;
                            newTrade.Volume = response.data.fill_orders[i].volume.ToDecimal();

                            if (MyTradeEvent != null)
                            {
                                MyTradeEvent(newTrade);
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private RateGate _getOrderStatusRateGate = new RateGate(1, TimeSpan.FromMilliseconds(210)); // individual IP speed limit is 5 requests per 1 second

        private void GetOrderStatusBySecurity(Order order)
        {
            _generalRateGate2.WaitToProceed();
            _getOrderStatusRateGate.WaitToProceed();

            try
            {
                RestClient client = new RestClient(_baseUrl);
                RestRequest request = new RestRequest("/openApi/swap/v2/trade/order", Method.GET);

                string timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string parameters = $"timestamp={timeStamp}&symbol={order.SecurityNameCode}&orderId={order.NumberMarket}&clientOrderID={order.NumberUser}";
                string sign = CalculateHmacSha256(parameters);

                request.AddParameter("timestamp", timeStamp);
                request.AddParameter("symbol", order.SecurityNameCode);
                request.AddParameter("orderId", order.NumberMarket);
                request.AddParameter("clientOrderID", order.NumberUser);
                request.AddParameter("signature", sign);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                IRestResponse json = client.Execute(request);

                if (json.StatusCode == HttpStatusCode.OK)
                {
                    ResponseFuturesBingXMessage<OrderData> response = JsonConvert.DeserializeObject<ResponseFuturesBingXMessage<OrderData>>(json.Content);
                    if (response.code == "0")
                    {
                        Order openOrder = new Order();

                        switch (response.data.order.status)
                        {
                            case "FILLED":
                                openOrder.State = OrderStateType.Done;
                                break;
                            case "PARTIALLY_FILLED":
                                openOrder.State = OrderStateType.Partial;
                                break;
                            case "CANCELLED":
                                openOrder.State = OrderStateType.Cancel;
                                break;
                            case "NEW":
                                openOrder.State = OrderStateType.Active;
                                break;
                            case "EXPIRED":
                                openOrder.State = OrderStateType.Fail;
                                break;
                            case "PENDING":
                                openOrder.State = OrderStateType.Active;
                                break;
                            default:
                                openOrder.State = OrderStateType.None;
                                break;
                        }

                        string numberUser = response.data.order.clientOrderId;

                        if (numberUser != "")
                        {
                            openOrder.NumberUser = Convert.ToInt32(response.data.order.clientOrderId);
                        }
                        openOrder.NumberMarket = response.data.order.orderId.ToString();
                        openOrder.SecurityNameCode = response.data.order.symbol;
                        openOrder.SecurityClassCode = response.data.order.symbol.Split('-')[1];
                        openOrder.PortfolioNumber = "BingXFutures";
                        openOrder.Side = response.data.order.side.Equals("BUY") ? Side.Buy : Side.Sell;
                        openOrder.Price = response.data.order.price.Replace('.', ',').ToDecimal();
                        openOrder.Volume = response.data.order.origQty.Replace('.', ',').ToDecimal();
                        openOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(response.data.order.time));
                        openOrder.TypeOrder = response.data.order.type.Equals("MARKET") ? OrderPriceType.Market : OrderPriceType.Limit;
                        openOrder.ServerType = ServerType.BingXFutures;

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(openOrder);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Get order status error: code - {response.code} | message - {response.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Http State Code: {json.StatusCode} - {json.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 11 Queries

        private const string _baseUrl = "https://open-api.bingx.com";

        private readonly HttpClient _httpPublicClient = new HttpClient();

        private string CreateListenKey()
        {
            _generalRateGate2.WaitToProceed();

            try
            {
                string endpoint = "/openApi/user/auth/userDataStream";

                RestRequest request = new RestRequest(endpoint, Method.POST);
                request.AddHeader("X-BX-APIKEY", _publicKey);

                string json = new RestClient(_baseUrl).Execute(request).Content;

                ListenKeyBingXFutures responseStr = JsonConvert.DeserializeObject<ListenKeyBingXFutures>(json);

                _timeLastUpdateListenKey = DateTime.Now;

                return responseStr.listenKey;
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private DateTime _timeLastUpdateListenKey = DateTime.MinValue;

        private RateGate _requestListenKeyRateGate = new RateGate(10, TimeSpan.FromSeconds(1)); // индивидуальный лимит скорости IP составляет 100 запросов в 10 секунд

        private void RequestListenKey()
        {
            _timeLastUpdateListenKey = DateTime.Now;

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(5000);
                    continue;
                }

                if (_timeLastUpdateListenKey.AddMinutes(30) > DateTime.Now)
                {   // sleep for 30 minutes
                    Thread.Sleep(10000);
                    continue;
                }

                try
                {
                    if (_listenKey == "")
                    {
                        continue;
                    }

                    _generalRateGate2.WaitToProceed();
                    _requestListenKeyRateGate.WaitToProceed();

                    string endpoint = "/openApi/user/auth/userDataStream";

                    RestClient client = new RestClient(_baseUrl);
                    RestRequest request = new RestRequest(endpoint, Method.PUT);

                    request.AddQueryParameter("listenKey", _listenKey);

                    IRestResponse response = client.Execute(request);

                    _timeLastUpdateListenKey = DateTime.Now;
                }
                catch
                {
                    SendLogMessage("Request Listen Key Error", LogMessageType.Error);
                }
            }
        }

        #endregion

        #region 12 Log

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            if (LogMessageEvent != null)
                LogMessageEvent(message, messageType);
        }

        #endregion

        #region 13 Helpers

        private string Decompress(byte[] data)
        {
            try
            {
                using (System.IO.MemoryStream compressedStream = new System.IO.MemoryStream(data))
                {
                    using (GZipStream decompressor = new GZipStream(compressedStream, CompressionMode.Decompress))
                    {
                        using (System.IO.MemoryStream resultStream = new System.IO.MemoryStream())
                        {
                            decompressor.CopyTo(resultStream);

                            return Encoding.UTF8.GetString(resultStream.ToArray());
                        }
                    }
                }
            }
            catch
            {
                SendLogMessage("Decompress error", LogMessageType.Error);
                return null;
            }
        }

        private string CalculateHmacSha256(string parametrs)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(_secretKey);
            byte[] inputBytes = Encoding.UTF8.GetBytes(parametrs);
            using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private string GenerateNewId()
        {
            return Guid.NewGuid().ToString();
        }

        #endregion
    }
}