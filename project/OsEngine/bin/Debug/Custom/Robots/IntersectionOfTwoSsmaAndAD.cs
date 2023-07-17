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

/* Description
trading robot for osengine

The trend robot on strategy for two Ssma and Accumulation Distribution.

Buy: fast Ssma above slow Ssma and AD rising.

Sell: fast Ssma below slow Ssma and AD falling.

Exit:
From purchase: fast Ssma below slow Ssma;

From sale: fast Ssma is higher than slow Ssma.

 */


namespace OsEngine.Robots.AO
{
    [Bot("IntersectionOfTwoSsmaAndAD")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionOfTwoSsmaAndAD : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // Setting indicator
        private StrategyParameterInt PeriodSsmaFast ;
        private StrategyParameterInt PeriodSsmaSlow;

        // Indicator
        Aindicator _AD;
        Aindicator _FastSsma;
        Aindicator _SlowSsma;

        // The last value of the indicators
        private decimal _lastFastSsma;
        private decimal _lastSlowSsma;
        private decimal _lastAD;

        // The prevlast value of the indicator
        private decimal _prevAD;

        public IntersectionOfTwoSsmaAndAD(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodSsmaFast = CreateParameter("Period Ssma Fast", 13, 10, 300, 10, "Indicator");
            PeriodSsmaSlow = CreateParameter("Period Ssma Slow", 26, 10, 300, 10, "Indicator");

            // Create indicator AD
            _AD = IndicatorsFactory.CreateIndicatorByName("AccumulationDistribution", name + "AD", false);
            _AD = (Aindicator)_tab.CreateCandleIndicator(_AD, "NewArea");
            _AD.Save();

            // Create indicator FastSsma
            _FastSsma = IndicatorsFactory.CreateIndicatorByName("Ssma", name + "Ssma Fast", false);
            _FastSsma = (Aindicator)_tab.CreateCandleIndicator(_FastSsma, "Prime");
            ((IndicatorParameterInt)_FastSsma.Parameters[0]).ValueInt = PeriodSsmaFast.ValueInt;
            _FastSsma.DataSeries[0].Color = Color.Yellow;
            _FastSsma.Save();

            // Create indicator SlowSsma
            _SlowSsma = IndicatorsFactory.CreateIndicatorByName("Ssma", name + "Ssma Slow", false);
            _SlowSsma = (Aindicator)_tab.CreateCandleIndicator(_SlowSsma, "Prime");
            ((IndicatorParameterInt)_SlowSsma.Parameters[0]).ValueInt = PeriodSsmaSlow.ValueInt;
            _SlowSsma.DataSeries[0].Color = Color.Green;
            _SlowSsma.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfTwoSsmaAndAD_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "trading robot for osengine " +
                "The trend robot on strategy for two Ssma and Accumulation Distribution. " +
                "Buy: fast Ssma above slow Ssma and AD rising. " +
                "Sell: fast Ssma below slow Ssma and AD falling. " +
                "Exit: " +
                "From purchase: fast Ssma below slow Ssma; " +
                "From sale: fast Ssma is higher than slow Ssma.";
        }

        // Indicator Update event
        private void IntersectionOfTwoSsmaAndAD_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_FastSsma.Parameters[0]).ValueInt = PeriodSsmaFast.ValueInt;
            _FastSsma.Save();
            _FastSsma.Reload();

            ((IndicatorParameterInt)_SlowSsma.Parameters[0]).ValueInt = PeriodSsmaSlow.ValueInt;
            _SlowSsma.Save();
            _SlowSsma.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionOfTwoSsmaAndAD";
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
            if (candles.Count < PeriodSsmaSlow.ValueInt)
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
                // The last value of the indicators
                _lastFastSsma = _FastSsma.DataSeries[0].Last;
                _lastSlowSsma = _SlowSsma.DataSeries[0].Last;
                _lastAD = _AD.DataSeries[0].Last;

                // The prevlast value of the indicator
                _prevAD = _AD.DataSeries[0].Values[_AD.DataSeries[0].Values.Count - 2];

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastFastSsma > _lastSlowSsma && _lastAD > _prevAD)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastFastSsma < _lastSlowSsma && _lastAD < _prevAD)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage);
                    }
                }
                return;
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // The last value of the indicators
            _lastFastSsma = _FastSsma.DataSeries[0].Last;
            _lastSlowSsma = _SlowSsma.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (positions.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastFastSsma < _lastSlowSsma)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastFastSsma > _lastSlowSsma)
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
