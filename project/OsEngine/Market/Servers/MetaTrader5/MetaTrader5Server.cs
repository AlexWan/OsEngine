using MtApi5;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Entity.Utils;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using QuikSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;

namespace OsEngine.Market.Servers.MetaTrader5
{
    public class MetaTrader5Server : AServer
    {
        public MetaTrader5Server()
        {
            MetaTrader5ServerRealization realization = new MetaTrader5ServerRealization();
            ServerRealization = realization;

            CreateParameterString("Host", "localhost"); // 0
            CreateParameterInt("Port", 8228); // 1
            CreateParameterBoolean("Use netting", true); // 2
            CreateParameterBoolean("Currency", true); // 3
            CreateParameterBoolean("Commodities", true); // 4
            CreateParameterBoolean("Funds", false); // 5
            CreateParameterBoolean("Other", false); // 6
            CreateParameterBoolean("Only market watch", true); // 7
            CreateParameterBoolean("Market depth of ticks", true); // 8
            CreateParameterEnum("Deposit currency", "RUB", new List<string> { "RUB", "USD", "EUR" }); // 9
            CreateParameterBoolean("Count the profit in points", true); // 10
            CreateParameterInt("Candle shift (hours)", 0); // 11

            ((ServerParameterBool)ServerParameters[3]).ValueChange += MetaTrader5Server_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[4]).ValueChange += MetaTrader5Server_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[5]).ValueChange += MetaTrader5Server_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[6]).ValueChange += MetaTrader5Server_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[7]).ValueChange += MetaTrader5Server_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[8]).ValueChange += MetaTrader5Server_ParametrValueChange;
            ((ServerParameterEnum)ServerParameters[9]).ValueChange += MetaTrader5Server_ParametrValueChange;
            ((ServerParameterBool)ServerParameters[10]).ValueChange += MetaTrader5Server_ParametrValueChange;
            ((ServerParameterInt)ServerParameters[11]).ValueChange += MetaTrader5Server_ParametrValueChange;

            ((ServerParameterString)ServerParameters[0]).Comment = OsLocalization.Market.Label266;
            ((ServerParameterInt)ServerParameters[1]).Comment = OsLocalization.Market.Label267;
            ((ServerParameterBool)ServerParameters[2]).Comment = OsLocalization.Market.Label257;
            ((ServerParameterBool)ServerParameters[3]).Comment = OsLocalization.Market.Label262;
            ((ServerParameterBool)ServerParameters[4]).Comment = OsLocalization.Market.Label263;
            ((ServerParameterBool)ServerParameters[5]).Comment = OsLocalization.Market.Label264;
            ((ServerParameterBool)ServerParameters[6]).Comment = OsLocalization.Market.Label265;
            ((ServerParameterBool)ServerParameters[7]).Comment = OsLocalization.Market.Label258;
            ((ServerParameterBool)ServerParameters[8]).Comment = OsLocalization.Market.Label259;
            ((ServerParameterEnum)ServerParameters[9]).Comment = OsLocalization.Market.Label260;
            ((ServerParameterBool)ServerParameters[10]).Comment = OsLocalization.Market.Label261;
            ((ServerParameterInt)ServerParameters[11]).Comment = OsLocalization.Market.Label282;
        }

        private void MetaTrader5Server_ParametrValueChange()
        {
            ((MetaTrader5ServerRealization)ServerRealization)._useCurrency = ((ServerParameterBool)ServerParameters[3]).Value;
            ((MetaTrader5ServerRealization)ServerRealization)._useMetals = ((ServerParameterBool)ServerParameters[4]).Value;
            ((MetaTrader5ServerRealization)ServerRealization)._useFunds = ((ServerParameterBool)ServerParameters[5]).Value;
            ((MetaTrader5ServerRealization)ServerRealization)._useAnotherOne = ((ServerParameterBool)ServerParameters[6]).Value;
            ((MetaTrader5ServerRealization)ServerRealization)._onlyMarketWatch = ((ServerParameterBool)ServerParameters[7]).Value;
            ((MetaTrader5ServerRealization)ServerRealization)._marketDepthOfTicks = ((ServerParameterBool)ServerParameters[8]).Value;
            ((MetaTrader5ServerRealization)ServerRealization)._depositCurrency = ((ServerParameterEnum)ServerParameters[9]).Value;
            ((MetaTrader5ServerRealization)ServerRealization)._profitInPoints = ((ServerParameterBool)ServerParameters[10]).Value;
            ((MetaTrader5ServerRealization)ServerRealization)._shiftTime = ((ServerParameterInt)ServerParameters[11]).Value;
            ((MetaTrader5ServerRealization)ServerRealization)._changeClassUse = true;
            Securities?.Clear();
        }
    }

    public class MetaTrader5ServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public MetaTrader5ServerRealization()
        {
            Thread thread1 = new Thread(ThreadUpdateMarketDepth);
            thread1.Name = "MT5ThreadUpdateMarketDepth";
            thread1.CurrentCulture = new CultureInfo("ru-Ru");
            thread1.Start();

            Thread thread2 = new Thread(ThreadUpdateTiks);
            thread2.Name = "MT5ThreadUpdateTiks";
            thread2.CurrentCulture = new CultureInfo("ru-Ru");
            thread2.Start();

            Thread thread3 = new Thread(ThreadUpdateMyTransaction);
            thread3.Name = "MT5ThreadUpdateMyTransaction";
            thread3.CurrentCulture = new CultureInfo("ru-Ru");
            thread3.Start();

            Thread thread4 = new Thread(ThreadRecalculatingCostPriceStep);
            thread4.Name = "ThreadRecalculatingCostPriceStep";
            thread4.CurrentCulture = new CultureInfo("ru-Ru");
            thread4.Start();

            Thread thread5 = new Thread(ThreadPortfolioUpdate);
            thread5.Name = "ThreadPortfolioUpdate";
            thread5.CurrentCulture = new CultureInfo("ru-Ru");
            thread5.Start();
        }

        public void Connect(WebProxy proxy)
        {
            try
            {
                _useNetting = ((ServerParameterBool)ServerParameters[2]).Value;
                _useCurrency = ((ServerParameterBool)ServerParameters[3]).Value;
                _useMetals = ((ServerParameterBool)ServerParameters[4]).Value;
                _useFunds = ((ServerParameterBool)ServerParameters[5]).Value;
                _useAnotherOne = ((ServerParameterBool)ServerParameters[6]).Value;
                _onlyMarketWatch = ((ServerParameterBool)ServerParameters[7]).Value;
                _marketDepthOfTicks = ((ServerParameterBool)ServerParameters[8]).Value;
                _marketDepthOfTicks = ((ServerParameterBool)ServerParameters[8]).Value;
                _depositCurrency = ((ServerParameterEnum)ServerParameters[9]).Value;
                _profitInPoints = ((ServerParameterBool)ServerParameters[10]).Value;
                _profitInPoints = ((ServerParameterBool)ServerParameters[10]).Value;
                _shiftTime = ((ServerParameterInt)ServerParameters[11]).Value;

                _mtApiClient.ConnectionStateChanged += ConnectionStateChanged;
                _mtApiClient.QuoteUpdate += QuoteUpdate; // OnTick - новые трейды
                _mtApiClient.OnBookEvent += OnBookEvent; // OnBookEvent - обновление стакана
                _mtApiClient.OnTradeTransaction += OnTradeTransaction; // транзакции пользователя

                _mtApiClient.BeginConnect("localhost", 8228);

                LoadPositionsFromFile();
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error Connect: {ex.ToString()}", LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_mtApiClient.ConnectionState == Mt5ConnectionState.Connected || _mtApiClient.ConnectionState == Mt5ConnectionState.Connecting)
                {
                    _mtApiClient.QuoteUpdate -= QuoteUpdate;
                    _mtApiClient.OnBookEvent -= OnBookEvent;
                    _mtApiClient.OnTradeTransaction -= OnTradeTransaction;

                    _mtApiClient.BeginDisconnect();
                }

                _securities.Clear();
                _myTransactionQueue.Clear();
                _myPortfolios.Clear();
                _ticksQueue.Clear();
                _dictionaryLastTimeTick.Clear();
                _dictionaryOpenPositions.Clear();
                _mdQueue.Clear();

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("Disconnected.", LogMessageType.System);

                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error Dispose: {ex.ToString()}", LogMessageType.Error);
            }
        }

        public ServerType ServerType => ServerType.MetaTrader5;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public DateTime ServerTime { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        #endregion

        #region Fields

        static readonly MtApi5Client _mtApiClient = new MtApi5Client();

        private List<Security> _securities = new List<Security>();

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        private ConcurrentQueue<Mt5BookEventArgs> _mdQueue = new ConcurrentQueue<Mt5BookEventArgs>();

        private ConcurrentQueue<Mt5QuoteEventArgs> _ticksQueue = new ConcurrentQueue<Mt5QuoteEventArgs>();

        private ConcurrentQueue<Mt5TradeTransactionEventArgs> _myTransactionQueue = new ConcurrentQueue<Mt5TradeTransactionEventArgs>();

        private Dictionary<int, ulong> _dictionaryOpenPositions = new Dictionary<int, ulong>();

        private Dictionary<string, DateTime> _dictionaryLastTimeTick = new Dictionary<string, DateTime>();

        private static readonly string SecuritiesCachePath = @"Engine\MetaTrader5SecuritiesCache.txt";

        private static readonly string PositionsCachePath = @"Engine\MetaTrader5PositionsCache.txt";

        public bool _changeClassUse = false;
        public bool _useCurrency = false;
        public bool _useMetals = false;
        public bool _useFunds = false;
        public bool _useAnotherOne = false;
        public bool _useIndexes = false;
        public bool _onlyMarketWatch = false;
        public bool _marketDepthOfTicks;
        public string _depositCurrency;
        public bool _profitInPoints;
        public int _shiftTime;
        public bool _useNetting;
        private string _accountId;

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            try
            {
                _securities = !_changeClassUse && IsLoadSecuritiesFromCache() ? LoadSecuritiesFromCache() : LoadSecuritiesFromMetaTrader();

                if (_securities == null)
                {
                    return;
                }

                if (SecurityEvent != null)
                {
                    SecurityEvent(_securities);
                }

                SendLogMessage(OsLocalization.Market.Message52 + _securities.Count, LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error GetSecurities: {ex.ToString()}", LogMessageType.Error);
            }
        }

        private List<Security> LoadSecuritiesFromMetaTrader()
        {
            try
            {
                SendLogMessage("The loading of security has begun, wait...", LogMessageType.System);

                List<Security> securities = new List<Security>();

                int securitiesCount = _mtApiClient.SymbolsTotal(false);

                for (int i = 0; i < securitiesCount; i++)
                {
                    Security security = new Security();
                    security.Exchange = ServerType.MetaTrader5.ToString();

                    string symbol = "";
                    if (_onlyMarketWatch)
                        symbol = _mtApiClient.SymbolName(i, true);
                    else
                        symbol = _mtApiClient.SymbolName(i, false);

                    if (symbol == "")
                        continue;

                    SelectSymbolInMT5(symbol);

                    SecurityType secType = SecurityType.None;

                    if (!CheckFilter(symbol, ref secType))
                        continue;

                    security.Name = symbol;
                    security.NameFull = symbol;
                    security.NameId = i.ToString();
                    security.NameClass = _mtApiClient.SymbolInfoString(symbol, ENUM_SYMBOL_INFO_STRING.SYMBOL_CURRENCY_PROFIT);

                    security.SecurityType = secType;
                    security.VolumeStep = _mtApiClient.SymbolInfoDouble(symbol, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_VOLUME_STEP).ToDecimal();
                    security.MinTradeAmount = _mtApiClient.SymbolInfoDouble(symbol, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_VOLUME_MIN).ToDecimal();
                    security.PriceStep = _mtApiClient.SymbolInfoDouble(symbol, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_TRADE_TICK_SIZE).ToDecimal();
                    security.PriceLimitLow = _mtApiClient.SymbolInfoDouble(security.NameId, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_SESSION_PRICE_LIMIT_MIN).ToDecimal();
                    security.PriceLimitHigh = _mtApiClient.SymbolInfoDouble(security.NameId, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_SESSION_PRICE_LIMIT_MAX).ToDecimal();
                    security.Decimals = (int)_mtApiClient.SymbolInfoInteger(symbol, ENUM_SYMBOL_INFO_INTEGER.SYMBOL_DIGITS);
                    security.Lot = _mtApiClient.SymbolInfoDouble(security.Name, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_TRADE_CONTRACT_SIZE).ToDecimal();
                    security.State = SecurityStateType.Activ;

                    if (!_profitInPoints && (security.SecurityType == SecurityType.CurrencyPair || security.SecurityType == SecurityType.Commodities))
                        CalculatePriceStep(ref security);
                    else
                        security.PriceStepCost = security.PriceStep / security.Lot;

                    // Синхронизируем данные для MarketWatch \\
                    long isTrue = _mtApiClient.SymbolInfoInteger(security.Name, ENUM_SYMBOL_INFO_INTEGER.SYMBOL_SELECT);
                    if (isTrue == 1)
                    {
                        _mtApiClient.SymbolIsSynchronized(security.Name);
                    }

                    securities.Add(security);
                }

                if (securities.Count > 0)
                {
                    SaveToCache(securities);
                }

                return securities;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error LoadSecuritiesFromMetaTrader: {ex.ToString()}", LogMessageType.Error);
                return null;
            }
        }

        private void CalculatePriceStep(ref Security security)
        {
            try
            {
                // -- Считаем стоимость пункта -- \\

                decimal point = security.PriceStep;

                if (security.SecurityType == SecurityType.CurrencyPair)
                {
                    if (security.Decimals == 5)
                        point = Math.Ceiling(security.PriceStep * 10000m) / 10000m;
                    else if (security.Decimals == 4)
                        point = Math.Ceiling(security.PriceStep * 1000m) / 1000m;
                    else if (security.Decimals == 3)
                        point = Math.Ceiling(security.PriceStep * 100m) / 100m;
                }

                // Если валюта депозита и валюта прибыли одинаковая \\
                if (_depositCurrency == security.NameClass)
                {
                    security.PriceStepCost = (security.Lot * point) * security.PriceStep / 10;
                    return;
                }
                else
                {
                    string currencyMargin = _mtApiClient.SymbolInfoString(security.Name, ENUM_SYMBOL_INFO_STRING.SYMBOL_CURRENCY_MARGIN);

                    // Если валюта депозита равна базовой \\
                    if (currencyMargin == _depositCurrency)
                    {
                        decimal currencyPrice = _mtApiClient.SymbolInfoDouble(security.Name, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_BID).ToDecimal();

                        if (currencyPrice != 0)
                            security.PriceStepCost = (security.Lot * point) / currencyPrice * security.PriceStep / 10;
                        return;
                    }
                    // Если валюта депозита ни базовая, ни котировочная, считаем кросс-курс \\
                    else
                    {
                        if (security.NameClass == "USD")
                        {
                            SelectSymbolInMT5("USD" + _depositCurrency);

                            decimal priceDepositCurrency = _mtApiClient.SymbolInfoDouble("USD" + _depositCurrency, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_BID).ToDecimal();

                            security.PriceStepCost = (security.Lot * point) * priceDepositCurrency * security.PriceStep / 10;
                            return;
                        }

                        string specifiedCurrency = security.NameClass + "USD";
                        SelectSymbolInMT5(specifiedCurrency);

                        decimal specifiedCurrencyPrice = _mtApiClient.SymbolInfoDouble(specifiedCurrency, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_BID).ToDecimal();

                        if (specifiedCurrencyPrice != 0)
                        {
                            decimal reverseSpecifiedCurrencyPrice = 1 / specifiedCurrencyPrice;

                            if (_depositCurrency != "USD")
                            {
                                SelectSymbolInMT5("USD" + _depositCurrency);
                                decimal priceDepositCurrency = _mtApiClient.SymbolInfoDouble("USD" + _depositCurrency, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_BID).ToDecimal();

                                security.PriceStepCost = (security.Lot * point) / reverseSpecifiedCurrencyPrice * priceDepositCurrency * security.PriceStep / 10;
                                return;
                            }
                            else
                            {
                                security.PriceStepCost = (security.Lot * point) / reverseSpecifiedCurrencyPrice * security.PriceStep / 10;
                                return;
                            }
                        }
                        else
                        {
                            specifiedCurrency = "USD" + security.NameClass;
                            SelectSymbolInMT5(specifiedCurrency);

                            specifiedCurrencyPrice = _mtApiClient.SymbolInfoDouble(specifiedCurrency, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_BID).ToDecimal();

                            if (specifiedCurrencyPrice != 0)
                            {
                                if (_depositCurrency != "USD")
                                {
                                    decimal priceDepositCurrency = _mtApiClient.SymbolInfoDouble("USD" + _depositCurrency, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_BID).ToDecimal();

                                    security.PriceStepCost = (security.Lot * point) / specifiedCurrencyPrice * priceDepositCurrency * security.PriceStep / 10;
                                    return;
                                }
                                else
                                {
                                    security.PriceStepCost = (security.Lot * point) / specifiedCurrencyPrice * security.PriceStep / 10;
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error CalculatePriceStep: {ex.ToString()}", LogMessageType.Error);
            }
        }

        private void SelectSymbolInMT5(string symbol)
        {
            try
            {
                long isSelected = _mtApiClient.SymbolInfoInteger(symbol, ENUM_SYMBOL_INFO_INTEGER.SYMBOL_SELECT);
                if (isSelected != 1)
                {
                    _mtApiClient.SymbolSelect(symbol, true);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error SelectSymbolInMT5: {ex.ToString()}", LogMessageType.Error);
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
                    return list != null && list.Count != 0 ? list : LoadSecuritiesFromMetaTrader();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error LoadSecuritiesFromCache: {ex.ToString()}", LogMessageType.Error);
                return LoadSecuritiesFromMetaTrader();
            }
        }

        public event Action<List<Security>> SecurityEvent;

        private void ThreadRecalculatingCostPriceStep()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_securities != null && _securities.Count == 0)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_profitInPoints)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    for (int i = 0; i < _securities.Count; i++)
                    {
                        var sec = _securities[i];
                        CalculatePriceStep(ref sec);
                    }

                    if (SecurityEvent != null)
                    {
                        SecurityEvent(_securities);
                    }

                    Thread.Sleep(60000);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"Error ThreadRecalculatingCostPriceStep: {ex.ToString()}", LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        #endregion

        #region 4 Portfolios

        public void GetPortfolios()
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    Thread.Sleep(1000);
                    return;
                }

                _accountId = _mtApiClient.AccountInfoInteger(ENUM_ACCOUNT_INFO_INTEGER.ACCOUNT_LOGIN).ToString();

                Portfolio myPortfolio = _myPortfolios.Find(p => p.Number == _accountId);

                if (myPortfolio == null)
                {
                    myPortfolio = new Portfolio();
                    myPortfolio.ServerType = ServerType.MetaTrader5;
                    myPortfolio.Number = _accountId;
                    myPortfolio.ValueCurrent = _mtApiClient.AccountInfoDouble(ENUM_ACCOUNT_INFO_DOUBLE.ACCOUNT_ASSETS).ToDecimal();
                    myPortfolio.ValueBegin = _mtApiClient.AccountInfoDouble(ENUM_ACCOUNT_INFO_DOUBLE.ACCOUNT_BALANCE).ToDecimal();
                    myPortfolio.ValueBlocked = _mtApiClient.AccountInfoDouble(ENUM_ACCOUNT_INFO_DOUBLE.ACCOUNT_COMMISSION_BLOCKED).ToDecimal();
                    myPortfolio.UnrealizedPnl = _mtApiClient.AccountInfoDouble(ENUM_ACCOUNT_INFO_DOUBLE.ACCOUNT_PROFIT).ToDecimal();

                    _myPortfolios.Add(myPortfolio);
                }
                else
                {
                    myPortfolio.ValueCurrent = _mtApiClient.AccountInfoDouble(ENUM_ACCOUNT_INFO_DOUBLE.ACCOUNT_BALANCE).ToDecimal();
                    myPortfolio.UnrealizedPnl = _mtApiClient.AccountInfoDouble(ENUM_ACCOUNT_INFO_DOUBLE.ACCOUNT_PROFIT).ToDecimal();
                }

                long positionsCount = _mtApiClient.PositionsTotal();

                if (myPortfolio.PositionOnBoard != null)
                {
                    myPortfolio.PositionOnBoard.Clear();
                }

                List<PositionOnBoard> positions = new List<PositionOnBoard>();

                for (int i = 0; i < positionsCount; i++)
                {
                    PositionOnBoard position = new PositionOnBoard();

                    ulong posTicket = _mtApiClient.PositionGetTicket(i);

                    bool isPos = _mtApiClient.PositionSelectByTicket(posTicket);

                    if (!isPos)
                    {
                        continue;
                    }

                    position.PortfolioName = myPortfolio.Number;
                    position.ValueCurrent = _mtApiClient.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_VOLUME).ToDecimal();
                    position.ValueBegin = position.ValueCurrent;
                    position.ValueBlocked = position.ValueCurrent;
                    position.UnrealizedPnl = _mtApiClient.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_PROFIT).ToDecimal();

                    long positionType = _mtApiClient.PositionGetInteger(ENUM_POSITION_PROPERTY_INTEGER.POSITION_TYPE);

                    if (positionType == 0)
                    {
                        position.SecurityNameCode = _mtApiClient.PositionGetSymbol(i) + "_LONG";
                    }
                    else if (positionType == 1)
                    {
                        position.SecurityNameCode = _mtApiClient.PositionGetSymbol(i) + "_SHORT";
                    }

                    if (positions.Count == 0)
                        positions.Add(position);
                    else
                    {
                        bool inArray = false;
                        for (int i2 = 0; i2 < positions.Count; i2++)
                        {
                            PositionOnBoard positionOnBoard = positions[i2];

                            if (positionOnBoard.SecurityNameCode == position.SecurityNameCode)
                            {
                                positionOnBoard.ValueCurrent += position.ValueCurrent;
                                positionOnBoard.ValueBlocked += position.ValueBlocked;
                                positionOnBoard.ValueBegin += position.ValueBegin;
                                positionOnBoard.UnrealizedPnl += position.UnrealizedPnl;

                                inArray = true;
                                break;
                            }
                        }

                        if (!inArray)
                        {
                            positions.Add(position);
                        }
                    }
                }

                for (int i = 0; i < positions.Count; i++)
                {
                    myPortfolio.SetNewPosition(positions[i]);
                }

                PortfolioEvent?.Invoke(_myPortfolios);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error GetPortfolios: {ex.ToString()}", LogMessageType.Error);
            }
        }

        private void ThreadPortfolioUpdate()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    Thread.Sleep(5000);

                    GetPortfolios();
                }
                catch (Exception ex)
                {
                    SendLogMessage($"Error ThreadPortfolioUpdate: {ex.ToString()}", LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            try
            {
                int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
                DateTime endTime = DateTime.Now;
                DateTime startTime = endTime.AddMinutes(-tfTotalMinutes * candleCount);

                return GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, endTime);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error GetLastCandleHistory: {ex.ToString()}", LogMessageType.Error);
                return null;
            }
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTimeOse, DateTime endTime, DateTime actualTime)
        {
            try
            {
                int tfTotalMinutes = (int)timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes;
                if (tfTotalMinutes <= 0)
                {
                    SendLogMessage("Таймфрейм должен быть положительным числом", LogMessageType.Error);
                    return null;
                }

                long isSelected = _mtApiClient.SymbolInfoInteger(security.Name, ENUM_SYMBOL_INFO_INTEGER.SYMBOL_SELECT);
                if (isSelected != 1)
                {
                    _mtApiClient.SymbolSelect(security.Name, true);
                }

                ENUM_TIMEFRAMES timeFrameMT5 = GetTimeFrame(timeFrameBuilder);

                int loadResult = CheckLoadHistory(security.Name, timeFrameMT5, startTimeOse);
                if (loadResult < 0)
                {
                    SendLogMessage($"Не удалось загрузить историю для {security.Name}", LogMessageType.Error);
                    return null;
                }

                int totalBars = _mtApiClient.Bars(security.Name, timeFrameMT5);
                if (totalBars <= 0)
                {
                    SendLogMessage($"Нет доступных баров для {security.Name} на таймфрейме {timeFrameMT5}", LogMessageType.Error);
                    return null;
                }

                double totalMinutes = (endTime - startTimeOse).TotalMinutes;
                int requiredCandleCount = (int)(totalMinutes / tfTotalMinutes) + 1;

                if (requiredCandleCount <= 0)
                {
                    SendLogMessage($"Рассчитано неверное количество свечей: {requiredCandleCount}", LogMessageType.Error);
                    return null;
                }

                int candlesToCopy = Math.Min(requiredCandleCount, totalBars);
                int copied = _mtApiClient.CopyRates(security.Name, timeFrameMT5, 0, candlesToCopy, out MqlRates[] mtCandles);

                if (copied <= 0 || mtCandles == null || mtCandles.Length == 0)
                {
                    SendLogMessage($"Не удалось скопировать данные для {security.Name}. Скопировано: {copied}", LogMessageType.Error);
                    return null;
                }

                List<Candle> candles = new List<Candle>();

                for (int i = 0; i < mtCandles.Length; i++)
                {
                    MqlRates mtCandle = mtCandles[i];

                    Candle candle = new Candle
                    {
                        TimeStart = mtCandle.time.AddHours(_shiftTime),
                        Open = mtCandle.open.ToDecimal(),
                        Close = mtCandle.close.ToDecimal(),
                        High = mtCandle.high.ToDecimal(),
                        Low = mtCandle.low.ToDecimal(),
                        Volume = mtCandle.tick_volume
                    };
                    candles.Add(candle);
                }

                return candles;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error GetCandleDataToSecurity: {security.Name}: {ex}", LogMessageType.Error);
                return null;
            }
        }

        private int CheckLoadHistory(string symbol, ENUM_TIMEFRAMES period, DateTime startDate)
        {
            try
            {
                DateTime firstDate = DateTime.MinValue;

                long firstDateLong = _mtApiClient.SeriesInfoInteger(symbol, period, ENUM_SERIES_INFO_INTEGER.SERIES_FIRSTDATE);
                if (firstDateLong > 0)
                {
                    firstDate = TimeManager.GetDateTimeFromTimeStampSeconds(firstDateLong);
                    if (firstDate > DateTime.MinValue && firstDate <= startDate)
                    {
                        return 1;
                    }
                }

                int maxBars = _mtApiClient.TerminalInfoInteger(ENUM_TERMINAL_INFO_INTEGER.TERMINAL_MAXBARS);

                DateTime firstServerDate = DateTime.MinValue;
                int attempts = 0;
                long firstServerDateLong = _mtApiClient.SeriesInfoInteger(symbol, ENUM_TIMEFRAMES.PERIOD_M1, ENUM_SERIES_INFO_INTEGER.SERIES_SERVER_FIRSTDATE);
                while (attempts < 100 && firstServerDateLong > 0)
                {
                    firstServerDateLong = _mtApiClient.SeriesInfoInteger(symbol, ENUM_TIMEFRAMES.PERIOD_M1, ENUM_SERIES_INFO_INTEGER.SERIES_SERVER_FIRSTDATE);
                    Thread.Sleep(5);
                    attempts++;
                }

                if (firstServerDateLong > 0)
                {
                    firstServerDate = TimeManager.GetDateTimeFromTimeStampSeconds(firstServerDateLong);
                    if (startDate < firstServerDate)
                    {
                        startDate = firstServerDate;
                    }
                }

                int failCount = 0;
                int maxAttempts = 100;

                while (failCount < maxAttempts)
                {
                    if (!_mtApiClient.SymbolIsSynchronized(symbol))
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    int bars = _mtApiClient.Bars(symbol, period);
                    if (bars > 0)
                    {
                        if (bars >= maxBars)
                            return -2;

                        firstDateLong = _mtApiClient.SeriesInfoInteger(symbol, period, ENUM_SERIES_INFO_INTEGER.SERIES_FIRSTDATE);
                        if (firstDateLong > 0)
                        {
                            firstDate = TimeManager.GetDateTimeFromTimeStampSeconds(firstDateLong);
                            if (firstDate > DateTime.MinValue && firstDate <= startDate)
                                return 0;
                        }
                    }

                    int copied = _mtApiClient.CopyRates(symbol, period, bars, 100, out MqlRates[] testCandles);

                    if (copied > 0)
                    {
                        if (testCandles[0].time <= startDate)
                            return 0;

                        if (bars + copied >= maxBars)
                            return -2;

                        failCount = 0;
                    }
                    else
                    {
                        failCount++;
                        Thread.Sleep(10);
                    }
                }

                return -5;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error CheckLoadHistory {symbol}: {ex}", LogMessageType.Error);
                return -1;
            }
        }

        private ENUM_TIMEFRAMES GetTimeFrame(TimeFrameBuilder timeFrameBuilder)
        {
            switch (timeFrameBuilder.TimeFrame)
            {
                case TimeFrame.Min1:
                    return ENUM_TIMEFRAMES.PERIOD_M1;
                case TimeFrame.Min2:
                    return ENUM_TIMEFRAMES.PERIOD_M2;
                case TimeFrame.Min5:
                    return ENUM_TIMEFRAMES.PERIOD_M5;
                case TimeFrame.Min10:
                    return ENUM_TIMEFRAMES.PERIOD_M10;
                case TimeFrame.Min15:
                    return ENUM_TIMEFRAMES.PERIOD_M15;
                case TimeFrame.Min30:
                    return ENUM_TIMEFRAMES.PERIOD_M30;
                case TimeFrame.Hour1:
                    return ENUM_TIMEFRAMES.PERIOD_H1;
                case TimeFrame.Hour2:
                    return ENUM_TIMEFRAMES.PERIOD_H2;
                case TimeFrame.Hour4:
                    return ENUM_TIMEFRAMES.PERIOD_H4;
                case TimeFrame.Day:
                    return ENUM_TIMEFRAMES.PERIOD_D1;
                default:
                    return ENUM_TIMEFRAMES.PERIOD_M1;
            }
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            try
            {
                return null;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 6 Security subscribe

        public void Subscribe(Security security)
        {
            try
            {
                long isSelected = _mtApiClient.SymbolInfoInteger(security.Name, ENUM_SYMBOL_INFO_INTEGER.SYMBOL_SELECT);
                if (isSelected != 1)
                {
                    _mtApiClient.SymbolSelect(security.Name, true);
                }

                _mtApiClient.ResetLastError();
                long chartId = _mtApiClient.ChartOpen(security.NameId, ENUM_TIMEFRAMES.PERIOD_M1);

                if (!_mtApiClient.MarketBookAdd(security.Name))
                {
                    int error = _mtApiClient.GetLastError();
                    _mtApiClient.ResetLastError();

                    SendLogMessage($"{(ErrorCode)error}", LogMessageType.System);
                }

                List<MqlTick> ticks = _mtApiClient.CopyTicks(security.Name, CopyTicksFlag.All, 0, 1);

                if (!_dictionaryLastTimeTick.ContainsKey(security.Name))
                    _dictionaryLastTimeTick.Add(security.Name, DateTime.Now);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error Subscribe{ex.ToString()}", LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            try
            {
                return false;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return false;
            }
        }

        #endregion

        #region 7 Reading event messages

        private void ThreadUpdateMyTransaction()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_myTransactionQueue.IsEmpty == false)
                    {
                        Mt5TradeTransactionEventArgs data = null;

                        if (_myTransactionQueue.TryDequeue(out data))
                        {
                            UpdateMyTransaction(data);
                        }
                    }
                    else Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"Error ThreadUpdateMyTransaction: {ex.ToString()}", LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        private void UpdateMyTransaction(Mt5TradeTransactionEventArgs myTransaction)
        {
            try
            {
                // -- Мои трейды -- \\
                if (myTransaction.Trans.Type == ENUM_TRADE_TRANSACTION_TYPE.TRADE_TRANSACTION_DEAL_ADD)
                {
                    if (myTransaction.Trans.DealType == ENUM_DEAL_TYPE.DEAL_TYPE_BUY ||
                        myTransaction.Trans.DealType == ENUM_DEAL_TYPE.DEAL_TYPE_SELL)
                    {
                        MyTrade trade = new MyTrade();

                        if (myTransaction.Trans.Order == 0)
                        {
                            return;
                        }

                        bool isDeal = _mtApiClient.HistoryDealSelect(myTransaction.Trans.Deal);

                        if (isDeal)
                        {
                            trade.Time = TimeManager.GetDateTimeFromTimeStamp(_mtApiClient.HistoryDealGetInteger(myTransaction.Trans.Deal, ENUM_DEAL_PROPERTY_INTEGER.DEAL_TIME_MSC)).AddHours(_shiftTime);
                            trade.NumberOrderParent = myTransaction.Trans.Order.ToString();
                            trade.NumberTrade = myTransaction.Trans.Deal.ToString();
                            trade.Volume = (decimal)myTransaction.Trans.Volume;
                            trade.Price = (decimal)myTransaction.Trans.Price;
                            trade.SecurityNameCode = myTransaction.Trans.Symbol;
                            trade.Side = myTransaction.Trans.DealType == ENUM_DEAL_TYPE.DEAL_TYPE_BUY ? Side.Buy : Side.Sell;

                            SendLogMessage($"Пришел трейд: {trade.NumberTrade}, orderId: {trade.NumberOrderParent}, Type: {myTransaction.Trans.Type}, Sec: {trade.SecurityNameCode}", LogMessageType.System);

                            MyTradeEvent.Invoke(trade);
                        }
                    }
                }
                // Мои ордера \\
                else if (myTransaction.Trans.Type == ENUM_TRADE_TRANSACTION_TYPE.TRADE_TRANSACTION_ORDER_UPDATE ||
                    myTransaction.Trans.Type == ENUM_TRADE_TRANSACTION_TYPE.TRADE_TRANSACTION_ORDER_DELETE)
                {
                    _mtApiClient.ResetLastError();
                    bool isOrder = _mtApiClient.OrderSelect(myTransaction.Trans.Order);

                    if (isOrder)
                    {
                        Order order = new Order();

                        if (myTransaction.Trans.Order != 0)
                            order.NumberMarket = myTransaction.Trans.Order.ToString();

                        long magic = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_MAGIC);
                        long orderType = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_TYPE);
                        long orderState = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_STATE);
                        long orderTime = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_TIME_SETUP);
                        long positionIdentifier = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_POSITION_ID);

                        order.NumberUser = Convert.ToInt32(magic);
                        order.State = GetOrderStateType(orderState);

                        order.Comment = positionIdentifier.ToString();
                        order.Volume = myTransaction.Trans.Volume.ToDecimal();
                        order.Price = myTransaction.Trans.Price.ToDecimal();
                        order.ServerType = ServerType.MetaTrader5;
                        order.SecurityNameCode = myTransaction.Trans.Symbol;
                        order.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(orderTime).AddHours(_shiftTime);
                        order.PortfolioNumber = _accountId;

                        if (myTransaction.Trans.OrderType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY)
                        {
                            order.Side = Side.Buy;
                            order.TypeOrder = OrderPriceType.Market;
                        }
                        else if (myTransaction.Trans.OrderType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL)
                        {
                            order.Side = Side.Sell;
                            order.TypeOrder = OrderPriceType.Market;
                        }
                        else if (myTransaction.Trans.OrderType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY_LIMIT)
                        {
                            order.Side = Side.Buy;
                            order.TypeOrder = OrderPriceType.Limit;
                        }
                        else if (myTransaction.Trans.OrderType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL_LIMIT)
                        {
                            order.Side = Side.Sell;
                            order.TypeOrder = OrderPriceType.Limit;
                        }

                        SendLogMessage($"Пришел ордер: {order.NumberMarket}, state: {order.State}, Type: {myTransaction.Trans.Type}, Sec:{order.SecurityNameCode}, Real", LogMessageType.System);

                        MyOrderEvent.Invoke(order);
                        return;
                    }

                    bool isHistoryOrder = _mtApiClient.HistoryOrderSelect(myTransaction.Trans.Order);

                    if (isHistoryOrder)
                    {
                        Order order = new Order();

                        if (myTransaction.Trans.Order != 0)
                            order.NumberMarket = myTransaction.Trans.Order.ToString();

                        long magic = _mtApiClient.HistoryOrderGetInteger(myTransaction.Trans.Order, ENUM_ORDER_PROPERTY_INTEGER.ORDER_MAGIC);
                        long orderType = _mtApiClient.HistoryOrderGetInteger(myTransaction.Trans.Order, ENUM_ORDER_PROPERTY_INTEGER.ORDER_TYPE);
                        long orderState = _mtApiClient.HistoryOrderGetInteger(myTransaction.Trans.Order, ENUM_ORDER_PROPERTY_INTEGER.ORDER_STATE);
                        long orderTime = _mtApiClient.HistoryOrderGetInteger(myTransaction.Trans.Order, ENUM_ORDER_PROPERTY_INTEGER.ORDER_TIME_SETUP);
                        long positionIdentifier = _mtApiClient.HistoryOrderGetInteger(myTransaction.Trans.Order, ENUM_ORDER_PROPERTY_INTEGER.ORDER_POSITION_ID);
                        double volume = _mtApiClient.HistoryOrderGetDouble(myTransaction.Trans.Order, ENUM_ORDER_PROPERTY_DOUBLE.ORDER_VOLUME_INITIAL);

                        order.NumberUser = Convert.ToInt32(magic);
                        order.State = GetOrderStateType(orderState);

                        order.Comment = positionIdentifier.ToString();
                        order.Volume = (decimal)volume;
                        order.Price = myTransaction.Trans.Price.ToDecimal();
                        order.ServerType = ServerType.MetaTrader5;
                        order.SecurityNameCode = myTransaction.Trans.Symbol;
                        order.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(orderTime).AddHours(_shiftTime);
                        order.PortfolioNumber = _accountId;

                        if (myTransaction.Trans.OrderType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY)
                        {
                            order.Side = Side.Buy;
                            order.TypeOrder = OrderPriceType.Market;
                        }
                        else if (myTransaction.Trans.OrderType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL)
                        {
                            order.Side = Side.Sell;
                            order.TypeOrder = OrderPriceType.Market;
                        }
                        else if (myTransaction.Trans.OrderType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY_LIMIT)
                        {
                            order.Side = Side.Buy;
                            order.TypeOrder = OrderPriceType.Limit;
                        }
                        else if (myTransaction.Trans.OrderType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL_LIMIT)
                        {
                            order.Side = Side.Sell;
                            order.TypeOrder = OrderPriceType.Limit;
                        }

                        SendLogMessage($"Пришел ордер: {order.NumberMarket}, state: {order.State}, Type: {myTransaction.Trans.Type}, Sec: {order.SecurityNameCode}, History", LogMessageType.System);

                        MyOrderEvent.Invoke(order);
                        return;
                    }

                    int error = _mtApiClient.GetLastError();
                    _mtApiClient.ResetLastError();

                    SendLogMessage($"{(ErrorCode)error}", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error UpdateMyTransaction: {ex.ToString()}", LogMessageType.Error);
            }
        }

        private void ThreadUpdateTiks()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_ticksQueue.IsEmpty == false)
                    {
                        Mt5QuoteEventArgs data = null;

                        if (_ticksQueue.TryDequeue(out data))
                        {
                            UpdateTick(data.Quote);
                        }
                    }
                    else Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"Error ThreadUpdateTiks: {ex.ToString()}", LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        private void UpdateTick(Mt5Quote ticks)
        {
            try
            {
                if (!_dictionaryLastTimeTick.ContainsKey(ticks.Instrument)) return;

                List<MqlTick> lastTicks = new List<MqlTick>();
                try
                {
                    lastTicks = _mtApiClient.CopyTicks(ticks.Instrument, CopyTicksFlag.All, 0, 50);
                }
                catch
                {
                    //ignore
                }

                if (lastTicks == null || lastTicks.Count == 0) return;

                List<Trade> tradeList = new List<Trade>();

                for (int i = 0; i < lastTicks.Count; i++)
                {
                    MqlTick lastTick = lastTicks[i];

                    DateTime tickTime = TimeManager.GetDateTimeFromTimeStamp(lastTick.TimeMsc);

                    ServerTime = tickTime;

                    Trade trade = new Trade();

                    if (tickTime.Ticks > _dictionaryLastTimeTick[ticks.Instrument].Ticks)
                    {
                        trade.SecurityNameCode = ticks.Instrument;
                        trade.Time = tickTime.AddHours(_shiftTime);
                        trade.Id = lastTick.TimeMsc.ToString();
                        trade.Volume = 1;
                        trade.Ask = ticks.Ask.ToDecimal();
                        trade.Bid = ticks.Bid.ToDecimal();
                        trade.Price = trade.Bid;

                        if (lastTick.flags == ENUM_TICK_FLAGS.TICK_FLAG_BUY || lastTick.flags == ENUM_TICK_FLAGS.TICK_FLAG_ASK)
                        {
                            trade.Side = Side.Buy;
                        }
                        else if (lastTick.flags == ENUM_TICK_FLAGS.TICK_FLAG_SELL || lastTick.flags == ENUM_TICK_FLAGS.TICK_FLAG_BID)
                        {
                            trade.Side = Side.Sell;
                        }

                        NewTradesEvent?.Invoke(trade);
                    }

                    if (i == lastTicks.Count - 1)
                    {
                        if (_marketDepthOfTicks)
                        {
                            MarketDepth depth = new MarketDepth();
                            depth.SecurityNameCode = ticks.Instrument;
                            depth.Time = DateTime.Now;

                            MarketDepthLevel level = new MarketDepthLevel();
                            MarketDepthLevel level2 = new MarketDepthLevel();

                            level.Price = lastTick.bid;
                            level.Bid = 1;
                            depth.Bids.Add(level);

                            level2.Price = lastTick.ask;
                            level2.Ask = 1;
                            depth.Asks.Add(level2);

                            MarketDepthEvent?.Invoke(depth);
                        }

                        _dictionaryLastTimeTick[ticks.Instrument] = tickTime;
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error UpdateTick: {ex.ToString()}", LogMessageType.Error);
            }
        }

        private void ThreadUpdateMarketDepth()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_mdQueue.IsEmpty == false)
                    {
                        Mt5BookEventArgs data = null;

                        if (_mdQueue.TryDequeue(out data))
                        {
                            if (!_marketDepthOfTicks)
                                UpdateMarketDepths(data);
                        }
                    }
                    else Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"Error ThreadUpdateMarketDepth: {ex.ToString()}", LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        private void UpdateMarketDepths(Mt5BookEventArgs mt5Book)
        {
            try
            {
                if (_mtApiClient.ConnectionState != Mt5ConnectionState.Connected) return;

                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = mt5Book.Symbol;
                depth.Time = DateTime.Now;

                List<MqlTick> lastTicks = new List<MqlTick>();
                try
                {
                    bool getBook = _mtApiClient.MarketBookGet(mt5Book.Symbol, out MqlBookInfo[] mtBook);

                    if (getBook == false || mtBook == null) return;

                    for (int i = 0; i < mtBook.Length; i++)
                    {
                        MarketDepthLevel level = new MarketDepthLevel();

                        level.Price = mtBook[i].price;
                        if (mtBook[i].type == ENUM_BOOK_TYPE.BOOK_TYPE_BUY)
                        {
                            level.Bid = mtBook[i].volume_real;
                            depth.Bids.Add(level);
                        }
                        if (mtBook[i].type == ENUM_BOOK_TYPE.BOOK_TYPE_SELL)
                        {
                            level.Ask = mtBook[i].volume_real;
                            depth.Asks.Add(level);
                        }
                    }
                }
                catch
                {
                    // ignore
                }

                depth.Asks.Reverse();

                MarketDepthEvent?.Invoke(depth);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error UpdateMarketDepths: {ex.ToString()}", LogMessageType.Error);
            }
        }

        private void ConnectionStateChanged(object sender, Mt5ConnectionEventArgs e)
        {
            switch (e.Status)
            {
                case Mt5ConnectionState.Connecting:
                    // ignore
                    break;
                case Mt5ConnectionState.Connected:
                    SetСonnected();
                    break;
                case Mt5ConnectionState.Disconnected:
                    Dispose();
                    break;
                case Mt5ConnectionState.Failed:
                    SendLogMessage($"Connection failed. {e.ConnectionMessage}", LogMessageType.System);

                    Dispose();
                    break;
            }
        }

        private void OnBookEvent(object sender, Mt5BookEventArgs e)
        {
            _mdQueue.Enqueue(e);
        }

        private void QuoteUpdate(object sender, Mt5QuoteEventArgs e)
        {
            _ticksQueue.Enqueue(e);
        }

        private void OnTradeTransaction(object sender, Mt5TradeTransactionEventArgs e)
        {
            _myTransactionQueue.Enqueue(e);
        }

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        #endregion

        #region 8 Trade
        public void SendOrder(Order order)
        {
            try
            {
                MqlTradeRequest request = new MqlTradeRequest();

                if (!_useNetting)
                {
                    if (order.NumberPosition != 0)
                    {
                        if (_dictionaryOpenPositions.ContainsKey(order.NumberPosition))
                        {
                            request.Position = _dictionaryOpenPositions[order.NumberPosition];
                        }
                    }
                }

                request.Magic = (ulong)order.NumberUser;
                request.Volume = Convert.ToDouble(order.Volume);
                request.Symbol = order.SecurityNameCode;
                request.Deviation = 1000;

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    request.Action = ENUM_TRADE_REQUEST_ACTIONS.TRADE_ACTION_PENDING;
                    request.Price = Convert.ToDouble(order.Price);
                    request.Type_filling = ENUM_ORDER_TYPE_FILLING.ORDER_FILLING_RETURN;
                    request.Type_time = ENUM_ORDER_TYPE_TIME.ORDER_TIME_DAY;

                    if (order.Side == Side.Buy)
                    {
                        request.Type = ENUM_ORDER_TYPE.ORDER_TYPE_BUY_LIMIT;
                    }

                    if (order.Side == Side.Sell)
                    {
                        request.Type = ENUM_ORDER_TYPE.ORDER_TYPE_SELL_LIMIT;
                    }
                }
                else if (order.TypeOrder == OrderPriceType.Market)
                {
                    request.Action = ENUM_TRADE_REQUEST_ACTIONS.TRADE_ACTION_DEAL;
                    request.Type_filling = ENUM_ORDER_TYPE_FILLING.ORDER_FILLING_FOK;

                    if (order.Side == Side.Buy)
                    {
                        request.Type = ENUM_ORDER_TYPE.ORDER_TYPE_BUY;
                    }

                    if (order.Side == Side.Sell)
                    {
                        request.Type = ENUM_ORDER_TYPE.ORDER_TYPE_SELL;
                    }
                }

                _mtApiClient.ResetLastError();
                bool checkResult = _mtApiClient.OrderCheck(request, out MqlTradeCheckResult orderCheckResult);

                if (!checkResult)
                {
                    order.State = OrderStateType.Fail;

                    var error = _mtApiClient.GetLastError();

                    SendLogMessage($"SendOrder fail. Error code - {orderCheckResult.Retcode}. Comment - {orderCheckResult.Comment} | Error - {(ErrorCode)error}", LogMessageType.System);

                    MyOrderEvent.Invoke(order);

                    return;
                }

                _mtApiClient.ResetLastError();
                bool result = _mtApiClient.OrderSend(request, out MqlTradeResult orderResult);

                if (result)
                {
                    if (!_dictionaryOpenPositions.ContainsKey(order.NumberPosition))
                    {
                        _dictionaryOpenPositions.Add(order.NumberPosition, orderResult.Order);

                        SavePositionsInFile();
                    }

                    order.State = OrderStateType.Active;
                }
                else
                {
                    order.State = OrderStateType.Fail;

                    SendLogMessage($"SendOrder fail. Error code - {orderResult.Retcode}. Comment - {orderResult.Comment}", LogMessageType.System);

                    MyOrderEvent.Invoke(order);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error SendOrder: {ex.ToString()}", LogMessageType.Error);
                order.State = OrderStateType.Fail;
                MyOrderEvent.Invoke(order);
            }
        }

        public bool CancelOrder(Order order)
        {
            try
            {
                MqlTradeRequest request = new MqlTradeRequest();

                request.Action = ENUM_TRADE_REQUEST_ACTIONS.TRADE_ACTION_REMOVE;
                request.Order = Convert.ToUInt64(order.NumberMarket);

                _mtApiClient.ResetLastError();
                if (!_mtApiClient.OrderSend(request, out MqlTradeResult response))
                {
                    SendLogMessage($"Cancel order error. Code: {_mtApiClient.GetLastError()}", LogMessageType.Error);
                    _mtApiClient.ResetLastError();
                    return false;
                }

                order.State = response.Retcode == 10009 ? OrderStateType.Cancel : OrderStateType.Fail;
                if (order.State == OrderStateType.Cancel)
                {
                    order.TimeCancel = order.TimeCallBack;
                }

                MyOrderEvent.Invoke(order);
                return true;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error CancelOrder: {ex.ToString()}", LogMessageType.Error);
                return false;
            }
        }

        public void CancelAllOrders()
        {
            try
            {
                return;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error CancelAllOrders: {ex.ToString()}", LogMessageType.Error);
                return;
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                return;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error CancelAllOrdersToSecurity: {ex.ToString()}", LogMessageType.Error);
                return;
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            try
            {
                return;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error ChangeOrderPrice: {ex.ToString()}", LogMessageType.Error);
                return;
            }
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            try
            {
                int totalOrders = _mtApiClient.OrdersTotal();

                if (totalOrders == 0) return null;

                List<Order> orders = new List<Order>();

                if (totalOrders < count)
                    count = totalOrders;

                for (int i = startIndex; i < totalOrders; i++)
                {
                    if (count == 0) break;

                    count--;

                    ulong orderTicket = _mtApiClient.OrderGetTicket(i);

                    if (orderTicket == 0) continue;

                    _mtApiClient.OrderSelect(orderTicket);

                    Order order = new Order();

                    order.ServerType = ServerType.MetaTrader5;
                    order.SecurityNameCode = _mtApiClient.OrderGetString(ENUM_ORDER_PROPERTY_STRING.ORDER_SYMBOL);
                    order.NumberMarket = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_TICKET).ToString();

                    long orderState = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_STATE);
                    order.State = GetOrderStateType(orderState);

                    long orderTime = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_TIME_SETUP);
                    order.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(orderTime);
                    order.PortfolioNumber = _accountId;
                    order.Volume = _mtApiClient.OrderGetDouble(ENUM_ORDER_PROPERTY_DOUBLE.ORDER_VOLUME_INITIAL).ToDecimal();
                    order.VolumeExecute = order.Volume - _mtApiClient.OrderGetDouble(ENUM_ORDER_PROPERTY_DOUBLE.ORDER_VOLUME_CURRENT).ToDecimal();
                    order.Price = _mtApiClient.OrderGetDouble(ENUM_ORDER_PROPERTY_DOUBLE.ORDER_PRICE_OPEN).ToDecimal();

                    long orderSideType = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_TYPE);

                    if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY)
                    {
                        order.Side = Side.Buy;
                        order.TypeOrder = OrderPriceType.Market;
                    }
                    else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL)
                    {
                        order.Side = Side.Sell;
                        order.TypeOrder = OrderPriceType.Market;
                    }
                    else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY_LIMIT)
                    {
                        order.Side = Side.Buy;
                        order.TypeOrder = OrderPriceType.Limit;
                    }
                    else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL_LIMIT)
                    {
                        order.Side = Side.Sell;
                        order.TypeOrder = OrderPriceType.Limit;
                    }

                    orders.Add(order);
                }

                return orders;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error GetActiveOrders: {ex.ToString()}", LogMessageType.Error);
                return null;
            }
        }

        public void GetAllActivOrders()
        {
            try
            {
                int totalOrders = _mtApiClient.OrdersTotal();

                if (totalOrders == 0) return;

                for (int i = 0; i < totalOrders; i++)
                {
                    ulong orderTicket = _mtApiClient.OrderGetTicket(i);

                    if (orderTicket == 0) continue;

                    _mtApiClient.OrderSelect(orderTicket);

                    Order order = new Order();

                    order.ServerType = ServerType.MetaTrader5;
                    order.SecurityNameCode = _mtApiClient.OrderGetString(ENUM_ORDER_PROPERTY_STRING.ORDER_SYMBOL);
                    order.NumberMarket = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_TICKET).ToString();
                    order.NumberUser = (int)_mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_MAGIC);

                    long orderState = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_STATE);
                    order.State = GetOrderStateType(orderState);

                    long orderTime = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_TIME_SETUP);
                    order.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(orderTime);
                    order.PortfolioNumber = _accountId;
                    order.Volume = _mtApiClient.OrderGetDouble(ENUM_ORDER_PROPERTY_DOUBLE.ORDER_VOLUME_INITIAL).ToDecimal();
                    order.VolumeExecute = order.Volume - _mtApiClient.OrderGetDouble(ENUM_ORDER_PROPERTY_DOUBLE.ORDER_VOLUME_CURRENT).ToDecimal();
                    order.Price = _mtApiClient.OrderGetDouble(ENUM_ORDER_PROPERTY_DOUBLE.ORDER_PRICE_OPEN).ToDecimal();

                    long orderSideType = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_TYPE);

                    if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY)
                    {
                        order.Side = Side.Buy;
                        order.TypeOrder = OrderPriceType.Market;
                    }
                    else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL)
                    {
                        order.Side = Side.Sell;
                        order.TypeOrder = OrderPriceType.Market;
                    }
                    else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY_LIMIT)
                    {
                        order.Side = Side.Buy;
                        order.TypeOrder = OrderPriceType.Limit;
                    }
                    else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL_LIMIT)
                    {
                        order.Side = Side.Sell;
                        order.TypeOrder = OrderPriceType.Limit;
                    }

                    MyOrderEvent.Invoke(order);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error GetAllActivOrders: {ex.ToString()}", LogMessageType.Error);
                return;
            }
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            try
            {
                bool isHistorySelect = _mtApiClient.HistorySelect(DateTime.Now.AddDays(-1), DateTime.Now);

                if (!isHistorySelect) return null;

                int total = _mtApiClient.HistoryOrdersTotal();

                if (total == 0) return null;

                int start = total - count - startIndex;

                List<Order> orders = new List<Order>();

                for (int i = start; i < total; i++)
                {
                    if (count == 0) break;

                    count--;

                    Order order = new Order();

                    ulong ticketNumber = _mtApiClient.HistoryOrderGetTicket(i);

                    order.ServerType = ServerType.MetaTrader5;
                    order.SecurityNameCode = _mtApiClient.HistoryOrderGetString(ticketNumber, ENUM_ORDER_PROPERTY_STRING.ORDER_SYMBOL);
                    order.NumberMarket = _mtApiClient.HistoryOrderGetInteger(ticketNumber, ENUM_ORDER_PROPERTY_INTEGER.ORDER_TICKET).ToString();
                    order.NumberUser = (int)_mtApiClient.HistoryOrderGetInteger(ticketNumber, ENUM_ORDER_PROPERTY_INTEGER.ORDER_MAGIC);

                    long orderState = _mtApiClient.HistoryOrderGetInteger(ticketNumber, ENUM_ORDER_PROPERTY_INTEGER.ORDER_STATE);
                    order.State = GetOrderStateType(orderState);

                    if (order.State == OrderStateType.Done)
                    {
                        order.TimeDone = TimeManager.GetDateTimeFromTimeStamp(_mtApiClient.HistoryOrderGetInteger(ticketNumber, ENUM_ORDER_PROPERTY_INTEGER.ORDER_TIME_DONE_MSC));
                    }
                    else if (order.State == OrderStateType.Cancel)
                    {
                        order.TimeCancel = TimeManager.GetDateTimeFromTimeStamp(_mtApiClient.HistoryOrderGetInteger(ticketNumber, ENUM_ORDER_PROPERTY_INTEGER.ORDER_TIME_DONE_MSC));
                    }

                    long orderTime = _mtApiClient.HistoryOrderGetInteger(ticketNumber, ENUM_ORDER_PROPERTY_INTEGER.ORDER_TIME_SETUP);
                    order.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(orderTime);
                    order.PortfolioNumber = _accountId;
                    order.Volume = _mtApiClient.HistoryOrderGetDouble(ticketNumber, ENUM_ORDER_PROPERTY_DOUBLE.ORDER_VOLUME_INITIAL).ToDecimal();
                    order.VolumeExecute = order.Volume - _mtApiClient.HistoryOrderGetDouble(ticketNumber, ENUM_ORDER_PROPERTY_DOUBLE.ORDER_VOLUME_CURRENT).ToDecimal();
                    order.Price = _mtApiClient.HistoryOrderGetDouble(ticketNumber, ENUM_ORDER_PROPERTY_DOUBLE.ORDER_PRICE_OPEN).ToDecimal();

                    long orderSideType = _mtApiClient.HistoryOrderGetInteger(ticketNumber, ENUM_ORDER_PROPERTY_INTEGER.ORDER_TYPE);

                    if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY)
                    {
                        order.Side = Side.Buy;
                        order.TypeOrder = OrderPriceType.Market;
                    }
                    else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL)
                    {
                        order.Side = Side.Sell;
                        order.TypeOrder = OrderPriceType.Market;
                    }
                    else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY_LIMIT)
                    {
                        order.Side = Side.Buy;
                        order.TypeOrder = OrderPriceType.Limit;
                    }
                    else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL_LIMIT)
                    {
                        order.Side = Side.Sell;
                        order.TypeOrder = OrderPriceType.Limit;
                    }

                    orders.Insert(0, order);
                }

                return orders;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error GetHistoricalOrders: {ex.ToString()}", LogMessageType.Error);
                return null;
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            try
            {
                int totalOrders = _mtApiClient.OrdersTotal();

                if (totalOrders > 0)
                {
                    for (int i = 0; i < totalOrders; i++)
                    {
                        ulong orderTicket = _mtApiClient.OrderGetTicket(i);

                        if (orderTicket == 0) continue;

                        _mtApiClient.OrderSelect(orderTicket);

                        int magic = (int)_mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_MAGIC);
                        if (magic != order.NumberUser)
                        {
                            continue;
                        }

                        order.ServerType = ServerType.MetaTrader5;
                        order.SecurityNameCode = _mtApiClient.OrderGetString(ENUM_ORDER_PROPERTY_STRING.ORDER_SYMBOL);
                        order.NumberMarket = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_TICKET).ToString();

                        long orderState = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_STATE);
                        order.State = GetOrderStateType(orderState);

                        if (order.State == OrderStateType.Done)
                        {
                            MyTrade trade = new MyTrade();

                            bool historySelect = _mtApiClient.HistorySelect(DateTime.Now.AddDays(-1), DateTime.Now);

                            if (historySelect)
                            {
                                int dealsTotal = _mtApiClient.HistoryDealsTotal();

                                if (dealsTotal > 0)
                                {
                                    for (int i2 = 0; i2 < dealsTotal; i2++)
                                    {
                                        ulong dealTicket = _mtApiClient.HistoryDealGetTicket(i2);

                                        if (dealTicket == 0) continue;

                                        long dealMagic = _mtApiClient.HistoryDealGetInteger(dealTicket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_MAGIC);

                                        if (magic != order.NumberUser) continue;

                                        trade.Time = TimeManager.GetDateTimeFromTimeStamp(_mtApiClient.HistoryDealGetInteger(dealTicket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_TIME_MSC));
                                        trade.NumberOrderParent = _mtApiClient.HistoryDealGetInteger(dealTicket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_ORDER).ToString();
                                        trade.NumberTrade = _mtApiClient.HistoryDealGetInteger(dealTicket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_TICKET).ToString();
                                        trade.Volume = (decimal)_mtApiClient.HistoryDealGetDouble(dealTicket, ENUM_DEAL_PROPERTY_DOUBLE.DEAL_VOLUME);
                                        trade.Price = (decimal)_mtApiClient.HistoryDealGetDouble(dealTicket, ENUM_DEAL_PROPERTY_DOUBLE.DEAL_PRICE);
                                        trade.SecurityNameCode = _mtApiClient.HistoryDealGetString(dealTicket, ENUM_DEAL_PROPERTY_STRING.DEAL_SYMBOL);
                                        trade.Side = (ENUM_DEAL_TYPE)_mtApiClient.HistoryDealGetInteger(dealTicket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_TYPE) == ENUM_DEAL_TYPE.DEAL_TYPE_BUY ? Side.Buy : Side.Sell;

                                        MyTradeEvent.Invoke(trade);
                                    }
                                }
                            }
                        }

                        long orderTime = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_TIME_SETUP);
                        order.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(orderTime);
                        order.PortfolioNumber = _accountId;
                        order.Volume = _mtApiClient.OrderGetDouble(ENUM_ORDER_PROPERTY_DOUBLE.ORDER_VOLUME_INITIAL).ToDecimal();
                        order.VolumeExecute = order.Volume - _mtApiClient.OrderGetDouble(ENUM_ORDER_PROPERTY_DOUBLE.ORDER_VOLUME_CURRENT).ToDecimal();
                        order.Price = _mtApiClient.OrderGetDouble(ENUM_ORDER_PROPERTY_DOUBLE.ORDER_PRICE_OPEN).ToDecimal();

                        long orderSideType = _mtApiClient.OrderGetInteger(ENUM_ORDER_PROPERTY_INTEGER.ORDER_TYPE);

                        if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY)
                        {
                            order.Side = Side.Buy;
                            order.TypeOrder = OrderPriceType.Market;
                        }
                        else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL)
                        {
                            order.Side = Side.Sell;
                            order.TypeOrder = OrderPriceType.Market;
                        }
                        else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY_LIMIT)
                        {
                            order.Side = Side.Buy;
                            order.TypeOrder = OrderPriceType.Limit;
                        }
                        else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL_LIMIT)
                        {
                            order.Side = Side.Sell;
                            order.TypeOrder = OrderPriceType.Limit;
                        }

                        MyOrderEvent.Invoke(order);
                        return order.State;
                    }
                }

                int total = _mtApiClient.HistoryOrdersTotal();

                if (total == 0) return OrderStateType.None;

                bool isHistorySelect = _mtApiClient.HistorySelect(DateTime.Now.AddDays(-1), DateTime.Now);

                if (!isHistorySelect) return OrderStateType.None;

                for (int i = 0; i < total; i++)
                {
                    ulong ticketNumber = _mtApiClient.HistoryOrderGetTicket(i);

                    var magic = _mtApiClient.HistoryOrderGetInteger(ticketNumber, ENUM_ORDER_PROPERTY_INTEGER.ORDER_MAGIC);

                    if (magic != order.NumberUser) continue;

                    order.ServerType = ServerType.MetaTrader5;
                    order.SecurityNameCode = _mtApiClient.HistoryOrderGetString(ticketNumber, ENUM_ORDER_PROPERTY_STRING.ORDER_SYMBOL);
                    order.NumberMarket = _mtApiClient.HistoryOrderGetInteger(ticketNumber, ENUM_ORDER_PROPERTY_INTEGER.ORDER_TICKET).ToString();

                    long orderState = _mtApiClient.HistoryOrderGetInteger(ticketNumber, ENUM_ORDER_PROPERTY_INTEGER.ORDER_STATE);
                    order.State = GetOrderStateType(orderState);

                    long orderTime = _mtApiClient.HistoryOrderGetInteger(ticketNumber, ENUM_ORDER_PROPERTY_INTEGER.ORDER_TIME_SETUP);
                    order.TimeCallBack = TimeManager.GetDateTimeFromTimeStampSeconds(orderTime);
                    order.PortfolioNumber = _accountId;
                    order.Volume = _mtApiClient.HistoryOrderGetDouble(ticketNumber, ENUM_ORDER_PROPERTY_DOUBLE.ORDER_VOLUME_INITIAL).ToDecimal();
                    order.VolumeExecute = order.Volume - _mtApiClient.HistoryOrderGetDouble(ticketNumber, ENUM_ORDER_PROPERTY_DOUBLE.ORDER_VOLUME_CURRENT).ToDecimal();
                    order.Price = _mtApiClient.HistoryOrderGetDouble(ticketNumber, ENUM_ORDER_PROPERTY_DOUBLE.ORDER_PRICE_OPEN).ToDecimal();

                    long orderSideType = _mtApiClient.HistoryOrderGetInteger(ticketNumber, ENUM_ORDER_PROPERTY_INTEGER.ORDER_TYPE);

                    if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY)
                    {
                        order.Side = Side.Buy;
                        order.TypeOrder = OrderPriceType.Market;
                    }
                    else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL)
                    {
                        order.Side = Side.Sell;
                        order.TypeOrder = OrderPriceType.Market;
                    }
                    else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_BUY_LIMIT)
                    {
                        order.Side = Side.Buy;
                        order.TypeOrder = OrderPriceType.Limit;
                    }
                    else if ((ENUM_ORDER_TYPE)orderSideType == ENUM_ORDER_TYPE.ORDER_TYPE_SELL_LIMIT)
                    {
                        order.Side = Side.Sell;
                        order.TypeOrder = OrderPriceType.Limit;
                    }

                    MyOrderEvent.Invoke(order);
                    return order.State;
                }

                return OrderStateType.None;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error GetOrderStatus: {ex.ToString()}", LogMessageType.Error);
                return OrderStateType.None;
            }
        }

        #endregion

        #region 9 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region 10 Helpers

        private void SavePositionsInFile()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(PositionsCachePath, false))
                {
                    string data = CompressionUtils.Compress(_dictionaryOpenPositions.ToJson());
                    writer.WriteLine(data);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error SavePositionInFile: {ex.ToString()}", LogMessageType.Error);
            }
        }

        private void LoadPositionsFromFile()
        {
            try
            {
                if (!File.Exists(PositionsCachePath))
                {
                    return;
                }

                using (StreamReader reader = new StreamReader(PositionsCachePath))
                {
                    string data = CompressionUtils.Decompress(reader.ReadToEnd());
                    _dictionaryOpenPositions = JsonConvert.DeserializeObject<Dictionary<int, ulong>>(data);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error LoadPositionsFromFile: {ex.ToString()}", LogMessageType.Error);
            }
        }


        private OrderStateType GetOrderStateType(long status)
        {
            return status switch
            {
                0 => OrderStateType.Pending,
                1 => OrderStateType.Active,
                2 => OrderStateType.Cancel,
                3 => OrderStateType.Partial,
                4 => OrderStateType.Done,
                5 => OrderStateType.Fail,
                6 => OrderStateType.Cancel,
                7 => OrderStateType.Pending,
                8 => OrderStateType.Pending,
                9 => OrderStateType.Pending,
                _ => OrderStateType.None
            };
        }

        private bool CheckFilter(string symbol, ref SecurityType sectype)
        {
            try
            {
                string sectorName = _mtApiClient.SymbolInfoString(symbol, ENUM_SYMBOL_INFO_STRING.SYMBOL_SECTOR_NAME);
                string industryName = _mtApiClient.SymbolInfoString(symbol, ENUM_SYMBOL_INFO_STRING.SYMBOL_INDUSTRY_NAME);

                if (_useCurrency)
                {
                    if (sectorName == "Currency")
                    {
                        sectype = SecurityType.CurrencyPair;
                        return true;
                    }
                }

                if (_useFunds)
                {
                    if (sectorName == "Financial")
                    {
                        if (industryName == "Exchange Traded Fund")
                        {
                            sectype = SecurityType.Fund;
                            return true;
                        }
                        else
                        {
                            sectype = SecurityType.Stock;
                            return false;
                        }
                    }
                }

                if (_useMetals)
                {
                    if (sectorName == "Commodities")
                    {
                        if (industryName == "Commodities - Precious" || industryName == "Oil & Gas E&P" || industryName == "Undefined")
                        {
                            sectype = SecurityType.Commodities;
                            return true;
                        }
                        else
                        {
                            sectype = SecurityType.Stock;
                            return false;
                        }
                    }
                }

                if (_useIndexes)
                {
                    if (sectorName == "Indexes")
                    {
                        sectype = SecurityType.Index;
                        return true;
                    }
                }

                if (_useAnotherOne)
                {
                    sectype = SecurityType.Stock;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return false;
            }
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

        private void SetСonnected()
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                SendLogMessage("Connected.", LogMessageType.System);

                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent?.Invoke();
            }
        }

        public event Action<News> NewsEvent { add { } remove { } }

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion
    }
}
