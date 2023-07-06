using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

Trend robot on Awesome Oscillator and Stochastic Oscillator

Buy:
1. Stochastic above 50 but below 80.
2. Column Awesome previous above preprevious.

Sale:
1. Stochastic below 50 but above 20.
2. Column Awesome previous below preprevious.

Exit from a long position: Stop and profit.
The stop is placed behind the minimum for the period specified for the stop (StopCandles).
Profit is equal to the size of the stop * CoefProfit (CoefProfit - how many times the size of the profit is greater than the size of the stop).

Exit from a short position: Stop and profit.
The stop is placed behind the maximum for the period specified for the stop (StopCandles).
Profit is equal to the size of the stop * CoefProfit (CoefProfit - how many times the size of the profit is greater than the size of the stop).
 
 */

namespace OsEngine.Robots.Aligator
{
    // We create an attribute so that we don't write anything to the BotFactory
    [Bot("StrategyOnAOAndStoh")]
    public class StrategyOnAOAndStoh : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // Setting indicator
        private StrategyParameterInt FastLineLengthAO;
        private StrategyParameterInt SlowLineLengthAO;
        private StrategyParameterInt StochasticPeriod1;
        private StrategyParameterInt StochasticPeriod2;
        private StrategyParameterInt StochasticPeriod3;

        // Indicator
        private Aindicator _AO;
        private Aindicator _Stochastic;

        // The last value of the indicators
        private decimal _lastStochastic;

        // The prevlast value of the indicator
        private decimal _prevAO;

        // The prevprev value of the indicator
        private decimal _prevprevAO;

        // Exit
        private StrategyParameterDecimal CoefProfit;
        private StrategyParameterInt StopCandles;

        public StrategyOnAOAndStoh(string name, StartProgram startProgram) : base(name, startProgram)
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

            // Setting indicator
            FastLineLengthAO = CreateParameter("Fast Line Length AO", 12, 10, 300, 10, "Indicator");
            SlowLineLengthAO = CreateParameter("Slow Line Length AO", 24, 10, 300, 10, "Indicator");
            StochasticPeriod1 = CreateParameter("Stochastic Period One", 10, 10, 300, 10, "Indicator");
            StochasticPeriod2 = CreateParameter("Stochastic Period Two", 20, 10, 300, 10, "Indicator");
            StochasticPeriod3 = CreateParameter("Stochastic Period Three", 30, 10, 300, 10, "Indicator");


            // Create indicator AO
            _AO = IndicatorsFactory.CreateIndicatorByName("AO", name + "AO", false);
            _AO = (Aindicator)_tab.CreateCandleIndicator(_AO, "NewArea");
            ((IndicatorParameterInt)_AO.Parameters[0]).ValueInt = FastLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_AO.Parameters[1]).ValueInt = SlowLineLengthAO.ValueInt;
            _AO.Save();

            // Create indicator Stochastic
            _Stochastic = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stochastic", false);
            _Stochastic = (Aindicator)_tab.CreateCandleIndicator(_Stochastic, "NewArea0");
            ((IndicatorParameterInt)_Stochastic.Parameters[0]).ValueInt = StochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stochastic.Parameters[1]).ValueInt = StochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_Stochastic.Parameters[2]).ValueInt = StochasticPeriod3.ValueInt;
            _Stochastic.Save();

            // Exit
            CoefProfit = CreateParameter("Coef Profit", 1, 1m, 10, 1, "Exit settings");
            StopCandles = CreateParameter("Stop Candles", 1, 2, 10, 1, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyOnAOAndStoh_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
        }

        // Indicator Update event
        private void StrategyOnAOAndStoh_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_AO.Parameters[0]).ValueInt = FastLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_AO.Parameters[1]).ValueInt = SlowLineLengthAO.ValueInt;
            _AO.Save();
            _AO.Reload();
            ((IndicatorParameterInt)_Stochastic.Parameters[0]).ValueInt = StochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stochastic.Parameters[1]).ValueInt = StochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_Stochastic.Parameters[2]).ValueInt = StochasticPeriod3.ValueInt;
            _Stochastic.Save();
            _Stochastic.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyOnAOAndStoh";
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
            if (candles.Count < SlowLineLengthAO.ValueInt || candles.Count < StochasticPeriod3.ValueInt)
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

            if (openPositions == null || openPositions.Count == 0)
            {
                // The last value of the indicators
                _lastStochastic = _Stochastic.DataSeries[0].Last;

                // The prevlast value of the indicators
                _prevAO = _AO.DataSeries[0].Values[_AO.DataSeries[0].Values.Count - 2];

                // The prevprev value of the indicators
                _prevprevAO = _AO.DataSeries[0].Values[_AO.DataSeries[0].Values.Count - 3];

                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                decimal lastPrice = candles[candles.Count - 1].Close;
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastStochastic > 50 && _lastStochastic < 80 && _prevAO > _prevprevAO)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastStochastic < 50 && _lastStochastic > 20 && _prevAO < _prevprevAO)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage);
                    }
                }
                return;
            }
        }

        //  logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal profitActivation = 0;
            decimal price = 0;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {

                    decimal stopActivation = GetPriceStop(openPositions[i].TimeCreate, Side.Buy, candles, candles.Count - 1);

                    if (stopActivation == 0)
                    {
                        return;
                    }

                    price = stopActivation;
                    profitActivation = pos.EntryPrice + (pos.EntryPrice - price) * CoefProfit.ValueDecimal;
                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal stopActivation = GetPriceStop(openPositions[i].TimeCreate, Side.Sell, candles, candles.Count - 1);

                    if (stopActivation == 0)
                    {
                        return;
                    }

                    price = stopActivation;
                    profitActivation = pos.EntryPrice - (price - pos.EntryPrice) * CoefProfit.ValueDecimal;
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
        private decimal GetPriceStop(DateTime positionCreateTime, Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < StopCandles.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                // calculate the stop price at Long
                // 1 find the minimum for the time from the opening of the transaction to the current
                decimal price = decimal.MaxValue; ;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 2].TimeStart;
                }

                for (int i = index; i > 0; i--)
                { // look at the index of the candle after which the position was opened
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - StopCandles.ValueInt; i--)
                { // look at the minimum after the opening

                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }

                return price;
            }

            if (side == Side.Sell)
            {
                // 1 find the maximum for the time from the opening of the transaction to the current
                decimal price = 0;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 1].TimeStart;
                }

                for (int i = index; i > 0; i--)
                { // look at the index of the candle after which the position was opened
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - StopCandles.ValueInt; i--)
                { // watch the maximum high

                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }

            return 0;
        }
    }
}
