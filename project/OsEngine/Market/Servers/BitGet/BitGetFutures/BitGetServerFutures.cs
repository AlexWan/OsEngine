/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BitGet.BitGetFutures.Entity;
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



namespace OsEngine.Market.Servers.BitGet.BitGetFutures
{
    public class BitGetServerFutures : AServer
    {
        public BitGetServerFutures(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            BitGetServerRealization realization = new BitGetServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterPassphrase, "");
            CreateParameterBoolean("Demo Trading", false);
            CreateParameterBoolean("Extended Data", false);

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label271;
            ServerParameters[3].Comment = OsLocalization.Market.Label250;
            ServerParameters[4].Comment = OsLocalization.Market.Label249;
            ServerParameters[5].Comment = OsLocalization.Market.Label268;
            ServerParameters[6].Comment = OsLocalization.Market.Label270;
        }
    }

    public class BitGetServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BitGetServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Thread threadMessageReaderPublic = new Thread(MessageReaderPublic);
            threadMessageReaderPublic.Name = "MessageReaderPublic";
            threadMessageReaderPublic.Start();

            Thread threadMessageReaderPrivate = new Thread(MessageReaderPrivate);
            threadMessageReaderPrivate.Name = "MessageReaderPrivate";
            threadMessageReaderPrivate.Start();

            Thread thread = new Thread(CheckAliveWebSocket);
            thread.Name = "CheckAliveWebSocket";
            thread.Start();

            Thread threadGetPortfolios = new Thread(ThreadGetPortfolios);
            threadGetPortfolios.Name = "ThreadBitGetFuturesPortfolios";
            threadGetPortfolios.Start();

            Thread threadExtendedData = new Thread(ThreadExtendedData);
            threadExtendedData.Name = "ThreadBitGetFuturesExtendedData";
            threadExtendedData.Start();

            Thread threadMarketDepthParsing = new Thread(ThreadMarketDepthParsing);
            threadMarketDepthParsing.Name = "ThreadMarketDepthParsing";
            threadMarketDepthParsing.Start();

            Thread threadTradesParsing = new Thread(ThreadTradesParsing);
            threadTradesParsing.Name = "ThreadTradesParsing";
            threadTradesParsing.Start();
        }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy = null)
        {
            _myProxy = proxy;
            PublicKey = ((ServerParameterString)ServerParameters[0]).Value;
            SeckretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
            Passphrase = ((ServerParameterPassword)ServerParameters[2]).Value;
            
            if (string.IsNullOrEmpty(PublicKey) ||
                string.IsNullOrEmpty(SeckretKey) ||
                string.IsNullOrEmpty(Passphrase))
            {
                SendLogMessage("Can`t run Bitget Futures connector. No keys or passphrase",
                    LogMessageType.Error);
                return;
            }

            if (((ServerParameterBool)ServerParameters[3]).Value == true)
            {
                _listCoin = new List<string>() { "SUSDT-FUTURES", "SCOIN-FUTURES", "SUSDC-FUTURES" };
            }
            else
            {
                _listCoin = new List<string>() { "USDT-FUTURES", "COIN-FUTURES", "USDC-FUTURES" };
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
                string requestStr = "/api/v2/public/time";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);

                RestClient client = new RestClient(BaseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    CreatePublicWebSocketConnect();
                    CreatePrivateWebSocketConnect();
                    _lastConnectionStartTime = DateTime.Now;
                }
                else
                {
                    SendLogMessage("Connection can be open. BitGet. Error request", LogMessageType.Error);
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

            FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();
            FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();
            _queueMarketDepths = new ConcurrentQueue<string>();
            _queueTrades = new ConcurrentQueue<string>();
            _portfolioIsStarted = false;

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
            get { return ServerType.BitGetFutures; }
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

        private string BaseUrl = "https://api.bitget.com";

        private string PublicKey;

        private string SeckretKey;

        private string Passphrase;

        private int _limitCandlesData = 200;

        //private int _limitCandlesTrader = 1000;

        private List<string> _listCoin;

        private bool _extendedMarketData;

        private Dictionary<string, List<string>> _allPositions = new Dictionary<string, List<string>>();

        #endregion

        #region 3 Securities

        private RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(50));

        public void GetSecurities()
        {
            for (int indCoin = 0; indCoin < _listCoin.Count; indCoin++)
            {
                try
                {
                    _rateGateSecurity.WaitToProceed();

                    string requestStr = $"/api/v2/mix/market/contracts?productType={_listCoin[indCoin]}";
                    RestRequest requestRest = new RestRequest(requestStr, Method.GET);

                    RestClient client = new RestClient(BaseUrl);

                    if (_myProxy != null)
                    {
                        client.Proxy = _myProxy;
                    }

                    IRestResponse response = client.Execute(requestRest);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        ResponseRestMessage<List<RestMessageSymbol>> symbols = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<RestMessageSymbol>>());

                        if (symbols.code.Equals("00000") == true)
                        {
                            List<Security> securities = new List<Security>();

                            if (symbols.data.Count == 0)
                            {
                                continue;
                            }

                            for (int i = 0; i < symbols.data.Count; i++)
                            {
                                RestMessageSymbol item = symbols.data[i];

                                int decimals = Convert.ToInt32(item.pricePlace);
                                decimal priceStep = (GetPriceStep(Convert.ToInt32(item.pricePlace), Convert.ToInt32(item.priceEndStep))).ToDecimal();

                                if (item.symbolStatus.Equals("normal"))
                                {
                                    Security newSecurity = new Security();

                                    newSecurity.Exchange = ServerType.BitGetFutures.ToString();
                                    newSecurity.DecimalsVolume = Convert.ToInt32(item.volumePlace);
                                    newSecurity.Lot = 1;
                                    newSecurity.Name = item.symbol;
                                    newSecurity.NameFull = item.symbol;
                                    newSecurity.NameClass = _listCoin[indCoin];
                                    newSecurity.NameId = item.symbol;
                                    newSecurity.SecurityType = SecurityType.Futures;
                                    newSecurity.Decimals = decimals;
                                    newSecurity.PriceStep = priceStep;
                                    newSecurity.PriceStepCost = priceStep;
                                    newSecurity.State = SecurityStateType.Activ;
                                    newSecurity.MinTradeAmountType = MinTradeAmountType.C_Currency;
                                    newSecurity.MinTradeAmount = item.minTradeUSDT.ToDecimal();

                                    if (newSecurity.DecimalsVolume == 0)
                                    {
                                        newSecurity.VolumeStep = 1;
                                    }
                                    else
                                    {
                                        newSecurity.VolumeStep = item.minTradeNum.ToDecimal();
                                    }

                                    securities.Add(newSecurity);
                                }
                            }

                            SecurityEvent?.Invoke(securities);
                        }
                        else
                        {
                            SendLogMessage($"Securities error: {symbols.code} || msg: {symbols.msg}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"Securities request error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }

                }
                catch (Exception ex)
                {
                    SendLogMessage($"Securities request error: {ex.Message} {ex.StackTrace}" + ex.ToString(), LogMessageType.Error);
                }
            }
        }

        private string GetPriceStep(int PricePlace, int PriceEndStep)
        {
            if (PricePlace == 0)
            {
                return Convert.ToString(PriceEndStep);
            }

            string res = String.Empty;

            for (int i = 0; i < PricePlace; i++)
            {
                if (i == 0)
                {
                    res += "0,";
                }
                else
                {
                    res += "0";
                }
            }

            return res + PriceEndStep;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _portfolios = new List<Portfolio>();

        private RateGate _rateGatePortfolio = new RateGate(1, TimeSpan.FromMilliseconds(100));

        private void ThreadGetPortfolios()
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

                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    if (_portfolioIsStarted == false)
                    {
                        continue;
                    }

                    for (int i = 0; i < _listCoin.Count; i++)
                    {
                        CreatePortfolio(false, _listCoin[i]);
                        CreatePositions(_listCoin[i]);
                    }

                    GetUSDTMasterPortfolio(false);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }
        }

        List<string> resultPositionInUSDT = new List<string>();
        List<string> resultPositionPnL = new List<string>();

        public void GetPortfolios()
        {
            for (int i = 0; i < _listCoin.Count; i++)
            {
                CreatePortfolio(true, _listCoin[i]);
                CreatePositions(_listCoin[i]);
            }

            GetUSDTMasterPortfolio(true);

            _portfolioIsStarted = true;
        }

        private void CreatePortfolio(bool IsUpdateValueBegin, string productType)
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                string path = "/api/v2/mix/account/accounts" + "?productType=" + productType;

                IRestResponse responseMessage = CreatePrivateQuery(path, Method.GET, null, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<Account>> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<Account>>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        Portfolio portfolio = _portfolios.Find(p => p.Number == "BitGetFutures");

                        if (portfolio == null)
                        {
                            Portfolio newPortf = new Portfolio();
                            newPortf.Number = "BitGetFutures";
                            newPortf.ValueBegin = 1;
                            newPortf.ValueCurrent = 1;
                            _portfolios.Add(newPortf);
                            portfolio = newPortf;
                        }

                        decimal positionInUSDT = 0;
                        decimal positionPnL = 0;

                        for (int i = 0; i < stateResponse.data.Count; i++)
                        {
                            Account item = stateResponse.data[i];

                            if (productType == "COIN-FUTURES"
                                && item.marginCoin.ToString() == "USDC")
                            {
                                continue;
                            }

                            if (item.marginCoin.ToString() == "USDT")
                            {
                                positionInUSDT = item.unionTotalMargin.ToDecimal();
                                positionPnL = item.unrealizedPL.ToDecimal();
                            }
                            else
                            {
                                positionInUSDT += item.usdtEquity.ToDecimal();
                                positionPnL += item.unrealizedPL.ToDecimal();
                            }

                            PositionOnBoard pos = new PositionOnBoard();
                            pos.PortfolioName = "BitGetFutures";
                            pos.SecurityNameCode = item.marginCoin.ToString();
                            pos.ValueBlocked = item.locked.ToDecimal();
                            pos.ValueCurrent = item.available.ToDecimal();
                            pos.UnrealizedPnl = Math.Round(item.unrealizedPL.ToDecimal(), 6);

                            if (IsUpdateValueBegin)
                            {
                                pos.ValueBegin = item.accountEquity.ToDecimal();
                            }

                            portfolio.SetNewPosition(pos);
                        }

                        resultPositionInUSDT.Add(positionInUSDT.ToString());
                        resultPositionPnL.Add(positionPnL.ToString());

                        PortfolioEvent?.Invoke(_portfolios);
                    }
                    else
                    {
                        SendLogMessage($"Portfolio error: {stateResponse.code} || msg: {stateResponse.msg}", LogMessageType.Error);
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

        private void CreatePositions(string productType)
        {
            _rateGatePortfolio.WaitToProceed();

            try
            {
                string path = "/api/v2/mix/position/all-position" + "?productType=" + productType;

                IRestResponse responseMessage = CreatePrivateQuery(path, Method.GET, null, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<RestMessagePositions>> positions = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<List<RestMessagePositions>>());

                    if (positions.code.Equals("00000") == true)
                    {
                        if (positions.data == null)
                        {
                            return;
                        }

                        if (_portfolios == null)
                        {
                            return;
                        }

                        Portfolio portfolio = _portfolios[0];

                        if (positions != null)
                        {
                            if (positions.data.Count > 0)
                            {
                                for (int i = 0; i < positions.data.Count; i++)
                                {
                                    RestMessagePositions item = positions.data[i];

                                    PositionOnBoard pos = new PositionOnBoard();
                                    pos.PortfolioName = "BitGetFutures";
                                    pos.SecurityNameCode = item.symbol;

                                    if (item.posMode == "hedge_mode")
                                    {
                                        if (item.holdSide == "long")
                                        {
                                            pos.SecurityNameCode = item.symbol + "_" + "LONG";
                                        }
                                        if (item.holdSide == "short")
                                        {
                                            pos.SecurityNameCode = item.symbol + "_" + "SHORT";
                                        }
                                    }

                                    if (item.holdSide == "long")
                                    {
                                        pos.ValueCurrent = item.available.ToDecimal();
                                    }
                                    else if (item.holdSide == "short")
                                    {
                                        pos.ValueCurrent = item.available.ToDecimal() * -1;
                                    }

                                    pos.ValueBlocked = item.locked.ToDecimal();
                                    pos.UnrealizedPnl = item.unrealizedPL.ToDecimal();

                                    portfolio.SetNewPosition(pos);

                                    if (!_allPositions.ContainsKey(productType))
                                    {
                                        _allPositions.Add(productType, new List<string>());
                                    }

                                    if (!_allPositions[productType].Contains(pos.SecurityNameCode))
                                    {
                                        _allPositions[productType].Add(pos.SecurityNameCode);
                                    }
                                }
                            }

                            if (_allPositions.ContainsKey(productType))
                            {
                                if (_allPositions[productType].Count > 0)
                                {
                                    for (int indAllPos = 0; indAllPos < _allPositions[productType].Count; indAllPos++)
                                    {
                                        bool isInData = false;

                                        if (positions.data.Count > 0)
                                        {
                                            for (int indData = 0; indData < positions.data.Count; indData++)
                                            {
                                                if (positions.data[indData].posMode == "hedge_mode")
                                                {
                                                    if (_allPositions[productType][indAllPos] == positions.data[indData].symbol + "_" + positions.data[indData].holdSide.ToUpper())
                                                    {
                                                        isInData = true;
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    if (_allPositions[productType][indAllPos] == positions.data[indData].symbol)
                                                    {
                                                        isInData = true;
                                                        break;
                                                    }
                                                }
                                            }
                                        }

                                        if (!isInData)
                                        {
                                            PositionOnBoard pos = new PositionOnBoard();
                                            pos.PortfolioName = "BitGetFutures";
                                            pos.SecurityNameCode = _allPositions[productType][indAllPos];
                                            pos.ValueCurrent = 0;
                                            pos.ValueBlocked = 0;

                                            portfolio.SetNewPosition(pos);

                                            _allPositions[productType].RemoveAt(indAllPos);
                                            indAllPos--;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            SendLogMessage("BITGET ERROR. NO POSITIONS IN REQUEST.", LogMessageType.Error);
                        }

                        PortfolioEvent?.Invoke(_portfolios);
                    }
                    else
                    {
                        SendLogMessage($"Positions error: {positions.code} || msg: {positions.msg}", LogMessageType.Error);
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
            decimal portfolioInUSDT = 0;
            decimal portfolioPnL = 0;

            for (int i = 0; i < resultPositionInUSDT.Count; i++)
            {
                portfolioInUSDT += resultPositionInUSDT[i].ToDecimal();
            }

            for (int i = 0; i < resultPositionPnL.Count; i++)
            {
                portfolioPnL += resultPositionPnL[i].ToDecimal();
            }

            Portfolio portfolio = _portfolios[0];

            if (IsUpdateValueBegin)
            {
                portfolio.ValueBegin = Math.Round(portfolioInUSDT, 4);
            }

            portfolio.ValueCurrent = Math.Round(portfolioInUSDT, 4);
            portfolio.UnrealizedPnl = Math.Round(portfolioPnL, 6);

            PortfolioEvent(_portfolios);

            resultPositionInUSDT.Clear();
            resultPositionPnL.Clear();
        }

        private bool _portfolioIsStarted = false;

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

            //int limitCandles = _limitCandlesTrader;
            int limitCandles = _limitCandlesData;

            TimeSpan span = endTime - startTime;

            if (limitCandles > span.TotalMinutes / tfTotalMinutes)
            {
                limitCandles = (int)Math.Round(span.TotalMinutes / tfTotalMinutes, MidpointRounding.AwayFromZero);
            }

            if (tfTotalMinutes == 1440)
            {
                limitCandles = 90;
            }

            List<Candle> allCandles = new List<Candle>();

            DateTime startTimeData = startTime;
            DateTime endTimeData = startTimeData.AddMinutes(tfTotalMinutes * limitCandles);

            do
            {
                long from = TimeManager.GetTimeStampMilliSecondsToDateTime(startTimeData);
                long to = TimeManager.GetTimeStampMilliSecondsToDateTime(endTimeData);

                string interval = GetInterval(timeFrameBuilder.TimeFrameTimeSpan);

                List<Candle> candles = RequestCandleHistory(security, interval, from, to, limitCandles);

                if (candles == null || candles.Count == 0)
                {
                    break;
                }

                Candle last = candles[candles.Count - 1];

                if (allCandles.Count > 0)
                {
                    if (allCandles[allCandles.Count - 1].TimeStart == candles[0].TimeStart)
                    {
                        candles.RemoveAt(0);
                    }
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

                startTimeData = endTimeData;
                endTimeData = startTimeData.AddMinutes(tfTotalMinutes * limitCandles);

                if (startTimeData >= endTime)
                {
                    break;
                }

                if (endTimeData > endTime)
                {
                    endTimeData = endTime;
                }

                span = endTimeData - startTimeData;

                if (limitCandles > span.TotalMinutes / tfTotalMinutes)
                {
                    limitCandles = (int)Math.Round(span.TotalMinutes / tfTotalMinutes, MidpointRounding.AwayFromZero);
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
                timeFrameMinutes == 3 ||
                timeFrameMinutes == 5 ||
                timeFrameMinutes == 15 ||
                timeFrameMinutes == 30 ||
                timeFrameMinutes == 60 ||
                timeFrameMinutes == 240
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

        private List<Candle> RequestCandleHistory(Security security, string interval, long startTime, long endTime, int limitCandles)
        {
            _rgCandleData.WaitToProceed();

            // string stringUrl = "/api/v2/mix/market/candles";
            string stringUrl = "/api/v2/mix/market/history-candles";

            try
            {
                string requestStr = $"{stringUrl}?symbol={security.Name}&productType={security.NameClass.ToLower()}&" +
                    $"startTime={startTime}&granularity={interval}&limit={limitCandles}&endTime={endTime}";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);

                RestClient client = new RestClient(BaseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    RestMessageCandle responseCandles = JsonConvert.DeserializeObject<RestMessageCandle>(response.Content);

                    if (responseCandles.code.Equals("00000") == true)
                    {
                        return ConvertCandles(responseCandles);
                    }
                    else
                    {
                        SendLogMessage($"Candle history error: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }
                }
                else
                {
                    if (response.ToString().StartsWith("<!DOCTYPE") == false)
                    {
                        SendLogMessage($"Candle history error. Code: {response.StatusCode} || msg: {response.Content}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Candle> ConvertCandles(RestMessageCandle responseCandles)
        {
            List<Candle> candles = new List<Candle>();

            if (responseCandles.data.Count == 0)
            {
                return null;
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

        private readonly RateGate _rgTickData = new RateGate(1, TimeSpan.FromMilliseconds(110));

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
            endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            actualTime = DateTime.SpecifyKind(actualTime, DateTimeKind.Utc);

            if (startTime < DateTime.UtcNow.AddDays(-90))
            {
                SendLogMessage("History more than 90 days is not supported by API", LogMessageType.Error);
                return null;
            }

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }

            List<Trade> trades = new List<Trade>();

            List<Trade> newTrades = GetTickHistoryToSecurity(security, endTime, startTime);

            if (newTrades == null ||
                    newTrades.Count == 0)
            {
                return null;
            }

            trades.AddRange(newTrades);
            DateTime timeEnd = DateTime.SpecifyKind(trades[0].Time, DateTimeKind.Utc);

            while (timeEnd > startTime)
            {
                newTrades = GetTickHistoryToSecurity(security, timeEnd, startTime);

                if (newTrades != null && trades.Count != 0 && newTrades.Count != 0)
                {
                    for (int j = 0; j < trades.Count; j++)
                    {
                        for (int i = 0; i < newTrades.Count; i++)
                        {
                            if (trades[j].Id == newTrades[i].Id)
                            {
                                newTrades.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                }

                if (newTrades.Count == 0)
                {
                    break;
                }

                trades.InsertRange(0, newTrades);
                timeEnd = DateTime.SpecifyKind(trades[0].Time, DateTimeKind.Utc);
            }

            if (trades.Count == 0)
            {
                return null;
            }

            for (int i = trades.Count - 1; i >= 0; i--)
            {
                if (DateTime.SpecifyKind(trades[i].Time, DateTimeKind.Utc) <= endTime)
                {
                    break;
                }
                else
                {
                    trades.RemoveAt(i);
                }
            }

            return trades;
        }

        private List<Trade> GetTickHistoryToSecurity(Security security, DateTime endTime, DateTime startTime)
        {
            _rgTickData.WaitToProceed();

            try
            {
                List<Trade> trades = new List<Trade>();

                long timeEnd = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);
                long timeStart = TimeManager.GetTimeStampMilliSecondsToDateTime(startTime);

                string requestStr = $"/api/v2/mix/market/fills-history?symbol={security.Name}&productType={security.NameClass.ToLower()}&" +
                    $"limit=1000&endTime={timeEnd}&startTime={timeStart}";

                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(BaseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<TradeData>> tradesResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<TradeData>>());

                    if (tradesResponse.code == "00000")
                    {
                        for (int i = 0; i < tradesResponse.data.Count; i++)
                        {
                            TradeData item = tradesResponse.data[i];

                            Trade trade = new Trade();
                            trade.SecurityNameCode = item.symbol;
                            trade.Id = item.tradeId;
                            trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.ts));
                            trade.Price = item.price.ToDecimal();
                            trade.Volume = item.size.ToDecimal();
                            trade.Side = item.side == "Sell" ? Side.Sell : Side.Buy;
                            trades.Add(trade);
                        }

                        trades.Reverse();
                        return trades;
                    }
                    else
                    {
                        SendLogMessage($"Trades request error: {tradesResponse.code} - {tradesResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage($"Trades request error: {response.StatusCode} - {response.Content}", LogMessageType.Error);
                }
            }
            catch (Exception error)
            {
                SendLogMessage($"Trades request error: {error.Message} {error.StackTrace}", LogMessageType.Error);
            }

            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private string _webSocketUrlPublic = "wss://ws.bitget.com/v2/ws/public";

        private string _webSocketUrlPrivate = "wss://ws.bitget.com/v2/ws/private";

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
                /* _webSocketPrivate.SslConfiguration.EnabledSslProtocols
                     = System.Security.Authentication.SslProtocols.Tls12
                    | System.Security.Authentication.SslProtocols.Tls13;*/
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
                }
            }
        }

        private void CreateAuthMessageWebSocekt()
        {
            try
            {
                string TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                string Sign = GenerateSignature(TimeStamp, "GET", "/user/verify", null, null, SeckretKey);

                RequestWebsocketAuth requestWebsocketAuth = new RequestWebsocketAuth();

                requestWebsocketAuth.op = "login";
                requestWebsocketAuth.args = new List<AuthItem>();
                requestWebsocketAuth.args.Add(new AuthItem());
                requestWebsocketAuth.args[0].apiKey = PublicKey;
                requestWebsocketAuth.args[0].passphrase = Passphrase;
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
                    SendLogMessage("Bitget WebSocket Public connection open", LogMessageType.System);
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

                if (FIFOListWebSocketPublicMessage == null)
                {
                    return;
                }

                FIFOListWebSocketPublicMessage.Enqueue(e.Data);
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
                SendLogMessage("Bitget WebSocket Private connection open", LogMessageType.System);
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

                if (FIFOListWebSocketPrivateMessage == null)
                {
                    return;
                }

                FIFOListWebSocketPrivateMessage.Enqueue(e.Data);
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
                CreateSubscribeSecurityMessageWebSocket(security);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateSubscribeSecurityMessageWebSocket(Security security)
        {
            try
            {
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
                    && _subscribedSecutiries.Count % 30 == 0)
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
                    webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"{security.NameClass}\",\"channel\": \"books15\",\"instId\": \"{security.Name}\"}}]}}");
                    webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{ \"instType\": \"{security.NameClass}\",\"channel\": \"trade\",\"instId\": \"{security.Name}\"}}]}}");

                    if (_extendedMarketData)
                    {
                        webSocketPublic.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{ \"instType\": \"{security.NameClass}\",\"channel\": \"ticker\",\"instId\": \"{security.Name}\"}}]}}");
                        GetFundingData(security.Name, security.NameClass);
                        GetFundingHistory(security.Name, security.NameClass);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private readonly RateGate _rgFunding = new RateGate(1, TimeSpan.FromMilliseconds(110));

        private void GetFundingData(string securityName, string productType)
        {
            _rgFunding.WaitToProceed();

            try
            {
                string requestStr = $"/api/v2/mix/market/current-fund-rate?symbol={securityName}&productType={productType}";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(BaseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<FundingItem>> responseFunding = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<FundingItem>>());

                    if (responseFunding.code == "00000")
                    {
                        FundingItem item = responseFunding.data[0];

                        Funding data = new Funding();

                        data.SecurityNameCode = item.symbol;
                        data.MaxFundingRate = item.maxFundingRate.ToDecimal();
                        data.MinFundingRate = item.minFundingRate.ToDecimal();
                        data.FundingIntervalHours = int.Parse(item.fundingRateInterval);

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

        private void GetFundingHistory(string securityName, string productType)
        {
            _rgFunding.WaitToProceed();

            try
            {
                string requestStr = $"/api/v2/mix/market/history-fund-rate?symbol={securityName}&productType={productType}";
                RestRequest requestRest = new RestRequest(requestStr, Method.GET);
                RestClient client = new RestClient(BaseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<List<FundingItemHistory>> responseFunding = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<List<FundingItemHistory>>());

                    if (responseFunding.code == "00000")
                    {
                        FundingItemHistory item = responseFunding.data[0];

                        Funding data = new Funding();

                        data.SecurityNameCode = item.symbol;
                        data.PreviousFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)item.fundingTime.ToDecimal());

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
                for (int i = 0; i < _listCoin.Count; i++)
                {
                    _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"{_listCoin[i]}\",\"channel\": \"account\",\"coin\": \"default\"}}]}}");
                    _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"{_listCoin[i]}\",\"channel\": \"positions\",\"coin\": \"default\"}}]}}");
                    _webSocketPrivate.SendAsync($"{{\"op\": \"subscribe\",\"args\": [{{\"instType\": \"{_listCoin[i]}\",\"channel\": \"orders\"}}]}}");
                }
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
                                        string nameClass = _subscribedSecutiries[i2].NameClass;
                                        string name = _subscribedSecutiries[i2].Name;

                                        argsList.Add($"{{\"instType\":\"{nameClass}\",\"channel\":\"books15\",\"instId\":\"{name}\"}}");
                                        argsList.Add($"{{\"instType\":\"{nameClass}\",\"channel\":\"trade\",\"instId\":\"{name}\"}}");

                                        if (_extendedMarketData)
                                        {
                                            argsList.Add($"{{\"instType\":\"{nameClass}\",\"channel\":\"ticker\",\"instId\":\"{name}\"}}");
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
                    List<string> privateArgsList = new List<string>();

                    for (int i = 0; i < _listCoin.Count; i++)
                    {
                        string instType = _listCoin[i];

                        privateArgsList.Add($"{{\"instType\":\"{instType}\",\"channel\":\"account\",\"coin\":\"default\"}}");
                        privateArgsList.Add($"{{\"instType\":\"{instType}\",\"channel\":\"positions\",\"coin\":\"default\"}}");
                        privateArgsList.Add($"{{\"instType\":\"{instType}\",\"channel\":\"orders\",\"coin\":\"default\"}}");
                    }

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

        #region 10 WebSocket parsing the messages

        private ConcurrentQueue<string> FIFOListWebSocketPublicMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueMarketDepths = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _queueTrades = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> FIFOListWebSocketPrivateMessage = new ConcurrentQueue<string>();

        private void MessageReaderPublic()
        {
            while (true)
            {
                try
                {
                    if (FIFOListWebSocketPublicMessage.IsEmpty)
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

                        FIFOListWebSocketPublicMessage.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.Contains("books15"))
                        {
                            _queueMarketDepths.Enqueue(message);
                            continue;
                        }
                        else if (message.Contains("trade"))
                        {
                            _queueTrades.Enqueue(message);
                            continue;
                        }
                        else if (message.Contains("ticker"))
                        {
                            UpdateTicker(message);
                            continue;
                        }
                        else
                        {
                            if (message.StartsWith("{\"event\":\"error\""))
                            {
                                SendLogMessage("WebSocket listener error", LogMessageType.Error);
                                SendLogMessage(message, LogMessageType.Error);

                                if (_lastConnectionStartTime.AddMinutes(5) > DateTime.Now)
                                { // if there are problems with the web socket startup, you need to restart it
                                    Disconnect();
                                }
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
                    Thread.Sleep(3000);
                }
            }
        }

        private void MessageReaderPrivate()
        {
            while (true)
            {
                try
                {
                    if (FIFOListWebSocketPrivateMessage.IsEmpty)
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

                        FIFOListWebSocketPrivateMessage.TryDequeue(out message);

                        if (message == null)
                        {
                            continue;
                        }

                        if (message.Contains("account"))
                        {
                            UpdateAccount(message);
                            continue;
                        }
                        else if (message.Contains("positions"))
                        {
                            UpdatePositions(message);
                            continue;
                        }
                        else if (message.Contains("orders"))
                        {
                            UpdateOrder(message);
                            continue;
                        }
                        else
                        {
                            if (message.StartsWith("{\"event\":\"error\""))
                            {
                                SendLogMessage("WebSocket listener error", LogMessageType.Error);
                                SendLogMessage(message, LogMessageType.Error);

                                if (_lastConnectionStartTime.AddMinutes(5) > DateTime.Now)
                                { // if there are problems with the web socket startup, you need to restart it
                                    Disconnect();
                                }
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

        private void UpdatePositions(string message)
        {
            if (_portfolioIsStarted == false)
            {
                return;
            }

            try
            {
                ResponseWebSocketMessageAction<List<ResponseMessagePositions>> positions = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseMessagePositions>>());

                if (positions.data == null)
                {
                    return;
                }

                if (_portfolios == null)
                {
                    return;
                }

                Portfolio portfolio = _portfolios[0];

                if (positions != null)
                {
                    if (positions.data.Count > 0)
                    {
                        for (int i = 0; i < positions.data.Count; i++)
                        {
                            PositionOnBoard pos = new PositionOnBoard();
                            pos.PortfolioName = "BitGetFutures";
                            pos.SecurityNameCode = positions.data[i].instId;

                            if (positions.data[i].posMode == "hedge_mode")
                            {
                                if (positions.data[i].holdSide == "long")
                                {
                                    pos.SecurityNameCode = positions.data[i].instId + "_" + "LONG";
                                }
                                if (positions.data[i].holdSide == "short")
                                {
                                    pos.SecurityNameCode = positions.data[i].instId + "_" + "SHORT";
                                }
                            }

                            if (positions.data[i].holdSide == "long")
                            {
                                pos.ValueCurrent = positions.data[i].available.ToDecimal();
                            }
                            else if (positions.data[i].holdSide == "short")
                            {
                                pos.ValueCurrent = positions.data[i].available.ToDecimal() * -1;
                            }

                            pos.ValueBlocked = positions.data[i].frozen.ToDecimal();
                            pos.UnrealizedPnl = positions.data[i].unrealizedPL.ToDecimal();

                            portfolio.SetNewPosition(pos);

                            if (!_allPositions.ContainsKey(positions.arg.instType))
                            {
                                _allPositions.Add(positions.arg.instType, new List<string>());
                            }

                            if (!_allPositions[positions.arg.instType].Contains(pos.SecurityNameCode))
                            {
                                _allPositions[positions.arg.instType].Add(pos.SecurityNameCode);
                            }
                        }
                    }

                    if (_allPositions.ContainsKey(positions.arg.instType))
                    {
                        if (_allPositions[positions.arg.instType].Count > 0)
                        {
                            for (int indAllPos = 0; indAllPos < _allPositions[positions.arg.instType].Count; indAllPos++)
                            {
                                bool isInData = false;

                                if (positions.data.Count > 0)
                                {
                                    for (int indData = 0; indData < positions.data.Count; indData++)
                                    {
                                        if (positions.data[indData].posMode == "hedge_mode")
                                        {
                                            if (_allPositions[positions.arg.instType][indAllPos] == positions.data[indData].instId + "_" + positions.data[indData].holdSide.ToUpper())
                                            {
                                                isInData = true;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            if (_allPositions[positions.arg.instType][indAllPos] == positions.data[indData].instId)
                                            {
                                                isInData = true;
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (!isInData)
                                {
                                    PositionOnBoard pos = new PositionOnBoard();
                                    pos.PortfolioName = "BitGetFutures";
                                    pos.SecurityNameCode = _allPositions[positions.arg.instType][indAllPos];
                                    pos.ValueCurrent = 0;
                                    pos.ValueBlocked = 0;

                                    portfolio.SetNewPosition(pos);

                                    _allPositions[positions.arg.instType].RemoveAt(indAllPos);
                                    indAllPos--;
                                }
                            }
                        }
                    }
                }
                else
                {
                    SendLogMessage("BITGET ERROR. NO POSITIONS IN REQUEST.", LogMessageType.Error);
                }

                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private void UpdateAccount(string message)
        {
            if (_portfolioIsStarted == false)
            {
                return;
            }

            try
            {
                ResponseWebSocketMessageAction<List<ResponseWebSocketAccount>> assets = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebSocketAccount>>());

                if (assets.data == null ||
                    assets.data.Count == 0)
                {
                    return;
                }

                if (_portfolios == null)
                {
                    return;
                }

                Portfolio portfolio = _portfolios[0];

                for (int i = 0; i < assets.data.Count; i++)
                {
                    PositionOnBoard pos = new PositionOnBoard();

                    pos.PortfolioName = "BitGetFutures";
                    pos.SecurityNameCode = assets.data[i].marginCoin;
                    pos.ValueBlocked = assets.data[i].frozen.ToDecimal();
                    pos.ValueCurrent = assets.data[i].available.ToDecimal();

                    portfolio.SetNewPosition(pos);
                }

                PortfolioEvent?.Invoke(_portfolios);
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
                ResponseWebSocketMessageAction<List<ResponseWebSocketOrder>> order = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebSocketOrder>>());

                if (order.data == null ||
                    order.data.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < order.data.Count; i++)
                {
                    ResponseWebSocketOrder item = order.data[i];

                    if (string.IsNullOrEmpty(item.orderId))
                    {
                        continue;
                    }

                    OrderStateType stateType = GetOrderState(item.status);

                    if (item.orderType.Equals("market") &&
                        stateType != OrderStateType.Done &&
                        stateType != OrderStateType.Partial)
                    {
                        continue;
                    }

                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = item.instId;
                    newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
                    int.TryParse(item.clientOId, out newOrder.NumberUser);
                    newOrder.NumberMarket = item.orderId.ToString();
                    newOrder.Side = GetSide(item.tradeSide, item.side);
                    newOrder.State = stateType;
                    newOrder.Volume = item.size.ToDecimal();
                    newOrder.Price = item.price.ToDecimal();
                    newOrder.ServerType = ServerType.BitGetFutures;
                    newOrder.PortfolioNumber = "BitGetFutures";
                    newOrder.SecurityClassCode = order.arg.instType.ToString();

                    if (item.orderType.Equals("market"))
                    {
                        newOrder.TypeOrder = OrderPriceType.Market;
                    }
                    else
                    {
                        newOrder.TypeOrder = OrderPriceType.Limit;
                    }

                    if (stateType == OrderStateType.Partial)
                    {
                        MyOrderEvent?.Invoke(newOrder);

                        MyTrade myTrade = new MyTrade();
                        myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.fillTime));
                        myTrade.NumberOrderParent = item.orderId.ToString();
                        myTrade.NumberTrade = item.tradeId;
                        myTrade.Volume = item.baseVolume.ToDecimal();
                        myTrade.Price = item.fillPrice.ToDecimal();
                        myTrade.SecurityNameCode = item.instId;
                        myTrade.Side = GetSide(item.tradeSide, item.side);

                        MyTradeEvent?.Invoke(myTrade);

                        return;
                    }
                    else if (stateType == OrderStateType.Done)
                    {
                        MyOrderEvent?.Invoke(newOrder);

                        MyTrade myTrade = new MyTrade();
                        myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.fillTime));
                        myTrade.NumberOrderParent = item.orderId.ToString();
                        myTrade.NumberTrade = item.tradeId;
                        myTrade.Volume = item.baseVolume.ToDecimal();

                        if (myTrade.Volume > 0)
                        {
                            myTrade.Price = item.fillPrice.ToDecimal();
                            myTrade.SecurityNameCode = item.instId;
                            myTrade.Side = GetSide(item.tradeSide, item.side);

                            MyTradeEvent?.Invoke(myTrade);
                        }

                        return;
                    }
                    else
                    {
                        MyOrderEvent?.Invoke(newOrder);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private Side GetSide(string tradeSide, string side)
        {
            if (tradeSide == "close")
            {
                return side == "buy" ? Side.Sell : Side.Buy;
            }
            return side == "buy" ? Side.Buy : Side.Sell;
        }

        private void UpdateTrade(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<List<ResponseWebsocketTrade>> responseTrade = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebsocketTrade>>());

                if (responseTrade == null
                    || responseTrade.data == null)
                {
                    return;
                }

                for (int i = responseTrade.data.Count - 1; i >= 0; i--)
                {
                    Trade trade = new Trade();
                    trade.SecurityNameCode = responseTrade.arg.instId;
                    trade.Price = responseTrade.data[i].price.ToDecimal();
                    trade.Id = responseTrade.data[i].tradeId;

                    if (trade.Id == null)
                    {
                        return;
                    }

                    trade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseTrade.data[i].ts));
                    trade.Volume = responseTrade.data[i].size.ToDecimal();
                    trade.Side = responseTrade.data[i].side.Equals("buy") ? Side.Buy : Side.Sell;

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

        private MarketDepth UpdateDepth(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<List<ResponseWebSocketDepthItem>> responseDepth = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseWebSocketDepthItem>>());

                if (responseDepth.data == null)
                {
                    return null;
                }

                if (responseDepth.data[0].asks.Count == 0 && responseDepth.data[0].bids.Count == 0)
                {
                    return null;
                }

                MarketDepth marketDepth = new MarketDepth();

                List<MarketDepthLevel> ascs = new List<MarketDepthLevel>();
                List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

                marketDepth.SecurityNameCode = responseDepth.arg.instId;

                for (int i = 0; i < responseDepth.data[0].asks.Count; i++)
                {
                    List<string> askList = responseDepth.data[0].asks[i];

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

                for (int i = 0; i < responseDepth.data[0].bids.Count; i++)
                {
                    List<string> bidList = responseDepth.data[0].bids[i];

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

        private void UpdateTicker(string message)
        {
            try
            {
                ResponseWebSocketMessageAction<List<ResponseTicker>> responseTicker = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageAction<List<ResponseTicker>>());

                if (responseTicker == null
                    || responseTicker.data == null
                    || responseTicker.data[0] == null)
                {
                    return;
                }

                Funding funding = new Funding();

                ResponseTicker item = responseTicker.data[0];

                funding.SecurityNameCode = item.instId;
                funding.CurrentValue = item.fundingRate.ToDecimal() * 100;
                funding.NextFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)item.nextFundingTime.ToDecimal());
                funding.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)responseTicker.ts.ToDecimal());

                FundingUpdateEvent?.Invoke(funding);

                SecurityVolumes volume = new SecurityVolumes();

                volume.SecurityNameCode = item.instId;
                volume.Volume24h = item.baseVolume.ToDecimal();
                volume.Volume24hUSDT = item.quoteVolume.ToDecimal();

                Volume24hUpdateEvent?.Invoke(volume);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private DateTime _lastTimeMd = DateTime.MinValue;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent;

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateOrder = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public void SendOrder(Order order)
        {
            try
            {
                string trSide = "open";
                string posSide;

                bool hedgeMode = GetHedgeModeFromSettings(order.SecurityClassCode);

                if (hedgeMode)
                {
                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        trSide = "close";
                        posSide = order.Side == Side.Buy ? "sell" : "buy";
                    }
                    else
                    {
                        trSide = "open";
                        posSide = order.Side == Side.Buy ? "buy" : "sell";
                    }
                }
                else
                {
                    posSide = order.Side == Side.Buy ? "buy" : "sell";
                }

                _rateGateOrder.WaitToProceed();

                Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>();

                jsonContent.Add("symbol", order.SecurityNameCode);
                jsonContent.Add("productType", order.SecurityClassCode.ToLower());
                jsonContent.Add("marginMode", GetMarginModeFromSettings(order.SecurityNameCode, order.SecurityClassCode));

                if (order.SecurityClassCode == "COIN-FUTURES")
                {
                    string securityName = order.SecurityNameCode.Substring(0, order.SecurityNameCode.IndexOf("USD"));
                    jsonContent.Add("marginCoin", securityName);
                }
                else
                {
                    jsonContent.Add("marginCoin", order.SecurityClassCode.Split('-')[0]);
                }

                jsonContent.Add("side", posSide);
                jsonContent.Add("orderType", order.TypeOrder.ToString().ToLower());
                jsonContent.Add("price", order.Price.ToString().Replace(",", "."));
                jsonContent.Add("size", order.Volume.ToString().Replace(",", "."));
                jsonContent.Add("clientOid", order.NumberUser);

                if (hedgeMode)
                {
                    jsonContent.Add("tradeSide", trSide);
                }

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                IRestResponse responseMessage = CreatePrivateQueryOrders("/api/v2/mix/order/place-order", Method.POST, null, jsonRequest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<object>());

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

        public bool CancelOrder(Order order)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("symbol", order.SecurityNameCode);
                jsonContent.Add("productType", order.SecurityClassCode.ToLower());
                jsonContent.Add("orderId", order.NumberMarket);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                IRestResponse response = CreatePrivateQueryOrders("/api/v2/mix/order/cancel-order", Method.POST, null, jsonRequest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<object>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        return true;
                        // ignore
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

            ordersOpenAll = ordersOpenAll.OrderByDescending(order => order.TimeCreate).ToList();

            return ordersOpenAll;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                string path = "/api/v2/mix/order/detail?symbol=" + order.SecurityNameCode + "&productType=" + order.SecurityClassCode + "&clientOid=" + order.NumberUser;

                IRestResponse responseMessage = CreatePrivateQuery(path, Method.GET, null, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<DataOrderStatus> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<DataOrderStatus>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        Order newOrder = new Order();

                        OrderStateType stateType = GetOrderState(stateResponse.data.state);

                        newOrder.SecurityNameCode = stateResponse.data.symbol;
                        newOrder.SecurityClassCode = stateResponse.data.marginCoin + "-FUTURES";
                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(stateResponse.data.cTime));
                        int.TryParse(stateResponse.data.clientOid, out newOrder.NumberUser);
                        newOrder.NumberMarket = stateResponse.data.orderId.ToString();
                        newOrder.Side = stateResponse.data.side == "buy" ? Side.Buy : Side.Sell;
                        newOrder.State = stateType;
                        newOrder.Volume = stateResponse.data.size.ToDecimal();
                        newOrder.Price = stateResponse.data.price.ToDecimal();
                        newOrder.ServerType = ServerType.BitGetFutures;
                        newOrder.PortfolioNumber = "BitGetFutures";
                        newOrder.TypeOrder = stateResponse.data.orderType == "limit" ? OrderPriceType.Limit : OrderPriceType.Market;

                        if (newOrder != null
                            && MyOrderEvent != null)
                        {
                            MyOrderEvent(newOrder);
                        }

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
                    SendLogMessage($"Order status error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
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
                _rateGateOrder.WaitToProceed();

                string path = $"/api/v2/mix/order/fills?symbol={order.SecurityNameCode}&productType={order.SecurityClassCode}";

                IRestResponse responseMessage = CreatePrivateQuery(path, Method.GET, null, null);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    RestMyTradesResponce stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new RestMyTradesResponce());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        for (int i = 0; i < stateResponse.data.fillList.Count; i++)
                        {
                            FillList item = stateResponse.data.fillList[i];

                            MyTrade myTrade = new MyTrade();
                            myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
                            myTrade.NumberOrderParent = item.orderId.ToString();
                            myTrade.NumberTrade = item.tradeId;
                            myTrade.Volume = item.baseVolume.ToDecimal();
                            myTrade.Price = item.price.ToDecimal();
                            myTrade.SecurityNameCode = item.symbol.ToUpper();
                            myTrade.Side = item.side == "buy" ? Side.Buy : Side.Sell;

                            MyTradeEvent?.Invoke(myTrade);
                        }
                    }
                    else
                    {
                        SendLogMessage($"My trades request error: {stateResponse.code} || msg: {stateResponse.msg}", LogMessageType.Error);
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

        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("symbol", security.Name);
                jsonContent.Add("productType", security.NameClass);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                CreatePrivateQueryOrders("/api/v2/mix/order/cancel-all-orders", Method.POST, null, jsonRequest);
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
                _rateGateOrder.WaitToProceed();

                for (int i = 0; i < _listCoin.Count; i++)
                {
                    Dictionary<string, string> jsonContent = new Dictionary<string, string>();
                    jsonContent.Add("productType", _listCoin[i]);

                    string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                    CreatePrivateQueryOrders("/api/v2/mix/order/cancel-all-orders", Method.POST, null, jsonRequest);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Cancel all orders error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public void GetAllOpenOrders(List<Order> array, int maxCount)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                List<Order> orders = new List<Order>();

                for (int i = 0; i < _listCoin.Count; i++)
                {
                    string requestPath = "/api/v2/mix/order/orders-pending";
                    requestPath += $"?productType={_listCoin[i]}&";
                    requestPath += $"limit=100";
                    //requestPath += $"clientOrderId={order.NumberUser.ToString()}";

                    IRestResponse responseMessage = CreatePrivateQuery(requestPath, Method.GET, null, null);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        ResponseRestMessage<RestMessageOrders> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<RestMessageOrders>());

                        if (stateResponse.code.Equals("00000") == true)
                        {
                            if (stateResponse.data.entrustedList == null)
                            {
                                continue;
                            }

                            for (int ind = 0; ind < stateResponse.data.entrustedList.Count; ind++)
                            {
                                Order curOder = ConvertRestToOrder(stateResponse.data.entrustedList[ind]);
                                orders.Add(curOder);
                            }
                        }
                        else
                        {
                            SendLogMessage($"Open orders error: {stateResponse.code} || msg: {stateResponse.msg}", LogMessageType.Error);
                            return;
                        }
                    }
                    else
                    {
                        SendLogMessage($"All open orders error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                        return;
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
            catch (Exception ex)
            {
                SendLogMessage($"All open orders error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return;
            }
        }

        private Order ConvertRestToOrder(EntrustedList item)
        {
            Order newOrder = new Order();

            OrderStateType stateType = GetOrderState(item.status);

            newOrder.SecurityNameCode = item.symbol;
            newOrder.SecurityClassCode = item.marginCoin + "-FUTURES";
            newOrder.State = stateType;
            newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.cTime));
            newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.uTime));

            if (newOrder.State == OrderStateType.Cancel)
            {
                newOrder.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.uTime));
            }

            if (newOrder.State == OrderStateType.Done)
            {
                newOrder.TimeDone = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(item.uTime));
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
            newOrder.Volume = item.size.ToDecimal();
            newOrder.Price = item.price != "" ? item.price.ToDecimal() : 0;
            newOrder.ServerType = ServerType.BitGetFutures;
            newOrder.PortfolioNumber = "BitGetFutures";
            newOrder.TypeOrder = item.orderType == "limit" ? OrderPriceType.Limit : OrderPriceType.Market;

            return newOrder;
        }

        private OrderStateType GetOrderState(string orderStateResponse)
        {
            OrderStateType stateType;

            switch (orderStateResponse)
            {
                case ("live"):
                    stateType = OrderStateType.Active;
                    break;
                case ("partially_filled"):
                    stateType = OrderStateType.Partial;
                    break;
                case ("filled"):
                    stateType = OrderStateType.Done;
                    break;
                case ("canceled"):
                    stateType = OrderStateType.Cancel;
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

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
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

            ordersOpenAll = ordersOpenAll.OrderByDescending(order => order.TimeCreate).ToList();

            return ordersOpenAll;
        }

        private void GetAllHistoricalOrders(List<Order> array, int maxCount)
        {
            try
            {
                _rateGateOrder.WaitToProceed();

                List<Order> orders = new List<Order>();

                for (int i = 0; i < _listCoin.Count; i++)
                {
                    string requestPath = "/api/v2/mix/order/orders-history";
                    requestPath += $"?productType={_listCoin[i]}&";
                    requestPath += $"limit=100";
                    //requestPath += $"clientOrderId={order.NumberUser.ToString()}";

                    IRestResponse responseMessage = CreatePrivateQuery(requestPath, Method.GET, null, null);

                    if (responseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        ResponseRestMessage<RestMessageOrders> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<RestMessageOrders>());

                        if (stateResponse.code.Equals("00000") == true)
                        {
                            if (stateResponse.data.entrustedList == null)
                            {
                                continue;
                            }

                            for (int ind = 0; ind < stateResponse.data.entrustedList.Count; ind++)
                            {
                                Order curOder = ConvertRestToOrder(stateResponse.data.entrustedList[ind]);

                                orders.Add(curOder);
                            }
                        }
                        else
                        {
                            SendLogMessage($"All historical orders error: {stateResponse.code} || msg: {stateResponse.msg}", LogMessageType.Error);
                            return;
                        }
                    }
                    else
                    {
                        SendLogMessage($"All historical orders error. Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                        return;
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
            catch (Exception ex)
            {
                SendLogMessage($"All historical orders error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return;
            }
        }

        #endregion

        #region 12 Queries

        private IRestResponse CreatePrivateQuery(string path, Method method, string queryString, string body)
        {
            try
            {
                RestRequest requestRest = new RestRequest(path, method);

                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature(timestamp, method.ToString(), path, queryString, body, SeckretKey);

                requestRest.AddHeader("ACCESS-KEY", PublicKey);
                requestRest.AddHeader("ACCESS-SIGN", signature);
                requestRest.AddHeader("ACCESS-TIMESTAMP", timestamp);
                requestRest.AddHeader("ACCESS-PASSPHRASE", Passphrase);
                requestRest.AddHeader("X-CHANNEL-API-CODE", "6yq7w");

                RestClient client = new RestClient(BaseUrl);

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

        private IRestResponse CreatePrivateQueryOrders(string path, Method method, string queryString, string body)
        {
            try
            {
                RestRequest requestRest = new RestRequest(path, method);

                string requestPath = path;
                string url = $"{BaseUrl}{requestPath}";
                string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                string signature = GenerateSignature(timestamp, method.ToString(), requestPath, queryString, body, SeckretKey);

                requestRest.AddHeader("ACCESS-KEY", PublicKey);
                requestRest.AddHeader("ACCESS-SIGN", signature);
                requestRest.AddHeader("ACCESS-TIMESTAMP", timestamp);
                requestRest.AddHeader("ACCESS-PASSPHRASE", Passphrase);
                requestRest.AddHeader("X-CHANNEL-API-CODE", "6yq7w");

                if (method.ToString().Equals("POST"))
                {
                    requestRest.AddParameter("application/json", body, ParameterType.RequestBody);
                }

                RestClient client = new RestClient(BaseUrl);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                return response;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Create private query orders error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
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

        private List<OpenInterestData> _openInterest = new List<OpenInterestData>();

        private DateTime _timeLastUpdateExtendedData = DateTime.Now;

        private readonly RateGate _rgOpenInterest = new RateGate(1, TimeSpan.FromMilliseconds(110));

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
                    string requestStr = $"/api/v2/mix/market/open-interest?symbol={_subscribedSecutiries[i].Name}&productType={_subscribedSecutiries[i].NameClass.ToLower()}";
                    RestRequest requestRest = new RestRequest(requestStr, Method.GET);

                    RestClient client = new RestClient(BaseUrl);

                    if (_myProxy != null)
                    {
                        client.Proxy = _myProxy;
                    }

                    IRestResponse response = client.Execute(requestRest);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        ResponseRestMessage<OIData> oiResponse = JsonConvert.DeserializeAnonymousType(response.Content, new ResponseRestMessage<OIData>());

                        if (oiResponse.code == "00000")
                        {
                            for (int j = 0; j < oiResponse.data.openInterestList.Count; j++)
                            {
                                OpenInterestData openInterestData = new OpenInterestData();

                                openInterestData.SecutityName = oiResponse.data.openInterestList[j].symbol;

                                if (oiResponse.data.openInterestList[j].size != null)
                                {
                                    openInterestData.OpenInterestValue = oiResponse.data.openInterestList[j].size;

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

        public void SetCommonLeverage(string selectedClass, string leverage) { }

        public void SetCommonHedgeMode(string selectedClass, string hedgeMode)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            bool mode = false;

            if (hedgeMode == "On")
            {
                mode = true;
            }

            try
            {
                Dictionary<string, string> jsonContent = new Dictionary<string, string>();

                jsonContent.Add("posMode", mode == true ? "hedge_mode" : "one_way_mode");
                jsonContent.Add("productType", selectedClass);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                IRestResponse responseMessage = CreatePrivateQueryOrders("/api/v2/mix/account/set-position-mode", Method.POST, null, jsonRequest);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<object>());

                    if (stateResponse.code.Equals("00000") == true)
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage($"SetPositionMode - Code: {stateResponse.code}\n"
                            + $"Message: {stateResponse.msg}", LogMessageType.Error);
                    }
                }
                else
                {
                    if (responseMessage.Content.Contains("\"sign signature error\"")
                        || responseMessage.Content.Contains("\"Apikey does not exist\"")
                        || responseMessage.Content.Contains("\"apikey/password is incorrect\"")
                        || responseMessage.Content.Contains("\"Request timestamp expired\"")
                        || responseMessage.Content == "")
                    {
                        Disconnect();
                    }

                    SendLogMessage($"SetPositionMode - Http State Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.Message, LogMessageType.Error);
            }
        }

        public void SetCommonMarginMode(string selectedClass, string marginMode) { }

        public void SetLeverage(string securityName, string className, string leverage, string leverageLong, string leverageShort)
        {
            Dictionary<string, string> jsonContent = new Dictionary<string, string>();

            jsonContent.Add("symbol", securityName);
            jsonContent.Add("productType", className);
            jsonContent.Add("marginCoin", className.Split("-")[0]);

            if (leverage != "")
            {
                jsonContent.Add("leverage", leverage.ToString());
            }
            else
            {
                jsonContent.Add("longLeverage", leverageLong.ToString());
                jsonContent.Add("shortLeverage", leverageShort.ToString());
            }

            string jsonRequest = JsonConvert.SerializeObject(jsonContent);

            IRestResponse responseMessage = CreatePrivateQueryOrders("/api/v2/mix/account/set-leverage", Method.POST, null, jsonRequest);

            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<object>());

                if (stateResponse.code.Equals("00000") == true)
                {
                    // ignore
                }
                else
                {
                    SendLogMessage($"SetLeverage - Code: {stateResponse.code}\n"
                        + $"Message: {stateResponse.msg}", LogMessageType.Error);
                }
            }
            else
            {
                if (responseMessage.Content.Contains("\"sign signature error\"")
                    || responseMessage.Content.Contains("\"Apikey does not exist\"")
                    || responseMessage.Content.Contains("\"apikey/password is incorrect\"")
                    || responseMessage.Content.Contains("\"Request timestamp expired\"")
                    || responseMessage.Content == "")
                {
                    Disconnect();
                }

                SendLogMessage($"SetLeverage - Http State Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
            }
        }

        public void SetHedgeMode(string securityName, string className, string hedgeMode) { }

        public void SetMarginMode(string securityName, string className, string marginMode)
        {
            Dictionary<string, string> jsonContent = new Dictionary<string, string>();

            jsonContent.Add("symbol", securityName);
            jsonContent.Add("productType", className);
            jsonContent.Add("marginCoin", className.Split("-")[0]);
            jsonContent.Add("marginMode", marginMode);

            string jsonRequest = JsonConvert.SerializeObject(jsonContent);

            IRestResponse responseMessage = CreatePrivateQueryOrders("/api/v2/mix/account/set-margin-mode", Method.POST, null, jsonRequest);

            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                ResponseRestMessage<object> stateResponse = JsonConvert.DeserializeAnonymousType(responseMessage.Content, new ResponseRestMessage<object>());

                if (stateResponse.code.Equals("00000") == true)
                {
                    // ignore
                }
                else
                {
                    SendLogMessage($"SetMarginMode - Code: {stateResponse.code}\n"
                        + $"Message: {stateResponse.msg}", LogMessageType.Error);
                }
            }
            else
            {
                if (responseMessage.Content.Contains("\"sign signature error\"")
                    || responseMessage.Content.Contains("\"Apikey does not exist\"")
                    || responseMessage.Content.Contains("\"apikey/password is incorrect\"")
                    || responseMessage.Content.Contains("\"Request timestamp expired\"")
                    || responseMessage.Content == "")
                {
                    Disconnect();
                }

                SendLogMessage($"SetMarginMode - Http State Code: {responseMessage.StatusCode} || msg: {responseMessage.Content}", LogMessageType.Error);
            }
        }

        private string GetMarginModeFromSettings(string securityName, string className)
        {
            List<AServer> servers = ServerMaster.GetAServers();

            List<SecurityLeverageData> securityData = new();

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].ServerRealization.Equals(this))
                {
                    securityData = servers[i].ListLeverageData[className].SecurityData;
                }
            }

            if (securityData.Count == 0)
            {
                return "";
            }

            for (int i = 0; i < securityData.Count; i++)
            {
                if (securityData[i].SecurityName == securityName && securityData[i].ClassName == className)
                {
                    return securityData[i].MarginMode.ToString();
                }
            }

            return "";
        }

        private bool GetHedgeModeFromSettings(string className)
        {
            List<AServer> servers = ServerMaster.GetAServers();

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].ServerRealization.Equals(this))
                {
                    ClassLeverageData data = servers[i].ListLeverageData[className];

                    if (data.CommonHedgeMode == "Off")
                    {
                        return false;
                    }
                    else if (data.CommonHedgeMode == "On")
                    {
                        return true;
                    }
                }
            }

            return false;       
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