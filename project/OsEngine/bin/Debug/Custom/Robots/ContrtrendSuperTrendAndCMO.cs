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

Countertrend strategy using SuperTrend and CMO indicators.

Buy: When the price is above the SuperTrend indicator line and the CMO indicator value is below -50.

Sell: When the price is below the SuperTrend indicator line and the CMO indicator value is above 50.

Exit from buy: When the price is below the SuperTrend indicator line.

Exit from sell: When the price is above the SuperTrend indicator line.
 */

namespace OsEngine.Robots.MyBots
{
    [Bot("ContrtrendSuperTrendAndCMO")] // We create an attribute so that we don't write anything to the BotFactory
    internal class ContrtrendSuperTrendAndCMO : BotPanel
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
        private StrategyParameterInt LengthSP;
        private StrategyParameterString TypePrice;
        private StrategyParameterDecimal SPDeviation;
        private StrategyParameterBool Wicks;
        private StrategyParameterInt LengthCMO;

        // Indicator
        private Aindicator _SP;
        private Aindicator _CMO;

        public ContrtrendSuperTrendAndCMO(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthSP = CreateParameter("Length SP", 10, 10, 200, 10, "Indicator");
            SPDeviation = CreateParameter("SP Deviation", 1, 1m, 10, 1, "Indicator");
            TypePrice = CreateParameter("Type Price", "Median", new[] { "Median", "Typical" }, "Indicator");
            Wicks = CreateParameter("Use Candle Shadow", false, "Indicator");
            LengthCMO = CreateParameter("CMO Length", 14, 5, 100, 10, "Indicator");

            // Create indicator SuperTrend
            _SP = IndicatorsFactory.CreateIndicatorByName("SuperTrend_indicator", name + "SuperTrend", false);
            _SP = (Aindicator)_tab.CreateCandleIndicator(_SP, "Prime");
            ((IndicatorParameterInt)_SP.Parameters[0]).ValueInt = LengthSP.ValueInt;
            ((IndicatorParameterDecimal)_SP.Parameters[1]).ValueDecimal = SPDeviation.ValueDecimal;
            ((IndicatorParameterString)_SP.Parameters[2]).ValueString = TypePrice.ValueString;
            ((IndicatorParameterBool)_SP.Parameters[3]).ValueBool = Wicks.ValueBool;
            _SP.DataSeries[2].Color = Color.Red;
            _SP.Save();

            // Create indicator CMO
            _CMO = IndicatorsFactory.CreateIndicatorByName("CMO", name + "Cmo", false);
            _CMO = (Aindicator)_tab.CreateCandleIndicator(_CMO, "CmoArea");
            ((IndicatorParameterInt)_CMO.Parameters[0]).ValueInt = LengthCMO.ValueInt;
            _CMO.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += ContrtrendSuperTrendAndCMO_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Countertrend strategy using SuperTrend and CMO indicators. " +
                "Buy: When the price is above the SuperTrend indicator line and the CMO indicator value is below -50." +
                "Sell: When the price is below the SuperTrend indicator line and the CMO indicator value is above 50." +
                "Exit from buy: When the price is below the SuperTrend indicator line. " +
                "Exit from sell: When the price is above the SuperTrend indicator line. ";
        }

        private void ContrtrendSuperTrendAndCMO_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_SP.Parameters[0]).ValueInt = LengthSP.ValueInt;
            ((IndicatorParameterDecimal)_SP.Parameters[1]).ValueDecimal = SPDeviation.ValueDecimal;
            ((IndicatorParameterString)_SP.Parameters[2]).ValueString = TypePrice.ValueString;
            ((IndicatorParameterBool)_SP.Parameters[3]).ValueBool = Wicks.ValueBool;
            _SP.Save();
            _SP.Reload();

            ((IndicatorParameterInt)_CMO.Parameters[0]).ValueInt = LengthCMO.ValueInt;
            _CMO.Save();
            _CMO.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "ContrtrendSuperTrendAndCMO";
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
            if (candles.Count < LengthSP.ValueInt + 10 || candles.Count < LengthCMO.ValueInt + 10)
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
            decimal lastSp = _SP.DataSeries[2].Last;
            decimal lastCMO = _CMO.DataSeries[0].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > lastSp && lastCMO < -50)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < lastSp && lastCMO > 50)
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
            decimal lastSp = _SP.DataSeries[2].Last;

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
                    if (lastPrice < lastSp)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice > lastSp)
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
