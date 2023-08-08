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
using System.Windows.Media.Animation;

/* Description
trading robot for osengine

The Countertrend robot on Bollinger

Buy:
1. The price was below the lower Bollinger line, and then returned and became above the lower line.
2. Rsi below 30.

Sale:
1. The price was above the upper Bollinger line, and then returned and became below the upper line.
2. Rsi above 70.

Buy exit: trailing stop as a % of the Low of the candle where you entered.
Sell ​​exit: trailing stop as a % of the High of the candle where you entered.

 */


namespace OsEngine.Robots.AO
{
    [Bot("CountertrendBollingerRSI")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendBollingerRSI : BotPanel
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
        private StrategyParameterInt BollingerLength;
        private StrategyParameterDecimal BollingerDeviation;
        private StrategyParameterInt LengthRSI;

        // Indicator
        Aindicator _Bollinger;
        Aindicator _RSI;

        // The last value of the indicator
        private decimal _lastUpLine;
        private decimal _lastDownLine;
        private decimal _lastRSI;

        // The prev value of the indicator
        private decimal _prevUpLine;
        private decimal _prevDownLine;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        public CountertrendBollingerRSI(string name, StartProgram startProgram) : base(name, startProgram)
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
            BollingerLength = CreateParameter("Bollinger Length", 21, 7, 48, 7, "Indicator");
            BollingerDeviation = CreateParameter("Bollinger Deviation", 1.0m, 1, 5, 0.1m, "Indicator");
            LengthRSI = CreateParameter("RSI Length", 14, 7, 48, 7, "Indicator");

            // Create indicator Bollinger
            _Bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _Bollinger = (Aindicator)_tab.CreateCandleIndicator(_Bollinger, "Prime");
            ((IndicatorParameterInt)_Bollinger.Parameters[0]).ValueInt = BollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_Bollinger.Parameters[1]).ValueDecimal = BollingerDeviation.ValueDecimal;
            _Bollinger.Save();

            // Create indicator RSI
            _RSI = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _RSI = (Aindicator)_tab.CreateCandleIndicator(_RSI, "NewArea");
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = LengthRSI.ValueInt;
            _RSI.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendBollingerRSI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The Countertrend robot on Bollinger " +
                "Buy: " +
                "1. The price was below the lower Bollinger line, and then returned and became above the lower line. " +
                "2. Rsi below 30. " +
                "Sale: " +
                "1. The price was above the upper Bollinger line, and then returned and became below the upper line. " +
                "2. Rsi above 70. " +
                "Buy exit: trailing stop as a % of the Low of the candle where you entered. " +
                "Sell ​​exit: trailing stop as a % of the High of the candle where you entered.";
        }

        private void CountertrendBollingerRSI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Bollinger.Parameters[0]).ValueInt = BollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_Bollinger.Parameters[1]).ValueDecimal = BollingerDeviation.ValueDecimal;
            _Bollinger.Save();
            _Bollinger.Reload();
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = LengthRSI.ValueInt;
            _RSI.Save();
            _RSI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendBollingerRSI";
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
            if (candles.Count < BollingerDeviation.ValueDecimal ||
                candles.Count < BollingerLength.ValueInt)
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
            _lastUpLine = _Bollinger.DataSeries[0].Last;
            _lastDownLine = _Bollinger.DataSeries[1].Last;
            _lastRSI = _RSI.DataSeries[0].Last;

            // The prev value of the indicator
            _prevUpLine = _Bollinger.DataSeries[0].Values[_Bollinger.DataSeries[0].Values.Count - 2];
            _prevDownLine = _Bollinger.DataSeries[1].Values[_Bollinger.DataSeries[1].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal prevPriceLow = candles[candles.Count - 2].Low;
                decimal prevPriceHigh = candles[candles.Count - 2].High;

                decimal lastPriceLow = candles[candles.Count - 1].Low;
                decimal lastPriceHigh = candles[candles.Count - 1].High;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (prevPriceLow < _prevDownLine && lastPriceLow > _lastDownLine && _lastRSI < 30)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (prevPriceHigh > _prevUpLine && lastPriceHigh < _lastUpLine && _lastRSI > 70)
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

            decimal stopPrice;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

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
