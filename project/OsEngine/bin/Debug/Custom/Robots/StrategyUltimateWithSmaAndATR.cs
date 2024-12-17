using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
trading robot for osengine

The trend robot on Strategy Ultimate With Sma And ATR.

Buy:
1. The candle closed above the Sma.
2. Ultimate Oscillator is above the BuyValue level.
Sell:
1. The candle closed below the Sma.
2. Ultimate Oscillator is below the SellValue level.

Exit from buy: trailing stop in % of the loy of the candle on which you entered - exit coefficient * Atr.
Exit from sell: trailing stop in % of the high of the candle on which you entered + exit coefficient * Atr.

 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyUltimateWithSmaAndATR")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyUltimateWithSmaAndATR : BotPanel
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
        private StrategyParameterDecimal BuyValue;
        private StrategyParameterDecimal SellValue;
        private StrategyParameterInt PeriodOneUltimate;
        private StrategyParameterInt PeriodTwoUltimate;
        private StrategyParameterInt PeriodThreeUltimate;
        private StrategyParameterInt LengthAtr;
        private StrategyParameterDecimal ExitCoefAtr;
        private StrategyParameterInt PeriodSma;

        // Indicator
        Aindicator _UltimateOsc;
        Aindicator _ATR;
        Aindicator _SMA;

        // The last value of the indicator
        private decimal _lastSma;
        private decimal _lastATR;
        private decimal _lastUltimate;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        public StrategyUltimateWithSmaAndATR(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodOneUltimate = CreateParameter("PeriodOneUltimate", 7, 10, 300, 1, "Indicator");
            PeriodTwoUltimate = CreateParameter("PeriodTwoUltimate", 14, 10, 300, 1, "Indicator");
            PeriodThreeUltimate = CreateParameter("PeriodThreeUltimate", 28, 9, 300, 1, "Indicator");
            BuyValue = CreateParameter("Buy Value", 10.0m, 10, 300, 10, "Indicator");
            SellValue = CreateParameter("Sell Value", 10.0m, 10, 300, 10, "Indicator");
            LengthAtr = CreateParameter("Length ATR", 14, 7, 48, 7, "Indicator");
            ExitCoefAtr = CreateParameter("Coef Atr", 1, 1m, 10, 1, "Indicator");
            PeriodSma = CreateParameter("Period Simple Moving Average", 20, 10, 200, 10, "Indicator");

            // Create indicator CCI
            _UltimateOsc = IndicatorsFactory.CreateIndicatorByName("UltimateOscilator", name + "UltimateOscilator", false);
            _UltimateOsc = (Aindicator)_tab.CreateCandleIndicator(_UltimateOsc, "NewArea");
            ((IndicatorParameterInt)_UltimateOsc.Parameters[0]).ValueInt = PeriodOneUltimate.ValueInt;
            ((IndicatorParameterInt)_UltimateOsc.Parameters[1]).ValueInt = PeriodTwoUltimate.ValueInt;
            ((IndicatorParameterInt)_UltimateOsc.Parameters[2]).ValueInt = PeriodThreeUltimate.ValueInt;
            _UltimateOsc.Save();

            // Create indicator ATR
            _ATR = IndicatorsFactory.CreateIndicatorByName("ATR", name + "Atr", false);
            _ATR = (Aindicator)_tab.CreateCandleIndicator(_ATR, "NewArea");
            ((IndicatorParameterInt)_ATR.Parameters[0]).ValueInt = LengthAtr.ValueInt;
            _ATR.Save();

            // Create indicator
            _SMA = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SMA", false);
            _SMA = (Aindicator)_tab.CreateCandleIndicator(_SMA, "Prime");
            ((IndicatorParameterInt)_SMA.Parameters[0]).ValueInt = PeriodSma.ValueInt;
            _SMA.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += OverboughtOversoldCCI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Ultimate With Sma And ATR. " +
                "Buy: " +
                "1. The candle closed above the Sma. " +
                "2. Ultimate Oscillator is above the BuyValue level. " +
                "Sell: " +
                "1. The candle closed below the Sma. " +
                "2. Ultimate Oscillator is below the SellValue level. " +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered - exit coefficient * Atr. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered + exit coefficient * Atr.";
        }

        private void OverboughtOversoldCCI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_UltimateOsc.Parameters[0]).ValueInt = PeriodOneUltimate.ValueInt;
            ((IndicatorParameterInt)_UltimateOsc.Parameters[1]).ValueInt = PeriodTwoUltimate.ValueInt;
            ((IndicatorParameterInt)_UltimateOsc.Parameters[2]).ValueInt = PeriodThreeUltimate.ValueInt;
            _UltimateOsc.Save();
            _UltimateOsc.Reload();
            ((IndicatorParameterInt)_ATR.Parameters[0]).ValueInt = LengthAtr.ValueInt;
            _ATR.Save();
            _ATR.Reload();
            ((IndicatorParameterInt)_SMA.Parameters[0]).ValueInt = PeriodSma.ValueInt;
            _SMA.Save();
            _SMA.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyUltimateWithSmaAndATR";
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
            if (candles.Count < PeriodOneUltimate.ValueInt ||
                candles.Count < PeriodTwoUltimate.ValueInt ||
                candles.Count < PeriodThreeUltimate.ValueInt)
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
            _lastUltimate = _UltimateOsc.DataSeries[0].Last;
            _lastSma = _SMA.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastUltimate > BuyValue.ValueDecimal && _lastSma < lastPrice)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastUltimate < SellValue.ValueDecimal && _lastSma > lastPrice)
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
            
            // The last value of the indicator
            _lastATR = _ATR.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * TrailingValue.ValueDecimal / 100 + _lastATR * ExitCoefAtr.ValueDecimal;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(position, stopPrice, stopPrice);
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

