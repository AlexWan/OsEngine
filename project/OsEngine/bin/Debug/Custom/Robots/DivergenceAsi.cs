using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Indicators.indicator;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on strategy Devergence Asi.

Buy: The lows on the chart are falling, while the lows are rising on the indicator.

Sell: the highs on the chart are rising, while the indicator is falling.

Exit from buy:
We set the stop to the minimum for the period specified for the stop, 
and the profit is equal to the size of the stop multiplied by the coefficient from the parameters.

Exit from sell:
We set the stop to the maximum for the period specified for the stop, 
and the profit is equal to the size of the stop multiplied by the coefficient from the parameters.
 
 */

namespace OsEngine.Robots.MyBots
{
    [Bot("DivergenceAsi")] // We create an attribute so that we don't write anything to the BotFactory
    internal class DivergenceAsi : BotPanel
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
        private StrategyParameterDecimal AsiLimit;

        // Indicator
        Aindicator _ZigZag;
        Aindicator _ZigZagAsi;

        // Exit
        private StrategyParameterDecimal CoefProfit;
        private StrategyParameterInt StopCandles;

        public DivergenceAsi(string name, StartProgram startProgram) : base(name, startProgram)
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
            AsiLimit = CreateParameter("Asi Limit", 10000.0m, 100, 100000m, 1000m, "Indicator");

            // Create indicator ZigZag
            _ZigZag = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _ZigZag = (Aindicator)_tab.CreateCandleIndicator(_ZigZag, "Prime");
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZag.Save();

