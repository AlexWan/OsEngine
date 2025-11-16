/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.IO;
using System.Globalization;
using OsEngine.OsTrader.Grids;
using OsEngine.Language;

/* Description
Grid Trend screener. Adaptive by volatility. 
We turn on the grid on when forming a pattern of three growing / falling candles
Stop by lifetime and positions count
Additionally: Work days / Non-trading periods intraday
Trailing Up/Down is on
 */

namespace OsEngine.Robots.Grids
{
    [Bot("GridScreenerAdaptiveSoldiers")]
    public class GridScreenerAdaptiveSoldiers : BotPanel
    {
        #region Constructor, settings, service

        private StrategyParameterString _regime;
        private StrategyParameterInt _maxGridsCount;

        private StrategyParameterInt _daysVolatilityAdaptive;
        private StrategyParameterDecimal _heightSoldiersVolaPercent;
        private StrategyParameterDecimal _minHeightOneSoldiersVolaPercent;

        private StrategyParameterBool _smaFilterIsOn;
        private StrategyParameterInt _smaFilterLen;

        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        private StrategyParameterInt _linesCount;
        private StrategyParameterDecimal _linesStep;
        private StrategyParameterDecimal _profitValue;
        private StrategyParameterInt _closePositionsCountToCloseGrid;
        private StrategyParameterInt _gridSecondsToLife;

        private BotTabScreener _tabScreener;

        public GridScreenerAdaptiveSoldiers(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            _tabScreener.CandleFinishedEvent += _tab_CandleFinishedEvent1;

            if(startProgram == StartProgram.IsTester)
            {
                _tabScreener.TestStartEvent += _tabScreener_TestStartEvent;
            }

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" });

            _daysVolatilityAdaptive = CreateParameter("Days volatility adaptive", 3, 1, 20, 1);
            _heightSoldiersVolaPercent = CreateParameter("Height soldiers volatility percent", 50, 10, 70, 1m);
            _minHeightOneSoldiersVolaPercent = CreateParameter("Min height one soldier volatility percent", 10, 5, 20, 1m);

            _smaFilterIsOn = CreateParameter("Sma filter is on", true);
            _smaFilterLen = CreateParameter("Sma filter length", 100, 100, 300, 10);

            _maxGridsCount = CreateParameter("Max grids count", 5, 0, 20, 1, "Grid");
            _linesCount = CreateParameter("Grid lines count", 10, 10, 100, 10, "Grid");
            _linesStep = CreateParameter("Grid lines step", 0.05m, 0.05m, 1, 0.05m, "Grid");
            _profitValue = CreateParameter("Profit % ", 0.05m, 0.05m, 1, 0.05m, "Grid");
            _closePositionsCountToCloseGrid = CreateParameter("Grid close positions max", 50, 10, 300, 10, "Grid");
            _gridSecondsToLife = CreateParameter("Grid life time seconds", 172800, 172800, 2000000, 10000, "Grid");

            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Grid");
            _volume = CreateParameter("Volume on one line", 1, 1.0m, 50, 4, "Grid");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Grid");

            // non trade periods

            _nonTradePeriod1OnOff = CreateParameter("Block trade. Period " + "1", false, " Trade periods ");
            _nonTradePeriod1Start = CreateParameterTimeOfDay("Start period " + "1", 9, 0, 0, 0, " Trade periods ");
            _nonTradePeriod1End = CreateParameterTimeOfDay("End period " + "1", 10, 5, 0, 0, " Trade periods ");

            _nonTradePeriod2OnOff = CreateParameter("Block trade. Period " + "2", false, " Trade periods ");
            _nonTradePeriod2Start = CreateParameterTimeOfDay("Start period " + "2", 13, 55, 0, 0, " Trade periods ");
            _nonTradePeriod2End = CreateParameterTimeOfDay("End period " + "2", 14, 5, 0, 0, " Trade periods ");

            _nonTradePeriod3OnOff = CreateParameter("Block trade. Period " + "3", false, " Trade periods ");
            _nonTradePeriod3Start = CreateParameterTimeOfDay("Start period " + "3", 18, 40, 0, 0, " Trade periods ");
            _nonTradePeriod3End = CreateParameterTimeOfDay("End period " + "3", 19, 5, 0, 0, " Trade periods ");

            _nonTradePeriod4OnOff = CreateParameter("Block trade. Period " + "4", false, " Trade periods ");
            _nonTradePeriod4Start = CreateParameterTimeOfDay("Start period " + "4", 23, 40, 0, 0, " Trade periods ");
            _nonTradePeriod4End = CreateParameterTimeOfDay("End period " + "4", 23, 59, 0, 0, " Trade periods ");

            _nonTradePeriod5OnOff = CreateParameter("Block trade. Period " + "5", false, " Trade periods ");
            _nonTradePeriod5Start = CreateParameterTimeOfDay("Start period " + "5", 23, 40, 0, 0, " Trade periods ");
            _nonTradePeriod5End = CreateParameterTimeOfDay("End period " + "5", 23, 59, 0, 0, " Trade periods ");

            CreateParameterLabel("Empty string tp", "", "", 20, 20, System.Drawing.Color.Black, " Trade periods ");

            _tradeInMonday = CreateParameter("Trade in Monday. Is on", true, " Trade periods ");
            _tradeInTuesday = CreateParameter("Trade in Tuesday. Is on", true, " Trade periods ");
            _tradeInWednesday = CreateParameter("Trade in Wednesday. Is on", true, " Trade periods ");
            _tradeInThursday = CreateParameter("Trade in Thursday. Is on", true, " Trade periods ");
            _tradeInFriday = CreateParameter("Trade in Friday. Is on", true, " Trade periods ");
            _tradeInSaturday = CreateParameter("Trade in Saturday. Is on", true, " Trade periods ");
            _tradeInSunday = CreateParameter("Trade in Sunday. Is on", true, " Trade periods ");

            Description = OsLocalization.Description.DescriptionLabel39;

            if (startProgram == StartProgram.IsOsTrader)
            {
                LoadTradeSettings();
            }

            this.DeleteEvent += ThreeSoldierAdaptiveScreener_DeleteEvent;
        }

