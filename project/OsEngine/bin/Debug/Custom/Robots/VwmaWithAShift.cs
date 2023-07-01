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

The trend robot on intersection of two Vwma with a shift

Buy: Fast Vwma is higher than slow Vwma.

Sale: Fast Vwma is lower than slow Vwma.

Exit: stop and profit in % of the entry price.

*/

namespace OsEngine.Robots.Vwma
{
    [Bot("VwmaWithAShift")] // We create an attribute so that we don't write anything to the BotFactory
    public class VwmaWithAShift : BotPanel
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
        private StrategyParameterInt PeriodOvwmaSlow;
        private StrategyParameterInt Offset;

        // Indicator
        private Aindicator _VwmaFast;
        private Aindicator _OvwmaSlow;

        // The last value of the indicators
        private decimal _lastVwmaFast;
        private decimal _lastOvwmaSlow;

        // Exit
        private StrategyParameterDecimal StopValue;
        private StrategyParameterDecimal ProfitValue;

        public VwmaWithAShift(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodVwmaFast = CreateParameter("Period Vwma Fast", 100, 10, 300, 10, "indicator");
            PeriodOvwmaSlow = CreateParameter("Period Vwma Slow", 200, 10, 300, 10, "indicator");
            Offset = CreateParameter("Offset", 3, 1, 20, 1, "indicator");

            // Create indicator VwmaFast
            _VwmaFast = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VwmaFast", false);
            _VwmaFast = (Aindicator)_tab.CreateCandleIndicator(_VwmaFast, "Prime");
            ((IndicatorParameterInt)_VwmaFast.Parameters[0]).ValueInt = PeriodVwmaFast.ValueInt;
            _VwmaFast.Save();

            // Create indicator VwmaSlow
            _OvwmaSlow = IndicatorsFactory.CreateIndicatorByName("OffsetVwma", name + "OsmaSlow", false);
            _OvwmaSlow = (Aindicator)_tab.CreateCandleIndicator(_OvwmaSlow, "Prime");
            _OvwmaSlow.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_OvwmaSlow.Parameters[0]).ValueInt = PeriodOvwmaSlow.ValueInt;
            ((IndicatorParameterInt)_OvwmaSlow.Parameters[1]).ValueInt = Offset.ValueInt;
            _OvwmaSlow.Save();

            // Exit
            StopValue = CreateParameter("Stop percent", 0.5m, 1, 10, 1, "Exit settings");
            ProfitValue = CreateParameter("Profit percent", 0.5m, 1, 10, 1, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += VwmaWithAShift_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
        }

        // Indicator Update event
        private void VwmaWithAShift_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_VwmaFast.Parameters[0]).ValueInt = PeriodVwmaFast.ValueInt;
            _VwmaFast.Save();
            _VwmaFast.Reload();
            ((IndicatorParameterInt)_OvwmaSlow.Parameters[0]).ValueInt = PeriodOvwmaSlow.ValueInt;
            ((IndicatorParameterInt)_OvwmaSlow.Parameters[1]).ValueInt = Offset.ValueInt;
            _OvwmaSlow.Save();
            _OvwmaSlow.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "VwmaWithAShift";
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
            if (candles.Count < PeriodVwmaFast.ValueInt || candles.Count < PeriodOvwmaSlow.ValueInt)
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
                _lastOvwmaSlow = _OvwmaSlow.DataSeries[0].Last;

                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastVwmaFast > _lastOvwmaSlow)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastVwmaFast < _lastOvwmaSlow)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestAsk - _slippage);
                    }
                }
                return;
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

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal profitActivation = pos.EntryPrice + pos.EntryPrice * ProfitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice - pos.EntryPrice * StopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal profitActivation = pos.EntryPrice - pos.EntryPrice * ProfitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice + pos.EntryPrice * StopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
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
