using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
trading robot for osengine

The coutertrend robot on Two Channel Linear Regression.

Buy: the price has become lower than the lower line of the global linear regression channel,
we place a purchase order at the price of the lower line of the local channel.

Sell: the price has become higher than the upper line of the global linear regression channel, 
we place a buy order at the price of the upper line of the local channel.

Exit: channel center.

 */


namespace OsEngine.Robots.MyBot
{
    [Bot("CountertrendTwoChannelLR")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendTwoChannelLR : BotPanel
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
        private StrategyParameterDecimal UpDeviationLoc;
        private StrategyParameterDecimal DownDeviationLoc;
        private StrategyParameterDecimal UpDeviationGlob;
        private StrategyParameterDecimal DownDeviationGlob;

        // Indicator
        Aindicator ChannelLRLoc;
        Aindicator ChannelLRGlob;

        // The last value of the indicator
        private decimal _lastUpLineLoc;
        private decimal _lastDownLineLoc;
        private decimal _lastUpLineGlob;
        private decimal _lastDownLineGlob;
        private decimal _lastCenterLineLoc;

        // The prev value of the indicator
        private decimal _prevCenterLineLoc;

        public CountertrendTwoChannelLR(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodLR = CreateParameter("PeriodLRLoc", 100, 10, 500, 10, "Indicator");
            UpDeviationLoc = CreateParameter("UpDeviationLoc", 1.0m, 1, 50, 1, "Indicator");
            DownDeviationLoc = CreateParameter("DownDeviationLoc", 1.0m, 1, 50, 1, "Indicator");
            UpDeviationGlob = CreateParameter("UpDeviationGlob", 3.0m, 1, 50, 1, "Indicator");
            DownDeviationGlob = CreateParameter("DownDeviationGlob", 3.0m, 1, 50, 1, "Indicator");

            // Create indicator LinearRegressionChannel
            ChannelLRLoc = IndicatorsFactory.CreateIndicatorByName("LinearRegressionChannel", name + "LinearRegressionLoc", false);
            ChannelLRLoc = (Aindicator)_tab.CreateCandleIndicator(ChannelLRLoc, "Prime");
            ((IndicatorParameterInt)ChannelLRLoc.Parameters[0]).ValueInt = PeriodLR.ValueInt;
            ((IndicatorParameterDecimal)ChannelLRLoc.Parameters[2]).ValueDecimal = UpDeviationLoc.ValueDecimal;
            ((IndicatorParameterDecimal)ChannelLRLoc.Parameters[3]).ValueDecimal = DownDeviationLoc.ValueDecimal;
            ChannelLRLoc.Save();

            // Create indicator LinearRegressionChannel
            ChannelLRGlob = IndicatorsFactory.CreateIndicatorByName("LinearRegressionChannel", name + "LinearRegressionGlob", false);
            ChannelLRGlob = (Aindicator)_tab.CreateCandleIndicator(ChannelLRGlob, "Prime");
            ((IndicatorParameterInt)ChannelLRGlob.Parameters[0]).ValueInt = PeriodLR.ValueInt;
            ((IndicatorParameterDecimal)ChannelLRGlob.Parameters[2]).ValueDecimal = UpDeviationGlob.ValueDecimal;
            ((IndicatorParameterDecimal)ChannelLRGlob.Parameters[3]).ValueDecimal = DownDeviationGlob.ValueDecimal;
            ChannelLRGlob.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendTwoChannelLR_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The coutertrend robot on Two Channel Linear Regression. " +
                "Buy: the price has become lower than the lower line of the global linear regression channel, " +
                "we place a purchase order at the price of the lower line of the local channel. " +
                "Sell: the price has become higher than the upper line of the global linear regression channel, " +
                "we place a buy order at the price of the upper line of the local channel. " +
                "Exit: channel center.";
        }

        private void CountertrendTwoChannelLR_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)ChannelLRLoc.Parameters[0]).ValueInt = PeriodLR.ValueInt;
            ((IndicatorParameterDecimal)ChannelLRLoc.Parameters[2]).ValueDecimal = UpDeviationLoc.ValueDecimal;
            ((IndicatorParameterDecimal)ChannelLRLoc.Parameters[3]).ValueDecimal = DownDeviationLoc.ValueDecimal;
            ChannelLRLoc.Save();
            ChannelLRLoc.Reload();
            ((IndicatorParameterInt)ChannelLRGlob.Parameters[0]).ValueInt = PeriodLR.ValueInt;
            ((IndicatorParameterDecimal)ChannelLRGlob.Parameters[2]).ValueDecimal = UpDeviationGlob.ValueDecimal;
            ((IndicatorParameterDecimal)ChannelLRGlob.Parameters[3]).ValueDecimal = DownDeviationGlob.ValueDecimal;
            ChannelLRGlob.Save();
            ChannelLRGlob.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendTwoChannelLR";
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
            if (candles.Count < PeriodLR.ValueInt)
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
            _lastUpLineLoc = ChannelLRLoc.DataSeries[0].Last;
            _lastDownLineLoc = ChannelLRLoc.DataSeries[2].Last;
            _lastUpLineGlob = ChannelLRGlob.DataSeries[0].Last;
            _lastDownLineGlob = ChannelLRGlob.DataSeries[2].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {


                    if (lastPrice < _lastDownLineGlob)
                    {
                        _tab.BuyAtLimit(GetVolume(), _lastDownLineLoc + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (lastPrice > _lastUpLineGlob)
                    {
                        _tab.SellAtLimit(GetVolume(), _lastUpLineLoc - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // The last value of the indicator
            _lastCenterLineLoc = ChannelLRLoc.DataSeries[1].Last;


            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastCenterLineLoc < lastPrice)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice + _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastCenterLineLoc > lastPrice)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
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
