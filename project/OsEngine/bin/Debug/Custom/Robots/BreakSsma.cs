﻿using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.MyRobots
{
    [Bot("IntersectionOfPriceSsma")] //We create an attribute so that we don't write anything in the Boot factory
    public class IntersectionOfPriceSsma : BotPanel
    {
        
      BotTabSimple _tab;
        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay TimeStart;
        private StrategyParameterTimeOfDay TimeEnd;

        // Indicator Settings
        private StrategyParameterInt _ssmaPeriod;

        // indicator
        private Aindicator _ssma;
        //he last value of the indicators
        private decimal _lastSsma;

        public IntersectionOfPriceSsma(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Basic Settings
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency", "% of the total portfolio" }, "Base");

            VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");

            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");
            // Indicator Settings
            _ssmaPeriod = CreateParameter("Moving period", 15, 50, 300, 1, "Robot parameters");

            // Creating an indicator
            _ssma = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "EMA", canDelete: false);
            _ssma = (Aindicator)_tab.CreateCandleIndicator(_ssma, nameArea: "Prime");
            ((IndicatorParameterInt)_ssma.Parameters[0]).ValueInt = _ssmaPeriod.ValueInt;
            _ssma.Save();
            // Subscribe to the indicator update event
            ParametrsChangeByUser += Break_EMA_ParametrsChangeByUser;
            // subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Indicator Update event
        }
        private void Break_EMA_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ssma.Parameters[0]).ValueInt = _ssmaPeriod.ValueInt;
            _ssma.Save();
            _ssma.Reload();
        }

        // Candle Completion Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)

        {
            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }
            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _ssmaPeriod.ValueInt)
            {
                return;
            }
            // If the time does not match, we exit
            if (TimeStart.Value > _tab.TimeServerCurrent ||
                TimeEnd.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;
            // if there are positions, then go to the position closing method
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles);
            }

            // if the position closing mode, then exit the method
            if (Regime.ValueString == "OnlyClosePosition")
            {
                return;
            }
            // if there are no positions, then go to the position opening method
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
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                // long
                if (Regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    _lastSsma = _ssma.DataSeries[0].Last;
                    decimal lastPrice = candles[candles.Count - 1].Close;
                    if (lastPrice > _lastSsma)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    _lastSsma = _ssma.DataSeries[0].Last;
                    decimal lastPrice = candles[candles.Count - 1].Close;
                    if (lastPrice < _lastSsma)
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
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // if the direction of the position is purchase
                {
                    decimal lastPrice = candles[candles.Count - 1].Close;
                    _lastSsma = _ssma.DataSeries[0].Last;

                    if (lastPrice < _lastSsma)
                    {

                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
               
                else // if the direction of the position is sale
                {
                    _lastSsma = _ssma.DataSeries[0].Last;
                    decimal lastPrice = candles[candles.Count - 1].Close;
                    if (lastPrice > _lastSsma)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
            }
        }

        // method for calculating the volume of entry into a position
        private decimal GetVolume()
        {
            decimal volume = 0;

            if (VolumeRegime.ValueString == "Contract currency") // "Contract currency"
            {
                decimal contractPrice = _tab.PriceBestAsk;
                volume = VolumeOnPosition.ValueDecimal / contractPrice;
            }
            else if (VolumeRegime.ValueString == "Number of contracts")
            {
                volume = VolumeOnPosition.ValueDecimal;
            }

            // if the robot is running in the tester
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

        public override string GetNameStrategyType()
        {
            return "IntersectionOfPriceSsma";
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }
    }
}
