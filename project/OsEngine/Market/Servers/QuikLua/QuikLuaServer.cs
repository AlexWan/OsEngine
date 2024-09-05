using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.Utils;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using QuikSharp;
using QuikSharp.DataStructures;
using QuikSharp.DataStructures.Transaction;
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

            //AVP новые параметры и обработка изменений
            CreateParameterBoolean(OsLocalization.Market.UseStock, true);
            CreateParameterBoolean(OsLocalization.Market.UseFutures, true);
            CreateParameterBoolean(OsLocalization.Market.UseCurrency, true);
            CreateParameterBoolean(OsLocalization.Market.UseOptions, false);
            CreateParameterBoolean(OsLocalization.Market.UseOther, false);
            CreateParameterBoolean(OsLocalization.Market.Label109, false);
            CreateParameterString("Client code", null);

            ServerParameters[0].Comment = OsLocalization.Market.Label107;
            ServerParameters[1].Comment = OsLocalization.Market.Label107;
            ServerParameters[2].Comment = OsLocalization.Market.Label107;
            ServerParameters[3].Comment = OsLocalization.Market.Label96;
            ServerParameters[4].Comment = OsLocalization.Market.Label97;
            ServerParameters[5].Comment = OsLocalization.Market.Label110;
            ServerParameters[6].Comment = OsLocalization.Market.Label121;

            ((ServerParameterBool)ServerParameters[0]).ValueChange += QuikLuaServer_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[1]).ValueChange += QuikLuaServer_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[2]).ValueChange += QuikLuaServer_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[3]).ValueChange += QuikLuaServer_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[4]).ValueChange += QuikLuaServer_ParametrValueChange;

            ((QuikLuaServerRealization)ServerRealization).ClientCodeFromSettings
                = (ServerParameterString)ServerParameters[6];
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

            Thread updateSpotPos = new Thread(UpdateSpotPosition);
            updateSpotPos.CurrentCulture = new CultureInfo("ru-RU");
            updateSpotPos.Start();

            Thread getPos = new Thread(GetPortfoliosArea);
            getPos.CurrentCulture = new CultureInfo("ru-RU");
            getPos.Start();

        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void Connect()
        {
            try
            {
                if (QuikLua == null)
                {
                    _useStock = (ServerParameterBool)ServerParameters[0];   // AVP линкуем параметры в локальные переменные
                    _useFutures = (ServerParameterBool)ServerParameters[1];
                    _useCurrency = (ServerParameterBool)ServerParameters[2];
                    _useOptions = (ServerParameterBool)ServerParameters[3];
                    _useOther = (ServerParameterBool)ServerParameters[4];

                    QuikLua = new QuikSharp.Quik(QuikSharp.Quik.DefaultPort, new InMemoryStorage());
                    //QuikLua.DefaultSendTimeout = new TimeSpan(0, 0, 5);
                    QuikLua.Events.OnConnected += EventsOnOnConnected;
                    QuikLua.Events.OnDisconnected += EventsOnOnDisconnected;
                    QuikLua.Events.OnConnectedToQuik += EventsOnOnConnectedToQuik;
                    QuikLua.Events.OnDisconnectedFromQuik += EventsOnOnDisconnectedFromQuik;
                    QuikLua.Events.OnTrade += EventsOnOnTrade;
                    QuikLua.Events.OnOrder += EventsOnOnOrder;
                    QuikLua.Events.OnQuote += EventsOnOnQuote;
                    QuikLua.Events.OnFuturesClientHolding += EventsOnOnFuturesClientHolding;
                    QuikLua.Events.OnFuturesLimitChange += EventsOnOnFuturesLimitChange;
                    QuikLua.Events.OnTransReply += Events_OnTransReply;

                    QuikLua.Service.QuikService.Start();

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

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void Dispose()
        {
            try
            {
                if (QuikLua != null && QuikLua.Service.IsConnected().Result)
                {
                    QuikLua.Service.QuikService.Stop();
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

                subscribedBook = new List<string>();
                QuikLua = null;

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

        #endregion

        #region 2 Properties

        public ServerParameterString ClientCodeFromSettings;

        public QuikSharp.Quik QuikLua;

        private object _serverLocker = new object();

        private static readonly Char Separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

        private static readonly string SecuritiesCachePath = @"Engine\QuikLuaSecuritiesCache.txt";

        private ServerParameterBool _useStock; //AVP 6 строк для контроля изменения набора классов инструментов

        private ServerParameterBool _useFutures;

        private ServerParameterBool _useOptions;

        private ServerParameterBool _useCurrency;

        private ServerParameterBool _useOther;

        public bool _changeClassUse = false;

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(200));

        private RateGate _gateToGetCandles = new RateGate(1, TimeSpan.FromMilliseconds(500));

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

        #endregion

        #region 3 Securities

        private List<Security> _securities = new List<Security>();

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
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

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
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

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
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
                foreach (SecurityInfo oneSec in allSec)
                {
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

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
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
                    string exp = oneSec.MatDate;
                    newSec.Expiration = new DateTime(Convert.ToInt32(exp.Substring(0, 4))
                        , Convert.ToInt32(exp.Substring(4, 2))
                        , Convert.ToInt32(exp.Substring(6, 2)));

                    newSec.Go = QuikLua.Trading
                        .GetParamEx(classCode, secCode, "SELLDEPO")
                        .Result.ParamValue.Replace('.', Separator).ToDecimal();
                }
                else if (oneSec.ClassCode == "SPBOPT")
                {
                    newSec.SecurityType = SecurityType.Option;

                    newSec.OptionType = QuikLua.Trading.GetParamEx(classCode, secCode, "OPTIONTYPE")
                        .Result.ParamImage == "Put"
                        ? OptionType.Put
                        : OptionType.Call;

                    string exp = oneSec.MatDate;
                    newSec.Expiration = new DateTime(Convert.ToInt32(exp.Substring(0, 4))
                        , Convert.ToInt32(exp.Substring(4, 2))
                        , Convert.ToInt32(exp.Substring(6, 2)));

                    newSec.Go = QuikLua.Trading
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
                newSec.NameFull = oneSec.Name;
                newSec.NameId = oneSec.Name;
                newSec.State = SecurityStateType.Activ;
                newSec.Exchange = "MOEX";

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

        public void GetPortfolios()
        {

        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void GetPortfoliosArea()
        {
            try
            {
                while (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    Thread.Sleep(5000);
                }

                Char separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                List<TradesAccounts> accaunts = QuikLua.Class.GetTradeAccounts().Result;
                string clientCode = QuikLua.Class.GetClientCode().Result;

                while (true)
                {
                    Thread.Sleep(5000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (QuikLua == null)
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

                        PortfolioInfo qPortfolio =
                            QuikLua.Trading.GetPortfolioInfo(accaunts[i].Firmid, accaunts[i].TrdaccId).Result;   //AVP сделал accaunts[i].TrdaccId, для финама , БКС, когда в квике несколько клиенткодов,  было clientCode 

                        if (qPortfolio == null)    // AVP для тех брокеров у которых через accaunts[i].TrdaccId портфель не находит, и клиент-код всего один.
                        {
                            qPortfolio =
                            QuikLua.Trading.GetPortfolioInfo(accaunts[i].Firmid, clientCode).Result;
                        }

                        if (qPortfolio.Assets == null ||
                            qPortfolio.Assets.ToDecimal() == 0)
                        {
                            PortfolioInfoEx qPortfolioEx =
                                QuikLua.Trading.GetPortfolioInfoEx(accaunts[i].Firmid, myPortfolio.Number, 0).Result;

                            if (qPortfolioEx != null &&
                                qPortfolioEx.StartLimitOpenPos != null)
                            {
                                qPortfolio.InAssets = qPortfolioEx.StartLimitOpenPos;
                            }
                            if (qPortfolioEx != null &&
                                qPortfolioEx.TotalLimitOpenPos != null)
                            {
                                qPortfolio.Assets = qPortfolioEx.TotalLimitOpenPos;
                            }
                        }

                        if (qPortfolio != null && qPortfolio.InAssets != null)
                        {
                            string begin = qPortfolio.InAssets.Replace('.', separator);
                            myPortfolio.ValueBegin = begin.Remove(begin.Length - 4).ToDecimal();
                        }

                        if (qPortfolio != null && qPortfolio.Assets != null)
                        {
                            string current = qPortfolio.Assets.Replace('.', separator);
                            myPortfolio.ValueCurrent = current.Remove(current.Length - 4).ToDecimal();
                        }

                        if (qPortfolio != null && qPortfolio.TotalLockedMoney != null)
                        {
                            string blocked = qPortfolio.TotalLockedMoney.Replace('.', separator);
                            myPortfolio.ValueBlocked = blocked.Remove(blocked.Length - 4).ToDecimal();
                        }

                        if (qPortfolio != null && qPortfolio.ProfitLoss != null)
                        {
                            string profit = qPortfolio.ProfitLoss.Replace('.', separator);
                            myPortfolio.Profit = profit.Remove(profit.Length - 4).ToDecimal();
                        }
                    }

                    if (PortfolioEvent != null)
                    {
                        PortfolioEvent(_portfolios);
                    }
                }
            }

            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void UpdateSpotPosition()
        {
            while (true)
            {
                Thread.Sleep(5000);

                try
                {
                    if (QuikLua != null)
                    {
                        bool quikStateIsActiv = QuikLua.Service.IsConnected().Result;
                    }

                    if (QuikLua == null)
                    {
                        continue;
                    }


                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    List<DepoLimitEx> spotPos = QuikLua.Trading.GetDepoLimits().Result;
                    Portfolio needPortf;
                    foreach (DepoLimitEx pos in spotPos)
                    {
                        Security sec = _securities.Find(sec => sec.Name.Split('+')[0] == pos.SecCode);

                        if (pos.LimitKind == LimitKind.T0 && sec != null)
                        {
                            needPortf = _portfolios.Find(p => p.Number == pos.TrdAccId);

                            PositionOnBoard position = new PositionOnBoard();

                            if (needPortf != null)
                            {
                                position.PortfolioName = pos.TrdAccId;
                                position.ValueBegin = pos.OpenBalance;
                                position.ValueCurrent = pos.CurrentBalance;
                                position.ValueBlocked = pos.LockedSell;
                                position.SecurityNameCode = sec.Name;

                                needPortf.SetNewPosition(position);
                            }
                        }
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
        }

        #endregion

        #region 5 Data

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            return null;
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            try
            {
                List<Trade> AllHistoricalTrades = new List<Trade>();

                //скачаем новые данные из квика. (доступна только текущая сессия. с 19.00 вчерашнего по 18.45 текущего дня)	
                List<Trade> newTrades = GetQuikLuaTickHistory(security);

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
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
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

        #region 6 Security subscrible

        private List<string> subscribedBook = new List<string>();

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void Subscrible(Security security)
        {
            try
            {
                if (subscribedBook.Find(s => s == security.Name) != null)
                {
                    return;
                }

                lock (_serverLocker)
                {
                    QuikLua.OrderBook.Subscribe(security.NameClass, security.Name.Split('+')[0]);
                    subscribedBook.Add(security.Name);
                    QuikLua.Events.OnAllTrade -= EventsOnOnAllTrade;
                    QuikLua.Events.OnAllTrade += EventsOnOnAllTrade;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 7 Parsing incoming data

        private object _newTradesLoker = new object();

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void EventsOnOnAllTrade(AllTrade allTrade)
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

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
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
                        needPortf.Profit = Convert.ToDecimal(futLimit.VarMargin);

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

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
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

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void EventsOnOnQuote(OrderBook orderBook)
        {
            try
            {
                lock (quoteLock)
                {
                    string curName = orderBook.sec_code + "+" + orderBook.class_code;

                    if (subscribedBook.Find(name => name == curName) == null)
                    {
                        return;
                    }

                    if (orderBook.bid == null || orderBook.offer == null)
                    {
                        return;
                    }

                    MarketDepth myDepth = new MarketDepth();

                    myDepth.SecurityNameCode = curName;
                    myDepth.Time = DateTime.Now;

                    myDepth.Bids = new List<MarketDepthLevel>();
                    for (int i = 0; i < orderBook.bid.Length; i++)
                    {
                        myDepth.Bids.Add(new MarketDepthLevel()
                        {
                            Bid = Convert.ToDecimal(orderBook.bid[i].quantity),
                            Price = Convert.ToDecimal(orderBook.bid[i].price),
                            Ask = 0
                        });
                    }

                    myDepth.Bids.Reverse();

                    myDepth.Asks = new List<MarketDepthLevel>();
                    for (int i = 0; i < orderBook.offer.Length; i++)
                    {
                        myDepth.Asks.Add(new MarketDepthLevel()
                        {
                            Ask = Convert.ToDecimal(orderBook.offer[i].quantity),
                            Price = Convert.ToDecimal(orderBook.offer[i].price),
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

        private object orderLocker = new object();

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void EventsOnOnOrder(QuikSharp.DataStructures.Transaction.Order qOrder)
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
                    order.NumberUser = Convert.ToInt32(qOrder.TransID); //Convert.qOrder.OrderNum;TransID
                    order.NumberMarket = qOrder.OrderNum.ToString(new CultureInfo("ru-RU"));
                    order.TimeCallBack = TimeManager.GetDateTimeFromTimeStamp(qOrder.LuaTimeStamp);
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
                        order.State = OrderStateType.Activ;
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
                        order.State = OrderStateType.Patrial;
                        order.VolumeExecute = qOrder.Quantity - qOrder.Balance;
                    }

                    if (_ordersAllReadyCanseled.Find(o => o.NumberUser == qOrder.TransID) != null)
                    {
                        order.State = OrderStateType.Cancel;
                        order.TimeCancel = order.TimeCallBack;
                    }

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

        private object myTradeLocker = new object();

        private List<QuikSharp.DataStructures.Transaction.Trade> _myTradesFromQuik =
            new List<QuikSharp.DataStructures.Transaction.Trade>();

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void EventsOnOnTrade(QuikSharp.DataStructures.Transaction.Trade qTrade)
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
                    trade.NumberOrderParent = qTrade.OrderNum.ToString();
                    trade.Price = Convert.ToDecimal(qTrade.Price);
                    trade.Volume = qTrade.Quantity;
                    trade.Time = new DateTime(qTrade.QuikDateTime.year, qTrade.QuikDateTime.month,
                        qTrade.QuikDateTime.day, qTrade.QuikDateTime.hour,
                        qTrade.QuikDateTime.min, qTrade.QuikDateTime.sec, qTrade.QuikDateTime.ms);

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

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
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

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
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

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
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

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void Events_OnTransReply(TransactionReply transReply)
        {
            try
            {
                if (transReply.Status != 4 && transReply.Status != 6)
                {
                    if (_sentOrders != null && _sentOrders.Count > 0)
                    {
                        for (int i = 0; i < _sentOrders.Count; i++)
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
                    for (int i = 0; i < _sentOrders.Count; i++)
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

        #region 8 Trade

        private string _clientCode;

        private List<Order> _sentOrders = new List<Order>();

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void SendOrder(Order order)
        {
            try
            {
                _rateGateSendOrder.WaitToProceed();

                QuikSharp.DataStructures.Transaction.Order qOrder = new QuikSharp.DataStructures.Transaction.Order();

                qOrder.SecCode = order.SecurityNameCode.Split('+')[0];
                qOrder.Account = order.PortfolioNumber; // "SPBFUT02F5M"

                List<TradesAccounts> accaunts = QuikLua.Class.GetTradeAccounts().Result;

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

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void CancelOrder(Order order)
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
                    qOrder.OrderNum = Convert.ToInt64(order.NumberMarket);
                }

                lock (_serverLocker)
                {
                    long res = QuikLua.Orders.KillOrder(qOrder).Result;
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void GetAllActivOrders()
        {
            List<QuikSharp.DataStructures.Transaction.Order> foundOrder =
                QuikLua.Orders.GetOrders().Result;

            if (foundOrder != null && foundOrder.Count > 0)
            {
                for (int i = 0; i < foundOrder.Count; i++)
                {
                    EventsOnOnOrder(foundOrder[i]);
                }
            }
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void GetOrderStatus(Order order)
        {
            try
            {
                QuikSharp.DataStructures.Transaction.Order foundOrder =
                QuikLua.Orders.GetOrder_by_transID(order.SecurityClassCode, order.SecurityNameCode.Split('+')[0],
                order.NumberUser).Result;

                if (foundOrder != null)
                {
                    EventsOnOnOrder(foundOrder);
                }
                else
                {
                    order.State = OrderStateType.Fail;

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
        }

        public void CancelAllOrders() { }

        public void ChangeOrderPrice(Order order, decimal newPrice) { }

        public void CancelAllOrdersToSecurity(Security security) { }

        #endregion

        #region 9 Helpers

        /// <summary>
        /// Проверяем какие классы выбраны то и грузим
        /// </summary>
        /// <param name="classesSec"></param>
        /// <returns></returns>
        private bool CheckFilter(string classesSec) //AVP
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

        #region 10 Log

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