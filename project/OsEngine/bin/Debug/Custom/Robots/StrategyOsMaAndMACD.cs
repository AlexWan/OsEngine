using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on Strategy OsMa and MACD.

Buy: When the previous value of the OsMa histogram was below zero, and the current value was above zero, 
and the MACD histogram was above zero.

Sell: When the previous value of the OsMa histogram was above zero, and the current value was below zero, 
and the MACD histogram was below zero.

Exit:
From buy: When the OsMa histogram value is below zero.
From sell: When the OsMa histogram value is above zero.
 */

namespace OsEngine.Robots.MyBots
{
    [Bot("StrategyOsMaAndMACD")] // We create an attribute so that we don't write anything to the BotFactory
    internal class StrategyOsMaAndMACD : BotPanel
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
        private StrategyParameterInt LenghtFastLineOsMa;
        private StrategyParameterInt LenghtSlowLineOsMa;
        private StrategyParameterInt LenghtSignalLineOsMa;
        private StrategyParameterInt LengthFastLineMACD;
        private StrategyParameterInt LengthSlowLineMACD;
        private StrategyParameterInt LengthSignalLineMACD;

        // Indicator
        Aindicator _OsMa;
        Aindicator _MACD;

        public StrategyOsMaAndMACD(string name, StartProgram startProgram) : base(name, startProgram)
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
            LenghtFastLineOsMa = CreateParameter("OsMa Fast Length", 12, 10, 100, 10, "Indicator");
            LenghtSlowLineOsMa = CreateParameter("OsMa Slow Length", 26, 20, 300, 10, "Indicator");
            LenghtSignalLineOsMa = CreateParameter("OsMa Signal Length", 9, 9, 300, 10, "Indicator");
            LengthFastLineMACD = CreateParameter("MACD Fast Length", 12, 10, 100, 10, "Indicator");
            LengthSlowLineMACD = CreateParameter("MACD Slow Length", 26, 20, 300, 10, "Indicator");
            LengthSignalLineMACD = CreateParameter("MACD Signal Length", 9, 10, 300, 10, "Indicator");

            // Create indicator OsMa
            _OsMa = IndicatorsFactory.CreateIndicatorByName("OsMa", name + "OsMa", false);
            _OsMa = (Aindicator)_tab.CreateCandleIndicator(_OsMa, "OsMaArea");
            ((IndicatorParameterInt)_OsMa.Parameters[0]).ValueInt = LenghtFastLineOsMa.ValueInt;
            ((IndicatorParameterInt)_OsMa.Parameters[1]).ValueInt = LenghtSlowLineOsMa.ValueInt;
            ((IndicatorParameterInt)_OsMa.Parameters[2]).ValueInt = LenghtSignalLineOsMa.ValueInt;
            _OsMa.Save();

            // Create indicator MACD
            _MACD = IndicatorsFactory.CreateIndicatorByName("MACD", name + "Macd", false);
            _MACD = (Aindicator)_tab.CreateCandleIndicator(_MACD, "MacdArea");
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = LengthFastLineMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = LengthSlowLineMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = LengthSignalLineMACD.ValueInt;
            _MACD.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyOsMa_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy OsMa and MACD. " +
               "Buy: When the previous value of the OsMa histogram was below zero, and the current value was above zero, " +
               "and the MACD histogram was above zero." +
               "Sell: When the previous value of the OsMa histogram was above zero, and the current value was below zero, " +
               "and the MACD histogram was below zero." +
               "Exit: " +
               "From buy:  When the OsMa histogram value is below zero." +
               "From sell: When the OsMa histogram value is above zero.";
        }        

        private void StrategyOsMa_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_OsMa.Parameters[0]).ValueInt = LenghtFastLineOsMa.ValueInt;
            ((IndicatorParameterInt)_OsMa.Parameters[1]).ValueInt = LenghtSlowLineOsMa.ValueInt;
            ((IndicatorParameterInt)_OsMa.Parameters[2]).ValueInt = LenghtSignalLineOsMa.ValueInt;
            _OsMa.Save();
            _OsMa.Reload();

            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = LengthFastLineMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = LengthSlowLineMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = LengthSignalLineMACD.ValueInt;
            _MACD.Save();
            _MACD.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyOsMaAndMACD";
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
            if (candles.Count < LengthFastLineMACD.ValueInt || candles.Count < LengthSlowLineMACD.ValueInt ||
                candles.Count < LengthSignalLineMACD.ValueInt || candles.Count < LenghtFastLineOsMa.ValueInt || 
                candles.Count < LenghtSlowLineOsMa.ValueInt ||  candles.Count < LenghtSignalLineOsMa.ValueInt)
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
            decimal lastMACD = _MACD.DataSeries[0].Last;
            decimal lastOsMa = _OsMa.DataSeries[2].Last;

            // The prev value of the indicator
            decimal prevOsMa = _OsMa.DataSeries[2].Values[_OsMa.DataSeries[2].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastMACD > 0 && lastOsMa > 0 && prevOsMa < 0)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastMACD < 0 && lastOsMa < 0 && prevOsMa > 0)
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

            decimal lastPrice = candles[candles.Count - 1].Close;

            // The last value of the indicator
            decimal lastMACD = _MACD.DataSeries[0].Last;
            decimal lastOsMa = _OsMa.DataSeries[2].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (lastOsMa < 0)
                    {

                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {

                    if (lastOsMa > 0)
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
    }
}
