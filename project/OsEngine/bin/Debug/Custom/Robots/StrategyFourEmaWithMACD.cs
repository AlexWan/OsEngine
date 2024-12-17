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

The trend robot on Strategy Four Ema With MACD.

Buy:
1. Ema1 is higher than Ema2 and the price is higher than Ema3 and Ema4;
2. MACD histogram> 0.
Sell:
1. Ema1 is lower than Ema2 and the price is lower than Ema3 and Ema4;
2. MACD histogram< 0.

Exit from a long position: The trailing stop is placed at the minimum 
for the period specified for the trailing stop and transferred (slides) to new price lows, also for the specified period.
Exit from the short position: The trailing stop is placed at the maximum 
for the period specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.
 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyFourEmaWithMACD")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyFourEmaWithMACD : BotPanel
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
        private StrategyParameterInt _EmaPeriod1;
        private StrategyParameterInt _EmaPeriod2;
        private StrategyParameterInt _EmaPeriod3;
        private StrategyParameterInt _EmaPeriod4;
        private StrategyParameterInt FastLineLengthMACD;
        private StrategyParameterInt SlowLineLengthMACD;
        private StrategyParameterInt SignalLineLengthMACD;

        // Indicator
        Aindicator _Ema1;
        Aindicator _Ema2;
        Aindicator _Ema3;
        Aindicator _Ema4;
        Aindicator _MACD;

        // The last value of the indicator
        private decimal _lastMACD;
        private decimal _OnelastEma;
        private decimal _TwolastEma;
        private decimal _ThreelastEma;
        private decimal _FourlastEma;

        // Exit 
        private StrategyParameterInt TrailBars;

        public StrategyFourEmaWithMACD(string name, StartProgram startProgram) : base(name, startProgram)
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
            _EmaPeriod1 = CreateParameter("Ema Length 1", 10, 7, 48, 7, "Indicator");
            _EmaPeriod2 = CreateParameter("Ema Length 2", 20, 7, 48, 7, "Indicator");
            _EmaPeriod3 = CreateParameter("Ema Length 3", 30, 7, 48, 7, "Indicator");
            _EmaPeriod4 = CreateParameter("Ema Length 4", 40, 7, 48, 7, "Indicator");
            FastLineLengthMACD = CreateParameter("MACD Fast Length", 16, 10, 300, 7, "Indicator");
            SlowLineLengthMACD = CreateParameter("MACD Slow Length", 32, 10, 300, 10, "Indicator");
            SignalLineLengthMACD = CreateParameter("MACD Signal Length", 8, 10, 300, 10, "Indicator");

            // Create indicator Ema1
            _Ema1 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA1", false);
            _Ema1 = (Aindicator)_tab.CreateCandleIndicator(_Ema1, "Prime");
            ((IndicatorParameterInt)_Ema1.Parameters[0]).ValueInt = _EmaPeriod1.ValueInt;
            _Ema1.Save();

            // Create indicator Ema1
            _Ema2 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA2", false);
            _Ema2 = (Aindicator)_tab.CreateCandleIndicator(_Ema2, "Prime");
            ((IndicatorParameterInt)_Ema2.Parameters[0]).ValueInt = _EmaPeriod2.ValueInt;
            _Ema2.Save();

            // Create indicator Ema1
            _Ema3 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA3", false);
            _Ema3 = (Aindicator)_tab.CreateCandleIndicator(_Ema3, "Prime");
            ((IndicatorParameterInt)_Ema3.Parameters[0]).ValueInt = _EmaPeriod3.ValueInt;
            _Ema3.Save();

            // Create indicator Ema1
            _Ema4 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA4", false);
            _Ema4 = (Aindicator)_tab.CreateCandleIndicator(_Ema4, "Prime");
            ((IndicatorParameterInt)_Ema4.Parameters[0]).ValueInt = _EmaPeriod4.ValueInt;
            _Ema4.Save();

            // Create indicator MACD
            _MACD = IndicatorsFactory.CreateIndicatorByName("MACD", name + "MACD", false);
            _MACD = (Aindicator)_tab.CreateCandleIndicator(_MACD, "NewArea");
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = FastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = SlowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = SignalLineLengthMACD.ValueInt;
            _MACD.Save();
            
            // Exit
            TrailBars = CreateParameter("Trail Bars", 5, 1, 50, 1, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyFourEmaWithMACD_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Four Ema With MACD. " +
                "Buy: " +
                "1. Ema1 is higher than Ema2 and the price is higher than Ema3 and Ema4; " +
                "2. MACD histogram> 0. " +
                "Sell: " +
                "1. Ema1 is lower than Ema2 and the price is lower than Ema3 and Ema4; " +
                "2. MACD histogram< 0. " +
                "Exit from a long position: The trailing stop is placed at the minimum  " +
                "for the period specified for the trailing stop and transferred (slides) to new price lows, also for the specified period. " +
                "Exit from the short position: The trailing stop is placed at the maximum  " +
                "for the period specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.";
        }

        private void StrategyFourEmaWithMACD_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Ema1.Parameters[0]).ValueInt = _EmaPeriod1.ValueInt;
            _Ema1.Save();
            _Ema1.Reload();
            ((IndicatorParameterInt)_Ema2.Parameters[0]).ValueInt = _EmaPeriod2.ValueInt;
            _Ema2.Save();
            _Ema2.Reload();
            ((IndicatorParameterInt)_Ema3.Parameters[0]).ValueInt = _EmaPeriod3.ValueInt;
            _Ema3.Save();
            _Ema3.Reload();
            ((IndicatorParameterInt)_Ema4.Parameters[0]).ValueInt = _EmaPeriod4.ValueInt;
            _Ema4.Save();
            _Ema4.Reload();
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = FastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = SlowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = SignalLineLengthMACD.ValueInt;
            _MACD.Save();
            _MACD.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyFourEmaWithMACD";
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
            if (candles.Count < _EmaPeriod1.ValueInt ||
                candles.Count < FastLineLengthMACD.ValueInt ||
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
            _OnelastEma = _Ema1.DataSeries[0].Last;
            _TwolastEma = _Ema2.DataSeries[0].Values[_Ema2.DataSeries[0].Values.Count - 2];
            _ThreelastEma = _Ema3.DataSeries[0].Values[_Ema3.DataSeries[0].Values.Count - 3];
            _FourlastEma = _Ema4.DataSeries[0].Values[_Ema4.DataSeries[0].Values.Count - 4];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastMACD > 0 && _OnelastEma > _TwolastEma && lastPrice > _ThreelastEma && lastPrice > _FourlastEma)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastMACD < 0 && _OnelastEma < _TwolastEma && lastPrice < _ThreelastEma && lastPrice < _FourlastEma)
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
                Position pos = openPositions[0];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
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
