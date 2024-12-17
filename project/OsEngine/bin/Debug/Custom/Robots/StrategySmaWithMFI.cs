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
using System.Windows.Media.Animation;
using System.Security.Cryptography;

/* Description
trading robot for osengine

The trend robot on Strategy Sma With MFI.

Buy:
1. The candle closed above the SMA.
2. MFI is above 50 and growing.
Sell:
1. The candle closed below the SMA.
2. MFI is below 50 and falling.

Exit: after a certain number of candles.
 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategySmaWithMFI")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategySmaWithMFI : BotPanel
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
        private StrategyParameterInt PeriodSma;

        // Indicator
        Aindicator _MFI;
        Aindicator _SMA;

        // The last value of the indicator
        private decimal _lastMFI;
        private decimal _lastSma;

        // The prev value of the indicator
        private decimal _prevMFI;

        // Exit
        private StrategyParameterInt ExitCandles;

        public StrategySmaWithMFI(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodSma = CreateParameter("Period Simple Moving Average", 20, 10, 200, 10, "Indicator");

            // Create indicator Sma
            _SMA = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SMA", false);
            _SMA = (Aindicator)_tab.CreateCandleIndicator(_SMA, "Prime");
            ((IndicatorParameterInt)_SMA.Parameters[0]).ValueInt = PeriodSma.ValueInt;
            _SMA.Save();

            // Create indicator MFI
            _MFI = IndicatorsFactory.CreateIndicatorByName("MFI", name + "MFI", false);
            _MFI = (Aindicator)_tab.CreateCandleIndicator(_MFI, "NewArea");
            ((IndicatorParameterInt)_MFI.Parameters[0]).ValueInt = _MFIPeriod.ValueInt;
            _MFI.Save();

            // Exit
            ExitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategySmaWithMFI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Sma With MFI. " +
                "Buy: " +
                "1. The candle closed above the SMA. " +
                "2. MFI is above 50 and growing. " +
                "Sell: " +
                "1. The candle closed below the SMA. " +
                "2. MFI is below 50 and falling. " +
                "Exit: after a certain number of candles.";
        }

        private void StrategySmaWithMFI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_MFI.Parameters[0]).ValueInt = _MFIPeriod.ValueInt;
            _MFI.Save();
            _MFI.Reload();
            ((IndicatorParameterInt)_SMA.Parameters[0]).ValueInt = PeriodSma.ValueInt;
            _SMA.Save();
            _SMA.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategySmaWithMFI";
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
            _lastSma = _SMA.DataSeries[0].Last;

            // The prev value of the indicator
            _prevMFI = _MFI.DataSeries[0].Values[_MFI.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastSma < lastPrice && _lastMFI > 50 && _lastMFI > _prevMFI)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastSma > lastPrice && _lastMFI < 50 && _lastMFI < _prevMFI)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage, time.ToString());
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

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (!NeedClosePosition(pos, candles))
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                }
                else // If the direction of the position is sale
                {
                    _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
                }

            }
        }

        private bool NeedClosePosition(Position position, List<Candle> candles)
        {
            if (position == null || position.OpenVolume == 0)
            {
                return false;
            }

            DateTime openTime = DateTime.Parse(position.SignalTypeOpen);

            int counter = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                counter++;
                DateTime candelTime = candles[i].TimeStart;
                if (candelTime == openTime)
                {
                    if (counter >= ExitCandles.ValueInt + 1)
                    {
                        return true;
                    }
                }
            }
            return false;
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
