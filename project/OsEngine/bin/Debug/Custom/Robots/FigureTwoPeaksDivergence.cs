using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on Divergence.

Buy:
1. The lows of the Awesome histogram are gradually increasing.
2. The lows of the chart, on the contrary, gradually decrease.
Sell:
1. The extreme points AO decrease successively.
2. Extremes of the price chart, on the contrary, are rising.

Buy Exit:
1. Stop behind the minimum for a certain number of candles
2. Profit - for the maximum for a certain number of candles
Sell Exit:
1. Stop behind the maximum for a certain number of candles
2. Profit - for a minimum for a certain number of candles
 
 */


namespace OsEngine.Robots.ZZAO
{
    // We create an attribute so that we don't write anything to the BotFactory
    [Bot("FigureTwoPeaksDivergence")]
    public class FigureTwoPeaksDivergence : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // Setting indicator
        private StrategyParameterInt FastLineLengthAO;
        private StrategyParameterInt SlowLineLengthAO;

        // Indicator
        private Aindicator _ZZ;
        private Aindicator _ZigZagAO;

        // Divergence
        private StrategyParameterInt LenghtZig;
        private StrategyParameterInt LenghtZigAO;

        public FigureTwoPeaksDivergence(string name, StartProgram startProgram) : base(name, startProgram)
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

            // Setting indicator
            FastLineLengthAO = CreateParameter("Fast Line Length AO", 12, 10, 300, 10, "Indicator");
            SlowLineLengthAO = CreateParameter("Slow Line Length AO", 24, 10, 300, 10, "Indicator");
            LenghtZig = CreateParameter("Period Zig", 24, 10, 300, 10, "Indicator");
            LenghtZigAO = CreateParameter("Period Zig AO", 30, 10, 300, 10, "Indicator");


            // Create indicator ZigZagAO
            _ZigZagAO = IndicatorsFactory.CreateIndicatorByName("ZigZagAO", name + "ZigZagAO", false);
            _ZigZagAO = (Aindicator)_tab.CreateCandleIndicator(_ZigZagAO, "NewArea");
            ((IndicatorParameterInt)_ZigZagAO.Parameters[0]).ValueInt = FastLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_ZigZagAO.Parameters[1]).ValueInt = SlowLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_ZigZagAO.Parameters[3]).ValueInt = LenghtZigAO.ValueInt;
            _ZigZagAO.Save();

            // Create indicator ZigZag
            _ZZ = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _ZZ = (Aindicator)_tab.CreateCandleIndicator(_ZZ, "Prime");
            ((IndicatorParameterInt)_ZZ.Parameters[0]).ValueInt = LenghtZig.ValueInt;
            _ZZ.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyOnAwesomeOscillatorAndParabolicSAR_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Divergence " +
                "Buy: " +
                "1. The lows of the Awesome histogram are gradually increasing. " +
                "2. The lows of the chart, on the contrary, gradually decrease. " +
                "Sell: " +
                "1. The extreme points AO decrease successively. " +
                "2. Extremes of the price chart, on the contrary, are rising. " +
                "Buy Exit: " +
                "1. Stop behind the minimum for a certain number of candles " +
                "2. Profit - for the maximum for a certain number of candles " +
                "Sell Exit: " +
                "1. Stop behind the maximum for a certain number of candles " +
                "2. Profit - for a minimum for a certain number of candles";
        }

        // Indicator Update event
        private void StrategyOnAwesomeOscillatorAndParabolicSAR_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ZigZagAO.Parameters[0]).ValueInt = FastLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_ZigZagAO.Parameters[1]).ValueInt = SlowLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_ZigZagAO.Parameters[3]).ValueInt = LenghtZigAO.ValueInt;
            _ZigZagAO.Save();
            _ZigZagAO.Reload();

            ((IndicatorParameterInt)_ZZ.Parameters[0]).ValueInt = LenghtZig.ValueInt;
            _ZZ.Save();
            _ZZ.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "FigureTwoPeaksDivergence";
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
            if (candles.Count < SlowLineLengthAO.ValueInt)
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

            List<decimal> zzHigh = _ZZ.DataSeries[2].Values;
            List<decimal> zzLow = _ZZ.DataSeries[3].Values;

            List<decimal> zzAOLow = _ZigZagAO.DataSeries[4].Values;
            List<decimal> zzAOHigh = _ZigZagAO.DataSeries[3].Values;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (DevirgenceBuy(zzLow,zzAOLow, zzAOHigh) == true)
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
        //  logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal lastPrice = candles[candles.Count - 1].Close;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            List<decimal> zzHigh = _ZZ.DataSeries[2].Values;
            List<decimal> zzLow = _ZZ.DataSeries[3].Values;

            List<decimal> zzAOLow = _ZigZagAO.DataSeries[4].Values;
            List<decimal> zzAOHigh = _ZigZagAO.DataSeries[3].Values;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (DevirgenceSell(zzHigh, zzAOHigh, zzAOLow) == true)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (DevirgenceBuy(zzLow, zzAOLow,zzAOHigh) == true)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
                    }
                }

            }

        }
        // Method for finding divergence
        private bool DevirgenceBuy(List<decimal> zzLow,List<decimal> zzAOLow,List<decimal> zzAOHigh)
        {
            decimal zzLowOne = 0;
            decimal zzLowTwo = 0;
            decimal zzAOLowOne = 0;
            decimal zzAOLowTwo = 0;
            
            int indexOne = 0;

            int indexTwo = 0;

            int indexHigh = 0;

            for (int i = zzAOHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzAOHigh[i] != 0)
                {
                    cnt++;
                    indexHigh = i;
                }

                if (cnt == 1)
                {
                    break;
                }

            }

            for (int i = zzLow.Count - 1;i >= 0;i--)
            {
                int cnt = 0;
                

                if (zzLow[i] != 0 &&  zzLowOne == 0)
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

                if(cnt == 2)
                {
                    break;
                }

            }

            for (int i = zzAOLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;


                if (zzAOLow[i] != 0 && zzAOLowOne == 0)
                {
                    zzAOLowOne = zzAOLow[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzAOLow[i] != 0 && indexTwo != i && zzAOLowTwo == 0)
                {
                    zzAOLowTwo = zzAOLow[i];
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
            if (zzAOLowOne > zzAOLowTwo && zzAOLowOne !=0)
            {
                cntLow++;
            }

            if(cntLow == 2) 
            {
                return true;
            }

            return false;
        }
        // Method for finding divergence
        private bool DevirgenceSell(List<decimal> zzHigh, List<decimal> zzAOHigh, List<decimal> zzAOLow)
        {

            decimal zzHighOne = 0;
            decimal zzHighTwo = 0;
            decimal zzAOHighOne = 0;
            decimal zzAOHighTwo = 0;

            int indexOne = 0;

            int indexTwo = 0;

            int indexLow = 0;

            for (int i = zzAOLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzAOLow[i] != 0)
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

            for (int i = zzAOHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;


                if (zzAOHigh[i] != 0 && zzAOHighOne == 0)
                {
                    zzAOHighOne = zzAOHigh[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzAOHigh[i] != 0 && indexTwo != i && zzAOHighTwo == 0)
                {
                    zzAOHighTwo = zzAOHigh[i];
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
            if (zzAOHighOne < zzAOHighTwo && zzAOHighOne != 0)
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
