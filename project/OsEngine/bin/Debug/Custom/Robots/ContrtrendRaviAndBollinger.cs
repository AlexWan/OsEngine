using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

Counter-trend robot based on Ravi and Bollinger indicators.

Buy: When the candle closed below the lower Bollinger line, and the Ravi indicator value is below the lower line.

Sell: When the candle closed above the upper Bollinger line, and the Ravi indicator value is above the upper line.

Exit from buy: When the candle closed above the upper Bollinger line.

Exit from sell: When the candle closed above the upper Bollinger line.

 */

namespace OsEngine.Robots.MyBots
{
    [Bot("ContrtrendRaviAndBollinger")] // We create an attribute so that we don't write anything to the BotFactory
    internal class ContrtrendRaviAndBollinger : BotPanel
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
        private StrategyParameterInt LengthFastRavi;
        private StrategyParameterInt LengthSlowRavi;
        private StrategyParameterDecimal RaviUpLine;
        private StrategyParameterDecimal RaviDownLine;
        private StrategyParameterInt BollingerLength;
        private StrategyParameterDecimal BollingerDeviation;

        // Indicator
        private Aindicator _Ravi;
        private Aindicator _Bollinger;

        public ContrtrendRaviAndBollinger(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthFastRavi = CreateParameter("Ravi Fast Length", 10, 5, 150, 10, "Indicator");
            LengthSlowRavi = CreateParameter("Ravi Slow Length", 50, 50, 200, 10, "Indicator");
            RaviUpLine = CreateParameter("Ravi Up Line", 3m, 1m, 3, 0.1m, "Indicator");
            RaviDownLine = CreateParameter("Ravi Down Line", 3m, 1m, 3, 0.1m, "Indicator");
            BollingerLength = CreateParameter("Bollinger Length", 21, 7, 48, 7, "Indicator");
            BollingerDeviation = CreateParameter("Bollinger Deviation", 1.0m, 1, 5, 0.1m, "Indicator");

            // Create indicator Ravi
            _Ravi = IndicatorsFactory.CreateIndicatorByName("RAVI", name + "Ravi", false);
            _Ravi = (Aindicator)_tab.CreateCandleIndicator(_Ravi, "RaviArea");
            ((IndicatorParameterInt)_Ravi.Parameters[0]).ValueInt = LengthSlowRavi.ValueInt;
            ((IndicatorParameterInt)_Ravi.Parameters[1]).ValueInt = LengthFastRavi.ValueInt;
            ((IndicatorParameterDecimal)_Ravi.Parameters[2]).ValueDecimal = RaviUpLine.ValueDecimal;
            ((IndicatorParameterDecimal)_Ravi.Parameters[3]).ValueDecimal = -RaviDownLine.ValueDecimal;
            _Ravi.Save();

            // Create indicator Bollinger
            _Bollinger = IndicatorsFactory.CreateIndicatorByName("Bollinger", name + "Bollinger", false);
            _Bollinger = (Aindicator)_tab.CreateCandleIndicator(_Bollinger, "Prime");
            ((IndicatorParameterInt)_Bollinger.Parameters[0]).ValueInt = BollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_Bollinger.Parameters[1]).ValueDecimal = BollingerDeviation.ValueDecimal;
            _Bollinger.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += ContrtrendRaviAndBollinger_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Counter-trend robot based on Ravi and Bollinger indicators. " +
                "Buy: When the candle closed below the lower Bollinger line, and the Ravi indicator value is below the lower line." +
                "Sell: When the candle closed above the upper Bollinger line, and the Ravi indicator value is above the upper line." +
                "Exit from buy: When the candle closed above the upper Bollinger line. " +
                "Exit from sell: When the candle closed above the upper Bollinger line.";
        }

        private void ContrtrendRaviAndBollinger_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Ravi.Parameters[0]).ValueInt = LengthSlowRavi.ValueInt;
            ((IndicatorParameterInt)_Ravi.Parameters[1]).ValueInt = LengthFastRavi.ValueInt;
            ((IndicatorParameterDecimal)_Ravi.Parameters[2]).ValueDecimal = RaviUpLine.ValueDecimal;
            ((IndicatorParameterDecimal)_Ravi.Parameters[3]).ValueDecimal = -RaviDownLine.ValueDecimal;
            _Ravi.Save();
            _Ravi.Reload();

            ((IndicatorParameterInt)_Bollinger.Parameters[0]).ValueInt = BollingerLength.ValueInt;
            ((IndicatorParameterDecimal)_Bollinger.Parameters[1]).ValueDecimal = BollingerDeviation.ValueDecimal;
            _Bollinger.Save();
            _Bollinger.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "ContrtrendRaviAndBollinger";
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
            if (candles.Count < LengthSlowRavi.ValueInt ||
                candles.Count < BollingerLength.ValueInt)
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
            decimal lastRavi = _Ravi.DataSeries[0].Last;
            decimal lastUpLine = _Bollinger.DataSeries[0].Last;
            decimal lastDownLine = _Bollinger.DataSeries[1].Last;

            // The prev value of the indicator
            decimal prevRavi = _Ravi.DataSeries[0].Values[_Ravi.DataSeries[0].Values.Count - 2];
            decimal prevUpLine = _Bollinger.DataSeries[0].Values[_Bollinger.DataSeries[0].Values.Count - 2];
            decimal prevDownLine = _Bollinger.DataSeries[1].Values[_Bollinger.DataSeries[1].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal prevPriceLow = candles[candles.Count - 2].Low;
                decimal prevPriceHigh = candles[candles.Count - 2].High;

                decimal lastPriceLow = candles[candles.Count - 1].Low;
                decimal lastPriceHigh = candles[candles.Count - 1].High;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastRavi > -RaviDownLine.ValueDecimal && prevRavi < -RaviDownLine.ValueDecimal &&
                         lastPriceLow < lastDownLine)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastRavi < RaviUpLine.ValueDecimal && prevRavi > RaviUpLine.ValueDecimal &&
                        lastPriceHigh > lastUpLine)
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

            // The last value of the indicator
            decimal lastRavi = _Ravi.DataSeries[0].Last;
            decimal lastUpLine = _Bollinger.DataSeries[0].Last;
            decimal lastDownLine = _Bollinger.DataSeries[1].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = _tab.Securiti.PriceStep * Slippage.ValueDecimal / 100;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (lastPrice > lastUpLine)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice < lastDownLine)
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
