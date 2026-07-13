/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.OsTrader;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Threading;

/* Description
Робот ребалансирует капитал между акциями МосБиржи, золотом и LQDTMOEX по моментуму.
Только лонг, без плеча.
*/

namespace OsEngine.Robots.Rebalancers
{
    [Bot("RebalancerByMomentum")]
    public class RebalancerByMomentum : BotPanel
    {
        #region Fields

        private BotTabScreener _tabScreenerStocks;
        private BotTabSimple _tabLqdt;
        private BotTabSimple _tabGold;

        private StrategyParameterString _regime;
        private StrategyParameterString _mainRebalancePeriodType;
        private StrategyParameterString _mainRebalanceDayOfWeek;
        private StrategyParameterTimeOfDay _mainRebalanceTime;

        private StrategyParameterBool _stockRebalanceOn;

        private StrategyParameterInt _stockMomentumLookback;
        private StrategyParameterInt _stockTopN;
        private StrategyParameterDecimal _stockMinMomentum;
        private StrategyParameterDecimal _stockMaxInvestedPercent;
        private StrategyParameterInt _stockKeltnerEmaPeriod;
        private StrategyParameterInt _stockKeltnerAtrPeriod;
        private StrategyParameterDecimal _stockKeltnerMultiplier;
        private StrategyParameterDecimal _stockMinRisingPercent;
        private StrategyParameterInt _stockIcebergOrdersCount;
        private StrategyParameterInt _stockIcebergMsDistance;

        private StrategyParameterBool _lqdtRebalanceOn;
        private StrategyParameterTimeOfDay _lqdtRebalanceTime;
        private StrategyParameterDecimal _lqdtMaxInvestedPercent;

        private StrategyParameterBool _goldRebalanceOn;
        private StrategyParameterInt _goldMomentumLookback;
        private StrategyParameterDecimal _goldMinMomentum;
        private StrategyParameterDecimal _goldMaxInvestedPercent;
        private StrategyParameterInt _goldKeltnerEmaPeriod;
        private StrategyParameterInt _goldKeltnerAtrPeriod;
        private StrategyParameterDecimal _goldKeltnerMultiplier;
        private StrategyParameterInt _goldIcebergOrdersCount;
        private StrategyParameterInt _goldIcebergMsDistance;

        private StrategyParameterButton _rebalanceNowButton;
        private StrategyParameterButton _rebalanceLqdtNowButton;

        #endregion

        #region Constructor

