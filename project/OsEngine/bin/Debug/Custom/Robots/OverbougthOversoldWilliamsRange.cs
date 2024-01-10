using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine.

Trend robot on Overbougth Oversold WilliamsRange.

Buy:
1. The price of the instrument is higher than the Ssma and the Ssma is rising.
2. WR leaves the oversold zone, crossing the -80 mark from bottom to top.
Sell:
1. The price of the instrument is lower than the Ssma and the Ssma is falling.
2. WR leaves the overbought zone, crossing the -20 mark from top to bottom.

Exit: stop and profit in % of the entry price.
*/

namespace OsEngine.Robots.MyRobots
{
    [Bot("OverbougthOversoldWilliamsRange")] //We create an attribute so that we don't write anything in the Boot factory
    public class OverbougthOversoldWilliamsRange : BotPanel
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
        private StrategyParameterInt _PeriodWilliams;

        // Indicator
        private Aindicator _ssma;
        private Aindicator _Williams;

        // Exit
        private StrategyParameterDecimal StopValue;
        private StrategyParameterDecimal ProfitValue;

        // The last value of the indicator
        private decimal _lastSsma;
        private decimal _lastWilliams;

        // The prev value of the indicator
        private decimal _prevSsma;
        private decimal _prevWilliams;

        public OverbougthOversoldWilliamsRange(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency", }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator Settings
            _ssmaPeriod = CreateParameter("Moving period", 15, 50, 300, 1, "Indicator");
            _PeriodWilliams = CreateParameter("Period Williams", 14, 50, 300, 1, "Indicator");

            // Creating an indicator Ssma
            _ssma = IndicatorsFactory.CreateIndicatorByName("Ssma", name + "Ssma", false);
            _ssma = (Aindicator)_tab.CreateCandleIndicator(_ssma, nameArea: "Prime");
            ((IndicatorParameterInt)_ssma.Parameters[0]).ValueInt = _ssmaPeriod.ValueInt;
            _ssma.Save();

            // Creating an indicator WilliamsRange
            _Williams = IndicatorsFactory.CreateIndicatorByName("WilliamsRange", name + "WilliamsRange", false);
            _Williams = (Aindicator)_tab.CreateCandleIndicator(_Williams, "NewArea");
            ((IndicatorParameterInt)_Williams.Parameters[0]).ValueInt = _PeriodWilliams.ValueInt;
            _Williams.Save();

            // Exit
            StopValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");
            ProfitValue = CreateParameter("Profit Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += OverbougthOversoldWilliamsRange_ParametrsChangeByUser;

            // subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on Overbougth Oversold WilliamsRange. " +
                "Buy: " +
                "1. The price of the instrument is higher than the Ssma and the Ssma is rising. " +
                "2. WR leaves the oversold zone, crossing the -80 mark from bottom to top. " +
                "Sell: " +
                "1. The price of the instrument is lower than the Ssma and the Ssma is falling. " +
                "2. WR leaves the overbought zone, crossing the -20 mark from top to bottom. " +
                "Exit: stop and profit in % of the entry price.";

        }

        // Indicator Update event
        private void OverbougthOversoldWilliamsRange_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ssma.Parameters[0]).ValueInt = _ssmaPeriod.ValueInt;
            _ssma.Save();
            _ssma.Reload();
            ((IndicatorParameterInt)_Williams.Parameters[0]).ValueInt = _PeriodWilliams.ValueInt;
            _Williams.Save();
            _Williams.Reload();
        }
        
        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "OverbougthOversoldWilliamsRange";
        }

        public override void ShowIndividualSettingsDialog()
        {

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

            // The last value of the indicator
            _lastSsma = _ssma.DataSeries[0].Last;
            _lastWilliams = _Williams.DataSeries[0].Last;

            // The prev value of the indicator
            _lastSsma = _ssma.DataSeries[0].Values[_ssma.DataSeries[0].Values.Count - 2];
            _prevWilliams = _Williams.DataSeries[0].Values[_Williams.DataSeries[0].Values.Count - 2];

            decimal lastPrice = candles[candles.Count - 1].Close;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastSsma && _prevWilliams < -80 && _lastWilliams > -80)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, we enter the short
                {
                    if (lastPrice < _lastSsma && _prevWilliams > -20 && _lastWilliams < -20)
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
