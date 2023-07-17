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

The trend robot on intersection of two VWMA

Buy: Fast Sma is higher than slow Vwma

Sell: Fast Sma below slow Vwma

Exit: on the opposite signal

*/

namespace OsEngine.Robots.Vwma
{
    [Bot("IntersectionOfTheTwoVwma")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionOfTheTwoVwma : BotPanel
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
        private StrategyParameterInt PeriodVWMAFast;
        private StrategyParameterInt PeriodVWMASlow;

        // Indicator
        private Aindicator _VWMAFast;
        private Aindicator _VWMASlow;

        // The last value of the indicators
        private decimal _lastVWMAFast;
        private decimal _lastVWMASlow;
        public IntersectionOfTheTwoVwma(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodVWMAFast = CreateParameter("Period SMA Fast", 100, 10, 300, 10, "Indicator");
            PeriodVWMASlow = CreateParameter("Period SMA Slow", 200, 10, 300, 10, "Indicator");

            // Create indicator SmaFast
            _VWMAFast = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VWMAFast", false);
            _VWMAFast = (Aindicator)_tab.CreateCandleIndicator(_VWMAFast, "Prime");
            ((IndicatorParameterInt)_VWMAFast.Parameters[0]).ValueInt = PeriodVWMAFast.ValueInt;
            _VWMAFast.Save();

            // Create indicator SmaSlow
            _VWMASlow = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VWMASlow", false);
            _VWMASlow = (Aindicator)_tab.CreateCandleIndicator(_VWMASlow, "Prime");
            _VWMASlow.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_VWMASlow.Parameters[0]).ValueInt = PeriodVWMASlow.ValueInt;
            _VWMASlow.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfTheTwoVwma_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "trading robot for osengine " +
                "The trend robot on intersection of two VWMA " +
                "Buy: Fast Sma is higher than slow Vwma " +
                "Sell: Fast Sma below slow Vwma " +
                "Exit: on the opposite signal";
        }

        // Indicator Update event
        private void IntersectionOfTheTwoVwma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_VWMAFast.Parameters[0]).ValueInt = PeriodVWMAFast.ValueInt;
            _VWMAFast.Save();
            _VWMAFast.Reload();
            ((IndicatorParameterInt)_VWMASlow.Parameters[0]).ValueInt = PeriodVWMASlow.ValueInt;
            _VWMASlow.Save();
            _VWMASlow.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionOfTheTwoVwma";
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
            if (candles.Count < PeriodVWMAFast.ValueInt || candles.Count < PeriodVWMASlow.ValueInt)
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
                _lastVWMAFast = _VWMAFast.DataSeries[0].Last;
                _lastVWMASlow = _VWMASlow.DataSeries[0].Last;

                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastVWMAFast > _lastVWMASlow)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastVWMAFast < _lastVWMASlow)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestAsk - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            // The last value of the indicators
            _lastVWMAFast = _VWMAFast.DataSeries[0].Last;
            _lastVWMASlow = _VWMASlow.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastVWMAFast < _lastVWMASlow)
                    {
                        decimal lastPrice = candles[candles.Count - 1].Close;
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }

                }
                else // If the direction of the position is sale
                {
                    if (_lastVWMAFast > _lastVWMASlow)
                    {
                        decimal lastPrice = candles[candles.Count - 1].Close;
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
