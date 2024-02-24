using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Trading robot for osengine.

The trend robot on Strategy Trix And Envelops.

Buy:
1. Trix above 0.
2. The candle closed above the upper line of the Envelope.
Sell:
1. Trix is below 0.
2. The candle closed below the bottom line of the Envelope.
Exit from buy: The trailing stop is placed at the minimum for the period
specified for the trailing stop and transferred (slides) to new price lows, also for the specified period.
Exit from sell: The trailing stop is placed at the maximum for the period
specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.
 */

namespace OsEngine.Robots.My_bots
{
    [Bot("StrategyTrixAndEnvelops")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyTrixAndEnvelops : BotPanel
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
        private StrategyParameterInt EnvelopsLength;
        private StrategyParameterDecimal EnvelopsDeviation;
        private StrategyParameterInt PeriodTrix;

        // Indicator
        Aindicator _Envelop;
        Aindicator _Trix;

        // Exit
        private StrategyParameterInt TrailCandlesLong;
        private StrategyParameterInt TrailCandlesShort;

        // The last value of the indicator
        private decimal _lastUpLine;
        private decimal _lastDownLine;
        private decimal _lastTrix;

        public StrategyTrixAndEnvelops(string name, StartProgram startProgram) : base(name, startProgram)
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
            EnvelopsLength = CreateParameter("Envelops Length", 21, 7, 48, 7, "Indicator");
            EnvelopsDeviation = CreateParameter("Envelops Deviation", 1.0m, 1, 5, 0.1m, "Indicator");
            PeriodTrix = CreateParameter("Period Trix", 9, 7, 48, 7, "Indicator");

            // Create indicator Envelop
            _Envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _Envelop = (Aindicator)_tab.CreateCandleIndicator(_Envelop, "Prime");
            ((IndicatorParameterInt)_Envelop.Parameters[0]).ValueInt = EnvelopsLength.ValueInt;
            ((IndicatorParameterDecimal)_Envelop.Parameters[1]).ValueDecimal = EnvelopsDeviation.ValueDecimal;
            _Envelop.Save();

            // Create indicator Trix
            _Trix = IndicatorsFactory.CreateIndicatorByName("Trix", name + "Trix", false);
            _Trix = (Aindicator)_tab.CreateCandleIndicator(_Trix, "NewArea");
            ((IndicatorParameterInt)_Trix.Parameters[0]).ValueInt = PeriodTrix.ValueInt;
            _Trix.Save();

            // Exit
            TrailCandlesLong = CreateParameter("Trail Candles Long", 5, 5, 200, 5, "Exit");
            TrailCandlesShort = CreateParameter("Trail Candles Short", 5, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyTrixAndEnvelops_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Trix And Envelops. " +
                "Buy: " +
                "1. Trix above 0. " +
                "2. The candle closed above the upper line of the Envelope. " +
                "Sell: " +
                "1. Trix is below 0. " +
                "2. The candle closed below the bottom line of the Envelope. " +
                "Exit from buy: The trailing stop is placed at the minimum for the period " +
                "specified for the trailing stop and transferred (slides) to new price lows, also for the specified period. " +
                "Exit from sell: The trailing stop is placed at the maximum for the period " +
                "specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.";
        }

        private void StrategyTrixAndEnvelops_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Envelop.Parameters[0]).ValueInt = EnvelopsLength.ValueInt;
            ((IndicatorParameterDecimal)_Envelop.Parameters[1]).ValueDecimal = EnvelopsDeviation.ValueDecimal;
            _Envelop.Save();
            _Envelop.Reload();
            ((IndicatorParameterInt)_Trix.Parameters[0]).ValueInt = PeriodTrix.ValueInt;
            _Trix.Save();
            _Trix.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyTrixAndEnvelops";
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
            if (candles.Count < EnvelopsDeviation.ValueDecimal ||
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
            _lastUpLine = _Envelop.DataSeries[0].Last;
            _lastDownLine = _Envelop.DataSeries[2].Last;
            _lastTrix = _Trix.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastUpLine && _lastTrix > 0)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < _lastDownLine && _lastTrix < 0)
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
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (positions.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(openPositions[0], price, price - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal price = GetPriceStop(Side.Sell, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(openPositions[0], price, price + _slippage);
                }

            }
        }
        private decimal GetPriceStop(Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < TrailCandlesLong.ValueInt || index < TrailCandlesShort.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                decimal price = decimal.MaxValue;

                for (int i = index; i > index - TrailCandlesLong.ValueInt; i--)
                {
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }
                return price;
            }

            if (side == Side.Sell)
            {
                decimal price = 0;

                for (int i = index; i > index - TrailCandlesShort.ValueInt; i--)
                {
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }
            return 0;
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
