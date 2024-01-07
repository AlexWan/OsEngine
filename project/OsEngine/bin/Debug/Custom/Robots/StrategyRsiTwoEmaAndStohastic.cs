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
using System.Diagnostics;
using System.Security.Cryptography;

/* Description
trading robot for osengine

The trend robot on Strategy Rsi,Two Ema And Stohastic.

Buy:
1. Fast Ema is higher than slow Ema.
2. The Rsi is above 50 and below 70, rising.
3. Stochastic is growing and is above 20 and below 80.
Sell:
1. Fast Ema is lower than slow Ema.
2. The Rsi is below 50 and above 20, falling.
3. Stochastic is falling and is above 20 and below 80.

Exit: the opposite signal of the Ema.
 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyRsiTwoEmaAndStohastic")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyRsiTwoEmaAndStohastic : BotPanel
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
        private StrategyParameterInt _periodEmaFast;
        private StrategyParameterInt _periodEmaSlow;
        private StrategyParameterInt PeriodRSI;
        private StrategyParameterInt StochPeriod1;
        private StrategyParameterInt StochPeriod2;
        private StrategyParameterInt StochPeriod3;

        // Indicator
        Aindicator _RSI;
        Aindicator _ema1;
        Aindicator _ema2;
        Aindicator _Stoh;

        // The last value of the indicator
        private decimal _lastRSI;
        private decimal _lastEmaFast;
        private decimal _lastEmaSlow;
        private decimal _lastStoh;

        // The prev value of the indicator
        private decimal _prevRSI;
        private decimal _prevStoh;

        public StrategyRsiTwoEmaAndStohastic(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodEmaFast = CreateParameter("fast EMA1 period", 250, 50, 500, 50, "Indicator");
            _periodEmaSlow = CreateParameter("slow EMA2 period", 1000, 500, 1500, 100, "Indicator");
            PeriodRSI = CreateParameter("Period RSI", 14, 10, 300, 1, "Indicator");
            StochPeriod1 = CreateParameter("Stoch Period 1", 5, 3, 40, 1, "Indicator");
            StochPeriod2 = CreateParameter("Stoch Period 2", 3, 2, 40, 1, "Indicator");
            StochPeriod3 = CreateParameter("Stoch Period 3", 3, 2, 40, 1, "Indicator");

            // Create indicator RSI
            _RSI = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _RSI = (Aindicator)_tab.CreateCandleIndicator(_RSI, "NewArea");
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = PeriodRSI.ValueInt;
            _RSI.Save();

            // Creating indicator EmaFast
            _ema1 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "EMA1", canDelete: false);
            _ema1 = (Aindicator)_tab.CreateCandleIndicator(_ema1, nameArea: "Prime");
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.ParametersDigit[0].Value = _periodEmaFast.ValueInt;
            _ema1.DataSeries[0].Color = Color.Red;
            _ema1.Save();

            // Creating indicator EmaSlow
            _ema2 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "Ema2", canDelete: false);
            _ema2 = (Aindicator)_tab.CreateCandleIndicator(_ema2, nameArea: "Prime");
            _ema2.ParametersDigit[0].Value = _periodEmaSlow.ValueInt;
            _ema2.DataSeries[0].Color = Color.Green;
            _ema2.Save();

            // Create indicator Stoh
            _Stoh = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stoh", false);
            _Stoh = (Aindicator)_tab.CreateCandleIndicator(_Stoh, "NewArea0");
            ((IndicatorParameterInt)_Stoh.Parameters[0]).ValueInt = StochPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[1]).ValueInt = StochPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[2]).ValueInt = StochPeriod1.ValueInt;
            _Stoh.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionKalmanAndVwma_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Rsi,Two Ema And Stohastic. " +
                "Buy: " +
                "1. Fast Ema is higher than slow Ema. " +
                "2. The Rsi is above 50 and below 70, rising. " +
                "3. Stochastic is growing and is above 20 and below 80. " +
                "Sell: " +
                "1. Fast Ema is lower than slow Ema. " +
                "2. The Rsi is below 50 and above 20, falling. " +
                "3. Stochastic is falling and is above 20 and below 80. " +
                "Exit: the opposite signal of the Ema.";
        }

        private void IntersectionKalmanAndVwma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = PeriodRSI.ValueInt;
            _RSI.Save();
            _RSI.Reload();
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.Save();
            _ema1.Reload();
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _ema2.Save();
            _ema2.Reload();
            ((IndicatorParameterInt)_Stoh.Parameters[0]).ValueInt = StochPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[1]).ValueInt = StochPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stoh.Parameters[2]).ValueInt = StochPeriod1.ValueInt;
            _Stoh.Save();
            _Stoh.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyRsiTwoEmaAndStohastic";
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
            if (candles.Count < PeriodRSI.ValueInt ||
                candles.Count < _periodEmaFast.ValueInt ||
                candles.Count < _periodEmaSlow.ValueInt ||
                candles.Count < StochPeriod1.ValueInt ||
                candles.Count < StochPeriod2.ValueInt ||
                candles.Count < StochPeriod3.ValueInt)
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
            _lastRSI = _RSI.DataSeries[0].Last;
            _lastEmaFast = _ema1.DataSeries[0].Last;
            _lastEmaSlow = _ema2.DataSeries[0].Last;
            _lastStoh = _Stoh.DataSeries[0].Last;

            // The prev value of the indicator
            _prevRSI = _RSI.DataSeries[0].Values[_RSI.DataSeries[0].Values.Count - 2];
            _prevStoh = _Stoh.DataSeries[0].Values[_Stoh.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {


                    if (_lastEmaFast > _lastEmaSlow &&
                        _lastRSI > 50 && _lastRSI < 70 && _prevRSI < _lastRSI &&
                        _lastStoh > 20 && _lastStoh < 80 && _prevStoh > _lastStoh)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastEmaFast < _lastEmaSlow &&
                        _lastRSI < 50 && _lastRSI > 20 && _prevRSI > _lastRSI &&
                        _lastStoh > 20 && _lastStoh < 80 && _prevStoh > _lastStoh)
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

            // The last value of the indicator
            _lastEmaFast = _ema1.DataSeries[0].Last;
            _lastEmaSlow = _ema2.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastEmaFast < _lastEmaSlow)
                    {

                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {

                    if (_lastEmaFast > _lastEmaSlow)
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
