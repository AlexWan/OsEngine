using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
trading robot for osengine

The Countertrend robot on Envelopes, CCI and ER.

Buy:
1. During the CandlesCountLow period, the candle's loy was below the lower Envelopes line, then the candle closed above the lower line.
 2. During the same period there was a maximum of Er, then it began to fall.
 3. During the same period, the CCI value was above +100, then it began to fall.
Sell:
 1. During the CandlesCountHigh period, the high of the candle was above the upper line of the Envelopes, then the candle closed below the upper line.
 2. During the same period there was a maximum of Er, then it began to fall.
 3. During the same period, the CCI value was below -100, then it grows.

Exit from buy: trailing stop in % of the Low candle on which you entered.
Exit from sell: trailing stop in % of the High of the candle on which you entered.

 */


namespace OsEngine.Robots.CMO
{
    [Bot("CountertrendEnvelopesCCIandER")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendEnvelopesCCIandER : BotPanel
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
        private StrategyParameterInt LengthCCI;
        private StrategyParameterInt EnvelopLength;
        private StrategyParameterDecimal EnvelopesDeviation;
        private StrategyParameterInt LengthER;
        private StrategyParameterInt CandlesCountLow;
        private StrategyParameterInt CandlesCountHigh;

        // Indicator
        Aindicator _CCI;
        Aindicator _Envelopes;
        Aindicator _ER;

        // Exit
        private StrategyParameterInt TrailingValue;

        // The last value of the indicator
        private decimal _lastCCI;
        private decimal _lastEnvelopUp;
        private decimal _lastEnvelopDown;
        private decimal _lastER;

        public CountertrendEnvelopesCCIandER(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthCCI = CreateParameter("CCI Length", 14, 7, 48, 7, "Indicator");
            EnvelopLength = CreateParameter("Envelop Length", 10, 10, 300, 10, "Indicator");
            EnvelopesDeviation = CreateParameter("Envelopes Deviation", 3.0m, 1, 5, 0.1m, "Indicator");
            LengthER = CreateParameter("LengthER", 20, 10, 300, 10, "Indicator");
            CandlesCountLow = CreateParameter("Candles Count Low", 10, 10, 200, 10, "Indicator");
            CandlesCountHigh = CreateParameter("Candles Count High", 10, 10, 200, 10, "Indicator");

            // Create indicator Envelopes
            _Envelopes = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _Envelopes = (Aindicator)_tab.CreateCandleIndicator(_Envelopes, "Prime");
            ((IndicatorParameterInt)_Envelopes.Parameters[0]).ValueInt = EnvelopLength.ValueInt;
            ((IndicatorParameterDecimal)_Envelopes.Parameters[1]).ValueDecimal = EnvelopesDeviation.ValueDecimal;
            _Envelopes.Save();

            // Create indicator CCI
            _CCI = IndicatorsFactory.CreateIndicatorByName("CCI", name + "CCI", false);
            _CCI = (Aindicator)_tab.CreateCandleIndicator(_CCI, "NewArea");
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = LengthCCI.ValueInt;
            _CCI.Save();

            // Create indicator EfficiencyRatio
            _ER = IndicatorsFactory.CreateIndicatorByName("EfficiencyRatio", name + "EfficiencyRatio", false);
            _ER = (Aindicator)_tab.CreateCandleIndicator(_ER, "NewArea0");
            ((IndicatorParameterInt)_ER.Parameters[0]).ValueInt = LengthER.ValueInt;
            _ER.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendEnvelopesCCIandER_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The Countertrend robot on Envelopes, CCI and ER. " +
                "Buy: " +
                "1. During the CandlesCountLow period, the candle's loy was below the lower Envelopes line, then the candle closed above the lower line. " +
                "2. During the same period there was a maximum of Er, then it began to fall. " +
                "3. During the same period, the CCI value was above +100, then it began to fall. " +
                "Sell: " +
                "1. During the CandlesCountHigh period, the high of the candle was above the upper line of the Envelopes, then the candle closed below the upper line. " +
                "2. During the same period there was a maximum of Er, then it began to fall. " +
                "3. During the same period, the CCI value was below -100, then it grows. " +
                "Exit from buy: trailing stop in % of the Low candle on which you entered. " +
                "Exit from sell: trailing stop in % of the High of the candle on which you entered.";
        }

        private void CountertrendEnvelopesCCIandER_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = LengthCCI.ValueInt;
            _CCI.Save();
            _CCI.Reload();
            ((IndicatorParameterInt)_Envelopes.Parameters[0]).ValueInt = EnvelopLength.ValueInt;
            ((IndicatorParameterDecimal)_Envelopes.Parameters[1]).ValueDecimal = EnvelopesDeviation.ValueDecimal;
            _Envelopes.Save();
            _Envelopes.Reload();
            ((IndicatorParameterInt)_ER.Parameters[0]).ValueInt = LengthER.ValueInt;
            _ER.Save();
            _ER.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendEnvelopesCCIandER";
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
            if (candles.Count < LengthCCI.ValueInt ||
                candles.Count < LengthER.ValueInt ||
                candles.Count < EnvelopLength.ValueInt ||
                candles.Count < CandlesCountLow.ValueInt + 3||
                candles.Count < CandlesCountHigh.ValueInt + 3)
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
            _lastEnvelopUp = _Envelopes.DataSeries[0].Last;
            _lastEnvelopDown = _Envelopes.DataSeries[2].Last;
            _lastER = _ER.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPriceLow = candles[candles.Count - 1].Low;
                decimal lastPriceHigh = candles[candles.Count - 1].High;

                List<decimal> VolumeER = _ER.DataSeries[0].Values;
                List<decimal> VolumeCCI = _CCI.DataSeries[0].Values;

                List<decimal> ValueUp = _Envelopes.DataSeries[0].Values;
                List<decimal> ValueDown = _Envelopes.DataSeries[2].Values;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (MaxValueOnPeriodInddicator(VolumeER,CandlesCountLow.ValueInt) > _lastER &&
                        MaxValueOnPeriodInddicator(VolumeCCI,CandlesCountLow.ValueInt) > 100 &&
                        _lastCCI < 100 && lastPriceLow > _lastEnvelopDown && EnterSellAndBuy(Side.Buy, candles,ValueDown) == true)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (MaxValueOnPeriodInddicator(VolumeER,CandlesCountHigh.ValueInt) > _lastER && 
                        MinValueOnPeriodInddicator(VolumeCCI, CandlesCountHigh.ValueInt) < -100 &&
                        _lastCCI > -100 && lastPriceHigh < _lastEnvelopUp && EnterSellAndBuy(Side.Sell,candles,ValueUp) == true)
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
            Position pos = openPositions[0];

            decimal stopPrice;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * TrailingValue.ValueInt / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueInt / 100;
                }
                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);

            }
        }

        private decimal MaxValueOnPeriodInddicator(List<decimal> Value, int period)
        {
            decimal max = 0;
            for (int i = 2; i <= period; i++)
            {
                if (max < Value[Value.Count - i])
                {
                    max = Value[Value.Count - i];
                }
            }
            return max;
        }

        private bool EnterSellAndBuy(Side side, List<Candle> candles, List<decimal> Value)
        {
            if(side == Side.Buy)
            {
                for (int i = 2; i <= CandlesCountLow.ValueInt + 2; i++)
                {
                    if (candles[candles.Count - i].Low > _Envelopes.DataSeries[2].Values[_Envelopes.DataSeries[2].Values.Count - i])
                    {
                        return false;
                    }
                }
            }
            else
            {
                for (int i = 2; i <= CandlesCountHigh.ValueInt + 2; i++)
                {
                    if (candles[candles.Count - i].High < _Envelopes.DataSeries[0].Values[_Envelopes.DataSeries[0].Values.Count - i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private decimal MinValueOnPeriodInddicator(List<decimal> Value, int period)
        {
            decimal min = 999999;
            for (int i = 2; i <= period; i++)
            {
                if (min > Value[Value.Count - i])
                {
                    min = Value[Value.Count - i];
                }
            }
            return min;
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

