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
using System.Data.SqlClient;

/* Description
trading robot for osengine

The trend robot on Break Price Channel and Eom.

Buy:
1. The candle closed above the upper PC line.
2. Breakdown of the maximum for a certain number of Eom candles.

Sale:
1. The candle closed below the lower PC line.
2. Breakdown of the minimum for a certain number of Eom candles.

Exit: opposite border of the PC channel.

 */


namespace OsEngine.Robots.AO
{
    [Bot("BreakPriceChannelAndEOM")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakPriceChannelAndEOM : BotPanel
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
        private StrategyParameterInt LengthEom;
        private StrategyParameterInt PcUpLength;
        private StrategyParameterInt PcDownLength;
        private StrategyParameterInt Period;

        // Indicator
        Aindicator _EOM;
        Aindicator _PC;

        // The last value of the indicator
        private decimal _lastEOM;

        // The prev value of the indicator
        private decimal _prevUpPC;
        private decimal _prevDownPC;

        public BreakPriceChannelAndEOM(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthEom = CreateParameter("Length Eom", 10, 1, 50, 1, "Indicator");
            PcUpLength = CreateParameter("Up Line Length", 21, 7, 48, 7, "Indicator");
            PcDownLength = CreateParameter("Down Line Length", 21, 7, 48, 7, "Indicator");
            Period = CreateParameter("Period", 10, 10, 1000, 10, "Indicator");

            // Create indicator EOM
            _EOM = IndicatorsFactory.CreateIndicatorByName("EaseOfMovement", name + "EaseOfMovement", false);
            _EOM = (Aindicator)_tab.CreateCandleIndicator(_EOM, "NewArea");
            ((IndicatorParameterInt)_EOM.Parameters[0]).ValueInt = LengthEom.ValueInt;
            _EOM.Save();

            // Create indicator PC
            _PC = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PC", false);
            _PC = (Aindicator)_tab.CreateCandleIndicator(_PC, "Prime");
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = PcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = PcDownLength.ValueInt;
            _PC.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakEOMAndSma_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Break Price Channel and Eom. " +
                "Buy: " +
                "1. The candle closed above the upper PC line. " +
                "2. Breakdown of the maximum for a certain number of Eom candles. " +
                "Sale: " +
                "1. The candle closed below the lower PC line. " +
                "2. Breakdown of the minimum for a certain number of Eom candles. " +
                "Exit: opposite border of the PC channel.";
        }

        private void BreakEOMAndSma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_EOM.Parameters[0]).ValueInt = LengthEom.ValueInt;
            _EOM.Save();
            _EOM.Reload();
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = PcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = PcDownLength.ValueInt;
            _PC.Save();
            _PC.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakPriceChannelAndEOM";
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
            if (candles.Count < PcUpLength.ValueInt ||
                candles.Count < PcDownLength.ValueInt ||
                candles.Count < LengthEom.ValueInt ||
                candles.Count < Period.ValueInt)
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
            _lastEOM = _EOM.DataSeries[0].Last;

            // The prev value of the indicator
            _prevUpPC = _PC.DataSeries[0].Values[_PC.DataSeries[0].Values.Count - 2];
            _prevDownPC = _PC.DataSeries[1].Values[_PC.DataSeries[1].Values.Count - 2];

            List<decimal> value = _EOM.DataSeries[0].Values;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _prevUpPC && MaxValueOnPeriodInddicator(value, Period.ValueInt) < _lastEOM)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < _prevDownPC && MinValueOnPeriodInddicator(value, Period.ValueInt) > _lastEOM)
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

            // The prev value of the indicator
            _prevUpPC = _PC.DataSeries[0].Values[_PC.DataSeries[0].Values.Count - 2];
            _prevDownPC = _PC.DataSeries[1].Values[_PC.DataSeries[1].Values.Count - 2];

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
                    if (lastPrice < _prevDownPC)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice > _prevUpPC)
                    {
                        _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
                    }
                }
            }
        }

        private decimal MaxValueOnPeriodInddicator(List<decimal> Value, int period)
        {
            decimal max = 0;
            for (int i = 2; i <= period; i++)
            {
                if (max < Value[Value.Count - i])
                {
                    max = Value[Value.Count - i];
                }
            }
            return max;
        }

        private decimal MinValueOnPeriodInddicator(List<decimal> Value, int period)
        {
            decimal min = 0;
            for (int i = 2; i <= period; i++)
            {
                if (min < Value[Value.Count - i])
                {
                    min = Value[Value.Count - i];
                }
            }
            return min;
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
