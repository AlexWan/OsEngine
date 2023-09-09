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

/* Description
trading robot for osengine

The trend robot on strategy for 4 Ema, Awesome Oscillator and Macd Histogram.

Buy:
 1. EmaFastLoc above EmaSlowLoc;
 2. EmaFastGlob above EmaSlowGlob;
 3. AO growing;
 4. Macd > 0.

Sell:
 1. EmaFastLoc below EmaSlowLoc;
 2. EmaFastGlob below EmaSlowGlob;
 3. AO falling;
 4. Macd < 0.

Exit from buy:trailing stop in % of the loy of the candle on which you entered.

Exit from sell: trailing stop in % of the high of the candle on which you entered.
 
 */

namespace OsEngine.Robots.AO
{
    [Bot("StrategyForFourEmaAOAndMacdHistogram")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyForFourEmaAOAndMacdHistogram : BotPanel
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
        private StrategyParameterInt PeriodEmaFastLoc;
        private StrategyParameterInt PeriodEmaSlowLoc;
        private StrategyParameterInt PeriodEmaFastGlob;
        private StrategyParameterInt PeriodEmaSlowGlob;
        private StrategyParameterInt FastLineLengthMacd;
        private StrategyParameterInt SlowLineLengthMacd;
        private StrategyParameterInt SignalLineLengthMacd;
        private StrategyParameterInt FastLineLengthAO;
        private StrategyParameterInt SlowLineLengthAO;

        // Indicator
        Aindicator _Macd;
        Aindicator _AO;
        Aindicator _EmaFastLoc;
        Aindicator _EmaSlowLoc;
        Aindicator _EmaFastGlob;
        Aindicator _EmaSlowGlob;

        // The last value of the indicators
        private decimal _lastEmaFastLoc;
        private decimal _lastEmaSlowLoc;
        private decimal _lastEmaFastGlob;
        private decimal _lastEmaSlowGlob;
        private decimal _lastAO;
        private decimal _lastMacd;

        // The prevlast value of the indicator
        private decimal _prevAO;

        // Exit 
        private StrategyParameterDecimal TrailingValue;

        public StrategyForFourEmaAOAndMacdHistogram(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodEmaFastLoc = CreateParameter("Period Ema Fast Loc", 36, 10, 300, 10, "Indicator");
            PeriodEmaSlowLoc = CreateParameter("Period Ema Slow Loc", 44, 10, 300, 10, "Indicator");
            PeriodEmaFastGlob = CreateParameter("Period Ema Fast Glob", 144, 10, 300, 10, "Indicator");
            PeriodEmaSlowGlob = CreateParameter("Period Ema Slow Glob", 176, 10, 300, 10, "Indicator");
            FastLineLengthMacd = CreateParameter("Fast Line Length Macd", 16, 10, 300, 10, "Indicator");
            SlowLineLengthMacd = CreateParameter("Slow Line Length Macd", 32, 10, 300, 10, "Indicator");
            SignalLineLengthMacd = CreateParameter("Signal Line Length Macd", 8, 10, 300, 10, "Indicator");
            FastLineLengthAO = CreateParameter("Fast Line Length AO", 13, 10, 300, 10, "Indicator");
            SlowLineLengthAO = CreateParameter("Slow Line Length AO", 26, 10, 300, 10, "Indicator");

            // Create indicator EmaFastLoc
            _EmaFastLoc = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema One Loc", false);
            _EmaFastLoc = (Aindicator)_tab.CreateCandleIndicator(_EmaFastLoc, "Prime");
            ((IndicatorParameterInt)_EmaFastLoc.Parameters[0]).ValueInt = PeriodEmaFastLoc.ValueInt;
            _EmaFastLoc.DataSeries[0].Color = Color.Blue;
            _EmaFastLoc.Save();

            // Create indicator EmaSlowLoc
            _EmaSlowLoc = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema Two Loc", false);
            _EmaSlowLoc = (Aindicator)_tab.CreateCandleIndicator(_EmaSlowLoc, "Prime");
            ((IndicatorParameterInt)_EmaSlowLoc.Parameters[0]).ValueInt = PeriodEmaSlowLoc.ValueInt;
            _EmaSlowLoc.DataSeries[0].Color = Color.Yellow;
            _EmaSlowLoc.Save();

            // Create indicator EmaFastGlob
            _EmaFastGlob = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema One Glob", false);
            _EmaFastGlob = (Aindicator)_tab.CreateCandleIndicator(_EmaFastGlob, "Prime");
            ((IndicatorParameterInt)_EmaFastGlob.Parameters[0]).ValueInt = PeriodEmaFastGlob.ValueInt;
            _EmaFastGlob.DataSeries[0].Color = Color.Green;
            _EmaFastGlob.Save();

            // Create indicator EmaSlowGlob
            _EmaSlowGlob = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema Two Glob", false);
            _EmaSlowGlob = (Aindicator)_tab.CreateCandleIndicator(_EmaSlowGlob, "Prime");
            ((IndicatorParameterInt)_EmaSlowGlob.Parameters[0]).ValueInt = PeriodEmaSlowGlob.ValueInt;
            _EmaSlowGlob.DataSeries[0].Color = Color.Red;
            _EmaSlowGlob.Save();

            // Create indicator Macd
            _Macd = IndicatorsFactory.CreateIndicatorByName("MACD", name + "Macd", false);
            _Macd = (Aindicator)_tab.CreateCandleIndicator(_Macd, "NewArea");
            ((IndicatorParameterInt)_Macd.Parameters[0]).ValueInt = FastLineLengthMacd.ValueInt;
            ((IndicatorParameterInt)_Macd.Parameters[1]).ValueInt = SlowLineLengthMacd.ValueInt;
            ((IndicatorParameterInt)_Macd.Parameters[2]).ValueInt = SignalLineLengthMacd.ValueInt;
            _Macd.Save();

            // Create indicator AO
            _AO = IndicatorsFactory.CreateIndicatorByName("AO", name + "AO", false);
            _AO = (Aindicator)_tab.CreateCandleIndicator(_AO, "NewArea1");
            ((IndicatorParameterInt)_AO.Parameters[0]).ValueInt = FastLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_AO.Parameters[1]).ValueInt = SlowLineLengthAO.ValueInt;
            _AO.Save();

            // Exit
            TrailingValue = CreateParameter("TrailingValue", 1.0m, 1, 10, 1, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyForFourEmaAOAndMacdHistogram_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on strategy for 4 Ema, Awesome Oscillator and Macd Histogram. " +
                "Buy: " +
                " 1. EmaFastLoc above EmaSlowLoc; " +
                " 2. EmaFastGlob above EmaSlowGlob; " +
                " 3. AO growing; " +
                " 4. Macd > 0. " +
                "Sell: " +
                " 1. EmaFastLoc below EmaSlowLoc; " +
                " 2. EmaFastGlob below EmaSlowGlob; " +
                " 3. AO falling; " +
                " 4. Macd < 0. " +
                "Exit from buy:trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";
        }

        // Indicator Update event
        private void StrategyForFourEmaAOAndMacdHistogram_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_EmaFastLoc.Parameters[0]).ValueInt = PeriodEmaFastLoc.ValueInt;
            _EmaFastLoc.Save();
            _EmaFastLoc.Reload();
            ((IndicatorParameterInt)_EmaSlowLoc.Parameters[0]).ValueInt = PeriodEmaSlowLoc.ValueInt;
            _EmaSlowLoc.Save();
            _EmaSlowLoc.Reload();
            ((IndicatorParameterInt)_EmaFastGlob.Parameters[0]).ValueInt = PeriodEmaFastGlob.ValueInt;
            _EmaFastGlob.Save();
            _EmaFastGlob.Reload();
            ((IndicatorParameterInt)_EmaSlowGlob.Parameters[0]).ValueInt = PeriodEmaSlowGlob.ValueInt;
            _EmaSlowGlob.Save();
            _EmaSlowGlob.Reload();
            ((IndicatorParameterInt)_Macd.Parameters[0]).ValueInt = FastLineLengthMacd.ValueInt;
            ((IndicatorParameterInt)_Macd.Parameters[1]).ValueInt = SlowLineLengthMacd.ValueInt;
            ((IndicatorParameterInt)_Macd.Parameters[2]).ValueInt = SignalLineLengthMacd.ValueInt;
            _Macd.Save();
            _Macd.Reload();
            ((IndicatorParameterInt)_AO.Parameters[0]).ValueInt = FastLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_AO.Parameters[1]).ValueInt = SlowLineLengthAO.ValueInt;
            _AO.Save();
            _AO.Reload();

        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyForFourEmaAOAndMacdHistogram";
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
            if (candles.Count < PeriodEmaSlowGlob.ValueInt)
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
                _lastEmaFastLoc = _EmaFastLoc.DataSeries[0].Last;
                _lastEmaSlowLoc = _EmaSlowLoc.DataSeries[0].Last;
                _lastEmaFastGlob = _EmaFastGlob.DataSeries[0].Last;
                _lastEmaSlowGlob = _EmaSlowGlob.DataSeries[0].Last;
                _lastAO = _AO.DataSeries[0].Last;
                _lastMacd = _Macd.DataSeries[0].Last;

                // The prevlast value of the indicator
                _prevAO = _AO.DataSeries[0].Values[_AO.DataSeries[0].Values.Count - 2];

                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEmaFastLoc > _lastEmaSlowLoc && 
                        _lastEmaFastGlob > _lastEmaSlowGlob &&
                        _lastAO > _prevAO &&
                        _lastMacd > 0)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastEmaFastLoc < _lastEmaSlowLoc &&
                    _lastEmaFastGlob < _lastEmaSlowGlob &&
                    _lastAO < _prevAO &&
                    _lastMacd < 0)
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

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                // Stop Price
                decimal stopPrice;

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * TrailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);
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