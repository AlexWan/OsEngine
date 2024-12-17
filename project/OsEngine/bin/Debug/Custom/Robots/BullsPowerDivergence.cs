using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
/*Discription
Trading robot for osengine.

Trend strategy on Bulls Power Divergence.

Buy:
1. Bulls Power columns must be higher than 0;
2. The highs on the chart are rising, and on the indicator they are decreasing

Exit:
The Bulls Power indicator has gone below 0.
*/
namespace OsEngine.Robots.My_bots
{
    [Bot("BullsPowerDivergence")]
    public  class BullsPowerDivergence : BotPanel
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
        private StrategyParameterInt _lengthBullsPower;
        private StrategyParameterInt _lengthZZ;
        private StrategyParameterInt _lengthZZBullsPower;

        // Indicator
        Aindicator _zz;
        Aindicator _zzBullsPower;

        // The last value of the indicators           
        private decimal _lastZZBullsPower;


        public BullsPowerDivergence(string name, StartProgram startProgram) : base(name, startProgram)
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
            _lengthZZBullsPower = CreateParameter("_length ZZ Bulls Power", 20, 10, 300, 10, "Indicator");
            _lengthBullsPower = CreateParameter(" _length Bulls Power", 20, 10, 300, 10, "Indicator");

            // Create indicator Zig Zag
            _zz = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "Zig Zag", false);
            _zz = (Aindicator)_tab.CreateCandleIndicator(_zz, "Prime");
            ((IndicatorParameterInt)_zz.Parameters[0]).ValueInt = _lengthZZ.ValueInt;
            _zz.Save();

            // Create indicator Zig Zag BullsPower
            _zzBullsPower = IndicatorsFactory.CreateIndicatorByName("ZZBullsPower", name + "Zig Zag BullsPower", false);
            _zzBullsPower = (Aindicator)_tab.CreateCandleIndicator(_zzBullsPower, "NewArea");
            ((IndicatorParameterInt)_zzBullsPower.Parameters[0]).ValueInt = _lengthBullsPower.ValueInt;
            ((IndicatorParameterInt)_zzBullsPower.Parameters[1]).ValueInt = _lengthZZBullsPower.ValueInt;
            _zzBullsPower.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BullsPowerDivergence_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend strategy on Bulls Power Divergence." +
                "Buy:" +
                "1.Bulls Power columns must be higher than 0;" +
                "2.The highs on the chart are rising, and on the indicator they are decreasing" +
                "Exit:" +
                "The Bulls Power indicator has gone below 0.";
        }

        // Indicator Update event
        private void BullsPowerDivergence_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_zz.Parameters[0]).ValueInt = _lengthZZ.ValueInt;
            _zz.Save();
            _zz.Reload();

            ((IndicatorParameterInt)_zzBullsPower.Parameters[0]).ValueInt = _lengthBullsPower.ValueInt;
            ((IndicatorParameterInt)_zzBullsPower.Parameters[1]).ValueInt = _lengthZZBullsPower.ValueInt;
            _zzBullsPower.Save();
            _zzBullsPower.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BullsPowerDivergence";
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
            if (candles.Count < _lengthZZ.ValueInt || candles.Count < _lengthBullsPower.ValueInt)
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
            List<decimal> zzHigh = _zz.DataSeries[2].Values;
            List<decimal> zzBullsPowerLow = _zzBullsPower.DataSeries[4].Values;
            List<decimal> zzBullsPowerHigh = _zzBullsPower.DataSeries[3].Values;

            // The last value of the indicators
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;


            // He last value of the indicator           
            if (openPositions == null || openPositions.Count == 0)
            {
                _lastZZBullsPower = _zzBullsPower.DataSeries[0].Last;

                if (DevirgenceSell(zzHigh, zzBullsPowerHigh, zzBullsPowerLow) == true && _lastZZBullsPower>0)
                {
                    _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage);
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
            _lastZZBullsPower = _zzBullsPower.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Sell) // If the direction of the position is buy
                {
                    if (_lastZZBullsPower < 0)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
            }
        }
        private bool DevirgenceSell(List<decimal> zzHigh, List<decimal> zzBullsPowerHigh, List<decimal> zzBullsPowerLow)
        {
            decimal zzHighOne = 0;
            decimal zzHighTwo = 0;
            decimal zzBullsPowerHighOne = 0;
            decimal zzBullsPowerHighTwo = 0;

            int indexOne = 0;

            int indexTwo = 0;

            int indexLow = 0;

            for (int i = zzBullsPowerLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzBullsPowerLow[i] != 0)
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

            for (int i = zzBullsPowerHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzBullsPowerHigh[i] != 0 && zzBullsPowerHighOne == 0)
                {
                    zzBullsPowerHighOne = zzBullsPowerHigh[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzBullsPowerHigh[i] != 0 && indexTwo != i && zzBullsPowerHighTwo == 0)
                {
                    zzBullsPowerHighTwo = zzBullsPowerHigh[i];
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
            if (zzBullsPowerHighOne < zzBullsPowerHighTwo && zzBullsPowerHighOne != 0)
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


    

