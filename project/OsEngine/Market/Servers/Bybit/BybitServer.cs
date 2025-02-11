﻿/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Bybit.Entities;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocketSharp;

namespace OsEngine.Market.Servers.Bybit
{
    public class BybitServer : AServer
    {
        public BybitServer()
        {
            BybitServerRealization realization = new BybitServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterEnum(OsLocalization.Market.Label1, Net_type.MainNet.ToString(), new List<string>() { Net_type.MainNet.ToString(), Net_type.TestNet.ToString() });
            CreateParameterEnum(OsLocalization.Market.ServerParam4, MarginMode.Cross.ToString(), new List<string>() { MarginMode.Cross.ToString(), MarginMode.Isolated.ToString() });
            CreateParameterEnum("Hedge Mode", "On", new List<string> { "On", "Off" });
        }
    }

    public class BybitServerRealization : IServerRealization
    {

        #region 1 Constructor, Status, Connection

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public BybitServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            supported_intervals = CreateIntervalDictionary();

            Thread threadPrivateMessageReader = new Thread(() => ThreadPrivateMessageReader());
            threadPrivateMessageReader.Name = "ThreadBybitPrivateMessageReader";
            threadPrivateMessageReader.Start();

            Thread threadPublicMessageReader = new Thread(() => ThreadPublicMessageReader());
            threadPublicMessageReader.Name = "ThreadBybitPublicMessageReader";
            threadPublicMessageReader.Start();

            Thread threadMessageReaderOrderBookSpot = new Thread(() => ThreadMessageReaderOrderBookSpot());
            threadMessageReaderOrderBookSpot.Name = "ThreadBybitMessageReaderOrderBookSpot";
            threadMessageReaderOrderBookSpot.Start();

            Thread threadMessageReaderOrderBookLinear = new Thread(() => ThreadMessageReaderOrderBookLinear());
            threadMessageReaderOrderBookLinear.Name = "ThreadBybitMessageReaderOrderBookLinear";
            threadMessageReaderOrderBookLinear.Start();

            Thread threadMessageReaderTradesSpot = new Thread(() => ThreadMessageReaderTradesSpot());
            threadMessageReaderTradesSpot.Name = "ThreadBybitMessageReaderTradesSpot";
            threadMessageReaderTradesSpot.Start();

            Thread threadMessageReaderTradesLinear = new Thread(() => ThreadMessageReaderTradesLinear());
            threadMessageReaderTradesLinear.Name = "ThreadBybitMessageReaderTradesLinear";
            threadMessageReaderTradesLinear.Start();

            Thread threadGetPortfolios = new Thread(() => ThreadGetPortfolios());
            threadGetPortfolios.Name = "ThreadBybitGetPortfolios";
            threadGetPortfolios.Start();

            Thread threadCheckAlivePublicWebSocket = new Thread(() => ThreadCheckAliveWebSocketThread());
            threadCheckAlivePublicWebSocket.Name = "ThreadBybitCheckAliveWebSocketThread";
            threadCheckAlivePublicWebSocket.Start();
        }

