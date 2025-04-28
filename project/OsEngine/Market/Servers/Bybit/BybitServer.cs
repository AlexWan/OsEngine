﻿/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
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
using WebSocketSharp;

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
            CreateParameterEnum(OsLocalization.Market.Label1, Net_type.MainNet.ToString(), new List<string>() { Net_type.MainNet.ToString(), Net_type.Demo.ToString() });
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

            Thread threadMessageReaderOrderBookInverse = new Thread(() => ThreadMessageReaderOrderBookInverse());
            threadMessageReaderOrderBookInverse.Name = "ThreadBybitMessageReaderOrderBookInverse";
            threadMessageReaderOrderBookInverse.Start();

            Thread threadMessageReaderTradesInverse = new Thread(() => ThreadMessageReaderTradesInverse());
            threadMessageReaderTradesInverse.Name = "ThreadBybitMessageReaderTradesInverse";
            threadMessageReaderTradesInverse.Start();
        }

        public void Connect(WebProxy proxy = null)
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
            SubscribeSecurityInverse.Clear();

            concurrentQueueMessagePublicWebSocket = new ConcurrentQueue<string>();
            _concurrentQueueMessageOrderBookSpot = new ConcurrentQueue<string>();
            _concurrentQueueMessageOrderBookLinear = new ConcurrentQueue<string>();
            _concurrentQueueMessageOrderBookInverse = new ConcurrentQueue<string>();
            concurrentQueueMessagePrivateWebSocket = new ConcurrentQueue<string>();

            _concurrentQueueTradesSpot = new ConcurrentQueue<string>();
            _concurrentQueueTradesLinear = new ConcurrentQueue<string>();
            _concurrentQueueTradesInverse = new ConcurrentQueue<string>();
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

        private List<string> _listLinearCurrency = new List<string>() { "USDC", "USDT" };

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

        private string test_Url = "https://api-demo.bybit.com";

        private string mainWsPublicUrl = "wss://stream.bybit.com/v5/public/";

        private string testWsPublicUrl = "wss://stream.bybit.com/v5/public/";

        private string mainWsPrivateUrl = "wss://stream.bybit.com/v5/private";

        private string testWsPrivateUrl = "wss://stream-demo.bybit.com/v5/private";

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

        private List<Security> _securities;

        public void GetSecurities()
        {
            try
            {
                if (_securities == null)
                {
                    _securities = new List<Security>();
                }

                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs.Add("limit", "1000");
                parametrs.Add("category", Category.spot);

                string security = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/instruments-info");
                ResponseRestMessage<ArraySymbols> responseSymbols;

                if (security != null)
                {
                    responseSymbols = JsonConvert.DeserializeObject<ResponseRestMessage<ArraySymbols>>(security);

                    if (responseSymbols != null
                        && responseSymbols.retCode == "0"
                        && responseSymbols.retMsg == "OK")
                    {
                        ConvertSecuritis(responseSymbols, Category.spot);
                    }
                    else
                    {
                        SendLogMessage($"Spot securities error. Code: {responseSymbols.retCode}\n"
                            + $"Message: {responseSymbols.retMsg}", LogMessageType.Error);
                    }
                }

                parametrs["category"] = Category.linear;

                security = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/instruments-info");

                if (security != null)
                {
                    responseSymbols = JsonConvert.DeserializeObject<ResponseRestMessage<ArraySymbols>>(security);

                    if (responseSymbols != null
                        && responseSymbols.retCode == "0"
                        && responseSymbols.retMsg == "OK")
                    {
                        ConvertSecuritis(responseSymbols, Category.linear);
                    }
                    else
                    {
                        SendLogMessage($"Linear securities error. Code: {responseSymbols.retCode}\n"
                            + $"Message: {responseSymbols.retMsg}", LogMessageType.Error);
                    }
                }

                parametrs["category"] = Category.inverse;

                security = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/instruments-info");

                if (security != null)
                {
                    responseSymbols = JsonConvert.DeserializeObject<ResponseRestMessage<ArraySymbols>>(security);

                    if (responseSymbols != null
                        && responseSymbols.retCode == "0"
                        && responseSymbols.retMsg == "OK")
                    {
                        ConvertSecuritis(responseSymbols, Category.inverse);
                    }
                    else
                    {
                        SendLogMessage($"Inverse securities error. Code: {responseSymbols.retCode}\n"
                            + $"Message: {responseSymbols.retMsg}", LogMessageType.Error);
                    }
                }

                SecurityEvent?.Invoke(_securities);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Securities request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void ConvertSecuritis(ResponseRestMessage<ArraySymbols> symbols, Category category)
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
                            security.DecimalsVolume = GetDecimalsVolume(oneSec.lotSizeFilter.minOrderQty);
                            security.VolumeStep = GetVolumeStepByVolumeDecimals(security.DecimalsVolume);
                        }

                        security.State = SecurityStateType.Activ;
                        security.Exchange = "ByBit";
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
            if(volumeDecimals == 0)
            {
                return 1;
            }

            string result = "0.";

            for(int i = 0;i < volumeDecimals;i++)
            {
                if(i +1 == volumeDecimals)
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
                        SendLogMessage($"GetPositionsLinear>. Position error. Code: {responsePositionOnBoard.retCode}\n"
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
            // ByBit returns no more than 1000 last ticks via API, which does not meet the connector requirements, so we will return null

            List<Trade> trades = new List<Trade>();

            try
            {
                string category = Category.spot.ToString();
                if (security.Name.EndsWith(".P"))
                {
                    category = Category.linear.ToString();
                }
                else if (security.Name.EndsWith(".I"))
                {
                    category = Category.inverse.ToString();
                }

                Dictionary<string, object> parametrs = new Dictionary<string, object>();
                parametrs.Add("category", category);
                parametrs.Add("symbol", security.Name.Split('.')[0]);
                parametrs.Add("limit", 1000);   // this is the maximum they give

                string tradesQuery = CreatePublicQuery(parametrs, HttpMethod.Get, "/v5/market/recent-newTrade");

                ResponseRestMessageList<RetTrade> responseTrades = JsonConvert.DeserializeObject<ResponseRestMessageList<RetTrade>>(tradesQuery);
                if (responseTrades == null || responseTrades.result == null || responseTrades.result.list == null || responseTrades.result.list.Count == 0)
                {
                    return null;
                }

                List<RetTrade> retTrade = new List<RetTrade>();
                retTrade = responseTrades.result.list;
                DateTime preTime = DateTime.MinValue;

                for (int i = retTrade.Count - 1; i >= 0; i--)
                {
                    RetTrade trade = retTrade[i];
                    Trade newTrade = new Trade();
                    newTrade.Id = trade.execId;
                    newTrade.Price = trade.price.ToDecimal();
                    newTrade.SecurityNameCode = trade.symbol;
                    newTrade.Side = trade.side == "Buy" ? Side.Buy : Side.Sell;
                    newTrade.Volume = trade.size.ToDecimal();
                    DateTime tradeTime = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(trade.time)).UtcDateTime;
                    if (tradeTime <= preTime)   // at the same moment in time, down to milliseconds, there can be several trades in buybit
                    {
                        // tradeTime = preTime.AddTicks(1);    // if several trades are not possible at the same time, then allow the row and add by tick
                    }

                    newTrade.Time = tradeTime;
                    newTrade.MicroSeconds = 0;
                    preTime = newTrade.Time;

                    trades.Add(newTrade);
                }
                return trades;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Trades request error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return trades;
            }
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
                }


                _webSocketPublicSpot.Add(CreateNewSpotPublicSocket());
                _webSocketPublicLinear.Add(CreateNewLinearPublicSocket());
                _webSocketPublicInverse.Add(CreateNewInversePublicSocket());

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
            webSocketPublicSpot.OnClose += WebSocketPublic_Closed;

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
            webSocketPublicLinear.OnClose += WebSocketPublic_Closed;

            webSocketPublicLinear.Connect();

            return webSocketPublicLinear;
        }

        private WebSocket CreateNewInversePublicSocket()
        {
            WebSocket webSocketPublicInverse = new WebSocket(wsPublicUrl(Category.inverse));
            webSocketPublicInverse.EmitOnPing = true;
            webSocketPublicInverse.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.None;
            webSocketPublicInverse.OnOpen += WebSocketPublic_Opened;
            webSocketPublicInverse.OnMessage += WebSocketPublicInverse_OnMessage;
            webSocketPublicInverse.OnError += WebSocketPublic_Error;
            webSocketPublicInverse.OnClose += WebSocketPublic_Closed;

            webSocketPublicInverse.Connect();

            return webSocketPublicInverse;
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

                    for (int i = 0; i < _webSocketPublicInverse.Count; i++)
                    {
                        WebSocket webSocketPublicInverse = _webSocketPublicInverse[i];

                        if (webSocketPublicInverse != null && webSocketPublicInverse?.ReadyState == WebSocketState.Open)
                        {
                            webSocketPublicInverse?.Send("{\"req_id\": \"OsEngine\", \"op\": \"ping\"}");
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
                    webSocketPublicSpot.OnClose -= WebSocketPublic_Closed;

                    try
                    {
                        if (webSocketPublicSpot != null && webSocketPublicSpot?.ReadyState == WebSocketState.Open)
                        {
                            for (int i2 = 0; i2 < SubscribeSecuritySpot.Count; i2++)
                            {
                                string s = SubscribeSecuritySpot[i2].Split('.')[0];
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
                    webSocketPublicLinear.OnClose -= WebSocketPublic_Closed;

                    try
                    {
                        if (webSocketPublicLinear != null && webSocketPublicLinear?.ReadyState == WebSocketState.Open)
                        {
                            for (int i2 = 0; i2 < SubscribeSecurityLinear.Count; i2++)
                            {
                                string s = SubscribeSecurityLinear[i2].Split('.')[0];
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
                                webSocketPublicInverse?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"publicTrade.{s}\" ] }}");
                                webSocketPublicInverse?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"unsubscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{s}\" ] }}");
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

                    webSocketPublicInverse = null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }

            _webSocketPublicInverse.Clear();

            _listMarketDepthSpot?.Clear();
            concurrentQueueMessagePublicWebSocket = null;
            _concurrentQueueMessageOrderBookSpot = null;
        }

        #endregion  8

        #region 9 Security subscrible

        private List<string> SubscribeSecuritySpot = new List<string>();

        private List<string> SubscribeSecurityLinear = new List<string>();

        private List<string> SubscribeSecurityInverse = new List<string>();

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(150));

        public void Subscrible(Security security)
        {
            try
            {
                _rateGateSubscribe.WaitToProceed();

                if (!security.Name.EndsWith(".P")
                    && !security.Name.EndsWith(".I"))
                {
                    if (SubscribeSecuritySpot.Exists(s => s == security.Name) == true)
                    {
                        // already subscribed to this
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
                        webSocketPublicSpot?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"publicTrade.{security.Name}\" ] }}");
                        webSocketPublicSpot?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{security.Name}\" ] }}");

                        if (SubscribeSecuritySpot.Exists(s => s == security.Name) == false)
                        {
                            SubscribeSecuritySpot.Add(security.Name);
                        }
                    }
                }
                else if (security.Name.EndsWith(".P"))
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
                else if (security.Name.EndsWith(".I"))
                {
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
                        if (SubscribeSecurityInverse.Exists(s => s == security.Name) == true)
                        {
                            SubscribeSecurityInverse.Add(security.Name);
                        }

                        webSocketPublicInverse?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"publicTrade.{security.Name.Replace(".I", "")}\" ] }}");
                        webSocketPublicInverse?.Send($"{{\"req_id\": \"trade0001\",  \"op\": \"subscribe\", \"args\": [\"orderbook.{marketDepthDeep}.{security.Name.Replace(".I", "")}\" ] }}");

                        if (SubscribeSecurityInverse.Exists(s => s == security.Name) == false)
                        {
                            SubscribeSecurityInverse.Add(security.Name);
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
                    else if (responseMyTrades.data[i].category == Category.linear.ToString())
                    {
                        myTrade.SecurityNameCode = responseMyTrades.data[i].symbol + ".P";
                    }
                    else if (responseMyTrades.data[i].category == Category.inverse.ToString())
                    {
                        myTrade.SecurityNameCode = responseMyTrades.data[i].symbol + ".I";
                    }

                    myTrade.Time = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(responseMyTrades.data[i].execTime));
                    myTrade.NumberOrderParent = responseMyTrades.data[i].orderId;
                    myTrade.NumberTrade = responseMyTrades.data[i].execId;
                    myTrade.Price = responseMyTrades.data[i].execPrice.ToDecimal();
                    myTrade.Side = responseMyTrades.data[i].side.ToUpper().Equals("BUY") ? Side.Buy : Side.Sell;

                    if (responseMyTrades.data[i].category == Category.spot.ToString() && myTrade.Side == Side.Buy && !string.IsNullOrWhiteSpace(responseMyTrades.data[i].execFee))   // комиссия на споте при покупке берется с купленой монеты
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

        private void ThreadMessageReaderOrderBookInverse()
        {
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
                        continue;
                    }

                    Category category = Category.inverse;

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

        private ConcurrentQueue<string> _concurrentQueueMessageOrderBookInverse;

        private Dictionary<string, MarketDepth> _listMarketDepthSpot = new Dictionary<string, MarketDepth>();

        private Dictionary<string, MarketDepth> _listMarketDepthLinear = new Dictionary<string, MarketDepth>();

        private Dictionary<string, MarketDepth> _listMarketDepthInverse = new Dictionary<string, MarketDepth>();

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

                if (response.type == "snapshot")
                {
                    marketDepth.Asks.Clear();
                    marketDepth.Bids.Clear();
                }

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
                    && _concurrentQueueMessageOrderBookInverse?.Count < 500)
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

        private void ThreadMessageReaderTradesInverse()
        {
            while (true)
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    Thread.Sleep(3000);
                }

                try
                {
                    if (_concurrentQueueTradesInverse == null
                        || _concurrentQueueTradesInverse.IsEmpty
                        || _concurrentQueueTradesInverse.Count == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (!_concurrentQueueTradesInverse.TryDequeue(out string message))
                    {
                        continue;
                    }

                    Category category = Category.inverse;
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

                        NewTradesEvent?.Invoke(trade);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
        }

        public event Action<Trade> NewTradesEvent;

        #endregion 10

        #region 11 Trade

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

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
                else
                {
                    parameters["category"] = Category.spot.ToString();
                }

                parameters["symbol"] = order.SecurityNameCode.Split('.')[0];
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

                string jsonPayload = parameters.Count > 0 ? GenerateQueryString(parameters) : "";

                DateTime startTime = DateTime.Now;
                string place_order_response = CreatePrivateQuery(parameters, HttpMethod.Post, "/v5/order/create");

                string isSuccessful = "ByBit error. The order was not accepted.";

                if (place_order_response != null)
                {
                    ResponseRestMessageList<string> responseOrder = JsonConvert.DeserializeObject<ResponseRestMessageList<string>>(place_order_response);
                    isSuccessful = responseOrder.retMsg;

                    if (responseOrder != null
                        && responseOrder.retCode == "0"
                        && isSuccessful == "OK")
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

        public void CancelOrder(Order order)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();

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
                        return;
                    }
                    else if (responseOrder.retCode == "110001" || responseOrder.retCode == "170213")   // "retCode":110001,"retMsg":"order not exists or too late to cancel"
                                                                                                       //  retCode":170213,"retMsg":"Order does not exist."
                                                                                                       // The order does not exist (maybe it has not yet been created) or has already been cancelled. Let's ask about its status
                    {
                        GetOrderStatus(order);
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
                    SendLogMessage($" Cancel Order Error. Code: {responseOrder.retCode}.\n" +
                        $" Order num {order.NumberUser}, {order.SecurityNameCode} {responseOrder.retMsg}", LogMessageType.Error);
                }
            }
            catch
            {
                SendLogMessage($" Cancel Order Error. Order num {order.NumberUser}, {order.SecurityNameCode}", LogMessageType.Error);
                return;
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
                List<Order> ordersOpenAll = new List<Order>();

                List<Order> spotOrders = GetOpenOrders(Category.spot, null);

                if (spotOrders != null
                    && spotOrders.Count > 0)
                {
                    ordersOpenAll.AddRange(spotOrders);
                }

                List<Order> inverseOrders = GetOpenOrders(Category.inverse, null);

                if (inverseOrders != null
                    && inverseOrders.Count > 0)
                {
                    ordersOpenAll.AddRange(inverseOrders);
                }

                List<Order> linearOrders = null;

                for (int i = 0; i < _listLinearCurrency.Count; i++)
                {
                    linearOrders = GetOpenOrders(Category.linear, _listLinearCurrency[i]);

                    if (linearOrders != null
                    && linearOrders.Count > 0)
                    {
                        ordersOpenAll.AddRange(linearOrders);
                    }
                }

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

        public void GetOrderStatus(Order order)
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

                Order newOrder = GetOrderFromHistory(order, category);

                if (newOrder == null)
                {
                    List<Order> openOrders = null;

                    if (category == Category.linear)
                    {
                        if (order.SecurityNameCode.Contains("USDT"))
                        {
                            openOrders = GetOpenOrders(category, "USDT");
                        }
                        else
                        {
                            openOrders = GetOpenOrders(category, "USDC");
                        }
                    }
                    else if (category == Category.spot)
                    {
                        openOrders = GetOpenOrders(category, null);
                    }
                    else if (category == Category.inverse)
                    {
                        openOrders = GetOpenOrders(category, null);
                    }

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
                SendLogMessage($"GetOrderStatus>. Order error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {
            try
            {
                List<Order> ordersOpenAll = new List<Order>();

                List<Order> spotOrders = GetOpenOrders(Category.spot, null);

                if (spotOrders != null
                    && spotOrders.Count > 0)
                {
                    ordersOpenAll.AddRange(spotOrders);
                }

                List<Order> inverseOrders = GetOpenOrders(Category.inverse, null);

                if (inverseOrders != null
                    && inverseOrders.Count > 0)
                {
                    ordersOpenAll.AddRange(inverseOrders);
                }

                List<Order> linearOrders = null;

                for (int i = 0; i < _listLinearCurrency.Count; i++)
                {
                    linearOrders = GetOpenOrders(Category.linear, _listLinearCurrency[i]);

                    if (linearOrders != null
                    && linearOrders.Count > 0)
                    {
                        ordersOpenAll.AddRange(linearOrders);
                    }
                }

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

        private List<Order> GetOpenOrders(Category category, string settleCoin)
        {
            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();

                parameters["category"] = category;
                parameters["openOnly"] = "0";

                if (category == Category.linear)
                {
                    parameters["settleCoin"] = settleCoin;
                }

                string orders_response = CreatePrivateQuery(parameters, HttpMethod.Get, "/v5/order/realtime");

                if (orders_response == null)
                {
                    return null;
                }

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
                        newOrder.State = OrderStateType.Active;
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

                    return activeOrders;
                }
                else
                {
                    SendLogMessage($"GetOpenOrders>. Order error. Code: {responseOrder.retCode}\n"
                            + $"Message: {responseOrder.retMsg}", LogMessageType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetOpenOrders>. Order error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        private Order GetOrderFromHistory(Order orderBase, Category category)
        {
            try
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();

                parameters["category"] = category;
                parameters["symbol"] = orderBase.SecurityNameCode.Split('.')[0].ToUpper();

                string orders_response = CreatePrivateQuery(parameters, HttpMethod.Get, "/v5/order/history");

                if (orders_response == null)
                {
                    return null;
                }

                ResponseRestMessageList<ResponseMessageOrders> responseOrder = JsonConvert.DeserializeObject<ResponseRestMessageList<ResponseMessageOrders>>(orders_response);

                if (responseOrder != null
                     && responseOrder.retCode == "0"
                     && responseOrder.retMsg == "OK")
                {
                    List<ResponseMessageOrders> ordChild = responseOrder.result.list;

                    for (int i = 0; i < ordChild.Count; i++)
                    {
                        ResponseMessageOrders order = ordChild[i];
                        Order newOrder = new Order();
                        string status = order.orderStatus;

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

                        if (newOrder.NumberUser != orderBase.NumberUser)
                        {
                            continue;
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

                        return newOrder;
                    }
                }
                else
                {
                    SendLogMessage($"GetOrderFromHistory>. Order error. Code: {responseOrder.retCode}\n"
                            + $"Message: {responseOrder.retMsg}", LogMessageType.Error);
                }

                return null;
            }
            catch (Exception ex)
            {
                SendLogMessage($"GetOrderFromHistory>. Order error. {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
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
                    httpClientHandler = new HttpClientHandler();
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

                    string response_msg = response.Content.ReadAsStringAsync().Result;

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        return response_msg;
                    }
                    else
                    {
                        SendLogMessage($"CreatePrivateQuery> BybitUnified Client.Code:{response.StatusCode}, Message:{response_msg}", LogMessageType.Error);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
                return null;
            }
        }

        public string CreatePublicQuery(Dictionary<string, object> parameters, HttpMethod httpMethod, string uri)
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

                    string response_msg = response.Content.ReadAsStringAsync().Result;

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        return response_msg;
                    }
                    else
                    {
                        SendLogMessage($"CreatePublicQuery> BybitUnified Client.Code:{response.StatusCode}, Message:{response_msg}", LogMessageType.Error);
                        return null;
                    }
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
        Demo
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