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

The trend robot on intersection of SMA with OffsetSma

Buy: Fast Sma is higher than slow Sma

Sell: Fast Sma below slow Sma

Exit: on the opposite signal

*/

namespace OsEngine.Robots.SMA
{
    [Bot("SmaWithAShift")] // We create an attribute so that we don't write anything to the BotFactory
    public class SmaWithAShift : BotPanel
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
        private StrategyParameterInt PeriodSmaFast;
        private StrategyParameterInt PeriodOsmaSlow;
        private StrategyParameterInt Offset;

        // Indicator
        private Aindicator _SmaFast;
        private Aindicator _OsmaSlow;

        // The last value of the indicators
        private decimal _lastSmaFast;
        private decimal _lastOsmaSlow;
        public SmaWithAShift(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodSmaFast = CreateParameter("Period SMA Fast", 100, 10, 300, 10, "Indicator");
            PeriodOsmaSlow = CreateParameter("Period SMA Slow", 200, 10, 300, 10, "Indicator");
            Offset = CreateParameter("Offset", 3, 1, 20, 1, "Indicator");

            // Create indicator SmaFast
            _SmaFast = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaFast", false);
            _SmaFast = (Aindicator)_tab.CreateCandleIndicator(_SmaFast, "Prime");
            ((IndicatorParameterInt)_SmaFast.Parameters[0]).ValueInt = PeriodSmaFast.ValueInt;
            _SmaFast.Save();

            // Create indicator SmaSlow
            _OsmaSlow = IndicatorsFactory.CreateIndicatorByName("OffsetSma", name + "OsmaSlow", false);
            _OsmaSlow = (Aindicator)_tab.CreateCandleIndicator(_OsmaSlow, "Prime");
            _OsmaSlow.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_OsmaSlow.Parameters[0]).ValueInt = PeriodOsmaSlow.ValueInt;
            ((IndicatorParameterInt)_OsmaSlow.Parameters[1]).ValueInt = Offset.ValueInt;
            _OsmaSlow.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += SmaWithAShift_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
        }
        
        // Indicator Update event
        private void SmaWithAShift_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_SmaFast.Parameters[0]).ValueInt = PeriodSmaFast.ValueInt;
            _SmaFast.Save();
            _SmaFast.Reload();
            ((IndicatorParameterInt)_OsmaSlow.Parameters[0]).ValueInt = PeriodOsmaSlow.ValueInt;
            ((IndicatorParameterInt)_OsmaSlow.Parameters[1]).ValueInt = Offset.ValueInt;
            _OsmaSlow.Save();
            _OsmaSlow.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "SmaWithAShift";
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
            if (candles.Count < PeriodSmaFast.ValueInt || candles.Count < PeriodOsmaSlow.ValueInt)
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
                _lastSmaFast = _SmaFast.DataSeries[0].Last;
                _lastOsmaSlow = _OsmaSlow.DataSeries[0].Last;

                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastSmaFast > _lastOsmaSlow)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastSmaFast < _lastOsmaSlow)
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
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                // The last value of the indicators
                _lastSmaFast = _SmaFast.DataSeries[0].Last;
                _lastOsmaSlow = _OsmaSlow.DataSeries[0].Last;

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastSmaFast < _lastOsmaSlow)
                    {
                        decimal lastPrice = candles[candles.Count - 1].Close;
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }

                }
                else // If the direction of the position is sale
                {
                    if (_lastSmaFast > _lastOsmaSlow)
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
