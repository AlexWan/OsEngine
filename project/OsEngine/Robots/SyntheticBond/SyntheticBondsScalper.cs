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
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

/*

Скальпер синтетических облигаций. Контанго-арбитраж на рынке фьючерсов на акции MOEX.
Работает круглосуточно (день/ночь/выходные), вход только лимитными ордерами по стакану.

Конструкция позиции (синтетическая облигация в контанго)
Лонг акция (база) + Шорт фьючерс
Объёмы в лотах: база = контракты фьючерса × мульт / лот акции, дельта-нейтрально.
Свободные деньги паркуются в LQDT (покрытие позиции — асинхронно, по стакану TMON@)

Режимы: Off / OnByLqdtSpread / OnByFixedYield / OnlyExit

ВХОД
Желаемый объём считается от денег (свободные средства с ГО + потолок Max position %),
стакан — только ограничитель сверху: если глубины не хватает — берём меньше.
Лимитные ордера по уровням контрагента. Три ближайшие серии фьючерса — равноправные кандидаты.

ВЫХОД
Единственный выход — за N дней до экспирации (Days before expiration to exit),
лимитками по стакану в окне ликвидности 10:00–18:00 будни, маркет — аварийно по дедлайну.

Только реальная торговля (тестер/оптимизатор не используются).

Источники
10 пар источников. В каждой паре BotTabSimple - базовая акция, BotTabScreener - фьючерсы на неё.
Все 10 пар разворачиваются кнопкой авто-развёртывания (Т-Банк в реале).

*/

namespace OsEngine.Robots.SyntheticBond
{
    [Bot("SyntheticBondsScalper")]
    public class SyntheticBondsScalper : BotPanel
    {
        #region Constructor and parameters

        private StrategyParameterString _regime;
        private StrategyParameterInt _logicThreadIntervalSec;
        private StrategyParameterDecimal _entryMinYieldDiffOverLqdt;
        private StrategyParameterDecimal _entryMinYield;
        private StrategyParameterDecimal _maxPositionPercent;
        private StrategyParameterInt _limitOrderTimeoutSec;
        private StrategyParameterInt _exitMarketFallbackSec;
        private StrategyParameterInt _exitWindowStartHour;
        private StrategyParameterInt _exitWindowEndHour;
        private StrategyParameterBool _tradeSeries1IsOn;
        private StrategyParameterBool _tradeSeries2IsOn;
        private StrategyParameterBool _tradeSeries3IsOn;
        private StrategyParameterBool _failOrdersReactionIsOn;
        private StrategyParameterInt _failOpenOrdersToReaction;
        private StrategyParameterInt _failCancelOrdersToReaction;
        private StrategyParameterBool _resetErrorsAtStartOfDay;
        private StrategyParameterInt _delayInRealMs;

        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodButton;

        private StrategyParameterInt _tableUpdateIntervalSec;
        private StrategyParameterString _multRegime;
        private StrategyParameterBool _fullLogIsOn;

        private StrategyParameterBool _LqdtRegimeIsOn;
        private StrategyParameterInt _LqdtYieldDays;
        private StrategyParameterDecimal _lqdtFreeMoneyBuffer;

        private StrategyParameterInt _daysBeforeExpirationToExit;
        private StrategyParameterInt _entryCooldownSec;
        private StrategyParameterBool _exitOnSaturday;
        private StrategyParameterBool _exitOnSunday;

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
        private StrategyParameterString _deployTimeFrame;

        public SyntheticBondsScalper(string name, StartProgram startProgram) : base(name, startProgram)
        {
            CreateSources();

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "OnByLqdtSpread", "OnByFixedYield", "OnlyExit" }, "Base");
            _logicThreadIntervalSec = CreateParameter("Logic thread interval, sec", 5, 1, 300, 1, "Base");
            _tableUpdateIntervalSec = CreateParameter("Table update interval, sec", 5, 1, 60, 1, "Base");
            _fullLogIsOn = CreateParameter("Full log is on", true, "Base");

            _daysBeforeExpirationToExit = CreateParameter("Days before expiration to exit", 7, 0, 10, 1, "Exit");
            _entryCooldownSec = CreateParameter("Entry cooldown, sec", 20, 0, 300, 5, "Base");

            _entryMinYieldDiffOverLqdt = CreateParameter("Min yield diff over LQDT % ann", 3.75m, 0.1m, 100, 1, "Base");
            _entryMinYield = CreateParameter("Min yield to entry % ann", 10m, 0.1m, 100, 0.1m, "Base");

            _maxPositionPercent = CreateParameter("Max position % of portfolio", 85m, 1m, 100m, 1, "Base");
            _limitOrderTimeoutSec = CreateParameter("Limit order timeout, sec", 30, 5, 600, 5, "Base");

            _exitMarketFallbackSec = CreateParameter("Exit market fallback, sec", 120, 10, 3600, 10, "Exit");
            _exitWindowStartHour = CreateParameter("Exit window start hour", 10, 0, 23, 1, "Exit");
            _exitWindowEndHour = CreateParameter("Exit window end hour", 18, 1, 24, 1, "Exit");
            _exitOnSaturday = CreateParameter("Exit on Saturday", false, "Exit");
            _exitOnSunday = CreateParameter("Exit on Sunday", false, "Exit");

            _tradeSeries1IsOn = CreateParameter("Trade series 1 is on", true, "Trade series");
            _tradeSeries2IsOn = CreateParameter("Trade series 2 is on", true, "Trade series");
            _tradeSeries3IsOn = CreateParameter("Trade series 3 is on", true, "Trade series");

            _failOrdersReactionIsOn = CreateParameter("Fail orders reaction is on", true, "Errors reaction");
            _failOpenOrdersToReaction = CreateParameter("Fail open orders to reaction", 10, 1, 1000, 1, "Errors reaction");
            _failCancelOrdersToReaction = CreateParameter("Fail cancel orders to reaction", 10, 1, 1000, 1, "Errors reaction");
            _resetErrorsAtStartOfDay = CreateParameter("Reset error counters at start of day", true, "Errors reaction");
            _delayInRealMs = CreateParameter("Delay in real, ms", 500, 0, 10000, 100, "Errors reaction");

