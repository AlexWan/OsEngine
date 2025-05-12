using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine.

Trend robot on the Ssma indicator.

Buy: the price of the instrument is higher than the Ssma.

Sell: the price of the instrument is below the Ssma.

Exit: on the opposite signal.
*/

namespace OsEngine.Robots.MyRobots
{
    [Bot("BreakSsma")] //We create an attribute so that we don't write anything in the Boot factory
    public class BreakSsma : BotPanel
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

        // Indicator
        private Aindicator _ssma;

        // He last value of the indicators
        private decimal _lastSsma;

        public BreakSsma(string name, StartProgram startProgram) : base(name, startProgram)
        {            
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            
            // Basic Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency",  }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator Settings
            _ssmaPeriod = CreateParameter("Moving period", 15, 50, 300, 1, "Indicator");

            // Creating an indicator
            _ssma = IndicatorsFactory.CreateIndicatorByName("Ssma", name + "Ssma", false);
            _ssma = (Aindicator)_tab.CreateCandleIndicator(_ssma, "Prime");
            ((IndicatorParameterInt)_ssma.Parameters[0]).ValueInt = _ssmaPeriod.ValueInt;
            _ssma.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakSsma_ParametrsChangeByUser;

            // subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on the Ssma indicator. " +
                "Buy: the price of the instrument is higher than the Ssma. " +
                "Sell: the price of the instrument is below the Ssma. " +
                "Exit: on the opposite signal.";

        }

        // Indicator Update event
        private void BreakSsma_ParametrsChangeByUser()
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
            
            // The last value of the indicators
            _lastSsma = _ssma.DataSeries[0].Last;
            
            decimal lastPrice = candles[candles.Count - 1].Close;
           
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
           
            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastSsma)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, we enter the short
                {                    
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
           
            // The last value of the indicators 
            _lastSsma = _ssma.DataSeries[0].Last;
           
            decimal lastPrice = candles[candles.Count - 1].Close;
            
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
           
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is buy
                { 
                    if (lastPrice < _lastSsma)
                    {

                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }

                else // If the direction of the position is sale
                {
                   
                    if (lastPrice > _lastSsma)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
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
            return "BreakSsma";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}
