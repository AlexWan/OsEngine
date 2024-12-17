using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine

Trend robot on the Strategy Sma, Ema, Parabolic And MacdLine.

Buy:
1. Ema above Sma;
2. The price is higher than the Parabolic value;
3. Macd line (green) above the signal line (red);
Sell:
1. The Ema is lower than the Sma;
2. The price is below the Parabolic value;
3. Macd line (green) below the signal line (red);

Exit from buy: The Ema is below the Sma.
Exit from sell: Ema is higher than Sma.
*/

namespace OsEngine.Robots.MyRobots

{
    [Bot("StrategySmaEmaParabolicAndMacdLine")] //We create an attribute so that we don't write anything in the Boot factory

    public class StrategySmaEmaParabolicAndMacdLine : BotPanel
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
        private StrategyParameterInt _FastPeriod;
        private StrategyParameterInt _SlowPeriod;
        private StrategyParameterInt _SignalPeriod;
        private StrategyParameterInt PeriodSma;
        private StrategyParameterInt PeriodEma;
        private StrategyParameterDecimal Step;
        private StrategyParameterDecimal MaxStep;

        // Indicator
        private Aindicator _Sma;
        private Aindicator _Ema;
        private Aindicator _MacdLine;
        private Aindicator _Parabolic;

        //The last value of the indicators
        private decimal _lastSma;
        private decimal _lastEma;
        private decimal _lastMacdSignal;
        private decimal _lastMacdGreen;
        private decimal _lastParabolic;

        public StrategySmaEmaParabolicAndMacdLine(string name, StartProgram startProgram) : base(name, startProgram)
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
            _FastPeriod = CreateParameter("Fast period", 12, 50, 300, 1, "Indicator");
            _SlowPeriod = CreateParameter("Slow period", 26, 50, 300, 1, "Indicator");
            _SignalPeriod = CreateParameter("Signal Period", 9, 50, 300, 1, "Indicator");
            PeriodSma = CreateParameter("Period Sma", 100, 10, 300, 10, "Indicator");
            PeriodEma = CreateParameter("Period Ema", 200, 10, 300, 10, "Indicator");
            Step = CreateParameter("Step", 10, 10.0m, 300, 10, "Indicator");
            MaxStep = CreateParameter("Max Step", 20, 10.0m, 300, 10, "Indicator");

            // Create indicator Sma
            _Sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _Sma = (Aindicator)_tab.CreateCandleIndicator(_Sma, "Prime");
            ((IndicatorParameterInt)_Sma.Parameters[0]).ValueInt = PeriodSma.ValueInt;
            _Sma.Save();

            // Create indicator Ema
            _Ema = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema", false);
            _Ema = (Aindicator)_tab.CreateCandleIndicator(_Ema, "Prime");
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = PeriodEma.ValueInt;
            _Ema.Save();

            // Create indicator macd
            _MacdLine = IndicatorsFactory.CreateIndicatorByName("MacdLine", name + "MacdLine", false);
            _MacdLine = (Aindicator)_tab.CreateCandleIndicator(_MacdLine, "NewArea");
            ((IndicatorParameterInt)_MacdLine.Parameters[0]).ValueInt = _FastPeriod.ValueInt;
            ((IndicatorParameterInt)_MacdLine.Parameters[1]).ValueInt = _SlowPeriod.ValueInt;
            ((IndicatorParameterInt)_MacdLine.Parameters[2]).ValueInt = _SignalPeriod.ValueInt;
            _MacdLine.Save();

            // Create indicator Parabolic
            _Parabolic = IndicatorsFactory.CreateIndicatorByName("ParabolicSAR", name + "Parabolic", false);
            _Parabolic = (Aindicator)_tab.CreateCandleIndicator(_Parabolic, "Prime");
            ((IndicatorParameterDecimal)_Parabolic.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_Parabolic.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _Parabolic.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategySmaEmaParabolicAndMacdLine_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on the Strategy Sma, Ema, Parabolic And MacdLine. " +
                "Buy: " +
                "1. Ema above Sma; " +
                "2. The price is higher than the Parabolic value; " +
                "3. Macd line (green) above the signal line (red); " +
                "Sell: " +
                "1. The Ema is lower than the Sma; " +
                "2. The price is below the Parabolic value; " +
                "3. Macd line (green) below the signal line (red); " +
                "Exit from buy: The Ema is below the Sma. " +
                "Exit from sell: Ema is higher than Sma.";
        }

        // Indicator Update event
        private void StrategySmaEmaParabolicAndMacdLine_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_MacdLine.Parameters[0]).ValueInt = _FastPeriod.ValueInt;
            ((IndicatorParameterInt)_MacdLine.Parameters[1]).ValueInt = _SlowPeriod.ValueInt;
            ((IndicatorParameterInt)_MacdLine.Parameters[2]).ValueInt = _SignalPeriod.ValueInt;
            _MacdLine.Save();
            _MacdLine.Reload();
            ((IndicatorParameterInt)_Sma.Parameters[0]).ValueInt = PeriodSma.ValueInt;
            _Sma.Save();
            _Sma.Reload();
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = PeriodEma.ValueInt;
            _Ema.Save();
            _Ema.Reload();
            ((IndicatorParameterDecimal)_Parabolic.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_Parabolic.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _Parabolic.Save();
            _Parabolic.Reload();
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
            if (candles.Count < _FastPeriod.ValueInt)
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
                _lastMacdSignal = _MacdLine.DataSeries[1].Last;
                _lastMacdGreen = _MacdLine.DataSeries[0].Last;
                _lastSma = _Sma.DataSeries[0].Last;
                _lastEma = _Ema.DataSeries[0].Last;
                _lastParabolic = _Parabolic.DataSeries[0].Last;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Long
                if (Regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    if (_lastMacdGreen > _lastMacdSignal &&
                        _lastSma < _lastEma &&
                        lastPrice > _lastParabolic)
                    {

                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if (_lastMacdGreen < _lastMacdSignal &&
                        _lastSma > _lastEma &&
                        lastPrice < _lastParabolic)
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

            // The last value of the indicators
            _lastSma = _Sma.DataSeries[0].Last;
            _lastEma = _Ema.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastEma < _lastSma)
                    {

                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {

                    if (_lastEma > _lastSma)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
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

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategySmaEmaParabolicAndMacdLine";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}
