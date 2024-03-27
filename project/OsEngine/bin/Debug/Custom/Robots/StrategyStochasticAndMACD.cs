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
using System.Security.Cryptography;

/* Description
trading robot for osengine

The trend robot on Strategy Stochastic And MACD.

Buy:
1. The stochastic line (blue) is above the signal line (red) and above 20.
2. The MACD histogram crosses the signal line from bottom to top.
Sell:
1. The stochastic line (blue) is below the signal line (red) and below 80.
2. The MACD histogram crosses the signal line from top to bottom.

Exit from buy: The trailing stop is placed at the minimum for the period specified for the trailing stop and transferred (slides) to new price lows, also for the specified period.
Exit from sell: The trailing stop is placed at the maximum for the period specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.
 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyStochasticAndMACD")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyStochasticAndMACD : BotPanel
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
        private StrategyParameterInt FastLineLengthMACD;
        private StrategyParameterInt SlowLineLengthMACD;
        private StrategyParameterInt SignalLineLengthMACD;
        private StrategyParameterInt StochasticPeriod1;
        private StrategyParameterInt StochasticPeriod2;
        private StrategyParameterInt StochasticPeriod3;

        // Indicator
        Aindicator _MACD;
        Aindicator _Stochastic;

        // The last value of the indicator
        private decimal _lastMACD;
        private decimal _lastBlueStoh;
        private decimal _lastRedStoh;
        private decimal _lastSignalMACD;

        // The prev value of the indicator
        private decimal _prevMACD;
        private decimal _prevSignalMACD;

        // Exit 
        private StrategyParameterInt TrailBars;

        public StrategyStochasticAndMACD(string name, StartProgram startProgram) : base(name, startProgram)
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
            FastLineLengthMACD = CreateParameter("MACD Fast Length", 16, 10, 300, 7, "Indicator");
            SlowLineLengthMACD = CreateParameter("MACD Slow Length", 32, 10, 300, 10, "Indicator");
            SignalLineLengthMACD = CreateParameter("MACD Signal Length", 8, 10, 300, 10, "Indicator");
            StochasticPeriod1 = CreateParameter("Stochastic Period One", 10, 10, 300, 10, "Indicator");
            StochasticPeriod2 = CreateParameter("Stochastic Period Two", 20, 10, 300, 10, "Indicator");
            StochasticPeriod3 = CreateParameter("Stochastic Period Three", 30, 10, 300, 10, "Indicator");

            // Create indicator MACD
            _MACD = IndicatorsFactory.CreateIndicatorByName("MACD", name + "MACD", false);
            _MACD = (Aindicator)_tab.CreateCandleIndicator(_MACD, "NewArea");
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = FastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = SlowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = SignalLineLengthMACD.ValueInt;
            _MACD.Save();

            // Create indicator Stochastic
            _Stochastic = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stochastic", false);
            _Stochastic = (Aindicator)_tab.CreateCandleIndicator(_Stochastic, "NewArea0");
            ((IndicatorParameterInt)_Stochastic.Parameters[0]).ValueInt = StochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stochastic.Parameters[1]).ValueInt = StochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_Stochastic.Parameters[2]).ValueInt = StochasticPeriod3.ValueInt;
            _Stochastic.Save();

            // Exit
            TrailBars = CreateParameter("Trail Bars", 5, 1, 50, 1, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyStochasticAndMACD_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Stochastic And MACD. " +
                "Buy: " +
                "1. The stochastic line (blue) is above the signal line (red) and above 20. " +
                "2. The MACD histogram crosses the signal line from bottom to top. " +
                "Sell: " +
                "1. The stochastic line (blue) is below the signal line (red) and below 80. " +
                "2. The MACD histogram crosses the signal line from top to bottom. " +
                "Exit from buy: The trailing stop is placed at the minimum for the period specified for the trailing stop and transferred (slides) to new price lows, also for the specified period. " +
                "Exit from sell: The trailing stop is placed at the maximum for the period specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.";
        }

        private void StrategyStochasticAndMACD_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = FastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = SlowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = SignalLineLengthMACD.ValueInt;
            _MACD.Save();
            _MACD.Reload();
            ((IndicatorParameterInt)_Stochastic.Parameters[0]).ValueInt = StochasticPeriod1.ValueInt;
            ((IndicatorParameterInt)_Stochastic.Parameters[1]).ValueInt = StochasticPeriod2.ValueInt;
            ((IndicatorParameterInt)_Stochastic.Parameters[2]).ValueInt = StochasticPeriod3.ValueInt;
            _Stochastic.Save();
            _Stochastic.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyStochasticAndMACD";
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
            if (candles.Count < FastLineLengthMACD.ValueInt ||
                candles.Count < SlowLineLengthMACD.ValueInt + 6 ||
                candles.Count < SignalLineLengthMACD.ValueInt)
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
            _lastMACD = _MACD.DataSeries[0].Last;
            _lastBlueStoh = _Stochastic.DataSeries[0].Last;
            _lastRedStoh = _Stochastic.DataSeries[1].Last;
            _lastSignalMACD = _MACD.DataSeries[2].Last;

            // The prev value of the indicator
            _prevMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 2];
            _prevSignalMACD = _MACD.DataSeries[2].Values[_MACD.DataSeries[2].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastBlueStoh > _lastRedStoh && _lastBlueStoh > 20 &&
                        _prevMACD < _prevSignalMACD && _lastMACD > _lastSignalMACD)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastBlueStoh < _lastRedStoh && _lastBlueStoh < 80 &&
                        _prevMACD > _prevSignalMACD && _lastMACD < _lastSignalMACD)
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


                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(pos, price, price - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal price = GetPriceStop(Side.Sell, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(pos, price, price + _slippage);
                }
            }
        }

        private decimal GetPriceStop(Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < TrailBars.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                decimal price = decimal.MaxValue;

                for (int i = index; i > index - TrailBars.ValueInt; i--)
                {
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }
                return price;
            }

            if (side == Side.Sell)
            {
                decimal price = 0;

                for (int i = index; i > index - TrailBars.ValueInt; i--)
                {
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }
            return 0;
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
