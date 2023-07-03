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
Trading robot for osengine.

Trend strategy on 4 EMAS and a channel of two EMAS (any slips and different output).

The channel consists of two Emas of the same length with a closing price of high and loy.

Buy:
1. Ema 1 is growing (i.e. the value of 2 candles ago was lower than 1 candle ago);
2. Ema2 is higher than Ema3;
3. The price is above Ema4 and above the upper line of the Ema channel.

Sale:
1. Ema1 falling (i.e. the value of 2 candles ago was higher than 1 candle ago);
2. Ema2 is lower than Ema3;
3. The price is below Ema4 and below the lower line of the Ema channel.

Exit from the purchase: The price is lower than Ema4.
Exit from sale: The price is higher than Ema4.
*/

namespace OsEngine.Robots.MyRobots
{
    [Bot("StrategyOfFourEma")]//We create an attribute so that we don't write anything in the Boot factory
    public class StrategyOfFourEma : BotPanel
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
        private Aindicator _ema1;
        private Aindicator _ema2;
        private Aindicator _ema3;
        private Aindicator _ema4;
        private Aindicator _emaUp;
        private Aindicator _emaDown;

        // Indicator Settings 
        private StrategyParameterInt _periodEma1;
        private StrategyParameterInt _periodEma2;
        private StrategyParameterInt _periodEma3;
        private StrategyParameterInt _periodEma4;
        private StrategyParameterInt _periodEmaChannel;

        // Thee last value of the indicators
        private decimal _lastEma2;
        private decimal _lastEma3;
        private decimal _lastEma4;
        private decimal _lastEmaUp;
        private decimal _lastEmaDown;

        public StrategyOfFourEma(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodEma1 = CreateParameter(" EMA1 period", 100, 10, 300, 1, "indicator");
            _periodEma2 = CreateParameter(" EMA2 period", 200, 10, 300, 1, "indicator");
            _periodEma3 = CreateParameter(" EMA3 period", 300, 10, 300, 1, "indicator");
            _periodEma4 = CreateParameter(" EMA4 period", 400, 10, 300, 1, "indicator");
            _periodEmaChannel = CreateParameter("EMA Channel Length", 10, 50, 50, 400, "indicator");

            // Creating indicator Ema1
            _ema1 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "Ema1", canDelete: false);
            _ema1 = (Aindicator)_tab.CreateCandleIndicator(_ema1, nameArea: "Prime");
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEma1.ValueInt;// Indicator Settings
            _ema1.ParametersDigit[0].Value = _periodEma1.ValueInt;
            _ema1.DataSeries[0].Color = Color.Red;
            _ema1.Save();

