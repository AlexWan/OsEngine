using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

Counter-trend robot on the EOM Watcher and ADX indicators.

Buy: When the ADX +DM indicator is above -DM, the previous value of the EOM Watcher indicator 
was below the lower standard deviation line, and the current value is above the lower line.

Sell: When the ADX +DM indicator is below -DM, the previous value of the EOM Watcher indicator 
was above the upper standard deviation line, and the current value is below the upper line.

Exit from buy: When the previous value of the EOM Watcher indicator was above the upper line of the standard deviation, 
and the current value is below the upper line.

Exit from sell: When the previous value of the EOM Watcher indicator was below the lower standard deviation line, 
and the current value is above the lower line.

 */

namespace OsEngine.Robots.MyBots
{
    [Bot("ContrTrendEomWatcherAndADX")] // We create an attribute so that we don't write anything to the BotFactory
    internal class ContrTrendEomWatcherAndADX : BotPanel
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
        private StrategyParameterInt LengthEomW;
        private StrategyParameterInt LengthADX;

        // Indicator
        Aindicator _EomW;
        Aindicator _ADX;

        public ContrTrendEomWatcherAndADX(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthEomW = CreateParameter("Length EomW", 24, 5, 100, 5, "Indicator");
            LengthADX = CreateParameter("Length ADX", 14, 10, 300, 10, "Indicator");

            // Create indicator EOMW
            _EomW = IndicatorsFactory.CreateIndicatorByName("EaseOfMovement_Watcher", name + "EOMW", false);
            _EomW = (Aindicator)_tab.CreateCandleIndicator(_EomW, "EomWArea");
            ((IndicatorParameterInt)_EomW.Parameters[0]).ValueInt = LengthEomW.ValueInt;
            _EomW.Save();

            // Create indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "AdxArea");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = LengthADX.ValueInt;
            _ADX.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += ContrTrendEomWatcher_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Counter-trend robot on the EOM Watcher and ADX indicators. " +
                "Buy:  When the ADX +DM indicator is above -DM, the previous value of the EOM Watcher indicator " +
                "was below the lower standard deviation line, and the current value is above the lower line." +
                "Sell: When the ADX +DM indicator is below -DM, the previous value of the EOM Watcher indicator " +
                "was above the upper standard deviation line, and the current value is below the upper line." +
                "Exit from buy: When the previous value of the EOM Watcher indicator was above the upper line of the standard deviation, " +
                "and the current value is below the upper line." +
                "Exit from sell: When the previous value of the EOM Watcher indicator was below the lower standard deviation line, " +
                "and the current value is above the lower line.";
        }

        private void ContrTrendEomWatcher_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_EomW.Parameters[0]).ValueInt = LengthEomW.ValueInt;
            _EomW.Save();
            _EomW.Reload();

            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = LengthADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "ContrTrendEomWatcherAndADX";
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
            if (candles.Count < LengthEomW.ValueInt ||
                candles.Count < LengthADX.ValueInt)
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
            decimal lastEOMWUp = _EomW.DataSeries[0].Last;
            decimal lastEOMWDown = _EomW.DataSeries[1].Last;
            decimal lastSDUp = _EomW.DataSeries[2].Last;
            decimal lastSdDown = _EomW.DataSeries[3].Last;

            decimal lastPlus = _ADX.DataSeries[1].Last;
            decimal lastMinus = _ADX.DataSeries[2].Last;

            // The prev value of the indicator
            decimal prevEOMWUp = _EomW.DataSeries[0].Values[_EomW.DataSeries[0].Values.Count - 2];
            decimal prevEOMWDown = _EomW.DataSeries[1].Values[_EomW.DataSeries[1].Values.Count - 2];
            decimal prevSDUp = _EomW.DataSeries[2].Values[_EomW.DataSeries[2].Values.Count - 2];
            decimal prevSdDown = _EomW.DataSeries[3].Values[_EomW.DataSeries[3].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastEOMWDown > lastSdDown && prevEOMWDown < prevSdDown && lastPlus > lastMinus)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastEOMWUp < lastSDUp && prevEOMWUp > prevSDUp && lastPlus < lastMinus)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];

            // The last value of the indicator
            decimal lastEOMWUp = _EomW.DataSeries[0].Last;
            decimal lastEOMWDown = _EomW.DataSeries[1].Last;
            decimal lastSDUp = _EomW.DataSeries[2].Last;
            decimal lastSdDown = _EomW.DataSeries[3].Last;

            // The prev value of the indicator
            decimal prevEOMWUp = _EomW.DataSeries[0].Values[_EomW.DataSeries[0].Values.Count - 2];
            decimal prevEOMWDown = _EomW.DataSeries[1].Values[_EomW.DataSeries[1].Values.Count - 2];
            decimal prevSDUp = _EomW.DataSeries[2].Values[_EomW.DataSeries[2].Values.Count - 2];
            decimal prevSdDown = _EomW.DataSeries[3].Values[_EomW.DataSeries[3].Values.Count - 2];

            decimal slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (lastEOMWUp < lastSDUp && prevEOMWUp > prevSDUp)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastEOMWDown > lastSdDown && prevEOMWDown < prevSdDown)
                    {
                        _tab.CloseAtLimit(pos, lastPrice + slippage, pos.OpenVolume);
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
