using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Robots;
using System;
using System.Collections.Generic;
using System.Drawing;

/*Discription
Trading robot for osengine.

Trend strategy based on Adaptive Look Back and ROC indicators.

Buy: 1. The candle closed above the high for the period Candles Count High + entry coefficient * Adaptive Look Back. (we set BuyAtStop).
     2. ROC is above 0.

Sale: 1. The candle closed below the lot during the period of the minimum number of candles - the entry coefficient * Adaptive look back (we install SellAtStop).
      2. ROC is below 0.

Exit: by the reverse signal of the RoC indicator.
*/


namespace OsEngine.Robots.My_bots
{
    [Bot("StrategyAdaptiveLookBackAndROC")] //We create an attribute so that we don't write anything in the Boot factory
    public class StrategyAdaptiveLookBackAndROC : BotPanel

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
        private StrategyParameterInt CandlesCountHigh;
        private StrategyParameterInt CandlesCountLow;
        private StrategyParameterInt PeriodALB;
        private StrategyParameterInt LengthROC;
        private StrategyParameterDecimal CoefExitALB;
        private StrategyParameterDecimal CoefEntryALB;
        
        // Indicator
        private Aindicator ROC;
        private Aindicator ALB;


        // The last value of the indicators      

        private decimal _lastALB;
        private decimal _lastROC;



        public StrategyAdaptiveLookBackAndROC(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthROC = CreateParameter("Rate of Change", 5, 1, 10, 1, "Indicator");
            PeriodALB = CreateParameter("Adaptive Look Back", 5, 1, 10, 1, "Indicator");
            CoefExitALB = CreateParameter("CoefExitALB", 0.2m, 0.01m, 2, 0.02m, "Indicator");
            CoefEntryALB = CreateParameter("CoefEntrytALB", 0.2m, 0.01m, 2, 0.02m, "Indicator");
            CandlesCountHigh = CreateParameter("CandlesCountHigh", 10, 50, 100, 20, "Indicator");
            CandlesCountLow = CreateParameter("CandlesCountLow", 5, 20, 100, 10, "Indicator");


            // Create indicator Adaptive Look Back
            ALB = IndicatorsFactory.CreateIndicatorByName(nameClass: "AdaptiveLookBack", name: name + "ALB", canDelete: false);
            ALB = (Aindicator)_tab.CreateCandleIndicator(ALB, nameArea: "NewArea0");
            ((IndicatorParameterInt)ALB.Parameters[0]).ValueInt = PeriodALB.ValueInt;
            ALB.Save();


            // Create indicator ROC
            ROC = IndicatorsFactory.CreateIndicatorByName("ROC", name + "Rate of Change", false);
            ROC = (Aindicator)_tab.CreateCandleIndicator(ROC, "NewArea1");
            ((IndicatorParameterInt)ROC.Parameters[0]).ValueInt = LengthROC.ValueInt;
            ROC.DataSeries[0].Color = Color.Red;
            ROC.Save();
            ParametrsChangeByUser += StrategyAdaptiveLookBackAndROC_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend strategy based on Adaptive Look Back and ROC indicators." +
                "Buy: 1. The candle closed above the high for the period Candles Count High + entry coefficient * Adaptive Look Back. (we set BuyAtStop). " +
                "2.ROC is above 0." +
                "Sale: 1. The candle closed below the lot during the period of the minimum number of candles - the entry coefficient * Adaptive look back (we install SellAtStop). " +
                " 2.ROC is below 0." +
                "Exit: by the reverse signal of the RoC indicator.";
        }
           
        // Indicator Update event
        private void StrategyAdaptiveLookBackAndROC_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)ALB.Parameters[0]).ValueInt = PeriodALB.ValueInt;
            ALB.Save();
            ALB.Reload();

            ((IndicatorParameterInt)ROC.Parameters[0]).ValueInt = LengthROC.ValueInt;
            ROC.Save();
            ROC.Reload();
        }
        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyAdaptiveLookBackAndROC";
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
            if (candles.Count < PeriodALB.ValueInt + 100 || candles.Count < CandlesCountHigh.ValueInt||
                candles.Count < LengthROC.ValueInt || candles.Count < CandlesCountLow.ValueInt)
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

            _lastROC = ROC.DataSeries[0].Last;
            _lastALB = ALB.DataSeries[0].Last;
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            _tab.BuyAtStopCancel();
            _tab.SellAtStopCancel();

            // Long
            if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
            {
                if (lastPrice > GetOupenBuy(candles, candles.Count - 1) + CoefEntryALB.ValueDecimal * _lastALB && _lastROC > 0)
                {
                    _tab.BuyAtStop(GetVolume(),
                        lastPrice + Slippage.ValueDecimal * _tab.Securiti.PriceStep,
                        lastPrice, StopActivateType.HigherOrEqual);
                }
            }

            // Short
            if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
            {
                if (lastPrice < GetOupenSell(candles, candles.Count - 1) - CoefEntryALB.ValueDecimal * _lastALB && _lastROC < 0)
                {
                    _tab.SellAtStop(GetVolume(),
                       lastPrice - Slippage.ValueDecimal * _tab.Securiti.PriceStep,
                       lastPrice, StopActivateType.LowerOrEqyal);
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            _lastROC = ROC.DataSeries[0].Last;
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is buy
                {
                    if (_lastROC < 0)

                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastROC > 0)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice + _slippage, openPositions[0].OpenVolume);
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

        private decimal GetOupenSell(List<Candle> candles, int index)
        {
            if (candles == null || index < CandlesCountLow.ValueInt)

            {
                return 0;
            }


            decimal price = decimal.MaxValue;

            for (int i = index-1; i > 0 && i > index - CandlesCountLow.ValueInt; i--)
            {
                // Looking at the maximum low
                if (candles[i].Low < price)
                {
                    price = candles[i].Low;
                }
            }

            return price;


           
        }
    
            private decimal GetOupenBuy(List<Candle> candles, int index)
            {
                if (candles == null || index < CandlesCountHigh.ValueInt)
                {
                    return 0;
                }


                decimal price = decimal.MinValue;
                for (int i = index-1; i > 0 && i > index - CandlesCountHigh.ValueInt; i--)
                {
                    // Looking at the maximum high
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;


            }
        }
    }

