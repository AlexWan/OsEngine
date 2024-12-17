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

The Counter trend robot on Ema with ATR

Buy:
1. The volume is above the VolumeValue.
2. Candle falling.
3. The price is below Ema.
4. The value of Atr is higher than the average value for a certain period (CandlesCountAtr) by MultAtr times.

Sell:
1. The volume is above the VolumeValue.
2. Candle growing.
3. The price is higher than Ema.
4. The value of Atr is higher than the average value for a certain period (CandlesCountAtr) by MultAtr times.

Exit from buy: Trailing stop is placed at the minimum for the period specified for the
trailing stop and is transferred (sliding) to new price lows, also for the specified period.
Exit from sell: Trailing stop is placed on the maximum for the period specified for
the trailing stop and is transferred (sliding) to a new price maximum, also for the specified period.

 */


namespace OsEngine.Robots.AO
{
    [Bot("CountertrendEmaATR")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendEmaATR : BotPanel
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
        private StrategyParameterInt PeriodEma;
        private StrategyParameterInt CandlesCountAtr;
        private StrategyParameterInt VolumeValue;
        private StrategyParameterInt LengthAtr;
        private StrategyParameterDecimal MultAtr;

        // Indicator
        Aindicator _Volume;
        Aindicator _Ema;
        Aindicator _ATR;

        // The last value of the indicator
        private decimal _lastATR;
        private decimal _lastEma;
        private decimal _lastVolume;

        // The prev value of the indicator
        private decimal _prevVolume;

        // Exit
        private StrategyParameterInt TrailCandlesLong;
        private StrategyParameterInt TrailCandlesShort;

        public CountertrendEmaATR(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodEma = CreateParameter("Period Ema", 50, 10, 300, 10,"Indicator");
            CandlesCountAtr = CreateParameter("Candles Count Atr", 21, 7, 48, 7, "Indicator");
            LengthAtr = CreateParameter("Length ATR", 14, 7, 48, 7, "Indicator");
            MultAtr = CreateParameter("Mult ATR", 1.1m, 1.1m, 10, 0.1m, "Indicator");
            VolumeValue = CreateParameter("Volume Value", 1000, 100, 10000, 100, "Indicator");

            // Create indicator Volume
            _Volume = IndicatorsFactory.CreateIndicatorByName("Volume", name + "Vol", false);
            _Volume = (Aindicator)_tab.CreateCandleIndicator(_Volume, "NewArea0");
            _Volume.Save();

            // Create indicator Ema
            _Ema = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema", false);
            _Ema = (Aindicator)_tab.CreateCandleIndicator(_Ema, "Prime");
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = PeriodEma.ValueInt;
            _Ema.Save();

            // Create indicator ATR
            _ATR = IndicatorsFactory.CreateIndicatorByName("ATR", name + "Atr", false);
            _ATR = (Aindicator)_tab.CreateCandleIndicator(_ATR, "NewArea1");
            ((IndicatorParameterInt)_ATR.Parameters[0]).ValueInt = LengthAtr.ValueInt;
            _ATR.Save();

            // Exit
            TrailCandlesLong = CreateParameter("Trail Candles Long", 5, 5, 200, 5, "Exit");
            TrailCandlesShort = CreateParameter("Trail Candles Short", 5, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendEmaATR_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The Counter trend robot on Ema with ATR. " +
                "Buy: " +
                "1. The volume is above the VolumeValue. " +
                "2. Candle falling. " +
                "3. The price is below Ema. " +
                "4. The value of Atr is higher than the average value for a certain period (CandlesCountAtr) by MultAtr times. " +
                "Sell: " +
                "1. The volume is above the VolumeValue. " +
                "2. Candle growing. " +
                "3. The price is higher than Ema. " +
                "4. The value of Atr is higher than the average value for a certain period (CandlesCountAtr) by MultAtr times. " +
                "Exit from buy: Trailing stop is placed at the minimum for the period specified for the " +
                "trailing stop and is transferred (sliding) to new price lows, also for the specified period. " +
                "Exit from sell: Trailing stop is placed on the maximum for the period specified for " +
                "the trailing stop and is transferred (sliding) to a new price maximum, also for the specified period.";
        }

        private void CountertrendEmaATR_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = PeriodEma.ValueInt;
            _Ema.Save();
            _Ema.Reload();
            ((IndicatorParameterInt)_ATR.Parameters[0]).ValueInt = LengthAtr.ValueInt;
            _ATR.Save();
            _ATR.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendEmaATR";
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
            if (candles.Count < LengthAtr.ValueInt ||
                candles.Count < PeriodEma.ValueInt ||
                candles.Count < CandlesCountAtr.ValueInt)
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
            _lastEma = _Ema.DataSeries[0].Last;
            _lastATR = _ATR.DataSeries[0].Last;
            _lastVolume = _Volume.DataSeries[0].Last;

            // The prev value of the indicator
            _prevVolume = _Volume.DataSeries[0].Values[_Volume.DataSeries[0].Values.Count - 2];

            List<decimal> Volume = _ATR.DataSeries[0].Values;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (VolumeValue.ValueInt < _lastVolume && 
                        candles[candles.Count - 1].IsDown && 
                        _lastEma > lastPrice &&
                        _lastATR > AverageVolumeValue(Volume,CandlesCountAtr.ValueInt) * MultAtr.ValueDecimal)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (VolumeValue.ValueInt < _lastVolume &&
                        candles[candles.Count - 1].IsUp &&
                        _lastEma < lastPrice &&
                        _lastATR > AverageVolumeValue(Volume, CandlesCountAtr.ValueInt) * MultAtr.ValueDecimal)
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

        private decimal AverageVolumeValue(List<decimal> Volume, int period)
        {
            decimal sum = 0;

            for(int i = 1; i <= period; i++)
            {
                sum += Volume[Volume.Count - i];
            }
            if(sum > 0)
            {
                return sum / period;
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
