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

The trend robot on Strategy MACD and ADX.

Buy:
1. MACD histogram > 0 or rising;
2. Adx > 20 and growing.
Sell:
1. MACD histogram < 0 or falling;
2. Adx > 20 and growing.
Exit:
From buy: MACD histogram <0 or falling.
From sell: MACD histogram> 0 or rising.
 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyMACDandADX")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyMACDandADX : BotPanel
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
        private StrategyParameterInt PeriodADX;
        private StrategyParameterInt FastLineLengthMACD;
        private StrategyParameterInt SlowLineLengthMACD;
        private StrategyParameterInt SignalLineLengthMACD;

        // Indicator
        Aindicator _ADX;
        Aindicator _MACD;

        // The last value of the indicator
        private decimal _lastADX;
        private decimal _lastMACD;

        // The prev value of the indicator
        private decimal _prevADX;
        private decimal _prevMACD;

        public StrategyMACDandADX(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodADX = CreateParameter("ADX Length", 21, 7, 48, 7, "Indicator");
            FastLineLengthMACD = CreateParameter("MACD Fast Length", 16, 10, 300, 7, "Indicator");
            SlowLineLengthMACD = CreateParameter("MACD Slow Length", 32, 10, 300, 10, "Indicator");
            SignalLineLengthMACD = CreateParameter("MACD Signal Length", 8, 10, 300, 10, "Indicator");

            // Create indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();

            // Create indicator MACD
            _MACD = IndicatorsFactory.CreateIndicatorByName("MACD", name + "MACD", false);
            _MACD = (Aindicator)_tab.CreateCandleIndicator(_MACD, "NewArea0");
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = FastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = SlowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = SignalLineLengthMACD.ValueInt;
            _MACD.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyMACDandADX_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy MACD and ADX. " +
                "Buy: " +
                "1. MACD histogram > 0 or rising; " +
                "2. Adx > 20 and growing. " +
                "Sell: " +
                "1. MACD histogram < 0 or falling; " +
                "2. Adx > 20 and growing. " +
                "Exit: " +
                "From buy: MACD histogram <0 or falling. " +
                "From sell: MACD histogram> 0 or rising.";
        }

        private void StrategyMACDandADX_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();
            ((IndicatorParameterInt)_MACD.Parameters[0]).ValueInt = FastLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[1]).ValueInt = SlowLineLengthMACD.ValueInt;
            ((IndicatorParameterInt)_MACD.Parameters[2]).ValueInt = SignalLineLengthMACD.ValueInt;
            _MACD.Save();
            _MACD.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyMACDandADX";
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
            if (candles.Count < PeriodADX.ValueInt ||
                candles.Count < FastLineLengthMACD.ValueInt ||
                candles.Count < SlowLineLengthMACD.ValueInt ||
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
            _lastADX = _ADX.DataSeries[0].Last;
            _lastMACD = _MACD.DataSeries[0].Last;

            // The prev value of the indicator
            _prevADX = _ADX.DataSeries[0].Values[_ADX.DataSeries[0].Values.Count - 2];
            _lastMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastMACD > 0 && _lastADX > 20 && _prevADX < _lastADX || 
                        _lastMACD > _prevMACD && _prevADX < _lastADX && _lastADX > 20)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastMACD < 0 && _lastADX > 20 && _prevADX < _lastADX ||
                        _lastMACD < _prevMACD && _prevADX < _lastADX && _lastADX > 20)
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
            _lastMACD = _MACD.DataSeries[0].Last;

            // The prev value of the indicator
            _lastMACD = _MACD.DataSeries[0].Values[_MACD.DataSeries[0].Values.Count - 2];

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastMACD < 0 || _lastMACD < _prevMACD)
                    {

                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {

                    if (_lastMACD > 0 || _lastMACD > _prevMACD)
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
