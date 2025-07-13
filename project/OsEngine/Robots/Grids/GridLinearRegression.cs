/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.OsTrader.Grids;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Robot showing the work with the grid. 
Throws a grid of the “Position Opening” type and closes the grid by a general trailing stop order. 
The linear regression indicator serves as a signal for grid throwing. 
When the candlestick closing price is higher than the upper channel line of the indicator - the grid is thrown out.
 */

namespace OsEngine.Robots.Grids
{
    [Bot("GridLinearRegression")]
    public class GridLinearRegression : BotPanel
    {
        private StrategyParameterString _regime; 
        private StrategyParameterDecimal _trailStopValuePercent;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        private StrategyParameterInt _lrLength;
        private StrategyParameterDecimal _lrDeviation;

        private StrategyParameterInt _linesCount;
        private StrategyParameterDecimal _linesStep;

        private Aindicator _linearRegression;

        private BotTabSimple _tab;

        public GridLinearRegression(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.Connector.TestStartEvent += Connector_TestStartEvent;

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" }, "Base");
           
            _startTradeTime = CreateParameterTimeOfDay("Start trade time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End trade time", 24, 0, 0, 0, "Base");
            _lrLength = CreateParameter("LR length", 10, 10, 300, 10, "Base");
            _lrDeviation = CreateParameter("LR deviation", 2.0m, 1, 5, 0.1m, "Base");

            _linesCount = CreateParameter("Grid lines count", 10, 10, 300, 10, "Grid");
            _linesStep = CreateParameter("Grid lines step percent", 0.1m, 10m, 300, 10, "Grid");
            _trailStopValuePercent = CreateParameter("Trail", 1.5m, 1, 5, 0.1m, "Grid");
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Grid");
            _volume = CreateParameter("Volume on one line", 1, 1.0m, 50, 4, "Grid");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Grid");


            // Create indicator LR
            _linearRegression = IndicatorsFactory.CreateIndicatorByName("LinearRegressionChannel", name + "LinearRegressionChannel", false);
            _linearRegression = (Aindicator)_tab.CreateCandleIndicator(_linearRegression, "Prime");
            ((IndicatorParameterInt)_linearRegression.Parameters[0]).ValueInt = _lrLength.ValueInt;
            ((IndicatorParameterDecimal)_linearRegression.Parameters[2]).ValueDecimal = _lrDeviation.ValueDecimal;
            ((IndicatorParameterDecimal)_linearRegression.Parameters[3]).ValueDecimal = _lrDeviation.ValueDecimal;
            _linearRegression.Save();

            ParametrsChangeByUser += ParametersChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel37;
        }

        private void ParametersChangeByUser()
        {
            ((IndicatorParameterInt)_linearRegression.Parameters[0]).ValueInt = _lrLength.ValueInt;
            ((IndicatorParameterDecimal)_linearRegression.Parameters[2]).ValueDecimal = _lrDeviation.ValueDecimal;
            ((IndicatorParameterDecimal)_linearRegression.Parameters[3]).ValueDecimal = _lrDeviation.ValueDecimal;
            _linearRegression.Save();
            _linearRegression.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "GridLinearRegression";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private void Connector_TestStartEvent()
        {
            if (_tab.GridsMaster == null)
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

            if (candles.Count < _lrLength.ValueInt)
            {
                return;
            }

            if (_startTradeTime.Value > _tab.TimeServerCurrent ||
                _endTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            if(_tab.GridsMaster.TradeGrids.Count == 0)
            {
                LogicCreateGrid(candles);
            }
            else
            {
                LogicCloseGrid();
            }
        }

        private void LogicCreateGrid(List<Candle> candles)
        {
            decimal lastLrUp = _linearRegression.DataSeries[0].Last;

            if (lastLrUp == 0)
            {
                return;
            }

            decimal lastPrice = candles[^1].Close;

            if (lastPrice > lastLrUp)
            {
                TradeGrid grid = _tab.GridsMaster.CreateNewTradeGrid();

                grid.GridType = TradeGridPrimeType.OpenPosition;

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
                grid.GridCreator.GridSide = Side.Buy;
                grid.GridCreator.CreateNewGrid(_tab,TradeGridPrimeType.OpenPosition);

                grid.StopAndProfit.TrailStopValue = _trailStopValuePercent.ValueDecimal;
                grid.StopAndProfit.TrailStopValueType = TradeGridValueType.Percent;
                grid.StopAndProfit.TrailStopRegime = OnOffRegime.On;
                grid.Save();

                grid.Regime = TradeGridRegime.On;

            }
        }

        private void LogicCloseGrid()
        {
            TradeGrid grid = _tab.GridsMaster.TradeGrids[0];

            // проверяем сетку на то что она уже прекратила работать и её надо удалить

            if(grid.HaveOpenPositionsByGrid == false 
                && grid.Regime == TradeGridRegime.Off)
            { // Grid is stop work
                _tab.GridsMaster.DeleteAtNum(grid.Number);
                return;
            }
        }
    }
}