        public void Connect()
        {
            try
            {
                PublicKey = ((ServerParameterString)ServerParameters[0]).Value;
                SecretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
                net_type = (Net_type)Enum.Parse(typeof(Net_type), ((ServerParameterEnum)ServerParameters[2]).Value);
                margineMode = (MarginMode)Enum.Parse(typeof(MarginMode), ((ServerParameterEnum)ServerParameters[3]).Value);

                if (((ServerParameterEnum)ServerParameters[4]).Value == "On")
                {
                    _hedgeMode = true;
                }
                else
                {
                    _hedgeMode = false;
                }

                if (!CheckApiKeyInformation(PublicKey))
                {
                    Disconnect();

                    return;
                }

                CreatePublicWebSocketConnect();
                CreatePrivateWebSocketConnect();

                CheckFullActivation();
                SetMargineMode();
                SetPositionMode();
            }
            catch (Exception ex)
            {
                SendLogMessage($"Can`t run ByBit connector. No internet connection. {ex.ToString()} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public void Disconnect()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        private void CheckFullActivation()
        {
            try
            {
                if (!CheckApiKeyInformation(PublicKey))
                {
                    Disconnect();
                    return;
                }

                if (webSocketPrivate == null
                    || webSocketPrivate?.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (_webSocketPublicSpot.Count == 0 ||
                    _webSocketPublicLinear.Count == 0)
                {
                    Disconnect();
                    return;
                }

                WebSocket webSocketPublicSpot = _webSocketPublicSpot[0];

                if (webSocketPublicSpot == null
                    || webSocketPublicSpot?.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                WebSocket webSocketPublicLinear = _webSocketPublicLinear[0];

                if (webSocketPublicLinear == null
                    || webSocketPublicLinear?.ReadyState != WebSocketState.Open)
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
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            try
            {
                try
                {
                    lock (_httpClientLocker)
                    {
                        httpClient?.Dispose();
                        httpClientHandler?.Dispose();
                    }

                    httpClient = null;
                    httpClientHandler = null;
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }
            catch
            {

            }

            try
            {
                DisposePublicWebSocket();
            }
            catch
            {

            }

            try
            {
                DisposePrivateWebSocket();
            }
            catch
            {

            }

            SubscribeSecuritySpot.Clear();
            SubscribeSecurityLinear.Clear();

            concurrentQueueMessagePublicWebSocket = new ConcurrentQueue<string>();
            _concurrentQueueMessageOrderBookSpot = new ConcurrentQueue<string>();
            _concurrentQueueMessageOrderBookLinear = new ConcurrentQueue<string>();
            concurrentQueueMessagePrivateWebSocket = new ConcurrentQueue<string>();

            _concurrentQueueTradesSpot = new ConcurrentQueue<string>();
            _concurrentQueueTradesLinear = new ConcurrentQueue<string>();

            Disconnect();
        }

        private void SetMargineMode()
        {
            try
            {
                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs["setMarginMode"] = margineMode == MarginMode.Cross ? "REGULAR_MARGIN" : "ISOLATED_MARGIN";
                CreatePrivateQuery(parametrs, HttpMethod.Post, "/v5/account/set-margin-mode");
            }
            catch (Exception ex)
            {
                SendLogMessage($"Check Bybit API Keys and Unified Account Settings! {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void SetPositionMode()
        {
            try
            {
                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs.Clear();
                parametrs["category"] = Category.linear.ToString();
                parametrs["coin"] = "USDT";
                parametrs["mode"] = _hedgeMode == true ? "3" : "0"; //Position mode. 0: Merged Single. 3: Both Sides

                CreatePrivateQuery(parametrs, HttpMethod.Post, "/v5/position/switch-mode");
            }
            catch (Exception ex)
            {
                SendLogMessage($"SetPositionMode: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        #endregion 1

        #region 2 Properties

        public ServerType ServerType => ServerType.Bybit;

        public ServerConnectStatus ServerStatus { get; set; }

        public DateTime ServerTime { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        private readonly Dictionary<double, string> supported_intervals;

        private string PublicKey = String.Empty;

        private string SecretKey = String.Empty;

        private Net_type net_type;

        private MarginMode margineMode;

        private bool _hedgeMode;

        private int marketDepthDeep
        {
            get
            {
                if (((ServerParameterBool)ServerParameters[12]).Value)
                {
                    return 50;
                }
                else
                {
                    return 1;
                }
            }
            set
            {

            }
        }

        private string main_Url = "https://api.bybit.com";

        private string test_Url = "https://api-testnet.bybit.com";

        private string mainWsPublicUrl = "wss://stream.bybit.com/v5/public/";    // +  linear, spot;

        private string testWsPublicUrl = "wss://stream-testnet.bybit.com/v5/public/";    // +  linear, spot";

        private string mainWsPrivateUrl = "wss://stream.bybit.com/v5/private";

        private string testWsPrivateUrl = "wss://stream-testnet.bybit.com/v5/private";

        private string wsPublicUrl(Category category = Category.spot)
        {
            string url;
            if (net_type == Net_type.MainNet)
            {
                url = mainWsPublicUrl;
            }
            else
            {
                url = testWsPublicUrl;
            }

            switch (category)
            {
                case Category.spot:
                    url = url + "spot";
                    break;
                case Category.linear:
                    url = url + "linear";
                    break;
                case Category.inverse:
                    url = url + "inverse";
                    break;
                case Category.option:
                    url = url + "option";
                    break;
                default:
                    break;
            }

            return url;
        }

        private string wsPrivateUrl
        {
            get
            {
                if (net_type == Net_type.MainNet)
                {
                    return mainWsPrivateUrl;
                }
                else
                {
                    return testWsPrivateUrl;
                }
            }
        }

        private string RestUrl
        {
            get
            {
                if (net_type == Net_type.MainNet)
                {
                    return main_Url;
                }
                else
                {
                    return test_Url;
                }
            }
        }

        #endregion 2

        #region 3 Securities

        public event Action<List<Security>> SecurityEvent;

        public void GetSecurities()
        {
            try
            {
                List<Security> securities = new List<Security>();
                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs.Add("limit", "1000");
                parametrs.Add("category", Category.spot);

                JToken sec = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/instruments-info");
                ResponseRestMessage<ArraySymbols> symbols;

                if (sec != null)
                {
                    symbols = JsonConvert.DeserializeObject<ResponseRestMessage<ArraySymbols>>(sec.ToString());
                    ConvertSecuritis(symbols, securities, Category.spot);
                }

                parametrs["category"] = Category.linear;

                sec = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/instruments-info");

                if (sec != null)
                {
                    symbols = JsonConvert.DeserializeObject<ResponseRestMessage<ArraySymbols>>(sec.ToString());
                    ConvertSecuritis(symbols, securities, Category.linear);
                }

                SecurityEvent?.Invoke(securities);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void ConvertSecuritis(ResponseRestMessage<ArraySymbols> symbols, List<Security> securities, Category category)
        {
            try
            {
                for (int i = 0; i < symbols.result.list.Count - 1; i++)
                {
                    Symbols oneSec = symbols.result.list[i];

                    if (oneSec.status.ToLower() == "trading")
                    {
                        Security security = new Security();
                        int.TryParse(oneSec.priceScale, out int ps);
                        security.Decimals = ps;
                        security.DecimalsVolume = GetDecimalsVolume(oneSec.lotSizeFilter.minOrderQty);
                        security.Name = oneSec.symbol + (category == Category.linear ? ".P" : "");
                        security.NameFull = oneSec.symbol;
                        security.NameId = oneSec.symbol;

                        if (category == Category.linear)
                        {
                            security.SecurityType = SecurityType.Futures;
                        }
                        else
                        {
                            security.SecurityType = SecurityType.CurrencyPair;
                        }

                        if (category == Category.spot)
                        {
                            security.NameClass = oneSec.quoteCoin;
                        }
                        else if (category == Category.linear)
                        {
                            if (security.NameFull.EndsWith("PERP"))
                            {
                                security.NameClass = oneSec.contractType + "_PERP";
                            }
                            else
                            {
                                security.NameClass = oneSec.contractType;
                            }
                        }
                        else
                        {
                            security.NameClass = oneSec.contractType;
                        }

                        security.PriceStep = oneSec.priceFilter.tickSize.ToDecimal();
                        security.PriceStepCost = oneSec.priceFilter.tickSize.ToDecimal();
                        security.MinTradeAmount = oneSec.lotSizeFilter.minOrderQty.ToDecimal();
                        security.State = SecurityStateType.Activ;
                        security.Exchange = "ByBit";
                        security.Lot = 1;

                        securities.Add(security);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private int GetDecimalsVolume(string str)
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

        #endregion 3

        #region 4 Portfolios

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]

        private void ThreadGetPortfolios()
        {
            Thread.Sleep(20000);

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                    continue;
                }

                try
                {
                    Thread.Sleep(5000);

                    GetPortfolios();
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }

        }

        private List<Portfolio> portfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            try
            {
                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs["accountType"] = "UNIFIED";
                JToken balance = CreatePrivateQuery(parametrs, HttpMethod.Get, "/v5/account/wallet-balance");

                if (balance == null)
                {
                    return;
                }

                List<Portfolio> _portfolios = new List<Portfolio>();

                for (int i = 0; i < portfolios.Count; i++)
                {
                    Portfolio p = portfolios[i];
                    Portfolio newp = new Portfolio();
                    newp.Number = p.Number;
                    newp.UnrealizedPnl = p.UnrealizedPnl;
                    newp.ValueBegin = p.ValueBegin;
                    newp.ValueBlocked = p.ValueBlocked;
                    newp.ValueCurrent = p.ValueCurrent;

                    List<PositionOnBoard> positionOnBoards = portfolios[i].GetPositionOnBoard();

                    for (int i2 = 0; positionOnBoards != null && i2 < positionOnBoards.Count; i2++)
                    {
                        PositionOnBoard oldPB = positionOnBoards[i2];
                        PositionOnBoard newPB = new PositionOnBoard();
                        newPB.PortfolioName = oldPB.PortfolioName;
                        newPB.SecurityNameCode = oldPB.SecurityNameCode;
                        newPB.ValueBegin = oldPB.ValueBegin;
                        newPB.ValueBlocked = oldPB.ValueBlocked;
                        newPB.ValueCurrent = 0;
                        newp.SetNewPosition(newPB);
                    }

                    _portfolios.Add(newp);
                }

                List<JToken> JPortolioList = balance.SelectToken("result.list").Children().ToList();

                for (int j = 0; JPortolioList != null && j < JPortolioList.Count; j++)
                {
                    JToken item = JPortolioList[j];
                    string portNumber = "Bybit" + item.SelectToken("accountType").ToString();
                    Portfolio portfolio = BybitPortfolioCreator(item, portNumber);
                    bool newPort = true;

                    for (int i = 0; i < _portfolios.Count; i++)
                    {
                        if (_portfolios[i].Number == portNumber)
                        {

                            _portfolios[i].ValueBegin = portfolio.ValueBegin;
                            _portfolios[i].ValueBlocked = portfolio.ValueBlocked;
                            _portfolios[i].ValueCurrent = portfolio.ValueCurrent;
                            portfolio = _portfolios[i];
                            newPort = false;
                            break;
                        }
                    }

                    if (newPort)
                    {
                        _portfolios.Add(portfolio);
                    }

                    List<PositionOnBoard> PositionOnBoard = GetPositionsLinear(portfolio.Number);
                    PositionOnBoard.AddRange(GetPositionsSpot(item.SelectToken("coin").Children().ToList(), portfolio.Number));

                    for (int i = 0; i < PositionOnBoard.Count; i++)
                    {
                        portfolio.SetNewPosition(PositionOnBoard[i]);
                    }
                }

                portfolios.Clear();
                portfolios = _portfolios;
                PortfolioEvent?.Invoke(_portfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Portfolio request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private static Portfolio BybitPortfolioCreator(JToken data, string portfolioName)
        {
            try
            {
                Portfolio portfolio = new Portfolio();
                portfolio.Number = portfolioName;

                portfolio.ValueCurrent = 1;
                portfolio.ValueBlocked = 1;
                portfolio.ValueBegin = 1;

                portfolio.ValueBegin = data.SelectToken("totalWalletBalance").Value<decimal>();

                if (data.SelectToken("totalMarginBalance").Value<string>().Length > 0)
                {
                    decimal.TryParse(data.SelectToken("totalMarginBalance").Value<string>(), System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out portfolio.ValueCurrent);
                }
                else
                {
                    decimal.TryParse(data.SelectToken("totalWalletBalance").Value<string>(), System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out portfolio.ValueCurrent);
                }

                if (data.SelectToken("totalInitialMargin").Value<string>().Length > 0)
                {
                    decimal.TryParse(data.SelectToken("totalInitialMargin").Value<string>(), System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out portfolio.ValueBlocked);
                }
                else
                {
                    portfolio.ValueBlocked = 0;
                }

                return portfolio;
            }
            catch
            {
                return new Portfolio(); ;
            }
        }

        private List<PositionOnBoard> GetPositionsSpot(List<JToken> JCoinList, string portfolioNumber)
        {
            try
            {
                List<PositionOnBoard> pb = new List<PositionOnBoard>();

                for (int j2 = 0; j2 < JCoinList.Count; j2++)
                {
                    JToken item2 = JCoinList[j2];
                    PositionOnBoard positions = new PositionOnBoard();
                    positions.PortfolioName = portfolioNumber;
                    positions.SecurityNameCode = item2.SelectToken("coin").Value<string>();
                    decimal.TryParse(item2.SelectToken("walletBalance").Value<string>(), System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out positions.ValueBegin);
                    decimal.TryParse(item2.SelectToken("equity").Value<string>(), System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out positions.ValueCurrent);
                    decimal.TryParse(item2.SelectToken("locked").Value<string>(), System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out positions.ValueBlocked);

                    pb.Add(positions);
                }

                return pb;
            }
            catch
            {
                return null;
            }
        }

        private List<PositionOnBoard> GetPositionsLinear(string portfolioNumber)
        {
            List<PositionOnBoard> positionOnBoards = new List<PositionOnBoard>();
            string[] settleCoin = new string[] { "USDT", "USDC", "BTC", "ETH" };

            try
            {
                Dictionary<string, object> parametrs = new Dictionary<string, object>();

                for (int i = 0; i < settleCoin.Length; i++)
                {
                    parametrs["settleCoin"] = settleCoin[i];
                    parametrs["category"] = Category.linear;
                    parametrs["limit"] = 10;

                    if (parametrs.ContainsKey("cursor"))
                    {
                        parametrs.Remove("cursor");
                    }

                    string nextPageCursor = "";

                    do
                    {
                        JToken position = CreatePrivateQuery(parametrs, HttpMethod.Get, "/v5/position/list");
                        if (position == null)
                        {
                            return positionOnBoards;
                        }

                        List<PositionOnBoard> positions = CreatePosOnBoard(position.SelectToken("result.list"), portfolioNumber);

                        if (positions != null && positions.Count > 0)
                        {
                            positionOnBoards.AddRange(positions);
                        }

                        nextPageCursor = position.SelectToken("result.nextPageCursor").ToString();

                        if (nextPageCursor.Length > 1)
                        {
                            parametrs["cursor"] = nextPageCursor;
                        }

                    } while (nextPageCursor.Length > 1);
                }
                return positionOnBoards;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Position request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return positionOnBoards;
            }
        }

        private List<PositionOnBoard> CreatePosOnBoard(JToken data, string potrolioNumber)
        {
            List<PositionOnBoard> poses = new List<PositionOnBoard>();

            List<PositionOnBoardResult> positions =
                      JsonConvert.DeserializeAnonymousType(data.ToString(), new List<PositionOnBoardResult>());

            for (int i = 0; i < positions.Count; i++)
            {
                PositionOnBoardResult posJson = positions[i];

                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = potrolioNumber;

                if (_hedgeMode 
                    && posJson.symbol.Contains("USDT"))
                {
                    if (posJson.side == "Buy")
                    {
                        pos.SecurityNameCode = posJson.symbol + ".P" + "_" + "LONG";
                    }
                    else
                    {
                        pos.SecurityNameCode = posJson.symbol + ".P" + "_" + "SHORT";
                    }
                }
                else
                {
                    pos.SecurityNameCode = posJson.symbol + ".P";
                }

                pos.ValueBegin =
                    posJson.size.ToDecimal() * (posJson.side == "Buy" ? 1 : -1);

                pos.ValueCurrent = pos.ValueBegin;

                poses.Add(pos);
            }

            return poses;
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion 4

        #region 5 Data

        private RateGate _rateGateGetCandleHistory = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            _rateGateGetCandleHistory.WaitToProceed();
            return GetCandleHistory(security.Name, timeFrameBuilder.TimeFrameTimeSpan, false, DateTime.UtcNow, candleCount);
        }

        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf, bool IsOsData, DateTime timeEnd, int CountToLoad)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return null;
                }

                string category = Category.spot.ToString();

                if (nameSec.EndsWith(".P"))
                {
                    category = Category.linear.ToString();
                }

                if (!supported_intervals.ContainsKey(Convert.ToInt32(tf.TotalMinutes)))
                {
                    return null;
                }

                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs["category"] = category;
                parametrs["symbol"] = nameSec.Replace(".P", "");
                parametrs["interval"] = supported_intervals[Convert.ToInt32(tf.TotalMinutes)];
                parametrs["start"] = ((DateTimeOffset)timeEnd.AddMinutes(tf.TotalMinutes * -1 * CountToLoad).ToUniversalTime()).ToUnixTimeMilliseconds();
                parametrs["end"] = ((DateTimeOffset)timeEnd.ToUniversalTime()).ToUnixTimeMilliseconds();
                parametrs["limit"] = 1000;

                JToken JCandles = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/kline");

                if (JCandles == null)
                {
                    return new List<Candle>();
                }

                List<Candle> candles = GetListCandles(JCandles);

                if (candles == null || candles.Count == 0)
                {
                    return null;
                }

                return GetListCandles(JCandles);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Candles request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
            return null;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            try
            {
                if (actualTime < startTime || actualTime > endTime)
                {
                    return null;
                }

                string category = Category.spot.ToString();

                if (security.Name.EndsWith(".P"))
                {
                    category = Category.linear.ToString();
                }

                if (!supported_intervals.ContainsKey(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes))
                {
                    return null;
                }

                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs["category"] = category;
                parametrs["symbol"] = security.Name.Replace(".P", "");
                parametrs["interval"] = supported_intervals[timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes];
                parametrs["limit"] = 1000;
                List<Candle> candles = new List<Candle>();
                parametrs["start"] = TimeManager.GetTimeStampMilliSecondsToDateTime(startTime);
                parametrs["end"] = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);

                do
                {
                    JToken JCandles = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/kline");

                    if (JCandles == null)
                    {
                        break;
                    }

                    List<Candle> newCandles = GetListCandles(JCandles);

                    if (newCandles != null && newCandles.Count > 0)
                    {
                        candles.InsertRange(0, newCandles);
                        if (candles[0].TimeStart > startTime)
                        {
                            parametrs["end"] = TimeManager.GetTimeStampMilliSecondsToDateTime(candles[0].TimeStart.AddMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * -1));
                        }
                        else
                        {
                            return candles;
                        }
                    }
                    else
                    {
                        break;
                    }

                } while (true);

                if (candles.Count > 0)
                {
                    return candles;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Candles request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
            // ByBit returns no more than 1000 last ticks via API, which does not meet the connector requirements, so we will return null

            List<Trade> trades = new List<Trade>();

            try
            {
                string category = Category.spot.ToString();
                if (security.Name.EndsWith(".P"))
                {
                    category = Category.linear.ToString();
                }

                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs.Add("category", category);
                parametrs.Add("symbol", security.Name.Replace(".P", ""));
                parametrs.Add("limit", 1000);   // this is the maximum they give

                JToken Jtrades = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/recent-trade");

                ResponseRestMessageList<RetTrade> responseTrades = JsonConvert.DeserializeObject<ResponseRestMessageList<RetTrade>>(Jtrades.ToString());
                if (responseTrades == null || responseTrades.result == null || responseTrades.result.list == null || responseTrades.result.list.Count == 0)
                {
                    return null;
                }

                List<RetTrade> retTrade = new List<RetTrade>();
                retTrade = responseTrades.result.list;
                DateTime preTime = DateTime.MinValue;

                for (int i = retTrade.Count - 1; i >= 0; i--)
                {
                    RetTrade Jtrade = retTrade[i];
                    Trade trade = new Trade();
                    trade.Id = Jtrade.execId;
                    trade.Price = Jtrade.price.ToDecimal();
                    trade.SecurityNameCode = Jtrade.symbol;
                    trade.Side = Jtrade.side == "Buy" ? Side.Buy : Side.Sell;
                    trade.Volume = Jtrade.size.ToDecimal();
                    DateTime tradeTime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(Jtrade.time)).UtcDateTime;
                    if (tradeTime <= preTime)   // at the same moment in time, down to milliseconds, there can be several trades in buybit
                    {
                        // tradeTime = preTime.AddTicks(1);    // if several trades are not possible at the same time, then allow the row and add by tick
                    }

                    trade.Time = tradeTime;
                    trade.MicroSeconds = 0;
                    preTime = trade.Time;

                    trades.Add(trade);
                }
                return trades;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Trades request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return trades;
            }
        }

        private static List<Candle> GetListCandles(JToken JCandles)
        {
            List<Candle> candles = new List<Candle>();

            try
            {
                JToken JListCandels = JCandles.SelectToken("result.list");
                List<JToken> JCandlesList = JListCandels.Children().Reverse().ToList();

                for (int i = 0; i < JCandlesList.Count; i++)
                {
                    JToken oneSec = JCandlesList[i];
                    List<JToken> listCandle = oneSec.Children().ToList();
                    DateTime d = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(listCandle[0].ToString())).UtcDateTime;
                    decimal o = listCandle[1].ToString().ToDecimal();
                    decimal h = listCandle[2].ToString().ToDecimal();
                    decimal l = listCandle[3].ToString().ToDecimal();
                    decimal c = listCandle[4].ToString().ToDecimal();
                    decimal v = listCandle[5].ToString().ToDecimal();

                    Candle candle = new Candle();
                    candle.TimeStart = d;
                    candle.Open = o;
                    candle.High = h;
                    candle.Low = l;
                    candle.Close = c;
                    candle.Volume = v;
                    candle.State = CandleState.Finished;
                    candles.Add(candle);
                }
            }
            catch
            {
                return new List<Candle>();
            }
            return candles;
        }

        private Dictionary<double, string> CreateIntervalDictionary()
        {
            Dictionary<double, string> dictionary = new Dictionary<double, string>();

            dictionary.Add(1, "1");
            dictionary.Add(3, "3");
            dictionary.Add(5, "5");
            dictionary.Add(15, "15");
            dictionary.Add(30, "30");
            dictionary.Add(60, "60");
            dictionary.Add(120, "120");
            dictionary.Add(240, "240");
            dictionary.Add(360, "360");
            dictionary.Add(720, "720");
            dictionary.Add(1440, "D");

            return dictionary;
        }

        #endregion 5

        #region 6 WebSocket creation

        private List<WebSocket> _webSocketPublicSpot = new List<WebSocket>();

        private List<WebSocket> _webSocketPublicLinear = new List<WebSocket>();

        private WebSocket webSocketPrivate;

        private ConcurrentQueue<string> concurrentQueueMessagePublicWebSocket;

        private ConcurrentQueue<string> concurrentQueueMessagePrivateWebSocket;

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (concurrentQueueMessagePublicWebSocket == null)
                {
                    concurrentQueueMessagePublicWebSocket = new ConcurrentQueue<string>();
                }

                if (_concurrentQueueMessageOrderBookSpot == null)
                {
                    _concurrentQueueMessageOrderBookSpot = new ConcurrentQueue<string>();
                    _concurrentQueueMessageOrderBookLinear = new ConcurrentQueue<string>();
                }

                _webSocketPublicSpot.Add(CreateNewSpotPublicSocket());
                _webSocketPublicLinear.Add(CreateNewLinearPublicSocket());

            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewSpotPublicSocket()
        {
            WebSocket webSocketPublicSpot = new WebSocket(wsPublicUrl(Category.spot));
            webSocketPublicSpot.EmitOnPing = true;
            webSocketPublicSpot.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.None;
            webSocketPublicSpot.OnOpen += WebSocketPublic_Opened;
            webSocketPublicSpot.OnMessage += WebSocketPublic_MessageReceivedSpot;
            webSocketPublicSpot.OnError += WebSocketPublic_Error;
            webSocketPublicSpot.OnClose += WebSocketPublic_Opened;

            webSocketPublicSpot.Connect();

            return webSocketPublicSpot;
        }

        private WebSocket CreateNewLinearPublicSocket()
        {
            WebSocket webSocketPublicLinear = new WebSocket(wsPublicUrl(Category.linear));
            webSocketPublicLinear.EmitOnPing = true;
            webSocketPublicLinear.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.None;
            webSocketPublicLinear.OnOpen += WebSocketPublic_Opened;
            webSocketPublicLinear.OnMessage += WebSocketPublic_MessageReceivedLinear;
            webSocketPublicLinear.OnError += WebSocketPublic_Error;
            webSocketPublicLinear.OnClose += WebSocketPublic_Opened;

            webSocketPublicLinear.Connect();

            return webSocketPublicLinear;
        }

        private void CreatePrivateWebSocketConnect()
        {
            try
            {
                if (concurrentQueueMessagePrivateWebSocket == null) concurrentQueueMessagePrivateWebSocket = new ConcurrentQueue<string>();

                webSocketPrivate = new WebSocket(wsPrivateUrl);
                webSocketPrivate.EmitOnPing = true;
                webSocketPrivate.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.None;

                webSocketPrivate.OnMessage += WebSocketPrivate_MessageReceived;
                webSocketPrivate.OnClose += WebSocketPrivate_Closed;
                webSocketPrivate.OnError += WebSocketPrivate_Error;
                webSocketPrivate.OnOpen += WebSocketPrivate_Opened;

                webSocketPrivate.Connect();
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        #endregion 6

        #region 7 WebSocket events

        private void WebSocketPrivate_Opened(object sender, EventArgs e)
        {
            try
            {
                CheckFullActivation();

                string authRequest = GetWebSocketAuthRequest();
                webSocketPrivate?.Send(authRequest);
                webSocketPrivate?.Send("{\"op\":\"subscribe\",\"args\":[\"order\"]}");
                webSocketPrivate?.Send("{\"op\":\"subscribe\", \"args\":[\"execution\"]}");
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private string GetWebSocketAuthRequest()
        {
            long.TryParse(GetServerTime(), out long expires);
            expires += 10000;
            string signature = GenerateSignature(SecretKey, "GET/realtime" + expires);
            string sign = $"{{\"op\":\"auth\",\"args\":[\"{PublicKey}\",{expires},\"{signature}\"]}}";
            return sign;
        }

        private string GenerateSignature(string secret, string message)
        {
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        private void WebSocketPrivate_Error(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            try
            {
                if (DateTime.Now.Subtract(SendLogMessageTime).TotalSeconds > 10)
                {
                    SendLogMessageTime = DateTime.Now;
                    SendLogMessage($"WebSocketPrivate {sender} error {e.Exception.Message}, wait reconnect", LogMessageType.Error);
                }

                CheckFullActivation();
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_Closed(object sender, EventArgs e)
        {
            try
            {
                CheckFullActivation();
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void WebSocketPrivate_MessageReceived(object sender, MessageEventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                return;
            }

            if (concurrentQueueMessagePrivateWebSocket != null)
            {
                concurrentQueueMessagePrivateWebSocket?.Enqueue(e.Data);
            }
        }

        private void WebSocketPublic_MessageReceivedSpot(object sender, MessageEventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                return;
            }

            if (concurrentQueueMessagePublicWebSocket != null)
            {
                concurrentQueueMessagePublicWebSocket?.Enqueue(e.Data + ".SPOT");
            }
        }

        private void WebSocketPublic_MessageReceivedLinear(object sender, MessageEventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                return;
            }

            if (concurrentQueueMessagePublicWebSocket != null)
            {
                concurrentQueueMessagePublicWebSocket?.Enqueue(e.Data);
            }
        }

        private void WebSocketPublic_Closed(object sender, EventArgs e)
        {
            try
            {
                CheckFullActivation();
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        DateTime SendLogMessageTime = DateTime.Now;

        private void WebSocketPublic_Error(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            try
            {
                if (DateTime.Now.Subtract(SendLogMessageTime).TotalSeconds > 10)
                {
                    SendLogMessageTime = DateTime.Now;
                    SendLogMessage($"WebSocketPublic {sender} error {e.Exception.Message}, wait reconnect", LogMessageType.Error);
                }

                CheckFullActivation();
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void WebSocketPublic_Opened(object sender, EventArgs e)
        {
            CheckFullActivation();
        }

        #endregion 7

        #region 8 WebSocket check alive

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        private void ThreadCheckAliveWebSocketThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(30000);

                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    if (httpClient == null
                        || !CheckApiKeyInformation(PublicKey))
                    {
                        continue;
                    }

                    for (int i = 0; i < _webSocketPublicSpot.Count; i++)
                    {
                        WebSocket webSocketPublicSpot = _webSocketPublicSpot[i];
                        if (webSocketPublicSpot != null && webSocketPublicSpot?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublicSpot?.Send("{\"req_id\": \"OsEngine\", \"op\": \"ping\"}");
                        }
                    }

                    for (int i = 0; i < _webSocketPublicLinear.Count; i++)
                    {
                        WebSocket webSocketPublicLinear = _webSocketPublicLinear[i];

                        if (webSocketPublicLinear != null && webSocketPublicLinear?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublicLinear?.Send("{\"req_id\": \"OsEngine\", \"op\": \"ping\"}");
                        }
                    }

                    if (webSocketPrivate != null && webSocketPrivate?.ReadyState == WebSocketState.Open)
                    {
                        webSocketPrivate?.Send("{\"req_id\": \"OsEngine\", \"op\": \"ping\"}");
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }
        }

        private void DisposePrivateWebSocket()
        {
            if (webSocketPrivate != null)
            {
                try
                {
                    if (webSocketPrivate.ReadyState == WebSocketState.Open)
                    {
                        // unsubscribe from a stream
                        webSocketPrivate.Send("{\"req_id\": \"order_1\", \"op\": \"unsubscribe\",\"args\": [\"order\"]}");
                        webSocketPrivate.Send("{\"req_id\": \"ticketInfo_1\", \"op\": \"unsubscribe\", \"args\": [ \"ticketInfo\"]}");
                    }
                    webSocketPrivate.OnMessage -= WebSocketPrivate_MessageReceived;
                    webSocketPrivate.OnClose -= WebSocketPrivate_Closed;
                    webSocketPrivate.OnError -= WebSocketPrivate_Error;
                    webSocketPrivate.OnOpen -= WebSocketPrivate_Opened;

                    webSocketPrivate.CloseAsync();
                    webSocketPrivate = null;
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }
            webSocketPrivate = null;
            concurrentQueueMessagePrivateWebSocket = null;
        }

        private void DisposePublicWebSocket()
        {
            try
            {
                for (int i = 0; i < _webSocketPublicSpot.Count; i++)
                {
                    WebSocket webSocketPublicSpot = _webSocketPublicSpot[i];

                    webSocketPublicSpot.OnOpen -= WebSocketPublic_Opened;
                    webSocketPublicSpot.OnMessage -= WebSocketPublic_MessageReceivedSpot;
                    webSocketPublicSpot.OnError -= WebSocketPublic_Error;
                    webSocketPublicSpot.OnClose -= WebSocketPublic_Opened;

                    try
                    {
                        if (webSocketPublicSpot != null && webSocketPublicSpot?.ReadyState == WebSocketState.Open)
                        {
                            for (int i2 = 0; i2 < SubscribeSecuritySpot.Count; i2++)
                            {
                                string s = SubscribeSecuritySpot[i2].Replace(".P", "");
                                webSocketPublicSpot?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"publicTrade.{s}\" ] }}");
                                webSocketPublicSpot?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{s}\" ] }}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                    }

                    if (webSocketPublicSpot.ReadyState == WebSocketState.Open)
                    {
                        webSocketPublicSpot.CloseAsync();
                    }
                    webSocketPublicSpot = null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            _webSocketPublicSpot.Clear();

            try
            {
                for (int i = 0; i < _webSocketPublicLinear.Count; i++)
                {
                    WebSocket webSocketPublicLinear = _webSocketPublicLinear[i];
                    webSocketPublicLinear.OnOpen -= WebSocketPublic_Opened;
                    webSocketPublicLinear.OnMessage -= WebSocketPublic_MessageReceivedLinear;
                    webSocketPublicLinear.OnError -= WebSocketPublic_Error;
                    webSocketPublicLinear.OnClose -= WebSocketPublic_Opened;

                    try
                    {
                        if (webSocketPublicLinear != null && webSocketPublicLinear?.ReadyState == WebSocketState.Open)
                        {
                            for (int i2 = 0; i2 < SubscribeSecurityLinear.Count; i2++)
                            {
                                string s = SubscribeSecurityLinear[i2].Replace(".P", "");
                                webSocketPublicLinear?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"publicTrade.{s}\" ] }}");
                                webSocketPublicLinear?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{s}\" ] }}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                    }

                    if (webSocketPublicLinear.ReadyState == WebSocketState.Open)
                    {
                        webSocketPublicLinear.CloseAsync();
                    }

                    webSocketPublicLinear = null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            _webSocketPublicLinear.Clear();

            _listMarketDepthSpot?.Clear();
            concurrentQueueMessagePublicWebSocket = null;
            _concurrentQueueMessageOrderBookSpot = null;
        }

        #endregion  8

        #region 9 Security subscrible

        private List<string> SubscribeSecuritySpot = new List<string>();

        private List<string> SubscribeSecurityLinear = new List<string>();

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(150));

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();

                if (!security.Name.EndsWith(".P"))
                {
                    if (SubscribeSecuritySpot.Exists(s => s == security.Name) == true)
                    {
                        // уже подписаны на такое
                        return;
                    }

                    if (_webSocketPublicSpot.Count == 0)
                    {
                        return;
                    }

                    WebSocket webSocketPublicSpot = _webSocketPublicSpot[_webSocketPublicSpot.Count - 1];

                    if (webSocketPublicSpot.ReadyState == WebSocketState.Open
                        && SubscribeSecuritySpot.Count != 0
                        && SubscribeSecuritySpot.Count % 50 == 0)
                    {
                        // создание нового сокета
                        WebSocket newSocket = CreateNewSpotPublicSocket();

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
                            _webSocketPublicSpot.Add(newSocket);
                            webSocketPublicSpot = newSocket;
                        }
                    }

                    if (webSocketPublicSpot != null)
                    {
                        webSocketPublicSpot?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"publicTrade.{security.Name}\" ] }}");
                        webSocketPublicSpot?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{security.Name}\" ] }}");

                        if (SubscribeSecuritySpot.Exists(s => s == security.Name) == false)
                        {
                            SubscribeSecuritySpot.Add(security.Name);
                        }
                    }
                }
                else
                {
                    if (_webSocketPublicLinear.Count == 0)
                    {
                        return;
                    }

                    WebSocket webSocketPublicLinear = _webSocketPublicLinear[_webSocketPublicLinear.Count - 1];

                    if (webSocketPublicLinear.ReadyState == WebSocketState.Open
                        && SubscribeSecurityLinear.Count != 0
                        && SubscribeSecurityLinear.Count % 50 == 0)
                    {
                        // создание нового сокета
                        WebSocket newSocket = CreateNewLinearPublicSocket();

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
                            _webSocketPublicLinear.Add(newSocket);
                            webSocketPublicLinear = newSocket;
                        }
                    }

                    if (webSocketPublicLinear != null
                        && webSocketPublicLinear?.ReadyState == WebSocketState.Open)
                    {
                        if (SubscribeSecurityLinear.Exists(s => s == security.Name) == true)
                        {
                            // уже подписаны на такое
                            SubscribeSecurityLinear.Add(security.Name);
                        }

                        webSocketPublicLinear?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"publicTrade.{security.Name.Replace(".P", "")}\" ] }}");
                        webSocketPublicLinear?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{security.Name.Replace(".P", "")}\" ] }}");

                        if (SubscribeSecurityLinear.Exists(s => s == security.Name) == false)
                        {
                            SubscribeSecurityLinear.Add(security.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent;

        #endregion 9

        #region 10 WebSocket parsing the messages

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        private void ThreadPublicMessageReader()
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
                    if (concurrentQueueMessagePublicWebSocket == null ||
                        concurrentQueueMessagePublicWebSocket.Count == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (!concurrentQueueMessagePublicWebSocket.TryDequeue(out string _message))
                    {
                        continue;
                    }

                    Category category = Category.linear;
                    string message = _message;

                    if (_message.EndsWith(".SPOT"))
                    {
                        category = Category.spot;
                        message = _message.Replace("}.SPOT", "}");
                    }

                    ResponseWebSocketMessage<object> response =
                     JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    if (response.topic != null)
                    {
                        if (response.topic.Contains("publicTrade"))
                        {
                            if (category == Category.spot)
                            {
                                _concurrentQueueTradesSpot.Enqueue(message);
                            }
                            else
                            {
                                _concurrentQueueTradesLinear.Enqueue(message);
                            }

                            continue;
                        }
                        else if (response.topic.Contains("orderbook"))
                        {

                            if (category == Category.spot)
                            {
                                _concurrentQueueMessageOrderBookSpot?.Enqueue(_message);
                            }
                            else if (category == Category.linear)
                            {
                                _concurrentQueueMessageOrderBookLinear.Enqueue(_message);
                            }

                            continue;
                        }
                        continue;
                    }

                    SubscribleMessage subscribleMessage =
                       JsonConvert.DeserializeAnonymousType(message, new SubscribleMessage());

                    if (subscribleMessage.op != null)
                    {
                        if (subscribleMessage.success == "false")
                        {
                            if (subscribleMessage.ret_msg.Contains("already"))
                            {
                                continue;
                            }
                            SendLogMessage("WebSocket Error: " + subscribleMessage.ret_msg, LogMessageType.Error);
                        }

                        continue;
                    }
                    /*if (subscribleMessage.op == "pong")
                    {
                        continue;
                    }*/
                }
                catch (Exception ex)
                {
                    Thread.Sleep(3000);
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        private void ThreadPrivateMessageReader()
        {
            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(5000);
                    continue;
                }

                try
                {
                    if (concurrentQueueMessagePrivateWebSocket == null
                  || concurrentQueueMessagePrivateWebSocket.IsEmpty
                  || concurrentQueueMessagePrivateWebSocket.Count == 0)
                    {
                        try
                        {
                            Thread.Sleep(1);
                        }
                        catch
                        {
                            return;
                        }
                        continue;
                    }

                    if (!concurrentQueueMessagePrivateWebSocket.TryDequeue(out string message))
                    {
                        continue;
                    }
                    SubscribleMessage subscribleMessage =
                      JsonConvert.DeserializeAnonymousType(message, new SubscribleMessage());
                    if (subscribleMessage.op == "pong")
                    {
                        continue;
                    }
                    ResponseWebSocketMessage<object> response =
                      JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    if (response.topic != null)
                    {
                        if (response.topic.Contains("execution"))
                        {
                            UpdateMyTrade(message);
                            continue;
                        }
                        else if (response.topic.Contains("order"))
                        {
                            UpdateOrder(message);
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(3000);
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }
        }

        private void UpdateMyTrade(string message)
        {
            try
            {
                ResponseWebSocketMyMessage<List<ResponseMyTrades>> responseMyTrades = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMyMessage<List<ResponseMyTrades>>());

                for (int i = 0; i < responseMyTrades.data.Count; i++)
                {
                    MyTrade myTrade = new MyTrade();

                    if (responseMyTrades.data[i].category == Category.spot.ToString())
                    {
                        myTrade.SecurityNameCode = responseMyTrades.data[i].symbol;
                    }
                    else
                    {
                        myTrade.SecurityNameCode = responseMyTrades.data[i].symbol + ".P";
                    }

                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseMyTrades.data[i].execTime));
                    myTrade.NumberOrderParent = responseMyTrades.data[i].orderId;
                    myTrade.NumberTrade = responseMyTrades.data[i].execId;
                    myTrade.Price = responseMyTrades.data[i].execPrice.ToDecimal();
                    myTrade.Side = responseMyTrades.data[i].side.ToUpper().Equals("BUY") ? Side.Buy : Side.Sell;

                    if (responseMyTrades.data[i].category == Category.spot.ToString() && myTrade.Side == Side.Buy && !string.IsNullOrWhiteSpace(responseMyTrades.data[i].execFee))   // комиссия на споте при покупке берется с купленой монеты
                    {
                        myTrade.Volume = responseMyTrades.data[i].execQty.ToDecimal() - responseMyTrades.data[i].execFee.ToDecimal();
                        int decimalVolum = GetDecimalsVolume(responseMyTrades.data[i].execQty);
                        if (decimalVolum > 0)
                        {
                            myTrade.Volume = Math.Floor(myTrade.Volume * (decimal)Math.Pow(10, decimalVolum)) / (decimal)Math.Pow(10, decimalVolum);
                        }
                    }
                    else
                    {
                        myTrade.Volume = responseMyTrades.data[i].execQty.ToDecimal();
                    }

                    MyTradeEvent?.Invoke(myTrade);
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
                ResponseWebSocketMyMessage<List<ResponseOrder>> responseMyTrades = JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMyMessage<List<ResponseOrder>>());

                for (int i = 0; i < responseMyTrades.data.Count; i++)
                {
                    OrderStateType stateType = OrderStateType.None;

                    stateType = responseMyTrades.data[i].orderStatus.ToUpper() switch
                    {
                        "CREATED" => OrderStateType.Active,
                        "NEW" => OrderStateType.Active,
                        "ORDER_NEW" => OrderStateType.Active,
                        "PARTIALLYFILLED" => OrderStateType.Active,
                        "FILLED" => OrderStateType.Done,
                        "ORDER_FILLED" => OrderStateType.Done,
                        "CANCELLED" => OrderStateType.Cancel,
                        "ORDER_CANCELLED" => OrderStateType.Cancel,
                        "PARTIALLYFILLEDCANCELED" => OrderStateType.Partial,
                        "REJECTED" => OrderStateType.Fail,
                        "ORDER_REJECTED" => OrderStateType.Fail,
                        "ORDER_FAILED" => OrderStateType.Fail,
                        _ => OrderStateType.Cancel,
                    };

                    Order newOrder = new Order();
                    newOrder.SecurityNameCode = responseMyTrades.data[i].symbol + (responseMyTrades.data[i].category.ToLower() == Category.spot.ToString() ? "" : ".P");
                    newOrder.TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseMyTrades.data[i].createdTime));

                    if (stateType == OrderStateType.Active)
                    {
                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseMyTrades.data[i].updatedTime));
                    }

                    if (stateType == OrderStateType.Cancel)
                    {
                        newOrder.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseMyTrades.data[i].updatedTime));
                    }

                    try
                    {
                        newOrder.NumberUser = Convert.ToInt32(responseMyTrades.data[i].orderLinkId);
                    }
                    catch
                    {
                        // ignore
                    }

                    newOrder.TypeOrder = responseMyTrades.data[i].orderType.ToLower() == "market" ? OrderPriceType.Market : OrderPriceType.Limit;
                    newOrder.NumberMarket = responseMyTrades.data[i].orderId;
                    newOrder.Side = responseMyTrades.data[i].side.ToUpper().Contains("BUY") ? Side.Buy : Side.Sell;
                    newOrder.State = stateType;

                    newOrder.Price = responseMyTrades.data[i].price.ToDecimal();
                    newOrder.Volume = responseMyTrades.data[i].qty.ToDecimal();
                    newOrder.ServerType = ServerType.Bybit;
                    newOrder.PortfolioNumber = "BybitUNIFIED";

                    MyOrderEvent?.Invoke(newOrder);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        // market depths parsing

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        private void ThreadMessageReaderOrderBookSpot()
        {
            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                }

                try
                {
                    if (_concurrentQueueMessageOrderBookSpot == null
                        || _concurrentQueueMessageOrderBookSpot.IsEmpty
                        || _concurrentQueueMessageOrderBookSpot.Count == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string _message;

                    if (!_concurrentQueueMessageOrderBookSpot.TryDequeue(out _message))
                    {
                        continue;
                    }

                    Category category = Category.spot;

                    string message = _message.Replace("}.SPOT", "}");

                    ResponseWebSocketMessage<object> response =
                        JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    UpdateOrderBook(message, response, category);

                    while (_concurrentQueueMessageOrderBookSpot?.Count > 10000)
                    {
                        _concurrentQueueMessageOrderBookSpot.TryDequeue(out _message);
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        private void ThreadMessageReaderOrderBookLinear()
        {
            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                }

                try
                {
                    if (_concurrentQueueMessageOrderBookLinear == null
                        || _concurrentQueueMessageOrderBookLinear.IsEmpty
                        || _concurrentQueueMessageOrderBookLinear.Count == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    if (!_concurrentQueueMessageOrderBookLinear.TryDequeue(out message))
                    {
                        continue;
                    }

                    Category category = Category.linear;

                    ResponseWebSocketMessage<object> response =
                        JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    UpdateOrderBook(message, response, category);

                    while (_concurrentQueueMessageOrderBookLinear.Count > 10000)
                    {
                        _concurrentQueueMessageOrderBookLinear.TryDequeue(out message);
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }
        }

        private ConcurrentQueue<string> _concurrentQueueMessageOrderBookSpot;

        private ConcurrentQueue<string> _concurrentQueueMessageOrderBookLinear;

        private Dictionary<string, MarketDepth> _listMarketDepthSpot = new Dictionary<string, MarketDepth>();

        private Dictionary<string, MarketDepth> _listMarketDepthLinear = new Dictionary<string, MarketDepth>();

        private void UpdateOrderBook(string message, ResponseWebSocketMessage<object> response, Category category)
        {
            CultureInfo cultureInfo = new CultureInfo("en-US");

            ResponseWebSocketMessage<ResponseOrderBook> responseDepth =
                              JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<ResponseOrderBook>());

            string[] topic = response.topic.Split('.');
            string sec = topic[2];

            if (category == Category.linear)
            {
                sec = sec + ".P";
            }

            MarketDepth marketDepth = null;

            if (category == Category.spot)
            {
                if (!_listMarketDepthSpot.TryGetValue(sec, out marketDepth))
                {
                    marketDepth = new MarketDepth();
                    marketDepth.SecurityNameCode = sec;
                    _listMarketDepthSpot.Add(sec, marketDepth);
                }
            }
            else if (category == Category.linear)
            {
                if (!_listMarketDepthLinear.TryGetValue(sec, out marketDepth))
                {
                    marketDepth = new MarketDepth();
                    marketDepth.SecurityNameCode = sec;
                    _listMarketDepthLinear.Add(sec, marketDepth);
                }
            }

            if (response.type == "snapshot")
            {
                marketDepth.Asks.Clear();
                marketDepth.Bids.Clear();
            }

            marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp((long)responseDepth.ts.ToDecimal());

            bool needSortAsk = false;
            bool needSortBid = false;

            if (responseDepth.data.a.Length > 1)
            {
                for (int i = 0; i < (responseDepth.data.a.Length / 2); i++)
                {
                    decimal.TryParse(responseDepth.data.a[i, 0], System.Globalization.NumberStyles.Number, cultureInfo, out decimal aPrice);
                    decimal.TryParse(responseDepth.data.a[i, 1], System.Globalization.NumberStyles.Number, cultureInfo, out decimal aAsk);

                    if (marketDepth.Asks.Exists(a => a.Price == aPrice))
                    {
                        if (aAsk == 0)
                        {
                            marketDepth.Asks.RemoveAll(a => a.Price == aPrice);
                        }
                        else
                        {
                            marketDepth.Asks.Find(a => a.Price == aPrice)
                                   .Ask = aAsk;
                        }
                    }
                    else
                    {
                        MarketDepthLevel marketDepthLevel = new MarketDepthLevel();
                        marketDepthLevel.Ask = aAsk;
                        marketDepthLevel.Price = aPrice;
                        marketDepth.Asks.Add(marketDepthLevel);
                        needSortAsk = true;
                    }

                    marketDepth.Bids.RemoveAll(a => a.Price == aPrice && aPrice != 0);
                }
            }
            if (responseDepth.data.b.Length > 1)
            {
                for (int i = 0; i < (responseDepth.data.b.Length / 2); i++)
                {
                    decimal.TryParse(responseDepth.data.b[i, 0], System.Globalization.NumberStyles.Number, cultureInfo, out decimal bPrice);
                    decimal.TryParse(responseDepth.data.b[i, 1], System.Globalization.NumberStyles.Number, cultureInfo, out decimal bBid);

                    if (marketDepth.Bids.Exists(b => b.Price == bPrice))
                    {
                        if (bBid == 0)
                        {
                            marketDepth.Bids.RemoveAll(b => b.Price == bPrice);
                        }
                        else
                        {
                            marketDepth.Bids.Find(b => b.Price == bPrice)
                                .Bid = bBid;
                        }
                    }
                    else
                    {
                        MarketDepthLevel marketDepthLevel = new MarketDepthLevel();
                        marketDepthLevel.Bid = bBid;
                        marketDepthLevel.Price = bPrice;
                        marketDepth.Bids.Add(marketDepthLevel);
                        needSortBid = true;
                    }

                    marketDepth.Asks.RemoveAll(a => a.Price == bPrice && bPrice != 0);
                }
            }

            if (needSortAsk && marketDepth.Asks.Count > 1)
            {
                marketDepth.Asks.RemoveAll(a => a.Ask == 0);
                marketDepth.Asks.Sort((x, y) => x.Price > y.Price ? 1 : -1);
            }

            if (needSortBid && marketDepth.Bids.Count > 1)
            {
                marketDepth.Bids.RemoveAll(a => a.Bid == 0);
                marketDepth.Bids.Sort((x, y) => x.Price > y.Price ? -1 : 1);
            }

            int _glassDeep = marketDepthDeep;

            if (marketDepthDeep > 20)
            {
                _glassDeep = 20;
            }

            while (marketDepth.Asks.Count > _glassDeep)
            {
                marketDepth.Asks.RemoveAt(_glassDeep);
            }

            while (marketDepth.Bids.Count > _glassDeep)
            {
                marketDepth.Bids.RemoveAt(_glassDeep);
            }

            if (marketDepth.Asks.Count == 0)
            {
                return;
            }

            if (marketDepth.Bids.Count == 0)
            {
                return;
            }

            if (_lastMdTime != DateTime.MinValue &&
                _lastMdTime >= marketDepth.Time)
            {
                marketDepth.Time = _lastMdTime.AddTicks(1);
            }

            _lastMdTime = marketDepth.Time;

            if (_concurrentQueueMessageOrderBookLinear.Count < 500
                && _concurrentQueueMessageOrderBookSpot.Count < 500)
            {
                MarketDepthEvent?.Invoke(marketDepth.GetCopy());
            }
            else
            {
                MarketDepthEvent?.Invoke(marketDepth);
            }
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        public event Action<MarketDepth> MarketDepthEvent;

        // deals parsing

        private ConcurrentQueue<string> _concurrentQueueTradesSpot = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _concurrentQueueTradesLinear = new ConcurrentQueue<string>();

        private void ThreadMessageReaderTradesSpot()
        {
            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                }

                try
                {
                    if (_concurrentQueueTradesSpot == null
                        || _concurrentQueueTradesSpot.IsEmpty
                        || _concurrentQueueTradesSpot.Count == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (!_concurrentQueueTradesSpot.TryDequeue(out string message))
                    {
                        continue;
                    }

                    Category category = Category.spot;

                    UpdateTrade(message, category);
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }
        }

        private void ThreadMessageReaderTradesLinear()
        {
            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                }

                try
                {
                    if (_concurrentQueueTradesLinear == null
                        || _concurrentQueueTradesLinear.IsEmpty
                        || _concurrentQueueTradesLinear.Count == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (!_concurrentQueueTradesLinear.TryDequeue(out string message))
                    {
                        continue;
                    }

                    Category category = Category.linear;

                    UpdateTrade(message, category);
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }
        }

        private void UpdateTrade(string message, Category category)
        {
            ResponseWebSocketMessageList<ResponseTrade> responseTrade =
                               JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessageList<ResponseTrade>());

            for (int i = 0; i < responseTrade.data.Count; i++)
            {
                ResponseTrade item = responseTrade.data[i];
                {
                    Trade trade = new Trade();
                    trade.Id = item.i;
                    trade.Time = TimeManager.GetDateTimeFromTimeStamp((long)item.T.ToDecimal());
                    trade.Price = item.p.ToDecimal();
                    trade.Volume = item.v.ToDecimal();
                    trade.Side = item.S == "Buy" ? Side.Buy : Side.Sell;

                    if (item.L != null)     // L string Direction of price change.Unique field for future
                    {
                        trade.SecurityNameCode = item.s + (category == Category.linear ? ".P" : "");
                    }
                    else
                    {
                        trade.SecurityNameCode = item.s;
                    }

                    NewTradesEvent?.Invoke(trade);
                }
            }
        }

        public event Action<Trade> NewTradesEvent;

        #endregion 10

        #region 11 Trade

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public void SendOrder(Order order)
        {
            try
            {
                if (order.TypeOrder == OrderPriceType.Iceberg)
                {
                    SendLogMessage("Bybit does't support iceberg orders", LogMessageType.Error);
                    return;
                }

                string side = "Buy";

                if (order.Side == Side.Sell)
                {
                    side = "Sell";
                }

                string type = "Limit";

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    type = "Market";
                }

                Dictionary<string, object> parameters = new Dictionary<string, object>();

                if ((order.SecurityClassCode != null && order.SecurityClassCode.ToLower().Contains(Category.linear.ToString())) || order.SecurityNameCode.EndsWith(".P"))
                {
                    parameters["category"] = Category.linear.ToString();
                }
                else
                {
                    parameters["category"] = Category.spot.ToString();
                }

                parameters["symbol"] = order.SecurityNameCode.Replace(".P", "");
                parameters["positionIdx"] = 0; // hedge_mode;

                bool reduceOnly = false;

                if (_hedgeMode
                    && order.SecurityClassCode == "LinearPerpetual")
                {
                    if (order.PositionConditionType == OrderPositionConditionType.Close)
                    {
                        reduceOnly = true;
                        parameters["positionIdx"] = order.Side == Side.Buy ? "2" : "1";
                    }
                    else
                    {
                        parameters["positionIdx"] = order.Side == Side.Buy ? "1" : "2";
                    }
                }

                parameters["side"] = side;
                parameters["order_type"] = type;
                parameters["qty"] = order.Volume.ToString().Replace(",", ".");

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    parameters["price"] = order.Price.ToString().Replace(",", ".");
                }
                else if ((string)parameters["category"] == Category.spot.ToString())
                {
                    parameters["marketUnit"] = "baseCoin";
                }

                parameters["orderLinkId"] = order.NumberUser.ToString();

                if (_hedgeMode)
                {
                    parameters["reduceOnly"] = reduceOnly;
                }

                DateTime startTime = DateTime.Now;
                JToken place_order_response = CreatePrivateQuery(parameters, HttpMethod.Post, "/v5/order/create");

                string isSuccessful = "ByBit error. The order was not accepted.";

                if (place_order_response != null)
                {
                    isSuccessful = place_order_response.SelectToken("retMsg").Value<string>();
                    if (isSuccessful == "OK")
                    {
                        //Console.WriteLine("SendOrder - " + place_order_response.ToString());
                        /*DateTime placedTime = DateTime.Now;
                        order.State = OrderStateType.Activ;
                        JToken ordChild = place_order_response.SelectToken("result.orderId");
                        order.NumberMarket = ordChild.ToString();
                        order.TimeCreate = DateTimeOffset.FromUnixTimeMilliseconds(place_order_response.SelectToken("time").Value<long>()).UtcDateTime;
                        order.TimeCallBack = DateTimeOffset.FromUnixTimeMilliseconds(place_order_response.SelectToken("time").Value<long>()).UtcDateTime.Add(placedTime.Subtract(startTime));
                        MyOrderEvent?.Invoke(order);
                        */
                        return;
                    }
                }

                //    SendLogMessage($"Order exchange error num {order.NumberUser}\n" + isSuccessful, LogMessageType.Error);
                order.State = OrderStateType.Fail;
                MyOrderEvent?.Invoke(order);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                if ((order.SecurityClassCode != null && order.SecurityClassCode.ToLower().Contains(Category.linear.ToString())) || order.SecurityNameCode.EndsWith(".P"))
                {
                    parameters["category"] = Category.linear.ToString();
                }
                else
                {
                    parameters["category"] = Category.spot.ToString();
                }

                parameters["symbol"] = order.SecurityNameCode.Replace(".P", "");
                parameters["orderLinkId"] = order.NumberUser.ToString();
                parameters["price"] = newPrice.ToString().Replace(",", ".");

                JToken place_order_response = CreatePrivateQuery(parameters, HttpMethod.Post, "/v5/order/amend");

                if (place_order_response != null)
                {
                    string retCode = place_order_response.SelectToken("retCode").Value<string>();
                    string isSuccessful = place_order_response.SelectToken("retMsg").Value<string>();
                    if (isSuccessful == "OK")
                    {
                        order.Price = newPrice;
                        MyOrderEvent?.Invoke(order);
                    }
                    else
                    {
                        SendLogMessage("ChangeOrderPrice Fail. Status: "
                       + retCode + "  " + order.SecurityNameCode, LogMessageType.Error);
                    }
                }
                else
                {
                    SendLogMessage("ChangeOrderPrice Fail. Status: "
                        + "Not change order price. " + order.SecurityNameCode, LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("ChangeOrderPrice Fail. " + order.SecurityNameCode + ex.Message, LogMessageType.Error);
            }
        }

        public void CancelOrder(Order order)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            if ((order.SecurityClassCode != null
                && order.SecurityClassCode.ToLower().Contains(Category.linear.ToString()))
                || order.SecurityNameCode.EndsWith(".P"))
            {
                parameters["category"] = Category.linear.ToString();
            }
            else
            {
                parameters["category"] = Category.spot.ToString();
            }
            parameters["symbol"] = order.SecurityNameCode.Replace(".P", "");

            if (string.IsNullOrEmpty(order.NumberMarket) == false)
            {
                parameters["orderId"] = order.NumberMarket;
            }
            else
            {
                parameters["orderLinkId"] = order.NumberUser.ToString();
            }

            try
            {
                //order.TimeCancel = DateTimeOffset.UtcNow.UtcDateTime;
                JToken place_order_response = CreatePrivateQuery(parameters, HttpMethod.Post, "/v5/order/cancel");

                if (place_order_response != null)
                {
                    string isSuccessful = place_order_response.SelectToken("retMsg").Value<string>();
                    string retCode = place_order_response.SelectToken("retCode").Value<string>();

                    if (isSuccessful == "OK")
                    {
                        order.TimeCancel = DateTimeOffset.FromUnixTimeMilliseconds(place_order_response.SelectToken("time").Value<long>()).UtcDateTime;
                        order.State = OrderStateType.Cancel;
                        MyOrderEvent?.Invoke(order);
                        return;
                    }
                    else if (retCode == "110001" || retCode == "170213")   // "retCode":110001,"retMsg":"order not exists or too late to cancel"
                                                                           //  retCode":170213,"retMsg":"Order does not exist."
                                                                           // The order does not exist (maybe it has not yet been created) or has already been cancelled. Let's ask about its status
                    {
                        GetOrdersState(new List<Order>() { order });
                        return;
                        /*DateTime TimeCancel = DateTimeOffset.FromUnixTimeMilliseconds(place_order_response.SelectToken("time").Value<long>()).UtcDateTime;
                        if ((TimeCancel - order.TimeCreate) > TimeSpan.FromSeconds(minTimeCreateOrders))
                        {
                                order.TimeCancel = DateTimeOffset.FromUnixTimeMilliseconds(place_order_response.SelectToken("time").Value<long>()).UtcDateTime;   // gives an error - removes the previously executed order to canceled
                                order.State = OrderStateType.Cancel;
                                MyOrderEvent?.Invoke(order);
                            return;
                        }*/

                        // If it turns out that the order doesn’t exist, and we created it a few seconds ago, then we don’t do anything with it.

                    }
                    SendLogMessage($" Cancel Order Error. Order num {order.NumberUser}, {order.SecurityNameCode} {isSuccessful}", LogMessageType.Error);
                }
            }
            catch
            {
                SendLogMessage($" Cancel Order Error. Order num {order.NumberUser}, {order.SecurityNameCode}", LogMessageType.Error);
                return;
            }
        }

        public void GetOrdersState(List<Order> orders)
        {
            if (orders == null && orders.Count == 0)
            {
                return;
            }

            Dictionary<string, object> parametrs = new Dictionary<string, object>();

            DateTime serverTime = ServerTime;

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if ((order.SecurityClassCode != null
                    && order.SecurityClassCode.ToLower().Contains(Category.linear.ToString()))
                    || order.SecurityNameCode.EndsWith(".P"))
                {
                    parametrs["category"] = Category.linear.ToString();
                }
                else
                {
                    parametrs["category"] = Category.spot.ToString();
                }

                if (order.State != OrderStateType.Active)
                {
                    continue;
                }

                parametrs["orderLinkId"] = order.NumberUser;

                JToken bOrders = CreatePrivateQuery(parametrs, HttpMethod.Get, "/v5/order/history");

                if (bOrders == null)
                {
                    //        order.State = OrderStateType.Fail;
                    //        MyOrderEvent?.Invoke(order);
                    continue;
                }

                List<JToken> OneOrder = bOrders.SelectToken("result.list").Children().ToList();

                if (OneOrder.Count == 0)
                {
                    continue;
                }

                for (int i1 = 0; i1 < OneOrder.Count; i1++)
                {
                    JToken o = OneOrder[i1];
                    string oStatus = o.SelectToken("orderStatus").ToString();

                    switch (oStatus)
                    {
                        case "Created":
                            order.State = OrderStateType.Active;
                            break;
                        case "Rejected":
                            order.State = OrderStateType.Fail;
                            break;
                        case "New":
                            order.State = OrderStateType.Active;
                            break;
                        case "PartiallyFilled":
                            order.State = OrderStateType.Active;
                            break;
                        case "Filled":
                            order.State = OrderStateType.Done;
                            break;
                        case "Cancelled":
                            order.State = OrderStateType.Cancel;
                            break;
                        case "PendingCancel":
                            order.State = OrderStateType.Cancel;
                            break;
                        default:
                            order.State = OrderStateType.Cancel;
                            break;
                    }

                    MyOrderEvent?.Invoke(order);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            Dictionary<string, object> parametrs = new Dictionary<string, object>();

            if (!security.NameClass.ToLower().Contains(Category.linear.ToString()))
            {
                parametrs["category"] = Category.spot.ToString();
            }
            else
            {
                parametrs["category"] = Category.linear.ToString();
            }

            parametrs.Add("symbol", security.Name.Replace(".P", ""));
            CreatePrivateQuery(parametrs, HttpMethod.Post, "/v5/order/cancel-all");
        }

        public void CancelAllOrders()
        {
            try
            {
                List<Order> ordersOpenAll = new List<Order>();

                List<Order> spotOrders = GetOpenOrders(Category.spot);

                if (spotOrders != null
                    && spotOrders.Count > 0)
                {
                    ordersOpenAll.AddRange(spotOrders);
                }

                List<Order> linearOrders = GetOpenOrders(Category.linear);

                if (linearOrders != null
                    && linearOrders.Count > 0)
                {
                    ordersOpenAll.AddRange(linearOrders);
                }

                for (int i = 0; i < ordersOpenAll.Count; i++)
                {
                    CancelOrder(ordersOpenAll[i]);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void GetOrderStatus(Order order)
        {
            try
            {
                Category category = Category.spot;

                if (order.SecurityNameCode.EndsWith(".P"))
                {
                    category = Category.linear;
                }

                Order newOrder = GetOrderFromHistory(order, category);

                if (newOrder == null)
                {
                    List<Order> openOrders = GetOpenOrders(category);

                    for (int i = 0; openOrders != null && i < openOrders.Count; i++)
                    {
                        if (openOrders[i].NumberUser == order.NumberUser)
                        {
                            newOrder = openOrders[i];
                            break;
                        }
                    }

                    if (newOrder == null)
                    {
                        return;
                    }
                }

                MyOrderEvent?.Invoke(newOrder);

                // check trades

                if (newOrder.State == OrderStateType.Active
                    || newOrder.State == OrderStateType.Partial
                    || newOrder.State == OrderStateType.Done
                    || newOrder.State == OrderStateType.Cancel)
                {
                    List<MyTrade> myTrades = GetMyTradesHistory(newOrder, category);

                    for (int i = 0; myTrades != null && i < myTrades.Count; i++)
                    {
                        MyTradeEvent?.Invoke(myTrades[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {
            try
            {
                List<Order> ordersOpenAll = new List<Order>();

                List<Order> spotOrders = GetOpenOrders(Category.spot);

                if (spotOrders != null
                    && spotOrders.Count > 0)
                {
                    ordersOpenAll.AddRange(spotOrders);
                }

                List<Order> linearOrders = GetOpenOrders(Category.linear);

                if (linearOrders != null
                    && linearOrders.Count > 0)
                {
                    ordersOpenAll.AddRange(linearOrders);
                }

                for (int i = 0; i < ordersOpenAll.Count; i++)
                {
                    MyOrderEvent?.Invoke(ordersOpenAll[i]);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private List<Order> GetOpenOrders(Category category)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            parameters["category"] = category;
            parameters["openOnly"] = "0";

            if (category == Category.linear)
            {
                parameters["settleCoin"] = "USDT";
            }

            JToken orders_response = CreatePrivateQuery(parameters, HttpMethod.Get, "/v5/order/realtime");

            if (orders_response == null)
            {
                return null;
            }

            JToken ordChild = orders_response.SelectToken("result.list");

            List<Order> activeOrders = new List<Order>();

            foreach (JToken order in ordChild)
            {
                Order newOrder = new Order();
                newOrder.State = OrderStateType.Active;
                newOrder.TypeOrder = OrderPriceType.Limit;
                newOrder.PortfolioNumber = "BybitUNIFIED";

                newOrder.NumberMarket = order.SelectToken("orderId").ToString();
                newOrder.SecurityNameCode = order.SelectToken("symbol").ToString();

                if (category == Category.linear
                    && newOrder.SecurityNameCode.EndsWith(".P") == false)
                {
                    newOrder.SecurityNameCode = newOrder.SecurityNameCode + ".P";
                }

                newOrder.Price = order.SelectToken("price").ToString().ToDecimal();
                newOrder.Volume = order.SelectToken("qty").ToString().ToDecimal();

                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.SelectToken("updatedTime").ToString()));
                newOrder.TimeCreate = newOrder.TimeCallBack;

                string numUser = order.SelectToken("orderLinkId").ToString();

                if (string.IsNullOrEmpty(numUser) == false)
                {
                    try
                    {
                        newOrder.NumberUser = Convert.ToInt32(numUser);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                string side = order.SelectToken("side").ToString();

                if (side == "Buy")
                {
                    newOrder.Side = Side.Buy;
                }
                else
                {
                    newOrder.Side = Side.Sell;
                }

                activeOrders.Add(newOrder);
            }

            return activeOrders;
        }

        private Order GetOrderFromHistory(Order orderBase, Category category)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            parameters["category"] = category;
            parameters["symbol"] = orderBase.SecurityNameCode.Replace(".P", "").ToUpper();

            JToken orders_response = CreatePrivateQuery(parameters, HttpMethod.Get, "/v5/order/history");

            if (orders_response == null)
            {
                return null;
            }

            JToken ordChild = orders_response.SelectToken("result.list");

            foreach (JToken order in ordChild)
            {
                Order newOrder = new Order();

                string status = order.SelectToken("orderStatus").ToString();

                OrderStateType stateType = status.ToUpper() switch
                {
                    "CREATED" => OrderStateType.Active,
                    "NEW" => OrderStateType.Active,
                    "ORDER_NEW" => OrderStateType.Active,
                    "PARTIALLYFILLED" => OrderStateType.Active,
                    "FILLED" => OrderStateType.Done,
                    "ORDER_FILLED" => OrderStateType.Done,
                    "CANCELLED" => OrderStateType.Cancel,
                    "ORDER_CANCELLED" => OrderStateType.Cancel,
                    "PARTIALLYFILLEDCANCELED" => OrderStateType.Partial,
                    "REJECTED" => OrderStateType.Fail,
                    "ORDER_REJECTED" => OrderStateType.Fail,
                    "ORDER_FAILED" => OrderStateType.Fail,
                    _ => OrderStateType.Cancel,
                };

                newOrder.State = stateType;
                newOrder.TypeOrder = OrderPriceType.Limit;
                newOrder.PortfolioNumber = "BybitUNIFIED";
                newOrder.NumberMarket = order.SelectToken("orderId").ToString();
                newOrder.SecurityNameCode = order.SelectToken("symbol").ToString();

                if (category == Category.linear
                    && newOrder.SecurityNameCode.EndsWith(".P") == false)
                {
                    newOrder.SecurityNameCode = newOrder.SecurityNameCode + ".P";
                }

                newOrder.Price = order.SelectToken("price").ToString().ToDecimal();
                newOrder.Volume = order.SelectToken("qty").ToString().ToDecimal();
                newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.SelectToken("updatedTime").ToString()));
                newOrder.TimeCreate = newOrder.TimeCallBack;

                string numUser = order.SelectToken("orderLinkId").ToString();

                if (string.IsNullOrEmpty(numUser) == false)
                {
                    try
                    {
                        newOrder.NumberUser = Convert.ToInt32(numUser);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (newOrder.NumberUser != orderBase.NumberUser)
                {
                    continue;
                }

                string side = order.SelectToken("side").ToString();

                if (side == "Buy")
                {
                    newOrder.Side = Side.Buy;
                }
                else
                {
                    newOrder.Side = Side.Sell;
                }

                return newOrder;
            }

            return null;
        }

        private List<MyTrade> GetMyTradesHistory(Order orderBase, Category category)
        {
            if (string.IsNullOrEmpty(orderBase.NumberMarket))
            {
                return null;
            }

            Dictionary<string, object> parameters = new Dictionary<string, object>();

            parameters["category"] = category;
            parameters["symbol"] = orderBase.SecurityNameCode.Replace(".P", "").ToUpper();
            parameters["orderId"] = orderBase.NumberMarket;

            JToken trades_response = CreatePrivateQuery(parameters, HttpMethod.Get, "/v5/execution/list");

            if (trades_response == null)
            {
                return null;
            }

            JToken trChild = trades_response.SelectToken("result.list");

            List<MyTrade> myTrades = new List<MyTrade>();

            foreach (JToken trade in trChild)
            {
                MyTrade newTrade = new MyTrade();
                newTrade.SecurityNameCode = trade.SelectToken("symbol").ToString();

                if (category == Category.linear)
                {
                    newTrade.SecurityNameCode = newTrade.SecurityNameCode + ".P";
                }

                newTrade.NumberTrade = trade.SelectToken("execId").ToString();
                newTrade.NumberOrderParent = orderBase.NumberMarket;
                newTrade.Price = trade.SelectToken("execPrice").ToString().ToDecimal();
                newTrade.Volume = trade.SelectToken("execQty").ToString().ToDecimal();
                newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(trade.SelectToken("execTime").ToString()));

                string side = trade.SelectToken("side").ToString();

                if (side == "Buy")
                {
                    newTrade.Side = Side.Buy;
                }
                else
                {
                    newTrade.Side = Side.Sell;
                }
                myTrades.Add(newTrade);
            }

            return myTrades;
        }

        #endregion 11

        #region 12 Query

        private const string RecvWindow = "50000";

        private RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(50));

        private HttpClientHandler httpClientHandler;

        private HttpClient httpClient;

        private string _httpClientLocker = "httpClientLocker";

        private HttpClient GetHttpClient()
        {
            try
            {
                if (httpClientHandler is null)
                {
                    httpClientHandler = new HttpClientHandler();   // управление экземплярами HttpClient
                }
                if (httpClient is null)
                {
                    httpClient = new HttpClient(httpClientHandler, false); ;   // управление экземплярами HttpClient
                }
                return httpClient;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        public bool CheckApiKeyInformation(string ApiKey)
        {
            string apiFromServer = "";
            _rateGate.WaitToProceed();

            try
            {
                JToken res = CreatePrivateQuery(new Dictionary<string, object>(), HttpMethod.Get, "/v5/user/query-api");

                if (res != null)
                {
                    JToken api = res.SelectToken("result.apiKey");
                    apiFromServer = api.ToString();
                }
                if (apiFromServer.Length < 1 || apiFromServer != ApiKey)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return false;
            }

            return true;
        }

        public JToken CreatePrivateQuery(Dictionary<string, object> parameters, HttpMethod httpMethod, string uri)
        {
            _rateGate.WaitToProceed();

            try
            {
                lock (_httpClientLocker)
                {
                    string timestamp = GetServerTime();
                    HttpRequestMessage request = null;
                    string jsonPayload = "";
                    string signature = "";
                    httpClient = GetHttpClient();

                    if (httpMethod == HttpMethod.Post)
                    {
                        signature = GeneratePostSignature(parameters, timestamp);
                        jsonPayload = parameters.Count > 0 ? JsonConvert.SerializeObject(parameters) : "";
                        request = new HttpRequestMessage(httpMethod, RestUrl + uri);
                        if (parameters.Count > 0)
                        {
                            request.Content = new StringContent(jsonPayload);
                        }
                    }
                    if (httpMethod == HttpMethod.Get)
                    {
                        signature = GenerateGetSignature(parameters, timestamp, PublicKey);
                        jsonPayload = parameters.Count > 0 ? GenerateQueryString(parameters) : "";
                        request = new HttpRequestMessage(httpMethod, RestUrl + uri + $"?" + jsonPayload);
                    }

                    request.Headers.Add("X-BAPI-API-KEY", PublicKey);
                    request.Headers.Add("X-BAPI-SIGN", signature);
                    request.Headers.Add("X-BAPI-SIGN-TYPE", "2");
                    request.Headers.Add("X-BAPI-TIMESTAMP", timestamp);
                    request.Headers.Add("X-BAPI-RECV-WINDOW", RecvWindow);
                    request.Headers.Add("referer", "OsEngine");

                    HttpResponseMessage response = httpClient?.SendAsync(request).Result;

                    if (response == null)
                    {
                        return null;
                    }

                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        LogMessageEvent?.Invoke($"BybitUnified Client {RestUrl + uri + $"?" + jsonPayload} StatusCode:{response.StatusCode.ToString()}", LogMessageType.Connect);
                        return null;
                    }

                    string response_msg = response.Content.ReadAsStringAsync().Result;
                    JToken token = JToken.Parse(response_msg);

                    if (token.SelectToken("retCode").ToString() == "110001" && uri.Contains("order/cancel"))
                    {
                        return token;
                    }

                    if (token.SelectToken("retCode").ToString() == "170213" && uri.Contains("order/cancel"))
                    {
                        return token;
                    }

                    if (token.SelectToken("retCode").ToString() != "0")
                    {
                        LogMessageEvent?.Invoke($"BybitUnified Client {RestUrl + uri + $"?" + jsonPayload} Status:{response_msg}", LogMessageType.Error);
                        return null;
                    }

                    return token;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        public JToken CreatePublicQuery(Dictionary<string, object> parameters, HttpMethod httpMethod, string uri)
        {
            _rateGate.WaitToProceed();

            try
            {
                string jsonPayload = parameters.Count > 0 ? GenerateQueryString(parameters) : "";
                httpClient = GetHttpClient();

                if (httpClient == null)
                {
                    return null;
                }

                lock (_httpClientLocker)
                {
                    HttpRequestMessage request = new HttpRequestMessage(httpMethod, RestUrl + uri + $"?{jsonPayload}");
                    HttpResponseMessage response = httpClient?.SendAsync(request).Result;

                    if (response == null)
                    {
                        return null;
                    }

                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        LogMessageEvent?.Invoke($"BybitUnified Client {RestUrl + uri + $"?" + jsonPayload} StatusCode:{response.StatusCode.ToString()}", LogMessageType.Connect);
                        return null;
                    }

                    string response_msg = response.Content.ReadAsStringAsync().Result;
                    JToken token = JToken.Parse(response_msg);

                    if (token.SelectToken("retCode").ToString() != "0"
                        || token.SelectToken("retMsg").ToString() != "OK")
                    {
                        LogMessageEvent?.Invoke($"BybitUnified Client {RestUrl + uri + $"?" + jsonPayload} StatusCode:{response_msg}", LogMessageType.Error);
                        return null;
                    }

                    return token;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        private string GenerateQueryString(Dictionary<string, object> parameters)
        {
            List<string> pairs = new List<string>();
            string[] keysArray = new string[parameters.Count];
            parameters.Keys.CopyTo(keysArray, 0);

            for (int i = 0; i < keysArray.Length; i++)
            {
                string key = keysArray[i];
                pairs.Add($"{key}={parameters[key]}");
            }

            string res = string.Join("&", pairs);

            return res;
        }

        private string _lockerServerTime = "lockerServerTime";

        public string GetServerTime()
        {
            lock (_lockerServerTime)
            {
                try
                {
                    httpClient = GetHttpClient();
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, RestUrl + "/v5/market/time");
                    long UtcNowUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    JToken timeFromServer = null;

                    HttpResponseMessage response = httpClient?.SendAsync(request).Result;
                    string response_msg = response.Content.ReadAsStringAsync().Result;

                    timeFromServer = JToken.Parse(response_msg);

                    if (timeFromServer == null)
                    {
                        return UtcNowUnixTimeMilliseconds.ToString();
                    }
                    JToken timeStamp = timeFromServer.Root.SelectToken("time");

                    if (long.TryParse(timeStamp.ToString(), out long timestampServer))
                    {
                        return timeStamp.ToString();
                    }

                    return UtcNowUnixTimeMilliseconds.ToString();
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            }
        }

        private string GeneratePostSignature(IDictionary<string, object> parameters, string Timestamp)
        {
            string paramJson = parameters.Count > 0 ? JsonConvert.SerializeObject(parameters) : "";
            string rawData = Timestamp + PublicKey + RecvWindow + paramJson;

            using HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretKey));
            byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return BitConverter.ToString(signature).Replace("-", "").ToLower();
        }

        private string GenerateGetSignature(Dictionary<string, object> parameters, string Timestamp, string ApiKey)
        {
            string queryString = GenerateQueryString(parameters);
            string rawData = Timestamp + ApiKey + RecvWindow + queryString;

            return ComputeSignature(rawData);
        }

        private string ComputeSignature(string data)
        {
            using HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretKey));
            byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));

            return BitConverter.ToString(signature).Replace("-", "").ToLower();
        }

        #endregion 12

        #region 13 Log

        public event Action<string, LogMessageType> LogMessageEvent;

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        #endregion 13
    }

    #region 14 Enum

    public enum Net_type
    {
        MainNet,
        TestNet
    }

    public enum MarginMode
    {
        Cross,
        Isolated
    }

    public enum Category
    {
        spot,
        linear,
        inverse,
        option
    }

    #endregion 14
}