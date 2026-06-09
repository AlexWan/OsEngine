/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitGetUnified.Entity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;



namespace OsEngine.Market.Servers.BitGetUnified
{
    public class BitGetUnifiedServer : AServer
    {
        public BitGetUnifiedServer()
        {
            BitGetUnifiedServerRealization realization = new BitGetUnifiedServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterPassphrase, "");
            CreateParameterBoolean("Hedge Mode", true);
            CreateParameterBoolean("Extended Data", false);

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label271;
            ServerParameters[3].Comment = OsLocalization.Market.Label250;
            ServerParameters[4].Comment = OsLocalization.Market.Label270;
        }
    }

    public class BitGetUnifiedServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BitGetUnifiedServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.Name = "MessageReaderPublic";
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.Name = "MessageReaderPrivate";
            threadMessageReaderPrivate.Start();

            Thread threadCheckingConnect = new Thread(CheckAliveWebSocket);
            threadCheckingConnect.Name = "CheckAliveWebSocket";
            threadCheckingConnect.Start();

            Thread threadExtendedData = new Thread(ThreadExtendedData);
            threadExtendedData.Name = "ThreadBitGetUnifiedExtendedData";
            threadExtendedData.Start();

            Thread threadPortfilio = new Thread(PortfolioUpdater);
            threadPortfilio.Name = "BitgetUnified_PortfolioUpdater";
            threadPortfilio.Start();

            Thread threadMessageReaderMarketDepthSpot = new Thread(ThreadMessageReaderMarketDepthSpot);
            threadMessageReaderMarketDepthSpot.Name = "ThreadOkxMessageReaderMarketDepthSpot";
            threadMessageReaderMarketDepthSpot.Start();

            Thread threadMessageReaderMarketDepthFuturesUsdt = new Thread(ThreadMessageReaderMarketDepthFuturesUsdt);
            threadMessageReaderMarketDepthFuturesUsdt.Name = "ThreadBitGetUnifiedMessageReaderMarketDepthFuturesUsdt";
            threadMessageReaderMarketDepthFuturesUsdt.Start();

            Thread threadMessageReaderMarketDepthFuturesUsdc = new Thread(ThreadMessageReaderMarketDepthFuturesUsdc);
            threadMessageReaderMarketDepthFuturesUsdc.Name = "ThreadBitGetUnifiedMessageReaderMarketDepthFuturesUsdc";
            threadMessageReaderMarketDepthFuturesUsdc.Start();

            Thread threadMessageReaderMarketDepthFuturesCoin = new Thread(ThreadMessageReaderMarketDepthFuturesCoin);
            threadMessageReaderMarketDepthFuturesCoin.Name = "ThreadBitGetUnifiedMessageReaderMarketDepthFuturesCoin";
            threadMessageReaderMarketDepthFuturesCoin.Start();

            Thread threadMessageReaderTradesSpot = new Thread(ThreadMessageReaderTradesSpot);
            threadMessageReaderTradesSpot.Name = "ThreadBitGetUnifiedMessageReaderTradesSpot";
            threadMessageReaderTradesSpot.Start();

            Thread threadMessageReaderTradesFuturesUsdt = new Thread(ThreadMessageReaderTradesFuturesUsdt);
            threadMessageReaderTradesFuturesUsdt.Name = "ThreadBitGetUnifiedMessageReaderTradesFuturesUsdt";
            threadMessageReaderTradesFuturesUsdt.Start();

            Thread threadMessageReaderTradesFuturesUsdc = new Thread(ThreadMessageReaderTradesFuturesUsdc);
            threadMessageReaderTradesFuturesUsdc.Name = "ThreadBitGetUnifiedMessageReaderTradesFuturesUsdc";
            threadMessageReaderTradesFuturesUsdc.Start();

            Thread threadMessageReaderTradesFuturesCoin = new Thread(ThreadMessageReaderTradesFuturesCoin);
            threadMessageReaderTradesFuturesCoin.Name = "ThreadBitGetUnifiedMessageReaderTradesFuturesCoin";
            threadMessageReaderTradesFuturesCoin.Start();

        }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy = null)
        {
            _myProxy = proxy;

            _publicKey = ((ServerParameterString)ServerParameters[0]).Value;
            _seckretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            _passphrase = ((ServerParameterPassword)ServerParameters[2]).Value;
            HedgeMode = ((ServerParameterBool)ServerParameters[3]).Value;


            if (string.IsNullOrEmpty(_publicKey) ||
                string.IsNullOrEmpty(_seckretKey) ||
                string.IsNullOrEmpty(_passphrase))
            {
                SendLogMessage("Can`t run Bitget Unified connector. No keys or passphrase", LogMessageType.Error);
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
                string requestStr = "/api/v3/account/settings";

                IRestResponse response = CreatePrivateQuery(requestStr, Method.GET, string.Empty, string.Empty);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<UnifiedAccountInfo> accountInfo
                       = JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<UnifiedAccountInfo>());

                    if (accountInfo.code == "00000" && accountInfo.data != null)
                    {
                        UnifiedAccountInfo info = accountInfo.data;
                        SendLogMessage($"Account info recived: uid: {info.uid}, accountMode: {info.accountMode}, assetMode: {info.assetMode}, " +
                            $"holdMode: {info.holdMode}, accountLevel: {info.accountLevel}", LogMessageType.Connect);
                    }

                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
                    _lastConnectionStartTime = DateTime.Now;
                }
                else
                {
                    SendLogMessage($"Connection can be open. BitGet Unified. Error request: {response.Content}", LogMessageType.Error);
                    Disconnect();
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                SendLogMessage("Connection can be open. BitGet. Error request", LogMessageType.Error);
                Disconnect();
            }
        }

        public void Dispose()
        {
            try
            {
                UnsubscribeFromAllWebSockets();
                _subscribedSecutiries.Clear();
                DeleteWebSocketConnection();
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            _fIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            _fIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
            _queueMessageMarketDepthSpot = new ConcurrentQueue<string>();
            _queueMessageMarketDepthFuturesUsdt = new ConcurrentQueue<string>();
            _queueMessageMarketDepthFuturesUsdc = new ConcurrentQueue<string>();
            _queueMessageMarketDepthFuturesCoin = new ConcurrentQueue<string>();
            _queueMessageTradesSpot = new ConcurrentQueue<string>();
            _queueMessageTradesFuturesUsdt = new ConcurrentQueue<string>();
            _queueMessageTradesFuturesUsdc = new ConcurrentQueue<string>();
            _queueMessageTradesFuturesCoin = new ConcurrentQueue<string>();

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
            get { return ServerType.BitGetUnified; }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        private DateTime _lastConnectionStartTime = DateTime.MinValue;

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        public bool IsCompletelyDeleted { get; set; }

        #endregion

        #region 2 Properties

        public List<IServerParameter> ServerParameters { get; set; }

        private string _baseUrl = "https://api.bitget.com";

        private string _publicKey;

        private string _seckretKey;

        private string _passphrase;

        private bool _hedgeMode;

        public bool HedgeMode
        {
            get
            {
                return _hedgeMode;
            }
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

        private bool _extendedMarketData;

        #endregion

        #region 3 Securities

        private readonly List<string> _instrumentCategories = new List<string>
        {
            "SPOT",
            "MARGIN",
            "USDT-FUTURES",
            "COIN-FUTURES",
            "USDC-FUTURES"
        };

        private RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<Security> _securities;

        public void GetSecurities()
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            try
            {
                for (int i = 0; i < _instrumentCategories.Count; i++)
                {
                    string category = _instrumentCategories[i];

                    _rateGateSecurity.WaitToProceed();

                    string requestStr = $"/api/v3/market/instruments?category={category}";

                    IRestResponse response = CreatePublicQuery(requestStr, Method.GET);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        SendLogMessage($"Securities error. Category: {category}. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                        continue;
                    }

                    BitGetUnifiedResponse<List<BitGetUnifiedInstrument>> instruments
                        = JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<List<BitGetUnifiedInstrument>>());

                    if (instruments == null)
                    {
                        SendLogMessage($"Securities error. Category: {category}. Empty response body", LogMessageType.Error);
                        continue;
                    }

                    if (instruments.code != "00000")
                    {
                        SendLogMessage($"Securities error. Category: {category}. {instruments.code} || msg: {instruments.msg}", LogMessageType.Error);
                        continue;
                    }

                    if (instruments.data == null || instruments.data.Count == 0)
                    {
                        continue;
                    }

                    for (int j = 0; j < instruments.data.Count; j++)
                    {
                        BitGetUnifiedInstrument instrument = instruments.data[j];

                        if (instrument.status != "online")
                        {
                            continue;
                        }

                        Security newSecurity = CreateSecurity(instrument);

                        if (newSecurity == null)
                        {
                            continue;
                        }

                        _securities.Add(newSecurity);
                    }
                }

                if (_securities.Count == 0)
                {
                    return;
                }

                if (_securities.Count > 0)
                {
                    _securities = _securities.OrderBy(s => s.Name).ToList();
                }

                SecurityEvent?.Invoke(_securities);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities request error: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private Security CreateSecurity(BitGetUnifiedInstrument instrument)
        {
            if (instrument == null
                || string.IsNullOrEmpty(instrument.symbol)
                || string.IsNullOrEmpty(instrument.category))
            {
                return null;
            }

            int priceDecimals = ParseIntSafe(instrument.pricePrecision);
            int volumeDecimals = ParseIntSafe(instrument.quantityPrecision);

            decimal priceStep = GetPriceStep(instrument.priceMultiplier, priceDecimals);
            decimal volumeStep = GetVolumeStep(instrument.quantityMultiplier, volumeDecimals, instrument.minOrderQty);

            Security security = new Security();

            security.Exchange = ServerType.BitGetUnified.ToString();
            security.Name = instrument.symbol;
            security.NameFull = $"{instrument.category}_{instrument.symbol}";
            security.NameClass = instrument.category;

            if (instrument.category == "SPOT"
                || instrument.category == "MARGIN")
            {
                security.NameClass = instrument.category + "_" + instrument.quoteCoin;
            }

            if (instrument.areaSymbol == "yes"
                    && instrument.quoteCoin == "USDT"
                    && instrument.category == "SPOT")
            {
                security.NameClass = instrument.category + "_" + instrument.quoteCoin + "_TradFi";
            }

            if (instrument.category == "USDT-FUTURES")
            {
                security.Name = instrument.symbol + ".P";
            }

            security.NameId = $"{instrument.category}_{instrument.symbol}";
            security.SecurityType = GetSecurityType(instrument.category);
            security.State = SecurityStateType.Activ;

            security.Decimals = priceDecimals;
            security.DecimalsVolume = volumeDecimals;
            security.PriceStep = priceStep;
            security.PriceStepCost = priceStep;
            security.VolumeStep = volumeStep;
            security.Lot = 1;

            security.MinTradeAmountType = MinTradeAmountType.C_Currency;
            security.MinTradeAmount = instrument.minOrderAmount.ToDecimal();

            return security;
        }

        private SecurityType GetSecurityType(string category)
        {
            if (category == "USDT-FUTURES"
                || category == "COIN-FUTURES"
                || category == "USDC-FUTURES")
            {
                return SecurityType.Futures;
            }

            return SecurityType.CurrencyPair;
        }

        private decimal GetPriceStep(string priceMultiplier, int pricePrecision)
        {
            if (string.IsNullOrEmpty(priceMultiplier) == false
                && priceMultiplier != "0")
            {
                return priceMultiplier.ToDecimal();
            }

            return GetDecimalStepFromPrecision(pricePrecision);
        }

        private decimal GetVolumeStep(string quantityMultiplier, int quantityPrecision, string minOrderQty)
        {
            if (string.IsNullOrEmpty(quantityMultiplier) == false
                && quantityMultiplier != "0")
            {
                return quantityMultiplier.ToDecimal();
            }

            if (quantityPrecision == 0)
            {
                return 1;
            }

            decimal precisionStep = GetDecimalStepFromPrecision(quantityPrecision);

            if (string.IsNullOrEmpty(minOrderQty))
            {
                return precisionStep;
            }

            decimal minOrderQtyValue = minOrderQty.ToDecimal();

            if (minOrderQtyValue <= 0)
            {
                return precisionStep;
            }

            return minOrderQtyValue < precisionStep
                ? minOrderQtyValue
                : precisionStep;
        }

        private decimal GetDecimalStepFromPrecision(int precision)
        {
            if (precision <= 0)
            {
                return 1;
            }

            string result = "0,";

            for (int i = 0; i < precision - 1; i++)
            {
                result += "0";
            }

            result += "1";

            return result.ToDecimal();
        }

        private int ParseIntSafe(string value)
        {
            int result;

            if (int.TryParse(value, out result))
            {
                return result;
            }

            return 0;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _portfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            UpdatePortfolio(true);
        }

        private void PortfolioUpdater()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(5000);

                    if (IsCompletelyDeleted == true)
                    {
                        return;
                    }

                    if (this.ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    UpdatePortfolio(false);

                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private void UpdatePortfolio(bool IsUpdateValueBegin)
        {
            try
            {
                AccountData assetsData = GetAssets();

                if (assetsData == null)
                {
                    Disconnect();
                    return;
                }

                Portfolio myPortfolio;

                if (_portfolios.Count == 0)
                {
                    Portfolio newPortf = new Portfolio();
                    newPortf.Number = "BitgetUnifiedPortfolio";
                    newPortf.ValueBegin = assetsData.usdtEquity.ToDecimal();
                    newPortf.ValueCurrent = assetsData.usdtEquity.ToDecimal();

                    _portfolios.Add(newPortf);

                    myPortfolio = newPortf;
                }
                else
                {
                    myPortfolio = _portfolios[0];
                }

                myPortfolio.UnrealizedPnl = assetsData.usdtUnrealisedPnl.ToDecimal();

                for (int i = 0; i < assetsData.assets.Length; i++)
                {
                    Asset asset = assetsData.assets[i];

                    PositionOnBoard posPortf = new PositionOnBoard();
                    posPortf.SecurityNameCode = asset.coin;
                    posPortf.ValueBegin = asset.balance.ToDecimal();
                    posPortf.ValueCurrent = asset.available.ToDecimal();
                    posPortf.ValueBlocked = asset.locked.ToDecimal();
                    posPortf.PortfolioName = "BitgetUnifiedPortfolio";

                    myPortfolio.SetNewPosition(posPortf);
                }

                List<BGUPos> positions = GetPositionsInfo();

                if (positions.Count > 0)
                {
                    for (int j = 0; j < positions.Count; j++)
                    {
                        BGUPos pos = positions[j];

                        PositionOnBoard posPortf = new PositionOnBoard();

                        string sec = pos.symbol;

                        if (pos.category == "USDT-FUTURES")
                        {
                            sec = pos.symbol + ".P";
                        }

                        posPortf.SecurityNameCode = pos.holdMode == "hedge_mode" ? sec + "_" + pos.posSide.ToUpper() : sec;
                        posPortf.ValueBegin = pos.total.ToDecimal();
                        posPortf.PortfolioName = "BitgetUnifiedPortfolio";
                        posPortf.UnrealizedPnl = pos.unrealisedPnl.ToDecimal();
                        posPortf.ValueBlocked = pos.frozen.ToDecimal();

                        decimal available = pos.available.ToDecimal();

                        posPortf.ValueCurrent = pos.posSide == "long" ? available : available * -1;

                        myPortfolio.SetNewPosition(posPortf);
                    }
                }

                if (IsUpdateValueBegin)
                {
                    myPortfolio.ValueBegin = Math.Round(assetsData.usdtEquity.ToDecimal(), 4);
                }

                myPortfolio.ValueCurrent = Math.Round(assetsData.usdtEquity.ToDecimal(), 4);
                myPortfolio.UnrealizedPnl = assetsData.usdtUnrealisedPnl.ToDecimal();

                if (myPortfolio.ValueCurrent == 0)
                {
                    myPortfolio.ValueCurrent = 1;
                    myPortfolio.ValueBegin = 1;
                }

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private AccountData GetAssets()
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                string path = "/api/v3/account/assets";

                IRestResponse response = CreatePrivateQuery(path, Method.GET, null, null);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<AccountData> accountInfo
                      = JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<AccountData>());

                    if (accountInfo != null && accountInfo.msg == "success")
                    {
                        return accountInfo.data;
                    }
                    else
                    {
                        SendLogMessage($"Portfolio error. Code: {accountInfo.code} - message: {accountInfo.msg}", LogMessageType.Error);
                        return null;
                    }
                }
                else
                {
                    SendLogMessage($"Portfolio error. {response.StatusCode} || {response.Content}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Portfolio request error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private RateGate _rateGatePos = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<BGUPos> GetPositionsInfo()
        {
            _rateGatePos.WaitToProceed();

            List<BGUPos> positions = [];

            try
            {
                for (int i = 0; i < _instrumentCategories.Count; i++)
                {
                    if (!_instrumentCategories[i].EndsWith("FUTURES"))
                    {
                        continue;
                    }

                    string requestStr = "/api/v3/position/current-position?category=" + _instrumentCategories[i];

                    IRestResponse response = CreatePrivateQuery(requestStr, Method.GET, string.Empty, string.Empty);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        BitGetUnifiedResponse<BGUPositions> positionsInfo
                           = JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<BGUPositions>());

                        if (positionsInfo.code == "00000" && positionsInfo.data != null)
                        {
                            if (positionsInfo.data.list != null && positionsInfo.data.list.Count > 0)
                            {
                                positions.AddRange(positionsInfo.data.list);
                            }
                        }
                        else
                        {
                            SendLogMessage($"Positions info getting error: {positionsInfo.code} || msg: {positionsInfo.msg}", LogMessageType.Error);
                            return positions;
                        }
                    }
                    else
                    {
                        SendLogMessage($"Positions info getting error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                        return positions;
                    }
                }

                return positions;
            }
            catch (Exception exception)

            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
                return positions;
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
            DateTime endTime = DateTime.UtcNow;
            DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

            return GetCandleData(security, timeFrameBuilder, startTime, endTime, endTime);
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return GetCandleData(security, timeFrameBuilder, startTime, endTime, actualTime);
        }

        private List<Candle> GetCandleData(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
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

            TimeSpan span = endTime - startTime;

            int maxCandles = (int)Math.Round(span.TotalMinutes / tfTotalMinutes, MidpointRounding.AwayFromZero);
            bool needLastCandles = true;

            int limitCandles = maxCandles;

            if (span.TotalDays > 90)
            {
                limitCandles = (int)Math.Round((90.0 * 1440) / tfTotalMinutes, MidpointRounding.AwayFromZero);
            }

            if (maxCandles > 1000
                && tfTotalMinutes != 1440)
            {
                limitCandles = 100;
                needLastCandles = false;
            }

            List<Candle> allCandles = new List<Candle>();

            DateTime startTimeData = startTime;

            if (tfTotalMinutes == 1440)
            {
                startTimeData = endTime.AddMinutes(-tfTotalMinutes * 900);
            }

            do
            {
                string stringUrl = needLastCandles ? "/api/v3/market/candles" : "/api/v3/market/history-candles";

                DateTime endTimeData = GetCandlesEndTime(startTimeData, endTime, tfTotalMinutes, limitCandles);

                if (limitCandles == 2 && startTimeData == endTime)
                {
                    startTime = startTime.AddMinutes(-(tfTotalMinutes * limitCandles));
                }

                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTimeData);
                long to = TimeManager.GetTimeStampMilliSecondsToDateTime(endTimeData);

                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

                List<Candle> candles = RequestCandleData(security, interval, from, to, limitCandles, stringUrl);

                if (candles == null
                    || candles.Count == 0)
                {
                    break;
                }

                if (allCandles.Count > 0)
                {
                    DateTime lastAllCandleTime = allCandles[allCandles.Count - 1].TimeStart;

                    for (int i = candles.Count - 1; i >= 0; i--)
                    {
                        if (candles[i].TimeStart <= lastAllCandleTime)
                        {
                            candles.RemoveAt(i);
                        }
                    }
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

                startTimeData = last.TimeStart.AddMinutes(tfTotalMinutes);

                if (startTimeData >= endTime)
                {
                    break;
                }
            }
            while (true);

            return allCandles;
        }

        private DateTime GetCandlesEndTime(DateTime startTime, DateTime endTime, int timeFrameMinutes, int limitCandles)
        {
            DateTime endTimeData = startTime.AddMinutes(timeFrameMinutes * limitCandles);

            if (endTimeData > endTime)
            {
                endTimeData = endTime;
            }

            return endTimeData;
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
            if (timeFrameMinutes == 1
                || timeFrameMinutes == 5
                || timeFrameMinutes == 15
                || timeFrameMinutes == 30
                || timeFrameMinutes == 60
                || timeFrameMinutes == 240
                || timeFrameMinutes == 1440)
            {
                return true;
            }

            return false;
        }

        private string GetInterval(TimeSpan tf)
        {
            if (tf.Minutes != 0)
            {
                return $"{tf.Minutes}m";
            }
            else if (tf.Hours != 0)
            {
                return $"{tf.Hours}H";
            }
            else
            {
                return $"{tf.Days}Dutc";
            }
        }

        private readonly RateGate _rgCandleData = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private List<Candle> RequestCandleData(Security security, string interval, long startTime, long endTime, int limitCandles, string url)
        {
            _rgCandleData.WaitToProceed();

            try
            {
                string category = security.NameFull.Split('_')[0];

                if (category == "MARGIN")
                {
                    category = "SPOT";
                }

                string requestStr = $"{url}?category={category}&symbol={security.Name.Split('.')[0]}&" +
                    $"startTime={startTime}&interval={interval}&limit={limitCandles}&endTime={endTime}";

                IRestResponse response = CreatePublicQuery(requestStr, Method.GET);

                if (response == null)
                {
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<List<List<string>>> responseCandles
                        = JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<List<List<string>>>());

                    if (responseCandles != null && responseCandles.code == "00000")
                    {
                        return ConvertCandles(responseCandles);
                    }

                    SendLogMessage($"Candle data error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
                else
                {
                    if (response.ToString().StartsWith("<!DOCTYPE") == false
                        && response.StatusCode != 0)
                    {
                        SendLogMessage($"Candle data error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Candle> ConvertCandles(BitGetUnifiedResponse<List<List<string>>> responseCandles)
        {
            List<Candle> candles = new List<Candle>();

            if (responseCandles.data == null || responseCandles.data.Count == 0)
            {
                return candles;
            }

            for (int i = 0; i < responseCandles.data.Count; i++)
            {
                if (CheckCandlesToZeroData(responseCandles.data[i]))
                {
                    continue;
                }

                Candle candle = new Candle();

                candle.State = CandleState.Finished;
                candle.TimeStart = TimeManager.GetDateTimeFromTimeStamp(long.Parse(responseCandles.data[i][0]));
                candle.Volume = responseCandles.data[i][5].ToDecimal();
                candle.Close = responseCandles.data[i][4].ToDecimal();
                candle.High = responseCandles.data[i][2].ToDecimal();
                candle.Low = responseCandles.data[i][3].ToDecimal();
                candle.Open = responseCandles.data[i][1].ToDecimal();

                candles.Add(candle);
            }

            candles.Sort((a, b) => a.TimeStart.CompareTo(b.TimeStart));

            return candles;
        }

        private bool CheckCandlesToZeroData(List<string> item)
        {
            if (item == null || item.Count < 6)
            {
                return true;
            }

            if (item[1].ToDecimal() == 0 ||
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
        }

        #endregion

        #region 6 WebSocket creation

        private string _webSocketUrlPublic = "wss://ws.bitget.com/v3/ws/public";

        private string _webSocketUrlPrivate = "wss://ws.bitget.com/v3/ws/private";

        private List<WebSocket> _webSocketPublic = new List<WebSocket>();

        private WebSocket _webSocketPrivate;

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
                WebSocket webSocketPublicNew = new WebSocket(_webSocketUrlPublic);

                if (_myProxy != null)
                {
                    webSocketPublicNew.SetProxy(_myProxy);
                }

                webSocketPublicNew.EmitOnPing = true;
                webSocketPublicNew.OnOpen += WebSocketPublic_Opened;
                webSocketPublicNew.OnMessage += WebSocketPublic_MessageReceived;
                webSocketPublicNew.OnError += WebSocketPublic_Error;
                webSocketPublicNew.OnClose += WebSocketPublic_Closed;
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

                if (_myProxy != null)
                {
                    _webSocketPrivate.SetProxy(_myProxy);
                }

                _webSocketPrivate.EmitOnPing = true;
                _webSocketPrivate.OnOpen += WebSocketPrivate_Opened;
                _webSocketPrivate.OnClose += WebSocketPrivate_Closed;
                _webSocketPrivate.OnMessage += WebSocketPrivate_MessageReceived;
                _webSocketPrivate.OnError += WebSocketPrivate_Error;
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

                        webSocketPublic.OnOpen -= WebSocketPublic_Opened;
                        webSocketPublic.OnClose -= WebSocketPublic_Closed;
                        webSocketPublic.OnMessage -= WebSocketPublic_MessageReceived;
                        webSocketPublic.OnError -= WebSocketPublic_Error;

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
                    _webSocketPrivate.OnOpen -= WebSocketPrivate_Opened;
                    _webSocketPrivate.OnClose -= WebSocketPrivate_Closed;
                    _webSocketPrivate.OnMessage -= WebSocketPrivate_MessageReceived;
                    _webSocketPrivate.OnError -= WebSocketPrivate_Error;
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

                    SetPositionMode();
                }
            }
        }

        private void CreateAuthMessageWebSocekt()
        {
            try
            {
                string TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                string Sign = GenerateSignature(TimeStamp, "GET", "/user/verify", null, null, _seckretKey);

                BGUWebsocketAuth requestWebsocketAuth = new BGUWebsocketAuth();

                requestWebsocketAuth.op = "login";
                requestWebsocketAuth.args = new List<AuthItem>();
                requestWebsocketAuth.args.Add(new AuthItem());
                requestWebsocketAuth.args[0].apiKey = _publicKey;
                requestWebsocketAuth.args[0].passphrase = _passphrase;
                requestWebsocketAuth.args[0].timestamp = TimeStamp;
                requestWebsocketAuth.args[0].sign = Sign;

                string AuthJson = JsonConvert.SerializeObject(requestWebsocketAuth);

                _webSocketPrivate.SendAsync(AuthJson);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketPublic_Opened(object sender, EventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("WebSocket Public connection open", LogMessageType.System);
                    CheckSocketsActivate();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPublic_Closed(object sender, CloseEventArgs e)
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

        private void WebSocketPublic_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (e.Data.Length == 4)
                { // pong message
                    return;
                }

                if (_fIFOListWebSocketPublicMessage == null)
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

        private void WebSocketPublic_Error(object sender, ErrorEventArgs e)
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

        private void WebSocketPrivate_Opened(object sender, EventArgs e)
        {
            try
            {
                CreateAuthMessageWebSocekt();
                SendLogMessage("WebSocket Private connection open", LogMessageType.System);
                CheckSocketsActivate();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_Closed(object sender, CloseEventArgs e)
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

        private void WebSocketPrivate_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (e.Data.Length == 4)
                { // pong message
                    return;
                }

                if (e.Data.Contains("login"))
                {
                    SubscribePrivate();
                }

                if (_fIFOListWebSocketPrivateMessage == null)
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

        private void WebSocketPrivate_Error(object sender, ErrorEventArgs e)
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

                    Thread.Sleep(25000);

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
                            webSocketPublic.SendAsync("ping");
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
                        _webSocketPrivate.SendAsync("ping");
                    }
                    else
                    {
                        Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }
        #endregion

        #region 9 Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private List<Security> _subscribedSecutiries = new List<Security>();

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (_subscribedSecutiries != null)
                {
                    for (int i = 0; i < _subscribedSecutiries.Count; i++)
                    {
                        if (_subscribedSecutiries[i].Name.Equals(security.Name))
                        {
                            return;
                        }
                    }
                }

                _subscribedSecutiries.Add(security);

                if (_webSocketPublic.Count == 0)
                {
                    return;
                }

                WebSocket webSocketPublic = _webSocketPublic[_webSocketPublic.Count - 1];

                if (webSocketPublic.ReadyState == WebSocketState.Open
                    && _subscribedSecutiries.Count != 0
                    && _subscribedSecutiries.Count % 50 == 0)
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
                    string category = security.NameFull.Split('_')[0];

                    if (category == "MARGIN")
                    {
                        category = "SPOT";
                    }

                    webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"{category.ToLower()}\",\"topic\": \"books50\",\"symbol\": \"{security.Name.Split('.')[0]}\"}}]}}");
                    webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{ \"instType\": \"{category.ToLower()}\",\"topic\": \"publicTrade\",\"symbol\": \"{security.Name.Split('.')[0]}\"}}]}}");

                    if (_extendedMarketData && category != "SPOT" && category != "MARGIN")
                    {
                        webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{ \"instType\": \"{category.ToLower()}\",\"topic\": \"ticker\",\"symbol\": \"{security.Name.Split('.')[0]}\"}}]}}");
                        GetFundingData(security.Name, category);
                        GetFundingHistory(security.Name, category);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private readonly RateGate _rgFunding = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private void GetFundingData(string securityName, string category)
        {
            _rgFunding.WaitToProceed();

            try
            {
                string requestStr = $"/api/v3/market/current-fund-rate?symbol={securityName.Split('.')[0]}";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<List<BGUFundingItem>> responseFunding =
                        JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<List<BGUFundingItem>>());

                    if (responseFunding.code == "00000")
                    {
                        BGUFundingItem item = responseFunding.data[0];

                        Funding data = new Funding();

                        data.SecurityNameCode = item.symbol;

                        if (category == "USDT-FUTURES")
                        {
                            data.SecurityNameCode = item.symbol + ".P";
                        }

                        data.MaxFundingRate = item.maxFundingRate.ToDecimal();
                        data.MinFundingRate = item.minFundingRate.ToDecimal();
                        data.FundingIntervalHours = int.Parse(item.fundingRateInterval);
                        data.CurrentValue = item.fundingRate.ToDecimal() * 100;

                        FundingUpdateEvent?.Invoke(data);
                    }
                    else
                    {
                        SendLogMessage($"Funding data error:{responseFunding.code} || msg: {responseFunding.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Funding data error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Funding data error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void GetFundingHistory(string securityName, string category)
        {
            _rgFunding.WaitToProceed();

            try
            {
                string requestStr = $"/api/v3/market/history-fund-rate?category={category}&symbol={securityName.Split('.')[0]}&limit=1";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<BGUFundingHistory> responseFunding =
                        JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<BGUFundingHistory>());

                    if (responseFunding.code == "00000")
                    {
                        BGUFundingHistoryItem item = responseFunding.data.resultList[0];

                        Funding data = new Funding();

                        data.SecurityNameCode = item.symbol;

                        if (category == "USDT-FUTURES")
                        {
                            data.SecurityNameCode = item.symbol + ".P";
                        }

                        data.PreviousFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)item.fundingRateTimestamp.ToDecimal());
                        data.CurrentValue = item.fundingRate.ToDecimal() * 100;

                        FundingUpdateEvent?.Invoke(data);
                    }
                    else
                    {
                        SendLogMessage($"Funding history error:{responseFunding.code} || msg: {responseFunding.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Funding history error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Funding history error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void SubscribePrivate()
        {
            try
            {
                _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"UTA\",\"topic\": \"position\"}}]}}");
                _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"UTA\",\"topic\": \"order\"}}]}}");
                _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"UTA\",\"topic\": \"account\"}}]}}");
                _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"UTA\",\"topic\": \"fill\"}}]}}");
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
                                if (_subscribedSecutiries != null)
                                {
                                    List<string> argsList = new List<string>();

                                    for (int i2 = 0; i2 < _subscribedSecutiries.Count; i2++)
                                    {
                                        string category = _subscribedSecutiries[i2].NameFull.Split('_')[0];
                                        string name = _subscribedSecutiries[i2].Name.Split('.')[0];

                                        if (category == "MARGIN")
                                        {
                                            category = "SPOT";
                                        }

                                        argsList.Add($"{{\"instType\": \"{category.ToLower()}\",\"topic\": \"books50\",\"symbol\": \"{name}\"}}");
                                        argsList.Add($"{{\"instType\": \"{category.ToLower()}\",\"topic\": \"publicTrade\",\"symbol\": \"{name}\"}}");

                                        if (_extendedMarketData && category != "SPOT" && category != "MARGIN")
                                        {
                                            argsList.Add($"{{\"instType\": \"{category.ToLower()}\",\"topic\": \"ticker\",\"symbol\": \"{name}\"}}");
                                        }
                                    }

                                    if (argsList.Count > 0)
                                    {
                                        string message = $"{{\"op\":\"unsubscribe\",\"args\":[{string.Join(",", argsList)}]}}";

                                        webSocketPublic.SendAsync(message);
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
                    List<string> privateArgsList =
                    [
                        $"{{\"instType\": \"UTA\",\"topic\": \"account\"}}",
                        $"{{\"instType\": \"UTA\",\"topic\": \"order\"}}]}}",
                        $"{{\"instType\": \"UTA\",\"topic\": \"position\"}}]}}",
                        $"{{\"instType\": \"UTA\",\"topic\": \"fill\"}}"
                    ];

                    if (privateArgsList.Count > 0)
                    {
                        string privateMessage = $"{{\"op\":\"unsubscribe\",\"args\":[{string.Join(",", privateArgsList)}]}}";

                        _webSocketPrivate.SendAsync(privateMessage);
                    }
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

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 10 WebSocket messages parsing

        private ConcurrentQueue<string> _fIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _fIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageMarketDepthSpot = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageMarketDepthFuturesUsdt = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageMarketDepthFuturesUsdc = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageMarketDepthFuturesCoin = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageTradesSpot = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageTradesFuturesUsdt = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageTradesFuturesUsdc = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMessageTradesFuturesCoin = new ConcurrentQueue<string>();

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
                        string message = null;

                        _fIFOListWebSocketPublicMessage.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.StartsWith("{\"event\":\"error\""))
                        {
                            SendLogMessage("WebSocket listener error", LogMessageType.Error);
                            SendLogMessage(message, LogMessageType.Error);

                            if (_lastConnectionStartTime.AddMinutes(5) > DateTime.Now)
                            {
                                Disconnect();
                            }
                        }
                        else if (message.Contains("books50"))
                        {
                            if (message.Contains("spot"))
                            {
                                _queueMessageMarketDepthSpot.Enqueue(message);
                            }
                            else if (message.Contains("usdt-futures"))
                            {
                                _queueMessageMarketDepthFuturesUsdt.Enqueue(message);
                            }
                            else if (message.Contains("coin-futures"))
                            {
                                _queueMessageMarketDepthFuturesCoin.Enqueue(message);
                            }
                            else if (message.Contains("usdc-futures"))
                            {
                                _queueMessageMarketDepthFuturesUsdc.Enqueue(message);
                            }

                            continue;
                        }
                        else if (message.Contains("publicTrade"))
                        {
                            if (message.Contains("spot"))
                            {
                                _queueMessageTradesSpot.Enqueue(message);
                            }
                            else if (message.Contains("usdt-futures"))
                            {
                                _queueMessageTradesFuturesUsdt.Enqueue(message);
                            }
                            else if (message.Contains("coin-futures"))
                            {
                                _queueMessageTradesFuturesCoin.Enqueue(message);
                            }
                            else if (message.Contains("usdc-futures"))
                            {
                                _queueMessageTradesFuturesUsdc.Enqueue(message);
                            }

                            continue;
                        }
                        else if (message.Contains("ticker"))
                        {
                            UpdateTicker(message);
                            continue;
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private void ThreadMessageReaderMarketDepthSpot()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageMarketDepthSpot.IsEmpty)
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

                        _queueMessageMarketDepthSpot.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        MarketDepth marketDepth = UpdateDepth(message);

                        if (marketDepth == null)
                        {
                            continue;
                        }

                        if (MarketDepthEvent != null)
                        {
                            MarketDepthEvent(marketDepth);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private void ThreadMessageReaderMarketDepthFuturesUsdt()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageMarketDepthFuturesUsdt.IsEmpty)
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

                        _queueMessageMarketDepthFuturesUsdt.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        MarketDepth marketDepth = UpdateDepth(message);

                        if (marketDepth == null)
                        {
                            continue;
                        }

                        if (MarketDepthEvent != null)
                        {
                            MarketDepthEvent(marketDepth);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private void ThreadMessageReaderMarketDepthFuturesUsdc()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageMarketDepthFuturesUsdc.IsEmpty)
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

                        _queueMessageMarketDepthFuturesUsdc.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        MarketDepth marketDepth = UpdateDepth(message);

                        if (marketDepth == null)
                        {
                            continue;
                        }

                        if (MarketDepthEvent != null)
                        {
                            MarketDepthEvent(marketDepth);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private void ThreadMessageReaderMarketDepthFuturesCoin()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageMarketDepthFuturesCoin.IsEmpty)
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

                        _queueMessageMarketDepthFuturesCoin.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        MarketDepth marketDepth = UpdateDepth(message);

                        if (marketDepth == null)
                        {
                            continue;
                        }

                        if (MarketDepthEvent != null)
                        {
                            MarketDepthEvent(marketDepth);
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private MarketDepth UpdateDepth(string message)
        {
            try
            {
                BGUResponseWebSockets<BGUnifiedDepth> responseDepth = JsonConvert.DeserializeAnonymousType(message, new BGUResponseWebSockets<BGUnifiedDepth>());

                if (responseDepth.data == null)
                {
                    return null;
                }

                if (responseDepth.data[0].a.Count == 0 && responseDepth.data[0].b.Count == 0)
                {
                    return null;
                }

                MarketDepth marketDepth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                marketDepth.SecurityNameCode = responseDepth.arg.symbol;

                if (responseDepth.arg.instType == "usdt-futures")
                {
                    marketDepth.SecurityNameCode = responseDepth.arg.symbol + ".P";
                }

                for (int i = 0; i < responseDepth.data[0].a.Count; i++)
                {
                    List<string> askList = responseDepth.data[0].a[i];

                    double ask = askList[1].ToDouble();
                    double price = askList[0].ToDouble();

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

                for (int i = 0; i < responseDepth.data[0].b.Count; i++)
                {
                    List<string> bidList = responseDepth.data[0].b[i];

                    double bid = bidList[1].ToDouble();
                    double price = bidList[0].ToDouble();

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

                marketDepth.Asks = ascs;
                marketDepth.Bids = bids;

                const int maxCount = 25;

                if (marketDepth.Bids.Count > maxCount)
                {
                    marketDepth.Bids.RemoveRange(maxCount - 1, marketDepth.Bids.Count - maxCount);
                }

                if (marketDepth.Asks.Count > maxCount)
                {
                    marketDepth.Asks.RemoveRange(maxCount - 1, marketDepth.Asks.Count - maxCount);
                }

                marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseDepth.data[0].ts));

                if (marketDepth.Time == DateTime.MinValue)
                {
                    return null;
                }

                if (marketDepth.Time <= _lastTimeMd)
                {
                    marketDepth.Time = _lastTimeMd.AddTicks(1);
                }

                _lastTimeMd = marketDepth.Time;

                return marketDepth;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private DateTime _lastTimeMd = DateTime.MinValue;

        private void ThreadMessageReaderTradesSpot()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageTradesSpot.IsEmpty)
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

                        _queueMessageTradesSpot.TryDequeue(out message);

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
                    Thread.Sleep(3000);
                }
            }
        }

        private void ThreadMessageReaderTradesFuturesUsdt()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageTradesFuturesUsdt.IsEmpty)
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

                        _queueMessageTradesFuturesUsdt.TryDequeue(out message);

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
                    Thread.Sleep(3000);
                }
            }
        }

        private void ThreadMessageReaderTradesFuturesUsdc()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageTradesFuturesUsdc.IsEmpty)
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

                        _queueMessageTradesFuturesUsdc.TryDequeue(out message);

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
                    Thread.Sleep(3000);
                }
            }
        }

        private void ThreadMessageReaderTradesFuturesCoin()
        {
            while (true)
            {
                try
                {
                    if (_queueMessageTradesFuturesCoin.IsEmpty)
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

                        _queueMessageTradesFuturesCoin.TryDequeue(out message);

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
                    Thread.Sleep(3000);
                }
            }
        }

        private void UpdateTrade(string message)
        {
            try
            {
                BGUResponseWebSockets<BGUnifiedPublicTrade> responseTrade = JsonConvert.DeserializeAnonymousType(message, new BGUResponseWebSockets<BGUnifiedPublicTrade>());

                if (responseTrade == null
                    || responseTrade.data == null)
                {
                    return;
                }

                for (int i = responseTrade.data.Length - 1; i >= 0; i--)
                {
                    Trade trade = new Trade();
                    trade.SecurityNameCode = responseTrade.arg.symbol;

                    if (responseTrade.arg.instType == "usdt-futures")
                    {
                        trade.SecurityNameCode = responseTrade.arg.symbol + ".P";
                    }

                    trade.Price = responseTrade.data[i].p.ToDecimal();
                    trade.Id = responseTrade.data[i].i;

                    if (trade.Id == null)
                    {
                        return;
                    }

                    trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data[i].T));
                    trade.Volume = responseTrade.data[i].v.ToDecimal();
                    trade.Side = responseTrade.data[i].S.Equals("buy") ? Side.Buy : Side.Sell;

                    if (_extendedMarketData)
                    {
                        trade.OpenInterest = GetOpenInterestValue(trade.SecurityNameCode);
                    }

                    NewTradesEvent?.Invoke(trade);
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

        private void UpdateTicker(string message)
        {
            try
            {
                BGUResponseWebSockets<BGUnifiedTicker> responseTicker = JsonConvert.DeserializeAnonymousType(message, new BGUResponseWebSockets<BGUnifiedTicker>());

                if (responseTicker == null
                    || responseTicker.data == null
                    || responseTicker.data[0] == null)
                {
                    return;
                }

                BGUnifiedTicker item = responseTicker.data[0];

                string sec = responseTicker.arg.symbol;

                if (responseTicker.arg.instType == "usdt-futures")
                {
                    sec = responseTicker.arg.symbol + ".P";
                }

                if (responseTicker.arg.instType.EndsWith("futures"))
                {
                    Funding funding = new Funding();

                    funding.SecurityNameCode = sec;
                    funding.CurrentValue = item.fundingRate.ToDecimal() * 100;
                    funding.NextFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)item.nextFundingTime.ToDecimal());
                    funding.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)responseTicker.ts.ToDecimal());

                    FundingUpdateEvent?.Invoke(funding);
                }

                SecurityVolumes volume = new SecurityVolumes();

                volume.SecurityNameCode = sec;
                volume.Volume24h = item.volume24h.ToDecimal();
                volume.Volume24hUSDT = item.turnover24h.ToDecimal();

                Volume24hUpdateEvent?.Invoke(volume);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
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
                        string message = null;

                        _fIFOListWebSocketPrivateMessage.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.StartsWith("{\"event\":\"error\""))
                        {
                            SendLogMessage("WebSocket listener error", LogMessageType.Error);
                            SendLogMessage(message, LogMessageType.Error);

                            if (_lastConnectionStartTime.AddMinutes(5) > DateTime.Now)
                            { // if there are problems with the web socket startup, you need to restart it
                                Disconnect();
                            }

                            continue;
                        }

                        BGUResponseWebSockets<object> stream = JsonConvert.DeserializeAnonymousType(message, new BGUResponseWebSockets<object>());

                        if ((stream?.arg) != null && (stream?.data) != null)
                        {
                            if (stream.arg.topic.Equals("account") && stream.action.Equals("update"))
                            {
                                UpdateAssets(message);
                                continue;
                            }
                            else if (stream.arg.topic.Equals("position") && stream.action.Equals("update"))
                            {
                                UpdatePositions(message);
                                continue;
                            }
                            else if (stream.arg.topic.Equals("order"))
                            {
                                UpdateOrder(message);
                                continue;
                            }
                            else if (stream.arg.topic.Equals("fill"))
                            {
                                UpdateMyTrade(message);
                                continue;
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(3000);
                }
            }
        }

        private void UpdateAssets(string message)
        {
            try
            {
                BGUResponseWebSockets<BGUnifiedAccount> responce = JsonConvert.DeserializeAnonymousType(message, new BGUResponseWebSockets<BGUnifiedAccount>());

                if (responce.data == null)
                {
                    return;
                }

                Portfolio portfolio = _portfolios[0];
                portfolio.UnrealizedPnl = responce.data[0].unrealisedPnL.ToDecimal();

                for (int i = 0; i < responce.data.Length; i++)
                {
                    for (int j = 0; j < responce.data[i].coin.Length; j++)
                    {
                        BGUnifiedCoin coin = responce.data[i].coin[j];

                        PositionOnBoard newPortf = new PositionOnBoard();
                        newPortf.SecurityNameCode = coin.coin;
                        newPortf.ValueBegin = coin.balance.ToDecimal();
                        newPortf.ValueCurrent = coin.available.ToDecimal();
                        newPortf.ValueBlocked = coin.locked.ToDecimal();
                        newPortf.PortfolioName = "BitgetUnifiedPortfolio";
                        portfolio.SetNewPosition(newPortf);
                    }
                }

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void UpdatePositions(string message)
        {
            try
            {
                BGUResponseWebSockets<BGUnifiedPositions> positions = JsonConvert.DeserializeAnonymousType(message, new BGUResponseWebSockets<BGUnifiedPositions>());

                if (positions.data == null)
                {
                    return;
                }

                if (positions != null)
                {
                    if (positions.data.Length > 0)
                    {
                        Portfolio portfolio = _portfolios[0];

                        for (int i = 0; i < positions.data.Length; i++)
                        {
                            var pos = positions.data[i];

                            PositionOnBoard posPortf = new PositionOnBoard();
                            posPortf.PortfolioName = "BitgetUnifiedPortfolio";

                            string sec = pos.symbol;

                            if (pos.symbol.EndsWith("USDT"))
                            {
                                sec = pos.symbol + ".P";
                            }
                            posPortf.SecurityNameCode = pos.holdMode == "hedge_mode" ? sec + "_" + pos.posSide.ToUpper() : sec;
                            posPortf.ValueBlocked = positions.data[i].frozen.ToDecimal();
                            posPortf.UnrealizedPnl = positions.data[i].unrealisedPnl.ToDecimal();

                            decimal available = pos.available.ToDecimal();
                            posPortf.ValueCurrent = pos.posSide == "long" ? available : available * -1;

                            portfolio.SetNewPosition(posPortf);
                        }

                        PortfolioEvent?.Invoke(_portfolios);
                    }
                }
                else
                {
                    SendLogMessage("Update positions error. Response data was null", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdateOrder(string message)
        {
            try
            {
                BGUResponseWebSockets<BGUnifiedOrder> orderSnapshot = JsonConvert.DeserializeAnonymousType(message, new BGUResponseWebSockets<BGUnifiedOrder>());

                if (orderSnapshot.data.Length == 0)
                {
                    return;
                }

                for (int i = 0; i < orderSnapshot.data.Length; i++)
                {
                    BGUnifiedOrder order = orderSnapshot.data[i];

                    if (string.IsNullOrEmpty(order.orderId))
                    {
                        continue;
                    }

                    OrderStateType stateType = GetOrderState(order.orderStatus);

                    if (order.orderType.Equals("market") && stateType != OrderStateType.Done && stateType != OrderStateType.Partial)
                    {
                        continue;
                    }

                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = order.symbol;

                    if (order.category == "usdt-futures")
                    {
                        newOrder.SecurityNameCode = order.symbol + ".P";
                    }

                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.updatedTime));
                    int.TryParse(order.clientOid, out newOrder.NumberUser);
                    newOrder.NumberMarket = order.orderId.ToString();
                    newOrder.Side = order.side == "buy" ? Side.Buy : Side.Sell;
                    newOrder.State = stateType;
                    newOrder.Volume = order.qty.ToDecimal();
                    newOrder.Price = order.price.ToDecimal();
                    newOrder.ServerType = ServerType.BitGetUnified;
                    newOrder.PortfolioNumber = "BitgetUnifiedPortfolio";
                    newOrder.SecurityClassCode = orderSnapshot.arg.instType.ToString();

                    if (order.orderType.Equals("market"))
                    {
                        newOrder.TypeOrder = OrderPriceType.Market;
                    }
                    else
                    {
                        newOrder.TypeOrder = OrderPriceType.Limit;
                    }

                    MyOrderEvent?.Invoke(newOrder);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdateMyTrade(string message)
        {
            try
            {
                BGUResponseWebSockets<BGUnifiedMyTrade> myTradeSnapshot = JsonConvert.DeserializeAnonymousType(message, new BGUResponseWebSockets<BGUnifiedMyTrade>());

                if (myTradeSnapshot.data == null
                    || myTradeSnapshot.data.Length == 0)
                {
                    return;
                }

                for (int i = 0; i < myTradeSnapshot.data.Length; i++)
                {
                    BGUnifiedMyTrade trade = myTradeSnapshot.data[i];

                    long time = Convert.ToInt64(trade.updatedTime);

                    MyTrade newTrade = new MyTrade();

                    newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(time);
                    newTrade.SecurityNameCode = trade.symbol;

                    if (trade.category == "usdt-futures")
                    {
                        newTrade.SecurityNameCode = trade.symbol + ".P";
                    }

                    newTrade.NumberOrderParent = trade.orderId;
                    newTrade.Price = trade.execPrice.ToDecimal();
                    newTrade.NumberTrade = trade.execId;
                    newTrade.Side = trade.side.Equals("buy") ? Side.Buy : Side.Sell;

                    if (string.IsNullOrEmpty(trade.feeDetail[0].feeCoin) == false
                       && string.IsNullOrEmpty(trade.feeDetail[0].fee) == false
                       && trade.feeDetail[0].fee.ToDecimal() != 0)
                    {
                        if (newTrade.SecurityNameCode.StartsWith(trade.feeDetail[0].feeCoin)
                            && trade.category != "coin-futures")
                        {
                            newTrade.Volume = trade.execQty.ToDecimal() - trade.feeDetail[0].fee.ToDecimal();
                            int decimalVolume = GetVolumeDecimals(newTrade.SecurityNameCode);

                            if (decimalVolume > 0)
                            {
                                newTrade.Volume = Math.Floor(newTrade.Volume * (decimal)Math.Pow(10, decimalVolume)) / (decimal)Math.Pow(10, decimalVolume);
                            }
                        }
                        else
                        {
                            newTrade.Volume = trade.execQty.ToDecimal();
                        }
                    }
                    else
                    {
                        newTrade.Volume = trade.execQty.ToDecimal();
                    }

                    MyTradeEvent?.Invoke(newTrade);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private int GetVolumeDecimals(string security)
        {
            for (int i = 0; i < _securities.Count; i++)
            {
                if (security == _securities[i].Name)
                {
                    return _securities[i].DecimalsVolume;
                }
            }

            return 0;
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

        private RateGate _rateGateOrder10S = new RateGate(1, TimeSpan.FromMilliseconds(100));
        private RateGate _rateGateOrder20S = new RateGate(1, TimeSpan.FromMilliseconds(50));
        private RateGate _rateGateOrder5S = new RateGate(1, TimeSpan.FromMilliseconds(200));

        public void SendOrder(Order order)
        {
            try
            {
                string reduceOnly = "no";
                string posSide = "long";
                bool isFuture = order.SecurityClassCode.Contains("FUT");

                if (HedgeMode)
                {
                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        posSide = order.Side == Side.Buy ? "short" : "long";
                    }
                    else
                    {
                        posSide = order.Side == Side.Buy ? "long" : "short";
                    }
                }
                else
                {
                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        reduceOnly = "yes";
                    }
                }

                _rateGateOrder10S.WaitToProceed();

                Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>();

                jsonContent.Add("category", order.SecurityClassCode.Split('_')[0]);
                jsonContent.Add("symbol", order.SecurityNameCode.Split('.')[0]);
                decimal volume = order.Volume;

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    jsonContent.Add("price", order.Price.ToString().Replace(",", "."));
                }

                if ((order.SecurityClassCode.Split('_')[0] == "SPOT"
                    || order.SecurityClassCode.Split('_')[0] == "MARGIN")
                    && order.TypeOrder == OrderPriceType.Market
                    && order.Side == Side.Buy)
                {
                    volume = CalculateQty(order);
                }

                jsonContent.Add("qty", volume.ToString().Replace(",", "."));
                jsonContent.Add("side", order.Side.ToString().ToLower());
                jsonContent.Add("orderType", order.TypeOrder.ToString().ToLower());

                if (HedgeMode && isFuture)
                {
                    jsonContent.Add("posSide", posSide);
                }

                jsonContent.Add("clientOid", order.NumberUser.ToString());
                jsonContent.Add("reduceOnly", reduceOnly);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                IRestResponse responseMessage = CreatePrivateQuery("/api/v3/trade/place-order", Method.POST, null, jsonRequest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<BGUOrderResponse> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new BitGetUnifiedResponse<BGUOrderResponse>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        // ignore
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Order Fail: {stateResponse.code} || msg: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Order Fail. Status: {responseMessage.StatusCode} || msg:{responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Order send error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public decimal CalculateQty(Order order)
        {
            decimal qty = order.Volume;

            try
            {
                string requestStr = $"/api/v3/market/tickers?category=SPOT&symbol={order.SecurityNameCode.Split('.')[0]}";

                IRestResponse response = CreatePublicQuery(requestStr, Method.GET);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    SendLogMessage($"Ticker Fail. Status: {response.StatusCode} || msg:{response.Content}", LogMessageType.Error);
                }

                BitGetUnifiedResponse<List<TickersItem>> ticker
                            = JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<List<TickersItem>>());

                qty = order.Volume * ticker.data[0].lastPrice.ToDecimal();
            }
            catch (Exception ex)
            {
                SendLogMessage($"Order send error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            return qty;
        }

        public bool CancelOrder(Order order)
        {
            try
            {
                _rateGateOrder10S.WaitToProceed();

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("orderId", order.NumberMarket);
                jsonContent.Add("clientOid", order.NumberUser.ToString());
                jsonContent.Add("category", order.SecurityClassCode.Split('_')[0]);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                IRestResponse response = CreatePrivateQuery("/api/v3/trade/cancel-order", Method.POST, null, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<BGUOrderResponse> stateResponse = JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<BGUOrderResponse>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        order.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(stateResponse.requestTime));
                        order.State = OrderStateType.Cancel;
                        MyOrderEvent?.Invoke(order);
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Cancel order failed: {stateResponse.code} || msg: {stateResponse.msg}", LogMessageType.Error);
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
                        SendLogMessage($"Cancel order failed. Status: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
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
                SendLogMessage($"Cancel order error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            return false;
        }

        public void GetAllActivOrders()
        {
            List<Order> ordersOpenAll = GetAllOpenOrdersArray();

            if (ordersOpenAll == null)
            {
                return;
            }

            for (int i = 0; i < ordersOpenAll.Count; i++)
            {
                if (MyOrderEvent != null)
                {
                    MyOrderEvent(ordersOpenAll[i]);
                }
            }
        }

        private List<Order> GetAllOpenOrdersArray()
        {
            List<Order> ordersOpenAll = new List<Order>();

            GetAllOpenOrders(ordersOpenAll, 100);

            if (ordersOpenAll.Count > 0)
            {
                ordersOpenAll.Sort((a, b) => b.TimeCreate.CompareTo(a.TimeCreate));
            }

            return ordersOpenAll;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            try
            {
                _rateGateOrder20S.WaitToProceed();

                string path = "/api/v3/trade/order-info?orderId=" + order.NumberMarket + "&clientOid=" + order.NumberUser;

                IRestResponse responseMessage = CreatePrivateQuery(path, Method.GET, null, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<BGUOrderInfo> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new BitGetUnifiedResponse<BGUOrderInfo>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        Order newOrder = ConvertRestToOrder(stateResponse.data);

                        MyOrderEvent?.Invoke(newOrder);

                        if (newOrder.State == OrderStateType.Done ||
                            newOrder.State == OrderStateType.Partial)
                        {
                            FindMyTradesToOrder(newOrder);
                        }

                        return newOrder.State;
                    }
                    else
                    {
                        SendLogMessage($"Order status error: {stateResponse.code} || msg: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Order status error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}.\nOrder: [Num: {order.NumberUser}, Sec: {order.SecurityNameCode} Vol: {order.Volume}]", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Order status error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            return OrderStateType.None;
        }

        private void FindMyTradesToOrder(Order order)
        {
            try
            {
                _rateGateOrder10S.WaitToProceed();

                string path = $"/api/v3/trade/fills?orderId={order.NumberMarket}";

                IRestResponse responseMessage = CreatePrivateQuery(path, Method.GET, null, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<BGUHistoryMyTrades> tradesResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new BitGetUnifiedResponse<BGUHistoryMyTrades>());

                    if (tradesResponse.code.Equals("00000") == true)
                    {
                        for (int i = 0; i < tradesResponse.data.list.Length; i++)
                        {
                            BGUMyTrade trade = tradesResponse.data.list[i];

                            long time = Convert.ToInt64(trade.updatedTime);

                            MyTrade newTrade = new MyTrade();

                            newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(time);
                            newTrade.SecurityNameCode = trade.symbol;

                            if (trade.category == "USDT-FUTURES")
                            {
                                newTrade.SecurityNameCode = trade.symbol + ".P";
                            }

                            newTrade.NumberOrderParent = trade.orderId;
                            newTrade.Price = trade.execPrice.ToDecimal();
                            newTrade.NumberTrade = trade.execId;
                            newTrade.Side = trade.side.Equals("buy") ? Side.Buy : Side.Sell;

                            if (string.IsNullOrEmpty(trade.feeDetail[0].feeCoin) == false
                                && string.IsNullOrEmpty(trade.feeDetail[0].fee) == false
                                && trade.feeDetail[0].fee.ToDecimal() != 0)
                            {
                                if (newTrade.SecurityNameCode.StartsWith(trade.feeDetail[0].feeCoin)
                                    && trade.category != "COIN-FUTURES")
                                {
                                    newTrade.Volume = trade.execQty.ToDecimal() - trade.feeDetail[0].fee.ToDecimal();
                                    int decimalVolume = GetVolumeDecimals(newTrade.SecurityNameCode);

                                    if (decimalVolume > 0)
                                    {
                                        newTrade.Volume = Math.Floor(newTrade.Volume * (decimal)Math.Pow(10, decimalVolume)) / (decimal)Math.Pow(10, decimalVolume);
                                    }
                                }
                                else
                                {
                                    newTrade.Volume = trade.execQty.ToDecimal();
                                }
                            }
                            else
                            {
                                newTrade.Volume = trade.execQty.ToDecimal();
                            }

                            MyTradeEvent?.Invoke(newTrade);
                        }
                    }
                    else
                    {
                        SendLogMessage($"My trades request error: {tradesResponse.code} || msg: {tradesResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"My trades to order error: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"My trades to order error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            try
            {
                _rateGateOrder10S.WaitToProceed();

                Dictionary<string, string> jsonContent = new Dictionary<string, string>
                {
                    { "orderId", order.NumberMarket },
                    { "clientOid", order.NumberUser.ToString() },
                    { "price", newPrice.ToString().Replace(',', '.') },
                    { "autoCancel", "no" },
                    { "symbol", order.SecurityNameCode.Split('.')[0] },
                    { "category", order.SecurityClassCode.Split('_')[0] }
                };

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                IRestResponse response = CreatePrivateQuery("/api/v3/trade/modify-order", Method.POST, null, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<BGUOrderResponse> stateResponse = JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<BGUOrderResponse>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        order.Price = newPrice;
                        MyOrderEvent?.Invoke(order);
                    }
                    else
                    {
                        SendLogMessage($"Change price order failed: {stateResponse.code} || msg: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Change price order failed: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Change order price error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                _rateGateOrder5S.WaitToProceed();

                Dictionary<string, string> jsonContent = new Dictionary<string, string>
                {
                    { "category", security.NameFull.Split('_')[0] },
                    { "symbol", security.Name.Split('.')[0] }
                };

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                IRestResponse response = CreatePrivateQuery("/api/v3/trade/cancel-symbol-order", Method.POST, null, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<BGUOrderResponse> stateResponse = JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<BGUOrderResponse>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage($"Cancel orders by symbol request failed: {stateResponse.code} || msg: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Cancel orders by symbol failed: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Cancel all orders to security error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public void CancelAllOrders()
        {
            try
            {
                for (int i = 0; i < _instrumentCategories.Count; i++)
                {
                    _rateGateOrder5S.WaitToProceed();

                    Dictionary<string, string> jsonContent = new Dictionary<string, string>
                    {
                        { "category", _instrumentCategories[i] }
                    };

                    string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                    IRestResponse response = CreatePrivateQuery("/api/v3/trade/cancel-symbol-order", Method.POST, null, jsonRequest);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        BitGetUnifiedResponse<BGUOrderResponse> stateResponse = JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<BGUOrderResponse>());

                        if (stateResponse.code.Equals("00000") == true)
                        {
                            // ignore
                        }
                        else
                        {
                            SendLogMessage($"Cancel all orders  request failed: {stateResponse.code} || msg: {stateResponse.msg}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Cancel all orders failed: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Cancel all orders error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void GetAllOpenOrders(List<Order> array, int maxCount)
        {
            try
            {
                _rateGateOrder20S.WaitToProceed();

                string cursor = string.Empty;

                do
                {
                    string path = string.IsNullOrEmpty(cursor) ? "/api/v3/trade/unfilled-orders" : $"/api/v3/trade/unfilled-orders?cursor={cursor}";

                    IRestResponse responseMessage = CreatePrivateQuery(path, Method.GET, null, null);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        BitGetUnifiedResponse<BGUOrders> unfilledOrdersResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new BitGetUnifiedResponse<BGUOrders>());

                        if (unfilledOrdersResponse.code.Equals("00000") == true)
                        {
                            if (unfilledOrdersResponse.data.list == null
                                || unfilledOrdersResponse.data.list.Length == 0)
                            {
                                cursor = null;
                                break;
                            }

                            if (unfilledOrdersResponse.data.list.Length < 100)
                            {
                                cursor = null;
                            }
                            else
                            {
                                cursor = unfilledOrdersResponse.data.cursor;
                            }

                            for (int i = 0; i < unfilledOrdersResponse.data.list.Length; i++)
                            {
                                Order curOder = ConvertRestToOrder(unfilledOrdersResponse.data.list[i]);
                                array.Add(curOder);

                                if (array.Count >= maxCount)
                                {
                                    cursor = null;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            SendLogMessage($"Open orders getting error: {unfilledOrdersResponse.code} || msg: {unfilledOrdersResponse.msg}", LogMessageType.Error);
                            return;
                        }
                    }
                    else
                    {
                        SendLogMessage($"All open orders getting error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                        return;
                    }
                }
                while (!string.IsNullOrEmpty(cursor));
            }
            catch (Exception ex)
            {
                SendLogMessage($"All open orders getting error: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return;
            }
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            List<Order> result = GetAllOpenOrdersArray();

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
            List<Order> result = GetAllHistoricalOrdersArray();

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

        private List<Order> GetAllHistoricalOrdersArray()
        {
            List<Order> ordersOpenAll = new List<Order>();

            GetAllHistoricalOrders(ordersOpenAll, 100);

            if (ordersOpenAll.Count > 0)
            {
                ordersOpenAll.Sort((a, b) => b.TimeCreate.CompareTo(a.TimeCreate));
            }

            return ordersOpenAll;
        }

        public void GetAllHistoricalOrders(List<Order> array, int maxCount)
        {
            try
            {
                _rateGateOrder20S.WaitToProceed();

                for (int i = 0; i < _instrumentCategories.Count; i++)
                {
                    string path = $"/api/v3/trade/history-orders?category={_instrumentCategories[i]}";

                    IRestResponse responseMessage = CreatePrivateQuery(path, Method.GET, null, null);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        BitGetUnifiedResponse<BGUOrders> historyOrdersResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new BitGetUnifiedResponse<BGUOrders>());

                        if (historyOrdersResponse.code.Equals("00000") == true)
                        {
                            if (historyOrdersResponse.data.list == null)
                            {
                                continue;
                            }

                            for (int j = 0; j < historyOrdersResponse.data.list.Length; j++)
                            {
                                Order curOder = ConvertRestToOrder(historyOrdersResponse.data.list[j]);
                                array.Add(curOder);

                                if (array.Count >= maxCount)
                                {
                                    break;
                                }
                            }
                        }
                        else
                        {
                            SendLogMessage($"History orders getting error: {historyOrdersResponse.code} || msg: {historyOrdersResponse.msg}", LogMessageType.Error);
                            continue;
                        }
                    }
                    else
                    {
                        SendLogMessage($"History orders getting error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"History orders getting error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return;
            }
        }

        private Order ConvertRestToOrder(BGUOrderInfo item)
        {
            Order newOrder = new Order();

            OrderStateType stateType = GetOrderState(item.orderStatus);

            newOrder.SecurityNameCode = item.symbol;

            if (item.category == "USDT-FUTURES")
            {
                newOrder.SecurityNameCode = item.symbol + ".P";
            }

            newOrder.SecurityClassCode = item.category;
            newOrder.State = stateType;
            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.createdTime));
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updatedTime));

            if (newOrder.State == OrderStateType.Cancel)
            {
                newOrder.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updatedTime));
            }

            if (newOrder.State == OrderStateType.Done)
            {
                newOrder.TimeDone = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.updatedTime));
            }

            try
            {
                newOrder.NumberUser = Convert.ToInt32(item.clientOid);
            }
            catch
            {

            }

            newOrder.NumberMarket = item.orderId.ToString();
            newOrder.Side = item.side == "buy" ? Side.Buy : Side.Sell;
            newOrder.Volume = item.qty.ToDecimal();
            newOrder.Price = item.price != "" ? item.price.ToDecimal() : 0;
            newOrder.ServerType = ServerType.BitGetUnified;
            newOrder.PortfolioNumber = "BitgetUnifiedPortfolio";
            newOrder.TypeOrder = item.orderType == "limit" ? OrderPriceType.Limit : OrderPriceType.Market;

            return newOrder;
        }

        private OrderStateType GetOrderState(string orderStatus)
        {
            OrderStateType stateType;

            switch (orderStatus)
            {
                case ("new"):
                    stateType = OrderStateType.Active;
                    break;
                case ("partially_filled"):
                    stateType = OrderStateType.Partial;
                    break;
                case ("filled"):
                    stateType = OrderStateType.Done;
                    break;
                case ("cancelled"):
                    stateType = OrderStateType.Cancel;
                    break;
                case ("live"):
                    stateType = OrderStateType.Active;
                    break;
                default:
                    stateType = OrderStateType.None;
                    break;
            }

            return stateType;
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            MyOrderEvent?.Invoke(order);
        }

        #endregion

        #region 12 Queries

        private IRestResponse CreatePublicQuery(string path, Method method)
        {
            try
            {
                RestRequest requestRest = new RestRequest(path, method);

                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Create public query error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private IRestResponse CreatePrivateQuery(string path, Method method, string queryString, string body)
        {
            try
            {
                RestRequest requestRest = new RestRequest(path, method);

                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature(timestamp, method.ToString(), path, queryString, body, _seckretKey);

                requestRest.AddHeader("ACCESS-KEY", _publicKey);
                requestRest.AddHeader("ACCESS-SIGN", signature);
                requestRest.AddHeader("ACCESS-TIMESTAMP", timestamp);
                requestRest.AddHeader("ACCESS-PASSPHRASE", _passphrase);
                requestRest.AddHeader("X-CHANNEL-API-CODE", "6yq7w");

                if (method.ToString().Equals("POST"))
                {
                    requestRest.AddParameter("application/json", body, ParameterType.RequestBody);
                }

                RestClient client = new RestClient(_baseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Create private query error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private string GenerateSignature(string timestamp, string method, string requestPath, string queryString, string body, string secretKey)
        {
            method = method.ToUpper();
            body = string.IsNullOrEmpty(body) ? string.Empty : body;
            queryString = string.IsNullOrEmpty(queryString) ? string.Empty : "?" + queryString;

            string preHash = timestamp + method + requestPath + queryString + body;

            byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            using (HMACSHA256 hmac = new HMACSHA256(secretKeyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(preHash));
                return Convert.ToBase64String(hashBytes);
            }
        }

        private void SetPositionMode()
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            try
            {
                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("holdMode", HedgeMode == true ? "hedge_mode" : "one_way_mode");

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                IRestResponse responseMessage = CreatePrivateQuery("/api/v3/account/set-hold-mode", Method.POST, null, jsonRequest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<string> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new BitGetUnifiedResponse<string>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage($"Position mode error: {stateResponse.code} || msg: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    if (responseMessage.Content.Contains("\"sign signature error\"")
                        || responseMessage.Content.Contains("\"Apikey does not exist\"")
                        || responseMessage.Content.Contains("\"apikey/password is incorrect\"")
                        || responseMessage.Content.Contains("\"Request timestamp expired\"")
                        || responseMessage.Content.Contains("\"Country/Region is not supported\"")
                        || responseMessage.Content == "")
                    {
                        Disconnect();
                    }

                    SendLogMessage($"Position mode error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }

            }
            catch (Exception ex)
            {
                SendLogMessage($"Position mode error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private List<OpenInterestData> _openInterest = new List<OpenInterestData>();

        private DateTime _timeLastUpdateExtendedData = DateTime.Now;

        private readonly RateGate _rgOpenInterest = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private void ThreadExtendedData()
        {
            while (true)
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    Thread.Sleep(3000);
                }

                if (IsCompletelyDeleted == true)
                {
                    return;
                }

                try
                {
                    if (_subscribedSecutiries != null
                    && _subscribedSecutiries.Count > 0
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
            _rgOpenInterest.WaitToProceed();

            try
            {
                for (int i = 0; i < _subscribedSecutiries.Count; i++)
                {
                    if (!_subscribedSecutiries[i].NameClass.EndsWith("FUTURES"))
                        continue;

                    string requestStr = $"/api/v3/market/open-interest?category={_subscribedSecutiries[i].NameFull.Split('_')[0]}&symbol={_subscribedSecutiries[i].Name.Split('.')[0]}";
                    RestRequest requestRest = new RestRequest(requestStr, Method.GET);

                    RestClient client = new RestClient(_baseUrl);

                    if (_myProxy != null)
                    {
                        client.Proxy = _myProxy;
                    }

                    IRestResponse response = client.Execute(requestRest);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        BitGetUnifiedResponse<BGUOiData> oiResponse = JsonConvert.DeserializeAnonymousType(response.Content, new BitGetUnifiedResponse<BGUOiData>());

                        if (oiResponse.code == "00000")
                        {
                            for (int j = 0; j < oiResponse.data.list.Length; j++)
                            {
                                OpenInterestData openInterestData = new OpenInterestData();

                                openInterestData.SecutityName = oiResponse.data.list[j].symbol;

                                if (_subscribedSecutiries[i].NameFull.Split('_')[0] == "USDT-FUTURES")
                                {
                                    openInterestData.SecutityName = oiResponse.data.list[j].symbol + ".P";
                                }

                                if (oiResponse.data.list[j].openInterest != null)
                                {
                                    openInterestData.OpenInterestValue = oiResponse.data.list[j].openInterest;

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
                        }
                        else
                        {
                            SendLogMessage($"Open interest error: {oiResponse.code} || msg: {oiResponse.msg}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Open interest error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Open interest error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public void SetLeverage(Security security, decimal leverage)
        {
            try
            {
                Dictionary<string, string> jsonContent = new Dictionary<string, string>
                {
                    { "category", security.NameFull.Split('_')[0] },
                    { "symbol", security.Name.Split('.')[0] },
                    {"leverage", leverage.ToString() },
                };

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                IRestResponse responseMessage = CreatePrivateQuery("/api/v3/account/set-leverage", Method.POST, null, jsonRequest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    BitGetUnifiedResponse<string> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new BitGetUnifiedResponse<string>());

                    if (stateResponse.code.Equals("00000") && stateResponse.data.Equals("success"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage($"SetLeverage: {security.Name} >> {responseMessage.Content}", LogMessageType.Error);
                        return;
                    }
                }
                else
                {
                    SendLogMessage($"SetLeverage: {security.Name} >> {responseMessage.Content}", LogMessageType.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"SetLeverage: {security.Name} >> {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
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
