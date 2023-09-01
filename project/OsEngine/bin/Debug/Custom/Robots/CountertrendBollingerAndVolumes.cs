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

The Countertrend robot on Bollinger And Volumes.

Buy:
1. During the CandlesCountLow period, the candle's loy was below the lower Bollinger line, then the candle closed above the lower line.
2. During the same period, there was a maximum surge in volumes (Volume), as well as the values of the Eom and OBV indicators were minimal.
That is, when the price returned to the channel (closed above the lower Bollinger line), the Volume indicator should be below its highs, and
Eom and OBV should be above their lows for the CandlesCountLow period.

Sale:
1. During the CandlesCountHigh period, the high of the candle was above the upper Bollinger line, then the candle closed below the upper line.
2. During the same period, there was a maximum surge in volumes (Volume), as well as the values of the Eom and OBV indicators were maximum.
That is, when the price returned to the channel (closed below the upper Bollinger line), the Volume, Eom and OBV indicators should be below 
their highs for the CandlesCountHigh period.

Exit from a long position: The trailing stop is placed at the minimum for the period specified for the trailing stop and is transferred, (slides), to new price lows, also for the specified period.

Exit from the short position: The trailing stop is placed at the maximum for the period specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.

 */


namespace OsEngine.Robots.AO
{
    [Bot("CountertrendBollingerAndVolumes")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendBollingerAndVolumes : BotPanel
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
        private StrategyParameterInt BollingerLength;
        private StrategyParameterDecimal BollingerDeviation;
        private StrategyParameterInt LengthEom;
        private StrategyParameterInt CandlesCountLow;
        private StrategyParameterInt CandlesCountHigh;
        private StrategyParameterInt Period;

        // Indicator
        Aindicator _Bollinger;
        Aindicator _EOM;
        Aindicator _OBV;
        Aindicator _Volume;

        // Exit
        private StrategyParameterInt TrailCandlesLong;
        private StrategyParameterInt TrailCandlesShort;

        // The last value of the indicator
        private decimal _lastUpLine;
        private decimal _lastDownLine;
        private decimal _lastEOM;
        private decimal _lastOBV;
        private decimal _lastVolume;

        public CountertrendBollingerAndVolumes(string name, StartProgram startProgram) : base(name, startProgram)
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
            BollingerLength = CreateParameter("Bollinger Length", 21, 7, 48, 7, "Indicator");
            BollingerDeviation = CreateParameter("Bollinger Deviation", 1.0m, 1, 5, 0.1m, "Indicator");
            LengthEom = CreateParameter("Eom Length", 14, 7, 48, 7, "Indicator");
            CandlesCountLow = CreateParameter("Candles Count Low", 10, 10, 200, 10, "Indicator");
            CandlesCountHigh = CreateParameter("Candles Count High", 10, 10, 200, 10, "Indicator");
            Period = CreateParameter("Period", 10, 10, 200, 10, "Indicator");

            // Create indicator Bollinger
            _Bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _Bollinger = (Aindicator)_tab.CreateCandleIndicator(_Bollinger, "Prime");
            ((IndicatorParameterInt)_Bollinger.Parameters[0]).ValueInt = BollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_Bollinger.Parameters[1]).ValueDecimal = BollingerDeviation.ValueDecimal;
            _Bollinger.Save();

            // Create indicator EOM
            _EOM = IndicatorsFactory.CreateIndicatorByName("EaseOfMovement", name + "EaseOfMovement", false);
            _EOM = (Aindicator)_tab.CreateCandleIndicator(_EOM, "NewArea");
            ((IndicatorParameterInt)_EOM.Parameters[0]).ValueInt = LengthEom.ValueInt;
            _EOM.Save();

            // Create indicator OBV
            _OBV = IndicatorsFactory.CreateIndicatorByName("OBV", name + "OBV", false);
            _OBV = (Aindicator)_tab.CreateCandleIndicator(_OBV, "NewArea0");
            _OBV.Save();

            // Create indicator Volume
            _Volume = IndicatorsFactory.CreateIndicatorByName("Volume", name + "Volume", false);
            _Volume = (Aindicator)_tab.CreateCandleIndicator(_Volume, "NewArea1");
            _Volume.Save();

