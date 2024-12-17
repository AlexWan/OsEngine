using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on Strategy Price Channel With Rsi And CoG.

Buy:When the Rsi indicator is above 50 and CoG is above the level from the parameters, 
we place a pending buy order along the top line of the PriceChannel indicator.

Sell:When the Rsi indicator is below 50 and CoG is below the level from the parameters, 
we place a pending sell order along the lower line of the PriceChannel indicator.

Exit from buy: We set a trailing stop as a percentage of the low of the candle at which we entered and along the lower border of the PriceChannel indicator.
The calculation method that is closest to the current price is selected.

Exit from sell: We set a trailing stop as a percentage of the high of the candle at which we entered and along the upper border of the PriceChannel indicator. 
The calculation method that is closest to the current price is selected.
 */

namespace OsEngine.Robots.MyBots
{
    [Bot("StrategyPCRsiAndCoG")] // We create an attribute so that we don't write anything to the BotFactory
    internal class StrategyPCRsiAndCoG : BotPanel
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
        private StrategyParameterInt LengthCog;
        private StrategyParameterInt LengthRSI;
        private StrategyParameterInt PcUpLength;
        private StrategyParameterInt PcDownLength;
        private StrategyParameterDecimal EntryLevel;

        // Exit
        private StrategyParameterDecimal TrailingValue;

        // Indicator
        Aindicator _Cog;
        Aindicator _RSI;
        Aindicator _PC;
        public StrategyPCRsiAndCoG(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthCog = CreateParameter("CoG Length", 14, 5, 50, 1, "Indicator");
            LengthRSI = CreateParameter("RSI Length", 14, 5, 80, 1, "Indicator");
            PcUpLength = CreateParameter("Up Line Length", 21, 7, 48, 7, "Indicator");
            PcDownLength = CreateParameter("Down Line Length", 21, 7, 48, 7, "Indicator");
            EntryLevel = CreateParameter("Entry Level for CoG", 0.5m, 0.1m, 1, 0.1m, "Indicator");

            // Exit
            TrailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Create indicator RSI
            _RSI = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _RSI = (Aindicator)_tab.CreateCandleIndicator(_RSI, "RsiArea");
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = LengthRSI.ValueInt;
            _RSI.Save();

            // Create indicator CoG
            _Cog = IndicatorsFactory.CreateIndicatorByName("COG_CentreOfGravity_Oscr", name + "CoG", false);
            _Cog = (Aindicator)_tab.CreateCandleIndicator(_Cog, "CogArea");
            ((IndicatorParameterInt)_Cog.Parameters[0]).ValueInt = LengthCog.ValueInt;
            _Cog.Save();

            // Create indicator PC
            _PC = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PC", false);
            _PC = (Aindicator)_tab.CreateCandleIndicator(_PC, "Prime");
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = PcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = PcDownLength.ValueInt;
            _PC.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyPCRsiAndCoG_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Successful position opening event
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

           Description = "The trend robot on Strategy Price Channel With Rsi And CoG. " +
                "Buy: When the Rsi indicator is above 50 and CoG is above the level from the parameters, " +
                "we place a pending buy order along the top line of the PriceChannel indicator." +
                "Sell: When the Rsi indicator is below 50 and CoG is below the level from the parameters," +
                " we place a pending sell order along the lower line of the PriceChannel indicator. " +
                "Exit from buy: We set a trailing stop as a percentage of the low of the candle at which we entered and along the lower border of the PriceChannel indicator. " +
                "The calculation method that is closest to the current price is selected." +
                "Exit from sell: We set a trailing stop as a percentage of the high of the candle at which we entered and along the upper border of the PriceChannel indicator." +
                " The calculation method that is closest to the current price is selected.";
        }

        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            _tab.SellAtStopCancel();
            _tab.BuyAtStopCancel();
        }

        private void StrategyPCRsiAndCoG_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = LengthRSI.ValueInt;
            _RSI.Save();
            _RSI.Reload();

            ((IndicatorParameterInt)_Cog.Parameters[0]).ValueInt = LengthCog.ValueInt;
            _Cog.Save();
            _Cog.Reload();

            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = PcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = PcDownLength.ValueInt;
            _PC.Save();
            _PC.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyPCRsiAndCoG";
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
            if (candles.Count < LengthRSI.ValueInt ||candles.Count < LengthCog.ValueInt ||
                candles.Count < PcUpLength.ValueInt || candles.Count < PcDownLength.ValueInt)
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
            decimal lastCog = _Cog.DataSeries[0].Last;
            decimal lastRSI = _RSI.DataSeries[0].Last;
            decimal upChannel = _PC.DataSeries[0].Last;
            decimal downChannel = _PC.DataSeries[1].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastCog > EntryLevel.ValueDecimal && lastRSI > 50)
                    {
                        _tab.BuyAtStopCancel();
                        _tab.BuyAtStop(GetVolume(), upChannel + _slippage, upChannel, StopActivateType.HigherOrEqual);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastCog < EntryLevel.ValueDecimal && lastRSI < 50)
                    {
                        _tab.SellAtStopCancel();
                        _tab.SellAtStop(GetVolume(), downChannel - _slippage, downChannel, StopActivateType.LowerOrEqyal);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal upChannel = _PC.DataSeries[0].Last;
            decimal downChannel = _PC.DataSeries[1].Last;

            decimal stopPrice;
            decimal stop_level = 0;

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
                    stop_level = stopPrice > downChannel ? stopPrice : downChannel;
                }
                else // If the direction of the position is sale
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * TrailingValue.ValueDecimal / 100;
                    stop_level = stopPrice < upChannel ? stopPrice : upChannel;
                }
                _tab.CloseAtTrailingStop(pos, stop_level, stop_level);
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
