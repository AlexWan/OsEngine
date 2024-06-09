using System;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

Counter-trend robot based on NRTR and ROC indicators.

Buy: When the candle closes below the NRTR line and the ROC indicator value is below the buy level from the parameters.

Sell: When the candle closed above the NRTR line and the ROC indicator value is above the sales level from the parameters.

Exit from buy: When the candle closed above the NRTR line.

Exit from sell: When the candle closed below the NRTR line.

 */

namespace OsEngine.Robots.MyBots
{
    [Bot("ContrTrendNrtrAndROC")] // We create an attribute so that we don't write anything to the BotFactory
    internal class ContrTrendNrtrAndROC : BotPanel
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
        private StrategyParameterInt LengthNrtr;
        private StrategyParameterDecimal DeviationNrtr;
        private StrategyParameterDecimal BuyValue;
        private StrategyParameterDecimal SellValue;
        private StrategyParameterInt LengthROC;

        // Indicator
        private Aindicator _Nrtr;
        private Aindicator _ROC;

        public ContrTrendNrtrAndROC(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthNrtr = CreateParameter("Length NRTR", 24, 5, 100, 5, "Indicator");
            DeviationNrtr = CreateParameter("Deviation NRTR", 1, 1m, 10, 1, "Indicator");
            LengthROC = CreateParameter("Length ROC", 14, 10, 200, 5, "Indicator");
            BuyValue = CreateParameter("Buy Value", 1.0m, 1, 10, 1, "Indicator");
            SellValue = CreateParameter("Sell Value", 1.0m, 1, 10, 1, "Indicator");

            // Create indicator NRTR
            _Nrtr = IndicatorsFactory.CreateIndicatorByName("NRTR", name + "Nrtr", false);
            _Nrtr = (Aindicator)_tab.CreateCandleIndicator(_Nrtr, "Prime");
            ((IndicatorParameterInt)_Nrtr.Parameters[0]).ValueInt = LengthNrtr.ValueInt;
            ((IndicatorParameterDecimal)_Nrtr.Parameters[1]).ValueDecimal = DeviationNrtr.ValueDecimal;
            _Nrtr.Save();

            // Create indicator CCI
            _ROC = IndicatorsFactory.CreateIndicatorByName("ROC", name + "ROC", false);
            _ROC = (Aindicator)_tab.CreateCandleIndicator(_ROC, "NewArea");
            ((IndicatorParameterInt)_ROC.Parameters[0]).ValueInt = LengthROC.ValueInt;
            _ROC.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += ContrTrendNrtr_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Counter-trend robot based on NRTR and ROC indicators. " +
                "Buy:  When the candle closes below the NRTR line and the ROC indicator value is below the buy level from the parameters." +
                "Sell: When the candle closed above the NRTR line and the ROC indicator value is above the sales level from the parameters." +
                "Exit from buy: When the candle closed above the NRTR line." +
                "Exit from sell: When the candle closed below the NRTR line. ";
        }    

        private void ContrTrendNrtr_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Nrtr.Parameters[0]).ValueInt = LengthNrtr.ValueInt;
            ((IndicatorParameterDecimal)_Nrtr.Parameters[1]).ValueDecimal = DeviationNrtr.ValueDecimal;
            _Nrtr.Save();
            _Nrtr.Reload();

            ((IndicatorParameterInt)_ROC.Parameters[0]).ValueInt = LengthROC.ValueInt;
            _ROC.Save();
            _ROC.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "ContrTrendNrtrAndROC";
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
            if (candles.Count < LengthNrtr.ValueInt || candles.Count < LengthROC.ValueInt)
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
            decimal lastNRTR = _Nrtr.DataSeries[2].Last;
            decimal lastROC = _ROC.DataSeries[0].Last;

            // The prev value of the indicator
            decimal prevROC = _ROC.DataSeries[0].Values[_ROC.DataSeries[0].Values.Count - 2];

            decimal lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice < lastNRTR && lastROC < -BuyValue.ValueDecimal)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice > lastNRTR && lastROC > SellValue.ValueDecimal)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - slippage);
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
            decimal lastNRTR = _Nrtr.DataSeries[2].Last;

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
                    if (lastPrice > lastNRTR)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice < lastNRTR)
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