            // Create indicator ZigZag MFI
            _ZigZagAsi = IndicatorsFactory.CreateIndicatorByName("ZigZagAsi", name + "ZigZagAsi", false);
            _ZigZagAsi = (Aindicator)_tab.CreateCandleIndicator(_ZigZagAsi, "ZZAsiArea");
            ((IndicatorParameterDecimal)_ZigZagAsi.Parameters[0]).ValueDecimal = AsiLimit.ValueDecimal;
            ((IndicatorParameterInt)_ZigZagAsi.Parameters[1]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZagAsi.Save();

            // Exit
            CoefProfit = CreateParameter("Coef Profit", 1, 1m, 10, 1, "Exit settings");
            StopCandles = CreateParameter("Stop Candles", 20, 2, 10, 1, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += DivergenceAsi_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on strategy Devergence Asi. " +
                "Buy: The lows on the chart are falling, while the lows are rising on the indicator. " +
                "Sell: the highs on the chart are rising, while the indicator is falling. " +
                "Exit from buy: We set the stop to the minimum for the period specified for the stop, " +
                "and the profit is equal to the size of the stop multiplied by the coefficient from the parameters." +
                "Exit from sell: We set the stop to the maximum for the period specified for the stop, " +
                "and the profit is equal to the size of the stop multiplied by the coefficient from the parameters. ";
        }

        private void DivergenceAsi_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZag.Save();
            _ZigZag.Reload();

            ((IndicatorParameterDecimal)_ZigZagAsi.Parameters[0]).ValueDecimal = AsiLimit.ValueDecimal;
            ((IndicatorParameterInt)_ZigZagAsi.Parameters[1]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZagAsi.Save();
            _ZigZagAsi.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "DivergenceAsi";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }

        // Logic
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
            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> zzHigh = _ZigZag.DataSeries[2].Values;
                List<decimal> zzLow = _ZigZag.DataSeries[3].Values;

                List<decimal> zzAsiLow = _ZigZagAsi.DataSeries[4].Values;
                List<decimal> zzAsiHigh = _ZigZagAsi.DataSeries[3].Values;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (DevirgenceBuy(zzLow, zzAsiLow, zzAsiHigh))
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (DevirgenceSell(zzHigh, zzAsiHigh, zzAsiLow))
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        //  Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal profitActivation;
            decimal price;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {

                    decimal stopActivation = GetPriceStop(openPositions[i].TimeCreate, Side.Buy, candles, candles.Count - 1);

                    if (stopActivation == 0)
                    {
                        return;
                    }

                    price = stopActivation;
                    profitActivation = pos.EntryPrice + (pos.EntryPrice - price) * CoefProfit.ValueDecimal;
                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);

                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal stopActivation = GetPriceStop(openPositions[i].TimeCreate, Side.Sell, candles, candles.Count - 1);

                    if (stopActivation == 0)
                    {
                        return;
                    }

                    price = stopActivation;
                    profitActivation = pos.EntryPrice - (price - pos.EntryPrice) * CoefProfit.ValueDecimal;
                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);

                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
                }
            }
        }

        private decimal GetPriceStop(DateTime positionCreateTime, Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < StopCandles.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                // We calculate the stop price at Long
                // We find the minimum for the time from the opening of the transaction to the current one
                decimal price = decimal.MaxValue; ;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 2].TimeStart;
                }

                for (int i = index; i > 0; i--)
                {
                    // Look at the index of the candle, after which the opening of the pose occurred
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - StopCandles.ValueInt; i--)
                {
                    // Looking at the minimum after opening
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }

                return price;
            }

            if (side == Side.Sell)
            {
                //  We find the maximum for the time from the opening of the transaction to the current one
                decimal price = 0;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 1].TimeStart;
                }

                for (int i = index; i > 0; i--)
                {
                    // Look at the index of the candle, after which the opening of the pose occurred
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - StopCandles.ValueInt; i--)
                {
                    // Looking at the maximum high
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }

            return 0;
        }

        // Method for finding divergence
        private bool DevirgenceBuy(List<decimal> zzLow, List<decimal> zzAsiLow, List<decimal> zzAsiHigh)
        {
            decimal zzLowOne = 0;
            decimal zzLowTwo = 0;
            decimal zzAsiLowOne = 0;
            decimal zzAsiLowTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexHigh = 0;

            for (int i = zzAsiHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzAsiHigh[i] != 0)
                {
                    cnt++;
                    indexHigh = i;
                }

                if (cnt == 1)
                {
                    break;
                }
            }

            for (int i = zzLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzLow[i] != 0 && zzLowOne == 0)
                {
                    zzLowOne = zzLow[i];
                    cnt++;
                    indexOne = i;
                }

                if (zzLow[i] != 0 && indexOne != i && zzLowTwo == 0)
                {
                    zzLowTwo = zzLow[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            for (int i = zzAsiLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzAsiLow[i] != 0 && zzAsiLowOne == 0)
                {
                    zzAsiLowOne = zzAsiLow[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzAsiLow[i] != 0 && indexTwo != i && zzAsiLowTwo == 0)
                {
                    zzAsiLowTwo = zzAsiLow[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            decimal cntLow = 0;
            if (zzLowOne < zzLowTwo && zzLowOne != 0 && indexTwo < indexHigh)
            {
                cntLow++;
            }
            if (zzAsiLowOne > zzAsiLowTwo && zzAsiLowOne != 0)
            {
                cntLow++;
            }

            if (cntLow == 2)
            {
                return true;
            }

            return false;
        }

        // Method for finding divergence
        private bool DevirgenceSell(List<decimal> zzHigh, List<decimal> zzAsiHigh, List<decimal> zzAsiLow)
        {
            decimal zzHighOne = 0;
            decimal zzHighTwo = 0;
            decimal zzAsiHighOne = 0;
            decimal zzAsiHighTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexLow = 0;

            for (int i = zzAsiLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzAsiLow[i] != 0)
                {
                    cnt++;
                    indexLow = i;
                }

                if (cnt == 1)
                {
                    break;
                }
            }

            for (int i = zzHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzHigh[i] != 0 && zzHighOne == 0)
                {
                    zzHighOne = zzHigh[i];
                    cnt++;
                    indexOne = i;
                }

                if (zzHigh[i] != 0 && indexOne != i && zzHighTwo == 0)
                {
                    zzHighTwo = zzHigh[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            for (int i = zzAsiHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzAsiHigh[i] != 0 && zzAsiHighOne == 0)
                {
                    zzAsiHighOne = zzAsiHigh[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzAsiHigh[i] != 0 && indexTwo != i && zzAsiHighTwo == 0)
                {
                    zzAsiHighTwo = zzAsiHigh[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            decimal cntHigh = 0;
            if (zzHighOne > zzHighTwo && zzHighTwo != 0 && indexTwo < indexLow)
            {
                cntHigh++;
            }
            if (zzAsiHighOne < zzAsiHighTwo && zzAsiHighOne != 0)
            {
                cntHigh++;
            }

            if (cntHigh == 2)
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
