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

The trend robot on Price Channel and ATR

Buy: The candle closed above the upper PC line.

Sell: Candle closed below the lower PC line.

Exit from buy: the candle closed below the bottom line PC - MultAtr * Atr.

Exit from sell: Candle closes above upper line PC + MultAtr * Atr.
 */


namespace OsEngine.Robots.AO
{
    [Bot("PriceChannelATR")] // We create an attribute so that we don't write anything to the BotFactory
    public class PriceChannelATR : BotPanel
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
        private StrategyParameterInt PcUpLength;
        private StrategyParameterInt PcDownLength;
        private StrategyParameterInt LengthAtr;
        private StrategyParameterDecimal MultAtr;

        // Indicator
        Aindicator _PC;
        Aindicator _ATR;

        // The last value of the indicator
        private decimal _lastATR;

        // The prev value of the indicator
        private decimal _prevUpPC;
        private decimal _prevDownPC;

        public PriceChannelATR(string name, StartProgram startProgram) : base(name, startProgram)
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
            PcUpLength = CreateParameter("Up Line Length", 21, 7, 48, 7,"Indicator");
            PcDownLength = CreateParameter("Down Line Length",21,7,48,7, "Indicator");
            LengthAtr = CreateParameter("Length ATR", 14, 7, 48, 7, "Indicator");
            MultAtr = CreateParameter("Mult ATR", 0.5m, 0.1m, 2, 0.1m, "Indicator");

            // Create indicator PC
            _PC = IndicatorsFactory.CreateIndicatorByName("PriceChannel",name + "PC", false);
            _PC = (Aindicator)_tab.CreateCandleIndicator(_PC, "Prime");
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = PcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = PcDownLength.ValueInt;
            _PC.Save();

            // Create indicator ATR
            _ATR = IndicatorsFactory.CreateIndicatorByName("ATR", name + "Atr", false);
            _ATR = (Aindicator)_tab.CreateCandleIndicator(_ATR, "NewArea");
            ((IndicatorParameterInt)_ATR.Parameters[0]).ValueInt = LengthAtr.ValueInt;
            _ATR.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += PriceChannelATR_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Price Channel and ATR " +
                "Buy: The candle closed above the upper PC line. " +
                "Sell: Candle closed below the lower PC line. " +
                "Exit from buy: the candle closed below the bottom line PC - MultAtr * Atr. " +
                "Exit from sell: Candle closes above upper line PC + MultAtr * Atr.";
        }

        private void PriceChannelATR_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = PcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = PcDownLength.ValueInt;
            _PC.Save();
            _PC.Reload();
            ((IndicatorParameterInt)_ATR.Parameters[0]).ValueInt = LengthAtr.ValueInt;
            _ATR.Save();
            _ATR.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PriceChannelATR";
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
            if (candles.Count < LengthAtr.ValueInt || 
                candles.Count < PcUpLength.ValueInt ||
                candles.Count < PcDownLength.ValueInt)
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
            // The prev value of the indicator
            _prevUpPC = _PC.DataSeries[0].Values[_PC.DataSeries[0].Values.Count - 2];
            _prevDownPC = _PC.DataSeries[1].Values[_PC.DataSeries[1].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_prevUpPC < lastPrice)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_prevDownPC > lastPrice)
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

            // The prev value of the indicator
            _prevUpPC = _PC.DataSeries[0].Values[_PC.DataSeries[0].Values.Count - 2];
            _prevDownPC = _PC.DataSeries[1].Values[_PC.DataSeries[1].Values.Count - 2];

            // The last value of the indicator
            _lastATR = _ATR.DataSeries[0].Last;

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
                    if (_prevDownPC - (MultAtr.ValueDecimal * _lastATR) > lastPrice)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_prevUpPC + (MultAtr.ValueDecimal * _lastATR) < lastPrice)
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
