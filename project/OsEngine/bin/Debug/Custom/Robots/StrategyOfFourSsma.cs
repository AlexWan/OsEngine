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

Trend robot at the stratege  with two Ssma channels.

Buy:
1. The price is above the slow channel (above the upper line) and above the fast (upper line);
2. The bottom line of the fast channel is higher than the top line of the slow channel.

Sale:
1. The price is below the slow channel (below the lower line) and below the fast channel (below the lower line);
2. The upper line of the fast channel is lower than the lower line of the slow channel.

Exit: stop and profit.
*/


namespace OsEngine.Robots.MyRobots
{
    [Bot("StrategyOfFourSsma")]// We create an attribute so that we don't write anything in the Boot factory
    public class StrategyOfFourSsma : BotPanel
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
        private Aindicator _ssmaUp1;
        private Aindicator _ssmaDown1;
        private Aindicator _ssmaUp2;
        private Aindicator _ssmaDown2;


        // Indicator Settings        
        private StrategyParameterInt _periodSsmaChannelFast;
        private StrategyParameterInt _periodSsmaChannelSlow;

        // Thee last value of the indicators
        private decimal _lastSsmaUp1;
        private decimal _lastSsmaDown1;
        private decimal _lastSsmaUp2;
        private decimal _lastSsmaDown2;

        // Exit
        private StrategyParameterDecimal CoefProfit;
        private StrategyParameterInt StopCandles;

        public StrategyOfFourSsma(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency", "Number of contracts" }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator Setting
            _periodSsmaChannelFast = CreateParameter("Ssma Channel Length Fast", 10, 50, 50, 400, "Indicator");
            _periodSsmaChannelSlow = CreateParameter("Ssma Channel Length Slow", 30, 50, 50, 400, "Indicator");


            // Creating an indicator SsmaUp1
            _ssmaUp1 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ssma", name: name + "SsmaUp1", canDelete: false);
            _ssmaUp1 = (Aindicator)_tab.CreateCandleIndicator(_ssmaUp1, nameArea: "Prime");
            ((IndicatorParameterInt)_ssmaUp1.Parameters[0]).ValueInt = _periodSsmaChannelFast.ValueInt;
            ((IndicatorParameterString)_ssmaUp1.Parameters[1]).ValueString = "High";
            _ssmaUp1.ParametersDigit[0].Value = _periodSsmaChannelFast.ValueInt;
            _ssmaUp1.DataSeries[0].Color = Color.Yellow;
            _ssmaUp1.Save();

            // Creating an indicator SsmaDown1
            _ssmaDown1 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ssma", name: name + "SsmaDown1", canDelete: false);
            _ssmaDown1 = (Aindicator)_tab.CreateCandleIndicator(_ssmaDown1, nameArea: "Prime");
            ((IndicatorParameterInt)_ssmaDown1.Parameters[0]).ValueInt = _periodSsmaChannelFast.ValueInt;
            ((IndicatorParameterString)_ssmaDown1.Parameters[1]).ValueString = "Low";
            _ssmaDown1.ParametersDigit[0].Value = _periodSsmaChannelFast.ValueInt;
            _ssmaDown1.DataSeries[0].Color = Color.Yellow;
            _ssmaDown1.Save();

            // Creating an indicator SsmaUp2
            _ssmaUp2 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ssma", name: name + "SsmaUp2", canDelete: false);
            _ssmaUp2 = (Aindicator)_tab.CreateCandleIndicator(_ssmaUp2, nameArea: "Prime");
            ((IndicatorParameterInt)_ssmaUp2.Parameters[0]).ValueInt = _periodSsmaChannelSlow.ValueInt;
            ((IndicatorParameterString)_ssmaUp2.Parameters[1]).ValueString = "High";
            _ssmaUp2.ParametersDigit[0].Value = _periodSsmaChannelSlow.ValueInt;
            _ssmaUp2.DataSeries[0].Color = Color.AliceBlue;

            // Creating an indicator SsmaDown2
            _ssmaDown2 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ssma", name: name + "SsmaDown2", canDelete: false);
            _ssmaDown2 = (Aindicator)_tab.CreateCandleIndicator(_ssmaDown2, nameArea: "Prime");
            ((IndicatorParameterInt)_ssmaDown2.Parameters[0]).ValueInt = _periodSsmaChannelSlow.ValueInt;
            ((IndicatorParameterString)_ssmaDown2.Parameters[1]).ValueString = "Low";
            _ssmaDown2.ParametersDigit[0].Value = _periodSsmaChannelSlow.ValueInt;
            _ssmaDown2.DataSeries[0].Color = Color.AliceBlue;
            _ssmaDown2.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfFourSsma_ParametrsChangeByUser;
            // subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Exit
            CoefProfit = CreateParameter("Coef Profit", 1, 1m, 10, 1, "Exit settings");
            StopCandles = CreateParameter("Stop Candles", 1, 2, 10, 1, "Exit settings");
        }
       
