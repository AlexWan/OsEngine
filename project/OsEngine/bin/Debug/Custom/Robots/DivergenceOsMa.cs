using System;
using System.Collections.Generic;
using System.Linq;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The robot on strategy Devergence OsMa.

Buy: The lows on the chart are decreasing, but on the indicator they are growing.

Sell: The highs on the chart are rising, and on the indicator they are decreasing.

Exit: After a certain number of candles.
 
 */

namespace OsEngine.Robots.MyBots
{
    [Bot("DivergenceOsMa")] // We create an attribute so that we don't write anything to the BotFactory
    internal class DivergenceOsMa : BotPanel
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
        private StrategyParameterInt LenghZigZag;
        private StrategyParameterInt LenghtFastLine;
        private StrategyParameterInt LenghtSlowLine;
        private StrategyParameterInt LenghtSignalLine;

        // Exit 
        private StrategyParameterInt ExitCandles;

        // Indicator
        Aindicator _ZigZag;
        Aindicator _ZigZagOsma;

        public DivergenceOsMa(string name, StartProgram startProgram) : base(name, startProgram)
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
            LenghZigZag = CreateParameter("lenght ZigZag", 10, 10, 300, 10, "Indicator");
            LenghtFastLine = CreateParameter("lenght Fast Line", 12, 10, 300, 1, "Indicator");
            LenghtSlowLine = CreateParameter("lenght Slow Line", 26, 10, 300, 1, "Indicator");
            LenghtSignalLine = CreateParameter("lenght Signal Line", 9, 9, 300, 1, "Indicator");

            // Exit
            ExitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit");

            // Create indicator ZigZag
            _ZigZag = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _ZigZag = (Aindicator)_tab.CreateCandleIndicator(_ZigZag, "Prime");
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = LenghZigZag.ValueInt;
            _ZigZag.Save();

            // Create indicator ZigZag Osma
            _ZigZagOsma = IndicatorsFactory.CreateIndicatorByName("ZigZagOsMa", name + "ZigZagOsma", false);
            _ZigZagOsma = (Aindicator)_tab.CreateCandleIndicator(_ZigZagOsma, "NewZZOsMa");
            ((IndicatorParameterInt)_ZigZagOsma.Parameters[0]).ValueInt = LenghtFastLine.ValueInt;
            ((IndicatorParameterInt)_ZigZagOsma.Parameters[1]).ValueInt = LenghtSlowLine.ValueInt;
            ((IndicatorParameterInt)_ZigZagOsma.Parameters[2]).ValueInt = LenghtSignalLine.ValueInt;
            ((IndicatorParameterInt)_ZigZagOsma.Parameters[3]).ValueInt = LenghZigZag.ValueInt;
            _ZigZagOsma.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += DivergenceOsMa_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The robot on Devergence Osma. " +
                "Buy: The lows on the chart are decreasing, but on the indicator they are growing. " +
                "Sell: The highs on the chart are rising, and on the indicator they are decreasing. " +
                "Exit: after a certain number of candles.";
        }
       
        private void DivergenceOsMa_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ZigZag.Parameters[0]).ValueInt = LenghZigZag.ValueInt;
            _ZigZag.Save();
            _ZigZag.Reload();

            ((IndicatorParameterInt)_ZigZagOsma.Parameters[0]).ValueInt = LenghtFastLine.ValueInt;
            ((IndicatorParameterInt)_ZigZagOsma.Parameters[1]).ValueInt = LenghtSlowLine.ValueInt;
            ((IndicatorParameterInt)_ZigZagOsma.Parameters[2]).ValueInt = LenghtSignalLine.ValueInt;
            ((IndicatorParameterInt)_ZigZagOsma.Parameters[3]).ValueInt = LenghZigZag.ValueInt;
            _ZigZagOsma.Save();
            _ZigZagOsma.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "DivergenceOsMa";
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
            if (candles.Count < LenghZigZag.ValueInt)
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

            List<decimal> zzOsMaLow = _ZigZagOsma.DataSeries[4].Values;
            List<decimal> zzOsMaHigh = _ZigZagOsma.DataSeries[3].Values;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (DevirgenceBuy(zzLow, zzOsMaLow, zzOsMaHigh) == true)
                    {
                        DateTime time = candles.Last().TimeStart;

                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (DevirgenceSell(zzHigh, zzOsMaHigh, zzOsMaLow) == true)
                    {
                        DateTime time = candles.Last().TimeStart;

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
        private bool DevirgenceBuy(List<decimal> zzLow, List<decimal> zzOsMaLow, List<decimal> zzOsMaHigh)
        {
            decimal zzLowOne = 0;
            decimal zzLowTwo = 0;
            decimal zzOsMaLowOne = 0;
            decimal zzOsMaLowTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexHigh = 0;

            for (int i = zzOsMaHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzOsMaHigh[i] != 0)
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

            for (int i = zzOsMaLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzOsMaLow[i] != 0 && zzOsMaLowOne == 0)
                {
                    zzOsMaLowOne = zzOsMaLow[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzOsMaLow[i] != 0 && indexTwo != i && zzOsMaLowTwo == 0)
                {
                    zzOsMaLowTwo = zzOsMaLow[i];
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
            if (zzOsMaLowOne > zzOsMaLowTwo && zzOsMaLowOne != 0)
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
        private bool DevirgenceSell(List<decimal> zzHigh, List<decimal> zzOsMaHigh, List<decimal> zzOsMaLow)
        {
            decimal zzHighOne = 0;
            decimal zzHighTwo = 0;
            decimal zzAsiHighOne = 0;
            decimal zzAsiHighTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexLow = 0;

            for (int i = zzOsMaLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzOsMaLow[i] != 0)
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

            for (int i = zzOsMaHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzOsMaHigh[i] != 0 && zzAsiHighOne == 0)
                {
                    zzAsiHighOne = zzOsMaHigh[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzOsMaHigh[i] != 0 && indexTwo != i && zzAsiHighTwo == 0)
                {
                    zzAsiHighTwo = zzOsMaHigh[i];
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
