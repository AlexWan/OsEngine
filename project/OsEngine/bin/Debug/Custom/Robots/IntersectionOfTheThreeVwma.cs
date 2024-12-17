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

The trend robot on intersection of three Vwma

Buy: Fast Vwma above average Vwma and medium above slow.

Sell: Fast Vwma below average Vwma and medium below slow.

Exit: on the opposite signal.

*/

namespace OsEngine.Robots.Vwma
{
    [Bot("IntersectionOfTheThreeVwma")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionOfTheThreeVwma : BotPanel
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
        private StrategyParameterInt PeriodVwmaFast;
        private StrategyParameterInt PeriodVwmaMiddle;
        private StrategyParameterInt PeriodVwmaSlow;

        // Indicator
        private Aindicator _VwmaFast;
        private Aindicator _VwmaMiddle;
        private Aindicator _VwmaSlow;

        // The last value of the indicators
        private decimal _lastVwmaFast;
        private decimal _lastVwmaMiddle;
        private decimal _lastVwmaSlow;

        public IntersectionOfTheThreeVwma(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodVwmaFast = CreateParameter("Period VWMA Fast", 100, 10, 300, 1, "Indicator");
            PeriodVwmaMiddle = CreateParameter("Period VWMA Middle", 200, 10, 300, 1, "Indicator");
            PeriodVwmaSlow = CreateParameter("Period VWMA Slow", 300, 10, 300, 1, "Indicator");

            // Create indicator VWMAFast
            _VwmaFast = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VWMAFast", false);
            _VwmaFast = (Aindicator)_tab.CreateCandleIndicator(_VwmaFast, "Prime");
            _VwmaFast.DataSeries[0].Color = System.Drawing.Color.Blue;
            ((IndicatorParameterInt)_VwmaFast.Parameters[0]).ValueInt = PeriodVwmaFast.ValueInt;
            _VwmaFast.Save();

            // Create indicator VWMAMiddle
            _VwmaMiddle = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VWMAMiddle", false);
            _VwmaMiddle = (Aindicator)_tab.CreateCandleIndicator(_VwmaMiddle, "Prime");
            _VwmaMiddle.DataSeries[0].Color = System.Drawing.Color.Pink;
            ((IndicatorParameterInt)_VwmaMiddle.Parameters[0]).ValueInt = PeriodVwmaMiddle.ValueInt;
            _VwmaMiddle.Save();

            // Create indicator VWMASlow
            _VwmaSlow = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VWMASlow", false);
            _VwmaSlow = (Aindicator)_tab.CreateCandleIndicator(_VwmaSlow, "Prime");
            _VwmaSlow.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_VwmaSlow.Parameters[0]).ValueInt = PeriodVwmaSlow.ValueInt;
            _VwmaSlow.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfTheThreeVwma_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on intersection of three Vwma " +
                "Buy: Fast Vwma above average Vwma and medium above slow. " +
                "Sell: Fast Vwma below average Vwma and medium below slow. " +
                "Exit: on the opposite signal.";
        }

        // Indicator Update event
        private void IntersectionOfTheThreeVwma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_VwmaFast.Parameters[0]).ValueInt = PeriodVwmaFast.ValueInt;
            _VwmaFast.Save();
            _VwmaFast.Reload();
            ((IndicatorParameterInt)_VwmaMiddle.Parameters[0]).ValueInt = PeriodVwmaMiddle.ValueInt;
            _VwmaMiddle.Save();
            _VwmaMiddle.Reload();
            ((IndicatorParameterInt)_VwmaSlow.Parameters[0]).ValueInt = PeriodVwmaSlow.ValueInt;
            _VwmaSlow.Save();
            _VwmaSlow.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionOfTheThreeVwma";
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
            if (candles.Count < PeriodVwmaFast.ValueInt
                || candles.Count < PeriodVwmaMiddle.ValueInt
                || candles.Count < PeriodVwmaSlow.ValueInt)
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
                _lastVwmaFast = _VwmaFast.DataSeries[0].Last;
                _lastVwmaMiddle = _VwmaMiddle.DataSeries[0].Last;
                _lastVwmaSlow = _VwmaSlow.DataSeries[0].Last;

                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastVwmaFast > _lastVwmaMiddle && _lastVwmaMiddle > _lastVwmaSlow)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastVwmaFast < _lastVwmaMiddle && _lastVwmaMiddle < _lastVwmaSlow)
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

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                // The last value of the indicators
                _lastVwmaFast = _VwmaFast.DataSeries[0].Last;
                _lastVwmaMiddle = _VwmaMiddle.DataSeries[0].Last;
                _lastVwmaSlow = _VwmaSlow.DataSeries[0].Last;

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastVwmaFast < _lastVwmaMiddle && _lastVwmaMiddle < _lastVwmaSlow)
                    {
                        decimal lastPrice = candles[candles.Count - 1].Close;
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastVwmaFast > _lastVwmaMiddle && _lastVwmaMiddle > _lastVwmaSlow)
                    {
                        decimal lastPrice = candles[candles.Count - 1].Close;
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

            // if the robot is running in the tester
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
