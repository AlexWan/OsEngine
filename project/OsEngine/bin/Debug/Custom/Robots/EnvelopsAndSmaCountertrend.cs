using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Trading robot for osengine.

The countertrend robot on Envelops and Sma.

Buy:
1. The price is below the lower Envelop line.
2. Sma below the lower Envelop line.
3. Sma growing.

Sell: 
1. The price is above the upper Envelop line.
2. Sma above the upper Envelop line.
3. Sma falling.

Exit from the buy: 
Trailing stop in % of the loy of the candle on which you entered.

Exit from the sell:
Trailing stop in % of the high of the candle on which you entered.
 */

namespace OsEngine.Robots.My_bots
{
    [Bot("EnvelopsAndSmaCountertrend")] // We create an attribute so that we don't write anything to the BotFactory
    public class EnvelopsAndSmaCountertrend : BotPanel
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
        private StrategyParameterInt EnvelopsLength;
        private StrategyParameterDecimal EnvelopsDeviation;
        private StrategyParameterInt PeriodSma;

        // Indicator
        Aindicator _Envelops;
        Aindicator _Sma;

        // The last value of the indicator
        private decimal _lastUpLine;
        private decimal _lastDownLine;
        private decimal _lastSma;

        // The prev value of the indicator
        private decimal _prevSma;

        // Exit
        private StrategyParameterInt TrailCandles;

        public EnvelopsAndSmaCountertrend(string name, StartProgram startProgram) : base(name, startProgram)
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
            EnvelopsLength = CreateParameter("Envelop Length ", 21, 7, 48, 7, "Indicator");
            EnvelopsDeviation = CreateParameter("Envelop Deviation ", 1.0m, 1, 5, 0.1m, "Indicator");

            PeriodSma = CreateParameter("Period Sma", 100, 10, 300, 10, "Indicator");

            // Create indicator Sma
            _Sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _Sma = (Aindicator)_tab.CreateCandleIndicator(_Sma, "Prime");
            ((IndicatorParameterInt)_Sma.Parameters[0]).ValueInt = PeriodSma.ValueInt;
            _Sma.Save();

            // Create indicator Envelops
            _Envelops = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "_EnvelopsLoc", false);
            _Envelops = (Aindicator)_tab.CreateCandleIndicator(_Envelops, "Prime");
            ((IndicatorParameterInt)_Envelops.Parameters[0]).ValueInt = EnvelopsLength.ValueInt;
            ((IndicatorParameterDecimal)_Envelops.Parameters[1]).ValueDecimal = EnvelopsDeviation.ValueDecimal;
            _Envelops.Save();

            // Exit
            TrailCandles = CreateParameter("Stop Value", 1, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += EnvelopsAndSmaCountertrend_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The countertrend robot on Envelops and Ema." +
                    "Buy:" +
                    "1.The price is below the lower Envelop line." +
                    "2.Sma below the lower Envelop line." +
                    "3.Sma growing." +
                    "Sell: " +
                    "1.The price is above the upper Envelop line." +
                    "2.Sma above the upper Envelop line." +
                    "3.Sma falling." +
                    "Exit from the buy: " +
                    "Trailing stop in % of the loy of the candle on which you entered." +
                    "Exit from the sell:" +
                    "Trailing stop in % of the high of the candle on which you entered.";
        }
        private void EnvelopsAndSmaCountertrend_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Envelops.Parameters[0]).ValueInt = EnvelopsLength.ValueInt;
            ((IndicatorParameterDecimal)_Envelops.Parameters[1]).ValueDecimal = EnvelopsDeviation.ValueDecimal;
            _Envelops.Save();
            _Envelops.Reload();

            ((IndicatorParameterInt)_Sma.Parameters[0]).ValueInt = PeriodSma.ValueInt;
            _Sma.Save();
            _Sma.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "EnvelopsAndSmaCountertrend";
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
            if (candles.Count < EnvelopsLength.ValueInt || candles.Count < EnvelopsDeviation.ValueDecimal ||
                candles.Count < PeriodSma.ValueInt)
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
            _lastUpLine = _Envelops.DataSeries[0].Last;
            _lastDownLine = _Envelops.DataSeries[2].Last;
            _lastSma = _Sma.DataSeries[0].Last;

            // The prev value of the indicator
            _prevSma = _Sma.DataSeries[0].Values[_Sma.DataSeries[0].Values.Count - 2];
            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice < _lastDownLine && _lastSma < _lastDownLine && _prevSma < _lastSma)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice > _lastUpLine && _lastSma > _lastUpLine && _prevSma > _lastSma)
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
                    stopPrice = lov - lov * TrailCandles.ValueInt / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailCandles.ValueInt / 100;
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