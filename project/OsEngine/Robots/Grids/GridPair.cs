/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

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
            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 1 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 58 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

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

            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            this.ParametrsChangeByUser += GridPair_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel38;
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
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

            if (_tradePeriodsSettings.CanTradeThisTime(tab1.TimeServerCurrent) == false)
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

        // Trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        private void CopyNonTradePeriodsSettingsInGrid(TradeGrid grid)
        {
            grid.NonTradePeriods.SettingsPeriod1.CopySettings(_tradePeriodsSettings);
        }

        #endregion
    }
}