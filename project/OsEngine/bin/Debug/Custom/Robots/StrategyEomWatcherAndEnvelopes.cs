using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

Trend robot based on Envelopes and EOM Watcher indicators.

Buy: When the candle closes above the upper line of the Envelopes indicator, and the EOM Watcher indicator is above zero.

Sell: When the candle closes below the lower line of the Envelopes indicator, and the EOM Watcher indicator is below zero.

Exit from buy: When the candle closed below the lower line of the Envelopes indicator.

Exit from sell: When the candle closed above the upper line of the Envelopes indicator.

 */

namespace OsEngine.Robots.MyBots
{
    [Bot("StrategyEomWatcherAndEnvelopes")] // We create an attribute so that we don't write anything to the BotFactory
    internal class StrategyEomWatcherAndEnvelopes : BotPanel
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
        private StrategyParameterInt EnvelopsLength;
        private StrategyParameterDecimal EnvelopsDeviation;

        // Indicator
        Aindicator _EomW;
        Aindicator _Envelop;
        public StrategyEomWatcherAndEnvelopes(string name, StartProgram startProgram) : base(name, startProgram)
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
            EnvelopsLength = CreateParameter("Envelops Length", 21, 7, 48, 7, "Indicator");
            EnvelopsDeviation = CreateParameter("Envelops Deviation", 1.0m, 1, 5, 0.1m, "Indicator");

            // Create indicator EOMW
            _EomW = IndicatorsFactory.CreateIndicatorByName("EaseOfMovement_Watcher", name + "EOMW", false);
            _EomW = (Aindicator)_tab.CreateCandleIndicator(_EomW, "EomWArea");
            ((IndicatorParameterInt)_EomW.Parameters[0]).ValueInt = LengthEomW.ValueInt;
            _EomW.Save();

            // Create indicator Envelops
            _Envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _Envelop = (Aindicator)_tab.CreateCandleIndicator(_Envelop, "Prime");
            ((IndicatorParameterInt)_Envelop.Parameters[0]).ValueInt = EnvelopsLength.ValueInt;
            ((IndicatorParameterDecimal)_Envelop.Parameters[1]).ValueDecimal = EnvelopsDeviation.ValueDecimal;
            _Envelop.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyEomWatcher_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trend robot based on Envelopes and EOM Watcher indicators. " +
                "Buy:  When the candle closes above the upper line of the Envelopes indicator, and the EOM Watcher indicator is above zero. " +
                "Sell: When the candle closes below the lower line of the Envelopes indicator, and the EOM Watcher indicator is below zero." +
                "Exit from buy: When the candle closed below the lower line of the Envelopes indicator. " +
                "Exit from sell: When the candle closed above the upper line of the Envelopes indicator. ";
                
        }

        private void StrategyEomWatcher_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_EomW.Parameters[0]).ValueInt = LengthEomW.ValueInt;
            _EomW.Save();
            _EomW.Reload();

            ((IndicatorParameterInt)_Envelop.Parameters[0]).ValueInt = EnvelopsLength.ValueInt;
            ((IndicatorParameterDecimal)_Envelop.Parameters[1]).ValueDecimal = EnvelopsDeviation.ValueDecimal;
            _Envelop.Save();
            _Envelop.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyEomWatcherAndEnvelopes";
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
                candles.Count < EnvelopsLength.ValueInt)
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
            decimal lastUpLine = _Envelop.DataSeries[0].Last;
            decimal lastDownLine = _Envelop.DataSeries[2].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastEOMWUp > 0 && lastPrice > lastUpLine)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastEOMWDown < 0 && lastPrice < lastDownLine)
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
         
            // The last value of the indicator
            decimal lastUpLine = _Envelop.DataSeries[0].Last;
            decimal lastDownLine = _Envelop.DataSeries[2].Last;

            decimal slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
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
                    if (lastPrice < lastDownLine)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice > lastUpLine)
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