            // Exit
            TrailCandlesLong = CreateParameter("Trail Candles Long", 5, 5, 200, 5, "Exit");
            TrailCandlesShort = CreateParameter("Trail Candles Short", 5, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakChannelVwmaATR_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The Countertrend robot on Bollinger And Volumes. " +
                "Buy: " +
                "1. During the CandlesCountLow period, the candle's loy was below the lower Bollinger line, then the candle closed above the lower line. " +
                "2. During the same period, there was a maximum surge in volumes (Volume), as well as the values of the Eom and OBV indicators were minimal. " +
                "That is, when the price returned to the channel (closed above the lower Bollinger line), the Volume indicator should be below its highs, and " +
                "Eom and OBV should be above their lows for the CandlesCountLow period. " +
                "Sale: " +
                "1. During the CandlesCountHigh period, the high of the candle was above the upper Bollinger line, then the candle closed below the upper line. " +
                "2. During the same period, there was a maximum surge in volumes (Volume), as well as the values of the Eom and OBV indicators were maximum. " +
                "That is, when the price returned to the channel (closed below the upper Bollinger line), the Volume, Eom and OBV indicators should be below  " +
                "their highs for the CandlesCountHigh period. " +
                "Exit from a long position: The trailing stop is placed at the minimum for the period specified for the trailing stop and is transferred, (slides), to new price lows, also for the specified period. " +
                "Exit from the short position: The trailing stop is placed at the maximum for the period specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.";
        }

        private void BreakChannelVwmaATR_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Bollinger.Parameters[0]).ValueInt = BollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_Bollinger.Parameters[1]).ValueDecimal = BollingerDeviation.ValueDecimal;
            _Bollinger.Save();
            _Bollinger.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendBollingerAndVolumes";
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
            if (candles.Count < BollingerDeviation.ValueDecimal ||
                candles.Count < BollingerLength.ValueInt ||
                candles.Count < LengthEom.ValueInt ||
                candles.Count < Period.ValueInt + Period.ValueInt ||
                candles.Count < CandlesCountHigh.ValueInt ||
                candles.Count < CandlesCountLow.ValueInt)
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
            _lastUpLine = _Bollinger.DataSeries[0].Last;
            _lastDownLine = _Bollinger.DataSeries[1].Last;
            _lastEOM = _EOM.DataSeries[0].Last;
            _lastOBV = _OBV.DataSeries[0].Last;
            _lastVolume = _Volume.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> Volume = _Volume.DataSeries[0].Values;
                List<decimal> VolumeEOM = _EOM.DataSeries[0].Values;
                List<decimal> VolumeOBV = _OBV.DataSeries[0].Values;


                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    

                    if (AverageVolumeValue(Volume, Period.ValueInt) > AverageVolumeValueTwo(Volume, Period.ValueInt) &&
                        MaxValueOnPeriodInddicator(Volume, Period.ValueInt) > _lastVolume &&
                        MinValueOnPeriodInddicator(VolumeEOM, Period.ValueInt) < _lastEOM &&
                        MinValueOnPeriodInddicator(VolumeOBV, Period.ValueInt) < _lastOBV && 
                        lastPrice > _lastDownLine)
                    {
                        for(int i = 2; i <= CandlesCountLow.ValueInt; i++)
                        {
                            if (candles[candles.Count - i].Low > _Bollinger.DataSeries[1].Values[_Bollinger.DataSeries[1].Values.Count - i])
                            {
                                return;
                            }
                        }

                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (AverageVolumeValue(Volume, Period.ValueInt) > AverageVolumeValueTwo(Volume, Period.ValueInt) &&
                        MaxValueOnPeriodInddicator(Volume, Period.ValueInt) > _lastVolume &&
                        MaxValueOnPeriodInddicator(VolumeEOM, Period.ValueInt) > _lastEOM &&
                        MaxValueOnPeriodInddicator(VolumeOBV, Period.ValueInt) > _lastOBV &&
                        lastPrice < _lastUpLine)
                    {
                        for (int i = 2; i <= CandlesCountHigh.ValueInt; i++)
                        {
                            if (candles[candles.Count - i].High < _Bollinger.DataSeries[0].Values[_Bollinger.DataSeries[0].Values.Count - i])
                            {
                                return;
                            }
                        }

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

        private decimal MaxValueOnPeriodInddicator(List<decimal> Value, int period)
        {
            decimal max = 0;
            for (int i = 2; i <= period; i++)
            {
                if(max < Value[Value.Count - i])
                {
                    max = Value[Value.Count - i];
                }
            }
            return max;
        }

        private decimal MinValueOnPeriodInddicator(List<decimal> Value, int period)
        {
            decimal min = 99999;
            for (int i = 2; i <= period; i++)
            {
                if (min > Value[Value.Count - i])
                {
                    min = Value[Value.Count - i];
                }
            }
            return min;
        }

        private decimal AverageVolumeValue(List<decimal> Volume, int period)
        {
            decimal sum = 0;

            for (int i = 1; i <= period; i++)
            {
                sum += Volume[Volume.Count - i];
            }
            if (sum > 0)
            {
                return sum / period;
            }

            return 0;
        }

        private decimal AverageVolumeValueTwo(List<decimal> Volume, int period)
        {
            decimal sum = 0;

            for (int i = period; i <= period * 2; i++)
            {
                sum += Volume[Volume.Count - i];
            }
            if (sum > 0)
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
