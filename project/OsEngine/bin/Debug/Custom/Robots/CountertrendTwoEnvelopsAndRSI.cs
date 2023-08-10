using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Trading robot for osengine.

The trend robot on countertrend two Envelopes and RSI.

Buy:
1. The price was in the lower zone between between the lower lines of the local Envelopes and the global or below the global. 
Then it returned back and became higher than the lower line of the local Envelopes;
2. Rsi is below a certain value, oversold zone (Oversold Line).

Sell: 
1. The price was in the upper zone between between the milestones of the local Envelopes and the global or above the global. 
Then it came back and became below the upper line of the local Envelopes;
2. The Rsi is above a certain value, the overbought zone (Overbought Line).

Exit: 
On the opposite signal.
 */

namespace OsEngine.Robots.My_bots
{
    [Bot("CountertrendTwoEnvelopsAndRSI")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendTwoEnvelopsAndRSI : BotPanel
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
        private StrategyParameterInt EnvelopsLengthLoc;
        private StrategyParameterDecimal EnvelopsDeviationLoc;
        private StrategyParameterInt EnvelopsLengthGlob;
        private StrategyParameterDecimal EnvelopsDeviationGlob;
        private StrategyParameterInt PeriodRsi;
        private StrategyParameterInt OversoldLine;
        private StrategyParameterInt OverboughtLine;

        // Indicator
        Aindicator _EnvelopsLoc;
        Aindicator _EnvelopsGlob;
        Aindicator _RSI;

        // The last value of the indicator
        private decimal _lastUpLineLoc;
        private decimal _lastDownLineLoc;
        private decimal _lastUpLineGlob;
        private decimal _lastDownLineGlob;
        private decimal _lastRSI;

        // The prev value of the indicator
        private decimal _prevUpLineLoc;
        private decimal _prevDownLineLoc;
        private decimal _prevUpLineGlob;
        private decimal _prevDownLineGlob;

        public CountertrendTwoEnvelopsAndRSI(string name, StartProgram startProgram) : base(name, startProgram)
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
            EnvelopsLengthLoc = CreateParameter("Envelop Length Loc", 21, 7, 48, 7, "Indicator");
            EnvelopsDeviationLoc = CreateParameter("Envelop Deviation Loc", 1.0m, 1, 5, 0.1m, "Indicator");
            EnvelopsLengthGlob = CreateParameter("Envelop Length Glob", 21, 7, 48, 7, "Indicator");
            EnvelopsDeviationGlob = CreateParameter("Envelop Deviation Glob", 1.0m, 1, 5, 0.1m, "Indicator");
            PeriodRsi = CreateParameter("RSI Period", 14, 7, 48, 7, "Indicator");
            OversoldLine = CreateParameter("Oversold Line", 20, 10,100, 10, "Indicator");
            OverboughtLine = CreateParameter("Overbought Line", 20, 10, 200, 10, "Indicator");
           
            // Create indicator RSI
            _RSI = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _RSI = (Aindicator)_tab.CreateCandleIndicator(_RSI, "NewArea");
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = PeriodRsi.ValueInt;
            _RSI.Save();

            // Create indicator EnvelopsLoc
            _EnvelopsLoc = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "_EnvelopsLoc", false);
            _EnvelopsLoc = (Aindicator)_tab.CreateCandleIndicator(_EnvelopsLoc, "Prime");
            ((IndicatorParameterInt)_EnvelopsLoc.Parameters[0]).ValueInt = EnvelopsLengthLoc.ValueInt;
            ((IndicatorParameterDecimal)_EnvelopsLoc.Parameters[1]).ValueDecimal = EnvelopsDeviationLoc.ValueDecimal;
            _EnvelopsLoc.Save();

