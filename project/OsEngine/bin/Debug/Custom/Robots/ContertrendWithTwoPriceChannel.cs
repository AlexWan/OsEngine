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

The contertrend robot on two Price Channel.

Buy: the bottom line of the local PC has become higher than the bottom line of the global PC.

Sell: the top line of the local PC has become lower than the top line of the global PC.

Exit from buy: The trailing stop is placed at the minimum for the period specified for the trailing
stop and transferred (slides) to new price lows, also for the specified period.
Exit from sell: The trailing stop is placed at the maximum for the period specified for the trailing
stop and is transferred (slides) to the new maximum price, also for the specified period.

 */


namespace OsEngine.Robots.AO
{
    [Bot("ContertrendWithTwoPriceChannel")] // We create an attribute so that we don't write anything to the BotFactory
    public class ContertrendWithTwoPriceChannel : BotPanel
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
        private StrategyParameterInt PcUpLengthLocal;
        private StrategyParameterInt PcDownLengthLocal;
        private StrategyParameterInt PcUpLengthGlobol;
        private StrategyParameterInt PcDownLengthGlobol;

        // Indicator
        Aindicator _PcLocal;
        Aindicator _PcGlobal;

        // Exit
        private StrategyParameterInt TrailCandlesLong;
        private StrategyParameterInt TrailCandlesShort;

        // The last value of the indicator
        private decimal _lastUpPcLocal;
        private decimal _lastDownPcLocal;
        private decimal _lastUpPcGlobol;
        private decimal _lastDownPcGlobol;

        // The prev value of the indicator
        private decimal _prevUpPcLocal;
        private decimal _prevDownPcLocal;
        private decimal _prevUpPcGlobol;
        private decimal _prevDownPcGlobol;

        public ContertrendWithTwoPriceChannel(string name, StartProgram startProgram) : base(name, startProgram)
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
            PcUpLengthLocal = CreateParameter("Up Line Length one", 7, 7, 48, 7, "Indicator");
            PcDownLengthLocal = CreateParameter("Down Line Length one", 7, 7, 48, 7, "Indicator");
            PcUpLengthGlobol = CreateParameter("Up Line Length Two", 21, 7, 48, 7, "Indicator");
            PcDownLengthGlobol = CreateParameter("Down Line Length Two", 21, 7, 48, 7, "Indicator");

            // Create indicator PC one
            _PcLocal = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PC one", false);
            _PcLocal = (Aindicator)_tab.CreateCandleIndicator(_PcLocal, "Prime");
            ((IndicatorParameterInt)_PcLocal.Parameters[0]).ValueInt = PcUpLengthLocal.ValueInt;
            ((IndicatorParameterInt)_PcLocal.Parameters[1]).ValueInt = PcDownLengthLocal.ValueInt;
            _PcLocal.Save();

            // Create indicator PC two
            _PcGlobal = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PC two", false);
            _PcGlobal = (Aindicator)_tab.CreateCandleIndicator(_PcGlobal, "Prime");
            ((IndicatorParameterInt)_PcGlobal.Parameters[0]).ValueInt = PcUpLengthGlobol.ValueInt;
            ((IndicatorParameterInt)_PcGlobal.Parameters[1]).ValueInt = PcDownLengthGlobol.ValueInt;
            _PcGlobal.Save();

            // Exit
            TrailCandlesLong = CreateParameter("Trail Candles Long", 5, 5, 200, 5, "Exit");
            TrailCandlesShort = CreateParameter("Trail Candles Short", 5, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakEOMAndSma_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The contertrend robot on two Price Channel. " +
                "Buy: the bottom line of the local PC has become higher than the bottom line of the global PC. " +
                "Sell: the top line of the local PC has become lower than the top line of the global PC. " +
                "Exit from buy: The trailing stop is placed at the minimum for the period specified for the trailing " +
                "stop and transferred (slides) to new price lows, also for the specified period. " +
                "Exit from sell: The trailing stop is placed at the maximum for the period specified for the trailing " +
                "stop and is transferred (slides) to the new maximum price, also for the specified period.";
        }

        private void BreakEOMAndSma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_PcLocal.Parameters[0]).ValueInt = PcUpLengthLocal.ValueInt;
            ((IndicatorParameterInt)_PcLocal.Parameters[1]).ValueInt = PcDownLengthLocal.ValueInt;
            _PcLocal.Save();
            _PcLocal.Reload();
            ((IndicatorParameterInt)_PcGlobal.Parameters[0]).ValueInt = PcUpLengthGlobol.ValueInt;
            ((IndicatorParameterInt)_PcGlobal.Parameters[1]).ValueInt = PcDownLengthGlobol.ValueInt;
            _PcGlobal.Save();
            _PcGlobal.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ContertrendWithTwoPriceChannel";
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
            if (candles.Count < PcUpLengthLocal.ValueInt ||
                candles.Count < PcDownLengthLocal.ValueInt ||
                candles.Count < PcUpLengthGlobol.ValueInt ||
                candles.Count < PcDownLengthGlobol.ValueInt)
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
            _lastUpPcLocal = _PcLocal.DataSeries[0].Last;
            _lastDownPcLocal = _PcLocal.DataSeries[1].Last;
            _lastUpPcGlobol = _PcGlobal.DataSeries[0].Last;
            _lastDownPcGlobol = _PcGlobal.DataSeries[1].Last;

            // The prev value of the indicator
            _prevUpPcLocal = _PcLocal.DataSeries[0].Values[_PcLocal.DataSeries[0].Values.Count - 2];
            _prevDownPcLocal = _PcLocal.DataSeries[1].Values[_PcLocal.DataSeries[1].Values.Count - 2];
            _prevUpPcGlobol = _PcGlobal.DataSeries[0].Values[_PcGlobal.DataSeries[0].Values.Count - 2];
            _prevDownPcGlobol = _PcGlobal.DataSeries[1].Values[_PcGlobal.DataSeries[1].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_prevDownPcGlobol >= _prevDownPcLocal && _lastDownPcGlobol < _lastDownPcLocal)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_prevUpPcGlobol <= _prevUpPcLocal && _lastUpPcGlobol > _lastUpPcLocal)
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
