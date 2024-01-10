using System;
using System.Collections.Generic;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine.

Trend robot on Strategy WilliamsRange With Parabolic.

Buy:
1. The price is higher than the indicator value. For the next candle, the price crosses the indicator from bottom to top.
2. WPR > -45. (the signal should be generated at the first three points of the parabolic).
Sell:
1. The price is lower than the indicator value. For the next candle, the price crosses the snzu indicator-up.
2. WPR < -55. (the signal should be generated at the first three points of the parabolic).

Exit from buy: trailing stop in % of the loy of the candle on which you entered.

Exit from sell: trailing stop in % of the high of the candle on which you entered.
*/

namespace OsEngine.Robots.MyRobots
{
    [Bot("StrategyWilliamsRangeWithParabolic")] //We create an attribute so that we don't write anything in the Boot factory
    public class StrategyWilliamsRangeWithParabolic : BotPanel
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
        private StrategyParameterDecimal Step;
        private StrategyParameterDecimal MaxStep;
        private StrategyParameterInt _PeriodWilliams;

        // Indicator
        private Aindicator _Parabolic;
        private Aindicator _Williams;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        // The last value of the indicator
        private decimal _lastParabolic;
        private decimal _lastWilliams;

        // The prev value of the indicator
        private decimal _prevParabolic;
        private decimal _prevWilliams;

        public StrategyWilliamsRangeWithParabolic(string name, StartProgram startProgram) : base(name, startProgram)
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
            Step = CreateParameter("Step", 10, 10.0m, 300, 10, "Indicator");
            MaxStep = CreateParameter("Max Step", 20, 10.0m, 300, 10, "Indicator");
            _PeriodWilliams = CreateParameter("Period Williams", 14, 50, 300, 1, "Indicator");

            // Create indicator Parabolic
            _Parabolic = IndicatorsFactory.CreateIndicatorByName("ParabolicSAR", name + "Parabolic", false);
            _Parabolic = (Aindicator)_tab.CreateCandleIndicator(_Parabolic, "Prime");
            ((IndicatorParameterDecimal)_Parabolic.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_Parabolic.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _Parabolic.Save();

            // Creating an indicator WilliamsRange
            _Williams = IndicatorsFactory.CreateIndicatorByName("WilliamsRange", name + "WilliamsRange", false);
            _Williams = (Aindicator)_tab.CreateCandleIndicator(_Williams, "NewArea");
            ((IndicatorParameterInt)_Williams.Parameters[0]).ValueInt = _PeriodWilliams.ValueInt;
            _Williams.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyWilliamsRangeWithParabolic_ParametrsChangeByUser;

            // subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on Strategy WilliamsRange With Parabolic. " +
                "Buy: " +
                "1. The price is higher than the indicator value. For the next candle, the price crosses the indicator from bottom to top. " +
                "2. WPR > -45. (the signal should be generated at the first three points of the parabolic). " +
                "Sell: " +
                "1. The price is lower than the indicator value. For the next candle, the price crosses the snzu indicator-up. " +
                "2. WPR < -55. (the signal should be generated at the first three points of the parabolic). " +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";

        }

        // Indicator Update event
        private void StrategyWilliamsRangeWithParabolic_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_Parabolic.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_Parabolic.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _Parabolic.Save();
            _Parabolic.Reload();
            ((IndicatorParameterInt)_Williams.Parameters[0]).ValueInt = _PeriodWilliams.ValueInt;
            _Williams.Save();
            _Williams.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyWilliamsRangeWithParabolic";
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
            if (candles.Count < _PeriodWilliams.ValueInt)
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
            _lastParabolic = _Parabolic.DataSeries[0].Last;
            _lastWilliams = _Williams.DataSeries[0].Last;

            // The prev value of the indicator
            _prevParabolic = _Parabolic.DataSeries[0].Values[_Parabolic.DataSeries[0].Values.Count - 2];
            _lastWilliams = _Williams.DataSeries[0].Values[_Williams.DataSeries[0].Values.Count - 2];

            decimal lastPrice = candles[candles.Count - 1].Close;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_prevParabolic < lastPrice && _lastParabolic > lastPrice && _lastWilliams > -45)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, we enter the short
                {
                    if (_prevParabolic > lastPrice && _lastParabolic < lastPrice && _lastWilliams < -55)
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

            decimal stopPrice;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * TrailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);
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
