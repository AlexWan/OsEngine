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

The trend robot on Strategy ZigZag With MACD And CCI.

Buy:
1. ZigZag growing.
2. MACD histogram > 0.
3. CCI > 100.
Sell:
1. ZigZag falling.
2. MACD histogram < 0.
3. CCI < -100.
Exit from buy: The trailing stop is placed at the minimum for the period specified for the trailing 
stop and transferred (slides) to new price lows, also for the specified period.
Exit from sell: The trailing stop is placed at the maximum for the period specified for the trailing 
stop and is transferred (slides) to the new maximum of the price, also for the specified period.

 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyZigZagWithMACDAndCCI")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyZigZagWithMACDAndCCI : BotPanel
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
        private StrategyParameterInt PeriodZigZag;
        private StrategyParameterInt FastLineLengthMACD;
        private StrategyParameterInt SlowLineLengthMACD;
        private StrategyParameterInt SignalLineLengthMACD;
        private StrategyParameterInt PeriodCCI;

        // Indicator
        Aindicator _ZigZag;
        Aindicator _MACD;
        Aindicator _CCI;

        // Exit
        private StrategyParameterInt TrailCandlesLong;
        private StrategyParameterInt TrailCandlesShort;

        // The last value of the indicator
        private decimal _lastZZ;
        private decimal _lastCCI;
        private decimal _lastMACD;

        // The prev value of the indicator
        private decimal _prevZZ;

        public StrategyZigZagWithMACDAndCCI(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodZigZag = CreateParameter("Period ZigZag", 10, 10, 300, 10, "Indicator");
            FastLineLengthMACD = CreateParameter("MACD Fast Length", 16, 10, 300, 7, "Indicator");
            SlowLineLengthMACD = CreateParameter("MACD Slow Length", 32, 10, 300, 10, "Indicator");
            SignalLineLengthMACD = CreateParameter("MACD Signal Length", 8, 10, 300, 10, "Indicator");
            PeriodCCI = CreateParameter("Period CCI", 14, 10, 300, 1, "Indicator");

            // Create indicator CCI
            _CCI = IndicatorsFactory.CreateIndicatorByName("CCI", name + "CCI", false);
            _CCI = (Aindicator)_tab.CreateCandleIndicator(_CCI, "NewArea");
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = PeriodCCI.ValueInt;
            _CCI.Save();

            // Create indicator MACD
            _MACD = IndicatorsFactory.CreateIndicatorByName("MACD", name + "MACD", false);
            _MACD = (Aindicator)_tab.CreateCandleIndicator(_MACD, "NewArea0");
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = FastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = SlowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = SignalLineLengthMACD.ValueInt;
            _MACD.Save();

            // Create indicator ZigZag
            _ZigZag = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _ZigZag = (Aindicator)_tab.CreateCandleIndicator(_ZigZag, "Prime");
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZag.Save();

            // Exit
            TrailCandlesLong = CreateParameter("Trail Candles Long", 5, 5, 200, 5, "Exit");
            TrailCandlesShort = CreateParameter("Trail Candles Short", 5, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyZigZagWithMACDAndCCI_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy ZigZag With MACD And CCI. " +
                "Buy: " +
                "1. ZigZag growing. " +
                "2. MACD histogram > 0. " +
                "3. CCI > 100. " +
                "Sell: " +
                "1. ZigZag falling. " +
                "2. MACD histogram < 0. " +
                "3. CCI < -100. " +
                "Exit from buy: The trailing stop is placed at the minimum for the period specified for the trailing  " +
                "stop and transferred (slides) to new price lows, also for the specified period. " +
                "Exit from sell: The trailing stop is placed at the maximum for the period specified for the trailing  " +
                "stop and is transferred (slides) to the new maximum of the price, also for the specified period.";
        }

        private void StrategyZigZagWithMACDAndCCI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZag.Save();
            _ZigZag.Reload();
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = FastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = SlowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = SignalLineLengthMACD.ValueInt;
            _MACD.Save();
            _MACD.Reload();
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = PeriodCCI.ValueInt;
            _CCI.Save();
            _CCI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyZigZagWithMACDAndCCI";
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
            if (candles.Count < PeriodZigZag.ValueInt)
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
            _lastMACD = _MACD.DataSeries[0].Last;
            _lastCCI = _CCI.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            List<decimal> zzHigh = _ZigZag.DataSeries[2].Values;
            List<decimal> zzLow = _ZigZag.DataSeries[3].Values;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastMACD > 0 && _lastCCI > 100 && ZigZagDirection(zzLow, zzHigh) == true)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastMACD < 0 && _lastCCI < -100 && ZigZagDirection(zzLow, zzHigh) == false)
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

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(position, price, price - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal price = GetPriceStop(Side.Sell, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(position, price, price + _slippage);
                }

            }
        }
        private decimal GetPriceStop(Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < TrailCandlesLong.ValueInt || index < TrailCandlesShort.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                decimal price = decimal.MaxValue;

                for (int i = index; i > index - TrailCandlesLong.ValueInt; i--)
                {
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }
                return price;
            }

            if (side == Side.Sell)
            {
                decimal price = 0;

                for (int i = index; i > index - TrailCandlesShort.ValueInt; i--)
                {
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }
            return 0;
        }

        // Method zigzag direction
        private bool ZigZagDirection(List<decimal> zzLow, List<decimal> zzHigh)
        {
            int indexLow = 0;

            int indexHigh = 0;

            for (int i = zzLow.Count - 1; i >= 0; i--)
            {
                if (zzLow[i] != 0)
                {
                    indexLow = i;
                    break;
                }
            }

            for (int i = zzHigh.Count - 1; i >= 0; i--)
            {
                if (zzHigh[i] != 0)
                {
                    indexHigh = i;
                    break;
                }
            }

            if (indexLow > indexHigh)
            {
                return true;
            }

            return false;
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
