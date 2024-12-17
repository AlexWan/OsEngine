using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on Break Asi.

Buy: When the ASI indicator has broken through the maximum for a certain number of candles and the ASI line is above the Sma line.

Sell: When the ASI indicator has broken through the minimum for a certain number of candles and the ASI line is below the Sma line.

Exit from buy: trailing stop in % of the loy of the candle on which you entered.

Exit from sell: trailing stop in % of the high of the candle on which you entered.

 */

namespace OsEngine.Robots.MyBots
{
    [Bot("BreakAsi")] // We create an attribute so that we don't write anything to the BotFactory
    internal class BreakAsi : BotPanel
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
        private StrategyParameterDecimal AsiLimit;
        private StrategyParameterInt AsiSmaLength;

        // Enter
        private StrategyParameterInt EntryCandlesLong;
        private StrategyParameterInt EntryCandlesShort;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        // Indicator
        Aindicator _Asi;

        public BreakAsi(string name, StartProgram startProgram) : base(name, startProgram)
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
            AsiLimit = CreateParameter("Asi Limit", 10000.0m, 100, 100000m, 1000m, "Indicator");
            AsiSmaLength = CreateParameter("Sma Length", 21, 7, 48, 7, "Indicator");

            // Enter 
            EntryCandlesLong = CreateParameter("Entry Candles Long", 10, 5, 200, 5, "Enter");
            EntryCandlesShort = CreateParameter("Entry Candles Short", 10, 5, 200, 5, "Enter");

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Create indicator Asi
            _Asi = IndicatorsFactory.CreateIndicatorByName("ASI", name + "Asi", false);
            _Asi = (Aindicator)_tab.CreateCandleIndicator(_Asi, "AsiArea");
            ((IndicatorParameterDecimal)_Asi.Parameters[0]).ValueDecimal = AsiLimit.ValueDecimal;
            ((IndicatorParameterInt)_Asi.Parameters[1]).ValueInt = AsiSmaLength.ValueInt;
            _Asi.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakAsi_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on strategy Break Asi. " +
                "Buy: When the ASI indicator has broken through the maximum for a certain number of candles and the ASI line is above the Sma line." +
                "Sell: When the ASI indicator has broken through the minimum for a certain number of candles and the ASI line is below the Sma line." +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";
        }

        private void BreakAsi_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_Asi.Parameters[0]).ValueDecimal = AsiLimit.ValueDecimal;
            ((IndicatorParameterInt)_Asi.Parameters[1]).ValueInt = AsiSmaLength.ValueInt;
            _Asi.Save();
            _Asi.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "BreakAsi";
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
            decimal lastAsi = _Asi.DataSeries[0].Last;
            decimal lastAsiSma = _Asi.DataSeries[1].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> values = _Asi.DataSeries[0].Values;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (EnterLong(values, EntryCandlesLong.ValueInt) < lastAsi && lastAsi > lastAsiSma)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (EnterShort(values, EntryCandlesShort.ValueInt) > lastAsi && lastAsi < lastAsiSma)
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

        // Method for finding the maximum for a period
        private decimal EnterLong(List<decimal> values, int period)
        {
            decimal Max = -9999999;
            for (int i = values.Count - 2; i > values.Count - 2 - period; i--)
            {
                if (values[i] > Max)
                {
                    Max = values[i];
                }
            }
            return Max;
        }

        // Method for finding the minimum for a period
        private decimal EnterShort(List<decimal> values, int period)
        {
            decimal Min = 9999999;
            for (int i = values.Count - 2; i > values.Count - 2 - period; i--)
            {
                if (values[i] < Min)
                {
                    Min = values[i];
                }
            }
            return Min;
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
