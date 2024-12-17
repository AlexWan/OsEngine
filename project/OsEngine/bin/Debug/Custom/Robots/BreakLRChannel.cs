using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
trading robot for osengine

The trend robot on Break Linear Regression Channel.

Buy: the price is above the upper LR line.

Sell: the price is below the lower LR line.

Exit: the reverse side of the channel.

 */


namespace OsEngine.Robots.AO
{
    [Bot("BreakLRChannel")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakLRChannel : BotPanel
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
        private StrategyParameterInt PeriodLR;
        private StrategyParameterDecimal UpDeviation;
        private StrategyParameterDecimal DownDeviation;

        // Indicator
        Aindicator ChannelLR;

        // The last value of the indicator
        private decimal _lastUpLine;
        private decimal _lastDownLine;

        public BreakLRChannel(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodLR = CreateParameter("PeriodLR", 1, 1, 50, 1, "Indicator");
            UpDeviation = CreateParameter("UpDeviation", 1.0m, 1, 50, 1, "Indicator");
            DownDeviation = CreateParameter("DownDeviation", 2.0m, 1, 50, 1, "Indicator");

            // Create indicator LinearRegressionChannel
            ChannelLR = IndicatorsFactory.CreateIndicatorByName("LinearRegressionChannel", name + "LinearRegressionChannel", false);
            ChannelLR = (Aindicator)_tab.CreateCandleIndicator(ChannelLR, "Prime");
            ((IndicatorParameterInt)ChannelLR.Parameters[0]).ValueInt = PeriodLR.ValueInt;
            ((IndicatorParameterDecimal)ChannelLR.Parameters[2]).ValueDecimal = UpDeviation.ValueDecimal;
            ((IndicatorParameterDecimal)ChannelLR.Parameters[3]).ValueDecimal = DownDeviation.ValueDecimal;
            ChannelLR.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakLRChannel_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Break Linear Regression Channel. " +
                "Buy: the price is above the upper LR line. " +
                "Sell: the price is below the lower LR line. " +
                "Exit: the reverse side of the channel.";
        }

        private void BreakLRChannel_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)ChannelLR.Parameters[0]).ValueInt = PeriodLR.ValueInt;
            ((IndicatorParameterDecimal)ChannelLR.Parameters[2]).ValueDecimal = UpDeviation.ValueDecimal;
            ((IndicatorParameterDecimal)ChannelLR.Parameters[3]).ValueDecimal = DownDeviation.ValueDecimal;
            ChannelLR.Save();
            ChannelLR.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakLRChannel";
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
            if (candles.Count < DownDeviation.ValueDecimal ||
                candles.Count < UpDeviation.ValueDecimal ||
                candles.Count < PeriodLR.ValueInt)
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
            _lastUpLine = ChannelLR.DataSeries[0].Last;
            _lastDownLine = ChannelLR.DataSeries[2].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {


                    if (_lastUpLine < lastPrice)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastDownLine > lastPrice)
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

            // The last value of the indicator
            _lastUpLine = ChannelLR.DataSeries[0].Last;
            _lastDownLine = ChannelLR.DataSeries[2].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastDownLine > lastPrice)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastUpLine < lastPrice)
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
