/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Grids;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Grid counter-trend screener. Bollinger and ADX. 
We turn on the grid on reduced volatility and breakdown of the level.
Volatility is viewed by ADX
Additionally: Work days / Non-trading periods intraday
 */

namespace OsEngine.Robots.Grids
{
    [Bot("GridBollingerScreener")]
    public class GridBollingerScreener : BotPanel
    {
        private StrategyParameterString _regime;
        private StrategyParameterInt _maxGridsCount;
        private StrategyParameterInt _bollingerLen;
        private StrategyParameterDecimal _bollingerDev;

        public StrategyParameterInt _adxFilterLength;
        public StrategyParameterDecimal _minAdxValue;
        public StrategyParameterDecimal _maxAdxValue;

        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        private StrategyParameterInt _linesCount;
        private StrategyParameterDecimal _linesStep;
        private StrategyParameterDecimal _profitValue;
        private StrategyParameterInt _closePositionsCountToCloseGrid;

        private BotTabScreener _tabScreener;

        public GridBollingerScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);

            _tabScreener = TabsScreener[0];
            _tabScreener.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;


            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" });
            _bollingerLen = CreateParameter("Bollinger length", 50, 0, 20, 1);
            _bollingerDev = CreateParameter("Bollinger deviation", 2m, 0, 20, 1m);

            _adxFilterLength = CreateParameter("ADX filter Len", 30, 10, 100, 3);
            _minAdxValue = CreateParameter("ADX min value", 10, 20, 90, 1m);
            _maxAdxValue = CreateParameter("ADX max value", 20, 20, 90, 1m);
            _closePositionsCountToCloseGrid = CreateParameter("Grid close positions max", 50, 10, 300, 10);

            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 0.5m, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            _maxGridsCount = CreateParameter("Max grids count", 5, 0, 20, 1, "Grid");
            _linesCount = CreateParameter("Grid lines count", 10, 10, 300, 10, "Grid");
            _linesStep = CreateParameter("Grid lines step", 0.1m, 10m, 300, 10, "Grid");
            _profitValue = CreateParameter("Profit percent", 0.1m, 1, 5, 0.1m, "Grid");

            _tabScreener.CreateCandleIndicator(1, "Bollinger", new List<string>() { "100", "2" }, "Prime");
            _tabScreener.CreateCandleIndicator(2, "ADX", new List<string>() { _adxFilterLength.ValueInt.ToString() }, "Second");

            // non trade periods

            NonTradePeriod1OnOff = CreateParameter("Block trade. Period " + "1", false, " Trade periods ");
            NonTradePeriod1Start = CreateParameterTimeOfDay("Start period " + "1", 9, 0, 0, 0, " Trade periods ");
            NonTradePeriod1End = CreateParameterTimeOfDay("End period " + "1", 10, 5, 0, 0, " Trade periods ");

            NonTradePeriod2OnOff = CreateParameter("Block trade. Period " + "2", false, " Trade periods ");
            NonTradePeriod2Start = CreateParameterTimeOfDay("Start period " + "2", 13, 55, 0, 0, " Trade periods ");
            NonTradePeriod2End = CreateParameterTimeOfDay("End period " + "2", 14, 5, 0, 0, " Trade periods ");

            NonTradePeriod3OnOff = CreateParameter("Block trade. Period " + "3", false, " Trade periods ");
            NonTradePeriod3Start = CreateParameterTimeOfDay("Start period " + "3", 18, 40, 0, 0, " Trade periods ");
            NonTradePeriod3End = CreateParameterTimeOfDay("End period " + "3", 19, 5, 0, 0, " Trade periods ");

            NonTradePeriod4OnOff = CreateParameter("Block trade. Period " + "4", false, " Trade periods ");
            NonTradePeriod4Start = CreateParameterTimeOfDay("Start period " + "4", 23, 40, 0, 0, " Trade periods ");
            NonTradePeriod4End = CreateParameterTimeOfDay("End period " + "4", 23, 59, 0, 0, " Trade periods ");

