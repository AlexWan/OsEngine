using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;

/*Discription
Trading robot for osengine

Trend robot at the Strategy Rsi And Two LRMA.

Buy:
1. Fast LRMA crosses slow one from bottom to top.
2. The RSI is above 50 and rising.
Sell:
1. The fast LRMA crosses the slow one from top to bottom.
2. The RSI is above 50 and rising.
Exit: stop and profit in % of the entry price.
*/

namespace OsEngine.Robots.MyRobots
{
    [Bot("StrategyRsiAndTwoLRMA")] //We create an attribute so that we don't write anything in the Boot factory
    public class StrategyRsiAndTwoLRMA : BotPanel
    {
        BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay TimeStart;
        private StrategyParameterTimeOfDay TimeEnd;

        // Indicator
        private Aindicator _FastLRMA;
        private Aindicator _SlowLRMA;
        Aindicator _RSI;

        // Indicator setting
        private StrategyParameterInt _periodFastLRMA;
        private StrategyParameterInt _periodSlowLRMA;
        private StrategyParameterInt PeriodRSI;

        // The last value of the indicators
        private decimal _lastFastLRMA;
        private decimal _lastSlowLRMA;
        private decimal _lastRSI;

        // The prev value of the indicator
        private decimal _prevRSI;
        private decimal _prevFastLRMA;
        private decimal _prevSlowLRMA;

        // Exit
        private StrategyParameterDecimal StopValue;
        private StrategyParameterDecimal ProfitValue;

        public StrategyRsiAndTwoLRMA(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodFastLRMA = CreateParameter("period Fast LRMA", 14, 5, 50, 5, "Indicator");
            _periodSlowLRMA = CreateParameter("period Slow LRMA", 24, 10, 100, 10, "Indicator");
            PeriodRSI = CreateParameter("Period RSI", 14, 10, 300, 1, "Indicator");

            // Creating indicator Fast LRMA
            _FastLRMA = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LinearRegressionLine1", false);
            _FastLRMA = (Aindicator)_tab.CreateCandleIndicator(_FastLRMA, "Prime");
            ((IndicatorParameterInt)_FastLRMA.Parameters[0]).ValueInt = _periodFastLRMA.ValueInt;
            _FastLRMA.DataSeries[0].Color = Color.Red;
            _FastLRMA.Save();

            // Creating indicator Slow LRMA
            _SlowLRMA = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LinearRegressionLine2", false);
            _SlowLRMA = (Aindicator)_tab.CreateCandleIndicator(_SlowLRMA, "Prime");
            ((IndicatorParameterInt)_SlowLRMA.Parameters[0]).ValueInt = _periodSlowLRMA.ValueInt;
            _SlowLRMA.DataSeries[0].Color = Color.Green;
            _SlowLRMA.Save();

            // Create indicator RSI
            _RSI = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _RSI = (Aindicator)_tab.CreateCandleIndicator(_RSI, "NewArea");
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = PeriodRSI.ValueInt;
            _RSI.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyRsiAndTwoLRMA_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Exit
            StopValue = CreateParameter("Stop", 0.5m, 1, 10, 1, "Exit settings");
            ProfitValue = CreateParameter("Profit", 0.5m, 1, 10, 1, "Exit settings");

            Description = "Trend robot at the Strategy Rsi And Two LRMA. " +
                "Buy: " +
                "1. Fast LRMA crosses slow one from bottom to top. " +
                "2. The RSI is above 50 and rising. " +
                "Sell: " +
                "1. The fast LRMA crosses the slow one from top to bottom. " +
                "2. The RSI is above 50 and rising. " +
                "Exit: stop and profit in % of the entry price.";

        }

        // Indicator Update event
        private void StrategyRsiAndTwoLRMA_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_FastLRMA.Parameters[0]).ValueInt = _periodFastLRMA.ValueInt;
            _FastLRMA.Save();
            _FastLRMA.Reload();
            ((IndicatorParameterInt)_SlowLRMA.Parameters[0]).ValueInt = _periodSlowLRMA.ValueInt;
            _SlowLRMA.Save();
            _SlowLRMA.Reload();
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = PeriodRSI.ValueInt;
            _RSI.Save();
            _RSI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyRsiAndTwoLRMA";
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
            if (candles.Count < _periodFastLRMA.ValueInt || candles.Count < _periodSlowLRMA.ValueInt)
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
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    // The last value of the indicators
                    _lastFastLRMA = _FastLRMA.DataSeries[0].Last;
                    _lastSlowLRMA = _SlowLRMA.DataSeries[0].Last;
                    _lastRSI = _RSI.DataSeries[0].Last;

                    // The prev value of the indicator
                    _prevRSI = _RSI.DataSeries[0].Values[_RSI.DataSeries[0].Values.Count - 2];
                    _prevFastLRMA = _FastLRMA.DataSeries[0].Values[_FastLRMA.DataSeries[0].Values.Count - 2];
                    _prevSlowLRMA = _SlowLRMA.DataSeries[0].Values[_SlowLRMA.DataSeries[0].Values.Count - 2];

                    if (_prevFastLRMA < _prevSlowLRMA && _lastFastLRMA > _lastSlowLRMA && _lastRSI > 50 && _lastRSI > _prevRSI)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_prevFastLRMA > _prevSlowLRMA && _lastFastLRMA < _lastSlowLRMA && _lastRSI > 50 && _lastRSI > _prevRSI)
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
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal profitActivation = pos.EntryPrice + pos.EntryPrice * ProfitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice - pos.EntryPrice * StopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal profitActivation = pos.EntryPrice - pos.EntryPrice * ProfitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice + pos.EntryPrice * StopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
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


