using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine.

Trend strategy on Bears Power and Bulls Power.

Buy:
1. Bears Power columns must be higher than 0.
2. Bulls Power columns must be above 0.
3. The sum of the last values of both indicators should be greater than a certain value.

Sell:
1. Bulls Power columns must be below 0.
2. Bears Power columns must be below 0.
3. The sum of the last values of both indicators should be less than a certain value with a minus sign.

Exit:
On the opposite signal.
*/

namespace OsEngine.Robots.My_bots
{
    [Bot("BullsPowerAndBearsPowerTrend")]
    public class BullsPowerAndBearsPowerTrend : BotPanel
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

        private StrategyParameterInt BearsPeriod;
        private StrategyParameterInt BullsPeriod;
        private StrategyParameterDecimal Step;

        // Indicator

        private Aindicator _bullsPower;
        private Aindicator _bearsPower;

        // The last value of the indicators      

        
        private decimal _lastBearsPrice;
        private decimal _lastBullsPrice;


        public BullsPowerAndBearsPowerTrend(string name, StartProgram startProgram) : base(name, startProgram)
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
            Step = CreateParameter("Step", 100, 50m, 500, 20, "Indicator");
            BearsPeriod = CreateParameter("Bears Period", 20, 10, 300, 10, "Indicator");
            BullsPeriod = CreateParameter("Bulls Period", 20, 10, 300, 10, "Indicator");


            // Create indicator BullsPower
            _bullsPower = IndicatorsFactory.CreateIndicatorByName("BullsPower", name + "BullsPower", false);
            _bullsPower = (Aindicator)_tab.CreateCandleIndicator(_bullsPower, "NewArea0");
            ((IndicatorParameterInt)_bullsPower.Parameters[0]).ValueInt = BullsPeriod.ValueInt;

            // Create indicator BearsPower
            _bearsPower = IndicatorsFactory.CreateIndicatorByName("BearsPower", name + "BearsPower", false);
            _bearsPower = (Aindicator)_tab.CreateCandleIndicator(_bearsPower, "NewArea1");
            ((IndicatorParameterInt)_bearsPower.Parameters[0]).ValueInt = BearsPeriod.ValueInt;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BullsPowerAndBearsPowerTrend_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend strategy on Bears Power and Bulls Power." +
                "Buy:" +
                " 1.Bears Power columns must be higher than 0." +
                "2.Bulls Power columns must be above 0." +
                "3.The sum of the last values of both indicators should be greater than a certain value." +
                "Sell:" +
                "1.Bulls Power columns must be below 0." +
                "2.Bears Power columns must be below 0." +
                "3.The sum of the last values of both indicators should be less than a certain value with a minus sign." +
                "Exit:" +
                "On the opposite signal.";
        }

        // Indicator Update event
        private void BullsPowerAndBearsPowerTrend_ParametrsChangeByUser()
        {


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
            return "BullsPowerAndBearsPowerTrend";
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
            if (candles.Count < BearsPeriod.ValueInt || candles.Count < BullsPeriod.ValueInt)
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

            _lastBearsPrice = _bearsPower.DataSeries[0].Values[_bearsPower.DataSeries[0].Values.Count - 1];
            _lastBullsPrice = _bullsPower.DataSeries[0].Values[_bullsPower.DataSeries[0].Values.Count - 1];
           
            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastBearsPrice > 0 && _lastBullsPrice > 0 && _lastBullsPrice + _lastBearsPrice > Step.ValueDecimal)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong")
                {
                    if (_lastBearsPrice < 0 && _lastBullsPrice < 0 && _lastBullsPrice + _lastBearsPrice < -Step.ValueDecimal)

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
            _lastBearsPrice = _bearsPower.DataSeries[0].Values[_bearsPower.DataSeries[0].Values.Count - 1];
            _lastBullsPrice = _bullsPower.DataSeries[0].Values[_bullsPower.DataSeries[0].Values.Count - 1];
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is buy
                {
                    if (_lastBearsPrice < 0 && _lastBullsPrice < 0 && _lastBullsPrice + _lastBearsPrice < -Step.ValueDecimal)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastBearsPrice > 0 && _lastBullsPrice > 0 && _lastBullsPrice + _lastBearsPrice > Step.ValueDecimal)
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

