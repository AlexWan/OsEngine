using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
Trading robot for osengine.

The trend robot on Strategy Trix And Envelops.

Buy:
1. Trix above 0.
2. The candle closed above the upper line of the Envelope.
Sell:
1. Trix is below 0.
2. The candle closed below the bottom line of the Envelope.
Exit from buy: The trailing stop is placed at the minimum for the period
specified for the trailing stop and transferred (slides) to new price lows, also for the specified period.
Exit from sell: The trailing stop is placed at the maximum for the period
specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.
 */

namespace OsEngine.Robots.My_bots
{
    [Bot("CountertrendTrixPriceChannelAndCCI")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendTrixPriceChannelAndCCI : BotPanel
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
        private StrategyParameterInt PeriodTrix;
        private StrategyParameterInt PeriodCCI;

        // Indicator
        Aindicator _PC;
        Aindicator _Trix;
        Aindicator _CCI;

        // Exit
        private StrategyParameterDecimal CoefProfit;
        private StrategyParameterInt StopCandles;

        // The last value of the indicator
        private decimal _lastTrix;
        private decimal _lastCCI;

        // The prev value of the indicator
        private decimal _prevUpPC;
        private decimal _prevDownPC;
        private decimal _prevCCI;
        private decimal _prevTrix;

        public CountertrendTrixPriceChannelAndCCI(string name, StartProgram startProgram) : base(name, startProgram)
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
            PcUpLength = CreateParameter("Up Line Length", 21, 7, 48, 7, "Indicator");
            PcDownLength = CreateParameter("Down Line Length", 21, 7, 48, 7, "Indicator");
            PeriodTrix = CreateParameter("Period Trix", 9, 20, 300, 1, "Indicator");
            PeriodCCI = CreateParameter("Period CCI", 14, 10, 300, 1, "Indicator");

            // Create indicator PC
            _PC = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PC", false);
            _PC = (Aindicator)_tab.CreateCandleIndicator(_PC, "Prime");
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = PcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = PcDownLength.ValueInt;
            _PC.Save();

            // Create indicator Trix
            _Trix = IndicatorsFactory.CreateIndicatorByName("Trix", name + "Trix", false);
            _Trix = (Aindicator)_tab.CreateCandleIndicator(_Trix, "NewArea");
            ((IndicatorParameterInt)_Trix.Parameters[0]).ValueInt = PeriodTrix.ValueInt;
            _Trix.Save();

            // Create indicator CCI
            _CCI = IndicatorsFactory.CreateIndicatorByName("CCI", name + "CCI", false);
            _CCI = (Aindicator)_tab.CreateCandleIndicator(_CCI, "NewArea0");
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = PeriodCCI.ValueInt;
            _CCI.Save();

            // Exit
            CoefProfit = CreateParameter("Coef Profit", 1, 1m, 10, 1, "Exit settings");
            StopCandles = CreateParameter("Stop Candles", 1, 2, 10, 1, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendTrixPriceChannelAndCCI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Strategy Trix And Envelops. " +
                "Buy: " +
                "1. Trix above 0. " +
                "2. The candle closed above the upper line of the Envelope. " +
                "Sell: " +
                "1. Trix is below 0. " +
                "2. The candle closed below the bottom line of the Envelope. " +
                "Exit from buy: The trailing stop is placed at the minimum for the period " +
                "specified for the trailing stop and transferred (slides) to new price lows, also for the specified period. " +
                "Exit from sell: The trailing stop is placed at the maximum for the period " +
                "specified for the trailing stop and is transferred (slides) to the new maximum of the price, also for the specified period.";
        }

        private void CountertrendTrixPriceChannelAndCCI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_PC.Parameters[0]).ValueInt = PcUpLength.ValueInt;
            ((IndicatorParameterInt)_PC.Parameters[1]).ValueInt = PcDownLength.ValueInt;
            _PC.Save();
            _PC.Reload();
            ((IndicatorParameterInt)_Trix.Parameters[0]).ValueInt = PeriodTrix.ValueInt;
            _Trix.Save();
            _Trix.Reload();
            ((IndicatorParameterInt)_CCI.Parameters[0]).ValueInt = PeriodCCI.ValueInt;
            _CCI.Save();
            _CCI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendTrixPriceChannelAndCCI";
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
            if (candles.Count < PcUpLength.ValueInt ||
                candles.Count < PcDownLength.ValueInt ||
                candles.Count < PeriodTrix.ValueInt ||
                candles.Count < PeriodCCI.ValueInt)
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
            _lastTrix = _Trix.DataSeries[0].Last;
            _lastCCI = _CCI.DataSeries[0].Last;

            // The prev value of the indicator
            _prevTrix = _Trix.DataSeries[0].Values[_Trix.DataSeries[0].Values.Count - 2];
            _prevCCI = _CCI.DataSeries[0].Values[_CCI.DataSeries[0].Values.Count - 2];
            _prevUpPC = _PC.DataSeries[0].Values[_PC.DataSeries[0].Values.Count - 2];
            _prevDownPC = _PC.DataSeries[1].Values[_PC.DataSeries[1].Values.Count - 2];

            List <Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal prevPrice = candles[candles.Count - 2].Close;
                decimal HighPrice = candles[candles.Count - 2].High;
                decimal LowPrice = candles[candles.Count - 2].Low;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastTrix < 0 && _prevTrix < _lastTrix &&
                        _lastCCI < -100 && _prevCCI < _lastCCI &&
                        LowPrice <= _prevDownPC && prevPrice > _prevDownPC)
                    {
                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastTrix < 0 && _prevTrix < _lastTrix &&
                        _lastCCI > 100 && _prevCCI > _lastCCI &&
                        HighPrice >= _prevUpPC && prevPrice < _prevUpPC)
                    {
                        _tab.SellAtLimit(GetVolume(), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        //  Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {

            List<Position> openPositions = _tab.PositionsOpenAll;
            Position pos = openPositions[0];

            decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal profitActivation;
            decimal price;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {

                    decimal stopActivation = GetPriceStop(openPositions[i].TimeCreate, Side.Buy, candles, candles.Count - 1);

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
                    decimal stopActivation = GetPriceStop(openPositions[i].TimeCreate, Side.Sell, candles, candles.Count - 1);

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
