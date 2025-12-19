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

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterEnum("USDT/COIN", "USDT", new List<string>() { "COIN", "USDT" });
            CreateParameterBoolean("Hedge Mode", true);
            CreateParameterEnum("Margin Mode", "Cross", new List<string> { "Cross", "Isolated" });
            CreateParameterString("Leverage", "1");
            CreateParameterBoolean("Extended Data", false);
            ServerParameters[3].ValueChange += HTXSwapServer_ValueChange;

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label254;
            ServerParameters[3].Comment = OsLocalization.Market.Label255;
            ServerParameters[4].Comment = OsLocalization.Market.Label249;
            ServerParameters[5].Comment = OsLocalization.Market.Label256;
            ServerParameters[6].Comment = OsLocalization.Market.Label270;
        }

        private void HTXSwapServer_ValueChange()
        {
            ((HTXSwapServerRealization)ServerRealization).HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;
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

            HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;

            if (((ServerParameterEnum)ServerParameters[4]).Value == "Cross"
                && "USDT".Equals(((ServerParameterEnum)ServerParameters[2]).Value))
            {
                _marginMode = "cross";
            }
            else
            {
                _marginMode = "isolated";
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

            _subscribedSecurities.Clear();
            _securitiesName.Clear();
            _listSecurities = new List<Security>();
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

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _accessKey;

        private string _secretKey;

        private string _baseUrl = "api.hbdm.com";

        private int _limitCandles = 1990;

        private ConcurrentQueue<string> _FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private PrivateUrlBuilder _privateUriBuilder;

        private Signer _signer;

        private string _pathWsPublic;

        private string _pathRest;

        private string _pathWsPrivate;

        private string _pathCandles;

        private bool _usdtSwapValue;

        private string _marginMode = "cross";

        private string _leverage;

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

                _securitiesName.Clear();
                //SetPositionMode();
            }
        }

        private bool _hedgeMode;

        private bool _extendedMarketData;

        private RateGate _rateGatePositionMode = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private List<string> _securitiesName = new List<string>();

        public void SetPositionMode(string nameSecurity)
        {
            _rateGatePositionMode.WaitToProceed();

            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                bool isInArraySecurity = false;

                for (int j = 0; j < _securitiesName.Count; j++)
                {
                    if (nameSecurity == _securitiesName[j])
                    {
                        isInArraySecurity = true;
                        break;
                    }
                }

                if (isInArraySecurity == true)
                {
                    return;
                }

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                if (_marginMode == "isolated")
                {
                    jsonContent.Add("margin_account", nameSecurity);
                }
                else
                {
                    string marginAccount = nameSecurity.Split('-')[1];
                    jsonContent.Add("margin_account", marginAccount);
                }

                if (HedgeMode)
                {
                    jsonContent.Add("position_mode", "dual_side");
                }
                else
                {
                    jsonContent.Add("position_mode", "single_side");
                }

                string url = null;

                if (_marginMode == "isolated")
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_switch_position_mode");
                }
                else
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_cross_switch_position_mode");
                }

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    _securitiesName.Add(nameSecurity);
                }
                else
                {
                    SendLogMessage($"Position Mode error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 3 Securities

        private List<Security> _listSecurities;

        private RateGate _rateGateSecurities = new RateGate(240, TimeSpan.FromMilliseconds(3000));

        public void GetSecurities()
        {
            if (_listSecurities == null)
            {
                _listSecurities = new List<Security>();
            }

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
                                newSecurity.Lot = 1; //item.contract_size.ToDecimal();
                                newSecurity.PriceStep = item.price_tick.ToDecimal();
                                newSecurity.Decimals = item.price_tick.DecimalsCount();
                                newSecurity.PriceStepCost = newSecurity.PriceStep;
                                newSecurity.State = SecurityStateType.Activ;
                                newSecurity.MinTradeAmount = item.contract_size.ToDecimal();
                                newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;
                                newSecurity.VolumeStep = item.contract_size.ToDecimal();

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
                GetAccountInfo();
                CreateQueryPortfolioUsdt(true);
            }
            else
            {
                CreateQueryPortfolioCoin(true);
            }

            _portfolioIsStarted = true;
        }

        private void ThreadUpdatePortfolio()
        {
            Thread.Sleep(30000);

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

                    if (_portfolioIsStarted == false)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_usdtSwapValue)
                    {
                        CreateQueryPortfolioUsdt(false);
                    }
                    else
                    {
                        CreateQueryPortfolioCoin(false);
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

        private void GetAccountInfo()
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                string url = _privateUriBuilder.Build("POST", $"{_pathRest}/v3/swap_switch_account_type");

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();
                jsonContent.Add("account_type", "2");

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRest<object> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRest<object>());

                    if (response.code != "200")
                    {
                        if (response.msg.Contains("Incorrect Access key [Access key错误]")
                            || response.msg.Contains("Verification failure [校验失败]"))
                        {
                            Disconnect();
                        }

                        SendLogMessage($"AccountInfo error. Code: {response.code} || msg: {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    if (responseMessage.Content.Contains("Incorrect Access key [Access key"))
                    {
                        Disconnect();
                    }

                    SendLogMessage($"AccountInfo error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
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

                if (!responseMessage.Content.Contains("error"))
                {
                    //UpdatePorfolioCoin(JsonResponse, IsUpdateValueBegin);
                }
                else
                {
                    if (responseMessage.Content.Contains("Incorrect Access key [Access key错误]")
                            || responseMessage.Content.Contains("Verification failure [校验失败]"))
                    {
                        Disconnect();
                    }

                    SendLogMessage($"Http State Code: {responseMessage.StatusCode}, {responseMessage.Content}", LogMessageType.Error);
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
                        if (response.msg.Contains("Incorrect Access key [Access key错误]")
                            || response.msg.Contains("Verification failure [校验失败]"))
                        {
                            Disconnect();
                        }

                        SendLogMessage($"Portfolio error. Code: {response.code} || msg: {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    if (responseMessage.Content.Contains("Incorrect Access key [Access key"))
                    {
                        Disconnect();
                    }

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
                positionBlocked += Math.Round(itemPortfolio[i].margin_frozen.ToDecimal(), 5);

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
                        SendLogMessage($"Candle History error. {response.errcode} || msg: {response.errmsg}", LogMessageType.Error);
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

                _webSocketPrivate = new WebSocket($"wss://{_baseUrl}{_pathWsPrivate}");
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

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(200));

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

            //SetPositionMode();

            if (_webSocketPublic.Count == 0)
            {
                return;
            }

            WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

            if (webSocketPublic.ReadyState == WebSocketState.Open
                && _subscribedSecurities.Count != 0
                && _subscribedSecurities.Count % 50 == 0)
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
                webSocketPublic.SendAsync($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");

                topic = $"market.{security.Name}.trade.detail";
                webSocketPublic.SendAsync($"{{ \"sub\": \"{topic}\",\"id\": \"{clientId}\" }}");

            }

            if (_webSocketPrivate != null)
            {
                if (_extendedMarketData)
                {
                    _webSocketPrivate.SendAsync($"{{\"op\":\"sub\", \"topic\":\"public.{security.Name}.funding_rate\", \"cid\": \"{clientId}\" }}");
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

        private void SendSubscribePrivate()
        {
            string clientId = "";
            string channelOrders = "orders.*";
            string channelAccounts = "accounts.*";
            string channelPositions = "positions.*";

            if (_marginMode == "cross")
            {
                channelOrders = "orders_cross.*";
                channelAccounts = "accounts_cross.*";
                channelPositions = "positions_cross.*";
            }

            if (_usdtSwapValue)
            {
                channelAccounts = "accounts_unify.USDT";
            }

            _webSocketPrivate.SendAsync($"{{\"op\":\"sub\", \"topic\":\"{channelOrders}\", \"cid\": \"{clientId}\" }}");
            _webSocketPrivate.SendAsync($"{{\"op\":\"sub\", \"topic\":\"{channelAccounts}\", \"cid\": \"{clientId}\" }}");
            _webSocketPrivate.SendAsync($"{{\"op\":\"sub\", \"topic\":\"{channelPositions}\", \"cid\": \"{clientId}\" }}");
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
                try
                {

                    _webSocketPrivate.SendAsync($"{{\"op\": \"pong\",\"ts\": \"{response.ts}\"}}");
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
                            if (_subscribedSecurities != null)
                            {
                                for (int j = 0; j < _subscribedSecurities.Count; j++)
                                {
                                    string securityName = _subscribedSecurities[j];

                                    string topic = $"market.{securityName}.depth.step0";
                                    webSocketPublic.SendAsync($"{{\"action\": \"unsub\",\"ch\": \"{topic}\"}}");

                                    topic = $"market.{securityName}.trade.detail";
                                    webSocketPublic.SendAsync($"{{\"action\": \"unsub\",\"ch\": \"{topic}\"}}");
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

                    if (_marginMode == "cross")
                    {
                        channelOrders = "orders_cross.*";
                        channelAccounts = "accounts_cross.*";
                        channelPositions = "positions_cross.*";
                    }

                    if (_usdtSwapValue)
                    {
                        channelAccounts = "accounts_unify.USDT";
                    }

                    _webSocketPrivate.SendAsync($"{{\"action\": \"unsub\",\"ch\": \"{channelOrders}\"}}");
                    _webSocketPrivate.SendAsync($"{{\"action\": \"unsub\",\"ch\": \"{channelAccounts}\"}}");
                    _webSocketPrivate.SendAsync($"{{\"action\": \"unsub\",\"ch\": \"{channelPositions}\"}}");

                    if (_extendedMarketData)
                    {
                        _webSocketPrivate.SendAsync($"{{\"action\": \"unsub\",\"ch\": \"public.*.funding_rate\"}}");
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        private List<OpenInterestData> _openInterest = new List<OpenInterestData>();

        private DateTime _timeLastUpdateExtendedData = DateTime.Now;

        private RateGate _rateGateOpenInterest = new RateGate(240, TimeSpan.FromMilliseconds(3000));

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
                            GetOpenInterest();
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

        private void GetOpenInterest()
        {
            _rateGateOpenInterest.WaitToProceed();

            try
            {
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    string url = $"https://{_baseUrl}{_pathRest}/v1/swap_open_interest?contract_code={_subscribedSecurities[i]}";
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
                            SendSubscribePrivate();
                            continue;
                        }

                        if (message.Contains("orders.")
                            || message.Contains("orders_cross."))
                        {
                            UpdateOrder(message);
                            continue;
                        }

                        if (message.Contains("accounts")
                            || message.Contains("accounts_unify"))
                        {
                            UpdatePortfolioFromSubscribe(message);
                            continue;
                        }

                        if (message.Contains("positions"))
                        {
                            UpdatePositionFromSubscribe(message);
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

                        double bid = item.bids[i][1].ToString().ToDouble();
                        double price = item.bids[i][0].ToString().ToDouble();

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
                marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.ts));

                if (_lastTimeMd != DateTime.MinValue &&
                    _lastTimeMd >= marketDepth.Time)
                {
                    marketDepth.Time = _lastTimeMd.AddTicks(1);
                }

                //if (marketDepth.Time < _lastTimeMd)
                //{
                //    marketDepth.Time = _lastTimeMd;
                //}
                //else if (marketDepth.Time == _lastTimeMd)
                //{
                //    _lastTimeMd = DateTime.FromBinary(_lastTimeMd.Ticks + 1);
                //    marketDepth.Time = _lastTimeMd;
                //}

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

                string num = response.trade[i].id;
                //if (num.Split('-').Length < 2)
                //{
                //    continue;
                //}

                myTrade.NumberTrade = num;
                myTrade.Price = response.trade[i].trade_price.ToDecimal();
                myTrade.SecurityNameCode = response.contract_code;
                myTrade.Side = response.direction.Equals("buy") ? Side.Buy : Side.Sell;
                myTrade.Volume = response.trade[i].trade_volume.ToDecimal() * GetVolume(myTrade.SecurityNameCode);

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

                try
                {
                    newOrder.NumberUser = Convert.ToInt32(response.client_order_id);
                }
                catch
                {
                }

                newOrder.NumberMarket = response.order_id.ToString();
                newOrder.Side = response.direction.Equals("buy") ? Side.Buy : Side.Sell;
                newOrder.State = GetOrderState(response.status);
                newOrder.Price = response.price.ToDecimal();

                if (response.order_price_type == "limit")
                {
                    newOrder.TypeOrder = OrderPriceType.Limit;
                }
                else if (response.order_price_type == "market")
                {
                    newOrder.TypeOrder = OrderPriceType.Market;
                }

                newOrder.PortfolioNumber = $"HTXSwapPortfolio";
                //newOrder.PositionConditionType = response.offset == "open" ? OrderPositionConditionType.Open : OrderPositionConditionType.Close;
                newOrder.Volume = response.volume.ToDecimal() * GetVolume(newOrder.SecurityNameCode);

                MyOrderEvent(newOrder);

                if (response.trade != null
                    && (newOrder.State == OrderStateType.Done
                    || newOrder.State == OrderStateType.Partial))
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

        private void UpdatePortfolioFromSubscribe(string message)
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

                        decimal frozen = item[i].margin_balance.ToDecimal() - item[i].withdraw_available.ToDecimal();
                        pos.ValueBlocked = Math.Round(frozen, 5);
                        pos.ValueCurrent = Math.Round(item[i].margin_balance.ToDecimal(), 5);

                        if (!_usdtSwapValue)
                        {
                            pos.ValueBegin = Math.Round(item[i].margin_static.ToDecimal(), 5);
                        }

                        portfolio.SetNewPosition(pos);
                    }
                }

                if (!HedgeMode
                    && "USDT".Equals(((ServerParameterEnum)ServerParameters[2]).Value))
                {
                    List<PositionOnBoard> positionInPortfolio = portfolio.GetPositionOnBoard();

                    for (int j = 0; j < positionInPortfolio.Count; j++)
                    {
                        if (positionInPortfolio[j].SecurityNameCode == "USDT")
                        {
                            continue;
                        }

                        bool isInArray = false;

                        for (int i = 0; i < item[0].isolated_swap.Count; i++)
                        {
                            string curNameSec = item[0].isolated_swap[i].contract_code;

                            if (curNameSec == positionInPortfolio[j].SecurityNameCode)
                            {
                                isInArray = true;
                                break;
                            }
                        }

                        if (isInArray == false)
                        {
                            positionInPortfolio[j].ValueCurrent = 0;
                        }
                    }
                }

                PortfolioEvent(Portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdatePositionFromSubscribe(string message)
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

                // decimal resultPnL = 0;

                for (int i = 0; i < item.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "HTXSwapPortfolio";

                    if (item[i].position_mode == "dual_side")
                    {
                        if (item[i].direction == "buy")
                        {
                            pos.SecurityNameCode = item[i].contract_code + "_" + "LONG";
                        }
                        else if (item[i].direction == "sell")
                        {
                            pos.SecurityNameCode = item[i].contract_code + "_" + "SHORT";
                        }
                    }
                    else
                    {
                        pos.SecurityNameCode = item[i].contract_code;
                    }

                    if (item[i].direction == "buy")
                    {
                        pos.ValueCurrent = item[i].volume.ToDecimal() * GetVolume(item[i].contract_code);
                    }
                    else if (item[i].direction == "sell")
                    {
                        pos.ValueCurrent = -item[i].volume.ToDecimal() * GetVolume(item[i].contract_code);
                    }

                    //pos.ValueBlocked = item[i].frozen.ToDecimal();
                    pos.UnrealizedPnl = Math.Round(item[i].profit_unreal.ToDecimal(), 5);
                    //resultPnL += pos.UnrealizedPnl;

                    portfolio.SetNewPosition(pos);
                }

                //if (_usdtSwapValue)
                //{
                //    portfolio.UnrealizedPnl = resultPnL;
                //}

                List<PositionOnBoard> positionInPortfolio = portfolio.GetPositionOnBoard();

                for (int j = 0; j < positionInPortfolio.Count; j++)
                {
                    if (positionInPortfolio[j].SecurityNameCode == "USDT")
                    {
                        continue;
                    }

                    bool isInArray = false;

                    for (int i = 0; i < item.Count; i++)
                    {
                        PositionsItem item2 = item[i];

                        string curNameSec = item2.contract_code;

                        if (item2.position_mode == "dual_side")
                        {
                            if (item[i].direction == "buy")
                            {
                                curNameSec = item2.contract_code + "_" + "LONG";
                            }
                            else if (item[i].direction == "sell")
                            {
                                curNameSec = item2.contract_code + "_" + "SHORT";
                            }
                        }
                        else
                        {
                            curNameSec = item2.contract_code;
                        }

                        if (curNameSec == positionInPortfolio[j].SecurityNameCode)
                        {
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        positionInPortfolio[j].ValueCurrent = 0;
                    }
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

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

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
                if ("USDT".Equals(((ServerParameterEnum)ServerParameters[2]).Value))
                {
                    SetPositionMode(order.SecurityNameCode);
                }

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("contract_code", order.SecurityNameCode);
                jsonContent.Add("client_order_id", order.NumberUser.ToString());
                jsonContent.Add("direction", order.Side == Side.Buy ? "buy" : "sell");

                decimal volume = order.Volume / GetVolume(order.SecurityNameCode);
                jsonContent.Add("volume", volume.ToString("0.#####").Replace(",", "."));

                if (HedgeMode
                    || "COIN".Equals(((ServerParameterEnum)ServerParameters[2]).Value))
                {
                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        jsonContent.Add("offset", "close");
                    }
                    else
                    {
                        jsonContent.Add("offset", "open");
                    }
                }

                jsonContent.Add("lever_rate", _leverage);

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    jsonContent.Add("order_price_type", "limit");
                    jsonContent.Add("price", order.Price.ToString().Replace(",", "."));
                }
                else if (order.TypeOrder == OrderPriceType.Market)
                {
                    if ("COIN".Equals(((ServerParameterEnum)ServerParameters[2]).Value))
                    {
                        jsonContent.Add("order_price_type", "limit");
                        jsonContent.Add("price", order.Price.ToString().Replace(",", "."));

                        if (order.Price == 0)
                        {
                            return;
                        }
                    }
                    else
                    {
                        jsonContent.Add("order_price_type", "market");
                    }
                }

                jsonContent.Add("channel_code", "AAe2ccbd47");

                string url = null;

                if (_marginMode == "isolated")
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_order");
                }
                else
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_cross_order");
                }

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<PlaceOrderResponse> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<PlaceOrderResponse>());

                    if (response.status == "ok")
                    {
                        //order.NumberMarket = response.data.order_id;
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

        private decimal GetVolume(string securityName)
        {
            if (_listSecurities == null)
            {
                return 1;
            }

            decimal minVolume = 1;

            for (int i = 0; i < _listSecurities.Count; i++)
            {
                if (_listSecurities[i].Name == securityName)
                {
                    minVolume = _listSecurities[i].MinTradeAmount;
                }
            }

            if (minVolume <= 0)
            {
                return 1;
            }

            return minVolume;
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            MyOrderEvent?.Invoke(order);
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
        }

        public bool CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            try
            {
                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("order_id", order.NumberMarket);
                jsonContent.Add("contract_code", order.SecurityNameCode);

                string url = null;

                if (_marginMode == "isolated")
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_cancel");
                }
                else
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_cross_cancel");
                }

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<PlaceOrderResponse> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<PlaceOrderResponse>());

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
                            SendLogMessage($"Cancel order failed: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
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

        public void CancelAllOrders()
        {
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            if ("COIN".Equals(((ServerParameterEnum)ServerParameters[2]).Value))
            {
                return;
            }

            _rateGateCancelOrder.WaitToProceed();

            try
            {
                Dictionary<string, string> jsonContent = new Dictionary<string, string>();
                jsonContent.Add("contract_code", security.Name);

                string url = null;

                if (_marginMode == "isolated")
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_cancelall");
                }
                else
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_cross_cancelall");
                }

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllActivOrdersArray(100, true);

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

        private List<Order> GetAllActivOrdersArray(int maxCountByCategory, bool onlyActive)
        {
            List<Order> ordersOpenAll = new List<Order>();

            List<Order> orders = new List<Order>();

            GetAllOpenOrders(orders, 1, 100, true);

            if (orders != null
                && orders.Count > 0)
            {
                ordersOpenAll.AddRange(orders);
            }

            return ordersOpenAll;
        }

        private void GetAllOpenOrders(List<Order> array, int pageIndex, int maxCount, bool onlyActive)
        {
            _rateGateSendOrder.WaitToProceed();

            List<Order> orders = new List<Order>();

            try
            {
                Dictionary<string, string> jsonContent = new Dictionary<string, string>();
                jsonContent.Add("page_index", pageIndex.ToString());
                jsonContent.Add("page_size", "20");

                string url = null;

                if (_marginMode == "isolated")
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_openorders");
                }
                else
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_cross_openorders");
                }

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<ResponseAllOrders> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<ResponseAllOrders>());

                    if (response.status == "ok")
                    {
                        for (int i = 0; i < response.data.orders.Count; i++)
                        {
                            OrdersItem item = response.data.orders[i];

                            Order newOrder = new Order();

                            newOrder.ServerType = ServerType.HTXSwap;
                            newOrder.SecurityNameCode = item.contract_code;
                            newOrder.State = GetOrderState(item.status);
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.update_time));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.created_at));

                            if (newOrder.State == OrderStateType.Cancel)
                            {
                                newOrder.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.update_time));
                            }

                            if (newOrder.State == OrderStateType.Done)
                            {
                                newOrder.TimeDone = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.update_time));
                            }

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(item.client_order_id);
                            }
                            catch
                            {

                            }

                            newOrder.NumberMarket = item.order_id.ToString();
                            newOrder.Side = item.direction.Equals("buy") ? Side.Buy : Side.Sell;
                            newOrder.Volume = item.volume.ToDecimal() * GetVolume(newOrder.SecurityNameCode);
                            newOrder.Price = item.price.ToDecimal();
                            newOrder.PortfolioNumber = $"HTXSwapPortfolio";

                            if (item.order_price_type == "limit")
                            {
                                newOrder.TypeOrder = OrderPriceType.Limit;
                            }
                            else if (item.order_price_type == "market")
                            {
                                newOrder.TypeOrder = OrderPriceType.Market;
                            }

                            //newOrder.PositionConditionType = item.offset == "open" ? OrderPositionConditionType.Open : OrderPositionConditionType.Close;

                            orders.Add(newOrder);
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
                            else if (array.Count < 20)
                            {
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }

                        if (response.data.orders.Count > 1)
                        {
                            int totalPage = Convert.ToInt32(response.data.total_page);
                            int currentPage = Convert.ToInt32(response.data.current_page);
                            //int totalSize = Convert.ToInt32(response.data.total_size);

                            while (currentPage < totalPage)
                            {
                                currentPage++;
                                GetAllOpenOrders(array, currentPage, maxCount, onlyActive);
                            }
                        }

                        return;
                    }
                    else
                    {
                        SendLogMessage($"Get all open orders failed: {response.errcode} || msg: {response.errmsg}", LogMessageType.Error);
                        return;
                    }
                }
                else
                {
                    SendLogMessage("Get all open orders request error " + responseMessage.StatusCode + "  " + responseMessage.Content, LogMessageType.Error);
                    return;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return;
            }
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
                List<MyTrade> tradesBySecurity
                    = GetMyTradesBySecurity(orderOnMarket.SecurityNameCode, orderOnMarket.NumberMarket);

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

        private Order GetOrderFromExchange(string securityNameCode, string numberMarket, string numberUser)
        {
            _rateGateSendOrder.WaitToProceed();

            Order newOrder = new Order();

            try
            {
                Dictionary<string, string> jsonContent = new Dictionary<string, string>();
                jsonContent.Add("contract_code", securityNameCode);

                if (numberMarket != "")
                {
                    jsonContent.Add("order_id", numberMarket);
                }
                else
                {
                    jsonContent.Add("client_order_id", numberUser);
                }

                string url = null;

                if (_marginMode == "isolated")
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_order_info");
                }
                else
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_cross_order_info");
                }

                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<ResponseGetOrder>> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<ResponseGetOrder>>());

                    if (response.status == "ok")
                    {
                        ResponseGetOrder item = response.data[0];

                        if (item != null)
                        {
                            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.created_at));
                            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(long.Parse(item.created_at));
                            newOrder.ServerType = ServerType.HTXSwap;
                            newOrder.SecurityNameCode = item.contract_code;

                            try
                            {
                                newOrder.NumberUser = Convert.ToInt32(item.client_order_id);
                            }
                            catch
                            {
                            }

                            newOrder.NumberMarket = item.order_id.ToString();
                            newOrder.Side = item.direction.Equals("buy") ? Side.Buy : Side.Sell;
                            newOrder.State = GetOrderState(item.status);
                            newOrder.Volume = item.volume.ToDecimal() * GetVolume(newOrder.SecurityNameCode);
                            newOrder.Price = item.price.ToDecimal();
                            newOrder.PortfolioNumber = $"HTXSwapPortfolio";

                            if (item.order_price_type == "limit")
                            {
                                newOrder.TypeOrder = OrderPriceType.Limit;
                            }
                            else if (item.order_price_type == "market")
                            {
                                newOrder.TypeOrder = OrderPriceType.Market;
                            }

                            //newOrder.PositionConditionType = item.offset == "open" ? OrderPositionConditionType.Open : OrderPositionConditionType.Close;
                        }

                        return newOrder;
                    }
                    else
                    {
                        SendLogMessage($"Get order from exchange failed: {responseMessage.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage("Get order from exchange request error " + responseMessage.StatusCode + "  " + responseMessage.Content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private List<MyTrade> GetMyTradesBySecurity(string security, string orderId)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("contract_code", security.Split('_')[0]);
                jsonContent.Add("order_id", orderId);
                //jsonContent.Add("created_at", TimeManager.GetTimeStampMilliSecondsToDateTime(createdOrderTime));

                string url = null;

                if (_marginMode == "isolated")
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_order_detail");
                }
                else
                {
                    url = _privateUriBuilder.Build("POST", $"{_pathRest}/v1/swap_cross_order_detail");
                }


                RestClient client = new RestClient(url);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("application/json", JsonConvert.SerializeObject(jsonContent), ParameterType.RequestBody);
                IRestResponse responseMessage = client.Execute(request);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<ResponseMyTradesBySecurity> response = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<ResponseMyTradesBySecurity>());

                    if (response.status == "ok")
                    {
                        List<MyTrade> osEngineOrders = new List<MyTrade>();

                        if (response.data.trades != null && response.data.trades.Count > 0)
                        {
                            for (int i = 0; i < response.data.trades.Count; i++)
                            {
                                MyTrade newTrade = new MyTrade();
                                newTrade.SecurityNameCode = response.data.contract_code;

                                string num = response.data.trades[i].id;
                                //if(num.Split('-').Length < 2)
                                //{
                                //    continue;
                                //}

                                newTrade.NumberTrade = num;
                                newTrade.NumberOrderParent = response.data.order_id;
                                newTrade.Volume = response.data.trades[i].trade_volume.ToDecimal() * GetVolume(newTrade.SecurityNameCode);
                                newTrade.Price = response.data.trades[i].trade_price.ToDecimal();
                                newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(response.data.trades[i].created_at));

                                if (response.data.direction == "buy")
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
                        SendLogMessage("Get my trades request error. ", LogMessageType.Error);

                        if (responseMessage.Content != null)
                        {
                            SendLogMessage("Fail reasons: "
                          + responseMessage.Content, LogMessageType.Error);
                        }
                    }
                }
                else
                {
                    SendLogMessage("Get my trades by security error " + responseMessage.StatusCode + "  " + responseMessage.Content, LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get my trades by security request error." + exception.ToString(), LogMessageType.Error);
            }
            return null;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            int countToMethod = startIndex + count;

            List<Order> result = GetAllActivOrdersArray(countToMethod, true);

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

        public void SetLeverage(Security security, decimal leverage) { }

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