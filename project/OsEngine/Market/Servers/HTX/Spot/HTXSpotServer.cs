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
using OsEngine.Market.Servers.HTX.Entity;
using OsEngine.Market.Servers.HTX.Spot.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;

namespace OsEngine.Market.Servers.HTX.Spot
{
    public class HTXSpotServer : AServer
    {
        public HTXSpotServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            HTXSpotServerRealization realization = new HTXSpotServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterBoolean("Extended Data", false);

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label269;
        }
    }

    public class HTXSpotServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public HTXSpotServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.IsBackground = true;
            threadMessageReaderPublic.Name = "MessageReaderPublic";
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.IsBackground = true;
            threadMessageReaderPrivate.Name = "MessageReaderPrivate";
            threadMessageReaderPrivate.Start();

            Thread threadUpdatePortfolio = new Thread(ThreadUpdatePortfolio);
            threadUpdatePortfolio.IsBackground = true;
            threadUpdatePortfolio.Name = "ThreadUpdatePortfolio";
            threadUpdatePortfolio.Start();

            Thread threadCheckAliveWebSocket = new Thread(CheckAliveWebSocket);
            threadCheckAliveWebSocket.IsBackground = true;
            threadCheckAliveWebSocket.Name = "CheckAliveWebSocketHTXSpot";
            threadCheckAliveWebSocket.Start();
        }

        public DateTime ServerTime { get; set; }

        public void Connect(WebProxy proxy)
        {
            _accessKey = ((ServerParameterString)ServerParameters[0]).Value;
            _secretKey = ((ServerParameterPassword)ServerParameters[1]).Value;

            if (string.IsNullOrEmpty(_accessKey) ||
                string.IsNullOrEmpty(_secretKey))
            {
                SendLogMessage("Can`t run HTX Spot connector. No keys", LogMessageType.Error);
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
                string url = $"https://{_baseUrl}/v2/market-status";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    _privateUriBuilder = new PrivateUrlBuilder(_accessKey, _secretKey, _baseUrl);
                    _signer = new Signer(_secretKey);

                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
                }
                else
                {
                    SendLogMessage($"Connection can be open. HTXSpot. {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage("Connection can be open. HTXSpot. Error request", LogMessageType.Error);
                Disconnect();
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
                DeleteWebscoektConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _subscribedSecurities.Clear();
            _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
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

        public ServerType ServerType
        {
            get { return ServerType.HTXSpot; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _accessKey;

        private string _secretKey;

        private string _baseUrl = "api.huobi.pro";

        private string _webSocketUrlPublic = "wss://api.huobi.pro/ws";

        private string _webSocketUrlPrivate = "wss://api.huobi.pro/ws/v2";

        private int _limitCandles = 2000;

        private PrivateUrlBuilder _privateUriBuilder;

        private Signer _signer;

        private bool _extendedMarketData;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            try
            {
                string url = $"https://{_baseUrl}/v1/settings/common/market-symbols";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<ResponseSecurities>> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<ResponseSecurities>>());

                    List<Security> securities = new List<Security>();

                    if (response.status == "ok")
                    {
                        for (int i = 0; i < response.data.Count; i++)
                        {
                            ResponseSecurities item = response.data[i];

                            if (item.state == "online")
                            {
                                Security newSecurity = new Security();

                                newSecurity.Exchange = ServerType.HTXSpot.ToString();
                                newSecurity.Name = item.symbol;
                                newSecurity.NameFull = item.symbol;
                                newSecurity.NameClass = item.qc;
                                newSecurity.NameId = item.symbol;
                                newSecurity.SecurityType = SecurityType.CurrencyPair;
                                newSecurity.DecimalsVolume = Convert.ToInt32(item.ap);
                                newSecurity.Lot = 1;
                                newSecurity.VolumeStep = Convert.ToInt32(item.ap).GetValueByDecimals();
                                newSecurity.PriceStep = Convert.ToInt32(item.pp).GetValueByDecimals();
                                newSecurity.Decimals = Convert.ToInt32(item.pp);
                                newSecurity.PriceStepCost = newSecurity.PriceStep;
                                newSecurity.State = SecurityStateType.Activ;
                                newSecurity.MinTradeAmount = item.minov.ToDecimal();

                                if (item.symbol == "btcusdt")
                                {
                                    newSecurity.MinTradeAmount = 10;
                                }

                                newSecurity.MinTradeAmountType = MinTradeAmountType.C_Currency;

                                securities.Add(newSecurity);
                            }
                        }

                        SecurityEvent(securities);
                    }
                    else
                    {
                        SendLogMessage($"Securities error. {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Securities error. Code: {responseMessage.StatusCode}, {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(50));

        public List<Portfolio> Portfolios;

        public void GetPortfolios()
        {
            if (Portfolios == null)
            {
                GetNewPortfolio();
            }

            CreatePositions(true);
            GetUSDTMasterPortfolio(true);
        }

        private void ThreadUpdatePortfolio()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(15000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (Portfolios == null)
                    {
                        continue;
                    }

                    CreatePositions(false);
                    GetUSDTMasterPortfolio(false);
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void GetNewPortfolio()
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                Portfolios = new List<Portfolio>();

                string url = _privateUriBuilder.Build("GET", "/v1/account/accounts");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<ResponsePortfolios>> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<ResponsePortfolios>>());

                    if (response.status == "ok")
                    {
                        List<ResponsePortfolios> item = response.data;

                        for (int i = 0; item != null && i < item.Count; i++)
                        {
                            Portfolio portfolio = new Portfolio();
                            portfolio.Number = $"HTX_{item[i].type}_{item[i].id}_Portfolio";
                            portfolio.ValueBegin = 1;
                            portfolio.ValueCurrent = 1;
                            portfolio.ValueBlocked = 0;

                            Portfolios.Add(portfolio);
                        }

                        PortfolioEvent(Portfolios);
                    }
                    else
                    {
                        SendLogMessage($"Portfolio error.  {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    if (responseMessage.Content.Contains("Incorrect Access key [Access key错误]")
                            || responseMessage.Content.Contains("Verification failure [校验失败]"))
                    {
                        Disconnect();
                    }

                    SendLogMessage($"Portfolio error. Code: {responseMessage.StatusCode}, {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreatePositions(bool IsUpdateValueBegin)
        {
            if (Portfolios == null)
            {
                return;
            }

            _rateGatePortfolio.WaitToProceed();

            try
            {
                for (int i = 0; i < Portfolios.Count; i++)
                {
                    Portfolio portfolio = Portfolios[i];

                    string type = portfolio.Number.Split('_')[1];
                    string id = portfolio.Number.Split('_')[2]; ;

                    string url = _privateUriBuilder.Build("GET", $"/v1/account/accounts/{id}/balance");

                    RestClient client = new RestClient(url);
                    RestRequest request = new RestRequest(Method.GET);
                    IRestResponse responseMessage = client.Execute(request);

                    string JsonResponse = responseMessage.Content;

                    if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        ResponseRestMessage<ResponsePositions> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<ResponsePositions>());

                        if (response.status == "ok")
                        {
                            if (response.data == null)
                            {
                                continue;
                            }

                            ResponsePositions positions = response.data;

                            for (int j = 0; j < response.data.list.Count; j++)
                            {
                                PositionOnBoard pos = new PositionOnBoard();

                                if (response.data.list[j].type == "trade" && response.data.list[j].balance == "0")
                                {
                                    continue;
                                }

                                if (response.data.list[j].type == "frozen")
                                {
                                    continue;
                                }

                                pos.PortfolioName = $"HTX_{type}_{id}_Portfolio";
                                pos.SecurityNameCode = response.data.list[j].currency;

                                if (response.data.list[j].type == "trade")
                                {
                                    pos.ValueCurrent = Math.Round(response.data.list[j].balance.ToDecimal(), 6);

                                    if (j != response.data.list.Count - 1)
                                    {
                                        if (response.data.list[j + 1].type == "frozen"
                                            && response.data.list[j].currency == response.data.list[j + 1].currency)
                                        {
                                            pos.ValueBlocked = Math.Round(response.data.list[j + 1].balance.ToDecimal(), 6);
                                        }
                                    }

                                    if (IsUpdateValueBegin)
                                    {
                                        pos.ValueBegin = Math.Round(response.data.list[j].balance.ToDecimal(), 6);
                                    }
                                }

                                portfolio.SetNewPosition(pos);
                            }
                        }
                        else
                        {
                            SendLogMessage($"Positions error.  {responseMessage.Content}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        if (responseMessage.Content.Contains("Incorrect Access key [Access key错误]")
                            || responseMessage.Content.Contains("Verification failure [校验失败]"))
                        {
                            Disconnect();
                        }

                        SendLogMessage($"Positions error. Code: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.Error);
                    }
                }

                PortfolioEvent(Portfolios);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private RateGate _rateGateAccountValuation = new RateGate(3, TimeSpan.FromSeconds(1));

        private void GetUSDTMasterPortfolio(bool IsUpdateValueBegin)
        {
            if (Portfolios == null)
            {
                return;
            }

            _rateGateAccountValuation.WaitToProceed();

            try
            {
                string url = _privateUriBuilder.Build("GET", "/v2/account/valuation");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                string JsonResponse = responseMessage.Content;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseRestMessage<ResponseAccountValuation> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<ResponseAccountValuation>());

                    if (response.code == "200")
                    {
                        for (int i = 0; i < Portfolios.Count; i++)
                        {
                            Portfolio portfolio = Portfolios[i];

                            string type = portfolio.Number.Split('_')[1];

                            for (int j = 0; j < response.data.profitAccountBalanceList.Count; j++)
                            {
                                if (response.data.profitAccountBalanceList[j].distributionType == "1"
                                    && type == "spot")
                                {
                                    if (IsUpdateValueBegin)
                                    {
                                        portfolio.ValueBegin = Math.Round(response.data.profitAccountBalanceList[j].accountBalanceUsdt.ToDecimal(), 4);
                                    }

                                    portfolio.ValueCurrent = Math.Round(response.data.profitAccountBalanceList[j].accountBalanceUsdt.ToDecimal(), 4);

                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (response.message.Contains("Incorrect Access key [Access key错误]")
                            || response.message.Contains("Verification failure [校验失败]"))
                        {
                            Disconnect();
                        }

                        SendLogMessage($"Master Portfolio error: {response.code}, {response.message}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Master Portfolio error. Code: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.Error);
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

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
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

            int countNeedToLoad = GetCountCandlesToLoad();

            List<Candle> allCandles = new List<Candle>();

            if (countNeedToLoad > _limitCandles)
            {
                countNeedToLoad = _limitCandles;
            }

            string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

            List<Candle> candles = RequestCandleHistory(security.Name, interval, countNeedToLoad);

            if (candles == null || candles.Count == 0)
            {
                return null;
            }

            if (allCandles.Count > 0
                && allCandles[allCandles.Count - 1].TimeStart == candles[0].TimeStart)
            {
                candles.RemoveAt(0);
            }

            if (candles.Count == 0)
            {
                return null;
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
            }

            allCandles.AddRange(candles);

            if (allCandles != null && allCandles.Count > 0)
            {
                for (int i = 1; i < allCandles.Count; i++)
                {
                    if (allCandles[i - 1].TimeStart == allCandles[i].TimeStart)
                    {
                        allCandles.RemoveAt(i);
                        i--;
                    }
                }
            }

            return allCandles;
        }

        private int GetCountCandlesToLoad()
        {
            for (int i = 0; i < ServerParameters.Count; i++)
            {
                if (ServerParameters[i].Name.Equals(OsLocalization.Market.ServerParam6))
                {
                    ServerParameterInt Param = (ServerParameterInt)ServerParameters[i];
                    return Param.Value;
                }
            }

            return 300;
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
                timeFrameMinutes == 240 ||
                timeFrameMinutes == 1440)
            {
                return true;
            }
            return false;
        }

        private string GetInterval(TimeSpan timeFrame)
        {
            switch (timeFrame.TotalMinutes)
            {
                case 1:
                    return "1min";
                case 5:
                    return "5min";
                case 15:
                    return "15min";
                case 30:
                    return "30min";
                case 60:
                    return "60min";
                case 240:
                    return "4hour";
                case 1440:
                    return "1day";
                default:
                    return null;
            }
        }

        private RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private List<Candle> RequestCandleHistory(string security, string interval, int countCandles)
        {
            _rgCandleData.WaitToProceed();

            try
            {
                string queryParam = $"symbol={security}&";
                queryParam += $"period={interval}&";
                queryParam += $"size={countCandles}";

                string url = $"https://{_baseUrl}/market/history/kline?{queryParam}";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<ResponseCandles>> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<ResponseCandles>>());

                    if (response.status == "ok")
                    {
                        return ConvertCandles(response);
                    }
                    else
                    {
                        SendLogMessage($"Candle history error.  {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Candle history error. Code:: {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private List<Candle> ConvertCandles(ResponseRestMessage<List<ResponseCandles>> response)
        {
            if (response == null)
            {
                return null;
            }

            List<Candle> candles = new List<Candle>();

            for (int i = 0; i < response.data.Count; i++)
            {
                ResponseCandles item = response.data[i];

                if (CheckCandlesToZeroData(item))
                {
                    continue;
                }

                Candle candle = new Candle();

                candle.State = CandleState.Finished;
                candle.TimeStart = TimeManager.GetDateTimeFromTimeStampSeconds(long.Parse(item.id));
                candle.Volume = item.vol.ToDecimal();
                candle.Close = item.close.ToDecimal();
                candle.High = item.high.ToDecimal();
                candle.Low = item.low.ToDecimal();
                candle.Open = item.open.ToDecimal();

                candles.Add(candle);
            }
            candles.Reverse();

            return candles;
        }

        private bool CheckCandlesToZeroData(ResponseCandles item)
        {
            if (item.close.ToDecimal() == 0 ||
                item.open.ToDecimal() == 0 ||
                item.high.ToDecimal() == 0 ||
                item.low.ToDecimal() == 0)
            {
                return true;
            }
            return false;
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.UtcNow;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, endTime);
        }

        #endregion

        #region 6 WebSocket creation

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (_FIFOListWebSocketPublicMessage == null)
                {
                    _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
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
                webSocketPublicNew.OnOpen += webSocketPublic_OnOpen;
                webSocketPublicNew.OnMessage += webSocketPublic_OnMessage;
                webSocketPublicNew.OnError += webSocketPublic_OnError;
                webSocketPublicNew.OnClose += webSocketPublic_OnClose;
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
                _webSocketPrivate.OnOpen += webSocketPrivate_OnOpen;
                _webSocketPrivate.OnMessage += webSocketPrivate_OnMessage;
                _webSocketPrivate.OnError += webSocketPrivate_OnError;
                _webSocketPrivate.OnClose += webSocketPrivate_OnClose;
                _webSocketPrivate.ConnectAsync();

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebscoektConnection()
        {
            if (_webSocketPublic != null)
            {
                try
                {
                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        webSocketPublic.OnOpen -= webSocketPublic_OnOpen;
                        webSocketPublic.OnClose -= webSocketPublic_OnClose;
                        webSocketPublic.OnMessage -= webSocketPublic_OnMessage;
                        webSocketPublic.OnError -= webSocketPublic_OnError;

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
                    _webSocketPrivate.OnOpen -= webSocketPrivate_OnOpen;
                    _webSocketPrivate.OnMessage -= webSocketPrivate_OnMessage;
                    _webSocketPrivate.OnError -= webSocketPrivate_OnError;
                    _webSocketPrivate.OnClose -= webSocketPrivate_OnClose;
                    _webSocketPrivate.CloseAsync();
                }
                catch
                {
                    // ignore
                }

                _webSocketPrivate = null;
            }
        }

        private string _lockerCheckActivateionSockets = "lockerCheckActivateionSockets";

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
                }
            }
        }

        #endregion

        #region 7 WebSocket events

        private void webSocketPublic_OnError(object sender, ErrorEventArgs e)
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

        private void webSocketPublic_OnMessage(object sender, MessageEventArgs e)
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
                    _FIFOListWebSocketPublicMessage.Enqueue(Decompress(e.RawData));
                }
                else if (e.IsText)
                {
                    _FIFOListWebSocketPublicMessage.Enqueue(e.Data);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void webSocketPublic_OnClose(object sender, CloseEventArgs e)
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

        private void webSocketPublic_OnOpen(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("Connection Websocket Public Open", LogMessageType.System);
                    CheckActivationSockets();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void webSocketPrivate_OnError(object sender, ErrorEventArgs e)
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

        private void webSocketPrivate_OnMessage(object sender, MessageEventArgs e)
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
                    _FIFOListWebSocketPrivateMessage.Enqueue(Decompress(e.RawData));
                }
                else if (e.IsText)
                {
                    _FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void webSocketPrivate_OnClose(object sender, CloseEventArgs e)
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

        private void webSocketPrivate_OnOpen(object sender, EventArgs e)
        {
            try
            {
                string authRequest = BuildSign(DateTime.UtcNow);
                _webSocketPrivate.SendAsync(authRequest);

                SendLogMessage("Connection Websocket Private Open", LogMessageType.System);
                CheckActivationSockets();
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
                    Thread.Sleep(10000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_webSocketPrivate != null && _webSocketPrivate.ReadyState == WebSocketState.Open ||
                        _webSocketPrivate.ReadyState == WebSocketState.Connecting)
                    {
                        // Supports one-way heartbeat.
                        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                        // _webSocketPrivate.Send($"{{ \"action\": \"ping\", \"data\": {{ \"ts\": {timestamp} }} }}");
                    }
                    else
                    {
                        Disconnect();
                    }

                    for (int i = 0; i < _webSocketPublic.Count; i++)
                    {
                        WebSocket webSocketPublic = _webSocketPublic[i];

                        if (webSocketPublic != null
                            && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            // Supports two-way heartbeat
                            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                            webSocketPublic.SendAsync($"{{\"ping\": \"{timestamp}\"}}");
                        }
                        else
                        {
                            Disconnect();
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        #endregion

        #region 9 Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(400));

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();
                CreateSubscribeSecurityMessageWebSocket(security);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private List<string> _subscribedSecurities = new List<string>();

        private void CreateSubscribeSecurityMessageWebSocket(Security security)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            if (_webSocketPublic == null)
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
                && _subscribedSecurities.Count % 150 == 0)
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

            string clientId = "";

            if (webSocketPublic != null)
            {
                string topic = $"market.{security.Name}.mbp.refresh.20";
                webSocketPublic.SendAsync($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");

                topic = $"market.{security.Name}.trade.detail";
                webSocketPublic.SendAsync($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");

                if (_extendedMarketData)
                {
                    topic = $"market.{security.Name}.ticker";
                    webSocketPublic.SendAsync($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");
                }
            }
        }

        private void SendSubscribePrivate()
        {
            string chOrders = "orders#*";
            string chTrades = "trade.clearing#*#0";
            _webSocketPrivate.SendAsync($"{{\"action\": \"sub\",\"ch\": \"{chOrders}\"}}");
            _webSocketPrivate.SendAsync($"{{\"action\": \"sub\",\"ch\": \"{chTrades}\"}}");
        }

        private void CreatePingMessageWebSocketPublic(string message)
        {
            ResponsePing response = JsonConvert.DeserializeObject<ResponsePing>(message);

            if (_webSocketPublic == null)
            {
                return;
            }
            else
            {
                for (int i = 0; i < _webSocketPublic.Count; i++)
                {
                    WebSocket webSocketPublic = _webSocketPublic[i];

                    try
                    {
                        if (webSocketPublic != null
                        && webSocketPublic?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublic.SendAsync($"{{\"pong\": \"{response.ping}\"}}");
                        }
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                    }
                }
            }
        }

        private void CreatePingMessageWebSocketPrivate(string message)
        {
            ResponsePingPrivate response = JsonConvert.DeserializeObject<ResponsePingPrivate>(message);

            if (_webSocketPrivate == null)
            {
                return;
            }
            else
            {
                _webSocketPrivate.SendAsync($"{{ \"action\": \"pong\", \"data\": {{ \"ts\": {response.data.ts} }} }}");
            }
        }

        private void UnsubscribeFromAllWebSockets()
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

                                    string topic = $"market.{securityName}.mbp.refresh.20";
                                    webSocketPublic.SendAsync($"{{\"action\": \"unsub\",\"ch\": \"{topic}\"}}");

                                    topic = $"market.{securityName}.trade.detail";
                                    webSocketPublic.SendAsync($"{{\"action\": \"unsub\",\"ch\": \"{topic}\"}}");

                                    if (_extendedMarketData)
                                    {
                                        topic = $"market.{securityName}.ticker";
                                        webSocketPublic.SendAsync($"{{\"action\": \"unsub\",\"ch\": \"{topic}\"}}");
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

            if (_webSocketPrivate != null
                && _webSocketPrivate.ReadyState == WebSocketState.Open)
            {
                try
                {
                    string chOrders = "orders#*";
                    string chTrades = "trade.clearing#*#0";
                    _webSocketPrivate.SendAsync($"{{\"action\": \"unsub\",\"ch\": \"{chOrders}\"}}");
                    _webSocketPrivate.SendAsync($"{{\"action\": \"unsub\",\"ch\": \"{chTrades}\"}}");
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
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

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

                    try
                    {
                        if (message.Contains("ping"))
                        {
                            CreatePingMessageWebSocketPublic(message);
                            continue;
                        }

                        if (message.Contains("pong"))
                        {
                            continue;
                        }
                        //if (message.Contains("kline"))
                        //{
                        //    _allCandleSeries = message;
                        //    continue;
                        //}

                        if (message.Contains("mbp"))
                        {
                            UpdateDepth(message);
                            continue;
                        }

                        if (message.Contains("trade.detail"))
                        {
                            UpdateTrade(message);
                            continue;
                        }

                        if (message.Contains("ticker"))
                        {
                            UpdateTicker(message);
                            continue;
                        }

                        if (message.Contains("error"))
                        {
                            SendLogMessage("Message public str: \n" + message, LogMessageType.Error);
                        }
                    }
                    catch (Exception exception)
                    {
                        SendLogMessage(exception.ToString(), LogMessageType.Error);
                        SendLogMessage("message str: \n" + message, LogMessageType.Error);
                        Thread.Sleep(5000);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

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

                    try
                    {
                        if (message.Contains("ping"))
                        {
                            CreatePingMessageWebSocketPrivate(message);
                            continue;
                        }

                        if (message.Contains("auth"))
                        {
                            SendSubscribePrivate();
                            continue;
                        }

                        if (message.Contains("orders#"))
                        {
                            UpdateOrder(message);
                        }

                        if (message.Contains("trade.clearing"))
                        {
                            UpdateMyTrade(message);
                        }

                        if (message.Contains("pong"))
                        {
                            continue;
                        }

                        if (message.Contains("error"))
                        {
                            SendLogMessage("Message private str: \n" + message, LogMessageType.Error);
                        }
                    }
                    catch (Exception exception)
                    {
                        SendLogMessage(exception.ToString(), LogMessageType.Error);
                        SendLogMessage("Message str: \n" + message, LogMessageType.Error);
                        Thread.Sleep(5000);
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
                ResponseWebSocketMessage<TradesData> response = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<TradesData>());

                if (response == null)
                {
                    return;
                }

                if (response.tick == null)
                {
                    return;
                }

                List<ResponseTrades> item = response.tick.data;

                for (int i = 0; i < item.Count; i++)
                {
                    Trade trade = new Trade();
                    trade.SecurityNameCode = GetSecurityName(response.ch);
                    trade.Price = item[i].price.ToDecimal();
                    trade.Id = item[i].tradeId;
                    trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item[i].ts));
                    trade.Volume = item[i].amount.ToDecimal();
                    trade.Side = item[i].direction.Equals("buy") ? Side.Buy : Side.Sell;

                    NewTradesEvent(trade);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdateTicker(string message)
        {
            try
            {
                ResponseWebSocketMessage<TickerItem> response = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<TickerItem>());

                if (response == null
                    || response.tick == null)
                {
                    return;
                }

                SecurityVolumes volume = new SecurityVolumes();

                volume.SecurityNameCode = GetSecurityName(response.ch);
                volume.Volume24h = response.tick.amount.ToDecimal();
                volume.Volume24hUSDT = response.tick.vol.ToDecimal();
                volume.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)response.ts.ToDecimal());

                Volume24hUpdateEvent?.Invoke(volume);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdateDepth(string message)
        {
            try
            {
                //Thread.Sleep(1);

                ResponseWebSocketMessage<ResponseDepth> response = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<ResponseDepth>());
                ResponseDepth item = response.tick;

                if (item == null)
                {
                    return;
                }

                if (item.asks.Count == 0 && item.bids.Count == 0)
                {
                    return;
                }

                MarketDepth marketDepth = new MarketDepth();

                List<MarketDepthLevel> asks = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                marketDepth.SecurityNameCode = GetSecurityName(response.ch);

                if (item.asks.Count > 0)
                {
                    for (int i = 0; i < item.asks.Count; i++)
                    {
                        double ask = item.asks[i][1].ToString().ToDouble();
                        double price = item.asks[i][0].ToString().ToDouble();

                        if (ask == 0 ||
                            price == 0)
                        {
                            continue;
                        }

                        MarketDepthLevel level = new MarketDepthLevel();
                        level.Ask = ask;
                        level.Price = price;
                        asks.Add(level);
                    }
                }

                if (item.bids.Count > 0)
                {
                    for (int i = 0; i < item.bids.Count; i++)
                    {
                        double bid = item.bids[i][1].ToString().ToDouble();
                        double price = item.bids[i][0].ToString().ToDouble();

                        if (bid == 0
                            || price == 0)
                        {
                            continue;
                        }

                        MarketDepthLevel level = new MarketDepthLevel();
                        level.Bid = bid;
                        level.Price = price;
                        bids.Add(level);
                    }
                }

                if (asks.Count == 0
                    || bids.Count == 0)
                {
                    return;
                }

                marketDepth.Asks = asks;
                marketDepth.Bids = bids;

                marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(response.ts));

                if (marketDepth.Time <= _lastMdTime)
                {
                    marketDepth.Time = _lastMdTime.AddTicks(1);
                }

                _lastMdTime = marketDepth.Time;

                MarketDepthEvent(marketDepth);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        DateTime _lastMdTime = DateTime.MinValue;

        private string GetSecurityName(string ch)
        {
            string[] strings = ch.Split('.');
            return strings[1];
        }

        private void UpdateMyTrade(string message)
        {
            try
            {
                ResponseMyTrade response = JsonConvert.DeserializeObject<ResponseMyTrade>(message);

                if (response.code != null)
                {
                    return;
                }

                MyTradeData item = response.data;

                MyTrade myTrade = new MyTrade();

                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.tradeTime));
                myTrade.NumberOrderParent = item.orderId;
                myTrade.NumberTrade = item.tradeId;
                myTrade.Price = item.tradePrice.ToDecimal();
                myTrade.SecurityNameCode = item.symbol;
                myTrade.Side = item.orderSide.Equals("buy") ? Side.Buy : Side.Sell;

                string commissionSecName = item.feeCurrency;

                if (myTrade.SecurityNameCode.StartsWith(commissionSecName))
                {
                    myTrade.Volume = item.tradeVolume.ToDecimal() - item.transactFee.ToDecimal();
                }
                else
                {
                    myTrade.Volume = item.tradeVolume.ToDecimal();
                }

                MyTradeEvent(myTrade);

                if (item.orderStatus.Equals("partial-filled") || item.orderStatus.Equals("filled"))
                {
                    Order newOrder = new Order();
                    newOrder.ServerType = ServerType.HTXSpot;
                    newOrder.SecurityNameCode = item.symbol;
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.tradeTime));
                    newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.orderCreateTime));

                    try
                    {
                        newOrder.NumberUser = Convert.ToInt32(item.clientOrderId);
                    }
                    catch
                    {
                        //ignore
                    }

                    newOrder.NumberMarket = item.orderId.ToString();
                    newOrder.Side = item.orderSide.Equals("buy") ? Side.Buy : Side.Sell;
                    newOrder.State = GetOrderState(item.orderStatus);
                    newOrder.Volume = item.orderSize.ToDecimal();
                    //newOrder.VolumeExecute = item.tradeVolume.ToDecimal();
                    newOrder.Price = item.orderPrice.ToDecimal();

                    string source = "spot";

                    if (item.source == "margin-api")
                    {
                        source = "margin";
                    }

                    if (item.source == "super-margin-api")
                    {
                        source = "super-margin";
                    }

                    newOrder.PortfolioNumber = $"HTX_{source}_{item.accountId}_Portfolio";

                    MyOrderEvent(newOrder);
                }
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
                ResponseChannelUpdateOrder response = JsonConvert.DeserializeObject<ResponseChannelUpdateOrder>(message);

                ResponseChannelUpdateOrder.Data item = response.data;

                if (response.code != null)
                {
                    return;
                }

                if (item.eventType.Equals("creation")
                    || item.eventType.Equals("cancellation")
                    || item.eventType.Equals("trade"))
                {
                    Order newOrder = new Order();

                    if (item.eventType.Equals("creation"))
                    {
                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.orderCreateTime));
                        newOrder.TimeCreate = newOrder.TimeCallBack;
                    }
                    else if (item.eventType.Equals("cancellation"))
                    {
                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.lastActTime));
                    }
                    else if (item.eventType.Equals("trade"))
                    {
                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.tradeTime));
                    }

                    newOrder.ServerType = ServerType.HTXSpot;
                    newOrder.SecurityNameCode = item.symbol;

                    if (item.clientOrderId != null)
                    {
                        try
                        {
                            string numberFull = item.clientOrderId;
                            string numUser = numberFull.Replace("AAe2ccbd47", "");
                            newOrder.NumberUser = Convert.ToInt32(numUser);
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    newOrder.NumberMarket = item.orderId.ToString();
                    newOrder.Side = item.type.Split('-')[0].Equals("buy") ? Side.Buy : Side.Sell;
                    newOrder.State = GetOrderState(item.orderStatus);

                    newOrder.Volume = item.orderSize.ToDecimal();
                    newOrder.Price = item.orderPrice.ToDecimal();

                    if (item.eventType.Equals("trade")
                        && newOrder.State == OrderStateType.Done)
                    {
                        newOrder.Volume = item.tradeVolume.ToDecimal();
                        newOrder.Price = item.tradePrice.ToDecimal();
                    }

                    if (item.type.Split('-')[1] == "market")
                    {
                        newOrder.TypeOrder = OrderPriceType.Market;
                    }

                    if (item.type.Split('-')[1] == "limit")
                    {
                        newOrder.TypeOrder = OrderPriceType.Limit;
                    }

                    string source = "spot";

                    if (item.orderSource == "margin-api")
                    {
                        source = "margin";
                    }

                    if (item.orderSource == "super-margin-api")
                    {
                        source = "super-margin";
                    }

                    newOrder.PortfolioNumber = $"HTX_{source}_{item.accountId}_Portfolio";

                    MyOrderEvent(newOrder);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("submitted"):
                    stateType = OrderStateType.Active;
                    break;
                case ("filled"):
                    stateType = OrderStateType.Done;
                    break;
                case ("partial-filled"):
                    stateType = OrderStateType.Partial;
                    break;
                case ("canceled"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("created"):
                    stateType = OrderStateType.Pending;
                    break;
                default:
                    stateType = OrderStateType.None;
                    break;
            }
            return stateType;
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<News> NewsEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(20));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(20));

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                string[] accountData = order.PortfolioNumber.Split('_');

                string source_portfolio = "spot-api";

                if (accountData[1] == "margin")
                {
                    source_portfolio = "margin-api";
                }
                else if (accountData[1] == "super-margin")
                {
                    source_portfolio = "super-margin-api";
                }

                string typeOrder = order.TypeOrder == OrderPriceType.Market ? "market" : "limit";

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("account-id", accountData[2]);
                jsonContent.Add("symbol", order.SecurityNameCode);
                jsonContent.Add("type", order.Side == Side.Buy ? $"buy-{typeOrder}" : $"sell-{typeOrder}");
                jsonContent.Add("amount", order.Volume.ToString().Replace(",", "."));

                if (order.TypeOrder != OrderPriceType.Market)
                {
                    jsonContent.Add("price", order.Price.ToString().Replace(",", "."));
                }

                jsonContent.Add("source", source_portfolio);
                jsonContent.Add("client-order-id", "AAe2ccbd47" + order.NumberUser.ToString());

                string url = _privateUriBuilder.Build("POST", $"/v1/order/orders/place");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    PlaceOrderResponse orderResponse = JsonConvert.DeserializeObject<PlaceOrderResponse>(responseMessage.Content);

                    if (orderResponse.status == "ok")
                    {

                    }
                    else
                    {
                        SendLogMessage($"Order Fail.  {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                        CreateOrderFail(order);
                    }
                }
                else
                {
                    SendLogMessage("Order Fail. Status: " + responseMessage.StatusCode + "  " + order.SecurityNameCode + ", " + responseMessage.Content, LogMessageType.Error);
                    CreateOrderFail(order);
                }

            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            MyOrderEvent?.Invoke(order);
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

            if (order.State == OrderStateType.Cancel)
            {
                return true;
            }

            try
            {
                string url = _privateUriBuilder.Build("POST", $"/v1/order/orders/{order.NumberMarket}/submitcancel");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    PlaceOrderResponse response = JsonConvert.DeserializeObject<PlaceOrderResponse>(responseMessage.Content);

                    if (response.status == "ok")
                    {
                        //order.State = OrderStateType.Cancel;
                        //MyOrderEvent(order);
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"CancelOrder. Http State Code: {responseMessage.Content}", LogMessageType.Error);
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
                        SendLogMessage("Cancel order failed. Status: " + responseMessage.StatusCode + "  " + order.SecurityNameCode + ", " + responseMessage.Content, LogMessageType.Error);
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
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public void GetAllActivOrders()
        {
            List<Order> ordersOpenAll = GetAllActivOrdersArray(100);

            for (int i = 0; i < ordersOpenAll.Count; i++)
            {
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(ordersOpenAll[i]);
                }
            }
        }

        private List<Order> GetAllActivOrdersArray(int maxCountByCategory)
        {
            List<Order> ordersOpenAll = new List<Order>();

            List<Order> orders = new List<Order>();

            GetAllOpenOrders(orders, 100);

            if (orders != null
                && orders.Count > 0)
            {
                ordersOpenAll.AddRange(orders);
            }

            return ordersOpenAll;
        }

        private RateGate _rateGateGetAllActivOrders = new RateGate(1, TimeSpan.FromMilliseconds(45));

        private void GetAllOpenOrders(List<Order> array, int maxCount)
        {
            _rateGateGetAllActivOrders.WaitToProceed();

            try
            {
                string url = _privateUriBuilder.Build("GET", $"/v1/order/openOrders");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                List<Order> orders = new List<Order>();

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<ResponseAllOrders>> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<ResponseAllOrders>>());
                    List<ResponseAllOrders> item = response.data;

                    if (response.status == "ok")
                    {
                        if (item != null && item.Count > 0)
                        {
                            for (int i = 0; i < item.Count; i++)
                            {
                                if (item[i].client_order_id == null
                                    || item[i].client_order_id == "")
                                {
                                    continue;
                                }

                                if (!item[i].source.Contains("api"))
                                {
                                    continue;
                                }

                                Order newOrder = new Order();


                                newOrder.ServerType = ServerType.HTXSpot;
                                newOrder.SecurityNameCode = item[i].symbol;
                                newOrder.State = GetOrderState(item[i].state);
                                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].created_at));
                                newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].created_at));

                                if (newOrder.State == OrderStateType.Cancel)
                                {
                                    newOrder.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].created_at));
                                }

                                if (newOrder.State == OrderStateType.Done)
                                {
                                    newOrder.TimeDone = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].created_at));
                                }

                                try
                                {
                                    string numberFull = item[i].client_order_id;
                                    string numUser = numberFull.Replace("AAe2ccbd47", "");
                                    newOrder.NumberUser = Convert.ToInt32(numUser);
                                }
                                catch
                                {
                                    // ignore
                                }


                                newOrder.NumberMarket = item[i].id.ToString();

                                newOrder.Volume = item[i].amount.ToDecimal();
                                newOrder.Price = item[i].price.ToDecimal();

                                if (item[i].type.Split('-')[1] == "market")
                                {
                                    newOrder.TypeOrder = OrderPriceType.Market;
                                }

                                if (item[i].type.Split('-')[1] == "limit")
                                {
                                    newOrder.TypeOrder = OrderPriceType.Limit;
                                }

                                if (item[i].type.Split('-')[0] == "buy")
                                {
                                    newOrder.Side = Side.Buy;
                                }
                                else
                                {
                                    newOrder.Side = Side.Sell;
                                }

                                string source = "spot";
                                if (item[i].source == "margin-api")
                                {
                                    source = "margin";
                                }

                                if (item[i].source == "super-margin-api")
                                {
                                    source = "super-margin";
                                }

                                newOrder.PortfolioNumber = $"HTX_{source}_{item[i].account_id}_Portfolio";

                                orders.Add(newOrder);
                            }
                        }

                        if (orders.Count > 0)
                        {
                            array.AddRange(orders);

                            if (array.Count > maxCount)
                            {
                                while (array.Count > maxCount)
                                {
                                    array.RemoveAt(array.Count - 1);
                                }
                                return;
                            }
                            else if (array.Count < 50)
                            {
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }

                        return;
                    }
                    else
                    {
                        SendLogMessage($"Get all open orders failed:  {responseMessage.Content}", LogMessageType.Error);
                        return;
                    }
                }
                else
                {
                    SendLogMessage($"Get all open orders request error: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                    return;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return;
            }
        }



        private List<Order> _activeOrdersCash = new List<Order>();
        private List<Order> _historicalOrdersCash = new List<Order>();
        private DateTime _timeOrdersCashCreate;

        public OrderStateType GetOrderStatus(Order order)
        {
            if (_timeOrdersCashCreate.AddSeconds(2) < DateTime.Now)
            {
                // We update order arrays once every two seconds.
                // We are creating a cache for mass requesting statuses on reconnection.
                _historicalOrdersCash = GetHistoricalOrders(0, 100);
                _activeOrdersCash = GetActiveOrders(0, 100);
                _timeOrdersCashCreate = DateTime.Now;
            }

            Order myOrder = null;

            for (int i = 0; _historicalOrdersCash != null && i < _historicalOrdersCash.Count; i++)
            {
                if (_historicalOrdersCash[i].NumberUser == order.NumberUser)
                {
                    myOrder = _historicalOrdersCash[i];
                    break;
                }
            }

            if (myOrder == null)
            {
                for (int i = 0; _activeOrdersCash != null && i < _activeOrdersCash.Count; i++)
                {
                    if (_activeOrdersCash[i].NumberUser == order.NumberUser)
                    {
                        myOrder = _activeOrdersCash[i];
                        break;
                    }
                }
            }

            if (myOrder == null)
            {
                return OrderStateType.None;
            }

            MyOrderEvent?.Invoke(myOrder);

            if (myOrder.State == OrderStateType.Done
                || myOrder.State == OrderStateType.Partial)
            {
                List<MyTrade> tradesBySecurity
                    = GetMyTradesBySecurity(myOrder.NumberMarket);

                if (tradesBySecurity == null)
                {
                    return OrderStateType.None;
                }

                List<MyTrade> tradesByMyOrder = new List<MyTrade>();

                for (int i = 0; i < tradesBySecurity.Count; i++)
                {
                    if (tradesBySecurity[i].NumberOrderParent == myOrder.NumberMarket)
                    {
                        tradesByMyOrder.Add(tradesBySecurity[i]);
                    }
                }

                for (int i = 0; i < tradesByMyOrder.Count; i++)
                {
                    if (MyTradeEvent != null)
                    {
                        MyTradeEvent(tradesByMyOrder[i]);
                    }
                }
            }

            return myOrder.State;
        }

        private Order GetOrderFromExchange(string numberMarket)
        {
            Order newOrder = new Order();

            try
            {
                string url = _privateUriBuilder.Build("GET", $"/v1/order/orders/{numberMarket}");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                ResponseMessageGetOrder response = JsonConvert.DeserializeObject<ResponseMessageGetOrder>(responseMessage.Content);

                ResponseMessageGetOrder.Data item = response.data;

                if (responseMessage.Content.Contains("error"))
                {
                    SendLogMessage($"GetOrderFromExchange. Http State Code: {responseMessage.Content}", LogMessageType.Error);
                }
                else
                {
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.created_at));
                    newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.created_at));
                    newOrder.ServerType = ServerType.HTXSpot;
                    newOrder.SecurityNameCode = item.symbol;

                    if (item.client_order_id != null)
                    {
                        try
                        {
                            string numberFull = item.client_order_id;
                            string numUser = numberFull.Replace("AAe2ccbd47", "");
                            newOrder.NumberUser = Convert.ToInt32(numUser);
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    newOrder.NumberMarket = item.id.ToString();
                    newOrder.State = GetOrderState(item.state);
                    newOrder.Volume = item.amount.ToDecimal();
                    newOrder.Price = item.price.ToDecimal();

                    if (item.type.Split('-')[1] == "market")
                    {
                        newOrder.TypeOrder = OrderPriceType.Market;
                    }
                    if (item.type.Split('-')[1] == "limit")
                    {
                        newOrder.TypeOrder = OrderPriceType.Limit;
                    }

                    if (item.type.Split('-')[0] == "buy")
                    {
                        newOrder.Side = Side.Buy;
                    }
                    else
                    {
                        newOrder.Side = Side.Sell;
                    }

                    string source = "spot";
                    if (item.source == "margin-api")
                    {
                        source = "margin";
                    }
                    if (item.source == "super-margin-api")
                    {
                        source = "super-margin";
                    }

                    newOrder.PortfolioNumber = $"HTX_{source}_{item.account_id}_Portfolio";

                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return newOrder;
        }

        private List<MyTrade> GetMyTradesBySecurity(string orderId)
        {
            try
            {
                string url = _privateUriBuilder.Build("GET", $"/v1/order/orders/{orderId}/matchresults");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<ResponseMyTrades>> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<ResponseMyTrades>>());
                    List<ResponseMyTrades> item = response.data;

                    if (response.status == "ok")
                    {
                        List<MyTrade> osEngineOrders = new List<MyTrade>();

                        if (item != null && item.Count > 0)
                        {
                            for (int i = 0; i < item.Count; i++)
                            {
                                MyTrade newTrade = new MyTrade();
                                newTrade.SecurityNameCode = item[i].symbol;
                                newTrade.NumberTrade = item[i].trade_id;
                                newTrade.NumberOrderParent = item[i].order_id;
                                newTrade.Volume = item[i].filled_amount.ToDecimal();
                                newTrade.Price = item[i].price.ToDecimal();
                                newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item[i].created_at));

                                if (item[i].type.Split('-')[0] == "buy")
                                {
                                    newTrade.Side = Side.Buy;
                                }
                                else
                                {
                                    newTrade.Side = Side.Sell;
                                }
                                osEngineOrders.Add(newTrade);
                            }
                        }
                        return osEngineOrders;
                    }
                    else if (responseMessage.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return null;
                    }
                    else
                    {
                        SendLogMessage("GetMyTradesBySecurity request error. ", LogMessageType.Error);

                        if (responseMessage.Content != null)
                        {
                            SendLogMessage("Fail reasons: "
                          + responseMessage.Content, LogMessageType.Error);
                        }
                    }
                }
                else
                {
                    SendLogMessage("Get my trades error " + responseMessage.StatusCode + "  " + responseMessage.Content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("GetMyTradesBySecurity request error." + exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            int countToMethod = startIndex + count;

            List<Order> result = GetAllActivOrdersArray(countToMethod);

            List<Order> resultExit = new List<Order>();

            if (result != null
                && startIndex < result.Count)
            {
                if (startIndex + count < result.Count)
                {
                    resultExit = result.GetRange(startIndex, count);
                }
                else
                {
                    resultExit = result.GetRange(startIndex, result.Count - startIndex);
                }
            }

            return resultExit;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            int countToMethod = startIndex + count;

            List<Order> result = GetAllHistoricalOrdersArray(countToMethod);

            List<Order> resultExit = new List<Order>();

            if (result != null
                && startIndex < result.Count)
            {
                if (startIndex + count < result.Count)
                {
                    resultExit = result.GetRange(startIndex, count);
                }
                else
                {
                    resultExit = result.GetRange(startIndex, result.Count - startIndex);
                }
            }

            return resultExit;
        }

        private List<Order> GetAllHistoricalOrdersArray(int maxCountByCategory)
        {
            List<Order> ordersOpenAll = new List<Order>();

            List<Order> orders = new List<Order>();

            GetAllHistoricalOrders(orders, 100);

            if (orders != null
                && orders.Count > 0)
            {
                ordersOpenAll.AddRange(orders);
            }

            return ordersOpenAll;
        }


        private RateGate _rateGateHistoricalOrders = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private void GetAllHistoricalOrders(List<Order> array, int maxCount)
        {
            _rateGateHistoricalOrders.WaitToProceed();

            try
            {
                string url = _privateUriBuilder.Build("GET", $"/v1/order/history");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                List<Order> orders = new List<Order>();

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<ResponseAllOrders>> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<ResponseAllOrders>>());
                    List<ResponseAllOrders> item = response.data;

                    if (response.status == "ok")
                    {
                        if (item != null && item.Count > 0)
                        {
                            for (int i = 0; i < item.Count; i++)
                            {
                                if (item[i].client_order_id == null
                                    || item[i].client_order_id == "")
                                {
                                    continue;
                                }

                                if (!item[i].source.Contains("api"))
                                {
                                    continue;
                                }

                                Order newOrder = new Order();


                                newOrder.ServerType = ServerType.HTXSpot;
                                newOrder.SecurityNameCode = item[i].symbol;
                                newOrder.State = GetOrderState(item[i].state);
                                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].created_at));
                                newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].created_at));

                                if (newOrder.State == OrderStateType.Cancel)
                                {
                                    newOrder.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].created_at));
                                }

                                if (newOrder.State == OrderStateType.Done)
                                {
                                    newOrder.TimeDone = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].created_at));
                                }

                                try
                                {
                                    string numberFull = item[i].client_order_id;
                                    string numUser = numberFull.Replace("AAe2ccbd47", "");
                                    newOrder.NumberUser = Convert.ToInt32(numUser);
                                }
                                catch
                                {
                                    // ignore
                                }


                                newOrder.NumberMarket = item[i].id.ToString();
                                newOrder.Volume = item[i].amount.ToDecimal();
                                newOrder.Price = item[i].price.ToDecimal();

                                if (item[i].type.Split('-')[1] == "market")
                                {
                                    newOrder.TypeOrder = OrderPriceType.Market;
                                }

                                if (item[i].type.Split('-')[1] == "limit")
                                {
                                    newOrder.TypeOrder = OrderPriceType.Limit;
                                }

                                if (item[i].type.Split('-')[0] == "buy")
                                {
                                    newOrder.Side = Side.Buy;
                                }
                                else
                                {
                                    newOrder.Side = Side.Sell;
                                }

                                string source = "spot";
                                if (item[i].source == "margin-api")
                                {
                                    source = "margin";
                                }

                                if (item[i].source == "super-margin-api")
                                {
                                    source = "super-margin";
                                }

                                newOrder.PortfolioNumber = $"HTX_{source}_{item[i].account_id}_Portfolio";

                                orders.Add(newOrder);
                            }
                        }

                        if (orders.Count > 0)
                        {
                            array.AddRange(orders);

                            if (array.Count > maxCount)
                            {
                                while (array.Count > maxCount)
                                {
                                    array.RemoveAt(array.Count - 1);
                                }
                                return;
                            }
                            else if (array.Count < 50)
                            {
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }

                        return;
                    }
                    else
                    {
                        SendLogMessage($"Get all open orders failed:  {responseMessage.Content}", LogMessageType.Error);
                        return;
                    }
                }
                else
                {
                    SendLogMessage($"Get all open orders request error: {responseMessage.StatusCode} || {responseMessage.Content}", LogMessageType.Error);
                    return;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return;
            }

        }

        #endregion

        #region 12 Queries

        public static string Decompress(byte[] input)
        {
            using (GZipStream stream = new GZipStream(new System.IO.MemoryStream(input), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];

                using (System.IO.MemoryStream memory = new System.IO.MemoryStream())
                {
                    int count = 0;

                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    } while (count > 0);

                    return Encoding.UTF8.GetString(memory.ToArray());
                }
            }
        }

        public string BuildSign(DateTime utcDateTime)
        {
            string strDateTime = utcDateTime.ToString("s");

            GetRequest request = new GetRequest();
            request.AddParam("accessKey", _accessKey);
            request.AddParam("signatureMethod", "HmacSHA256");
            request.AddParam("signatureVersion", "2.1");
            request.AddParam("timestamp", strDateTime);

            string signature = _signer.Sign("GET", _baseUrl, "/ws/v2", request.BuildParams());

            WebSocketAuthenticationRequestV2 auth = new WebSocketAuthenticationRequestV2();
            auth.@params = new WebSocketAuthenticationRequestV2.Params();
            auth.@params.accessKey = _accessKey;
            auth.@params.signature = signature;
            auth.@params.timestamp = strDateTime;

            return JsonConvert.SerializeObject(auth);
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
}