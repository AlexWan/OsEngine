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

The trend robot on intersection of the Alligator indicator with the price.
The price is above the fast (lips) line, the fast is above the average line (teeth), the average is above the slow (jaw) - enter into a long position.
The price is below the fast (lips) line, the fast is below the average line (teeth), the average is below the slow (jaw) - enter short position

Exit the position the price crosses the middle line. 
 
 */


namespace OsEngine.Robots.Aligator
{
    [Bot("BreakAlligator")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakAlligator : BotPanel
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

        // The last value of the indicators
        private decimal _lastFast;
        private decimal _lastMiddle;
        private decimal _lastSlow;

        public BreakAlligator(string name, StartProgram startProgram) : base(name, startProgram)
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
            AlligatorFastLineLength = CreateParameter("Period Simple Moving Average Fast", 20, 10, 300, 10, "indicator");
            AlligatorMiddleLineLength = CreateParameter("Period Simple Moving Middle", 20, 10, 300, 10, "indicator");
            AlligatorSlowLineLength = CreateParameter("Period Simple Moving Slow", 20, 10, 300, 10, "indicator");

            // Create indicator
            _Alligator = IndicatorsFactory.CreateIndicatorByName("Alligator", name + "Alligator", false);
            _Alligator = (Aindicator)_tab.CreateCandleIndicator(_Alligator, "Prime");
            ((IndicatorParameterInt)_Alligator.Parameters[0]).ValueInt = AlligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[1]).ValueInt = AlligatorFastLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[2]).ValueInt = AlligatorMiddleLineLength.ValueInt;
            _Alligator.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakAlligator_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
        }

        // Indicator Update event
        private void BreakAlligator_ParametrsChangeByUser()
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
            return "BreakAlligator";
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
                    if (lastPrice > _lastFast && _lastFast > _lastMiddle && _lastMiddle > _lastSlow)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    
                    if (lastPrice < _lastFast && _lastFast < _lastMiddle && _lastMiddle < _lastSlow)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage);
                    }
                }
                return;
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
                   if(_lastFast < _lastMiddle)
                   {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if(_lastFast > _lastMiddle)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice + _slippage, openPositions[0].OpenVolume);
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
