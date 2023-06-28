﻿using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine

Trend robot on the EMA indicator

Bay: the price of the instrument is higher than the Ema.

Sale: the price of the instrument is below the Ema.

Output: on the opposite signal.
*/

namespace OsEngine.Robots.MyRobots

{
    [Bot("BreakEMA")] //We create an attribute so that we don't write anything in the Boot factory

    public class BreakEMA : BotPanel
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
        private StrategyParameterInt _emaPeriod;
        
        // Indicator
        private Aindicator _ema;
       
        //The last value of the indicators
        private decimal _lastMa;

        public BreakEMA(string name, StartProgram startProgram) : base(name, startProgram)
        {
            
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
           
            // Basic Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency", "% of the total portfolio" }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");            
            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");
            
            // Indicator Settings
            _emaPeriod = CreateParameter("Moving period", 15, 50, 300, 1, "Robot parameters");

            // Creating an indicator
            _ema = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "EMA", canDelete: false);
            _ema = (Aindicator)_tab.CreateCandleIndicator(_ema, nameArea: "Prime");
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _emaPeriod.ValueInt;
            _ema.Save();
            
            // Subscribe to the indicator update event
            ParametrsChangeByUser += Break_EMA_ParametrsChangeByUser;
           
            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;                      
        }

        // Indicator Update event
        private void Break_EMA_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _emaPeriod.ValueInt;
            _ema.Save();
            _ema.Reload();
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
            if (candles.Count < _emaPeriod.ValueInt)
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
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                
                // The last value of the indicators               
                _lastMa = _ema.DataSeries[0].Last;
                decimal lastPrice = candles[candles.Count - 1].Close;
               
                // Long
                if (Regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                   
                    if (lastPrice > _lastMa)
                    {
                       
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {                    
                    if (lastPrice < _lastMa)
                    {
                       
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage);
                    }
                }
                return;
            }
        }

        // Logic close position 
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;         
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
            decimal lastPrice = candles[candles.Count - 1].Close;
            _lastMa = _ema.DataSeries[0].Last;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {                                       
                     if (lastPrice < _lastMa)
                        {
                        
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {

                    if (lastPrice > _lastMa)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice + _slippage, openPositions[0].OpenVolume);
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
        
        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakEMA";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }

}

