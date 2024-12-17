using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The contrtrend robot on Overbougth Oversold DeMarket.

Buy: When the price is below the Sma indicator and the DeMarker indicator has left the oversold zone.

Sell: When the price is above the Sma indicator and the DeMarker indicator has left the overbought zone.

Exit from buy: trailing stop in % of the loy of the candle on which you entered.

Exit from sell: trailing stop in % of the high of the candle on which you entered.
 */

namespace OsEngine.Robots.MyBots
{
    [Bot("OverbougthOversoldDeMarker")] // We create an attribute so that we don't write anything to the BotFactory
    internal class OverbougthOversoldDeMarker : BotPanel
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
        private StrategyParameterInt DeMLength;
        private StrategyParameterDecimal OverboughtLevel;
        private StrategyParameterDecimal OversoldLevel;
        private StrategyParameterInt LengthSma;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        // Indicator
        private Aindicator _DeM;
        private Aindicator _SMA;
        public OverbougthOversoldDeMarker(string name, StartProgram startProgram) : base(name, startProgram)
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
            DeMLength = CreateParameter("DeM Length", 14, 5, 200, 10, "Indicator");
            OverboughtLevel = CreateParameter("Overbought Level", 0.7m, 0.5m, 1, 0.1m, "Indicator");
            OversoldLevel = CreateParameter("Oversold Level", 0.3m, 0.1m, 0.5m, 0.1m, "Indicator");
            LengthSma = CreateParameter("Length SMA", 20, 10, 200, 10, "Indicator");

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 1, 20, 1, "Exit");

            // Create indicator DeMarker
            _DeM = IndicatorsFactory.CreateIndicatorByName("DeMarker_DeM", name + "DeMarker", false);
            _DeM = (Aindicator)_tab.CreateCandleIndicator(_DeM, "DeMArea");
            ((IndicatorParameterInt)_DeM.Parameters[0]).ValueInt = DeMLength.ValueInt;
            ((IndicatorParameterDecimal)_DeM.Parameters[1]).ValueDecimal = OverboughtLevel.ValueDecimal;
            ((IndicatorParameterDecimal)_DeM.Parameters[2]).ValueDecimal = OversoldLevel.ValueDecimal;
            _DeM.Save();

            // Create indicator
            _SMA = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SMA", false);
            _SMA = (Aindicator)_tab.CreateCandleIndicator(_SMA, "Prime");
            ((IndicatorParameterInt)_SMA.Parameters[0]).ValueInt = LengthSma.ValueInt;
            _SMA.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += OverbougthOversoldDeMarker_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The contrtrend robot on Overbougth Oversold DeMarket. " +
                "Buy: When the price is below the Sma indicator and the DeMarker indicator has left the oversold zone. " +
                "Sell: When the price is above the Sma indicator and the DeMarker indicator has left the overbought zone. " +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";
        }

        private void OverbougthOversoldDeMarker_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_DeM.Parameters[0]).ValueInt = DeMLength.ValueInt;
            ((IndicatorParameterDecimal)_DeM.Parameters[1]).ValueDecimal = OverboughtLevel.ValueDecimal;
            ((IndicatorParameterDecimal)_DeM.Parameters[2]).ValueDecimal = OversoldLevel.ValueDecimal;
            _DeM.Save();
            _DeM.Reload();

            ((IndicatorParameterInt)_SMA.Parameters[0]).ValueInt = LengthSma.ValueInt;
            _SMA.Save();
            _SMA.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "OverbougthOversoldDeMarker";
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
            if (candles.Count < DeMLength.ValueInt + 10 ||
                candles.Count < LengthSma.ValueInt)
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
            decimal lastDeM = _DeM.DataSeries[0].Last;
            decimal prevDeM = _DeM.DataSeries[0].Values[_DeM.DataSeries[0].Values.Count - 2];
            decimal _lastSma = _SMA.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;
            if (lastDeM == 0)
            {
                return;
            }

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = Slippage.ValueDecimal/100 * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastDeM > OversoldLevel.ValueDecimal && prevDeM < OversoldLevel.ValueDecimal && _lastSma > lastPrice)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastDeM < OverboughtLevel.ValueDecimal && prevDeM > OverboughtLevel.ValueDecimal && _lastSma < lastPrice)
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
            
            decimal stopPrice;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
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
