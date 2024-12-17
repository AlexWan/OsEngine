using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Linq;
using OsEngine.Logging;

/* Description
trading robot for osengine

The trend robot on Intersection Kalman And Vwma.

Buy: Vwma above Kalman.

Sell: Vwma below Kalman.

Exit: stop and profit in % of the entry price.
 */


namespace OsEngine.Robots.AO
{
    [Bot("IntersectionKalmanAndVwma")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionKalmanAndVwma : BotPanel
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
        private StrategyParameterDecimal Sharpness;
        private StrategyParameterDecimal CoefK;
        private StrategyParameterInt PeriodVwma;

        // Indicator
        Aindicator _Kalman;
        Aindicator _VWMA;

        // Exit
        private StrategyParameterDecimal StopValue;
        private StrategyParameterDecimal ProfitValue;

        // The last value of the indicator
        private decimal _lastKalman;
        private decimal _lastVWMA;

        // The prev value of the indicator
        private decimal _prevKalman;
        private decimal _prevVWMA;

        public IntersectionKalmanAndVwma(string name, StartProgram startProgram) : base(name, startProgram)
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
            Sharpness = CreateParameter("Sharpness", 1.0m, 1, 50, 1, "Indicator");
            CoefK = CreateParameter("CoefK", 1.0m, 1, 50, 1, "Indicator");
            PeriodVwma = CreateParameter("Period VWMA", 100, 10, 300, 1, "Indicator");

            // Create indicator ChaikinOsc
            _Kalman = IndicatorsFactory.CreateIndicatorByName("KalmanFilter", name + "KalmanFilter", false);
            _Kalman = (Aindicator)_tab.CreateCandleIndicator(_Kalman, "Prime");
            ((IndicatorParameterDecimal)_Kalman.Parameters[0]).ValueDecimal = Sharpness.ValueDecimal;
            ((IndicatorParameterDecimal)_Kalman.Parameters[1]).ValueDecimal = CoefK.ValueDecimal;
            _Kalman.Save();

            // Create indicator VWMAFast
            _VWMA = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VWMA", false);
            _VWMA = (Aindicator)_tab.CreateCandleIndicator(_VWMA, "Prime");
            ((IndicatorParameterInt)_VWMA.Parameters[0]).ValueInt = PeriodVwma.ValueInt;
            _VWMA.Save();

            // Exit
            StopValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");
            ProfitValue = CreateParameter("Profit Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionKalmanAndVwma_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Intersection Kalman And Vwma. " +
                "Buy: Vwma above Kalman. " +
                "Sell: Vwma below Kalman. " +
                "Exit: stop and profit in % of the entry price.";
        }

        private void IntersectionKalmanAndVwma_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_Kalman.Parameters[0]).ValueDecimal = Sharpness.ValueDecimal;
            ((IndicatorParameterDecimal)_Kalman.Parameters[1]).ValueDecimal = CoefK.ValueDecimal;
            _Kalman.Save();
            _Kalman.Reload();
            ((IndicatorParameterInt)_VWMA.Parameters[0]).ValueInt = PeriodVwma.ValueInt;
            _VWMA.Save();
            _VWMA.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionKalmanAndVwma";
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
            if (candles.Count < CoefK.ValueDecimal ||
                candles.Count < Sharpness.ValueDecimal ||
                candles.Count < PeriodVwma.ValueInt)
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
            _lastKalman = _Kalman.DataSeries[0].Last;
            _lastVWMA = _VWMA.DataSeries[0].Last;

            // The prev value of the indicator
            _prevKalman = _Kalman.DataSeries[0].Values[_Kalman.DataSeries[0].Values.Count - 2];
            _prevVWMA = _VWMA.DataSeries[0].Values[_VWMA.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {


                    if (_prevVWMA < _prevKalman && _lastVWMA > _lastKalman)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_prevVWMA > _prevKalman && _lastVWMA < _lastKalman)
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

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];
       
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
