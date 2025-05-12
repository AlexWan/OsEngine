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

Trend robot at the intersection of three exponential averages.

Buy: Fast Ema is higher than slow Ema.

Sell: Fast Ema is lower than slow Ema.

Exit from the buy: trailing stop in % of the loy of the candle on which you entered.

Exit from sale: trailing stop in % of the high of the candle on which you entered.
*/

namespace OsEngine.Robots.MyRobots
{
    [Bot("IntersectionOfThreeEma")]//We create an attribute so that we don't write anything in the Boot factory
    class IntersectionOfThreeEma : BotPanel
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

        // Indicator
        private Aindicator _ema1;
        private Aindicator _ema2;
        private Aindicator _ema3;

        // He last value of the indicators
        private decimal _lastEmaFast;
        private decimal _lastEmaMiddle;
        private decimal _lastEmaSlow;

        public IntersectionOfThreeEma(string name, StartProgram startProgram) : base(name, startProgram)
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

            // Creating an indicator EmaFast
            _ema1 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema1", false);
            _ema1 = (Aindicator)_tab.CreateCandleIndicator(_ema1, "Prime");
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.ParametersDigit[0].Value = _periodEmaFast.ValueInt;
            _ema1.DataSeries[0].Color = Color.Red;
            _ema1.Save();

            // Creating an indicator Middle
            _ema2 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema2", false);
            _ema2 = (Aindicator)_tab.CreateCandleIndicator(_ema2, "Prime");
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodMiddle.ValueInt;
            _ema2.ParametersDigit[0].Value = _periodMiddle.ValueInt;
            _ema2.DataSeries[0].Color = Color.Blue;
            _ema2.Save();

            // Creating an indicator EmaSlow
            _ema3 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema3", false);
            _ema3 = (Aindicator)_tab.CreateCandleIndicator(_ema3, "Prime");
            ((IndicatorParameterInt)_ema3.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _ema3.ParametersDigit[0].Value = _periodEmaSlow.ValueInt;
            _ema3.DataSeries[0].Color = Color.Green;
            _ema3.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfThreeEma_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot at the intersection of three exponential averages. " +
                "Buy: Fast Ema is higher than slow Ema. " +
                "Sell: Fast Ema is lower than slow Ema. " +
                "Exit from the buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sale: trailing stop in % of the high of the candle on which you entered.";


        }

        // Indicator Update event
        private void IntersectionOfThreeEma_ParametrsChangeByUser()
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

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEmaFast > _lastEmaMiddle && _lastEmaMiddle > _lastEmaSlow )
                    {
                        // We put a stop on the buy                       
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastEmaFast < _lastEmaMiddle && _lastEmaMiddle < _lastEmaSlow)
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

            // He last value of the indicators
            _lastEmaFast = _ema1.DataSeries[0].Last;
            _lastEmaMiddle = _ema2.DataSeries[0].Last;
            _lastEmaSlow = _ema3.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }
                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (lastPrice < _lastEmaSlow)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice > _lastEmaSlow)
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

        // The name of the robot in OsEngin
        public override string GetNameStrategyType()
        {
            return "IntersectionOfThreeEma";
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }
    }
}

