using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Linq;
using OsEngine.Logging;

/* Description
trading robot for osengine

The trend robot on Break Fractal, Parabolic And Stoh.

Buy:
1. The price is higher than the Parabolic value. For the next candle, the price crosses the indicator from the bottom up.
 2. Stochastic is directed up and below the 80 level.
 3. The price is higher than the last ascending fractal.

Sell:
 1. The price is lower than the Parabolic value. For the next candle, the price crosses the indicator from top to bottom.
 2. Stochastic is directed down and above the level of 20.
 3. the price is lower than the last descending fractal.

Exit: by the opposite signal of the parabolic.

 */


namespace OsEngine.Robots.AO
{
    [Bot("BreakFractalParabolicAndStoh")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakFractalParabolicAndStoh : BotPanel
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
        private StrategyParameterDecimal Step;
        private StrategyParameterDecimal MaxStep;
        private StrategyParameterInt StochPeriod1;
        private StrategyParameterInt StochPeriod2;
        private StrategyParameterInt StochPeriod3;

        // Indicator
        Aindicator _Parabolic;
        Aindicator _Fractal;
        Aindicator _Stoh;

        // The last value of the indicator
        private decimal _lastParabolic;
        private decimal _lastUpFract;
        private decimal _lastDownFract;
        private decimal _lastIndexDown;
        private decimal _lastIndexUp;
        private decimal _lastStoh;

        // The prev value of the indicator
        private decimal _prevStoh;

        public BreakFractalParabolicAndStoh(string name, StartProgram startProgram) : base(name, startProgram)
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
            Step = CreateParameter("Step", 0.1m, 0.01m, 0.1m, 0.01m, "Indicator");
            MaxStep = CreateParameter("Max Step", 0.1m, 0.01m, 0.1m, 0.01m, "Indicator");
            StochPeriod1 = CreateParameter("Stoch Period 1", 5, 3, 40, 1, "Indicator");
            StochPeriod2 = CreateParameter("Stoch Period 2", 3, 2, 40, 1, "Indicator");
            StochPeriod3 = CreateParameter("Stoch Period 3", 3, 2, 40, 1, "Indicator");

            // Create indicator Parabolic
            _Parabolic = IndicatorsFactory.CreateIndicatorByName("ParabolicSAR", name + "Par", false);
            _Parabolic = (Aindicator)_tab.CreateCandleIndicator(_Parabolic, "Prime");
            ((IndicatorParameterDecimal)_Parabolic.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_Parabolic.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _Parabolic.Save();

            // Create indicator Fractal
            _Fractal = IndicatorsFactory.CreateIndicatorByName("Fractal", name + "Fractal", false);
            _Fractal = (Aindicator)_tab.CreateCandleIndicator(_Fractal, "Prime");
            _Fractal.Save();

            // Create indicator Stoh
            _Stoh = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stoh", false);
            _Stoh = (Aindicator)_tab.CreateCandleIndicator(_Stoh, "NewArea0");
            ((IndicatorParameterInt)_Stoh.Parameters[0]).ValueInt = StochPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[1]).ValueInt = StochPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[2]).ValueInt = StochPeriod1.ValueInt;
            _Stoh.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakFractalParabolicAndStoh_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Break Fractal, Parabolic And Stoh. " +
                "Buy: " +
                "1. The price is higher than the Parabolic value. For the next candle, the price crosses the indicator from the bottom up. " +
                "2. Stochastic is directed up and below the 80 level. " +
                "3. The price is higher than the last ascending fractal. " +
                "Sell: " +
                "1. The price is lower than the Parabolic value. For the next candle, the price crosses the indicator from top to bottom. " +
                "2. Stochastic is directed down and above the level of 20. " +
                "3. the price is lower than the last descending fractal. " +
                "Exit: by the opposite signal of the parabolic.";
        }

        private void BreakFractalParabolicAndStoh_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_Parabolic.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_Parabolic.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _Parabolic.Save();
            _Parabolic.Reload();
            ((IndicatorParameterInt)_Stoh.Parameters[0]).ValueInt = StochPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[1]).ValueInt = StochPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[2]).ValueInt = StochPeriod1.ValueInt;
            _Stoh.Save();
            _Stoh.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakFractalParabolicAndStoh";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < StochPeriod1.ValueInt ||
                candles.Count < StochPeriod2.ValueInt ||
                candles.Count < StochPeriod3.ValueInt ||
                candles.Count < Step.ValueDecimal ||
                candles.Count < MaxStep.ValueDecimal)
            {
                return;
            }

            // If the time does not match, we leave
            if (StartTradeTime.Value > _tab.TimeServerCurrent ||
                EndTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            for (int i = _Fractal.DataSeries[1].Values.Count - 1; i > -1; i--)
            {
                if (_Fractal.DataSeries[1].Values[i] != 0)
                {
                    _lastUpFract = _Fractal.DataSeries[1].Values[i];
                    _lastIndexUp = i;
                    break;
                }
            }

            for (int i = _Fractal.DataSeries[0].Values.Count - 1; i > -1; i--)
            {
                if (_Fractal.DataSeries[0].Values[i] != 0)
                {
                    _lastDownFract = _Fractal.DataSeries[0].Values[i];
                    _lastIndexDown = i;
                    break;
                }
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
            _lastParabolic = _Parabolic.DataSeries[0].Last;
            _lastStoh = _Stoh.DataSeries[0].Last;

            // The prev value of the indicator
            _prevStoh = _Stoh.DataSeries[0].Values[_Stoh.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastParabolic < lastPrice && _prevStoh < _lastStoh && _lastStoh < 80 && _lastUpFract < lastPrice)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastParabolic > lastPrice && _prevStoh > _lastStoh && _lastStoh > 20 && _lastDownFract > lastPrice)
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
            Position pos = openPositions[0];

            // The last value of the indicator
            _lastParabolic = _Parabolic.DataSeries[0].Last;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastParabolic > lastPrice)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastParabolic < lastPrice)
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