        public RebalancerByMomentum(string name, StartProgram startProgram) : base(name, startProgram)
        {

            // 1 Общие настройки

            _regime = CreateParameter("Regime", "Off", new[] { "On", "Off" }, "Base");
            _mainRebalancePeriodType = CreateParameter("Rebalance period", "Weekly", new[] { "Daily", "Weekly", "Monthly" }, "Base");
            _mainRebalanceDayOfWeek = CreateParameter("Rebalance day of week", "Tuesday", new[] { "Monday", "Tuesday", "Wednesday" }, "Base");
            _mainRebalanceTime = CreateParameterTimeOfDay("Main rebalance time", 10, 0, 0, 0, "Base");

            _rebalanceNowButton = CreateParameterButton("Rebalance NOW", "Base");
            _rebalanceNowButton.UserClickOnButtonEvent += RebalanceNowButton_UserClickOnButtonEvent;

            // 2 Настройки ребалансировки акций

            _tabScreenerStocks = TabCreate<BotTabScreener>();

            _stockRebalanceOn = CreateParameter("Stock rebalance on", true, "Stock");
            _stockMinRisingPercent = CreateParameter("Stock % grow", 54.0m, 0.0m, 100.0m, 1.0m, "Stock");
            _stockMomentumLookback = CreateParameter("Stock momentum lookback", 49, 10, 250, 10, "Stock");
            _stockTopN = CreateParameter("Stock Top N", 10, 1, 20, 1, "Stock");
            _stockMinMomentum = CreateParameter("Stock min momentum", 103.9m, 101.0m, 106.0m, 0.5m, "Stock");
            _stockMaxInvestedPercent = CreateParameter("Stock max % deposit", 50.0m, 10.0m, 100.0m, 10.0m, "Stock");
            _stockKeltnerEmaPeriod = CreateParameter("Stock keltner EMA period", 375, 5, 100, 1, "Stock");
            _stockKeltnerAtrPeriod = CreateParameter("Stock keltner ATR period", 20, 5, 100, 1, "Stock");
            _stockKeltnerMultiplier = CreateParameter("Stock keltner multiplier", 5.5m, 0.5m, 5.0m, 0.1m, "Stock");
            _stockIcebergOrdersCount = CreateParameter("Stock iceberg orders count", 1, 1, 50, 1, "Stock");
            _stockIcebergMsDistance = CreateParameter("Stock iceberg ms distance", 100, 1, 10000, 100, "Stock");

            _stockMomentumLookback.ValueChange += OnMomentumStocksParameterChanged;
            _stockKeltnerEmaPeriod.ValueChange += OnKeltnerStocksParameterChanged;
            _stockKeltnerAtrPeriod.ValueChange += OnKeltnerStocksParameterChanged;
            _stockKeltnerMultiplier.ValueChange += OnKeltnerStocksParameterChanged;

            _tabScreenerStocks.CreateCandleIndicator(1, "KeltnerChannel", new List<string>() {
                _stockKeltnerEmaPeriod.ValueInt.ToString(),
                _stockKeltnerAtrPeriod.ValueInt.ToString(),
                _stockKeltnerAtrPeriod.ValueInt.ToString(),
                _stockKeltnerMultiplier.ValueDecimal.ToString(),
                "Typical"
            }, "Prime");

            _tabScreenerStocks.CreateCandleIndicator(2, "Momentum", new List<string>() {
                _stockMomentumLookback.ValueInt.ToString(),
                "Close"
            }, "Second");

            // 3 Настройки ребалансировки золота

            _tabGold = TabCreate<BotTabSimple>();

            _goldRebalanceOn = CreateParameter("Gold rebalance on", true, "Gold");
            _goldMomentumLookback = CreateParameter("Gold momentum lookback", 60, 10, 250, 10, "Gold");
            _goldMinMomentum = CreateParameter("Gold max momentum", 102.3m, 99.0m, 106.0m, 0.3m, "Gold");
            _goldMaxInvestedPercent = CreateParameter("Gold max % deposit", 50.0m, 10.0m, 100.0m, 10.0m, "Gold");
            _goldKeltnerEmaPeriod = CreateParameter("Gold keltner EMA period", 640, 5, 100, 1, "Gold");
            _goldKeltnerAtrPeriod = CreateParameter("Gold keltner ATR period", 20, 5, 100, 1, "Gold");
            _goldKeltnerMultiplier = CreateParameter("Gold keltner multiplier", 1.4m, 0.5m, 5.0m, 0.1m, "Gold");
            _goldIcebergOrdersCount = CreateParameter("Gold iceberg orders count", 1, 1, 50, 1, "Gold");
            _goldIcebergMsDistance = CreateParameter("Gold iceberg ms distance", 100, 1, 10000, 100, "Gold");

            _goldMomentumLookback.ValueChange += OnGoldMomentumParameterChanged;
            _goldKeltnerEmaPeriod.ValueChange += OnGoldKeltnerParameterChanged;
            _goldKeltnerAtrPeriod.ValueChange += OnGoldKeltnerParameterChanged;
            _goldKeltnerMultiplier.ValueChange += OnGoldKeltnerParameterChanged;

            Aindicator keltnerGold = IndicatorsFactory.CreateIndicatorByName("KeltnerChannel", name + "KeltnerChannel", false);
            keltnerGold = (Aindicator)_tabGold.CreateCandleIndicator(keltnerGold, "Prime");
            ((IndicatorParameterInt)keltnerGold.Parameters[0]).ValueInt = _goldKeltnerEmaPeriod.ValueInt;
            ((IndicatorParameterInt)keltnerGold.Parameters[1]).ValueInt = _goldKeltnerAtrPeriod.ValueInt;
            ((IndicatorParameterInt)keltnerGold.Parameters[2]).ValueInt = _goldKeltnerAtrPeriod.ValueInt;
            ((IndicatorParameterDecimal)keltnerGold.Parameters[3]).ValueDecimal = _goldKeltnerMultiplier.ValueDecimal;
            keltnerGold.Save();

            Aindicator momentumGold = IndicatorsFactory.CreateIndicatorByName("Momentum", name + "Momentum", false);
            momentumGold = (Aindicator)_tabGold.CreateCandleIndicator(momentumGold, "MomentumArea");
            ((IndicatorParameterInt)momentumGold.Parameters[0]).ValueInt = _goldMomentumLookback.ValueInt;
            momentumGold.Save();

            // 4 Настройки ребалансировки LQDT

            _tabLqdt = TabCreate<BotTabSimple>();

            _lqdtRebalanceOn = CreateParameter("LQDT rebalance on", true, "LQDT");
            _lqdtRebalanceTime = CreateParameterTimeOfDay("LQDT rebalance time", 11, 0, 0, 0, "LQDT");
            _lqdtMaxInvestedPercent = CreateParameter("LQDT max % deposit", 49.0m, 10.0m, 100.0m, 10.0m, "LQDT");

            _rebalanceLqdtNowButton = CreateParameterButton("Rebalance LQDT NOW", "LQDT");
            _rebalanceLqdtNowButton.UserClickOnButtonEvent += RebalanceLqdtNowButton_UserClickOnButtonEvent;

            // 5 Подписки на события

            _tabScreenerStocks.CandlesSyncFinishedEvent += _tabScreener_CandlesSyncFinishedEvent;
            _tabScreenerStocks.CandleFinishedEvent += _tabScreenerStocks_CandleFinishedEvent;
            Description = OsLocalization.ConvertToLocString(
                "Eng:Rebalancer by momentum between MOEX stocks, Gold and LQDTMOEX_" +
                "Ru:Робот ребалансирует капитал между акциями МосБиржи, золотом и LQDTMOEX по моментуму_");

        }

