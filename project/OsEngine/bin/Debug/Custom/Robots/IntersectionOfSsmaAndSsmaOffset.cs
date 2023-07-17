using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;

/*Discription
Trading robot for osengine.

Trend robot at the Intersection of Ssma and  Ssma offset.

Buy: Fast Ssma is higher than slow Ssma.

Sale: Fast Ssma is lower than slow Ssma.

Exit: on the opposite signal.
*/

namespace OsEngine.Robots.MyRobots
{
    [Bot("IntersectionOfSsmaAndSsmaOffset")]//We create an attribute so that we don't write anything in the Boot factory
    public class IntersectionOfSsmaAndSsmaOffset : BotPanel
    {
        BotTabSimple _tab;
        
        // Basic Settings
        private StrategyParameterString Regime;
        private StrategyParameterDecimal VolumeOnPosition;
        private StrategyParameterString VolumeRegime;
        private StrategyParameterDecimal Slippage;
        private StrategyParameterTimeOfDay TimeStart;
        private StrategyParameterTimeOfDay TimeEnd;

        // Indicator
        private Aindicator _ssma1;
        private Aindicator _ssma2;

        // Indicator setting
        private StrategyParameterInt _periodSsmaFast;
        private StrategyParameterInt _periodSsmaSlow;
        private StrategyParameterInt _periodOffset;

        // The last value of the indicators
        private decimal _lastSsmaSlow;
        private decimal _lastSsmaFast;

        public IntersectionOfSsmaAndSsmaOffset(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            VolumeRegime = CreateParameter("Volume type", "Number of contracts", new[] { "Number of contracts", "Contract currency" }, "Base");
            VolumeOnPosition = CreateParameter("Volume", 10, 1.0m, 50, 4, "Base");
            Slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            TimeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            TimeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Indicator Settings
            _periodSsmaFast = CreateParameter("fast Ssma1 period", 250, 50, 500, 50, "Indicator");
            _periodSsmaSlow = CreateParameter("slow Ssma2 period", 1000, 500, 1500, 100, "Indicator");
            _periodOffset = CreateParameter("offset SSma2 period", 0, 3, 10, 11, "Indicator");
           
            // Creating an indicator Ssma1
            _ssma1 = IndicatorsFactory.CreateIndicatorByName(nameClass: "Ssma", name: name + "Ssma1", canDelete: false);
            _ssma1 = (Aindicator)_tab.CreateCandleIndicator(_ssma1, nameArea: "Prime");
            ((IndicatorParameterInt)_ssma1.Parameters[0]).ValueInt = _periodSsmaFast.ValueInt;
            _ssma1.ParametersDigit[0].Value = _periodSsmaFast.ValueInt;
            _ssma1.DataSeries[0].Color = Color.Red;
            _ssma1.Save();

            // Creating indicator Ssma2
            _ssma2 = IndicatorsFactory.CreateIndicatorByName(nameClass: "OffsetEma", name: name + "Ssma2", canDelete: false);
            _ssma2 = (Aindicator)_tab.CreateCandleIndicator(_ssma2, nameArea: "Prime");
            _ssma2.ParametersDigit[0].Value = _periodSsmaSlow.ValueInt;
            _ssma2.ParametersDigit[1].Value = _periodOffset.ValueInt;
            _ssma2.DataSeries[0].Color = Color.Green;
            _ssma2.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfSsmaAndSsmaOffset_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Trading robot for osengine. " +
                "Trend robot at the Intersection of Ssma and  Ssma offset. " +
                "Buy: Fast Ssma is higher than slow Ssma. " +
                "Sale: Fast Ssma is lower than slow Ssma. " +
                "Exit: on the opposite signal.";

        }

        // Indicator Update event
        private void IntersectionOfSsmaAndSsmaOffset_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ssma1.Parameters[0]).ValueInt = _periodSsmaFast.ValueInt;
            _ssma1.Save();
            _ssma1.Reload();
            ((IndicatorParameterInt)_ssma2.Parameters[0]).ValueInt = _periodSsmaSlow.ValueInt;
            _ssma2.Save();
            _ssma2.Reload();
        }


        // Candle Completion Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _periodSsmaFast.ValueInt || candles.Count < _periodSsmaSlow.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (TimeStart.Value > _tab.TimeServerCurrent ||
                TimeEnd.Value < _tab.TimeServerCurrent)
            {
                return;
            }
            List<Position> openPositions = _tab.PositionsOpenAll;

            // if there are positions, then go to the position closing method
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


            // He last value of the indicators
            _lastSsmaFast = _ssma1.DataSeries[0].Last;
            _lastSsmaSlow = _ssma2.DataSeries[0].Last;


            if (openPositions == null || openPositions.Count == 0)
            {
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastSsmaFast > _lastSsmaSlow && lastPrice > _lastSsmaFast)
                    {
                        // We put a stop on the buy                       
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastSsmaFast < _lastSsmaSlow && lastPrice < _lastSsmaFast)
                    {
                        // Putting a stop on sale
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
            decimal lastPrice = candles[candles.Count - 1].Close;

            // He last value of the indicators
            _lastSsmaFast = _ssma1.DataSeries[0].Last;
            _lastSsmaSlow = _ssma2.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }
                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is buy
                {
                    if (_lastSsmaFast < _lastSsmaSlow && lastPrice < _lastSsmaFast)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastSsmaFast > _lastSsmaSlow && lastPrice > _lastSsmaFast)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice + _slippage, openPositions[0].OpenVolume);
                    }
                }
            }
        }


        // The name of the robot in OsEngin
        public override string GetNameStrategyType()
        {
            return "IntersectionOfSsmaAndSsmaOffset";
        }

        public override void ShowIndividualSettingsDialog()
        {

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