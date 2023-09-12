using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
/*Discription
Trading robot for osengine.

Trend strategy on Bears Power, Bulls Power and Parabolic SAR.

Buy:
1. The price is higher than the Parabolic value. For the next candle, the price crosses the indicator from the bottom up.
2. Bears Power columns must be higher than 0.
3. Bulls Power columns must be above 0.

Sell:
1. The price is lower than the Parabolic value. For the next candle, the price crosses the indicator from top to bottom.
2. Bulls Power columns must be below 0.
3. Bears Power columns must be below 0.

Exit:
On the opposite signal of the parabolic.
*/
namespace OsEngine.Robots.myRobots
{  
    // We create an attribute so that we don't write anything to the BotFactory
    [Bot("StrategyParabolicBearsAndBullsPowers")]
   
    public class StrategyParabolicBearsAndBullsPowers : BotPanel
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
        private StrategyParameterDecimal Step;
        private StrategyParameterDecimal MaxStep;
        private StrategyParameterInt BearsPeriod;
        private StrategyParameterInt BullsPeriod;

        // Indicator
        private Aindicator _PS;
        private Aindicator _bullsPower;
        private Aindicator _bearsPower;

        // The last value of the indicators      
        private decimal _lastSar;
        private decimal _lastBears;
        private decimal _lastBulls;
       

        public StrategyParabolicBearsAndBullsPowers(string name, StartProgram startProgram) : base(name, startProgram)
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
            Step = CreateParameter("Step", 0.02m, 0.001m, 3, 0.001m, "Indicator");
            MaxStep = CreateParameter("MaxStep", 0.2m, 0.01m, 1, 0.01m, "Indicator"); 
            BearsPeriod = CreateParameter("Bears Period", 20, 10, 300, 10, "Indicator");
            BullsPeriod = CreateParameter("Bulls Period", 20, 10, 300, 10, "Indicator");

            // Create indicator Ema
            _PS = IndicatorsFactory.CreateIndicatorByName(nameClass: "ParabolicSAR", name: name + "Parabolic", canDelete: false);
            _PS = (Aindicator)_tab.CreateCandleIndicator(_PS, nameArea: "Prime");
            ((IndicatorParameterDecimal)_PS.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_PS.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _PS.Save();


            // Create indicator BullsPower
            _bullsPower = IndicatorsFactory.CreateIndicatorByName("BullsPower", name + "BullsPower", false);
            _bullsPower = (Aindicator)_tab.CreateCandleIndicator(_bullsPower, "NewArea0");
            ((IndicatorParameterInt)_bullsPower.Parameters[0]).ValueInt = BullsPeriod.ValueInt;

            // Create indicator BearsPower
            _bearsPower = IndicatorsFactory.CreateIndicatorByName("BearsPower", name + "BearsPower", false);
            _bearsPower = (Aindicator)_tab.CreateCandleIndicator(_bearsPower, "NewArea1");
            ((IndicatorParameterInt)_bearsPower.Parameters[0]).ValueInt = BearsPeriod.ValueInt;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyParabolicBearsAndBullsPowers_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend strategy on Bears Power, Bulls Power and Parabolic SAR. " +
                "Buy: " +
                "1. The price is higher than the Parabolic value. For the next candle, the price crosses the indicator from the bottom up. " +
                "2. Bears Power columns must be higher than 0. " +
                "3. Bulls Power columns must be above 0. " +
                "Sell: " +
                "1. The price is lower than the Parabolic value. For the next candle, the price crosses the indicator from top to bottom. " +
                "2. Bulls Power columns must be below 0. " +
                "3. Bears Power columns must be below 0. " +
                "Exit: " +
                "On the opposite signal of the parabolic.";
        }

        // Indicator Update event
        private void StrategyParabolicBearsAndBullsPowers_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_PS.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_PS.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _PS.Save();
            _PS.Reload();
           
            ((IndicatorParameterInt)_bearsPower.Parameters[0]).ValueInt = BearsPeriod.ValueInt;
            _bearsPower.Save();
            _bearsPower.Reload();

            ((IndicatorParameterInt)_bullsPower.Parameters[0]).ValueInt = BullsPeriod.ValueInt;
            _bullsPower.Save();
            _bullsPower.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyParabolicBearsAndBullsPowers";
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
            if (candles.Count < Step.ValueDecimal|| candles.Count < MaxStep.ValueDecimal||candles.Count < BearsPeriod.ValueInt||candles.Count < BullsPeriod.ValueInt)
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
            _lastSar = _PS.DataSeries[0].Last;
            _lastBulls = _bullsPower.DataSeries[0].Last;
            _lastBears = _bearsPower.DataSeries[0].Last;
            if (openPositions == null || openPositions.Count == 0)
            {                               
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastSar < lastPrice && _lastBears > 0 && _lastBulls > 0 )
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastSar > lastPrice && _lastBulls < 0  && _lastBears < 0 )
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
            _lastSar = _PS.DataSeries[0].Last;
           
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }
               
                if (openPositions[i].Direction == Side.Buy) // if the direction of the position is buy
                {
                    if (_lastSar > lastPrice)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastSar < lastPrice)
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
    }
}
