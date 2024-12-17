using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine

Trend robot on the Strategy Two Stochastic.

Buy:
1. A fast stochastic is in the oversold zone or has just left it (below 30) and the stochastic line (blue) is above the signal line (red).
2. Slow stochastic in the oversold zone (below 20) and the stochastic line (blue) above the signal line (red).

Sell:
1. A fast stochastic is in the overbought zone or has just left it (above 70) and the stochastic line (blue) is below the signal line (red).
2. Slow stochastic in the overbought zone (above 80) and the stochastic line (blue) below the signal line (red).

Exit from buy: trailing stop in % of the loy of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.
*/

namespace OsEngine.Robots.MyRobots

{
    [Bot("StrategyTwoStochastic")] //We create an attribute so that we don't write anything in the Boot factory

    public class StrategyTwoStochastic : BotPanel
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
        private StrategyParameterInt FastStochasticPeriod1;
        private StrategyParameterInt FastStochasticPeriod2;
        private StrategyParameterInt FastStochasticPeriod3;
        private StrategyParameterInt SlowStochasticPeriod1;
        private StrategyParameterInt SlowStochasticPeriod2;
        private StrategyParameterInt SlowStochasticPeriod3;

        // Indicator
        private Aindicator _FastStochastic;
        private Aindicator _SlowStochastic;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        //The last value of the indicators
        private decimal _lastBlueStohFast;
        private decimal _lastRedStohFast;
        private decimal _lastBlueStohSlow;
        private decimal _lastRedStohSlow;

        public StrategyTwoStochastic(string name, StartProgram startProgram) : base(name, startProgram)
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
            FastStochasticPeriod1 = CreateParameter("Fast Stochastic Period One", 10, 10, 300, 10, "Indicator");
            FastStochasticPeriod2 = CreateParameter("Fast Stochastic Period Two", 20, 10, 300, 10, "Indicator");
            FastStochasticPeriod3 = CreateParameter("Fast Stochastic Period Three", 30, 10, 300, 10, "Indicator");
            SlowStochasticPeriod1 = CreateParameter("Slow Stochastic Period One", 10, 10, 300, 10, "Indicator");
            SlowStochasticPeriod2 = CreateParameter("Slow Stochastic Period Two", 20, 10, 300, 10, "Indicator");
            SlowStochasticPeriod3 = CreateParameter("Slow Stochastic Period Three", 30, 10, 300, 10, "Indicator");

            // Create indicator Stochastic Fast
            _FastStochastic = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "StochasticFast", false);
            _FastStochastic = (Aindicator)_tab.CreateCandleIndicator(_FastStochastic, "NewArea0");
            ((IndicatorParameterInt)_FastStochastic.Parameters[0]).ValueInt = FastStochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_FastStochastic.Parameters[1]).ValueInt = FastStochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_FastStochastic.Parameters[2]).ValueInt = FastStochasticPeriod3.ValueInt;
            _FastStochastic.Save();

            // Create indicator Stochastic Slow
            _SlowStochastic = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "StochasticSlow", false);
            _SlowStochastic = (Aindicator)_tab.CreateCandleIndicator(_SlowStochastic, "NewArea");
            ((IndicatorParameterInt)_SlowStochastic.Parameters[0]).ValueInt = SlowStochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_SlowStochastic.Parameters[1]).ValueInt = SlowStochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_SlowStochastic.Parameters[2]).ValueInt = SlowStochasticPeriod3.ValueInt;
            _SlowStochastic.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyTwoStochastic_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on the Strategy Two Stochastic. " +
                "Buy: " +
                "1. A fast stochastic is in the oversold zone or has just left it (below 30) and the stochastic line (blue) is above the signal line (red). " +
                "2. Slow stochastic in the oversold zone (below 20) and the stochastic line (blue) above the signal line (red). " +
                "Sell: " +
                "1. A fast stochastic is in the overbought zone or has just left it (above 70) and the stochastic line (blue) is below the signal line (red). " +
                "2. Slow stochastic in the overbought zone (above 80) and the stochastic line (blue) below the signal line (red). " +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";
        }

        // Indicator Update event
        private void StrategyTwoStochastic_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_FastStochastic.Parameters[0]).ValueInt = FastStochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_FastStochastic.Parameters[1]).ValueInt = FastStochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_FastStochastic.Parameters[2]).ValueInt = FastStochasticPeriod3.ValueInt;
            _FastStochastic.Save();
            _FastStochastic.Reload();
            ((IndicatorParameterInt)_SlowStochastic.Parameters[0]).ValueInt = SlowStochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_SlowStochastic.Parameters[1]).ValueInt = SlowStochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_SlowStochastic.Parameters[2]).ValueInt = SlowStochasticPeriod3.ValueInt;
            _SlowStochastic.Save();
            _SlowStochastic.Reload();
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
            if (candles.Count < FastStochasticPeriod1.ValueInt ||
                candles.Count < FastStochasticPeriod2.ValueInt ||
                candles.Count < FastStochasticPeriod3.ValueInt ||
                candles.Count < SlowStochasticPeriod1.ValueInt ||
                candles.Count < SlowStochasticPeriod2.ValueInt ||
                candles.Count < SlowStochasticPeriod3.ValueInt)
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

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // The last value of the indicators     
                _lastBlueStohFast = _FastStochastic.DataSeries[0].Last;
                _lastRedStohFast = _FastStochastic.DataSeries[1].Last;
                _lastBlueStohSlow = _SlowStochastic.DataSeries[0].Last;
                _lastRedStohSlow = _SlowStochastic.DataSeries[1].Last;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Long
                if (Regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    if (_lastBlueStohFast < 30 && _lastBlueStohFast > _lastRedStohFast &&
                        _lastBlueStohSlow < 20 && _lastBlueStohSlow > _lastRedStohSlow)
                    {

                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if (_lastBlueStohFast > 70 && _lastBlueStohFast < _lastRedStohFast &&
                        _lastBlueStohSlow > 80 && _lastBlueStohSlow < _lastRedStohSlow)
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
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * TrailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(position, stopPrice, stopPrice);
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

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyTwoStochastic";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}
