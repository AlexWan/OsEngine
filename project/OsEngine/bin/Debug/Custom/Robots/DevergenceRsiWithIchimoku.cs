using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;

/* Description
trading robot for osengine

The trend robot on Devergence Rsi with Ichimoku.

Buy:
1. The lows on the chart are decreasing, and the Rsi indicator is growing.
2. The Tenkan line crosses the Kijun line from bottom to top.
Sell:
1. The highs on the chart are rising, and on the Rsi indicator they are decreasing.
2. The Tenkan line crosses the Kijun line from top to bottom.
Exit from the buy:
1. Stop for a minimum of a certain number of candles.
2. Profit – for the maximum for a certain number of candles.
Exit from sell:
1. Stop for the maximum for a certain number of candles.
2. Profit – for a minimum of a certain number of candles.
 
 */


namespace OsEngine.Robots.AO
{
    [Bot("DevergenceRsiWithIchimoku")] // We create an attribute so that we don't write anything to the BotFactory
    public class DevergenceRsiWithIchimoku : BotPanel
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
        private StrategyParameterInt TenkanLength;
        private StrategyParameterInt KijunLength;
        private StrategyParameterInt SenkouLength;
        private StrategyParameterInt ChinkouLength;
        private StrategyParameterInt Offset;
        private StrategyParameterInt PeriodZigZag;
        private StrategyParameterInt PeriodRsi;

        // Indicator
        Aindicator _Ichomoku;
        Aindicator _ZigZag;
        Aindicator _ZigZagRsi;

        // The last value of the indicator
        private decimal _lastSenkouA;
        private decimal _lastSenkouB;

        // The prev value of the indicator
        private decimal _prevSenkouA;
        private decimal _prevSenkouB;

        // Exit
        private StrategyParameterInt StopCandles;
        private StrategyParameterInt ProfitCandles;

        // Counter
        decimal Cnt;

        public DevergenceRsiWithIchimoku(string name, StartProgram startProgram) : base(name, startProgram)
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
            TenkanLength = CreateParameter("Tenkan Length", 9, 1, 50, 3, "Indicator");
            KijunLength = CreateParameter("Kijun Length", 26, 1, 50, 4, "Indicator");
            SenkouLength = CreateParameter("Senkou Length", 52, 1, 100, 8, "Indicator");
            ChinkouLength = CreateParameter("Chinkou Length", 26, 1, 50, 4, "Indicator");
            Offset = CreateParameter("Offset", 26, 1, 50, 4, "Indicator");
            PeriodZigZag = CreateParameter("Period ZigZag", 10, 10, 300, 10, "Indicator");
            PeriodRsi = CreateParameter("Period CCI", 10, 10, 300, 10, "Indicator");

            // Create indicator _Ichomoku
            _Ichomoku = IndicatorsFactory.CreateIndicatorByName("Ichimoku", name + "Ichimoku", false);
            _Ichomoku = (Aindicator)_tab.CreateCandleIndicator(_Ichomoku, "Prime");
            ((IndicatorParameterInt)_Ichomoku.Parameters[0]).ValueInt = TenkanLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[1]).ValueInt = KijunLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[2]).ValueInt = SenkouLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[3]).ValueInt = ChinkouLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[4]).ValueInt = Offset.ValueInt;
            _Ichomoku.Save();

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
            StopCandles = CreateParameter("Stop Candel", 1, 5, 200, 5, "Exit");
            ProfitCandles = CreateParameter("Profit Candel", 1, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += DevergenceRsiWithIchimoku_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Devergence Rsi with Ichimoku. " +
                "Buy: " +
                "1. The lows on the chart are decreasing, and the Rsi indicator is growing. " +
                "2. The Tenkan line crosses the Kijun line from bottom to top. " +
                "Sell: " +
                "1. The highs on the chart are rising, and on the Rsi indicator they are decreasing. " +
                "2. The Tenkan line crosses the Kijun line from top to bottom. " +
                "Exit from the buy: " +
                "1. Stop for a minimum of a certain number of candles. " +
                "2. Profit – for the maximum for a certain number of candles. " +
                "Exit from sell: " +
                "1. Stop for the maximum for a certain number of candles. " +
                "2. Profit – for a minimum of a certain number of candles.";
        }

        private void DevergenceRsiWithIchimoku_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Ichomoku.Parameters[0]).ValueInt = TenkanLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[1]).ValueInt = KijunLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[2]).ValueInt = SenkouLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[3]).ValueInt = ChinkouLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[4]).ValueInt = Offset.ValueInt;
            _Ichomoku.Save();
            _Ichomoku.Reload();
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
            return "DevergenceRsiWithIchimoku";
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
            if (candles.Count < TenkanLength.ValueInt ||
                candles.Count < KijunLength.ValueInt ||
                candles.Count < SenkouLength.ValueInt ||
                candles.Count < ChinkouLength.ValueInt ||
                candles.Count < Offset.ValueInt ||
                candles.Count < StopCandles.ValueInt ||
                candles.Count < ProfitCandles.ValueInt ||
                candles.Count < PeriodZigZag.ValueInt ||
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
                Cnt = 0;
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicator
            _lastSenkouA = _Ichomoku.DataSeries[3].Last;
            _lastSenkouB = _Ichomoku.DataSeries[4].Last;

            // The prev value of the indicator
            _prevSenkouA = _Ichomoku.DataSeries[3].Values[_Ichomoku.DataSeries[3].Values.Count - 2];
            _prevSenkouB = _Ichomoku.DataSeries[4].Values[_Ichomoku.DataSeries[4].Values.Count - 2];

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
                    if (_prevSenkouA < _prevSenkouB && _lastSenkouA > _lastSenkouB && DevirgenceBuy(zzLow, zzRsiLow, zzRsiHigh) == true)
                    {
                        _tab.BuyAtLimit(GetVolume(), lastPrice + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_prevSenkouA > _prevSenkouB && _lastSenkouA < _lastSenkouB && DevirgenceSell(zzHigh, zzRsiHigh, zzRsiLow) == true)
                    {
                        _tab.SellAtLimit(GetVolume(), lastPrice - _slippage);
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
                Position pos = openPositions[i];

                if (Cnt == 1)
                {
                    return;
                }

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    _tab.CloseAtProfit(pos, MaxPrice(candles, ProfitCandles.ValueInt), MaxPrice(candles, ProfitCandles.ValueInt) + _slippage);
                    _tab.CloseAtStop(pos, MinPrice(candles,StopCandles.ValueInt), MinPrice(candles, StopCandles.ValueInt) - _slippage);
                    Cnt = 1;
                }
                else // If the direction of the position is sale
                {
                    _tab.CloseAtProfit(pos, MinPrice(candles, ProfitCandles.ValueInt), MinPrice(candles, ProfitCandles.ValueInt) - _slippage);
                    _tab.CloseAtStop(pos, MaxPrice(candles, StopCandles.ValueInt), MaxPrice(candles, StopCandles.ValueInt) + _slippage);
                    Cnt = 1;
                }

            }
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