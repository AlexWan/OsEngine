using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers.Tester;
using OsEngine.Market;
using System.Threading;
using OsEngine.Market.Servers;
using System.Globalization;
using System.IO;
using OsEngine.Indicators;

namespace OsEngine.Robots
{
    [Bot("Symphony")]
    public class Symphony : BotPanel
    {
        #region Service. Settings and modules

        public override string GetNameStrategyType()
        {
            return "Symphony";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabIndex _tabIndex;

        private BotTabScreener _tabScreener;

        public Symphony(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Index);
            _tabIndex = TabsIndex[0];
            _tabIndex.SpreadChangeEvent += _tabIndex_SpreadChangeEvent;

            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];
            _tabScreener.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            RegimeMain = CreateParameter("Regime. Main", "Off", new[] { "Off", "On" }, "Main Regime");
            RegimeTradingModule = CreateParameter("Trading  module", "Soldiers", new[] { "Soldiers", "Linear Regression", "Cointegration" }, "Main Regime");

            LastTimeCheckSecurities = CreateParameter("Last time check securities ", "", "Main Regime");
            AutoDelistingSecurities = CreateParameter("Auto delisting", true, "Main Regime");

            VolatilityLookBackDaysCountGetSecInTrade = CreateParameter("Days LookBack", 30, 1, 50, 4, "Auto-selection");
            TopVolumeSecRemovePercent = CreateParameter("Top vol. sec remove percent", 15m, 1, 50, 4, "Auto-selection");
            LowVolumeSecRemovePercent = CreateParameter("Low vol. sec remove percent", 15m, 1, 50, 4, "Auto-selection");
            LogIsOn = CreateParameter("Log is on", true, "Auto-selection");
            FullLogIsOn = CreateParameter("Full log is on", false, "Auto-selection");

            //Модуль 1. Вола чуть больше. Корреляция очень хорошая
            CorrelationMaxMd1 = CreateParameter("CorrelationMax", 1m, 1.0m, 50, 4, "Auto-selection");
            CorrelationMinMd1 = CreateParameter("CorrelationMin", 0.9m, 1.0m, 50, 4, "Auto-selection");
            VolatilityMaxMd1 = CreateParameter("Vol Diff Max", 1.4m, 1.0m, 50, 4, "Auto-selection");
            VolatilityMinMd1 = CreateParameter("Vol Diff Min", 1.1m, 1.0m, 50, 4, "Auto-selection");

            StrategyParameterButton button = CreateParameterButton("update manual", "Auto-selection");
            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;

            _moduleSoldiers = new TradeModuleThreeSoldiers(this, 1);
            _moduleLinearRegression = new TradeModuleLinearRegression(this, 2);
            _moduleCointegration = new TradeModuleCointegration(this, 3);

            if (StartProgram == StartProgram.IsTester)
            {
                TesterServer server = (TesterServer)ServerMaster.GetServers()[0];

                server.TestingStartEvent += Server_TestingStartEvent;
            }

            this.ParamGuiSettings.Height = 600;
            this.ParamGuiSettings.Width = 600;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                Thread worker = new Thread(DelistSecuritiesDeletion);
                worker.Start();
            }

