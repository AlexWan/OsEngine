using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on intersection of the Alligator indicator with Fractal.
Fast line (lips) above the middle line (teeth), medium above the slow line (jaw) and 
the price is higher than the last ascending fractal - enter into a long position.
Fast line (lips) below the midline (teeth), medium below the slow line (jaw) and
the price is lower than the last descending fractal.- enter short position

Exit from a long position: The trailing stop is placed at the minimum for the period 
specified for the trailing stop and is transferred, (slides), to new price lows, also
for the specified period.

Exit from the short position: The trailing stop is placed at the maximum for the period
specified for the trailing stop and is transferred (slides) to the new maximum of the 
price, also for the specified period.
 
 */


namespace OsEngine.Robots.Aligator
{
    [Bot("AlligatorStrategyAndFractals")] // We create an attribute so that we don't write anything to the BotFactory
    public class AlligatorStrategyAndFractals : BotPanel
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
        private StrategyParameterInt AlligatorFastLineLength;
        private StrategyParameterInt AlligatorMiddleLineLength;
        private StrategyParameterInt AlligatorSlowLineLength;

        // Indicator
        private Aindicator _Alligator;
        private Aindicator _Fractal;

        decimal _lastFractalUp = 0;
        decimal _lastFractalDown = 0;

        // The last value of the indicators
        private decimal _lastFast;
        private decimal _lastMiddle;
        private decimal _lastSlow;

        // Exit 
        private StrategyParameterInt TrailCandles;

        public AlligatorStrategyAndFractals(string name, StartProgram startProgram) : base(name, startProgram)
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
            AlligatorFastLineLength = CreateParameter("Period Simple Moving Average Fast", 20, 10, 300, 10, "Indicator");
            AlligatorMiddleLineLength = CreateParameter("Period Simple Moving Middle", 20, 10, 300, 10, "Indicator");
            AlligatorSlowLineLength = CreateParameter("Period Simple Moving Slow", 20, 10, 300, 10, "Indicator");

            // Create indicator Alligator
            _Alligator = IndicatorsFactory.CreateIndicatorByName("Alligator", name + "Alligator", false);
            _Alligator = (Aindicator)_tab.CreateCandleIndicator(_Alligator, "Prime");
            ((IndicatorParameterInt)_Alligator.Parameters[0]).ValueInt = AlligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[1]).ValueInt = AlligatorFastLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[2]).ValueInt = AlligatorMiddleLineLength.ValueInt;
            _Alligator.Save();

            // Create indicator Fractal
            _Fractal = IndicatorsFactory.CreateIndicatorByName("Fractal", name + "Fractal", false);
            _Fractal = (Aindicator)_tab.CreateCandleIndicator(_Fractal, "Prime");
            _Fractal.Save();

            // Exit
            TrailCandles = CreateParameter("Trail Candles", 5, 1, 50, 1, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += AlligatorStrategyAndFractals_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "trading robot for osengine " +
                "The trend robot on intersection of the Alligator indicator with Fractal. " +
                "Fast line (lips) above the middle line (teeth), medium above the slow line (jaw) and" +
                " the price is higher than the last ascending fractal - enter into a long position." +
                "Fast line (lips) below the midline (teeth), medium below the slow line (jaw) and " +
                "the price is lower than the last descending fractal.- enter short position " +
                "Exit from a long position: The trailing stop is placed at the minimum for the period " +
                "specified for the trailing stop and is transferred, (slides), to new price lows, also " +
                "for the specified period. " +
                "Exit from the short position: The trailing stop is placed at the maximum for the period " +
                "specified for the trailing stop and is transferred (slides) to the new maximum of the  " +
                "price, also for the specified period.";
        }

        // Indicator Update event
        private void AlligatorStrategyAndFractals_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Alligator.Parameters[0]).ValueInt = AlligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[1]).ValueInt = AlligatorFastLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[2]).ValueInt = AlligatorMiddleLineLength.ValueInt;
            _Alligator.Save();
            _Alligator.Reload();

        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "AlligatorStrategyAndFractals";
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
            if (candles.Count < AlligatorSlowLineLength.ValueInt)
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
                    _lastFractalUp = _Fractal.DataSeries[1].Values[i];
                    break;
                }
            }

            for (int i = _Fractal.DataSeries[0].Values.Count - 1; i > -1; i--)
            {
                if (_Fractal.DataSeries[0].Values[i] != 0)
                {
                    _lastFractalDown = _Fractal.DataSeries[0].Values[i];
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
            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // The last value of the indicators
                _lastFast = _Alligator.DataSeries[2].Last;
                _lastMiddle = _Alligator.DataSeries[1].Last;
                _lastSlow = _Alligator.DataSeries[0].Last;
                
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                decimal lastPrice = candles[candles.Count - 1].Close;
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastFractalUp && _lastFast > _lastMiddle && _lastMiddle > _lastSlow)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (lastPrice < _lastFractalDown && _lastFast < _lastMiddle && _lastMiddle < _lastSlow)
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

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // The last value of the indicators
            _lastFast = _Alligator.DataSeries[2].Last;
            _lastMiddle = _Alligator.DataSeries[1].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }


                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(pos, price, price - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal price = GetPriceStop(Side.Sell, candles, candles.Count - 1);
                    if(price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(pos, price, price + _slippage);
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

        private decimal GetPriceStop(Side side, List<Candle> candles, int index)
        {
            if(candles == null || index < TrailCandles.ValueInt)
            {
                return 0;
            }

            if(side == Side.Buy)
            {
                decimal price = decimal.MaxValue;

                for (int i = index; i > index - TrailCandles.ValueInt; i--)
                {
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }
                return price;
            }

            if(side == Side.Sell)
            {
                decimal price = 0;

                for(int i = index; i > index - TrailCandles.ValueInt; i--)
                {
                    if(candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }
            return 0;
        }
    }
}
