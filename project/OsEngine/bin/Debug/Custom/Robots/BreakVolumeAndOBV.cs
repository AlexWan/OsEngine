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
using System.Windows.Media.Animation;

/* Description
trading robot for osengine

The trend robot on strategy Break Volume And OBV.

Buy:
 1. The Volume indicator has grown CoefVolume times over the EntryCandlesLong period.
 2. The value of the OBV indicator broke through the minimum for a certain number of candles (EntryCandlesLong) and closed lower.
Sell:
 1. The Volume indicator has grown CoefVolume times over the EntryCandlesShort period.
 2. The value of the OBV indicator broke through the maximum for a certain number of candles (EntryCandlesShort) and closed higher.
Exit from buy: The trailing stop is placed at the minimum for the period
specified for the trailing stop and transferred (slides) to new price lows, also for the specified period.

Exit from sell: The trailing stop is placed at the maximum for the period
specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.
 
 */


namespace OsEngine.Robots.AO
{
    [Bot("BreakVolumeAndOBV")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakVolumeAndOBV : BotPanel
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
        private StrategyParameterDecimal CoefVolume;

        // Indicator
        Aindicator _Volume;
        Aindicator _OBV;

        // Enter
        private StrategyParameterInt EntryCandlesLong;
        private StrategyParameterInt EntryCandlesShort;

        // Exit
        private StrategyParameterInt TrailCandlesLong;
        private StrategyParameterInt TrailCandlesShort;

        // The last value of the indicator
        private decimal _lastVolume; 

        public BreakVolumeAndOBV(string name, StartProgram startProgram) : base(name, startProgram)
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
            CoefVolume = CreateParameter("CoefVolume", 0m, 0, 20, 0.1m, "Indicator");

            // Create indicator AD
            _Volume = IndicatorsFactory.CreateIndicatorByName("Volume", name + "Volume", false);
            _Volume = (Aindicator)_tab.CreateCandleIndicator(_Volume, "NewArea");
            _Volume.Save();

            // Create indicator OBV
            _OBV = IndicatorsFactory.CreateIndicatorByName("OBV", name + "OBV", false);
            _OBV = (Aindicator)_tab.CreateCandleIndicator(_OBV, "NewArea0");
            _OBV.Save();

            // Enter 
            EntryCandlesLong = CreateParameter("Entry Candles Long", 10, 5, 200, 5, "Enter");
            EntryCandlesShort = CreateParameter("Entry Candles Short", 10, 5, 200, 5, "Enter");

            // Exit
            TrailCandlesLong = CreateParameter("Trail Candles Long", 5, 5, 200, 5, "Exit");
            TrailCandlesShort = CreateParameter("Trail Candles Short", 5, 5, 200, 5, "Exit");

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on strategy Break Volume And OBV. " +
                "Buy: " +
                " 1. The Volume indicator has grown CoefVolume times over the EntryCandlesLong period. " +
                " 2. The value of the OBV indicator broke through the minimum for a certain number of candles (EntryCandlesLong) and closed lower. " +
                "Sell: " +
                " 1. The Volume indicator has grown CoefVolume times over the EntryCandlesShort period. " +
                " 2. The value of the OBV indicator broke through the maximum for a certain number of candles (EntryCandlesShort) and closed higher. " +
                "Exit from buy: The trailing stop is placed at the minimum for the period " +
                "specified for the trailing stop and transferred (slides) to new price lows, also for the specified period. " +
                "Exit from sell: The trailing stop is placed at the maximum for the period " +
                "specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.";
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakVolumeAndOBV";
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
            if (candles.Count < EntryCandlesShort.ValueInt + 10 || candles.Count < EntryCandlesLong.ValueInt + 10)
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

            if (openPositions == null || openPositions.Count == 0)
            {
                // The last value of the indicator
                _lastVolume = _Volume.DataSeries[0].Last;

                List<decimal> values = _OBV.DataSeries[0].Values;
                List<decimal> valuesVolume = _Volume.DataSeries[0].Values;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {


                    if (EnterLongAndShort(values, EntryCandlesLong.ValueInt) == "true" && 
                        Average(valuesVolume, EntryCandlesLong.ValueInt)*CoefVolume.ValueDecimal < _lastVolume)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (EnterLongAndShort(values, EntryCandlesShort.ValueInt) == "false" &&
                        Average(valuesVolume, EntryCandlesShort.ValueInt) * CoefVolume.ValueDecimal < _lastVolume)
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

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal price = GetPriceStop(Side.Buy, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(position, price, price - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal price = GetPriceStop(Side.Sell, candles, candles.Count - 1);
                    if (price == 0)
                    {
                        return;
                    }
                    _tab.CloseAtTrailingStop(position, price, price + _slippage);
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

        private decimal Average(List<decimal> values, int period)
        {
            decimal sum = 0;

            for (int i = 1; i <= period; i++)
            {
                sum += values[i];
            }

            decimal Avarege = sum / period;

            return Avarege;
        }

        private string EnterLongAndShort(List<decimal> values, int period)
        {
            decimal Max = -9999999;
            decimal Min = 9999999;
            for (int i = 1; i <= period; i++)
            {
                if (values[values.Count - 1 - i] > Max)
                {
                    Max = values[values.Count - 1 - i];
                }
                if (values[values.Count - 1 - i] < Min)
                {
                    Min = values[values.Count - 1 - i];
                }
            }
            if (Max < values[values.Count - 1])
            {
                return "true";
            }
            else if (Min > values[values.Count - 1])
            {
                return "false";
            }
            return "nope";
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
    }
}