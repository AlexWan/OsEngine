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

The Countertrend robot on PivotFloor And ADX.

Buy:
1. The price touched the S2 level or closed below, then returned back and closed above the level.
2. The Adx indicator is falling and below a certain level (AdxLevel).
Sell:
1. The price touched the R2 level or closed higher, then returned back and closed below the level.
2. The Adx indicator is falling and below a certain level (AdxLevel).

Exit from buy: stop – S3, profit - R1.
Exit from sell: stop – R3, profit –S1.
 */


namespace OsEngine.Robots.AO
{
    [Bot("CountertrendPivotFloorAndADX")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendPivotFloorAndADX : BotPanel
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
        private StrategyParameterString PivotFloorPeriod;
        private StrategyParameterInt PeriodADX;
        private StrategyParameterDecimal AdxLevel;

        // Indicator
        Aindicator _PivotFloor;
        Aindicator _ADX;

        // The last value of the indicator
        private decimal _lastR1;
        private decimal _lastR2;
        private decimal _lastR3;
        private decimal _lastS1;
        private decimal _lastS2;
        private decimal _lastS3;
        private decimal _lastADX;

        // The prev value of the indicator
        private decimal _prevADX;
        private decimal _prevS2;
        private decimal _prevR2;

        public CountertrendPivotFloorAndADX(string name, StartProgram startProgram) : base(name, startProgram)
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
            PivotFloorPeriod = CreateParameter("Period", "Daily", new[] { "Daily", "Weekly" }, "Indicator");
            PeriodADX = CreateParameter("ADX Length", 21, 7, 48, 7, "Indicator");
            AdxLevel = CreateParameter("ADX Level", 21.0m, 7, 48, 7, "Indicator");

            // Create indicator ChaikinOsc
            _PivotFloor = IndicatorsFactory.CreateIndicatorByName("PivotFloor", name + "PivotFloor", false);
            _PivotFloor = (Aindicator)_tab.CreateCandleIndicator(_PivotFloor, "Prime");
            ((IndicatorParameterString)_PivotFloor.Parameters[0]).ValueString = PivotFloorPeriod.ValueString;
            _PivotFloor.Save();

            // Create indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendPivotFloorAndADX_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The Countertrend robot on PivotFloor And ADX. " +
                "Buy: " +
                "1. The price touched the S2 level or closed below, then returned back and closed above the level. " +
                "2. The Adx indicator is falling and below a certain level (AdxLevel). " +
                "Sell: " +
                "1. The price touched the R2 level or closed higher, then returned back and closed below the level. " +
                "2. The Adx indicator is falling and below a certain level (AdxLevel). " +
                "Exit from buy: stop – S3, profit - R1. " +
                "Exit from sell: stop – R3, profit –S1.";
        }

        private void CountertrendPivotFloorAndADX_ParametrsChangeByUser()
        {
            ((IndicatorParameterString)_PivotFloor.Parameters[0]).ValueString = PivotFloorPeriod.ValueString;
            _PivotFloor.Save();
            _PivotFloor.Reload();
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendPivotFloorAndADX";
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

            _lastR1 = _PivotFloor.DataSeries[1].Last;

            // If there are not enough candles to build an indicator, we exit
            if (_lastR1 == 0 ||
                candles.Count < PeriodADX.ValueInt)
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
            _lastR1 = _PivotFloor.DataSeries[1].Last;
            _lastR2 = _PivotFloor.DataSeries[2].Last;
            _lastR3 = _PivotFloor.DataSeries[3].Last;
            _lastS1 = _PivotFloor.DataSeries[4].Last;
            _lastS2 = _PivotFloor.DataSeries[5].Last;
            _lastS3 = _PivotFloor.DataSeries[6].Last;
            _lastADX = _ADX.DataSeries[0].Last;

            // The orev value of the indicator
            _prevR2 = _PivotFloor.DataSeries[2].Values[_PivotFloor.DataSeries[2].Values.Count - 2];
            _prevS2 = _PivotFloor.DataSeries[5].Values[_PivotFloor.DataSeries[5].Values.Count - 2];
            _prevADX = _ADX.DataSeries[0].Values[_ADX.DataSeries[0].Values.Count - 2];

            List <Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;
                decimal prevPrice = candles[candles.Count - 2].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_prevS2 > prevPrice && _lastS2 < lastPrice && _prevADX > _lastADX && _lastADX < AdxLevel.ValueDecimal)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_prevR2 < prevPrice && _lastR2 > lastPrice && _prevADX > _lastADX && _lastADX < AdxLevel.ValueDecimal)
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
            
            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    _tab.CloseAtProfit(pos, _lastR1, _lastR1 + _slippage);
                    _tab.CloseAtStop(pos, _lastS3, _lastS3 - _slippage);
                }
                else // If the direction of the position is sale
                {
                    _tab.CloseAtProfit(pos, _lastS1, _lastS1 - _slippage);
                    _tab.CloseAtStop(pos, _lastR3, _lastR3 + _slippage);
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
