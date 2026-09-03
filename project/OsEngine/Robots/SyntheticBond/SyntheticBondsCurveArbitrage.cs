/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Candles.Series;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

using PairInPosition = (OsEngine.OsTrader.Panels.Tab.BotTabSimple Base, OsEngine.OsTrader.Panels.Tab.BotTabSimple Futures);
using Pretender = (OsEngine.OsTrader.Panels.Tab.BotTabSimple Base, OsEngine.OsTrader.Panels.Tab.BotTabSimple Futures, decimal Mult, bool IsSecondSeries);

/*

Арбитраж синтетических облигаций. Контанго-арбитраж на рынке фьючерсов на акции MOEX

Конструкция позиции (синтетическая облигация в контанго)
Лонг акция (база) + Шорт фьючерс
Объёмы в лотах: база = контракты фьючерса × мульт / лот акции (в тестере лот = 1),
дельта-нейтрально. Свободные деньги паркуются в LQDT

Источники
10 пар источников. В каждой паре BotTabSimple - базовая акция, BotTabScreener - фьючерсы на неё.
Все 10 пар разворачиваются кнопками авто-развёртывания (Т-Банк в реале, выбранный сет в тестере).
По каждой паре торгуются две серии фьючерсов: ближайшая (до 120 дней) и следующая (до 180 дней,
вторая серия - по флагам группы Second series, по умолчанию включена для пар 1-7).
Выбор пары - по годовой доходности контанго (yieldAnn)

ВХОД в позицию
Из всех претендентов (обе разрешённые серии всех пар) выбирается пара с максимальной годовой
доходностью контанго. Вход разрешён, если доходность выше LQDT + Min yield diff over LQDT
(отдельные пороги для первой и второй серии)

ПЕРЕНОС позиции
Если доходность у претендента больше текущего на Min Yield Diff To Move,
позиция закрывается и открывается на более доходной паре

ВЫХОД из позиции
1) Накануне экспирации фьючерса
2) Аварийный выход: открылась только одна нога, ноги открыты в разные дни
   или нога открыта до 10:00 (артефакт укороченной сессии)
3) Если позиций стало больше одной, закрывается худшая
4) Если пара ушла в бэквордацию (Exit on backwardation is on)

*/

namespace OsEngine.Robots.SyntheticBond
{
    [Bot("SyntheticBondsCurveArbitrage")]
    public class SyntheticBondsCurveArbitrage : BotPanel
    {
        private StrategyParameterString _regime;
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodButton;

        private StrategyParameterDecimal _minYieldDiffToMove;
        private StrategyParameterInt _tableUpdateIntervalSec;
        private StrategyParameterString _multRegime;
        private StrategyParameterBool _fullLogIsOn;
        private StrategyParameterBool _exitOnBackwardationIsOn;
        private StrategyParameterInt _minDaysToExpiration;

        private StrategyParameterBool _tradePair1;
        private StrategyParameterBool _tradePair2;
        private StrategyParameterBool _tradePair3;
        private StrategyParameterBool _tradePair4;
        private StrategyParameterBool _tradePair5;
        private StrategyParameterBool _tradePair6;
        private StrategyParameterBool _tradePair7;
        private StrategyParameterBool _tradePair8;
        private StrategyParameterBool _tradePair9;
        private StrategyParameterBool _tradePair10;

        private StrategyParameterBool _tradeSecondSeries1;
        private StrategyParameterBool _tradeSecondSeries2;
        private StrategyParameterBool _tradeSecondSeries3;
        private StrategyParameterBool _tradeSecondSeries4;
        private StrategyParameterBool _tradeSecondSeries5;
        private StrategyParameterBool _tradeSecondSeries6;
        private StrategyParameterBool _tradeSecondSeries7;
        private StrategyParameterBool _tradeSecondSeries8;
        private StrategyParameterBool _tradeSecondSeries9;
        private StrategyParameterBool _tradeSecondSeries10;

        private StrategyParameterDecimal _minYieldDiffOverLqdtSeries2;

        private StrategyParameterBool _LqdtRegimeIsOn;
        private StrategyParameterDecimal _minYieldDiffOverLqdt;
        private StrategyParameterInt _LqdtYieldDays;
        private StrategyParameterDecimal _LqdtVolumePercent;

        private StrategyParameterInt _daysBeforeExpirationToExit;

        private StrategyParameterDecimal _futuresMult1;
        private StrategyParameterDecimal _futuresMult2;
        private StrategyParameterDecimal _futuresMult3;
        private StrategyParameterDecimal _futuresMult4;
        private StrategyParameterDecimal _futuresMult5;
        private StrategyParameterDecimal _futuresMult6;
        private StrategyParameterDecimal _futuresMult7;
        private StrategyParameterDecimal _futuresMult8;
        private StrategyParameterDecimal _futuresMult9;
        private StrategyParameterDecimal _futuresMult10;

        private StrategyParameterString _portfolioNum;
        private StrategyParameterString _testerDeployTimeFrame;
        private StrategyParameterString _deployTimeFrame;

