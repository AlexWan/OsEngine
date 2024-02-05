using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine

Trend robot on the Strategy Two Sma, Stochastic And MacdLine.

Buy:
1. Fast Sma is higher than Slow Sma;
2. The price is higher than the fast Sma;
3. Stochastic line K (blue) is above the signal line (red) and the stochastic value is above 25 (blue line);
4. Macd line (green) above the signal line (red);
Sell:
1. Fast Sma is lower than Slow Sma;
2. The price is lower than the fast Sma;
3. Stochastic line K (blue) is below the signal line (red) and the stochastic value is below 80 (blue line);
4. Macd line (green) below the signal line (red);
Exit: 
From buy: Stochastic K line (blue) below the signal line (red);
From sell: Stochastic K line (blue) above the signal line (red).
*/

namespace OsEngine.Robots.MyRobots

{
    [Bot("StrategyTwoSmaStochasticAndMacdLine")] //We create an attribute so that we don't write anything in the Boot factory

    public class StrategyTwoSmaStochasticAndMacdLine : BotPanel
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
        private StrategyParameterInt PeriodSmaFast;
        private StrategyParameterInt PeriodSmaSlow;
        private StrategyParameterInt StochasticPeriod1;
        private StrategyParameterInt StochasticPeriod2;
        private StrategyParameterInt StochasticPeriod3;

        // Indicator
        private Aindicator _SmaFast;
        private Aindicator _SmaSlow;
        private Aindicator _MacdLine;
        private Aindicator _Stochastic;

        //The last value of the indicators
        private decimal _lastSmaFast;
        private decimal _lastSmaSlow;
        private decimal _lastMacdSignal;
        private decimal _lastMacdGreen;
        private decimal _lastBlueStoh;
        private decimal _lastRedStoh;

        public StrategyTwoSmaStochasticAndMacdLine(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodSmaFast = CreateParameter("Period SMA Fast", 100, 10, 300, 10, "Indicator");
            PeriodSmaSlow = CreateParameter("Period SMA Slow", 200, 10, 300, 10, "Indicator");
            StochasticPeriod1 = CreateParameter("Stochastic Period One", 10, 10, 300, 10, "Indicator");
            StochasticPeriod2 = CreateParameter("Stochastic Period Two", 20, 10, 300, 10, "Indicator");
            StochasticPeriod3 = CreateParameter("Stochastic Period Three", 30, 10, 300, 10, "Indicator");

            // Create indicator SmaFast
            _SmaFast = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaFast", false);
            _SmaFast = (Aindicator)_tab.CreateCandleIndicator(_SmaFast, "Prime");
            ((IndicatorParameterInt)_SmaFast.Parameters[0]).ValueInt = PeriodSmaFast.ValueInt;
            _SmaFast.Save();

            // Create indicator SmaSlow
            _SmaSlow = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaSlow", false);
            _SmaSlow = (Aindicator)_tab.CreateCandleIndicator(_SmaSlow, "Prime");
            _SmaSlow.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_SmaSlow.Parameters[0]).ValueInt = PeriodSmaSlow.ValueInt;
            _SmaSlow.Save();

            // Create indicator macd
            _MacdLine = IndicatorsFactory.CreateIndicatorByName("MacdLine", name + "MacdLine", false);
            _MacdLine = (Aindicator)_tab.CreateCandleIndicator(_MacdLine, "NewArea");
            ((IndicatorParameterInt)_MacdLine.Parameters[0]).ValueInt = _FastPeriod.ValueInt;
            ((IndicatorParameterInt)_MacdLine.Parameters[1]).ValueInt = _SlowPeriod.ValueInt;
            ((IndicatorParameterInt)_MacdLine.Parameters[2]).ValueInt = _SignalPeriod.ValueInt;
            _MacdLine.Save();

            // Create indicator Stochastic
            _Stochastic = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stochastic", false);
            _Stochastic = (Aindicator)_tab.CreateCandleIndicator(_Stochastic, "NewArea0");
            ((IndicatorParameterInt)_Stochastic.Parameters[0]).ValueInt = StochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stochastic.Parameters[1]).ValueInt = StochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_Stochastic.Parameters[2]).ValueInt = StochasticPeriod3.ValueInt;
            _Stochastic.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyTwoSmaStochasticAndMacdLine_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on the Strategy Two Sma, Stochastic And MacdLine. " +
                "Buy: " +
                "1. Fast Sma is higher than Slow Sma; " +
                "2. The price is higher than the fast Sma; " +
                "3. Stochastic line K (blue) is above the signal line (red) and the stochastic value is above 25 (blue line); " +
                "4. Macd line (green) above the signal line (red); " +
                "Sell: " +
                "1. Fast Sma is lower than Slow Sma; " +
                "2. The price is lower than the fast Sma; " +
                "3. Stochastic line K (blue) is below the signal line (red) and the stochastic value is below 80 (blue line); " +
                "4. Macd line (green) below the signal line (red); " +
                "Exit:  " +
                "From buy: Stochastic K line (blue) below the signal line (red); " +
                "From sell: Stochastic K line (blue) above the signal line (red).";
        }

        // Indicator Update event
        private void StrategyTwoSmaStochasticAndMacdLine_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_MacdLine.Parameters[0]).ValueInt = _FastPeriod.ValueInt;
            ((IndicatorParameterInt)_MacdLine.Parameters[1]).ValueInt = _SlowPeriod.ValueInt;
            ((IndicatorParameterInt)_MacdLine.Parameters[2]).ValueInt = _SignalPeriod.ValueInt;
            _MacdLine.Save();
            _MacdLine.Reload();
            ((IndicatorParameterInt)_SmaFast.Parameters[0]).ValueInt = PeriodSmaFast.ValueInt;
            _SmaFast.Save();
            _SmaFast.Reload();
            ((IndicatorParameterInt)_SmaSlow.Parameters[0]).ValueInt = PeriodSmaSlow.ValueInt;
            _SmaSlow.Save();
            _SmaSlow.Reload();
            ((IndicatorParameterInt)_Stochastic.Parameters[0]).ValueInt = StochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stochastic.Parameters[1]).ValueInt = StochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_Stochastic.Parameters[2]).ValueInt = StochasticPeriod3.ValueInt;
            _Stochastic.Save();
            _Stochastic.Reload();
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
                _lastSmaFast = _SmaFast.DataSeries[0].Last;
                _lastSmaSlow = _SmaSlow.DataSeries[0].Last;
                _lastBlueStoh = _Stochastic.DataSeries[0].Last;
                _lastRedStoh = _Stochastic.DataSeries[1].Last;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Long
                if (Regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    if (_lastMacdGreen > _lastMacdSignal &&
                        _lastSmaFast > _lastSmaSlow &&
                        lastPrice > _lastSmaFast &&
                        _lastBlueStoh > _lastRedStoh &&
                        _lastBlueStoh > 25)
                    {

                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if (_lastMacdGreen < _lastMacdSignal &&
                        _lastSmaFast < _lastSmaSlow &&
                        lastPrice < _lastSmaFast &&
                        _lastBlueStoh < _lastRedStoh &&
                        _lastBlueStoh < 80)
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
            _lastBlueStoh = _Stochastic.DataSeries[0].Last;
            _lastRedStoh = _Stochastic.DataSeries[1].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastBlueStoh < _lastRedStoh)
                    {

                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {

                    if (_lastBlueStoh > _lastRedStoh)
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

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyTwoSmaStochasticAndMacdLine";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}
