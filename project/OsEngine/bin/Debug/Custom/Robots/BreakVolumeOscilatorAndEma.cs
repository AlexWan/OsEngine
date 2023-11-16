using System;
using System.Collections.Generic;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine

Trend robot on the Break VolumeOscilator And Ema.

Buy: the Volume Oscillator indicator line is above 0 and the price is your Ema.

Sell: the Volume Oscillator indicator line is below 0 and the price is below Ema.

Exit: on the opposite signal.
*/

namespace OsEngine.Robots.MyRobots

{
    [Bot("BreakVolumeOscilatorAndEma")] //We create an attribute so that we don't write anything in the Bot factory

    public class BreakVolumeOscilatorAndEma : BotPanel
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
        private StrategyParameterInt _EmaPeriod;
        private StrategyParameterInt _LenghtVolOsSlow;
        private StrategyParameterInt _LenghtVolOsFast;

        // Indicator
        Aindicator _Ema;
        Aindicator _VolumeOsc;

        //The last value of the indicators
        private decimal _lastEma;
        private decimal _lastVolume;

        public BreakVolumeOscilatorAndEma(string name, StartProgram startProgram) : base(name, startProgram)
        {

            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency" }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 1, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator Settings
            _EmaPeriod = CreateParameter("Ema period", 15, 50, 300, 1, "Indicator");
            _LenghtVolOsSlow = CreateParameter("LenghtVolOsSlow", 10, 20, 300, 1, "Indicator");
            _LenghtVolOsFast = CreateParameter("LenghtVolOsFast", 10, 10, 300, 1, "Indicator");

            // Create indicator Ema
            _Ema = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA", false);
            _Ema = (Aindicator)_tab.CreateCandleIndicator(_Ema, "Prime");
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = _EmaPeriod.ValueInt;
            _Ema.Save();

            // Create indicator VoluneOscilator
            _VolumeOsc = IndicatorsFactory.CreateIndicatorByName("VolumeOscilator", name + "VolumeOscilator", false);
            _VolumeOsc = (Aindicator)_tab.CreateCandleIndicator(_VolumeOsc, "NewArea1");
            ((IndicatorParameterInt)_VolumeOsc.Parameters[0]).ValueInt = _LenghtVolOsSlow.ValueInt;
            ((IndicatorParameterInt)_VolumeOsc.Parameters[1]).ValueInt = _LenghtVolOsFast.ValueInt;
            _VolumeOsc.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakVolumeOscilatorAndEma_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on the Break VolumeOscilator And Ema. " +
                "Buy: the Volume Oscillator indicator line is above 0 and the price is your Ema. " +
                "Sell: the Volume Oscillator indicator line is below 0 and the price is below Ema. " +
                "Exit: on the opposite signal.";
        }

        // Indicator Update event
        private void BreakVolumeOscilatorAndEma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = _EmaPeriod.ValueInt;
            _Ema.Save();
            _Ema.Reload();
            ((IndicatorParameterInt)_VolumeOsc.Parameters[0]).ValueInt = _LenghtVolOsSlow.ValueInt;
            ((IndicatorParameterInt)_VolumeOsc.Parameters[1]).ValueInt = _LenghtVolOsFast.ValueInt;
            _VolumeOsc.Save();
            _VolumeOsc.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakVolumeOscilatorAndEma";
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
            if (candles.Count < _EmaPeriod.ValueInt)
            {
                return;
            }

            // If the time does not match, we exit
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

                // The last value of the indicators               
                _lastEma = _Ema.DataSeries[0].Last;
                _lastVolume = _VolumeOsc.DataSeries[0].Last;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Long
                if (Regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastEma && _lastVolume > 0)
                    {

                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if (lastPrice < _lastEma && _lastVolume < 0)
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
            
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
            
            decimal lastPrice = candles[candles.Count - 1].Close;
            
            // The last value of the indicators               
            _lastEma = _Ema.DataSeries[0].Last;
            _lastVolume = _VolumeOsc.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (lastPrice < _lastEma && _lastVolume < 0)
                    {

                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {

                    if (lastPrice > _lastEma && _lastVolume > 0)
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


    }
}
