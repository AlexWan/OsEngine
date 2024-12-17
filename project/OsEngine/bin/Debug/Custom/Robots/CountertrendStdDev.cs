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
using OsEngine.Logging;
using System.Windows.Media.Animation;
using System.Security.Cryptography;

/* Description
trading robot for osengine

The countertrend robot on StdDev.

Buy:
1. Standard Deviation above MinValue.
2. Two candles are falling.
Sell:
1. Standard Deviation is higher than MinValue.
2. Two candles growing.

Exit: after a certain number of candles.
 */


namespace OsEngine.Robots.AO
{
    [Bot("CountertrendStdDev")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendStdDev : BotPanel
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
        private StrategyParameterInt _PeriodStdDev;
        private StrategyParameterDecimal MinValue;

        // Indicator
        Aindicator _StdDev;

        // The last value of the indicator
        private decimal _lastSD;

        // Exit
        private StrategyParameterInt ExitCandles;

        public CountertrendStdDev(string name, StartProgram startProgram) : base(name, startProgram)
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
            _PeriodStdDev = CreateParameter("StdDev Length", 20, 20, 48, 7, "Indicator");
            MinValue = CreateParameter("MinValue", 0.2m, 10, 200, 10, "Indicator");

            // Create indicator StdDev
            _StdDev = IndicatorsFactory.CreateIndicatorByName("StdDev", name + "StdDev", false);
            _StdDev = (Aindicator)_tab.CreateCandleIndicator(_StdDev, "NewArea");
            ((IndicatorParameterInt)_StdDev.Parameters[0]).ValueInt = _PeriodStdDev.ValueInt;
            _StdDev.Save();

            // Exit
            ExitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendStdDev_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The countertrend robot on StdDev. " +
                "Buy: " +
                "1. Standard Deviation above MinValue. " +
                "2. Two candles are falling. " +
                "Sell: " +
                "1. Standard Deviation is higher than MinValue. " +
                "2. Two candles growing. " +
                "Exit: after a certain number of candles.";
        }

        private void CountertrendStdDev_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_StdDev.Parameters[0]).ValueInt = _PeriodStdDev.ValueInt;
            _StdDev.Save();
            _StdDev.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendStdDev";
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
            if (candles.Count < _PeriodStdDev.ValueInt)
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
            _lastSD = _StdDev.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastSD > MinValue.ValueDecimal && candles[candles.Count - 1].IsDown && candles[candles.Count - 2].IsDown)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastSD > MinValue.ValueDecimal && candles[candles.Count - 1].IsUp && candles[candles.Count - 2].IsUp)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage, time.ToString());
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

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (!NeedClosePosition(position, candles))
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    _tab.CloseAtLimit(position, lastPrice - _slippage, position.OpenVolume);
                }
                else // If the direction of the position is sale
                {
                    _tab.CloseAtLimit(position, lastPrice + _slippage, position.OpenVolume);
                }

            }
        }

        private bool NeedClosePosition(Position position, List<Candle> candles)
        {
            if (position == null || position.OpenVolume == 0)
            {
                return false;
            }

            DateTime openTime = DateTime.Parse(position.SignalTypeOpen);

            int counter = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                counter++;
                DateTime candelTime = candles[i].TimeStart;
                if (candelTime == openTime)
                {
                    if (counter >= ExitCandles.ValueInt + 1)
                    {
                        return true;
                    }
                }
            }
            return false;
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
