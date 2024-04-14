﻿using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on Parabolic SAR and Aroon

Buy: When the price is above the Parabolic SAR value and the Aroon Up line is above 50 and above the Aroon Down line.

Sell: When the price is below the Parabolic SAR value and the Aroon Down line is above 50 and above the Aroon Up line.

Exit the position:

From buying when the price is below the Parabolic SAR value.

From sale when the price is higher than the Parabolic SAR value.

 */
namespace OsEngine.Robots.MyBots
{
    [Bot("BreakParabolicAndAroon")] // We create an attribute so that we don't write anything to the BotFactory
    internal class BreakParabolicAndAroon : BotPanel
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
        private StrategyParameterDecimal Step;
        private StrategyParameterDecimal MaxStep;
        private StrategyParameterInt AroonLength;

        // Indicator
        Aindicator _PS;
        Aindicator _Aroon;
        public BreakParabolicAndAroon(string name, StartProgram startProgram) : base(name, startProgram)
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
            Step = CreateParameter("Step", 0.02m, 0.001m, 3, 0.001m, "Indicator");
            MaxStep = CreateParameter("MaxStep", 0.2m, 0.01m, 1, 0.01m, "Indicator");
            AroonLength = CreateParameter("Aroon Length", 14, 1, 200, 1, "Indicator");

            // Create indicator Parabolic Sar
            _PS = IndicatorsFactory.CreateIndicatorByName(nameClass: "ParabolicSAR", name: name + "Parabolic", canDelete: false);
            _PS = (Aindicator)_tab.CreateCandleIndicator(_PS, nameArea: "Prime");
            ((IndicatorParameterDecimal)_PS.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_PS.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _PS.Save();

            // Create indicator Aroon
            _Aroon = IndicatorsFactory.CreateIndicatorByName("Aroon", name + "Aroon", false);
            _Aroon = (Aindicator)_tab.CreateCandleIndicator(_Aroon, "AroonArea");
            ((IndicatorParameterInt)_Aroon.Parameters[0]).ValueInt = AroonLength.ValueInt;
            _Aroon.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakPCAndAroon_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            // Successful position opening event
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

            Description = "The trend robot on Parabolic SAR and Aroon" +
                "Buy: When the price is above the Parabolic SAR value and the Aroon Up line is above 50 and above the Aroon Down line. " +
                "Sell: When the price is below the Parabolic SAR value and the Aroon Down line is above 50 and above the Aroon Up line. " +
                "Exit the position:  From buying when the price is below the Parabolic SAR value." +
                "From sale when the price is higher than the Parabolic SAR value.";
        }

        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            _tab.SellAtStopCancel();
            _tab.BuyAtStopCancel();
        }

        private void BreakPCAndAroon_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_PS.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_PS.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _PS.Save();
            _PS.Reload();

            ((IndicatorParameterInt)_Aroon.Parameters[0]).ValueInt = AroonLength.ValueInt;
            _Aroon.Save();
            _Aroon.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "BreakParabolicAndAroon";
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
            // If there are less than 20 candles, then we exit the method
            if (candles.Count < 20)
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
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal lastSar = _PS.DataSeries[0].Last;
            decimal aroonUp = _Aroon.DataSeries[0].Last;
            decimal aroonDown = _Aroon.DataSeries[1].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (aroonUp > aroonDown && aroonUp > 50)
                    {
                        if (lastPrice > lastSar)
                        {
                            return;
                        }

                        decimal _slippage = Slippage.ValueDecimal * lastSar / 100;
                        _tab.BuyAtStopCancel();
                        _tab.BuyAtStop(GetVolume(), lastSar + _slippage, lastSar, StopActivateType.HigherOrEqual, 1);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (aroonDown > aroonUp && aroonDown > 50)
                    {
                        if (lastPrice < lastSar)
                        {
                            return;
                        }

                        decimal _slippage = Slippage.ValueDecimal * lastSar / 100;
                        _tab.SellAtStopCancel();
                        _tab.SellAtStop(GetVolume(), lastSar - _slippage, lastSar, StopActivateType.LowerOrEqyal, 1);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal lastSar = _PS.DataSeries[0].Last;

            for (int i = 0; i < openPositions.Count; i++)
            {
                _tab.SellAtStopCancel();
                _tab.BuyAtStopCancel();
                Position pos = openPositions[0];

                if (pos.CloseActiv == true && pos.CloseOrders != null && pos.CloseOrders.Count > 0)
                {
                    return;
                }

                decimal priceLine = lastSar;
                decimal priceOrder = lastSar;
                decimal _slippage = Slippage.ValueDecimal * priceOrder / 100;

                if (pos.Direction == Side.Buy)
                {
                    _tab.CloseAtStop(pos, priceLine, priceOrder - _slippage);
                }
                else if (pos.Direction == Side.Sell)
                {
                    _tab.CloseAtStop(pos, priceLine, priceOrder + _slippage);
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
