using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The robot on Overbought Oversold QStick.

Buy: QStick indicator values ​​below a certain value.

Sell: QStick indicator values ​​are above a certain value.

Exit: On the opposite signal.

 */

namespace OsEngine.Robots.MyBots
{
    [Bot("OverbougthOversoldQStick")] // We create an attribute so that we don't write anything to the BotFactory
    internal class OverbougthOversoldQStick : BotPanel
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
        private StrategyParameterInt LengthQStick;
        private StrategyParameterString TypeMa;
        private StrategyParameterDecimal BuyValue;
        private StrategyParameterDecimal SellValue;

        // Indicator
        private Aindicator _QStick;

        public OverbougthOversoldQStick(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthQStick = CreateParameter("Length QStick", 14, 10, 200, 10, "Indicator");
            TypeMa = CreateParameter("Type Ma", "SMA", new[] { "SMA", "EMA" }, "Indicator");
            BuyValue = CreateParameter("Buy Value", 5.0m, 10, 300, 10, "Indicator");
            SellValue = CreateParameter("Sell Value", 5.0m, 10, 300, 10, "Indicator");

            // Create indicator _QStick
            _QStick = IndicatorsFactory.CreateIndicatorByName("QStick", name + "QStick", false);
            _QStick = (Aindicator)_tab.CreateCandleIndicator(_QStick, "QStickArea");
            ((IndicatorParameterInt)_QStick.Parameters[0]).ValueInt = LengthQStick.ValueInt;
            ((IndicatorParameterString)_QStick.Parameters[1]).ValueString = TypeMa.ValueString;
            _QStick.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += OverbougthOversoldQStick_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The robot on Overbought Oversold QStick. " +
                "Buy: QStick indicator values ​​below a certain value. " +
                "Sell: QStick indicator values ​​are above a certain value. " +
                "Exit: On the opposite signal.";
        }

        private void OverbougthOversoldQStick_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_QStick.Parameters[0]).ValueInt = LengthQStick.ValueInt;
            ((IndicatorParameterString)_QStick.Parameters[1]).ValueString = TypeMa.ValueString;
            _QStick.Save();
            _QStick.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "OverbougthOversoldQStick";
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
            if (candles.Count < LengthQStick.ValueInt)
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
            decimal lastQStick = _QStick.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastQStick < -BuyValue.ValueDecimal)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastQStick > SellValue.ValueDecimal)
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
            decimal lastQStick = _QStick.DataSeries[0].Last;

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
                    if (lastQStick > SellValue.ValueDecimal)
                    {
                        _tab.CloseAtLimit(openPositions[0], lastPrice - _slippage, openPositions[0].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastQStick < -BuyValue.ValueDecimal)
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