            DeleteEvent += Symphony_DeleteEvent;
        }

        private void Symphony_DeleteEvent()
        {
            try
            {
                if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void Server_TestingStartEvent()
        {
            _timeLastCheckSecurities = DateTime.MinValue;

            _moduleSoldiers.SecuritiesToWatch.ValueString = "";
            _moduleSoldiers.TradeSettings.Clear();
            _moduleLinearRegression.SecuritiesToWatch.ValueString = "";
            _moduleCointegration.SecuritiesToWatch.ValueString = "";
            _moduleCointegration.TimeLastCloseByBadCorrelationArray.Clear();
        }

        public StrategyParameterString RegimeMain;

        public StrategyParameterString RegimeTradingModule;

        public StrategyParameterString LastTimeCheckSecurities;

        public StrategyParameterBool AutoDelistingSecurities;

        TradeModuleThreeSoldiers _moduleSoldiers;

        TradeModuleLinearRegression _moduleLinearRegression;

        TradeModuleCointegration _moduleCointegration;

        #endregion

        #region Processing of incoming events

        List<Candle> indexCandles;

        private void _tabIndex_SpreadChangeEvent(List<Candle> candles)
        {
            if (RegimeMain.ValueString == "Off")
            {
                return;
            }

            if (candles == null || candles.Count < 100)
            {
                return;
            }

            indexCandles = candles;

            CheckSecurities(candles[candles.Count - 1].TimeStart);

            if (RegimeTradingModule.ValueString == "Cointegration")
            {
                for (int i = 0; i < _tabScreener.Tabs.Count; i++)
                {
                    if (_tabScreener.Tabs[i].IsReadyToTrade == false)
                    {
                        continue;
                    }

                    List<Candle> candlesCur = null;

                    if (StartProgram == StartProgram.IsTester)
                    {
                        candlesCur = _tabScreener.Tabs[i].CandlesAll;
                    }
                    else if (StartProgram == StartProgram.IsOsTrader)
                    {
                        candlesCur = _tabScreener.Tabs[i].CandlesFinishedOnly;
                    }

                    if (candlesCur == null ||
                        candlesCur.Count < 100)
                    {
                        continue;
                    }

                    if (candlesCur[candlesCur.Count - 1].TimeStart != indexCandles[indexCandles.Count - 1].TimeStart)
                    {
                        continue;
                    }

                    _moduleCointegration.TryTrade(candlesCur, indexCandles, _tabScreener.Tabs[i], _tabScreener);
                }
            }
        }

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (RegimeMain.ValueString == "Off")
            {
                return;
            }

            if (candles == null || candles.Count < 100)
            {
                return;
            }

            if (indexCandles == null ||
                indexCandles.Count == 0)
            {
                return;
            }

            if (RegimeTradingModule.ValueString == "Soldiers")
            {
                _moduleSoldiers.TryTrade(candles, indexCandles, tab, _tabScreener);
            }
            else if (RegimeTradingModule.ValueString == "Linear Regression")
            {
                _moduleLinearRegression.TryTrade(candles, indexCandles, tab, _tabScreener);
            }
            else if (RegimeTradingModule.ValueString == "Cointegration")
            {
                if (candles[candles.Count - 1].TimeStart != indexCandles[indexCandles.Count - 1].TimeStart)
                {
                    return;
                }

                _moduleCointegration.TryTrade(candles, indexCandles, tab, _tabScreener);
            }
        }

        #endregion

        #region Deletion of delisted securities in the index

        private void DelistSecuritiesDeletion()
        {
            DateTime _lastTimeStartConnector = DateTime.MinValue;

            bool _alreadyCheckSecurities = false;

            while (true)
            {
                Thread.Sleep(5000);

                try
                {
                    // 0 on off
                    if (AutoDelistingSecurities.ValueBool == false)
                    {
                        continue;
                    }

                    // 1 различные проверки на наличие данных

                    if (_tabIndex.Tabs == null ||
                        _tabIndex.Tabs.Count == 0)
                    {
                        continue;
                    }

                    ServerType myServerInIndex = _tabIndex.Tabs[0].ServerType;

                    if (myServerInIndex == ServerType.None)
                    {
                        continue;
                    }

                    ServerType myServerInScreener = _tabScreener.ServerType;

                    if (myServerInScreener == ServerType.None)
                    {
                        continue;
                    }

                    List<IServer> allServers = ServerMaster.GetServers();

                    if (allServers == null
                        || allServers.Count == 0)
                    {
                        continue;
                    }

                    IServer myServerIndex = null;
                    IServer myserverScreener = null;

                    for (int i = 0; i < allServers.Count; i++)
                    {
                        if (allServers[i].ServerType == myServerInIndex)
                        {
                            myServerIndex = allServers[i];
                        }
                        if (allServers[i].ServerType == myServerInScreener)
                        {
                            myserverScreener = allServers[i];
                        }
                    }

                    if (myServerIndex == null
                        || myserverScreener == null)
                    {
                        continue;
                    }

                    // 2 ожидаем когда коннектор подключится

                    if (myServerIndex.ServerStatus == ServerConnectStatus.Disconnect
                        || myserverScreener.ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        _lastTimeStartConnector = DateTime.MinValue;
                        continue;
                    }

                    // 3 коннектор подключен

                    if (_lastTimeStartConnector == DateTime.MinValue)
                    {
                        _lastTimeStartConnector = DateTime.Now;
                        _alreadyCheckSecurities = false;
                        continue;
                    }

                    if (_lastTimeStartConnector.AddMinutes(10) > DateTime.Now)
                    {
                        continue;
                    }

                    // 4 через 10 минут после включения коннектора, доходим сюда

                    if (_alreadyCheckSecurities)
                    {
                        continue;
                    }

                    // 5 костыль. Проверяем чтобы более чем в 80 % бумаг были данные

                    decimal haveDataSourcesCount = 0;

                    for (int i = 0; i < _tabIndex.Tabs.Count; i++)
                    {
                        ConnectorCandles tab = _tabIndex.Tabs[i];

                        List<Candle> candles = tab.Candles(true);
                        decimal bestAsk = tab.BestAsk;
                        decimal bestBid = tab.BestBid;

                        if (candles != null &&
                            candles.Count > 0)
                        {
                            haveDataSourcesCount++;
                        }
                    }

                    if (haveDataSourcesCount
                        / (Convert.ToDecimal(_tabIndex.Tabs.Count) / 100m)
                        < 80)
                    {
                        // менее 80% бумаг подключены. Переносим данную проверку на 5ть минут.
                        _lastTimeStartConnector = DateTime.Now.AddMinutes(-5);
                        continue;
                    }


                    // 6 проверка бумаг в индексе на наличие свечей.

                    _alreadyCheckSecurities = true;

                    bool haveDelisting = false;

                    for (int i = 0; i < _tabIndex.Tabs.Count; i++)
                    {
                        ConnectorCandles tab = _tabIndex.Tabs[i];

                        List<Candle> candles = tab.Candles(true);
                        decimal bestAsk = tab.BestAsk;
                        decimal bestBid = tab.BestBid;

                        if (candles == null ||
                            candles.Count == 0)
                        {
                            // делистинг
                            SendNewLogMessage("Security auto-delete from INDEX: " + tab.SecurityName, LogMessageType.Error);
                            _tabIndex.DeleteSecurityTab(i);
                            haveDelisting = true;
                            i--;
                        }
                    }

                    if (haveDelisting)
                    {
                        _tabIndex.AutoFormulaBuilder.RebuildHard();
                    }

                    for (int i = 0; i < _tabScreener.Tabs.Count; i++)
                    {
                        BotTabSimple tab = _tabScreener.Tabs[i];

                        List<Candle> candles = tab.Connector.Candles(true);
                        decimal bestAsk = tab.Connector.BestAsk;
                        decimal bestBid = tab.Connector.BestBid;

                        if ((candles == null ||
                            candles.Count == 0) &&
                            bestAsk == 0 &&
                            bestBid == 0)
                        {
                            // делистинг
                            SendNewLogMessage("Security auto-delete from SCREENER: " + tab.Connector.SecurityName, LogMessageType.Error);

                            _tabScreener.Tabs.RemoveAt(i);
                            tab.Delete();
                            i--;
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        #endregion

        #region Selection of securities to trade by module

        public StrategyParameterInt VolatilityLookBackDaysCountGetSecInTrade;
        public StrategyParameterDecimal TopVolumeSecRemovePercent;
        public StrategyParameterDecimal LowVolumeSecRemovePercent;
        public StrategyParameterBool LogIsOn;
        public StrategyParameterBool FullLogIsOn;

        public StrategyParameterDecimal CorrelationMaxMd1;
        public StrategyParameterDecimal CorrelationMinMd1;
        public StrategyParameterDecimal VolatilityMaxMd1;
        public StrategyParameterDecimal VolatilityMinMd1;

        private DateTime _timeLastCheckSecurities;

        private bool _alreadyTryToLoadTime = false;

        private void Button_UserClickOnButtonEvent()
        {
            try
            {
                _timeLastCheckSecurities = DateTime.MinValue;
                LastTimeCheckSecurities.ValueString = "";

                indexCandles = _tabIndex.Candles;

                CheckSecurities(ServerMaster.GetServers()[0].ServerTime);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void CheckSecurities(DateTime time)
        {
            if (_timeLastCheckSecurities == DateTime.MinValue &&
                StartProgram == StartProgram.IsOsTrader &&
                _alreadyTryToLoadTime == false)
            {
                _alreadyTryToLoadTime = true;
                try
                {
                    _timeLastCheckSecurities = Convert.ToDateTime(LastTimeCheckSecurities.ValueString);
                }
                catch
                {

                }
            }

            if (_timeLastCheckSecurities.AddDays(1) > time)
            {
                if (FullLogIsOn.ValueBool == true)
                {
                    SendNewLogMessage("Cant check secs. Reason 1", LogMessageType.Error);
                }

                return;
            }

            if (_timeLastCheckSecurities != DateTime.MinValue &&
                time.Hour != 11)
            {
                if (FullLogIsOn.ValueBool == true)
                {
                    SendNewLogMessage("Cant check secs. Reason 2", LogMessageType.Error);
                }
                return;
            }

            _timeLastCheckSecurities = time;
            LastTimeCheckSecurities.ValueString = time.ToShortDateString();

            if (indexCandles == null ||
                indexCandles.Count < 100)
            {
                if (FullLogIsOn.ValueBool == true)
                {
                    SendNewLogMessage("Cant check secs. Reason 3", LogMessageType.Error);
                }
                return;
            }

            bool haveOnePaper = false;

            for (int i = 0; i < _tabScreener.Tabs.Count; i++)
            {
                if (_tabScreener.Tabs[i].Securiti != null)
                {
                    haveOnePaper = true;
                    break;
                }
            }

            if (haveOnePaper == false)
            {
                if (FullLogIsOn.ValueBool == true)
                {
                    SendNewLogMessage("Cant check secs. Reason 4", LogMessageType.Error);
                }
                return;
            }

            for (int i = 0; i < _tabScreener.Tabs.Count; i++)
            {
                if (_tabScreener.Tabs[i].Securiti != null)
                {
                    List<Candle> readyCandles = _tabScreener.Tabs[i].CandlesFinishedOnly;

                    if (readyCandles == null ||
                        readyCandles.Count < 100)
                    {
                        if (FullLogIsOn.ValueBool == true)
                        {
                            SendNewLogMessage("Cant check secs. One of securities len < 100 ", LogMessageType.Error);
                        }

                        return;
                    }
                }
            }

            _moduleSoldiers.SecuritiesToWatch.ValueString = "";
            _moduleLinearRegression.SecuritiesToWatch.ValueString = "";
            _moduleCointegration.SecuritiesToWatch.ValueString = "";

            // берём все вкладки и считаем объёмы

            List<SecuritiesByVolume> topSecuritiesByVolume = new List<SecuritiesByVolume>();

            for (int i = 0; i < _tabScreener.Tabs.Count; i++)
            {
                SecuritiesByVolume sec = new SecuritiesByVolume();
                sec.Tab = _tabScreener.Tabs[i];

                if (StartProgram == StartProgram.IsTester)
                {
                    sec.Candles = _tabScreener.Tabs[i].CandlesAll;
                }
                else if (StartProgram == StartProgram.IsOsTrader)
                {
                    sec.Candles = _tabScreener.Tabs[i].CandlesFinishedOnly;
                }

                sec.CalculateVolume(VolatilityLookBackDaysCountGetSecInTrade.ValueInt);

                topSecuritiesByVolume.Add(sec);
            }

            // сортируем объёмы

            for (int i = 0; i < topSecuritiesByVolume.Count; i++)
            {
                for (int i2 = 1; i2 < topSecuritiesByVolume.Count; i2++)
                {
                    // Пузырик
                    if (topSecuritiesByVolume[i2].VolumeByDays >
                        topSecuritiesByVolume[i2 - 1].VolumeByDays)
                    {
                        SecuritiesByVolume sec = topSecuritiesByVolume[i2];
                        topSecuritiesByVolume[i2] = topSecuritiesByVolume[i2 - 1];
                        topSecuritiesByVolume[i2 - 1] = sec;
                    }
                }
            }

            // сортируем бумаги по модулям

            List<string> securitiesTm1 = new List<string>();

            List<string> securitiesCloseModule = new List<string>();

            List<string> logs = new List<string>();

            logs.Add(time.ToShortDateString());

            decimal countSecInTrade = 0;

            decimal volaDay = GetVolatilityInDay(indexCandles, VolatilityLookBackDaysCountGetSecInTrade.ValueInt);

            int firstIndexByVolumeTopLow = 0;
            int lastIndexByVolumeTopLow = topSecuritiesByVolume.Count;

            if (TopVolumeSecRemovePercent.ValueDecimal > 0)
            {
                firstIndexByVolumeTopLow =
                    Convert.ToInt32(TopVolumeSecRemovePercent.ValueDecimal * (topSecuritiesByVolume.Count / 100m));
            }

            if (LowVolumeSecRemovePercent.ValueDecimal > 0)
            {
                lastIndexByVolumeTopLow =
                    topSecuritiesByVolume.Count
                    - Convert.ToInt32(LowVolumeSecRemovePercent.ValueDecimal * (topSecuritiesByVolume.Count / 100m));
            }

            for (int i = 0; i < topSecuritiesByVolume.Count; i++)
            {
                BotTabSimple curTab = topSecuritiesByVolume[i].Tab;

                if (topSecuritiesByVolume[i].Tab.Securiti == null)
                {
                    if (string.IsNullOrEmpty(curTab.Connector.SecurityName) == false)
                    {
                        securitiesCloseModule.Add(curTab.Connector.SecurityName);
                    }
                    continue;
                }

                if (i < firstIndexByVolumeTopLow
                    || i >= lastIndexByVolumeTopLow)
                {
                    string logMessag = curTab.Securiti.Name;
                    logMessag += "   Go: close module";
                    securitiesCloseModule.Add(curTab.Securiti.Name);
                    logs.Add(logMessag);
                    continue;
                }

                string logMessage = curTab.Securiti.Name;

                List<Candle> candles = null;

                if (StartProgram == StartProgram.IsTester)
                {
                    candles = curTab.CandlesFinishedOnly;
                }
                else if (StartProgram == StartProgram.IsOsTrader)
                {
                    candles = curTab.CandlesAll;
                }


                decimal correlation = GetCorrelation(candles, indexCandles);

                logMessage += "  Corr: " + Math.Round(correlation, 5);

                decimal volaDiff = GetVolatilityDiff(candles, indexCandles, VolatilityLookBackDaysCountGetSecInTrade.ValueInt);

                logMessage += "  Vola: " + Math.Round(volaDiff, 5);

                // public StrategyParameterDecimal CorrelationMaxMd1;
                // public StrategyParameterDecimal CorrelationMinMd1;
                // public StrategyParameterDecimal VolatilityMaxMd1;
                // public StrategyParameterDecimal VolatilityMinMd1;

                if (correlation >= CorrelationMinMd1.ValueDecimal &&
                    correlation <= CorrelationMaxMd1.ValueDecimal &&
                    volaDiff >= VolatilityMinMd1.ValueDecimal &&
                    volaDiff <= VolatilityMaxMd1.ValueDecimal)
                {
                    securitiesTm1.Add(curTab.Securiti.Name);
                    logMessage += "   Go: 1 module";
                    countSecInTrade++;
                }
                else
                {
                    securitiesCloseModule.Add(curTab.Securiti.Name);
                    logMessage += "   Go: close module";
                }

                logs.Add(logMessage);
            }


            if (LogIsOn.ValueBool)
            {
                string logMessage = "";
                logMessage += logs[0] + "\n";
                logMessage += "Count Sec in Trade = " + countSecInTrade + "\n";
                logMessage += "Index vola percent = " + Math.Round(volaDay, 2) + "\n";

                _tabScreener.Tabs[0].SetNewLogMessage(logMessage, LogMessageType.Error);
            }

            string res = "";

            _moduleSoldiers.SecuritiesToWatch.ValueString = "";
            _moduleLinearRegression.SecuritiesToWatch.ValueString = "";
            _moduleCointegration.SecuritiesToWatch.ValueString = "";

            res = "";
            for (int i = 0; i < securitiesTm1.Count; i++)
            {
                res += securitiesTm1[i] + "_";
            }

            if (RegimeTradingModule.ValueString == "Soldiers")
            {
                _moduleSoldiers.SecuritiesToWatch.ValueString = res;
            }
            else if (RegimeTradingModule.ValueString == "Linear Regression")
            {
                _moduleLinearRegression.SecuritiesToWatch.ValueString = res;
            }
            else if (RegimeTradingModule.ValueString == "Cointegration")
            {
                _moduleCointegration.SecuritiesToWatch.ValueString = res;
            }
        }

        CorrelationBuilder _correlationBuilder = new CorrelationBuilder();

        private decimal GetCorrelation(List<Candle> sec, List<Candle> index)
        {
            int curDay = sec[sec.Count - 1].TimeStart.Day;
            int daysCount = 1;
            int candlesCount = 1;


            for (int i = sec.Count - 1; i > 0; i--)
            {
                Candle curCandle = sec[i];
                candlesCount++;

                if (curDay != curCandle.TimeStart.Day)
                {
                    curDay = sec[i].TimeStart.Day;
                    daysCount++;

                    if (VolatilityLookBackDaysCountGetSecInTrade.ValueInt == daysCount)
                    {
                        break;
                    }
                }

            }

            PairIndicatorValue value = _correlationBuilder.ReloadCorrelationLast(sec, index, candlesCount);

            if (value == null)
            {
                return 0;
            }

            return value.Value;
        }

        private decimal GetVolatilityDiff(List<Candle> sec, List<Candle> index, int len)
        {

            // волатильность. Берём внутридневную волу за месяц в % по бумаге(V1) и по индексу(V2)
            // делим V1 / V2 - получаем отношение волатильности бумаги к индексу. 
            // среднюю - в первый торговый модуль
            // чуть повышенную - во второй
            // чуть пониженную - в третий

            decimal volSec = GetVolatilityInDay(sec, len);
            decimal volIndex = GetVolatilityInDay(index, len);

            if (volIndex == 0)
            {
                return 1;
            }

            decimal result = volSec / volIndex;

            return result;
        }

        private decimal GetVolatilityInDay(List<Candle> candles, int len)
        {


            List<decimal> curDaysVola = new List<decimal>();

            decimal curMinInDay = decimal.MaxValue;
            decimal curMaxInDay = 0;
            int curDay = candles[candles.Count - 1].TimeStart.Day;
            int daysCount = 1;


            for (int i = candles.Count - 1; i > 0; i--)
            {
                Candle curCandle = candles[i];

                if (curDay != curCandle.TimeStart.Day)
                {
                    if (curMaxInDay != 0 &&
                        curMinInDay != decimal.MaxValue)
                    {
                        decimal moveInDay = curMaxInDay - curMinInDay;
                        decimal percentMove = moveInDay / (curMinInDay / 100);
                        curDaysVola.Add(percentMove);
                    }

                    curMinInDay = decimal.MaxValue;
                    curMaxInDay = 0;
                    curDay = candles[i].TimeStart.Day;

                    daysCount++;

                    if (len == daysCount)
                    {
                        break;
                    }
                }

                if (curCandle.High > curMaxInDay)
                {
                    curMaxInDay = curCandle.High;
                }
                if (curCandle.Low < curMinInDay)
                {
                    curMinInDay = curCandle.Low;
                }
            }

            if (curDaysVola.Count == 0)
            {
                return 0;
            }

            decimal result = 0;

            for (int i = 0; i < curDaysVola.Count; i++)
            {
                result += curDaysVola[i];
            }

            return result / curDaysVola.Count;
        }

        #endregion
    }

    public class SecuritiesByVolume
    {
        public BotTabSimple Tab;

        public List<Candle> Candles;

        public decimal VolumeByDays;

        public void CalculateVolume(int daysCount)
        {
            if (Candles == null || Candles.Count == 0)
            {
                return;
            }

            int curDay = Candles[Candles.Count - 1].TimeStart.Day;
            int curDaysCount = 1;
            decimal volume = 0;

            for (int i = Candles.Count - 1; i > 0; i--)
            {
                Candle curCandle = Candles[i];
                volume += curCandle.Volume * curCandle.Open;

                if (curDay != curCandle.TimeStart.Day)
                {
                    curDay = Candles[i].TimeStart.Day;
                    curDaysCount++;

                    if (daysCount == curDaysCount)
                    {
                        break;
                    }
                }
            }

            if (Tab.Securiti.Lot > 1)
            {
                volume = volume * Tab.Securiti.Lot;
            }

            VolumeByDays = volume;
        }
    }

    public class TradeModuleCointegration
    {
        public StrategyParameterString Regime;

        public StrategyParameterString SecuritiesToWatch;

        public StrategyParameterInt CorrelationLookBack;
        public StrategyParameterInt CointegrationLookBack;
        public StrategyParameterDecimal CointegrationLinesMult;
        public StrategyParameterDecimal MinCorrelationToEntry;
        public StrategyParameterDecimal MinCorrelationToExit;
        public StrategyParameterDecimal LossPercentToExit;

        public StrategyParameterDecimal VolumeOnPositionMain;
        public StrategyParameterInt MaxPositionsCount;
        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterString OrdersType;
        public StrategyParameterInt IcebergSecondsBetweenOrders;
        public StrategyParameterInt IcebergCount;
        public StrategyParameterDecimal SlippagePercent;

        private StrategyParameterTimeOfDay TimeStart;
        private StrategyParameterTimeOfDay TimeEnd;

        public StrategyParameterBool TimeDeleyFilterIsOn;
        public StrategyParameterInt TimeDeleyFilterCount;

        public StrategyParameterBool SmaFilterOnSecurityIsOn;
        public StrategyParameterBool SmaFilterOnIndexIsOn;
        public StrategyParameterInt SmaFilterOnSecurityLen;
        public StrategyParameterInt SmaFilterOnIndexLen;

        public TradeModuleCointegration(BotPanel bot, int moduleNum)
        {
            Regime = bot.CreateParameter("Regime. " + moduleNum, "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" }, "Cointegration");
            VolumeOnPositionMain = bot.CreateParameter("Volume mult " + moduleNum, 1, 1.0m, 50, 4, "Cointegration");
            MaxPositionsCount = bot.CreateParameter("Max positions count" + moduleNum, 1, 1, 50, 4, "Cointegration");
            TradeAssetInPortfolio = bot.CreateParameter("Asset in portfolio" + moduleNum, "Prime", "Cointegration");

            SecuritiesToWatch = bot.CreateParameter("Securities " + moduleNum, "", "Cointegration");

            CorrelationLookBack = bot.CreateParameter("Correlation Look Back " + moduleNum, 50, 20, 500, 1, "Cointegration");
            CointegrationLookBack = bot.CreateParameter("Cointegration Look Back " + moduleNum, 150, 20, 500, 1, "Cointegration");
            CointegrationLinesMult = bot.CreateParameter("Cointegration Lines Mult " + moduleNum, 1.2m, 1, 5, 1, "Cointegration");
            MinCorrelationToEntry = bot.CreateParameter("Min Correlation To Entry " + moduleNum, 0.7m, 1.0m, 50, 4, "Cointegration");
            MinCorrelationToExit = bot.CreateParameter("Min Correlation To Exit " + moduleNum, -0.6m, 1.0m, 50, 4, "Cointegration");

            LossPercentToExit = bot.CreateParameter("Loss percent To Exit " + moduleNum, -5m, 1.0m, 50, 4, "Cointegration");

            TimeDeleyFilterIsOn = bot.CreateParameter("Extra filters stop trade " + moduleNum, true, "Cointegration");
            TimeDeleyFilterCount = bot.CreateParameter("Candles extra filters stop trade" + moduleNum, 30, 10, 500, 1, "Cointegration");

            if (bot.StartProgram == StartProgram.IsOsTrader)
            {
                OrdersType = bot.CreateParameter("Orders type " + moduleNum, "Market", new[] { "Market", "Limit", "Iceberg Market" }, "Cointegration");
                IcebergCount = bot.CreateParameter("Iceberg count " + moduleNum, 5, 1, 50, 4, "Cointegration");
                IcebergSecondsBetweenOrders = bot.CreateParameter("Iceberg seconds between orders " + moduleNum, 5, 1, 50, 4, "Cointegration");
            }
            else
            {
                OrdersType = bot.CreateParameter("Orders type" + moduleNum, "Market", new[] { "Market", "Limit" }, "Cointegration");
            }

            SlippagePercent = bot.CreateParameter("Slippage Percent" + moduleNum, 0, 1.0m, 50, 4, "Cointegration");
           

            TimeStart = bot.CreateParameterTimeOfDay("Start Trade Time" + moduleNum, 0, 0, 0, 0, "Cointegration");
            TimeEnd = bot.CreateParameterTimeOfDay("End Trade Time" + moduleNum, 24, 0, 0, 0, "Cointegration");

            SmaFilterOnSecurityIsOn = bot.CreateParameter("Sma Filter On Security IsOn" + moduleNum, false, "Cointegration");
            SmaFilterOnSecurityLen = bot.CreateParameter("Sma Filter On Security Len" + moduleNum, 50, 20, 500, 1, "Cointegration");

            SmaFilterOnIndexIsOn = bot.CreateParameter("Sma Filter On Index IsOn" + moduleNum, false, "Cointegration");
            SmaFilterOnIndexLen = bot.CreateParameter("Sma Filter On Index Len" + moduleNum, 50, 20, 500, 1, "Cointegration");
        }

        private CorrelationBuilder _correlationBuilder = new CorrelationBuilder();

        private CointegrationBuilder _cointegrationBuilder = new CointegrationBuilder();

        public void TryTrade(List<Candle> _candlesTradeSec,
            List<Candle> _candlesIndex, BotTabSimple tab, BotTabScreener screener)
        {
            try
            {
                if (_candlesIndex == null ||
                    _candlesIndex.Count < 10)
                {
                    return;
                }

                if (_candlesTradeSec == null ||
                    _candlesTradeSec.Count < 10)
                {
                    return;
                }

                if (Regime.ValueString == "Off")
                {
                    List<Position> positions = tab.PositionsOpenAll;

                    for (int i = 0; i < positions.Count; i++)
                    {
                        if (positions[i].State == PositionStateType.Open)
                        {
                            tab.CloseAtMarket(positions[i], positions[i].OpenVolume);
                        }
                    }

                    return;
                }

                if (_candlesIndex[_candlesIndex.Count - 1].TimeStart
                    != _candlesTradeSec[_candlesTradeSec.Count - 1].TimeStart)
                {
                    return;
                }

                if (TimeStart.Value > tab.TimeServerCurrent ||
                    TimeEnd.Value < tab.TimeServerCurrent)
                {
                    return;
                }

                // рассчитываем корреляцию и коинтеграцию

                PairIndicatorValue correlation = _correlationBuilder.ReloadCorrelationLast(_candlesIndex, _candlesTradeSec, CorrelationLookBack.ValueInt);

                if (correlation == null)
                {
                    return;
                }

                _cointegrationBuilder.CointegrationLookBack = CointegrationLookBack.ValueInt;
                _cointegrationBuilder.CointegrationDeviation = CointegrationLinesMult.ValueDecimal;

                _cointegrationBuilder.ReloadCointegration(_candlesIndex, _candlesTradeSec, false);

                if (_cointegrationBuilder.Cointegration == null
                    || _cointegrationBuilder.Cointegration.Count == 0)
                {
                    return;
                }

                decimal correlationLast = correlation.Value;
                decimal cointegrationLast = _cointegrationBuilder.Cointegration[_cointegrationBuilder.Cointegration.Count - 1].Value;
                CointegrationLineSide coinSide = _cointegrationBuilder.SideCointegrationValue;

                decimal lvlUp = _cointegrationBuilder.LineUpCointegration;
                decimal lvlDown = _cointegrationBuilder.LineDownCointegration;
                decimal curPriceIndx = cointegrationLast;

                if (lvlUp == 0 ||
                    lvlDown == 0 ||
                    curPriceIndx == 0)
                {
                    return;
                }

                List<Position> openPositions = tab.PositionsOpenAll;

                if (openPositions == null || openPositions.Count == 0)
                {
                    string sec = tab.Securiti.Name;

                    if (SecuritiesToWatch.ValueString.Contains(sec) == false)
                    {// выходим, если бумага не установлена в торговую для модуля
                        return;
                    }

                    if (screener.PositionsOpenAll.Count >= MaxPositionsCount.ValueInt)
                    {
                        return;
                    }

                    LogicOpenPosition(tab, screener, _candlesTradeSec, 
                        _candlesIndex,curPriceIndx,lvlUp,lvlDown,correlationLast);
                }
                else
                {
                    LogicClosePosition(tab,lvlDown,lvlUp,correlationLast,curPriceIndx);
                }

            }
            catch (Exception error)
            {
                tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void LogicOpenPosition(
            BotTabSimple tab,
            BotTabScreener screener,
            List<Candle> _candlesTradeSec,
            List<Candle> _candlesIndex,
            decimal curPriceIndx,
            decimal lvlUp,
            decimal lvlDown,
            decimal correlationLast)
        {
            if (tab.PositionsOpenAll.Count != 0)
            {
                return;
            }

            if (TimeDeleyFilterIsOn.ValueBool == true)
            {
                BadCorrelationCloseTime myTime = null;

                for (int i = 0; i < TimeLastCloseByBadCorrelationArray.Count; i++)
                {
                    if (TimeLastCloseByBadCorrelationArray[i].SecurityName == tab.Securiti.Name)
                    {
                        myTime = TimeLastCloseByBadCorrelationArray[i];
                        break;
                    }
                }

                if (myTime != null && myTime.Time > tab.TimeServerCurrent)
                {
                    return;
                }
            }


            // SHORT
            if (curPriceIndx > lvlUp
                && Regime.ValueString != "OnlyLong"
                && screener.PositionsOpenAll.Count < MaxPositionsCount.ValueInt
                )
            {
                bool filterIsNormal = true;

                if (MinCorrelationToEntry.ValueDecimal >= correlationLast)
                {
                    filterIsNormal = false;
                }

                if (SmaFilterOnSecurityIsOn.ValueBool == true)
                {
                    decimal smaValue = Sma(_candlesTradeSec, SmaFilterOnSecurityLen.ValueInt, _candlesTradeSec.Count - 1);
                    decimal smaPrev = Sma(_candlesTradeSec, SmaFilterOnSecurityLen.ValueInt, _candlesTradeSec.Count - 2);

                    if (smaValue > smaPrev)
                    {
                        filterIsNormal = false;
                    }
                }

                if (SmaFilterOnIndexIsOn.ValueBool == true)
                {
                    decimal smaValue = Sma(_candlesIndex, SmaFilterOnIndexLen.ValueInt, _candlesIndex.Count - 1);
                    decimal smaPrev = Sma(_candlesIndex, SmaFilterOnIndexLen.ValueInt, _candlesIndex.Count - 2);

                    if (smaValue > smaPrev)
                    {
                        filterIsNormal = false;
                    }
                }

                if (filterIsNormal == true)
                {// продажа
                    CreateShort(tab);
                }
            }

            // LONG
            if (curPriceIndx < lvlDown
               && Regime.ValueString != "OnlyShort"
               && screener.PositionsOpenAll.Count < MaxPositionsCount.ValueInt)
            {
                bool filterIsNormal = true;

                if (MinCorrelationToEntry.ValueDecimal >= correlationLast)
                {
                    filterIsNormal = false;
                }

                if (SmaFilterOnSecurityIsOn.ValueBool == true)
                {
                    decimal smaValue = Sma(_candlesTradeSec, SmaFilterOnSecurityLen.ValueInt, _candlesTradeSec.Count - 1);
                    decimal smaPrev = Sma(_candlesTradeSec, SmaFilterOnSecurityLen.ValueInt, _candlesTradeSec.Count - 2);

                    if (smaValue < smaPrev)
                    {
                        filterIsNormal = false;
                    }
                }

                if (SmaFilterOnIndexIsOn.ValueBool == true)
                {
                    decimal smaValue = Sma(_candlesIndex, SmaFilterOnIndexLen.ValueInt, _candlesIndex.Count - 1);
                    decimal smaPrev = Sma(_candlesIndex, SmaFilterOnIndexLen.ValueInt, _candlesIndex.Count - 2);

                    if (smaValue < smaPrev)
                    {
                        filterIsNormal = false;
                    }
                }

                if (filterIsNormal == true)
                {// покупка
                    CreateLong(tab);
                }
            }
        }

        private void LogicClosePosition(
            BotTabSimple tab,
            decimal lvlDown,
            decimal lvlUp,
            decimal correlationLast,
            decimal curPriceIndx)
        {
            List<Position> positionThird = tab.PositionsOpenAll;

            for (int i = 0; i < positionThird.Count; i++)
            {
                Position curPos = positionThird[i];

                if (MinCorrelationToExit.ValueDecimal >= correlationLast
                    && curPos.ProfitPortfolioPunkt < 0)
                {
                    // выход по раскорреляции инструментов
                    ClosePosition(curPos, tab);

                    if (TimeDeleyFilterIsOn.ValueBool == true)
                    {
                        BadCorrelationCloseTime myTime = null;

                        for (int i2 = 0; i2 < TimeLastCloseByBadCorrelationArray.Count; i2++)
                        {
                            if (TimeLastCloseByBadCorrelationArray[i].SecurityName == tab.Securiti.Name)
                            {
                                myTime = TimeLastCloseByBadCorrelationArray[i];
                                break;
                            }
                        }

                        if (myTime == null)
                        {
                            myTime = new BadCorrelationCloseTime();
                            myTime.SecurityName = tab.Securiti.Name;
                            TimeLastCloseByBadCorrelationArray.Add(myTime);
                        }

                        myTime.Time = tab.TimeServerCurrent.AddMinutes(TimeDeleyFilterCount.ValueInt * tab.Connector.TimeFrameTimeSpan.TotalMinutes);
                    }

                    return;
                }

                decimal profitPercent = (tab.PriceBestAsk - curPos.EntryPrice) / (curPos.EntryPrice / 100);

                if (profitPercent < LossPercentToExit.ValueDecimal)
                {
                    ClosePosition(curPos, tab);

                    if (TimeDeleyFilterIsOn.ValueBool == true)
                    {
                        BadCorrelationCloseTime myTime = null;

                        for (int i2 = 0; i2 < TimeLastCloseByBadCorrelationArray.Count; i2++)
                        {
                            if (TimeLastCloseByBadCorrelationArray[i].SecurityName == tab.Securiti.Name)
                            {
                                myTime = TimeLastCloseByBadCorrelationArray[i];
                                break;
                            }
                        }

                        if (myTime == null)
                        {
                            myTime = new BadCorrelationCloseTime();
                            myTime.SecurityName = tab.Securiti.Name;
                            TimeLastCloseByBadCorrelationArray.Add(myTime);
                        }

                        myTime.Time = tab.TimeServerCurrent.AddMinutes(TimeDeleyFilterCount.ValueInt * tab.Connector.TimeFrameTimeSpan.TotalMinutes);
                    }

                    return;
                }

                if (curPos.Direction == Side.Buy &&
                    curPriceIndx > lvlUp )
                {
                    ClosePosition(curPos, tab);
                }
                if (curPos.Direction == Side.Sell &&
                    curPriceIndx < lvlDown)
                {
                    ClosePosition(curPos, tab);
                }
            }
        }

        private void CreateShort(BotTabSimple tab)
        {
            decimal volumeToOpen = GetVolume(tab);

            List<Position> positions = tab.PositionsOpenAll;

            if (positions.Count > 1)
            {
                return;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                Position pos = positions[i];

                if (pos.Direction == Side.Buy)
                {
                    return;
                }

                if (pos.OpenActiv
                    || pos.State == PositionStateType.Opening)
                {// уже открываемся где-то
                    return; // выходим из метода
                }

                if (pos.State == PositionStateType.Closing
                    || pos.CloseActiv)
                {
                    continue;
                }
            }

            // покупаем

            if (OrdersType.ValueString == "Market")
            {
                tab.SellAtMarket(volumeToOpen);
            }
            else if (OrdersType.ValueString == "Limit")
            {
                decimal price = tab.PriceBestBid;

                decimal slippage = price * (SlippagePercent.ValueDecimal / 100);

                tab.SellAtLimit(volumeToOpen, price + slippage);
            }
            else if (OrdersType.ValueString == "Iceberg Market")
            {
                IcebergMaker icebergMaker = new IcebergMaker();
                icebergMaker.VolumeOnAllOrders = volumeToOpen;
                icebergMaker.OrdersCount = IcebergCount.ValueInt;
                icebergMaker.SecondsBetweenOrders = IcebergSecondsBetweenOrders.ValueInt;
                icebergMaker.Tab = tab;
                icebergMaker.Side = Side.Sell;
                icebergMaker.Start();
            }
        }

        private void CreateLong(BotTabSimple tab)
        {
            decimal volumeToOpen = GetVolume(tab);

            List<Position> positions = tab.PositionsOpenAll;

            if (positions.Count > 1)
            {
                return;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                Position pos = positions[i];

                if (pos.Direction == Side.Sell)
                {
                    return;
                }

                if (pos.OpenActiv
                    || pos.State == PositionStateType.Opening)
                {// уже открываемся где-то
                    return; // выходим из метода
                }

                if (pos.State == PositionStateType.Closing
                    || pos.CloseActiv)
                {
                    continue;
                }
            }

            // продаём

            if (OrdersType.ValueString == "Market")
            {
                tab.BuyAtMarket(volumeToOpen);
            }
            else if (OrdersType.ValueString == "Limit")
            {
                decimal price = tab.PriceBestBid;

                decimal slippage = price * (SlippagePercent.ValueDecimal / 100);

                tab.BuyAtLimit(volumeToOpen, price - slippage);
            }
            else if (OrdersType.ValueString == "Iceberg Market")
            {
                IcebergMaker icebergMaker = new IcebergMaker();
                icebergMaker.VolumeOnAllOrders = volumeToOpen;
                icebergMaker.OrdersCount = IcebergCount.ValueInt;
                icebergMaker.SecondsBetweenOrders = IcebergSecondsBetweenOrders.ValueInt;
                icebergMaker.Tab = tab;
                icebergMaker.Side = Side.Buy;
                icebergMaker.Start();
            }
        }

        private void ClosePosition(Position pos, BotTabSimple tabSimple)
        {
            if (pos.State == PositionStateType.Closing
                || pos.CloseActiv == true)
            {
                return;
            }
            if (OrdersType.ValueString == "Iceberg Market")
            {
                IcebergMaker icebergMaker = new IcebergMaker();
                icebergMaker.OrdersCount = IcebergCount.ValueInt;
                icebergMaker.SecondsBetweenOrders = IcebergSecondsBetweenOrders.ValueInt;
                icebergMaker.Tab = tabSimple;
                icebergMaker.PositionToClose = pos;
                icebergMaker.Start();
            }
            else
            {
                if (pos.Direction == Side.Buy)
                {
                    tabSimple.CloseAtMarket(pos, pos.OpenVolume);
                }
                if (pos.Direction == Side.Sell)
                {
                    tabSimple.CloseAtMarket(pos, pos.OpenVolume);
                }
            }
        }

        public decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = VolumeOnPositionMain.ValueDecimal;
            decimal contractPrice = tab.PriceBestAsk;

            if (tab.StartProgram == StartProgram.IsOsTrader) // "% от депозита для Бинанс или MOEX"
            {
                CheckPortfolioAutoDate(tab);

                return Math.Round((AllPortfolioValue * (VolumeOnPositionMain.ValueDecimal / 100)) / contractPrice / tab.Securiti.Lot,
                   tab.Securiti.DecimalsVolume);
            }
            else if (tab.StartProgram == StartProgram.IsTester) // "% от депозита"
            {
                return Math.Round(tab.Portfolio.ValueCurrent * (volume / 100) / tab.PriceBestAsk / tab.Securiti.Lot, 12);
            }
            else
            {
                return volume;
            }
        }

        decimal AllPortfolioValue = 0m;

        private void CheckPortfolioAutoDate(BotTabSimple tab)
        {

            Portfolio portfolio = tab.Portfolio;

            if (portfolio == null)
            {
                return;
            }

            List<PositionOnBoard> poses = portfolio.GetPositionOnBoard();

            if (poses == null)
            {
                return;
            }

            string secName = TradeAssetInPortfolio.ValueString;

            if (secName == "Prime")
            {
                AllPortfolioValue = portfolio.ValueCurrent;
                return;
            }

            decimal result;
            if (decimal.TryParse(secName, out result))
            {
                AllPortfolioValue = result;
                return;
            }

            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == secName)
                {
                    decimal value = poses[i].ValueCurrent;

                    if (value == 0)
                    {
                        return;
                    }

                    AllPortfolioValue = value;
                }
            }


        }

        private decimal Sma(List<Candle> candles, int len, int index)
        {
            if (candles.Count == 0
                || index >= candles.Count
                || index <= 0)
            {
                return 0;
            }

            decimal summ = 0;

            int countPoints = 0;

            for (int i = index; i >= 0 && i > index - len; i--)
            {
                countPoints++;
                summ += candles[i].Close;
            }

            if (countPoints == 0)
            {
                return 0;
            }

            return summ / countPoints;
        }

        public List<BadCorrelationCloseTime> TimeLastCloseByBadCorrelationArray = new List<BadCorrelationCloseTime>();
    }

    public class TradeModuleLinearRegression
    {
        public StrategyParameterString Regime;

        public StrategyParameterString SecuritiesToWatch;

        private StrategyParameterDecimal UpDeviation;
        private StrategyParameterInt PeriodLR;

        public StrategyParameterDecimal VolumeOnPositionMain;
        public StrategyParameterInt MaxPositionsCount;
        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterString OrdersType;
        public StrategyParameterDecimal SlippagePercent;
        public StrategyParameterInt IcebergSecondsBetweenOrders;
        public StrategyParameterInt IcebergCount;

        private StrategyParameterTimeOfDay TimeStart;
        private StrategyParameterTimeOfDay TimeEnd;

        public StrategyParameterBool SmaFilterOnSecurityIsOn;
        public StrategyParameterBool SmaFilterOnIndexIsOn;
        public StrategyParameterInt SmaFilterOnSecurityLen;
        public StrategyParameterInt SmaFilterOnIndexLen;

        public int ModuleNum;

        public TradeModuleLinearRegression(BotPanel bot, int moduleNum)
        {
            Regime = bot.CreateParameter("Regime. " + moduleNum, "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" }, "Linear Regression");

            VolumeOnPositionMain = bot.CreateParameter("Volume mult " + moduleNum, 25, 1.0m, 50, 4, "Linear Regression");
            MaxPositionsCount = bot.CreateParameter("Max positions " + moduleNum, 4, 1, 50, 4, "Linear Regression");
            TradeAssetInPortfolio = bot.CreateParameter("Asset in portfolio" + moduleNum, "Prime", "Linear Regression");

            SecuritiesToWatch = bot.CreateParameter("Securities " + moduleNum, "", "Linear Regression");

            PeriodLR = bot.CreateParameter("Period Linear Regression", 50, 50, 300, 1, "Linear Regression");
            UpDeviation = bot.CreateParameter("Deviation LR", 1, 0.1m, 3, 0.1m, "Linear Regression");

            if(bot.StartProgram == StartProgram.IsOsTrader)
            {
                OrdersType = bot.CreateParameter("Orders type" + moduleNum, "Market", new[] { "Market", "Limit", "Iceberg Market" }, "Linear Regression");
                IcebergCount = bot.CreateParameter("Iceberg count " + moduleNum, 5, 1, 50, 4, "Linear Regression");
                IcebergSecondsBetweenOrders = bot.CreateParameter("Iceberg seconds between orders " + moduleNum, 5, 1, 50, 4, "Linear Regression");
            }
            else
            {
                OrdersType = bot.CreateParameter("Orders type" + moduleNum, "Market", new[] { "Market", "Limit"}, "Linear Regression");
            }

            SlippagePercent = bot.CreateParameter("Slippage Percent" + moduleNum, 0, 1.0m, 50, 4, "Linear Regression");

            TimeStart = bot.CreateParameterTimeOfDay("Start Trade Time. lr", 0, 0, 0, 0, "Linear Regression");
            TimeEnd = bot.CreateParameterTimeOfDay("End Trade Time. lr", 24, 0, 0, 0, "Linear Regression");

            SmaFilterOnSecurityIsOn = bot.CreateParameter("Sma Filter On Security IsOn" + moduleNum, false, "Linear Regression");
            SmaFilterOnSecurityLen = bot.CreateParameter("Sma Filter On Security Len" + moduleNum, 150, 20, 500, 1, "Linear Regression");

            SmaFilterOnIndexIsOn = bot.CreateParameter("Sma Filter On Index IsOn" + moduleNum, false, "Linear Regression");
            SmaFilterOnIndexLen = bot.CreateParameter("Sma Filter On Index Len" + moduleNum, 150, 20, 500, 1, "Linear Regression");

            ModuleNum = moduleNum;
        }

        public void TryTrade(
            List<Candle> _candlesTradeSec, List<Candle> _candlesIndex,
            BotTabSimple tab, BotTabScreener screener)
        {
            try
            {
                if (_candlesIndex == null ||
                    _candlesIndex.Count < 10)
                {
                    return;
                }

                if (_candlesTradeSec == null ||
                    _candlesTradeSec.Count < 10)
                {
                    return;
                }

                if (Regime.ValueString == "Off")
                {
                    List<Position> positions = tab.PositionsOpenAll;

                    for (int i = 0; i < positions.Count; i++)
                    {
                        if (positions[i].State == PositionStateType.Open
                            && positions[i].SignalTypeOpen == ModuleNum.ToString())
                        {
                            tab.CloseAtMarket(positions[i], positions[i].OpenVolume);
                        }
                    }

                    return;
                }

                if (TimeStart.Value > tab.TimeServerCurrent ||
                    TimeEnd.Value < tab.TimeServerCurrent)
                {
                    return;
                }

                Aindicator lrIndicator = null;

                for(int i = 0; tab.Indicators != null && i < tab.Indicators.Count;i++)
                {
                    Aindicator curInd = (Aindicator)tab.Indicators[i];

                    if(curInd.GetType().Name == "LinearRegressionChannelFast_Indicator")
                    {
                        lrIndicator = curInd;
                    }
                    else
                    {
                        tab.DeleteCandleIndicator(curInd);
                    }
                }

                if(lrIndicator == null)
                {
                    lrIndicator = IndicatorsFactory.CreateIndicatorByName("LinearRegressionChannelFast_Indicator", tab.TabName + "LinearRegressionChannel", false);
                    lrIndicator = (Aindicator)tab.CreateCandleIndicator(lrIndicator, "Prime");
                    lrIndicator.ParametersDigit[0].Value = PeriodLR.ValueInt;
                    lrIndicator.ParametersDigit[1].Value = UpDeviation.ValueDecimal;
                    lrIndicator.ParametersDigit[2].Value = UpDeviation.ValueDecimal;
                    lrIndicator.Save();
                }

                if(lrIndicator.ParametersDigit[0].Value != PeriodLR.ValueInt)
                {
                    lrIndicator.ParametersDigit[0].Value = PeriodLR.ValueInt;
                    lrIndicator.Save();
                }

                if(lrIndicator.ParametersDigit[1].Value != UpDeviation.ValueDecimal)
                {
                    lrIndicator.ParametersDigit[1].Value = UpDeviation.ValueDecimal;
                    lrIndicator.ParametersDigit[2].Value = UpDeviation.ValueDecimal;
                    lrIndicator.Save();
                }

                List<Position> openPositions = tab.PositionsOpenAll;

                if (openPositions == null || openPositions.Count == 0)
                {
                    string sec = tab.Securiti.Name;

                    if (SecuritiesToWatch.ValueString.Contains(sec) == false)
                    {// выходим, если бумага не установлена в торговую для модуля
                        return;
                    }

                    if (screener.PositionsOpenAll.Count >= MaxPositionsCount.ValueInt)
                    {
                        return;
                    }

                    LogicOpenPosition(_candlesTradeSec, tab, _candlesIndex,lrIndicator);
                }
                else
                {
                    LogicClosePosition(_candlesTradeSec, tab, lrIndicator);
                }
            }
            catch (Exception error)
            {
                tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void LogicOpenPosition(List<Candle> candlesSec,
    BotTabSimple tab, List<Candle> candlesIndex, Aindicator _LinearRegression)
        {
           if (tab.PositionsOpenAll.Count != 0)
            {
                return;
            }

            decimal upChannel = _LinearRegression.DataSeries[0].Values[_LinearRegression.DataSeries[0].Values.Count - 1];
            decimal downChannel = _LinearRegression.DataSeries[2].Values[_LinearRegression.DataSeries[2].Values.Count - 1];

            if (upChannel == 0 ||
                downChannel == 0)
            {
                return;
            }

            bool signalBuy = candlesSec[candlesSec.Count - 1].Close > upChannel;
            bool signalShort = candlesSec[candlesSec.Count - 1].Close < downChannel;

            decimal _lastPrice = candlesSec[candlesSec.Count - 1].Close;

            //  long
            if (Regime.ValueString != "OnlyShort")
            {
                if (signalBuy)
                {
                    bool isFiltred = false;

                    if (SmaFilterOnSecurityIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candlesSec, SmaFilterOnSecurityLen.ValueInt, candlesSec.Count - 1);
                        decimal smaPrev = Sma(candlesSec, SmaFilterOnSecurityLen.ValueInt, candlesSec.Count - 2);

                        if (smaValue < smaPrev)
                        {
                            isFiltred = true;
                        }
                    }

                    if (SmaFilterOnIndexIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candlesIndex, SmaFilterOnIndexLen.ValueInt, candlesIndex.Count - 1);
                        decimal smaPrev = Sma(candlesIndex, SmaFilterOnIndexLen.ValueInt, candlesIndex.Count - 2);

                        if (smaValue < smaPrev)
                        {
                            isFiltred = true;
                        }
                    }

                    if(isFiltred == false)
                    {
                        if (OrdersType.ValueString == "Market")
                        {
                            tab.BuyAtMarket(GetVolume(tab), ModuleNum.ToString());
                        }
                        else if (OrdersType.ValueString == "Limit")
                        {
                            tab.BuyAtLimit(GetVolume(tab), _lastPrice + _lastPrice * (SlippagePercent.ValueDecimal / 100), ModuleNum.ToString());
                        }
                        else if (OrdersType.ValueString == "Iceberg Market")
                        {
                            IcebergMaker icebergMaker = new IcebergMaker();
                            icebergMaker.VolumeOnAllOrders = GetVolume(tab);
                            icebergMaker.OrdersCount = IcebergCount.ValueInt;
                            icebergMaker.SecondsBetweenOrders = IcebergSecondsBetweenOrders.ValueInt;
                            icebergMaker.ModuleNum = ModuleNum;
                            icebergMaker.Tab = tab;
                            icebergMaker.Side = Side.Buy;
                            icebergMaker.Start();
                        }
                    }
                }
            }

            // Short
            if (Regime.ValueString != "OnlyLong")
            {
                if (signalShort)
                {
                    bool isFiltred = false;

                    if (SmaFilterOnSecurityIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candlesSec, SmaFilterOnSecurityLen.ValueInt, candlesSec.Count - 1);
                        decimal smaPrev = Sma(candlesSec, SmaFilterOnSecurityLen.ValueInt, candlesSec.Count - 2);

                        if (smaValue > smaPrev)
                        {
                            isFiltred = true;
                        }
                    }

                    if (SmaFilterOnIndexIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candlesIndex, SmaFilterOnIndexLen.ValueInt, candlesIndex.Count - 1);
                        decimal smaPrev = Sma(candlesIndex, SmaFilterOnIndexLen.ValueInt, candlesIndex.Count - 2);

                        if (smaValue > smaPrev)
                        {
                            isFiltred = true;
                        }
                    }

                    if (isFiltred == false)
                    {
                        if (OrdersType.ValueString == "Market")
                        {
                            tab.SellAtMarket(GetVolume(tab), ModuleNum.ToString());
                        }
                        else if (OrdersType.ValueString == "Limit")
                        {
                            tab.SellAtLimit(GetVolume(tab), _lastPrice - _lastPrice * (SlippagePercent.ValueDecimal / 100), ModuleNum.ToString());
                        }
                        else if (OrdersType.ValueString == "Iceberg Market")
                        {
                            IcebergMaker icebergMaker = new IcebergMaker();
                            icebergMaker.VolumeOnAllOrders = GetVolume(tab);
                            icebergMaker.OrdersCount = IcebergCount.ValueInt;
                            icebergMaker.SecondsBetweenOrders = IcebergSecondsBetweenOrders.ValueInt;
                            icebergMaker.ModuleNum = ModuleNum;
                            icebergMaker.Tab = tab;
                            icebergMaker.Side = Side.Sell;
                            icebergMaker.Start();
                        }
                    }
                }
            }
        }

        private void LogicClosePosition(List<Candle> candles, BotTabSimple tab, Aindicator _LinearRegression)
        {
            List<Position> openPositions = tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].SignalTypeOpen != ModuleNum.ToString())
                {
                    continue;
                }

                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                decimal upChannel = _LinearRegression.DataSeries[0].Values[_LinearRegression.DataSeries[0].Values.Count - 1];
                decimal downChannel = _LinearRegression.DataSeries[2].Values[_LinearRegression.DataSeries[2].Values.Count - 1];

                if (upChannel == 0 ||
                    downChannel == 0)
                {
                    return;
                }

                if (openPositions[i].Direction == Side.Buy)
                {

                    decimal priceStop = downChannel;

                    if (OrdersType.ValueString == "Market")
                    {
                        tab.CloseAtTrailingStopMarket(openPositions[i], priceStop);
                    }
                    else if (OrdersType.ValueString == "Limit")
                    {
                        tab.CloseAtTrailingStop(openPositions[i], priceStop, priceStop - priceStop * (SlippagePercent.ValueDecimal / 100));
                    }
                }
                else
                {
                    decimal priceStop = upChannel;

                    if (OrdersType.ValueString == "Market")
                    {
                        tab.CloseAtTrailingStopMarket(openPositions[i], priceStop);
                    }
                    else if (OrdersType.ValueString == "Limit")
                    {
                        tab.CloseAtTrailingStop(openPositions[i], priceStop, priceStop + priceStop * (SlippagePercent.ValueDecimal / 100));
                    }
                }
            }
        }

        public decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = VolumeOnPositionMain.ValueDecimal;
            decimal contractPrice = tab.PriceBestAsk;

            if (tab.StartProgram == StartProgram.IsOsTrader) // "% от депозита для Бинанс или MOEX"
            {
                CheckPortfolioAutoDate(tab);

                return Math.Round((AllPortfolioValue * (VolumeOnPositionMain.ValueDecimal / 100)) / contractPrice / tab.Securiti.Lot,
                   tab.Securiti.DecimalsVolume);
            }
            else if (tab.StartProgram == StartProgram.IsTester) // "% от депозита"
            {
                return Math.Round(tab.Portfolio.ValueCurrent * (volume / 100) / tab.PriceBestAsk / tab.Securiti.Lot, 12);
            }
            else
            {
                return volume;
            }
        }

        decimal AllPortfolioValue = 0m;

        private void CheckPortfolioAutoDate(BotTabSimple tab)
        {

            Portfolio portfolio = tab.Portfolio;

            if (portfolio == null)
            {
                return;
            }

            List<PositionOnBoard> poses = portfolio.GetPositionOnBoard();

            if (poses == null)
            {
                return;
            }

            string secName = TradeAssetInPortfolio.ValueString;

            if (secName == "Prime")
            {
                AllPortfolioValue = portfolio.ValueCurrent;
                return;
            }

            decimal result;
            if (decimal.TryParse(secName, out result))
            {
                AllPortfolioValue = result;
                return;
            }

            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == secName)
                {
                    decimal value = poses[i].ValueCurrent;

                    if (value == 0)
                    {
                        return;
                    }

                    AllPortfolioValue = value;
                }
            }
        }

        private decimal Sma(List<Candle> candles, int len, int index)
        {
            if (candles.Count == 0
                || index >= candles.Count
                || index <= 0)
            {
                return 0;
            }

            decimal summ = 0;

            int countPoints = 0;

            for (int i = index; i >= 0 && i > index - len; i--)
            {
                countPoints++;
                summ += candles[i].Close;
            }

            if (countPoints == 0)
            {
                return 0;
            }

            return summ / countPoints;
        }
    }

    public class TradeModuleThreeSoldiers
    {
        public StrategyParameterString Regime;

        public StrategyParameterString SecuritiesToWatch;

        public StrategyParameterInt DaysVolatilityAdaptive;
        public StrategyParameterDecimal HeightThreeSoldiersVolaPecrent;
        public StrategyParameterDecimal MinHeightOneCandleVolaPecrent;

        public StrategyParameterDecimal StopVolaPercent;

        public StrategyParameterDecimal VolumeOnPositionMain;
        public StrategyParameterInt MaxPositionsCount;
        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterString OrdersType;
        public StrategyParameterInt IcebergSecondsBetweenOrders;
        public StrategyParameterInt IcebergCount;
        public StrategyParameterDecimal SlippagePercent;

        private StrategyParameterTimeOfDay TimeStart;
        private StrategyParameterTimeOfDay TimeEnd;

        public StrategyParameterBool SmaFilterOnSecurityIsOn;
        public StrategyParameterBool SmaFilterOnIndexIsOn;
        public StrategyParameterInt SmaFilterOnSecurityLen;
        public StrategyParameterInt SmaFilterOnIndexLen;

        public int ModuleNum;

        private string NameStrategyUniq;

        public TradeModuleThreeSoldiers(BotPanel bot, int moduleNum)
        {
            NameStrategyUniq = bot.NameStrategyUniq;

            Regime = bot.CreateParameter("Regime" + moduleNum, "Off", 
                new[] { 
                  "Off"
                , "On"
                , "OnlyLong"
                , "OnlyShort"
                }, "Soldiers");

            VolumeOnPositionMain = bot.CreateParameter("Volume mult " + moduleNum, 25, 1.0m, 50, 4, "Soldiers");
            MaxPositionsCount = bot.CreateParameter("Max positions " + moduleNum, 4, 1, 50, 4, "Soldiers");
            TradeAssetInPortfolio = bot.CreateParameter("Asset in portfolio" + moduleNum, "Prime", "Soldiers");

            SecuritiesToWatch = bot.CreateParameter("Securities " + moduleNum, "", "Soldiers");

            DaysVolatilityAdaptive = bot.CreateParameter("Soldiers adaptive days lookBack", 1, 0, 20, 1, "Soldiers");
            HeightThreeSoldiersVolaPecrent = bot.CreateParameter("Height three soldiers volatility percent", 60, 0, 20, 1m, "Soldiers");
            MinHeightOneCandleVolaPecrent = bot.CreateParameter("Min height one candle volatility percent", 5, 0, 20, 1m, "Soldiers");

            StopVolaPercent = bot.CreateParameter("Stop vola percent" + moduleNum, 30, 1.0m, 50, 4, "Soldiers");

            if (bot.StartProgram == StartProgram.IsOsTrader)
            {
                OrdersType = bot.CreateParameter("Orders type" + moduleNum, "Market", new[] { "Market", "Limit", "Iceberg Market" }, "Soldiers");
                IcebergCount = bot.CreateParameter("Iceberg count " + moduleNum, 5, 1, 50, 4, "Soldiers");
                IcebergSecondsBetweenOrders = bot.CreateParameter("Iceberg seconds between orders " + moduleNum, 5, 1, 50, 4, "Soldiers");
            }
            else
            {
                OrdersType = bot.CreateParameter("Orders type" + moduleNum, "Market", new[] { "Market", "Limit" }, "Soldiers");
            }

            SlippagePercent = bot.CreateParameter("Slippage Percent" + moduleNum, 0, 1.0m, 50, 4, "Soldiers");

            TimeStart = bot.CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Soldiers");
            TimeEnd = bot.CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Soldiers");

            SmaFilterOnSecurityIsOn = bot.CreateParameter("Sma Filter On Security IsOn" + moduleNum, false, "Soldiers");
            SmaFilterOnSecurityLen = bot.CreateParameter("Sma Filter On Security Len" + moduleNum, 150, 20, 500, 1, "Soldiers");

            SmaFilterOnIndexIsOn = bot.CreateParameter("Sma Filter On Index IsOn" + moduleNum, false, "Soldiers");
            SmaFilterOnIndexLen = bot.CreateParameter("Sma Filter On Index Len" + moduleNum, 150, 20, 500, 1, "Soldiers");

            ModuleNum = moduleNum;

            LoadTradeSettings();
        }

        public void TryTrade(
            List<Candle> _candlesTradeSec, List<Candle> _candlesIndex, 
            BotTabSimple tab, BotTabScreener screener)
        {
            try
            {
                if (_candlesIndex == null ||
                    _candlesIndex.Count < 10)
                {
                    return;
                }

                if (_candlesTradeSec == null ||
                    _candlesTradeSec.Count < 10)
                {
                    return;
                }

                if (Regime.ValueString == "Off")
                {
                    List<Position> positions = tab.PositionsOpenAll;

                    for (int i = 0; i < positions.Count; i++)
                    {
                        if (positions[i].State == PositionStateType.Open
                            && positions[i].SignalTypeOpen == ModuleNum.ToString())
                        {
                            tab.CloseAtMarket(positions[i], positions[i].OpenVolume);
                        }
                    }

                    return;
                }

                if (TimeStart.Value > tab.TimeServerCurrent ||
                    TimeEnd.Value < tab.TimeServerCurrent)
                {
                    return;
                }

                for (int i = 0; tab.Indicators != null && i < tab.Indicators.Count; i++)
                {
                    tab.DeleteCandleIndicator(tab.Indicators[i]);
                    i--;
                }

                // если фильтр времени включен и прошли время закрытия принудительного.

                SecuritiesTradeSettings securitiesTradeSettings = GetMyVolaSetting(_candlesTradeSec, tab);

                List<Position> openPositions = tab.PositionsOpenAll;

                if (openPositions == null || openPositions.Count == 0)
                {
                    string sec = tab.Securiti.Name;

                    if (SecuritiesToWatch.ValueString.Contains(sec) == false)
                    {// выходим, если бумага не установлена в торговую для модуля
                        return;
                    }

                    if (screener.PositionsOpenAll.Count >= MaxPositionsCount.ValueInt)
                    {
                        return;
                    }

                    LogicOpenPositionThreeSoldiers(_candlesTradeSec, tab, securitiesTradeSettings, _candlesIndex);
                }
                else
                {
                    LogicClosePositionByTralingStop(_candlesTradeSec, tab, securitiesTradeSettings);
                }
            }
            catch (Exception error)
            {
                tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void LogicOpenPositionThreeSoldiers(List<Candle> candlesSec, 
            BotTabSimple tab, SecuritiesTradeSettings settings, List<Candle> candlesIndex)
        {
            if(tab.PositionsOpenAll.Count != 0)
            {
                return;
            }

            decimal _lastPrice = candlesSec[candlesSec.Count - 1].Close;

            if (Math.Abs(candlesSec[candlesSec.Count - 3].Open - candlesSec[candlesSec.Count - 1].Close)
                / (candlesSec[candlesSec.Count - 1].Close / 100) < settings.HeightThreeSoldiers)
            {
                return;
            }
            if (Math.Abs(candlesSec[candlesSec.Count - 3].Open - candlesSec[candlesSec.Count - 3].Close)
                / (candlesSec[candlesSec.Count - 3].Close / 100) < settings.MinHeightOneSoldier)
            {
                return;
            }
            if (Math.Abs(candlesSec[candlesSec.Count - 2].Open - candlesSec[candlesSec.Count - 2].Close)
                / (candlesSec[candlesSec.Count - 2].Close / 100) < settings.MinHeightOneSoldier)
            {
                return;
            }
            if (Math.Abs(candlesSec[candlesSec.Count - 1].Open - candlesSec[candlesSec.Count - 1].Close)
                / (candlesSec[candlesSec.Count - 1].Close / 100) < settings.MinHeightOneSoldier)
            {
                return;
            }

            //  long
            if (Regime.ValueString != "OnlyShort")
            {
                if (candlesSec[candlesSec.Count - 3].Open < candlesSec[candlesSec.Count - 3].Close
                    && candlesSec[candlesSec.Count - 2].Open < candlesSec[candlesSec.Count - 2].Close
                    && candlesSec[candlesSec.Count - 1].Open < candlesSec[candlesSec.Count - 1].Close)
                {
                    if (SmaFilterOnSecurityIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candlesSec, SmaFilterOnSecurityLen.ValueInt, candlesSec.Count - 1);
                        decimal smaPrev = Sma(candlesSec, SmaFilterOnSecurityLen.ValueInt, candlesSec.Count - 2);

                        if (smaValue < smaPrev)
                        {
                            return;
                        }
                    }

                    if (SmaFilterOnIndexIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candlesIndex, SmaFilterOnIndexLen.ValueInt, candlesIndex.Count - 1);
                        decimal smaPrev = Sma(candlesIndex, SmaFilterOnIndexLen.ValueInt, candlesIndex.Count - 2);

                        if (smaValue < smaPrev)
                        {
                            return;
                        }
                    }

                    if (OrdersType.ValueString == "Market")
                    {
                        tab.BuyAtMarket(GetVolume(tab), ModuleNum.ToString());
                    }
                    else if (OrdersType.ValueString == "Limit")
                    {
                        tab.BuyAtLimit(GetVolume(tab), _lastPrice + _lastPrice * (SlippagePercent.ValueDecimal / 100), ModuleNum.ToString());
                    }
                    else if (OrdersType.ValueString == "Iceberg Market")
                    {
                        IcebergMaker icebergMaker = new IcebergMaker();
                        icebergMaker.VolumeOnAllOrders = GetVolume(tab);
                        icebergMaker.OrdersCount = IcebergCount.ValueInt;
                        icebergMaker.SecondsBetweenOrders = IcebergSecondsBetweenOrders.ValueInt;
                        icebergMaker.ModuleNum = ModuleNum;
                        icebergMaker.Tab = tab;
                        icebergMaker.Side = Side.Buy;
                        icebergMaker.Start();
                    }
                }
            }

            // Short
            if (Regime.ValueString != "OnlyLong")
            {
                if (candlesSec[candlesSec.Count - 3].Open > candlesSec[candlesSec.Count - 3].Close
                    && candlesSec[candlesSec.Count - 2].Open > candlesSec[candlesSec.Count - 2].Close
                    && candlesSec[candlesSec.Count - 1].Open > candlesSec[candlesSec.Count - 1].Close)
                {
                    if (SmaFilterOnSecurityIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candlesSec, SmaFilterOnSecurityLen.ValueInt, candlesSec.Count - 1);
                        decimal smaPrev = Sma(candlesSec, SmaFilterOnSecurityLen.ValueInt, candlesSec.Count - 2);

                        if (smaValue > smaPrev)
                        {
                            return;
                        }
                    }

                    if (SmaFilterOnIndexIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candlesIndex, SmaFilterOnIndexLen.ValueInt, candlesIndex.Count - 1);
                        decimal smaPrev = Sma(candlesIndex, SmaFilterOnIndexLen.ValueInt, candlesIndex.Count - 2);

                        if (smaValue > smaPrev)
                        {
                            return;
                        }
                    }

                    if (OrdersType.ValueString == "Market")
                    {
                        tab.SellAtMarket(GetVolume(tab), ModuleNum.ToString());
                    }
                    else if (OrdersType.ValueString == "Limit")
                    {
                        tab.SellAtLimit(GetVolume(tab), _lastPrice - _lastPrice * (SlippagePercent.ValueDecimal / 100), ModuleNum.ToString());
                    }
                    else if (OrdersType.ValueString == "Iceberg Market")
                    {
                        IcebergMaker icebergMaker = new IcebergMaker();
                        icebergMaker.VolumeOnAllOrders = GetVolume(tab);
                        icebergMaker.OrdersCount = IcebergCount.ValueInt;
                        icebergMaker.SecondsBetweenOrders = IcebergSecondsBetweenOrders.ValueInt;
                        icebergMaker.ModuleNum = ModuleNum;
                        icebergMaker.Tab = tab;
                        icebergMaker.Side = Side.Sell;
                        icebergMaker.Start();
                    }
                }
            }
        }

        private void LogicClosePositionByTralingStop(List<Candle> candles, BotTabSimple tab, SecuritiesTradeSettings settings)
        {
            decimal _lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].SignalTypeOpen != ModuleNum.ToString())
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy)
                {
                    decimal heightPattern = settings.Volatility;

                    decimal priceStop = _lastPrice - _lastPrice * ((heightPattern * (StopVolaPercent.ValueDecimal / 100)) / 100);

                    if (OrdersType.ValueString == "Limit")
                    {
                        tab.CloseAtTrailingStop(openPositions[i], priceStop, priceStop - priceStop * (SlippagePercent.ValueDecimal / 100));
                    }
                    else //if (OrdersType.ValueString == "Market")
                    {
                        tab.CloseAtTrailingStopMarket(openPositions[i], priceStop);
                    }
                }
                else
                {
                    decimal heightPattern = settings.Volatility;

                    decimal priceStop = _lastPrice + _lastPrice * ((heightPattern * (StopVolaPercent.ValueDecimal / 100)) / 100);

                    if (OrdersType.ValueString == "Limit")
                    {
                        tab.CloseAtTrailingStop(openPositions[i], priceStop, priceStop + priceStop * (SlippagePercent.ValueDecimal / 100));
                    }
                    else //if (OrdersType.ValueString == "Market")
                    {
                        tab.CloseAtTrailingStopMarket(openPositions[i], priceStop);
                    }
                }
            }
        }

        public decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = VolumeOnPositionMain.ValueDecimal;
            decimal contractPrice = tab.PriceBestAsk;

            if (tab.StartProgram == StartProgram.IsOsTrader) // "% от депозита для Бинанс или MOEX"
            {
                CheckPortfolioAutoDate(tab);

                return Math.Round((AllPortfolioValue * (VolumeOnPositionMain.ValueDecimal / 100)) / contractPrice / tab.Securiti.Lot,
                   tab.Securiti.DecimalsVolume);
            }
            else if (tab.StartProgram == StartProgram.IsTester) // "% от депозита"
            {
                return Math.Round(tab.Portfolio.ValueCurrent * (volume / 100) / tab.PriceBestAsk / tab.Securiti.Lot, 12);
            }
            else
            {
                return volume;
            }
        }

        decimal AllPortfolioValue = 0m;

        private void CheckPortfolioAutoDate(BotTabSimple tab)
        {

            Portfolio portfolio = tab.Portfolio;

            if (portfolio == null)
            {
                return;
            }

            List<PositionOnBoard> poses = portfolio.GetPositionOnBoard();

            if (poses == null)
            {
                return;
            }

            string secName = TradeAssetInPortfolio.ValueString;

            if(secName == "Prime")
            {
                AllPortfolioValue = portfolio.ValueCurrent;
                return;
            }

            decimal result;
            if (decimal.TryParse(secName, out result))
            {
                AllPortfolioValue = result;
                return;
            }

            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == secName)
                {
                    decimal value = poses[i].ValueCurrent;

                    if (value == 0)
                    {
                        return;
                    }

                    AllPortfolioValue = value;
                }
            }
        }

        private decimal Sma(List<Candle> candles, int len, int index)
        {
            if (candles.Count == 0
                || index >= candles.Count
                || index <= 0)
            {
                return 0;
            }

            decimal summ = 0;

            int countPoints = 0;

            for (int i = index; i >= 0 && i > index - len; i--)
            {
                countPoints++;
                summ += candles[i].Close;
            }

            if (countPoints == 0)
            {
                return 0;
            }

            return summ / countPoints;
        }

        #region Securities volatility adaptive

        public List<SecuritiesTradeSettings> TradeSettings = new List<SecuritiesTradeSettings>();

        private void SaveTradeSettings()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    for (int i = 0; i < TradeSettings.Count; i++)
                    {
                        writer.WriteLine(TradeSettings[i].GetSaveString());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void LoadTradeSettings()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string line = reader.ReadLine();

                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        SecuritiesTradeSettings newSettings = new SecuritiesTradeSettings();
                        newSettings.LoadFromString(line);
                        TradeSettings.Add(newSettings);
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void AdaptSoldiersHeight(List<Candle> candles, SecuritiesTradeSettings settings)
        {
            if (DaysVolatilityAdaptive.ValueInt <= 0
                || HeightThreeSoldiersVolaPecrent.ValueDecimal <= 0
                || MinHeightOneCandleVolaPecrent.ValueDecimal <= 0)
            {
                return;
            }

            // 1 рассчитываем движение от хая до лоя внутри N дней

            decimal minValueInDay = decimal.MaxValue;
            decimal maxValueInDay = decimal.MinValue;

            List<decimal> volaInDaysPercent = new List<decimal>();

            DateTime date = candles[candles.Count - 1].TimeStart.Date;

            int days = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                Candle curCandle = candles[i];

                if (curCandle.TimeStart.Date < date)
                {
                    date = curCandle.TimeStart.Date;
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);

                    volaInDaysPercent.Add(volaPercentToday);


                    minValueInDay = decimal.MaxValue;
                    maxValueInDay = decimal.MinValue;
                }

                if (days >= DaysVolatilityAdaptive.ValueInt)
                {
                    break;
                }

                if (curCandle.High > maxValueInDay)
                {
                    maxValueInDay = curCandle.High;
                }
                if (curCandle.Low < minValueInDay)
                {
                    minValueInDay = curCandle.Low;
                }

                if (i == 0)
                {
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);
                    volaInDaysPercent.Add(volaPercentToday);
                }
            }

            if (volaInDaysPercent.Count == 0)
            {
                return;
            }

            // 2 усредняем это движение. Нужна усреднённая волатильность. процент

            decimal volaPercentSma = 0;

            for (int i = 0; i < volaInDaysPercent.Count; i++)
            {
                volaPercentSma += volaInDaysPercent[i];
            }

            volaPercentSma = volaPercentSma / volaInDaysPercent.Count;

            // 3 считаем размер свечей с учётом этой волатильности
            decimal threeSoldiersHeight = volaPercentSma * (HeightThreeSoldiersVolaPecrent.ValueDecimal / 100);
            decimal oneCandleHeight = volaPercentSma * (MinHeightOneCandleVolaPecrent.ValueDecimal / 100);

            settings.Volatility = volaPercentSma;
            settings.HeightThreeSoldiers = threeSoldiersHeight;
            settings.MinHeightOneSoldier = oneCandleHeight;
            settings.LastUpdateTime = candles[candles.Count - 1].TimeStart;
        }

        private SecuritiesTradeSettings GetMyVolaSetting(List<Candle> candles, BotTabSimple tab)
        {
            SecuritiesTradeSettings mySettings = null;

            for (int i = 0; i < TradeSettings.Count; i++)
            {
                if (TradeSettings[i].SecName == tab.Securiti.Name &&
                    TradeSettings[i].SecClass == tab.Securiti.NameClass)
                {
                    mySettings = TradeSettings[i];
                    break;
                }
            }

            if (mySettings == null)
            {
                mySettings = new SecuritiesTradeSettings();
                mySettings.SecName = tab.Securiti.Name;
                mySettings.SecClass = tab.Securiti.NameClass;
                TradeSettings.Add(mySettings);
            }

            if (mySettings.LastUpdateTime.Date < candles[candles.Count - 1].TimeStart.Date
                || mySettings.LastUpdateTime.Date != candles[candles.Count - 1].TimeStart.Date)
            {
                AdaptSoldiersHeight(candles, mySettings);

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    SaveTradeSettings();
                }
            }

            if (mySettings.HeightThreeSoldiers == 0 ||
                mySettings.MinHeightOneSoldier == 0)
            {
                return null;
            }

            return mySettings;
        }

        #endregion
    }

    public class IcebergMaker
    {
        public int OrdersCount;

        public int SecondsBetweenOrders;

        public decimal VolumeOnAllOrders;

        public int ModuleNum;

        public BotTabSimple Tab;

        public Side Side;

        public Position PositionToClose;

        public void Start()
        {
            if (PositionToClose == null)
            {
                Thread worker = new Thread(OpenPositionMethod);
                worker.Start();
            }
            else
            {
                Thread worker = new Thread(ClosePositionMethod);
                worker.Start();
            }
        }

        private void OpenPositionMethod()
        {
            try
            {
                if (OrdersCount < 1)
                {
                    OrdersCount = 1;
                }

                List<decimal> volumes = new List<decimal>();

                decimal allVolumeInArray = 0;

                for (int i = 0; i < OrdersCount; i++)
                {
                    decimal curVolume = VolumeOnAllOrders / OrdersCount;
                    curVolume = Math.Round(curVolume, Tab.Securiti.DecimalsVolume);
                    allVolumeInArray += curVolume;
                    volumes.Add(curVolume);
                }

                if (allVolumeInArray != VolumeOnAllOrders)
                {
                    decimal residue = VolumeOnAllOrders - allVolumeInArray;

                    volumes[0] = Math.Round(volumes[0] + residue, Tab.Securiti.DecimalsVolume);
                }

                for (int i = 0; i < volumes.Count; i++)
                {
                    if (Side == Side.Buy)
                    {
                        if (Tab.PositionsOpenAll.Count == 0)
                        {
                            Tab.BuyAtMarket(volumes[i], ModuleNum.ToString());
                        }
                        else
                        {
                            Tab.BuyAtMarketToPosition(Tab.PositionsOpenAll[0], volumes[i]);
                        }
                    }
                    if (Side == Side.Sell)
                    {
                        if (Tab.PositionsOpenAll.Count == 0)
                        {
                            Tab.SellAtMarket(volumes[i], ModuleNum.ToString());
                        }
                        else
                        {
                            Tab.SellAtMarketToPosition(Tab.PositionsOpenAll[0], volumes[i]);
                        }
                    }
                    Thread.Sleep(SecondsBetweenOrders * 1000);
                }
            }
            catch (Exception error)
            {
                Tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ClosePositionMethod()
        {
            try
            {
                int iterationCount = 0;

                if (OrdersCount < 1)
                {
                    OrdersCount = 1;
                }

                VolumeOnAllOrders = PositionToClose.OpenVolume;

                List<decimal> volumes = new List<decimal>();

                decimal allVolumeInArray = 0;

                for (int i = 0; i < OrdersCount; i++)
                {
                    decimal curVolume = VolumeOnAllOrders / OrdersCount;
                    curVolume = Math.Round(curVolume, Tab.Securiti.DecimalsVolume);
                    allVolumeInArray += curVolume;
                    volumes.Add(curVolume);
                }

                if (allVolumeInArray != VolumeOnAllOrders)
                {
                    decimal residue = VolumeOnAllOrders - allVolumeInArray;

                    volumes[0] = Math.Round(volumes[0] + residue, Tab.Securiti.DecimalsVolume);
                }

                for (int i = 0; i < volumes.Count; i++)
                {
                    Tab.CloseAtMarket(PositionToClose, volumes[i]);

                    Thread.Sleep(SecondsBetweenOrders * 1000);
                }
            }
            catch (Exception error)
            {
                Tab.SetNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }

    public class BadCorrelationCloseTime
    {
        public DateTime Time;

        public string SecurityName;

    }

    public class SecurityInIndex
    {
        public string SecName;

        public string Name;

        public decimal Mult;

        public decimal SummVolume;

        public decimal VolatylityDayPercent;

        public List<Candle> Candles;

        public decimal LastPrice
        {
            get
            {
                if (Candles == null || Candles.Count == 0)
                {
                    return 0;
                }
                return Candles[Candles.Count - 1].Close;
            }
        }
    }

    public class SecuritiesTradeSettings
    {
        public string SecName;

        public string SecClass;

        public decimal Volatility;

        public decimal HeightThreeSoldiers;

        public decimal MinHeightOneSoldier;

        public DateTime LastUpdateTime;

        public string GetSaveString()
        {
            string result = "";

            result += SecName + "%";
            result += SecClass + "%";
            result += Volatility + "%";
            result += HeightThreeSoldiers + "%";
            result += MinHeightOneSoldier + "%";
            result += LastUpdateTime.ToString(CultureInfo.InvariantCulture) + "%";

            return result;
        }

        public void LoadFromString(string str)
        {
            string[] array = str.Split('%');

            SecName = array[0];
            SecClass = array[1];
            Volatility = array[2].ToDecimal();
            HeightThreeSoldiers = array[5].ToDecimal();
            MinHeightOneSoldier = array[6].ToDecimal();
            LastUpdateTime = Convert.ToDateTime(array[7], CultureInfo.InvariantCulture);
        }
    }
}