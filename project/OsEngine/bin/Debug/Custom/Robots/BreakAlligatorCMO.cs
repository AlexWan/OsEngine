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
using System.Runtime.CompilerServices;
using OsEngine.Market.Servers.Tinkoff.TinkoffJsonSchema;

/* Description
trading robot for osengine

The trend robot on Break Alligator with CMO.

Buy:
1. The value of the CMO indicator crosses the level 0 from the bottom up.
2. fast line (lips) above the middle line (teeth), middle line above the slow line (jaw).

Sell:
1. The value of the CMO indicator crosses level 0 from top to bottom.
2. fast line (lips) below the midline (teeth), middle line below the slow one (jaw).

Buy exit: trailing stop in % of the line of the candle on which you entered.
Sell ​​exit: trailing stop in % of the high of the candle where you entered.

 */


namespace OsEngine.Robots.CMO
{
    [Bot("BreakAlligatorCMO")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakAlligatorCMO : BotPanel
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
        private StrategyParameterInt LengthCMO;
        private StrategyParameterInt AlligatorFastLineLength;
        private StrategyParameterInt AlligatorMiddleLineLength;
        private StrategyParameterInt AlligatorSlowLineLength;

        // Indicator
        Aindicator _CMO;
        Aindicator _Alligator;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        // The last value of the indicator
        private decimal _lastCMO;
        private decimal _lastFastAlg;
        private decimal _lastMiddleAlg;
        private decimal _lastSlowAlg;

        // The prev value of the indicator
        private decimal _prevCMO;

        public BreakAlligatorCMO(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthCMO = CreateParameter("CMO Length", 14, 7, 48, 7, "Indicator");
            AlligatorFastLineLength = CreateParameter("Alligator Fast Line Length", 10, 10, 300, 10, "Indicator");
            AlligatorMiddleLineLength = CreateParameter("Alligator Middle Line Length", 20, 10, 300, 10, "Indicator");
            AlligatorSlowLineLength = CreateParameter("Alligator Slow Line Length", 30, 10, 300, 10, "Indicator");

            // Create indicator Alligator
            _Alligator = IndicatorsFactory.CreateIndicatorByName("Alligator", name + "Alligator", false);
            _Alligator = (Aindicator)_tab.CreateCandleIndicator(_Alligator, "Prime");
            ((IndicatorParameterInt)_Alligator.Parameters[0]).ValueInt = AlligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[2]).ValueInt = AlligatorMiddleLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[1]).ValueInt = AlligatorFastLineLength.ValueInt;
            _Alligator.Save();

            // Create indicator CMO
            _CMO = IndicatorsFactory.CreateIndicatorByName("CMO", name + "CMO", false);
            _CMO = (Aindicator)_tab.CreateCandleIndicator(_CMO, "NewArea");
            ((IndicatorParameterInt)_CMO.Parameters[0]).ValueInt = LengthCMO.ValueInt;
            _CMO.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakAlligatorCMO_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Break Alligator with CMO. " +
                "Buy: " +
                "1. The value of the CMO indicator crosses the level 0 from the bottom up. " +
                "2. fast line (lips) above the middle line (teeth), middle line above the slow line (jaw). " +
                "Sell: " +
                "1. The value of the CMO indicator crosses level 0 from top to bottom. " +
                "2. fast line (lips) below the midline (teeth), middle line below the slow one (jaw). " +
                "Buy exit: trailing stop in % of the line of the candle on which you entered. " +
                "Sell ​​exit: trailing stop in % of the high of the candle where you entered.";
        }

        private void BreakAlligatorCMO_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_CMO.Parameters[0]).ValueInt = LengthCMO.ValueInt;
            _CMO.Save();
            _CMO.Reload();
            ((IndicatorParameterInt)_Alligator.Parameters[0]).ValueInt = AlligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[2]).ValueInt = AlligatorMiddleLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[1]).ValueInt = AlligatorFastLineLength.ValueInt;
            _Alligator.Save();
            _Alligator.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakAlligatorCMO";
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
            if (candles.Count < LengthCMO.ValueInt)
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
            _lastCMO = _CMO.DataSeries[0].Last;
            _lastFastAlg = _Alligator.DataSeries[2].Last;
            _lastMiddleAlg = _Alligator.DataSeries[1].Last;
            _lastSlowAlg = _Alligator.DataSeries[0].Last;

            // The prev value of the indicator
            _prevCMO = _CMO.DataSeries[0].Values[_CMO.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastCMO > 0 && _prevCMO < 0 && _lastFastAlg > _lastMiddleAlg && _lastMiddleAlg > _lastSlowAlg)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastCMO < 0 && _prevCMO > 0 && _lastFastAlg < _lastMiddleAlg && _lastMiddleAlg < _lastSlowAlg)
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

