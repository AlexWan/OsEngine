using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on Strategy Parabolic With Sma Channel.

Buy:
1. The price is higher than the Parabolic value. 
2. The price is higher than SmaHigh.
Sell:
1. The price is lower than the Parabolic value. 
2. The price is lower than SmaLow.
Exit the position: the opposite boundary of the Sma channel.
 
 */


namespace OsEngine.Robots.Aligator
{
    // We create an attribute so that we don't write anything to the BotFactory
    [Bot("StrategyParabolicWithSmaChannel")]
    public class StrategyParabolicWithSmaChannel : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay StartTradeTime;
        private StrategyParameterTimeOfDay EndTradeTime;

        // Setting indicator
        private StrategyParameterDecimal Step;
        private StrategyParameterDecimal MaxStep;
        private StrategyParameterInt PeriodSmaChannel;

        // Indicator
        private Aindicator _Parabolic;
        private Aindicator SmaUp;
        private Aindicator SmaDown;

        // The last value of the indicators
        private decimal _lastParabolic;
        private decimal _lastSmaUp;
        private decimal _lastSmaDown;

        public StrategyParabolicWithSmaChannel(string name, StartProgram startProgram) : base(name, startProgram)
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

            // Setting indicator
            Step = CreateParameter("Step", 10, 10.0m, 300, 10, "Indicator");
            MaxStep = CreateParameter("Max Step", 20, 10.0m, 300, 10, "Indicator");
            PeriodSmaChannel = CreateParameter("EMA Channel Length", 10, 50, 50, 400, "Indicator");

            // Create indicator Parabolic
            _Parabolic = IndicatorsFactory.CreateIndicatorByName("ParabolicSAR", name + "Parabolic", false);
            _Parabolic = (Aindicator)_tab.CreateCandleIndicator(_Parabolic, "Prime");
            ((IndicatorParameterDecimal)_Parabolic.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_Parabolic.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _Parabolic.Save();

            // Creating indicator SmaUp
            SmaUp = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaUp", false);
            SmaUp = (Aindicator)_tab.CreateCandleIndicator(SmaUp,"Prime");
            ((IndicatorParameterInt)SmaUp.Parameters[0]).ValueInt = PeriodSmaChannel.ValueInt;
            ((IndicatorParameterString)SmaUp.Parameters[1]).ValueString = "High";
            SmaUp.Save();

            // Creating indicator SmaDown
            SmaDown = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaDown", false);
            SmaDown = (Aindicator)_tab.CreateCandleIndicator(SmaDown,"Prime");
            ((IndicatorParameterInt)SmaDown.Parameters[0]).ValueInt = PeriodSmaChannel.ValueInt;
            ((IndicatorParameterString)SmaDown.Parameters[1]).ValueString = "Low";
            SmaDown.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyParabolicWithSmaChannel_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Parabolic With Sma Channel. " +
                "Buy: " +
                "1. The price is higher than the Parabolic value.  " +
                "2. The price is higher than SmaHigh. " +
                "Sell: " +
                "1. The price is lower than the Parabolic value.  " +
                "2. The price is lower than SmaLow. " +
                "Exit the position: the opposite boundary of the Sma channel.";
        }

        // Indicator Update event
        private void StrategyParabolicWithSmaChannel_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_Parabolic.Parameters[0]).ValueDecimal = Step.ValueDecimal;
            ((IndicatorParameterDecimal)_Parabolic.Parameters[1]).ValueDecimal = MaxStep.ValueDecimal;
            _Parabolic.Save();
            _Parabolic.Reload();
            ((IndicatorParameterInt)SmaUp.Parameters[0]).ValueInt = PeriodSmaChannel.ValueInt;
            SmaUp.Save();
            SmaUp.Reload();
            ((IndicatorParameterInt)SmaDown.Parameters[0]).ValueInt = PeriodSmaChannel.ValueInt;
            SmaDown.Save();
            SmaDown.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyParabolicWithSmaChannel";
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
            if (candles.Count < PeriodSmaChannel.ValueInt)
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
                _lastParabolic = _Parabolic.DataSeries[0].Last;
                _lastSmaUp = SmaUp.DataSeries[0].Last;
                _lastSmaDown = SmaDown.DataSeries[0].Last;

                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                decimal lastPrice = candles[candles.Count - 1].Close;
                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastParabolic && _lastSmaUp < lastPrice)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (lastPrice < _lastParabolic && _lastSmaDown > lastPrice)
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

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // The last value of the indicators
            _lastSmaUp = SmaUp.DataSeries[0].Last;
            _lastSmaDown = SmaDown.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {

                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }


                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (_lastSmaDown > lastPrice)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastSmaUp < lastPrice)
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