        private void OnGoldKeltnerParameterChanged()
        {
            Aindicator keltnerGold = _tabGold.Indicators[0] as Aindicator;

            ((IndicatorParameterInt)keltnerGold.Parameters[0]).ValueInt = _goldKeltnerEmaPeriod.ValueInt;
            ((IndicatorParameterInt)keltnerGold.Parameters[1]).ValueInt = _goldKeltnerAtrPeriod.ValueInt;
            ((IndicatorParameterInt)keltnerGold.Parameters[2]).ValueInt = _goldKeltnerAtrPeriod.ValueInt;
            ((IndicatorParameterDecimal)keltnerGold.Parameters[3]).ValueDecimal = _goldKeltnerMultiplier.ValueDecimal;
            keltnerGold.Reload();
            keltnerGold.Save();
        }

        private void OnGoldMomentumParameterChanged()
        {
            Aindicator momentumGold = _tabGold.Indicators[1] as Aindicator;
            ((IndicatorParameterInt)momentumGold.Parameters[0]).ValueInt = _goldMomentumLookback.ValueInt;
            momentumGold.Reload();
            momentumGold.Save();
        }

        private void OnMomentumStocksParameterChanged()
        {
            for (int i = 0; i < _tabScreenerStocks._indicators.Count; i++)
            {
                if (_tabScreenerStocks._indicators[i].Num == 2)
                {
                    _tabScreenerStocks._indicators[i].Parameters[0] = _stockMomentumLookback.ValueInt.ToString();
                    break;
                }
            }

            _tabScreenerStocks.UpdateIndicatorsParameters();
        }

        private void OnKeltnerStocksParameterChanged()
        {
            for (int i = 0; i < _tabScreenerStocks._indicators.Count; i++)
            {
                if (_tabScreenerStocks._indicators[i].Num == 1)
                {
                    _tabScreenerStocks._indicators[i].Parameters[0] = _stockKeltnerEmaPeriod.ValueInt.ToString();
                    _tabScreenerStocks._indicators[i].Parameters[1] = _stockKeltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerStocks._indicators[i].Parameters[2] = _stockKeltnerAtrPeriod.ValueInt.ToString();
                    _tabScreenerStocks._indicators[i].Parameters[3] = _stockKeltnerMultiplier.ValueDecimal.ToString();
                    break;
                }
            }

            _tabScreenerStocks.UpdateIndicatorsParameters();
        }

        #endregion

        #region Helpers

