using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

/* Description
trading robot for osengine

The trend robot on Strategy Volume And MFI.

Buy:
1. The volume is higher than the VolumeValue value.
2. The MFI is lower than the MfiMinValue value.
Sell:
1. The volume is higher than the VolumeValue value.
2. The MFI is higher than the MfiMaxValue value.

Exit from buy: trailing stop in % of the loy of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.
 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyVolumeAndMFI")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyVolumeAndMFI : BotPanel
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
        private StrategyParameterInt _MFIPeriod;
        private StrategyParameterDecimal MfiMinValue;
        private StrategyParameterDecimal MfiMaxValue;
        private StrategyParameterDecimal VolumeValue;

        // Indicator
        Aindicator _MFI;
        Aindicator _Volume;

        // The last value of the indicator
        private decimal _lastMFI;
        private decimal _lastVolume;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        public StrategyVolumeAndMFI(string name, StartProgram startProgram) : base(name, startProgram)
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
            _MFIPeriod = CreateParameter("MFI Length", 3, 3, 48, 7, "Indicator");
            MfiMinValue = CreateParameter("MfiMinValue", 1.0m, 3, 48, 7, "Indicator");
            MfiMaxValue = CreateParameter("MfiMaxValue", 3.0m, 3, 48, 7, "Indicator");
            VolumeValue = CreateParameter("VolumeValue", 30.0m, 3, 48, 7, "Indicator");

            // Create indicator MFI
            _MFI = IndicatorsFactory.CreateIndicatorByName("MFI", name + "MFI", false);
            _MFI = (Aindicator)_tab.CreateCandleIndicator(_MFI, "NewArea");
            ((IndicatorParameterInt)_MFI.Parameters[0]).ValueInt = _MFIPeriod.ValueInt;
            _MFI.Save();

            // Create indicator Volume
            _Volume = IndicatorsFactory.CreateIndicatorByName("Volume", name + "Volume", false);
            _Volume = (Aindicator)_tab.CreateCandleIndicator(_Volume, "NewArea0");
            _Volume.Save();

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyVolumeAndMFI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Volume And MFI. " +
                "Buy: " +
                "1. The volume is higher than the VolumeValue value. " +
                "2. The MFI is lower than the MfiMinValue value. " +
                "Sell: " +
                "1. The volume is higher than the VolumeValue value. " +
                "2. The MFI is higher than the MfiMaxValue value. " +
                "Exit from buy: trailing stop in % of the loy of the candle on which you entered. " +
                "Exit from sell: trailing stop in % of the high of the candle on which you entered.";
        }

        private void StrategyVolumeAndMFI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_MFI.Parameters[0]).ValueInt = _MFIPeriod.ValueInt;
            _MFI.Save();
            _MFI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyVolumeAndMFI";
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
            if (candles.Count < _MFIPeriod.ValueInt)
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
            _lastMFI = _MFI.DataSeries[0].Last;
            _lastVolume = _Volume.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastMFI < MfiMinValue.ValueDecimal && _lastVolume > VolumeValue.ValueDecimal)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastMFI > MfiMaxValue.ValueDecimal && _lastVolume > VolumeValue.ValueDecimal)
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

            decimal stopPrice;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position positions = openPositions[i];

                if (positions.State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
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
