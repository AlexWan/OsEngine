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

The trend robot on Fractal And CCI.

Buy:
1. Formed fractal at a local minimum.
2. The CCI curve has pushed off from the additional -300 level and is directed upwards.

Sell:
1. The local maximum is marked by a fractal.
2. The CCI line touched the 300 level and is directed downwards.

Exit from buy: trailing stop in % of the High of the candle on which you entered.

Exit from sell: trailing stop in % of the Low candle on which you entered.

 */


namespace OsEngine.Robots.AO
{
    [Bot("FractalAndCCI")] // We create an attribute so that we don't write anything to the BotFactory
    public class FractalAndCCI : BotPanel
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
        private StrategyParameterInt LengthCCI;

        // Indicator
        Aindicator _CCI;
        Aindicator _Fractal;

        // The last value of the indicator
        private decimal _lastCCI;
        private decimal _lastUpFract;
        private decimal _lastDownFract;

        // The prev value of the indicator
        private decimal _prevCCI;

        // Exit
        private StrategyParameterInt TrailingValueLong;
        private StrategyParameterInt TrailingValueShort;

        public FractalAndCCI(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthCCI = CreateParameter("CCI Length", 21, 7, 48, 7, "Indicator");

            // Create indicator CCI
            _CCI = IndicatorsFactory.CreateIndicatorByName("CCI", name + "CCI", false);
            _CCI = (Aindicator)_tab.CreateCandleIndicator(_CCI, "NewArea");
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = LengthCCI.ValueInt;
            _CCI.Save();

            // Create indicator Fractal
            _Fractal = IndicatorsFactory.CreateIndicatorByName("Fractal", name + "Fractal", false);
            _Fractal = (Aindicator)_tab.CreateCandleIndicator(_Fractal, "Prime");
            _Fractal.Save();

            // Exit
            TrailingValueLong = CreateParameter("Long Exit", 1, 5, 200, 5, "Exit");
            TrailingValueShort = CreateParameter("Short Exit", 1, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += FractalAndCCI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Fractal And CCI. " +
                "Buy: " +
                "1. Formed fractal at a local minimum. " +
                "2. The CCI curve has pushed off from the additional -300 level and is directed upwards. " +
                "Sell: " +
                "1. The local maximum is marked by a fractal. " +
                "2. The CCI line touched the 300 level and is directed downwards. " +
                "Exit from buy: trailing stop in % of the High of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the Low candle on which you entered.";
        }

        private void FractalAndCCI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = LengthCCI.ValueInt;
            _CCI.Save();
            _CCI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "FractalAndCCI";
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
            if (candles.Count < LengthCCI.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (StartTradeTime.Value > _tab.TimeServerCurrent ||
                EndTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            for (int i = _Fractal.DataSeries[1].Values.Count - 1; i > -1; i--)
            {
                if (_Fractal.DataSeries[1].Values[i] != 0)
                {
                    _lastUpFract = _Fractal.DataSeries[1].Values[i];
                    break;
                }
            }

            for (int i = _Fractal.DataSeries[0].Values.Count - 1; i > -1; i--)
            {
                if (_Fractal.DataSeries[0].Values[i] != 0)
                {
                    _lastDownFract = _Fractal.DataSeries[0].Values[i];
                    break;
                }
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
            _lastCCI = _CCI.DataSeries[0].Values[_CCI.DataSeries[0].Values.Count - 2];

            // The prev value of the indicator
            _prevCCI = _CCI.DataSeries[0].Values[_CCI.DataSeries[0].Values.Count - 3];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 3].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastDownFract < lastPrice && _prevCCI < -300 && _lastCCI > -300)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastUpFract > lastPrice && _prevCCI > 300 && _lastCCI < 300)
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
                    stopPrice = lov - lov * TrailingValueLong.ValueInt / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValueShort.ValueInt / 100;
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
