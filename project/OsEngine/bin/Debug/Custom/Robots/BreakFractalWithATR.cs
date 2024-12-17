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

The trend robot on Break Fractal With ATR.

Buy: 
If an upper fractal has formed on the chart, set BuyAtStop + lastAtr * CoefAtr.ValueDecimal. 

Sell: 
If a lower fractal has formed on the chart, set SellAtStop - lastAtr * CoefAtr.ValueDecimal. 

Exit:
The stop is placed at the minimum for the period specified for the stop (StopCandles). The profit is equal to the size of the stop * CoefProfit.

Exit:
The stop is placed at the maximum for the period specified for the stop (StopCandles). The profit is equal to the size of the stop * CoefProfit.
 */


namespace OsEngine.Robots.AO
{
    [Bot("BreakFractalWithATR")] // We create an attribute so that we don't write anything to the BotFactory
    public class BreakFractalWithATR : BotPanel
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
        private StrategyParameterInt LengthAtr;
        private StrategyParameterDecimal CoefAtr;

        // Indicator
        Aindicator _ATR;
        Aindicator _Fractal;

        // The last value of the indicator
        private decimal _lastAtr;
        private decimal _lastUpFract;
        private decimal _lastDownFract;
        private decimal _lastIndexDown;
        private decimal _lastIndexUp;

        // Exit
        private StrategyParameterDecimal CoefProfit;
        private StrategyParameterInt StopCandles;

        public BreakFractalWithATR(string name, StartProgram startProgram) : base(name, startProgram)
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
            LengthAtr = CreateParameter("CCI Length", 21, 7, 48, 7, "Indicator");
            CoefAtr = CreateParameter("Coef Atr", 1, 1m, 10, 1, "Indicator");

            // Create indicator ATR
            _ATR = IndicatorsFactory.CreateIndicatorByName("ATR", name + "Atr", false);
            _ATR = (Aindicator)_tab.CreateCandleIndicator(_ATR, "NewArea");
            ((IndicatorParameterInt)_ATR.Parameters[0]).ValueInt = LengthAtr.ValueInt;
            _ATR.Save();

            // Create indicator Fractal
            _Fractal = IndicatorsFactory.CreateIndicatorByName("Fractal", name + "Fractal", false);
            _Fractal = (Aindicator)_tab.CreateCandleIndicator(_Fractal, "Prime");
            _Fractal.Save();

            // Exit
            CoefProfit = CreateParameter("Coef Profit", 1, 1m, 10, 1, "Exit settings");
            StopCandles = CreateParameter("Stop Candles", 1, 2, 10, 1, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakFractalWithATR_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on Break Fractal With ATR. " +
                "Buy:  " +
                "If an upper fractal has formed on the chart, set BuyAtStop + lastAtr * CoefAtr.ValueDecimal. " +
                "Sell: " +
                "If a lower fractal has formed on the chart, set SellAtStop - lastAtr * CoefAtr.ValueDecimal. " +
                "Exit: " +
                "The stop is placed at the minimum for the period specified for the stop (StopCandles). The profit is equal to the size of the stop * CoefProfit. " +
                "Exit: " +
                "The stop is placed at the maximum for the period specified for the stop (StopCandles). The profit is equal to the size of the stop * CoefProfit.";
        }

        private void BreakFractalWithATR_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ATR.Parameters[0]).ValueInt = LengthAtr.ValueInt;
            _ATR.Save();
            _ATR.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BreakFractalWithATR";
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
            if (candles.Count < LengthAtr.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (StartTradeTime.Value > _tab.TimeServerCurrent ||
                EndTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            for (int i = _Fractal.DataSeries[1].Values.Count - 1; i > -1; i--)
            {
                if (_Fractal.DataSeries[1].Values[i] != 0)
                {
                    _lastUpFract = _Fractal.DataSeries[1].Values[i];
                    _lastIndexUp = i;
                    break;
                }
            }

            for (int i = _Fractal.DataSeries[0].Values.Count - 1; i > -1; i--)
            {
                if (_Fractal.DataSeries[0].Values[i] != 0)
                {
                    _lastDownFract = _Fractal.DataSeries[0].Values[i];
                    _lastIndexDown = i;
                    break;
                }
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
            _lastAtr = _ATR.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 3].Close;

            if (openPositions == null || openPositions.Count == 0)
            {
                _tab.BuyAtStopCancel();
                _tab.SellAtStopCancel();
                // long
                if (Regime.ValueString != "OnlyShort") // если режим не только шорт, то входим в лонг
                {
                    if(_lastUpFract > lastPrice && _lastIndexUp > _lastIndexDown)
                    {
                        decimal priceEnter = lastPrice + _lastAtr * CoefAtr.ValueDecimal;
                    
                        _tab.BuyAtStop(GetVolume(),
                        priceEnter + Slippage.ValueDecimal * _tab.Securiti.PriceStep,
                        priceEnter, StopActivateType.HigherOrEqual);
                    }
                    
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // если режим не только лонг, входим в шорт
                {
                    if(_lastDownFract < lastPrice && _lastIndexDown > _lastIndexUp) 
                    {
                        decimal priceEnter = lastPrice - _lastAtr * CoefAtr.ValueDecimal;

                        _tab.SellAtStop(GetVolume(),
                            priceEnter - Slippage.ValueDecimal * _tab.Securiti.PriceStep,
                            priceEnter, StopActivateType.LowerOrEqyal);
                    }
                }
                return;
            }
        }

        //  Logic close position
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
