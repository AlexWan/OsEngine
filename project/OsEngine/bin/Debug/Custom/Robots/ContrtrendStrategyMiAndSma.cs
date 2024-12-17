using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

Counter-trend robot based on the MassIndex and Sma indicators.

Buy: When Sma falls and the current value of the MassIndex indicator is below the lower line, 
and the previous one was above the upper line.

Sell: When Sma grows and the current value of the MassIndex indicator is below the lower line, 
and the previous one was above the upper line.

Exit from buy: trailing stop in % of the loy of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.

 */

namespace OsEngine.Robots.MyBots
{
    [Bot("ContrtrendStrategyMiAndSma")] // We create an attribute so that we don't write anything to the BotFactory
    internal class ContrtrendStrategyMiAndSma : BotPanel
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
        private StrategyParameterInt MiLength;
        private StrategyParameterInt MiSumLength;
        private StrategyParameterDecimal UpLine;
        private StrategyParameterDecimal DownLine;
        private StrategyParameterInt SmaLength;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        // Indicator
        private Aindicator _Mi;
        private Aindicator _Sma;

        public ContrtrendStrategyMiAndSma(string name, StartProgram startProgram) : base(name, startProgram)
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
            MiLength = CreateParameter("Mi Length", 9, 5, 200, 5, "Indicator");
            MiSumLength = CreateParameter("Mi Sum Length", 25, 5, 200, 5, "Indicator");
            UpLine = CreateParameter("Mi Up Line", 27m, 5, 50, 2, "Indicator");
            DownLine = CreateParameter("Mi Down Line", 26.5m, 5, 50, 1, "Indicator");
            SmaLength = CreateParameter("Sma Length", 20, 5, 50, 1, "Indicator");

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Create indicator MassIndex
            _Mi = IndicatorsFactory.CreateIndicatorByName("Mass_Index_MI", name + "MI", false);
            _Mi = (Aindicator)_tab.CreateCandleIndicator(_Mi, "MiArea");
            ((IndicatorParameterInt)_Mi.Parameters[0]).ValueInt = MiLength.ValueInt;
            ((IndicatorParameterInt)_Mi.Parameters[1]).ValueInt = MiSumLength.ValueInt;
            ((IndicatorParameterDecimal)_Mi.Parameters[2]).ValueDecimal = UpLine.ValueDecimal;
            ((IndicatorParameterDecimal)_Mi.Parameters[3]).ValueDecimal = DownLine.ValueDecimal;
            _Mi.Save();

            // Create indicator Sma
            _Sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SMA", false);
            _Sma = (Aindicator)_tab.CreateCandleIndicator(_Sma, "Prime");
            ((IndicatorParameterInt)_Sma.Parameters[0]).ValueInt = SmaLength.ValueInt;
            _Sma.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyMiAndSma_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "Counter-trend robot based on the MassIndex and Sma indicators. " +
                "Buy: When Sma falls and the current value of the MassIndex indicator is below the lower line, " +
                "and the previous one was above the upper line." +
                "Sell: When Sma grows and the current value of the MassIndex indicator is below the lower line, " +
                "and the previous one was above the upper line." +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";
        }

        private void StrategyMiAndSma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Mi.Parameters[0]).ValueInt = MiLength.ValueInt;
            ((IndicatorParameterInt)_Mi.Parameters[1]).ValueInt = MiSumLength.ValueInt;
            ((IndicatorParameterDecimal)_Mi.Parameters[2]).ValueDecimal = UpLine.ValueDecimal;
            ((IndicatorParameterDecimal)_Mi.Parameters[3]).ValueDecimal = DownLine.ValueDecimal;
            _Mi.Save();
            _Mi.Reload();

            ((IndicatorParameterInt)_Sma.Parameters[0]).ValueInt = SmaLength.ValueInt;
            _Sma.Save();
            _Sma.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "ContrtrendStrategyMiAndSma";
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
            if (candles.Count < MiLength.ValueInt + 10 || candles.Count < MiSumLength.ValueInt 
                || candles.Count < SmaLength.ValueInt)
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
            decimal lastMi = _Mi.DataSeries[2].Last;
            decimal lastSma = _Sma.DataSeries[0].Last;

            // The prev value of the indicator
            decimal prevMi = _Mi.DataSeries[2].Values[_Mi.DataSeries[2].Values.Count - 2];
            decimal prevMi2 = _Mi.DataSeries[2].Values[_Mi.DataSeries[2].Values.Count - 3];
            decimal prevMi3 = _Mi.DataSeries[2].Values[_Mi.DataSeries[2].Values.Count - 4];
            decimal prevMi4 = _Mi.DataSeries[2].Values[_Mi.DataSeries[2].Values.Count - 5];
            decimal prevSma= _Sma.DataSeries[0].Values[_Sma.DataSeries[0].Values.Count - 2];

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if ((prevMi > UpLine.ValueDecimal || prevMi2 > UpLine.ValueDecimal || prevMi3 > UpLine.ValueDecimal
                        || prevMi4 > UpLine.ValueDecimal) && lastMi < DownLine.ValueDecimal && lastSma < prevSma)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if ((prevMi > UpLine.ValueDecimal || prevMi2 > UpLine.ValueDecimal || prevMi3 > UpLine.ValueDecimal 
                        || prevMi4 > UpLine.ValueDecimal) && lastMi < DownLine.ValueDecimal && lastSma > prevSma)
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
            
            decimal stopPrice;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * TrailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);
            }
        }

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