        private bool IsRebalanceDay(DateTime serverTime)
        {
            string period = _mainRebalancePeriodType.ValueString;

            if (period == "Daily")
            {
                return true;
            }

            DayOfWeek targetDay = ParseDayOfWeek(_mainRebalanceDayOfWeek.ValueString);

            if (serverTime.DayOfWeek != targetDay)
            {
                return false;
            }

            if (period == "Weekly")
            {
                return true;
            }

            if (period == "Monthly")
            {
                if (serverTime.Day > 7)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private DayOfWeek ParseDayOfWeek(string day)
        {
            if (Enum.TryParse(day, true, out DayOfWeek result))
            {
                return result;
            }

            return DayOfWeek.Monday;
        }

        private decimal GetCurrentCapital()
        {
            if (_tabLqdt.Portfolio == null)
            {
                return 0m;
            }

            decimal capital = _tabLqdt.Portfolio.ValueCurrent;

            if (capital == 0m)
            {
                capital = _tabLqdt.Portfolio.ValueBegin;
            }

            return capital;
        }

        #endregion

        #region Events to entry logic

        private bool _optimizerEventSubscribed = false;

        private DateTime _lastMainRebalanceDate = DateTime.MinValue;

        private DateTime _lastLqdtRebalanceDate = DateTime.MinValue;

        private void _tabScreenerStocks_CandleFinishedEvent(List<Candle> candles, BotTabSimple source)
        {
            if (source.Connector.ServerType != Market.ServerType.Optimizer)
            {
                return;
            }

            if (_optimizerEventSubscribed == true)
            {
                return;
            }

            _optimizerEventSubscribed = true;

            OptimizerServer server = source.Connector.MyServer as OptimizerServer;

            server.EndNextMinuteWithCandlesEvent += Server_EndNextMinuteWithCandlesEvent;

        }

        private void Server_EndNextMinuteWithCandlesEvent()
        {
            _tabScreener_CandlesSyncFinishedEvent(_tabScreenerStocks.Tabs);
        }

        private void _tabScreener_CandlesSyncFinishedEvent(List<BotTabSimple> tabs)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader)
            {
                Thread worker = new Thread(MainLogic);
                worker.Start();
            }
            else
            {
                MainLogic();
            }
        }

        #endregion

        #region Main logic

