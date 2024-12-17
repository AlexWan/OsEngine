using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;

/* Description
trading robot for osengine

The countertrend robot on  On Extreme Volume And Volatility.

Buy:
1. The volume is above the average volume for the period (the number of candles back) in the multivolume times.
2. Volatility is higher than the average volatility for the period by a factor of several.
3. A falling candle
Sell:
1. The volume is higher than the average volume for the period (the number of candles back) by a factor of several.
2. Volatility is higher than the average volatility for the period by a factor of several.
3. The candle is growing

Exit after a certain number of hours.

 */


namespace OsEngine.Robots.AO
{
    [Bot("CountertrendIOnExtremeVolumeAndVolatility")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendIOnExtremeVolumeAndVolatility : BotPanel
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
        private StrategyParameterDecimal MultVolume;
        private StrategyParameterDecimal MultVolatility;
        private StrategyParameterInt CandlesCountVolume;
        private StrategyParameterInt CandlesCountVolatility;
        private StrategyParameterInt VolatilityLength;
        private StrategyParameterDecimal VolatilityCoef;

        // Indicator
        Aindicator _Volume;
        Aindicator _Volatility;

        // The last value of the indicator
        private decimal _lastVolume;
        private decimal _lastVolatility;

        // Exit 
        private StrategyParameterInt ExitCandles;

        public CountertrendIOnExtremeVolumeAndVolatility(string name, StartProgram startProgram) : base(name, startProgram)
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
            CandlesCountVolume = CreateParameter("CandlesCountVolume", 13, 10, 300, 1, "Indicator");
            MultVolume = CreateParameter("MultVolume", 10.0m, 10, 300, 10, "Indicator");
            CandlesCountVolatility = CreateParameter("CandlesCountVolatility", 13, 10, 300, 1, "Indicator");
            MultVolatility = CreateParameter("MultVolatility", 10.0m, 10, 300, 10, "Indicator");
            VolatilityLength = CreateParameter("VolatilityLength", 50, 10, 300, 1, "Indicator");
            VolatilityCoef = CreateParameter("VolatilityCoef", 0.2m, 0.1m, 1, 0.1m, "Indicator");

            // Create indicator Volatility
            _Volatility = IndicatorsFactory.CreateIndicatorByName("VolatilityCandles", name + "VolatilityCandles", false);
            _Volatility = (Aindicator)_tab.CreateCandleIndicator(_Volatility, "NewArea0");
            ((IndicatorParameterInt)_Volatility.Parameters[0]).ValueInt = VolatilityLength.ValueInt;
            ((IndicatorParameterDecimal)_Volatility.Parameters[1]).ValueDecimal = VolatilityCoef.ValueDecimal;
            _Volatility.Save();

            // Create indicator Volume
            _Volume = IndicatorsFactory.CreateIndicatorByName("Volume", name + "Volume", false);
            _Volume = (Aindicator)_tab.CreateCandleIndicator(_Volume, "NewArea");
            _Volume.Save();

            // Exit
            ExitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendIOnExtremeVolumeAndVolatility_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The countertrend robot on  On Extreme Volume And Volatility. " +
                "Buy: " +
                "1. The volume is above the average volume for the period (the number of candles back) in the multivolume times. " +
                "2. Volatility is higher than the average volatility for the period by a factor of several. " +
                "3. A falling candle " +
                "Sell: " +
                "1. The volume is higher than the average volume for the period (the number of candles back) by a factor of several. " +
                "2. Volatility is higher than the average volatility for the period by a factor of several. " +
                "3. The candle is growing " +
                "Exit after a certain number of hours.";
        }

        private void CountertrendIOnExtremeVolumeAndVolatility_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_Volatility.Parameters[0]).ValueInt = VolatilityLength.ValueInt;
            ((IndicatorParameterDecimal)_Volatility.Parameters[1]).ValueDecimal = VolatilityCoef.ValueDecimal;
            _Volatility.Save();
            _Volatility.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendIOnExtremeVolumeAndVolatility";
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
            if (candles.Count < CandlesCountVolume.ValueInt ||
                candles.Count < CandlesCountVolatility.ValueInt ||
                candles.Count < VolatilityLength.ValueInt)
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
            _lastVolume = _Volume.DataSeries[0].Last;
            _lastVolatility = _Volatility.DataSeries[0].Last;

            // The prev value of the indicator

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal prevPrice = candles[candles.Count - 2].Close;

                List<decimal> VolumeValues = _Volume.DataSeries[0].Values;
                List<decimal> VolatilityValues = _Volatility.DataSeries[0].Values;

                // Slippage
                decimal _slippage = Slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (Regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (GetAverage(VolatilityValues, CandlesCountVolatility.ValueInt) * MultVolatility.ValueDecimal < _lastVolatility &&
                        GetAverage(VolumeValues, CandlesCountVolume.ValueInt) * MultVolume.ValueDecimal < _lastVolume &&
                        candles[candles.Count - 1].IsDown)
                    {
                        var time = candles.Last().TimeStart;

                        _tab.BuyAtLimit(GetVolume(), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (GetAverage(VolatilityValues, CandlesCountVolatility.ValueInt) * MultVolatility.ValueDecimal < _lastVolatility &&
                        GetAverage(VolumeValues, CandlesCountVolume.ValueInt) * MultVolume.ValueDecimal < _lastVolume &&
                        candles[candles.Count - 1].IsUp)
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
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (!NeedClosePosition(position, candles))
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    _tab.CloseAtLimit(position, lastPrice - _slippage, position.OpenVolume);
                }
                else // If the direction of the position is sale
                {
                    _tab.CloseAtLimit(position, lastPrice + _slippage, position.OpenVolume);
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

        private decimal GetAverage(List<decimal> Volume, int period)
        {
            decimal sum = 0;

            for (int i = 2; i < period; i++)
            {
                sum += Volume[Volume.Count - i];
            }

            return sum / period;
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