            // Creating indicator Ema2
            _ema2 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "Ema2", canDelete: false);
            _ema2 = (Aindicator)_tab.CreateCandleIndicator(_ema2, nameArea: "Prime");
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodEma3.ValueInt;
            _ema2.ParametersDigit[0].Value = _periodEma3.ValueInt;
            _ema2.DataSeries[0].Color = Color.Blue;
            _ema2.Save();

            // Creating indicator Ema3
            _ema3 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "Ema3", canDelete: false);
            _ema3 = (Aindicator)_tab.CreateCandleIndicator(_ema3, nameArea: "Prime");
            ((IndicatorParameterInt)_ema3.Parameters[0]).ValueInt = _periodEma3.ValueInt;
            _ema3.ParametersDigit[0].Value = _periodEma4.ValueInt;
            _ema3.DataSeries[0].Color = Color.Green;
            _ema3.Save();

            // Creating indicator Ema4
            _ema4 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "Ema4", canDelete: false);
            _ema4 = (Aindicator)_tab.CreateCandleIndicator(_ema4, nameArea: "Prime");
            ((IndicatorParameterInt)_ema4.Parameters[0]).ValueInt = _periodEma4.ValueInt;
            _ema4.ParametersDigit[0].Value = _periodEma4.ValueInt;
            _ema4.DataSeries[0].Color = Color.Aqua;
            _ema4.Save();

            // Creating indicator EmaUp
            _emaUp = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "EmaUp", canDelete: false);
            _emaUp = (Aindicator)_tab.CreateCandleIndicator(_emaUp, nameArea: "Prime");
            ((IndicatorParameterInt)_emaUp.Parameters[0]).ValueInt = _periodEmaChannel.ValueInt;
            ((IndicatorParameterString)_emaUp.Parameters[1]).ValueString = "High";
            _emaUp.ParametersDigit[0].Value = _periodEmaChannel.ValueInt;
            _emaUp.DataSeries[0].Color = Color.BlueViolet;
            _emaUp.Save();
            
            // Creating indicator EmaDown
            _emaDown = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "EmaDown", canDelete: false);
            _emaDown = (Aindicator)_tab.CreateCandleIndicator(_emaDown, nameArea: "Prime");
            ((IndicatorParameterInt)_emaDown.Parameters[0]).ValueInt = _periodEmaChannel.ValueInt;
            ((IndicatorParameterString)_emaDown.Parameters[1]).ValueString = "Low";
            _emaDown.ParametersDigit[0].Value = _periodEmaChannel.ValueInt;
            _emaDown.DataSeries[0].Color = Color.Bisque;
            _emaDown.Save();

            // Subscribe to the indicator update event           
            ParametrsChangeByUser += IntersectionOfFourEma_ParametrsChangeByUser;
           
            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
        }
        // Indicator Update event
        private void IntersectionOfFourEma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEma1.ValueInt;
            _ema1.Save();
            _ema1.Reload();

            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodEma2.ValueInt;
            _ema2.Save();
            _ema2.Reload();

            ((IndicatorParameterInt)_ema3.Parameters[0]).ValueInt = _periodEma3.ValueInt;
            _ema3.Save();
            _ema3.Reload();

            ((IndicatorParameterInt)_ema4.Parameters[0]).ValueInt = _periodEma4.ValueInt;
            _ema4.Save();
            _ema4.Reload();

            ((IndicatorParameterInt)_emaUp.Parameters[0]).ValueInt = _periodEmaChannel.ValueInt;
            _emaUp.Save();
            _emaUp.Reload();

            ((IndicatorParameterInt)_emaDown.Parameters[0]).ValueInt = _periodEmaChannel.ValueInt;
            _emaDown.Save();
            _emaDown.Reload();

        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _periodEma1.ValueInt || candles.Count < _periodEma2.ValueInt ||
                candles.Count < _periodEma3.ValueInt || candles.Count < _periodEma4.ValueInt ||
                candles.Count < _periodEmaChannel.ValueInt)
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
          
            _lastEma2 = _ema2.DataSeries[0].Last;
            _lastEma3 = _ema3.DataSeries[0].Last;
            _lastEma4 = _ema4.DataSeries[0].Last;
            _lastEmaUp = _emaUp.DataSeries[0].Last;
            _lastEmaDown = _emaDown.DataSeries[0].Last;
            var smaValues = _ema1.DataSeries[0].Values;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
               
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    bool lastEma1Up = smaValues.Last() > smaValues[smaValues.Count - 2];

                    if (lastEma1Up && _lastEma2 > _lastEma3 && lastPrice > _lastEma4 && lastPrice > _lastEmaUp)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    bool lastEma1Down = smaValues.Last() < smaValues[smaValues.Count - 2];
                    if (lastEma1Down && _lastEma2 < _lastEma3 && lastPrice < _lastEma4 && lastPrice < _lastEmaDown)
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
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
            decimal lastPrice = candles[candles.Count - 1].Close;
            _lastEma4 = _ema4.DataSeries[0].Last;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }
                if (openPositions[i].Direction == Side.Buy)  // We put a stop on the buy
                {
                    if (lastPrice < _lastEma4)
                    {

                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice > _lastEma4)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice + _slippage, openPositions[0].OpenVolume);
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

        // The name of the robot in OsEngin
        public override string GetNameStrategyType()
        {
            return "StrategyOfFourEma";
        }

        public override void ShowIndividualSettingsDialog()
        {
            throw new NotImplementedException();
        }
    }
}
