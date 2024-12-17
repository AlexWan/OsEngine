using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The contrtrend robot on Overbougth Oversold DPO with Bollinger.

Buy: When the current candle closed above the lower Bollinger line, and the previous candle closed below, 
and the DPO indicator came out of the oversold zone.

Sell: When the current candle closed below the upper Bollinger line, 
and the previous candle closed above, and the DPO indicator left the overbought zone.

Exit from buy: trailing stop in % of the loy of the candle on which you entered.

Exit from sell: trailing stop in % of the high of the candle on which you entered.
 */

namespace OsEngine.Robots.MyBots
{
    [Bot("OverbougthOversoldDpoWithBollinger")] // We create an attribute so that we don't write anything to the BotFactory
    internal class OverbougthOversoldDpoWithBollinger : BotPanel
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
        private StrategyParameterDecimal OverboughtLevel;
        private StrategyParameterDecimal OversoldLevel;
        private StrategyParameterInt BollingerLength;
        private StrategyParameterDecimal BollingerDeviation;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        // Indicator
        private Aindicator _Dpo;
        private Aindicator _Bollinger;

        public OverbougthOversoldDpoWithBollinger(string name, StartProgram startProgram) : base(name, startProgram)
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
            OverboughtLevel = CreateParameter("Overbought Level", 500m, 100, 1000, 100, "Indicator");
            OversoldLevel = CreateParameter("Oversold Level", -500m, -1000, -100, 100, "Indicator");
            BollingerLength = CreateParameter("Bollinger Length", 50, 10, 300, 10, "Indicator");
            BollingerDeviation = CreateParameter("Bollinger Deviation", 1.0m, 1, 5, 0.1m, "Indicator");

            // Exit
            TrailingValue = CreateParameter("Trailing Value", 1.0m, 1, 20, 1, "Exit");

            // Create indicator Dpo
            _Dpo = IndicatorsFactory.CreateIndicatorByName("DPO_Detrended_Price_Oscillator", name + "Dpo", false);
            _Dpo = (Aindicator)_tab.CreateCandleIndicator(_Dpo, "DpoArea");
            ((IndicatorParameterInt)_Dpo.Parameters[0]).ValueInt = DpoLength.ValueInt;
            _Dpo.Save();

            // Create indicator Bollinger
            _Bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _Bollinger = (Aindicator)_tab.CreateCandleIndicator(_Bollinger, "Prime");
            ((IndicatorParameterInt)_Bollinger.Parameters[0]).ValueInt = BollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_Bollinger.Parameters[1]).ValueDecimal = BollingerDeviation.ValueDecimal;
            _Bollinger.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += OverbougthOversoldDpoWithBollinger_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The contrtrend robot on Overbougth Oversold DPO with Bollinger. " +
                "Buy: When the current candle closed above the lower Bollinger line," +
                " and the previous candle closed below, and the DPO indicator came out of the oversold zone." +
                "Sell: When the current candle closed below the upper Bollinger line, " +
                "and the previous candle closed above, and the DPO indicator left the overbought zone. " +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";
        }

        private void OverbougthOversoldDpoWithBollinger_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Dpo.Parameters[0]).ValueInt = DpoLength.ValueInt;
            _Dpo.Save();
            _Dpo.Reload();

            ((IndicatorParameterInt)_Bollinger.Parameters[0]).ValueInt = BollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_Bollinger.Parameters[1]).ValueDecimal = BollingerDeviation.ValueDecimal;
            _Bollinger.Save();
            _Bollinger.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "OverbougthOversoldDpoWithBollinger";
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
            if (candles.Count < DpoLength.ValueInt + 10 ||
                candles.Count < BollingerLength.ValueInt)
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
            decimal prevDpo = _Dpo.DataSeries[0].Values[_Dpo.DataSeries[0].Values.Count - 2];
            decimal lastUpLineBol = _Bollinger.DataSeries[0].Last;
            decimal lastDownLineBol = _Bollinger.DataSeries[1].Last;
            decimal prevUpLineBol = _Bollinger.DataSeries[0].Values[_Bollinger.DataSeries[0].Values.Count - 2];
            decimal prevDownLineBol = _Bollinger.DataSeries[1].Values[_Bollinger.DataSeries[1].Values.Count - 2];

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal prevPrice = candles[candles.Count - 2].Close;

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
                    if (lastDpo > OversoldLevel.ValueDecimal && prevDpo < OversoldLevel.ValueDecimal && lastDownLineBol < lastPrice
                         && prevPrice < prevDownLineBol)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastDpo < OverboughtLevel.ValueDecimal && prevDpo > OverboughtLevel.ValueDecimal && lastUpLineBol > lastPrice
                        && prevPrice > prevUpLineBol)
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
