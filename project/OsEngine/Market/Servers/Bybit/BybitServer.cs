/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Bybit.Entities;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


namespace OsEngine.Market.Servers.Bybit
{
    public class BybitServer : AServer
    {
        public BybitServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            BybitServerRealization realization = new BybitServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParameterSecretKey, "");
            CreateParameterEnum("Server type", Net_type.MainNet.ToString(), new List<string>() { Net_type.MainNet.ToString(),
                Net_type.Demo.ToString(), Net_type.Netherlands.ToString(), Net_type.HongKong.ToString(), Net_type.Turkey.ToString(), Net_type.Kazakhstan.ToString() });
            CreateParameterEnum("Margin Mode", MarginMode.Cross.ToString(), new List<string>() { MarginMode.Cross.ToString(), MarginMode.Isolated.ToString() });
            CreateParameterBoolean("Hedge Mode", true);
            ServerParameters[4].ValueChange += BybitServer_ValueChange;
            CreateParameterString("Leverage", "");
            CreateParameterBoolean("Extended Data", false);
            CreateParameterBoolean("Use Options", false);

            ServerParameters[0].Comment = OsLocalization.Market.Label246;
            ServerParameters[1].Comment = OsLocalization.Market.Label247;
            ServerParameters[2].Comment = OsLocalization.Market.Label248;
            ServerParameters[3].Comment = OsLocalization.Market.Label249;
            ServerParameters[4].Comment = OsLocalization.Market.Label250;
            ServerParameters[5].Comment = OsLocalization.Market.Label251;
            ServerParameters[6].Comment = OsLocalization.Market.Label252;
            ServerParameters[7].Comment = OsLocalization.Market.Label253;
        }

        private void BybitServer_ValueChange()
        {
            ((BybitServerRealization)ServerRealization).HedgeMode = ((ServerParameterBool)ServerParameters[4]).Value;
        }
    }

    public class BybitServerRealization : IServerRealization
    {

        #region 1 Constructor, Status, Connection

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

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

            Thread threadMessageReaderOrderBookInverse = new Thread(() => ThreadMessageReaderOrderBookInverse());
            threadMessageReaderOrderBookInverse.Name = "ThreadBybitMessageReaderOrderBookInverse";
            threadMessageReaderOrderBookInverse.Start();

            Thread threadMessageReaderTradesInverse = new Thread(() => ThreadMessageReaderTradesInverse());
            threadMessageReaderTradesInverse.Name = "ThreadBybitMessageReaderTradesInverse";
            threadMessageReaderTradesInverse.Start();

            Thread threadMessageReaderTradesOption = new Thread(() => ThreadMessageReaderTradesOption());
            threadMessageReaderTradesOption.Name = "ThreadBybitMessageReaderTradesOption";
            threadMessageReaderTradesOption.Start();

            Thread threadMessageReaderOrderBookOption = new Thread(() => ThreadMessageReaderOrderBookOption());
            threadMessageReaderOrderBookOption.Name = "ThreadBybitMessageReaderOrderBookOption";
            threadMessageReaderOrderBookOption.Start();
        }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                _myProxy = proxy;

                PublicKey = ((ServerParameterString)ServerParameters[0]).Value;
                SecretKey = ((ServerParameterPassword)ServerParameters[1]).Value;
                net_type = (Net_type)Enum.Parse(typeof(Net_type), ((ServerParameterEnum)ServerParameters[2]).Value);
                margineMode = (MarginMode)Enum.Parse(typeof(MarginMode), ((ServerParameterEnum)ServerParameters[3]).Value);
                HedgeMode = ((ServerParameterBool)ServerParameters[4]).Value;

                httpClientHandler = null;
                httpClient = null;

                _leverage = ((ServerParameterString)ServerParameters[5]).Value.Replace(",", ".");

                if (((ServerParameterBool)ServerParameters[6]).Value == true)
                {
                    _extendedMarketData = true;
                }
                else
                {
                    _extendedMarketData = false;
                }

                if (((ServerParameterBool)ServerParameters[7]).Value == true)
                {
                    _useOptions = true;
                }
                else
                {
                    _useOptions = false;
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
                //SetPositionMode();
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

                if (_webSocketPublicSpot.Count == 0
                    || _webSocketPublicLinear.Count == 0
                    || _webSocketPublicInverse.Count == 0)
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

                WebSocket webSocketPublicInvers = _webSocketPublicInverse[0];

                if (webSocketPublicInvers == null
                    || webSocketPublicInvers?.ReadyState != WebSocketState.Open)
                {
                    Disconnect();
                    return;
                }

                if (_useOptions)
                {
                    if (_webSocketPublicOption.Count == 0)
                    {
                        Disconnect();
                        return;
                    }

                    WebSocket webSocketPublicOption = _webSocketPublicOption[0];

                    if (webSocketPublicOption == null
                        || webSocketPublicOption?.ReadyState != WebSocketState.Open)
                    {
                        Disconnect();
                        return;
                    }
                }

                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();

                    SetPositionMode();
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
            SubscribeSecurityInverse.Clear();
            SubscribedSecurityOption.Clear();
            _subscribedOptionTradeBaseCoins.Clear();

            concurrentQueueMessagePublicWebSocket = new ConcurrentQueue<string>();
            _concurrentQueueMessageOrderBookSpot = new ConcurrentQueue<string>();
            _concurrentQueueMessageOrderBookLinear = new ConcurrentQueue<string>();
            _concurrentQueueMessageOrderBookInverse = new ConcurrentQueue<string>();
            _concurrentQueueMessageOrderBookOption = new ConcurrentQueue<string>();
            concurrentQueueMessagePrivateWebSocket = new ConcurrentQueue<string>();
            _concurrentQueueTickersLinear = new ConcurrentQueue<string>();
            _concurrentQueueTickersInverse = new ConcurrentQueue<string>();
            _concurrentQueueTickersOption = new ConcurrentQueue<string>();

            _concurrentQueueTradesSpot = new ConcurrentQueue<string>();
            _concurrentQueueTradesLinear = new ConcurrentQueue<string>();
            _concurrentQueueTradesInverse = new ConcurrentQueue<string>();
            _concurrentQueueTradesOption = new ConcurrentQueue<string>();
            portfolios = new List<Portfolio>();

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
                SendLogMessage($"Check Bybit API Keys and Unified AccountBalance Settings! {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void SetPositionMode()
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs.Clear();
                parametrs["category"] = Category.linear.ToString();
                parametrs["coin"] = "USDT";
                parametrs["mode"] = HedgeMode == true ? "3" : "0"; //Position mode. 0: Merged Single. 3: Both Sides

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

        private string _leverage;

        private bool _extendedMarketData;

        private bool _useOptions;

        private List<string> _listLinearCurrency = new List<string>() { "USDC", "USDT" };

        private int marketDepthDeep
        {
            get
            {
                if (((ServerParameterBool)ServerParameters[15]).Value)
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

        private string test_Url = "https://api-demo.bybit.com";

        private string Netherlands_Url = "https://api.bybit.nl";

        private string HongKong_Url = "https://api.byhkbit.com";

        private string Turkey_Url = "https://api.bybit-tr.com";

        private string Kazakhstan_Url = "https://api.bybit.kz";

        private string mainWsPublicUrl = "wss://stream.bybit.com/v5/public/";

        private string testWsPublicUrl = "wss://stream.bybit.com/v5/public/";

        private string TurkeyWsPublicUrl = "wss://stream.bybit-tr.com/v5/public/";

        private string KazakhstanWsPublicUrl = "wss://stream.bybit.kz/v5/public/";

        private string mainWsPrivateUrl = "wss://stream.bybit.com/v5/private";

        private string TurkeyWsPrivateUrl = "wss://stream.bybit-tr.com/v5/private";

        private string KazakhstanWsPrivateUrl = "wss://stream.bybit.kz/v5/private";

        private string testWsPrivateUrl = "wss://stream-demo.bybit.com/v5/private";

        private string wsPublicUrl(Category category = Category.spot)
        {
            string url;
            if (net_type == Net_type.MainNet
                 || net_type == Net_type.Netherlands
                 || net_type == Net_type.HongKong)
            {
                url = mainWsPublicUrl;
            }
            else if (net_type == Net_type.Turkey)
            {
                url = TurkeyWsPublicUrl;
            }
            else if (net_type == Net_type.Kazakhstan)
            {
                url = KazakhstanWsPublicUrl;
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
                if (net_type == Net_type.MainNet
                   || net_type == Net_type.Netherlands
                   || net_type == Net_type.HongKong)
                {
                    return mainWsPrivateUrl;
                }
                else if (net_type == Net_type.Turkey)
                {
                    return TurkeyWsPrivateUrl;
                }
                else if (net_type == Net_type.Kazakhstan)
                {
                    return KazakhstanWsPrivateUrl;
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
                else if (net_type == Net_type.Netherlands)
                {
                    return Netherlands_Url;
                }
                else if (net_type == Net_type.HongKong)
                {
                    return HongKong_Url;
                }
                else if (net_type == Net_type.Turkey)
                {
                    return Turkey_Url;
                }
                else if (net_type == Net_type.Kazakhstan)
                {
                    return Kazakhstan_Url;
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

        private List<Security> _securities;

        private void LoadInstrumentsForCategory(Category category)
        {
            Dictionary<string, object> parametrs = new Dictionary<string, object>();
            parametrs.Add("limit", "1000");
            parametrs["category"] = category.ToString();
            
            string cursor = "";

            while (true)
            {
                parametrs["cursor"] = cursor;

                string securityData = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/instruments-info");

                if (securityData != null)
                {
                    ResponseRestMessage<ArraySymbols> responseSymbols = JsonConvert.DeserializeObject<ResponseRestMessage<ArraySymbols>>(securityData);

                    if (responseSymbols != null && responseSymbols.retCode == "0" && responseSymbols.retMsg == "OK")
                    {
                        ConvertSecurities(responseSymbols, category);
                    }
                    else
                    {
                        SendLogMessage($"{category} securities error. Code: {responseSymbols?.retCode}\nMessage: {responseSymbols?.retMsg}", LogMessageType.Error);
                        break; 
                    }

                    if (string.IsNullOrEmpty(responseSymbols.result.nextPageCursor))
                    {
                        break;
                    }
                    else
                    {
                        cursor = responseSymbols.result.nextPageCursor;
                    }
                }
                else
                {
                    break; 
                }
            }
        }

        public void GetSecurities()
        {
            try
            {
                if (_securities == null)
                {
                    _securities = new List<Security>();
                }

                LoadInstrumentsForCategory(Category.spot);
                LoadInstrumentsForCategory(Category.linear);
                LoadInstrumentsForCategory(Category.inverse);

                if (_useOptions)
                {
                    LoadOptionInstruments("BTC");
                    LoadOptionInstruments("ETH");
                    LoadOptionInstruments("SOL");
                }

                SecurityEvent?.Invoke(_securities);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void LoadOptionInstruments(string baseCoin = "BTC")
        {
            Dictionary<string, object> parametrs = new Dictionary<string, object>();
            parametrs.Add("limit", "1000");

            parametrs["category"] = Category.option.ToString();
            parametrs["baseCoin"] = baseCoin;
            parametrs["cursor"] = "";
            bool allLoaded = false;

            while (!allLoaded)
            {
                string security = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/instruments-info");

                if (security != null)
                {
                    ResponseRestMessage<ArraySymbols> responseSymbols = JsonConvert.DeserializeObject<ResponseRestMessage<ArraySymbols>>(security);

                    if (responseSymbols != null
                        && responseSymbols.retMsg == "success")
                    {
                        ConvertSecurities(responseSymbols, Category.option);
                    }
                    else
                    {
                        SendLogMessage($"Option securities error. Code: {responseSymbols.retCode}\n"
                                       + $"Message: {responseSymbols.retMsg}", LogMessageType.Error);
                        allLoaded = true;
                    }

                    if (responseSymbols.result.nextPageCursor == "")
                    {
                        allLoaded = true;
                    }
                    else
                    {
                        parametrs["cursor"] = responseSymbols.result.nextPageCursor; // we need to get the next page
                    }
                }
            }
        }

        private void ConvertSecurities(ResponseRestMessage<ArraySymbols> symbols, Category category)
        {
            try
            {
                for (int i = 0; i < symbols.result.list.Count - 1; i++)
                {
                    Symbols oneSec = symbols.result.list[i];

                    if (oneSec.status.ToLower() == "trading")
                    {
                        Security security = new Security();
                        security.NameFull = oneSec.symbol;
                        
                        if (category == Category.linear
                            || category == Category.inverse)
                        {
                            security.SecurityType = SecurityType.Futures;

                            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                            DateTime expiration = origin.AddMilliseconds(oneSec.deliveryTime.ToDouble());
                            security.Expiration = expiration;
                            security.UnderlyingAsset = oneSec.baseCoin + oneSec.quoteCoin;
                        }
                        else
                        {
                            security.SecurityType = SecurityType.CurrencyPair;
                        }

                        if (category == Category.spot)
                        {
                            security.Name = oneSec.symbol;
                            security.NameId = oneSec.symbol;
                            security.NameClass = oneSec.quoteCoin;
                            security.MinTradeAmount = oneSec.lotSizeFilter.minOrderAmt.ToDecimal();
                        }
                        else if (category == Category.linear)
                        {
                            security.Name = oneSec.symbol + ".P";
                            security.NameId = oneSec.symbol + ".P";

                            if (security.NameFull.EndsWith("PERP"))
                            {
                                security.NameClass = oneSec.contractType + "_PERP";
                            }
                            else
                            {
                                security.NameClass = oneSec.contractType;
                            }

                            security.MinTradeAmount = oneSec.lotSizeFilter.minNotionalValue.ToDecimal();
                        }
                        else if (category == Category.inverse)
                        {
                            security.Name = oneSec.symbol + ".I";
                            security.NameId = oneSec.symbol + ".I";
                            security.NameClass = oneSec.contractType;
                            security.MinTradeAmount = oneSec.lotSizeFilter.minOrderQty.ToDecimal();
                        }
                        else if (category == Category.option)
                        {
                            security.SecurityType = SecurityType.Option;
                            security.Name = oneSec.symbol;
                            security.NameId = oneSec.symbol;
                            security.NameClass = oneSec.quoteCoin + "_Options";
                            security.MinTradeAmount = oneSec.lotSizeFilter.minOrderQty.ToDecimal();
                            security.OptionType = oneSec.optionsType == "Call" ? OptionType.Call : OptionType.Put;

                            // https://bybit-exchange.github.io/docs/api-explorer/v5/market/instrument
                            // get strike price from symbol signature
                            string[] tokens = oneSec.symbol.Split('-');

                            security.UnderlyingAsset = oneSec.baseCoin + (oneSec.quoteCoin == "USDT" ? "USDT" : "") + "-" + tokens[1] + ".P";

                            // Strike price is always the 3rd token
                            string strikeStr = tokens[2].Trim();
                            security.Strike = strikeStr.ToDecimal();

                            // set expiration/delivery
                            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                            DateTime expiration = origin.AddMilliseconds(oneSec.deliveryTime.ToDouble());
                            security.Expiration = expiration;
                        }
                        else
                        {
                            security.NameClass = oneSec.contractType;
                        }

                        int.TryParse(oneSec.priceScale, out int ps);
                        security.Decimals = ps;

                        security.PriceStep = oneSec.priceFilter.tickSize.ToDecimal();
                        security.PriceStepCost = oneSec.priceFilter.tickSize.ToDecimal();

                        security.MinTradeAmountType = MinTradeAmountType.C_Currency;

                        if (oneSec.lotSizeFilter.qtyStep != null)
                        {
                            security.DecimalsVolume = GetDecimalsVolume(oneSec.lotSizeFilter.qtyStep);
                            security.VolumeStep = oneSec.lotSizeFilter.qtyStep.ToDecimal();
                        }
                        else
                        {
                            security.DecimalsVolume = oneSec.lotSizeFilter.basePrecision.DecimalsCount();
                            security.VolumeStep = oneSec.lotSizeFilter.basePrecision.ToDecimal();
                        }

                        security.State = SecurityStateType.Activ;
                        security.Exchange = ServerType.Bybit.ToString();
                        security.Lot = 1;

                        _securities.Add(security);
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

        private decimal GetVolumeStepByVolumeDecimals(int volumeDecimals)
        {
            if (volumeDecimals == 0)
            {
                return 1;
            }

            string result = "0.";

            for (int i = 0; i < volumeDecimals; i++)
            {
                if (i + 1 == volumeDecimals)
                {
                    result += "1";
                }
                else
                {
                    result += "0";
                }
            }

            return result.ToDecimal();
        }

        #endregion 3

        #region 4 Portfolios

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

                if (portfolios.Count == 0)
                {
                    Thread.Sleep(3000);
                    continue;
                }

                try
                {
                    Thread.Sleep(5000);
                    CreateQueryPortfolio(false);
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
            CreateQueryPortfolio(true);
        }

        private void CreateQueryPortfolio(bool IsUpdateValueBegin)
        {
            try
            {
                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs["accountType"] = "UNIFIED";
                string balanceQuery = CreatePrivateQuery(parametrs, HttpMethod.Get, "/v5/account/wallet-balance");

                if (balanceQuery == null)
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

                        if (IsUpdateValueBegin)
                        {
                            newPB.ValueBegin = oldPB.ValueBegin;
                        }

                        newPB.ValueBlocked = oldPB.ValueBlocked;
                        newPB.ValueCurrent = 0;
                        newp.SetNewPosition(newPB);
                    }

                    _portfolios.Add(newp);
                }

                ResponseRestMessageList<AccountBalance> responseAccountBalance = JsonConvert.DeserializeObject<ResponseRestMessageList<AccountBalance>>(balanceQuery);

                if (responseAccountBalance != null
                        && responseAccountBalance.retCode == "0"
                        && responseAccountBalance.retMsg == "OK")
                {
                    for (int j = 0; responseAccountBalance != null && j < responseAccountBalance.result.list.Count; j++)
                    {
                        AccountBalance item = responseAccountBalance.result.list[j];
                        string portNumber = "Bybit" + item.accountType;
                        Portfolio portfolio = BybitPortfolioCreator(item, portNumber, IsUpdateValueBegin);
                        bool newPort = true;

                        for (int i = 0; i < _portfolios.Count; i++)
                        {
                            if (_portfolios[i].Number == portNumber)
                            {
                                _portfolios[i].ValueBlocked = portfolio.ValueBlocked;
                                _portfolios[i].ValueCurrent = portfolio.ValueCurrent;
                                _portfolios[i].UnrealizedPnl = portfolio.UnrealizedPnl;
                                portfolio = _portfolios[i];
                                newPort = false;
                                break;
                            }
                        }

                        if (newPort)
                        {
                            _portfolios.Add(portfolio);
                        }

                        List<PositionOnBoard> PositionOnBoard = GetPositionsLinear(portfolio.Number, IsUpdateValueBegin);
                        PositionOnBoard.AddRange(GetPositionsInverse(portfolio.Number, IsUpdateValueBegin));
                        PositionOnBoard.AddRange(GetPositionsSpot(item.coin, portfolio.Number, IsUpdateValueBegin));

                        for (int i = 0; i < PositionOnBoard.Count; i++)
                        {
                            portfolio.SetNewPosition(PositionOnBoard[i]);
                        }
                    }

                    portfolios.Clear();
                    portfolios = _portfolios;
                    PortfolioEvent?.Invoke(_portfolios);
                }
                else
                {
                    SendLogMessage($"CreateQueryPortfolio>. Portfolio error. Code: {responseAccountBalance.retCode}\n"
                            + $"Message: {responseAccountBalance.retMsg}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"CreateQueryPortfolio>. Portfolio request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private static Portfolio BybitPortfolioCreator(AccountBalance data, string portfolioName, bool IsUpdateValueBegin)
        {
            try
            {
                Portfolio portfolio = new Portfolio();
                portfolio.Number = portfolioName;

                if (IsUpdateValueBegin)
                {
                    if (data.totalEquity.Length > 0)
                    {
                        portfolio.ValueBegin = Math.Round(data.totalEquity.ToDecimal(), 4);
                    }
                    else
                    {
                        portfolio.ValueBegin = 1;
                    }
                }

                if (data.totalEquity.Length > 0)
                {
                    portfolio.ValueCurrent = Math.Round(data.totalEquity.ToDecimal(), 4);
                }
                else
                {
                    portfolio.ValueCurrent = 1;
                }

                if (data.totalInitialMargin.Length > 0)
                {
                    portfolio.ValueBlocked = Math.Round(data.totalInitialMargin.ToDecimal(), 4);
                }
                else
                {
                    portfolio.ValueBlocked = 0;
                }

                if (data.totalPerpUPL.Length > 0)
                {
                    decimal.TryParse(data.totalPerpUPL, System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out portfolio.UnrealizedPnl);
                }
                else
                {
                    portfolio.UnrealizedPnl = 0;
                }

                return portfolio;
            }
            catch
            {
                return new Portfolio();
            }
        }

        private List<PositionOnBoard> GetPositionsInverse(string portfolioNumber, bool IsUpdateValueBegin)
        {
            List<PositionOnBoard> positionOnBoards = new List<PositionOnBoard>();

            try
            {
                Dictionary<string, object> parametrs = new Dictionary<string, object>();

                parametrs["category"] = Category.inverse;

                if (parametrs.ContainsKey("cursor"))
                {
                    parametrs.Remove("cursor");
                }

                string nextPageCursor = "";

                do
                {
                    string positionQuery = CreatePrivateQuery(parametrs, HttpMethod.Get, "/v5/position/list");

                    if (positionQuery == null)
                    {
                        return positionOnBoards;
                    }

                    ResponseRestMessageList<PositionOnBoardResult> responsePositionOnBoard = JsonConvert.DeserializeObject<ResponseRestMessageList<PositionOnBoardResult>>(positionQuery);

                    if (responsePositionOnBoard != null
                    && responsePositionOnBoard.retCode == "0"
                    && responsePositionOnBoard.retMsg == "OK")
                    {
                        List<PositionOnBoard> poses = new List<PositionOnBoard>();

                        for (int i = 0; i < responsePositionOnBoard.result.list.Count; i++)
                        {
                            PositionOnBoardResult posJson = responsePositionOnBoard.result.list[i];

                            PositionOnBoard pos = new PositionOnBoard();

                            pos.PortfolioName = portfolioNumber;
                            pos.SecurityNameCode = posJson.symbol + ".I";

                            if (IsUpdateValueBegin)
                            {
                                pos.ValueBegin = posJson.size.ToDecimal() * (posJson.side == "Buy" ? 1 : -1);
                            }

                            pos.UnrealizedPnl = posJson.unrealisedPnl.ToDecimal();
                            pos.ValueCurrent = posJson.size.ToDecimal() * (posJson.side == "Buy" ? 1 : -1);

                            poses.Add(pos);
                        }

                        if (poses != null && poses.Count > 0)
                        {
                            positionOnBoards.AddRange(poses);
                        }

                        nextPageCursor = responsePositionOnBoard.result.nextPageCursor;

                        if (nextPageCursor.Length > 1)
                        {
                            parametrs["cursor"] = nextPageCursor;
                        }
                    }
                    else
                    {
                        SendLogMessage($"GetPositionsInverse>. Position error. Code: {responsePositionOnBoard.retCode}\n"
                                + $"Message: {responsePositionOnBoard.retMsg}", LogMessageType.Error);
                    }

                } while (nextPageCursor.Length > 1);

                return positionOnBoards;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Position request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return positionOnBoards;
            }
        }

        private List<PositionOnBoard> GetPositionsSpot(List<Coin> coinList, string portfolioNumber, bool IsUpdateValueBegin)
        {
            try
            {
                List<PositionOnBoard> pb = new List<PositionOnBoard>();

                for (int j2 = 0; j2 < coinList.Count; j2++)
                {
                    Coin item2 = coinList[j2];
                    PositionOnBoard positions = new PositionOnBoard();
                    positions.PortfolioName = portfolioNumber;
                    positions.SecurityNameCode = item2.coin;

                    if (IsUpdateValueBegin)
                    {
                        decimal.TryParse(item2.walletBalance, System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out positions.ValueBegin);
                    }

                    decimal.TryParse(item2.equity, System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out positions.ValueCurrent);
                    decimal.TryParse(item2.locked, System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out positions.ValueBlocked);
                    decimal.TryParse(item2.unrealisedPnl, System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture, out positions.UnrealizedPnl);
                    pb.Add(positions);
                }

                return pb;
            }
            catch
            {
                return null;
            }
        }

        private List<PositionOnBoard> GetPositionsLinear(string portfolioNumber, bool IsUpdateValueBegin)
        {
            List<PositionOnBoard> positionOnBoards = new List<PositionOnBoard>();

            try
            {
                Dictionary<string, object> parametrs = new Dictionary<string, object>();

                for (int i = 0; i < _listLinearCurrency.Count; i++)
                {
                    parametrs["settleCoin"] = _listLinearCurrency[i];
                    parametrs["category"] = Category.linear;
                    parametrs["limit"] = 10;

                    if (parametrs.ContainsKey("cursor"))
                    {
                        parametrs.Remove("cursor");
                    }

                    string nextPageCursor = "";

                    do
                    {
                        string positionQuery = CreatePrivateQuery(parametrs, HttpMethod.Get, "/v5/position/list");

                        if (positionQuery == null)
                        {
                            return positionOnBoards;
                        }

                        ResponseRestMessageList<PositionOnBoardResult> responsePositionOnBoard = JsonConvert.DeserializeObject<ResponseRestMessageList<PositionOnBoardResult>>(positionQuery);

                        if (responsePositionOnBoard != null
                        && responsePositionOnBoard.retCode == "0"
                        && responsePositionOnBoard.retMsg == "OK")
                        {
                            List<PositionOnBoard> positions = CreatePosOnBoard(responsePositionOnBoard.result.list, portfolioNumber, IsUpdateValueBegin);

                            if (positions != null && positions.Count > 0)
                            {
                                positionOnBoards.AddRange(positions);
                            }

                            nextPageCursor = responsePositionOnBoard.result.nextPageCursor;

                            if (nextPageCursor.Length > 1)
                            {
                                parametrs["cursor"] = nextPageCursor;
                            }
                        }
                        else
                        {
                            SendLogMessage($"GetPositionsLinear>. Position error. Code: {responsePositionOnBoard.retCode}\n"
                                    + $"Message: {responsePositionOnBoard.retMsg}", LogMessageType.Error);
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

        private List<PositionOnBoard> CreatePosOnBoard(List<PositionOnBoardResult> positions, string potrolioNumber, bool IsUpdateValueBegin)
        {
            List<PositionOnBoard> poses = new List<PositionOnBoard>();

            for (int i = 0; i < positions.Count; i++)
            {
                PositionOnBoardResult posJson = positions[i];

                PositionOnBoard pos = new PositionOnBoard();

                pos.PortfolioName = potrolioNumber;

                if (HedgeMode
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

                if (IsUpdateValueBegin)
                {
                    pos.ValueBegin = posJson.size.ToDecimal() * (posJson.side == "Buy" ? 1 : -1);
                }

                pos.UnrealizedPnl = posJson.unrealisedPnl.ToDecimal();
                pos.ValueCurrent = posJson.size.ToDecimal() * (posJson.side == "Buy" ? 1 : -1);

                poses.Add(pos);
            }

            return poses;
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion 4

        #region 5 Data

        private RateGate _rateGateGetCandleHistory = new RateGate(5, TimeSpan.FromMilliseconds(100));
        private string _rateGateGetCandleHistoryLocker = "_rateGateGetCandleHistoryLocker";

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            if (security.SecurityType == SecurityType.Option)
            {
                return new List<Candle>(); // no option history
            }

            lock (_rateGateGetCandleHistoryLocker)
            {
                _rateGateGetCandleHistory.WaitToProceed();
            }

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
                else if (nameSec.EndsWith(".I"))
                {
                    category = Category.inverse.ToString();
                }

                if (!supported_intervals.ContainsKey(Convert.ToInt32(tf.TotalMinutes)))
                {
                    return null;
                }

                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs["category"] = category;
                parametrs["symbol"] = nameSec.Split('.')[0];
                parametrs["interval"] = supported_intervals[Convert.ToInt32(tf.TotalMinutes)];
                parametrs["start"] = ((DateTimeOffset)timeEnd.AddMinutes(tf.TotalMinutes * -1 * CountToLoad).ToUniversalTime()).ToUnixTimeMilliseconds();
                parametrs["end"] = ((DateTimeOffset)timeEnd.ToUniversalTime()).ToUnixTimeMilliseconds();
                parametrs["limit"] = 1000;

                string candlesQuery = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/kline");

                if (candlesQuery == null)
                {
                    return new List<Candle>();
                }

                List<Candle> candles = GetListCandles(candlesQuery);

                if (candles == null || candles.Count == 0)
                {
                    return null;
                }

                return GetListCandles(candlesQuery);
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
                else if (security.Name.EndsWith(".I"))
                {
                    category = Category.inverse.ToString();
                }

                if (!supported_intervals.ContainsKey(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes))
                {
                    return null;
                }

                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs["category"] = category;
                parametrs["symbol"] = security.Name.Split('.')[0];
                parametrs["interval"] = supported_intervals[timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes];
                parametrs["limit"] = 1000;
                List<Candle> candles = new List<Candle>();
                parametrs["start"] = TimeManager.GetTimeStampMilliSecondsToDateTime(startTime);
                parametrs["end"] = TimeManager.GetTimeStampMilliSecondsToDateTime(endTime);

                do
                {
                    string candlesQuery = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/kline");

                    if (candlesQuery == null)
                    {
                        break;
                    }

                    List<Candle> newCandles = GetListCandles(candlesQuery);

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
        }

        private List<Candle> GetListCandles(string candlesQuery)
        {
            List<Candle> candles = new List<Candle>();

            try
            {
                ResponseRestMessageList<List<string>> response = JsonConvert.DeserializeObject<ResponseRestMessageList<List<string>>>(candlesQuery);

                if (response != null
                        && response.retCode == "0"
                        && response.retMsg == "OK")
                {
                    for (int i = 0; i < response.result.list.Count; i++)
                    {
                        List<string> oneSec = response.result.list[i];

                        Candle candle = new Candle();

                        candle.TimeStart = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(oneSec[0].ToString())).UtcDateTime;
                        candle.Open = oneSec[1].ToString().ToDecimal();
                        candle.High = oneSec[2].ToString().ToDecimal();
                        candle.Low = oneSec[3].ToString().ToDecimal();
                        candle.Close = oneSec[4].ToString().ToDecimal();
                        candle.Volume = oneSec[5].ToString().ToDecimal();
                        candle.State = CandleState.Finished;

                        candles.Add(candle);
                    }

                    candles.Reverse();
                }
                else
                {
                    SendLogMessage($"GetListCandles>. Candles error. Code: {response.retCode}\n"
                            + $"Message: {response.retMsg}", LogMessageType.Error);
                }

            }
            catch (Exception ex)
            {
                SendLogMessage($"GetListCandles>. Candles request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
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

        private List<WebSocket> _webSocketPublicInverse = new List<WebSocket>();

        private List<WebSocket> _webSocketPublicOption = new List<WebSocket>();

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
                    _concurrentQueueMessageOrderBookInverse = new ConcurrentQueue<string>();
                    _concurrentQueueMessageOrderBookOption = new ConcurrentQueue<string>();
                }

                _webSocketPublicSpot.Add(CreateNewSpotPublicSocket());
                _webSocketPublicLinear.Add(CreateNewLinearPublicSocket());
                _webSocketPublicInverse.Add(CreateNewInversePublicSocket());

                if (_useOptions)
                {
                    _webSocketPublicOption.Add(CreateNewOptionPublicSocket());
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewSpotPublicSocket()
        {
            WebSocket webSocketPublicSpot = new WebSocket(wsPublicUrl(Category.spot));

            if (_myProxy != null)
            {
                webSocketPublicSpot.SetProxy(_myProxy);
            }

            webSocketPublicSpot.OnOpen += WebSocketPublic_Opened;
            webSocketPublicSpot.OnMessage += WebSocketPublic_MessageReceivedSpot;
            webSocketPublicSpot.OnError += WebSocketPublic_Error;
            webSocketPublicSpot.OnClose += WebSocketPublic_Closed;

            webSocketPublicSpot.ConnectAsync();

            return webSocketPublicSpot;
        }

        private WebSocket CreateNewLinearPublicSocket()
        {
            WebSocket webSocketPublicLinear = new WebSocket(wsPublicUrl(Category.linear));

            if (_myProxy != null)
            {
                webSocketPublicLinear.SetProxy(_myProxy);
            }

            webSocketPublicLinear.OnOpen += WebSocketPublic_Opened;
            webSocketPublicLinear.OnMessage += WebSocketPublic_MessageReceivedLinear;
            webSocketPublicLinear.OnError += WebSocketPublic_Error;
            webSocketPublicLinear.OnClose += WebSocketPublic_Closed;

            webSocketPublicLinear.ConnectAsync();

            return webSocketPublicLinear;
        }

        private WebSocket CreateNewInversePublicSocket()
        {
            WebSocket webSocketPublicInverse = new WebSocket(wsPublicUrl(Category.inverse));

            if (_myProxy != null)
            {
                NetworkCredential credential = (NetworkCredential)_myProxy.Credentials;
                webSocketPublicInverse.SetProxy(_myProxy);
            }

            webSocketPublicInverse.OnOpen += WebSocketPublic_Opened;
            webSocketPublicInverse.OnMessage += WebSocketPublicInverse_OnMessage;
            webSocketPublicInverse.OnError += WebSocketPublic_Error;
            webSocketPublicInverse.OnClose += WebSocketPublic_Closed;

            webSocketPublicInverse.ConnectAsync();

            return webSocketPublicInverse;
        }

        private WebSocket CreateNewOptionPublicSocket()
        {
            WebSocket webSocketPublicOption = new WebSocket(wsPublicUrl(Category.option));

            if (_myProxy != null)
            {
                NetworkCredential credential = (NetworkCredential)_myProxy.Credentials;
                webSocketPublicOption.SetProxy(_myProxy);
            }

            webSocketPublicOption.OnOpen += WebSocketPublic_Opened;
            webSocketPublicOption.OnMessage += WebSocketPublicOption_OnMessage;
            webSocketPublicOption.OnError += WebSocketPublic_Error;
            webSocketPublicOption.OnClose += WebSocketPublic_Closed;

            webSocketPublicOption.ConnectAsync();

            return webSocketPublicOption;
        }

        private void CreatePrivateWebSocketConnect()
        {
            try
            {
                if (concurrentQueueMessagePrivateWebSocket == null) concurrentQueueMessagePrivateWebSocket = new ConcurrentQueue<string>();

                webSocketPrivate = new WebSocket(wsPrivateUrl);

                if (_myProxy != null)
                {
                    webSocketPrivate.SetProxy(_myProxy);
                }

                webSocketPrivate.OnMessage += WebSocketPrivate_MessageReceived;
                webSocketPrivate.OnClose += WebSocketPrivate_Closed;
                webSocketPrivate.OnError += WebSocketPrivate_Error;
                webSocketPrivate.OnOpen += WebSocketPrivate_Opened;

                webSocketPrivate.ConnectAsync();
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
                webSocketPrivate?.SendAsync(authRequest);
                webSocketPrivate?.SendAsync("{\"op\":\"subscribe\",\"args\":[\"order\"]}");
                webSocketPrivate?.SendAsync("{\"op\":\"subscribe\", \"args\":[\"execution\"]}");
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
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
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

        private void WebSocketPublicInverse_OnMessage(object sender, MessageEventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                return;
            }

            if (concurrentQueueMessagePublicWebSocket != null)
            {
                concurrentQueueMessagePublicWebSocket?.Enqueue(e.Data + ".INVERSE");
            }
        }

        private void WebSocketPublicOption_OnMessage(object sender, MessageEventArgs e)
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                return;
            }

            if (concurrentQueueMessagePublicWebSocket != null)
            {
                concurrentQueueMessagePublicWebSocket?.Enqueue(e.Data + ".OPTION");
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
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void WebSocketPublic_Opened(object sender, EventArgs e)
        {
            CheckFullActivation();
        }

        #endregion 7

        #region 8 WebSocket check alive

        private void ThreadCheckAliveWebSocketThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(19000); // https://bybit-exchange.github.io/docs/v5/ws/connect#ip-limits To avoid network or program issues, we recommend that you send the ping heartbeat packet every 20 seconds to maintain the WebSocket connection.

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
                            webSocketPublicSpot?.SendAsync("{\"req_id\": \"OsEngine\", \"op\": \"ping\"}");
                        }
                    }

                    for (int i = 0; i < _webSocketPublicLinear.Count; i++)
                    {
                        WebSocket webSocketPublicLinear = _webSocketPublicLinear[i];

                        if (webSocketPublicLinear != null && webSocketPublicLinear?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublicLinear?.SendAsync("{\"req_id\": \"OsEngine\", \"op\": \"ping\"}");
                        }
                    }

                    for (int i = 0; i < _webSocketPublicInverse.Count; i++)
                    {
                        WebSocket webSocketPublicInverse = _webSocketPublicInverse[i];

                        if (webSocketPublicInverse != null && webSocketPublicInverse?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublicInverse?.SendAsync("{\"req_id\": \"OsEngine\", \"op\": \"ping\"}");
                        }
                    }

                    for (int i = 0; i < _webSocketPublicOption.Count; i++)
                    {
                        WebSocket webSocketPublicOption = _webSocketPublicOption[i];

                        if (webSocketPublicOption != null && webSocketPublicOption?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublicOption?.SendAsync("{\"req_id\": \"OsEngine\", \"op\": \"ping\"}");
                        }
                    }

                    if (webSocketPrivate != null && webSocketPrivate?.ReadyState == WebSocketState.Open)
                    {
                        webSocketPrivate?.SendAsync("{\"req_id\": \"OsEngine\", \"op\": \"ping\"}");
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                }
            }
        }

        #endregion  8

        #region 9 Security subscribe

        private List<string> SubscribeSecuritySpot = new List<string>();

        private List<string> SubscribeSecurityLinear = new List<string>();

        private List<string> SubscribeSecurityInverse = new List<string>();

        private List<string> SubscribedSecurityOption = new List<string>();

        private List<string> _subscribedOptionTradeBaseCoins = new List<string>();

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(50));

        public void Subscribe(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (!security.Name.EndsWith(".P")
                    && !security.Name.EndsWith(".I") && security.SecurityType != SecurityType.Option)
                {
                    if (SubscribeSecuritySpot == null)
                    {
                        return;
                    }

                    for (int i = 0; i < SubscribeSecuritySpot.Count; i++)
                    {
                        if (SubscribeSecuritySpot[i].Equals(security.Name))
                        {
                            return;
                        }
                    }

                    SubscribeSecuritySpot.Add(security.Name);

                    if (_webSocketPublicSpot.Count == 0)
                    {
                        return;
                    }

                    WebSocket webSocketPublicSpot = _webSocketPublicSpot[_webSocketPublicSpot.Count - 1];

                    if (webSocketPublicSpot.ReadyState == WebSocketState.Open
                        && SubscribeSecuritySpot.Count != 0
                        && SubscribeSecuritySpot.Count % 50 == 0)
                    {
                        // creating a new socket
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
                        webSocketPublicSpot?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"publicTrade.{security.Name}\" ] }}");
                        webSocketPublicSpot?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{security.Name}\" ] }}");

                        if (_extendedMarketData)
                        {
                            webSocketPublicSpot?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"tickers.{security.Name}\" ] }}");
                        }
                    }
                }
                else if (security.Name.EndsWith(".P") && security.SecurityType != SecurityType.Option)
                {
                    if (SubscribeSecurityLinear == null)
                    {
                        return;
                    }

                    for (int i = 0; i < SubscribeSecurityLinear.Count; i++)
                    {
                        if (SubscribeSecurityLinear[i].Equals(security.Name))
                        {
                            return;
                        }
                    }

                    SubscribeSecurityLinear.Add(security.Name);

                    if (_webSocketPublicLinear.Count == 0)
                    {
                        return;
                    }

                    WebSocket webSocketPublicLinear = _webSocketPublicLinear[_webSocketPublicLinear.Count - 1];

                    if (webSocketPublicLinear.ReadyState == WebSocketState.Open
                        && SubscribeSecurityLinear.Count != 0
                        && SubscribeSecurityLinear.Count % 50 == 0)
                    {
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
                        webSocketPublicLinear?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"publicTrade.{security.Name.Replace(".P", "")}\" ] }}");
                        webSocketPublicLinear?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{security.Name.Replace(".P", "")}\" ] }}");

                        if (_extendedMarketData)
                        {
                            webSocketPublicLinear?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"tickers.{security.Name.Replace(".P", "")}\" ] }}");
                            GetFundingData(security.Name.Replace(".P", ""));
                        }
                    }

                    SetLeverage(security);
                }
                else if (security.Name.EndsWith(".I") && security.SecurityType != SecurityType.Option)
                {
                    if (SubscribeSecurityInverse == null)
                    {
                        return;
                    }

                    for (int i = 0; i < SubscribeSecurityInverse.Count; i++)
                    {
                        if (SubscribeSecurityInverse[i].Equals(security.Name))
                        {
                            return;
                        }
                    }

                    SubscribeSecurityInverse.Add(security.Name);

                    if (_webSocketPublicInverse.Count == 0)
                    {
                        return;
                    }

                    WebSocket webSocketPublicInverse = _webSocketPublicInverse[_webSocketPublicInverse.Count - 1];

                    if (webSocketPublicInverse.ReadyState == WebSocketState.Open
                        && SubscribeSecurityInverse.Count != 0
                        && SubscribeSecurityInverse.Count % 50 == 0)
                    {
                        WebSocket newSocket = CreateNewInversePublicSocket();

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
                            _webSocketPublicInverse.Add(newSocket);
                            webSocketPublicInverse = newSocket;
                        }
                    }

                    if (webSocketPublicInverse != null
                        && webSocketPublicInverse?.ReadyState == WebSocketState.Open)
                    {
                        webSocketPublicInverse?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"publicTrade.{security.Name.Replace(".I", "")}\" ] }}");
                        webSocketPublicInverse?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{security.Name.Replace(".I", "")}\" ] }}");

                        if (_extendedMarketData)
                        {
                            webSocketPublicInverse?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"tickers.{security.Name.Replace(".I", "")}\" ] }}");
                        }
                    }
                }
                else if (security.SecurityType == SecurityType.Option)
                {
                    if (_useOptions == false)
                    {
                        return;
                    }

                    if (SubscribedSecurityOption == null)
                    {
                        return;
                    }

                    for (int i = 0; i < SubscribedSecurityOption.Count; i++)
                    {
                        if (SubscribedSecurityOption[i].Equals(security.Name))
                        {
                            return;
                        }
                    }

                    SubscribedSecurityOption.Add(security.Name);

                    if (_webSocketPublicInverse.Count == 0)
                    {
                        return;
                    }

                    WebSocket webSocketPublicOption = _webSocketPublicOption[^1];

                    if (webSocketPublicOption.ReadyState == WebSocketState.Open
                        && SubscribedSecurityOption.Count != 0
                        && SubscribedSecurityOption.Count % 50 == 0)
                    {
                        WebSocket newSocket = CreateNewOptionPublicSocket();

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
                            _webSocketPublicOption.Add(newSocket);
                            webSocketPublicOption = newSocket;
                        }
                    }

                    // Note: option uses baseCoin, e.g., publicTrade.BTC https://bybit-exchange.github.io/docs/v5/websocket/public/trade
                    string baseCoin = security.Name.Split('-')[0];

                    if (!_subscribedOptionTradeBaseCoins.Contains(baseCoin))
                    {
                        webSocketPublicOption.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"publicTrade.{baseCoin}\" ] }}");
                        _subscribedOptionTradeBaseCoins.Add(baseCoin);
                    }

                    webSocketPublicOption.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"orderbook.25.{security.NameId}\" ] }}"); // only 25 or 100 for options

                    if (_extendedMarketData)
                    {
                        webSocketPublicOption.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"tickers.{security.NameId}\" ] }}");
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }
        private void GetFundingData(string security)
        {
            try
            {
                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs.Add("symbol", security);
                parametrs.Add("category", Category.linear);

                string message = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/instruments-info");

                var responseSymbols = JsonConvert.DeserializeObject<ResponseRestMessage<ArraySymbols>>(message);

                if (responseSymbols != null
                    && responseSymbols.retCode == "0"
                    && responseSymbols.retMsg == "OK")
                {
                    Symbols item = responseSymbols.result.list[0];

                    string sec = item.symbol + ".P";

                    Funding data = new Funding();

                    data.SecurityNameCode = sec;
                    int.TryParse(item.fundingInterval, out data.FundingIntervalHours);
                    data.MaxFundingRate = item.upperFundingRate.ToDecimal();
                    data.MinFundingRate = item.lowerFundingRate.ToDecimal();
                    data.FundingIntervalHours = data.FundingIntervalHours / 60;

                    FundingUpdateEvent?.Invoke(data);
                }
                else
                {
                    SendLogMessage($"Linear securities error. Code: {responseSymbols.retCode}\n"
                        + $"Message: {responseSymbols.retMsg}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetFundingData error. {ex.ToString()}", LogMessageType.Error);
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
                        webSocketPrivate.SendAsync("{\"req_id\": \"order_1\", \"op\": \"unsubscribe\",\"args\": [\"order\"]}");
                        webSocketPrivate.SendAsync("{\"req_id\": \"ticketInfo_1\", \"op\": \"unsubscribe\", \"args\": [ \"ticketInfo\"]}");
                    }
                    webSocketPrivate.OnMessage -= WebSocketPrivate_MessageReceived;
                    webSocketPrivate.OnClose -= WebSocketPrivate_Closed;
                    webSocketPrivate.OnError -= WebSocketPrivate_Error;
                    webSocketPrivate.OnOpen -= WebSocketPrivate_Opened;

                    webSocketPrivate.CloseAsync();
                    webSocketPrivate.Dispose();
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
                    webSocketPublicSpot.OnClose -= WebSocketPublic_Closed;

                    try
                    {
                        if (webSocketPublicSpot != null && webSocketPublicSpot?.ReadyState == WebSocketState.Open)
                        {
                            for (int i2 = 0; i2 < SubscribeSecuritySpot.Count; i2++)
                            {
                                string s = SubscribeSecuritySpot[i2].Split('.')[0];
                                webSocketPublicSpot?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"publicTrade.{s}\" ] }}");
                                webSocketPublicSpot?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{s}\" ] }}");

                                if (_extendedMarketData)
                                {
                                    webSocketPublicSpot?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"tickers.{s}\" ] }}");
                                }
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
                    webSocketPublicSpot.Dispose();
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
                    webSocketPublicLinear.OnClose -= WebSocketPublic_Closed;

                    try
                    {
                        if (webSocketPublicLinear != null && webSocketPublicLinear?.ReadyState == WebSocketState.Open)
                        {
                            for (int i2 = 0; i2 < SubscribeSecurityLinear.Count; i2++)
                            {
                                string s = SubscribeSecurityLinear[i2].Split('.')[0];
                                webSocketPublicLinear?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"publicTrade.{s}\" ] }}");
                                webSocketPublicLinear?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{s}\" ] }}");

                                if (_extendedMarketData)
                                {
                                    webSocketPublicLinear?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"tickers.{s}\" ] }}");
                                }
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

                    webSocketPublicLinear.Dispose();
                    webSocketPublicLinear = null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            _webSocketPublicLinear.Clear();

            try
            {
                for (int i = 0; i < _webSocketPublicInverse.Count; i++)
                {
                    WebSocket webSocketPublicInverse = _webSocketPublicInverse[i];
                    webSocketPublicInverse.OnOpen -= WebSocketPublic_Opened;
                    webSocketPublicInverse.OnMessage -= WebSocketPublicInverse_OnMessage;
                    webSocketPublicInverse.OnError -= WebSocketPublic_Error;
                    webSocketPublicInverse.OnClose -= WebSocketPublic_Closed;

                    try
                    {
                        if (webSocketPublicInverse != null && webSocketPublicInverse?.ReadyState == WebSocketState.Open)
                        {
                            for (int i2 = 0; i2 < SubscribeSecurityInverse.Count; i2++)
                            {
                                string s = SubscribeSecurityInverse[i2].Split('.')[0];
                                webSocketPublicInverse?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"publicTrade.{s}\" ] }}");
                                webSocketPublicInverse?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{s}\" ] }}");

                                if (_extendedMarketData)
                                {
                                    webSocketPublicInverse?.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"tickers.{s}\" ] }}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                    }

                    if (webSocketPublicInverse.ReadyState == WebSocketState.Open)
                    {
                        webSocketPublicInverse.CloseAsync();
                    }

                    webSocketPublicInverse.Dispose();
                    webSocketPublicInverse = null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            _webSocketPublicInverse.Clear();

            try
            {
                for (int i = 0; i < _webSocketPublicOption.Count; i++)
                {
                    WebSocket webSocketPublicOption = _webSocketPublicOption[i];
                    webSocketPublicOption.OnOpen -= WebSocketPublic_Opened;
                    webSocketPublicOption.OnMessage -= WebSocketPublicOption_OnMessage;
                    webSocketPublicOption.OnError -= WebSocketPublic_Error;
                    webSocketPublicOption.OnClose -= WebSocketPublic_Closed;

                    try
                    {
                        if (webSocketPublicOption != null && webSocketPublicOption?.ReadyState == WebSocketState.Open)
                        {
                            for (int i2 = 0; i2 < SubscribedSecurityOption.Count; i2++)
                            {
                                string s = SubscribedSecurityOption[i2];
                                string baseCoin = s.Split('.')[0];

                                webSocketPublicOption.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"publicTrade.{baseCoin}\" ] }}");
                                webSocketPublicOption.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"orderbook.25.{s}\" ] }}");

                                if (_extendedMarketData)
                                {
                                    webSocketPublicOption.SendAsync($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"tickers.{s}\" ] }}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
                    }

                    if (webSocketPublicOption.ReadyState == WebSocketState.Open)
                    {
                        webSocketPublicOption.CloseAsync();
                    }

                    webSocketPublicOption.Dispose();
                    webSocketPublicOption = null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            _webSocketPublicOption.Clear();

            _listMarketDepthSpot?.Clear();
            concurrentQueueMessagePublicWebSocket = null;
            _concurrentQueueMessageOrderBookSpot = null;
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion 9

        #region 10 WebSocket parsing the messages

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

                    if (_message.EndsWith(".INVERSE"))
                    {
                        category = Category.inverse;
                        message = _message.Replace("}.INVERSE", "}");
                    }

                    if (_message.EndsWith(".OPTION"))
                    {
                        category = Category.option;
                        message = _message.Replace("}.OPTION", "}");
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
                            else if (category == Category.linear)
                            {
                                _concurrentQueueTradesLinear.Enqueue(message);
                            }
                            else if (category == Category.inverse)
                            {
                                _concurrentQueueTradesInverse.Enqueue(message);
                            }
                            else if (category == Category.option)
                            {
                                _concurrentQueueTradesOption.Enqueue(message);
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
                            else if (category == Category.inverse)
                            {
                                _concurrentQueueMessageOrderBookInverse.Enqueue(message);
                            }
                            else if (category == Category.option)
                            {
                                _concurrentQueueMessageOrderBookOption.Enqueue(message);
                            }

                            continue;
                        }
                        else if (response.topic.Contains("tickers"))
                        {
                            if (category == Category.linear)
                            {
                                _concurrentQueueTickersLinear.Enqueue(_message);
                            }
                            else if (category == Category.inverse)
                            {
                                _concurrentQueueTickersInverse.Enqueue(message);
                            }
                            else if (category == Category.spot)
                            {
                                _concurrentQueueTickersSpot.Enqueue(message);
                            }
                            else if (category == Category.option)
                            {
                                _concurrentQueueTickersOption.Enqueue(message);
                            }

                            continue;
                        }

                        continue;
                    }

                    SubscribeMessage subscribeMessage =
                       JsonConvert.DeserializeAnonymousType(message, new SubscribeMessage());

                    if (subscribeMessage.op == "pong")
                    {
                        continue;
                    }

                    if (subscribeMessage.op != null)
                    {
                        if (subscribeMessage.success == "false")
                        {
                            if (subscribeMessage.ret_msg.Contains("already"))
                            {
                                continue;
                            }
                            SendLogMessage("WebSocket Error: " + subscribeMessage.ret_msg, LogMessageType.Error);
                        }

                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(3000);
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }
        }

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

                    SubscribeMessage subscribeMessage =
                      JsonConvert.DeserializeAnonymousType(message, new SubscribeMessage());

                    if (subscribeMessage.op == "pong")
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
                    else if (responseMyTrades.data[i].category == Category.linear.ToString())
                    {
                        myTrade.SecurityNameCode = responseMyTrades.data[i].symbol + ".P";
                    }
                    else if (responseMyTrades.data[i].category == Category.inverse.ToString())
                    {
                        myTrade.SecurityNameCode = responseMyTrades.data[i].symbol + ".I";
                    }
                    else
                    {
                        myTrade.SecurityNameCode = responseMyTrades.data[i].symbol;
                    }

                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseMyTrades.data[i].execTime));
                    myTrade.NumberOrderParent = responseMyTrades.data[i].orderId;
                    myTrade.NumberTrade = responseMyTrades.data[i].execId;
                    myTrade.Price = responseMyTrades.data[i].execPrice.ToDecimal();
                    myTrade.Side = responseMyTrades.data[i].side.ToUpper().Equals("BUY") ? Side.Buy : Side.Sell;

                    if (responseMyTrades.data[i].category == Category.spot.ToString() && myTrade.Side == Side.Buy && !string.IsNullOrWhiteSpace(responseMyTrades.data[i].execFee))   // The spot commission for purchases is taken from the purchased coin.
                    {
                        myTrade.Volume = responseMyTrades.data[i].execQty.ToDecimal() - responseMyTrades.data[i].execFee.ToDecimal();
                        int decimalVolum = GetVolumeDecimals(myTrade.SecurityNameCode);
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
                        "UNTRIGGERED" => OrderStateType.Active,
                        "PARTIALLYFILLED" => OrderStateType.Partial,
                        "FILLED" => OrderStateType.Done,
                        "ORDER_FILLED" => OrderStateType.Done,
                        "CANCELLED" => OrderStateType.Cancel,
                        "ORDER_CANCELLED" => OrderStateType.Cancel,
                        "PARTIALLYFILLEDCANCELED" => OrderStateType.Cancel,
                        "DEACTIVATED" => OrderStateType.Cancel,
                        "REJECTED" => OrderStateType.Fail,
                        "ORDER_REJECTED" => OrderStateType.Fail,
                        "ORDER_FAILED" => OrderStateType.Fail,
                        _ => OrderStateType.Cancel,
                    };

                    Order newOrder = new Order();

                    if (responseMyTrades.data[i].category.ToLower() == Category.spot.ToString())
                    {
                        newOrder.SecurityNameCode = responseMyTrades.data[i].symbol;
                    }
                    else if (responseMyTrades.data[i].category.ToLower() == Category.inverse.ToString())
                    {
                        newOrder.SecurityNameCode = responseMyTrades.data[i].symbol + ".I";
                    }
                    else if (responseMyTrades.data[i].category.ToLower() == Category.linear.ToString())
                    {
                        newOrder.SecurityNameCode = responseMyTrades.data[i].symbol + ".P";
                    }
                    else
                    {
                        newOrder.SecurityNameCode = responseMyTrades.data[i].symbol;
                    }

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
                        if (string.IsNullOrEmpty(responseMyTrades.data[i].orderLinkId) == false)
                        {
                            newOrder.NumberUser = Convert.ToInt32(responseMyTrades.data[i].orderLinkId);
                        }
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

        private void ThreadMessageReaderOrderBookSpot()
        {
            Category category = Category.spot;

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
                        Thread.Sleep(1);
                        continue;
                    }

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

        private void ThreadMessageReaderOrderBookInverse()
        {
            Category category = Category.inverse;

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                }

                try
                {
                    if (_concurrentQueueMessageOrderBookInverse == null
                        || _concurrentQueueMessageOrderBookInverse.IsEmpty
                        || _concurrentQueueMessageOrderBookInverse.Count == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string _message;

                    if (!_concurrentQueueMessageOrderBookInverse.TryDequeue(out _message))
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message = _message.Replace("}.INVERSE", "}");

                    ResponseWebSocketMessage<object> response =
                        JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    UpdateOrderBook(message, response, category);

                    while (_concurrentQueueMessageOrderBookInverse?.Count > 10000)
                    {
                        _concurrentQueueMessageOrderBookInverse.TryDequeue(out _message);
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }
        }

        private void ThreadMessageReaderOrderBookLinear()
        {
            Category category = Category.linear;

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
                        Thread.Sleep(1);
                        continue;
                    }

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

        private void ThreadMessageReaderOrderBookOption()
        {
            Category category = Category.option;

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                }

                try
                {
                    if (_concurrentQueueMessageOrderBookOption == null
                        || _concurrentQueueMessageOrderBookOption.IsEmpty
                        || _concurrentQueueMessageOrderBookOption.Count == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    if (!_concurrentQueueMessageOrderBookOption.TryDequeue(out message))
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    ResponseWebSocketMessage<object> response =
                        JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<object>());

                    UpdateOrderBook(message, response, category);

                    while (_concurrentQueueMessageOrderBookOption.Count > 10000)
                    {
                        _concurrentQueueMessageOrderBookOption.TryDequeue(out message);
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

        private ConcurrentQueue<string> _concurrentQueueMessageOrderBookInverse;

        private ConcurrentQueue<string> _concurrentQueueMessageOrderBookOption;

        private Dictionary<string, MarketDepth> _listMarketDepthSpot = new Dictionary<string, MarketDepth>();

        private Dictionary<string, MarketDepth> _listMarketDepthLinear = new Dictionary<string, MarketDepth>();

        private Dictionary<string, MarketDepth> _listMarketDepthInverse = new Dictionary<string, MarketDepth>();

        private Dictionary<string, MarketDepth> _listMarketDepthOption = new Dictionary<string, MarketDepth>();

        private void UpdateOrderBook(string message, ResponseWebSocketMessage<object> response, Category category)
        {
            try
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
                else if (category == Category.inverse)
                {
                    sec = sec + ".I";
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
                else if (category == Category.inverse)
                {
                    if (!_listMarketDepthInverse.TryGetValue(sec, out marketDepth))
                    {
                        marketDepth = new MarketDepth();
                        marketDepth.SecurityNameCode = sec;
                        _listMarketDepthInverse.Add(sec, marketDepth);
                    }
                }
                else if (category == Category.option)
                {
                    if (!_listMarketDepthOption.TryGetValue(sec, out marketDepth))
                    {
                        marketDepth = new MarketDepth();
                        marketDepth.SecurityNameCode = sec;
                        _listMarketDepthOption.Add(sec, marketDepth);
                    }
                }

                if (response.type == "snapshot")
                {
                    marketDepth.Asks.Clear();
                    marketDepth.Bids.Clear();
                }

                if (responseDepth.data.a.Length > 1)
                {
                    for (int i = 0; i < (responseDepth.data.a.Length / 2); i++)
                    {
                        double.TryParse(responseDepth.data.a[i, 0], System.Globalization.NumberStyles.Number, cultureInfo, out double aPrice);
                        double.TryParse(responseDepth.data.a[i, 1], System.Globalization.NumberStyles.Number, cultureInfo, out double aAsk);

                        if (marketDepth.Asks.Exists(a => a.Price == aPrice))
                        {
                            if (aAsk == 0)
                            {
                                marketDepth.Asks.RemoveAll(a => a.Price == aPrice);
                            }
                            else
                            {
                                for (int j = 0; j < marketDepth.Asks.Count; j++)
                                {
                                    if (marketDepth.Asks[j].Price == aPrice)
                                    {
                                        marketDepth.Asks[j].Ask = aAsk;
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            MarketDepthLevel marketDepthLevel = new MarketDepthLevel();
                            marketDepthLevel.Ask = aAsk;
                            marketDepthLevel.Price = aPrice;
                            marketDepth.Asks.Add(marketDepthLevel);
                            marketDepth.Asks.RemoveAll(a => a.Ask == 0);
                            marketDepth.Bids.RemoveAll(a => a.Price == aPrice && aPrice != 0);
                            SortAsks(marketDepth.Asks);
                        }
                    }
                }
                if (responseDepth.data.b.Length > 1)
                {
                    for (int i = 0; i < (responseDepth.data.b.Length / 2); i++)
                    {
                        double.TryParse(responseDepth.data.b[i, 0], System.Globalization.NumberStyles.Number, cultureInfo, out double bPrice);
                        double.TryParse(responseDepth.data.b[i, 1], System.Globalization.NumberStyles.Number, cultureInfo, out double bBid);

                        if (marketDepth.Bids.Exists(b => b.Price == bPrice))
                        {
                            if (bBid == 0)
                            {
                                marketDepth.Bids.RemoveAll(b => b.Price == bPrice);
                            }
                            else
                            {
                                for (int j = 0; j < marketDepth.Bids.Count; j++)
                                {
                                    if (marketDepth.Bids[j].Price == bPrice)
                                    {
                                        marketDepth.Bids[j].Bid = bBid;
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            MarketDepthLevel marketDepthLevel = new MarketDepthLevel();
                            marketDepthLevel.Bid = bBid;
                            marketDepthLevel.Price = bPrice;
                            marketDepth.Bids.Add(marketDepthLevel);
                            marketDepth.Bids.RemoveAll(a => a.Bid == 0);
                            marketDepth.Asks.RemoveAll(a => a.Price == bPrice && bPrice != 0);
                            SortBids(marketDepth.Bids);
                        }
                    }
                }

                marketDepth.Time = TimeManager.GetDateTimeFromTimeStamp((long)responseDepth.ts.ToDecimal());

                int _depthDeep = marketDepthDeep;

                if (marketDepthDeep > 20)
                {
                    _depthDeep = 20;
                }

                while (marketDepth.Asks.Count > _depthDeep)
                {
                    marketDepth.Asks.RemoveAt(_depthDeep);
                }

                while (marketDepth.Bids.Count > _depthDeep)
                {
                    marketDepth.Bids.RemoveAt(_depthDeep);
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

                if (_concurrentQueueMessageOrderBookLinear?.Count < 500
                    && _concurrentQueueMessageOrderBookSpot?.Count < 500
                    && _concurrentQueueMessageOrderBookInverse?.Count < 500
                    && _concurrentQueueMessageOrderBookOption?.Count < 500)
                {
                    MarketDepthEvent?.Invoke(marketDepth.GetCopy());
                }
                else
                {
                    MarketDepthEvent?.Invoke(marketDepth);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        protected void SortBids(List<MarketDepthLevel> levels)
        {
            levels.Sort((a, b) =>
            {
                if (a.Price > b.Price)
                {
                    return -1;
                }
                else if (a.Price < b.Price)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            });
        }

        protected void SortAsks(List<MarketDepthLevel> levels)
        {
            levels.Sort((a, b) =>
            {
                if (a.Price > b.Price)
                {
                    return 1;
                }
                else if (a.Price < b.Price)
                {
                    return -1;
                }
                else
                {
                    return 0;
                }
            });
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        public event Action<MarketDepth> MarketDepthEvent;

        private ConcurrentQueue<string> _concurrentQueueTradesSpot = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _concurrentQueueTradesLinear = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _concurrentQueueTradesInverse = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _concurrentQueueTradesOption = new ConcurrentQueue<string>();

        private void ThreadMessageReaderTradesSpot()
        {
            Category category = Category.spot;

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                }

                try
                {
                    if (_concurrentQueueTradesSpot != null
                        && _concurrentQueueTradesSpot.IsEmpty == false)
                    {
                        if (_concurrentQueueTradesSpot.TryDequeue(out string message))
                        {
                            UpdateTrade(message, category);
                        }
                    }
                    else if (_concurrentQueueTickersSpot != null
                    && _concurrentQueueTickersSpot.IsEmpty == false)
                    {
                        if (_concurrentQueueTickersSpot.TryDequeue(out string message2))
                        {
                            UpdateTicker(message2, category);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
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
            Category category = Category.linear;

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                }

                try
                {
                    if (_concurrentQueueTradesLinear != null
                        && _concurrentQueueTradesLinear.IsEmpty == false)
                    {
                        if (_concurrentQueueTradesLinear.TryDequeue(out string message))
                        {
                            UpdateTrade(message, category);
                        }
                    }
                    else if (_concurrentQueueTickersLinear != null
                    && _concurrentQueueTickersLinear.IsEmpty == false)
                    {
                        if (_concurrentQueueTickersLinear.TryDequeue(out string message2))
                        {
                            UpdateTicker(message2, category);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }
        }

        private void ThreadMessageReaderTradesInverse()
        {
            Category category = Category.inverse;

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                }

                try
                {
                    if (_concurrentQueueTradesInverse != null
                       && _concurrentQueueTradesInverse.IsEmpty == false)
                    {
                        if (_concurrentQueueTradesInverse.TryDequeue(out string message))
                        {
                            UpdateTrade(message, category);
                        }
                    }
                    else if (_concurrentQueueTickersInverse != null
                    && _concurrentQueueTickersInverse.IsEmpty == false)
                    {
                        if (_concurrentQueueTickersInverse.TryDequeue(out string message2))
                        {
                            UpdateTicker(message2, category);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(5000);
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }
            }
        }

        private void ThreadMessageReaderTradesOption()
        {
            Category category = Category.option;

            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                }

                try
                {
                    if (_concurrentQueueTradesOption != null
                       && _concurrentQueueTradesOption.IsEmpty == false)
                    {
                        if (_concurrentQueueTradesOption.TryDequeue(out string message))
                        {
                            UpdateTrade(message, category);
                        }
                    }
                    else if (_concurrentQueueTickersOption != null
                    && _concurrentQueueTickersOption.IsEmpty == false)
                    {
                        if (_concurrentQueueTickersOption.TryDequeue(out string message2))
                        {
                            UpdateTicker(message2, category);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
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
            try
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
                            if (category == Category.linear)
                            {
                                trade.SecurityNameCode = item.s + ".P";
                            }
                            else if (category == Category.inverse)
                            {
                                trade.SecurityNameCode = item.s + ".I";
                            }
                        }
                        else
                        {
                            trade.SecurityNameCode = item.s;
                        }

                        if (_extendedMarketData && category != Category.spot)
                        {
                            trade.OpenInterest = GetOpenInterest(trade.SecurityNameCode);
                        }

                        NewTradesEvent?.Invoke(trade);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        private decimal GetOpenInterest(string securityNameCode)
        {
            if (_allTickers.Count == 0
                || _allTickers == null)
            {
                return 0;
            }

            for (int i = 0; i < _allTickers.Count; i++)
            {
                if (_allTickers[i].SecurityName == securityNameCode)
                {
                    return _allTickers[i].OpenInterest.ToDecimal();
                }
            }

            return 0;
        }

        private ConcurrentQueue<string> _concurrentQueueTickersLinear = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _concurrentQueueTickersInverse = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _concurrentQueueTickersSpot = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> _concurrentQueueTickersOption = new ConcurrentQueue<string>();

        private List<Tickers> _allTickers = new List<Tickers>();

        private void UpdateTicker(string message, Category category)
        {
            try
            {
                ResponseWebSocketMessage<ResponseTicker> responseTicker =
                                 JsonConvert.DeserializeAnonymousType(message, new ResponseWebSocketMessage<ResponseTicker>());

                if (responseTicker == null
                    || responseTicker.data == null)
                {
                    return;
                }

                Tickers tickers = new Tickers();

                if (category == Category.linear)
                {
                    tickers.SecurityName = responseTicker.data.symbol + ".P";
                }
                else if (category == Category.inverse)
                {
                    tickers.SecurityName = responseTicker.data.symbol + ".I";
                }
                else if (category == Category.spot)
                {
                    tickers.SecurityName = responseTicker.data.symbol;
                }
                else if (category == Category.option)
                {
                    tickers.SecurityName = responseTicker.data.symbol;

                    Security sec = _securities.Find(sec => sec.Name == responseTicker.data.symbol);

                    OptionMarketDataForConnector data = new OptionMarketDataForConnector();

                    data.SecurityName = responseTicker.data.symbol;
                    data.UnderlyingAsset = sec.UnderlyingAsset;

                    data.Delta = responseTicker.data.delta;
                    data.Gamma = responseTicker.data.gamma;
                    data.Vega = responseTicker.data.vega;
                    data.Theta = responseTicker.data.theta;
                    data.TimeCreate = responseTicker.ts;
                    data.BidIV = responseTicker.data.bidIv;
                    data.AskIV = responseTicker.data.askIv;
                    data.MarkIV = responseTicker.data.markPriceIv;
                    data.OpenInterest = responseTicker.data.openInterest;
                    data.MarkPrice = responseTicker.data.markPrice;
                    data.UnderlyingPrice = responseTicker.data.underlyingPrice;

                    AdditionalMarketDataEvent!(data);
                }

                if (responseTicker.data.openInterestValue != null)
                {
                    tickers.OpenInterest = responseTicker.data.openInterestValue;

                    bool isInArray = false;

                    for (int i = 0; i < _allTickers.Count; i++)
                    {
                        if (_allTickers[i].SecurityName == tickers.SecurityName)
                        {
                            _allTickers[i].OpenInterest = tickers.OpenInterest;
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        _allTickers.Add(tickers);
                    }
                }

                Funding funding = new Funding();

                ResponseTicker item = responseTicker.data;

                funding.SecurityNameCode = tickers.SecurityName;
                funding.CurrentValue = item.fundingRate.ToDecimal() * 100;
                funding.NextFundingTime = TimeManager.GetDateTimeFromTimeStamp((long)item.nextFundingTime.ToDecimal());
                funding.TimeUpdate = TimeManager.GetDateTimeFromTimeStamp((long)responseTicker.ts.ToDecimal());

                FundingUpdateEvent?.Invoke(funding);

                SecurityVolumes volume = new SecurityVolumes();

                volume.SecurityNameCode = tickers.SecurityName;
                volume.Volume24h = item.volume24h.ToDecimal();
                volume.Volume24hUSDT = item.turnover24h.ToDecimal();

                Volume24hUpdateEvent?.Invoke(volume);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<Funding> FundingUpdateEvent;

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        #endregion 10

        #region 11 Trade

        public void SendOrder(Order order)
        {
            try
            {
                if (order.TypeOrder == OrderPriceType.Iceberg)
                {
                    SendLogMessage("Bybit doesn't support iceberg orders", LogMessageType.Error);
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
                Security sec = _securities.Find(sec => sec.Name == order.SecurityNameCode);

                if ((order.SecurityClassCode != null
                    && order.SecurityClassCode.ToLower().Contains(Category.linear.ToString()))
                    || order.SecurityNameCode.EndsWith(".P"))
                {
                    parameters["category"] = Category.linear.ToString();
                }
                else if ((order.SecurityClassCode != null
                    && order.SecurityClassCode.ToLower().Contains(Category.inverse.ToString()))
                    || order.SecurityNameCode.EndsWith(".I"))
                {
                    parameters["category"] = Category.inverse.ToString();
                }
                else if (sec.SecurityType == SecurityType.Option)
                {
                    parameters["category"] = Category.option.ToString();
                }
                else
                {
                    parameters["category"] = Category.spot.ToString();
                }

                parameters["symbol"] = order.SecurityNameCode.Split('.')[0];
                parameters["positionIdx"] = 0; // hedge_mode;

                bool reduceOnly = false;

                if (HedgeMode
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

                if (HedgeMode)
                {
                    parameters["reduceOnly"] = reduceOnly;
                }

                string jsonPayload = parameters.Count > 0 ? GenerateQueryString(parameters) : "";

                DateTime startTime = DateTime.Now;
                string place_order_response = CreatePrivateQuery(parameters, HttpMethod.Post, "/v5/order/create");

                string isSuccessful = "ByBit error. The order was not accepted.";

                if (place_order_response != null)
                {
                    ResponseRestMessage<SendOrderResponse> responseOrder = JsonConvert.DeserializeObject<ResponseRestMessage<SendOrderResponse>>(place_order_response);
                    isSuccessful = responseOrder.retMsg;

                    if (responseOrder != null
                        && responseOrder.retCode == "0"
                        && isSuccessful == "OK")
                    {
                        if (responseOrder.result.orderId != string.Empty)
                        {
                            DateTime placedTime = DateTime.Now;
                            order.State = OrderStateType.Active;
                            order.NumberMarket = responseOrder.result.orderId;
                            order.TimeCreate = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(responseOrder.time)).UtcDateTime;
                            order.TimeCallBack = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(responseOrder.time)).UtcDateTime.Add(placedTime.Subtract(startTime));
                            MyOrderEvent?.Invoke(order);
                        }

                        return;
                    }
                    else
                    {
                        SendLogMessage($"SendOrder>. Order error. {jsonPayload}.\n" +
                            $" Code:{responseOrder.retCode}. Message: {responseOrder.retMsg}", LogMessageType.Error);
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
                Security sec = _securities.Find(sec => sec.Name == order.SecurityNameCode);

                if ((order.SecurityClassCode != null
                   && order.SecurityClassCode.ToLower().Contains(Category.linear.ToString()))
                   || order.SecurityNameCode.EndsWith(".P"))
                {
                    parameters["category"] = Category.linear.ToString();
                }
                else if ((order.SecurityClassCode != null
                    && order.SecurityClassCode.ToLower().Contains(Category.inverse.ToString()))
                    || order.SecurityNameCode.EndsWith(".I"))
                {
                    parameters["category"] = Category.inverse.ToString();
                }
                else if (sec.SecurityType == SecurityType.Option)
                {
                    parameters["category"] = Category.option.ToString();
                }
                else
                {
                    parameters["category"] = Category.spot.ToString();
                }

                parameters["symbol"] = order.SecurityNameCode.Split('.')[0];
                parameters["orderLinkId"] = order.NumberUser.ToString();
                parameters["price"] = newPrice.ToString().Replace(",", ".");

                string place_order_response = CreatePrivateQuery(parameters, HttpMethod.Post, "/v5/order/amend");

                if (place_order_response != null)
                {
                    ResponseRestMessageList<string> responseOrder = JsonConvert.DeserializeObject<ResponseRestMessageList<string>>(place_order_response);

                    if (responseOrder != null
                        && responseOrder.retCode == "0"
                        && responseOrder.retMsg == "OK")
                    {
                        order.Price = newPrice;
                        MyOrderEvent?.Invoke(order);
                    }
                    else
                    {
                        SendLogMessage($"ChangeOrderPrice Fail. Code: {responseOrder.retCode}\n"
                                 + $"Message: {responseOrder.retMsg}", LogMessageType.Error);
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

        public bool CancelOrder(Order order)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            Security sec = _securities.Find(sec => sec.Name == order.SecurityNameCode);

            if ((order.SecurityClassCode != null
                  && order.SecurityClassCode.ToLower().Contains(Category.linear.ToString()))
                  || order.SecurityNameCode.EndsWith(".P"))
            {
                parameters["category"] = Category.linear.ToString();
            }
            else if ((order.SecurityClassCode != null
                && order.SecurityClassCode.ToLower().Contains(Category.inverse.ToString()))
                || order.SecurityNameCode.EndsWith(".I"))
            {
                parameters["category"] = Category.inverse.ToString();
            }
            else if (sec.SecurityType == SecurityType.Option)
            {
                parameters["category"] = Category.option.ToString();
            }
            else
            {
                parameters["category"] = Category.spot.ToString();
            }

            parameters["symbol"] = order.SecurityNameCode.Split('.')[0];

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
                string place_order_response = CreatePrivateQuery(parameters, HttpMethod.Post, "/v5/order/cancel");

                if (place_order_response != null)
                {
                    ResponseRestMessageList<string> responseOrder = JsonConvert.DeserializeObject<ResponseRestMessageList<string>>(place_order_response);

                    if (responseOrder != null
                        && responseOrder.retCode == "0"
                        && responseOrder.retMsg == "OK")
                    {
                        order.TimeCancel = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(responseOrder.time)).UtcDateTime;
                        order.State = OrderStateType.Cancel;
                        MyOrderEvent?.Invoke(order);
                        return true;
                    }
                    else
                    {
                        OrderStateType state = GetOrderStatus(order);

                        if (state == OrderStateType.None)
                        {
                            SendLogMessage($"Cancel Order Error. {place_order_response}.", LogMessageType.Error);
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
                        SendLogMessage($"Cancel Order Error. {place_order_response}.", LogMessageType.Error);
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
                SendLogMessage($" Cancel Order Error. Order num {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return false;
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                Dictionary<string, object> parametrs = new Dictionary<string, object>();

                if (security.NameClass.ToLower().Contains(Category.linear.ToString()))
                {
                    parametrs["category"] = Category.linear.ToString();
                }
                else if (security.NameClass.ToLower().Contains(Category.inverse.ToString()))
                {
                    parametrs["category"] = Category.inverse.ToString();
                }
                else if (security.SecurityType == SecurityType.Option)
                {
                    parametrs["category"] = Category.option.ToString();
                }
                else
                {
                    parametrs["category"] = Category.spot.ToString();
                }

                parametrs.Add("symbol", security.Name.Split('.')[0]);
                CreatePrivateQuery(parametrs, HttpMethod.Post, "/v5/order/cancel-all");
            }
            catch (Exception ex)
            {
                SendLogMessage($"CancelAllOrdersToSecurity>. Order error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public void CancelAllOrders()
        {
            try
            {
                List<Order> ordersOpenAll = GetAllOrdersArray(100, true);

                for (int i = 0; i < ordersOpenAll.Count; i++)
                {
                    CancelOrder(ordersOpenAll[i]);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"CancelAllOrders>. Order error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {
            try
            {
                List<Order> ordersOpenAll = GetAllOrdersArray(100, true);

                for (int i = 0; i < ordersOpenAll.Count; i++)
                {
                    MyOrderEvent?.Invoke(ordersOpenAll[i]);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetAllActivOrders>. Order error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private List<Order> GetAllOrdersArray(int maxCountByCategory, bool onlyActive)
        {
            List<Order> ordersOpenAll = new List<Order>();

            List<Order> spotOrders = new List<Order>();
            GetOrders(Category.spot, null, spotOrders, null, maxCountByCategory, onlyActive);

            if (spotOrders != null
                && spotOrders.Count > 0)
            {
                ordersOpenAll.AddRange(spotOrders);
            }

            List<Order> inverseOrders = new List<Order>();
            GetOrders(Category.inverse, null, inverseOrders, null, maxCountByCategory, onlyActive);

            if (inverseOrders != null
                && inverseOrders.Count > 0)
            {
                ordersOpenAll.AddRange(inverseOrders);
            }

            List<Order> linearOrders = new List<Order>();

            for (int i = 0; i < _listLinearCurrency.Count; i++)
            {
                GetOrders(Category.linear, _listLinearCurrency[i], linearOrders, null, maxCountByCategory, onlyActive);

                if (linearOrders != null
                && linearOrders.Count > 0)
                {
                    ordersOpenAll.AddRange(linearOrders);
                }
            }

            return ordersOpenAll;
        }

        private void GetOrders(Category category, string settleCoin, List<Order> array, string cursor, int maxCount, bool onlyActive)
        {
            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();

                parameters["category"] = category;
                parameters["limit"] = "50";

                if (onlyActive)
                {
                    parameters["openOnly"] = "0";
                }
                else
                {
                    parameters["openOnly"] = "1";
                }

                if (cursor != null)
                {
                    parameters["cursor"] = cursor;
                }

                if (category == Category.linear)
                {
                    parameters["settleCoin"] = settleCoin;
                }

                string orders_response = CreatePrivateQuery(parameters, HttpMethod.Get, "/v5/order/realtime");

                if (orders_response == null)
                {
                    return;
                }

                //RetResalt result = JsonConvert.DeserializeObject <nRetResalt> (orders_response);

                ResponseRestMessageList<ResponseMessageOrders> responseOrder = JsonConvert.DeserializeObject<ResponseRestMessageList<ResponseMessageOrders>>(orders_response);

                if (responseOrder != null
                            && responseOrder.retCode == "0"
                            && responseOrder.retMsg == "OK")
                {
                    List<ResponseMessageOrders> ordChild = responseOrder.result.list;

                    List<Order> activeOrders = new List<Order>();

                    for (int i = 0; i < ordChild.Count; i++)
                    {
                        ResponseMessageOrders order = ordChild[i];

                        Order newOrder = new Order();
                        newOrder.ServerType = this.ServerType;

                        if (order.orderStatus == "Cancelled"
                            || order.orderStatus == "Rejected"
                            || order.orderStatus == "PartiallyFilledCanceled"
                            || order.orderStatus == "Deactivated")
                        {
                            newOrder.State = OrderStateType.Cancel;
                            newOrder.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.updatedTime));
                        }
                        else if (order.orderStatus == "Filled")
                        {
                            newOrder.State = OrderStateType.Done;
                            newOrder.TimeDone = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.updatedTime));
                        }
                        else if (order.orderStatus == "New"
                            || order.orderStatus == "Untriggered")
                        {
                            newOrder.State = OrderStateType.Active;
                        }
                        else if(order.orderStatus == "PartiallyFilled")
                        {
                            newOrder.State = OrderStateType.Partial;
                        }

                        if (order.cumExecQty != null)
                        {
                            newOrder.VolumeExecute = order.cumExecQty.ToDecimal();
                        }

                        newOrder.TypeOrder = OrderPriceType.Limit;
                        newOrder.PortfolioNumber = "BybitUNIFIED";

                        newOrder.NumberMarket = order.orderId;
                        newOrder.SecurityNameCode = order.symbol;

                        if (category == Category.linear
                            && newOrder.SecurityNameCode.EndsWith(".P") == false)
                        {
                            newOrder.SecurityNameCode = newOrder.SecurityNameCode + ".P";
                        }

                        if (category == Category.inverse
                            && newOrder.SecurityNameCode.EndsWith(".I") == false)
                        {
                            newOrder.SecurityNameCode = newOrder.SecurityNameCode + ".I";
                        }

                        newOrder.Price = order.price.ToDecimal();
                        newOrder.Volume = order.qty.ToDecimal();

                        newOrder.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(order.updatedTime));
                        newOrder.TimeCreate = newOrder.TimeCallBack;

                        string numUser = order.orderLinkId;

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

                        string side = order.side;

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

                    if (activeOrders.Count > 0)
                    {
                        array.AddRange(activeOrders);

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

                    if (ordChild.Count > 1)
                    {
                        cursor = responseOrder.result.nextPageCursor;

                        if (cursor != null)
                        {
                            GetOrders(category, settleCoin, array, cursor, maxCount, onlyActive);
                        }
                    }

                    return;
                }
                else
                {
                    SendLogMessage($"GetOpenOrders>. Order error. Code: {responseOrder.retCode}\n"
                            + $"Message: {responseOrder.retMsg}", LogMessageType.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetOpenOrders>. Order error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return;
            }
        }

        private List<MyTrade> GetMyTradesHistory(Order orderBase, Category category)
        {
            try
            {
                if (string.IsNullOrEmpty(orderBase.NumberMarket))
                {
                    return null;
                }

                Dictionary<string, object> parameters = new Dictionary<string, object>();

                parameters["category"] = category;
                parameters["symbol"] = orderBase.SecurityNameCode.Split('.')[0].ToUpper();
                parameters["orderId"] = orderBase.NumberMarket;

                string trades_response = CreatePrivateQuery(parameters, HttpMethod.Get, "/v5/execution/list");

                if (trades_response == null)
                {
                    return null;
                }

                ResponseRestMessageList<ResponseMessageMyTrade> responseMyTrade = JsonConvert.DeserializeObject<ResponseRestMessageList<ResponseMessageMyTrade>>(trades_response);

                if (responseMyTrade != null
                    && responseMyTrade.retCode == "0"
                    && responseMyTrade.retMsg == "OK")
                {
                    List<ResponseMessageMyTrade> trChild = responseMyTrade.result.list;

                    List<MyTrade> myTrades = new List<MyTrade>();

                    for (int i = 0; i < trChild.Count; i++)
                    {
                        ResponseMessageMyTrade trade = trChild[i];

                        MyTrade newTrade = new MyTrade();
                        newTrade.SecurityNameCode = trade.symbol;

                        if (category == Category.linear)
                        {
                            newTrade.SecurityNameCode = newTrade.SecurityNameCode + ".P";
                        }
                        else if (category == Category.inverse)
                        {
                            newTrade.SecurityNameCode = newTrade.SecurityNameCode + ".I";
                        }

                        newTrade.NumberTrade = trade.execId;
                        newTrade.NumberOrderParent = orderBase.NumberMarket;
                        newTrade.Price = trade.execPrice.ToDecimal();
                        newTrade.Volume = trade.execQty.ToDecimal();
                        newTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(trade.execTime));

                        string side = trade.side;

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
                else
                {
                    SendLogMessage($"GetMyTradesHistory>. Order error. Code: {responseMyTrade.retCode}\n"
                            + $"Message: {responseMyTrade.retMsg}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetMyTradesHistory>. Order error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            int countToMethod = startIndex + count;

            List<Order> result = GetAllOrdersArray(countToMethod, true);

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

            List<Order> result = GetAllOrdersArray(countToMethod, false);

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

        private List<Order> _activeOrdersCash = new List<Order>();
        private List<Order> _historicalOrdersCash = new List<Order>();
        private DateTime _timeOrdersCashCreate;

        public OrderStateType GetOrderStatus(Order order)
        {
            try
            {
                Category category = Category.spot;

                if (order.SecurityNameCode.EndsWith(".P"))
                {
                    category = Category.linear;
                }
                else if (order.SecurityNameCode.EndsWith(".I"))
                {
                    category = Category.inverse;
                }

                if (_securities != null)
                {
                    Security sec = _securities.Find(sec => sec.Name == order.SecurityNameCode);

                    if (sec != null
                        && sec.SecurityType == SecurityType.Option)
                    {
                        category = Category.option;
                    }
                }

                if (_timeOrdersCashCreate.AddSeconds(2) < DateTime.Now)
                {
                    // обновляем массивы ордеров один раз в две секунды.
                    // Формируем КЭШ для массового запроса статусов на реконнекте
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

                // check trades

                if (myOrder.State == OrderStateType.Partial
                    || myOrder.State == OrderStateType.Done
                    || myOrder.VolumeExecute != 0)
                {
                    List<MyTrade> myTrades = GetMyTradesHistory(myOrder, category);

                    for (int i = 0; myTrades != null && i < myTrades.Count; i++)
                    {
                        MyTradeEvent?.Invoke(myTrades[i]);
                    }
                }

                return myOrder.State;
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetOrderStatus>. Order error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            return OrderStateType.None;
        }

        #endregion 11

        #region 12 Query

        private const string RecvWindow = "50000";

        private RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(15));

        private HttpClientHandler httpClientHandler;

        private HttpClient httpClient;

        private string _httpClientLocker = "httpClientLocker";

        private HttpClient GetHttpClient()
        {
            try
            {
                if (httpClientHandler == null)
                {
                    if (_myProxy == null)
                    {
                        httpClientHandler = new HttpClientHandler();
                    }
                    else if (_myProxy != null)
                    {
                        httpClientHandler = new HttpClientHandler
                        {
                            Proxy = _myProxy
                        };
                    }


                }
                if (httpClient is null)
                {
                    httpClient = new HttpClient(httpClientHandler, false);
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
                string res = CreatePrivateQuery(new Dictionary<string, object>(), HttpMethod.Get, "/v5/user/query-api");

                if (res != null)
                {
                    ResponseRestMessage<APKeyInformation> keyInformation = JsonConvert.DeserializeObject<ResponseRestMessage<APKeyInformation>>(res);

                    if (keyInformation != null
                        && keyInformation.retCode == "0")
                    {

                        string api = keyInformation.result.apiKey;
                        apiFromServer = api.ToString();

                    }
                    else
                    {
                        SendLogMessage($"CheckApiKeyInformation>. Error. Code: {keyInformation.retCode}\n"
                            + $"Message: {keyInformation.retMsg}", LogMessageType.Error);
                    }
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

        public string CreatePrivateQuery(Dictionary<string, object> parameters, HttpMethod httpMethod, string uri)
        {
            lock (_httpClientLocker)
            {
                _rateGate.WaitToProceed();
            }

            try
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

                string response_msg = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return response_msg;
                }
                else
                {
                    if (response_msg.Contains("\"retCode\": 10006"))
                    {
                        SendLogMessage($"Limit 1000.Code:{response.StatusCode}, Message:{response_msg}", LogMessageType.Error);
                    }
                    else
                    {
                        SendLogMessage($"CreatePrivateQuery> BybitUnified Client.Code:{response.StatusCode}, Message:{response_msg}", LogMessageType.Error);
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("A task was canceled") == false)
                {
                    SendLogMessage(ex.Message, LogMessageType.Error);
                }

                return null;
            }
        }

        public string CreatePublicQuery(Dictionary<string, object> parameters, HttpMethod httpMethod, string uri)
        {
            lock (_httpClientLocker)
            {
                _rateGate.WaitToProceed();
            }

            try
            {
                string jsonPayload = parameters.Count > 0 ? GenerateQueryString(parameters) : "";
                httpClient = GetHttpClient();

                if (httpClient == null)
                {
                    return null;
                }

                // lock (_httpClientLocker)
                // {
                HttpRequestMessage request = new HttpRequestMessage(httpMethod, RestUrl + uri + $"?{jsonPayload}");
                HttpResponseMessage response = httpClient?.SendAsync(request).Result;

                if (response == null)
                {
                    return null;
                }

                string response_msg = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return response_msg;
                }
                else
                {
                    if (response_msg.Contains("\"retCode\": 10006"))
                    {
                        SendLogMessage($"Limit 1000.Code:{response.StatusCode}, Message:{response_msg}", LogMessageType.Error);
                    }
                    else
                    {
                        SendLogMessage($"CreatePublicQuery> BybitUnified Client.Code:{response.StatusCode}, Message:{response_msg}", LogMessageType.Error);
                    }

                    return null;
                }
                //  }
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

                    HttpResponseMessage response = httpClient?.SendAsync(request).Result;
                    string response_msg = response.Content.ReadAsStringAsync().Result;

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        ResponseRestMessageList<string> timeFromServer = JsonConvert.DeserializeObject<ResponseRestMessageList<string>>(response_msg);

                        if (timeFromServer != null
                        && timeFromServer.retCode == "0")
                        {
                            if (timeFromServer == null)
                            {
                                return UtcNowUnixTimeMilliseconds.ToString();
                            }
                            string timeStamp = timeFromServer.time;

                            if (long.TryParse(timeStamp.ToString(), out long timestampServer))
                            {
                                return timeStamp.ToString();
                            }

                            return UtcNowUnixTimeMilliseconds.ToString();
                        }
                        else
                        {
                            SendLogMessage($"GetServerTime>. Error. Code: {timeFromServer.retCode}\n"
                                + $"Message: {timeFromServer.retMsg}", LogMessageType.Error);
                        }
                    }
                    else
                    {
                        SendLogMessage($"GetServerTime>.Code:{response.StatusCode}, Message:{response_msg}", LogMessageType.Error);
                    }
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

        private void SetLeverage(Security security)
        {
            try
            {
                if (_leverage == "")
                {
                    return;
                }

                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs.Clear();
                parametrs["category"] = Category.linear.ToString();
                parametrs["symbol"] = security.Name.Split(".")[0];
                parametrs["buyLeverage"] = _leverage;
                parametrs["sellLeverage"] = _leverage;

                CreatePrivateQuery(parametrs, HttpMethod.Post, "/v5/position/set-leverage");
            }
            catch (Exception ex)
            {
                SendLogMessage($"SetLeverage: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
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

    public class Tickers
    {
        public string SecurityName { get; set; }
        public string OpenInterest { get; set; }
    }


    #region 14 Enum

    public enum Net_type
    {
        MainNet,
        Demo,
        Netherlands,
        HongKong,
        Turkey,
        Kazakhstan
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