        private void MainLogic()
        {
            try
            {
                if (_regime.ValueString == "Off")
                {
                    return;
                }

                List<BotTabSimple> tabs = _tabScreenerStocks.Tabs;

                if (tabs == null
                    || tabs.Count == 0
                    || tabs[0].CandlesAll == null
                    || tabs[0].CandlesAll.Count < 10)
                {
                    return;
                }

                Candle lastCandle = tabs[0].CandlesAll[^1];

                DateTime serverTime = TimeServer;

                if (serverTime == DateTime.MinValue)
                {
                    return;
                }

                if (_lastMainRebalanceDate.Date != serverTime.Date
                    && IsMainRebalanceTime(serverTime, lastCandle, tabs[0]))
                {
                    _lastMainRebalanceDate = serverTime;
                    ExecuteMainRebalance();
                }
                else if (_lqdtRebalanceOn.ValueBool
                    && _lastLqdtRebalanceDate.Date != serverTime.Date
                    && IsLqdtRebalanceTime(serverTime, lastCandle))
                {
                    _lastLqdtRebalanceDate = serverTime;
                    ExecuteLqdtRebalance(serverTime);
                    return;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ExecuteMainRebalance()
        {
            try
            {
                _lastMainRebalanceDate = TimeServer;

                SendNewLogMessage("Main rebalance started", LogMessageType.System);

                RebalancerPositionPackage stocksPoses = GetStocksToEntry();

                // 1 по акциям позиций быть не должно
                if (stocksPoses.TabsToEntry.Count == 0)
                {
                    int countClosedPoses = 0;

                    for (int i = 0; i < _tabScreenerStocks.Tabs.Count; i++)
                    {
                        BotTabSimple currentTab = _tabScreenerStocks.Tabs[i];

                        if (currentTab.IsConnected == false
                            || currentTab.IsReadyToTrade == false)
                        {
                            continue;
                        }

                        List<Position> positions = currentTab.PositionsOpenAll;

                        if (positions.Count == 0)
                        {
                            continue;
                        }
                        countClosedPoses++;
                        currentTab.CloseAtIcebergMarket(positions[0], positions[0].OpenVolume, _stockIcebergOrdersCount.ValueInt, _stockIcebergMsDistance.ValueInt);
                    }

                    if (countClosedPoses > 0
                        && StartProgram == StartProgram.IsOsTrader)
                    {
                        // в реале. Если были сделки на закрытие позиций по акциям. То ждём пока обновится портфель.
                        // это нужно, чтобы правильно определить позицию по золоту
                        Thread.Sleep(30000);
                    }

                    if (NeedBuyGold() == true)
                    {// 2 если по акциям позиций нет, то проверяем золото
                        decimal capital = GetCurrentCapital();
                        decimal targetMoney = capital * _goldMaxInvestedPercent.ValueDecimal / 100m;
                        decimal freeMoney = GetFreeMoney();

                        decimal moneyToUse = targetMoney;

                        if (StartProgram == StartProgram.IsOsTrader)
                        {
                            moneyToUse = Math.Min(targetMoney, freeMoney);
                        }

                        if (moneyToUse > 0)
                        {
                            EntryInPositions(_tabGold, moneyToUse, Side.Buy, _goldIcebergOrdersCount.ValueInt, _goldIcebergMsDistance.ValueInt);
                        }
                        else
                        {
                            SendNewLogMessage("Not enough free money to buy gold", LogMessageType.System);
                        }

                        if (_tabGold.PositionsOpenAll.Count == 0)
                        {

                        }
                    }
                    else
                    {
                        TryCloseGoldPosition();
                    }
                }
                else if (stocksPoses.TabsToEntry.Count != 0
                    && _tabScreenerStocks.PositionsOpenAll.Count == 0)
                {
                    int multiplier = 0;

                    if (stocksPoses.TabsToEntry.Count > 0)
                    {
                        multiplier++;
                    }

                    if (multiplier != 0)
                    {
                        stocksPoses.MoneyOnAllPositions /= multiplier;
                    }

                    if (stocksPoses.TabsToEntry.Count > 0)
                    {
                        stocksPoses.MoneyOnOnePosition = stocksPoses.MoneyOnAllPositions / stocksPoses.TabsToEntry.Count;
                    }

                    for (int i = 0; i < stocksPoses.TabsToEntry.Count; i++)
                    {
                        EntryInPositions(stocksPoses.TabsToEntry[i], stocksPoses.MoneyOnOnePosition, stocksPoses.Direction, _stockIcebergOrdersCount.ValueInt, _stockIcebergMsDistance.ValueInt);
                    }
                }

                if (stocksPoses.TabsToEntry.Count != 0)
                {
                    TryCloseGoldPosition();
                }

                SendNewLogMessage("Main rebalance finished", LogMessageType.System);
            }
            catch (Exception error)
            {
                SendNewLogMessage($"Main rebalance error: {error}", LogMessageType.Error);
            }
        }

        private void ExecuteLqdtRebalance(DateTime serverTime)
        {
            try
            {
                _lastLqdtRebalanceDate = serverTime;

                SendNewLogMessage("LQDT rebalance started", LogMessageType.System);

                RebalanceLqdt();

                SendNewLogMessage("LQDT rebalance finished", LogMessageType.System);
            }
            catch (Exception error)
            {
                SendNewLogMessage($"LQDT rebalance error: {error}", LogMessageType.Error);
            }
        }

        private void EntryInPositions(BotTabSimple tab, decimal targetMoney, Side direction, int icebergOrdersCount, int icebergMsDistance)
        {
            if (direction == Side.None)
            {
                throw new Exception("No position side value. Tab: " + tab.TabName);
            }

            if (tab == null || tab.Security == null || tab.CandlesAll == null || tab.CandlesAll.Count == 0)
            {
                return;
            }

            decimal price = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;

            if (price == 0)
            {
                return;
            }

            decimal currentVolume = GetOpenVolume(tab);
            decimal currentMoney = currentVolume * price;

            if (currentMoney >= targetMoney)
            {
                return;
            }

            decimal moneyToBuy = targetMoney - currentMoney;
            decimal volumeToEntry = CalculateVolumeForMoney(tab, moneyToBuy);

            if (volumeToEntry <= 0)
            {
                return;
            }

            List<Position> positions = tab.PositionsOpenAll;

            // не доливаем в существующую позицию, только новый вход
            if (positions.Count > 0)
            {
                return;
            }

            if (direction == Side.Buy)
            {
                tab.BuyAtIcebergMarket(volumeToEntry, icebergOrdersCount, icebergMsDistance);
            }
            else if (direction == Side.Sell)
            {
                tab.SellAtIcebergMarket(volumeToEntry, icebergOrdersCount, icebergMsDistance);
            }
        }

        private bool IsMainRebalanceTime(DateTime serverTime, Candle candle, BotTabSimple tab)
        {
            if (candle == null)
            {
                return false;
            }

            DateTime realTime = candle.TimeStart + tab.TimeFrameBuilder.TimeFrameTimeSpan;

            if (realTime.TimeOfDay.Hours != _mainRebalanceTime.Value.TimeSpan.Hours
               || realTime.TimeOfDay.Minutes != _mainRebalanceTime.Value.TimeSpan.Minutes)
            {
                return false;
            }

            return IsRebalanceDay(serverTime);
        }

        private void RebalanceNowButton_UserClickOnButtonEvent()
        {
            try
            {
                SendNewLogMessage("Manual main rebalance requested", LogMessageType.System);

                Thread worker = new Thread(ExecuteMainRebalance);
                worker.Start();
            }
            catch (Exception error)
            {
                SendNewLogMessage($"Manual main rebalance error: {error}", LogMessageType.Error);
            }
        }

        private void RebalanceLqdtNowButton_UserClickOnButtonEvent()
        {
            try
            {
                SendNewLogMessage("Manual LQDT rebalance requested", LogMessageType.System);
                ExecuteLqdtRebalance(TimeServer);
            }
            catch (Exception error)
            {
                SendNewLogMessage($"Manual LQDT rebalance error: {error}", LogMessageType.Error);
            }
        }

        #endregion

        #region Lqdt rebalance

        private bool IsLqdtRebalanceTime(DateTime serverTime, Candle candle)
        {
            if (candle == null)
            {
                return false;
            }

            DateTime realTime = candle.TimeStart + _tabLqdt.TimeFrameBuilder.TimeFrameTimeSpan;

            if (realTime.TimeOfDay.Hours != _lqdtRebalanceTime.Value.TimeSpan.Hours
               || realTime.TimeOfDay.Minutes != _lqdtRebalanceTime.Value.TimeSpan.Minutes)
            {
                return false;
            }

            return IsRebalanceDay(serverTime);
        }

        private void RebalanceLqdt()
        {
            List<Position> stockPoses = _tabScreenerStocks.PositionsOpenAll;
            List<Position> goldPoses = _tabGold.PositionsOpenAll;

            if ((stockPoses.Count != 0 || goldPoses.Count != 0)
                && _tabLqdt.PositionsOpenAll.Count == 0)
            {
                return;
            }
            else if ((stockPoses.Count != 0 || goldPoses.Count != 0)
                 && _tabLqdt.PositionsOpenAll.Count != 0)
            {
                _tabLqdt.CloseAtMarket(_tabLqdt.PositionsOpenAll[0], _tabLqdt.PositionsOpenAll[0].OpenVolume);
                return;
            }
            else if (_tabLqdt.StartProgram == StartProgram.IsTester
                || _tabLqdt.StartProgram == StartProgram.IsOsOptimizer)
            {
                double daysToExpiration = (_tabLqdt.Security.Expiration - _tabLqdt.TimeServerCurrent).TotalDays;

                if (daysToExpiration <= 5)
                {// выходим за пять дней до конца тестирования.
                    if (_tabLqdt.PositionsOpenAll.Count != 0)
                    {
                        _tabLqdt.CloseAtMarket(_tabLqdt.PositionsOpenAll[0], _tabLqdt.PositionsOpenAll[0].OpenVolume);
                    }

                    return;
                }
                if (_tabLqdt.PositionsOpenAll.Count != 0
                    && _tabLqdt.TimeServerCurrent.Year != _tabLqdt.PositionsOpenAll[0].TimeOpen.Year)
                { // выходим и переоткрываем позицию, если наступил новый год, чтобы не держать позицию через годовой отчет.
                    decimal volume = _tabLqdt.PositionsOpenAll[0].OpenVolume;
                    _tabLqdt.CloseAtMarket(_tabLqdt.PositionsOpenAll[0], volume);
                    _tabLqdt.BuyAtMarket(volume);
                    return;
                }
            }

            TryBuyLqdt();
        }

        private void TryBuyLqdt()
        {
            if (_tabLqdt == null
                || _tabLqdt.Security == null
                || _tabLqdt.CandlesAll == null
                || _tabLqdt.CandlesAll.Count == 0)
            {
                return;
            }

            if (_tabLqdt.PositionsOpenAll.Count > 0)
            {
                return;
            }

            decimal targetMoney = GetCurrentCapital() * (_lqdtMaxInvestedPercent.ValueDecimal / 100m);
            decimal freeMoney = GetFreeMoney();

            decimal moneyToUse = targetMoney;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                moneyToUse = Math.Min(targetMoney, freeMoney);
            }

            if (moneyToUse <= 0)
            {
                return;
            }

            decimal volumeToBuy = CalculateVolumeForMoney(_tabLqdt, moneyToUse);

            if (volumeToBuy <= 0)
            {
                return;
            }

            _tabLqdt.BuyAtMarket(volumeToBuy);
        }

        private decimal GetFreeMoney()
        {
            try
            {
                if (_tabLqdt.Portfolio == null)
                {
                    return 0m;
                }

                if (_tabLqdt.Connector.MyServer.ServerType == ServerType.TInvest)
                {
                    List<PositionOnBoard> positions = _tabLqdt.Portfolio.GetPositionOnBoard();

                    for (int i = 0; i < positions.Count; i++)
                    {
                        if (positions[i].SecurityNameCode == "rub")
                        {
                            return positions[i].ValueCurrent;
                        }
                    }
                }
                else if (_tabLqdt.Connector.MyServer.ServerType == ServerType.Tester)
                {
                    decimal portfolioValue = _tabLqdt.Portfolio.ValueCurrent;
                    decimal volumeToPositions = GetVolumeToPositions();
                    return portfolioValue - volumeToPositions;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage("GetFreeMoney: " + ex.Message, LogMessageType.Error);
            }

            return 0m;
        }

        private decimal GetVolumeToPositions()
        {
            decimal volumeToPosition = 0m;

            for (int i = 0; i < OsTraderMaster.Master.PanelsArray.Count; i++)
            {
                List<Position> positionsBot = OsTraderMaster.Master.PanelsArray[i].OpenPositions;

                for (int j = 0; j < positionsBot.Count; j++)
                {
                    decimal margin = GetMarginSecurities(positionsBot[j].SecurityName, positionsBot[j].Direction);

                    if (margin > 1)
                    {
                        volumeToPosition += positionsBot[j].OpenVolume * margin * positionsBot[j].Lots;
                    }
                    else if (margin <= 1 && _tabLqdt.Connector.MyServer.ServerType == ServerType.Tester)
                    {
                        volumeToPosition += positionsBot[j].OpenVolume * positionsBot[j].EntryPrice * positionsBot[j].Lots;
                    }
                }
            }

            return volumeToPosition;
        }

        private decimal GetMarginSecurities(string nameSecurity, Side side)
        {
            List<Security> securities = _tabLqdt.Connector.MyServer.Securities;

            for (int i = 0; i < securities.Count; i++)
            {
                if (securities[i].Name == nameSecurity)
                {
                    return side == Side.Buy ? securities[i].MarginBuy : securities[i].MarginSell;
                }
            }

            return 0m;
        }

        #endregion

        #region Stocks rebalance

        private RebalancerPositionPackage GetStocksToEntry()
        {
            RebalancerPositionPackage result = new RebalancerPositionPackage();
            result.Direction = Side.Buy;

            List<BotTabSimple> stockTabs = _tabScreenerStocks.Tabs;

            if (stockTabs.Count == 0)
            {
                SendNewLogMessage("No stock tabs available for rebalancing", LogMessageType.Error);
                return result;
            }

            // 1. Считаем текущие рабочие акции, которые можно купить

            if (_stockRebalanceOn.ValueBool == true
                && IsEnoughStocksAboveKeltnerCenter(stockTabs))
            {
                List<StockCandidate> candidates = new List<StockCandidate>();

                for (int i = 0; i < _tabScreenerStocks.Tabs.Count; i++)
                {
                    BotTabSimple tab = _tabScreenerStocks.Tabs[i];

                    Aindicator momentum = (Aindicator)tab.Indicators[1];

                    if (momentum.DataSeries[0].Last < _stockMinMomentum.ValueDecimal)
                    {
                        continue;
                    }

                    Aindicator keltner = (Aindicator)tab.Indicators[0];

                    decimal upChannel = keltner.DataSeries[1].Last;
                    decimal lastPrice = tab.CandlesAll[^1].Close;

                    if (lastPrice < upChannel)
                    {
                        continue;
                    }

                    candidates.Add(new StockCandidate
                    {
                        Tab = tab,
                        Momentum = momentum.DataSeries[0].Last
                    });
                }

                // 2. Сортируем по убыванию моментума и берём топ-N

                candidates.Sort((a, b) => b.Momentum.CompareTo(a.Momentum));

                for (int i = 0; i < candidates.Count && i < _stockTopN.ValueInt; i++)
                {
                    result.TabsToEntry.Add(candidates[i].Tab);
                }
            }

            // 3 Считаем деньги на одну акцию

            if (result.TabsToEntry.Count > 0)
            {
                decimal capital = GetCurrentCapital();
                decimal maxInvested = capital * _stockMaxInvestedPercent.ValueDecimal / 100m;
                result.MoneyOnAllPositions = maxInvested;
            }

            return result;

        }

        private bool IsEnoughStocksAboveKeltnerCenter(List<BotTabSimple> tabs)
        {
            int countRising = 0;

            for (int i = 0; i < tabs.Count; i++)
            {
                Aindicator keltner = (Aindicator)tabs[i].Indicators[0];
                decimal centerKeltnerChannel = keltner.DataSeries[3].Last;

                if (tabs[i].CandlesAll == null
                    || tabs[i].CandlesAll.Count == 0)
                {
                    continue;
                }

                decimal lastPrice = tabs[i].CandlesAll[^1].Close;

                if (lastPrice >= centerKeltnerChannel)
                {
                    countRising++;
                }
            }

            decimal minRisingCount = tabs.Count * _stockMinRisingPercent.ValueDecimal / 100m;

            if (countRising < minRisingCount)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Gold rebalance

        private bool NeedBuyGold()
        {
            if (_goldRebalanceOn.ValueBool == false)
            {
                return false;
            }

            if (_tabGold.IsReadyToTrade == false
                || _tabGold.CandlesAll == null
                || _tabGold.CandlesAll.Count < 10)
            {
                return false;
            }


            // Проверяем условия входа в золото

            Aindicator keltner = (Aindicator)_tabGold.Indicators[0];

            Aindicator momentum = (Aindicator)_tabGold.Indicators[1];

            decimal lastMomentum = momentum.DataSeries[0].Last;

            if (lastMomentum > _goldMinMomentum.ValueDecimal
                || lastMomentum < 99.5m)
            {
                return false;
            }

            decimal upChannel = keltner.DataSeries[1].Last;

            decimal lastPrice = _tabGold.CandlesAll[^1].Close;

            if (lastPrice < upChannel)
            {
                return false;
            }

            return true;

        }

        private void TryCloseGoldPosition()
        {
            if (_tabGold.PositionsOpenAll.Count == 0)
            {
                return;
            }

            if (_tabGold.IsConnected == false
                || _tabGold.IsReadyToTrade == false)
            {
                return;
            }

            _tabGold.CloseAtIcebergMarket(_tabGold.PositionsOpenAll[0], _tabGold.PositionsOpenAll[0].OpenVolume, _goldIcebergOrdersCount.ValueInt, _goldIcebergMsDistance.ValueInt);
        }

        #endregion

        #region Position management

        private class StockCandidate
        {
            public BotTabSimple Tab;
            public decimal Momentum;
        }

        private decimal GetOpenVolume(BotTabSimple tab)
        {
            decimal volume = 0m;
            List<Position> positions = tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].OpenVolume != 0)
                {
                    volume += positions[i].OpenVolume;
                }
            }

            return volume;
        }

        private decimal CalculateVolumeForMoney(BotTabSimple tab, decimal money)
        {
            if (tab == null || tab.Security == null || tab.CandlesAll == null || tab.CandlesAll.Count == 0)
            {
                return 0m;
            }

            decimal price = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;

            if (price == 0)
            {
                return 0m;
            }

            decimal lot = tab.Security.Lot;

            if (lot == 0)
            {
                lot = 1m;
            }

            decimal volume = money / (price * lot);

            int decimals = tab.Security.DecimalsVolume;

            if (decimals < 0)
            {
                decimals = 0;
            }

            decimal multiplier = (decimal)Math.Pow(10, decimals);
            volume = Math.Floor(volume * multiplier) / multiplier;

            return volume;
        }

        #endregion
    }

    public class RebalancerPositionPackage
    {
        public List<BotTabSimple> TabsToEntry = new List<BotTabSimple>();

        public Side Direction;

        public decimal MoneyOnAllPositions;

        public decimal MoneyOnOnePosition;
    }
}