            NonTradePeriod5OnOff = CreateParameter("Block trade. Period " + "5", false, " Trade periods ");
            NonTradePeriod5Start = CreateParameterTimeOfDay("Start period " + "5", 23, 40, 0, 0, " Trade periods ");
            NonTradePeriod5End = CreateParameterTimeOfDay("End period " + "5", 23, 59, 0, 0, " Trade periods ");

            CreateParameterLabel("Empty string tp", "", "", 20, 20, System.Drawing.Color.Black, " Trade periods ");

            TradeInMonday = CreateParameter("Trade in Monday. Is on", true, " Trade periods ");
            TradeInTuesday = CreateParameter("Trade in Tuesday. Is on", true, " Trade periods ");
            TradeInWednesday = CreateParameter("Trade in Wednesday. Is on", true, " Trade periods ");
            TradeInThursday = CreateParameter("Trade in Thursday. Is on", true, " Trade periods ");
            TradeInFriday = CreateParameter("Trade in Friday. Is on", true, " Trade periods ");
            TradeInSaturday = CreateParameter("Trade in Saturday. Is on", true, " Trade periods ");
            TradeInSunday = CreateParameter("Trade in Sunday. Is on", true, " Trade periods ");

            this.ParametrsChangeByUser += GridBollingerScreener_ParametrsChangeByUser;

