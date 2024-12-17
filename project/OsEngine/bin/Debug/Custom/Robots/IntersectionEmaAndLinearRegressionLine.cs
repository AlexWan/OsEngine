using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;

/*Discription
Trading robot for osengine

Trend robot at the Intersection Ema And LinearRegressionLine.

Buy: Ema is higher than LRMA.

Sell: Ema is lower than LRMA.

Exit from buy: trailing stop in % of the loy of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.
*/

namespace OsEngine.Robots.MyRobots
{
    [Bot("IntersectionEmaAndLinearRegressionLine")] //We create an attribute so that we don't write anything in the Boot factory
    public class IntersectionEmaAndLinearRegressionLine : BotPanel
    {
        BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay TimeStart;
        private StrategyParameterTimeOfDay TimeEnd;

        // Indicator
        private Aindicator _Ema;
        private Aindicator _LRline;

        // Indicator setting
        private StrategyParameterInt _PeriodEma;
        private StrategyParameterInt _PeriodLRLine;

        // The last value of the indicators
        private decimal _lastEma;
        private decimal _lastLRline;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        public IntersectionEmaAndLinearRegressionLine(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency" }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator Settings
            _PeriodEma = CreateParameter("Ema period", 100, 50, 500, 50, "Indicator");
            _PeriodLRLine = CreateParameter("LR line period", 14, 10, 100, 10, "Indicator");

            // Creating indicator Ema
            _Ema = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA",  false);
            _Ema = (Aindicator)_tab.CreateCandleIndicator(_Ema, "Prime");
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = _PeriodEma.ValueInt;
            _Ema.Save();

            // Creating indicator Lrline
            _LRline = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LinearRegressionLine", false);
            _LRline = (Aindicator)_tab.CreateCandleIndicator(_LRline, "Prime");
            ((IndicatorParameterInt)_LRline.Parameters[0]).ValueInt = _PeriodLRLine.ValueInt;
            _LRline.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionEmaAndLinearRegressionLine_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            Description = "Trend robot at the Intersection Ema And LinearRegressionLine. " +
                "Buy: Ema is higher than LRMA. " +
                "Sell: Ema is lower than LRMA. " +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";

        }

        // Indicator Update event
        private void IntersectionEmaAndLinearRegressionLine_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = _PeriodEma.ValueInt;
            _Ema.Save();
            _Ema.Reload();
            ((IndicatorParameterInt)_LRline.Parameters[0]).ValueInt = _PeriodLRLine.ValueInt;
            _LRline.Save();
            _LRline.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionEmaAndLinearRegressionLine";
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
            if (candles.Count < _PeriodEma.ValueInt || candles.Count < _PeriodLRLine.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
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
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    // The last value of the indicators
                    _lastEma = _Ema.DataSeries[0].Last;
                    _lastLRline = _LRline.DataSeries[0].Last;

                    if (_lastEma > _lastLRline)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastEma < _lastLRline)
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
            
            decimal stopPrice;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
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


