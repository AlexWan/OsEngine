using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on Strategy Ravi And Momentum.

Buy:When the Ravi indicator value is above the upper line and the Momentum indicator is above 100.

Sell:When the Ravi indicator value is below the lower line and the Momentum indicator is below 100.

Exit from buy: When the Ravi indicator value is below the lower line and the Momentum indicator is below 100.

Exit from sell: When the Ravi indicator value is above the upper line and the Momentum indicator is above 100.
 */

namespace OsEngine.Robots.MyBots
{
    [Bot("StrategyRaviAndMomentum")] // We create an attribute so that we don't write anything to the BotFactory
    internal class StrategyRaviAndMomentum : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // Indicator setting 
        private StrategyParameterInt LengthFastRavi;
        private StrategyParameterInt LengthSlowRavi;
        private StrategyParameterDecimal RaviUpLine;
        private StrategyParameterDecimal RaviDownLine;
        private StrategyParameterInt LengthMomentum;

        // Indicator
        private Aindicator _Ravi;
        private Aindicator _Momentum;

        public StrategyRaviAndMomentum(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency" }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 1, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            StartTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            EndTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator setting
            LengthFastRavi = CreateParameter("Ravi Fast Length", 10, 5, 150, 10, "Indicator");
            LengthSlowRavi = CreateParameter("Ravi Slow Length", 50, 50, 200, 10, "Indicator");
            RaviUpLine = CreateParameter("Ravi Up Line", 3m, 1m, 3, 0.1m, "Indicator");
            RaviDownLine = CreateParameter("Ravi Down Line", 3m, 1m, 3, 0.1m, "Indicator");
            LengthMomentum = CreateParameter("Momentum Length", 14, 10, 300, 10, "Indicator");

            // Create indicator Ravi
            _Ravi = IndicatorsFactory.CreateIndicatorByName("RAVI", name + "Ravi", false);
            _Ravi = (Aindicator)_tab.CreateCandleIndicator(_Ravi, "RaviArea");
            ((IndicatorParameterInt)_Ravi.Parameters[0]).ValueInt = LengthSlowRavi.ValueInt;
            ((IndicatorParameterInt)_Ravi.Parameters[1]).ValueInt = LengthFastRavi.ValueInt;
            ((IndicatorParameterDecimal)_Ravi.Parameters[2]).ValueDecimal = RaviUpLine.ValueDecimal;
            ((IndicatorParameterDecimal)_Ravi.Parameters[3]).ValueDecimal = -RaviDownLine.ValueDecimal;
            _Ravi.Save();

            // Create indicator Momentum
            _Momentum = IndicatorsFactory.CreateIndicatorByName("Momentum", name + "Momentum", false);
            _Momentum = (Aindicator)_tab.CreateCandleIndicator(_Momentum, "MomentumArea");
            ((IndicatorParameterInt)_Momentum.Parameters[0]).ValueInt = LengthMomentum.ValueInt;
            _Momentum.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyRavi_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Ravi And Momentum. " +
                "Buy: When the Ravi indicator value is above the upper line and the Momentum indicator is above 100." +
                "Sell: When the Ravi indicator value is below the lower line and the Momentum indicator is below 100." +
                "Exit from buy: When the Ravi indicator value is below the lower line and the Momentum indicator is below 100. " +
                "Exit from sell: When the Ravi indicator value is above the upper line and the Momentum indicator is above 100.";
        }

        private void StrategyRavi_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Ravi.Parameters[0]).ValueInt = LengthSlowRavi.ValueInt;
            ((IndicatorParameterInt)_Ravi.Parameters[1]).ValueInt = LengthFastRavi.ValueInt;
            ((IndicatorParameterDecimal)_Ravi.Parameters[2]).ValueDecimal = RaviUpLine.ValueDecimal;
            ((IndicatorParameterDecimal)_Ravi.Parameters[3]).ValueDecimal = -RaviDownLine.ValueDecimal;
            _Ravi.Save();
            _Ravi.Reload();

            ((IndicatorParameterInt)_Momentum.Parameters[0]).ValueInt = LengthMomentum.ValueInt;
            _Momentum.Save();
            _Momentum.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyRaviAndMomentum";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }

        // Logic
        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < LengthSlowRavi.ValueInt ||
                candles.Count < LengthMomentum.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (StartTradeTime.Value > _tab.TimeServerCurrent ||
                EndTradeTime.Value < _tab.TimeServerCurrent)
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
            if (Regime.ValueString == "OnlyClosePosition")
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
            decimal lastRavi = _Ravi.DataSeries[0].Last;
            decimal lastMomentum = _Momentum.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastRavi > RaviUpLine.ValueDecimal && lastMomentum > 100)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastRavi < -RaviDownLine.ValueDecimal && lastMomentum < 100)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
           
            // The last value of the indicator
            decimal lastRavi = _Ravi.DataSeries[0].Last;
            decimal lastMomentum = _Momentum.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = _tab.Securiti.PriceStep * Slippage.ValueDecimal / 100;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (lastRavi < -RaviDownLine.ValueDecimal && lastMomentum < 100)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastRavi > RaviUpLine.ValueDecimal && lastMomentum > 100)
                    {
                        _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
                    }
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume()
        {
            decimal volume = 0;

            if (VolumeRegime.ValueString == "Contract currency")
            {
                decimal contractPrice = _tab.PriceBestAsk;
                volume = VolumeOnPosition.ValueDecimal / contractPrice;
            }
            else if (VolumeRegime.ValueString == "Number of contracts")
            {
                volume = VolumeOnPosition.ValueDecimal;
            }

            // If the robot is running in the tester
            if (StartProgram == StartProgram.IsTester)
            {
                volume = Math.Round(volume, 6);
            }
            else
            {
                volume = Math.Round(volume, _tab.Securiti.DecimalsVolume);
            }
            return volume;
        }
    }
}
