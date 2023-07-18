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

The trend robot on strategy on two Ema and two Vwma

Buy: Fast Vwma and slow Vwma punch up (above) both Emas (also fast and slow).

Sale: Fast Vwma and slow Vwma punch down (below) both Emas (also fast and slow).

Exit from purchase: Fast Vwma below slow Vwma.

Exit from sale: Fast Vwma above slow Vwma.

*/

namespace OsEngine.Robots.Vwma
{
    [Bot("StrategyOnTwoEmaAndTwoVwma")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyOnTwoEmaAndTwoVwma : BotPanel
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
        private StrategyParameterInt PeriodEmaFast;
        private StrategyParameterInt PeriodEmaSlow;
        private StrategyParameterInt PeriodVwmaFast;
        private StrategyParameterInt PeriodVwmaSlow;

        // Indicator
        private Aindicator _EmaFast;
        private Aindicator _EmaSlow;
        private Aindicator _VwmaFast;
        private Aindicator _VwmaSlow;

        // The last value of the indicators
        private decimal _lastEmaFast;
        private decimal _lastEmaSlow;
        private decimal _lastVwmaFast;
        private decimal _lastVwmaSlow;

        public StrategyOnTwoEmaAndTwoVwma(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodEmaFast = CreateParameter("Period Ema Fast", 100, 10, 300, 10, "Indicator");
            PeriodEmaSlow = CreateParameter("Period Ema Slow", 200, 10, 300, 10, "Indicator");
            PeriodVwmaFast = CreateParameter("Period Vwma Fast", 100, 10, 300, 10, "Indicator");
            PeriodVwmaSlow = CreateParameter("Period Vwma Slow", 200, 10, 300, 10, "Indicator");

            // Create indicator EmaFast
            _EmaFast = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaFast", false);
            _EmaFast = (Aindicator)_tab.CreateCandleIndicator(_EmaFast, "Prime");
            ((IndicatorParameterInt)_EmaFast.Parameters[0]).ValueInt = PeriodEmaFast.ValueInt;
            _EmaFast.Save();

            // Create indicator EmaSlow
            _EmaSlow = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaSlow", false);
            _EmaSlow = (Aindicator)_tab.CreateCandleIndicator(_EmaSlow, "Prime");
            ((IndicatorParameterInt)_EmaSlow.Parameters[0]).ValueInt = PeriodEmaSlow.ValueInt;
            _EmaSlow.Save();

            // Create indicator VwmaFast
            _VwmaFast = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VwmaFast", false);
            _VwmaFast = (Aindicator)_tab.CreateCandleIndicator(_VwmaFast, "Prime");
            ((IndicatorParameterInt)_VwmaFast.Parameters[0]).ValueInt = PeriodVwmaFast.ValueInt;
            _VwmaFast.Save();

            // Create indicator VwmaSlow
            _VwmaSlow = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VwmaSlow", false);
            _VwmaSlow = (Aindicator)_tab.CreateCandleIndicator(_VwmaSlow, "Prime");
            _VwmaSlow.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_VwmaSlow.Parameters[0]).ValueInt = PeriodVwmaSlow.ValueInt;
            _VwmaSlow.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += VwmaWithAShift_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on strategy on two Ema and two Vwma " +
                "Buy: Fast Vwma and slow Vwma punch up (above) both Emas (also fast and slow). " +
                "Sale: Fast Vwma and slow Vwma punch down (below) both Emas (also fast and slow). " +
                "Exit from purchase: Fast Vwma below slow Vwma. " +
                "Exit from sale: Fast Vwma above slow Vwma.";
        }

        // Indicator Update event
        private void VwmaWithAShift_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_VwmaFast.Parameters[0]).ValueInt = PeriodVwmaFast.ValueInt;
            _VwmaFast.Save();
            _VwmaFast.Reload();
            ((IndicatorParameterInt)_VwmaSlow.Parameters[0]).ValueInt = PeriodVwmaSlow.ValueInt;
            _VwmaSlow.Save();
            _VwmaSlow.Reload();
            ((IndicatorParameterInt)_EmaFast.Parameters[0]).ValueInt = PeriodEmaFast.ValueInt;
            _EmaFast.Save();
            _EmaFast.Reload();
            ((IndicatorParameterInt)_EmaSlow.Parameters[0]).ValueInt = PeriodEmaSlow.ValueInt;
            _EmaSlow.Save();
            _EmaSlow.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyOnTwoEmaAndTwoVwma";
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
            if (candles.Count < PeriodVwmaFast.ValueInt || candles.Count < PeriodVwmaSlow.ValueInt)
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
                _lastEmaFast = _EmaFast.DataSeries[0].Last;
                _lastEmaSlow = _EmaSlow.DataSeries[0].Last;
                _lastVwmaFast = _VwmaFast.DataSeries[0].Last;
                _lastVwmaSlow = _VwmaSlow.DataSeries[0].Last;

                decimal lastPrice = candles[candles.Count - 1].Close;

                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastVwmaFast > _lastEmaFast && _lastVwmaFast > _lastEmaSlow
                        && _lastVwmaSlow > _lastEmaFast && _lastVwmaSlow > _lastEmaSlow)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastVwmaFast < _lastEmaFast && _lastVwmaFast < _lastEmaSlow
                        && _lastVwmaSlow < _lastEmaFast && _lastVwmaSlow < _lastEmaSlow)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestAsk - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {                        
                decimal lastPrice = candles[candles.Count - 1].Close;

                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }
                // The last value of the indicators
                _lastEmaFast = _EmaFast.DataSeries[0].Last;
                _lastEmaSlow = _EmaSlow.DataSeries[0].Last;
                _lastVwmaFast = _VwmaFast.DataSeries[0].Last;
                _lastVwmaSlow = _VwmaSlow.DataSeries[0].Last;
                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                   if(_lastVwmaFast < _lastEmaFast && _lastVwmaFast < _lastEmaSlow
                      && _lastVwmaSlow < _lastEmaFast && _lastVwmaSlow < _lastEmaSlow)
                   {

                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if(_lastVwmaFast > _lastEmaFast && _lastVwmaFast > _lastEmaSlow
                        && _lastVwmaSlow > _lastEmaFast && _lastVwmaSlow > _lastEmaSlow)
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

