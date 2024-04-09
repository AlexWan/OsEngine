using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;

/* Description
trading robot for osengine

The trend robot on Stochastic And CCI.

Buy:
1. The CCI line is in the range between levels 100 and -100, and is directed upwards.
2. Stochastic crosses level 20 from bottom to top.

Sell:
1. The CCI line is in the range between levels 100 and -100, and is directed downwards.
2. Stochastic crosses the level 80 from top to bottom.

Buy exit: trailing stop in % of the line of the candle on which you entered.

Sell ​​exit: trailing stop in % of the high of the candle where you entered.
 
 */


namespace OsEngine.Robots.AO
{
    [Bot("TraderStohAndCCI")] // We create an attribute so that we don't write anything to the BotFactory
    public class TraderStohAndCCI : BotPanel
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
        private StrategyParameterInt PeriodCCI;
        private StrategyParameterInt StochPeriod1;
        private StrategyParameterInt StochPeriod2;
        private StrategyParameterInt StochPeriod3;

        // Indicator
        Aindicator _CCI;
        Aindicator _Stoh;

        // The last value of the indicator
        private decimal _lastCCI;
        private decimal _lastStoh;

        // The prev value of the indicator
        private decimal _prevCCI;
        private decimal _prevStoh;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        public TraderStohAndCCI(string name, StartProgram startProgram) : base(name, startProgram)
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

            PeriodCCI = CreateParameter("Period CCI", 21, 10, 300, 10, "Indicator");
            StochPeriod1 = CreateParameter("Stoch Period 1", 5, 3, 40, 1, "Indicator");
            StochPeriod2 = CreateParameter("Stoch Period 2", 3, 2, 40, 1, "Indicator");
            StochPeriod3 = CreateParameter("Stoch Period 3", 3, 2, 40, 1, "Indicator");

            // Create indicator CCI
            _CCI = IndicatorsFactory.CreateIndicatorByName("CCI", name + "CCI", false);
            _CCI = (Aindicator)_tab.CreateCandleIndicator(_CCI, "NewArea");
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = PeriodCCI.ValueInt;
            _CCI.DataSeries[0].Color = Color.GreenYellow;
            _CCI.Save();

            // Create indicator Stoh
            _Stoh = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stoh", false);
            _Stoh = (Aindicator)_tab.CreateCandleIndicator(_Stoh, "NewArea0");
            ((IndicatorParameterInt)_Stoh.Parameters[0]).ValueInt = StochPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[1]).ValueInt = StochPeriod2.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[2]).ValueInt = StochPeriod3.ValueInt;
            _Stoh.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += TraderStohAndCCI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Stochastic And CCI. " +
                "Buy: " +
                "1. The CCI line is in the range between levels 100 and -100, and is directed upwards. " +
                "2. Stochastic crosses level 20 from bottom to top. " +
                "Sell: " +
                "1. The CCI line is in the range between levels 100 and -100, and is directed downwards. " +
                "2. Stochastic crosses the level 80 from top to bottom. " +
                "Buy exit: trailing stop in % of the line of the candle on which you entered. " +
                "Sell ​​exit: trailing stop in % of the high of the candle where you entered.";
        }

        private void TraderStohAndCCI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = PeriodCCI.ValueInt;
            _CCI.Save();
            _CCI.Reload();
            ((IndicatorParameterInt)_Stoh.Parameters[0]).ValueInt = StochPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[1]).ValueInt = StochPeriod2.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[2]).ValueInt = StochPeriod3.ValueInt;
            _Stoh.Save();
            _Stoh.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "TraderStohAndCCI";
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
            if (candles.Count < StochPeriod1.ValueInt ||
                candles.Count < PeriodCCI.ValueInt ||
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
            _lastCCI = _CCI.DataSeries[0].Last;
            _lastStoh = _Stoh.DataSeries[0].Last;

            // The prev value of the indicator
            _prevCCI = _CCI.DataSeries[0].Values[_CCI.DataSeries[0].Values.Count - 2];
            _prevStoh = _Stoh.DataSeries[0].Values[_Stoh.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> values = _Stoh.DataSeries[0].Values;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastCCI > _prevCCI && -100 < _lastCCI && _lastCCI < 100 && _prevStoh < 20 && _lastStoh > 20)
                    {
                        _tab.BuyAtLimit(GetVolume(), lastPrice + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastCCI < _prevCCI && -100 < _lastCCI && _lastCCI < 100 && _prevStoh > 80 && _lastStoh < 80)
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
            Position pos = openPositions[0];

            decimal stopPrice;

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
    }
}