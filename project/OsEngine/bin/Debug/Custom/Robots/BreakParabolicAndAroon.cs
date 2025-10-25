/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Parabolic SAR and Aroon

Buy:
When the price is above the Parabolic SAR value and the Aroon Up line is above 50 and above the Aroon Down line.

Sell:
When the price is below the Parabolic SAR value and the Aroon Down line is above 50 and above the Aroon Up line.

Exit the position:
From buying when the price is below the Parabolic SAR value.
From sale when the price is higher than the Parabolic SAR value.
 */

namespace OsEngine.Robots
{
    [Bot("BreakParabolicAndAroon")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    internal class BreakParabolicAndAroon : BotPanel
    {
        // Reference to the main trading tab
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterDecimal _step;
        private StrategyParameterDecimal _maxStep;
        private StrategyParameterInt _aroonLength;

        // Indicators
        private Aindicator _pS;
        private Aindicator _aroon;

        public BreakParabolicAndAroon(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");

            // Indicator settings
            _step = CreateParameter("Step", 0.02m, 0.001m, 3, 0.001m, "Indicator");
            _maxStep = CreateParameter("MaxStep", 0.2m, 0.01m, 1, 0.01m, "Indicator");
            _aroonLength = CreateParameter("Aroon Length", 14, 1, 200, 1, "Indicator");

            // Create indicator Parabolic Sar
            _pS = IndicatorsFactory.CreateIndicatorByName("ParabolicSAR", name + "Parabolic", false);
            _pS = (Aindicator)_tab.CreateCandleIndicator(_pS, "Prime");
            ((IndicatorParameterDecimal)_pS.Parameters[0]).ValueDecimal = _step.ValueDecimal;
            ((IndicatorParameterDecimal)_pS.Parameters[1]).ValueDecimal = _maxStep.ValueDecimal;
            _pS.Save();

            // Create indicator Aroon
            _aroon = IndicatorsFactory.CreateIndicatorByName("Aroon", name + "Aroon", false);
            _aroon = (Aindicator)_tab.CreateCandleIndicator(_aroon, "AroonArea");
            ((IndicatorParameterInt)_aroon.Parameters[0]).ValueInt = _aroonLength.ValueInt;
            _aroon.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakPCAndAroon_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Successful position opening event
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

            Description = OsLocalization.Description.DescriptionLabel160;
        }

        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            _tab.SellAtStopCancel();
            _tab.BuyAtStopCancel();
        }

        private void BreakPCAndAroon_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_pS.Parameters[0]).ValueDecimal = _step.ValueDecimal;
            ((IndicatorParameterDecimal)_pS.Parameters[1]).ValueDecimal = _maxStep.ValueDecimal;
            _pS.Save();
            _pS.Reload();

            ((IndicatorParameterInt)_aroon.Parameters[0]).ValueInt = _aroonLength.ValueInt;
            _aroon.Save();
            _aroon.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakParabolicAndAroon";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are less than value _aroonLength, then we exit the method
            if (candles.Count < _aroonLength.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (_startTradeTime.Value > _tab.TimeServerCurrent ||
                _endTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            // If there are positions, then go to the position closing method
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles);
            }

            // If the position closing mode, then exit the method
            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            // If there are no positions, then go to the position opening method
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        // Opening position logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicator
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal lastSar = _pS.DataSeries[0].Last;
            decimal aroonUp = _aroon.DataSeries[0].Last;
            decimal aroonDown = _aroon.DataSeries[1].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (aroonUp > aroonDown && aroonUp > 50)
                    {
                        if (lastPrice > lastSar)
                        {
                            return;
                        }

                        decimal _slippage = this._slippage.ValueDecimal * lastSar / 100;

                        _tab.BuyAtStopCancel();
                        _tab.BuyAtStop(GetVolume(_tab), lastSar + _slippage, lastSar, StopActivateType.HigherOrEqual, 1);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (aroonDown > aroonUp && aroonDown > 50)
                    {
                        if (lastPrice < lastSar)
                        {
                            return;
                        }

                        decimal _slippage = this._slippage.ValueDecimal * lastSar / 100;

                        _tab.SellAtStopCancel();
                        _tab.SellAtStop(GetVolume(_tab), lastSar - _slippage, lastSar, StopActivateType.LowerOrEqyal, 1);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal lastSar = _pS.DataSeries[0].Last;

            for (int i = 0; i < openPositions.Count; i++)
            {
                _tab.SellAtStopCancel();
                _tab.BuyAtStopCancel();

                Position pos = openPositions[i];

                if (pos.CloseActiv == true && pos.CloseOrders != null && pos.CloseOrders.Count > 0)
                {
                    return;
                }

                decimal priceLine = lastSar;
                decimal priceOrder = lastSar;
                decimal _slippage = this._slippage.ValueDecimal * priceOrder / 100;

                if (pos.Direction == Side.Buy)
                {
                    _tab.CloseAtStop(pos, priceLine, priceOrder - _slippage);
                }
                else if (pos.Direction == Side.Sell)
                {
                    _tab.CloseAtStop(pos, priceLine, priceOrder + _slippage);
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                       && tab.Security.PriceStep != tab.Security.PriceStepCost
                       && tab.PriceBestAsk != 0
                       && tab.Security.PriceStep != 0
                       && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }
    }
}