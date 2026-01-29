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

/*
 
 Скринер выбрасывающий сетку на падениях акции, при растущем рынке.
 
 Логика точки выброса сетки:
 На все бумаги скринере ложится индикатор Bollinger
 Считаем ренкинг расположения ласт прайс относительно Bollinger. 
 И если 80% бумаг находятся по боллинджеру выше верхней линии.
 А наша бумага находится по боллинджеру ниже нижней(Т.е. падает когда остальной рынок летит вверх ракетой)
 В этот момент мы выбрасываем сетку Лонг. В режиме открытия позиции. С мартингейлом?
 */


namespace OsEngine.Robots.Grids
{
    [Bot("GridDumpBollingerScreener")]
    public class GridDumpBollingerScreener : BotPanel
    {
        private StrategyParameterString _regime;
        private StrategyParameterInt _maxGridsCount;
        private StrategyParameterInt _bollingerLen;
        private StrategyParameterDecimal _bollingerDev;

        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        private StrategyParameterInt _linesCount;
        private StrategyParameterDecimal _linesStep;
        private StrategyParameterDecimal _profitValue;
        private StrategyParameterInt _closePositionsCountToCloseGrid;

        private BotTabScreener _tabScreener;

        public GridDumpBollingerScreener(string name, StartProgram startProgram) : base(name, startProgram)
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

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On"});
            _bollingerLen = CreateParameter("Bollinger length", 50, 15, 20, 1);
            _bollingerDev = CreateParameter("Bollinger deviation", 1.5m, 0.7m, 2.5m, 0.1m);

            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 0.5m, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            _maxGridsCount = CreateParameter("Max grids count", 5, 0, 20, 1, "Grid");
            _linesCount = CreateParameter("Grid lines count", 10, 10, 300, 10, "Grid");
            _linesStep = CreateParameter("Grid lines step", 0.1m, 0.1m, 5, 0.1m, "Grid");
            _profitValue = CreateParameter("Profit percent", 0.1m, 0.1m, 5, 0.1m, "Grid");
            _closePositionsCountToCloseGrid = CreateParameter("Grid close positions max", 50, 10, 300, 10, "Grid");

            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            _tabScreener.CreateCandleIndicator(1, "Bollinger", new List<string>() { "100", "2" }, "Prime");

            this.ParametrsChangeByUser += GridBollingerScreener_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel35;
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        private void GridBollingerScreener_ParametrsChangeByUser()
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

            if (_tradePeriodsSettings.CanTradeThisTime(tab.TimeServerCurrent) == false)
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

            decimal lastUpLine = bollinger.DataSeries[0].Last;
            decimal lastDownLine = bollinger.DataSeries[1].Last;

            if (lastUpLine == 0
                || lastDownLine == 0)
            {
                return;
            }

            decimal lastPrice = candles[^1].Close;

            if (lastPrice < lastDownLine
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

        // Trade periods
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        private void CopyNonTradePeriodsSettingsInGrid(TradeGrid grid)
        {
            /*
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
            grid.NonTradePeriods.SettingsPeriod1.NonTradePeriodGeneral.NonTradePeriod5End = _nonTradePeriod5End.Value;*/

        }

        #endregion
    }
}