            _LqdtRegimeIsOn = CreateParameter("LQDT regime is on", true, "LQDT");
            _LqdtYieldDays = CreateParameter("LQDT yield days", 10, 5, 60, 5, "LQDT");
            _lqdtFreeMoneyBuffer = CreateParameter("LQDT free money buffer", 5000m, 0m, 100000m, 500, "LQDT");

            _tradePeriodsSettings = new NonTradePeriods(name);

            ClearNonTradePeriods(_tradePeriodsSettings.NonTradePeriodGeneral);

            ApplyWeekdayNonTradePeriods(_tradePeriodsSettings.NonTradePeriodMonday);
            ApplyWeekdayNonTradePeriods(_tradePeriodsSettings.NonTradePeriodTuesday);
            ApplyWeekdayNonTradePeriods(_tradePeriodsSettings.NonTradePeriodWednesday);
            ApplyWeekdayNonTradePeriods(_tradePeriodsSettings.NonTradePeriodThursday);
            ApplyWeekdayNonTradePeriods(_tradePeriodsSettings.NonTradePeriodFriday);

            ApplyWeekendNonTradePeriods(_tradePeriodsSettings.NonTradePeriodSaturday);
            ApplyWeekendNonTradePeriods(_tradePeriodsSettings.NonTradePeriodSunday);

            _tradePeriodsSettings.TradeInSunday = true;
            _tradePeriodsSettings.TradeInSaturday = true;

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

                DeleteEvent += Scalper_DeleteEvent;

