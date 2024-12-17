using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

Trend robot on the NRTR and SmaChannel indicators.

Buy: When the candle closed above the upper SmaChannel line and above the NRTR line.

Sell: When the candle closed below the lower SmaChannel line and below the NRTR line.

Exit from buy: Set a trailing stop along the NRTR line and at the lower border of the SmaChannel indicator. 
The calculation method that is further from the current price is selected.

Exit from sell: Set a trailing stop along the NRTR line and at the upper border of the SmaChannel indicator. 
The calculation method that is further from the current price is selected.

 */

namespace OsEngine.Robots.MyBots
{
    [Bot("StrategyNrtrAndSmaChannel")] // We create an attribute so that we don't write anything to the BotFactory
    internal class StrategyNrtrAndSmaChannel : BotPanel
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
        private StrategyParameterInt SmaLength;
        private StrategyParameterDecimal SmaDeviation;

        // Indicator
        private Aindicator _Nrtr;
        private Aindicator _SmaChannel;

        public StrategyNrtrAndSmaChannel(string name, StartProgram startProgram) : base(name, startProgram)
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
            SmaLength = CreateParameter("Length Sma", 10, 10, 300, 10, "Indicator");
            SmaDeviation = CreateParameter("Deviation Sma", 1.0m, 1, 10, 1, "Indicator");

            // Create indicator NRTR
            _Nrtr = IndicatorsFactory.CreateIndicatorByName("NRTR", name + "Nrtr", false);
            _Nrtr = (Aindicator)_tab.CreateCandleIndicator(_Nrtr, "Prime");
            ((IndicatorParameterInt)_Nrtr.Parameters[0]).ValueInt = LengthNrtr.ValueInt;
            ((IndicatorParameterDecimal)_Nrtr.Parameters[1]).ValueDecimal = DeviationNrtr.ValueDecimal;
            _Nrtr.Save();

            // Create indicator SmaChannel
            _SmaChannel = IndicatorsFactory.CreateIndicatorByName("SmaChannel", name + "SmaChannel", false);
            _SmaChannel = (Aindicator)_tab.CreateCandleIndicator(_SmaChannel, "Prime");
            ((IndicatorParameterInt)_SmaChannel.Parameters[0]).ValueInt = SmaLength.ValueInt;
            ((IndicatorParameterDecimal)_SmaChannel.Parameters[1]).ValueDecimal = SmaDeviation.ValueDecimal;
            _SmaChannel.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyNrtrAndAdx_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on the NRTR and SmaChannel indicators. " +
                "Buy:  When the candle closed above the upper SmaChannel line and above the NRTR line." +
                "Sell: When the candle closed below the lower SmaChannel line and below the NRTR line." +
                "Exit from buy: Set a trailing stop along the NRTR line and at the lower border of the SmaChannel indicator." +
                " The calculation method that is further from the current price is selected." +
                "Exit from sell: Set a trailing stop along the NRTR line and at the upper border of the SmaChannel indicator." +
                " The calculation method that is further from the current price is selected. ";
        }       

        private void StrategyNrtrAndAdx_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Nrtr.Parameters[0]).ValueInt = LengthNrtr.ValueInt;
            ((IndicatorParameterDecimal)_Nrtr.Parameters[1]).ValueDecimal = DeviationNrtr.ValueDecimal;
            _Nrtr.Save();
            _Nrtr.Reload();

            ((IndicatorParameterInt)_SmaChannel.Parameters[0]).ValueInt = SmaLength.ValueInt;
            ((IndicatorParameterDecimal)_SmaChannel.Parameters[1]).ValueDecimal = SmaDeviation.ValueDecimal;
            _SmaChannel.Save();
            _SmaChannel.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyNrtrAndSmaChannel";
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
            if (candles.Count < LengthNrtr.ValueInt ||
                candles.Count < SmaLength.ValueInt)
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
            decimal lastUpSma = _SmaChannel.DataSeries[0].Last;
            decimal lastDownSma = _SmaChannel.DataSeries[2].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > lastNRTR  && lastPrice > lastUpSma)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < lastNRTR  && lastPrice < lastDownSma)
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
            

            // The last value of the indicator
            decimal lastNRTR = _Nrtr.DataSeries[2].Last;
            decimal lastUpSma = _SmaChannel.DataSeries[0].Last;
            decimal lastDownSma = _SmaChannel.DataSeries[2].Last;

            decimal stop_level = 0;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    
                    stop_level = lastNRTR < lastDownSma ? lastNRTR : lastDownSma;
                }
                else // If the direction of the position is sale
                {
                    
                    stop_level = lastNRTR > lastUpSma ? lastNRTR : lastUpSma;
                }
                _tab.CloseAtTrailingStop(pos, stop_level, stop_level);
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
