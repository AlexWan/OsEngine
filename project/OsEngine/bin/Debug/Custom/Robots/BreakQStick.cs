using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on Break QStick.

Buy: When the QStick indicator broke the maximum for a certain number of candles and the candle closed above the Sma line.

Sell: When the QStick indicator broke the minimum for a certain number of candles and the candle closed below the Sma line.

Exit from buy: We set the stop to the minimum for the period specified for the stop, 
and the profit is equal to the size of the stop multiplied by the coefficient from the parameters.

Exit from sell: We set the stop to the maximum for the period specified for the stop, 
and the profit is equal to the size of the stop multiplied by the coefficient from the parameters.

 */

namespace OsEngine.Robots.MyBots
{
    [Bot("BreakQStick")] // We create an attribute so that we don't write anything to the BotFactory
    internal class BreakQStick : BotPanel
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
        private StrategyParameterInt LengthSma;

        // Enter
        private StrategyParameterInt EntryCandlesLong;
        private StrategyParameterInt EntryCandlesShort;

        // Exit
        private StrategyParameterDecimal CoefProfit;
        private StrategyParameterInt StopCandles;

        // Indicator
        private Aindicator _QStick;
        private Aindicator _SMA;
        public BreakQStick(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthSma = CreateParameter("Length Sma", 20, 10, 200, 10, "Indicator");

            // Enter 
            EntryCandlesLong = CreateParameter("Entry Candles Long", 10, 5, 200, 5, "Enter");
            EntryCandlesShort = CreateParameter("Entry Candles Short", 10, 5, 200, 5, "Enter");

            // Exit
            CoefProfit = CreateParameter("Coef Profit", 1, 1m, 10, 1, "Exit settings");
            StopCandles = CreateParameter("Stop Candles", 10, 2, 100, 5, "Exit settings");

            // Create indicator _QStick
            _QStick = IndicatorsFactory.CreateIndicatorByName("QStick", name + "QStick", false);
            _QStick = (Aindicator)_tab.CreateCandleIndicator(_QStick, "QStickArea");
            ((IndicatorParameterInt)_QStick.Parameters[0]).ValueInt = LengthQStick.ValueInt;
            ((IndicatorParameterString)_QStick.Parameters[1]).ValueString = TypeMa.ValueString;
            _QStick.Save();

            // Create indicator Sma
            _SMA = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SMA", false);
            _SMA = (Aindicator)_tab.CreateCandleIndicator(_SMA, "Prime");
            ((IndicatorParameterInt)_SMA.Parameters[0]).ValueInt = LengthSma.ValueInt;
            _SMA.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakQStick_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Break QStick. " +
                "Buy: When the QStick indicator broke the maximum for a certain number of candles and the candle closed above the Sma line." +
                "Sell: When the QStick indicator broke the minimum for a certain number of candles and the candle closed below the Sma line." +
                "Exit from buy: We set the stop to the minimum for the period specified for the stop, " +
                "and the profit is equal to the size of the stop multiplied by the coefficient from the parameters." +
                "Exit from sell: We set the stop to the maximum for the period specified for the stop, " +
                "and the profit is equal to the size of the stop multiplied by the coefficient from the parameters.";
        }       

        private void BreakQStick_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_QStick.Parameters[0]).ValueInt = LengthQStick.ValueInt;
            ((IndicatorParameterString)_QStick.Parameters[1]).ValueString = TypeMa.ValueString;
            _QStick.Save();
            _QStick.Reload();

            ((IndicatorParameterInt)_SMA.Parameters[0]).ValueInt = LengthSma.ValueInt;
            _SMA.Save();
            _SMA.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "BreakQStick";
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
            if (candles.Count < EntryCandlesShort.ValueInt + 10 || candles.Count < EntryCandlesLong.ValueInt + 10
                || candles.Count < LengthSma.ValueInt || candles.Count < LengthQStick.ValueInt)
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
            decimal lastQStick = _QStick.DataSeries[0].Last;
            decimal lastSma = _SMA.DataSeries[0].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                List<decimal> values = _QStick.DataSeries[0].Values;

                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (EnterLong(values, EntryCandlesLong.ValueInt) <= lastQStick && lastPrice > lastSma)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (EnterShort(values, EntryCandlesShort.ValueInt) >= lastQStick && lastPrice < lastSma)
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

            decimal profitActivation;
            decimal price;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {

                    decimal stopActivation = GetPriceStop(pos.TimeCreate, Side.Buy, candles, candles.Count - 1);

                    if (stopActivation == 0)
                    {
                        return;
                    }

                    price = stopActivation;
                    profitActivation = pos.EntryPrice + (pos.EntryPrice - price) * CoefProfit.ValueDecimal;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal stopActivation = GetPriceStop(pos.TimeCreate, Side.Sell, candles, candles.Count - 1);

                    if (stopActivation == 0)
                    {
                        return;
                    }

                    price = stopActivation;
                    profitActivation = pos.EntryPrice - (price - pos.EntryPrice) * CoefProfit.ValueDecimal;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
                }
            }
        }

        // Method for finding the maximum for a period
        private decimal EnterLong(List<decimal> values, int period)
        {
            decimal Max = int.MinValue;
            for (int i = values.Count - 1; i > values.Count - 1 - period; i--)
            {
                if (values[i] > Max)
                {
                    Max = values[i];
                }
            }
            return Max;
        }

        // Method for finding the minimum for a period
        private decimal EnterShort(List<decimal> values, int period)
        {
            decimal Min = int.MaxValue;
            for (int i = values.Count - 1; i > values.Count -1 - period; i--)
            {
                if (values[i] < Min)
                {
                    Min = values[i];
                }
            }
            return Min;
        }

        private decimal GetPriceStop(DateTime positionCreateTime, Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < StopCandles.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                // We calculate the stop price at Long
                // We find the minimum for the time from the opening of the transaction to the current one
                decimal price = decimal.MaxValue; ;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 2].TimeStart;
                }

                for (int i = index; i > 0; i--)
                {
                    // Look at the index of the candle, after which the opening of the pose occurred
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - StopCandles.ValueInt; i--)
                {
                    // Looking at the minimum after opening
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }

                return price;
            }

            if (side == Side.Sell)
            {
                //  We find the maximum for the time from the opening of the transaction to the current one
                decimal price = 0;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 1].TimeStart;
                }

                for (int i = index; i > 0; i--)
                {
                    // Look at the index of the candle, after which the opening of the pose occurred
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - StopCandles.ValueInt; i--)
                {
                    // Looking at the maximum high
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
