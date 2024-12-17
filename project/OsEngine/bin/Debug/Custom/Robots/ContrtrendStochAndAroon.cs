using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The contrtrend robot on Stochastic And Aroon.

Buy: When the Aroon Up line is above 70 and Stochastic has left the oversold zone (above 30).

Sell: When the Aroon Down line is above 70 and Stochastic has left the overbought zone (below 80).

Buy exit: When the Aroon Up line is below 60.

Sell ​​exit: When the Aroon Down line is below 60.
 
 */

namespace OsEngine.Robots.MyBots
{
    [Bot("ContrtrendStochAndAroon")] // We create an attribute so that we don't write anything to the BotFactory
    internal class ContrtrendStochAndAroon : BotPanel
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
        private StrategyParameterInt AroonLength;
        private StrategyParameterInt StochPeriod1;
        private StrategyParameterInt StochPeriod2;
        private StrategyParameterInt StochPeriod3;

        // Indicator
        Aindicator _Aroon;
        Aindicator _Stoh;

        public ContrtrendStochAndAroon(string name, StartProgram startProgram) : base(name, startProgram)
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

            AroonLength = CreateParameter("Aroon Length", 14, 1, 200, 1, "Indicator");
            StochPeriod1 = CreateParameter("Stoch Period 1", 9, 3, 40, 1, "Indicator");
            StochPeriod2 = CreateParameter("Stoch Period 2", 5, 2, 40, 1, "Indicator");
            StochPeriod3 = CreateParameter("Stoch Period 3", 3, 2, 40, 1, "Indicator");

            // Create indicator Aroon
            _Aroon = IndicatorsFactory.CreateIndicatorByName("Aroon", name + "Aroon", false);
            _Aroon = (Aindicator)_tab.CreateCandleIndicator(_Aroon, "AroonArea");
            ((IndicatorParameterInt)_Aroon.Parameters[0]).ValueInt = AroonLength.ValueInt;
            _Aroon.Save();

            // Create indicator Stoh
            _Stoh = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stoch", false);
            _Stoh = (Aindicator)_tab.CreateCandleIndicator(_Stoh, "StochArea");
            ((IndicatorParameterInt)_Stoh.Parameters[0]).ValueInt = StochPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[1]).ValueInt = StochPeriod2.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[2]).ValueInt = StochPeriod3.ValueInt;
            _Stoh.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += ContrtrendStochAndAroon_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The contrtrend robot on Stochastic And Aroon." +
                "Buy: When the Aroon Up line is above 70 and Stochastic has left the oversold zone (above 30). " +
                "Sell: When the Aroon Down line is above 70 and Stochastic has left the overbought zone (below 80). " +
                "Buy exit: When the Aroon Up line is below 60." +
                "Sell ​​exit: When the Aroon Down line is below 60.";

        }   

        private void ContrtrendStochAndAroon_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Aroon.Parameters[0]).ValueInt = AroonLength.ValueInt;
            _Aroon.Save();
            _Aroon.Reload();

            ((IndicatorParameterInt)_Stoh.Parameters[0]).ValueInt = StochPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[1]).ValueInt = StochPeriod2.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[2]).ValueInt = StochPeriod3.ValueInt;
            _Stoh.Save();
            _Stoh.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "ContrtrendStochAndAroon";
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
            if (candles.Count < StochPeriod1.ValueInt ||
                candles.Count < AroonLength.ValueInt ||
                candles.Count < StochPeriod2.ValueInt ||
                candles.Count < StochPeriod3.ValueInt)
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
            decimal aroonUp = _Aroon.DataSeries[0].Last;
            decimal aroonDown = _Aroon.DataSeries[1].Last;
            decimal stoch = _Stoh.DataSeries[0].Last;

            // The prev value of the indicator
            decimal prevStoh = _Stoh.DataSeries[0].Values[_Stoh.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;
                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (aroonUp > 70 && stoch > 30 && prevStoh < 30)
                    {
                        _tab.BuyAtLimit(GetVolume(), lastPrice + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (aroonDown > 70 && stoch < 80 && prevStoh > 80)
                    {
                        _tab.SellAtLimit(GetVolume(), lastPrice - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
           
            // The prev value of the indicator
            decimal aroonUp = _Aroon.DataSeries[0].Last;
            decimal aroonDown = _Aroon.DataSeries[1].Last;
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = _tab.Securiti.PriceStep * Slippage.ValueDecimal / 100;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (aroonUp < 60)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (aroonDown < 60)
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
