/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Grids;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using OsEngine.Language;

/*Description
Ejection of two grids in one direction
First buy signal: Breakdown of Price-Channel down
Second buy signal: There is the first grid + price returned to the center of the channel.
Output: By the number of closed lines
Output 2: By time in seconds
TrailingUp / Trailing Down. The permutation step is 20 minimum price steps.
*/

namespace OsEngine.Robots.Grids
{
    [Bot("GridTwoSignals")]
    public class GridTwoSignals : BotPanel
    {
        private StrategyParameterString _regime;
        private StrategyParameterInt _priceChannelLength;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        private StrategyParameterInt _linesCountGrid1;
        private StrategyParameterDecimal _linesStepGrid1;
        private StrategyParameterDecimal _profitValueGrid1;
        private StrategyParameterString _volumeTypeGrid1;
        private StrategyParameterDecimal _volumeGrid1;
        private StrategyParameterString _tradeAssetInPortfolioGrid1;
        private StrategyParameterInt _lifeTimeSecondsGrid1;
        private StrategyParameterInt _closePositionsCountToCloseGrid1;

        private StrategyParameterInt _linesCountGrid2;
        private StrategyParameterDecimal _linesStepGrid2;
        private StrategyParameterDecimal _profitValueGrid2;
        private StrategyParameterString _volumeTypeGrid2;
        private StrategyParameterDecimal _volumeGrid2;
        private StrategyParameterString _tradeAssetInPortfolioGrid2;
        private StrategyParameterInt _lifeTimeSecondsGrid2;
        private StrategyParameterInt _closePositionsCountToCloseGrid2;

        private Aindicator _priceChannel;

        private BotTabSimple _tab;

