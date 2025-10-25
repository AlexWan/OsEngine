/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Strategy Ravi And Momentum.

Buy:When the Ravi indicator value is above the upper line and the Momentum indicator is above 100.

Sell:When the Ravi indicator value is below the lower line and the Momentum indicator is below 100.

Exit from buy: When the Ravi indicator value is below the lower line and the Momentum indicator is below 100.

Exit from sell: When the Ravi indicator value is above the upper line and the Momentum indicator is above 100.
 */

namespace OsEngine.Robots
{
    [Bot("StrategyRaviAndMomentum")] // We create an attribute so that we don't write anything to the BotFactory
    internal class StrategyRaviAndMomentum : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _lengthFastRavi;
        private StrategyParameterInt _lengthSlowRavi;
        private StrategyParameterDecimal _raviUpLine;
        private StrategyParameterDecimal _raviDownLine;
        private StrategyParameterInt _lengthMomentum;

        // Indicator
        private Aindicator _ravi;
        private Aindicator _momentum;

        public StrategyRaviAndMomentum(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _lengthFastRavi = CreateParameter("Ravi Fast Length", 10, 5, 150, 10, "Indicator");
            _lengthSlowRavi = CreateParameter("Ravi Slow Length", 50, 50, 200, 10, "Indicator");
            _raviUpLine = CreateParameter("Ravi Up Line", 3m, 1m, 3, 0.1m, "Indicator");
            _raviDownLine = CreateParameter("Ravi Down Line", 3m, 1m, 3, 0.1m, "Indicator");
            _lengthMomentum = CreateParameter("Momentum Length", 14, 10, 300, 10, "Indicator");

            // Create indicator Ravi
            _ravi = IndicatorsFactory.CreateIndicatorByName("RAVI", name + "Ravi", false);
            _ravi = (Aindicator)_tab.CreateCandleIndicator(_ravi, "RaviArea");
            ((IndicatorParameterInt)_ravi.Parameters[0]).ValueInt = _lengthSlowRavi.ValueInt;
            ((IndicatorParameterInt)_ravi.Parameters[1]).ValueInt = _lengthFastRavi.ValueInt;
            ((IndicatorParameterDecimal)_ravi.Parameters[2]).ValueDecimal = _raviUpLine.ValueDecimal;
            ((IndicatorParameterDecimal)_ravi.Parameters[3]).ValueDecimal = -_raviDownLine.ValueDecimal;
            _ravi.Save();

            // Create indicator Momentum
            _momentum = IndicatorsFactory.CreateIndicatorByName("Momentum", name + "Momentum", false);
            _momentum = (Aindicator)_tab.CreateCandleIndicator(_momentum, "MomentumArea");
            ((IndicatorParameterInt)_momentum.Parameters[0]).ValueInt = _lengthMomentum.ValueInt;
            _momentum.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyRavi_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel270;
        }

        private void StrategyRavi_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ravi.Parameters[0]).ValueInt = _lengthSlowRavi.ValueInt;
            ((IndicatorParameterInt)_ravi.Parameters[1]).ValueInt = _lengthFastRavi.ValueInt;
            ((IndicatorParameterDecimal)_ravi.Parameters[2]).ValueDecimal = _raviUpLine.ValueDecimal;
            ((IndicatorParameterDecimal)_ravi.Parameters[3]).ValueDecimal = -_raviDownLine.ValueDecimal;
            _ravi.Save();
            _ravi.Reload();
            ((IndicatorParameterInt)_momentum.Parameters[0]).ValueInt = _lengthMomentum.ValueInt;
            _momentum.Save();
            _momentum.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyRaviAndMomentum";
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

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _lengthSlowRavi.ValueInt ||
                candles.Count < _lengthMomentum.ValueInt)
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

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicator
            decimal lastRavi = _ravi.DataSeries[0].Last;
            decimal lastMomentum = _momentum.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastRavi > _raviUpLine.ValueDecimal && lastMomentum > 100)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastRavi < -_raviDownLine.ValueDecimal && lastMomentum < 100)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
           
            // The last value of the indicator
            decimal lastRavi = _ravi.DataSeries[0].Last;
            decimal lastMomentum = _momentum.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = _tab.Securiti.PriceStep * this._slippage.ValueDecimal / 100;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    if (lastRavi < -_raviDownLine.ValueDecimal && lastMomentum < 100)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (lastRavi > _raviUpLine.ValueDecimal && lastMomentum > 100)
                    {
                        _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
                    }
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