            Description =
                "Grid counter-trend screener. Bollinger and Adx. "
              + "We turn on the grid on reduced volatility and breakdown of the level. "
              + "Volatility is viewed by Adx. "
              + "Additionally: Work days / Non-trading periods intraday.";
        }

        private void GridBollingerScreener_ParametrsChangeByUser()
        {
            for(int i = 0;i < _tabScreener.Tabs.Count;i++)
            {
                BotTabSimple tab = _tabScreener.Tabs[i];

                if(tab.GridsMaster.TradeGrids.Count > 0)
                {
                    TradeGrid grid = tab.GridsMaster.TradeGrids[0];
                    CopyNonTradePeriodsSettingsInGrid(grid);
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "GridBollingerScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic

        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 5)
            {
                return;
            }

            if (tab.GridsMaster.TradeGrids.Count == 0)
            {
                LogicCreateGrid(candles, tab);
            }
            else
            {
                LogicCloseGrid(candles, tab);
            }
        }

        private void LogicCreateGrid(List<Candle> candles, BotTabSimple tab)
        {
            if (_tabScreener.SourceWithGridsCount >= _maxGridsCount.ValueInt)
            {
                return;
            }

            if(IsBlockNonTradePeriods(tab.TimeServerCurrent))
            {
                return;
            }

            Aindicator bollinger = (Aindicator)tab.Indicators[0];

            if (bollinger.ParametersDigit[0].Value != _bollingerLen.ValueInt
                || bollinger.ParametersDigit[1].Value != _bollingerDev.ValueDecimal)
            {
                bollinger.ParametersDigit[0].Value = _bollingerLen.ValueInt;
                bollinger.ParametersDigit[1].Value = _bollingerDev.ValueDecimal;
                bollinger.Save();
                bollinger.Reload();
            }

            if (bollinger.DataSeries[0].Values.Count == 0 ||
                bollinger.DataSeries[0].Last == 0 ||
                bollinger.DataSeries[1].Values.Count == 0 ||
                bollinger.DataSeries[1].Last == 0)
            {
                return;
            }

            Aindicator adx = (Aindicator)tab.Indicators[1];

            if (adx.ParametersDigit[0].Value != _adxFilterLength.ValueInt)
            {
                adx.ParametersDigit[0].Value = _adxFilterLength.ValueInt;
                adx.Save();
                adx.Reload();
            }

            decimal adxLast = adx.DataSeries[0].Last;

            if (adxLast == 0)
            {
                return;
            }

            if (adxLast < _minAdxValue.ValueDecimal
                || adxLast > _maxAdxValue.ValueDecimal)
            {
                return;
            }

            decimal lastUpLine = bollinger.DataSeries[0].Last;
            decimal lastDownLine = bollinger.DataSeries[1].Last;

            if (lastUpLine == 0
                || lastDownLine == 0)
            {
                return;
            }

            decimal lastPrice = candles[^1].Close;

            bool upGrid = false;

            bool downGrid = false;

            if(lastPrice > lastUpLine 
                && _regime.ValueString != "OnlyLong")
            {
                downGrid = true;
            }
            if(lastPrice < lastDownLine
                && _regime.ValueString != "OnlyShort")
            {
                upGrid = true;
            }

            if (downGrid
                || upGrid)
            {
                TradeGrid grid = tab.GridsMaster.CreateNewTradeGrid();

                grid.GridType = TradeGridPrimeType.MarketMaking;

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

                grid.GridCreator.FirstPrice = lastPrice;
                grid.GridCreator.LineCountStart = _linesCount.ValueInt;
                grid.GridCreator.LineStep = _linesStep.ValueDecimal;
                grid.GridCreator.TypeStep = TradeGridValueType.Percent;

                grid.GridCreator.TypeProfit = TradeGridValueType.Percent;
                grid.GridCreator.ProfitStep = _profitValue.ValueDecimal;

                if (downGrid)
                {
                    grid.GridCreator.GridSide = Side.Sell;
                }
                else if (upGrid)
                {
                    grid.GridCreator.GridSide = Side.Buy;
                }
                grid.GridCreator.CreateNewGrid(tab, TradeGridPrimeType.MarketMaking);

                CopyNonTradePeriodsSettingsInGrid(grid);

                // устанавливаем Trailing Up

                grid.TrailingUp.TrailingUpStep = tab.Security.PriceStep * 20;
                grid.TrailingUp.TrailingUpLimit = lastPrice + lastPrice * 0.1m;
                grid.TrailingUp.TrailingUpIsOn = true;

                // устанавливаем Trailing Down

                grid.TrailingUp.TrailingDownStep = tab.Security.PriceStep * 20;
                grid.TrailingUp.TrailingDownLimit = lastPrice - lastPrice * 0.1m;
                grid.TrailingUp.TrailingDownIsOn = true;

                // устанавливаем закрытие сетки по количеству сделок

                grid.StopBy.StopGridByPositionsCountReaction = TradeGridRegime.CloseForced;
                grid.StopBy.StopGridByPositionsCountValue = _closePositionsCountToCloseGrid.ValueInt;
                grid.StopBy.StopGridByPositionsCountIsOn = true;

                grid.Save();
                grid.Regime = TradeGridRegime.On;
            }
        }

        private void LogicCloseGrid(List<Candle> candles, BotTabSimple tab)
        {
            Aindicator bollinger = (Aindicator)tab.Indicators[0];

            if (bollinger.ParametersDigit[0].Value != _bollingerLen.ValueInt
                || bollinger.ParametersDigit[1].Value != _bollingerDev.ValueDecimal)
            {
                bollinger.ParametersDigit[0].Value = _bollingerLen.ValueInt;
                bollinger.ParametersDigit[1].Value = _bollingerDev.ValueDecimal;
                bollinger.Save();
                bollinger.Reload();
            }

            if (bollinger.DataSeries[0].Values.Count == 0 ||
                bollinger.DataSeries[0].Last == 0 ||
                bollinger.DataSeries[1].Values.Count == 0 ||
                bollinger.DataSeries[1].Last == 0)
            {
                return;
            }

            TradeGrid grid = tab.GridsMaster.TradeGrids[0];

            // 1 проверяем сетку на то что она уже прекратила работать и её надо удалить

            if (grid.HaveOpenPositionsByGrid == false
                && grid.Regime == TradeGridRegime.Off)
            { // Grid is stop work
                tab.GridsMaster.DeleteAtNum(grid.Number);
                return;
            }

            if (grid.Regime == TradeGridRegime.CloseForced)
            {
                return;
            }

            // 2 проверяем сетку на обратную сторону канала. Может пора её закрывать

            decimal lastUpLine = bollinger.DataSeries[0].Last;
            decimal lastDownLine = bollinger.DataSeries[1].Last;

            if (lastUpLine == 0
                || lastDownLine == 0)
            {
                return;
            }

            decimal lastPrice = candles[^1].Close;

            Side gridSide = grid.GridCreator.GridSide;

            if (gridSide == Side.Buy
                && lastPrice > lastUpLine)
            {
                grid.Regime = TradeGridRegime.CloseForced;
            }
            else if (gridSide == Side.Sell
                && lastPrice < lastDownLine)
            {
                grid.Regime = TradeGridRegime.CloseForced;
            }
        }

        #region Non trade periods

        public StrategyParameterBool NonTradePeriod1OnOff;
        public StrategyParameterTimeOfDay NonTradePeriod1Start;
        public StrategyParameterTimeOfDay NonTradePeriod1End;

        public StrategyParameterBool NonTradePeriod2OnOff;
        public StrategyParameterTimeOfDay NonTradePeriod2Start;
        public StrategyParameterTimeOfDay NonTradePeriod2End;

        public StrategyParameterBool NonTradePeriod3OnOff;
        public StrategyParameterTimeOfDay NonTradePeriod3Start;
        public StrategyParameterTimeOfDay NonTradePeriod3End;

        public StrategyParameterBool NonTradePeriod4OnOff;
        public StrategyParameterTimeOfDay NonTradePeriod4Start;
        public StrategyParameterTimeOfDay NonTradePeriod4End;

        public StrategyParameterBool NonTradePeriod5OnOff;
        public StrategyParameterTimeOfDay NonTradePeriod5Start;
        public StrategyParameterTimeOfDay NonTradePeriod5End;

        public StrategyParameterBool TradeInMonday;
        public StrategyParameterBool TradeInTuesday;
        public StrategyParameterBool TradeInWednesday;
        public StrategyParameterBool TradeInThursday;
        public StrategyParameterBool TradeInFriday;
        public StrategyParameterBool TradeInSaturday;
        public StrategyParameterBool TradeInSunday;

        private bool IsBlockNonTradePeriods(DateTime curTime)
        {
            if (NonTradePeriod1OnOff.ValueBool == true)
            {
                if (NonTradePeriod1Start.Value < curTime
                 && NonTradePeriod1End.Value > curTime)
                {
                    return true;
                }

                if (NonTradePeriod1Start.Value > NonTradePeriod1End.Value)
                { // overnight transfer
                    if (NonTradePeriod1Start.Value > curTime
                        || NonTradePeriod1End.Value < curTime)
                    {
                        return true;
                    }
                }
            }

            if (NonTradePeriod2OnOff.ValueBool == true)
            {
                if (NonTradePeriod2Start.Value < curTime
                 && NonTradePeriod2End.Value > curTime)
                {
                    return true;
                }

                if (NonTradePeriod2Start.Value > NonTradePeriod2End.Value)
                { // overnight transfer
                    if (NonTradePeriod2Start.Value > curTime
                        || NonTradePeriod2End.Value < curTime)
                    {
                        return true;
                    }
                }
            }

            if (NonTradePeriod3OnOff.ValueBool == true)
            {
                if (NonTradePeriod3Start.Value < curTime
                 && NonTradePeriod3End.Value > curTime)
                {
                    return true;
                }

                if (NonTradePeriod3Start.Value > NonTradePeriod3End.Value)
                { // overnight transfer
                    if (NonTradePeriod3Start.Value > curTime
                        || NonTradePeriod3End.Value < curTime)
                    {
                        return true;
                    }
                }
            }

            if (NonTradePeriod4OnOff.ValueBool == true)
            {
                if (NonTradePeriod4Start.Value < curTime
                 && NonTradePeriod4End.Value > curTime)
                {
                    return true;
                }

                if (NonTradePeriod4Start.Value > NonTradePeriod4End.Value)
                { // overnight transfer
                    if (NonTradePeriod4Start.Value > curTime
                        || NonTradePeriod4End.Value < curTime)
                    {
                        return true;
                    }
                }
            }

            if (NonTradePeriod5OnOff.ValueBool == true)
            {
                if (NonTradePeriod5Start.Value < curTime
                 && NonTradePeriod5End.Value > curTime)
                {
                    return true;
                }

                if (NonTradePeriod5Start.Value > NonTradePeriod5End.Value)
                { // overnight transfer
                    if (NonTradePeriod5Start.Value > curTime
                        || NonTradePeriod5End.Value < curTime)
                    {
                        return true;
                    }
                }
            }

            if (TradeInMonday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Monday)
            {
                return true;
            }

            if (TradeInTuesday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Tuesday)
            {
                return true;
            }

            if (TradeInWednesday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Wednesday)
            {
                return true;
            }

            if (TradeInThursday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Thursday)
            {
                return true;
            }

            if (TradeInFriday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Friday)
            {
                return true;
            }

            if (TradeInSaturday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Saturday)
            {
                return true;
            }

            if (TradeInSunday.ValueBool == false
                && curTime.DayOfWeek == DayOfWeek.Sunday)
            {
                return true;
            }

            return false;
        }

        private void CopyNonTradePeriodsSettingsInGrid(TradeGrid grid)
        {
            grid.NonTradeDays.TradeInMonday = TradeInMonday.ValueBool;
            grid.NonTradeDays.TradeInTuesday = TradeInTuesday.ValueBool;
            grid.NonTradeDays.TradeInWednesday = TradeInWednesday.ValueBool;
            grid.NonTradeDays.TradeInThursday = TradeInThursday.ValueBool;
            grid.NonTradeDays.TradeInFriday = TradeInFriday.ValueBool;
            grid.NonTradeDays.TradeInSaturday = TradeInSaturday.ValueBool;
            grid.NonTradeDays.TradeInSunday = TradeInSunday.ValueBool;
            grid.NonTradeDays.NonTradeDaysRegime = TradeGridRegime.CloseForced;

            grid.NonTradePeriods.NonTradePeriod1OnOff = NonTradePeriod1OnOff.ValueBool;
            grid.NonTradePeriods.NonTradePeriod1Start = NonTradePeriod1Start.Value;
            grid.NonTradePeriods.NonTradePeriod1End = NonTradePeriod1End.Value;

            grid.NonTradePeriods.NonTradePeriod2OnOff = NonTradePeriod2OnOff.ValueBool;
            grid.NonTradePeriods.NonTradePeriod2Start = NonTradePeriod2Start.Value;
            grid.NonTradePeriods.NonTradePeriod2End = NonTradePeriod2End.Value;

            grid.NonTradePeriods.NonTradePeriod3OnOff = NonTradePeriod3OnOff.ValueBool;
            grid.NonTradePeriods.NonTradePeriod3Start = NonTradePeriod3Start.Value;
            grid.NonTradePeriods.NonTradePeriod3End = NonTradePeriod3End.Value;

            grid.NonTradePeriods.NonTradePeriod4OnOff = NonTradePeriod4OnOff.ValueBool;
            grid.NonTradePeriods.NonTradePeriod4Start = NonTradePeriod4Start.Value;
            grid.NonTradePeriods.NonTradePeriod4End = NonTradePeriod4End.Value;

            grid.NonTradePeriods.NonTradePeriod5OnOff = NonTradePeriod5OnOff.ValueBool;
            grid.NonTradePeriods.NonTradePeriod5Start = NonTradePeriod5Start.Value;
            grid.NonTradePeriods.NonTradePeriod5End = NonTradePeriod5End.Value;
        }

        #endregion
    }
}