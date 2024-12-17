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

/* Description
trading robot for osengine

The trend robot on strategy ADX, Stochastic and three Ema.

Buy:
1. fast Ema is higher than the average Ema and the average is higher than the slow one.

2. Stochastic crosses the level 50 and is growing (from bottom to top).

3. Adx is rising and crosses level 20 upwards (growing).

Sell:
1. fast Ema is below the average Ema and the average is below the slow one.

2. Stochastic crosses the level 50 and falls (from top to bottom).

3. Adx is rising and crosses level 20 upwards (growing).

Exit:
From buy: fast Ema below average Ema.

From sell: fast Ema above average Ema.
 
 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyADXStohAndThreeEMA")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyADXStohAndThreeEMA : BotPanel
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
        private StrategyParameterInt PeriodEmaFast;
        private StrategyParameterInt PeriodEmaMiddle;
        private StrategyParameterInt PeriodEmaSlow;
        private StrategyParameterInt PeriodADX;
        private StrategyParameterInt StochasticPeriod1;
        private StrategyParameterInt StochasticPeriod2;
        private StrategyParameterInt StochasticPeriod3;

        // Indicator
        Aindicator _EmaFast;
        Aindicator _EmaMiddle;
        Aindicator _EmaSlow;   
        Aindicator _ADX;
        Aindicator _Stoh;

        // The last value of the indicator
        private decimal _lastEmaFast;
        private decimal _lastEmaMiddle;
        private decimal _lastEmaSlow;
        private decimal _lastADX;
        private decimal _lastStoh;

        // The prev value of the indicator
        private decimal _prevADX;
        private decimal _prevStoh;

        public StrategyADXStohAndThreeEMA(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodEmaFast = CreateParameter("Period Ema Fast", 10, 10, 100, 10, "Indicator");
            PeriodEmaMiddle = CreateParameter("Period Ema Middle", 20, 10, 300, 10, "Indicator");
            PeriodEmaSlow = CreateParameter("Period Ema Slow", 30, 10, 100, 10, "Indicator");
            PeriodADX = CreateParameter("Period ADX", 10, 10, 300, 10, "Indicator");
            StochasticPeriod1 = CreateParameter("StochasticPeriod One", 10, 10, 300, 10, "Indicator");
            StochasticPeriod2 = CreateParameter("StochasticPeriod Two", 3, 10, 300, 10, "Indicator");
            StochasticPeriod3 = CreateParameter("StochasticPeriod Three", 3, 10, 300, 10, "Indicator");

            // Create indicator EmaFast
            _EmaFast = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaFast", false);
            _EmaFast = (Aindicator)_tab.CreateCandleIndicator(_EmaFast, "Prime");
            ((IndicatorParameterInt)_EmaFast.Parameters[0]).ValueInt = PeriodEmaFast.ValueInt;
            _EmaFast.DataSeries[0].Color = Color.Gray;
            _EmaFast.Save();

            // Create indicator EmaMiddle
            _EmaMiddle = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaMiddle", false);
            _EmaMiddle = (Aindicator)_tab.CreateCandleIndicator(_EmaMiddle, "Prime");
            ((IndicatorParameterInt)_EmaMiddle.Parameters[0]).ValueInt = PeriodEmaMiddle.ValueInt;
            _EmaMiddle.DataSeries[0].Color = Color.Pink;
            _EmaMiddle.Save();

            // Create indicator EmaSlow
            _EmaSlow = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaSlow", false);
            _EmaSlow = (Aindicator)_tab.CreateCandleIndicator(_EmaSlow, "Prime");
            ((IndicatorParameterInt)_EmaSlow.Parameters[0]).ValueInt = PeriodEmaSlow.ValueInt;
            _EmaSlow.DataSeries[0].Color = Color.Yellow;
            _EmaSlow.Save();

            // Create indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();

            // Create indicator Stoh
            _Stoh = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stochastic", false);
            _Stoh = (Aindicator)_tab.CreateCandleIndicator(_Stoh, "NewArea0");
            ((IndicatorParameterInt)_Stoh.Parameters[0]).ValueInt = StochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[1]).ValueInt = StochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[2]).ValueInt = StochasticPeriod3.ValueInt;
            _Stoh.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyADXStohAndThreeEMA_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on strategy ADX, Stochastic and three Ema. " +
                "Buy: " +
                "1. fast Ema is higher than the average Ema and the average is higher than the slow one. " +
                "2. Stochastic crosses the level 50 and is growing (from bottom to top). " +
                "3. Adx is rising and crosses level 20 upwards (growing). " +
                "Sell: " +
                "1. fast Ema is below the average Ema and the average is below the slow one. " +
                "2. Stochastic crosses the level 50 and falls (from top to bottom). " +
                "3. Adx is rising and crosses level 20 upwards (growing). " +
                "Exit: " +
                "From buy: fast Ema below average Ema. " +
                "From sell: fast Ema above average Ema.";
        }

        private void StrategyADXStohAndThreeEMA_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_EmaFast.Parameters[0]).ValueInt = PeriodEmaFast.ValueInt;
            _EmaFast.Save();
            _EmaFast.Reload();
            ((IndicatorParameterInt)_EmaMiddle.Parameters[0]).ValueInt = PeriodEmaMiddle.ValueInt;
            _EmaMiddle.Save();
            _EmaMiddle.Reload();
            ((IndicatorParameterInt)_EmaSlow.Parameters[0]).ValueInt = PeriodEmaSlow.ValueInt;
            _EmaSlow.Save();
            _EmaSlow.Reload();
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();
            ((IndicatorParameterInt)_Stoh.Parameters[0]).ValueInt = StochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[1]).ValueInt = StochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[2]).ValueInt = StochasticPeriod3.ValueInt;
            _Stoh.Save();
            _Stoh.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyADXStohAndThreeEMA";
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
            if (candles.Count < PeriodEmaFast.ValueInt || 
                candles.Count < PeriodEmaMiddle.ValueInt ||
                candles.Count < PeriodEmaSlow.ValueInt || 
                candles.Count < PeriodADX.ValueInt || 
                candles.Count < StochasticPeriod1.ValueInt ||
                candles.Count < StochasticPeriod2.ValueInt ||
                candles.Count < StochasticPeriod3.ValueInt)
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
            // The last value of the indicator
            _lastEmaFast = _EmaFast.DataSeries[0].Last;
            _lastEmaMiddle = _EmaMiddle.DataSeries[0].Last;
            _lastEmaSlow = _EmaSlow.DataSeries[0].Last;
            _lastADX = _ADX.DataSeries[0].Last;
            _lastStoh = _Stoh.DataSeries[0].Last;

            // The prev value of the indicator
            _prevADX = _ADX.DataSeries[0].Values[_ADX.DataSeries[0].Values.Count - 2];
            _prevStoh = _Stoh.DataSeries[0].Values[_Stoh.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> values = _ADX.DataSeries[0].Values;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEmaFast > _lastEmaMiddle &&
                        _lastEmaMiddle > _lastEmaSlow &&
                        _prevStoh < 50 && _lastStoh > 50 &&
                        _prevADX < 20 && _lastADX > 20)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastEmaFast < _lastEmaMiddle &&
                        _lastEmaMiddle < _lastEmaSlow &&
                        _prevStoh > 50 && _lastStoh < 50 &&
                        _prevADX < 20 && _lastADX > 20)
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
            _lastEmaFast = _EmaFast.DataSeries[0].Last;
            _lastEmaMiddle = _EmaMiddle.DataSeries[0].Last;

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
                    if(_lastEmaFast < _lastEmaMiddle)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastEmaFast > _lastEmaMiddle)
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