        public override string GetNameStrategyType()
        {
            return "GridScreenerAdaptiveSoldiers";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private void _tabScreener_TestStartEvent()
        {
            for (int i = 0; i < _tabScreener.Tabs.Count; i++)
            {
                BotTabSimple _tab = _tabScreener.Tabs[i];

                if (_tab.GridsMaster == null)
                {
                    continue;
                }

                for (int j = 0; j < _tab.GridsMaster.TradeGrids.Count; j++)
                {
                    TradeGrid grid = _tab.GridsMaster.TradeGrids[j];
                    _tab.GridsMaster.DeleteAtNum(grid.Number);
                    j--;
                }
            }
        }

        #endregion

        #region Volatility adaptation

        private List<SecuritiesTradeSettings> _tradeSettings = new List<SecuritiesTradeSettings>();

        private void SaveTradeSettings()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    for (int i = 0; i < _tradeSettings.Count; i++)
                    {
                        writer.WriteLine(_tradeSettings[i].GetSaveString());
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
                        _tradeSettings.Add(newSettings);
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void ThreeSoldierAdaptiveScreener_DeleteEvent()
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

        private void AdaptSoldiersHeight(List<Candle> candles, SecuritiesTradeSettings settings)
        {
            if (_daysVolatilityAdaptive.ValueInt <= 0
                || _heightSoldiersVolaPercent.ValueDecimal <= 0
                || _minHeightOneSoldiersVolaPercent.ValueDecimal <= 0)
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

                if (days >= _daysVolatilityAdaptive.ValueInt)
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

            decimal allSoldiersHeight = volaPercentSma * (_heightSoldiersVolaPercent.ValueDecimal / 100);
            decimal oneSoldiersHeight = volaPercentSma * (_minHeightOneSoldiersVolaPercent.ValueDecimal / 100);

            settings.HeightSoldiers = allSoldiersHeight;
            settings.MinHeightOneSoldier = oneSoldiersHeight;
            settings.LastUpdateTime = candles[candles.Count - 1].TimeStart;
        }

        #endregion

        #region Logic

        private void _tab_CandleFinishedEvent1(List<Candle> candles, BotTabSimple tab)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 150)
            {
                return;
            }

            SecuritiesTradeSettings mySettings = null;

            for (int i = 0; i < _tradeSettings.Count; i++)
            {
                if (_tradeSettings[i].SecName == tab.Security.Name &&
                    _tradeSettings[i].SecClass == tab.Security.NameClass)
                {
                    mySettings = _tradeSettings[i];
                    break;
                }
            }

            if (mySettings == null)
            {
                mySettings = new SecuritiesTradeSettings();
                mySettings.SecName = tab.Security.Name;
                mySettings.SecClass = tab.Security.NameClass;
                _tradeSettings.Add(mySettings);
            }

            if (mySettings.LastUpdateTime.Date != candles[candles.Count - 1].TimeStart.Date)
            {
                AdaptSoldiersHeight(candles, mySettings);

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    SaveTradeSettings();
                }
            }

