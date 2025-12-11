using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.Utils;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.QuikLua.Entity;
using QuikSharp;
using QuikSharp.DataStructures;
using QuikSharp.DataStructures.Transaction;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Candle = OsEngine.Entity.Candle;
using Order = OsEngine.Entity.Order;
using Trade = OsEngine.Entity.Trade;

namespace OsEngine.Market.Servers.QuikLua
{
    public class QuikLuaServer : AServer
    {
        public QuikLuaServer()
        {
            ServerRealization = new QuikLuaServerRealization();

            CreateParameterBoolean(OsLocalization.Market.UseStock, true); // 0
            CreateParameterBoolean(OsLocalization.Market.UseFutures, true); // 1
            CreateParameterBoolean(OsLocalization.Market.UseCurrency, true); // 2
            CreateParameterBoolean(OsLocalization.Market.UseOptions, false); // 3
            CreateParameterBoolean(OsLocalization.Market.UseBonds, false); // 4
            CreateParameterBoolean(OsLocalization.Market.UseOther, false); // 5
            CreateParameterBoolean(OsLocalization.Market.Label109, false); // 6
            CreateParameterString("Client code", null); // 7
            CreateParameterBoolean(OsLocalization.Market.Label162, false); // 8
            CreateParameterEnum(OsLocalization.Market.Label307, "T0", new List<string> { "T0", "T1", "T2", "NotImplemented" }); // 9

            ServerParameters[0].Comment = OsLocalization.Market.Label107;
            ServerParameters[1].Comment = OsLocalization.Market.Label107;
            ServerParameters[2].Comment = OsLocalization.Market.Label107;
            ServerParameters[3].Comment = OsLocalization.Market.Label96;
            ServerParameters[4].Comment = OsLocalization.Market.Label107;
            ServerParameters[5].Comment = OsLocalization.Market.Label97;
            ServerParameters[6].Comment = OsLocalization.Market.Label110;
            ServerParameters[7].Comment = OsLocalization.Market.Label121;
            ServerParameters[8].Comment = OsLocalization.Market.Label163;
            ServerParameters[9].Comment = OsLocalization.Market.Label308;

            ((ServerParameterBool)ServerParameters[0]).ValueChange += QuikLuaServer_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[1]).ValueChange += QuikLuaServer_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[2]).ValueChange += QuikLuaServer_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[3]).ValueChange += QuikLuaServer_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[4]).ValueChange += QuikLuaServer_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[5]).ValueChange += QuikLuaServer_ParametrValueChange;

            ((QuikLuaServerRealization)ServerRealization).ClientCodeFromSettings
                = (ServerParameterString)ServerParameters[7];
        }

        /// <summary>
        /// контроль изменения списка классов используемых в коннекторе 
        /// </summary>
        private void QuikLuaServer_ParametrValueChange()
        {
            ((QuikLuaServerRealization)ServerRealization)._changeClassUse = true;
            Securities?.Clear();    // AVP  изменили список классов для работы, старый удалим и в коннекторе заново перечитаем
        }
    }

    public class QuikLuaServerRealization : IServerRealization
    {

        #region 1 Constructor, Status, Connection

        public QuikLuaServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Trace.Listeners.Add(new CustomTraceListener());

            CustomTraceListener.OnTraceMessageReceived += message =>
            {
                if (message.Contains("ThrowOperationCanceledException") || message.Contains("TaskCanceledException"))
                {
                    return;
                }
                SendLogMessage($"Ошибка в QuikSharp: {message}", LogMessageType.Error);
            };

            Thread worker1 = new Thread(GetPortfoliosArea);
            worker1.CurrentCulture = new CultureInfo("ru-Ru");
            worker1.Start();

            Thread worker2 = new Thread(ThreadTradesParsingWorkPlace);
            worker2.CurrentCulture = new CultureInfo("ru-Ru");
            worker2.Start();

            Thread worker3 = new Thread(ThreadMarketDepthsParsingWorkPlace);
            worker3.CurrentCulture = new CultureInfo("ru-Ru");
            worker3.Start();

            Thread worker4 = new Thread(ThreadDataParsingWorkPlace);
            worker4.CurrentCulture = new CultureInfo("ru-RU");
            worker4.Start();

            Thread worker5 = new Thread(ThreadPing);
            worker5.CurrentCulture = new CultureInfo("ru-RU");
            worker5.Start();
        }

        public void Connect(WebProxy proxy)
        {
            try
            {
                if (QuikLua == null)
                {
                    _useStock = (ServerParameterBool)ServerParameters[0];
                    _useFutures = (ServerParameterBool)ServerParameters[1];
                    _useCurrency = (ServerParameterBool)ServerParameters[2];
                    _useOptions = (ServerParameterBool)ServerParameters[3];
                    _useBonds = (ServerParameterBool)ServerParameters[4];
                    _useOther = (ServerParameterBool)ServerParameters[5];
                    _isClientCodeOne = ((ServerParameterBool)ServerParameters[8]).Value;
                    string tradeMode = ((ServerParameterEnum)ServerParameters[9]).Value;

                    if (tradeMode == "T0") _tradeMode = 0;
                    else if (tradeMode == "T1") _tradeMode = 1;
                    else if (tradeMode == "T2") _tradeMode = 2;
                    else if (tradeMode == "NotImplemented") _tradeMode = 3;

                    QuikLua = new Quik(Quik.DefaultPort, new InMemoryStorage());
                    QuikLua.Events.OnConnected += EventsOnOnConnected;
                    QuikLua.Events.OnDisconnected += EventsOnOnDisconnected;
                    QuikLua.Events.OnConnectedToQuik += EventsOnOnConnectedToQuik;
                    QuikLua.Events.OnDisconnectedFromQuik += EventsOnOnDisconnectedFromQuik;
                    QuikLua.Events.OnClose += Events_OnClose;
                    QuikLua.Events.OnTrade += EventsOnOnTrade;
                    QuikLua.Events.OnOrder += EventsOnOnOrder;
                    QuikLua.Events.OnQuote += EventsOnOnQuote;
                    QuikLua.Events.OnFuturesClientHolding += EventsOnOnFuturesClientHolding;
                    QuikLua.Events.OnFuturesLimitChange += EventsOnOnFuturesLimitChange;
                    QuikLua.Events.OnTransReply += Events_OnTransReply;

                    QuikLua.Service.QuikService.Start();

                    if (string.IsNullOrEmpty(ClientCodeFromSettings.Value) == false)
                    {
                        _clientCode = ClientCodeFromSettings.Value;
                    }
                    else
                    {
                        if (_clientCode == null)
                        {
                            _clientCode = QuikLua.Class.GetClientCode().Result;
                        }
                    }

                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        ServerStatus = ServerConnectStatus.Connect;
                        ConnectEvent();
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            try
            {
                if (QuikLua != null && QuikLua.Service.IsConnected().Result)
                {
                    bool isStoped = QuikLua.Service.QuikService.Stop();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            try
            {
                if (QuikLua != null)
                {
                    QuikLua.Events.OnConnected -= EventsOnOnConnected;
                    QuikLua.Events.OnDisconnected -= EventsOnOnDisconnected;
                    QuikLua.Events.OnConnectedToQuik -= EventsOnOnConnectedToQuik;
                    QuikLua.Events.OnDisconnectedFromQuik -= EventsOnOnDisconnectedFromQuik;
                    QuikLua.Events.OnTrade -= EventsOnOnTrade;
                    QuikLua.Events.OnOrder -= EventsOnOnOrder;
                    QuikLua.Events.OnQuote -= EventsOnOnQuote;
                    QuikLua.Events.OnFuturesClientHolding -= EventsOnOnFuturesClientHolding;
                    QuikLua.Events.OnFuturesLimitChange -= EventsOnOnFuturesLimitChange;
                    QuikLua.Events.OnTransReply -= Events_OnTransReply;
                }

                subscribedSecurities = new List<Security>();
                _clientCode = null;
                QuikLua = null;

                if (_myTradesQueue != null) _myTradesQueue.Clear();
                if (_mdQueue != null) _mdQueue.Clear();
                if (_ordersQueue != null) _ordersQueue.Clear();
                if (_portfolios != null) _portfolios.Clear();
                if (_sentOrders != null) _sentOrders.Clear();
                if (_trades != null) _trades.Clear();
                if (_myTradesFromQuik != null) _myTradesFromQuik.Clear();

                _lastTimePingMarketDepth = DateTime.MinValue;
                _lastTimePingMyOrders = DateTime.MinValue;
                _lastTimePingPortfoios = DateTime.MinValue;
                _lastTimePingTrades = DateTime.MinValue;

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public ServerConnectStatus ServerStatus { get; set; }

        public ServerType ServerType => ServerType.QuikLua;

        public List<IServerParameter> ServerParameters { get; set; }

        public DateTime ServerTime { get; set; }

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action ConnectEvent;

        /// <summary>
        /// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region 2 Properties

        public ServerParameterString ClientCodeFromSettings;

        public QuikSharp.Quik QuikLua;

        private object _serverLocker = new object();

        private static readonly Char Separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

        private static readonly string SecuritiesCachePath = @"Engine\QuikLuaSecuritiesCache.txt";

        private ServerParameterBool _useStock;

        private ServerParameterBool _useFutures;

        private ServerParameterBool _useBonds;

        private ServerParameterBool _useOptions;

        private ServerParameterBool _useCurrency;

        private ServerParameterBool _useOther;

        public bool _changeClassUse = false;

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private RateGate _gateToGetCandles = new RateGate(1, TimeSpan.FromMilliseconds(500));

        private int _tradeMode;

        private bool _isClientCodeOne = false;

        /// <summary>
        /// called when order changed
        /// вызывается когда изменился ордер
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// called when my trade changed
        /// вызывается когда изменился мой трейд
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// appeared new portfolios
        /// появились новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
        /// new securities
        /// новые бумаги
        /// </summary>
        public event Action<List<Security>> SecurityEvent;

        /// <summary>
        /// new depth
        /// новый стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
        /// new trade
        /// новый трейд
        /// </summary>
        public event Action<Trade> NewTradesEvent;

        /// <summary>
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion

        #region 3 Securities

        private List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            try
            {
                _securities = !_changeClassUse && IsLoadSecuritiesFromCache() ? LoadSecuritiesFromCache() : LoadSecuritiesFromQuik();   //AVP добавил !_changeClassUse, если набор классов поменяли, то надо кэш обновить

                if (_securities == null)
                {
                    return;
                }

                SendLogMessage(OsLocalization.Market.Message52 + _securities.Count, LogMessageType.System);
                if (SecurityEvent != null)
                {
                    SecurityEvent(_securities);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<Security> LoadSecuritiesFromCache()
        {
            try
            {
                using (StreamReader reader = new StreamReader(SecuritiesCachePath))
                {
                    string data = CompressionUtils.Decompress(reader.ReadToEnd());
                    List<Security> list = JsonConvert.DeserializeObject<List<Security>>(data);
                    return list != null && list.Count != 0 ? list : LoadSecuritiesFromQuik();
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
                return LoadSecuritiesFromQuik();
            }
        }

        private List<Security> LoadSecuritiesFromQuik()
        {
            try
            {
                string[] classesList;

                lock (_serverLocker)
                {
                    classesList = QuikLua.Class.GetClassesList().Result;
                }

                List<SecurityInfo> allSec = new List<SecurityInfo>();

                for (int i = 0; i < classesList.Length; i++)
                {
                    if (classesList[i].EndsWith("INFO"))
                    {
                        continue;
                    }

                    if (!CheckFilter(classesList[i]))  // AVP фильтр выбранных классов для загрузки инструментов
                    {
                        continue;
                    }

                    string[] secCodes = QuikLua.Class.GetClassSecurities(classesList[i]).Result;
                    for (int j = 0; j < secCodes.Length; j++)
                    {
                        allSec.Add(QuikLua.Class.GetSecurityInfo(classesList[i], secCodes[j]).Result);
                    }
                }

                List<Security> securities = new List<Security>();
                for (int i = 0; i < allSec.Count; i++)
                {
                    SecurityInfo oneSec = allSec[i];
                    BuildSecurity(oneSec, securities);
                }

                if (securities.Count > 0)
                {
                    SaveToCache(securities);
                }

                return securities;

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return new List<Security>();
        }

        private void SaveToCache(List<Security> list)
        {
            if (list == null)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(SecuritiesCachePath, false))
                {
                    string data = CompressionUtils.Compress(list.ToJson());
                    writer.WriteLine(data);
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private void BuildSecurity(SecurityInfo oneSec, List<Security> securities)
        {
            try
            {
                if (oneSec == null)
                {
                    return;
                }

                Security newSec = new Security();
                string secCode = oneSec.SecCode;
                string classCode = oneSec.ClassCode;
                if (oneSec.ClassCode == "SPBFUT")
                {
                    newSec.SecurityType = SecurityType.Futures;
                    newSec.UsePriceStepCostToCalculateVolume = true;
                    string exp = oneSec.MatDate;
                    newSec.Expiration = new DateTime(Convert.ToInt32(exp.Substring(0, 4))
                        , Convert.ToInt32(exp.Substring(4, 2))
                        , Convert.ToInt32(exp.Substring(6, 2)));

                    newSec.MarginBuy = QuikLua.Trading
                        .GetParamEx(classCode, secCode, "SELLDEPO")
                        .Result.ParamValue.Replace('.', Separator).ToDecimal();
                }
                else if (oneSec.ClassCode == "SPBOPT")
                {
                    newSec.SecurityType = SecurityType.Option;
                    newSec.UsePriceStepCostToCalculateVolume = true;

                    newSec.OptionType = QuikLua.Trading.GetParamEx(classCode, secCode, "OPTIONTYPE")
                        .Result.ParamImage == "Put"
                        ? OptionType.Put
                        : OptionType.Call;

                    string exp = oneSec.MatDate;
                    newSec.Expiration = new DateTime(Convert.ToInt32(exp.Substring(0, 4))
                        , Convert.ToInt32(exp.Substring(4, 2))
                        , Convert.ToInt32(exp.Substring(6, 2)));

                    newSec.MarginBuy = QuikLua.Trading
                        .GetParamEx(classCode, secCode, "SELLDEPO")
                        .Result.ParamValue.Replace('.', Separator).ToDecimal();

                    newSec.Strike = QuikLua.Trading
                        .GetParamEx(classCode, secCode, "STRIKE")
                        .Result.ParamValue.Replace('.', Separator).ToDecimal();
                }
                else
                {
                    newSec.SecurityType = SecurityType.Stock;
                }

                newSec.Name = oneSec.SecCode + "+" + oneSec.ClassCode;

                if (oneSec.Name == null || oneSec.Name == "")
                {
                    newSec.NameFull = newSec.Name;
                    newSec.NameId = newSec.Name;
                }
                else
                {
                    newSec.NameFull = oneSec.Name;
                    newSec.NameId = oneSec.Name;
                }

                newSec.State = SecurityStateType.Activ;
                newSec.Exchange = "MOEX";
                newSec.VolumeStep = 1;

                newSec.Decimals = Convert.ToInt32(oneSec.Scale);

                if (oneSec.ClassCode != "SPBFUT")
                {
                    newSec.Lot = Convert.ToDecimal(oneSec.LotSize);
                }
                else
                {
                    newSec.Lot = 1;
                }

                newSec.NameClass = oneSec.ClassCode;

                newSec.PriceLimitHigh = QuikLua.Trading
                    .GetParamEx(classCode, secCode, "PRICEMAX")
                    .Result.ParamValue.Replace('.', Separator).ToDecimal();

                newSec.PriceLimitLow = QuikLua.Trading
                    .GetParamEx(classCode, secCode, "PRICEMIN")
                    .Result.ParamValue.Replace('.', Separator).ToDecimal();

                newSec.PriceStep = QuikLua.Trading
                    .GetParamEx(classCode, secCode, "SEC_PRICE_STEP")
                    .Result.ParamValue.Replace('.', Separator).ToDecimal();

                newSec.PriceStepCost = QuikLua.Trading
                    .GetParamEx(classCode, secCode, "STEPPRICE")
                    .Result.ParamValue.Replace('.', Separator).ToDecimal();

                if (newSec.PriceStep == 0 &&
                    newSec.Decimals > 0)
                {
                    newSec.PriceStep = newSec.Decimals * 0.1m;
                }

                if (newSec.PriceStep == 0)
                {
                    newSec.PriceStep = 1;
                }

                if (newSec.PriceStepCost == 0)
                {
                    newSec.PriceStepCost = newSec.PriceStep;
                }

                securities.Add(newSec);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _portfolios;

        public void GetPortfolios() { }

        private void GetPortfoliosArea()
        {
            while (true)
            {
                _lastTimePingPortfoios = DateTime.Now;

                try
                {
                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    if (QuikLua == null)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    Char separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
                    if (_portfolios == null)
                    {
                        _portfolios = new List<Portfolio>();
                    }

                    List<TradesAccounts> accaunts = QuikLua.Class.GetTradeAccounts().Result;

                    List<DepoLimitEx> spotPos = QuikLua.Trading.GetDepoLimits().Result;

                    List<MoneyLimitEx> money = QuikLua.Trading.GetMoneyLimits().Result;

                    List<FuturesLimits> futuresLimits = QuikLua.Trading.GetFuturesClientLimits().Result;

                    if (accaunts == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < accaunts.Count; i++)
                    {
                        if (String.IsNullOrWhiteSpace(accaunts[i].ClassCodes))
                        {
                            continue;
                        }

                        Portfolio myPortfolio = _portfolios.Find(p => p.Number == accaunts[i].TrdaccId);

                        if (myPortfolio == null)
                        {
                            myPortfolio = new Portfolio();
                            _portfolios.Add(myPortfolio);
                        }

                        myPortfolio.Number = accaunts[i].TrdaccId;

                        PortfolioInfoEx qPortfolio = new PortfolioInfoEx();

                        if (_isClientCodeOne == false && QuikLua != null)
                            qPortfolio = QuikLua.Trading.GetPortfolioInfoEx(accaunts[i].Firmid, accaunts[i].TrdaccId, _tradeMode).Result;
                        else if (QuikLua != null)
                            qPortfolio = QuikLua.Trading.GetPortfolioInfoEx(accaunts[i].Firmid, _clientCode, _tradeMode).Result;

                        if (qPortfolio != null && (qPortfolio.PortfolioValue == null ||
                            qPortfolio.PortfolioValue.ToDecimal() == 0))
                        {
                            if (QuikLua == null) continue;

                            PortfolioInfoEx qPortfolioEx = QuikLua.Trading.GetPortfolioInfoEx(accaunts[i].Firmid, myPortfolio.Number, _tradeMode).Result;

                            if (qPortfolioEx != null &&
                                qPortfolioEx.StartLimitOpenPos != null)
                            {
                                qPortfolio.InAssets = qPortfolioEx.StartLimitOpenPos;
                            }

                            if (qPortfolioEx != null &&
                                qPortfolioEx.PortfolioValue != null)
                            {
                                qPortfolio.PortfolioValue = qPortfolioEx.PortfolioValue;
                            }

                            if (qPortfolioEx.IsFutures != "")
                            {
                                qPortfolio = qPortfolioEx;

                                for (int i2 = 0; i2 < futuresLimits.Count; i2++)
                                {
                                    if (futuresLimits[i2].TrdAccId != qPortfolioEx.IsFutures) continue;

                                    string begin = futuresLimits[i2].CbpPrevLimit.ToString().Replace('.', separator);
                                    qPortfolio.InAllAssets = begin;

                                    string varMargin = futuresLimits[i2].VarMargin.ToString().Replace('.', separator);
                                    string CbpLUsed = futuresLimits[i2].CbpLUsed.ToString().Replace('.', separator);
                                    string CbpLPlanned = futuresLimits[i2].CbpLPlanned.ToString().Replace('.', separator);

                                    qPortfolio.AllAssets = (CbpLUsed.ToDecimal() + CbpLPlanned.ToDecimal() + varMargin.ToDecimal()).ToString();

                                    break;
                                }
                            }
                        }

                        if (qPortfolio != null && qPortfolio.InAllAssets != null)
                        {
                            string begin = qPortfolio.InAllAssets.Replace('.', separator);

                            int dotIndex = begin.IndexOf(separator);
                            if (dotIndex > 0 && begin.Length > dotIndex + 5)
                                begin = begin.Substring(0, dotIndex + 5);

                            myPortfolio.ValueBegin = begin.ToDecimal();
                        }

                        if (qPortfolio != null && qPortfolio.AllAssets != null)
                        {
                            string current = qPortfolio.AllAssets.Replace('.', separator);

                            int dotIndex = current.IndexOf(separator);
                            if (dotIndex > 0 && current.Length > dotIndex + 5)
                                current = current.Substring(0, dotIndex + 5);

                            myPortfolio.ValueCurrent = current.ToDecimal();
                        }

                        if (qPortfolio != null && qPortfolio.TotalLockedMoney != null)
                        {
                            string blocked = qPortfolio.TotalLockedMoney.Replace('.', separator);

                            int dotIndex = blocked.IndexOf(separator);
                            if (dotIndex > 0 && blocked.Length > dotIndex + 5)
                                blocked = blocked.Substring(0, dotIndex + 5);

                            myPortfolio.ValueBlocked = blocked.Remove(blocked.Length - 4).ToDecimal();
                        }

                        if (qPortfolio != null && qPortfolio.ProfitLoss != null)
                        {
                            string profit = qPortfolio.ProfitLoss.Replace('.', separator);

                            int dotIndex = profit.IndexOf(separator);
                            if (dotIndex > 0 && profit.Length > dotIndex + 5)
                                profit = profit.Substring(0, dotIndex + 5);

                            myPortfolio.UnrealizedPnl = profit.Remove(profit.Length - 4).ToDecimal();
                        }

                        UpdateSpotPosition(spotPos, myPortfolio, money, qPortfolio);
                    }

                    Thread.Sleep(5000);

                    if (PortfolioEvent != null)
                    {
                        PortfolioEvent(_portfolios);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void UpdateSpotPosition(List<DepoLimitEx> spotPos, Portfolio needPortf, List<MoneyLimitEx> money, PortfolioInfoEx portfolioEx)
        {
            try
            {
                if (spotPos == null) return;

                Char separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

                for (int i = 0; i < spotPos.Count; i++)
                {
                    DepoLimitEx pos = spotPos[i];

                    if (needPortf.Number != pos.TrdAccId) continue;

                    Security sec = subscribedSecurities.Find(sec => sec.Name.Split('+')[0] == pos.SecCode);

                    LimitKind limitKind = LimitKind.T0;

                    if (_tradeMode == 0) limitKind = LimitKind.T0;
                    else if (_tradeMode == 1) limitKind = LimitKind.T1;
                    else if (_tradeMode == 2) limitKind = LimitKind.T2;
                    else limitKind = LimitKind.NotImplemented;

                    PositionOnBoard position = new PositionOnBoard();

                    if (pos.LimitKind == limitKind && sec != null)
                    {
                        position.PortfolioName = pos.TrdAccId;
                        position.ValueBegin = pos.OpenBalance / sec.Lot;
                        position.ValueCurrent = pos.CurrentBalance / sec.Lot;
                        position.ValueBlocked = pos.LockedSell / sec.Lot;
                        position.SecurityNameCode = sec.Name;

                        needPortf.SetNewPosition(position);
                    }

                    PositionOnBoard position2 = new PositionOnBoard();

                    for (int i2 = 0; i2 < money.Count; i2++)
                    {
                        if (_clientCode != money[i2].ClientCode || _tradeMode != money[i2].LimitKind) continue;

                        position2.PortfolioName = pos.TrdAccId;
                        position2.SecurityNameCode = "rub";
                        position2.ValueBlocked = 0;
                        position2.ValueCurrent = money[i2].CurrentBal.ToDecimal();
                        position2.ValueBegin = money[i2].OpenBal.ToDecimal();

                        needPortf.SetNewPosition(position2);

                        break;
                    }
                }

                if (portfolioEx != null && portfolioEx.IsFutures != "")
                {
                    PositionOnBoard position = new PositionOnBoard();

                    position.PortfolioName = portfolioEx.IsFutures;
                    position.SecurityNameCode = "rub";
                    position.ValueBlocked = 0;
                    position.ValueCurrent = portfolioEx.LimitOpenPos.ToDecimal();
                    position.ValueBegin = portfolioEx.StartLimitOpenPos.ToDecimal();

                    needPortf.SetNewPosition(position);
                }

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                Thread.Sleep(5000);
            }
        }

        #endregion

        #region 5 Data

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            try
            {
                List<Trade> AllHistoricalTrades = new List<Trade>();

                //скачаем новые данные из квика. (доступна только текущая сессия. с 19.00 вчерашнего по 18.45 текущего дня)	
                List<Trade> newTrades = GetQuikLuaTickHistory(security);

                if (newTrades == null) { return null; }

                //сохраним новые данные	
                if (!Directory.Exists(@"Data\Temp\"))
                {
                    Directory.CreateDirectory(@"Data\Temp\");
                }

                DateTime fileNameDate = DateTime.Now.TimeOfDay.Hours < 19 ? DateTime.Now.Date : DateTime.Now.Date.AddDays(1);
                string fileName = @"Data\Temp\" + security.Name + "_QuikLuaServer_" + fileNameDate.ToShortDateString() + ".txt";

                StreamWriter writer = new StreamWriter(fileName, false);
                for (int i = 0; i < newTrades.Count; i++)
                {
                    writer.WriteLine(newTrades[i].GetSaveString());
                }

                writer.Close();

                // объединим со старыми данными, если они есть	
                List<string> files = Directory.GetFiles(@"Data\Temp\", "*").ToList().FindAll(x => x.Contains(security.Name + "_QuikLuaServer_"));

                for (int i = 0; i < files.Count; i++)
                {
                    StreamReader reader = new StreamReader(files[i]);

                    while (!reader.EndOfStream)
                    {
                        try
                        {
                            Trade newTrade = new Trade();
                            newTrade.SetTradeFromString(reader.ReadLine());
                            newTrade.SecurityNameCode = security.Name;
                            AllHistoricalTrades.Add(newTrade);
                        }
                        catch
                        {
                            // ignore	
                        }
                    }
                    reader.Close();
                }
                return AllHistoricalTrades;

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return new List<Trade>();
        }

        /// <summary>
        /// ticks downloaded using method GetQuikLuaTickHistory
        /// тиковые данные скаченные из метода GetQuikLuaTickHistory
        /// </summary>
        private List<Trade> _trades;

        /// <summary>
        /// download all ticks by instrument
        /// скачать все тиковые данные по инструменту
        /// </summary>
        /// <param name="security"> short security name/короткое название бумаги</param>
        /// <returns>failure will return null/в случае неудачи вернётся null</returns>
        public List<Trade> GetQuikLuaTickHistory(Security security)
        {
            try
            {
                Security needSec = _securities.Find(sec =>
                    sec.Name == security.Name && sec.NameClass == security.NameClass);

                _trades = new List<Trade>();

                if (needSec != null)
                {
                    string classCode = needSec.NameClass;

                    List<QuikSharp.DataStructures.Candle> allCandlesForSec = QuikLua.Candles.GetAllCandles(classCode, needSec.Name.Split('+')[0], CandleInterval.TICK).Result;

                    if (allCandlesForSec == null) { return null; }

                    for (int i = 0; i < allCandlesForSec.Count; i++)
                    {
                        if (allCandlesForSec[i] != null)
                        {
                            Trade newTrade = new Trade();
                            newTrade.Price = allCandlesForSec[i].Close;
                            newTrade.Volume = allCandlesForSec[i].Volume;
                            newTrade.Time = (DateTime)allCandlesForSec[i].Datetime;
                            newTrade.MicroSeconds = allCandlesForSec[i].Datetime.mcs;
                            newTrade.SecurityNameCode = security.Name;
                            _trades.Add(newTrade);
                        }
                    }
                }

                return _trades;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// candles downloadin with using method GetLastCandleHistory
        /// свечи скаченные из метода GetLastCandleHistory
        /// </summary>
        private List<Candle> _candles;

        private object _getCandlesLocker = new object();

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            try
            {
                lock (_getCandlesLocker)
                {
                    _gateToGetCandles.WaitToProceed();

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        return null;
                    }

                    if (timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes > 1440 ||
                        timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes < 1)
                    {
                        return null;
                    }

                    CandleInterval candleInterval = SelectTimeFrame(timeFrameBuilder.TimeFrame);

                    _candles = null;

                    Security needSec = security;

                    if (needSec != null)
                    {
                        _candles = new List<Candle>();
                        string classCode = needSec.NameClass;

                        if (QuikLua == null)
                        {
                            return null;
                        }

                        List<QuikSharp.DataStructures.Candle> allCandlesForSec = QuikLua.Candles.GetLastCandles(classCode, needSec.Name.Split('+')[0], candleInterval, candleCount).Result;

                        if (allCandlesForSec == null) { return null; }

                        for (int i = 0; i < allCandlesForSec.Count; i++)
                        {
                            if (allCandlesForSec[i] != null)
                            {
                                Candle newCandle = new Candle();

                                newCandle.Close = allCandlesForSec[i].Close;
                                newCandle.High = allCandlesForSec[i].High;
                                newCandle.Low = allCandlesForSec[i].Low;
                                newCandle.Open = allCandlesForSec[i].Open;
                                newCandle.Volume = allCandlesForSec[i].Volume;

                                if (i == allCandlesForSec.Count - 1)
                                {
                                    newCandle.State = CandleState.None;
                                }
                                else
                                {
                                    newCandle.State = CandleState.Finished;
                                }

                                newCandle.TimeStart = new DateTime(allCandlesForSec[i].Datetime.year,
                                    allCandlesForSec[i].Datetime.month,
                                    allCandlesForSec[i].Datetime.day,
                                    allCandlesForSec[i].Datetime.hour,
                                    allCandlesForSec[i].Datetime.min,
                                    allCandlesForSec[i].Datetime.sec);

                                _candles.Add(newCandle);
                            }
                        }
                    }

                    return _candles;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 6 Security subscribe

        private List<Security> subscribedSecurities = new List<Security>();

        public void Subscribe(Security security)
        {
            try
            {
                if (subscribedSecurities.Find(sec => sec.Name == security.Name) != null)
                {
                    return;
                }

                if (QuikLua == null)
                {
                    return;
                }

                lock (_serverLocker)
                {
                    QuikLua.OrderBook.Subscribe(security.NameClass, security.Name.Split('+')[0]);
                    subscribedSecurities.Add(security);
                    QuikLua.Events.OnAllTrade -= EventsOnOnAllTrade;
                    QuikLua.Events.OnAllTrade += EventsOnOnAllTrade;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion

        #region 7 Parsing incoming data

        private ConcurrentQueue<AllTrade> _tradesQueue = new ConcurrentQueue<AllTrade>();

        private ConcurrentQueue<QuikSharp.DataStructures.Transaction.Order> _ordersQueue = new ConcurrentQueue<QuikSharp.DataStructures.Transaction.Order>();

        private ConcurrentQueue<QuikSharp.DataStructures.Transaction.Trade> _myTradesQueue = new ConcurrentQueue<QuikSharp.DataStructures.Transaction.Trade>();

        private ConcurrentQueue<OrderBook> _mdQueue = new ConcurrentQueue<OrderBook>();

        private void ThreadTradesParsingWorkPlace()
        {
            while (true)
            {
                _lastTimePingTrades = DateTime.Now;

                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(5000);
                        continue;
                    }

                    if (_tradesQueue.IsEmpty == false)
                    {
                        AllTrade trades = null;

                        if (_tradesQueue.TryDequeue(out trades))
                        {
                            UpdateTrades(trades);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }

                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void ThreadMarketDepthsParsingWorkPlace()
        {
            while (true)
            {
                _lastTimePingMarketDepth = DateTime.Now;

                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_mdQueue.IsEmpty == false)
                    {
                        OrderBook quotes = null;

                        if (_mdQueue.TryDequeue(out quotes))
                        {
                            UpdateMarketDepths(quotes);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void ThreadDataParsingWorkPlace()
        {
            while (true)
            {
                _lastTimePingMyOrders = DateTime.Now;

                try
                {

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_ordersQueue.IsEmpty == false)
                    {
                        QuikSharp.DataStructures.Transaction.Order orders = null;

                        if (_ordersQueue.TryDequeue(out orders))
                        {
                            UpdateMyOrders(orders);
                        }
                    }
                    else if (_myTradesQueue.IsEmpty == false)
                    {
                        QuikSharp.DataStructures.Transaction.Trade trades = null;

                        if (_myTradesQueue.TryDequeue(out trades))
                        {
                            UpdateMyTrades(trades);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private object _newTradesLoker = new object();

        private void UpdateTrades(AllTrade allTrade)
        {
            try
            {
                if (allTrade == null)
                {
                    return;
                }

                lock (_newTradesLoker)
                {
                    Trade trade = new Trade();
                    trade.SecurityNameCode = allTrade.SecCode + "+" + allTrade.ClassCode;
                    trade.Id = allTrade.TradeNum.ToString();
                    trade.Price = Convert.ToDecimal(allTrade.Price);
                    trade.Volume = Convert.ToInt32(allTrade.Qty);

                    int side = Convert.ToInt32(allTrade.Flags);

                    if (side == 1025 || side == 1)
                    {
                        trade.Side = Side.Sell;
                    }
                    else //if(side == 1026 || side == 2)
                    {
                        trade.Side = Side.Buy;
                    }
                    trade.Time = new DateTime(allTrade.Datetime.year, allTrade.Datetime.month, allTrade.Datetime.day,
                        allTrade.Datetime.hour, allTrade.Datetime.min, allTrade.Datetime.sec);
                    trade.MicroSeconds = allTrade.Datetime.mcs;

                    if (allTrade.OpenInterest != 0)
                    {
                        trade.OpenInterest = Convert.ToInt32(allTrade.OpenInterest);
                    }

                    if (NewTradesEvent != null)
                    {
                        NewTradesEvent(trade);
                    }

                    // write last tick time in server time / перегружаем последним временем тика время сервера
                    ServerTime = trade.Time;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private object changeFutPortf = new object();

        /// <summary>
        /// Функция вызывается терминалом QUIK при получении изменений ограничений по срочному рынку.
        /// </summary>
        private void EventsOnOnFuturesLimitChange(FuturesLimits futLimit)
        {
            try
            {
                lock (changeFutPortf)
                {
                    if (_portfolios == null || _portfolios.Count == 0)
                    {
                        return;
                    }

                    Portfolio needPortf = _portfolios.Find(p => p.Number == futLimit.TrdAccId);

                    if (needPortf != null)
                    {
                        needPortf.ValueBegin = Convert.ToDecimal(futLimit.CbpPrevLimit);
                        needPortf.ValueCurrent = Convert.ToDecimal(futLimit.CbpLimit);
                        needPortf.ValueBlocked =
                            Convert.ToDecimal(futLimit.CbpLUsedForOrders + futLimit.CbpLUsedForPositions);
                        needPortf.UnrealizedPnl = Convert.ToDecimal(futLimit.VarMargin);

                        if (PortfolioEvent != null)
                        {
                            PortfolioEvent(_portfolios);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private object changeFutPosLocker = new object();

        /// <summary>
        /// Функция вызывается терминалом QUIK при изменении позиции по срочному рынку.
        /// </summary>
        private void EventsOnOnFuturesClientHolding(FuturesClientHolding futPos)
        {
            try
            {
                lock (changeFutPosLocker)
                {
                    if (_portfolios != null && _portfolios.Count != 0)
                    {
                        Portfolio needPortfolio = _portfolios.Find(p => p.Number == futPos.trdAccId);

                        if (needPortfolio == null)
                        {
                            return;
                        }

                        Security sec = _securities.Find(sec => sec.Name.Split('+')[0] == futPos.secCode);

                        if (sec != null)
                        {
                            PositionOnBoard newPos = new PositionOnBoard();

                            newPos.PortfolioName = futPos.trdAccId;
                            newPos.SecurityNameCode = sec.Name;
                            newPos.ValueBegin = Convert.ToDecimal(futPos.startNet);
                            newPos.ValueCurrent = Convert.ToDecimal(futPos.totalNet);
                            newPos.ValueBlocked = 0;

                            needPortfolio.SetNewPosition(newPos);

                            if (PortfolioEvent != null)
                            {
                                PortfolioEvent(_portfolios);
                            }
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private object quoteLock = new object();

        private void UpdateMarketDepths(OrderBook orderBook)
        {
            try
            {
                lock (quoteLock)
                {
                    string curName = orderBook.sec_code + "+" + orderBook.class_code;

                    if (subscribedSecurities.Find(sec => sec.Name == curName) == null)
                    {
                        return;
                    }

                    if (orderBook.bid == null || orderBook.offer == null)
                    {
                        return;
                    }

                    MarketDepth myDepth = new MarketDepth();

                    myDepth.SecurityNameCode = curName;
                    myDepth.Time = TimeManager.GetExchangeTime("Russian Standard Time");

                    myDepth.Bids = new List<MarketDepthLevel>();
                    for (int i = 0; i < orderBook.bid.Length; i++)
                    {
                        myDepth.Bids.Add(new MarketDepthLevel()
                        {
                            Bid = orderBook.bid[i].quantity,
                            Price = orderBook.bid[i].price,
                            Ask = 0
                        });
                    }

                    myDepth.Bids.Reverse();

                    myDepth.Asks = new List<MarketDepthLevel>();
                    for (int i = 0; i < orderBook.offer.Length; i++)
                    {
                        myDepth.Asks.Add(new MarketDepthLevel()
                        {
                            Ask = orderBook.offer[i].quantity,
                            Price = orderBook.offer[i].price,
                            Bid = 0
                        });
                    }

                    if (MarketDepthEvent != null)
                    {
                        MarketDepthEvent(myDepth);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private object myTradeLocker = new object();

        private List<QuikSharp.DataStructures.Transaction.Trade> _myTradesFromQuik =
            new List<QuikSharp.DataStructures.Transaction.Trade>();

        private void UpdateMyTrades(QuikSharp.DataStructures.Transaction.Trade qTrade)
        {
            lock (myTradeLocker)
            {
                try
                {
                    if (_myTradesFromQuik.Find(t => t.TradeNum == qTrade.TradeNum) != null)
                    {
                        return;
                    }

                    _myTradesFromQuik.Add(qTrade);

                    MyTrade trade = new MyTrade();
                    trade.NumberTrade = qTrade.TradeNum.ToString();
                    trade.SecurityNameCode = qTrade.SecCode + "+" + qTrade.ClassCode;
                    trade.Price = Convert.ToDecimal(qTrade.Price);
                    trade.Volume = qTrade.Quantity;
                    trade.Time = new DateTime(qTrade.QuikDateTime.year, qTrade.QuikDateTime.month,
                        qTrade.QuikDateTime.day, qTrade.QuikDateTime.hour,
                        qTrade.QuikDateTime.min, qTrade.QuikDateTime.sec, qTrade.QuikDateTime.ms);
                    trade.NumberOrderParent = qTrade.OrderNum.ToString() + "+" + qTrade.TransID.ToString();

                    if (qTrade.Flags.ToString().Contains("IsSell"))
                    {
                        trade.Side = Side.Sell;
                    }
                    else
                    {
                        trade.Side = Side.Buy;
                    }

                    trade.MicroSeconds = qTrade.QuikDateTime.mcs;

                    if (MyTradeEvent != null)
                    {
                        MyTradeEvent(trade);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private object orderLocker = new object();

        private void UpdateMyOrders(QuikSharp.DataStructures.Transaction.Order qOrder)
        {
            lock (orderLocker)
            {
                try
                {
                    if (qOrder.TransID == 0)
                    {
                        return;
                    }

                    Order order = new Order();
                    order.NumberUser = Convert.ToInt32(qOrder.TransID);
                    order.TimeCallBack = new DateTime(qOrder.Datetime.year, qOrder.Datetime.month,
                        qOrder.Datetime.day, qOrder.Datetime.hour,
                        qOrder.Datetime.min, qOrder.Datetime.sec, qOrder.Datetime.ms);
                    order.SecurityNameCode = qOrder.SecCode + "+" + qOrder.ClassCode;
                    order.SecurityClassCode = order.SecurityNameCode.Split('+')[1];
                    order.Price = qOrder.Price;
                    order.Volume = qOrder.Quantity;
                    order.VolumeExecute = qOrder.Quantity - qOrder.Balance;
                    order.PortfolioNumber = qOrder.Account;
                    order.TypeOrder = qOrder.Flags.ToString().Contains("IsLimit")
                        ? OrderPriceType.Limit
                        : OrderPriceType.Market;
                    order.ServerType = ServerType.QuikLua;

                    if (qOrder.State == State.Active)
                    {
                        order.State = OrderStateType.Active;
                        order.TimeCallBack = new DateTime(qOrder.Datetime.year, qOrder.Datetime.month,
                            qOrder.Datetime.day,
                            qOrder.Datetime.hour, qOrder.Datetime.min, qOrder.Datetime.sec);
                    }
                    else if (qOrder.State == State.Completed)
                    {
                        order.State = OrderStateType.Done;
                        order.VolumeExecute = qOrder.Quantity;
                        order.TimeDone = order.TimeCallBack;
                    }
                    else if (qOrder.State == State.Canceled)
                    {
                        order.TimeCancel = new DateTime(qOrder.WithdrawDatetime.year, qOrder.WithdrawDatetime.month,
                            qOrder.WithdrawDatetime.day,
                            qOrder.WithdrawDatetime.hour, qOrder.WithdrawDatetime.min, qOrder.WithdrawDatetime.sec);
                        order.State = OrderStateType.Cancel;
                        order.VolumeExecute = 0;
                    }
                    else if (qOrder.Balance != 0)
                    {
                        order.State = OrderStateType.Partial;
                        order.VolumeExecute = qOrder.Quantity - qOrder.Balance;
                    }

                    if (_ordersAllReadyCanseled.Find(o => o.NumberUser == qOrder.TransID) != null)
                    {
                        order.State = OrderStateType.Cancel;
                        order.TimeCancel = order.TimeCallBack;
                    }

                    order.NumberMarket = qOrder.OrderNum.ToString() + "+" + order.NumberUser.ToString();

                    if (qOrder.Operation == Operation.Buy)
                    {
                        order.Side = Side.Buy;
                    }
                    else
                    {
                        order.Side = Side.Sell;
                    }

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void EventsOnOnAllTrade(AllTrade allTrade)
        {
            _tradesQueue.Enqueue(allTrade);
        }

        /// <summary>
        /// Функция вызывается терминалом QUIK при получении изменения стакана котировок.
        /// </summary>
        private void EventsOnOnQuote(OrderBook orderBook)
        {
            _mdQueue.Enqueue(orderBook);
        }

        /// <summary>
        /// Функция вызывается терминалом QUIK при получении новой заявки или при изменении параметров существующей заявки.
        /// </summary>
        private void EventsOnOnOrder(QuikSharp.DataStructures.Transaction.Order qOrder)
        {
            _ordersQueue.Enqueue(qOrder);
        }

        /// <summary>
        /// Функция вызывается терминалом QUIK при получении сделки.
        /// </summary>
        private void EventsOnOnTrade(QuikSharp.DataStructures.Transaction.Trade qTrade)
        {
            _myTradesQueue.Enqueue(qTrade);
        }

        /// <summary>
        /// Событие вызывается когда библиотека QuikSharp была отключена от Quik'а
        /// </summary>
        private void EventsOnOnDisconnectedFromQuik()
        {
            try
            {
                Dispose();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Функция вызывается перед закрытием терминала QUIK.
        /// </summary>
        private void Events_OnClose()
        {
            try
            {
                Dispose();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Событие вызывается когда библиотека QuikSharp успешно подключилась к Quik'у
        /// </summary>
        private void EventsOnOnConnectedToQuik(int port)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Функция вызывается терминалом QUIK при отключении от сервера QUIK.
        /// </summary>
        private void EventsOnOnDisconnected()
        {
            try
            {
                Dispose();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Функция вызывается терминалом QUIK при установлении связи с сервером QUIK.
        /// </summary>
        private void EventsOnOnConnected()
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Функция вызывается терминалом QUIK при получении ответа на транзакцию пользователя.
        /// </summary>
        private void Events_OnTransReply(TransactionReply transReply)
        {
            try
            {
                if (transReply.Status != 4 && transReply.Status != 6)
                {
                    if (_sentOrders != null && _sentOrders.Count > 0)
                    {
                        for (int i = _sentOrders.Count - 1; i >= 0; i--)
                        {
                            if (_sentOrders[i].NumberUser == transReply.TransID)
                            {
                                _sentOrders.RemoveAt(i);
                            }
                        }
                    }

                    return;
                }

                Order order = new Order();
                order.NumberUser = transReply.TransID;
                order.State = OrderStateType.Fail;
                order.SecurityNameCode = transReply.SecCode;
                order.SecurityClassCode = transReply.ClassCode;
                order.PortfolioNumber = transReply.Account;
                order.Price = transReply.Price.ToString().ToDecimal();
                order.Volume = transReply.Quantity.ToString().ToDecimal();
                order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(transReply.LuaTimeStamp);

                if (_sentOrders != null && _sentOrders.Count > 0)
                {
                    for (int i = _sentOrders.Count - 1; i >= 0; i--)
                    {
                        if (_sentOrders[i].NumberUser == transReply.TransID)
                        {
                            order.Side = _sentOrders[i].Side;
                            _sentOrders.RemoveAt(i);
                        }
                    }
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }

                SendLogMessage("Transaction  " + order.NumberUser + "  error: " + transReply.ResultMsg, LogMessageType.Error);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 Thread ping

        private DateTime _lastTimePingMyOrders = DateTime.MinValue;

        private DateTime _lastTimePingMarketDepth = DateTime.MinValue;

        private DateTime _lastTimePingTrades = DateTime.MinValue;

        private DateTime _lastTimePingPortfoios = DateTime.MinValue;

        private void ThreadPing()
        {
            while (true)
            {
                try
                {
                    if (_lastTimePingMyOrders != DateTime.MinValue && _lastTimePingMyOrders.AddMinutes(1) < DateTime.Now)
                    {
                        SendLogMessage($"Поток обработки собственных ордеров и трейдов не отвечает. Переподключение коннектора...", LogMessageType.System);
                        Dispose();
                    }
                    else if (_lastTimePingMarketDepth != DateTime.MinValue && _lastTimePingMarketDepth.AddMinutes(1) < DateTime.Now)
                    {
                        SendLogMessage($"Поток обработки стакана не отвечает. Переподключение коннектора...", LogMessageType.System);
                        Dispose();
                    }
                    else if (_lastTimePingPortfoios != DateTime.MinValue && _lastTimePingPortfoios.AddMinutes(1) < DateTime.Now)
                    {
                        SendLogMessage($"Поток обработки портфеля не отвечает. Переподключение коннектора...", LogMessageType.System);
                        Dispose();
                    }
                    else if (_lastTimePingTrades != DateTime.MinValue && _lastTimePingTrades.AddMinutes(1) < DateTime.Now)
                    {
                        SendLogMessage($"Поток обработки трейдов не отвечает. Переподключение коннектора...", LogMessageType.System);
                        Dispose();
                    }
                    else
                    {
                        Thread.Sleep(60000);
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        #endregion

        #region 9 Trade

        private string _clientCode;

        private List<Order> _sentOrders = new List<Order>();

        public void SendOrder(Order order)
        {
            try
            {
                _rateGateSendOrder.WaitToProceed();

                QuikSharp.DataStructures.Transaction.Order qOrder = new QuikSharp.DataStructures.Transaction.Order();

                qOrder.SecCode = order.SecurityNameCode.Split('+')[0];
                qOrder.Account = order.PortfolioNumber; // "SPBFUT02F5M"

                qOrder.ClientCode = _clientCode;
                qOrder.ClassCode = _securities.Find(sec => sec.Name == order.SecurityNameCode).NameClass;
                qOrder.Quantity = Convert.ToInt32(order.Volume);
                qOrder.Operation = order.Side == Side.Buy ? Operation.Buy : Operation.Sell;
                qOrder.Price = order.Price;

                if (((ServerParameterBool)ServerParameters[5]).Value == false)
                {
                    qOrder.Comment = order.NumberUser.ToString();
                }
                else if (((ServerParameterBool)ServerParameters[5]).Value == true)
                {
                    qOrder.Comment = order.PortfolioNumber + "//" + order.NumberUser.ToString();
                }

                lock (_serverLocker)
                {
                    long res = QuikLua.Orders.CreateOrder(qOrder).Result;

                    if (res > 0)
                    {
                        order.NumberUser = (int)res;

                        _sentOrders.Add(order);
                    }

                    if (res < 0)
                    {
                        order.State = OrderStateType.Fail;
                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                order.State = OrderStateType.Fail;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }
            }
        }

        private List<Order> _ordersAllReadyCanseled = new List<Order>();

        public bool CancelOrder(Order order)
        {
            try
            {
                _rateGateSendOrder.WaitToProceed();

                _ordersAllReadyCanseled.Add(order);
                QuikSharp.DataStructures.Transaction.Order qOrder = new QuikSharp.DataStructures.Transaction.Order();
                qOrder.SecCode = order.SecurityNameCode.Split('+')[0];
                qOrder.Account = order.PortfolioNumber;
                qOrder.ClassCode = _securities.Find(sec => sec.Name == order.SecurityNameCode).NameClass;

                if (order.NumberMarket == "")
                {
                    qOrder.OrderNum = 0;
                }
                else
                {
                    string numberMarket = order.NumberMarket.Split('+')[0];
                    qOrder.OrderNum = Convert.ToInt64(numberMarket);
                }

                lock (_serverLocker)
                {
                    long res = QuikLua.Orders.KillOrder(qOrder).Result;
                }

                return true;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public void GetAllActivOrders()
        {
            try
            {
                List<QuikSharp.DataStructures.Transaction.Order> foundOrders =
                QuikLua.Orders.GetOrders().Result;

                if (foundOrders != null && foundOrders.Count > 0)
                {
                    for (int i = 0; i < foundOrders.Count; i++)
                    {
                        EventsOnOnOrder(foundOrders[i]);
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            try
            {
                QuikSharp.DataStructures.Transaction.Order foundOrder;

                if (order.NumberMarket != null && order.NumberMarket != "")
                {
                    string numberMarket = order.NumberMarket.Split('+')[0];
                    foundOrder = QuikLua.Orders.GetOrder(order.SecurityNameCode.Split('+')[1], Convert.ToInt64(numberMarket)).Result;
                }
                else
                {
                    foundOrder = QuikLua.Orders.GetOrder_by_transID(order.SecurityNameCode.Split('+')[1], order.SecurityNameCode.Split('+')[0], order.NumberUser).Result;
                }

                bool needTrade = false;

                if (foundOrder != null)
                {
                    if (foundOrder.TransID == 0)
                    {
                        return OrderStateType.None;
                    }

                    order.NumberUser = Convert.ToInt32(foundOrder.TransID);
                    order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(foundOrder.LuaTimeStamp);
                    order.SecurityNameCode = foundOrder.SecCode + "+" + foundOrder.ClassCode;
                    order.SecurityClassCode = order.SecurityNameCode.Split('+')[1];
                    order.Price = foundOrder.Price;
                    order.Volume = foundOrder.Quantity;
                    order.VolumeExecute = foundOrder.Quantity - foundOrder.Balance;
                    order.PortfolioNumber = foundOrder.Account;
                    order.TypeOrder = foundOrder.Flags.ToString().Contains("IsLimit")
                        ? OrderPriceType.Limit
                        : OrderPriceType.Market;
                    order.ServerType = ServerType.QuikLua;

                    if (foundOrder.State == State.Active)
                    {
                        order.State = OrderStateType.Active;
                        order.TimeCallBack = new DateTime(foundOrder.Datetime.year, foundOrder.Datetime.month,
                            foundOrder.Datetime.day,
                            foundOrder.Datetime.hour, foundOrder.Datetime.min, foundOrder.Datetime.sec);
                    }
                    else if (foundOrder.State == State.Completed)
                    {
                        order.State = OrderStateType.Done;
                        order.VolumeExecute = foundOrder.Quantity;
                        order.TimeDone = order.TimeCallBack;

                        needTrade = true;
                    }
                    else if (foundOrder.State == State.Canceled)
                    {
                        order.TimeCancel = new DateTime(foundOrder.WithdrawDatetime.year, foundOrder.WithdrawDatetime.month,
                            foundOrder.WithdrawDatetime.day,
                            foundOrder.WithdrawDatetime.hour, foundOrder.WithdrawDatetime.min, foundOrder.WithdrawDatetime.sec);
                        order.State = OrderStateType.Cancel;
                        order.VolumeExecute = 0;
                    }
                    else if (foundOrder.Balance != 0)
                    {
                        order.State = OrderStateType.Partial;
                        order.VolumeExecute = foundOrder.Quantity - foundOrder.Balance;

                        needTrade = true;
                    }

                    if (_ordersAllReadyCanseled.Find(o => o.NumberUser == foundOrder.TransID) != null)
                    {
                        order.State = OrderStateType.Cancel;
                        order.TimeCancel = order.TimeCallBack;
                    }

                    if (foundOrder.Operation == Operation.Buy)
                    {
                        order.Side = Side.Buy;
                    }
                    else
                    {
                        order.Side = Side.Sell;
                    }

                    order.NumberMarket = foundOrder.OrderNum.ToString() + "+" + order.NumberUser.ToString();

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }

                    if (needTrade)
                    {
                        string numberMarket = order.NumberMarket.Split('+')[0];
                        var quikTrades = QuikLua.Trading.GetTrades_by_OdrerNumber(Convert.ToInt64(numberMarket)).Result;

                        if (quikTrades != null)
                        {
                            for (int i = 0; i < quikTrades.Count; i++)
                            {
                                var quikTrade = quikTrades[i];
                                MyTrade trade = new MyTrade();
                                trade.NumberTrade = quikTrade.TradeNum.ToString();
                                trade.SecurityNameCode = quikTrade.SecCode + "+" + quikTrade.ClassCode;
                                trade.Price = Convert.ToDecimal(quikTrade.Price);
                                trade.Volume = quikTrade.Quantity;
                                trade.Time = new DateTime(quikTrade.QuikDateTime.year, quikTrade.QuikDateTime.month,
                                    quikTrade.QuikDateTime.day, quikTrade.QuikDateTime.hour,
                                    quikTrade.QuikDateTime.min, quikTrade.QuikDateTime.sec, quikTrade.QuikDateTime.ms);
                                trade.NumberOrderParent = quikTrade.OrderNum.ToString() + "+" + quikTrade.TransID.ToString();

                                if (order.NumberMarket != trade.NumberOrderParent) continue;

                                if (quikTrade.Flags.ToString().Contains("IsSell"))
                                {
                                    trade.Side = Side.Sell;
                                }
                                else
                                {
                                    trade.Side = Side.Buy;
                                }

                                trade.MicroSeconds = quikTrade.QuikDateTime.mcs;

                                if (MyTradeEvent != null)
                                {
                                    MyTradeEvent(trade);
                                }
                            }
                        }
                    }
                }
                else
                {
                    SendLogMessage($"The order was not found. NumberUser - {order.NumberUser}", LogMessageType.System);

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
            return OrderStateType.None;
        }

        public void CancelAllOrders() { }

        public void ChangeOrderPrice(Order order, decimal newPrice) { }

        public void CancelAllOrdersToSecurity(Security security) { }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        #endregion

        #region 10 Helpers

        /// <summary>
        /// Проверяем какие классы выбраны то и грузим
        /// </summary>
        /// <param name="classesSec"></param>
        /// <returns></returns>
        private bool CheckFilter(string classesSec)
        {
            {
                if (classesSec.EndsWith("TQBR") || classesSec.EndsWith("TQOB") || classesSec.EndsWith("QJSIM"))
                {
                    if (_useStock.Value)
                    {
                        return true;
                    }
                    return false;
                }

                if (classesSec.Contains("TQCB"))
                {
                    if (_useBonds.Value)
                    {
                        return true;
                    }
                    return false;
                }

                if (classesSec.Contains("FUT"))
                {
                    if (_useFutures.Value)
                    {
                        return true;
                    }
                    return false;
                }

                if (classesSec.Contains("OPT"))
                {
                    if (_useOptions.Value)
                    {
                        return true;
                    }
                    return false;
                }

                if (classesSec.Contains("CETS") || classesSec == "CURRENCY")
                {
                    if (_useCurrency.Value)
                    {
                        return true;
                    }
                    return false;
                }

                if (_useOther.Value)
                {
                    return true;
                }

                return false;
            }
        }

        private CandleInterval SelectTimeFrame(TimeFrame timeFrame)
        {
            CandleInterval candleInterval = CandleInterval.M5;

            if (timeFrame == TimeFrame.Min1)
            {
                candleInterval = CandleInterval.M1;
            }
            else if (timeFrame == TimeFrame.Min2)
            {
                candleInterval = CandleInterval.M2;
            }
            else if (timeFrame == TimeFrame.Min5)
            {
                candleInterval = CandleInterval.M5;
            }
            else if (timeFrame == TimeFrame.Min10)
            {
                candleInterval = CandleInterval.M10;
            }
            else if (timeFrame == TimeFrame.Min15)
            {
                candleInterval = CandleInterval.M15;
            }
            else if (timeFrame == TimeFrame.Min20)
            {
                candleInterval = CandleInterval.M20;
            }
            else if (timeFrame == TimeFrame.Min30)
            {
                candleInterval = CandleInterval.M30;
            }
            else if (timeFrame == TimeFrame.Hour1)
            {
                candleInterval = CandleInterval.H1;
            }
            else if (timeFrame == TimeFrame.Hour2)
            {
                candleInterval = CandleInterval.H2;
            }
            else if (timeFrame == TimeFrame.Hour4)
            {
                candleInterval = CandleInterval.H4;
            }
            else if (timeFrame == TimeFrame.Day)
            {
                candleInterval = CandleInterval.D1;
            }

            return candleInterval;
        }

        private bool IsLoadSecuritiesFromCache()
        {
            if (!File.Exists(SecuritiesCachePath))
            {
                return false;
            }

            DateTime lastWriteTime = File.GetLastWriteTime(SecuritiesCachePath);

            return DateTime.Now < lastWriteTime.AddHours(1);
        }

        #endregion

        #region 11 Log

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        #endregion

    }
}
