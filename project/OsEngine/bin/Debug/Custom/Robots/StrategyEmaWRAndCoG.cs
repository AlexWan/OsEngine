using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/*Discription
Trading robot for osengine.

Contrtrend robot on WilliamsRange, Ema and CoG.

Buy:
When the price is below the Ema indicator, the WilliamsRange indicator leaves the overbought zone, 
crossing the -20 mark from bottom to top, and the main line of the CoG indicator is above the signal line.

Sell:
When the price is above the Ema indicator, the WilliamsRange indicator leaves the oversold zone, crossing the -80 mark from top to bottom and the main line of the CoG indicator is below the signal line.

Exit: 
From purchases, the candle closed above the Ema indicator.
From sales, the candle closed below the Ema indicator.

*/

namespace OsEngine.Robots.MyBots
{
    [Bot("StrategyEmaWRAndCoG")] // We create an attribute so that we don't write anything to the BotFactory
    internal class StrategyEmaWRAndCoG : BotPanel
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
        private StrategyParameterInt LengthCog;
        private StrategyParameterInt PeriodWilliams;
        private StrategyParameterInt LengthEma;

        // Indicator
        Aindicator _Cog;
        Aindicator _Williams;
        Aindicator _Ema;
        public StrategyEmaWRAndCoG(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthCog = CreateParameter("CoG Length", 5, 5, 50, 1, "Indicator");
            PeriodWilliams = CreateParameter("Williams Length", 14, 50, 300, 1, "Indicator");
            LengthEma = CreateParameter("Ema Length", 15, 50, 300, 1, "Indicator");

            // Create indicator CoG
            _Cog = IndicatorsFactory.CreateIndicatorByName("COG_CentreOfGravity_Oscr", name + "CoG", false);
            _Cog = (Aindicator)_tab.CreateCandleIndicator(_Cog, "CogArea");
            ((IndicatorParameterInt)_Cog.Parameters[0]).ValueInt = LengthCog.ValueInt;
            _Cog.Save();

            // Creating an indicator WilliamsRange
            _Williams = IndicatorsFactory.CreateIndicatorByName("WilliamsRange", name + "WilliamsRange", false);
            _Williams = (Aindicator)_tab.CreateCandleIndicator(_Williams, "WRArea");
            ((IndicatorParameterInt)_Williams.Parameters[0]).ValueInt = PeriodWilliams.ValueInt;
            _Williams.Save();

            // Creating an indicator Ssma
            _Ema = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema", false);
            _Ema = (Aindicator)_tab.CreateCandleIndicator(_Ema, nameArea: "Prime");
            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = LengthEma.ValueInt;
            _Ema.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyWRAndCoG_ParametrsChangeByUser;

            // subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Contrtrend robot on WilliamsRange, Ema and CoG. " +
               "Buy:" +
               " When the price is below the Ema indicator, the WilliamsRange indicator leaves the overbought zone, " +
               "crossing the -20 mark from bottom to top, and the main line of the CoG indicator is above the signal line." +
               "Sell: " +
               "When the price is above the Ema indicator, the WilliamsRange indicator leaves the oversold zone, " +
               "crossing the -80 mark from top to bottom and the main line of the CoG indicator is below the signal line." +
               "Exit: " +
               "From purchases, the candle closed above the Ema indicator." +
               "From sales, the candle closed below the Ema indicator.";
        }

        private void StrategyWRAndCoG_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Cog.Parameters[0]).ValueInt = LengthCog.ValueInt;
            _Cog.Save();
            _Cog.Reload();

            ((IndicatorParameterInt)_Williams.Parameters[0]).ValueInt = PeriodWilliams.ValueInt;
            _Williams.Save();
            _Williams.Reload();

            ((IndicatorParameterInt)_Ema.Parameters[0]).ValueInt = LengthEma.ValueInt;
            _Ema.Save();
            _Ema.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyEmaWRAndCoG";
        }

        public override void ShowIndividualSettingsDialog()
        { 
        }

        // Logic
        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < LengthEma.ValueInt || candles.Count < LengthCog.ValueInt ||
                candles.Count < PeriodWilliams.ValueInt)
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
            decimal lastCog = _Cog.DataSeries[0].Last;
            decimal lastCogSignal = _Cog.DataSeries[1].Last;
            decimal lastEma = _Ema.DataSeries[0].Last;
            decimal lastWilliams = _Williams.DataSeries[0].Last;
            decimal lastPrice = candles[candles.Count - 1].Close;

            // The prev value of the indicator
            decimal prevWilliams = _Williams.DataSeries[0].Values[_Williams.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastCog > lastCogSignal && lastPrice < lastEma
                       && prevWilliams > -20 && lastWilliams < -20)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastCog < lastCogSignal && lastPrice > lastEma
                        && prevWilliams < -80 && lastWilliams > -80)
                   
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
           
            decimal lastEma = _Ema.DataSeries[0].Last;
            decimal lastPrice = candles[candles.Count - 1].Close;
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
                    if (lastPrice > lastEma)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice < lastEma)
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
