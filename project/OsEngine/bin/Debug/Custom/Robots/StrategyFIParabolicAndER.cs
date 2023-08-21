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
using OsEngine.Logging;

/* Description
trading robot for osengine

The trend robot on Strategy Force Index Parabolic And EfficiencyRatio.

Buy:
1. The price is higher than the value of the Parabolic indicator. For the next candle, the price crosses the indicator from the bottom up.
2. The value of the force index indicator is higher than MaxValueFi.
3. The value of the Er indicator is higher than MaxValueEr.
4. The values of the indicators are valid only if the points of the parabolic are no more than ParabolicCount.

Sale:
1. The price is lower than the value of the Parabolic indicator. For the next candle, the price crosses the indicator from top to bottom.
2. The value of the force index indicator is lower than MinValueFi.
3. The value of the Er indicator is higher than MaxValueEr.
4. The values of the indicators are valid only if the points of the parabolic are no more than ParabolicCount.

Sell: on the opposite Parabolic signal.

 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyFIParabolicAndER")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyFIParabolicAndER : BotPanel
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
        private StrategyParameterInt LengthFI;
        private StrategyParameterDecimal Step;
        private StrategyParameterDecimal MaxStep;
        private StrategyParameterInt LengthER;
        private StrategyParameterDecimal MaxValueFi;
        private StrategyParameterDecimal MinValueFi;
        private StrategyParameterDecimal MaxValueER;
        private StrategyParameterInt ParabolicCount;

        // Indicator
        Aindicator _FI;
        Aindicator _Parabolic;
        Aindicator _ER;

        // The last value of the indicator
        private decimal _lastFI;
        private decimal _lastParabolic;
        private decimal _lastER;

        public StrategyFIParabolicAndER(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthFI = CreateParameter("FI Length", 13, 7, 48, 7, "Indicator");
            Step = CreateParameter("Step", 0.1m, 0.01m, 0.1m, 0.01m, "Indicator");
            MaxStep = CreateParameter("Max Step", 0.1m, 0.01m, 0.1m, 0.01m, "Indicator");
            LengthER = CreateParameter("ER Length", 14, 7, 48, 7, "Indicator");
            MaxValueFi = CreateParameter("Max Value Fi", 0.5m, 0.01m, 0.1m, 0.01m, "Indicator");
            MinValueFi = CreateParameter("Min Value Fi", 0.5m, 0.01m, 0.1m, 0.01m, "Indicator");
            MaxValueER = CreateParameter("Max Value ER", 0.7m, 0.01m, 0.1m, 0.01m, "Indicator");
            ParabolicCount = CreateParameter("Parabolic Count", 5, 5, 50, 5, "Indicator");

            // Create indicator FI
            _FI = IndicatorsFactory.CreateIndicatorByName("ForceIndex", name + "ForceIndex", false);
            _FI = (Aindicator)_tab.CreateCandleIndicator(_FI, "NewArea");
            ((IndicatorParameterInt)_FI.Parameters[0]).ValueInt = LengthFI.ValueInt;
            _FI.Save();

            // Create indicator Parabolic
            _Parabolic = IndicatorsFactory.CreateIndicatorByName("ParabolicSAR", name + "Par", false);
            _Parabolic = (Aindicator)_tab.CreateCandleIndicator(_Parabolic, "Prime");
            ((IndicatorParameterDecimal)_Parabolic.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_Parabolic.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _Parabolic.Save();

            // Create indicator EfficiencyRatio
            _ER = IndicatorsFactory.CreateIndicatorByName("EfficiencyRatio", name + "EfficiencyRatio", false);
            _ER = (Aindicator)_tab.CreateCandleIndicator(_ER, "NewArea0");
            ((IndicatorParameterInt)_ER.Parameters[0]).ValueInt = LengthER.ValueInt;
            _ER.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyFIParabolicAndER_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Force Index Parabolic And EfficiencyRatio. " +
                "Buy: " +
                "1. The price is higher than the value of the Parabolic indicator. For the next candle, the price crosses the indicator from the bottom up. " +
                "2. The value of the force index indicator is higher than MaxValueFi. " +
                "3. The value of the Er indicator is higher than MaxValueEr. " +
                "4. The values of the indicators are valid only if the points of the parabolic are no more than ParabolicCount. " +
                "nSale: " +
                "1. The price is lower than the value of the Parabolic indicator. For the next candle, the price crosses the indicator from top to bottom. " +
                "2. The value of the force index indicator is lower than MinValueFi. " +
                "3. The value of the Er indicator is higher than MaxValueEr. " +
                "4. The values of the indicators are valid only if the points of the parabolic are no more than ParabolicCount. " +
                "Sell: on the opposite Parabolic signal.";
        }

        private void StrategyFIParabolicAndER_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_FI.Parameters[0]).ValueInt = LengthFI.ValueInt;
            _FI.Save();
            _FI.Reload();
            ((IndicatorParameterDecimal)_Parabolic.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_Parabolic.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _Parabolic.Save();
            _Parabolic.Reload();
            ((IndicatorParameterInt)_ER.Parameters[0]).ValueInt = LengthER.ValueInt;
            _ER.Save();
            _ER.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyFIParabolicAndER";
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
            if (candles.Count < LengthER.ValueInt ||
                candles.Count < LengthFI.ValueInt || 
                candles.Count < ParabolicCount.ValueInt)
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
            _lastFI = _FI.DataSeries[0].Last;
            _lastER = _ER.DataSeries[0].Last;
            _lastParabolic = _Parabolic.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                { 
                    if (lastPrice > _lastParabolic && _lastFI > MaxValueFi.ValueDecimal && _lastER > MaxValueER.ValueDecimal)
                    {
                        if(_Parabolic.DataSeries[0].Values[_Parabolic.DataSeries[0].Values.Count - ParabolicCount.ValueInt] < lastPrice)
                        {
                            return;
                        }

                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < _lastParabolic && _lastFI < MinValueFi.ValueDecimal && _lastER > MaxValueER.ValueDecimal)
                    {
                        if (_Parabolic.DataSeries[0].Values[_Parabolic.DataSeries[0].Values.Count - ParabolicCount.ValueInt] > lastPrice)
                        {
                            return;
                        }

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
            _lastParabolic = _Parabolic.DataSeries[0].Last;

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (lastPrice < _lastParabolic)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice > _lastParabolic)
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

