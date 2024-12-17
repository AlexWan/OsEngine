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

/* Description
trading robot for osengine

The trend robot on Break EaseOfMovement and Sma.

Buy:
1. The value of the Ease of movement indicator crosses the level 0 from the bottom up.
2. Candle closed above Sma.

Sell:
1. The value of the Ease of movement indicator crosses level 0 from top to bottom.
2. Candle closed below Sma.

Exit: on the opposite signal.

 */


namespace OsEngine.Robots.AO
{
    [Bot("BreakEOMAndSma")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakEOMAndSma : BotPanel
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
        private StrategyParameterInt PeriodSma;

        // Indicator
        Aindicator _EOM;
        Aindicator _Sma;

        // The last value of the indicator
        private decimal _lastEOM;
        private decimal _lastSma;

        // The prev value of the indicator
        private decimal _prevEOM;

        public BreakEOMAndSma(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodSma = CreateParameter("Period Sma", 10, 1, 500, 1, "Indicator");

            // Create indicator EOM
            _EOM = IndicatorsFactory.CreateIndicatorByName("EaseOfMovement", name + "EaseOfMovement", false);
            _EOM = (Aindicator)_tab.CreateCandleIndicator(_EOM, "NewArea");
            ((IndicatorParameterInt)_EOM.Parameters[0]).ValueInt = LengthEom.ValueInt;
            _EOM.Save();

            // Create indicator Sma
            _Sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _Sma = (Aindicator)_tab.CreateCandleIndicator(_Sma, "Prime");
            ((IndicatorParameterInt)_Sma.Parameters[0]).ValueInt = PeriodSma.ValueInt;
            _Sma.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakEOMAndSma_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Break EaseOfMovement and Sma. " +
                "Buy: " +
                "1. The value of the Ease of movement indicator crosses the level 0 from the bottom up. " +
                "2. Candle closed above Sma. " +
                "Sell: " +
                "1. The value of the Ease of movement indicator crosses level 0 from top to bottom. " +
                "2. Candle closed below Sma. " +
                "Exit: on the opposite signal.";
        }

        private void BreakEOMAndSma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_EOM.Parameters[0]).ValueInt = LengthEom.ValueInt;
            _EOM.Save();
            _EOM.Reload();
            ((IndicatorParameterInt)_Sma.Parameters[0]).ValueInt = PeriodSma.ValueInt;
            _Sma.Save();
            _Sma.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakEOMAndSma";
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
            if (candles.Count < PeriodSma.ValueInt ||
                candles.Count < LengthEom.ValueInt)
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
            _lastSma = _Sma.DataSeries[0].Last;

            // The prev value of the indicator
            _prevEOM = _EOM.DataSeries[0].Values[_EOM.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEOM > 0 && _prevEOM < 0 && _lastSma < lastPrice)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastEOM < 0 && _prevEOM > 0 && _lastSma > lastPrice)
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
           
            // The last value of the indicator
            _lastEOM = _EOM.DataSeries[0].Last;
            _lastSma = _Sma.DataSeries[0].Last;

            // The prev value of the indicator
            _prevEOM = _EOM.DataSeries[0].Values[_EOM.DataSeries[0].Values.Count - 2];

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastEOM < 0 && _prevEOM > 0 && _lastSma > lastPrice)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastEOM > 0 && _prevEOM < 0 && _lastSma < lastPrice)
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
