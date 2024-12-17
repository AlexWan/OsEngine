using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Trading robot for osengine.

The trend robot on Envelopes and VWMA channel.

Buy: 
The lower line of Envelopes is above the upper line of the Vwma channel.

Sell:
The upper line Envelopes below the lower line of the Vwma channel.

Exit from the buy: 
The trailing stop is placed at the minimum for the period specified for the trailing stop and is transferred
(slides), over the new price minimums, also for the specified period - IvashovRange*MuItIvashov.

Exit from the sell:
The trailing stop is placed at the maximum for the period specified for the trailing stop and is transferred (slides), 
to the new maximum of the price, also for the specified period + IvashovRange*MuItIvashov.

 */

namespace OsEngine.Robots.My_bots
{
    [Bot("IntersectionOfEnvelopesAndVWMAСhannel")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionOfEnvelopesAndVWMAСhannel : BotPanel
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
        private StrategyParameterInt EnvelopesLength;
        private StrategyParameterDecimal EnvelopesDeviation;
        private StrategyParameterInt LengthVwmaChannel;
        private StrategyParameterInt LengthMAIvashov;
        private StrategyParameterInt LengthRangeIvashov;
        private StrategyParameterDecimal MultIvashov;

        // Indicator
        Aindicator _VwmaHigh;
        Aindicator _VwmaLow;
        Aindicator _Envelop;
        Aindicator _RangeIvashov;

        // The last value of the indicator
        private decimal _lastUpLine;
        private decimal _lastDownLine;
        private decimal _lastVwmaHigh;
        private decimal _lastVwmaLow;
        private decimal _lastRangeIvashov;
       

        // Exit
        private StrategyParameterInt TrailCandlesLong;
        private StrategyParameterInt TrailCandlesShort;

        public IntersectionOfEnvelopesAndVWMAСhannel(string name, StartProgram startProgram) : base(name, startProgram)
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
            EnvelopesLength = CreateParameter("Envelopes Length", 21, 7, 48, 7, "Indicator");
            EnvelopesDeviation = CreateParameter("Envelopes Deviation", 0.5m, 0.1m, 2, 0.1m, "Indicator");
            LengthVwmaChannel = CreateParameter("Period Vwma", 21, 7, 48, 7, "Indicator");
            LengthMAIvashov = CreateParameter("Length MA Ivashov", 14, 7, 48, 7, "Indicator");
            LengthRangeIvashov = CreateParameter("Length Range Ivashov", 14, 7, 48, 7, "Indicator");
            MultIvashov = CreateParameter("Mult Ivashov", 0.5m, 0.1m, 2, 0.1m, "Indicator");

            // Create indicator Envelop
            _Envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _Envelop = (Aindicator)_tab.CreateCandleIndicator(_Envelop, "Prime");
            ((IndicatorParameterInt)_Envelop.Parameters[0]).ValueInt = EnvelopesLength.ValueInt;
            ((IndicatorParameterDecimal)_Envelop.Parameters[1]).ValueDecimal = EnvelopesDeviation.ValueDecimal;
            _Envelop.Save();

            // Create indicator VwmaHigh
            _VwmaHigh = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "Vwma High", false);
            _VwmaHigh = (Aindicator)_tab.CreateCandleIndicator(_VwmaHigh, "Prime");
            ((IndicatorParameterInt)_VwmaHigh.Parameters[0]).ValueInt = LengthVwmaChannel.ValueInt;
            ((IndicatorParameterString)_VwmaHigh.Parameters[1]).ValueString = "High";
            _VwmaHigh.Save();

            // Create indicator VwmaLow
            _VwmaLow = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "Vwma Low", false);
            _VwmaLow = (Aindicator)_tab.CreateCandleIndicator(_VwmaLow, "Prime");
            ((IndicatorParameterInt)_VwmaLow.Parameters[0]).ValueInt = LengthVwmaChannel.ValueInt;
            ((IndicatorParameterString)_VwmaLow.Parameters[1]).ValueString = "Low";
            _VwmaLow.Save();

            // Create indicator Ivashov
            _RangeIvashov = IndicatorsFactory.CreateIndicatorByName("IvashovRange", name + "Range Ivashov", false);
            _RangeIvashov = (Aindicator)_tab.CreateCandleIndicator(_RangeIvashov, "NewArea");
            ((IndicatorParameterInt)_RangeIvashov.Parameters[0]).ValueInt = LengthMAIvashov.ValueInt;
            ((IndicatorParameterInt)_RangeIvashov.Parameters[1]).ValueInt = LengthRangeIvashov.ValueInt;
            _RangeIvashov.Save();

            // Exit
            TrailCandlesLong = CreateParameter("Stop Value Long", 5, 10, 500, 10, "Exit");
            TrailCandlesShort = CreateParameter("Stop Value Short", 1, 15, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfEnvelopesAndVWMAСhannel_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Envelopes and VWMA channel." +
                "Buy:" +
                " The lower line of Envelopes is above the upper line of the Vwma channel." +
                "Sell:" +
                "The upper line Envelopes below the lower line of the Vwma channel." +
                "Exit from the buy: " +
                "The trailing stop is placed at the minimum for the period specified for the trailing stop and is transferred" +
                "(slides), over the new price minimums, also for the specified period - IvashovRange * MuItIvashov." +
                "Exit from the sell:" +
                "The trailing stop is placed at the maximum for the period specified for the trailing stop and is transferred(slides)," +
                "to the new maximum of the price, also for the specified period + IvashovRange * MuItIvashov.";
        }

        private void IntersectionOfEnvelopesAndVWMAСhannel_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Envelop.Parameters[0]).ValueInt = EnvelopesLength.ValueInt;
            ((IndicatorParameterDecimal)_Envelop.Parameters[1]).ValueDecimal = EnvelopesDeviation.ValueDecimal;
            _Envelop.Save();
            _Envelop.Reload();
           
            ((IndicatorParameterInt)_VwmaHigh.Parameters[0]).ValueInt = LengthVwmaChannel.ValueInt;
            _VwmaHigh.Save();
            _VwmaHigh.Reload();
           
            ((IndicatorParameterInt)_VwmaLow.Parameters[0]).ValueInt = LengthVwmaChannel.ValueInt;
            _VwmaLow.Save();
            _VwmaLow.Reload();

            ((IndicatorParameterInt)_RangeIvashov.Parameters[0]).ValueInt = LengthMAIvashov.ValueInt;
            ((IndicatorParameterInt)_RangeIvashov.Parameters[1]).ValueInt = LengthRangeIvashov.ValueInt;
            _RangeIvashov.Save();
            _RangeIvashov.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionOfEnvelopesAndVWMAСhannel";
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
            if (candles.Count < LengthRangeIvashov.ValueInt ||
                candles.Count < LengthVwmaChannel.ValueInt ||
                candles.Count < EnvelopesLength.ValueInt)
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
            _lastVwmaHigh = _VwmaHigh.DataSeries[0].Last;
            _lastVwmaLow = _VwmaLow.DataSeries[0].Last;
            _lastUpLine = _Envelop.DataSeries[0].Last;
            _lastDownLine = _Envelop.DataSeries[2].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastDownLine > _lastVwmaHigh )
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastUpLine < _lastVwmaLow)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            _lastRangeIvashov = _RangeIvashov.DataSeries[0].Last;
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
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1) - _lastRangeIvashov * MultIvashov.ValueDecimal;
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(position, price, price - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal price = GetPriceStop(Side.Sell, candles, candles.Count - 1) + _lastRangeIvashov * MultIvashov.ValueDecimal; 
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
