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

The trend robot on BreakLRTrailVolatility.

Buy: the price is above the upper LR line.

Sell: the price is below the lower LR line.

Exit from buy: The trailing stop is placed at the minimum –Atr * Er for the period specified for the
trailing stop and is transferred, (slides), to new price lows, also for the specified period.

Exit from sell: The trailing stop is placed at the maximum +Atr * Er for the period specified for the
trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.

 */


namespace OsEngine.Robots.CMO
{
    [Bot("BreakLRTrailVolatility")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakLRTrailVolatility : BotPanel
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
        private StrategyParameterInt LengthATR;
        private StrategyParameterInt LrLength;
        private StrategyParameterDecimal UpDeviation;
        private StrategyParameterDecimal DownDeviation;
        private StrategyParameterInt LengthER;

        // Indicator
        Aindicator _ATR;
        Aindicator _LR;
        Aindicator _ER;

        // Exit
        private StrategyParameterInt TrailCandlesLong;
        private StrategyParameterInt TrailCandlesShort;

        // The last value of the indicator
        private decimal _lastATR;
        private decimal _lastLrUp;
        private decimal _lastLrDown;
        private decimal _lastER;

        public BreakLRTrailVolatility(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthATR = CreateParameter("ATR Length", 14, 7, 48, 7, "Indicator");
            LrLength = CreateParameter("LR Length", 10, 10, 300, 10, "Indicator");
            UpDeviation = CreateParameter("Up Deviation", 3.0m, 1, 5, 0.1m, "Indicator");
            DownDeviation = CreateParameter("Down Deviation", 3.0m, 1, 5, 0.1m, "Indicator");
            LengthER = CreateParameter("LengthER", 20, 10, 300, 10, "Indicator");

            // Create indicator LR
            _LR = IndicatorsFactory.CreateIndicatorByName("LinearRegressionChannel", name + "LinearRegressionChannel", false);
            _LR = (Aindicator)_tab.CreateCandleIndicator(_LR, "Prime");
            ((IndicatorParameterInt)_LR.Parameters[0]).ValueInt = LrLength.ValueInt;
            ((IndicatorParameterDecimal)_LR.Parameters[2]).ValueDecimal = UpDeviation.ValueDecimal;
            ((IndicatorParameterDecimal)_LR.Parameters[3]).ValueDecimal = DownDeviation.ValueDecimal;
            _LR.Save();

            // Create indicator ATR
            _ATR = IndicatorsFactory.CreateIndicatorByName("ATR", name + "ATR", false);
            _ATR = (Aindicator)_tab.CreateCandleIndicator(_ATR, "NewArea");
            ((IndicatorParameterInt)_ATR.Parameters[0]).ValueInt = LengthATR.ValueInt;
            _ATR.Save();

            // Create indicator EfficiencyRatio
            _ER = IndicatorsFactory.CreateIndicatorByName("EfficiencyRatio", name + "EfficiencyRatio", false);
            _ER = (Aindicator)_tab.CreateCandleIndicator(_ER, "NewArea0");
            ((IndicatorParameterInt)_ER.Parameters[0]).ValueInt = LengthER.ValueInt;
            _ER.Save();

            // Exit
            TrailCandlesLong = CreateParameter("Trail Candles Long", 5, 5, 200, 5, "Exit");
            TrailCandlesShort = CreateParameter("Trail Candles Short", 5, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakLRTrailVolatility_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on BreakLRTrailVolatility. " +
                "Buy: the price is above the upper LR line. " +
                "Sell: the price is below the lower LR line. " +
                "Exit from buy: The trailing stop is placed at the minimum –Atr * Er for the period specified for the " +
                "trailing stop and is transferred, (slides), to new price lows, also for the specified period. " +
                "Exit from sell: The trailing stop is placed at the maximum +Atr * Er for the period specified for the " +
                "trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.";
        }

        private void BreakLRTrailVolatility_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ATR.Parameters[0]).ValueInt = LengthATR.ValueInt;
            _ATR.Save();
            _ATR.Reload();
            ((IndicatorParameterInt)_LR.Parameters[0]).ValueInt = LrLength.ValueInt;
            ((IndicatorParameterDecimal)_LR.Parameters[2]).ValueDecimal = UpDeviation.ValueDecimal;
            ((IndicatorParameterDecimal)_LR.Parameters[3]).ValueDecimal = DownDeviation.ValueDecimal;
            _LR.Save();
            _LR.Reload();
            ((IndicatorParameterInt)_ER.Parameters[0]).ValueInt = LengthER.ValueInt;
            _ER.Save();
            _ER.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakLRTrailVolatility";
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
            if (candles.Count < LengthATR.ValueInt ||
                candles.Count < LengthER.ValueInt ||
                candles.Count < LrLength.ValueInt)
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
            _lastATR = _ATR.DataSeries[0].Last;
            _lastLrUp = _LR.DataSeries[0].Last;
            _lastLrDown = _LR.DataSeries[2].Last;
            _lastER = _ER.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                List<decimal> VolumeER = _ER.DataSeries[0].Values;
                List<decimal> VolumeCCI = _ATR.DataSeries[0].Values;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if(_lastLrUp < lastPrice)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if(_lastLrDown > lastPrice)
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

            // The last value of the indicator
            _lastER = _ER.DataSeries[0].Last;
            _lastATR = _ATR.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1) - _lastATR * _lastER;
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(position, price, price - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal price = GetPriceStop(Side.Sell, candles, candles.Count - 1) + _lastATR * _lastER;
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

