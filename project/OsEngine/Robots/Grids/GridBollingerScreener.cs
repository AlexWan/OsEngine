/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
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

            if (startProgram == StartProgram.IsTester)
            {
                _tabScreener.TestStartEvent += _tabScreener_TestStartEvent;
            }

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" });
            _bollingerLen = CreateParameter("Bollinger length", 50, 15, 20, 1);
            _bollingerDev = CreateParameter("Bollinger deviation", 1.5m, 0.7m, 2.5m, 0.1m);
            _adxFilterLength = CreateParameter("ADX filter length", 30, 10, 100, 3);
            _minAdxValue = CreateParameter("ADX min value", 10, 5, 90, 1m);
            _maxAdxValue = CreateParameter("ADX max value", 30, 20, 90, 1m);
           
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 0.5m, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            _maxGridsCount = CreateParameter("Max grids count", 5, 0, 20, 1, "Grid");
            _linesCount = CreateParameter("Grid lines count", 10, 10, 300, 10, "Grid");
            _linesStep = CreateParameter("Grid lines step", 0.1m, 0.1m, 5, 0.1m, "Grid");
            _profitValue = CreateParameter("Profit percent", 0.1m, 0.1m, 5, 0.1m, "Grid");
            _closePositionsCountToCloseGrid = CreateParameter("Grid close positions max", 50, 10, 300, 10, "Grid");

            _tabScreener.CreateCandleIndicator(1, "Bollinger", new List<string>() { "100", "2" }, "Prime");
            _tabScreener.CreateCandleIndicator(2, "ADX", new List<string>() { _adxFilterLength.ValueInt.ToString() }, "Second");

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

            this.ParametrsChangeByUser += GridBollingerScreener_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel35;
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

            if (tab.GridsMaster.TradeGrids.Count != 0)
            {
                LogicCloseGrid(candles, tab);
            }

            if (tab.GridsMaster.TradeGrids.Count == 0)
            {
                LogicCreateGrid(candles, tab);
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

            if(lastPrice > lastUpLine 
                && _regime.ValueString != "OnlyLong")
            {
                ThrowGrid(lastPrice, Side.Sell, tab);
            }
            if(lastPrice < lastDownLine
                && _regime.ValueString != "OnlyShort")
            {
                ThrowGrid(lastPrice, Side.Buy, tab);
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
            grid.GridCreator.TypeStep = TradeGridValueType.Percent;

            grid.GridCreator.TypeProfit = TradeGridValueType.Percent;
            grid.GridCreator.ProfitStep = _profitValue.ValueDecimal;

            grid.GridCreator.GridSide = side;

            grid.GridCreator.CreateNewGrid(tab, TradeGridPrimeType.MarketMaking);

            // 5 устанавливаем не торговые периоды на сетку

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

            // 9 сохраняем
            grid.Save();

            // 10 включаем
            grid.Regime = TradeGridRegime.On;
        }

        private void LogicCloseGrid(List<Candle> candles, BotTabSimple tab)
        {
            // 1 проверяем всё ли в порядке с индикатором

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

            // 2 проверяем сетку на то что она уже прекратила работать и её надо удалить

            if (grid.HaveOpenPositionsByGrid == false
                && grid.Regime == TradeGridRegime.Off)
            { // Grid is stop work
                tab.GridsMaster.DeleteAtNum(grid.Number);
                return;
            }

            if (grid.Regime != TradeGridRegime.On)
            {
                return;
            }

            // 3 проверяем сетку на обратную сторону канала. Может пора её закрывать

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
}