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
using System.Security.Cryptography;
using System.Windows.Media.Animation;

/* Description
trading robot for osengine

The trend robot on strategy Devergence Ultimate.

Buy: The lows on the chart are falling, while the lows are rising on the indicator.

Sell: the highs on the chart are rising, while the indicator is falling.

Exit from buy: the oscillator rose above 50, and then fell below 45 or entered the overbought zone (above 70), and then began to fall.

Exit from sell: the oscillator rose above 65 or entered the oversold zone (below 30).
 
 */


namespace OsEngine.Robots.AO
{
    [Bot("DevergenceUltimate")] // We create an attribute so that we don't write anything to the BotFactory
    public class DevergenceUltimate : BotPanel
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
        private StrategyParameterInt PeriodOneUltimate;
        private StrategyParameterInt PeriodTwoUltimate;
        private StrategyParameterInt PeriodThreeUltimate;

        // Indicator
        Aindicator _ZigZag;
        Aindicator _ZigZagUltimate;

        // The last value of the indicator
        private decimal _lastUltimate;

        // The prev value of the indicator
        private decimal _prevUltimate;

        public DevergenceUltimate(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodOneUltimate = CreateParameter("PeriodOneUltimate", 7, 10, 300, 1, "Indicator");
            PeriodTwoUltimate = CreateParameter("PeriodTwoUltimate", 14, 10, 300, 1, "Indicator");
            PeriodThreeUltimate = CreateParameter("PeriodThreeUltimate", 28, 9, 300, 1, "Indicator");

            // Create indicator ZigZag
            _ZigZag = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _ZigZag = (Aindicator)_tab.CreateCandleIndicator(_ZigZag, "Prime");
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZag.Save();

            // Create indicator ZigZag Ultimate
            _ZigZagUltimate = IndicatorsFactory.CreateIndicatorByName("ZigZagUltimate", name + "ZigZagUltimate", false);
            _ZigZagUltimate = (Aindicator)_tab.CreateCandleIndicator(_ZigZagUltimate, "NewArea");
            ((IndicatorParameterInt)_ZigZagUltimate.Parameters[0]).ValueInt = PeriodOneUltimate.ValueInt;
            ((IndicatorParameterInt)_ZigZagUltimate.Parameters[1]).ValueInt = PeriodTwoUltimate.ValueInt;
            ((IndicatorParameterInt)_ZigZagUltimate.Parameters[2]).ValueInt = PeriodThreeUltimate.ValueInt;
            ((IndicatorParameterInt)_ZigZagUltimate.Parameters[3]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZagUltimate.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += DevergenceMACD_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on strategy Devergence Ultimate. " +
                "Buy: The lows on the chart are falling, while the lows are rising on the indicator. " +
                "Sell: the highs on the chart are rising, while the indicator is falling. " +
                "Exit from buy: the oscillator rose above 50, and then fell below 45 or entered the overbought zone (above 70), and then began to fall. " +
                "Exit from sell: the oscillator rose above 65 or entered the oversold zone (below 30).";
        }

        private void DevergenceMACD_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZag.Save();
            _ZigZag.Reload();
            ((IndicatorParameterInt)_ZigZagUltimate.Parameters[0]).ValueInt = PeriodOneUltimate.ValueInt;
            ((IndicatorParameterInt)_ZigZagUltimate.Parameters[1]).ValueInt = PeriodTwoUltimate.ValueInt;
            ((IndicatorParameterInt)_ZigZagUltimate.Parameters[2]).ValueInt = PeriodThreeUltimate.ValueInt;
            ((IndicatorParameterInt)_ZigZagUltimate.Parameters[3]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZagUltimate.Save();
            _ZigZagUltimate.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "DevergenceUltimate";
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
            if (candles.Count < PeriodOneUltimate.ValueInt || candles.Count < PeriodZigZag.ValueInt ||
                candles.Count < PeriodTwoUltimate.ValueInt || candles.Count < PeriodThreeUltimate.ValueInt)
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

                List<decimal> zzAOLow = _ZigZagUltimate.DataSeries[4].Values;
                List<decimal> zzAOHigh = _ZigZagUltimate.DataSeries[3].Values;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (DevirgenceBuy(zzLow, zzAOLow, zzAOHigh) == true)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (DevirgenceSell(zzHigh, zzAOHigh, zzAOLow) == true)
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

            // The last value of the indicator
            _lastUltimate = _ZigZagUltimate.DataSeries[0].Last;

            // The prev value of the indicator
            _prevUltimate = _ZigZagUltimate.DataSeries[0].Values[_ZigZagUltimate.DataSeries[0].Values.Count - 2];

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastUltimate < 45 && _prevUltimate > 50 || _prevUltimate > 70 && _lastUltimate < 70)
                    {

                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {

                    if (_lastUltimate > 65 || _lastUltimate < 30)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice + _slippage, openPositions[0].OpenVolume);
                    }
                }
            }
        }

        // Method for finding divergence
        private bool DevirgenceBuy(List<decimal> zzLow, List<decimal> zzMACDLow, List<decimal> zzMACDHigh)
        {
            decimal zzLowOne = 0;
            decimal zzLowTwo = 0;
            decimal zzMACDLowOne = 0;
            decimal zzMACDLowTwo = 0;

            int indexOne = 0;

            int indexTwo = 0;

            int indexHigh = 0;

            for (int i = zzMACDHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzMACDHigh[i] != 0)
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

            for (int i = zzMACDLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;


                if (zzMACDLow[i] != 0 && zzMACDLowOne == 0)
                {
                    zzMACDLowOne = zzMACDLow[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzMACDLow[i] != 0 && indexTwo != i && zzMACDLowTwo == 0)
                {
                    zzMACDLowTwo = zzMACDLow[i];
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
            if (zzMACDLowOne > zzMACDLowTwo && zzMACDLowOne != 0)
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
        private bool DevirgenceSell(List<decimal> zzHigh, List<decimal> zzMACDHigh, List<decimal> zzMACDLow)
        {

            decimal zzHighOne = 0;
            decimal zzHighTwo = 0;
            decimal zzMACDHighOne = 0;
            decimal zzMACDHighTwo = 0;

            int indexOne = 0;

            int indexTwo = 0;

            int indexLow = 0;

            for (int i = zzMACDLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzMACDLow[i] != 0)
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

            for (int i = zzMACDHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;


                if (zzMACDHigh[i] != 0 && zzMACDHighOne == 0)
                {
                    zzMACDHighOne = zzMACDHigh[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzMACDHigh[i] != 0 && indexTwo != i && zzMACDHighTwo == 0)
                {
                    zzMACDHighTwo = zzMACDHigh[i];
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
            if (zzMACDHighOne < zzMACDHighTwo && zzMACDHighOne != 0)
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