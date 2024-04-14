using System;
using System.Collections.Generic;
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

The trend robot on strategy Devergence SMI.

Buy: The lows on the chart are falling, while the lows are rising on the indicator.

Sell: the highs on the chart are rising, while the indicator is falling.

Buy exit: trailing stop as a % of the Low of the candle where you entered.

Sell ​​exit: trailing stop as a % of the High of the candle where you entered.

 */


namespace OsEngine.Robots.AO
{
    [Bot("DevergenceSMI")] // We create an attribute so that we don't write anything to the BotFactory
    public class DevergenceSMI : BotPanel
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
        private StrategyParameterInt StochasticPeriod1;
        private StrategyParameterInt StochasticPeriod2;
        private StrategyParameterInt StochasticPeriod3;
        private StrategyParameterInt StochasticPeriod4;

        // Indicator
        Aindicator _ZigZag;
        Aindicator _ZigZagSMI;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        public DevergenceSMI(string name, StartProgram startProgram) : base(name, startProgram)
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
            StochasticPeriod1 = CreateParameter("StochasticPeriod1", 12, 10, 300, 1, "Indicator");
            StochasticPeriod2 = CreateParameter("StochasticPeriod2", 26, 10, 300, 1, "Indicator");
            StochasticPeriod3 = CreateParameter("StochasticPeriod3", 3, 9, 300, 1, "Indicator");
            StochasticPeriod4 = CreateParameter("StochasticPeriod4", 2, 9, 300, 1, "Indicator");

            // Create indicator ZigZag
            _ZigZag = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _ZigZag = (Aindicator)_tab.CreateCandleIndicator(_ZigZag, "Prime");
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZag.Save();

            // Create indicator ZigZag Stochastic
            _ZigZagSMI = IndicatorsFactory.CreateIndicatorByName("ZigZagSMI", name + "ZigZagSMI", false);
            _ZigZagSMI = (Aindicator)_tab.CreateCandleIndicator(_ZigZagSMI, "NewArea");
            ((IndicatorParameterInt)_ZigZagSMI.Parameters[0]).ValueInt = StochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_ZigZagSMI.Parameters[1]).ValueInt = StochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_ZigZagSMI.Parameters[2]).ValueInt = StochasticPeriod3.ValueInt;
            ((IndicatorParameterInt)_ZigZagSMI.Parameters[3]).ValueInt = StochasticPeriod4.ValueInt;
            ((IndicatorParameterInt)_ZigZagSMI.Parameters[4]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZagSMI.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += DevergenceSMI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on strategy Devergence SMI. " +
                "Buy: The lows on the chart are falling, while the lows are rising on the indicator. " +
                "Sell: the highs on the chart are rising, while the indicator is falling. " +
                "Buy exit: trailing stop as a % of the Low of the candle where you entered. " +
                "Sell ​​exit: trailing stop as a % of the High of the candle where you entered.";

        }

        private void DevergenceSMI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZag.Save();
            _ZigZag.Reload();
            ((IndicatorParameterInt)_ZigZagSMI.Parameters[0]).ValueInt = StochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_ZigZagSMI.Parameters[1]).ValueInt = StochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_ZigZagSMI.Parameters[2]).ValueInt = StochasticPeriod3.ValueInt;
            ((IndicatorParameterInt)_ZigZagSMI.Parameters[3]).ValueInt = StochasticPeriod4.ValueInt;
            ((IndicatorParameterInt)_ZigZagSMI.Parameters[4]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZagSMI.Save();
            _ZigZagSMI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "DevergenceSMI";
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
            if (candles.Count < StochasticPeriod1.ValueInt || candles.Count < PeriodZigZag.ValueInt)
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

                List<decimal> zzAOLow = _ZigZagSMI.DataSeries[4].Values;
                List<decimal> zzAOHigh = _ZigZagSMI.DataSeries[3].Values;

                decimal lastPrice = candles[candles.Count - 1].Close;

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
                    stopPrice = lov - lov * TrailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);

            }
        }

        // Method for finding divergence
        private bool DevirgenceBuy(List<decimal> zzLow, List<decimal> zzAOLow, List<decimal> zzAOHigh)
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
            if (zzAOLowOne > zzAOLowTwo && zzAOLowOne != 0)
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