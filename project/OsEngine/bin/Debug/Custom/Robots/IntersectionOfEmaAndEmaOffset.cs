using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;

/*Discription
Trading robot for osengine.

Trend robot at the Intersection of Ema and  Ema offset.

Buy: Fast Ema is higher than slow Ema.

Sale: Fast Ema is lower than slow Ema.

Exit from the buy: trailing stop in % of the loy of the candle on which you entered.

Exit from sale: trailing stop in % of the high of the candle on which you entered.
*/
namespace OsEngine.Robots.MyRobots
{
    [Bot("IntersectionOfEmaAndEmaOffset")]//We create an attribute so that we don't write anything in the Boot factory
    public class IntersectionOfEmaAndEmaOffset : BotPanel
    {
        BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay TimeStart;
        private StrategyParameterTimeOfDay TimeEnd;

        // Indicator Settings 
        private Aindicator _ema1;

        // Indicator Settings
        private StrategyParameterInt _periodEmaFast;

        // Indicator
        private Aindicator _ema2;

        private StrategyParameterInt _periodEmaSlow;
        private StrategyParameterInt _periodOffset;
        private StrategyParameterDecimal TrailingValue;

        // He last value of the indicators
        private decimal _lastEmaFast;
        private decimal _lastEmaSlow;

        public IntersectionOfEmaAndEmaOffset(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodEmaFast = CreateParameter("fast Ema1 period", 250, 50, 500, 50, "Indicator");
            _periodEmaSlow = CreateParameter("slow Ema2 period", 1000, 500, 1500, 100, "Indicator");
            _periodOffset = CreateParameter("offset Ema2 period", 0, 3, 10, 11, "Indicator");

            // Creating an indicator EmaFast
            _ema1 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "EMA1", canDelete: false);
            _ema1 = (Aindicator)_tab.CreateCandleIndicator(_ema1, nameArea: "Prime");
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.ParametersDigit[0].Value = _periodEmaFast.ValueInt;
            _ema1.DataSeries[0].Color = Color.Red;
            _ema1.Save();

            // Creating an indicator EmaSlow
            _ema2 = IndicatorsFactory.CreateIndicatorByName(nameClass: "OffsetEma", name: name + "Ema2", canDelete: false);
            _ema2 = (Aindicator)_tab.CreateCandleIndicator(_ema2, nameArea: "Prime");
            _ema2.ParametersDigit[0].Value = _periodEmaSlow.ValueInt;
            _ema2.ParametersDigit[1].Value = _periodOffset.ValueInt;
            _ema2.DataSeries[0].Color = Color.Green;
            _ema2.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfEmaAndEmaOffset_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            TrailingValue = CreateParameter("TrailingValue", 1, 1.0m, 10, 1, "Exit settings");

            Description = "Trading robot for osengine. " +
                "Trend robot at the Intersection of Ema and  Ema offset. " +
                "Buy: Fast Ema is higher than slow Ema. " +
                "Sale: Fast Ema is lower than slow Ema. " +
                "Exit from the buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sale: trailing stop in % of the high of the candle on which you entered.";

        }

        // Indicator Update event
        private void IntersectionOfEmaAndEmaOffset_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.Save();
            _ema1.Reload();
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            ((IndicatorParameterInt)_ema2.Parameters[1]).ValueInt = _periodOffset.ValueInt;
            _ema2.Save();
            _ema2.Reload();
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _periodEmaFast.ValueInt || candles.Count < _periodEmaSlow.ValueInt)
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

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // He last value of the indicators
                _lastEmaFast = _ema1.DataSeries[0].Last;
                _lastEmaSlow = _ema2.DataSeries[0].Last;

                // Long
                if (Regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {

                    if (_lastEmaFast > _lastEmaSlow)
                    {
                        // We put a stop on the buy
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // if the mode is not only long, then we enter short
                {
                    if (_lastEmaFast < _lastEmaSlow)
                    {
                        // putting a stop on sale
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestAsk - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }
                decimal stopPriсe;
                if (openPositions[i].Direction == Side.Buy) // if the direction of the position is buy
                {
                    decimal low = candles[candles.Count - 1].Low;
                    stopPriсe = low - low * TrailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPriсe = high + high * TrailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(pos, stopPriсe, stopPriсe);
            }
        }

        // The name of the robot in OsEngin
        public override string GetNameStrategyType()
        {
            return "IntersectionOfEmaAndEmaOffset";
        }

        public override void ShowIndividualSettingsDialog()
        {

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
