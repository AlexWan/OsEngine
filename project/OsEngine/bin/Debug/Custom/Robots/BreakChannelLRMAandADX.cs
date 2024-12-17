using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

/*Discription
Trading robot for osengine

Trend robot at the Break Channel LRMA and ADX.

Buy:
1. The price is above the upper line of the channel.
2. Adx is growing and crosses level 20 from bottom to top.
Sell:
1. The price is below the bottom line of the channel.
2. Adx is growing and crosses level 20 from bottom to top.
Exit: after a certain number of candles.
*/

namespace OsEngine.Robots.MyRobots
{
    [Bot("BreakChannelLRMAandADX")] //We create an attribute so that we don't write anything in the Boot factory
    public class BreakChannelLRMAandADX : BotPanel
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
        private Aindicator _OneLRMA;
        private Aindicator _TwoLRMA;
        Aindicator _ADX;

        // Indicator setting
        private StrategyParameterInt _periodLRMA;
        private StrategyParameterInt PeriodADX;

        // The last value of the indicators
        private decimal _lastOneLRMA;
        private decimal _lastTwoLRMA;
        private decimal _lastADX;

        // The prev value of the indicator
        private decimal _prevADX;

        // Exit
        private StrategyParameterInt ExitCandles;

        public BreakChannelLRMAandADX(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodLRMA = CreateParameter("period LRMA", 14, 5, 50, 5, "Indicator");
            PeriodADX = CreateParameter("Period ADX", 10, 10, 300, 10, "Indicator");

            // Creating indicator One LRMA
            _OneLRMA = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LinearRegressionLine1", false);
            _OneLRMA = (Aindicator)_tab.CreateCandleIndicator(_OneLRMA, "Prime");
            ((IndicatorParameterInt)_OneLRMA.Parameters[0]).ValueInt = _periodLRMA.ValueInt;
            ((IndicatorParameterString)_OneLRMA.Parameters[1]).ValueString = "High";
            _OneLRMA.Save();

            // Creating indicator Two LRMA
            _TwoLRMA = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LinearRegressionLine2", false);
            _TwoLRMA = (Aindicator)_tab.CreateCandleIndicator(_TwoLRMA, "Prime");
            ((IndicatorParameterInt)_TwoLRMA.Parameters[0]).ValueInt = _periodLRMA.ValueInt;
            ((IndicatorParameterString)_OneLRMA.Parameters[1]).ValueString = "Low";
            _TwoLRMA.Save();

            // Create indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea0");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();
            
            // Exit
            ExitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit settings");
            
            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakChannelLRMAandADX_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            

            Description = "Trend robot at the Break Channel LRMA and ADX. " +
                "Buy: " +
                "1. The price is above the upper line of the channel. " +
                "2. Adx is growing and crosses level 20 from bottom to top. " +
                "Sell: " +
                "1. The price is below the bottom line of the channel. " +
                "2. Adx is growing and crosses level 20 from bottom to top. " +
                "Exit: after a certain number of candles.";

        }

        // Indicator Update event
        private void BreakChannelLRMAandADX_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_OneLRMA.Parameters[0]).ValueInt = _periodLRMA.ValueInt;
            _OneLRMA.Save();
            _OneLRMA.Reload();
            ((IndicatorParameterInt)_TwoLRMA.Parameters[0]).ValueInt = _periodLRMA.ValueInt;
            _TwoLRMA.Save();
            _TwoLRMA.Reload();
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakChannelLRMAandADX";
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
            if (candles.Count < _periodLRMA.ValueInt || candles.Count < PeriodADX.ValueInt)
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
                    _lastOneLRMA = _OneLRMA.DataSeries[0].Last;
                    _lastTwoLRMA = _TwoLRMA.DataSeries[0].Last;
                    _lastADX = _ADX.DataSeries[0].Last;

                    // The prev value of the indicator
                    _prevADX = _ADX.DataSeries[0].Values[_ADX.DataSeries[0].Values.Count - 2];

                    if (lastPrice > _lastOneLRMA && _lastADX > _prevADX && _lastADX > 20 && _prevADX < 20)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (lastPrice < _lastTwoLRMA && _lastADX > _prevADX && _lastADX > 20 && _prevADX < 20)
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


