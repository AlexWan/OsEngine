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
Trading robot for osengine

Trend robot at the intersection of two Vwma (all cars and different outputs) and Adaptive Look Back.

Buy: fast Ema is higher than slow Vwma and price is higher than fast Vwma + entry coefficient * Adaptive Look Back.

Sell: fast Ema is lower than average Vvma and price is lower than fast Ma - entry coefficient * Adaptive Look back .

Exit: reverse intersection of Wma.
*/

namespace OsEngine.Robots.myRobots
{
    [Bot("IntersectionOfTwoVwmaAndAdaptiveLookBack")]
    public class IntersectionOfTwoVwmaAndAdaptiveLookBack:BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;


        // Indicator Settings
        private StrategyParameterInt PeriodVwmaFast;
        private StrategyParameterInt PeriodVwmaSlow;
        private StrategyParameterInt PeriodALB;
        private StrategyParameterDecimal CoefEntryALB;

        // Indicator
        private Aindicator ALB;
        private Aindicator Vwma1;
        private Aindicator Vwma2;

        // The last value of the indicators      
        private decimal _lastALB;
        private decimal _lastVwmaFasts;
        private decimal _lastVwmaSlow;
   


    public IntersectionOfTwoVwmaAndAdaptiveLookBack(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodALB = CreateParameter("Adaptive Look Back", 5, 1, 10, 1 , "Indicator");
            CoefEntryALB = CreateParameter("CoefEntryALB", 0.2m, 0.01m, 2, 0.02m, "Indicator");
            PeriodVwmaFast = CreateParameter("Fast Vwma1 period", 250, 50, 500, 20, "Indicator");
            PeriodVwmaSlow = CreateParameter("Slow Vwma2 period", 1000, 500, 1500, 100, "Indicator");

            // Create indicator Adaptive Look Back
            ALB = IndicatorsFactory.CreateIndicatorByName(nameClass: "AdaptiveLookBack", name: name + "ALB", canDelete: false);
            ALB = (Aindicator)_tab.CreateCandleIndicator(ALB, nameArea: "NewArea");
        ((IndicatorParameterInt)ALB.Parameters[0]).ValueInt = PeriodALB.ValueInt;
            ALB.Save();

            // Create indicator Vwma1
            Vwma1 = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "Vwma1", false);
            Vwma1 = (Aindicator)_tab.CreateCandleIndicator(Vwma1, "Prime");
        ((IndicatorParameterInt)Vwma1.Parameters[0]).ValueInt = PeriodVwmaFast.ValueInt;
            Vwma1.DataSeries[0].Color = Color.Red;
            Vwma1.Save();
           
            // Create indicator Vwma2
            Vwma2 = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "Vwma2", false);
            Vwma2 = (Aindicator)_tab.CreateCandleIndicator(Vwma2, "Prime");
        ((IndicatorParameterInt)Vwma2.Parameters[0]).ValueInt = PeriodVwmaSlow.ValueInt;
            Vwma2.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfTwoVwmaAndAdaptiveLookBack_ParametrsChangeByUser;

        // Subscribe to the candle finished event
        _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot at the intersection of two Vwma (all cars and different outputs) and Adaptive Look Back." +
               "Buy: fast Ema is higher than slow Vwma and price is higher than fast Vwma +entry coefficient* Adaptive Look Back." +
               "Sell: fast Ema is lower than average Vvma and price is lower than fast Ma -entry coefficient* Adaptive Look back ." +
               "Exit: reverse intersection of Wma.";
        }

    // Indicator Update event
    private void IntersectionOfTwoVwmaAndAdaptiveLookBack_ParametrsChangeByUser()
    {
            ((IndicatorParameterInt)ALB.Parameters[0]).ValueInt = PeriodALB.ValueInt;
            ALB.Save();
            ALB.Reload();

        ((IndicatorParameterInt)Vwma1.Parameters[0]).ValueInt = PeriodVwmaFast.ValueInt;
            Vwma1.Save();
            Vwma1.Reload();

        ((IndicatorParameterInt)Vwma2.Parameters[0]).ValueInt = PeriodVwmaSlow.ValueInt;
            Vwma2.Save();
            Vwma2.Reload();
    }

    // The name of the robot in OsEngine
    public override string GetNameStrategyType()
    {
        return "IntersectionOfTwoVwmaAndAdaptiveLookBack";
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
        if (candles.Count < PeriodALB.ValueInt || candles.Count < CoefEntryALB.ValueDecimal || candles.Count < PeriodVwmaFast.ValueInt || candles.Count < PeriodVwmaSlow.ValueInt)
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
            _lastALB = ALB.DataSeries[0].Last;
            _lastVwmaFasts = Vwma1.DataSeries[0].Last;
            _lastVwmaSlow = Vwma2.DataSeries[0].Last;
            
        if (openPositions == null || openPositions.Count == 0)
        {
            // Long
            if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
            {
                if (_lastVwmaFasts > _lastVwmaSlow && lastPrice > _lastVwmaFasts + CoefEntryALB.ValueDecimal * _lastALB)
                {
                    _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                }
            }

            // Short
            if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
            {

                if (_lastVwmaFasts < _lastVwmaSlow && lastPrice < _lastVwmaFasts - CoefEntryALB.ValueDecimal * _lastALB)
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

        decimal lastPrice = candles[candles.Count - 1].Close;
        decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // He last value of the indicator
            _lastVwmaFasts = Vwma1.DataSeries[0].Last;
            _lastVwmaSlow = Vwma2.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
        {
            if (openPositions[i].State != PositionStateType.Open)
            {
                continue;
            }

            if (openPositions[i].Direction == Side.Buy) // if the direction of the position is buy
            {
                if (_lastVwmaFasts < _lastVwmaSlow)
                {
                    _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                }
            }
            else // If the direction of the position is sale
            {
                if (_lastVwmaFasts > _lastVwmaSlow)
                {
                    _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
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