            //  Create indicator EnvelopsGlob
            _EnvelopsGlob = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "_EnvelopsGlob", false);
            _EnvelopsGlob = (Aindicator)_tab.CreateCandleIndicator(_EnvelopsGlob, "Prime");
            ((IndicatorParameterInt)_EnvelopsGlob.Parameters[0]).ValueInt = EnvelopsLengthGlob.ValueInt;
            ((IndicatorParameterDecimal)_EnvelopsGlob.Parameters[1]).ValueDecimal = EnvelopsDeviationGlob.ValueDecimal;
            _EnvelopsGlob.DataSeries[0].Color = Color.Aquamarine;
            _EnvelopsGlob.DataSeries[2].Color = Color.Aquamarine;
            _EnvelopsGlob.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendTwoEnvelopsAndRSI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on countertrend two Envelopes and RSI." +
                "Buy:" +
                "  1.The price was in the lower zone between between the lower lines of the local Envelopes and the global or below the glob" +
                " Then it returned back and became higher than the lower line of the local Envelope" +
                " 2.Rsi is below a certain value, oversold zone(Oversold Line)." +
                "Sell: " +
                "1.The price was in the upper zone between between the milestones of the local Envelopes and the global or above the global. " +
                "Then it came back and became below the upper line of the local Envelopes;" +
                " 2.The Rsi is above a certain value, the overbought zone(Overbought Line)." +
                "Exit:" +
                " On the opposite signal.";
        }

        private void CountertrendTwoEnvelopsAndRSI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_EnvelopsLoc.Parameters[0]).ValueInt = EnvelopsLengthLoc.ValueInt;
            ((IndicatorParameterDecimal)_EnvelopsLoc.Parameters[1]).ValueDecimal = EnvelopsDeviationLoc.ValueDecimal;
            _EnvelopsLoc.Save();
            _EnvelopsLoc.Reload();

            ((IndicatorParameterInt)_EnvelopsGlob.Parameters[0]).ValueInt = EnvelopsLengthGlob.ValueInt;
            ((IndicatorParameterDecimal)_EnvelopsGlob.Parameters[1]).ValueDecimal = EnvelopsDeviationGlob.ValueDecimal;
            _EnvelopsGlob.Save();
            _EnvelopsGlob.Reload();

            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = PeriodRsi.ValueInt;
            _RSI.Save();
            _RSI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendTwoEnvelopsAndRSI";
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
            if (candles.Count < EnvelopsLengthLoc.ValueInt || candles.Count < EnvelopsDeviationLoc.ValueDecimal ||
                candles.Count < EnvelopsLengthGlob.ValueInt || candles.Count < EnvelopsDeviationGlob.ValueDecimal)
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
            _lastUpLineLoc = _EnvelopsLoc.DataSeries[0].Last;
            _lastDownLineLoc = _EnvelopsLoc.DataSeries[2].Last;
            _lastUpLineGlob = _EnvelopsGlob.DataSeries[0].Last;
            _lastDownLineGlob = _EnvelopsGlob.DataSeries[2].Last;
            _lastRSI = _RSI.DataSeries[0].Last;

            // The prev value of the indicator
            _prevUpLineLoc = _EnvelopsLoc.DataSeries[0].Values[_EnvelopsLoc.DataSeries[0].Values.Count - 2];
            _prevDownLineLoc = _EnvelopsLoc.DataSeries[2].Values[_EnvelopsLoc.DataSeries[2].Values.Count - 2];
            _prevUpLineGlob = _EnvelopsGlob.DataSeries[0].Values[_EnvelopsGlob.DataSeries[0].Values.Count - 2];
            _prevDownLineGlob = _EnvelopsGlob.DataSeries[2].Values[_EnvelopsGlob.DataSeries[2].Values.Count - 2];
            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;
                decimal prevPrice = candles[candles.Count - 2].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (prevPrice < _prevDownLineLoc && prevPrice > _prevDownLineGlob && 
                        lastPrice > _lastDownLineLoc && _lastRSI < OversoldLine.ValueInt
                        || prevPrice < _prevDownLineGlob && lastPrice > _lastDownLineLoc
                        && _lastRSI < OversoldLine.ValueInt)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (prevPrice > _prevUpLineLoc && prevPrice < _prevUpLineGlob && lastPrice < _lastUpLineLoc
                        && _lastRSI > OverboughtLine.ValueInt || prevPrice > _prevUpLineGlob && lastPrice < _lastUpLineLoc
                        && _lastRSI > OverboughtLine.ValueInt)
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
            Position pos = openPositions[0];

            _lastUpLineLoc = _EnvelopsLoc.DataSeries[0].Last;
            _lastDownLineLoc = _EnvelopsLoc.DataSeries[1].Last;
            _lastUpLineGlob = _EnvelopsGlob.DataSeries[0].Last;
            _lastDownLineGlob = _EnvelopsGlob.DataSeries[1].Last;
            _lastRSI = _RSI.DataSeries[0].Last;

            // The prev value of the indicator
            _prevUpLineLoc = _EnvelopsLoc.DataSeries[0].Values[_EnvelopsLoc.DataSeries[0].Values.Count - 2];
            _prevDownLineLoc = _EnvelopsLoc.DataSeries[1].Values[_EnvelopsLoc.DataSeries[1].Values.Count - 2];
            _prevUpLineGlob = _EnvelopsGlob.DataSeries[0].Values[_EnvelopsGlob.DataSeries[0].Values.Count - 2];
            _prevDownLineGlob = _EnvelopsGlob.DataSeries[1].Values[_EnvelopsGlob.DataSeries[1].Values.Count - 2];

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];
                decimal lastPrice = candles[candles.Count - 1].Close;
                decimal prevPrice = candles[candles.Count - 2].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (prevPrice > _prevUpLineLoc && prevPrice < _prevUpLineGlob && lastPrice < _lastUpLineLoc
                        && _lastRSI > OverboughtLine.ValueInt || prevPrice > _prevUpLineGlob && lastPrice < _lastUpLineLoc
                        && _lastRSI > OverboughtLine.ValueInt)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (prevPrice < _prevDownLineLoc && prevPrice > _prevDownLineGlob && lastPrice > _lastDownLineLoc && _lastRSI < OversoldLine.ValueInt
                        || prevPrice < _prevDownLineGlob && lastPrice > _lastDownLineLoc && _lastRSI < OversoldLine.ValueInt)
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