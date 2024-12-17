using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;

/*Discription
Trading robot for osengine

Trend robot at the strategy on Adaptive Look Back and Bollinger

Buy: the price is above the upper Bollinger band.

Sell: the price is below the lower Bollinger band.

Exit the buy: trailing stop in % of the loy of the candle on which the minus exit coefficient entered * Adaptive Look Back.
Exit the sell: trailing stop in % of the high of the candle on which you entered plus the entry coefficient * Adaptive Look. 
*/

namespace OsEngine.Robots.My_bots
{
    [Bot("StrategyAdaptiveLookBackandBollinger")]//We create an attribute so that we don't write anything in the Boot factory
    public class StrategyAdaptiveLookBackandBollinger : BotPanel

    {
        BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // Indicator Settings
        private StrategyParameterInt BollingerLength;
        private StrategyParameterDecimal BollingerDeviation;
        private StrategyParameterInt PeriodALB;
        private StrategyParameterDecimal CoefExitALB;
        private StrategyParameterDecimal CoefEntryALB;
        // Indicator
        private Aindicator ALB;
        private Aindicator Bollinger;


        // The last value of the indicators      
        private decimal _lastUpBollinger;
        private decimal _lastDownBollinger;
        private decimal _lastALB;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        public StrategyAdaptiveLookBackandBollinger(string name, StartProgram startProgram) : base(name, startProgram)
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

            // Indicator Settings
            PeriodALB = CreateParameter("Adaptive Look Back", 5, 1, 10, 1, "Indicator");
            CoefExitALB = CreateParameter("CoefExitALB", 0.2m, 0.01m, 2, 0.02m, "Indicator");
            CoefEntryALB= CreateParameter("CoefEntrytALB", 0.2m, 0.01m, 2, 0.02m, "Indicator");
            BollingerLength = CreateParameter("BollingerLength", 250, 50, 500, 20, "Indicator");
            BollingerDeviation = CreateParameter("BollingerDeviation", 0.2m, 0.01m, 2, 0.02m, "Indicator");


            // Create indicator Adaptive Look Back
            ALB = IndicatorsFactory.CreateIndicatorByName(nameClass: "AdaptiveLookBack", name: name + "ALB", canDelete: false);
            ALB = (Aindicator)_tab.CreateCandleIndicator(ALB, nameArea: "NewArea");
            ((IndicatorParameterInt)ALB.Parameters[0]).ValueInt = PeriodALB.ValueInt;
            ALB.Save();


            // Create indicator Bollinger
            Bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            Bollinger = (Aindicator)_tab.CreateCandleIndicator(Bollinger, "Prime");
            ((IndicatorParameterInt)Bollinger.Parameters[0]).ValueInt = BollingerLength.ValueInt;
            ((IndicatorParameterDecimal)Bollinger.Parameters[1]).ValueDecimal = BollingerDeviation.ValueDecimal;
            Bollinger.DataSeries[0].Color = Color.Red;
            Bollinger.DataSeries[1].Color = Color.Red;
            Bollinger.Save();
            ParametrsChangeByUser += StrategyAdaptiveLookBackandBollinger_ParametrsChangeByUser;

            // Exit
            TrailingValue = CreateParameter("TrailingValue", 1, 1.0m, 10, 1, "Exit settings");

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot on the Ssma indicator. " +
               "Trend robot at the strategy on Adaptive Look Back and Bollinger" +
               "Buy: the price is above the upper Bollinger band." +
               "Sell: the price is below the lower Bollinger band." +
               "Exit the buy: trailing stop in % of the loy of the candle on which the minus exit coefficient entered *Adaptive Look Back." +
               "Exit the sell: trailing stop in % of the high of the candle on which you entered plus the entry coefficient* Adaptive Look. ";
        }

        // Indicator Update event
        private void StrategyAdaptiveLookBackandBollinger_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)ALB.Parameters[0]).ValueInt = PeriodALB.ValueInt;
            ALB.Save();
            ALB.Reload();

            ((IndicatorParameterInt)Bollinger.Parameters[0]).ValueInt = BollingerLength.ValueInt;
            ((IndicatorParameterDecimal)Bollinger.Parameters[1]).ValueDecimal = BollingerDeviation.ValueDecimal;
            Bollinger.Save();
            Bollinger.Reload();
        }
        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyAdaptiveLookBackandBollinger";
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
            if (candles.Count < PeriodALB.ValueInt || candles.Count < CoefExitALB.ValueDecimal || 
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

            // The last value of the indicators
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
            decimal lastPrice = candles[candles.Count - 1].Close;

            // He last value of the indicator           
            _lastUpBollinger = Bollinger.DataSeries[0].Last;
            _lastDownBollinger = Bollinger.DataSeries[1].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if ( lastPrice > _lastUpBollinger)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (lastPrice < _lastDownBollinger)
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
            
            _lastALB = ALB.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }
                decimal stopPriсe;
                if (pos.Direction == Side.Buy) // If the direction of the position is buy
                {
                    decimal low = candles[candles.Count - 1].Low;
                    stopPriсe = low - low * TrailingValue.ValueDecimal / 100 - CoefExitALB.ValueDecimal * _lastALB;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPriсe = high + high * TrailingValue.ValueDecimal / 100 + CoefEntryALB.ValueDecimal * _lastALB;
                }
                _tab.CloseAtTrailingStop(pos, stopPriсe, stopPriсe);
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

