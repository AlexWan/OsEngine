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

The trend robot on of five Sma

Buy:

All Smas are rising (when all five moving averages are larger than they were one bar ago) + 
half of the difference between the high and low of the previous bar.

Sell:

All Smas fall (when all five moving averages are less than they were one bar ago) - 
half the difference between the high and low of the previous bar.

Exit from buy: Sma1, Sma2 and Sma3 are falling.

Exit from sell: Sma1, Sma2 and Sma3 are growing.

*/

namespace OsEngine.Robots.SMA
{
    [Bot("StrategyFiveSma")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyFiveSma : BotPanel
    {
        public BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // Setting indicator
        private StrategyParameterInt PeriodSma1;
        private StrategyParameterInt PeriodSma2;
        private StrategyParameterInt PeriodSma3;
        private StrategyParameterInt PeriodSma4;
        private StrategyParameterInt PeriodSma5;

        // Indicator
        private Aindicator _Sma1;
        private Aindicator _Sma2;
        private Aindicator _Sma3;
        private Aindicator _Sma4;
        private Aindicator _Sma5;

        // The last value of the indicators
        private decimal _lastSma1;
        private decimal _lastSma2;
        private decimal _lastSma3;
        private decimal _lastSma4;
        private decimal _lastSma5;

        // The penultimate value of the indicators
        private decimal _prevSma1;
        private decimal _prevSma2;
        private decimal _prevSma3;
        private decimal _prevSma4;
        private decimal _prevSma5;

        public StrategyFiveSma(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodSma1 = CreateParameter("Period SMA1", 50, 10, 300, 1, "Indicator");
            PeriodSma2 = CreateParameter("Period SMA2", 100, 10, 300, 1, "Indicator");
            PeriodSma3 = CreateParameter("Period SMA3", 150, 10, 300, 1, "Indicator");
            PeriodSma4 = CreateParameter("Period SMA4", 200, 10, 300, 1, "Indicator");
            PeriodSma5 = CreateParameter("Period SMA5", 250, 10, 300, 1, "Indicator");

            // Create indicator Sma1
            _Sma1 = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma1", false);
            _Sma1 = (Aindicator)_tab.CreateCandleIndicator(_Sma1, "Prime");
            _Sma1.DataSeries[0].Color = System.Drawing.Color.Blue;
            ((IndicatorParameterInt)_Sma1.Parameters[0]).ValueInt = PeriodSma1.ValueInt;
            _Sma1.Save();

            // Create indicator Sma2
            _Sma2 = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma2", false);
            _Sma2 = (Aindicator)_tab.CreateCandleIndicator(_Sma2, "Prime");
            _Sma2.DataSeries[0].Color = System.Drawing.Color.Pink;
            ((IndicatorParameterInt)_Sma2.Parameters[0]).ValueInt = PeriodSma2.ValueInt;
            _Sma2.Save();

            // Create indicator Sma3
            _Sma3 = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma3", false);
            _Sma3 = (Aindicator)_tab.CreateCandleIndicator(_Sma3, "Prime");
            _Sma3.DataSeries[0].Color = System.Drawing.Color.Yellow;
            ((IndicatorParameterInt)_Sma3.Parameters[0]).ValueInt = PeriodSma3.ValueInt;
            _Sma3.Save();

            // Create indicator Sma4
            _Sma4 = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma4", false);
            _Sma4 = (Aindicator)_tab.CreateCandleIndicator(_Sma4, "Prime");
            _Sma4.DataSeries[0].Color = System.Drawing.Color.Gray;
            ((IndicatorParameterInt)_Sma4.Parameters[0]).ValueInt = PeriodSma4.ValueInt;
            _Sma4.Save();

            // Create indicator Sma5
            _Sma5 = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma5", false);
            _Sma5 = (Aindicator)_tab.CreateCandleIndicator(_Sma5, "Prime");
            _Sma5.DataSeries[0].Color = System.Drawing.Color.Green;
            ((IndicatorParameterInt)_Sma5.Parameters[0]).ValueInt = PeriodSma5.ValueInt;
            _Sma5.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyFiveSma_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on of five Sma " +
                "Buy: " +
                "All Smas are rising (when all five moving averages are larger than they were one bar ago) +  " +
                "half of the difference between the high and low of the previous bar. " +
                "Sell:" +
                "All Smas fall (when all five moving averages are less than they were one bar ago) - " +
                "half the difference between the high and low of the previous bar. " +
                "Exit from buy: Sma1, Sma2 and Sma3 are falling. " +
                "Exit from sell: Sma1, Sma2 and Sma3 are growing.";
        }

        // Indicator Update event
        private void StrategyFiveSma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Sma1.Parameters[0]).ValueInt = PeriodSma1.ValueInt;
            _Sma1.Save();
            _Sma1.Reload();
            ((IndicatorParameterInt)_Sma2.Parameters[0]).ValueInt = PeriodSma2.ValueInt;
            _Sma2.Save();
            _Sma2.Reload();
            ((IndicatorParameterInt)_Sma3.Parameters[0]).ValueInt = PeriodSma3.ValueInt;
            _Sma3.Save();
            _Sma3.Reload();
            ((IndicatorParameterInt)_Sma4.Parameters[0]).ValueInt = PeriodSma4.ValueInt;
            _Sma4.Save();
            _Sma4.Reload();
            ((IndicatorParameterInt)_Sma5.Parameters[0]).ValueInt = PeriodSma5.ValueInt;
            _Sma5.Save();
            _Sma5.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyFiveSma";
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
            if (candles.Count < PeriodSma1.ValueInt
                || candles.Count < PeriodSma2.ValueInt
                || candles.Count < PeriodSma3.ValueInt
                || candles.Count < PeriodSma4.ValueInt
                || candles.Count < PeriodSma5.ValueInt)
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
                // We find the last value and the penultimate value of the indicator
                _prevSma1 = _Sma1.DataSeries[0].Values[_Sma1.DataSeries[0].Values.Count - 2];
                _prevSma2 = _Sma2.DataSeries[0].Values[_Sma2.DataSeries[0].Values.Count - 2];
                _prevSma3 = _Sma3.DataSeries[0].Values[_Sma3.DataSeries[0].Values.Count - 2];
                _prevSma4 = _Sma4.DataSeries[0].Values[_Sma4.DataSeries[0].Values.Count - 2];
                _prevSma5 = _Sma5.DataSeries[0].Values[_Sma5.DataSeries[0].Values.Count - 2];
                _lastSma1 = _Sma1.DataSeries[0].Last;
                _lastSma2 = _Sma2.DataSeries[0].Last;
                _lastSma3 = _Sma3.DataSeries[0].Last;
                _lastSma4 = _Sma4.DataSeries[0].Last;
                _lastSma5 = _Sma5.DataSeries[0].Last;

                    decimal high = candles[candles.Count - 1].High;
                    decimal low = candles[candles.Count - 1].Low;
                    decimal highminuslow = (high - low)/2;

                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastSma1 > _prevSma1 + highminuslow 
                        && _lastSma2 > _prevSma2 + highminuslow
                        && _lastSma3 > _prevSma3 + highminuslow
                        && _lastSma4 > _prevSma4 + highminuslow
                        && _lastSma5 > _prevSma5 + highminuslow)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastSma1 < _prevSma1 - highminuslow
                        && _lastSma2 < _prevSma2 - highminuslow
                        && _lastSma3 < _prevSma3 - highminuslow
                        && _lastSma4 < _prevSma4 - highminuslow
                        && _lastSma5 < _prevSma5 - highminuslow)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestAsk - _slippage);
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
                // We find the last value and the penultimate value of the indicator
                _prevSma1 = _Sma1.DataSeries[0].Values[_Sma1.DataSeries[0].Values.Count - 2];
                _prevSma2 = _Sma2.DataSeries[0].Values[_Sma2.DataSeries[0].Values.Count - 2];
                _prevSma3 = _Sma3.DataSeries[0].Values[_Sma3.DataSeries[0].Values.Count - 2];
                _lastSma1 = _Sma1.DataSeries[0].Last;
                _lastSma2 = _Sma2.DataSeries[0].Last;
                _lastSma3 = _Sma3.DataSeries[0].Last;

                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    

                    if (_lastSma1 < _prevSma1 
                        && _lastSma2 < _prevSma2
                        && _lastSma3 < _prevSma3)
                    {
                        decimal lastPrice = candles[candles.Count - 1].Close;
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastSma1 > _prevSma1
                        && _lastSma2 > _prevSma2
                        && _lastSma3 > _prevSma3)
                    {
                        decimal lastPrice = candles[candles.Count - 1].Close;
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
