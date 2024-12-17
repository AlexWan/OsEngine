using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

Trend robot based on Alligator and DPO indicators.

Buy: When the fast Alligator line is above the middle line and the average line is above the slow line, 
and the DPO indicator is above zero.

Sell: When the fast Alligator line is below the middle line and the average line is below the slow line,
and the DPO indicator is below zero.

Exit from buy: the candle closed below the slow Alligator line.

Exit from sell: the candle closed above the slow Alligator line.
 */

namespace OsEngine.Robots.MyBots
{
    [Bot("StrategyDpoAndAlligator")] // We create an attribute so that we don't write anything to the BotFactory
    internal class StrategyDpoAndAlligator : BotPanel
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
        private StrategyParameterInt DpoLength;
        private StrategyParameterInt AlligatorFastLineLength;
        private StrategyParameterInt AlligatorMiddleLineLength;
        private StrategyParameterInt AlligatorSlowLineLength;

        // Indicator
        private Aindicator _Dpo;
        private Aindicator _Alligator;

        public StrategyDpoAndAlligator(string name, StartProgram startProgram) : base(name, startProgram)
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
            DpoLength = CreateParameter("Dpo Length", 14, 5, 200, 10, "Indicator");
            AlligatorFastLineLength = CreateParameter("Period Simple Moving Fast", 10, 10, 300, 10, "Indicator");
            AlligatorMiddleLineLength = CreateParameter("Period Simple Moving Average Middle", 20, 10, 300, 10, "Indicator");
            AlligatorSlowLineLength = CreateParameter("Period Simple Moving Slow", 30, 10, 300, 10, "Indicator");

            // Create indicator Dpo
            _Dpo = IndicatorsFactory.CreateIndicatorByName("DPO_Detrended_Price_Oscillator", name + "Dpo", false);
            _Dpo = (Aindicator)_tab.CreateCandleIndicator(_Dpo, "DpoArea");
            ((IndicatorParameterInt)_Dpo.Parameters[0]).ValueInt = DpoLength.ValueInt;
            _Dpo.Save();

            // Create indicator Alligator
            _Alligator = IndicatorsFactory.CreateIndicatorByName("Alligator", name + "Alligator", false);
            _Alligator = (Aindicator)_tab.CreateCandleIndicator(_Alligator, "Prime");
            ((IndicatorParameterInt)_Alligator.Parameters[0]).ValueInt = AlligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[1]).ValueInt = AlligatorMiddleLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[2]).ValueInt = AlligatorFastLineLength.ValueInt;
            _Alligator.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyDpo_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot based on Alligator and DPO indicators. " +
                "Buy: When the fast Alligator line is above the middle line and the average line is above the slow line, " +
                "and the DPO indicator is above zero." +
                "Sell: When the fast Alligator line is below the middle line and the average line is below the slow line, " +
                "and the DPO indicator is below zero. " +
                "Exit from buy: the candle closed below the slow Alligator line. " +
                "Exit from sell: the candle closed above the slow Alligator line.";
        }

        private void StrategyDpo_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Dpo.Parameters[0]).ValueInt = DpoLength.ValueInt;
            _Dpo.Save();
            _Dpo.Reload();

            ((IndicatorParameterInt)_Alligator.Parameters[0]).ValueInt = AlligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[1]).ValueInt = AlligatorMiddleLineLength.ValueInt;
            ((IndicatorParameterInt)_Alligator.Parameters[2]).ValueInt = AlligatorFastLineLength.ValueInt;
            _Alligator.Save();
            _Alligator.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyDpoAndAlligator";
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
            if (candles.Count < DpoLength.ValueInt ||
                candles.Count < AlligatorSlowLineLength.ValueInt + 10)
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

            // The value of the indicator
            decimal lastDpo = _Dpo.DataSeries[0].Last;
            decimal lastFast = _Alligator.DataSeries[2].Last;
            decimal lastMiddle = _Alligator.DataSeries[1].Last;
            decimal lastSlow = _Alligator.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            if (lastDpo == 0)
            {
                return;
            }

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = Slippage.ValueDecimal / 100 * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastDpo > 0 && lastPrice > lastFast && lastFast > lastMiddle && lastMiddle > lastSlow)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastDpo < 0 && lastPrice < lastFast && lastFast < lastMiddle && lastMiddle < lastSlow)
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
           
            decimal lastSlow = _Alligator.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (lastPrice < lastSlow)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice > lastSlow)
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
