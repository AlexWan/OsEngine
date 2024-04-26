using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on Break DeMarker and the intersection of two exponential averages.

Buy: When the DeMarker indicator value is above the maximum for the period and the fast Ema is higher than the slow Ema.

Sell: When the DeMarker indicator value is below the minimum for the period and the fast Ema is below the slow Ema.

Exit from buy: When fast Ema is lower than slow Ema.

Exit from sell: When fast Ema is higher than slow Ema.

 */

namespace OsEngine.Robots.MyBots
{
    [Bot("BreakDeMarkerAndIntersectionOfTwoEma")] // We create an attribute so that we don't write anything to the BotFactory
    internal class BreakDeMarkerAndIntersectionOfTwoEma : BotPanel
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
        private StrategyParameterInt DeMLength;
        private StrategyParameterInt LengthEmaFast;
        private StrategyParameterInt LengthEmaSlow;

        // Enter
        private StrategyParameterInt EntryCandlesLong;
        private StrategyParameterInt EntryCandlesShort;

        // Indicator
        private Aindicator _DeM;
        private Aindicator _Ema1;
        private Aindicator _Ema2;

        public BreakDeMarkerAndIntersectionOfTwoEma(string name, StartProgram startProgram) : base(name, startProgram)
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
            DeMLength = CreateParameter("DeM Length", 14, 5, 200, 10, "Indicator");
            LengthEmaFast = CreateParameter("fast EMA1 period", 30, 10, 300, 10, "Indicator");
            LengthEmaSlow = CreateParameter("slow EMA2 period", 100, 50, 500, 10, "Indicator");

            // Enter 
            EntryCandlesLong = CreateParameter("Entry Candles Long", 10, 5, 200, 5, "Enter");
            EntryCandlesShort = CreateParameter("Entry Candles Short", 10, 5, 200, 5, "Enter");


            // Create indicator DeMarker
            _DeM = IndicatorsFactory.CreateIndicatorByName("DeMarker_DeM", name + "DeMarker", false);
            _DeM = (Aindicator)_tab.CreateCandleIndicator(_DeM, "DeMArea");
            ((IndicatorParameterInt)_DeM.Parameters[0]).ValueInt = DeMLength.ValueInt;
            _DeM.Save();

            // Creating indicator Ema1
            _Ema1 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "EMA1", canDelete: false);
            _Ema1 = (Aindicator)_tab.CreateCandleIndicator(_Ema1, nameArea: "Prime");
            ((IndicatorParameterInt)_Ema1.Parameters[0]).ValueInt = LengthEmaFast.ValueInt;
            _Ema1.DataSeries[0].Color = Color.Red;
            _Ema1.Save();

            // Creating indicator Ema2
            _Ema2 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ema", name: name + "Ema2", canDelete: false);
            _Ema2 = (Aindicator)_tab.CreateCandleIndicator(_Ema2, nameArea: "Prime");
            ((IndicatorParameterInt)_Ema2.Parameters[0]).ValueInt = LengthEmaSlow.ValueInt;
            _Ema2.DataSeries[0].Color = Color.Green;
            _Ema2.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakDeMarker_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Break DeMarker and the intersection of two exponential averages. " +
                "Buy: When the DeMarker indicator value is above the maximum for the period and the fast Ema is higher than the slow Ema." +
                "Sell: When the DeMarker indicator value is below the minimum for the period and the fast Ema is below the slow Ema." +
                "Exit from buy: When fast Ema is lower than slow Ema. " +
                "Exit from sell: When fast Ema is higher than slow Ema.";
        }

        private void BreakDeMarker_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_DeM.Parameters[0]).ValueInt = DeMLength.ValueInt;
            _DeM.Save();
            _DeM.Reload();

            ((IndicatorParameterInt)_Ema1.Parameters[0]).ValueInt = LengthEmaFast.ValueInt;
            _Ema1.Save();
            _Ema1.Reload();

            ((IndicatorParameterInt)_Ema2.Parameters[0]).ValueInt = LengthEmaSlow.ValueInt;
            _Ema2.Save();
            _Ema2.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "BreakDeMarkerAndIntersectionOfTwoEma";
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
            if (candles.Count < DeMLength.ValueInt + 10 ||
                candles.Count < LengthEmaFast.ValueInt || candles.Count < LengthEmaSlow.ValueInt)
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

            // The last value of the indicator
            decimal lastDeM = _DeM.DataSeries[0].Last;
            decimal lastEmaFast = _Ema1.DataSeries[0].Last;
            decimal lastEmaSlow = _Ema2.DataSeries[0].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> values = _DeM.DataSeries[0].Values;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (EnterLong(values, EntryCandlesLong.ValueInt) < lastDeM && lastEmaFast > lastEmaSlow)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (EnterShort(values, EntryCandlesShort.ValueInt) > lastDeM && lastEmaFast < lastEmaSlow)
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

            // The last value of the indicator
            decimal lastEmaFast = _Ema1.DataSeries[0].Last;
            decimal lastEmaSlow = _Ema2.DataSeries[0].Last;
            // Slippage
            decimal _slippage = Slippage.ValueDecimal / 100 * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];
                decimal lastPrice = candles[candles.Count - 1].Close;

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is buy
                {
                    if (lastEmaFast < lastEmaSlow)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastEmaFast > lastEmaSlow)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice + _slippage, openPositions[0].OpenVolume);
                    }
                }
            }
        }

        // Method for finding the maximum for a period
        private decimal EnterLong(List<decimal> values, int period)
        {
            decimal Max = -9999999;
            for (int i = values.Count - 2; i > values.Count - 2 - period; i--)
            {
                if (values[i] > Max)
                {
                    Max = values[i];
                }
            }
            return Max;
        }

        // Method for finding the minimum for a period
        private decimal EnterShort(List<decimal> values, int period)
        {
            decimal Min = 9999999;
            for (int i = values.Count - 2; i > values.Count - 2 - period; i--)
            {
                if (values[i] < Min)
                {
                    Min = values[i];
                }
            }
            return Min;
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
