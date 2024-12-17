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

/* Description
trading robot for osengine

The trend robot on strategy Devergence MACD.

Buy: The lows on the chart are falling, while the lows are rising on the indicator.

Sell: the highs on the chart are rising, while the indicator is falling.

Exit: stop and profit in % of the entry price.
 
 */


namespace OsEngine.Robots.AO
{
    [Bot("DevergenceMACD")] // We create an attribute so that we don't write anything to the BotFactory
    public class DevergenceMACD : BotPanel
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
        private StrategyParameterInt _lenghtFastLine;
        private StrategyParameterInt _lenghtSlowLine;
        private StrategyParameterInt _lenghtSignalLine;

        // Indicator
        Aindicator _ZigZag;
        Aindicator _ZigZagMACD;

        // Exit
        private StrategyParameterDecimal StopValue;
        private StrategyParameterDecimal ProfitValue;

        public DevergenceMACD(string name, StartProgram startProgram) : base(name, startProgram)
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
            _lenghtFastLine = CreateParameter("lenght Fast Line", 12, 10, 300, 1, "Indicator");
            _lenghtSlowLine = CreateParameter("lenght Slow Line", 26, 10, 300, 1, "Indicator");
            _lenghtSignalLine = CreateParameter("lenght Signal Line", 9, 9, 300, 1, "Indicator");

            // Create indicator ZigZag
            _ZigZag = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _ZigZag = (Aindicator)_tab.CreateCandleIndicator(_ZigZag, "Prime");
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZag.Save();

            // Create indicator ZigZag CCI
            _ZigZagMACD = IndicatorsFactory.CreateIndicatorByName("ZigZagMACD", name + "ZigZagMACD", false);
            _ZigZagMACD = (Aindicator)_tab.CreateCandleIndicator(_ZigZagMACD, "NewArea");
            ((IndicatorParameterInt)_ZigZagMACD.Parameters[0]).ValueInt = _lenghtFastLine.ValueInt;
            ((IndicatorParameterInt)_ZigZagMACD.Parameters[1]).ValueInt = _lenghtSlowLine.ValueInt;
            ((IndicatorParameterInt)_ZigZagMACD.Parameters[2]).ValueInt = _lenghtSignalLine.ValueInt;
            ((IndicatorParameterInt)_ZigZagMACD.Parameters[3]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZagMACD.Save();

            // Exit
            StopValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");
            ProfitValue = CreateParameter("Profit Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += DevergenceMACD_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on strategy Devergence MACD. " +
                "Buy: The lows on the chart are falling, while the lows are rising on the indicator. " +
                "Sell: the highs on the chart are rising, while the indicator is falling. " +
                "Exit: stop and profit in % of the entry price.";
        }

        private void DevergenceMACD_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZag.Save();
            _ZigZag.Reload();
            ((IndicatorParameterInt)_ZigZagMACD.Parameters[0]).ValueInt = _lenghtFastLine.ValueInt;
            ((IndicatorParameterInt)_ZigZagMACD.Parameters[1]).ValueInt = _lenghtSlowLine.ValueInt;
            ((IndicatorParameterInt)_ZigZagMACD.Parameters[2]).ValueInt = _lenghtSignalLine.ValueInt;
            ((IndicatorParameterInt)_ZigZagMACD.Parameters[3]).ValueInt = PeriodZigZag.ValueInt;
            _ZigZagMACD.Save();
            _ZigZagMACD.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "DevergenceMACD";
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
            if (candles.Count < _lenghtFastLine.ValueInt || candles.Count < PeriodZigZag.ValueInt ||
                candles.Count < _lenghtSlowLine.ValueInt || candles.Count < _lenghtSignalLine.ValueInt)
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

                List<decimal> zzAOLow = _ZigZagMACD.DataSeries[4].Values;
                List<decimal> zzAOHigh = _ZigZagMACD.DataSeries[3].Values;

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

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal profitActivation = pos.EntryPrice + pos.EntryPrice * ProfitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice - pos.EntryPrice * StopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal profitActivation = pos.EntryPrice - pos.EntryPrice * ProfitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice + pos.EntryPrice * StopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
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