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
using System.Windows.Media.Animation;

/* Description
trading robot for osengine

The trend robot on strategy Rsi with ADX.

Buy:
 1. RSI enters the overbought zone - rises above the 70th level;
 2. Adx > 30 and growing.
Sell:
 1. The RSI enters the oversold zone - falls below the 30th level;
 2. Adx > 30 and growing.

Exit from buy: The trailing stop is placed at the minimum
for the period specified for the trailing stop and transferred (slides) to new price lows, also for the specified period.

Exit from sell: The trailing stop is placed at the maximum
for the period specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.

 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyRsiAndADX")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyRsiAndADX : BotPanel
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
        private StrategyParameterInt PeriodRSI;
        private StrategyParameterInt PeriodADX;

        // Indicator
        Aindicator _RSI;
        Aindicator _ADX;

        // The last value of the indicator
        private decimal _lastRSI;
        private decimal _lastADX;

        // Exit
        private StrategyParameterInt TrailingCandles;

        public StrategyRsiAndADX(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodRSI = CreateParameter("Period RSI", 14, 10, 300, 10, "Indicator");
            PeriodADX = CreateParameter("Period ADX", 10, 10, 300, 10, "Indicator");

            // Create indicator RSI
            _RSI = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _RSI = (Aindicator)_tab.CreateCandleIndicator(_RSI, "NewArea");
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = PeriodRSI.ValueInt;
            _RSI.Save();

            // Create indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea0");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();

            // Exit
            TrailingCandles = CreateParameter("TrailingCandles", 1, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyEmaADX_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on strategy Rsi with ADX. " +
                "Buy: " +
                " 1. RSI enters the overbought zone - rises above the 70th level; " +
                " 2. Adx > 30 and growing. " +
                "Sell: " +
                " 1. The RSI enters the oversold zone - falls below the 30th level; " +
                " 2. Adx > 30 and growing. " +
                "Exit from buy: The trailing stop is placed at the minimum " +
                "for the period specified for the trailing stop and transferred (slides) to new price lows, also for the specified period. " +
                "Exit from sell: The trailing stop is placed at the maximum " +
                "for the period specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.";
        }

        private void StrategyEmaADX_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = PeriodRSI.ValueInt;
            _RSI.Save();
            _RSI.Reload();
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyRsiAndADX";
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
            if (candles.Count < PeriodADX.ValueInt || candles.Count < PeriodRSI.ValueInt)
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
            _lastRSI = _RSI.DataSeries[0].Last;
            _lastADX = _ADX.DataSeries[0].Last;

            decimal prevCandel = candles[candles.Count - 2].Low;
            decimal lastCandel = candles[candles.Count - 1].Low;
            decimal lastHigh = candles[candles.Count - 1].High;

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
                    if (_lastRSI > 70 && _lastADX > 30)
                    {
                        _tab.BuyAtLimit(GetVolume(), lastHigh + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastRSI < 30 && _lastADX > 30)
                    {
                        _tab.SellAtLimit(GetVolume(), lastCandel - _slippage);
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
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (positions.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(openPositions[0], price, price - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal price = GetPriceStop(Side.Sell, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(openPositions[0], price, price + _slippage);
                }

            }
        }

        private decimal GetPriceStop(Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < TrailingCandles.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                decimal price = decimal.MaxValue;

                for (int i = index; i > index - TrailingCandles.ValueInt; i--)
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

                for (int i = index; i > index - TrailingCandles.ValueInt; i--)
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