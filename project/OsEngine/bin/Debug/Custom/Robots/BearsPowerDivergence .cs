using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine.

Trend strategy on Bears Power Divergence.

Buy:
1. Bears Power columns must be below 0.
2. The minimums on the chart are decreasing, but on the indicator they are growing.

Sale:
1. The price is lower than the Parabolic value. For the next candle, the price crosses the indicator from top to bottom.
2. Bulls Power columns must be below 0.
3. Bears Power columns must be below 0.

Exit:
The Bears Power indicator has become higher than 0.
*/

namespace OsEngine.Robots.myRobots
{
    // We create an attribute so that we don't write anything to the BotFactory

    [Bot("BearsPowerDivergence")]
    public class BearsPowerDivergence : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;


        // Indicator Settings
        private StrategyParameterInt _lengthBearsPower;
        private StrategyParameterInt _lengthZZ;
        private StrategyParameterInt _lengthZZBearsPower;

        // Indicator
        Aindicator _zz;
        Aindicator _zzBP;

        // The last value of the indicators           
        private decimal _lastZZBP;


        public BearsPowerDivergence(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency" }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 1, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            StartTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            EndTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator Settings
            _lengthZZ = CreateParameter("Zig Zag", 20, 10, 300, 10, "Indicator");
            _lengthZZBearsPower = CreateParameter("_length ZZ Bears Power", 20, 10, 300, 10, "Indicator");
            _lengthBearsPower = CreateParameter("Zig Zag BP", 20, 10, 300, 10, "Indicator");

            // Create indicator Zig Zag
            _zz = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "Zig Zag", false);
            _zz = (Aindicator)_tab.CreateCandleIndicator(_zz, "Prime");
            ((IndicatorParameterInt)_zz.Parameters[0]).ValueInt = _lengthZZ.ValueInt;
            _zz.Save();

            // Create indicator Zig Zag BP
            _zzBP = IndicatorsFactory.CreateIndicatorByName("ZigZagBP", name + "Zig Zag BP", false);
            _zzBP = (Aindicator)_tab.CreateCandleIndicator(_zzBP, "NewArea");
            ((IndicatorParameterInt)_zzBP.Parameters[0]).ValueInt = _lengthBearsPower.ValueInt;
            ((IndicatorParameterInt)_zzBP.Parameters[1]).ValueInt = _lengthZZBearsPower.ValueInt;
            _zzBP.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BearsPowerDivergence_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Desctiption = "Trading robot for osengine. " +
                "Trend strategy on Bears Power Divergence. " +
                "Buy: " +
                "1. Bears Power columns must be below 0. " +
                "2. The minimums on the chart are decreasing, but on the indicator they are growing. " +
                "Sale: " +
                "1. The price is lower than the Parabolic value. For the next candle, the price crosses the indicator from top to bottom. " +
                "2. Bulls Power columns must be below 0. " +
                "3. Bears Power columns must be below 0. " +
                "Exit: " +
                "The Bears Power indicator has become higher than 0.";
        }

        // Indicator Update event
        private void BearsPowerDivergence_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_zz.Parameters[0]).ValueInt = _lengthZZ.ValueInt;
            _zz.Save();
            _zz.Reload();

            ((IndicatorParameterInt)_zzBP.Parameters[0]).ValueInt = _lengthBearsPower.ValueInt;
            ((IndicatorParameterInt)_zzBP.Parameters[1]).ValueInt = _lengthZZBearsPower.ValueInt;
            _zzBP.Save();
            _zzBP.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BearsPowerDivergence";
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
            if (candles.Count < _lengthZZ.ValueInt || candles.Count < _lengthBearsPower.ValueInt)
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
            List<decimal> zzLow = _zz.DataSeries[3].Values;
            List<decimal> zzBPLow = _zzBP.DataSeries[4].Values;
            List<decimal> zzBPHigh = _zzBP.DataSeries[3].Values;
           
            // The last value of the indicators
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;


            // He last value of the indicator           
            if (openPositions == null || openPositions.Count == 0)
            {
                _lastZZBP = _zzBP.DataSeries[0].Last;

                if (DevirgenceBuy(zzLow, zzBPLow, _lastZZBP,zzBPHigh) == true)
                {
                    _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // He last value of the indicator
            _lastZZBP = _zzBP.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // if the direction of the position is buy
                {
                    if (_lastZZBP > 0)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }                
                }
            }
        }
        private bool DevirgenceBuy(List<decimal> zzLow, List<decimal> zzBPLow, decimal lastBP,List <decimal> zzBPHigh)
        {
            if(lastBP>0)
            {
                return false;
            }
            decimal zzLowOne = 0;
            decimal zzLowTwo = 0;
            decimal zzBPLowOne = 0;
            decimal zzBPLowTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexHigh = 0;

            for (int i = zzBPHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzBPHigh[i] != 0)
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

            for (int i = zzBPLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;
               
                if (zzBPLow[i] != 0 && zzBPLowOne == 0)
                {
                    zzBPLowOne = zzBPLow[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzBPLow[i] != 0 && indexTwo != i && zzBPLowTwo == 0)
                {
                    zzBPLowTwo = zzBPLow[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            decimal cntLow = 0;
            if (zzLowOne < zzLowTwo && zzLowOne != 0)
            {
                cntLow++;
            }
            if (zzBPLowOne > zzBPLowTwo && zzBPLowOne != 0 && indexTwo<indexHigh)
            {
                cntLow++;
            }

            if (cntLow == 2)
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


