using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine.

Trend robot on Intersection Two WilliamsRange.

Buy: Fast (red) WPR is higher than slow (blue).

Sell: fast (red) WPR below slow (blue).

Exit: by the reverse intersection.
*/

namespace OsEngine.Robots.MyRobots
{
    [Bot("IntersectionTwoWilliamsRange")] //We create an attribute so that we don't write anything in the Boot factory
    public class IntersectionTwoWilliamsRange : BotPanel
    {

        BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay TimeStart;
        private StrategyParameterTimeOfDay TimeEnd;

        // Indicator Settings
        private StrategyParameterInt _PeriodWilliamsFast;
        private StrategyParameterInt _PeriodWilliamsSlow;

        // Indicator
        private Aindicator _WilliamsFast;
        private Aindicator _WilliamsSlow;

        // The last value of the indicator
        private decimal _lastWilliamsFast;
        private decimal _lastWilliamsSlow;

        public IntersectionTwoWilliamsRange(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency", }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator Settings
            _PeriodWilliamsFast = CreateParameter("Period Williams Fast", 14, 50, 300, 1, "Indicator");
            _PeriodWilliamsSlow = CreateParameter("Period Williams Slow", 28, 50, 300, 1, "Indicator");

            // Creating an indicator Ssma
            _WilliamsFast = IndicatorsFactory.CreateIndicatorByName("WilliamsRange", name + "WilliamsRange Fast", false);
            _WilliamsFast = (Aindicator)_tab.CreateCandleIndicator(_WilliamsFast, "NewArea");
            _WilliamsFast.DataSeries[0].Color = System.Drawing.Color.Red;
            ((IndicatorParameterInt)_WilliamsFast.Parameters[0]).ValueInt = _PeriodWilliamsFast.ValueInt;
            _WilliamsFast.Save();

            // Creating an indicator WilliamsRange
            _WilliamsSlow = IndicatorsFactory.CreateIndicatorByName("WilliamsRange", name + "WilliamsRange Slow", false);
            _WilliamsSlow = (Aindicator)_tab.CreateCandleIndicator(_WilliamsSlow, "NewArea");
            ((IndicatorParameterInt)_WilliamsSlow.Parameters[0]).ValueInt = _PeriodWilliamsSlow.ValueInt;
            _WilliamsSlow.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionTwoWilliamsRange_ParametrsChangeByUser;

            // subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on Intersection Two WilliamsRange. " +
                "Buy: Fast (red) WPR is higher than slow (blue). " +
                "Sell: fast (red) WPR below slow (blue). " +
                "Exit: by the reverse intersection.";

        }

        // Indicator Update event
        private void IntersectionTwoWilliamsRange_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_WilliamsFast.Parameters[0]).ValueInt = _PeriodWilliamsFast.ValueInt;
            _WilliamsFast.Save();
            _WilliamsFast.Reload();
            ((IndicatorParameterInt)_WilliamsSlow.Parameters[0]).ValueInt = _PeriodWilliamsSlow.ValueInt;
            _WilliamsSlow.Save();
            _WilliamsSlow.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionTwoWilliamsRange";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Completion Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)

        {
            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _PeriodWilliamsFast.ValueInt)
            {
                return;
            }

            // If the time does not match, we exit
            if (TimeStart.Value > _tab.TimeServerCurrent ||
                TimeEnd.Value < _tab.TimeServerCurrent)
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

            // The last value of the indicator
            _lastWilliamsFast = _WilliamsFast.DataSeries[0].Last;
            _lastWilliamsSlow = _WilliamsSlow.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastWilliamsFast > _lastWilliamsSlow)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, we enter the short
                {
                    if (_lastWilliamsFast < _lastWilliamsSlow)
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

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            // The last value of the indicator
            _lastWilliamsFast = _WilliamsFast.DataSeries[0].Last;
            _lastWilliamsSlow = _WilliamsSlow.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastWilliamsFast < _lastWilliamsSlow)
                    {

                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {

                    if (_lastWilliamsFast > _lastWilliamsSlow)
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