        public SyntheticBondsCurveArbitrage(string name, StartProgram startProgram) : base(name, startProgram)
        {
            CreateSources();

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" }, "Base");
            _minYieldDiffToMove = CreateParameter("Min Yield Diff To Move % ann", 5.5m, 1.0m, 100, 1, "Base");
            _tableUpdateIntervalSec = CreateParameter("Table update interval, sec", 5, 1, 60, 1, "Base");
            _fullLogIsOn = CreateParameter("Full log is on", false, "Base");
            _exitOnBackwardationIsOn = CreateParameter("Exit on backwardation is on", true, "Base");
            _minDaysToExpiration = CreateParameter("Min days to expiration", 18, 1, 60, 1, "Base");

            _tradePair1 = CreateParameter("Trade pair 1", true, "Trade pairs");
            _tradePair2 = CreateParameter("Trade pair 2", true, "Trade pairs");
            _tradePair3 = CreateParameter("Trade pair 3", true, "Trade pairs");
            _tradePair4 = CreateParameter("Trade pair 4", true, "Trade pairs");
            _tradePair5 = CreateParameter("Trade pair 5", true, "Trade pairs");
            _tradePair6 = CreateParameter("Trade pair 6", true, "Trade pairs");
            _tradePair7 = CreateParameter("Trade pair 7", true, "Trade pairs");
            _tradePair8 = CreateParameter("Trade pair 8", true, "Trade pairs");
            _tradePair9 = CreateParameter("Trade pair 9", true, "Trade pairs");
            _tradePair10 = CreateParameter("Trade pair 10", true, "Trade pairs");

            _tradeSecondSeries1 = CreateParameter("Trade second series 1", true, "Second series");
            _tradeSecondSeries2 = CreateParameter("Trade second series 2", true, "Second series");
            _tradeSecondSeries3 = CreateParameter("Trade second series 3", true, "Second series");
            _tradeSecondSeries4 = CreateParameter("Trade second series 4", true, "Second series");
            _tradeSecondSeries5 = CreateParameter("Trade second series 5", true, "Second series");
            _tradeSecondSeries6 = CreateParameter("Trade second series 6", true, "Second series");
            _tradeSecondSeries7 = CreateParameter("Trade second series 7", true, "Second series");
            _tradeSecondSeries8 = CreateParameter("Trade second series 8", false, "Second series");
            _tradeSecondSeries9 = CreateParameter("Trade second series 9", false, "Second series");
            _tradeSecondSeries10 = CreateParameter("Trade second series 10", false, "Second series");

            
            _daysBeforeExpirationToExit = CreateParameter("Days before expiration to exit", 7, 0, 10, 1, "Base");
            _LqdtRegimeIsOn = CreateParameter("LQDT regime is on", true, "LQDT");
            _minYieldDiffOverLqdt = CreateParameter("Min yield diff over LQDT %. Series 1", 3.5m, 0.1m, 100, 1, "LQDT");
            _minYieldDiffOverLqdtSeries2 = CreateParameter("Min yield diff over LQDT %. Series 2", 2.5m, 0.1m, 100, 1, "LQDT");
            _LqdtYieldDays = CreateParameter("LQDT yield days", 10, 5, 60, 5, "LQDT");
            _LqdtVolumePercent = CreateParameter("LQDT volume % of free money", 100m, 0m, 100m, 1, "LQDT");

            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 85m, 1.0m, 100, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 30 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 24, Minute = 00 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            _tradePeriodButton = CreateParameterButton("Non trade periods", "Base");
            _tradePeriodButton.UserClickOnButtonEvent += _tradePeriodButton_UserClickOnButtonEvent;

            _multRegime = CreateParameter("Mult regime", "Auto", new[] { "Auto", "Manual" }, "Fut mults");
            _futuresMult1 = CreateParameter("Fut mult 2", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult2 = CreateParameter("Fut mult 4", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult3 = CreateParameter("Fut mult 6", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult4 = CreateParameter("Fut mult 8", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult5 = CreateParameter("Fut mult 10", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult6 = CreateParameter("Fut mult 12", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult7 = CreateParameter("Fut mult 14", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult8 = CreateParameter("Fut mult 16", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult9 = CreateParameter("Fut mult 18", 1m, 1.0m, 50, 4, "Fut mults");
            _futuresMult10 = CreateParameter("Fut mult 20", 1m, 1.0m, 50, 4, "Fut mults");

            if (startProgram == StartProgram.IsOsTrader)
            {
                _portfolioNum = CreateParameter("Portfolio number", "", "Auto deploy");
                _deployTimeFrame = CreateParameter("Deploy time frame", "Min5", new[] { "Min1", "Min5", "Min15", "Min30" }, "Auto deploy");
                StrategyParameterButton buttonAutoDeploy = CreateParameterButton("Deploy standard securities", "Auto deploy");
                buttonAutoDeploy.UserClickOnButtonEvent += ButtonAutoDeploy_UserClickOnButtonEvent;

                _logicTimer = new System.Threading.Timer(LogicTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

                _futs1.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs2.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs3.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs4.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs5.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs6.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs7.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs8.CandleFinishedEvent += Screener_CandleFinishedEvent;
                _futs9.CandleFinishedEvent += Screener_CandleFinishedEvent;
            _futs10.CandleFinishedEvent += Screener_CandleFinishedEvent;

            }

            if (startProgram == StartProgram.IsTester)
            {
                _testerDeployTimeFrame = CreateParameter("Tester deploy time frame", "Min5",
                    new[] { "Min1", "Min2", "Min3", "Min5", "Min10", "Min15", "Min20", "Min30", "Min45", "Hour1" }, "Auto deploy");

                StrategyParameterButton buttonAutoDeployTester = CreateParameterButton("Deploy tester securities", "Auto deploy");
                buttonAutoDeployTester.UserClickOnButtonEvent += ButtonAutoDeployTester_UserClickOnButtonEvent;

                List<IServer> server = ServerMaster.GetServers();

                if (server != null &&
                    server.Count > 0
                    && server[0].ServerType == ServerType.Tester)
                {
                    TesterServer serverT = (TesterServer)server[0];
                    serverT.EndNextMinuteWithCandlesEvent += ServerT_EndNextMinuteWithCandlesEvent;
                    serverT.TestingEndEvent += ServerT_TestingEndEvent;
                    serverT.TestingStartEvent += ServerT_TestingStartEvent;
                }
            }

            if (startProgram == StartProgram.IsOsOptimizer)
            {
                _futs1.CandleFinishedEvent += Screener_CandleFinishedEventInOptimizer;
            }

            /*
            Description = OsLocalization.ConvertToLocString(
              "Eng:Arbitrage of synthetic bonds on the MOEX stock futures market. Long stock plus short futures (equal number of shares in both legs, delta-neutral) in the pair with the highest annualized contango yield among 10 blue chips. Two futures series per pair can trade (nearest and next one), selection by annualized yield. Free money is parked in LQDT. The position is moved to a more profitable contract and closed before expiration_" +
              "Ru:Арбитраж синтетических облигаций на рынке фьючерсов на акции MOEX. Лонг акция плюс шорт фьючерс (равное число акций в ногах, дельта-нейтрально) в паре с максимальной доходностью контанго в годовых среди 10 голубых фишек. По каждой паре могут торговаться две серии фьючерсов (ближайшая и следующая), выбор по годовой доходности. Свободные деньги паркуются в LQDT. Позиция переносится на более доходный контракт и закрывается перед экспирацией_");
            */

            if (startProgram != StartProgram.IsOsOptimizer)
            {
                this.ParamGuiSettings.Height = 800;
                this.ParamGuiSettings.Width = 700;

                CustomTabToParametersUi customTabMonitor = ParamGuiSettings.CreateCustomTab(" Monitor ");
                CreateColumnsTable();
                customTabMonitor.AddChildren(_hostTable);

                _monitorTimer = new System.Threading.Timer(MonitorTimerCallback, null, 2000, Timeout.Infinite);
            }
        }

        private bool _optimizerEventSubscribed = false;

        private void Screener_CandleFinishedEventInOptimizer(List<Candle> candles, BotTabSimple source)
        {
            if (_optimizerEventSubscribed)
            {
                return;
            }

            if (source.Connector.ServerType != ServerType.Optimizer)
            {
                return;
            }

            _optimizerEventSubscribed = true;

            OptimizerServer server = source.Connector.MyServer as OptimizerServer;

            if (server != null)
            {
                server.EndNextMinuteWithCandlesEvent += ServerT_EndNextMinuteWithCandlesEvent;
            }
        }

        private void ServerT_EndNextMinuteWithCandlesEvent()
        {
            Logic();
        }

        private void ServerT_TestingEndEvent()
        {
            LogYearSummary();
        }

        private void ServerT_TestingStartEvent()
        {
            try
            {
                string date = DateTime.Now.Year + "_" + DateTime.Now.Month + "_" + DateTime.Now.Day;
                string path = @"Engine\Log\" + NameStrategyUniq + @"Log_" + date + ".txt";

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }

            LogFull("Test started");
        }

        private void LogYearSummary()
        {
            try
            {
                Dictionary<int, decimal> profitAbsByYear = new Dictionary<int, decimal>();
                Dictionary<int, decimal> profitPercentByYear = new Dictionary<int, decimal>();

                List<IIBotTab> tabs = GetTabs();

                for (int i = 0; tabs != null && i < tabs.Count; i++)
                {
                    if (tabs[i] is BotTabSimple simple)
                    {
                        AddPositionsProfitByYear(simple.PositionsCloseAll, profitAbsByYear, profitPercentByYear);
                        AddPositionsProfitByYear(simple.PositionsOpenAll, profitAbsByYear, profitPercentByYear);
                    }
                    else if (tabs[i] is BotTabScreener screener)
                    {
                        for (int j = 0; j < screener.Tabs.Count; j++)
                        {
                            AddPositionsProfitByYear(screener.Tabs[j].PositionsCloseAll, profitAbsByYear, profitPercentByYear);
                            AddPositionsProfitByYear(screener.Tabs[j].PositionsOpenAll, profitAbsByYear, profitPercentByYear);
                        }
                    }
                }

                Dictionary<int, decimal> lqdtByYear = GetLqdtYearReturns();

                List<int> years = new List<int>();

                foreach (int year in profitAbsByYear.Keys)
                {
                    if (years.Contains(year) == false)
                    {
                        years.Add(year);
                    }
                }
                foreach (int year in lqdtByYear.Keys)
                {
                    if (years.Contains(year) == false)
                    {
                        years.Add(year);
                    }
                }

                years.Sort();

                decimal alphaSum = 0;
                int yearsCount = 0;

                for (int i = 0; i < years.Count; i++)
                {
                    int year = years[i];

                    yearsCount++;

                    decimal robotProfit = 0;
                    profitAbsByYear.TryGetValue(year, out robotProfit);

                    decimal robotPercent = 0;
                    profitPercentByYear.TryGetValue(year, out robotPercent);

                    decimal lqdtReturn = 0;
                    lqdtByYear.TryGetValue(year, out lqdtReturn);

                    decimal alpha = robotPercent - lqdtReturn;
                    alphaSum += alpha;

                    SendNewLogMessage(
                        "YearSummary: " + year
                        + " | LQDT " + Math.Round(lqdtReturn, 1) + "%"
                        + " | Robot " + Math.Round(robotProfit, 0) + " (" + Math.Round(robotPercent, 1) + "%)"
                        + " | alpha " + Math.Round(alpha, 1) + " pp", LogMessageType.Error);
                }

                if (yearsCount > 0)
                {
                    SendNewLogMessage(
                        "YearSummary: AVERAGE alpha "
                        + Math.Round(alphaSum / yearsCount, 1) + " pp over " + yearsCount + " years", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void AddPositionsProfitByYear(List<Position> positions,
            Dictionary<int, decimal> profitAbsByYear, Dictionary<int, decimal> profitPercentByYear)
        {
            if (positions == null)
            {
                return;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                int year = positions[i].TimeOpen.Year;

                if (year < 2000)
                {
                    continue;
                }

                if (profitAbsByYear.ContainsKey(year) == false)
                {
                    profitAbsByYear.Add(year, 0);
                    profitPercentByYear.Add(year, 0);
                }

                profitAbsByYear[year] += positions[i].ProfitPortfolioAbs;
                profitPercentByYear[year] += positions[i].ProfitPortfolioPercent;
            }
        }

        private Dictionary<int, decimal> GetLqdtYearReturns()
        {
            Dictionary<int, decimal> result = new Dictionary<int, decimal>();
            Dictionary<int, decimal> firstCloseByYear = new Dictionary<int, decimal>();

            List<Candle> candles = _tabLqdt.CandlesAll;

            if (candles == null
                || candles.Count < 2)
            {
                return result;
            }

            for (int i = 0; i < candles.Count; i++)
            {
                int year = candles[i].TimeStart.Year;

                if (firstCloseByYear.ContainsKey(year) == false)
                {
                    firstCloseByYear.Add(year, candles[i].Close);
                    result.Add(year, 0);
                }
                else
                {
                    decimal firstClose = firstCloseByYear[year];

                    if (firstClose != 0)
                    {
                        result[year] = (candles[i].Close / firstClose - 1) * 100;
                    }
                }
            }

            return result;
        }

        private void _tradePeriodButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        #region Logic entry synchronization in real

        private System.Threading.Timer _logicTimer;
        private readonly object _logicTimerLocker = new object();
        private bool _logicTimerStarted = false;

        private void Screener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            lock (_logicTimerLocker)
            {
                if (_logicTimerStarted)
                {
                    return;
                }

                _logicTimerStarted = true;
                _logicTimer.Change(5000, Timeout.Infinite);
            }
        }

        private void LogicTimerCallback(object state)
        {
            lock (_logicTimerLocker)
            {
                _logicTimerStarted = false;
            }

            Logic();
        }

        #endregion

        #region Full logging

        private void LogFull(string message)
        {
            if (_fullLogIsOn.ValueBool)
            {
                SendNewLogMessage(message, LogMessageType.System);
            }
        }

        private string LastCandleTimeStr(BotTabSimple tab)
        {
            List<Candle> candles = tab.CandlesAll;

            if (candles == null
                || candles.Count == 0)
            {
                return "no candles";
            }

            return candles[^1].TimeStart.ToString("dd.MM.yyyy HH:mm");
        }

        private string PairDescription(BotTabSimple baseSource, BotTabSimple futuresSource, decimal mult)
        {
            return baseSource.Connector?.SecurityName + " / " + futuresSource.Connector?.SecurityName
                + " | mult " + mult
                + " | futBid " + futuresSource.PriceBestBid + " baseAsk " + baseSource.PriceBestAsk
                + " | lastCandle base " + LastCandleTimeStr(baseSource)
                + " fut " + LastCandleTimeStr(futuresSource);
        }

        #endregion

        #region Logic

        private void Logic()
        {
            try
            {
                LogicInternal();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void LogicInternal()
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            DateTime currentTime = GetCurrentServerTime();

            if (currentTime == DateTime.MinValue)
            {
                return;
            }

            if (_tradePeriodsSettings.CanTradeThisTime(currentTime) == false)
            {
                return;
            }

            if (IsShortenedSessionDay(currentTime.Date)
                && currentTime.Hour >= 13)
            {
                return;
            }

            if (_LqdtRegimeIsOn.ValueBool)
            {
                TryResetLqdtByYear(currentTime);
            }

            List<PairInPosition> pairsInPosition = GetPairsInPositions();

            if (pairsInPosition.Count > 1)
            {
                for (int i = 0; i < pairsInPosition.Count; i++)
                {
                    if (HaveClosingPosition(pairsInPosition[i].Base)
                        || HaveClosingPosition(pairsInPosition[i].Futures))
                    {
                        LogFull("More than 1 position. Waiting: closing in progress on "
                            + PairDescription(pairsInPosition[i].Base, pairsInPosition[i].Futures, GetMultByBase(pairsInPosition[i].Base)));
                        return;
                    }
                }

                decimal dev0 = CalculateContango(
                    pairsInPosition[0].Base, pairsInPosition[0].Futures, GetMultByBase(pairsInPosition[0].Base));
                decimal dev1 = CalculateContango(
                    pairsInPosition[1].Base, pairsInPosition[1].Futures, GetMultByBase(pairsInPosition[1].Base));

                if (dev0 > dev1)
                {
                    LogFull("More than 1 position. Closing worst: "
                        + PairDescription(pairsInPosition[1].Base, pairsInPosition[1].Futures, GetMultByBase(pairsInPosition[1].Base))
                        + " | yield " + dev1 + " vs " + dev0);

                    ExitFromPosition(pairsInPosition[1].Base, pairsInPosition[1].Futures, "worst of two");
                }
                else
                {
                    LogFull("More than 1 position. Closing worst: "
                        + PairDescription(pairsInPosition[0].Base, pairsInPosition[0].Futures, GetMultByBase(pairsInPosition[0].Base))
                        + " | yield " + dev0 + " vs " + dev1);

                    ExitFromPosition(pairsInPosition[0].Base, pairsInPosition[0].Futures, "worst of two");
                }

                return;
            }

            List<Pretender> pretenders = GetPretenders();

            if (pairsInPosition.Count > 0)
            {
                PairInPosition pair = pairsInPosition[0];

                if (StartProgram == StartProgram.IsTester)
                {
                    if (TryResetArbPositionByYear(pair.Base, pair.Futures, currentTime))
                    {
                        return;
                    }
                }

                if (TryExitByErrorEntry(pair.Base, pair.Futures))
                {
                    return;
                }

                if (_exitOnBackwardationIsOn.ValueBool
                    && IsBackwardation(pair.Base, pair.Futures))
                {
                    if (currentTime.Hour < 10)
                    {
                        LogFull("Exit by backwardation skipped: too early. Time: " + currentTime.ToString("dd.MM.yyyy HH:mm"));
                        return;
                    }

                    if (pair.Base.CandlesFinishedOnly[^1].TimeStart != currentTime
                        || pair.Futures.CandlesFinishedOnly[^1].TimeStart != currentTime)
                    {
                        return;
                    }

                    LogFull("Exit by backwardation: " + PairDescription(pair.Base, pair.Futures, GetMultByBase(pair.Base)));

                    ExitFromPosition(pair.Base, pair.Futures, "backwardation");
                    return;

                }

                if (TryExitByExpiration(pair.Base, pair.Futures))
                {
                    return;
                }

                TryMovePosition(pair.Base, pair.Futures, pretenders, currentTime);
            }
            else
            {
                if (_LqdtRegimeIsOn.ValueBool)
                {
                    if (HaveGoodPretender(pretenders, currentTime) == false)
                    {
                        TryBuyLqdt();
                        return;
                    }
                }

                TryFirstEntry(pretenders, currentTime);
            }
        }

        private bool IsShortenedSessionDay(DateTime date)
        {
            if (date.Year == 2022
                && date.Month == 3
                && (date.Day == 24
                    || date.Day == 25
                    || date.Day == 28
                    || date.Day == 29
                    || date.Day == 30))
            {
                return true;
            }

            return false;
        }

        private void TryFirstEntry(List<Pretender> pretenders, DateTime serverTime)
        {
            if (pretenders == null
                || pretenders.Count == 0)
            {
                LogFull("First entry: pretenders list is empty");
                return;
            }

            if(this.TimeServer.Hour < 10)
            {
                LogFull("Method TryFirstEntry Error. Time: " + this.TimeServer.Hour);
                return;
            }

            BotTabSimple bestBase = null;
            BotTabSimple bestFutures = null;
            decimal bestYieldAnn = 0;
            decimal bestMult = 1;

            for (int i = 0; i < pretenders.Count; i++)
            {
                decimal yieldAnn = CalculateContango(pretenders[i].Base, pretenders[i].Futures, pretenders[i].Mult);

                LogFull("Entry candidate: " + PairDescription(pretenders[i].Base, pretenders[i].Futures, pretenders[i].Mult)
                    + " | ann " + yieldAnn);

                if (PassLqdtGate(yieldAnn, serverTime, pretenders[i].IsSecondSeries) == false)
                {
                    continue;
                }

                if (yieldAnn > bestYieldAnn)
                {
                    bestYieldAnn = yieldAnn;
                    bestBase = pretenders[i].Base;
                    bestFutures = pretenders[i].Futures;
                    bestMult = pretenders[i].Mult;
                }
            }

            if (bestBase == null)
            {
                LogFull("First entry: no valid pretenders (all yields are 0)");
                return;
            }

            LogFull("First entry: " + PairDescription(bestBase, bestFutures, bestMult) + " | ann " + bestYieldAnn);

            EntryInPositionContango(bestBase, bestFutures);
        }

        private bool TryResetArbPositionByYear(BotTabSimple baseSource, BotTabSimple futuresSource, DateTime currentTime)
        {
            if (currentTime.Hour < 10)
            {
                LogFull("Year reset skipped: too early. Time: " + currentTime.ToString("dd.MM.yyyy HH:mm"));
                return false;
            }

            if(baseSource.CandlesFinishedOnly[^1].TimeStart != currentTime
                || futuresSource.CandlesFinishedOnly[^1].TimeStart != currentTime)
            {
                return false;
            }

            List<Position> basePos = baseSource.PositionsOpenAll;
            List<Position> futPos = futuresSource.PositionsOpenAll;

            if (basePos == null
                || futPos == null
                || basePos.Count != 1
                || futPos.Count != 1)
            {
                return false;
            }

            if (basePos[0].State != PositionStateType.Open
                || futPos[0].State != PositionStateType.Open)
            {
                return false;
            }

            if (currentTime.Year <= basePos[0].TimeOpen.Year
                && currentTime.Year <= futPos[0].TimeOpen.Year)
            {
                return false;
            }

            if (baseSource.IsReadyToTrade == false
                || futuresSource.IsReadyToTrade == false)
            {
                return false;
            }

            decimal baseVolume = basePos[0].OpenVolume;
            decimal futVolume = futPos[0].OpenVolume;

            if (baseVolume <= 0
                || futVolume <= 0)
            {
                return false;
            }

            LogFull("Year reset arb position: " + PairDescription(baseSource, futuresSource, GetMultByBase(baseSource))
                + " | volBase " + baseVolume + " volFut " + futVolume);

            ExitFromPosition(baseSource, futuresSource, "year reset");

            futuresSource.SellAtMarket(futVolume);
            baseSource.BuyAtMarket(baseVolume);

            return true;
        }

        private bool IsBackwardation(BotTabSimple baseSource, BotTabSimple futuresSource)
        {
            List<Candle> baseCandles = baseSource.CandlesAll;
            List<Candle> futCandles = futuresSource.CandlesAll;

            if (baseCandles == null
                || baseCandles.Count == 0
                || futCandles == null
                || futCandles.Count == 0)
            {
                return false;
            }

            Candle lastBaseC = baseCandles[^1];
            Candle lastFutC = futCandles[^1];

            if (lastBaseC.TimeStart != lastFutC.TimeStart)
            {
                return false;
            }

            return lastFutC.Close / GetMultByBase(baseSource) < lastBaseC.Close;
        }

        private bool TryExitByExpiration(BotTabSimple baseSource, BotTabSimple futuresSource)
        {
            int daysToExpiration = (futuresSource.Security.Expiration - futuresSource.TimeServerCurrent).Days;

            if (daysToExpiration <= _daysBeforeExpirationToExit.ValueInt)
            {
                LogFull("Exit by expiration: " + PairDescription(baseSource, futuresSource, GetMultByBase(baseSource))
                    + " | daysToExpiration " + daysToExpiration);

                ExitFromPosition(baseSource, futuresSource, "expiration");
                return true;
            }

            return false;
        }

        private bool TryExitByErrorEntry(BotTabSimple baseSource, BotTabSimple futuresSource)
        {
            List<Position> basePos = baseSource.PositionsOpenAll;
            List<Position> futPos = futuresSource.PositionsOpenAll;

            if (basePos.Count + futPos.Count != 2)
            {
                LogFull("Exit by error entry (one leg): " + PairDescription(baseSource, futuresSource, GetMultByBase(baseSource))
                    + " | basePos " + basePos.Count + " futPos " + futPos.Count);

                ExitFromPosition(baseSource, futuresSource, "error entry");
                return true;
            }

            if (basePos[0].State == PositionStateType.Open
                && futPos[0].State == PositionStateType.Open)
            {
                if (basePos[0].TimeOpen.Date != futPos[0].TimeOpen.Date)
                {
                    LogFull("Exit by error entry (legs opened on different days): "
                        + PairDescription(baseSource, futuresSource, GetMultByBase(baseSource))
                        + " | baseOpen " + basePos[0].TimeOpen.ToString("dd.MM.yyyy HH:mm")
                        + " futOpen " + futPos[0].TimeOpen.ToString("dd.MM.yyyy HH:mm"));

                    ExitFromPosition(baseSource, futuresSource, "error entry: overnight leg");
                    return true;
                }

                if (basePos[0].TimeOpen.Hour < 10
                    || futPos[0].TimeOpen.Hour < 10)
                {
                    LogFull("Exit by error entry (leg opened before 10:00): "
                        + PairDescription(baseSource, futuresSource, GetMultByBase(baseSource))
                        + " | baseOpen " + basePos[0].TimeOpen.ToString("dd.MM.yyyy HH:mm")
                        + " futOpen " + futPos[0].TimeOpen.ToString("dd.MM.yyyy HH:mm"));

                    ExitFromPosition(baseSource, futuresSource, "error entry: early fill");
                    return true;
                }
            }

            return false;
        }

        private void TryMovePosition(BotTabSimple baseInPosition, BotTabSimple futuresInPosition, List<Pretender> pretenders, DateTime serverTime)
        {
            if (pretenders == null
                || pretenders.Count == 0)
            {
                LogFull("Move check: pretenders list is empty. Position: "
                    + PairDescription(baseInPosition, futuresInPosition, GetMultByBase(baseInPosition)));
                return;
            }

            if (this.TimeServer.Hour < 10)
            {
                LogFull("Method TryFirstEntry Error. Time: " + this.TimeServer.Hour);
                return;
            }

            BotTabSimple bestBase = null;
            BotTabSimple bestFutures = null;
            decimal bestYieldAnn = 0;
            decimal bestMult = 1;

            for (int i = 0; i < pretenders.Count; i++)
            {
                decimal yieldAnn = CalculateContango(pretenders[i].Base, pretenders[i].Futures, pretenders[i].Mult);

                LogFull("Move candidate: " + PairDescription(pretenders[i].Base, pretenders[i].Futures, pretenders[i].Mult)
                    + " | ann " + yieldAnn);

                if (PassLqdtGate(yieldAnn, serverTime, pretenders[i].IsSecondSeries) == false)
                {
                    continue;
                }

                if (yieldAnn > bestYieldAnn)
                {
                    bestYieldAnn = yieldAnn;
                    bestBase = pretenders[i].Base;
                    bestFutures = pretenders[i].Futures;
                    bestMult = pretenders[i].Mult;
                }
            }

            if (bestBase == null)
            {
                LogFull("Move check: no valid pretenders (all yields are 0)");
                return;
            }

            decimal curMult = GetMultByBase(baseInPosition);
            decimal curYieldAnn = CalculateContango(baseInPosition, futuresInPosition, curMult);

            if (curYieldAnn >= bestYieldAnn
                || bestYieldAnn <= 0
                || curYieldAnn == 0)
            {
                LogFull("Move check skipped: current ann yield " + curYieldAnn + " best pretender ann yield " + bestYieldAnn);
                return;
            }

            decimal diff = bestYieldAnn - curYieldAnn;

            LogFull("Move decision: current " + PairDescription(baseInPosition, futuresInPosition, curMult)
                + " | ann yield " + curYieldAnn
                + " || best pretender " + PairDescription(bestBase, bestFutures, bestMult)
                + " | ann yield " + bestYieldAnn
                + " || diff " + diff + " need > " + _minYieldDiffToMove.ValueDecimal
                + " => " + (diff > _minYieldDiffToMove.ValueDecimal ? "MOVE" : "HOLD"));

            if (diff > _minYieldDiffToMove.ValueDecimal)
            {
                if(baseInPosition.CandlesFinishedOnly[^1].TimeStart != futuresInPosition.CandlesFinishedOnly[^1].TimeStart
                   || bestBase.CandlesFinishedOnly[^1].TimeStart != bestFutures.CandlesFinishedOnly[^1].TimeStart
                   || bestBase.CandlesFinishedOnly[^1].TimeStart != futuresInPosition.CandlesFinishedOnly[^1].TimeStart)
                {// где-то рассинхрон. не переносим позицию, ждем следующей свечи
                    return;
                }

                ExitFromPosition(baseInPosition, futuresInPosition, "move");
                EntryInPositionContango(bestBase, bestFutures);
            }
        }

        private decimal CalculateContango(BotTabSimple baseSource, BotTabSimple futuresSource, decimal mult)
        {
            List<Candle> baseCandles = baseSource.CandlesAll;
            List<Candle> futCandles = futuresSource.CandlesAll;

            if (baseCandles == null
                || baseCandles.Count == 0
                || futCandles == null
                || futCandles.Count == 0)
            {
                return 0;
            }

            Candle lastBaseC = baseCandles[^1];
            Candle lastFutC = futCandles[^1];

            if (lastBaseC.TimeStart != lastFutC.TimeStart)
            {
                return 0;
            }

            if (lastFutC.Close / mult <= lastBaseC.Close)
            {
                return 0;
            }

            if (baseSource.PriceBestAsk == 0)
            {
                return 0;
            }

            if (futuresSource.Security == null
                || futuresSource.Security.Expiration == DateTime.MinValue)
            {
                return 0;
            }

            decimal deviation = futuresSource.PriceBestBid / mult - baseSource.PriceBestAsk;
            deviation = deviation / (baseSource.PriceBestAsk / 100);

            int daysToExpiration = (futuresSource.Security.Expiration - futuresSource.TimeServerCurrent).Days;

            decimal yieldAnn = 0;

            if (daysToExpiration > 0)
            {
                yieldAnn = deviation * 365 / daysToExpiration;
            }

            return yieldAnn;
        }

        #endregion


        #region LQDT branch

        private bool PassLqdtGate(decimal yieldAnn, DateTime serverTime, bool isSecondSeries = false)
        {
            if (_LqdtRegimeIsOn.ValueBool == false)
            {
                return true;
            }

            decimal LqdtYield = GetLqdtYieldAnn(serverTime);

            if(LqdtYield <= 0)
            {
                LogFull("ERROR. LQDT profit is Zero!!! ");
                return false;
            }

            decimal minDiffOverLqdt = _minYieldDiffOverLqdt.ValueDecimal;

            if (isSecondSeries)
            {
                minDiffOverLqdt = _minYieldDiffOverLqdtSeries2.ValueDecimal;
            }

            return yieldAnn > LqdtYield + minDiffOverLqdt;
        }

        private DateTime _lastLqdtGetTime;

        private decimal _lqdtProfitValue;

        private decimal GetLqdtYieldAnn(DateTime serverTime)
        {
            if(_lastLqdtGetTime == serverTime)
            {
                return _lqdtProfitValue;
            }

            List<Candle> candles = _tabLqdt.CandlesAll;

            if (candles == null
                || candles.Count < 2)
            {
                return 0;
            }

            Candle last = candles[^1];

            DateTime border;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                border = last.TimeStart.AddDays(-7);
            }
            else
            {
                border = last.TimeStart.AddDays(-_LqdtYieldDays.ValueInt);
            }

            decimal oldPrice = 0;
            int daysReal = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                if (candles[i].TimeStart <= border)
                {
                    oldPrice = candles[i].Close;
                    daysReal = (last.TimeStart - candles[i].TimeStart).Days;
                    break;
                }
            }

            if (oldPrice == 0)
            {
                oldPrice = candles[0].Close;
                daysReal = (last.TimeStart - candles[0].TimeStart).Days;
            }

            if (oldPrice == 0
                || daysReal <= 0)
            {
                return 0;
            }

            _lqdtProfitValue = (last.Close / oldPrice - 1) * 365 / daysReal * 100;

            _lastLqdtGetTime = serverTime;
            return _lqdtProfitValue;
        }

        private bool HaveGoodPretender(List<Pretender> pretenders,DateTime serverTime)
        {
            for (int i = 0; pretenders != null && i < pretenders.Count; i++)
            {
                decimal yieldAnn = CalculateContango(pretenders[i].Base, pretenders[i].Futures, pretenders[i].Mult);

                if (yieldAnn > 0
                    && PassLqdtGate(yieldAnn, serverTime, pretenders[i].IsSecondSeries))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HaveAnyPositions()
        {
            List<IIBotTab> tabs = GetTabs();

            for (int i = 0; tabs != null && i < tabs.Count; i++)
            {
                if (tabs[i] is BotTabSimple simple)
                {
                    if (simple.PositionsOpenAll != null
                        && simple.PositionsOpenAll.Count > 0)
                    {
                        return true;
                    }
                }
                else if (tabs[i] is BotTabScreener screener)
                {
                    for (int j = 0; j < screener.Tabs.Count; j++)
                    {
                        if (screener.Tabs[j].PositionsOpenAll != null
                            && screener.Tabs[j].PositionsOpenAll.Count > 0)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void TryBuyLqdt()
        {
            if (HaveAnyPositions())
            {
                return;
            }

            BuyLqdtOnFreeMoney();
        }

        private void CloseLqdtIfAny()
        {
            Position LqdtPos = GetLqdtPosition();

            if (LqdtPos == null
                || LqdtPos.State != PositionStateType.Open
                || LqdtPos.OpenVolume <= 0)
            {
                return;
            }

            if (_tabLqdt.IsReadyToTrade == false)
            {
                return;
            }

            LogFull("LQDT: sell at arb entry. Volume " + LqdtPos.OpenVolume);

            _tabLqdt.CloseAtMarket(LqdtPos, LqdtPos.OpenVolume);
        }

        private Position GetLqdtPosition()
        {
            List<Position> positions = _tabLqdt.PositionsOpenAll;

            if (positions == null
                || positions.Count == 0)
            {
                return null;
            }

            return positions[0];
        }

        private void TryResetLqdtByYear(DateTime currentTime)
        {
            Position LqdtPos = GetLqdtPosition();

            if (LqdtPos == null)
            {
                return;
            }

            if (currentTime.Year <= LqdtPos.TimeOpen.Year)
            {
                return;
            }

            if (LqdtPos.State != PositionStateType.Open
                || LqdtPos.OpenVolume <= 0)
            {
                return;
            }

            if (_tabLqdt.IsReadyToTrade == false)
            {
                return;
            }

            LogFull("LQDT: year reset. Close volume " + LqdtPos.OpenVolume);

            _tabLqdt.CloseAtMarket(LqdtPos, LqdtPos.OpenVolume);
        }

        private void BuyLqdtOnFreeMoney()
        {
            if (_tabLqdt.IsReadyToTrade == false
                || _tabLqdt.PriceBestAsk == 0
                || _tabLqdt.Security == null)
            {
                return;
            }

            decimal freeMoney = GetFreeMoney();

            if (freeMoney <= 1000)
            {
                return;
            }

            freeMoney = freeMoney * _LqdtVolumePercent.ValueDecimal / 100;

            decimal volume = freeMoney / _tabLqdt.PriceBestAsk;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                if (_tabLqdt.Security.Lot > 1)
                {
                    volume = freeMoney / (_tabLqdt.PriceBestAsk * _tabLqdt.Security.Lot);
                }

                volume = Math.Round(volume, _tabLqdt.Security.DecimalsVolume);
            }
            else
            {
                volume = Math.Round(volume, 6);
            }

            if (volume <= 0)
            {
                return;
            }

            LogFull("LQDT: buy on free money " + freeMoney + " volume " + volume);

            _tabLqdt.BuyAtMarket(volume);
        }

        private decimal GetFreeMoney()
        {
            Portfolio portfolio = _tabLqdt.Portfolio;

            if (portfolio == null)
            {
                return 0;
            }

            if (StartProgram == StartProgram.IsOsTrader)
            {
                List<PositionOnBoard> positions = portfolio.GetPositionOnBoard();

                if (positions == null)
                {
                    return 0;
                }

                for (int i = 0; i < positions.Count; i++)
                {
                    if (positions[i].SecurityNameCode == "rub"
                        || positions[i].SecurityNameCode == "RUB")
                    {
                        return positions[i].ValueCurrent;
                    }
                }

                return 0;
            }

            decimal moneyInPositions = 0;

            List<IIBotTab> tabs = GetTabs();

            for (int i = 0; tabs != null && i < tabs.Count; i++)
            {
                if (tabs[i] is BotTabSimple simple)
                {
                    List<Position> positions = simple.PositionsOpenAll;

                    for (int j = 0; positions != null && j < positions.Count; j++)
                    {
                        moneyInPositions += positions[j].OpenVolume * positions[j].EntryPrice * positions[j].Lots;
                    }
                }
                else if (tabs[i] is BotTabScreener screener)
                {
                    for (int j = 0; j < screener.Tabs.Count; j++)
                    {
                        List<Position> positions = screener.Tabs[j].PositionsOpenAll;

                        for (int k = 0; positions != null && k < positions.Count; k++)
                        {
                            moneyInPositions += positions[k].OpenVolume * positions[k].EntryPrice * positions[k].Lots;
                        }
                    }
                }
            }

            return portfolio.ValueCurrent - moneyInPositions;
        }

        #endregion

        #region Sources

        private BotTabSimple _base1;
        private BotTabScreener _futs1;

        private BotTabSimple _base2;
        private BotTabScreener _futs2;

        private BotTabSimple _base3;
        private BotTabScreener _futs3;

        private BotTabSimple _base4;
        private BotTabScreener _futs4;

        private BotTabSimple _base5;
        private BotTabScreener _futs5;

        private BotTabSimple _base6;
        private BotTabScreener _futs6;

        private BotTabSimple _base7;
        private BotTabScreener _futs7;

        private BotTabSimple _base8;
        private BotTabScreener _futs8;

        private BotTabSimple _base9;
        private BotTabScreener _futs9;

        private BotTabSimple _base10;
        private BotTabScreener _futs10;

        private BotTabSimple _tabLqdt;

        private void CreateSources()
        {
            _base1 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs1 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base2 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs2 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base3 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs3 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base4 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs4 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base5 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs5 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base6 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs6 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base7 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs7 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base8 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs8 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base9 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs9 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _base10 = (BotTabSimple)TabCreate(BotTabType.Simple);
            _futs10 = (BotTabScreener)TabCreate(BotTabType.Screener);

            _tabLqdt = (BotTabSimple)TabCreate(BotTabType.Simple);
        }

        private DateTime GetCurrentServerTime()
        {
            DateTime result = DateTime.MinValue;

            result = MaxDateTime(result, LastCandleTime(_base1));
            result = MaxDateTime(result, LastCandleTime(_base2));
            result = MaxDateTime(result, LastCandleTime(_base3));
            result = MaxDateTime(result, LastCandleTime(_base4));
            result = MaxDateTime(result, LastCandleTime(_base5));
            result = MaxDateTime(result, LastCandleTime(_base6));
            result = MaxDateTime(result, LastCandleTime(_base7));
            result = MaxDateTime(result, LastCandleTime(_base8));
            result = MaxDateTime(result, LastCandleTime(_base9));
            result = MaxDateTime(result, LastCandleTime(_base10));

            if (result == DateTime.MinValue)
            {
                result = this.TimeServer;
            }

            return result;
        }

        private DateTime LastCandleTime(BotTabSimple tab)
        {
            List<Candle> candles = tab?.CandlesFinishedOnly;

            if (candles == null
                || candles.Count == 0)
            {
                return DateTime.MinValue;
            }

            return candles[^1].TimeStart;
        }

        private DateTime MaxDateTime(DateTime a, DateTime b)
        {
            return a > b ? a : b;
        }

        private decimal GetMultByBase(BotTabSimple baseSource)
        {
            if (_multRegime.ValueString == "Auto"
                && baseSource.Security != null)
            {
                DateTime time = baseSource.TimeServerCurrent;

                if (time == DateTime.MinValue)
                {
                    time = DateTime.Now;
                }

                return GetAutoMult(baseSource.Security, time);
            }

            if (baseSource == _base1) return _futuresMult1.ValueDecimal;
            if (baseSource == _base2) return _futuresMult2.ValueDecimal;
            if (baseSource == _base3) return _futuresMult3.ValueDecimal;
            if (baseSource == _base4) return _futuresMult4.ValueDecimal;
            if (baseSource == _base5) return _futuresMult5.ValueDecimal;
            if (baseSource == _base6) return _futuresMult6.ValueDecimal;
            if (baseSource == _base7) return _futuresMult7.ValueDecimal;
            if (baseSource == _base8) return _futuresMult8.ValueDecimal;
            if (baseSource == _base9) return _futuresMult9.ValueDecimal;
            if (baseSource == _base10) return _futuresMult10.ValueDecimal;

            return 1;
        }

        #endregion

        #region Pairs creation

        private List<PairInPosition> GetPairsInPositions()
        {
            List<PairInPosition> result = new List<PairInPosition>();

            AddPairsInPositionsBySecurity(_base1, _futs1, result);
            AddPairsInPositionsBySecurity(_base2, _futs2, result);
            AddPairsInPositionsBySecurity(_base3, _futs3, result);
            AddPairsInPositionsBySecurity(_base4, _futs4, result);
            AddPairsInPositionsBySecurity(_base5, _futs5, result);
            AddPairsInPositionsBySecurity(_base6, _futs6, result);
            AddPairsInPositionsBySecurity(_base7, _futs7, result);
            AddPairsInPositionsBySecurity(_base8, _futs8, result);
            AddPairsInPositionsBySecurity(_base9, _futs9, result);
            AddPairsInPositionsBySecurity(_base10, _futs10, result);

            return result;
        }

        private void AddPairsInPositionsBySecurity(
            BotTabSimple baseSource, BotTabScreener screener, List<PairInPosition> result)
        {
            if (string.IsNullOrEmpty(baseSource.Connector?.SecurityName))
            {
                return;
            }

            if (baseSource.PositionsOpenAll.Count == 0)
            {
                return;
            }

            for (int i = 0; i < screener.Tabs.Count; i++)
            {
                BotTabSimple curTab = screener.Tabs[i];

                if (curTab.PositionsOpenAll.Count > 0)
                {
                    result.Add((baseSource, curTab));
                }
            }
        }

        private List<Pretender> GetPretenders()
        {
            List<Pretender> result = new List<Pretender>();

            DateTime timeServer = this.TimeServer;

            AddPretenderBySecurity(_base1, _futs1, GetMultByBase(_base1), result, timeServer);
            AddPretenderBySecurity(_base2, _futs2, GetMultByBase(_base2), result, timeServer);
            AddPretenderBySecurity(_base3, _futs3, GetMultByBase(_base3), result, timeServer);
            AddPretenderBySecurity(_base4, _futs4, GetMultByBase(_base4), result, timeServer);
            AddPretenderBySecurity(_base5, _futs5, GetMultByBase(_base5), result, timeServer);
            AddPretenderBySecurity(_base6, _futs6, GetMultByBase(_base6), result, timeServer);
            AddPretenderBySecurity(_base7, _futs7, GetMultByBase(_base7), result, timeServer);
            AddPretenderBySecurity(_base8, _futs8, GetMultByBase(_base8), result, timeServer);
            AddPretenderBySecurity(_base9, _futs9, GetMultByBase(_base9), result, timeServer);
            AddPretenderBySecurity(_base10, _futs10, GetMultByBase(_base10), result, timeServer);

            return result;
        }

        private bool CanTradeThisPair(BotTabSimple baseSource)
        {
            if (baseSource == _base1) return _tradePair1.ValueBool;
            if (baseSource == _base2) return _tradePair2.ValueBool;
            if (baseSource == _base3) return _tradePair3.ValueBool;
            if (baseSource == _base4) return _tradePair4.ValueBool;
            if (baseSource == _base5) return _tradePair5.ValueBool;
            if (baseSource == _base6) return _tradePair6.ValueBool;
            if (baseSource == _base7) return _tradePair7.ValueBool;
            if (baseSource == _base8) return _tradePair8.ValueBool;
            if (baseSource == _base9) return _tradePair9.ValueBool;
            if (baseSource == _base10) return _tradePair10.ValueBool;

            return false;
        }

        private bool CanTradeSecondSeries(BotTabSimple baseSource)
        {
            if (baseSource == _base1) return _tradeSecondSeries1.ValueBool;
            if (baseSource == _base2) return _tradeSecondSeries2.ValueBool;
            if (baseSource == _base3) return _tradeSecondSeries3.ValueBool;
            if (baseSource == _base4) return _tradeSecondSeries4.ValueBool;
            if (baseSource == _base5) return _tradeSecondSeries5.ValueBool;
            if (baseSource == _base6) return _tradeSecondSeries6.ValueBool;
            if (baseSource == _base7) return _tradeSecondSeries7.ValueBool;
            if (baseSource == _base8) return _tradeSecondSeries8.ValueBool;
            if (baseSource == _base9) return _tradeSecondSeries9.ValueBool;
            if (baseSource == _base10) return _tradeSecondSeries10.ValueBool;

            return false;
        }

        private void AddPretenderBySecurity(
             BotTabSimple baseSource, BotTabScreener screener, decimal mult, List<Pretender> result, DateTime serverTimeToTester)
        {
            if (string.IsNullOrEmpty(baseSource.Connector?.SecurityName))
            {
                return;
            }

            if (CanTradeThisPair(baseSource) == false)
            {
                return;
            }

            if (baseSource.PositionsOpenAll.Count > 0)
            {
                return;
            }

            DateTime time = baseSource.TimeServerCurrent;

            List<BotTabSimple> series = GetNearestSeries(screener, time, screener.Tabs.Count);

            int seriesNumber = 0;

            for (int i = 0; i < series.Count; i++)
            {
                BotTabSimple curFutures = series[i];

                int daysToExpiration = (curFutures.Security.Expiration - time).Days;

                if (daysToExpiration <= _minDaysToExpiration.ValueInt)
                {
                    continue;
                }

                if (seriesNumber == 0)
                {
                    if (daysToExpiration > 100)
                    {
                        break;
                    }
                }
                else if (seriesNumber == 1)
                {
                    if (CanTradeSecondSeries(baseSource) == false)
                    {
                        break;
                    }

                    if (daysToExpiration > 180)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }

                List<Candle> baseCandles = baseSource.CandlesAll;
                List<Candle> futCandles = curFutures.CandlesAll;

                if (baseCandles == null
                    || baseCandles.Count == 0
                    || futCandles == null
                    || futCandles.Count == 0)
                {
                    continue;
                }

                if (baseCandles[^1].TimeStart != futCandles[^1].TimeStart)
                {
                    continue;
                }

                if(StartProgram == StartProgram.IsTester
                    || StartProgram == StartProgram.IsOsOptimizer)
                {
                    if(baseCandles[^1].TimeStart != serverTimeToTester)
                    {
                        continue;
                    }
                }

                result.Add((baseSource, curFutures, mult, seriesNumber == 1));

                seriesNumber++;

                if(seriesNumber == 2)
                {
                    break;
                }
            }
        }

        #endregion

        #region Position execution logic

        private void EntryInPositionContango(BotTabSimple baseSource, BotTabSimple futuresSource)
        {
            if (_LqdtRegimeIsOn.ValueBool)
            {
                CloseLqdtIfAny();
            }

            if (baseSource.CandlesFinishedOnly[^1].TimeStart != futuresSource.CandlesFinishedOnly[^1].TimeStart)
            {
                LogFull("ENTRY ERROR. Time is not equal!!!!!");
                return;
            }

            decimal volumeFutures = GetVolume(futuresSource);

            decimal baseLot = 1;

            if (baseSource.Security != null
                && baseSource.Security.Lot > 1)
            {
                baseLot = baseSource.Security.Lot;
            }

            decimal volumeBase = volumeFutures * GetMultByBase(baseSource) / baseLot;

            if (StartProgram == StartProgram.IsOsTrader
                && baseSource.Security != null)
            {
                volumeBase = Math.Round(volumeBase, baseSource.Security.DecimalsVolume);
            }
            else
            {
                volumeBase = Math.Round(volumeBase, 6);
            }

            decimal entryYieldAnn = CalculateContango(baseSource, futuresSource, GetMultByBase(baseSource));

            LogFull("ENTRY: " + PairDescription(baseSource, futuresSource, GetMultByBase(baseSource))
                + " | ann " + entryYieldAnn
                + " | lqdtAnn " + GetLqdtYieldAnn(futuresSource.CandlesFinishedOnly[^1].TimeStart)
                + " | volFut " + volumeFutures + " volBase " + volumeBase);

            if (volumeFutures <= 0
                || volumeBase <= 0)
            {
                LogFull("ENTRY skipped: zero or negative volume. "
                    + PairDescription(baseSource, futuresSource, GetMultByBase(baseSource))
                    + " | volFut " + volumeFutures + " volBase " + volumeBase);
                return;
            }

            futuresSource.SellAtMarket(volumeFutures);
            baseSource.BuyAtMarket(volumeBase);
        }

        private void ExitFromPosition(BotTabSimple baseSource, BotTabSimple futuresSource, string reason = "")
        {
            if (baseSource.CandlesFinishedOnly[^1].TimeStart != futuresSource.CandlesFinishedOnly[^1].TimeStart)
            {
                LogFull("EXIT ERROR. Time is not equal!!!!!");
                return;
            }

            List<Position> positionsFut = futuresSource.PositionsOpenAll;
            List<Position> positionsBase = baseSource.PositionsOpenAll;

            LogFull("EXIT: " + PairDescription(baseSource, futuresSource, GetMultByBase(baseSource))
                + " | futPosCount " + positionsFut.Count + " basePosCount " + positionsBase.Count);

            LogPositionClose(baseSource, futuresSource, reason);

            if (positionsFut.Count > 0)
            {
                ClosePosAtMarket(futuresSource, positionsFut[0]);
            }
            if (positionsBase.Count > 0)
            {
                ClosePosAtMarket(baseSource, positionsBase[0]);
            }
        }

        private void LogPositionClose(BotTabSimple baseSource, BotTabSimple futuresSource, string reason)
        {
            if (_fullLogIsOn.ValueBool == false)
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                List<Position> positionsBase = baseSource.PositionsOpenAll;
                List<Position> positionsFut = futuresSource.PositionsOpenAll;

                if (positionsBase == null
                    || positionsFut == null
                    || positionsBase.Count == 0
                    || positionsFut.Count == 0)
                {
                    return;
                }

                Position basePos = positionsBase[0];
                Position futPos = positionsFut[0];

                if (basePos.State != PositionStateType.Open
                    || futPos.State != PositionStateType.Open)
                {
                    return;
                }

                decimal mult = GetMultByBase(baseSource);

                decimal baseExitPrice = baseSource.PriceBestBid;
                decimal futExitPrice = futuresSource.PriceBestAsk;

                decimal spreadIn = 0;

                if (basePos.EntryPrice != 0)
                {
                    spreadIn = (futPos.EntryPrice / mult - basePos.EntryPrice) / (basePos.EntryPrice / 100);
                }

                decimal spreadOut = 0;

                if (baseExitPrice != 0)
                {
                    spreadOut = (futExitPrice / mult - baseExitPrice) / (baseExitPrice / 100);
                }

                int daysInPosition = (futuresSource.TimeServerCurrent - basePos.TimeOpen).Days;

                decimal totalProfit = basePos.ProfitPortfolioAbs + futPos.ProfitPortfolioAbs;

                string message =
                    "CLOSE " + reason + ": " + baseSource.Connector?.SecurityName + " / " + futuresSource.Connector?.SecurityName
                    + " | open " + basePos.TimeOpen.ToString("dd.MM.yyyy HH:mm")
                    + " close " + futuresSource.TimeServerCurrent.ToString("dd.MM.yyyy HH:mm")
                    + " | base in " + basePos.EntryPrice + " out " + baseExitPrice + " profit " + Math.Round(basePos.ProfitPortfolioAbs, 0)
                    + " | fut in " + futPos.EntryPrice + " out " + futExitPrice + " profit " + Math.Round(futPos.ProfitPortfolioAbs, 0)
                    + " | total " + Math.Round(totalProfit, 0)
                    + " | spreadIn " + Math.Round(spreadIn, 2) + "% spreadOut " + Math.Round(spreadOut, 2) + "%"
                    + " | days " + daysInPosition;

                SendNewLogMessage(message, LogMessageType.System);

                if (totalProfit < 0)
                {
                    SendNewLogMessage(message, LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ClosePosAtMarket(BotTabSimple tab, Position pos)
        {
            if (tab.IsReadyToTrade == false)
            {
                return;
            }

            if (pos.State != PositionStateType.Open)
            {
                return;
            }

            if (pos.OpenVolume <= 0)
            {
                return;
            }

            tab.CloseAtMarket(pos, pos.OpenVolume);
        }

        private bool HaveClosingPosition(BotTabSimple tab)
        {
            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State == PositionStateType.Closing)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                        && tab.Security.PriceStep != tab.Security.PriceStepCost
                        && tab.PriceBestAsk != 0
                        && tab.Security.PriceStep != 0
                        && tab.Security.PriceStepCost != 0)
                    {
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }

                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }

        #region Monitor table

        private WindowsFormsHost _hostTable;
        private DataGridView _tableDataGrid;
        private System.Threading.Timer _monitorTimer;
        private bool _monitorUpdateInProgress = false;
        private List<BondMonitorRow> _monitorRows = new List<BondMonitorRow>();

        private void CreateColumnsTable()
        {
            try
            {
                if (MainWindow.GetDispatcher.CheckAccess() == false)
                {
                    MainWindow.GetDispatcher.Invoke(new Action(CreateColumnsTable));
                    return;
                }

                _hostTable = new WindowsFormsHost();

                _tableDataGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                       DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
                _tableDataGrid.ScrollBars = ScrollBars.Vertical;
                _tableDataGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                _tableDataGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                _tableDataGrid.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                DataGridViewTextBoxCell cellParam0 = new DataGridViewTextBoxCell();
                cellParam0.Style = _tableDataGrid.DefaultCellStyle;
                cellParam0.Style.WrapMode = DataGridViewTriState.True;

                DataGridViewColumn newColumn0 = new DataGridViewColumn();
                newColumn0.CellTemplate = cellParam0;
                newColumn0.HeaderText = "Stock";
                _tableDataGrid.Columns.Add(newColumn0);
                newColumn0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn1 = new DataGridViewColumn();
                newColumn1.CellTemplate = cellParam0;
                newColumn1.HeaderText = "Mult";
                _tableDataGrid.Columns.Add(newColumn1);
                newColumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn2 = new DataGridViewColumn();
                newColumn2.CellTemplate = cellParam0;
                newColumn2.HeaderText = "Series 1";
                _tableDataGrid.Columns.Add(newColumn2);
                newColumn2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumnSeries2 = new DataGridViewColumn();
                newColumnSeries2.CellTemplate = cellParam0;
                newColumnSeries2.HeaderText = "Series 2";
                _tableDataGrid.Columns.Add(newColumnSeries2);
                newColumnSeries2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _tableDataGrid.DataError += _tableDataGrid_DataError;
                _tableDataGrid.CellClick += _tableDataGrid_CellClick;
                _tableDataGrid.CellEndEdit += _tableDataGrid_CellEndEdit;

                _hostTable.Child = _tableDataGrid;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _tableDataGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void _tableDataGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if (row < 0
                    || row >= _monitorRows.Count)
                {
                    return;
                }

                if (column == 0)
                {
                    ShowChartForTab(_monitorRows[row].Base);
                }
                else if (column == 2)
                {
                    ShowFuturesChart(_monitorRows[row], 0);
                }
                else if (column == 3)
                {
                    ShowFuturesChart(_monitorRows[row], 1);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _tableDataGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if (row < 0
                    || row >= _monitorRows.Count
                    || column != 1)
                {
                    return;
                }

                object value = _tableDataGrid.Rows[row].Cells[column].Value;

                if (value == null)
                {
                    return;
                }

                decimal newMult = value.ToString().ToDecimal();

                if (newMult <= 0)
                {
                    return;
                }

                SetMultByBase(_monitorRows[row].Base, newMult);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ShowChartForTab(BotTabSimple tab)
        {
            try
            {
                if (tab == null)
                {
                    return;
                }

                ActiveTab = tab;
                ShowChartDialog();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ShowFuturesChart(BondMonitorRow rowData, int seriesIndex = 0)
        {
            if (rowData.Futs == null)
            {
                ShowChartForTab(rowData.Base);
                return;
            }

            if (rowData.Series.Count <= seriesIndex)
            {
                return;
            }

            for (int i = 0; i < rowData.Futs.Tabs.Count; i++)
            {
                if (rowData.Futs.Tabs[i] == rowData.Series[seriesIndex].Tab)
                {
                    rowData.Futs.ShowChart(i);
                    return;
                }
            }
        }

        private void MonitorTimerCallback(object state)
        {
            try
            {
                if (_monitorUpdateInProgress)
                {
                    return;
                }

                _monitorUpdateInProgress = true;

                RefreshMonitorData();
                UpdateTable();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
            finally
            {
                _monitorUpdateInProgress = false;

                try
                {
                    int interval = _tableUpdateIntervalSec.ValueInt;

                    if (interval < 1)
                    {
                        interval = 1;
                    }

                    _monitorTimer?.Change(interval * 1000, Timeout.Infinite);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void RefreshMonitorData()
        {
            List<BondMonitorRow> rows = new List<BondMonitorRow>();

            AddBondMonitorRow(_base1, _futs1, rows);
            AddBondMonitorRow(_base2, _futs2, rows);
            AddBondMonitorRow(_base3, _futs3, rows);
            AddBondMonitorRow(_base4, _futs4, rows);
            AddBondMonitorRow(_base5, _futs5, rows);
            AddBondMonitorRow(_base6, _futs6, rows);
            AddBondMonitorRow(_base7, _futs7, rows);
            AddBondMonitorRow(_base8, _futs8, rows);
            AddBondMonitorRow(_base9, _futs9, rows);
            AddBondMonitorRow(_base10, _futs10, rows);

            AddLqdtMonitorRow(rows);

            _monitorRows = rows;
        }

        private void AddLqdtMonitorRow(List<BondMonitorRow> rows)
        {
            if (_tabLqdt == null
                || string.IsNullOrEmpty(_tabLqdt.Connector?.SecurityName))
            {
                return;
            }

            BondMonitorRow newRow = new BondMonitorRow();
            newRow.Base = _tabLqdt;
            newRow.BaseName = "LQDT";

            SetTabPosInfo(_tabLqdt, newRow);

            SeriesInfo info = new SeriesInfo();
            info.Name = "LQDT";
            info.YieldPercent = GetLqdtYieldAnn(GetCurrentServerTime());

            newRow.Series.Add(info);

            rows.Add(newRow);
        }

        private void AddBondMonitorRow(BotTabSimple baseSource, BotTabScreener screener, List<BondMonitorRow> rows)
        {
            if (string.IsNullOrEmpty(baseSource.Connector?.SecurityName))
            {
                return;
            }

            BondMonitorRow newRow = new BondMonitorRow();
            newRow.Base = baseSource;
            newRow.Futs = screener;
            newRow.BaseName = baseSource.Connector.SecurityName;

            SetTabPosInfo(baseSource, newRow);

            DateTime time = baseSource.TimeServerCurrent;

            if (time == DateTime.MinValue)
            {
                rows.Add(newRow);
                return;
            }

            decimal mult = GetMultByBase(baseSource);

            List<BotTabSimple> nearestSeries = GetNearestSeries(screener, time, 2);

            for (int i = 0; i < nearestSeries.Count; i++)
            {
                BotTabSimple seriesTab = nearestSeries[i];

                SeriesInfo info = new SeriesInfo();
                info.Tab = seriesTab;
                info.Name = seriesTab.Connector?.SecurityName;
                info.Expiration = seriesTab.Security.Expiration;
                info.DaysToExpiration = (info.Expiration - time).Days;
                info.YieldPercent = CalculateContangoForMonitor(baseSource, seriesTab, mult, info.DaysToExpiration);

                SetTabPosInfo(seriesTab, info);

                newRow.Series.Add(info);
            }

            rows.Add(newRow);
        }

        private void SetTabPosInfo(BotTabSimple tab, BondMonitorRow row)
        {
            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State == PositionStateType.Opening
                    || positions[i].State == PositionStateType.Open
                    || positions[i].State == PositionStateType.Closing)
                {
                    row.BaseHasPosition = true;
                    row.BasePosVolume = positions[i].OpenVolume;
                    row.BasePosSide = positions[i].Direction;
                    return;
                }
            }
        }

        private void SetTabPosInfo(BotTabSimple tab, SeriesInfo info)
        {
            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State == PositionStateType.Opening
                    || positions[i].State == PositionStateType.Open
                    || positions[i].State == PositionStateType.Closing)
                {
                    info.HasPosition = true;
                    info.PosVolume = positions[i].OpenVolume;
                    info.PosSide = positions[i].Direction;
                    return;
                }
            }
        }

        private List<BotTabSimple> GetNearestSeries(BotTabScreener screener, DateTime time, int count)
        {
            List<BotTabSimple> result = new List<BotTabSimple>();

            for (int i = 0; i < screener.Tabs.Count; i++)
            {
                BotTabSimple curTab = screener.Tabs[i];

                if (curTab.Security == null
                    || curTab.Security.Expiration == DateTime.MinValue)
                {
                    continue;
                }

                int daysToExpiration = (curTab.Security.Expiration - time).Days;

                if (daysToExpiration <= 0)
                {
                    continue;
                }

                result.Add(curTab);
            }

            if (result.Count > 1)
            {
                result = result.OrderBy(tab => tab.Security.Expiration).ToList();
            }

            if (result.Count > count)
            {
                result = result.GetRange(0, count);
            }

            return result;
        }

        private decimal CalculateContangoForMonitor(BotTabSimple baseSource, BotTabSimple futuresSource, decimal mult, int daysToExpiration)
        {
            if (baseSource.PriceBestAsk == 0
                || futuresSource.PriceBestBid == 0)
            {
                return 0;
            }

            decimal deviation = futuresSource.PriceBestBid / mult - baseSource.PriceBestAsk;
            deviation = deviation / (baseSource.PriceBestAsk / 100);

            decimal yieldAnn = 0;

            if (daysToExpiration > 0)
            {
                yieldAnn = deviation * 365 / daysToExpiration;
            }

            return yieldAnn;
        }

        private void UpdateTable()
        {
            // 0 Stock
            // 1 Mult
            // 2 Series 1
            // 3 Series 2

            try
            {
                if (_tableDataGrid.InvokeRequired)
                {
                    _tableDataGrid.Invoke(new Action(UpdateTable));
                    return;
                }

                bool needRebuild = _tableDataGrid.Rows.Count != _monitorRows.Count;

                if (needRebuild == false)
                {
                    for (int i = 0; i < _monitorRows.Count; i++)
                    {
                        object cellValue = _tableDataGrid.Rows[i].Cells[0].Value;

                        if (cellValue == null
                            || (cellValue.ToString() != _monitorRows[i].BaseName
                                && cellValue.ToString().StartsWith(_monitorRows[i].BaseName + " (") == false))
                        {
                            needRebuild = true;
                            break;
                        }
                    }
                }

                if (needRebuild)
                {
                    _tableDataGrid.Rows.Clear();

                    for (int i = 0; i < _monitorRows.Count; i++)
                    {
                        _tableDataGrid.Rows.Add(GetRow(_monitorRows[i]));
                    }

                    return;
                }

                for (int i = 0; i < _monitorRows.Count; i++)
                {
                    DataGridViewRow currentRow = _tableDataGrid.Rows[i];
                    DataGridViewRow newRow = GetRow(_monitorRows[i]);

                    for (int col = 0; col <= 3; col++)
                    {
                        if (currentRow.Cells[col].Value == null
                            || currentRow.Cells[col].Value.ToString() != newRow.Cells[col].Value.ToString())
                        {
                            if (col == 1 && currentRow.Cells[col].IsInEditMode)
                            {
                                continue;
                            }

                            currentRow.Cells[col].Value = newRow.Cells[col].Value;
                        }

                        if (currentRow.Cells[col].Style.ForeColor != newRow.Cells[col].Style.ForeColor)
                        {
                            currentRow.Cells[col].Style.ForeColor = newRow.Cells[col].Style.ForeColor;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private DataGridViewRow GetRow(BondMonitorRow data)
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;

            if (data.BaseHasPosition)
            {
                row.Cells[^1].Value = data.BaseName + " (" + data.BasePosVolume + ")";
                row.Cells[^1].Style.ForeColor = data.BasePosSide == Side.Buy
                    ? System.Drawing.Color.LimeGreen
                    : System.Drawing.Color.OrangeRed;
            }
            else
            {
                row.Cells[^1].Value = data.BaseName;
            }

            row.Cells.Add(new DataGridViewTextBoxCell());

            if (data.Futs == null)
            {
                row.Cells[^1].ReadOnly = true;
                row.Cells[^1].Value = "";
            }
            else
            {
                row.Cells[^1].ReadOnly = false;
                row.Cells[^1].Value = GetMultByBase(data.Base);
            }

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;

            if (data.Series.Count > 0)
            {
                string text;

                if (data.Futs == null)
                {
                    text = "LQDT  " + Math.Round(data.Series[0].YieldPercent, 2) + "% ann";
                }
                else
                {
                    text = data.Series[0].Name
                        + "  " + Math.Round(data.Series[0].YieldPercent, 1) + "%";
                }

                if (data.Series[0].HasPosition)
                {
                    text += " (" + data.Series[0].PosVolume + ")";
                    row.Cells[^1].Style.ForeColor = data.Series[0].PosSide == Side.Buy
                        ? System.Drawing.Color.LimeGreen
                        : System.Drawing.Color.OrangeRed;
                }

                row.Cells[^1].Value = text;
            }
            else
            {
                row.Cells[^1].Value = "";
            }

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;

            if (data.Series.Count > 1)
            {
                string text = data.Series[1].Name
                    + "  " + Math.Round(data.Series[1].YieldPercent, 1) + "%";

                if (data.Series[1].HasPosition)
                {
                    text += " (" + data.Series[1].PosVolume + ")";
                    row.Cells[^1].Style.ForeColor = data.Series[1].PosSide == Side.Buy
                        ? System.Drawing.Color.LimeGreen
                        : System.Drawing.Color.OrangeRed;
                }

                row.Cells[^1].Value = text;
            }
            else
            {
                row.Cells[^1].Value = "";
            }

            return row;
        }

        #endregion

        #region Auto-set securities to T-Investment

        private void ButtonAutoDeploy_UserClickOnButtonEvent()
        {
            SetTSecurities();
        }

        public void SetTSecurities()
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.ConvertToLocString(
                "Eng:Auto deploy will set the standard securities. 10 pairs of MOEX stock and futures via the T-Invest connector will be assigned to the sources. Current sources settings will be overwritten. Continue_" +
                "Ru:Авто-развёртывание установит стандартные бумаги. В источники будут прописаны 10 пар акция плюс фьючерсы MOEX через коннектор Т-Инвестиции. Текущие настройки источников будут перезаписаны. Продолжить_"));

            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            List<AServer> servers = ServerMaster.GetAServers();

            if (servers == null
                || servers.Count == 0)
            {
                SendNewLogMessage("Сначала подключите коннектор к Т-Инвестиции", LogMessageType.Error);
                return;
            }

            if (servers.Find(s => s.ServerType == ServerType.TInvest) == null)
            {
                SendNewLogMessage("Сначала подключите коннектор к Т-Инвестиции", LogMessageType.Error);
                return;
            }

            string portfolioName = _portfolioNum.ValueString;

            if (string.IsNullOrEmpty(portfolioName) == true)
            {
                CustomMessageBoxUi uiInfo = new CustomMessageBoxUi(OsLocalization.ConvertToLocString(
                    "Eng:First set the portfolio number in the Auto deploy tab_" +
                    "Ru:Сначала укажите номер портфеля на вкладке Auto deploy_"));
                uiInfo.ShowDialog();
                SendNewLogMessage("Не указан портфель для развёртывания источников", LogMessageType.Error);
                return;
            }

            Portfolio myPortfolio = null;
            AServer myServer = null;

            for (int i = 0; i < servers.Count; i++)
            {
                if (servers[i].ServerType != ServerType.TInvest)
                {
                    continue;
                }

                List<Portfolio> portfoliosInServer = servers[i].Portfolios;

                if (portfoliosInServer == null
                    || portfoliosInServer.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < portfoliosInServer.Count; j++)
                {
                    if (portfoliosInServer[j].Number == portfolioName)
                    {
                        myServer = servers[i];
                        myPortfolio = portfoliosInServer[j];
                        break;
                    }
                }

                if (myServer != null)
                {
                    break;
                }
            }

            if (myServer == null)
            {
                CustomMessageBoxUi uiInfo = new CustomMessageBoxUi(OsLocalization.ConvertToLocString(
                    "Eng:Portfolio not found. Check the portfolio number and the T-Invest connector_" +
                    "Ru:Портфель не найден. Проверьте номер портфеля и коннектор Т-Инвестиции_"));
                uiInfo.ShowDialog();
                SendNewLogMessage("Не найден портфель и сервер. Возможно указан не верный портфель", LogMessageType.Error);
                return;
            }

            List<Security> securitiesAll = myServer.Securities;

            if (securitiesAll == null
                || securitiesAll.Count == 0)
            {
                SendNewLogMessage("В коннекторе не найдены бумаги. Возможно он не подключен", LogMessageType.Error);
                return;
            }

            if (securitiesAll.Find(s => s.SecurityType == SecurityType.Futures) == null)
            {
                SendNewLogMessage("В коннекторе не найдены фьючерсы. Возможно в коннекторе выключено разрешение на их скачивание. Это настраивается в коннекторе", LogMessageType.Error);
                return;
            }

            if (securitiesAll.Find(s => s.SecurityType == SecurityType.Stock) == null)
            {
                SendNewLogMessage("В коннекторе не найдены акции. Возможно в коннекторе выключено разрешение на их скачивание. Это настраивается в коннекторе", LogMessageType.Error);
                return;
            }

            Security spotSber = securitiesAll.Find(s => s.Name == "SBER" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresSber =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("SRH") || s.Name.StartsWith("SRM")
                || s.Name.StartsWith("SRZ") || s.Name.StartsWith("SRU")));

            SetSecurities(_base1, _futs1, spotSber, futuresSber, myPortfolio, myServer);

            Security spotSberPref = securitiesAll.Find(s => s.Name == "SBERP" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresSberPref =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("SPH") || s.Name.StartsWith("SPM")
                || s.Name.StartsWith("SPZ") || s.Name.StartsWith("SPU")));

            SetSecurities(_base2, _futs2, spotSberPref, futuresSberPref, myPortfolio, myServer);

            Security spotGazp = securitiesAll.Find(s => s.Name == "GAZP" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresGazp =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("GZH") || s.Name.StartsWith("GZM")
                || s.Name.StartsWith("GZZ") || s.Name.StartsWith("GZU")));

            SetSecurities(_base3, _futs3, spotGazp, futuresGazp, myPortfolio, myServer);

            Security spotRosn = securitiesAll.Find(s => s.Name == "ROSN" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresRosn =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("RNH") || s.Name.StartsWith("RNM")
                || s.Name.StartsWith("RNZ") || s.Name.StartsWith("RNU")));

            SetSecurities(_base4, _futs4, spotRosn, futuresRosn, myPortfolio, myServer);

            Security spotLkoh = securitiesAll.Find(s => s.Name == "LKOH" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresLkoh =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("LKH") || s.Name.StartsWith("LKM")
                || s.Name.StartsWith("LKZ") || s.Name.StartsWith("LKU")));

            SetSecurities(_base5, _futs5, spotLkoh, futuresLkoh, myPortfolio, myServer);

            Security spotVtb = securitiesAll.Find(s => s.Name == "VTBR" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresVtb =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("VBH") || s.Name.StartsWith("VBM")
                || s.Name.StartsWith("VBZ") || s.Name.StartsWith("VBU")));

            SetSecurities(_base6, _futs6, spotVtb, futuresVtb, myPortfolio, myServer);

            Security spotGmk = securitiesAll.Find(s => s.Name == "GMKN" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresGmk =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("GKH") || s.Name.StartsWith("GKM")
                || s.Name.StartsWith("GKZ") || s.Name.StartsWith("GKU")));

            SetSecurities(_base7, _futs7, spotGmk, futuresGmk, myPortfolio, myServer);

            Security spotAlrs = securitiesAll.Find(s => s.Name == "ALRS" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresAlrs =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("ALH") || s.Name.StartsWith("ALM")
                || s.Name.StartsWith("ALZ") || s.Name.StartsWith("ALU")));

            SetSecurities(_base8, _futs8, spotAlrs, futuresAlrs, myPortfolio, myServer);

            Security spotAflt = securitiesAll.Find(s => s.Name == "AFLT" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresAflt =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
               (s.Name.StartsWith("AFH") || s.Name.StartsWith("AFM")
                || s.Name.StartsWith("AFZ") || s.Name.StartsWith("AFU")));

            SetSecurities(_base9, _futs9, spotAflt, futuresAflt, myPortfolio, myServer);

            Security spotMgnt = securitiesAll.Find(s => s.Name == "MGNT" && s.SecurityType == SecurityType.Stock);
            List<Security> futuresMgnt =
                securitiesAll.FindAll(s => s.SecurityType == SecurityType.Futures &&
                (s.Name.StartsWith("MNH") || s.Name.StartsWith("MNM")
                || s.Name.StartsWith("MNZ") || s.Name.StartsWith("MNU")));

            SetSecurities(_base10, _futs10, spotMgnt, futuresMgnt, myPortfolio, myServer);

            Security lqdt = securitiesAll.Find(s => s.Name.StartsWith("TMON") && s.SecurityType == SecurityType.Fund);

            if (lqdt != null
                && _tabLqdt.Connector != null)
            {
                _tabLqdt.Connector.ServerType = myServer.ServerType;
                _tabLqdt.Connector.ServerFullName = myServer.ServerNameAndPrefix;
                _tabLqdt.Connector.TimeFrame = TimeFrame.Hour1;
                _tabLqdt.Connector.SecurityName = lqdt.Name;
                _tabLqdt.Connector.SecurityClass = lqdt.NameClass;
                _tabLqdt.Connector.PortfolioName = myPortfolio.Number;
                _tabLqdt.Connector.Save();
            }
        }

        private TimeFrame GetDeployTimeFrame()
        {
            TimeFrame timeFrame = TimeFrame.Min5;

            if (Enum.TryParse(_deployTimeFrame.ValueString, out TimeFrame parsedFrame))
            {
                timeFrame = parsedFrame;
            }

            return timeFrame;
        }

        private void SetSecurities(BotTabSimple tabSpot, BotTabScreener tabFutures,
            Security spotSecurity, List<Security> futuresSecurity, Portfolio portfolio, AServer server)
        {
            if (spotSecurity == null
                || futuresSecurity == null
                || futuresSecurity.Count == 0)
            {
                return;
            }

            TimeFrame timeFrame = GetDeployTimeFrame();

            tabSpot.Connector.ServerType = server.ServerType;
            tabSpot.Connector.ServerFullName = server.ServerNameAndPrefix;
            tabSpot.Connector.TimeFrame = timeFrame;
            tabSpot.Connector.SecurityName = spotSecurity.Name;
            tabSpot.Connector.SecurityClass = spotSecurity.NameClass;
            tabSpot.Connector.PortfolioName = portfolio.Number;
            tabSpot.Connector.Save();

            tabFutures.SecuritiesClass = futuresSecurity[0].NameClass;
            tabFutures.TimeFrame = timeFrame;
            tabFutures.PortfolioName = portfolio.Number;
            tabFutures.ServerType = server.ServerType;
            tabFutures.ServerName = server.ServerNameAndPrefix;

            tabFutures.CandleCreateMethodType = CandleCreateMethodType.Simple.ToString();
            ((Simple)tabFutures.CandleSeriesRealization).TimeFrame = timeFrame;
            ((Simple)tabFutures.CandleSeriesRealization).TimeFrameParameter.ValueString = timeFrame.ToString();

            List<ActivatedSecurity> securitiesToScreener = new List<ActivatedSecurity>();

            for (int i = 0; i < futuresSecurity.Count; i++)
            {
                ActivatedSecurity sec = new ActivatedSecurity();
                sec.SecurityClass = futuresSecurity[i].NameClass;
                sec.SecurityName = futuresSecurity[i].Name;
                sec.IsOn = true;
                securitiesToScreener.Add(sec);
            }

            for (int i = 0; i < securitiesToScreener.Count; i++)
            {
                if (tabFutures.SecuritiesNames.Find(s => s.SecurityName == securitiesToScreener[i].SecurityName) == null)
                {
                    tabFutures.SecuritiesNames.Add(securitiesToScreener[i]);
                }
            }

            tabFutures.SaveSettings();
            tabFutures.NeedToReloadTabs = true;

            SetMultByBase(tabSpot, GetAutoMult(spotSecurity, DateTime.Now));
        }

        private void SetMultByBase(BotTabSimple baseSource, decimal mult)
        {
            if (baseSource == _base1) _futuresMult1.ValueDecimal = mult;
            if (baseSource == _base2) _futuresMult2.ValueDecimal = mult;
            if (baseSource == _base3) _futuresMult3.ValueDecimal = mult;
            if (baseSource == _base4) _futuresMult4.ValueDecimal = mult;
            if (baseSource == _base5) _futuresMult5.ValueDecimal = mult;
            if (baseSource == _base6) _futuresMult6.ValueDecimal = mult;
            if (baseSource == _base7) _futuresMult7.ValueDecimal = mult;
            if (baseSource == _base8) _futuresMult8.ValueDecimal = mult;
            if (baseSource == _base9) _futuresMult9.ValueDecimal = mult;
            if (baseSource == _base10) _futuresMult10.ValueDecimal = mult;
        }

        private decimal GetAutoMult(Security spotSecurity, DateTime time)
        {
            decimal coeff = 1;

            if (spotSecurity.Name.Contains("MGNT") == false
                && spotSecurity.Name.Contains("VTB") == false
                && spotSecurity.Name.Contains("GMKN") == false)
            {
                for (int i = 0; i < spotSecurity.Decimals; i++)
                {
                    coeff = coeff * 10;
                }
            }
            else if (spotSecurity.Name.Contains("VTB") == true)
            {
                if (time.Year < 2024
                    || (time.Year == 2024 && time.Month < 7)
                    || (time.Year == 2024 && time.Month == 7 && time.Day < 15))
                {
                    coeff = 20;
                }
                else
                {
                    coeff = 100;
                }
            }
            else if (spotSecurity.Name.Contains("GMKN") == true)
            {
                if (time.Year < 2024
                    || (time.Year == 2024 && time.Month < 4)
                    || (time.Year == 2024 && time.Month == 4 && time.Day < 4))
                {
                    coeff = 100;
                }
                else
                {
                    coeff = 10;
                }
            }

            return coeff;
        }

        #endregion

        #region SetSecurities in tester

        private void ButtonAutoDeployTester_UserClickOnButtonEvent()
        {
            SetTesterSecurities();
        }

        public void SetTesterSecurities()
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.ConvertToLocString(
                "Eng:Auto deploy will set the securities from the set selected in the tester to the sources. Current sources settings will be overwritten. Continue_" +
                "Ru:Авто-развёртывание пропишет в источники бумаги из выбранного в тестере сета. Текущие настройки источников будут перезаписаны. Продолжить_"));

            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            List<IServer> servers = ServerMaster.GetServers();

            if (servers == null
                || servers.Count == 0
                || servers[0].ServerType != ServerType.Tester)
            {
                SendNewLogMessage("Сначала подключите тестер", LogMessageType.Error);
                return;
            }

            IServer server = servers[0];

            List<Security> securitiesAll = server.Securities;

            if (securitiesAll == null
                || securitiesAll.Count == 0)
            {
                SendNewLogMessage("В тестере не найдены бумаги. Сначала выберите сет и дождитесь загрузки", LogMessageType.Error);
                return;
            }

            if (server.Portfolios == null
                || server.Portfolios.Count == 0)
            {
                SendNewLogMessage("В тестере не найден портфель", LogMessageType.Error);
                return;
            }

            Portfolio myPortfolio = server.Portfolios[0];

            Security spotSber = securitiesAll.Find(s => s.Name == "SBER.txt");
            List<Security> futuresSber =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("SRH") || s.Name.StartsWith("SRM")
                || s.Name.StartsWith("SRZ") || s.Name.StartsWith("SRU"));

            SetSecuritiesInTester(_base1, _futs1, spotSber, futuresSber, myPortfolio, server);

            Security spotSberPref = securitiesAll.Find(s => s.Name == "SBERP.txt");
            List<Security> futuresSberPref =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("SPH") || s.Name.StartsWith("SPM")
                || s.Name.StartsWith("SPZ") || s.Name.StartsWith("SPU"));

            SetSecuritiesInTester(_base2, _futs2, spotSberPref, futuresSberPref, myPortfolio, server);

            Security spotGazp = securitiesAll.Find(s => s.Name == "GAZP.txt");
            List<Security> futuresGazp =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("GZH") || s.Name.StartsWith("GZM")
                || s.Name.StartsWith("GZZ") || s.Name.StartsWith("GZU"));

            SetSecuritiesInTester(_base3, _futs3, spotGazp, futuresGazp, myPortfolio, server);

            Security spotRosn = securitiesAll.Find(s => s.Name == "ROSN.txt");
            List<Security> futuresRosn =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("RNH") || s.Name.StartsWith("RNM")
                || s.Name.StartsWith("RNZ") || s.Name.StartsWith("RNU"));

            SetSecuritiesInTester(_base4, _futs4, spotRosn, futuresRosn, myPortfolio, server);

            Security spotLkoh = securitiesAll.Find(s => s.Name == "LKOH.txt");
            List<Security> futuresLkoh =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("LKH") || s.Name.StartsWith("LKM")
                || s.Name.StartsWith("LKZ") || s.Name.StartsWith("LKU"));

            SetSecuritiesInTester(_base5, _futs5, spotLkoh, futuresLkoh, myPortfolio, server);

            Security spotVtb = securitiesAll.Find(s => s.Name == "VTBR.txt");
            List<Security> futuresVtb =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("VBH") || s.Name.StartsWith("VBM")
                || s.Name.StartsWith("VBZ") || s.Name.StartsWith("VBU"));

            SetSecuritiesInTester(_base6, _futs6, spotVtb, futuresVtb, myPortfolio, server);

            Security spotGmk = securitiesAll.Find(s => s.Name == "GMKN.txt");
            List<Security> futuresGmk =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("GKH") || s.Name.StartsWith("GKM")
                || s.Name.StartsWith("GKZ") || s.Name.StartsWith("GKU"));

            SetSecuritiesInTester(_base7, _futs7, spotGmk, futuresGmk, myPortfolio, server);

            Security spotAlrs = securitiesAll.Find(s => s.Name == "ALRS.txt");
            List<Security> futuresAlrs =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("ALH") || s.Name.StartsWith("ALM")
                || s.Name.StartsWith("ALZ") || s.Name.StartsWith("ALU"));

            SetSecuritiesInTester(_base8, _futs8, spotAlrs, futuresAlrs, myPortfolio, server);

            Security spotAflt = securitiesAll.Find(s => s.Name == "AFLT.txt");
            List<Security> futuresAflt =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("AFH") || s.Name.StartsWith("AFM")
                || s.Name.StartsWith("AFZ") || s.Name.StartsWith("AFU"));

            SetSecuritiesInTester(_base9, _futs9, spotAflt, futuresAflt, myPortfolio, server);

            Security spotMgnt = securitiesAll.Find(s => s.Name == "MGNT.txt");
            List<Security> futuresMgnt =
                securitiesAll.FindAll(s =>
                s.Name.StartsWith("MNH") || s.Name.StartsWith("MNM")
                || s.Name.StartsWith("MNZ") || s.Name.StartsWith("MNU"));

            SetSecuritiesInTester(_base10, _futs10, spotMgnt, futuresMgnt, myPortfolio, server);

            Security lqdt = securitiesAll.Find(s => s.Name.StartsWith("LQDT"));

            if (lqdt != null
                && _tabLqdt.Connector != null)
            {
                TimeFrame timeFrame = TimeFrame.Min5;

                if (Enum.TryParse(_testerDeployTimeFrame.ValueString, out TimeFrame parsedFrame))
                {
                    timeFrame = parsedFrame;
                }

                _tabLqdt.Connector.ServerType = server.ServerType;
                _tabLqdt.Connector.ServerFullName = server.ServerNameAndPrefix;
                _tabLqdt.Connector.TimeFrame = timeFrame;
                _tabLqdt.Connector.SecurityName = lqdt.Name;
                _tabLqdt.Connector.SecurityClass = lqdt.NameClass;
                _tabLqdt.Connector.PortfolioName = myPortfolio.Number;
                _tabLqdt.Connector.Save();
            }
        }

        private void SetSecuritiesInTester(BotTabSimple tabSpot, BotTabScreener tabFutures,
            Security spotSecurity, List<Security> futuresSecurity, Portfolio portfolio, IServer server)
        {
            if (spotSecurity == null
                || futuresSecurity == null
                || futuresSecurity.Count == 0)
            {
                return;
            }

            TimeFrame timeFrame = TimeFrame.Min5;

            if (Enum.TryParse(_testerDeployTimeFrame.ValueString, out TimeFrame parsedFrame))
            {
                timeFrame = parsedFrame;
            }

            tabSpot.Connector.ServerType = server.ServerType;
            tabSpot.Connector.ServerFullName = server.ServerNameAndPrefix;
            tabSpot.Connector.TimeFrame = timeFrame;
            tabSpot.Connector.SecurityName = spotSecurity.Name;
            tabSpot.Connector.SecurityClass = spotSecurity.NameClass;
            tabSpot.Connector.PortfolioName = portfolio.Number;
            tabSpot.Connector.Save();
            tabSpot.Connector.CommissionType = CommissionType.Percent;
            tabSpot.Connector.CommissionValue = 0.04m;

            tabFutures.SecuritiesClass = futuresSecurity[0].NameClass;
            tabFutures.TimeFrame = timeFrame;
            tabFutures.PortfolioName = portfolio.Number;
            tabFutures.ServerType = server.ServerType;
            tabFutures.ServerName = server.ServerNameAndPrefix;
            tabFutures.CommissionType = CommissionType.Percent;
            tabFutures.CommissionValue = 0.04m;

            tabFutures.CandleCreateMethodType = CandleCreateMethodType.Simple.ToString();
            ((Simple)tabFutures.CandleSeriesRealization).TimeFrame = timeFrame;
            ((Simple)tabFutures.CandleSeriesRealization).TimeFrameParameter.ValueString = timeFrame.ToString();

            List<ActivatedSecurity> securitiesToScreener = new List<ActivatedSecurity>();

            for (int i = 0; i < futuresSecurity.Count; i++)
            {
                ActivatedSecurity sec = new ActivatedSecurity();
                sec.SecurityClass = futuresSecurity[i].NameClass;
                sec.SecurityName = futuresSecurity[i].Name;
                sec.IsOn = true;
                securitiesToScreener.Add(sec);
            }

            for (int i = 0; i < securitiesToScreener.Count; i++)
            {
                if (tabFutures.SecuritiesNames.Find(s => s.SecurityName == securitiesToScreener[i].SecurityName) == null)
                {
                    tabFutures.SecuritiesNames.Add(securitiesToScreener[i]);
                }
            }

            tabFutures.SaveSettings();
            tabFutures.NeedToReloadTabs = true;

            DateTime multTime = DateTime.Now;

            TesterServer testerServer = server as TesterServer;

            if (testerServer != null
                && testerServer.TimeStart != DateTime.MinValue)
            {
                multTime = testerServer.TimeStart;
            }

            SetMultByBase(tabSpot, GetAutoMult(spotSecurity, multTime));
        }

        #endregion
    }
}