            if (mySettings.HeightSoldiers == 0 ||
                mySettings.MinHeightOneSoldier == 0)
            {
                return;
            }

            if (tab.GridsMaster.TradeGrids.Count != 0)
            {
                LogicCloseGrid(candles, tab);
            }

            if (tab.GridsMaster.TradeGrids.Count == 0)
            {
                LogicCreateGrid(candles, tab, mySettings);
            }
        }

        private void LogicCreateGrid(List<Candle> candles, BotTabSimple tab, SecuritiesTradeSettings settings)
        {
            if (_tabScreener.SourceWithGridsCount >= _maxGridsCount.ValueInt)
            {
                return;
            }

            if (IsBlockNonTradePeriods(tab.TimeServerCurrent))
            {
                return;
            }

            decimal _lastPrice = candles[candles.Count - 1].Close;

            if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 1].Close)
                / (candles[candles.Count - 1].Close / 100) < settings.HeightSoldiers)
            {
                return;
            }
            if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 3].Close)
                / (candles[candles.Count - 3].Close / 100) < settings.MinHeightOneSoldier)
            {
                return;
            }
            if (Math.Abs(candles[candles.Count - 2].Open - candles[candles.Count - 2].Close)
                / (candles[candles.Count - 2].Close / 100) < settings.MinHeightOneSoldier)
            {
                return;
            }
            if (Math.Abs(candles[candles.Count - 1].Open - candles[candles.Count - 1].Close)
                / (candles[candles.Count - 1].Close / 100) < settings.MinHeightOneSoldier)
            {
                return;
            }

            //  long
            if (_regime.ValueString != "OnlyShort")
            {
                if (candles[candles.Count - 3].Open < candles[candles.Count - 3].Close
                    && candles[candles.Count - 2].Open < candles[candles.Count - 2].Close
                    && candles[candles.Count - 1].Open < candles[candles.Count - 1].Close)
                {
                    if (_smaFilterIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 1);
                        decimal smaPrev = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 2);

                        if (smaValue < smaPrev)
                        {
                            return;
                        }
                    }

                    decimal lastPrice = candles[^1].Close;
                    ThrowGrid(lastPrice, Side.Buy, tab);
                }
            }

            // Short
            if (_regime.ValueString != "OnlyLong")
            {
                if (candles[candles.Count - 3].Open > candles[candles.Count - 3].Close
                    && candles[candles.Count - 2].Open > candles[candles.Count - 2].Close
                    && candles[candles.Count - 1].Open > candles[candles.Count - 1].Close)
                {
                    if (_smaFilterIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 1);
                        decimal smaPrev = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 2);

                        if (smaValue > smaPrev)
                        {
                            return;
                        }
                    }

                    decimal lastPrice = candles[^1].Close;
                    ThrowGrid(lastPrice, Side.Sell, tab);
                }
            }
        }

        private void ThrowGrid(decimal lastPrice, Side side, BotTabSimple tab)
        {
            // 1 создаём сетку
            TradeGrid grid = tab.GridsMaster.CreateNewTradeGrid();

            // 2 устанавливаем её тип
            grid.GridType = TradeGridPrimeType.MarketMaking;

            // 3 устанавливаем объёмы
            grid.GridCreator.StartVolume = _volume.ValueDecimal;
            grid.GridCreator.TradeAssetInPortfolio = _tradeAssetInPortfolio.ValueString;
            if (_volumeType.ValueString == "Contracts")
            {
                grid.GridCreator.TypeVolume = TradeGridVolumeType.Contracts;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                grid.GridCreator.TypeVolume = TradeGridVolumeType.ContractCurrency;
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                grid.GridCreator.TypeVolume = TradeGridVolumeType.DepositPercent;
            }

            // 4 генерируем линии

            grid.GridCreator.FirstPrice = lastPrice;
            grid.GridCreator.LineCountStart = _linesCount.ValueInt;
            grid.GridCreator.LineStep = _linesStep.ValueDecimal;
            grid.GridCreator.ProfitStep = _profitValue.ValueDecimal;
            grid.GridCreator.TypeStep = TradeGridValueType.Percent;
            grid.GridCreator.TypeProfit = TradeGridValueType.Percent;
            grid.GridCreator.GridSide = side;
            grid.GridCreator.CreateNewGrid(tab, TradeGridPrimeType.MarketMaking);

            // 5 устанавливаем не торговые периоды
            CopyNonTradePeriodsSettingsInGrid(grid);

            // 6 устанавливаем Trailing Up

            grid.TrailingUp.TrailingUpStep = tab.RoundPrice(lastPrice * 0.005m, tab.Security, Side.Sell);
            grid.TrailingUp.TrailingUpLimit = lastPrice + lastPrice * 0.1m;
            grid.TrailingUp.TrailingUpIsOn = true;
            grid.TrailingUp.TrailingUpCanMoveExitOrder = false;

            // 7 устанавливаем Trailing Down

            grid.TrailingUp.TrailingDownStep = tab.RoundPrice(lastPrice * 0.005m, tab.Security, Side.Buy);
            grid.TrailingUp.TrailingDownLimit = lastPrice - lastPrice * 0.1m;
            grid.TrailingUp.TrailingDownIsOn = true;
            grid.TrailingUp.TrailingDownCanMoveExitOrder = false;

            // 8 устанавливаем закрытие сетки по количеству сделок

            grid.StopBy.StopGridByPositionsCountReaction = TradeGridRegime.CloseForced;
            grid.StopBy.StopGridByPositionsCountValue = _closePositionsCountToCloseGrid.ValueInt;
            grid.StopBy.StopGridByPositionsCountIsOn = true;

            // 9 устанавливаем закрытие сетки по времени жизни

            grid.StopBy.StopGridByLifeTimeIsOn = true; 
            grid.StopBy.StopGridByLifeTimeReaction = TradeGridRegime.CloseForced;
            grid.StopBy.StopGridByLifeTimeSecondsToLife = _gridSecondsToLife.ValueInt;

            // 10 сохраняем
            grid.Save();

            // 11 включаем
            grid.Regime = TradeGridRegime.On;
        }

        private void LogicCloseGrid(List<Candle> candles, BotTabSimple tab)
        {
            TradeGrid grid = tab.GridsMaster.TradeGrids[0];

            // 1 проверяем сетку на то что она уже прекратила работать и её надо удалить

            if (grid.HaveOpenPositionsByGrid == false
                && grid.Regime == TradeGridRegime.Off)
            { // Grid is stop work
                tab.GridsMaster.DeleteAtNum(grid.Number);
                return;
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

        #endregion

        #region Non trade periods

        private StrategyParameterBool _nonTradePeriod1OnOff;
        private StrategyParameterTimeOfDay _nonTradePeriod1Start;
        private StrategyParameterTimeOfDay _nonTradePeriod1End;

        private StrategyParameterBool _nonTradePeriod2OnOff;
        private StrategyParameterTimeOfDay _nonTradePeriod2Start;
        private StrategyParameterTimeOfDay _nonTradePeriod2End;

        private StrategyParameterBool _nonTradePeriod3OnOff;
        private StrategyParameterTimeOfDay _nonTradePeriod3Start;
        private StrategyParameterTimeOfDay _nonTradePeriod3End;

        private StrategyParameterBool _nonTradePeriod4OnOff;
        private StrategyParameterTimeOfDay _nonTradePeriod4Start;
        private StrategyParameterTimeOfDay _nonTradePeriod4End;

        private StrategyParameterBool _nonTradePeriod5OnOff;
        private StrategyParameterTimeOfDay _nonTradePeriod5Start;
        private StrategyParameterTimeOfDay _nonTradePeriod5End;

        private StrategyParameterBool _tradeInMonday;
        private StrategyParameterBool _tradeInTuesday;
        private StrategyParameterBool _tradeInWednesday;
        private StrategyParameterBool _tradeInThursday;
        private StrategyParameterBool _tradeInFriday;
        private StrategyParameterBool _tradeInSaturday;
        private StrategyParameterBool _tradeInSunday;

        private bool IsBlockNonTradePeriods(DateTime curTime)
        {
            if (_nonTradePeriod1OnOff.ValueBool == true)
            {
                if (_nonTradePeriod1Start.Value < curTime
                 && _nonTradePeriod1End.Value > curTime)
                {
                    return true;
                }

                if (_nonTradePeriod1Start.Value > _nonTradePeriod1End.Value)
                { // overnight transfer
                    if (_nonTradePeriod1Start.Value > curTime
                        || _nonTradePeriod1End.Value < curTime)
                    {
                        return true;
                    }
                }
            }

            if (_nonTradePeriod2OnOff.ValueBool == true)
            {
                if (_nonTradePeriod2Start.Value < curTime
                 && _nonTradePeriod2End.Value > curTime)
                {
                    return true;
                }

                if (_nonTradePeriod2Start.Value > _nonTradePeriod2End.Value)
                { // overnight transfer
                    if (_nonTradePeriod2Start.Value > curTime
                        || _nonTradePeriod2End.Value < curTime)
                    {
                        return true;
                    }
                }
            }

            if (_nonTradePeriod3OnOff.ValueBool == true)
            {
                if (_nonTradePeriod3Start.Value < curTime
                 && _nonTradePeriod3End.Value > curTime)
                {
                    return true;
                }

                if (_nonTradePeriod3Start.Value > _nonTradePeriod3End.Value)
                { // overnight transfer
                    if (_nonTradePeriod3Start.Value > curTime
                        || _nonTradePeriod3End.Value < curTime)
                    {
                        return true;
                    }
                }
            }

            if (_nonTradePeriod4OnOff.ValueBool == true)
            {
                if (_nonTradePeriod4Start.Value < curTime
                 && _nonTradePeriod4End.Value > curTime)
                {
                    return true;
                }

                if (_nonTradePeriod4Start.Value > _nonTradePeriod4End.Value)
                { // overnight transfer
                    if (_nonTradePeriod4Start.Value > curTime
                        || _nonTradePeriod4End.Value < curTime)
                    {
                        return true;
                    }
                }
            }

            if (_nonTradePeriod5OnOff.ValueBool == true)
            {
                if (_nonTradePeriod5Start.Value < curTime
                 && _nonTradePeriod5End.Value > curTime)
                {
                    return true;
                }

                if (_nonTradePeriod5Start.Value > _nonTradePeriod5End.Value)
                { // overnight transfer
                    if (_nonTradePeriod5Start.Value > curTime
                        || _nonTradePeriod5End.Value < curTime)
                    {
                        return true;
                    }
                }
            }

            if (_tradeInMonday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Monday)
            {
                return true;
            }

            if (_tradeInTuesday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Tuesday)
            {
                return true;
            }

            if (_tradeInWednesday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Wednesday)
            {
                return true;
            }

            if (_tradeInThursday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Thursday)
            {
                return true;
            }

            if (_tradeInFriday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Friday)
            {
                return true;
            }

            if (_tradeInSaturday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Saturday)
            {
                return true;
            }

            if (_tradeInSunday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Sunday)
            {
                return true;
            }

            return false;
        }

        private void CopyNonTradePeriodsSettingsInGrid(TradeGrid grid)
        {

            grid.NonTradePeriods.NonTradePeriod1Regime = TradeGridRegime.CloseForced;

            grid.NonTradePeriods.Settings.TradeInMonday = _tradeInMonday.ValueBool;
            grid.NonTradePeriods.Settings.TradeInTuesday = _tradeInTuesday.ValueBool;
            grid.NonTradePeriods.Settings.TradeInWednesday = _tradeInWednesday.ValueBool;
            grid.NonTradePeriods.Settings.TradeInThursday = _tradeInThursday.ValueBool;
            grid.NonTradePeriods.Settings.TradeInFriday = _tradeInFriday.ValueBool;
            grid.NonTradePeriods.Settings.TradeInSaturday = _tradeInSaturday.ValueBool;
            grid.NonTradePeriods.Settings.TradeInSunday = _tradeInSunday.ValueBool;


            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod1OnOff = _nonTradePeriod1OnOff.ValueBool;
            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod1Start = _nonTradePeriod1Start.Value;
            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod1End = _nonTradePeriod1End.Value;

            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod2OnOff = _nonTradePeriod2OnOff.ValueBool;
            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod2Start = _nonTradePeriod2Start.Value;
            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod2End = _nonTradePeriod2End.Value;

            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod3OnOff = _nonTradePeriod3OnOff.ValueBool;
            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod3Start = _nonTradePeriod3Start.Value;
            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod3End = _nonTradePeriod3End.Value;

            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod4OnOff = _nonTradePeriod4OnOff.ValueBool;
            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod4Start = _nonTradePeriod4Start.Value;
            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod4End = _nonTradePeriod4End.Value;

            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod5OnOff = _nonTradePeriod5OnOff.ValueBool;
            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod5Start = _nonTradePeriod5Start.Value;
            grid.NonTradePeriods.Settings.NonTradePeriodGeneral.NonTradePeriod5End = _nonTradePeriod5End.Value;
        }

        #endregion

    }

    public class SecuritiesTradeSettings
    {
        public string SecName;

        public string SecClass;

        public decimal HeightSoldiers;

        public decimal MinHeightOneSoldier;

        public DateTime LastUpdateTime;

        public string GetSaveString()
        {
            string result = "";

            result += SecName + "%";
            result += SecClass + "%";
            result += HeightSoldiers + "%";
            result += MinHeightOneSoldier + "%";
            result += LastUpdateTime.ToString(CultureInfo.InvariantCulture) + "%";

            return result;
        }

        public void LoadFromString(string str)
        {
            string[] array = str.Split('%');

            SecName = array[0];
            SecClass = array[1];
            HeightSoldiers = array[2].ToDecimal();
            MinHeightOneSoldier = array[3].ToDecimal();
            LastUpdateTime = Convert.ToDateTime(array[4], CultureInfo.InvariantCulture);
        }
    }
}