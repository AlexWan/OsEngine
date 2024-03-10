﻿using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Trading robot for osengine.

The trend robot on Strategy Envelops And RVI.

Buy:
1. The candle closed above the upper line of the Envelope.
2. The RVI is greater than 0 and growing.
Sell:
1. The candle closed below the bottom line of the Envelope.
2. RVI is less than 0 and falling.

Exit: the reverse side of the envelope channel.
 */

namespace OsEngine.Robots.My_bots
{
    [Bot("StrategyEnvelopsAndRVI")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyEnvelopsAndRVI : BotPanel
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
        private StrategyParameterInt EnvelopsLength;
        private StrategyParameterDecimal EnvelopsDeviation;
        private StrategyParameterInt _PeriodRVI;

        // Indicator
        Aindicator _Envelop;
        private Aindicator _RVI;

        // The last value of the indicator
        private decimal _lastUpLine;
        private decimal _lastDownLine;
        private decimal _lastSignalRVI;

        // The prev value of the indicator
        private decimal _prevSignalRVI;

        public StrategyEnvelopsAndRVI(string name, StartProgram startProgram) : base(name, startProgram)
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
            EnvelopsLength = CreateParameter("Envelops Length", 21, 7, 48, 7, "Indicator");
            EnvelopsDeviation = CreateParameter("Envelops Deviation", 1.0m, 1, 5, 0.1m, "Indicator");
            _PeriodRVI = CreateParameter("Period RVI", 5, 10, 300, 1, "Indicator");

            // Create indicator Envelop
            _Envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _Envelop = (Aindicator)_tab.CreateCandleIndicator(_Envelop, "Prime");
            ((IndicatorParameterInt)_Envelop.Parameters[0]).ValueInt = EnvelopsLength.ValueInt;
            ((IndicatorParameterDecimal)_Envelop.Parameters[1]).ValueDecimal = EnvelopsDeviation.ValueDecimal;
            _Envelop.Save();

            // Creating an indicator RVI
            _RVI = IndicatorsFactory.CreateIndicatorByName("RVI", name + "RVI", false);
            _RVI = (Aindicator)_tab.CreateCandleIndicator(_RVI, "NewArea");
            ((IndicatorParameterInt)_RVI.Parameters[0]).ValueInt = _PeriodRVI.ValueInt;
            _RVI.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakEnvelops_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Envelops And RVI. " +
                "Buy: " +
                "1. The candle closed above the upper line of the Envelope. " +
                "2. The RVI is greater than 0 and growing. " +
                "Sell: " +
                "1. The candle closed below the bottom line of the Envelope. " +
                "2. RVI is less than 0 and falling. " +
                "Exit: the reverse side of the envelope channel.";
        }

        private void BreakEnvelops_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Envelop.Parameters[0]).ValueInt = EnvelopsLength.ValueInt;
            ((IndicatorParameterDecimal)_Envelop.Parameters[1]).ValueDecimal = EnvelopsDeviation.ValueDecimal;
            _Envelop.Save();
            _Envelop.Reload();
            ((IndicatorParameterInt)_RVI.Parameters[0]).ValueInt = _PeriodRVI.ValueInt;
            _RVI.Save();
            _RVI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyEnvelopsAndRVI";
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
            if (candles.Count < EnvelopsDeviation.ValueDecimal ||
                candles.Count < EnvelopsLength.ValueInt)
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
            // The last value of the indicator
            _lastUpLine = _Envelop.DataSeries[0].Last;
            _lastDownLine = _Envelop.DataSeries[2].Last;
            _lastSignalRVI = _RVI.DataSeries[1].Last;

            // The prev value of the indicator
            _prevSignalRVI = _RVI.DataSeries[1].Values[_RVI.DataSeries[1].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastUpLine && _lastSignalRVI > 0 && _lastSignalRVI > _prevSignalRVI)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < _lastDownLine && _lastSignalRVI < 0 && _lastSignalRVI < _prevSignalRVI)
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

            // The last value of the indicator
            _lastUpLine = _Envelop.DataSeries[0].Last;
            _lastDownLine = _Envelop.DataSeries[2].Last;

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
                    if (lastPrice < _lastDownLine)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice > _lastUpLine)
                    {
                        _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
                    }
                }
            }
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
