using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
/*Discription
Trading robot for osengine.

Trend strategy on Bears Power, Bulls Power and Ema.

Buy:
1. The price crosses the Ema from bottom to top.
2. Bears Power columns should be below 0, but constantly growing.
3. Bulls Power columns should be above 0 and grow.

Sell:
1. The price crosses the Ema from top to bottom.
2. Bulls Power columns should be above 0, but decrease.
3. Bears Power columns should be below 0 and decrease.

Exit from the buy: trailing stop in % of the loy of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.
*/
namespace OsEngine.Robots.myRobots
{
    // We create an attribute so that we don't write anything to the BotFactory
   
    [Bot("StrategyEmaBearsAndBullsPowers")]
    public class StrategyEmaBearsAndBullsPowers:BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;
        private StrategyParameterDecimal TrailingValue;

        // Indicator Settings
        private StrategyParameterInt _emaPeriod;
        private StrategyParameterInt BearsPeriod;
        private StrategyParameterInt BullsPeriod;

        // Indicator
        private Aindicator _ema;
        private Aindicator _bullsPower;
        private Aindicator _bearsPower;

        // The last value of the indicators
        private decimal _lastEma;        
        private decimal _lastBears;
        private decimal _lastBulls;
        private decimal _prevBears;
        private decimal _prevBulls;

        public StrategyEmaBearsAndBullsPowers(string name, StartProgram startProgram) : base(name, startProgram)
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
            _emaPeriod = CreateParameter("Moving period", 15, 50, 300, 1, "Indicator");           
            BearsPeriod = CreateParameter("Bears Period", 20, 10, 300, 10, "Indicator");
            BullsPeriod = CreateParameter("Bulls Period", 20, 10, 300, 10, "Indicator");

            // Create indicator Ema
            _ema = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "Ema", canDelete: false);
            _ema = (Aindicator)_tab.CreateCandleIndicator(_ema, nameArea: "Prime");
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _emaPeriod.ValueInt;
            _ema.Save();
            
            // Create indicator BullsPower
            _bullsPower = IndicatorsFactory.CreateIndicatorByName("BullsPower", name + "BullsPower", false);
            _bullsPower = (Aindicator)_tab.CreateCandleIndicator(_bullsPower, "NewArea0");
            ((IndicatorParameterInt)_bullsPower.Parameters[0]).ValueInt = BullsPeriod.ValueInt;
            _bullsPower.Save();

            // Create indicator BearsPower
            _bearsPower = IndicatorsFactory.CreateIndicatorByName("BearsPower", name + "BearsPower", false);
            _bearsPower = (Aindicator)_tab.CreateCandleIndicator(_bearsPower, "NewArea1");
            ((IndicatorParameterInt)_bearsPower.Parameters[0]).ValueInt = BearsPeriod.ValueInt;
            _bearsPower.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyEmaBearsAndBullsPowers_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            TrailingValue = CreateParameter("TrailingValue", 1, 1.0m, 10, 1, "Exit settings");

            Description = "Trend strategy on Bears Power, Bulls Power and Ema. " +
                "Buy: " +
                "1. The price crosses the Ema from bottom to top. " +
                "2. Bears Power columns should be below 0, but constantly growing. " +
                "3. Bulls Power columns should be above 0 and grow. " +
                "Sell: " +
                "1. The price crosses the Ema from top to bottom. " +
                "2. Bulls Power columns should be above 0, but decrease. " +
                "3. Bears Power columns should be below 0 and decrease. " +
                "Exit from the buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";
        }

        // Indicator Update event
        private void StrategyEmaBearsAndBullsPowers_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ema.Parameters[0]).ValueInt = _emaPeriod.ValueInt;           
            _ema.Save();
            _ema.Reload();

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
            return "StrategyEmaBearsAndBullsPowers";
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
            if (candles.Count < _emaPeriod.ValueInt|| candles.Count < BearsPeriod.ValueInt || candles.Count< BullsPeriod.ValueInt)
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

            if (openPositions == null || openPositions.Count == 0)
            {
                // The last value of the indicators
                _lastEma = _ema.DataSeries[0].Last;
                _lastBulls = _bullsPower.DataSeries[0].Last;
                _lastBears = _bearsPower.DataSeries[0].Last;
                _prevBulls = _bullsPower.DataSeries[0].Values[_bullsPower.DataSeries[0].Values.Count - 2];
                _prevBears = _bearsPower.DataSeries[0].Values[_bearsPower.DataSeries[0].Values.Count - 2];

                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                decimal lastPrice = candles[candles.Count - 1].Close;
                
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEma< lastPrice && _lastBears < 0 && _lastBears > _prevBears && _lastBulls > 0 && _lastBulls > _prevBulls)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastEma > lastPrice && _lastBulls > 0 && _lastBulls < _prevBulls && _lastBears < 0 && _lastBears < _prevBears)
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
                    stopPriсe = low - low * TrailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPriсe = high + high * TrailingValue.ValueDecimal / 100;
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
