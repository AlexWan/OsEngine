using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

/* Description
trading robot for osengine

The trend robot on Devergence Rsi.

Buy:
1. The lows on the chart are decreasing, but on the indicator they are growing.
Sell:
1. The highs on the chart are rising, and on the indicator they are decreasing.

Exit: after a certain number of candles.
 */


namespace OsEngine.Robots.AO
{
    [Bot("DevergenceRsi")] // We create an attribute so that we don't write anything to the BotFactory
    public class DevergenceRsi : BotPanel
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
        private StrategyParameterInt PeriodRsi;

        // Indicator
        Aindicator _ZigZag;
        Aindicator _ZigZagRsi;

        // Exit 
        private StrategyParameterInt ExitCandles;

        public DevergenceRsi(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodRsi = CreateParameter("Period CCI", 10, 10, 300, 10, "Indicator");

            // Create indicator ZigZag
            _ZigZag = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _ZigZag = (Aindicator)_tab.CreateCandleIndicator(_ZigZag, "Prime");
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZag.Save();

            // Create indicator ZigZag Rsi
            _ZigZagRsi = IndicatorsFactory.CreateIndicatorByName("ZigZagRsi", name + "ZigZagRsi", false);
            _ZigZagRsi = (Aindicator)_tab.CreateCandleIndicator(_ZigZagRsi, "NewArea");
            ((IndicatorParameterInt)_ZigZagRsi.Parameters[0]).ValueInt = PeriodRsi.ValueInt;
            ((IndicatorParameterInt)_ZigZagRsi.Parameters[1]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZagRsi.Save();

            // Exit
            ExitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += DevergenceRsi_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Devergence Rsi. " +
                "Buy: " +
                "1. The lows on the chart are decreasing, but on the indicator they are growing. " +
                "Sell: " +
                "1. The highs on the chart are rising, and on the indicator they are decreasing. " +
                "Exit: after a certain number of candles.";
        }

        private void DevergenceRsi_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZag.Save();
            _ZigZag.Reload();
            ((IndicatorParameterInt)_ZigZagRsi.Parameters[0]).ValueInt = PeriodRsi.ValueInt;
            ((IndicatorParameterInt)_ZigZagRsi.Parameters[1]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZagRsi.Save();
            _ZigZagRsi.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "DevergenceRsi";
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
            if (candles.Count < PeriodZigZag.ValueInt ||
                candles.Count < PeriodRsi.ValueInt)
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

            List<decimal> zzHigh = _ZigZag.DataSeries[2].Values;
            List<decimal> zzLow = _ZigZag.DataSeries[3].Values;

            List<decimal> zzRsiLow = _ZigZagRsi.DataSeries[4].Values;
            List<decimal> zzRsiHigh = _ZigZagRsi.DataSeries[3].Values;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (DevirgenceBuy(zzLow, zzRsiLow, zzRsiHigh) == true)
                    {
                        var time = candles.Last().TimeStart;

                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (DevirgenceSell(zzHigh, zzRsiHigh, zzRsiLow) == true)
                    {
                        var time = candles.Last().TimeStart;

                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage, time.ToString());
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

                if (!NeedClosePosition(position, candles))
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    _tab.CloseAtLimit(position, lastPrice - _slippage, position.OpenVolume);
                }
                else // If the direction of the position is sale
                {
                    _tab.CloseAtLimit(position, lastPrice + _slippage, position.OpenVolume);
                }

            }
        }

        private bool NeedClosePosition(Position position, List<Candle> candles)
        {
            if (position == null || position.OpenVolume == 0)
            {
                return false;
            }

            DateTime openTime = DateTime.Parse(position.SignalTypeOpen);

            int counter = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                counter++;
                DateTime candelTime = candles[i].TimeStart;
                if (candelTime == openTime)
                {
                    if (counter >= ExitCandles.ValueInt + 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Method for finding divergence
        private bool DevirgenceBuy(List<decimal> zzLow, List<decimal> zzRsiLow, List<decimal> zzRsiHigh)
        {
            decimal zzLowOne = 0;
            decimal zzLowTwo = 0;
            decimal zzRsiLowOne = 0;
            decimal zzRsiLowTwo = 0;

            int indexOne = 0;

            int indexTwo = 0;

            int indexHigh = 0;

            for (int i = zzRsiHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzRsiHigh[i] != 0)
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

            for (int i = zzRsiLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;


                if (zzRsiLow[i] != 0 && zzRsiLowOne == 0)
                {
                    zzRsiLowOne = zzRsiLow[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzRsiLow[i] != 0 && indexTwo != i && zzRsiLowTwo == 0)
                {
                    zzRsiLowTwo = zzRsiLow[i];
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
            if (zzRsiLowOne > zzRsiLowTwo && zzRsiLowOne != 0)
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
        private bool DevirgenceSell(List<decimal> zzHigh, List<decimal> zzRsiHigh, List<decimal> zzRsiLow)
        {

            decimal zzHighOne = 0;
            decimal zzHighTwo = 0;
            decimal zzRsiHighOne = 0;
            decimal zzRsiHighTwo = 0;

            int indexOne = 0;

            int indexTwo = 0;

            int indexLow = 0;

            for (int i = zzRsiLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzRsiLow[i] != 0)
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

            for (int i = zzRsiHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;


                if (zzRsiHigh[i] != 0 && zzRsiHighOne == 0)
                {
                    zzRsiHighOne = zzRsiHigh[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzRsiHigh[i] != 0 && indexTwo != i && zzRsiHighTwo == 0)
                {
                    zzRsiHighTwo = zzRsiHigh[i];
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
            if (zzRsiHighOne < zzRsiHighTwo && zzRsiHighOne != 0)
            {
                cntHigh++;
            }

            if (cntHigh == 2)
            {
                return true;
            }

            return false;
        }

        private decimal MaxPrice(List<Candle> candles, int period)
        {
            decimal max = 0;
            for (int i = 1; i <= period; i++)
            {
                if (max < candles[candles.Count - i].Close)
                {
                    max = candles[candles.Count - i].Close;
                }
            }
            return max;
        }

        private decimal MinPrice(List<Candle> candles, int period)
        {
            decimal min = decimal.MaxValue;
            for (int i = 1; i <= period; i++)
            {
                if (min > candles[candles.Count - i].Close)
                {
                    min = candles[candles.Count - i].Close;
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