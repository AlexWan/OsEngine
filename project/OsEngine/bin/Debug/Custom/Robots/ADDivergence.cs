using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on AD indicator divergence and price

Buy: 
at the price, the minimum for a certain period of time is below the previous
minimum, and on the indicator, the minimum is higher than the previous one.

Sell: 
on the price the maximum for a certain period of time is higher than the 
previous maximum, and on the indicator the maximum is lower than the previous one.

Exit: after n number of candles.
 
 */

namespace OsEngine.Robots.ZZAO
{
    // We create an attribute so that we don't write anything to the BotFactory
    [Bot("ADDivergence")]
    public class ADDivergence : BotPanel
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
        private Aindicator _ZigZagAD;

        // Divergence
        private StrategyParameterInt LenghtZig;
        private StrategyParameterInt LenghtZigAO;

        // Exit 
        private StrategyParameterInt ExitCandlesCount;

        public ADDivergence(string name, StartProgram startProgram) : base(name, startProgram)
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
            LenghtZig = CreateParameter("Period Zig", 30, 10, 300, 10, "Indicator");
            LenghtZigAO = CreateParameter("Period Zig AO", 30, 10, 300, 10, "Indicator");


            // Create indicator ZigZagAO
            _ZigZagAD = IndicatorsFactory.CreateIndicatorByName("ZigZagAD", name + "ZigZagAD", false);
            _ZigZagAD = (Aindicator)_tab.CreateCandleIndicator(_ZigZagAD, "NewArea");
            ((IndicatorParameterInt)_ZigZagAD.Parameters[0]).ValueInt = LenghtZigAO.ValueInt;
            _ZigZagAD.Save();

            // Create indicator ZigZag
            _ZZ = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _ZZ = (Aindicator)_tab.CreateCandleIndicator(_ZZ, "Prime");
            ((IndicatorParameterInt)_ZZ.Parameters[0]).ValueInt = LenghtZig.ValueInt;
            _ZZ.Save();

            // Exit
            ExitCandlesCount = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += ADDivergence_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on AD indicator divergence and price. " +
                "Buy: at the price, the minimum for a certain period of time is below the previous minimum," +
                " and on the indicator, the minimum is higher than the previous one. " +
                "Sell: on the price the maximum for a certain period of time is higher than the previous maximum," +
                " and on the indicator the maximum is lower than the previous one." +
                "Exit: after n number of candles.";
        }

        // Indicator Update event
        private void ADDivergence_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ZigZagAD.Parameters[0]).ValueInt = LenghtZigAO.ValueInt;
            _ZigZagAD.Save();
            _ZigZagAD.Reload();

            ((IndicatorParameterInt)_ZZ.Parameters[0]).ValueInt = LenghtZig.ValueInt;
            _ZZ.Save();
            _ZZ.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ADDivergence";
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

            List<decimal> zzADLow = _ZigZagAD.DataSeries[4].Values;
            List<decimal> zzADHigh = _ZigZagAD.DataSeries[3].Values;


            if (openPositions == null || openPositions.Count == 0)
            {

                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                decimal lastPrice = candles[candles.Count - 1].Close;
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (DevirgenceBuy(zzLow, zzADLow, zzADHigh) == true)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (DevirgenceSell(zzHigh, zzADHigh, zzADLow) == true)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage, time.ToString());
                    }

                }
                return;
            }
        }
        //  logic close position
        private void LogicClosePosition(List<Candle> candles)
        {

            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];

            decimal lastPrice = candles[candles.Count - 1].Close;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;


            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                List<Candle> candles1 = new List<Candle>();

                

                if (!NeedClosePosition(positions, candles))
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                }
                else // If the direction of the position is sale
                {
                    _tab.CloseAtLimit(openPositions[0], lastPrice + _slippage, openPositions[0].OpenVolume);
                }
            }
        }

        private bool DevirgenceBuy(List<decimal> zzLow, List<decimal> zzADLow, List<decimal> zzADHigh)
        {
            decimal zzLowOne = 0;
            decimal zzLowTwo = 0;
            decimal zzADLowOne = 0;
            decimal zzADLowTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexHigh = 0;

            for (int i = zzADHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzADHigh[i] != 0)
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

            for (int i = zzADLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzADLow[i] != 0 && zzADLowOne == 0)
                {
                    zzADLowOne = zzADLow[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzADLow[i] != 0 && indexTwo != i && zzADLowTwo == 0)
                {
                    zzADLowTwo = zzADLow[i];
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
            if (zzADLowOne > zzADLowTwo && zzADLowOne != 0)
            {
                cntLow++;
            }

            if (cntLow == 2)
            {
                return true;
            }
            return false;
        }

        private bool DevirgenceSell(List<decimal> zzHigh, List<decimal> zzADHigh, List<decimal> zzADLow)
        {
            decimal zzHighOne = 0;
            decimal zzHighTwo = 0;
            decimal zzADHighOne = 0;
            decimal zzADHighTwo = 0;

            int indexOne = 0;

            int indexTwo = 0;

            int indexLow = 0;

            for (int i = zzADLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzADLow[i] != 0)
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

            for (int i = zzADHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzADHigh[i] != 0 && zzADHighOne == 0)
                {
                    zzADHighOne = zzADHigh[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzADHigh[i] != 0 && indexTwo != i && zzADHighTwo == 0)
                {
                    zzADHighTwo = zzADHigh[i];
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
            if (zzADHighOne < zzADHighTwo && zzADHighOne != 0)
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
                    if (counter >= ExitCandlesCount.ValueInt + 1)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}

