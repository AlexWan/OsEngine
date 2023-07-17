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
using System.Windows.Media.Animation;

/* Description
trading robot for osengine

The trend robot on strategy Ema with ADX.

Buy:
The previous candle was above Ema, the last candle was lower or equal to Ema, 
and Adx must be above 25. We set a litka for purchase at the price of the high of this candle.

Sale:
The previous candle was below Ema, the last high candle is higher than or equal to Ema,
and Adx must be above 25. We set a litka for purchase at the price of this candle's loy.

Buy exit: trailing stop in % of the line of the candle on which you entered.

Sell ​​exit: trailing stop in % of the high of the candle where you entered.

 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyEmaADX")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyEmaADX : BotPanel
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
        private StrategyParameterInt PeriodEma;
        private StrategyParameterInt PeriodADX;

        // Indicator
        Aindicator _Ema;
        Aindicator _ADX;

        // The last value of the indicator
        private decimal _lastEma;
        private decimal _lastADX;

        // Exit
        private StrategyParameterInt TrailingValue;

        public StrategyEmaADX(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodEma = CreateParameter("Period Ema", 10, 10, 300, 10, "Indicator");
            PeriodADX = CreateParameter("Period ADX", 10, 10, 300, 10, "Indicator");

            // Create indicator Ema
            _Ema = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema", false);
            _Ema = (Aindicator)_tab.CreateCandleIndicator(_Ema, "Prime");
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = PeriodEma.ValueInt;
            _Ema.DataSeries[0].Color = Color.GreenYellow;
            _Ema.Save();

            // Create indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyEmaADX_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "trading robot for osengine " +
                "The trend robot on strategy Ema with ADX. " +
                "Buy: " +
                "The previous candle was above Ema, the last candle was lower or equal to Ema,  " +
                "and Adx must be above 25. We set a litka for purchase at the price of the high of this candle. " +
                "Sale: " +
                "The previous candle was below Ema, the last high candle is higher than or equal to Ema, " +
                "and Adx must be above 25. We set a litka for purchase at the price of this candle's loy. " +
                "Buy exit: trailing stop in % of the line of the candle on which you entered. " +
                "Sell ​​exit: trailing stop in % of the high of the candle where you entered.";
        }

        private void StrategyEmaADX_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = PeriodEma.ValueInt;
            _Ema.Save();
            _Ema.Reload();    
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyEmaADX";
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
            if (candles.Count < PeriodADX.ValueInt || candles.Count < PeriodEma.ValueInt)
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
            _lastEma = _Ema.DataSeries[0].Last;
            _lastADX = _ADX.DataSeries[0].Last;

            decimal prevCandel = candles[candles.Count - 2].Low;
            decimal lastCandel = candles[candles.Count - 1].Low;
            decimal lastHigh = candles[candles.Count - 1].High;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> values = _ADX.DataSeries[0].Values;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEma < prevCandel && _lastEma >= lastCandel && _lastADX > 25)
                    {
                        _tab.BuyAtLimit(GetVolume(), lastHigh + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastEma > prevCandel && lastHigh >= _lastEma && _lastADX > 25)
                    {
                        _tab.SellAtLimit(GetVolume(), lastCandel - _slippage);
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

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

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
                    stopPrice = lov - lov * TrailingValue.ValueInt / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueInt / 100;
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