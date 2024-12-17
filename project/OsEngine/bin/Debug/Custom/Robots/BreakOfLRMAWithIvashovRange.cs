using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Trading robot for osengine.

The trend robot on Break of LRMAW ith Ivashov Range.

Buy:
The candle closed above LRMA + MultIvashov * Ivashov.

Sell:
The candle closed below IRMA - Cartoon ivashov * Ivashov.

Exit: 
On the return signal.

 */

namespace OsEngine.Robots.My_bots
{
    [Bot("BreakOfLRMAWithIvashovRange")]
    public class BreakOfLRMAWithIvashovRange : BotPanel
    
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // Setting indicator
        private StrategyParameterInt LengthLRMA;
        private StrategyParameterInt LengthMAIvashov;
        private StrategyParameterInt LengthRangeIvashov;
        private StrategyParameterDecimal MultIvashov;
       
        // Indicator
        private Aindicator _LRMA;
        private Aindicator _RangeIvashov;
       
        // The last value of the indicators
        private decimal _lastLRMA;
        private decimal _lastRangeIvashov;

        public BreakOfLRMAWithIvashovRange(string name, StartProgram startProgram) : base(name, startProgram)
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

            // Setting indicator 
            LengthLRMA = CreateParameter("Length LRMA", 20, 10, 200, 10, "Indicator");
            LengthMAIvashov = CreateParameter("Length MA Ivashov", 14, 7, 48, 7, "Indicator");
            LengthRangeIvashov = CreateParameter("Length Range Ivashov", 14, 7, 48, 7, "Indicator");
            MultIvashov = CreateParameter("Mult Ivashov", 0.5m, 0.1m, 2, 0.1m, "Indicator");

            // Create indicator LRMA
            _LRMA = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LRMA", false);
            _LRMA = (Aindicator)_tab.CreateCandleIndicator(_LRMA, "Prime");
            ((IndicatorParameterInt)_LRMA.Parameters[0]).ValueInt = LengthLRMA.ValueInt;
            _LRMA.Save();

            // Create indicator Ivashov
            _RangeIvashov = IndicatorsFactory.CreateIndicatorByName("IvashovRange", name + "Range Ivashov", false);
            _RangeIvashov = (Aindicator)_tab.CreateCandleIndicator(_RangeIvashov, "NewArea");
            ((IndicatorParameterInt)_RangeIvashov.Parameters[0]).ValueInt = LengthMAIvashov.ValueInt;
            ((IndicatorParameterInt)_RangeIvashov.Parameters[1]).ValueInt = LengthRangeIvashov.ValueInt;
            _RangeIvashov.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakOfLRMAWithIvashovRange_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Break of LRMAW ith Ivashov Range." +
                "Buy:" +
                "The candle closed above LRMA + MultIvashov * Ivashov." +
                "Sell:" +
                "The candle closed below IRMA - Cartoon ivashov* Ivashov." +
                "Exit:" +
                "On the return signal.";
        }

        // Indicator Update event
        private void BreakOfLRMAWithIvashovRange_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_LRMA.Parameters[0]).ValueInt = LengthLRMA.ValueInt;
            _LRMA.Save();
            _LRMA.Reload();

            ((IndicatorParameterInt)_RangeIvashov.Parameters[0]).ValueInt = LengthMAIvashov.ValueInt;
            ((IndicatorParameterInt)_RangeIvashov.Parameters[1]).ValueInt = LengthRangeIvashov.ValueInt;
            _RangeIvashov.Save();
            _RangeIvashov.Reload();

        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakOfLRMAWithIvashovRange";
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
            if (candles.Count < LengthLRMA.ValueInt || candles.Count < LengthRangeIvashov.ValueInt)
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
            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // The last value of the indicators
                _lastLRMA = _LRMA.DataSeries[0].Last;
                _lastRangeIvashov = _RangeIvashov.DataSeries[0].Last;

                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    decimal lastPrice = candles[candles.Count - 1].Close;
                   
                    if (lastPrice > _lastLRMA + MultIvashov.ValueDecimal * _lastRangeIvashov)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    decimal lastPrice = candles[candles.Count - 1].Close;
                    if (lastPrice < _lastLRMA - MultIvashov.ValueDecimal * _lastRangeIvashov)
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
            _lastLRMA = _LRMA.DataSeries[0].Last;
            _lastRangeIvashov = _RangeIvashov.DataSeries[0].Last;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (lastPrice < _lastLRMA - MultIvashov.ValueDecimal * _lastRangeIvashov)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice > _lastLRMA + MultIvashov.ValueDecimal * _lastRangeIvashov)
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
