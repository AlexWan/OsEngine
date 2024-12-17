using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Trading robot for osengine.

Trend robot on the Momentum breakdown with SMA .

Buy:
1. The value of the Momentum indicator broke through the maximum
for a certain number of candles and closed higher.
2. The price is higher than Sma.

Sell: 
1. The value of the Momentum indicator broke through the minimum
for a certain number of candles and closed lower.
2. The price is lower than Sma.

Exit from the buy: 
Trailing sop by Sma + Multivashov * Ivashov.

Exit from the sell:
Trailing sop by Sma - Multivashov * Ivashov.
 */

namespace OsEngine.Robots.My_bots
{
    [Bot("BreakMomentumWithSmaTrading")]
    public class BreakMomentumWithSmaTrading : BotPanel // We create an attribute so that we don't write anything to the BotFactory
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
        private StrategyParameterInt MomentumLength;
        private StrategyParameterInt LengthSma;
        private StrategyParameterInt LengthMAIvashov;
        private StrategyParameterInt LengthRangeIvashov;
        private StrategyParameterDecimal MultIvashov;
        
        // Indicator
        Aindicator _Momentum;
        Aindicator _Sma;
        Aindicator _RangeIvashov;

        // The last value of the indicator
      
        private decimal _lastSma;
        private decimal _lastRangeIvashov;

        // Exit
        private StrategyParameterInt TrailCandlesLong;
        private StrategyParameterInt TrailCandlesShort;

        public BreakMomentumWithSmaTrading(string name, StartProgram startProgram) : base(name, startProgram)
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
            MomentumLength = CreateParameter("Momentum Length", 10, 5, 100, 5, "Indicator");
            LengthMAIvashov = CreateParameter("Length MA Ivashov",14 , 7, 48, 7, "Indicator");
            LengthRangeIvashov = CreateParameter("Length Range Ivashov", 14, 7, 48, 7, "Indicator");
            MultIvashov = CreateParameter("Mult Ivashov", 0.5m, 0.1m, 2, 0.1m, "Indicator");
            LengthSma = CreateParameter("Length Sma", 100, 10, 300, 10, "Indicator");

            // Create indicator Sma
            _Sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _Sma = (Aindicator)_tab.CreateCandleIndicator(_Sma, "Prime");
            ((IndicatorParameterInt)_Sma.Parameters[0]).ValueInt = LengthSma.ValueInt;
            _Sma.Save();


            // Create indicator Momentum
            _Momentum = IndicatorsFactory.CreateIndicatorByName("Momentum", name + "Momentum Length", false);
            _Momentum = (Aindicator)_tab.CreateCandleIndicator(_Momentum, "NewArea0");
            ((IndicatorParameterInt)_Momentum.Parameters[0]).ValueInt = MomentumLength.ValueInt;
            _Momentum.Save();

            // Create indicator Ivashov Range
            _RangeIvashov = IndicatorsFactory.CreateIndicatorByName("IvashovRange", name + "Range Ivashov", false);
            _RangeIvashov = (Aindicator)_tab.CreateCandleIndicator(_RangeIvashov, "NewArea1");
            ((IndicatorParameterInt)_RangeIvashov.Parameters[0]).ValueInt = LengthMAIvashov.ValueInt;
            ((IndicatorParameterInt)_RangeIvashov.Parameters[1]).ValueInt = LengthRangeIvashov.ValueInt;
            _RangeIvashov.Save();

            // Exit
            TrailCandlesLong = CreateParameter("Stop Value Long", 5, 10, 500, 10, "Exit");
            TrailCandlesShort = CreateParameter("Stop Value Short", 1, 15, 200, 5, "Exit");
           
            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakMomentumWithSmaTrading_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on the Momentum breakdown with SMA." +
                "Buy:" +
                "1.The value of the Momentum indicator broke through the maximum" +
                "for a certain number of candles and closed higher." +
                "2.The price is higher than Sma." +
                "Sell: " +
                "1.The value of the Momentum indicator broke through the minimum" +
                "for a certain number of candles and closed lower." +
                "2.The price is lower than Sma." +
                "Exit from the buy: " +
                "Trailing sop by Sma + Multivashov * Ivashov." +
                "Exit from the sell:" +
                "Trailing sop by Sma - Multivashov * Ivashov. ";
        }

        private void BreakMomentumWithSmaTrading_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Momentum.Parameters[0]).ValueInt = LengthMAIvashov.ValueInt;
            _Momentum.Save();
            _Momentum.Reload();

            ((IndicatorParameterInt)_Sma.Parameters[0]).ValueInt = LengthSma.ValueInt;
            _Sma.Save();
            _Sma.Reload();

            ((IndicatorParameterInt)_RangeIvashov.Parameters[0]).ValueInt = LengthMAIvashov.ValueInt;
            ((IndicatorParameterInt)_RangeIvashov.Parameters[1]).ValueInt = LengthRangeIvashov.ValueInt;
            _RangeIvashov.Save();
            _RangeIvashov.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakMomentumWithSmaTrading";
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
            if (candles.Count < MomentumLength.ValueInt || candles.Count < LengthRangeIvashov.ValueInt ||
                candles.Count < LengthSma.ValueInt || candles.Count < LengthMAIvashov.ValueInt
                || candles.Count < MultIvashov.ValueDecimal || candles.Count < TrailCandlesLong.ValueInt + 2
                || candles.Count < TrailCandlesShort.ValueInt + 2)
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

        private void LogicOpenPosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            _lastSma = _Sma.DataSeries[0].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> values = _Momentum.DataSeries[0].Values;               

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {

                  
                    if ( EnterLongAndShort(values, TrailCandlesLong.ValueInt)=="true" && lastPrice > _lastSma) 
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (EnterLongAndShort(values, TrailCandlesShort.ValueInt) == "false" && lastPrice < _lastSma)
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
            if (candles == null || index < LengthSma.ValueInt || index < LengthSma.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                decimal price = decimal.MaxValue;

                for (int i = index; i > index - LengthSma.ValueInt; i--)
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

                for (int i = index; i > index - LengthSma.ValueInt; i--)
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
        private string EnterLongAndShort(List<decimal> values, int period)

        {
            if(values.Count==period)
            {
                return "false";
            }
                int l = 0;
                decimal Max = -9999999;
                decimal Min = 9999999;
                for (int i = 1; i <= period; i++)
                {
               

                    if (values[values.Count - 1 - i] > Max)
                    {
                        Max = values[values.Count - 1 - i];
                    }
                    if (values[values.Count - 1 - i] < Min)
                    {
                        Min = values[values.Count - 1 - i];
                    }
                    l = i;
                }
                if (Max < values[values.Count - 1])
                {
                    return "true";
                }
                else if (Min > values[values.Count - 1])
                {
                    return "false";
                }
                return "nope";
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
