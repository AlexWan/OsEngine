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

The trend robot on Strategy StdDev Sma And Rsi.

Buy:
1. RSI is more than 50.
2. Price is higher than SMA.
3. Standard Deviation is higher than MinValue.
Sell:
1. RSI is less than 50.
2. Price is lower than SMA.
3. Standard Deviation is higher than MinValue.
Exit: after a certain number of candles.
 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyStdDevSmaAndRsi")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyStdDevSmaAndRsi : BotPanel
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
        private StrategyParameterInt _PeriodStdDev;
        private StrategyParameterDecimal MinValue;
        private StrategyParameterInt PeriodRSI;
        private StrategyParameterInt PeriodSma;

        // Indicator
        Aindicator _StdDev;
        Aindicator _RSI;
        Aindicator _Sma;

        // The last value of the indicator
        private decimal _lastSD;
        private decimal _lastRSI;
        private decimal _lastSma;

        // Exit
        private StrategyParameterInt ExitCandles;

        public StrategyStdDevSmaAndRsi(string name, StartProgram startProgram) : base(name, startProgram)
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
            _PeriodStdDev = CreateParameter("StdDev Length", 20, 20, 48, 7, "Indicator");
            MinValue = CreateParameter("MinValue", 0.2m, 10, 200, 10, "Indicator");
            PeriodRSI = CreateParameter("Period RSI", 14, 10, 300, 1, "Indicator");
            PeriodSma = CreateParameter("Period Sma", 100, 10, 300, 10, "Indicator");

            // Create indicator Sma
            _Sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _Sma = (Aindicator)_tab.CreateCandleIndicator(_Sma, "Prime");
            ((IndicatorParameterInt)_Sma.Parameters[0]).ValueInt = PeriodSma.ValueInt;
            _Sma.Save();

            // Create indicator RSI
            _RSI = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _RSI = (Aindicator)_tab.CreateCandleIndicator(_RSI, "NewArea");
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = PeriodRSI.ValueInt;
            _RSI.Save();

            // Create indicator StdDev
            _StdDev = IndicatorsFactory.CreateIndicatorByName("StdDev", name + "StdDev", false);
            _StdDev = (Aindicator)_tab.CreateCandleIndicator(_StdDev, "NewArea0");
            ((IndicatorParameterInt)_StdDev.Parameters[0]).ValueInt = _PeriodStdDev.ValueInt;
            _StdDev.Save();

            // Exit
            ExitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyStdDevSmaAndRsi_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy StdDev Sma And Rsi. " +
                "Buy: " +
                "1. RSI is more than 50. " +
                "2. Price is higher than SMA. " +
                "3. Standard Deviation is higher than MinValue. " +
                "Sell: " +
                "1. RSI is less than 50. " +
                "2. Price is lower than SMA. " +
                "3. Standard Deviation is higher than MinValue. " +
                "Exit: after a certain number of candles.";
        }

        private void StrategyStdDevSmaAndRsi_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_StdDev.Parameters[0]).ValueInt = _PeriodStdDev.ValueInt;
            _StdDev.Save();
            _StdDev.Reload();
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = PeriodRSI.ValueInt;
            _RSI.Save();
            _RSI.Reload();
            ((IndicatorParameterInt)_Sma.Parameters[0]).ValueInt = PeriodSma.ValueInt;
            _Sma.Save();
            _Sma.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyStdDevSmaAndRsi";
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
            if (candles.Count < _PeriodStdDev.ValueInt)
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
            _lastSD = _StdDev.DataSeries[0].Last;
            _lastRSI = _RSI.DataSeries[0].Last;
            _lastSma = _Sma.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastRSI > 50 && _lastSD > MinValue.ValueDecimal && _lastSma < lastPrice)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastRSI < 50 && _lastSD > MinValue.ValueDecimal && _lastSma > lastPrice)
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
