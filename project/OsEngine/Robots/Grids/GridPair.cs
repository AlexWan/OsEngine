/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Grids;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Market maker's grid for trading in a pair.
A graph of minimum residuals from the difference of two price series with an optimal multiplier is calculated.
Extreme deviations of two securities from each other are traded.
 */

namespace OsEngine.Robots.Grids
{
    [Bot("GridPair")]
    public class GridPair : BotPanel
    {

        #region Constructor, settings, service

        private StrategyParameterString _regime;
        private StrategyParameterInt _deviationChartLen;

        public StrategyParameterDecimal _deviationToStartTrading;

        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        private StrategyParameterInt _linesCount;
        private StrategyParameterDecimal _linesStep;
        private StrategyParameterDecimal _profitValue;

        private BotTabScreener _tabScreener;

        public GridPair(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);

            _tabScreener = TabsScreener[0];
            _tabScreener.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

            if (startProgram == StartProgram.IsTester)
            {
                _tabScreener.TestStartEvent += _tabScreener_TestStartEvent;
            }

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });

            _deviationChartLen = CreateParameter("Deviation chart length", 150, 15, 200, 1);
            _deviationToStartTrading = CreateParameter("Standard deviation value", 1.5m, 1m, 3, 0.1m);

            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 0.5m, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            _linesCount = CreateParameter("Grid lines count", 10, 10, 300, 10, "Grid");
            _linesStep = CreateParameter("Grid lines step", 0.1m, 0.1m, 3, 0.01m, "Grid");
            _profitValue = CreateParameter("Profit percent", 0.1m, 0.1m, 3, 0.01m, "Grid");

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

            this.ParametrsChangeByUser += GridPair_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel38;
        }

        private void GridPair_ParametrsChangeByUser()
        {
            for (int i = 0; i < _tabScreener.Tabs.Count; i++)
            {
                BotTabSimple tab = _tabScreener.Tabs[i];

                if (tab.GridsMaster.TradeGrids.Count > 0)
                {
                    TradeGrid grid = tab.GridsMaster.TradeGrids[0];
                    CopyNonTradePeriodsSettingsInGrid(grid);
                }
            }
        }

        public override string GetNameStrategyType()
        {
            return "GridPair";
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

        #region Logic

        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if(_tabScreener.Tabs.Count < 2)
            {
                return;
            }

            BotTabSimple tab1 = _tabScreener.Tabs[0];
            BotTabSimple tab2 = _tabScreener.Tabs[1];

            List<Candle> candles1 = tab.CandlesFinishedOnly;
            List<Candle> candles2 = tab2.CandlesFinishedOnly;

            if(candles1 == null 
                || candles1.Count < 30
                || candles2 == null
                || candles2.Count < 30) 
            { 
                return; 
            }

            Candle lastCandle1 = candles1[^1];
            Candle lastCandle2 = candles2[^1];

            if(lastCandle1.TimeStart != lastCandle2.TimeStart)
            {
                return;
            }

            if (tab1.GridsMaster.TradeGrids.Count != 0
                ||
                tab2.GridsMaster.TradeGrids.Count != 0)
            {
                LogicDeleteGrid(candles, tab1);
                LogicDeleteGrid(candles, tab2);
                LogicStopGridTrading(candles1, candles2, tab1, tab2);
            }

            if (tab1.GridsMaster.TradeGrids.Count == 0
                &&
                tab2.GridsMaster.TradeGrids.Count == 0)
            {
                LogicCreateGrid(candles1, candles2, tab1, tab2);
            }
        }

        private void LogicCreateGrid(List<Candle> candles1, List<Candle> candles2, 
            BotTabSimple tab1, BotTabSimple tab2)
        {
            if (IsBlockNonTradePeriods(tab1.TimeServerCurrent))
            {
                return;
            }

            CointegrationBuilder cointegrationIndicator = new CointegrationBuilder();
            cointegrationIndicator.CointegrationLookBack = _deviationChartLen.ValueInt;
            cointegrationIndicator.CointegrationDeviation = _deviationToStartTrading.ValueDecimal;
            cointegrationIndicator.ReloadCointegration(candles1, candles2, false);

            if (cointegrationIndicator.Cointegration == null
                || cointegrationIndicator.Cointegration.Count == 0)
            {
                return;
            }

            if (cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Down)
            { // первая бумага внизу. Вторая бумага вверху
              // первое лонгуем. Второе шортим
                ThrowGrid(Side.Buy, tab1);
                ThrowGrid(Side.Sell, tab2);
            }
            if (cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Up)
            { // первое шортим. Второе лонгуем
                ThrowGrid(Side.Sell, tab1);
                ThrowGrid(Side.Buy, tab2);
            }
        }

        private void ThrowGrid(Side side, BotTabSimple tab)
        {
            decimal lastPrice = tab.CandlesAll[^1].Close;

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

            grid.TrailingUp.TrailingUpStep = tab.RoundPrice(lastPrice * 0.002m, tab.Security, Side.Buy);
            grid.TrailingUp.TrailingUpLimit = lastPrice + lastPrice * 0.25m;
            grid.TrailingUp.TrailingUpIsOn = true;
            grid.TrailingUp.TrailingUpCanMoveExitOrder = false;

            // 7 устанавливаем Trailing Down

            grid.TrailingUp.TrailingDownStep = tab.RoundPrice(lastPrice * 0.002m, tab.Security, Side.Sell);
            grid.TrailingUp.TrailingDownLimit = lastPrice - lastPrice * 0.25m;
            grid.TrailingUp.TrailingDownIsOn = true;
            grid.TrailingUp.TrailingDownCanMoveExitOrder = false;

            // 8 сохраняем
            grid.Save();

            // 9 включаем
            grid.Regime = TradeGridRegime.On;
        }

        private void LogicStopGridTrading(List<Candle> candles1, List<Candle> candles2,
BotTabSimple tab1, BotTabSimple tab2)
        {
            if (tab1.GridsMaster.TradeGrids == null || tab2.GridsMaster.TradeGrids == null)
            {
                return;
            }

            if (tab1.GridsMaster.TradeGrids.Count == 0
                || tab2.GridsMaster.TradeGrids.Count == 0)
            {
                return;
            }

            TradeGrid grid1 = tab1.GridsMaster.TradeGrids[0];
            TradeGrid grid2 = tab2.GridsMaster.TradeGrids[0];

            if (grid1.Regime != TradeGridRegime.On
                || grid2.Regime != TradeGridRegime.On)
            {
                return;
            }

            CointegrationBuilder cointegrationIndicator = new CointegrationBuilder();
            cointegrationIndicator.CointegrationLookBack = _deviationChartLen.ValueInt;
            cointegrationIndicator.CointegrationDeviation = _deviationToStartTrading.ValueDecimal;
            cointegrationIndicator.ReloadCointegration(candles1, candles2, false);

            if (cointegrationIndicator.Cointegration == null
                || cointegrationIndicator.Cointegration.Count == 0)
            {
                return;
            }

            if (cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Down
                && grid1.GridCreator.GridSide != Side.Buy)
            {
                grid1.Regime = TradeGridRegime.CloseForced;
                grid2.Regime = TradeGridRegime.CloseForced;
            }
            if (cointegrationIndicator.SideCointegrationValue == CointegrationLineSide.Up
                && grid1.GridCreator.GridSide != Side.Sell)
            {
                grid1.Regime = TradeGridRegime.CloseForced;
                grid2.Regime = TradeGridRegime.CloseForced;
            }
        }

        private void LogicDeleteGrid(List<Candle> candles, BotTabSimple tab)
        {
            if(tab.GridsMaster.TradeGrids.Count == 0)
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

            grid.NonTradePeriods.SettingsPeriod1.TradeInMonday = _tradeInMonday.ValueBool;
            grid.NonTradePeriods.SettingsPeriod1.TradeInTuesday = _tradeInTuesday.ValueBool;
            grid.NonTradePeriods.SettingsPeriod1.TradeInWednesday = _tradeInWednesday.ValueBool;
            grid.NonTradePeriods.SettingsPeriod1.TradeInThursday = _tradeInThursday.ValueBool;
            grid.NonTradePeriods.SettingsPeriod1.TradeInFriday = _tradeInFriday.ValueBool;
            grid.NonTradePeriods.SettingsPeriod1.TradeInSaturday = _tradeInSaturday.ValueBool;
            grid.NonTradePeriods.SettingsPeriod1.TradeInSunday = _tradeInSunday.ValueBool;


            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod1OnOff = _nonTradePeriod1OnOff.ValueBool;
            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod1Start = _nonTradePeriod1Start.Value;
            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod1End = _nonTradePeriod1End.Value;

            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod2OnOff = _nonTradePeriod2OnOff.ValueBool;
            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod2Start = _nonTradePeriod2Start.Value;
            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod2End = _nonTradePeriod2End.Value;

            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod3OnOff = _nonTradePeriod3OnOff.ValueBool;
            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod3Start = _nonTradePeriod3Start.Value;
            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod3End = _nonTradePeriod3End.Value;

            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod4OnOff = _nonTradePeriod4OnOff.ValueBool;
            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod4Start = _nonTradePeriod4Start.Value;
            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod4End = _nonTradePeriod4End.Value;

            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod5OnOff = _nonTradePeriod5OnOff.ValueBool;
            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod5Start = _nonTradePeriod5Start.Value;
            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod5End = _nonTradePeriod5End.Value;
        }

        #endregion
    }
}