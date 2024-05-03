using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;

/*Discription
Trading robot for osengine

Robot at the trend SmaChannel And SMI.

Buy:
The candle has closed below the lower SmaChannel line and the stochastic (violet) line is below a certain level.

Sell:
the candle closed above the upper SmaChannel line and the stochastic (violet) line is above a certain level.

Exit: 
We set the stop and profit as a percentage of the entry price.
*/

namespace OsEngine.Robots.MyRobots
{
    [Bot("StrategySmaChannelAndSMI")] //We create an attribute so that we don't write anything in the Boot factory
    public class StrategySmaChannelAndSMI : BotPanel
    {
        BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay TimeStart;
        private StrategyParameterTimeOfDay TimeEnd;

        // Indicator
        Aindicator _SMI;
        Aindicator _SmaChannel;

        // Indicator setting
        private StrategyParameterInt StochasticPeriod1;
        private StrategyParameterInt StochasticPeriod2;
        private StrategyParameterInt StochasticPeriod3;
        private StrategyParameterInt StochasticPeriod4;
        private StrategyParameterInt SmaLength;
        private StrategyParameterDecimal SmaDeviation;
        private StrategyParameterDecimal OverboughtLine;
        private StrategyParameterDecimal OversoldLine;


        // The last value of the indicators
        private decimal _lastSMI;
        private decimal _lastUpSma;
        private decimal _lastDownSma;

        // Exit
        private StrategyParameterDecimal StopValue;
        private StrategyParameterDecimal ProfitValue;

        public StrategySmaChannelAndSMI(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency" }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator Settings
            StochasticPeriod1 = CreateParameter("Stochastic Period One", 10, 10, 300, 10, "Indicator");
            StochasticPeriod2 = CreateParameter("Stochastic Period Two", 26, 10, 300, 10, "Indicator");
            StochasticPeriod3 = CreateParameter("Stochastic Period Three", 3, 10, 300, 10, "Indicator");
            StochasticPeriod4 = CreateParameter("Stochastic Period 4", 2, 10, 300, 10, "Indicator");
            SmaLength = CreateParameter("SmaLength", 21, 10, 300, 10, "Indicator");
            SmaDeviation = CreateParameter("SmaDeviation", 2.0m, 10, 300, 10, "Indicator");
            OverboughtLine = CreateParameter("OverboughtLine", 2.0m, 10, 300, 10, "Indicator");
            OversoldLine = CreateParameter("OversoldLine", 2.0m, 10, 300, 10, "Indicator");

            // Create indicator SmaChannel
            _SmaChannel = IndicatorsFactory.CreateIndicatorByName("SmaChannel", name + "SmaChannel", false);
            _SmaChannel = (Aindicator)_tab.CreateCandleIndicator(_SmaChannel, "Prime");
            ((IndicatorParameterInt)_SmaChannel.Parameters[0]).ValueInt = SmaLength.ValueInt;
            ((IndicatorParameterDecimal)_SmaChannel.Parameters[1]).ValueDecimal = SmaDeviation.ValueDecimal;
            _SmaChannel.Save();

            // Create indicator Stochastic
            _SMI = IndicatorsFactory.CreateIndicatorByName("StochasticMomentumIndex", name + "StochasticMomentumIndex", false);
            _SMI = (Aindicator)_tab.CreateCandleIndicator(_SMI, "NewArea0");
            ((IndicatorParameterInt)_SMI.Parameters[0]).ValueInt = StochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[1]).ValueInt = StochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[2]).ValueInt = StochasticPeriod3.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[3]).ValueInt = StochasticPeriod4.ValueInt;
            _SMI.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategySmaChannelAndSMI_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Exit
            StopValue = CreateParameter("Stop", 0.5m, 1, 10, 1, "Exit settings");
            ProfitValue = CreateParameter("Profit", 0.5m, 1, 10, 1, "Exit settings");

            Description = "Robot at the trend SmaChannel And SMI. " +
                "Buy: " +
                "The candle has closed below the lower SmaChannel line and the stochastic (violet) line is below a certain level. " +
                "Sell: " +
                "the candle closed above the upper SmaChannel line and the stochastic (violet) line is above a certain level. " +
                "Exit:  " +
                "We set the stop and profit as a percentage of the entry price.";

        }

        // Indicator Update event
        private void StrategySmaChannelAndSMI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_SmaChannel.Parameters[0]).ValueInt = SmaLength.ValueInt;
            ((IndicatorParameterDecimal)_SmaChannel.Parameters[1]).ValueDecimal = SmaDeviation.ValueDecimal;
            _SmaChannel.Save();
            _SmaChannel.Reload();
            ((IndicatorParameterInt)_SMI.Parameters[0]).ValueInt = StochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[1]).ValueInt = StochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[2]).ValueInt = StochasticPeriod3.ValueInt;
            ((IndicatorParameterInt)_SMI.Parameters[3]).ValueInt = StochasticPeriod4.ValueInt;
            _SMI.Save();
            _SMI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategySmaChannelAndSMI";
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
            if (candles.Count < SmaLength.ValueInt || 
                candles.Count < StochasticPeriod1.ValueInt || 
                candles.Count < StochasticPeriod2.ValueInt || 
                candles.Count < StochasticPeriod3.ValueInt || 
                candles.Count < StochasticPeriod4.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
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
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    // The last value of the indicator
                    _lastSMI = _SMI.DataSeries[0].Last;
                    _lastUpSma = _SmaChannel.DataSeries[0].Last;
                    _lastDownSma = _SmaChannel.DataSeries[2].Last;

                    if (_lastDownSma < lastPrice && _lastSMI < OversoldLine.ValueDecimal)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastUpSma > lastPrice && _lastSMI > OverboughtLine.ValueDecimal)
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

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
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


