using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

Trend robot on the SuperTrend indicator.

Buy: When the SuperTrend with a smaller period and deviation is higher than the SuperTrend with a larger period and deviation.

Sell: When the SuperTrend with a smaller period and deviation is lower than a SuperTrend with a larger period and deviation.

Exit from buy: When the SuperTrend with a smaller period and deviation is lower than a SuperTrend with a larger period and deviation.

Exit from sell: When the SuperTrend with a smaller period and deviation is higher than the SuperTrend with a larger period and deviation.
 */

namespace OsEngine.Robots.MyBots
{
    [Bot("IntersectionOfSuperTrends")] // We create an attribute so that we don't write anything to the BotFactory
    internal class IntersectionOfSuperTrends : BotPanel
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
        private StrategyParameterInt LengthFastSP;
        private StrategyParameterString TypeFastPrice;
        private StrategyParameterDecimal FastSPDeviation;
        private StrategyParameterInt LengthSlowSP;
        private StrategyParameterString TypeSlowPrice;
        private StrategyParameterDecimal SlowSPDeviation;

        // Indicator
        private Aindicator _FastSP;
        private Aindicator _SlowSP;
        public IntersectionOfSuperTrends(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthFastSP = CreateParameter("Length Fast SP", 10, 10, 200, 10, "Indicator");
            FastSPDeviation = CreateParameter("Fast SP Deviation", 1, 1m, 10, 1, "Indicator");
            TypeFastPrice = CreateParameter("Type Fast Price", "Median", new[] { "Median", "Typical" }, "Indicator");
            LengthSlowSP = CreateParameter("Length Slow SP", 50, 50, 300, 10, "Indicator");
            SlowSPDeviation = CreateParameter("Slow SP Deviation", 1, 1m, 10, 1, "Indicator");
            TypeSlowPrice = CreateParameter("Type Slow Price", "Median", new[] { "Median", "Typical" }, "Indicator");

            // Create indicator SuperTrend
            _FastSP = IndicatorsFactory.CreateIndicatorByName("SuperTrend_indicator", name + "SuperTrendFast", false);
            _FastSP = (Aindicator)_tab.CreateCandleIndicator(_FastSP, "Prime");
            ((IndicatorParameterInt)_FastSP.Parameters[0]).ValueInt = LengthFastSP.ValueInt;
            ((IndicatorParameterDecimal)_FastSP.Parameters[1]).ValueDecimal = FastSPDeviation.ValueDecimal;
            ((IndicatorParameterString)_FastSP.Parameters[2]).ValueString = TypeFastPrice.ValueString;
            ((IndicatorParameterBool)_FastSP.Parameters[3]).ValueBool = false;
            _FastSP.DataSeries[2].Color = Color.Red;
            _FastSP.Save();

            // Create indicator SuperTrend
            _SlowSP = IndicatorsFactory.CreateIndicatorByName("SuperTrend_indicator", name + "SuperTrendSlow", false);
            _SlowSP = (Aindicator)_tab.CreateCandleIndicator(_SlowSP, "Prime");
            ((IndicatorParameterInt)_SlowSP.Parameters[0]).ValueInt = LengthSlowSP.ValueInt;
            ((IndicatorParameterDecimal)_SlowSP.Parameters[1]).ValueDecimal = SlowSPDeviation.ValueDecimal;
            ((IndicatorParameterString)_SlowSP.Parameters[2]).ValueString = TypeSlowPrice.ValueString;
            ((IndicatorParameterBool)_SlowSP.Parameters[3]).ValueBool = false;
            _SlowSP.DataSeries[2].Color = Color.Green;
            _SlowSP.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfSuperTrends_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on the SuperTrend indicator. " +
                "Buy: When the SuperTrend with a smaller period and deviation is higher than the SuperTrend with a larger period and deviation." +
                "Sell: When the SuperTrend with a smaller period and deviation is lower than a SuperTrend with a larger period and deviation." +
                "Exit from buy: When the SuperTrend with a smaller period and deviation is lower than a SuperTrend with a larger period and deviation." +
                "Exit from sell: When the SuperTrend with a smaller period and deviation is higher than the SuperTrend with a larger period and deviation.";
        }

        private void IntersectionOfSuperTrends_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_FastSP.Parameters[0]).ValueInt = LengthFastSP.ValueInt;
            ((IndicatorParameterDecimal)_FastSP.Parameters[1]).ValueDecimal = FastSPDeviation.ValueDecimal;
            ((IndicatorParameterString)_FastSP.Parameters[2]).ValueString = TypeFastPrice.ValueString;
            ((IndicatorParameterBool)_FastSP.Parameters[3]).ValueBool = false;
            _FastSP.Save();
            _FastSP.Reload();

            ((IndicatorParameterInt)_SlowSP.Parameters[0]).ValueInt = LengthSlowSP.ValueInt;
            ((IndicatorParameterDecimal)_SlowSP.Parameters[1]).ValueDecimal = SlowSPDeviation.ValueDecimal;
            ((IndicatorParameterString)_SlowSP.Parameters[2]).ValueString = TypeSlowPrice.ValueString;
            ((IndicatorParameterBool)_SlowSP.Parameters[3]).ValueBool = false;
            _SlowSP.Save();
            _SlowSP.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "IntersectionOfSuperTrends";
        }

        public override void ShowIndividualSettingsDialog()
        {
        }

        // Logic
        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < LengthSlowSP.ValueInt + 10 || candles.Count < LengthFastSP.ValueInt + 10)
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

            // The last value of the indicator
            decimal lastFastSp = _FastSP.DataSeries[2].Last;
            decimal lastSlowSp = _SlowSP.DataSeries[2].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > lastFastSp && lastFastSp > lastSlowSp)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < lastFastSp && lastFastSp < lastSlowSp)
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

            // The last value of the indicator
            decimal lastFastSp = _FastSP.DataSeries[2].Last;
            decimal lastSlowSp = _SlowSP.DataSeries[2].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (lastFastSp < lastSlowSp)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastFastSp > lastSlowSp)
                    {
                        _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
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