        public GridTwoSignals(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.Connector.TestStartEvent += Connector_TestStartEvent;

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" }, "Base");
            _priceChannelLength = CreateParameter("Price channel length", 21, 7, 48, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start trade time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End trade time", 24, 0, 0, 0, "Base");

            _linesCountGrid1 = CreateParameter("Grid lines count", 10, 10, 300, 10, "Grid 1");
            _linesStepGrid1 = CreateParameter("Grid lines step", 0.05m, 1, 5, 0.1m, "Grid 1");
            _profitValueGrid1 = CreateParameter("Profit percent", 0.05m, 1, 5, 0.1m, "Grid 1");
            _volumeTypeGrid1 = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Grid 1");
            _volumeGrid1 = CreateParameter("Volume on one line", 1, 1.0m, 50, 4, "Grid 1");
            _tradeAssetInPortfolioGrid1 = CreateParameter("Asset in portfolio", "Prime", "Grid 1");
            _lifeTimeSecondsGrid1 = CreateParameter("Grid life time seconds", 1200, 60, 30000, 60, "Grid 1");
            _closePositionsCountToCloseGrid1 = CreateParameter("Grid close positions max", 50, 10, 300, 10, "Grid 1");

            _linesCountGrid2 = CreateParameter("Grid lines count 2", 10, 10, 300, 10, "Grid 2");
            _linesStepGrid2 = CreateParameter("Grid lines step 2", 0.05m, 1, 5, 0.1m, "Grid 2");
            _profitValueGrid2 = CreateParameter("Profit percent 2", 0.05m, 1, 5, 0.1m, "Grid 2");
            _volumeTypeGrid2 = CreateParameter("Volume type 2", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Grid 2");
            _volumeGrid2 = CreateParameter("Volume on one line 2", 1, 1.0m, 50, 4, "Grid 2");
            _tradeAssetInPortfolioGrid2 = CreateParameter("Asset in portfolio 2", "Prime", "Grid 2");
            _lifeTimeSecondsGrid2 = CreateParameter("Grid life time seconds 2", 1200, 60, 30000, 60, "Grid 2");
            _closePositionsCountToCloseGrid2 = CreateParameter("Grid close positions max 2", 50, 10, 300, 10, "Grid 2");

            // Create indicator Bollinger
            _priceChannel = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _priceChannel = (Aindicator)_tab.CreateCandleIndicator(_priceChannel, "Prime");
            ((IndicatorParameterInt)_priceChannel.Parameters[0]).ValueInt = _priceChannelLength.ValueInt;
            ((IndicatorParameterInt)_priceChannel.Parameters[1]).ValueInt = _priceChannelLength.ValueInt;

            _priceChannel.Save();

            ParametrsChangeByUser += ParametersChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel41;
        }

        private void ParametersChangeByUser()
        {
            ((IndicatorParameterInt)_priceChannel.Parameters[0]).ValueInt = _priceChannelLength.ValueInt;
            ((IndicatorParameterInt)_priceChannel.Parameters[1]).ValueInt = _priceChannelLength.ValueInt;
            _priceChannel.Save();
            _priceChannel.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "GridTwoSignals";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private void Connector_TestStartEvent()
        {
            if(_tab.GridsMaster == null)
            {
                return;
            }
            for (int i = 0; i < _tab.GridsMaster.TradeGrids.Count; i++)
            {
                TradeGrid grid = _tab.GridsMaster.TradeGrids[i];
                _tab.GridsMaster.DeleteAtNum(grid.Number);
                i--;
            }
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < _priceChannelLength.ValueInt)
            {
                return;
            }

            if (_startTradeTime.Value > _tab.TimeServerCurrent ||
                _endTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            if (_tab.GridsMaster.TradeGrids.Count != 0)
            {
                LogicDeleteGrid(candles);
            }

            if (_tab.GridsMaster.TradeGrids.Count == 0
                || _tab.GridsMaster.TradeGrids.Count == 1)
            {
                LogicCreateGrid(candles);
            }
        }

        private void LogicCreateGrid(List<Candle> candles)
        {
            decimal lastUpLine = _priceChannel.DataSeries[0].Values[^2];
            decimal lastDownLine = _priceChannel.DataSeries[1].Values[^2];

            if (lastUpLine == 0
                || lastDownLine == 0)
            {
                return;
            }

            decimal lastPrice = candles[^1].Close;

            if (_tab.GridsMaster.TradeGrids.Count == 0
                && lastPrice < lastDownLine)
            {
                ThrowGridOne(lastPrice);
            }
            else if(_tab.GridsMaster.TradeGrids.Count == 1
                && _tab.GridsMaster.TradeGrids[0].Number == 1
                && lastPrice > (lastDownLine + lastUpLine) / 2)
            {
                ThrowGridTwo(lastPrice);
            }
        }

        private void ThrowGridOne(decimal lastPrice)
        {
            // 1 создаём сетку
            TradeGrid grid = _tab.GridsMaster.CreateNewTradeGrid();

            // 2 устанавливаем её тип
            grid.GridType = TradeGridPrimeType.MarketMaking;

            // 3 устанавливаем объёмы
            grid.GridCreator.StartVolume = _volumeGrid1.ValueDecimal;
            grid.GridCreator.TradeAssetInPortfolio = _tradeAssetInPortfolioGrid1.ValueString;
            if (_volumeTypeGrid1.ValueString == "Contracts")
            {
                grid.GridCreator.TypeVolume = TradeGridVolumeType.Contracts;
            }
            else if (_volumeTypeGrid1.ValueString == "Contract currency")
            {
                grid.GridCreator.TypeVolume = TradeGridVolumeType.ContractCurrency;
            }
            else if (_volumeTypeGrid1.ValueString == "Deposit percent")
            {
                grid.GridCreator.TypeVolume = TradeGridVolumeType.DepositPercent;
            }

            // 4 генерируем линии

            grid.GridCreator.FirstPrice = lastPrice;
            grid.GridCreator.LineCountStart = _linesCountGrid1.ValueInt;
            grid.GridCreator.LineStep = _linesStepGrid1.ValueDecimal;
            grid.GridCreator.TypeStep = TradeGridValueType.Percent;
            grid.GridCreator.TypeProfit = TradeGridValueType.Percent;
            grid.GridCreator.ProfitStep = _profitValueGrid1.ValueDecimal;
            grid.GridCreator.GridSide = Side.Buy;
            grid.GridCreator.CreateNewGrid(_tab, TradeGridPrimeType.MarketMaking);

            // 5 устанавливаем Trailing Up

            grid.TrailingUp.TrailingUpStep = _tab.RoundPrice(lastPrice * 0.005m, _tab.Security, Side.Buy);
            grid.TrailingUp.TrailingUpLimit = lastPrice + lastPrice * 0.1m;
            grid.TrailingUp.TrailingUpIsOn = true;
            grid.TrailingUp.TrailingUpCanMoveExitOrder = false;

            // 6 устанавливаем Trailing Down

            grid.TrailingUp.TrailingDownStep = _tab.RoundPrice(lastPrice * 0.005m, _tab.Security, Side.Sell);
            grid.TrailingUp.TrailingDownLimit = lastPrice - lastPrice * 0.1m;
            grid.TrailingUp.TrailingDownIsOn = true;
            grid.TrailingUp.TrailingDownCanMoveExitOrder = false;

            // 7 устанавливаем закрытие сетки по времени

            grid.StopBy.StopGridByLifeTimeReaction = TradeGridRegime.CloseForced;
            grid.StopBy.StopGridByLifeTimeSecondsToLife = _lifeTimeSecondsGrid1.ValueInt;
            grid.StopBy.StopGridByLifeTimeIsOn = true;

            // 8 устанавливаем закрытие сетки по количеству сделок

            grid.StopBy.StopGridByPositionsCountReaction = TradeGridRegime.CloseForced;
            grid.StopBy.StopGridByPositionsCountValue = _closePositionsCountToCloseGrid1.ValueInt;
            grid.StopBy.StopGridByPositionsCountIsOn = true;

            // 9 сохраняем
            grid.Save();

            // 10 включаем
            grid.Regime = TradeGridRegime.On;
        }

        private void ThrowGridTwo(decimal lastPrice)
        {
            // 1 создаём сетку
            TradeGrid grid = _tab.GridsMaster.CreateNewTradeGrid();

            // 2 устанавливаем её тип
            grid.GridType = TradeGridPrimeType.MarketMaking;

            // 3 устанавливаем объёмы
            grid.GridCreator.StartVolume = _volumeGrid2.ValueDecimal;
            grid.GridCreator.TradeAssetInPortfolio = _tradeAssetInPortfolioGrid2.ValueString;
            if (_volumeTypeGrid2.ValueString == "Contracts")
            {
                grid.GridCreator.TypeVolume = TradeGridVolumeType.Contracts;
            }
            else if (_volumeTypeGrid2.ValueString == "Contract currency")
            {
                grid.GridCreator.TypeVolume = TradeGridVolumeType.ContractCurrency;
            }
            else if (_volumeTypeGrid2.ValueString == "Deposit percent")
            {
                grid.GridCreator.TypeVolume = TradeGridVolumeType.DepositPercent;
            }

            // 4 генерируем линии

            grid.GridCreator.FirstPrice = lastPrice;
            grid.GridCreator.LineCountStart = _linesCountGrid2.ValueInt;
            grid.GridCreator.LineStep = _linesStepGrid2.ValueDecimal;
            grid.GridCreator.TypeStep = TradeGridValueType.Percent;
            grid.GridCreator.TypeProfit = TradeGridValueType.Percent;
            grid.GridCreator.ProfitStep = _profitValueGrid2.ValueDecimal;
            grid.GridCreator.GridSide = Side.Buy;
            grid.GridCreator.CreateNewGrid(_tab, TradeGridPrimeType.MarketMaking);

            // 5 устанавливаем Trailing Up

            grid.TrailingUp.TrailingUpStep = _tab.RoundPrice(lastPrice * 0.005m, _tab.Security, Side.Buy);
            grid.TrailingUp.TrailingUpLimit = lastPrice + lastPrice * 0.1m;
            grid.TrailingUp.TrailingUpIsOn = true;
            grid.TrailingUp.TrailingUpCanMoveExitOrder = false;

            // 6 устанавливаем Trailing Down

            grid.TrailingUp.TrailingDownStep = _tab.RoundPrice(lastPrice * 0.005m, _tab.Security, Side.Sell);
            grid.TrailingUp.TrailingDownLimit = lastPrice - lastPrice * 0.1m;
            grid.TrailingUp.TrailingDownIsOn = true;
            grid.TrailingUp.TrailingDownCanMoveExitOrder = false;

            // 7 устанавливаем закрытие сетки по времени

            grid.StopBy.StopGridByLifeTimeReaction = TradeGridRegime.CloseForced;
            grid.StopBy.StopGridByLifeTimeSecondsToLife = _lifeTimeSecondsGrid2.ValueInt;
            grid.StopBy.StopGridByLifeTimeIsOn = true;

            // 8 устанавливаем закрытие сетки по количеству сделок

            grid.StopBy.StopGridByPositionsCountReaction = TradeGridRegime.CloseForced;
            grid.StopBy.StopGridByPositionsCountValue = _closePositionsCountToCloseGrid2.ValueInt;
            grid.StopBy.StopGridByPositionsCountIsOn = true;

            // сохраняем
            grid.Save();

            // включаем
            grid.Regime = TradeGridRegime.On;
        }

        private void LogicDeleteGrid(List<Candle> candles)
        {
            for(int i = 0;i < _tab.GridsMaster.TradeGrids.Count;i++)
            {
                TradeGrid grid = _tab.GridsMaster.TradeGrids[i];

                // проверяем сетку на то что она уже прекратила работать и её надо удалить

                if (grid.HaveOpenPositionsByGrid == false
                    && grid.Regime == TradeGridRegime.Off)
                { // Grid is stop work
                    _tab.GridsMaster.DeleteAtNum(grid.Number);
                    i--;
                }
            }
        }
    }
}
