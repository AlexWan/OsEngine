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
Ejection of two grids in both directions at the same time.
Signal to start trading: Atr fell in M than it was N candles ago 
Signal to stop trading: Atr became higher than it was N candles ago
 */

namespace OsEngine.Robots.Grids
{
    [Bot("GridTwoSides")]
    public class GridTwoSides : BotPanel
    {
        private StrategyParameterString _regime;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        private StrategyParameterInt _atrLength;
        private StrategyParameterInt _atrLookBack;
        private StrategyParameterDecimal _atrMult;

        private StrategyParameterInt _linesCount;
        private StrategyParameterDecimal _linesStep;
        private StrategyParameterDecimal _profitValue;

        private Aindicator _atr; 

        private BotTabSimple _tab;

        public GridTwoSides(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.Connector.TestStartEvent += Connector_TestStartEvent;

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" }, "Base");

            _atrLength = CreateParameter("Atr length", 21, 7, 48, 7, "Base");
            _atrLookBack = CreateParameter("Atr lookBack", 20, 7, 48, 7, "Base");
            _atrMult = CreateParameter("Atr mult percent", 30m, 7, 48, 7, "Base");

            _startTradeTime = CreateParameterTimeOfDay("Start trade time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End trade time", 24, 0, 0, 0, "Base");

            _linesCount = CreateParameter("Grid lines count", 10, 10, 300, 10, "Grid");
            _linesStep = CreateParameter("Grid lines step", 0.05m, 10m, 300, 10, "Grid");
            _profitValue = CreateParameter("Profit percent", 0.05m, 1, 5, 0.1m, "Grid");

            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Grid");
            _volume = CreateParameter("Volume on one line", 1, 1.0m, 50, 4, "Grid");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Grid");

            _atr = IndicatorsFactory.CreateIndicatorByName("ATR", name + "ATR1", false);
            _atr = (Aindicator)_tab.CreateCandleIndicator(_atr, "Second");
            ((IndicatorParameterInt)_atr.Parameters[0]).ValueInt = _atrLength.ValueInt;
            _atr.Save();

            ParametrsChangeByUser += ParametersChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel40;
        }

        private void ParametersChangeByUser()
        {
            ((IndicatorParameterInt)_atr.Parameters[0]).ValueInt = _atrLength.ValueInt;
            _atr.Save();
            _atr.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "GridTwoSides";
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

            if (candles.Count - _atrLookBack.ValueInt < _atrLength.ValueInt)
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
                LogicCloseGrid(candles);
            }

            if (_tab.GridsMaster.TradeGrids.Count == 0)
            {
                LogicCreateGrid(candles);
            }
        }

        private void LogicCreateGrid(List<Candle> candles)
        {
            decimal lastAtr = _atr.DataSeries[0].Values[^1];

            decimal lookBackAtr = _atr.DataSeries[0].Values[^(1+_atrLookBack.ValueInt)];

            if (lastAtr == 0
                || lookBackAtr == 0)
            {
                return;
            }

            if (lastAtr + lastAtr * (_atrMult.ValueDecimal/100) < lookBackAtr)
            {
                decimal lastPrice = candles[^1].Close;

                ThrowGrid(lastPrice - _tab.Security.PriceStep, Side.Buy);
                ThrowGrid(lastPrice + _tab.Security.PriceStep, Side.Sell);
            }
        }

        private void ThrowGrid(decimal lastPrice, Side side)
        {
            // 1 создаём сетку
            TradeGrid grid = _tab.GridsMaster.CreateNewTradeGrid();

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
            grid.GridCreator.CreateNewGrid(_tab, TradeGridPrimeType.MarketMaking);

            // сохраняем
            grid.Save();

            // включаем
            grid.Regime = TradeGridRegime.On;
        }

        private void LogicDeleteGrid(List<Candle> candles)
        {
            for (int i = 0; i < _tab.GridsMaster.TradeGrids.Count; i++)
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

        private void LogicCloseGrid(List<Candle> candles)
        {
            if (_tab.GridsMaster.TradeGrids.Count == 0)
            {
                return;
            }

            TradeGrid grid = _tab.GridsMaster.TradeGrids[0];

            if(grid.Regime != TradeGridRegime.On)
            {
                return;
            }

            decimal lastAtr = _atr.DataSeries[0].Values[^1];

            decimal lookBackAtr = _atr.DataSeries[0].Values[^(1 + _atrLookBack.ValueInt)];

            if (lastAtr == 0
                || lookBackAtr == 0)
            {
                return;
            }

            if (lastAtr > lookBackAtr)
            {
                for(int i = 0;i < _tab.GridsMaster.TradeGrids.Count;i++)
                {
                    _tab.GridsMaster.TradeGrids[i].Regime = TradeGridRegime.CloseForced;
                }
            }
        }
    }
}