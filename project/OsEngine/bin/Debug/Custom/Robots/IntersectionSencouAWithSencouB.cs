using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;

/* Description
trading robot for osengine

The trend robot on Ichimocu.

Buy: The Senkou A line crosses the Senkou B line from bottom to top.
Sell: The Senkou A line crosses the Senkou B line from top to bottom.

Exit from buy: trailing stop in % of the loy of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.
 
 */


namespace OsEngine.Robots.AO
{
    [Bot("IntersectionSencouAWithSencouB")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionSencouAWithSencouB : BotPanel
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
        private StrategyParameterInt TenkanLength;
        private StrategyParameterInt KijunLength;
        private StrategyParameterInt SenkouLength;
        private StrategyParameterInt ChinkouLength;
        private StrategyParameterInt Offset;

        // Indicator
        Aindicator _Ichomoku;

        // The last value of the indicator
        private decimal _lastSenkouA;
        private decimal _lastSenkouB;

        // The prev value of the indicator
        private decimal _prevSenkouA;
        private decimal _prevSenkouB;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        public IntersectionSencouAWithSencouB(string name, StartProgram startProgram) : base(name, startProgram)
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
            TenkanLength = CreateParameter("Tenkan Length", 9, 1, 50, 3, "Indicator");
            KijunLength = CreateParameter("Kijun Length", 26, 1, 50, 4, "Indicator");
            SenkouLength = CreateParameter("Senkou Length", 52, 1, 100, 8, "Indicator");
            ChinkouLength = CreateParameter("Chinkou Length", 26, 1, 50, 4, "Indicator");
            Offset = CreateParameter("Offset", 26, 1, 50, 4, "Indicator");

            // Create indicator _Ichomoku
            _Ichomoku = IndicatorsFactory.CreateIndicatorByName("Ichimoku", name + "Ichimoku", false);
            _Ichomoku = (Aindicator)_tab.CreateCandleIndicator(_Ichomoku, "Prime");
            ((IndicatorParameterInt)_Ichomoku.Parameters[0]).ValueInt = TenkanLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[1]).ValueInt = KijunLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[2]).ValueInt = SenkouLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[3]).ValueInt = ChinkouLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[4]).ValueInt = Offset.ValueInt;
            _Ichomoku.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionSencouAWithSencouB_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Ichimocu. " +
                "Buy: The Senkou A line crosses the Senkou B line from bottom to top. " +
                "Sell: The Senkou A line crosses the Senkou B line from top to bottom. " +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";
        }

        private void IntersectionSencouAWithSencouB_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Ichomoku.Parameters[0]).ValueInt = TenkanLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[1]).ValueInt = KijunLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[2]).ValueInt = SenkouLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[3]).ValueInt = ChinkouLength.ValueInt;
            ((IndicatorParameterInt)_Ichomoku.Parameters[4]).ValueInt = Offset.ValueInt;
            _Ichomoku.Save();
            _Ichomoku.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionSencouAWithSencouB";
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
            if (candles.Count < TenkanLength.ValueInt ||
                candles.Count < KijunLength.ValueInt ||
                candles.Count < SenkouLength.ValueInt ||
                candles.Count < ChinkouLength.ValueInt ||
                candles.Count < Offset.ValueInt)
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
            _lastSenkouA = _Ichomoku.DataSeries[3].Last;
            _lastSenkouB = _Ichomoku.DataSeries[4].Last;

            // The prev value of the indicator
            _prevSenkouA = _Ichomoku.DataSeries[3].Values[_Ichomoku.DataSeries[3].Values.Count - 2];
            _prevSenkouB = _Ichomoku.DataSeries[4].Values[_Ichomoku.DataSeries[4].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_prevSenkouA < _prevSenkouB && _lastSenkouA > _lastSenkouB)
                    {
                        _tab.BuyAtLimit(GetVolume(), lastPrice + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_prevSenkouA > _prevSenkouB && _lastSenkouA < _lastSenkouB)
                    {
                        _tab.SellAtLimit(GetVolume(), lastPrice - _slippage);
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