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
using OsEngine.Logging;

/* Description
trading robot for osengine

The trend robot on Strategy Two Ema And CCI

Buy:
1. fast Ema is higher than slow Ema.
2. CCI crossed level 0 from below.

Sell:
1. fast Ema is lower than slow Ema.
2. CCI crossed level 0 from above.

Buy Exit: Fast Ema Below Slow Ema.

Sell Exit: Fast Ema Above Slow Ema.

 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyTwoEmaAndCCI")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyTwoEmaAndCCI : BotPanel
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
        private StrategyParameterInt LengthEmaFast;
        private StrategyParameterInt LengthEmaSlow;
        private StrategyParameterInt LengthCCI;

        // Indicator
        Aindicator _EmaFast;
        Aindicator _EmaSlow;
        Aindicator _CCI;

        // The last value of the indicator
        private decimal _lastFastEma;
        private decimal _lastSlowEma;
        private decimal _lastCCI;

        // The prev value of the indicator
        private decimal _prevCCI;

        public StrategyTwoEmaAndCCI(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthCCI = CreateParameter("CCI Length", 21, 7, 48, 7, "Indicator");
            LengthEmaFast = CreateParameter("LengthEmaFast", 10, 10, 300, 10, "Indicator");
            LengthEmaSlow = CreateParameter("LengthEmaSlow", 10, 10, 300, 10, "Indicator");

            // Create indicator EmaFast
            _EmaFast = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaFast", false);
            _EmaFast = (Aindicator)_tab.CreateCandleIndicator(_EmaFast, "Prime");
            ((IndicatorParameterInt)_EmaFast.Parameters[0]).ValueInt = LengthEmaFast.ValueInt;
            _EmaFast.Save();

            // Create indicator EmaSlow
            _EmaSlow = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaSlow", false);
            _EmaSlow = (Aindicator)_tab.CreateCandleIndicator(_EmaSlow, "Prime");
            ((IndicatorParameterInt)_EmaSlow.Parameters[0]).ValueInt = LengthEmaSlow.ValueInt;
            _EmaSlow.DataSeries[0].Color = Color.White;
            _EmaSlow.Save();

            // Create indicator CCI
            _CCI = IndicatorsFactory.CreateIndicatorByName("CCI", name + "CCI", false);
            _CCI = (Aindicator)_tab.CreateCandleIndicator(_CCI, "NewArea");
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = LengthCCI.ValueInt;
            _CCI.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyTwoEmaAndCCI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Two Ema And CCI. " +
                "Buy: " +
                "1. fast Ema is higher than slow Ema. " +
                "2. CCI crossed level 0 from below. " +
                "Sell: " +
                "1. fast Ema is lower than slow Ema. " +
                "2. CCI crossed level 0 from above. " +
                "Buy Exit: Fast Ema Below Slow Ema. " +
                "Sell Exit: Fast Ema Above Slow Ema.";
        }

        private void StrategyTwoEmaAndCCI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = LengthCCI.ValueInt;
            _CCI.Save();
            _CCI.Reload();
            ((IndicatorParameterInt)_EmaFast.Parameters[0]).ValueInt = LengthEmaFast.ValueInt;
            _EmaFast.Save();
            _EmaFast.Reload();
            ((IndicatorParameterInt)_EmaSlow.Parameters[0]).ValueInt = LengthEmaSlow.ValueInt;
            _EmaSlow.Save();
            _EmaSlow.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyTwoEmaAndCCI";
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
            if (candles.Count < LengthEmaFast.ValueInt ||
                candles.Count < LengthCCI.ValueInt ||
                candles.Count < LengthEmaSlow.ValueInt)
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
            _lastCCI = _CCI.DataSeries[0].Last;
            _prevCCI = _CCI.DataSeries[0].Values[_CCI.DataSeries[0].Values.Count - 2];
            _lastFastEma = _EmaFast.DataSeries[0].Last;
            _lastSlowEma = _EmaSlow.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastFastEma > _lastSlowEma && _prevCCI < 0 && _lastCCI > 0)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastFastEma < _lastSlowEma && _prevCCI > 0 && _lastCCI < 0)
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
            _lastFastEma = _EmaFast.DataSeries[0].Last;
            _lastSlowEma = _EmaSlow.DataSeries[0].Last;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastFastEma < _lastSlowEma)
                    {
                        _tab.CloseAtLimit(position, lastPrice - _slippage, position.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastFastEma > _lastSlowEma)
                    {
                        _tab.CloseAtLimit(position, lastPrice + _slippage, position.OpenVolume);
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

