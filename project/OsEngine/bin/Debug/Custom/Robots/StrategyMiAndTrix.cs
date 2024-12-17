using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

Trend robot based on MassIndex and Trix indicators.

Buy: When the MassIndex indicator value is above the lower line, and the Trix indicator is greater than zero.

Sell: When the MassIndex indicator value is above the lower line, and the Trix indicator is less than zero.

Exit from buy: When the MassIndex indicator value is above the lower line, and the Trix indicator is less than zero.

Exit from sell: When the MassIndex indicator value is above the lower line, and the Trix indicator is greater than zero.

 */

namespace OsEngine.Robots.MyBots
{
    [Bot("StrategyMiAndTrix")] // We create an attribute so that we don't write anything to the BotFactory
    internal class StrategyMiAndTrix : BotPanel
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
        private StrategyParameterInt MiLength;
        private StrategyParameterInt MiSumLength;
        private StrategyParameterDecimal MiLine;
        private StrategyParameterInt LengthTrix;

        // Indicator
        private Aindicator _Mi;
        private Aindicator _Trix;

        public StrategyMiAndTrix(string name, StartProgram startProgram) : base(name, startProgram)
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
            MiLength = CreateParameter("Mi Length", 9, 5, 200, 5, "Indicator");
            MiSumLength = CreateParameter("Mi Sum Length", 25, 5, 200, 5, "Indicator");
            MiLine = CreateParameter("Mi Line", 26.5m, 5, 50, 1, "Indicator");
            LengthTrix = CreateParameter("Length Trix", 9, 7, 48, 7, "Indicator");

            // Create indicator MassIndex
            _Mi = IndicatorsFactory.CreateIndicatorByName("Mass_Index_MI", name + "MI", false);
            _Mi = (Aindicator)_tab.CreateCandleIndicator(_Mi, "MiArea");
            ((IndicatorParameterInt)_Mi.Parameters[0]).ValueInt = MiLength.ValueInt;
            ((IndicatorParameterInt)_Mi.Parameters[1]).ValueInt = MiSumLength.ValueInt;
            ((IndicatorParameterDecimal)_Mi.Parameters[2]).ValueDecimal = MiLine.ValueDecimal;
            ((IndicatorParameterDecimal)_Mi.Parameters[3]).ValueDecimal = MiLine.ValueDecimal;
            _Mi.Save();

            // Create indicator Trix
            _Trix = IndicatorsFactory.CreateIndicatorByName("Trix", name + "Trix", false);
            _Trix = (Aindicator)_tab.CreateCandleIndicator(_Trix, "NewArea");
            ((IndicatorParameterInt)_Trix.Parameters[0]).ValueInt = LengthTrix.ValueInt;
            _Trix.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyMi_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot based on MassIndex and Trix indicators. " +
                "Buy: When the MassIndex indicator value is above the lower line, and the Trix indicator is greater than zero." +
                "Sell: When the MassIndex indicator value is above the lower line, and the Trix indicator is less than zero." +
                "Exit from buy: When the MassIndex indicator value is above the lower line, and the Trix indicator is less than zero. " +
                "Exit from sell: When the MassIndex indicator value is above the lower line, and the Trix indicator is greater than zero.";
        }       

        private void StrategyMi_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Mi.Parameters[0]).ValueInt = MiLength.ValueInt;
            ((IndicatorParameterInt)_Mi.Parameters[1]).ValueInt = MiSumLength.ValueInt;
            ((IndicatorParameterDecimal)_Mi.Parameters[2]).ValueDecimal = MiLine.ValueDecimal;
            ((IndicatorParameterDecimal)_Mi.Parameters[3]).ValueDecimal = MiLine.ValueDecimal;
            _Mi.Save();
            _Mi.Reload();

            ((IndicatorParameterInt)_Trix.Parameters[0]).ValueInt = LengthTrix.ValueInt;
            _Trix.Save();
            _Trix.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyMiAndTrix";
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
            if (candles.Count < MiLength.ValueInt + 10 || candles.Count < MiSumLength.ValueInt
                || candles.Count < LengthTrix.ValueInt)
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
            decimal lastMi = _Mi.DataSeries[2].Last;
            decimal lastTrix = _Trix.DataSeries[0].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastMi > MiLine.ValueDecimal && lastTrix > 0)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastMi > MiLine.ValueDecimal && lastTrix < 0)
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
            
            // The last value of the indicator
            decimal lastMi = _Mi.DataSeries[2].Last;
            decimal lastTrix = _Trix.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = _tab.Securiti.PriceStep * Slippage.ValueDecimal / 100;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (lastMi > MiLine.ValueDecimal && lastTrix < 0)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastMi > MiLine.ValueDecimal && lastTrix > 0)
                    {
                        _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
                    }
                }
            }
        }

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
