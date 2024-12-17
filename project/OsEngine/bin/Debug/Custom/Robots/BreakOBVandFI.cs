using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Linq;

/* Description
trading robot for osengine

The trend robot on strategy Break OBV and FI.

Buy:
1. The value of the OBV indicator broke through the maximum for a certain number of candles and closed higher.
2. The values of the force index indicator cross the 0 level from bottom to top.

Sell:
1. The value of the OBV indicator broke through the minimum for a certain number of candles and closed lower.
2. The values of the force index indicator cross the level 0 from top to bottom.

Exit from buy: trailing stop in % of the loy of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.
 
 */


namespace OsEngine.Robots.AO
{
    [Bot("BreakOBVandFI")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakOBVandFI : BotPanel
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
        private StrategyParameterInt LengthFI;

        // Indicator
        Aindicator _FI;
        Aindicator _OBV;

        // Enter
        private StrategyParameterInt EntryCandlesLong;
        private StrategyParameterInt EntryCandlesShort;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        // The last value of the indicator
        private decimal _lastFI;

        // The prev value of the indicator
        private decimal _prevFI;

        public BreakOBVandFI(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthFI = CreateParameter("FI Length", 21, 7, 48, 7, "Indicator");

            // Create indicator FI
            _FI = IndicatorsFactory.CreateIndicatorByName("ForceIndex", name + "ForceIndex", false);
            _FI = (Aindicator)_tab.CreateCandleIndicator(_FI, "NewArea");
            ((IndicatorParameterInt)_FI.Parameters[0]).ValueInt = LengthFI.ValueInt;
            _FI.Save();

            // Create indicator OBV
            _OBV = IndicatorsFactory.CreateIndicatorByName("OBV", name + "OBV", false);
            _OBV = (Aindicator)_tab.CreateCandleIndicator(_OBV, "NewArea0");
            _OBV.Save();

            // Enter 
            EntryCandlesLong = CreateParameter("Entry Candles Long", 10, 5, 200, 5, "Enter");
            EntryCandlesShort = CreateParameter("Entry Candles Short", 10, 5, 200, 5, "Enter");

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakOBVandFI_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on strategy Break OBV and FI. " +
                "Buy: " +
                "1. The value of the OBV indicator broke through the maximum for a certain number of candles and closed higher. " +
                "2. The values of the force index indicator cross the 0 level from bottom to top. " +
                "Sell: " +
                "1. The value of the OBV indicator broke through the minimum for a certain number of candles and closed lower. " +
                "2. The values of the force index indicator cross the level 0 from top to bottom. " +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";
        }

        // Indicator update event
        private void BreakOBVandFI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_FI.Parameters[0]).ValueInt = LengthFI.ValueInt;
            _FI.Save();
            _FI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakOBVandFI";
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
            if (candles.Count < EntryCandlesShort.ValueInt + 10 || candles.Count < EntryCandlesLong.ValueInt + 10)
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

            // The last value of the indicator
            _lastFI = _FI.DataSeries[0].Last;

            // The prev value of the indicator
            _prevFI = _FI.DataSeries[0].Values[_FI.DataSeries[0].Values.Count - 2];

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> values = _OBV.DataSeries[0].Values;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {


                    if (EnterLongAndShort(values, EntryCandlesLong.ValueInt) == "true" &&
                        _lastFI > 0 && _prevFI < 0)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (EnterLongAndShort(values, EntryCandlesShort.ValueInt) == "false" &&
                        _lastFI < 0 && _prevFI > 0)
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

            decimal stopPrice;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * TrailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);
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

        private string EnterLongAndShort(List<decimal> values, int period)
        {
            decimal Max = -9999999;
            decimal Min = 9999999;
            for (int i = 1; i <= period; i++)
            {
                if (values[values.Count - 1 - i] > Max)
                {
                    Max = values[values.Count - 1 - i];
                }
                if (values[values.Count - 1 - i] < Min)
                {
                    Min = values[values.Count - 1 - i];
                }               
            }
            if (Max < values[values.Count - 1])
            {
                return "true";
            }
            else if (Min > values[values.Count - 1])
            {
                return "false";
            }
            return "nope";
        }
        
    }
}