        // Indicator Update event
        private void IntersectionOfFourSsma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ssmaUp1.Parameters[0]).ValueInt = _periodSsmaChannelFast.ValueInt;
            _ssmaUp1.Save();
            _ssmaUp1.Reload();

            ((IndicatorParameterInt)_ssmaDown1.Parameters[0]).ValueInt = _periodSsmaChannelFast.ValueInt;
            _ssmaDown1.Save();
            _ssmaDown1.Reload();

            ((IndicatorParameterInt)_ssmaUp2.Parameters[0]).ValueInt = _periodSsmaChannelSlow.ValueInt;
            _ssmaUp2.Save();
            _ssmaUp2.Reload();

            ((IndicatorParameterInt)_ssmaDown2.Parameters[0]).ValueInt = _periodSsmaChannelSlow.ValueInt;
            _ssmaDown2.Save();
            _ssmaDown2.Reload();
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }
            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _periodSsmaChannelFast.ValueInt || candles.Count < _periodSsmaChannelSlow.ValueInt)
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
            _lastSsmaUp1 = _ssmaUp1.DataSeries[0].Last;
            _lastSsmaDown1 = _ssmaDown1.DataSeries[0].Last;
            _lastSsmaUp2 = _ssmaUp2.DataSeries[0].Last;
            _lastSsmaDown2 = _ssmaDown2.DataSeries[0].Last;

            {
                decimal slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                decimal lastPrice = candles[candles.Count - 1].Close;
               
                // Long
                if (Regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {

                    if (lastPrice > _lastSsmaUp1 && lastPrice > _lastSsmaUp2
                        && _lastSsmaDown1 > _lastSsmaUp2)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + slippage);
                    }
                }
                
                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (lastPrice < _lastSsmaDown1 && lastPrice < _lastSsmaDown2
                        && _lastSsmaUp1 < _lastSsmaDown2)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - slippage);
                    }
                }
            }
        }


        //  Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {

            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal profitActivation;
            decimal price;

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
                // We calculate the stop price at Long
                // We find the minimum for the time from the opening of the transaction to the current one
                decimal price = decimal.MaxValue; ;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 2].TimeStart;
                }

                for (int i = index; i > 0; i--)
                {
                    // Look at the index of the candle, after which the opening of the pose occurred
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - StopCandles.ValueInt; i--)
                { 
                    // Looking at the minimum after opening
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }

                return price;
            }

            if (side == Side.Sell)
            {
                //  We find the maximum for the time from the opening of the transaction to the current one
                decimal price = 0;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 1].TimeStart;
                }

                for (int i = index; i > 0; i--)
                { 
                    // Look at the index of the candle, after which the opening of the pose occurred
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - StopCandles.ValueInt; i--)
                {
                    // Looking at the maximum high
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }

            return 0;
        }
        // The name of the robot in OsEngin
        public override string GetNameStrategyType()
        {
            return "StrategyOfFourSsma";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }
    }
}



