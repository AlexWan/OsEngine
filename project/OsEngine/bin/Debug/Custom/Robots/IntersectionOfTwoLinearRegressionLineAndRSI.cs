using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine

Trend robot on the intersection of two Linear Regression Line and RSI.

Buy: 
1. The fast EMA crosses the slow ONE from bottom to top.
2. The RSI is above 50 and growing.

Sale:
1. The fast EMA crosses the slow ONE from top to bottom.
2. The RSI is above 50 and growing.

Exit:
Stop and profit in % of the entry price.
*/
namespace OsEngine.Robots.My_bots
{
    [Bot("IntersectionOfTwoLinearRegressionLineAndRSI")] //We create an attribute so that we don't write anything in the Boot factory
    public class IntersectionOfTwoLinearRegressionLineAndRSI : BotPanel
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
        private StrategyParameterInt _periodRsi;
        private StrategyParameterInt _periodLRMAFast;
        private StrategyParameterInt _periodLRMASlow;
        
        // Indicator
        private Aindicator _Rsi;
        private Aindicator _LRMA1;
        private Aindicator _LRMA2;

        //The last value of the indicators
        private decimal _lastRsi;
        private decimal _prevRsi;
        private decimal _lastLRMAFast;
        private decimal _lastLRMASlow;
        private decimal _prevLRMAFast;
        private decimal _prevLRMASlow;

        // Exit
        private StrategyParameterDecimal StopValue;
        private StrategyParameterDecimal ProfitValue;

        public IntersectionOfTwoLinearRegressionLineAndRSI(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodRsi = CreateParameter("Period RSI", 15, 50, 300, 10, "Indicator");
            _periodLRMAFast = CreateParameter("Period LRMA Fast", 250, 50, 500, 20, "Indicator");
            _periodLRMASlow = CreateParameter("Period LRMA Slow", 500, 100, 1500, 100, "Indicator");
           
            // Creating an indicator RSI
            _Rsi = IndicatorsFactory.CreateIndicatorByName("RSI", name + "Rsi", false);
            _Rsi = (Aindicator)_tab.CreateCandleIndicator(_Rsi, "NewArea");
            ((IndicatorParameterInt)_Rsi.Parameters[0]).ValueInt = _periodRsi.ValueInt;
            _Rsi.Save();

            // Creating an indicator LRMA1
            _LRMA1 = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LRMA1", false);
            _LRMA1 = (Aindicator)_tab.CreateCandleIndicator(_LRMA1, "Prime");
            ((IndicatorParameterInt)_LRMA1.Parameters[0]).ValueInt = _periodLRMAFast.ValueInt;
            _LRMA1.Save();

            // Creating an indicator LRMA2
            _LRMA2 = IndicatorsFactory.CreateIndicatorByName("LinearRegressionLine", name + "LRMA2", false);
            _LRMA2 = (Aindicator)_tab.CreateCandleIndicator(_LRMA2, "Prime");
            ((IndicatorParameterInt)_LRMA2.Parameters[0]).ValueInt = _periodLRMASlow.ValueInt;
            _LRMA2.DataSeries[0].Color = Color.Aquamarine;
            _LRMA2.Save();

            // Exit
            StopValue = CreateParameter("Stop percent", 0.5m, 1, 10, 1, "Exit settings");
            ProfitValue = CreateParameter("Profit percent", 0.5m, 1, 10, 1, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfTwoLinearRegressionLineAndRSI_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on the intersection of two Linear Regression Line and RSI." +
                "Buy:" +
                "1.The fast EMA crosses the slow ONE from bottom to top." +
                "2.The RSI is above 50 and growing." +
                "Sale:" +
                "1.The fast EMA crosses the slow ONE from top to bottom." +
                "2.The RSI is above 50 and growing." +
                "Exit:" +
                "Stop and profit in % of the entry price.";
        }

        // Indicator Update event
        private void IntersectionOfTwoLinearRegressionLineAndRSI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Rsi.Parameters[0]).ValueInt = _periodRsi.ValueInt;
            _Rsi.Save();
            _Rsi.Reload();

            ((IndicatorParameterInt)_LRMA1.Parameters[0]).ValueInt = _periodLRMAFast.ValueInt;
            _LRMA1.Save();
            _LRMA1.Reload();

            ((IndicatorParameterInt)_LRMA2.Parameters[0]).ValueInt = _periodLRMASlow.ValueInt;
            _LRMA2.Save();
            _LRMA2.Reload();

        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionOfTwoLinearRegressionLineAndRSI";
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
            if (candles.Count < _periodRsi.ValueInt || candles.Count < _periodLRMAFast.ValueInt
               || candles.Count < _periodLRMASlow.ValueInt)
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
                _lastRsi = _Rsi.DataSeries[0].Last;
                _prevRsi = _Rsi.DataSeries[0].Values[_Rsi.DataSeries[0].Values.Count - 2];
                _lastLRMAFast = _LRMA1.DataSeries[0].Last;
                _prevLRMAFast = _LRMA1.DataSeries[0].Values[_LRMA1.DataSeries[0].Values.Count - 2];
                _lastLRMASlow = _LRMA2.DataSeries[0].Last;
                _prevLRMASlow = _LRMA2.DataSeries[0].Values[_LRMA2.DataSeries[0].Values.Count - 2];


                // Long
                if (Regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {
                    if (_lastLRMAFast > _prevLRMAFast && _prevLRMAFast > _prevLRMASlow
                        && _lastLRMAFast > _lastLRMASlow && _lastRsi > 50 && _prevRsi < _lastRsi)
                    {

                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // if the mode is not only long, we enter the short
                {
                    if( _lastLRMAFast < _prevLRMAFast && _prevLRMAFast < _prevLRMASlow
                        && _lastLRMAFast < _lastLRMASlow && _lastRsi > 50 && _prevRsi < _lastRsi)
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

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                decimal lastPrice = candles[candles.Count - 1].Close;

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
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

