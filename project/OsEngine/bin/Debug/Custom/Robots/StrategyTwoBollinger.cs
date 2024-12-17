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

The trend robot on Two Bollinger.

Buy:
1. The price is in the lower zone between the two lower Bollinger lines.
2. The price has become higher than the lower line of the local bollinger (with a smaller deviation).
3. The last two candles are growing.

Sell:
1. The price is in the upper zone between the two upper lines of the bolter.
2. The price has become below the upper line of the local bollinger (with a smaller deviation).
3. The last two candles are falling.

Exit: the other side of the local bollinger.

 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyTwoBollinger")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyTwoBollinger : BotPanel
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
        private StrategyParameterInt BollingerLengthGlob;
        private StrategyParameterDecimal BollingerDeviationGlob;
        private StrategyParameterInt BollingerLengthLoc;
        private StrategyParameterDecimal BollingerDeviationLoc;

        // Indicator
        Aindicator _BollingerGlob;
        Aindicator _BollingerLoc;

        // The last value of the indicator
        private decimal _lastUpLineLoc;
        private decimal _lastDownLineLoc;

        // The prev value of the indicator
        private decimal _prevUpLineGlob;
        private decimal _prevDownLineGlob;
        private decimal _prevUpLineLoc;
        private decimal _prevDownLineLoc;

        public StrategyTwoBollinger(string name, StartProgram startProgram) : base(name, startProgram)
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
            BollingerLengthGlob = CreateParameter("Bollinger Length Glob", 21, 7, 48, 7, "Indicator");
            BollingerDeviationGlob = CreateParameter("Bollinger Deviation Glob", 1.0m, 1, 5, 0.1m, "Indicator");
            BollingerLengthLoc = CreateParameter("Bollinger Length Loc", 21, 7, 48, 7, "Indicator");
            BollingerDeviationLoc = CreateParameter("Bollinger Deviation Loc", 1.0m, 1, 5, 0.1m, "Indicator");

            // Create indicator Bollinger Glob
            _BollingerGlob = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "BollingerGlob", false);
            _BollingerGlob = (Aindicator)_tab.CreateCandleIndicator(_BollingerGlob, "Prime");
            ((IndicatorParameterInt)_BollingerGlob.Parameters[0]).ValueInt = BollingerLengthGlob.ValueInt;
            ((IndicatorParameterDecimal)_BollingerGlob.Parameters[1]).ValueDecimal = BollingerDeviationGlob.ValueDecimal;
            _BollingerGlob.DataSeries[0].Color = Color.Yellow;
            _BollingerGlob.DataSeries[1].Color = Color.Yellow;
            _BollingerGlob.DataSeries[2].Color = Color.Yellow;
            _BollingerGlob.Save();

            // Create indicator Bollinger Loc
            _BollingerLoc = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "BollingerLoc", false);
            _BollingerLoc = (Aindicator)_tab.CreateCandleIndicator(_BollingerLoc, "Prime");
            ((IndicatorParameterInt)_BollingerLoc.Parameters[0]).ValueInt = BollingerLengthLoc.ValueInt;
            ((IndicatorParameterDecimal)_BollingerLoc.Parameters[1]).ValueDecimal = BollingerDeviationLoc.ValueDecimal;
            _BollingerLoc.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyTwoBollinger_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Two Bollinger. " +
                "Buy: " +
                "1. The price is in the lower zone between the two lower Bollinger lines. " +
                "2. The price has become higher than the lower line of the local bollinger (with a smaller deviation). " +
                "3. The last two candles are growing. " +
                "Sell: " +
                "1. The price is in the upper zone between the two upper lines of the bolter. " +
                "2. The price has become below the upper line of the local bollinger (with a smaller deviation). " +
                "3. The last two candles are falling. " +
                "Exit: the other side of the local bollinger.";
        }

        private void StrategyTwoBollinger_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_BollingerGlob.Parameters[0]).ValueInt = BollingerLengthGlob.ValueInt;
            ((IndicatorParameterDecimal)_BollingerGlob.Parameters[1]).ValueDecimal = BollingerDeviationGlob.ValueDecimal;
            _BollingerGlob.Save();
            _BollingerGlob.Reload();
            ((IndicatorParameterInt)_BollingerLoc.Parameters[0]).ValueInt = BollingerLengthLoc.ValueInt;
            ((IndicatorParameterDecimal)_BollingerLoc.Parameters[1]).ValueDecimal = BollingerDeviationLoc.ValueDecimal;
            _BollingerLoc.Save();
            _BollingerLoc.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyTwoBollinger";
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
            if (candles.Count < BollingerLengthGlob.ValueInt ||
                candles.Count < BollingerLengthLoc.ValueInt)
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
            _lastUpLineLoc = _BollingerLoc.DataSeries[0].Last;
            _lastDownLineLoc = _BollingerLoc.DataSeries[1].Last;

            // The prev value of the indicator
            _prevUpLineGlob = _BollingerGlob.DataSeries[0].Values[_BollingerGlob.DataSeries[0].Values.Count - 2];
            _prevDownLineGlob = _BollingerGlob.DataSeries[1].Values[_BollingerGlob.DataSeries[1].Values.Count - 2];
            _prevUpLineLoc = _BollingerLoc.DataSeries[0].Values[_BollingerLoc.DataSeries[0].Values.Count - 2];
            _prevDownLineLoc = _BollingerLoc.DataSeries[1].Values[_BollingerLoc.DataSeries[1].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;
                decimal prevPriceGlob = candles[candles.Count - 2].High;
                decimal prevPriceLoc = candles[candles.Count - 2].Low;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_prevDownLineLoc > prevPriceLoc &&
                        _prevDownLineGlob < prevPriceLoc &&
                        _lastDownLineLoc < lastPrice && 
                        candles[candles.Count - 1].IsUp && 
                        candles[candles.Count - 2].IsUp)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_prevUpLineLoc < prevPriceGlob &&
                        _prevUpLineGlob > prevPriceGlob &&
                        _lastUpLineLoc > lastPrice &&
                        candles[candles.Count - 1].IsDown &&
                        candles[candles.Count - 2].IsDown)
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
            
            // The prev value of the indicator
            _prevUpLineGlob = _BollingerGlob.DataSeries[0].Values[_BollingerGlob.DataSeries[0].Values.Count - 2];
            _prevDownLineGlob = _BollingerGlob.DataSeries[1].Values[_BollingerGlob.DataSeries[1].Values.Count - 2];
            _prevUpLineLoc = _BollingerLoc.DataSeries[0].Values[_BollingerLoc.DataSeries[0].Values.Count - 2];
            _prevDownLineLoc = _BollingerLoc.DataSeries[1].Values[_BollingerLoc.DataSeries[1].Values.Count - 2];

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal prevPriceGlob = candles[candles.Count - 2].High;

            decimal prevPriceLoc = candles[candles.Count - 2].Low;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_prevUpLineLoc < prevPriceGlob &&
                        _prevUpLineGlob > prevPriceGlob)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_prevDownLineLoc > prevPriceLoc &&
                        _prevDownLineGlob < prevPriceLoc)
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
