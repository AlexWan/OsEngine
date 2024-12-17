using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Linq;



/* Description
Trading robot for osengine.

Trend robot on channel breakdown from Linear Regression Line and ADX.

Buy: 
1. The price is above the upper line of the channel.
2. Adx is growing and crosses level 20 from bottom to top.

Sale:
1. The price is below the bottom line of the channel.
2. Adx is growing and crosses level 20 from bottom to top.

Exit: 
After a certain number of candles.
 */

namespace OsEngine.Robots.My_bots
{
    [Bot( "BreakChannelLinearRegressionLineAndADX")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakChannelLinearRegressionLineAndADX : BotPanel
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
        private StrategyParameterInt _periodADX;
        private StrategyParameterInt _periodLRMAChannel;
       

        // Indicator
        private Aindicator _ADX;
        private Aindicator _LRMAUp;
        private Aindicator _LRMADown;

        //The last value of the indicators
        private decimal _lastADX;
        private decimal _prevADX;
        private decimal _lastUpLine;
        private decimal _lastDownLine;

        // Exit
        private StrategyParameterInt ExitCandles;
       

        public BreakChannelLinearRegressionLineAndADX(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodADX = CreateParameter("Period ADX", 15, 50, 300, 10, "Indicator");
            _periodLRMAChannel = CreateParameter("Period LRMA Channel", 21, 7, 48, 7, "Indicator");
          
            // Creating an indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = _periodADX.ValueInt;
            _ADX.Save();
           
            // Creating an indicator LRMA1
            _LRMAUp = IndicatorsFactory.CreateIndicatorByName(nameClass: "LinearRegressionLine", name: name + "_LRMAUp", canDelete: false);
            _LRMAUp = (Aindicator)_tab.CreateCandleIndicator(_LRMAUp, nameArea: "Prime");
            ((IndicatorParameterInt)_LRMAUp.Parameters[0]).ValueInt = _periodLRMAChannel.ValueInt;
            ((IndicatorParameterString)_LRMAUp.Parameters[1]).ValueString = "High";
            _LRMAUp.Save();

            // Creating an indicator LRMA2
            _LRMADown = IndicatorsFactory.CreateIndicatorByName(nameClass: "LinearRegressionLine", name: name + "_LRMADown", canDelete: false);
            _LRMADown = (Aindicator)_tab.CreateCandleIndicator(_LRMADown, nameArea: "Prime");
            ((IndicatorParameterInt)_LRMADown.Parameters[0]).ValueInt = _periodLRMAChannel.ValueInt;
            ((IndicatorParameterString)_LRMADown.Parameters[1]).ValueString = "Low";
            _LRMADown.Save();

            // Exit
            ExitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit settings");
            

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakChannelLinearRegressionLineAndADX_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on channel breakdown from Linear Regression Line and ADX." +
                "Buy:" +
                "1.The price is above the upper line of the channel." +
                "2.Adx is growing and crosses level 20 from bottom to top." +
                "Sale:" +
                "1.The price is below the bottom line of the channel." +
                "2.Adx is growing and crosses level 20 from bottom to top." +
                "Exit: " +
                "After a certain number of candles.";
        }

        // Indicator Update event
        private void BreakChannelLinearRegressionLineAndADX_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = _periodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();

            ((IndicatorParameterInt)_LRMAUp.Parameters[0]).ValueInt = _periodLRMAChannel.ValueInt;
            _LRMAUp.Save();
            _LRMAUp.Reload();

            ((IndicatorParameterInt)_LRMADown.Parameters[0]).ValueInt = _periodLRMAChannel.ValueInt;
            _LRMADown.Save();
            _LRMADown.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakChannelLinearRegressionLineAndADX";
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
            if (candles.Count < _periodADX.ValueInt || candles.Count < _periodLRMAChannel.ValueInt)               
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
                decimal lastPrice = candles[candles.Count - 1].Close;
                
                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // The prev value of the indicator
                _prevADX = _ADX.DataSeries[0].Values[_ADX.DataSeries[0].Values.Count - 2];

                // The last value of the indicators               
                _lastADX = _ADX.DataSeries[0].Last;
                _lastUpLine = _LRMAUp.DataSeries[0].Last;
                _lastDownLine = _LRMADown.DataSeries[0].Last;
            
                // Long
                if (Regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastUpLine  
                        && _prevADX < _lastADX && _lastADX > 20)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if (lastPrice < _lastDownLine
                        && _prevADX < _lastADX && _lastADX > 20)
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
    }
}