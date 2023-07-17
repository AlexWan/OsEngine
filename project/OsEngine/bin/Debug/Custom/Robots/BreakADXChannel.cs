using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Linq;
using OsEngine.Logging;

/* Description
trading robot for osengine

The trend robot on strategy break Channel Ema with ADX.

Buy: the price is above EmaHigh and Adx is growing and not above the critical value.

Sell: the price is below EmaLow and Adx is growing and not above the critical value.

Exit: stop and profit in % of the entry price. 

 */


namespace OsEngine.Robots.AO
{
    [Bot("BreakADXChannel")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakADXChannel : BotPanel
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
        private StrategyParameterInt PeriodEma;
        private StrategyParameterInt PeriodADX;
        private StrategyParameterInt CriticalValue;

        // Indicator
        Aindicator _EmaHigh;
        Aindicator _EmaLow;
        Aindicator _ADX;

        // The last value of the indicator
        private decimal _lastEmaHigh;
        private decimal _lastEmaLow;
        private decimal _lastADX;

        // The prev value of the indicator
        private decimal _prevADX;

        // Exit
        private StrategyParameterInt StopValue;
        private StrategyParameterInt ProfitValue;

        public BreakADXChannel(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodEma = CreateParameter("Period Ema", 10, 10, 300, 10, "Indicator");
            PeriodADX = CreateParameter("Period ADX", 10, 10, 300, 10, "Indicator");
            CriticalValue = CreateParameter("CriticalValue", 10, 10, 100, 10, "Indicator");

            // Create indicator EmaHigh
            _EmaHigh = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaHigh", false);
            _EmaHigh = (Aindicator)_tab.CreateCandleIndicator(_EmaHigh, "Prime");
            ((IndicatorParameterInt)_EmaHigh.Parameters[0]).ValueInt = PeriodEma.ValueInt;
            ((IndicatorParameterString)_EmaHigh.Parameters[1]).ValueString = "High";
            _EmaHigh.DataSeries[0].Color = Color.Pink;
            _EmaHigh.Save();

            // Create indicator EmaLow
            _EmaLow = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaLow", false);
            _EmaLow = (Aindicator)_tab.CreateCandleIndicator(_EmaLow, "Prime");
            ((IndicatorParameterInt)_EmaLow.Parameters[0]).ValueInt = PeriodEma.ValueInt;
            ((IndicatorParameterString)_EmaLow.Parameters[1]).ValueString = "Low";
            _EmaLow.DataSeries[0].Color = Color.GreenYellow;
            _EmaLow.Save();

            // Create indicator ADX
            _ADX = IndicatorsFactory.CreateIndicatorByName("ADX", name + "ADX", false);
            _ADX = (Aindicator)_tab.CreateCandleIndicator(_ADX, "NewArea");
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();

            // Exit
            StopValue = CreateParameter("Stop Value", 1, 5, 200, 5, "Exit");
            ProfitValue = CreateParameter("Profit Value", 1, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakADXChannel_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Descriptoin = "trading robot for osengine " +
                "The trend robot on strategy break Channel Ema with ADX. " +
                "Buy: the price is above EmaHigh and Adx is growing and not above the critical value. " +
                "Sell: the price is below EmaLow and Adx is growing and not above the critical value. " +
                "Exit: stop and profit in % of the entry price.";
        }

        private void BreakADXChannel_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_EmaHigh.Parameters[0]).ValueInt = PeriodEma.ValueInt;
            _EmaHigh.Save();
            _EmaHigh.Reload();
            ((IndicatorParameterInt)_EmaLow.Parameters[0]).ValueInt = PeriodEma.ValueInt;
            _EmaLow.Save();
            _EmaLow.Reload();
            ((IndicatorParameterInt)_ADX.Parameters[0]).ValueInt = PeriodADX.ValueInt;
            _ADX.Save();
            _ADX.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakADXChannel";
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
            if (candles.Count < PeriodADX.ValueInt || candles.Count < PeriodEma.ValueInt)
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
            _lastEmaHigh = _EmaHigh.DataSeries[0].Last;
            _lastEmaLow = _EmaLow.DataSeries[0].Last;
            _lastADX = _ADX.DataSeries[0].Last;

            // The prev value of the indicator
            _prevADX = _ADX.DataSeries[0].Values[_ADX.DataSeries[0].Values.Count-2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEmaHigh < lastPrice && _lastADX > _prevADX && _lastADX < CriticalValue.ValueInt)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (_lastEmaLow > lastPrice && _lastADX > _prevADX && _lastADX < CriticalValue.ValueInt)
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
                    decimal profitActivation = pos.EntryPrice + pos.EntryPrice * ProfitValue.ValueInt / 100;
                    decimal stopActivation = pos.EntryPrice - pos.EntryPrice * StopValue.ValueInt / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal profitActivation = pos.EntryPrice - pos.EntryPrice * ProfitValue.ValueInt / 100;
                    decimal stopActivation = pos.EntryPrice + pos.EntryPrice * StopValue.ValueInt / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
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