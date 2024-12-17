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

The trend robot on two Price Channel with Rsi.

Buy: the price is above the upper PCGlobal line and the Rsi is > 50.

Sell: the price is below the lower PCGlobal line and the Rsi is < 50.

Exit: the reverse side of the PCLocal channel.

 */


namespace OsEngine.Robots.AO
{
    [Bot("StrategyTwoPriceChannelWithRsi")] // We create an attribute so that we don't write anything to the BotFactory
    public class StrategyTwoPriceChannelWithRsi : BotPanel
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
        private StrategyParameterInt PeriodRSI;

        // Indicator
        Aindicator _PcLocal;
        Aindicator _PcGlobal;
        Aindicator _RSI;

        // The last value of the indicator
        private decimal _lastRsi;

        // The prev value of the indicator
        private decimal _prevUpPcLocal;
        private decimal _prevDownPcLocal;
        private decimal _prevUpPcGlobol;
        private decimal _prevDownPcGlobol;

        public StrategyTwoPriceChannelWithRsi(string name, StartProgram startProgram) : base(name, startProgram)
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
            PeriodRSI = CreateParameter("Period RSI", 14, 10, 300, 1, "Indicator");

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

            // Create indicator RSI
            _RSI = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _RSI = (Aindicator)_tab.CreateCandleIndicator(_RSI, "NewArea");
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = PeriodRSI.ValueInt;
            _RSI.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakEOMAndSma_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on two Price Channel with Rsi. " +
                "Buy: the price is above the upper PCGlobal line and the Rsi is > 50. " +
                "Sell: the price is below the lower PCGlobal line and the Rsi is < 50. " +
                "Exit: the reverse side of the PCLocal channel.";
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
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = PeriodRSI.ValueInt;
            _RSI.Save();
            _RSI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyTwoPriceChannelWithRsi";
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
            _lastRsi = _RSI.DataSeries[0].Last;

            // The prev value of the indicator
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
                    if (lastPrice > _prevUpPcGlobol && _lastRsi > 50)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < _prevDownPcGlobol && _lastRsi < 50)
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
            
            // The prev value of the indicator
            _prevUpPcLocal = _PcLocal.DataSeries[0].Values[_PcLocal.DataSeries[0].Values.Count - 2];
            _prevDownPcLocal = _PcLocal.DataSeries[1].Values[_PcLocal.DataSeries[1].Values.Count - 2];

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
                    if (lastPrice < _prevDownPcLocal)
                    {
                        _tab.CloseAtLimit(position, lastPrice - _slippage, position.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (lastPrice > _prevUpPcLocal)
                    {
                        _tab.CloseAtLimit(position, lastPrice + _slippage, position.OpenVolume);
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
