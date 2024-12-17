using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine.

Trend robot on the intersection of EMA and LRMA.

Buy: 
The Ema is higher than the LRMA.

Sale: 
The Ema is lower than the LRMA.

Exit from the buy: 
Trailing stop in % of the loy of the candle on which you entered.

Exit from the sell:
Trailing stop in % of the high of the candle on which you entered.
*/

namespace OsEngine.Robots.My_bots
{
    [Bot("IntersectionOfEmaAndLRMA")] //We create an attribute so that we don't write anything in the Boot factory
    public class IntersectionOfEmaAndLRMA : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // Indicator Settings
        private StrategyParameterInt _periodEma;
        private StrategyParameterInt _periodLRMA;
        // Indicator
        private Aindicator _ema;
        private Aindicator _LRMA;


        //The last value of the indicators
        private decimal _lastMa;
        private decimal _lastLRMA;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        public IntersectionOfEmaAndLRMA(string name, StartProgram startProgram) : base(name, startProgram)
        {

            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency" }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            StartTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            EndTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator Settings
            _periodEma = CreateParameter("Moving period", 15, 50, 300, 10, "Indicator");
            _periodLRMA= CreateParameter("LRMA period", 10, 50, 200, 10, "Indicator");

            // Creating an indicator EMA
            _ema = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "Ema", canDelete: false);
            _ema = (Aindicator)_tab.CreateCandleIndicator(_ema, nameArea: "Prime");
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _periodEma.ValueInt;
            _ema.Save();

            // Creating an indicator LRMA
            _LRMA = IndicatorsFactory.CreateIndicatorByName(nameClass: "LinearRegressionLine", name: name + "LRMA", canDelete: false);
            _LRMA = (Aindicator)_tab.CreateCandleIndicator(_LRMA, nameArea: "Prime");
            ((IndicatorParameterInt)_LRMA.Parameters[0]).ValueInt = _periodLRMA.ValueInt;
            _LRMA.Save();

            // Exit
            TrailingValue = CreateParameter("TrailingValue", 1, 1.0m, 10, 1, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfEmaAndLRMA_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on the intersection of EMA and LRMA." +
                "Buy:" +
                "The Ema is higher than the LRMA." +
                "Sale:" +
                "The Ema is lower than the LRMA." +
                "Exit from the buy: " +
                "Trailing stop in % of the loy of the candle on which you entered." +
                "Exit from the sell:" +
                "Trailing stop in % of the high of the candle on which you entered.";
        }

        // Indicator Update event
        private void IntersectionOfEmaAndLRMA_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _periodEma.ValueInt;
            _ema.Save();
            _ema.Reload();

            ((IndicatorParameterInt)_LRMA.Parameters[0]).ValueInt = _periodLRMA.ValueInt;
            _LRMA.Save();
            _LRMA.Reload();


        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionOfEmaAndLRMA";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Completion Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)

        {
            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _periodEma.ValueInt || candles.Count < _periodLRMA.ValueInt)
            {
                return;
            }

            // If the time does not match, we exit
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

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // The last value of the indicators               
                _lastMa = _ema.DataSeries[0].Last;
                _lastLRMA = _LRMA.DataSeries[0].Last;
                

                // Long
                if (Regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                     if (_lastMa > _lastLRMA)
                        {

                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if (_lastMa < _lastLRMA)
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
            
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }
                decimal stopPriсe;
                if (pos.Direction == Side.Buy) // If the direction of the position is buy
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