                Thread worker = new Thread(LogicThreadWorker) { IsBackground = true };
                worker.Start();
            }

            Description = OsLocalization.ConvertToLocString(
              "Eng:Scalper of synthetic bonds on the MOEX stock futures market. Accumulates a long stock + short futures position with limit orders placed at the counterparty levels of the order book, when the annualized contango yield exceeds the threshold (vs LQDT or fixed). Exits N days before futures expiration. Free money is parked in LQDT_" +
              "Ru:Скальпер синтетических облигаций на рынке фьючерсов на акции MOEX. Набирает позицию лонг акция + шорт фьючерс лимитными ордерами по уровням контрагентов в стакане, когда годовая доходность контанго превышает порог (над LQDT или фиксированный). Выход за N дней до экспирации фьючерса. Свободные деньги паркуются в LQDT_");

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

        #endregion

        #region Non trade periods

        private void ClearNonTradePeriods(NonTradePeriodInDay day)
        {
            day.NonTradePeriod1OnOff = false;
            day.NonTradePeriod2OnOff = false;
            day.NonTradePeriod3OnOff = false;
            day.NonTradePeriod4OnOff = false;
            day.NonTradePeriod5OnOff = false;
        }

        private void ApplyWeekdayNonTradePeriods(NonTradePeriodInDay day)
        {
            day.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0, Second = 0 };
            day.NonTradePeriod1End = new TimeOfDay() { Hour = 7, Minute = 0, Second = 20 };
            day.NonTradePeriod1OnOff = true;

            day.NonTradePeriod2Start = new TimeOfDay() { Hour = 9, Minute = 49, Second = 40 };
            day.NonTradePeriod2End = new TimeOfDay() { Hour = 10, Minute = 0, Second = 20 };
            day.NonTradePeriod2OnOff = true;

            day.NonTradePeriod3Start = new TimeOfDay() { Hour = 13, Minute = 59, Second = 40 };
            day.NonTradePeriod3End = new TimeOfDay() { Hour = 14, Minute = 5, Second = 20 };
            day.NonTradePeriod3OnOff = true;

            day.NonTradePeriod4Start = new TimeOfDay() { Hour = 18, Minute = 49, Second = 40 };
            day.NonTradePeriod4End = new TimeOfDay() { Hour = 19, Minute = 5, Second = 20 };
            day.NonTradePeriod4OnOff = true;

            day.NonTradePeriod5Start = new TimeOfDay() { Hour = 23, Minute = 49, Second = 40 };
            day.NonTradePeriod5End = new TimeOfDay() { Hour = 24, Minute = 0, Second = 0 };
            day.NonTradePeriod5OnOff = true;
        }

        private void ApplyWeekendNonTradePeriods(NonTradePeriodInDay day)
        {
            day.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0, Second = 0 };
            day.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 0, Second = 20 };
            day.NonTradePeriod1OnOff = true;

            day.NonTradePeriod2Start = new TimeOfDay() { Hour = 18, Minute = 59, Second = 40 };
            day.NonTradePeriod2End = new TimeOfDay() { Hour = 24, Minute = 0, Second = 0 };
            day.NonTradePeriod2OnOff = true;

            day.NonTradePeriod3OnOff = false;
            day.NonTradePeriod4OnOff = false;
            day.NonTradePeriod5OnOff = false;
        }

        #endregion

        #region Service

        private void _tradePeriodButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        private bool _isDeleted = false;

        private void Scalper_DeleteEvent()
        {
            try
            {
                _isDeleted = true;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private bool IsTradeAllowed()
        {
            if (_base1 != null
                && _base1.IsNonTradePeriodInConnector)
            {
                return false;
            }

            if (_tradePeriodsSettings.CanTradeThisTime(DateTime.Now) == false)
            {
                return false;
            }

            return true;
        }

        private bool IsExitWindow(DateTime time)
        {
            if (time.DayOfWeek == DayOfWeek.Saturday
                && _exitOnSaturday.ValueBool == false)
            {
                return false;
            }

            if (time.DayOfWeek == DayOfWeek.Sunday
                && _exitOnSunday.ValueBool == false)
            {
                return false;
            }

            if (time.Hour < _exitWindowStartHour.ValueInt
                || time.Hour >= _exitWindowEndHour.ValueInt)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Worker thread

        private void LogicThreadWorker()
        {
            while (true)
            {
                try
                {
                    int interval = _logicThreadIntervalSec.ValueInt;

                    if (interval < 1)
                    {
                        interval = 1;
                    }

                    Thread.Sleep(interval * 1000);

                    if (_isDeleted)
                    {
                        return;
                    }

                    Logic();
                }
                catch (Exception ex)
                {
                    SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(10000);
                }
            }
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

        #region General logic

        private void Logic()
        {
            try
            {
                ScalperLogic();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private string _prevRegime = "";

        private readonly List<(BotTabSimple Tab, Position Pos, DateTime Time)> _pendingLimits = new List<(BotTabSimple, Position, DateTime)>();

        private int _failOpenOrdersCountFact = 0;
        private int _failCancelOrdersCountFact = 0;
        private DateTime _vacationTime = DateTime.MinValue;
        private DateTime _lastErrorResetDate = DateTime.MinValue;
        private DateTime _lastCancelTryTime = DateTime.MinValue;

        private readonly Dictionary<string, DateTime> _exitStartTime = new Dictionary<string, DateTime>();

        private void ScalperLogic()
        {
            TryResetErrorsAtStartOfDay();

            if (_vacationTime > DateTime.Now)
            {
                return;
            }

            TryTimeoutLimitOrders();

            string regime = _regime.ValueString;

            if (regime == "Off")
            {
                _prevRegime = regime;
                return;
            }

            if (regime == "OnlyExit")
            {
                if (_prevRegime != "OnlyExit")
                {
                    LogFull("ONLY EXIT activated. Cancel all active limit orders.");

                    CancelAllActiveOrders();

                    _pendingLimits.Clear();
                }

                _prevRegime = regime;

                TryExitByExpiration();

                TryLqdtParking();

                return;
            }

            _prevRegime = regime;

            if (IsTradeAllowed())
            {
                if (regime == "OnByLqdtSpread")
                {
                    TryEntryByLqdtSpread();
                }
                else if (regime == "OnByFixedYield")
                {
                    TryEntryByFixedYield();
                }
            }

            TryExitByExpiration();

            TryLqdtParking();
        }

        private void CancelAllActiveOrders()
        {
            BotTabSimple[] bases = { _base1, _base2, _base3, _base4, _base5, _base6, _base7, _base8, _base9, _base10 };
            BotTabScreener[] screeners = { _futs1, _futs2, _futs3, _futs4, _futs5, _futs6, _futs7, _futs8, _futs9, _futs10 };

            for (int i = 0; i < bases.Length; i++)
            {
                CancelActiveOrdersOnTab(bases[i]);

                for (int j = 0; j < screeners[i].Tabs.Count; j++)
                {
                    CancelActiveOrdersOnTab(screeners[i].Tabs[j]);
                }
            }

            CancelActiveOrdersOnTab(_tabLqdt);
        }

        private void CancelActiveOrdersOnTab(BotTabSimple tab)
        {
            if (tab == null)
            {
                return;
            }

            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State == PositionStateType.Opening
                    || positions[i].State == PositionStateType.Closing)
                {
                    tab.CloseAllOrderToPosition(positions[i]);
                }
            }
        }

        private void TryTimeoutLimitOrders()
        {
            for (int i = _pendingLimits.Count - 1; i >= 0; i--)
            {
                (BotTabSimple tab, Position pos, DateTime time) = _pendingLimits[i];

                if (tab == null
                    || pos == null)
                {
                    _pendingLimits.RemoveAt(i);
                    continue;
                }

                if (pos.State == PositionStateType.Open
                    || pos.State == PositionStateType.Done
                    || pos.State == PositionStateType.Closing)
                {
                    _pendingLimits.RemoveAt(i);
                    continue;
                }

                if ((DateTime.Now - time).TotalSeconds < _limitOrderTimeoutSec.ValueInt)
                {
                    continue;
                }

                if ((DateTime.Now - _lastCancelTryTime).TotalSeconds < 1)
                {
                    continue;
                }

                _lastCancelTryTime = DateTime.Now;

                LogFull("LIMIT timeout. Cancel orders of position " + pos.Number
                    + " on " + tab.Connector?.SecurityName);

                tab.CloseAllOrderToPosition(pos);

                _pendingLimits.RemoveAt(i);
            }
        }

        private void TryResetErrorsAtStartOfDay()
        {
            if (_resetErrorsAtStartOfDay.ValueBool == false)
            {
                return;
            }

            DateTime today = DateTime.Now.Date;

            if (_lastErrorResetDate == today)
            {
                return;
            }

            _lastErrorResetDate = today;
            _failOpenOrdersCountFact = 0;
            _failCancelOrdersCountFact = 0;
        }

        private void Tab_PositionOpeningFailEvent(Position position)
        {
            try
            {
                if (_failOrdersReactionIsOn.ValueBool == false)
                {
                    return;
                }

                _failOpenOrdersCountFact++;

                LogFull("ERROR on open order. Count: " + _failOpenOrdersCountFact);

                if (_failOpenOrdersCountFact >= _failOpenOrdersToReaction.ValueInt)
                {
                    LogFull("Open orders errors threshold reached. Regime -> OnlyExit");
                    _regime.ValueString = "OnlyExit";
                }

                _vacationTime = DateTime.Now.AddMilliseconds(_delayInRealMs.ValueInt * _failOpenOrdersCountFact);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void Tab_CancelOrderFailEvent(Order order)
        {
            try
            {
                if (_failOrdersReactionIsOn.ValueBool == false)
                {
                    return;
                }

                _failCancelOrdersCountFact++;

                LogFull("ERROR on cancel order. Count: " + _failCancelOrdersCountFact);

                if (_failCancelOrdersCountFact >= _failCancelOrdersToReaction.ValueInt)
                {
                    LogFull("Cancel orders errors threshold reached. Regime -> OnlyExit");
                    _regime.ValueString = "OnlyExit";
                }

                _vacationTime = DateTime.Now.AddMilliseconds(_delayInRealMs.ValueInt * _failCancelOrdersCountFact);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void TryEntryAboveThreshold(decimal minYieldAnn)
        {
            if (HasOrdersInMarket())
            {
                return;
            }

            if ((DateTime.Now - _lastOrderExecutionTime).TotalSeconds < _entryCooldownSec.ValueInt)
            {
                return;
            }

            BotTabSimple bestBase = null;
            BotTabSimple bestFutures = null;
            decimal bestYield = 0;

            BotTabSimple[] bases = { _base1, _base2, _base3, _base4, _base5, _base6, _base7, _base8, _base9, _base10 };
            BotTabScreener[] screeners = { _futs1, _futs2, _futs3, _futs4, _futs5, _futs6, _futs7, _futs8, _futs9, _futs10 };

            DateTime time = GetCurrentServerTime();

            for (int i = 0; i < bases.Length; i++)
            {
                BotTabSimple baseTab = bases[i];
                BotTabScreener screener = screeners[i];

                if (baseTab == null
                    || screener == null
                    || string.IsNullOrEmpty(baseTab.Connector?.SecurityName))
                {
                    continue;
                }

                decimal mult = GetMultByBase(baseTab);

                List<BotTabSimple> series = GetNearestSeries(screener, time, 3);

                for (int s = 0; s < series.Count; s++)
                {
                    if (s == 0 && _tradeSeries1IsOn.ValueBool == false) continue;
                    if (s == 1 && _tradeSeries2IsOn.ValueBool == false) continue;
                    if (s == 2 && _tradeSeries3IsOn.ValueBool == false) continue;

                    decimal yieldAnn = CalculateYieldAnnBook(baseTab, series[s], mult);

                    if (yieldAnn <= 0)
                    {
                        continue;
                    }

                    if (yieldAnn <= minYieldAnn)
                    {
                        continue;
                    }

                    if (yieldAnn > bestYield)
                    {
                        bestYield = yieldAnn;
                        bestBase = baseTab;
                        bestFutures = series[s];
                    }
                }
            }

            if (bestBase == null
                || bestFutures == null)
            {
                return;
            }

            TryPlaceLimitEntry(bestBase, bestFutures, bestYield);
        }

        private void TryEntryByFixedYield()
        {
            TryEntryAboveThreshold(_entryMinYield.ValueDecimal);
        }

        private void OnOrderUpdate(Order order)
        {
            if (order != null
                && order.State == OrderStateType.Done)
            {
                _lastOrderExecutionTime = DateTime.Now;
            }
        }

        private bool HasOrdersInMarket()
        {
            BotTabSimple[] bases = { _base1, _base2, _base3, _base4, _base5, _base6, _base7, _base8, _base9, _base10 };
            BotTabScreener[] screeners = { _futs1, _futs2, _futs3, _futs4, _futs5, _futs6, _futs7, _futs8, _futs9, _futs10 };

            for (int i = 0; i < bases.Length; i++)
            {
                if (TabHasOrdersInMarket(bases[i]))
                {
                    return true;
                }

                BotTabScreener screener = screeners[i];

                if (screener == null)
                {
                    continue;
                }

                for (int j = 0; j < screener.Tabs.Count; j++)
                {
                    if (TabHasOrdersInMarket(screener.Tabs[j]))
                    {
                        return true;
                    }
                }
            }

            return TabHasOrdersInMarket(_tabLqdt);
        }

        private bool TabHasOrdersInMarket(BotTabSimple tab)
        {
            if (tab == null)
            {
                return false;
            }

            List<Position> positions = tab.PositionsOpenAll;

            if (positions == null)
            {
                return false;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                Position pos = positions[i];

                if (pos == null)
                {
                    continue;
                }

                if (OrdersHasActive(pos.OpenOrders)
                    || OrdersHasActive(pos.CloseOrders))
                {
                    return true;
                }
            }

            return false;
        }

        private bool OrdersHasActive(List<Order> orders)
        {
            if (orders == null)
            {
                return false;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i] != null
                    && (orders[i].State == OrderStateType.Active
                        || orders[i].State == OrderStateType.Pending))
                {
                    return true;
                }
            }

            return false;
        }

        private decimal CalculateYieldAnnBook(BotTabSimple baseSource, BotTabSimple futuresSource, decimal mult)
        {
            if (baseSource.PriceBestAsk == 0
                || futuresSource.PriceBestBid == 0
                || futuresSource.Security == null
                || futuresSource.Security.Expiration == DateTime.MinValue)
            {
                return 0;
            }

            DateTime time = futuresSource.TimeServerCurrent;

            if (time == DateTime.MinValue)
            {
                time = DateTime.Now;
            }

            int daysToExpiration = (futuresSource.Security.Expiration - time).Days;

            if (daysToExpiration <= _daysBeforeExpirationToExit.ValueInt)
            {
                return 0;
            }

            decimal deviation = futuresSource.PriceBestBid / mult - baseSource.PriceBestAsk;

            if (deviation <= 0)
            {
                return 0;
            }

            return deviation / (baseSource.PriceBestAsk / 100) * 365 / daysToExpiration;
        }

        private void TryPlaceLimitEntry(BotTabSimple baseSource, BotTabSimple futuresSource, decimal yieldAnn)
        {
            if (baseSource.IsReadyToTrade == false
                || futuresSource.IsReadyToTrade == false)
            {
                return;
            }

            MarketDepth futBook = futuresSource.MarketDepth;
            MarketDepth baseBook = baseSource.MarketDepth;

            if (futBook == null
                || futBook.Bids == null
                || futBook.Bids.Count == 0
                || baseBook == null
                || baseBook.Asks == null
                || baseBook.Asks.Count == 0)
            {
                return;
            }

            decimal mult = GetMultByBase(baseSource);

            decimal futBidPrice = (decimal)futBook.Bids[0].Price;
            decimal futBidVolume = (decimal)futBook.Bids[0].Bid;

            decimal baseAskPrice = (decimal)baseBook.Asks[0].Price;
            decimal baseAskVolume = (decimal)baseBook.Asks[0].Ask;

            decimal baseLot = 1;

            if (baseSource.Security != null
                && baseSource.Security.Lot > 1)
            {
                baseLot = baseSource.Security.Lot;
            }

            decimal portfolioValue = _base1 != null && _base1.Portfolio != null
                ? _base1.Portfolio.ValueCurrent : 0;

            decimal baseInvested = GetBaseInvestedTotal();

            decimal freeMoney = GetFreeMoneyWithGo();

            if (baseAskPrice <= 0
                || futBidPrice <= 0)
            {
                return;
            }

            decimal room = portfolioValue * (_maxPositionPercent.ValueDecimal / 100) - baseInvested;

            if (room <= 0)
            {
                LogFull("ENTRY skipped: max position % reached. "
                    + PairDescription(baseSource, futuresSource, mult)
                    + " | invested " + baseInvested + " | cap " + _maxPositionPercent.ValueDecimal + "%");
                return;
            }

            decimal baseMoneyPerContract = mult * baseAskPrice;
            decimal goPerContract = GetGoPerContract(futuresSource);

            decimal byRoom = room / baseMoneyPerContract;
            decimal byFree = freeMoney / (baseMoneyPerContract + goPerContract);

            decimal desiredContracts = Math.Floor(Math.Min(byRoom, byFree));

            if (desiredContracts < 1)
            {
                LogFull("ENTRY skipped: not enough money for 1 contract. "
                    + PairDescription(baseSource, futuresSource, mult)
                    + " | room " + room + " | free " + freeMoney);
                return;
            }

            decimal futContracts = Math.Floor(Math.Min(desiredContracts, futBidVolume));

            if (futContracts < 1)
            {
                return;
            }

            decimal baseLots = Math.Floor(futContracts * mult / baseLot);

            if (baseLots > baseAskVolume)
            {
                baseLots = Math.Floor(baseAskVolume);
                futContracts = Math.Floor(baseLots * baseLot / mult);
            }

            if (baseLots < 1
                || futContracts < 1)
            {
                return;
            }

            LogFull("ENTRY limit: " + PairDescription(baseSource, futuresSource, mult)
                + " | yieldAnn " + Math.Round(yieldAnn, 2)
                + " | futSell " + futContracts + " @ " + futBidPrice
                + " | baseBuy " + baseLots + " lots @ " + baseAskPrice);

            Position futPos = futuresSource.SellAtLimit(futContracts, futBidPrice);

            Thread.Sleep(_delayInRealMs.ValueInt);

            Position basePos = baseSource.BuyAtLimit(baseLots, baseAskPrice);

            DateTime now = DateTime.Now;

            if (futPos != null)
            {
                _pendingLimits.Add((futuresSource, futPos, now));
            }

            if (basePos != null)
            {
                _pendingLimits.Add((baseSource, basePos, now));
            }
        }

        private decimal GetFreeMoneyWithGo()
        {
            decimal invested = 0;

            BotTabSimple[] bases = { _base1, _base2, _base3, _base4, _base5, _base6, _base7, _base8, _base9, _base10 };
            BotTabScreener[] screeners = { _futs1, _futs2, _futs3, _futs4, _futs5, _futs6, _futs7, _futs8, _futs9, _futs10 };

            for (int i = 0; i < bases.Length; i++)
            {
                invested += GetBaseInvestedMoney(bases[i]);
                invested += GetFuturesGo(screeners[i]);
            }

            decimal portfolioValue = 0;

            if (_base1 != null
                && _base1.Portfolio != null)
            {
                portfolioValue = _base1.Portfolio.ValueCurrent;
            }

            return portfolioValue - invested;
        }

        private decimal GetBaseInvestedMoney(BotTabSimple tab)
        {
            decimal sum = 0;

            if (tab == null)
            {
                return 0;
            }

            decimal lot = 1;

            if (tab.Security != null
                && tab.Security.Lot > 1)
            {
                lot = tab.Security.Lot;
            }

            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State == PositionStateType.Open
                    && positions[i].Direction == Side.Buy
                    && positions[i].OpenVolume > 0)
                {
                    sum += positions[i].OpenVolume * lot * tab.PriceBestBid;
                }
            }

            return sum;
        }

        private decimal GetFuturesGo(BotTabScreener screener)
        {
            decimal sum = 0;

            if (screener == null)
            {
                return 0;
            }

            for (int i = 0; i < screener.Tabs.Count; i++)
            {
                BotTabSimple tab = screener.Tabs[i];

                if (tab == null
                    || tab.Security == null)
                {
                    continue;
                }

                List<Position> positions = tab.PositionsOpenAll;

                for (int j = 0; j < positions.Count; j++)
                {
                    if (positions[j].State == PositionStateType.Open
                        && positions[j].Direction == Side.Sell
                        && positions[j].OpenVolume > 0)
                    {
                        sum += positions[j].OpenVolume * GetGoPerContract(tab);
                    }
                }
            }

            return sum;
        }

        private decimal GetGoPerContract(BotTabSimple futTab)
        {
            decimal marginSell = 0;

            if (futTab != null
                && futTab.Security != null)
            {
                marginSell = futTab.Security.MarginSell;
            }

            if (marginSell > 0)
            {
                return marginSell;
            }

            return futTab.PriceBestAsk * 0.2m;
        }

        private void TryExitByExpiration()
        {
            DateTime now = DateTime.Now;

            BotTabSimple[] bases = { _base1, _base2, _base3, _base4, _base5, _base6, _base7, _base8, _base9, _base10 };
            BotTabScreener[] screeners = { _futs1, _futs2, _futs3, _futs4, _futs5, _futs6, _futs7, _futs8, _futs9, _futs10 };

            for (int i = 0; i < bases.Length; i++)
            {
                BotTabSimple baseTab = bases[i];
                BotTabScreener screener = screeners[i];

                if (baseTab == null
                    || screener == null
                    || string.IsNullOrEmpty(baseTab.Connector?.SecurityName))
                {
                    continue;
                }

                string baseName = baseTab.Connector.SecurityName;

                decimal baseBuyVolume = GetOpenBuyVolume(baseTab);
                decimal futSellVolume = GetOpenSellVolume(screener);

                if (baseBuyVolume <= 0
                    && futSellVolume <= 0)
                {
                    _exitStartTime.Remove(baseName);
                    continue;
                }

                if (_exitStartTime.ContainsKey(baseName))
                {
                    DateTime start = _exitStartTime[baseName];

                    if ((now - start).TotalSeconds >= _exitMarketFallbackSec.ValueInt
                        && (IsExitWindow(now) || HasExpiredFutures(screener)))
                    {
                        LogFull("EXIT market fallback: " + baseName);
                        TryClosePairMarket(baseTab, screener);
                        _exitStartTime.Remove(baseName);
                    }

                    continue;
                }

                if (HasDueFutures(screener) == false)
                {
                    continue;
                }

                if (HasExpiredFutures(screener))
                {
                    LogFull("EXIT past expiration (market): " + baseName);
                    TryClosePairMarket(baseTab, screener);
                    _exitStartTime.Remove(baseName);
                    continue;
                }

                if (IsExitWindow(now) == false)
                {
                    continue;
                }

                LogFull("EXIT by expiration: " + baseName
                    + " | base " + baseBuyVolume + " | fut " + futSellVolume);

                TryClosePairLimits(baseTab, screener);

                _exitStartTime[baseName] = now;
            }
        }

        private bool HasExpiredFutures(BotTabScreener screener)
        {
            DateTime time = DateTime.Now;

            for (int i = 0; i < screener.Tabs.Count; i++)
            {
                BotTabSimple tab = screener.Tabs[i];

                if (tab == null
                    || tab.Security == null
                    || tab.Security.Expiration == DateTime.MinValue)
                {
                    continue;
                }

                if ((tab.Security.Expiration - time).Days > 0)
                {
                    continue;
                }

                List<Position> positions = tab.PositionsOpenAll;

                for (int j = 0; j < positions.Count; j++)
                {
                    if (positions[j].State == PositionStateType.Open
                        && positions[j].Direction == Side.Sell
                        && positions[j].OpenVolume > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private decimal GetOpenBuyVolume(BotTabSimple tab)
        {
            decimal sum = 0;

            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State == PositionStateType.Open
                    && positions[i].Direction == Side.Buy
                    && positions[i].OpenVolume > 0)
                {
                    sum += positions[i].OpenVolume;
                }
            }

            return sum;
        }

        private decimal GetOpenSellVolume(BotTabScreener screener)
        {
            decimal sum = 0;

            for (int i = 0; i < screener.Tabs.Count; i++)
            {
                List<Position> positions = screener.Tabs[i].PositionsOpenAll;

                for (int j = 0; j < positions.Count; j++)
                {
                    if (positions[j].State == PositionStateType.Open
                        && positions[j].Direction == Side.Sell
                        && positions[j].OpenVolume > 0)
                    {
                        sum += positions[j].OpenVolume;
                    }
                }
            }

            return sum;
        }

        private bool HasDueFutures(BotTabScreener screener)
        {
            DateTime time = DateTime.Now;

            for (int i = 0; i < screener.Tabs.Count; i++)
            {
                BotTabSimple tab = screener.Tabs[i];

                if (tab == null
                    || tab.Security == null
                    || tab.Security.Expiration == DateTime.MinValue)
                {
                    continue;
                }

                int days = (tab.Security.Expiration - time).Days;

                if (days > _daysBeforeExpirationToExit.ValueInt)
                {
                    continue;
                }

                List<Position> positions = tab.PositionsOpenAll;

                for (int j = 0; j < positions.Count; j++)
                {
                    if (positions[j].State == PositionStateType.Open
                        && positions[j].Direction == Side.Sell
                        && positions[j].OpenVolume > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void TryClosePairLimits(BotTabSimple baseTab, BotTabScreener screener)
        {
            if (baseTab == null
                || baseTab.IsReadyToTrade == false)
            {
                return;
            }

            for (int i = 0; i < screener.Tabs.Count; i++)
            {
                BotTabSimple futTab = screener.Tabs[i];

                if (futTab == null
                    || futTab.IsReadyToTrade == false)
                {
                    continue;
                }

                List<Position> positions = futTab.PositionsOpenAll;

                for (int j = 0; j < positions.Count; j++)
                {
                    if (positions[j].State != PositionStateType.Open
                        || positions[j].Direction != Side.Sell
                        || positions[j].OpenVolume <= 0)
                    {
                        continue;
                    }

                    MarketDepth book = futTab.MarketDepth;

                    if (book != null
                        && book.Asks != null
                        && book.Asks.Count > 0)
                    {
                        futTab.CloseAtLimit(positions[j], (decimal)book.Asks[0].Price, positions[j].OpenVolume);
                    }
                    else
                    {
                        futTab.CloseAtMarket(positions[j], positions[j].OpenVolume);
                    }
                }
            }

            MarketDepth baseBook = baseTab.MarketDepth;

            List<Position> basePositions = baseTab.PositionsOpenAll;

            for (int j = 0; j < basePositions.Count; j++)
            {
                if (basePositions[j].State != PositionStateType.Open
                    || basePositions[j].Direction != Side.Buy
                    || basePositions[j].OpenVolume <= 0)
                {
                    continue;
                }

                if (baseBook != null
                    && baseBook.Bids != null
                    && baseBook.Bids.Count > 0)
                {
                    baseTab.CloseAtLimit(basePositions[j], (decimal)baseBook.Bids[0].Price, basePositions[j].OpenVolume);
                }
                else
                {
                    baseTab.CloseAtMarket(basePositions[j], basePositions[j].OpenVolume);
                }
            }
        }

        private void TryClosePairMarket(BotTabSimple baseTab, BotTabScreener screener)
        {
            if (baseTab == null
                || baseTab.IsReadyToTrade == false)
            {
                return;
            }

            for (int i = 0; i < screener.Tabs.Count; i++)
            {
                BotTabSimple futTab = screener.Tabs[i];

                if (futTab == null
                    || futTab.IsReadyToTrade == false)
                {
                    continue;
                }

                List<Position> positions = futTab.PositionsOpenAll;

                for (int j = 0; j < positions.Count; j++)
                {
                    if (positions[j].State == PositionStateType.Open
                        && positions[j].Direction == Side.Sell
                        && positions[j].OpenVolume > 0)
                    {
                        futTab.CloseAtMarket(positions[j], positions[j].OpenVolume);
                    }
                }
            }

            List<Position> basePositions = baseTab.PositionsOpenAll;

            for (int j = 0; j < basePositions.Count; j++)
            {
                if (basePositions[j].State == PositionStateType.Open
                    && basePositions[j].Direction == Side.Buy
                    && basePositions[j].OpenVolume > 0)
                {
                    baseTab.CloseAtMarket(basePositions[j], basePositions[j].OpenVolume);
                }
            }
        }

        #endregion

        #region LQDT logic

        private DateTime _lastLqdtGetTime;

        private DateTime _lastLqdtActionTime = DateTime.MinValue;

        private DateTime _lastOrderExecutionTime = DateTime.MinValue;

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

        private decimal GetLqdtOpenVolume()
        {
            decimal sum = 0;

            List<Position> positions = _tabLqdt.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].Direction == Side.Buy
                    && positions[i].OpenVolume > 0
                    && positions[i].State != PositionStateType.Done)
                {
                    sum += positions[i].OpenVolume;
                }
            }

            return sum;
        }

        private decimal GetActualFreeCash()
        {
            if (_base1 == null
                || _base1.Portfolio == null)
            {
                return -1;
            }

            List<PositionOnBoard> onBoard = _base1.Portfolio.PositionOnBoard;

            if (onBoard == null)
            {
                return -1;
            }

            for (int i = 0; i < onBoard.Count; i++)
            {
                if (onBoard[i] != null
                    && string.Equals(onBoard[i].SecurityNameCode, "rub", StringComparison.OrdinalIgnoreCase))
                {
                    return onBoard[i].ValueCurrent;
                }
            }

            return -1;
        }

        private void TryLqdtParking()
        {
            if (_LqdtRegimeIsOn.ValueBool == false
                || _tabLqdt.IsReadyToTrade == false
                || _tabLqdt.Security == null)
            {
                return;
            }

            if (HasOrdersInMarket())
            {
                return;
            }

            MarketDepth book = _tabLqdt.MarketDepth;

            if (book == null)
            {
                return;
            }

            if ((DateTime.Now - _lastLqdtActionTime).TotalSeconds < 60)
            {
                return;
            }

            decimal lot = _tabLqdt.Security.Lot > 1 ? _tabLqdt.Security.Lot : 1;

            decimal idleCash = GetFreeMoneyWithGo() - GetLqdtValue();

            decimal buffer = _lqdtFreeMoneyBuffer.ValueDecimal;

            if (buffer < 500)
            {
                buffer = 500;
            }

            if (idleCash > buffer)
            {
                if (book.Asks == null
                    || book.Asks.Count == 0)
                {
                    return;
                }

                decimal price = (decimal)book.Asks[0].Price;
                decimal askVolume = (decimal)book.Asks[0].Ask;

                decimal buyMoney = idleCash - buffer;

                if (buyMoney < 1000)
                {
                    return;
                }

                decimal freeCash = GetActualFreeCash();

                if (freeCash != -1
                    && buyMoney > freeCash)
                {
                    buyMoney = freeCash;
                }

                if (buyMoney < 1000)
                {
                    return;
                }

                decimal lots = Math.Floor(buyMoney / (price * lot));

                if (lots > askVolume)
                {
                    lots = Math.Floor(askVolume);
                }

                if (lots < 1)
                {
                    return;
                }

                LogFull("LQDT park buy " + lots + " lots @ " + price);

                if (_tabLqdt.PositionOpenLong.Count > 0)
                {
                    _tabLqdt.BuyAtLimitToPosition(_tabLqdt.PositionOpenLong[0], price, lots);
                }
                else
                {
                    _tabLqdt.BuyAtLimit(lots, price);
                }

                _lastLqdtActionTime = DateTime.Now;
            }
            else if (idleCash < buffer)
            {
                decimal lqdtVolume = GetLqdtOpenVolume();

                if (lqdtVolume <= 0)
                {
                    return;
                }

                if (book.Bids == null
                    || book.Bids.Count == 0)
                {
                    return;
                }

                decimal price = (decimal)book.Bids[0].Price;
                decimal bidVolume = (decimal)book.Bids[0].Bid;

                decimal sellMoney = buffer - idleCash;

                if (sellMoney < 1000)
                {
                    return;
                }

                decimal lots = Math.Floor(sellMoney / (price * lot));

                if (lots > lqdtVolume)
                {
                    lots = lqdtVolume;
                }

                if (lots > bidVolume)
                {
                    lots = Math.Floor(bidVolume);
                }

                if (lots < 1)
                {
                    return;
                }

                LogFull("LQDT cover sell " + lots + " lots @ " + price);

                if (_tabLqdt.PositionOpenLong.Count > 0)
                {
                    _tabLqdt.CloseAtLimit(_tabLqdt.PositionOpenLong[0], price, lots);
                }

                _lastLqdtActionTime = DateTime.Now;
            }
        }

        private decimal GetBaseInvestedTotal()
        {
            decimal sum = 0;

            BotTabSimple[] bases = { _base1, _base2, _base3, _base4, _base5, _base6, _base7, _base8, _base9, _base10 };

            for (int i = 0; i < bases.Length; i++)
            {
                sum += GetBaseInvestedMoney(bases[i]);
            }

            return sum;
        }

        private decimal GetLqdtValue()
        {
            decimal volume = GetLqdtOpenVolume();

            if (volume <= 0)
            {
                return 0;
            }

            decimal lot = _tabLqdt.Security != null && _tabLqdt.Security.Lot > 1 ? _tabLqdt.Security.Lot : 1;

            return volume * lot * _tabLqdt.PriceBestBid;
        }

        private void TryEntryByLqdtSpread()
        {
            decimal lqdtYieldAnn = GetLqdtYieldAnn(GetCurrentServerTime());

            decimal minYieldAnn = lqdtYieldAnn + _entryMinYieldDiffOverLqdt.ValueDecimal;

            TryEntryAboveThreshold(minYieldAnn);
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

            BotTabSimple[] bases = { _base1, _base2, _base3, _base4, _base5, _base6, _base7, _base8, _base9, _base10 };
            BotTabScreener[] screeners = { _futs1, _futs2, _futs3, _futs4, _futs5, _futs6, _futs7, _futs8, _futs9, _futs10 };

            for (int i = 0; i < bases.Length; i++)
            {
                bases[i].PositionOpeningFailEvent += Tab_PositionOpeningFailEvent;
                bases[i].CancelOrderFailEvent += Tab_CancelOrderFailEvent;
                bases[i].OrderUpdateEvent += OnOrderUpdate;

                screeners[i].OrderUpdateEvent += (Order order, BotTabSimple tab) =>
                {
                    OnOrderUpdate(order);
                };
            }

            _tabLqdt.OrderUpdateEvent += OnOrderUpdate;
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

                DataGridViewColumn newColumn3 = new DataGridViewColumn();
                newColumn3.CellTemplate = cellParam0;
                newColumn3.HeaderText = "Series 2";
                _tableDataGrid.Columns.Add(newColumn3);
                newColumn3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn4 = new DataGridViewColumn();
                newColumn4.CellTemplate = cellParam0;
                newColumn4.HeaderText = "Series 3";
                _tableDataGrid.Columns.Add(newColumn4);
                newColumn4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn5 = new DataGridViewColumn();
                newColumn5.CellTemplate = cellParam0;
                newColumn5.HeaderText = "Close";
                _tableDataGrid.Columns.Add(newColumn5);
                newColumn5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

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
                else if (column == 4)
                {
                    ShowFuturesChart(_monitorRows[row], 2);
                }
                else if (column == 5)
                {
                    CloseBondWithConfirm(_monitorRows[row]);
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

        private void CloseBondWithConfirm(BondMonitorRow rowData)
        {
            try
            {
                if (rowData.Futs == null)
                {
                    return;
                }

                List<string> info = new List<string>();

                decimal baseVol = GetOpenBuyVolume(rowData.Base);

                if (baseVol > 0)
                {
                    info.Add(rowData.BaseName + "  Buy  " + baseVol);
                }

                for (int i = 0; i < rowData.Futs.Tabs.Count; i++)
                {
                    decimal sellVol = GetOpenSellVolumeSingle(rowData.Futs.Tabs[i]);

                    if (sellVol > 0)
                    {
                        info.Add(rowData.Futs.Tabs[i].Connector?.SecurityName + "  Sell  " + sellVol);
                    }
                }

                if (info.Count == 0)
                {
                    CustomMessageBoxUi uiInfo = new CustomMessageBoxUi(OsLocalization.ConvertToLocString(
                        "Eng:No open positions for " + rowData.BaseName + "_" +
                        "Ru:Нет открытых позиций по " + rowData.BaseName + "_"));
                    uiInfo.ShowDialog();
                    return;
                }

                string message = OsLocalization.ConvertToLocString(
                    "Eng:Closing positions for " + rowData.BaseName + "_Ru:Закрываем позиции по " + rowData.BaseName + "_") + "\n\n";

                for (int i = 0; i < info.Count; i++)
                {
                    message += info[i] + "\n";
                }

                message += "\n" + OsLocalization.ConvertToLocString("Eng:Continue_Ru:Продолжить_") + "?";

                AcceptDialogUi ui = new AcceptDialogUi(message);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                LogFull("MANUAL CLOSE: " + rowData.BaseName);

                TryClosePairMarket(rowData.Base, rowData.Futs);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private decimal GetOpenSellVolumeSingle(BotTabSimple tab)
        {
            decimal sum = 0;

            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State == PositionStateType.Open
                    && positions[i].Direction == Side.Sell
                    && positions[i].OpenVolume > 0)
                {
                    sum += positions[i].OpenVolume;
                }
            }

            return sum;
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

            List<BotTabSimple> nearestSeries = GetNearestSeries(screener, time, 3);

            for (int i = 0; i < nearestSeries.Count; i++)
            {
                BotTabSimple seriesTab = nearestSeries[i];

                SeriesInfo info = new SeriesInfo();
                info.Tab = seriesTab;
                info.Name = seriesTab.Connector?.SecurityName;
                info.Expiration = seriesTab.Security.Expiration;
                info.DaysToExpiration = (info.Expiration - time).Days;
                (decimal contangoAbs, decimal yieldAnn) = CalculateContangoForMonitor(baseSource, seriesTab, mult, info.DaysToExpiration);
                info.YieldPercent = yieldAnn;
                info.ContangoAbsPercent = contangoAbs;

                SetTabPosInfo(seriesTab, info);

                newRow.Series.Add(info);
            }

            rows.Add(newRow);
        }

        private void SetTabPosInfo(BotTabSimple tab, BondMonitorRow row)
        {
            List<Position> positions = tab.PositionsOpenAll;

            decimal sum = 0;
            Side side = Side.Buy;
            bool has = false;

            for (int i = 0; i < positions.Count; i++)
            {
                if ((positions[i].State == PositionStateType.Opening
                        || positions[i].State == PositionStateType.Open
                        || positions[i].State == PositionStateType.Closing)
                    && positions[i].OpenVolume > 0)
                {
                    sum += positions[i].OpenVolume;
                    side = positions[i].Direction;
                    has = true;
                }
            }

            row.BaseHasPosition = has;
            row.BasePosVolume = sum;
            row.BasePosSide = side;
        }

        private void SetTabPosInfo(BotTabSimple tab, SeriesInfo info)
        {
            List<Position> positions = tab.PositionsOpenAll;

            decimal sum = 0;
            Side side = Side.Buy;
            bool has = false;

            for (int i = 0; i < positions.Count; i++)
            {
                if ((positions[i].State == PositionStateType.Opening
                        || positions[i].State == PositionStateType.Open
                        || positions[i].State == PositionStateType.Closing)
                    && positions[i].OpenVolume > 0)
                {
                    sum += positions[i].OpenVolume;
                    side = positions[i].Direction;
                    has = true;
                }
            }

            info.HasPosition = has;
            info.PosVolume = sum;
            info.PosSide = side;
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

        private (decimal ContangoAbs, decimal YieldAnn) CalculateContangoForMonitor(BotTabSimple baseSource, BotTabSimple futuresSource, decimal mult, int daysToExpiration)
        {
            if (baseSource.PriceBestAsk == 0
                || futuresSource.PriceBestBid == 0)
            {
                return (0, 0);
            }

            decimal deviation = futuresSource.PriceBestBid / mult - baseSource.PriceBestAsk;
            deviation = deviation / (baseSource.PriceBestAsk / 100);

            decimal yieldAnn = 0;

            if (daysToExpiration > 0)
            {
                yieldAnn = deviation * 365 / daysToExpiration;
            }

            return (deviation, yieldAnn);
        }

        private void UpdateTable()
        {
            // 0 Stock
            // 1 Mult
            // 2 Series 1

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

                    for (int col = 0; col <= 5; col++)
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

            for (int i = 0; i < 3; i++)
            {
                row.Cells.Add(new DataGridViewButtonCell());
                row.Cells[^1].ReadOnly = true;

                if (data.Series.Count > i)
                {
                    string text;

                    if (data.Futs == null)
                    {
                        text = "LQDT  " + Math.Round(data.Series[0].YieldPercent, 2) + "% ann";
                    }
                    else
                    {
                        text = data.Series[i].Name
                            + "  " + Math.Round(data.Series[i].YieldPercent, 2) + "%";
                    }

                    if (data.Series[i].HasPosition)
                    {
                        text += " (" + data.Series[i].PosVolume + ")";
                        row.Cells[^1].Style.ForeColor = data.Series[i].PosSide == Side.Buy
                            ? System.Drawing.Color.LimeGreen
                            : System.Drawing.Color.OrangeRed;
                    }

                    row.Cells[^1].Value = text;
                }
                else
                {
                    row.Cells[^1].Value = "";
                }
            }

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;

            if (data.Futs != null)
            {
                row.Cells[^1].Value = "Close";
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
    }
}
