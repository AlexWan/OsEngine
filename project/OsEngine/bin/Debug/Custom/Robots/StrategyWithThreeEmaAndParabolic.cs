using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;

/*Discription
Trading robot for osengine.

The trend robot on Strategy With Three Ema And Parabolic.

Buy:
1. The price is above the Parabolic value. For the next candle, the price crosses the indicator from bottom to top.
2. The fast Ema crosses the medium and slow Ema from bottom to top.
Sell:
1. The price is lower than the Parabolic value. For the next candle, the price crosses the indicator from top to bottom.
2. The fast Ema crosses the medium and slow Ema from top to bottom.

Exit: trailing stop by EmaMiddle.
*/

namespace OsEngine.Robots.MyRobots
{
    [Bot("StrategyWithThreeEmaAndParabolic")]//We create an attribute so that we don't write anything in the Boot factory
    class StrategyWithThreeEmaAndParabolic : BotPanel
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
        private StrategyParameterInt _periodEmaFast;
        private StrategyParameterInt _periodMiddle;
        private StrategyParameterInt _periodEmaSlow;
        private StrategyParameterDecimal Step;
        private StrategyParameterDecimal MaxStep;

        // Indicator
        private Aindicator _ema1;
        private Aindicator _ema2;
        private Aindicator _ema3;
        private Aindicator _Parabolic;

        // He last value of the indicators
        private decimal _lastEmaFast;
        private decimal _lastEmaMiddle;
        private decimal _lastEmaSlow;
        private decimal _lastParabolic;

        // The prev value of the indicator
        private decimal _prevEmaFast;
        private decimal _prevEmaMiddle;
        private decimal _prevEmaSlow;

        public StrategyWithThreeEmaAndParabolic(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Basic Settings
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency" }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator Settings
            _periodEmaFast = CreateParameter("fast EMA1 period", 100, 10, 300, 1, "Indicator");
            _periodMiddle = CreateParameter("middle EMA2 period", 200, 10, 300, 1, "Indicator");
            _periodEmaSlow = CreateParameter("slow EMA3 period", 300, 10, 300, 1, "Indicator");
            Step = CreateParameter("Step", 10, 10.0m, 300, 10, "Indicator");
            MaxStep = CreateParameter("Max Step", 20, 10.0m, 300, 10, "Indicator");

            // Create indicator Parabolic
            _Parabolic = IndicatorsFactory.CreateIndicatorByName("ParabolicSAR", name + "Parabolic", false);
            _Parabolic = (Aindicator)_tab.CreateCandleIndicator(_Parabolic, "Prime");
            ((IndicatorParameterDecimal)_Parabolic.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_Parabolic.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _Parabolic.Save();

            // Creating an indicator EmaFast
            _ema1 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema1", false);
            _ema1 = (Aindicator)_tab.CreateCandleIndicator(_ema1, "Prime");
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.DataSeries[0].Color = Color.Red;
            _ema1.Save();

            // Creating an indicator Middle
            _ema2 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema2", false);
            _ema2 = (Aindicator)_tab.CreateCandleIndicator(_ema2, "Prime");
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodMiddle.ValueInt;
            _ema2.DataSeries[0].Color = Color.Blue;
            _ema2.Save();

            // Creating an indicator EmaSlow
            _ema3 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema3", false);
            _ema3 = (Aindicator)_tab.CreateCandleIndicator(_ema3, "Prime");
            ((IndicatorParameterInt)_ema3.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _ema3.DataSeries[0].Color = Color.Green;
            _ema3.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyWithThreeEmaAndParabolic_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy With Three Ema And Parabolic. " +
                "Buy: " +
                "1. The price is above the Parabolic value. For the next candle, the price crosses the indicator from bottom to top. " +
                "2. The fast Ema crosses the medium and slow Ema from bottom to top. " +
                "Sell: " +
                "1. The price is lower than the Parabolic value. For the next candle, the price crosses the indicator from top to bottom. " +
                "2. The fast Ema crosses the medium and slow Ema from top to bottom. " +
                "Exit: trailing stop by EmaMiddle.";
        }

        // Indicator Update event
        private void StrategyWithThreeEmaAndParabolic_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.Save();
            _ema1.Reload();
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodMiddle.ValueInt;
            _ema2.Save();
            _ema2.Reload();
            ((IndicatorParameterInt)_ema3.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _ema3.Save();
            _ema3.Reload();
            ((IndicatorParameterDecimal)_Parabolic.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_Parabolic.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _Parabolic.Save();
            _Parabolic.Reload();
        }
        
        // The name of the robot in OsEngin
        public override string GetNameStrategyType()
        {
            return "StrategyWithThreeEmaAndParabolic";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Completion Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _periodEmaFast.ValueInt || candles.Count < _periodMiddle.ValueInt || candles.Count < _periodEmaSlow.ValueInt)
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

            // if there are positions, then go to the position closing method
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

            // He last value of the indicators
            _lastEmaFast = _ema1.DataSeries[0].Last;
            _lastEmaMiddle = _ema2.DataSeries[0].Last;
            _lastEmaSlow = _ema3.DataSeries[0].Last;
            _lastParabolic = _Parabolic.DataSeries[0].Last;

            // The prev value of the indicator
            _prevEmaFast = _ema1.DataSeries[0].Values[_ema1.DataSeries[0].Values.Count - 2];
            _prevEmaMiddle = _ema2.DataSeries[0].Values[_ema2.DataSeries[0].Values.Count - 2];
            _prevEmaSlow = _ema3.DataSeries[0].Values[_ema3.DataSeries[0].Values.Count - 2];

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEmaFast > _prevEmaMiddle 
                        && _prevEmaFast > _prevEmaSlow &&
                        lastPrice > _lastParabolic)
                    {
                        // We put a stop on the buy                       
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastEmaFast < _prevEmaMiddle 
                        && _prevEmaFast < _prevEmaSlow &&
                        lastPrice < _lastParabolic)
                    {
                        // Putting a stop on sale
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestAsk - _slippage);
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

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                // He last value of the indicators
                _lastEmaMiddle = _ema2.DataSeries[0].Last;

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    _tab.CloseAtTrailingStop(pos, _lastEmaMiddle, _lastEmaMiddle - _slippage);
                }
                else // If the direction of the position is sale
                {
                    _tab.CloseAtTrailingStop(pos, _lastEmaMiddle, _lastEmaMiddle + _slippage);
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

