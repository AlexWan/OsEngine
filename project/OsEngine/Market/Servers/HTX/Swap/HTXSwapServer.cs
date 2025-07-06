﻿using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.HTX.Entity;
using OsEngine.Market.Servers.HTX.Swap.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;


namespace OsEngine.Market.Servers.HTX.Swap
{
    public class HTXSwapServer : AServer
    {
        public HTXSwapServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            HTXSwapServerRealization realization = new HTXSwapServerRealization();
            ServerRealization = realization;

            CreateParameterString("Access Key", "");
            CreateParameterPassword("Secret Key", "");
            CreateParameterEnum("USDT/COIN", "USDT", new List<string>() { "COIN", "USDT" });
            CreateParameterBoolean("Extended Data", false);
        }
    }

    public class HTXSwapServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public HTXSwapServerRealization()
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

            Thread threadExtendedData = new Thread(ThreadExtendedData);
            threadExtendedData.IsBackground = true;
            threadExtendedData.Name = "ThreadHTXSwapExtendedData";
            threadExtendedData.Start();

            Thread threadCheckAliveWebSocket = new Thread(CheckAliveWebSocket);
            threadCheckAliveWebSocket.IsBackground = true;
            threadCheckAliveWebSocket.Name = "CheckAliveWebSocketHTXSwap";
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
                SendLogMessage("Can`t run HTX connector. No keys", LogMessageType.Error);
                return;
            }

            if ("USDT".Equals(((ServerParameterEnum)ServerParameters[2]).Value))
            {
                _pathWsPublic = "/linear-swap-ws";
                _pathRest = "/linear-swap-api";
                _pathWsPrivate = "/linear-swap-notification";
                _pathCandles = "/linear-swap-ex";
                _usdtSwapValue = true;
            }
            else
            {
                _pathWsPublic = "/swap-ws";
                _pathRest = "/swap-api";
                _pathWsPrivate = "/swap-notification";
                _pathCandles = "/swap-ex";
                _usdtSwapValue = false;
            }

            if (((ServerParameterBool)ServerParameters[3]).Value == true)
            {
                _extendedMarketData = true;
            }
            else
            {
                _extendedMarketData = false;
            }

            try
            {
                string url = $"https://{_baseUrl}/api/v1/timestamp";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<string> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<string>());

                    if (response.status == "ok")
                    {
                        _privateUriBuilder = new PrivateUrlBuilder(_accessKey, _secretKey, _baseUrl);
                        _signer = new Signer(_secretKey);

                        _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
                        _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
                        CreatePublicWebSocketConnect();
                        CreatePrivateWebSocketConnect();
                    }
                    else
                    {
                        SendLogMessage($"Connection can be open. HTXSwap. - Code: {response.errcode} - {response.errmsg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Connection can be open. HTXSwap. - Code: {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage("Connection can be open. HTXSwap. Error request", LogMessageType.Error);
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

            _subscribledSecurities.Clear();
            _arrayPrivateChannels.Clear();
            _arrayPublicChannels.Clear();
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
            get { return ServerType.HTXSwap; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _accessKey;

        private string _secretKey;

        private string _baseUrl = "api.hbdm.com";

        private int _limitCandles = 1990;

        private List<string> _arrayPrivateChannels = new List<string>();

        private List<string> _arrayPublicChannels = new List<string>();

        private ConcurrentQueue<string> _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private PrivateUrlBuilder _privateUriBuilder;

        private Signer _signer;

        private string _pathWsPublic;

        private string _pathRest;

        private string _pathWsPrivate;

        private string _pathCandles;

        private bool _usdtSwapValue;

        private bool _extendedMarketData;

        #endregion

        #region 3 Securities

        private List<Security> _listSecurities = new List<Security>();

        private RateGate _rateGateSecurities = new RateGate(240, TimeSpan.FromMilliseconds(3000));

        public void GetSecurities()
        {
            _rateGateSecurities.WaitToProceed();

            try
            {
                string url = $"https://{_baseUrl}{_pathRest}/v1/swap_contract_info";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<SecuritiesInfo>> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<SecuritiesInfo>>());

                    if (response.status == "ok")
                    {
                        for (int i = 0; i < response.data.Count; i++)
                        {
                            SecuritiesInfo item = response.data[i];

                            if (item.contract_status == "1")
                            {
                                Security newSecurity = new Security();

                                newSecurity.Exchange = ServerType.HTXSwap.ToString();
                                newSecurity.Name = item.contract_code;
                                newSecurity.NameFull = item.contract_code;
                                newSecurity.NameClass = item.contract_code.Split('-')[1];
                                newSecurity.NameId = item.contract_code;
                                newSecurity.SecurityType = SecurityType.Futures;
                                newSecurity.DecimalsVolume = item.contract_size.DecimalsCount();
                                newSecurity.Lot = 1;
                                newSecurity.PriceStep = item.price_tick.ToDecimal();
                                newSecurity.Decimals = item.price_tick.DecimalsCount();
                                newSecurity.PriceStepCost = newSecurity.PriceStep;
                                newSecurity.State = SecurityStateType.Activ;
                                newSecurity.MinTradeAmount = item.contract_size.ToDecimal();
                                newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                                newSecurity.VolumeStep = newSecurity.DecimalsVolume.GetValueByDecimals();

                                _listSecurities.Add(newSecurity);
                            }
                        }

                        SecurityEvent(_listSecurities);
                    }
                    else
                    {
                        SendLogMessage($"Securities error. Code: {response.errcode} || msg: {response.errmsg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Securities error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
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

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public List<Portfolio> Portfolios;

        private bool _portfolioIsStarted = false;

        public void GetPortfolios()
        {
            if (Portfolios == null)
            {
                GetNewPortfolio();
            }

            if (_usdtSwapValue)
            {
                CreateQueryPortfolioUsdt(true);
            }
            else
            {
                //CreateQueryPortfolioCoin(true);
            }

            _portfolioIsStarted = true;
        }

        private void ThreadUpdatePortfolio()
        {
            Thread.Sleep(5000);

            while (true)
            {
                try
                {
                    Thread.Sleep(10000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (Portfolios == null)
                    {
                        GetNewPortfolio();
                    }

                    if (_usdtSwapValue)
                    {
                        CreateQueryPortfolioUsdt(false);
                    }
                    else
                    {
                        //CreateQueryPortfolioCoin(false);
                    }
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
            Portfolios = new List<Portfolio>();

            Portfolio portfolioInitial = new Portfolio();
            portfolioInitial.Number = "HTXSwapPortfolio";
            portfolioInitial.ValueBegin = 1;
            portfolioInitial.ValueCurrent = 1;
            portfolioInitial.ValueBlocked = 0;

            Portfolios.Add(portfolioInitial);

            PortfolioEvent(Portfolios);
        }

        private void CreateQueryPortfolioCoin(bool IsUpdateValueBegin)
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                string url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_account_info");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                IRestResponse responseMessage = client.Execute(request);

                string JsonResponse = responseMessage.Content;

                if (!JsonResponse.Contains("error"))
                {
                    UpdatePorfolioCoin(JsonResponse, IsUpdateValueBegin);
                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePorfolioCoin(string json, bool IsUpdateValueBegin)
        {
            ResponseMessagePortfoliosCoin response = JsonConvert.DeserializeObject<ResponseMessagePortfoliosCoin>(json);
            List<ResponseMessagePortfoliosCoin.Data> item = response.data;

            if (Portfolios == null)
            {
                return;
            }

            Portfolio portfolio = Portfolios[0];

            for (int i = 0; i < item.Count; i++)
            {
                if (item[i].margin_balance == "0")
                {
                    continue;
                }
                PositionOnBoard pos = new PositionOnBoard();
                pos.PortfolioName = "HTXSwapPortfolio";
                pos.SecurityNameCode = item[i].symbol.ToString();
                pos.ValueBlocked = Math.Round(item[i].margin_frozen.ToDecimal(), 5);
                pos.ValueCurrent = Math.Round(item[i].margin_balance.ToDecimal(), 5);

                if (IsUpdateValueBegin)
                {
                    pos.ValueBegin = Math.Round(item[i].margin_balance.ToDecimal(), 5);
                }

                portfolio.SetNewPosition(pos);
            }

            PortfolioEvent(Portfolios);
        }

        private void CreateQueryPortfolioUsdt(bool IsUpdateValueBegin)
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                string url = _privateUriBuilder.Build("POST", $"{_pathRest}/v3/unified_account_info");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRest<List<PortfoliosUsdt>> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRest<List<PortfoliosUsdt>>());

                    if (response.code == "200")
                    {
                        List<PortfoliosUsdt> itemPortfolio = response.data;
                        UpdatePorfolioUsdt(itemPortfolio, IsUpdateValueBegin);
                    }
                    else
                    {
                        SendLogMessage($"Portfolio error. Code: {response.code} || msg: {response.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePorfolioUsdt(List<PortfoliosUsdt> itemPortfolio, bool IsUpdateValueBegin)
        {
            if (Portfolios == null)
            {
                return;
            }

            Portfolio portfolio = Portfolios[0];

            decimal positionInUSDT = 0;
            decimal sizeUSDT = 0;
            decimal positionBlocked = 0;

            for (int i = 0; itemPortfolio != null && i < itemPortfolio.Count; i++)
            {
                if (itemPortfolio[i].margin_static == "0")
                {
                    continue;
                }

                if (itemPortfolio[i].margin_asset == "USDT")
                {
                    sizeUSDT = itemPortfolio[i].margin_balance.ToDecimal();
                }
                else if (itemPortfolio[i].margin_asset != "HUSD"
                    || itemPortfolio[i].margin_asset != "HT")
                {
                    positionInUSDT += GetPriceSecurity(itemPortfolio[i].margin_asset.ToLower() + "usdt") * itemPortfolio[i].margin_balance.ToDecimal();
                }

                PositionOnBoard pos = new PositionOnBoard();
                pos.PortfolioName = "HTXSwapPortfolio";
                pos.SecurityNameCode = itemPortfolio[i].margin_asset.ToString();
                pos.ValueBlocked = Math.Round(itemPortfolio[i].margin_frozen.ToDecimal(), 5);
                pos.ValueCurrent = Math.Round(itemPortfolio[i].margin_balance.ToDecimal(), 5);
                positionBlocked += pos.ValueBlocked;

                if (IsUpdateValueBegin)
                {
                    pos.ValueBegin = Math.Round(itemPortfolio[i].margin_balance.ToDecimal(), 5);
                }

                portfolio.SetNewPosition(pos);
            }

            if (IsUpdateValueBegin)
            {
                portfolio.ValueBegin = Math.Round(sizeUSDT + positionInUSDT, 5);
            }

            portfolio.ValueCurrent = Math.Round(sizeUSDT + positionInUSDT, 5);
            portfolio.ValueBlocked = positionBlocked;

            if (portfolio.ValueCurrent == 0)
            {
                portfolio.ValueBegin = 1;
                portfolio.ValueCurrent = 1;
            }

            if (PortfolioEvent != null)
            {
                PortfolioEvent(Portfolios);
            }
        }

        private decimal GetPriceSecurity(string security)
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                string _baseUrl = "api.huobi.pro";

                string url = $"https://{_baseUrl}/market/trade?symbol={security}";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);
                string JsonResponse = responseMessage.Content;

                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    ResponseTrades responseTrade = JsonConvert.DeserializeObject<ResponseTrades>(JsonResponse);

                    if (responseTrade == null)
                    {
                        return 0;
                    }

                    if (responseTrade.tick == null)
                    {
                        return 0;
                    }

                    List<ResponseTrades.Data> item = responseTrade.tick.data;

                    decimal price = item[0].price.ToDecimal();

                    return price;

                }
                else
                {
                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}, {JsonResponse}", LogMessageType.Error);
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

            if (endTime.Hour == 0
                && endTime.Minute == 0
                && endTime.Second == 0)
            {
                endTime = endTime.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
            }

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

            DateTime startTimeData = startTime;
            DateTime endTimeData = startTimeData.AddMinutes(tfTotalMinutes * _limitCandles);

            if (endTimeData > DateTime.UtcNow)
            {
                endTimeData = DateTime.UtcNow;
            }

            do
            {
                long from = TimeManager.GetTimeStampSecondsToDateTime(startTimeData);
                long to = TimeManager.GetTimeStampSecondsToDateTime(endTimeData);

                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

                List<Candle> candles = RequestCandleHistory(security.Name, interval, from, to);

                if (candles == null || candles.Count == 0)
                {
                    break;
                }

                if (allCandles.Count > 0
                    && allCandles[allCandles.Count - 1].TimeStart == candles[0].TimeStart)
                {
                    candles.RemoveAt(0);
                }

                if (candles.Count == 0)
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

                startTimeData = endTimeData;
                endTimeData = startTimeData.AddMinutes(tfTotalMinutes * _limitCandles);

                if (startTimeData >= DateTime.UtcNow)
                {
                    break;
                }

                if (endTimeData > DateTime.UtcNow)
                {
                    endTimeData = DateTime.UtcNow;
                }

            } while (true);

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

        private readonly RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private List<Candle> RequestCandleHistory(string security, string interval, long fromTimeStamp, long toTimeStamp)
        {
            _rgCandleData.WaitToProceed();

            try
            {
                string queryParam = $"contract_code={security}&";
                queryParam += $"period={interval}&";
                queryParam += $"from={fromTimeStamp}&";
                queryParam += $"to={toTimeStamp}";

                string url = $"https://{_baseUrl}{_pathCandles}/market/history/kline?{queryParam}";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<ResponseCandles>> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<ResponseCandles>>());

                    if (response.status == "ok")
                    {
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
                        return candles;
                    }
                    else
                    {
                        SendLogMessage($"Candle History error. Code: {response.errcode} || msg: {response.errmsg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Candle History error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return null;
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
                WebSocket webSocketPublicNew = new WebSocket($"wss://{_baseUrl}{_pathWsPublic}");

                //if (_myProxy != null)
                //{
                //    webSocketPublicNew.SetProxy(_myProxy);
                //}

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += webSocketPublic_OnOpen;
                webSocketPublicNew.OnMessage += webSocketPublic_OnMessage;
                webSocketPublicNew.OnError += webSocketPublic_OnError;
                webSocketPublicNew.OnClose += webSocketPublic_OnClose;
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

                _webSocketPrivate = new WebSocket($"wss://{_baseUrl}{_pathWsPrivate}");
                //_webSocketPrivate.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;
                _webSocketPrivate.OnOpen += webSocketPrivate_OnOpen;
                _webSocketPrivate.OnMessage += webSocketPrivate_OnMessage;
                _webSocketPrivate.OnError += webSocketPrivate_OnError;
                _webSocketPrivate.OnClose += webSocketPrivate_OnClose;
                _webSocketPrivate.Connect();

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

        private string _lockerCheckActivateionSockets = "lockerCheckActivateionSocketsHtxSwap";

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
                    //SetPositionMode();
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
                _webSocketPrivate.Send(authRequest);

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
                        //_webSocketPrivate.Send($"{{\"op\": \"ping\",\"ts\": \"{timestamp}\"}}");
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
                            webSocketPublic.Send($"{{\"ping\": \"{timestamp}\"}}");
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

        #region 9 Security subscrible

        private RateGate _rateGateSubscrible = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscrible.WaitToProceed();
                CreateSubscribleSecurityMessageWebSocket(security);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private List<string> _subscribledSecurities = new List<string>();

        private void CreateSubscribleSecurityMessageWebSocket(Security security)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            if (_webSocketPublic == null)
            {
                return;
            }

            for (int i = 0; i < _subscribledSecurities.Count; i++)
            {
                if (_subscribledSecurities[i].Equals(security.Name))
                {
                    return;
                }
            }

            _subscribledSecurities.Add(security.Name);

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

            string clientId = "";

            if (webSocketPublic != null)
            {
                string topic = $"market.{security.Name}.depth.step0";
                webSocketPublic.Send($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");

                topic = $"market.{security.Name}.trade.detail";
                webSocketPublic.Send($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");

            }

            if (_webSocketPrivate != null)
            {
                if (_extendedMarketData)
                {
                    _webSocketPrivate.Send($"{{\"op\":\"sub\", \"topic\":\"public.{security.Name}.funding_rate\", \"cid\": \"{clientId}\" }}");
                    GetFundingHistory(security.Name);
                }
            }
        }

        private void GetFundingHistory(string securityName)
        {
            try
            {
                string queryParam = $"contract_code={securityName}";

                string url = $"https://{_baseUrl}{_pathRest}/v1/swap_historical_funding_rate?{queryParam}";
                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.GET);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<FundingData> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<FundingData>());

                    if (response.status == "ok")
                    {
                        FundingItemHistory item = response.data.data[0];

                        Funding funding = new Funding();

                        funding.SecurityNameCode = item.contract_code;
                        funding.PreviousFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)item.funding_time.ToDecimal());
                        TimeSpan data = TimeManager.GetDateTimeFromTimeStamp((long)item.funding_time.ToDecimal()) - TimeManager.GetDateTimeFromTimeStamp((long)response.data.data[1].funding_time.ToDecimal());
                        funding.FundingIntervalHours = int.Parse(data.Hours.ToString());

                        FundingUpdateEvent?.Invoke(funding);
                    }
                    else
                    {
                        SendLogMessage($"GetFundingHistory> error. Code: {response.errcode} || msg: {response.errmsg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"GetFundingHistory> error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void SendSubscriblePrivate()
        {
            string clientId = "";
            string channelOrders = "orders.*";
            string channelAccounts = "accounts.*";
            string channelPositions = "positions.*";

            if (_usdtSwapValue)
            {
                channelAccounts = "accounts_unify.USDT";
            }

            _webSocketPrivate.Send($"{{\"op\":\"sub\", \"topic\":\"{channelOrders}\", \"cid\": \"{clientId}\" }}");
            _webSocketPrivate.Send($"{{\"op\":\"sub\", \"topic\":\"{channelAccounts}\", \"cid\": \"{clientId}\" }}");
            _webSocketPrivate.Send($"{{\"op\":\"sub\", \"topic\":\"{channelPositions}\", \"cid\": \"{clientId}\" }}");
        }

        private void CreatePingMessageWebSocketPublic(string message)
        {
            ResponsePingPublic response = JsonConvert.DeserializeObject<ResponsePingPublic>(message);

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
                            webSocketPublic.Send($"{{\"pong\": \"{response.ping}\"}}");
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
                try
                {

                    _webSocketPrivate.Send($"{{\"op\": \"pong\",\"ts\": \"{response.ts}\"}}");
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
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
                            if (_subscribledSecurities != null)
                            {
                                for (int j = 0; j < _subscribledSecurities.Count; j++)
                                {
                                    string securityName = _subscribledSecurities[j];

                                    string topic = $"market.{securityName}.depth.step0";
                                    webSocketPublic.Send($"{{\"action\": \"unsub\",\"ch\": \"{topic}\"}}");

                                    topic = $"market.{securityName}.trade.detail";
                                    webSocketPublic.Send($"{{\"action\": \"unsub\",\"ch\": \"{topic}\"}}");
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
                    string channelOrders = "orders.*";
                    string channelAccounts = "accounts.*";
                    string channelPositions = "positions.*";

                    if (_usdtSwapValue)
                    {
                        channelAccounts = "accounts_unify.USDT";
                    }

                    _webSocketPrivate.Send($"{{\"action\": \"unsub\",\"ch\": \"{channelOrders}\"}}");
                    _webSocketPrivate.Send($"{{\"action\": \"unsub\",\"ch\": \"{channelAccounts}\"}}");
                    _webSocketPrivate.Send($"{{\"action\": \"unsub\",\"ch\": \"{channelPositions}\"}}");

                    if (_extendedMarketData)
                    {
                        _webSocketPrivate.Send($"{{\"action\": \"unsub\",\"ch\": \"public.*.funding_rate\"}}");
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        private List<OpenInterestData> _openInterest = new List<OpenInterestData>();

        private DateTime _timeLast = DateTime.Now;

        private RateGate _rateGateOpenInterest = new RateGate(240, TimeSpan.FromMilliseconds(3000));

        private void ThreadExtendedData()
        {
            while (true)
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                if (!_extendedMarketData)
                {
                    continue;
                }

                if (_subscribledSecurities == null
                    || _subscribledSecurities.Count == 0)
                {
                    continue;
                }

                if (_timeLast.AddSeconds(20) > DateTime.Now)
                {
                    continue;
                }

                GetOpenInterest();
            }
        }

        private void GetOpenInterest()
        {
            _rateGateOpenInterest.WaitToProceed();

            try
            {
                for (int i = 0; i < _subscribledSecurities.Count; i++)
                {
                    string url = $"https://{_baseUrl}{_pathRest}/v1/swap_open_interest?contract_code={_subscribledSecurities[i]}";
                    RestClient client = new RestClient(url);
                    RestRequest request = new RestRequest(Method.GET);
                    IRestResponse responseMessage = client.Execute(request);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        ResponseRestMessage<List<OpenInterestInfo>> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<OpenInterestInfo>>());

                        if (response.status == "ok")
                        {
                            OpenInterestData openInterestData = new OpenInterestData();

                            for (int j = 0; j < response.data.Count; j++)
                            {
                                openInterestData.SecutityName = response.data[j].contract_code;

                                if (response.data[j].amount != null)
                                {
                                    openInterestData.OpenInterestValue = response.data[j].amount;

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

                                SecurityVolumes volume = new SecurityVolumes();

                                volume.SecurityNameCode = response.data[j].contract_code;
                                volume.Volume24h = response.data[j].trade_amount.ToDecimal();
                                volume.Volume24hUSDT = response.data[j].trade_turnover.ToDecimal();
                                volume.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)response.ts.ToDecimal());

                                Volume24hUpdateEvent?.Invoke(volume);
                            }
                        }
                        else
                        {
                            SendLogMessage($"GetOpenInterest> - Code: {response.errcode} - {response.errmsg}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"GetOpenInterest> - Code: {responseMessage.StatusCode} - {responseMessage.Content}", LogMessageType.Error);
                    }
                }

                _timeLast = DateTime.Now;
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

        public event Action<News> NewsEvent;

        #endregion

        #region 10 WebSocket parsing the messages

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

                        if (message.Contains("depth"))
                        {
                            UpdateDepth(message);
                            continue;
                        }

                        if (message.Contains("trade.detail"))
                        {
                            UpdateTrade(message);
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
                            SendSubscriblePrivate();
                            continue;
                        }

                        if (message.Contains("orders."))
                        {
                            UpdateOrder(message);
                            continue;
                        }

                        if (message.Contains("accounts") || message.Contains("accounts_unify"))
                        {
                            UpdatePortfolioFromSubscrible(message);
                            continue;
                        }

                        if (message.Contains("positions"))
                        {
                            UpdatePositionFromSubscrible(message);
                            continue;
                        }

                        if (message.Contains("pong"))
                        {
                            continue;
                        }

                        if (message.Contains("funding_rate"))
                        {
                            UpdateFundingRate(message);
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

        private void UpdateFundingRate(string message)
        {
            try
            {
                ResponseWebSocketMessage<List<FundingItem>> response = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<List<FundingItem>>());

                if (response == null
                    || response.data == null)
                {
                    return;
                }

                for (int i = 0; i < response.data.Count; i++)
                {
                    Funding funding = new Funding();
                    funding.SecurityNameCode = response.data[i].contract_code;
                    funding.CurrentValue = response.data[i].funding_rate.ToDecimal() * 100;
                    funding.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)response.data[i].funding_time.ToDecimal());
                    funding.NextFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)response.data[i].settlement_time.ToDecimal());
                    FundingUpdateEvent?.Invoke(funding);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdateTrade(string message)
        {
            try
            {
                ResponseChannelTrades responseTrade = JsonConvert.DeserializeObject<ResponseChannelTrades>(message);

                if (responseTrade == null)
                {
                    return;
                }

                if (responseTrade.tick == null)
                {
                    return;
                }

                List<ResponseChannelTrades.Data> item = responseTrade.tick.data;

                for (int i = 0; i < item.Count; i++)
                {
                    Trade trade = new Trade();
                    trade.SecurityNameCode = GetSecurityName(responseTrade.ch);
                    trade.Price = item[i].price.ToDecimal();
                    trade.Id = item[i].id;
                    trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item[i].ts));
                    trade.Volume = item[i].amount.ToDecimal();
                    trade.Side = item[i].direction.Equals("buy") ? Side.Buy : Side.Sell;

                    if (_extendedMarketData)
                    {
                        trade.OpenInterest = GetOpenInterestValue(trade.SecurityNameCode);
                    }

                    NewTradesEvent(trade);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
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
            Thread.Sleep(1);

            try
            {
                ResponseChannelBook responseDepth = JsonConvert.DeserializeObject<ResponseChannelBook>(message);

                ResponseChannelBook.Tick item = responseDepth.tick;

                if (item == null)
                {
                    return;
                }

                if (item.asks.Count == 0 && item.bids.Count == 0)
                {
                    return;
                }

                MarketDepth marketDepth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                marketDepth.SecurityNameCode = responseDepth.ch.Split('.')[1];

                if (item.asks.Count > 0)
                {
                    for (int i = 0; i < 25 && i < item.asks.Count; i++)
                    {
                        if (item.asks[i].Count < 2)
                        {
                            continue;
                        }

                        decimal ask = item.asks[i][1].ToString().ToDecimal();
                        decimal price = item.asks[i][0].ToString().ToDecimal();

                        if (ask == 0 ||
                            price == 0)
                        {
                            continue;
                        }

                        MarketDepthLevel level = new MarketDepthLevel();
                        level.Ask = ask;
                        level.Price = price;
                        ascs.Add(level);
                    }
                }

                if (item.bids.Count > 0)
                {
                    for (int i = 0; i < 25 && i < item.bids.Count; i++)
                    {
                        if (item.bids[i].Count < 2)
                        {
                            continue;
                        }

                        decimal bid = item.bids[i][1].ToString().ToDecimal();
                        decimal price = item.bids[i][0].ToString().ToDecimal();

                        if (bid == 0 ||
                            price == 0)
                        {
                            continue;
                        }

                        MarketDepthLevel level = new MarketDepthLevel();
                        level.Bid = bid;
                        level.Price = price;
                        bids.Add(level);
                    }
                }

                if (ascs.Count == 0 ||
                    bids.Count == 0)
                {
                    return;
                }

                marketDepth.Asks = ascs;
                marketDepth.Bids = bids;
                marketDepth.Time
                    = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.ts));

                if (marketDepth.Time < _lastTimeMd)
                {
                    marketDepth.Time = _lastTimeMd;
                }
                else if (marketDepth.Time == _lastTimeMd)
                {
                    _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);
                    marketDepth.Time = _lastTimeMd;
                }

                _lastTimeMd = marketDepth.Time;

                MarketDepthEvent(marketDepth);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private DateTime _lastTimeMd;

        private string GetSecurityName(string ch)
        {
            string[] strings = ch.Split('.');
            return strings[1];
        }

        private void UpdateMyTrade(ResponseChannelUpdateOrder response)
        {
            for (int i = 0; i < response.trade.Count; i++)
            {
                MyTrade myTrade = new MyTrade();

                myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(response.trade[i].created_at));
                myTrade.NumberOrderParent = response.order_id;
                myTrade.NumberTrade = response.trade[i].id;
                myTrade.Price = response.trade[i].trade_price.ToDecimal();
                myTrade.SecurityNameCode = response.contract_code;
                myTrade.Side = response.direction.Equals("buy") ? Side.Buy : Side.Sell;
                myTrade.Volume = /*GetSecurityLot(response.contract_code) **/ response.trade[i].trade_volume.ToDecimal();

                MyTradeEvent(myTrade);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                ResponseChannelUpdateOrder response = JsonConvert.DeserializeObject<ResponseChannelUpdateOrder>(message);

                if (response == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(response.order_id))
                {
                    return;
                }

                Order newOrder = new Order();

                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(response.ts));
                newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(response.created_at));
                newOrder.ServerType = ServerType.HTXFutures;
                newOrder.SecurityNameCode = response.contract_code;
                newOrder.NumberUser = Convert.ToInt32(response.client_order_id);
                newOrder.NumberMarket = response.order_id.ToString();
                newOrder.Side = response.direction.Equals("buy") ? Side.Buy : Side.Sell;
                newOrder.State = GetOrderState(response.status);
                newOrder.Price = response.price.ToDecimal();
                newOrder.PortfolioNumber = $"HTXSwapPortfolio";
                newOrder.PositionConditionType = response.offset == "open" ? OrderPositionConditionType.Open : OrderPositionConditionType.Close;
                newOrder.Volume = /*GetSecurityLot(response.contract_code) **/ response.volume.ToDecimal();

                MyOrderEvent(newOrder);

                if (response.trade != null)
                {
                    UpdateMyTrade(response);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("1"):
                    stateType = OrderStateType.Pending;
                    break;
                case ("2"):
                    stateType = OrderStateType.Pending;
                    break;
                case ("3"):
                    stateType = OrderStateType.Active;
                    break;
                case ("4"):
                    stateType = OrderStateType.Partial;
                    break;
                case ("5"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("6"):
                    stateType = OrderStateType.Done;
                    break;
                case ("7"):
                    stateType = OrderStateType.Cancel;
                    break;
                default:
                    stateType = OrderStateType.None;
                    break;
            }
            return stateType;
        }

        private void UpdatePortfolioFromSubscrible(string message)
        {
            if (Portfolios == null)
            {
                return;
            }

            if (_portfolioIsStarted == false)
            {
                return;
            }

            try
            {
                ResponseWebSocketMessage<List<PortfolioItem>> response = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<List<PortfolioItem>>());

                List<PortfolioItem> item = response.data;

                if (item == null)
                {
                    return;
                }

                Portfolio portfolio = Portfolios[0];

                if (item.Count != 0)
                {
                    for (int i = 0; i < item.Count; i++)
                    {
                        PositionOnBoard pos = new PositionOnBoard();

                        pos.PortfolioName = "HTXSwapPortfolio";
                        pos.SecurityNameCode = _usdtSwapValue ? item[i].margin_asset : item[i].symbol;
                        pos.ValueBlocked = item[i].margin_frozen.ToDecimal();
                        pos.ValueCurrent = Math.Round(item[i].margin_balance.ToDecimal(), 5);

                        if (!_usdtSwapValue)
                        {
                            pos.ValueBegin = Math.Round(item[i].margin_static.ToDecimal(), 5);
                        }

                        portfolio.SetNewPosition(pos);
                    }
                }

                PortfolioEvent(Portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdatePositionFromSubscrible(string message)
        {
            try
            {
                ResponseWebSocketMessage<List<PositionsItem>> response = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<List<PositionsItem>>());

                List<PositionsItem> item = response.data;

                if (item == null)
                {
                    return;
                }

                if (item.Count == 0)
                {
                    return;
                }

                if (Portfolios == null)
                {
                    return;
                }

                Portfolio portfolio = Portfolios[0];

                decimal resultPnL = 0;

                for (int i = 0; i < item.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "HTXSwapPortfolio";

                    if (item[i].direction == "buy")
                    {
                        pos.SecurityNameCode = item[i].contract_code + "_" + "LONG";
                    }
                    else if (item[i].direction == "sell")
                    {
                        pos.SecurityNameCode = item[i].contract_code + "_" + "SHORT";
                    }

                    if (item[i].direction == "buy")
                    {
                        pos.ValueCurrent = /*GetSecurityLot(item[i].contract_code) **/ item[i].volume.ToDecimal();
                    }
                    else if (item[i].direction == "sell")
                    {
                        pos.ValueCurrent = /*GetSecurityLot(item[i].contract_code) **/ (-item[i].volume.ToDecimal());
                    }

                    pos.ValueBlocked = /*GetSecurityLot(item[i].contract_code) * */item[i].frozen.ToDecimal();
                    pos.UnrealizedPnl = Math.Round(item[i].profit_unreal.ToDecimal(), 5);
                    resultPnL += pos.UnrealizedPnl;

                    portfolio.SetNewPosition(pos);
                }

                if (_usdtSwapValue)
                {
                    portfolio.UnrealizedPnl = resultPnL;
                }

                PortfolioEvent(Portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        public event Action<Funding> FundingUpdateEvent;

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("contract_code", order.SecurityNameCode);
                jsonContent.Add("client_order_id", order.NumberUser.ToString());
                jsonContent.Add("price", order.Price.ToString().Replace(",", "."));

                jsonContent.Add("direction", order.Side == Side.Buy ? "buy" : "sell");
                jsonContent.Add("volume", order.Volume /*/ GetSecurityLot(order.SecurityNameCode)*/.ToString().Replace(",", "."));

                if (order.PositionConditionType == OrderPositionConditionType.Close)
                {
                    jsonContent.Add("offset", "close");
                }
                else
                {
                    jsonContent.Add("offset", "open");
                }

                jsonContent.Add("lever_rate", "10");
                jsonContent.Add("order_price_type", "limit");

                string url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_order");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                PlaceOrderResponse orderResponse = JsonConvert.DeserializeObject<PlaceOrderResponse>(responseMessage.Content);

                if (responseMessage.Content.Contains("error"))
                {
                    order.State = OrderStateType.Fail;
                    MyOrderEvent(order);
                    SendLogMessage($"SendOrder. Error: {responseMessage.Content}", LogMessageType.Error);
                }
                else
                {
                    order.NumberMarket = orderResponse.data.order_id;
                    order.State = OrderStateType.Pending;
                    MyOrderEvent(order);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
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
                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("order_id", order.NumberMarket);
                jsonContent.Add("contract_code", order.SecurityNameCode);

                string url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_cancel");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                PlaceOrderResponse response = JsonConvert.DeserializeObject<PlaceOrderResponse>(responseMessage.Content);

                if (response.status != "ok")
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
                else
                {
                    order.State = OrderStateType.Cancel;
                    MyOrderEvent(order);
                    return true;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public void CancelAllOrders()
        {
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange();

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

                orders[i].TimeCreate = orders[i].TimeCallBack;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        private List<Order> GetAllOrdersFromExchange()
        {
            List<Order> orders = new List<Order>();

            try
            {
                string url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_openorders");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                IRestResponse responseMessage = client.Execute(request);

                ResponseMessageAllOrders response = JsonConvert.DeserializeObject<ResponseMessageAllOrders>(responseMessage.Content);

                List<ResponseMessageAllOrders.Orders> item = response.data.orders;

                if (responseMessage.Content.Contains("error"))
                {
                    SendLogMessage($"GetAllOrder. Http State Code: {responseMessage.Content}", LogMessageType.Error);
                }
                else
                {
                    if (item != null && item.Count > 0)
                    {
                        for (int i = 0; i < item.Count; i++)
                        {
                            Order newOrder = new Order();

                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].update_time));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[i].created_at));
                            newOrder.ServerType = ServerType.HTXSwap;
                            newOrder.SecurityNameCode = item[i].contract_code;
                            newOrder.NumberUser = Convert.ToInt32(item[i].client_order_id);
                            newOrder.NumberMarket = item[i].order_id.ToString();
                            newOrder.Side = item[i].direction.Equals("buy") ? Side.Buy : Side.Sell;
                            newOrder.State = GetOrderState(item[i].status);
                            newOrder.Volume = /*GetSecurityLot(item[i].contract_code) **/ item[i].volume.ToDecimal();
                            newOrder.Price = item[i].price.ToDecimal();
                            newOrder.PortfolioNumber = $"HTXSwapPortfolio";
                            newOrder.PositionConditionType = item[i].offset == "open" ? OrderPositionConditionType.Open : OrderPositionConditionType.Close;

                            orders.Add(newOrder);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return orders;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            Order orderFromExchange = GetOrderFromExchange(order.SecurityNameCode, order.NumberMarket);

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
                List<MyTrade> tradesBySecurity
                    = GetMyTradesBySecurity(order.SecurityNameCode, order.NumberMarket, order.TimeCreate);

                if (tradesBySecurity == null)
                {
                    return orderOnMarket.State;
                }

                List<MyTrade> tradesByMyOrder = new List<MyTrade>();

                for (int i = 0; i < tradesBySecurity.Count; i++)
                {
                    if (tradesBySecurity[i].NumberOrderParent == orderOnMarket.NumberMarket)
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

            return orderOnMarket.State;
        }

        private Order GetOrderFromExchange(string securityNameCode, string numberMarket)
        {
            Order newOrder = new Order();

            try
            {
                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("order_id", numberMarket);
                jsonContent.Add("contract_code", securityNameCode);

                string url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_order_info");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                ResponseMessageGetOrder response = JsonConvert.DeserializeObject<ResponseMessageGetOrder>(responseMessage.Content);

                List<ResponseMessageGetOrder.Data> item = response.data;

                if (responseMessage.Content.Contains("error"))
                {
                    SendLogMessage($"GetAllOrder. Http State Code: {responseMessage.Content}", LogMessageType.Error);
                }
                else
                {
                    if (item != null && item.Count > 0)
                    {
                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[0].created_at));
                        newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item[0].created_at));
                        newOrder.ServerType = ServerType.HTXSwap;
                        newOrder.SecurityNameCode = item[0].contract_code;
                        newOrder.NumberUser = Convert.ToInt32(item[0].client_order_id);
                        newOrder.NumberMarket = item[0].order_id.ToString();
                        newOrder.Side = item[0].direction.Equals("buy") ? Side.Buy : Side.Sell;
                        newOrder.State = GetOrderState(item[0].status);
                        newOrder.Volume = /*GetSecurityLot(item[0].contract_code) * */item[0].volume.ToDecimal();
                        newOrder.Price = item[0].price.ToDecimal();
                        newOrder.PortfolioNumber = $"HTXSwapPortfolio";
                        newOrder.PositionConditionType = item[0].offset == "open" ? OrderPositionConditionType.Open : OrderPositionConditionType.Close;
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return newOrder;
        }

        private List<MyTrade> GetMyTradesBySecurity(string security, string orderId, DateTime createdOrderTime)
        {
            try
            {
                Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>();

                jsonContent.Add("contract_code", security.Split('_')[0]);
                jsonContent.Add("order_id", Convert.ToInt64(orderId));
                jsonContent.Add("created_at", TimeManager.GetTimeStampMilliSecondsToDateTime(createdOrderTime));

                string url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_order_detail");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                string respString = responseMessage.Content;

                if (!respString.Contains("error"))
                {
                    ResponseMessageGetMyTradesBySecurity orderResponse = JsonConvert.DeserializeObject<ResponseMessageGetMyTradesBySecurity>(respString);

                    List<MyTrade> osEngineOrders = new List<MyTrade>();

                    if (orderResponse.data.trades != null && orderResponse.data.trades.Count > 0)
                    {
                        for (int i = 0; i < orderResponse.data.trades.Count; i++)
                        {
                            MyTrade newTrade = new MyTrade();
                            newTrade.SecurityNameCode = orderResponse.data.contract_code;
                            newTrade.NumberTrade = orderResponse.data.trades[i].trade_id;
                            newTrade.NumberOrderParent = orderResponse.data.order_id;
                            newTrade.Volume = /*GetSecurityLot(orderResponse.data.contract_code) **/ orderResponse.data.trades[i].trade_volume.ToDecimal();
                            newTrade.Price = orderResponse.data.trades[i].trade_price.ToDecimal();
                            newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(orderResponse.data.trades[i].created_at));

                            if (orderResponse.data.direction == "buy")
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
                else if (responseMessage.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    SendLogMessage("Get all orders request error. ", LogMessageType.Error);

                    if (responseMessage.Content != null)
                    {
                        SendLogMessage("Fail reasons: "
                      + responseMessage.Content, LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
            }
            return null;
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
            request.AddParam("AccessKeyId", _accessKey);
            request.AddParam("SignatureMethod", "HmacSHA256");
            request.AddParam("SignatureVersion", "2");
            request.AddParam("Timestamp", strDateTime);

            string signature = _signer.Sign("GET", _baseUrl, _pathWsPrivate, request.BuildParams());

            WebSocketAuthenticationRequestFutures auth = new WebSocketAuthenticationRequestFutures();
            auth.AccessKeyId = _accessKey;
            auth.Signature = signature;
            auth.Timestamp = strDateTime;

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

    public class OpenInterestData
    {
        public string SecutityName { get; set; }
        public string OpenInterestValue { get; set; }
    }
}